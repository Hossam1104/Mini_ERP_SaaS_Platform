#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

internal sealed class MigrationGlOpeningExecutionCoordinator
{
    private const string Contract = "migration-gl-opening.v1";
    private const string Event = "recognition";
    private readonly IMigrationExecutionPersistence migration;
    private readonly IMigrationReferenceAuthority references;
    private readonly FinanceApp.IFinanceSettlementPersistence finance;
    private readonly InventoryApp.InventoryValuationService valuation;
    private readonly TimeProvider clock;

    public MigrationGlOpeningExecutionCoordinator(IMigrationExecutionPersistence migration, IMigrationReferenceAuthority references, FinanceApp.IFinanceSettlementPersistence finance, InventoryApp.InventoryValuationService valuation, TimeProvider? clock = null)
    {
        this.migration = migration;
        this.references = references;
        this.finance = finance;
        this.valuation = valuation;
        this.clock = clock ?? TimeProvider.System;
    }

    internal Task<MigrationOwnerExecutionCoordinator.OwnerPreparationResult> PrepareAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, MigrationAttemptRecord attempt, IReadOnlyList<MigrationExecutionPlanRow> rows, CancellationToken cancellationToken) => PrepareAsync(requestContext, tenant, run, attempt, rows, rows, cancellationToken);

    internal async Task<MigrationOwnerExecutionCoordinator.OwnerPreparationResult> PrepareAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, MigrationAttemptRecord attempt, IReadOnlyList<MigrationExecutionPlanRow> glRows, IReadOnlyList<MigrationExecutionPlanRow> economicRows, CancellationToken cancellationToken)
    {
        if (glRows.Count == 0 || glRows.Any(item => item.Parsed.Payload is not MigrationGlOpeningPayload)) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_gl_opening_plan_invalid");
        if (!FinanceApp.FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("finance_request_context_unavailable");
        foreach (var row in glRows)
        {
            if (row.Parsed.Payload is not MigrationGlOpeningPayload payload || !ReadyPayload(payload)) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_execution_payload_invalid");
            var checks = await references.ValidateAsync(requestContext, row.Parsed, cancellationToken);
            var failed = checks.FirstOrDefault(item => item.State is not (MigrationReferenceState.NotApplicable or MigrationReferenceState.Active));
            if (failed is not null) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(failed.Code);
        }
        var preparedGroups = new List<(IReadOnlyList<MigrationExecutionPlanRow> Rows, FinanceApp.FinanceMigrationGlOpeningCommand Command, FinanceApp.FinanceGlOpeningPreflightResult Preflight)>();
        var preparedEffects = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).ToDictionary(item => item.StagedRecordId);
        var preparedRepresentations = await migration.ListRepresentationsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        foreach (var group in Groups(glRows))
        {
            var command = await CommandAsync(requestContext, tenant, run, group.ToArray(), economicRows, cancellationToken, requireOwnerEvidence: false, ownerEffects: preparedEffects, representations: preparedRepresentations, includeProjections: true);
            if (command is null) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_gl_opening_payload_invalid");
            var ready = await finance.PreflightMigrationGlOpeningAsync(financeContext, command, cancellationToken);
            if (!ready.Ready) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(ready.Code);
            preparedGroups.Add((group.ToArray(), command, ready));
        }
        var batchId = StableId($"migration-gl-opening-batch:{attempt.AttemptId:D}");
        var fingerprint = Fingerprint(glRows.Select(item => (MigrationGlOpeningPayload)item.Parsed.Payload));
        var batch = new MigrationExecutionBatchRecord(batchId, tenant.TenantId, run.RunId, attempt.AttemptId, MigrationCanonicalRecordType.GlOpening, MigrationExecutionBatchState.Prepared, batchId, fingerprint, clock.GetUtcNow(), null, null, run.CorrelationId.Value, Guid.NewGuid().ToByteArray());
        var savedBatch = await migration.CreateBatchAsync(tenant, new CreateMigrationExecutionBatchCommand(batch), cancellationToken);
        if (!savedBatch.Succeeded) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(savedBatch.Code);
        foreach (var row in glRows.OrderBy(item => item.Staged.SourceSequence))
        {
            var payload = (MigrationGlOpeningPayload)row.Parsed.Payload;
            var effect = new MigrationExecutionEffectRecord(StableId($"migration-gl-opening-effect:{attempt.AttemptId:D}:{row.Staged.StagedRecordId:D}"), tenant.TenantId, run.RunId, attempt.AttemptId, row.Staged.StagedRecordId, row.Staged.SourceSequence, MigrationCanonicalRecordType.GlOpening, batchId, null, null, payload.SourceLineReference?.Trim(), MigrationExecutionEffectDisposition.Prepared, null, clock.GetUtcNow(), null, null, run.CorrelationId.Value, Guid.NewGuid().ToByteArray());
            var savedEffect = await migration.CreateEffectAsync(tenant, new CreateMigrationExecutionEffectCommand(effect), cancellationToken);
            if (!savedEffect.Succeeded) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(savedEffect.Code);
        }
        var effects = await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        foreach (var prepared in preparedGroups)
        {
            foreach (var projection in prepared.Command.Projections)
            {
                if (projection.SourceRecordId == Guid.Empty) continue;
                var expected = prepared.Preflight.Expectations?.SingleOrDefault(item => item.SourceRecordId == projection.SourceRecordId);
                var effect = effects.SingleOrDefault(item => item.StagedRecordId == projection.SourceRecordId);
                if (expected is null || effect is null) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_gl_opening_economic_expectation_incomplete");
                if (preparedRepresentations.Any(item => item.EffectId == effect.Id && item.OwnerModule == MigrationEconomicOwnerModule.Finance && item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation && item.OwnerId == expected.SourceRecordId)) continue;
                var saved = await migration.CreateRepresentationAsync(tenant, new CreateMigrationEconomicRepresentationCommand(ExpectedRepresentation(tenant, attempt, effect, expected, projection.AlreadyEstablishedExact)), cancellationToken);
                if (!saved.Succeeded) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(saved.Code);
            }
        }
        return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Successful();
    }

    internal Task<MigrationEconomicGroupResult> ExecuteAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, MigrationAttemptRecord attempt, IReadOnlyList<MigrationExecutionPlanRow> rows, CancellationToken cancellationToken) => ExecuteAsync(requestContext, tenant, run, attempt, rows, rows, cancellationToken);

    internal async Task<MigrationEconomicGroupResult> ExecuteAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, MigrationAttemptRecord attempt, IReadOnlyList<MigrationExecutionPlanRow> glRows, IReadOnlyList<MigrationExecutionPlanRow> economicRows, CancellationToken cancellationToken)
    {
        var batch = await migration.FindBatchAsync(tenant, run.RunId, attempt.AttemptId, MigrationCanonicalRecordType.GlOpening, cancellationToken);
        if (batch is null) return MigrationEconomicGroupResult.Failure("migration_execution_batch_not_found", false);
        if (batch.State == MigrationExecutionBatchState.Prepared)
        {
            var started = await migration.UpdateBatchAsync(tenant, new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Started, clock.GetUtcNow(), null, batch.Version), cancellationToken);
            if (!started.Succeeded) return MigrationEconomicGroupResult.Failure(started.Code, started.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
            batch = started.Value!;
        }
        if (!FinanceApp.FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null) return MigrationEconomicGroupResult.Failure("finance_request_context_unavailable", false);
        var effects = await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        foreach (var group in Groups(glRows))
        {
            var groupEffects = effects.Where(item => group.Any(row => row.Staged.StagedRecordId == item.StagedRecordId)).ToArray();
            var ownerEffects = effects.ToDictionary(item => item.StagedRecordId);
            var representations = await migration.ListRepresentationsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
            var command = await CommandAsync(requestContext, tenant, run, group.ToArray(), economicRows, cancellationToken, requireOwnerEvidence: true, ownerEffects, representations, includeProjections: true);
            if (command is null) return await FailBatchAsync(tenant, batch, "migration_gl_opening_owner_evidence_unavailable", true, cancellationToken);
            if (groupEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)) return MigrationEconomicGroupResult.Failure("migration_execution_outcome_unknown", true);
            if (groupEffects.All(item => item.Disposition is MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.NonEffect)) continue;
            var startedEffects = new Dictionary<Guid, MigrationExecutionEffectRecord>();
            foreach (var effect in groupEffects.Where(item => item.Disposition == MigrationExecutionEffectDisposition.Prepared))
            {
                var startedEffect = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Started, null, null, null, clock.GetUtcNow(), null, cancellationToken);
                if (startedEffect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
                startedEffects[effect.Id] = startedEffect;
            }
            FinanceContracts.FinanceOperationResult<FinanceApp.FinanceMigrationGlOpeningEvidence> response;
            var responseUnknown = false;
            try { response = await finance.CreateMigrationGlOpeningAsync(financeContext, command, cancellationToken); }
            catch { responseUnknown = true; response = FinanceContracts.FinanceOperationResult<FinanceApp.FinanceMigrationGlOpeningEvidence>.Failure("migration_gl_opening_outcome_unknown"); }
            FinanceApp.FinanceMigrationGlOpeningEvidence? evidence = null;
            try { evidence = response.Value ?? await finance.ReadMigrationGlOpeningAsync(financeContext, command, CancellationToken.None); } catch { }
            if (evidence is null)
            {
                var unknown = responseUnknown || response.Code is "finance_gl_opening_unavailable" or "migration_gl_opening_outcome_unknown";
                return await FailBatchAsync(tenant, batch, response.Code, unknown, CancellationToken.None);
            }
            if (evidence.Journal is not null && !EvidenceMatches(evidence, command)) return await FailBatchAsync(tenant, batch, "migration_gl_opening_evidence_mismatch", true, CancellationToken.None);
            foreach (var effect in groupEffects)
            {
                var disposition = evidence.Journal is null ? MigrationExecutionEffectDisposition.NonEffect : MigrationExecutionEffectDisposition.Committed;
                if (evidence.Journal is not null && !await SaveRepresentationsAsync(tenant, attempt, effect, evidence, CancellationToken.None)) return await FailBatchAsync(tenant, batch, "migration_economic_evidence_persistence_unknown", true, CancellationToken.None);
                var currentEffect = startedEffects.TryGetValue(effect.Id, out var startedEffect) ? startedEffect : effect;
                if (await ChangeEffectAsync(tenant, currentEffect, disposition, null, evidence.Journal?.Id, null, currentEffect.EffectStartedAt, clock.GetUtcNow(), CancellationToken.None) is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
            }
        }
        var final = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.GlOpening).ToArray();
        var state = final.All(item => item.Disposition is MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.NonEffect) ? MigrationExecutionBatchState.Completed : final.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown) ? MigrationExecutionBatchState.Unknown : MigrationExecutionBatchState.Failed;
        await FinishBatchAsync(tenant, batch, state, cancellationToken);
        return state == MigrationExecutionBatchState.Completed ? new(true, "migration_gl_opening_completed", false) : MigrationEconomicGroupResult.Failure("migration_execution_partially_completed", state == MigrationExecutionBatchState.Unknown);
    }

    internal async Task MarkPreparationFailedAsync(TenantContext tenant, MigrationAttemptRecord attempt, string code, CancellationToken cancellationToken)
    {
        foreach (var effect in (await migration.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.GlOpening && item.Disposition == MigrationExecutionEffectDisposition.Prepared)) await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Failed, null, null, code, null, clock.GetUtcNow(), cancellationToken);
        var batch = await migration.FindBatchAsync(tenant, attempt.RunId, attempt.AttemptId, MigrationCanonicalRecordType.GlOpening, cancellationToken);
        if (batch is not null) await FinishBatchAsync(tenant, batch, MigrationExecutionBatchState.Failed, cancellationToken);
    }

    internal async Task<IReadOnlyList<MigrationGlEconomicReconciliationRecord>> ReadReconciliationsAsync(FoundationRequestContext requestContext, TenantContext tenant, IReadOnlyList<MigrationExecutionEffectRecord> effects, IReadOnlyList<MigrationStagedRecord> staged, CancellationToken cancellationToken)
    {
        if (!FinanceApp.FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null) return [];
        var parsed = staged.Select(item => (item, Payload: Parse(item))).Where(item => item.Payload is not null).ToArray();
        var glEffects = effects.Where(item => item.RecordType == MigrationCanonicalRecordType.GlOpening).ToArray();
        var result = new List<MigrationGlEconomicReconciliationRecord>();
        foreach (var effect in glEffects.OrderBy(item => item.SourceSequence))
        {
            var gl = parsed.FirstOrDefault(item => item.item.StagedRecordId == effect.StagedRecordId).Payload as MigrationGlOpeningPayload;
            if (gl is null || !ReadyPayload(gl)) { result.Add(new(effect.Id, effect.SourceSequence, "unavailable", "migration_gl_opening_payload_invalid", Guid.Empty, string.Empty, default, 0m, 0m, 0m, 0m, 0m, 0m, null, null, clock.GetUtcNow())); continue; }
            var group = parsed.Where(item => SameGroup(item.Payload!, gl)).ToArray();
            var command = await CommandAsync(requestContext, tenant, Guid.Empty, group.Select(item => new PlanItem(item.Payload!, string.Empty, item.item.SourceSequence, item.item.StagedRecordId)).ToArray(), cancellationToken, requireOwnerEvidence: false, ownerEffects: null, representations: null, includeProjections: false);
            if (command is null) { result.Add(new(effect.Id, effect.SourceSequence, "unavailable", "migration_gl_opening_command_invalid", gl.CompanyId!.Value, gl.CurrencyCode!.Trim().ToUpperInvariant(), gl.OpeningDate!.Value, gl.Debit ?? 0m, gl.Credit ?? 0m, 0m, 0m, 0m, 0m, null, null, clock.GetUtcNow())); continue; }
            var evidence = await finance.ReadMigrationGlOpeningAsync(financeContext, command, cancellationToken);
            if (evidence is null) { result.Add(new(effect.Id, effect.SourceSequence, "partial", "migration_gl_opening_evidence_not_reconciled", gl.CompanyId!.Value, command.CurrencyCode, command.OpeningDate, command.Lines.Sum(item => item.Debit), command.Lines.Sum(item => item.Credit), 0m, 0m, 0m, 0m, null, null, clock.GetUtcNow())); continue; }
            result.Add(Reconciliation(effect, command, evidence));
        }
        return result;
    }

    private async Task<FinanceApp.FinanceMigrationGlOpeningCommand?> CommandAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, IReadOnlyList<MigrationExecutionPlanRow> glRows, IReadOnlyList<MigrationExecutionPlanRow> economicRows, CancellationToken cancellationToken, bool requireOwnerEvidence, IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord>? ownerEffects, IReadOnlyList<MigrationEconomicRepresentationRecord>? representations, bool includeProjections)
    {
        var items = glRows.Select(item => new PlanItem(item.Parsed.Payload, item.Staged.PayloadHash, item.Staged.SourceSequence, item.Staged.StagedRecordId)).Concat(economicRows.Where(item => item.Staged.RecordType != MigrationCanonicalRecordType.GlOpening).Select(item => new PlanItem(item.Parsed.Payload, item.Staged.PayloadHash, item.Staged.SourceSequence, item.Staged.StagedRecordId))).ToArray();
        return await CommandAsync(requestContext, tenant, run.RunId, items, cancellationToken, requireOwnerEvidence, ownerEffects, representations, includeProjections);
    }

    private async Task<FinanceApp.FinanceMigrationGlOpeningCommand?> CommandAsync(FoundationRequestContext requestContext, TenantContext tenant, Guid runId, IReadOnlyList<PlanItem> items, CancellationToken cancellationToken, bool requireOwnerEvidence, IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord>? ownerEffects, IReadOnlyList<MigrationEconomicRepresentationRecord>? representations, bool includeProjections)
    {
        var gl = items.Where(item => item.Payload is MigrationGlOpeningPayload).Select(item => (item, Payload: (MigrationGlOpeningPayload)item.Payload)).ToArray();
        if (gl.Length == 0 || gl.Any(item => !ReadyPayload(item.Payload))) return null;
        var first = gl[0].Payload;
        if (gl.Any(item => item.Payload.CompanyId != first.CompanyId || item.Payload.OpeningDate != first.OpeningDate || !string.Equals(item.Payload.CurrencyCode, first.CurrencyCode, StringComparison.OrdinalIgnoreCase))) return null;
        var lines = gl.OrderBy(item => item.item.SourceSequence).Select(item => new FinanceApp.FinanceMigrationGlOpeningLine(item.Payload.AccountId!.Value, item.Payload.SourceLineReference!.Trim(), item.Payload.Debit!.Value, item.Payload.Credit!.Value)).ToArray();
        var projections = new List<FinanceApp.FinanceMigrationOpeningProjection>();
        foreach (var item in items.Where(item => item.Payload is not MigrationGlOpeningPayload))
        {
            if (!SameGroup(item.Payload, first)) continue;
            var projection = await ResolveProjectionAsync(requestContext, item, ownerEffects, representations, cancellationToken);
            if (projection is null || requireOwnerEvidence && !projection.AlreadyEstablishedExact) return null;
            if (!includeProjections) continue;
            projections.Add(projection);
        }
        var hash = Fingerprint(gl.Select(item => item.Payload));
        return new FinanceApp.FinanceMigrationGlOpeningCommand(first.CompanyId!.Value, first.OpeningDate!.Value, first.CurrencyCode!.Trim(), lines, projections, hash, $"migration-gl-opening:{runId:N}:{first.CompanyId.Value:D}:{first.OpeningDate:yyyy-MM-dd}:{first.CurrencyCode.Trim().ToUpperInvariant()}", hash, GroupFingerprint(gl.Select(item => item.Payload)));
    }

    private async Task<FinanceApp.FinanceMigrationOpeningProjection?> ResolveProjectionAsync(FoundationRequestContext requestContext, PlanItem item, IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord>? effects, IReadOnlyList<MigrationEconomicRepresentationRecord>? representations, CancellationToken cancellationToken)
    {
        if (!FinanceApp.FinanceRequestContext.TryCreate(requestContext, out var context) || context is null) return null;
        switch (item.Payload)
        {
            case MigrationArOpeningPayload ar when ar.CompanyId is { } company && ar.CustomerId is { } customer && ar.DocumentDate is { } documentDate && ar.OpeningDate is { } openingDate && ar.Amount is { } amount && !string.IsNullOrWhiteSpace(ar.SourceReference) && !string.IsNullOrWhiteSpace(ar.CurrencyCode):
            {
                var command = new FinanceApp.FinanceMigrationArOpeningCommand(company, customer, ar.SourceReference.Trim(), documentDate, openingDate, amount, ar.CurrencyCode.Trim(), ar.DueDate, ar.PaymentTermId, item.PayloadHash, $"migration-ar-opening:{item.StagedRecordId:N}", item.PayloadHash);
                var evidence = await finance.ReadMigrationArOpeningAsync(context, command, cancellationToken);
                var monetary = ExpectedMonetary(item.StagedRecordId, effects, representations);
                var projection = new FinanceApp.FinanceMigrationOpeningProjection("migration-ar-opening.v1", Event, monetary?.FunctionalAmount ?? amount, false, item.StagedRecordId, customer, ar.SourceReference.Trim(), MonetaryExpectation: monetary);
                if (evidence is null) return projection;
                if (!ArMatches(evidence, command)) return null;
                return MatchActual(projection, Actual(evidence.RecognitionJournal, evidence.SourceEffect, false, evidence.OpenItem.CustomerId!.Value), effects, representations, item.StagedRecordId);
            }
            case MigrationApOpeningPayload ap when ap.CompanyId is { } company && ap.SupplierId is { } supplier && ap.DocumentDate is { } documentDate && ap.OpeningDate is { } openingDate && ap.Amount is { } amount && !string.IsNullOrWhiteSpace(ap.SourceReference) && !string.IsNullOrWhiteSpace(ap.CurrencyCode):
            {
                var command = new FinanceApp.FinanceMigrationApOpeningCommand(company, supplier, ap.SourceReference.Trim(), documentDate, openingDate, amount, ap.CurrencyCode.Trim(), ap.DueDate, ap.PaymentTermId, item.PayloadHash, $"migration-ap-opening:{item.StagedRecordId:N}", item.PayloadHash);
                var evidence = await finance.ReadMigrationApOpeningAsync(context, command, cancellationToken);
                var monetary = ExpectedMonetary(item.StagedRecordId, effects, representations);
                var projection = new FinanceApp.FinanceMigrationOpeningProjection("migration-ap-opening.v1", Event, monetary?.FunctionalAmount ?? amount, false, item.StagedRecordId, supplier, ap.SourceReference.Trim(), MonetaryExpectation: monetary);
                if (evidence is null) return projection;
                if (!ApMatches(evidence, command)) return null;
                return MatchActual(projection, Actual(evidence.RecognitionJournal, evidence.SourceEffect, true, evidence.OpenItem.SupplierId!.Value), effects, representations, item.StagedRecordId);
            }
            case MigrationCashBankOpeningPayload cash when cash.CompanyId is { } company && cash.CashAccountId is { } cashAccount && cash.OpeningDate is { } openingDate && cash.Amount is { } amount && !string.IsNullOrWhiteSpace(cash.SourceReference) && !string.IsNullOrWhiteSpace(cash.CurrencyCode):
            {
                var command = new FinanceApp.FinanceMigrationCashBankOpeningCommand(company, cashAccount, cash.SourceReference.Trim(), openingDate, amount, cash.CurrencyCode.Trim(), item.PayloadHash, $"migration-cash-bank-opening:{item.StagedRecordId:N}", item.PayloadHash);
                var evidence = await finance.ReadMigrationCashBankOpeningAsync(context, command, cancellationToken);
                var monetary = ExpectedMonetary(item.StagedRecordId, effects, representations);
                var projection = new FinanceApp.FinanceMigrationOpeningProjection("migration-cash-bank-opening.v1", Event, monetary?.FunctionalAmount ?? amount, false, item.StagedRecordId, cashAccount, cash.SourceReference.Trim(), MonetaryExpectation: monetary);
                if (evidence is null) return projection;
                if (!CashMatches(evidence, command)) return null;
                return MatchActual(projection, Actual(evidence.RecognitionJournal, evidence.SourceEffect, false, evidence.CashAccount.Id, evidence.LinkedAccount.Id), effects, representations, item.StagedRecordId);
            }
            case MigrationInventoryOpeningPayload inventory when inventory.CompanyId is { } company && inventory.BranchId is { } branch && inventory.WarehouseId is { } warehouse && inventory.OpeningDate is { } openingDate && inventory.Quantity is { } quantity && inventory.UnitCost is { } unitCost && !string.IsNullOrWhiteSpace(inventory.CurrencyCode):
            {
                var projected = await valuation.ProjectOpeningForMigrationAsync(requestContext, new InventoryApp.InventoryScope(context.TenantId.Value, company, branch, warehouse), openingDate, quantity, unitCost, cancellationToken);
                if (!projected.Succeeded || projected.Value is not { } value) return null;
                var projection = new FinanceApp.FinanceMigrationOpeningProjection("inventory-valuation-finance.v1", FinanceApp.FinanceInventoryPostingClassifier.Classify(InventoryContracts.InventoryMovementSourceType.OpeningBalance, InventoryContracts.InventoryMovementDirection.Inbound), value.FunctionalAmount, false, item.StagedRecordId, warehouse, inventory.SourceLineReference?.Trim());
                if (effects is null || !effects.TryGetValue(item.StagedRecordId, out var effect)) return projection;
                var journal = representations?.SingleOrDefault(record => record.EffectId == effect.Id && record.Kind == MigrationEconomicRepresentationKind.FinanceJournal && record.EvidenceConfirmed);
                if (journal is null || journal.PostingRuleId is null || journal.PostingRuleVersionNumber is null || journal.ControlAccountId is null || journal.OffsetAccountId is null || journal.FunctionalAmount is null || journal.SourceContract is null || journal.SourceEvent is null) return projection;
                if (journal.OwnerSourceId is not { } ownerSourceId || ownerSourceId != projection.OwnerSourceId) return projection;
                return MatchActual(projection, projection with { AlreadyEstablishedExact = true, OwnerSourceId = ownerSourceId, PostingRuleId = journal.PostingRuleId, PostingRuleVersionNumber = journal.PostingRuleVersionNumber, ControlAccountId = journal.ControlAccountId, OffsetAccountId = journal.OffsetAccountId, Reversal = journal.Reversal, SourceEvidenceId = journal.SourceEvidenceId, SourceEvidenceVersion = journal.SourceEvidenceVersion }, effects, representations, item.StagedRecordId);
            }
            default:
                return null;
        }
    }

    private static bool ArMatches(FinanceApp.FinanceMigrationArOpeningEvidence evidence, FinanceApp.FinanceMigrationArOpeningCommand command) => evidence.OpenItem.Kind == FinanceContracts.FinanceOpenItemKind.Receivable && evidence.OpenItem.CompanyId == command.CompanyId && evidence.OpenItem.CustomerId == command.CustomerId && evidence.OpenItem.SourceContract == "migration-ar-opening.v1" && evidence.OpenItem.Reference == command.SourceReference && evidence.OpenItem.DocumentDate == command.DocumentDate && evidence.OpenItem.OriginalAmount == command.Amount && SameCurrency(evidence.OpenItem.CurrencyCode, command.CurrencyCode) && evidence.OpenItem.RecognitionJournalId == evidence.RecognitionJournal.Id && PostedEvidence(evidence.RecognitionJournal, evidence.SourceEffect, command.CompanyId, "migration-ar-opening.v1", command.OpeningDate);
    private static bool ApMatches(FinanceApp.FinanceMigrationApOpeningEvidence evidence, FinanceApp.FinanceMigrationApOpeningCommand command) => evidence.OpenItem.Kind == FinanceContracts.FinanceOpenItemKind.Payable && evidence.OpenItem.CompanyId == command.CompanyId && evidence.OpenItem.SupplierId == command.SupplierId && evidence.OpenItem.SourceContract == "migration-ap-opening.v1" && evidence.OpenItem.Reference == command.SourceReference && evidence.OpenItem.DocumentDate == command.DocumentDate && evidence.OpenItem.OriginalAmount == command.Amount && SameCurrency(evidence.OpenItem.CurrencyCode, command.CurrencyCode) && evidence.OpenItem.RecognitionJournalId == evidence.RecognitionJournal.Id && PostedEvidence(evidence.RecognitionJournal, evidence.SourceEffect, command.CompanyId, "migration-ap-opening.v1", command.OpeningDate);
    private static bool CashMatches(FinanceApp.FinanceMigrationCashBankOpeningEvidence evidence, FinanceApp.FinanceMigrationCashBankOpeningCommand command) => evidence.CashAccount.Id == command.CashAccountId && evidence.CashAccount.CompanyId == command.CompanyId && evidence.LinkedAccount.Id == evidence.CashAccount.LinkedAccountId && SameCurrency(evidence.CashAccount.CurrencyCode, command.CurrencyCode) && PostedEvidence(evidence.RecognitionJournal, evidence.SourceEffect, command.CompanyId, "migration-cash-bank-opening.v1", command.OpeningDate) && CashMonetaryMatches(evidence, command);
    private static bool CashMonetaryMatches(FinanceApp.FinanceMigrationCashBankOpeningEvidence evidence, FinanceApp.FinanceMigrationCashBankOpeningCommand command)
    {
        var functionalTotal = evidence.RecognitionJournal.Lines.Sum(item => item.FunctionalDebit);
        return evidence.MonetaryEvidence is null
            ? functionalTotal == command.Amount
            : SameCurrency(evidence.MonetaryEvidence.TransactionCurrencyCode, command.CurrencyCode) && evidence.MonetaryEvidence.TransactionAmount == command.Amount && functionalTotal == evidence.MonetaryEvidence.FunctionalAmount;
    }
    private static FinanceApp.FinanceMigrationOpeningProjection Actual(FinanceContracts.FinanceJournalRecord journal, FinanceApp.FinanceSourceEffectRecord source, bool reversal, Guid ownerSourceId, Guid? controlAccountId = null, Guid? offsetAccountId = null)
    {
        var lines = journal.Lines.Where(item => item.FunctionalDebit > 0m || item.FunctionalCredit > 0m).OrderBy(item => item.LineNumber).ToArray();
        var control = controlAccountId ?? (reversal ? lines.SingleOrDefault(item => item.FunctionalCredit > 0m)?.AccountId : lines.SingleOrDefault(item => item.FunctionalDebit > 0m)?.AccountId);
        var offset = offsetAccountId ?? (reversal ? lines.SingleOrDefault(item => item.FunctionalDebit > 0m)?.AccountId : lines.SingleOrDefault(item => item.FunctionalCredit > 0m)?.AccountId);
        return new(journal.SourceContract, journal.SourceEvent, lines.Sum(item => item.FunctionalDebit), true, Guid.Empty, ownerSourceId, null, journal.PostingRuleId, journal.PostingRuleVersionNumber, control, offset, reversal, source.SourceEvidenceId, source.SourceEvidenceVersion);
    }

    private static FinanceApp.FinanceMigrationOpeningProjection MatchActual(FinanceApp.FinanceMigrationOpeningProjection expectedShape, FinanceApp.FinanceMigrationOpeningProjection actual, IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord>? effects, IReadOnlyList<MigrationEconomicRepresentationRecord>? representations, Guid sourceRecordId)
    {
        var effect = effects?.TryGetValue(sourceRecordId, out var current) == true ? current : null;
        var persisted = effect is null ? null : representations?.SingleOrDefault(item => item.EffectId == effect.Id && item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation);
        return persisted is null && expectedShape.MonetaryExpectation is not null || persisted is not null && !Matches(persisted, actual)
            ? expectedShape
            : actual with { SourceRecordId = sourceRecordId, OwnerReference = expectedShape.OwnerReference, AlreadyEstablishedExact = true, MonetaryExpectation = expectedShape.MonetaryExpectation };
    }

    private static bool Matches(MigrationEconomicRepresentationRecord expected, FinanceApp.FinanceMigrationOpeningProjection actual) => expected.SourceContract == actual.SourceContract && expected.SourceEvent == actual.SourceEvent && expected.FunctionalAmount == actual.Amount && expected.PostingRuleId == actual.PostingRuleId && expected.PostingRuleVersionNumber == actual.PostingRuleVersionNumber && expected.ControlAccountId == actual.ControlAccountId && expected.OffsetAccountId == actual.OffsetAccountId && expected.Reversal == actual.Reversal && (expected.SourceEvidenceId is null || expected.SourceEvidenceId == actual.SourceEvidenceId) && (expected.SourceEvidenceVersion is null || expected.SourceEvidenceVersion == actual.SourceEvidenceVersion) && expected.OwnerSourceId == actual.OwnerSourceId;
    private static bool PostedEvidence(FinanceContracts.FinanceJournalRecord journal, FinanceApp.FinanceSourceEffectRecord source, Guid companyId, string contract, DateOnly openingDate) => journal.CompanyId == companyId && journal.Status == FinanceContracts.FinanceJournalStatus.Posted && journal.PostingDate == openingDate && journal.SourceContract == contract && journal.SourceEvidenceId == source.SourceEvidenceId && source.CompanyId == companyId && source.SourceContract == contract && source.JournalId == journal.Id;

    private static FinanceApp.FinanceMigrationOpeningExpectation? ExpectedMonetary(Guid stagedRecordId, IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord>? effects, IReadOnlyList<MigrationEconomicRepresentationRecord>? representations)
    {
        if (effects is null || !effects.TryGetValue(stagedRecordId, out var effect)) return null;
        var record = representations?.SingleOrDefault(item => item.EffectId == effect.Id && item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation);
        return record is null ? null : MigrationOpeningMonetaryMatching.FromRepresentation(record);
    }

    private static IEnumerable<IGrouping<(Guid, DateOnly), MigrationExecutionPlanRow>> Groups(IReadOnlyList<MigrationExecutionPlanRow> rows) => rows.GroupBy(item => { var payload = (MigrationGlOpeningPayload)item.Parsed.Payload; return (payload.CompanyId!.Value, payload.OpeningDate!.Value); }).OrderBy(item => item.Key.Item2).ThenBy(item => item.Key.Item1);
    private static bool ReadyPayload(MigrationGlOpeningPayload payload) => payload.CompanyId is not null && payload.AccountId is not null && payload.AccountId != Guid.Empty && payload.Debit is >= 0m && payload.Credit is >= 0m && (payload.Debit > 0m ^ payload.Credit > 0m) && payload.OpeningDate is not null && !string.IsNullOrWhiteSpace(payload.CurrencyCode) && !string.IsNullOrWhiteSpace(payload.SourceLineReference);
    private static bool SameCurrency(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool SameGroup(MigrationCanonicalPayload left, MigrationGlOpeningPayload right) => left switch { MigrationGlOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate, MigrationArOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate, MigrationApOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate, MigrationCashBankOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate, MigrationInventoryOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate && SameCurrency(value.CurrencyCode, right.CurrencyCode), _ => false };
    private static MigrationCanonicalPayload? Parse(MigrationStagedRecord item) { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); return item.RecordType switch { MigrationCanonicalRecordType.GlOpening => JsonSerializer.Deserialize<MigrationGlOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.ArOpening => JsonSerializer.Deserialize<MigrationArOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.ApOpening => JsonSerializer.Deserialize<MigrationApOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.CashBankOpening => JsonSerializer.Deserialize<MigrationCashBankOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.InventoryOpening => JsonSerializer.Deserialize<MigrationInventoryOpeningPayload>(item.CanonicalPayload, options), _ => null }; }
    private static MigrationGlEconomicReconciliationRecord Reconciliation(MigrationExecutionEffectRecord effect, FinanceApp.FinanceMigrationGlOpeningCommand command, FinanceApp.FinanceMigrationGlOpeningEvidence evidence)
    {
        var allLines = evidence.ResidualLines.Concat(evidence.EstablishedLines
            ?? evidence.RepresentedControlLines ?? Array.Empty<FinanceApp.FinanceMigrationGlOpeningResidualLine>()).OrderBy(item => item.AccountId).ToArray();
        var targetDebit = command.Lines.Sum(item => item.Debit); var targetCredit = command.Lines.Sum(item => item.Credit); var residualDebit = evidence.ResidualLines.Sum(item => item.Debit); var residualCredit = evidence.ResidualLines.Sum(item => item.Credit); var established = allLines.Select(item => item.TargetSignedAmount - (item.Debit - item.Credit)); var establishedDebit = established.Where(item => item > 0m).Sum(); var establishedCredit = established.Where(item => item < 0m).Sum(item => -item); var lines = allLines.Select(item => { var journalLine = evidence.Journal?.Lines.SingleOrDefault(line => line.AccountId == item.AccountId); return new MigrationGlEconomicReconciliationLineRecord(item.AccountId, item.SourceLineReference, item.AccountingTreatment, item.TargetSignedAmount, item.EstablishedSignedAmount, item.Debit, item.Credit, item.IsControlAccount, journalLine?.Id, journalLine?.Description); }).ToArray(); return new(effect.Id, effect.SourceSequence, "reconciled", null, command.CompanyId, command.CurrencyCode, command.OpeningDate, targetDebit, targetCredit, establishedDebit, establishedCredit, residualDebit, residualCredit, evidence.Journal?.Id, evidence.SourceEffect?.Id, DateTimeOffset.UtcNow, lines);
    }
    private static bool EvidenceMatches(FinanceApp.FinanceMigrationGlOpeningEvidence evidence, FinanceApp.FinanceMigrationGlOpeningCommand command) => evidence.Journal is not null && evidence.SourceEffect is not null && (string.Equals(evidence.Journal.CorrelationId, command.SourcePayloadFingerprint, StringComparison.Ordinal) || string.Equals(evidence.Journal.CorrelationId, command.SourcePayloadFingerprint + (command.SourceGroupFingerprint ?? string.Empty), StringComparison.Ordinal)) && evidence.Journal.Status == FinanceContracts.FinanceJournalStatus.Posted && evidence.Journal.CompanyId == command.CompanyId && evidence.Journal.PostingDate == command.OpeningDate && evidence.Journal.SourceContract == Contract && evidence.Journal.SourceEvent == Event && evidence.Journal.SourceEvidenceId == evidence.SourceEffect.SourceEvidenceId && evidence.Journal.PostingRuleId is null && evidence.Journal.Lines.Sum(item => item.FunctionalDebit) == evidence.Journal.Lines.Sum(item => item.FunctionalCredit);
    private async Task<MigrationEconomicGroupResult> FailBatchAsync(TenantContext tenant, MigrationExecutionBatchRecord batch, string code, bool unknown, CancellationToken cancellationToken) { await FinishBatchAsync(tenant, batch, unknown ? MigrationExecutionBatchState.Unknown : MigrationExecutionBatchState.Failed, cancellationToken); return MigrationEconomicGroupResult.Failure(code, unknown); }
    private async Task<MigrationExecutionEffectRecord?> ChangeEffectAsync(TenantContext tenant, MigrationExecutionEffectRecord effect, MigrationExecutionEffectDisposition disposition, Guid? ownerRowId, Guid? resourceId, string? safeCode, DateTimeOffset? startedAt, DateTimeOffset? completedAt, CancellationToken cancellationToken) { var result = await migration.UpdateEffectAsync(tenant, new UpdateMigrationExecutionEffectCommand(effect.Id, disposition, ownerRowId, resourceId, null, safeCode, startedAt, completedAt, effect.Version), cancellationToken); return result.Succeeded ? result.Value : null; }
    private async Task FinishBatchAsync(TenantContext tenant, MigrationExecutionBatchRecord batch, MigrationExecutionBatchState state, CancellationToken cancellationToken) { if (batch.State != state) await migration.UpdateBatchAsync(tenant, new UpdateMigrationExecutionBatchCommand(batch.Id, state, batch.StartedAt, state is MigrationExecutionBatchState.Completed or MigrationExecutionBatchState.Failed or MigrationExecutionBatchState.Unknown ? clock.GetUtcNow() : null, batch.Version), cancellationToken); }
    private async Task<bool> SaveRepresentationsAsync(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, FinanceApp.FinanceMigrationGlOpeningEvidence evidence, CancellationToken cancellationToken)
    {
        var values = new List<MigrationEconomicRepresentationRecord>();
        if (evidence.Journal is not null) values.Add(Representation(tenant, attempt, effect, MigrationEconomicRepresentationKind.FinanceJournal, evidence.Journal.Id, evidence.Journal.JournalNumber, evidence.Journal.Status.ToString(), Convert.ToHexString(evidence.Journal.Version), evidence.Journal.PostedAt ?? evidence.Journal.CreatedAt));
        if (evidence.SourceEffect is not null) values.Add(Representation(tenant, attempt, effect, MigrationEconomicRepresentationKind.FinanceSourceEffect, evidence.SourceEffect.Id, evidence.SourceEffect.SourceContract, "Committed", evidence.SourceEffect.CreatedAt.ToString("O"), evidence.SourceEffect.CreatedAt));
        foreach (var value in values) if (!(await migration.CreateRepresentationAsync(tenant, new CreateMigrationEconomicRepresentationCommand(value), cancellationToken)).Succeeded) return false;
        return true;
    }
    private MigrationEconomicRepresentationRecord ExpectedRepresentation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, FinanceApp.FinanceMigrationOpeningExpectation expected, bool established) => new(StableId($"migration-opening-expectation:{effect.Id:D}:{expected.SourceContract}:{expected.SourceEvent}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, MigrationEconomicOwnerModule.Finance, MigrationEconomicRepresentationKind.FinanceOpeningExpectation, expected.SourceRecordId, expected.OwnerReference, established ? "established" : "prepared", "migration-economic-expectation-v1", clock.GetUtcNow(), clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray(), expected.SourceContract, expected.SourceEvent, expected.FunctionalAmount, expected.PostingRuleId, expected.PostingRuleVersionNumber, expected.ControlAccountId, expected.OffsetAccountId, expected.Reversal, expected.SourceEvidenceId, expected.SourceEvidenceVersion, expected.OwnerSourceId, expected.TransactionCurrencyCode, expected.TransactionAmount, expected.ExpectedFunctionalCurrencyCode, expected.RateDate, expected.ExchangeRateId, expected.ExchangeRateVersionId, expected.ExchangeRateVersionNumber, expected.AppliedRate, expected.MonetaryPolicyId, expected.MonetaryPolicyVersionNumber, expected.RoundingScale, expected.RoundingMode, expected.ReportingCurrencyCode, expected.ReportingExchangeRateId, expected.ReportingExchangeRateVersionId, expected.ReportingExchangeRateVersionNumber, expected.ReportingAppliedRate);
    private MigrationEconomicRepresentationRecord Representation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, MigrationEconomicRepresentationKind kind, Guid ownerId, string? reference, string status, string version, DateTimeOffset occurredAt) => new(StableId($"migration-gl-representation:{effect.Id:D}:{kind}:{ownerId:D}:{version}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, MigrationEconomicOwnerModule.Finance, kind, ownerId, reference, status, version, occurredAt, clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray());
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    private static string Fingerprint(IEnumerable<MigrationGlOpeningPayload> rows) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", rows.Select(item => string.Join(";", item.CompanyId!.Value.ToString("D"), item.OpeningDate!.Value.ToString("yyyy-MM-dd"), item.CurrencyCode!.Trim().ToUpperInvariant(), item.AccountId!.Value.ToString("D"), item.Debit!.Value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture), item.Credit!.Value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture), item.SourceLineReference!.Trim())).OrderBy(item => item, StringComparer.Ordinal)))));
    private static string GroupFingerprint(IEnumerable<MigrationGlOpeningPayload> rows) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", rows.Select(item => string.Join(";", item.CompanyId!.Value.ToString("D"), item.AccountId!.Value.ToString("D"), item.SourceLineReference!.Trim())).OrderBy(item => item, StringComparer.Ordinal)))));
    private sealed record PlanItem(MigrationCanonicalPayload Payload, string PayloadHash, int SourceSequence, Guid StagedRecordId);
}

#pragma warning restore CS1591
