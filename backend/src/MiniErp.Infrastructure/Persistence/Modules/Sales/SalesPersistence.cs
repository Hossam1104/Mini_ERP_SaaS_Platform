#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

public sealed class SalesPersistence(DbContextOptions options) : ISalesPersistence, ISalesReportingReadPort, ISalesFulfillmentReportingReadPort
{
    private readonly DbContextOptions options = options;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private SalesDbContext Create(ProcurementRequestContext context) => new(options, context.TenantContext);

    public async Task<IReadOnlyList<SalesQuotationSummaryResponse>> ListQuotationsAsync(ProcurementRequestContext context, Guid? companyId, SalesQuotationStatus? status, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var query = ApplyTrustedScope(db.Quotations.AsNoTracking(), context.TenantContext.Scope);
        if (companyId is { } company) query = query.Where(item => item.CompanyId == company);
        if (status is { } state) query = query.Where(item => item.Status == state);
        return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.UpdatedAt).Take(500).Select(ToSummary).ToArray();
    }

    public async Task<SalesQuotationResponse?> GetQuotationAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var entity = await ApplyTrustedScope(db.Quotations.AsNoTracking(), context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : ToResponse(entity);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> CreateQuotationAsync(ProcurementRequestContext context, SalesQuotationWriteModel model, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesQuotationResponse>(db, context, "sales.quotation.create", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var now = DateTimeOffset.UtcNow;
        var number = $"SQ-{now:yyyy}-{model.Id:N}"[..17];
        var entity = new SalesQuotationEntity(context.TenantId, model, number, LinesJson(model.Lines), JsonSerializer.Serialize(policy, Json), now);
        entity.SetCreator(context.ActorId);
        db.Quotations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddRevision(db, context, entity, response, null, now);
        AddHistory(db, context, "quotation", entity.Id, SalesHistoryAction.Created, null, entity.Status, null, policy, null, null, now);
        AddAudit(db, context, "sales.quotation.create", "quotation", entity.Id, "Allowed", null, null, Summary(response), idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.quotation.create", idempotencyKey, requestFingerprint, "quotation", entity.Id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesQuotationResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> EditQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default, SalesApprovalPolicyDefinition? policy = null)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesQuotationResponse>(db, context, "sales.quotation.edit", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Quotations, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesQuotationResponse>("quotation_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesQuotationResponse>("concurrency_conflict");
        if (entity.Status is not (SalesQuotationStatus.Draft or SalesQuotationStatus.ReturnedForChange)) return Failure<SalesQuotationResponse>("quotation_edit_locked");
        if (entity.CompanyId != model.CompanyId || entity.BranchId != model.BranchId) return Failure<SalesQuotationResponse>("quotation_scope_immutable");
        var before = ToResponse(entity);
        var now = DateTimeOffset.UtcNow;
        entity.Edit(model, LinesJson(model.Lines), now, JsonSerializer.Serialize(policy, Json));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddRevision(db, context, entity, response, "quotation-edited", now);
        AddHistory(db, context, "quotation", id, SalesHistoryAction.Edited, before.Status, entity.Status, "quotation-edited", policy, null, Hash(response), now, JsonSerializer.Serialize(before, Json));
        AddAudit(db, context, "sales.quotation.edit", "quotation", id, "Allowed", null, Summary(before), Summary(response), idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.quotation.edit", idempotencyKey, requestFingerprint, "quotation", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesQuotationResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> EditOrderAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesOrderResponse>(db, context, "sales.order.edit", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesOrderResponse>("order_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesOrderResponse>("concurrency_conflict");
        if (entity.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.ReturnedForChange)) return Failure<SalesOrderResponse>("order_edit_locked");
        var before = ToResponse(entity);
        var now = DateTimeOffset.UtcNow;
        entity.Edit(model, LinesJson(model.Lines), now);
        if (policy is not null) entity.Transition(entity.Status, null, now, null, JsonSerializer.Serialize(policy, Json), "[]");
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddHistory(db, context, "order", id, SalesHistoryAction.Edited, before.Status, entity.Status, "order-edited", policy, null, Hash(before), now, JsonSerializer.Serialize(before, Json));
        AddAudit(db, context, "sales.order.edit", "order", id, "Allowed", null, Summary(before), Summary(response), idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.order.edit", idempotencyKey, requestFingerprint, "order", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesOrderResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> TransitionQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesQuotationResponse>(db, context, $"sales.quotation.{Action(target)}", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Quotations, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesQuotationResponse>("quotation_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesQuotationResponse>("concurrency_conflict");
        if (!CanTransition(entity.Status, target)) return Failure<SalesQuotationResponse>("quotation_transition_invalid");
        var before = entity.Status;
        var now = DateTimeOffset.UtcNow;
        var storedPolicy = DeserializePolicy(entity.ApprovalPolicyJson);
        var effectivePolicy = target == SalesQuotationStatus.PendingApproval ? policy : storedPolicy;
        if (entity.Status == SalesQuotationStatus.PendingApproval && effectivePolicy is null)
            return Failure<SalesQuotationResponse>("approval_policy_missing");
        var actualTarget = target;
        SalesApprovalStateResponse? approvalState = DeserializeApprovalState(entity.CurrentApprovalsJson);
        if (target == SalesQuotationStatus.Cancelled && entity.Status == SalesQuotationStatus.PendingApproval
            && (effectivePolicy is null || !effectivePolicy.AllowRequesterCancellationWhilePending || entity.CreatedByActorId != context.ActorId))
            return Failure<SalesQuotationResponse>("cancellation_not_allowed");
        if (target == SalesQuotationStatus.PendingApproval)
        {
            if (effectivePolicy is null) return Failure<SalesQuotationResponse>("approval_policy_missing");
            approvalState = NewApprovalState(effectivePolicy);
        }
        else if (target == SalesQuotationStatus.Approved)
        {
            var approval = EvaluateApproval(approvalState, effectivePolicy, entity.CreatedByActorId, context.ActorId, delegatedFromActorId, entity.RevisionNumber, entity.Version);
            if (!approval.Succeeded) return Failure<SalesQuotationResponse>(approval.Code);
            approvalState = approval.State;
            actualTarget = approval.IsFullyApproved ? SalesQuotationStatus.Approved : SalesQuotationStatus.PendingApproval;
        }
        entity.Transition(actualTarget, now, effectivePolicy is null ? entity.ApprovalPolicyJson : JsonSerializer.Serialize(effectivePolicy, Json), SerializeApprovalState(approvalState));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddHistory(db, context, "quotation", id, ActionToHistory(actualTarget), before, actualTarget, reason, effectivePolicy, null, Hash(response), now, JsonSerializer.Serialize(response, Json));
        AddAudit(db, context, $"sales.quotation.{Action(target)}", "quotation", id, "Allowed", reason, $"status={before}", $"status={actualTarget};revision={entity.RevisionNumber}", idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, $"sales.quotation.{Action(target)}", idempotencyKey, requestFingerprint, "quotation", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesQuotationResponse>.Success(response);
    }

    public async Task<IReadOnlyList<SalesQuotationRevisionResponse>> ListQuotationRevisionsAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var rows = await db.QuotationRevisions.AsNoTracking().Where(item => item.QuotationId == id).OrderByDescending(item => item.RevisionNumber).ToListAsync(cancellationToken);
        return rows.Select(item => new SalesQuotationRevisionResponse(item.Id, item.QuotationId, item.RevisionNumber, item.Status, item.SnapshotHash, item.ActorId, item.OccurredAt, item.Reason, JsonSerializer.Deserialize<SalesQuotationResponse>(item.SnapshotJson, Json)!)).ToArray();
    }

    public async Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.History.AsNoTracking().Where(item => item.DocumentType == documentType && item.DocumentId == id).ToListAsync(cancellationToken)).OrderByDescending(item => item.OccurredAt).Select(item => new SalesHistoryResponse(item.Id, item.DocumentType, item.DocumentId, item.Action.ToString(), item.FromStatus, item.ToStatus, item.ActorId, item.OccurredAt, item.Reason, item.PolicyId, item.PolicyVersion, item.CreditOutcome, item.SnapshotHash, item.SnapshotJson)).ToArray();
    }

    public async Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.Audit.AsNoTracking().Where(item => item.DocumentType == documentType && item.DocumentId == id).ToListAsync(cancellationToken)).OrderByDescending(item => item.OccurredAt).Select(item => new SalesAuditResponse(item.Id, item.OperationId, item.DocumentType, item.DocumentId, item.ActorId, item.OccurredAt, item.Decision, item.Reason, item.BeforeSummary, item.AfterSummary, item.IdempotencyKey, item.CorrelationId)).ToArray();
    }

    public async Task<SalesApprovalPolicyDefinition?> GetApprovalPolicyAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        if (string.Equals(documentType, "quotation", StringComparison.Ordinal))
        {
            var quotation = await ApplyTrustedScope(db.Quotations.AsNoTracking(), context.TenantContext.Scope)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            return quotation is null ? null : DeserializePolicy(quotation.ApprovalPolicyJson);
        }

        if (string.Equals(documentType, "order", StringComparison.Ordinal))
        {
            var order = await ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            return order is null ? null : DeserializePolicy(order.ApprovalPolicyJson);
        }

        return null;
    }

    public async Task<IReadOnlyList<SalesOrderSummaryResponse>> ListOrdersAsync(ProcurementRequestContext context, Guid? companyId, SalesOrderStatus? status, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var query = ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope);
        if (companyId is { } company) query = query.Where(item => item.CompanyId == company);
        if (status is { } state) query = query.Where(item => item.Status == state);
        return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.UpdatedAt).Take(500).Select(ToSummary).ToArray();
    }

    public async Task<ReportingSourcePage<SalesOrderSummaryResponse>> ListOrdersReportingPageAsync(
        ProcurementRequestContext context,
        Guid? companyId,
        SalesOrderStatus? status,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var query = ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope);
        if (companyId is { } selectedCompany) query = query.Where(item => item.CompanyId == selectedCompany);
        if (status is { } selectedStatus) query = query.Where(item => item.Status == selectedStatus);
        var total = await query.CountAsync(cancellationToken);
        var asOf = total == 0 ? null : await query.Select(item => (DateTimeOffset?)item.UpdatedAt).MaxAsync(cancellationToken);
        var ordered = page.SortBy?.ToLowerInvariant() switch
        {
            "number" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.Number).ThenByDescending(item => item.Id) : query.OrderBy(item => item.Number).ThenBy(item => item.Id),
            "customer" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.CustomerName).ThenByDescending(item => item.Id) : query.OrderBy(item => item.CustomerName).ThenBy(item => item.Id),
            "status" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.Status).ThenByDescending(item => item.Id) : query.OrderBy(item => item.Status).ThenBy(item => item.Id),
            "total" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.Total).ThenByDescending(item => item.Id) : query.OrderBy(item => item.Total).ThenBy(item => item.Id),
            _ => page.SortDirection == "asc" ? query.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Id) : query.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
        };
        var values = await ordered.Skip(page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        return new ReportingSourcePage<SalesOrderSummaryResponse>(values.Select(ToSummary).ToArray(), total, "updatedAt,id", asOf);
    }

    public async Task<ReportingSourcePage<SalesFulfillmentReportingRecord>> ListFulfillmentReportingPageAsync(
        ProcurementRequestContext context,
        SalesDeliveryStatus? status,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var deliveries = ApplyTrustedScope(db.Deliveries.AsNoTracking(), context.TenantContext.Scope);
        if (status is { } selectedStatus) deliveries = deliveries.Where(item => item.Status == selectedStatus);

        var total = await deliveries.CountAsync(cancellationToken);
        DateTimeOffset? dataAsOf = null;
        if (total > 0)
        {
            dataAsOf = db.Database.IsSqlite()
                ? (await deliveries.Select(item => item.PostedAt ?? item.CreatedAt).ToListAsync(cancellationToken)).Max()
                : await deliveries.Select(item => (DateTimeOffset?)(item.PostedAt ?? item.CreatedAt)).MaxAsync(cancellationToken);
        }
        var ordered = page.SortBy?.ToLowerInvariant() switch
        {
            "status" => page.SortDirection == "desc" ? deliveries.OrderByDescending(item => item.Status).ThenByDescending(item => item.Id) : deliveries.OrderBy(item => item.Status).ThenBy(item => item.Id),
            "warehouse" => page.SortDirection == "desc" ? deliveries.OrderByDescending(item => item.WarehouseId).ThenByDescending(item => item.Id) : deliveries.OrderBy(item => item.WarehouseId).ThenBy(item => item.Id),
            "postedat" => page.SortDirection == "asc" ? deliveries.OrderBy(item => item.PostedAt).ThenBy(item => item.Id) : deliveries.OrderByDescending(item => item.PostedAt).ThenByDescending(item => item.Id),
            _ => page.SortDirection == "asc" ? deliveries.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id) : deliveries.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
        };
        var values = await ordered.Skip(page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        return new ReportingSourcePage<SalesFulfillmentReportingRecord>(values.Select(ToFulfillmentReporting).ToArray(), total, "createdAt,id", dataAsOf);
    }

    public async Task<SalesOrderResponse?> GetOrderAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var entity = await ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : ToResponse(entity);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> ConvertQuotationAsync(ProcurementRequestContext context, Guid quotationId, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesOrderResponse>(db, context, "sales.quotation.convert", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var quote = await ApplyTrustedScope(db.Quotations, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == quotationId, cancellationToken);
        if (quote is null) return Failure<SalesOrderResponse>("quotation_not_found");
        if (!quote.Version.SequenceEqual(expectedVersion)) return Failure<SalesOrderResponse>("concurrency_conflict");
        if (quote.Status is not (SalesQuotationStatus.Approved or SalesQuotationStatus.Sent)) return Failure<SalesOrderResponse>("quotation_not_convertible");
        if (DateOnly.FromDateTime(DateTime.UtcNow) > quote.ValidUntil) return Failure<SalesOrderResponse>("quotation_expired");
        if (await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).AnyAsync(item => item.SourceQuotationId == quote.Id && item.SourceQuotationRevision == quote.RevisionNumber, cancellationToken)) return Failure<SalesOrderResponse>("quotation_revision_already_converted");
        var now = DateTimeOffset.UtcNow;
        var quoteBefore = quote.Status;
        var order = new SalesOrderEntity(context.TenantId, quote, context.ActorId, $"SO-{now:yyyy}-{Guid.NewGuid():N}"[..17], quote.LinesJson, JsonSerializer.Serialize(policy, Json), now);
        db.Orders.Add(order);
        quote.Transition(SalesQuotationStatus.Converted, now, quote.ApprovalPolicyJson);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(order);
        AddHistory(db, context, "order", order.Id, SalesHistoryAction.Created, null, order.Status, "converted-from-quotation", policy, null, Hash(response), now);
        AddHistory(db, context, "quotation", quote.Id, SalesHistoryAction.Converted, quoteBefore, quote.Status, "converted-to-order", policy, null, Hash(response), now);
        AddAudit(db, context, "sales.quotation.convert", "order", order.Id, "Allowed", null, $"source-quotation={quote.Number};revision={quote.RevisionNumber}", Summary(response), idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.quotation.convert", idempotencyKey, requestFingerprint, "order", order.Id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesOrderResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> TransitionOrderAsync(ProcurementRequestContext context, Guid id, SalesOrderStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation? credit, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesOrderResponse>(db, context, $"sales.order.{Action(target)}", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesOrderResponse>("order_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesOrderResponse>("concurrency_conflict");
        if (!CanTransition(entity.Status, target)) return Failure<SalesOrderResponse>("order_transition_invalid");
        var before = entity.Status;
        var now = DateTimeOffset.UtcNow;
        var storedPolicy = DeserializePolicy(entity.ApprovalPolicyJson);
        var effectivePolicy = target == SalesOrderStatus.PendingApproval ? policy : storedPolicy;
        if (entity.Status == SalesOrderStatus.PendingApproval && effectivePolicy is null)
            return Failure<SalesOrderResponse>("approval_policy_missing");
        var actualTarget = target;
        SalesApprovalStateResponse? approvalState = DeserializeApprovalState(entity.CurrentApprovalsJson);
        if (target == SalesOrderStatus.Cancelled && entity.Status == SalesOrderStatus.PendingApproval
            && (effectivePolicy is null || !effectivePolicy.AllowRequesterCancellationWhilePending || entity.CreatedByActorId != context.ActorId))
            return Failure<SalesOrderResponse>("cancellation_not_allowed");
        if (target == SalesOrderStatus.PendingApproval)
        {
            if (effectivePolicy is null) return Failure<SalesOrderResponse>("approval_policy_missing");
            approvalState = NewApprovalState(effectivePolicy);
        }
        else if (target == SalesOrderStatus.Approved)
        {
            var approval = EvaluateApproval(approvalState, effectivePolicy, entity.CreatedByActorId, context.ActorId, delegatedFromActorId, entity.RevisionNumber, entity.Version);
            if (!approval.Succeeded) return Failure<SalesOrderResponse>(approval.Code);
            approvalState = approval.State;
            actualTarget = approval.IsFullyApproved ? SalesOrderStatus.Approved : SalesOrderStatus.PendingApproval;
        }
        entity.Transition(actualTarget, reason, now, credit, effectivePolicy is null ? entity.ApprovalPolicyJson : JsonSerializer.Serialize(effectivePolicy, Json), SerializeApprovalState(approvalState));
        if (credit is not null) db.Credit.Add(ToCredit(entity, credit));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        string historyHash;
        string? historySnapshot;
        if (credit is null)
        {
            historyHash = Hash(response);
            historySnapshot = JsonSerializer.Serialize(response, Json);
        }
        else
        {
            var snapshot = new { Order = response, Credit = credit };
            historyHash = Hash(snapshot);
            historySnapshot = JsonSerializer.Serialize(snapshot, Json);
        }
        AddHistory(db, context, "order", id, credit is null ? ActionToHistory(actualTarget) : SalesHistoryAction.CreditEvaluated, before, actualTarget, reason, effectivePolicy, credit?.Outcome.ToString(), historyHash, now, historySnapshot);
        AddAudit(db, context, $"sales.order.{Action(target)}", "order", id, "Allowed", reason, $"status={before}", $"status={actualTarget};credit={credit?.Outcome}", idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, $"sales.order.{Action(target)}", idempotencyKey, requestFingerprint, "order", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesOrderResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> OverrideOrderCreditAsync(ProcurementRequestContext context, Guid id, string reason, DateTimeOffset expiresAt, string? scope, string? sourceReference, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation credit, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesOrderResponse>(db, context, "sales.order.credit.override", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesOrderResponse>("order_not_found");
        if (entity.Status != SalesOrderStatus.CreditHold) return Failure<SalesOrderResponse>("credit_override_not_allowed");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesOrderResponse>("concurrency_conflict");
        if (expiresAt <= DateTimeOffset.UtcNow) return Failure<SalesOrderResponse>("credit_override_expired");
        var now = DateTimeOffset.UtcNow;
        var persistedCredit = credit with { OverrideExpiresAt = expiresAt };
        entity.OverrideCredit(persistedCredit, now);
        db.Credit.Add(ToCredit(entity, persistedCredit));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddHistory(db, context, "order", id, SalesHistoryAction.CreditOverridden, SalesOrderStatus.CreditHold, entity.Status, reason, null, SalesCreditOutcome.Overridden.ToString(), Hash(new { Order = response, Credit = persistedCredit }), now, JsonSerializer.Serialize(new { Order = response, Credit = persistedCredit }, Json));
        AddAudit(db, context, "sales.order.credit.override", "order", id, "Allowed", reason, "credit=hold", $"credit=overridden;expires={expiresAt:O};scope={scope};source={sourceReference}", idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.order.credit.override", idempotencyKey, requestFingerprint, "order", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesOrderResponse>.Success(response);
    }

    public async Task<SalesCreditResponse?> GetOrderCreditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var order = await ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return null;
        var row = (await db.Credit.AsNoTracking().Where(item => item.DocumentId == id).ToListAsync(cancellationToken)).OrderByDescending(item => item.EvaluatedAt).FirstOrDefault();
        return row is null ? null : ToCreditResponse(row, order);
    }

    public async Task<IReadOnlyList<SalesDeliveryResponse>> ListDeliveriesAsync(ProcurementRequestContext context, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var rows = await db.Deliveries.AsNoTracking().Where(item => item.OrderId == orderId).OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        return rows.Where(item => InScope(context, item.CompanyId, item.BranchId)).Select(ToDelivery).ToArray();
    }

    public async Task<SalesDeliveryResponse?> GetDeliveryAsync(ProcurementRequestContext context, Guid deliveryId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var row = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deliveryId, cancellationToken);
        return row is null || !InScope(context, row.CompanyId, row.BranchId) ? null : ToDelivery(row);
    }

    public async Task<SalesOperationResult<SalesDeliveryResponse>> CreateDeliveryAsync(ProcurementRequestContext context, SalesDeliveryWriteModel model, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesDeliveryResponse>(db, context, "sales.delivery.create", idempotencyKey, requestFingerprint, cancellationToken); if (replay is not null) return replay;
        var order = await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == model.OrderId, cancellationToken);
        if (order is null || order.CompanyId != model.CompanyId || order.BranchId != model.BranchId || order.CustomerId != model.CustomerId || order.RevisionNumber != model.OrderRevisionNumber) return Failure<SalesDeliveryResponse>("order_not_found");
        var now = DateTimeOffset.UtcNow;
        var entity = new SalesDeliveryEntity(context.TenantId, model.Id, model.OrderId, model.OrderRevisionNumber, model.CompanyId, model.BranchId, model.CustomerId, model.WarehouseId, JsonSerializer.Serialize(model.Lines, Json), model.SourceSnapshot, model.ActorId, idempotencyKey, now, requestFingerprint);
        db.Deliveries.Add(entity); AddHistory(db, context, "delivery", entity.Id, SalesHistoryAction.Created, null, entity.Status, "delivery-created", null, null, Hash(model.SourceSnapshot), now, model.SourceSnapshot); AddAudit(db, context, "sales.delivery.create", "delivery", entity.Id, "Allowed", null, null, $"status={entity.Status};order={entity.OrderId}", idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken); var response = ToDelivery(entity); await SaveIdempotencyAsync(db, context, "sales.delivery.create", idempotencyKey, requestFingerprint, "delivery", entity.Id, response, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return SalesOperationResult<SalesDeliveryResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesDeliveryResponse>> CompleteDeliveryAsync(ProcurementRequestContext context, Guid deliveryId, IReadOnlyList<Guid> movementIds, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default, string? downstreamIdempotencyKey = null, string? downstreamRequestFingerprint = null)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesDeliveryResponse>(db, context, "sales.delivery.complete", idempotencyKey, requestFingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.Deliveries.SingleOrDefaultAsync(item => item.Id == deliveryId, cancellationToken); if (entity is null || !InScope(context, entity.CompanyId, entity.BranchId)) return Failure<SalesDeliveryResponse>("delivery_not_found"); if (movementIds is null || movementIds.Count == 0 || movementIds.Distinct().Count() != movementIds.Count) return Failure<SalesDeliveryResponse>("delivery_effect_invalid"); var recordedMovements = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(entity.MovementIdsJson, Json) ?? []; var handoff = DeserializeHandoff(entity.HandoffJson); if (handoff is { DownstreamEffectIds.Count: > 0 } && (!recordedMovements.SequenceEqual(handoff.DownstreamEffectIds) || !handoff.DownstreamEffectIds.SequenceEqual(movementIds.Distinct()))) return Failure<SalesDeliveryResponse>("delivery_effect_mismatch"); if (handoff?.DownstreamRequestFingerprint is not null && !string.Equals(handoff.DownstreamRequestFingerprint, downstreamRequestFingerprint, StringComparison.Ordinal)) return Failure<SalesDeliveryResponse>("delivery_fingerprint_mismatch"); if (handoff?.DownstreamIdempotencyKey is not null && !string.Equals(handoff.DownstreamIdempotencyKey, downstreamIdempotencyKey, StringComparison.Ordinal)) return Failure<SalesDeliveryResponse>("delivery_idempotency_mismatch"); if (entity.Status == SalesDeliveryStatus.Posted) return recordedMovements.SequenceEqual(movementIds.Distinct()) ? SalesOperationResult<SalesDeliveryResponse>.Success(ToDelivery(entity)) : Failure<SalesDeliveryResponse>("delivery_effect_mismatch"); if (entity.Status is not (SalesDeliveryStatus.Draft or SalesDeliveryStatus.Unknown)) return Failure<SalesDeliveryResponse>("delivery_not_postable");
        var before = entity.Status; var now = DateTimeOffset.UtcNow; entity.Posted(movementIds, now, downstreamIdempotencyKey, downstreamRequestFingerprint); AddHistory(db, context, "delivery", entity.Id, SalesHistoryAction.Confirmed, before, entity.Status, "inventory-posted", null, null, Hash(movementIds), now); AddAudit(db, context, "sales.delivery.complete", "delivery", entity.Id, "Allowed", null, $"status={before}", $"status={entity.Status};movements={movementIds.Count}", idempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var response = ToDelivery(entity); await SaveIdempotencyAsync(db, context, "sales.delivery.complete", idempotencyKey, requestFingerprint, "delivery", entity.Id, response, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return SalesOperationResult<SalesDeliveryResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesDeliveryResponse>> FailDeliveryAsync(ProcurementRequestContext context, Guid deliveryId, string code, bool unknown, CancellationToken cancellationToken = default, SalesDownstreamEvidence? downstream = null)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var entity = await db.Deliveries.SingleOrDefaultAsync(item => item.Id == deliveryId, cancellationToken); if (entity is null || !InScope(context, entity.CompanyId, entity.BranchId)) return Failure<SalesDeliveryResponse>("delivery_not_found"); if (entity.Status == SalesDeliveryStatus.Posted) return SalesOperationResult<SalesDeliveryResponse>.Success(ToDelivery(entity)); var before = entity.Status; entity.Fail(code, unknown, downstream); var now = DateTimeOffset.UtcNow; AddHistory(db, context, "delivery", entity.Id, SalesHistoryAction.Edited, before, entity.Status, code, null, null, null, now); AddAudit(db, context, "sales.delivery.fail", "delivery", entity.Id, "Failed", code, before.ToString(), entity.Status.ToString(), null, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return SalesOperationResult<SalesDeliveryResponse>.Success(ToDelivery(entity));
    }

    public async Task<IReadOnlyList<SalesInvoiceRequestResponse>> ListInvoiceRequestsAsync(ProcurementRequestContext context, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); var rows = (await db.InvoiceRequests.AsNoTracking().Where(item => item.OrderId == orderId).ToListAsync(cancellationToken)).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id); return rows.Where(item => InScope(context, item.CompanyId, item.BranchId)).Select(ToInvoice).ToArray();
    }

    public async Task<SalesInvoiceRequestResponse?> GetInvoiceRequestAsync(ProcurementRequestContext context, Guid requestId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); var row = await db.InvoiceRequests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken); return row is null || !InScope(context, row.CompanyId, row.BranchId) ? null : ToInvoice(row);
    }

    public async Task<SalesOperationResult<SalesInvoiceRequestResponse>> CreateInvoiceRequestAsync(ProcurementRequestContext context, SalesInvoiceRequestWriteModel model, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesInvoiceRequestResponse>(db, context, "sales.invoice-request.create", idempotencyKey, requestFingerprint, cancellationToken); if (replay is not null) return replay;
        var order = await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == model.OrderId, cancellationToken);
        if (order is null || order.RevisionNumber != model.OrderRevisionNumber || order.CompanyId != model.CompanyId || order.BranchId != model.BranchId || order.CustomerId != model.CustomerId) return Failure<SalesInvoiceRequestResponse>("order_not_found");
        var orderLines = Lines(order.LinesJson);
        var paymentTerm = DeserializePaymentTerm(order.PaymentTermJson);
        if (paymentTerm is null || model.InvoiceDate == default || string.IsNullOrWhiteSpace(order.CurrencyCode)) return Failure<SalesInvoiceRequestResponse>("invoice_source_evidence_missing");
        var postedDeliveries = (await db.Deliveries.Where(item => item.OrderId == model.OrderId && item.OrderRevisionNumber == model.OrderRevisionNumber && item.Status == SalesDeliveryStatus.Posted && item.CompanyId == order.CompanyId && item.BranchId == order.BranchId && item.CustomerId == order.CustomerId).ToListAsync(cancellationToken)).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToArray();
        if (model.DeliveryId is { } deliveryId && !postedDeliveries.Any(item => item.Id == deliveryId)) return Failure<SalesInvoiceRequestResponse>("delivery_not_posted");
        var sourceDeliveries = (model.DeliveryId is { } selectedDeliveryId ? postedDeliveries.Where(item => item.Id == selectedDeliveryId) : postedDeliveries).ToArray();
        var existingInvoices = await db.InvoiceRequests.Where(item => item.OrderId == model.OrderId && item.OrderRevisionNumber == model.OrderRevisionNumber && item.Status != SalesInvoiceRequestStatus.Failed).ToListAsync(cancellationToken);
        if (existingInvoices.Any(item => item.Status == SalesInvoiceRequestStatus.Unknown)) return Failure<SalesInvoiceRequestResponse>("invoice_source_unknown");
        var persistedInvoices = existingInvoices.Select(item => ReadInvoiceLines(item.LinesJson)).ToArray();
        if (persistedInvoices.Any(item => item.Lines.Count > 0 && item.Evidence.Count == 0)) return Failure<SalesInvoiceRequestResponse>("invoice_source_evidence_missing");
        var priorAllocations = persistedInvoices.SelectMany(item => item.Evidence.SelectMany(evidence => evidence.Allocations)).ToArray();
        var priorLines = persistedInvoices.SelectMany(item => item.Lines).ToArray();
        if (model.Lines is null || model.Lines.Count == 0) return Failure<SalesInvoiceRequestResponse>("invalid_invoice_lines");
        var requestedByLine = model.Lines.GroupBy(item => item.OrderLineId).ToDictionary(item => item.Key, item => item.Sum(value => value.Quantity));
        if (requestedByLine.Count != model.Lines.Count) return Failure<SalesInvoiceRequestResponse>("invalid_invoice_lines");
        var allocatedEvidence = new List<SalesInvoiceLineEvidence>();
        foreach (var requested in requestedByLine)
        {
            var orderLine = orderLines.SingleOrDefault(item => item.Id == requested.Key);
            if (orderLine is null || requested.Value <= 0m || requested.Value > orderLine.Quantity) return Failure<SalesInvoiceRequestResponse>("invoice_quantity_conflict");
            var allocations = new List<SalesInvoiceSourceAllocation>();
            var remaining = requested.Value;
            foreach (var delivery in sourceDeliveries)
            {
                var sourceQuantity = (JsonSerializer.Deserialize<IReadOnlyList<SalesDeliveryRequestLine>>(delivery.LinesJson, Json) ?? []).Where(item => item.OrderLineId == requested.Key).Sum(item => item.Quantity);
                if (sourceQuantity <= 0m) continue;
                var prior = priorAllocations.Where(item => item.DeliveryId == delivery.Id && item.OrderLineId == requested.Key && item.OrderRevisionNumber == model.OrderRevisionNumber).Sum(item => item.ConsumedQuantity);
                var available = Math.Max(0m, sourceQuantity - prior);
                var consumed = Math.Min(remaining, available);
                if (consumed > 0m) allocations.Add(new(delivery.Id, requested.Key, model.OrderRevisionNumber, sourceQuantity, prior, consumed, Math.Max(0m, available - consumed)));
                remaining -= consumed;
                if (remaining == 0m) break;
            }
            if (remaining > 0m) return Failure<SalesInvoiceRequestResponse>("invoice_quantity_conflict");
            var priorQuantity = priorLines.Where(item => item.OrderLineId == requested.Key).Sum(item => item.Quantity);
            var priorEvidence = persistedInvoices.SelectMany(item => item.Evidence).Where(item => item.OrderLineId == requested.Key).ToArray();
            var priorNet = priorEvidence.Sum(item => item.NetAmount);
            var priorTax = priorEvidence.Sum(item => item.TaxAmount);
            var priorTaxableBase = priorEvidence.Sum(item => item.TaxEvidence?.TaxableBase ?? 0m);
            var isResidual = priorQuantity + requested.Value >= orderLine.Quantity;
            var net = isResidual ? decimal.Round(orderLine.LineTotal - priorNet, 2, MidpointRounding.AwayFromZero) : decimal.Round(orderLine.LineTotal * requested.Value / orderLine.Quantity, 2, MidpointRounding.AwayFromZero);
            var tax = isResidual ? decimal.Round(orderLine.TaxAmount - priorTax, 2, MidpointRounding.AwayFromZero) : decimal.Round(orderLine.TaxAmount * requested.Value / orderLine.Quantity, 2, MidpointRounding.AwayFromZero);
            if (net < 0m || tax < 0m) return Failure<SalesInvoiceRequestResponse>("invoice_amount_conflict");
            SalesInvoiceTaxEvidence? taxEvidence = null;
            if (orderLine.TaxAmount > 0m)
            {
                if (orderLine.TaxId is not { } taxId || string.IsNullOrWhiteSpace(orderLine.TaxCode) || orderLine.TaxRateVersionId is not { } rateVersionId || orderLine.TaxRateVersionNumber is not { } rateVersionNumber || orderLine.TaxEffectiveFrom is not { } effectiveFrom || orderLine.TaxRatePercentage is not { } rate || orderLine.TaxableBase is not { } taxableBase || string.IsNullOrWhiteSpace(orderLine.TaxReferenceValue) || rate < 0m || taxableBase < 0m) return Failure<SalesInvoiceRequestResponse>("tax_evidence_missing");
                var currentTaxableBase = isResidual ? decimal.Round(taxableBase - priorTaxableBase, 2, MidpointRounding.AwayFromZero) : decimal.Round(taxableBase * requested.Value / orderLine.Quantity, 2, MidpointRounding.AwayFromZero);
                if (currentTaxableBase < 0m) return Failure<SalesInvoiceRequestResponse>("invoice_amount_conflict");
                taxEvidence = new(taxId, orderLine.TaxCode, rateVersionId, rateVersionNumber, effectiveFrom, effectiveFrom, orderLine.TaxEffectiveTo, rate, currentTaxableBase, tax, orderLine.TaxReferenceValue);
            }
            allocatedEvidence.Add(new(requested.Key, orderLine.Quantity, priorQuantity + requested.Value, requested.Value, net, tax, decimal.Round(net + tax, 2, MidpointRounding.AwayFromZero), taxEvidence, allocations));
        }
        var linesJson = JsonSerializer.Serialize(new PersistedInvoiceLines(model.Lines, allocatedEvidence), Json);
        var netAmount = decimal.Round(allocatedEvidence.Sum(item => item.NetAmount), 2, MidpointRounding.AwayFromZero);
        var taxAmount = decimal.Round(allocatedEvidence.Sum(item => item.TaxAmount), 2, MidpointRounding.AwayFromZero);
        var amount = decimal.Round(allocatedEvidence.Sum(item => item.GrossAmount), 2, MidpointRounding.AwayFromZero);
        var sourceSnapshot = JsonSerializer.Serialize(new { Order = ToResponse(order), Request = new { model.InvoiceDate, model.DeliveryId, model.Lines }, Evidence = allocatedEvidence, PaymentTerm = paymentTerm }, Json);
        var now = DateTimeOffset.UtcNow; var entity = new SalesInvoiceRequestEntity(context.TenantId, model.Id, model.OrderId, model.OrderRevisionNumber, model.DeliveryId, model.CompanyId, model.BranchId, model.CustomerId, model.InvoiceDate, linesJson, amount, order.CurrencyCode, sourceSnapshot, model.ActorId, idempotencyKey, now, JsonSerializer.Serialize(paymentTerm, Json), requestFingerprint); db.InvoiceRequests.Add(entity); AddHistory(db, context, "invoice-request", entity.Id, SalesHistoryAction.Created, null, entity.Status, "invoice-request-created", null, null, Hash(sourceSnapshot), now, sourceSnapshot); AddAudit(db, context, "sales.invoice-request.create", "invoice-request", entity.Id, "Allowed", null, null, $"status={entity.Status};order={entity.OrderId};amount={amount}", idempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var response = ToInvoice(entity); await SaveIdempotencyAsync(db, context, "sales.invoice-request.create", idempotencyKey, requestFingerprint, "invoice-request", entity.Id, response, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return SalesOperationResult<SalesInvoiceRequestResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesInvoiceRequestResponse>> CompleteInvoiceRequestAsync(ProcurementRequestContext context, Guid requestId, Guid financeOpenItemId, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default, string? downstreamIdempotencyKey = null, string? downstreamRequestFingerprint = null)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReplayAsync<SalesInvoiceRequestResponse>(db, context, "sales.invoice-request.complete", idempotencyKey, requestFingerprint, cancellationToken); if (replay is not null) return replay; var entity = await db.InvoiceRequests.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken); if (entity is null || !InScope(context, entity.CompanyId, entity.BranchId)) return Failure<SalesInvoiceRequestResponse>("invoice_request_not_found"); if (financeOpenItemId == Guid.Empty) return Failure<SalesInvoiceRequestResponse>("invoice_effect_invalid"); var handoff = DeserializeHandoff(entity.HandoffJson); if (handoff is { DownstreamEffectIds.Count: > 0 } && (!handoff.DownstreamEffectIds.SequenceEqual([financeOpenItemId]) || !string.Equals(handoff.DownstreamRequestFingerprint, downstreamRequestFingerprint, StringComparison.Ordinal) || !string.Equals(handoff.DownstreamIdempotencyKey, downstreamIdempotencyKey, StringComparison.Ordinal))) return Failure<SalesInvoiceRequestResponse>("invoice_effect_mismatch"); if (entity.Status == SalesInvoiceRequestStatus.Posted) return entity.FinanceOpenItemId == financeOpenItemId ? SalesOperationResult<SalesInvoiceRequestResponse>.Success(ToInvoice(entity)) : Failure<SalesInvoiceRequestResponse>("invoice_effect_mismatch"); if (entity.Status is not (SalesInvoiceRequestStatus.Pending or SalesInvoiceRequestStatus.Unknown)) return Failure<SalesInvoiceRequestResponse>("invoice_request_not_postable"); var before = entity.Status; var now = DateTimeOffset.UtcNow; entity.Posted(financeOpenItemId, now, downstreamIdempotencyKey, downstreamRequestFingerprint); AddHistory(db, context, "invoice-request", entity.Id, SalesHistoryAction.Confirmed, before, entity.Status, "finance-posted", null, null, null, now); AddAudit(db, context, "sales.invoice-request.complete", "invoice-request", entity.Id, "Allowed", null, before.ToString(), $"status={entity.Status};finance={financeOpenItemId}", idempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var response = ToInvoice(entity); await SaveIdempotencyAsync(db, context, "sales.invoice-request.complete", idempotencyKey, requestFingerprint, "invoice-request", entity.Id, response, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return SalesOperationResult<SalesInvoiceRequestResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesInvoiceRequestResponse>> FailInvoiceRequestAsync(ProcurementRequestContext context, Guid requestId, string code, bool unknown, CancellationToken cancellationToken = default, SalesDownstreamEvidence? downstream = null)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var entity = await db.InvoiceRequests.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken); if (entity is null || !InScope(context, entity.CompanyId, entity.BranchId)) return Failure<SalesInvoiceRequestResponse>("invoice_request_not_found"); if (entity.Status == SalesInvoiceRequestStatus.Posted) return SalesOperationResult<SalesInvoiceRequestResponse>.Success(ToInvoice(entity)); var before = entity.Status; entity.Fail(code, unknown, downstream); var now = DateTimeOffset.UtcNow; AddHistory(db, context, "invoice-request", entity.Id, SalesHistoryAction.Edited, before, entity.Status, code, null, null, null, now); AddAudit(db, context, "sales.invoice-request.fail", "invoice-request", entity.Id, "Failed", code, before.ToString(), entity.Status.ToString(), null, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return SalesOperationResult<SalesInvoiceRequestResponse>.Success(ToInvoice(entity));
    }

    private static IQueryable<SalesQuotationEntity> ApplyTrustedScope(IQueryable<SalesQuotationEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference) return query;
        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return query.Where(_ => false);
        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static IQueryable<SalesOrderEntity> ApplyTrustedScope(IQueryable<SalesOrderEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference) return query;
        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return query.Where(_ => false);
        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static IQueryable<SalesDeliveryEntity> ApplyTrustedScope(IQueryable<SalesDeliveryEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference) return query;
        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return query.Where(_ => false);
        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Warehouse" => query.Where(item => item.WarehouseId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static bool InScope(ProcurementRequestContext context, Guid companyId, Guid? branchId)
    {
        if (context.TenantContext.Scope is not { } scope) return true;
        var parts = scope.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && Guid.TryParse(parts[1], out var id) && (parts[0] switch { "Tenant" => true, "Company" => companyId == id, "Branch" => branchId == id, _ => false });
    }

    private static async Task<SalesOperationResult<T>?> ReplayAsync<T>(SalesDbContext db, ProcurementRequestContext context, string operation, string key, string fingerprint, CancellationToken cancellationToken)
    {
        var row = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.OperationId == operation && item.Key == key, cancellationToken);
        if (row is null) return null;
        if (!string.Equals(row.Fingerprint, fingerprint, StringComparison.Ordinal)) return SalesOperationResult<T>.Failure("idempotency_conflict");
        var response = JsonSerializer.Deserialize<T>(row.ResponseJson, Json);
        return response is null ? SalesOperationResult<T>.Failure("idempotency_replay_unavailable") : SalesOperationResult<T>.Success(response);
    }

    private static async Task SaveIdempotencyAsync<T>(SalesDbContext db, ProcurementRequestContext context, string operation, string key, string fingerprint, string type, Guid documentId, T response, DateTimeOffset now, CancellationToken cancellationToken)
    {
        db.Idempotency.Add(new SalesIdempotencyEntity(context.TenantId, operation, key, fingerprint, type, documentId, JsonSerializer.Serialize(response, Json), now));
        await Task.CompletedTask;
    }

    private static void AddRevision(SalesDbContext db, ProcurementRequestContext context, SalesQuotationEntity entity, SalesQuotationResponse response, string? reason, DateTimeOffset now) => db.QuotationRevisions.Add(new SalesQuotationRevisionEntity(context.TenantId, entity.Id, entity.RevisionNumber, entity.Status, JsonSerializer.Serialize(response, Json), Hash(response), context.ActorId, reason, now));
    private static void AddHistory(SalesDbContext db, ProcurementRequestContext context, string type, Guid id, SalesHistoryAction action, Enum? from, Enum? to, string? reason, SalesApprovalPolicyDefinition? policy, string? credit, string? hash, DateTimeOffset now, string? snapshotJson = null) => db.History.Add(new SalesHistoryEntity(context.TenantId, type, id, action, from?.ToString(), to?.ToString(), context.ActorId, reason, policy?.PolicyId, policy?.Version, credit, hash, now, snapshotJson));
    private static void AddAudit(SalesDbContext db, ProcurementRequestContext context, string operation, string type, Guid id, string decision, string? reason, string? before, string? after, string? key, DateTimeOffset now) => db.Audit.Add(new SalesAuditEntity(context.TenantId, operation, type, id, context.ActorId, now, decision, reason, before, after, key, context.CorrelationId?.Value ?? "sales"));
    private static SalesCreditEntity ToCredit(SalesOrderEntity entity, SalesCreditEvaluation credit) => new(entity.TenantId, new SalesCreditResponse(entity.Id, entity.CustomerId, entity.CompanyId, credit.CurrencyCode, credit.OpenReceivables, credit.OverdueReceivables, credit.NetReceivableExposure, credit.ProposedExposure, credit.CreditLimit, credit.Outcome, credit.Reason, credit.AsOfDate, credit.EvaluatedAt, credit.OverrideExpiresAt, credit.TransactionCurrencyCode ?? entity.CurrencyCode, credit.TransactionAmount ?? entity.Total, credit.ConvertedOrderCommitment, credit.ExchangeRateEvidence, credit.OrderRevisionNumber ?? entity.RevisionNumber));
    private static SalesCreditResponse ToCreditResponse(SalesCreditEntity row, SalesOrderEntity order) => new(row.DocumentId, row.CustomerId, row.CompanyId, row.OrderRevisionNumber is null ? null : row.CurrencyCode, row.OpenReceivables, row.OverdueReceivables, row.NetReceivableExposure, row.ProposedExposure, row.CreditLimit, row.Outcome, row.Reason, row.AsOfDate, row.EvaluatedAt, row.OverrideExpiresAt, row.TransactionCurrencyCode ?? order.CurrencyCode, row.TransactionAmount ?? order.Total, row.ConvertedOrderCommitment, DeserializeExchangeRate(row.ExchangeRateJson), row.OrderRevisionNumber);
    private static string LinesJson(IReadOnlyList<SalesLineWriteModel> lines) => JsonSerializer.Serialize(lines.Select(item => new SalesQuotationLineResponse(item.Id, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureId, item.UnitOfMeasureCode, item.Quantity, item.UnitPrice, item.ResolvedUnitPrice, item.DiscountPercent, item.DiscountAmount, item.TaxAmount, item.LineTotal, item.PriceListId, item.PriceVersionNumber, item.PriceEffectiveFrom, item.PriceProvenance, item.PriceSourceReference, item.ManualPriceApplied, item.CommercialAuthorityPolicyId, item.CommercialAuthorityActorId, item.CommercialAuthorityEvidence, item.Notes, item.TaxEvidence?.TaxId, item.TaxEvidence?.TaxCode, item.TaxEvidence?.RateVersionId, item.TaxEvidence?.RateVersionNumber, item.TaxEvidence?.EffectiveFrom, item.TaxEvidence?.EffectiveTo, item.TaxEvidence?.RatePercentage, item.TaxEvidence?.TaxableBase, item.TaxEvidence?.ReferenceValue)).ToArray(), Json);
    private static IReadOnlyList<SalesQuotationLineResponse> Lines(string json) => JsonSerializer.Deserialize<IReadOnlyList<SalesQuotationLineResponse>>(json, Json) ?? [];
    private static SalesQuotationResponse ToResponse(SalesQuotationEntity item) => new(item.Id, item.Number, item.TenantId.Value, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.QuotationDate, item.ValidUntil, item.CurrencyId, item.CurrencyCode, item.CustomerContactId, item.Notes, item.CustomerReference, item.Subtotal, item.DiscountAmount, item.TaxAmount, item.Total, item.Status, item.RevisionNumber, Lines(item.LinesJson), item.Version, item.CreatedAt, item.UpdatedAt, DeserializeExchangeRate(item.ExchangeRateJson), DeserializeApprovalState(item.CurrentApprovalsJson), DeserializePaymentTerm(item.PaymentTermJson));
    private static SalesQuotationSummaryResponse ToSummary(SalesQuotationEntity item) => new(item.Id, item.Number, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.QuotationDate, item.ValidUntil, item.CurrencyId, item.CurrencyCode, item.Subtotal, item.DiscountAmount, item.TaxAmount, item.Total, item.Status, item.RevisionNumber, item.Version, item.UpdatedAt);
    private static SalesOrderResponse ToResponse(SalesOrderEntity item) => new(item.Id, item.Number, item.TenantId.Value, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.SourceQuotationId, item.SourceQuotationNumber, item.SourceQuotationRevision, item.CurrencyId, item.CurrencyCode, item.Subtotal, item.DiscountAmount, item.TaxAmount, item.Total, item.Status, item.CreditOutcome, item.CreditReason, item.CreditEvaluatedAt, item.CreditOverrideExpiresAt, Lines(item.LinesJson), item.Version, item.CreatedAt, item.UpdatedAt, DeserializeExchangeRate(item.ExchangeRateJson), item.RevisionNumber, DeserializeApprovalState(item.CurrentApprovalsJson), DeserializePaymentTerm(item.PaymentTermJson));
    private static SalesExchangeRateEvidence? DeserializeExchangeRate(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<SalesExchangeRateEvidence>(json, Json);
    private static SalesPaymentTermSnapshot? DeserializePaymentTerm(string? json) => string.IsNullOrWhiteSpace(json) || json == "null" ? null : JsonSerializer.Deserialize<SalesPaymentTermSnapshot>(json, Json);
    private static SalesHandoffEvidence? DeserializeHandoff(string? json) => string.IsNullOrWhiteSpace(json) || json == "{}" || json == "null" ? null : JsonSerializer.Deserialize<SalesHandoffEvidence>(json, Json);
    private static (IReadOnlyList<SalesInvoiceRequestLine> Lines, IReadOnlyList<SalesInvoiceLineEvidence> Evidence) ReadInvoiceLines(string json)
    {
        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedInvoiceLines>(json, Json);
            if (persisted?.Lines is not null && persisted.Evidence is not null) return (persisted.Lines, persisted.Evidence);
        }
        catch (JsonException) { }
        return (JsonSerializer.Deserialize<IReadOnlyList<SalesInvoiceRequestLine>>(json, Json) ?? [], []);
    }
    private sealed record PersistedInvoiceLines(IReadOnlyList<SalesInvoiceRequestLine> Lines, IReadOnlyList<SalesInvoiceLineEvidence> Evidence);
    private static SalesOrderSummaryResponse ToSummary(SalesOrderEntity item) => new(item.Id, item.Number, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.SourceQuotationId, item.SourceQuotationNumber, item.SourceQuotationRevision, item.CurrencyId, item.CurrencyCode, item.Total, item.Status, item.CreditOutcome, item.Version, item.UpdatedAt, item.RevisionNumber);
    private static SalesDeliveryResponse ToDelivery(SalesDeliveryEntity item) => new(item.Id, item.TenantId.Value, item.OrderId, item.OrderRevisionNumber, item.CompanyId, item.BranchId, item.CustomerId, item.WarehouseId, item.Status, item.ErrorCode, JsonSerializer.Deserialize<IReadOnlyList<SalesDeliveryRequestLine>>(item.LinesJson, Json) ?? [], JsonSerializer.Deserialize<IReadOnlyList<Guid>>(item.MovementIdsJson, Json) ?? [], item.CreatedAt, item.PostedAt, item.Version, DeserializeHandoff(item.HandoffJson));
    private static SalesFulfillmentReportingRecord ToFulfillmentReporting(SalesDeliveryEntity item)
    {
        var lines = JsonSerializer.Deserialize<IReadOnlyList<SalesDeliveryRequestLine>>(item.LinesJson, Json) ?? [];
        var movements = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(item.MovementIdsJson, Json) ?? [];
        var handoff = DeserializeHandoff(item.HandoffJson) ?? new SalesHandoffEvidence("inventory.sales-delivery.post", [], "Unknown", "NotAcknowledged", "Required", null, 0, null, string.Empty);
        return new(item.Id, item.TenantId.Value, item.OrderId, item.OrderRevisionNumber, item.CompanyId, item.BranchId, item.CustomerId, item.WarehouseId, item.Status, item.ErrorCode, lines.Count, lines.Sum(value => value.Quantity), movements.Count, handoff, item.CreatedAt, item.PostedAt, item.Version.ToArray());
    }
    private static SalesInvoiceRequestResponse ToInvoice(SalesInvoiceRequestEntity item)
    {
        var persisted = ReadInvoiceLines(item.LinesJson);
        var evidence = persisted.Evidence.ToArray();
        return new(item.Id, item.TenantId.Value, item.OrderId, item.OrderRevisionNumber, item.DeliveryId, item.Status, item.ErrorCode, item.FinanceOpenItemId, item.Amount, persisted.Lines, item.CreatedAt, item.PostedAt, item.Version, evidence.Sum(value => value.NetAmount), evidence.Sum(value => value.TaxAmount), DeserializePaymentTerm(item.PaymentTermJson), evidence, DeserializeHandoff(item.HandoffJson), item.CurrencyCode, item.InvoiceDate, item.SourceSnapshotJson);
    }
    private static string Summary(SalesQuotationResponse item) => $"quotation={item.Number};revision={item.RevisionNumber};status={item.Status};total={item.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    private static string Summary(SalesOrderResponse item) => $"order={item.Number};source={item.SourceQuotationNumber};status={item.Status};credit={item.CreditOutcome};total={item.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static SalesOperationResult<T> Failure<T>(string code) => SalesOperationResult<T>.Failure(code);
    private static SalesApprovalPolicyDefinition? DeserializePolicy(string? json) => string.IsNullOrWhiteSpace(json) || json == "null" || json == "{}" ? null : JsonSerializer.Deserialize<SalesApprovalPolicyDefinition>(json, Json);
    private static SalesApprovalStateResponse? DeserializeApprovalState(string? json) => string.IsNullOrWhiteSpace(json) || json == "[]" || json == "null" ? null : JsonSerializer.Deserialize<SalesApprovalStateResponse>(json, Json);
    private static string? SerializeApprovalState(SalesApprovalStateResponse? state) => state is null ? null : JsonSerializer.Serialize(state, Json);
    private static SalesApprovalStateResponse NewApprovalState(SalesApprovalPolicyDefinition policy) {
        var stage = policy.Stages.OrderBy(item => item.Sequence).FirstOrDefault();
        return new(policy.PolicyId, policy.Version, 0, stage?.StageKey, stage?.RequiredApprovals ?? 0, 0, stage?.EligibleApproverIds ?? [], []);
    }
    private static ApprovalDecisionResult EvaluateApproval(SalesApprovalStateResponse? state, SalesApprovalPolicyDefinition? policy, Guid creatorId, Guid actorId, Guid? delegatedFromActorId, int revisionNumber, byte[] documentVersion)
    {
        if (policy is null || state is null || state.PolicyId != policy.PolicyId || state.PolicyVersion != policy.Version) return ApprovalDecisionResult.Failure("approval_state_missing");
        var stages = policy.Stages.OrderBy(item => item.Sequence).ToArray();
        if (state.CurrentStageIndex < 0 || state.CurrentStageIndex >= stages.Length) return ApprovalDecisionResult.Failure("approval_already_complete");
        var stage = stages[state.CurrentStageIndex];
        if (creatorId == actorId) return ApprovalDecisionResult.Failure("self_approval_denied");
        if (state.Decisions.Any(item => item.StageKey == stage.StageKey && item.ActorId == actorId)) return ApprovalDecisionResult.Failure("approval_already_recorded");
        if (policy.EnforceSeparationOfDuties && state.Decisions.Any(item => item.ActorId == actorId)) return ApprovalDecisionResult.Failure("approval_sod_violation");
        if (stage.EligibleApproverIds.Count > 0 && !stage.EligibleApproverIds.Contains(actorId))
        {
            if (!stage.AllowDelegation || delegatedFromActorId is null || !stage.EligibleApproverIds.Contains(delegatedFromActorId.Value)) return ApprovalDecisionResult.Failure("approver_not_eligible");
        }
        else if (delegatedFromActorId is not null) return ApprovalDecisionResult.Failure("delegation_invalid");
        var decision = new SalesApprovalDecisionResponse(stage.StageKey, actorId, delegatedFromActorId, DateTimeOffset.UtcNow, policy.PolicyId, policy.Version, revisionNumber, documentVersion.ToArray());
        var decisions = state.Decisions.Concat([decision]).ToArray();
        var count = decisions.Count(item => item.StageKey == stage.StageKey);
        if (count < stage.RequiredApprovals) return ApprovalDecisionResult.Success(state with { CurrentStageApprovalCount = count, Decisions = decisions }, false);
        var nextIndex = state.CurrentStageIndex + 1;
        if (nextIndex >= stages.Length) return ApprovalDecisionResult.Success(state with { CurrentStageIndex = nextIndex, CurrentStageKey = null, CurrentStageRequiredApprovals = 0, CurrentStageApprovalCount = 0, CurrentStageApproverIds = [], Decisions = decisions }, true);
        var next = stages[nextIndex];
        return ApprovalDecisionResult.Success(state with { CurrentStageIndex = nextIndex, CurrentStageKey = next.StageKey, CurrentStageRequiredApprovals = next.RequiredApprovals, CurrentStageApprovalCount = 0, CurrentStageApproverIds = next.EligibleApproverIds, Decisions = decisions }, false);
    }
    private sealed record ApprovalDecisionResult(bool Succeeded, string Code, SalesApprovalStateResponse? State, bool IsFullyApproved)
    {
        internal static ApprovalDecisionResult Failure(string code) => new(false, code, null, false);
        internal static ApprovalDecisionResult Success(SalesApprovalStateResponse state, bool complete) => new(true, "succeeded", state, complete);
    }
    private static string Action(Enum target) => target is SalesQuotationStatus quote ? quote switch { SalesQuotationStatus.PendingApproval => "submit", SalesQuotationStatus.Approved => "approve", SalesQuotationStatus.Rejected => "reject", SalesQuotationStatus.ReturnedForChange => "return", SalesQuotationStatus.Sent => "send", SalesQuotationStatus.Withdrawn => "withdraw", SalesQuotationStatus.Cancelled => "cancel", _ => "transition" } : target is SalesOrderStatus order ? order switch { SalesOrderStatus.PendingApproval => "submit", SalesOrderStatus.Approved => "approve", SalesOrderStatus.Rejected => "reject", SalesOrderStatus.ReturnedForChange => "return", SalesOrderStatus.Confirmed or SalesOrderStatus.CreditHold => "confirm", SalesOrderStatus.Cancelled => "cancel", _ => "transition" } : "transition";
    private static SalesHistoryAction ActionToHistory(Enum target) => target is SalesQuotationStatus quote ? quote switch { SalesQuotationStatus.PendingApproval => SalesHistoryAction.Submitted, SalesQuotationStatus.Approved => SalesHistoryAction.Approved, SalesQuotationStatus.Rejected => SalesHistoryAction.Rejected, SalesQuotationStatus.ReturnedForChange => SalesHistoryAction.ReturnedForChange, SalesQuotationStatus.Sent => SalesHistoryAction.Sent, SalesQuotationStatus.Withdrawn => SalesHistoryAction.Withdrawn, SalesQuotationStatus.Expired => SalesHistoryAction.Expired, SalesQuotationStatus.Cancelled => SalesHistoryAction.Cancelled, _ => SalesHistoryAction.Edited } : target is SalesOrderStatus order ? order switch { SalesOrderStatus.PendingApproval => SalesHistoryAction.Submitted, SalesOrderStatus.Approved => SalesHistoryAction.Approved, SalesOrderStatus.Rejected => SalesHistoryAction.Rejected, SalesOrderStatus.ReturnedForChange => SalesHistoryAction.ReturnedForChange, SalesOrderStatus.Cancelled => SalesHistoryAction.Cancelled, SalesOrderStatus.Confirmed => SalesHistoryAction.Confirmed, _ => SalesHistoryAction.Edited } : SalesHistoryAction.Edited;
    private static bool CanTransition(SalesQuotationStatus from, SalesQuotationStatus to) => (from, to) switch { (SalesQuotationStatus.Draft, SalesQuotationStatus.PendingApproval) => true, (SalesQuotationStatus.ReturnedForChange, SalesQuotationStatus.PendingApproval) => true, (SalesQuotationStatus.PendingApproval, SalesQuotationStatus.Approved or SalesQuotationStatus.Rejected or SalesQuotationStatus.ReturnedForChange or SalesQuotationStatus.Cancelled) => true, (SalesQuotationStatus.Approved, SalesQuotationStatus.Sent or SalesQuotationStatus.Withdrawn or SalesQuotationStatus.Cancelled) => true, (SalesQuotationStatus.Sent, SalesQuotationStatus.Withdrawn or SalesQuotationStatus.Expired) => true, _ => false };
    private static bool CanTransition(SalesOrderStatus from, SalesOrderStatus to) => (from, to) switch { (SalesOrderStatus.Draft or SalesOrderStatus.ReturnedForChange, SalesOrderStatus.PendingApproval) => true, (SalesOrderStatus.PendingApproval, SalesOrderStatus.Approved or SalesOrderStatus.Rejected or SalesOrderStatus.ReturnedForChange or SalesOrderStatus.Cancelled) => true, (SalesOrderStatus.Approved, SalesOrderStatus.Confirmed or SalesOrderStatus.Cancelled or SalesOrderStatus.CreditHold or SalesOrderStatus.ReturnedForChange) => true, (SalesOrderStatus.CreditHold, SalesOrderStatus.Confirmed or SalesOrderStatus.Cancelled or SalesOrderStatus.ReturnedForChange) => true, _ => false };
}

#pragma warning restore CS1591
