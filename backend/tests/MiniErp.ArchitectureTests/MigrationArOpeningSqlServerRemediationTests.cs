using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationEconomicOpeningRemediationSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    [Fact]
    public async Task Sql_server_s10_foreign_ar_ap_cash_uses_opening_date_rate_and_persists_monetary_evidence()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();

        var arReady = await fixture.Settlement.PreflightMigrationArOpeningAsync(fixture.FinanceContext, fixture.Command("S10-AR-USD", 100m) with { CurrencyCode = "USD" });
        Assert.True(arReady.Ready, arReady.Code);
        Assert.Equal(375m, arReady.Expectation!.FunctionalAmount);
        Assert.Equal(fixture.ExchangeRates.RateId, arReady.Expectation.ExchangeRateId);
        Assert.Equal(new DateOnly(2026, 1, 15), arReady.Expectation.RateDate);
        var ar = await fixture.Settlement.CreateMigrationArOpeningAsync(fixture.FinanceContext, fixture.Command("S10-AR-USD", 100m) with { CurrencyCode = "USD" });
        Assert.True(ar.Succeeded, ar.Code);
        var arEvidence = await fixture.Settlement.ReadMigrationArOpeningAsync(fixture.FinanceContext, fixture.Command("S10-AR-USD", 100m) with { CurrencyCode = "USD" });
        Assert.NotNull(arEvidence?.MonetaryEvidence);
        Assert.Equal(375m, arEvidence!.OpenItem.OriginalFunctionalAmount);
        Assert.Equal("USD", arEvidence.OpenItem.CurrencyCode);
        Assert.Equal(375m, arEvidence.RecognitionJournal.Lines.Sum(item => item.FunctionalDebit));
        Assert.Equal("USD", arEvidence.RecognitionJournal.TransactionCurrencyCode);

        var ap = await fixture.Settlement.CreateMigrationApOpeningAsync(fixture.FinanceContext, fixture.ApCommand("S10-AP-USD", 100m) with { CurrencyCode = "USD" });
        Assert.True(ap.Succeeded, ap.Code);
        var apEvidence = await fixture.Settlement.ReadMigrationApOpeningAsync(fixture.FinanceContext, fixture.ApCommand("S10-AP-USD", 100m) with { CurrencyCode = "USD" });
        Assert.NotNull(apEvidence?.MonetaryEvidence);
        Assert.Equal(375m, apEvidence!.OpenItem.OriginalFunctionalAmount);
        Assert.Equal(375m, apEvidence.RecognitionJournal.Lines.Sum(item => item.FunctionalCredit));

        var cashAccount = await fixture.Settlement.CreateCashAccountAsync(fixture.FinanceContextFor("tenant.finance.settlement.manage"), new FinanceCashAccountCommand(fixture.CompanyId, "S10-USD-CASH", "S10 USD cash", null, FinanceCashAccountKind.Bank, "USD", fixture.CashLinkedAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s10-cash", "s10-cash"));
        Assert.True(cashAccount.Succeeded, cashAccount.Code);
        var cashCommand = fixture.CashCommand("S10-CASH-USD", 100m) with { CashAccountId = cashAccount.Value!.Id, CurrencyCode = "USD" };
        var cash = await fixture.Settlement.CreateMigrationCashBankOpeningAsync(fixture.FinanceContext, cashCommand);
        Assert.True(cash.Succeeded, cash.Code);
        var cashEvidence = await fixture.Settlement.ReadMigrationCashBankOpeningAsync(fixture.FinanceContext, cashCommand);
        Assert.NotNull(cashEvidence?.MonetaryEvidence);
        Assert.Equal(375m, cashEvidence!.MonetaryEvidence!.FunctionalAmount);
        Assert.Equal(375m, cashEvidence.RecognitionJournal.Lines.Sum(item => item.FunctionalDebit));

        fixture.ExchangeRates.Rate = 4m;
        var historical = await fixture.Settlement.ReadMigrationArOpeningAsync(fixture.FinanceContext, fixture.Command("S10-AR-USD", 100m) with { CurrencyCode = "USD" });
        Assert.Equal(375m, historical!.MonetaryEvidence!.FunctionalAmount);
        Assert.Equal(fixture.ExchangeRates.RateId, historical.MonetaryEvidence.TransactionToFunctionalRate!.ExchangeRateId);

        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Equal(3, await db.JournalMonetaryEvidence.CountAsync(item => item.CompanyId == fixture.CompanyId));
        Assert.Equal(0, await db.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && (item.SourceContract == "finance-fx.v1" || item.SourceContract == "finance-revaluation.v1")));
    }

    [Fact]
    public async Task Sql_server_s10_foreign_opening_fails_closed_without_policy_or_exact_rate()
    {
        await using var noPolicy = await ArSqlFixture.CreateAsync(safety);
        var missingPolicy = await noPolicy.Settlement.PreflightMigrationArOpeningAsync(noPolicy.FinanceContext, noPolicy.Command("S10-NO-POLICY", 100m) with { CurrencyCode = "USD" });
        Assert.False(missingPolicy.Ready);
        Assert.Equal("migration_opening_monetary_policy_required", missingPolicy.Code);

        await using var missingRate = await ArSqlFixture.CreateAsync(safety);
        await missingRate.EnableMonetaryPolicyAsync();
        missingRate.ExchangeRates.Available = false;
        var unavailable = await missingRate.Settlement.PreflightMigrationApOpeningAsync(missingRate.FinanceContext, missingRate.ApCommand("S10-NO-RATE", 100m) with { CurrencyCode = "USD" });
        Assert.False(unavailable.Ready);
        Assert.Equal("migration_opening_exchange_rate_unavailable", unavailable.Code);

        await using var invalidScale = await ArSqlFixture.CreateAsync(safety);
        await invalidScale.EnableMonetaryPolicyAsync(9);
        var invalid = await invalidScale.Settlement.PreflightMigrationArOpeningAsync(invalidScale.FinanceContext, invalidScale.Command("S10-BAD-SCALE", 100m) with { CurrencyCode = "USD" });
        Assert.False(invalid.Ready);
        Assert.Equal("migration_opening_rounding_policy_invalid", invalid.Code);
    }

    [Fact]
    public async Task Sql_server_s10_rate_drift_fails_before_foreign_owner_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var command = fixture.Command("S10-DRIFT-USD", 100m) with { CurrencyCode = "USD", SourceRecordId = Guid.NewGuid() };
        var prepared = await fixture.Settlement.PreflightMigrationArOpeningAsync(fixture.FinanceContext, command);
        Assert.True(prepared.Ready, prepared.Code);

        fixture.ExchangeRates.Rate = 4m;
        var result = await fixture.Settlement.CreateMigrationArOpeningAsync(
            fixture.FinanceContext,
            command with { ExpectedExpectation = prepared.Expectation });

        Assert.False(result.Succeeded);
        Assert.Equal("migration_opening_exchange_rate_drift", result.Code);
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Empty(await db.OpenItems.AsNoTracking().Where(item => item.SourceContract == "migration-ar-opening.v1" && item.Reference == command.SourceReference).ToListAsync());
        Assert.Empty(await db.Journals.AsNoTracking().Where(item => item.SourceContract == "migration-ar-opening.v1" && item.Description.Contains(command.SourceReference)).ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p02_foreign_ar_real_orchestration_succeeds_and_exposes_fx_evidence()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareAsync("S10-P02-AR-USD", 100m, "USD");

        var result = await fixture.NewExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p02-ar", prepared.Run.Version);

        Assert.Equal(MigrationAttemptOutcome.Succeeded, result.Value!.AttemptOutcome);
        await fixture.AssertOneEffectAsync("S10-P02-AR-USD", 100m);
        var reconciliation = Assert.Single(result.Value!.ArEconomicReconciliations!, item => item.SourceReference == "S10-P02-AR-USD");
        Assert.Equal("USD", reconciliation.TransactionCurrencyCode);
        Assert.Equal(100m, reconciliation.TransactionAmount);
        Assert.Equal("SAR", reconciliation.FunctionalCurrencyCode);
        Assert.Equal(375m, reconciliation.FunctionalAmount);
        Assert.Equal(fixture.ExchangeRates.RateId, reconciliation.ExchangeRateId);
        Assert.Equal(new DateOnly(2026, 1, 15), reconciliation.RateDate);
        Assert.Equal(3.75m, reconciliation.AppliedRate);
    }

    [Fact]
    public async Task Sql_server_s10_p03_foreign_ap_real_orchestration_succeeds_and_exposes_fx_evidence()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareApAsync("S10-P03-AP-USD", 100m, "USD");

        var result = await fixture.NewApExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p03-ap", prepared.Run.Version);

        Assert.Equal(MigrationAttemptOutcome.Succeeded, result.Value!.AttemptOutcome);
        await fixture.AssertOneApEffectAsync("S10-P03-AP-USD", 100m);
        var reconciliation = Assert.Single(result.Value!.ApEconomicReconciliations!, item => item.SourceReference == "S10-P03-AP-USD");
        Assert.Equal("USD", reconciliation.TransactionCurrencyCode);
        Assert.Equal(100m, reconciliation.TransactionAmount);
        Assert.Equal(375m, reconciliation.FunctionalAmount);
        Assert.Equal(3.75m, reconciliation.AppliedRate);
    }

    [Fact]
    public async Task Sql_server_s10_p04_foreign_cash_real_orchestration_mixed_gl_group_regression()
    {
        // Regression proof for the Blocker A fix: MigrationGlOpeningExecutionCoordinator.CashMatches
        // previously compared the functional-currency journal total against the raw transaction-currency
        // command amount (e.g. SAR 375 vs USD 100), which always failed for foreign Cash inside a mixed
        // owner group that also posts GL. This exercises the REAL mixed/GL orchestration path, not the
        // standalone Cash coordinator (which was never affected) or a direct Settlement call.
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var usdCash = await fixture.Settlement.CreateCashAccountAsync(
            fixture.FinanceContextFor("tenant.finance.settlement.manage"),
            new FinanceCashAccountCommand(fixture.CompanyId, "S10-P04-USD-CASH", "S10 P04 USD cash", null, FinanceCashAccountKind.Bank, "USD", fixture.CashLinkedAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s10-p04-cash", "s10-p04-cash"));
        Assert.True(usdCash.Succeeded, usdCash.Code);

        var prepared = await fixture.PrepareMixedAsync("S10-P04", 375m, includeCash: true, includeGl: true, cashAmount: 100m, cashCurrencyCode: "USD", cashAccountIdOverride: usdCash.Value!.Id);
        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p04-mixed-cash-fx", prepared.Run.Version);

        Assert.True(result.Succeeded, $"{result.Kind}:{result.Code}");
        Assert.Equal(MigrationAttemptOutcome.Succeeded, result.Value!.AttemptOutcome);
        await fixture.AssertOneCashEffectAsync("S10-P04-CASH", 375m);
        var reconciliation = Assert.Single(result.Value!.CashBankEconomicReconciliations!, item => item.SourceReference == "S10-P04-CASH");
        Assert.Equal("USD", reconciliation.TransactionCurrencyCode);
        Assert.Equal(100m, reconciliation.TransactionAmount);
        Assert.Equal("SAR", reconciliation.FunctionalCurrencyCode);
        Assert.Equal(375m, reconciliation.FunctionalAmount);
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-gl-opening.v1" && item.Status == FinanceJournalStatus.Posted));
    }

    [Fact]
    public async Task Sql_server_s10_p05_foreign_inventory_opening_rejected_before_owner_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareInventoryOnlyAsync("S10-P05-LINE", "USD");

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p05-foreign-inventory", prepared.Run.Version);

        Assert.False(result.Succeeded, result.Code);
        await fixture.AssertNoStockMovementAsync();
    }

    [Fact]
    public async Task Sql_server_s10_p07_mixed_all_five_two_foreign_owners_real_orchestration()
    {
        // P07 flagship: two independently-foreign owner types (AR and Cash, both USD) inside one mixed
        // run alongside functional-currency AP/Inventory/GL, executed through the real orchestration
        // path (Prepare -> durable expectation -> owner execution -> GL last -> public reconciliation).
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var usdCash = await fixture.Settlement.CreateCashAccountAsync(
            fixture.FinanceContextFor("tenant.finance.settlement.manage"),
            new FinanceCashAccountCommand(fixture.CompanyId, "S10-P07-USD-CASH", "S10 P07 USD cash", null, FinanceCashAccountKind.Bank, "USD", fixture.CashLinkedAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s10-p07-cash", "s10-p07-cash"));
        Assert.True(usdCash.Succeeded, usdCash.Code);

        var prepared = await fixture.PrepareMixedAsync("S10-P07", 375m, includeCash: true, includeGl: true, cashAmount: 100m, cashCurrencyCode: "USD", cashAccountIdOverride: usdCash.Value!.Id, arAmount: 100m, arCurrencyCode: "USD");
        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p07-mixed-all-five", prepared.Run.Version);

        Assert.Equal(MigrationAttemptOutcome.Succeeded, result.Value!.AttemptOutcome);
        await fixture.AssertOneEffectAsync("S10-P07", 100m);
        await fixture.AssertOneApEffectAsync("S10-P07-AP", 375m);
        await fixture.AssertOneCashEffectAsync("S10-P07-CASH", 375m);

        var arReconciliation = Assert.Single(result.Value!.ArEconomicReconciliations!, item => item.SourceReference == "S10-P07");
        Assert.Equal("USD", arReconciliation.TransactionCurrencyCode);
        Assert.Equal(375m, arReconciliation.FunctionalAmount);
        var cashReconciliation = Assert.Single(result.Value!.CashBankEconomicReconciliations!, item => item.SourceReference == "S10-P07-CASH");
        Assert.Equal("USD", cashReconciliation.TransactionCurrencyCode);
        Assert.Equal(375m, cashReconciliation.FunctionalAmount);
        var apReconciliation = Assert.Single(result.Value!.ApEconomicReconciliations!, item => item.SourceReference == "S10-P07-AP");
        Assert.Equal("SAR", apReconciliation.TransactionCurrencyCode);
        Assert.Null(apReconciliation.AppliedRate);

        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-gl-opening.v1" && item.Status == FinanceJournalStatus.Posted));
        Assert.Equal(5, await db.JournalMonetaryEvidence.CountAsync(item => item.CompanyId == fixture.CompanyId));
    }

    [Fact]
    public async Task Sql_server_cross_run_same_ap_identity_converges_to_one_finance_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var first = await fixture.PrepareApAsync("AP-CROSS-RUN", 100m);
        var second = await fixture.PrepareApAsync("AP-CROSS-RUN", 100m);

        var results = await Task.WhenAll(
            fixture.NewApExecution().ExecuteAsync(fixture.Request, first.Run.RunId, "ap-cross-run-a", first.Run.Version),
            fixture.NewApExecution().ExecuteAsync(fixture.Request, second.Run.RunId, "ap-cross-run-b", second.Run.Version));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Code));
        await fixture.AssertOneApEffectAsync("AP-CROSS-RUN", 100m);
    }

    [Fact]
    public async Task Sql_server_ap_lost_response_reads_complete_owner_evidence_without_duplicate_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var proxy = new FaultingFinancePersistence(fixture.Settlement) { ThrowAfterApCreate = true };
        var prepared = await fixture.PrepareApAsync("AP-LOST-RESPONSE", 100m);

        var result = await fixture.NewApExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "ap-lost-response", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(1, proxy.ApCreateCalls);
        await fixture.AssertOneApEffectAsync("AP-LOST-RESPONSE", 100m);
        Assert.Equal(MigrationRunStatus.Completed, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
    }

    [Fact]
    public async Task Sql_server_ap_success_without_owner_readback_is_outcome_unknown_and_hard_stops_retries()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var proxy = new FaultingFinancePersistence(fixture.Settlement) { HideApReadback = true };
        var prepared = await fixture.PrepareApAsync("AP-MISSING-READBACK", 100m);

        var first = await fixture.NewApExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "ap-unknown-a", prepared.Run.Version);
        var sameKey = await fixture.NewApExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "ap-unknown-a", prepared.Run.Version);
        var differentKey = await fixture.NewApExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "ap-unknown-b", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.UnknownOutcome, sameKey.Kind);
        Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
        Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
        Assert.Equal(1, proxy.ApCreateCalls);
        await fixture.AssertOneApEffectAsync("AP-MISSING-READBACK", 100m);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("journal")]
    [InlineData("open-item")]
    public async Task Sql_server_incomplete_ap_owner_evidence_during_confirmation_is_unknown_and_non_replayable(string missing)
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var sourceReference = $"AP-INCOMPLETE-{missing}";
        var proxy = new FaultingFinancePersistence(fixture.Settlement)
        {
            AfterApCreate = _ => fixture.RemoveApEvidenceAsync(missing)
        };
        var prepared = await fixture.PrepareApAsync(sourceReference, 100m);

        var first = await fixture.NewApExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"ap-incomplete-{missing}-a", prepared.Run.Version);
        var sameKey = await fixture.NewApExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"ap-incomplete-{missing}-a", prepared.Run.Version);
        var differentKey = await fixture.NewApExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"ap-incomplete-{missing}-b", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal("finance_ap_opening_evidence_unavailable", first.Code);
        Assert.NotNull(first.Value);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, first.Value!.RunStatus);
        Assert.Equal(MigrationAttemptOutcome.UnknownOutcome, first.Value.AttemptOutcome);
        Assert.Equal(MigrationExecutionEffectDisposition.Unknown, Assert.Single(first.Value.Effects).Disposition);
        Assert.Equal("finance_ap_opening_evidence_unavailable", Assert.Single(first.Value.Effects).SafeCode);
        Assert.Equal(MigrationResultKind.UnknownOutcome, sameKey.Kind);
        Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
        Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
        Assert.Equal(1, proxy.ApCreateCalls);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
        await fixture.AssertIncompleteApEvidenceAsync(missing);
    }

    [Fact]
    public async Task Sql_server_ap_preflight_covers_due_date_payment_term_and_supplier_authority_matrix()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var baseCommand = fixture.ApCommand("AP-PREFLIGHT-MATRIX", 100m);

        var dueDateOnly = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand);
        Assert.True(dueDateOnly.Ready, dueDateOnly.Code);

        var termOnly = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-TERM", DueDate = null, PaymentTermId = fixture.PaymentTermId });
        Assert.True(termOnly.Ready, termOnly.Code);
        Assert.Equal(new DateOnly(2026, 2, 14), termOnly.DueDate);

        var matching = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-MATCH", PaymentTermId = fixture.PaymentTermId });
        Assert.True(matching.Ready, matching.Code);
        var mismatch = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-MISMATCH", DueDate = new DateOnly(2026, 2, 15), PaymentTermId = fixture.PaymentTermId });
        Assert.False(mismatch.Ready);
        Assert.Equal("payment_term_snapshot_mismatch", mismatch.Code);

        var neither = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-NONE", DueDate = null, PaymentTermId = null });
        Assert.False(neither.Ready);
        Assert.Equal("payment_term_not_configured", neither.Code);
        var invalidTerm = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-INVALID-TERM", DueDate = null, PaymentTermId = Guid.NewGuid() });
        Assert.False(invalidTerm.Ready);
        Assert.Equal("payment_term_not_configured", invalidTerm.Code);

        fixture.PaymentTerms.LifecycleState = MasterDataLifecycleState.Inactive;
        var inactiveTerm = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-INACTIVE-TERM", DueDate = null, PaymentTermId = fixture.PaymentTermId });
        Assert.False(inactiveTerm.Ready);
        Assert.Equal("payment_term_not_configured", inactiveTerm.Code);
        fixture.PaymentTerms.LifecycleState = MasterDataLifecycleState.Active;
        fixture.PaymentTerms.EffectiveVersionAvailable = false;
        var invalidVersion = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-INVALID-VERSION", DueDate = null, PaymentTermId = fixture.PaymentTermId });
        Assert.False(invalidVersion.Ready);
        Assert.Equal("payment_term_not_configured", invalidVersion.Code);

        fixture.PaymentTerms.EffectiveVersionAvailable = true;
        fixture.SupplierReader.LifecycleState = MasterDataLifecycleState.Inactive;
        var inactiveSupplier = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-INACTIVE-SUPPLIER" });
        Assert.False(inactiveSupplier.Ready);
        Assert.Equal("party_scope_denied", inactiveSupplier.Code);
        fixture.SupplierReader.LifecycleState = MasterDataLifecycleState.Active;
        fixture.SupplierReader.ForeignTenant = true;
        var foreignSupplier = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-FOREIGN-SUPPLIER" });
        Assert.False(foreignSupplier.Ready);
        Assert.Equal("party_scope_denied", foreignSupplier.Code);
        fixture.SupplierReader.ForeignTenant = false;
        var missingSupplier = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-MISSING-SUPPLIER", SupplierId = Guid.NewGuid() });
        Assert.False(missingSupplier.Ready);
        Assert.Equal("party_scope_denied", missingSupplier.Code);
        var wrongCompany = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, baseCommand with { SourceReference = "AP-PREFLIGHT-WRONG-COMPANY", CompanyId = Guid.NewGuid() });
        Assert.False(wrongCompany.Ready);
        Assert.Equal("company_scope_denied", wrongCompany.Code);
    }

    [Fact]
    public async Task Sql_server_ap_preflight_covers_period_rule_account_allocation_approval_and_overlap_matrix()
    {
        async Task<(bool Ready, string Code)> CheckAsync(Func<ArSqlFixture, Task> arrange, Func<FinanceMigrationApOpeningCommand, FinanceMigrationApOpeningCommand>? alter = null)
        {
            await using var fixture = await ArSqlFixture.CreateAsync(safety);
            await arrange(fixture);
            var command = alter?.Invoke(fixture.ApCommand("AP-RULE-MATRIX-" + Guid.NewGuid().ToString("N"), 100m)) ?? fixture.ApCommand("AP-RULE-MATRIX-" + Guid.NewGuid().ToString("N"), 100m);
            var result = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, command);
            return (result.Ready, result.Code);
        }

        var noPeriod = await CheckAsync(_ => Task.CompletedTask, command => command with { OpeningDate = new DateOnly(2027, 1, 15) });
        Assert.False(noPeriod.Ready);
        Assert.Equal("period_not_configured", noPeriod.Code);

        var ambiguousPeriod = await CheckAsync(async fixture =>
        {
            var calendar = Assert.Single(await fixture.Finance.ListCalendarsAsync(fixture.FinanceContextFor("tenant.finance.calendar.manage"), fixture.CompanyId));
            var year = Assert.Single(await fixture.Finance.ListYearsAsync(fixture.FinanceContextFor("tenant.finance.calendar.manage"), calendar.Id));
            await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
            db.FiscalPeriods.Add(new FinanceFiscalPeriodEntity(fixture.Tenant.TenantId, Guid.NewGuid(), new FinanceFiscalPeriodCommand(year.Id, 2, "S7-2026-OVERLAP", "Overlap", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "s7-period-overlap", "s7-period-overlap"), fixture.CompanyId));
            await db.SaveChangesAsync();
            var overlap = await db.FiscalPeriods.SingleAsync(item => item.Code == "S7-2026-OVERLAP");
            overlap.SetState(FinanceFiscalPeriodState.Open);
            await db.SaveChangesAsync();
        });
        Assert.False(ambiguousPeriod.Ready);
        Assert.Equal("period_ambiguous", ambiguousPeriod.Code);

        var softClosed = await CheckAsync(async fixture =>
        {
            var period = Assert.Single(await fixture.Finance.ListPeriodsAsync(fixture.FinanceContextFor("tenant.finance.calendar.manage"), fixture.FiscalYearId));
            var closed = await fixture.Finance.SetPeriodStateAsync(fixture.FinanceContextFor("tenant.finance.calendar.manage"), new FinancePeriodStateCommand(period.Id, FinanceFiscalPeriodState.SoftClosed, "s7 soft-close control", period.Version, "s7-period-soft-close", "s7-period-soft-close"));
            Assert.True(closed.Succeeded, closed.Code);
        });
        Assert.False(softClosed.Ready);
        Assert.Equal("period_soft_closed", softClosed.Code);

        var closedPeriod = await CheckAsync(async fixture =>
        {
            var period = Assert.Single(await fixture.Finance.ListPeriodsAsync(fixture.FinanceContextFor("tenant.finance.calendar.manage"), fixture.FiscalYearId));
            var closed = await fixture.Finance.SetPeriodStateAsync(fixture.FinanceContextFor("tenant.finance.calendar.manage"), new FinancePeriodStateCommand(period.Id, FinanceFiscalPeriodState.Closed, "s7 close control", period.Version, "s7-period-close", "s7-period-close"));
            Assert.True(closed.Succeeded, closed.Code);
        });
        Assert.False(closedPeriod.Ready);
        Assert.Equal("period_closed", closedPeriod.Code);

        var missingRule = await CheckAsync(async fixture =>
        {
            var disabled = await fixture.Finance.SetPostingRuleLifecycleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), fixture.ApRuleId, fixture.CompanyId, FinancePostingRuleLifecycle.Disabled, fixture.ApRuleVersion, "s7-ap-rule-disable", "s7-ap-rule-disable");
            Assert.True(disabled.Succeeded, disabled.Code);
        });
        Assert.False(missingRule.Ready);
        Assert.Equal("pending_mapping", missingRule.Code);

        var ambiguousRule = await CheckAsync(async fixture =>
        {
            // The owner API rejects overlapping rules. Insert a legacy duplicate only in this disposable test database.
            await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
            db.PostingRules.Add(new FinancePostingRuleEntity(
                fixture.Tenant.TenantId,
                Guid.NewGuid(),
                new FinancePostingRuleCommand(fixture.CompanyId, "migration-ap-opening.v1", "recognition", fixture.ApOffsetAccountId, fixture.ApAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s7-ap-rule-duplicate", "s7-ap-rule-duplicate"),
                2,
                "S7-AP-OFFSET",
                "S7-AP"));
            await db.SaveChangesAsync();
        });
        Assert.False(ambiguousRule.Ready);
        Assert.Equal("ambiguous_mapping", ambiguousRule.Code);

        var invalidRule = await CheckAsync(async fixture =>
        {
            var disabled = await fixture.Finance.SetPostingRuleLifecycleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), fixture.ApRuleId, fixture.CompanyId, FinancePostingRuleLifecycle.Disabled, fixture.ApRuleVersion, "s7-ap-invalid-disable", "s7-ap-invalid-disable");
            Assert.True(disabled.Succeeded, disabled.Code);
            await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
            db.PostingRules.Add(new FinancePostingRuleEntity(
                fixture.Tenant.TenantId,
                Guid.NewGuid(),
                new FinancePostingRuleCommand(fixture.CompanyId, "migration-ap-opening.v1", "recognition", fixture.ApOffsetAccountId, fixture.ApOffsetAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s7-ap-invalid-rule", "s7-ap-invalid-rule"),
                2,
                "S7-AP-OFFSET",
                "S7-AP-OFFSET"));
            await db.SaveChangesAsync();
        });
        Assert.False(invalidRule.Ready);
        Assert.Equal("posting_rule_accounts_invalid", invalidRule.Code);

        var nonPostable = await CheckAsync(async fixture =>
        {
            var account = (await fixture.Finance.ListAccountsAsync(fixture.FinanceContextFor("tenant.finance.account.manage"), fixture.CompanyId)).Single(item => item.Id == fixture.ApAccountId);
            var edited = await fixture.Finance.EditAccountAsync(fixture.FinanceContextFor("tenant.finance.account.manage"), new FinanceAccountCommand(account.CompanyId, account.Code, account.EnglishName, account.ArabicName, account.ParentAccountId, account.AccountType, false, account.CurrencyBehavior, account.EffectiveFrom, account.EffectiveTo, account.Id, account.Version, "s7-ap-account-nonpostable", "s7-ap-account-nonpostable"));
            Assert.True(edited.Succeeded, edited.Code);
        });
        Assert.False(nonPostable.Ready);
        Assert.Equal("account_not_postable", nonPostable.Code);

        var inactiveAccount = await CheckAsync(async fixture =>
        {
            var account = (await fixture.Finance.ListAccountsAsync(fixture.FinanceContextFor("tenant.finance.account.manage"), fixture.CompanyId)).Single(item => item.Id == fixture.ApAccountId);
            var disabled = await fixture.Finance.SetAccountLifecycleAsync(fixture.FinanceContextFor("tenant.finance.account.manage"), account.Id, fixture.CompanyId, FinanceAccountLifecycle.Inactive, account.Version, "s7-ap-account-inactive", "s7-ap-account-inactive");
            Assert.True(disabled.Succeeded, disabled.Code);
        });
        Assert.False(inactiveAccount.Ready);
        Assert.Equal("account_not_postable", inactiveAccount.Code);

        var expiredAccount = await CheckAsync(async fixture =>
        {
            var account = (await fixture.Finance.ListAccountsAsync(fixture.FinanceContextFor("tenant.finance.account.manage"), fixture.CompanyId)).Single(item => item.Id == fixture.ApAccountId);
            var edited = await fixture.Finance.EditAccountAsync(fixture.FinanceContextFor("tenant.finance.account.manage"), new FinanceAccountCommand(account.CompanyId, account.Code, account.EnglishName, account.ArabicName, account.ParentAccountId, account.AccountType, account.IsPostingAccount, account.CurrencyBehavior, account.EffectiveFrom, new DateOnly(2026, 1, 14), account.Id, account.Version, "s7-ap-account-expired", "s7-ap-account-expired"));
            Assert.True(edited.Succeeded, edited.Code);
        });
        Assert.False(expiredAccount.Ready);
        Assert.Equal("account_not_postable", expiredAccount.Code);

        var dimensionRequired = await CheckAsync(async fixture =>
        {
            var disabled = await fixture.Finance.SetPostingRuleLifecycleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), fixture.ApRuleId, fixture.CompanyId, FinancePostingRuleLifecycle.Disabled, fixture.ApRuleVersion, "s7-ap-rule-dimension-disable", "s7-ap-rule-dimension-disable");
            Assert.True(disabled.Succeeded, disabled.Code);
            var replacement = await fixture.Finance.CreatePostingRuleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), new FinancePostingRuleCommand(fixture.CompanyId, "migration-ap-opening.v1", "recognition", fixture.ApOffsetAccountId, fixture.ApAccountId, true, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s7-ap-rule-dimension", "s7-ap-rule-dimension"));
            Assert.True(replacement.Succeeded, replacement.Code);
        });
        Assert.False(dimensionRequired.Ready);
        Assert.Equal("dimension_required", dimensionRequired.Code);

        var missingAllocation = await CheckAsync(async fixture =>
        {
            var disabled = await fixture.Finance.SetPostingRuleLifecycleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), fixture.AllocationRuleId, fixture.CompanyId, FinancePostingRuleLifecycle.Disabled, fixture.AllocationRuleVersion, "s7-allocation-disable", "s7-allocation-disable");
            Assert.True(disabled.Succeeded, disabled.Code);
        });
        Assert.False(missingAllocation.Ready);
        Assert.Equal("payment_allocation_rule_not_configured", missingAllocation.Code);

        var allocationMismatch = await CheckAsync(async fixture =>
        {
            var disabled = await fixture.Finance.SetPostingRuleLifecycleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), fixture.AllocationRuleId, fixture.CompanyId, FinancePostingRuleLifecycle.Disabled, fixture.AllocationRuleVersion, "s7-allocation-mismatch-disable", "s7-allocation-mismatch-disable");
            Assert.True(disabled.Succeeded, disabled.Code);
            var replacement = await fixture.Finance.CreatePostingRuleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), new FinancePostingRuleCommand(fixture.CompanyId, "supplier-payment.v1", "allocation", fixture.ApOffsetAccountId, fixture.ApAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s7-allocation-mismatch", "s7-allocation-mismatch"));
            Assert.True(replacement.Succeeded, replacement.Code);
        });
        Assert.False(allocationMismatch.Ready);
        Assert.Equal("payment_allocation_control_mismatch", allocationMismatch.Code);

        var approvalRequired = await CheckAsync(fixture => { fixture.Approval.Requirement = FinanceApprovalRequirement.Required; return Task.CompletedTask; });
        Assert.False(approvalRequired.Ready);
        Assert.Equal("approval_required", approvalRequired.Code);
        var approvalNotConfigured = await CheckAsync(fixture => { fixture.Approval.Requirement = FinanceApprovalRequirement.NotConfigured; return Task.CompletedTask; });
        Assert.False(approvalNotConfigured.Ready);
        Assert.Equal("approval_policy_not_configured", approvalNotConfigured.Code);

        var inventoryOverlap = await CheckAsync(async fixture =>
        {
            var disabled = await fixture.Finance.SetPostingRuleLifecycleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), fixture.InventoryRuleId, fixture.CompanyId, FinancePostingRuleLifecycle.Disabled, fixture.InventoryRuleVersion, "s7-inventory-disable", "s7-inventory-disable");
            Assert.True(disabled.Succeeded, disabled.Code);
            var replacement = await fixture.Finance.CreatePostingRuleAsync(fixture.FinanceContextFor("tenant.finance.posting-rule.manage"), new FinancePostingRuleCommand(fixture.CompanyId, "inventory-valuation-finance.v1", "OpeningBalance:Inbound", fixture.ApAccountId, fixture.ApOffsetAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s7-inventory-overlap", "s7-inventory-overlap"));
            Assert.True(replacement.Succeeded, replacement.Code);
        });
        Assert.False(inventoryOverlap.Ready);
        Assert.Equal("ap_control_account_overlaps_inventory", inventoryOverlap.Code);

        var cashOverlap = await CheckAsync(async fixture =>
        {
            var cash = await fixture.Settlement.CreateCashAccountAsync(fixture.FinanceContextFor("tenant.finance.settlement.manage"), new FinanceCashAccountCommand(fixture.CompanyId, "S7-AP-OVERLAP-CASH", "AP overlap cash", null, FinanceCashAccountKind.Bank, "SAR", fixture.ApAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s7-ap-overlap-cash", "s7-ap-overlap-cash"));
            Assert.True(cash.Succeeded, cash.Code);
        });
        Assert.False(cashOverlap.Ready);
        Assert.Equal("ap_control_account_overlaps_cash", cashOverlap.Code);
    }

    [Fact]
    public async Task Sql_server_cross_run_same_ar_identity_converges_to_one_finance_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var first = await fixture.PrepareAsync("AR-CROSS-RUN", 100m);
        var second = await fixture.PrepareAsync("AR-CROSS-RUN", 100m);

        var results = await Task.WhenAll(
            fixture.NewExecution().ExecuteAsync(fixture.Request, first.Run.RunId, "cross-run-a", first.Run.Version),
            fixture.NewExecution().ExecuteAsync(fixture.Request, second.Run.RunId, "cross-run-b", second.Run.Version));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Code));
        await fixture.AssertOneEffectAsync("AR-CROSS-RUN", 100m);
    }

    [Fact]
    public async Task Sql_server_cross_run_changed_ar_payload_has_one_winner_and_no_duplicate_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var first = await fixture.PrepareAsync("AR-CROSS-CONFLICT", 100m);
        var second = await fixture.PrepareAsync("AR-CROSS-CONFLICT", 101m);

        var results = await Task.WhenAll(
            fixture.NewExecution().ExecuteAsync(fixture.Request, first.Run.RunId, "cross-conflict-a", first.Run.Version),
            fixture.NewExecution().ExecuteAsync(fixture.Request, second.Run.RunId, "cross-conflict-b", second.Run.Version));

        Assert.Contains(results, result => result.Succeeded);
        Assert.Contains(results, result => !result.Succeeded && result.Kind is (MigrationResultKind.KnownFailure or MigrationResultKind.UnknownOutcome));
        await fixture.AssertOneEffectAsync("AR-CROSS-CONFLICT", results.Single(result => result.Succeeded).Value!.ArEconomicReconciliations!.Single().CanonicalAmount);
    }

    [Fact]
    public async Task Sql_server_ar_post_commit_lost_response_recovers_from_complete_owner_evidence()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var proxy = new FaultingFinancePersistence(fixture.Settlement) { ThrowAfterCreate = true };
        var prepared = await fixture.PrepareAsync("AR-LOST-RESPONSE", 100m);

        var result = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "lost-response", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(1, proxy.CreateCalls);
        await fixture.AssertOneEffectAsync("AR-LOST-RESPONSE", 100m);
        Assert.Equal(MigrationRunStatus.Completed, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
    }

    [Fact]
    public async Task Sql_server_ar_success_without_owner_readback_is_outcome_unknown_without_duplicate_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var proxy = new FaultingFinancePersistence(fixture.Settlement) { HideReadback = true };
        var prepared = await fixture.PrepareAsync("AR-MISSING-READBACK", 100m);

        var result = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "missing-readback", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, result.Kind);
        Assert.Equal(1, proxy.CreateCalls);
        var run = (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!;
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, run.Status);
        await fixture.AssertOneEffectAsync("AR-MISSING-READBACK", 100m);
    }

    [Fact]
    public async Task Sql_server_ar_missing_source_effect_is_not_exact_owner_evidence()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareAsync("AR-INCOMPLETE", 100m);
        var result = await fixture.NewExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "incomplete-create", prepared.Run.Version);
        Assert.True(result.Succeeded, result.Code);

        await using (var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant))
        {
            var effects = await db.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync();
            db.SourceEffects.RemoveRange(effects);
            await db.SaveChangesAsync();
        }

        var command = fixture.Command("AR-INCOMPLETE", 100m);
        Assert.Null(await fixture.Settlement.ReadMigrationArOpeningAsync(fixture.FinanceContext, command));
    }

    [Fact]
    public async Task Sql_server_mixed_inventory_ar_and_ap_preflight_before_any_economic_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("AR-MIXED", 100m);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "mixed-success", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationRunStatus.Completed, result.Value!.RunStatus);
        Assert.Equal(3, result.Value.Effects.Count);
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.InventoryOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.ApOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(10, result.Value.Representations!.Count);

        await fixture.AssertMixedEffectsAsync("AR-MIXED", 100m);
    }

    [Fact]
    public async Task Sql_server_mixed_inventory_ar_and_ap_ap_preflight_failure_has_zero_economic_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        const string sourceReference = "AR-MIXED-AP-BLOCKED";
        await fixture.DisableApAccountAsync();
        var inventoryPreflight = await fixture.Finance.PreflightInventoryOpeningAsync(fixture.FinanceContext, fixture.CompanyId, new DateOnly(2026, 1, 15));
        var arPreflight = await fixture.Settlement.PreflightMigrationArOpeningAsync(fixture.FinanceContext, fixture.Command(sourceReference, 100m));
        var apPreflight = await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, fixture.ApCommand(sourceReference + "-AP", 100m));
        Assert.True(inventoryPreflight.Ready, inventoryPreflight.Code);
        Assert.True(arPreflight.Ready, arPreflight.Code);
        Assert.False(apPreflight.Ready);
        Assert.Equal("account_not_postable", apPreflight.Code);
        var prepared = await fixture.PrepareMixedAsync(sourceReference, 100m);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "mixed-preflight-failure", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("account_not_postable", result.Code);
        var run = (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!;
        Assert.Equal(MigrationRunStatus.Approved, run.Status);
        await fixture.AssertNoMixedEffectsAsync(sourceReference);
    }

    [Fact]
    public async Task Sql_server_mixed_inventory_ar_ap_and_cash_bank_execution_has_one_effect_per_row()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("S8-MIXED", 100m, includeCash: true);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-mixed-success", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationRunStatus.Completed, result.Value!.RunStatus);
        Assert.Equal(4, result.Value.Effects.Count);
        Assert.All(result.Value.Effects, item => Assert.Equal(MigrationExecutionEffectDisposition.Committed, item.Disposition));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.InventoryOpening));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.ArOpening));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.ApOpening));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.CashBankOpening));
        Assert.Equal(13, result.Value.Representations!.Count);
        await fixture.AssertMixedEffectsAsync("S8-MIXED", 100m);
        await fixture.AssertOneCashEffectAsync("S8-MIXED-CASH", 100m);
    }

    [Fact]
    public async Task Sql_server_cash_bank_mixed_preflight_failure_has_zero_new_owner_effects()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.DisableCashAccountAsync();
        const string sourceReference = "S8-MIXED-CASH-BLOCKED";
        Assert.True((await fixture.Finance.PreflightInventoryOpeningAsync(fixture.FinanceContext, fixture.CompanyId, new DateOnly(2026, 1, 15))).Ready);
        Assert.True((await fixture.Settlement.PreflightMigrationArOpeningAsync(fixture.FinanceContext, fixture.Command(sourceReference, 100m))).Ready);
        Assert.True((await fixture.Settlement.PreflightMigrationApOpeningAsync(fixture.FinanceContext, fixture.ApCommand(sourceReference + "-AP", 100m))).Ready);
        var cash = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, fixture.CashCommand(sourceReference + "-CASH", 100m));
        Assert.False(cash.Ready);
        Assert.Equal("cash_account_inactive", cash.Code);

        var prepared = await fixture.PrepareMixedAsync(sourceReference, 100m, includeCash: true);
        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-mixed-cash-blocked", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("cash_account_inactive", result.Code);
        Assert.Equal(MigrationRunStatus.Approved, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
        await fixture.AssertNoMixedEffectsAsync(sourceReference);
    }

    [Fact]
    public async Task Sql_server_migration_all_five_executes_owners_once_and_gl_last()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("MIG-S9-01", 100m, includeCash: true, includeGl: true);
        var proxy = new FaultingFinancePersistence(fixture.Settlement)
        {
            BeforeGlCreate = _ => fixture.AssertOwnerEffectsReadyBeforeGlAsync()
        };

        var result = await fixture.NewMixedExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "mig-s9-01", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationRunStatus.Completed, result.Value!.RunStatus);
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.InventoryOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.ApOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.CashBankOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(10, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.GlOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        var expectations = result.Value.Representations!.Where(item => item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation).ToArray();
        Assert.Equal(4, expectations.Length);
        Assert.All(expectations, item =>
        {
            Assert.Equal(MigrationEconomicOwnerModule.Finance, item.OwnerModule);
            Assert.Equal("migration-economic-expectation-v1", item.EvidenceVersion);
            Assert.True(item.EvidenceConfirmed);
            Assert.NotEqual(Guid.Empty, item.OwnerId);
            Assert.NotNull(item.SourceContract);
            Assert.NotNull(item.SourceEvent);
            Assert.NotNull(item.FunctionalAmount);
            Assert.NotNull(item.PostingRuleId);
            Assert.NotNull(item.PostingRuleVersionNumber);
            Assert.NotNull(item.ControlAccountId);
            Assert.NotNull(item.OffsetAccountId);
            Assert.NotNull(item.OwnerSourceId);
        });
        Assert.Equal(1, proxy.GlCreateCalls);
        await fixture.AssertOneCashEffectAsync("MIG-S9-01-CASH", 100m);
        await fixture.AssertMixedEffectsAsync("MIG-S9-01", 100m);
        await fixture.AssertLedgerMatchesTargetAsync(100m, derivedOffsets: false);
    }

    [Fact]
    public async Task Sql_server_migration_gl_prepare_failure_leaves_all_five_effects_at_zero()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("MIG-S9-02", 100m, includeCash: true, includeGl: true, invalidGl: true);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "mig-s9-02", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("migration_gl_opening_imbalanced", result.Code);
        Assert.Equal(MigrationRunStatus.Approved, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
        await fixture.AssertNoAllFiveEffectsAsync();
    }

    [Fact]
    public async Task Sql_server_migration_inventory_approval_required_stops_all_five_before_owner_effects()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        fixture.Approval.Requirement = FinanceApprovalRequirement.Required;
        var prepared = await fixture.PrepareMixedAsync("MIG-S9-03", 100m, includeCash: true, includeGl: true);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "mig-s9-03", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("approval_required", result.Code);
        Assert.Equal(MigrationRunStatus.Approved, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
        await fixture.AssertNoAllFiveEffectsAsync();
    }

    [Fact]
    public async Task Sql_server_migration_gl_lost_response_recovers_committed_effect_without_duplicate()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("MIG-S9-04", 100m, includeCash: true, includeGl: true);
        var proxy = new FaultingFinancePersistence(fixture.Settlement) { ThrowAfterGlCreate = true };

        var result = await fixture.NewMixedExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "mig-s9-04", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(1, proxy.GlCreateCalls);
        Assert.True(proxy.GlReadCalls >= 1);
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: false));
        await fixture.AssertLedgerMatchesTargetAsync(100m, derivedOffsets: false);
    }

    [Fact]
    public async Task Sql_server_migration_prepared_amount_mismatch_fails_closed_before_gl_effect()
    {
        await AssertPreparedActualMismatchAsync(safety, "MIG-S9-05", (fixture, _) => fixture.MutateArJournalAmountAsync(101m));
    }

    [Fact]
    public async Task Sql_server_migration_prepared_rule_mismatch_fails_closed_before_gl_effect()
    {
        await AssertPreparedActualMismatchAsync(safety, "MIG-S9-06", (fixture, _) => fixture.MutateArJournalRuleAsync());
    }

    [Fact]
    public async Task Sql_server_migration_prepared_control_offset_mismatch_fails_closed_before_gl_effect()
    {
        await AssertPreparedActualMismatchAsync(safety, "MIG-S9-07", (fixture, _) => fixture.MutateArJournalMappingAsync());
    }

    [Fact]
    public async Task Sql_server_migration_committed_owner_without_finance_evidence_fails_closed()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("MIG-S9-08", 100m, includeCash: true, includeGl: true);
        var proxy = new FaultingFinancePersistence(fixture.Settlement)
        {
            BeforeSecondArRead = fixture.RemoveArSourceEffectAsync
        };

        var result = await fixture.NewMixedExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "mig-s9-08", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, result.Kind);
        Assert.Equal(MigrationExecutionEffectDisposition.Committed, Assert.Single(result.Value!.Effects, item => item.RecordType == MigrationCanonicalRecordType.ArOpening).Disposition);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, result.Value.RunStatus);
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: false));
    }

    [Fact]
    public async Task Sql_server_migration_public_gl_reconciliation_exposes_residual_derived_offset_and_all_represented_controls()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("MIG-S9-09", 100m, includeCash: true, includeGl: true, derivedOffsets: true);
        var execution = fixture.NewMixedExecution();
        var completed = await execution.ExecuteAsync(fixture.Request, prepared.Run.RunId, "mig-s9-09", prepared.Run.Version);
        Assert.True(completed.Succeeded, completed.Code);

        var before = await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, completed.Value!.AttemptId);
        var read = await execution.ReadAsync(fixture.Request, prepared.Run.RunId);
        var after = await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, completed.Value.AttemptId);

        // Read-only proof: the public reconciliation read must not create, mutate, or duplicate any
        // durable Finance/Inventory/Migration economic evidence.
        Assert.Equal(before, after);
        var repeatRead = await execution.ReadAsync(fixture.Request, prepared.Run.RunId);
        var afterRepeat = await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, completed.Value.AttemptId);
        Assert.Equal(before, afterRepeat);
        Assert.NotEmpty(read!.GlEconomicReconciliations!);
        Assert.NotEmpty(repeatRead!.GlEconomicReconciliations!);

        var reconciliationLines = read.GlEconomicReconciliations!.SelectMany(item => item.Lines!).ToArray();

        // Residual/offset proof: existing source-residual and derived-offset-clearing lines remain intact.
        Assert.Contains(reconciliationLines, item => item.AccountId == fixture.GlDebitAccountId && item.SourceLineReference is not null && item.AccountingTreatment == "source_residual" && item.JournalLineId is not null && item.ResidualDebit == 50m && item.ResidualCredit == 0m);
        Assert.Contains(read.GlEconomicReconciliations!, item => item.EstablishedDebit > 0m && item.EstablishedCredit > 0m);
        Assert.Contains(reconciliationLines, item => item.SourceLineReference is null && item.AccountingTreatment == "derived_offset_clearing" && item.JournalLineId is not null);

        // Represented control proof: all four fully-represented subsidiary control domains are now
        // explicit, zero-residual, source-preserved, and never carry a fabricated Journal line.
        Assert.Contains(reconciliationLines, item => item.AccountId == fixture.ArAccountId && item.AccountingTreatment == "represented_by_ar" && item.IsControlAccount && item.ResidualDebit == 0m && item.ResidualCredit == 0m && item.TargetSignedAmount == 100m && item.EstablishedSignedAmount == 100m && item.SourceLineReference == "MIG-GL-AR-CONTROL" && item.JournalLineId is null);
        Assert.Contains(reconciliationLines, item => item.AccountId == fixture.ApAccountId && item.AccountingTreatment == "represented_by_ap" && item.IsControlAccount && item.ResidualDebit == 0m && item.ResidualCredit == 0m && item.TargetSignedAmount == -100m && item.EstablishedSignedAmount == -100m && item.SourceLineReference == "MIG-GL-AP-CONTROL" && item.JournalLineId is null);
        Assert.Contains(reconciliationLines, item => item.AccountId == fixture.CashLinkedAccountId && item.AccountingTreatment == "represented_by_cash_bank" && item.IsControlAccount && item.ResidualDebit == 0m && item.ResidualCredit == 0m && item.TargetSignedAmount == 100m && item.EstablishedSignedAmount == 100m && item.SourceLineReference == "MIG-GL-CASH-CONTROL" && item.JournalLineId is null);
        Assert.Contains(reconciliationLines, item => item.AccountId == fixture.InventoryControlAccountId && item.AccountingTreatment == "represented_by_inventory" && item.IsControlAccount && item.ResidualDebit == 0m && item.ResidualCredit == 0m && item.TargetSignedAmount == 100m && item.EstablishedSignedAmount == 100m && item.SourceLineReference == "MIG-GL-INVENTORY-CONTROL" && item.JournalLineId is null);

        // Aggregate totals must remain correct with no double counting: sum of every line's residual
        // debit/credit reconciles to the reconciliation's own reported totals.
        foreach (var reconciliation in read.GlEconomicReconciliations!)
        {
            Assert.Equal(reconciliation.ResidualDebit, reconciliation.Lines!.Sum(item => item.ResidualDebit));
            Assert.Equal(reconciliation.ResidualCredit, reconciliation.Lines!.Sum(item => item.ResidualCredit));
        }

        await using (var financeDb = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant))
        {
            Assert.Equal(1, await financeDb.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1" && item.Status == FinanceJournalStatus.Posted));
            Assert.Equal(1, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1"));
        }
        await fixture.AssertLedgerMatchesTargetAsync(100m, derivedOffsets: true);
    }

    private static async Task AssertPreparedActualMismatchAsync(SqlServerSafetyFixture safety, string sourceReference, Func<ArSqlFixture, string, Task> mutate)
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync(sourceReference, 100m, includeCash: true, includeGl: true);
        var proxy = new FaultingFinancePersistence(fixture.Settlement)
        {
            BeforeSecondArRead = () => mutate(fixture, sourceReference)
        };

        var result = await fixture.NewMixedExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, sourceReference.ToLowerInvariant(), prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, result.Kind);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, result.Value!.RunStatus);
        Assert.Equal(MigrationExecutionEffectDisposition.Committed, Assert.Single(result.Value.Effects, item => item.RecordType == MigrationCanonicalRecordType.ArOpening).Disposition);
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: false));
    }

    internal sealed class ArSqlFixture : IAsyncDisposable
    {
        private readonly Microsoft.Data.SqlClient.SqlConnection connection;
        private readonly MigrationFoundationService foundation;
        private readonly IMigrationReferenceAuthority references = new MixedReferenceAuthority();
        private readonly TenantWideScopeResolver scopes = new();
        private readonly string sourceHash = new('A', 64);
        private readonly string packageHash = new('B', 64);

        private ArSqlFixture(
            Microsoft.Data.SqlClient.SqlConnection connection,
            DbContextOptions financeOptions,
            DbContextOptions inventoryOptions,
            DbContextOptions migrationOptions,
            TenantContext tenant,
            Guid companyId,
            Guid customerId,
            Guid supplierId,
            MutableApprovalPolicy approval,
            ActiveSupplierReader supplierReader,
            TestCurrencyPaymentTermPersistence paymentTerms,
            FinanceSettlementPersistence settlement,
            MutableExchangeRates exchangeRates,
            IFinancePersistence finance,
            InventoryPersistence inventory,
            InventoryService inventoryService,
            InventoryValuationService valuation,
            Guid branchId,
            Guid warehouseId,
            Guid productId,
            Guid unitId,
            Guid currencyId,
            Guid paymentTermId,
            MigrationPersistence migration,
            FoundationRequestContext request)
        {
            this.connection = connection;
            FinanceOptions = financeOptions;
            InventoryOptions = inventoryOptions;
            MigrationOptions = migrationOptions;
            Tenant = tenant;
            CompanyId = companyId;
            CustomerId = customerId;
            SupplierId = supplierId;
            Approval = approval;
            SupplierReader = supplierReader;
            PaymentTerms = paymentTerms;
            Settlement = settlement;
            ExchangeRates = exchangeRates;
            Finance = finance;
            Inventory = inventory;
            InventoryService = inventoryService;
            Valuation = valuation;
            BranchId = branchId;
            WarehouseId = warehouseId;
            ProductId = productId;
            UnitId = unitId;
            CurrencyId = currencyId;
            PaymentTermId = paymentTermId;
            Migration = migration;
            foundation = new MigrationFoundationService(migration, new NoopAuditSink());
            Request = request;
        }

        internal DbContextOptions FinanceOptions { get; }
        internal DbContextOptions InventoryOptions { get; }
        private DbContextOptions MigrationOptions { get; }
        internal TenantContext Tenant { get; }
        internal Guid CompanyId { get; }
        internal Guid CustomerId { get; }
        internal Guid SupplierId { get; }
        internal MutableApprovalPolicy Approval { get; }
        internal ActiveSupplierReader SupplierReader { get; }
        internal TestCurrencyPaymentTermPersistence PaymentTerms { get; }
        internal FinanceSettlementPersistence Settlement { get; }
        internal MutableExchangeRates ExchangeRates { get; }
        internal IFinancePersistence Finance { get; }
        internal InventoryPersistence Inventory { get; }
        internal InventoryService InventoryService { get; }
        internal InventoryValuationService Valuation { get; }
        internal Guid BranchId { get; }
        internal Guid WarehouseId { get; }
        internal Guid ProductId { get; }
        internal Guid UnitId { get; }
        internal Guid CurrencyId { get; }
        internal Guid PaymentTermId { get; }
        internal Guid ArAccountId { get; private set; }
        internal Guid ArOffsetAccountId { get; private set; }
        internal Guid ApAccountId { get; private set; }
        internal Guid ApOffsetAccountId { get; private set; }
        internal Guid AllocationRuleId { get; private set; }
        internal byte[] AllocationRuleVersion { get; private set; } = [];
        internal Guid InventoryRuleId { get; private set; }
        internal Guid InventoryControlAccountId { get; private set; }
        internal Guid InventoryOffsetAccountId { get; private set; }
        internal byte[] InventoryRuleVersion { get; private set; } = [];
        internal Guid FiscalYearId { get; private set; }
        internal Guid PeriodId { get; private set; }
        internal Guid ApRuleId { get; private set; }
        internal byte[] ApRuleVersion { get; private set; } = [];
        internal Guid CashAccountId { get; private set; }
        internal Guid CashLinkedAccountId { get; private set; }
        internal Guid CashOffsetAccountId { get; private set; }
        internal Guid CashRuleId { get; private set; }
        internal byte[] CashRuleVersion { get; private set; } = [];
        internal Guid GlDebitAccountId { get; private set; }
        internal Guid GlCreditAccountId { get; private set; }
        internal MigrationPersistence Migration { get; }
        internal FoundationRequestContext Request { get; }
        internal FinanceRequestContext FinanceContext => FinanceRequestContext.TryCreate(Request, out var context) ? context! : throw new InvalidOperationException();

        internal static async Task<ArSqlFixture> CreateAsync(SqlServerSafetyFixture safety)
        {
            var connection = await safety.OpenConnectionAsync();
            var financeOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
            var inventoryOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.InventoryHistoryTable);
            var migrationOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);
            var tenant = TenantContext.ForOrdinaryMembership(safety.TenantA.TenantId, new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("ar-s6-r4"), actorId: Guid.NewGuid());
            var companyId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var supplierId = Guid.Parse("88888888-8888-8888-8888-888888888888");
            var branchId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var unitId = Guid.NewGuid();
            var currencyId = Guid.NewGuid();
            var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenant.TenantId.Value, companyId, "S6 AR company", "SAR")]);
            var approval = new MutableApprovalPolicy();
            var currency = new MasterDataCurrencyRecord(currencyId, tenant.TenantId, "SAR", new LocalizedName("Saudi Riyal"), MasterDataLifecycleState.Active, 1, [1]);
            var currencies = new TestCurrencyPaymentTermPersistence(currency, Guid.NewGuid());
            var exchangeRates = new MutableExchangeRates(tenant.TenantId);
            var warehouses = new ConfiguredInventoryWarehouseProvider([new InventoryWarehouseOption(tenant.TenantId.Value, companyId, branchId, warehouseId, "S6-WH", "S6 warehouse")]);
            var products = new StaticInventoryProductProvider(new InventoryProductReference(tenant.TenantId.Value, productId, "S6-SKU", "S6 product", unitId, "EA", true, true, true));
            var inventory = new InventoryPersistence(inventoryOptions);
            var valuationPersistence = new InventoryValuationPersistence(inventoryOptions, null, null, new UnavailableMasterDataExchangeRatePersistence());
            var inventoryAuthorization = new InventoryResourceAuthorizationService();
            var inventoryService = new InventoryService(inventory, inventoryAuthorization, warehouses, products);
            var valuation = new InventoryValuationService(valuationPersistence, inventoryAuthorization, warehouses, currencies);
            var setup = new FinancePersistence(financeOptions, companies, valuationPersistence, exchangeRates, approval);
            var supplierReader = new ActiveSupplierReader(supplierId);
            var settlement = new FinanceSettlementPersistence(financeOptions, companies, exchangeRates, new ActiveCustomerReader(customerId), supplierReader, currencies, new UnavailableFinanceSupplierInvoiceSourceProvider(), approval);
            var fixture = new ArSqlFixture(connection, financeOptions, inventoryOptions, migrationOptions, tenant, companyId, customerId, supplierId, approval, supplierReader, currencies, settlement, exchangeRates, setup, inventory, inventoryService, valuation, branchId, warehouseId, productId, unitId, currencyId, currencies.PaymentTermId, new MigrationPersistence(migrationOptions), FoundationContext(tenant, "tenant.migration.execute"));
            var inventoryContext = new InventoryTenantContextResolver().Resolve(FoundationContext(tenant, "tenant.inventory.valuation.policy.create"));
            Assert.True(inventoryContext.Allowed, inventoryContext.Code);
            var policy = await valuation.CreatePolicyAsync(inventoryContext.Context!, new InventoryValuationPolicyRequest(companyId, currencyId, "SAR", InventoryValuationScopeMode.WarehouseProductUomTracking, new DateOnly(2026, 1, 15), null, 2, 2, InventoryValuationRoundingMode.ToEven, "PurchaseOrderUnitPrice", "CurrentMovingAverage", "CurrentMovingAverage"), "s6-mixed-valuation-policy");
            Assert.True(policy.Succeeded, policy.Code);
            var accountContext = fixture.FinanceContextFor("tenant.finance.account.manage");
            var calendarContext = fixture.FinanceContextFor("tenant.finance.calendar.manage");
            var ruleContext = fixture.FinanceContextFor("tenant.finance.posting-rule.manage");
            var arAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "S6-AR", FinanceAccountType.Asset));
            var offsetAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "S6-OFFSET", FinanceAccountType.Equity));
            var receiptAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "S6-RECEIPT", FinanceAccountType.Asset));
            Assert.True(arAccount.Succeeded, arAccount.Code);
            Assert.True(offsetAccount.Succeeded, offsetAccount.Code);
            Assert.True(receiptAccount.Succeeded, receiptAccount.Code);
            var calendar = await setup.CreateCalendarAsync(calendarContext, new FinanceFiscalCalendarCommand(companyId, "S6 FY", Guid.NewGuid(), "s6-calendar", "s6-calendar"));
            Assert.True(calendar.Succeeded, calendar.Code);
            var year = await setup.CreateYearAsync(calendarContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "s6-year", "s6-year"));
            Assert.True(year.Succeeded, year.Code);
            var period = await setup.CreatePeriodAsync(calendarContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "S6-2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "s6-period", "s6-period"));
            Assert.True(period.Succeeded, period.Code);
            var opened = await setup.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "s6-period-open", "s6-period-open"));
            Assert.True(opened.Succeeded, opened.Code);
            var arRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "migration-ar-opening.v1", "recognition", arAccount.Value!.Id, offsetAccount.Value!.Id, false, new DateOnly(2026, 1, 15), null, Guid.NewGuid(), "s6-ar-rule", "s6-ar-rule"));
            var receiptRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "customer-receipt.v1", "allocation", receiptAccount.Value!.Id, arAccount.Value.Id, false, new DateOnly(2026, 1, 15), null, Guid.NewGuid(), "s6-receipt-rule", "s6-receipt-rule"));
            var inventoryDebit = await setup.CreateAccountAsync(accountContext, Account(companyId, "S6-INV", FinanceAccountType.Asset));
            var inventoryCredit = await setup.CreateAccountAsync(accountContext, Account(companyId, "S6-INV-OFFSET", FinanceAccountType.Equity));
            Assert.True(arRule.Succeeded, arRule.Code);
            Assert.True(receiptRule.Succeeded, receiptRule.Code);
            Assert.True(inventoryDebit.Succeeded, inventoryDebit.Code);
            Assert.True(inventoryCredit.Succeeded, inventoryCredit.Code);
            var inventoryRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "inventory-valuation-finance.v1", "OpeningBalance:Inbound", inventoryDebit.Value!.Id, inventoryCredit.Value!.Id, false, new DateOnly(2026, 1, 15), null, Guid.NewGuid(), "s6-inventory-rule", "s6-inventory-rule"));
            var apAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "S7-AP", FinanceAccountType.Liability));
            var apOffset = await setup.CreateAccountAsync(accountContext, Account(companyId, "S7-AP-OFFSET", FinanceAccountType.Equity));
            Assert.True(apAccount.Succeeded, apAccount.Code);
            Assert.True(apOffset.Succeeded, apOffset.Code);
            var apRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "migration-ap-opening.v1", "recognition", apOffset.Value!.Id, apAccount.Value!.Id, false, new DateOnly(2026, 1, 15), null, Guid.NewGuid(), "s7-ap-rule", "s7-ap-rule"));
            var apAllocationRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "supplier-payment.v1", "allocation", apAccount.Value.Id, receiptAccount.Value!.Id, false, new DateOnly(2026, 1, 15), null, Guid.NewGuid(), "s7-ap-allocation-rule", "s7-ap-allocation-rule"));
            Assert.True(inventoryRule.Succeeded, inventoryRule.Code);
            Assert.True(apRule.Succeeded, apRule.Code);
            Assert.True(apAllocationRule.Succeeded, apAllocationRule.Code);
            var cashAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "S8-CASH", FinanceAccountType.Asset));
            var cashOffset = await setup.CreateAccountAsync(accountContext, Account(companyId, "S8-CASH-OFFSET", FinanceAccountType.Equity));
            Assert.True(cashAccount.Succeeded, cashAccount.Code);
            Assert.True(cashOffset.Succeeded, cashOffset.Code);
            var configuredCash = await settlement.CreateCashAccountAsync(fixture.FinanceContextFor("tenant.finance.settlement.manage"), new FinanceCashAccountCommand(companyId, "S8-BANK", "Slice 8 bank", null, FinanceCashAccountKind.Bank, "SAR", cashAccount.Value!.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s8-cash-account", "s8-cash-account"));
            Assert.True(configuredCash.Succeeded, configuredCash.Code);
            var cashRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "migration-cash-bank-opening.v1", "recognition", cashAccount.Value.Id, cashOffset.Value!.Id, false, new DateOnly(2026, 1, 15), null, Guid.NewGuid(), "s8-cash-rule", "s8-cash-rule"));
            Assert.True(cashRule.Succeeded, cashRule.Code);
            var glDebit = await setup.CreateAccountAsync(accountContext, Account(companyId, "S9-GL-DEBIT", FinanceAccountType.Asset));
            var glCredit = await setup.CreateAccountAsync(accountContext, Account(companyId, "S9-GL-CREDIT", FinanceAccountType.Equity));
            Assert.True(glDebit.Succeeded, glDebit.Code);
            Assert.True(glCredit.Succeeded, glCredit.Code);
            fixture.ArAccountId = arAccount.Value.Id;
            fixture.ArOffsetAccountId = offsetAccount.Value.Id;
            fixture.ApAccountId = apAccount.Value.Id;
            fixture.ApOffsetAccountId = apOffset.Value.Id;
            fixture.AllocationRuleId = apAllocationRule.Value!.Id;
            fixture.AllocationRuleVersion = apAllocationRule.Value.Version;
            fixture.InventoryRuleId = inventoryRule.Value!.Id;
            fixture.InventoryControlAccountId = inventoryDebit.Value!.Id;
            fixture.InventoryOffsetAccountId = inventoryCredit.Value!.Id;
            fixture.InventoryRuleVersion = inventoryRule.Value.Version;
            fixture.FiscalYearId = year.Value.Id;
            fixture.PeriodId = period.Value.Id;
            fixture.ApRuleId = apRule.Value!.Id;
            fixture.ApRuleVersion = apRule.Value.Version;
            fixture.CashAccountId = configuredCash.Value!.Id;
            fixture.CashLinkedAccountId = cashAccount.Value.Id;
            fixture.CashOffsetAccountId = cashOffset.Value.Id;
            fixture.CashRuleId = cashRule.Value!.Id;
            fixture.CashRuleVersion = cashRule.Value.Version;
            fixture.GlDebitAccountId = glDebit.Value!.Id;
            fixture.GlCreditAccountId = glCredit.Value!.Id;
            return fixture;
        }

        internal FinanceRequestContext FinanceContextFor(string permission)
        {
            Assert.True(FinanceRequestContext.TryCreate(FoundationContext(Tenant, permission), out var context));
            return context!;
        }

        internal async Task<PreparedRun> PrepareAsync(string sourceReference, decimal amount, string currencyCode = "SAR")
        {
            var objectId = Guid.NewGuid();
            var run = MigrationRun.Create(Tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(objectId, Tenant.TenantId, null, null, null, sourceHash, 1, 1);
            var intakeKey = new MigrationIdempotencyKey($"s6-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(Tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("s6-intake-fingerprint"), MigrationIntakeFingerprint.Version, source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(Tenant, new MigrationEvidenceReference(intake.Value!.Run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
            var persisted = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var json = JsonSerializer.Serialize(new { companyId = CompanyId, customerId = CustomerId, sourceReference, documentDate = new DateOnly(2026, 1, 10), openingDate = new DateOnly(2026, 1, 15), amount, currencyCode, dueDate = new DateOnly(2026, 2, 14) });
            var staged = new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 1, sourceReference, MigrationCanonicalRecordType.ArOpening, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow);
            Assert.True((await Migration.StagePackageAsync(Tenant, new StageMigrationPackageCommand(persisted.RunId, objectId, sourceHash, packageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, [staged]))).Succeeded);
            var current = await TransitionAsync(persisted, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, $"s6-validation-{Guid.NewGuid():N}", "s6-validation-fingerprint");
            var validation = new MigrationValidationSummary(Guid.NewGuid(), Tenant.TenantId, current.RunId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.ArOpening, MigrationRecordDisposition.Accepted, [])], DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, validationAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "validation_completed", validationAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Validated);
            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, $"s6-dry-{Guid.NewGuid():N}", "s6-dry-fingerprint");
            var dryRun = new MigrationDryRunPreview(Guid.NewGuid(), Tenant.TenantId, current.RunId, dryAttempt.AttemptId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.ArOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null)], DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, dryAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "dry_run_completed", dryAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, [staged]);
        }

        internal async Task<PreparedRun> PrepareApAsync(string sourceReference, decimal amount, string currencyCode = "SAR")
        {
            var objectId = Guid.NewGuid();
            var run = MigrationRun.Create(Tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(objectId, Tenant.TenantId, null, null, null, sourceHash, 1, 1);
            var intakeKey = new MigrationIdempotencyKey($"s7-ap-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(Tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("s7-ap-intake-fingerprint"), MigrationIntakeFingerprint.Version, source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(Tenant, new MigrationEvidenceReference(intake.Value!.Run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
            var persisted = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var json = JsonSerializer.Serialize(new { companyId = CompanyId, supplierId = SupplierId, sourceReference, documentDate = new DateOnly(2026, 1, 10), openingDate = new DateOnly(2026, 1, 15), amount, currencyCode, dueDate = new DateOnly(2026, 2, 14) });
            var staged = new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 1, sourceReference, MigrationCanonicalRecordType.ApOpening, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow);
            Assert.True((await Migration.StagePackageAsync(Tenant, new StageMigrationPackageCommand(persisted.RunId, objectId, sourceHash, packageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, [staged]))).Succeeded);
            var current = await TransitionAsync(persisted, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, $"s7-ap-validation-{Guid.NewGuid():N}", "s7-ap-validation-fingerprint");
            var validation = new MigrationValidationSummary(Guid.NewGuid(), Tenant.TenantId, current.RunId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.ApOpening, MigrationRecordDisposition.Accepted, [])], DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, validationAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "validation_completed", validationAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Validated);
            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, $"s7-ap-dry-{Guid.NewGuid():N}", "s7-ap-dry-fingerprint");
            var dryRun = new MigrationDryRunPreview(Guid.NewGuid(), Tenant.TenantId, current.RunId, dryAttempt.AttemptId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.ApOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null)], DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, dryAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "dry_run_completed", dryAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, [staged]);
        }

        internal async Task<PreparedRun> PrepareInventoryOnlyAsync(string sourceReference, string currencyCode = "SAR")
        {
            var objectId = Guid.NewGuid();
            var run = MigrationRun.Create(Tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(objectId, Tenant.TenantId, null, null, null, sourceHash, 1, 1);
            var intakeKey = new MigrationIdempotencyKey($"s10-inventory-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(Tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("s10-inventory-intake-fingerprint"), MigrationIntakeFingerprint.Version, source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(Tenant, new MigrationEvidenceReference(intake.Value!.Run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
            var persisted = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var inventoryPayload = new MigrationInventoryOpeningPayload(CompanyId, BranchId, WarehouseId, ProductId, UnitId, 2m, 50m, currencyCode, new DateOnly(2026, 1, 15), TrackingIdentity: sourceReference + "-LOT", SourceLineReference: sourceReference);
            var json = JsonSerializer.Serialize(inventoryPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var staged = new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 1, sourceReference, MigrationCanonicalRecordType.InventoryOpening, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow);
            Assert.True((await Migration.StagePackageAsync(Tenant, new StageMigrationPackageCommand(persisted.RunId, objectId, sourceHash, packageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, [staged]))).Succeeded);
            var current = await TransitionAsync(persisted, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, $"s10-inventory-validation-{Guid.NewGuid():N}", "s10-inventory-validation-fingerprint");
            var validation = new MigrationValidationSummary(Guid.NewGuid(), Tenant.TenantId, current.RunId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.InventoryOpening, MigrationRecordDisposition.Accepted, [])], DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, validationAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "validation_completed", validationAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Validated);
            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, $"s10-inventory-dry-{Guid.NewGuid():N}", "s10-inventory-dry-fingerprint");
            var dryRun = new MigrationDryRunPreview(Guid.NewGuid(), Tenant.TenantId, current.RunId, dryAttempt.AttemptId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.InventoryOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null)], DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, dryAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "dry_run_completed", dryAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, [staged]);
        }

        internal MigrationExecutionService NewExecution(IFinanceSettlementPersistence? finance = null, IMigrationReferenceAuthority? referenceAuthority = null)
        {
            var ar = new MigrationArOpeningExecutionCoordinator(Migration, referenceAuthority ?? references, finance ?? Settlement);
            return new MigrationExecutionService(foundation, Migration, Migration, Migration, scopes, scopes, new UnusedOwnerGateway(), references, null, ar);
        }

        internal MigrationExecutionService NewApExecution(IFinanceSettlementPersistence? finance = null)
        {
            var ap = new MigrationApOpeningExecutionCoordinator(Migration, references, finance ?? Settlement);
            return new MigrationExecutionService(foundation, Migration, Migration, Migration, scopes, scopes, new UnusedOwnerGateway(), references, null, null, ap);
        }

        internal MigrationExecutionService NewMixedExecution(IFinanceSettlementPersistence? finance = null)
        {
            var settlement = finance ?? Settlement;
            var ar = new MigrationArOpeningExecutionCoordinator(Migration, references, settlement);
            var inventory = new MigrationInventoryOpeningExecutionCoordinator(Migration, references, InventoryService, Valuation, Finance);
            var ap = new MigrationApOpeningExecutionCoordinator(Migration, references, settlement);
            var cash = new MigrationCashBankOpeningExecutionCoordinator(Migration, references, settlement);
            var gl = new MigrationGlOpeningExecutionCoordinator(Migration, references, settlement, Valuation);
            return new MigrationExecutionService(foundation, Migration, Migration, Migration, scopes, scopes, new UnusedOwnerGateway(), references, inventory, ar, ap, null, cash, gl);
        }

        internal FinanceMigrationArOpeningCommand Command(string sourceReference, decimal amount) => new(CompanyId, CustomerId, sourceReference, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15), amount, "SAR", new DateOnly(2026, 2, 14), null, "payload", $"command-{sourceReference}", "payload");

        internal FinanceMigrationApOpeningCommand ApCommand(string sourceReference, decimal amount) => new(CompanyId, SupplierId, sourceReference, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15), amount, "SAR", new DateOnly(2026, 2, 14), null, "payload", $"ap-command-{sourceReference}", "payload");

        internal FinanceMigrationCashBankOpeningCommand CashCommand(string sourceReference, decimal amount) => new(CompanyId, CashAccountId, sourceReference, new DateOnly(2026, 1, 15), amount, "SAR", "payload", $"cash-command-{sourceReference}", "payload");

        internal async Task EnableMonetaryPolicyAsync(int scale = 2, string mode = "AwayFromZero", Guid? reportingCurrencyId = null, bool revaluationEnabled = false, string? reportingCurrencyCode = null)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(Tenant.TenantId, new FinanceMonetaryPolicyCommand(CompanyId, reportingCurrencyId, scale, mode, revaluationEnabled, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"s10-policy-{Guid.NewGuid():N}", $"s10-policy-{Guid.NewGuid():N}"), "SAR", reportingCurrencyCode, 1));
            await db.SaveChangesAsync();
        }

        internal async Task AssertOneEffectAsync(string sourceReference, decimal amount)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            Assert.Equal(1, await db.OpenItems.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1" && item.Reference == sourceReference));
            Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1" && item.Status == FinanceJournalStatus.Posted));
            Assert.Equal(1, await db.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1"));
            Assert.Equal(amount, await db.OpenItems.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1" && item.Reference == sourceReference).Select(item => item.OriginalAmount).SingleAsync());
        }

        internal async Task AssertMixedEffectsAsync(string sourceReference, decimal amount)
        {
            await AssertOneEffectAsync(sourceReference, amount);
            await AssertOneApEffectAsync(sourceReference + "-AP", amount);
            await using var inventoryDb = new InventoryDbContext(InventoryOptions, Tenant);
            Assert.Equal(1, await inventoryDb.StockMovements.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            Assert.Equal(1, await inventoryDb.MovementValuationEvents.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId && item.Status == InventoryValuationEventStatus.Applied));
            Assert.Equal(1, await inventoryDb.FinanceValuationHandoffs.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            await using var financeDb = new FinanceDbContext(FinanceOptions, Tenant);
            Assert.Equal(1, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "inventory-valuation-finance.v1" && item.Status == FinanceJournalStatus.Posted));
        }

        internal async Task AssertOneApEffectAsync(string sourceReference, decimal amount)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            Assert.Equal(1, await db.OpenItems.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1" && item.Reference == sourceReference && item.SupplierId == SupplierId));
            Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1" && item.Status == FinanceJournalStatus.Posted));
            Assert.Equal(1, await db.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1"));
            Assert.Equal(amount, await db.OpenItems.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1" && item.Reference == sourceReference).Select(item => item.OriginalAmount).SingleAsync());
        }

        internal async Task RemoveApEvidenceAsync(string missing)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            if (missing == "source")
                db.SourceEffects.RemoveRange(await db.SourceEffects.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1").ToListAsync());
            else if (missing == "journal")
                db.Journals.RemoveRange(await db.Journals.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1").ToListAsync());
            else
                db.OpenItems.RemoveRange(await db.OpenItems.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1").ToListAsync());
            await db.SaveChangesAsync();
        }

        internal async Task AssertIncompleteApEvidenceAsync(string missing)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            var sourceEffects = await db.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1");
            var journals = await db.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1");
            var openItems = await db.OpenItems.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1");
            Assert.Equal(missing == "source" ? 0 : 1, sourceEffects);
            Assert.Equal(missing == "journal" ? 0 : 1, journals);
            Assert.Equal(missing == "open-item" ? 0 : 1, openItems);
            Assert.InRange(sourceEffects, 0, 1);
            Assert.InRange(journals, 0, 1);
            Assert.InRange(openItems, 0, 1);
        }

        internal async Task AssertNoMixedEffectsAsync(string sourceReference)
        {
            await using var inventoryDb = new InventoryDbContext(InventoryOptions, Tenant);
            Assert.Equal(0, await inventoryDb.StockMovements.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            Assert.Equal(0, await inventoryDb.MovementValuationEvents.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            Assert.Equal(0, await inventoryDb.FinanceValuationHandoffs.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            await using var financeDb = new FinanceDbContext(FinanceOptions, Tenant);
            Assert.Equal(0, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && (item.SourceContract == "inventory-valuation-finance.v1" || item.SourceContract == "migration-ar-opening.v1")));
            Assert.Equal(0, await financeDb.OpenItems.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1" && item.Reference == sourceReference));
            Assert.Equal(0, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1"));
            Assert.Equal(0, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1"));
            Assert.Equal(0, await financeDb.OpenItems.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1"));
            Assert.Equal(0, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ap-opening.v1"));
            Assert.Equal(0, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1"));
            Assert.Equal(0, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1"));
            Assert.Equal(0, await financeDb.SettlementDocuments.CountAsync(item => item.CompanyId == CompanyId));
        }

        internal async Task AssertNoStockMovementAsync()
        {
            await using var inventoryDb = new InventoryDbContext(InventoryOptions, Tenant);
            Assert.Equal(0, await inventoryDb.StockMovements.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
        }

        internal async Task DisableArAccountAsync()
        {
            var accounts = await ((FinancePersistence)Finance).ListAccountsAsync(FinanceContextFor("tenant.finance.account.manage"), CompanyId);
            var ar = accounts.Single(item => item.Id == ArAccountId);
            var disabled = await ((FinancePersistence)Finance).SetAccountLifecycleAsync(FinanceContextFor("tenant.finance.account.manage"), ar.Id, CompanyId, FinanceAccountLifecycle.Inactive, ar.Version, "s6-disable-ar", "s6-disable-ar");
            Assert.True(disabled.Succeeded, disabled.Code);
        }

        internal async Task DisableApAccountAsync()
        {
            var accounts = await ((FinancePersistence)Finance).ListAccountsAsync(FinanceContextFor("tenant.finance.account.manage"), CompanyId);
            var ap = accounts.Single(item => item.Id == ApAccountId);
            var disabled = await ((FinancePersistence)Finance).SetAccountLifecycleAsync(FinanceContextFor("tenant.finance.account.manage"), ap.Id, CompanyId, FinanceAccountLifecycle.Inactive, ap.Version, "s7-disable-ap", "s7-disable-ap");
            Assert.True(disabled.Succeeded, disabled.Code);
        }

        internal async Task DisableCashAccountAsync()
        {
            var cash = (await Settlement.ListCashAccountsAsync(FinanceContextFor("tenant.finance.settlement.manage"), CompanyId)).Single(item => item.Id == CashAccountId);
            var disabled = await Settlement.SetCashAccountLifecycleAsync(FinanceContextFor("tenant.finance.settlement.manage"), cash.Id, CompanyId, FinancePaymentMethodLifecycle.Inactive, cash.Version, "s8-disable-cash", "s8-disable-cash");
            Assert.True(disabled.Succeeded, disabled.Code);
        }

        internal async Task AssertOneCashEffectAsync(string sourceReference, decimal amount)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1" && item.Status == FinanceJournalStatus.Posted && item.Description == $"Cash/Bank opening {sourceReference}"));
            Assert.Equal(1, await db.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1"));
            var journal = await db.Journals.Include(item => item.Lines).SingleAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1" && item.Description == $"Cash/Bank opening {sourceReference}");
            Assert.Equal(amount, journal.Lines.Single(item => item.LineNumber == 1).FunctionalDebit);
            Assert.Equal(amount, journal.Lines.Single(item => item.LineNumber == 2).FunctionalCredit);
        }

        internal async Task<PreparedRun> PrepareMixedAsync(string sourceReference, decimal amount, bool includeCash = false, bool includeGl = false, bool derivedOffsets = false, bool invalidGl = false, decimal? cashAmount = null, string cashCurrencyCode = "SAR", Guid? cashAccountIdOverride = null, decimal? arAmount = null, string arCurrencyCode = "SAR", IReadOnlyList<(string SourceReference, decimal Amount, string CurrencyCode)>? additionalArOpenings = null, string glCurrencyCode = "SAR", decimal? apAmount = null, string apCurrencyCode = "SAR")
        {
            var objectId = Guid.NewGuid();
            var run = MigrationRun.Create(Tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(objectId, Tenant.TenantId, null, null, null, sourceHash, 1, 1);
            var intakeKey = new MigrationIdempotencyKey($"s6-mixed-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(Tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("s6-mixed-intake-fingerprint"), MigrationIntakeFingerprint.Version, source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(Tenant, new MigrationEvidenceReference(intake.Value!.Run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
            var persisted = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var inventoryPayload = new MigrationInventoryOpeningPayload(CompanyId, BranchId, WarehouseId, ProductId, UnitId, 2m, amount / 2m, "SAR", new DateOnly(2026, 1, 15), TrackingIdentity: "S6-MIXED-LOT", SourceLineReference: "S6-MIXED-LINE");
            var inventoryJson = JsonSerializer.Serialize(inventoryPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var arJson = JsonSerializer.Serialize(new { companyId = CompanyId, customerId = CustomerId, sourceReference, documentDate = new DateOnly(2026, 1, 10), openingDate = new DateOnly(2026, 1, 15), amount = arAmount ?? amount, currencyCode = arCurrencyCode, dueDate = new DateOnly(2026, 2, 14) });
            var staged = new List<MigrationStagedRecord>
            {
                new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 1, "S6-MIXED-INVENTORY", MigrationCanonicalRecordType.InventoryOpening, inventoryJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inventoryJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow),
                new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 2, sourceReference, MigrationCanonicalRecordType.ArOpening, arJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(arJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow)
            };
            foreach (var opening in additionalArOpenings ?? [])
            {
                var additionalJson = JsonSerializer.Serialize(new { companyId = CompanyId, customerId = CustomerId, sourceReference = opening.SourceReference, documentDate = new DateOnly(2026, 1, 10), openingDate = new DateOnly(2026, 1, 15), amount = opening.Amount, currencyCode = opening.CurrencyCode, dueDate = new DateOnly(2026, 2, 14) });
                staged.Add(new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, staged.Count + 1, opening.SourceReference, MigrationCanonicalRecordType.ArOpening, additionalJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(additionalJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow));
            }
            var apJson = JsonSerializer.Serialize(new { companyId = CompanyId, supplierId = SupplierId, sourceReference = sourceReference + "-AP", documentDate = new DateOnly(2026, 1, 10), openingDate = new DateOnly(2026, 1, 15), amount = apAmount ?? amount, currencyCode = apCurrencyCode, dueDate = new DateOnly(2026, 2, 14) });
            staged.Add(new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, staged.Count + 1, sourceReference + "-AP", MigrationCanonicalRecordType.ApOpening, apJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow));
            if (includeCash)
            {
                var cashJson = JsonSerializer.Serialize(new { companyId = CompanyId, cashAccountId = cashAccountIdOverride ?? CashAccountId, sourceReference = sourceReference + "-CASH", amount = cashAmount ?? amount, currencyCode = cashCurrencyCode, openingDate = new DateOnly(2026, 1, 15) });
                staged.Add(new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, staged.Count + 1, sourceReference + "-CASH", MigrationCanonicalRecordType.CashBankOpening, cashJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cashJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow));
            }
            if (includeGl)
            {
                var sequence = staged.Count + 1;
                foreach (var line in GlTargetLines(amount, derivedOffsets, invalidGl))
                {
                    var glJson = JsonSerializer.Serialize(new MigrationGlOpeningPayload(CompanyId, line.AccountId, line.Debit, line.Credit, glCurrencyCode, new DateOnly(2026, 1, 15), line.SourceLineReference), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    staged.Add(new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, sequence++, line.SourceLineReference, MigrationCanonicalRecordType.GlOpening, glJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(glJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow));
                }
            }
            Assert.True((await Migration.StagePackageAsync(Tenant, new StageMigrationPackageCommand(persisted.RunId, objectId, sourceHash, packageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, staged))).Succeeded);
            var current = await TransitionAsync(persisted, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, $"s6-mixed-validation-{Guid.NewGuid():N}", "s6-mixed-validation-fingerprint");
            var validationRows = staged.Select(item => new MigrationValidationRecordResult(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, [])).ToArray();
            var validation = new MigrationValidationSummary(Guid.NewGuid(), Tenant.TenantId, current.RunId, validationAttempt.AttemptId, packageHash, sourceHash, staged.Count, staged.Count, 0, 0, new Dictionary<string, int>(), validationRows, DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, validationAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "validation_completed", validationAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Validated);
            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, $"s6-mixed-dry-{Guid.NewGuid():N}", "s6-mixed-dry-fingerprint");
            var previewRows = staged.Select(item => new MigrationPreviewRow(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, "provider-realistic mixed test")).ToArray();
            var dryRun = new MigrationDryRunPreview(Guid.NewGuid(), Tenant.TenantId, current.RunId, dryAttempt.AttemptId, validationAttempt.AttemptId, packageHash, sourceHash, staged.Count, staged.Count, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, previewRows, DateTime.UtcNow);
            Assert.True((await Migration.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, dryAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "dry_run_completed", dryAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, staged);
        }

        internal async Task AssertOwnerEffectsReadyBeforeGlAsync()
        {
            await using var financeDb = new FinanceDbContext(FinanceOptions, Tenant);
            foreach (var contract in new[] { "inventory-valuation-finance.v1", "migration-ar-opening.v1", "migration-ap-opening.v1", "migration-cash-bank-opening.v1" })
            {
                Assert.Equal(1, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == contract && item.Status == FinanceJournalStatus.Posted));
                Assert.Equal(1, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == contract));
            }
            Assert.Equal(0, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-gl-opening.v1"));
            Assert.Equal(0, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-gl-opening.v1"));
            await using var inventoryDb = new InventoryDbContext(InventoryOptions, Tenant);
            Assert.Equal(1, await inventoryDb.StockMovements.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            Assert.Equal(1, await inventoryDb.MovementValuationEvents.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId && item.Status == InventoryValuationEventStatus.Applied));
            Assert.Equal(1, await inventoryDb.FinanceValuationHandoffs.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
        }

        internal async Task AssertNoAllFiveEffectsAsync()
        {
            await using var financeDb = new FinanceDbContext(FinanceOptions, Tenant);
            foreach (var contract in new[] { "inventory-valuation-finance.v1", "migration-ar-opening.v1", "migration-ap-opening.v1", "migration-cash-bank-opening.v1", "migration-gl-opening.v1" })
            {
                Assert.Equal(0, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == contract));
                Assert.Equal(0, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == contract));
            }
            await using var inventoryDb = new InventoryDbContext(InventoryOptions, Tenant);
            Assert.Equal(0, await inventoryDb.StockMovements.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            Assert.Equal(0, await inventoryDb.MovementValuationEvents.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            Assert.Equal(0, await inventoryDb.FinanceValuationHandoffs.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
        }

        internal async Task<int> CountFinanceRowsAsync(string contract, bool journals)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            return journals
                ? await db.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == contract)
                : await db.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == contract);
        }

        internal async Task MutateArJournalAmountAsync(decimal amount)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            var journalId = await db.Journals.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1").Select(item => item.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [finance].[JournalLines] SET [FunctionalDebit] = {amount}, [Debit] = {amount} WHERE [TenantId] = {Tenant.TenantId.Value} AND [JournalId] = {journalId} AND [LineNumber] = 1");
        }

        internal async Task MutateArJournalRuleAsync()
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            var journalId = await db.Journals.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1").Select(item => item.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [finance].[Journals] SET [PostingRuleId] = {Guid.NewGuid()}, [PostingRuleVersionNumber] = {99} WHERE [TenantId] = {Tenant.TenantId.Value} AND [Id] = {journalId}");
        }

        internal async Task MutateArJournalMappingAsync()
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            var journalId = await db.Journals.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1").Select(item => item.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [finance].[JournalLines] SET [AccountId] = {GlDebitAccountId} WHERE [TenantId] = {Tenant.TenantId.Value} AND [JournalId] = {journalId} AND [LineNumber] = 1");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [finance].[JournalLines] SET [AccountId] = {GlCreditAccountId} WHERE [TenantId] = {Tenant.TenantId.Value} AND [JournalId] = {journalId} AND [LineNumber] = 2");
        }

        internal async Task RemoveArSourceEffectAsync()
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            db.SourceEffects.RemoveRange(await db.SourceEffects.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
            await db.SaveChangesAsync();
        }

        internal async Task AssertLedgerMatchesTargetAsync(decimal amount, bool derivedOffsets)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            var actual = await db.Journals.Where(item => item.CompanyId == CompanyId && item.Status == FinanceJournalStatus.Posted).SelectMany(item => item.Lines).GroupBy(item => item.AccountId).ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.FunctionalDebit - item.FunctionalCredit));
            var target = GlTargetLines(amount, derivedOffsets, invalidGl: false).GroupBy(item => item.AccountId).ToDictionary(group => group.Key, group => group.Sum(item => item.Debit - item.Credit));
            var accounts = actual.Keys.Concat(target.Keys).Distinct().OrderBy(item => item).ToArray();
            Assert.All(accounts, account => Assert.Equal(target.GetValueOrDefault(account), actual.GetValueOrDefault(account)));
        }

        internal async Task<(int Journals, int SourceEffects, int OpenItems, int StockMovements, int ValuationEvents, int Handoffs, int Batches, int Effects, int Representations)> ReadEconomicCountsAsync(Guid runId, Guid attemptId)
        {
            var contracts = new[] { "inventory-valuation-finance.v1", "migration-ar-opening.v1", "migration-ap-opening.v1", "migration-cash-bank-opening.v1", "migration-gl-opening.v1" };
            var openItemContracts = new[] { "migration-ar-opening.v1", "migration-ap-opening.v1" };
            await using var financeDb = new FinanceDbContext(FinanceOptions, Tenant);
            await using var inventoryDb = new InventoryDbContext(InventoryOptions, Tenant);
            return (
                await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && contracts.Contains(item.SourceContract)),
                await financeDb.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && contracts.Contains(item.SourceContract)),
                await financeDb.OpenItems.CountAsync(item => item.CompanyId == CompanyId && openItemContracts.Contains(item.SourceContract)),
                await inventoryDb.StockMovements.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId),
                await inventoryDb.MovementValuationEvents.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId),
                await inventoryDb.FinanceValuationHandoffs.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId),
                (await Migration.ListBatchesAsync(Tenant, runId, attemptId)).Count,
                (await Migration.ListEffectsAsync(Tenant, runId, attemptId)).Count,
                (await Migration.ListRepresentationsAsync(Tenant, runId, attemptId)).Count);
        }

        private IReadOnlyList<GlTargetLine> GlTargetLines(decimal amount, bool derivedOffsets, bool invalidGl)
        {
            var lines = new List<GlTargetLine>
            {
                new(ArAccountId, amount, 0m, "MIG-GL-AR-CONTROL"),
                new(ApAccountId, 0m, amount, "MIG-GL-AP-CONTROL"),
                new(CashLinkedAccountId, amount, 0m, "MIG-GL-CASH-CONTROL"),
                new(InventoryControlAccountId, amount, 0m, "MIG-GL-INVENTORY-CONTROL")
            };
            if (!derivedOffsets)
            {
                lines.Add(new(ArOffsetAccountId, 0m, amount, "MIG-GL-AR-OFFSET"));
                lines.Add(new(ApOffsetAccountId, amount, 0m, "MIG-GL-AP-OFFSET"));
                lines.Add(new(InventoryOffsetAccountId, 0m, amount, "MIG-GL-INVENTORY-OFFSET"));
                lines.Add(new(CashOffsetAccountId, 0m, amount, "MIG-GL-CASH-OFFSET"));
            }
            lines.Add(new(GlDebitAccountId, 50m, 0m, "MIG-GL-SOURCE-DEBIT"));
            lines.Add(new(GlCreditAccountId, 0m, derivedOffsets ? 2m * amount + 50m : invalidGl ? 49m : 50m, "MIG-GL-SOURCE-CREDIT"));
            return lines;
        }


        private async Task<MigrationRunRecord> TransitionAsync(MigrationRunRecord run, MigrationRunStatus target)
        {
            var result = await foundation.TransitionRunAsync(Request, run.RunId, target, run.Version);
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        private async Task<MigrationAttemptRecord> StartAttemptAsync(MigrationRunRecord run, MigrationOperationKind operation, string key, string fingerprint)
        {
            var result = await foundation.StartAttemptAsync(Request, run.RunId, operation, key, fingerprint);
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private sealed class StaticInventoryProductProvider(InventoryProductReference product) : IInventoryProductProvider
    {
        public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryProductReference?>(product.ProductId == productId && product.TenantId == context.TenantId.Value ? product : null);
    }

    internal sealed class TestCurrencyPaymentTermPersistence(MasterDataCurrencyRecord currency, Guid paymentTermId) : IMasterDataCurrencyPaymentTermPersistence
    {
        private readonly UnavailableMasterDataCurrencyPaymentTermPersistence fallback = new();
        private readonly List<MasterDataCurrencyRecord> currencies = [currency];
        private readonly MasterDataPaymentTermRecord paymentTerm = new(
            paymentTermId,
            currency.TenantId,
            "NET35",
            new LocalizedName("Net 35"),
            MasterDataLifecycleState.Active,
            1,
            [new MasterDataPaymentTermVersionRecord(Guid.NewGuid(), 1, new DateOnly(2026, 1, 1), null, PaymentTermBaseDateRule.DocumentDate, PaymentTermScheduleMode.SingleDueDate, new MasterDataPaymentTermOffset(35, 0), [], MasterDataEarlySettlementDiscount.Disabled(), "NET35", new LocalizedName("Net 35"))],
            [1]);

        internal Guid PaymentTermId => paymentTerm.Id;
        internal MasterDataLifecycleState LifecycleState { get; set; } = MasterDataLifecycleState.Active;
        internal bool EffectiveVersionAvailable { get; set; } = true;
        internal Guid AddCurrency(string code, MasterDataLifecycleState state = MasterDataLifecycleState.Active)
        {
            var additional = new MasterDataCurrencyRecord(Guid.NewGuid(), currency.TenantId, code, new LocalizedName(code), state, 1, [1]);
            currencies.Add(additional);
            return additional.Id;
        }

        public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataCurrencyRecord>>(tenantContext.TenantId == currency.TenantId ? currencies : []);
        public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) => Task.FromResult(tenantContext.TenantId == currency.TenantId ? currencies.FirstOrDefault(item => item.Id == currencyId) : null);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateCurrencyAsync(tenantContext, currencyId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditCurrencyAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetCurrencyLifecycleAsync(tenantContext, currencyId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataPaymentTermRecord>>([paymentTerm]);
        public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid requestedPaymentTermId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataPaymentTermRecord?>(requestedPaymentTermId == paymentTerm.Id && tenantContext.TenantId == paymentTerm.TenantId ? paymentTerm with { LifecycleState = LifecycleState, Versions = EffectiveVersionAvailable ? paymentTerm.Versions : [] } : null);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreatePaymentTermAsync(tenantContext, paymentTermId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditPaymentTermAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetPaymentTermLifecycleAsync(tenantContext, paymentTermId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, resourceKind, resourceId, cancellationToken);
    }

    internal sealed class FaultingFinancePersistence(IFinanceSettlementPersistence inner) : IFinanceSettlementPersistence
    {
        internal Func<FinanceArOpeningPreflightResult, FinanceArOpeningPreflightResult>? TransformArPreflight { get; set; }
        internal Func<FinanceArOpeningPreflightResult, Task>? AfterArPreflight { get; set; }
        internal Func<FinanceMigrationArOpeningCommand, Task>? AfterArCreate { get; set; }
        internal bool ThrowAfterCreate { get; set; }
        internal bool HideReadback { get; set; }
        internal int CreateCalls { get; private set; }
        internal bool ThrowAfterApCreate { get; set; }
        internal bool HideApReadback { get; set; }
        internal Func<FinanceMigrationApOpeningCommand, Task>? AfterApCreate { get; set; }
        internal int ApCreateCalls { get; private set; }
        internal bool ThrowAfterCashCreate { get; set; }
        internal bool HideCashReadback { get; set; }
        internal Func<FinanceMigrationCashBankOpeningCommand, Task>? AfterCashCreate { get; set; }
        internal int CashCreateCalls { get; private set; }
        internal int CashReadCalls { get; private set; }
        internal Func<FinanceMigrationGlOpeningCommand, Task>? BeforeGlCreate { get; set; }
        internal bool ThrowAfterGlCreate { get; set; }
        internal bool HideGlReadback { get; set; }
        internal int GlCreateCalls { get; private set; }
        internal int GlReadCalls { get; private set; }
        internal Func<Task>? BeforeSecondArRead { get; set; }
        internal int ArReadCalls { get; private set; }
        public Task<IReadOnlyList<FinancePaymentMethodRecord>> ListPaymentMethodsAsync(FinanceRequestContext c, Guid id, CancellationToken t = default) => inner.ListPaymentMethodsAsync(c, id, t);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> CreatePaymentMethodAsync(FinanceRequestContext c, FinancePaymentMethodCommand x, CancellationToken t = default) => inner.CreatePaymentMethodAsync(c, x, t);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> EditPaymentMethodAsync(FinanceRequestContext c, FinancePaymentMethodCommand x, CancellationToken t = default) => inner.EditPaymentMethodAsync(c, x, t);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> SetPaymentMethodLifecycleAsync(FinanceRequestContext c, Guid id, Guid company, FinancePaymentMethodLifecycle l, byte[] v, string k, string f, CancellationToken t = default) => inner.SetPaymentMethodLifecycleAsync(c, id, company, l, v, k, f, t);
        public Task<IReadOnlyList<FinanceCashAccountRecord>> ListCashAccountsAsync(FinanceRequestContext c, Guid id, CancellationToken t = default) => inner.ListCashAccountsAsync(c, id, t);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> CreateCashAccountAsync(FinanceRequestContext c, FinanceCashAccountCommand x, CancellationToken t = default) => inner.CreateCashAccountAsync(c, x, t);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> EditCashAccountAsync(FinanceRequestContext c, FinanceCashAccountCommand x, byte[] v, CancellationToken t = default) => inner.EditCashAccountAsync(c, x, v, t);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> SetCashAccountLifecycleAsync(FinanceRequestContext c, Guid id, Guid company, FinancePaymentMethodLifecycle l, byte[] v, string k, string f, CancellationToken t = default) => inner.SetCashAccountLifecycleAsync(c, id, company, l, v, k, f, t);
        public Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext c, string r, Guid id, CancellationToken t = default) => inner.ResolveCompanyIdAsync(c, r, id, t);
        public Task<IReadOnlyList<FinanceOpenItemRecord>> ListOpenItemsAsync(FinanceRequestContext c, FinanceOpenItemKind k, Guid id, CancellationToken t = default) => inner.ListOpenItemsAsync(c, k, id, t);
        public Task<FinanceOpenItemRecord?> GetOpenItemAsync(FinanceRequestContext c, Guid id, FinanceOpenItemKind? k = null, CancellationToken t = default) => inner.GetOpenItemAsync(c, id, k, t);
        public Task<IReadOnlyList<FinanceApSourceReadyRecord>> ListApSourceReadyAsync(FinanceRequestContext c, Guid? id = null, CancellationToken t = default) => inner.ListApSourceReadyAsync(c, id, t);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> RecognizeSupplierInvoiceAsync(FinanceRequestContext c, FinanceSupplierInvoiceRecognitionCommand x, CancellationToken t = default) => inner.RecognizeSupplierInvoiceAsync(c, x, t);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateManualReceivableAsync(FinanceRequestContext c, FinanceManualReceivableCommand x, CancellationToken t = default) => inner.CreateManualReceivableAsync(c, x, t);
        public async Task<FinanceArOpeningPreflightResult> PreflightMigrationArOpeningAsync(FinanceRequestContext c, FinanceMigrationArOpeningCommand x, CancellationToken t = default)
        {
            var result = await inner.PreflightMigrationArOpeningAsync(c, x, t);
            if (TransformArPreflight is not null) result = TransformArPreflight(result);
            if (AfterArPreflight is not null) await AfterArPreflight(result);
            return result;
        }
        public Task<FinanceApOpeningPreflightResult> PreflightMigrationApOpeningAsync(FinanceRequestContext c, FinanceMigrationApOpeningCommand x, CancellationToken t = default) => inner.PreflightMigrationApOpeningAsync(c, x, t);
        public Task<FinanceCashBankOpeningPreflightResult> PreflightMigrationCashBankOpeningAsync(FinanceRequestContext c, FinanceMigrationCashBankOpeningCommand x, CancellationToken t = default) => inner.PreflightMigrationCashBankOpeningAsync(c, x, t);
        public async Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateMigrationArOpeningAsync(FinanceRequestContext c, FinanceMigrationArOpeningCommand x, CancellationToken t = default) { CreateCalls++; var result = await inner.CreateMigrationArOpeningAsync(c, x, t); if (AfterArCreate is not null) await AfterArCreate(x); if (ThrowAfterCreate) throw new InvalidOperationException("test lost response after commit"); return result; }
        public async Task<FinanceMigrationArOpeningEvidence?> ReadMigrationArOpeningAsync(FinanceRequestContext c, FinanceMigrationArOpeningCommand x, CancellationToken t = default)
        {
            ArReadCalls++;
            if (ArReadCalls == 3 && BeforeSecondArRead is not null) await BeforeSecondArRead();
            return HideReadback ? null : await inner.ReadMigrationArOpeningAsync(c, x, t);
        }
        public async Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateMigrationApOpeningAsync(FinanceRequestContext c, FinanceMigrationApOpeningCommand x, CancellationToken t = default) { ApCreateCalls++; var result = await inner.CreateMigrationApOpeningAsync(c, x, t); if (AfterApCreate is not null) await AfterApCreate(x); if (ThrowAfterApCreate) throw new InvalidOperationException("test AP lost response after commit"); return result; }
        public Task<FinanceMigrationApOpeningEvidence?> ReadMigrationApOpeningAsync(FinanceRequestContext c, FinanceMigrationApOpeningCommand x, CancellationToken t = default) => HideApReadback ? Task.FromResult<FinanceMigrationApOpeningEvidence?>(null) : inner.ReadMigrationApOpeningAsync(c, x, t);
        public async Task<FinanceOperationResult<FinanceJournalRecord>> CreateMigrationCashBankOpeningAsync(FinanceRequestContext c, FinanceMigrationCashBankOpeningCommand x, CancellationToken t = default) { CashCreateCalls++; var result = await inner.CreateMigrationCashBankOpeningAsync(c, x, t); if (AfterCashCreate is not null) await AfterCashCreate(x); if (ThrowAfterCashCreate) throw new InvalidOperationException("test cash lost response after commit"); return result; }
        public Task<FinanceMigrationCashBankOpeningEvidence?> ReadMigrationCashBankOpeningAsync(FinanceRequestContext c, FinanceMigrationCashBankOpeningCommand x, CancellationToken t = default) { CashReadCalls++; return HideCashReadback ? Task.FromResult<FinanceMigrationCashBankOpeningEvidence?>(null) : inner.ReadMigrationCashBankOpeningAsync(c, x, t); }
        public Task<FinanceGlOpeningPreflightResult> PreflightMigrationGlOpeningAsync(FinanceRequestContext c, FinanceMigrationGlOpeningCommand x, CancellationToken t = default) => inner.PreflightMigrationGlOpeningAsync(c, x, t);
        public async Task<FinanceOperationResult<FinanceMigrationGlOpeningEvidence>> CreateMigrationGlOpeningAsync(FinanceRequestContext c, FinanceMigrationGlOpeningCommand x, CancellationToken t = default)
        {
            GlCreateCalls++;
            if (BeforeGlCreate is not null) await BeforeGlCreate(x);
            var result = await inner.CreateMigrationGlOpeningAsync(c, x, t);
            if (ThrowAfterGlCreate) throw new InvalidOperationException("test GL lost response after commit");
            return result;
        }
        public async Task<FinanceMigrationGlOpeningEvidence?> ReadMigrationGlOpeningAsync(FinanceRequestContext c, FinanceMigrationGlOpeningCommand x, CancellationToken t = default)
        {
            GlReadCalls++;
            return HideGlReadback ? null : await inner.ReadMigrationGlOpeningAsync(c, x, t);
        }
        public Task<FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>> EvaluateSalesInvoiceAsync(FinanceRequestContext c, FinanceSalesInvoiceCommand x, CancellationToken t = default) => inner.EvaluateSalesInvoiceAsync(c, x, t);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateSalesInvoiceAsync(FinanceRequestContext c, FinanceSalesInvoiceCommand x, CancellationToken t = default) => inner.CreateSalesInvoiceAsync(c, x, t);
        public Task<IReadOnlyList<FinanceSettlementDocumentRecord>> ListSettlementDocumentsAsync(FinanceRequestContext c, FinanceSettlementQuery q, CancellationToken t = default) => inner.ListSettlementDocumentsAsync(c, q, t);
        public Task<FinanceSettlementDocumentRecord?> GetSettlementDocumentAsync(FinanceRequestContext c, Guid id, FinancePaymentMethodDirection? d = null, CancellationToken t = default) => inner.GetSettlementDocumentAsync(c, id, d, t);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> CreateSettlementDocumentAsync(FinanceRequestContext c, FinanceSettlementDocumentCommand x, CancellationToken t = default) => inner.CreateSettlementDocumentAsync(c, x, t);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> EditSettlementDocumentAsync(FinanceRequestContext c, FinanceSettlementDocumentCommand x, byte[] v, CancellationToken t = default) => inner.EditSettlementDocumentAsync(c, x, v, t);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> TransitionSettlementDocumentAsync(FinanceRequestContext c, FinanceSettlementActionCommand x, FinanceSettlementDocumentStatus s, CancellationToken t = default) => inner.TransitionSettlementDocumentAsync(c, x, s, t);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> PostSettlementDocumentAsync(FinanceRequestContext c, FinanceSettlementActionCommand x, CancellationToken t = default) => inner.PostSettlementDocumentAsync(c, x, t);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> ReverseSettlementDocumentAsync(FinanceRequestContext c, FinanceSettlementReversalCommand x, CancellationToken t = default) => inner.ReverseSettlementDocumentAsync(c, x, t);
        public Task<IReadOnlyList<FinanceAllocationRecord>> ListAllocationsAsync(FinanceRequestContext c, Guid id, CancellationToken t = default) => inner.ListAllocationsAsync(c, id, t);
        public Task<FinanceOperationResult<FinanceAllocationRecord>> CreateAllocationAsync(FinanceRequestContext c, FinanceAllocationCommand x, CancellationToken t = default) => inner.CreateAllocationAsync(c, x, t);
        public Task<FinanceOperationResult<FinanceAllocationRecord>> ReverseAllocationAsync(FinanceRequestContext c, FinanceAllocationReversalCommand x, CancellationToken t = default) => inner.ReverseAllocationAsync(c, x, t);
        public Task<IReadOnlyList<FinanceAgingRecord>> GetAgingAsync(FinanceRequestContext c, FinanceAgingQuery q, CancellationToken t = default) => inner.GetAgingAsync(c, q, t);
        public Task<FinanceCustomerExposureRecord?> GetExposureAsync(FinanceRequestContext c, FinanceExposureQuery q, CancellationToken t = default) => inner.GetExposureAsync(c, q, t);
        public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext c, Guid id, CancellationToken t = default) => inner.GetReconciliationAsync(c, id, t);
        public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext c, Guid id, DateOnly date, CancellationToken t = default) => inner.GetReconciliationAsync(c, id, date, t);
    }

    private sealed class ActiveCustomerReader(Guid customerId) : IBusinessCustomerReferenceReader
    {
        public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid requestedId, CancellationToken cancellationToken = default) => Task.FromResult<BusinessCustomerReference?>(requestedId == customerId ? new BusinessCustomerReference(customerId, tenantContext.TenantId, "S6-CUSTOMER", MasterDataLifecycleState.Active) : null);
    }

    internal sealed class MutableExchangeRates(TenantId tenantId) : IMasterDataExchangeRatePersistence
    {
        private readonly List<MasterDataExchangeRateRecord> additionalRates = [];
        private readonly TenantId ownerTenant = tenantId;
        internal Guid RateId { get; } = Guid.NewGuid();
        internal Guid VersionId { get; } = Guid.NewGuid();
        internal decimal Rate { get; set; } = 3.75m;
        internal bool Available { get; set; } = true;
        internal bool BaseAvailable { get; set; } = true;
        internal TenantId BaseTenantId { get; set; } = tenantId;
        internal MasterDataLifecycleState BaseState { get; set; } = MasterDataLifecycleState.Active;
        internal DateOnly BaseEffectiveFrom { get; set; } = new(2026, 1, 1);
        internal DateOnly? BaseEffectiveTo { get; set; }

        internal (Guid RateId, Guid VersionId) AddRate(string sourceCurrency, string targetCurrency, decimal rate, TenantId? ownerTenant = null, MasterDataLifecycleState state = MasterDataLifecycleState.Active, DateOnly? effectiveFrom = null, DateOnly? effectiveTo = null)
        {
            var rateId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var tenant = ownerTenant ?? this.ownerTenant;
            var from = effectiveFrom ?? new DateOnly(2026, 1, 1);
            additionalRates.Add(new(rateId, tenant, Guid.NewGuid(), Guid.NewGuid(), sourceCurrency, targetCurrency, state, 1,
                [new(versionId, 1, from, effectiveTo, rate, 6, ExchangeRateProvenance.Configured, "MESP-141 S10 provider scenario", sourceCurrency, targetCurrency)], [1]));
            return (rateId, versionId);
        }

        internal void UpdateRate(Guid rateId, decimal rate)
        {
            var index = additionalRates.FindIndex(item => item.Id == rateId);
            var record = additionalRates[index];
            additionalRates[index] = record with { Versions = [.. record.Versions.Select(version => version with { Rate = rate })] };
        }

        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default)
        {
            if (!Available || tenantContext.TenantId != ownerTenant)
                return Task.FromResult<IReadOnlyList<MasterDataExchangeRateRecord>>([]);
            var records = additionalRates.Where(item => item.TenantId == tenantContext.TenantId).ToList();
            if (BaseAvailable && BaseTenantId == tenantContext.TenantId)
                records.Add(new(RateId, BaseTenantId, Guid.NewGuid(), Guid.NewGuid(), "USD", "SAR", BaseState, 1, [new(VersionId, 1, BaseEffectiveFrom, BaseEffectiveTo, Rate, 6, ExchangeRateProvenance.Configured, "MESP-141 S10", "USD", "SAR")], [1]));
            return Task.FromResult<IReadOnlyList<MasterDataExchangeRateRecord>>(records);
        }

        public async Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) =>
            (await ListExchangeRatesAsync(tenantContext, cancellationToken)).SingleOrDefault(item => item.Id == exchangeRateId);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Task.FromException<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>(new InvalidOperationException("unused"));
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Task.FromException<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>(new InvalidOperationException("unused"));
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Task.FromException<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>(new InvalidOperationException("unused"));
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Task.FromException<MasterDataPersistenceResult<MasterDataAuditRecord>>(new InvalidOperationException("unused"));
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
    }

    internal sealed class MutableApprovalPolicy : IFinanceSourceApprovalPolicy
    {
        internal FinanceApprovalRequirement Requirement { get; set; } = FinanceApprovalRequirement.NotRequired;
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) => Requirement;
    }

    private sealed class MixedReferenceAuthority : IMigrationReferenceAuthority
    {
        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>([new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, "reference_active", "active")]);
        public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) => row.Payload switch
        {
            MigrationArOpeningPayload ar when ar.CompanyId is { } company && ar.CustomerId is { } customer && !string.IsNullOrWhiteSpace(ar.SourceReference) => MigrationBusinessIdentityResolution.Valid($"ar-opening:{company:D}:{customer:D}:{ar.SourceReference.Trim()}"),
            MigrationApOpeningPayload ap when ap.CompanyId is { } company && ap.SupplierId is { } supplier && !string.IsNullOrWhiteSpace(ap.SourceReference) => MigrationBusinessIdentityResolution.Valid($"ap-opening:{company:D}:{supplier:D}:{ap.SourceReference.Trim()}"),
            MigrationCashBankOpeningPayload cash when cash.CompanyId is { } company && cash.CashAccountId is { } account && !string.IsNullOrWhiteSpace(cash.SourceReference) => MigrationBusinessIdentityResolution.Valid($"cash-bank-opening:{company:D}:{account:D}:{cash.SourceReference.Trim()}"),
            _ => MigrationBusinessIdentityResolution.NotApplicable()
        };
    }

    internal sealed class ActiveSupplierReader(Guid supplierId) : ISupplierPersistence
    {
        internal MasterDataLifecycleState LifecycleState { get; set; } = MasterDataLifecycleState.Active;
        internal bool ForeignTenant { get; set; }
        public Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierRecord>>([Reference(tenantContext)]);
        public Task<SupplierRecord?> FindSupplierAsync(TenantContext tenantContext, Guid requestedId, CancellationToken cancellationToken = default) => Task.FromResult<SupplierRecord?>(requestedId == supplierId ? Reference(tenantContext) : null);
        public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(TenantContext tenantContext, Guid id, CreateSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(TenantContext tenantContext, EditSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(TenantContext tenantContext, Guid id, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
        private SupplierRecord Reference(TenantContext context) => new(supplierId, ForeignTenant ? new TenantId(Guid.NewGuid()) : context.TenantId, "S7-SUPPLIER", new LocalizedName("Supplier"), null, null, LifecycleState, [1], []);
        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("test persistence unavailable"));
    }

    private sealed class UnusedOwnerGateway : IOwnerExecutionGateway
    {
        public Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(FoundationRequestContext c, OwnerImportRequest r, CancellationToken t = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(FoundationRequestContext c, Guid id, CancellationToken t = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(FoundationRequestContext c, Guid id, byte[] v, CancellationToken t = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerEvidence?> ReadEvidenceAsync(FoundationRequestContext c, Guid id, CancellationToken t = default) => Task.FromResult<OwnerEvidence?>(null);
    }

    private sealed class TenantWideScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) => TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));
        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) => TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    internal sealed record PreparedRun(MigrationRunRecord Run, IReadOnlyList<MigrationStagedRecord> Staged);
    private sealed record GlTargetLine(Guid AccountId, decimal Debit, decimal Credit, string SourceLineReference);

    private static FoundationRequestContext FoundationContext(TenantContext tenant, string permission) => FoundationRequestContext.ForTenant(tenant.ActorId!.Value, Guid.NewGuid(), tenant, permission);
    private static FinanceAccountCommand Account(Guid companyId, string code, FinanceAccountType type) => new(companyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");
}
