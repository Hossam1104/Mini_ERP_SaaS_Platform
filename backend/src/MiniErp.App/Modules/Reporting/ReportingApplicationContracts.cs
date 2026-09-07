#pragma warning disable CS1591

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.App.Modules.Reporting;

public sealed class ReportingRequestContext
{
    private ReportingRequestContext(FoundationRequestContext foundation)
    {
        FoundationContext = foundation;
        TenantContext = foundation.TenantContext!;
        TenantId = TenantContext.TenantId;
        ActorId = foundation.ActorId!.Value;
        SessionId = foundation.SessionId!.Value;
    }

    public FoundationRequestContext FoundationContext { get; }
    public TenantContext TenantContext { get; }
    public TenantId TenantId { get; }
    public Guid ActorId { get; }
    public Guid SessionId { get; }
    public string CorrelationId => TenantContext.CorrelationId?.Value ?? string.Empty;

    internal ReportingRequestContext WithTenantContext(TenantContext tenantContext)
    {
        var foundation = FoundationRequestContext.ForTenant(
            FoundationContext.ActorId!.Value,
            FoundationContext.SessionId!.Value,
            tenantContext,
            FoundationContext.Permission,
            FoundationContext.LifecycleState);
        return new ReportingRequestContext(foundation);
    }

    public static bool TryCreate(FoundationRequestContext foundation, out ReportingRequestContext? context)
    {
        context = null;
        if (foundation is null || foundation.TenantContext is null
            || foundation.SecurityProfile is not (FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant)
            || foundation.ActorId is not { } actorId || actorId == Guid.Empty
            || foundation.SessionId is not { } sessionId || sessionId == Guid.Empty) return false;
        context = new ReportingRequestContext(foundation);
        return true;
    }
}

public enum ReportingResultState { Fresh = 1, Stale = 2, Partial = 3, Unavailable = 4, Failed = 5, Unknown = 6, Pending = 7, Denied = 8 }
public enum ReportingJobStatus { Queued = 1, Running = 2, Completed = 3, Failed = 4, Unknown = 5 }
public enum ReportingScheduleStatus { Disabled = 1, Enabled = 2 }
public enum ReportingImplementationState { IMPLEMENTABLE_NOW = 1, SOURCE_CAPABILITY_UNAVAILABLE = 2, OPEN_PRODUCTION_POLICY_ONLY = 3, NOT_IN_PD042 = 4 }

internal static class ReportingTimeSemantics
{
    internal static DateOnly DefaultAsOf(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    internal static DateTimeOffset? AggregateDataAsOf(IReadOnlyList<ReportingSourceEvidence> sources)
    {
        var values = sources.Select(item => item.DataAsOf).ToArray();
        return values.Length > 0 && values.All(item => item.HasValue)
            ? values.Min(item => item!.Value)
            : null;
    }

    internal static ReportingResultState AggregateState(IReadOnlyList<ReportingSourceEvidence> sources, ReportingResultState state) =>
        state == ReportingResultState.Fresh && sources.Any(item => !item.DataAsOf.HasValue)
            ? ReportingResultState.Unknown
            : state;
}

public sealed record ReportingQuery(
    Guid? CompanyId = null,
    Guid? BranchId = null,
    Guid? WarehouseId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    DateOnly? AsOfDate = null,
    string? Status = null,
    string? CurrencyCode = null,
    string? SortBy = null,
    string? SortDirection = null,
    int Page = 1,
    int PageSize = 100)
{
    public TenantWorkScopeRequest ToScopeRequest() => WarehouseId is { } warehouse
        ? TenantWorkScopeRequest.ForWarehouse(CompanyId ?? throw new ArgumentException("CompanyId is required for a Warehouse scope."), BranchId ?? throw new ArgumentException("BranchId is required for a Warehouse scope."), warehouse)
        : BranchId is { } branch
            ? TenantWorkScopeRequest.ForBranch(CompanyId ?? throw new ArgumentException("CompanyId is required for a Branch scope."), branch)
            : CompanyId is { } company
                ? TenantWorkScopeRequest.ForCompany(company)
                : TenantWorkScopeRequest.TenantWide();
}

public sealed record ReportingDefinition(
    string Code,
    string Name,
    string ArabicName,
    string Domain,
    string DefinitionVersion,
    string SourceOwnership,
    string ReconciliationPath,
    string[] AllowedFilters,
    bool ExportEnabled,
    bool SchedulingEnabled,
    bool RequiresCompany,
    bool PendingDecision,
    string? PendingDecisionCode = null,
    ReportingImplementationState ImplementationState = ReportingImplementationState.IMPLEMENTABLE_NOW);

public sealed record ReportingColumn(string Key, string Label, string ArabicLabel, string DataType);
public sealed record ReportingLineage(string SourceDomain, string SourceReference, string SourceStatus, string? SourceVersion, string? ReconciliationReference);
public sealed record ReportingRow(string Key, IReadOnlyDictionary<string, string?> Values, IReadOnlyList<ReportingLineage> Lineage);
public sealed record ReportingSourceEvidence(string SourceDomain, string Status, string? SourceVersion, DateTimeOffset? DataAsOf, string? Detail = null);
public sealed record ReportingResultMetadata(
    Guid ResultId,
    Guid TenantId,
    string Scope,
    string ReportCode,
    string DefinitionVersion,
    IReadOnlyDictionary<string, string?> Parameters,
    IReadOnlyList<ReportingSourceEvidence> Sources,
    DateTimeOffset GeneratedAt,
    DateTimeOffset? DataAsOf,
    ReportingResultState State,
    string Freshness,
    string ReconciliationStatus,
    string? ReconciliationOwner,
    string CorrelationId,
    string? Explanation,
    bool IsProjected = false);
public sealed record ReportingResult(ReportingDefinition Definition, ReportingResultMetadata Metadata, IReadOnlyList<ReportingColumn> Columns, IReadOnlyList<ReportingRow> Rows, int TotalRows);
public sealed record ReportingJobRecord(Guid JobId, Guid TenantId, Guid ActorId, string ReportCode, string RequestFingerprint, ReportingJobStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? ArtifactId, string? FailureCode, ReportingResultMetadata? ResultMetadata, string OrganizationScope = "Tenant");
public sealed record ReportingArtifactRecord(Guid ArtifactId, Guid TenantId, Guid ActorId, string ReportCode, Guid PrivateObjectId, string FileName, string ContentType, DateTimeOffset CreatedAt, Guid JobId, string OrganizationScope = "Tenant");
public sealed record ReportingArtifactAccess(ReportingArtifactRecord Artifact, byte[] Content);
public sealed record ReportingScheduleRecord(Guid ScheduleId, Guid TenantId, Guid ActorId, string ReportCode, ReportingQuery Query, string Recurrence, string TimeZone, ReportingScheduleStatus Status, string DestinationKind, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, byte[] Version, string OrganizationScope = "Tenant");

public sealed record ReportingOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static ReportingOperationResult<T> Success(T value) => new(true, "succeeded", value);
    public static ReportingOperationResult<T> Failure(string code) => new(false, code, default);
}

public sealed class ReportingRuntimeStore
{
    private readonly object sync = new();
    private readonly TimeProvider clock;
    private readonly Dictionary<Guid, ReportingJobRecord> jobs = [];
    private readonly Dictionary<Guid, ReportingArtifactRecord> artifacts = [];
    private readonly Dictionary<Guid, ReportingScheduleRecord> schedules = [];
    private readonly Dictionary<(Guid TenantId, Guid ActorId, string Key), (string Fingerprint, Guid JobId)> idempotency = [];
    private readonly Dictionary<(Guid TenantId, Guid ActorId, string Key), (string Fingerprint, Guid ScheduleId)> scheduleIdempotency = [];
    private readonly Dictionary<(Guid TenantId, Guid ActorId, Guid ScheduleId, string Key), (string Fingerprint, ReportingScheduleRecord Schedule)> scheduleUpdateIdempotency = [];

    public ReportingRuntimeStore(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;

    public ReportingJobRecord? FindJob(Guid tenantId, Guid actorId, Guid id) { lock (sync) return jobs.TryGetValue(id, out var value) && value.TenantId == tenantId && value.ActorId == actorId ? value : null; }
    public ReportingJobRecord? FindJob(Guid tenantId, Guid id) { lock (sync) return jobs.TryGetValue(id, out var value) && value.TenantId == tenantId ? value : null; }
    public ReportingJobRecord? FindJobByIdempotency(Guid tenantId, Guid actorId, string key, string fingerprint, out bool conflict)
    {
        lock (sync)
        {
            conflict = false;
            if (!idempotency.TryGetValue((tenantId, actorId, key), out var value)) return null;
            if (!string.Equals(value.Fingerprint, fingerprint, StringComparison.Ordinal)) { conflict = true; return null; }
            return jobs.TryGetValue(value.JobId, out var job) ? job : null;
        }
    }
    public void SaveJob(ReportingJobRecord job, string? key = null) { lock (sync) { jobs[job.JobId] = job; if (!string.IsNullOrWhiteSpace(key)) idempotency[(job.TenantId, job.ActorId, key)] = (job.RequestFingerprint, job.JobId); } }
    public void SaveArtifact(ReportingArtifactRecord artifact) { lock (sync) artifacts[artifact.ArtifactId] = artifact; }
    public ReportingArtifactRecord? FindArtifact(Guid tenantId, Guid actorId, Guid id) { lock (sync) return artifacts.TryGetValue(id, out var value) && value.TenantId == tenantId && value.ActorId == actorId ? value : null; }
    public ReportingArtifactRecord? FindArtifact(Guid tenantId, Guid id) { lock (sync) return artifacts.TryGetValue(id, out var value) && value.TenantId == tenantId ? value : null; }
    public IReadOnlyList<ReportingScheduleRecord> ListSchedules(Guid tenantId, Guid actorId) { lock (sync) return schedules.Values.Where(item => item.TenantId == tenantId && item.ActorId == actorId).OrderBy(item => item.CreatedAt).ToArray(); }
    public IReadOnlyList<ReportingScheduleRecord> ListSchedules(Guid tenantId) { lock (sync) return schedules.Values.Where(item => item.TenantId == tenantId).OrderBy(item => item.CreatedAt).ToArray(); }
    public ReportingScheduleRecord? FindSchedule(Guid tenantId, Guid actorId, Guid id) { lock (sync) return schedules.TryGetValue(id, out var value) && value.TenantId == tenantId && value.ActorId == actorId ? value : null; }
    public ReportingScheduleRecord? FindSchedule(Guid tenantId, Guid id) { lock (sync) return schedules.TryGetValue(id, out var value) && value.TenantId == tenantId ? value : null; }
    public ReportingScheduleRecord? FindScheduleByIdempotency(Guid tenantId, Guid actorId, string key, string fingerprint, out bool conflict)
    {
        lock (sync)
        {
            conflict = false;
            if (!scheduleIdempotency.TryGetValue((tenantId, actorId, key), out var value)) return null;
            if (!string.Equals(value.Fingerprint, fingerprint, StringComparison.Ordinal)) { conflict = true; return null; }
            return schedules.TryGetValue(value.ScheduleId, out var schedule) ? schedule : null;
        }
    }
    public void SaveSchedule(ReportingScheduleRecord schedule, string? key = null, string? fingerprint = null) { lock (sync) { schedules[schedule.ScheduleId] = schedule; if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(fingerprint)) scheduleIdempotency[(schedule.TenantId, schedule.ActorId, key)] = (fingerprint, schedule.ScheduleId); } }
    public ReportingScheduleRecord? TryUpdateSchedule(Guid tenantId, Guid actorId, Guid id, ReportingScheduleStatus status, byte[] expectedVersion, string key, string fingerprint, out string code)
    {
        lock (sync)
        {
            code = "schedule_not_found";
            if (!schedules.TryGetValue(id, out var existing) || existing.TenantId != tenantId || existing.ActorId != actorId) return null;
            if (scheduleUpdateIdempotency.TryGetValue((tenantId, actorId, id, key), out var replay))
            {
                if (!string.Equals(replay.Fingerprint, fingerprint, StringComparison.Ordinal)) { code = "idempotency_conflict"; return null; }
                code = "succeeded";
                return replay.Schedule;
            }
            if (!existing.Version.SequenceEqual(expectedVersion)) { code = "concurrency_conflict"; return null; }
            var next = existing with { Status = status, UpdatedAt = clock.GetUtcNow(), Version = [.. expectedVersion, 1] };
            schedules[id] = next;
            scheduleUpdateIdempotency[(tenantId, actorId, id, key)] = (fingerprint, next);
            code = "succeeded";
            return next;
        }
    }

    public ReportingScheduleRecord? TryUpdateSchedule(Guid tenantId, Guid id, ReportingScheduleStatus status, byte[] expectedVersion, string key, string fingerprint, out string code)
    {
        lock (sync)
        {
            code = "schedule_not_found";
            if (!schedules.TryGetValue(id, out var existing) || existing.TenantId != tenantId) return null;
            var identity = (tenantId, existing.ActorId, id, key);
            if (scheduleUpdateIdempotency.TryGetValue(identity, out var replay))
            {
                if (!string.Equals(replay.Fingerprint, fingerprint, StringComparison.Ordinal)) { code = "idempotency_conflict"; return null; }
                code = "succeeded";
                return replay.Schedule;
            }
            if (!existing.Version.SequenceEqual(expectedVersion)) { code = "concurrency_conflict"; return null; }
            var next = existing with { Status = status, UpdatedAt = clock.GetUtcNow(), Version = [.. expectedVersion, 1] };
            schedules[id] = next;
            scheduleUpdateIdempotency[identity] = (fingerprint, next);
            code = "succeeded";
            return next;
        }
    }
}

