#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.Modules.Audit;

/// <summary>
/// Safe text rules shared by evidence construction and telemetry projection.
/// </summary>
internal static class FoundationAuditText
{
    public const int MaximumLength = 128;

    public static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The evidence value is required.", name);
        }

        return Validated(value, name);
    }

    public static string? Optional(string? value, string name)
    {
        return value is null ? null : Validated(value, name);
    }

    private static string Validated(string value, string name)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > MaximumLength)
        {
            throw new ArgumentException("The evidence value is outside the approved bound.", name);
        }

        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The evidence value contains a control character.", name);
        }

        return normalized;
    }
}

/// <summary>
/// Creates evidence from an immutable, server-derived Foundation request
/// context. No caller-supplied Tenant or target identifier is accepted.
/// </summary>
public static class FoundationAuditEvidenceFactory
{
    public static FoundationAuditEvidence Create(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotencyKey = null,
        string? operationVersion = null,
        string? supportPurpose = null,
        DateTimeOffset? supportGrantExpiresAt = null,
        Guid? retryOfEvidenceId = null,
        int attempt = 1,
        DateTimeOffset? occurredAt = null,
        string? source = null,
        string? targetType = null,
        string? targetReference = null,
        string? changeSummary = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var operation = FoundationAuditText.Required(operationId, nameof(operationId));
        var correlation = FoundationAuditText.Required(correlationId, nameof(correlationId));
        if (!FoundationCorrelation.IsValid(correlation))
        {
            throw new ArgumentException("The correlation identifier is invalid.", nameof(correlationId));
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        if (decision == FoundationAuditDecision.Retry
            && (retryOfEvidenceId is null || attempt < 2))
        {
            throw new ArgumentException("A retry must identify its prior evidence.", nameof(retryOfEvidenceId));
        }

        if (decision is not (FoundationAuditDecision.Retry or FoundationAuditDecision.EffectFailed)
            && retryOfEvidenceId is not null)
        {
            throw new ArgumentException("Only retry evidence may reference prior evidence.", nameof(retryOfEvidenceId));
        }

        var idempotency = FoundationAuditText.Optional(idempotencyKey, nameof(idempotencyKey));
        if (idempotency is not null && !FoundationCorrelation.IsValid(idempotency))
        {
            throw new ArgumentException("The idempotency key is invalid.", nameof(idempotencyKey));
        }

        var version = FoundationAuditText.Optional(operationVersion, nameof(operationVersion));
        var evidenceSource = FoundationAuditText.Required(source ?? "application", nameof(source));
        var safeTargetType = FoundationAuditText.Optional(targetType, nameof(targetType));
        var safeTargetReference = FoundationAuditText.Optional(targetReference, nameof(targetReference));
        var safeChangeSummary = FoundationAuditText.Optional(changeSummary, nameof(changeSummary));
        var clock = timeProvider ?? TimeProvider.System;
        var actorId = context.ActorId
            ?? throw new ArgumentException("A protected context requires a server actor.", nameof(context));
        var sessionId = context.SessionId
            ?? throw new ArgumentException("A protected context requires a server session.", nameof(context));

        if (context.SecurityProfile == FoundationSecurityProfile.OrdinaryMembership
            && (supportPurpose is not null || supportGrantExpiresAt is not null))
        {
            throw new ArgumentException("Ordinary evidence cannot contain support facts.", nameof(supportPurpose));
        }

        return context.SecurityProfile switch
        {
            FoundationSecurityProfile.OrdinaryMembership => CreateTenantEvidence(
                context,
                operation,
                correlation,
                actorId,
                sessionId,
                FoundationAuditAuthorizationPath.OrdinaryMembership,
                decision,
                reason,
                idempotency,
                version,
                supportPurpose: null,
                supportGrantExpiresAt: null,
                retryOfEvidenceId,
                attempt,
                occurredAt,
                evidenceSource,
                safeTargetType,
                safeTargetReference,
                safeChangeSummary,
                clock),
            FoundationSecurityProfile.SupportGrant => CreateTenantEvidence(
                context,
                operation,
                correlation,
                actorId,
                sessionId,
                FoundationAuditAuthorizationPath.SupportGrant,
                decision,
                reason,
                idempotency,
                version,
                FoundationAuditText.Required(supportPurpose, nameof(supportPurpose)),
                supportGrantExpiresAt,
                retryOfEvidenceId,
                attempt,
                occurredAt,
                evidenceSource,
                safeTargetType,
                safeTargetReference,
                safeChangeSummary,
                clock),
            FoundationSecurityProfile.PlatformGovernanceContext => CreatePlatformEvidence(
                context,
                operation,
                correlation,
                actorId,
                sessionId,
                decision,
                reason,
                idempotency,
                version,
                retryOfEvidenceId,
                attempt,
                occurredAt,
                evidenceSource,
                safeTargetType,
                safeTargetReference,
                safeChangeSummary,
                clock),
            _ => throw new ArgumentException(
                "Evidence requires one explicit Tenant or platform authorization path.",
                nameof(context))
        };
    }

    private static FoundationAuditEvidence CreateTenantEvidence(
        FoundationRequestContext context,
        string operation,
        string correlation,
        Guid actorId,
        Guid sessionId,
        FoundationAuditAuthorizationPath path,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotency,
        string? version,
        string? supportPurpose,
        DateTimeOffset? supportGrantExpiresAt,
        Guid? retryOfEvidenceId,
        int attempt,
        DateTimeOffset? occurredAt,
        string source,
        string? targetType,
        string? targetReference,
        string? changeSummary,
        TimeProvider timeProvider)
    {
        var tenant = context.TenantContext
            ?? throw new ArgumentException("Tenant evidence requires a trusted Tenant context.", nameof(context));

        var expectedPath = path == FoundationAuditAuthorizationPath.OrdinaryMembership
            ? TenantAuthorizationPath.OrdinaryMembership
            : TenantAuthorizationPath.SupportGrant;
        if (tenant.AuthorizationPath != expectedPath || context.PlatformGovernanceContext is not null)
        {
            throw new ArgumentException("The evidence path does not match the trusted Tenant context.", nameof(context));
        }

        Guid? supportUserId = path == FoundationAuditAuthorizationPath.SupportGrant ? actorId : null;
        var supportGrantId = tenant.SupportGrant?.GrantId;
        var supportCaseId = tenant.SupportGrant?.CaseId;
        if (path == FoundationAuditAuthorizationPath.SupportGrant
            && (supportGrantId is null || supportCaseId is null))
        {
            throw new ArgumentException("Support evidence requires a trusted grant and case.", nameof(context));
        }

        if (path == FoundationAuditAuthorizationPath.OrdinaryMembership
            && (supportPurpose is not null || supportGrantExpiresAt is not null))
        {
            throw new ArgumentException("Ordinary evidence cannot contain support facts.", nameof(supportPurpose));
        }

        return new FoundationAuditEvidence(
            Guid.NewGuid(),
            occurredAt ?? timeProvider.GetUtcNow(),
            operation,
            correlation,
            actorId,
            sessionId,
            path,
            tenant.TenantId.Value,
            tenant.Scope?.Value,
            supportPurpose,
            supportUserId,
            supportGrantId,
            supportCaseId,
            supportGrantExpiresAt,
            decision,
            reason,
            idempotency,
            version,
            retryOfEvidenceId,
            attempt,
            source,
            targetType,
            targetReference,
            changeSummary);
    }

    private static FoundationAuditEvidence CreatePlatformEvidence(
        FoundationRequestContext context,
        string operation,
        string correlation,
        Guid actorId,
        Guid sessionId,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotency,
        string? version,
        Guid? retryOfEvidenceId,
        int attempt,
        DateTimeOffset? occurredAt,
        string source,
        string? targetType,
        string? targetReference,
        string? changeSummary,
        TimeProvider timeProvider)
    {
        var platform = context.PlatformGovernanceContext
            ?? throw new ArgumentException("Platform evidence requires a trusted governance context.", nameof(context));
        if (context.TenantContext is not null)
        {
            throw new ArgumentException("Platform evidence cannot contain a Tenant context.", nameof(context));
        }

        return new FoundationAuditEvidence(
            Guid.NewGuid(),
            occurredAt ?? timeProvider.GetUtcNow(),
            operation,
            correlation,
            actorId,
            sessionId,
            FoundationAuditAuthorizationPath.PlatformGovernanceContext,
            tenantId: null,
            organizationScope: null,
            platform.Purpose.ToString(),
            supportUserId: null,
            supportGrantId: null,
            supportCaseId: null,
            supportGrantExpiresAt: null,
            decision,
            reason,
            idempotency,
            version,
            retryOfEvidenceId,
            attempt,
            source,
            targetType,
            targetReference,
            changeSummary);
    }
}

/// <summary>
/// Validates the immutable evidence invariants before local persistence.
/// </summary>
internal static class FoundationAuditEvidenceMapping
{
    public static void Validate(FoundationAuditEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.EvidenceId == Guid.Empty
            || evidence.ActorId == Guid.Empty
            || evidence.SessionId == Guid.Empty
            || evidence.Attempt < 1
            || !Enum.IsDefined(evidence.AuthorizationPath)
            || !Enum.IsDefined(evidence.Decision)
            || !Enum.IsDefined(evidence.Reason))
        {
            throw new FoundationAuditAppendException("evidence_invalid");
        }

