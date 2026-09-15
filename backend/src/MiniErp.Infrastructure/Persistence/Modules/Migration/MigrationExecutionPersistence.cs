#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

internal sealed partial class MigrationPersistence
{
    public async Task<MigrationPersistenceResult<MigrationExecutionBatchRecord>> CreateBatchAsync(
        TenantContext tenantContext,
        CreateMigrationExecutionBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);
        var record = command.Batch;
        if (record.TenantId != tenantContext.TenantId
            || record.Id == Guid.Empty
            || record.RunId == Guid.Empty
            || record.AttemptId == Guid.Empty
            || record.OwnerBatchId == Guid.Empty
            || string.IsNullOrWhiteSpace(record.Fingerprint))
        {
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                "migration_execution_reference_invalid");
        }

        await using var db = CreateContext(tenantContext);
        var existing = await db.ExecutionBatches.SingleOrDefaultAsync(
            item => item.TenantId == record.TenantId
                && item.RunId == record.RunId
                && item.AttemptId == record.AttemptId
                && item.RecordType == record.RecordType,
            cancellationToken);
        if (existing is not null)
        {
            return SameBatch(existing, record)
                ? MigrationPersistenceResult<MigrationExecutionBatchRecord>.Replay(ToRecord(existing))
                : MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_batch_conflict");
        }

        db.ExecutionBatches.Add(new MigrationExecutionBatchEntity(record));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var saved = await db.ExecutionBatches.SingleAsync(item => item.Id == record.Id, cancellationToken);
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Success(ToRecord(saved));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await using var fresh = CreateContext(tenantContext);
            var winner = await fresh.ExecutionBatches.SingleOrDefaultAsync(
                item => item.TenantId == record.TenantId
                    && item.RunId == record.RunId
                    && item.AttemptId == record.AttemptId
                    && item.RecordType == record.RecordType,
                cancellationToken);
            return winner is not null && SameBatch(winner, record)
                ? MigrationPersistenceResult<MigrationExecutionBatchRecord>.Replay(ToRecord(winner))
                : MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_execution_batch_outcome_unknown");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_execution_batch_outcome_unknown");
        }
    }

    public async Task<MigrationExecutionBatchRecord?> FindBatchAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        MigrationCanonicalRecordType recordType,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ExecutionBatches.SingleOrDefaultAsync(
            item => item.RunId == runId && item.AttemptId == attemptId && item.RecordType == recordType,
            cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<MigrationExecutionBatchRecord>> ListBatchesAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.ExecutionBatches
            .Where(item => item.RunId == runId && item.AttemptId == attemptId)
            .OrderBy(item => item.RecordType)
            .ToListAsync(cancellationToken);
        return entities.Select(ToRecord).ToList();
    }

    public async Task<MigrationPersistenceResult<MigrationExecutionBatchRecord>> UpdateBatchAsync(
        TenantContext tenantContext,
        UpdateMigrationExecutionBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ExecutionBatches.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (entity is null)
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.NotFound, "migration_execution_batch_not_found");
        if (entity.State == command.State)
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Replay(ToRecord(entity));
        if (!entity.Version.AsSpan().SequenceEqual(command.ExpectedVersion))
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_batch_version_conflict");
        if (!entity.Apply(command.State, command.StartedAt, command.CompletedAt))
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_batch_state_conflict");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_batch_version_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await using var fresh = CreateContext(tenantContext);
            var active = await fresh.ExecutionBatches.AnyAsync(
                item => item.RunId == entity.RunId
                    && item.RecordType == entity.RecordType
                    && (item.State == MigrationExecutionBatchState.Started
                        || item.State == MigrationExecutionBatchState.Completed),
                cancellationToken);
            return active
                ? MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_execution_batch_claim_conflict")
                : MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(
                    MigrationPersistenceOutcome.UnknownOutcome,
                    "migration_execution_batch_outcome_unknown");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationExecutionBatchRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_execution_batch_outcome_unknown");
        }
    }

    public async Task<MigrationPersistenceResult<MigrationExecutionEffectRecord>> CreateEffectAsync(
        TenantContext tenantContext,
        CreateMigrationExecutionEffectCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);
        var record = command.Effect;
        if (record.TenantId != tenantContext.TenantId
            || record.Id == Guid.Empty
            || record.RunId == Guid.Empty
            || record.AttemptId == Guid.Empty
            || record.StagedRecordId == Guid.Empty
            || record.OwnerBatchId == Guid.Empty)
        {
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.InvalidReference, "migration_execution_reference_invalid");
        }

        await using var db = CreateContext(tenantContext);
        var existing = await db.ExecutionEffects.SingleOrDefaultAsync(item =>
            item.TenantId == record.TenantId
            && item.RunId == record.RunId
            && item.AttemptId == record.AttemptId
            && item.StagedRecordId == record.StagedRecordId, cancellationToken);
        if (existing is not null)
            return SameEffect(existing, record)
                ? MigrationPersistenceResult<MigrationExecutionEffectRecord>.Replay(ToRecord(existing))
                : MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_effect_conflict");

        db.ExecutionEffects.Add(new MigrationExecutionEffectEntity(record));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var saved = await db.ExecutionEffects.SingleAsync(item => item.Id == record.Id, cancellationToken);
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Success(ToRecord(saved));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await using var fresh = CreateContext(tenantContext);
            var winner = await fresh.ExecutionEffects.SingleOrDefaultAsync(item =>
                item.TenantId == record.TenantId
                && item.RunId == record.RunId
                && item.AttemptId == record.AttemptId
                && item.StagedRecordId == record.StagedRecordId, cancellationToken);
            return winner is not null && SameEffect(winner, record)
                ? MigrationPersistenceResult<MigrationExecutionEffectRecord>.Replay(ToRecord(winner))
                : MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_execution_effect_outcome_unknown");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_execution_effect_outcome_unknown");
        }
    }

    public async Task<IReadOnlyList<MigrationExecutionEffectRecord>> ListEffectsAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.ExecutionEffects
            .Where(item => item.RunId == runId && item.AttemptId == attemptId)
            .OrderBy(item => item.SourceSequence)
            .ToListAsync(cancellationToken);
        return entities.Select(ToRecord).ToList();
    }

    public async Task<MigrationPersistenceResult<MigrationExecutionEffectRecord>> UpdateEffectAsync(
        TenantContext tenantContext,
        UpdateMigrationExecutionEffectCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ExecutionEffects.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (entity is null)
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.NotFound, "migration_execution_effect_not_found");
        if (entity.Disposition == command.Disposition)
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Replay(ToRecord(entity));
        if (!entity.Version.AsSpan().SequenceEqual(command.ExpectedVersion))
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_effect_version_conflict");
        if (!entity.Apply(command))
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_effect_state_conflict");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_execution_effect_version_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationExecutionEffectRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_execution_effect_outcome_unknown");
        }
    }

    private static bool SameBatch(MigrationExecutionBatchEntity entity, MigrationExecutionBatchRecord record) =>
        entity.Id == record.Id
        && entity.OwnerBatchId == record.OwnerBatchId
        && entity.RecordType == record.RecordType
        && string.Equals(entity.Fingerprint, record.Fingerprint, StringComparison.Ordinal);

    private static bool SameEffect(MigrationExecutionEffectEntity entity, MigrationExecutionEffectRecord record) =>
        entity.Id == record.Id
        && entity.StagedRecordId == record.StagedRecordId
        && entity.OwnerBatchId == record.OwnerBatchId
        && entity.RecordType == record.RecordType;

    private static MigrationExecutionBatchRecord ToRecord(MigrationExecutionBatchEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.RunId,
        entity.AttemptId,
        entity.RecordType,
        entity.State,
        entity.OwnerBatchId,
        entity.Fingerprint,
        entity.CreatedAt,
        entity.StartedAt,
        entity.CompletedAt,
        entity.CorrelationId,
        entity.Version);

    private static MigrationExecutionEffectRecord ToRecord(MigrationExecutionEffectEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.RunId,
        entity.AttemptId,
        entity.StagedRecordId,
        entity.SourceSequence,
        entity.RecordType,
        entity.OwnerBatchId,
        entity.OwnerRowId,
        entity.ResultingResourceId,
        entity.ResultingResourceCode,
        entity.Disposition,
        entity.SafeCode,
        entity.CreatedAt,
        entity.EffectStartedAt,
        entity.CompletedAt,
        entity.CorrelationId,
        entity.Version);
}