public interface IReportingService
{
    IReadOnlyList<ReportingDefinition> Catalogue();
    Task<ReportingResult?> ExecuteAsync(ReportingRequestContext context, string reportCode, ReportingQuery query, CancellationToken cancellationToken = default);
    Task<ReportingOperationResult<ReportingJobRecord>> CreateExportJobAsync(ReportingRequestContext context, string reportCode, ReportingQuery query, string idempotencyKey, CancellationToken cancellationToken = default);
    ReportingJobRecord? FindJob(ReportingRequestContext context, Guid jobId);
    Task<ReportingOperationResult<ReportingArtifactAccess>> ReadArtifactAsync(ReportingRequestContext context, Guid artifactId, CancellationToken cancellationToken = default);
    IReadOnlyList<ReportingScheduleRecord> ListSchedules(ReportingRequestContext context);
    Task<ReportingOperationResult<ReportingScheduleRecord>> CreateScheduleAsync(ReportingRequestContext context, string reportCode, ReportingQuery query, string recurrence, string timeZone, string destinationKind, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<ReportingOperationResult<ReportingScheduleRecord>> SetScheduleStatusAsync(ReportingRequestContext context, Guid scheduleId, ReportingScheduleStatus status, byte[] expectedVersion, string idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed class ReportingAuthorizationService
{
    private readonly IFinanceCompanyProvider companies;
    private readonly IOrganizationScopeOwnershipResolver? scopeOwnership;

    public ReportingAuthorizationService(
        IFinanceCompanyProvider companies,
        IOrganizationScopeOwnershipResolver? scopeOwnership = null)
    {
        this.companies = companies;
        this.scopeOwnership = scopeOwnership;
    }

    public ReportingOperationResult<ReportingRequestContext> Resolve(FoundationRequestContext foundation, string operationId, ReportingDefinition? definition, ReportingQuery? query)
    {
        if (!ReportingRequestContext.TryCreate(foundation, out var context) || context is null)
            return ReportingOperationResult<ReportingRequestContext>.Failure(foundation.SecurityProfile == FoundationSecurityProfile.Anonymous ? "authentication_required" : "access_denied");
        if (!FoundationOperationCatalog.TryGet(operationId, out var descriptor) || descriptor.ExactPermissionCode is null || !string.Equals(foundation.Permission, descriptor.ExactPermissionCode, StringComparison.Ordinal))
            return ReportingOperationResult<ReportingRequestContext>.Failure("permission_denied");
        if (definition is null || query is null) return ReportingOperationResult<ReportingRequestContext>.Success(context);
        var normalized = NormalizeQuery(context, definition, query);
        if (!normalized.Succeeded || normalized.Value is null) return ReportingOperationResult<ReportingRequestContext>.Failure(normalized.Code);
        query = normalized.Value;
        if (query.BranchId.HasValue && !query.CompanyId.HasValue || query.WarehouseId.HasValue && !query.BranchId.HasValue) return ReportingOperationResult<ReportingRequestContext>.Failure("scope_invalid");
        if (scopeOwnership is not null && (query.CompanyId.HasValue || query.BranchId.HasValue || query.WarehouseId.HasValue))
        {
            try
            {
                var resolution = scopeOwnership.Resolve(context.TenantContext, query.ToScopeRequest());
                if (!resolution.Allowed || resolution.Scope is null) return ReportingOperationResult<ReportingRequestContext>.Failure(ScopeFailureCode(resolution.SafeReason));
                if (definition.RequiresCompany && resolution.Scope.CompanyId is null) return ReportingOperationResult<ReportingRequestContext>.Failure("company_required");
                return ReportingOperationResult<ReportingRequestContext>.Success(context);
            }
            catch (ArgumentException)
            {
                return ReportingOperationResult<ReportingRequestContext>.Failure("scope_invalid");
            }
        }
        if (query.CompanyId is { } target)
        {
            if (!companies.List(context.TenantId).Any(item => item.CompanyId == target && item.IsActive)) return ReportingOperationResult<ReportingRequestContext>.Failure("company_scope_denied");
            if (context.TenantContext.Scope is { } scope)
            {
                var value = scope.Value;
                if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase) && (!Guid.TryParse(value["Company:".Length..], out var scoped) || scoped != target)) return ReportingOperationResult<ReportingRequestContext>.Failure("company_scope_denied");
                if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase) && (!Guid.TryParse(value["Branch:".Length..], out var branch) || query.BranchId is not { } requested || requested != branch)) return ReportingOperationResult<ReportingRequestContext>.Failure("branch_scope_denied");
            }
        }
        return ReportingOperationResult<ReportingRequestContext>.Success(context);
    }

    public ReportingOperationResult<ReportingQuery> NormalizeQuery(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query)
    {
        if (!definition.RequiresCompany) return ReportingOperationResult<ReportingQuery>.Success(query);
        if (scopeOwnership is ICurrentOrganizationScopeResolver current)
        {
            if (query.CompanyId is null)
            {
                var currentResolution = current.ResolveCurrent(context.TenantContext);
                if (!currentResolution.Allowed || currentResolution.Scope is not { } currentScope)
                    return ReportingOperationResult<ReportingQuery>.Failure(ScopeFailureCode(currentResolution.SafeReason));
                if (currentScope.CompanyId is not { } currentCompany)
                    return ReportingOperationResult<ReportingQuery>.Failure("company_required");

                query = query with
                {
                    CompanyId = currentCompany,
                    BranchId = query.BranchId ?? currentScope.BranchId,
                    WarehouseId = query.WarehouseId ?? currentScope.WarehouseId
                };
            }

            try
            {
                var resolution = scopeOwnership.Resolve(context.TenantContext, query.ToScopeRequest());
                if (!resolution.Allowed || resolution.Scope is not { } resolvedScope)
                    return ReportingOperationResult<ReportingQuery>.Failure(ScopeFailureCode(resolution.SafeReason));
                if (resolvedScope.CompanyId is null)
                    return ReportingOperationResult<ReportingQuery>.Failure("company_required");
                return ReportingOperationResult<ReportingQuery>.Success(query);
            }
            catch (ArgumentException)
            {
                return ReportingOperationResult<ReportingQuery>.Failure("scope_invalid");
            }
        }
        var options = companies.List(context.TenantId);
        FinanceCompanyOption? selected = null;
        if (context.TenantContext.Scope is { } scope)
        {
            var value = scope.Value;
            if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(value["Company:".Length..], out var companyId)) selected = options.SingleOrDefault(item => item.CompanyId == companyId && item.IsActive);
            else if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(value["Branch:".Length..], out var branchId)) selected = options.SingleOrDefault(item => item.BranchId == branchId && item.IsActive);
        }
        else if (options.Count == 1)
        {
            selected = options[0];
        }
        if (query.CompanyId is null)
        {
            if (selected is null) return ReportingOperationResult<ReportingQuery>.Failure("company_required");
            query = query with { CompanyId = selected.CompanyId, BranchId = query.BranchId ?? selected.BranchId };
        }
        if (selected is not null && query.CompanyId != selected.CompanyId) return ReportingOperationResult<ReportingQuery>.Failure("company_scope_denied");
        if (selected?.BranchId is { } selectedBranch && query.BranchId is { } requestedBranch && requestedBranch != selectedBranch) return ReportingOperationResult<ReportingQuery>.Failure("branch_scope_denied");
        return ReportingOperationResult<ReportingQuery>.Success(query);
    }

    private static string ScopeFailureCode(string safeReason) => safeReason.Contains("warehouse", StringComparison.OrdinalIgnoreCase)
        ? "warehouse_scope_denied"
        : safeReason.Contains("branch", StringComparison.OrdinalIgnoreCase)
            ? "branch_scope_denied"
            : safeReason.Contains("company", StringComparison.OrdinalIgnoreCase)
                ? "company_scope_denied"
                : "scope_denied";
}

public sealed class ReportingService : IReportingService
{
    private readonly IFinanceMesp135Persistence finance;
    private readonly IInventoryValuationPersistence inventory;
    private readonly IPurchaseOrderPersistence purchaseOrders;
    private readonly IGoodsReceiptPersistence goodsReceipts;
    private readonly ISalesPersistence sales;
    private readonly IFinanceSettlementReportingReadPort settlementReporting;
    private readonly IPurchaseInvoiceMatchReportingReadPort matchReporting;
    private readonly ISalesFulfillmentReportingReadPort fulfillmentReporting;
    private readonly ISalesCustomerReturnReportingReadPort customerReturnReporting;
    private readonly FoundationAuditCoordinator audit;
    private readonly IFoundationAuditEvidenceReader auditReader;
    private readonly IOrganizationScopeOwnershipResolver scopeOwnership;
    private readonly IPrivateObjectStorage privateFiles;
    private readonly ReportingRuntimeStore runtime;
    private readonly IReadOnlyDictionary<string, ReportingDefinition> definitions;
    private readonly TimeProvider clock;
    private DateOnly DefaultAsOf => ReportingTimeSemantics.DefaultAsOf(clock);

