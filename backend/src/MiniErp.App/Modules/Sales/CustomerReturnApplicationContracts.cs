#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.App.Modules.Sales;

public sealed record SalesCustomerReturnSourceLineRecord(
    Guid OrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal DeliveredQuantity,
    decimal AlreadyReturnedQuantity,
    decimal EligibleQuantity,
    decimal UnitNetAmount,
    decimal UnitTaxAmount,
    decimal UnitGrossAmount,
    Guid? DeliveryMovementId,
    decimal ReturnQuantity = 0m,
    Guid? ReturnLineId = null,
    Guid? TaxId = null,
    Guid? TaxRateVersionId = null,
    decimal ReceivedQuantity = 0m,
    decimal InspectedQuantity = 0m,
    decimal CommerciallyAcceptedQuantity = 0m,
    decimal RestockedQuantity = 0m,
    decimal NonRestockableAcceptedQuantity = 0m,
    decimal RejectedQuantity = 0m,
    string StockDisposition = "PendingInspection",
    IReadOnlyList<Guid>? InventoryMovementIds = null,
    IReadOnlyList<Guid>? DeliveryMovementIds = null,
    decimal? DeliveryUnitCost = null);

public sealed record SalesCustomerReturnInvoiceAllocationRecord(
    Guid Id,
    Guid InvoiceId,
    Guid? FinanceOpenItemId,
    Guid DeliveryId,
    Guid OrderLineId,
    int OrderRevisionNumber,
    decimal RecognizedQuantity,
    decimal ReturnQuantity,
    decimal CommerciallyAcceptedQuantity,
    decimal PreviouslyCreditedQuantity,
    decimal RemainingCreditableQuantity,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    string CurrencyCode,
    Guid? TaxId,
    Guid? TaxRateVersionId,
    int? TaxRateVersionNumber,
    string SourceAllocationFingerprint,
    string SourceInvoiceFingerprint);

public sealed record SalesCustomerReturnSourceRecord(
    Guid ReturnSourceId,
    Guid DeliveryId,
    Guid OrderId,
    int OrderRevisionNumber,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    Guid WarehouseId,
    DateTimeOffset? DeliveryPostedAt,
    Guid? RecognizedInvoiceId,
    Guid? FinanceOpenItemId,
    string CurrencyCode,
    IReadOnlyList<SalesCustomerReturnSourceLineRecord> Lines,
    SalesCustomerReturnStatus Status = SalesCustomerReturnStatus.Approved,
    SalesCustomerReturnConsequence Consequence = SalesCustomerReturnConsequence.None,
    byte[]? Version = null,
    IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord>? InvoiceAllocations = null);

/// <summary>
/// MESP-138 HOLD-5 (HOLD-138-S). Credit Note Finance authority is derived from the exact
/// intersection of the requested Return lines and quantities with the remaining eligible
/// recognized Invoice allocation capacity for the same Delivery/Order lineage, optionally
/// narrowed to one requested Invoice. Source-wide Invoice presence is never sufficient.
/// </summary>
public static class SalesCustomerReturnCreditEligibility
{
    /// <summary>Remaining recognized Invoice capacity for one requested Order line, respecting the optional Invoice filter.</summary>
    public static decimal LineCapacity(Guid orderLineId, Guid? invoiceId, IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> allocations) =>
        allocations
            .Where(item => item.OrderLineId == orderLineId && (invoiceId is null || item.InvoiceId == invoiceId))
            .Sum(item => Math.Max(0m, item.RemainingCreditableQuantity));

    /// <summary>Requested-line and requested-quantity intersected recognized coverage for the whole request.</summary>
    public static decimal Coverage(SalesCustomerReturnCreateRequest request, IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> allocations)
    {
        var coverage = 0m;
        foreach (var line in request.Lines ?? [])
        {
            if (line.Quantity <= 0m) continue;
            coverage += Math.Min(line.Quantity, LineCapacity(line.OrderLineId, request.InvoiceId, allocations));
        }
        return coverage;
    }

    /// <summary>Returns the failure code when the request cannot carry Credit Note Finance authority, otherwise null.</summary>
    public static string? Evaluate(SalesCustomerReturnCreateRequest request, IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord>? allocations)
    {
        if (request is null || request.Consequence != SalesCustomerReturnConsequence.CreditNote) return null;
        var source = allocations ?? [];
        var lines = request.Lines ?? [];
        if (source.Count == 0 || lines.Count == 0) return "recognized_invoice_required";
        if (request.InvoiceId is { } requestedInvoiceId)
        {
            if (!source.Any(item => item.InvoiceId == requestedInvoiceId)) return "invoice_source_mismatch";
            if (!source.Any(item => item.InvoiceId == requestedInvoiceId && item.RemainingCreditableQuantity > 0m && lines.Any(line => line.OrderLineId == item.OrderLineId && line.Quantity > 0m))) return "invoice_source_mismatch";
        }
        return Coverage(request, source) <= 0m ? "recognized_invoice_required" : null;
    }
}

