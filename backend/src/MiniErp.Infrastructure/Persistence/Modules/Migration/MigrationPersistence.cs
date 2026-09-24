#pragma warning disable CS1591

using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

/// <summary>
/// Durable Migration foundation adapter. Every query is created from the
/// trusted Tenant context and every idempotency record stores identifiers and
/// fingerprints only; no imported payload is persisted by this slice.
/// </summary>
internal sealed partial class MigrationPersistence : IMigrationFoundationPersistence, IMigrationValidationPersistence, IMigrationExecutionPersistence, IMigrationReconciliationPersistence
{
    private readonly DbContextOptions options;
    private readonly TimeProvider timeProvider;

    internal MigrationPersistence(DbContextOptions options, TimeProvider? timeProvider = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MigrationPersistenceResult<MigrationIntakeRecord>> CreateIntakeAsync(
        TenantContext tenantContext,
        CreateMigrationIntakeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);

        if (command.Run.TenantId != tenantContext.TenantId
            || command.Source.TenantId != tenantContext.TenantId
            || command.Source.ObjectId == Guid.Empty
            || command.Source.Length < 0
            || command.Source.ConcurrencyVersion < 1)
        {
            return MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                "migration_tenant_context_mismatch");
        }

        await using var db = CreateContext(tenantContext);
        var existingIntake = await db.Intakes.SingleOrDefaultAsync(
            item => item.IdempotencyKey == command.IdempotencyKey.Value,
            cancellationToken);
        if (existingIntake is not null)
        {
            return await ResolveIntakeReplayAsync(
                tenantContext,
                existingIntake,
                command.RequestFingerprint.Value,
                cancellationToken);
        }

        var existingKey = await FindIdempotencyEntityAsync(
            db,
            command.Operation,
            command.IdempotencyKey.Value,
            cancellationToken);
        if (existingKey is not null)
        {
            return await ResolveExistingIntakeKeyAsync(
                tenantContext,
                existingKey,
                command.RequestFingerprint.Value,
                cancellationToken);
        }

        var run = new MigrationRunEntity(command.Run);
        var intake = new MigrationIntakeEntity(
            command.Run,
            command.Operation,
            command.IdempotencyKey,
            command.RequestFingerprint,
            command.FingerprintVersion,
            command.Source);
        var identity = new MigrationIdempotencyEntity(
            tenantContext.TenantId,
            command.Run.RunId,
            command.Operation,
            command.IdempotencyKey,
            command.RequestFingerprint,
            attemptId: null,
            MigrationResultKind.Succeeded,
            "intake_created",
            command.Run.CreatedAt);
        db.Runs.Add(run);
        db.Intakes.Add(intake);
        db.Idempotency.Add(identity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationIntakeRecord>.Success(ToRecord(intake, run));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return await ResolveConcurrentIntakeAsync(
                tenantContext,
                command.Operation,
                command.IdempotencyKey.Value,
                command.RequestFingerprint.Value,
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_intake_outcome_unknown");
        }
    }

    public async Task<MigrationPersistenceResult<MigrationRunRecord>> CreateRunAsync(
        TenantContext tenantContext,
        CreateMigrationRunCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);

