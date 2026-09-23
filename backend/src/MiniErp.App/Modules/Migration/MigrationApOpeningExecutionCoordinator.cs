#pragma warning disable CS1591

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

internal sealed class MigrationApOpeningExecutionCoordinator
{
    private readonly IMigrationExecutionPersistence migration;
    private readonly IMigrationReferenceAuthority references;
    private readonly IFinanceSettlementPersistence finance;
    private readonly TimeProvider clock;

    public MigrationApOpeningExecutionCoordinator(
        IMigrationExecutionPersistence migration,
        IMigrationReferenceAuthority references,
        IFinanceSettlementPersistence finance,
        TimeProvider? clock = null)
    {
        this.migration = migration;
        this.references = references;
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
        if (rows.Count == 0 || rows.Any(item => item.Parsed.Payload is not MigrationApOpeningPayload))
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_ap_opening_plan_invalid");
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null)
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("finance_request_context_unavailable");

        var expectations = new Dictionary<Guid, FinanceMigrationOpeningExpectation>();
        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            if (row.Parsed.Payload is not MigrationApOpeningPayload payload
                || payload.CompanyId is not { } companyId
                || payload.SupplierId is not { } supplierId
                || string.IsNullOrWhiteSpace(payload.SourceReference)
                || payload.DocumentDate is not { } documentDate
                || payload.OpeningDate is not { } openingDate
                || payload.Amount is not { } amount
                || string.IsNullOrWhiteSpace(payload.CurrencyCode)
                || payload.DueDate is null && payload.PaymentTermId is null)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_execution_payload_invalid");

            var checks = await references.ValidateAsync(requestContext, row.Parsed, cancellationToken);
            var failedCheck = checks.FirstOrDefault(item => item.State is not (MigrationReferenceState.NotApplicable or MigrationReferenceState.Active));
            if (failedCheck is not null)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(failedCheck.Code);

