#pragma warning disable CS1591

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

public sealed class CustomerReturnInventoryPersistence(
    DbContextOptions options,
    ISalesCustomerReturnSourceProvider salesReturns) : IInventoryCustomerReturnPersistence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private InventoryDbContext Create(InventoryRequestContext context) => new(options, context.TenantContext);

    public async Task<InventoryCustomerReturnResponse?> GetAsync(InventoryRequestContext context, Guid salesCustomerReturnId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var item = await db.CustomerReturns.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.SalesCustomerReturnId == salesCustomerReturnId, cancellationToken);
        return item is null || !InScope(context, item.CompanyId, item.BranchId) ? null : ToResponse(item);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReceiveAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, InventoryCustomerReturnReceiptRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync(db, context, "inventory.customer-return.receive", idempotencyKey, fingerprint, cancellationToken);
        if (replay is not null) return replay;
        var source = await salesReturns.GetCustomerReturnSourceAsync(context.TenantContext, salesCustomerReturnId, cancellationToken);
        if (source is null) return Failure("sales_return_not_found");
        if (source.Version is null || !source.Version.SequenceEqual(expectedVersion)) return Failure("concurrency_conflict");
        var existing = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.SalesCustomerReturnId == salesCustomerReturnId, cancellationToken);
        var entity = existing ?? new InventoryCustomerReturnEntity(context.TenantId, Guid.NewGuid(), salesCustomerReturnId, source.CompanyId, source.BranchId, source.WarehouseId, request, context.ActorId, DateTimeOffset.UtcNow, fingerprint, idempotencyKey, context.CorrelationId?.Value ?? "inventory");
        var knownOperation = existing?.RequestFingerprint == fingerprint;
        foreach (var line in request.Lines)
        {
            var sourceLine = source.Lines.SingleOrDefault(item => item.OrderLineId == line.OrderLineId);
            var localLine = entity.Lines.SingleOrDefault(item => item.OrderLineId == line.OrderLineId);
            if (knownOperation) continue;
            if (sourceLine is null || localLine is not null && localLine.ReceivedQuantity + line.Quantity > sourceLine.ReturnQuantity) return Failure("return_quantity_conflict");
            if (localLine is null) entity.Lines.Add(new InventoryCustomerReturnLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, line.OrderLineId, line.Quantity));
            else localLine.Receive(line.Quantity);
        }
        if (existing is null) db.CustomerReturns.Add(entity);
        else entity.SetOperation(fingerprint, idempotencyKey);
        AddAudit(db, context, "inventory.customer-return.receive", entity.Id, "Succeeded", null, null, "physical receipt recorded", idempotencyKey, fingerprint);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await CompleteHandoffAsync(context, entity.Id, "inventory.customer-return.receive", idempotencyKey, fingerprint, cancellationToken);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> InspectAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, InventoryCustomerReturnInspectionRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync(db, context, "inventory.customer-return.inspect", idempotencyKey, fingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.SalesCustomerReturnId == salesCustomerReturnId, cancellationToken);
        if (entity is null) return Failure("inventory_customer_return_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure("concurrency_conflict");
        if (entity.Status is not (InventoryCustomerReturnStatus.Received or InventoryCustomerReturnStatus.Inspected or InventoryCustomerReturnStatus.Posted or InventoryCustomerReturnStatus.ReconciliationRequired or InventoryCustomerReturnStatus.Unknown)) return Failure("inventory_customer_return_transition_invalid");
        var source = await salesReturns.GetCustomerReturnSourceAsync(context.TenantContext, salesCustomerReturnId, cancellationToken);
        if (source is null) return Failure("sales_return_not_found");
        var requested = request.Lines.ToDictionary(item => item.OrderLineId);
        var resolved = new List<(InventoryCustomerReturnLineEntity Line, InventoryCustomerReturnInspectionLineRequest Request, Guid DeliveryMovementId, decimal UnitCost)>();
        foreach (var item in entity.Lines)
        {
            if (!requested.TryGetValue(item.OrderLineId, out var inspection)) continue;
            if (inspection.Quantity > item.ReceivedQuantity) return Failure("disposition_quantity_conflict");
            if (inspection.Quantity <= item.DispositionedQuantity) continue;
            var remaining = inspection.Quantity - item.DispositionedQuantity;
            var sourceLine = source.Lines.SingleOrDefault(value => value.OrderLineId == item.OrderLineId);
            if (sourceLine is null) return Failure("return_source_mismatch");
            if (inspection.Disposition == InventoryCustomerReturnDisposition.Restockable)
            {
                var deliveryMovement = await db.StockMovements.AsNoTracking().Where(value => value.SourceType == InventoryMovementSourceType.SalesDelivery && value.Direction == InventoryMovementDirection.Outbound && value.SourceDocumentId == source.DeliveryId && value.SourceLineId == item.OrderLineId && value.ProductId == sourceLine.ProductId && value.UnitOfMeasureId == sourceLine.UnitOfMeasureId && value.WarehouseId == entity.WarehouseId && value.TenantId == context.TenantId).OrderByDescending(value => value.LedgerSequence).FirstOrDefaultAsync(cancellationToken);
                if (deliveryMovement is null || deliveryMovement.ValuationStatus != InventoryValuationStatus.Known || deliveryMovement.UnitCost is null || string.IsNullOrWhiteSpace(deliveryMovement.CurrencyCode))
                {
                    entity.SetInspected(request.InspectionEvidenceReference, InventoryCustomerReturnStatus.ReconciliationRequired, DateTimeOffset.UtcNow, fingerprint);
                    AddAudit(db, context, "inventory.customer-return.inspect", entity.Id, "Blocked", "delivery_cost_evidence_missing", entity.Status.ToString(), "ReconciliationRequired", idempotencyKey, fingerprint);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return Failure("delivery_cost_evidence_missing");
                }
                resolved.Add((item, inspection with { Quantity = remaining }, deliveryMovement.Id, deliveryMovement.UnitCost.Value));
            }
        }
        var now = DateTimeOffset.UtcNow;
        foreach (var value in resolved)
        {
            var sourceLine = source.Lines.Single(item => item.OrderLineId == value.Line.OrderLineId);
            var deliveryMovement = await db.StockMovements.AsNoTracking().SingleAsync(item => item.Id == value.DeliveryMovementId, cancellationToken);
            var movement = new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), entity.CompanyId, entity.BranchId, entity.WarehouseId, deliveryMovement.WarehouseCode, deliveryMovement.WarehouseName, sourceLine.ProductId, sourceLine.ProductSku, sourceLine.ProductName, sourceLine.UnitOfMeasureId, sourceLine.UnitOfMeasureCode, InventoryMovementDirection.Inbound, value.Request.Quantity, value.UnitCost, deliveryMovement.CurrencyCode, InventoryValuationStatus.Known, deliveryMovement.TrackingIdentity, InventoryMovementSourceType.CustomerReturn, entity.SalesCustomerReturnId, value.Line.Id, null, entity.ReceiptDate ?? DateOnly.FromDateTime(now.UtcDateTime), context.ActorId, context.CorrelationId?.Value ?? "inventory", now, sourceReference: $"sales-delivery:{source.DeliveryId:D};movement:{deliveryMovement.Id:D}");
            db.StockMovements.Add(movement);
            value.Line.Dispose(value.Request.Quantity, value.Request.Disposition, value.Request.CommerciallyAccepted ?? value.Request.Disposition != InventoryCustomerReturnDisposition.Rejected, value.Request.Notes, movement.Id, deliveryMovement.Id, value.UnitCost);
        }
        foreach (var item in entity.Lines)
        {
            if (requested.TryGetValue(item.OrderLineId, out var inspection) && inspection.Quantity > item.DispositionedQuantity && item.MovementId is null) item.Dispose(inspection.Quantity - item.DispositionedQuantity, inspection.Disposition, inspection.CommerciallyAccepted ?? inspection.Disposition != InventoryCustomerReturnDisposition.Rejected, inspection.Notes, null, null, null);
        }
        var complete = entity.Lines.All(item => item.DispositionedQuantity == item.ReceivedQuantity);
        entity.SetInspected(request.InspectionEvidenceReference, complete ? InventoryCustomerReturnStatus.Posted : InventoryCustomerReturnStatus.Inspected, now, fingerprint);
        AddAudit(db, context, "inventory.customer-return.inspect", entity.Id, "Succeeded", null, "Received", entity.Status.ToString(), idempotencyKey, fingerprint);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await CompleteHandoffAsync(context, entity.Id, "inventory.customer-return.inspect", idempotencyKey, fingerprint, cancellationToken);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReverseAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync(db, context, "inventory.customer-return.reverse", idempotencyKey, fingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.SalesCustomerReturnId == salesCustomerReturnId, cancellationToken);
        if (entity is null) return Failure("inventory_customer_return_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure("concurrency_conflict");
        if (entity.Status is not (InventoryCustomerReturnStatus.Posted or InventoryCustomerReturnStatus.ReconciliationRequired or InventoryCustomerReturnStatus.Unknown or InventoryCustomerReturnStatus.Reversed)) return Failure("return_reversal_transition_invalid");
        if (entity.Status != InventoryCustomerReturnStatus.Reversed)
        {
            var originalIds = entity.Lines.SelectMany(line => DeserializeIds(line.MovementIdsJson)).Distinct().ToArray();
            var originals = await db.StockMovements.Where(item => originalIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            if (originals.Count != originalIds.Length || originals.Values.Any(item => item.TenantId.Value != context.TenantId.Value || item.SourceType != InventoryMovementSourceType.CustomerReturn || item.Direction != InventoryMovementDirection.Inbound || item.SourceDocumentId != entity.SalesCustomerReturnId)) return Failure("return_movement_lineage_mismatch");
            foreach (var line in entity.Lines)
            {
                foreach (var originalId in DeserializeIds(line.MovementIdsJson))
                {
                    var existingCorrection = await db.StockMovements.SingleOrDefaultAsync(item => item.CorrectionOfMovementId == originalId, cancellationToken);
                    if (existingCorrection is not null)
                    {
                        if (existingCorrection.SourceType != InventoryMovementSourceType.Correction || existingCorrection.SourceDocumentId != entity.SalesCustomerReturnId || existingCorrection.Direction != InventoryMovementDirection.Outbound) return Failure("return_reversal_lineage_conflict");
                        line.RecordReversalMovement(existingCorrection.Id);
                        continue;
                    }
                    var original = originals[originalId];
                    var reversal = new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), original.CompanyId, original.BranchId, original.WarehouseId, original.WarehouseCode, original.WarehouseName, original.ProductId, original.ProductSku, original.ProductName, original.UnitOfMeasureId, original.UnitOfMeasureCode, InventoryMovementDirection.Outbound, original.Quantity, original.UnitCost, original.CurrencyCode, InventoryValuationStatus.Known, original.TrackingIdentity, InventoryMovementSourceType.Correction, entity.SalesCustomerReturnId, Guid.NewGuid(), original.Id, original.EffectiveDate, context.ActorId, context.CorrelationId?.Value ?? "inventory", DateTimeOffset.UtcNow, sourceReference: $"customer-return:{entity.SalesCustomerReturnId:D};correction-of:{original.Id:D};{original.SourceReference}");
                    db.StockMovements.Add(reversal);
                    line.RecordReversalMovement(reversal.Id);
                }
            }
            var now = DateTimeOffset.UtcNow;
            entity.BeginReversal(now, fingerprint);
            AddAudit(db, context, "inventory.customer-return.reverse", entity.Id, "Succeeded", null, "Posted", "Reversed", idempotencyKey, fingerprint);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await CompleteReversalHandoffAsync(context, entity.Id, idempotencyKey, fingerprint, cancellationToken);
    }

    private async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> CompleteHandoffAsync(InventoryRequestContext context, Guid entityId, string operation, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = Create(context);
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
        if (entity is null) return Failure("inventory_customer_return_not_found");
        var command = new SalesCustomerReturnInventoryAcknowledgementCommand(entity.SalesCustomerReturnId, context.TenantId.Value, entity.Id, entity.EffectFingerprint, entity.RequestFingerprint, entity.DownstreamIdempotencyKey, entity.PhysicalEvidenceReference, entity.InspectionEvidenceReference, entity.Lines.Select(line => new SalesCustomerReturnInventoryAcknowledgementLine(line.OrderLineId, line.ReceivedQuantity, line.DispositionedQuantity, line.CommerciallyAcceptedQuantity, line.RestockedQuantity, line.NonRestockableAcceptedQuantity, line.RejectedQuantity, line.Disposition.ToString(), DeserializeIds(line.MovementIdsJson), DeserializeIds(line.DeliveryMovementIdsJson), line.DeliveryUnitCost)).ToArray(), entity.CommitState, entity.CorrelationId, DateTimeOffset.UtcNow);
        var acknowledged = false;
        string? error = null;
        try
        {
            var result = await salesReturns.AcknowledgeInventoryAsync(context.TenantContext, command, cancellationToken);
            acknowledged = result.Succeeded;
            error = result.Succeeded ? null : result.Code;
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateException)
        {
            error = exception.Message;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        entity.SetHandoff(acknowledged, error, DateTimeOffset.UtcNow);
        if (!acknowledged)
        {
            try { await salesReturns.RecordInventoryFailureAsync(context.TenantContext, new SalesCustomerReturnInventoryFailureCommand(entity.SalesCustomerReturnId, context.TenantId.Value, entity.Id, entity.EffectFingerprint, entity.RequestFingerprint, error ?? "sales_acknowledgement_failed", entity.CorrelationId, DateTimeOffset.UtcNow), cancellationToken); } catch (Exception exception) when (exception is InvalidOperationException or DbUpdateException) { error = exception.Message; }
        }
        await db.SaveChangesAsync(cancellationToken);
        if (!acknowledged) { await transaction.CommitAsync(cancellationToken); return Failure(error ?? "sales_acknowledgement_failed"); }
        var response = ToResponse(entity);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) db.Idempotency.Add(new InventoryIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, idempotencyKey!, fingerprint, "customer-return", response.SalesCustomerReturnId, JsonSerializer.Serialize(response, Json), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryOperationResult<InventoryCustomerReturnResponse>.Success(response);
    }

    private async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> CompleteReversalHandoffAsync(InventoryRequestContext context, Guid entityId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = Create(context);
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
        if (entity is null) return Failure("inventory_customer_return_not_found");
        var acknowledged = false;
        string? error = null;
        try
        {
            var result = await salesReturns.RecordDownstreamReversalAsync(context.TenantContext, new SalesCustomerReturnDownstreamReversalCommand(entity.SalesCustomerReturnId, context.TenantId.Value, "Inventory", entity.CorrelationId, DateTimeOffset.UtcNow), cancellationToken);
            acknowledged = result.Succeeded;
            error = result.Succeeded ? null : result.Code;
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateException)
        {
            error = exception.Message;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        entity.SetReversalHandoff(acknowledged, error, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        if (!acknowledged)
        {
            await transaction.CommitAsync(cancellationToken);
            return Failure(error ?? "sales_reversal_acknowledgement_failed");
        }
        var response = ToResponse(entity);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) db.Idempotency.Add(new InventoryIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, "inventory.customer-return.reverse", idempotencyKey!, fingerprint, "customer-return", response.SalesCustomerReturnId, JsonSerializer.Serialize(response, Json), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryOperationResult<InventoryCustomerReturnResponse>.Success(response);
    }

    private static InventoryCustomerReturnResponse ToResponse(InventoryCustomerReturnEntity item) => new(item.Id, item.TenantId.Value, item.SalesCustomerReturnId, item.CompanyId, item.BranchId, item.WarehouseId, item.Status, item.PhysicalEvidenceReference, item.InspectionEvidenceReference, item.HandoffState, item.ReceiptDate, item.PostedAt, item.Lines.Select(line => new InventoryCustomerReturnLineResponse(line.Id, line.OrderLineId, line.ReceivedQuantity, line.DispositionedQuantity, line.Disposition, line.MovementId, line.DeliveryMovementId, line.DeliveryUnitCost, line.Notes, line.CommerciallyAcceptedQuantity, line.RestockedQuantity, line.NonRestockableAcceptedQuantity, line.RejectedQuantity, DeserializeIds(line.MovementIdsJson), DeserializeIds(line.DeliveryMovementIdsJson), DeserializeIds(line.ReversalMovementIdsJson))).ToArray(), item.Version, item.Id, item.EffectFingerprint, item.RequestFingerprint, item.CommitState, item.AcknowledgementState, item.ReconciliationState, item.AttemptCount, item.LastError, item.LastAttemptAt, item.CorrelationId);
    private static IReadOnlyList<Guid> DeserializeIds(string json) => JsonSerializer.Deserialize<IReadOnlyList<Guid>>(json, Json) ?? [];
    private static bool InScope(InventoryRequestContext context, Guid companyId, Guid? branchId) => context.TenantContext.Scope is not { } scope || ScopeMatches(scope.Value, companyId, branchId);
    private static bool ScopeMatches(string value, Guid companyId, Guid? branchId) { var parts = value.Split(':', 2); return parts.Length == 2 && Guid.TryParse(parts[1], out var id) && (parts[0] switch { "Tenant" => true, "Company" => companyId == id, "Branch" => branchId == id, _ => false }); }
    private static InventoryOperationResult<InventoryCustomerReturnResponse> Failure(string code) => InventoryOperationResult<InventoryCustomerReturnResponse>.Failure(code);
    private static void AddAudit(InventoryDbContext db, InventoryRequestContext context, string operation, Guid id, string decision, string? reason, string? before, string? after, string? key, string? fingerprint) => db.Audit.Add(new InventoryAuditEntity(context.TenantId, Guid.NewGuid(), "customer-return", id, operation, context.ActorId, context.SessionId, context.AuthorizationPath.ToString(), decision, reason, context.CorrelationId?.Value ?? string.Empty, key, fingerprint, before, after, DateTimeOffset.UtcNow));
    private static async Task<InventoryOperationResult<InventoryCustomerReturnResponse>?> ReplayAsync(InventoryDbContext db, InventoryRequestContext context, string operation, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var row = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.OperationId == operation && item.Key == key, cancellationToken);
        if (row is null) return null;
        return row.Fingerprint == fingerprint ? InventoryOperationResult<InventoryCustomerReturnResponse>.Success(JsonSerializer.Deserialize<InventoryCustomerReturnResponse>(row.SnapshotJson, Json)!) : Failure("idempotency_conflict");
    }
}

#pragma warning restore CS1591
