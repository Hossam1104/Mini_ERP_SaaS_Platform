#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.Contracts.Modules.Finance;
using System.Text.Json;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal abstract class FinanceEntity : ITenantOwned
{
    public TenantId TenantId { get; private set; }
    internal Guid Id { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    protected FinanceEntity() { }
    protected FinanceEntity(TenantId tenantId, Guid id)
    {
        TenantId = tenantId;
        Id = id;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class FinanceAccountEntity : FinanceEntity
{
    private FinanceAccountEntity() { Code = EnglishName = string.Empty; }
    internal FinanceAccountEntity(TenantId tenantId, Guid id, FinanceAccountCommand command) : base(tenantId, id)
    {
        CompanyId = command.CompanyId; Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName;
        ParentAccountId = command.ParentAccountId; AccountType = command.AccountType; IsPostingAccount = command.IsPostingAccount;
        CurrencyBehavior = command.CurrencyBehavior; EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; Lifecycle = FinanceAccountLifecycle.Active;
    }
    internal Guid CompanyId { get; private set; }
    internal string Code { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal Guid? ParentAccountId { get; private set; }
    internal FinanceAccountType AccountType { get; private set; }
    internal bool IsPostingAccount { get; private set; }
    internal FinanceAccountLifecycle Lifecycle { get; private set; }
    internal FinanceCurrencyBehavior CurrencyBehavior { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal void Edit(FinanceAccountCommand command)
    {
        Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName; ParentAccountId = command.ParentAccountId;
        AccountType = command.AccountType; IsPostingAccount = command.IsPostingAccount; CurrencyBehavior = command.CurrencyBehavior;
        EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; TouchVersion();
    }
    internal void SetLifecycle(FinanceAccountLifecycle lifecycle) { Lifecycle = lifecycle; TouchVersion(); }
}

internal sealed class FinanceFiscalCalendarEntity : FinanceEntity
{
    private FinanceFiscalCalendarEntity() { Name = FunctionalCurrencyCode = string.Empty; }
    internal FinanceFiscalCalendarEntity(TenantId tenantId, Guid id, FinanceFiscalCalendarCommand command, string currency) : base(tenantId, id)
    { CompanyId = command.CompanyId; Name = command.Name; FunctionalCurrencyCode = currency; Lifecycle = FinanceCalendarLifecycle.Active; }
    internal Guid CompanyId { get; private set; }
    internal string Name { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal FinanceCalendarLifecycle Lifecycle { get; private set; }
}

internal sealed class FinanceFiscalYearEntity : FinanceEntity
{
    private FinanceFiscalYearEntity() { }
    internal FinanceFiscalYearEntity(TenantId tenantId, Guid id, FinanceFiscalYearCommand command, Guid companyId) : base(tenantId, id)
    { CalendarId = command.CalendarId; CompanyId = companyId; YearNumber = command.YearNumber; StartDate = command.StartDate; EndDate = command.EndDate; State = FinanceFiscalYearState.Open; }
    internal Guid CalendarId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal int YearNumber { get; private set; }
    internal DateOnly StartDate { get; private set; }
    internal DateOnly EndDate { get; private set; }
    internal FinanceFiscalYearState State { get; private set; }
    internal void SetState(FinanceFiscalYearState state) { State = state; TouchVersion(); }
}

internal sealed class FinanceFiscalPeriodEntity : FinanceEntity
{
    private FinanceFiscalPeriodEntity() { Code = string.Empty; }
    internal FinanceFiscalPeriodEntity(TenantId tenantId, Guid id, FinanceFiscalPeriodCommand command, Guid companyId) : base(tenantId, id)
    { FiscalYearId = command.FiscalYearId; CompanyId = companyId; Sequence = command.Sequence; Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName; StartDate = command.StartDate; EndDate = command.EndDate; State = FinanceFiscalPeriodState.Draft; }
    internal Guid FiscalYearId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal int Sequence { get; private set; }
    internal string Code { get; private set; }
    internal string? EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal DateOnly StartDate { get; private set; }
    internal DateOnly EndDate { get; private set; }
    internal FinanceFiscalPeriodState State { get; private set; }
    internal void SetState(FinanceFiscalPeriodState state) { State = state; TouchVersion(); }
}

internal sealed class FinanceCostCenterEntity : FinanceEntity
{
    private FinanceCostCenterEntity() { Code = EnglishName = string.Empty; }
    internal FinanceCostCenterEntity(TenantId tenantId, Guid id, FinanceCostCenterCommand command) : base(tenantId, id)
    { CompanyId = command.CompanyId; Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName; EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; Lifecycle = FinanceAccountLifecycle.Active; }
    internal Guid CompanyId { get; private set; }
    internal string Code { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal FinanceAccountLifecycle Lifecycle { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
}

internal sealed class FinancePostingRuleEntity : FinanceEntity
{
    private FinancePostingRuleEntity() { SourceContract = SourceEvent = DebitAccountCode = CreditAccountCode = string.Empty; }
    internal FinancePostingRuleEntity(TenantId tenantId, Guid id, FinancePostingRuleCommand command, int version, string debitCode, string creditCode) : base(tenantId, id)
    { CompanyId = command.CompanyId; SourceContract = command.SourceContract; SourceEvent = command.SourceEvent; VersionNumber = version; DebitAccountId = command.DebitAccountId; DebitAccountCode = debitCode; CreditAccountId = command.CreditAccountId; CreditAccountCode = creditCode; CostCenterRequired = command.CostCenterRequired; EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; Lifecycle = FinancePostingRuleLifecycle.Enabled; }
    internal Guid CompanyId { get; private set; }
    internal string SourceContract { get; private set; }
    internal string SourceEvent { get; private set; }
    internal int VersionNumber { get; private set; }
    internal Guid DebitAccountId { get; private set; }
    internal string DebitAccountCode { get; private set; }
    internal Guid CreditAccountId { get; private set; }
    internal string CreditAccountCode { get; private set; }
    internal bool CostCenterRequired { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal FinancePostingRuleLifecycle Lifecycle { get; private set; }
    internal void SetLifecycle(FinancePostingRuleLifecycle lifecycle) { Lifecycle = lifecycle; TouchVersion(); }
}

internal sealed class FinanceJournalEntity : FinanceEntity
{
    private FinanceJournalEntity() { JournalNumber = FunctionalCurrencyCode = SourceContract = SourceEvent = Description = CorrelationId = string.Empty; Lines = []; }
    internal FinanceJournalEntity(TenantId tenantId, Guid id, FinanceJournalCommand command, long sequence, string functionalCurrency, Guid actorId, DateTimeOffset at) : base(tenantId, id)
    {
        JournalSequence = sequence; JournalNumber = $"{sequence:D8}"; CompanyId = command.CompanyId; JournalDate = command.JournalDate; PostingDate = command.PostingDate; FunctionalCurrencyCode = functionalCurrency; TransactionCurrencyCode = command.TransactionCurrencyCode; ExchangeRate = command.ExchangeRate; ExchangeRateId = command.ExchangeRateId; ExchangeRateVersionId = command.ExchangeRateVersionId; ExchangeRateVersionNumber = command.ExchangeRateVersionNumber; SourceContract = command.SourceContract; SourceEvent = command.SourceEvent; SourceEvidenceId = command.SourceEvidenceId; SourceEvidenceVersion = command.SourceEvidenceVersion; PostingRuleId = command.PostingRuleId; Description = command.Description; AmountAuthority = command.AmountAuthority; ApprovalRequirement = command.ApprovalRequirement; Status = FinanceJournalStatus.Draft; CreatedBy = actorId; CreatedAt = at; CorrelationId = string.Empty; Lines = [];
    }
    internal Guid CompanyId { get; private set; }
    internal long JournalSequence { get; private set; }
    internal string JournalNumber { get; private set; }
    internal DateOnly JournalDate { get; private set; }
    internal DateOnly PostingDate { get; private set; }
    internal Guid? FiscalYearId { get; private set; }
    internal Guid? FiscalPeriodId { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal string? TransactionCurrencyCode { get; private set; }
    internal decimal? ExchangeRate { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal string SourceContract { get; private set; }
    internal string SourceEvent { get; private set; }
    internal Guid? SourceEvidenceId { get; private set; }
    internal int? SourceEvidenceVersion { get; private set; }
    internal Guid? PostingRuleId { get; private set; }
    internal int? PostingRuleVersionNumber { get; private set; }
    internal FinanceJournalAmountAuthority AmountAuthority { get; private set; }
    internal FinanceApprovalRequirement ApprovalRequirement { get; private set; }
    internal string Description { get; private set; }
    internal FinanceJournalStatus Status { get; private set; }
    internal Guid CreatedBy { get; private set; }
    internal Guid? SubmittedBy { get; private set; }
    internal Guid? ApprovedBy { get; private set; }
    internal Guid? PostedBy { get; private set; }
    internal Guid? ReversedBy { get; private set; }
    internal Guid? ReversalOfJournalId { get; private set; }
    internal Guid? ReversalJournalId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal ICollection<FinanceJournalLineEntity> Lines { get; private set; }
    internal void UpdateHeader(FinanceJournalCommand command)
    {
        JournalDate = command.JournalDate; PostingDate = command.PostingDate; TransactionCurrencyCode = command.TransactionCurrencyCode;
        ExchangeRate = command.ExchangeRate; ExchangeRateId = command.ExchangeRateId; ExchangeRateVersionId = command.ExchangeRateVersionId;
        ExchangeRateVersionNumber = command.ExchangeRateVersionNumber; Description = command.Description; TouchVersion();
    }
    internal void ReplaceLines(IEnumerable<FinanceJournalLineEntity> lines)
    {
        Lines.Clear();
        foreach (var line in lines) Lines.Add(line);
        TouchVersion();
    }
    internal void SetCorrelation(string correlationId) => CorrelationId = correlationId;
    internal void SetPeriod(Guid yearId, Guid periodId) { FiscalYearId = yearId; FiscalPeriodId = periodId; }
    internal void SetRule(Guid ruleId, int version) { PostingRuleId = ruleId; PostingRuleVersionNumber = version; }
    internal void SetStatus(FinanceJournalStatus status, Guid actorId, DateTimeOffset at)
    { Status = status; if (status == FinanceJournalStatus.Submitted) SubmittedBy = actorId; if (status == FinanceJournalStatus.Approved) ApprovedBy = actorId; if (status == FinanceJournalStatus.Posted) { PostedBy = actorId; PostedAt = at; } if (status == FinanceJournalStatus.Reversed) ReversedBy = actorId; TouchVersion(); }
    internal void LinkReversal(Guid reversalId) { ReversalJournalId = reversalId; TouchVersion(); }
    internal void LinkOriginal(Guid originalId) { ReversalOfJournalId = originalId; }
}

internal sealed class FinanceJournalLineEntity : FinanceEntity
{
    private FinanceJournalLineEntity() { AccountCode = AccountName = string.Empty; }
    internal FinanceJournalLineEntity(TenantId tenantId, Guid id, Guid journalId, int number, FinanceAccountEntity account, FinanceJournalLineCommand command, FinanceCostCenterEntity? costCenter, decimal functionalDebit, decimal functionalCredit, FinanceJournalAmountAuthority amountAuthority) : base(tenantId, id)
    { JournalId = journalId; LineNumber = number; AccountId = account.Id; AccountCode = account.Code; AccountName = account.EnglishName; Debit = command.Debit; Credit = command.Credit; FunctionalDebit = functionalDebit; FunctionalCredit = functionalCredit; TransactionAmount = amountAuthority == FinanceJournalAmountAuthority.SourceFunctionalCurrency ? command.TransactionAmount : command.TransactionAmount ?? Math.Max(command.Debit, command.Credit); TransactionCurrencyCode = command.TransactionCurrencyCode; CostCenterId = costCenter?.Id; CostCenterCode = costCenter?.Code; Description = command.Description; }
    internal Guid JournalId { get; private set; }
    internal int LineNumber { get; private set; }
    internal Guid AccountId { get; private set; }
    internal string AccountCode { get; private set; }
    internal string AccountName { get; private set; }
    internal decimal Debit { get; private set; }
    internal decimal Credit { get; private set; }
    internal decimal FunctionalDebit { get; private set; }
    internal decimal FunctionalCredit { get; private set; }
    internal decimal? TransactionAmount { get; private set; }
    internal string? TransactionCurrencyCode { get; private set; }
    internal Guid? CostCenterId { get; private set; }
    internal string? CostCenterCode { get; private set; }
    internal string? Description { get; private set; }
}

internal sealed class FinanceAuditEntity : FinanceEntity
{
    private FinanceAuditEntity() { OperationId = ResourceType = Result = CorrelationId = string.Empty; }
    internal FinanceAuditEntity(TenantId tenantId, Guid id, string operationId, string resourceType, Guid resourceId, Guid actorId, Guid sessionId, string result, string? reason, string correlationId, string? idempotencyKey, DateTimeOffset at) : base(tenantId, id)
    { OperationId = operationId; ResourceType = resourceType; ResourceId = resourceId; ActorId = actorId; SessionId = sessionId; Result = result; Reason = reason; CorrelationId = correlationId; IdempotencyKey = idempotencyKey; OccurredAt = at; }
    internal string OperationId { get; private set; }
    internal string ResourceType { get; private set; }
    internal Guid ResourceId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string Result { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; }
    internal string? IdempotencyKey { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
}

internal sealed class FinanceIdempotencyEntity : FinanceEntity
{
    private FinanceIdempotencyEntity() { OperationId = Key = Fingerprint = ResourceType = SnapshotJson = string.Empty; }
    internal FinanceIdempotencyEntity(TenantId tenantId, Guid id, Guid actorId, string operationId, string key, string fingerprint, string resourceType, Guid resourceId, string snapshotJson, DateTimeOffset at) : base(tenantId, id)
    { ActorId = actorId; OperationId = operationId; Key = key; Fingerprint = fingerprint; ResourceType = resourceType; ResourceId = resourceId; SnapshotJson = snapshotJson; CreatedAt = at; }
    internal Guid ActorId { get; private set; }
    internal string OperationId { get; private set; }
    internal string Key { get; private set; }
    internal string Fingerprint { get; private set; }
    internal string ResourceType { get; private set; }
    internal Guid ResourceId { get; private set; }
    internal string SnapshotJson { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal void SetSnapshot(string snapshotJson) => SnapshotJson = snapshotJson;
}

internal sealed class FinanceSourceEffectEntity : FinanceEntity
{
    private FinanceSourceEffectEntity() { SourceContract = string.Empty; }
    internal FinanceSourceEffectEntity(TenantId tenantId, Guid id, Guid companyId, string sourceContract, Guid sourceEvidenceId, int sourceVersion, Guid journalId, DateTimeOffset at) : base(tenantId, id)
    { CompanyId = companyId; SourceContract = sourceContract; SourceEvidenceId = sourceEvidenceId; SourceEvidenceVersion = sourceVersion; JournalId = journalId; CreatedAt = at; }
    internal Guid CompanyId { get; private set; }
    internal string SourceContract { get; private set; }
    internal Guid SourceEvidenceId { get; private set; }
    internal int SourceEvidenceVersion { get; private set; }
    internal Guid JournalId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>
/// Immutable monetary evidence captured with a Finance journal. This is kept
/// separate from the journal header so legacy journals remain distinguishable
/// from journals created after the MESP-134 monetary policy became effective.
/// </summary>
internal sealed class FinanceJournalMonetaryEvidenceEntity : FinanceEntity
{
    private FinanceJournalMonetaryEvidenceEntity()
    {
        TransactionCurrencyCode = FunctionalCurrencyCode = RoundingMode = string.Empty;
        MonetaryEvidenceJson = string.Empty;
    }

    internal FinanceJournalMonetaryEvidenceEntity(
        TenantId tenantId,
        Guid id,
        Guid journalId,
        Guid companyId,
        Guid? effectId,
        FinanceMonetaryEvidence evidence,
        DateTimeOffset at)
        : base(tenantId, id)
    {
        JournalId = journalId;
        CompanyId = companyId;
        EffectId = effectId;
        TransactionCurrencyCode = evidence.TransactionCurrencyCode;
        TransactionAmount = evidence.TransactionAmount;
        FunctionalCurrencyCode = evidence.FunctionalCurrencyCode;
        FunctionalAmount = evidence.FunctionalAmount;
        ReportingCurrencyCode = evidence.ReportingCurrencyCode;
        ReportingAmount = evidence.ReportingAmount;
        TransactionToFunctionalRateId = evidence.TransactionToFunctionalRate?.ExchangeRateId;
        TransactionToFunctionalRateVersionId = evidence.TransactionToFunctionalRate?.ExchangeRateVersionId;
        TransactionToFunctionalRateVersionNumber = evidence.TransactionToFunctionalRate?.VersionNumber;
        FunctionalToReportingRateId = evidence.FunctionalToReportingRate?.ExchangeRateId;
        FunctionalToReportingRateVersionId = evidence.FunctionalToReportingRate?.ExchangeRateVersionId;
        FunctionalToReportingRateVersionNumber = evidence.FunctionalToReportingRate?.VersionNumber;
        SourceUnroundedFunctionalAmount = evidence.SourceUnroundedFunctionalAmount;
        SourceUnroundedReportingAmount = evidence.SourceUnroundedReportingAmount;
        RoundingScale = evidence.RoundingScale;
        RoundingMode = evidence.RoundingMode;
        FunctionalRoundingDifference = evidence.FunctionalRoundingDifference;
        ReportingRoundingDifference = evidence.ReportingRoundingDifference;
        ReportingEvidenceStatus = evidence.ReportingEvidenceStatus;
        MonetaryEvidenceJson = JsonSerializer.Serialize(evidence);
        CreatedAt = at;
    }

    internal Guid JournalId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? EffectId { get; private set; }
    internal string TransactionCurrencyCode { get; private set; }
    internal decimal TransactionAmount { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal decimal FunctionalAmount { get; private set; }
    internal string? ReportingCurrencyCode { get; private set; }
    internal decimal? ReportingAmount { get; private set; }
    internal Guid? TransactionToFunctionalRateId { get; private set; }
    internal Guid? TransactionToFunctionalRateVersionId { get; private set; }
    internal int? TransactionToFunctionalRateVersionNumber { get; private set; }
    internal Guid? FunctionalToReportingRateId { get; private set; }
    internal Guid? FunctionalToReportingRateVersionId { get; private set; }
    internal int? FunctionalToReportingRateVersionNumber { get; private set; }
    internal decimal SourceUnroundedFunctionalAmount { get; private set; }
    internal decimal? SourceUnroundedReportingAmount { get; private set; }
    internal int RoundingScale { get; private set; }
    internal string RoundingMode { get; private set; }
    internal decimal FunctionalRoundingDifference { get; private set; }
    internal decimal? ReportingRoundingDifference { get; private set; }
    internal FinanceEvidenceStatus ReportingEvidenceStatus { get; private set; }
    internal string MonetaryEvidenceJson { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
}

#pragma warning restore CS1591
