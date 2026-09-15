#pragma warning disable CS1591

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

/// <summary>
/// Executes the approved, validated and dry-run-proven MESP-141 master-data
/// reference package through the owning Master Data import engine.
/// </summary>
public sealed class MigrationExecutionService
{
    public const string OperationId = "migration.execution.start";
    public const string FingerprintVersion = "migration-master-execution-v1";

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

    private readonly MigrationFoundationService foundation;
    private readonly IMigrationFoundationPersistence foundationPersistence;
    private readonly IMigrationValidationPersistence validationPersistence;
    private readonly IMigrationExecutionPersistence executionPersistence;
    private readonly ICurrentOrganizationScopeResolver scopeResolver;
    private readonly IOrganizationScopeOwnershipResolver scopeOwnership;
    private readonly IOwnerExecutionGateway owner;
    private readonly TimeProvider timeProvider;
    // ponytail: one process-wide execution gate; durable row claims protect
    // cross-process calls, while per-Tenant gates can be added if throughput
    // requires it.
    private readonly SemaphoreSlim executionGate = new(1, 1);

    public MigrationExecutionService(
        MigrationFoundationService foundation,
        IMigrationFoundationPersistence foundationPersistence,
        IMigrationValidationPersistence validationPersistence,
        IMigrationExecutionPersistence executionPersistence,
        ICurrentOrganizationScopeResolver scopeResolver,
        IOrganizationScopeOwnershipResolver scopeOwnership,
        IOwnerExecutionGateway owner,
        TimeProvider? timeProvider = null)
    {
        this.foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
        this.foundationPersistence = foundationPersistence ?? throw new ArgumentNullException(nameof(foundationPersistence));
        this.validationPersistence = validationPersistence ?? throw new ArgumentNullException(nameof(validationPersistence));
        this.executionPersistence = executionPersistence ?? throw new ArgumentNullException(nameof(executionPersistence));
        this.scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        this.scopeOwnership = scopeOwnership ?? throw new ArgumentNullException(nameof(scopeOwnership));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MigrationOperationResult<MigrationExecutionResult>> ExecuteAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        byte[] expectedRunVersion,
        CancellationToken cancellationToken = default)
    {
        await executionGate.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteCoreAsync(requestContext, runId, idempotencyKey, expectedRunVersion, cancellationToken);
        }
        finally
        {
            executionGate.Release();
        }
    }

    public async Task<MigrationExecutionResult?> ReadAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (requestContext.TenantContext is not { } tenant || runId == Guid.Empty)
            return null;
        var run = await foundationPersistence.FindRunAsync(tenant, runId, cancellationToken);
        var intake = await validationPersistence.FindIntakeAsync(tenant, runId, cancellationToken);
        if (run is null || intake is null || !IsCurrentScopeAuthorized(tenant, intake.Source))
            return null;
        var attempts = await foundationPersistence.ListAttemptsAsync(tenant, runId, cancellationToken);
        var attempt = attempts.LastOrDefault(item => item.Operation == MigrationOperationKind.Execution);
        if (attempt is null)
            return null;
        var fingerprint = attempt.RequestFingerprint;
        return await BuildResultAsync(run, attempt, fingerprint, attempt.SafeOutcomeCode ?? "execution_in_progress", tenant, cancellationToken);
    }

    private async Task<MigrationOperationResult<MigrationExecutionResult>> ExecuteCoreAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        byte[] expectedRunVersion,
        CancellationToken cancellationToken)
    {
        if (requestContext.TenantContext is not { } tenant)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_tenant_context_required");
        if (runId == Guid.Empty || expectedRunVersion is null || expectedRunVersion.Length == 0)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_request_invalid");

        var run = await foundationPersistence.FindRunAsync(tenant, runId, cancellationToken);
        var intake = await validationPersistence.FindIntakeAsync(tenant, runId, cancellationToken);
        if (run is null || intake is null)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_run_not_found");
        if (!run.EvidenceConfirmed)
            return MigrationOperationResult<MigrationExecutionResult>.Unknown("migration_audit_recovery_required");
        if (!IsCurrentScopeAuthorized(tenant, intake.Source))
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_source_scope_denied");

        var validation = await validationPersistence.FindLatestValidationAsync(tenant, runId, cancellationToken);
        var dryRun = await validationPersistence.FindLatestDryRunAsync(tenant, runId, cancellationToken);
        if (validation is null || !validation.IsValid)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_validation_required");
        if (dryRun is null)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_dry_run_required");

        var staged = await validationPersistence.ListStagedRecordsAsync(tenant, runId, 0, int.MaxValue, cancellationToken);
        var gate = BuildPlan(run, intake, validation, dryRun, staged, tenant);
        if (!gate.Succeeded || gate.Plan is null)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected(gate.Code);

        var fingerprint = ComputeFingerprint(run, intake, validation, dryRun, gate.Scope!);
        var existing = await foundationPersistence.FindIdempotencyAsync(
            tenant,
            MigrationOperationKind.Execution,
            idempotencyKey,
            cancellationToken);
        if (existing is not null && !string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_idempotency_conflict");
        if (existing is null && run.Status != MigrationRunStatus.Approved)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_approval_required");
        if (existing is null && !run.Version.AsSpan().SequenceEqual(expectedRunVersion))
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_run_version_conflict");

        var started = await foundation.StartAttemptAsync(
            requestContext,
            runId,
            MigrationOperationKind.Execution,
            idempotencyKey,
            fingerprint,
            cancellationToken);
        if (!started.Succeeded || started.Value is not { } attempt)
            return Map(started);

        if (attempt.Outcome != MigrationAttemptOutcome.Pending)
        {
            var replayRun = await foundationPersistence.FindRunAsync(tenant, runId, cancellationToken) ?? run;
            if (attempt.Outcome == MigrationAttemptOutcome.UnknownOutcome)
            {
                var recovered = await RecoverUnknownAsync(requestContext, tenant, attempt, cancellationToken);
                if (recovered)
                {
                    var recoveredResult = await BuildResultAsync(
                            replayRun,
                            attempt,
                            fingerprint,
                            "migration_execution_recovered_owner_evidence",
                            tenant,
                            cancellationToken);
                    return new MigrationOperationResult<MigrationExecutionResult>(
                        MigrationResultKind.Replayed,
                        "migration_execution_recovered_owner_evidence",
                        recoveredResult,
                        false);
                }
            }
            var replay = await BuildResultAsync(replayRun, attempt, fingerprint, attempt.SafeOutcomeCode ?? "execution_completed", tenant, cancellationToken);
            return started.Kind == MigrationResultKind.Replayed
                ? MigrationOperationResult<MigrationExecutionResult>.Replay(replay)
                : MigrationOperationResult<MigrationExecutionResult>.Success(replay);
        }

        var prepared = await PrepareLineageAsync(tenant, run, attempt, gate.Plan, cancellationToken);
        if (!prepared.Succeeded)
            return await FinishWithoutOwnerEffectAsync(requestContext, tenant, run, attempt, prepared.Code, prepared.Unknown, cancellationToken);

        var currentRun = await foundationPersistence.FindRunAsync(tenant, runId, cancellationToken) ?? run;
        if (currentRun.Status == MigrationRunStatus.Approved)
        {
            var executing = await foundation.TransitionRunAsync(
                requestContext,
                runId,
                MigrationRunStatus.Executing,
                currentRun.Version,
                cancellationToken);
            if (!executing.Succeeded || executing.Value is not { } transitioned)
                return MapExecutionFailure(executing);
            currentRun = transitioned;
        }
        else if (currentRun.Status != MigrationRunStatus.Executing)
        {
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_state_invalid");
        }

        foreach (var group in gate.Plan.GroupBy(item => item.Preview.RecordType).OrderBy(item => Array.IndexOf(ExecutionOrder, item.Key)))
        {
            var processed = await ExecuteGroupAsync(requestContext, tenant, attempt, group.Key, group.ToArray(), cancellationToken);
            if (!processed.Succeeded)
            {
                if (processed.Code == "migration_execution_batch_claim_conflict")
                    return await FinishWithoutOwnerEffectAsync(requestContext, tenant, currentRun, attempt, processed.Code, false, cancellationToken);
                var effects = await executionPersistence.ListEffectsAsync(tenant, runId, attempt.AttemptId, cancellationToken);
                if (processed.Unknown || effects.Any(item => item.Disposition is MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Unknown))
                    return await FinishAsync(requestContext, tenant, currentRun, attempt, MigrationAttemptOutcome.UnknownOutcome, processed.Code, cancellationToken);
                return await FinishAsync(
                    requestContext,
                    tenant,
                    currentRun,
                    attempt,
                    MigrationAttemptOutcome.KnownFailure,
                    effects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Committed)
                        ? "migration_execution_partially_completed"
                        : processed.Code,
                    cancellationToken);
            }
        }

        var finalEffects = await executionPersistence.ListEffectsAsync(tenant, runId, attempt.AttemptId, cancellationToken);
        var outcome = finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)
            ? MigrationAttemptOutcome.UnknownOutcome
            : finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Failed)
                ? MigrationAttemptOutcome.KnownFailure
                : MigrationAttemptOutcome.Succeeded;
        var code = outcome switch
        {
            MigrationAttemptOutcome.Succeeded => "migration_execution_completed",
            MigrationAttemptOutcome.KnownFailure when finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Committed) => "migration_execution_partially_completed",
            MigrationAttemptOutcome.KnownFailure => "migration_execution_failed",
            _ => "migration_execution_outcome_unknown"
        };
        return await FinishAsync(requestContext, tenant, currentRun, attempt, outcome, code, cancellationToken);
    }

    private async Task<LineageResult> PrepareLineageAsync(
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
            var savedBatch = await executionPersistence.CreateBatchAsync(tenant, new CreateMigrationExecutionBatchCommand(batch), cancellationToken);
            if (!savedBatch.Succeeded)
                return LineageResult.Failure(savedBatch.Code, savedBatch.Outcome == MigrationPersistenceOutcome.UnknownOutcome);

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
                    row.Preview.PlannedAction is MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip
                        ? MigrationExecutionEffectDisposition.NonEffect
                        : MigrationExecutionEffectDisposition.Prepared,
                    row.Preview.PlannedAction is MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip
                        ? "matched_reference"
                        : null,
                    timeProvider.GetUtcNow(),
                    null,
                    row.Preview.PlannedAction is MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip ? timeProvider.GetUtcNow() : null,
                    run.CorrelationId.Value,
                    Guid.NewGuid().ToByteArray());
                var savedEffect = await executionPersistence.CreateEffectAsync(tenant, new CreateMigrationExecutionEffectCommand(effect), cancellationToken);
                if (!savedEffect.Succeeded)
                    return LineageResult.Failure(savedEffect.Code, savedEffect.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
            }
        }
        return LineageResult.Successful();
    }

    private async Task<GroupResult> ExecuteGroupAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        MigrationCanonicalRecordType type,
        IReadOnlyList<MigrationExecutionPlanRow> plan,
        CancellationToken cancellationToken)
    {
        var batch = await executionPersistence.FindBatchAsync(tenant, attempt.RunId, attempt.AttemptId, type, cancellationToken);
        if (batch is null)
            return GroupResult.Failure("migration_execution_batch_not_found", unknown: true);
        var effects = (await executionPersistence.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken))
            .Where(item => item.RecordType == type)
            .OrderBy(item => item.SourceSequence)
            .ToArray();
        if (batch.State == MigrationExecutionBatchState.Completed && effects.All(item => item.Disposition is MigrationExecutionEffectDisposition.NonEffect or MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.Failed))
            return GroupResult.Successful();
        if (batch.State is MigrationExecutionBatchState.Failed or MigrationExecutionBatchState.Unknown)
            return GroupResult.Failure(batch.State == MigrationExecutionBatchState.Unknown ? "migration_execution_outcome_unknown" : "migration_execution_group_failed", batch.State == MigrationExecutionBatchState.Unknown);

        if (plan.All(item => item.Preview.PlannedAction is MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip))
        {
            if (effects.Any(item => item.Disposition != MigrationExecutionEffectDisposition.NonEffect))
                return GroupResult.Failure("migration_execution_lineage_conflict", unknown: true);
            var completed = await executionPersistence.UpdateBatchAsync(
                tenant,
                new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Completed, null, timeProvider.GetUtcNow(), batch.Version),
                cancellationToken);
            return completed.Succeeded ? GroupResult.Successful() : GroupResult.Failure(completed.Code, completed.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        }

        var ownerRows = plan
            .Where(item => item.Preview.PlannedAction == MigrationPlannedAction.Create)
            .Select(item => new OwnerImportRowInput(item.Staged.SourceSequence, OwnerFields(item.Parsed)))
            .ToArray();
        var ownerRequest = new OwnerImportRequest(
            batch.OwnerBatchId,
            ToOwnerKind(type),
            new OwnerImportSource("migration", null, $"{attempt.RunId:D}:{attempt.AttemptId:D}:{type}"),
            $"migration:{attempt.AttemptId:D}:{type}",
            batch.Fingerprint,
            ownerRows);

        var ownerEvidence = await owner.ReadEvidenceAsync(requestContext, batch.OwnerBatchId, cancellationToken);
        if (ownerEvidence is null)
        {
            var created = await owner.CreateBatchAsync(requestContext, ownerRequest, cancellationToken);
            if (!created.Succeeded)
                return await FailGroupAsync(tenant, batch, effects, created.Code, unknown: false, cancellationToken);
            ownerEvidence = await owner.ReadEvidenceAsync(requestContext, batch.OwnerBatchId, cancellationToken);
        }

        if (ownerEvidence is { Batch.Status: OwnerBatchStatus.Completed or OwnerBatchStatus.CompletedWithErrors })
            return await ReconcileOwnerEvidenceAsync(tenant, batch, effects, ownerEvidence, cancellationToken);

        if (ownerEvidence is null || ownerEvidence.Batch.Status == OwnerBatchStatus.Draft)
        {
            var simulated = await owner.SimulateAsync(requestContext, batch.OwnerBatchId, cancellationToken);
            if (!simulated.Succeeded)
                return await FailGroupAsync(tenant, batch, effects, simulated.Code, unknown: false, cancellationToken);
            ownerEvidence = await owner.ReadEvidenceAsync(requestContext, batch.OwnerBatchId, cancellationToken);
        }

        if (ownerEvidence is null || ownerEvidence.Batch.Status != OwnerBatchStatus.Validated)
            return await FailGroupAsync(tenant, batch, effects, "migration_owner_evidence_unavailable", unknown: true, cancellationToken);

        var started = await executionPersistence.UpdateBatchAsync(
            tenant,
            new UpdateMigrationExecutionBatchCommand(batch.Id, MigrationExecutionBatchState.Started, timeProvider.GetUtcNow(), null, batch.Version),
            cancellationToken);
        if (!started.Succeeded || started.Value is not { } startedBatch)
            return GroupResult.Failure(started.Code, started.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        var startedEffects = new List<MigrationExecutionEffectRecord>();
        foreach (var effect in effects.Where(item => item.Disposition == MigrationExecutionEffectDisposition.Prepared))
        {
            var marked = await executionPersistence.UpdateEffectAsync(
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
                return GroupResult.Failure(marked.Code, marked.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
            startedEffects.Add(marked.Value ?? effect);
        }

        var executed = await owner.ExecuteAsync(requestContext, batch.OwnerBatchId, ownerEvidence.Batch.Version, cancellationToken);
        var after = await owner.ReadEvidenceAsync(requestContext, batch.OwnerBatchId, cancellationToken);
        if (after is null)
            return GroupResult.Failure(executed.Succeeded ? "migration_owner_evidence_unavailable" : executed.Code, unknown: true);
        return await ReconcileOwnerEvidenceAsync(tenant, startedBatch, effects.Select(effect =>
            startedEffects.FirstOrDefault(startedEffect => startedEffect.Id == effect.Id) ?? effect).ToArray(), after, cancellationToken, executed.Succeeded ? null : executed.Code);
    }

    private async Task<bool> RecoverUnknownAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        CancellationToken cancellationToken)
    {
        var batches = await executionPersistence.ListBatchesAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken);
        var effects = await executionPersistence.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken);
        foreach (var batch in batches.OrderBy(item => Array.IndexOf(ExecutionOrder, item.RecordType)))
        {
            var batchEffects = effects.Where(item => item.RecordType == batch.RecordType).ToArray();
            if (batchEffects.All(item => item.Disposition == MigrationExecutionEffectDisposition.NonEffect))
                continue;
            if (batchEffects.All(item => item.Disposition == MigrationExecutionEffectDisposition.Committed))
                continue;

            var evidence = await owner.ReadEvidenceAsync(requestContext, batch.OwnerBatchId, cancellationToken);
            if (evidence is null)
                return false;
            var reconciled = await ReconcileOwnerEvidenceAsync(tenant, batch, batchEffects, evidence, cancellationToken);
            if (!reconciled.Succeeded)
                return false;
        }

        return (await executionPersistence.ListEffectsAsync(tenant, attempt.RunId, attempt.AttemptId, cancellationToken))
            .All(item => item.Disposition is MigrationExecutionEffectDisposition.NonEffect or MigrationExecutionEffectDisposition.Committed);
    }

    private async Task<GroupResult> ReconcileOwnerEvidenceAsync(
        TenantContext tenant,
        MigrationExecutionBatchRecord batch,
        IReadOnlyList<MigrationExecutionEffectRecord> effects,
        OwnerEvidence evidence,
        CancellationToken cancellationToken,
        string? ownerFailureCode = null)
    {
        if (evidence.Batch.Status is not (OwnerBatchStatus.Completed or OwnerBatchStatus.CompletedWithErrors))
            return GroupResult.Failure(ownerFailureCode ?? "migration_owner_execution_in_progress", unknown: ownerFailureCode is not null);

        var ownerRows = evidence.Rows.ToDictionary(item => item.OriginalRowNumber);
        var failed = false;
        foreach (var effect in effects.Where(item => item.Disposition is MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Prepared))
        {
            if (!ownerRows.TryGetValue(effect.SourceSequence, out var row))
                return GroupResult.Failure("migration_owner_effect_unproven", unknown: true);
            var disposition = row.MutationDisposition is OwnerMutationDisposition.Committed or OwnerMutationDisposition.Updated
                ? MigrationExecutionEffectDisposition.Committed
                : row.Outcome is OwnerRowOutcome.Rejected or OwnerRowOutcome.Quarantined
                    || row.MutationDisposition == OwnerMutationDisposition.Failed
                    ? MigrationExecutionEffectDisposition.Failed
                    : MigrationExecutionEffectDisposition.Unknown;
            if (disposition == MigrationExecutionEffectDisposition.Unknown)
                return GroupResult.Failure("migration_owner_effect_unproven", unknown: true);
            failed |= disposition == MigrationExecutionEffectDisposition.Failed;
            var updated = await executionPersistence.UpdateEffectAsync(
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
                return GroupResult.Failure(updated.Code, updated.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        }

        var current = await executionPersistence.FindBatchAsync(tenant, batch.RunId, batch.AttemptId, batch.RecordType, cancellationToken) ?? batch;
        var completed = await executionPersistence.UpdateBatchAsync(
            tenant,
            new UpdateMigrationExecutionBatchCommand(
                current.Id,
                failed ? MigrationExecutionBatchState.Failed : MigrationExecutionBatchState.Completed,
                current.StartedAt,
                timeProvider.GetUtcNow(),
                current.Version),
            cancellationToken);
        return completed.Succeeded
            ? failed ? GroupResult.Failure("migration_execution_group_failed", unknown: false) : GroupResult.Successful()
            : GroupResult.Failure(completed.Code, completed.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
    }

    private async Task<GroupResult> FailGroupAsync(
        TenantContext tenant,
        MigrationExecutionBatchRecord batch,
        IReadOnlyList<MigrationExecutionEffectRecord> effects,
        string code,
        bool unknown,
        CancellationToken cancellationToken)
    {
        foreach (var effect in effects.Where(item => item.Disposition is MigrationExecutionEffectDisposition.Prepared or MigrationExecutionEffectDisposition.Started))
        {
            var updated = await executionPersistence.UpdateEffectAsync(
                tenant,
                new UpdateMigrationExecutionEffectCommand(
                    effect.Id,
                    unknown ? MigrationExecutionEffectDisposition.Unknown : MigrationExecutionEffectDisposition.Failed,
                    null,
                    null,
                    null,
                    code,
                    effect.EffectStartedAt,
                    timeProvider.GetUtcNow(),
                    effect.Version),
                cancellationToken);
            if (!updated.Succeeded)
                return GroupResult.Failure(updated.Code, updated.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
        }
        var current = await executionPersistence.FindBatchAsync(tenant, batch.RunId, batch.AttemptId, batch.RecordType, cancellationToken) ?? batch;
        var updatedBatch = await executionPersistence.UpdateBatchAsync(
            tenant,
            new UpdateMigrationExecutionBatchCommand(
                current.Id,
                unknown ? MigrationExecutionBatchState.Unknown : MigrationExecutionBatchState.Failed,
                current.StartedAt,
                timeProvider.GetUtcNow(),
                current.Version),
            cancellationToken);
        return updatedBatch.Succeeded ? GroupResult.Failure(code, unknown) : GroupResult.Failure(updatedBatch.Code, updatedBatch.Outcome == MigrationPersistenceOutcome.UnknownOutcome);
    }

    private async Task<MigrationOperationResult<MigrationExecutionResult>> FinishAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        MigrationAttemptOutcome outcome,
        string code,
        CancellationToken cancellationToken)
    {
        var recorded = await foundation.RecordAttemptOutcomeAsync(
            requestContext,
            run.RunId,
            attempt.AttemptId,
            outcome,
            code,
            attempt.Version,
            cancellationToken);
        if (!recorded.Succeeded || recorded.Value is not { } completedAttempt)
            return MapExecutionFailure(recorded);

        var target = outcome switch
        {
            MigrationAttemptOutcome.Succeeded => MigrationRunStatus.Completed,
            MigrationAttemptOutcome.KnownFailure => (await executionPersistence.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken)).Any(item => item.Disposition == MigrationExecutionEffectDisposition.Committed)
                ? MigrationRunStatus.PartiallyCompleted
                : MigrationRunStatus.Failed,
            _ => MigrationRunStatus.OutcomeUnknown
        };
        var current = await foundationPersistence.FindRunAsync(tenant, run.RunId, cancellationToken) ?? run;
        var transitioned = await foundation.TransitionRunAsync(requestContext, run.RunId, target, current.Version, cancellationToken);
        if (!transitioned.Succeeded || transitioned.Value is not { } completedRun)
            return MapExecutionFailure(transitioned);
        var result = await BuildResultAsync(completedRun, completedAttempt, attempt.RequestFingerprint, code, tenant, cancellationToken);
        return outcome == MigrationAttemptOutcome.Succeeded
            ? MigrationOperationResult<MigrationExecutionResult>.Success(result, code)
            : outcome == MigrationAttemptOutcome.KnownFailure
                ? MigrationOperationResult<MigrationExecutionResult>.Failure(code)
                : MigrationOperationResult<MigrationExecutionResult>.Unknown(code);
    }

    private async Task<MigrationOperationResult<MigrationExecutionResult>> FinishWithoutOwnerEffectAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        string code,
        bool unknown,
        CancellationToken cancellationToken)
    {
        var recorded = await foundation.RecordAttemptOutcomeAsync(
            requestContext,
            run.RunId,
            attempt.AttemptId,
            MigrationAttemptOutcome.KnownFailure,
            code,
            attempt.Version,
            cancellationToken);
        return !recorded.Succeeded
            ? MapExecutionFailure(recorded)
            : MigrationOperationResult<MigrationExecutionResult>.Failure(code);
    }

    private async Task<MigrationExecutionResult> BuildResultAsync(
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        string fingerprint,
        string code,
        TenantContext tenant,
        CancellationToken cancellationToken)
    {
        var batches = await executionPersistence.ListBatchesAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        var effects = await executionPersistence.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        return new MigrationExecutionResult(
            run.RunId,
            tenant.TenantId,
            attempt.AttemptId,
            FingerprintVersion,
            fingerprint,
            run.Status,
            attempt.Outcome,
            code,
            batches,
            effects);
    }

    private PlanGate BuildPlan(
        MigrationRunRecord run,
        MigrationIntakeRecord intake,
        MigrationValidationSummary validation,
        MigrationDryRunPreview dryRun,
        IReadOnlyList<MigrationStagedRecord> staged,
        TenantContext tenant)
    {
        if (!string.Equals(validation.PackageHash, dryRun.PackageHash, StringComparison.Ordinal)
            || !string.Equals(validation.SourceSnapshotHash, dryRun.SourceSnapshotHash, StringComparison.Ordinal)
            || !string.Equals(validation.SourceSnapshotHash, intake.Source.Sha256, StringComparison.OrdinalIgnoreCase)
            || validation.AttemptId != dryRun.ValidationAttemptId
            || staged.Count != validation.TotalStagedRecords
            || validation.Records.Count != staged.Count
            || dryRun.Rows.Count != staged.Count
            || staged.Any(item => item.TenantId != tenant.TenantId
                || item.RunId != run.RunId
                || !string.Equals(item.PackageHash, validation.PackageHash, StringComparison.Ordinal)
                || !string.Equals(item.SourceSnapshotHash, validation.SourceSnapshotHash, StringComparison.Ordinal)))
            return PlanGate.Failure("migration_execution_authoritative_snapshot_mismatch");

        var supported = staged.All(item => IsSupported(item.RecordType));
        if (!supported)
            return PlanGate.Failure("migration_execution_record_type_not_supported");
        if (validation.Records.Any(item => item.Disposition != MigrationRecordDisposition.Accepted)
            || dryRun.Rows.Any(item => item.Disposition != MigrationRecordDisposition.Accepted || item.PlannedAction == MigrationPlannedAction.Blocked))
            return PlanGate.Failure("migration_execution_blocking_row_present");

        if (validation.Records.GroupBy(item => item.StagedRecordId).Any(group => group.Count() != 1)
            || dryRun.Rows.GroupBy(item => item.StagedRecordId).Any(group => group.Count() != 1))
            return PlanGate.Failure("migration_execution_authoritative_snapshot_mismatch");

        var validationById = validation.Records.ToDictionary(item => item.StagedRecordId);
        var previewById = dryRun.Rows.ToDictionary(item => item.StagedRecordId);
        var plan = new List<MigrationExecutionPlanRow>(staged.Count);
        foreach (var record in staged.OrderBy(item => item.SourceSequence))
        {
            if (!validationById.ContainsKey(record.StagedRecordId) || !previewById.TryGetValue(record.StagedRecordId, out var preview))
                return PlanGate.Failure("migration_execution_authoritative_snapshot_mismatch");
            if (!TryParse(record, out var parsed))
                return PlanGate.Failure("migration_execution_payload_invalid");
            if (preview.PlannedAction == MigrationPlannedAction.Create && parsed!.Payload is not (MigrationProductPayload or MigrationSupplierPayload or MigrationCustomerPayload))
                return PlanGate.Failure("migration_execution_owner_action_invalid");
            if (preview.PlannedAction is not (MigrationPlannedAction.Create or MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip))
                return PlanGate.Failure("migration_execution_plan_invalid");
            plan.Add(new MigrationExecutionPlanRow(record, preview, parsed!));
        }

        var scope = scopeResolver.ResolveCurrent(tenant);
        return !scope.Allowed || scope.Scope is not { } resolved
            ? PlanGate.Failure("migration_source_scope_denied")
            : PlanGate.Success(plan, resolved);
    }

    private bool IsCurrentScopeAuthorized(TenantContext tenant, MigrationSourceArtifactSnapshot source)
    {
        if (source.TenantId != tenant.TenantId)
            return false;
        var authorized = scopeResolver.ResolveCurrent(tenant);
        var requested = ResolveRequestedScope(tenant, source.CompanyId, source.BranchId, source.WarehouseId);
        return authorized.Allowed
            && authorized.Scope is { } authorizedScope
            && requested.Allowed
            && requested.Scope is { } requestedScope
            && authorizedScope.ContainsAuthorizedDescendant(requestedScope);
    }

    private TenantWorkScopeResolution ResolveRequestedScope(TenantContext tenant, Guid? companyId, Guid? branchId, Guid? warehouseId)
    {
        try
        {
            return scopeOwnership.Resolve(tenant, new TenantWorkScopeRequest(companyId, branchId, warehouseId));
        }
        catch (ArgumentException)
        {
            return TenantWorkScopeResolution.Denied("scope_invalid");
        }
    }

    private static string ComputeFingerprint(
        MigrationRunRecord run,
        MigrationIntakeRecord intake,
        MigrationValidationSummary validation,
        MigrationDryRunPreview dryRun,
        TenantWorkScope scope) =>
        MigrationFingerprintEncoder.Compute(
            FingerprintVersion,
            run.RunId.ToString("D", CultureInfo.InvariantCulture),
            run.TenantId.Value.ToString("D", CultureInfo.InvariantCulture),
            run.Definition.DefinitionId,
            run.Definition.Version,
            run.SourceProfile.ProfileId,
            run.SourceProfile.ProfileVersion,
            intake.Source.Sha256,
            validation.ValidationResultId.ToString("D", CultureInfo.InvariantCulture),
            validation.PackageHash,
            dryRun.PreviewId.ToString("D", CultureInfo.InvariantCulture),
            dryRun.AttemptId.ToString("D", CultureInfo.InvariantCulture),
            dryRun.PackageHash,
            ScopeValue(scope),
            "master-data-owner-import-v1");

    private static string ScopeValue(TenantWorkScope scope) => scope.WarehouseId is { } warehouse
        ? $"Warehouse:{warehouse:D}"
        : scope.BranchId is { } branch
            ? $"Branch:{branch:D}"
            : scope.CompanyId is { } company
                ? $"Company:{company:D}"
                : $"Tenant:{scope.TenantId.Value:D}";

    private static bool IsSupported(MigrationCanonicalRecordType type) => type is
        MigrationCanonicalRecordType.Product or
        MigrationCanonicalRecordType.Supplier or
        MigrationCanonicalRecordType.Customer or
        MigrationCanonicalRecordType.Currency or
        MigrationCanonicalRecordType.Tax or
        MigrationCanonicalRecordType.PaymentTerm or
        MigrationCanonicalRecordType.UnitOfMeasure;

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

    private static bool TryParse(MigrationStagedRecord staged, out MigrationParsedCanonicalRow? parsed)
    {
        parsed = null;
        try
        {
            using var document = JsonDocument.Parse(staged.CanonicalPayload);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            MigrationCanonicalPayload? payload = staged.RecordType switch
            {
                MigrationCanonicalRecordType.Product => document.Deserialize<MigrationProductPayload>(options),
                MigrationCanonicalRecordType.Supplier => document.Deserialize<MigrationSupplierPayload>(options),
                MigrationCanonicalRecordType.Customer => document.Deserialize<MigrationCustomerPayload>(options),
                MigrationCanonicalRecordType.Currency or MigrationCanonicalRecordType.Tax or MigrationCanonicalRecordType.PaymentTerm or MigrationCanonicalRecordType.UnitOfMeasure => document.Deserialize<MigrationReferencePayload>(options),
                _ => null
            };
            parsed = payload is null ? null : new MigrationParsedCanonicalRow(staged.SourceSequence, staged.SourceRecordId, staged.RecordType, payload, staged.CanonicalPayload);
            return parsed is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);

    private static MigrationOperationResult<MigrationExecutionResult> Map<T>(MigrationOperationResult<T> result) => result.Kind switch
    {
        MigrationResultKind.UnknownOutcome => MigrationOperationResult<MigrationExecutionResult>.Unknown(result.Code),
        MigrationResultKind.KnownFailure => MigrationOperationResult<MigrationExecutionResult>.Failure(result.Code, result.IsSafeToRetry),
        _ => MigrationOperationResult<MigrationExecutionResult>.Rejected(result.Code)
    };

    private static MigrationOperationResult<MigrationExecutionResult> MapExecutionFailure<T>(MigrationOperationResult<T> result) => result.Kind == MigrationResultKind.UnknownOutcome
        ? MigrationOperationResult<MigrationExecutionResult>.Unknown(result.Code)
        : result.Kind == MigrationResultKind.KnownFailure
            ? MigrationOperationResult<MigrationExecutionResult>.Failure(result.Code, result.IsSafeToRetry)
            : MigrationOperationResult<MigrationExecutionResult>.Rejected(result.Code);

    private sealed record PlanGate(bool Succeeded, string Code, IReadOnlyList<MigrationExecutionPlanRow>? Plan, TenantWorkScope? Scope)
    {
        internal static PlanGate Success(IReadOnlyList<MigrationExecutionPlanRow> plan, TenantWorkScope scope) => new(true, "plan_ready", plan, scope);
        internal static PlanGate Failure(string code) => new(false, code, null, null);
    }

    private sealed record LineageResult(bool Succeeded, string Code, bool Unknown)
    {
        internal static LineageResult Successful() => new(true, "lineage_ready", false);
        internal static LineageResult Failure(string code, bool unknown) => new(false, code, unknown);
    }

    private sealed record GroupResult(bool Succeeded, string Code, bool Unknown)
    {
        internal static GroupResult Successful() => new(true, "group_completed", false);
        internal static GroupResult Failure(string code, bool unknown) => new(false, code, unknown);
    }
}
