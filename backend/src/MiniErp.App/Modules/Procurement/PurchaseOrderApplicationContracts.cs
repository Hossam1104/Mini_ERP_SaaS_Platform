#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record PurchaseOrderOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static PurchaseOrderOperationResult<T> Success(T value) => new(true, "succeeded", value);

    public static PurchaseOrderOperationResult<T> Failure(string code) => new(false, code, default);
}

public interface IPurchaseOrderReportingReadPort
{
    Task<ReportingSourcePage<PurchaseOrderListRecord>> ListReportingPageAsync(
        TenantContext tenantContext,
        PurchaseOrderStatus? status,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);
}

public sealed record PurchaseOrderSourceLineSnapshot(
    Guid SourceQuotationLineId,
    Guid PurchaseRequestLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal RequestedQuantity,
    decimal SelectedQuantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    Guid? TaxId,
    string? TaxCode,
    string? TaxName,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    DateOnly RequestedNeedByDate,
    DateOnly? OfferedDeliveryDate);

public sealed record PurchaseOrderSourceOptionRecord(
    PurchaseOrderSourceResponse Source,
    PurchaseRequestScope Scope,
    IReadOnlyList<PurchaseOrderSourceLineSnapshot> Lines,
    byte[] PurchaseRequestVersion);

public sealed record PurchaseOrderLineRecord(
    Guid Id,
    Guid SourceQuotationLineId,
    Guid PurchaseRequestLineId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal OrderedQuantity,
    decimal ConfirmedQuantity,
    decimal RemainingQuantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    string? TaxCode,
    string? TaxName,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    DateOnly RequestedNeedByDate,
    DateOnly? DeliveryDate,
    string? Notes,
    byte[] Version);

public sealed record PurchaseOrderEvidenceReference(
    Guid Id,
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source,
    string? ExternalReference,
    Guid RecordedByActorId,
    DateTimeOffset RecordedAt,
    byte[]? Version = null);

public sealed record PurchaseOrderSupplierChangeRecord(
    Guid Id,
    Guid ConfirmationId,
    Guid PurchaseOrderLineId,
    decimal PreviousOrderedQuantity,
    decimal? ProposedQuantity,
    decimal PreviousUnitPrice,
    decimal? ProposedUnitPrice,
    DateOnly? PreviousDeliveryDate,
    DateOnly? ProposedDeliveryDate,
    PurchaseOrderSupplierChangeStatus Status,
    string Reason,
    Guid ActorId,
    DateTimeOffset ProposedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionReason,
    byte[]? Version = null);

public sealed record PurchaseOrderConfirmationLineRecord(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal OrderedQuantityAtResponse,
    decimal ConfirmedQuantity,
    decimal RemainingQuantity,
    DateOnly? ExpectedDeliveryDate,
    decimal? ProposedQuantity,
    decimal? ProposedUnitPrice,
    DateOnly? ProposedDeliveryDate,
    string? ChangeReason,
    byte[]? Version = null);

public sealed record PurchaseOrderConfirmationRecord(
    Guid Id,
    Guid PurchaseOrderId,
    PurchaseOrderConfirmationStatus Status,
    DateOnly ResponseDate,
    string? SupplierReference,
    string? SupplierContact,
    string? Reason,
    string? Notes,
    Guid RecordedByActorId,
    DateTimeOffset RecordedAt,
    byte[] PurchaseOrderVersion,
    IReadOnlyList<PurchaseOrderConfirmationLineRecord> Lines,
    IReadOnlyList<PurchaseOrderEvidenceReference> Evidence,
    IReadOnlyList<PurchaseOrderSupplierChangeRecord> Changes,
    byte[] Version);

