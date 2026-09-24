#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

internal sealed class MigrationReconciliationEntity : ITenantOwned
{
    private MigrationReconciliationEntity() { }

    internal MigrationReconciliationEntity(MigrationReconciliationRecord x)
    {
        Id = x.Id; TenantId = x.TenantId; RunId = x.RunId; AttemptId = x.AttemptId;
        VersionNumber = x.VersionNumber; EvidenceFingerprint = x.EvidenceFingerprint; IdempotencyKey = x.IdempotencyKey; Status = x.Status;
        CreatedAt = x.CreatedAt; CalculatedAt = x.CalculatedAt; SubmittedCount = x.SubmittedCount;
        AcceptedCount = x.AcceptedCount; RejectedCount = x.RejectedCount; DuplicateCount = x.DuplicateCount;
        SkippedCount = x.SkippedCount; QuarantinedCount = x.QuarantinedCount; UnresolvedCount = x.UnresolvedCount;
        RequiredApprovalCount = x.RequiredApprovalCount; SourceDebit = x.SourceDebit; SourceCredit = x.SourceCredit;
        TargetDebit = x.TargetDebit; TargetCredit = x.TargetCredit; Variance = x.Variance;
        ApprovalPolicyId = x.ApprovalPolicyId; ApprovalPolicyVersion = x.ApprovalPolicyVersion;
        ApprovalPolicyCode = x.ApprovalPolicyCode;
        ApprovalPolicyEffectiveFrom = x.ApprovalPolicyEffectiveFrom; ApprovalPolicyEffectiveTo = x.ApprovalPolicyEffectiveTo;
        ApprovalEnforcesSeparationOfDuties = x.ApprovalEnforcesSeparationOfDuties; Version = Guid.NewGuid().ToByteArray();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal int VersionNumber { get; private set; }
    internal string EvidenceFingerprint { get; private set; } = string.Empty;
    internal string IdempotencyKey { get; private set; } = string.Empty;
    internal MigrationReconciliationStatus Status { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset CalculatedAt { get; private set; }
    internal int SubmittedCount { get; private set; }
    internal int AcceptedCount { get; private set; }
    internal int RejectedCount { get; private set; }
    internal int DuplicateCount { get; private set; }
    internal int SkippedCount { get; private set; }
    internal int QuarantinedCount { get; private set; }
    internal int UnresolvedCount { get; private set; }
    internal int RequiredApprovalCount { get; private set; }
    internal decimal SourceDebit { get; private set; }
    internal decimal SourceCredit { get; private set; }
    internal decimal TargetDebit { get; private set; }
    internal decimal TargetCredit { get; private set; }
    internal decimal Variance { get; private set; }
    internal string? ApprovalPolicyId { get; private set; }
    internal int? ApprovalPolicyVersion { get; private set; }
    internal string ApprovalPolicyCode { get; private set; } = string.Empty;
    internal DateTimeOffset? ApprovalPolicyEffectiveFrom { get; private set; }
    internal DateTimeOffset? ApprovalPolicyEffectiveTo { get; private set; }
    internal bool ApprovalEnforcesSeparationOfDuties { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

internal sealed class MigrationReconciliationRequirementEntity : ITenantOwned
{
    private MigrationReconciliationRequirementEntity() { }
    internal MigrationReconciliationRequirementEntity(TenantId tenantId, Guid runId, Guid reconciliationId, string policyId, int policyVersion, bool enforceSod, MigrationReconciliationApprovalRequirement x)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; RunId = runId; ReconciliationId = reconciliationId;
        PolicyId = policyId; PolicyVersion = policyVersion; EnforceSeparationOfDuties = enforceSod;
        Domain = x.Domain; RequirementKey = x.RequirementKey; RequiredCount = x.RequiredCount;
        EligibleActorIdsJson = System.Text.Json.JsonSerializer.Serialize(x.EligibleActorIds.OrderBy(item => item));
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid ReconciliationId { get; private set; }
    internal string Domain { get; private set; } = string.Empty;
    internal string RequirementKey { get; private set; } = string.Empty;
    internal int RequiredCount { get; private set; }
    internal string PolicyId { get; private set; } = string.Empty;
    internal int PolicyVersion { get; private set; }
    internal bool EnforceSeparationOfDuties { get; private set; }
    internal string EligibleActorIdsJson { get; private set; } = "[]";
    internal MigrationReconciliationApprovalRequirement ToRecord() => new(Domain, RequirementKey, RequiredCount,
        System.Text.Json.JsonSerializer.Deserialize<Guid[]>(EligibleActorIdsJson) ?? []);
}

internal sealed class MigrationReconciliationDetailEntity : ITenantOwned
{
    private MigrationReconciliationDetailEntity() { }
    internal MigrationReconciliationDetailEntity(TenantId tenantId, Guid runId, Guid reconciliationId, MigrationReconciliationDetail x)
    {
        Id = x.Id; TenantId = tenantId; RunId = runId; ReconciliationId = reconciliationId; Domain = x.Domain; ScopeKey = x.ScopeKey;
        CompanyId = x.CompanyId; OpeningDate = x.OpeningDate; CurrencyCode = x.CurrencyCode; TransactionCurrencyCode = x.TransactionCurrencyCode;
        FunctionalCurrencyCode = x.FunctionalCurrencyCode; SourceCount = x.SourceCount;
        SourceDebit = x.SourceDebit; SourceCredit = x.SourceCredit; TargetDebit = x.TargetDebit; TargetCredit = x.TargetCredit;
        Variance = x.Variance; SourceAmount = x.SourceAmount; TargetAmount = x.TargetAmount; AmountVariance = x.AmountVariance; OwnerRoundingDifference = x.OwnerRoundingDifference;
        TransactionAmount = x.TransactionAmount; FunctionalAmount = x.FunctionalAmount; SubsidiaryEstablishedAmount = x.SubsidiaryEstablishedAmount;
        GlControlAmount = x.GlControlAmount; ExchangeRateId = x.ExchangeRateId; ExchangeRateVersionId = x.ExchangeRateVersionId;
        ExchangeRateVersionNumber = x.ExchangeRateVersionNumber; AppliedRate = x.AppliedRate;
        SourceQuantity = x.SourceQuantity; TargetQuantity = x.TargetQuantity; QuantityVariance = x.QuantityVariance;
        ControlAccountId = x.ControlAccountId; PostingRuleId = x.PostingRuleId; PostingRuleVersionNumber = x.PostingRuleVersionNumber;
        OwnerSourceId = x.OwnerSourceId; WarehouseId = x.WarehouseId; ProductId = x.ProductId; UnitOfMeasureId = x.UnitOfMeasureId;
        SourceContract = x.SourceContract; SourceEvent = x.SourceEvent; RoundingPolicyId = x.RoundingPolicyId;
        RoundingPolicyVersionNumber = x.RoundingPolicyVersionNumber; RoundingScale = x.RoundingScale; RoundingMode = x.RoundingMode;
        IsBlocking = x.IsBlocking; FindingCode = x.FindingCode; Explanation = x.Explanation; EffectId = x.EffectId;
        OwnerReferenceId = x.OwnerReferenceId; LinkedAccountId = x.LinkedAccountId;
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid ReconciliationId { get; private set; }
    internal MigrationReconciliationDomain Domain { get; private set; }
    internal string ScopeKey { get; private set; } = string.Empty;
    internal Guid? CompanyId { get; private set; }
    internal DateOnly? OpeningDate { get; private set; }
    internal string? CurrencyCode { get; private set; }
    internal string? TransactionCurrencyCode { get; private set; }
    internal string? FunctionalCurrencyCode { get; private set; }
    internal int SourceCount { get; private set; }
    internal decimal? SourceDebit { get; private set; }
    internal decimal? SourceCredit { get; private set; }
    internal decimal? TargetDebit { get; private set; }
    internal decimal? TargetCredit { get; private set; }
    internal decimal? Variance { get; private set; }
    internal decimal? SourceAmount { get; private set; }
    internal decimal? TargetAmount { get; private set; }
    internal decimal? AmountVariance { get; private set; }
    internal decimal? OwnerRoundingDifference { get; private set; }
    internal decimal? TransactionAmount { get; private set; }
    internal decimal? FunctionalAmount { get; private set; }
    internal decimal? SubsidiaryEstablishedAmount { get; private set; }
    internal decimal? GlControlAmount { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal decimal? AppliedRate { get; private set; }
    internal decimal? SourceQuantity { get; private set; }
    internal decimal? TargetQuantity { get; private set; }
    internal decimal? QuantityVariance { get; private set; }
    internal Guid? ControlAccountId { get; private set; }
    internal Guid? PostingRuleId { get; private set; }
    internal int? PostingRuleVersionNumber { get; private set; }
    internal Guid? OwnerSourceId { get; private set; }
    internal Guid? WarehouseId { get; private set; }
    internal Guid? ProductId { get; private set; }
    internal Guid? UnitOfMeasureId { get; private set; }
    internal string? SourceContract { get; private set; }
    internal string? SourceEvent { get; private set; }
    internal Guid? RoundingPolicyId { get; private set; }
    internal int? RoundingPolicyVersionNumber { get; private set; }
    internal int? RoundingScale { get; private set; }
    internal string? RoundingMode { get; private set; }
    internal bool IsBlocking { get; private set; }
    internal string? FindingCode { get; private set; }
    internal string? Explanation { get; private set; }
    internal Guid? EffectId { get; private set; }
    internal Guid? OwnerReferenceId { get; private set; }
    internal Guid? LinkedAccountId { get; private set; }
}

internal sealed class MigrationReconciliationApprovalEntity : ITenantOwned
{
    private MigrationReconciliationApprovalEntity() { }
    internal MigrationReconciliationApprovalEntity(MigrationReconciliationApprovalRecord x)
    {
        Id = x.Id; TenantId = x.TenantId; RunId = x.RunId; ReconciliationId = x.ReconciliationId;
        ReconciliationVersion = x.ReconciliationVersion; AttemptId = x.AttemptId; EvidenceFingerprint = x.EvidenceFingerprint;
        IdempotencyKey = x.IdempotencyKey; Domain = x.Domain; RequirementKey = x.RequirementKey; PolicyId = x.PolicyId; PolicyVersion = x.PolicyVersion;
        ActorId = x.ActorId; Decision = x.Decision; Reason = x.Reason; DecidedAt = x.DecidedAt; Version = x.Version; EvidenceConfirmed = x.EvidenceConfirmed;
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid ReconciliationId { get; private set; }
    internal int ReconciliationVersion { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal string EvidenceFingerprint { get; private set; } = string.Empty;
    internal string IdempotencyKey { get; private set; } = string.Empty;
    internal string Domain { get; private set; } = string.Empty;
    internal string RequirementKey { get; private set; } = string.Empty;
    internal string PolicyId { get; private set; } = string.Empty;
    internal int PolicyVersion { get; private set; }
    internal Guid ActorId { get; private set; }
    internal MigrationApprovalDecision Decision { get; private set; }
    internal string? Reason { get; private set; }
    internal DateTimeOffset DecidedAt { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
    internal bool EvidenceConfirmed { get; private set; }
    internal void ConfirmEvidence() => EvidenceConfirmed = true;
}

internal sealed class MigrationHandoverReadinessEntity : ITenantOwned
{
    private MigrationHandoverReadinessEntity() { }
    internal MigrationHandoverReadinessEntity(MigrationHandoverReadinessSnapshot x)
    {
        Id = x.Id; TenantId = x.TenantId; RunId = x.RunId; ReconciliationId = x.ReconciliationId;
        ReconciliationVersion = x.ReconciliationVersion; AttemptId = x.AttemptId; EvidenceFingerprint = x.EvidenceFingerprint;
        IdempotencyKey = x.IdempotencyKey; CreatedAt = x.CreatedAt; BusinessReady = x.BusinessReady; ProductionReady = x.ProductionReady;
        Mesp48Complete = x.Mesp48Complete; Mesp50Complete = x.Mesp50Complete; TenantActivationPerformed = x.TenantActivationPerformed;
        ResultCode = x.ResultCode; Version = x.Version;
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid ReconciliationId { get; private set; }
    internal int ReconciliationVersion { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal string EvidenceFingerprint { get; private set; } = string.Empty;
    internal string IdempotencyKey { get; private set; } = string.Empty;
    internal DateTimeOffset CreatedAt { get; private set; }
    internal bool BusinessReady { get; private set; }
    internal bool ProductionReady { get; private set; }
    internal bool Mesp48Complete { get; private set; }
    internal bool Mesp50Complete { get; private set; }
    internal bool TenantActivationPerformed { get; private set; }
    internal string ResultCode { get; private set; } = string.Empty;
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

#pragma warning restore CS1591