    public ReportingService(IFinanceMesp135Persistence finance, IInventoryValuationPersistence inventory, IPurchaseOrderPersistence purchaseOrders, IGoodsReceiptPersistence goodsReceipts, ISalesPersistence sales, IFinanceSettlementReportingReadPort settlementReporting, IPurchaseInvoiceMatchReportingReadPort matchReporting, ISalesFulfillmentReportingReadPort fulfillmentReporting, ISalesCustomerReturnReportingReadPort customerReturnReporting, FoundationAuditCoordinator audit, IFoundationAuditEvidenceReader auditReader, IOrganizationScopeOwnershipResolver scopeOwnership, IPrivateObjectStorage privateFiles, ReportingRuntimeStore runtime, TimeProvider? clock = null)
    {
        this.finance = finance; this.inventory = inventory; this.purchaseOrders = purchaseOrders; this.goodsReceipts = goodsReceipts; this.sales = sales; this.settlementReporting = settlementReporting; this.matchReporting = matchReporting; this.fulfillmentReporting = fulfillmentReporting; this.customerReturnReporting = customerReturnReporting; this.audit = audit; this.auditReader = auditReader; this.scopeOwnership = scopeOwnership; this.privateFiles = privateFiles; this.runtime = runtime;
        this.clock = clock ?? TimeProvider.System;
        definitions = BuildDefinitions().ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ReportingDefinition> Catalogue() => definitions.Values.OrderBy(item => item.Domain).ThenBy(item => item.Code).ToArray();

    public async Task<ReportingResult?> ExecuteAsync(ReportingRequestContext context, string reportCode, ReportingQuery query, CancellationToken cancellationToken = default)
    {
        if (!definitions.TryGetValue(reportCode, out var definition)) return null;
        var requestedQuery = query;
        var now = clock.GetUtcNow();
        if (!TryResolveEffectiveScope(context, definition, query, out var scopedContext, out var scopedQuery, out var scopeCode))
            return EmptyResult(context, definition, query, ReportingResultState.Denied, scopeCode, "The requested organization scope is not authorized.", now);
        context = scopedContext!;
        query = scopedQuery;
        ValidateQuery(definition, query, requestedQuery);
        if (definition.ImplementationState == ReportingImplementationState.SOURCE_CAPABILITY_UNAVAILABLE)
            return EmptyResult(context, definition, query, ReportingResultState.Unavailable, definition.PendingDecisionCode ?? "source_capability_unavailable", "The authoritative source read capability is not available on the accepted baseline.", now);
        if (definition.ImplementationState == ReportingImplementationState.NOT_IN_PD042)
            return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "not_in_pd042", "This report is not part of the approved Release 1 catalogue.", now);
        if (definition.PendingDecision || definition.ImplementationState == ReportingImplementationState.OPEN_PRODUCTION_POLICY_ONLY) return EmptyResult(context, definition, query, ReportingResultState.Pending, definition.PendingDecisionCode ?? "pending_decision", "This branch remains conditional on the approved production decision.", now);
        try
        {
            var result = definition.Code switch
            {
                "finance.trial-balance" => await FinanceTrialBalanceAsync(context, definition, query, cancellationToken),
                "finance.general-ledger" => await FinanceGeneralLedgerAsync(context, definition, query, cancellationToken),
                "finance.ap-aging" or "finance.ar-aging" => await FinanceAgingAsync(context, definition, query, cancellationToken),
                "finance.cash-movement" => await FinanceCashMovementAsync(context, definition, query, cancellationToken),
                "finance.profit-loss" or "finance.balance-sheet" => await FinanceStatementAsync(context, definition, query, cancellationToken),
                "finance.reconciliation" => await FinanceReconciliationAsync(context, definition, query, cancellationToken),
                "finance.tax-summary" => await FinanceTaxSummaryAsync(context, definition, query, cancellationToken),
                "inventory.valuation" => await InventoryValuationAsync(context, definition, query, cancellationToken),
                "inventory.stock-movements" => await InventoryMovementsAsync(context, definition, query, cancellationToken),
                "inventory.stock-balance" => await InventoryStockBalanceAsync(context, definition, query, cancellationToken),
                "procurement.open-orders" => await ProcurementOrdersAsync(context, definition, query, cancellationToken),
                "procurement.receipts" => await ProcurementReceiptsAsync(context, definition, query, cancellationToken),
                "procurement.match-exceptions" => await ProcurementMatchExceptionsAsync(context, definition, query, cancellationToken),
                "sales.orders" => await SalesOrdersAsync(context, definition, query, cancellationToken),
                "sales.fulfillment" => await SalesFulfillmentAsync(context, definition, query, cancellationToken),
                "sales.returns-credits" => await SalesReturnsCreditsAsync(context, definition, query, cancellationToken),
                "operations.audit-activity" => await AuditActivityAsync(context, definition, query, cancellationToken),
                "operations.source-health" => SourceHealth(context, definition, query),
                _ => EmptyResult(context, definition, query, ReportingResultState.Failed, "report_not_implemented", "The report adapter is not available.", now)
            };
            await audit.RecordAsync(context.FoundationContext, "reporting.report.execute", context.CorrelationId, FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, operationVersion: definition.DefinitionVersion, cancellationToken: cancellationToken);
            return result;
        }
        catch (ArgumentException)
        {
            await audit.RecordAsync(context.FoundationContext, "reporting.report.execute", context.CorrelationId, FoundationAuditDecision.Denied, FoundationAuditReason.ValidationFailed, operationVersion: definition.DefinitionVersion, cancellationToken: cancellationToken);
            return EmptyResult(context, definition, query, ReportingResultState.Failed, "validation_failed", "The report parameters are not valid.", now);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            await audit.RecordAsync(context.FoundationContext, "reporting.report.execute", context.CorrelationId, FoundationAuditDecision.EffectFailed, FoundationAuditReason.InternalFailure, operationVersion: definition.DefinitionVersion, cancellationToken: cancellationToken);
            return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "source_unavailable", "An authoritative source is temporarily unavailable.", now);
        }
    }

    public async Task<ReportingOperationResult<ReportingJobRecord>> CreateExportJobAsync(ReportingRequestContext context, string reportCode, ReportingQuery query, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128) return ReportingOperationResult<ReportingJobRecord>.Failure("idempotency_key_invalid");
        if (!definitions.TryGetValue(reportCode, out var definition)) return ReportingOperationResult<ReportingJobRecord>.Failure("report_not_found");
        if (!definition.ExportEnabled) return ReportingOperationResult<ReportingJobRecord>.Failure("export_not_available");
        var requestedQuery = query;
        if (!TryResolveEffectiveScope(context, definition, query, out var scopedContext, out var scopedQuery, out var scopeCode)) return ReportingOperationResult<ReportingJobRecord>.Failure(scopeCode);
        context = scopedContext!;
        query = scopedQuery;
        ValidateQuery(definition, query, requestedQuery);
        var fingerprint = Fingerprint(reportCode, query, definition.DefinitionVersion);
        var replay = runtime.FindJobByIdempotency(context.TenantId.Value, context.ActorId, idempotencyKey.Trim(), fingerprint, out var conflict);
        if (conflict) return ReportingOperationResult<ReportingJobRecord>.Failure("idempotency_conflict");
        if (replay is not null) return ReportingOperationResult<ReportingJobRecord>.Success(replay);
        var jobId = Guid.NewGuid();
        var now = clock.GetUtcNow();
        var job = new ReportingJobRecord(jobId, context.TenantId.Value, context.ActorId, reportCode, fingerprint, ReportingJobStatus.Running, now, now, null, null, null, StoredScopeText(query));
        runtime.SaveJob(job, idempotencyKey.Trim());
        await audit.RecordAsync(context.FoundationContext, "reporting.report.export", context.CorrelationId, FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, idempotencyKey.Trim(), definition.DefinitionVersion, cancellationToken: cancellationToken);
        var result = await ExecuteAsync(context, reportCode, query, cancellationToken);
        if (result is null)
        {
            job = job with { Status = ReportingJobStatus.Failed, UpdatedAt = clock.GetUtcNow(), FailureCode = "report_not_found" }; runtime.SaveJob(job);
            return ReportingOperationResult<ReportingJobRecord>.Success(job);
        }
        if (result.Metadata.State is ReportingResultState.Failed or ReportingResultState.Unavailable or ReportingResultState.Pending or ReportingResultState.Unknown or ReportingResultState.Denied)
        {
            job = job with { Status = ReportingJobStatus.Failed, UpdatedAt = clock.GetUtcNow(), FailureCode = result.Metadata.Explanation ?? result.Metadata.State.ToString(), ResultMetadata = result.Metadata };
            runtime.SaveJob(job);
            await audit.RecordAsync(context.FoundationContext, "reporting.report.export", context.CorrelationId, FoundationAuditDecision.EffectFailed, FoundationAuditReason.InternalFailure, idempotencyKey.Trim(), definition.DefinitionVersion, cancellationToken: cancellationToken);
            return ReportingOperationResult<ReportingJobRecord>.Success(job);
        }
        var scope = scopeOwnership.Resolve(context.TenantContext, query.ToScopeRequest());
        if (!scope.Allowed || scope.Scope is null)
        {
            job = job with { Status = ReportingJobStatus.Failed, UpdatedAt = clock.GetUtcNow(), FailureCode = "scope_denied" }; runtime.SaveJob(job);
            return ReportingOperationResult<ReportingJobRecord>.Success(job);
        }
        var csv = ToCsv(result);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var file = await privateFiles.StoreAsync(context.TenantContext, scope.Scope, $"{reportCode}-{jobId:N}.csv", "text/csv", stream, cancellationToken: cancellationToken);
        var artifact = new ReportingArtifactRecord(Guid.NewGuid(), context.TenantId.Value, context.ActorId, reportCode, file.ObjectId, $"{reportCode}-{jobId:N}.csv", "text/csv", clock.GetUtcNow(), jobId, StoredScopeText(query));
        runtime.SaveArtifact(artifact);
        job = job with { Status = result.Metadata.State is ReportingResultState.Failed or ReportingResultState.Unavailable ? ReportingJobStatus.Failed : ReportingJobStatus.Completed, UpdatedAt = clock.GetUtcNow(), ArtifactId = artifact.ArtifactId, FailureCode = result.Metadata.Explanation, ResultMetadata = result.Metadata };
        runtime.SaveJob(job);
        await audit.RecordAsync(context.FoundationContext, "reporting.report.export", context.CorrelationId, job.Status == ReportingJobStatus.Completed ? FoundationAuditDecision.Allowed : FoundationAuditDecision.EffectFailed, job.Status == ReportingJobStatus.Completed ? FoundationAuditReason.Allowed : FoundationAuditReason.EffectFailed, idempotencyKey.Trim(), definition.DefinitionVersion, cancellationToken: cancellationToken);
        return ReportingOperationResult<ReportingJobRecord>.Success(job);
    }

    public ReportingJobRecord? FindJob(ReportingRequestContext context, Guid jobId)
    {
        var job = runtime.FindJob(context.TenantId.Value, jobId);
        return job is not null && IsStoredScopeAuthorized(context, job.OrganizationScope) ? job : null;
    }

    public async Task<ReportingOperationResult<ReportingArtifactAccess>> ReadArtifactAsync(ReportingRequestContext context, Guid artifactId, CancellationToken cancellationToken = default)
    {
        var artifact = runtime.FindArtifact(context.TenantId.Value, artifactId);
        if (artifact is null || !IsStoredScopeAuthorized(context, artifact.OrganizationScope)) return ReportingOperationResult<ReportingArtifactAccess>.Failure("artifact_not_found");
        var effectiveContext = ContextForStoredScope(context, artifact.OrganizationScope);
        if (effectiveContext is null) return ReportingOperationResult<ReportingArtifactAccess>.Failure("artifact_not_found");
        var read = await privateFiles.ReadAsync(effectiveContext.TenantContext, artifact.PrivateObjectId, cancellationToken);
        if (!read.Allowed || read.Content is null) return ReportingOperationResult<ReportingArtifactAccess>.Failure("artifact_not_available");
        await audit.RecordAsync(context.FoundationContext, "reporting.artifact.read", context.CorrelationId, FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, operationVersion: "1.0", cancellationToken: cancellationToken);
        return ReportingOperationResult<ReportingArtifactAccess>.Success(new ReportingArtifactAccess(artifact, read.Content));
    }

    public IReadOnlyList<ReportingScheduleRecord> ListSchedules(ReportingRequestContext context) => runtime.ListSchedules(context.TenantId.Value).Where(item => IsStoredScopeAuthorized(context, item.OrganizationScope)).ToArray();

    public async Task<ReportingOperationResult<ReportingScheduleRecord>> CreateScheduleAsync(ReportingRequestContext context, string reportCode, ReportingQuery query, string recurrence, string timeZone, string destinationKind, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128) return ReportingOperationResult<ReportingScheduleRecord>.Failure("idempotency_key_invalid");
        if (!definitions.TryGetValue(reportCode, out var definition)) return ReportingOperationResult<ReportingScheduleRecord>.Failure("report_not_found");
        if (!definition.SchedulingEnabled) return ReportingOperationResult<ReportingScheduleRecord>.Failure("scheduling_not_available");
        var requestedQuery = query;
        if (!TryResolveEffectiveScope(context, definition, query, out var scopedContext, out var scopedQuery, out var scopeCode)) return ReportingOperationResult<ReportingScheduleRecord>.Failure(scopeCode);
        context = scopedContext!;
        query = scopedQuery;
        if (string.IsNullOrWhiteSpace(recurrence) || recurrence.Length > 64 || string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > 64) return ReportingOperationResult<ReportingScheduleRecord>.Failure("schedule_parameters_invalid");
        if (!string.Equals(destinationKind, "local-test-sink", StringComparison.Ordinal)) return ReportingOperationResult<ReportingScheduleRecord>.Failure("distribution_provider_not_approved");
        ValidateQuery(definition, query, requestedQuery);
        var fingerprint = Fingerprint($"schedule:{reportCode}:{recurrence}:{timeZone}:{destinationKind}", query, definition.DefinitionVersion);
        var replay = runtime.FindScheduleByIdempotency(context.TenantId.Value, context.ActorId, idempotencyKey.Trim(), fingerprint, out var conflict);
        if (conflict) return ReportingOperationResult<ReportingScheduleRecord>.Failure("idempotency_conflict");
        if (replay is not null) return ReportingOperationResult<ReportingScheduleRecord>.Success(replay);
        var now = clock.GetUtcNow();
        var schedule = new ReportingScheduleRecord(Guid.NewGuid(), context.TenantId.Value, context.ActorId, reportCode, query, recurrence.Trim(), timeZone.Trim(), ReportingScheduleStatus.Disabled, destinationKind, now, now, [1], StoredScopeText(query));
        runtime.SaveSchedule(schedule, idempotencyKey.Trim(), fingerprint);
        await audit.RecordAsync(context.FoundationContext, "reporting.schedule.create", context.CorrelationId, FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, idempotencyKey.Trim(), definition.DefinitionVersion, cancellationToken: cancellationToken);
        return ReportingOperationResult<ReportingScheduleRecord>.Success(schedule);
    }

    public async Task<ReportingOperationResult<ReportingScheduleRecord>> SetScheduleStatusAsync(ReportingRequestContext context, Guid scheduleId, ReportingScheduleStatus status, byte[] expectedVersion, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128) return ReportingOperationResult<ReportingScheduleRecord>.Failure("idempotency_key_invalid");
        var existing = runtime.FindSchedule(context.TenantId.Value, scheduleId);
        if (existing is null || !IsStoredScopeAuthorized(context, existing.OrganizationScope)) return ReportingOperationResult<ReportingScheduleRecord>.Failure("schedule_not_found");
        if (!definitions.TryGetValue(existing.ReportCode, out var definition) || !definition.SchedulingEnabled)
            return ReportingOperationResult<ReportingScheduleRecord>.Failure("scheduling_not_available");
        var fingerprint = Fingerprint($"schedule-update:{scheduleId:D}:{status}:{Convert.ToBase64String(expectedVersion)}", new ReportingQuery(), "1.0");
        var next = runtime.TryUpdateSchedule(context.TenantId.Value, scheduleId, status, expectedVersion, idempotencyKey.Trim(), fingerprint, out var code);
        if (next is null) return ReportingOperationResult<ReportingScheduleRecord>.Failure(code);
        await audit.RecordAsync(context.FoundationContext, "reporting.schedule.update", context.CorrelationId, FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, idempotencyKey.Trim(), operationVersion: "1.0", cancellationToken: cancellationToken);
        return ReportingOperationResult<ReportingScheduleRecord>.Success(next);
    }

    private async Task<ReportingResult> FinanceTrialBalanceAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var fc = FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) ? financeContext : null;
        if (fc is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "finance_context_unavailable", "Finance context is unavailable.", clock.GetUtcNow());
        var report = await finance.QueryTrialBalanceAsync(fc, new FinanceTrialBalanceQuery(query.CompanyId!.Value, query.AsOfDate ?? DefaultAsOf), ct);
        var rows = report.Rows.Select(row => Row(row.AccountId.ToString("D"), new Dictionary<string, string?> { ["account"] = row.AccountCode, ["name"] = row.AccountName, ["type"] = row.AccountType.ToString(), ["opening"] = Amount(row.OpeningBalance), ["debit"] = Amount(row.PeriodDebit), ["credit"] = Amount(row.PeriodCredit), ["closing"] = Amount(row.ClosingBalance), ["currency"] = row.FunctionalCurrencyCode }, "Finance", row.AccountId.ToString("D"), "Posted", report.AsOfDate.ToDateTime(TimeOnly.MinValue), "finance-reconciliation"));
        return Result(context, definition, query, rows, [Column("account", "Account", "الحساب", "text"), Column("name", "Name", "الاسم", "text"), Column("type", "Type", "النوع", "text"), Column("opening", "Opening", "الافتتاحي", "decimal"), Column("debit", "Debit", "مدين", "decimal"), Column("credit", "Credit", "دائن", "decimal"), Column("closing", "Closing", "الإقفال", "decimal"), Column("currency", "Currency", "العملة", "text")], [new("Finance", "Posted", "Finance", report.AsOfDate.ToDateTime(TimeOnly.MinValue))], ReportingResultState.Fresh, "Finance owns posted balances; Reporting does not recalculate them.", "Reconciled");
    }

    private async Task<ReportingResult> FinanceGeneralLedgerAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var fc = FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) ? financeContext : null;
        if (fc is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "finance_context_unavailable", "Finance context is unavailable.", clock.GetUtcNow());
        if (finance is IFinanceReportingReadPort bounded)
        {
            var page = ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection);
            var sourcePage = await bounded.QueryGeneralLedgerPageAsync(fc, new FinanceGeneralLedgerQuery(query.CompanyId!.Value, FromDate: query.FromDate, ToDate: query.ToDate), page, ct);
            return GeneralLedgerPageResult(context, definition, query, sourcePage);
        }
        var lines = await finance.QueryGeneralLedgerAsync(fc, new FinanceGeneralLedgerQuery(query.CompanyId!.Value, FromDate: query.FromDate, ToDate: query.ToDate), ct);
        var rows = lines.Select(row => Row($"{row.JournalId:D}:{row.LineNumber}", new Dictionary<string, string?> { ["date"] = row.PostingDate.ToString("yyyy-MM-dd"), ["journal"] = row.JournalNumber, ["account"] = row.AccountCode, ["name"] = row.AccountName, ["debit"] = Amount(row.FunctionalDebit), ["credit"] = Amount(row.FunctionalCredit), ["running"] = Amount(row.RunningBalance), ["source"] = row.SourceContract, ["currency"] = row.FunctionalCurrencyCode }, "Finance", row.SourceEvidenceId?.ToString("D") ?? row.JournalId.ToString("D"), "Posted", row.PostingDate.ToDateTime(TimeOnly.MinValue), "finance-reconciliation"));
        return Result(context, definition, query, rows, [Column("date", "Posting date", "تاريخ الترحيل", "date"), Column("journal", "Journal", "القيد", "text"), Column("account", "Account", "الحساب", "text"), Column("name", "Name", "الاسم", "text"), Column("debit", "Debit", "مدين", "decimal"), Column("credit", "Credit", "دائن", "decimal"), Column("running", "Running balance", "الرصيد الجاري", "decimal"), Column("source", "Source", "المصدر", "text"), Column("currency", "Currency", "العملة", "text")], [new("Finance", "Posted", "Finance", lines.Count == 0 ? null : lines.Max(item => item.PostingDate).ToDateTime(TimeOnly.MinValue))], ReportingResultState.Fresh, "Finance owns the posted journal and running balance.", "Reconciled");
    }

    private async Task<ReportingResult> FinanceAgingAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var fc = FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) ? financeContext : null;
        if (fc is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "finance_context_unavailable", "Finance context is unavailable.", clock.GetUtcNow());
        var kind = definition.Code.EndsWith("ap-aging", StringComparison.Ordinal) ? FinanceOpenItemKind.Payable : FinanceOpenItemKind.Receivable;
        if (finance is IFinanceReportingReadPort bounded)
        {
            var page = ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection);
            var sourcePage = await bounded.QueryAgingPageAsync(fc, new FinanceAgingReportQuery(query.CompanyId!.Value, query.AsOfDate ?? DefaultAsOf, kind, CurrencyCode: query.CurrencyCode), page, ct);
            return AgingPageResult(context, definition, query, sourcePage);
        }
        var rows = await finance.QueryAgingAsync(fc, new FinanceAgingReportQuery(query.CompanyId!.Value, query.AsOfDate ?? DefaultAsOf, kind, CurrencyCode: query.CurrencyCode), ct);
        var resultRows = rows.Select(row => Row(row.OpenItemId.ToString("D"), new Dictionary<string, string?> { ["reference"] = row.SourceReference, ["documentDate"] = row.DocumentDate.ToString("yyyy-MM-dd"), ["dueDate"] = row.DueDate.ToString("yyyy-MM-dd"), ["bucket"] = row.AgingBucket, ["currency"] = row.CurrencyCode, ["original"] = Amount(row.OriginalAmount), ["allocated"] = Amount(row.AllocatedAmount), ["outstanding"] = Amount(row.OutstandingAmount), ["status"] = row.Status.ToString() }, "Finance", row.OpenItemId.ToString("D"), row.Status.ToString(), row.AsOfDate.ToDateTime(TimeOnly.MinValue), "finance-reconciliation"));
        return Result(context, definition, query, resultRows, [Column("reference", "Reference", "المرجع", "text"), Column("documentDate", "Document date", "تاريخ المستند", "date"), Column("dueDate", "Due date", "تاريخ الاستحقاق", "date"), Column("bucket", "Aging bucket", "شريحة الاستحقاق", "text"), Column("currency", "Currency", "العملة", "text"), Column("original", "Original", "الأصلي", "decimal"), Column("allocated", "Allocated", "المخصص", "decimal"), Column("outstanding", "Outstanding", "المستحق", "decimal"), Column("status", "Status", "الحالة", "text")], [new("Finance", "Posted open items", "Finance", rows.Count == 0 ? null : rows.Max(item => item.AsOfDate).ToDateTime(TimeOnly.MinValue))], ReportingResultState.Fresh, "Finance owns Payment Terms, due dates, and aging buckets.", "Reconciled");
    }

    private async Task<ReportingResult> FinanceCashMovementAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var fc = FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) ? financeContext : null;
        if (fc is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "finance_context_unavailable", "Finance context is unavailable.", clock.GetUtcNow());
        var sourcePage = await settlementReporting.ListCashMovementReportingPageAsync(fc, query.CompanyId!.Value, query.FromDate, query.ToDate, ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection), ct);
        var values = sourcePage.Rows;
        var rows = values.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?>
        {
            ["documentDate"] = item.DocumentDate.ToString("yyyy-MM-dd"),
            ["direction"] = item.Direction.ToString(),
            ["status"] = item.Status.ToString(),
            ["cashAccount"] = item.CashAccountId.ToString("D"),
            ["paymentMethod"] = item.PaymentMethodId.ToString("D"),
            ["currency"] = item.CurrencyCode,
            ["amount"] = Amount(item.Amount),
            ["functionalCurrency"] = item.FunctionalCurrencyCode,
            ["functionalAmount"] = Amount(item.FunctionalAmount),
            ["reference"] = item.ExternalReference,
            ["description"] = item.Description,
            ["postedJournal"] = item.PostedJournalId?.ToString("D"),
            ["reversalJournal"] = item.ReversalJournalId?.ToString("D")
        }, "Finance", item.Id.ToString("D"), item.Status.ToString(), item.PostedAt ?? item.CreatedAt, "finance-cash-subledger"));
        return Result(context, definition, query, rows,
            [Column("documentDate", "Document date", "\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0645\u0633\u062a\u0646\u062f", "date"), Column("direction", "Direction", "\u0627\u0644\u0627\u062a\u062c\u0627\u0647", "text"), Column("status", "Status", "\u0627\u0644\u062d\u0627\u0644\u0629", "text"), Column("cashAccount", "Cash account", "\u0627\u0644\u062d\u0633\u0627\u0628 \u0627\u0644\u0646\u0642\u062f\u064a", "text"), Column("paymentMethod", "Payment method", "\u0637\u0631\u064a\u0642\u0629 \u0627\u0644\u062f\u0641\u0639", "text"), Column("currency", "Currency", "\u0627\u0644\u0639\u0645\u0644\u0629", "text"), Column("amount", "Amount", "\u0627\u0644\u0645\u0628\u0644\u063a", "decimal"), Column("functionalCurrency", "Functional currency", "\u0627\u0644\u0639\u0645\u0644\u0629 \u0627\u0644\u0648\u0638\u064a\u0641\u064a\u0629", "text"), Column("functionalAmount", "Functional amount", "\u0627\u0644\u0645\u0628\u0644\u063a \u0627\u0644\u0648\u0638\u064a\u0641\u064a", "decimal"), Column("reference", "Reference", "\u0627\u0644\u0645\u0631\u062c\u0639", "text"), Column("description", "Description", "\u0627\u0644\u0648\u0635\u0641", "text"), Column("postedJournal", "Posted journal", "\u0627\u0644\u0642\u064a\u062f \u0627\u0644\u0645\u0631\u062d\u0644", "text"), Column("reversalJournal", "Reversal journal", "\u0642\u064a\u062f \u0627\u0644\u0639\u0643\u0633", "text")],
            [new("Finance", "Settlement source", "Finance", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Finance owns settlement status, cash movement amount, and posting lineage.", "Finance-owned", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : values.Count);
    }

    private async Task<ReportingResult> FinanceTaxSummaryAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var fc = FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) ? financeContext : null;
        if (fc is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "finance_context_unavailable", "Finance context is unavailable.", clock.GetUtcNow());
        var asOf = ResolveAsOf(query);
        var report = await finance.QueryReconciliationAsync(fc, query.CompanyId!.Value, asOf, cancellationToken: ct);
        var tax = report.Items.Where(item => string.Equals(item.Scope, "Tax", StringComparison.OrdinalIgnoreCase)).ToArray();
        var rows = tax.Select(item => Row(item.SourceReference ?? item.Scope, new Dictionary<string, string?> { ["scope"] = item.Scope, ["status"] = item.Status.ToString(), ["expected"] = OptionalAmount(item.ExpectedAmount), ["actual"] = OptionalAmount(item.ActualAmount), ["difference"] = OptionalAmount(item.Difference), ["source"] = item.SourceReference, ["detail"] = item.Detail, ["evidence"] = item.HasDurableEvidence.ToString() }, "Finance", item.SourceReference ?? item.Scope, item.Status.ToString(), asOf.ToDateTime(TimeOnly.MinValue), "finance-tax-reconciliation"));
        var state = report.OverallStatus.ToString().Contains("Reconciled", StringComparison.OrdinalIgnoreCase) ? ReportingResultState.Fresh : ReportingResultState.Partial;
        return Result(context, definition, query, rows, [Column("scope", "Scope", "النطاق", "text"), Column("status", "Status", "الحالة", "text"), Column("expected", "Expected", "المتوقع", "decimal"), Column("actual", "Actual", "الفعلي", "decimal"), Column("difference", "Difference", "الفرق", "decimal"), Column("source", "Source", "المصدر", "text"), Column("detail", "Detail", "التفاصيل", "text"), Column("evidence", "Evidence", "الدليل", "boolean")], [new("Finance", report.OverallStatus.ToString(), "Finance", asOf.ToDateTime(TimeOnly.MinValue))], state, "Only internal Finance tax evidence is published; no statutory certification is claimed.", report.OverallStatus.ToString(), tax.Length);
    }

    private async Task<ReportingResult> InventoryStockBalanceAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var ic = InventoryRequestContext.FromFoundationContext(context.FoundationContext);
        var source = inventory is IInventoryReportingReadPort bounded
            ? await bounded.ListStatesReportingPageAsync(ic, new InventoryValuationQuery(query.CompanyId!.Value, query.BranchId, query.WarehouseId), ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection), ct)
            : new ReportingSourcePage<InventoryValuationStateRecord>(await inventory.ListStatesAsync(ic, new InventoryValuationQuery(query.CompanyId!.Value, query.BranchId, query.WarehouseId), ct), 0, "warehouseId,productId,trackingIdentity,id", null, false);
        var states = source.Rows;
        var rows = states.Select(item => Row(item.ProductId.ToString("D"), new Dictionary<string, string?> { ["company"] = item.CompanyId.ToString("D"), ["warehouse"] = item.WarehouseId.ToString("D"), ["product"] = item.ProductId.ToString("D"), ["tracking"] = item.TrackingIdentity, ["quantity"] = Amount(item.Quantity), ["value"] = Amount(item.Value), ["averageUnitCost"] = Amount(item.AverageUnitCost), ["ledgerSequence"] = item.LastAppliedLedgerSequence.ToString(CultureInfo.InvariantCulture), ["updatedAt"] = item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) }, "Inventory", item.ProductId.ToString("D"), "Persisted", item.UpdatedAt, "inventory-balance"));
        return Result(context, definition, query, rows, [Column("company", "Company", "الشركة", "text"), Column("warehouse", "Warehouse", "المستودع", "text"), Column("product", "Product", "المنتج", "text"), Column("tracking", "Tracking", "التتبع", "text"), Column("quantity", "Quantity", "الكمية", "decimal"), Column("value", "Value", "القيمة", "decimal"), Column("averageUnitCost", "Average unit cost", "متوسط تكلفة الوحدة", "decimal"), Column("ledgerSequence", "Ledger sequence", "تسلسل الدفتر", "integer"), Column("updatedAt", "Updated", "التحديث", "datetime")], [new("Inventory", "Persisted", "Inventory", source.DataAsOf)], ReportingResultState.Fresh, "Inventory owns persisted stock-balance state; zero quantity is a valid persisted value.", "Inventory-owned", source.TotalRowsKnown ? source.TotalRows : states.Count);
    }

    private async Task<ReportingResult> FinanceStatementAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var fc = FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) ? financeContext : null;
        if (fc is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "finance_context_unavailable", "Finance context is unavailable.", clock.GetUtcNow());
        var kind = definition.Code.EndsWith("profit-loss", StringComparison.Ordinal) ? FinanceStatementKind.ProfitAndLoss : FinanceStatementKind.BalanceSheet;
        var report = await finance.QueryStatementAsync(fc, query.CompanyId!.Value, kind, query.FromDate ?? new DateOnly(DefaultAsOf.Year, 1, 1), query.ToDate ?? DefaultAsOf, ct);
        var rows = report.Rows.Select(row => Row(row.AccountId.ToString("D"), new Dictionary<string, string?> { ["account"] = row.AccountCode, ["name"] = row.AccountName, ["type"] = row.AccountType.ToString(), ["opening"] = Amount(row.OpeningBalance), ["debit"] = Amount(row.Debit), ["credit"] = Amount(row.Credit), ["closing"] = Amount(row.ClosingBalance), ["currency"] = row.FunctionalCurrencyCode }, "Finance", row.AccountId.ToString("D"), "Posted", report.ToDate.ToDateTime(TimeOnly.MinValue), "finance-reconciliation"));
        return Result(context, definition, query, rows, [Column("account", "Account", "الحساب", "text"), Column("name", "Name", "الاسم", "text"), Column("type", "Type", "النوع", "text"), Column("opening", "Opening", "الافتتاحي", "decimal"), Column("debit", "Debit", "مدين", "decimal"), Column("credit", "Credit", "دائن", "decimal"), Column("closing", "Closing", "الإقفال", "decimal"), Column("currency", "Currency", "العملة", "text")], [new("Finance", "Posted", "Finance", report.ToDate.ToDateTime(TimeOnly.MinValue))], ReportingResultState.Fresh, "Finance owns statement formulas and posting truth.", "Reconciled");
    }

    private async Task<ReportingResult> FinanceReconciliationAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var fc = FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) ? financeContext : null;
        if (fc is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, "finance_context_unavailable", "Finance context is unavailable.", clock.GetUtcNow());
        var report = await finance.QueryReconciliationAsync(fc, query.CompanyId!.Value, query.AsOfDate ?? DefaultAsOf, cancellationToken: ct);
        var rows = report.Items.Select(item => Row($"{item.Scope}:{item.SourceReference}", new Dictionary<string, string?> { ["scope"] = item.Scope, ["status"] = item.Status.ToString(), ["expected"] = OptionalAmount(item.ExpectedAmount), ["actual"] = OptionalAmount(item.ActualAmount), ["difference"] = OptionalAmount(item.Difference), ["source"] = item.SourceReference, ["detail"] = item.Detail, ["evidence"] = item.HasDurableEvidence.ToString() }, "Finance", item.SourceReference ?? item.Scope, item.Status.ToString(), report.AsOfDate.ToDateTime(TimeOnly.MinValue), "finance-reconciliation"));
        var state = report.OverallStatus.ToString().Contains("Reconciled", StringComparison.OrdinalIgnoreCase) ? ReportingResultState.Fresh : ReportingResultState.Partial;
        return Result(context, definition, query, rows, [Column("scope", "Scope", "النطاق", "text"), Column("status", "Status", "الحالة", "text"), Column("expected", "Expected", "المتوقع", "decimal"), Column("actual", "Actual", "الفعلي", "decimal"), Column("difference", "Difference", "الفرق", "decimal"), Column("source", "Source", "المصدر", "text"), Column("detail", "Detail", "التفاصيل", "text"), Column("evidence", "Evidence", "الدليل", "boolean")], [new("Finance", report.OverallStatus.ToString(), "Finance", report.AsOfDate.ToDateTime(TimeOnly.MinValue))], state, "Finance owns reconciliation outcomes; Reporting only publishes them.", report.OverallStatus.ToString());
    }

    private async Task<ReportingResult> InventoryValuationAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var ic = InventoryRequestContext.FromFoundationContext(context.FoundationContext);
        var result = await inventory.SummaryAsync(ic, new InventoryValuationQuery(query.CompanyId!.Value, query.BranchId, query.WarehouseId), ct);
        if (!result.Succeeded || result.Value is null) return EmptyResult(context, definition, query, ReportingResultState.Unavailable, result.Code, "Inventory valuation is unavailable.", clock.GetUtcNow());
        var value = result.Value;
        var state = value.IsPartial ? ReportingResultState.Partial : value.ReconciliationStatus == InventoryValuationReconciliationStatus.Reconciled ? ReportingResultState.Fresh : ReportingResultState.Unknown;
        var rows = new[] { Row($"{value.CompanyId:D}:{value.WarehouseId:D}", new Dictionary<string, string?> { ["company"] = value.CompanyId.ToString("D"), ["warehouse"] = value.WarehouseId?.ToString("D"), ["quantity"] = Amount(value.PhysicalOnHandQuantity), ["valuedQuantity"] = Amount(value.ValuedQuantity), ["value"] = Amount(value.ValuedAmount), ["inTransitQuantity"] = Amount(value.InTransitQuantity), ["inTransitValue"] = Amount(value.InTransitValue), ["currency"] = value.FunctionalCurrencyCode, ["pending"] = value.PendingMovementCount.ToString(CultureInfo.InvariantCulture), ["blocked"] = value.BlockedMovementCount.ToString(CultureInfo.InvariantCulture), ["reconciliation"] = value.ReconciliationStatus.ToString() }, "Inventory", value.LatestLedgerSequence?.ToString(CultureInfo.InvariantCulture) ?? "valuation-summary", value.ReconciliationStatus.ToString(), value.FreshAsOf, "inventory-valuation-reconciliation") };
        return Result(context, definition, query, rows, [Column("company", "Company", "الشركة", "text"), Column("warehouse", "Warehouse", "المستودع", "text"), Column("quantity", "Physical quantity", "الكمية الفعلية", "decimal"), Column("valuedQuantity", "Valued quantity", "الكمية المقيمة", "decimal"), Column("value", "Valued amount", "القيمة المقيمة", "decimal"), Column("inTransitQuantity", "In transit quantity", "كمية قيد النقل", "decimal"), Column("inTransitValue", "In transit value", "قيمة قيد النقل", "decimal"), Column("currency", "Currency", "العملة", "text"), Column("pending", "Pending movements", "الحركات المعلقة", "integer"), Column("blocked", "Blocked movements", "الحركات المحجوبة", "integer"), Column("reconciliation", "Reconciliation", "التسوية", "text")], [new("Inventory", value.ReconciliationStatus.ToString(), "Inventory", value.FreshAsOf)], state, "Inventory owns physical quantity and moving-weighted-average valuation evidence.", value.ReconciliationStatus.ToString());
    }

    private async Task<ReportingResult> InventoryMovementsAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var ic = InventoryRequestContext.FromFoundationContext(context.FoundationContext);
        if (inventory is IInventoryReportingReadPort bounded)
        {
            var page = ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection);
            var sourcePage = await bounded.ListEventsReportingPageAsync(ic, new InventoryValuationQuery(query.CompanyId!.Value, query.BranchId, query.WarehouseId, EffectiveFrom: query.FromDate, EffectiveTo: query.ToDate), page, ct);
            return InventoryMovementsPageResult(context, definition, query, sourcePage);
        }
        var events = await inventory.ListEventsAsync(ic, new InventoryValuationQuery(query.CompanyId!.Value, query.BranchId, query.WarehouseId, EffectiveFrom: query.FromDate, EffectiveTo: query.ToDate), ct);
        var rows = events.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["effectiveOn"] = item.EffectiveOn.ToString("yyyy-MM-dd"), ["source"] = item.SourceType.ToString(), ["reference"] = item.SourceReference, ["warehouse"] = item.WarehouseId.ToString("D"), ["product"] = item.ProductId.ToString("D"), ["quantity"] = Amount(item.Quantity), ["direction"] = item.Direction.ToString(), ["movementValue"] = OptionalAmount(item.MovementValue), ["status"] = item.Status.ToString(), ["currency"] = item.FunctionalCurrencyCode }, "Inventory", item.Id.ToString("D"), item.Status.ToString(), item.OccurredAt, "inventory-valuation-reconciliation"));
        return Result(context, definition, query, rows, [Column("effectiveOn", "Effective on", "تاريخ السريان", "date"), Column("source", "Source type", "نوع المصدر", "text"), Column("reference", "Source reference", "مرجع المصدر", "text"), Column("warehouse", "Warehouse", "المستودع", "text"), Column("product", "Product", "المنتج", "text"), Column("quantity", "Quantity", "الكمية", "decimal"), Column("direction", "Direction", "الاتجاه", "text"), Column("movementValue", "Movement value", "قيمة الحركة", "decimal"), Column("status", "Status", "الحالة", "text"), Column("currency", "Currency", "العملة", "text")], [new("Inventory", events.Any(item => item.Status is InventoryValuationEventStatus.Pending or InventoryValuationEventStatus.Blocked) ? "Partial" : "Posted", "Inventory", events.Count == 0 ? null : events.Max(item => item.OccurredAt))], events.Any(item => item.Status is InventoryValuationEventStatus.Pending or InventoryValuationEventStatus.Blocked) ? ReportingResultState.Partial : ReportingResultState.Fresh, "Movement history is read from the Inventory-owned valuation event stream.", "Inventory-owned");
    }

    private async Task<ReportingResult> ProcurementOrdersAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var pc = ProcurementRequestContext.FromFoundationContext(context.FoundationContext);
        if (purchaseOrders is IPurchaseOrderReportingReadPort bounded)
        {
            var page = ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection);
            var sourcePage = await bounded.ListReportingPageAsync(pc.TenantContext, ParseOptionalEnum<PurchaseOrderStatus>(query.Status), page, ct);
            return ProcurementOrdersPageResult(context, definition, query, sourcePage);
        }
        var orders = await purchaseOrders.ListAsync(pc.TenantContext, null, ct);
        var filtered = orders.Where(item => !query.CompanyId.HasValue || item.Scope.CompanyId == query.CompanyId).Where(item => !query.BranchId.HasValue || item.Scope.BranchId == query.BranchId).ToArray();
        var rows = filtered.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["supplier"] = item.SupplierName, ["reference"] = item.SupplierQuotationReference, ["status"] = item.Status.ToString(), ["currency"] = item.CurrencyCode, ["total"] = Amount(item.Total), ["lines"] = item.LineCount.ToString(CultureInfo.InvariantCulture), ["createdAt"] = item.CreatedAt.ToString("O", CultureInfo.InvariantCulture), ["company"] = item.Scope.CompanyId.ToString("D"), ["branch"] = item.Scope.BranchId?.ToString("D") }, "Procurement", item.Id.ToString("D"), item.Status.ToString(), item.UpdatedAt, "procurement-order-source"));
        return Result(context, definition, query, rows, [Column("supplier", "Supplier", "المورد", "text"), Column("reference", "Source reference", "مرجع المصدر", "text"), Column("status", "Status", "الحالة", "text"), Column("currency", "Currency", "العملة", "text"), Column("total", "Total", "الإجمالي", "decimal"), Column("lines", "Lines", "البنود", "integer"), Column("createdAt", "Created", "الإنشاء", "datetime"), Column("company", "Company", "الشركة", "text"), Column("branch", "Branch", "الفرع", "text")], [new("Procurement", "Source-owned", "Procurement", filtered.Length == 0 ? null : filtered.Max(item => item.UpdatedAt))], ReportingResultState.Fresh, "Procurement owns order status and commercial commitment meaning.", "Procurement-owned");
    }

    private async Task<ReportingResult> ProcurementReceiptsAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var pc = ProcurementRequestContext.FromFoundationContext(context.FoundationContext);
        if (goodsReceipts is IGoodsReceiptReportingReadPort bounded)
        {
            var page = ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection);
            var sourcePage = await bounded.ListReportingPageAsync(pc.TenantContext, ParseOptionalEnum<GoodsReceiptStatus>(query.Status), query.FromDate, query.ToDate, page, ct);
            return ProcurementReceiptsPageResult(context, definition, query, sourcePage);
        }
        var receipts = await goodsReceipts.ListAsync(pc.TenantContext, null, null, ct);
        var filtered = receipts.Where(item => !query.WarehouseId.HasValue || item.WarehouseId == query.WarehouseId).Where(item => !query.FromDate.HasValue || item.ReceivedDate >= query.FromDate).Where(item => !query.ToDate.HasValue || item.ReceivedDate <= query.ToDate).ToArray();
        var rows = filtered.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["supplier"] = item.SupplierName, ["purchaseOrder"] = item.PurchaseOrderId.ToString("D"), ["warehouse"] = item.WarehouseId.ToString("D"), ["status"] = item.Status.ToString(), ["receivedDate"] = item.ReceivedDate.ToString("yyyy-MM-dd"), ["acceptedQuantity"] = Amount(item.TotalAcceptedQuantity), ["lines"] = item.LineCount.ToString(CultureInfo.InvariantCulture) }, "Procurement", item.Id.ToString("D"), item.Status.ToString(), item.CreatedAt, "inventory-receipt-source"));
        return Result(context, definition, query, rows, [Column("supplier", "Supplier", "المورد", "text"), Column("purchaseOrder", "Purchase order", "أمر الشراء", "text"), Column("warehouse", "Warehouse", "المستودع", "text"), Column("status", "Status", "الحالة", "text"), Column("receivedDate", "Received", "تاريخ الاستلام", "date"), Column("acceptedQuantity", "Accepted quantity", "الكمية المقبولة", "decimal"), Column("lines", "Lines", "البنود", "integer")], [new("Procurement", "Source-owned", "Procurement", filtered.Length == 0 ? null : filtered.Max(item => item.CreatedAt))], ReportingResultState.Fresh, "Procurement owns receipt document status; Inventory owns physical stock effects.", "Procurement-owned");
    }

    private async Task<ReportingResult> ProcurementMatchExceptionsAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var pc = ProcurementRequestContext.FromFoundationContext(context.FoundationContext);
        var sourcePage = await matchReporting.ListReportingPageAsync(pc.TenantContext, ParseOptionalEnum<PurchaseInvoiceMatchResult>(query.Status), query.FromDate, query.ToDate, ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection), ct);
        var values = sourcePage.Rows;
        var rows = values.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?>
        {
            ["evaluatedAt"] = item.EvaluatedAt.ToString("O", CultureInfo.InvariantCulture),
            ["lifecycle"] = item.Lifecycle.ToString(),
            ["result"] = item.Result.ToString(),
            ["purchaseInvoiceHandoff"] = item.PurchaseInvoiceHandoffId.ToString("D"),
            ["purchaseOrder"] = item.PurchaseOrderId.ToString("D"),
            ["varianceCount"] = item.Variances.Count.ToString(CultureInfo.InvariantCulture),
            ["varianceClassifications"] = string.Join(",", item.Variances.Select(value => value.Classification).Distinct(StringComparer.Ordinal)),
            ["resolutionReason"] = item.ResolutionReason,
            ["declaredEvidence"] = item.DeclaredEvidenceId?.ToString("D"),
            ["sourceFingerprint"] = item.SourceFingerprint
        }, "Procurement", item.Id.ToString("D"), $"{item.Lifecycle}/{item.Result}", item.EvaluatedAt, "procurement-match-evidence"));
        return Result(context, definition, query, rows,
            [Column("evaluatedAt", "Evaluated", "\u0627\u0644\u062a\u0642\u064a\u064a\u0645", "datetime"), Column("lifecycle", "Lifecycle", "\u062f\u0648\u0631\u0629 \u0627\u0644\u062d\u064a\u0627\u0629", "text"), Column("result", "Result", "\u0627\u0644\u0646\u062a\u064a\u062c\u0629", "text"), Column("purchaseInvoiceHandoff", "Invoice handoff", "\u062a\u0633\u0644\u064a\u0645 \u0627\u0644\u0641\u0627\u062a\u0648\u0631\u0629", "text"), Column("purchaseOrder", "Purchase order", "\u0623\u0645\u0631 \u0627\u0644\u0634\u0631\u0627\u0621", "text"), Column("varianceCount", "Variances", "\u0627\u0644\u0641\u0631\u0648\u0642\u0627\u062a", "integer"), Column("varianceClassifications", "Variance classifications", "\u062a\u0635\u0646\u064a\u0641\u0627\u062a \u0627\u0644\u0641\u0631\u0648\u0642\u0627\u062a", "text"), Column("resolutionReason", "Resolution reason", "\u0633\u0628\u0628 \u0627\u0644\u0645\u0639\u0627\u0644\u062c\u0629", "text"), Column("declaredEvidence", "Declared evidence", "\u0627\u0644\u062f\u0644\u064a\u0644 \u0627\u0644\u0645\u0635\u0631\u062d \u0628\u0647", "text"), Column("sourceFingerprint", "Source fingerprint", "\u0628\u0635\u0645\u0629 \u0627\u0644\u0645\u0635\u062f\u0631", "text")],
            [new("Procurement", "Match evaluation source", "Procurement", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Procurement owns match evaluation, variance classifications, lifecycle, and resolution evidence.", "Procurement-owned", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : values.Count);
    }

    private async Task<ReportingResult> SalesOrdersAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var sc = ProcurementRequestContext.FromFoundationContext(context.FoundationContext);
        if (sales is ISalesReportingReadPort bounded)
        {
            var page = ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection);
            var sourcePage = await bounded.ListOrdersReportingPageAsync(sc, query.CompanyId, ParseOptionalEnum<SalesOrderStatus>(query.Status), page, ct);
            return SalesOrdersPageResult(context, definition, query, sourcePage);
        }
        var orders = await sales.ListOrdersAsync(sc, query.CompanyId, null, ct);
        var rows = orders.Where(item => !query.BranchId.HasValue || item.BranchId == query.BranchId).Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["number"] = item.Number, ["customer"] = item.CustomerName, ["status"] = item.Status.ToString(), ["credit"] = item.CreditOutcome.ToString(), ["currency"] = item.CurrencyCode, ["total"] = Amount(item.Total), ["quotation"] = item.SourceQuotationNumber, ["revision"] = item.RevisionNumber.ToString(CultureInfo.InvariantCulture), ["updatedAt"] = item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture), ["company"] = item.CompanyId.ToString("D"), ["branch"] = item.BranchId?.ToString("D") }, "Sales", item.Id.ToString("D"), item.Status.ToString(), item.UpdatedAt, "sales-commercial-chain"));
        return Result(context, definition, query, rows, [Column("number", "Order", "الأمر", "text"), Column("customer", "Customer", "العميل", "text"), Column("status", "Status", "الحالة", "text"), Column("credit", "Credit outcome", "نتيجة الائتمان", "text"), Column("currency", "Currency", "العملة", "text"), Column("total", "Total", "الإجمالي", "decimal"), Column("quotation", "Source quotation", "عرض السعر المصدر", "text"), Column("revision", "Revision", "المراجعة", "integer"), Column("updatedAt", "Updated", "التحديث", "datetime"), Column("company", "Company", "الشركة", "text"), Column("branch", "Branch", "الفرع", "text")], [new("Sales", "Commercial source", "Sales", orders.Count == 0 ? null : orders.Max(item => item.UpdatedAt))], ReportingResultState.Fresh, "Sales owns commercial order status; Finance and Inventory remain owners of monetary and physical effects.", "Sales-owned");
    }

    private async Task<ReportingResult> SalesFulfillmentAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var sc = ProcurementRequestContext.FromFoundationContext(context.FoundationContext);
        var sourcePage = await fulfillmentReporting.ListFulfillmentReportingPageAsync(sc, ParseOptionalEnum<SalesDeliveryStatus>(query.Status), ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection), ct);
        var values = sourcePage.Rows;
        var rows = values.Select(item =>
        {
            var lineage = new List<ReportingLineage> { new("Sales", item.Id.ToString("D"), item.Status.ToString(), "source-contract", "sales-delivery-fulfillment") };
            if (item.Handoff.DownstreamEffectIds.Count > 0)
                lineage.Add(new("Inventory", string.Join(",", item.Handoff.DownstreamEffectIds.Select(value => value.ToString("D"))), $"{item.Handoff.DownstreamCommitState}/{item.Handoff.SalesAcknowledgementState}", "sales-handoff-evidence", item.Handoff.ReconciliationStatus));
            return new ReportingRow(item.Id.ToString("D"), new Dictionary<string, string?>
            {
                ["delivery"] = item.Id.ToString("D"),
                ["order"] = item.OrderId.ToString("D"),
                ["revision"] = item.OrderRevisionNumber.ToString(CultureInfo.InvariantCulture),
                ["company"] = item.CompanyId.ToString("D"),
                ["branch"] = item.BranchId?.ToString("D"),
                ["warehouse"] = item.WarehouseId.ToString("D"),
                ["status"] = item.Status.ToString(),
                ["error"] = item.ErrorCode,
                ["lineCount"] = item.LineCount.ToString(CultureInfo.InvariantCulture),
                ["requestedQuantity"] = Amount(item.RequestedQuantity),
                ["movementCount"] = item.MovementCount.ToString(CultureInfo.InvariantCulture),
                ["downstreamCommit"] = item.Handoff.DownstreamCommitState,
                ["salesAcknowledgement"] = item.Handoff.SalesAcknowledgementState,
                ["reconciliation"] = item.Handoff.ReconciliationStatus,
                ["createdAt"] = item.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                ["postedAt"] = item.PostedAt?.ToString("O", CultureInfo.InvariantCulture)
            }, lineage);
        });
        return Result(context, definition, query, rows,
            [Column("delivery", "Delivery", "\u0627\u0644\u062a\u0633\u0644\u064a\u0645", "text"), Column("order", "Order", "\u0627\u0644\u0623\u0645\u0631", "text"), Column("revision", "Revision", "\u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629", "integer"), Column("company", "Company", "\u0627\u0644\u0634\u0631\u0643\u0629", "text"), Column("branch", "Branch", "\u0627\u0644\u0641\u0631\u0639", "text"), Column("warehouse", "Warehouse", "\u0627\u0644\u0645\u0633\u062a\u0648\u062f\u0639", "text"), Column("status", "Status", "\u0627\u0644\u062d\u0627\u0644\u0629", "text"), Column("error", "Error", "\u0627\u0644\u062e\u0637\u0623", "text"), Column("lineCount", "Lines", "\u0627\u0644\u0628\u0646\u0648\u062f", "integer"), Column("requestedQuantity", "Requested quantity", "\u0627\u0644\u0643\u0645\u064a\u0629 \u0627\u0644\u0645\u0637\u0644\u0648\u0628\u0629", "decimal"), Column("movementCount", "Inventory movements", "\u062d\u0631\u0643\u0627\u062a \u0627\u0644\u0645\u062e\u0632\u0648\u0646", "integer"), Column("downstreamCommit", "Downstream commit", "\u0627\u0644\u0627\u0644\u062a\u0632\u0627\u0645 \u0627\u0644\u0644\u0627\u062d\u0642", "text"), Column("salesAcknowledgement", "Sales acknowledgement", "\u0625\u0642\u0631\u0627\u0631 \u0627\u0644\u0645\u0628\u064a\u0639\u0627\u062a", "text"), Column("reconciliation", "Reconciliation", "\u0627\u0644\u062a\u0633\u0648\u064a\u0629", "text"), Column("createdAt", "Created", "\u0627\u0644\u0625\u0646\u0634\u0627\u0621", "datetime"), Column("postedAt", "Posted", "\u0627\u0644\u062a\u0631\u062d\u064a\u0644", "datetime")],
            [new("Sales", "Delivery source", "Sales", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Sales owns Delivery fulfillment truth; Inventory movement state is published only as durable handoff evidence.", "Sales-owned with Inventory handoff evidence", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : values.Count);
    }

    private async Task<ReportingResult> SalesReturnsCreditsAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        var sc = ProcurementRequestContext.FromFoundationContext(context.FoundationContext);
        var sourcePage = await customerReturnReporting.ListReportingPageAsync(sc, query.FromDate, query.ToDate, ParseOptionalEnum<SalesCustomerReturnStatus>(query.Status), ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection), ct);
        var values = sourcePage.Rows;
        var rows = values.Select(item =>
        {
            var lineage = new List<ReportingLineage> { new("Sales", item.Id.ToString("D"), $"{item.Status}/{item.Consequence}", "source-contract", "sales-customer-return") };
            if (item.InventoryEffectId is { } inventoryEffect)
                lineage.Add(new("Inventory", inventoryEffect.ToString("D"), $"{item.InventoryCommitState}/{item.InventoryAcknowledgementState}", "sales-return-handoff", item.InventoryReconciliationState));
            foreach (var effect in item.FinanceEffects)
                lineage.Add(new("Finance", effect.CreditNoteId.ToString("D"), $"{effect.State}/{effect.ReversalState}", "sales-credit-effect-evidence", effect.EffectFingerprint));
            return new ReportingRow(item.Id.ToString("D"), new Dictionary<string, string?>
            {
                ["return"] = item.Id.ToString("D"),
                ["delivery"] = item.DeliveryId.ToString("D"),
                ["order"] = item.OrderId.ToString("D"),
                ["revision"] = item.OrderRevisionNumber.ToString(CultureInfo.InvariantCulture),
                ["company"] = item.CompanyId.ToString("D"),
                ["branch"] = item.BranchId?.ToString("D"),
                ["warehouse"] = item.WarehouseId.ToString("D"),
                ["returnDate"] = item.ReturnDate.ToString("yyyy-MM-dd"),
                ["status"] = item.Status.ToString(),
                ["consequence"] = item.Consequence.ToString(),
                ["lineCount"] = item.LineCount.ToString(CultureInfo.InvariantCulture),
                ["returnQuantity"] = Amount(item.ReturnQuantity),
                ["currency"] = item.CurrencyCode,
                ["invoice"] = item.InvoiceId?.ToString("D"),
                ["inventoryEffect"] = item.InventoryEffectId?.ToString("D"),
                ["inventoryReconciliation"] = item.InventoryReconciliationState,
                ["financeState"] = item.FinanceEffectState,
                ["activeCreditNotes"] = item.ActiveFinanceCreditNoteCount.ToString(CultureInfo.InvariantCulture),
                ["creditNotes"] = string.Join(",", item.FinanceCreditNoteIds.Select(value => value.ToString("D"))),
                ["reversedCreditNotes"] = string.Join(",", item.FinanceReversedCreditNoteIds.Select(value => value.ToString("D"))),
                ["updatedAt"] = item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)
            }, lineage);
        });
        var sources = values.Any(item => item.FinanceEffects.Count > 0)
            ? new[] { new ReportingSourceEvidence("Sales", "Customer Return source", "source-contract", sourcePage.DataAsOf), new ReportingSourceEvidence("Finance", "Credit effect evidence", "source-contract", sourcePage.DataAsOf) }
            : new[] { new ReportingSourceEvidence("Sales", "Customer Return source", "source-contract", sourcePage.DataAsOf) };
        return Result(context, definition, query, rows,
            [Column("return", "Return", "\u0627\u0644\u0645\u0631\u062a\u062c\u0639", "text"), Column("delivery", "Delivery", "\u0627\u0644\u062a\u0633\u0644\u064a\u0645", "text"), Column("order", "Order", "\u0627\u0644\u0623\u0645\u0631", "text"), Column("revision", "Revision", "\u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629", "integer"), Column("company", "Company", "\u0627\u0644\u0634\u0631\u0643\u0629", "text"), Column("branch", "Branch", "\u0627\u0644\u0641\u0631\u0639", "text"), Column("warehouse", "Warehouse", "\u0627\u0644\u0645\u0633\u062a\u0648\u062f\u0639", "text"), Column("returnDate", "Return date", "\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0645\u0631\u062a\u062c\u0639", "date"), Column("status", "Status", "\u0627\u0644\u062d\u0627\u0644\u0629", "text"), Column("consequence", "Consequence", "\u0627\u0644\u0623\u062b\u0631", "text"), Column("lineCount", "Lines", "\u0627\u0644\u0628\u0646\u0648\u062f", "integer"), Column("returnQuantity", "Return quantity", "\u0643\u0645\u064a\u0629 \u0627\u0644\u0645\u0631\u062a\u062c\u0639", "decimal"), Column("currency", "Currency", "\u0627\u0644\u0639\u0645\u0644\u0629", "text"), Column("invoice", "Invoice", "\u0627\u0644\u0641\u0627\u062a\u0648\u0631\u0629", "text"), Column("inventoryEffect", "Inventory effect", "\u0623\u062b\u0631 \u0627\u0644\u0645\u062e\u0632\u0648\u0646", "text"), Column("inventoryReconciliation", "Inventory reconciliation", "\u062a\u0633\u0648\u064a\u0629 \u0627\u0644\u0645\u062e\u0632\u0648\u0646", "text"), Column("financeState", "Finance credit state", "\u062d\u0627\u0644\u0629 \u0627\u0644\u0627\u0626\u062a\u0645\u0627\u0646 \u0627\u0644\u0645\u0627\u0644\u064a", "text"), Column("activeCreditNotes", "Active credit notes", "\u0625\u0634\u0639\u0627\u0631\u0627\u062a \u0627\u0644\u062f\u0627\u0626\u0646 \u0627\u0644\u0646\u0634\u0637\u0629", "integer"), Column("creditNotes", "Credit notes", "\u0625\u0634\u0639\u0627\u0631\u0627\u062a \u0627\u0644\u062f\u0627\u0626\u0646", "text"), Column("reversedCreditNotes", "Reversed credit notes", "\u0625\u0634\u0639\u0627\u0631\u0627\u062a \u0627\u0644\u062f\u0627\u0626\u0646 \u0627\u0644\u0645\u0639\u0643\u0648\u0633\u0629", "text"), Column("updatedAt", "Updated", "\u0627\u0644\u062a\u062d\u062f\u064a\u062b", "datetime")],
            sources, ReportingResultState.Fresh, "Sales owns Return lifecycle and links; Inventory and Finance effects remain source-owned durable evidence.", "Sales-owned with Inventory/Finance evidence", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : values.Count);
    }

    private async Task<ReportingResult> AuditActivityAsync(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, CancellationToken ct)
    {
        if (auditReader is IFoundationAuditScopedEvidenceReader bounded)
        {
            var page = ReportingPageRequest.Create(query.Page, query.PageSize, query.SortBy, query.SortDirection);
            var scope = scopeOwnership.Resolve(context.TenantContext, query.ToScopeRequest());
            if (!scope.Allowed || scope.Scope is null) return EmptyResult(context, definition, query, ReportingResultState.Denied, "scope_denied", "The requested organization scope is not authorized.", clock.GetUtcNow());
            var sourcePage = await bounded.ReadForTenantScopeAsync(context.TenantContext, scope.Scope, page, query.FromDate, query.ToDate, ct);
            return AuditPageResult(context, definition, query, sourcePage);
        }
        var evidence = (await auditReader.ReadForTenantAsync(context.TenantContext, ct)).Where(item => AuditEvidenceInScope(item, context.TenantContext.Scope)).ToArray();
        var filtered = evidence.Where(item => !query.FromDate.HasValue || DateOnly.FromDateTime(item.OccurredAt.UtcDateTime) >= query.FromDate).Where(item => !query.ToDate.HasValue || DateOnly.FromDateTime(item.OccurredAt.UtcDateTime) <= query.ToDate).ToArray();
        var rows = filtered.Select(item => Row(item.EvidenceId.ToString("D"), new Dictionary<string, string?> { ["occurredAt"] = item.OccurredAt.ToString("O", CultureInfo.InvariantCulture), ["operation"] = item.OperationId, ["decision"] = item.Decision.ToString(), ["reason"] = item.Reason.ToString(), ["scope"] = item.OrganizationScope, ["correlation"] = item.CorrelationId }, "SaaS/Admin/Audit", item.EvidenceId.ToString("D"), item.Decision.ToString(), item.OccurredAt, "audit-evidence"));
        return Result(context, definition, query, rows, [Column("occurredAt", "Occurred", "وقت الحدث", "datetime"), Column("operation", "Operation", "العملية", "text"), Column("decision", "Decision", "القرار", "text"), Column("reason", "Reason", "السبب", "text"), Column("scope", "Scope", "النطاق", "text"), Column("correlation", "Correlation", "الترابط", "text")], [new("SaaS/Admin/Audit", "Append-only evidence", "Audit", filtered.Length == 0 ? null : filtered.Max(item => item.OccurredAt))], ReportingResultState.Fresh, "Audit evidence is read-only and Tenant-scoped.", "Audit-owned");
    }

    private ReportingResult SourceHealth(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query)
    {
        var now = clock.GetUtcNow();
        var rows = new[] { ("Finance", "Finance-owned posted truth", "Registered adapter; query remains source-owned"), ("Inventory", "Inventory-owned ledger and valuation", "Registered adapter; query remains source-owned"), ("Procurement", "Procurement-owned documents", "Registered adapter; query remains source-owned"), ("Sales", "Sales-owned commercial chain", "Registered adapter; query remains source-owned"), ("SaaS/Admin/Audit", "Append-only platform evidence", "Registered adapter; query remains source-owned") }.Select(item => Row(item.Item1, new Dictionary<string, string?> { ["source"] = item.Item1, ["ownership"] = item.Item2, ["status"] = item.Item3 }, "SaaS/Admin/Audit", item.Item1, "Registered", now, "source-health"));
        return Result(context, definition, query, rows, [Column("source", "Source domain", "مجال المصدر", "text"), Column("ownership", "Authoritative meaning", "المعنى الموثوق", "text"), Column("status", "Status", "الحالة", "text")], [new("SaaS/Admin/Audit", "Adapter registry", "Reporting", now)], ReportingResultState.Fresh, "Source health reports adapter availability, not business totals.", "Adapter-health");
    }

    private ReportingResult InventoryMovementsPageResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingSourcePage<InventoryMovementValuationEventRecord> sourcePage)
    {
        var events = sourcePage.Rows;
        var rows = events.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["effectiveOn"] = item.EffectiveOn.ToString("yyyy-MM-dd"), ["source"] = item.SourceType.ToString(), ["reference"] = item.SourceReference, ["warehouse"] = item.WarehouseId.ToString("D"), ["product"] = item.ProductId.ToString("D"), ["quantity"] = Amount(item.Quantity), ["direction"] = item.Direction.ToString(), ["movementValue"] = OptionalAmount(item.MovementValue), ["status"] = item.Status.ToString(), ["currency"] = item.FunctionalCurrencyCode }, "Inventory", item.Id.ToString("D"), item.Status.ToString(), item.OccurredAt, "inventory-valuation-reconciliation"));
        var state = events.Any(item => item.Status is InventoryValuationEventStatus.Pending or InventoryValuationEventStatus.Blocked) ? ReportingResultState.Partial : ReportingResultState.Fresh;
        return Result(context, definition, query, rows, [Column("effectiveOn", "Effective on", "تاريخ السريان", "date"), Column("source", "Source type", "نوع المصدر", "text"), Column("reference", "Source reference", "مرجع المصدر", "text"), Column("warehouse", "Warehouse", "المستودع", "text"), Column("product", "Product", "المنتج", "text"), Column("quantity", "Quantity", "الكمية", "decimal"), Column("direction", "Direction", "الاتجاه", "text"), Column("movementValue", "Movement value", "قيمة الحركة", "decimal"), Column("status", "Status", "الحالة", "text"), Column("currency", "Currency", "العملة", "text")], [new("Inventory", state == ReportingResultState.Partial ? "Partial" : "Posted", "Inventory", sourcePage.DataAsOf)], state, "Movement history is read from the Inventory-owned valuation event stream.", "Inventory-owned", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : events.Count);
    }

    private ReportingResult ProcurementOrdersPageResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingSourcePage<PurchaseOrderListRecord> sourcePage)
    {
        var values = sourcePage.Rows;
        var rows = values.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["supplier"] = item.SupplierName, ["reference"] = item.SupplierQuotationReference, ["status"] = item.Status.ToString(), ["currency"] = item.CurrencyCode, ["total"] = Amount(item.Total), ["lines"] = item.LineCount.ToString(CultureInfo.InvariantCulture), ["createdAt"] = item.CreatedAt.ToString("O", CultureInfo.InvariantCulture), ["company"] = item.Scope.CompanyId.ToString("D"), ["branch"] = item.Scope.BranchId?.ToString("D") }, "Procurement", item.Id.ToString("D"), item.Status.ToString(), item.UpdatedAt, "procurement-order-source"));
        return Result(context, definition, query, rows, [Column("supplier", "Supplier", "المورد", "text"), Column("reference", "Source reference", "مرجع المصدر", "text"), Column("status", "Status", "الحالة", "text"), Column("currency", "Currency", "العملة", "text"), Column("total", "Total", "الإجمالي", "decimal"), Column("lines", "Lines", "البنود", "integer"), Column("createdAt", "Created", "الإنشاء", "datetime"), Column("company", "Company", "الشركة", "text"), Column("branch", "Branch", "الفرع", "text")], [new("Procurement", "Source-owned", "Procurement", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Procurement owns order status and commercial commitment meaning.", "Procurement-owned", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : values.Count);
    }

    private ReportingResult ProcurementReceiptsPageResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingSourcePage<GoodsReceiptListRecord> sourcePage)
    {
        var values = sourcePage.Rows;
        var rows = values.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["supplier"] = item.SupplierName, ["purchaseOrder"] = item.PurchaseOrderId.ToString("D"), ["warehouse"] = item.WarehouseId.ToString("D"), ["status"] = item.Status.ToString(), ["receivedDate"] = item.ReceivedDate.ToString("yyyy-MM-dd"), ["acceptedQuantity"] = Amount(item.TotalAcceptedQuantity), ["lines"] = item.LineCount.ToString(CultureInfo.InvariantCulture) }, "Procurement", item.Id.ToString("D"), item.Status.ToString(), item.CreatedAt, "inventory-receipt-source"));
        return Result(context, definition, query, rows, [Column("supplier", "Supplier", "المورد", "text"), Column("purchaseOrder", "Purchase order", "أمر الشراء", "text"), Column("warehouse", "Warehouse", "المستودع", "text"), Column("status", "Status", "الحالة", "text"), Column("receivedDate", "Received", "تاريخ الاستلام", "date"), Column("acceptedQuantity", "Accepted quantity", "الكمية المقبولة", "decimal"), Column("lines", "Lines", "البنود", "integer")], [new("Procurement", "Source-owned", "Procurement", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Procurement owns receipt document status; Inventory owns physical stock effects.", "Procurement-owned", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : values.Count);
    }

    private ReportingResult SalesOrdersPageResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingSourcePage<SalesOrderSummaryResponse> sourcePage)
    {
        var values = sourcePage.Rows;
        var rows = values.Select(item => Row(item.Id.ToString("D"), new Dictionary<string, string?> { ["number"] = item.Number, ["customer"] = item.CustomerName, ["status"] = item.Status.ToString(), ["credit"] = item.CreditOutcome.ToString(), ["currency"] = item.CurrencyCode, ["total"] = Amount(item.Total), ["quotation"] = item.SourceQuotationNumber, ["revision"] = item.RevisionNumber.ToString(CultureInfo.InvariantCulture), ["updatedAt"] = item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture), ["company"] = item.CompanyId.ToString("D"), ["branch"] = item.BranchId?.ToString("D") }, "Sales", item.Id.ToString("D"), item.Status.ToString(), item.UpdatedAt, "sales-commercial-chain"));
        return Result(context, definition, query, rows, [Column("number", "Order", "الأمر", "text"), Column("customer", "Customer", "العميل", "text"), Column("status", "Status", "الحالة", "text"), Column("credit", "Credit outcome", "نتيجة الائتمان", "text"), Column("currency", "Currency", "العملة", "text"), Column("total", "Total", "الإجمالي", "decimal"), Column("quotation", "Source quotation", "عرض السعر المصدر", "text"), Column("revision", "Revision", "المراجعة", "integer"), Column("updatedAt", "Updated", "التحديث", "datetime"), Column("company", "Company", "الشركة", "text"), Column("branch", "Branch", "الفرع", "text")], [new("Sales", "Commercial source", "Sales", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Sales owns commercial order status; Finance and Inventory remain owners of monetary and physical effects.", "Sales-owned", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : values.Count);
    }

    private ReportingResult AuditPageResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingSourcePage<FoundationAuditEvidence> sourcePage)
    {
        var evidence = sourcePage.Rows;
        var rows = evidence.Select(item => Row(item.EvidenceId.ToString("D"), new Dictionary<string, string?> { ["occurredAt"] = item.OccurredAt.ToString("O", CultureInfo.InvariantCulture), ["operation"] = item.OperationId, ["decision"] = item.Decision.ToString(), ["reason"] = item.Reason.ToString(), ["scope"] = item.OrganizationScope, ["correlation"] = item.CorrelationId }, "SaaS/Admin/Audit", item.EvidenceId.ToString("D"), item.Decision.ToString(), item.OccurredAt, "audit-evidence"));
        return Result(context, definition, query, rows, [Column("occurredAt", "Occurred", "وقت الحدث", "datetime"), Column("operation", "Operation", "العملية", "text"), Column("decision", "Decision", "القرار", "text"), Column("reason", "Reason", "السبب", "text"), Column("scope", "Scope", "النطاق", "text"), Column("correlation", "Correlation", "الترابط", "text")], [new("SaaS/Admin/Audit", "Append-only evidence", "Audit", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Audit evidence is read-only and organization-scoped.", "Audit-owned", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : evidence.Count);
    }

    private ReportingResult GeneralLedgerPageResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingSourcePage<FinanceGeneralLedgerLineRecord> sourcePage)
    {
        var lines = sourcePage.Rows;
        var rows = lines.Select(row => Row($"{row.JournalId:D}:{row.LineNumber}", new Dictionary<string, string?> { ["date"] = row.PostingDate.ToString("yyyy-MM-dd"), ["journal"] = row.JournalNumber, ["account"] = row.AccountCode, ["name"] = row.AccountName, ["debit"] = Amount(row.FunctionalDebit), ["credit"] = Amount(row.FunctionalCredit), ["running"] = Amount(row.RunningBalance), ["source"] = row.SourceContract, ["currency"] = row.FunctionalCurrencyCode }, "Finance", row.SourceEvidenceId?.ToString("D") ?? row.JournalId.ToString("D"), "Posted", row.PostingDate.ToDateTime(TimeOnly.MinValue), "finance-reconciliation"));
        return Result(context, definition, query, rows, [Column("date", "Posting date", "تاريخ الترحيل", "date"), Column("journal", "Journal", "القيد", "text"), Column("account", "Account", "الحساب", "text"), Column("name", "Name", "الاسم", "text"), Column("debit", "Debit", "مدين", "decimal"), Column("credit", "Credit", "دائن", "decimal"), Column("running", "Running balance", "الرصيد الجاري", "decimal"), Column("source", "Source", "المصدر", "text"), Column("currency", "Currency", "العملة", "text")], [new("Finance", "Posted", "Finance", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Finance owns the posted journal and running balance.", "Reconciled", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : lines.Count);
    }

    private ReportingResult AgingPageResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingSourcePage<FinanceAgingReportRow> sourcePage)
    {
        var rows = sourcePage.Rows;
        var resultRows = rows.Select(row => Row(row.OpenItemId.ToString("D"), new Dictionary<string, string?> { ["reference"] = row.SourceReference, ["documentDate"] = row.DocumentDate.ToString("yyyy-MM-dd"), ["dueDate"] = row.DueDate.ToString("yyyy-MM-dd"), ["bucket"] = row.AgingBucket, ["currency"] = row.CurrencyCode, ["original"] = Amount(row.OriginalAmount), ["allocated"] = Amount(row.AllocatedAmount), ["outstanding"] = Amount(row.OutstandingAmount), ["status"] = row.Status.ToString() }, "Finance", row.OpenItemId.ToString("D"), row.Status.ToString(), row.AsOfDate.ToDateTime(TimeOnly.MinValue), "finance-reconciliation"));
        return Result(context, definition, query, resultRows, [Column("reference", "Reference", "المرجع", "text"), Column("documentDate", "Document date", "تاريخ المستند", "date"), Column("dueDate", "Due date", "تاريخ الاستحقاق", "date"), Column("bucket", "Aging bucket", "شريحة الاستحقاق", "text"), Column("currency", "Currency", "العملة", "text"), Column("original", "Original", "الأصلي", "decimal"), Column("allocated", "Allocated", "المخصص", "decimal"), Column("outstanding", "Outstanding", "المستحق", "decimal"), Column("status", "Status", "الحالة", "text")], [new("Finance", "Posted open items", "Finance", sourcePage.DataAsOf)], ReportingResultState.Fresh, "Finance owns Payment Terms, due dates, and aging buckets.", "Reconciled", sourcePage.TotalRowsKnown ? sourcePage.TotalRows : rows.Count);
    }

    private static IEnumerable<ReportingDefinition> BuildDefinitions()
    {
        yield return Definition("finance.trial-balance", "Trial balance", "ميزان المراجعة", "Finance", "Finance owns posted truth; Reporting publishes source evidence.", "Finance posted balances → Finance reconciliation", ["company", "asOfDate"], true, false, true);
        yield return Definition("finance.general-ledger", "General ledger", "الأستاذ العام", "Finance", "Finance owns posted truth; Reporting publishes source evidence.", "Posted journals → Finance reconciliation", ["company", "fromDate", "toDate"], true, false, true);
        yield return Definition("finance.ap-aging", "Accounts payable aging", "أعمار الدائنين", "Finance", "Finance owns open items, Payment Terms, due dates and aging.", "AP open items → Finance reconciliation", ["company", "asOfDate", "currency"], true, false, true);
        yield return Definition("finance.ar-aging", "Accounts receivable aging", "أعمار العملاء", "Finance", "Finance owns open items, Payment Terms, due dates and aging.", "AR open items → Finance reconciliation", ["company", "asOfDate", "currency"], true, false, true);
        yield return Definition("finance.profit-loss", "Profit and loss", "الأرباح والخسائر", "Finance", "Finance owns posted truth; Reporting publishes source evidence.", "Posted journals → Finance statement/reconciliation", ["company", "fromDate", "toDate"], true, false, true);
        yield return Definition("finance.balance-sheet", "Balance sheet", "الميزانية العمومية", "Finance", "Finance owns posted truth; Reporting publishes source evidence.", "Posted journals → Finance statement/reconciliation", ["company", "fromDate", "toDate"], true, false, true);
        yield return Definition("finance.reconciliation", "Finance reconciliation", "تسوية المالية", "Finance", "Finance owns posted truth; Reporting publishes source evidence.", "Finance-owned reconciliation evidence", ["company", "asOfDate"], true, false, true);
        yield return Definition("finance.cash-movement", "Cash movement", "حركة النقد", "Finance", "Finance settlement documents own cash movement status, monetary amounts, and posting/reversal lineage; Reporting reads a bounded Finance-owned page.", "Finance settlement document → cash/subledger reconciliation", ["company", "fromDate", "toDate"], false, false, true);
        yield return Definition("finance.tax-summary", "Tax summary", "ملخص الضرائب", "Finance", "Finance owns internal tax evidence; Reporting publishes only the approved internal VAT/accounting facts.", "Finance tax evidence → tax reconciliation", ["company", "asOfDate"], true, false, true);
        yield return Definition("finance.bank-reconciliation", "Bank reconciliation", "تسوية البنك", "Finance", "No accepted bank-statement source capability, bank-statement entities, or provider exists; external/provider behavior remains excluded.", "Finance bank evidence → bank reconciliation", ["company", "asOfDate"], false, false, true, false, "source_capability_unavailable", ReportingImplementationState.SOURCE_CAPABILITY_UNAVAILABLE);
        yield return Definition("inventory.valuation", "Inventory valuation", "تقييم المخزون", "Inventory", "Inventory owns quantity, valuation, in-transit and reconciliation evidence.", "Inventory ledger/movement → valuation reconciliation", ["company", "branch", "warehouse"], true, false, true);
        yield return Definition("inventory.stock-movements", "Stock movements", "حركات المخزون", "Inventory", "Inventory owns immutable movement and valuation event history.", "Inventory movement ledger → valuation reconciliation", ["company", "branch", "warehouse", "fromDate", "toDate"], true, false, true);
        yield return Definition("inventory.stock-balance", "Stock balance", "رصيد المخزون", "Inventory", "Inventory owns the persisted valuation state and balance meaning.", "Inventory balance/projection → ledger reconciliation", ["company", "branch", "warehouse"], true, false, true);
        yield return Definition("inventory.count-variance", "Count variance", "فروقات الجرد", "Inventory", "No accepted Inventory stock-count workflow/entities or bounded count-variance source capability exist; provider/reporting behavior remains excluded.", "Inventory count evidence → stock reconciliation", ["company", "warehouse", "asOfDate"], false, false, true, false, "source_capability_unavailable", ReportingImplementationState.SOURCE_CAPABILITY_UNAVAILABLE);
        yield return Definition("procurement.open-orders", "Open purchase orders", "أوامر الشراء المفتوحة", "Procurement", "Procurement owns commercial commitments and order status.", "Purchase Order → receipt/invoice/AP references when available", ["company", "branch", "status"], true, false, false);
        yield return Definition("procurement.receipts", "Purchase receipts", "استلامات المشتريات", "Procurement", "Procurement owns receipt document status; Inventory owns stock effects.", "Goods Receipt → Inventory movement → Finance source evidence", ["warehouse", "fromDate", "toDate", "status"], true, false, false);
        yield return Definition("procurement.match-exceptions", "Purchase match exceptions", "استثناءات مطابقة المشتريات", "Procurement", "Procurement PurchaseInvoiceMatchPersistence owns match result, variance classifications, lifecycle, and resolution evidence; Reporting reads a bounded Procurement-owned page.", "PO/receipt/invoice → PurchaseInvoiceMatchPersistence → Finance matching evidence", ["company", "fromDate", "toDate", "status"], false, false, true);
        yield return Definition("sales.orders", "Sales orders", "أوامر المبيعات", "Sales", "Sales owns commercial order status and customer chain.", "Quotation → Sales Order → fulfillment/invoice references", ["company", "branch", "status"], true, false, false);
        yield return Definition("sales.fulfillment", "Sales fulfillment", "تنفيذ المبيعات", "Sales", "Sales Delivery owns fulfillment document truth; Inventory movement identifiers and acknowledgement/reconciliation remain durable handoff evidence.", "Sales Order → Sales Delivery → Inventory handoff evidence", ["company", "branch", "warehouse", "status"], false, false, true);
        yield return Definition("sales.returns-credits", "Sales returns and credits", "مرتجعات وائتمانات المبيعات", "Sales", "Sales CustomerReturnPersistence owns return lifecycle and links; Inventory and Finance credit effects are published only from durable source-owned evidence.", "Delivery → Customer Return → Inventory/Finance evidence", ["company", "fromDate", "toDate", "status"], false, false, true);
        yield return Definition("operations.audit-activity", "Audit activity", "نشاط التدقيق", "SaaS/Admin", "Append-only Tenant audit evidence; no audit mutation.", "Audit evidence → report access evidence", ["fromDate", "toDate"], true, false, false);
        yield return Definition("operations.source-health", "Source health", "حالة المصادر", "SaaS/Admin", "Reports adapter/source state and does not fabricate business totals.", "Source adapter registry", [], true, false, false);
    }

    private static string Arabic(string code) => code switch
    {
        "finance.trial-balance" => "\u0645\u064a\u0632\u0627\u0646 \u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629",
        "finance.general-ledger" => "\u0627\u0644\u0623\u0633\u062a\u0627\u0630 \u0627\u0644\u0639\u0627\u0645",
        "finance.ap-aging" => "\u0623\u0639\u0645\u0627\u0631 \u0627\u0644\u062f\u0627\u0626\u0646\u064a\u0646",
        "finance.ar-aging" => "\u0623\u0639\u0645\u0627\u0631 \u0627\u0644\u0639\u0645\u0644\u0627\u0621",
        "finance.profit-loss" => "\u0627\u0644\u0623\u0631\u0628\u0627\u062d \u0648\u0627\u0644\u062e\u0633\u0627\u0626\u0631",
        "finance.balance-sheet" => "\u0627\u0644\u0645\u064a\u0632\u0627\u0646\u064a\u0629 \u0627\u0644\u0639\u0645\u0648\u0645\u064a\u0629",
        "finance.reconciliation" => "\u062a\u0633\u0648\u064a\u0629 \u0627\u0644\u0645\u0627\u0644\u064a\u0629",
        "finance.cash-movement" => "\u062d\u0631\u0643\u0629 \u0627\u0644\u0646\u0642\u062f",
        "finance.tax-summary" => "\u0645\u0644\u062e\u0635 \u0627\u0644\u0636\u0631\u0627\u0626\u0628",
        "finance.bank-reconciliation" => "\u062a\u0633\u0648\u064a\u0629 \u0627\u0644\u0628\u0646\u0643",
        "inventory.valuation" => "\u062a\u0642\u064a\u064a\u0645 \u0627\u0644\u0645\u062e\u0632\u0648\u0646",
        "inventory.stock-movements" => "\u062d\u0631\u0643\u0627\u062a \u0627\u0644\u0645\u062e\u0632\u0648\u0646",
        "inventory.stock-balance" => "\u0631\u0635\u064a\u062f \u0627\u0644\u0645\u062e\u0632\u0648\u0646",
        "inventory.count-variance" => "\u0641\u0631\u0648\u0642\u0627\u062a \u0627\u0644\u062c\u0631\u062f",
        "procurement.open-orders" => "\u0623\u0648\u0627\u0645\u0631 \u0627\u0644\u0634\u0631\u0627\u0621 \u0627\u0644\u0645\u0641\u062a\u0648\u062d\u0629",
        "procurement.receipts" => "\u0627\u0633\u062a\u0644\u0627\u0645\u0627\u062a \u0627\u0644\u0645\u0634\u062a\u0631\u064a\u0627\u062a",
        "procurement.match-exceptions" => "\u0627\u0633\u062a\u062b\u0646\u0627\u0621\u0627\u062a \u0645\u0637\u0627\u0628\u0642\u0629 \u0627\u0644\u0645\u0634\u062a\u0631\u064a\u0627\u062a",
        "sales.orders" => "\u0623\u0648\u0627\u0645\u0631 \u0627\u0644\u0645\u0628\u064a\u0639\u0627\u062a",
        "sales.fulfillment" => "\u062a\u0646\u0641\u064a\u0630 \u0627\u0644\u0645\u0628\u064a\u0639\u0627\u062a",
        "sales.returns-credits" => "\u0645\u0631\u062a\u062c\u0639\u0627\u062a \u0648\u0627\u0626\u062a\u0645\u0627\u0646\u0627\u062a \u0627\u0644\u0645\u0628\u064a\u0639\u0627\u062a",
        "operations.audit-activity" => "\u0646\u0634\u0627\u0637 \u0627\u0644\u062a\u062f\u0642\u064a\u0642",
        "operations.source-health" => "\u062d\u0627\u0644\u0629 \u0627\u0644\u0645\u0635\u0627\u062f\u0631",
        _ => code
    };
    private static ReportingDefinition Definition(string code, string name, string ar, string domain, string ownership, string reconciliation, string[] filters, bool export, bool scheduling, bool requiresCompany, bool pending = false, string? pendingCode = null, ReportingImplementationState implementationState = ReportingImplementationState.IMPLEMENTABLE_NOW) => new(code, name, Arabic(code), domain, "1.0", ownership, reconciliation, filters, export, scheduling, requiresCompany, pending, pendingCode, implementationState);
    private ReportingResult Result(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, IEnumerable<ReportingRow> rows, IReadOnlyList<ReportingColumn> columns, IReadOnlyList<ReportingSourceEvidence> sources, ReportingResultState state, string explanation, string reconciliation, int? totalRows = null)
    {
        var generated = clock.GetUtcNow();
        var array = rows.ToArray();
        var dataAsOf = ReportingTimeSemantics.AggregateDataAsOf(sources);
        var effectiveState = ReportingTimeSemantics.AggregateState(sources, state);
        return new ReportingResult(definition, Metadata(context, definition, query, sources, generated, dataAsOf, effectiveState, explanation, reconciliation), columns, array, totalRows ?? array.Length);
    }
    private static ReportingResult EmptyResult(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, ReportingResultState state, string code, string explanation, DateTimeOffset generated) => new(definition, Metadata(context, definition, query, [new ReportingSourceEvidence(definition.Domain, state.ToString(), null, null, code)], generated, null, state, explanation, "Not proven"), [], [], 0);
    private static ReportingResultMetadata Metadata(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, IReadOnlyList<ReportingSourceEvidence> sources, DateTimeOffset generated, DateTimeOffset? dataAsOf, ReportingResultState state, string? explanation, string reconciliation) => new(Guid.NewGuid(), context.TenantId.Value, ScopeText(query), definition.Code, definition.DefinitionVersion, Parameters(query), sources, generated, dataAsOf, state, state == ReportingResultState.Fresh ? "Fresh" : state.ToString(), reconciliation, null, context.CorrelationId, explanation);
    private static ReportingRow Row(string key, IReadOnlyDictionary<string, string?> values, string domain, string sourceReference, string status, DateTimeOffset? asOf, string reconciliation) => new(key, values, [new ReportingLineage(domain, sourceReference, status, "source-contract", reconciliation)]);
    private static ReportingColumn Column(string key, string label, string ar, string type) => new(key, label, ArabicColumn(key), type);
    private static string ArabicColumn(string key) => key switch
    {
        "account" => "\u0627\u0644\u062d\u0633\u0627\u0628", "name" => "\u0627\u0644\u0627\u0633\u0645", "type" => "\u0627\u0644\u0646\u0648\u0639", "opening" => "\u0627\u0644\u0627\u0641\u062a\u062a\u0627\u062d\u064a", "debit" => "\u0645\u062f\u064a\u0646", "credit" => "\u062f\u0627\u0626\u0646", "closing" => "\u0627\u0644\u0625\u0642\u0641\u0627\u0644", "currency" => "\u0627\u0644\u0639\u0645\u0644\u0629",
        "date" => "\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u062a\u0631\u062d\u064a\u0644", "journal" => "\u0627\u0644\u0642\u064a\u062f", "running" => "\u0627\u0644\u0631\u0635\u064a\u062f \u0627\u0644\u062c\u0627\u0631\u064a", "source" => "\u0627\u0644\u0645\u0635\u062f\u0631", "reference" => "\u0627\u0644\u0645\u0631\u062c\u0639", "documentDate" => "\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0645\u0633\u062a\u0646\u062f", "dueDate" => "\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0627\u0633\u062a\u062d\u0642\u0627\u0642", "bucket" => "\u0634\u0631\u064a\u062d\u0629 \u0627\u0644\u0627\u0633\u062a\u062d\u0642\u0627\u0642",
        "original" => "\u0627\u0644\u0623\u0635\u0644\u064a", "allocated" => "\u0627\u0644\u0645\u062e\u0635\u0635", "outstanding" => "\u0627\u0644\u0645\u0633\u062a\u062d\u0642", "status" => "\u0627\u0644\u062d\u0627\u0644\u0629", "scope" => "\u0627\u0644\u0646\u0637\u0627\u0642", "expected" => "\u0627\u0644\u0645\u062a\u0648\u0642\u0639", "actual" => "\u0627\u0644\u0641\u0639\u0644\u064a", "difference" => "\u0627\u0644\u0641\u0631\u0642", "detail" => "\u0627\u0644\u062a\u0641\u0627\u0635\u064a\u0644", "evidence" => "\u0627\u0644\u062f\u0644\u064a\u0644",
        "company" => "\u0627\u0644\u0634\u0631\u0643\u0629", "warehouse" => "\u0627\u0644\u0645\u0633\u062a\u0648\u062f\u0639", "quantity" => "\u0627\u0644\u0643\u0645\u064a\u0629", "valuedQuantity" => "\u0627\u0644\u0643\u0645\u064a\u0629 \u0627\u0644\u0645\u0642\u064a\u0645\u0629", "value" => "\u0627\u0644\u0642\u064a\u0645\u0629 \u0627\u0644\u0645\u0642\u064a\u0645\u0629", "inTransitQuantity" => "\u0643\u0645\u064a\u0629 \u0642\u064a\u062f \u0627\u0644\u0646\u0642\u0644", "inTransitValue" => "\u0642\u064a\u0645\u0629 \u0642\u064a\u062f \u0627\u0644\u0646\u0642\u0644", "pending" => "\u0627\u0644\u062d\u0631\u0643\u0627\u062a \u0627\u0644\u0645\u0639\u0644\u0642\u0629", "blocked" => "\u0627\u0644\u062d\u0631\u0643\u0627\u062a \u0627\u0644\u0645\u062d\u062c\u0648\u0628\u0629", "reconciliation" => "\u0627\u0644\u062a\u0633\u0648\u064a\u0629",
        "supplier" => "\u0627\u0644\u0645\u0648\u0631\u062f", "purchaseOrder" => "\u0623\u0645\u0631 \u0627\u0644\u0634\u0631\u0627\u0621", "receivedDate" => "\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0627\u0633\u062a\u0644\u0627\u0645", "acceptedQuantity" => "\u0627\u0644\u0643\u0645\u064a\u0629 \u0627\u0644\u0645\u0642\u0628\u0648\u0644\u0629", "lines" => "\u0627\u0644\u0628\u0646\u0648\u062f", "number" => "\u0627\u0644\u0623\u0645\u0631", "customer" => "\u0627\u0644\u0639\u0645\u064a\u0644", "quotation" => "\u0639\u0631\u0636 \u0627\u0644\u0633\u0639\u0631 \u0627\u0644\u0645\u0635\u062f\u0631", "revision" => "\u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629", "updatedAt" => "\u0627\u0644\u062a\u062d\u062f\u064a\u062b",
        "occurredAt" => "\u0648\u0642\u062a \u0627\u0644\u062d\u062f\u062b", "operation" => "\u0627\u0644\u0639\u0645\u0644\u064a\u0629", "decision" => "\u0627\u0644\u0642\u0631\u0627\u0631", "reason" => "\u0627\u0644\u0633\u0628\u0628", "correlation" => "\u0627\u0644\u062a\u0631\u0627\u0628\u0637", "ownership" => "\u0627\u0644\u0645\u0639\u0646\u0649 \u0627\u0644\u0645\u0648\u062b\u0648\u0642",
        _ => key
    };
    private bool TryResolveEffectiveScope(ReportingRequestContext context, ReportingDefinition definition, ReportingQuery query, out ReportingRequestContext? scopedContext, out ReportingQuery scopedQuery, out string code)
    {
        scopedContext = null;
        scopedQuery = query;
        code = "scope_denied";
        TenantWorkScopeRequest request;
        try
        {
            request = query.CompanyId.HasValue || query.BranchId.HasValue || query.WarehouseId.HasValue
                ? query.ToScopeRequest()
                : CurrentScopeRequest(context.TenantContext);
        }
        catch (ArgumentException)
        {
            code = "scope_invalid";
            return false;
        }

        var resolution = scopeOwnership.Resolve(context.TenantContext, request);
        if (!resolution.Allowed || resolution.Scope is null || resolution.Scope.TenantId != context.TenantId)
        {
            code = resolution.SafeReason.Contains("branch", StringComparison.OrdinalIgnoreCase) ? "branch_scope_denied" : "scope_denied";
            return false;
        }

        var scope = resolution.Scope;
        if (definition.RequiresCompany && scope.CompanyId is null)
        {
            code = "company_required";
            return false;
        }

        scopedQuery = query with
        {
            CompanyId = scope.CompanyId ?? query.CompanyId,
            BranchId = scope.BranchId ?? query.BranchId,
            WarehouseId = scope.WarehouseId ?? query.WarehouseId
        };
        var canonical = scope.WarehouseId is { } warehouse
            ? $"Warehouse:{warehouse:D}"
            : scope.BranchId is { } branch
                ? $"Branch:{branch:D}"
                : scope.CompanyId is { } company
                    ? $"Company:{company:D}"
                    : $"Tenant:{context.TenantId.Value:D}";
        var tenantContext = context.TenantContext.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership
            ? TenantContext.ForOrdinaryMembership(context.TenantId, context.TenantContext.Membership!.Value, new ScopeReference(canonical), context.TenantContext.CorrelationId, context.ActorId)
            : TenantContext.ForSupportGrant(context.TenantId, context.TenantContext.SupportGrant!.Value, new ScopeReference(canonical), context.TenantContext.CorrelationId, context.ActorId);
        scopedContext = context.WithTenantContext(tenantContext);
        code = "scope_verified";
        return true;
    }

    private bool IsStoredScopeAuthorized(ReportingRequestContext context, string scopeText)
    {
        try
        {
            var request = ParseStoredScope(scopeText);
            var resolution = scopeOwnership.Resolve(context.TenantContext, request);
            return resolution.Allowed && resolution.Scope is not null && resolution.Scope.TenantId == context.TenantId
                && resolution.Scope.CompanyId == request.CompanyId
                && resolution.Scope.BranchId == request.BranchId
                && resolution.Scope.WarehouseId == request.WarehouseId;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private ReportingRequestContext? ContextForStoredScope(ReportingRequestContext context, string scopeText)
    {
        try
        {
            var request = ParseStoredScope(scopeText);
            var resolution = scopeOwnership.Resolve(context.TenantContext, request);
            if (!resolution.Allowed || resolution.Scope is null) return null;
            var scope = resolution.Scope;
            var canonical = scope.WarehouseId is { } warehouse ? $"Warehouse:{warehouse:D}" : scope.BranchId is { } branch ? $"Branch:{branch:D}" : scope.CompanyId is { } company ? $"Company:{company:D}" : $"Tenant:{context.TenantId.Value:D}";
            var tenantContext = context.TenantContext.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership
                ? TenantContext.ForOrdinaryMembership(context.TenantId, context.TenantContext.Membership!.Value, new ScopeReference(canonical), context.TenantContext.CorrelationId, context.ActorId)
                : TenantContext.ForSupportGrant(context.TenantId, context.TenantContext.SupportGrant!.Value, new ScopeReference(canonical), context.TenantContext.CorrelationId, context.ActorId);
            return context.WithTenantContext(tenantContext);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private TenantWorkScopeRequest CurrentScopeRequest(TenantContext tenantContext)
    {
        if (scopeOwnership is ICurrentOrganizationScopeResolver current)
        {
            var resolution = current.ResolveCurrent(tenantContext);
            if (resolution.Allowed && resolution.Scope is { } selected)
            {
                return selected.WarehouseId is { } warehouse
                    ? TenantWorkScopeRequest.ForWarehouse(selected.CompanyId!.Value, selected.BranchId!.Value, warehouse)
                    : selected.BranchId is { } branch
                        ? TenantWorkScopeRequest.ForBranch(selected.CompanyId!.Value, branch)
                        : selected.CompanyId is { } company
                            ? TenantWorkScopeRequest.ForCompany(company)
                            : TenantWorkScopeRequest.TenantWide();
            }
            throw new ArgumentException("The trusted scope is not authorized.");
        }

        if (tenantContext.Scope is not { } value || value.Value.StartsWith("Tenant:", StringComparison.OrdinalIgnoreCase)) return TenantWorkScopeRequest.TenantWide();
        var parts = value.Value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && Guid.TryParse(parts[1], out var id))
            return parts[0].ToLowerInvariant() switch
            {
                "company" => TenantWorkScopeRequest.ForCompany(id),
                _ => throw new ArgumentException("The trusted scope marker is incomplete.")
            };
        throw new ArgumentException("The trusted scope marker is invalid.");
    }

    private static TenantWorkScopeRequest ParseStoredScope(string value)
    {
        if (string.Equals(value, "Tenant", StringComparison.OrdinalIgnoreCase) || value.StartsWith("Tenant:", StringComparison.OrdinalIgnoreCase)) return TenantWorkScopeRequest.TenantWide();
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0].Equals("Company", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(parts[1], out var company)) return TenantWorkScopeRequest.ForCompany(company);
        if (parts.Length == 3 && parts[0].Equals("Branch", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(parts[1], out var branchCompany) && Guid.TryParse(parts[2], out var branch)) return TenantWorkScopeRequest.ForBranch(branchCompany, branch);
        if (parts.Length == 4 && parts[0].Equals("Warehouse", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(parts[1], out var warehouseCompany) && Guid.TryParse(parts[2], out var warehouseBranch) && Guid.TryParse(parts[3], out var warehouse)) return TenantWorkScopeRequest.ForWarehouse(warehouseCompany, warehouseBranch, warehouse);
        throw new ArgumentException("Stored reporting scope is invalid.");
    }

    private static string StoredScopeText(ReportingQuery query) => query.WarehouseId is { } warehouse
        ? $"Warehouse:{query.CompanyId:D}:{query.BranchId:D}:{warehouse:D}"
        : query.BranchId is { } branch
            ? $"Branch:{query.CompanyId:D}:{branch:D}"
            : query.CompanyId is { } company
                ? $"Company:{company:D}"
                : "Tenant";
    private static bool AuditEvidenceInScope(FoundationAuditEvidence item, ScopeReference? scope)
    {
        if (scope is not { } selected || selected.Value.StartsWith("Tenant:", StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(item.OrganizationScope, selected.Value, StringComparison.OrdinalIgnoreCase);
    }
    private DateOnly ResolveAsOf(ReportingQuery query) => query.AsOfDate ?? DefaultAsOf;
    private static string ScopeText(ReportingQuery query) => query.WarehouseId is { } warehouse ? $"Warehouse:{warehouse:D}" : query.BranchId is { } branch ? $"Branch:{branch:D}" : query.CompanyId is { } company ? $"Company:{company:D}" : "Tenant";
    private static IReadOnlyDictionary<string, string?> Parameters(ReportingQuery query) => new Dictionary<string, string?> { ["companyId"] = query.CompanyId?.ToString("D"), ["branchId"] = query.BranchId?.ToString("D"), ["warehouseId"] = query.WarehouseId?.ToString("D"), ["fromDate"] = query.FromDate?.ToString("yyyy-MM-dd"), ["toDate"] = query.ToDate?.ToString("yyyy-MM-dd"), ["asOfDate"] = query.AsOfDate?.ToString("yyyy-MM-dd"), ["status"] = query.Status, ["currencyCode"] = query.CurrencyCode, ["sortBy"] = query.SortBy, ["sortDirection"] = query.SortDirection, ["page"] = query.Page.ToString(CultureInfo.InvariantCulture), ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture) };
    private static string Fingerprint(string code, ReportingQuery query, string version) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{code}|{version}|{string.Join('|', Parameters(query).OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"))}")));
    private static T? ParseOptionalEnum<T>(string? value) where T : struct, Enum => string.IsNullOrWhiteSpace(value)
        ? null
        : Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException("A report status is invalid.");
    private static void ValidateQuery(ReportingDefinition definition, ReportingQuery query, ReportingQuery? suppliedQuery = null)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 500) throw new ArgumentException("Page bounds are invalid.");
        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate > query.ToDate) throw new ArgumentException("Date bounds are invalid.");
        if (definition.RequiresCompany && query.CompanyId is null) throw new ArgumentException("Company is required.");
        if (query.BranchId.HasValue && !query.CompanyId.HasValue || query.WarehouseId.HasValue && !query.BranchId.HasValue) throw new ArgumentException("Organization scope is invalid.");
        var requested = suppliedQuery ?? query;
        var supplied = new List<string>();
        if (requested.CompanyId.HasValue) supplied.Add("company");
        if (requested.BranchId.HasValue) supplied.Add("branch");
        if (requested.WarehouseId.HasValue) supplied.Add("warehouse");
        if (requested.FromDate.HasValue) supplied.Add("fromDate");
        if (requested.ToDate.HasValue) supplied.Add("toDate");
        if (requested.AsOfDate.HasValue) supplied.Add("asOfDate");
        if (!string.IsNullOrWhiteSpace(requested.Status)) supplied.Add("status");
        if (!string.IsNullOrWhiteSpace(requested.CurrencyCode)) supplied.Add("currency");
        var allowed = definition.AllowedFilters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (supplied.Any(item => !allowed.Contains(item))) throw new ArgumentException("A supplied report filter is not allowed.");
        if (!string.IsNullOrWhiteSpace(query.SortDirection) && query.SortDirection.Trim().ToLowerInvariant() is not ("asc" or "desc")) throw new ArgumentException("Sort direction is invalid.");
        if (!string.IsNullOrWhiteSpace(query.SortBy) && !AllowedSortKeys(definition.Code).Contains(query.SortBy.Trim(), StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Sort key is not supported by this report.");
    }

    private static IReadOnlyList<string> AllowedSortKeys(string code) => code switch
    {
        "finance.general-ledger" => ["date", "journal", "account", "debit", "credit"],
        "finance.cash-movement" => ["documentDate", "amount", "direction", "status"],
        "finance.ap-aging" or "finance.ar-aging" => ["dueDate", "outstanding", "status"],
        "inventory.stock-movements" => ["effectiveOn", "quantity", "status"],
        "inventory.stock-balance" => ["quantity", "value", "updatedAt"],
        "procurement.open-orders" => ["supplier", "status", "currency", "createdAt"],
        "procurement.receipts" => ["receivedDate", "status", "supplier"],
        "procurement.match-exceptions" => ["evaluatedAt", "status", "lifecycle"],
        "sales.orders" => ["number", "customer", "status", "total"],
        "sales.fulfillment" => ["createdAt", "postedAt", "status", "warehouse"],
        "sales.returns-credits" => ["returnDate", "updatedAt", "status"],
        "operations.audit-activity" => ["occurredAt"],
        _ => ["account", "date", "status"]
    };
    private static string Amount(decimal value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private static string? OptionalAmount(decimal? value) => value is { } amount ? Amount(amount) : null;
    private static string ToCsv(ReportingResult result) { var builder = new StringBuilder(); builder.AppendLine(string.Join(',', result.Columns.Select(item => Escape(item.Label)))); foreach (var row in result.Rows) builder.AppendLine(string.Join(',', result.Columns.Select(column => Escape(row.Values.TryGetValue(column.Key, out var value) ? value ?? string.Empty : string.Empty)))); return builder.ToString(); }
    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

#pragma warning restore CS1591
