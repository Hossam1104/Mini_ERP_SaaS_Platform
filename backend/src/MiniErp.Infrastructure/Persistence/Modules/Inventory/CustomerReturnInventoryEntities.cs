#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryCustomerReturnEntity : ITenantOwned
{
    private InventoryCustomerReturnEntity() { PhysicalEvidenceReference = string.Empty; InspectionEvidenceReference = string.Empty; HandoffState = string.Empty; CommitState = "Committed"; AcknowledgementState = "NotAcknowledged"; ReconciliationState = "Pending"; CorrelationId = string.Empty; }
    internal InventoryCustomerReturnEntity(TenantId tenantId, Guid id, Guid salesCustomerReturnId, Guid companyId, Guid? branchId, Guid warehouseId, InventoryCustomerReturnReceiptRequest request, Guid actorId, DateTimeOffset at, string requestFingerprint, string? idempotencyKey, string correlationId)
    { Id = id; TenantId = tenantId; SalesCustomerReturnId = salesCustomerReturnId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; Status = InventoryCustomerReturnStatus.Received; PhysicalEvidenceReference = request.PhysicalEvidenceReference.Trim(); InspectionEvidenceReference = string.Empty; HandoffState = "PhysicalReceiptRecorded"; CommitState = "Committed"; AcknowledgementState = "NotAcknowledged"; ReconciliationState = "Pending"; EffectFingerprint = requestFingerprint; RequestFingerprint = requestFingerprint; DownstreamIdempotencyKey = idempotencyKey; ReceiptDate = request.ReceiptDate; ActorId = actorId; CorrelationId = correlationId; CreatedAt = at; UpdatedAt = at; Version = Guid.NewGuid().ToByteArray(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid SalesCustomerReturnId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal InventoryCustomerReturnStatus Status { get; private set; }
    internal string PhysicalEvidenceReference { get; private set; } = string.Empty;
    internal string InspectionEvidenceReference { get; private set; } = string.Empty;
    internal string HandoffState { get; private set; } = string.Empty;
    internal string CommitState { get; private set; } = "Committed";
    internal string AcknowledgementState { get; private set; } = "NotAcknowledged";
    internal string ReconciliationState { get; private set; } = "Pending";
    internal string EffectFingerprint { get; private set; } = string.Empty;
    internal string RequestFingerprint { get; private set; } = string.Empty;
    internal string? DownstreamIdempotencyKey { get; private set; }
    internal int AttemptCount { get; private set; }
    internal string? LastError { get; private set; }
    internal DateTimeOffset? LastAttemptAt { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal DateOnly? ReceiptDate { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<InventoryCustomerReturnLineEntity> Lines { get; } = [];
    internal void SetInspected(string evidence, InventoryCustomerReturnStatus status, DateTimeOffset at, string requestFingerprint) { InspectionEvidenceReference = evidence.Trim(); Status = status; HandoffState = "Committed"; CommitState = "Committed"; ReconciliationState = "Pending"; RequestFingerprint = requestFingerprint; PostedAt = status == InventoryCustomerReturnStatus.Posted ? at : null; UpdatedAt = at; Version = Guid.NewGuid().ToByteArray(); }
    internal void SetHandoff(bool acknowledged, string? error, DateTimeOffset at)
    {
        AcknowledgementState = acknowledged ? "Acknowledged" : "NotAcknowledged";
        ReconciliationState = acknowledged ? "Reconciled" : "Required";
        HandoffState = acknowledged ? "Acknowledged" : "ReconciliationRequired";
        LastError = error;
        AttemptCount++;
        LastAttemptAt = at;
        if (!acknowledged && Status == InventoryCustomerReturnStatus.Posted) Status = InventoryCustomerReturnStatus.ReconciliationRequired;
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }
    internal void SetOperation(string requestFingerprint, string? idempotencyKey) { RequestFingerprint = requestFingerprint; DownstreamIdempotencyKey = idempotencyKey; UpdatedAt = DateTimeOffset.UtcNow; Version = Guid.NewGuid().ToByteArray(); }
    internal void BeginReversal(DateTimeOffset at, string requestFingerprint)
    {
        if (Status == InventoryCustomerReturnStatus.Reversed) return;
        if (Status is not (InventoryCustomerReturnStatus.Posted or InventoryCustomerReturnStatus.ReconciliationRequired or InventoryCustomerReturnStatus.Unknown)) throw new InvalidOperationException("return_reversal_transition_invalid");
        Status = InventoryCustomerReturnStatus.Reversed;
        CommitState = "Reversed";
        AcknowledgementState = "NotAcknowledged";
        ReconciliationState = "Pending";
        HandoffState = "ReversalRecorded";
        RequestFingerprint = requestFingerprint;
        LastError = null;
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }
    internal void SetReversalHandoff(bool acknowledged, string? error, DateTimeOffset at)
    {
        AcknowledgementState = acknowledged ? "Acknowledged" : "NotAcknowledged";
        ReconciliationState = acknowledged ? "Reconciled" : "Required";
        HandoffState = acknowledged ? "ReversalAcknowledged" : "ReversalReconciliationRequired";
        LastError = error;
        AttemptCount++;
        LastAttemptAt = at;
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }
}

internal sealed class InventoryCustomerReturnLineEntity : ITenantOwned
{
    private InventoryCustomerReturnLineEntity() { Notes = null; }
    internal InventoryCustomerReturnLineEntity(TenantId tenantId, Guid id, Guid returnId, Guid orderLineId, decimal receivedQuantity)
    { Id = id; TenantId = tenantId; InventoryCustomerReturnId = returnId; OrderLineId = orderLineId; ReceivedQuantity = receivedQuantity; DispositionedQuantity = 0m; Disposition = InventoryCustomerReturnDisposition.PendingInspection; Version = Guid.NewGuid().ToByteArray(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid InventoryCustomerReturnId { get; private set; }
    internal Guid OrderLineId { get; private set; }
    internal decimal ReceivedQuantity { get; private set; }
    internal decimal DispositionedQuantity { get; private set; }
    internal InventoryCustomerReturnDisposition Disposition { get; private set; }
    internal Guid? MovementId { get; private set; }
    internal Guid? DeliveryMovementId { get; private set; }
    internal decimal? DeliveryUnitCost { get; private set; }
    internal string? Notes { get; private set; }
    internal decimal CommerciallyAcceptedQuantity { get; private set; }
    internal decimal RestockedQuantity { get; private set; }
    internal decimal NonRestockableAcceptedQuantity { get; private set; }
    internal decimal RejectedQuantity { get; private set; }
    internal string MovementIdsJson { get; private set; } = "[]";
    internal string DeliveryMovementIdsJson { get; private set; } = "[]";
    internal string ReversalMovementIdsJson { get; private set; } = "[]";
    internal byte[] Version { get; private set; } = [];
    internal void Receive(decimal quantity) { if (quantity <= 0m || ReceivedQuantity + quantity < ReceivedQuantity) throw new InvalidOperationException("return_quantity_conflict"); ReceivedQuantity += quantity; Version = Guid.NewGuid().ToByteArray(); }
    internal void Dispose(decimal quantity, InventoryCustomerReturnDisposition disposition, bool commerciallyAccepted, string? notes, Guid? movementId, Guid? deliveryMovementId, decimal? deliveryUnitCost)
    {
        if (quantity <= 0m || DispositionedQuantity + quantity > ReceivedQuantity) throw new InvalidOperationException("disposition_quantity_conflict");
        DispositionedQuantity += quantity; Disposition = disposition; Notes = notes; MovementId ??= movementId; DeliveryMovementId ??= deliveryMovementId; DeliveryUnitCost ??= deliveryUnitCost;
        if (commerciallyAccepted) { CommerciallyAcceptedQuantity += quantity; if (disposition == InventoryCustomerReturnDisposition.Restockable) RestockedQuantity += quantity; if (disposition == InventoryCustomerReturnDisposition.NonRestockable) NonRestockableAcceptedQuantity += quantity; } else RejectedQuantity += quantity;
        var movements = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(MovementIdsJson) ?? []; MovementIdsJson = JsonSerializer.Serialize(movements.Concat(movementId is { } value ? [value] : []).Distinct()); var deliveryMovements = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(DeliveryMovementIdsJson) ?? []; DeliveryMovementIdsJson = JsonSerializer.Serialize(deliveryMovements.Concat(deliveryMovementId is { } deliveryValue ? [deliveryValue] : []).Distinct()); Version = Guid.NewGuid().ToByteArray();
    }
    internal void RecordReversalMovement(Guid movementId)
    {
        if (movementId == Guid.Empty) throw new InvalidOperationException("return_reversal_movement_invalid");
        var movements = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(ReversalMovementIdsJson) ?? [];
        ReversalMovementIdsJson = JsonSerializer.Serialize(movements.Append(movementId).Distinct());
        Version = Guid.NewGuid().ToByteArray();
    }
}

#pragma warning restore CS1591
