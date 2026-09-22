#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Finance;

public enum FinanceRevaluationBatchStatus
{
    Draft = 1,
    Calculated = 2,
    Posted = 3,
    Reversed = 4
}

public enum FinanceFxDirection
{
    Gain = 1,
    Loss = 2,
    Zero = 3
}

public enum FinanceEvidenceStatus
{
    Captured = 1,
    NotCaptured = 2,
    LegacyWithoutReportingEvidence = 3,
    Reconciled = 4,
    PendingMapping = 5,
    AmbiguousMapping = 6,
    MissingRate = 7,
    Reversed = 8
}

public static class FinanceRevaluationScopes
{
    public const string ApArAndUnallocatedSettlements = "AP_AR_AND_UNALLOCATED_SETTLEMENTS";
}

public sealed record FinanceExchangeRateEvidence(
    Guid ExchangeRateId,
    Guid ExchangeRateVersionId,
    int VersionNumber,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    DateOnly EffectiveOn,
    decimal Rate,
    int RateScale,
    string Provenance,
    string? SourceNotes,
    string ReferenceValue,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null);

public sealed record FinanceMonetaryEvidence(
    string TransactionCurrencyCode,
    decimal TransactionAmount,
    string FunctionalCurrencyCode,
    decimal FunctionalAmount,
    FinanceExchangeRateEvidence? TransactionToFunctionalRate,
    string? ReportingCurrencyCode,
    decimal? ReportingAmount,
    FinanceExchangeRateEvidence? FunctionalToReportingRate,
    decimal SourceUnroundedFunctionalAmount,
    decimal? SourceUnroundedReportingAmount,
    int RoundingScale,
    string RoundingMode,
    decimal FunctionalRoundingDifference,
    decimal? ReportingRoundingDifference,
    FinanceEvidenceStatus ReportingEvidenceStatus,
    Guid? MonetaryPolicyId = null,
    int? MonetaryPolicyVersionNumber = null);

public sealed record FinanceMonetaryPolicyRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string FunctionalCurrencyCode,
    Guid? ReportingCurrencyId,
    string? ReportingCurrencyCode,
    int RoundingScale,
    string RoundingMode,
    bool RevaluationEnabled,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int VersionNumber,
    byte[] Version);

public sealed record FinanceTaxAccountingEffectRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid OpenItemId,
    FinanceOpenItemKind Kind,
    Guid TaxId,
    string TaxCode,
    Guid TaxRateVersionId,
    int TaxRateVersionNumber,
    DateOnly TaxEffectiveOn,
    decimal TaxRatePercentage,
    decimal TaxableBase,
    decimal TaxAmount,
    string TransactionCurrencyCode,
    decimal FunctionalAmount,
    string FunctionalCurrencyCode,
    Guid JournalId,
    Guid? ReversalJournalId,
    Guid PostingRuleId,
    int PostingRuleVersionNumber,
    FinanceMonetaryEvidence MonetaryEvidence,
    FinanceEvidenceStatus Status,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    byte[] Version);

public sealed record FinanceRevaluationLineRecord(
    Guid Id,
    Guid BatchId,
    Guid CompanyId,
    Guid SourceId,
    string SourceType,
    DateOnly AsOfDate,
    string TransactionCurrencyCode,
    decimal OutstandingTransactionAmount,
    decimal HistoricalFunctionalAmount,
    decimal RevaluedFunctionalAmount,
    decimal Difference,
    FinanceFxDirection Direction,
    FinanceExchangeRateEvidence ExchangeRateEvidence,
    Guid? JournalId,
    Guid? ReversalJournalId,
    FinanceEvidenceStatus Status,
    byte[] Version,
    FinanceMonetaryEvidence? MonetaryEvidence = null,
    string? SourceSnapshotFingerprint = null,
    Guid? PostingRuleId = null,
    int? PostingRuleVersionNumber = null,
    Guid? ExpectedGainAccountId = null,
    Guid? ExpectedLossAccountId = null,
    FinanceEvidenceStatus ReconciliationStatus = FinanceEvidenceStatus.PendingMapping);

