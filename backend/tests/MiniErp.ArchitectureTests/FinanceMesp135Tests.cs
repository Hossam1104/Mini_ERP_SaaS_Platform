using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class FinanceMesp135Tests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void MESP135_public_operations_are_scoped_and_use_mutation_controls()
    {
        var operationIds = new[]
        {
            "finance.close.readiness", "finance.close.runs", "finance.close.history", "finance.close.period", "finance.close.reopen",
            "finance.year-end.list", "finance.year-end.calculate", "finance.year-end.post", "finance.year-end.reverse",
            "finance.correction.create", "finance.reconciliation.close", "finance.reconciliation.close.export",
            "finance.report.trial-balance", "finance.report.trial-balance.export",
            "finance.report.general-ledger", "finance.report.general-ledger.export", "finance.report.ap-aging", "finance.report.ap-aging.export",
            "finance.report.ar-aging", "finance.report.ar-aging.export", "finance.report.profit-loss", "finance.report.profit-loss.export",
            "finance.report.balance-sheet", "finance.report.balance-sheet.export"
        };

        foreach (var operationId in operationIds)
        {
            var operation = FoundationOperationCatalog.GetRequired(operationId);
            Assert.Equal(FoundationOperationVisibility.Public, operation.Visibility);
            Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);
            Assert.False(string.IsNullOrWhiteSpace(operation.ExactPermissionCode));
        }

        foreach (var operationId in new[]
                 {
                     "finance.close.period", "finance.close.reopen", "finance.year-end.post",
                     "finance.year-end.reverse", "finance.correction.create"
                 })
        {
            var operation = FoundationOperationCatalog.GetRequired(operationId);
            Assert.Equal(FoundationConcurrencyPolicy.IfMatch, operation.Concurrency);
            Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
            Assert.True(operation.RequiresAntiforgery);
            Assert.True(operation.RequiresMandatoryAudit);
            Assert.True(operation.IsUnsafe);
        }
    }

    [Fact]
    public async Task Period_close_persists_readiness_evidence_history_and_reopen_is_versioned()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(
            fixture.Context("tenant.finance.period.close"),
            new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));

        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Status);
        Assert.Contains(readiness.Value.Checks, check => check.Code == "gl_balanced");

        var close = await fixture.Persistence.ClosePeriodAsync(
            fixture.Context("tenant.finance.period.close"),
            new FinancePeriodCloseCommand(fixture.CompanyId, period.Id, period.Version, "MESP-135 close", Guid.NewGuid(), "close-1", "close-1"));

        Assert.True(close.Succeeded, close.Code);
        Assert.Equal(FinanceCloseRunStatus.Closed, close.Value!.Status);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            Assert.Equal(1, await db.PeriodCloseEvidence.CountAsync(item => item.PeriodId == period.Id));
            Assert.Equal(1, await db.PeriodCloseRuns.CountAsync(item => item.PeriodId == period.Id));
            Assert.Equal(1, await db.PeriodHistory.CountAsync(item => item.PeriodId == period.Id));
        }

        var stale = await fixture.Persistence.ReopenPeriodAsync(
            fixture.Context("tenant.finance.period.reopen"),
            new FinancePeriodReopenCommand(fixture.CompanyId, period.Id, period.Version, "stale reopen", Guid.NewGuid(), "reopen-stale", "reopen-stale"));
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var currentVersion = await fixture.CurrentPeriodVersionAsync(period.Id);
        var reopened = await fixture.Persistence.ReopenPeriodAsync(
            fixture.Context("tenant.finance.period.reopen"),
            new FinancePeriodReopenCommand(fixture.CompanyId, period.Id, currentVersion, "MESP-135 reopen", Guid.NewGuid(), "reopen-1", "reopen-1"));

        Assert.True(reopened.Succeeded, reopened.Code);
        Assert.Equal(FinanceCloseRunStatus.Reopened, reopened.Value!.Status);
        var history = await fixture.Persistence.ListPeriodHistoryAsync(fixture.Context("tenant.finance.period.view"), fixture.CompanyId, period.Id);
        Assert.Equal([FinancePeriodHistoryAction.Reopened, FinancePeriodHistoryAction.Closed], history.Select(item => item.Action).ToArray());
    }

    [Fact]
    public async Task Correction_is_exactly_inverse_and_reports_are_deterministic()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var original = await fixture.CreatePostedJournalAsync(period);

        var correction = await fixture.Persistence.CorrectJournalAsync(
            fixture.Context("tenant.finance.journal.correct"),
            new FinanceCorrectionCommand(fixture.CompanyId, original.Id, new DateOnly(2026, 1, 20), original.Version, "MESP-135 correction", Guid.NewGuid(), "correction-1", "correction-1"));

        Assert.True(correction.Succeeded, correction.Code);
        Assert.Equal(original.Id, correction.Value!.ReversalOfJournalId);
        Assert.Equal(original.Lines.Count, correction.Value.Lines.Count);
        for (var index = 0; index < original.Lines.Count; index++)
        {
            Assert.Equal(original.Lines[index].Debit, correction.Value.Lines[index].Credit);
            Assert.Equal(original.Lines[index].Credit, correction.Value.Lines[index].Debit);
            Assert.Equal(original.Lines[index].FunctionalDebit, correction.Value.Lines[index].FunctionalCredit);
            Assert.Equal(original.Lines[index].FunctionalCredit, correction.Value.Lines[index].FunctionalDebit);
        }

        var trialBalance = await fixture.Persistence.QueryTrialBalanceAsync(
            fixture.Context("tenant.finance.report.trial-balance"),
            new FinanceTrialBalanceQuery(fixture.CompanyId, period.EndDate, period.Id));
        var generalLedger = await fixture.Persistence.QueryGeneralLedgerAsync(
            fixture.Context("tenant.finance.report.general-ledger"),
            new FinanceGeneralLedgerQuery(fixture.CompanyId, FiscalPeriodId: period.Id));

        Assert.Equal(trialBalance.TotalDebit, trialBalance.TotalCredit);
        Assert.Equal(0m, trialBalance.TotalClosingBalance);
        Assert.Equal(4, generalLedger.Count);
        Assert.Equal(0m, generalLedger.Sum(item => item.FunctionalDebit - item.FunctionalCredit));

        await using var db = new FinanceDbContext(fixture.Options, fixture.TenantContext);
        var storedOriginal = await db.Journals.SingleAsync(item => item.Id == original.Id);
        Assert.Equal(FinanceJournalStatus.Reversed, storedOriginal.Status);
        Assert.Equal(correction.Value.Id, storedOriginal.ReversalJournalId);
    }

    [Fact]
    public async Task Close_readiness_is_read_only_and_does_not_create_evidence_or_history()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var query = new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.period.close"), query);
            Assert.True(readiness.Succeeded, readiness.Code);
        }

        await using var db = new FinanceDbContext(fixture.Options, fixture.TenantContext);
        Assert.Equal(0, await db.PeriodCloseEvidence.CountAsync(item => item.PeriodId == period.Id));
        Assert.Equal(0, await db.PeriodCloseRuns.CountAsync(item => item.PeriodId == period.Id));
        Assert.Equal(0, await db.PeriodHistory.CountAsync(item => item.PeriodId == period.Id));
        Assert.Equal(period.Version, await fixture.CurrentPeriodVersionAsync(period.Id));
    }

    [Fact]
    public async Task Profit_and_loss_has_zero_opening_while_balance_sheet_carries_prior_period_closing()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var januaryPeriod = await fixture.CreateOpenPeriodAsync();
        var januaryJournal = await fixture.CreatePostedJournalAsync(januaryPeriod);

        var profitAndLoss = await fixture.Persistence.QueryStatementAsync(
            fixture.Context("tenant.finance.report.view"), fixture.CompanyId, FinanceStatementKind.ProfitAndLoss,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var revenueRow = Assert.Single(profitAndLoss.Rows, row => row.AccountType == FinanceAccountType.Revenue);
        Assert.Equal(0m, revenueRow.OpeningBalance);
        Assert.Equal(100m, revenueRow.Credit);
        Assert.Equal(-100m, revenueRow.ClosingBalance);

        var balanceSheetJanuary = await fixture.Persistence.QueryStatementAsync(
            fixture.Context("tenant.finance.report.view"), fixture.CompanyId, FinanceStatementKind.BalanceSheet,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var assetRowJanuary = Assert.Single(balanceSheetJanuary.Rows, row => row.AccountType == FinanceAccountType.Asset);
        Assert.Equal(0m, assetRowJanuary.OpeningBalance);
        Assert.Equal(100m, assetRowJanuary.ClosingBalance);

        var balanceSheetFebruary = await fixture.Persistence.QueryStatementAsync(
            fixture.Context("tenant.finance.report.view"), fixture.CompanyId, FinanceStatementKind.BalanceSheet,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        var assetRowFebruary = Assert.Single(balanceSheetFebruary.Rows, row => row.AccountType == FinanceAccountType.Asset);
        Assert.Equal(100m, assetRowFebruary.OpeningBalance);
        Assert.Equal(0m, assetRowFebruary.Debit);
        Assert.Equal(100m, assetRowFebruary.ClosingBalance);
    }

    [Fact]
    public async Task Year_end_post_establishes_closing_line_lineage_and_reverse_reopens_period_for_correction()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenYearPeriodAsync();
        var journal = await fixture.CreatePostedJournalAsync(period);
        await fixture.CreateYearEndPostingRuleAsync();

        var closed = await fixture.Persistence.ClosePeriodAsync(
            fixture.Context("tenant.finance.period.close"),
            new FinancePeriodCloseCommand(fixture.CompanyId, period.Id, period.Version, "MESP-135 year-end close", Guid.NewGuid(), "yearend-close", "yearend-close"));
        Assert.True(closed.Succeeded, closed.Code);

        var calculated = await fixture.Persistence.CalculateYearEndAsync(
            fixture.Context("tenant.finance.year-end.calculate"),
            new FinanceYearEndCommand(fixture.CompanyId, fixture.YearId, new DateOnly(2026, 12, 31), "MESP-135 year end", Guid.NewGuid(), "yearend-calc", "yearend-calc"));
        Assert.True(calculated.Succeeded, calculated.Code);
        Assert.NotEmpty(calculated.Value!.Lines);
        Assert.All(calculated.Value.Lines, line => Assert.Null(line.ClosingJournalLineId));

        var posted = await fixture.Persistence.PostYearEndAsync(
            fixture.Context("tenant.finance.year-end.post"),
            new FinanceYearEndActionCommand(fixture.CompanyId, calculated.Value.Id, calculated.Value.Version, "MESP-135 year end post", Guid.NewGuid(), "yearend-post", "yearend-post"));
        Assert.True(posted.Succeeded, posted.Code);
        Assert.NotNull(posted.Value!.ClosingJournalId);
        Assert.All(posted.Value.Lines, line => Assert.NotNull(line.ClosingJournalLineId));

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var closingLineIds = await db.Journals.Where(item => item.Id == posted.Value.ClosingJournalId).SelectMany(item => item.Lines).Select(item => item.Id).ToListAsync();
            Assert.Equal(closingLineIds.OrderBy(item => item).ToArray(), posted.Value.Lines.Select(item => item.ClosingJournalLineId!.Value).OrderBy(item => item).ToArray());
        }

        var reversed = await fixture.Persistence.ReverseYearEndAsync(
            fixture.Context("tenant.finance.year-end.reverse"),
            new FinanceYearEndActionCommand(fixture.CompanyId, posted.Value.Id, posted.Value.Version, "MESP-135 year end reverse", Guid.NewGuid(), "yearend-reverse", "yearend-reverse"));
        Assert.True(reversed.Succeeded, reversed.Code);
        Assert.NotNull(reversed.Value!.ReversalJournalId);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var reopenedPeriod = await db.FiscalPeriods.SingleAsync(item => item.Id == period.Id);
            Assert.Equal(FinanceFiscalPeriodState.Open, reopenedPeriod.State);
            var reversalJournal = await db.Journals.SingleAsync(item => item.Id == reversed.Value.ReversalJournalId);
            Assert.Equal(FinanceJournalStatus.Posted, reversalJournal.Status);
            Assert.Equal(posted.Value.ClosingJournalId, reversalJournal.ReversalOfJournalId);
        }
    }

    [Fact]
    public async Task Close_readiness_and_reconciliation_thread_the_durable_asOf_date_into_subledger_reconciliation()
    {
        var spy = new SpySettlementPersistence();
        await using var fixture = await SqliteFixture.CreateAsync(settlement: spy);
        var period = await fixture.CreateOpenPeriodAsync();

        var requestedAsOfDate = new DateOnly(2026, 1, 20);
        await fixture.Persistence.QueryReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, requestedAsOfDate);
        Assert.Contains(requestedAsOfDate, spy.RequestedAsOfDates);

        spy.RequestedAsOfDates.Clear();
        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Contains(period.EndDate, spy.RequestedAsOfDates);
        Assert.All(spy.RequestedAsOfDates, date => Assert.Equal(period.EndDate, date));
    }

    [Fact]
    public async Task QueryReconciliation_covers_all_five_evidence_scopes_and_uses_worst_status_severity()
    {
        var settlementSpy = new SpySettlementPersistence();
        var mesp134Spy = new SpyMesp134Persistence();
        await using var fixture = await SqliteFixture.CreateAsync(settlement: settlementSpy, mesp134: mesp134Spy);
        var asOfDate = new DateOnly(2026, 1, 31);

        settlementSpy.OnReconciliation = (_, _) => [];
        mesp134Spy.OnTax = (_, _) => [];
        mesp134Spy.OnFx = (_, _) => [];
        mesp134Spy.OnUnrealizedFx = (_, _) => [new FinanceUnrealizedFxReconciliationRecord(Guid.NewGuid(), Guid.NewGuid(), fixture.CompanyId, Guid.NewGuid(), "OpenItem", 10m, 10m, FinanceFxDirection.Gain, FinanceEvidenceStatus.Reconciled, Guid.NewGuid(), null, null, null, null)];
        mesp134Spy.OnReportingCurrency = (_, _) => [new FinanceReportingCurrencyReconciliationRecord(Guid.NewGuid(), fixture.CompanyId, "SAR", 10m, "USD", 20m, 20m, null, null, null, FinanceEvidenceStatus.Reconciled)];

        var reconciled = await fixture.Persistence.QueryReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, asOfDate);
        Assert.Contains(reconciled.Items, item => item.Scope == "Unrealized FX");
        Assert.Contains(reconciled.Items, item => item.Scope == "Reporting Currency");
        Assert.Equal(FinanceReconciliationViewStatus.Reconciled, reconciled.OverallStatus);

        mesp134Spy.OnUnrealizedFx = (_, _) => [new FinanceUnrealizedFxReconciliationRecord(Guid.NewGuid(), Guid.NewGuid(), fixture.CompanyId, Guid.NewGuid(), "OpenItem", 10m, 5m, FinanceFxDirection.Gain, FinanceEvidenceStatus.NotCaptured, null, null, null, null, null)];
        var legacy = await fixture.Persistence.QueryReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, asOfDate);
        Assert.Equal(FinanceReconciliationViewStatus.LegacyWithoutEvidence, legacy.OverallStatus);

        mesp134Spy.OnUnrealizedFx = (_, _) => [new FinanceUnrealizedFxReconciliationRecord(Guid.NewGuid(), Guid.NewGuid(), fixture.CompanyId, Guid.NewGuid(), "OpenItem", 10m, 5m, FinanceFxDirection.Gain, FinanceEvidenceStatus.PendingMapping, null, null, null, null, null)];
        var pending = await fixture.Persistence.QueryReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, asOfDate);
        Assert.Equal(FinanceReconciliationViewStatus.Pending, pending.OverallStatus);
    }

    [Fact]
    public async Task Settlement_reconciliation_uses_durable_posting_and_reversal_dates_for_ap_ar_and_as_of_cash_history()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var arControl = await fixture.CreateAccountAsync("M135-AR-CONTROL", FinanceAccountType.Asset);
        var other = await fixture.CreateAccountAsync("M135-SETTLEMENT-OTHER", FinanceAccountType.Expense);
        var tenant = new TenantId(TenantId);
        var jan31 = new DateOnly(2026, 1, 31);
        var feb15 = new DateOnly(2026, 2, 15);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var supplierId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var apItemId = Guid.NewGuid();
        var arItemId = Guid.NewGuid();
        var apDocumentId = Guid.NewGuid();
        var arDocumentId = Guid.NewGuid();
        var apCashId = Guid.NewGuid();
        var arCashId = Guid.NewGuid();
        var apMethodId = Guid.NewGuid();
        var arMethodId = Guid.NewGuid();
        var cashLinkedAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var apControlId = seed.Lines.Single(item => item.Credit > 0m).AccountId;

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var cashAccount = await db.Accounts.SingleAsync(item => item.Id == cashLinkedAccountId);
            var apAccount = await db.Accounts.SingleAsync(item => item.Id == apControlId);
            var arAccount = await db.Accounts.SingleAsync(item => item.Id == arControl.Id);
            var otherAccount = await db.Accounts.SingleAsync(item => item.Id == other.Id);
            db.PaymentMethods.Add(new FinancePaymentMethodEntity(tenant, new FinancePaymentMethodCommand(fixture.CompanyId, "M135-AP-PM", "MESP-135 AP payment", null, FinancePaymentMethodDirection.Payment, true, false, new DateOnly(2025, 1, 1), null, apMethodId, null, "ap-method", "ap-method")));
            db.PaymentMethods.Add(new FinancePaymentMethodEntity(tenant, new FinancePaymentMethodCommand(fixture.CompanyId, "M135-AR-PM", "MESP-135 AR receipt", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2025, 1, 1), null, arMethodId, null, "ar-method", "ar-method")));
            db.CashAccounts.Add(new FinanceCashAccountEntity(tenant, new FinanceCashAccountCommand(fixture.CompanyId, "M135-AP-CASH", "MESP-135 AP cash", null, FinanceCashAccountKind.Bank, "USD", cashLinkedAccountId, null, new DateOnly(2025, 1, 1), null, apCashId, null, "ap-cash", "ap-cash"), "USD"));
            db.CashAccounts.Add(new FinanceCashAccountEntity(tenant, new FinanceCashAccountCommand(fixture.CompanyId, "M135-AR-CASH", "MESP-135 AR cash", null, FinanceCashAccountKind.Bank, "USD", cashLinkedAccountId, null, new DateOnly(2025, 1, 1), null, arCashId, null, "ar-cash", "ar-cash"), "USD"));

            var apRecognition = PostedJournal(tenant, fixture.CompanyId, period, otherAccount, apAccount, new DateOnly(2026, 1, 15), 2, "m135-ap-recognition", 100m, 375m);
            var arRecognition = PostedJournal(tenant, fixture.CompanyId, period, arAccount, otherAccount, new DateOnly(2026, 1, 15), 3, "m135-ar-recognition", 100m, 375m);
            var apSettlement = PostedJournal(tenant, fixture.CompanyId, period, apAccount, cashAccount, new DateOnly(2026, 1, 20), 4, "supplier-payment.v1", 100m, 375m);
            var apSettlementReversal = PostedJournal(tenant, fixture.CompanyId, period, cashAccount, apAccount, new DateOnly(2026, 2, 10), 5, "finance-reversal.v1", 100m, 375m);
            apSettlementReversal.LinkOriginal(apSettlement.Id);
            var arSettlement = PostedJournal(tenant, fixture.CompanyId, period, cashAccount, arAccount, new DateOnly(2026, 1, 20), 6, "customer-receipt.v1", 100m, 375m);
            var arSettlementReversal = PostedJournal(tenant, fixture.CompanyId, period, arAccount, cashAccount, new DateOnly(2026, 1, 30), 7, "finance-reversal.v1", 100m, 375m);
            arSettlementReversal.LinkOriginal(arSettlement.Id);
            var apAllocationJournal = PostedJournal(tenant, fixture.CompanyId, period, apAccount, cashAccount, new DateOnly(2026, 1, 20), 8, "supplier-payment.v1", 100m, 375m);
            var arAllocationJournal = PostedJournal(tenant, fixture.CompanyId, period, cashAccount, arAccount, new DateOnly(2026, 1, 20), 9, "customer-receipt.v1", 100m, 375m);
            var apAllocationReversalJournal = PostedJournal(tenant, fixture.CompanyId, period, cashAccount, apAccount, today, 10, "finance-reversal.v1", 100m, 375m);
            apAllocationReversalJournal.LinkOriginal(apAllocationJournal.Id);
            var arAllocationReversalJournal = PostedJournal(tenant, fixture.CompanyId, period, arAccount, cashAccount, today, 11, "finance-reversal.v1", 100m, 375m);
            arAllocationReversalJournal.LinkOriginal(arAllocationJournal.Id);

            var apItem = new FinanceOpenItemEntity(tenant, apItemId, FinanceOpenItemKind.Payable, fixture.CompanyId, supplierId, null, "procurement-supplier-invoice.v1", apItemId, 1, apItemId, 1, "M135-AP", new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10), "USD", 100m, "SAR", 375m, 3.75m, null, null, null, null, null, null, null);
            apItem.SetRecognition(FinanceOpenItemRecognitionState.Recognized, apRecognition.Id);
            var arItem = new FinanceOpenItemEntity(tenant, arItemId, FinanceOpenItemKind.Receivable, fixture.CompanyId, null, customerId, "manual-ar.v1", arItemId, 1, arItemId, 1, "M135-AR", new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10), "USD", 100m, "SAR", 375m, 3.75m, null, null, null, null, null, null, null);
            arItem.SetRecognition(FinanceOpenItemRecognitionState.Recognized, arRecognition.Id);
            db.OpenItems.AddRange(apItem, arItem);

            var apDocument = SettlementDocument(tenant, fixture.CompanyId, apDocumentId, FinancePaymentMethodDirection.Payment, supplierId, null, apCashId, apMethodId, apSettlement, apSettlementReversal);
            var arDocument = SettlementDocument(tenant, fixture.CompanyId, arDocumentId, FinancePaymentMethodDirection.Receipt, null, customerId, arCashId, arMethodId, arSettlement, arSettlementReversal);
            db.SettlementDocuments.AddRange(apDocument, arDocument);

            var apAllocation = new FinanceAllocationEntity(tenant, new FinanceAllocationCommand(apDocumentId, apItemId, 100m, new DateOnly(2026, 1, 20), "MESP-135 AP full settlement", Guid.NewGuid(), "ap-allocation", "ap-allocation"), fixture.CompanyId, "USD", 375m, ActorId);
            apAllocation.SetJournal(apAllocationJournal.Id);
            var arAllocation = new FinanceAllocationEntity(tenant, new FinanceAllocationCommand(arDocumentId, arItemId, 100m, new DateOnly(2026, 1, 20), "MESP-135 AR full settlement", Guid.NewGuid(), "ar-allocation", "ar-allocation"), fixture.CompanyId, "USD", 375m, ActorId);
            arAllocation.SetJournal(arAllocationJournal.Id);
            var apAllocationReversal = new FinanceAllocationEntity(tenant, new FinanceAllocationReversalCommand(apAllocation.Id, apAllocation.Version, "MESP-135 AP allocation reversal", Guid.NewGuid(), "ap-allocation-reversal", "ap-allocation-reversal"), apAllocation, fixture.CompanyId, ActorId);
            apAllocationReversal.SetJournal(apAllocationReversalJournal.Id);
            var arAllocationReversal = new FinanceAllocationEntity(tenant, new FinanceAllocationReversalCommand(arAllocation.Id, arAllocation.Version, "MESP-135 AR allocation reversal", Guid.NewGuid(), "ar-allocation-reversal", "ar-allocation-reversal"), arAllocation, fixture.CompanyId, ActorId);
            arAllocationReversal.SetJournal(arAllocationReversalJournal.Id);
            db.Journals.AddRange(apRecognition, arRecognition, apSettlement, apSettlementReversal, arSettlement, arSettlementReversal, apAllocationJournal, arAllocationJournal, apAllocationReversalJournal, arAllocationReversalJournal);
            db.Allocations.AddRange(apAllocation, arAllocation, apAllocationReversal, arAllocationReversal);
            await db.SaveChangesAsync();
        }

        var settlement = new FinanceSettlementPersistence(
            fixture.Options,
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantId, fixture.CompanyId, "MESP-135 Settlement Company", "SAR")]),
            new UnavailableMasterDataExchangeRatePersistence(),
            new UnavailableCustomerPersistence(),
            new UnavailableSupplierPersistence(),
            new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            new UnavailableFinanceSupplierInvoiceSourceProvider());
        var beforeReversal = await settlement.GetReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, jan31);
        var afterFebruaryReversal = await settlement.GetReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, feb15);
        var afterAllocationReversals = await settlement.GetReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, today);

        AssertReconciled(Assert.Single(beforeReversal, item => item.Kind == FinanceOpenItemKind.Payable), 0m, 0m, jan31);
        AssertReconciled(Assert.Single(beforeReversal, item => item.Kind == FinanceOpenItemKind.Receivable), 0m, 0m, jan31);
        Assert.Equal(-375m, Assert.Single(beforeReversal, item => item.Scope.StartsWith("Cash/Bank M135-AP-CASH", StringComparison.Ordinal)).SubledgerAmount);
        Assert.Equal(0m, Assert.Single(beforeReversal, item => item.Scope.StartsWith("Cash/Bank M135-AR-CASH", StringComparison.Ordinal)).SubledgerAmount);
        Assert.Equal(0m, Assert.Single(afterFebruaryReversal, item => item.Scope.StartsWith("Cash/Bank M135-AP-CASH", StringComparison.Ordinal)).SubledgerAmount);
        Assert.Equal(0m, Assert.Single(afterFebruaryReversal, item => item.Scope.StartsWith("Cash/Bank M135-AR-CASH", StringComparison.Ordinal)).SubledgerAmount);
        AssertReconciled(Assert.Single(afterAllocationReversals, item => item.Kind == FinanceOpenItemKind.Payable), 375m, 375m, today);
        AssertReconciled(Assert.Single(afterAllocationReversals, item => item.Kind == FinanceOpenItemKind.Receivable), 375m, 375m, today);
    }

    [Fact]
    public async Task Valid_reversed_evidence_reconciles_the_view_and_does_not_block_close_readiness()
    {
        var settlementSpy = new SpySettlementPersistence();
        var mesp134Spy = new SpyMesp134Persistence();
        await using var fixture = await SqliteFixture.CreateAsync(settlement: settlementSpy, mesp134: mesp134Spy);
        var period = await fixture.CreateOpenPeriodAsync();
        var asOfDate = period.EndDate;

        settlementSpy.OnReconciliation = (_, _) => [];
        mesp134Spy.OnTax = (_, _) => [new FinanceTaxAccountingReconciliationRecord(Guid.NewGuid(), fixture.CompanyId, Guid.NewGuid(), Guid.NewGuid(), 10m, 10m, FinanceEvidenceStatus.Reversed, Guid.NewGuid(), Guid.NewGuid())];
        mesp134Spy.OnFx = (_, _) => [new FinanceFxReconciliationRecord(Guid.NewGuid(), fixture.CompanyId, 10m, 10m, FinanceFxDirection.Gain, FinanceEvidenceStatus.Reversed, Guid.NewGuid(), null, null, Guid.NewGuid())];
        mesp134Spy.OnUnrealizedFx = (_, _) => [new FinanceUnrealizedFxReconciliationRecord(Guid.NewGuid(), Guid.NewGuid(), fixture.CompanyId, Guid.NewGuid(), "OpenItem", 10m, 10m, FinanceFxDirection.Gain, FinanceEvidenceStatus.Reversed, Guid.NewGuid(), Guid.NewGuid(), null, null, null)];
        mesp134Spy.OnReportingCurrency = (_, _) => [];

        var reconciled = await fixture.Persistence.QueryReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, asOfDate);
        Assert.Equal(FinanceReconciliationViewStatus.Reconciled, Assert.Single(reconciled.Items, item => item.Scope == "Tax").Status);
        Assert.Equal(FinanceReconciliationViewStatus.Reconciled, Assert.Single(reconciled.Items, item => item.Scope == "Realized FX").Status);
        Assert.Equal(FinanceReconciliationViewStatus.Reconciled, Assert.Single(reconciled.Items, item => item.Scope == "Unrealized FX").Status);
        Assert.Equal(FinanceReconciliationViewStatus.Reconciled, reconciled.OverallStatus);

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Checks.Single(item => item.Code == "tax_reconciliation").Status);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value.Checks.Single(item => item.Code == "realized_fx_reconciliation").Status);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value.Checks.Single(item => item.Code == "unrealized_fx_reconciliation").Status);

        mesp134Spy.OnTax = (_, _) => [new FinanceTaxAccountingReconciliationRecord(Guid.NewGuid(), fixture.CompanyId, Guid.NewGuid(), Guid.NewGuid(), 10m, 10m, FinanceEvidenceStatus.PendingMapping, Guid.NewGuid(), null)];
        mesp134Spy.OnFx = (_, _) => [new FinanceFxReconciliationRecord(Guid.NewGuid(), fixture.CompanyId, 10m, 10m, FinanceFxDirection.Gain, FinanceEvidenceStatus.PendingMapping, Guid.NewGuid(), null, null, null)];
        mesp134Spy.OnUnrealizedFx = (_, _) => [new FinanceUnrealizedFxReconciliationRecord(Guid.NewGuid(), Guid.NewGuid(), fixture.CompanyId, Guid.NewGuid(), "OpenItem", 10m, 10m, FinanceFxDirection.Gain, FinanceEvidenceStatus.PendingMapping, Guid.NewGuid(), null, null, null, null)];
        var invalid = await fixture.Persistence.QueryReconciliationAsync(fixture.Context("tenant.finance.reconciliation.close"), fixture.CompanyId, asOfDate);
        Assert.All(invalid.Items.Where(item => item.Scope is "Tax" or "Realized FX" or "Unrealized FX"), item => Assert.Equal(FinanceReconciliationViewStatus.Pending, item.Status));
        Assert.Equal(FinanceReconciliationViewStatus.Pending, invalid.OverallStatus);
        var blocked = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(blocked.Succeeded, blocked.Code);
        Assert.All(blocked.Value!.Checks.Where(item => item.Code is "tax_reconciliation" or "realized_fx_reconciliation" or "unrealized_fx_reconciliation"), item => Assert.Equal(FinanceCloseCheckStatus.Blocked, item.Status));
    }

    [Fact]
    public async Task Revaluation_readiness_check_nets_allocations_against_foreign_open_item_exposure()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var journal = await fixture.CreatePostedJournalAsync(period);
        var linkedAccountId = journal.Lines.Single(item => item.Debit > 0m).AccountId;
        var openItemId = Guid.NewGuid();
        var settlementDocumentId = Guid.NewGuid();
        var tenantId = new TenantId(TenantId);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var paymentMethod = new FinancePaymentMethodEntity(tenantId, new FinancePaymentMethodCommand(fixture.CompanyId, "BLOCKER-F-PM", "Blocker F payment method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "pm-key", "pm-key"));
            db.PaymentMethods.Add(paymentMethod);
            var cashAccount = new FinanceCashAccountEntity(tenantId, new FinanceCashAccountCommand(fixture.CompanyId, "BLOCKER-F-CASH", "Blocker F cash account", null, FinanceCashAccountKind.Bank, "USD", linkedAccountId, null, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "cash-key", "cash-key"), "USD");
            db.CashAccounts.Add(cashAccount);
            db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(tenantId, new FinanceMonetaryPolicyCommand(fixture.CompanyId, null, 2, "AwayFromZero", true, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), "policy-key", "policy-key"), "SAR", null, 1));
            var openItem = new FinanceOpenItemEntity(tenantId, openItemId, FinanceOpenItemKind.Receivable, fixture.CompanyId, null, null, "manual-ar.v1", openItemId, 1, openItemId, 1, "BLOCKER-F", new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10), "USD", 100m, "SAR", 375m, 3.75m, null, null, null, null, null, null, null);
            openItem.SetRecognition(FinanceOpenItemRecognitionState.Recognized, Guid.NewGuid());
            db.OpenItems.Add(openItem);
            var settlementDocument = new FinanceSettlementDocumentEntity(tenantId, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, fixture.CompanyId, null, null, cashAccount.Id, paymentMethod.Id, new DateOnly(2026, 1, 25), "USD", 100m, 375m, 3.75m, null, null, null, null, null, settlementDocumentId, "doc-key", "doc-key"), "USD", "SAR", 375m, ActorId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(settlementDocument);
            await db.SaveChangesAsync();
        }

        var blocked = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(blocked.Succeeded, blocked.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, blocked.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            db.Allocations.Add(new FinanceAllocationEntity(tenantId, new FinanceAllocationCommand(settlementDocumentId, openItemId, 100m, new DateOnly(2026, 1, 25), "full settlement", Guid.NewGuid(), "alloc-key", "alloc-key"), fixture.CompanyId, "USD", 375m, ActorId));
            await db.SaveChangesAsync();
        }

        var settled = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(settled.Succeeded, settled.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, settled.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Revaluation_readiness_sees_exposure_for_a_foreign_settlement_posted_before_period_end_with_no_reversal()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var journal = await fixture.CreatePostedJournalAsync(period);
        var debitAccountId = journal.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = journal.Lines.Single(item => item.Credit > 0m).AccountId;
        var settlementDocumentId = Guid.NewGuid();
        var tenantId = new TenantId(TenantId);

        var settlementJournal = await fixture.CreateDatedJournalAsync(debitAccountId, creditAccountId, new DateOnly(2026, 1, 25));

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var paymentMethod = new FinancePaymentMethodEntity(tenantId, new FinancePaymentMethodCommand(fixture.CompanyId, "BLOCKER-B2-1-PM", "Blocker 2 scenario 1 payment method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "pm-key-b2-1", "pm-key-b2-1"));
            db.PaymentMethods.Add(paymentMethod);
            var cashAccount = new FinanceCashAccountEntity(tenantId, new FinanceCashAccountCommand(fixture.CompanyId, "BLOCKER-B2-1-CASH", "Blocker 2 scenario 1 cash account", null, FinanceCashAccountKind.Bank, "USD", debitAccountId, null, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "cash-key-b2-1", "cash-key-b2-1"), "USD");
            db.CashAccounts.Add(cashAccount);
            db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(tenantId, new FinanceMonetaryPolicyCommand(fixture.CompanyId, null, 2, "AwayFromZero", true, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), "policy-key-b2-1", "policy-key-b2-1"), "SAR", null, 1));
            var settlementDocument = new FinanceSettlementDocumentEntity(tenantId, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, fixture.CompanyId, null, null, cashAccount.Id, paymentMethod.Id, new DateOnly(2026, 1, 25), "USD", 100m, 375m, 3.75m, null, null, null, null, null, settlementDocumentId, "doc-key-b2-1", "doc-key-b2-1"), "USD", "SAR", 375m, ActorId, DateTimeOffset.UtcNow);
            settlementDocument.SetPostedJournal(settlementJournal.Id);
            settlementDocument.SetStatus(FinanceSettlementDocumentStatus.Posted, ActorId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(settlementDocument);
            await db.SaveChangesAsync();
        }

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Revaluation_readiness_at_period_end_is_unchanged_by_a_settlement_reversal_posted_after_period_end()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (january, _) = await fixture.CreateOpenJanuaryAndFebruaryPeriodsAsync();
        var journal = await fixture.CreatePostedJournalAsync(january);
        var debitAccountId = journal.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = journal.Lines.Single(item => item.Credit > 0m).AccountId;
        var settlementDocumentId = Guid.NewGuid();
        var tenantId = new TenantId(TenantId);

        var settlementJournal = await fixture.CreateDatedJournalAsync(debitAccountId, creditAccountId, new DateOnly(2026, 1, 25));
        var reversalJournal = await fixture.CreateDatedJournalAsync(creditAccountId, debitAccountId, new DateOnly(2026, 2, 10));

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var paymentMethod = new FinancePaymentMethodEntity(tenantId, new FinancePaymentMethodCommand(fixture.CompanyId, "BLOCKER-B2-2-PM", "Blocker 2 scenario 2 payment method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "pm-key-b2-2", "pm-key-b2-2"));
            db.PaymentMethods.Add(paymentMethod);
            var cashAccount = new FinanceCashAccountEntity(tenantId, new FinanceCashAccountCommand(fixture.CompanyId, "BLOCKER-B2-2-CASH", "Blocker 2 scenario 2 cash account", null, FinanceCashAccountKind.Bank, "USD", debitAccountId, null, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "cash-key-b2-2", "cash-key-b2-2"), "USD");
            db.CashAccounts.Add(cashAccount);
            db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(tenantId, new FinanceMonetaryPolicyCommand(fixture.CompanyId, null, 2, "AwayFromZero", true, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), "policy-key-b2-2", "policy-key-b2-2"), "SAR", null, 1));
            var settlementDocument = new FinanceSettlementDocumentEntity(tenantId, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, fixture.CompanyId, null, null, cashAccount.Id, paymentMethod.Id, new DateOnly(2026, 1, 25), "USD", 100m, 375m, 3.75m, null, null, null, null, null, settlementDocumentId, "doc-key-b2-2", "doc-key-b2-2"), "USD", "SAR", 375m, ActorId, DateTimeOffset.UtcNow);
            settlementDocument.SetPostedJournal(settlementJournal.Id);
            settlementDocument.SetStatus(FinanceSettlementDocumentStatus.Posted, ActorId, DateTimeOffset.UtcNow);
            settlementDocument.SetReversal(reversalJournal.Id);
            settlementDocument.SetStatus(FinanceSettlementDocumentStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(settlementDocument);
            await db.SaveChangesAsync();
        }

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Revaluation_readiness_no_longer_sees_exposure_once_the_reversal_is_effective_by_period_end()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var journal = await fixture.CreatePostedJournalAsync(period);
        var debitAccountId = journal.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = journal.Lines.Single(item => item.Credit > 0m).AccountId;
        var settlementDocumentId = Guid.NewGuid();
        var tenantId = new TenantId(TenantId);

        var settlementJournal = await fixture.CreateDatedJournalAsync(debitAccountId, creditAccountId, new DateOnly(2026, 1, 20));
        var reversalJournal = await fixture.CreateDatedJournalAsync(creditAccountId, debitAccountId, new DateOnly(2026, 1, 30));

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var paymentMethod = new FinancePaymentMethodEntity(tenantId, new FinancePaymentMethodCommand(fixture.CompanyId, "BLOCKER-B2-3-PM", "Blocker 2 scenario 3 payment method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "pm-key-b2-3", "pm-key-b2-3"));
            db.PaymentMethods.Add(paymentMethod);
            var cashAccount = new FinanceCashAccountEntity(tenantId, new FinanceCashAccountCommand(fixture.CompanyId, "BLOCKER-B2-3-CASH", "Blocker 2 scenario 3 cash account", null, FinanceCashAccountKind.Bank, "USD", debitAccountId, null, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, "cash-key-b2-3", "cash-key-b2-3"), "USD");
            db.CashAccounts.Add(cashAccount);
            db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(tenantId, new FinanceMonetaryPolicyCommand(fixture.CompanyId, null, 2, "AwayFromZero", true, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), "policy-key-b2-3", "policy-key-b2-3"), "SAR", null, 1));
            var settlementDocument = new FinanceSettlementDocumentEntity(tenantId, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, fixture.CompanyId, null, null, cashAccount.Id, paymentMethod.Id, new DateOnly(2026, 1, 20), "USD", 100m, 375m, 3.75m, null, null, null, null, null, settlementDocumentId, "doc-key-b2-3", "doc-key-b2-3"), "USD", "SAR", 375m, ActorId, DateTimeOffset.UtcNow);
            settlementDocument.SetPostedJournal(settlementJournal.Id);
            settlementDocument.SetStatus(FinanceSettlementDocumentStatus.Posted, ActorId, DateTimeOffset.UtcNow);
            settlementDocument.SetReversal(reversalJournal.Id);
            settlementDocument.SetStatus(FinanceSettlementDocumentStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(settlementDocument);
            await db.SaveChangesAsync();
        }

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Revaluation_readiness_is_satisfied_by_a_period_end_revaluation_effective_at_period_end()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var (january, _) = await fixture.CreateOpenJanuaryAndFebruaryPeriodsAsync();
        var seed = await fixture.CreatePostedJournalAsync(january);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;

        var settlementJournal = await fixture.CreateDatedJournalAsync(debitAccountId, creditAccountId, new DateOnly(2026, 1, 25));
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD4-1", debitAccountId, settlementJournal.Id, new DateOnly(2026, 1, 25));

        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD4-1-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD4-1-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, new DateOnly(2026, 1, 31), 25m);
        await fixture.SeedRevaluationAsync(new DateOnly(2026, 1, 31), rule, revaluationJournal.Id, sourceId, 25m);

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Revaluation_readiness_at_period_end_is_unchanged_by_a_revaluation_reversal_posted_after_period_end()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var (january, _) = await fixture.CreateOpenJanuaryAndFebruaryPeriodsAsync();
        var seed = await fixture.CreatePostedJournalAsync(january);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;

        var settlementJournal = await fixture.CreateDatedJournalAsync(debitAccountId, creditAccountId, new DateOnly(2026, 1, 25));
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD4-2", debitAccountId, settlementJournal.Id, new DateOnly(2026, 1, 25));

        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD4-2-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD4-2-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, new DateOnly(2026, 1, 31), 25m);
        var reversalJournal = await fixture.ReverseJournalAsync(revaluationJournal.Id, new DateOnly(2026, 2, 10));
        await fixture.SeedRevaluationAsync(new DateOnly(2026, 1, 31), rule, revaluationJournal.Id, sourceId, 25m, reversalJournal.Id);

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, new DateOnly(2026, 1, 31));
        var reconciled = Assert.Single(reconciliation);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, reconciled.Status);
        Assert.Equal(revaluationJournal.Id, reconciled.JournalId);
        Assert.Null(reconciled.ReversalJournalId);
    }

    [Fact]
    public async Task Revaluation_readiness_is_blocked_when_the_revaluation_reversal_is_effective_by_period_end()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var (january, _) = await fixture.CreateOpenJanuaryAndFebruaryPeriodsAsync();
        var seed = await fixture.CreatePostedJournalAsync(january);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;

        var settlementJournal = await fixture.CreateDatedJournalAsync(debitAccountId, creditAccountId, new DateOnly(2026, 1, 25));
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD4-3", debitAccountId, settlementJournal.Id, new DateOnly(2026, 1, 25));

        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD4-3-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD4-3-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, new DateOnly(2026, 1, 20), 25m);
        var reversalJournal = await fixture.ReverseJournalAsync(revaluationJournal.Id, new DateOnly(2026, 1, 28));
        await fixture.SeedRevaluationAsync(new DateOnly(2026, 1, 31), rule, revaluationJournal.Id, sourceId, 25m, reversalJournal.Id);

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, new DateOnly(2026, 1, 31));
        var reversed = Assert.Single(reconciliation);
        Assert.Equal(FinanceEvidenceStatus.Reversed, reversed.Status);
        Assert.Equal(reversalJournal.Id, reversed.ReversalJournalId);
    }

    [Fact]
    public async Task Revaluation_readiness_snapshot_at_period_end_is_stable_across_a_later_revaluation_reversal()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var (january, _) = await fixture.CreateOpenJanuaryAndFebruaryPeriodsAsync();
        var seed = await fixture.CreatePostedJournalAsync(january);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;

        var settlementJournal = await fixture.CreateDatedJournalAsync(debitAccountId, creditAccountId, new DateOnly(2026, 1, 25));
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD4-4", debitAccountId, settlementJournal.Id, new DateOnly(2026, 1, 25));

        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD4-4-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD4-4-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, new DateOnly(2026, 1, 31), 25m);
        await fixture.SeedRevaluationAsync(new DateOnly(2026, 1, 31), rule, revaluationJournal.Id, sourceId, 25m);

        var before = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(before.Succeeded, before.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, before.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var reversalJournal = await fixture.ReverseJournalAsync(revaluationJournal.Id, new DateOnly(2026, 2, 10));
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var line = await db.RevaluationLines.SingleAsync(item => item.CompanyId == fixture.CompanyId && item.AsOfDate == new DateOnly(2026, 1, 31));
            line.SetReversal(reversalJournal.Id);
            var batch = await db.RevaluationBatches.SingleAsync(item => item.Id == line.BatchId);
            batch.SetStatus(FinanceRevaluationBatchStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var after = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(after.Succeeded, after.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, after.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
        Assert.Equal(
            before.Value.Checks.Select(item => (item.Code, item.Status, item.Message)),
            after.Value.Checks.Select(item => (item.Code, item.Status, item.Message)));
        Assert.Equal(before.Value.SnapshotFingerprint, after.Value.SnapshotFingerprint);

        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, january.EndDate);
        var stillActive = Assert.Single(reconciliation);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, stillActive.Status);
        Assert.Equal(revaluationJournal.Id, stillActive.JournalId);
        Assert.Null(stillActive.ReversalJournalId);
    }

    [Fact]
    public async Task Revaluation_readiness_treats_zero_effect_foreign_sources_as_evaluated_without_a_journal()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync(3.75m);
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD5-ZERO", seed.Lines.Single(item => item.Debit > 0m).AccountId, seed.Id, new DateOnly(2026, 1, 25));

        var scope = await fixture.Mesp134.EvaluateRevaluationScopeAsync(fixture.Context("tenant.finance.revaluation.scope"), fixture.CompanyId, period.EndDate);
        Assert.True(scope.Succeeded, scope.Code);
        var source = Assert.Single(scope.Value!.Sources);
        Assert.Equal(sourceId, source.SourceId);
        Assert.Equal(0m, source.Difference);
        Assert.Equal(FinanceFxDirection.Zero, source.Direction);
        Assert.NotEmpty(scope.Value.Fingerprint);

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        await using var db = new FinanceDbContext(fixture.Options, fixture.TenantContext);
        Assert.Equal(0, await db.RevaluationLines.CountAsync(item => item.CompanyId == fixture.CompanyId && item.AsOfDate == period.EndDate));
    }

    [Fact]
    public async Task Revaluation_readiness_requires_one_authoritative_line_for_each_non_zero_source_and_rejects_duplicates()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var linkedAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var firstSourceId = await fixture.SeedForeignExposureAsync("HOLD5-COVER-1", linkedAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var secondSourceId = await fixture.SeedForeignExposureAsync("HOLD5-COVER-2", linkedAccountId, seed.Id, new DateOnly(2026, 1, 26));

        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD5-COVER-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD5-COVER-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var firstRevaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        var secondRevaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationBatchAsync(period.EndDate, rule, new Dictionary<Guid, Guid>
        {
            [firstSourceId] = firstRevaluationJournal.Id,
            [secondSourceId] = secondRevaluationJournal.Id
        });

        var ready = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(ready.Succeeded, ready.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, ready.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var duplicateJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationBatchAsync(period.EndDate, rule, new Dictionary<Guid, Guid>
        {
            [firstSourceId] = duplicateJournal.Id,
            [secondSourceId] = duplicateJournal.Id
        });

        var duplicate = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(duplicate.Succeeded, duplicate.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, duplicate.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Revaluation_readiness_blocks_a_late_foreign_source()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var linkedAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD5-LATE", linkedAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD5-LATE-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD5-LATE-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, revaluationJournal.Id, sourceId, 25m);

        var initial = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(initial.Succeeded, initial.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, initial.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var lateSourceId = await fixture.SeedForeignExposureAsync("HOLD5-LATE-NEW", linkedAccountId, seed.Id, new DateOnly(2026, 1, 26));
        var lateScope = await fixture.Mesp134.EvaluateRevaluationScopeAsync(fixture.Context("tenant.finance.revaluation.scope"), fixture.CompanyId, period.EndDate);
        Assert.True(lateScope.Succeeded, lateScope.Code);
        Assert.Contains(lateScope.Value!.Sources, item => item.SourceId == lateSourceId);
        Assert.NotEqual(initial.Value.SnapshotFingerprint, lateScope.Value.Fingerprint);

        var late = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(late.Succeeded, late.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, late.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

    }

    [Fact]
    public async Task Revaluation_readiness_blocks_stale_settlement_evidence_after_a_ready_close_snapshot()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var linkedAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD5-STALE", linkedAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD5-STALE-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD5-STALE-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, revaluationJournal.Id, sourceId, 25m);

        var initial = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(initial.Succeeded, initial.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, initial.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var document = await db.SettlementDocuments.SingleAsync(item => item.Id == sourceId);
            document.Edit(
                new FinanceSettlementDocumentCommand(
                    document.Direction,
                    document.CompanyId,
                    document.SupplierId,
                    document.CustomerId,
                    document.CashAccountId,
                    document.PaymentMethodId,
                    document.DocumentDate,
                    document.CurrencyCode,
                    document.Amount + 1m,
                    document.FunctionalAmount + 3.75m,
                    document.ExchangeRate,
                    document.ExchangeRateId,
                    document.ExchangeRateVersionId,
                    document.ExchangeRateVersionNumber,
                    document.ExternalReference,
                    document.Description,
                    document.Id,
                    $"stale-{document.Id:N}",
                    $"stale-{document.Id:N}"),
                document.CurrencyCode,
                document.FunctionalAmount + 3.75m);
            await db.SaveChangesAsync();
        }

        var stale = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(stale.Succeeded, stale.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, stale.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Revaluation_readiness_historical_as_of_snapshot_is_stable_after_a_later_settlement_reversal()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var (january, _) = await fixture.CreateOpenJanuaryAndFebruaryPeriodsAsync();
        var seed = await fixture.CreatePostedJournalAsync(january);
        var linkedAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD5-HISTORICAL", linkedAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD5-HISTORICAL-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD5-HISTORICAL-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, january.EndDate, 25m);
        await fixture.SeedRevaluationAsync(january.EndDate, rule, revaluationJournal.Id, sourceId, 25m);

        var before = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(before.Succeeded, before.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, before.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var laterReversal = await fixture.CreateDatedJournalAsync(linkedAccountId, seed.Lines.Single(item => item.Credit > 0m).AccountId, new DateOnly(2026, 2, 10), 100m);
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var document = await db.SettlementDocuments.SingleAsync(item => item.Id == sourceId);
            document.SetReversal(laterReversal.Id);
            document.SetStatus(FinanceSettlementDocumentStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var after = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, january.Id));
        Assert.True(after.Succeeded, after.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, after.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
        Assert.Equal(before.Value.SnapshotFingerprint, after.Value.SnapshotFingerprint);
    }

    [Fact]
    public async Task Hold6_REPLACE01_valid_reversed_revaluation_is_inactive_while_replacement_is_the_only_active_candidate()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD6-REPLACE", debitAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD6-REPLACE-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD6-REPLACE-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);

        var originalJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, originalJournal.Id, sourceId, 25m);
        var before = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(before.Succeeded, before.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, before.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var reversalJournal = await fixture.ReverseJournalAsync(originalJournal.Id, new DateOnly(2026, 1, 30));
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var originalLine = await db.RevaluationLines.SingleAsync(item => item.CompanyId == fixture.CompanyId && item.SourceId == sourceId && item.AsOfDate == period.EndDate);
            originalLine.SetReversal(reversalJournal.Id);
            var originalBatch = await db.RevaluationBatches.SingleAsync(item => item.Id == originalLine.BatchId);
            originalBatch.SetStatus(FinanceRevaluationBatchStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var replacementJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, replacementJournal.Id, sourceId, 25m);

        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, period.EndDate);
        Assert.Equal(1, reconciliation.Count(item => item.Status == FinanceEvidenceStatus.Reversed));
        Assert.Equal(1, reconciliation.Count(item => item.Status == FinanceEvidenceStatus.Reconciled));
        Assert.Contains(reconciliation, item => item.Status == FinanceEvidenceStatus.Reversed && item.JournalId == originalJournal.Id && item.ReversalJournalId == reversalJournal.Id);
        Assert.Contains(reconciliation, item => item.Status == FinanceEvidenceStatus.Reconciled && item.JournalId == replacementJournal.Id && item.ReversalJournalId is null);

        var after = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        var repeated = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(after.Succeeded, after.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, after.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
        Assert.NotEqual(before.Value.SnapshotFingerprint, after.Value.SnapshotFingerprint);
        Assert.Equal(after.Value.SnapshotFingerprint, repeated.Value!.SnapshotFingerprint);
    }

    [Fact]
    public async Task Hold6_ZERO_REV01_valid_reversed_stale_evidence_is_allowed_when_the_authoritative_source_is_zero_effect()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD6-ZERO-REV", debitAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD6-ZERO-REV-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD6-ZERO-REV-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var originalJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, originalJournal.Id, sourceId, 25m);
        var reversalJournal = await fixture.ReverseJournalAsync(originalJournal.Id, new DateOnly(2026, 1, 30));
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var line = await db.RevaluationLines.SingleAsync(item => item.CompanyId == fixture.CompanyId && item.SourceId == sourceId && item.AsOfDate == period.EndDate);
            line.SetReversal(reversalJournal.Id);
            var batch = await db.RevaluationBatches.SingleAsync(item => item.Id == line.BatchId);
            batch.SetStatus(FinanceRevaluationBatchStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            var document = await db.SettlementDocuments.SingleAsync(item => item.Id == sourceId);
            document.Edit(
                new FinanceSettlementDocumentCommand(
                    document.Direction, document.CompanyId, document.SupplierId, document.CustomerId, document.CashAccountId, document.PaymentMethodId,
                    document.DocumentDate, document.CurrencyCode, document.Amount, 400m, document.ExchangeRate, document.ExchangeRateId,
                    document.ExchangeRateVersionId, document.ExchangeRateVersionNumber, document.ExternalReference, document.Description,
                    document.Id, $"hold6-zero-rev-{document.Id:N}", $"hold6-zero-rev-{document.Id:N}"), document.CurrencyCode, 400m);
            await db.SaveChangesAsync();
        }

        var scope = await fixture.Mesp134.EvaluateRevaluationScopeAsync(fixture.Context("tenant.finance.revaluation.scope"), fixture.CompanyId, period.EndDate);
        Assert.True(scope.Succeeded, scope.Code);
        Assert.Equal(0m, Assert.Single(scope.Value!.Sources).Difference);
        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, period.EndDate);
        Assert.Equal(FinanceEvidenceStatus.Reversed, Assert.Single(reconciliation).Status);

        int journalCount;
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext)) journalCount = await db.Journals.CountAsync();
        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            Assert.Equal(journalCount, await db.Journals.CountAsync());
            Assert.Equal(0, await db.Journals.CountAsync(item => item.SourceContract == "finance-revaluation.v1"));
        }
    }

    [Fact]
    public async Task Hold6_BROKEN_REV01_unresolved_reversal_evidence_remains_fail_closed()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD6-BROKEN-REV", debitAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD6-BROKEN-REV-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD6-BROKEN-REV-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, revaluationJournal.Id, sourceId, 25m);
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var line = await db.RevaluationLines.SingleAsync(item => item.CompanyId == fixture.CompanyId && item.SourceId == sourceId && item.AsOfDate == period.EndDate);
            line.SetReversal(Guid.NewGuid());
            var batch = await db.RevaluationBatches.SingleAsync(item => item.Id == line.BatchId);
            batch.SetStatus(FinanceRevaluationBatchStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, period.EndDate);
        Assert.Equal(FinanceEvidenceStatus.PendingMapping, Assert.Single(reconciliation).Status);
        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Hold6_DUP_ACTIVE01_two_active_reconciled_candidates_for_one_source_block_readiness()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD6-DUP-ACTIVE", debitAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD6-DUP-ACTIVE-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD6-DUP-ACTIVE-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var firstJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        var secondJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, firstJournal.Id, sourceId, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, secondJournal.Id, sourceId, 25m);

        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, period.EndDate);
        Assert.Equal(2, reconciliation.Count(item => item.SourceId == sourceId && item.Status == FinanceEvidenceStatus.Reconciled));
        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Hold6_EXTRA_ACTIVE01_active_evidence_for_a_current_zero_effect_source_is_extra_and_blocks()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var seed = await fixture.CreatePostedJournalAsync(period);
        var debitAccountId = seed.Lines.Single(item => item.Debit > 0m).AccountId;
        var creditAccountId = seed.Lines.Single(item => item.Credit > 0m).AccountId;
        var sourceId = await fixture.SeedForeignExposureAsync("HOLD6-EXTRA-ACTIVE", debitAccountId, seed.Id, new DateOnly(2026, 1, 25));
        var fxLoss = await fixture.CreateAccountAsync("M135-HOLD6-EXTRA-ACTIVE-FX-LOSS", FinanceAccountType.Expense);
        var fxGain = await fixture.CreateAccountAsync("M135-HOLD6-EXTRA-ACTIVE-FX-GAIN", FinanceAccountType.Revenue);
        var rule = await fixture.CreateUnrealizedFxPostingRuleAsync(fxLoss.Id, fxGain.Id);
        var revaluationJournal = await fixture.CreateDatedJournalAsync(fxLoss.Id, fxGain.Id, period.EndDate, 25m);
        await fixture.SeedRevaluationAsync(period.EndDate, rule, revaluationJournal.Id, sourceId, 25m);
        await fixture.SetSettlementFunctionalAmountAsync(sourceId, 400m);

        var scope = await fixture.Mesp134.EvaluateRevaluationScopeAsync(fixture.Context("tenant.finance.revaluation.scope"), fixture.CompanyId, period.EndDate);
        Assert.Equal(0m, Assert.Single(scope.Value!.Sources).Difference);
        var reconciliation = await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, period.EndDate);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, Assert.Single(reconciliation).Status);
        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, readiness.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Hold6_TENANT_COMPANY_ISOLATION_effective_candidates_cannot_cross_tenant_or_company_boundaries()
    {
        await using var fixture = await SqliteFixture.CreateWithRevaluationAuthorityAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var otherCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var otherTenantContext = TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("mesp135-hold6-isolation"),
            actorId: ActorId);

        async Task SeedOutOfScopeLineAsync(TenantContext tenantContext, Guid companyId)
        {
            await using var db = new FinanceDbContext(fixture.Options, tenantContext);
            var batch = new FinanceRevaluationBatchEntity(
                tenantContext.TenantId,
                new FinanceRevaluationBatchCommand(companyId, period.EndDate, FinanceRevaluationScopes.ApArAndUnallocatedSettlements, Guid.NewGuid(), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")),
                ActorId,
                DateTimeOffset.UtcNow);
            var rate = new FinanceExchangeRateEvidence(Guid.NewGuid(), Guid.NewGuid(), 1, "USD", "SAR", period.EndDate, 4m, 6, "Configured", "HOLD6 isolation", $"USD->SAR;v1@{period.EndDate:yyyy-MM-dd}", new DateOnly(2026, 1, 1), null);
            var line = new FinanceRevaluationLineEntity(tenantContext.TenantId, Guid.NewGuid(), batch, Guid.NewGuid(), "AR", "USD", 100m, 375m, 400m, 25m, FinanceFxDirection.Gain, rate, sourceSnapshotJson: "{}");
            line.SetJournal(Guid.NewGuid());
            line.SetPostingRule(Guid.NewGuid(), 1);
            batch.Lines.Add(line);
            batch.SetStatus(FinanceRevaluationBatchStatus.Posted, ActorId, DateTimeOffset.UtcNow);
            db.RevaluationBatches.Add(batch);
            await db.SaveChangesAsync();
        }

        await SeedOutOfScopeLineAsync(fixture.TenantContext, otherCompanyId);
        await SeedOutOfScopeLineAsync(otherTenantContext, fixture.CompanyId);
        Assert.Empty(await fixture.Mesp134.ReconcileUnrealizedFxAsync(fixture.Context("tenant.finance.fx.reconcile"), fixture.CompanyId, period.EndDate));

        var noPrimarySource = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(noPrimarySource.Succeeded, noPrimarySource.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, noPrimarySource.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);

        var seed = await fixture.CreatePostedJournalAsync(period);
        await fixture.SeedForeignExposureAsync("HOLD6-PRIMARY", seed.Lines.Single(item => item.Debit > 0m).AccountId, seed.Id, new DateOnly(2026, 1, 25));
        var primaryMissingCoverage = await fixture.Persistence.EvaluateCloseReadinessAsync(fixture.Context("tenant.finance.close.readiness"), new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));
        Assert.True(primaryMissingCoverage.Succeeded, primaryMissingCoverage.Code);
        Assert.Equal(FinanceCloseCheckStatus.Blocked, primaryMissingCoverage.Value!.Checks.Single(item => item.Code == "revaluation_policy").Status);
    }

    [Fact]
    public async Task Correction_reversal_preserves_original_posting_rule_lineage()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var journal = await fixture.CreatePostedJournalAsync(period);
        await fixture.CreateYearEndPostingRuleAsync();

        Guid ruleId; int ruleVersion; byte[] currentVersion;
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            var rule = await db.PostingRules.SingleAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "finance-year-end.v1");
            ruleId = rule.Id; ruleVersion = rule.VersionNumber;
            var entity = await db.Journals.SingleAsync(item => item.Id == journal.Id);
            entity.SetRule(ruleId, ruleVersion);
            await db.SaveChangesAsync();
            currentVersion = await db.Journals.Where(item => item.Id == journal.Id).Select(item => item.Version).SingleAsync();
        }

        var correction = await fixture.Persistence.CorrectJournalAsync(
            fixture.Context("tenant.finance.correction.create"),
            new FinanceCorrectionCommand(fixture.CompanyId, journal.Id, new DateOnly(2026, 1, 20), currentVersion, "Blocker G regression", Guid.NewGuid(), "blocker-g", "blocker-g"));
        Assert.True(correction.Succeeded, correction.Code);
        Assert.Equal(ruleId, correction.Value!.PostingRuleId);
        Assert.Equal(ruleVersion, correction.Value.PostingRuleVersionNumber);
    }

    [Fact]
    public async Task Reporting_currency_allocation_zeroes_out_per_account_across_original_and_exact_reversal()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var journal = await fixture.CreatePostedJournalAsync(period);

        var evidence = new FinanceMonetaryEvidence("SAR", 100m, "SAR", 100m, null, "USD", 27m, null, 100m, 27m, 2, "AwayFromZero", 0m, 0m, FinanceEvidenceStatus.Captured);
        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(new TenantId(TenantId), Guid.NewGuid(), journal.Id, fixture.CompanyId, null, evidence, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var correction = await fixture.Persistence.CorrectJournalAsync(
            fixture.Context("tenant.finance.correction.create"),
            new FinanceCorrectionCommand(fixture.CompanyId, journal.Id, new DateOnly(2026, 1, 20), journal.Version, "Blocker C regression", Guid.NewGuid(), "blocker-c", "blocker-c"));
        Assert.True(correction.Succeeded, correction.Code);

        var lines = await fixture.Persistence.QueryGeneralLedgerAsync(
            fixture.Context("tenant.finance.report.general-ledger"),
            new FinanceGeneralLedgerQuery(fixture.CompanyId, PresentationCurrencyCode: "USD"));

        var assetAccountId = journal.Lines.Single(item => item.Debit > 0m).AccountId;
        var revenueAccountId = journal.Lines.Single(item => item.Credit > 0m).AccountId;
        var originalAssetLine = lines.Single(item => item.JournalId == journal.Id && item.AccountId == assetAccountId);
        var reversalAssetLine = lines.Single(item => item.JournalId == correction.Value!.Id && item.AccountId == assetAccountId);
        var originalRevenueLine = lines.Single(item => item.JournalId == journal.Id && item.AccountId == revenueAccountId);
        var reversalRevenueLine = lines.Single(item => item.JournalId == correction.Value!.Id && item.AccountId == revenueAccountId);

        Assert.Equal(FinanceEvidenceStatus.Reconciled, originalAssetLine.ReportingEvidenceStatus);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, reversalAssetLine.ReportingEvidenceStatus);
        Assert.Equal(0m, (originalAssetLine.ReportingAmount ?? 0m) + (reversalAssetLine.ReportingAmount ?? 0m));
        Assert.Equal(0m, (originalRevenueLine.ReportingAmount ?? 0m) + (reversalRevenueLine.ReportingAmount ?? 0m));
        Assert.Equal(0m, (reversalAssetLine.ReportingAmount ?? 0m) + (reversalRevenueLine.ReportingAmount ?? 0m));
    }

    private static void AssertReconciled(FinanceReconciliationRecord record, decimal subledgerAmount, decimal postedJournalAmount, DateOnly asOfDate)
    {
        Assert.Equal(subledgerAmount, record.SubledgerAmount);
        Assert.Equal(postedJournalAmount, record.PostedJournalAmount);
        Assert.Equal(0m, record.Difference);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, record.Status);
        Assert.Equal(asOfDate, record.AsOfDate);
    }

    private static FinanceJournalEntity PostedJournal(TenantId tenant, Guid companyId, FinanceFiscalPeriodRecord period, FinanceAccountEntity debitAccount, FinanceAccountEntity creditAccount, DateOnly postingDate, long sequence, string sourceContract, decimal amount, decimal functionalAmount)
    {
        var id = Guid.NewGuid();
        var command = new FinanceJournalCommand(companyId, postingDate, postingDate, "USD", 3.75m, null, null, null, sourceContract, "test", null, null, null, sourceContract, [new FinanceJournalLineCommand(debitAccount.Id, amount, 0m, amount, "USD", null, sourceContract), new FinanceJournalLineCommand(creditAccount.Id, 0m, amount, amount, "USD", null, sourceContract)], id, $"{sourceContract}-{id:N}", $"{sourceContract}-{id:N}", FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var journal = new FinanceJournalEntity(tenant, id, command, sequence, "SAR", ActorId, DateTimeOffset.UtcNow);
        journal.SetPeriod(period.FiscalYearId, period.Id);
        journal.SetStatus(FinanceJournalStatus.Posted, ActorId, DateTimeOffset.UtcNow);
        journal.Lines.Add(new FinanceJournalLineEntity(tenant, Guid.NewGuid(), journal.Id, 1, debitAccount, command.Lines[0], null, functionalAmount, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        journal.Lines.Add(new FinanceJournalLineEntity(tenant, Guid.NewGuid(), journal.Id, 2, creditAccount, command.Lines[1], null, 0m, functionalAmount, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        return journal;
    }

    private static FinanceSettlementDocumentEntity SettlementDocument(TenantId tenant, Guid companyId, Guid documentId, FinancePaymentMethodDirection direction, Guid? supplierId, Guid? customerId, Guid cashAccountId, Guid paymentMethodId, FinanceJournalEntity postedJournal, FinanceJournalEntity reversalJournal)
    {
        var command = new FinanceSettlementDocumentCommand(direction, companyId, supplierId, customerId, cashAccountId, paymentMethodId, new DateOnly(2026, 1, 10), "USD", 100m, 375m, 3.75m, null, null, null, null, "MESP-135 historical settlement", documentId, $"document-{documentId:N}", $"document-{documentId:N}");
        var document = new FinanceSettlementDocumentEntity(tenant, command, "USD", "SAR", 375m, ActorId, DateTimeOffset.UtcNow);
        document.SetPostedJournal(postedJournal.Id);
        document.SetReversal(reversalJournal.Id);
        document.SetStatus(FinanceSettlementDocumentStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
        return document;
    }

    private sealed class SpySettlementPersistence : IFinanceSettlementPersistence
    {
        private readonly UnavailableFinanceSettlementPersistence fallback = new();
        internal List<DateOnly> RequestedAsOfDates { get; } = [];
        internal Func<Guid, DateOnly, IReadOnlyList<FinanceReconciliationRecord>>? OnReconciliation;

        public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.GetReconciliationAsync(context, companyId, cancellationToken);
        public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default)
        {
            RequestedAsOfDates.Add(asOfDate);
            IReadOnlyList<FinanceReconciliationRecord> result = OnReconciliation?.Invoke(companyId, asOfDate) ?? [];
            return Task.FromResult(result);
        }
        public Task<IReadOnlyList<FinancePaymentMethodRecord>> ListPaymentMethodsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListPaymentMethodsAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> CreatePaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default) => fallback.CreatePaymentMethodAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> EditPaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default) => fallback.EditPaymentMethodAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> SetPaymentMethodLifecycleAsync(FinanceRequestContext context, Guid methodId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => fallback.SetPaymentMethodLifecycleAsync(context, methodId, companyId, lifecycle, expectedVersion, idempotencyKey, fingerprint, cancellationToken);
        public Task<IReadOnlyList<FinanceCashAccountRecord>> ListCashAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListCashAccountsAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> CreateCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, CancellationToken cancellationToken = default) => fallback.CreateCashAccountAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> EditCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default) => fallback.EditCashAccountAsync(context, command, expectedVersion, cancellationToken);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> SetCashAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => fallback.SetCashAccountLifecycleAsync(context, accountId, companyId, lifecycle, expectedVersion, idempotencyKey, fingerprint, cancellationToken);
        public Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, string resource, Guid resourceId, CancellationToken cancellationToken = default) => fallback.ResolveCompanyIdAsync(context, resource, resourceId, cancellationToken);
        public Task<IReadOnlyList<FinanceOpenItemRecord>> ListOpenItemsAsync(FinanceRequestContext context, FinanceOpenItemKind kind, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListOpenItemsAsync(context, kind, companyId, cancellationToken);
        public Task<FinanceOpenItemRecord?> GetOpenItemAsync(FinanceRequestContext context, Guid itemId, FinanceOpenItemKind? expectedKind = null, CancellationToken cancellationToken = default) => fallback.GetOpenItemAsync(context, itemId, expectedKind, cancellationToken);
        public Task<IReadOnlyList<FinanceApSourceReadyRecord>> ListApSourceReadyAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) => fallback.ListApSourceReadyAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> RecognizeSupplierInvoiceAsync(FinanceRequestContext context, FinanceSupplierInvoiceRecognitionCommand command, CancellationToken cancellationToken = default) => fallback.RecognizeSupplierInvoiceAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateManualReceivableAsync(FinanceRequestContext context, FinanceManualReceivableCommand command, CancellationToken cancellationToken = default) => fallback.CreateManualReceivableAsync(context, command, cancellationToken);
        public Task<FinanceArOpeningPreflightResult> PreflightMigrationArOpeningAsync(FinanceRequestContext context, FinanceMigrationArOpeningCommand command, CancellationToken cancellationToken = default) => fallback.PreflightMigrationArOpeningAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateMigrationArOpeningAsync(FinanceRequestContext context, FinanceMigrationArOpeningCommand command, CancellationToken cancellationToken = default) => fallback.CreateMigrationArOpeningAsync(context, command, cancellationToken);
        public Task<FinanceMigrationArOpeningEvidence?> ReadMigrationArOpeningAsync(FinanceRequestContext context, FinanceMigrationArOpeningCommand command, CancellationToken cancellationToken = default) => fallback.ReadMigrationArOpeningAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>> EvaluateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default) => fallback.EvaluateSalesInvoiceAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default) => fallback.CreateSalesInvoiceAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceSettlementDocumentRecord>> ListSettlementDocumentsAsync(FinanceRequestContext context, FinanceSettlementQuery query, CancellationToken cancellationToken = default) => fallback.ListSettlementDocumentsAsync(context, query, cancellationToken);
        public Task<FinanceSettlementDocumentRecord?> GetSettlementDocumentAsync(FinanceRequestContext context, Guid documentId, FinancePaymentMethodDirection? expectedDirection = null, CancellationToken cancellationToken = default) => fallback.GetSettlementDocumentAsync(context, documentId, expectedDirection, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> CreateSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, CancellationToken cancellationToken = default) => fallback.CreateSettlementDocumentAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> EditSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default) => fallback.EditSettlementDocumentAsync(context, command, expectedVersion, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> TransitionSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, FinanceSettlementDocumentStatus target, CancellationToken cancellationToken = default) => fallback.TransitionSettlementDocumentAsync(context, command, target, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> PostSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, CancellationToken cancellationToken = default) => fallback.PostSettlementDocumentAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> ReverseSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementReversalCommand command, CancellationToken cancellationToken = default) => fallback.ReverseSettlementDocumentAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceAllocationRecord>> ListAllocationsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListAllocationsAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinanceAllocationRecord>> CreateAllocationAsync(FinanceRequestContext context, FinanceAllocationCommand command, CancellationToken cancellationToken = default) => fallback.CreateAllocationAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceAllocationRecord>> ReverseAllocationAsync(FinanceRequestContext context, FinanceAllocationReversalCommand command, CancellationToken cancellationToken = default) => fallback.ReverseAllocationAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceAgingRecord>> GetAgingAsync(FinanceRequestContext context, FinanceAgingQuery query, CancellationToken cancellationToken = default) => fallback.GetAgingAsync(context, query, cancellationToken);
        public Task<FinanceCustomerExposureRecord?> GetExposureAsync(FinanceRequestContext context, FinanceExposureQuery query, CancellationToken cancellationToken = default) => fallback.GetExposureAsync(context, query, cancellationToken);
    }

    private sealed class SpyMesp134Persistence : IFinanceMesp134Persistence
    {
        private readonly UnavailableFinanceMesp134Persistence fallback = new();
        internal Func<Guid, DateOnly?, IReadOnlyList<FinanceTaxAccountingReconciliationRecord>>? OnTax;
        internal Func<Guid, DateOnly?, IReadOnlyList<FinanceFxReconciliationRecord>>? OnFx;
        internal Func<Guid, DateOnly?, IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>>? OnUnrealizedFx;
        internal Func<Guid, DateOnly?, IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>>? OnReportingCurrency;

        public Task<IReadOnlyList<FinanceMonetaryPolicyRecord>> ListMonetaryPoliciesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListMonetaryPoliciesAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinanceMonetaryPolicyRecord>> CreateMonetaryPolicyAsync(FinanceRequestContext context, FinanceMonetaryPolicyCommand command, CancellationToken cancellationToken = default) => fallback.CreateMonetaryPolicyAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceTaxAccountingEffectRecord>> ListTaxEffectsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListTaxEffectsAsync(context, companyId, cancellationToken);
        public Task<FinanceTaxAccountingEffectRecord?> PreviewTaxAsync(FinanceRequestContext context, FinanceTaxAccountingCommand command, CancellationToken cancellationToken = default) => fallback.PreviewTaxAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> PostTaxAsync(FinanceRequestContext context, FinanceTaxAccountingCommand command, CancellationToken cancellationToken = default) => fallback.PostTaxAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> ReverseTaxAsync(FinanceRequestContext context, FinanceTaxAccountingReversalCommand command, CancellationToken cancellationToken = default) => fallback.ReverseTaxAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceRevaluationBatchRecord>> ListRevaluationBatchesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListRevaluationBatchesAsync(context, companyId, cancellationToken);
        public Task<FinanceRevaluationBatchRecord?> GetRevaluationBatchAsync(FinanceRequestContext context, Guid batchId, CancellationToken cancellationToken = default) => fallback.GetRevaluationBatchAsync(context, batchId, cancellationToken);
        public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CreateRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationBatchCommand command, CancellationToken cancellationToken = default) => fallback.CreateRevaluationBatchAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CalculateRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default) => fallback.CalculateRevaluationBatchAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceRevaluationScopeEvaluation>> EvaluateRevaluationScopeAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default) => fallback.EvaluateRevaluationScopeAsync(context, companyId, asOfDate, cancellationToken);
        public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> PostRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default) => fallback.PostRevaluationBatchAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> ReverseRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default) => fallback.ReverseRevaluationBatchAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ReconcileTaxAsync(context, companyId, cancellationToken);
        public Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
        { IReadOnlyList<FinanceTaxAccountingReconciliationRecord> result = OnTax?.Invoke(companyId, asOfDate) ?? []; return Task.FromResult(result); }
        public Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ReconcileFxAsync(context, companyId, cancellationToken);
        public Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
        { IReadOnlyList<FinanceFxReconciliationRecord> result = OnFx?.Invoke(companyId, asOfDate) ?? []; return Task.FromResult(result); }
        public Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ReconcileUnrealizedFxAsync(context, companyId, cancellationToken);
        public Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
        { IReadOnlyList<FinanceUnrealizedFxReconciliationRecord> result = OnUnrealizedFx?.Invoke(companyId, asOfDate) ?? []; return Task.FromResult(result); }
        public Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ReconcileReportingCurrencyAsync(context, companyId, cancellationToken);
        public Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
        { IReadOnlyList<FinanceReportingCurrencyReconciliationRecord> result = OnReportingCurrency?.Invoke(companyId, asOfDate) ?? []; return Task.FromResult(result); }
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ConfiguredFinanceCompanyProvider companies;
        private readonly FinancePersistence setup;

        private SqliteFixture(
            SqliteConnection connection,
            DbContextOptions options,
            TenantContext tenantContext,
            FinanceRequestContext context,
            ConfiguredFinanceCompanyProvider companies,
            FinancePersistence setup,
            FinanceMesp135Persistence persistence,
            Guid companyId)
        {
            this.connection = connection;
            Options = options;
            TenantContext = tenantContext;
            ContextValue = context;
            this.companies = companies;
            this.setup = setup;
            Persistence = persistence;
            CompanyId = companyId;
        }

        internal DbContextOptions Options { get; }
        internal TenantContext TenantContext { get; }
        internal FinanceMesp135Persistence Persistence { get; }
        internal Guid CompanyId { get; }
        internal Guid YearId { get; private set; }
        internal IFinanceMesp134Persistence Mesp134 { get; private set; } = new UnavailableFinanceMesp134Persistence();
        private FinanceRequestContext ContextValue { get; }

        internal FinanceRequestContext Context(string permission, Guid actorId = default)
        {
            actorId = actorId == Guid.Empty ? ActorId : actorId;
            var foundation = FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), TenantContext, permission);
            Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
            return context!;
        }

        internal static async Task<SqliteFixture> CreateAsync(IFinanceSettlementPersistence? settlement = null, IFinanceMesp134Persistence? mesp134 = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("mesp135-test"), actorId: ActorId);
            await using (var db = new FinanceDbContext(options, tenantContext)) await db.Database.EnsureCreatedAsync();
            var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantId, companyId, "MESP-135 Test Company", "SAR")]);
            var setup = new FinancePersistence(options, companies, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
            var persistence = new FinanceMesp135Persistence(options, companies, settlement ?? new UnavailableFinanceSettlementPersistence(), mesp134 ?? new UnavailableFinanceMesp134Persistence(), new UnavailableMasterDataExchangeRatePersistence());
            var foundation = FoundationRequestContext.ForTenant(ActorId, Guid.NewGuid(), tenantContext, "tenant.finance.period.close");
            Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
            return new SqliteFixture(connection, options, tenantContext, context!, companies, setup, persistence, companyId);
        }

        internal static async Task<SqliteFixture> CreateWithRevaluationAuthorityAsync(decimal revaluationRate = 4m)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("mesp135-test"), actorId: ActorId);
            await using (var db = new FinanceDbContext(options, tenantContext)) await db.Database.EnsureCreatedAsync();
            var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantId, companyId, "MESP-135 Test Company", "SAR")]);
            var exchangeRates = new TestExchangeRatePersistence();
            exchangeRates.Add(UsdSarRate(revaluationRate));
            var setup = new FinancePersistence(options, companies, new UnavailableInventoryValuationPersistence(), exchangeRates);
            var authorization = new MasterDataResourceAuthorizationService(new GrantingCapabilityResolver(), new TaxResourcePolicy(), new TaxApprovalPolicy(), new TaxScopePolicy());
            var taxService = new MasterDataTaxService(authorization, new UnavailableMasterDataTaxPersistence());
            var mesp134 = new FinanceMesp134Persistence(options, companies, new UnavailableMasterDataCurrencyPaymentTermPersistence(), exchangeRates, taxService, new UnavailableFinanceSupplierInvoiceSourceProvider());
            var persistence = new FinanceMesp135Persistence(options, companies, new UnavailableFinanceSettlementPersistence(), mesp134, exchangeRates);
            var foundation = FoundationRequestContext.ForTenant(ActorId, Guid.NewGuid(), tenantContext, "tenant.finance.period.close");
            Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
            return new SqliteFixture(connection, options, tenantContext, context!, companies, setup, persistence, companyId) { Mesp134 = mesp134 };
        }

        internal async Task<FinanceFiscalPeriodRecord> CreateOpenPeriodAsync()
        {
            var context = Context("tenant.finance.calendar.create");
            var calendar = await setup.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "MESP-135 FY", Guid.NewGuid(), "m135-calendar", "m135-calendar"));
            Assert.True(calendar.Succeeded, calendar.Code);
            var year = await setup.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "m135-year", "m135-year"));
            Assert.True(year.Succeeded, year.Code);
            YearId = year.Value!.Id;
            var period = await setup.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "MESP-135", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "m135-period", "m135-period"));
            Assert.True(period.Succeeded, period.Code);
            var opened = await setup.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
            Assert.True(opened.Succeeded, opened.Code);
            return opened.Value!;
        }

        internal async Task<FinanceFiscalPeriodRecord> CreateOpenYearPeriodAsync()
        {
            var context = Context("tenant.finance.calendar.create");
            var calendar = await setup.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "MESP-135 Year FY", Guid.NewGuid(), "m135-year-calendar", "m135-year-calendar"));
            Assert.True(calendar.Succeeded, calendar.Code);
            var year = await setup.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "m135-year-year", "m135-year-year"));
            Assert.True(year.Succeeded, year.Code);
            YearId = year.Value!.Id;
            var period = await setup.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "MESP-135-FULL-YEAR", "FY2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "m135-year-period", "m135-year-period"));
            Assert.True(period.Succeeded, period.Code);
            var opened = await setup.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
            Assert.True(opened.Succeeded, opened.Code);
            return opened.Value!;
        }

        internal async Task<(FinanceFiscalPeriodRecord January, FinanceFiscalPeriodRecord February)> CreateOpenJanuaryAndFebruaryPeriodsAsync()
        {
            var context = Context("tenant.finance.calendar.create");
            var calendar = await setup.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "MESP-135 Two-Period FY", Guid.NewGuid(), "m135-two-period-calendar", "m135-two-period-calendar"));
            Assert.True(calendar.Succeeded, calendar.Code);
            var year = await setup.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "m135-two-period-year", "m135-two-period-year"));
            Assert.True(year.Succeeded, year.Code);
            YearId = year.Value!.Id;
            var january = await setup.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "MESP-135-JAN", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "m135-jan-period", "m135-jan-period"));
            Assert.True(january.Succeeded, january.Code);
            var openedJanuary = await setup.SetPeriodStateAsync(context, new FinancePeriodStateCommand(january.Value!.Id, FinanceFiscalPeriodState.Open, null, january.Value.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
            Assert.True(openedJanuary.Succeeded, openedJanuary.Code);
            var february = await setup.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 2, "MESP-135-FEB", "February", null, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), Guid.NewGuid(), "m135-feb-period", "m135-feb-period"));
            Assert.True(february.Succeeded, february.Code);
            var openedFebruary = await setup.SetPeriodStateAsync(context, new FinancePeriodStateCommand(february.Value!.Id, FinanceFiscalPeriodState.Open, null, february.Value.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
            Assert.True(openedFebruary.Succeeded, openedFebruary.Code);
            return (openedJanuary.Value!, openedFebruary.Value!);
        }

        internal async Task CreateYearEndPostingRuleAsync()
        {
            var context = Context("tenant.finance.postingrule.create");
            await using var db = new FinanceDbContext(Options, TenantContext);
            var revenueAccountId = await db.Accounts.Where(item => item.CompanyId == CompanyId && item.Code == "M135-REVENUE").Select(item => item.Id).SingleAsync();
            var equity = await setup.CreateAccountAsync(context, Account("M135-EQUITY", FinanceAccountType.Equity));
            Assert.True(equity.Succeeded, equity.Code);
            var rule = await setup.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, "finance-year-end.v1", "close", revenueAccountId, equity.Value!.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid().ToString("N"), "year-end-rule"));
            Assert.True(rule.Succeeded, rule.Code);
        }

        internal async Task<FinanceJournalRecord> CreatePostedJournalAsync(FinanceFiscalPeriodRecord period)
        {
            var createContext = Context("tenant.finance.journal.create");
            var debit = await setup.CreateAccountAsync(createContext, Account("M135-ASSET", FinanceAccountType.Asset));
            var credit = await setup.CreateAccountAsync(createContext, Account("M135-REVENUE", FinanceAccountType.Revenue));
            Assert.True(debit.Succeeded && credit.Succeeded);
            var key = Guid.NewGuid().ToString("N");
            var command = new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "MESP-135 test journal", [new FinanceJournalLineCommand(debit.Value!.Id, 100m, 0m, 100m, "SAR", null, "Asset"), new FinanceJournalLineCommand(credit.Value!.Id, 0m, 100m, 100m, "SAR", null, "Revenue")], Guid.NewGuid(), key, key);
            var created = await setup.CreateJournalAsync(createContext, command);
            Assert.True(created.Succeeded, created.Code);
            var submitted = await setup.TransitionJournalAsync(createContext, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "submit", key + "-submit", key + "-submit"), FinanceJournalStatus.Submitted);
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await setup.TransitionJournalAsync(Context("tenant.finance.journal.approve", Guid.Parse("66666666-6666-6666-6666-666666666666")), new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "approve", key + "-approve", key + "-approve"), FinanceJournalStatus.Approved);
            Assert.True(approved.Succeeded, approved.Code);
            var posted = await setup.PostJournalAsync(createContext, new FinanceJournalActionCommand(approved.Value!.Id, approved.Value.Version, "post", key + "-post", key + "-post"));
            Assert.True(posted.Succeeded, posted.Code);
            return posted.Value!;
        }

        internal async Task<FinanceJournalRecord> CreateDatedJournalAsync(Guid debitAccountId, Guid creditAccountId, DateOnly postingDate, decimal amount = 100m)
        {
            var createContext = Context("tenant.finance.journal.create");
            var key = Guid.NewGuid().ToString("N");
            var command = new FinanceJournalCommand(CompanyId, postingDate, postingDate, null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "MESP-135 dated journal", [new FinanceJournalLineCommand(debitAccountId, amount, 0m, amount, "SAR", null, "Debit"), new FinanceJournalLineCommand(creditAccountId, 0m, amount, amount, "SAR", null, "Credit")], Guid.NewGuid(), key, key);
            var created = await setup.CreateJournalAsync(createContext, command);
            Assert.True(created.Succeeded, created.Code);
            var submitted = await setup.TransitionJournalAsync(createContext, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "submit", key + "-submit", key + "-submit"), FinanceJournalStatus.Submitted);
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await setup.TransitionJournalAsync(Context("tenant.finance.journal.approve", Guid.Parse("66666666-6666-6666-6666-666666666666")), new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "approve", key + "-approve", key + "-approve"), FinanceJournalStatus.Approved);
            Assert.True(approved.Succeeded, approved.Code);
            var posted = await setup.PostJournalAsync(createContext, new FinanceJournalActionCommand(approved.Value!.Id, approved.Value.Version, "post", key + "-post", key + "-post"));
            Assert.True(posted.Succeeded, posted.Code);
            return posted.Value!;
        }

        internal async Task<FinanceAccountRecord> CreateAccountAsync(string code, FinanceAccountType type)
        {
            var result = await setup.CreateAccountAsync(Context("tenant.finance.account.create"), Account(code, type));
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        internal async Task<byte[]> CurrentPeriodVersionAsync(Guid periodId)
        {
            await using var db = new FinanceDbContext(Options, TenantContext);
            return await db.FiscalPeriods.Where(item => item.Id == periodId).Select(item => item.Version).SingleAsync();
        }

        private FinanceAccountCommand Account(string code, FinanceAccountType type) => new(CompanyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");

        internal async Task<FinancePostingRuleRecord> CreateUnrealizedFxPostingRuleAsync(Guid debitAccountId, Guid creditAccountId)
        {
            var key = Guid.NewGuid().ToString("N");
            var rule = await setup.CreatePostingRuleAsync(Context("tenant.finance.postingrule.create"), new FinancePostingRuleCommand(CompanyId, "finance-fx.v1", "unrealized", debitAccountId, creditAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), key, key));
            Assert.True(rule.Succeeded, rule.Code);
            return rule.Value!;
        }

        internal async Task<FinanceJournalRecord> ReverseJournalAsync(Guid journalId, DateOnly postingDate)
        {
            var key = Guid.NewGuid().ToString("N");
            var reversal = await setup.ReverseJournalAsync(Context("tenant.finance.journal.reverse"), new FinanceReversalCommand(journalId, postingDate, "MESP-135 HOLD 4 reversal", Guid.NewGuid(), key, key));
            Assert.True(reversal.Succeeded, reversal.Code);
            return reversal.Value!;
        }

        internal async Task<Guid> SeedForeignExposureAsync(string discriminator, Guid linkedAccountId, Guid postedJournalId, DateOnly documentDate)
        {
            var tenantId = new TenantId(TenantId);
            await using var db = new FinanceDbContext(Options, TenantContext);
            var paymentMethod = new FinancePaymentMethodEntity(tenantId, new FinancePaymentMethodCommand(CompanyId, $"{discriminator}-PM", $"{discriminator} payment method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, $"pm-{discriminator}", $"pm-{discriminator}"));
            db.PaymentMethods.Add(paymentMethod);
            var cashAccount = new FinanceCashAccountEntity(tenantId, new FinanceCashAccountCommand(CompanyId, $"{discriminator}-CASH", $"{discriminator} cash account", null, FinanceCashAccountKind.Bank, "USD", linkedAccountId, null, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), null, $"cash-{discriminator}", $"cash-{discriminator}"), "USD");
            db.CashAccounts.Add(cashAccount);
            if (!await db.MonetaryPolicies.AnyAsync(item => item.CompanyId == CompanyId))
                db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(tenantId, new FinanceMonetaryPolicyCommand(CompanyId, null, 2, "AwayFromZero", true, new DateOnly(2025, 1, 1), null, Guid.NewGuid(), $"policy-{discriminator}", $"policy-{discriminator}"), "SAR", null, 1));
            var settlementDocumentId = Guid.NewGuid();
            var settlementDocument = new FinanceSettlementDocumentEntity(tenantId, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, CompanyId, null, null, cashAccount.Id, paymentMethod.Id, documentDate, "USD", 100m, 375m, 3.75m, null, null, null, null, null, settlementDocumentId, $"doc-{discriminator}", $"doc-{discriminator}"), "USD", "SAR", 375m, ActorId, DateTimeOffset.UtcNow);
            settlementDocument.SetPostedJournal(postedJournalId);
            settlementDocument.SetStatus(FinanceSettlementDocumentStatus.Posted, ActorId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(settlementDocument);
            await db.SaveChangesAsync();
            return settlementDocumentId;
        }

        internal async Task SetSettlementFunctionalAmountAsync(Guid settlementDocumentId, decimal functionalAmount)
        {
            await using var db = new FinanceDbContext(Options, TenantContext);
            var document = await db.SettlementDocuments.SingleAsync(item => item.Id == settlementDocumentId);
            document.Edit(
                new FinanceSettlementDocumentCommand(
                    document.Direction, document.CompanyId, document.SupplierId, document.CustomerId, document.CashAccountId, document.PaymentMethodId,
                    document.DocumentDate, document.CurrencyCode, document.Amount, functionalAmount, document.ExchangeRate, document.ExchangeRateId,
                    document.ExchangeRateVersionId, document.ExchangeRateVersionNumber, document.ExternalReference, document.Description,
                    document.Id, $"hold6-functional-{document.Id:N}", $"hold6-functional-{document.Id:N}"), document.CurrencyCode, functionalAmount);
            await db.SaveChangesAsync();
        }

        internal async Task SeedRevaluationAsync(DateOnly asOfDate, FinancePostingRuleRecord rule, Guid revaluationJournalId, Guid sourceId, decimal expectedDifference, Guid? reversalJournalId = null)
        {
            await SeedRevaluationBatchAsync(asOfDate, rule, new Dictionary<Guid, Guid> { [sourceId] = revaluationJournalId }, reversalJournalId);
            await using var db = new FinanceDbContext(Options, TenantContext);
            var lines = await db.RevaluationLines.Where(item => item.CompanyId == CompanyId && item.SourceId == sourceId && item.AsOfDate == asOfDate).ToListAsync();
            Assert.NotEmpty(lines);
            Assert.All(lines, line => Assert.Equal(expectedDifference, line.Difference));
        }

        internal async Task SeedRevaluationBatchAsync(DateOnly asOfDate, FinancePostingRuleRecord rule, IReadOnlyDictionary<Guid, Guid> journalBySource, Guid? reversalJournalId = null)
        {
            var key = Guid.NewGuid().ToString("N");
            var created = await Mesp134.CreateRevaluationBatchAsync(Context("tenant.finance.revaluation.create"), new FinanceRevaluationBatchCommand(CompanyId, asOfDate, FinanceRevaluationScopes.ApArAndUnallocatedSettlements, Guid.NewGuid(), key, key));
            Assert.True(created.Succeeded, created.Code);
            var calculated = await Mesp134.CalculateRevaluationBatchAsync(Context("tenant.finance.revaluation.calculate"), new FinanceRevaluationActionCommand(created.Value!.Id, created.Value.Version, "calculate", Guid.NewGuid(), key + "-calculate", key + "-calculate"));
            Assert.True(calculated.Succeeded, calculated.Code);
            Assert.Equal(journalBySource.Count, calculated.Value!.Lines.Count);
            Assert.All(calculated.Value.Lines, line => Assert.Contains(line.SourceId, journalBySource.Keys));

            await using var db = new FinanceDbContext(Options, TenantContext);
            var batch = await db.RevaluationBatches.SingleAsync(item => item.Id == created.Value.Id);
            foreach (var lineRecord in calculated.Value.Lines)
            {
                var line = await db.RevaluationLines.SingleAsync(item => item.Id == lineRecord.Id);
                line.SetPostingRule(rule.Id, rule.VersionNumber);
                line.SetJournal(journalBySource[lineRecord.SourceId]);
                if (reversalJournalId is { } reversalId)
                    line.SetReversal(reversalId);
            }
            batch.SetStatus(reversalJournalId is null ? FinanceRevaluationBatchStatus.Posted : FinanceRevaluationBatchStatus.Reversed, ActorId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private sealed class TestExchangeRatePersistence : IMasterDataExchangeRatePersistence
    {
        internal List<MasterDataExchangeRateRecord> Records { get; } = [];

        internal void Add(MasterDataExchangeRateRecord record) => Records.Add(record);

        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MasterDataExchangeRateRecord>>(Records);

        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MasterDataExchangeRateRecord?>(Records.SingleOrDefault(item => item.Id == exchangeRateId));

        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
    }

    private static MasterDataExchangeRateRecord UsdSarRate(decimal rate)
    {
        var versionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0135");
        return new MasterDataExchangeRateRecord(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0134"),
            new TenantId(TenantId),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0136"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0137"),
            "USD",
            "SAR",
            MasterDataLifecycleState.Active,
            1,
            [new MasterDataExchangeRateVersionRecord(versionId, 1, new DateOnly(2026, 1, 1), null, rate, 6, ExchangeRateProvenance.Configured, "MESP-135 test authority", "USD", "SAR")],
            [1]);
    }

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => Enum.GetValues<MasterDataCapability>().ToHashSet();
    }
}

