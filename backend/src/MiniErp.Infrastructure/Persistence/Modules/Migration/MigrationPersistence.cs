#pragma warning disable CS1591

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
internal sealed class MigrationPersistence : IMigrationFoundationPersistence
{
    private readonly DbContextOptions options;

    internal MigrationPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
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
        var existingKey = await db.Idempotency.SingleOrDefaultAsync(
            item => item.Operation == command.Operation
                && item.IdempotencyKey == command.IdempotencyKey.Value,
            cancellationToken);
        if (existingKey is not null)
        {
            if (!string.Equals(existingKey.RequestFingerprint, command.RequestFingerprint.Value, StringComparison.Ordinal))
            {
                return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_idempotency_conflict");
            }

            var replayRun = await db.Runs.SingleOrDefaultAsync(
                item => item.RunId == existingKey.RunId,
                cancellationToken);
            return replayRun is null
                ? MigrationPersistenceResult<MigrationRunRecord>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_idempotency_orphaned")
                : MigrationPersistenceResult<MigrationRunRecord>.Replay(ToRecord(replayRun));
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
        catch (DbUpdateException)
        {
            var concurrent = await db.Idempotency.SingleOrDefaultAsync(
                item => item.Operation == command.Operation
                    && item.IdempotencyKey == command.IdempotencyKey.Value,
                cancellationToken);
            if (concurrent is not null
                && string.Equals(concurrent.RequestFingerprint, command.RequestFingerprint.Value, StringComparison.Ordinal))
            {
                var concurrentRun = await db.Runs.SingleOrDefaultAsync(item => item.RunId == concurrent.RunId, cancellationToken);
                if (concurrentRun is not null)
                {
                    return MigrationPersistenceResult<MigrationRunRecord>.Replay(ToRecord(concurrentRun));
                }
            }

            return MigrationPersistenceResult<MigrationRunRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_duplicate");
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

    public async Task<MigrationPersistenceResult<MigrationAttemptRecord>> CreateAttemptAsync(
        TenantContext tenantContext,
        CreateMigrationAttemptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);
        var attempt = command.Attempt;
        if (attempt.TenantId != tenantContext.TenantId)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                "migration_tenant_context_mismatch");
        }

        await using var db = CreateContext(tenantContext);
        var runExists = await db.Runs.AnyAsync(item => item.RunId == attempt.RunId, cancellationToken);
        if (!runExists)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.NotFound,
                "migration_run_not_found");
        }

        var existingKey = await db.Idempotency.SingleOrDefaultAsync(
            item => item.Operation == attempt.Operation
                && item.IdempotencyKey == attempt.IdempotencyKey.Value,
            cancellationToken);
        if (existingKey is not null)
        {
            if (existingKey.RunId != attempt.RunId
                || !string.Equals(existingKey.RequestFingerprint, attempt.RequestFingerprint.Value, StringComparison.Ordinal))
            {
                return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_idempotency_conflict");
            }

            if (existingKey.AttemptId is not { } existingAttemptId)
            {
                return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_idempotency_run_key_reuse");
            }

            var replayAttempt = await db.Attempts.SingleOrDefaultAsync(item => item.AttemptId == existingAttemptId, cancellationToken);
            return replayAttempt is null
                ? MigrationPersistenceResult<MigrationAttemptRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_idempotency_orphaned")
                : MigrationPersistenceResult<MigrationAttemptRecord>.Replay(ToRecord(replayAttempt));
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
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationAttemptRecord>.Denied(
                MigrationPersistenceOutcome.Conflict,
                "migration_attempt_duplicate");
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
        var identity = await db.Idempotency.SingleOrDefaultAsync(
            item => item.Operation == operation && item.IdempotencyKey == idempotencyKey,
            cancellationToken);
        return identity is null ? null : ToRecord(identity);
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
        entity.Version);

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
        entity.Version);

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
        entity.Version);
}

#pragma warning restore CS1591