public sealed record PurchaseOrderCreateCommand(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid CreatedByActorId,
    PurchaseOrderSourceResponse Source,
    IReadOnlyList<PurchaseOrderSourceLineSnapshot> Lines,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseOrderEditCommand(
    Guid Id,
    string? Notes,
    IReadOnlyList<PurchaseOrderLineEditRequest> Lines,
    byte[] ExpectedVersion,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseOrderSubmitCommand(
    Guid Id,
    byte[] ExpectedVersion,
    PurchaseRequestApprovalPolicyDefinition Policy,
    bool IsReapproval,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseOrderApprovalCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    Guid? DelegatedFromActorId,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseOrderActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseOrderConfirmationLineCommand(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal OrderedQuantityAtResponse,
    decimal ConfirmedQuantity,
    decimal RemainingQuantity,
    DateOnly? ExpectedDeliveryDate,
    decimal? ProposedQuantity,
    decimal? ProposedUnitPrice,
    DateOnly? ProposedDeliveryDate,
    string? ChangeReason);

public sealed record PurchaseOrderConfirmationCommand(
    Guid Id,
    Guid PurchaseOrderId,
    PurchaseOrderConfirmationStatus Status,
    DateOnly ResponseDate,
    string? SupplierReference,
    string? SupplierContact,
    string? Reason,
    string? Notes,
    Guid ActorId,
    byte[] PurchaseOrderVersion,
    IReadOnlyList<PurchaseOrderConfirmationLineCommand> Lines,
    IReadOnlyList<PurchaseOrderEvidenceReference> Evidence,
    PurchaseRequestApprovalPolicyDefinition? ReapprovalPolicy,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseOrderRecord(
    Guid Id,
    Guid TenantId,
    Guid CreatedByActorId,
    PurchaseRequestScope Scope,
    PurchaseOrderStatus Status,
    PurchaseOrderSourceResponse Source,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? CancelledAt,
    Guid? LatestConfirmationId,
    PurchaseOrderConfirmationStatus? LatestConfirmationStatus,
    PurchaseRequestApprovalPolicyDefinition? ApprovalPolicy,
    PurchaseOrderApprovalRecord? Approval,
    IReadOnlyList<PurchaseOrderLineRecord> Lines,
    IReadOnlyList<PurchaseOrderSupplierChangeRecord> PendingChanges,
    byte[] Version);

public sealed record PurchaseOrderListRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    PurchaseOrderStatus Status,
    string SupplierCode,
    string SupplierName,
    string SupplierQuotationReference,
    string CurrencyCode,
    decimal Total,
    int LineCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);

public sealed record PurchaseOrderHistoryRecord(
    Guid EvidenceId,
    Guid PurchaseOrderId,
    DateTimeOffset OccurredAt,
    PurchaseOrderStatus FromStatus,
    PurchaseOrderStatus ToStatus,
    PurchaseOrderHistoryAction Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    Guid? DelegatedFromActorId);

public sealed record PurchaseOrderAuditEvidence(
    Guid EvidenceId,
    Guid PurchaseOrderId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    PurchaseOrderStatus? BeforeStatus,
    PurchaseOrderStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseOrderAuditRecord(
    Guid EvidenceId,
    Guid PurchaseOrderId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    PurchaseOrderStatus? BeforeStatus,
    PurchaseOrderStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

/// <summary>
/// Bounded durable replay probe input. <paramref name="PurchaseOrderId"/> is null only for creation,
/// which has no pre-existing target; every other operation must supply the exact resolved target so a
/// reused key against a different Purchase Order can never replay an unrelated result.
/// </summary>
public sealed record PurchaseOrderReplayQuery(
    Guid ActorId,
    string OperationId,
    string? IdempotencyKey,
    Guid? PurchaseOrderId,
    string? RequestFingerprint);

public enum PurchaseOrderReplayOutcome
{
    NotFound = 1,
    Replay = 2,
    Conflict = 3
}

public sealed record PurchaseOrderReplayProbe(PurchaseOrderReplayOutcome Outcome, PurchaseOrderRecord? Record)
{
    public static readonly PurchaseOrderReplayProbe NotFound = new(PurchaseOrderReplayOutcome.NotFound, null);
    public static readonly PurchaseOrderReplayProbe Conflict = new(PurchaseOrderReplayOutcome.Conflict, null);
    public static PurchaseOrderReplayProbe ForReplay(PurchaseOrderRecord record) => new(PurchaseOrderReplayOutcome.Replay, record);
}

public interface IPurchaseOrderPersistence
{
    Task<IReadOnlyList<PurchaseOrderListRecord>> ListAsync(TenantContext tenantContext, PurchaseOrderStatus? status, CancellationToken cancellationToken = default);
    Task<bool> SourceDecisionConsumedAsync(TenantContext tenantContext, Guid sourceDecisionId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderReplayProbe> ProbeReplayAsync(TenantContext tenantContext, PurchaseOrderReplayQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseOrderRecord?> FindAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CreateAsync(TenantContext tenantContext, PurchaseOrderCreateCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> EditAsync(TenantContext tenantContext, PurchaseOrderEditCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> SubmitAsync(TenantContext tenantContext, PurchaseOrderSubmitCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveAsync(TenantContext tenantContext, PurchaseOrderApprovalCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ReturnForChangeAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> IssueAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CancelAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RecordConfirmationAsync(TenantContext tenantContext, PurchaseOrderConfirmationCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveSupplierChangeAsync(TenantContext tenantContext, PurchaseOrderApprovalCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectSupplierChangeAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrderConfirmationRecord>> ReadConfirmationsAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrderHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrderAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public sealed class UnavailablePurchaseOrderPersistence : IPurchaseOrderPersistence
{
    private static Task<PurchaseOrderPersistenceResult<T>> Unavailable<T>() =>
        Task.FromResult(PurchaseOrderPersistenceResult<T>.Denied(PurchaseOrderPersistenceOutcome.Failure, "persistence_unavailable"));

    public Task<IReadOnlyList<PurchaseOrderListRecord>> ListAsync(TenantContext tenantContext, PurchaseOrderStatus? status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderListRecord>>([]);
    public Task<bool> SourceDecisionConsumedAsync(TenantContext tenantContext, Guid sourceDecisionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<PurchaseOrderReplayProbe> ProbeReplayAsync(TenantContext tenantContext, PurchaseOrderReplayQuery query, CancellationToken cancellationToken = default) => Task.FromResult(PurchaseOrderReplayProbe.NotFound);
    public Task<PurchaseOrderRecord?> FindAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseOrderRecord?>(null);
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CreateAsync(TenantContext tenantContext, PurchaseOrderCreateCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> EditAsync(TenantContext tenantContext, PurchaseOrderEditCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> SubmitAsync(TenantContext tenantContext, PurchaseOrderSubmitCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveAsync(TenantContext tenantContext, PurchaseOrderApprovalCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ReturnForChangeAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> IssueAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CancelAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RecordConfirmationAsync(TenantContext tenantContext, PurchaseOrderConfirmationCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveSupplierChangeAsync(TenantContext tenantContext, PurchaseOrderApprovalCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectSupplierChangeAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderRecord>();
    public Task<IReadOnlyList<PurchaseOrderConfirmationRecord>> ReadConfirmationsAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderConfirmationRecord>>([]);
    public Task<IReadOnlyList<PurchaseOrderHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderHistoryRecord>>([]);
    public Task<IReadOnlyList<PurchaseOrderAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderAuditRecord>>([]);
}

public sealed class PurchaseOrderValuePolicy
{
    public static bool TryText(string? value, int maxLength, bool allowEmpty, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return (allowEmpty || !string.IsNullOrWhiteSpace(normalized)) && (normalized?.Length ?? 0) <= maxLength;
    }

    public static bool TryNormalizeEdit(
        PurchaseOrderEditRequest request,
        out string? notes,
        out IReadOnlyList<PurchaseOrderLineEditRequest> lines)
    {
        notes = null;
        lines = [];
        if (request is null || request.Lines is null || request.Lines.Count == 0 || request.Lines.Count > 500
            || !TryText(request.Notes, 4096, true, out notes))
        {
            return false;
        }

        var normalized = new List<PurchaseOrderLineEditRequest>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (line.PurchaseOrderLineId == Guid.Empty || line.OrderedQuantity <= 0 || line.UnitPrice < 0 || line.UnitPrice > 1_000_000_000m
                || line.Notes?.Trim().Length > 2048)
            {
                return false;
            }

            normalized.Add(line with { Notes = string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim() });
        }

        lines = normalized;
        return true;
    }

    public static bool TryNormalizeConfirmation(
        PurchaseOrderConfirmationRequest request,
        out string? supplierReference,
        out string? supplierContact,
        out string? reason,
        out string? notes,
        out IReadOnlyList<PurchaseOrderConfirmationLineRequest> lines,
        out IReadOnlyList<PurchaseOrderEvidenceReferenceWriteRequest> evidence)
    {
        supplierReference = supplierContact = reason = notes = null;
        lines = [];
        evidence = [];
        if (request is null || request.Lines is null || request.Evidence is null
            || !Enum.IsDefined(request.Status)
            || !TryText(request.SupplierReference, 256, true, out supplierReference)
            || !TryText(request.SupplierContact, 256, true, out supplierContact)
            || !TryText(request.Reason, 4096, request.Status is not (PurchaseOrderConfirmationStatus.Rejected or PurchaseOrderConfirmationStatus.NoResponse), out reason)
            || !TryText(request.Notes, 4096, true, out notes)
            || request.Lines.Count > 500 || request.Evidence.Count > 50)
        {
            return false;
        }
        var normalizedLines = new List<PurchaseOrderConfirmationLineRequest>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (line.PurchaseOrderLineId == Guid.Empty || line.ConfirmedQuantity < 0
                || line.ProposedQuantity is <= 0
                || line.ProposedUnitPrice is < 0
                || line.ChangeReason?.Trim().Length > 4096)
            {
                return false;
            }
            var hasChange = line.ProposedQuantity.HasValue || line.ProposedUnitPrice.HasValue || line.ProposedDeliveryDate.HasValue;
            if (hasChange && !TryText(line.ChangeReason, 4096, false, out var changeReason))
            {
                return false;
            }

            normalizedLines.Add(line with { ChangeReason = string.IsNullOrWhiteSpace(line.ChangeReason) ? null : line.ChangeReason.Trim() });
        }

        var normalizedEvidence = new List<PurchaseOrderEvidenceReferenceWriteRequest>(request.Evidence.Count);
        foreach (var item in request.Evidence)
        {
            if (!TryText(item.ReferenceId, 256, true, out var referenceId)
                || !TryText(item.FileName, 255, true, out var fileName)
                || !TryText(item.ContentType, 128, true, out var contentType)
                || !TryText(item.Description, 1024, true, out var description)
                || !TryText(item.Source, 256, true, out var source)
                || !TryText(item.ExternalReference, 1024, true, out var externalReference)
                || string.IsNullOrWhiteSpace(referenceId) && string.IsNullOrWhiteSpace(externalReference))
            {
                return false;
            }

            normalizedEvidence.Add(item with
            {
                ReferenceId = referenceId,
                FileName = fileName,
                ContentType = contentType,
                Description = description,
                Source = source ?? "buyer-recorded",
                ExternalReference = externalReference
            });
        }

        lines = normalizedLines;
        evidence = normalizedEvidence;
        return true;
    }

    public static bool TryReason(string? reason, out string normalized) {
        normalized = reason?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 4096;
    }

    public static decimal Total(PurchaseOrderRecord order) => order.Lines.Sum(CommercialLineTotal);

    public static decimal CommercialLineTotal(PurchaseOrderLineRecord line)
    {
        var gross = line.OrderedQuantity * line.UnitPrice;
        var discount = line.DiscountAmount ?? (line.DiscountPercentage is { } percentage ? gross * percentage / 100m : 0m);
        var taxable = Math.Max(0m, gross - discount);
        var tax = line.TaxAmount ?? (line.TaxRatePercentage is { } rate ? taxable * rate / 100m : 0m);
        return taxable + tax;
    }
}

public sealed record PurchaseOrderPersistenceResult<T>(PurchaseOrderPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == PurchaseOrderPersistenceOutcome.Succeeded;
    public static PurchaseOrderPersistenceResult<T> Success(T value) => new(PurchaseOrderPersistenceOutcome.Succeeded, "persisted", value);
    public static PurchaseOrderPersistenceResult<T> Denied(PurchaseOrderPersistenceOutcome outcome, string code) => new(outcome, code, default);
}

public enum PurchaseOrderPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

#pragma warning restore CS1591
