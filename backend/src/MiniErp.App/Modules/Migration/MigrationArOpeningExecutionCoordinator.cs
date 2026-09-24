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

internal sealed class MigrationArOpeningExecutionCoordinator
{
    private readonly IMigrationExecutionPersistence migration;
    private readonly IMigrationReferenceAuthority references;
    private readonly IFinanceSettlementPersistence finance;
    private readonly TimeProvider clock;

    public MigrationArOpeningExecutionCoordinator(
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
        if (rows.Count == 0 || rows.Any(item => item.Parsed.Payload is not MigrationArOpeningPayload))
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("migration_ar_opening_plan_invalid");
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null)
            return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure("finance_request_context_unavailable");

        var expectations = new Dictionary<Guid, FinanceMigrationOpeningExpectation>();
        foreach (var row in rows.OrderBy(item => item.Staged.SourceSequence))
        {
            if (row.Parsed.Payload is not MigrationArOpeningPayload payload
                || payload.CompanyId is not { } companyId
                || payload.CustomerId is not { } customerId
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

            var command = Command(payload, amount, companyId, customerId, documentDate, openingDate, row.Staged.PayloadHash, OwnerKey(run.RunId, row.Staged.StagedRecordId, "finance"), row.Staged.StagedRecordId);
            var ready = await finance.PreflightMigrationArOpeningAsync(financeContext, command, cancellationToken);
            if (!ready.Ready)
                return MigrationOwnerExecutionCoordinator.OwnerPreparationResult.Failure(ready.Code);
            if (ready.Expectation is { } expectation)
                expectations[row.Staged.StagedRecordId] = expectation;
        }

        var batchId = StableId($"migration-ar-opening-batch:{attempt.AttemptId:D}");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', rows.OrderBy(item => item.Staged.SourceSequence).Select(item => item.Staged.PayloadHash)))));
        var batch = new MigrationExecutionBatchRecord(
            batchId,
            tenant.TenantId,
            run.RunId,
            attempt.AttemptId,
            MigrationCanonicalRecordType.ArOpening,
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
                StableId($"migration-ar-opening-effect:{attempt.AttemptId:D}:{row.Staged.StagedRecordId:D}"),
                tenant.TenantId,
                run.RunId,
                attempt.AttemptId,
                row.Staged.StagedRecordId,
                row.Staged.SourceSequence,
                MigrationCanonicalRecordType.ArOpening,
                batchId,
                null,
                null,
                ((MigrationArOpeningPayload)row.Parsed.Payload).SourceReference?.Trim(),
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
        var batch = await migration.FindBatchAsync(tenant, run.RunId, attempt.AttemptId, MigrationCanonicalRecordType.ArOpening, cancellationToken);
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
            if (row.Parsed.Payload is not MigrationArOpeningPayload payload || !ReadyPayload(payload))
                return await FailEffectAsync(tenant, batch, effect, "migration_execution_payload_invalid", false, cancellationToken);

            var expectedRepresentation = representations.FirstOrDefault(item => item.EffectId == effect.Id && item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation);
            var expected = MigrationOpeningMonetaryMatching.HasMonetaryEvidence(expectedRepresentation) ? MigrationOpeningMonetaryMatching.FromRepresentation(expectedRepresentation!) : null;
            var command = Command(payload, payload.Amount!.Value, payload.CompanyId!.Value, payload.CustomerId!.Value, payload.DocumentDate!.Value, payload.OpeningDate!.Value, row.Staged.PayloadHash, OwnerKey(run.RunId, row.Staged.StagedRecordId, "finance"), row.Staged.StagedRecordId, expected);
            effect = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Started, null, null, null, clock.GetUtcNow(), null, cancellationToken);
            if (effect is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);

            FinanceOperationResult<FinanceOpenItemRecord> response;
            var responseLost = false;
            try
            {
                response = await finance.CreateMigrationArOpeningAsync(financeContext, command, cancellationToken);
            }
            catch (Exception)
            {
                responseLost = true;
                response = FinanceOperationResult<FinanceOpenItemRecord>.Failure("finance_ar_opening_outcome_unknown");
            }

            FinanceMigrationArOpeningEvidence? evidence;
            try
            {
                evidence = await finance.ReadMigrationArOpeningAsync(financeContext, command, CancellationToken.None);
            }
            catch (Exception)
            {
                return await FailEffectAsync(tenant, batch, effect, "finance_ar_opening_outcome_unknown", true, CancellationToken.None);
            }
            if (evidence is not null)
            {
                var exact = EvidenceMatches(evidence, command);
                if (!exact)
                    return await FailEffectAsync(tenant, batch, effect, "finance_ar_opening_evidence_mismatch", true, CancellationToken.None);
                if (!await SaveRepresentationsAsync(tenant, attempt, effect, evidence, CancellationToken.None))
                    return await FailEffectAsync(tenant, batch, effect, "migration_economic_evidence_persistence_unknown", true, CancellationToken.None);
                var committed = await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Committed, evidence.OpenItem.Id, evidence.OpenItem.Id, null, effect.EffectStartedAt, clock.GetUtcNow(), CancellationToken.None);
                if (committed is null) return MigrationEconomicGroupResult.Failure("migration_execution_effect_state_conflict", false);
                continue;
            }

