#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Sales;

public enum SalesCustomerReturnStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    AwaitingReceipt = 4,
    PartiallyReceived = 5,
    Received = 6,
    Completed = 7,
    Rejected = 8,
    Cancelled = 9,
    Reversed = 10,
    Unknown = 11,
    ReconciliationRequired = 12
}

public enum SalesCustomerReturnConsequence
{
    None = 1,
    CreditNote = 2,
    ReplacementRequested = 3
}

public sealed record SalesCustomerReturnEvidenceReference(
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source);

public sealed record SalesCustomerReturnLineRequest(Guid OrderLineId, decimal Quantity, string? Reason = null);

public sealed record SalesCustomerReturnCreateRequest(
    Guid DeliveryId,
    DateOnly ReturnDate,
    SalesCustomerReturnConsequence Consequence,
    Guid? InvoiceId,
    IReadOnlyList<SalesCustomerReturnLineRequest> Lines,
    string? Reason,
    IReadOnlyList<SalesCustomerReturnEvidenceReference>? Evidence = null);

public sealed record SalesCustomerReturnInvoiceAllocationResponse(
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

public sealed record SalesCustomerReturnActionRequest(string? Reason);

public sealed record SalesCustomerReturnSourceLineResponse(
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
    Guid? DeliveryMovementId);

public sealed record SalesCustomerReturnSourceResponse(
    Guid DeliveryId,
    Guid OrderId,
    int OrderRevisionNumber,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    Guid WarehouseId,
    DateTimeOffset? PostedAt,
    Guid? RecognizedInvoiceId,
    Guid? FinanceOpenItemId,
    string CurrencyCode,
    IReadOnlyList<SalesCustomerReturnSourceLineResponse> Lines,
    byte[]? Version = null,
    IReadOnlyList<SalesCustomerReturnInvoiceAllocationResponse>? InvoiceAllocations = null);

public sealed record SalesCustomerReturnLineResponse(
    Guid Id,
    Guid OrderLineId,
    decimal DeliveredQuantity,
    decimal PreviouslyReturnedQuantity,
    decimal ReturnQuantity,
    string? Reason);

public sealed record SalesCustomerReturnResponse(
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
    SalesCustomerReturnStatus Status,
    SalesCustomerReturnConsequence Consequence,
    DateOnly ReturnDate,
    string? Reason,
    string? HandoffJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SalesCustomerReturnLineResponse> Lines,
    IReadOnlyList<SalesCustomerReturnEvidenceReference> Evidence,
    byte[] Version);

#pragma warning restore CS1591
