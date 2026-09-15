#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

public enum MigrationExecutionBatchState
{
    Prepared = 1,
    Started = 2,
    Completed = 3,
    Failed = 4,
    Unknown = 5
}

public enum MigrationExecutionEffectDisposition
{
    Prepared = 1,
    NonEffect = 2,
    Started = 3,
    Committed = 4,
    Failed = 5,
    Unknown = 6
}

public sealed record MigrationExecutionBatchRecord(
    Guid Id,
    TenantId TenantId,
    Guid RunId,
    Guid AttemptId,
    MigrationCanonicalRecordType RecordType,
    MigrationExecutionBatchState State,
    Guid OwnerBatchId,
    string Fingerprint,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string CorrelationId,
    byte[] Version);

public sealed record MigrationExecutionEffectRecord(
    Guid Id,
    TenantId TenantId,
    Guid RunId,
    Guid AttemptId,
    Guid StagedRecordId,
    int SourceSequence,
    MigrationCanonicalRecordType RecordType,
    Guid OwnerBatchId,
    Guid? OwnerRowId,
    Guid? ResultingResourceId,
    string? ResultingResourceCode,
    MigrationExecutionEffectDisposition Disposition,
    string? SafeCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EffectStartedAt,
    DateTimeOffset? CompletedAt,
    string CorrelationId,
    byte[] Version);

public sealed record MigrationExecutionResult(
    Guid RunId,
    TenantId TenantId,
    Guid AttemptId,
    string FingerprintVersion,
    string Fingerprint,
    MigrationRunStatus RunStatus,
    MigrationAttemptOutcome AttemptOutcome,
    string OutcomeCode,
    IReadOnlyList<MigrationExecutionBatchRecord> Batches,
    IReadOnlyList<MigrationExecutionEffectRecord> Effects);

public sealed record CreateMigrationExecutionBatchCommand(MigrationExecutionBatchRecord Batch);

public sealed record CreateMigrationExecutionEffectCommand(MigrationExecutionEffectRecord Effect);

public sealed record UpdateMigrationExecutionBatchCommand(
    Guid Id,
    MigrationExecutionBatchState State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    byte[] ExpectedVersion);

public sealed record UpdateMigrationExecutionEffectCommand(
    Guid Id,
    MigrationExecutionEffectDisposition Disposition,
    Guid? OwnerRowId,
    Guid? ResultingResourceId,
    string? ResultingResourceCode,
    string? SafeCode,
    DateTimeOffset? EffectStartedAt,
    DateTimeOffset? CompletedAt,
    byte[] ExpectedVersion);

public interface IMigrationExecutionPersistence
{
    Task<MigrationPersistenceResult<MigrationExecutionBatchRecord>> CreateBatchAsync(
        TenantContext tenantContext,
        CreateMigrationExecutionBatchCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationExecutionBatchRecord?> FindBatchAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        MigrationCanonicalRecordType recordType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MigrationExecutionBatchRecord>> ListBatchesAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    Task<MigrationPersistenceResult<MigrationExecutionBatchRecord>> UpdateBatchAsync(
        TenantContext tenantContext,
        UpdateMigrationExecutionBatchCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationPersistenceResult<MigrationExecutionEffectRecord>> CreateEffectAsync(
        TenantContext tenantContext,
        CreateMigrationExecutionEffectCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MigrationExecutionEffectRecord>> ListEffectsAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    Task<MigrationPersistenceResult<MigrationExecutionEffectRecord>> UpdateEffectAsync(
        TenantContext tenantContext,
        UpdateMigrationExecutionEffectCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableMigrationExecutionPersistence : IMigrationExecutionPersistence
{
    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Migration execution persistence is unavailable."));

    public Task<MigrationPersistenceResult<MigrationExecutionBatchRecord>> CreateBatchAsync(TenantContext tenantContext, CreateMigrationExecutionBatchCommand command, CancellationToken cancellationToken = default) => Unavailable<MigrationPersistenceResult<MigrationExecutionBatchRecord>>();
    public Task<MigrationExecutionBatchRecord?> FindBatchAsync(TenantContext tenantContext, Guid runId, Guid attemptId, MigrationCanonicalRecordType recordType, CancellationToken cancellationToken = default) => Unavailable<MigrationExecutionBatchRecord?>();
    public Task<IReadOnlyList<MigrationExecutionBatchRecord>> ListBatchesAsync(TenantContext tenantContext, Guid runId, Guid attemptId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MigrationExecutionBatchRecord>>();
    public Task<MigrationPersistenceResult<MigrationExecutionBatchRecord>> UpdateBatchAsync(TenantContext tenantContext, UpdateMigrationExecutionBatchCommand command, CancellationToken cancellationToken = default) => Unavailable<MigrationPersistenceResult<MigrationExecutionBatchRecord>>();
    public Task<MigrationPersistenceResult<MigrationExecutionEffectRecord>> CreateEffectAsync(TenantContext tenantContext, CreateMigrationExecutionEffectCommand command, CancellationToken cancellationToken = default) => Unavailable<MigrationPersistenceResult<MigrationExecutionEffectRecord>>();
    public Task<IReadOnlyList<MigrationExecutionEffectRecord>> ListEffectsAsync(TenantContext tenantContext, Guid runId, Guid attemptId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MigrationExecutionEffectRecord>>();
    public Task<MigrationPersistenceResult<MigrationExecutionEffectRecord>> UpdateEffectAsync(TenantContext tenantContext, UpdateMigrationExecutionEffectCommand command, CancellationToken cancellationToken = default) => Unavailable<MigrationPersistenceResult<MigrationExecutionEffectRecord>>();
}

internal sealed record MigrationExecutionPlanRow(
    MigrationStagedRecord Staged,
    MigrationPreviewRow Preview,
    MigrationParsedCanonicalRow Parsed);
