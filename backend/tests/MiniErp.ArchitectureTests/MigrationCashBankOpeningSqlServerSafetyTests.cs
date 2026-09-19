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
        var dryRun = await validationService.DryRunAsync(migrationFoundationContext, runId, "cash-dry-run");
        Assert.True(dryRun.Succeeded, dryRun.Code);
        Assert.Equal(MigrationPlannedAction.Create, Assert.Single(dryRun.Value!.Rows).PlannedAction);
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
        Assert.Equal("migration_cash_bank_opening_currency_not_functional", foreign.Code);
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
        var receipt = await settlement.CreateSettlementDocumentAsync(settlementContext, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, companyId, null, customerId, cash.Value.Id, method.Value!.Id, openingDate, "SAR", 10m, null, null, null, null, null, "cash-receipt", "ordinary receipt", Guid.NewGuid(), "cash-receipt-create", "cash-receipt-create"));
        Assert.True(receipt.Succeeded, receipt.Code);
        var submitted = await settlement.TransitionSettlementDocumentAsync(settlementContext, new FinanceSettlementActionCommand(receipt.Value!.Id, receipt.Value.Version, null, "cash-receipt-submit", "cash-receipt-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var posted = await settlement.PostSettlementDocumentAsync(settlementContext, new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "cash-receipt-post", "cash-receipt-post", FinancePaymentMethodDirection.Receipt));
        Assert.True(posted.Succeeded, posted.Code);

        await using var db = new FinanceDbContext(options, tenant);
        Assert.Equal(2, await db.SourceEffects.CountAsync(item => item.SourceContract == "migration-cash-bank-opening.v1"));
        Assert.Equal(2, await db.Journals.CountAsync(item => item.SourceContract == "migration-cash-bank-opening.v1" && item.Status == FinanceJournalStatus.Posted));
        Assert.Equal(1, await db.Journals.CountAsync(item => item.Description == "Cash/Bank opening CASH-RACE"));
        Assert.Equal(1, await db.SettlementDocuments.CountAsync(item => item.Id == posted.Value!.Id));
    }

    private static FinanceRequestContext Context(TenantContext tenant, Guid actorId, string permission)
    {
        Assert.True(FinanceRequestContext.TryCreate(FoundationContext(tenant, actorId, permission), out var context));
        return context!;
    }

    private static FoundationRequestContext FoundationContext(TenantContext tenant, Guid actorId, string permission) => FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, permission);

    private static FinanceAccountCommand Account(Guid companyId, string code, FinanceAccountType type) => new(companyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");

    private sealed class MutableApprovalPolicy : IFinanceSourceApprovalPolicy
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
