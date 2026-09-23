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
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationCashBankOpeningSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    internal enum OffsetVariant
    {
        NonPosting,
        Inactive,
        Future,
        Expired
    }

    [Fact]
    public async Task Sql_server_cash_bank_opening_is_finance_owned_replay_safe_and_settleable()
    {
        await using var connection = await safety.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var migrationOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);
        var actorId = Guid.NewGuid();
        var tenant = TenantContext.ForOrdinaryMembership(safety.TenantA.TenantId, new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("cash-sql"), actorId: actorId);
        var companyId = Guid.NewGuid();
        var customerId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var openingDate = new DateOnly(2026, 1, 15);
        var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenant.TenantId.Value, companyId, "Cash opening company", "SAR")]);
        var approval = new MutableApprovalPolicy();
        var setup = new FinancePersistence(options, companies, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence(), approval);
        var settlement = new FinanceSettlementPersistence(options, companies, new UnavailableMasterDataExchangeRatePersistence(), new ActiveCustomerReader(customerId), new UnavailableSupplierPersistence(), new UnavailableMasterDataCurrencyPaymentTermPersistence(), new UnavailableFinanceSupplierInvoiceSourceProvider(), approval);
        var accountContext = Context(tenant, actorId, "tenant.finance.account.manage");
        var calendarContext = Context(tenant, actorId, "tenant.finance.calendar.manage");
        var ruleContext = Context(tenant, actorId, "tenant.finance.posting-rule.manage");
        var migrationContext = Context(tenant, actorId, "tenant.migration.execute");
        var migrationFoundationContext = FoundationContext(tenant, actorId, "tenant.migration.execute");
        var settlementContext = Context(tenant, actorId, "tenant.finance.settlement.manage");

        var cashGl = await setup.CreateAccountAsync(accountContext, Account(companyId, "CASH-SLICE8", FinanceAccountType.Asset));
        var offsetGl = await setup.CreateAccountAsync(accountContext, Account(companyId, "CASH-SLICE8-OFFSET", FinanceAccountType.Equity));
        var arGl = await setup.CreateAccountAsync(accountContext, Account(companyId, "CASH-SLICE8-AR", FinanceAccountType.Asset));
        Assert.True(cashGl.Succeeded, cashGl.Code);
        Assert.True(offsetGl.Succeeded, offsetGl.Code);
        Assert.True(arGl.Succeeded, arGl.Code);

        var calendar = await setup.CreateCalendarAsync(calendarContext, new FinanceFiscalCalendarCommand(companyId, "Cash FY", Guid.NewGuid(), "cash-calendar", "cash-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await setup.CreateYearAsync(calendarContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "cash-year", "cash-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await setup.CreatePeriodAsync(calendarContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "cash-period", "cash-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await setup.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "cash-period-open", "cash-period-open"));
        Assert.True(opened.Succeeded, opened.Code);

        var cash = await settlement.CreateCashAccountAsync(settlementContext, new FinanceCashAccountCommand(companyId, "BANK-SLICE8", "Slice 8 bank", null, FinanceCashAccountKind.Bank, "SAR", cashGl.Value!.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "cash-account", "cash-account"));
        Assert.True(cash.Succeeded, cash.Code);
        var openingRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "migration-cash-bank-opening.v1", "recognition", cashGl.Value.Id, offsetGl.Value!.Id, false, openingDate, null, Guid.NewGuid(), "cash-opening-rule", "cash-opening-rule"));
        var receiptRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "customer-receipt.v1", "on-account", cashGl.Value.Id, arGl.Value!.Id, false, openingDate, null, Guid.NewGuid(), "cash-receipt-rule", "cash-receipt-rule"));
        Assert.True(openingRule.Succeeded, openingRule.Code);
        Assert.True(receiptRule.Succeeded, receiptRule.Code);

        var migration = new MigrationPersistence(migrationOptions);
        var foundation = new MigrationFoundationService(migration, new NoopAuditSink());
        var scopes = new TenantWideScopeResolver();
        var references = new CashReferenceAuthority();
        var cashBeforeValidation = Assert.Single(await settlement.ListCashAccountsAsync(migrationContext, companyId), item => item.Id == cash.Value!.Id);
        async Task AssertValidationHasNoFinanceEffectsAsync()
        {
            await using var preflightDb = new FinanceDbContext(options, tenant);
            Assert.Equal(0, await preflightDb.Journals.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "migration-cash-bank-opening.v1"));
            Assert.Equal(0, await preflightDb.SourceEffects.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "migration-cash-bank-opening.v1"));
            Assert.Equal(0, await preflightDb.SettlementDocuments.CountAsync(item => item.CompanyId == companyId));
            var currentCash = Assert.Single(await settlement.ListCashAccountsAsync(migrationContext, companyId), item => item.Id == cash.Value.Id);
            Assert.Equal(cashBeforeValidation.Id, currentCash.Id);
            Assert.Equal(cashBeforeValidation.LinkedAccountId, currentCash.LinkedAccountId);
            Assert.Equal(cashBeforeValidation.Lifecycle, currentCash.Lifecycle);
            Assert.Equal(cashBeforeValidation.EffectiveFrom, currentCash.EffectiveFrom);
            Assert.Equal(cashBeforeValidation.EffectiveTo, currentCash.EffectiveTo);
            Assert.Equal(cashBeforeValidation.Version, currentCash.Version);
        }
        var objectId = Guid.NewGuid();
        var sourceHash = new string('B', 64);
        var package = JsonSerializer.SerializeToUtf8Bytes(new
        {
            packageVersion = MigrationCanonicalPackageParser.Version,
            definitionId = "tenant-onboarding.foundation",
            definitionVersion = "1",
            sourceProfileId = "neutral-source-profile",
            sourceProfileVersion = "1",
            logicalDataset = "opening",
            sourceSnapshot = new { objectId, sha256 = sourceHash, length = 1, concurrencyVersion = 1 },
            records = new[]
            {
                new
                {
                    sourceSequence = 1,
                    sourceRecordId = "cash-1",
                    recordType = "CashBankOpening",
                    payload = new { companyId, cashAccountId = cash.Value!.Id, sourceReference = " CASH-OPEN-001 ", amount = 100m, currencyCode = "sar", openingDate }
                }
            }
        });
        var source = new MigrationSourceArtifactSnapshot(objectId, tenant.TenantId, null, null, null, sourceHash, 1, 1);
        var storage = new StaticPrivateObjectStorage(tenant, objectId, package, source);
        var definition = new MigrationDefinitionReference("tenant-onboarding.foundation", "1");
        var profile = new MigrationSourceProfileReference("neutral-source-profile", "1");
        var intake = await new MigrationIntakeService(migration, storage, scopes, new NoopAuditSink()).RegisterAsync(migrationFoundationContext, new MigrationIntakeRegistrationRequest(definition, profile, MigrationOperationKind.Validation, objectId), "cash-intake");
        Assert.True(intake.Succeeded, intake.Code);
        var runId = intake.Value!.Run.RunId;
        var validationService = new MigrationValidationService(foundation, migration, storage, scopes, references, scopes);
        var validation = await validationService.ValidateAsync(migrationFoundationContext, runId, "cash-validation");
        Assert.True(validation.Succeeded, validation.Code);
        Assert.Equal(1, validation.Value!.AcceptedCount);
        await AssertValidationHasNoFinanceEffectsAsync();
        var dryRun = await validationService.DryRunAsync(migrationFoundationContext, runId, "cash-dry-run");
        Assert.True(dryRun.Succeeded, dryRun.Code);
        Assert.Equal(MigrationPlannedAction.Create, Assert.Single(dryRun.Value!.Rows).PlannedAction);
        await AssertValidationHasNoFinanceEffectsAsync();
        var approved = await foundation.TransitionRunAsync(migrationFoundationContext, runId, MigrationRunStatus.Approved, (await migration.FindRunAsync(tenant, runId))!.Version);
        Assert.True(approved.Succeeded, approved.Code);

        var coordinator = new MigrationCashBankOpeningExecutionCoordinator(migration, references, settlement);
        var execution = new MigrationExecutionService(foundation, migration, migration, migration, scopes, scopes, new UnusedOwnerGateway(), references, null, null, null, null, coordinator);
        var executed = await execution.ExecuteAsync(migrationFoundationContext, runId, "cash-execution", approved.Value!.Version);
        Assert.True(executed.Succeeded, executed.Code);
        Assert.Equal(MigrationRunStatus.Completed, executed.Value!.RunStatus);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, executed.Value.FingerprintVersion);
        Assert.Single(executed.Value.Effects, item => item.RecordType == MigrationCanonicalRecordType.CashBankOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed);
        Assert.Equal(3, executed.Value.Representations!.Count);
        Assert.Contains(executed.Value.Representations, item => item.Kind == MigrationEconomicRepresentationKind.FinanceJournal);
        Assert.Contains(executed.Value.Representations, item => item.Kind == MigrationEconomicRepresentationKind.FinanceSourceEffect);
        Assert.Contains(executed.Value.Representations, item => item.Kind == MigrationEconomicRepresentationKind.FinanceCashAccount);
        Assert.Equal("reconciled", Assert.Single(executed.Value.CashBankEconomicReconciliations!).Status);

        var staged = Assert.Single(await migration.ListStagedRecordsAsync(tenant, runId, 0, 100));
        var command = new FinanceMigrationCashBankOpeningCommand(companyId, cash.Value.Id, " CASH-OPEN-001 ", openingDate, 100m, "sar", staged.PayloadHash, "cash-direct", staged.PayloadHash);
        var ready = await settlement.PreflightMigrationCashBankOpeningAsync(migrationContext, command);
        Assert.True(ready.Ready, ready.Code);
        Assert.Equal("replay", ready.Code);
        var replay = await settlement.CreateMigrationCashBankOpeningAsync(migrationContext, command with { IdempotencyKey = "cash-replay", RequestFingerprint = "cash-replay" });
        Assert.True(replay.Succeeded, replay.Code);
        var changed = await settlement.CreateMigrationCashBankOpeningAsync(migrationContext, command with { Amount = 101m, IdempotencyKey = "cash-changed", RequestFingerprint = "cash-changed" });
        Assert.False(changed.Succeeded);
        Assert.Equal("migration_cash_bank_source_conflict", changed.Code);
        var foreign = await settlement.PreflightMigrationCashBankOpeningAsync(migrationContext, command with { SourceReference = "CASH-FOREIGN", CurrencyCode = "USD" });
        Assert.False(foreign.Ready);
        Assert.Equal("cash_account_currency_mismatch", foreign.Code);
        approval.Requirement = FinanceApprovalRequirement.Required;
        var approvalBlocked = await settlement.PreflightMigrationCashBankOpeningAsync(migrationContext, command with { SourceReference = "CASH-APPROVAL" });
        Assert.False(approvalBlocked.Ready);
        Assert.Equal("approval_required", approvalBlocked.Code);
        approval.Requirement = FinanceApprovalRequirement.NotRequired;

        var raceCommand = command with { SourceReference = "CASH-RACE", Amount = 25m };
        var race = await Task.WhenAll(
            settlement.CreateMigrationCashBankOpeningAsync(migrationContext, raceCommand with { IdempotencyKey = "cash-race-a", RequestFingerprint = "cash-race-a" }),
            settlement.CreateMigrationCashBankOpeningAsync(migrationContext, raceCommand with { IdempotencyKey = "cash-race-b", RequestFingerprint = "cash-race-b" }));
        Assert.All(race, item => Assert.True(item.Succeeded, item.Code));
        Assert.Single(race.Select(item => item.Value!.Id).Distinct());

        var method = await settlement.CreatePaymentMethodAsync(settlementContext, new FinancePaymentMethodCommand(companyId, "BANK-RECEIPT", "Bank receipt", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "cash-method", "cash-method"));
        Assert.True(method.Succeeded, method.Code);
        var beforeSettlement = await settlement.ListCashAccountsAsync(migrationContext, companyId);
        Assert.Single(beforeSettlement, item => item.Id == cash.Value.Id && item.LinkedAccountId == cashGl.Value.Id);
        await using (var beforeSettlementDb = new FinanceDbContext(options, tenant))
            Assert.Equal(0, await beforeSettlementDb.SettlementDocuments.CountAsync(item => item.CompanyId == companyId));
        var receipt = await settlement.CreateSettlementDocumentAsync(settlementContext, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, companyId, null, customerId, cash.Value.Id, method.Value!.Id, openingDate, "SAR", 10m, null, null, null, null, null, "cash-receipt", "ordinary receipt", Guid.NewGuid(), "cash-receipt-create", "cash-receipt-create"));
        Assert.True(receipt.Succeeded, receipt.Code);
        var submitted = await settlement.TransitionSettlementDocumentAsync(settlementContext, new FinanceSettlementActionCommand(receipt.Value!.Id, receipt.Value.Version, null, "cash-receipt-submit", "cash-receipt-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var posted = await settlement.PostSettlementDocumentAsync(settlementContext, new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "cash-receipt-post", "cash-receipt-post", FinancePaymentMethodDirection.Receipt));
        Assert.True(posted.Succeeded, posted.Code);

        await using var db = new FinanceDbContext(options, tenant);
        Assert.Equal(2, await db.SourceEffects.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "migration-cash-bank-opening.v1"));
        Assert.Equal(2, await db.Journals.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "migration-cash-bank-opening.v1" && item.Status == FinanceJournalStatus.Posted));
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == companyId && item.Description == "Cash/Bank opening CASH-RACE"));
        Assert.Equal(1, await db.SettlementDocuments.CountAsync(item => item.CompanyId == companyId));
        Assert.Equal(1, await db.SettlementDocuments.CountAsync(item => item.Id == posted.Value!.Id));

        var readBack = await execution.ReadAsync(migrationFoundationContext, runId);
        Assert.NotNull(readBack);
        Assert.Equal("reconciled", Assert.Single(readBack!.CashBankEconomicReconciliations!).Status);
    }

    [Fact]
    public async Task Sql_server_migration_cash_bank_cross_run_same_identity_converges_to_one_effect()
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var first = await fixture.PrepareAsync("S8-CROSS-RUN", 100m);
        var second = await fixture.PrepareAsync("S8-CROSS-RUN", 100m);

        var results = await Task.WhenAll(
            fixture.NewExecution().ExecuteAsync(fixture.Request, first.Run.RunId, "s8-cross-run-a", first.Run.Version),
            fixture.NewExecution().ExecuteAsync(fixture.Request, second.Run.RunId, "s8-cross-run-b", second.Run.Version));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Code));
        Assert.Equal(1, await fixture.CountCashEffectsAsync("S8-CROSS-RUN"));
        Assert.Equal(1, await fixture.CountCashJournalsAsync("S8-CROSS-RUN"));
    }

    [Fact]
    public async Task Sql_server_migration_cash_bank_changed_economics_has_one_winner_and_no_duplicate()
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var first = await fixture.PrepareAsync("S8-CROSS-CONFLICT", 100m);
        var second = await fixture.PrepareAsync("S8-CROSS-CONFLICT", 101m);

        var results = await Task.WhenAll(
            fixture.NewExecution().ExecuteAsync(fixture.Request, first.Run.RunId, "s8-conflict-a", first.Run.Version),
            fixture.NewExecution().ExecuteAsync(fixture.Request, second.Run.RunId, "s8-conflict-b", second.Run.Version));

        Assert.Contains(results, result => result.Succeeded);
        Assert.Contains(results, result => !result.Succeeded && result.Kind is MigrationResultKind.KnownFailure or MigrationResultKind.UnknownOutcome);
        Assert.Equal(1, await fixture.CountCashEffectsAsync("S8-CROSS-CONFLICT"));
        Assert.Equal(1, await fixture.CountCashJournalsAsync("S8-CROSS-CONFLICT"));
    }

    [Fact]
    public async Task Sql_server_migration_cash_bank_lost_response_recovers_committed_evidence()
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            ThrowAfterCashCreate = true
        };
        var prepared = await fixture.PrepareAsync("S8-LOST-RESPONSE", 100m);

        var result = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-lost-response", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(1, proxy.CashCreateCalls);
        Assert.True(proxy.CashReadCalls >= 1);
        Assert.Equal(MigrationRunStatus.Completed, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
        Assert.Equal(1, await fixture.CountCashEffectsAsync("S8-LOST-RESPONSE"));
        Assert.Equal(1, await fixture.CountCashJournalsAsync("S8-LOST-RESPONSE"));
    }

    [Fact]
    public async Task Sql_server_migration_cash_bank_unavailable_readback_is_unknown_and_non_replayable()
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement) { HideCashReadback = true };
        var prepared = await fixture.PrepareAsync("S8-MISSING-READBACK", 100m);

        var first = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-unknown-a", prepared.Run.Version);
        var sameKey = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-unknown-a", prepared.Run.Version);
        var differentKey = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-unknown-b", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.UnknownOutcome, sameKey.Kind);
        Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
        Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
        Assert.Equal(1, proxy.CashCreateCalls);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
        Assert.Equal(1, await fixture.CountCashEffectsAsync("S8-MISSING-READBACK"));
        Assert.Equal(1, await fixture.CountCashJournalsAsync("S8-MISSING-READBACK"));
    }

    [Theory]
    [InlineData("journal")]
    [InlineData("source")]
    public async Task Sql_server_migration_cash_bank_incomplete_readback_is_unknown_and_non_replayable(string missing)
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            AfterCashCreate = _ => fixture.RemoveCashEvidenceAsync(missing)
        };
        var prepared = await fixture.PrepareAsync($"S8-INCOMPLETE-{missing}", 100m);

        var first = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"s8-incomplete-{missing}-a", prepared.Run.Version);
        var sameKey = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"s8-incomplete-{missing}-a", prepared.Run.Version);
        var differentKey = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, $"s8-incomplete-{missing}-b", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.UnknownOutcome, sameKey.Kind);
        Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
        Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
        Assert.Equal(MigrationExecutionEffectDisposition.Unknown, Assert.Single(first.Value!.Effects).Disposition);
        Assert.Equal(1, proxy.CashCreateCalls);
        Assert.Equal(missing == "journal" ? 1 : 0, await fixture.CountCashEffectsAsync($"S8-INCOMPLETE-{missing}"));
        Assert.Equal(missing == "journal" ? 0 : 1, await fixture.CountCashJournalsAsync($"S8-INCOMPLETE-{missing}"));
    }

    [Fact]
    public async Task Sql_server_migration_cash_bank_owner_mismatch_fails_closed_without_replay()
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var proxy = new MigrationEconomicOpeningRemediationSqlServerSafetyTests.FaultingFinancePersistence(fixture.Settlement)
        {
            AfterCashCreate = _ => fixture.ChangeCashLinkAsync()
        };
        var prepared = await fixture.PrepareAsync("S8-OWNER-MISMATCH", 100m);

        var first = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-mismatch-a", prepared.Run.Version);
        var retry = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-mismatch-a", prepared.Run.Version);
        var differentKey = await fixture.NewExecution(proxy).ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-mismatch-b", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.UnknownOutcome, retry.Kind);
        Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
        Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
        Assert.Equal(MigrationExecutionEffectDisposition.Unknown, Assert.Single(first.Value!.Effects).Disposition);
        Assert.Equal(1, proxy.CashCreateCalls);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!.Status);
        Assert.Equal(1, await fixture.CountCashEffectsAsync("S8-OWNER-MISMATCH"));
        Assert.Equal(1, await fixture.CountCashJournalsAsync("S8-OWNER-MISMATCH"));
    }

    [Fact]
    public async Task Sql_server_cash_bank_preflight_matrix_preserves_finance_authority_and_zero_effect_boundaries()
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var command = fixture.Command("S8-MATRIX", 100m);
        var bank = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command);
        Assert.True(bank.Ready, bank.Code);

        var cashAccount = await fixture.CreateVariantAsync(FinanceCashAccountKind.Cash);
        var cash = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { CashAccountId = cashAccount.Id, SourceReference = "S8-MATRIX-CASH" });
        Assert.True(cash.Ready, cash.Code);

        var missing = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { CashAccountId = Guid.NewGuid(), SourceReference = "S8-MATRIX-MISSING" });
        Assert.False(missing.Ready);
        Assert.Equal("cash_account_missing", missing.Code);
        var wrongCompany = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { CompanyId = Guid.NewGuid(), SourceReference = "S8-MATRIX-COMPANY" });
        Assert.False(wrongCompany.Ready);
        Assert.Equal("company_scope_denied", wrongCompany.Code);
        var foreignTenant = TenantContext.ForOrdinaryMembership(new TenantId(Guid.NewGuid()), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("s8-foreign"), actorId: Guid.NewGuid());
        var foreignContext = Context(foreignTenant, foreignTenant.ActorId!.Value, "tenant.migration.execute");
        var foreign = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(foreignContext, command with { SourceReference = "S8-MATRIX-FOREIGN" });
        Assert.False(foreign.Ready);
        Assert.Equal("company_scope_denied", foreign.Code);

        var negative = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { Amount = -1m, SourceReference = "S8-MATRIX-NEGATIVE" });
        var zero = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { Amount = 0m, SourceReference = "S8-MATRIX-ZERO" });
        Assert.Equal("migration_cash_bank_opening_amount_negative", negative.Code);
        Assert.Equal("migration_cash_bank_opening_zero_amount", zero.Code);
        var foreignCurrency = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { CurrencyCode = "USD", SourceReference = "S8-MATRIX-FX" });
        Assert.Equal("cash_account_currency_mismatch", foreignCurrency.Code);
        var mismatchedCurrencyAccount = await fixture.CreateVariantAsync(FinanceCashAccountKind.Bank, "USD");
        var currencyMismatch = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { CashAccountId = mismatchedCurrencyAccount.Id, SourceReference = "S8-MATRIX-CURRENCY-MISMATCH" });
        Assert.Equal("cash_account_currency_mismatch", currencyMismatch.Code);

        fixture.Approval.Requirement = FinanceApprovalRequirement.Required;
        var approval = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { SourceReference = "S8-MATRIX-APPROVAL" });
        Assert.Equal("approval_required", approval.Code);
        fixture.Approval.Requirement = FinanceApprovalRequirement.NotConfigured;
        var unconfigured = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command with { SourceReference = "S8-MATRIX-NOT-CONFIGURED" });
        Assert.Equal("approval_policy_not_configured", unconfigured.Code);
    }

    [Fact]
    public async Task Sql_server_cash_bank_preflight_covers_finance_lifecycle_rule_period_offset_dimension_and_overlap_matrix()
    {
        async Task<string> CheckAsync(
            string sourceReference,
            Func<CashSqlFixture, Task>? arrange = null,
            Func<FinanceMigrationCashBankOpeningCommand, FinanceMigrationCashBankOpeningCommand>? alter = null)
        {
            await using var fixture = await CashSqlFixture.CreateAsync(safety);
            if (arrange is not null) await arrange(fixture);
            var command = alter?.Invoke(fixture.Command(sourceReference, 100m)) ?? fixture.Command(sourceReference, 100m);
            var result = await fixture.Settlement.PreflightMigrationCashBankOpeningAsync(fixture.FinanceContext, command);
            Assert.False(result.Ready);
            await fixture.AssertNoCashOpeningEffectsAsync();
            return result.Code;
        }

        Assert.Equal("cash_account_inactive", await CheckAsync("S8-R4-INACTIVE-CASH", fixture => fixture.DisableCashAccountAsync()));
        Assert.Equal("cash_account_not_effective", await CheckAsync("S8-R4-FUTURE-CASH", fixture => fixture.SetCashAccountEffectiveAsync(new DateOnly(2026, 1, 16), null)));
        Assert.Equal("cash_account_not_effective", await CheckAsync("S8-R4-EXPIRED-CASH", fixture => fixture.SetCashAccountEffectiveAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14))));

        Assert.Equal("cash_account_link_missing", await CheckAsync("S8-R4-MISSING-LINK", fixture => fixture.RemoveLinkedAccountAsync()));
        Assert.Equal("cash_account_link_not_postable", await CheckAsync("S8-R4-NONPOSTING-LINK", fixture => fixture.SetLinkedAccountPostingAsync(false)));
        Assert.Equal("cash_account_link_not_postable", await CheckAsync("S8-R4-INACTIVE-LINK", fixture => fixture.DisableLinkedAccountAsync()));
        Assert.Equal("cash_account_link_not_effective", await CheckAsync("S8-R4-FUTURE-LINK", fixture => fixture.SetLinkedAccountEffectiveAsync(new DateOnly(2026, 1, 16), null)));
        Assert.Equal("cash_account_link_not_effective", await CheckAsync("S8-R4-EXPIRED-LINK", fixture => fixture.SetLinkedAccountEffectiveAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14))));

        Assert.Equal("period_not_configured", await CheckAsync("S8-R4-NO-PERIOD", alter: command => command with { OpeningDate = new DateOnly(2027, 1, 15) }));
        Assert.Equal("period_ambiguous", await CheckAsync("S8-R4-AMBIGUOUS-PERIOD", fixture => fixture.AddOverlappingPeriodAsync()));
        Assert.Equal("period_soft_closed", await CheckAsync("S8-R4-SOFT-CLOSED", fixture => fixture.SetPeriodStateAsync(FinanceFiscalPeriodState.SoftClosed)));
        Assert.Equal("period_closed", await CheckAsync("S8-R4-CLOSED", fixture => fixture.SetPeriodStateAsync(FinanceFiscalPeriodState.Closed)));

        Assert.Equal("pending_mapping", await CheckAsync("S8-R4-MISSING-RULE", fixture => fixture.DisableOpeningRuleAsync()));
        Assert.Equal("ambiguous_mapping", await CheckAsync("S8-R4-AMBIGUOUS-RULE", fixture => fixture.AddAmbiguousOpeningRuleAsync()));
        Assert.Equal("posting_rule_accounts_invalid", await CheckAsync("S8-R4-DEBIT-CREDIT-SAME", fixture => fixture.AddOpeningRuleAsync(fixture.CashLinkedAccountId, fixture.CashLinkedAccountId)));
        Assert.Equal("cash_bank_posting_rule_cash_side_mismatch", await CheckAsync("S8-R4-CASH-SIDE-MISMATCH", fixture => fixture.AddOpeningRuleAsync(fixture.OffsetAccountId, fixture.CashLinkedAccountId)));

        Assert.Equal("cash_bank_opening_offset_not_postable", await CheckAsync("S8-R4-MISSING-OFFSET", fixture => fixture.AddOpeningRuleAsync(fixture.CashLinkedAccountId, Guid.NewGuid())));
        Assert.Equal("cash_bank_opening_offset_not_postable", await CheckAsync("S8-R4-NONPOSTING-OFFSET", fixture => fixture.UseOffsetVariantAsync(OffsetVariant.NonPosting)));
        Assert.Equal("cash_bank_opening_offset_not_postable", await CheckAsync("S8-R4-INACTIVE-OFFSET", fixture => fixture.UseOffsetVariantAsync(OffsetVariant.Inactive)));
        Assert.Equal("cash_bank_opening_offset_not_postable", await CheckAsync("S8-R4-FUTURE-OFFSET", fixture => fixture.UseOffsetVariantAsync(OffsetVariant.Future)));
        Assert.Equal("cash_bank_opening_offset_not_postable", await CheckAsync("S8-R4-EXPIRED-OFFSET", fixture => fixture.UseOffsetVariantAsync(OffsetVariant.Expired)));
        Assert.Equal("dimension_required", await CheckAsync("S8-R4-DIMENSION", fixture => fixture.AddOpeningRuleAsync(fixture.CashLinkedAccountId, fixture.OffsetAccountId, true)));

        Assert.Equal("cash_bank_account_overlaps_inventory_control", await CheckAsync("S8-R4-INVENTORY-OVERLAP", fixture => fixture.AddOverlapRuleAsync("inventory-valuation-finance.v1", "OpeningBalance:Inbound", fixture.CashLinkedAccountId, fixture.OffsetAccountId)));
        Assert.Equal("cash_bank_account_overlaps_ar_control", await CheckAsync("S8-R4-AR-OVERLAP", fixture => fixture.AddOverlapRuleAsync("migration-ar-opening.v1", "recognition", fixture.CashLinkedAccountId, fixture.OffsetAccountId)));
        Assert.Equal("cash_bank_account_overlaps_ap_control", await CheckAsync("S8-R4-AP-OVERLAP", fixture => fixture.AddOverlapRuleAsync("migration-ap-opening.v1", "recognition", fixture.OffsetAccountId, fixture.CashLinkedAccountId)));
    }

    [Theory]
    [InlineData("journal")]
    [InlineData("source")]
    public async Task Sql_server_cash_bank_negative_reconciliation_is_read_only(string missing)
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareAsync($"S8-R6-MISSING-{missing}", 100m);
        var executed = await fixture.NewExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, $"s8-r6-{missing}", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);

        await fixture.RemoveCashEvidenceAsync(missing);
        var beforeRead = await fixture.ReadCashEvidenceCountsAsync();
        var read = await fixture.NewExecution().ReadAsync(fixture.Request, prepared.Run.RunId);
        var reconciliation = Assert.Single(read!.CashBankEconomicReconciliations!);
        Assert.NotEqual("reconciled", reconciliation.Status);
        Assert.Equal("finance_cash_bank_opening_evidence_not_reconciled", reconciliation.SafeCode);
        Assert.Equal(beforeRead, await fixture.ReadCashEvidenceCountsAsync());
    }

    [Fact]
    public async Task Sql_server_cash_bank_owner_mismatch_reconciliation_is_read_only()
    {
        await using var fixture = await CashSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareAsync("S8-R6-OWNER-MISMATCH", 100m);
        var executed = await fixture.NewExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s8-r6-owner-mismatch", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);

        await fixture.ChangeCashLinkAsync();
        var beforeRead = await fixture.ReadCashEvidenceCountsAsync();
        var read = await fixture.NewExecution().ReadAsync(fixture.Request, prepared.Run.RunId);
        var reconciliation = Assert.Single(read!.CashBankEconomicReconciliations!);
        Assert.NotEqual("reconciled", reconciliation.Status);
        Assert.Equal("finance_cash_bank_opening_evidence_not_reconciled", reconciliation.SafeCode);
        Assert.Equal(beforeRead, await fixture.ReadCashEvidenceCountsAsync());
    }

    internal sealed class CashSqlFixture : IAsyncDisposable
    {
        private readonly Microsoft.Data.SqlClient.SqlConnection connection;
        private readonly MigrationFoundationService foundation;
        private readonly IMigrationReferenceAuthority references = new CashReferenceAuthority();
        private readonly TenantWideScopeResolver scopes = new();
        private readonly string sourceHash = new('A', 64);
        private readonly string packageHash = new('B', 64);
        private readonly FinancePersistence setup;

        private CashSqlFixture(
            Microsoft.Data.SqlClient.SqlConnection connection,
            DbContextOptions financeOptions,
            DbContextOptions migrationOptions,
            TenantContext tenant,
            Guid companyId,
            FinancePersistence setup,
            FinanceSettlementPersistence settlement,
            MigrationPersistence migration,
            FoundationRequestContext request,
            Guid cashAccountId,
            Guid cashLinkedAccountId,
            Guid offsetAccountId,
            Guid periodId,
            Guid fiscalYearId,
            Guid openingRuleId,
            byte[] openingRuleVersion,
            MutableApprovalPolicy approval)
        {
            this.connection = connection;
            FinanceOptions = financeOptions;
            MigrationOptions = migrationOptions;
            Tenant = tenant;
            CompanyId = companyId;
            this.setup = setup;
            Settlement = settlement;
            Migration = migration;
            foundation = new MigrationFoundationService(migration, new NoopAuditSink());
            Request = request;
            CashAccountId = cashAccountId;
            CashLinkedAccountId = cashLinkedAccountId;
            OffsetAccountId = offsetAccountId;
            PeriodId = periodId;
            FiscalYearId = fiscalYearId;
            OpeningRuleId = openingRuleId;
            OpeningRuleVersion = openingRuleVersion;
            Approval = approval;
        }

        internal DbContextOptions FinanceOptions { get; }
        private DbContextOptions MigrationOptions { get; }
        internal TenantContext Tenant { get; }
        internal Guid CompanyId { get; }
        internal Guid CashAccountId { get; }
        internal Guid CashLinkedAccountId { get; }
        internal Guid OffsetAccountId { get; }
        internal Guid PeriodId { get; }
        internal Guid FiscalYearId { get; }
        internal Guid OpeningRuleId { get; }
        internal byte[] OpeningRuleVersion { get; }
        internal MutableApprovalPolicy Approval { get; }
        internal FinanceSettlementPersistence Settlement { get; }
        internal MigrationPersistence Migration { get; }
        internal FoundationRequestContext Request { get; }
        internal FinanceRequestContext FinanceContext => FinanceRequestContext.TryCreate(Request, out var context) ? context! : throw new InvalidOperationException();

        internal static async Task<CashSqlFixture> CreateAsync(SqlServerSafetyFixture safety)
        {
            var connection = await safety.OpenConnectionAsync();
            var financeOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
            var migrationOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);
            var tenant = TenantContext.ForOrdinaryMembership(safety.TenantA.TenantId, new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("s8-cash"), actorId: Guid.NewGuid());
            var companyId = Guid.NewGuid();
            var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenant.TenantId.Value, companyId, "S8 Cash company", "SAR")]);
            var approval = new MutableApprovalPolicy();
            var setup = new FinancePersistence(financeOptions, companies, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence(), approval);
            var settlement = new FinanceSettlementPersistence(financeOptions, companies, new UnavailableMasterDataExchangeRatePersistence(), new ActiveCustomerReader(Guid.NewGuid()), new UnavailableSupplierPersistence(), new UnavailableMasterDataCurrencyPaymentTermPersistence(), new UnavailableFinanceSupplierInvoiceSourceProvider(), approval);
            var accountContext = Context(tenant, tenant.ActorId!.Value, "tenant.finance.account.manage");
            var calendarContext = Context(tenant, tenant.ActorId.Value, "tenant.finance.calendar.manage");
            var ruleContext = Context(tenant, tenant.ActorId.Value, "tenant.finance.posting-rule.manage");
            var settlementContext = Context(tenant, tenant.ActorId.Value, "tenant.finance.settlement.manage");
            var linked = await setup.CreateAccountAsync(accountContext, Account(companyId, "S8-CASH", FinanceAccountType.Asset));
            var offset = await setup.CreateAccountAsync(accountContext, Account(companyId, "S8-CASH-OFFSET", FinanceAccountType.Equity));
            Assert.True(linked.Succeeded, linked.Code);
            Assert.True(offset.Succeeded, offset.Code);
            var calendar = await setup.CreateCalendarAsync(calendarContext, new FinanceFiscalCalendarCommand(companyId, "S8 FY", Guid.NewGuid(), "s8-calendar", "s8-calendar"));
            Assert.True(calendar.Succeeded, calendar.Code);
            var year = await setup.CreateYearAsync(calendarContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "s8-year", "s8-year"));
            Assert.True(year.Succeeded, year.Code);
            var period = await setup.CreatePeriodAsync(calendarContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "S8-2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "s8-period", "s8-period"));
            Assert.True(period.Succeeded, period.Code);
            var opened = await setup.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "s8-period-open", "s8-period-open"));
            Assert.True(opened.Succeeded, opened.Code);
            var cash = await settlement.CreateCashAccountAsync(settlementContext, new FinanceCashAccountCommand(companyId, "S8-BANK", "S8 bank", null, FinanceCashAccountKind.Bank, "SAR", linked.Value!.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "s8-cash", "s8-cash"));
            Assert.True(cash.Succeeded, cash.Code);
            var rule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "migration-cash-bank-opening.v1", "recognition", linked.Value.Id, offset.Value!.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "s8-rule", "s8-rule"));
            Assert.True(rule.Succeeded, rule.Code);
            var request = FoundationContext(tenant, tenant.ActorId!.Value, "tenant.migration.execute");
            return new CashSqlFixture(connection, financeOptions, migrationOptions, tenant, companyId, setup, settlement, new MigrationPersistence(migrationOptions), request, cash.Value!.Id, linked.Value.Id, offset.Value.Id, period.Value.Id, year.Value!.Id, rule.Value!.Id, rule.Value.Version, approval);
        }

        internal async Task<FinanceCashAccountRecord> CreateVariantAsync(FinanceCashAccountKind kind, string currency = "SAR")
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var created = await Settlement.CreateCashAccountAsync(
                Context(Tenant, Tenant.ActorId!.Value, "tenant.finance.settlement.manage"),
                new FinanceCashAccountCommand(CompanyId, $"S8-{suffix}-CASH", $"S8 {kind} {suffix}", null, kind, currency, CashLinkedAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"s8-{suffix}", $"s8-{suffix}"));
            Assert.True(created.Succeeded, created.Code);
            return created.Value!;
        }

        internal async Task DisableCashAccountAsync()
        {
            var cash = Assert.Single(await Settlement.ListCashAccountsAsync(Context(Tenant, Tenant.ActorId!.Value, "tenant.finance.settlement.manage"), CompanyId), item => item.Id == CashAccountId);
            var disabled = await Settlement.SetCashAccountLifecycleAsync(Context(Tenant, Tenant.ActorId.Value, "tenant.finance.settlement.manage"), cash.Id, CompanyId, FinancePaymentMethodLifecycle.Inactive, cash.Version, "s8-cash-inactive", "s8-cash-inactive");
            Assert.True(disabled.Succeeded, disabled.Code);
        }

        internal async Task SetCashAccountEffectiveAsync(DateOnly effectiveFrom, DateOnly? effectiveTo)
        {
            var context = Context(Tenant, Tenant.ActorId!.Value, "tenant.finance.settlement.manage");
            var cash = Assert.Single(await Settlement.ListCashAccountsAsync(context, CompanyId), item => item.Id == CashAccountId);
            var edited = await Settlement.EditCashAccountAsync(context, new FinanceCashAccountCommand(CompanyId, cash.Code, cash.EnglishName, cash.ArabicName, cash.Kind, cash.CurrencyCode, cash.LinkedAccountId, cash.BankReference, effectiveFrom, effectiveTo, cash.Id, cash.Version, "s8-cash-effective", "s8-cash-effective"), cash.Version);
            Assert.True(edited.Succeeded, edited.Code);
        }

        internal async Task RemoveLinkedAccountAsync()
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [finance].[CashAccounts] DROP CONSTRAINT [FK_CashAccounts_Accounts_TenantId_CompanyId_LinkedAccountId]");
            var missing = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [finance].[CashAccounts] SET [LinkedAccountId] = {missing} WHERE [TenantId] = {Tenant.TenantId.Value} AND [CompanyId] = {CompanyId} AND [Id] = {CashAccountId}");
        }

        internal async Task SetLinkedAccountPostingAsync(bool isPosting)
        {
            var account = await LinkedAccountAsync();
            var edited = await setup.EditAccountAsync(AccountContext(), new FinanceAccountCommand(account.CompanyId, account.Code, account.EnglishName, account.ArabicName, account.ParentAccountId, account.AccountType, isPosting, account.CurrencyBehavior, account.EffectiveFrom, account.EffectiveTo, account.Id, account.Version, "s8-linked-posting", "s8-linked-posting"));
            Assert.True(edited.Succeeded, edited.Code);
        }

        internal async Task DisableLinkedAccountAsync()
        {
            var account = await LinkedAccountAsync();
            var disabled = await setup.SetAccountLifecycleAsync(AccountContext(), account.Id, CompanyId, FinanceAccountLifecycle.Inactive, account.Version, "s8-linked-inactive", "s8-linked-inactive");
            Assert.True(disabled.Succeeded, disabled.Code);
        }

        internal async Task SetLinkedAccountEffectiveAsync(DateOnly effectiveFrom, DateOnly? effectiveTo)
        {
            var account = await LinkedAccountAsync();
            var edited = await setup.EditAccountAsync(AccountContext(), new FinanceAccountCommand(account.CompanyId, account.Code, account.EnglishName, account.ArabicName, account.ParentAccountId, account.AccountType, account.IsPostingAccount, account.CurrencyBehavior, effectiveFrom, effectiveTo, account.Id, account.Version, "s8-linked-effective", "s8-linked-effective"));
            Assert.True(edited.Succeeded, edited.Code);
        }

        internal async Task AddOverlappingPeriodAsync()
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            db.FiscalPeriods.Add(new FinanceFiscalPeriodEntity(Tenant.TenantId, Guid.NewGuid(), new FinanceFiscalPeriodCommand(FiscalYearId, 2, "S8-2026-OVERLAP", "Overlap", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "s8-period-overlap", "s8-period-overlap"), CompanyId));
            await db.SaveChangesAsync();
            var overlap = await db.FiscalPeriods.SingleAsync(item => item.Code == "S8-2026-OVERLAP");
            overlap.SetState(FinanceFiscalPeriodState.Open);
            await db.SaveChangesAsync();
        }

        internal async Task SetPeriodStateAsync(FinanceFiscalPeriodState state)
        {
            var periods = await setup.ListPeriodsAsync(CalendarContext(), FiscalYearId);
            var period = Assert.Single(periods, item => item.Id == PeriodId);
            var changed = await setup.SetPeriodStateAsync(CalendarContext(), new FinancePeriodStateCommand(period.Id, state, "s8-period-state", period.Version, $"s8-period-{state}", $"s8-period-{state}"));
            Assert.True(changed.Succeeded, changed.Code);
        }

        internal async Task DisableOpeningRuleAsync()
        {
            var disabled = await setup.SetPostingRuleLifecycleAsync(RuleContext(), OpeningRuleId, CompanyId, FinancePostingRuleLifecycle.Disabled, OpeningRuleVersion, "s8-opening-rule-disable", "s8-opening-rule-disable");
            Assert.True(disabled.Succeeded, disabled.Code);
        }

        internal async Task AddOpeningRuleAsync(Guid debitAccountId, Guid creditAccountId, bool costCenterRequired = false)
        {
            await DisableOpeningRuleAsync();
            await AddPostingRuleDirectAsync("migration-cash-bank-opening.v1", "recognition", debitAccountId, creditAccountId, costCenterRequired);
        }

        internal Task AddAmbiguousOpeningRuleAsync() => AddPostingRuleDirectAsync(
            "migration-cash-bank-opening.v1",
            "recognition",
            CashLinkedAccountId,
            OffsetAccountId,
            false);

        internal async Task AddOverlapRuleAsync(string sourceContract, string sourceEvent, Guid debitAccountId, Guid creditAccountId)
        {
            var rule = await setup.CreatePostingRuleAsync(RuleContext(), new FinancePostingRuleCommand(CompanyId, sourceContract, sourceEvent, debitAccountId, creditAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"s8-overlap-{sourceContract}", $"s8-overlap-{sourceContract}"));
            Assert.True(rule.Succeeded, rule.Code);
        }

        internal async Task UseOffsetVariantAsync(OffsetVariant variant)
        {
            var created = await setup.CreateAccountAsync(AccountContext(), Account(CompanyId, $"S8-OFFSET-{Guid.NewGuid():N}"[..16], FinanceAccountType.Equity));
            Assert.True(created.Succeeded, created.Code);
            var account = created.Value!;
            switch (variant)
            {
                case OffsetVariant.NonPosting:
                    account = await EditAccountAsync(account, false, account.EffectiveFrom, account.EffectiveTo, "s8-offset-nonposting");
                    break;
                case OffsetVariant.Inactive:
                    var disabled = await setup.SetAccountLifecycleAsync(AccountContext(), account.Id, CompanyId, FinanceAccountLifecycle.Inactive, account.Version, "s8-offset-inactive", "s8-offset-inactive");
                    Assert.True(disabled.Succeeded, disabled.Code);
                    break;
                case OffsetVariant.Future:
                    account = await EditAccountAsync(account, account.IsPostingAccount, new DateOnly(2026, 1, 16), null, "s8-offset-future");
                    break;
                case OffsetVariant.Expired:
                    account = await EditAccountAsync(account, account.IsPostingAccount, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14), "s8-offset-expired");
                    break;
            }
            await AddOpeningRuleAsync(CashLinkedAccountId, account.Id);
        }

        internal async Task AssertNoCashOpeningEffectsAsync()
        {
            var counts = await ReadCashEvidenceCountsAsync();
            Assert.Equal(0, counts.Journals);
            Assert.Equal(0, counts.SourceEffects);
            Assert.Equal(0, counts.SettlementDocuments);
        }

        internal sealed record CashEvidenceCounts(int Journals, int SourceEffects, int SettlementDocuments);

        private FinanceRequestContext AccountContext() => Context(Tenant, Tenant.ActorId!.Value, "tenant.finance.account.manage");
        private FinanceRequestContext CalendarContext() => Context(Tenant, Tenant.ActorId!.Value, "tenant.finance.calendar.manage");
        private FinanceRequestContext RuleContext() => Context(Tenant, Tenant.ActorId!.Value, "tenant.finance.posting-rule.manage");

        private async Task<FinanceAccountRecord> LinkedAccountAsync() => Assert.Single(await setup.ListAccountsAsync(AccountContext(), CompanyId), item => item.Id == CashLinkedAccountId);

        private async Task<FinanceAccountRecord> EditAccountAsync(FinanceAccountRecord account, bool isPostingAccount, DateOnly effectiveFrom, DateOnly? effectiveTo, string key)
        {
            var edited = await setup.EditAccountAsync(AccountContext(), new FinanceAccountCommand(account.CompanyId, account.Code, account.EnglishName, account.ArabicName, account.ParentAccountId, account.AccountType, isPostingAccount, account.CurrencyBehavior, effectiveFrom, effectiveTo, account.Id, account.Version, key, key));
            Assert.True(edited.Succeeded, edited.Code);
            return edited.Value!;
        }

        private async Task AddPostingRuleDirectAsync(string sourceContract, string sourceEvent, Guid debitAccountId, Guid creditAccountId, bool costCenterRequired)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            var debit = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == debitAccountId && item.CompanyId == CompanyId);
            var credit = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == creditAccountId && item.CompanyId == CompanyId);
            var command = new FinancePostingRuleCommand(CompanyId, sourceContract, sourceEvent, debitAccountId, creditAccountId, costCenterRequired, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"s8-direct-{Guid.NewGuid():N}", $"s8-direct-{Guid.NewGuid():N}");
            db.PostingRules.Add(new FinancePostingRuleEntity(Tenant.TenantId, command.Id, command, 2, debit?.Code ?? "MISSING-DEBIT", credit?.Code ?? "MISSING-CREDIT"));
            await db.SaveChangesAsync();
        }

        internal MigrationExecutionService NewExecution(IFinanceSettlementPersistence? finance = null)
        {
            var coordinator = new MigrationCashBankOpeningExecutionCoordinator(Migration, references, finance ?? Settlement);
            return new MigrationExecutionService(foundation, Migration, Migration, Migration, scopes, scopes, new UnusedOwnerGateway(), references, null, null, null, null, coordinator);
        }

        internal FinanceMigrationCashBankOpeningCommand Command(string sourceReference, decimal amount) => new(CompanyId, CashAccountId, sourceReference, new DateOnly(2026, 1, 15), amount, "SAR", "payload", $"cash-{sourceReference}", "payload");

        internal async Task<PreparedRun> PrepareAsync(string sourceReference, decimal amount)
        {
            var objectId = Guid.NewGuid();
            var run = MigrationRun.Create(Tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(objectId, Tenant.TenantId, null, null, null, sourceHash, 1, 1);
            var intakeKey = new MigrationIdempotencyKey($"s8-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(Tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("s8-intake"), MigrationIntakeFingerprint.Version, source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(Tenant, new MigrationEvidenceReference(run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
            var persisted = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var payload = JsonSerializer.Serialize(new { companyId = CompanyId, cashAccountId = CashAccountId, sourceReference, amount, currencyCode = "SAR", openingDate = new DateOnly(2026, 1, 15) });
            var staged = new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 1, sourceReference, MigrationCanonicalRecordType.CashBankOpening, payload, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow);
            Assert.True((await Migration.StagePackageAsync(Tenant, new StageMigrationPackageCommand(persisted.RunId, objectId, sourceHash, packageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, [staged]))).Succeeded);
            var current = await TransitionAsync(persisted, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, $"s8-validation-{Guid.NewGuid():N}", "s8-validation");
            var validation = new MigrationValidationSummary(Guid.NewGuid(), Tenant.TenantId, current.RunId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.CashBankOpening, MigrationRecordDisposition.Accepted, [])], DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, validationAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "validation_completed", validationAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Validated);
            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, $"s8-dry-{Guid.NewGuid():N}", "s8-dry");
            var dryRun = new MigrationDryRunPreview(Guid.NewGuid(), Tenant.TenantId, current.RunId, dryAttempt.AttemptId, validationAttempt.AttemptId, packageHash, sourceHash, 1, 1, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, [new(staged.StagedRecordId, 1, MigrationCanonicalRecordType.CashBankOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, "cash")], DateTime.UtcNow);
            Assert.True((await Migration.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, dryAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "dry_run_completed", dryAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, staged);
        }

        internal async Task RemoveCashEvidenceAsync(string missing)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            if (missing == "source") db.SourceEffects.RemoveRange(await db.SourceEffects.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1").ToListAsync());
            else db.Journals.RemoveRange(await db.Journals.Where(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1").ToListAsync());
            await db.SaveChangesAsync();
        }

        internal async Task<CashEvidenceCounts> ReadCashEvidenceCountsAsync()
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            return new(
                await db.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1"),
                await db.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1"),
                await db.SettlementDocuments.CountAsync(item => item.CompanyId == CompanyId));
        }

        internal async Task ChangeCashLinkAsync()
        {
            var account = await setup.CreateAccountAsync(Context(Tenant, Tenant.ActorId!.Value, "tenant.finance.account.manage"), Account(CompanyId, "S8-MISMATCH", FinanceAccountType.Asset));
            Assert.True(account.Succeeded, account.Code);
            var cash = (await Settlement.ListCashAccountsAsync(Context(Tenant, Tenant.ActorId.Value, "tenant.finance.settlement.manage"), CompanyId)).Single(item => item.Id == CashAccountId);
            var changed = await Settlement.EditCashAccountAsync(Context(Tenant, Tenant.ActorId.Value, "tenant.finance.settlement.manage"), new FinanceCashAccountCommand(CompanyId, cash.Code, cash.EnglishName, cash.ArabicName, cash.Kind, cash.CurrencyCode, account.Value!.Id, cash.BankReference, cash.EffectiveFrom, cash.EffectiveTo, cash.Id, cash.Version, "s8-mismatch", "s8-mismatch"), cash.Version);
            Assert.True(changed.Succeeded, changed.Code);
        }

        internal async Task<int> CountCashEffectsAsync(string sourceReference)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            return await db.SourceEffects.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1");
        }

        internal async Task<int> CountCashJournalsAsync(string sourceReference)
        {
            await using var db = new FinanceDbContext(FinanceOptions, Tenant);
            return await db.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "migration-cash-bank-opening.v1" && item.Description == $"Cash/Bank opening {sourceReference}");
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

        internal sealed record PreparedRun(MigrationRunRecord Run, MigrationStagedRecord Staged);
    }

    private static FinanceRequestContext Context(TenantContext tenant, Guid actorId, string permission)
    {
        Assert.True(FinanceRequestContext.TryCreate(FoundationContext(tenant, actorId, permission), out var context));
        return context!;
    }

    private static FoundationRequestContext FoundationContext(TenantContext tenant, Guid actorId, string permission) => FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, permission);

    private static FinanceAccountCommand Account(Guid companyId, string code, FinanceAccountType type) => new(companyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");

    internal sealed class MutableApprovalPolicy : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Requirement { get; set; } = FinanceApprovalRequirement.NotRequired;
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) => Requirement;
    }

    private sealed class CashReferenceAuthority : IMigrationReferenceAuthority
    {
        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>([new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, "reference_active", "Reference is active.")]);
        public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) => row.Payload is MigrationCashBankOpeningPayload cash && cash.CompanyId is { } company && cash.CashAccountId is { } account && !string.IsNullOrWhiteSpace(cash.SourceReference) ? MigrationBusinessIdentityResolution.Valid($"cash-bank:{company:D}:{account:D}:{cash.SourceReference.Trim()}") : MigrationBusinessIdentityResolution.NotApplicable();
    }

    private sealed class ActiveCustomerReader(Guid customerId) : IBusinessCustomerReferenceReader
    {
        public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid requestedId, CancellationToken cancellationToken = default) => Task.FromResult<BusinessCustomerReference?>(requestedId == customerId ? new BusinessCustomerReference(customerId, tenantContext.TenantId, "CASH-CUSTOMER", MasterDataLifecycleState.Active) : null);
    }

    private sealed class StaticPrivateObjectStorage(TenantContext tenant, Guid objectId, byte[] content, MigrationSourceArtifactSnapshot source) : IPrivateObjectStorage
    {
        private readonly PrivateFileMetadata metadata = new(objectId, tenant.TenantId, TenantWorkScope.IssueFromVerifiedAuthority(tenant, TenantWorkScopeRequest.TenantWide()), "cash-opening.json", "application/json", source.Length, source.Sha256, DateTimeOffset.UnixEpoch, null, PrivateFileSafetyRequirement.TrustedGenerated);
        public ValueTask<PrivateFileMetadata> StoreAsync(TenantContext tenantContext, TenantWorkScope requestedScope, string originalFileName, string contentType, Stream content, DateTimeOffset? expiresAt = null, PrivateFileSafetyRequirement safetyRequirement = PrivateFileSafetyRequirement.ExternalScanRequired, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PrivateFileAccessResult> ReadAsync(TenantContext tenantContext, Guid requestedObjectId, CancellationToken cancellationToken = default) => tenantContext.TenantId == tenant.TenantId && requestedObjectId == objectId ? ValueTask.FromResult(PrivateFileAccessResult.AllowedResult(metadata, content)) : ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));
        public ValueTask<PrivateFileOverwriteResult> OverwriteAsync(TenantContext tenantContext, Guid requestedObjectId, long expectedConcurrencyVersion, Stream replacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TenantWideScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) => TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));
        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) => TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class UnusedOwnerGateway : IOwnerExecutionGateway
    {
        public Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(FoundationRequestContext trustedContext, OwnerImportRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(FoundationRequestContext trustedContext, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(FoundationRequestContext trustedContext, Guid batchId, byte[] expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerEvidence?> ReadEvidenceAsync(FoundationRequestContext trustedContext, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<OwnerEvidence?>(null);
    }

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
