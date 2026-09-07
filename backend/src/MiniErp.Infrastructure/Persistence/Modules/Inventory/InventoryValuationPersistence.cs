#pragma warning disable CS1591

using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryValuationPersistence(
    DbContextOptions options,
    IGoodsReceiptPersistence? goodsReceipts,
    IPurchaseOrderPersistence? purchaseOrders,
    IMasterDataExchangeRatePersistence? exchangeRates) : IInventoryValuationPersistence, IInventoryReportingReadPort
{
    private InventoryDbContext CreateContext(InventoryRequestContext context) => new(options, context.TenantContext);

    public async Task<InventoryPersistenceResult<InventoryValuationPolicyRecord>> CreatePolicyAsync(InventoryRequestContext context, InventoryValuationPolicyCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var replay = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.OperationId == "inventory.valuation.policy.create" && item.Key == command.IdempotencyKey, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.Fingerprint, command.RequestFingerprint, StringComparison.Ordinal))
                    return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.Conflict, "idempotency_conflict");
                InventoryValuationPolicyRecord? value;
                try { value = JsonSerializer.Deserialize<InventoryValuationPolicyRecord>(replay.SnapshotJson); }
                catch (JsonException) { return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.Failure, "replay_unavailable"); }
                return value is null
                    ? InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.Failure, "replay_unavailable")
                    : InventoryPersistenceResult<InventoryValuationPolicyRecord>.Success(value);
            }
        }

        var overlaps = await db.ValuationPolicies.AnyAsync(item => item.CompanyId == command.Request.CompanyId && item.IsActive && item.EffectiveFrom <= (command.Request.EffectiveTo ?? DateOnly.MaxValue) && (item.EffectiveTo == null || item.EffectiveTo >= command.Request.EffectiveFrom), cancellationToken);
        if (overlaps) return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.Conflict, "valuation_policy_overlap");
        var maxVersionNumber = await db.ValuationPolicies
            .Where(item => item.CompanyId == command.Request.CompanyId)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var predecessor = maxVersionNumber == 0
            ? null
            : await db.ValuationPolicies.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CompanyId == command.Request.CompanyId && item.VersionNumber == maxVersionNumber, cancellationToken);
        var hasValuedStock = await db.ValuationStates.AnyAsync(item => item.CompanyId == command.Request.CompanyId && (item.Quantity != 0m || item.LastAppliedLedgerSequence > 0), cancellationToken);
        if (predecessor is not null && hasValuedStock && !AreCompatible(predecessor, command.Request))
            return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.InvalidState, "valuation_policy_transition_requires_rebaseline");

        var versionNumber = checked((predecessor?.VersionNumber ?? 0) + 1);
        var policy = new InventoryValuationPolicyEntity(context.TenantId, command.Id, command.Request, versionNumber, predecessor?.Id, command.ActorId, command.OccurredAt);
        try
        {
            db.ValuationPolicies.Add(policy);
            db.Audit.Add(new InventoryAuditEntity(context.TenantId, Guid.NewGuid(), "valuation-policy", policy.Id, "inventory.valuation.policy.create", command.ActorId, context.SessionId, context.AuthorizationPath.ToString(), "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"policy:{policy.Id}", command.OccurredAt));
            await db.SaveChangesAsync(cancellationToken);
            var result = ToPolicy(policy);
            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
                db.Idempotency.Add(new InventoryIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, "inventory.valuation.policy.create", command.IdempotencyKey!, command.RequestFingerprint, "valuation-policy", policy.Id, JsonSerializer.Serialize(result), command.OccurredAt));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Success(result);
        }
        catch (DbUpdateException exception) when (InventoryPersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.Conflict, "valuation_concurrency_conflict");
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception))
        {
            return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.Conflict, "valuation_concurrency_conflict");
        }
    }

    public async Task<IReadOnlyList<InventoryValuationPolicyRecord>> ListPoliciesAsync(InventoryRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var values = await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == companyId).OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.VersionNumber).ToListAsync(cancellationToken);
        return values.Select(ToPolicy).ToArray();
    }

    public async Task<InventoryPersistenceResult<InventoryValuationProcessResult>> ProcessAsync(InventoryRequestContext context, InventoryValuationProcessCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
            {
                var replay = await db.ValuationRuns.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
                if (replay is not null)
                {
                    if (!string.Equals(replay.RequestFingerprint, command.RequestFingerprint, StringComparison.Ordinal))
                        return InventoryPersistenceResult<InventoryValuationProcessResult>.Denied(InventoryPersistenceOutcome.Conflict, "idempotency_conflict");
                    InventoryValuationProcessResult? replayValue;
                    try { replayValue = JsonSerializer.Deserialize<InventoryValuationProcessResult>(replay.ResultJson); }
                    catch (JsonException) { return InventoryPersistenceResult<InventoryValuationProcessResult>.Denied(InventoryPersistenceOutcome.Failure, "replay_unavailable"); }
                    return replayValue is null
                        ? InventoryPersistenceResult<InventoryValuationProcessResult>.Denied(InventoryPersistenceOutcome.Failure, "replay_unavailable")
                        : InventoryPersistenceResult<InventoryValuationProcessResult>.Success(replayValue);
                }
            }

            var movementsQuery = db.StockMovements.AsNoTracking().Where(item => item.CompanyId == command.CompanyId);
            if (command.BranchId.HasValue) movementsQuery = movementsQuery.Where(item => item.BranchId == command.BranchId);
            if (command.WarehouseId.HasValue) movementsQuery = movementsQuery.Where(item => item.WarehouseId == command.WarehouseId);
            if (command.ProductId.HasValue) movementsQuery = movementsQuery.Where(item => item.ProductId == command.ProductId);
            if (command.UnitOfMeasureId.HasValue) movementsQuery = movementsQuery.Where(item => item.UnitOfMeasureId == command.UnitOfMeasureId);
            var movements = await movementsQuery.OrderBy(item => item.LedgerSequence).ThenBy(item => item.Id).ToListAsync(cancellationToken);
            var policies = await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == command.CompanyId && item.IsActive).ToListAsync(cancellationToken);
            var movementIds = movements.Select(item => item.Id).ToArray();
            var existingEvents = movementIds.Length == 0
                ? []
                : await db.MovementValuationEvents.Where(item => movementIds.Contains(item.MovementId)).ToListAsync(cancellationToken);
            var appliedEventsForCompany = await db.MovementValuationEvents.AsNoTracking().Where(item => item.CompanyId == command.CompanyId && item.Status == InventoryValuationEventStatus.Applied).ToListAsync(cancellationToken);

            var applied = 0;
            var pending = 0;
            var blocked = 0;
            long? latest = movements.Count == 0 ? null : movements.Max(item => item.LedgerSequence);
            string functionalCurrency = string.Empty;
            Guid? lastPolicyId = null;
            var missingPolicyBlockedBasePools = new HashSet<PhysicalPoolKey>();
            var stoppedValuationScopes = new HashSet<ValuationScopeKey>();
            var stateCache = new Dictionary<ValuationScopeKey, InventoryValuationStateEntity>();

            foreach (var movement in movements)
            {
                if (existingEvents.Any(item => item.MovementId == movement.Id && item.Status == InventoryValuationEventStatus.Applied))
                    continue;

                var basePool = PhysicalPoolKey.From(movement);
                var policy = policies
                    .Where(item => item.EffectiveFrom <= movement.EffectiveDate && (item.EffectiveTo == null || item.EffectiveTo >= movement.EffectiveDate))
                    .OrderByDescending(item => item.EffectiveFrom)
                    .ThenByDescending(item => item.VersionNumber)
                    .FirstOrDefault();
                if (policy is null)
                {
                    missingPolicyBlockedBasePools.Add(basePool);
                    AddPendingEventIfMissing(db, existingEvents, context, movement, null, null, "valuation_policy_not_configured", "valuation_policy_not_configured", command);
                    pending++;
                    continue;
                }

                functionalCurrency = policy.FunctionalCurrencyCode;
                lastPolicyId = policy.Id;
                var scopeKey = ValuationScopeKey.From(movement, policy.ScopeMode);
                if (!stateCache.TryGetValue(scopeKey, out var state))
                {
                    state = await db.ValuationStates.SingleOrDefaultAsync(item => item.CompanyId == scopeKey.CompanyId && item.BranchId == scopeKey.BranchId && item.WarehouseId == scopeKey.WarehouseId && item.ProductId == scopeKey.ProductId && item.UnitOfMeasureId == scopeKey.UnitOfMeasureId && item.TrackingIdentity == scopeKey.TrackingIdentity, cancellationToken);
                    var anchor = await db.ValuationScopeAnchors.SingleOrDefaultAsync(item => item.CompanyId == scopeKey.CompanyId && item.BranchId == scopeKey.BranchId && item.WarehouseId == scopeKey.WarehouseId && item.ProductId == scopeKey.ProductId && item.UnitOfMeasureId == scopeKey.UnitOfMeasureId && item.TrackingIdentity == scopeKey.TrackingIdentity, cancellationToken);
                    if (state is null)
                    {
                        state = new InventoryValuationStateEntity(context.TenantId, scopeKey.CompanyId, scopeKey.BranchId, scopeKey.WarehouseId, scopeKey.ProductId, scopeKey.UnitOfMeasureId, scopeKey.TrackingIdentity, policy, command.OccurredAt);
                        db.ValuationStates.Add(state);
                    }

                    if (anchor is null)
                        db.ValuationScopeAnchors.Add(new InventoryValuationScopeAnchorEntity(context.TenantId, scopeKey.CompanyId, scopeKey.BranchId, scopeKey.WarehouseId, scopeKey.ProductId, scopeKey.UnitOfMeasureId, scopeKey.TrackingIdentity));
                    else
                        anchor.TouchVersion();
                    stateCache[scopeKey] = state;
                }

                if (movement.LedgerSequence <= state.LastAppliedLedgerSequence)
                    continue;

                if (missingPolicyBlockedBasePools.Contains(basePool))
                {
                    AddPendingEventIfMissing(db, existingEvents, context, movement, policy, state, "pending_predecessor", "pending_predecessor", command);
                    pending++;
                    continue;
                }

                if (stoppedValuationScopes.Contains(scopeKey))
                {
                    AddPendingEventIfMissing(db, existingEvents, context, movement, policy, state, "pending_predecessor", "pending_predecessor", command);
                    pending++;
                    continue;
                }

                if (state.CurrentPolicyId.HasValue && state.CurrentPolicyId.Value != policy.Id && (state.Quantity != 0m || state.LastAppliedLedgerSequence > 0))
                {
                    var priorPolicy = policies.SingleOrDefault(item => item.Id == state.CurrentPolicyId.Value);
                    if (priorPolicy is not null && !AreCompatible(priorPolicy, policy))
                    {
                        stoppedValuationScopes.Add(scopeKey);
                        AddPendingEventIfMissing(db, existingEvents, context, movement, policy, state, "valuation_policy_transition_requires_rebaseline", "valuation_policy_transition_requires_rebaseline", command);
                        blocked++;
                        continue;
                    }
                }

                var correction = movement.CorrectionOfMovementId.HasValue
                    ? await ResolveCorrectionAsync(context, db, movement, policy, cancellationToken)
                    : null;
                if (correction is not null && !correction.Succeeded)
                {
                    var correctionStatus = correction.IsBlocked ? InventoryValuationEventStatus.Blocked : InventoryValuationEventStatus.Pending;
                    AddPendingEventIfMissing(db, existingEvents, context, movement, policy, state, correction.Code, correction.Reason, command, correctionStatus, correction.Cost?.BaseUnitCost, correction.Cost, correction.OriginalValuationEventId);
                    stoppedValuationScopes.Add(scopeKey);
                    if (correction.IsBlocked) blocked++; else pending++;
                    continue;
                }

                var cost = correction?.Cost ?? await ResolveCostAsync(context, db, movement, policy, state, cancellationToken);
                if (!cost.Succeeded)
                {
                    AddPendingEventIfMissing(db, existingEvents, context, movement, policy, state, cost.Code, cost.Reason, command, InventoryValuationEventStatus.Pending, null, cost);
                    stoppedValuationScopes.Add(scopeKey);
                    pending++;
                    continue;
                }

                var baseCost = movement.Direction == InventoryMovementDirection.Outbound ? state.AverageUnitCost : cost.BaseUnitCost ?? 0m;
                MovingWeightedAverageOutput calculation;
                string calculationError;
                var calculationSucceeded = correction is not null
                    ? MovingWeightedAverageCalculator.TryApplyCorrection(new MovingWeightedAverageCorrectionInput(movement.Direction, movement.Quantity, correction.ReversalValue, state.Quantity, state.Value, policy.UnitCostScale, policy.AmountScale, policy.RoundingMode, correction.IsFullReversal, correction.FormulaReversalValue, correction.RoundingAdjustmentAmount), out calculation, out calculationError)
                    : MovingWeightedAverageCalculator.TryApply(new MovingWeightedAverageInput(movement.Direction, movement.Quantity, baseCost, state.Quantity, state.Value, policy.UnitCostScale, policy.AmountScale, policy.RoundingMode, state.AverageUnitCost), out calculation, out calculationError);
                if (!calculationSucceeded)
                {
                    AddPendingEventIfMissing(db, existingEvents, context, movement, policy, state, calculationError, calculationError, command, InventoryValuationEventStatus.Blocked, baseCost, cost, correction?.OriginalValuationEventId);
                    stoppedValuationScopes.Add(scopeKey);
                    blocked++;
                    continue;
                }

                // Keep the entity invariant as a deterministic processing
                // boundary as well as a calculator invariant. No invalid
                // calculated state may reach Apply or escape as an outage.
                if (calculation.NewQuantity < 0m
                    || calculation.NewValue < 0m
                    || calculation.NewQuantity == 0m && calculation.NewValue != 0m)
                {
                    const string invariantCode = "valuation_state_invariant_violation";
                    AddPendingEventIfMissing(db, existingEvents, context, movement, policy, state, invariantCode, invariantCode, command, InventoryValuationEventStatus.Blocked, baseCost, cost, correction?.OriginalValuationEventId);
                    stoppedValuationScopes.Add(scopeKey);
                    blocked++;
                    continue;
                }

                var isBackdated = appliedEventsForCompany.Any(item => item.CompanyId == scopeKey.CompanyId && item.BranchId == scopeKey.BranchId && item.WarehouseId == scopeKey.WarehouseId && item.ProductId == scopeKey.ProductId && item.UnitOfMeasureId == scopeKey.UnitOfMeasureId && item.TrackingIdentity == scopeKey.TrackingIdentity && item.EffectiveOn > movement.EffectiveDate);
                var eventEntity = new InventoryMovementValuationEventEntity(context.TenantId, Guid.NewGuid(), movement.Id, movement.LedgerSequence, InventoryValuationEventStatus.Applied, isBackdated ? "backdated_applied" : "applied", movement, policy, state.Quantity, state.Value, calculation.NewQuantity, calculation.NewValue, calculation.MovementValue, calculation.FormulaMovementValue, calculation.RoundingAdjustmentAmount, baseCost, cost.ExchangeRateId, cost.ExchangeRateVersionId, cost.ExchangeRateVersionNumber, cost.ExchangeRate, cost.ExchangeRateScale, cost.ExchangeRateProvenance, correction?.OriginalValuationEventId, null, isBackdated, null, command.ActorId, command.CorrelationId, command.OccurredAt, cost.TransactionUnitCost, cost.TransactionCurrencyCode);
                db.MovementValuationEvents.Add(eventEntity);
                existingEvents.Add(eventEntity);
                appliedEventsForCompany.Add(eventEntity);
                state.Apply(calculation.NewQuantity, calculation.NewValue, calculation.AverageUnitCost, movement.LedgerSequence, policy, command.OccurredAt);
                var handoffAmount = Math.Abs(calculation.MovementValue);
                db.FinanceValuationHandoffs.Add(new InventoryFinanceValuationHandoffEntity(context.TenantId, movement, eventEntity, policy, baseCost, handoffAmount, calculation.RoundingAdjustmentAmount, cost.TransactionUnitCost, cost.TransactionCurrencyCode, cost.ExchangeRateId, cost.ExchangeRateVersionId, cost.ExchangeRateVersionNumber, cost.ExchangeRate, cost.ExchangeRateScale, cost.ExchangeRateProvenance, InventoryFinanceValuationHandoffStatus.ReadyForFinance, command.CorrelationId, command.OccurredAt));
                applied++;
            }

            var result = new InventoryValuationProcessResult(command.CompanyId, command.BranchId, command.WarehouseId, command.ProductId, applied, pending, blocked, latest, command.OccurredAt, functionalCurrency, lastPolicyId?.ToString("D"), movements.Count == 0 ? "no_movements" : null);
            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
                db.ValuationRuns.Add(new InventoryValuationRunEntity(context.TenantId, context.ActorId, command.IdempotencyKey!, command.RequestFingerprint, JsonSerializer.Serialize(result), command.OccurredAt));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InventoryPersistenceResult<InventoryValuationProcessResult>.Success(result);
        }
        catch (DbUpdateConcurrencyException)
        {
            return InventoryPersistenceResult<InventoryValuationProcessResult>.Denied(InventoryPersistenceOutcome.Conflict, "valuation_concurrency_conflict");
        }
        catch (DbUpdateException exception) when (InventoryPersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return InventoryPersistenceResult<InventoryValuationProcessResult>.Denied(InventoryPersistenceOutcome.Conflict, "valuation_concurrency_conflict");
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception))
        {
            return InventoryPersistenceResult<InventoryValuationProcessResult>.Denied(InventoryPersistenceOutcome.Conflict, "valuation_concurrency_conflict");
        }
    }

    public async Task<IReadOnlyList<InventoryValuationStateRecord>> ListStatesAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await ApplyScope(db.ValuationStates.AsNoTracking(), query).OrderBy(item => item.WarehouseId).ThenBy(item => item.ProductId).ThenBy(item => item.TrackingIdentity).ToListAsync(cancellationToken); return values.Select(ToState).ToArray();
    }

    public async Task<IReadOnlyList<InventoryMovementValuationEventRecord>> ListEventsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await ApplyScope(db.MovementValuationEvents.AsNoTracking(), query).OrderByDescending(item => item.LedgerSequence).ThenByDescending(item => item.Id).ToListAsync(cancellationToken); return values.Select(ToEvent).ToArray();
    }

    public async Task<ReportingSourcePage<InventoryValuationStateRecord>> ListStatesReportingPageAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var source = ApplyScope(db.ValuationStates.AsNoTracking(), query);
        var total = await source.CountAsync(cancellationToken);
        var asOf = total == 0 ? null : await source.Select(item => (DateTimeOffset?)item.UpdatedAt).MaxAsync(cancellationToken);
        var ordered = page.SortBy?.ToLowerInvariant() switch
        {
            "quantity" => page.SortDirection == "desc" ? source.OrderByDescending(item => item.Quantity).ThenByDescending(item => item.Id) : source.OrderBy(item => item.Quantity).ThenBy(item => item.Id),
            "value" => page.SortDirection == "desc" ? source.OrderByDescending(item => item.Value).ThenByDescending(item => item.Id) : source.OrderBy(item => item.Value).ThenBy(item => item.Id),
            "updatedat" => page.SortDirection == "asc" ? source.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Id) : source.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.WarehouseId).ThenBy(item => item.ProductId).ThenBy(item => item.TrackingIdentity).ThenBy(item => item.Id)
        };
        var values = await ordered.Skip(page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        return new ReportingSourcePage<InventoryValuationStateRecord>(values.Select(ToState).ToArray(), total, "warehouseId,productId,trackingIdentity,id", asOf);
    }

    public async Task<ReportingSourcePage<InventoryMovementValuationEventRecord>> ListEventsReportingPageAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var source = ApplyScope(db.MovementValuationEvents.AsNoTracking(), query);
        var total = await source.CountAsync(cancellationToken);
        var asOf = total == 0 ? null : await source.Select(item => (DateTimeOffset?)item.OccurredAt).MaxAsync(cancellationToken);
        var ordered = page.SortBy?.ToLowerInvariant() switch
        {
            "effectiveon" => page.SortDirection == "asc" ? source.OrderBy(item => item.EffectiveOn).ThenBy(item => item.Id) : source.OrderByDescending(item => item.EffectiveOn).ThenByDescending(item => item.Id),
            "quantity" => page.SortDirection == "asc" ? source.OrderBy(item => item.Quantity).ThenBy(item => item.Id) : source.OrderByDescending(item => item.Quantity).ThenByDescending(item => item.Id),
            "status" => page.SortDirection == "asc" ? source.OrderBy(item => item.Status).ThenBy(item => item.Id) : source.OrderByDescending(item => item.Status).ThenByDescending(item => item.Id),
            _ => page.SortDirection == "asc" ? source.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id) : source.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id)
        };
        var values = await ordered.Skip(page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        return new ReportingSourcePage<InventoryMovementValuationEventRecord>(values.Select(ToEvent).ToArray(), total, "occurredAt,id", asOf);
    }

    public async Task<IReadOnlyList<InventoryValuationReconciliationRecord>> ReconcileAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var movements = await ApplyMovementScope(db.StockMovements.AsNoTracking(), query).OrderBy(item => item.LedgerSequence).ToListAsync(cancellationToken);
        var events = await ApplyScope(db.MovementValuationEvents.AsNoTracking(), query).ToListAsync(cancellationToken);
        var states = await ApplyScope(db.ValuationStates.AsNoTracking(), query).ToListAsync(cancellationToken);
        var policies = await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.IsActive).ToListAsync(cancellationToken);
        var handoffs = await ApplyHandoffScope(db.FinanceValuationHandoffs.AsNoTracking(), query).ToListAsync(cancellationToken);
        var rows = new List<InventoryValuationReconciliationRecord>();
        var groupedMovements = movements.GroupBy(item =>
        {
            var movementPolicy = EffectivePolicy(policies, item.EffectiveDate);
            return ValuationScopeKey.From(item, movementPolicy?.ScopeMode ?? InventoryValuationScopeMode.WarehouseProductUom);
        });
        foreach (var group in groupedMovements)
        {
            var state = states.SingleOrDefault(item => item.CompanyId == group.Key.CompanyId && item.BranchId == group.Key.BranchId && item.WarehouseId == group.Key.WarehouseId && item.ProductId == group.Key.ProductId && item.UnitOfMeasureId == group.Key.UnitOfMeasureId && item.TrackingIdentity == group.Key.TrackingIdentity);
            var policy = state?.CurrentPolicyId is { } currentPolicyId
                ? policies.SingleOrDefault(item => item.Id == currentPolicyId)
                : group.Select(item => EffectivePolicy(policies, item.EffectiveDate)).Where(item => item is not null).Cast<InventoryValuationPolicyEntity>().OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.VersionNumber).FirstOrDefault();
            var movementIds = group.Select(item => item.Id).ToHashSet(); var groupEvents = events.Where(item => movementIds.Contains(item.MovementId)).ToArray();
            var physicalQuantity = group.Sum(item => item.Direction == InventoryMovementDirection.Inbound ? item.Quantity : -item.Quantity);
            var movementStatuses = group.Select(movement =>
            {
                var movementEvents = groupEvents.Where(item => item.MovementId == movement.Id).ToArray();
                return new
                {
                    MovementId = movement.Id,
                    HasApplied = movementEvents.Any(value => value.Status == InventoryValuationEventStatus.Applied),
                    HasPending = movementEvents.Any(value => value.Status == InventoryValuationEventStatus.Pending) || movementEvents.Length == 0,
                    HasBlocked = movementEvents.Any(value => value.Status == InventoryValuationEventStatus.Blocked),
                    OldestPendingSequence = movementEvents.Where(value => value.Status is InventoryValuationEventStatus.Pending or InventoryValuationEventStatus.Blocked).Select(value => (long?)value.LedgerSequence).Min() ?? movement.LedgerSequence
                };
            }).ToArray();
            var appliedMovementIds = movementStatuses.Where(item => item.HasApplied).Select(item => item.MovementId).ToHashSet();
            var pendingMovementStatuses = movementStatuses.Where(item => !item.HasApplied && item.HasPending).ToArray();
            var blockedMovementStatuses = movementStatuses.Where(item => !item.HasApplied && item.HasBlocked).ToArray();
            var valuedQuantity = state?.Quantity ?? 0m;
            // Quantity is a physical ledger fact, not a monetary amount. Both
            // values are persisted at decimal(28,8), so compare the exact
            // stored decimal facts without AmountScale or a currency
            // tolerance.
            var quantityDifference = physicalQuantity - valuedQuantity;
            var appliedHandoffCount = handoffs.Count(item => appliedMovementIds.Contains(item.MovementId) && item.Status == InventoryFinanceValuationHandoffStatus.ReadyForFinance);
            var financeHandoffStatus = appliedMovementIds.Count == 0
                ? InventoryFinanceValuationHandoffStatus.Pending
                : appliedHandoffCount == appliedMovementIds.Count
                    ? InventoryFinanceValuationHandoffStatus.ReadyForFinance
                    : InventoryFinanceValuationHandoffStatus.Pending;
            var valuedAmount = state?.Value ?? 0m;
            var valuationMismatch = valuedQuantity < 0m || valuedAmount < 0m || valuedQuantity == 0m && valuedAmount != 0m;
            var status = valuationMismatch ? InventoryValuationReconciliationStatus.ValuationMismatch : blockedMovementStatuses.Length > 0 ? InventoryValuationReconciliationStatus.Blocked : pendingMovementStatuses.Length > 0 || policy is null ? InventoryValuationReconciliationStatus.PendingValuation : quantityDifference != 0m ? InventoryValuationReconciliationStatus.QuantityMismatch : financeHandoffStatus != InventoryFinanceValuationHandoffStatus.ReadyForFinance ? InventoryValuationReconciliationStatus.FinanceHandoffPending : InventoryValuationReconciliationStatus.Reconciled;
            var inTransit = await ReadInTransitAsync(db, group.Key, events, policies, cancellationToken);
            var differenceReason = status == InventoryValuationReconciliationStatus.ValuationMismatch
                ? valuedQuantity == 0m && valuedAmount != 0m
                    ? "valuation_state_zero_quantity_non_zero_value"
                    : "valuation_state_negative_quantity_or_value"
                : status == InventoryValuationReconciliationStatus.QuantityMismatch
                    ? "physical_quantity_differs_from_valued_state"
                    : status == InventoryValuationReconciliationStatus.FinanceHandoffPending
                        ? "finance_handoff_evidence_pending"
                        : status == InventoryValuationReconciliationStatus.PendingValuation ? "valuation_evidence_pending" : null;
            rows.Add(new InventoryValuationReconciliationRecord(context.TenantId.Value, group.Key.CompanyId, group.Key.BranchId, group.Key.WarehouseId, group.Key.ProductId, group.Key.UnitOfMeasureId, string.IsNullOrEmpty(group.Key.TrackingIdentity) ? null : group.Key.TrackingIdentity, state?.FunctionalCurrencyCode ?? policy?.FunctionalCurrencyCode ?? string.Empty, state?.CurrentPolicyId ?? policy?.Id, status, physicalQuantity, valuedQuantity, quantityDifference, valuedAmount, state?.AverageUnitCost ?? 0m, group.Max(item => (long?)item.LedgerSequence), state?.LastAppliedLedgerSequence ?? 0L, group.Count(), appliedMovementIds.Count, pendingMovementStatuses.Length, blockedMovementStatuses.Length, movementStatuses.Where(item => !item.HasApplied).Select(item => (long?)item.OldestPendingSequence).Min(), inTransit.Quantity, inTransit.Value, inTransit.Status, financeHandoffStatus, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, differenceReason));
        }
        return rows;
    }

    public async Task<InventoryPersistenceResult<InventoryValuationSummaryRecord>> SummaryAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await ReconcileAsync(context, query, cancellationToken);
        var asOf = DateTimeOffset.UtcNow;
        if (rows.Count == 0)
        {
            await using var emptyDb = CreateContext(context);
            var policy = await emptyDb.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.IsActive && item.EffectiveFrom <= DateOnly.FromDateTime(asOf.UtcDateTime) && (item.EffectiveTo == null || item.EffectiveTo >= DateOnly.FromDateTime(asOf.UtcDateTime))).OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            return InventoryPersistenceResult<InventoryValuationSummaryRecord>.Success(new InventoryValuationSummaryRecord(context.TenantId.Value, query.CompanyId, query.BranchId, query.WarehouseId, policy?.FunctionalCurrencyCode ?? string.Empty, 0m, 0m, 0m, 0, 0, 0m, 0m, InventoryInTransitValuationStatus.Ready, InventoryValuationReconciliationStatus.Reconciled, null, null, true, false, asOf, asOf));
        }

        var status = rows.Any(item => item.Status == InventoryValuationReconciliationStatus.ValuationMismatch) ? InventoryValuationReconciliationStatus.ValuationMismatch
            : rows.Any(item => item.Status == InventoryValuationReconciliationStatus.Blocked) ? InventoryValuationReconciliationStatus.Blocked
            : rows.Any(item => item.Status == InventoryValuationReconciliationStatus.PendingValuation) ? InventoryValuationReconciliationStatus.PendingValuation
            : rows.Any(item => item.Status == InventoryValuationReconciliationStatus.QuantityMismatch) ? InventoryValuationReconciliationStatus.QuantityMismatch
            : rows.Any(item => item.Status == InventoryValuationReconciliationStatus.FinanceHandoffPending) ? InventoryValuationReconciliationStatus.FinanceHandoffPending
            : InventoryValuationReconciliationStatus.Reconciled;
        var complete = status == InventoryValuationReconciliationStatus.Reconciled && rows.All(item => item.InTransitValueStatus == InventoryInTransitValuationStatus.Ready);
        return InventoryPersistenceResult<InventoryValuationSummaryRecord>.Success(new InventoryValuationSummaryRecord(
            context.TenantId.Value,
            query.CompanyId,
            query.BranchId,
            query.WarehouseId,
            rows.Select(item => item.FunctionalCurrencyCode).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? string.Empty,
            rows.Sum(item => item.PhysicalOnHandQuantity),
            rows.Sum(item => item.ValuedQuantity),
            rows.Sum(item => item.ValuedAmount),
            rows.Sum(item => item.PendingMovementCount),
            rows.Sum(item => item.BlockedMovementCount),
            rows.Sum(item => item.InTransitQuantity),
            rows.Sum(item => item.InTransitValue),
            rows.Any(item => item.InTransitValueStatus == InventoryInTransitValuationStatus.Pending) ? InventoryInTransitValuationStatus.Pending : InventoryInTransitValuationStatus.Ready,
            status,
            rows.Max(item => item.LatestLedgerSequence),
            rows.Select(item => (long?)item.LastAppliedLedgerSequence).Where(item => item.HasValue && item.Value > 0).Max(),
            complete,
            !complete,
            asOf,
            rows.Max(item => item.FreshAsOf)));
    }

    public async Task<IReadOnlyList<InventoryFinanceValuationHandoffRecord>> ListFinanceHandoffsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await ApplyHandoffScope(db.FinanceValuationHandoffs.AsNoTracking(), query).OrderByDescending(item => item.LedgerSequence).ToListAsync(cancellationToken); return values.Select(ToHandoff).ToArray();
    }

    public async Task<Guid?> ResolveFinanceHandoffCompanyIdAsync(InventoryRequestContext context, Guid handoffId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        return await db.FinanceValuationHandoffs.AsNoTracking()
            .Where(item => item.Id == handoffId)
            .Select(item => (Guid?)item.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<InventoryValuationExportRecord> ExportAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var asOf = DateTimeOffset.UtcNow;
        var events = await ApplyScope(db.MovementValuationEvents.AsNoTracking(), query)
            .OrderBy(item => item.LedgerSequence)
            .ThenBy(item => item.Id)
            .Take(10_000)
            .ToListAsync(cancellationToken);
        var policyIds = events.Select(item => item.PolicyId).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var policy = query.PolicyId.HasValue
            ? await db.ValuationPolicies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == query.PolicyId.Value, cancellationToken)
            : policyIds.Length == 1
                ? await db.ValuationPolicies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == policyIds[0], cancellationToken)
                : await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.IsActive).OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        var functionalCurrency = policy?.FunctionalCurrencyCode ?? events.Select(item => item.FunctionalCurrencyCode).FirstOrDefault() ?? string.Empty;
        var requestFingerprint = JsonSerializer.Serialize(query);
        var freshAsOf = DateTimeOffset.UtcNow;
        var content = new StringBuilder()
            .AppendLine("# export=inventory-valuation")
            .AppendLine($"# tenantId={Csv(context.TenantId.Value)}")
            .AppendLine($"# filters={Csv(requestFingerprint)}")
            .AppendLine($"# asOf={Csv(asOf.ToString("O", CultureInfo.InvariantCulture))}")
            .AppendLine($"# freshness={Csv(freshAsOf.ToString("O", CultureInfo.InvariantCulture))}")
            .AppendLine($"# functionalCurrency={Csv(functionalCurrency)}")
            .AppendLine($"# policyId={Csv(policy?.Id.ToString("D"))}")
            .AppendLine($"# policyVersion={Csv(policy?.VersionNumber.ToString(CultureInfo.InvariantCulture))}")
            .AppendLine($"# generatedActor={Csv(context.ActorId.ToString("D"))}")
            .AppendLine($"# generatedCorrelation={Csv(context.CorrelationId?.Value)}")
            .AppendLine("LedgerSequence,MovementId,SourceType,SourceDocumentId,SourceLineId,Status,StatusCode,CompanyId,BranchId,WarehouseId,ProductId,UnitOfMeasureId,TrackingIdentity,PolicyId,PolicyVersion,FunctionalCurrency,Quantity,Direction,TransactionUnitCost,TransactionCurrency,ExchangeRate,BaseUnitCost,PriorQuantity,PriorValue,NewQuantity,NewValue,MovementValue,FormulaMovementValue,RoundingAdjustmentAmount,EffectiveOn,CorrectionOfValuationEventId,SourceRevisionId,PendingReason,CorrelationId,ActorId,OccurredAt");
        foreach (var item in events)
        {
            content.AppendLine(string.Join(',',
                Csv(item.LedgerSequence.ToString(CultureInfo.InvariantCulture)), Csv(item.MovementId.ToString("D")), Csv(item.SourceType), Csv(item.SourceDocumentId.ToString("D")), Csv(item.SourceLineId.ToString("D")), Csv(item.Status), Csv(item.StatusCode), Csv(item.CompanyId.ToString("D")), Csv(item.BranchId?.ToString("D")), Csv(item.WarehouseId.ToString("D")), Csv(item.ProductId.ToString("D")), Csv(item.UnitOfMeasureId.ToString("D")), Csv(string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity), Csv(item.PolicyId?.ToString("D")), Csv(item.PolicyVersionNumber?.ToString(CultureInfo.InvariantCulture)), Csv(item.FunctionalCurrencyCode), Csv(item.Quantity.ToString(CultureInfo.InvariantCulture)), Csv(item.Direction), Csv(item.TransactionUnitCost?.ToString(CultureInfo.InvariantCulture)), Csv(item.TransactionCurrencyCode), Csv(item.ExchangeRate?.ToString(CultureInfo.InvariantCulture)), Csv(item.BaseUnitCost?.ToString(CultureInfo.InvariantCulture)), Csv(item.PriorQuantity.ToString(CultureInfo.InvariantCulture)), Csv(item.PriorValue.ToString(CultureInfo.InvariantCulture)), Csv(item.NewQuantity.ToString(CultureInfo.InvariantCulture)), Csv(item.NewValue.ToString(CultureInfo.InvariantCulture)), Csv(item.MovementValue?.ToString(CultureInfo.InvariantCulture)), Csv(item.FormulaMovementValue?.ToString(CultureInfo.InvariantCulture)), Csv(item.RoundingAdjustmentAmount?.ToString(CultureInfo.InvariantCulture)), Csv(item.EffectiveOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(item.CorrectionOfValuationEventId?.ToString("D")), Csv(item.SourceRevisionId?.ToString("D")), Csv(item.PendingReason), Csv(item.CorrelationId), Csv(item.ActorId.ToString("D")), Csv(item.OccurredAt.ToString("O", CultureInfo.InvariantCulture))));
        }

        var exportId = Guid.NewGuid();
        db.Audit.Add(new InventoryAuditEntity(context.TenantId, exportId, "valuation-export", exportId, "inventory.valuation.export", context.ActorId, context.SessionId, context.AuthorizationPath.ToString(), "Succeeded", null, context.CorrelationId?.Value ?? string.Empty, null, requestFingerprint, null, $"rows:{events.Count};asOf:{asOf:O};functionalCurrency:{functionalCurrency};policy:{policy?.Id:D};policyVersion:{policy?.VersionNumber}", asOf));
        await db.SaveChangesAsync(cancellationToken);
        return new InventoryValuationExportRecord($"inventory-valuation-{asOf:yyyyMMddHHmmss}.csv", "text/csv; charset=utf-8", content.ToString(), context.TenantId.Value, query.CompanyId, functionalCurrency, policy?.Id, policy?.VersionNumber, asOf, freshAsOf, context.ActorId, context.CorrelationId?.Value ?? string.Empty);
    }

    public Task<InventoryPersistenceResult<InventoryMovementValuationEventRecord>> CorrectAsync(InventoryRequestContext context, InventoryValuationCorrectionCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(InventoryPersistenceResult<InventoryMovementValuationEventRecord>.Denied(InventoryPersistenceOutcome.Conflict, "authoritative_source_revision_provider_required"));

    private async Task<CostResolution> ResolveCostAsync(InventoryRequestContext context, InventoryDbContext db, InventoryStockMovementEntity movement, InventoryValuationPolicyEntity policy, InventoryValuationStateEntity state, CancellationToken cancellationToken)
    {
        decimal? transactionCost = movement.UnitCost; string? transactionCurrency = movement.CurrencyCode;
        if (movement.SourceType == InventoryMovementSourceType.GoodsReceipt && movement.PurchaseOrderId.HasValue && movement.PurchaseOrderLineId.HasValue && goodsReceipts is not null && purchaseOrders is not null)
        {
            var purchaseOrder = await purchaseOrders.FindAsync(context.TenantContext, movement.PurchaseOrderId.Value, cancellationToken);
            var line = purchaseOrder?.Lines.FirstOrDefault(item => item.Id == movement.PurchaseOrderLineId.Value);
            transactionCost = line?.UnitPrice; transactionCurrency = purchaseOrder?.Source.Currency.Code;
        }
        else if ((movement.SourceType is InventoryMovementSourceType.WarehouseTransferReceipt or InventoryMovementSourceType.WarehouseTransferLoss or InventoryMovementSourceType.WarehouseTransferReturn) && movement.TransferId.HasValue)
        {
            var shipment = await db.StockMovements.AsNoTracking().Where(item => item.TransferId == movement.TransferId && item.SourceType == InventoryMovementSourceType.WarehouseTransferShipment).OrderBy(item => item.LedgerSequence).FirstOrDefaultAsync(cancellationToken);
            if (shipment is not null)
            {
                var shipmentEvent = await db.MovementValuationEvents.AsNoTracking().Where(item => item.MovementId == shipment.Id && item.Status == InventoryValuationEventStatus.Applied).OrderByDescending(item => item.LedgerSequence).FirstOrDefaultAsync(cancellationToken);
                transactionCost = shipmentEvent?.BaseUnitCost; transactionCurrency = policy.FunctionalCurrencyCode;
                if (shipmentEvent is null) return CostResolution.Pending("transfer_shipment_valuation_pending");
                return CostResolution.Success(shipmentEvent.BaseUnitCost ?? 0m, null, null, null, null, null, null, "transfer_inherited", shipmentEvent.TransactionUnitCost ?? shipmentEvent.BaseUnitCost, shipmentEvent.FunctionalCurrencyCode);
            }
            return CostResolution.Pending("transfer_shipment_not_found");
        }
        else if (movement.Direction == InventoryMovementDirection.Outbound && movement.SourceType == InventoryMovementSourceType.SupplierReturn && policy.SupplierReturnCostBasis == "LinkedReceiptValuation")
        {
            var receipt = await db.StockMovements.AsNoTracking().Where(item => item.GoodsReceiptLineId == movement.GoodsReceiptLineId && item.SourceType == InventoryMovementSourceType.GoodsReceipt).FirstOrDefaultAsync(cancellationToken);
            var receiptEvent = receipt is null ? null : await db.MovementValuationEvents.AsNoTracking().Where(item => item.MovementId == receipt.Id && item.Status == InventoryValuationEventStatus.Applied).FirstOrDefaultAsync(cancellationToken);
            if (receiptEvent is null) return CostResolution.Pending("linked_receipt_valuation_pending");
            return CostResolution.Success(receiptEvent.BaseUnitCost ?? 0m, null, null, null, null, null, null, "linked_receipt", receiptEvent.TransactionUnitCost ?? receiptEvent.BaseUnitCost, receiptEvent.FunctionalCurrencyCode);
        }

        if (movement.Direction == InventoryMovementDirection.Inbound
            && movement.SourceType is InventoryMovementSourceType.StockAdjustment or InventoryMovementSourceType.InventoryCountVariance
            && policy.PositiveAdjustmentCostBasis == "CurrentMovingAverage")
        {
            if (state.Quantity <= 0m)
                return CostResolution.Pending("current_moving_average_unavailable", movement.UnitCost, movement.CurrencyCode);

            return CostResolution.Success(state.AverageUnitCost, null, null, null, null, null, null, "current_moving_average", movement.UnitCost, movement.CurrencyCode);
        }

        if (movement.Direction == InventoryMovementDirection.Outbound || (movement.Direction == InventoryMovementDirection.Inbound && !transactionCost.HasValue))
        {
            if (movement.SourceType == InventoryMovementSourceType.CustomerReturn && movement.Direction == InventoryMovementDirection.Inbound && !transactionCost.HasValue)
                return CostResolution.Pending("customer_return_original_delivery_valuation_required");
            if (movement.Direction == InventoryMovementDirection.Inbound && movement.SourceType is (InventoryMovementSourceType.StockAdjustment or InventoryMovementSourceType.InventoryCountVariance or InventoryMovementSourceType.CustomerReturn) && policy.PositiveAdjustmentCostBasis != "CurrentMovingAverage") return CostResolution.Pending("positive_movement_cost_basis_not_configured", movement.UnitCost, movement.CurrencyCode);
            if (state.Quantity <= 0m) return CostResolution.Pending("current_moving_average_unavailable");
            return CostResolution.Success(state.AverageUnitCost, null, null, null, null, null, null, "current_moving_average", movement.UnitCost, movement.CurrencyCode);
        }
        if (!transactionCost.HasValue || string.IsNullOrWhiteSpace(transactionCurrency)) return CostResolution.Pending("transaction_cost_or_currency_missing", transactionCost, transactionCurrency);
        var fx = await ResolveExchangeRateAsync(context, transactionCurrency!, policy.FunctionalCurrencyCode, movement.EffectiveDate, cancellationToken);
        if (!fx.Succeeded) return fx with { TransactionUnitCost = transactionCost, TransactionCurrencyCode = transactionCurrency };
        var baseUnitCost = MovingWeightedAverageCalculator.Round(transactionCost.Value * fx.ExchangeRate!.Value, policy.UnitCostScale, policy.RoundingMode);
        return CostResolution.Success(baseUnitCost, fx.ExchangeRateId, fx.ExchangeRateVersionId, fx.ExchangeRateVersionNumber, fx.ExchangeRate, fx.ExchangeRateScale, fx.ExchangeRateProvenance, "source_cost", transactionCost, transactionCurrency);
    }

    private static async Task<CorrectionResolution> ResolveCorrectionAsync(InventoryRequestContext context, InventoryDbContext db, InventoryStockMovementEntity movement, InventoryValuationPolicyEntity policy, CancellationToken cancellationToken)
    {
        var originalMovement = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(item => item.Id == movement.CorrectionOfMovementId, cancellationToken);
        if (originalMovement is null)
            return CorrectionResolution.Blocked("correction_source_movement_missing");

        var originalEvent = db.MovementValuationEvents.Local
            .Where(item => item.MovementId == originalMovement.Id && item.Status == InventoryValuationEventStatus.Applied)
            .OrderByDescending(item => item.LedgerSequence)
            .FirstOrDefault();
        originalEvent ??= await db.MovementValuationEvents.AsNoTracking()
            .Where(item => item.MovementId == originalMovement.Id && item.Status == InventoryValuationEventStatus.Applied)
            .OrderByDescending(item => item.LedgerSequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (originalEvent is null)
            return CorrectionResolution.Pending("correction_source_valuation_pending");
        if (originalEvent.Direction == movement.Direction)
            return CorrectionResolution.Blocked("correction_direction_must_reverse");
        if (originalEvent.MovementValue is null || originalEvent.Quantity <= 0m)
            return CorrectionResolution.Pending("correction_source_value_missing");
        if (movement.Quantity > originalEvent.Quantity)
            return CorrectionResolution.Blocked("correction_quantity_exceeds_original");

        var isFullReversal = movement.Quantity == originalEvent.Quantity;
        var originalMovementValue = Math.Abs(originalEvent.MovementValue.Value);
        var originalFormulaMovementValue = Math.Abs(originalEvent.FormulaMovementValue ?? originalEvent.MovementValue.Value);
        var reversalValue = isFullReversal
            ? originalMovementValue
            : MovingWeightedAverageCalculator.Round(originalMovementValue * movement.Quantity / originalEvent.Quantity, policy.AmountScale, policy.RoundingMode);
        var formulaReversalValue = isFullReversal
            ? originalFormulaMovementValue
            : MovingWeightedAverageCalculator.Round(originalFormulaMovementValue * movement.Quantity / originalEvent.Quantity, policy.AmountScale, policy.RoundingMode);
        var roundingAdjustmentAmount = isFullReversal
            ? originalEvent.RoundingAdjustmentAmount ?? MovingWeightedAverageCalculator.Round(reversalValue - formulaReversalValue, policy.AmountScale, policy.RoundingMode)
            : MovingWeightedAverageCalculator.Round(reversalValue - formulaReversalValue, policy.AmountScale, policy.RoundingMode);
        var baseUnitCost = originalEvent.BaseUnitCost
            ?? MovingWeightedAverageCalculator.Round(originalEvent.MovementValue.Value / originalEvent.Quantity, policy.UnitCostScale, policy.RoundingMode);
        var cost = CostResolution.Success(baseUnitCost, originalEvent.ExchangeRateId, originalEvent.ExchangeRateVersionId, originalEvent.ExchangeRateVersionNumber, originalEvent.ExchangeRate, originalEvent.ExchangeRateScale, originalEvent.ExchangeRateProvenance, "correction_reversal", originalEvent.TransactionUnitCost, originalEvent.TransactionCurrencyCode);
        return CorrectionResolution.Applied(originalEvent.Id, reversalValue, formulaReversalValue, roundingAdjustmentAmount, cost, isFullReversal);
    }

    private async Task<CostResolution> ResolveExchangeRateAsync(InventoryRequestContext context, string sourceCurrency, string targetCurrency, DateOnly effectiveOn, CancellationToken cancellationToken)
    {
        if (string.Equals(sourceCurrency.Trim(), targetCurrency.Trim(), StringComparison.OrdinalIgnoreCase)) return CostResolution.Success(1m, null, null, null, 1m, 0, "same_currency", "same_currency");
        if (exchangeRates is null) return CostResolution.Pending("exchange_rate_persistence_unavailable");
        IReadOnlyList<MasterDataExchangeRateRecord> rates;
        try { rates = await exchangeRates.ListExchangeRatesAsync(context.TenantContext, cancellationToken); }
        catch (InvalidOperationException) { return CostResolution.Pending("exchange_rate_persistence_unavailable"); }
        var candidates = rates.Where(rate => rate.LifecycleState == MasterDataLifecycleState.Active && string.Equals(rate.SourceCurrencyCode, sourceCurrency.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(rate.TargetCurrencyCode, targetCurrency.Trim(), StringComparison.OrdinalIgnoreCase)).SelectMany(rate => rate.Versions.Where(version => version.EffectiveFrom <= effectiveOn && (version.EffectiveTo is null || version.EffectiveTo >= effectiveOn)).Select(version => (rate, version))).ToArray();
        if (candidates.Length != 1) return CostResolution.Pending(candidates.Length == 0 ? "exchange_rate_missing" : "exchange_rate_ambiguous");
        var selected = candidates[0]; return CostResolution.Success(selected.version.Rate, selected.rate.Id, selected.version.Id, selected.version.VersionNumber, selected.version.Rate, selected.version.RateScale, selected.version.Provenance.ToString(), "exchange_rate");
    }

    private static void AddPendingEventIfMissing(
        InventoryDbContext db,
        List<InventoryMovementValuationEventEntity> existingEvents,
        InventoryRequestContext context,
        InventoryStockMovementEntity movement,
        InventoryValuationPolicyEntity? policy,
        InventoryValuationStateEntity? state,
        string statusCode,
        string? reason,
        InventoryValuationProcessCommand command,
        InventoryValuationEventStatus status = InventoryValuationEventStatus.Pending,
        decimal? baseUnitCost = null,
        CostResolution? cost = null,
        Guid? correctionOfValuationEventId = null)
    {
        if (existingEvents.Any(item => item.MovementId == movement.Id && item.Status == status && item.StatusCode == statusCode))
            return;

        var priorQuantity = state?.Quantity ?? 0m;
        var priorValue = state?.Value ?? 0m;
        var evidence = new InventoryMovementValuationEventEntity(
            context.TenantId,
            Guid.NewGuid(),
            movement.Id,
            movement.LedgerSequence,
            status,
            statusCode,
            movement,
            policy,
            priorQuantity,
            priorValue,
            priorQuantity,
            priorValue,
            null,
            null,
            null,
            baseUnitCost ?? cost?.BaseUnitCost,
            cost?.ExchangeRateId,
            cost?.ExchangeRateVersionId,
            cost?.ExchangeRateVersionNumber,
            cost?.ExchangeRate,
            cost?.ExchangeRateScale,
            cost?.ExchangeRateProvenance,
            correctionOfValuationEventId,
            null,
            false,
            reason,
            command.ActorId,
            command.CorrelationId,
            command.OccurredAt,
            cost?.TransactionUnitCost,
            cost?.TransactionCurrencyCode);
        db.MovementValuationEvents.Add(evidence);
        existingEvents.Add(evidence);
    }

    private static bool AreCompatible(InventoryValuationPolicyEntity left, InventoryValuationPolicyRequest right) =>
        string.Equals(left.FunctionalCurrencyCode, right.FunctionalCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase)
        && left.ScopeMode == right.ScopeMode
        && left.UnitCostScale == right.UnitCostScale
        && left.AmountScale == right.AmountScale
        && left.RoundingMode == right.RoundingMode;

    private static bool AreCompatible(InventoryValuationPolicyEntity left, InventoryValuationPolicyEntity right) =>
        string.Equals(left.FunctionalCurrencyCode, right.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)
        && left.ScopeMode == right.ScopeMode
        && left.UnitCostScale == right.UnitCostScale
        && left.AmountScale == right.AmountScale
        && left.RoundingMode == right.RoundingMode;

    private static InventoryValuationPolicyEntity? EffectivePolicy(IEnumerable<InventoryValuationPolicyEntity> policies, DateOnly date) =>
        policies.Where(item => IsEffective(item, date))
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenByDescending(item => item.VersionNumber)
            .FirstOrDefault();

    private static IQueryable<InventoryValuationStateEntity> ApplyScope(IQueryable<InventoryValuationStateEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.PolicyId.HasValue) query = query.Where(item => item.CurrentPolicyId == filter.PolicyId); if (!string.IsNullOrWhiteSpace(filter.FunctionalCurrencyCode)) query = query.Where(item => item.FunctionalCurrencyCode == filter.FunctionalCurrencyCode); return query;
    }

    private static IQueryable<InventoryMovementValuationEventEntity> ApplyScope(IQueryable<InventoryMovementValuationEventEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.SourceType.HasValue) query = query.Where(item => item.SourceType == filter.SourceType); if (filter.PolicyId.HasValue) query = query.Where(item => item.PolicyId == filter.PolicyId); if (!string.IsNullOrWhiteSpace(filter.FunctionalCurrencyCode)) query = query.Where(item => item.FunctionalCurrencyCode == filter.FunctionalCurrencyCode); if (filter.Status.HasValue) query = query.Where(item => item.Status == filter.Status); if (filter.FromLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence >= filter.FromLedgerSequence); if (filter.ToLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence <= filter.ToLedgerSequence); if (filter.EffectiveFrom.HasValue) query = query.Where(item => item.EffectiveOn >= filter.EffectiveFrom); if (filter.EffectiveTo.HasValue) query = query.Where(item => item.EffectiveOn <= filter.EffectiveTo); return query;
    }

    private static IQueryable<InventoryStockMovementEntity> ApplyMovementScope(IQueryable<InventoryStockMovementEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.SourceType.HasValue) query = query.Where(item => item.SourceType == filter.SourceType); if (filter.FromLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence >= filter.FromLedgerSequence); if (filter.ToLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence <= filter.ToLedgerSequence); if (filter.EffectiveFrom.HasValue) query = query.Where(item => item.EffectiveDate >= filter.EffectiveFrom); if (filter.EffectiveTo.HasValue) query = query.Where(item => item.EffectiveDate <= filter.EffectiveTo); return query;
    }

    private static IQueryable<InventoryFinanceValuationHandoffEntity> ApplyHandoffScope(IQueryable<InventoryFinanceValuationHandoffEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.SourceType.HasValue) query = query.Where(item => item.SourceType == filter.SourceType); if (filter.PolicyId.HasValue) query = query.Where(item => item.PolicyId == filter.PolicyId); if (!string.IsNullOrWhiteSpace(filter.FunctionalCurrencyCode)) query = query.Where(item => item.FunctionalCurrencyCode == filter.FunctionalCurrencyCode); if (filter.FromLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence >= filter.FromLedgerSequence); if (filter.ToLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence <= filter.ToLedgerSequence); return query;
    }

    private static bool IsEffective(InventoryValuationPolicyEntity policy, DateOnly date) => policy.EffectiveFrom <= date && (policy.EffectiveTo is null || policy.EffectiveTo >= date);

    private static string Csv(object? value) => $"\"{(value?.ToString() ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static async Task<(decimal Quantity, decimal Value, InventoryInTransitValuationStatus Status)> ReadInTransitAsync(InventoryDbContext db, ValuationScopeKey key, IReadOnlyList<InventoryMovementValuationEventEntity> events, IReadOnlyList<InventoryValuationPolicyEntity> policies, CancellationToken cancellationToken)
    {
        var transfersQuery = db.Transfers.AsNoTracking().Where(item => item.CompanyId == key.CompanyId && item.BranchId == key.BranchId && item.SourceWarehouseId == key.WarehouseId && item.ProductId == key.ProductId && item.UnitOfMeasureId == key.UnitOfMeasureId);
        if (!string.IsNullOrEmpty(key.TrackingIdentity))
            transfersQuery = transfersQuery.Where(item => item.TrackingIdentity == key.TrackingIdentity);
        var transfers = await transfersQuery.ToListAsync(cancellationToken);
        if (transfers.Count == 0)
            return (0m, 0m, InventoryInTransitValuationStatus.Ready);

        var transferIds = transfers.Select(item => item.Id).ToArray();
        var transferEvents = await db.TransferEvents.AsNoTracking().Where(item => transferIds.Contains(item.TransferId)).ToListAsync(cancellationToken);
        var transferMovements = await db.StockMovements.AsNoTracking().Where(item => item.TransferId.HasValue && transferIds.Contains(item.TransferId.Value)).ToListAsync(cancellationToken);
        decimal quantity = 0m;
        decimal value = 0m;
        var status = InventoryInTransitValuationStatus.Ready;
        foreach (var transfer in transfers)
        {
            var shipment = transferMovements.Where(item => item.TransferId == transfer.Id && item.SourceType == InventoryMovementSourceType.WarehouseTransferShipment).OrderBy(item => item.LedgerSequence).FirstOrDefault();
            if (shipment is null)
                continue;

            var transferHistory = transferEvents.Where(item => item.TransferId == transfer.Id).ToArray();
            var shipped = transferHistory.Where(item => item.EventType == InventoryTransferEventType.Shipped).Sum(item => item.Quantity);
            if (transferHistory.Any(item => item.EventType == InventoryTransferEventType.DirectCompleted))
                shipped = transfer.Quantity;
            if (shipped <= 0m)
                shipped = shipment.Quantity;
            var received = transferHistory.Where(item => item.EventType == InventoryTransferEventType.Received).Sum(item => item.Quantity);
            var loss = transferHistory.Where(item => item.EventType == InventoryTransferEventType.ShortageResolved).Sum(item => item.Quantity);
            var returned = transferMovements.Where(item => item.TransferId == transfer.Id && item.SourceType == InventoryMovementSourceType.WarehouseTransferReturn).Sum(item => item.Quantity);
            var remaining = Math.Max(0m, shipped - received - loss - returned);
            if (remaining == 0m)
                continue;

            quantity += remaining;
            var shipmentEvent = events.FirstOrDefault(item => item.MovementId == shipment.Id && item.Status == InventoryValuationEventStatus.Applied);
            shipmentEvent ??= await db.MovementValuationEvents.AsNoTracking().Where(item => item.MovementId == shipment.Id && item.Status == InventoryValuationEventStatus.Applied).OrderByDescending(item => item.LedgerSequence).FirstOrDefaultAsync(cancellationToken);
            if (shipmentEvent?.MovementValue is null || shipped <= 0m)
            {
                status = InventoryInTransitValuationStatus.Pending;
                continue;
            }

            var policy = EffectivePolicy(policies, shipment.EffectiveDate);
            var amountScale = policy?.AmountScale ?? shipmentEvent.AmountScale ?? 8;
            var roundingMode = policy?.RoundingMode ?? shipmentEvent.RoundingMode ?? InventoryValuationRoundingMode.ToEven;
            var shipmentUnitValue = Math.Abs(shipmentEvent.MovementValue.Value) / shipped;
            value += MovingWeightedAverageCalculator.Round(remaining * shipmentUnitValue, amountScale, roundingMode);
        }
        return (quantity, value, status);
    }

    private static InventoryValuationPolicyRecord ToPolicy(InventoryValuationPolicyEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.FunctionalCurrencyId, item.FunctionalCurrencyCode, item.ScopeMode, item.EffectiveFrom, item.EffectiveTo, item.VersionNumber, item.UnitCostScale, item.AmountScale, item.RoundingMode, item.GoodsReceiptCostBasis, item.PositiveAdjustmentCostBasis, item.SupplierReturnCostBasis, item.IsActive, item.Version, item.SupersedesPolicyId);
    private static InventoryValuationStateRecord ToState(InventoryValuationStateEntity item) => new(item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity, item.CurrentPolicyId, item.CurrentPolicyVersionNumber, item.FunctionalCurrencyCode, item.Quantity, item.Value, item.AverageUnitCost, item.LastAppliedLedgerSequence, item.UpdatedAt, item.Version);
    private static InventoryMovementValuationEventRecord ToEvent(InventoryMovementValuationEventEntity item) => new(item.Id, item.TenantId.Value, item.MovementId, item.SourceType, item.SourceDocumentId, item.SourceLineId, item.CorrectionOfMovementId, item.GoodsReceiptId, item.GoodsReceiptLineId, item.SupplierReturnId, item.SupplierReturnLineId, item.PurchaseOrderId, item.PurchaseOrderLineId, item.TransferId, item.TransferLineId, item.SourceReference, item.LedgerSequence, item.Status, item.StatusCode, item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity, item.PolicyId, item.PolicyVersionNumber, item.FunctionalCurrencyCode, item.Quantity, item.Direction, item.TransactionUnitCost, item.TransactionCurrencyCode, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.ExchangeRate, item.ExchangeRateScale, item.ExchangeRateProvenance, item.EffectiveOn, item.BaseUnitCost, item.PriorQuantity, item.PriorValue, item.NewQuantity, item.NewValue, item.MovementValue, item.FormulaMovementValue, item.RoundingAdjustmentAmount, item.UnitCostScale, item.AmountScale, item.RoundingMode, item.CorrectionOfValuationEventId, item.SourceRevisionId, item.IsBackdated, item.PendingReason, item.CorrelationId, item.ActorId, item.OccurredAt, item.Version);
    private static InventoryFinanceValuationHandoffRecord ToHandoff(InventoryFinanceValuationHandoffEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.MovementId, item.LedgerSequence, item.SourceType, item.SourceDocumentId, item.SourceLineId, item.ValuationEvidenceId, item.ValuationEvidenceVersion, item.Quantity, item.Direction, item.BaseUnitCost, item.BaseAmount, item.SignedBaseAmount, item.RoundingAdjustmentAmount, item.PolicyId, item.PolicyVersionNumber, item.FunctionalCurrencyCode, item.TransactionUnitCost, item.TransactionCurrencyCode, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.ExchangeRate, item.ExchangeRateScale, item.ExchangeRateProvenance, item.ProductId, item.UnitOfMeasureId, string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity, item.CorrectionOfMovementId, item.Status, item.ContractVersion, item.CorrelationId, item.AsOf, item.CreatedAt, item.Version);

    private readonly record struct ValuationScopeKey(Guid CompanyId, Guid? BranchId, Guid WarehouseId, Guid ProductId, Guid UnitOfMeasureId, string TrackingIdentity)
    {
        internal static ValuationScopeKey From(InventoryStockMovementEntity movement, InventoryValuationScopeMode mode) => new(movement.CompanyId, movement.BranchId, movement.WarehouseId, movement.ProductId, movement.UnitOfMeasureId, mode == InventoryValuationScopeMode.WarehouseProductUomTracking ? movement.TrackingIdentity ?? string.Empty : string.Empty);
    }
    private readonly record struct PhysicalPoolKey(Guid CompanyId, Guid? BranchId, Guid WarehouseId, Guid ProductId, Guid UnitOfMeasureId)
    {
        internal static PhysicalPoolKey From(InventoryStockMovementEntity movement) => new(movement.CompanyId, movement.BranchId, movement.WarehouseId, movement.ProductId, movement.UnitOfMeasureId);
    }
    private sealed record CostResolution(bool Succeeded, string Code, string? Reason, decimal? BaseUnitCost, Guid? ExchangeRateId, Guid? ExchangeRateVersionId, int? ExchangeRateVersionNumber, decimal? ExchangeRate, int? ExchangeRateScale, string? ExchangeRateProvenance, decimal? TransactionUnitCost, string? TransactionCurrencyCode)
    {
        internal static CostResolution Success(decimal baseUnitCost, Guid? id, Guid? versionId, int? versionNumber, decimal? rate, int? scale, string? provenance, string code, decimal? transactionUnitCost = null, string? transactionCurrencyCode = null) => new(true, code, null, baseUnitCost, id, versionId, versionNumber, rate, scale, provenance, transactionUnitCost, transactionCurrencyCode);
        internal static CostResolution Pending(string reason, decimal? transactionUnitCost = null, string? transactionCurrencyCode = null) => new(false, "valuation_pending", reason, null, null, null, null, null, null, null, transactionUnitCost, transactionCurrencyCode);
    }

    private sealed record CorrectionResolution(bool Succeeded, bool IsBlocked, string Code, string? Reason, Guid? OriginalValuationEventId, decimal ReversalValue, decimal FormulaReversalValue, decimal RoundingAdjustmentAmount, CostResolution? Cost, bool IsFullReversal)
    {
        internal static CorrectionResolution Pending(string reason) => new(false, false, "correction_pending", reason, null, 0m, 0m, 0m, null, false);
        internal static CorrectionResolution Blocked(string reason) => new(false, true, "correction_blocked", reason, null, 0m, 0m, 0m, null, false);
        internal static CorrectionResolution Applied(Guid originalValuationEventId, decimal reversalValue, decimal formulaReversalValue, decimal roundingAdjustmentAmount, CostResolution cost, bool isFullReversal) => new(true, false, "correction_reversal", null, originalValuationEventId, reversalValue, formulaReversalValue, roundingAdjustmentAmount, cost, isFullReversal);
    }
}

#pragma warning restore CS1591