        _ = FoundationAuditText.Required(evidence.OperationId, nameof(evidence.OperationId));
        _ = FoundationAuditText.Required(evidence.CorrelationId, nameof(evidence.CorrelationId));
        if (!FoundationCorrelation.IsValid(evidence.CorrelationId))
        {
            throw new FoundationAuditAppendException("correlation_invalid");
        }

        var tenantPath = evidence.AuthorizationPath is
            FoundationAuditAuthorizationPath.OrdinaryMembership or FoundationAuditAuthorizationPath.SupportGrant;
        if (tenantPath != (evidence.TenantId.HasValue && evidence.TenantId.Value != Guid.Empty))
        {
            throw new FoundationAuditAppendException("tenant_path_invalid");
        }

        if (evidence.AuthorizationPath == FoundationAuditAuthorizationPath.PlatformGovernanceContext
            && string.IsNullOrWhiteSpace(evidence.Purpose))
        {
            throw new FoundationAuditAppendException("platform_purpose_missing");
        }

        if (evidence.AuthorizationPath == FoundationAuditAuthorizationPath.OrdinaryMembership
            && (evidence.Purpose is not null
                || evidence.SupportUserId is not null
                || evidence.SupportGrantId is not null
                || evidence.SupportCaseId is not null
                || evidence.SupportGrantExpiresAt is not null))
        {
            throw new FoundationAuditAppendException("ordinary_support_facts_invalid");
        }

