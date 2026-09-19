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
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationApOpeningSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    [Fact]
    public async Task Sql_server_ap_opening_is_finance_owned_replay_safe_and_settleable()
    {
        await using var connection = await safety.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var migrationOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);
        var actorId = Guid.NewGuid();
        var tenant = TenantContext.ForOrdinaryMembership(safety.TenantA.TenantId, new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("ap-sql"), actorId: actorId);
        var companyId = Guid.NewGuid();
        var supplierId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var documentDate = new DateOnly(2026, 1, 10);
        var openingDate = new DateOnly(2026, 1, 15);
        var dueDate = new DateOnly(2026, 2, 14);
        var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenant.TenantId.Value, companyId, "AP opening company", "SAR")]);
        var approval = new MutableApprovalPolicy();
        var setup = new FinancePersistence(options, companies, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence(), approval);
        var settlement = new FinanceSettlementPersistence(options, companies, new UnavailableMasterDataExchangeRatePersistence(), new UnavailableCustomerReferenceReader(), new ActiveSupplierReader(supplierId), new UnavailableMasterDataCurrencyPaymentTermPersistence(), new UnavailableFinanceSupplierInvoiceSourceProvider(), approval);
        var accountContext = Context(tenant, actorId, "tenant.finance.account.manage");
        var calendarContext = Context(tenant, actorId, "tenant.finance.calendar.manage");
        var ruleContext = Context(tenant, actorId, "tenant.finance.posting-rule.manage");
        var migrationContext = Context(tenant, actorId, "tenant.migration.execute");
        var foundationContext = FoundationContext(tenant, actorId, "tenant.migration.execute");
        var settlementContext = Context(tenant, actorId, "tenant.finance.settlement.manage");
        var readContext = Context(tenant, actorId, "tenant.finance.journal.view");

        var apAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "AP-OPENING", FinanceAccountType.Liability));
        var offsetAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "AP-OPENING-OFFSET", FinanceAccountType.Equity));
        var cashAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "AP-OPENING-CASH", FinanceAccountType.Asset));
        Assert.True(apAccount.Succeeded, apAccount.Code);
        Assert.True(offsetAccount.Succeeded, offsetAccount.Code);
        Assert.True(cashAccount.Succeeded, cashAccount.Code);

        var calendar = await setup.CreateCalendarAsync(calendarContext, new FinanceFiscalCalendarCommand(companyId, "AP FY", Guid.NewGuid(), "ap-calendar", "ap-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await setup.CreateYearAsync(calendarContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "ap-year", "ap-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await setup.CreatePeriodAsync(calendarContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "ap-period", "ap-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await setup.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "ap-period-open", "ap-period-open"));
        Assert.True(opened.Succeeded, opened.Code);

        var apRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "migration-ap-opening.v1", "recognition", offsetAccount.Value!.Id, apAccount.Value!.Id, false, openingDate, null, Guid.NewGuid(), "ap-rule", "ap-rule"));
        var paymentRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "supplier-payment.v1", "on-account", apAccount.Value!.Id, cashAccount.Value!.Id, false, openingDate, null, Guid.NewGuid(), "payment-rule", "payment-rule"));
        var allocationRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "supplier-payment.v1", "allocation", apAccount.Value!.Id, cashAccount.Value!.Id, false, openingDate, null, Guid.NewGuid(), "allocation-rule", "allocation-rule"));
        Assert.True(apRule.Succeeded, apRule.Code);
        Assert.True(paymentRule.Succeeded, paymentRule.Code);
        Assert.True(allocationRule.Succeeded, allocationRule.Code);

        var migration = new MigrationPersistence(migrationOptions);
        var foundation = new MigrationFoundationService(migration, new NoopAuditSink());
        var scopes = new TenantWideScopeResolver();
        var references = new ApReferenceAuthority();
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
                    sourceRecordId = "ap-1",
                    recordType = "ApOpening",
                    payload = new { companyId, supplierId, sourceReference = " AP-OPEN-001 ", documentDate, openingDate, amount = 100m, currencyCode = "sar", dueDate }
                }
            }
        });
        var source = new MigrationSourceArtifactSnapshot(objectId, tenant.TenantId, null, null, null, sourceHash, 1, 1);
        var storage = new StaticPrivateObjectStorage(tenant, objectId, package, source);
        var definition = new MigrationDefinitionReference("tenant-onboarding.foundation", "1");
        var profile = new MigrationSourceProfileReference("neutral-source-profile", "1");
        var intake = await new MigrationIntakeService(migration, storage, scopes, new NoopAuditSink()).RegisterAsync(foundationContext, new MigrationIntakeRegistrationRequest(definition, profile, MigrationOperationKind.Validation, objectId), "ap-intake");
        Assert.True(intake.Succeeded, intake.Code);
        var runId = intake.Value!.Run.RunId;
        var validationService = new MigrationValidationService(foundation, migration, storage, scopes, references, scopes);
        var validation = await validationService.ValidateAsync(foundationContext, runId, "ap-validation");
        Assert.True(validation.Succeeded, validation.Code);
        Assert.Equal(1, validation.Value!.AcceptedCount);
        var dryRun = await validationService.DryRunAsync(foundationContext, runId, "ap-dry-run");
        Assert.True(dryRun.Succeeded, dryRun.Code);
        Assert.Equal(MigrationPlannedAction.Create, Assert.Single(dryRun.Value!.Rows).PlannedAction);
        var approved = await foundation.TransitionRunAsync(foundationContext, runId, MigrationRunStatus.Approved, (await migration.FindRunAsync(tenant, runId))!.Version);
        Assert.True(approved.Succeeded, approved.Code);

        var apCoordinator = new MigrationApOpeningExecutionCoordinator(migration, references, settlement);
        var execution = new MigrationExecutionService(foundation, migration, migration, migration, scopes, scopes, new UnusedOwnerGateway(), references, null, null, apCoordinator);
        var executed = await execution.ExecuteAsync(foundationContext, runId, "ap-execution", approved.Value!.Version);
        Assert.True(executed.Succeeded, executed.Code);
        Assert.Equal(MigrationRunStatus.Completed, executed.Value!.RunStatus);
        Assert.Single(executed.Value.Effects, item => item.RecordType == MigrationCanonicalRecordType.ApOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed);
        Assert.Equal(2, executed.Value.Representations!.Count);
        Assert.Equal("reconciled", Assert.Single(executed.Value.ApEconomicReconciliations!).Status);

        var staged = Assert.Single(await migration.ListStagedRecordsAsync(tenant, runId, 0, 100));
        var command = new FinanceMigrationApOpeningCommand(companyId, supplierId, " AP-OPEN-001 ", documentDate, openingDate, 100m, "sar", dueDate, null, staged.PayloadHash, "ap-create", staged.PayloadHash);
        var ready = await settlement.PreflightMigrationApOpeningAsync(migrationContext, command);
        Assert.True(ready.Ready, ready.Code);
        var created = await settlement.CreateMigrationApOpeningAsync(migrationContext, command);
        Assert.True(created.Succeeded, created.Code);
        var evidence = await settlement.ReadMigrationApOpeningAsync(migrationContext, command);
        Assert.NotNull(evidence);
        Assert.Equal(created.Value!.Id, evidence.OpenItem.Id);
        Assert.Equal(FinanceJournalStatus.Posted, evidence.RecognitionJournal.Status);
        Assert.Equal(100m, evidence.RecognitionJournal.Lines.Sum(line => line.FunctionalCredit));
        Assert.Equal("migration-ap-opening.v1", evidence.RecognitionJournal.SourceContract);

        var replay = await settlement.CreateMigrationApOpeningAsync(migrationContext, command with { IdempotencyKey = "ap-replay", RequestFingerprint = "ap-replay" });
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(created.Value.Id, replay.Value!.Id);
        var changed = await settlement.CreateMigrationApOpeningAsync(migrationContext, command with { Amount = 101m, IdempotencyKey = "ap-changed", RequestFingerprint = "ap-changed" });
        Assert.False(changed.Succeeded);
        Assert.Equal("migration_ap_source_conflict", changed.Code);
        var foreign = await settlement.PreflightMigrationApOpeningAsync(migrationContext, command with { CurrencyCode = "USD", SourceReference = "AP-FOREIGN" });
        Assert.False(foreign.Ready);
        Assert.Equal("currency_not_functional", foreign.Code);

        var method = await settlement.CreatePaymentMethodAsync(settlementContext, new FinancePaymentMethodCommand(companyId, "AP-PAYMENT", "AP payment", null, FinancePaymentMethodDirection.Payment, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "ap-method", "ap-method"));
        var cash = await settlement.CreateCashAccountAsync(settlementContext, new FinanceCashAccountCommand(companyId, "AP-CASH", "AP cash", null, FinanceCashAccountKind.Bank, "SAR", cashAccount.Value.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "ap-cash", "ap-cash"));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);
        var payment = await PostPaymentAsync(settlement, settlementContext, companyId, supplierId, cash.Value!.Id, method.Value!.Id, 100m, "AP-PAYMENT-1");
        var allocation = await settlement.CreateAllocationAsync(settlementContext, new FinanceAllocationCommand(payment.Id, created.Value.Id, 40m, openingDate, "partial", Guid.NewGuid(), "ap-allocation-1", "ap-allocation-1"));
        Assert.True(allocation.Succeeded, allocation.Code);
        var partial = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, new DateOnly(2026, 3, 1), FinanceOpenItemKind.Payable, supplierId)));
        Assert.Equal(FinanceOpenItemStatus.PartiallySettled, partial.Status);
        Assert.Equal(60m, partial.OutstandingAmount);

        var remainder = await settlement.CreateAllocationAsync(settlementContext, new FinanceAllocationCommand(payment.Id, created.Value.Id, 60m, openingDate, "settle", Guid.NewGuid(), "ap-allocation-full", "ap-allocation-full"));
        Assert.True(remainder.Succeeded, remainder.Code);
        var settled = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, new DateOnly(2026, 3, 1), FinanceOpenItemKind.Payable, supplierId)));
        Assert.Equal(FinanceOpenItemStatus.Settled, settled.Status);
        Assert.Equal(0m, settled.OutstandingAmount);

        var reopenedByFullReversal = await settlement.ReverseAllocationAsync(settlementContext, new FinanceAllocationReversalCommand(remainder.Value!.Id, remainder.Value.Version, "reopen partial", Guid.NewGuid(), "ap-allocation-full-reverse", "ap-allocation-full-reverse"));
        Assert.True(reopenedByFullReversal.Succeeded, reopenedByFullReversal.Code);
        var partiallyReopened = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), FinanceOpenItemKind.Payable, supplierId)));
        Assert.Equal(FinanceOpenItemStatus.PartiallySettled, partiallyReopened.Status);
        Assert.Equal(60m, partiallyReopened.OutstandingAmount);

        var reversed = await settlement.ReverseAllocationAsync(settlementContext, new FinanceAllocationReversalCommand(allocation.Value!.Id, allocation.Value.Version, "reverse", Guid.NewGuid(), "ap-allocation-reverse", "ap-allocation-reverse"));
        Assert.True(reversed.Succeeded, reversed.Code);
        var reopened = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), FinanceOpenItemKind.Payable, supplierId)));
        Assert.Equal(FinanceOpenItemStatus.Open, reopened.Status);
        Assert.Equal(100m, reopened.OutstandingAmount);

        await using var db = new FinanceDbContext(options, tenant);
        Assert.Equal(1, await db.OpenItems.CountAsync(item => item.SourceContract == "migration-ap-opening.v1" && item.SupplierId == supplierId));
        Assert.Equal(1, await db.Journals.CountAsync(item => item.SourceContract == "migration-ap-opening.v1" && item.Status == FinanceJournalStatus.Posted));
        Assert.Equal(1, await db.SourceEffects.CountAsync(item => item.SourceContract == "migration-ap-opening.v1"));
    }

    private static async Task<FinanceSettlementDocumentRecord> PostPaymentAsync(IFinanceSettlementPersistence settlement, FinanceRequestContext context, Guid companyId, Guid supplierId, Guid cashAccountId, Guid paymentMethodId, decimal amount, string key)
    {
        var created = await settlement.CreateSettlementDocumentAsync(context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Payment, companyId, supplierId, null, cashAccountId, paymentMethodId, new DateOnly(2026, 1, 15), "SAR", amount, null, null, null, null, null, key, "AP payment", Guid.NewGuid(), key + "-create", key + "-create"));
        Assert.True(created.Succeeded, created.Code);
        var submitted = await settlement.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, key + "-submit", key + "-submit", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var posted = await settlement.PostSettlementDocumentAsync(context, new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, key + "-post", key + "-post", FinancePaymentMethodDirection.Payment));
        Assert.True(posted.Succeeded, posted.Code);
        return posted.Value!;
    }

    private static FinanceRequestContext Context(TenantContext tenant, Guid actorId, string permission)
    {
        Assert.True(FinanceRequestContext.TryCreate(FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, permission), out var context));
        return context!;
    }

    private static FoundationRequestContext FoundationContext(TenantContext tenant, Guid actorId, string permission) => FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, permission);
    private static FinanceAccountCommand Account(Guid companyId, string code, FinanceAccountType type) => new(companyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");

    private sealed class UnavailableCustomerReferenceReader : IBusinessCustomerReferenceReader
    {
        public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<BusinessCustomerReference?>(null);
    }

    private sealed class ActiveSupplierReader(Guid supplierId) : ISupplierPersistence
    {
        public Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierRecord>>([Reference(tenantContext)]);
        public Task<SupplierRecord?> FindSupplierAsync(TenantContext tenantContext, Guid requestedId, CancellationToken cancellationToken = default) => Task.FromResult<SupplierRecord?>(requestedId == supplierId ? Reference(tenantContext) : null);
        public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(TenantContext tenantContext, Guid id, CreateSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(TenantContext tenantContext, EditSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(TenantContext tenantContext, Guid id, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
        private SupplierRecord Reference(TenantContext context) => new(supplierId, context.TenantId, "SUP-1", new LocalizedName("Supplier"), null, null, MasterDataLifecycleState.Active, [1], []);
        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("test persistence unavailable"));
    }

    private sealed class MutableApprovalPolicy : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) => FinanceApprovalRequirement.NotRequired;
    }

    private sealed class StaticPrivateObjectStorage(TenantContext tenant, Guid objectId, byte[] content, MigrationSourceArtifactSnapshot source) : IPrivateObjectStorage
    {
        private readonly PrivateFileMetadata metadata = new(objectId, tenant.TenantId, TenantWorkScope.IssueFromVerifiedAuthority(tenant, TenantWorkScopeRequest.TenantWide()), "ap-opening.json", "application/json", source.Length, source.Sha256, DateTimeOffset.UnixEpoch, null, PrivateFileSafetyRequirement.TrustedGenerated);
        public ValueTask<PrivateFileMetadata> StoreAsync(TenantContext tenantContext, TenantWorkScope requestedScope, string originalFileName, string contentType, Stream content, DateTimeOffset? expiresAt = null, PrivateFileSafetyRequirement safetyRequirement = PrivateFileSafetyRequirement.ExternalScanRequired, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PrivateFileAccessResult> ReadAsync(TenantContext tenantContext, Guid requestedObjectId, CancellationToken cancellationToken = default) => tenantContext.TenantId == tenant.TenantId && requestedObjectId == objectId ? ValueTask.FromResult(PrivateFileAccessResult.AllowedResult(metadata, content)) : ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));
        public ValueTask<PrivateFileOverwriteResult> OverwriteAsync(TenantContext tenantContext, Guid requestedObjectId, long expectedConcurrencyVersion, Stream replacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ApReferenceAuthority : IMigrationReferenceAuthority
    {
        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>([new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, "reference_active", "Reference is active.")]);
        public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) => row.Payload is MigrationApOpeningPayload ap && ap.CompanyId is { } company && ap.SupplierId is { } supplier && !string.IsNullOrWhiteSpace(ap.SourceReference) ? MigrationBusinessIdentityResolution.Valid($"ap-opening:{company:D}:{supplier:D}:{ap.SourceReference.Trim()}") : MigrationBusinessIdentityResolution.NotApplicable();
    }

    private sealed class UnusedOwnerGateway : IOwnerExecutionGateway
    {
        public Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(FoundationRequestContext trustedContext, OwnerImportRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(FoundationRequestContext trustedContext, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(FoundationRequestContext trustedContext, Guid batchId, byte[] expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "not_used", null));
        public Task<OwnerEvidence?> ReadEvidenceAsync(FoundationRequestContext trustedContext, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<OwnerEvidence?>(null);
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
