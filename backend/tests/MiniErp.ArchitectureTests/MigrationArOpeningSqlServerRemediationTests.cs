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
public sealed class MigrationArOpeningRemediationSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
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
    public async Task Sql_server_mixed_inventory_and_ar_preflight_before_any_economic_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("AR-MIXED", 100m);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "mixed-success", prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationRunStatus.Completed, result.Value!.RunStatus);
        Assert.Equal(2, result.Value.Effects.Count);
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.InventoryOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(1, result.Value.Effects.Count(item => item.RecordType == MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.Equal(8, result.Value.Representations!.Count);

        await fixture.AssertMixedEffectsAsync("AR-MIXED", 100m);
    }

    [Fact]
    public async Task Sql_server_mixed_inventory_and_ar_preflight_failure_has_zero_economic_effect()
    {
        await using var fixture = await ArSqlFixture.CreateAsync(safety);
        await fixture.DisableArAccountAsync();
        var prepared = await fixture.PrepareMixedAsync("AR-MIXED-BLOCKED", 100m);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "mixed-preflight-failure", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("account_not_postable", result.Code);
        var run = (await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId))!;
        Assert.NotEqual(MigrationRunStatus.Executing, run.Status);
        Assert.NotEqual(MigrationRunStatus.Completed, run.Status);
        await fixture.AssertNoMixedEffectsAsync("AR-MIXED-BLOCKED");
    }

    private sealed class ArSqlFixture : IAsyncDisposable
    {
        private readonly Microsoft.Data.SqlClient.SqlConnection connection;
        private readonly MigrationFoundationService foundation;
        private readonly IMigrationReferenceAuthority references = new ArReferenceAuthority();
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
            FinanceSettlementPersistence settlement,
            IFinancePersistence finance,
            InventoryPersistence inventory,
            InventoryService inventoryService,
            InventoryValuationService valuation,
            Guid branchId,
            Guid warehouseId,
            Guid productId,
            Guid unitId,
            Guid currencyId,
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
            Settlement = settlement;
            Finance = finance;
            Inventory = inventory;
            InventoryService = inventoryService;
            Valuation = valuation;
            BranchId = branchId;
            WarehouseId = warehouseId;
            ProductId = productId;
            UnitId = unitId;
            CurrencyId = currencyId;
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
        internal FinanceSettlementPersistence Settlement { get; }
        internal IFinancePersistence Finance { get; }
        internal InventoryPersistence Inventory { get; }
        internal InventoryService InventoryService { get; }
        internal InventoryValuationService Valuation { get; }
        internal Guid BranchId { get; }
        internal Guid WarehouseId { get; }
        internal Guid ProductId { get; }
        internal Guid UnitId { get; }
        internal Guid CurrencyId { get; }
        internal Guid ArAccountId { get; private set; }
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
            var branchId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var unitId = Guid.NewGuid();
            var currencyId = Guid.NewGuid();
            var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenant.TenantId.Value, companyId, "S6 AR company", "SAR")]);
            var approval = new NoApprovalPolicy();
            var currency = new MasterDataCurrencyRecord(currencyId, tenant.TenantId, "SAR", new LocalizedName("Saudi Riyal"), MasterDataLifecycleState.Active, 1, [1]);
            var currencies = new TestCurrencyPaymentTermPersistence(currency);
            var warehouses = new ConfiguredInventoryWarehouseProvider([new InventoryWarehouseOption(tenant.TenantId.Value, companyId, branchId, warehouseId, "S6-WH", "S6 warehouse")]);
            var products = new StaticInventoryProductProvider(new InventoryProductReference(tenant.TenantId.Value, productId, "S6-SKU", "S6 product", unitId, "EA", true, true, true));
            var inventory = new InventoryPersistence(inventoryOptions);
            var valuationPersistence = new InventoryValuationPersistence(inventoryOptions, null, null, new UnavailableMasterDataExchangeRatePersistence());
            var inventoryAuthorization = new InventoryResourceAuthorizationService();
            var inventoryService = new InventoryService(inventory, inventoryAuthorization, warehouses, products);
            var valuation = new InventoryValuationService(valuationPersistence, inventoryAuthorization, warehouses, currencies);
            var setup = new FinancePersistence(financeOptions, companies, valuationPersistence, new UnavailableMasterDataExchangeRatePersistence(), approval);
            var settlement = new FinanceSettlementPersistence(financeOptions, companies, new UnavailableMasterDataExchangeRatePersistence(), new ActiveCustomerReader(customerId), new UnavailableSupplierPersistence(), new UnavailableMasterDataCurrencyPaymentTermPersistence(), new UnavailableFinanceSupplierInvoiceSourceProvider(), approval);
            var fixture = new ArSqlFixture(connection, financeOptions, inventoryOptions, migrationOptions, tenant, companyId, customerId, settlement, setup, inventory, inventoryService, valuation, branchId, warehouseId, productId, unitId, currencyId, new MigrationPersistence(migrationOptions), FoundationContext(tenant, "tenant.migration.execute"));
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
            Assert.True(inventoryRule.Succeeded, inventoryRule.Code);
            fixture.ArAccountId = arAccount.Value.Id;
            return fixture;
        }

        internal FinanceRequestContext FinanceContextFor(string permission)
        {
            Assert.True(FinanceRequestContext.TryCreate(FoundationContext(Tenant, permission), out var context));
            return context!;
        }

        internal async Task<PreparedRun> PrepareAsync(string sourceReference, decimal amount)
        {
            var objectId = Guid.NewGuid();
            var run = MigrationRun.Create(Tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(objectId, Tenant.TenantId, null, null, null, sourceHash, 1, 1);
            var intakeKey = new MigrationIdempotencyKey($"s6-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(Tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("s6-intake-fingerprint"), MigrationIntakeFingerprint.Version, source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(Tenant, new MigrationEvidenceReference(intake.Value!.Run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
            var persisted = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var json = JsonSerializer.Serialize(new { companyId = CompanyId, customerId = CustomerId, sourceReference, documentDate = new DateOnly(2026, 1, 10), openingDate = new DateOnly(2026, 1, 15), amount, currencyCode = "SAR", dueDate = new DateOnly(2026, 2, 14) });
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

        internal MigrationExecutionService NewExecution(IFinanceSettlementPersistence? finance = null)
        {
            var ar = new MigrationArOpeningExecutionCoordinator(Migration, references, finance ?? Settlement);
            return new MigrationExecutionService(foundation, Migration, Migration, Migration, scopes, scopes, new UnusedOwnerGateway(), references, null, ar);
        }

        internal MigrationExecutionService NewMixedExecution()
        {
            var ar = new MigrationArOpeningExecutionCoordinator(Migration, references, Settlement);
            var inventory = new MigrationInventoryOpeningExecutionCoordinator(Migration, references, InventoryService, Valuation, Finance);
            return new MigrationExecutionService(foundation, Migration, Migration, Migration, scopes, scopes, new UnusedOwnerGateway(), references, inventory, ar);
        }

        internal FinanceMigrationArOpeningCommand Command(string sourceReference, decimal amount) => new(CompanyId, CustomerId, sourceReference, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15), amount, "SAR", new DateOnly(2026, 2, 14), null, "payload", $"command-{sourceReference}", "payload");

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
            await using var inventoryDb = new InventoryDbContext(InventoryOptions, Tenant);
            Assert.Equal(1, await inventoryDb.StockMovements.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            Assert.Equal(1, await inventoryDb.MovementValuationEvents.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId && item.Status == InventoryValuationEventStatus.Applied));
            Assert.Equal(1, await inventoryDb.FinanceValuationHandoffs.CountAsync(item => item.CompanyId == CompanyId && item.ProductId == ProductId));
            await using var financeDb = new FinanceDbContext(FinanceOptions, Tenant);
            Assert.Equal(1, await financeDb.Journals.CountAsync(item => item.CompanyId == CompanyId && item.SourceContract == "inventory-valuation-finance.v1" && item.Status == FinanceJournalStatus.Posted));
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
        }

        internal async Task DisableArAccountAsync()
        {
            var accounts = await ((FinancePersistence)Finance).ListAccountsAsync(FinanceContextFor("tenant.finance.account.manage"), CompanyId);
            var ar = accounts.Single(item => item.Id == ArAccountId);
            var disabled = await ((FinancePersistence)Finance).SetAccountLifecycleAsync(FinanceContextFor("tenant.finance.account.manage"), ar.Id, CompanyId, FinanceAccountLifecycle.Inactive, ar.Version, "s6-disable-ar", "s6-disable-ar");
            Assert.True(disabled.Succeeded, disabled.Code);
        }

        internal async Task<PreparedRun> PrepareMixedAsync(string sourceReference, decimal amount)
        {
            var objectId = Guid.NewGuid();
            var run = MigrationRun.Create(Tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(objectId, Tenant.TenantId, null, null, null, sourceHash, 1, 1);
            var intakeKey = new MigrationIdempotencyKey($"s6-mixed-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(Tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("s6-mixed-intake-fingerprint"), MigrationIntakeFingerprint.Version, source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(Tenant, new MigrationEvidenceReference(intake.Value!.Run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
            var persisted = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var inventoryPayload = new MigrationInventoryOpeningPayload(CompanyId, BranchId, WarehouseId, ProductId, UnitId, 2m, 50m, "SAR", new DateOnly(2026, 1, 15), TrackingIdentity: "S6-MIXED-LOT", SourceLineReference: "S6-MIXED-LINE");
            var inventoryJson = JsonSerializer.Serialize(inventoryPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var arJson = JsonSerializer.Serialize(new { companyId = CompanyId, customerId = CustomerId, sourceReference, documentDate = new DateOnly(2026, 1, 10), openingDate = new DateOnly(2026, 1, 15), amount, currencyCode = "SAR", dueDate = new DateOnly(2026, 2, 14) });
            var staged = new[]
            {
                new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 1, "S6-MIXED-INVENTORY", MigrationCanonicalRecordType.InventoryOpening, inventoryJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inventoryJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow),
                new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, persisted.RunId, 2, sourceReference, MigrationCanonicalRecordType.ArOpening, arJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(arJson))), packageHash, MigrationCanonicalPackageParser.Version, objectId, sourceHash, DateTimeOffset.UtcNow)
            };
            Assert.True((await Migration.StagePackageAsync(Tenant, new StageMigrationPackageCommand(persisted.RunId, objectId, sourceHash, packageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, staged))).Succeeded);
            var current = await TransitionAsync(persisted, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, $"s6-mixed-validation-{Guid.NewGuid():N}", "s6-mixed-validation-fingerprint");
            var validationRows = staged.Select(item => new MigrationValidationRecordResult(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, [])).ToArray();
            var validation = new MigrationValidationSummary(Guid.NewGuid(), Tenant.TenantId, current.RunId, validationAttempt.AttemptId, packageHash, sourceHash, 2, 2, 0, 0, new Dictionary<string, int>(), validationRows, DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, validationAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "validation_completed", validationAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Validated);
            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, $"s6-mixed-dry-{Guid.NewGuid():N}", "s6-mixed-dry-fingerprint");
            var previewRows = staged.Select(item => new MigrationPreviewRow(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, "provider-realistic mixed test")).ToArray();
            var dryRun = new MigrationDryRunPreview(Guid.NewGuid(), Tenant.TenantId, current.RunId, dryAttempt.AttemptId, validationAttempt.AttemptId, packageHash, sourceHash, 2, 2, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, previewRows, DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun))).Succeeded);
            Assert.True((await foundation.RecordAttemptOutcomeAsync(Request, current.RunId, dryAttempt.AttemptId, MigrationAttemptOutcome.Succeeded, "dry_run_completed", dryAttempt.Version)).Succeeded);
            current = (await Migration.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, staged);
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

    private sealed class TestCurrencyPaymentTermPersistence(MasterDataCurrencyRecord currency) : IMasterDataCurrencyPaymentTermPersistence
    {
        private readonly UnavailableMasterDataCurrencyPaymentTermPersistence fallback = new();

        public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataCurrencyRecord>>([currency]);
        public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataCurrencyRecord?>(currency.Id == currencyId ? currency : null);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateCurrencyAsync(tenantContext, currencyId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditCurrencyAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetCurrencyLifecycleAsync(tenantContext, currencyId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => fallback.ListPaymentTermsAsync(tenantContext, cancellationToken);
        public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CancellationToken cancellationToken = default) => fallback.FindPaymentTermAsync(tenantContext, paymentTermId, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreatePaymentTermAsync(tenantContext, paymentTermId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditPaymentTermAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetPaymentTermLifecycleAsync(tenantContext, paymentTermId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, resourceKind, resourceId, cancellationToken);
    }

    private sealed class FaultingFinancePersistence(IFinanceSettlementPersistence inner) : IFinanceSettlementPersistence
    {
        internal bool ThrowAfterCreate { get; set; }
        internal bool HideReadback { get; set; }
        internal int CreateCalls { get; private set; }
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
        public Task<FinanceArOpeningPreflightResult> PreflightMigrationArOpeningAsync(FinanceRequestContext c, FinanceMigrationArOpeningCommand x, CancellationToken t = default) => inner.PreflightMigrationArOpeningAsync(c, x, t);
        public async Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateMigrationArOpeningAsync(FinanceRequestContext c, FinanceMigrationArOpeningCommand x, CancellationToken t = default) { CreateCalls++; var result = await inner.CreateMigrationArOpeningAsync(c, x, t); if (ThrowAfterCreate) throw new InvalidOperationException("test lost response after commit"); return result; }
        public Task<FinanceMigrationArOpeningEvidence?> ReadMigrationArOpeningAsync(FinanceRequestContext c, FinanceMigrationArOpeningCommand x, CancellationToken t = default) => HideReadback ? Task.FromResult<FinanceMigrationArOpeningEvidence?>(null) : inner.ReadMigrationArOpeningAsync(c, x, t);
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

    private sealed class NoApprovalPolicy : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) => FinanceApprovalRequirement.NotRequired;
    }

    private sealed class ArReferenceAuthority : IMigrationReferenceAuthority
    {
        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>([new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, "reference_active", "active")]);
        public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) => row.Payload is MigrationArOpeningPayload ar && ar.CompanyId is { } company && ar.CustomerId is { } customer && !string.IsNullOrWhiteSpace(ar.SourceReference) ? MigrationBusinessIdentityResolution.Valid($"ar-opening:{company:D}:{customer:D}:{ar.SourceReference.Trim()}") : MigrationBusinessIdentityResolution.NotApplicable();
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

    private sealed record PreparedRun(MigrationRunRecord Run, IReadOnlyList<MigrationStagedRecord> Staged);

    private static FoundationRequestContext FoundationContext(TenantContext tenant, string permission) => FoundationRequestContext.ForTenant(tenant.ActorId!.Value, Guid.NewGuid(), tenant, permission);
    private static FinanceAccountCommand Account(Guid companyId, string code, FinanceAccountType type) => new(companyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");
}
