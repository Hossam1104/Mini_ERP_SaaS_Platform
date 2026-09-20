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
        foreach (var group in Groups(glRows))
        {
            var command = await CommandAsync(requestContext, tenant, run, group.ToArray(), economicRows, cancellationToken);
            if (command is null) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_gl_opening_payload_invalid");
            var ready = await finance.PreflightMigrationGlOpeningAsync(financeContext, command, cancellationToken);
            if (!ready.Ready) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(ready.Code);
        }
        var batchId = StableId($"migration-gl-opening-batch:{attempt.AttemptId:D}");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', glRows.OrderBy(item => item.Staged.SourceSequence).Select(item => item.Staged.PayloadHash)))));
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
            var command = await CommandAsync(requestContext, tenant, run, group.ToArray(), economicRows, cancellationToken);
            if (command is null) return await FailBatchAsync(tenant, batch, "migration_gl_opening_payload_invalid", false, cancellationToken);
            var groupEffects = effects.Where(item => group.Any(row => row.Staged.StagedRecordId == item.StagedRecordId)).ToArray();
            if (groupEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)) return MigrationEconomicGroupResult.Failure("migration_execution_outcome_unknown", true);
            if (groupEffects.All(item => item.Disposition is MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.NonEffect)) continue;
            foreach (var effect in groupEffects.Where(item => item.Disposition == MigrationExecutionEffectDisposition.Prepared))
                if (await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Started, null, null, null, clock.GetUtcNow(), null, cancellationToken) is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
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
                if (await ChangeEffectAsync(tenant, effect, disposition, null, evidence.Journal?.Id, null, effect.EffectStartedAt, clock.GetUtcNow(), CancellationToken.None) is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
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
            var command = await CommandAsync(requestContext, tenant, Guid.Empty, group.Select(item => new PlanItem(item.Payload!, string.Empty, item.item.SourceSequence)).ToArray(), cancellationToken);
            if (command is null) { result.Add(new(effect.Id, effect.SourceSequence, "unavailable", "migration_gl_opening_command_invalid", gl.CompanyId!.Value, gl.CurrencyCode!.Trim().ToUpperInvariant(), gl.OpeningDate!.Value, gl.Debit ?? 0m, gl.Credit ?? 0m, 0m, 0m, 0m, 0m, null, null, clock.GetUtcNow())); continue; }
            var evidence = await finance.ReadMigrationGlOpeningAsync(financeContext, command, cancellationToken);
            if (evidence is null) { result.Add(new(effect.Id, effect.SourceSequence, "partial", "migration_gl_opening_evidence_not_reconciled", gl.CompanyId!.Value, command.CurrencyCode, command.OpeningDate, command.Lines.Sum(item => item.Debit), command.Lines.Sum(item => item.Credit), 0m, 0m, 0m, 0m, null, null, clock.GetUtcNow())); continue; }
            result.Add(Reconciliation(effect, command, evidence));
        }
        return result;
    }

    private async Task<FinanceApp.FinanceMigrationGlOpeningCommand?> CommandAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, IReadOnlyList<MigrationExecutionPlanRow> glRows, IReadOnlyList<MigrationExecutionPlanRow> economicRows, CancellationToken cancellationToken)
    {
        var items = glRows.Select(item => new PlanItem(item.Parsed.Payload, item.Staged.PayloadHash, item.Staged.SourceSequence)).Concat(economicRows.Where(item => item.Staged.RecordType != MigrationCanonicalRecordType.GlOpening).Select(item => new PlanItem(item.Parsed.Payload, item.Staged.PayloadHash, item.Staged.SourceSequence))).ToArray();
        return await CommandAsync(requestContext, tenant, run.RunId, items, cancellationToken);
    }

    private async Task<FinanceApp.FinanceMigrationGlOpeningCommand?> CommandAsync(FoundationRequestContext requestContext, TenantContext tenant, Guid runId, IReadOnlyList<PlanItem> items, CancellationToken cancellationToken)
    {
        var gl = items.Where(item => item.Payload is MigrationGlOpeningPayload).Select(item => (item, Payload: (MigrationGlOpeningPayload)item.Payload)).ToArray();
        if (gl.Length == 0 || gl.Any(item => !ReadyPayload(item.Payload))) return null;
        var first = gl[0].Payload;
        if (gl.Any(item => item.Payload.CompanyId != first.CompanyId || item.Payload.OpeningDate != first.OpeningDate || !string.Equals(item.Payload.CurrencyCode, first.CurrencyCode, StringComparison.OrdinalIgnoreCase))) return null;
        var lines = gl.OrderBy(item => item.item.SourceSequence).Select(item => new FinanceApp.FinanceMigrationGlOpeningLine(item.Payload.AccountId!.Value, item.Payload.SourceLineReference!.Trim(), item.Payload.Debit!.Value, item.Payload.Credit!.Value)).ToArray();
        var projections = new List<FinanceApp.FinanceMigrationOpeningProjection>();
        foreach (var item in items.Where(item => item.Payload is not MigrationGlOpeningPayload))
        {
            switch (item.Payload)
            {
                case MigrationArOpeningPayload ar when ar.CompanyId == first.CompanyId && ar.OpeningDate == first.OpeningDate && SameCurrency(ar.CurrencyCode, first.CurrencyCode) && ar.Amount is > 0m:
                    projections.Add(new("migration-ar-opening.v1", Event, ar.Amount.Value)); break;
                case MigrationApOpeningPayload ap when ap.CompanyId == first.CompanyId && ap.OpeningDate == first.OpeningDate && SameCurrency(ap.CurrencyCode, first.CurrencyCode) && ap.Amount is > 0m:
                    projections.Add(new("migration-ap-opening.v1", Event, ap.Amount.Value)); break;
                case MigrationCashBankOpeningPayload cash when cash.CompanyId == first.CompanyId && cash.OpeningDate == first.OpeningDate && SameCurrency(cash.CurrencyCode, first.CurrencyCode) && cash.Amount is > 0m:
                    projections.Add(new("migration-cash-bank-opening.v1", Event, cash.Amount.Value)); break;
                case MigrationInventoryOpeningPayload inventory when inventory.CompanyId == first.CompanyId && inventory.OpeningDate == first.OpeningDate && SameCurrency(inventory.CurrencyCode, first.CurrencyCode) && inventory.BranchId is { } branch && inventory.WarehouseId is { } warehouse && inventory.Quantity is { } quantity && inventory.UnitCost is { } unitCost:
                    var projected = await valuation.ProjectOpeningForMigrationAsync(requestContext, new InventoryApp.InventoryScope(tenant.TenantId.Value, inventory.CompanyId!.Value, branch, warehouse), inventory.OpeningDate!.Value, quantity, unitCost, cancellationToken);
                    if (!projected.Succeeded || projected.Value is not { } value) return null;
                    projections.Add(new("inventory-valuation-finance.v1", FinanceApp.FinanceInventoryPostingClassifier.Classify(InventoryContracts.InventoryMovementSourceType.OpeningBalance, InventoryContracts.InventoryMovementDirection.Inbound), value.FunctionalAmount)); break;
            }
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', gl.Select(item => item.item.PayloadHash)))));
        return new FinanceApp.FinanceMigrationGlOpeningCommand(first.CompanyId!.Value, first.OpeningDate!.Value, first.CurrencyCode!.Trim(), lines, projections, hash, $"migration-gl-opening:{runId:N}:{first.CompanyId.Value:D}:{first.OpeningDate:yyyy-MM-dd}:{first.CurrencyCode.Trim().ToUpperInvariant()}", hash);
    }

    private static IEnumerable<IGrouping<(Guid, string, DateOnly), MigrationExecutionPlanRow>> Groups(IReadOnlyList<MigrationExecutionPlanRow> rows) => rows.GroupBy(item => { var payload = (MigrationGlOpeningPayload)item.Parsed.Payload; return (payload.CompanyId!.Value, payload.CurrencyCode!.Trim().ToUpperInvariant(), payload.OpeningDate!.Value); }).OrderBy(item => item.Key.Item3).ThenBy(item => item.Key.Item1);
    private static bool ReadyPayload(MigrationGlOpeningPayload payload) => payload.CompanyId is not null && payload.AccountId is not null && payload.AccountId != Guid.Empty && payload.Debit is >= 0m && payload.Credit is >= 0m && (payload.Debit > 0m ^ payload.Credit > 0m) && payload.OpeningDate is not null && !string.IsNullOrWhiteSpace(payload.CurrencyCode) && !string.IsNullOrWhiteSpace(payload.SourceLineReference);
    private static bool SameCurrency(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool SameGroup(MigrationCanonicalPayload left, MigrationGlOpeningPayload right) => left switch { MigrationGlOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate && SameCurrency(value.CurrencyCode, right.CurrencyCode), MigrationArOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate && SameCurrency(value.CurrencyCode, right.CurrencyCode), MigrationApOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate && SameCurrency(value.CurrencyCode, right.CurrencyCode), MigrationCashBankOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate && SameCurrency(value.CurrencyCode, right.CurrencyCode), MigrationInventoryOpeningPayload value => value.CompanyId == right.CompanyId && value.OpeningDate == right.OpeningDate && SameCurrency(value.CurrencyCode, right.CurrencyCode), _ => false };
    private static MigrationCanonicalPayload? Parse(MigrationStagedRecord item) { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); return item.RecordType switch { MigrationCanonicalRecordType.GlOpening => JsonSerializer.Deserialize<MigrationGlOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.ArOpening => JsonSerializer.Deserialize<MigrationArOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.ApOpening => JsonSerializer.Deserialize<MigrationApOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.CashBankOpening => JsonSerializer.Deserialize<MigrationCashBankOpeningPayload>(item.CanonicalPayload, options), MigrationCanonicalRecordType.InventoryOpening => JsonSerializer.Deserialize<MigrationInventoryOpeningPayload>(item.CanonicalPayload, options), _ => null }; }
    private static MigrationGlEconomicReconciliationRecord Reconciliation(MigrationExecutionEffectRecord effect, FinanceApp.FinanceMigrationGlOpeningCommand command, FinanceApp.FinanceMigrationGlOpeningEvidence evidence)
    {
        var targetDebit = command.Lines.Sum(item => item.Debit); var targetCredit = command.Lines.Sum(item => item.Credit); var residualDebit = evidence.ResidualLines.Sum(item => item.Debit); var residualCredit = evidence.ResidualLines.Sum(item => item.Credit); var established = evidence.ResidualLines.Select(item => item.TargetSignedAmount - (item.Debit - item.Credit)); var establishedDebit = established.Where(item => item > 0m).Sum(); var establishedCredit = established.Where(item => item < 0m).Sum(item => -item); return new(effect.Id, effect.SourceSequence, evidence.Journal is null ? "reconciled" : "reconciled", null, command.CompanyId, command.CurrencyCode, command.OpeningDate, targetDebit, targetCredit, establishedDebit, establishedCredit, residualDebit, residualCredit, evidence.Journal?.Id, evidence.SourceEffect?.Id, DateTimeOffset.UtcNow);
    }
    private static bool EvidenceMatches(FinanceApp.FinanceMigrationGlOpeningEvidence evidence, FinanceApp.FinanceMigrationGlOpeningCommand command) => evidence.Journal is not null && evidence.SourceEffect is not null && evidence.Journal.Status == FinanceContracts.FinanceJournalStatus.Posted && evidence.Journal.CompanyId == command.CompanyId && evidence.Journal.PostingDate == command.OpeningDate && evidence.Journal.SourceContract == Contract && evidence.Journal.SourceEvent == Event && evidence.Journal.SourceEvidenceId == evidence.SourceEffect.SourceEvidenceId && evidence.Journal.PostingRuleId is null && evidence.Journal.Lines.Sum(item => item.FunctionalDebit) == evidence.Journal.Lines.Sum(item => item.FunctionalCredit);
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
    private MigrationEconomicRepresentationRecord Representation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, MigrationEconomicRepresentationKind kind, Guid ownerId, string? reference, string status, string version, DateTimeOffset occurredAt) => new(StableId($"migration-gl-representation:{effect.Id:D}:{kind}:{ownerId:D}:{version}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, MigrationEconomicOwnerModule.Finance, kind, ownerId, reference, status, version, occurredAt, clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray());
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    private sealed record PlanItem(MigrationCanonicalPayload Payload, string PayloadHash, int SourceSequence);
}

#pragma warning restore CS1591
