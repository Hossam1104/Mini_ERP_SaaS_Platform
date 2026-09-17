using System.Globalization;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

#pragma warning disable CS1591

namespace MiniErp.App.Modules.Migration;

/// <summary>
/// Executes the approved, validated and dry-run-proven MESP-141 master-data
/// reference package through the owning Master Data import engine.
/// </summary>
public sealed class MigrationExecutionService
{
    public const string OperationId = "migration.execution.start";
    public const string FingerprintVersion = "migration-economic-execution-v2";

    private static readonly MigrationCanonicalRecordType[] ExecutionOrder =
    [
        MigrationCanonicalRecordType.Currency,
        MigrationCanonicalRecordType.UnitOfMeasure,
        MigrationCanonicalRecordType.PaymentTerm,
        MigrationCanonicalRecordType.Tax,
        MigrationCanonicalRecordType.Supplier,
        MigrationCanonicalRecordType.Customer,
        MigrationCanonicalRecordType.Product,
        MigrationCanonicalRecordType.InventoryOpening,
        MigrationCanonicalRecordType.ArOpening
    ];

    private readonly MigrationFoundationService foundation;
    private readonly IMigrationFoundationPersistence foundationPersistence;
    private readonly IMigrationValidationPersistence validationPersistence;
    private readonly IMigrationExecutionPersistence executionPersistence;
    private readonly ICurrentOrganizationScopeResolver scopeResolver;
    private readonly IOrganizationScopeOwnershipResolver scopeOwnership;
    private readonly MigrationOwnerExecutionCoordinator ownerCoordinator;
    private readonly MigrationInventoryOpeningExecutionCoordinator? inventoryOpeningCoordinator;
    private readonly MigrationArOpeningExecutionCoordinator? arOpeningCoordinator;
    private readonly SemaphoreSlim executionGate = new(1, 1);

    public MigrationExecutionService(
        MigrationFoundationService foundation,
        IMigrationFoundationPersistence foundationPersistence,
        IMigrationValidationPersistence validationPersistence,
        IMigrationExecutionPersistence executionPersistence,
        ICurrentOrganizationScopeResolver scopeResolver,
        IOrganizationScopeOwnershipResolver scopeOwnership,
        IOwnerExecutionGateway owner,
        IMigrationReferenceAuthority references,
        TimeProvider? timeProvider = null)
        : this(foundation, foundationPersistence, validationPersistence, executionPersistence, scopeResolver, scopeOwnership, owner, references, null, null, timeProvider)
    {
    }

    internal MigrationExecutionService(
        MigrationFoundationService foundation,
        IMigrationFoundationPersistence foundationPersistence,
        IMigrationValidationPersistence validationPersistence,
        IMigrationExecutionPersistence executionPersistence,
        ICurrentOrganizationScopeResolver scopeResolver,
        IOrganizationScopeOwnershipResolver scopeOwnership,
        IOwnerExecutionGateway owner,
        IMigrationReferenceAuthority references,
        MigrationInventoryOpeningExecutionCoordinator? inventoryOpeningCoordinator,
        TimeProvider? timeProvider = null)
        : this(foundation, foundationPersistence, validationPersistence, executionPersistence, scopeResolver, scopeOwnership, owner, references, inventoryOpeningCoordinator, null, timeProvider)
    {
    }

