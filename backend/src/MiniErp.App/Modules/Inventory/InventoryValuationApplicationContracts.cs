#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.App.Modules.Inventory;

public sealed record InventoryValuationPolicyCommand(
    Guid Id,
    InventoryValuationPolicyRequest Request,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryValuationProcessCommand(
    Guid CompanyId,
    Guid? BranchId,
    Guid? WarehouseId,
    Guid? ProductId,
    Guid? UnitOfMeasureId,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryValuationCorrectionCommand(
    Guid OriginalValuationEventId,
    Guid AuthoritativeSourceRevisionId,
    string Reason,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryValuationQuery(
    Guid CompanyId,
    Guid? BranchId = null,
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    Guid? UnitOfMeasureId = null,
    string? TrackingIdentity = null,
    InventoryValuationEventStatus? Status = null,
    long? FromLedgerSequence = null,
    long? ToLedgerSequence = null,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    InventoryMovementSourceType? SourceType = null,
    Guid? PolicyId = null,
    string? FunctionalCurrencyCode = null);

public interface IInventoryValuationPersistence
{
    Task<InventoryPersistenceResult<InventoryValuationPolicyRecord>> CreatePolicyAsync(
        InventoryRequestContext context,
        InventoryValuationPolicyCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryValuationPolicyRecord>> ListPoliciesAsync(
        InventoryRequestContext context,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<InventoryPersistenceResult<InventoryValuationProcessResult>> ProcessAsync(
        InventoryRequestContext context,
        InventoryValuationProcessCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryValuationStateRecord>> ListStatesAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovementValuationEventRecord>> ListEventsAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryValuationReconciliationRecord>> ReconcileAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<InventoryPersistenceResult<InventoryValuationSummaryRecord>> SummaryAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryFinanceValuationHandoffRecord>> ListFinanceHandoffsAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<Guid?> ResolveFinanceHandoffCompanyIdAsync(
        InventoryRequestContext context,
        Guid handoffId,
        CancellationToken cancellationToken = default);

    Task<InventoryValuationExportRecord> ExportAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<InventoryPersistenceResult<InventoryMovementValuationEventRecord>> CorrectAsync(
        InventoryRequestContext context,
        InventoryValuationCorrectionCommand command,
        CancellationToken cancellationToken = default);
}

public interface IInventoryReportingReadPort
{
    Task<ReportingSourcePage<InventoryValuationStateRecord>> ListStatesReportingPageAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);

    Task<ReportingSourcePage<InventoryMovementValuationEventRecord>> ListEventsReportingPageAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableInventoryValuationPersistence : IInventoryValuationPersistence
{
    private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("Inventory valuation persistence is unavailable."));

    public Task<InventoryPersistenceResult<InventoryValuationPolicyRecord>> CreatePolicyAsync(InventoryRequestContext context, InventoryValuationPolicyCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryPersistenceResult<InventoryValuationPolicyRecord>>();
    public Task<IReadOnlyList<InventoryValuationPolicyRecord>> ListPoliciesAsync(InventoryRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryValuationPolicyRecord>>();
    public Task<InventoryPersistenceResult<InventoryValuationProcessResult>> ProcessAsync(InventoryRequestContext context, InventoryValuationProcessCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryPersistenceResult<InventoryValuationProcessResult>>();
    public Task<IReadOnlyList<InventoryValuationStateRecord>> ListStatesAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryValuationStateRecord>>();
    public Task<IReadOnlyList<InventoryMovementValuationEventRecord>> ListEventsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryMovementValuationEventRecord>>();
    public Task<IReadOnlyList<InventoryValuationReconciliationRecord>> ReconcileAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryValuationReconciliationRecord>>();
    public Task<InventoryPersistenceResult<InventoryValuationSummaryRecord>> SummaryAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<InventoryPersistenceResult<InventoryValuationSummaryRecord>>();
    public Task<IReadOnlyList<InventoryFinanceValuationHandoffRecord>> ListFinanceHandoffsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryFinanceValuationHandoffRecord>>();
    public Task<Guid?> ResolveFinanceHandoffCompanyIdAsync(InventoryRequestContext context, Guid handoffId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
    public Task<InventoryValuationExportRecord> ExportAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<InventoryValuationExportRecord>();
    public Task<InventoryPersistenceResult<InventoryMovementValuationEventRecord>> CorrectAsync(InventoryRequestContext context, InventoryValuationCorrectionCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryPersistenceResult<InventoryMovementValuationEventRecord>>();
}

public sealed record MovingWeightedAverageInput(
    InventoryMovementDirection Direction,
    decimal Quantity,
    decimal BaseUnitCost,
    decimal PriorQuantity,
    decimal PriorValue,
    int UnitCostScale,
    int AmountScale,
    InventoryValuationRoundingMode RoundingMode,
    decimal? PriorAverageUnitCost = null);

public sealed record MovingWeightedAverageOutput(
    decimal NewQuantity,
    decimal NewValue,
    decimal AverageUnitCost,
    decimal MovementValue,
    decimal FormulaMovementValue,
    decimal RoundingAdjustmentAmount);

public sealed record MovingWeightedAverageCorrectionInput(
    InventoryMovementDirection Direction,
    decimal Quantity,
    decimal ReversalValue,
    decimal PriorQuantity,
    decimal PriorValue,
    int UnitCostScale,
    int AmountScale,
    InventoryValuationRoundingMode RoundingMode,
    bool IsFullReversal = false,
    decimal? FormulaReversalValue = null,
    decimal? RoundingAdjustmentAmount = null);

public static class MovingWeightedAverageCalculator
{
    public static bool TryApply(MovingWeightedAverageInput input, out MovingWeightedAverageOutput output, out string error)
    {
        output = new(0m, 0m, 0m, 0m, 0m, 0m);
        error = string.Empty;
        if (input.Quantity <= 0m || input.PriorQuantity < 0m || input.PriorValue < 0m || input.BaseUnitCost < 0m)
        {
            error = "invalid_non_negative_valuation_input";
            return false;
        }

        // Quantity is a physical ledger fact. AmountScale controls monetary
        // values only; do not collapse physical quantities to currency
        // precision here.
        var priorQuantity = input.PriorQuantity;
        var priorValue = Round(input.PriorValue, input.AmountScale, input.RoundingMode);
        var quantity = input.Quantity;
        var baseUnitCost = Round(input.BaseUnitCost, input.UnitCostScale, input.RoundingMode);
        decimal newQuantity;
        decimal movementValue;
        decimal formulaMovementValue;
        decimal roundingAdjustmentAmount;
        decimal newValue;

        if (input.Direction == InventoryMovementDirection.Inbound)
        {
            formulaMovementValue = Round(quantity * baseUnitCost, input.AmountScale, input.RoundingMode);
            movementValue = formulaMovementValue;
            roundingAdjustmentAmount = 0m;
            newQuantity = priorQuantity + quantity;
            newValue = Round(priorValue + movementValue, input.AmountScale, input.RoundingMode);
        }
        else if (input.Direction == InventoryMovementDirection.Outbound)
        {
            if (quantity > priorQuantity)
            {
                error = "valuation_would_make_quantity_negative";
                return false;
            }

            if (input.PriorAverageUnitCost is null)
            {
                error = "prior_moving_average_required";
                return false;
            }

            var priorAverageUnitCost = Round(input.PriorAverageUnitCost.Value, input.UnitCostScale, input.RoundingMode);
            formulaMovementValue = Round(quantity * priorAverageUnitCost, input.AmountScale, input.RoundingMode);
            newQuantity = priorQuantity - quantity;
            if (newQuantity == 0m)
            {
                // Full depletion closes the entire stored value. The difference
                // from the rounded unit-cost formula is immutable evidence, not
                // an unexplained loss hidden in the resulting state.
                movementValue = priorValue;
                roundingAdjustmentAmount = Round(priorValue - formulaMovementValue, input.AmountScale, input.RoundingMode);
                newValue = 0m;
            }
            else
            {
                movementValue = formulaMovementValue;
                roundingAdjustmentAmount = 0m;
                newValue = Round(priorValue - movementValue, input.AmountScale, input.RoundingMode);
            }
        }
        else
        {
            error = "movement_direction_required";
            return false;
        }

        if (newQuantity < 0m || newValue < 0m)
        {
            error = "valuation_would_make_state_negative";
            return false;
        }

        if (newQuantity == 0m)
        {
            newValue = 0m;
        }

        var average = newQuantity == 0m
            ? 0m
            : Round(newValue / newQuantity, input.UnitCostScale, input.RoundingMode);
        output = new(newQuantity, newValue, average, movementValue, formulaMovementValue, roundingAdjustmentAmount);
        return true;
    }

    public static bool TryApplyCorrection(MovingWeightedAverageCorrectionInput input, out MovingWeightedAverageOutput output, out string error)
    {
        output = new(0m, 0m, 0m, 0m, 0m, 0m);
        error = string.Empty;
        if (input.Quantity <= 0m || input.ReversalValue < 0m || input.PriorQuantity < 0m || input.PriorValue < 0m)
        {
            error = "invalid_non_negative_correction_input";
            return false;
        }

        // Correction quantities follow the same physical-ledger precision as
        // ordinary movements. Only reversal values are monetary and may use
        // AmountScale.
        var priorQuantity = input.PriorQuantity;
        var priorValue = Round(input.PriorValue, input.AmountScale, input.RoundingMode);
        var quantity = input.Quantity;
        var reversalValue = input.IsFullReversal
            ? input.ReversalValue
            : Round(input.ReversalValue, input.AmountScale, input.RoundingMode);
        var formulaReversalValue = input.FormulaReversalValue.HasValue
            ? Round(input.FormulaReversalValue.Value, input.AmountScale, input.RoundingMode)
            : reversalValue;
        var roundingAdjustmentAmount = input.RoundingAdjustmentAmount.HasValue
            ? Round(input.RoundingAdjustmentAmount.Value, input.AmountScale, input.RoundingMode)
            : Round(reversalValue - formulaReversalValue, input.AmountScale, input.RoundingMode);
        var newQuantity = input.Direction == InventoryMovementDirection.Inbound
            ? priorQuantity + quantity
            : input.Direction == InventoryMovementDirection.Outbound
                ? priorQuantity - quantity
                : -1m;
        var newValue = input.Direction == InventoryMovementDirection.Inbound
            ? Round(priorValue + reversalValue, input.AmountScale, input.RoundingMode)
            : input.Direction == InventoryMovementDirection.Outbound
                ? Round(priorValue - reversalValue, input.AmountScale, input.RoundingMode)
                : -1m;

        if (newQuantity < 0m || newValue < 0m)
        {
            error = newQuantity < 0m
                ? "correction_would_make_quantity_negative"
                : "correction_would_make_value_negative";
            return false;
        }

        if (newQuantity == 0m && newValue != 0m)
        {
            // A correction cannot silently absorb value drift that would
            // leave an impossible zero-quantity/non-zero-value state. The
            // processing path records the correction as Blocked and stops
            // only this valuation scope for explicit remediation.
            error = "correction_would_orphan_residual_value";
            return false;
        }

        var average = newQuantity == 0m
            ? 0m
            : Round(newValue / newQuantity, input.UnitCostScale, input.RoundingMode);
        // A correction is an explicit reversal event. Keep the physical state
        // effect directionally clear in immutable evidence: an outbound
        // reversal removes value, while an inbound reversal restores it.
        var signedMovementValue = input.Direction == InventoryMovementDirection.Outbound
            ? -reversalValue
            : reversalValue;
        output = new(newQuantity, newValue, average, signedMovementValue, formulaReversalValue, roundingAdjustmentAmount);
        return true;
    }

    public static decimal Round(decimal value, int scale, InventoryValuationRoundingMode mode)
    {
        if (scale is < 0 or > 28) throw new ArgumentOutOfRangeException(nameof(scale));
        return Math.Round(value, scale, mode == InventoryValuationRoundingMode.AwayFromZero
            ? MidpointRounding.AwayFromZero
            : MidpointRounding.ToEven);
    }
}

#pragma warning restore CS1591