        if (evidence.AuthorizationPath == FoundationAuditAuthorizationPath.SupportGrant
            && (evidence.SupportUserId != evidence.ActorId
                || evidence.SupportGrantId is null
                || evidence.SupportCaseId is null
                || string.IsNullOrWhiteSpace(evidence.Purpose)))
        {
            throw new FoundationAuditAppendException("support_facts_missing");
        }

        if (evidence.Decision == FoundationAuditDecision.Retry
            ? evidence.RetryOfEvidenceId is null || evidence.Attempt < 2
            : evidence.Decision == FoundationAuditDecision.EffectFailed
                ? evidence.RetryOfEvidenceId is null || evidence.Attempt < 2
                : evidence.RetryOfEvidenceId is not null)
        {
            throw new FoundationAuditAppendException("retry_relationship_invalid");
        }

        if (evidence.IdempotencyKey is not null && !FoundationCorrelation.IsValid(evidence.IdempotencyKey))
        {
            throw new FoundationAuditAppendException("idempotency_invalid");
        }

        _ = FoundationAuditText.Required(evidence.Source, nameof(evidence.Source));
        _ = FoundationAuditText.Optional(evidence.OrganizationScope, nameof(evidence.OrganizationScope));
        _ = FoundationAuditText.Optional(evidence.Purpose, nameof(evidence.Purpose));
        _ = FoundationAuditText.Optional(evidence.OperationVersion, nameof(evidence.OperationVersion));
        _ = FoundationAuditText.Optional(evidence.TargetType, nameof(evidence.TargetType));
        _ = FoundationAuditText.Optional(evidence.TargetReference, nameof(evidence.TargetReference));
        _ = FoundationAuditText.Optional(evidence.ChangeSummary, nameof(evidence.ChangeSummary));
    }
}