    internal MigrationExecutionService(
        MigrationFoundationService foundation,
        IMigrationFoundationPersistence foundationPersistence,
        IMigrationValidationPersistence validationPersistence,
        IMigrationExecutionPersistence executionPersistence,
        ICurrentOrganizationScopeResolver scopeResolver,
        IOrganizationScopeOwnershipResolver scopeOwnership,
        IOwnerExecutionGateway owner,
        IMigrationReferenceAuthority references,
        MigrationInventoryOpeningExecutionCoordinator? inventoryOpeningCoordinator,
        MigrationArOpeningExecutionCoordinator? arOpeningCoordinator,
        TimeProvider? timeProvider = null)
    {
        this.foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
        this.foundationPersistence = foundationPersistence ?? throw new ArgumentNullException(nameof(foundationPersistence));
        this.validationPersistence = validationPersistence ?? throw new ArgumentNullException(nameof(validationPersistence));
        this.executionPersistence = executionPersistence ?? throw new ArgumentNullException(nameof(executionPersistence));
        this.scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        this.scopeOwnership = scopeOwnership ?? throw new ArgumentNullException(nameof(scopeOwnership));
        var clock = timeProvider ?? TimeProvider.System;
        var referenceAuthority = references ?? throw new ArgumentNullException(nameof(references));
        ownerCoordinator = new MigrationOwnerExecutionCoordinator(
            executionPersistence,
            referenceAuthority,
            owner ?? throw new ArgumentNullException(nameof(owner)),
            clock);
        this.inventoryOpeningCoordinator = inventoryOpeningCoordinator;
        this.arOpeningCoordinator = arOpeningCoordinator;
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
        return attempt is null
            ? null
            : await BuildResultAsync(requestContext, run, attempt, attempt.RequestFingerprint, attempt.SafeOutcomeCode ?? "execution_in_progress", tenant, cancellationToken);
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
        {
            var attempts = await foundationPersistence.ListAttemptsAsync(tenant, runId, cancellationToken);
            if (attempts.Any(item => item.Operation == MigrationOperationKind.Execution
                    && item.Outcome == MigrationAttemptOutcome.Pending
                    && !string.Equals(item.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)))
                return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_attempt_claim_conflict");

            return MigrationOperationResult<MigrationExecutionResult>.Unknown("migration_audit_recovery_required");
        }
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
        var hasInventoryOpening = gate.Plan.Any(item => item.Staged.RecordType == MigrationCanonicalRecordType.InventoryOpening);
        var hasArOpening = gate.Plan.Any(item => item.Staged.RecordType == MigrationCanonicalRecordType.ArOpening);
        if (hasInventoryOpening && inventoryOpeningCoordinator is null)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_inventory_execution_unavailable");
        if (hasArOpening && arOpeningCoordinator is null)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_ar_execution_unavailable");

        var economicTypes = gate.Plan.Where(item => item.Staged.RecordType is MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening).Select(item => item.Staged.RecordType).Distinct().ToArray();
        var fingerprint = ComputeFingerprint(run, intake, validation, dryRun, gate.Scope!, economicTypes);
        var existing = await foundationPersistence.FindIdempotencyAsync(tenant, MigrationOperationKind.Execution, idempotencyKey, cancellationToken);
        if (existing is not null && !string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_idempotency_conflict");
        if (existing is null && MigrationRun.ReconciliationRequiredStates.Contains(run.Status))
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_run_requires_reconciliation");
        var resumeRows = Array.Empty<MigrationExecutionPlanRow>();
        if (existing is null && run.Status == MigrationRunStatus.PartiallyCompleted)
        {
            var attempts = await foundationPersistence.ListAttemptsAsync(tenant, runId, cancellationToken);
            var prior = attempts.OrderByDescending(item => item.Sequence).FirstOrDefault();
            var priorEffects = prior is null
                ? []
                : await executionPersistence.ListEffectsAsync(tenant, runId, prior.AttemptId, cancellationToken);
            if (prior is null || prior.Outcome != MigrationAttemptOutcome.KnownFailure
                || priorEffects.Count == 0
                || priorEffects.Any(item => item.Disposition is MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Unknown)
                || priorEffects.Any(item => item.RecordType is not (MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening) && item.Disposition is not (MigrationExecutionEffectDisposition.NonEffect or MigrationExecutionEffectDisposition.Committed))
                || !priorEffects.Any(item => item.RecordType is MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.PartialCompleted))
                return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_partial_resume_not_permitted");
            var completedEconomic = priorEffects.Where(item => item.RecordType is MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed)
                .Select(item => item.StagedRecordId).ToHashSet();
            resumeRows = gate.Plan.Where(item => item.Staged.RecordType is MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening && !completedEconomic.Contains(item.Staged.StagedRecordId)).ToArray();
            if (resumeRows.Length == 0)
                return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_partial_resume_not_permitted");
        }
        if (existing is null && run.Status != MigrationRunStatus.Approved && run.Status != MigrationRunStatus.PartiallyCompleted)
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_approval_required");
        if (existing is null && !run.Version.AsSpan().SequenceEqual(expectedRunVersion))
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_run_version_conflict");

        var started = await foundation.StartAttemptAsync(requestContext, runId, MigrationOperationKind.Execution, idempotencyKey, fingerprint, cancellationToken);
        if (!started.Succeeded || started.Value is not { } attempt)
            return Map(started);

        if (started.Kind == MigrationResultKind.Replayed && attempt.Outcome == MigrationAttemptOutcome.Pending)
        {
            var inProgressRun = await foundationPersistence.FindRunAsync(tenant, runId, cancellationToken) ?? run;
            var inProgress = await BuildResultAsync(requestContext, inProgressRun, attempt, fingerprint, "migration_execution_in_progress", tenant, cancellationToken);
            return MigrationOperationResult<MigrationExecutionResult>.Replay(inProgress);
        }

        if (attempt.Outcome != MigrationAttemptOutcome.Pending)
        {
            var replayRun = await foundationPersistence.FindRunAsync(tenant, runId, cancellationToken) ?? run;
            var replayResult = await BuildResultAsync(requestContext, replayRun, attempt, fingerprint, attempt.SafeOutcomeCode ?? "execution_completed", tenant, cancellationToken);
            return attempt.Outcome switch
            {
                MigrationAttemptOutcome.UnknownOutcome => new MigrationOperationResult<MigrationExecutionResult>(MigrationResultKind.UnknownOutcome, attempt.SafeOutcomeCode ?? "migration_execution_outcome_unknown", replayResult, false),
                MigrationAttemptOutcome.KnownFailure => MigrationOperationResult<MigrationExecutionResult>.Failure(attempt.SafeOutcomeCode ?? "migration_execution_failed"),
                _ => started.Kind == MigrationResultKind.Replayed ? MigrationOperationResult<MigrationExecutionResult>.Replay(replayResult) : MigrationOperationResult<MigrationExecutionResult>.Success(replayResult)
            };
        }

        var masterPlan = (resumeRows.Length > 0 ? resumeRows : gate.Plan)
            .Where(item => item.Staged.RecordType is not (MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening))
            .ToArray();
        var economicPlan = (resumeRows.Length > 0 ? resumeRows : gate.Plan)
            .Where(item => item.Staged.RecordType is MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening)
            .ToArray();
        var prepared = await ownerCoordinator.PrepareAsync(requestContext, tenant, run, attempt, masterPlan, cancellationToken);
        if (!prepared.Succeeded)
        {
            await ownerCoordinator.MarkPreparationFailedAsync(tenant, attempt, prepared.Code, cancellationToken);
            return await FinishWithoutOwnerEffectAsync(requestContext, run, attempt, prepared.Code, cancellationToken);
        }

        var inventoryPlan = economicPlan.Where(item => item.Staged.RecordType == MigrationCanonicalRecordType.InventoryOpening).ToArray();
        var arPlan = economicPlan.Where(item => item.Staged.RecordType == MigrationCanonicalRecordType.ArOpening).ToArray();
        if (inventoryPlan.Length > 0)
        {
            var economicPrepared = await inventoryOpeningCoordinator!.PrepareAsync(requestContext, tenant, run, attempt, inventoryPlan, cancellationToken);
            if (!economicPrepared.Succeeded)
            {
                await ownerCoordinator.MarkPreparationFailedAsync(tenant, attempt, economicPrepared.Code, cancellationToken);
                await inventoryOpeningCoordinator.MarkPreparationFailedAsync(tenant, attempt, economicPrepared.Code, cancellationToken);
                return await FinishWithoutOwnerEffectAsync(requestContext, run, attempt, economicPrepared.Code, cancellationToken);
            }
        }
        if (arPlan.Length > 0)
        {
            var economicPrepared = await arOpeningCoordinator!.PrepareAsync(requestContext, tenant, run, attempt, arPlan, cancellationToken);
            if (!economicPrepared.Succeeded)
            {
                await ownerCoordinator.MarkPreparationFailedAsync(tenant, attempt, economicPrepared.Code, cancellationToken);
                if (inventoryPlan.Length > 0) await inventoryOpeningCoordinator!.MarkPreparationFailedAsync(tenant, attempt, economicPrepared.Code, cancellationToken);
                await arOpeningCoordinator.MarkPreparationFailedAsync(tenant, attempt, economicPrepared.Code, cancellationToken);
                return await FinishWithoutOwnerEffectAsync(requestContext, run, attempt, economicPrepared.Code, cancellationToken);
            }
        }

        var currentRun = await foundationPersistence.FindRunAsync(tenant, runId, cancellationToken) ?? run;
        if (currentRun.Status is MigrationRunStatus.Approved or MigrationRunStatus.PartiallyCompleted)
        {
            var executing = await foundation.TransitionRunAsync(requestContext, runId, MigrationRunStatus.Executing, currentRun.Version, cancellationToken);
            if (!executing.Succeeded || executing.Value is not { } transitioned)
                return MapExecutionFailure(executing);
            currentRun = transitioned;
        }
        else if (currentRun.Status != MigrationRunStatus.Executing)
        {
            return MigrationOperationResult<MigrationExecutionResult>.Rejected("migration_execution_state_invalid");
        }

        foreach (var group in masterPlan.GroupBy(item => item.Preview.RecordType).OrderBy(item => Array.IndexOf(ExecutionOrder, item.Key)))
        {
            var masterResult = await ownerCoordinator.ExecuteAsync(requestContext, tenant, attempt, group.Key, group.ToArray(), cancellationToken);
            var processed = new MigrationEconomicGroupResult(masterResult.Succeeded, masterResult.Code, masterResult.Unknown);
            if (!processed.Succeeded)
                return await FinishGroupFailureAsync(requestContext, tenant, currentRun, attempt, processed, cancellationToken);
        }

        if (economicPlan.Length > 0)
        {
            foreach (var group in economicPlan.GroupBy(item => item.Preview.RecordType).OrderBy(item => Array.IndexOf(ExecutionOrder, item.Key)))
            {
                var processed = group.Key == MigrationCanonicalRecordType.InventoryOpening
                    ? await inventoryOpeningCoordinator!.ExecuteAsync(requestContext, tenant, run, attempt, group.ToArray(), cancellationToken)
                    : await arOpeningCoordinator!.ExecuteAsync(requestContext, tenant, run, attempt, group.ToArray(), cancellationToken);
                if (!processed.Succeeded)
                    return await FinishGroupFailureAsync(requestContext, tenant, currentRun, attempt, processed, cancellationToken);
            }
        }

        var finalEffects = await executionPersistence.ListEffectsAsync(tenant, runId, attempt.AttemptId, CancellationToken.None);
        var outcome = finalEffects.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)
            ? MigrationAttemptOutcome.UnknownOutcome
            : finalEffects.Any(item => item.Disposition is MigrationExecutionEffectDisposition.Failed or MigrationExecutionEffectDisposition.PartialCompleted)
                ? MigrationAttemptOutcome.KnownFailure
                : MigrationAttemptOutcome.Succeeded;
        var code = outcome switch
        {
            MigrationAttemptOutcome.Succeeded => "migration_execution_completed",
            MigrationAttemptOutcome.KnownFailure when finalEffects.Any(item => item.Disposition is MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.PartialCompleted) => "migration_execution_partially_completed",
            MigrationAttemptOutcome.KnownFailure => "migration_execution_failed",
            _ => "migration_execution_outcome_unknown"
        };
        return await FinishAsync(requestContext, tenant, currentRun, attempt, outcome, code, CancellationToken.None);
    }

