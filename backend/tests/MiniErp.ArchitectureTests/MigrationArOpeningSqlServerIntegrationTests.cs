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
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationArOpeningSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    [Fact]
    public async Task Sql_server_ar_opening_is_finance_owned_replay_safe_and_settleable()
    {
        await using var connection = await safety.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var migrationOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);
        var actorId = Guid.NewGuid();
        var tenant = TenantContext.ForOrdinaryMembership(
            safety.TenantA.TenantId,
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("ar-sql"),
            actorId: actorId);
        var companyId = Guid.NewGuid();
        var customerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var documentDate = new DateOnly(2026, 1, 10);
        var openingDate = new DateOnly(2026, 1, 15);
        var dueDate = new DateOnly(2026, 2, 14);
        var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenant.TenantId.Value, companyId, "AR opening company", "SAR")]);
        var approval = new MutableApprovalPolicy();
        var setup = new FinancePersistence(options, companies, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence(), approval);
        var settlement = new FinanceSettlementPersistence(
            options,
            companies,
            new UnavailableMasterDataExchangeRatePersistence(),
            new ActiveCustomerReader(customerId),
            new UnavailableSupplierPersistence(),
            new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            new UnavailableFinanceSupplierInvoiceSourceProvider(),
            approval);
        var accountContext = Context(tenant, actorId, "tenant.finance.account.manage");
        var calendarContext = Context(tenant, actorId, "tenant.finance.calendar.manage");
        var ruleContext = Context(tenant, actorId, "tenant.finance.posting-rule.manage");
        var migrationContext = Context(tenant, actorId, "tenant.migration.execute");
        var migrationFoundationContext = FoundationContext(tenant, actorId, "tenant.migration.execute");
        var receiptContext = Context(tenant, actorId, "tenant.finance.settlement.manage");
        var readContext = Context(tenant, actorId, "tenant.finance.journal.view");

        var arAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "AR-OPENING", FinanceAccountType.Asset));
        var offsetAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "AR-OPENING-OFFSET", FinanceAccountType.Equity));
        var cashAccount = await setup.CreateAccountAsync(accountContext, Account(companyId, "AR-OPENING-CASH", FinanceAccountType.Asset));
        Assert.True(arAccount.Succeeded, arAccount.Code);
        Assert.True(offsetAccount.Succeeded, offsetAccount.Code);
        Assert.True(cashAccount.Succeeded, cashAccount.Code);

        var calendar = await setup.CreateCalendarAsync(calendarContext, new FinanceFiscalCalendarCommand(companyId, "AR FY", Guid.NewGuid(), "ar-calendar", "ar-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await setup.CreateYearAsync(calendarContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "ar-year", "ar-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await setup.CreatePeriodAsync(calendarContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "ar-period", "ar-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await setup.SetPeriodStateAsync(calendarContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "ar-period-open", "ar-period-open"));
        Assert.True(opened.Succeeded, opened.Code);

        var arRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "migration-ar-opening.v1", "recognition", arAccount.Value!.Id, offsetAccount.Value!.Id, false, openingDate, null, Guid.NewGuid(), "ar-rule", "ar-rule"));
        var receiptRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "customer-receipt.v1", "on-account", cashAccount.Value!.Id, arAccount.Value.Id, false, openingDate, null, Guid.NewGuid(), "receipt-rule", "receipt-rule"));
        var allocationRule = await setup.CreatePostingRuleAsync(ruleContext, new FinancePostingRuleCommand(companyId, "customer-receipt.v1", "allocation", cashAccount.Value.Id, arAccount.Value.Id, false, openingDate, null, Guid.NewGuid(), "allocation-rule", "allocation-rule"));
        Assert.True(arRule.Succeeded, arRule.Code);
        Assert.True(receiptRule.Succeeded, receiptRule.Code);
        Assert.True(allocationRule.Succeeded, allocationRule.Code);

        var migration = new MigrationPersistence(migrationOptions);
        var foundation = new MigrationFoundationService(migration, new NoopAuditSink());
        var scopes = new TenantWideScopeResolver();
        var references = new ArReferenceAuthority();
        var objectId = Guid.NewGuid();
        var sourceHash = new string('A', 64);
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
                    sourceRecordId = "ar-1",
                    recordType = "ArOpening",
                    payload = new { companyId, customerId, sourceReference = " AR-OPEN-001 ", documentDate, openingDate, amount = 100m, currencyCode = "sar", dueDate }
                }
            }
        });
        var source = new MigrationSourceArtifactSnapshot(objectId, tenant.TenantId, null, null, null, sourceHash, 1, 1);
        var storage = new StaticPrivateObjectStorage(tenant, objectId, package, source);
        var definition = new MigrationDefinitionReference("tenant-onboarding.foundation", "1");
        var profile = new MigrationSourceProfileReference("neutral-source-profile", "1");
        var intake = await new MigrationIntakeService(migration, storage, scopes, new NoopAuditSink()).RegisterAsync(
            migrationFoundationContext,
            new MigrationIntakeRegistrationRequest(definition, profile, MigrationOperationKind.Validation, objectId),
            "ar-intake");
        Assert.True(intake.Succeeded, intake.Code);
        var runId = intake.Value!.Run.RunId;
        var validationService = new MigrationValidationService(foundation, migration, storage, scopes, references, scopes);
        var validation = await validationService.ValidateAsync(migrationFoundationContext, runId, "ar-validation");
        Assert.True(validation.Succeeded, validation.Code);
        Assert.Equal(1, validation.Value!.AcceptedCount);
        var dryRun = await validationService.DryRunAsync(migrationFoundationContext, runId, "ar-dry-run");
        Assert.True(dryRun.Succeeded, dryRun.Code);
        Assert.Equal(MigrationPlannedAction.Create, Assert.Single(dryRun.Value!.Rows).PlannedAction);
        var approved = await foundation.TransitionRunAsync(migrationFoundationContext, runId, MigrationRunStatus.Approved, (await migration.FindRunAsync(tenant, runId))!.Version);
        Assert.True(approved.Succeeded, approved.Code);
        var arCoordinator = new MigrationArOpeningExecutionCoordinator(migration, references, settlement);
        var execution = new MigrationExecutionService(
            foundation,
            migration,
            migration,
            migration,
            scopes,
            scopes,
            new UnusedOwnerGateway(),
            references,
            null,
            arCoordinator);
        var executed = await execution.ExecuteAsync(migrationFoundationContext, runId, "ar-execution", approved.Value!.Version);
        Assert.True(executed.Succeeded, executed.Code);
        Assert.Equal(MigrationRunStatus.Completed, executed.Value!.RunStatus);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, executed.Value.FingerprintVersion);
        Assert.Single(executed.Value.Effects, item => item.RecordType == MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.Committed);
        Assert.Equal(2, executed.Value.Representations!.Count);
        Assert.Contains(executed.Value.Representations, item => item.Kind == MigrationEconomicRepresentationKind.FinanceOpenItem);
        Assert.Contains(executed.Value.Representations, item => item.Kind == MigrationEconomicRepresentationKind.FinanceJournal);
        Assert.Equal("reconciled", Assert.Single(executed.Value.ArEconomicReconciliations!).Status);

        var staged = Assert.Single(await migration.ListStagedRecordsAsync(tenant, runId, 0, 100));
        var command = new FinanceMigrationArOpeningCommand(companyId, customerId, " AR-OPEN-001 ", documentDate, openingDate, 100m, "sar", dueDate, null, staged.PayloadHash, "ar-create", staged.PayloadHash);
        var ready = await settlement.PreflightMigrationArOpeningAsync(migrationContext, command);
        Assert.True(ready.Ready, ready.Code);
        Assert.Equal(FinanceApprovalRequirement.NotRequired, ready.ApprovalRequirement);
        var created = await settlement.CreateMigrationArOpeningAsync(migrationContext, command);
        Assert.True(created.Succeeded, created.Code);
        var evidence = await settlement.ReadMigrationArOpeningAsync(migrationContext, command);
        Assert.NotNull(evidence);
        Assert.Equal(created.Value!.Id, evidence.OpenItem.Id);
        Assert.Equal(created.Value.OriginalAmount, evidence.RecognitionJournal.Lines.Sum(line => line.FunctionalDebit));
        Assert.Equal(FinanceJournalStatus.Posted, evidence.RecognitionJournal.Status);
        Assert.Equal(openingDate, evidence.RecognitionJournal.PostingDate);
        Assert.Equal("migration-ar-opening.v1", evidence.RecognitionJournal.SourceContract);
        Assert.Equal(evidence.OpenItem.RecognitionJournalId, evidence.RecognitionJournal.Id);
        Assert.Equal(evidence.RecognitionJournal.Id, evidence.SourceEffect.JournalId);
        Assert.Equal(evidence.OpenItem.SourceEvidenceId, evidence.SourceEffect.SourceEvidenceId);
        Assert.Equal(evidence.OpenItem.SourceEvidenceVersion, evidence.SourceEffect.SourceEvidenceVersion);
        Assert.Equal(2, evidence.RecognitionJournal.Lines.Count);
        Assert.Equal(100m, evidence.RecognitionJournal.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, evidence.RecognitionJournal.Lines.Sum(line => line.Credit));
        Assert.Equal(100m, evidence.RecognitionJournal.Lines.Sum(line => line.FunctionalDebit));
        Assert.Equal(100m, evidence.RecognitionJournal.Lines.Sum(line => line.FunctionalCredit));

        var replay = await settlement.CreateMigrationArOpeningAsync(migrationContext, command with { IdempotencyKey = "ar-replay", RequestFingerprint = "ar-replay" });
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(created.Value.Id, replay.Value!.Id);
        var changed = await settlement.CreateMigrationArOpeningAsync(migrationContext, command with { Amount = 101m, IdempotencyKey = "ar-changed", RequestFingerprint = "ar-changed" });
        Assert.False(changed.Succeeded);
        Assert.Equal("migration_ar_source_conflict", changed.Code);
        var foreign = await settlement.PreflightMigrationArOpeningAsync(migrationContext, command with { CurrencyCode = "USD", SourceReference = "AR-FOREIGN" });
        Assert.False(foreign.Ready);
        Assert.Equal("currency_not_functional", foreign.Code);

        approval.Requirement = FinanceApprovalRequirement.Required;
        var approvalBlocked = await settlement.PreflightMigrationArOpeningAsync(migrationContext, command with { SourceReference = "AR-APPROVAL" });
        Assert.False(approvalBlocked.Ready);
        Assert.Equal("approval_required", approvalBlocked.Code);
        approval.Requirement = FinanceApprovalRequirement.NotRequired;

        var method = await settlement.CreatePaymentMethodAsync(receiptContext, new FinancePaymentMethodCommand(companyId, "AR-RECEIPT", "AR receipt", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "ar-method", "ar-method"));
        var cash = await settlement.CreateCashAccountAsync(receiptContext, new FinanceCashAccountCommand(companyId, "AR-CASH", "AR cash", null, FinanceCashAccountKind.Bank, "SAR", cashAccount.Value.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "ar-cash", "ar-cash"));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);

        var firstReceipt = await PostReceiptAsync(settlement, receiptContext, companyId, customerId, cash.Value!.Id, method.Value!.Id, 40m, "AR-RECEIPT-1");
        var firstAllocation = await settlement.CreateAllocationAsync(receiptContext, new FinanceAllocationCommand(firstReceipt.Id, created.Value.Id, 40m, openingDate, "partial", Guid.NewGuid(), "ar-allocation-1", "ar-allocation-1"));
        Assert.True(firstAllocation.Succeeded, firstAllocation.Code);
        var partial = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, new DateOnly(2026, 3, 1), FinanceOpenItemKind.Receivable, customerId)));
        Assert.Equal(FinanceOpenItemStatus.PartiallySettled, partial.Status);
        Assert.Equal(60m, partial.OutstandingAmount);

        var secondReceipt = await PostReceiptAsync(settlement, receiptContext, companyId, customerId, cash.Value.Id, method.Value.Id, 60m, "AR-RECEIPT-2");
        var secondAllocation = await settlement.CreateAllocationAsync(receiptContext, new FinanceAllocationCommand(secondReceipt.Id, created.Value.Id, 60m, openingDate, "full", Guid.NewGuid(), "ar-allocation-2", "ar-allocation-2"));
        Assert.True(secondAllocation.Succeeded, secondAllocation.Code);
        var full = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, new DateOnly(2026, 3, 1), FinanceOpenItemKind.Receivable, customerId)));
        Assert.Equal(FinanceOpenItemStatus.Settled, full.Status);
        Assert.Equal(0m, full.OutstandingAmount);
        var exposure = await settlement.GetExposureAsync(readContext, new FinanceExposureQuery(companyId, customerId, new DateOnly(2026, 3, 1)));
        Assert.NotNull(exposure);
        Assert.Equal(0m, exposure.NetReceivableExposure);

        var reversedSecond = await settlement.ReverseAllocationAsync(receiptContext, new FinanceAllocationReversalCommand(secondAllocation.Value!.Id, secondAllocation.Value.Version, "reverse second", Guid.NewGuid(), "ar-allocation-reverse-2", "ar-allocation-reverse-2"));
        Assert.True(reversedSecond.Succeeded, reversedSecond.Code);
        var postReversalAsOf = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var partialAfterReversal = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, postReversalAsOf, FinanceOpenItemKind.Receivable, customerId)));
        Assert.Equal(FinanceOpenItemStatus.PartiallySettled, partialAfterReversal.Status);
        Assert.Equal(60m, partialAfterReversal.OutstandingAmount);
        var reversedFirst = await settlement.ReverseAllocationAsync(receiptContext, new FinanceAllocationReversalCommand(firstAllocation.Value!.Id, firstAllocation.Value.Version, "reverse first", Guid.NewGuid(), "ar-allocation-reverse-1", "ar-allocation-reverse-1"));
        Assert.True(reversedFirst.Succeeded, reversedFirst.Code);
        var reopened = Assert.Single(await settlement.GetAgingAsync(readContext, new FinanceAgingQuery(companyId, postReversalAsOf, FinanceOpenItemKind.Receivable, customerId)));
        Assert.Equal(FinanceOpenItemStatus.Open, reopened.Status);
        Assert.Equal(100m, reopened.OutstandingAmount);

        await using var db = new FinanceDbContext(options, tenant);
        Assert.Equal(1, await db.OpenItems.CountAsync(item => item.SourceContract == "migration-ar-opening.v1" && item.CustomerId == customerId));
        Assert.Equal(1, await db.Journals.CountAsync(item => item.SourceContract == "migration-ar-opening.v1" && item.Status == FinanceJournalStatus.Posted));
        Assert.Equal(1, await db.SourceEffects.CountAsync(item => item.SourceContract == "migration-ar-opening.v1"));
        Assert.Equal(4, await db.Allocations.CountAsync(item => item.OpenItemId == created.Value.Id));
    }

    private static async Task<FinanceSettlementDocumentRecord> PostReceiptAsync(IFinanceSettlementPersistence settlement, FinanceRequestContext context, Guid companyId, Guid customerId, Guid cashAccountId, Guid paymentMethodId, decimal amount, string key)
    {
        var created = await settlement.CreateSettlementDocumentAsync(context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, companyId, null, customerId, cashAccountId, paymentMethodId, new DateOnly(2026, 1, 15), "SAR", amount, null, null, null, null, null, key, "AR receipt", Guid.NewGuid(), key + "-create", key + "-create"));
        Assert.True(created.Succeeded, created.Code);
        var submitted = await settlement.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, key + "-submit", key + "-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var posted = await settlement.PostSettlementDocumentAsync(context, new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, key + "-post", key + "-post", FinancePaymentMethodDirection.Receipt));
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

    private sealed class ActiveCustomerReader(Guid customerId) : IBusinessCustomerReferenceReader
    {
        public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid requestedId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BusinessCustomerReference?>(requestedId == customerId ? new BusinessCustomerReference(customerId, tenantContext.TenantId, "AR-CUSTOMER", MasterDataLifecycleState.Active) : null);
    }

    private sealed class MutableApprovalPolicy : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Requirement { get; set; } = FinanceApprovalRequirement.NotRequired;
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) => Requirement;
    }

    private sealed class StaticPrivateObjectStorage(TenantContext tenant, Guid objectId, byte[] content, MigrationSourceArtifactSnapshot source) : IPrivateObjectStorage
    {
        private readonly PrivateFileMetadata metadata = new(objectId, tenant.TenantId, TenantWorkScope.IssueFromVerifiedAuthority(tenant, TenantWorkScopeRequest.TenantWide()), "ar-opening.json", "application/json", source.Length, source.Sha256, DateTimeOffset.UnixEpoch, null, PrivateFileSafetyRequirement.TrustedGenerated);

        public ValueTask<PrivateFileMetadata> StoreAsync(TenantContext tenantContext, TenantWorkScope requestedScope, string originalFileName, string contentType, Stream content, DateTimeOffset? expiresAt = null, PrivateFileSafetyRequirement safetyRequirement = PrivateFileSafetyRequirement.ExternalScanRequired, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PrivateFileAccessResult> ReadAsync(TenantContext tenantContext, Guid requestedObjectId, CancellationToken cancellationToken = default) => tenantContext.TenantId == tenant.TenantId && requestedObjectId == objectId ? ValueTask.FromResult(PrivateFileAccessResult.AllowedResult(metadata, content)) : ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));
        public ValueTask<PrivateFileOverwriteResult> OverwriteAsync(TenantContext tenantContext, Guid requestedObjectId, long expectedConcurrencyVersion, Stream replacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ArReferenceAuthority : IMigrationReferenceAuthority
    {
        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>([new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, "reference_active", "Reference is active.")]);
        public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) => row.Payload is MigrationArOpeningPayload ar && ar.CompanyId is { } company && ar.CustomerId is { } customer && !string.IsNullOrWhiteSpace(ar.SourceReference) ? MigrationBusinessIdentityResolution.Valid($"ar-opening:{company:D}:{customer:D}:{ar.SourceReference.Trim()}") : MigrationBusinessIdentityResolution.NotApplicable();
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
