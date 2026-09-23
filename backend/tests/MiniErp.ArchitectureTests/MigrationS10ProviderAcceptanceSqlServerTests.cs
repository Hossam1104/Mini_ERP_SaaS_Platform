using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Adapters;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationS10ProviderAcceptanceSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    [Fact]
    public async Task Sql_server_s10_p01_functional_currency_all_five_remains_independent_of_fx()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        fixture.ExchangeRates.Available = false;
        var prepared = await fixture.PrepareMixedAsync("S10-P01", 100m, includeCash: true, includeGl: true);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p01", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, result.Value!.FingerprintVersion);
        Assert.Equal("migration-economic-execution-v2", result.Value.FingerprintVersion);
        await fixture.AssertMixedEffectsAsync("S10-P01", 100m);
        await fixture.AssertOneCashEffectAsync("S10-P01-CASH", 100m);
        await fixture.AssertLedgerMatchesTargetAsync(100m, derivedOffsets: false);
        Assert.All(result.Value!.ArEconomicReconciliations!, item =>
        {
            Assert.Equal("SAR", item.CurrencyCode);
            Assert.Null(item.TransactionCurrencyCode);
            Assert.Null(item.TransactionAmount);
            Assert.Null(item.FunctionalCurrencyCode);
            Assert.Null(item.FunctionalAmount);
            Assert.Null(item.ExchangeRateId);
            Assert.Null(item.AppliedRate);
        });
        Assert.All(result.Value.ApEconomicReconciliations!, item => Assert.Null(item.ExchangeRateId));
        Assert.All(result.Value.CashBankEconomicReconciliations!, item => Assert.Null(item.ExchangeRateId));

        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        var evidence = await db.JournalMonetaryEvidence.AsNoTracking()
            .Where(item => item.CompanyId == fixture.CompanyId)
            .Select(item => item.MonetaryEvidenceJson)
            .ToArrayAsync();
        Assert.Empty(evidence);
    }

    [Fact]
    public async Task Sql_server_s10_p06_foreign_gl_is_rejected_before_any_owner_effect()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("S10-P06", 100m, includeCash: true, includeGl: true, glCurrencyCode: "USD");

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p06", prepared.Run.Version);

        Assert.False(result.Succeeded);
        Assert.Equal("migration_gl_opening_currency_not_functional", result.Code);
        await fixture.AssertNoAllFiveEffectsAsync();
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-gl-opening.v1").ToListAsync());
        Assert.Empty(await db.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-gl-opening.v1").ToListAsync());
    }

    [Theory]
    [InlineData("missing-rate", "migration_opening_exchange_rate_unavailable")]
    [InlineData("ambiguous-rate", "migration_opening_exchange_rate_unavailable")]
    [InlineData("inverse-only", "migration_opening_exchange_rate_unavailable")]
    [InlineData("wrong-direction", "migration_opening_exchange_rate_unavailable")]
    [InlineData("inactive-rate", "migration_opening_exchange_rate_unavailable")]
    [InlineData("unsupported-currency", "migration_opening_exchange_rate_unavailable")]
    [InlineData("not-effective", "migration_opening_exchange_rate_unavailable")]
    [InlineData("foreign-tenant-rate", "migration_opening_exchange_rate_unavailable")]
    [InlineData("wrong-tenant-evidence", "migration_opening_exchange_rate_drift")]
    public async Task Sql_server_s10_p08_rate_failures_stop_real_migration_before_owner_effect(string scenario, string expectedCode)
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var currency = scenario == "unsupported-currency" ? "XXX" : "USD";
        switch (scenario)
        {
            case "missing-rate":
                fixture.ExchangeRates.Available = false;
                break;
            case "ambiguous-rate":
                fixture.ExchangeRates.AddRate("USD", "SAR", 3.75m);
                break;
            case "inverse-only":
                fixture.ExchangeRates.BaseAvailable = false;
                fixture.ExchangeRates.AddRate("SAR", "USD", 0.266667m);
                break;
            case "wrong-direction":
                fixture.ExchangeRates.BaseAvailable = false;
                fixture.ExchangeRates.AddRate("USD", "EUR", 3.75m);
                break;
            case "inactive-rate":
                fixture.ExchangeRates.BaseAvailable = false;
                fixture.ExchangeRates.AddRate("USD", "SAR", 3.75m, state: MasterDataLifecycleState.Inactive);
                break;
            case "unsupported-currency":
                fixture.ExchangeRates.BaseAvailable = false;
                break;
            case "not-effective":
                fixture.ExchangeRates.BaseAvailable = false;
                fixture.ExchangeRates.AddRate("USD", "SAR", 3.75m, effectiveFrom: new DateOnly(2026, 1, 16));
                break;
            case "foreign-tenant-rate":
                fixture.ExchangeRates.BaseAvailable = false;
                fixture.ExchangeRates.AddRate("USD", "SAR", 3.75m, new MiniErp.App.BuildingBlocks.Tenancy.TenantId(Guid.NewGuid()));
                break;
        }
        var prepared = await fixture.PrepareAsync($"S10-P08-{scenario}", 100m, currency);
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement);
        if (scenario == "wrong-tenant-evidence")
            proxy.AfterArPreflight = _ =>
            {
                fixture.ExchangeRates.BaseTenantId = new MiniErp.App.BuildingBlocks.Tenancy.TenantId(Guid.NewGuid());
                return Task.CompletedTask;
            };

        var result = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"s10-p08-{scenario}", prepared.Run.Version);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.Code);
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Empty(await db.OpenItems.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await db.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p08_inactive_currency_from_masterdata_is_rejected_before_finance_preflight()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        await using var connection = await safety.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable);
        var inactiveUsd = Guid.NewGuid();
        await using (var masterData = new MasterDataDbContext(options, fixture.Tenant))
        {
            masterData.Currencies.Add(new MasterDataCurrencyEntity(inactiveUsd, fixture.Tenant.TenantId, "USD", new LocalizedName("US Dollar")));
            await masterData.SaveChangesAsync();
            var entity = await masterData.Currencies.SingleAsync(item => item.Id == inactiveUsd);
            entity.SetLifecycle(MasterDataLifecycleState.Inactive);
            await masterData.SaveChangesAsync();
        }

        var prepared = await fixture.PrepareAsync("S10-P08-INACTIVE-CURRENCY", 100m, "USD");
        var authority = new SqlCurrencyReferenceAuthority(new MasterDataCurrencyPaymentTermPersistence(options));
        var result = await fixture.NewExecution(referenceAuthority: authority).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p08-inactive-currency", prepared.Run.Version);

        Assert.False(result.Succeeded);
        Assert.Equal("migration_currency_inactive", result.Code);
        await using var financeDb = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Empty(await financeDb.OpenItems.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await financeDb.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await financeDb.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
    }

    [Theory]
    [InlineData("transaction-amount", "migration_opening_monetary_evidence_drift")]
    [InlineData("functional-amount", "migration_opening_monetary_evidence_drift")]
    [InlineData("rate-id", "migration_opening_exchange_rate_drift")]
    [InlineData("rate-version-id", "migration_opening_exchange_rate_drift")]
    [InlineData("rate-version-number", "migration_opening_exchange_rate_drift")]
    [InlineData("applied-rate", "migration_opening_exchange_rate_drift")]
    [InlineData("policy-id", "migration_opening_monetary_evidence_drift")]
    [InlineData("policy-version", "migration_opening_monetary_evidence_drift")]
    [InlineData("rounding-scale", "migration_opening_monetary_evidence_drift")]
    [InlineData("rounding-mode", "migration_opening_monetary_evidence_drift")]
    [InlineData("reporting-currency", "migration_opening_monetary_evidence_drift")]
    [InlineData("reporting-rate-id", "migration_opening_monetary_evidence_drift")]
    [InlineData("reporting-rate-version-id", "migration_opening_monetary_evidence_drift")]
    [InlineData("reporting-rate-version-number", "migration_opening_monetary_evidence_drift")]
    [InlineData("reporting-applied-rate", "migration_opening_monetary_evidence_drift")]
    public async Task Sql_server_s10_p11_typed_expectation_mismatch_matrix_fails_before_owner_effect(string dimension, string expectedCode)
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareAsync($"S10-P11-{dimension}", 100m, "USD");
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            TransformArPreflight = result => result with { Expectation = Mismatch(result.Expectation!, dimension) }
        };

        var result = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"s10-p11-{dimension}", prepared.Run.Version);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.Code);
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Empty(await db.OpenItems.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await db.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p09_rate_drift_during_real_migration_execution_fails_closed_and_new_prepare_pins_current_rate()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareAsync("S10-P09-DRIFT", 100m, "USD");
        var drift = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            AfterArPreflight = _ =>
            {
                fixture.ExchangeRates.Rate = 4m;
                return Task.CompletedTask;
            }
        };

        var failed = await fixture.NewExecution(drift).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p09-drift", prepared.Run.Version);

        Assert.False(failed.Succeeded);
        Assert.Equal("migration_opening_exchange_rate_drift", failed.Code);
        await using (var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant))
        {
            Assert.Empty(await db.OpenItems.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
            Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
            Assert.Empty(await db.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        }

        var failedAttempt = Assert.Single(await fixture.Migration.ListAttemptsAsync(fixture.Tenant, prepared.Run.RunId), item => item.Operation == MigrationOperationKind.Execution);
        var stored = await fixture.Migration.ListRepresentationsAsync(fixture.Tenant, prepared.Run.RunId, failedAttempt.AttemptId);
        var expectation = Assert.Single(stored, item => item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation);
        Assert.Equal(3.75m, expectation.AppliedRate);
        Assert.Equal(fixture.ExchangeRates.VersionId, expectation.ExchangeRateVersionId);

        var next = await fixture.PrepareAsync("S10-P09-NEXT", 100m, "USD");
        var succeeded = await fixture.NewExecution().ExecuteAsync(fixture.Request, next.Run.RunId, "s10-p09-next", next.Run.Version);
        Assert.True(succeeded.Succeeded, succeeded.Code);
        Assert.Equal(400m, Assert.Single(succeeded.Value!.ArEconomicReconciliations!).FunctionalAmount);
    }

    [Fact]
    public async Task Sql_server_s10_p10_rate_change_after_owner_commit_does_not_reinterpret_gl_control()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareMixedAsync("S10-P10", 375m, includeCash: true, includeGl: true, cashAmount: 375m, arAmount: 100m, arCurrencyCode: "USD");
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            BeforeGlCreate = _ =>
            {
                fixture.ExchangeRates.Rate = 4m;
                return Task.CompletedTask;
            }
        };

        var result = await fixture.NewMixedExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p10", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(375m, Assert.Single(result.Value!.ArEconomicReconciliations!).FunctionalAmount);
        Assert.Equal(375m, Assert.Single(result.Value.CashBankEconomicReconciliations!).FunctionalAmount);
        Assert.Contains(result.Value.GlEconomicReconciliations!.SelectMany(item => item.Lines!), line =>
            line.AccountId == fixture.ArAccountId && line.AccountingTreatment == "represented_by_ar" && line.EstablishedSignedAmount == 375m && line.ResidualDebit == 0m && line.ResidualCredit == 0m);
        Assert.Contains(result.Value.GlEconomicReconciliations!.SelectMany(item => item.Lines!), line =>
            line.AccountId == fixture.CashLinkedAccountId && line.AccountingTreatment == "represented_by_cash_bank" && line.EstablishedSignedAmount == 375m && line.ResidualDebit == 0m && line.ResidualCredit == 0m);
    }

    [Fact]
    public async Task Sql_server_s10_p12_exact_replay_after_rate_change_returns_historical_evidence_without_new_effects()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareAsync("S10-P12", 100m, "USD");
        var execution = fixture.NewExecution();
        var first = await execution.ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p12-exact", prepared.Run.Version);
        Assert.True(first.Succeeded, first.Code);
        var before = await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, first.Value!.AttemptId);

        fixture.ExchangeRates.Rate = 4m;
        var replay = await execution.ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p12-exact", prepared.Run.Version);

        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(first.Value.FingerprintVersion, replay.Value!.FingerprintVersion);
        Assert.Equal(first.Value.Fingerprint, replay.Value.Fingerprint);
        Assert.Equal(375m, Assert.Single(replay.Value.ArEconomicReconciliations!).FunctionalAmount);
        Assert.Equal(before, await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, first.Value.AttemptId));
    }

    [Fact]
    public async Task Sql_server_s10_p13_post_commit_lost_response_recovers_historical_rate_without_duplicate_effect()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareAsync("S10-P13", 100m, "USD");
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            AfterArCreate = _ =>
            {
                fixture.ExchangeRates.Rate = 4m;
                return Task.CompletedTask;
            },
            ThrowAfterCreate = true
        };

        var result = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p13-lost", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(375m, Assert.Single(result.Value!.ArEconomicReconciliations!).FunctionalAmount);
        Assert.Equal(1, proxy.CreateCalls);
        await fixture.AssertOneEffectAsync("S10-P13", 100m);
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-ar-opening.v1", journals: true));
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-ar-opening.v1", journals: false));
    }

    [Fact]
    public async Task Sql_server_s10_p14_concurrent_identical_runs_converge_to_one_owner_effect()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var first = await fixture.PrepareAsync("S10-P14-SAME", 100m, "USD");
        var second = await fixture.PrepareAsync("S10-P14-SAME", 100m, "USD");
        var bothAtPreflight = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            AfterArPreflight = async _ =>
            {
                if (Interlocked.Increment(ref arrivals) == 2)
                    bothAtPreflight.TrySetResult();
                await bothAtPreflight.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
        };

        var results = await Task.WhenAll(
            fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, first.Run.RunId, "s10-p14-a", first.Run.Version),
            fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, second.Run.RunId, "s10-p14-b", second.Run.Version));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Code));
        Assert.All(results, result => Assert.Equal(375m, Assert.Single(result.Value!.ArEconomicReconciliations!).FunctionalAmount));
        await fixture.AssertOneEffectAsync("S10-P14-SAME", 100m);
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-ar-opening.v1", journals: true));
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-ar-opening.v1", journals: false));
    }

    [Fact]
    public async Task Sql_server_s10_p15_usd_and_eur_openings_aggregate_to_one_functional_ar_control()
    {
        await using (var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety))
        {
            await fixture.EnableMonetaryPolicyAsync();
            var eur = fixture.ExchangeRates.AddRate("EUR", "SAR", 4m);
            var prepared = await fixture.PrepareMixedAsync("S10-P15-USD", 775m, includeCash: true, includeGl: true, arAmount: 100m, arCurrencyCode: "USD",
                additionalArOpenings: [("S10-P15-EUR", 100m, "EUR")]);

            var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p15-exact", prepared.Run.Version);

            Assert.True(result.Succeeded, result.Code);
            var usd = Assert.Single(result.Value!.ArEconomicReconciliations!, item => item.SourceReference == "S10-P15-USD");
            var euro = Assert.Single(result.Value.ArEconomicReconciliations!, item => item.SourceReference == "S10-P15-EUR");
            Assert.Equal(("USD", 100m, "SAR", 375m, fixture.ExchangeRates.RateId),
                (usd.TransactionCurrencyCode, usd.TransactionAmount, usd.FunctionalCurrencyCode, usd.FunctionalAmount, usd.ExchangeRateId));
            Assert.Equal(("EUR", 100m, "SAR", 400m, eur.RateId),
                (euro.TransactionCurrencyCode, euro.TransactionAmount, euro.FunctionalCurrencyCode, euro.FunctionalAmount, euro.ExchangeRateId));
            Assert.Contains(result.Value.GlEconomicReconciliations!.SelectMany(item => item.Lines!), line =>
                line.AccountId == fixture.ArAccountId && line.AccountingTreatment == "represented_by_ar"
                && line.EstablishedSignedAmount == 775m && line.ResidualDebit == 0m && line.ResidualCredit == 0m);
            await using (var financeDb = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant))
            {
                Assert.Equal(2, await financeDb.OpenItems.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1"));
                Assert.Equal(2, await financeDb.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1"));
                Assert.Equal(2, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1"));
            }
            await fixture.AssertOneApEffectAsync("S10-P15-USD-AP", 775m);
            await fixture.AssertOneCashEffectAsync("S10-P15-USD-CASH", 775m);
            await fixture.AssertLedgerMatchesTargetAsync(775m, derivedOffsets: false);
        }

        await using var mismatchFixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await mismatchFixture.EnableMonetaryPolicyAsync();
        mismatchFixture.ExchangeRates.AddRate("EUR", "SAR", 4m);
        var mismatch = await mismatchFixture.PrepareMixedAsync("S10-P15-MISMATCH", 776m, includeCash: true, includeGl: true,
            additionalArOpenings: [("S10-P15-MISMATCH-EUR", 100m, "EUR")]);
        var blocked = await mismatchFixture.NewMixedExecution().ExecuteAsync(mismatchFixture.Request, mismatch.Run.RunId, "s10-p15-mismatch", mismatch.Run.Version);

        Assert.False(blocked.Succeeded);
        await using var db = new FinanceDbContext(mismatchFixture.FinanceOptions, mismatchFixture.Tenant);
        Assert.Empty(await db.Journals.Where(item => item.CompanyId == mismatchFixture.CompanyId && item.SourceContract == "migration-gl-opening.v1").ToListAsync());
        Assert.Empty(await db.SourceEffects.Where(item => item.CompanyId == mismatchFixture.CompanyId && item.SourceContract == "migration-gl-opening.v1").ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p16_finance_rounding_modes_scale_eight_and_invalid_scale_are_provider_verified()
    {
        foreach (var (mode, expected, difference) in new[] { ("ToEven", 3.74m, -0.005m), ("AwayFromZero", 3.75m, 0.005m) })
        {
            await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
            await fixture.EnableMonetaryPolicyAsync(2, mode);
            fixture.ExchangeRates.Rate = 3.745m;
            var prepared = await fixture.PrepareAsync($"S10-P16-{mode}", 1m, "USD");
            var result = await fixture.NewExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, $"s10-p16-{mode}", prepared.Run.Version);

            Assert.True(result.Succeeded, result.Code);
            var evidence = Assert.Single(result.Value!.ArEconomicReconciliations!);
            Assert.Equal(expected, evidence.FunctionalAmount);
            Assert.Equal(difference, evidence.FunctionalRoundingDifference);
            Assert.Equal(mode, evidence.RoundingMode);
            Assert.Equal(2, evidence.RoundingScale);
            Assert.NotNull(evidence.MonetaryPolicyId);
            await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
            Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1"));
            Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract != "migration-ar-opening.v1").ToListAsync());
        }

        await using (var scaleEight = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety))
        {
            await scaleEight.EnableMonetaryPolicyAsync(8, "ToEven");
            var prepared = await scaleEight.PrepareAsync("S10-P16-SCALE-8", 1.00000001m, "USD");
            var result = await scaleEight.NewExecution().ExecuteAsync(scaleEight.Request, prepared.Run.RunId, "s10-p16-scale-8", prepared.Run.Version);
            Assert.True(result.Succeeded, result.Code);
            Assert.Equal(3.75000004m, Assert.Single(result.Value!.ArEconomicReconciliations!).FunctionalAmount);
            Assert.Equal(8, Assert.Single(result.Value.ArEconomicReconciliations!).RoundingScale);
        }

        await using var invalidScale = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await invalidScale.EnableMonetaryPolicyAsync(9);
        var invalid = await invalidScale.PrepareAsync("S10-P16-SCALE-9", 1m, "USD");
        var rejected = await invalidScale.NewExecution().ExecuteAsync(invalidScale.Request, invalid.Run.RunId, "s10-p16-scale-9", invalid.Run.Version);
        Assert.False(rejected.Succeeded);
        Assert.Equal("migration_opening_rounding_policy_invalid", rejected.Code);
        await using var invalidDb = new FinanceDbContext(invalidScale.FinanceOptions, invalidScale.Tenant);
        Assert.Empty(await invalidDb.OpenItems.Where(item => item.CompanyId == invalidScale.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await invalidDb.Journals.Where(item => item.CompanyId == invalidScale.CompanyId).ToListAsync());
        Assert.Empty(await invalidDb.SourceEffects.Where(item => item.CompanyId == invalidScale.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p17_reporting_currency_is_additive_and_pinned_only_when_configured()
    {
        await using (var absent = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety))
        {
            await absent.EnableMonetaryPolicyAsync();
            var prepared = await absent.PrepareAsync("S10-P17-NO-REPORTING", 100m, "USD");
            var result = await absent.NewExecution().ExecuteAsync(absent.Request, prepared.Run.RunId, "s10-p17-a", prepared.Run.Version);
            Assert.True(result.Succeeded, result.Code);
            var evidence = Assert.Single(result.Value!.ArEconomicReconciliations!);
            Assert.Null(evidence.ReportingCurrencyCode);
            Assert.Null(evidence.ReportingAmount);
            Assert.Equal("NotCaptured", evidence.ReportingEvidenceStatus);
        }

        await using (var functional = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety))
        {
            await functional.EnableMonetaryPolicyAsync(reportingCurrencyId: functional.CurrencyId, reportingCurrencyCode: "SAR");
            var prepared = await functional.PrepareAsync("S10-P17-FUNCTIONAL", 100m, "USD");
            var result = await functional.NewExecution().ExecuteAsync(functional.Request, prepared.Run.RunId, "s10-p17-b", prepared.Run.Version);
            Assert.True(result.Succeeded, result.Code);
            var evidence = Assert.Single(result.Value!.ArEconomicReconciliations!);
            Assert.Equal("SAR", evidence.ReportingCurrencyCode);
            Assert.Equal(375m, evidence.ReportingAmount);
            Assert.Null(evidence.ReportingExchangeRateId);
            Assert.Equal("Captured", evidence.ReportingEvidenceStatus);
        }

        await using (var foreign = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety))
        {
            var usd = foreign.PaymentTerms.AddCurrency("USD");
            var reportingRate = foreign.ExchangeRates.AddRate("SAR", "USD", 0.2667m);
            await foreign.EnableMonetaryPolicyAsync(reportingCurrencyId: usd, reportingCurrencyCode: "USD");
            var prepared = await foreign.PrepareAsync("S10-P17-FOREIGN", 100m, "USD");
            var result = await foreign.NewExecution().ExecuteAsync(foreign.Request, prepared.Run.RunId, "s10-p17-c", prepared.Run.Version);
            Assert.True(result.Succeeded, result.Code);
            var evidence = Assert.Single(result.Value!.ArEconomicReconciliations!);
            Assert.Equal("USD", evidence.ReportingCurrencyCode);
            Assert.Equal(100.01m, evidence.ReportingAmount);
            Assert.Equal(reportingRate.RateId, evidence.ReportingExchangeRateId);
            Assert.Equal(reportingRate.VersionId, evidence.ReportingExchangeRateVersionId);
            Assert.Equal(1, evidence.ReportingExchangeRateVersionNumber);
            Assert.Equal(0.2667m, evidence.ReportingAppliedRate);
            await using var db = new FinanceDbContext(foreign.FinanceOptions, foreign.Tenant);
            var json = await db.JournalMonetaryEvidence.Where(item => item.CompanyId == foreign.CompanyId).Select(item => item.MonetaryEvidenceJson).SingleAsync();
            var journalEvidence = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(json)!;
            Assert.Equal(reportingRate.RateId, journalEvidence.FunctionalToReportingRate!.ExchangeRateId);
            Assert.Equal(100.01m, journalEvidence.ReportingAmount);
        }

        await using (var missing = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety))
        {
            var usd = missing.PaymentTerms.AddCurrency("USD");
            await missing.EnableMonetaryPolicyAsync(reportingCurrencyId: usd, reportingCurrencyCode: "USD");
            var prepared = await missing.PrepareAsync("S10-P17-MISSING-RATE", 100m, "USD");
            var result = await missing.NewExecution().ExecuteAsync(missing.Request, prepared.Run.RunId, "s10-p17-d", prepared.Run.Version);
            Assert.False(result.Succeeded);
            Assert.Equal("migration_opening_reporting_rate_unavailable", result.Code);
            await using var db = new FinanceDbContext(missing.FinanceOptions, missing.Tenant);
            Assert.Empty(await db.OpenItems.Where(item => item.CompanyId == missing.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
            Assert.Empty(await db.Journals.Where(item => item.CompanyId == missing.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
            Assert.Empty(await db.SourceEffects.Where(item => item.CompanyId == missing.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        }

        await using var drift = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var reporting = drift.PaymentTerms.AddCurrency("USD");
        var pinned = drift.ExchangeRates.AddRate("SAR", "USD", 0.2667m);
        await drift.EnableMonetaryPolicyAsync(reportingCurrencyId: reporting, reportingCurrencyCode: "USD");
        var driftRun = await drift.PrepareAsync("S10-P17-RATE-DRIFT", 100m, "USD");
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(drift.Settlement)
        {
            AfterArPreflight = _ =>
            {
                drift.ExchangeRates.UpdateRate(pinned.RateId, 0.25m);
                return Task.CompletedTask;
            }
        };
        var drifted = await drift.NewExecution(proxy).ExecuteAsync(drift.Request, driftRun.Run.RunId, "s10-p17-e", driftRun.Run.Version);
        Assert.False(drifted.Succeeded);
        Assert.Equal("migration_opening_reporting_rate_drift", drifted.Code);
        var attempt = Assert.Single(await drift.Migration.ListAttemptsAsync(drift.Tenant, driftRun.Run.RunId), item => item.Operation == MigrationOperationKind.Execution);
        var expected = Assert.Single(await drift.Migration.ListRepresentationsAsync(drift.Tenant, driftRun.Run.RunId, attempt.AttemptId), item => item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation);
        Assert.Equal(pinned.RateId, expected.ReportingExchangeRateId);
        Assert.Equal(pinned.VersionId, expected.ReportingExchangeRateVersionId);
        Assert.Equal(0.2667m, expected.ReportingAppliedRate);
        await using var driftDb = new FinanceDbContext(drift.FinanceOptions, drift.Tenant);
        Assert.Empty(await driftDb.OpenItems.Where(item => item.CompanyId == drift.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p18_foreign_opening_without_policy_fails_before_owner_effect()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareAsync("S10-P18-NO-POLICY", 100m, "USD");

        var result = await fixture.NewExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p18-no-policy", prepared.Run.Version);

        Assert.False(result.Succeeded);
        Assert.Equal("migration_opening_monetary_policy_required", result.Code);
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Empty(await db.OpenItems.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await db.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p19_cross_tenant_sql_rate_cannot_satisfy_or_leak_into_foreign_opening()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        await using var connection = await safety.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable);
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var rateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        await using (var masterData = new MasterDataDbContext(options, safety.TenantB))
        {
            masterData.Currencies.AddRange(
                new MasterDataCurrencyEntity(source, safety.TenantB.TenantId, "USD", new LocalizedName("US Dollar")),
                new MasterDataCurrencyEntity(target, safety.TenantB.TenantId, "SAR", new LocalizedName("Saudi Riyal")));
            var rate = new MasterDataExchangeRateEntity(rateId, safety.TenantB.TenantId, source, target);
            rate.Versions.Add(new MasterDataExchangeRateVersionEntity(versionId, safety.TenantB.TenantId, rateId, 1, new DateOnly(2026, 1, 1), null, 3.75m, 2, ExchangeRateProvenance.Configured, "S10 P19 foreign Tenant seed", "USD", "SAR"));
            masterData.ExchangeRates.Add(rate);
            await masterData.SaveChangesAsync();
        }

        var rates = new MasterDataExchangeRatePersistence(options);
        Assert.Equal(rateId, Assert.Single(await rates.ListExchangeRatesAsync(safety.TenantB)).Id);
        Assert.Empty(await rates.ListExchangeRatesAsync(fixture.Tenant));
        var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(fixture.Tenant.TenantId.Value, fixture.CompanyId, "S10 P19", "SAR")]);
        var settlement = new FinanceSettlementPersistence(fixture.FinanceOptions, companies, rates,
            new SqlCustomerReader(fixture.CustomerId),
            fixture.SupplierReader, fixture.PaymentTerms, new UnavailableFinanceSupplierInvoiceSourceProvider(), fixture.Approval);
        var prepared = await fixture.PrepareAsync("S10-P19-FOREIGN-RATE", 100m, "USD");

        var result = await fixture.NewExecution(settlement).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p19-foreign-rate", prepared.Run.Version);

        Assert.False(result.Succeeded);
        Assert.Equal("migration_opening_exchange_rate_unavailable", result.Code);
        Assert.DoesNotContain(rateId.ToString("D"), result.Code, StringComparison.OrdinalIgnoreCase);
        await using var financeDb = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        Assert.Empty(await financeDb.OpenItems.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await financeDb.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
        Assert.Empty(await financeDb.SourceEffects.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1").ToListAsync());
    }

    [Fact]
    public async Task Sql_server_s10_p21_ordinary_receipt_allocation_uses_historical_opening_carrying_ratio()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var prepared = await fixture.PrepareAsync("S10-P21-OPENING", 100m, "USD");
        var migration = fixture.NewExecution();
        var opening = await migration.ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p21-opening", prepared.Run.Version);
        Assert.True(opening.Succeeded, opening.Code);
        var openingEvidence = Assert.Single(opening.Value!.ArEconomicReconciliations!);
        Assert.Equal(375m, openingEvidence.FunctionalAmount);
        fixture.ExchangeRates.Rate = 4m;

        var context = fixture.FinanceContextFor("tenant.finance.settlement.manage");
        var finance = new FinancePersistence(fixture.FinanceOptions,
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(fixture.Tenant.TenantId.Value, fixture.CompanyId, "S10 P21", "SAR")]),
            new UnavailableInventoryValuationPersistence(), fixture.ExchangeRates);
        var setup = fixture.FinanceContextFor("tenant.finance.setup");
        var onAccountRule = await finance.CreatePostingRuleAsync(setup,
            new FinancePostingRuleCommand(fixture.CompanyId, "customer-receipt.v1", "on-account", fixture.CashLinkedAccountId, fixture.ArOffsetAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s10-p21-on-account", "s10-p21-on-account"));
        Assert.True(onAccountRule.Succeeded, onAccountRule.Code);
        var realizedGain = await finance.CreateAccountAsync(setup,
            new FinanceAccountCommand(fixture.CompanyId, "S10-P21-FX-GAIN", "S10 P21 realized FX gain", null, null, FinanceAccountType.Revenue, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s10-p21-realized-gain", "s10-p21-realized-gain"));
        Assert.True(realizedGain.Succeeded, realizedGain.Code);
        var realizedRule = await finance.CreatePostingRuleAsync(setup,
            new FinancePostingRuleCommand(fixture.CompanyId, "finance-fx.v1", "realized", fixture.ArOffsetAccountId, realizedGain.Value!.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s10-p21-realized-fx", "s10-p21-realized-fx"));
        Assert.True(realizedRule.Succeeded, realizedRule.Code);
        var method = await fixture.Settlement.CreatePaymentMethodAsync(context,
            new FinancePaymentMethodCommand(fixture.CompanyId, "S10-P21-RECEIPT", "S10 P21 receipt", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s10-p21-method", "s10-p21-method"));
        Assert.True(method.Succeeded, method.Code);
        var cash = await fixture.Settlement.CreateCashAccountAsync(context,
            new FinanceCashAccountCommand(fixture.CompanyId, "S10-P21-USD-CASH", "S10 P21 USD cash", null, FinanceCashAccountKind.Bank, "USD", fixture.CashLinkedAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s10-p21-cash", "s10-p21-cash"));
        Assert.True(cash.Succeeded, cash.Code);
        var settlement = await fixture.Settlement.CreateSettlementDocumentAsync(context,
            new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, fixture.CompanyId, null, fixture.CustomerId, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 20), "USD", 25m, null, 4m, fixture.ExchangeRates.RateId, fixture.ExchangeRates.VersionId, 1, "S10-P21", "Settlement after foreign opening", Guid.NewGuid(), "s10-p21-settlement", "s10-p21-settlement"));
        Assert.True(settlement.Succeeded, settlement.Code);
        var submitted = await fixture.Settlement.TransitionSettlementDocumentAsync(context,
            new FinanceSettlementActionCommand(settlement.Value!.Id, settlement.Value.Version, null, "s10-p21-submit", "s10-p21-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var posted = await fixture.Settlement.PostSettlementDocumentAsync(fixture.FinanceContextFor("tenant.finance.settlement.post"),
            new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "s10-p21-post", "s10-p21-post", FinancePaymentMethodDirection.Receipt));
        Assert.True(posted.Succeeded, posted.Code);
        var allocation = await fixture.Settlement.CreateAllocationAsync(fixture.FinanceContextFor("tenant.finance.allocation.create"),
            new FinanceAllocationCommand(posted.Value!.Id, openingEvidence.OpenItemId!.Value, 25m, new DateOnly(2026, 1, 20), "S10 P21 historical carrying", Guid.NewGuid(), "s10-p21-allocation", "s10-p21-allocation"));

        Assert.True(allocation.Succeeded, allocation.Code);
        Assert.Equal(93.75m, allocation.Value!.HistoricalFunctionalAmount);
        Assert.Equal(100m, allocation.Value.SettlementFunctionalAmount);
        Assert.Equal(6.25m, allocation.Value.RealizedFxAmount);
        Assert.Equal("Gain", allocation.Value.RealizedFxDirection);
        Assert.NotNull(allocation.Value.RealizedFxJournalId);
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        var openItem = await db.OpenItems.SingleAsync(item => item.Id == openingEvidence.OpenItemId);
        Assert.Equal(375m, openItem.OriginalFunctionalAmount);
        Assert.Equal(fixture.ExchangeRates.RateId, openItem.ExchangeRateId);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1"));
    }

    [Fact]
    public async Task Sql_server_s10_p22_finance_revaluation_uses_opening_historical_functional_basis()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync(revaluationEnabled: true);
        var prepared = await fixture.PrepareAsync("S10-P22-OPENING", 100m, "USD");
        var opening = await fixture.NewExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p22-opening", prepared.Run.Version);
        Assert.True(opening.Succeeded, opening.Code);
        var openingEvidence = Assert.Single(opening.Value!.ArEconomicReconciliations!);
        Assert.Equal(375m, openingEvidence.FunctionalAmount);
        await using (var before = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant))
        Assert.Empty(await before.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "finance-revaluation.v1").ToListAsync());

        fixture.ExchangeRates.Rate = 4m;
        var finance = new FinancePersistence(fixture.FinanceOptions,
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(fixture.Tenant.TenantId.Value, fixture.CompanyId, "S10 P22", "SAR")]),
            new UnavailableInventoryValuationPersistence(), fixture.ExchangeRates);
        var fxRule = await finance.CreatePostingRuleAsync(fixture.FinanceContextFor("tenant.finance.setup"),
            new FinancePostingRuleCommand(fixture.CompanyId, "finance-fx.v1", "unrealized", fixture.GlDebitAccountId, fixture.GlCreditAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s10-p22-fx-rule", "s10-p22-fx-rule"));
        Assert.True(fxRule.Succeeded, fxRule.Code);
        await using var connection = await safety.OpenConnectionAsync();
        var masterDataOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable);
        var taxAuthorization = new MasterDataResourceAuthorizationService(new S10GrantingMasterDataCapabilities(), new TaxResourcePolicy(), new TaxApprovalPolicy(), new TaxScopePolicy());
        var taxService = new MasterDataTaxService(taxAuthorization, new MasterDataTaxPersistence(masterDataOptions));
        var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(fixture.Tenant.TenantId.Value, fixture.CompanyId, "S10 P22", "SAR")]);
        var revaluation = new FinanceMesp134Persistence(fixture.FinanceOptions, companies, fixture.PaymentTerms, fixture.ExchangeRates, taxService, new UnavailableFinanceSupplierInvoiceSourceProvider());
        var context = fixture.FinanceContextFor("tenant.finance.tax.post");
        var batch = await revaluation.CreateRevaluationBatchAsync(context,
            new FinanceRevaluationBatchCommand(fixture.CompanyId, new DateOnly(2026, 1, 15), FinanceRevaluationScopes.ApArAndUnallocatedSettlements, Guid.NewGuid(), "s10-p22-create", "s10-p22-create"));
        Assert.True(batch.Succeeded, batch.Code);
        var calculated = await revaluation.CalculateRevaluationBatchAsync(context,
            new FinanceRevaluationActionCommand(batch.Value!.Id, batch.Value.Version, null, Guid.NewGuid(), "s10-p22-calculate", "s10-p22-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        var line = Assert.Single(calculated.Value!.Lines);
        Assert.Equal(100m, line.OutstandingTransactionAmount);
        Assert.Equal(375m, line.HistoricalFunctionalAmount);
        Assert.Equal(400m, line.RevaluedFunctionalAmount);
        Assert.Equal(25m, line.Difference);
        var posted = await revaluation.PostRevaluationBatchAsync(context,
            new FinanceRevaluationActionCommand(calculated.Value.Id, calculated.Value.Version, null, Guid.NewGuid(), "s10-p22-post", "s10-p22-post"));
        Assert.True(posted.Succeeded, posted.Code);
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        var openItem = await db.OpenItems.SingleAsync(item => item.Id == openingEvidence.OpenItemId);
        Assert.Equal(375m, openItem.OriginalFunctionalAmount);
        var monetary = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(await db.JournalMonetaryEvidence.Where(item => item.JournalId == openingEvidence.JournalId).Select(item => item.MonetaryEvidenceJson).SingleAsync())!;
        Assert.Equal(3.75m, monetary.TransactionToFunctionalRate!.Rate);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "finance-revaluation.v1"));
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == fixture.CompanyId && item.SourceContract == "migration-ar-opening.v1"));
    }

    [Fact]
    public async Task Sql_server_s10_p23_public_read_exposes_persisted_ar_ap_cash_fx_and_functional_gl_without_rate_reresolution()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var usdCash = await fixture.Settlement.CreateCashAccountAsync(fixture.FinanceContextFor("tenant.finance.settlement.manage"),
            new FinanceCashAccountCommand(fixture.CompanyId, "S10-P23-USD-CASH", "S10 P23 USD cash", null, FinanceCashAccountKind.Bank, "USD", fixture.CashLinkedAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s10-p23-cash", "s10-p23-cash"));
        Assert.True(usdCash.Succeeded, usdCash.Code);
        var prepared = await fixture.PrepareMixedAsync("S10-P23", 375m, includeCash: true, includeGl: true,
            cashAmount: 100m, cashCurrencyCode: "USD", cashAccountIdOverride: usdCash.Value!.Id,
            arAmount: 100m, arCurrencyCode: "USD", apAmount: 100m, apCurrencyCode: "USD");
        var execution = fixture.NewMixedExecution();
        var executed = await execution.ExecuteAsync(fixture.Request, prepared.Run.RunId, "s10-p23-execute", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);
        var before = await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, executed.Value!.AttemptId);
        fixture.ExchangeRates.Rate = 4m;

        var read = await execution.ReadAsync(fixture.Request, prepared.Run.RunId);

        Assert.NotNull(read);
        Assert.Equal("migration-economic-execution-v2", read!.FingerprintVersion);
        var ar = Assert.Single(read.ArEconomicReconciliations!, item => item.SourceReference == "S10-P23");
        var ap = Assert.Single(read.ApEconomicReconciliations!, item => item.SourceReference == "S10-P23-AP");
        var cash = Assert.Single(read.CashBankEconomicReconciliations!, item => item.SourceReference == "S10-P23-CASH");
        void AssertOpeningEvidence(string? transactionCurrency, decimal? transactionAmount, string? functionalCurrency,
            decimal? functionalAmount, DateOnly? rateDate, Guid? rateId, Guid? rateVersionId, int? rateVersionNumber,
            decimal? appliedRate, Guid? policyId, int? policyVersion, int? roundingScale, string? roundingMode)
        {
            Assert.Equal("USD", transactionCurrency);
            Assert.Equal(100m, transactionAmount);
            Assert.Equal("SAR", functionalCurrency);
            Assert.Equal(375m, functionalAmount);
            Assert.Equal(new DateOnly(2026, 1, 15), rateDate);
            Assert.Equal(fixture.ExchangeRates.RateId, rateId);
            Assert.Equal(fixture.ExchangeRates.VersionId, rateVersionId);
            Assert.Equal(1, rateVersionNumber);
            Assert.Equal(3.75m, appliedRate);
            Assert.NotNull(policyId);
            Assert.Equal(1, policyVersion);
            Assert.Equal(2, roundingScale);
            Assert.Equal("AwayFromZero", roundingMode);
        }
        AssertOpeningEvidence(ar.TransactionCurrencyCode, ar.TransactionAmount, ar.FunctionalCurrencyCode, ar.FunctionalAmount, ar.RateDate, ar.ExchangeRateId, ar.ExchangeRateVersionId, ar.ExchangeRateVersionNumber, ar.AppliedRate, ar.MonetaryPolicyId, ar.MonetaryPolicyVersionNumber, ar.RoundingScale, ar.RoundingMode);
        AssertOpeningEvidence(ap.TransactionCurrencyCode, ap.TransactionAmount, ap.FunctionalCurrencyCode, ap.FunctionalAmount, ap.RateDate, ap.ExchangeRateId, ap.ExchangeRateVersionId, ap.ExchangeRateVersionNumber, ap.AppliedRate, ap.MonetaryPolicyId, ap.MonetaryPolicyVersionNumber, ap.RoundingScale, ap.RoundingMode);
        AssertOpeningEvidence(cash.TransactionCurrencyCode, cash.TransactionAmount, cash.FunctionalCurrencyCode, cash.FunctionalAmount, cash.RateDate, cash.ExchangeRateId, cash.ExchangeRateVersionId, cash.ExchangeRateVersionNumber, cash.AppliedRate, cash.MonetaryPolicyId, cash.MonetaryPolicyVersionNumber, cash.RoundingScale, cash.RoundingMode);
        var glLines = read.GlEconomicReconciliations!.SelectMany(item => item.Lines!).ToArray();
        Assert.Contains(glLines, line =>
            line.AccountId == fixture.ArAccountId && line.AccountingTreatment == "represented_by_ar" && line.EstablishedSignedAmount == 375m && line.ResidualDebit == 0m && line.ResidualCredit == 0m);
        Assert.Contains(glLines, line =>
            line.AccountId == fixture.CashLinkedAccountId && line.AccountingTreatment == "represented_by_cash_bank" && line.EstablishedSignedAmount == 375m && line.ResidualDebit == 0m && line.ResidualCredit == 0m);
        Assert.Equal(before, await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, executed.Value.AttemptId));
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        var allowedOpeningContracts = new[] { "inventory-valuation-finance.v1", "migration-ar-opening.v1", "migration-ap-opening.v1", "migration-cash-bank-opening.v1", "migration-gl-opening.v1" };
        Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && !allowedOpeningContracts.Contains(item.SourceContract)).ToListAsync());
        Assert.Empty(await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && (item.SourceContract == "finance-fx.v1" || item.SourceContract == "finance-revaluation.v1")).ToListAsync());
    }

    private static FinanceMigrationOpeningExpectation Mismatch(FinanceMigrationOpeningExpectation value, string dimension) => dimension switch
    {
        "transaction-amount" => value with { TransactionAmount = value.TransactionAmount + 1m },
        "functional-amount" => value with { FunctionalAmount = value.FunctionalAmount + 1m },
        "rate-id" => value with { ExchangeRateId = Guid.NewGuid() },
        "rate-version-id" => value with { ExchangeRateVersionId = Guid.NewGuid() },
        "rate-version-number" => value with { ExchangeRateVersionNumber = value.ExchangeRateVersionNumber + 1 },
        "applied-rate" => value with { AppliedRate = value.AppliedRate + 0.01m },
        "policy-id" => value with { MonetaryPolicyId = Guid.NewGuid() },
        "policy-version" => value with { MonetaryPolicyVersionNumber = value.MonetaryPolicyVersionNumber + 1 },
        "rounding-scale" => value with { RoundingScale = value.RoundingScale + 1 },
        "rounding-mode" => value with { RoundingMode = "ToEven" },
        "reporting-currency" => value with { ReportingCurrencyCode = "USD" },
        "reporting-rate-id" => value with { ReportingExchangeRateId = Guid.NewGuid() },
        "reporting-rate-version-id" => value with { ReportingExchangeRateVersionId = Guid.NewGuid() },
        "reporting-rate-version-number" => value with { ReportingExchangeRateVersionNumber = 2 },
        "reporting-applied-rate" => value with { ReportingAppliedRate = 3.75m },
        _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };

    private sealed class SqlCurrencyReferenceAuthority(IMasterDataCurrencyPaymentTermPersistence currencies) : IMigrationReferenceAuthority
    {
        public async Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default)
        {
            if (requestContext.TenantContext is not { } tenant || row.Payload is not MigrationArOpeningPayload ar || string.IsNullOrWhiteSpace(ar.CurrencyCode))
                return [new(MigrationReferenceState.Unavailable, MigrationFindingCategory.Reference, "migration_reference_tenant_unavailable", "Tenant or AR source context is unavailable.")];
            var currency = (await currencies.ListCurrenciesAsync(tenant, cancellationToken)).FirstOrDefault(item => string.Equals(item.Code, ar.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase));
            return
            [
                new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, "customer_active", "Customer is active."),
                currency is null
                    ? new(MigrationReferenceState.Missing, MigrationFindingCategory.Currency, "migration_currency_missing", "Currency is not configured.", ar.CurrencyCode)
                    : currency.LifecycleState == MasterDataLifecycleState.Active
                        ? new(MigrationReferenceState.Active, MigrationFindingCategory.Currency, "migration_currency_active", "Currency is active.", currency.Code)
                        : new(MigrationReferenceState.Inactive, MigrationFindingCategory.Currency, "migration_currency_inactive", "Currency is inactive.", currency.Code)
            ];
        }

        public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) => row.Payload is MigrationArOpeningPayload ar
            ? MigrationBusinessIdentityResolution.Valid($"ar-opening:{ar.CompanyId:D}:{ar.CustomerId:D}:{ar.SourceReference?.Trim()}")
            : MigrationBusinessIdentityResolution.NotApplicable();
    }

    private sealed class S10GrantingMasterDataCapabilities : IMasterDataCapabilityResolver
    {
        private readonly IReadOnlySet<MasterDataCapability> capabilities = Enum.GetValues<MasterDataCapability>().ToHashSet();
        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => capabilities;
    }

    private sealed class SqlCustomerReader(Guid customerId) : IBusinessCustomerReferenceReader
    {
        public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid requestedId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BusinessCustomerReference?>(requestedId == customerId
                ? new BusinessCustomerReference(customerId, tenantContext.TenantId, "S10-CUSTOMER", MasterDataLifecycleState.Active)
                : null);
    }
}
