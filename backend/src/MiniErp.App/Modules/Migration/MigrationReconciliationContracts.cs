#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

public enum MigrationReconciliationDomain { Inventory = 1, Ar = 2, Ap = 3, CashBank = 4, Gl = 5 }
public enum MigrationReconciliationStatus { Reconciled = 1, Blocked = 2 }
public enum MigrationApprovalDecision { Approved = 1, Rejected = 2 }

public sealed record MigrationReconciliationDetail(
    Guid Id = default,
    MigrationReconciliationDomain Domain = default,
    string ScopeKey = "",
    Guid? CompanyId = null,
    DateOnly? OpeningDate = null,
    string? CurrencyCode = null,
    string? TransactionCurrencyCode = null,
    string? FunctionalCurrencyCode = null,
    int SourceCount = 0,
    decimal? SourceDebit = null,
    decimal? SourceCredit = null,
    decimal? TargetDebit = null,
    decimal? TargetCredit = null,
    decimal? Variance = null,
    decimal? SourceAmount = null,
    decimal? TargetAmount = null,
    decimal? AmountVariance = null,
    decimal? OwnerRoundingDifference = null,
    decimal? TransactionAmount = null,
    decimal? FunctionalAmount = null,
    decimal? SubsidiaryEstablishedAmount = null,
    decimal? GlControlAmount = null,
    Guid? ExchangeRateId = null,
    Guid? ExchangeRateVersionId = null,
    int? ExchangeRateVersionNumber = null,
    decimal? AppliedRate = null,
    decimal? SourceQuantity = null,
    decimal? TargetQuantity = null,
    decimal? QuantityVariance = null,
    Guid? ControlAccountId = null,
    Guid? PostingRuleId = null,
    int? PostingRuleVersionNumber = null,
    Guid? OwnerSourceId = null,
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    Guid? UnitOfMeasureId = null,
    string? SourceContract = null,
    string? SourceEvent = null,
    Guid? RoundingPolicyId = null,
    int? RoundingPolicyVersionNumber = null,
    int? RoundingScale = null,
    string? RoundingMode = null,
    bool IsBlocking = false,
    string? FindingCode = null,
    string? Explanation = null,
    Guid? EffectId = null,
    Guid? OwnerReferenceId = null,
    Guid? LinkedAccountId = null);

public sealed record MigrationReconciliationApprovalRequirement(
    string Domain,
    string RequirementKey,
    int RequiredCount,
    IReadOnlyList<Guid> EligibleActorIds);

public sealed record MigrationReconciliationApprovalPolicy(
    string PolicyId,
    int Version,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool EnforceSeparationOfDuties,
    IReadOnlyList<MigrationReconciliationApprovalRequirement> Requirements);

public interface IMigrationReconciliationApprovalPolicy
{
    Task<MigrationReconciliationApprovalPolicy?> ResolveAsync(TenantContext tenant, Guid runId, DateTimeOffset at, CancellationToken cancellationToken = default);
}

