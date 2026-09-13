#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record CreateMasterDataCategoryCommand(
    string Code,
    LocalizedName Name,
    Guid? ParentCategoryId = null,
    bool TrackingDefaultEnabled = false);

public sealed record EditMasterDataCategoryCommand(
    Guid CategoryId,
    string Code,
    LocalizedName Name,
    Guid? ParentCategoryId,
    byte[] ExpectedVersion,
    bool TrackingDefaultEnabled = false);

public sealed record CreateMasterDataUnitOfMeasureCommand(
    string Code,
    LocalizedName Name);

public sealed record EditMasterDataUnitOfMeasureCommand(
    Guid UnitOfMeasureId,
    string Code,
    LocalizedName Name,
    byte[] ExpectedVersion);

public sealed record CreateMasterDataConversionCommand(
    Guid FromUnitOfMeasureId,
    Guid ToUnitOfMeasureId,
    decimal Factor);

public sealed record MasterDataCategoryRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName Name,
    Guid? ParentCategoryId,
    MasterDataLifecycleState LifecycleState,
    byte[] Version,
    bool TrackingDefaultEnabled = false);

public sealed record MasterDataUnitOfMeasureRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName Name,
    MasterDataLifecycleState LifecycleState,
    byte[] Version);

public sealed record MasterDataConversionRecord(
    Guid Id,
    TenantId TenantId,
    Guid FromUnitOfMeasureId,
    Guid ToUnitOfMeasureId,
    decimal Factor,
    byte[] Version);

public sealed record MasterDataAuditRecord(
    Guid EvidenceId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    TenantId TenantId,
    Guid ActorId,
    Guid SessionId,
    TenantAuthorizationPath AuthorizationPath,
    MasterDataResourceKind ResourceKind,
    Guid? ResourceId,
    string? BusinessCode,
    BusinessScope? Scope,
    MasterDataOperation Operation,
    MasterDataPolicyOutcome PolicyOutcome,
    FoundationAuditDecision Decision,
    FoundationAuditReason Reason,
    string? BeforeSummary,
    string? AfterSummary,
    Guid? ApproverId);

public enum MasterDataPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Duplicate = 3,
    Conflict = 4,
    InvalidReference = 5,
    InUse = 6,
    AuditFailure = 7,
    Failure = 8
}

public sealed record MasterDataPersistenceResult<T>(
    MasterDataPersistenceOutcome Outcome,
    string Code,
    T? Value)
{
    public bool Succeeded => Outcome == MasterDataPersistenceOutcome.Succeeded;

    public static MasterDataPersistenceResult<T> Success(T value) =>
        new(MasterDataPersistenceOutcome.Succeeded, "persisted", value);

    public static MasterDataPersistenceResult<T> Denied(
        MasterDataPersistenceOutcome outcome,
        string code) => new(outcome, code, default);
}

public sealed record MasterDataQuantityConversionResult(
    bool Succeeded,
    string Code,
    decimal? Quantity);

public interface IMasterDataCatalogPersistence
{
    Task<IReadOnlyList<MasterDataCategoryRecord>> ListCategoriesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<MasterDataCategoryRecord?> FindCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> CreateCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CreateMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> EditCategoryAsync(
        TenantContext tenantContext,
        EditMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> SetCategoryLifecycleAsync(
        TenantContext tenantContext,
        Guid categoryId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataUnitOfMeasureRecord>> ListUnitsOfMeasureAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<MasterDataUnitOfMeasureRecord?> FindUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default);

    Task<MasterDataConversionRecord?> FindConversionAsync(
        TenantContext tenantContext,
        Guid fromUnitOfMeasureId,
        Guid toUnitOfMeasureId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> CreateUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CreateMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> EditUnitOfMeasureAsync(
        TenantContext tenantContext,
        EditMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> SetUnitOfMeasureLifecycleAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataConversionRecord>> CreateConversionAsync(
        TenantContext tenantContext,
        Guid conversionId,
        CreateMasterDataConversionCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataQuantityConversionResult> ConvertQuantityAsync(
        TenantContext tenantContext,
        Guid conversionId,
        decimal quantity,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveConversionReferenceAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        MasterDataResourceKind resourceKind,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fail-closed composition default used until the host supplies the approved
/// module-owned persistence provider. It keeps the REST surface startable
/// without making an unavailable store look like an empty catalogue.
/// </summary>
public sealed class UnavailableMasterDataCatalogPersistence : IMasterDataCatalogPersistence
{
    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Master Data persistence is unavailable."));

    public Task<IReadOnlyList<MasterDataCategoryRecord>> ListCategoriesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default) =>
        Unavailable<IReadOnlyList<MasterDataCategoryRecord>>();

    public Task<MasterDataCategoryRecord?> FindCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataCategoryRecord?>();

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> CreateCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CreateMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataCategoryRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> EditCategoryAsync(
        TenantContext tenantContext,
        EditMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataCategoryRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> SetCategoryLifecycleAsync(
        TenantContext tenantContext,
        Guid categoryId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataCategoryRecord>>();

    public Task<IReadOnlyList<MasterDataUnitOfMeasureRecord>> ListUnitsOfMeasureAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default) =>
        Unavailable<IReadOnlyList<MasterDataUnitOfMeasureRecord>>();

    public Task<MasterDataUnitOfMeasureRecord?> FindUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataUnitOfMeasureRecord?>();

    public Task<MasterDataConversionRecord?> FindConversionAsync(
        TenantContext tenantContext,
        Guid fromUnitOfMeasureId,
        Guid toUnitOfMeasureId,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataConversionRecord?>();

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> CreateUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CreateMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> EditUnitOfMeasureAsync(
        TenantContext tenantContext,
        EditMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> SetUnitOfMeasureLifecycleAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataConversionRecord>> CreateConversionAsync(
        TenantContext tenantContext,
        Guid conversionId,
        CreateMasterDataConversionCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataConversionRecord>>();

    public Task<MasterDataQuantityConversionResult> ConvertQuantityAsync(
        TenantContext tenantContext,
        Guid conversionId,
        decimal quantity,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataQuantityConversionResult>();

    public Task<bool> HasActiveConversionReferenceAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default) =>
        Unavailable<bool>();

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();

    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        MasterDataResourceKind resourceKind,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default) =>
        Unavailable<IReadOnlyList<MasterDataAuditRecord>>();
}

#pragma warning restore CS1591
