#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

internal sealed class MigrationExecutionBatchEntity : ITenantOwned
{
    private MigrationExecutionBatchEntity()
    {
        Fingerprint = string.Empty;
        CorrelationId = string.Empty;
    }

    internal MigrationExecutionBatchEntity(MigrationExecutionBatchRecord record)
    {
        Id = record.Id;
        TenantId = record.TenantId;
        RunId = record.RunId;
        AttemptId = record.AttemptId;
        RecordType = record.RecordType;
        State = record.State;
        OwnerBatchId = record.OwnerBatchId;
        Fingerprint = record.Fingerprint;
        CreatedAt = record.CreatedAt;
        StartedAt = record.StartedAt;
        CompletedAt = record.CompletedAt;
        CorrelationId = record.CorrelationId;
        Version = record.Version;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal MigrationCanonicalRecordType RecordType { get; private set; }
    internal MigrationExecutionBatchState State { get; private set; }
    internal Guid OwnerBatchId { get; private set; }
    internal string Fingerprint { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? StartedAt { get; private set; }
    internal DateTimeOffset? CompletedAt { get; private set; }
    internal string CorrelationId { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal bool Apply(
        MigrationExecutionBatchState state,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt)
    {
        if (State == state)
            return true;

        var allowed = State switch
        {
            MigrationExecutionBatchState.Prepared => state is MigrationExecutionBatchState.Started or MigrationExecutionBatchState.Completed or MigrationExecutionBatchState.Failed or MigrationExecutionBatchState.Unknown,
            MigrationExecutionBatchState.Started => state is MigrationExecutionBatchState.Completed or MigrationExecutionBatchState.Failed or MigrationExecutionBatchState.Unknown,
            _ => false
        };
        if (!allowed)
            return false;

        State = state;
        StartedAt = startedAt ?? StartedAt;
        CompletedAt = completedAt ?? CompletedAt;
        Version = Guid.NewGuid().ToByteArray();
        return true;
    }
}
internal sealed class MigrationExecutionEffectEntity : ITenantOwned
{
    private MigrationExecutionEffectEntity()
    {
        SafeCode = null;
        ResultingResourceCode = null;
        CorrelationId = string.Empty;
    }

    internal MigrationExecutionEffectEntity(MigrationExecutionEffectRecord record)
    {
        Id = record.Id;
        TenantId = record.TenantId;
        RunId = record.RunId;
        AttemptId = record.AttemptId;
        StagedRecordId = record.StagedRecordId;
        SourceSequence = record.SourceSequence;
        RecordType = record.RecordType;
        OwnerBatchId = record.OwnerBatchId;
        OwnerRowId = record.OwnerRowId;
        ResultingResourceId = record.ResultingResourceId;
        ResultingResourceCode = record.ResultingResourceCode;
        Disposition = record.Disposition;
        SafeCode = record.SafeCode;
        CreatedAt = record.CreatedAt;
        EffectStartedAt = record.EffectStartedAt;
        CompletedAt = record.CompletedAt;
        CorrelationId = record.CorrelationId;
        Version = record.Version;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal Guid StagedRecordId { get; private set; }
    internal int SourceSequence { get; private set; }
    internal MigrationCanonicalRecordType RecordType { get; private set; }
    internal Guid OwnerBatchId { get; private set; }
    internal Guid? OwnerRowId { get; private set; }
    internal Guid? ResultingResourceId { get; private set; }
    internal string? ResultingResourceCode { get; private set; }
    internal MigrationExecutionEffectDisposition Disposition { get; private set; }
    internal string? SafeCode { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? EffectStartedAt { get; private set; }
    internal DateTimeOffset? CompletedAt { get; private set; }
    internal string CorrelationId { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal bool Apply(UpdateMigrationExecutionEffectCommand command)
    {
        if (Disposition == command.Disposition)
            return true;

        var allowed = Disposition switch
        {
            MigrationExecutionEffectDisposition.Prepared => command.Disposition is MigrationExecutionEffectDisposition.NonEffect or MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.PartialCompleted or MigrationExecutionEffectDisposition.Failed or MigrationExecutionEffectDisposition.Unknown,
            MigrationExecutionEffectDisposition.Started => command.Disposition is MigrationExecutionEffectDisposition.Committed or MigrationExecutionEffectDisposition.PartialCompleted or MigrationExecutionEffectDisposition.Failed or MigrationExecutionEffectDisposition.Unknown,
            _ => false
        };
        if (!allowed)
            return false;

        Disposition = command.Disposition;
        OwnerRowId = command.OwnerRowId ?? OwnerRowId;
        ResultingResourceId = command.ResultingResourceId ?? ResultingResourceId;
        ResultingResourceCode = command.ResultingResourceCode ?? ResultingResourceCode;
        SafeCode = command.SafeCode ?? SafeCode;
        EffectStartedAt = command.EffectStartedAt ?? EffectStartedAt;
        CompletedAt = command.CompletedAt ?? CompletedAt;
        Version = Guid.NewGuid().ToByteArray();
        return true;
    }
}

internal sealed class MigrationEconomicRepresentationEntity : ITenantOwned
{
    private MigrationEconomicRepresentationEntity()
    {
        OwnerReference = null;
        Status = string.Empty;
        EvidenceVersion = string.Empty;
    }

    internal MigrationEconomicRepresentationEntity(MigrationEconomicRepresentationRecord record)
    {
        Id = record.Id;
        TenantId = record.TenantId;
        RunId = record.RunId;
        AttemptId = record.AttemptId;
        EffectId = record.EffectId;
        OwnerModule = record.OwnerModule;
        Kind = record.Kind;
        OwnerId = record.OwnerId;
        OwnerReference = record.OwnerReference;
        Status = record.Status;
        EvidenceVersion = record.EvidenceVersion;
        OccurredAt = record.OccurredAt;
        RecordedAt = record.RecordedAt;
        EvidenceConfirmed = record.EvidenceConfirmed;
        SourceContract = record.SourceContract;
        SourceEvent = record.SourceEvent;
        FunctionalAmount = record.FunctionalAmount;
        PostingRuleId = record.PostingRuleId;
        PostingRuleVersionNumber = record.PostingRuleVersionNumber;
        ControlAccountId = record.ControlAccountId;
        OffsetAccountId = record.OffsetAccountId;
        Reversal = record.Reversal;
        SourceEvidenceId = record.SourceEvidenceId;
        SourceEvidenceVersion = record.SourceEvidenceVersion;
        OwnerSourceId = record.OwnerSourceId;
        TransactionCurrencyCode = record.TransactionCurrencyCode;
        TransactionAmount = record.TransactionAmount;
        ExpectedFunctionalCurrencyCode = record.ExpectedFunctionalCurrencyCode;
        RateDate = record.RateDate;
        ExchangeRateId = record.ExchangeRateId;
        ExchangeRateVersionId = record.ExchangeRateVersionId;
        ExchangeRateVersionNumber = record.ExchangeRateVersionNumber;
        AppliedRate = record.AppliedRate;
        MonetaryPolicyId = record.MonetaryPolicyId;
        MonetaryPolicyVersionNumber = record.MonetaryPolicyVersionNumber;
        RoundingScale = record.RoundingScale;
        RoundingMode = record.RoundingMode;
        ReportingCurrencyCode = record.ReportingCurrencyCode;
        ReportingExchangeRateId = record.ReportingExchangeRateId;
        ReportingExchangeRateVersionId = record.ReportingExchangeRateVersionId;
        ReportingExchangeRateVersionNumber = record.ReportingExchangeRateVersionNumber;
        ReportingAppliedRate = record.ReportingAppliedRate;
        Version = record.Version;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal Guid EffectId { get; private set; }
    internal MigrationEconomicOwnerModule OwnerModule { get; private set; }
    internal MigrationEconomicRepresentationKind Kind { get; private set; }
    internal Guid OwnerId { get; private set; }
    internal string? OwnerReference { get; private set; }
    internal string Status { get; private set; }
    internal string EvidenceVersion { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal DateTimeOffset RecordedAt { get; private set; }
    internal bool EvidenceConfirmed { get; private set; }
    internal string? SourceContract { get; private set; }
    internal string? SourceEvent { get; private set; }
    internal decimal? FunctionalAmount { get; private set; }
    internal Guid? PostingRuleId { get; private set; }
    internal int? PostingRuleVersionNumber { get; private set; }
    internal Guid? ControlAccountId { get; private set; }
    internal Guid? OffsetAccountId { get; private set; }
    internal bool? Reversal { get; private set; }
    internal Guid? SourceEvidenceId { get; private set; }
    internal int? SourceEvidenceVersion { get; private set; }
    internal Guid? OwnerSourceId { get; private set; }
    internal string? TransactionCurrencyCode { get; private set; }
    internal decimal? TransactionAmount { get; private set; }
    internal string? ExpectedFunctionalCurrencyCode { get; private set; }
    internal DateOnly? RateDate { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal decimal? AppliedRate { get; private set; }
    internal Guid? MonetaryPolicyId { get; private set; }
    internal int? MonetaryPolicyVersionNumber { get; private set; }
    internal int? RoundingScale { get; private set; }
    internal string? RoundingMode { get; private set; }
    internal string? ReportingCurrencyCode { get; private set; }
    internal Guid? ReportingExchangeRateId { get; private set; }
    internal Guid? ReportingExchangeRateVersionId { get; private set; }
    internal int? ReportingExchangeRateVersionNumber { get; private set; }
    internal decimal? ReportingAppliedRate { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}