public sealed record FinanceRevaluationScopeSourceRecord(
    Guid SourceId,
    string SourceType,
    DateOnly AsOfDate,
    string TransactionCurrencyCode,
    decimal OutstandingTransactionAmount,
    decimal HistoricalFunctionalAmount,
    decimal RevaluedFunctionalAmount,
    decimal Difference,
    FinanceFxDirection Direction,
    FinanceExchangeRateEvidence? ExchangeRateEvidence,
    string SourceSnapshotFingerprint);

public sealed record FinanceRevaluationScopeEvaluation(
    Guid CompanyId,
    DateOnly AsOfDate,
    string Scope,
    IReadOnlyList<FinanceRevaluationScopeSourceRecord> Sources,
    string Fingerprint);

public sealed record FinanceRevaluationBatchRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    DateOnly AsOfDate,
    string Scope,
    FinanceRevaluationBatchStatus Status,
    IReadOnlyList<FinanceRevaluationLineRecord> Lines,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? PostedBy,
    DateTimeOffset? PostedAt,
    Guid? ReversedBy,
    DateTimeOffset? ReversedAt,
    byte[] Version);

public sealed record FinanceTaxAccountingCommand(
    Guid CompanyId,
    Guid OpenItemId,
    Guid TaxId,
    decimal TaxableBase,
    string? SourceLineage,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceTaxAccountingReversalCommand(
    Guid EffectId,
    byte[] ExpectedVersion,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceMonetaryPolicyCommand(
    Guid CompanyId,
    Guid? ReportingCurrencyId,
    int RoundingScale,
    string RoundingMode,
    bool RevaluationEnabled,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceRevaluationBatchCommand(
    Guid CompanyId,
    DateOnly AsOfDate,
    string Scope,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceRevaluationActionCommand(
    Guid BatchId,
    byte[] ExpectedVersion,
    string? Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceTaxAccountingReconciliationRecord(
    Guid EffectId,
    Guid CompanyId,
    Guid OpenItemId,
    Guid TaxId,
    decimal TaxAmount,
    decimal PostedTaxAmount,
    FinanceEvidenceStatus Status,
    Guid JournalId,
    Guid? ReversalJournalId);

public sealed record FinanceFxReconciliationRecord(
    Guid AllocationId,
    Guid CompanyId,
    decimal RealizedDifference,
    decimal PostedDifference,
    FinanceFxDirection Direction,
    FinanceEvidenceStatus Status,
    Guid? JournalId,
    Guid? OpenItemId = null,
    Guid? SettlementDocumentId = null,
    Guid? ReversalJournalId = null,
    Guid? ExpectedAccountId = null,
    Guid? RuleId = null,
    int? RuleVersionNumber = null,
    string? StatusReason = null);

public sealed record FinanceUnrealizedFxReconciliationRecord(
    Guid LineId,
    Guid BatchId,
    Guid CompanyId,
    Guid SourceId,
    string SourceType,
    decimal ExpectedAmount,
    decimal PostedAmount,
    FinanceFxDirection Direction,
    FinanceEvidenceStatus Status,
    Guid? JournalId,
    Guid? ReversalJournalId,
    Guid? ExpectedAccountId,
    Guid? PostingRuleId,
    int? PostingRuleVersionNumber,
    string? StatusReason = null);

public sealed record FinanceReportingCurrencyReconciliationRecord(
    Guid JournalId,
    Guid CompanyId,
    string FunctionalCurrencyCode,
    decimal FunctionalAmount,
    string? ReportingCurrencyCode,
    decimal? ReportingAmount,
    decimal? ExpectedReportingAmount,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    FinanceEvidenceStatus Status,
    Guid? EffectId = null,
    string? StatusReason = null);

#pragma warning restore CS1591
