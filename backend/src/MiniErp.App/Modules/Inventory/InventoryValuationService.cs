#pragma warning disable CS1591

using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.Inventory;

public sealed partial class InventoryValuationService(
    IInventoryValuationPersistence persistence,
    InventoryResourceAuthorizationService authorization,
    IInventoryWarehouseProvider warehouses,
    IMasterDataCurrencyPaymentTermPersistence currencies)
{
    public async Task<InventoryOperationResult<InventoryValuationPolicyRecord>> CreatePolicyAsync(
        InventoryRequestContext context,
        InventoryValuationPolicyRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidPolicy(request)) return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("validation_failed");
        if (!authorization.IsCompanyAllowed(context, "inventory.valuation.policy.create", context.TenantId.Value, request.CompanyId, null))
            return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("forbidden");

        MasterDataCurrencyRecord? currency;
        try { currency = await currencies.FindCurrencyAsync(context.TenantContext, request.FunctionalCurrencyId, cancellationToken); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("persistence_unavailable"); }
        if (currency is null || currency.LifecycleState != MasterDataLifecycleState.Active
            || !string.Equals(currency.Code, request.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
            return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("functional_currency_not_active");

        var command = new InventoryValuationPolicyCommand(
            Guid.NewGuid(),
            request with { FunctionalCurrencyCode = currency.Code.Trim().ToUpperInvariant() },
            context.ActorId,
            DateTimeOffset.UtcNow,
            context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
            idempotencyKey,
            InventoryFingerprints.Create(request with { FunctionalCurrencyCode = currency.Code.Trim().ToUpperInvariant() }));
        try
        {
            var result = await persistence.CreatePolicyAsync(context, command, cancellationToken);
            return result.Succeeded && result.Value is not null
                ? InventoryOperationResult<InventoryValuationPolicyRecord>.Success(result.Value)
                : InventoryOperationResult<InventoryValuationPolicyRecord>.Failure(result.Code);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryValuationPolicyRecord>>> ListPoliciesAsync(
        InventoryRequestContext context,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || !authorization.IsCompanyAllowed(context, "inventory.valuation.policy.read", context.TenantId.Value, companyId, null))
            return InventoryOperationResult<IReadOnlyList<InventoryValuationPolicyRecord>>.Failure("forbidden");
        try { return InventoryOperationResult<IReadOnlyList<InventoryValuationPolicyRecord>>.Success(await persistence.ListPoliciesAsync(context, companyId, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryValuationPolicyRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryValuationProcessResult>> ProcessAsync(
        InventoryRequestContext context,
        InventoryValuationProcessCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await IsQueryAllowedAsync(context, "inventory.valuation.process", command.CompanyId, command.BranchId, command.WarehouseId, cancellationToken))
            return InventoryOperationResult<InventoryValuationProcessResult>.Failure("forbidden");
        try
        {
            var result = await persistence.ProcessAsync(context, command, cancellationToken);
            return result.Succeeded && result.Value is not null
                ? InventoryOperationResult<InventoryValuationProcessResult>.Success(result.Value)
                : InventoryOperationResult<InventoryValuationProcessResult>.Failure(result.Code);
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "valuation_concurrency_conflict", StringComparison.Ordinal)) { return InventoryOperationResult<InventoryValuationProcessResult>.Failure("conflict"); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryValuationProcessResult>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryValuationStateRecord>>> ListStatesAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        if (!await IsQueryAllowedAsync(context, "inventory.valuation.state.read", query.CompanyId, query.BranchId, query.WarehouseId, cancellationToken)) return InventoryOperationResult<IReadOnlyList<InventoryValuationStateRecord>>.Failure("forbidden");
        try { return InventoryOperationResult<IReadOnlyList<InventoryValuationStateRecord>>.Success(await persistence.ListStatesAsync(context, query, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryValuationStateRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryMovementValuationEventRecord>>> ListEventsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        if (!await IsQueryAllowedAsync(context, "inventory.valuation.history.read", query.CompanyId, query.BranchId, query.WarehouseId, cancellationToken)) return InventoryOperationResult<IReadOnlyList<InventoryMovementValuationEventRecord>>.Failure("forbidden");
        try { return InventoryOperationResult<IReadOnlyList<InventoryMovementValuationEventRecord>>.Success(await persistence.ListEventsAsync(context, query, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryMovementValuationEventRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryValuationReconciliationRecord>>> ReconcileAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        if (!IsCurrentReconciliationQuery(query)) return InventoryOperationResult<IReadOnlyList<InventoryValuationReconciliationRecord>>.Failure("validation_failed");
        if (!await IsQueryAllowedAsync(context, "inventory.valuation.reconciliation.read", query.CompanyId, query.BranchId, query.WarehouseId, cancellationToken)) return InventoryOperationResult<IReadOnlyList<InventoryValuationReconciliationRecord>>.Failure("forbidden");
        try { return InventoryOperationResult<IReadOnlyList<InventoryValuationReconciliationRecord>>.Success(await persistence.ReconcileAsync(context, query, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryValuationReconciliationRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryValuationSummaryRecord>> SummaryAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        if (!IsCurrentReconciliationQuery(query)) return InventoryOperationResult<InventoryValuationSummaryRecord>.Failure("validation_failed");
        if (!await IsQueryAllowedAsync(context, "inventory.valuation.summary.read", query.CompanyId, query.BranchId, query.WarehouseId, cancellationToken)) return InventoryOperationResult<InventoryValuationSummaryRecord>.Failure("forbidden");
        try
        {
            var result = await persistence.SummaryAsync(context, query, cancellationToken);
            return result.Succeeded && result.Value is not null
                ? InventoryOperationResult<InventoryValuationSummaryRecord>.Success(result.Value)
                : InventoryOperationResult<InventoryValuationSummaryRecord>.Failure(result.Code);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryValuationSummaryRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryFinanceValuationHandoffRecord>>> ListFinanceHandoffsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        if (!await IsQueryAllowedAsync(context, "inventory.valuation.finance-handoff.read", query.CompanyId, query.BranchId, query.WarehouseId, cancellationToken)) return InventoryOperationResult<IReadOnlyList<InventoryFinanceValuationHandoffRecord>>.Failure("forbidden");
        try { return InventoryOperationResult<IReadOnlyList<InventoryFinanceValuationHandoffRecord>>.Success(await persistence.ListFinanceHandoffsAsync(context, query, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryFinanceValuationHandoffRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryValuationExportRecord>> ExportAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        if (!await IsQueryAllowedAsync(context, "inventory.valuation.export", query.CompanyId, query.BranchId, query.WarehouseId, cancellationToken)) return InventoryOperationResult<InventoryValuationExportRecord>.Failure("forbidden");
        try { return InventoryOperationResult<InventoryValuationExportRecord>.Success(await persistence.ExportAsync(context, query, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryValuationExportRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryMovementValuationEventRecord>> CorrectAsync(InventoryRequestContext context, InventoryValuationCorrectionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OriginalValuationEventId == Guid.Empty || command.AuthoritativeSourceRevisionId == Guid.Empty || string.IsNullOrWhiteSpace(command.Reason)) return InventoryOperationResult<InventoryMovementValuationEventRecord>.Failure("validation_failed");
        try
        {
            var result = await persistence.CorrectAsync(context, command, cancellationToken);
            return result.Succeeded && result.Value is not null
                ? InventoryOperationResult<InventoryMovementValuationEventRecord>.Success(result.Value)
                : InventoryOperationResult<InventoryMovementValuationEventRecord>.Failure(result.Code);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryMovementValuationEventRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<bool> IsQueryAllowedAsync(InventoryRequestContext context, string operationId, Guid companyId, Guid? branchId, Guid? warehouseId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty || !authorization.IsCompanyAllowed(context, operationId, context.TenantId.Value, companyId, branchId)) return false;
        if (!warehouseId.HasValue) return true;
        var warehouse = await warehouses.FindAsync(context, warehouseId.Value, cancellationToken);
        return warehouse is { IsActive: true, CompanyId: var warehouseCompanyId } && warehouseCompanyId == companyId && warehouse.BranchId == branchId
            && authorization.IsAllowed(context, operationId, context.TenantId.Value, companyId, branchId, warehouseId.Value);
    }

    private static bool IsValidPolicy(InventoryValuationPolicyRequest request) =>
        request is not null
        && request.CompanyId != Guid.Empty
        && request.FunctionalCurrencyId != Guid.Empty
        && !string.IsNullOrWhiteSpace(request.FunctionalCurrencyCode)
        && request.EffectiveFrom != default
        && (request.EffectiveTo is null || request.EffectiveTo.Value >= request.EffectiveFrom)
        && request.UnitCostScale is > 0 and <= 12
        && request.AmountScale is > 0 and <= 12
        && request.ScopeMode is InventoryValuationScopeMode.WarehouseProductUom or InventoryValuationScopeMode.WarehouseProductUomTracking
        && request.RoundingMode is InventoryValuationRoundingMode.ToEven or InventoryValuationRoundingMode.AwayFromZero
        && string.Equals(request.GoodsReceiptCostBasis, "PurchaseOrderUnitPrice", StringComparison.Ordinal)
        && string.Equals(request.PositiveAdjustmentCostBasis, "CurrentMovingAverage", StringComparison.Ordinal)
        && request.SupplierReturnCostBasis is "CurrentMovingAverage" or "LinkedReceiptValuation";

    private static bool IsCurrentReconciliationQuery(InventoryValuationQuery query) =>
        query.Status is null
        && query.FromLedgerSequence is null
        && query.ToLedgerSequence is null
        && query.EffectiveFrom is null
        && query.EffectiveTo is null
        && query.SourceType is null
        && query.PolicyId is null
        && string.IsNullOrWhiteSpace(query.FunctionalCurrencyCode);
}

#pragma warning restore CS1591