/// <summary>
/// Safe append-only evidence sink. Implementations must not expose mutation or
/// deletion operations.
/// </summary>
public interface IFoundationAuditEvidenceSink
{
    ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicit Tenant-bound read surface. There is intentionally no unscoped
/// query method.
/// </summary>
public interface IFoundationAuditEvidenceReader
{
    ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForTenantAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForPlatformAsync(
        PlatformGovernanceContext governanceContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Scope-aware audit read seam. Audit remains the source owner; consumers
/// receive only evidence at the requested effective organization boundary.
/// </summary>
public interface IFoundationAuditScopedEvidenceReader
{
    ValueTask<ReportingSourcePage<FoundationAuditEvidence>> ReadForTenantScopeAsync(
        TenantContext tenantContext,
        TenantWorkScope scope,
        ReportingPageRequest page,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bounded, Tenant- and organization-scope-bound audit search. The source owns
/// filtering and deterministic ordering; callers cannot request an unscoped
/// or unbounded evidence read.
/// </summary>
public interface IFoundationAuditSearchReader
{
    ValueTask<ReportingSourcePage<FoundationAuditEvidence>> SearchAsync(
        TenantContext tenantContext,
        TenantWorkScope scope,
        FoundationAuditSearch search,
        CancellationToken cancellationToken = default);
}

/// <summary>Safe audit filters with bounded paging and time windows.</summary>
public sealed record FoundationAuditSearch(
    ReportingPageRequest Page,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? ActorId = null,
    string? OperationId = null,
    FoundationAuditDecision? Decision = null,
    FoundationAuditReason? Reason = null,
    string? Source = null,
    string? TargetType = null,
    string? TargetReference = null,
    string? CorrelationId = null)
{
    public const int MaximumWindowDays = 366;

    public static FoundationAuditSearch Create(
        ReportingPageRequest page,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        Guid? actorId = null,
        string? operationId = null,
        FoundationAuditDecision? decision = null,
        FoundationAuditReason? reason = null,
        string? source = null,
        string? targetType = null,
        string? targetReference = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Page > 100_000)
        {
            throw new ArgumentException("The audit page is too deep.", nameof(page));
        }
        if (from.HasValue && to.HasValue && from > to)
        {
            throw new ArgumentException("The audit time window is invalid.");
        }

        if (from.HasValue && to.HasValue && to.Value - from.Value > TimeSpan.FromDays(MaximumWindowDays))
        {
            throw new ArgumentException("The audit time window is too large.");
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("The actor filter must be a valid identifier.", nameof(actorId));
        }

        if (decision.HasValue && !Enum.IsDefined(decision.Value)
            || reason.HasValue && !Enum.IsDefined(reason.Value))
        {
            throw new ArgumentException("The audit outcome filter is invalid.");
        }

        return new FoundationAuditSearch(
            page,
            from,
            to,
            actorId,
            SafeOptional(operationId, nameof(operationId)),
            decision,
            reason,
            SafeOptional(source, nameof(source)),
            SafeOptional(targetType, nameof(targetType)),
            SafeOptional(targetReference, nameof(targetReference)),
            SafeOptional(correlationId, nameof(correlationId)));
    }

    private static string? SafeOptional(string? value, string name)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > FoundationAuditText.MaximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The audit filter is invalid or unbounded.", name);
        }

        return normalized;
    }
}

/// <summary>
/// A bounded local append-only store used for isolated validation only. It is
/// not a production retention, purge or database provider.
/// </summary>
public sealed class LocalImmutableAuditEvidenceStore : IFoundationAuditEvidenceSink, IFoundationAuditEvidenceReader, IFoundationAuditScopedEvidenceReader, IFoundationAuditSearchReader
{
    private readonly object syncRoot = new();
    private readonly List<FoundationAuditEvidence> evidence = [];
    private readonly int capacity;