            var missingEvidenceCode = response.Succeeded ? "finance_ar_opening_evidence_unavailable" : response.Code;
            var unknown = responseLost || response.Succeeded || response.Code is "finance_unavailable" or "finance_ar_opening_outcome_unknown" or "migration_ar_opening_outcome_unknown";
            return await FailEffectAsync(tenant, batch, effect, missingEvidenceCode, unknown, unknown ? CancellationToken.None : cancellationToken);
        }

        var finalEffects = (await migration.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.ArOpening).ToArray();
        var state = finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)
            ? MigrationExecutionBatchState.Unknown
            : finalEffects.All(item => item.Disposition == MigrationExecutionEffectDisposition.Committed)
                ? MigrationExecutionBatchState.Completed
                : MigrationExecutionBatchState.Failed;
        await FinishBatchAsync(tenant, batch, state, cancellationToken);
        return state == MigrationExecutionBatchState.Completed
            ? new MigrationEconomicGroupResult(true, "migration_ar_opening_completed", false)
            : MigrationEconomicGroupResult.Failure("migration_execution_partially_completed", state == MigrationExecutionBatchState.Unknown);
    }

    internal async Task MarkPreparationFailedAsync(TenantContext tenant, MigrationAttemptRecord attempt, string code, CancellationToken cancellationToken)
    {
        foreach (var effect in (await migration.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken)).Where(item => item.RecordType == MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.Prepared))
            await ChangeEffectAsync(tenant, effect, MigrationExecutionEffectDisposition.Failed, null, null, code, null, clock.GetUtcNow(), cancellationToken);
        var batch = await migration.FindBatchAsync(tenant, attempt.RunId, attempt.AttemptId, MigrationCanonicalRecordType.ArOpening, cancellationToken);
        if (batch is not null) await FinishBatchAsync(tenant, batch, MigrationExecutionBatchState.Failed, cancellationToken);
    }

    internal async Task<IReadOnlyList<MigrationArEconomicReconciliationRecord>> ReadReconciliationsAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        IReadOnlyList<MigrationExecutionEffectRecord> effects,
        IReadOnlyList<MigrationStagedRecord> staged,
        CancellationToken cancellationToken)
    {
        if (!FinanceRequestContext.TryCreate(requestContext, out var financeContext) || financeContext is null) return [];
        var stagedById = staged.ToDictionary(item => item.StagedRecordId);
        var result = new List<MigrationArEconomicReconciliationRecord>();
        foreach (var effect in effects.Where(item => item.RecordType == MigrationCanonicalRecordType.ArOpening).OrderBy(item => item.SourceSequence))
        {
            if (!stagedById.TryGetValue(effect.StagedRecordId, out var stagedRow)) continue;
            MigrationArOpeningPayload? payload;
            try { payload = JsonSerializer.Deserialize<MigrationArOpeningPayload>(stagedRow.CanonicalPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { payload = null; }
            if (payload?.CompanyId is not { } companyId || payload.CustomerId is not { } customerId || string.IsNullOrWhiteSpace(payload.SourceReference) || payload.Amount is not { } amount || string.IsNullOrWhiteSpace(payload.CurrencyCode) || payload.DocumentDate is not { } documentDate || payload.OpeningDate is not { } openingDate)
            {
                result.Add(new(effect.Id, effect.SourceSequence, "unavailable", "migration_ar_opening_payload_invalid", Guid.Empty, Guid.Empty, string.Empty, 0m, null, null, null, null, string.Empty, null, null, clock.GetUtcNow()));
                continue;
            }
            var command = Command(payload, amount, companyId, customerId, documentDate, openingDate, stagedRow.PayloadHash, "reconciliation");
            FinanceMigrationArOpeningEvidence? evidence = null;
            try { evidence = await finance.ReadMigrationArOpeningAsync(financeContext, command, cancellationToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
            var journalAmount = evidence?.RecognitionJournal.Lines.Sum(item => item.FunctionalDebit);
            var monetary = MigrationOpeningMonetaryMatching.ReconciliationFields(evidence?.MonetaryEvidence);
            var exact = evidence is not null && EvidenceMatches(evidence, command)
                && journalAmount == (monetary.FunctionalAmount ?? amount);
            result.Add(new(effect.Id, effect.SourceSequence, exact ? "reconciled" : "partial", exact ? null : "finance_ar_opening_evidence_not_reconciled", companyId, customerId, payload.SourceReference.Trim(), amount, evidence?.OpenItem.OriginalAmount, evidence?.OpenItem.OutstandingAmount, journalAmount, evidence?.OpenItem.AllocatedAmount, payload.CurrencyCode.Trim().ToUpperInvariant(), evidence?.OpenItem.Id, evidence?.RecognitionJournal.Id, clock.GetUtcNow(), monetary.TransactionCurrencyCode, monetary.TransactionAmount, monetary.FunctionalCurrencyCode, monetary.FunctionalAmount, monetary.RateDate, monetary.ExchangeRateId, monetary.ExchangeRateVersionId, monetary.ExchangeRateVersionNumber, monetary.AppliedRate, monetary.MonetaryPolicyId, monetary.MonetaryPolicyVersionNumber, monetary.RoundingScale, monetary.RoundingMode, monetary.FunctionalRoundingDifference, monetary.ReportingCurrencyCode, monetary.ReportingAmount, monetary.ReportingExchangeRateId, monetary.ReportingExchangeRateVersionId, monetary.ReportingExchangeRateVersionNumber, monetary.ReportingAppliedRate, monetary.ReportingEvidenceStatus));
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

    private async Task<bool> SaveRepresentationsAsync(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, FinanceMigrationArOpeningEvidence evidence, CancellationToken cancellationToken)
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

    private static FinanceMigrationArOpeningCommand Command(MigrationArOpeningPayload payload, decimal amount, Guid companyId, Guid customerId, DateOnly documentDate, DateOnly openingDate, string fingerprint, string idempotencyKey, Guid sourceRecordId = default, FinanceMigrationOpeningExpectation? expected = null) => new(companyId, customerId, payload.SourceReference!.Trim(), documentDate, openingDate, amount, payload.CurrencyCode!.Trim(), payload.DueDate, payload.PaymentTermId, fingerprint, idempotencyKey, fingerprint, sourceRecordId, expected);
    private static bool ReadyPayload(MigrationArOpeningPayload payload) => payload.CompanyId is not null && payload.CustomerId is not null && !string.IsNullOrWhiteSpace(payload.SourceReference) && payload.DocumentDate is not null && payload.OpeningDate is not null && payload.Amount is > 0m && !string.IsNullOrWhiteSpace(payload.CurrencyCode) && (payload.DueDate is not null || payload.PaymentTermId is not null);
    private static bool EvidenceMatches(FinanceMigrationArOpeningEvidence evidence, FinanceMigrationArOpeningCommand command) => evidence.OpenItem.Kind == FinanceOpenItemKind.Receivable && evidence.OpenItem.CompanyId == command.CompanyId && evidence.OpenItem.CustomerId == command.CustomerId && evidence.OpenItem.SourceContract == "migration-ar-opening.v1" && evidence.OpenItem.Reference == command.SourceReference.Trim() && evidence.OpenItem.DocumentDate == command.DocumentDate && (command.DueDate is { } explicitDue ? evidence.OpenItem.DueDate == explicitDue : evidence.OpenItem.PaymentTerm?.DueDate == evidence.OpenItem.DueDate) && evidence.OpenItem.OriginalAmount == command.Amount && string.Equals(evidence.OpenItem.CurrencyCode, command.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase) && evidence.OpenItem.RecognitionState == FinanceOpenItemRecognitionState.Recognized && evidence.OpenItem.RecognitionJournalId == evidence.RecognitionJournal.Id && evidence.RecognitionJournal.CompanyId == command.CompanyId && evidence.RecognitionJournal.Status == FinanceJournalStatus.Posted && evidence.RecognitionJournal.PostingDate == command.OpeningDate && string.Equals(evidence.RecognitionJournal.TransactionCurrencyCode, command.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase) && evidence.RecognitionJournal.SourceContract == "migration-ar-opening.v1" && evidence.RecognitionJournal.SourceEvidenceId == evidence.OpenItem.SourceEvidenceId && evidence.RecognitionJournal.SourceEvidenceVersion == evidence.OpenItem.SourceEvidenceVersion && evidence.SourceEffect.CompanyId == command.CompanyId && evidence.SourceEffect.SourceContract == "migration-ar-opening.v1" && evidence.SourceEffect.SourceEvidenceId == evidence.OpenItem.SourceEvidenceId && evidence.SourceEffect.SourceEvidenceVersion == evidence.OpenItem.SourceEvidenceVersion && evidence.SourceEffect.JournalId == evidence.RecognitionJournal.Id && MigrationOpeningMonetaryMatching.Matches(command.ExpectedExpectation, evidence.MonetaryEvidence, command.CurrencyCode, command.Amount, evidence.OpenItem.FunctionalCurrencyCode, command.OpeningDate);
    private MigrationEconomicRepresentationRecord ExpectedRepresentation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, FinanceMigrationOpeningExpectation expected) => new(StableId($"migration-ar-opening-expectation:{effect.Id:D}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, MigrationEconomicOwnerModule.Finance, MigrationEconomicRepresentationKind.FinanceOpeningExpectation, expected.SourceRecordId, expected.OwnerReference, "prepared", "migration-economic-expectation-v1", clock.GetUtcNow(), clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray(), expected.SourceContract, expected.SourceEvent, expected.FunctionalAmount, expected.PostingRuleId, expected.PostingRuleVersionNumber, expected.ControlAccountId, expected.OffsetAccountId, expected.Reversal, expected.SourceEvidenceId, expected.SourceEvidenceVersion, expected.OwnerSourceId, expected.TransactionCurrencyCode, expected.TransactionAmount, expected.ExpectedFunctionalCurrencyCode, expected.RateDate, expected.ExchangeRateId, expected.ExchangeRateVersionId, expected.ExchangeRateVersionNumber, expected.AppliedRate, expected.MonetaryPolicyId, expected.MonetaryPolicyVersionNumber, expected.RoundingScale, expected.RoundingMode, expected.ReportingCurrencyCode, expected.ReportingExchangeRateId, expected.ReportingExchangeRateVersionId, expected.ReportingExchangeRateVersionNumber, expected.ReportingAppliedRate);
    private MigrationEconomicRepresentationRecord Representation(TenantContext tenant, MigrationAttemptRecord attempt, MigrationExecutionEffectRecord effect, MigrationEconomicOwnerModule module, MigrationEconomicRepresentationKind kind, Guid ownerId, string? reference, string status, string version, DateTimeOffset occurredAt) => new(StableId($"migration-ar-representation:{effect.Id:D}:{module}:{kind}:{ownerId:D}:{version}"), tenant.TenantId, attempt.RunId, attempt.AttemptId, effect.Id, module, kind, ownerId, reference, status, version, occurredAt, clock.GetUtcNow(), true, Guid.NewGuid().ToByteArray());
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    private static string OwnerKey(Guid runId, Guid stagedRecordId, string step) => $"migration-ar-opening:{runId:N}:{stagedRecordId:N}:{step}";
}

internal static class MigrationOpeningMonetaryMatching
{
    internal static bool HasMonetaryEvidence(FinanceMigrationOpeningExpectation? expectation) => expectation is not null && (expectation.MonetaryPolicyId is not null || expectation.ExchangeRateId is not null || expectation.ReportingExchangeRateId is not null);
    internal static bool HasMonetaryEvidence(MigrationEconomicRepresentationRecord? representation) => representation is not null && (representation.MonetaryPolicyId is not null || representation.ExchangeRateId is not null || representation.ReportingExchangeRateId is not null);

    internal static bool Matches(FinanceMigrationOpeningExpectation? expected, FinanceMonetaryEvidence? actual, string transactionCurrency, decimal transactionAmount, string functionalCurrency, DateOnly openingDate)
    {
        if (expected is null) return true;
        if (!string.Equals(expected.TransactionCurrencyCode, transactionCurrency, StringComparison.OrdinalIgnoreCase)
            || expected.TransactionAmount != transactionAmount
            || !string.Equals(expected.ExpectedFunctionalCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            || expected.FunctionalAmount <= 0m)
            return false;
        if (actual is null)
            return expected.ExchangeRateId is null && expected.MonetaryPolicyId is null;
        return actual.FunctionalAmount == expected.FunctionalAmount
            && actual.TransactionToFunctionalRate?.ExchangeRateId == expected.ExchangeRateId
            && actual.TransactionToFunctionalRate?.ExchangeRateVersionId == expected.ExchangeRateVersionId
            && actual.TransactionToFunctionalRate?.VersionNumber == expected.ExchangeRateVersionNumber
            && actual.TransactionToFunctionalRate?.Rate == expected.AppliedRate
            && actual.MonetaryPolicyId == expected.MonetaryPolicyId
            && actual.MonetaryPolicyVersionNumber == expected.MonetaryPolicyVersionNumber
            && actual.RoundingScale == expected.RoundingScale
            && string.Equals(actual.RoundingMode, expected.RoundingMode, StringComparison.Ordinal)
            && string.Equals(actual.ReportingCurrencyCode, expected.ReportingCurrencyCode, StringComparison.OrdinalIgnoreCase)
            && actual.FunctionalToReportingRate?.ExchangeRateId == expected.ReportingExchangeRateId
            && actual.FunctionalToReportingRate?.ExchangeRateVersionId == expected.ReportingExchangeRateVersionId
            && actual.FunctionalToReportingRate?.VersionNumber == expected.ReportingExchangeRateVersionNumber
            && actual.FunctionalToReportingRate?.Rate == expected.ReportingAppliedRate
            && (expected.RateDate is null || expected.RateDate == openingDate);
    }

    internal static MigrationOpeningReconciliationMonetaryFields ReconciliationFields(FinanceMonetaryEvidence? evidence) => evidence is null
        ? new MigrationOpeningReconciliationMonetaryFields(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
        : new MigrationOpeningReconciliationMonetaryFields(
            evidence.TransactionCurrencyCode,
            evidence.TransactionAmount,
            evidence.FunctionalCurrencyCode,
            evidence.FunctionalAmount,
            evidence.TransactionToFunctionalRate?.EffectiveOn,
            evidence.TransactionToFunctionalRate?.ExchangeRateId,
            evidence.TransactionToFunctionalRate?.ExchangeRateVersionId,
            evidence.TransactionToFunctionalRate?.VersionNumber,
            evidence.TransactionToFunctionalRate?.Rate,
            evidence.MonetaryPolicyId,
            evidence.MonetaryPolicyVersionNumber,
            evidence.RoundingScale,
            evidence.RoundingMode,
            evidence.FunctionalRoundingDifference,
            evidence.ReportingCurrencyCode,
            evidence.ReportingAmount,
            evidence.FunctionalToReportingRate?.ExchangeRateId,
            evidence.FunctionalToReportingRate?.ExchangeRateVersionId,
            evidence.FunctionalToReportingRate?.VersionNumber,
            evidence.FunctionalToReportingRate?.Rate,
            evidence.ReportingEvidenceStatus.ToString());

    internal static FinanceMigrationOpeningExpectation FromRepresentation(MigrationEconomicRepresentationRecord record) => new(
        record.OwnerId,
        record.SourceContract ?? string.Empty,
        record.SourceEvent ?? string.Empty,
        record.FunctionalAmount ?? 0m,
        record.PostingRuleId ?? Guid.Empty,
        record.PostingRuleVersionNumber ?? 0,
        record.ControlAccountId ?? Guid.Empty,
        record.OffsetAccountId ?? Guid.Empty,
        record.Reversal ?? false,
        record.SourceEvidenceId,
        record.SourceEvidenceVersion,
        record.OwnerSourceId,
        record.OwnerReference,
        record.TransactionCurrencyCode,
        record.TransactionAmount,
        record.ExpectedFunctionalCurrencyCode,
        record.RateDate,
        record.ExchangeRateId,
        record.ExchangeRateVersionId,
        record.ExchangeRateVersionNumber,
        record.AppliedRate,
        record.MonetaryPolicyId,
        record.MonetaryPolicyVersionNumber,
        record.RoundingScale,
        record.RoundingMode,
        record.ReportingCurrencyCode,
        record.ReportingExchangeRateId,
        record.ReportingExchangeRateVersionId,
        record.ReportingExchangeRateVersionNumber,
        record.ReportingAppliedRate);
}

internal sealed record MigrationOpeningReconciliationMonetaryFields(
    string? TransactionCurrencyCode,
    decimal? TransactionAmount,
    string? FunctionalCurrencyCode,
    decimal? FunctionalAmount,
    DateOnly? RateDate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    decimal? AppliedRate,
    Guid? MonetaryPolicyId,
    int? MonetaryPolicyVersionNumber,
    int? RoundingScale,
    string? RoundingMode,
    decimal? FunctionalRoundingDifference,
    string? ReportingCurrencyCode,
    decimal? ReportingAmount,
    Guid? ReportingExchangeRateId,
    Guid? ReportingExchangeRateVersionId,
    int? ReportingExchangeRateVersionNumber,
    decimal? ReportingAppliedRate,
    string? ReportingEvidenceStatus);

#pragma warning restore CS1591