        if (command.Run.TenantId != tenantContext.TenantId)
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                "migration_tenant_context_mismatch");
        }

        await using var db = CreateContext(tenantContext);
        var existingKey = await FindIdempotencyEntityAsync(
            db,
            command.Operation,
            command.IdempotencyKey.Value,
            cancellationToken);
        if (existingKey is not null)
        {
            return await ResolveRunReplayAsync(
                tenantContext,
                existingKey,
                command.RequestFingerprint.Value,
                cancellationToken);
        }

        var run = new MigrationRunEntity(command.Run);
        var identity = new MigrationIdempotencyEntity(
            tenantContext.TenantId,
            command.Run.RunId,
            command.Operation,
            command.IdempotencyKey,
            command.RequestFingerprint,
            attemptId: null,
            MigrationResultKind.Succeeded,
            "run_created",
            command.Run.CreatedAt);
        db.Runs.Add(run);
        db.Idempotency.Add(identity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationRunRecord>.Success(ToRecord(run));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // A concurrent caller won the race on the Tenant-scoped
            // idempotency key. The winner's row must be read from a FRESH
            // context: this context still tracks our own failed Added entities,
            // so re-querying it would resolve the key to our own uncommitted
            // run and find no committed run behind it, which previously turned
            // a legitimate replay into a spurious duplicate rejection.
            return await ResolveConcurrentRunAsync(
                tenantContext,
                command.Operation,
                command.IdempotencyKey.Value,
                command.RequestFingerprint.Value,
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Not a uniqueness race. The write may or may not have taken
            // effect, so it is reported as an unproven outcome rather than
            // being misclassified as a business conflict.
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_run_outcome_unknown");
        }
    }

    public async Task<MigrationRunRecord?> FindRunAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (runId == Guid.Empty)
        {
            return null;
        }

        await using var db = CreateContext(tenantContext);
        var run = await db.Runs.SingleOrDefaultAsync(item => item.RunId == runId, cancellationToken);
        return run is null ? null : ToRecord(run);
    }

    public async Task<MigrationPersistenceResult<MigrationAttemptRecord>> StartAttemptAsync(
        TenantContext tenantContext,
        StartMigrationAttemptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);

        await using var db = CreateContext(tenantContext);
        var runEntity = await db.Runs.SingleOrDefaultAsync(item => item.RunId == command.RunId, cancellationToken);
        if (runEntity is null)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.NotFound,
                "migration_run_not_found");
        }

        if (!runEntity.EvidenceConfirmed)
        {
            if (command.Operation == MigrationOperationKind.Execution
                && await db.Attempts.AnyAsync(item => item.RunId == command.RunId
                    && item.Operation == MigrationOperationKind.Execution
                    && item.Outcome == MigrationAttemptOutcome.Pending
                    && item.IdempotencyKey != command.IdempotencyKey.Value, cancellationToken))
            {
                return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_execution_attempt_claim_conflict");
            }

            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_audit_recovery_required");
        }

        var existingKey = await FindIdempotencyEntityAsync(
            db,
            command.Operation,
            command.IdempotencyKey.Value,
            cancellationToken);
        if (existingKey is not null)
        {
            return await ResolveAttemptReplayAsync(
                tenantContext,
                existingKey,
                command,
                cancellationToken);
        }

        // Lineage is derived here, from the durable attempts of exactly this
        // run, rather than accepted from the caller. Sequence and predecessor
        // therefore cannot be forged, skipped or reused.
        var persistedAttempts = await ReadAttemptsAsync(db, command.RunId, cancellationToken);
        var run = MigrationRun.Rehydrate(ToRecord(runEntity));
        var lineage = MigrationAttemptLineage.FromPersistedAttempts(
            tenantContext.TenantId,
            command.RunId,
            persistedAttempts,
            command.Operation == MigrationOperationKind.Execution);
        var started = MigrationAttempt.StartNext(
            run,
            command.Operation,
            command.IdempotencyKey,
            command.RequestFingerprint,
            lineage,
            timeProvider);
        if (started.Value is not { } attempt)
        {
            // The idempotency read above and the attempts read are separate
            // statements, so a concurrent caller with this same key can commit
            // between them. This caller then sees the winner's open attempt and
            // would refuse its own retry as "previous still open". The winner
            // writes the attempt and its idempotency row in one transaction, so
            // re-checking the key proves which case this is: a committed row
            // replays, and a free key keeps the genuine lineage denial.
            return await ResolveLineageDenialAsync(
                tenantContext,
                command,
                started.Code,
                cancellationToken);
        }

        var entity = new MigrationAttemptEntity(attempt);
        var identity = new MigrationIdempotencyEntity(
            tenantContext.TenantId,
            attempt.RunId,
            attempt.Operation,
            attempt.IdempotencyKey,
            attempt.RequestFingerprint,
            attempt.AttemptId,
            MigrationResultKind.Succeeded,
            "attempt_started",
            attempt.StartedAt);
        db.Attempts.Add(entity);
        db.Idempotency.Add(identity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationAttemptRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Either a concurrent caller used the same idempotency key, or two
            // callers derived the same lineage sequence for this run. Both are
            // resolved from a fresh context so the winner's committed attempt
            // can be replayed instead of rejected.
            return await ResolveConcurrentAttemptAsync(tenantContext, command, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_attempt_outcome_unknown");
        }
    }

    public async Task<MigrationAttemptRecord?> FindAttemptAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (runId == Guid.Empty || attemptId == Guid.Empty)
        {
            return null;
        }

        await using var db = CreateContext(tenantContext);
        var attempt = await db.Attempts.SingleOrDefaultAsync(
            item => item.RunId == runId && item.AttemptId == attemptId,
            cancellationToken);
        return attempt is null ? null : ToRecord(attempt);
    }

    public async Task<IReadOnlyList<MigrationAttemptRecord>> ListAttemptsAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (runId == Guid.Empty)
        {
            return Array.Empty<MigrationAttemptRecord>();
        }

        await using var db = CreateContext(tenantContext);
        return await ReadAttemptsAsync(db, runId, cancellationToken);
    }

    public async Task<MigrationPersistenceResult<MigrationRunRecord>> ApplyRunTransitionAsync(
        TenantContext tenantContext,
        ApplyMigrationRunTransitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);

        await using var db = CreateContext(tenantContext);
        var entity = await db.Runs.SingleOrDefaultAsync(item => item.RunId == command.RunId, cancellationToken);
        if (entity is null)
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.NotFound,
                "migration_run_not_found");
        }

        if (!entity.EvidenceConfirmed)
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_audit_recovery_required");
        }

        if (!entity.Version.AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_run_version_conflict");
        }

        // The transition is evaluated by the domain map against the PERSISTED
        // status, not against any status the caller believed the run held.
        var run = MigrationRun.Rehydrate(ToRecord(entity));
        var transition = run.TryTransition(command.Target, timeProvider);
        if (!transition.Allowed)
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                transition.Code);
        }

        entity.ApplyDomainTransition(run.Status, run.UpdatedAt);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationRunRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_run_version_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_run_outcome_unknown");
        }
    }

    public async Task<MigrationPersistenceResult<MigrationAttemptRecord>> RecordAttemptOutcomeAsync(
        TenantContext tenantContext,
        RecordMigrationAttemptOutcomeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);

        await using var db = CreateContext(tenantContext);
        var entity = await db.Attempts.SingleOrDefaultAsync(
            item => item.RunId == command.RunId && item.AttemptId == command.AttemptId,
            cancellationToken);
        if (entity is null)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.NotFound,
                "migration_attempt_not_found");
        }

        var runEntity = await db.Runs.SingleOrDefaultAsync(item => item.RunId == command.RunId, cancellationToken);
        if (runEntity is null)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.NotFound,
                "migration_run_not_found");
        }

        if (!runEntity.EvidenceConfirmed)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_audit_recovery_required");
        }

        if (!entity.EvidenceConfirmed)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_audit_recovery_required");
        }

        if (!entity.Version.AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_attempt_version_conflict");
        }

        if (!entity.TryRecordOutcome(command.Outcome, command.SafeOutcomeCode, timeProvider.GetUtcNow()))
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_attempt_already_finished");
        }

        // The attempt outcome, its idempotency identity and the owning run form
        // one evidence lineage. Invalidate all three before the single
        // SaveChanges transaction so replay cannot observe a terminal outcome
        // behind stale certainty.
        runEntity.SetEvidenceConfirmed(false);
        var identity = await db.Idempotency.SingleOrDefaultAsync(
            item => item.RunId == command.RunId
                && item.AttemptId == command.AttemptId
                && item.Operation == entity.Operation
                && item.IdempotencyKey == entity.IdempotencyKey,
            cancellationToken);
        identity?.SetEvidenceConfirmed(false);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationAttemptRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_attempt_version_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_attempt_outcome_unknown");
        }
    }

    public async Task<MigrationIdempotencyRecord?> FindIdempotencyAsync(
        TenantContext tenantContext,
        MigrationOperationKind operation,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        await using var db = CreateContext(tenantContext);
        var identity = await FindIdempotencyEntityAsync(db, operation, idempotencyKey, cancellationToken);
        return identity is null ? null : ToRecord(identity);
    }

    public async Task<MigrationPersistenceResult<bool>> SetEvidenceStateAsync(
        TenantContext tenantContext,
        MigrationEvidenceReference reference,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(reference);

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [RunId] FROM [migration].[MigrationRuns] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantContext.TenantId.Value} AND [RunId] = {reference.RunId}", cancellationToken);
        var run = await db.Runs.SingleOrDefaultAsync(item => item.RunId == reference.RunId, cancellationToken);
        if (run is null)
        {
            return MigrationPersistenceResult<bool>.Denied(
                MigrationPersistenceOutcome.NotFound,
                "migration_run_not_found");
        }

        if (reference.Operation is { } operation && !string.IsNullOrWhiteSpace(reference.IdempotencyKey))
        {
            var identity = await db.Idempotency.SingleOrDefaultAsync(
                item => item.Operation == operation && item.IdempotencyKey == reference.IdempotencyKey,
                cancellationToken);
            if (identity is null || identity.RunId != reference.RunId)
            {
                return MigrationPersistenceResult<bool>.Denied(
                    MigrationPersistenceOutcome.NotFound,
                    "migration_idempotency_not_found");
            }

            if (reference.AttemptId is { } expectedAttempt && identity.AttemptId != expectedAttempt)
            {
                return MigrationPersistenceResult<bool>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_attempt_identity_mismatch");
            }

            if (identity.EvidenceConfirmed != confirmed)
                identity.SetEvidenceConfirmed(confirmed);
            if (identity.AttemptId is { } identityAttemptId)
            {
                var attempt = await db.Attempts.SingleOrDefaultAsync(
                    item => item.RunId == reference.RunId && item.AttemptId == identityAttemptId,
                    cancellationToken);
                if (attempt is not null && attempt.EvidenceConfirmed != confirmed)
                    attempt.SetEvidenceConfirmed(confirmed);
            }
        }

        if (reference.AttemptId is { } attemptId)
        {
            var attempt = await db.Attempts.SingleOrDefaultAsync(
                item => item.RunId == reference.RunId && item.AttemptId == attemptId,
                cancellationToken);
            if (attempt is null)
            {
                return MigrationPersistenceResult<bool>.Denied(
                    MigrationPersistenceOutcome.NotFound,
                    "migration_attempt_not_found");
            }

            if (attempt.EvidenceConfirmed != confirmed)
                attempt.SetEvidenceConfirmed(confirmed);
        }

        if (run.EvidenceConfirmed != confirmed)
            run.SetEvidenceConfirmed(confirmed);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MigrationPersistenceResult<bool>.Success(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return MigrationPersistenceResult<bool>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_evidence_version_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<bool>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_evidence_outcome_unknown");
        }
    }

    /// <summary>
    /// Resolves a run replay from a committed idempotency row.
    /// </summary>
    private async Task<MigrationPersistenceResult<MigrationRunRecord>> ResolveRunReplayAsync(
        TenantContext tenantContext,
        MigrationIdempotencyEntity existingKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existingKey.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_conflict");
        }

        if (!await WaitForEvidenceConfirmationAsync(
            tenantContext,
            existingKey,
            existingKey.RunId,
            cancellationToken))
        {
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_audit_recovery_required");
        }

        var replayRun = await FindRunAsync(tenantContext, existingKey.RunId, cancellationToken);
        return replayRun is null
            ? MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_orphaned")
            : MigrationPersistenceResult<MigrationRunRecord>.Replay(replayRun);
    }

    /// <summary>
    /// Reads the race winner's committed rows through a fresh context and
    /// applies normal replay semantics.
    /// </summary>
    private async Task<MigrationPersistenceResult<MigrationRunRecord>> ResolveConcurrentRunAsync(
        TenantContext tenantContext,
        MigrationOperationKind operation,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        await using var fresh = CreateContext(tenantContext);
        var concurrent = await FindIdempotencyEntityAsync(fresh, operation, idempotencyKey, cancellationToken);
        if (concurrent is null)
        {
            // The uniqueness violation was not on the idempotency key and no
            // winner is visible, so the outcome cannot be proved.
            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_run_outcome_unknown");
        }

        return await ResolveRunReplayAsync(tenantContext, concurrent, requestFingerprint, cancellationToken);
    }

    private async Task<MigrationPersistenceResult<MigrationIntakeRecord>> ResolveIntakeReplayAsync(
        TenantContext tenantContext,
        MigrationIntakeEntity existingIntake,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existingIntake.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        var run = await db.Runs.SingleOrDefaultAsync(item => item.RunId == existingIntake.RunId, cancellationToken);
        if (run is not null
            && !await WaitForEvidenceConfirmationAsync(
                tenantContext,
                existingIntake.Operation,
                existingIntake.IdempotencyKey,
                existingIntake.RunId,
                cancellationToken))
        {
            return MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_audit_recovery_required");
        }

        return run is null
            ? MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_orphaned")
            : MigrationPersistenceResult<MigrationIntakeRecord>.Replay(ToRecord(existingIntake, run));
    }

    private async Task<MigrationPersistenceResult<MigrationIntakeRecord>> ResolveConcurrentIntakeAsync(
        TenantContext tenantContext,
        MigrationOperationKind operation,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        await using var fresh = CreateContext(tenantContext);
        var concurrentIntake = await fresh.Intakes.SingleOrDefaultAsync(
            item => item.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (concurrentIntake is not null)
        {
            return await ResolveIntakeReplayAsync(tenantContext, concurrentIntake, requestFingerprint, cancellationToken);
        }

        var concurrentKey = await FindIdempotencyEntityAsync(fresh, operation, idempotencyKey, cancellationToken);
        return concurrentKey is null
            ? MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_intake_outcome_unknown")
            : await ResolveExistingIntakeKeyAsync(
                tenantContext,
                concurrentKey,
                requestFingerprint,
                cancellationToken);
    }

    private async Task<MigrationPersistenceResult<MigrationIntakeRecord>> ResolveExistingIntakeKeyAsync(
        TenantContext tenantContext,
        MigrationIdempotencyEntity existingKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existingKey.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_conflict");
        }

        // A provider can expose the idempotency key at the end of a unique-key
        // race before a concurrent intake read observes its sibling row. Three
        // short fresh reads keep identical callers replayable without treating
        // an unrelated run-only key as an intake.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var fresh = CreateContext(tenantContext);
            var concurrentIntake = await fresh.Intakes.SingleOrDefaultAsync(
                item => item.RunId == existingKey.RunId,
                cancellationToken);
            if (concurrentIntake is not null)
            {
                return await ResolveIntakeReplayAsync(
                    tenantContext,
                    concurrentIntake,
                    requestFingerprint,
                    cancellationToken);
            }

            if (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            }
        }

        return MigrationPersistenceResult<MigrationIntakeRecord>.Denied(
            MigrationPersistenceOutcome.Conflict,
            "migration_idempotency_conflict");
    }

    /// <summary>
    /// Resolves an attempt replay from a committed idempotency row.
    /// </summary>
    private async Task<MigrationPersistenceResult<MigrationAttemptRecord>> ResolveAttemptReplayAsync(
        TenantContext tenantContext,
        MigrationIdempotencyEntity existingKey,
        StartMigrationAttemptCommand command,
        CancellationToken cancellationToken)
    {
        if (existingKey.RunId != command.RunId
            || !string.Equals(existingKey.RequestFingerprint, command.RequestFingerprint.Value, StringComparison.Ordinal))
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_conflict");
        }

        if (!await WaitForEvidenceConfirmationAsync(
            tenantContext,
            existingKey,
            existingKey.RunId,
            cancellationToken))
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_audit_recovery_required");
        }

        if (existingKey.AttemptId is not { } existingAttemptId)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_run_key_reuse");
        }

        var replayAttempt = await FindAttemptAsync(
            tenantContext,
            command.RunId,
            existingAttemptId,
            cancellationToken);
        return replayAttempt is null
            ? MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_idempotency_orphaned")
            : !replayAttempt.EvidenceConfirmed
                ? MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                    MigrationPersistenceOutcome.UnknownOutcome,
                    "migration_audit_recovery_required")
            : MigrationPersistenceResult<MigrationAttemptRecord>.Replay(replayAttempt);
    }

    /// <summary>
    /// Reads the race winner's committed attempt through a fresh context.
    /// </summary>
    private async Task<MigrationPersistenceResult<MigrationAttemptRecord>> ResolveConcurrentAttemptAsync(
        TenantContext tenantContext,
        StartMigrationAttemptCommand command,
        CancellationToken cancellationToken)
    {
        await using var fresh = CreateContext(tenantContext);
        var concurrent = await FindIdempotencyEntityAsync(
            fresh,
            command.Operation,
            command.IdempotencyKey.Value,
            cancellationToken);
        if (concurrent is not null)
        {
            return await ResolveAttemptReplayAsync(tenantContext, concurrent, command, cancellationToken);
        }

        if (command.Operation == MigrationOperationKind.Execution)
        {
            // A unique sequence violation means another execution attempt has
            // already claimed this run. Do not create a second attempt: the
            // pre-effect execution claim is the authority that elects the
            // winner, and recursive recovery could finalize a loser while the
            // winner was transitioning the run's evidence state.
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_execution_attempt_claim_conflict");
        }

        // The idempotency key is free, so the violation was the Tenant-scoped
        // (run, sequence) uniqueness guard: a concurrent caller committed the
        // sequence this caller derived. That is a genuine lineage race, and it
        // is a retry-safe conflict rather than an unproven outcome because
        // nothing of this caller's was committed.
        return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
            MigrationPersistenceOutcome.Conflict,
            "migration_attempt_lineage_race");
    }

    /// <summary>
    /// Classifies a lineage refusal that may be a stale read of this same
    /// request's concurrently committed attempt.
    /// </summary>
    private async Task<MigrationPersistenceResult<MigrationAttemptRecord>> ResolveLineageDenialAsync(
        TenantContext tenantContext,
        StartMigrationAttemptCommand command,
        string denialCode,
        CancellationToken cancellationToken)
    {
        await using var fresh = CreateContext(tenantContext);
        var concurrent = await FindIdempotencyEntityAsync(
            fresh,
            command.Operation,
            command.IdempotencyKey.Value,
            cancellationToken);
        return concurrent is null
            ? MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                denialCode)
            : await ResolveAttemptReplayAsync(tenantContext, concurrent, command, cancellationToken);
    }

    private static Task<MigrationIdempotencyEntity?> FindIdempotencyEntityAsync(
        MigrationDbContext db,
        MigrationOperationKind operation,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        db.Idempotency.SingleOrDefaultAsync(
            item => item.Operation == operation && item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private async Task<bool> WaitForEvidenceConfirmationAsync(
        TenantContext tenantContext,
        MigrationIdempotencyEntity existing,
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (existing.EvidenceConfirmed)
            return true;

        return await WaitForEvidenceConfirmationAsync(
            tenantContext,
            existing.Operation,
            existing.IdempotencyKey,
            runId,
            cancellationToken);
    }

    private async Task<bool> WaitForEvidenceConfirmationAsync(
        TenantContext tenantContext,
        MigrationOperationKind operation,
        string idempotencyKey,
        Guid runId,
        CancellationToken cancellationToken)
    {
        // Give the winning application call a bounded window to append and
        // confirm its audit evidence before classifying a concurrent replay
        // as genuinely unproved.
        for (var retry = 0; retry < 40; retry++)
        {
            await using var fresh = CreateContext(tenantContext);
            var current = await FindIdempotencyEntityAsync(fresh, operation, idempotencyKey, cancellationToken);
            if (current?.RunId == runId && current.EvidenceConfirmed)
                return true;

            if (current is not null && current.RunId != runId)
                return false;

            if (retry < 39)
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        return false;
    }

    private static async Task<IReadOnlyList<MigrationAttemptRecord>> ReadAttemptsAsync(
        MigrationDbContext db,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var attempts = await db.Attempts
            .Where(item => item.RunId == runId)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        return attempts.Select(ToRecord).ToList();
    }

    /// <summary>
    /// Classifies a failed write as a uniqueness violation.
    /// </summary>
    /// <remarks>
    /// Only a uniqueness race can be safely resolved into an idempotent
    /// replay. Treating every <see cref="DbUpdateException"/> as a duplicate
    /// would report unrelated infrastructure failures as business conflicts and
    /// would claim, wrongly, that a competing record exists.
    /// </remarks>
    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                // SQL Server: 2627 unique constraint, 2601 unique index.
                case Microsoft.Data.SqlClient.SqlException sql
                    when sql.Number is 2627 or 2601:
                    return true;

                // SQLite: only the primary-key and unique-index subcodes are
                // replay-safe. Code 19 is generic and also covers FK, CHECK,
                // NOT NULL and other non-unique constraint failures.
                case Microsoft.Data.Sqlite.SqliteException sqlite
                    when sqlite.SqliteExtendedErrorCode is 1555 or 2067:
                    return true;
            }
        }

        return false;
    }

    private MigrationDbContext CreateContext(TenantContext tenantContext) => new(options, tenantContext);

    private static MigrationRunRecord ToRecord(MigrationRunEntity entity) => new(
        entity.RunId,
        entity.TenantId,
        entity.ActorId,
        new CorrelationId(entity.CorrelationId),
        new MigrationDefinitionReference(entity.DefinitionId, entity.DefinitionVersion),
        new MigrationSourceProfileReference(entity.SourceProfileId, entity.SourceProfileVersion),
        entity.Status,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.Version,
        entity.EvidenceConfirmed);

    private static MigrationAttemptRecord ToRecord(MigrationAttemptEntity entity) => new(
        entity.AttemptId,
        entity.TenantId,
        entity.RunId,
        entity.Sequence,
        entity.PreviousAttemptId,
        entity.Operation,
        entity.Outcome,
        entity.IdempotencyKey,
        entity.RequestFingerprint,
        entity.StartedAt,
        entity.FinishedAt,
        entity.SafeOutcomeCode,
        entity.Version,
        entity.EvidenceConfirmed);

    private static MigrationIdempotencyRecord ToRecord(MigrationIdempotencyEntity entity) => new(
        entity.TenantId.Value,
        entity.RunId,
        entity.Operation,
        entity.IdempotencyKey,
        entity.RequestFingerprint,
        entity.AttemptId,
        entity.ResultKind,
        entity.ResultCode,
        entity.CreatedAt,
        entity.Version,
        entity.EvidenceConfirmed);

    private static MigrationIntakeRecord ToRecord(
        MigrationIntakeEntity intake,
        MigrationRunEntity run) => new(
        ToRecord(run),
        intake.Operation,
        intake.FingerprintVersion,
        intake.RequestFingerprint,
        new MigrationSourceArtifactSnapshot(
            intake.SourceObjectId,
            intake.SourceTenantId,
            intake.SourceCompanyId,
            intake.SourceBranchId,
            intake.SourceWarehouseId,
            intake.SourceSha256,
            intake.SourceLength,
            intake.SourceConcurrencyVersion),
        intake.CapturedAt,
        intake.Version);
}

#pragma warning restore CS1591