    public LocalImmutableAuditEvidenceStore(int capacity = 2048)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public ValueTask AppendAsync(
        FoundationAuditEvidence item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FoundationAuditEvidenceMapping.Validate(item);

        lock (syncRoot)
        {
            if (evidence.Count >= capacity)
            {
                throw new FoundationAuditAppendException("evidence_capacity_reached");
            }

            if (evidence.Any(existing => existing.EvidenceId == item.EvidenceId))
            {
                throw new FoundationAuditAppendException("evidence_id_reuse");
            }

            evidence.Add(item);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForTenantAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantContext.TenantId.Value;
        var expectedPath = tenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationAuditAuthorizationPath.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationAuditAuthorizationPath.SupportGrant,
            _ => throw new ArgumentOutOfRangeException(nameof(tenantContext))
        };
        lock (syncRoot)
        {
            IReadOnlyList<FoundationAuditEvidence> result = evidence
                .Where(item => item.TenantId == tenantId
                    && item.AuthorizationPath == expectedPath)
                .ToArray();
            return ValueTask.FromResult(result);
        }
    }

    public ValueTask<ReportingSourcePage<FoundationAuditEvidence>> ReadForTenantScopeAsync(
        TenantContext tenantContext,
        TenantWorkScope scope,
        ReportingPageRequest page,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantContext.TenantId.Value;
        var expectedPath = tenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationAuditAuthorizationPath.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationAuditAuthorizationPath.SupportGrant,
            _ => throw new ArgumentOutOfRangeException(nameof(tenantContext))
        };
        lock (syncRoot)
        {
            // The local audit seam deliberately uses the immutable canonical
            // scope marker. It never widens a restricted request to all
            // Tenant evidence; descendant graph expansion remains an Identity
            // concern and is represented by the server-issued context passed
            // to source reads.
            var search = FoundationAuditSearch.Create(
                page,
                fromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                toDate?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
            return SearchUnsafe(tenantContext, scope, search, tenantId, expectedPath);
        }
    }

    public ValueTask<ReportingSourcePage<FoundationAuditEvidence>> SearchAsync(
        TenantContext tenantContext,
        TenantWorkScope scope,
        FoundationAuditSearch search,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(search);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantContext.TenantId.Value;
        var expectedPath = tenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationAuditAuthorizationPath.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationAuditAuthorizationPath.SupportGrant,
            _ => throw new ArgumentOutOfRangeException(nameof(tenantContext))
        };
        lock (syncRoot)
        {
            return SearchUnsafe(tenantContext, scope, search, tenantId, expectedPath);
        }
    }

    private ValueTask<ReportingSourcePage<FoundationAuditEvidence>> SearchUnsafe(
        TenantContext tenantContext,
        TenantWorkScope scope,
        FoundationAuditSearch search,
        Guid tenantId,
        FoundationAuditAuthorizationPath expectedPath)
    {
        if (scope.TenantId != tenantContext.TenantId)
        {
            return ValueTask.FromResult(ReportingSourcePage<FoundationAuditEvidence>.Empty("occurredAt desc,evidenceId desc"));
        }

        var candidates = evidence
            .Where(item => item.TenantId == tenantId && item.AuthorizationPath == expectedPath)
            .Where(item => scope.CompanyId is null
                ? true
                : scope.WarehouseId is { } warehouseId
                    ? string.Equals(item.OrganizationScope, $"Warehouse:{warehouseId:D}", StringComparison.OrdinalIgnoreCase)
                    : scope.BranchId is { } branchId
                        ? string.Equals(item.OrganizationScope, $"Branch:{branchId:D}", StringComparison.OrdinalIgnoreCase)
                        : string.Equals(item.OrganizationScope, $"Company:{scope.CompanyId:D}", StringComparison.OrdinalIgnoreCase))
            .Where(item => !search.From.HasValue || item.OccurredAt >= search.From.Value)
            .Where(item => !search.To.HasValue || item.OccurredAt <= search.To.Value)
            .Where(item => !search.ActorId.HasValue || item.ActorId == search.ActorId.Value)
            .Where(item => search.OperationId is null || string.Equals(item.OperationId, search.OperationId, StringComparison.Ordinal))
            .Where(item => !search.Decision.HasValue || item.Decision == search.Decision.Value)
            .Where(item => !search.Reason.HasValue || item.Reason == search.Reason.Value)
            .Where(item => search.Source is null || string.Equals(item.Source, search.Source, StringComparison.Ordinal))
            .Where(item => search.TargetType is null || string.Equals(item.TargetType, search.TargetType, StringComparison.Ordinal))
            .Where(item => search.TargetReference is null || string.Equals(item.TargetReference, search.TargetReference, StringComparison.Ordinal))
            .Where(item => search.CorrelationId is null || string.Equals(item.CorrelationId, search.CorrelationId, StringComparison.Ordinal))
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.EvidenceId)
            .ToArray();
        var rows = candidates.Skip(search.Page.Offset).Take(search.Page.PageSize).ToArray();
        return ValueTask.FromResult(new ReportingSourcePage<FoundationAuditEvidence>(rows, candidates.Length, "occurredAt desc,evidenceId desc", candidates.Length == 0 ? null : candidates.Min(item => item.OccurredAt)));
    }

