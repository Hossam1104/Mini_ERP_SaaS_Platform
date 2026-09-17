using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

/// <summary>
/// Owns the narrow owner-module preparation, effect execution and evidence
/// reconciliation seam. The migration service owns run lifecycle only.
/// </summary>
internal sealed class MigrationOwnerExecutionCoordinator
{
    private static readonly MigrationCanonicalRecordType[] ExecutionOrder =
    [
        MigrationCanonicalRecordType.Currency,
        MigrationCanonicalRecordType.UnitOfMeasure,
        MigrationCanonicalRecordType.PaymentTerm,
        MigrationCanonicalRecordType.Tax,
        MigrationCanonicalRecordType.Supplier,
        MigrationCanonicalRecordType.Customer,
        MigrationCanonicalRecordType.Product
    ];

    private readonly IMigrationExecutionPersistence persistence;
    private readonly IMigrationReferenceAuthority references;
    private readonly IOwnerExecutionGateway owner;
    private readonly TimeProvider timeProvider;

    internal MigrationOwnerExecutionCoordinator(
        IMigrationExecutionPersistence persistence,
        IMigrationReferenceAuthority references,
        IOwnerExecutionGateway owner,
        TimeProvider timeProvider)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    internal async Task<OwnerPreparationResult> PrepareAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        IReadOnlyList<MigrationExecutionPlanRow> plan,
        CancellationToken cancellationToken)
    {
        foreach (var group in plan.GroupBy(item => item.Preview.RecordType).OrderBy(item => Array.IndexOf(ExecutionOrder, item.Key)))
        {
            var type = group.Key;
            var ownerBatchId = StableId($"owner-batch:{attempt.AttemptId:D}:{type}");
            var batchId = StableId($"execution-batch:{attempt.AttemptId:D}:{type}");
            var groupFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', group.Select(item => item.Staged.PayloadHash).OrderBy(item => item, StringComparer.Ordinal)))));
            var batch = new MigrationExecutionBatchRecord(
                batchId,
                tenant.TenantId,
                run.RunId,
                attempt.AttemptId,
                type,
                MigrationExecutionBatchState.Prepared,
                ownerBatchId,
                groupFingerprint,
                timeProvider.GetUtcNow(),
                null,
                null,
                run.CorrelationId.Value,
                Guid.NewGuid().ToByteArray());
            var savedBatch = await persistence.CreateBatchAsync(tenant, new CreateMigrationExecutionBatchCommand(batch), cancellationToken);
            if (!savedBatch.Succeeded)
                return OwnerPreparationResult.Failure(savedBatch.Code);

            foreach (var row in group)
            {
                var effect = new MigrationExecutionEffectRecord(
                    StableId($"execution-effect:{attempt.AttemptId:D}:{row.Staged.StagedRecordId:D}"),
                    tenant.TenantId,
                    run.RunId,
                    attempt.AttemptId,
                    row.Staged.StagedRecordId,
                    row.Staged.SourceSequence,
                    type,
                    ownerBatchId,
                    null,
                    row.Parsed.Payload is MigrationReferencePayload reference ? reference.ReferenceId : null,
                    row.Parsed.Payload is MigrationReferencePayload referenceWithCode ? referenceWithCode.Code : null,
                    MigrationExecutionEffectDisposition.Prepared,
                    null,
                    timeProvider.GetUtcNow(),
                    null,
                    null,
                    run.CorrelationId.Value,
                    Guid.NewGuid().ToByteArray());
                var savedEffect = await persistence.CreateEffectAsync(tenant, new CreateMigrationExecutionEffectCommand(effect), cancellationToken);
                if (!savedEffect.Succeeded)
                    return OwnerPreparationResult.Failure(savedEffect.Code);
            }

            var effects = (await persistence.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken))
                .Where(item => item.RecordType == type)
                .OrderBy(item => item.SourceSequence)
                .ToArray();
            foreach (var row in group)
            {
                var checks = await references.ValidateAsync(requestContext, row.Parsed, cancellationToken);
                var failedCheck = checks.FirstOrDefault(item => item.State is not (MigrationReferenceState.NotApplicable or MigrationReferenceState.Active));
                if (failedCheck is not null)
                    return OwnerPreparationResult.Failure(failedCheck.Code);

                if (row.Preview.PlannedAction is MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip)
                {
                    var effect = effects.First(item => item.StagedRecordId == row.Staged.StagedRecordId);
                    if (effect.Disposition != MigrationExecutionEffectDisposition.NonEffect)
                    {
                        var marked = await persistence.UpdateEffectAsync(
                            tenant,
                            new UpdateMigrationExecutionEffectCommand(
                                effect.Id,
                                MigrationExecutionEffectDisposition.NonEffect,
                                null,
                                null,
                                null,
                                "matched_reference",
                                null,
                                timeProvider.GetUtcNow(),
                                effect.Version),
                            cancellationToken);
                        if (!marked.Succeeded)
                            return OwnerPreparationResult.Failure(marked.Code);
                    }
                }
            }

            var ownerRows = group
                .Where(item => item.Preview.PlannedAction == MigrationPlannedAction.Create)
                .Select(item => new OwnerImportRowInput(item.Staged.SourceSequence, OwnerFields(item.Parsed)))
                .ToArray();
            if (ownerRows.Length == 0)
                continue;

            var ownerRequest = new OwnerImportRequest(
                ownerBatchId,
                ToOwnerKind(type),
                new OwnerImportSource("migration", null, $"{attempt.RunId:D}:{attempt.AttemptId:D}:{type}"),
                $"migration:{attempt.AttemptId:D}:{type}",
                groupFingerprint,
                ownerRows);
            var ownerEvidence = await owner.ReadEvidenceAsync(requestContext, ownerBatchId, cancellationToken);
            if (ownerEvidence is null)
            {
                var created = await owner.CreateBatchAsync(requestContext, ownerRequest, cancellationToken);
                if (!created.Succeeded)
                    return OwnerPreparationResult.Failure(created.Code);
                ownerEvidence = await owner.ReadEvidenceAsync(requestContext, ownerBatchId, cancellationToken);
            }

            if (ownerEvidence is null)
                return OwnerPreparationResult.Failure("migration_owner_evidence_unavailable");
            if (ownerEvidence.Batch.Status == OwnerBatchStatus.Draft)
            {
                var simulated = await owner.SimulateAsync(requestContext, ownerBatchId, cancellationToken);
                if (!simulated.Succeeded)
                    return OwnerPreparationResult.Failure(simulated.Code);
                ownerEvidence = await owner.ReadEvidenceAsync(requestContext, ownerBatchId, cancellationToken);
            }

            if (ownerEvidence is null)
                return OwnerPreparationResult.Failure("migration_owner_evidence_unavailable");
            if (ownerEvidence.Batch.Status is not (OwnerBatchStatus.Validated or OwnerBatchStatus.Completed or OwnerBatchStatus.CompletedWithErrors))
                return OwnerPreparationResult.Failure("migration_owner_evidence_unavailable");
        }

        return OwnerPreparationResult.Successful();
    }

    internal async Task MarkPreparationFailedAsync(
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        string code,
        CancellationToken cancellationToken)
    {
        var effects = await persistence.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken);
        foreach (var effect in effects.Where(item => item.RecordType != MigrationCanonicalRecordType.InventoryOpening && item.Disposition == MigrationExecutionEffectDisposition.Prepared))
        {
            await persistence.UpdateEffectAsync(
                tenant,
                new UpdateMigrationExecutionEffectCommand(
                    effect.Id,
                    MigrationExecutionEffectDisposition.Failed,
                    null,
                    null,
                    null,
                    code,
                    null,
                    timeProvider.GetUtcNow(),
                    effect.Version),
                cancellationToken);
        }

        var batches = await persistence.ListBatchesAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken);
        foreach (var batch in batches.Where(item => item.RecordType != MigrationCanonicalRecordType.InventoryOpening && item.State == MigrationExecutionBatchState.Prepared))
        {
            await persistence.UpdateBatchAsync(
                tenant,
                new UpdateMigrationExecutionBatchCommand(
                    batch.Id,
                    MigrationExecutionBatchState.Failed,
                    null,
                    timeProvider.GetUtcNow(),
                    batch.Version),
                cancellationToken);
        }
    }

    internal async Task<OwnerGroupResult> ExecuteAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        MigrationCanonicalRecordType type,
        IReadOnlyList<MigrationExecutionPlanRow> plan,
        CancellationToken cancellationToken)
    {
        var batch = await persistence.FindBatchAsync(tenant, attempt.RunId, attempt.AttemptId, type, cancellationToken);
        if (batch is null)
            return OwnerGroupResult.Failure("migration_execution_batch_not_found", unknown: true);
        var effects = (await persistence.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken))
            .Where(item => item.RecordType == type)
            .OrderBy(item => item.SourceSequence)
            .ToArray();
        if (batch.State == MigrationExecutionBatchState.Completed && effects.All(item => item.Disposition is MigrationExecutionEffectDisposition.NonEffect or MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.Failed))
            return OwnerGroupResult.Successful();
        if (batch.State is MigrationExecutionBatchState.Failed or MigrationExecutionBatchState.Unknown)
            return OwnerGroupResult.Failure(batch.State == MigrationExecutionBatchState.Unknown ? "migration_execution_outcome_unknown" : "migration_execution_group_failed", batch.State == MigrationExecutionBatchState.Unknown);

        if (plan.All(item => item.Preview.PlannedAction is MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip))
        {
            if (effects.Any(item => item.Disposition != MigrationExecutionEffectDisposition.NonEffect))
                return OwnerGroupResult.Failure("migration_execution_lineage_conflict", unknown: true);
            var completed = await persistence.UpdateBatchAsync(
                tenant,
                new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Completed, null, timeProvider.GetUtcNow(), batch.Version),
                cancellationToken);
            return completed.Succeeded ? OwnerGroupResult.Successful() : OwnerGroupResult.Failure(completed.Code, completed.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        }

        var ownerEvidence = await owner.ReadEvidenceAsync(requestContext, batch.OwnerBatchId, cancellationToken);
        if (ownerEvidence is null)
            return OwnerGroupResult.Failure("migration_owner_evidence_unavailable", unknown: true);
        if (ownerEvidence.Batch.Status is OwnerBatchStatus.Completed or OwnerBatchStatus.CompletedWithErrors)
            return await ReconcileAsync(tenant, batch, effects, ownerEvidence, cancellationToken);
        if (ownerEvidence.Batch.Status != OwnerBatchStatus.Validated)
            return OwnerGroupResult.Failure("migration_owner_evidence_unavailable", unknown: true);

        var started = await persistence.UpdateBatchAsync(
            tenant,
            new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Started, timeProvider.GetUtcNow(), null, batch.Version),
            cancellationToken);
        if (!started.Succeeded || started.Value is not { } startedBatch)
            return OwnerGroupResult.Failure(started.Code, started.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        var startedEffects = new List<MigrationExecutionEffectRecord>();
        foreach (var effect in effects.Where(item => item.Disposition == MigrationExecutionEffectDisposition.Prepared))
        {
            var marked = await persistence.UpdateEffectAsync(
                tenant,
                new UpdateMigrationExecutionEffectCommand(
                    effect.Id,
                    MigrationExecutionEffectDisposition.Started,
                    null,
                    null,
                    null,
                    null,
                    timeProvider.GetUtcNow(),
                    null,
                    effect.Version),
                cancellationToken);
            if (!marked.Succeeded)
                return OwnerGroupResult.Failure(marked.Code, marked.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
            startedEffects.Add(marked.Value ?? effect);
        }

        var executed = await owner.ExecuteAsync(requestContext, batch.OwnerBatchId, ownerEvidence.Batch.Version, cancellationToken);
        var after = await owner.ReadEvidenceAsync(requestContext, batch.OwnerBatchId, cancellationToken);
        if (after is null)
            return await MarkUnknownAsync(
                tenant,
                startedBatch,
                effects.Select(effect => startedEffects.FirstOrDefault(startedEffect => startedEffect.Id == effect.Id) ?? effect).ToArray(),
                cancellationToken);
        return await ReconcileAsync(
            tenant,
            startedBatch,
            effects.Select(effect => startedEffects.FirstOrDefault(startedEffect => startedEffect.Id == effect.Id) ?? effect).ToArray(),
            after,
            cancellationToken,
            executed.Succeeded ? null : executed.Code);
    }

    private async Task<OwnerGroupResult> MarkUnknownAsync(
        TenantContext tenant,
        MigrationExecutionBatchRecord batch,
        IReadOnlyList<MigrationExecutionEffectRecord> effects,
        CancellationToken cancellationToken)
    {
        foreach (var effect in effects.Where(item => item.Disposition is MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Prepared))
        {
            var updated = await persistence.UpdateEffectAsync(
                tenant,
                new UpdateMigrationExecutionEffectCommand(
                    effect.Id,
                    MigrationExecutionEffectDisposition.Unknown,
                    null,
                    null,
                    null,
                    "migration_execution_outcome_unknown",
                    effect.EffectStartedAt,
                    timeProvider.GetUtcNow(),
                    effect.Version),
                cancellationToken);
            if (!updated.Succeeded)
                return OwnerGroupResult.Failure(updated.Code, updated.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        }

        var current = await persistence.FindBatchAsync(tenant, batch.RunId, batch.AttemptId, batch.RecordType, cancellationToken) ?? batch;
        var marked = await persistence.UpdateBatchAsync(
            tenant,
            new UpdateMigrationExecutionBatchCommand(current.Id, MigrationExecutionBatchState.Unknown, current.StartedAt, timeProvider.GetUtcNow(), current.Version),
            cancellationToken);
        return marked.Succeeded
            ? OwnerGroupResult.Failure("migration_execution_outcome_unknown", unknown: true)
            : OwnerGroupResult.Failure(marked.Code, marked.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
    }

    private async Task<OwnerGroupResult> ReconcileAsync(
        TenantContext tenant,
        MigrationExecutionBatchRecord batch,
        IReadOnlyList<MigrationExecutionEffectRecord> effects,
        OwnerEvidence evidence,
        CancellationToken cancellationToken,
        string? ownerFailureCode = null)
    {
        if (evidence.Batch.Status is not (OwnerBatchStatus.Completed or OwnerBatchStatus.CompletedWithErrors))
            return OwnerGroupResult.Failure(ownerFailureCode ?? "migration_owner_execution_in_progress", unknown: ownerFailureCode is not null);

        var ownerRows = evidence.Rows.ToDictionary(item => item.OriginalRowNumber);
        var failed = false;
        foreach (var effect in effects.Where(item => item.Disposition is MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Prepared))
        {
            if (!ownerRows.TryGetValue(effect.SourceSequence, out var row))
                return OwnerGroupResult.Failure("migration_owner_effect_unproven", unknown: true);
            var disposition = row.MutationDisposition is OwnerMutationDisposition.Committed or OwnerMutationDisposition.Updated
                ? MigrationExecutionEffectDisposition.Committed
                : row.Outcome is OwnerRowOutcome.Rejected or OwnerRowOutcome.Quarantined
                    || row.MutationDisposition == OwnerMutationDisposition.Failed
                    ? MigrationExecutionEffectDisposition.Failed
                    : MigrationExecutionEffectDisposition.Unknown;
            if (disposition == MigrationExecutionEffectDisposition.Unknown)
                return OwnerGroupResult.Failure("migration_owner_effect_unproven", unknown: true);
            failed |= disposition == MigrationExecutionEffectDisposition.Failed;
            var updated = await persistence.UpdateEffectAsync(
                tenant,
                new UpdateMigrationExecutionEffectCommand(
                    effect.Id,
                    disposition,
                    row.Id,
                    row.ResultingResourceId,
                    row.ResultingResourceCode,
                    row.Diagnostics.FirstOrDefault()?.Code,
                    effect.EffectStartedAt,
                    timeProvider.GetUtcNow(),
                    effect.Version),
                cancellationToken);
            if (!updated.Succeeded)
                return OwnerGroupResult.Failure(updated.Code, updated.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        }

        var current = await persistence.FindBatchAsync(tenant, batch.RunId, batch.AttemptId, batch.RecordType, cancellationToken) ?? batch;
        var completed = await persistence.UpdateBatchAsync(
            tenant,
            new UpdateMigrationExecutionBatchCommand(
                current.Id,
                failed ? MigrationExecutionBatchState.Failed : MigrationExecutionBatchState.Completed,
                current.StartedAt,
                timeProvider.GetUtcNow(),
                current.Version),
            cancellationToken);
        return completed.Succeeded
            ? failed ? OwnerGroupResult.Failure("migration_execution_group_failed", unknown: false) : OwnerGroupResult.Successful()
            : OwnerGroupResult.Failure(completed.Code, completed.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
    }

    private static OwnerResourceKind ToOwnerKind(MigrationCanonicalRecordType type) => type switch
    {
        MigrationCanonicalRecordType.Product => OwnerResourceKind.Product,
        MigrationCanonicalRecordType.Supplier => OwnerResourceKind.Supplier,
        MigrationCanonicalRecordType.Customer => OwnerResourceKind.Customer,
        MigrationCanonicalRecordType.Currency => OwnerResourceKind.Currency,
        MigrationCanonicalRecordType.Tax => OwnerResourceKind.Tax,
        MigrationCanonicalRecordType.PaymentTerm => OwnerResourceKind.PaymentTerm,
        MigrationCanonicalRecordType.UnitOfMeasure => OwnerResourceKind.UnitOfMeasure,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static IReadOnlyDictionary<string, string?> OwnerFields(MigrationParsedCanonicalRow row) => row.Payload switch
    {
        MigrationProductPayload product => Fields(
            ("sku", product.Sku),
            ("englishName", product.NameEnglish),
            ("arabicName", product.NameArabic),
            ("categoryId", product.CategoryId?.ToString("D")),
            ("baseUnitOfMeasureId", product.BaseUnitOfMeasureId?.ToString("D"))),
        MigrationSupplierPayload supplier => Fields(
            ("code", supplier.Code),
            ("legalNameEnglish", supplier.NameEnglish),
            ("legalNameArabic", supplier.NameArabic)),
        MigrationCustomerPayload customer => Fields(
            ("code", customer.Code),
            ("legalNameEnglish", customer.NameEnglish),
            ("legalNameArabic", customer.NameArabic)),
        _ => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    };

    private static IReadOnlyDictionary<string, string?> Fields(params (string Key, string? Value)[] values) =>
        values.Where(item => item.Value is not null).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);

    internal sealed record OwnerPreparationResult(bool Succeeded, string Code)
    {
        internal static OwnerPreparationResult Successful() => new(true, "owner_prepared");
        internal static OwnerPreparationResult Failure(string code) => new(false, code);
    }

    internal sealed record OwnerGroupResult(bool Succeeded, string Code, bool Unknown)
    {
        internal static OwnerGroupResult Successful() => new(true, "owner_group_completed", false);
        internal static OwnerGroupResult Failure(string code, bool unknown) => new(false, code, unknown);
    }
}