            var command = Command(payload, amount, companyId, supplierId, documentDate, openingDate, row.Staged.PayloadHash, OwnerKey(run.RunId, row.Staged.StagedRecordId, "finance"), row.Staged.StagedRecordId);
            var ready = await finance.PreflightMigrationApOpeningAsync(financeContext, command, cancellationToken);
            if (!ready.Ready)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(ready.Code);
            if (ready.Expectation is { } expectation && MigrationOpeningMonetaryMatching.HasMonetaryEvidence(expectation))
                expectations[row.Staged.StagedRecordId] = expectation;
        }

        var batchId = StableId($"migration-ap-opening-batch:{attempt.AttemptId:D}");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', rows.OrderBy(item => item.Staged.SourceSequence).Select(item => item.Staged.PayloadHash)))));
        var batch = new MigrationExecutionBatchRecord(
            batchId,
            tenant.TenantId,
            run.RunId,
            attempt.AttemptId,
            MigrationCanonicalRecordType.ApOpening,
            MigrationExecutionBatchState.Prepared,
            batchId,
            fingerprint,
            clock.GetUtcNow(),
            null,
            null,
            run.CorrelationId.Value,
            Guid.NewGuid().ToByteArray());
        var savedBatch = await migration.CreateBatchAsync(tenant, new CreateMigrationExecutionBatchCommand(batch), cancellationToken);
        if (!savedBatch.Succeeded)
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(savedBatch.Code);

        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            var effect = new MigrationExecutionEffectRecord(
                StableId($"migration-ap-opening-effect:{attempt.AttemptId:D}:{row.Staged.StagedRecordId:D}"),
                tenant.TenantId,
                run.RunId,
                attempt.AttemptId,
                row.Staged.StagedRecordId,
                row.Staged.SourceSequence,
                MigrationCanonicalRecordType.ApOpening,
                batchId,
                null,
                null,
                ((MigrationApOpeningPayload)row.Parsed.Payload).SourceReference?.Trim(),
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
            if (expectations.TryGetValue(row.Staged.StagedRecordId, out var expectation))
            {
                var savedExpectation = await migration.CreateRepresentationAsync(tenant, new CreateMigrationEconomicRepresentationCommand(ExpectedRepresentation(tenant, attempt, effect, expectation)), cancellationToken);
                if (!savedExpectation.Succeeded)
                    return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_economic_expectation_persistence_unknown");
            }
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
        var batch = await migration.FindBatchAsync(tenant, run.RunId, attempt.AttemptId, MigrationCanonicalRecordType.ApOpening, cancellationToken);
        if (batch is null) return MigrationEconomicGroupResult.Failure("migration_execution_batch_not_found", false);
        if (batch.State == MigrationExecutionBatchState.Prepared)
        {
            var started = await migration.UpdateBatchAsync(tenant, new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Started, clock.GetUtcNow(), null, batch.Version), cancellationToken);
            if (!started.Succeeded) return MigrationEconomicGroupResult.Failure(started.Code, started.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
            batch = started.Value!;
        }
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null)
            return MigrationEconomicGroupResult.Failure("finance_request_context_unavailable", false);

        var representations = await migration.ListRepresentationsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken) ?? [];
        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            var effect = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).SingleOrDefault(item => item.StagedRecordId == row.Staged.StagedRecordId);
            if (effect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_not_found", false);
            if (effect.Disposition == MigrationExecutionEffectDisposition.Unknown)
                return MigrationEconomicGroupResult.Failure(effect.SafeCode ?? "migration_execution_outcome_unknown", true);
            if (effect.Disposition == MigrationExecutionEffectDisposition.Committed) continue;
            if (row.Parsed.Payload is not MigrationApOpeningPayload payload || !ReadyPayload(payload))
                return await FailEffectAsync(tenant, batch, effect, "migration_execution_payload_invalid", false, cancellationToken);

            var expectedRepresentation = representations.FirstOrDefault(item => item.EffectId == effect.Id && item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation);
            var expected = MigrationOpeningMonetaryMatching.HasMonetaryEvidence(expectedRepresentation) ? MigrationOpeningMonetaryMatching.FromRepresentation(expectedRepresentation!) : null;
            var command = Command(payload, payload.Amount!.Value, payload.CompanyId!.Value, payload.SupplierId!.Value, payload.DocumentDate!.Value, payload.OpeningDate!.Value, row.Staged.PayloadHash, OwnerKey(run.RunId, row.Staged.StagedRecordId, "finance"), row.Staged.StagedRecordId, expected);
            effect = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Started, null, null, null, clock.GetUtcNow(), null, cancellationToken);
            if (effect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);

            FinanceOperationResult<FinanceOpenItemRecord> response;
            var responseLost = false;
            try
            {
                response = await finance.CreateMigrationApOpeningAsync(financeContext, command, cancellationToken);
            }
            catch (Exception)
            {
                responseLost = true;
                response = FinanceOperationResult<FinanceOpenItemRecord>.Failure("finance_ap_opening_outcome_unknown");
            }

            FinanceMigrationApOpeningEvidence? evidence;
            try
            {
                evidence = await finance.ReadMigrationApOpeningAsync(financeContext, command, CancellationToken.None);
            }
            catch (Exception)
            {
                return await FailEffectAsync(tenant, batch, effect, "finance_ap_opening_outcome_unknown", true, CancellationToken.None);
            }
            if (evidence is not null)
            {
                if (!EvidenceMatches(evidence, command))
                    return await FailEffectAsync(tenant, batch, effect, "finance_ap_opening_evidence_mismatch", true, CancellationToken.None);
                if (!await SaveRepresentationsAsync(tenant, attempt, effect, evidence, CancellationToken.None))
                    return await FailEffectAsync(tenant, batch, effect, "migration_economic_evidence_persistence_unknown", true, CancellationToken.None);
                var committed = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Committed, evidence.OpenItem.Id, evidence.OpenItem.Id, null, effect.EffectStartedAt, clock.GetUtcNow(), CancellationToken.None);
                if (committed is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
                continue;
            }

            var missingEvidenceCode = response.Succeeded ? "finance_ap_opening_evidence_unavailable" : response.Code;
            var unknown = responseLost || response.Succeeded || response.Code is "finance_unavailable" or "finance_ap_opening_outcome_unknown" or "migration_ap_opening_outcome_unknown";
            return await FailEffectAsync(tenant, batch, effect, missingEvidenceCode, unknown, unknown ? CancellationToken.None : cancellationToken);
        }

        var finalEffects = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.ApOpening).ToArray();
        var state = finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)
            ? MigrationExecutionBatchState.Unknown
            : finalEffects.All(item => item.Disposition == MigrationExecutionEffectDisposition.Committed)
                ? MigrationExecutionBatchState.Completed
                : MigrationExecutionBatchState.Failed;
        await FinishBatchAsync(tenant, batch, state, cancellationToken);
        return state == MigrationExecutionBatchState.Completed
            ? new MigrationEconomicGroupResult(true, "migration_ap_opening_completed", false)
            : MigrationEconomicGroupResult.Failure("migration_execution_partially_completed", state == MigrationExecutionBatchState.Unknown);
    }

    internal async Task MarkPreparationFailedAsync(TenantContext tenant, MigrationAttemptRecord attempt, string code, CancellationToken cancellationToken)
    {
        foreach (var effect in (await migration.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.ApOpening && item.Disposition == MigrationExecutionEffectDisposition.Prepared))
            await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Failed, null, null, code, null, clock.GetUtcNow(), cancellationToken);
        var batch = await migration.FindBatchAsync(tenant, attempt.RunId, attempt.AttemptId, MigrationCanonicalRecordType.ApOpening, cancellationToken);
        if (batch is not null) await FinishBatchAsync(tenant, batch, MigrationExecutionBatchState.Failed, cancellationToken);
    }

    internal async Task<IReadOnlyList<MigrationApEconomicReconciliationRecord>> ReadReconciliationsAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        IReadOnlyList<MigrationExecutionEffectRecord> effects,
        IReadOnlyList<MigrationStagedRecord> staged,
        CancellationToken cancellationToken)
    {
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null) return [];
        var stagedById = staged.ToDictionary(item => item.StagedRecordId);
        var result = new List<MigrationApEconomicReconciliationRecord>();
        foreach (var effect in effects.Where(item => item.RecordType == MigrationCanonicalRecordType.ApOpening).OrderBy(item => item.SourceSequence))
        {
            if (!stagedById.TryGetValue(effect.StagedRecordId, out var stagedRow)) continue;
            MigrationApOpeningPayload? payload;
            try { payload = JsonSerializer.Deserialize<MigrationApOpeningPayload>(stagedRow.CanonicalPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { payload = null; }
            if (payload?.CompanyId is not { } companyId || payload.SupplierId is not { } supplierId || string.IsNullOrWhiteSpace(payload.SourceReference) || payload.Amount is not { } amount || string.IsNullOrWhiteSpace(payload.CurrencyCode) || payload.DocumentDate is not { } documentDate || payload.OpeningDate is not { } openingDate)
            {
                result.Add(new(effect.Id, effect.SourceSequence, "unavailable", "migration_ap_opening_payload_invalid", Guid.Empty, Guid.Empty, string.Empty, 0m, null, null, null, null, string.Empty, null, null, clock.GetUtcNow()));
                continue;
            }
            var command = Command(payload, amount, companyId, supplierId, documentDate, openingDate, stagedRow.PayloadHash, "reconciliation");
            FinanceMigrationApOpeningEvidence? evidence = null;
            try { evidence = await finance.ReadMigrationApOpeningAsync(financeContext, command, cancellationToken); }
            catch { }
            var journalAmount = evidence?.OpenItem.OriginalFunctionalAmount;
            var exact = evidence is not null && EvidenceMatches(evidence, command) && journalAmount is not null;
            var monetary = MigrationOpeningMonetaryMatching.ReconciliationFields(evidence?.MonetaryEvidence);
            result.Add(new(effect.Id, effect.SourceSequence, exact ? "reconciled" : "partial", exact ? null : "finance_ap_opening_evidence_not_reconciled", companyId, supplierId, payload.SourceReference.Trim(), amount, evidence?.OpenItem.OriginalAmount, evidence?.OpenItem.OutstandingAmount, journalAmount, evidence?.OpenItem.AllocatedAmount, payload.CurrencyCode.Trim().ToUpperInvariant(), evidence?.OpenItem.Id, evidence?.RecognitionJournal.Id, clock.GetUtcNow(), monetary.TransactionCurrencyCode, monetary.TransactionAmount, monetary.FunctionalCurrencyCode, monetary.FunctionalAmount, monetary.RateDate, monetary.ExchangeRateId, monetary.ExchangeRateVersionId, monetary.ExchangeRateVersionNumber, monetary.AppliedRate, monetary.MonetaryPolicyId, monetary.MonetaryPolicyVersionNumber, monetary.RoundingScale, monetary.RoundingMode, monetary.FunctionalRoundingDifference, monetary.ReportingCurrencyCode, monetary.ReportingAmount, monetary.ReportingExchangeRateId, monetary.ReportingExchangeRateVersionId, monetary.ReportingExchangeRateVersionNumber, monetary.ReportingAppliedRate, monetary.ReportingEvidenceStatus));
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

    private async Task<bool> SaveRepresentationsAsync(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, FinanceMigrationApOpeningEvidence evidence, CancellationToken cancellationToken)
    {
        var journal = Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Finance, MigrationEconomicRepresentationKind.FinanceJournal, evidence.RecognitionJournal.Id, evidence.RecognitionJournal.JournalNumber, evidence.RecognitionJournal.Status.ToString(), Convert.ToHexString(evidence.RecognitionJournal.Version), evidence.RecognitionJournal.PostedAt ?? evidence.RecognitionJournal.CreatedAt);
        var openItem = Representation(tenant, attempt, effect, MigrationEconomicOwnerModule.Finance, MigrationEconomicRepresentationKind.FinanceOpenItem, evidence.OpenItem.Id, evidence.OpenItem.Reference, evidence.OpenItem.Status.ToString(), Convert.ToHexString(evidence.OpenItem.Version), clock.GetUtcNow());
        return (await migration.CreateRepresentationAsync(tenant, new CreateMigrationEconomicRepresentationCommand(journal), cancellationToken)).Succeeded
            && (await migration.CreateRepresentationAsync(tenant, new CreateMigrationEconomicRepresentationCommand(openItem), cancellationToken)).Succeeded;
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

    private static FinanceMigrationApOpeningCommand Command(MigrationApOpeningPayload payload, decimal amount, Guid companyId, Guid supplierId, DateOnly documentDate, DateOnly openingDate, string fingerprint, string idempotencyKey, Guid sourceRecordId = default, FinanceMigrationOpeningExpectation? expected = null) => new(companyId, supplierId, payload.SourceReference!.Trim(), documentDate, openingDate, amount, payload.CurrencyCode!.Trim(), payload.DueDate, payload.PaymentTermId, fingerprint, idempotencyKey, fingerprint, sourceRecordId, expected);
    private static bool ReadyPayload(MigrationApOpeningPayload payload) => payload.CompanyId is not null && payload.SupplierId is not null && !string.IsNullOrWhiteSpace(payload.SourceReference) && payload.DocumentDate is not null && payload.OpeningDate is not null && payload.Amount is > 0m && !string.IsNullOrWhiteSpace(payload.CurrencyCode) && (payload.DueDate is not null || payload.PaymentTermId is not null);
    private static bool EvidenceMatches(FinanceMigrationApOpeningEvidence evidence, FinanceMigrationApOpeningCommand command) => evidence.OpenItem.Kind == FinanceOpenItemKind.Payable && evidence.OpenItem.CompanyId == command.CompanyId && evidence.OpenItem.SupplierId == command.SupplierId && evidence.OpenItem.SourceContract == "migration-ap-opening.v1" && evidence.OpenItem.Reference == command.SourceReference.Trim() && evidence.OpenItem.DocumentDate == command.DocumentDate && (command.DueDate is { } explicitDue ? evidence.OpenItem.DueDate == explicitDue : evidence.OpenItem.PaymentTerm?.DueDate == evidence.OpenItem.DueDate) && evidence.OpenItem.OriginalAmount == command.Amount && string.Equals(evidence.OpenItem.CurrencyCode, command.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase) && evidence.OpenItem.RecognitionState == FinanceOpenItemRecognitionState.Recognized && evidence.OpenItem.RecognitionJournalId == evidence.RecognitionJournal.Id && evidence.RecognitionJournal.CompanyId == command.CompanyId && evidence.RecognitionJournal.Status == FinanceJournalStatus.Posted && evidence.RecognitionJournal.PostingDate == command.OpeningDate && string.Equals(evidence.RecognitionJournal.TransactionCurrencyCode, command.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase) && evidence.RecognitionJournal.SourceContract == "migration-ap-opening.v1" && evidence.RecognitionJournal.SourceEvidenceId == evidence.OpenItem.SourceEvidenceId && evidence.RecognitionJournal.SourceEvidenceVersion == evidence.OpenItem.SourceEvidenceVersion && evidence.SourceEffect.CompanyId == command.CompanyId && evidence.SourceEffect.SourceContract == "migration-ap-opening.v1" && evidence.SourceEffect.SourceEvidenceId == evidence.OpenItem.SourceEvidenceId && evidence.SourceEffect.SourceEvidenceVersion == evidence.OpenItem.SourceEvidenceVersion && evidence.SourceEffect.JournalId == evidence.RecognitionJournal.Id && MigrationOpeningMonetaryMatching.Matches(command.ExpectedExpectation, evidence.MonetaryEvidence, command.CurrencyCode, command.Amount, evidence.OpenItem.FunctionalCurrencyCode, command.OpeningDate);
    private MigrationEconomicRepresentationRecord ExpectedRepresentation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, FinanceMigrationOpeningExpectation expected) => new(StableId($"migration-ap-opening-expectation:{effect.Id:D}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, MigrationEconomicOwnerModule.Finance, MigrationEconomicRepresentationKind.FinanceOpeningExpectation, expected.SourceRecordId, expected.OwnerReference, "prepared", "migration-economic-expectation-v1", clock.GetUtcNow(), clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray(), expected.SourceContract, expected.SourceEvent, expected.FunctionalAmount, expected.PostingRuleId, expected.PostingRuleVersionNumber, expected.ControlAccountId, expected.OffsetAccountId, expected.Reversal, expected.SourceEvidenceId, expected.SourceEvidenceVersion, expected.OwnerSourceId, expected.TransactionCurrencyCode, expected.TransactionAmount, expected.ExpectedFunctionalCurrencyCode, expected.RateDate, expected.ExchangeRateId, expected.ExchangeRateVersionId, expected.ExchangeRateVersionNumber, expected.AppliedRate, expected.MonetaryPolicyId, expected.MonetaryPolicyVersionNumber, expected.RoundingScale, expected.RoundingMode, expected.ReportingCurrencyCode, expected.ReportingExchangeRateId, expected.ReportingExchangeRateVersionId, expected.ReportingExchangeRateVersionNumber, expected.ReportingAppliedRate);
    private MigrationEconomicRepresentationRecord Representation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, MigrationEconomicOwnerModule module, MigrationEconomicRepresentationKind kind, Guid ownerId, string? reference, string status, string version, DateTimeOffset occurredAt) => new(StableId($"migration-ap-representation:{effect.Id:D}:{module}:{kind}:{ownerId:D}:{version}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, module, kind, ownerId, reference, status, version, occurredAt, clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray());
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    private static string OwnerKey(Guid runId, Guid stagedRecordId, string step) => $"migration-ap-opening:{runId:N}:{stagedRecordId:N}:{step}";
}

#pragma warning restore CS1591