public sealed record SalesCustomerReturnInventoryAcknowledgementLine(
    Guid OrderLineId,
    decimal ReceivedQuantity,
    decimal InspectedQuantity,
    decimal CommerciallyAcceptedQuantity,
    decimal RestockedQuantity,
    decimal NonRestockableAcceptedQuantity,
    decimal RejectedQuantity,
    string StockDisposition,
    IReadOnlyList<Guid> InventoryMovementIds,
    IReadOnlyList<Guid> DeliveryMovementIds,
    decimal? DeliveryUnitCost);

public sealed record SalesCustomerReturnInventoryAcknowledgementCommand(
    Guid ReturnId,
    Guid TenantId,
    Guid InventoryEffectId,
    string EffectFingerprint,
    string RequestFingerprint,
    string? DownstreamIdempotencyKey,
    string PhysicalEvidenceReference,
    string InspectionEvidenceReference,
    IReadOnlyList<SalesCustomerReturnInventoryAcknowledgementLine> Lines,
    string CommitState,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record SalesCustomerReturnInventoryFailureCommand(
    Guid ReturnId,
    Guid TenantId,
    Guid InventoryEffectId,
    string EffectFingerprint,
    string RequestFingerprint,
    string Error,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record SalesCustomerReturnDownstreamReversalCommand(
    Guid ReturnId,
    Guid TenantId,
    string Downstream,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    Guid? CreditNoteId = null,
    Guid? ReversalJournalId = null,
    Guid? OriginalJournalId = null,
    string? EffectFingerprint = null,
    string? RequestFingerprint = null,
    string? CommitState = null,
    string? DownstreamIdempotencyKey = null,
    Guid? OriginalInvoiceId = null,
    Guid? OriginalFinanceOpenItemId = null,
    Guid? OriginalPostingJournalId = null,
    IReadOnlyList<Guid>? OriginalSourceAllocationIds = null,
    IReadOnlyList<Guid>? OriginalTaxJournalIds = null,
    decimal? OriginalNetAmount = null,
    decimal? OriginalTaxAmount = null,
    decimal? OriginalGrossAmount = null,
    string? OriginalCurrencyCode = null,
    string? OriginalSourceFingerprint = null,
    string? OriginalEffectFingerprint = null,
    string? OriginalDownstreamIdempotencyKey = null,
    Guid? OriginalCompanyId = null,
    Guid? OriginalCustomerId = null);

public sealed record SalesCustomerReturnFinanceAllocationEffect(
    Guid SourceAllocationId,
    decimal Quantity,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    string SourceAllocationFingerprint);

public sealed record SalesCustomerReturnFinanceEffectCommand(
    Guid ReturnId,
    Guid TenantId,
    Guid CreditNoteId,
    Guid InvoiceId,
    IReadOnlyList<Guid> SourceAllocationIds,
    DateTimeOffset OccurredAt,
    Guid? FinanceOpenItemId = null,
    Guid? PostingJournalId = null,
    IReadOnlyList<Guid>? TaxJournalIds = null,
    decimal? NetAmount = null,
    decimal? TaxAmount = null,
    decimal? GrossAmount = null,
    string? CurrencyCode = null,
    string? SourceFingerprint = null,
    string? EffectFingerprint = null,
    string? RequestFingerprint = null,
    string? CommitState = null,
    string? DownstreamIdempotencyKey = null,
    IReadOnlyList<SalesCustomerReturnFinanceAllocationEffect>? Allocations = null,
    Guid? CompanyId = null,
    Guid? CustomerId = null);

public sealed record SalesCustomerReturnOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static SalesCustomerReturnOperationResult<T> Success(T value) => new(true, "succeeded", value);
    public static SalesCustomerReturnOperationResult<T> Failure(string code) => new(false, code, default);
}

public enum SalesCustomerReturnMutation
{
    Submit = 1,
    Approve = 2,
    Reject = 3,
    Cancel = 4,
    Reverse = 5
}

public sealed record SalesCustomerReturnCreateCommand(
    Guid Id,
    SalesCustomerReturnCreateRequest Request,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record SalesCustomerReturnActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    SalesCustomerReturnMutation Action,
    string? Reason,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    string RequestFingerprint);

public interface ISalesCustomerReturnPersistence
{
    Task<IReadOnlyList<SalesCustomerReturnSourceRecord>> ListEligibleSourcesAsync(ProcurementRequestContext context, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnSourceRecord?> GetEligibleSourceAsync(ProcurementRequestContext context, Guid deliveryId, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnResponse?> GetAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateAsync(ProcurementRequestContext context, SalesCustomerReturnCreateCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> MutateAsync(ProcurementRequestContext context, SalesCustomerReturnActionCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext context, SalesCustomerReturnInventoryAcknowledgementCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext context, SalesCustomerReturnInventoryFailureCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext context, SalesCustomerReturnDownstreamReversalCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RegisterFinanceCreditNoteAsync(TenantContext context, SalesCustomerReturnFinanceEffectCommand command, CancellationToken cancellationToken = default);
}

public interface ISalesCustomerReturnSourceProvider
{
    Task<SalesCustomerReturnSourceRecord?> GetCustomerReturnSourceAsync(TenantContext context, Guid returnId, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext context, SalesCustomerReturnInventoryAcknowledgementCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext context, SalesCustomerReturnInventoryFailureCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext context, SalesCustomerReturnDownstreamReversalCommand command, CancellationToken cancellationToken = default);
    Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RegisterFinanceCreditNoteAsync(TenantContext context, SalesCustomerReturnFinanceEffectCommand command, CancellationToken cancellationToken = default);
}

public sealed record SalesCustomerReturnReportingFinanceEffectRecord(
    Guid Id,
    Guid CreditNoteId,
    Guid InvoiceId,
    Guid FinanceOpenItemId,
    Guid PostingJournalId,
    IReadOnlyList<Guid> SourceAllocationIds,
    IReadOnlyList<Guid> TaxJournalIds,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    string CurrencyCode,
    string SourceFingerprint,
    string EffectFingerprint,
    string State,
    string ReversalState,
    Guid? ReversalJournalId,
    Guid? ReversalEffectId,
    DateTimeOffset AcknowledgedAt,
    DateTimeOffset? ReversedAt,
    byte[] Version);

public sealed record SalesCustomerReturnReportingRecord(
    Guid Id,
    Guid TenantId,
    Guid DeliveryId,
    Guid OrderId,
    int OrderRevisionNumber,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    Guid WarehouseId,
    Guid? InvoiceId,
    Guid? FinanceOpenItemId,
    string CurrencyCode,
    SalesCustomerReturnStatus Status,
    SalesCustomerReturnConsequence Consequence,
    DateOnly ReturnDate,
    int LineCount,
    decimal ReturnQuantity,
    Guid? InventoryEffectId,
    string InventoryCommitState,
    string InventoryAcknowledgementState,
    string InventoryReconciliationState,
    string? InventoryLastError,
    string FinanceEffectState,
    int ActiveFinanceCreditNoteCount,
    IReadOnlyList<Guid> FinanceCreditNoteIds,
    IReadOnlyList<Guid> FinanceReversedCreditNoteIds,
    IReadOnlyList<SalesCustomerReturnReportingFinanceEffectRecord> FinanceEffects,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);

public interface ISalesCustomerReturnReportingReadPort
{
    Task<ReportingSourcePage<SalesCustomerReturnReportingRecord>> ListReportingPageAsync(
        ProcurementRequestContext context,
        DateOnly? fromDate,
        DateOnly? toDate,
        SalesCustomerReturnStatus? status,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableSalesCustomerReturnPersistence : ISalesCustomerReturnPersistence, ISalesCustomerReturnSourceProvider, ISalesCustomerReturnReportingReadPort
{
    private static Task<IReadOnlyList<T>> Empty<T>() => Task.FromResult<IReadOnlyList<T>>([]);
    private static SalesCustomerReturnOperationResult<T> Failure<T>() => SalesCustomerReturnOperationResult<T>.Failure("sales_customer_return_persistence_unavailable");
    public Task<IReadOnlyList<SalesCustomerReturnSourceRecord>> ListEligibleSourcesAsync(ProcurementRequestContext c, CancellationToken x = default) => Empty<SalesCustomerReturnSourceRecord>();
    public Task<SalesCustomerReturnSourceRecord?> GetEligibleSourceAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<SalesCustomerReturnSourceRecord?>(null);
    public Task<SalesCustomerReturnResponse?> GetAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<SalesCustomerReturnResponse?>(null);
    public Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<IReadOnlyList<SalesHistoryResponse>>([]);
    public Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<IReadOnlyList<SalesAuditResponse>>([]);
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateAsync(ProcurementRequestContext c, SalesCustomerReturnCreateCommand m, CancellationToken x = default) => Task.FromResult(Failure<SalesCustomerReturnResponse>());
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> MutateAsync(ProcurementRequestContext c, SalesCustomerReturnActionCommand m, CancellationToken x = default) => Task.FromResult(Failure<SalesCustomerReturnResponse>());
    public Task<SalesCustomerReturnSourceRecord?> GetCustomerReturnSourceAsync(TenantContext c, Guid id, CancellationToken x = default) => Task.FromResult<SalesCustomerReturnSourceRecord?>(null);
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext c, SalesCustomerReturnInventoryAcknowledgementCommand m, CancellationToken x = default) => Task.FromResult(Failure<SalesCustomerReturnResponse>());
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext c, SalesCustomerReturnInventoryFailureCommand m, CancellationToken x = default) => Task.FromResult(Failure<SalesCustomerReturnResponse>());
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext c, SalesCustomerReturnDownstreamReversalCommand m, CancellationToken x = default) => Task.FromResult(Failure<SalesCustomerReturnResponse>());
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RegisterFinanceCreditNoteAsync(TenantContext c, SalesCustomerReturnFinanceEffectCommand m, CancellationToken x = default) => Task.FromResult(Failure<SalesCustomerReturnResponse>());
    public Task<ReportingSourcePage<SalesCustomerReturnReportingRecord>> ListReportingPageAsync(ProcurementRequestContext context, DateOnly? fromDate, DateOnly? toDate, SalesCustomerReturnStatus? status, ReportingPageRequest page, CancellationToken cancellationToken = default) => throw new InvalidOperationException("sales_customer_return_reporting_unavailable");
}

public sealed class SalesCustomerReturnService(
    ISalesCustomerReturnPersistence persistence,
    SalesAuthorizationService authorization)
{
    public async Task<SalesCustomerReturnOperationResult<IReadOnlyList<SalesCustomerReturnSourceResponse>>> ListEligibleSourcesAsync(ProcurementRequestContext context, CancellationToken cancellationToken = default)
    {
        if (!authorization.Authorize(context, "sales.customer-return.eligible-source.list")) return SalesCustomerReturnOperationResult<IReadOnlyList<SalesCustomerReturnSourceResponse>>.Failure("permission_denied");
        var sources = await persistence.ListEligibleSourcesAsync(context, cancellationToken);
        return SalesCustomerReturnOperationResult<IReadOnlyList<SalesCustomerReturnSourceResponse>>.Success(sources.Select(ToSourceResponse).ToArray());
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnSourceResponse>> GetEligibleSourceAsync(ProcurementRequestContext context, Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var source = await persistence.GetEligibleSourceAsync(context, deliveryId, cancellationToken);
        if (source is null) return SalesCustomerReturnOperationResult<SalesCustomerReturnSourceResponse>.Failure("return_source_not_found");
        if (!authorization.Authorize(context, "sales.customer-return.eligible-source.read", new SalesScope(source.TenantId, source.CompanyId, source.BranchId))) return SalesCustomerReturnOperationResult<SalesCustomerReturnSourceResponse>.Failure("permission_denied");
        return SalesCustomerReturnOperationResult<SalesCustomerReturnSourceResponse>.Success(ToSourceResponse(source));
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> GetAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        var value = await persistence.GetAsync(context, id, cancellationToken);
        if (value is null) return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("customer_return_not_found");
        return authorization.Authorize(context, "sales.customer-return.read", new SalesScope(value.TenantId, value.CompanyId, value.BranchId))
            ? SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(value)
            : SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("permission_denied");
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateAsync(ProcurementRequestContext context, SalesCustomerReturnCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request is null || request.DeliveryId == Guid.Empty || request.ReturnDate == default || request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(line => line.OrderLineId == Guid.Empty || line.Quantity <= 0m) || request.Lines.Select(line => line.OrderLineId).Distinct().Count() != request.Lines.Count)
            return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("validation_failed");
        var source = await persistence.GetEligibleSourceAsync(context, request.DeliveryId, cancellationToken);
        if (source is null) return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("return_source_not_found");
        if (!authorization.Authorize(context, "sales.customer-return.create", new SalesScope(source.TenantId, source.CompanyId, source.BranchId))) return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("permission_denied");
        if (SalesCustomerReturnCreditEligibility.Evaluate(request, source.InvoiceAllocations) is { } eligibilityFailure) return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure(eligibilityFailure);
        var fingerprint = Fingerprint(request);
        return await persistence.CreateAsync(context, new SalesCustomerReturnCreateCommand(Guid.NewGuid(), request, context.ActorId, DateTimeOffset.UtcNow, Normalize(idempotencyKey), fingerprint), cancellationToken);
    }

    public async Task<SalesCustomerReturnOperationResult<IReadOnlyList<SalesHistoryResponse>>> ListHistoryAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) =>
        (await persistence.GetAsync(context, id, cancellationToken)) is { } value && authorization.Authorize(context, "sales.customer-return.history.read", new SalesScope(value.TenantId, value.CompanyId, value.BranchId))
            ? SalesCustomerReturnOperationResult<IReadOnlyList<SalesHistoryResponse>>.Success(await persistence.ListHistoryAsync(context, id, cancellationToken))
            : SalesCustomerReturnOperationResult<IReadOnlyList<SalesHistoryResponse>>.Failure("customer_return_not_found");

    public async Task<SalesCustomerReturnOperationResult<IReadOnlyList<SalesAuditResponse>>> ListAuditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) =>
        (await persistence.GetAsync(context, id, cancellationToken)) is { } value && authorization.Authorize(context, "sales.customer-return.audit.read", new SalesScope(value.TenantId, value.CompanyId, value.BranchId))
            ? SalesCustomerReturnOperationResult<IReadOnlyList<SalesAuditResponse>>.Success(await persistence.ListAuditAsync(context, id, cancellationToken))
            : SalesCustomerReturnOperationResult<IReadOnlyList<SalesAuditResponse>>.Failure("customer_return_not_found");

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> MutateAsync(ProcurementRequestContext context, Guid id, byte[] expectedVersion, SalesCustomerReturnMutation action, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null || expectedVersion.Length == 0) return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("concurrency_conflict");
        var value = await persistence.GetAsync(context, id, cancellationToken);
        if (value is null) return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("customer_return_not_found");
        var operation = "sales.customer-return." + action.ToString().ToLowerInvariant();
        if (!authorization.Authorize(context, operation, new SalesScope(value.TenantId, value.CompanyId, value.BranchId))) return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("permission_denied");
        return await persistence.MutateAsync(context, new SalesCustomerReturnActionCommand(id, expectedVersion, action, reason, context.ActorId, DateTimeOffset.UtcNow, Normalize(idempotencyKey), Fingerprint(new { id, action, reason, expectedVersion })), cancellationToken);
    }

    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext context, SalesCustomerReturnInventoryAcknowledgementCommand command, CancellationToken cancellationToken = default) => persistence.AcknowledgeInventoryAsync(context, command, cancellationToken);
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext context, SalesCustomerReturnInventoryFailureCommand command, CancellationToken cancellationToken = default) => persistence.RecordInventoryFailureAsync(context, command, cancellationToken);
    public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext context, SalesCustomerReturnDownstreamReversalCommand command, CancellationToken cancellationToken = default) => persistence.RecordDownstreamReversalAsync(context, command, cancellationToken);

    private static SalesCustomerReturnSourceResponse ToSourceResponse(SalesCustomerReturnSourceRecord source) => new(source.DeliveryId, source.OrderId, source.OrderRevisionNumber, source.CompanyId, source.BranchId, source.CustomerId, source.WarehouseId, source.DeliveryPostedAt, source.RecognizedInvoiceId, source.FinanceOpenItemId, source.CurrencyCode, source.Lines.Select(line => new SalesCustomerReturnSourceLineResponse(line.OrderLineId, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, line.DeliveredQuantity, line.AlreadyReturnedQuantity, line.EligibleQuantity, line.UnitNetAmount, line.UnitTaxAmount, line.UnitGrossAmount, line.DeliveryMovementId)).ToArray(), source.Version, source.InvoiceAllocations?.Select(line => new SalesCustomerReturnInvoiceAllocationResponse(line.Id, line.InvoiceId, line.FinanceOpenItemId, line.DeliveryId, line.OrderLineId, line.OrderRevisionNumber, line.RecognizedQuantity, line.ReturnQuantity, line.CommerciallyAcceptedQuantity, line.PreviouslyCreditedQuantity, line.RemainingCreditableQuantity, line.NetAmount, line.TaxAmount, line.GrossAmount, line.CurrencyCode, line.TaxId, line.TaxRateVersionId, line.TaxRateVersionNumber, line.SourceAllocationFingerprint, line.SourceInvoiceFingerprint)).ToArray());
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}

#pragma warning restore CS1591
