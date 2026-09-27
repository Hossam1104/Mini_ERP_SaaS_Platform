#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

public sealed record MigrationCancellationResult(
    Guid RunId,
    TenantId TenantId,
    Guid AttemptId,
    MigrationRunStatus RunStatus,
    string Reason,
    byte[] Version);

public sealed class MigrationRunSafetyService(
    MigrationFoundationService foundation,
    MigrationValidationService validation,
    IMigrationExecutionPersistence executionPersistence)
{
    public async Task<MigrationOperationResult<MigrationCancellationResult>> CancelAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        byte[] expectedRunVersion,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (requestContext.TenantContext is not { } tenant || runId == Guid.Empty)
            return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_tenant_context_required");
        if (!FoundationCorrelation.IsValid(idempotencyKey)
            || expectedRunVersion is not { Length: > 0 }
            || string.IsNullOrWhiteSpace(reason)
            || reason.Trim().Length > 512
            || reason.Any(char.IsControl))
            return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_cancellation_request_invalid");

        var runResult = await foundation.FindRunAsync(tenant, runId, cancellationToken);
        if (!runResult.Succeeded || runResult.Value is not { } run)
            return MigrationOperationResult<MigrationCancellationResult>.Rejected(runResult.Code);
        if (!await validation.IsResourceAuthorizedAsync(requestContext, runId, cancellationToken))
            return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_source_scope_denied");

        var safeReason = reason.Trim();
        var fingerprint = MigrationFingerprintEncoder.Compute(
            "migration-run-cancellation-v1",
            runId.ToString("D"),
            tenant.TenantId.Value.ToString("D"),
            safeReason);
        var existing = await foundation.FindIdempotencyAsync(
            tenant, MigrationOperationKind.Cancellation, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_idempotency_conflict");
            if (existing.AttemptId is not { } attemptId)
                return MigrationOperationResult<MigrationCancellationResult>.Unknown("migration_cancellation_attempt_unavailable");
            var attempt = await foundation.FindAttemptAsync(tenant, runId, attemptId, cancellationToken);
            if (attempt is null || !attempt.EvidenceConfirmed || !run.EvidenceConfirmed)
                return MigrationOperationResult<MigrationCancellationResult>.Unknown("migration_audit_recovery_required");
            if (attempt.Outcome == MigrationAttemptOutcome.Pending)
                return MigrationOperationResult<MigrationCancellationResult>.Failure("migration_cancellation_in_progress", safeToRetry: true);
            if (attempt.Outcome == MigrationAttemptOutcome.UnknownOutcome)
                return MigrationOperationResult<MigrationCancellationResult>.Unknown(attempt.SafeOutcomeCode ?? "migration_cancellation_outcome_unknown");
            if (attempt.Outcome == MigrationAttemptOutcome.KnownFailure)
                return MigrationOperationResult<MigrationCancellationResult>.Rejected(attempt.SafeOutcomeCode ?? "migration_cancellation_failed");
            return MigrationOperationResult<MigrationCancellationResult>.Replay(ToResult(run, attempt));
        }

        if (!MigrationRun.Rehydrate(run).PermitsAttempt(MigrationOperationKind.Cancellation)
            || MigrationRun.Rehydrate(run).HasReachedEffectBoundary)
            return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_cancellation_effect_boundary_crossed");
        if (!run.Version.AsSpan().SequenceEqual(expectedRunVersion))
            return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_run_version_conflict");

        var attempts = await foundation.ListAttemptsAsync(tenant, runId, cancellationToken);
        var executionAttempts = attempts.Where(item => item.Operation == MigrationOperationKind.Execution).ToArray();
        foreach (var executionAttempt in executionAttempts)
        {
            var effects = await executionPersistence.ListEffectsAsync(tenant, runId, executionAttempt.AttemptId, cancellationToken);
            if (executionAttempt.Outcome == MigrationAttemptOutcome.UnknownOutcome
                || effects.Any(item => item.Disposition is MigrationExecutionEffectDisposition.Started
                    or MigrationExecutionEffectDisposition.Committed
                    or MigrationExecutionEffectDisposition.Unknown
                    or MigrationExecutionEffectDisposition.PartialCompleted))
                return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_cancellation_effect_boundary_crossed");
            return MigrationOperationResult<MigrationCancellationResult>.Rejected("migration_cancellation_after_execution_started");
        }

        var started = await foundation.StartAttemptAsync(
            requestContext,
            runId,
            MigrationOperationKind.Cancellation,
            idempotencyKey,
            fingerprint,
            cancellationToken);
        if (!started.Succeeded || started.Value is not { } cancellationAttempt)
            return started.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationCancellationResult>.Unknown(started.Code)
                : MigrationOperationResult<MigrationCancellationResult>.Rejected(started.Code);

        var transitioned = await foundation.TransitionRunAsync(
            requestContext,
            runId,
            MigrationRunStatus.Cancelled,
            run.Version,
            cancellationToken,
            safeReason);
        if (!transitioned.Succeeded || transitioned.Value is not { } cancelledRun)
        {
            if (transitioned.Kind == MigrationResultKind.UnknownOutcome)
                return MigrationOperationResult<MigrationCancellationResult>.Unknown(transitioned.Code);
            var failed = await foundation.RecordAttemptOutcomeAsync(
                requestContext,
                runId,
                cancellationAttempt.AttemptId,
                MigrationAttemptOutcome.KnownFailure,
                transitioned.Code,
                cancellationAttempt.Version,
                cancellationToken);
            return failed.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationCancellationResult>.Unknown(failed.Code)
                : MigrationOperationResult<MigrationCancellationResult>.Rejected(transitioned.Code);
        }

        var outcome = await foundation.RecordAttemptOutcomeAsync(
            requestContext,
            runId,
            cancellationAttempt.AttemptId,
            MigrationAttemptOutcome.Succeeded,
            "run_cancelled",
            cancellationAttempt.Version,
            cancellationToken);
        if (!outcome.Succeeded)
            return outcome.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationCancellationResult>.Unknown(outcome.Code)
                : MigrationOperationResult<MigrationCancellationResult>.Failure(outcome.Code, outcome.IsSafeToRetry);

        var confirmedRun = await foundation.FindRunAsync(tenant, runId, cancellationToken);
        var confirmedAttempt = await foundation.FindAttemptAsync(tenant, runId, cancellationAttempt.AttemptId, cancellationToken);
        var finalRun = confirmedRun.Value;
        if (finalRun is null
            || finalRun.Status != MigrationRunStatus.Cancelled
            || string.IsNullOrWhiteSpace(finalRun.CancellationReason)
            || confirmedAttempt is null
            || !finalRun.EvidenceConfirmed
            || !confirmedAttempt.EvidenceConfirmed)
            return MigrationOperationResult<MigrationCancellationResult>.Unknown("migration_cancellation_confirmation_unavailable");
        return MigrationOperationResult<MigrationCancellationResult>.Success(
            ToResult(finalRun, confirmedAttempt),
            "migration_run_cancelled");
    }

    private static MigrationCancellationResult ToResult(MigrationRunRecord run, MigrationAttemptRecord attempt) =>
        new(run.RunId, run.TenantId, attempt.AttemptId, run.Status, run.CancellationReason ?? string.Empty, run.Version);
}

#pragma warning restore CS1591