public sealed class UnconfiguredMigrationReconciliationApprovalPolicy : IMigrationReconciliationApprovalPolicy
{
    public Task<MigrationReconciliationApprovalPolicy?> ResolveAsync(TenantContext tenant, Guid runId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult<MigrationReconciliationApprovalPolicy?>(null);
}

public sealed record MigrationReconciliationApprovalRecord(
    Guid Id,
    TenantId TenantId,
    Guid RunId,
    Guid ReconciliationId,
    int ReconciliationVersion,
    Guid AttemptId,
    string EvidenceFingerprint,
    string IdempotencyKey,
    string Domain,
    string RequirementKey,
    string PolicyId,
    int PolicyVersion,
    Guid ActorId,
    MigrationApprovalDecision Decision,
    string? Reason,
    DateTimeOffset DecidedAt,
    byte[] Version,
    bool EvidenceConfirmed = false);

public sealed record MigrationHandoverReadinessSnapshot(
    Guid Id,
    TenantId TenantId,
    Guid RunId,
    Guid ReconciliationId,
    int ReconciliationVersion,
    Guid AttemptId,
    string EvidenceFingerprint,
    string IdempotencyKey,
    DateTimeOffset CreatedAt,
    bool BusinessReady,
    bool ProductionReady,
    bool Mesp48Complete,
    bool Mesp50Complete,
    bool TenantActivationPerformed,
    string ResultCode,
    byte[] Version);

public sealed record MigrationReconciliationRecord(
    Guid Id,
    TenantId TenantId,
    Guid RunId,
    Guid AttemptId,
    int VersionNumber,
    string EvidenceFingerprint,
    string IdempotencyKey,
    MigrationReconciliationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset CalculatedAt,
    int SubmittedCount,
    int AcceptedCount,
    int RejectedCount,
    int DuplicateCount,
    int SkippedCount,
    int QuarantinedCount,
    int UnresolvedCount,
    int RequiredApprovalCount,
    int ObtainedApprovalCount,
    decimal SourceDebit,
    decimal SourceCredit,
    decimal TargetDebit,
    decimal TargetCredit,
    decimal Variance,
    byte[] Version,
    bool IsCurrent,
    string? ApprovalPolicyId,
    int? ApprovalPolicyVersion,
    string ApprovalPolicyCode,
    DateTimeOffset? ApprovalPolicyEffectiveFrom,
    DateTimeOffset? ApprovalPolicyEffectiveTo,
    bool ApprovalEnforcesSeparationOfDuties,
    IReadOnlyList<MigrationReconciliationApprovalRequirement> Requirements,
    IReadOnlyList<MigrationReconciliationDetail> Details,
    IReadOnlyList<MigrationReconciliationApprovalRecord> Approvals,
    MigrationHandoverReadinessSnapshot? Readiness);

public sealed record SaveMigrationReconciliationCommand(MigrationReconciliationRecord Reconciliation, byte[] ExpectedRunVersion);
public sealed record CreateMigrationReconciliationApprovalCommand(MigrationReconciliationApprovalRecord Approval, byte[] ExpectedReconciliationVersion);
public sealed record CreateMigrationHandoverReadinessCommand(MigrationHandoverReadinessSnapshot Snapshot, byte[] ExpectedReconciliationVersion);

public interface IMigrationReconciliationPersistence
{
    Task<MigrationPersistenceResult<MigrationReconciliationRecord>> SaveAsync(TenantContext tenant, SaveMigrationReconciliationCommand command, CancellationToken cancellationToken = default);
    Task<MigrationReconciliationRecord?> FindLatestAsync(TenantContext tenant, Guid runId, CancellationToken cancellationToken = default);
    Task<MigrationReconciliationRecord?> FindAsync(TenantContext tenant, Guid reconciliationId, CancellationToken cancellationToken = default);
    Task<MigrationPersistenceResult<MigrationReconciliationApprovalRecord>> SaveApprovalAsync(TenantContext tenant, CreateMigrationReconciliationApprovalCommand command, CancellationToken cancellationToken = default);
    Task<MigrationPersistenceResult<MigrationReconciliationApprovalRecord>> ConfirmApprovalEvidenceAsync(TenantContext tenant, Guid approvalId, CancellationToken cancellationToken = default);
    Task<MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>> SaveReadinessAsync(TenantContext tenant, CreateMigrationHandoverReadinessCommand command, CancellationToken cancellationToken = default);
}

public sealed record MigrationReconcileRequest(Guid RunId, byte[] ExpectedRunVersion, string IdempotencyKey);
public sealed record MigrationApprovalRequest(Guid RunId, Guid ReconciliationId, string Domain, string RequirementKey, MigrationApprovalDecision Decision, string? Reason, byte[] ExpectedVersion, string IdempotencyKey);
public sealed record MigrationHandoverRequest(Guid RunId, Guid ReconciliationId, byte[] ExpectedVersion, string IdempotencyKey);

#pragma warning restore CS1591
