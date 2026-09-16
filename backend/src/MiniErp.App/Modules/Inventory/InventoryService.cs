#pragma warning disable CS1591

using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;

namespace MiniErp.App.Modules.Inventory;

public sealed partial class InventoryService(
    IInventoryPersistence persistence,
    InventoryResourceAuthorizationService authorization,
    IInventoryWarehouseProvider warehouses,
    IInventoryProductProvider products,
    IInventoryGoodsReceiptSourceProvider? goodsReceiptSources = null,
    IInventorySupplierReturnSourceProvider? supplierReturnSources = null,
    IInventorySupplierReturnHandoffWriter? supplierReturnHandoff = null,
    ISupplierReturnPhysicalEffectGate? physicalEffectGate = null,
    IInventorySupplierReturnStateProvider? supplierReturnState = null,
    IInventoryApprovalPolicyProvider? approvalPolicies = null,
    IInventoryApprovalDelegationProvider? approvalDelegation = null) : ISalesInventoryPort
{
    private readonly ISupplierReturnPhysicalEffectGate supplierReturnPhysicalEffectGate = physicalEffectGate ?? new SupplierReturnPhysicalEffectGate();
    private readonly IInventorySupplierReturnStateProvider supplierReturnStateProvider = supplierReturnState ?? new NoInventorySupplierReturnStateProvider();
    private readonly IInventoryApprovalPolicyProvider approvalPolicyProvider = approvalPolicies ?? new NoInventoryApprovalPolicyProvider();
    private readonly IInventoryApprovalDelegationProvider approvalDelegationProvider = approvalDelegation ?? new NoInventoryApprovalDelegationProvider();

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryWarehouseOption>>> ListWarehousesAsync(
        InventoryRequestContext context,
        Guid? companyId,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await warehouses.ListAsync(context, companyId, branchId, cancellationToken);
        if (context.TrustedScope is null)
        {
            return InventoryOperationResult<IReadOnlyList<InventoryWarehouseOption>>.Success(result);
        }

        var filtered = result.Where(item => authorization.IsAllowed(
            context,
            "inventory.warehouse.list",
            new InventoryScope(context.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId))).ToArray();
        return InventoryOperationResult<IReadOnlyList<InventoryWarehouseOption>>.Success(filtered);
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>> ListMovementsAsync(
        InventoryRequestContext context,
        Guid? warehouseId,
        Guid? companyId,
        Guid? branchId,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.ledger.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>.Failure(scope.Code);
        try
        {
            return InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>.Success(
                await persistence.ListMovementsAsync(context, scope.Value, productId, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryMovementRecord>> FindMovementAsync(
        InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindMovementAsync(context, movementId, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryMovementRecord>.Failure("not_found");
            return !authorization.IsAllowed(context, "inventory.ledger.read", new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryMovementRecord>.Failure("forbidden")
                : InventoryOperationResult<InventoryMovementRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryMovementRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryAvailabilityRecord>> GetAvailabilityAsync(
        InventoryRequestContext context,
        Guid warehouseId,
        Guid companyId,
        Guid? branchId,
        Guid productId,
        Guid unitOfMeasureId,
        string? trackingIdentity,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(context, "inventory.availability.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryAvailabilityRecord>.Failure(scope.Code);
        var warehouse = scope.Warehouse!;
        var product = await products.FindAsync(context, productId, cancellationToken);
        var productResult = ValidateProduct(product, unitOfMeasureId, trackingIdentity);
        if (!productResult.Succeeded) return InventoryOperationResult<InventoryAvailabilityRecord>.Failure(productResult.Code);
        try
        {
            var value = await persistence.GetAvailabilityAsync(context, scope.Value!, productId, unitOfMeasureId, NormalizeTracking(trackingIdentity), product!, warehouse, cancellationToken);
            return value is null
                ? InventoryOperationResult<InventoryAvailabilityRecord>.Failure("not_found")
                : InventoryOperationResult<InventoryAvailabilityRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryAvailabilityRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>> ListOpeningBalancesAsync(
        InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.opening.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>.Failure(scope.Code);
        try
        {
            return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>.Success(
                await persistence.ListOpeningBalancesAsync(context, scope.Value, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> FindOpeningBalanceAsync(
        InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindOpeningBalanceAsync(context, id, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("not_found");
            return !authorization.IsAllowed(context, "inventory.opening.read", new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden")
                : InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> CreateOpeningBalanceAsync(
        InventoryRequestContext context,
        InventoryOpeningBalanceCreateRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default) =>
        await CreateOpeningBalanceCoreAsync(context, request, idempotencyKey, null, null, migrationExecution: false, cancellationToken);

    public Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActOpeningAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.opening.validate", persistence.ValidateOpeningBalanceAsync, cancellationToken);

    public Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActOpeningAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.opening.post", persistence.PostOpeningBalanceAsync, cancellationToken);

    public Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActOpeningAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.opening.correct", persistence.CorrectOpeningBalanceAsync, cancellationToken);

    private async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> ActOpeningAsync(
        InventoryRequestContext context,
        Guid id,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        string operationId,
        Func<InventoryRequestContext, Guid, byte[], Guid, string?, string, string?, string, CancellationToken, Task<InventoryOpeningBalanceRecord?>> action,
        CancellationToken cancellationToken,
        bool migrationExecution = false)
    {
        var current = await persistence.FindOpeningBalanceAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("not_found");
        var scope = new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.WarehouseId);
        if (migrationExecution
            ? !authorization.IsMigrationAllowed(context, scope)
            : !authorization.IsAllowed(context, operationId, scope)) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden");
        try
        {
            var value = await action(context, id, expectedVersion, context.ActorId, Normalize(reason, 2048), context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, reason, expectedVersion }), cancellationToken);
            return value is null ? InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("conflict") : InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var opening = await persistence.FindOpeningBalanceAsync(context, id, cancellationToken);
            if (opening is null) return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Failure("not_found");
            if (!authorization.IsAllowed(context, "inventory.opening.history.read", new InventoryScope(context.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Failure("forbidden");
            return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Success(await persistence.ReadOpeningHistoryAsync(context, id, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>> ListReservationsAsync(InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId, Guid? productId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.reservation.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Failure(scope.Code);
        try { return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Success(await persistence.ListReservationsAsync(context, scope.Value, productId, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReservationRecord>> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindReservationAsync(context, id, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryReservationRecord>.Failure("not_found");
            return !authorization.IsAllowed(context, "inventory.reservation.read", new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryReservationRecord>.Failure("forbidden")
                : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReservationRecord>> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.RequestedQuantity <= 0 || string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference)) return InventoryOperationResult<InventoryReservationRecord>.Failure("invalid_reservation");
        var scope = await ResolveScopeAsync(context, "inventory.reservation.create", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryReservationRecord>.Failure(scope.Code);
        var product = await products.FindAsync(context, request.ProductId, cancellationToken);
        var productValidation = ValidateProduct(product, request.UnitOfMeasureId, request.TrackingIdentity);
        if (!productValidation.Succeeded) return InventoryOperationResult<InventoryReservationRecord>.Failure(productValidation.Code);
        try
        {
            var availability = await persistence.GetAvailabilityAsync(context, scope.Value!, request.ProductId, request.UnitOfMeasureId, NormalizeTracking(request.TrackingIdentity), product!, scope.Warehouse!, cancellationToken);
            var available = availability?.AvailableQuantity ?? 0;
            if (request.RequestedQuantity > available && !request.AllowPartialAllocation) return InventoryOperationResult<InventoryReservationRecord>.Failure("insufficient_available");
            var command = new InventoryReservationCommand(
                Guid.NewGuid(), scope.Value!, request.ProductId, request.UnitOfMeasureId, request.RequestedQuantity,
                NormalizeRequired(request.SourceType, 128), NormalizeRequired(request.SourceReference, 512), request.AllowPartialAllocation,
                NormalizeTracking(request.TrackingIdentity), product!, scope.Warehouse!.Code, scope.Warehouse.Name, context.ActorId,
                DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256),
                InventoryFingerprints.Create(request), request.SourceDocumentId, request.SourceLineId, request.SourceRevision, request.SourceQuantityLimit);
            var value = await persistence.CreateReservationAsync(context, command, available, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryReservationRecord>.Failure("conflict") : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public Task<InventoryOperationResult<InventoryReservationRecord>> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, quantity, reason, idempotencyKey, "inventory.reservation.reduce", persistence.ReduceReservationAsync, cancellationToken);

    public Task<InventoryOperationResult<InventoryReservationRecord>> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, null, reason, idempotencyKey, "inventory.reservation.release", (c, i, v, _, a, r, correlation, k, f, ct) => persistence.ReleaseReservationAsync(c, i, v, a, r, correlation, k, f, ct), cancellationToken);

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>> ListSalesReservationsAsync(InventoryRequestContext context, string sourceReference, string operationId = "sales.fulfillment.read", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceReference)) return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Failure("source_reference_required");
        try
        {
            var values = await persistence.ListReservationsAsync(context, null, null, cancellationToken);
            var matching = values.Where(item => string.Equals(item.SourceReference, sourceReference, StringComparison.Ordinal)).ToArray();
            if (matching.Any(item => !authorization.IsAllowed(context, operationId, new InventoryScope(context.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId))))
                return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Failure("forbidden");
            return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Success(matching);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReservationRecord>> CreateSalesReservationAsync(InventoryRequestContext context, InventoryReservationCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.RequestedQuantity <= 0m || request.SourceDocumentId is null || request.SourceLineId is null || request.SourceRevision is not > 0 || string.IsNullOrWhiteSpace(request.SourceReference)) return InventoryOperationResult<InventoryReservationRecord>.Failure("invalid_reservation");
        var scope = await ResolveScopeAsync(context, "sales.fulfillment.reserve", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryReservationRecord>.Failure(scope.Code);
        var product = await products.FindAsync(context, request.ProductId, cancellationToken); var productValidation = ValidateProduct(product, request.UnitOfMeasureId, request.TrackingIdentity); if (!productValidation.Succeeded) return InventoryOperationResult<InventoryReservationRecord>.Failure(productValidation.Code);
        try
        {
            var availability = await persistence.GetAvailabilityAsync(context, scope.Value!, request.ProductId, request.UnitOfMeasureId, NormalizeTracking(request.TrackingIdentity), product!, scope.Warehouse!, cancellationToken); var available = availability?.AvailableQuantity ?? 0m; if (request.RequestedQuantity > available && !request.AllowPartialAllocation) return InventoryOperationResult<InventoryReservationRecord>.Failure("insufficient_available");
            var command = new InventoryReservationCommand(Guid.NewGuid(), scope.Value!, request.ProductId, request.UnitOfMeasureId, request.RequestedQuantity, NormalizeRequired(request.SourceType, 128), NormalizeRequired(request.SourceReference, 512), request.AllowPartialAllocation, NormalizeTracking(request.TrackingIdentity), product!, scope.Warehouse!.Code, scope.Warehouse.Name, context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request), request.SourceDocumentId, request.SourceLineId, request.SourceRevision, request.SourceQuantityLimit);
            var value = await persistence.CreateReservationAsync(context, command, available, cancellationToken); return value is null ? InventoryOperationResult<InventoryReservationRecord>.Failure("conflict") : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReservationRecord>> AllocateSalesReservationAsync(InventoryRequestContext context, InventoryReservationRecord reservation, decimal quantity, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0m || reservation.SourceDocumentId is null || reservation.SourceLineId is null) return InventoryOperationResult<InventoryReservationRecord>.Failure("invalid_quantity");
        if (!authorization.IsAllowed(context, "sales.fulfillment.reserve", new InventoryScope(context.TenantId.Value, reservation.CompanyId, reservation.BranchId, reservation.WarehouseId))) return InventoryOperationResult<InventoryReservationRecord>.Failure("forbidden");
        try
        {
            var value = await persistence.AllocateReservationAsync(context, reservation.Id, reservation.Version, quantity, context.ActorId, "sales-backorder-allocation", context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { reservation.Id, quantity }), cancellationToken);
            return value is null ? InventoryOperationResult<InventoryReservationRecord>.Failure("conflict") : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventorySalesDeliveryPostingRecord>> PostSalesDeliveryAsync(InventoryRequestContext context, InventorySalesDeliveryPostCommand command, CancellationToken cancellationToken = default)
    {
        if (command.DeliveryId == Guid.Empty || command.SalesOrderId == Guid.Empty || command.SalesOrderRevision <= 0 || string.IsNullOrWhiteSpace(command.SourceReference) || command.Lines.Count == 0 || command.Lines.Any(item => item.ReservationId == Guid.Empty || item.SourceLineId == Guid.Empty || item.Quantity <= 0m || item.ExpectedReservationVersion.Length == 0 || string.IsNullOrWhiteSpace(item.SourceReference)))
            return InventoryOperationResult<InventorySalesDeliveryPostingRecord>.Failure("invalid_delivery");
        try
        {
            var reservations = await persistence.ListReservationsAsync(context, null, null, cancellationToken);
            var scope = reservations.Where(item => command.Lines.Any(line => line.ReservationId == item.Id)).Select(item => new InventoryScope(context.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId)).Distinct().ToArray();
            if (scope.Length != 1 || !authorization.IsAllowed(context, "sales.delivery.post", scope[0]))
                return InventoryOperationResult<InventorySalesDeliveryPostingRecord>.Failure("forbidden");
            var value = await persistence.PostSalesDeliveryAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventorySalesDeliveryPostingRecord>.Failure("conflict") : InventoryOperationResult<InventorySalesDeliveryPostingRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventorySalesDeliveryPostingRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<InventoryOperationResult<InventoryReservationRecord>> ActReservationAsync(
        InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal? quantity, string? reason, string? idempotencyKey, string operationId,
        Func<InventoryRequestContext, Guid, byte[], decimal, Guid, string?, string, string?, string, CancellationToken, Task<InventoryReservationRecord?>> action,
        CancellationToken cancellationToken)
    {
        var current = await persistence.FindReservationAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryReservationRecord>.Failure("not_found");
        if (!authorization.IsAllowed(context, operationId, new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.WarehouseId))) return InventoryOperationResult<InventoryReservationRecord>.Failure("forbidden");
        if (quantity is <= 0) return InventoryOperationResult<InventoryReservationRecord>.Failure("invalid_quantity");
        try
        {
            var value = await action(context, id, expectedVersion, quantity ?? 0, context.ActorId, Normalize(reason, 2048), context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, quantity, reason, expectedVersion }), cancellationToken);
            return value is null ? InventoryOperationResult<InventoryReservationRecord>.Failure("conflict") : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await persistence.FindReservationAsync(context, id, cancellationToken);
            if (reservation is null) return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Failure("not_found");
            if (!authorization.IsAllowed(context, "inventory.reservation.history.read", new InventoryScope(context.TenantId.Value, reservation.CompanyId, reservation.BranchId, reservation.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Failure("forbidden");
            return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Success(await persistence.ReadReservationHistoryAsync(context, id, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (resourceType.Equals("opening-balance", StringComparison.OrdinalIgnoreCase))
            {
                var opening = await persistence.FindOpeningBalanceAsync(context, resourceId, cancellationToken);
                if (opening is null) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("not_found");
                if (!authorization.IsAllowed(context, "inventory.opening.audit.read", new InventoryScope(context.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("forbidden");
            }
            else if (resourceType.Equals("reservation", StringComparison.OrdinalIgnoreCase))
            {
                var reservation = await persistence.FindReservationAsync(context, resourceId, cancellationToken);
                if (reservation is null) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("not_found");
                if (!authorization.IsAllowed(context, "inventory.reservation.audit.read", new InventoryScope(context.TenantId.Value, reservation.CompanyId, reservation.BranchId, reservation.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("forbidden");
            }
            else if (resourceType.Equals("transfer", StringComparison.OrdinalIgnoreCase))
            {
                var transfer = await persistence.FindTransferAsync(context, resourceId, cancellationToken);
                if (transfer is null) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("not_found");
                if (!authorization.IsAllowed(context, "inventory.transfer.audit.read", new InventoryScope(context.TenantId.Value, transfer.CompanyId, transfer.BranchId, transfer.SourceWarehouseId))
                    || !authorization.IsAllowed(context, "inventory.transfer.audit.read", new InventoryScope(context.TenantId.Value, transfer.CompanyId, transfer.BranchId, transfer.DestinationWarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("forbidden");
            }
            else return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("invalid_resource");

            return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Success(await persistence.ReadAuditAsync(context, resourceType, resourceId, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryGoodsReceiptPostingRecord>> PostGoodsReceiptAsync(InventoryRequestContext context, InventoryGoodsReceiptPostRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.GoodsReceiptId == Guid.Empty || request.GoodsReceiptLineId == Guid.Empty) return InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("validation_failed");
        var sourceProvider = goodsReceiptSources ?? new NoInventoryGoodsReceiptSourceProvider();
        if (request.ExpectedVersion is null || request.ExpectedVersion.Length == 0) return InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("validation_failed");
        var source = await sourceProvider.FindAsync(context, request.GoodsReceiptId, request.GoodsReceiptLineId, cancellationToken);
        if (source is null) return InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("goods_receipt_source_not_eligible");
        if (source.Product.TrackingEnabled) return InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("tracking_identity_required");
        if (!source.Receipt.Version.SequenceEqual(request.ExpectedVersion)) return InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("conflict");
        var scope = new InventoryScope(context.TenantId.Value, source.Receipt.Scope.CompanyId, source.Receipt.Scope.BranchId, source.Warehouse.WarehouseId);
        if (!authorization.IsAllowed(context, "inventory.goods-receipt.post", scope)) return InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("forbidden");
        var command = new InventoryGoodsReceiptPostCommand(Guid.NewGuid(), source, context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try
        {
            var value = await persistence.PostGoodsReceiptAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("conflict") : InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryGoodsReceiptPostingRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventorySupplierReturnPostingRecord>> PostSupplierReturnAsync(InventoryRequestContext context, InventorySupplierReturnPostRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.SupplierReturnId == Guid.Empty || request.ExpectedVersion is null || request.ExpectedVersion.Length == 0) return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("validation_failed");
        var sourceProvider = supplierReturnSources ?? new NoInventorySupplierReturnSourceProvider();
        var normalizedKey = Normalize(idempotencyKey, 256);
        var requestFingerprint = InventoryFingerprints.Create(request);
        InventorySupplierReturnPostingRecord? replayedPosting = null;
        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            InventoryReplayProbe<InventorySupplierReturnPostingRecord> replay;
            try
            {
                replay = await persistence.ProbeSupplierReturnReplayAsync(context, normalizedKey, requestFingerprint, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("persistence_unavailable");
            }

            if (replay.Outcome == InventoryReplayOutcome.Conflict)
            {
                return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("idempotency_conflict");
            }

            if (replay.Outcome == InventoryReplayOutcome.Replay)
            {
                if (replay.Value is null)
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("persistence_unavailable");
                }

                if (!replay.Value.CompanyId.HasValue || !replay.Value.WarehouseId.HasValue)
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("persistence_unavailable");
                }

                var replayScope = new InventoryScope(context.TenantId.Value, replay.Value.CompanyId.Value, replay.Value.BranchId, replay.Value.WarehouseId.Value);
                if (!authorization.IsAllowed(context, "inventory.supplier-return.post", replayScope))
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("forbidden");
                }

                if (replay.Value.HandoffRecorded)
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Success(replay.Value);
                }

                InventorySupplierReturnStateLookup state;
                try
                {
                    state = await supplierReturnStateProvider.FindAsync(context, request.SupplierReturnId, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("persistence_unavailable");
                }

                if (state.Outcome is InventorySupplierReturnStateLookupOutcome.Unavailable or InventorySupplierReturnStateLookupOutcome.NotFound || state.Record is null)
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("persistence_unavailable");
                }

                if (state.Record.Status is SupplierReturnStatus.InventoryHandoffRecorded or SupplierReturnStatus.AwaitingFinance or SupplierReturnStatus.Completed)
                {
                    return state.Record.InventoryHandoffId is not null
                        ? InventoryOperationResult<InventorySupplierReturnPostingRecord>.Success(replay.Value with { HandoffRecorded = true })
                        : InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("inventory_handoff_reconciliation_conflict");
                }

                if (state.Record.Status is not SupplierReturnStatus.AwaitingInventory)
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("inventory_handoff_reconciliation_conflict");
                }

                // The physical effect is durable, but the first caller may have lost the response
                // while the Procurement handoff was still being converged. Keep the source lookup
                // for that narrow pending case; the persistence replay is never posted again.
                replayedPosting = replay.Value;
            }
        }

        InventorySupplierReturnSourceRecord? source;
        InventorySupplierReturnPostingRecord? value;
        using (await supplierReturnPhysicalEffectGate.AcquireAsync(request.SupplierReturnId, cancellationToken))
        {
            source = await sourceProvider.FindAsync(context, request.SupplierReturnId, cancellationToken);
            if (source is null)
            {
                return replayedPosting is null
                    ? InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("supplier_return_source_not_eligible")
                    : InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("inventory_handoff_pending");
            }

            if (source.Lines.Any(line => line.Product.TrackingEnabled)) return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("tracking_identity_required");
            if (!source.SupplierReturn.Version.SequenceEqual(request.ExpectedVersion)) return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("conflict");
            var scope = new InventoryScope(context.TenantId.Value, source.SupplierReturn.Scope.CompanyId, source.SupplierReturn.Scope.BranchId, source.Warehouse.WarehouseId);
            if (!authorization.IsAllowed(context, "inventory.supplier-return.post", scope)) return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("forbidden");

            if (replayedPosting is not null)
            {
                // The writer takes the same gate around the Procurement state transition. Do not
                // invoke it while the physical-post gate is held, otherwise a retry can deadlock
                // itself. This branch is handled below after the source read is released.
                value = replayedPosting;
            }
            else
            {
                var command = new InventorySupplierReturnPostCommand(Guid.NewGuid(), source, context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), normalizedKey, requestFingerprint);
                try
                {
                    value = await persistence.PostSupplierReturnAsync(context, command, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("persistence_unavailable");
                }

                if (value is null) return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("conflict");
                if (value.HandoffRecorded) return InventoryOperationResult<InventorySupplierReturnPostingRecord>.Success(value);
            }
        }

        if (replayedPosting is not null)
        {
            var handoffWriter = supplierReturnHandoff ?? new NoInventorySupplierReturnHandoffWriter();
            var replayHandoff = await handoffWriter.RecordAsync(context, source!, replayedPosting.HandoffReference, cancellationToken);
            return replayHandoff.Succeeded
                ? InventoryOperationResult<InventorySupplierReturnPostingRecord>.Success(replayedPosting with { HandoffRecorded = true })
                : InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("inventory_handoff_pending");
        }

        var handoffWriterAfterPost = supplierReturnHandoff ?? new NoInventorySupplierReturnHandoffWriter();
        var handoff = await handoffWriterAfterPost.RecordAsync(context, source!, value!.HandoffReference, cancellationToken);
        return handoff.Succeeded
            ? InventoryOperationResult<InventorySupplierReturnPostingRecord>.Success(value with { HandoffRecorded = true })
            : InventoryOperationResult<InventorySupplierReturnPostingRecord>.Failure("inventory_handoff_pending");
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryTransferRecord>>> ListTransfersAsync(InventoryRequestContext context, Guid? companyId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        if (context.TrustedScope is not null && !companyId.HasValue) return InventoryOperationResult<IReadOnlyList<InventoryTransferRecord>>.Failure("scope_required");
        try
        {
            var values = await persistence.ListTransfersAsync(context, companyId.HasValue ? new InventoryScope(context.TenantId.Value, companyId.Value, branchId, Guid.Empty) : null, cancellationToken);
            var permitted = values.Where(item =>
                authorization.IsAllowed(context, "inventory.transfer.read", new InventoryScope(context.TenantId.Value, item.CompanyId, item.BranchId, item.SourceWarehouseId))
                && authorization.IsAllowed(context, "inventory.transfer.read", new InventoryScope(context.TenantId.Value, item.CompanyId, item.BranchId, item.DestinationWarehouseId))).ToArray();
            return InventoryOperationResult<IReadOnlyList<InventoryTransferRecord>>.Success(permitted);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryTransferRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryTransferRecord>> FindTransferAsync(InventoryRequestContext context, Guid transferId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindTransferAsync(context, transferId, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryTransferRecord>.Failure("not_found");
            var sourceScope = new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.SourceWarehouseId);
            var destinationScope = new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.DestinationWarehouseId);
            return authorization.IsAllowed(context, "inventory.transfer.read", sourceScope)
                && authorization.IsAllowed(context, "inventory.transfer.read", destinationScope)
                ? InventoryOperationResult<InventoryTransferRecord>.Success(value)
                : InventoryOperationResult<InventoryTransferRecord>.Failure("forbidden");
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryTransferRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryTransferRecord>> CreateTransferAsync(InventoryRequestContext context, InventoryTransferCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.CompanyId == Guid.Empty || request.SourceWarehouseId == Guid.Empty || request.DestinationWarehouseId == Guid.Empty || request.SourceWarehouseId == request.DestinationWarehouseId || request.ProductId == Guid.Empty || request.Quantity <= 0m) return InventoryOperationResult<InventoryTransferRecord>.Failure("validation_failed");
        var sourceResolution = await ResolveScopeAsync(context, "inventory.transfer.create", request.SourceWarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!sourceResolution.Succeeded) return InventoryOperationResult<InventoryTransferRecord>.Failure(sourceResolution.Code);
        var destination = await warehouses.FindAsync(context, request.DestinationWarehouseId, cancellationToken);
        if (destination is null || !destination.IsActive || destination.CompanyId != request.CompanyId || destination.BranchId != request.BranchId) return InventoryOperationResult<InventoryTransferRecord>.Failure("warehouse_not_available");
        if (!authorization.IsAllowed(context, "inventory.transfer.create", new InventoryScope(context.TenantId.Value, request.CompanyId, request.BranchId, destination.WarehouseId))) return InventoryOperationResult<InventoryTransferRecord>.Failure("forbidden");
        var product = await products.FindAsync(context, request.ProductId, cancellationToken); var validation = ValidateProduct(product, request.UnitOfMeasureId, request.TrackingIdentity);
        if (!validation.Succeeded) return InventoryOperationResult<InventoryTransferRecord>.Failure(validation.Code);
        var command = new InventoryTransferCreateCommand(Guid.NewGuid(), sourceResolution.Value!, sourceResolution.Warehouse!, destination, request.ProductId, request.UnitOfMeasureId, product!, request.Quantity, request.Mode, NormalizeTracking(request.TrackingIdentity), Normalize(request.Reason, 2048), context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try { var value = await persistence.CreateTransferAsync(context, command, cancellationToken); return value is null ? InventoryOperationResult<InventoryTransferRecord>.Failure("conflict") : InventoryOperationResult<InventoryTransferRecord>.Success(value); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryTransferRecord>.Failure("persistence_unavailable"); }
    }

    public Task<InventoryOperationResult<InventoryTransferRecord>> PostDirectTransferAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryTransferActionRequest? request, string? idempotencyKey, CancellationToken cancellationToken = default) => ActTransferAsync(context, id, expectedVersion, request, idempotencyKey, "inventory.transfer.direct", persistence.PostDirectTransferAsync, cancellationToken);
    public Task<InventoryOperationResult<InventoryTransferRecord>> ShipTransferAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryTransferActionRequest? request, string? idempotencyKey, CancellationToken cancellationToken = default) => ActTransferAsync(context, id, expectedVersion, request, idempotencyKey, "inventory.transfer.ship", persistence.ShipTransferAsync, cancellationToken);
    public Task<InventoryOperationResult<InventoryTransferRecord>> ReceiveTransferAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryTransferActionRequest? request, string? idempotencyKey, CancellationToken cancellationToken = default) => ActTransferAsync(context, id, expectedVersion, request, idempotencyKey, "inventory.transfer.receive", persistence.ReceiveTransferAsync, cancellationToken);
    public Task<InventoryOperationResult<InventoryTransferRecord>> ResolveTransferShortageAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryTransferActionRequest? request, string? idempotencyKey, CancellationToken cancellationToken = default) => ActTransferAsync(context, id, expectedVersion, request, idempotencyKey, "inventory.transfer.shortage-resolve", persistence.ResolveTransferShortageAsync, cancellationToken);
    public Task<InventoryOperationResult<InventoryTransferRecord>> CancelTransferAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryTransferActionRequest? request, string? idempotencyKey, CancellationToken cancellationToken = default) => ActTransferAsync(context, id, expectedVersion, request, idempotencyKey, "inventory.transfer.cancel", persistence.CancelTransferAsync, cancellationToken);

    public Task<InventoryOperationResult<IReadOnlyList<InventoryTransferEventRecord>>> ReadTransferHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => ReadTransferHistoryAuthorizedAsync(context, id, cancellationToken);

    public InventoryOperationResult<InventoryCustomerReturnBoundaryRecord> GetCustomerReturnBoundary(InventoryRequestContext context) =>
        InventoryOperationResult<InventoryCustomerReturnBoundaryRecord>.Success(new InventoryCustomerReturnBoundaryRecord(false, "unavailable", "sales_customer_return_handoff_required", "Sales integration boundary"));

    private async Task<InventoryOperationResult<InventoryTransferRecord>> ActTransferAsync(
        InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryTransferActionRequest? request, string? idempotencyKey, string operationId,
        Func<InventoryRequestContext, InventoryTransferActionCommand, CancellationToken, Task<InventoryTransferRecord?>> action, CancellationToken cancellationToken)
    {
        var current = await persistence.FindTransferAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryTransferRecord>.Failure("not_found");
        var sourceScope = new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.SourceWarehouseId);
        var destinationScope = new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.DestinationWarehouseId);
        if (!authorization.IsAllowed(context, operationId, sourceScope) || !authorization.IsAllowed(context, operationId, destinationScope)) return InventoryOperationResult<InventoryTransferRecord>.Failure("forbidden");
        if (expectedVersion is null || expectedVersion.Length == 0) return InventoryOperationResult<InventoryTransferRecord>.Failure("validation_failed");
        if (request?.Quantity is <= 0) return InventoryOperationResult<InventoryTransferRecord>.Failure("invalid_quantity");
        var canonicalRequest = request is null
            ? null
            : request with { Reference = InventoryTransferReferencePolicy.Normalize(request.Reference) };
        var command = new InventoryTransferActionCommand(id, expectedVersion, canonicalRequest?.Quantity, canonicalRequest?.Reference, Normalize(canonicalRequest?.Reason, 2048), context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, request = canonicalRequest, expectedVersion }));
        try { var value = await action(context, command, cancellationToken); return value is null ? InventoryOperationResult<InventoryTransferRecord>.Failure("conflict") : InventoryOperationResult<InventoryTransferRecord>.Success(value); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryTransferRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<InventoryOperationResult<IReadOnlyList<InventoryTransferEventRecord>>> ReadTransferHistoryAuthorizedAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken)
    {
        var current = await persistence.FindTransferAsync(context, id, cancellationToken); if (current is null) return InventoryOperationResult<IReadOnlyList<InventoryTransferEventRecord>>.Failure("not_found");
        if (!authorization.IsAllowed(context, "inventory.transfer.history.read", new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.SourceWarehouseId))
            || !authorization.IsAllowed(context, "inventory.transfer.history.read", new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.DestinationWarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryTransferEventRecord>>.Failure("forbidden");
        return InventoryOperationResult<IReadOnlyList<InventoryTransferEventRecord>>.Success(await persistence.ReadTransferHistoryAsync(context, id, cancellationToken));
    }

    private async Task<ScopeResolution> ResolveScopeAsync(InventoryRequestContext context, string operationId, Guid warehouseId, Guid companyId, Guid? branchId, CancellationToken cancellationToken)
    {
        if (warehouseId == Guid.Empty || companyId == Guid.Empty) return ScopeResolution.Failure("invalid_scope");
        var warehouse = await warehouses.FindAsync(context, warehouseId, cancellationToken);
        if (warehouse is null || !warehouse.IsActive || warehouse.CompanyId != companyId || warehouse.BranchId != branchId) return ScopeResolution.Failure("warehouse_not_available");
        var scope = new InventoryScope(context.TenantId.Value, companyId, branchId, warehouseId);
        return authorization.IsAllowed(context, operationId, scope) ? ScopeResolution.Success(scope, warehouse) : ScopeResolution.Failure("forbidden");
    }

    private async Task<ScopeResolution> ResolveOptionalScopeAsync(InventoryRequestContext context, string operationId, Guid? warehouseId, Guid? companyId, Guid? branchId, CancellationToken cancellationToken)
    {
        if (!warehouseId.HasValue)
        {
            return context.TrustedScope is null
                ? ScopeResolution.Success(null, null)
                : ScopeResolution.Failure("scope_required");
        }
        if (!companyId.HasValue) return ScopeResolution.Failure("company_required");
        return await ResolveScopeAsync(context, operationId, warehouseId.Value, companyId.Value, branchId, cancellationToken);
    }

    private static InventoryOperationResult<bool> ValidateProduct(InventoryProductReference? product, Guid unitOfMeasureId, string? trackingIdentity)
    {
        if (product is null) return InventoryOperationResult<bool>.Failure("product_not_available");
        if (!product.IsActive || !product.IsInventoryRelevant) return InventoryOperationResult<bool>.Failure("product_not_available");
        if (product.BaseUnitOfMeasureId != unitOfMeasureId) return InventoryOperationResult<bool>.Failure("unit_of_measure_not_supported");
        if (product.TrackingEnabled && string.IsNullOrWhiteSpace(trackingIdentity)) return InventoryOperationResult<bool>.Failure("tracking_identity_required");
        if (!product.TrackingEnabled && !string.IsNullOrWhiteSpace(trackingIdentity)) return InventoryOperationResult<bool>.Failure("tracking_not_enabled");
        return InventoryOperationResult<bool>.Success(true);
    }

    private static string? ValidateOpeningRow(InventoryOpeningBalanceRowRequest row)
    {
        if (row.Quantity <= 0) return "invalid_quantity";
        if (row.UnitCost < 0) return "invalid_unit_cost";
        if (string.IsNullOrWhiteSpace(row.CurrencyCode)) return "currency_required";
        if (string.IsNullOrWhiteSpace(row.SourceLineReference)) return "source_provenance_required";
        return null;
    }

    private sealed record ScopeResolution(bool Succeeded, string Code, InventoryScope? Value, InventoryWarehouseOption? Warehouse)
    {
        internal static ScopeResolution Success(InventoryScope? scope, InventoryWarehouseOption? warehouse) => new(true, "resolved", scope, warehouse);
        internal static ScopeResolution Failure(string code) => new(false, code, null, null);
    }

    private static string? Normalize(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string NormalizeRequired(string value, int max) => Normalize(value, max) ?? string.Empty;
    private static string NormalizeCurrency(string value) => NormalizeRequired(value, 16).ToUpperInvariant();
    private static string? NormalizeTracking(string? value) => Normalize(value, 256);
}

#pragma warning restore CS1591