[Collection(SqlServerSafetyCollection.Name)]
public sealed class FinanceMesp135SqlServerSafetyTests
{
    private readonly SqlServerSafetyFixture fixture;

    public FinanceMesp135SqlServerSafetyTests(SqlServerSafetyFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Close01_Concurrent_period_close_has_one_committed_winner()
    {
        var scenario = await CreateScenarioAsync();
        var first = Safe(() => scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "close01-a")));
        var second = Safe(() => scenario.Second.ClosePeriodAsync(scenario.SecondContext, CloseCommand(scenario, "close01-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        Assert.Contains(results, item => item.Code is "concurrency_conflict" or "period_already_closed");
    }

    [Fact]
    public async Task Close02_Concurrent_reopen_has_one_committed_winner()
    {
        var scenario = await CreateScenarioAsync();
        var closed = await scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "close02-close"));
        Assert.True(closed.Succeeded, closed.Code);
        scenario = scenario with { Period = await CurrentPeriodAsync(scenario) };

        var first = Safe(() => scenario.First.ReopenPeriodAsync(scenario.FirstContext, ReopenCommand(scenario, "reopen02-a")));
        var second = Safe(() => scenario.Second.ReopenPeriodAsync(scenario.SecondContext, ReopenCommand(scenario, "reopen02-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        Assert.Contains(results, item => item.Code is "concurrency_conflict" or "period_not_closed");
    }

    [Fact]
    public async Task Close03_Concurrent_close_and_post_reject_closed_period_journal()
    {
        var scenario = await CreateScenarioAsync(postJournal: true);
        var close = Safe(() => scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "close03-close")));
        var post = Safe(() => scenario.Setup.PostJournalAsync(scenario.FirstContext, new FinanceJournalActionCommand(scenario.ApprovedJournal!.Id, scenario.ApprovedJournal.Version, "post during close", "close03-post", "close03-post")));
        await Task.WhenAll(close, post);
        var closeResult = await close;
        var postResult = await post;

        Assert.True(closeResult.Succeeded, closeResult.Code);
        if (postResult.Succeeded)
        {
            Assert.True(postResult.Value!.PostedAt <= closeResult.Value!.CreatedAt);
        }
        else
        {
            Assert.Contains(postResult.Code, new[] { "period_closed", "concurrency_conflict" });
        }
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(FinanceFiscalPeriodState.Closed, await db.FiscalPeriods.Where(item => item.Id == scenario.Period.Id).Select(item => item.State).SingleAsync());
    }

    [Fact]
    public async Task Year01_Concurrent_year_end_calculation_has_one_durable_snapshot()
    {
        var scenario = await CreateScenarioAsync(postJournal: true, yearEndRule: true, closePeriod: true);
        var first = Safe(() => scenario.First.CalculateYearEndAsync(scenario.FirstContext, YearEndCommand(scenario, "year01-a")));
        var second = Safe(() => scenario.Second.CalculateYearEndAsync(scenario.SecondContext, YearEndCommand(scenario, "year01-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(1, await db.YearEndRuns.CountAsync(item => item.CompanyId == scenario.CompanyId));
    }

    [Fact]
    public async Task Year02_Concurrent_year_end_post_has_one_committed_journal()
    {
        var scenario = await CreateScenarioAsync(postJournal: true, yearEndRule: true, closePeriod: true);
        var calculated = await scenario.First.CalculateYearEndAsync(scenario.FirstContext, YearEndCommand(scenario, "year02-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        scenario = scenario with { YearEnd = calculated.Value };

        var first = Safe(() => scenario.First.PostYearEndAsync(scenario.FirstContext, YearEndActionCommand(scenario, "year02-a")));
        var second = Safe(() => scenario.Second.PostYearEndAsync(scenario.SecondContext, YearEndActionCommand(scenario, "year02-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-year-end.v1"));
    }

    [Fact]
    public async Task Corr01_Concurrent_correction_has_one_committed_reversal()
    {
        var scenario = await CreateScenarioAsync(postJournal: true);
        var first = Safe(() => scenario.First.CorrectJournalAsync(scenario.FirstContext, CorrectionCommand(scenario, "corr01-a")));
        var second = Safe(() => scenario.Second.CorrectJournalAsync(scenario.SecondContext, CorrectionCommand(scenario, "corr01-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-correction.v1"));
    }

    [Fact]
    public async Task Corr02_correction_and_reversal_are_exact_and_linked()
    {
        var scenario = await CreateScenarioAsync(postJournal: true);
        var corrected = await scenario.First.CorrectJournalAsync(scenario.FirstContext, CorrectionCommand(scenario, "corr02"));
        Assert.True(corrected.Succeeded, corrected.Code);
        Assert.Equal(scenario.Journal!.Id, corrected.Value!.ReversalOfJournalId);

        for (var index = 0; index < scenario.Journal.Lines.Count; index++)
        {
            var original = scenario.Journal.Lines[index];
            var reversal = corrected.Value.Lines[index];
            Assert.Equal(original.Debit, reversal.Credit);
            Assert.Equal(original.Credit, reversal.Debit);
            Assert.Equal(original.FunctionalDebit, reversal.FunctionalCredit);
            Assert.Equal(original.FunctionalCredit, reversal.FunctionalDebit);
        }

        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(corrected.Value.Id, await db.Journals.Where(item => item.Id == scenario.Journal.Id).Select(item => item.ReversalJournalId).SingleAsync());
    }

    [Fact]
    public async Task Close04_Concurrent_reopen_and_post_preserve_one_coherent_period_state()
    {
        var scenario = await CreateScenarioAsync();
        var approved = await CreateApprovedJournalAsync(scenario.Setup, scenario.FirstContext, scenario.SecondContext, scenario.CompanyId, scenario.Accounts);
        var closed = await scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "close04-close"));
        Assert.True(closed.Succeeded, closed.Code);
        scenario = scenario with { Period = await CurrentPeriodAsync(scenario), ApprovedJournal = approved };
        var postPersistence = new FinancePersistence(scenario.Options, scenario.Provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());

        var reopen = Safe(() => scenario.First.ReopenPeriodAsync(scenario.FirstContext, ReopenCommand(scenario, "close04-reopen")));
        var post = Safe(() => postPersistence.PostJournalAsync(scenario.SecondContext, new FinanceJournalActionCommand(approved.Id, approved.Version, "post concurrent with reopen", "close04-post", "close04-post")));
        await Task.WhenAll(reopen, post);
        var reopenResult = await reopen;
        var postResult = await post;

        Assert.True(reopenResult.Succeeded, reopenResult.Code);
        var finalPeriod = await CurrentPeriodAsync(scenario);
        await using var historyDb = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(1, await historyDb.PeriodHistory.CountAsync(item => item.PeriodId == scenario.Period.Id && item.Action == FinancePeriodHistoryAction.Reopened));
        if (postResult.Succeeded)
        {
            Assert.Equal(FinanceFiscalPeriodState.Open, finalPeriod.State);
            await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
            Assert.Equal(FinanceJournalStatus.Posted, await db.Journals.Where(item => item.Id == approved.Id).Select(item => item.Status).SingleAsync());
        }
        else
        {
            Assert.Contains(postResult.Code, new[] { "period_closed", "concurrency_conflict" });
            Assert.Equal(FinanceFiscalPeriodState.Open, finalPeriod.State);
        }
    }

    [Fact]
    public async Task Year03_Concurrent_year_end_post_and_late_journal_cannot_commit_stale_year_end()
    {
        var scenario = await CreateScenarioAsync(postJournal: true, yearEndRule: true, closePeriod: true);
        var calculated = await scenario.First.CalculateYearEndAsync(scenario.FirstContext, YearEndCommand(scenario, "year03-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        scenario = scenario with { YearEnd = calculated.Value };
        var lateJournal = await CreateApprovedJournalAsync(scenario.Setup, scenario.FirstContext, scenario.SecondContext, scenario.CompanyId, scenario.Accounts);
        var postPersistence = new FinancePersistence(scenario.Options, scenario.Provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());

        var yearEnd = Safe(() => scenario.First.PostYearEndAsync(scenario.FirstContext, YearEndActionCommand(scenario, "year03-post")));
        var latePost = Safe(() => postPersistence.PostJournalAsync(scenario.SecondContext, new FinanceJournalActionCommand(lateJournal.Id, lateJournal.Version, "late ordinary journal", "year03-late-post", "year03-late-post")));
        await Task.WhenAll(yearEnd, latePost);
        var yearEndResult = await yearEnd;
        var latePostResult = await latePost;

        if (yearEndResult.Succeeded)
        {
            Assert.False(latePostResult.Succeeded);
            Assert.Contains(latePostResult.Code, new[] { "period_closed", "concurrency_conflict" });
            await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
            Assert.Equal(FinanceFiscalYearState.Closed, await db.FiscalYears.Where(item => item.Id == scenario.Year.Id).Select(item => item.State).SingleAsync());
            Assert.Equal(FinanceJournalStatus.Posted, await db.Journals.Where(item => item.Id == yearEndResult.Value!.ClosingJournalId).Select(item => item.Status).SingleAsync());
            Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-year-end.v1"));
            Assert.Equal(FinanceJournalStatus.Approved, await db.Journals.Where(item => item.Id == lateJournal.Id).Select(item => item.Status).SingleAsync());
        }
        else
        {
            Assert.True(latePostResult.Succeeded, latePostResult.Code);
            Assert.Contains(yearEndResult.Code, new[] { "year_periods_not_closed", "year_end_source_changed", "concurrency_conflict" });
            await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
            Assert.Equal(0, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-year-end.v1"));
            Assert.Equal(FinanceJournalStatus.Posted, await db.Journals.Where(item => item.Id == lateJournal.Id).Select(item => item.Status).SingleAsync());
        }
    }

    [Fact]
    public async Task Corr03_Concurrent_correction_and_period_close_preserve_close_snapshot()
    {
        var scenario = await CreateScenarioAsync(postJournal: true);
        var close = Safe(() => scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "corr03-close")));
        var correction = Safe(() => scenario.Second.CorrectJournalAsync(scenario.SecondContext, CorrectionCommand(scenario, "corr03-correction")));
        await Task.WhenAll(close, correction);
        var closeResult = await close;
        var correctionResult = await correction;

        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        var finalPeriodState = await db.FiscalPeriods.Where(item => item.Id == scenario.Period.Id).Select(item => item.State).SingleAsync();
        if (correctionResult.Succeeded)
        {
            Assert.Equal(FinanceJournalStatus.Reversed, await db.Journals.Where(item => item.Id == scenario.Journal!.Id).Select(item => item.Status).SingleAsync());
            Assert.NotNull(await db.Journals.Where(item => item.Id == scenario.Journal!.Id).Select(item => item.ReversalJournalId).SingleAsync());
            if (closeResult.Succeeded)
            {
                Assert.Equal(FinanceFiscalPeriodState.Closed, finalPeriodState);
                var closeRun = await db.PeriodCloseRuns.Where(item => item.PeriodId == scenario.Period.Id).SingleAsync();
                Assert.NotEmpty(closeRun.SnapshotFingerprint);
                Assert.Contains("gl_balanced", closeRun.ChecksJson);
            }
            else
            {
                Assert.Equal(FinanceFiscalPeriodState.Open, finalPeriodState);
            }
        }
        else
        {
            Assert.True(closeResult.Succeeded, closeResult.Code);
            Assert.Contains(correctionResult.Code, new[] { "period_closed", "concurrency_conflict" });
            Assert.Equal(FinanceFiscalPeriodState.Closed, finalPeriodState);
            Assert.Equal(0, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-correction.v1"));
            Assert.Equal(1, await db.PeriodCloseRuns.CountAsync(item => item.PeriodId == scenario.Period.Id));
        }
    }

    private async Task<SqlScenario> CreateScenarioAsync(bool postJournal = false, bool yearEndRule = false, bool closePeriod = false)
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var companyId = Guid.NewGuid();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(fixture.TenantA.TenantId.Value, companyId, "MESP-135 SQL Company", "SAR")]);
        var setup = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var first = new FinanceMesp135Persistence(options, provider, new UnavailableFinanceSettlementPersistence(), new UnavailableFinanceMesp134Persistence(), new UnavailableMasterDataExchangeRatePersistence());
        var second = new FinanceMesp135Persistence(options, provider, new UnavailableFinanceSettlementPersistence(), new UnavailableFinanceMesp134Persistence(), new UnavailableMasterDataExchangeRatePersistence());
        var firstContext = Context("tenant.finance.period.close", Guid.NewGuid());
        var secondContext = Context("tenant.finance.period.close", Guid.NewGuid());
        var calendar = await setup.CreateCalendarAsync(firstContext, new FinanceFiscalCalendarCommand(companyId, "MESP-135 SQL FY", Guid.NewGuid(), Guid.NewGuid().ToString("N"), "calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await setup.CreateYearAsync(firstContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), Guid.NewGuid().ToString("N"), "year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await setup.CreatePeriodAsync(firstContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, Guid.NewGuid().ToString("N"), "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), Guid.NewGuid().ToString("N"), "period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await setup.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        Assert.True(opened.Succeeded, opened.Code);
        var accounts = await CreateAccountsAsync(setup, firstContext, companyId);
        FinanceJournalRecord? approvedJournal = null;
        FinanceJournalRecord? postedJournal = null;
        if (postJournal)
        {
            var journals = await CreatePostedJournalAsync(setup, firstContext, secondContext, companyId, accounts);
            approvedJournal = journals.Approved;
            postedJournal = journals.Posted;
        }
        if (yearEndRule)
        {
            var rule = await setup.CreatePostingRuleAsync(firstContext, new FinancePostingRuleCommand(companyId, "finance-year-end.v1", "close", accounts.Revenue.Id, accounts.Equity.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid().ToString("N"), "year-end"));
            Assert.True(rule.Succeeded, rule.Code);
        }
        var currentPeriod = opened.Value!;
        if (closePeriod)
        {
            var closed = await setup.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(currentPeriod.Id, FinanceFiscalPeriodState.Closed, "seed close", currentPeriod.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
            Assert.True(closed.Succeeded, closed.Code);
            currentPeriod = closed.Value!;
        }
        return new SqlScenario(options, companyId, provider, setup, first, second, firstContext, secondContext, year.Value!, currentPeriod, accounts, approvedJournal, postedJournal, null);
    }

    private async Task<(FinanceAccountRecord Debit, FinanceAccountRecord Revenue, FinanceAccountRecord Equity)> CreateAccountsAsync(FinancePersistence setup, FinanceRequestContext context, Guid companyId)
    {
        var debit = await setup.CreateAccountAsync(context, Account(companyId, "M135-SQL-ASSET", FinanceAccountType.Asset));
        var revenue = await setup.CreateAccountAsync(context, Account(companyId, "M135-SQL-REVENUE", FinanceAccountType.Revenue));
        var equity = await setup.CreateAccountAsync(context, Account(companyId, "M135-SQL-EQUITY", FinanceAccountType.Equity));
        Assert.True(debit.Succeeded, debit.Code);
        Assert.True(revenue.Succeeded, revenue.Code);
        Assert.True(equity.Succeeded, equity.Code);
        return (debit.Value!, revenue.Value!, equity.Value!);
    }

    private static async Task<(FinanceJournalRecord Approved, FinanceJournalRecord Posted)> CreatePostedJournalAsync(FinancePersistence setup, FinanceRequestContext creator, FinanceRequestContext approver, Guid companyId, (FinanceAccountRecord Debit, FinanceAccountRecord Revenue, FinanceAccountRecord Equity) accounts)
    {
        var key = Guid.NewGuid().ToString("N");
        var command = new FinanceJournalCommand(companyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "MESP-135 SQL journal", [new FinanceJournalLineCommand(accounts.Debit.Id, 100m, 0m, 100m, "SAR", null, "Asset"), new FinanceJournalLineCommand(accounts.Revenue.Id, 0m, 100m, 100m, "SAR", null, "Revenue")], Guid.NewGuid(), key, key);
        var created = await setup.CreateJournalAsync(creator, command);
        Assert.True(created.Succeeded, created.Code);
        var submitted = await setup.TransitionJournalAsync(creator, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "submit", key + "-submit", key + "-submit"), FinanceJournalStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await setup.TransitionJournalAsync(approver, new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "approve", key + "-approve", key + "-approve"), FinanceJournalStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await setup.PostJournalAsync(creator, new FinanceJournalActionCommand(approved.Value!.Id, approved.Value.Version, "post", key + "-post", key + "-post"));
        Assert.True(posted.Succeeded, posted.Code);
        return (approved.Value!, posted.Value!);
    }

    private static async Task<FinanceJournalRecord> CreateApprovedJournalAsync(FinancePersistence setup, FinanceRequestContext creator, FinanceRequestContext approver, Guid companyId, (FinanceAccountRecord Debit, FinanceAccountRecord Revenue, FinanceAccountRecord Equity) accounts)
    {
        var key = Guid.NewGuid().ToString("N");
        var command = new FinanceJournalCommand(companyId, new DateOnly(2026, 1, 20), new DateOnly(2026, 1, 20), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "MESP-135 SQL late journal", [new FinanceJournalLineCommand(accounts.Debit.Id, 40m, 0m, 40m, "SAR", null, "Asset"), new FinanceJournalLineCommand(accounts.Revenue.Id, 0m, 40m, 40m, "SAR", null, "Revenue")], Guid.NewGuid(), key, key);
        var created = await setup.CreateJournalAsync(creator, command);
        Assert.True(created.Succeeded, created.Code);
        var submitted = await setup.TransitionJournalAsync(creator, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "submit", key + "-submit", key + "-submit"), FinanceJournalStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await setup.TransitionJournalAsync(approver, new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "approve", key + "-approve", key + "-approve"), FinanceJournalStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        return approved.Value!;
    }

    private FinanceRequestContext Context(string permission, Guid actorId)
    {
        var foundation = FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), fixture.TenantA, permission);
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private static FinanceAccountCommand Account(Guid companyId, string code, FinanceAccountType type) => new(companyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");

    private static FinancePeriodCloseCommand CloseCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Period.Id, scenario.Period.Version, "MESP-135 SQL close", Guid.NewGuid(), key, key);
    private static FinancePeriodReopenCommand ReopenCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Period.Id, scenario.Period.Version, "MESP-135 SQL reopen", Guid.NewGuid(), key, key);
    private static FinanceYearEndCommand YearEndCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Year.Id, scenario.Year.EndDate, "MESP-135 SQL year end", Guid.NewGuid(), key, key);
    private static FinanceYearEndActionCommand YearEndActionCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.YearEnd!.Id, scenario.YearEnd.Version, "MESP-135 SQL year end post", Guid.NewGuid(), key, key);
    private static FinanceCorrectionCommand CorrectionCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Journal!.Id, new DateOnly(2026, 1, 20), scenario.Journal.Version, "MESP-135 SQL correction", Guid.NewGuid(), key, key);

    private async Task<FinanceFiscalPeriodRecord> CurrentPeriodAsync(SqlScenario scenario)
    {
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        return await db.FiscalPeriods.Where(item => item.Id == scenario.Period.Id).Select(item => new FinanceFiscalPeriodRecord(item.Id, item.FiscalYearId, item.TenantId.Value, item.CompanyId, item.Sequence, item.Code, item.EnglishName, item.ArabicName, item.StartDate, item.EndDate, item.State, item.Version)).SingleAsync();
    }

    private static async Task<FinanceOperationResult<T>> Safe<T>(Func<Task<FinanceOperationResult<T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (DbUpdateConcurrencyException)
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && IsExpectedContention(sql.Number))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (SqlException exception) when (IsExpectedContention(exception.Number))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (InvalidOperationException exception) when (ContainsExpectedContention(exception))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
    }

    private static bool IsExpectedContention(int number) => number is 1205 or 1222 or 2601 or 2627 or 3960;

    private static bool ContainsExpectedContention(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && IsExpectedContention(sql.Number)) return true;
        }
        return false;
    }

    private sealed record SqlScenario(
        DbContextOptions Options,
        Guid CompanyId,
        ConfiguredFinanceCompanyProvider Provider,
        FinancePersistence Setup,
        FinanceMesp135Persistence First,
        FinanceMesp135Persistence Second,
        FinanceRequestContext FirstContext,
        FinanceRequestContext SecondContext,
        FinanceFiscalYearRecord Year,
        FinanceFiscalPeriodRecord Period,
        (FinanceAccountRecord Debit, FinanceAccountRecord Revenue, FinanceAccountRecord Equity) Accounts,
        FinanceJournalRecord? ApprovedJournal,
        FinanceJournalRecord? Journal,
        FinanceYearEndRunRecord? YearEnd);
}
