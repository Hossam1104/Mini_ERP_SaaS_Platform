#pragma warning disable CS1591

global using FinanceApp = MiniErp.App.Modules.Finance;
global using FinanceContracts = MiniErp.Contracts.Modules.Finance;
global using InventoryApp = MiniErp.App.Modules.Inventory;
global using InventoryContracts = MiniErp.Contracts.Modules.Inventory;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

internal sealed class MigrationCashBankOpeningExecutionCoordinator
{
    private const string Contract = "migration-cash-bank-opening.v1";
    private readonly IMigrationExecutionPersistence migration;
    private readonly IMigrationReferenceAuthority references;
    private readonly IFinanceSettlementPersistence finance;
    private readonly TimeProvider clock;

    public MigrationCashBankOpeningExecutionCoordinator(IMigrationExecutionPersistence migration, IMigrationReferenceAuthority references, IFinanceSettlementPersistence finance, TimeProvider? clock = null)
    {
        this.migration = migration;
        this.references = references;
        this.finance = finance;
        this.clock = clock ?? TimeProvider.System;
    }

    internal async Task<MigrationOwnerExecutionCoordinator.OwnerPreparationResult> PrepareAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, MigrationAttemptRecord attempt, IReadOnlyList<MigrationExecutionPlanRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0 || rows.Any(item => item.Parsed.Payload is not MigrationCashBankOpeningPayload))
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_cash_bank_opening_plan_invalid");
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null)
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("finance_request_context_unavailable");

        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            if (row.Parsed.Payload is not MigrationCashBankOpeningPayload payload || !ReadyPayload(payload))
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_execution_payload_invalid");
            var checks = await references.ValidateAsync(requestContext, row.Parsed, cancellationToken);
            var failedCheck = checks.FirstOrDefault(item => item.State is not (MigrationReferenceState.NotApplicable or MigrationReferenceState.Active));
            if (failedCheck is not null)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(failedCheck.Code);
            var command = Command(payload, row.Staged.PayloadHash, OwnerKey(run.RunId, row.Staged.StagedRecordId, "finance"));
            var ready = await finance.PreflightMigrationCashBankOpeningAsync(financeContext, command, cancellationToken);
            if (!ready.Ready)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(ready.Code);
        }

        var batchId = StableId($"migration-cash-bank-opening-batch:{attempt.AttemptId:D}");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', rows.OrderBy(item => item.Staged.SourceSequence).Select(item => item.Staged.PayloadHash)))));
        var batch = new MigrationExecutionBatchRecord(batchId, tenant.TenantId, run.RunId, attempt.AttemptId, MigrationCanonicalRecordType.CashBankOpening, MigrationExecutionBatchState.Prepared, batchId, fingerprint, clock.GetUtcNow(), null, null, run.CorrelationId.Value, Guid.NewGuid().ToByteArray());
        var savedBatch = await migration.CreateBatchAsync(tenant, new CreateMigrationExecutionBatchCommand(batch), cancellationToken);
        if (!savedBatch.Succeeded) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(savedBatch.Code);
        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            var payload = (MigrationCashBankOpeningPayload)row.Parsed.Payload;
            var effect = new MigrationExecutionEffectRecord(StableId($"migration-cash-bank-opening-effect:{attempt.AttemptId:D}:{row.Staged.StagedRecordId:D}"), tenant.TenantId, run.RunId, attempt.AttemptId, row.Staged.StagedRecordId, row.Staged.SourceSequence, MigrationCanonicalRecordType.CashBankOpening, batchId, null, null, payload.SourceReference?.Trim(), MigrationExecutionEffectDisposition.Prepared, null, clock.GetUtcNow(), null, null, run.CorrelationId.Value, Guid.NewGuid().ToByteArray());
            var savedEffect = await migration.CreateEffectAsync(tenant, new CreateMigrationExecutionEffectCommand(effect), cancellationToken);
            if (!savedEffect.Succeeded) return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(savedEffect.Code);
        }
        return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Successful();
    }

    internal async Task<MigrationEconomicGroupResult> ExecuteAsync(FoundationRequestContext requestContext, TenantContext tenant, MigrationRunRecord run, MigrationAttemptRecord attempt, IReadOnlyList<MigrationExecutionPlanRow> rows, CancellationToken cancellationToken)
    {
        var batch = await migration.FindBatchAsync(tenant, run.RunId, attempt.AttemptId, MigrationCanonicalRecordType.CashBankOpening, cancellationToken);
        if (batch is null) return MigrationEconomicGroupResult.Failure("migration_execution_batch_not_found", false);
        if (batch.State == MigrationExecutionBatchState.Prepared)
        {
            var started = await migration.UpdateBatchAsync(tenant, new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Started, clock.GetUtcNow(), null, batch.Version), cancellationToken);
            if (!started.Succeeded) return MigrationEconomicGroupResult.Failure(started.Code, started.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
            batch = started.Value!;
        }
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null)
            return MigrationEconomicGroupResult.Failure("finance_request_context_unavailable", false);

        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            var effect = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).SingleOrDefault(item => item.StagedRecordId == row.Staged.StagedRecordId);
            if (effect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_not_found", false);
            if (effect.Disposition == MigrationExecutionEffectDisposition.Unknown) return MigrationEconomicGroupResult.Failure(effect.SafeCode ?? "migration_execution_outcome_unknown", true);
            if (effect.Disposition == MigrationExecutionEffectDisposition.Committed) continue;
            if (row.Parsed.Payload is not MigrationCashBankOpeningPayload payload || !ReadyPayload(payload)) return await FailEffectAsync(tenant, batch, effect, "migration_execution_payload_invalid", false, cancellationToken);
            var command = Command(payload, row.Staged.PayloadHash, OwnerKey(run.RunId, row.Staged.StagedRecordId, "finance"));
            effect = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Started, payload.CashAccountId, null, null, clock.GetUtcNow(), null, cancellationToken);
            if (effect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
            FinanceOperationResult<FinanceJournalRecord> response;
            var responseLost = false;
            try { response = await finance.CreateMigrationCashBankOpeningAsync(financeContext, command, cancellationToken); }
            catch { responseLost = true; response = FinanceOperationResult<FinanceJournalRecord>.Failure("finance_cash_bank_opening_outcome_unknown"); }
            FinanceMigrationCashBankOpeningEvidence? evidence;
            try { evidence = await finance.ReadMigrationCashBankOpeningAsync(financeContext, command, CancellationToken.None); }
            catch { return await FailEffectAsync(tenant, batch, effect, "finance_cash_bank_opening_outcome_unknown", true, CancellationToken.None); }
            if (evidence is not null)
            {
                if (!EvidenceMatches(evidence, command)) return await FailEffectAsync(tenant, batch, effect, "finance_cash_bank_opening_evidence_mismatch", true, CancellationToken.None);
                if (!await SaveRepresentationsAsync(tenant, attempt, effect, evidence, CancellationToken.None)) return await FailEffectAsync(tenant, batch, effect, "migration_economic_evidence_persistence_unknown", true, CancellationToken.None);
                var committed = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Committed, evidence.CashAccount.Id, evidence.RecognitionJournal.Id, null, effect.EffectStartedAt, clock.GetUtcNow(), CancellationToken.None);
                if (committed is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
                continue;
            }
            var code = responseLost || response.Succeeded ? "finance_cash_bank_opening_evidence_unavailable" : response.Code;
            var unknown = responseLost || response.Succeeded || response.Code is "finance_unavailable" or "finance_cash_bank_opening_outcome_unknown";
            return await FailEffectAsync(tenant, batch, effect, code, unknown, unknown ? CancellationToken.None : cancellationToken);
        }

        var finalEffects = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.CashBankOpening).ToArray();
        var state = finalEffects.All(item => item.Disposition == MigrationExecutionEffectDisposition.Committed) ? MigrationExecutionBatchState.Completed : finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown) ? MigrationExecutionBatchState.Unknown : MigrationExecutionBatchState.Failed;
        await FinishBatchAsync(tenant, batch, state, cancellationToken);
        return state == MigrationExecutionBatchState.Completed ? new MigrationEconomicGroupResult(true, "migration_cash_bank_opening_completed", false) : MigrationEconomicGroupResult.Failure("migration_execution_partially_completed", state == MigrationExecutionBatchState.Unknown);
    }

    internal async Task MarkPreparationFailedAsync(TenantContext tenant, MigrationAttemptRecord attempt, string code, CancellationToken cancellationToken)
    {
        foreach (var effect in (await migration.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.CashBankOpening && item.Disposition == MigrationExecutionEffectDisposition.Prepared))
            await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Failed, null, null, code, null, clock.GetUtcNow(), cancellationToken);
        var batch = await migration.FindBatchAsync(tenant, attempt.RunId, attempt.AttemptId, MigrationCanonicalRecordType.CashBankOpening, cancellationToken);
        if (batch is not null) await FinishBatchAsync(tenant, batch, MigrationExecutionBatchState.Failed, cancellationToken);
    }

    internal async Task<IReadOnlyList<MigrationCashBankEconomicReconciliationRecord>> ReadReconciliationsAsync(FoundationRequestContext requestContext, TenantContext tenant, IReadOnlyList<MigrationExecutionEffectRecord> effects, IReadOnlyList<MigrationStagedRecord> staged, CancellationToken cancellationToken)
    {
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null) return [];
        var stagedById = staged.ToDictionary(item => item.StagedRecordId);
        var result = new List<MigrationCashBankEconomicReconciliationRecord>();
        foreach (var effect in effects.Where(item => item.RecordType == MigrationCanonicalRecordType.CashBankOpening).OrderBy(item => item.SourceSequence))
        {
            if (!stagedById.TryGetValue(effect.StagedRecordId, out var stagedRow)) continue;
            MigrationCashBankOpeningPayload? payload;
            try { payload = JsonSerializer.Deserialize<MigrationCashBankOpeningPayload>(stagedRow.CanonicalPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web)); } catch (JsonException) { payload = null; }
            if (payload?.CompanyId is not { } companyId || payload.CashAccountId is not { } cashAccountId || string.IsNullOrWhiteSpace(payload.SourceReference) || payload.Amount is not { } amount || string.IsNullOrWhiteSpace(payload.CurrencyCode) || payload.OpeningDate is not { } openingDate)
            {
                result.Add(new(effect.Id, effect.SourceSequence, "unavailable", "migration_cash_bank_opening_payload_invalid", Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, 0m, null, string.Empty, default, null, null, clock.GetUtcNow()));
                continue;
            }
            var command = Command(payload, stagedRow.PayloadHash, "reconciliation");
            FinanceMigrationCashBankOpeningEvidence? evidence = null;
            try { evidence = await finance.ReadMigrationCashBankOpeningAsync(financeContext, command, cancellationToken); } catch { }
            var posted = evidence?.RecognitionJournal.Lines.Sum(item => item.FunctionalDebit);
            var exact = evidence is not null && EvidenceMatches(evidence, command) && posted == amount;
            result.Add(new(effect.Id, effect.SourceSequence, exact ? "reconciled" : "partial", exact ? null : "finance_cash_bank_opening_evidence_not_reconciled", companyId, cashAccountId, evidence?.LinkedAccount.Id ?? Guid.Empty, payload.SourceReference.Trim(), amount, posted, payload.CurrencyCode.Trim().ToUpperInvariant(), openingDate, evidence?.RecognitionJournal.Id, evidence?.SourceEffect.Id, clock.GetUtcNow()));
        }
        return result;
    }

    private async Task<MigrationEconomicGroupResult> FailEffectAsync(TenantContext tenant, MigrationExecutionBatchRecord batch, MigrationExecutionEffectRecord effect, string code, bool unknown, CancellationToken cancellationToken)
    {
        var disposition = unknown ? MigrationExecutionEffectDisposition.Unknown : MigrationExecutionEffectDisposition.Failed;
        var changed = await ChangeEffectAsync(tenant, effect, disposition, null, null, code, effect.EffectStartedAt, clock.GetUtcNow(), cancellationToken);
        if (changed is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
        await FinishBatchAsync(tenant, batch, unknown ? MigrationExecutionBatchState.Unknown : MigrationExecutionBatchState.Failed, cancellationToken);
        return MigrationEconomicGroupResult.Failure(code, unknown);
    }

    private async Task<bool> SaveRepresentationsAsync(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, FinanceMigrationCashBankOpeningEvidence evidence, CancellationToken cancellationToken)
    {
        var values = new[]
        {
            Representation(tenant, attempt, effect, MigrationEconomicRepresentationKind.FinanceJournal, evidence.RecognitionJournal.Id, evidence.RecognitionJournal.JournalNumber, evidence.RecognitionJournal.Status.ToString(), Convert.ToHexString(evidence.RecognitionJournal.Version), evidence.RecognitionJournal.PostedAt ?? evidence.RecognitionJournal.CreatedAt),
            Representation(tenant, attempt, effect, MigrationEconomicRepresentationKind.FinanceSourceEffect, evidence.SourceEffect.Id, evidence.SourceEffect.SourceContract, "Committed", evidence.SourceEffect.CreatedAt.ToString("O"), evidence.SourceEffect.CreatedAt),
            Representation(tenant, attempt, effect, MigrationEconomicRepresentationKind.FinanceCashAccount, evidence.CashAccount.Id, evidence.CashAccount.Code, evidence.CashAccount.Lifecycle.ToString(), Convert.ToHexString(evidence.CashAccount.Version), clock.GetUtcNow())
        };
        foreach (var value in values)
            if (!(await migration.CreateRepresentationAsync(tenant, new CreateMigrationEconomicRepresentationCommand(value), cancellationToken)).Succeeded) return false;
        return true;
    }

    private async Task<MigrationExecutionEffectRecord?> ChangeEffectAsync(TenantContext tenant, MigrationExecutionEffectRecord effect, MigrationExecutionEffectDisposition disposition, Guid? ownerRowId, Guid? resourceId, string? safeCode, DateTimeOffset? startedAt, DateTimeOffset? completedAt, CancellationToken cancellationToken)
    {
        var result = await migration.UpdateEffectAsync(tenant, new UpdateMigrationExecutionEffectCommand(effect.Id, disposition, ownerRowId, resourceId, effect.ResultingResourceCode, safeCode, startedAt, completedAt, effect.Version), cancellationToken);
        return result.Succeeded ? result.Value : null;
    }

    private async Task FinishBatchAsync(TenantContext tenant, MigrationExecutionBatchRecord batch, MigrationExecutionBatchState state, CancellationToken cancellationToken)
    {
        if (batch.State == state) return;
        await migration.UpdateBatchAsync(tenant, new UpdateMigrationExecutionBatchCommand(batch.Id, state, batch.StartedAt, state is MigrationExecutionBatchState.Completed or MigrationExecutionBatchState.Failed or MigrationExecutionBatchState.Unknown ? clock.GetUtcNow() : null, batch.Version), cancellationToken);
    }

    private static FinanceMigrationCashBankOpeningCommand Command(MigrationCashBankOpeningPayload payload, string fingerprint, string idempotencyKey) => new(payload.CompanyId!.Value, payload.CashAccountId!.Value, payload.SourceReference!.Trim(), payload.OpeningDate!.Value, payload.Amount!.Value, payload.CurrencyCode!.Trim(), fingerprint, idempotencyKey, fingerprint);
    private static bool ReadyPayload(MigrationCashBankOpeningPayload payload) => payload.CompanyId is not null && payload.CashAccountId is not null && payload.ControlAccountId is null && !string.IsNullOrWhiteSpace(payload.SourceReference) && payload.OpeningDate is not null && payload.Amount is > 0m && !string.IsNullOrWhiteSpace(payload.CurrencyCode);
    private static bool EvidenceMatches(FinanceMigrationCashBankOpeningEvidence evidence, FinanceMigrationCashBankOpeningCommand command) => evidence.CashAccount.Id == command.CashAccountId && evidence.CashAccount.CompanyId == command.CompanyId && evidence.LinkedAccount.Id == evidence.CashAccount.LinkedAccountId && evidence.RecognitionJournal.CompanyId == command.CompanyId && evidence.RecognitionJournal.Status == FinanceJournalStatus.Posted && evidence.RecognitionJournal.PostingDate == command.OpeningDate && evidence.RecognitionJournal.SourceContract == Contract && evidence.RecognitionJournal.SourceEvidenceId == evidence.SourceEffect.SourceEvidenceId && evidence.SourceEffect.SourceContract == Contract && evidence.SourceEffect.JournalId == evidence.RecognitionJournal.Id && evidence.RecognitionJournal.Lines.Sum(item => item.FunctionalDebit) == command.Amount && string.Equals(evidence.CashAccount.CurrencyCode, command.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase);
    private MigrationEconomicRepresentationRecord Representation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, MigrationEconomicRepresentationKind kind, Guid ownerId, string? reference, string status, string version, DateTimeOffset occurredAt) => new(StableId($"migration-cash-bank-representation:{effect.Id:D}:{kind}:{ownerId:D}:{version}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, MigrationEconomicOwnerModule.Finance, kind, ownerId, reference, status, version, occurredAt, clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray());
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    private static string OwnerKey(Guid runId, Guid stagedRecordId, string step) => $"migration-cash-bank-opening:{runId:N}:{stagedRecordId:N}:{step}";
}

#pragma warning restore CS1591
