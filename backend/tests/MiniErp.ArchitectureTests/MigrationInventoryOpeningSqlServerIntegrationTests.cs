using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.SqlClient;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
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
public sealed class MigrationInventoryOpeningSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    [Fact]
    public async Task Sql_server_inventory_opening_posts_stock_valuation_finance_and_reconciles_exactly()
    {
        await using var connection = await safety.OpenConnectionAsync();
        var inventoryOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.InventoryHistoryTable);
        var financeOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var migrationOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);
        var actorId = Guid.NewGuid();
        var tenant = TenantContext.ForOrdinaryMembership(
            safety.TenantA.TenantId,
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("slice5-integration"),
            actorId: actorId);
        await using (var migrationSchema = new MigrationDbContext(migrationOptions, tenant))
        {
            var migrator = migrationSchema.GetService<IMigrator>();
            await migrator.MigrateAsync("20260915053312_MESP141MasterReferenceExecution");
            await migrator.MigrateAsync();
        }
        await using (var migrationSchema = new MigrationDbContext(migrationOptions, tenant))
            Assert.Contains("20260916131448_MESP141InventoryEconomicRepresentations", await migrationSchema.Database.GetAppliedMigrationsAsync());

        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var openingDate = new DateOnly(2026, 1, 15);
        const string currencyCode = "SAR";
        const string trackingIdentity = "LOT-OPEN-001";
        const string sourceLineReference = "source-line-001";
        var currency = new MasterDataCurrencyRecord(currencyId, tenant.TenantId, currencyCode, new LocalizedName(currencyCode), MasterDataLifecycleState.Active, 1, [1]);
        var currencies = CurrencyStub(currency);
        var warehouses = new ConfiguredInventoryWarehouseProvider([new InventoryWarehouseOption(tenant.TenantId.Value, companyId, branchId, warehouseId, "WH-OPEN", "Opening warehouse")]);
        var products = new StaticInventoryProductProvider(new InventoryProductReference(tenant.TenantId.Value, productId, "SKU-OPEN", "Opening product", unitId, "EA", true, true, true));
        var authorization = new InventoryResourceAuthorizationService();
        var inventoryPersistence = new InventoryPersistence(inventoryOptions);
        var valuationPersistence = new InventoryValuationPersistence(inventoryOptions, null, null, new UnavailableMasterDataExchangeRatePersistence());
        var migration = new MigrationPersistence(migrationOptions);
        var boundaryObserver = new RunBoundaryObserver(migrationOptions, tenant);
        var observedInventoryPersistence = ForwardingProxy<IInventoryPersistence>.Create(inventoryPersistence, boundaryObserver.Observe);
        var inventoryService = new InventoryService(observedInventoryPersistence, authorization, warehouses, products);
        var valuationService = new InventoryValuationService(valuationPersistence, authorization, warehouses, currencies);

        var inventorySetup = new InventoryTenantContextResolver().Resolve(FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, "tenant.inventory.valuation.policy.create"));
        Assert.True(inventorySetup.Allowed, inventorySetup.Code);
        var policy = await valuationService.CreatePolicyAsync(
            inventorySetup.Context!,
            new InventoryValuationPolicyRequest(companyId, currencyId, currencyCode, InventoryValuationScopeMode.WarehouseProductUomTracking, openingDate, null, 2, 2, InventoryValuationRoundingMode.ToEven, "PurchaseOrderUnitPrice", "CurrentMovingAverage", "CurrentMovingAverage"),
            "migration-opening-valuation-policy");
        Assert.True(policy.Succeeded, policy.Code);

        var financeCompanyProvider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenant.TenantId.Value, companyId, "Opening company", currencyCode)]);
        var approval = new OpeningFinanceApprovalPolicy();
        var finance = new FinancePersistence(financeOptions, financeCompanyProvider, valuationPersistence, new UnavailableMasterDataExchangeRatePersistence(), approval);
        var processHandoffCalls = 0;
        var observedFinance = ForwardingProxy<IFinancePersistence>.Create(finance, (method, args) =>
        {
            boundaryObserver.Observe(method, args);
            if (method.Name == nameof(IFinancePersistence.ProcessHandoffAsync))
                Interlocked.Increment(ref processHandoffCalls);
        });
        var accountContext = FinanceContext(tenant, actorId, "tenant.finance.account.manage");
        var calendarContext = FinanceContext(tenant, actorId, "tenant.finance.calendar.manage");
        var postingRuleContext = FinanceContext(tenant, actorId, "tenant.finance.posting-rule.manage");
        var journalReadContext = FinanceContext(tenant, actorId, "tenant.finance.journal.view");
        var migrationFinanceContext = FinanceContext(tenant, actorId, "tenant.migration.execute");
        var missingPeriod = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.False(missingPeriod.Ready);
        Assert.Equal("period_not_configured", missingPeriod.Code);
        var accounts = await CreateAccountsAsync(finance, accountContext, companyId);
        var calendar = await finance.CreateCalendarAsync(calendarContext, new FinanceFiscalCalendarCommand(companyId, "Opening FY", Guid.NewGuid(), "opening-calendar", "opening-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await finance.CreateYearAsync(calendarContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "opening-year", "opening-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await finance.CreatePeriodAsync(calendarContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026", "2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "opening-period", "opening-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await finance.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "opening-period-open", "opening-period-open"));
        Assert.True(opened.Succeeded, opened.Code);
        var missingRule = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.False(missingRule.Ready);
        Assert.Equal("pending_mapping", missingRule.Code);
        var postingRuleCommand = new FinancePostingRuleCommand(companyId, "inventory-valuation-finance.v1", "OpeningBalance:Inbound", accounts.Debit.Id, accounts.Credit.Id, false, openingDate, null, Guid.NewGuid(), "opening-posting-rule", "opening-posting-rule");
        var postingRule = await finance.CreatePostingRuleAsync(postingRuleContext, postingRuleCommand);
        Assert.True(postingRule.Succeeded, postingRule.Code);
        var ready = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.True(ready.Ready, ready.Code);
        Assert.Equal(FinanceApprovalRequirement.NotRequired, ready.ApprovalRequirement);

        var softClosed = await finance.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value.Id, FinanceFiscalPeriodState.SoftClosed, "slice5 preflight test", opened.Value!.Version, "opening-period-soft-close", "opening-period-soft-close"));
        Assert.True(softClosed.Succeeded, softClosed.Code);
        var softClosedPreflight = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.False(softClosedPreflight.Ready);
        Assert.Equal("period_soft_closed", softClosedPreflight.Code);
        var closed = await finance.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value.Id, FinanceFiscalPeriodState.Closed, "slice5 preflight test", softClosed.Value!.Version, "opening-period-close", "opening-period-close"));
        Assert.True(closed.Succeeded, closed.Code);
        var closedPreflight = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.False(closedPreflight.Ready);
        Assert.Equal("period_closed", closedPreflight.Code);
        var reopened = await finance.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value.Id, FinanceFiscalPeriodState.Open, "slice5 preflight test", closed.Value!.Version, "opening-period-reopen", "opening-period-reopen"));
        Assert.True(reopened.Succeeded, reopened.Code);
        approval.Requirement = FinanceApprovalRequirement.NotConfigured;
        var approvalMissing = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.False(approvalMissing.Ready);
        Assert.Equal("approval_policy_not_configured", approvalMissing.Code);
        approval.Requirement = FinanceApprovalRequirement.NotRequired;

        var request = FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, "tenant.migration.execute");
        var foundation = new MigrationFoundationService(migration, new NoopAuditSink());
        var references = new ActiveReferenceAuthority();
        var economicCoordinator = new MigrationInventoryOpeningExecutionCoordinator(migration, references, inventoryService, valuationService, observedFinance);
        var execution = new MigrationExecutionService(
            foundation,
            migration,
            migration,
            migration,
            new TenantWideScopeResolver(),
            new TenantWideScopeResolver(),
            new UnusedOwnerGateway(),
            references,
            economicCoordinator);
        foreach (var blockedPayload in new[]
        {
            new MigrationInventoryOpeningPayload(companyId, branchId, warehouseId, productId, unitId, 1m, 2m, "USD", openingDate, TrackingIdentity: "LOT-FOREIGN", SourceLineReference: "source-line-foreign"),
            new MigrationInventoryOpeningPayload(companyId, branchId, Guid.NewGuid(), productId, unitId, 1m, 2m, currencyCode, openingDate, TrackingIdentity: "LOT-DENIED", SourceLineReference: "source-line-denied"),
            new MigrationInventoryOpeningPayload(companyId, branchId, warehouseId, productId, unitId, -1m, 2m, currencyCode, openingDate, TrackingIdentity: "LOT-QUARANTINED", SourceLineReference: "source-line-quarantined")
        })
        {
            var expectedCode = blockedPayload.CurrencyCode == "USD"
                ? "migration_inventory_opening_currency_not_functional"
                : blockedPayload.WarehouseId != warehouseId
                    ? "warehouse_not_available"
                    : "inventory_opening_row_quarantined";
            var blockedPrepared = await PrepareAsync(migration, foundation, request, tenant, [blockedPayload]);
            boundaryObserver.RunId = blockedPrepared.Run.RunId;
            var blocked = await execution.ExecuteAsync(request, blockedPrepared.Run.RunId, blockedPrepared.ExecutionKey, blockedPrepared.Run.Version);
            Assert.Equal(MigrationResultKind.KnownFailure, blocked.Kind);
            Assert.Equal(expectedCode, blocked.Code);
            Assert.Empty((await execution.ReadAsync(request, blockedPrepared.Run.RunId))!.Effects);
            Assert.Empty(await inventoryPersistence.ListMovementsAsync(new InventoryTenantContextResolver().Resolve(request).Context!, new InventoryScope(tenant.TenantId.Value, companyId, branchId, warehouseId)));
            Assert.Empty(await finance.ListJournalsAsync(journalReadContext, companyId));
        }
        MigrationInventoryOpeningPayload[] payloads =
        [
            new(companyId, branchId, warehouseId, productId, unitId, 3m, 1.005m, currencyCode, openingDate, TrackingIdentity: trackingIdentity, SourceLineReference: sourceLineReference),
            new(companyId, branchId, warehouseId, productId, unitId, 2m, 2.255m, currencyCode, openingDate, TrackingIdentity: "LOT-OPEN-002", SourceLineReference: "source-line-002")
        ];
        var prepared = await PrepareAsync(migration, foundation, request, tenant, payloads);
        boundaryObserver.RunId = prepared.Run.RunId;

        var executionReplica = new MigrationExecutionService(
            foundation,
            migration,
            migration,
            migration,
            new TenantWideScopeResolver(),
            new TenantWideScopeResolver(),
            new UnusedOwnerGateway(),
            references,
            economicCoordinator);
        var concurrentResults = await Task.WhenAll(
            execution.ExecuteAsync(request, prepared.Run.RunId, prepared.ExecutionKey, prepared.Run.Version),
            executionReplica.ExecuteAsync(request, prepared.Run.RunId, prepared.ExecutionKey, prepared.Run.Version));
        Assert.Single(concurrentResults, item => item.Kind == MigrationResultKind.Succeeded);
        Assert.Single(concurrentResults, item => item.Kind == MigrationResultKind.Replayed);
        var result = concurrentResults.Single(item => item.Kind == MigrationResultKind.Succeeded);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationRunStatus.Completed, result.Value!.RunStatus);
        Assert.Equal(MigrationAttemptOutcome.Succeeded, result.Value.AttemptOutcome);
        var effects = result.Value.Effects.ToArray();
        Assert.Equal(2, effects.Length);
        Assert.All(effects, effect =>
        {
            Assert.Equal(MigrationCanonicalRecordType.InventoryOpening, effect.RecordType);
            Assert.Equal(MigrationExecutionEffectDisposition.Committed, effect.Disposition);
        });
        var representations = result.Value.Representations!;
        Assert.Equal(12, representations.Count);
        Assert.All(effects, effect => Assert.Equal(6, representations.Count(item => item.EffectId == effect.Id)));
        Assert.Contains(representations, item => item.OwnerModule == MigrationEconomicOwnerModule.Inventory && item.Kind == MigrationEconomicRepresentationKind.InventoryStockMovement);
        Assert.Contains(representations, item => item.OwnerModule == MigrationEconomicOwnerModule.Finance && item.Kind == MigrationEconomicRepresentationKind.FinanceJournal);

        var reconciliations = result.Value.EconomicReconciliations!.OrderBy(item => item.SourceSequence).ToArray();
        Assert.Equal(2, reconciliations.Length);
        Assert.All(reconciliations, reconciliation =>
        {
            Assert.Equal("reconciled", reconciliation.Status);
            Assert.True(reconciliation.PhysicalQuantityProven);
            Assert.True(reconciliation.ValuationAmountProven);
            Assert.True(reconciliation.FinanceAmountProven);
            Assert.Equal(0m, reconciliation.DeclaredRoundingAdjustment);
            Assert.Equal(2, reconciliation.InventoryUnitCostScale);
            Assert.Equal(2, reconciliation.InventoryAmountScale);
            Assert.Equal("ToEven", reconciliation.InventoryRoundingMode);
            Assert.Equal(currencyCode, reconciliation.FunctionalCurrencyCode);
        });
        Assert.Equal(3.015m, reconciliations[0].CanonicalValue);
        Assert.Equal(3m, reconciliations[0].InventoryValue);
        Assert.Equal(3m, reconciliations[0].FinancePostedAmount);
        Assert.Equal(4.510m, reconciliations[1].CanonicalValue);
        Assert.Equal(4.52m, reconciliations[1].InventoryValue);
        Assert.Equal(4.52m, reconciliations[1].FinancePostedAmount);

        var openingContextResolution = new InventoryTenantContextResolver().Resolve(FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, "tenant.migration.execute"));
        Assert.True(openingContextResolution.Allowed);
        var openingIds = effects.Select(effect => effect.OwnerBatchId).ToArray();
        await using (var inventoryDb = new InventoryDbContext(inventoryOptions, tenant))
        {
            var movements = await inventoryDb.StockMovements.Where(item => openingIds.Contains(item.SourceDocumentId)).ToArrayAsync();
            Assert.Equal(2, movements.Length);
            foreach (var effect in effects)
            {
                var index = effect.SourceSequence - 1;
                var ownerOpening = await inventoryPersistence.FindOpeningBalanceAsync(openingContextResolution.Context!, effect.OwnerBatchId);
                var ownerRow = Assert.Single(ownerOpening!.Rows);
                Assert.Equal(payloads[index].TrackingIdentity, ownerRow.TrackingIdentity);
                Assert.Equal(payloads[index].SourceLineReference, ownerRow.SourceLineReference);
                var movement = Assert.Single(movements, item => item.SourceDocumentId == effect.OwnerBatchId);
                Assert.Equal(effect.OwnerRowId, movement.SourceLineId);
                Assert.Equal(payloads[index].TrackingIdentity, movement.TrackingIdentity);
                var eventRecord = await inventoryDb.MovementValuationEvents.SingleAsync(item => item.MovementId == movement.Id && item.Status == InventoryValuationEventStatus.Applied);
                Assert.Equal(index == 0 ? 3m : 4.52m, eventRecord.MovementValue);
                Assert.Equal(2, eventRecord.UnitCostScale);
                Assert.Equal(2, eventRecord.AmountScale);
                Assert.Equal(InventoryValuationRoundingMode.ToEven, eventRecord.RoundingMode);
                Assert.Single(await inventoryDb.FinanceValuationHandoffs.Where(item => item.MovementId == movement.Id).ToListAsync());
            }
        }
        await using (var financeDb = new FinanceDbContext(financeOptions, tenant))
        {
            var journals = await financeDb.Journals.Include(item => item.Lines).Where(item => item.SourceContract == "inventory-valuation-finance.v1").ToArrayAsync();
            Assert.Equal(2, journals.Length);
            Assert.Equal(new decimal[] { 3m, 4.52m }, journals.OrderBy(item => item.Lines.Sum(line => line.FunctionalDebit)).Select(item => item.Lines.Sum(line => line.FunctionalDebit)).ToArray());
            Assert.All(journals, journal =>
            {
                Assert.Equal(FinanceJournalStatus.Posted, journal.Status);
                Assert.Equal(FinanceJournalAmountAuthority.SourceFunctionalCurrency, journal.AmountAuthority);
                Assert.Equal(journal.Lines.Sum(item => item.FunctionalDebit), journal.Lines.Sum(item => item.FunctionalCredit));
            });
        }

        var replay = await execution.ExecuteAsync(request, prepared.Run.RunId, prepared.ExecutionKey, prepared.Run.Version);
        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        var replayMovements = await inventoryPersistence.ListMovementsAsync(openingContextResolution.Context!, new InventoryScope(tenant.TenantId.Value, companyId, branchId, warehouseId));
        var replayJournals = await finance.ListJournalsAsync(journalReadContext, companyId);
        Assert.Equal(2, replayMovements.Count);
        Assert.Equal(2, replayJournals.Count);
        await AssertRepresentationSqlConstraintsAsync(migrationOptions, tenant, safety.TenantB, representations[0]);

        var changedKey = await execution.ExecuteAsync(request, prepared.Run.RunId, "inventory-opening-changed-key", prepared.Run.Version);
        Assert.Equal(MigrationResultKind.Rejected, changedKey.Kind);
        Assert.Equal("migration_execution_approval_required", changedKey.Code);
        Assert.Equal(2, (await inventoryPersistence.ListMovementsAsync(openingContextResolution.Context!, new InventoryScope(tenant.TenantId.Value, companyId, branchId, warehouseId))).Count);
        Assert.Equal(2, (await finance.ListJournalsAsync(journalReadContext, companyId)).Count);

        approval.Requirement = FinanceApprovalRequirement.Required;
        var pendingPrepared = await PrepareAsync(
            migration,
            foundation,
            request,
            tenant,
            [new(companyId, branchId, warehouseId, productId, unitId, 1m, 4m, currencyCode, openingDate.AddDays(1), TrackingIdentity: trackingIdentity, SourceLineReference: "source-line-pending")]);
        boundaryObserver.RunId = pendingPrepared.Run.RunId;
        var pendingResult = await execution.ExecuteAsync(request, pendingPrepared.Run.RunId, pendingPrepared.ExecutionKey, pendingPrepared.Run.Version);
        Assert.Equal(MigrationResultKind.KnownFailure, pendingResult.Kind);
        var pendingRead = (await execution.ReadAsync(request, pendingPrepared.Run.RunId))!;
        Assert.Equal(MigrationRunStatus.PartiallyCompleted, pendingRead.RunStatus);
        Assert.Contains(pendingRead.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.PartialCompleted);
        var pendingReconciliation = Assert.Single(pendingRead.EconomicReconciliations!);
        Assert.True(pendingReconciliation.PhysicalQuantityProven);
        Assert.True(pendingReconciliation.ValuationAmountProven);
        Assert.False(pendingReconciliation.FinanceAmountProven);
        Assert.Equal("finance_approval_pending", pendingReconciliation.SafeCode);
        Assert.Equal(1, pendingRead.Representations!.Count(item => item.Kind == MigrationEconomicRepresentationKind.FinanceJournal && item.Status == FinanceJournalStatus.Submitted.ToString()));

        var pendingMovementCount = (await inventoryPersistence.ListMovementsAsync(openingContextResolution.Context!, new InventoryScope(tenant.TenantId.Value, companyId, branchId, warehouseId))).Count;
        var pendingJournalCount = (await finance.ListJournalsAsync(journalReadContext, companyId)).Count;
        Assert.Equal(3, pendingMovementCount);
        Assert.Equal(3, pendingJournalCount);
        var pendingReplay = await execution.ExecuteAsync(request, pendingPrepared.Run.RunId, pendingPrepared.ExecutionKey, pendingPrepared.Run.Version);
        Assert.Equal(MigrationResultKind.KnownFailure, pendingReplay.Kind);
        var currentPendingRun = (await migration.FindRunAsync(tenant, pendingPrepared.Run.RunId))!;
        var pendingResume = await execution.ExecuteAsync(request, pendingPrepared.Run.RunId, "inventory-pending-forward-resume", currentPendingRun.Version);
        Assert.Equal(MigrationResultKind.KnownFailure, pendingResume.Kind);
        var afterPendingResume = (await execution.ReadAsync(request, pendingPrepared.Run.RunId))!;
        Assert.Equal(MigrationRunStatus.PartiallyCompleted, afterPendingResume.RunStatus);
        Assert.Contains(afterPendingResume.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.PartialCompleted);
        Assert.Equal(3, (await inventoryPersistence.ListMovementsAsync(openingContextResolution.Context!, new InventoryScope(tenant.TenantId.Value, companyId, branchId, warehouseId))).Count);
        Assert.Equal(3, (await finance.ListJournalsAsync(journalReadContext, companyId)).Count);

        approval.Requirement = FinanceApprovalRequirement.NotRequired;
        var handoffBaseline = processHandoffCalls;
        var zeroPayload = new MigrationInventoryOpeningPayload(companyId, branchId, warehouseId, productId, unitId, 1m, 5m, currencyCode, openingDate, TrackingIdentity: "LOT-HANDOFF-ZERO", SourceLineReference: "source-line-handoff-zero");
        boundaryObserver.RunId = Guid.Empty;
        var zero = await PreparePostedOpeningAsync(migration, foundation, request, tenant, inventoryService, valuationService, zeroPayload);
        boundaryObserver.RunId = zero.Prepared.Run.RunId;
        await DeleteHandoffAsync(inventoryOptions, tenant, zero.Handoff.Id);
        var zeroResult = await execution.ExecuteAsync(request, zero.Prepared.Run.RunId, zero.Prepared.ExecutionKey, zero.Prepared.Run.Version);
        Assert.Equal(MigrationResultKind.KnownFailure, zeroResult.Kind);
        Assert.Equal("migration_execution_partially_completed", zeroResult.Code);
        var zeroRead = (await execution.ReadAsync(request, zero.Prepared.Run.RunId))!;
        Assert.Equal(MigrationRunStatus.PartiallyCompleted, zeroRead.RunStatus);
        Assert.Equal(MigrationAttemptOutcome.KnownFailure, zeroRead.AttemptOutcome);
        Assert.Equal(MigrationExecutionEffectDisposition.PartialCompleted, Assert.Single(zeroRead.Effects).Disposition);
        Assert.Equal(handoffBaseline, processHandoffCalls);
        var zeroJournals = await finance.ListJournalsAsync(journalReadContext, companyId);
        Assert.DoesNotContain(zeroJournals, item => item.SourceEvidenceId == zero.Handoff.ValuationEvidenceId);

        var multiplePayload = new MigrationInventoryOpeningPayload(companyId, branchId, warehouseId, productId, unitId, 1m, 6m, currencyCode, openingDate, TrackingIdentity: "LOT-HANDOFF-MULTIPLE", SourceLineReference: "source-line-handoff-multiple");
        boundaryObserver.RunId = Guid.Empty;
        var multiple = await PreparePostedOpeningAsync(migration, foundation, request, tenant, inventoryService, valuationService, multiplePayload);
        boundaryObserver.RunId = multiple.Prepared.Run.RunId;
        var duplicateHandoffId = await InsertDuplicateHandoffAsync(inventoryOptions, tenant, multiple.Handoff.Id);
        try
        {
            var multipleResult = await execution.ExecuteAsync(request, multiple.Prepared.Run.RunId, multiple.Prepared.ExecutionKey, multiple.Prepared.Run.Version);
            Assert.Equal(MigrationResultKind.UnknownOutcome, multipleResult.Kind);
            Assert.Equal("inventory_finance_handoff_evidence_ambiguous", multipleResult.Code);
            var multipleRead = (await execution.ReadAsync(request, multiple.Prepared.Run.RunId))!;
            Assert.Equal(MigrationRunStatus.OutcomeUnknown, multipleRead.RunStatus);
            Assert.Equal(MigrationAttemptOutcome.UnknownOutcome, multipleResult.Value!.AttemptOutcome);
            Assert.Equal(MigrationExecutionEffectDisposition.Unknown, Assert.Single(multipleRead.Effects).Disposition);
            Assert.Equal(handoffBaseline, processHandoffCalls);

            var multipleReplay = await execution.ExecuteAsync(request, multiple.Prepared.Run.RunId, multiple.Prepared.ExecutionKey, multiple.Prepared.Run.Version);
            Assert.Equal(MigrationResultKind.UnknownOutcome, multipleReplay.Kind);
            Assert.Equal(handoffBaseline, processHandoffCalls);
            var multipleRun = (await migration.FindRunAsync(tenant, multiple.Prepared.Run.RunId))!;
            var differentKey = await execution.ExecuteAsync(request, multiple.Prepared.Run.RunId, "inventory-opening-multiple-different-key", multipleRun.Version);
            Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
            Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
            Assert.Equal(handoffBaseline, processHandoffCalls);
            Assert.Single(await inventoryService.ListOpeningMovementsForMigrationAsync(request, multiple.Opening));
            Assert.Equal(2, (await valuationService.ReadOpeningEvidenceForMigrationAsync(request, new InventoryScope(tenant.TenantId.Value, companyId, branchId, warehouseId), productId, unitId, multiple.OwnerRow.TrackingIdentity))!.Value.Handoffs.Count(item => item.MovementId == multiple.Movement.Id));
            var multipleJournals = await finance.ListJournalsAsync(journalReadContext, companyId);
            Assert.DoesNotContain(multipleJournals, item => item.SourceEvidenceId == multiple.Handoff.ValuationEvidenceId);
        }
        finally
        {
            await RestoreHandoffIndexAsync(inventoryOptions, tenant, duplicateHandoffId);
        }

        var preparationObservations = boundaryObserver.Observations.Where(item => item.Method is
            nameof(IInventoryPersistence.CreateOpeningBalanceAsync)
            or nameof(IInventoryPersistence.ValidateOpeningBalanceAsync)).ToArray();
        Assert.NotEmpty(preparationObservations);
        Assert.All(preparationObservations, item => Assert.True(item.Status is MigrationRunStatus.Approved or MigrationRunStatus.PartiallyCompleted));
        Assert.Contains(boundaryObserver.Observations, item => item.Method == nameof(IFinancePersistence.PreflightInventoryOpeningAsync)
            && (item.Status is MigrationRunStatus.Approved or MigrationRunStatus.PartiallyCompleted));
        var postObservations = boundaryObserver.Observations.Where(item => item.Method == nameof(IInventoryPersistence.PostOpeningBalanceAsync)).ToArray();
        Assert.NotEmpty(postObservations);
        Assert.All(postObservations, item => Assert.Equal(MigrationRunStatus.Executing, item.Status));

        var inactiveDebit = await finance.SetAccountLifecycleAsync(accountContext, accounts.Debit.Id, companyId, FinanceAccountLifecycle.Inactive, accounts.Debit.Version, "opening-account-disable", "opening-account-disable");
        Assert.True(inactiveDebit.Succeeded, inactiveDebit.Code);
        var nonPostableAccount = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.False(nonPostableAccount.Ready);
        Assert.Equal("account_not_postable", nonPostableAccount.Code);
        var activeDebit = await finance.SetAccountLifecycleAsync(accountContext, accounts.Debit.Id, companyId, FinanceAccountLifecycle.Active, inactiveDebit.Value!.Version, "opening-account-enable", "opening-account-enable");
        Assert.True(activeDebit.Succeeded, activeDebit.Code);

        // The owner API rejects overlapping rules. Insert a duplicate only in this disposable test database to prove preflight also fails closed on legacy/corrupt ambiguity.
        await using (var financeDb = new FinanceDbContext(financeOptions, tenant))
        {
            financeDb.PostingRules.Add(new FinancePostingRuleEntity(
                tenant.TenantId,
                Guid.NewGuid(),
                postingRuleCommand with { Id = Guid.NewGuid(), IdempotencyKey = "legacy-ambiguous-rule", RequestFingerprint = "legacy-ambiguous-rule" },
                postingRule.Value!.VersionNumber + 1,
                accounts.Debit.Code,
                accounts.Credit.Code));
            await financeDb.SaveChangesAsync();
        }
        var ambiguousRule = await finance.PreflightInventoryOpeningAsync(migrationFinanceContext, companyId, openingDate, CancellationToken.None);
        Assert.False(ambiguousRule.Ready);
        Assert.Equal("ambiguous_mapping", ambiguousRule.Code);
        Assert.Equal(5, (await inventoryPersistence.ListMovementsAsync(openingContextResolution.Context!, new InventoryScope(tenant.TenantId.Value, companyId, branchId, warehouseId))).Count);
        Assert.Equal(3, (await finance.ListJournalsAsync(journalReadContext, companyId)).Count);
    }

    private sealed record PostedOpening(
        (MigrationRunRecord Run, string ExecutionKey) Prepared,
        InventoryOpeningBalanceRecord Opening,
        InventoryOpeningBalanceRowRecord OwnerRow,
        InventoryMovementRecord Movement,
        InventoryFinanceValuationHandoffRecord Handoff);

    private static async Task<PostedOpening> PreparePostedOpeningAsync(
        MigrationPersistence migration,
        MigrationFoundationService foundation,
        FoundationRequestContext request,
        TenantContext tenant,
        InventoryService inventory,
        InventoryValuationService valuation,
        MigrationInventoryOpeningPayload payload)
    {
        var prepared = await PrepareAsync(migration, foundation, request, tenant, [payload]);
        var staged = Assert.Single(await migration.ListStagedRecordsAsync(tenant, prepared.Run.RunId));
        var ownerId = StableId($"inventory-opening:{tenant.TenantId.Value:D}:{prepared.Run.RunId:D}:{staged.StagedRecordId:D}");
        var ownerRowId = StableId($"inventory-opening-row:{tenant.TenantId.Value:D}:{prepared.Run.RunId:D}:{staged.StagedRecordId:D}");
        var requestModel = new InventoryOpeningBalanceCreateRequest(
            payload.CompanyId!.Value,
            payload.BranchId,
            payload.WarehouseId!.Value,
            payload.OpeningDate!.Value,
            "Migration",
            "MESP-141",
            prepared.Run.CreatedAt,
            $"{prepared.Run.RunId:D}/{staged.StagedRecordId:D}",
            [new InventoryOpeningBalanceRowRequest(
                payload.ProductId!.Value,
                payload.UnitOfMeasureId!.Value,
                payload.Quantity!.Value,
                payload.UnitCost!.Value,
                payload.CurrencyCode!,
                payload.TrackingIdentity,
                payload.SourceLineReference)]);
        var created = await inventory.CreateOpeningBalanceForMigrationAsync(request, requestModel, ownerId, ownerRowId, $"migration-opening:{prepared.Run.RunId:N}:create", CancellationToken.None);
        Assert.True(created.Succeeded, created.Code);
        var opening = created.Value!;
        var validated = await inventory.ValidateOpeningBalanceForMigrationAsync(request, opening.Id, $"migration-opening:{prepared.Run.RunId:N}:validate");
        Assert.True(validated.Succeeded, validated.Code);
        opening = validated.Value!;
        var posted = await inventory.PostOpeningBalanceForMigrationAsync(request, opening.Id, $"migration-opening:{prepared.Run.RunId:N}:post");
        Assert.True(posted.Succeeded, posted.Code);
        opening = posted.Value!;
        var ownerRow = Assert.Single(opening.Rows);
        var scope = new InventoryScope(tenant.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId);
        var movement = Assert.Single(await inventory.ListOpeningMovementsForMigrationAsync(request, opening));
        var valued = await valuation.ProcessOpeningMovementsForMigrationAsync(
            request,
            scope,
            movement.ProductId,
            movement.UnitOfMeasureId,
            [movement.Id],
            $"migration-opening:{prepared.Run.RunId:N}:valuation",
            new DateTimeOffset(opening.AsOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            MigrationFingerprintEncoder.Compute("migration-inventory-opening-valuation-v1", prepared.Run.RunId.ToString("D"), staged.StagedRecordId.ToString("D"), movement.Id.ToString("D")));
        Assert.True(valued.Succeeded, valued.Code);
        var evidence = (await valuation.ReadOpeningEvidenceForMigrationAsync(request, scope, movement.ProductId, movement.UnitOfMeasureId, movement.TrackingIdentity))!.Value;
        var handoff = Assert.Single(evidence.Handoffs, item => item.MovementId == movement.Id);
        return new(prepared, opening, ownerRow, movement, handoff);
    }

    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);

    private static async Task DeleteHandoffAsync(DbContextOptions options, TenantContext tenant, Guid handoffId)
    {
        await using var db = new InventoryDbContext(options, tenant);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [inventory].[FinanceValuationHandoffs] WHERE [TenantId] = {tenant.TenantId.Value} AND [Id] = {handoffId}");
    }

    private static async Task<Guid> InsertDuplicateHandoffAsync(DbContextOptions options, TenantContext tenant, Guid sourceHandoffId)
    {
        var duplicateId = Guid.NewGuid();
        var indexesDropped = false;
        await using var db = new InventoryDbContext(options, tenant);
        try
        {
            // Production uniqueness is the normal protection. This disposable LocalDB-only setup drops the two natural-key indexes to model legacy corruption/race residue; it never bypasses production constraints.
            await db.Database.ExecuteSqlRawAsync("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[inventory].[FinanceValuationHandoffs]') AND name = N'IX_FinanceValuationHandoffs_TenantId_CompanyId_BranchId_WarehouseId_LedgerSequence')
                    DROP INDEX [IX_FinanceValuationHandoffs_TenantId_CompanyId_BranchId_WarehouseId_LedgerSequence] ON [inventory].[FinanceValuationHandoffs];
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[inventory].[FinanceValuationHandoffs]') AND name = N'IX_FinanceValuationHandoffs_TenantId_MovementId')
                    DROP INDEX [IX_FinanceValuationHandoffs_TenantId_MovementId] ON [inventory].[FinanceValuationHandoffs];
                """);
            indexesDropped = true;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [inventory].[FinanceValuationHandoffs]
                    ([Id], [TenantId], [CompanyId], [BranchId], [WarehouseId], [MovementId], [LedgerSequence], [SourceType], [SourceDocumentId], [SourceLineId], [ValuationEvidenceId], [ValuationEvidenceVersion], [Quantity], [Direction], [BaseUnitCost], [BaseAmount], [SignedBaseAmount], [RoundingAdjustmentAmount], [PolicyId], [PolicyVersionNumber], [FunctionalCurrencyCode], [TransactionUnitCost], [TransactionCurrencyCode], [ExchangeRateId], [ExchangeRateVersionId], [ExchangeRateVersionNumber], [ExchangeRate], [ExchangeRateScale], [ExchangeRateProvenance], [ProductId], [UnitOfMeasureId], [TrackingIdentity], [CorrectionOfMovementId], [Status], [ContractVersion], [CorrelationId], [AsOf], [CreatedAt])
                SELECT
                    {duplicateId}, [TenantId], [CompanyId], [BranchId], [WarehouseId], [MovementId], [LedgerSequence], [SourceType], [SourceDocumentId], [SourceLineId], [ValuationEvidenceId], [ValuationEvidenceVersion], [Quantity], [Direction], [BaseUnitCost], [BaseAmount], [SignedBaseAmount], [RoundingAdjustmentAmount], [PolicyId], [PolicyVersionNumber], [FunctionalCurrencyCode], [TransactionUnitCost], [TransactionCurrencyCode], [ExchangeRateId], [ExchangeRateVersionId], [ExchangeRateVersionNumber], [ExchangeRate], [ExchangeRateScale], [ExchangeRateProvenance], [ProductId], [UnitOfMeasureId], [TrackingIdentity], [CorrectionOfMovementId], [Status], [ContractVersion], [CorrelationId], [AsOf], [CreatedAt]
                FROM [inventory].[FinanceValuationHandoffs]
                WHERE [TenantId] = {tenant.TenantId.Value} AND [Id] = {sourceHandoffId}
                """);
            return duplicateId;
        }
        catch
        {
            if (indexesDropped)
                await RestoreHandoffIndexAsync(options, tenant, duplicateId);
            throw;
        }
    }

    private static async Task RestoreHandoffIndexAsync(DbContextOptions options, TenantContext tenant, Guid duplicateHandoffId)
    {
        await using var db = new InventoryDbContext(options, tenant);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [inventory].[FinanceValuationHandoffs] WHERE [TenantId] = {tenant.TenantId.Value} AND [Id] = {duplicateHandoffId}");
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[inventory].[FinanceValuationHandoffs]') AND name = N'IX_FinanceValuationHandoffs_TenantId_CompanyId_BranchId_WarehouseId_LedgerSequence')
                CREATE UNIQUE INDEX [IX_FinanceValuationHandoffs_TenantId_CompanyId_BranchId_WarehouseId_LedgerSequence] ON [inventory].[FinanceValuationHandoffs] ([TenantId], [CompanyId], [BranchId], [WarehouseId], [LedgerSequence]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[inventory].[FinanceValuationHandoffs]') AND name = N'IX_FinanceValuationHandoffs_TenantId_MovementId')
                CREATE UNIQUE INDEX [IX_FinanceValuationHandoffs_TenantId_MovementId] ON [inventory].[FinanceValuationHandoffs] ([TenantId], [MovementId]);
            """);
    }

    private static async Task AssertRepresentationSqlConstraintsAsync(
        DbContextOptions options,
        TenantContext tenant,
        TenantContext otherTenant,
        MigrationEconomicRepresentationRecord existing)
    {
        await using (var db = new MigrationDbContext(options, tenant))
        {
            var duplicate = await Assert.ThrowsAsync<SqlException>(() => InsertRepresentationAsync(db, Guid.NewGuid(), existing.TenantId.Value, existing.RunId, existing.AttemptId, existing.EffectId, existing));
            Assert.True(duplicate.Number is 2601 or 2627, $"Expected natural-identity uniqueness violation, received SQL error {duplicate.Number}.");
        }

        await using (var db = new MigrationDbContext(options, otherTenant))
        {
            var crossTenant = await Assert.ThrowsAsync<SqlException>(() => InsertRepresentationAsync(db, Guid.NewGuid(), otherTenant.TenantId.Value, existing.RunId, existing.AttemptId, existing.EffectId, existing));
            Assert.Equal(547, crossTenant.Number);
        }

        await using (var db = new MigrationDbContext(options, tenant))
        {
            var orphan = await Assert.ThrowsAsync<SqlException>(() => InsertRepresentationAsync(db, Guid.NewGuid(), tenant.TenantId.Value, existing.RunId, existing.AttemptId, Guid.NewGuid(), existing));
            Assert.Equal(547, orphan.Number);
        }
    }

    private static Task<int> InsertRepresentationAsync(
        MigrationDbContext db,
        Guid id,
        Guid tenantId,
        Guid runId,
        Guid attemptId,
        Guid effectId,
        MigrationEconomicRepresentationRecord template) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [migration].[MigrationEconomicRepresentations]
                ([Id], [TenantId], [RunId], [AttemptId], [EffectId], [OwnerModule], [Kind], [OwnerId], [OwnerReference], [Status], [EvidenceVersion], [OccurredAt], [RecordedAt], [EvidenceConfirmed])
            VALUES
                ({id}, {tenantId}, {runId}, {attemptId}, {effectId}, {(int)template.OwnerModule}, {(int)template.Kind}, {template.OwnerId}, {template.OwnerReference}, {template.Status}, {template.EvidenceVersion}, {template.OccurredAt}, {template.RecordedAt}, {template.EvidenceConfirmed})
            """);

    private static async Task<(MigrationRunRecord Run, string ExecutionKey)> PrepareAsync(
        MigrationPersistence persistence,
        MigrationFoundationService foundation,
        FoundationRequestContext request,
        TenantContext tenant,
        IReadOnlyList<MigrationInventoryOpeningPayload> payloads)
    {
        const string packageHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        const string sourceHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
        var source = new MigrationSourceArtifactSnapshot(Guid.NewGuid(), tenant.TenantId, null, null, null, sourceHash, 1, 1);
        var run = MigrationRun.Create(tenant, new MigrationDefinitionReference("tenant-onboarding.foundation", "1"), new MigrationSourceProfileReference("neutral-source-profile", "1"));
        var intakeKey = new MigrationIdempotencyKey($"inventory-intake-{Guid.NewGuid():N}");
        var intake = await persistence.CreateIntakeAsync(tenant, new CreateMigrationIntakeCommand(run, MigrationOperationKind.Validation, intakeKey, new MigrationRequestFingerprint("inventory-intake-fingerprint"), MigrationIntakeFingerprint.Version, source));
        Assert.True(intake.Succeeded, intake.Code);
        Assert.True((await persistence.SetEvidenceStateAsync(tenant, new MigrationEvidenceReference(run.RunId, MigrationOperationKind.Validation, intakeKey.Value), true)).Succeeded);
        var staged = payloads.Select((payload, index) =>
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new MigrationStagedRecord(Guid.NewGuid(), tenant.TenantId, run.RunId, index + 1, $"inventory-opening-{index + 1}", MigrationCanonicalRecordType.InventoryOpening, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), packageHash, MigrationCanonicalPackageParser.Version, source.ObjectId, sourceHash, DateTimeOffset.UtcNow);
        }).ToArray();
        Assert.True((await persistence.StagePackageAsync(tenant, new StageMigrationPackageCommand(run.RunId, source.ObjectId, sourceHash, packageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, staged))).Succeeded);

        var current = await TransitionAsync(foundation, request, (await persistence.FindRunAsync(tenant, run.RunId))!, MigrationRunStatus.Prepared);
        current = await TransitionAsync(foundation, request, current, MigrationRunStatus.Validating);
        var validationAttempt = (await foundation.StartAttemptAsync(request, run.RunId, MigrationOperationKind.Validation, $"inventory-validation-{run.RunId:N}", $"inventory-validation-{run.RunId:N}-fingerprint")).Value!;
        var validationRows = staged.Select(item => new MigrationValidationRecordResult(item.StagedRecordId, item.SourceSequence, MigrationCanonicalRecordType.InventoryOpening, MigrationRecordDisposition.Accepted, [])).ToArray();
        var validation = new MigrationValidationSummary(Guid.NewGuid(), tenant.TenantId, run.RunId, validationAttempt.AttemptId, packageHash, sourceHash, staged.Length, staged.Length, 0, 0, new Dictionary<string, int>(), validationRows, DateTimeOffset.UtcNow);
        Assert.True((await persistence.SaveValidationAsync(tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
        current = await CompleteAttemptAsync(foundation, persistence, request, current, validationAttempt, "validation_completed");
        current = await TransitionAsync(foundation, request, current, MigrationRunStatus.Validated);

        var dryRunAttempt = (await foundation.StartAttemptAsync(request, run.RunId, MigrationOperationKind.DryRun, $"inventory-dry-run-{run.RunId:N}", $"inventory-dry-run-{run.RunId:N}-fingerprint")).Value!;
        var previewRows = staged.Select(item => new MigrationPreviewRow(item.StagedRecordId, item.SourceSequence, MigrationCanonicalRecordType.InventoryOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, "would be evaluated by the owning module")).ToArray();
        var preview = new MigrationDryRunPreview(Guid.NewGuid(), tenant.TenantId, run.RunId, dryRunAttempt.AttemptId, validationAttempt.AttemptId, packageHash, sourceHash, staged.Length, staged.Length, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, previewRows, DateTimeOffset.UtcNow);
        Assert.True((await persistence.SaveDryRunAsync(tenant, new SaveMigrationDryRunCommand(preview))).Succeeded);
        current = await CompleteAttemptAsync(foundation, persistence, request, current, dryRunAttempt, "dry_run_completed");
        current = await TransitionAsync(foundation, request, current, MigrationRunStatus.Approved);
        return (current, $"inventory-execution-{Guid.NewGuid():N}");
    }

    private static async Task<MigrationRunRecord> TransitionAsync(MigrationFoundationService foundation, FoundationRequestContext request, MigrationRunRecord current, MigrationRunStatus target)
    {
        var result = await foundation.TransitionRunAsync(request, current.RunId, target, current.Version);
        Assert.True(result.Succeeded, result.Code);
        return result.Value!;
    }

    private static async Task<MigrationRunRecord> CompleteAttemptAsync(MigrationFoundationService foundation, MigrationPersistence persistence, FoundationRequestContext request, MigrationRunRecord current, MigrationAttemptRecord attempt, string code)
    {
        var result = await foundation.RecordAttemptOutcomeAsync(request, current.RunId, attempt.AttemptId, MigrationAttemptOutcome.Succeeded, code, attempt.Version);
        Assert.True(result.Succeeded, result.Code);
        return (await persistence.FindRunAsync(request.TenantContext!, current.RunId))!;
    }

    private static async Task<(FinanceAccountRecord Debit, FinanceAccountRecord Credit)> CreateAccountsAsync(IFinancePersistence finance, FinanceRequestContext context, Guid companyId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var debit = await finance.CreateAccountAsync(context, new FinanceAccountCommand(companyId, $"op-d-{suffix}", "Opening debit", null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"opening-debit-{suffix}", $"opening-debit-{suffix}"));
        var credit = await finance.CreateAccountAsync(context, new FinanceAccountCommand(companyId, $"op-c-{suffix}", "Opening credit", null, null, FinanceAccountType.Revenue, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"opening-credit-{suffix}", $"opening-credit-{suffix}"));
        Assert.True(debit.Succeeded, debit.Code);
        Assert.True(credit.Succeeded, credit.Code);
        return (debit.Value!, credit.Value!);
    }

    private static FinanceRequestContext FinanceContext(TenantContext tenant, Guid actorId, string permission)
    {
        Assert.True(FinanceRequestContext.TryCreate(FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, permission), out var context));
        return context!;
    }

    private class ForwardingProxy<T> : DispatchProxy where T : class
    {
        private T inner = null!;
        private Action<MethodInfo, object?[]?> before = (_, _) => { };

        internal static T Create(T inner, Action<MethodInfo, object?[]?> before)
        {
            var proxy = DispatchProxy.Create<T, ForwardingProxy<T>>();
            var state = (ForwardingProxy<T>)(object)proxy;
            state.inner = inner;
            state.before = before;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            before(targetMethod, args);
            try
            {
                return targetMethod.Invoke(inner, args);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw();
                throw;
            }
        }
    }

    private sealed class RunBoundaryObserver(DbContextOptions options, TenantContext tenant)
    {
        internal Guid RunId { get; set; }
        internal List<(string Method, MigrationRunStatus Status)> Observations { get; } = [];

        internal void Observe(MethodInfo method, object?[]? _)
        {
            if (RunId == Guid.Empty || method.Name is not (
                nameof(IFinancePersistence.PreflightInventoryOpeningAsync)
                or nameof(IInventoryPersistence.CreateOpeningBalanceAsync)
                or nameof(IInventoryPersistence.ValidateOpeningBalanceAsync)
                or nameof(IInventoryPersistence.PostOpeningBalanceAsync)))
                return;

            using var db = new MigrationDbContext(options, tenant);
            var status = db.Runs.AsNoTracking()
                .Where(item => item.RunId == RunId)
                .Select(item => item.Status)
                .Single();
            lock (Observations)
                Observations.Add((method.Name, status));
        }
    }

    private static IMasterDataCurrencyPaymentTermPersistence CurrencyStub(MasterDataCurrencyRecord currency) => Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name == "FindCurrencyAsync" ? currency : null);

    private static T Stub<T>(Func<MethodInfo, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private class TestProxy : DispatchProxy
    {
        internal Func<MethodInfo, object?> Handler { get; set; } = _ => null;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            var value = Handler(targetMethod);
            return targetMethod.ReturnType.IsGenericType && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                ? typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]).Invoke(null, [value])
                : targetMethod.ReturnType == typeof(Task) ? Task.CompletedTask : value;
        }
    }

    private sealed class StaticInventoryProductProvider(InventoryProductReference product) : IInventoryProductProvider
    {
        public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryProductReference?>(product.ProductId == productId && product.TenantId == context.TenantId.Value ? product : null);
    }

    private sealed class OpeningFinanceApprovalPolicy : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Requirement { get; set; } = FinanceApprovalRequirement.NotRequired;

        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) => Requirement;
    }

    private sealed class ActiveReferenceAuthority : IMigrationReferenceAuthority
    {
        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>([new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, "reference_active", "Reference is active.")]);
    }

    private sealed class UnusedOwnerGateway : IOwnerExecutionGateway
    {
        public Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(FoundationRequestContext context, OwnerImportRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(FoundationRequestContext context, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(FoundationRequestContext context, Guid batchId, byte[] expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerEvidence?> ReadEvidenceAsync(FoundationRequestContext context, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<OwnerEvidence?>(null);
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
}