    public ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForPlatformAsync(
        PlatformGovernanceContext governanceContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(governanceContext);
        cancellationToken.ThrowIfCancellationRequested();
        var purpose = governanceContext.Purpose.ToString();
        lock (syncRoot)
        {
            IReadOnlyList<FoundationAuditEvidence> result = evidence
                .Where(item => item.AuthorizationPath == FoundationAuditAuthorizationPath.PlatformGovernanceContext
                    && item.TenantId is null
                    && string.Equals(item.Purpose, purpose, StringComparison.Ordinal))
                .ToArray();
            return ValueTask.FromResult(result);
        }
    }

    internal int Count
    {
        get
        {
            lock (syncRoot)
            {
                return evidence.Count;
            }
        }
    }
}

/// <summary>
/// Safe append failure with an allow-listed code and no provider detail.
/// </summary>
public sealed class FoundationAuditAppendException : InvalidOperationException
{
    public FoundationAuditAppendException(string code)
        : base("Mandatory audit evidence could not be appended.")
    {
        Code = FoundationAuditText.Required(code, nameof(code));
    }

    public string Code { get; }
}

/// <summary>
/// Provides an in-process structured log, metric and trace hook. No exporter
/// or endpoint is configured by the Foundation slice.
/// </summary>
public interface IFoundationAuditTelemetrySink
{
    void Emit(FoundationAuditTelemetryEvent telemetryEvent);
}

public sealed class LocalFoundationAuditTelemetrySink : IFoundationAuditTelemetrySink
{
    private readonly object syncRoot = new();
    private readonly List<FoundationAuditTelemetryEvent> events = [];
    private readonly int capacity;

    public LocalFoundationAuditTelemetrySink(int capacity = 2048)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public IReadOnlyList<FoundationAuditTelemetryEvent> Events
    {
        get
        {
            lock (syncRoot)
            {
                return events.ToArray();
            }
        }
    }

    public void Emit(FoundationAuditTelemetryEvent telemetryEvent)
    {
        lock (syncRoot)
        {
            if (events.Count < capacity)
            {
                events.Add(telemetryEvent);
            }
        }
    }

    internal void Clear()
    {
        lock (syncRoot)
        {
            events.Clear();
        }
    }
}

/// <summary>
/// Bounded safe operational signal hook for evidence failures.
/// </summary>
public interface IFoundationAuditOperationalSignalSink
{
    void Emit(FoundationAuditOperationalSignal signal);
}

public sealed class LocalFoundationAuditOperationalSignalSink : IFoundationAuditOperationalSignalSink
{
    private readonly object syncRoot = new();
    private readonly List<FoundationAuditOperationalSignal> signals = [];
    private readonly int capacity;

    public LocalFoundationAuditOperationalSignalSink(int capacity = 256)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public IReadOnlyList<FoundationAuditOperationalSignal> Signals
    {
        get
        {
            lock (syncRoot)
            {
                return signals.ToArray();
            }
        }
    }

