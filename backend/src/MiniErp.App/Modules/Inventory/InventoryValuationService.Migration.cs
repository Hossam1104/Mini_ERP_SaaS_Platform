#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.App.Modules.Inventory;

public sealed partial class InventoryValuationService
{
    internal async Task<InventoryOperationResult<InventoryValuationPolicyRecord>> CheckOpeningPolicyForMigrationAsync(
        FoundationRequestContext foundationContext,
        InventoryScope scope,
        DateOnly effectiveDate,
        CancellationToken cancellationToken = default)
    {
        var context = TryMigrationContext(foundationContext);
        if (context is null || !authorization.IsMigrationAllowed(context, scope))
            return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("forbidden");
        try
        {
            var candidates = (await persistence.ListPoliciesAsync(context, scope.CompanyId, cancellationToken))
                .Where(item => item.IsActive && item.EffectiveFrom <= effectiveDate && (item.EffectiveTo is null || item.EffectiveTo >= effectiveDate))
                .OrderByDescending(item => item.EffectiveFrom)
                .ThenByDescending(item => item.VersionNumber)
                .ToArray();
            if (candidates.Length == 0)
                return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("valuation_policy_not_configured");

            var latest = candidates[0];
            if (candidates.Count(item => item.EffectiveFrom == latest.EffectiveFrom && item.VersionNumber == latest.VersionNumber) != 1)
                return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("valuation_policy_ambiguous");
            return latest.FunctionalCurrencyCode.Length == 0
                ? InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("functional_currency_unavailable")
                : InventoryOperationResult<InventoryValuationPolicyRecord>.Success(latest);
        }
        catch (InvalidOperationException)
        {
            return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("persistence_unavailable");
        }
    }

    internal async Task<InventoryOperationResult<InventoryValuationProcessResult>> ProcessOpeningMovementsForMigrationAsync(
        FoundationRequestContext foundationContext,
        InventoryScope scope,
        Guid productId,
        Guid unitOfMeasureId,
        IReadOnlyList<Guid> movementIds,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var context = TryMigrationContext(foundationContext);
        if (context is null || !authorization.IsMigrationAllowed(context, scope) || movementIds.Count != 1 || movementIds[0] == Guid.Empty)
            return InventoryOperationResult<InventoryValuationProcessResult>.Failure("forbidden");
        try
        {
            var result = await persistence.ProcessAsync(
                context,
                new InventoryValuationProcessCommand(
                    scope.CompanyId,
                    scope.BranchId,
                    scope.WarehouseId,
                    productId,
                    unitOfMeasureId,
                    context.ActorId,
                    occurredAt,
                    context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
                    idempotencyKey,
                    requestFingerprint,
                    movementIds),
                cancellationToken);
            return result.Succeeded && result.Value is not null
                ? InventoryOperationResult<InventoryValuationProcessResult>.Success(result.Value)
                : InventoryOperationResult<InventoryValuationProcessResult>.Failure(result.Code);
        }
        catch (InvalidOperationException)
        {
            return InventoryOperationResult<InventoryValuationProcessResult>.Failure("persistence_unavailable");
        }
    }

    internal async Task<(IReadOnlyList<InventoryMovementValuationEventRecord> Events, IReadOnlyList<InventoryFinanceValuationHandoffRecord> Handoffs)?> ReadOpeningEvidenceForMigrationAsync(
        FoundationRequestContext foundationContext,
        InventoryScope scope,
        Guid productId,
        Guid unitOfMeasureId,
        string? trackingIdentity,
        CancellationToken cancellationToken = default)
    {
        var context = TryMigrationContext(foundationContext);
        if (context is null || !authorization.IsMigrationAllowed(context, scope)) return null;
        try
        {
            var query = new InventoryValuationQuery(
                scope.CompanyId,
                scope.BranchId,
                scope.WarehouseId,
                productId,
                unitOfMeasureId,
                trackingIdentity,
                SourceType: InventoryMovementSourceType.OpeningBalance);
            var events = await persistence.ListEventsAsync(context, query, cancellationToken);
            var handoffs = await persistence.ListFinanceHandoffsAsync(context, query, cancellationToken);
            return (events, handoffs);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static InventoryRequestContext? TryMigrationContext(FoundationRequestContext foundationContext)
    {
        if (!InventoryResourceAuthorizationService.IsMigrationExecutionContext(foundationContext)) return null;
        try { return InventoryRequestContext.FromFoundationContext(foundationContext); }
        catch (ArgumentException) { return null; }
    }
}

#pragma warning restore CS1591
