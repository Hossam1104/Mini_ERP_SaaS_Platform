using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class InventoryValuationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WarehouseId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid ProductId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Moving_weighted_average_uses_decimal_inbound_and_outbound_formulas()
    {
        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Inbound, 10m, 10m, 0m, 0m, 8, 8, InventoryValuationRoundingMode.ToEven),
            out var first,
            out var firstError), firstError);
        Assert.Equal(10m, first.NewQuantity);
        Assert.Equal(100m, first.NewValue);
        Assert.Equal(10m, first.AverageUnitCost);

        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Inbound, 5m, 20m, first.NewQuantity, first.NewValue, 8, 8, InventoryValuationRoundingMode.ToEven),
            out var second,
            out var secondError), secondError);
        Assert.Equal(15m, second.NewQuantity);
        Assert.Equal(200m, second.NewValue);
        Assert.Equal(13.33333333m, second.AverageUnitCost);

        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Outbound, 5m, 0m, second.NewQuantity, second.NewValue, 8, 8, InventoryValuationRoundingMode.ToEven, second.AverageUnitCost),
            out var issue,
            out var issueError), issueError);
        Assert.Equal(10m, issue.NewQuantity);
        Assert.Equal(133.33333335m, issue.NewValue);
        Assert.Equal(13.33333334m, issue.AverageUnitCost);
    }

    [Fact]
    public void Moving_weighted_average_rejects_negative_state_and_over_issue()
    {
        Assert.False(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Outbound, 11m, 0m, 10m, 100m, 8, 8, InventoryValuationRoundingMode.ToEven),
            out _,
            out var error));
        Assert.Equal("valuation_would_make_quantity_negative", error);

        Assert.False(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Inbound, 1m, -1m, 0m, 0m, 8, 8, InventoryValuationRoundingMode.ToEven),
            out _,
            out error));
        Assert.Equal("invalid_non_negative_valuation_input", error);
    }

    [Fact]
    public void Full_depletion_closes_stored_value_and_preserves_rounding_bridge()
    {
        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(
                InventoryMovementDirection.Outbound,
                3m,
                0m,
                3m,
                100m,
                2,
                4,
                InventoryValuationRoundingMode.ToEven,
                33.33m),
            out var output,
            out var error), error);

        Assert.Equal(99.99m, output.FormulaMovementValue);
        Assert.Equal(0.01m, output.RoundingAdjustmentAmount);
        Assert.Equal(100m, output.MovementValue);
        Assert.Equal(0m, output.NewQuantity);
        Assert.Equal(0m, output.NewValue);
        Assert.Equal(0m, output.AverageUnitCost);
    }

    [Fact]
    public void Partial_outbound_keeps_normal_formula_without_closeout_adjustment()
    {
        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(
                InventoryMovementDirection.Outbound,
                1m,
                0m,
                3m,
                100m,
                2,
                4,
                InventoryValuationRoundingMode.ToEven,
                33.33m),
            out var output,
            out var error), error);

        Assert.Equal(33.33m, output.FormulaMovementValue);
        Assert.Equal(33.33m, output.MovementValue);
        Assert.Equal(0m, output.RoundingAdjustmentAmount);
        Assert.Equal(2m, output.NewQuantity);
        Assert.Equal(66.67m, output.NewValue);
    }

    [Fact]
    public async Task Tracking_scopes_isolate_known_policy_failure_and_successors()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 10m, "USD", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, "LOT-A"),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), "LOT-B"),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 30m, "SAR", new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(2), "LOT-A"),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 40m, "SAR", new DateOnly(2026, 1, 4), DateTimeOffset.UtcNow.AddSeconds(3), "LOT-B"));
        var persistence = new InventoryValuationPersistence(options, null, null, new TestExchangeRatePersistence());
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), scope: InventoryValuationScopeMode.WarehouseProductUomTracking), "tracking-isolation-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("tracking-isolation-process"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(2, result.Value!.AppliedCount);
        Assert.Equal(2, result.Value.PendingCount);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal(InventoryValuationEventStatus.Pending, events[0].Status);
        Assert.Equal("exchange_rate_missing", events[0].PendingReason);
        Assert.Equal(InventoryValuationEventStatus.Applied, events[1].Status);
        Assert.Equal(InventoryValuationEventStatus.Pending, events[2].Status);
        Assert.Equal("pending_predecessor", events[2].StatusCode);
        Assert.Equal(InventoryValuationEventStatus.Applied, events[3].Status);
        var lotB = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId, TrackingIdentity: "LOT-B")));
        Assert.Equal(2m, lotB.Quantity);
        Assert.Equal(60m, lotB.Value);
    }

    [Fact]
    public async Task Non_tracking_known_policy_failure_stops_the_combined_cost_pool()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 10m, "USD", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, "LOT-A"),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), "LOT-B"));
        var persistence = new InventoryValuationPersistence(options, null, null, new TestExchangeRatePersistence());
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), scope: InventoryValuationScopeMode.WarehouseProductUom), "nontracking-failure-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("nontracking-failure-process"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(0, result.Value!.AppliedCount);
        Assert.Equal(2, result.Value.PendingCount);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal("exchange_rate_missing", events[0].PendingReason);
        Assert.Equal("pending_predecessor", events[1].StatusCode);
        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Null(state.TrackingIdentity);
        Assert.Equal(0m, state.Quantity);
        Assert.Equal(0m, state.Value);
    }

    [Fact]
    public async Task Targeted_opening_valuation_processes_only_its_movement_and_blocks_pending_predecessor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var start = DateTimeOffset.UtcNow;
        var predecessor = Guid.NewGuid();
        var target = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        await AddMovementsAsync(options, context,
            CustomMovement(predecessor, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 10m, "USD", new DateOnly(2026, 1, 1), start, "LOT-A"),
            CustomMovement(target, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", new DateOnly(2026, 1, 2), start.AddSeconds(1), "LOT-A"),
            CustomMovement(unrelated, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 30m, "SAR", new DateOnly(2026, 1, 3), start.AddSeconds(2), "LOT-B"));
        var persistence = new InventoryValuationPersistence(options, null, null, new TestExchangeRatePersistence());
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), scope: InventoryValuationScopeMode.WarehouseProductUomTracking), "targeted-opening-policy");

        var blocked = await persistence.ProcessAsync(context, ProcessCommand("targeted-opening-blocked", fingerprint: "targeted-opening-blocked") with { MovementIds = [target] });
        Assert.False(blocked.Succeeded);
        Assert.Equal("pending_predecessor", blocked.Code);
        Assert.Empty(await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId)));

        var isolated = await persistence.ProcessAsync(context, ProcessCommand("targeted-opening-isolated", fingerprint: "targeted-opening-isolated") with { MovementIds = [unrelated] });
        Assert.True(isolated.Succeeded, isolated.Code);
        Assert.Equal(1, isolated.Value!.AppliedCount);
        var events = await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId));
        Assert.Equal(unrelated, Assert.Single(events).MovementId);
    }

    [Fact]
    public async Task Valuation_correction_reverses_original_evidence_append_only()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var originalMovementId = Guid.NewGuid();
        var correctionMovementId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.Add(Movement(originalMovementId, new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow, 10m));
            db.StockMovements.Add(new InventoryStockMovementEntity(
                new TenantId(TenantId),
                correctionMovementId,
                CompanyId,
                null,
                WarehouseId,
                "WH-A",
                "Warehouse A",
                ProductId,
                "SKU-A",
                "Product A",
                UnitId,
                "EA",
                InventoryMovementDirection.Outbound,
                5m,
                null,
                null,
                null,
                InventoryMovementSourceType.Correction,
                Guid.NewGuid(),
                Guid.NewGuid(),
                originalMovementId,
                new DateOnly(2026, 8, 23),
                ActorId,
                "valuation-correction",
                DateTimeOffset.UtcNow.AddSeconds(1)));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(
                Guid.NewGuid(),
                new InventoryValuationPolicyRequest(
                    CompanyId,
                    Guid.NewGuid(),
                    "SAR",
                    InventoryValuationScopeMode.WarehouseProductUom,
                    new DateOnly(2026, 1, 1),
                    null,
                    8,
                    8,
                    InventoryValuationRoundingMode.ToEven,
                    "PurchaseOrderUnitPrice",
                    "CurrentMovingAverage",
                    "CurrentMovingAverage"),
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-correction-policy",
                "valuation-correction-policy-key",
                "valuation-correction-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        var result = await persistence.ProcessAsync(
            context,
            new InventoryValuationProcessCommand(
                CompanyId,
                null,
                WarehouseId,
                ProductId,
                UnitId,
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-correction-process",
                "valuation-correction-process-key",
                "valuation-correction-process-fingerprint"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(2, result.Value!.AppliedCount);
        var events = await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId));
        Assert.Equal(2, events.Count);
        var original = Assert.Single(events, item => item.MovementId == originalMovementId);
        var correction = Assert.Single(events, item => item.MovementId == correctionMovementId);
        Assert.Equal(InventoryValuationEventStatus.Applied, correction.Status);
        Assert.Equal(original.Id, correction.CorrectionOfValuationEventId);
        Assert.Equal(5m, correction.NewQuantity);
        Assert.Equal(50m, correction.NewValue);
        Assert.Equal(-50m, correction.MovementValue);
        Assert.Equal(2L, correction.LedgerSequence);
    }

    [Fact]
    public async Task Drifted_correction_blocks_without_corrupting_scope_and_unrelated_company_pool_continues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var unrelatedProductId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();
        var correctionId = Guid.NewGuid();
        var successorId = Guid.NewGuid();
        var postedAt = DateTimeOffset.UtcNow;
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 10m, 10m, "SAR", new DateOnly(2026, 1, 1), postedAt),
            CustomMovement(adjustmentId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 10m, 999m, "SAR", new DateOnly(2026, 1, 2), postedAt.AddSeconds(1), sourceType: InventoryMovementSourceType.StockAdjustment),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 20m, 20m, "SAR", new DateOnly(2026, 1, 3), postedAt.AddSeconds(2)),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 30m, null, null, new DateOnly(2026, 1, 4), postedAt.AddSeconds(3), sourceType: InventoryMovementSourceType.StockIssue),
            CustomMovement(correctionId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 10m, null, null, new DateOnly(2026, 1, 5), postedAt.AddSeconds(4), sourceType: InventoryMovementSourceType.Correction, correctionOfMovementId: adjustmentId),
            CustomMovement(successorId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 30m, "SAR", new DateOnly(2026, 1, 6), postedAt.AddSeconds(5)),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, unrelatedProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 50m, "SAR", new DateOnly(2026, 1, 6), postedAt.AddSeconds(6)));

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "drifted-correction-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("drifted-correction-process"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(5, result.Value!.AppliedCount);
        Assert.Equal(1, result.Value.PendingCount);
        Assert.Equal(1, result.Value.BlockedCount);

        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        var originalAdjustment = Assert.Single(events, item => item.MovementId == adjustmentId);
        Assert.Equal(InventoryValuationEventStatus.Applied, originalAdjustment.Status);
        Assert.Equal(100m, originalAdjustment.MovementValue);

        var correction = Assert.Single(events, item => item.MovementId == correctionId);
        Assert.Equal(InventoryValuationEventStatus.Blocked, correction.Status);
        Assert.Equal("correction_would_orphan_residual_value", correction.StatusCode);
        Assert.Equal("correction_would_orphan_residual_value", correction.PendingReason);
        Assert.Equal(10m, correction.PriorQuantity);
        Assert.Equal(150m, correction.PriorValue);
        Assert.Equal(10m, correction.Quantity);
        Assert.Equal(10m, correction.NewQuantity);
        Assert.Equal(150m, correction.NewValue);

        var successor = Assert.Single(events, item => item.MovementId == successorId);
        Assert.Equal(InventoryValuationEventStatus.Pending, successor.Status);
        Assert.Equal("pending_predecessor", successor.StatusCode);

        var affectedState = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId, ProductId: ProductId)));
        Assert.Equal(10m, affectedState.Quantity);
        Assert.Equal(150m, affectedState.Value);
        var unrelatedState = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId, ProductId: unrelatedProductId)));
        Assert.Equal(1m, unrelatedState.Quantity);
        Assert.Equal(50m, unrelatedState.Value);
    }

    [Fact]
    public void Moving_weighted_average_correction_rejects_drifted_zero_quantity_residual_value()
    {
        Assert.False(MovingWeightedAverageCalculator.TryApplyCorrection(
            new MovingWeightedAverageCorrectionInput(
                InventoryMovementDirection.Outbound,
                10m,
                100m,
                10m,
                150m,
                2,
                2,
                InventoryValuationRoundingMode.ToEven,
                IsFullReversal: true),
            out _,
            out var error));

        Assert.Equal("correction_would_orphan_residual_value", error);
    }

    [Fact]
    public void Moving_weighted_average_correction_preserves_fractional_physical_quantity()
    {
        Assert.True(MovingWeightedAverageCalculator.TryApplyCorrection(
            new MovingWeightedAverageCorrectionInput(
                InventoryMovementDirection.Outbound,
                0.001m,
                0.10m,
                1.005m,
                100.50m,
                2,
                2,
                InventoryValuationRoundingMode.ToEven),
            out var output,
            out var error), error);

        Assert.Equal(1.004m, output.NewQuantity);
        Assert.Equal(100.40m, output.NewValue);
        Assert.Equal(100m, output.AverageUnitCost);
        Assert.Equal(-0.10m, output.MovementValue);
        Assert.Equal(0.10m, output.FormulaMovementValue);
        Assert.Equal(0m, output.RoundingAdjustmentAmount);
    }

    [Fact]
    public async Task Fractional_stock_adjustment_correction_preserves_quantity_and_reconciles()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var adjustmentId = Guid.NewGuid();
        var correctionId = Guid.NewGuid();
        var postedAt = DateTimeOffset.UtcNow;
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1.004m, 100m, "SAR", new DateOnly(2026, 1, 1), postedAt),
            CustomMovement(adjustmentId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 0.001m, null, null, new DateOnly(2026, 1, 2), postedAt.AddSeconds(1), sourceType: InventoryMovementSourceType.StockAdjustment),
            CustomMovement(correctionId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 0.001m, null, null, new DateOnly(2026, 1, 3), postedAt.AddSeconds(2), sourceType: InventoryMovementSourceType.Correction, correctionOfMovementId: adjustmentId));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "fractional-correction-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("fractional-correction-process"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(3, result.Value!.AppliedCount);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        var adjustment = Assert.Single(events, item => item.MovementId == adjustmentId);
        Assert.Equal(InventoryValuationEventStatus.Applied, adjustment.Status);
        Assert.Equal(0.001m, adjustment.Quantity);
        Assert.Equal(1.004m, adjustment.PriorQuantity);
        Assert.Equal(1.005m, adjustment.NewQuantity);
        Assert.Equal(0.10m, adjustment.MovementValue);
        Assert.Equal(100.50m, adjustment.NewValue);

        var correction = Assert.Single(events, item => item.MovementId == correctionId);
        Assert.Equal(InventoryValuationEventStatus.Applied, correction.Status);
        Assert.Equal(1.005m, correction.PriorQuantity);
        Assert.Equal(0.001m, correction.Quantity);
        Assert.Equal(1.004m, correction.NewQuantity);
        Assert.Equal(-0.10m, correction.MovementValue);
        Assert.Equal(100.40m, correction.NewValue);
        Assert.Equal(correction.PriorQuantity - correction.Quantity, correction.NewQuantity);

        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(1.004m, state.Quantity);
        Assert.Equal(100.40m, state.Value);
        Assert.Equal(100m, state.AverageUnitCost);

        var handoff = Assert.Single(
            await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(CompanyId)),
            item => item.MovementId == correctionId);
        Assert.Equal(0.001m, handoff.Quantity);
        Assert.Equal(InventoryMovementDirection.Outbound, handoff.Direction);
        Assert.Equal(100m, handoff.BaseUnitCost);
        Assert.Equal(0.10m, handoff.BaseAmount);
        Assert.Equal(-0.10m, handoff.SignedBaseAmount);

        var reconciliation = Assert.Single(await persistence.ReconcileAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(0m, reconciliation.QuantityDifference);
        Assert.Equal(InventoryValuationReconciliationStatus.Reconciled, reconciliation.Status);
    }

    [Fact]
    public async Task Fractional_inbound_preserves_physical_quantity_and_finance_amount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1.005m, 100m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "fractional-inbound-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("fractional-inbound-process"));

        Assert.True(result.Succeeded, result.Code);
        var @event = Assert.Single(await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(1.005m, @event.Quantity);
        Assert.Equal(0m, @event.PriorQuantity);
        Assert.Equal(1.005m, @event.NewQuantity);
        Assert.Equal(100.50m, @event.MovementValue);
        Assert.Equal(100.50m, @event.FormulaMovementValue);
        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(1.005m, state.Quantity);
        Assert.Equal(100.50m, state.Value);
        var handoff = Assert.Single(await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(1.005m, handoff.Quantity);
        Assert.Equal(100m, handoff.BaseUnitCost);
        Assert.Equal(100.50m, handoff.BaseAmount);
        Assert.Equal(100.50m, handoff.SignedBaseAmount);
        Assert.Equal(0m, handoff.RoundingAdjustmentAmount);
    }

    [Fact]
    public async Task Fractional_outbound_preserves_physical_quantity_and_money_value()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1.005m, 100m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 0.005m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.StockIssue));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "fractional-outbound-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("fractional-outbound-process"));

        Assert.True(result.Succeeded, result.Code);
        var @event = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).Single(item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(0.005m, @event.Quantity);
        Assert.Equal(1.005m, @event.PriorQuantity);
        Assert.Equal(1.000m, @event.NewQuantity);
        Assert.Equal(0.50m, @event.MovementValue);
        Assert.Equal(0.50m, @event.FormulaMovementValue);
        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(1.000m, state.Quantity);
        Assert.Equal(100.00m, state.Value);
    }

    [Fact]
    public async Task Fractional_full_depletion_closes_quantity_value_and_average_to_zero()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1.005m, 100m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 1.005m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.StockIssue));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "fractional-closeout-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("fractional-closeout-process"));

        Assert.True(result.Succeeded, result.Code);
        var @event = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).Single(item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(100.50m, @event.FormulaMovementValue);
        Assert.Equal(0m, @event.RoundingAdjustmentAmount);
        Assert.Equal(100.50m, @event.MovementValue);
        Assert.Equal(0m, @event.NewQuantity);
        Assert.Equal(0m, @event.NewValue);
        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(0m, state.Quantity);
        Assert.Equal(0m, state.Value);
        Assert.Equal(0m, state.AverageUnitCost);
    }

    [Fact]
    public async Task Reconciliation_detects_fractional_quantity_difference_without_money_tolerance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1.005m, 100m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "fractional-reconciliation-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("fractional-reconciliation-process"))).Succeeded);

        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE ValuationStates SET Quantity = 1.000");
        }

        var row = Assert.Single(await persistence.ReconcileAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(0.005m, row.QuantityDifference);
        Assert.Equal(InventoryValuationReconciliationStatus.QuantityMismatch, row.Status);
        Assert.Equal("physical_quantity_differs_from_valued_state", row.DifferenceReason);
    }

    [Fact]
    public async Task Valuation_event_quantity_arithmetic_is_self_consistent_at_ledger_precision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1.005m, 100m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 0.005m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.StockIssue));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "fractional-arithmetic-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("fractional-arithmetic-process"))).Succeeded);

        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        var inbound = Assert.Single(events, item => item.Direction == InventoryMovementDirection.Inbound);
        var outbound = Assert.Single(events, item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(inbound.PriorQuantity + inbound.Quantity, inbound.NewQuantity);
        Assert.Equal(outbound.PriorQuantity - outbound.Quantity, outbound.NewQuantity);
        Assert.Equal(inbound.NewQuantity, outbound.PriorQuantity);
        Assert.Equal(1.005m, outbound.PriorQuantity);
        Assert.Equal(0.005m, outbound.Quantity);
        Assert.Equal(1.000m, outbound.NewQuantity);
    }

    [Fact]
    public async Task Fractional_finance_handoff_reconstructs_quantity_times_unit_cost()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1.005m, 100m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 2), "fractional-finance-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("fractional-finance-process"))).Succeeded);

        var @event = Assert.Single(await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId)));
        var handoff = Assert.Single(await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(100.50m, Math.Round(@event.Quantity * @event.BaseUnitCost!.Value, 2, MidpointRounding.ToEven));
        Assert.Equal(100.50m, handoff.BaseAmount);
        Assert.Equal(@event.Quantity, handoff.Quantity);
        Assert.Equal(@event.BaseUnitCost, handoff.BaseUnitCost);
        Assert.Equal(0m, handoff.RoundingAdjustmentAmount);
    }

    [Fact]
    public async Task Company_ledger_sequence_is_durable_and_independent_of_timestamps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.AddRange(
                Movement(Guid.NewGuid(), new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow.AddMinutes(1), 10m),
                Movement(Guid.NewGuid(), new DateOnly(2026, 8, 1), DateTimeOffset.UtcNow.AddMinutes(-1), 20m));
            await db.SaveChangesAsync();
        }

        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            var rows = await db.StockMovements.AsNoTracking().OrderBy(item => item.LedgerSequence).ToArrayAsync();
            Assert.Equal([1L, 2L], rows.Select(item => item.LedgerSequence).ToArray());
            var anchor = await db.CompanyLedgerSequenceAnchors.AsNoTracking().SingleAsync(item => item.CompanyId == CompanyId);
            Assert.Equal(3L, anchor.NextSequence);
        }
    }

    [Fact]
    public async Task Valuation_process_appends_explainable_evidence_and_truthful_finance_handoff()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var movementId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.Add(Movement(movementId, new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow, 10m));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(
                Guid.NewGuid(),
                new InventoryValuationPolicyRequest(
                    CompanyId,
                    Guid.NewGuid(),
                    "SAR",
                    InventoryValuationScopeMode.WarehouseProductUom,
                    new DateOnly(2026, 1, 1),
                    null,
                    8,
                    8,
                    InventoryValuationRoundingMode.ToEven,
                    "PurchaseOrderUnitPrice",
                    "CurrentMovingAverage",
                    "CurrentMovingAverage"),
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-policy",
                "valuation-policy-key",
                "valuation-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        var result = await persistence.ProcessAsync(
            context,
            new InventoryValuationProcessCommand(
                CompanyId,
                null,
                WarehouseId,
                ProductId,
                UnitId,
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-process",
                "valuation-process-key",
                "valuation-process-fingerprint"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(1, result.Value!.AppliedCount);
        Assert.Equal(0, result.Value.PendingCount);
        var events = await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId));
        var evidence = Assert.Single(events);
        Assert.Equal(InventoryValuationEventStatus.Applied, evidence.Status);
        Assert.Equal(movementId, evidence.MovementId);
        Assert.Equal(InventoryMovementSourceType.OpeningBalance, evidence.SourceType);
        Assert.Equal(10m, evidence.TransactionUnitCost);
        Assert.Equal("SAR", evidence.TransactionCurrencyCode);
        Assert.Equal(10m, evidence.BaseUnitCost);
        Assert.Equal(100m, evidence.MovementValue);
        Assert.Equal(100m, evidence.NewValue);

        var handoffs = await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(CompanyId));
        var handoff = Assert.Single(handoffs);
        Assert.Equal(InventoryFinanceValuationHandoffStatus.ReadyForFinance, handoff.Status);
        Assert.Equal(evidence.Id, handoff.ValuationEvidenceId);
        Assert.Equal(InventoryMovementSourceType.OpeningBalance, handoff.SourceType);
        Assert.Equal("inventory-valuation-finance.v1", handoff.ContractVersion);
        Assert.DoesNotContain("Journal", handoff.ContractVersion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Valuation_export_is_bounded_filtered_and_audited()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var movementId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.Add(Movement(movementId, new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow, 10m));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(
                Guid.NewGuid(),
                new InventoryValuationPolicyRequest(CompanyId, Guid.NewGuid(), "SAR", InventoryValuationScopeMode.WarehouseProductUom, new DateOnly(2026, 1, 1), null, 8, 8, InventoryValuationRoundingMode.ToEven, "PurchaseOrderUnitPrice", "CurrentMovingAverage", "CurrentMovingAverage"),
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-export-policy",
                "valuation-export-policy-key",
                "valuation-export-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        await persistence.ProcessAsync(
            context,
            new InventoryValuationProcessCommand(CompanyId, null, WarehouseId, ProductId, UnitId, ActorId, DateTimeOffset.UtcNow, "valuation-export-process", "valuation-export-process-key", "valuation-export-process-fingerprint"));

        var exported = await persistence.ExportAsync(context, new InventoryValuationQuery(CompanyId, WarehouseId: WarehouseId, FromLedgerSequence: 1, ToLedgerSequence: 1));
        Assert.EndsWith(".csv", exported.FileName, StringComparison.Ordinal);
        Assert.Equal("text/csv; charset=utf-8", exported.ContentType);
        Assert.Contains("# export=inventory-valuation", exported.Content, StringComparison.Ordinal);
        Assert.Contains("# filters=", exported.Content, StringComparison.Ordinal);
        Assert.Contains("# freshness=", exported.Content, StringComparison.Ordinal);
        Assert.Contains(movementId.ToString("D"), exported.Content, StringComparison.Ordinal);

        await using var auditDb = new InventoryDbContext(options, context.TenantContext);
        Assert.Contains(await auditDb.Audit.AsNoTracking().ToArrayAsync(), item => item.OperationId == "inventory.valuation.export");
    }

    [Fact]
    public void Valuation_contract_keeps_correction_and_source_revision_as_append_only_seams()
    {
        var eventType = typeof(InventoryMovementValuationEventRecord);
        var correction = eventType.GetProperty(nameof(InventoryMovementValuationEventRecord.CorrectionOfValuationEventId));
        var sourceRevision = eventType.GetProperty(nameof(InventoryMovementValuationEventRecord.SourceRevisionId));
        Assert.NotNull(correction);
        Assert.NotNull(sourceRevision);
        Assert.Contains("SourceDocumentId", eventType.GetProperties().Select(item => item.Name));
        Assert.Contains("SourceLineId", eventType.GetProperties().Select(item => item.Name));
        Assert.Contains("ValuationEvidenceId", typeof(InventoryFinanceValuationHandoffRecord).GetProperties().Select(item => item.Name));
    }

    [Fact]
    public void Mutation_contract_has_no_as_of_or_public_tracking_selector_and_policy_has_no_hidden_defaults()
    {
        Assert.DoesNotContain(typeof(InventoryValuationProcessRequest).GetProperties(), item => item.Name is "AsOfDate" or "TrackingIdentity");
        Assert.DoesNotContain(typeof(InventoryValuationProcessCommand).GetProperties(), item => item.Name is "AsOfDate" or "TrackingIdentity");
        var policyConstructor = typeof(InventoryValuationPolicyRequest).GetConstructors().Single();
        Assert.All(policyConstructor.GetParameters(), parameter => Assert.False(parameter.IsOptional));
    }

    [Fact]
    public async Task Ledger_sequence_beats_effective_date_and_backdated_evidence_is_explicit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var first = CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 10m, 10m, "SAR", new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow.AddMinutes(2));
        var second = CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 5m, 20m, "SAR", new DateOnly(2026, 8, 1), DateTimeOffset.UtcNow.AddMinutes(1));
        await AddMovementsAsync(options, context, first, second);
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "sequence-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("sequence-process"));

        Assert.True(result.Succeeded, result.Code);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal([1L, 2L], events.Select(item => item.LedgerSequence).ToArray());
        Assert.Equal(first.Id, events[0].MovementId);
        Assert.Equal(second.Id, events[1].MovementId);
        Assert.True(events[1].IsBackdated);
        Assert.Equal(200m, events[1].NewValue);
    }

    [Fact]
    public async Task Non_tracking_pool_is_not_split_by_tracking_identity_but_tracking_policy_is_independent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var nonTrackingMovements = new[]
        {
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 10m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, "LOT-A"),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), "LOT-B"),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 30m, "SAR", new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(2), "LOT-A"),
        };
        await AddMovementsAsync(options, context, nonTrackingMovements);
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), scope: InventoryValuationScopeMode.WarehouseProductUom), "pool-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("pool-process"))).Succeeded);
        var pooledStates = await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId));
        Assert.Single(pooledStates);
        Assert.Null(pooledStates[0].TrackingIdentity);
        Assert.Equal(3m, pooledStates[0].Quantity);

        var trackingCompany = Guid.NewGuid();
        var trackingProduct = Guid.NewGuid();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), trackingCompany, WarehouseId, trackingProduct, UnitId, InventoryMovementDirection.Inbound, 1m, 10m, "SAR", new DateOnly(2026, 2, 1), DateTimeOffset.UtcNow, "LOT-A"),
            CustomMovement(Guid.NewGuid(), trackingCompany, WarehouseId, trackingProduct, UnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", new DateOnly(2026, 2, 2), DateTimeOffset.UtcNow.AddSeconds(1), "LOT-B"));
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 2, 1), scope: InventoryValuationScopeMode.WarehouseProductUomTracking, companyId: trackingCompany), "tracking-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("tracking-process", productId: trackingProduct, companyId: trackingCompany))).Succeeded);
        var trackingStates = await persistence.ListStatesAsync(context, new InventoryValuationQuery(trackingCompany, ProductId: trackingProduct));
        Assert.Equal(2, trackingStates.Count);
        Assert.Equal(["LOT-A", "LOT-B"], trackingStates.OrderBy(item => item.TrackingIdentity).Select(item => item.TrackingIdentity!).ToArray());
    }

    [Fact]
    public async Task Missing_policy_is_durable_and_blocks_later_same_pool_movement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 10m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, "LOT-A"),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(1), "LOT-B"));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 2)), "future-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("missing-policy-process"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(0, result.Value!.AppliedCount);
        Assert.Equal(2, result.Value.PendingCount);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal("valuation_policy_not_configured", events[0].StatusCode);
        Assert.Equal("pending_predecessor", events[1].StatusCode);
        await using var db = new InventoryDbContext(options, context.TenantContext);
        Assert.Equal(2, await db.MovementValuationEvents.CountAsync());
    }

    [Fact]
    public async Task Later_policy_coverage_allows_pending_predecessor_then_successor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 10m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(1)));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 31)), "successor-policy");
        var first = await persistence.ProcessAsync(context, ProcessCommand("coverage-before"));
        Assert.Equal(2, first.Value!.PendingCount);

        var predecessor = await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1)), "predecessor-policy");
        var second = await persistence.ProcessAsync(context, ProcessCommand("coverage-after"));

        Assert.True(second.Succeeded, second.Code);
        Assert.Equal(2, second.Value!.AppliedCount);
        Assert.Equal(2, predecessor.VersionNumber);
        var states = await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId));
        Assert.Equal(2m, states.Single().Quantity);
        Assert.Equal(30m, states.Single().Value);
    }

    [Fact]
    public async Task Policy_version_increments_and_compatible_transition_carries_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 2m, 10m, "SAR", new DateOnly(2026, 8, 2), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 2m, 20m, "SAR", new DateOnly(2026, 9, 2), DateTimeOffset.UtcNow.AddSeconds(1)));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var firstPolicy = await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), "version-one");
        var secondPolicy = await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 9, 1)), "version-two");
        var process = await persistence.ProcessAsync(context, ProcessCommand("compatible-transition"));

        Assert.True(process.Succeeded, process.Code);
        Assert.Equal(1, firstPolicy.VersionNumber);
        Assert.Equal(2, secondPolicy.VersionNumber);
        Assert.Equal(firstPolicy.Id, secondPolicy.SupersedesPolicyId);
        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(secondPolicy.Id, state.CurrentPolicyId);
        Assert.Equal(4m, state.Quantity);
        Assert.Equal(60m, state.Value);
    }

    [Fact]
    public async Task Incompatible_currency_scope_and_precision_transitions_fail_closed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, Movement(Guid.NewGuid(), new DateOnly(2026, 8, 2), DateTimeOffset.UtcNow, 10m));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var firstPolicy = await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), "transition-first");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("transition-process"))).Succeeded);

        var currency = await persistence.CreatePolicyAsync(context, PolicyCommand(Policy(new DateOnly(2026, 9, 1), currency: "USD"), "transition-currency"));
        var scope = await persistence.CreatePolicyAsync(context, PolicyCommand(Policy(new DateOnly(2026, 9, 1), scope: InventoryValuationScopeMode.WarehouseProductUomTracking), "transition-scope"));
        var precision = await persistence.CreatePolicyAsync(context, PolicyCommand(Policy(new DateOnly(2026, 9, 1), unitCostScale: 6), "transition-precision"));

        Assert.False(currency.Succeeded);
        Assert.Equal("valuation_policy_transition_requires_rebaseline", currency.Code);
        Assert.False(scope.Succeeded);
        Assert.Equal("valuation_policy_transition_requires_rebaseline", scope.Code);
        Assert.False(precision.Succeeded);
        Assert.Equal("valuation_policy_transition_requires_rebaseline", precision.Code);
        Assert.Equal(firstPolicy.Id, (await persistence.ListPoliciesAsync(context, CompanyId)).Single().Id);
    }

    [Fact]
    public async Task Empty_positive_adjustment_and_count_variance_remain_pending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var adjustmentProduct = Guid.NewGuid();
        var countProduct = Guid.NewGuid();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, adjustmentProduct, UnitId, InventoryMovementDirection.Inbound, 1m, null, null, new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, sourceType: InventoryMovementSourceType.StockAdjustment),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, countProduct, UnitId, InventoryMovementDirection.Inbound, 1m, null, null, new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.InventoryCountVariance));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "empty-adjustment-policy");
        var result = await persistence.ProcessAsync(context, ProcessCommand("empty-adjustment-process"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(2, result.Value!.PendingCount);
        var pending = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).Where(item => item.Status == InventoryValuationEventStatus.Pending).ToArray();
        Assert.Equal(2, pending.Length);
        Assert.All(pending, item => Assert.Equal("current_moving_average_unavailable", item.PendingReason));
    }

    [Fact]
    public async Task Established_current_moving_average_values_positive_adjustment_even_when_source_cost_is_present()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            Movement(Guid.NewGuid(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 999m, "SAR", new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.StockAdjustment));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "adjustment-average-policy");
        var result = await persistence.ProcessAsync(context, ProcessCommand("adjustment-average-process"));

        Assert.True(result.Succeeded, result.Code);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal(10m, events[1].BaseUnitCost);
        Assert.Equal(10m, events[1].MovementValue);
        Assert.Equal(110m, events[1].NewValue);
    }

    [Fact]
    public async Task Outbound_uses_stored_prior_average_at_unit_cost_scale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 3m, 33.333m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 1m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.StockIssue));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 4), "scale-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("scale-process"))).Succeeded);
        var outbound = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).Single(item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(33.33m, outbound.BaseUnitCost);
        Assert.Equal(33.33m, outbound.MovementValue);
        Assert.Equal(33.33m, outbound.UnitCostScale is null ? 0m : outbound.BaseUnitCost);
    }

    [Fact]
    public async Task Full_depletion_persists_actual_closeout_and_correction_restores_exact_value()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var firstInboundId = Guid.NewGuid();
        var secondInboundId = Guid.NewGuid();
        var outboundId = Guid.NewGuid();
        var correctionId = Guid.NewGuid();
        await AddMovementsAsync(options, context,
            CustomMovement(firstInboundId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 2m, 33.33m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(secondInboundId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 33.34m, "SAR", new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1)),
            CustomMovement(outboundId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 3m, null, null, new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(2), sourceType: InventoryMovementSourceType.StockIssue),
            CustomMovement(correctionId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 3m, null, null, new DateOnly(2026, 1, 4), DateTimeOffset.UtcNow.AddSeconds(3), sourceType: InventoryMovementSourceType.Correction, correctionOfMovementId: outboundId));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 4), "full-closeout-policy");

        var result = await persistence.ProcessAsync(context, ProcessCommand("full-closeout-process"));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(4, result.Value!.AppliedCount);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        var outbound = Assert.Single(events, item => item.MovementId == outboundId);
        Assert.Equal(100m, outbound.PriorValue);
        Assert.Equal(33.33m, outbound.BaseUnitCost);
        Assert.Equal(99.99m, outbound.FormulaMovementValue);
        Assert.Equal(0.01m, outbound.RoundingAdjustmentAmount);
        Assert.Equal(100m, outbound.MovementValue);
        Assert.Equal(0m, outbound.NewQuantity);
        Assert.Equal(0m, outbound.NewValue);
        var correction = Assert.Single(events, item => item.MovementId == correctionId);
        Assert.Equal(outbound.Id, correction.CorrectionOfValuationEventId);
        Assert.Equal(100m, correction.MovementValue);
        Assert.Equal(99.99m, correction.FormulaMovementValue);
        Assert.Equal(0.01m, correction.RoundingAdjustmentAmount);
        Assert.Equal(3m, correction.NewQuantity);
        Assert.Equal(100m, correction.NewValue);

        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(3m, state.Quantity);
        Assert.Equal(100m, state.Value);
        Assert.Equal(33.33m, state.AverageUnitCost);
        var handoffs = (await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        var outboundHandoff = Assert.Single(handoffs, item => item.MovementId == outboundId);
        Assert.Equal(InventoryMovementDirection.Outbound, outboundHandoff.Direction);
        Assert.Equal(33.33m, outboundHandoff.BaseUnitCost);
        Assert.Equal(100m, outboundHandoff.BaseAmount);
        Assert.Equal(-100m, outboundHandoff.SignedBaseAmount);
        Assert.Equal(0.01m, outboundHandoff.RoundingAdjustmentAmount);
    }

    [Fact]
    public async Task Full_depletion_followed_by_inbound_starts_from_zero_value_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 2m, 33.33m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 33.34m, "SAR", new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1)),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 3m, null, null, new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(2), sourceType: InventoryMovementSourceType.StockIssue),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, 50m, "SAR", new DateOnly(2026, 1, 4), DateTimeOffset.UtcNow.AddSeconds(3)));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 4), "closeout-restart-policy");

        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("closeout-restart-process"))).Succeeded);
        var events = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        var laterInbound = events[^1];
        Assert.Equal(0m, laterInbound.PriorQuantity);
        Assert.Equal(0m, laterInbound.PriorValue);
        Assert.Equal(50m, laterInbound.NewValue);
        var state = Assert.Single(await persistence.ListStatesAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(1m, state.Quantity);
        Assert.Equal(50m, state.Value);
        Assert.Equal(50m, state.AverageUnitCost);
    }

    [Fact]
    public async Task Reconciliation_fails_closed_for_legacy_zero_quantity_non_zero_value_and_summary_is_partial()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, Movement(Guid.NewGuid(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "legacy-impossible-state-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("legacy-impossible-state-process"))).Succeeded);

        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE ValuationStates SET Quantity = 0, Value = 0.01, AverageUnitCost = 0");
        }

        var row = Assert.Single(await persistence.ReconcileAsync(context, new InventoryValuationQuery(CompanyId)));
        Assert.Equal(InventoryValuationReconciliationStatus.ValuationMismatch, row.Status);
        Assert.Equal("valuation_state_zero_quantity_non_zero_value", row.DifferenceReason);
        var summary = (await persistence.SummaryAsync(context, new InventoryValuationQuery(CompanyId))).Value!;
        Assert.Equal(InventoryValuationReconciliationStatus.ValuationMismatch, summary.ReconciliationStatus);
        Assert.False(summary.IsComplete);
        Assert.True(summary.IsPartial);
    }

    [Fact]
    public async Task Full_and_partial_corrections_are_exact_and_deterministic()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var originalId = Guid.NewGuid();
        var fullCorrectionId = Guid.NewGuid();
        var partialOriginalId = Guid.NewGuid();
        var partialCorrectionId = Guid.NewGuid();
        await AddMovementsAsync(options, context,
            Movement(originalId, new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m),
            CustomMovement(fullCorrectionId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 10m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), correctionOfMovementId: originalId, sourceType: InventoryMovementSourceType.Correction),
            CustomMovement(partialOriginalId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 4m, 20m, "SAR", new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(2)),
            CustomMovement(partialCorrectionId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 2m, null, null, new DateOnly(2026, 1, 4), DateTimeOffset.UtcNow.AddSeconds(3), correctionOfMovementId: partialOriginalId, sourceType: InventoryMovementSourceType.Correction));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "correction-exact-policy");
        var result = await persistence.ProcessAsync(context, ProcessCommand("correction-exact-process"));

        Assert.True(result.Succeeded, result.Code);
        var events = await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId));
        var full = events.Single(item => item.MovementId == fullCorrectionId);
        var partial = events.Single(item => item.MovementId == partialCorrectionId);
        Assert.Equal(InventoryValuationEventStatus.Applied, full.Status);
        Assert.Equal(-100m, full.MovementValue);
        Assert.Equal(0m, full.NewValue);
        Assert.Equal(-40m, partial.MovementValue);
        Assert.True(full.CorrectionOfValuationEventId.HasValue);
    }

    [Fact]
    public async Task In_transit_quantity_and_value_conserve_ship_receipt_loss_and_return()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var destinationWarehouseId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var transferLineId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var transfer = new InventoryTransferEntity(context.TenantId, transferId, CompanyId, null, WarehouseId, "WH-A", "Warehouse A", destinationWarehouseId, "WH-B", "Warehouse B", ProductId, "SKU-A", "Product A", UnitId, "EA", 4m, InventoryTransferMode.InTransit, null, null, ActorId, DateTimeOffset.UtcNow);
        var line = new InventoryTransferLineEntity(context.TenantId, transferLineId, transferId, 4m);
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.Transfers.Add(transfer);
            db.TransferLines.Add(line);
            db.TransferEvents.AddRange(
                new InventoryTransferEventEntity(context.TenantId, Guid.NewGuid(), transferId, transferLineId, InventoryTransferEventType.Shipped, 4m, null, null, ActorId, "transit-shipped", DateTimeOffset.UtcNow, shipmentId),
                new InventoryTransferEventEntity(context.TenantId, Guid.NewGuid(), transferId, transferLineId, InventoryTransferEventType.Received, 1m, null, null, ActorId, "transit-received", DateTimeOffset.UtcNow.AddSeconds(1)));
            db.StockMovements.AddRange(
                Movement(Guid.NewGuid(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m),
                CustomMovement(shipmentId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 4m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.WarehouseTransferShipment, transferId: transferId, transferLineId: transferLineId));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "transit-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("transit-process"))).Succeeded);
        var beforeResolution = Assert.Single(await persistence.ReconcileAsync(context, new InventoryValuationQuery(CompanyId, WarehouseId: WarehouseId)));
        Assert.Equal(3m, beforeResolution.InTransitQuantity);
        Assert.Equal(30m, beforeResolution.InTransitValue);
        Assert.Equal(InventoryInTransitValuationStatus.Ready, beforeResolution.InTransitValueStatus);

        var returnMovementId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            db.TransferEvents.Add(new InventoryTransferEventEntity(context.TenantId, Guid.NewGuid(), transferId, transferLineId, InventoryTransferEventType.ShortageResolved, 2m, null, "loss", ActorId, "transit-loss", DateTimeOffset.UtcNow.AddSeconds(2)));
            db.StockMovements.Add(CustomMovement(returnMovementId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 1m, null, null, new DateOnly(2026, 1, 3), DateTimeOffset.UtcNow.AddSeconds(2), sourceType: InventoryMovementSourceType.WarehouseTransferReturn, transferId: transferId, transferLineId: transferLineId));
            await db.SaveChangesAsync();
        }
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("transit-resolution-process"))).Succeeded);
        var afterResolution = Assert.Single(await persistence.ReconcileAsync(context, new InventoryValuationQuery(CompanyId, WarehouseId: WarehouseId)));
        Assert.Equal(0m, afterResolution.InTransitQuantity);
        Assert.Equal(0m, afterResolution.InTransitValue);
        Assert.Equal(InventoryInTransitValuationStatus.Ready, afterResolution.InTransitValueStatus);
    }

    [Fact]
    public async Task Missing_shipment_valuation_marks_in_transit_value_pending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var transferId = Guid.NewGuid();
        var transferLineId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.Transfers.Add(new InventoryTransferEntity(context.TenantId, transferId, CompanyId, null, WarehouseId, "WH-A", "Warehouse A", Guid.NewGuid(), "WH-B", "Warehouse B", ProductId, "SKU-A", "Product A", UnitId, "EA", 4m, InventoryTransferMode.InTransit, null, null, ActorId, DateTimeOffset.UtcNow));
            db.TransferLines.Add(new InventoryTransferLineEntity(context.TenantId, transferLineId, transferId, 4m));
            db.TransferEvents.Add(new InventoryTransferEventEntity(context.TenantId, Guid.NewGuid(), transferId, transferLineId, InventoryTransferEventType.Shipped, 4m, null, null, ActorId, "pending-transit-shipped", DateTimeOffset.UtcNow, shipmentId));
            db.StockMovements.Add(CustomMovement(shipmentId, CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 4m, null, null, new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, sourceType: InventoryMovementSourceType.WarehouseTransferShipment, transferId: transferId, transferLineId: transferLineId));
            await db.SaveChangesAsync();
        }
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "pending-transit-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("pending-transit-process"))).Succeeded);
        var row = Assert.Single(await persistence.ReconcileAsync(context, new InventoryValuationQuery(CompanyId, WarehouseId: WarehouseId)));
        Assert.Equal(4m, row.InTransitQuantity);
        Assert.Equal(0m, row.InTransitValue);
        Assert.Equal(InventoryInTransitValuationStatus.Pending, row.InTransitValueStatus);
    }

    [Fact]
    public async Task Process_replay_is_exact_and_different_payload_conflicts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, Movement(Guid.NewGuid(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "process-replay-policy");
        var first = await persistence.ProcessAsync(context, ProcessCommand("same-process-key", fingerprint: "same-process-fingerprint"));
        var replay = await persistence.ProcessAsync(context, ProcessCommand("same-process-key", fingerprint: "same-process-fingerprint"));
        var conflict = await persistence.ProcessAsync(context, ProcessCommand("same-process-key", fingerprint: "different-process-fingerprint"));

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(first.Value, replay.Value);
        Assert.False(conflict.Succeeded);
        Assert.Equal("idempotency_conflict", conflict.Code);
        await using var db = new InventoryDbContext(options, context.TenantContext);
        Assert.Equal(1, await db.ValuationRuns.CountAsync());
    }

    [Fact]
    public async Task Policy_create_replay_uses_inventory_idempotency_and_corrupt_snapshot_fails_safe()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await using (var db = new InventoryDbContext(options, context.TenantContext))
            await db.Database.EnsureCreatedAsync();
        var request = Policy(new DateOnly(2026, 1, 1));
        var firstCommand = PolicyCommand(request, "same-policy-key", "same-policy-fingerprint");
        var first = await persistence.CreatePolicyAsync(context, firstCommand);
        var replay = await persistence.CreatePolicyAsync(context, PolicyCommand(request, "same-policy-key", "same-policy-fingerprint"));
        var conflict = await persistence.CreatePolicyAsync(context, PolicyCommand(request, "same-policy-key", "different-policy-fingerprint"));
        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);
        Assert.False(conflict.Succeeded);
        Assert.Equal("idempotency_conflict", conflict.Code);

        var corruptKey = "corrupt-policy-key";
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            db.Idempotency.Add(new InventoryIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, "inventory.valuation.policy.create", corruptKey, "corrupt-fingerprint", "valuation-policy", Guid.NewGuid(), "{not-json", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        var corrupt = await persistence.CreatePolicyAsync(context, PolicyCommand(Policy(new DateOnly(2026, 2, 1)), corruptKey, "corrupt-fingerprint"));
        Assert.False(corrupt.Succeeded);
        Assert.Equal("replay_unavailable", corrupt.Code);
    }

    [Fact]
    public async Task Repeated_scope_processing_cannot_fork_state_anchor_or_applied_event()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, Movement(Guid.NewGuid(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "single-scope-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("first-scope-process"))).Succeeded);
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("second-scope-process"))).Succeeded);
        await using var db = new InventoryDbContext(options, context.TenantContext);
        Assert.Equal(1, await db.ValuationStates.CountAsync());
        Assert.Equal(1, await db.ValuationScopeAnchors.CountAsync());
        Assert.Equal(1, await db.MovementValuationEvents.CountAsync(item => item.Status == InventoryValuationEventStatus.Applied));
    }

    [Fact]
    public async Task Finance_handoff_exposes_nonnegative_base_amount_and_signed_direction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            Movement(Guid.NewGuid(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Outbound, 2m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1), sourceType: InventoryMovementSourceType.StockIssue));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "handoff-sign-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("handoff-sign-process"))).Succeeded);
        var handoffs = (await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(CompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal(2, handoffs.Length);
        Assert.Equal(InventoryMovementDirection.Inbound, handoffs[0].Direction);
        Assert.Equal(100m, handoffs[0].BaseAmount);
        Assert.Equal(100m, handoffs[0].SignedBaseAmount);
        Assert.Equal(InventoryMovementDirection.Outbound, handoffs[1].Direction);
        Assert.Equal(20m, handoffs[1].BaseAmount);
        Assert.Equal(-20m, handoffs[1].SignedBaseAmount);
        Assert.All(handoffs, item => Assert.True(item.BaseAmount >= 0m));
    }

    [Fact]
    public async Task Summary_aggregates_products_and_pending_pool_is_partial_without_average()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var secondProduct = Guid.NewGuid();
        await AddMovementsAsync(options, context,
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 2m, 10m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, secondProduct, UnitId, InventoryMovementDirection.Inbound, 3m, 20m, "SAR", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow.AddSeconds(1)));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "summary-complete-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("summary-complete-process"))).Succeeded);
        var complete = (await persistence.SummaryAsync(context, new InventoryValuationQuery(CompanyId, WarehouseId: WarehouseId))).Value!;
        Assert.Equal(5m, complete.PhysicalOnHandQuantity);
        Assert.Equal(5m, complete.ValuedQuantity);
        Assert.Equal(80m, complete.ValuedAmount);
        Assert.True(complete.IsComplete);
        Assert.DoesNotContain(typeof(InventoryValuationSummaryRecord).GetProperties(), item => item.Name == "AverageUnitCost");

        var pendingProduct = Guid.NewGuid();
        await AddMovementsAsync(options, context, CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, pendingProduct, UnitId, InventoryMovementDirection.Inbound, 1m, null, null, new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(2), sourceType: InventoryMovementSourceType.StockAdjustment));
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("summary-pending-process"))).Succeeded);
        var partial = (await persistence.SummaryAsync(context, new InventoryValuationQuery(CompanyId, WarehouseId: WarehouseId))).Value!;
        Assert.Equal(6m, partial.PhysicalOnHandQuantity);
        Assert.Equal(5m, partial.ValuedQuantity);
        Assert.Equal(1, partial.PendingMovementCount);
        Assert.True(partial.IsPartial);
        Assert.False(partial.IsComplete);
        Assert.Equal(InventoryValuationReconciliationStatus.PendingValuation, partial.ReconciliationStatus);
    }

    [Fact]
    public async Task Current_reconciliation_state_filters_do_not_create_false_period_mismatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context,
            Movement(Guid.NewGuid(), new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow, 10m),
            CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 5m, 20m, "SAR", new DateOnly(2026, 1, 2), DateTimeOffset.UtcNow.AddSeconds(1)));
        var persistence = new InventoryValuationPersistence(options, null, null, null);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "reconcile-current-policy");
        Assert.True((await persistence.ProcessAsync(context, ProcessCommand("reconcile-current-process"))).Succeeded);
        var rows = await persistence.ReconcileAsync(context, new InventoryValuationQuery(CompanyId, BranchId: null, WarehouseId: WarehouseId, ProductId: ProductId, UnitOfMeasureId: UnitId));
        var row = Assert.Single(rows);
        Assert.Equal(InventoryValuationReconciliationStatus.Reconciled, row.Status);
        Assert.Equal(0m, row.QuantityDifference);
    }

    [Fact]
    public async Task Missing_fx_blocks_predecessor_then_later_rate_availability_continues_deterministically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await AddMovementsAsync(options, context, CustomMovement(Guid.NewGuid(), CompanyId, WarehouseId, ProductId, UnitId, InventoryMovementDirection.Inbound, 2m, 10m, "USD", new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow));
        var exchangeRates = new TestExchangeRatePersistence();
        var persistence = new InventoryValuationPersistence(options, null, null, exchangeRates);
        await CreatePolicyAsync(persistence, context, Policy(new DateOnly(2026, 1, 1)), "fx-policy");
        var pending = await persistence.ProcessAsync(context, ProcessCommand("fx-before"));
        Assert.Equal(1, pending.Value!.PendingCount);
        Assert.Equal("exchange_rate_missing", (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).Single().PendingReason);

        exchangeRates.Rates =
        [
            new MasterDataExchangeRateRecord(Guid.NewGuid(), new TenantId(TenantId), Guid.NewGuid(), Guid.NewGuid(), "USD", "SAR", MasterDataLifecycleState.Active, 1,
            [new MasterDataExchangeRateVersionRecord(Guid.NewGuid(), 1, new DateOnly(2026, 1, 1), null, 2m, 4, ExchangeRateProvenance.Manual, "test", "USD", "SAR")], [1]),
        ];
        var applied = await persistence.ProcessAsync(context, ProcessCommand("fx-after"));
        Assert.Equal(1, applied.Value!.AppliedCount);
        Assert.Equal(40m, (await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId))).Single(item => item.Status == InventoryValuationEventStatus.Applied).NewValue);
    }

    private static InventoryValuationPolicyRequest Policy(
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null,
        string currency = "SAR",
        InventoryValuationScopeMode scope = InventoryValuationScopeMode.WarehouseProductUom,
        int unitCostScale = 8,
        int amountScale = 8,
        InventoryValuationRoundingMode rounding = InventoryValuationRoundingMode.ToEven,
        Guid? companyId = null) =>
        new(companyId ?? CompanyId, Guid.Parse("99999999-9999-9999-9999-999999999999"), currency, scope, effectiveFrom, effectiveTo, unitCostScale, amountScale, rounding, "PurchaseOrderUnitPrice", "CurrentMovingAverage", "CurrentMovingAverage");

    private static InventoryValuationPolicyCommand PolicyCommand(InventoryValuationPolicyRequest request, string key, string? fingerprint = null) =>
        new(Guid.NewGuid(), request, ActorId, DateTimeOffset.UtcNow, $"test-{key}", key, fingerprint ?? $"fingerprint-{key}");

    private static InventoryValuationProcessCommand ProcessCommand(
        string key,
        Guid? productId = null,
        Guid? warehouseId = null,
        Guid? branchId = null,
        Guid? unitOfMeasureId = null,
        string? fingerprint = null,
        Guid? companyId = null) =>
        new(companyId ?? CompanyId, branchId, warehouseId ?? InventoryValuationTests.WarehouseId, productId, unitOfMeasureId ?? InventoryValuationTests.UnitId, ActorId, DateTimeOffset.UtcNow, $"test-{key}", key, fingerprint ?? $"fingerprint-{key}");

    private static async Task<InventoryValuationPolicyRecord> CreatePolicyAsync(
        InventoryValuationPersistence persistence,
        InventoryRequestContext context,
        InventoryValuationPolicyRequest request,
        string key)
    {
        var result = await persistence.CreatePolicyAsync(context, PolicyCommand(request, key));
        Assert.True(result.Succeeded, result.Code);
        return result.Value!;
    }

    private static async Task AddMovementsAsync(
        DbContextOptions<InventoryDbContext> options,
        InventoryRequestContext context,
        params InventoryStockMovementEntity[] movements)
    {
        await using var db = new InventoryDbContext(options, context.TenantContext);
        await db.Database.EnsureCreatedAsync();
        db.StockMovements.AddRange(movements);
        await db.SaveChangesAsync();
    }

    private static InventoryStockMovementEntity CustomMovement(
        Guid id,
        Guid companyId,
        Guid warehouseId,
        Guid productId,
        Guid unitId,
        InventoryMovementDirection direction,
        decimal quantity,
        decimal? unitCost,
        string? currency,
        DateOnly effectiveDate,
        DateTimeOffset postedAt,
        string? trackingIdentity = null,
        InventoryMovementSourceType sourceType = InventoryMovementSourceType.OpeningBalance,
        Guid? correctionOfMovementId = null,
        Guid? transferId = null,
        Guid? transferLineId = null) =>
        new(
            new TenantId(TenantId), id, companyId, null, warehouseId, "WH-A", "Warehouse A", productId, "SKU-" + productId.ToString("N")[..6], "Product", unitId, "EA",
            direction, quantity, unitCost, currency, InventoryValuationStatus.Pending, trackingIdentity, sourceType, Guid.NewGuid(), Guid.NewGuid(), correctionOfMovementId,
            effectiveDate, ActorId, "valuation-test", postedAt, null, null, null, null, null, null, transferId, transferLineId, "valuation-test");

    private sealed class TestExchangeRatePersistence : IMasterDataExchangeRatePersistence
    {
        public IReadOnlyList<MasterDataExchangeRateRecord> Rates { get; set; } = [];
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult(Rates);
        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataExchangeRateRecord?>(Rates.FirstOrDefault(item => item.Id == exchangeRateId));
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static InventoryStockMovementEntity Movement(Guid id, DateOnly effectiveDate, DateTimeOffset postedAt, decimal cost) =>
        new(
            new TenantId(TenantId),
            id,
            CompanyId,
            null,
            WarehouseId,
            "WH-A",
            "Warehouse A",
            ProductId,
            "SKU-A",
            "Product A",
            UnitId,
            "EA",
            InventoryMovementDirection.Inbound,
            10m,
            cost,
            "SAR",
            null,
            InventoryMovementSourceType.OpeningBalance,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            effectiveDate,
            ActorId,
            "valuation-test",
            postedAt);

    private static InventoryRequestContext Context() =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                ActorId,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantContext.ForOrdinaryMembership(
                    new TenantId(TenantId),
                    new MembershipReference(Guid.NewGuid()),
                    actorId: ActorId),
                "tenant.inventory.valuation.process")).Context!;
}