    public void Emit(FoundationAuditOperationalSignal signal)
    {
        lock (syncRoot)
        {
            if (signals.Count < capacity)
            {
                signals.Add(signal);
            }
        }
    }

    internal void Clear()
    {
        lock (syncRoot)
        {
            signals.Clear();
        }
    }
}

/// <summary>
/// Projects evidence through an explicit allow-list. The optional input bag
/// is intentionally ignored; arbitrary request objects are never serialized.
/// </summary>
public static class FoundationAuditRedactor
{
    public static FoundationAuditTelemetryEvent ToTelemetry(
        FoundationAuditEvidence evidence,
        FoundationAuditTelemetryKind kind,
        IReadOnlyDictionary<string, string?>? ignoredInput = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        _ = ignoredInput;
        return new FoundationAuditTelemetryEvent(
            Guid.NewGuid(),
            evidence.OccurredAt,
            kind,
            evidence.EvidenceId,
            evidence.OperationId,
            evidence.CorrelationId,
            evidence.AuthorizationPath,
            evidence.TenantId,
            evidence.Decision,
            evidence.Reason,
            evidence.Decision == FoundationAuditDecision.Retry);
    }
}

/// <summary>
/// Result of an evidence append attempt. A failed result contains no business
/// value and is safe to expose to an API boundary.
/// </summary>
public sealed record FoundationAuditRecordResult(
    bool Succeeded,
    FoundationAuditEvidence? Evidence,
    string Code)
{
    public static FoundationAuditRecordResult Success(FoundationAuditEvidence evidence) =>
        new(true, evidence, "recorded");

    public static FoundationAuditRecordResult Failure(string code) =>
        new(false, null, code);
}

/// <summary>
/// Result for a protected effect that requires mandatory evidence first.
/// </summary>
public sealed record FoundationAuditExecutionResult<T>(
    bool Succeeded,
    T? Value,
    FoundationAuditEvidence? Evidence,
    string Code)
{
    public static FoundationAuditExecutionResult<T> Success(T value, FoundationAuditEvidence evidence) =>
        new(true, value, evidence, "success");

    public static FoundationAuditExecutionResult<T> Failure(string code) =>
        new(false, default, null, code);
}

/// <summary>
/// Coordinates mandatory append-before-effect behavior and safe telemetry.
/// </summary>
public sealed class FoundationAuditCoordinator
{
    private readonly IFoundationAuditEvidenceSink evidenceSink;
    private readonly IFoundationAuditTelemetrySink telemetrySink;
    private readonly IFoundationAuditOperationalSignalSink signalSink;
    private readonly TimeProvider timeProvider;

