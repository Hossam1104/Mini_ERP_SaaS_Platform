#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Inventory;

public enum InventoryCustomerReturnStatus
{
    AwaitingReceipt = 1,
    Received = 2,
    Inspected = 3,
    Posted = 4,
    Unknown = 5,
    ReconciliationRequired = 6,
    Reversed = 7
}

public enum InventoryCustomerReturnDisposition
{
    PendingInspection = 1,
    Restockable = 2,
    NonRestockable = 3,
    Damaged = 4,
    Scrap = 5,
    Rejected = 6
}

public sealed record InventoryCustomerReturnReceiptLineRequest(Guid OrderLineId, decimal Quantity);

public sealed record InventoryCustomerReturnReceiptRequest(
    DateOnly ReceiptDate,
    IReadOnlyList<InventoryCustomerReturnReceiptLineRequest> Lines,
    string PhysicalEvidenceReference);

public sealed record InventoryCustomerReturnInspectionLineRequest(
    Guid OrderLineId,
    decimal Quantity,
    InventoryCustomerReturnDisposition Disposition,
    string? Notes = null,
    bool? CommerciallyAccepted = null);

public sealed record InventoryCustomerReturnInspectionRequest(
    IReadOnlyList<InventoryCustomerReturnInspectionLineRequest> Lines,
    string InspectionEvidenceReference);

public sealed record InventoryCustomerReturnLineResponse(
    Guid Id,
    Guid OrderLineId,
    decimal ReceivedQuantity,
    decimal DispositionedQuantity,
    InventoryCustomerReturnDisposition Disposition,
    Guid? MovementId,
    Guid? DeliveryMovementId,
    decimal? DeliveryUnitCost,
    string? Notes,
    decimal CommerciallyAcceptedQuantity = 0m,
    decimal RestockedQuantity = 0m,
    decimal NonRestockableAcceptedQuantity = 0m,
    decimal RejectedQuantity = 0m,
    IReadOnlyList<Guid>? MovementIds = null,
    IReadOnlyList<Guid>? DeliveryMovementIds = null,
    IReadOnlyList<Guid>? ReversalMovementIds = null);

public sealed record InventoryCustomerReturnResponse(
    Guid Id,
    Guid TenantId,
    Guid SalesCustomerReturnId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    InventoryCustomerReturnStatus Status,
    string PhysicalEvidenceReference,
    string InspectionEvidenceReference,
    string HandoffState,
    DateOnly? ReceiptDate,
    DateTimeOffset? PostedAt,
    IReadOnlyList<InventoryCustomerReturnLineResponse> Lines,
    byte[] Version,
    Guid? EffectId = null,
    string? EffectFingerprint = null,
    string? RequestFingerprint = null,
    string? CommitState = null,
    string? AcknowledgementState = null,
    string? ReconciliationState = null,
    int AttemptCount = 0,
    string? LastError = null,
    DateTimeOffset? LastAttemptAt = null,
    string? CorrelationId = null);

#pragma warning restore CS1591
