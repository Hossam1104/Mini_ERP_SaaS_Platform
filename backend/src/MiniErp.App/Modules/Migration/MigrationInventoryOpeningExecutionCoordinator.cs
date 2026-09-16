#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

internal sealed class MigrationInventoryOpeningExecutionCoordinator
{
    private const string InventoryOwner = "Migration";
    private const string InventorySystem = "MESP-141";

    private readonly IMigrationExecutionPersistence migration;
    private readonly IMigrationReferenceAuthority references;
    private readonly InventoryService inventory;
    private readonly InventoryValuationService valuation;
    private readonly IFinancePersistence finance;
    private readonly TimeProvider clock;

    public MigrationInventoryOpeningExecutionCoordinator(
        IMigrationExecutionPersistence migration,
        IMigrationReferenceAuthority references,
        InventoryService inventory,
        InventoryValuationService valuation,
        IFinancePersistence finance,
        TimeProvider? clock = null)
    {
        this.migration = migration;
        this.references = references;
        this.inventory = inventory;
        this.valuation = valuation;
        this.finance = finance;
        this.clock = clock ?? TimeProvider.System;
    }

    internal async Task<MigrationOwnerExecutionCoordinator.OwnerPreparationResult> PrepareAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        IReadOnlyList<MigrationExecutionPlanRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 || rows.Any(item => item.Parsed.Payload is not MigrationInventoryOpeningPayload))
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_inventory_opening_plan_invalid");
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null)
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("finance_request_context_unavailable");

        var preparedRows = new List<PreparedOpening>(rows.Count);
        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            var payload = (MigrationInventoryOpeningPayload)row.Parsed.Payload;
            if (payload.CompanyId is not { } companyId
                || payload.BranchId is not { }
                || payload.WarehouseId is not { } warehouseId
                || payload.ProductId is not { } productId
                || payload.UnitOfMeasureId is not { } unitId
                || payload.Quantity is not { } quantity
                || payload.UnitCost is not { } unitCost
                || payload.OpeningDate is not { } openingDate
                || string.IsNullOrWhiteSpace(payload.CurrencyCode)
                || string.IsNullOrWhiteSpace(payload.SourceLineReference))
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_execution_payload_invalid");

            var checks = await references.ValidateAsync(requestContext, row.Parsed, cancellationToken);
            var failedCheck = checks.FirstOrDefault(item => item.State is not (MigrationReferenceState.NotApplicable or MigrationReferenceState.Active));
            if (failedCheck is not null)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(failedCheck.Code);

            var financeReady = await finance.PreflightInventoryOpeningAsync(financeContext, companyId, openingDate, cancellationToken);
            if (!financeReady.Ready)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(financeReady.Code);
            if (!string.Equals(payload.CurrencyCode.Trim(), financeReady.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_inventory_opening_currency_not_functional");

            var scope = new InventoryScope(tenant.TenantId.Value, companyId, payload.BranchId, warehouseId);
            var valuationReady = await valuation.CheckOpeningPolicyForMigrationAsync(
                requestContext,
                scope,
                openingDate,
                cancellationToken);
            if (!valuationReady.Succeeded || valuationReady.Value is not { } policy)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(valuationReady.Code);
            if (!string.Equals(policy.FunctionalCurrencyCode, financeReady.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("inventory_finance_functional_currency_mismatch");

            var ownerId = StableId($"inventory-opening:{tenant.TenantId.Value:D}:{run.RunId:D}:{row.Staged.StagedRecordId:D}");
            var ownerRowId = StableId($"inventory-opening-row:{tenant.TenantId.Value:D}:{run.RunId:D}:{row.Staged.StagedRecordId:D}");
            var sourceReference = $"{run.RunId:D}/{row.Staged.StagedRecordId:D}";
            var request = new InventoryOpeningBalanceCreateRequest(
                companyId,
                payload.BranchId,
                warehouseId,
                openingDate,
                InventoryOwner,
                InventorySystem,
                run.CreatedAt,
                sourceReference,
                [new InventoryOpeningBalanceRowRequest(
                    productId,
                    unitId,
                    quantity,
                    unitCost,
                    financeReady.FunctionalCurrencyCode,
                    payload.TrackingIdentity,
                    payload.SourceLineReference)]);

            var opening = await inventory.FindOpeningBalanceForMigrationAsync(requestContext, ownerId, cancellationToken);
            if (opening is null)
            {
                var created = await inventory.CreateOpeningBalanceForMigrationAsync(
                    requestContext,
                    request,
                    ownerId,
                    ownerRowId,
                    OwnerKey(run.RunId, row.Staged.StagedRecordId, "create"),
                    cancellationToken);
                opening = created.Value ?? await inventory.FindOpeningBalanceForMigrationAsync(requestContext, ownerId, cancellationToken);
                if (opening is null || !created.Succeeded && !OwnerRecordMatches(opening, request, ownerRowId))
                    return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(created.Code);
            }
            if (!OwnerRecordMatches(opening, request, ownerRowId))
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("inventory_opening_identity_conflict");

            if (opening.Status == InventoryOpeningBalanceStatus.Draft)
            {
                var validated = await inventory.ValidateOpeningBalanceForMigrationAsync(
                    requestContext,
                    opening.Id,
                    OwnerKey(run.RunId, row.Staged.StagedRecordId, "validate"),
                    cancellationToken);
                opening = validated.Value ?? await inventory.FindOpeningBalanceForMigrationAsync(requestContext, ownerId, cancellationToken);
                if (opening is null || !validated.Succeeded && opening.Status != InventoryOpeningBalanceStatus.Validated)
                    return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(validated.Code);
            }
            if (opening.QuarantinedRowCount != 0
                || opening.ValidRowCount != 1
                || opening.Rows.Count != 1
                || opening.Rows[0].Status is not (InventoryOpeningRowStatus.Valid or InventoryOpeningRowStatus.Posted))
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("inventory_opening_row_quarantined");

            preparedRows.Add(new PreparedOpening(row, payload, request, opening, ownerRowId));
        }

        var batchId = StableId($"migration-inventory-opening-batch:{attempt.AttemptId:D}");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', rows.OrderBy(item => item.Staged.SourceSequence).Select(item => item.Staged.PayloadHash)))));
        var batch = new MigrationExecutionBatchRecord(
            batchId,
            tenant.TenantId,
            run.RunId,
            attempt.AttemptId,
            MigrationCanonicalRecordType.InventoryOpening,
            MigrationExecutionBatchState.Prepared,
            preparedRows[0].Opening.Id,
            fingerprint,
            clock.GetUtcNow(),
            null,
            null,
            run.CorrelationId.Value,
            Guid.NewGuid().ToByteArray());
        var savedBatch = await migration.CreateBatchAsync(tenant, new CreateMigrationExecutionBatchCommand(batch), cancellationToken);
        if (!savedBatch.Succeeded)
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(savedBatch.Code);

        foreach (var item in preparedRows)
        {
            var effect = new MigrationExecutionEffectRecord(
                StableId($"migration-inventory-opening-effect:{attempt.AttemptId:D}:{item.Row.Staged.StagedRecordId:D}"),
                tenant.TenantId,
                run.RunId,
                attempt.AttemptId,
                item.Row.Staged.StagedRecordId,
                item.Row.Staged.SourceSequence,
                MigrationCanonicalRecordType.InventoryOpening,
                item.Opening.Id,
                item.OwnerRowId,
                null,
                item.Payload.SourceLineReference,
                MigrationExecutionEffectDisposition.Prepared,
                null,
                clock.GetUtcNow(),
                null,
                null,
                run.CorrelationId.Value,
                Guid.NewGuid().ToByteArray());
            var savedEffect = await migration.CreateEffectAsync(tenant, new CreateMigrationExecutionEffectCommand(effect), cancellationToken);
            if (!savedEffect.Succeeded)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(savedEffect.Code);
        }

        return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Successful();
    }

    internal async Task<MigrationEconomicGroupResult> ExecuteAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        IReadOnlyList<MigrationExecutionPlanRow> rows,
        CancellationToken cancellationToken)
    {
        var batch = await migration.FindBatchAsync(tenant, run.RunId, attempt.AttemptId, MigrationCanonicalRecordType.InventoryOpening, cancellationToken);
        if (batch is null) return MigrationEconomicGroupResult.Failure("migration_execution_batch_not_found", false);
        if (batch.State == MigrationExecutionBatchState.Prepared)
        {
            var startedBatch = await migration.UpdateBatchAsync(tenant, new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Started, clock.GetUtcNow(), null, batch.Version), cancellationToken);
            if (!startedBatch.Succeeded) return MigrationEconomicGroupResult.Failure(startedBatch.Code, startedBatch.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        }

        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            var effect = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken))
                .SingleOrDefault(item => item.StagedRecordId == row.Staged.StagedRecordId);
            if (effect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_not_found", false);
            if (effect.Disposition == MigrationExecutionEffectDisposition.Unknown)
                return MigrationEconomicGroupResult.Failure(effect.SafeCode ?? "migration_execution_outcome_unknown", true);

            var opening = await inventory.FindOpeningBalanceForMigrationAsync(requestContext, effect.OwnerBatchId, cancellationToken);
            if (opening is null)
                return await FailEffectAsync(tenant, batch, effect, "inventory_opening_evidence_unavailable", unknown: effect.Disposition == MigrationExecutionEffectDisposition.Started, cancellationToken);
            var ownerRow = opening.Rows.SingleOrDefault(item => item.Id == effect.OwnerRowId);
            if (ownerRow is null || opening.TenantId != tenant.TenantId.Value || !OpeningMatchesPlan(opening, ownerRow, row))
                return await FailEffectAsync(tenant, batch, effect, "inventory_opening_owner_evidence_mismatch", unknown: effect.Disposition == MigrationExecutionEffectDisposition.Started, cancellationToken);

            var movements = await inventory.ListOpeningMovementsForMigrationAsync(requestContext, opening, cancellationToken);
            var movement = movements.SingleOrDefault(item => item.SourceLineId == ownerRow.Id);
            if (opening.Status == InventoryOpeningBalanceStatus.Posted)
            {
                if (movement is null || movements.Count != 1 || !MovementMatches(movement, opening, ownerRow))
                    return await FailEffectAsync(tenant, batch, effect, "inventory_posted_movement_unproven", unknown: true, cancellationToken);
                var result = await ContinueFromStockAsync(requestContext, tenant, attempt, effect, opening, ownerRow, movement, cancellationToken);
                if (!result.Succeeded)
                    return await StopRemainingAsync(tenant, batch, row.Staged.SourceSequence, result, cancellationToken);
                continue;
            }

            if (movement is not null || movements.Count != 0)
                return await FailEffectAsync(tenant, batch, effect, "inventory_unexpected_stock_movement", unknown: true, cancellationToken);
            if (opening.Status != InventoryOpeningBalanceStatus.Validated
                || opening.QuarantinedRowCount != 0
                || ownerRow.Status != InventoryOpeningRowStatus.Valid)
                return await FailEffectAsync(tenant, batch, effect, "inventory_opening_not_validated", unknown: false, cancellationToken);

            var ready = await PreflightAsync(requestContext, opening.CompanyId, opening.AsOfDate, ownerRow.CurrencyCode, cancellationToken);
            if (!ready.Succeeded)
                return await FailEffectAsync(tenant, batch, effect, ready.Code, unknown: false, cancellationToken);

            effect = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Started, null, null, null, clock.GetUtcNow(), null, cancellationToken);
            if (effect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
            InventoryOperationResult<InventoryOpeningBalanceRecord> posted;
            var postResponseLost = false;
            try
            {
                posted = await inventory.PostOpeningBalanceForMigrationAsync(
                    requestContext,
                    opening.Id,
                    OwnerKey(run.RunId, row.Staged.StagedRecordId, "post"),
                    cancellationToken);
            }
            catch (Exception)
            {
                postResponseLost = true;
                posted = InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("inventory_post_outcome_unknown");
            }

            try
            {
                opening = await inventory.FindOpeningBalanceForMigrationAsync(requestContext, opening.Id, CancellationToken.None);
            }
            catch (Exception)
            {
                return await FailEffectAsync(tenant, batch, effect, "inventory_post_outcome_unknown", unknown: true, CancellationToken.None);
            }
            if (opening is null)
                return await FailEffectAsync(tenant, batch, effect, "inventory_post_outcome_unknown", unknown: true, CancellationToken.None);
            ownerRow = opening.Rows.SingleOrDefault(item => item.Id == effect.OwnerRowId);
            try
            {
                movements = await inventory.ListOpeningMovementsForMigrationAsync(requestContext, opening, CancellationToken.None);
            }
            catch (Exception)
            {
                return await FailEffectAsync(tenant, batch, effect, "inventory_post_outcome_unknown", unknown: true, CancellationToken.None);
            }
            movement = movements.SingleOrDefault(item => item.SourceLineId == effect.OwnerRowId);
            if (opening.Status != InventoryOpeningBalanceStatus.Posted || movement is null || ownerRow is null || movements.Count != 1 || !MovementMatches(movement, opening, ownerRow))
            {
                if (opening.Status != InventoryOpeningBalanceStatus.Posted && movements.Count == 0)
                {
                    var unknown = postResponseLost || posted.Code is "persistence_unavailable" or "inventory_post_outcome_unknown";
                    return await FailEffectAsync(tenant, batch, effect, posted.Code, unknown, unknown ? CancellationToken.None : cancellationToken);
                }
                return await FailEffectAsync(tenant, batch, effect, "inventory_post_outcome_unknown", unknown: true, CancellationToken.None);
            }

            if (!await SaveRepresentationsAsync(tenant, attempt, effect, opening, ownerRow, movement, [], [], null, cancellationToken))
                return await FailEffectAsync(tenant, batch, effect, "migration_economic_evidence_persistence_unknown", unknown: true, cancellationToken);

            var continued = await ContinueFromStockAsync(requestContext, tenant, attempt, effect, opening, ownerRow, movement, cancellationToken);
            if (!continued.Succeeded)
                return await StopRemainingAsync(tenant, batch, row.Staged.SourceSequence, continued, cancellationToken);
        }

        var finalEffects = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken))
            .Where(item => item.RecordType == MigrationCanonicalRecordType.InventoryOpening)
            .ToArray();
        var batchState = finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)
            ? MigrationExecutionBatchState.Unknown
            : finalEffects.All(item => item.Disposition == MigrationExecutionEffectDisposition.Committed)
                ? MigrationExecutionBatchState.Completed
                : MigrationExecutionBatchState.Failed;
        await FinishBatchAsync(tenant, batch, batchState, cancellationToken);
        return batchState == MigrationExecutionBatchState.Completed
            ? MigrationEconomicGroupResult.Successful()
            : MigrationEconomicGroupResult.Failure("migration_execution_partially_completed", batchState == MigrationExecutionBatchState.Unknown);
    }

    internal async Task MarkPreparationFailedAsync(
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        string code,
        CancellationToken cancellationToken)
    {
        foreach (var effect in (await migration.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken))
            .Where(item => item.RecordType == MigrationCanonicalRecordType.InventoryOpening && item.Disposition == MigrationExecutionEffectDisposition.Prepared))
            await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Failed, null, null, code, null, clock.GetUtcNow(), cancellationToken);
        var batch = await migration.FindBatchAsync(tenant, attempt.RunId, attempt.AttemptId, MigrationCanonicalRecordType.InventoryOpening, cancellationToken);
        if (batch is not null)
            await FinishBatchAsync(tenant, batch, MigrationExecutionBatchState.Failed, cancellationToken);
    }

    internal async Task<IReadOnlyList<MigrationEconomicReconciliationRecord>> ReadReconciliationsAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        IReadOnlyList<MigrationExecutionEffectRecord> effects,
        IReadOnlyList<MigrationStagedRecord> staged,
        CancellationToken cancellationToken)
    {
        var stagedById = staged.ToDictionary(item => item.StagedRecordId);
        var results = new List<MigrationEconomicReconciliationRecord>();
        foreach (var effect in effects.Where(item => item.RecordType == MigrationCanonicalRecordType.InventoryOpening).OrderBy(item => item.SourceSequence))
        {
            var reconciledAt = clock.GetUtcNow();
            MigrationInventoryOpeningPayload? payload = null;
            if (stagedById.TryGetValue(effect.StagedRecordId, out var stagedRow))
            {
                try { payload = JsonSerializer.Deserialize<MigrationInventoryOpeningPayload>(stagedRow.CanonicalPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
                catch (JsonException) { }
            }
            if (payload?.Quantity is not { } canonicalQuantity || payload.UnitCost is not { } unitCost)
            {
                results.Add(new(effect.Id, effect.SourceSequence, "unavailable", "migration_inventory_opening_payload_invalid", false, false, false, payload?.Quantity, null, null, null, null, null, null, reconciledAt));
                continue;
            }

            var opening = await inventory.FindOpeningBalanceForMigrationAsync(requestContext, effect.OwnerBatchId, cancellationToken);
            var ownerRow = opening?.Rows.SingleOrDefault(item => item.Id == effect.OwnerRowId);
            if (opening is null || ownerRow is null)
            {
                results.Add(new(effect.Id, effect.SourceSequence, "unavailable", "inventory_opening_evidence_unavailable", false, false, false, canonicalQuantity, null, canonicalQuantity * unitCost, null, null, null, null, reconciledAt));
                continue;
            }

            var scope = new InventoryScope(tenant.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId);
            var movements = await inventory.ListOpeningMovementsForMigrationAsync(requestContext, opening, cancellationToken);
            var movement = movements.SingleOrDefault(item => item.SourceLineId == ownerRow.Id);
            var physical = movements.Count == 1
                && movement is not null
                && OpeningMatchesPayload(opening, ownerRow, payload)
                && MovementMatches(movement, opening, ownerRow)
                && movement.Quantity == canonicalQuantity;

            var valuationEvidence = movement is null
                ? null
                : await valuation.ReadOpeningEvidenceForMigrationAsync(requestContext, scope, movement.ProductId, movement.UnitOfMeasureId, movement.TrackingIdentity, cancellationToken);
            var events = valuationEvidence?.Events.Where(item => item.MovementId == movement!.Id && item.Status == InventoryValuationEventStatus.Applied).ToArray() ?? [];
            var handoffs = valuationEvidence?.Handoffs.Where(item => item.MovementId == movement!.Id).ToArray() ?? [];
            var valuationEvent = events.Length == 1 ? events[0] : null;
            var handoff = handoffs.Length == 1 ? handoffs[0] : null;
            var canonicalValue = canonicalQuantity * unitCost;
            var inventoryValue = valuationEvent?.MovementValue;
            var roundedCanonicalUnitCost = valuationEvent?.UnitCostScale is { } unitCostScale && valuationEvent.RoundingMode is { } unitCostRounding
                ? MovingWeightedAverageCalculator.Round(unitCost, unitCostScale, unitCostRounding)
                : (decimal?)null;
            var roundedFormulaValue = roundedCanonicalUnitCost is { } roundedUnitCost && valuationEvent?.AmountScale is { } amountScale && valuationEvent.RoundingMode is { } amountRounding
                ? MovingWeightedAverageCalculator.Round(canonicalQuantity * roundedUnitCost, amountScale, amountRounding)
                : (decimal?)null;
            var valuationProven = physical
                && valuationEvent is not null
                && valuationEvent.UnitCostScale is not null
                && valuationEvent.AmountScale is not null
                && valuationEvent.RoundingMode is not null
                && valuationEvent.BaseUnitCost == roundedCanonicalUnitCost
                && valuationEvent.FormulaMovementValue == roundedFormulaValue
                && valuationEvent.MovementValue == valuationEvent.FormulaMovementValue + (valuationEvent.RoundingAdjustmentAmount ?? 0m)
                && valuationEvent.FunctionalCurrencyCode is not null
                && string.Equals(valuationEvent.FunctionalCurrencyCode, handoff?.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)
                && handoff is not null
                && handoff.SignedBaseAmount == valuationEvent.MovementValue
                && handoff.RoundingAdjustmentAmount == (valuationEvent.RoundingAdjustmentAmount ?? 0m);

            decimal? financePostedAmount = null;
            var financeProven = false;
            var journalCode = "finance_journal_evidence_unavailable";
            var financeContext = FinanceContext(requestContext);
            if (financeContext is not null && handoff is not null && valuationEvent is not null)
            {
                var classifier = FinanceInventoryPostingClassifier.Classify(handoff.SourceType, handoff.Direction);
                var journals = (await finance.ListJournalsAsync(financeContext, opening.CompanyId, cancellationToken))
                    .Where(item => item.SourceContract == handoff.ContractVersion
                        && item.SourceEvent == classifier
                        && item.SourceEvidenceId == handoff.ValuationEvidenceId
                        && item.SourceEvidenceVersion == handoff.ValuationEvidenceVersion)
                    .ToArray();
                var journal = journals.Length == 1 ? journals[0] : null;
                if (journal is not null && journal.Status == FinanceJournalStatus.Posted)
                {
                    var expectedAmount = Math.Abs(handoff.SignedBaseAmount);
                    var debit = journal.Lines.Sum(item => item.FunctionalDebit);
                    var credit = journal.Lines.Sum(item => item.FunctionalCredit);
                    financeProven = journal.TenantId == tenant.TenantId.Value
                        && journal.CompanyId == opening.CompanyId
                        && journal.PostingDate == DateOnly.FromDateTime(handoff.AsOf.UtcDateTime)
                        && journal.AmountAuthority == FinanceJournalAmountAuthority.SourceFunctionalCurrency
                        && string.Equals(journal.FunctionalCurrencyCode, handoff.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)
                        && journal.Lines.Count == 2
                        && debit == expectedAmount
                        && credit == expectedAmount;
                    if (financeProven) financePostedAmount = debit;
                    else journalCode = "finance_journal_not_reconciled";
                }
                else if (journal?.Status == FinanceJournalStatus.Submitted)
                    journalCode = "finance_approval_pending";
                else if (journals.Length > 1)
                    journalCode = "finance_journal_evidence_ambiguous";
            }

            var fullyProven = physical && valuationProven && financeProven;
            var safeCode = fullyProven ? null
                : !physical ? "inventory_physical_quantity_not_proven"
                : !valuationProven ? "inventory_valuation_amount_not_proven"
                : journalCode;
            results.Add(new(effect.Id, effect.SourceSequence, fullyProven ? "reconciled" : "partial", safeCode, physical, valuationProven, financeProven,
                canonicalQuantity, movement?.Quantity, canonicalValue, inventoryValue, valuationEvent?.RoundingAdjustmentAmount, financePostedAmount,
                handoff?.FunctionalCurrencyCode, reconciledAt, valuationEvent?.UnitCostScale, valuationEvent?.AmountScale, valuationEvent?.RoundingMode.ToString()));
        }
        return results;
    }

    private async Task<MigrationEconomicGroupResult> ContinueFromStockAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        MigrationExecutionEffectRecord effect,
        InventoryOpeningBalanceRecord opening,
        InventoryOpeningBalanceRowRecord ownerRow,
        InventoryMovementRecord movement,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ContinueFromStockCoreAsync(requestContext, tenant, attempt, effect, opening, ownerRow, movement, cancellationToken);
        }
        catch (Exception)
        {
            return await MarkIncompleteAsync(tenant, effect, "inventory_economic_owner_outcome_unknown", unknown: true, CancellationToken.None);
        }
    }

    private async Task<MigrationEconomicGroupResult> ContinueFromStockCoreAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        MigrationExecutionEffectRecord effect,
        InventoryOpeningBalanceRecord opening,
        InventoryOpeningBalanceRowRecord ownerRow,
        InventoryMovementRecord movement,
        CancellationToken cancellationToken)
    {
        var scope = new InventoryScope(tenant.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId);
        var evidence = await valuation.ReadOpeningEvidenceForMigrationAsync(requestContext, scope, movement.ProductId, movement.UnitOfMeasureId, movement.TrackingIdentity, cancellationToken);
        if (evidence is null)
            return await MarkIncompleteAsync(tenant, effect, "inventory_valuation_evidence_unavailable", unknown: true, cancellationToken);

        var events = evidence.Value.Events.Where(item => item.MovementId == movement.Id).ToArray();
        var applied = events.Where(item => item.Status == InventoryValuationEventStatus.Applied).ToArray();
        if (applied.Length > 1)
            return await MarkIncompleteAsync(tenant, effect, "inventory_valuation_evidence_ambiguous", unknown: true, cancellationToken);
        if (applied.Length == 0)
        {
            var occurredAt = new DateTimeOffset(opening.AsOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            InventoryOperationResult<InventoryValuationProcessResult> processed;
            var processResponseLost = false;
            try
            {
                processed = await valuation.ProcessOpeningMovementsForMigrationAsync(
                    requestContext,
                    scope,
                    movement.ProductId,
                    movement.UnitOfMeasureId,
                    [movement.Id],
                    OwnerKey(attempt.RunId, effect.StagedRecordId, "valuation"),
                    occurredAt,
                    MigrationFingerprintEncoder.Compute("migration-inventory-opening-valuation-v1", effect.RunId.ToString("D"), effect.StagedRecordId.ToString("D"), movement.Id.ToString("D")),
                    cancellationToken);
            }
            catch (Exception)
            {
                processResponseLost = true;
                processed = InventoryOperationResult<InventoryValuationProcessResult>.Failure("inventory_valuation_outcome_unknown");
            }
            try
            {
                evidence = await valuation.ReadOpeningEvidenceForMigrationAsync(requestContext, scope, movement.ProductId, movement.UnitOfMeasureId, movement.TrackingIdentity, CancellationToken.None);
            }
            catch (Exception)
            {
                return await MarkIncompleteAsync(tenant, effect, "inventory_valuation_outcome_unknown", unknown: true, CancellationToken.None);
            }
            if (evidence is null)
                return await MarkIncompleteAsync(tenant, effect, "inventory_valuation_outcome_unknown", unknown: true, CancellationToken.None);
            events = evidence.Value.Events.Where(item => item.MovementId == movement.Id).ToArray();
            applied = events.Where(item => item.Status == InventoryValuationEventStatus.Applied).ToArray();
            if (applied.Length == 0)
            {
                var deterministic = !processResponseLost && processed.Code is "pending_predecessor" or "valuation_policy_not_configured" or "valuation_policy_transition_requires_rebaseline" or "cost_basis_not_configured" or "valuation_scope_blocked";
                await SaveRepresentationsAsync(tenant, attempt, effect, opening, ownerRow, movement, events, evidence.Value.Handoffs.Where(item => item.MovementId == movement.Id).ToArray(), null, cancellationToken);
                var unknown = processResponseLost || processed.Code == "persistence_unavailable" || !deterministic && !processed.Succeeded;
                return await MarkIncompleteAsync(tenant, effect, processed.Code, unknown, unknown ? CancellationToken.None : cancellationToken);
            }
        }

        if (applied.Length != 1)
            return await MarkIncompleteAsync(tenant, effect, "inventory_valuation_evidence_ambiguous", unknown: true, cancellationToken);
        var valuationEvent = applied[0];
        var handoffs = evidence.Value.Handoffs.Where(item => item.MovementId == movement.Id).ToArray();
        if (handoffs.Length != 1)
        {
            await SaveRepresentationsAsync(tenant, attempt, effect, opening, ownerRow, movement, events, handoffs, null, cancellationToken);
            return await MarkIncompleteAsync(tenant, effect, "inventory_finance_handoff_unavailable", unknown: false, cancellationToken);
        }
        var handoff = handoffs[0];
        if (handoff.ValuationEvidenceId != valuationEvent.Id
            || handoff.ValuationEvidenceVersion != 1
            || handoff.SourceDocumentId != opening.Id
            || handoff.SourceLineId != ownerRow.Id
            || handoff.ContractVersion != "inventory-valuation-finance.v1"
            || valuationEvent.MovementValue is not { } movementValue
            || handoff.SignedBaseAmount != movementValue)
        {
            await SaveRepresentationsAsync(tenant, attempt, effect, opening, ownerRow, movement, events, handoffs, null, cancellationToken);
            return await MarkIncompleteAsync(tenant, effect, "inventory_finance_handoff_evidence_mismatch", unknown: true, cancellationToken);
        }

        var financeContext = FinanceContext(requestContext);
        if (financeContext is null)
            return await MarkIncompleteAsync(tenant, effect, "finance_request_context_unavailable", unknown: false, cancellationToken);
        var preflight = await finance.PreflightInventoryOpeningAsync(financeContext, opening.CompanyId, DateOnly.FromDateTime(handoff.AsOf.UtcDateTime), cancellationToken);
        if (!preflight.Ready)
        {
            await SaveRepresentationsAsync(tenant, attempt, effect, opening, ownerRow, movement, events, handoffs, null, cancellationToken);
            return await MarkIncompleteAsync(tenant, effect, preflight.Code, unknown: false, cancellationToken);
        }
        if (!string.Equals(preflight.FunctionalCurrencyCode, handoff.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
            return await MarkIncompleteAsync(tenant, effect, "inventory_finance_functional_currency_mismatch", unknown: true, cancellationToken);

        var handoffJournalCandidates = (await finance.ListJournalsAsync(financeContext, opening.CompanyId, cancellationToken))
            .Where(item => item.SourceContract == handoff.ContractVersion
                && item.SourceEvent == FinanceInventoryPostingClassifier.Classify(handoff.SourceType, handoff.Direction)
                && item.SourceEvidenceId == handoff.ValuationEvidenceId
                && item.SourceEvidenceVersion == handoff.ValuationEvidenceVersion)
            .ToArray();
        if (handoffJournalCandidates.Length > 1)
            return await MarkIncompleteAsync(tenant, effect, "finance_journal_evidence_ambiguous", unknown: true, cancellationToken);
        var journal = handoffJournalCandidates.SingleOrDefault();
        if (journal?.Status != FinanceJournalStatus.Posted)
        {
            FinanceOperationResult<FinanceJournalRecord> processed;
            var processResponseLost = false;
            try
            {
                processed = await finance.ProcessHandoffAsync(
                    financeContext,
                    new FinanceHandoffProcessCommand(
                        handoff.Id,
                        OwnerKey(attempt.RunId, effect.StagedRecordId, "finance"),
                        MigrationFingerprintEncoder.Compute("migration-inventory-opening-finance-v1", effect.RunId.ToString("D"), effect.StagedRecordId.ToString("D"), handoff.ValuationEvidenceId.ToString("D"), handoff.ValuationEvidenceVersion.ToString())),
                    cancellationToken);
            }
            catch (Exception)
            {
                processResponseLost = true;
                processed = FinanceOperationResult<FinanceJournalRecord>.Failure("finance_handoff_outcome_unknown");
            }
            if (!processed.Succeeded)
            {
                IReadOnlyList<FinanceJournalRecord> journals;
                try
                {
                    journals = await finance.ListJournalsAsync(financeContext, opening.CompanyId, CancellationToken.None);
                }
                catch (Exception)
                {
                    return await MarkIncompleteAsync(tenant, effect, "finance_handoff_outcome_unknown", unknown: true, CancellationToken.None);
                }
                handoffJournalCandidates = journals.Where(item => item.SourceContract == handoff.ContractVersion
                    && item.SourceEvent == FinanceInventoryPostingClassifier.Classify(handoff.SourceType, handoff.Direction)
                    && item.SourceEvidenceId == handoff.ValuationEvidenceId
                    && item.SourceEvidenceVersion == handoff.ValuationEvidenceVersion).ToArray();
                if (handoffJournalCandidates.Length > 1)
                    return await MarkIncompleteAsync(tenant, effect, "finance_journal_evidence_ambiguous", unknown: true, cancellationToken);
                journal = handoffJournalCandidates.SingleOrDefault();
                if (journal is null)
                {
                    await SaveRepresentationsAsync(tenant, attempt, effect, opening, ownerRow, movement, events, handoffs, null, cancellationToken);
                    var unknown = processResponseLost || processed.Code is "persistence_unavailable" or "finance_handoff_outcome_unknown";
                    return await MarkIncompleteAsync(tenant, effect, processed.Code, unknown, unknown ? CancellationToken.None : cancellationToken);
                }
            }
            else
            {
                journal = processed.Value;
            }
        }

        if (journal is null)
            return await MarkIncompleteAsync(tenant, effect, "finance_journal_evidence_unavailable", unknown: true, cancellationToken);
        var financeAmount = Math.Abs(handoff.SignedBaseAmount);
        var postedAmountMatches = journal.Status == FinanceJournalStatus.Posted
            && journal.AmountAuthority == FinanceJournalAmountAuthority.SourceFunctionalCurrency
            && string.Equals(journal.FunctionalCurrencyCode, handoff.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)
            && journal.Lines.Count == 2
            && journal.Lines.Sum(item => item.FunctionalDebit) == financeAmount
            && journal.Lines.Sum(item => item.FunctionalCredit) == financeAmount;
        if (!await SaveRepresentationsAsync(tenant, attempt, effect, opening, ownerRow, movement, events, handoffs, journal, cancellationToken))
            return await MarkIncompleteAsync(tenant, effect, "migration_economic_evidence_persistence_unknown", unknown: true, cancellationToken);
        if (!postedAmountMatches)
            return await MarkIncompleteAsync(tenant, effect, journal.Status == FinanceJournalStatus.Submitted ? "finance_approval_pending" : "finance_journal_not_posted_or_unreconciled", unknown: false, cancellationToken);

        var completed = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Committed, ownerRow.Id, opening.Id, null, null, clock.GetUtcNow(), cancellationToken);
        return completed is null
            ? MigrationEconomicGroupResult.Failure("migration_execution_effect_outcome_unknown", true)
            : MigrationEconomicGroupResult.Successful();
    }

    private async Task<PreflightResult> PreflightAsync(
        FoundationRequestContext requestContext,
        Guid companyId,
        DateOnly openingDate,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null)
            return new(false, "finance_request_context_unavailable");
        var financeReady = await finance.PreflightInventoryOpeningAsync(financeContext, companyId, openingDate, cancellationToken);
        if (!financeReady.Ready) return new(false, financeReady.Code);
        if (!string.Equals(currencyCode, financeReady.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
            return new(false, "migration_inventory_opening_currency_not_functional");
        return new(true, "ready");
    }

    private async Task<MigrationEconomicGroupResult> StopRemainingAsync(
        TenantContext tenant,
        MigrationExecutionBatchRecord batch,
        int afterSequence,
        MigrationEconomicGroupResult result,
        CancellationToken cancellationToken)
    {
        var stateToken = CancellationToken.None;
        foreach (var item in (await migration.ListEffectsAsync(tenant, batch.RunId, batch.AttemptId, stateToken))
            .Where(effect => effect.RecordType == MigrationCanonicalRecordType.InventoryOpening
                && effect.SourceSequence > afterSequence
                && effect.Disposition == MigrationExecutionEffectDisposition.Prepared))
            await ChangeEffectAsync(tenant, item, MigrationExecutionEffectDisposition.Failed, null, null, "not_attempted_after_prior_failure", null, clock.GetUtcNow(), stateToken);
        await FinishBatchAsync(tenant, batch, result.Unknown ? MigrationExecutionBatchState.Unknown : MigrationExecutionBatchState.Failed, stateToken);
        return result;
    }

    private async Task<MigrationEconomicGroupResult> FailEffectAsync(
        TenantContext tenant,
        MigrationExecutionBatchRecord batch,
        MigrationExecutionEffectRecord effect,
        string code,
        bool unknown,
        CancellationToken cancellationToken)
    {
        var target = unknown ? MigrationExecutionEffectDisposition.Unknown : MigrationExecutionEffectDisposition.Failed;
        var stateToken = CancellationToken.None;
        await ChangeEffectAsync(tenant, effect, target, null, null, code, unknown ? clock.GetUtcNow() : null, clock.GetUtcNow(), stateToken);
        await FinishBatchAsync(tenant, batch, unknown ? MigrationExecutionBatchState.Unknown : MigrationExecutionBatchState.Failed, stateToken);
        return MigrationEconomicGroupResult.Failure(code, unknown);
    }

    private async Task<MigrationEconomicGroupResult> MarkIncompleteAsync(
        TenantContext tenant,
        MigrationExecutionEffectRecord effect,
        string code,
        bool unknown,
        CancellationToken cancellationToken)
    {
        var stateToken = CancellationToken.None;
        var current = (await migration.ListEffectsAsync(tenant, effect.RunId, effect.AttemptId, stateToken)).Single(item => item.Id == effect.Id);
        if (current.Disposition is MigrationExecutionEffectDisposition.Prepared or MigrationExecutionEffectDisposition.Started)
            await ChangeEffectAsync(tenant, current, unknown ? MigrationExecutionEffectDisposition.Unknown : MigrationExecutionEffectDisposition.PartialCompleted,
                current.OwnerRowId, current.ResultingResourceId, code, unknown ? clock.GetUtcNow() : null, clock.GetUtcNow(), stateToken);
        return MigrationEconomicGroupResult.Failure(code, unknown);
    }

    private async Task<MigrationExecutionEffectRecord?> ChangeEffectAsync(
        TenantContext tenant,
        MigrationExecutionEffectRecord effect,
        MigrationExecutionEffectDisposition disposition,
        Guid? ownerRowId,
        Guid? resourceId,
        string? code,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        CancellationToken cancellationToken)
    {
        if (effect.Disposition == disposition) return effect;
        var saved = await migration.UpdateEffectAsync(tenant, new UpdateMigrationExecutionEffectCommand(
            effect.Id,
            disposition,
            ownerRowId,
            resourceId,
            effect.ResultingResourceCode,
            code,
            startedAt,
            completedAt,
            effect.Version), cancellationToken);
        return saved.Succeeded ? saved.Value : null;
    }

    private async Task FinishBatchAsync(
        TenantContext tenant,
        MigrationExecutionBatchRecord batch,
        MigrationExecutionBatchState state,
        CancellationToken cancellationToken)
    {
        var current = await migration.FindBatchAsync(tenant, batch.RunId, batch.AttemptId, MigrationCanonicalRecordType.InventoryOpening, cancellationToken);
        if (current is null || current.State == state || current.State is MigrationExecutionBatchState.Completed or MigrationExecutionBatchState.Failed or MigrationExecutionBatchState.Unknown) return;
        await migration.UpdateBatchAsync(tenant, new UpdateMigrationExecutionBatchCommand(current.Id, state, current.StartedAt, clock.GetUtcNow(), current.Version), cancellationToken);
    }

    private async Task<bool> SaveRepresentationsAsync(
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        MigrationExecutionEffectRecord effect,
        InventoryOpeningBalanceRecord opening,
        InventoryOpeningBalanceRowRecord ownerRow,
        InventoryMovementRecord? movement,
        IReadOnlyList<InventoryMovementValuationEventRecord> events,
        IReadOnlyList<InventoryFinanceValuationHandoffRecord> handoffs,
        FinanceJournalRecord? journal,
        CancellationToken cancellationToken)
    {
        var records = new List<MigrationEconomicRepresentationRecord>
        {
            Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Inventory, MigrationEconomicRepresentationKind.InventoryOpening, opening.Id, opening.SourceReference, opening.Status.ToString(), Version(opening.Version), opening.UpdatedAt),
            Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Inventory, MigrationEconomicRepresentationKind.InventoryOpeningRow, ownerRow.Id, ownerRow.SourceLineReference, ownerRow.Status.ToString(), Version(ownerRow.Version), ownerRow.PostedAt ?? opening.UpdatedAt)
        };
        if (movement is not null)
            records.Add(Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Inventory, MigrationEconomicRepresentationKind.InventoryStockMovement, movement.Id, movement.LedgerSequence.ToString(), movement.ValuationStatus.ToString(), Version(movement.Version), movement.PostedAt));
        records.AddRange(events.Select(item => Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Inventory, MigrationEconomicRepresentationKind.InventoryValuationEvent, item.Id, item.MovementId.ToString("D"), item.Status.ToString(), Version(item.Version), item.OccurredAt)));
        records.AddRange(handoffs.Select(item => Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Inventory, MigrationEconomicRepresentationKind.InventoryFinanceHandoff, item.Id, item.ValuationEvidenceId.ToString("D"), item.Status.ToString(), Version(item.Version), item.AsOf)));
        if (journal is not null)
            records.Add(Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Finance, MigrationEconomicRepresentationKind.FinanceJournal, journal.Id, journal.JournalNumber, journal.Status.ToString(), Version(journal.Version), journal.PostedAt ?? journal.CreatedAt));

        foreach (var record in records)
        {
            var result = await migration.CreateRepresentationAsync(tenant, new CreateMigrationEconomicRepresentationCommand(record), cancellationToken);
            if (!result.Succeeded) return false;
        }
        return true;
    }

    private static FinanceRequestContext? FinanceContext(FoundationRequestContext requestContext) =>
        FinanceRequestContext.TryCreate(requestContext, out var context) ? context : null;

    private static bool OwnerRecordMatches(InventoryOpeningBalanceRecord opening, InventoryOpeningBalanceCreateRequest request, Guid ownerRowId) =>
        opening.CompanyId == request.CompanyId
        && opening.BranchId == request.BranchId
        && opening.WarehouseId == request.WarehouseId
        && opening.AsOfDate == request.AsOfDate
        && opening.SourceOwner == request.SourceOwner
        && opening.SourceSystem == request.SourceSystem
        && opening.SourceReference == request.SourceReference
        && opening.Rows.Count == 1
        && opening.Rows[0].Id == ownerRowId
        && opening.Rows[0].ProductId == request.Rows[0].ProductId
        && opening.Rows[0].UnitOfMeasureId == request.Rows[0].UnitOfMeasureId
        && opening.Rows[0].Quantity == request.Rows[0].Quantity
        && opening.Rows[0].UnitCost == request.Rows[0].UnitCost
        && opening.Rows[0].CurrencyCode == request.Rows[0].CurrencyCode
        && opening.Rows[0].TrackingIdentity == request.Rows[0].TrackingIdentity
        && opening.Rows[0].SourceLineReference == request.Rows[0].SourceLineReference;

    private static bool OpeningMatchesPlan(InventoryOpeningBalanceRecord opening, InventoryOpeningBalanceRowRecord row, MigrationExecutionPlanRow plan)
    {
        if (plan.Parsed.Payload is not MigrationInventoryOpeningPayload payload) return false;
        return OpeningMatchesPayload(opening, row, payload);
    }

    private static bool OpeningMatchesPayload(InventoryOpeningBalanceRecord opening, InventoryOpeningBalanceRowRecord row, MigrationInventoryOpeningPayload payload)
    {
        return opening.CompanyId == payload.CompanyId
            && opening.BranchId == payload.BranchId
            && opening.WarehouseId == payload.WarehouseId
            && opening.AsOfDate == payload.OpeningDate
            && row.ProductId == payload.ProductId
            && row.UnitOfMeasureId == payload.UnitOfMeasureId
            && row.Quantity == payload.Quantity
            && row.UnitCost == payload.UnitCost
            && string.Equals(row.CurrencyCode, payload.CurrencyCode, StringComparison.OrdinalIgnoreCase)
            && row.TrackingIdentity == payload.TrackingIdentity?.Trim()
            && row.SourceLineReference == payload.SourceLineReference?.Trim();
    }

    private static bool MovementMatches(InventoryMovementRecord movement, InventoryOpeningBalanceRecord opening, InventoryOpeningBalanceRowRecord? row) =>
        row is not null
        && movement.TenantId == opening.TenantId
        && movement.CompanyId == opening.CompanyId
        && movement.BranchId == opening.BranchId
        && movement.WarehouseId == opening.WarehouseId
        && movement.SourceType == InventoryMovementSourceType.OpeningBalance
        && movement.SourceDocumentId == opening.Id
        && movement.SourceLineId == row.Id
        && movement.ProductId == row.ProductId
        && movement.UnitOfMeasureId == row.UnitOfMeasureId
        && movement.Quantity == row.Quantity
        && movement.UnitCost == row.UnitCost
        && string.Equals(movement.CurrencyCode, row.CurrencyCode, StringComparison.OrdinalIgnoreCase)
        && movement.TrackingIdentity == row.TrackingIdentity
        && movement.Direction == InventoryMovementDirection.Inbound
        && movement.EffectiveDate == opening.AsOfDate;

    private MigrationEconomicRepresentationRecord Representation(
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        MigrationExecutionEffectRecord effect,
        MigrationEconomicOwnerModule module,
        MigrationEconomicRepresentationKind kind,
        Guid ownerId,
        string? ownerReference,
        string status,
        string version,
        DateTimeOffset occurredAt)
    {
        var id = StableId($"migration-economic-representation:{effect.Id:D}:{module}:{kind}:{ownerId:D}:{version}");
        return new(id, tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, module, kind, ownerId, Trim(ownerReference, 128), Trim(status, 64) ?? "unknown", version, occurredAt, clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray());
    }

    private static string OwnerKey(Guid runId, Guid stagedRecordId, string step) =>
        $"migration-opening:{runId:N}:{stagedRecordId:N}:{step}";

    private static string Version(byte[] bytes) => Convert.ToHexString(bytes);

    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];

    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);

    private sealed record PreparedOpening(
        MigrationExecutionPlanRow Row,
        MigrationInventoryOpeningPayload Payload,
        InventoryOpeningBalanceCreateRequest Request,
        InventoryOpeningBalanceRecord Opening,
        Guid OwnerRowId);

    private sealed record PreflightResult(bool Succeeded, string Code);
}

internal sealed record MigrationEconomicGroupResult(bool Succeeded, string Code, bool Unknown)
{
    internal static MigrationEconomicGroupResult Successful() => new(true, "migration_inventory_opening_completed", false);
    internal static MigrationEconomicGroupResult Failure(string code, bool unknown) => new(false, code, unknown);
}

#pragma warning restore CS1591
