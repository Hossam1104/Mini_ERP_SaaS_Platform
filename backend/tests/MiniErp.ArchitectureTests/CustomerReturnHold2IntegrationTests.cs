using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Real relational coverage for MESP-138 HOLD 2. SQL Server race evidence is
/// intentionally kept in the safety-provider suite.
/// </summary>
public sealed class CustomerReturnHold2IntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CustomerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static TenantId TenantIdValue => new(TenantId);

    [Fact]
    public async Task Post_and_reverse_commit_finance_first_and_retry_without_duplicate_effects()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var approved = await fixture.CreateAndApproveAsync();

        fixture.Sales.FailFinanceAcknowledgement = true;
        var firstPost = await fixture.Persistence.MutateAsync(fixture.Context, fixture.NoteId, approved.Version, FinanceCreditNoteMutation.Post, "post", "post-key", "post-fingerprint");

        Assert.False(firstPost.Succeeded);
        Assert.Equal("sales_finance_acknowledgement_required", firstPost.Code);
        var committed = await fixture.Persistence.GetAsync(fixture.Context, fixture.NoteId);
        Assert.NotNull(committed);
        Assert.Equal(FinanceCreditNoteStatus.Posted, committed!.Status);
        Assert.Equal("Committed", committed.FinanceCommitState);
        Assert.Equal("NotAcknowledged", committed.SalesAcknowledgementState);
        Assert.Equal("Required", committed.ReconciliationState);
        Assert.NotNull(committed.FinanceEffectId);
        Assert.Equal(1, fixture.Sales.FinanceAcknowledgementCalls);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.Context.TenantContext))
        {
            Assert.Equal(1, await db.Journals.CountAsync(item => item.SourceContract == "sales-credit-note.v1"));
            Assert.Equal(1, await db.Journals.CountAsync(item => item.SourceContract == "sales-credit-note.tax.v1"));
            Assert.Equal(1, await db.CustomerCredits.CountAsync());
            Assert.Equal(1, await db.CustomerCreditApplications.CountAsync());

            var primary = await db.Journals.Include(item => item.Lines).SingleAsync(item => item.SourceContract == "sales-credit-note.v1");
            var tax = await db.Journals.Include(item => item.Lines).SingleAsync(item => item.SourceContract == "sales-credit-note.tax.v1");
            Assert.Equal(100m, primary.Lines.Where(item => item.AccountId == fixture.RevenueAccountId).Sum(item => item.Debit));
            Assert.Equal(100m, primary.Lines.Where(item => item.AccountId == fixture.ArAccountId).Sum(item => item.Credit));
            Assert.Equal(20m, tax.Lines.Where(item => item.AccountId == fixture.TaxAccountId).Sum(item => item.Debit));
            Assert.Equal(20m, tax.Lines.Where(item => item.AccountId == fixture.RevenueAccountId).Sum(item => item.Credit));
            Assert.Equal(120m, primary.Lines.Sum(item => item.Debit) + tax.Lines.Sum(item => item.Debit));
            Assert.Equal(120m, primary.Lines.Sum(item => item.Credit) + tax.Lines.Sum(item => item.Credit));

            var credit = await db.CustomerCredits.SingleAsync();
            Assert.Equal(100m, credit.OriginalAmount);
            Assert.Equal(100m, credit.AppliedAmount);
            Assert.Equal(0m, credit.OutstandingAmount);
        }

        fixture.Sales.FailFinanceAcknowledgement = false;
        var retriedPost = await fixture.Persistence.MutateAsync(fixture.Context, fixture.NoteId, approved.Version, FinanceCreditNoteMutation.Post, "post", "post-key", "post-fingerprint");
        Assert.True(retriedPost.Succeeded, retriedPost.Code);
        Assert.Equal("Acknowledged", retriedPost.Value!.SalesAcknowledgementState);
        Assert.Equal("Reconciled", retriedPost.Value.ReconciliationState);
        Assert.Equal(2, fixture.Sales.FinanceAcknowledgementCalls);
        var wrongIdentity = await fixture.Persistence.MutateAsync(fixture.Context, fixture.NoteId, approved.Version, FinanceCreditNoteMutation.Post, "post", "different-key", "post-fingerprint");
        Assert.False(wrongIdentity.Succeeded);
        Assert.Equal("idempotency_conflict", wrongIdentity.Code);
        Assert.Equal(2, fixture.Sales.FinanceAcknowledgementCalls);

        fixture.Sales.FailReversalAcknowledgement = true;
        var firstReverse = await fixture.Persistence.MutateAsync(fixture.Context, fixture.NoteId, retriedPost.Value.Version, FinanceCreditNoteMutation.Reverse, "reverse", "reverse-key", "reverse-fingerprint");
        Assert.False(firstReverse.Succeeded);
        Assert.Equal("sales_finance_reversal_required", firstReverse.Code);
        var reversed = await fixture.Persistence.GetAsync(fixture.Context, fixture.NoteId);
        Assert.Equal(FinanceCreditNoteStatus.Reversed, reversed!.Status);
        Assert.Equal("Committed", reversed.ReversalFinanceCommitState);
        Assert.Equal("Required", reversed.ReversalReconciliationState);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.Context.TenantContext))
        {
            Assert.Equal(2, await db.Journals.CountAsync(item => item.SourceContract == "finance-reversal.v1"));
            Assert.Equal(1, await db.CustomerCreditApplications.CountAsync(item => item.Reversed));
            Assert.Equal(FinanceCustomerCreditStatus.Reversed, (await db.CustomerCredits.SingleAsync()).Status);
            var correctionJournals = await db.Journals.Include(item => item.Lines).Where(item => item.SourceContract == "sales-credit-note.v1" || item.SourceContract == "sales-credit-note.tax.v1" || item.SourceContract == "finance-reversal.v1").ToListAsync();
            Assert.All(correctionJournals.SelectMany(item => item.Lines).GroupBy(item => item.AccountId), group => Assert.Equal(0m, group.Sum(item => item.Debit - item.Credit)));
        }

        fixture.Sales.FailReversalAcknowledgement = false;
        var retriedReverse = await fixture.Persistence.MutateAsync(fixture.Context, fixture.NoteId, retriedPost.Value.Version, FinanceCreditNoteMutation.Reverse, "reverse", "reverse-key", "reverse-fingerprint");
        Assert.True(retriedReverse.Succeeded, retriedReverse.Code);
        Assert.Equal("Acknowledged", retriedReverse.Value!.ReversalSalesAcknowledgementState);
        Assert.Equal("Reconciled", retriedReverse.Value.ReversalReconciliationState);
        Assert.Equal(2, fixture.Sales.ReversalAcknowledgementCalls);
    }

    [Fact]
    public async Task Tax_reversal_uses_original_tax_effect_after_current_mapping_changes()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var approved = await fixture.CreateAndApproveAsync();

        var posted = await fixture.Persistence.MutateAsync(fixture.Context, fixture.NoteId, approved.Version, FinanceCreditNoteMutation.Post, "post", "tax-map-post-key", "tax-map-post-fingerprint");

        Assert.True(posted.Succeeded, posted.Code);
        await using var db = new FinanceDbContext(fixture.Options, fixture.Context.TenantContext);
        var tax = await db.Journals.Include(item => item.Lines).SingleAsync(item => item.SourceContract == "sales-credit-note.tax.v1");
        Assert.Equal(20m, tax.Lines.Where(item => item.AccountId == fixture.TaxAccountId).Sum(item => item.Debit));
        Assert.Equal(0m, tax.Lines.Where(item => item.AccountId == fixture.AlternateTaxAccountId).Sum(item => item.Debit));
    }

    [Fact]
    public async Task Finance_commit_failure_never_calls_sales_or_leaves_a_phantom_effect()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var approved = await fixture.CreateAndApproveAsync();
        var failing = new CustomerReturnFinancePersistence(fixture.FailingOptions, fixture.Sales, Companies(), new UnavailableMasterDataExchangeRatePersistence());

        var result = await failing.MutateAsync(fixture.Context, fixture.NoteId, approved.Version, FinanceCreditNoteMutation.Post, "post", "failed-key", "failed-fingerprint");

        Assert.False(result.Succeeded);
        Assert.Equal("finance_commit_failed", result.Code);
        Assert.Equal(0, fixture.Sales.FinanceAcknowledgementCalls);
        await using var db = new FinanceDbContext(fixture.Options, fixture.Context.TenantContext);
        Assert.Equal(0, await db.Journals.CountAsync(item => item.SourceContract == "sales-credit-note.v1"));
        Assert.Equal(0, await db.Journals.CountAsync(item => item.SourceContract == "sales-credit-note.tax.v1"));
        Assert.Equal(0, await db.CustomerCredits.CountAsync());
        var note = await db.CreditNotes.SingleAsync();
        Assert.Equal(FinanceCreditNoteStatus.Approved, note.Status);
        Assert.Equal("NotCommitted", note.FinanceCommitState);
    }

    [Fact]
    public async Task Sales_source_persistence_attributes_one_invoice_line_across_two_deliveries_once()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = SalesContext();
        var orderId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var openItemId = Guid.NewGuid();
        var deliveryA = Guid.NewGuid();
        var deliveryB = Guid.NewGuid();
        var reservationA = Guid.NewGuid();
        var reservationB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var productId = Guid.NewGuid();
        var unitOfMeasureId = Guid.NewGuid();
        var line = new SalesQuotationLineResponse(orderLineId, productId, "SKU", "Product", unitOfMeasureId, "EA", 3m, 0.5m, 0.5m, 0m, 0m, 0.5m, 1.5m, null, null, null, "test", null, false, null, null, null, null);
        var linesJson = JsonSerializer.Serialize(new[] { line });
        var writeLine = new SalesLineWriteModel(orderLineId, productId, "SKU", "Product", unitOfMeasureId, "EA", 3m, 0.5m, 0.5m, 0m, 0m, 0.5m, 1.5m, null, null, null, "test", null, false, null, null, null, null);
        var quotation = new SalesQuotationEntity(TenantIdValue, new SalesQuotationWriteModel(Guid.NewGuid(), CompanyId, null, CustomerId, "CUST", "Customer", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "SAR", null, null, null, [writeLine], 1m, 0m, 0.5m, 1.5m), "Q-1", linesJson, "{}", now);
        var order = new SalesOrderEntity(TenantIdValue, quotation, ActorId, "SO-1", linesJson, "{}", now);
        var deliveryEntityA = new SalesDeliveryEntity(TenantIdValue, deliveryA, order.Id, 1, CompanyId, null, CustomerId, Guid.NewGuid(), JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(orderLineId, reservationA, 1m) }), "{}", ActorId, null, now);
        var deliveryEntityB = new SalesDeliveryEntity(TenantIdValue, deliveryB, order.Id, 1, CompanyId, null, CustomerId, Guid.NewGuid(), JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(orderLineId, reservationB, 2m) }), "{}", ActorId, null, now.AddMinutes(1));
        deliveryEntityA.Posted([Guid.NewGuid()], now);
        deliveryEntityB.Posted([Guid.NewGuid()], now.AddMinutes(1));
        var taxId = Guid.NewGuid();
        var taxVersionId = Guid.NewGuid();
        var evidence = new SalesInvoiceLineEvidence(orderLineId, 3m, 3m, 3m, 1m, 0.5m, 1.5m, new SalesInvoiceTaxEvidence(taxId, "VAT", taxVersionId, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, 15m, 5m, 0.5m, "VAT"), [new SalesInvoiceSourceAllocation(deliveryA, orderLineId, 1, 1m, 0m, 1m, 0m), new SalesInvoiceSourceAllocation(deliveryB, orderLineId, 1, 2m, 0m, 2m, 0m)]);
        var invoiceLinesJson = JsonSerializer.Serialize(new { Lines = new[] { new SalesInvoiceRequestLine(orderLineId, 3m) }, Evidence = new[] { evidence } });
        var invoice = new SalesInvoiceRequestEntity(TenantIdValue, invoiceId, order.Id, 1, deliveryA, CompanyId, null, CustomerId, new DateOnly(2026, 1, 1), invoiceLinesJson, 1.5m, "SAR", "{}", ActorId, "invoice", now);
        invoice.Posted(openItemId, now);

        await using (var db = new SalesDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.Quotations.Add(quotation);
            db.Orders.Add(order);
            db.Deliveries.AddRange(deliveryEntityA, deliveryEntityB);
            db.InvoiceRequests.Add(invoice);
            await db.SaveChangesAsync();
        }

        var persistence = new CustomerReturnPersistence(options);
        var sourceA = await persistence.GetEligibleSourceAsync(context, deliveryA);
        var sourceB = await persistence.GetEligibleSourceAsync(context, deliveryB);
        var allocationA = Assert.Single(sourceA!.InvoiceAllocations!);
        var allocationB = Assert.Single(sourceB!.InvoiceAllocations!);
        Assert.Equal(1m, allocationA.RecognizedQuantity);
        Assert.Equal(2m, allocationB.RecognizedQuantity);
        Assert.Equal(0.33333333m, allocationA.NetAmount);
        Assert.Equal(0.66666667m, allocationB.NetAmount);
        Assert.Equal(0.16666667m, allocationA.TaxAmount);
        Assert.Equal(0.33333333m, allocationB.TaxAmount);
        Assert.Equal(0.5m, allocationA.GrossAmount);
        Assert.Equal(1m, allocationB.GrossAmount);
        Assert.Equal(1m, allocationA.NetAmount + allocationB.NetAmount);
        Assert.Equal(0.5m, allocationA.TaxAmount + allocationB.TaxAmount);
        Assert.Equal(1.5m, allocationA.GrossAmount + allocationB.GrossAmount);
        Assert.Equal(allocationA.SourceAllocationFingerprint, (await persistence.GetEligibleSourceAsync(context, deliveryA))!.InvoiceAllocations!.Single().SourceAllocationFingerprint);
        Assert.NotEqual(allocationA.SourceAllocationFingerprint, allocationB.SourceAllocationFingerprint);
        Assert.DoesNotContain(deliveryB.ToString("D"), allocationA.SourceAllocationFingerprint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(deliveryA.ToString("D"), allocationB.SourceAllocationFingerprint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Historical_source_rate_uses_invoice_date_and_rejects_tampered_provenance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenant = TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("hold2-fx"));
        var rateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var record = new MasterDataExchangeRateRecord(rateId, new TenantId(TenantId), Guid.NewGuid(), Guid.NewGuid(), "USD", "SAR", MasterDataLifecycleState.Inactive, 1, [new MasterDataExchangeRateVersionRecord(versionId, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14), 3.5m, 6, ExchangeRateProvenance.Configured, "invoice source", "USD", "SAR")], [1]);
        var rates = new StaticExchangeRates(record);
        await using (var db = new FinanceDbContext(options, tenant))
        {
            await db.Database.EnsureCreatedAsync();
            db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(tenant.TenantId, new FinanceMonetaryPolicyCommand(CompanyId, null, 2, "ToEven", false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "fx-policy", "fx-policy"), "SAR", null, 1));
            await db.SaveChangesAsync();
            var sourceDate = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, tenant, rates, CompanyId, new DateOnly(2026, 1, 10), "USD", 100m, "SAR", 350m, 3.5m, rateId, versionId, 1, CancellationToken.None);
            Assert.True(sourceDate.Succeeded, sourceDate.Code);
            Assert.Equal(rateId, sourceDate.Evidence!.TransactionToFunctionalRate!.ExchangeRateId);
            Assert.Equal(versionId, sourceDate.Evidence.TransactionToFunctionalRate.ExchangeRateVersionId);

            var laterCreditNoteDate = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, tenant, rates, CompanyId, new DateOnly(2026, 1, 15), "USD", 100m, "SAR", 350m, 3.5m, rateId, versionId, 1, CancellationToken.None);
            Assert.False(laterCreditNoteDate.Succeeded);
            var tampered = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, tenant, rates, CompanyId, new DateOnly(2026, 1, 10), "USD", 100m, "SAR", 350m, 3.6m, rateId, versionId, 1, CancellationToken.None);
            Assert.False(tampered.Succeeded);
        }
    }

    private static FinanceRequestContext FinanceContext()
    {
        var foundation = FoundationRequestContext.ForTenant(ActorId, Guid.NewGuid(), TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("hold2")), "finance.credit-note");
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private static ProcurementRequestContext SalesContext()
    {
        var foundation = FoundationRequestContext.ForTenant(ActorId, Guid.NewGuid(), TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("hold2-sales")), "sales.customer-return");
        var resolution = new ProcurementTenantContextResolver().Resolve(foundation);
        Assert.True(resolution.Allowed, resolution.Code);
        return resolution.Context!;
    }

    private static ConfiguredFinanceCompanyProvider Companies() => new([new FinanceCompanyOption(TenantId, CompanyId, "Test Company", "SAR")]);

    private sealed class FinanceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private FinanceFixture(SqliteConnection connection, DbContextOptions options, DbContextOptions failingOptions, FinanceRequestContext context, SalesSpy sales, Guid arAccountId, Guid revenueAccountId, Guid taxAccountId, Guid alternateTaxAccountId)
        {
            this.connection = connection;
            Options = options;
            FailingOptions = failingOptions;
            Context = context;
            Sales = sales;
            ArAccountId = arAccountId;
            RevenueAccountId = revenueAccountId;
            TaxAccountId = taxAccountId;
            AlternateTaxAccountId = alternateTaxAccountId;
            Persistence = new CustomerReturnFinancePersistence(options, sales, Companies(), new UnavailableMasterDataExchangeRatePersistence());
        }

        internal DbContextOptions Options { get; }
        internal DbContextOptions FailingOptions { get; }
        internal FinanceRequestContext Context { get; }
        internal SalesSpy Sales { get; }
        internal CustomerReturnFinancePersistence Persistence { get; }
        internal Guid NoteId { get; private set; }
        internal Guid ArAccountId { get; }
        internal Guid RevenueAccountId { get; }
        internal Guid TaxAccountId { get; }
        internal Guid AlternateTaxAccountId { get; }

        internal static async Task<FinanceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var interceptor = new SaveBeforeCommitInterceptor();
            var failingOptions = new DbContextOptionsBuilder().UseSqlite(connection).AddInterceptors(interceptor).Options;
            var context = FinanceContext();
            await using (var db = new FinanceDbContext(options, context.TenantContext)) await db.Database.EnsureCreatedAsync();

            var finance = new FinancePersistence(options, Companies(), new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
            var ar = await AccountAsync(finance, options, context, "AR");
            var revenue = await AccountAsync(finance, options, context, "REV");
            var tax = await AccountAsync(finance, options, context, "TAX");
            var alternateTax = await AccountAsync(finance, options, context, "TAXB");
            await RulesAsync(finance, context, ar.Id, revenue.Id, tax.Id, alternateTax.Id);
            await using (var db = new FinanceDbContext(options, context.TenantContext))
            {
                db.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(context.TenantId, new FinanceMonetaryPolicyCommand(CompanyId, null, 2, "ToEven", false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "monetary-policy", "monetary-policy"), "SAR", null, 1));
                await db.SaveChangesAsync();
            }

            var returnId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var openItemId = Guid.NewGuid();
            var allocationId = Guid.NewGuid();
            var orderLineId = Guid.NewGuid();
            var source = Source(returnId, invoiceId, openItemId, allocationId, orderLineId, tax.Id);
            var sales = new SalesSpy(source);
            await SeedRecognitionAsync(options, context, invoiceId, openItemId, ar.Id, revenue.Id, tax.Id);
            var fixture = new FinanceFixture(connection, options, failingOptions, context, sales, ar.Id, revenue.Id, tax.Id, alternateTax.Id);
            fixture.Source = source;
            return fixture;
        }

        private SalesCustomerReturnSourceRecord Source { get; set; } = null!;

        internal async Task<FinanceCreditNoteResponse> CreateAndApproveAsync()
        {
            var created = await Persistence.CreateAsync(Context, new FinanceCreditNoteCreateRequest(Source.ReturnSourceId, new DateOnly(2026, 1, 15), "HOLD 2", Source.RecognizedInvoiceId), "create-key", "create-fingerprint");
            Assert.True(created.Succeeded, created.Code);
            NoteId = created.Value!.Id;
            var submitted = await Persistence.MutateAsync(Context, NoteId, created.Value.Version, FinanceCreditNoteMutation.Submit, null, "submit-key", "submit-fingerprint");
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await Persistence.MutateAsync(Context, NoteId, submitted.Value!.Version, FinanceCreditNoteMutation.Approve, null, "approve-key", "approve-fingerprint");
            Assert.True(approved.Succeeded, approved.Code);
            return approved.Value!;
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private sealed class SalesSpy(SalesCustomerReturnSourceRecord source) : ISalesCustomerReturnSourceProvider
    {
        internal bool FailFinanceAcknowledgement { get; set; }
        internal bool FailReversalAcknowledgement { get; set; }
        internal int FinanceAcknowledgementCalls { get; private set; }
        internal int ReversalAcknowledgementCalls { get; private set; }

        public Task<SalesCustomerReturnSourceRecord?> GetCustomerReturnSourceAsync(TenantContext context, Guid returnId, CancellationToken cancellationToken = default) => Task.FromResult<SalesCustomerReturnSourceRecord?>(returnId == source.ReturnSourceId ? source : null);
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext context, SalesCustomerReturnInventoryAcknowledgementCommand command, CancellationToken cancellationToken = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext context, SalesCustomerReturnInventoryFailureCommand command, CancellationToken cancellationToken = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext context, SalesCustomerReturnDownstreamReversalCommand command, CancellationToken cancellationToken = default)
        {
            ReversalAcknowledgementCalls++;
            return Task.FromResult(FailReversalAcknowledgement ? SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("sales_finance_reversal_required") : SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(Response(command.ReturnId)));
        }
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RegisterFinanceCreditNoteAsync(TenantContext context, SalesCustomerReturnFinanceEffectCommand command, CancellationToken cancellationToken = default)
        {
            FinanceAcknowledgementCalls++;
            return Task.FromResult(FailFinanceAcknowledgement ? SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("sales_finance_acknowledgement_required") : SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(Response(command.ReturnId)));
        }

        private SalesCustomerReturnResponse Response(Guid returnId) => new(returnId, source.TenantId, source.DeliveryId, source.OrderId, source.OrderRevisionNumber, source.CompanyId, source.BranchId, source.CustomerId, source.WarehouseId, source.RecognizedInvoiceId, source.FinanceOpenItemId, source.Status, source.Consequence, new DateOnly(2026, 1, 1), "test", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], [], [1]);
    }

    private sealed class SaveBeforeCommitInterceptor : SaveChangesInterceptor
    {
        private int remaining = 1;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is FinanceDbContext && Interlocked.Exchange(ref remaining, 0) == 1) throw new InvalidOperationException("injected finance save failure");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StaticExchangeRates(MasterDataExchangeRateRecord record) : IMasterDataExchangeRatePersistence
    {
        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("unused"));
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataExchangeRateRecord>>([record]);
        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataExchangeRateRecord?>(exchangeRateId == record.Id && tenantContext.TenantId.Value == record.TenantId.Value ? record : null);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();
    }

    private static SalesCustomerReturnSourceRecord Source(Guid returnId, Guid invoiceId, Guid openItemId, Guid allocationId, Guid orderLineId, Guid taxId) => new(
        returnId, Guid.NewGuid(), Guid.NewGuid(), 1, TenantId, CompanyId, null, CustomerId, Guid.NewGuid(), DateTimeOffset.UtcNow, invoiceId, openItemId, "SAR",
        [new(orderLineId, Guid.NewGuid(), "SKU", "Product", Guid.NewGuid(), "EA", 1m, 0m, 1m, 80m, 20m, 100m, null, 1m, null, taxId, Guid.NewGuid(), 1m, 1m, 1m, 1m, 0m, 0m, "Restockable", [], [], null)],
        SalesCustomerReturnStatus.Received, SalesCustomerReturnConsequence.CreditNote, [1],
        [new(allocationId, invoiceId, openItemId, Guid.NewGuid(), orderLineId, 1, 1m, 1m, 1m, 0m, 1m, 80m, 20m, 100m, "SAR", taxId, Guid.NewGuid(), 1, "allocation", "invoice")]);

    private static async Task<FinanceAccountRecord> AccountAsync(FinancePersistence persistence, DbContextOptions options, FinanceRequestContext context, string code)
    {
        var result = await persistence.CreateAccountAsync(context, new FinanceAccountCommand(CompanyId, code, code, null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"account-{code}", $"account-{code}"));
        Assert.True(result.Succeeded, result.Code);
        return result.Value!;
    }

    private static async Task RulesAsync(FinancePersistence persistence, FinanceRequestContext context, Guid ar, Guid revenue, Guid taxAccount, Guid alternateTaxAccount)
    {
        var calendar = await persistence.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "HOLD2", Guid.NewGuid(), "calendar", "calendar"));
        var year = await persistence.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "year", "year"));
        var period = await persistence.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026", "2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "period", "period"));
        var opened = await persistence.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "period-open", "period-open"));
        Assert.True(opened.Succeeded, opened.Code);
        var recognition = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, "sales-invoice.v1", "recognition", ar, revenue, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "recognition", "recognition"));
        var credit = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, "sales-credit-note.v1", "posting", revenue, ar, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "credit", "credit"));
        var taxRule = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, "finance-tax.v1", "output", revenue, taxAccount, false, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14), Guid.NewGuid(), "tax", "tax"));
        var currentTaxRule = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, "finance-tax.v1", "output", revenue, alternateTaxAccount, false, new DateOnly(2026, 1, 15), null, Guid.NewGuid(), "tax-current", "tax-current"));
        Assert.True(recognition.Succeeded, recognition.Code);
        Assert.True(credit.Succeeded, credit.Code);
        Assert.True(taxRule.Succeeded, taxRule.Code);
        Assert.True(currentTaxRule.Succeeded, currentTaxRule.Code);
    }

    private static async Task SeedRecognitionAsync(DbContextOptions options, FinanceRequestContext context, Guid invoiceId, Guid openItemId, Guid arId, Guid revenueId, Guid taxId)
    {
        await using var db = new FinanceDbContext(options, context.TenantContext);
        var ar = await db.Accounts.SingleAsync(item => item.Id == arId);
        var revenue = await db.Accounts.SingleAsync(item => item.Id == revenueId);
        var tax = await db.Accounts.SingleAsync(item => item.Code == "TAX");
        var rule = await db.PostingRules.SingleAsync(item => item.SourceContract == "sales-invoice.v1");
        var taxRule = await db.PostingRules.SingleAsync(item => item.SourceContract == "finance-tax.v1" && item.SourceEvent == "output" && item.VersionNumber == 1);
        var period = await db.FiscalPeriods.SingleAsync();
        var journalId = Guid.NewGuid();
        var command = new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "SAR", 1m, null, null, null, "sales-invoice.v1", "recognition", invoiceId, 1, rule.Id, "invoice", [new(arId, 100m, 0m, 100m, "SAR", null, "invoice"), new(revenueId, 0m, 100m, 100m, "SAR", null, "invoice")], journalId, "invoice", "invoice", FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var journal = new FinanceJournalEntity(context.TenantId, journalId, command, 1, "SAR", context.ActorId, DateTimeOffset.UtcNow);
        journal.SetPeriod(period.FiscalYearId, period.Id);
        journal.SetRule(rule.Id, rule.VersionNumber);
        journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow);
        journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journalId, 1, ar, command.Lines[0], null, 100m, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journalId, 2, revenue, command.Lines[1], null, 0m, 100m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        var item = new FinanceOpenItemEntity(context.TenantId, openItemId, FinanceOpenItemKind.Receivable, CompanyId, null, CustomerId, "sales-invoice.v1", Guid.NewGuid(), 1, invoiceId, 1, "invoice", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "SAR", 100m, "SAR", 100m, null, null, null, null, null, null, null, "invoice");
        item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journalId);
        var taxJournalId = Guid.NewGuid();
        var taxCommand = new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "SAR", 1m, null, null, null, "finance-tax.v1", "output", openItemId, 1, taxRule.Id, "invoice tax", [new(revenueId, 20m, 0m, 20m, "SAR", null, "invoice tax"), new(tax.Id, 0m, 20m, 20m, "SAR", null, "invoice tax")], taxJournalId, "invoice-tax", "invoice-tax", FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var taxJournal = new FinanceJournalEntity(context.TenantId, taxJournalId, taxCommand, 2, "SAR", context.ActorId, DateTimeOffset.UtcNow);
        taxJournal.SetPeriod(period.FiscalYearId, period.Id);
        taxJournal.SetRule(taxRule.Id, taxRule.VersionNumber);
        taxJournal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow);
        taxJournal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), taxJournalId, 1, revenue, taxCommand.Lines[0], null, 20m, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        taxJournal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), taxJournalId, 2, tax, taxCommand.Lines[1], null, 0m, 20m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        var taxEvidence = new FinanceMonetaryEvidence("SAR", 20m, "SAR", 20m, null, null, null, null, 20m, null, 2, "ToEven", 0m, null, FinanceEvidenceStatus.NotCaptured);
        var taxEffect = new FinanceTaxAccountingEffectEntity(context.TenantId, Guid.NewGuid(), CompanyId, openItemId, FinanceOpenItemKind.Receivable, taxId, "VAT", Guid.NewGuid(), 1, new DateOnly(2026, 1, 1), 20m, 100m, 20m, "SAR", 20m, "SAR", taxJournalId, taxRule.Id, taxRule.VersionNumber, taxEvidence, context.ActorId, DateTimeOffset.UtcNow);
        db.Journals.Add(journal);
        db.Journals.Add(taxJournal);
        db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), taxJournalId, CompanyId, taxEffect.Id, taxEvidence, DateTimeOffset.UtcNow));
        db.TaxAccountingEffects.Add(taxEffect);
        db.OpenItems.Add(item);
        await db.SaveChangesAsync();
    }
}
