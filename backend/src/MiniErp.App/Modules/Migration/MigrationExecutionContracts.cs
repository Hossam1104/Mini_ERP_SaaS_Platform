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
    Unknown = 6,
    PartialCompleted = 7
}

public enum MigrationEconomicOwnerModule
{
    Inventory = 1,
    Finance = 2
}

public enum MigrationEconomicRepresentationKind
{
    InventoryOpening = 1,
    InventoryOpeningRow = 2,
    InventoryStockMovement = 3,
    InventoryValuationEvent = 4,
    InventoryFinanceHandoff = 5,
    FinanceJournal = 6,
    FinanceOpenItem = 7
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
    IReadOnlyList<MigrationExecutionEffectRecord> Effects,
    IReadOnlyList<MigrationEconomicRepresentationRecord>? Representations = null,
    IReadOnlyList<MigrationEconomicReconciliationRecord>? EconomicReconciliations = null,
    IReadOnlyList<MigrationArEconomicReconciliationRecord>? ArEconomicReconciliations = null,
    IReadOnlyList<MigrationApEconomicReconciliationRecord>? ApEconomicReconciliations = null);

public sealed record MigrationEconomicRepresentationRecord(
    Guid Id,
    TenantId TenantId,
    Guid RunId,
    Guid AttemptId,
    Guid EffectId,
    MigrationEconomicOwnerModule OwnerModule,
    MigrationEconomicRepresentationKind Kind,
    Guid OwnerId,
    string? OwnerReference,
    string Status,
    string EvidenceVersion,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    bool EvidenceConfirmed,
    byte[] Version);

public sealed record MigrationEconomicReconciliationRecord(
    Guid EffectId,
    int SourceSequence,
    string Status,
    string? SafeCode,
    bool PhysicalQuantityProven,
    bool ValuationAmountProven,
    bool FinanceAmountProven,
    decimal? CanonicalQuantity,
    decimal? InventoryQuantity,
    decimal? CanonicalValue,
    decimal? InventoryValue,
    decimal? DeclaredRoundingAdjustment,
    decimal? FinancePostedAmount,
    string? FunctionalCurrencyCode,
    DateTimeOffset ReconciledAt,
    int? InventoryUnitCostScale = null,
    int? InventoryAmountScale = null,
    string? InventoryRoundingMode = null);

public sealed record MigrationArEconomicReconciliationRecord(
    Guid EffectId,
    int SourceSequence,
    string Status,
    string? SafeCode,
    Guid CompanyId,
    Guid CustomerId,
    string SourceReference,
    decimal CanonicalAmount,
    decimal? OpenItemOriginalAmount,
    decimal? OutstandingAmount,
    decimal? RecognitionJournalAmount,
    decimal? AllocatedAmount,
    string CurrencyCode,
    Guid? OpenItemId,
    Guid? JournalId,
    DateTimeOffset ReconciledAt);

public sealed record MigrationApEconomicReconciliationRecord(
    Guid EffectId,
    int SourceSequence,
    string Status,
    string? SafeCode,
    Guid CompanyId,
    Guid SupplierId,
    string SourceReference,
    decimal CanonicalAmount,
    decimal? OpenItemOriginalAmount,
    decimal? OutstandingAmount,
    decimal? RecognitionJournalAmount,
    decimal? AllocatedAmount,
    string CurrencyCode,
    Guid? OpenItemId,
    Guid? JournalId,
    DateTimeOffset ReconciledAt);

public sealed record CreateMigrationEconomicRepresentationCommand(MigrationEconomicRepresentationRecord Representation);

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

    Task<MigrationPersistenceResult<MigrationEconomicRepresentationRecord>> CreateRepresentationAsync(
        TenantContext tenantContext,
        CreateMigrationEconomicRepresentationCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MigrationEconomicRepresentationRecord>> ListRepresentationsAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
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
    public Task<MigrationPersistenceResult<MigrationEconomicRepresentationRecord>> CreateRepresentationAsync(TenantContext tenantContext, CreateMigrationEconomicRepresentationCommand command, CancellationToken cancellationToken = default) => Unavailable<MigrationPersistenceResult<MigrationEconomicRepresentationRecord>>();
    public Task<IReadOnlyList<MigrationEconomicRepresentationRecord>> ListRepresentationsAsync(TenantContext tenantContext, Guid runId, Guid attemptId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MigrationEconomicRepresentationRecord>>();
}

internal sealed record MigrationExecutionPlanRow(
    MigrationStagedRecord Staged,
    MigrationPreviewRow Preview,
    MigrationParsedCanonicalRow Parsed);
