#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.App.Modules.Inventory;

public sealed partial class InventoryService
{
    internal async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> CreateOpeningBalanceForMigrationAsync(
        FoundationRequestContext foundationContext,
        InventoryOpeningBalanceCreateRequest request,
        Guid ownerBatchId,
        Guid ownerRowId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryResourceAuthorizationService.IsMigrationExecutionContext(foundationContext)
            || ownerBatchId == Guid.Empty
            || ownerRowId == Guid.Empty)
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden");
        try
        {
            return await CreateOpeningBalanceCoreAsync(
                InventoryRequestContext.FromFoundationContext(foundationContext),
                request,
                idempotencyKey,
                ownerBatchId,
                ownerRowId,
                migrationExecution: true,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden");
        }
    }

    internal async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> ValidateOpeningBalanceForMigrationAsync(
        FoundationRequestContext foundationContext,
        Guid openingId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        await ActOpeningForMigrationAsync(foundationContext, openingId, "migration validation", idempotencyKey, persistence.ValidateOpeningBalanceAsync, cancellationToken);

    internal async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> PostOpeningBalanceForMigrationAsync(
        FoundationRequestContext foundationContext,
        Guid openingId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        await ActOpeningForMigrationAsync(foundationContext, openingId, "migration execution", idempotencyKey, persistence.PostOpeningBalanceAsync, cancellationToken);

    internal async Task<InventoryOpeningBalanceRecord?> FindOpeningBalanceForMigrationAsync(
        FoundationRequestContext foundationContext,
        Guid openingId,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryResourceAuthorizationService.IsMigrationExecutionContext(foundationContext)) return null;
        try
        {
            var context = InventoryRequestContext.FromFoundationContext(foundationContext);
            var opening = await persistence.FindOpeningBalanceAsync(context, openingId, cancellationToken);
            return opening is not null && authorization.IsMigrationAllowed(context, OpeningScope(context, opening)) ? opening : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal async Task<IReadOnlyList<InventoryMovementRecord>> ListOpeningMovementsForMigrationAsync(
        FoundationRequestContext foundationContext,
        InventoryOpeningBalanceRecord opening,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryResourceAuthorizationService.IsMigrationExecutionContext(foundationContext)) return [];
        try
        {
            var context = InventoryRequestContext.FromFoundationContext(foundationContext);
            var scope = OpeningScope(context, opening);
            if (!authorization.IsMigrationAllowed(context, scope)) return [];
            return (await persistence.ListMovementsAsync(context, scope, cancellationToken: cancellationToken))
                .Where(item => item.SourceType == InventoryMovementSourceType.OpeningBalance && item.SourceDocumentId == opening.Id)
                .OrderBy(item => item.SourceLineId)
                .ToArray();
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    private async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> CreateOpeningBalanceCoreAsync(
        InventoryRequestContext context,
        InventoryOpeningBalanceCreateRequest request,
        string? idempotencyKey,
        Guid? ownerBatchId,
        Guid? ownerRowId,
        bool migrationExecution,
        CancellationToken cancellationToken)
    {
        if (request.Rows is null || request.Rows.Count == 0 || request.Rows.Count > 10000)
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("rows_required");
        if (migrationExecution && (request.Rows.Count != 1 || ownerBatchId is null || ownerRowId is null))
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("migration_opening_identity_invalid");
        if (request.CompanyId == Guid.Empty || request.WarehouseId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceOwner) || string.IsNullOrWhiteSpace(request.SourceSystem))
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("invalid_opening_source");
        if (request.ExtractedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("invalid_extracted_at");

        ScopeResolution scope;
        if (migrationExecution)
        {
            var warehouse = await warehouses.FindAsync(context, request.WarehouseId, cancellationToken);
            var target = new InventoryScope(context.TenantId.Value, request.CompanyId, request.BranchId, request.WarehouseId);
            scope = warehouse is { IsActive: true }
                && warehouse.CompanyId == request.CompanyId
                && warehouse.BranchId == request.BranchId
                && authorization.IsMigrationAllowed(context, target)
                ? ScopeResolution.Success(target, warehouse)
                : ScopeResolution.Failure("warehouse_not_available");
        }
        else
        {
            scope = await ResolveScopeAsync(context, "inventory.opening.create", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        }
        if (!scope.Succeeded) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure(scope.Code);

        var rows = new List<InventoryOpeningBalanceRowCommand>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            var product = await products.FindAsync(context, row.ProductId, cancellationToken);
            var validation = ValidateProduct(product, row.UnitOfMeasureId, row.TrackingIdentity);
            var code = validation.Succeeded ? ValidateOpeningRow(row) : validation.Code;
            rows.Add(new InventoryOpeningBalanceRowCommand(
                ownerRowId ?? Guid.NewGuid(), row.ProductId, row.UnitOfMeasureId, row.Quantity, row.UnitCost,
                NormalizeCurrency(row.CurrencyCode), NormalizeTracking(row.TrackingIdentity),
                Normalize(row.SourceLineReference, 256), product, code));
        }

        var command = new InventoryOpeningBalanceCommand(
            ownerBatchId ?? Guid.NewGuid(), scope.Value!, scope.Warehouse!.Code, scope.Warehouse.Name, request.AsOfDate, NormalizeRequired(request.SourceOwner, 256),
            NormalizeRequired(request.SourceSystem, 256), request.ExtractedAt, Normalize(request.SourceReference, 512),
            rows, context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
            Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try
        {
            var value = await persistence.CreateOpeningBalanceAsync(context, command, cancellationToken);
            return value is null
                ? InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("duplicate_or_conflict")
                : InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(value);
        }
        catch (InvalidOperationException)
        {
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("persistence_unavailable");
        }
    }

    private async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> ActOpeningForMigrationAsync(
        FoundationRequestContext foundationContext,
        Guid openingId,
        string reason,
        string idempotencyKey,
        Func<InventoryRequestContext, Guid, byte[], Guid, string?, string, string?, string, CancellationToken, Task<InventoryOpeningBalanceRecord?>> action,
        CancellationToken cancellationToken)
    {
        if (!InventoryResourceAuthorizationService.IsMigrationExecutionContext(foundationContext))
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden");
        try
        {
            var context = InventoryRequestContext.FromFoundationContext(foundationContext);
            var current = await persistence.FindOpeningBalanceAsync(context, openingId, cancellationToken);
            if (current is null) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("not_found");
            if (!authorization.IsMigrationAllowed(context, OpeningScope(context, current)))
                return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden");
            if (current.Status == InventoryOpeningBalanceStatus.Posted)
                return InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(current);

            var value = await action(
                context,
                openingId,
                current.Version,
                context.ActorId,
                reason,
                context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
                Normalize(idempotencyKey, 256),
                InventoryFingerprints.Create(new { openingId, reason }),
                cancellationToken);
            return value is null
                ? InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("conflict")
                : InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(value);
        }
        catch (ArgumentException)
        {
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden");
        }
        catch (InvalidOperationException)
        {
            return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("persistence_unavailable");
        }
    }

    private static InventoryScope OpeningScope(InventoryRequestContext context, InventoryOpeningBalanceRecord opening) =>
        new(context.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId);
}

#pragma warning restore CS1591
