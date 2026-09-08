#pragma warning disable CS1591

using MiniErp.Contracts.Modules.Finance;
using MiniErp.App.BuildingBlocks.Reporting;

namespace MiniErp.App.Modules.Finance;

public sealed record FinanceCloseReadinessQuery(Guid CompanyId, Guid PeriodId);

public sealed record FinancePeriodCloseCommand(
    Guid CompanyId,
    Guid PeriodId,
    byte[] ExpectedVersion,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinancePeriodReopenCommand(
    Guid CompanyId,
    Guid PeriodId,
    byte[] ExpectedVersion,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceYearEndCommand(
    Guid CompanyId,
    Guid FiscalYearId,
    DateOnly AsOfDate,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceYearEndActionCommand(
    Guid CompanyId,
    Guid RunId,
    byte[] ExpectedVersion,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceCorrectionCommand(
    Guid CompanyId,
    Guid OriginalJournalId,
    DateOnly PostingDate,
    byte[] ExpectedVersion,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public interface IFinanceMesp135Persistence
{
    Task<FinanceOperationResult<FinanceCloseReadinessRecord>> EvaluateCloseReadinessAsync(FinanceRequestContext context, FinanceCloseReadinessQuery query, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinancePeriodCloseRunRecord>> ClosePeriodAsync(FinanceRequestContext context, FinancePeriodCloseCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinancePeriodCloseRunRecord>> ReopenPeriodAsync(FinanceRequestContext context, FinancePeriodReopenCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancePeriodCloseRunRecord>> ListPeriodCloseRunsAsync(FinanceRequestContext context, Guid companyId, Guid? periodId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancePeriodHistoryRecord>> ListPeriodHistoryAsync(FinanceRequestContext context, Guid companyId, Guid? periodId = null, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceYearEndRunRecord>> CalculateYearEndAsync(FinanceRequestContext context, FinanceYearEndCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceYearEndRunRecord>> PostYearEndAsync(FinanceRequestContext context, FinanceYearEndActionCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceYearEndRunRecord>> ReverseYearEndAsync(FinanceRequestContext context, FinanceYearEndActionCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceYearEndRunRecord>> ListYearEndRunsAsync(FinanceRequestContext context, Guid companyId, Guid? fiscalYearId = null, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceJournalRecord>> CorrectJournalAsync(FinanceRequestContext context, FinanceCorrectionCommand command, CancellationToken cancellationToken = default);
    Task<FinanceTrialBalanceReport> QueryTrialBalanceAsync(FinanceRequestContext context, FinanceTrialBalanceQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceGeneralLedgerLineRecord>> QueryGeneralLedgerAsync(FinanceRequestContext context, FinanceGeneralLedgerQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceAgingReportRow>> QueryAgingAsync(FinanceRequestContext context, FinanceAgingReportQuery query, CancellationToken cancellationToken = default);
    Task<FinanceCloseReconciliationRecord> QueryReconciliationAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, Guid? periodId = null, CancellationToken cancellationToken = default);
    Task<FinanceStatementReport> QueryStatementAsync(FinanceRequestContext context, Guid companyId, FinanceStatementKind kind, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}

/// <summary>Source-owned bounded read ports used by Reporting detail views.</summary>
public interface IFinanceReportingReadPort
{
    Task<ReportingSourcePage<FinanceGeneralLedgerLineRecord>> QueryGeneralLedgerPageAsync(
        FinanceRequestContext context,
        FinanceGeneralLedgerQuery query,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);

    Task<ReportingSourcePage<FinanceAgingReportRow>> QueryAgingPageAsync(
        FinanceRequestContext context,
        FinanceAgingReportQuery query,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableFinanceMesp135Persistence : IFinanceMesp135Persistence
{
    private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
    private static Task<IReadOnlyList<T>> EmptyList<T>() => Task.FromResult<IReadOnlyList<T>>([]);
    private static FinanceOperationResult<T> Failure<T>() => FinanceOperationResult<T>.Failure("finance_unavailable");
    public Task<FinanceOperationResult<FinanceCloseReadinessRecord>> EvaluateCloseReadinessAsync(FinanceRequestContext c, FinanceCloseReadinessQuery q, CancellationToken t = default) => Task.FromResult(Failure<FinanceCloseReadinessRecord>());
    public Task<FinanceOperationResult<FinancePeriodCloseRunRecord>> ClosePeriodAsync(FinanceRequestContext c, FinancePeriodCloseCommand q, CancellationToken t = default) => Task.FromResult(Failure<FinancePeriodCloseRunRecord>());
    public Task<FinanceOperationResult<FinancePeriodCloseRunRecord>> ReopenPeriodAsync(FinanceRequestContext c, FinancePeriodReopenCommand q, CancellationToken t = default) => Task.FromResult(Failure<FinancePeriodCloseRunRecord>());
    public Task<IReadOnlyList<FinancePeriodCloseRunRecord>> ListPeriodCloseRunsAsync(FinanceRequestContext c, Guid x, Guid? p = null, CancellationToken t = default) => EmptyList<FinancePeriodCloseRunRecord>();
    public Task<IReadOnlyList<FinancePeriodHistoryRecord>> ListPeriodHistoryAsync(FinanceRequestContext c, Guid x, Guid? p = null, CancellationToken t = default) => EmptyList<FinancePeriodHistoryRecord>();
    public Task<FinanceOperationResult<FinanceYearEndRunRecord>> CalculateYearEndAsync(FinanceRequestContext c, FinanceYearEndCommand q, CancellationToken t = default) => Task.FromResult(Failure<FinanceYearEndRunRecord>());
    public Task<FinanceOperationResult<FinanceYearEndRunRecord>> PostYearEndAsync(FinanceRequestContext c, FinanceYearEndActionCommand q, CancellationToken t = default) => Task.FromResult(Failure<FinanceYearEndRunRecord>());
    public Task<FinanceOperationResult<FinanceYearEndRunRecord>> ReverseYearEndAsync(FinanceRequestContext c, FinanceYearEndActionCommand q, CancellationToken t = default) => Task.FromResult(Failure<FinanceYearEndRunRecord>());
    public Task<IReadOnlyList<FinanceYearEndRunRecord>> ListYearEndRunsAsync(FinanceRequestContext c, Guid x, Guid? y = null, CancellationToken t = default) => EmptyList<FinanceYearEndRunRecord>();
    public Task<FinanceOperationResult<FinanceJournalRecord>> CorrectJournalAsync(FinanceRequestContext c, FinanceCorrectionCommand q, CancellationToken t = default) => Task.FromResult(Failure<FinanceJournalRecord>());
    public Task<FinanceTrialBalanceReport> QueryTrialBalanceAsync(FinanceRequestContext c, FinanceTrialBalanceQuery q, CancellationToken t = default) => Empty<FinanceTrialBalanceReport>();
    public Task<IReadOnlyList<FinanceGeneralLedgerLineRecord>> QueryGeneralLedgerAsync(FinanceRequestContext c, FinanceGeneralLedgerQuery q, CancellationToken t = default) => EmptyList<FinanceGeneralLedgerLineRecord>();
    public Task<IReadOnlyList<FinanceAgingReportRow>> QueryAgingAsync(FinanceRequestContext c, FinanceAgingReportQuery q, CancellationToken t = default) => EmptyList<FinanceAgingReportRow>();
    public Task<FinanceCloseReconciliationRecord> QueryReconciliationAsync(FinanceRequestContext c, Guid x, DateOnly a, Guid? p = null, CancellationToken t = default) => Empty<FinanceCloseReconciliationRecord>();
    public Task<FinanceStatementReport> QueryStatementAsync(FinanceRequestContext c, Guid x, FinanceStatementKind k, DateOnly f, DateOnly to, CancellationToken t = default) => Empty<FinanceStatementReport>();
}

#pragma warning restore CS1591