    public FoundationAuditCoordinator(
        IFoundationAuditEvidenceSink evidenceSink,
        IFoundationAuditTelemetrySink telemetrySink,
        IFoundationAuditOperationalSignalSink signalSink,
        TimeProvider? timeProvider = null)
    {
        this.evidenceSink = evidenceSink ?? throw new ArgumentNullException(nameof(evidenceSink));
        this.telemetrySink = telemetrySink ?? throw new ArgumentNullException(nameof(telemetrySink));
        this.signalSink = signalSink ?? throw new ArgumentNullException(nameof(signalSink));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<FoundationAuditRecordResult> RecordAsync(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotencyKey = null,
        string? operationVersion = null,
        string? supportPurpose = null,
        DateTimeOffset? supportGrantExpiresAt = null,
        Guid? retryOfEvidenceId = null,
        int attempt = 1,
        CancellationToken cancellationToken = default,
        string? source = null,
        string? targetType = null,
        string? targetReference = null,
        string? changeSummary = null)
    {
        FoundationAuditEvidence evidence;
        try
        {
            evidence = FoundationAuditEvidenceFactory.Create(
                context,
                operationId,
                correlationId,
                decision,
                reason,
                idempotencyKey,
                operationVersion,
                supportPurpose,
                supportGrantExpiresAt,
                retryOfEvidenceId,
                attempt,
                occurredAt: timeProvider.GetUtcNow(),
                source: source,
                targetType: targetType,
                targetReference: targetReference,
                changeSummary: changeSummary,
                timeProvider: timeProvider);
        }
        catch
        {
            EmitFailureSignal(operationId, correlationId);
            return FoundationAuditRecordResult.Failure("audit_evidence_invalid");
        }

        try
        {
            await evidenceSink.AppendAsync(evidence, cancellationToken);
        }
        catch
        {
            EmitFailureSignal(evidence.OperationId, evidence.CorrelationId);
            return FoundationAuditRecordResult.Failure("audit_evidence_unavailable");
        }

        EmitTelemetry(evidence);
        return FoundationAuditRecordResult.Success(evidence);
    }

    public Task<FoundationAuditRecordResult> RecordRetryAsync(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        Guid priorEvidenceId,
        int attempt,
        string? idempotencyKey = null,
        string? operationVersion = null,
        CancellationToken cancellationToken = default) => RecordAsync(
            context,
            operationId,
            correlationId,
            FoundationAuditDecision.Retry,
            FoundationAuditReason.RetryAccepted,
            idempotencyKey,
            operationVersion,
            retryOfEvidenceId: priorEvidenceId,
            attempt: attempt,
            cancellationToken: cancellationToken);

    public async Task<FoundationAuditExecutionResult<T>> ExecuteProtectedAsync<T>(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        FoundationAuditReason reason,
        Func<Task<T>> protectedEffect,
        string? idempotencyKey = null,
        string? operationVersion = null,
        string? supportPurpose = null,
        DateTimeOffset? supportGrantExpiresAt = null,
        CancellationToken cancellationToken = default,
        FoundationAuditDecision decision = FoundationAuditDecision.Allowed)
    {
        ArgumentNullException.ThrowIfNull(protectedEffect);
        var record = await RecordAsync(
            context,
            operationId,
            correlationId,
            decision,
            reason,
            idempotencyKey,
            operationVersion,
            supportPurpose,
            supportGrantExpiresAt,
            cancellationToken: cancellationToken);
        if (!record.Succeeded || record.Evidence is null)
        {
            return FoundationAuditExecutionResult<T>.Failure(record.Code);
        }

        if (decision != FoundationAuditDecision.Allowed)
        {
            return FoundationAuditExecutionResult<T>.Failure("authorization_denied");
        }

        try
        {
            var value = await protectedEffect();
            return FoundationAuditExecutionResult<T>.Success(value, record.Evidence);
        }
        catch
        {
            var failureRecord = await RecordAsync(
                context,
                operationId,
                correlationId,
                FoundationAuditDecision.EffectFailed,
                FoundationAuditReason.EffectFailed,
                idempotencyKey,
                operationVersion,
                supportPurpose,
                supportGrantExpiresAt,
                retryOfEvidenceId: record.Evidence.EvidenceId,
                attempt: 2,
                cancellationToken: cancellationToken);
            return FoundationAuditExecutionResult<T>.Failure(
                failureRecord.Succeeded ? "protected_effect_failed" : "protected_effect_failed_evidence_unavailable");
        }
    }

    private void EmitTelemetry(FoundationAuditEvidence evidence)
    {
        foreach (var kind in Enum.GetValues<FoundationAuditTelemetryKind>())
        {
            try
            {
                telemetrySink.Emit(FoundationAuditRedactor.ToTelemetry(evidence, kind));
            }
            catch
            {
                EmitFailureSignal(evidence.OperationId, evidence.CorrelationId);
            }
        }
    }

    private void EmitFailureSignal(string operationId, string correlationId)
    {
        var safeOperation = SafeSignalText(operationId, "unknown.operation");
        var safeCorrelation = SafeSignalText(correlationId, Guid.NewGuid().ToString("N"));
        try
        {
            signalSink.Emit(new FoundationAuditOperationalSignal(
                Guid.NewGuid(),
                timeProvider.GetUtcNow(),
                "audit_evidence_failure",
                safeOperation,
                safeCorrelation,
                FoundationAuditDecision.EvidenceFailure));
        }
        catch
        {
            // The operational signal is deliberately best effort and bounded.
        }
    }

    private static string SafeSignalText(string value, string fallback)
    {
        try
        {
            return FoundationAuditText.Required(value, nameof(value));
        }
        catch
        {
            return fallback;
        }
    }
}