    private async Task<MigrationOperationResult<MigrationExecutionResult>> FinishGroupFailureAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        MigrationEconomicGroupResult result,
        CancellationToken cancellationToken)
    {
        var effects = await executionPersistence.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, CancellationToken.None);
        if (result.Unknown || effects.Any(item => item.Disposition is MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Unknown))
            return await FinishAsync(requestContext, tenant, run, attempt, MigrationAttemptOutcome.UnknownOutcome, result.Code, CancellationToken.None);
        var code = effects.Any(item => item.Disposition is MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.PartialCompleted)
            ? "migration_execution_partially_completed"
            : result.Code;
        return await FinishAsync(requestContext, tenant, run, attempt, MigrationAttemptOutcome.KnownFailure, code, CancellationToken.None);
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
        var stateToken = outcome == MigrationAttemptOutcome.UnknownOutcome ? CancellationToken.None : cancellationToken;
        var recorded = await foundation.RecordAttemptOutcomeAsync(requestContext, run.RunId, attempt.AttemptId, outcome, code, attempt.Version, stateToken);
        if (!recorded.Succeeded || recorded.Value is not { } completedAttempt)
            return MapExecutionFailure(recorded);

        var target = outcome switch
        {
            MigrationAttemptOutcome.Succeeded => MigrationRunStatus.Completed,
            MigrationAttemptOutcome.KnownFailure => (await executionPersistence.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, stateToken)).Any(item => item.Disposition is MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.PartialCompleted) ? MigrationRunStatus.PartiallyCompleted : MigrationRunStatus.Failed,
            _ => MigrationRunStatus.OutcomeUnknown
        };
        var current = await foundationPersistence.FindRunAsync(tenant, run.RunId, stateToken) ?? run;
        var transitioned = await foundation.TransitionRunAsync(requestContext, run.RunId, target, current.Version, stateToken);
        if (!transitioned.Succeeded || transitioned.Value is not { } completedRun)
            return MapExecutionFailure(transitioned);
        var result = await BuildResultAsync(requestContext, completedRun, completedAttempt, attempt.RequestFingerprint, code, tenant, stateToken);
        return outcome == MigrationAttemptOutcome.Succeeded
            ? MigrationOperationResult<MigrationExecutionResult>.Success(result, code)
            : outcome == MigrationAttemptOutcome.KnownFailure
                ? MigrationOperationResult<MigrationExecutionResult>.Failure(code)
                : new MigrationOperationResult<MigrationExecutionResult>(MigrationResultKind.UnknownOutcome, code, result, false);
    }

    private async Task<MigrationOperationResult<MigrationExecutionResult>> FinishWithoutOwnerEffectAsync(
        FoundationRequestContext requestContext,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        string code,
        CancellationToken cancellationToken)
    {
        var recorded = await foundation.RecordAttemptOutcomeAsync(requestContext, run.RunId, attempt.AttemptId, MigrationAttemptOutcome.KnownFailure, code, attempt.Version, cancellationToken);
        return !recorded.Succeeded ? MapExecutionFailure(recorded) : MigrationOperationResult<MigrationExecutionResult>.Failure(code);
    }

    private async Task<MigrationExecutionResult> BuildResultAsync(
        FoundationRequestContext requestContext,
        MigrationRunRecord run,
        MigrationAttemptRecord attempt,
        string fingerprint,
        string code,
        TenantContext tenant,
        CancellationToken cancellationToken)
    {
        var batches = await executionPersistence.ListBatchesAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        var effects = await executionPersistence.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        var representations = await executionPersistence.ListRepresentationsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken);
        IReadOnlyList<MigrationEconomicReconciliationRecord> reconciliations = [];
        IReadOnlyList<MigrationArEconomicReconciliationRecord> arReconciliations = [];
        if (inventoryOpeningCoordinator is not null)
        {
            var attempts = await foundationPersistence.ListAttemptsAsync(tenant, run.RunId, cancellationToken);
            var effectsByAttempt = new List<(int Sequence, MigrationExecutionEffectRecord Effect)>();
            foreach (var item in attempts)
            {
                foreach (var effect in await executionPersistence.ListEffectsAsync(tenant, run.RunId, item.AttemptId, cancellationToken))
                    effectsByAttempt.Add((item.Sequence, effect));
            }
            var latestEffects = effectsByAttempt
                .Where(item => item.Effect.RecordType == MigrationCanonicalRecordType.InventoryOpening)
                .GroupBy(item => item.Effect.StagedRecordId)
                .Select(group => group.OrderByDescending(item => item.Sequence).First().Effect)
                .ToArray();
            if (latestEffects.Length > 0)
            {
                var staged = await validationPersistence.ListStagedRecordsAsync(tenant, run.RunId, 0, int.MaxValue, cancellationToken);
                reconciliations = await inventoryOpeningCoordinator.ReadReconciliationsAsync(requestContext, tenant, latestEffects, staged, cancellationToken);
            }
        }
        if (arOpeningCoordinator is not null)
        {
            var attempts = await foundationPersistence.ListAttemptsAsync(tenant, run.RunId, cancellationToken);
            var effectsByAttempt = new List<(int Sequence, MigrationExecutionEffectRecord Effect)>();
            foreach (var item in attempts)
            {
                foreach (var effect in await executionPersistence.ListEffectsAsync(tenant, run.RunId, item.AttemptId, cancellationToken))
                    effectsByAttempt.Add((item.Sequence, effect));
            }
            var latestEffects = effectsByAttempt
                .Where(item => item.Effect.RecordType == MigrationCanonicalRecordType.ArOpening)
                .GroupBy(item => item.Effect.StagedRecordId)
                .Select(group => group.OrderByDescending(item => item.Sequence).First().Effect)
                .ToArray();
            if (latestEffects.Length > 0)
            {
                var staged = await validationPersistence.ListStagedRecordsAsync(tenant, run.RunId, 0, int.MaxValue, cancellationToken);
                arReconciliations = await arOpeningCoordinator.ReadReconciliationsAsync(requestContext, tenant, latestEffects, staged, cancellationToken);
            }
        }
        return new MigrationExecutionResult(run.RunId, tenant.TenantId, attempt.AttemptId, FingerprintVersion, fingerprint, run.Status, attempt.Outcome, code, batches, effects, representations, reconciliations, arReconciliations);
    }

    private PlanGate BuildPlan(
        MigrationRunRecord run,
        MigrationIntakeRecord intake,
        MigrationValidationSummary validation,
        MigrationDryRunPreview dryRun,
        IReadOnlyList<MigrationStagedRecord> staged,
        TenantContext tenant)
    {
        if (validation.TenantId != tenant.TenantId
            || validation.RunId != run.RunId
            || dryRun.TenantId != tenant.TenantId
            || dryRun.RunId != run.RunId
            || !string.Equals(validation.PackageHash, dryRun.PackageHash, StringComparison.Ordinal)
            || !string.Equals(validation.SourceSnapshotHash, dryRun.SourceSnapshotHash, StringComparison.Ordinal)
            || !string.Equals(validation.SourceSnapshotHash, intake.Source.Sha256, StringComparison.OrdinalIgnoreCase)
            || validation.AttemptId != dryRun.ValidationAttemptId
            || staged.Count != validation.TotalStagedRecords
            || staged.Count != dryRun.TotalStagedRecords
            || validation.Records.Count != staged.Count
            || dryRun.Rows.Count != staged.Count
            || staged.Any(item => item.TenantId != tenant.TenantId || item.RunId != run.RunId || !string.Equals(item.PackageHash, validation.PackageHash, StringComparison.Ordinal) || !string.Equals(item.SourceSnapshotHash, validation.SourceSnapshotHash, StringComparison.Ordinal)))
            return PlanGate.Failure("migration_execution_authoritative_snapshot_mismatch");

        if (staged.GroupBy(item => item.StagedRecordId).Any(group => group.Count() != 1)
            || staged.GroupBy(item => item.SourceSequence).Any(group => group.Count() != 1)
            || validation.Records.GroupBy(item => item.StagedRecordId).Any(group => group.Count() != 1)
            || validation.Records.GroupBy(item => item.SourceSequence).Any(group => group.Count() != 1)
            || dryRun.Rows.GroupBy(item => item.StagedRecordId).Any(group => group.Count() != 1)
            || dryRun.Rows.GroupBy(item => item.SourceSequence).Any(group => group.Count() != 1))
            return PlanGate.Failure("migration_execution_authoritative_snapshot_mismatch");
        if (staged.Any(item => !IsSupported(item.RecordType)))
            return PlanGate.Failure("migration_execution_record_type_not_supported");
        if (validation.Records.Any(item => item.Disposition != MigrationRecordDisposition.Accepted)
            || dryRun.Rows.Any(item => item.Disposition != MigrationRecordDisposition.Accepted || item.PlannedAction == MigrationPlannedAction.Blocked))
            return PlanGate.Failure("migration_execution_blocking_row_present");

        var validationById = validation.Records.ToDictionary(item => item.StagedRecordId);
        var previewById = dryRun.Rows.ToDictionary(item => item.StagedRecordId);
        var plan = new List<MigrationExecutionPlanRow>(staged.Count);
        foreach (var record in staged.OrderBy(item => item.SourceSequence))
        {
            if (!validationById.TryGetValue(record.StagedRecordId, out var validationRow)
                || !previewById.TryGetValue(record.StagedRecordId, out var preview)
                || validationRow.SourceSequence != record.SourceSequence
                || validationRow.RecordType != record.RecordType
                || preview.SourceSequence != record.SourceSequence
                || preview.RecordType != record.RecordType)
                return PlanGate.Failure("migration_execution_authoritative_snapshot_mismatch");
            if (!TryParse(record, out var parsed))
                return PlanGate.Failure("migration_execution_payload_invalid");
            if (parsed!.HasForbiddenControlAccountId)
                return PlanGate.Failure("migration_ar_control_account_not_allowed");
            if (preview.PlannedAction == MigrationPlannedAction.Create && parsed.Payload is not (MigrationProductPayload or MigrationSupplierPayload or MigrationCustomerPayload or MigrationInventoryOpeningPayload or MigrationArOpeningPayload))
                return PlanGate.Failure("migration_execution_owner_action_invalid");
            if (preview.PlannedAction is not (MigrationPlannedAction.Create or MigrationPlannedAction.MatchReference or MigrationPlannedAction.Skip))
                return PlanGate.Failure("migration_execution_plan_invalid");
            plan.Add(new MigrationExecutionPlanRow(record, preview, parsed!));
        }

        var scope = scopeResolver.ResolveCurrent(tenant);
        return !scope.Allowed || scope.Scope is not { } resolved ? PlanGate.Failure("migration_source_scope_denied") : PlanGate.Success(plan, resolved);
    }

    private bool IsCurrentScopeAuthorized(TenantContext tenant, MigrationSourceArtifactSnapshot source)
    {
        if (source.TenantId != tenant.TenantId)
            return false;
        var authorized = scopeResolver.ResolveCurrent(tenant);
        var requested = ResolveRequestedScope(tenant, source.CompanyId, source.BranchId, source.WarehouseId);
        return authorized.Allowed && authorized.Scope is { } authorizedScope && requested.Allowed && requested.Scope is { } requestedScope && authorizedScope.ContainsAuthorizedDescendant(requestedScope);
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

    private static string ComputeFingerprint(MigrationRunRecord run, MigrationIntakeRecord intake, MigrationValidationSummary validation, MigrationDryRunPreview dryRun, TenantWorkScope scope, IReadOnlyList<MigrationCanonicalRecordType> economicTypes)
    {
        var common = new[]
        {
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
            "master-data-owner-import-v1"
        };
        var economics = economicTypes.OrderBy(item => item).Select(item => item switch
        {
            MigrationCanonicalRecordType.InventoryOpening => "inventory-economic-opening-v1",
            MigrationCanonicalRecordType.ArOpening => "ar-economic-opening-v1",
            _ => item.ToString()
        });
        return MigrationFingerprintEncoder.Compute(FingerprintVersion, [.. common, .. economics]);
    }

    private static string ScopeValue(TenantWorkScope scope) => scope.WarehouseId is { } warehouse ? $"Warehouse:{warehouse:D}" : scope.BranchId is { } branch ? $"Branch:{branch:D}" : scope.CompanyId is { } company ? $"Company:{company:D}" : $"Tenant:{scope.TenantId.Value:D}";

    private static bool IsSupported(MigrationCanonicalRecordType type) => type is
        MigrationCanonicalRecordType.Product or
        MigrationCanonicalRecordType.Supplier or
        MigrationCanonicalRecordType.Customer or
        MigrationCanonicalRecordType.Currency or
        MigrationCanonicalRecordType.Tax or
        MigrationCanonicalRecordType.PaymentTerm or
        MigrationCanonicalRecordType.UnitOfMeasure or
        MigrationCanonicalRecordType.InventoryOpening or
        MigrationCanonicalRecordType.ArOpening;

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
                MigrationCanonicalRecordType.InventoryOpening => document.Deserialize<MigrationInventoryOpeningPayload>(options),
                MigrationCanonicalRecordType.ArOpening => document.Deserialize<MigrationArOpeningPayload>(options),
                MigrationCanonicalRecordType.Supplier => document.Deserialize<MigrationSupplierPayload>(options),
                MigrationCanonicalRecordType.Customer => document.Deserialize<MigrationCustomerPayload>(options),
                MigrationCanonicalRecordType.Currency or MigrationCanonicalRecordType.Tax or MigrationCanonicalRecordType.PaymentTerm or MigrationCanonicalRecordType.UnitOfMeasure => document.Deserialize<MigrationReferencePayload>(options),
                _ => null
            };
            parsed = payload is null ? null : new MigrationParsedCanonicalRow(staged.SourceSequence, staged.SourceRecordId, staged.RecordType, payload, staged.CanonicalPayload, staged.RecordType == MigrationCanonicalRecordType.ArOpening && document.RootElement.EnumerateObject().Any(item => string.Equals(item.Name, "controlAccountId", StringComparison.OrdinalIgnoreCase)));
            return parsed is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

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
}

#pragma warning restore CS1591
