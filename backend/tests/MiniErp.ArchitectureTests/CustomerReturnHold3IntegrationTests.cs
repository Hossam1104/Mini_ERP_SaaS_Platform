using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class CustomerReturnHold3IntegrationTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static TenantId TenantValue => new(Tenant);

    [Fact]
    public async Task Multi_invoice_return_acknowledges_each_invoice_subset_without_root_shortcut()
    {
        var invoiceA = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();
        await using var fixture = await SalesFixture.CreateAsync([
            Allocation(invoiceA, Guid.NewGuid(), 1m, 80m, 20m, 100m, "allocation-a"),
            Allocation(invoiceB, Guid.NewGuid(), 1m, 40m, 10m, 50m, "allocation-b")], recognizedInvoiceId: null);

        var first = await fixture.AcknowledgeAsync(Guid.NewGuid(), fixture.Allocations[0], fixture.Effect(fixture.Allocations[0]));
        var second = await fixture.AcknowledgeAsync(Guid.NewGuid(), fixture.Allocations[1], fixture.Effect(fixture.Allocations[1]));

        Assert.True(first.Succeeded, first.Code);
        Assert.True(second.Succeeded, second.Code);
        Assert.Null((await fixture.Persistence.GetAsync(fixture.ProcurementContext, fixture.ReturnId))!.InvoiceId);
        await using var db = new SalesDbContext(fixture.Options, fixture.Context);
        Assert.Equal(2, await db.CustomerReturnFinanceEffects.CountAsync());
        Assert.Equal(2, await db.CustomerReturnFinanceEffectAllocations.CountAsync());
        Assert.Equal(1, await db.CustomerReturnFinanceEffects.CountAsync(item => item.InvoiceId == invoiceA && item.FinanceOpenItemId == fixture.Allocations[0].FinanceOpenItemId));
        Assert.Equal(1, await db.CustomerReturnFinanceEffects.CountAsync(item => item.InvoiceId == invoiceB && item.FinanceOpenItemId == fixture.Allocations[1].FinanceOpenItemId));
    }

    [Fact]
    public async Task Partial_subset_then_remaining_subset_is_durable_and_non_overlapping()
    {
        var invoice = Guid.NewGuid();
        var openItem = Guid.NewGuid();
        await using var fixture = await SalesFixture.CreateAsync([
            Allocation(invoice, openItem, 1m, 80m, 20m, 100m, "allocation-a"),
            Allocation(invoice, openItem, 1m, 40m, 10m, 50m, "allocation-b")]);

        var first = await fixture.AcknowledgeAsync(Guid.NewGuid(), fixture.Allocations[0], fixture.Effect(fixture.Allocations[0]));
        var second = await fixture.AcknowledgeAsync(Guid.NewGuid(), fixture.Allocations[1], fixture.Effect(fixture.Allocations[1]));
        var overlap = await fixture.AcknowledgeAsync(Guid.NewGuid(), fixture.Allocations[0], fixture.Effect(fixture.Allocations[0]));

        Assert.True(first.Succeeded, first.Code);
        Assert.True(second.Succeeded, second.Code);
        Assert.False(overlap.Succeeded);
        Assert.Equal("finance_effect_mismatch", overlap.Code);
        await using var db = new SalesDbContext(fixture.Options, fixture.Context);
        Assert.Equal(2, await db.CustomerReturnFinanceEffectAllocations.CountAsync());
    }

    [Fact]
    public async Task Partial_quantity_residual_rounding_reconciles_exactly_once()
    {
        var allocation = Allocation(Guid.NewGuid(), Guid.NewGuid(), 1m, 80m, 20m, 100m, "allocation-residual");
        await using var fixture = await SalesFixture.CreateAsync([allocation]);

        var first = await fixture.AcknowledgeAsync(Guid.NewGuid(), allocation, fixture.Effect(allocation, .4m, 32m, 8m, 40m));
        var second = await fixture.AcknowledgeAsync(Guid.NewGuid(), allocation, fixture.Effect(allocation, .6m, 48m, 12m, 60m));
        var excess = await fixture.AcknowledgeAsync(Guid.NewGuid(), allocation, fixture.Effect(allocation, .01m, .8m, .2m, 1m));

        Assert.True(first.Succeeded, first.Code);
        Assert.True(second.Succeeded, second.Code);
        Assert.False(excess.Succeeded);
        await using var db = new SalesDbContext(fixture.Options, fixture.Context);
        Assert.Equal(2, await db.CustomerReturnFinanceEffectAllocations.CountAsync());
        Assert.Equal(80m, await db.CustomerReturnFinanceEffectAllocations.SumAsync(item => item.NetAmount));
        Assert.Equal(20m, await db.CustomerReturnFinanceEffectAllocations.SumAsync(item => item.TaxAmount));
        Assert.Equal(100m, await db.CustomerReturnFinanceEffectAllocations.SumAsync(item => item.GrossAmount));
    }

    [Fact]
    public async Task Uninvoiced_return_remainder_does_not_gain_finance_authority()
    {
        var allocation = Allocation(Guid.NewGuid(), Guid.NewGuid(), 1m, 80m, 20m, 100m, "allocation-invoiced");
        await using var fixture = await SalesFixture.CreateAsync([allocation], recognizedInvoiceId: null, returnQuantity: 2m);

        var result = await fixture.AcknowledgeAsync(Guid.NewGuid(), allocation, fixture.Effect(allocation));

        Assert.True(result.Succeeded, result.Code);
        await using var db = new SalesDbContext(fixture.Options, fixture.Context);
        Assert.Null(await db.CustomerReturns.Where(item => item.Id == fixture.ReturnId).Select(item => item.InvoiceId).SingleAsync());
        Assert.Equal(1, await db.CustomerReturnFinanceEffectAllocations.CountAsync());
    }

    [Fact]
    public async Task Post_mismatch_and_reversal_identity_fail_closed_but_exact_retry_converges()
    {
        var allocation = Allocation(Guid.NewGuid(), Guid.NewGuid(), 1m, 80m, 20m, 100m, "allocation-reversal");
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var creditNoteId = Guid.NewGuid();
        var effect = fixture.Effect(allocation);
        var posted = await fixture.AcknowledgeAsync(creditNoteId, allocation, effect);
        var tampered = await fixture.AcknowledgeAsync(creditNoteId, allocation, effect with { GrossAmount = 99m });
        var wrongReversal = await fixture.ReverseAsync(creditNoteId, allocation, effect, Guid.NewGuid(), Guid.NewGuid());
        var reversalJournalId = Guid.NewGuid();
        var exactReversal = await fixture.ReverseAsync(creditNoteId, allocation, effect, reversalJournalId, fixture.PostingJournalId);
        var retry = await fixture.ReverseAsync(creditNoteId, allocation, effect, reversalJournalId, fixture.PostingJournalId);

        Assert.True(posted.Succeeded, posted.Code);
        Assert.False(tampered.Succeeded);
        Assert.Equal("finance_effect_mismatch", tampered.Code);
        Assert.False(wrongReversal.Succeeded);
        Assert.True(exactReversal.Succeeded, exactReversal.Code);
        Assert.True(retry.Succeeded, retry.Code);
        await using var db = new SalesDbContext(fixture.Options, fixture.Context);
        var stored = await db.CustomerReturnFinanceEffects.SingleAsync();
        Assert.Equal("Reversed", stored.State);
        Assert.Equal(reversalJournalId, stored.ReversalJournalId);
        Assert.Equal(1, await db.CustomerReturnFinanceEffectAllocations.CountAsync());
    }

    [Fact]
    public async Task Company_customer_and_tenant_scope_mismatch_fail_closed()
    {
        var allocation = Allocation(Guid.NewGuid(), Guid.NewGuid(), 1m, 80m, 20m, 100m, "allocation-scope");
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var effect = fixture.Effect(allocation);
        var companyMismatch = await fixture.AcknowledgeAsync(Guid.NewGuid(), allocation, effect, companyId: Guid.NewGuid());
        var customerMismatch = await fixture.AcknowledgeAsync(Guid.NewGuid(), allocation, effect, customerId: Guid.NewGuid());
        var otherTenant = TenantContext.ForOrdinaryMembership(new TenantId(Guid.NewGuid()), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("hold3-other-tenant"));
        var tenantMismatch = await fixture.Persistence.RegisterFinanceCreditNoteAsync(otherTenant, fixture.Command(Guid.NewGuid(), [effect], Company, Customer), CancellationToken.None);

        Assert.False(companyMismatch.Succeeded);
        Assert.False(customerMismatch.Succeeded);
        Assert.False(tenantMismatch.Succeeded);
        Assert.Equal("customer_return_not_found", tenantMismatch.Code);
    }

    private static SalesCustomerReturnInvoiceAllocationRecord Allocation(Guid invoiceId, Guid openItemId, decimal quantity, decimal net, decimal tax, decimal gross, string fingerprint) =>
        new(Guid.NewGuid(), invoiceId, openItemId, Guid.NewGuid(), Guid.NewGuid(), 1, quantity, quantity, quantity, 0m, quantity, net, tax, gross, "SAR", Guid.NewGuid(), Guid.NewGuid(), 1, fingerprint, $"invoice-{invoiceId:N}");

    private static ProcurementRequestContext ProcurementContext()
    {
        var foundation = FoundationRequestContext.ForTenant(Guid.NewGuid(), Guid.NewGuid(), TenantContext.ForOrdinaryMembership(TenantValue, new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("hold3-sales")), "sales.customer-return");
        var resolution = new ProcurementTenantContextResolver().Resolve(foundation);
        Assert.True(resolution.Allowed, resolution.Code);
        return resolution.Context!;
    }

    private sealed class SalesFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private SalesFixture(SqliteConnection connection, DbContextOptions options, ProcurementRequestContext procurementContext, IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> allocations, Guid returnId, Guid deliveryId)
        {
            this.connection = connection;
            Options = options;
            ProcurementContext = procurementContext;
            Context = procurementContext.TenantContext;
            Allocations = allocations;
            ReturnId = returnId;
            DeliveryId = deliveryId;
            Persistence = new CustomerReturnPersistence(options);
            PostingJournalId = Guid.NewGuid();
            TaxJournalId = Guid.NewGuid();
        }

        internal DbContextOptions Options { get; }
        internal ProcurementRequestContext ProcurementContext { get; }
        internal TenantContext Context { get; }
        internal IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> Allocations { get; }
        internal Guid ReturnId { get; }
        internal Guid DeliveryId { get; }
        internal Guid PostingJournalId { get; }
        internal Guid TaxJournalId { get; }
        internal CustomerReturnPersistence Persistence { get; }

        internal static async Task<SalesFixture> CreateAsync(IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> allocations, Guid? recognizedInvoiceId = null, decimal returnQuantity = 1m)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var procurementContext = ProcurementContext();
            var returnId = Guid.NewGuid();
            var deliveryId = allocations[0].DeliveryId;
            var orderId = Guid.NewGuid();
            var orderLineId = allocations[0].OrderLineId;
            var taxId = Guid.NewGuid();
            var sourceLine = new SalesCustomerReturnSourceLineRecord(orderLineId, Guid.NewGuid(), "SKU", "Product", Guid.NewGuid(), "EA", returnQuantity, 0m, returnQuantity, 80m, 20m, 100m, null, returnQuantity, null, taxId, Guid.NewGuid(), returnQuantity, returnQuantity, returnQuantity, returnQuantity, 0m, 0m, "Restockable", [], [], null);
            var source = new SalesCustomerReturnSourceRecord(returnId, deliveryId, orderId, 1, Tenant, Company, null, Customer, Guid.NewGuid(), DateTimeOffset.UtcNow, recognizedInvoiceId, recognizedInvoiceId is null ? null : allocations.FirstOrDefault(item => item.InvoiceId == recognizedInvoiceId)?.FinanceOpenItemId, "SAR", [sourceLine], SalesCustomerReturnStatus.Received, SalesCustomerReturnConsequence.CreditNote, [1], allocations);
            var request = new SalesCustomerReturnCreateRequest(deliveryId, new DateOnly(2026, 9, 5), SalesCustomerReturnConsequence.CreditNote, recognizedInvoiceId, [new SalesCustomerReturnLineRequest(orderLineId, returnQuantity)], "hold3");
            var entity = new SalesCustomerReturnEntity(TenantValue, returnId, request, source, Guid.NewGuid(), DateTimeOffset.UtcNow);
            entity.Lines.Add(new SalesCustomerReturnLineEntity(TenantValue, Guid.NewGuid(), returnId, deliveryId, request.Lines[0], sourceLine));
            await using (var db = new SalesDbContext(options, procurementContext.TenantContext))
            {
                await db.Database.EnsureCreatedAsync();
                db.CustomerReturns.Add(entity);
                db.CustomerReturnInvoiceAllocations.AddRange(allocations.Select(item => new SalesCustomerReturnInvoiceAllocationEntity(TenantValue, item.Id, returnId, item)));
                await db.SaveChangesAsync();
            }
            return new SalesFixture(connection, options, procurementContext, allocations, returnId, deliveryId);
        }

        internal SalesCustomerReturnFinanceAllocationEffect Effect(SalesCustomerReturnInvoiceAllocationRecord allocation, decimal? quantity = null, decimal? net = null, decimal? tax = null, decimal? gross = null) =>
            new(allocation.Id, quantity ?? allocation.CommerciallyAcceptedQuantity, net ?? allocation.NetAmount, tax ?? allocation.TaxAmount, gross ?? allocation.GrossAmount, allocation.SourceAllocationFingerprint);

        internal SalesCustomerReturnFinanceEffectCommand Command(Guid creditNoteId, IReadOnlyList<SalesCustomerReturnFinanceAllocationEffect> effects, Guid companyId, Guid customerId) =>
            new(ReturnId, Tenant, creditNoteId, effects.Select(item => Allocations.Single(value => value.Id == item.SourceAllocationId).InvoiceId).Distinct().Single(), effects.Select(item => item.SourceAllocationId).ToArray(), DateTimeOffset.UtcNow, effects.Select(item => Allocations.Single(value => value.Id == item.SourceAllocationId).FinanceOpenItemId).Distinct().Single(), PostingJournalId, [TaxJournalId], effects.Sum(item => item.NetAmount), effects.Sum(item => item.TaxAmount), effects.Sum(item => item.GrossAmount), "SAR", "source", $"effect-{creditNoteId:N}", $"request-{creditNoteId:N}", "Committed", $"downstream-{creditNoteId:N}", effects, companyId, customerId);

        internal Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeAsync(Guid creditNoteId, SalesCustomerReturnInvoiceAllocationRecord allocation, SalesCustomerReturnFinanceAllocationEffect effect, Guid companyId = default, Guid customerId = default)
        {
            if (companyId == Guid.Empty) companyId = Company;
            if (customerId == Guid.Empty) customerId = Customer;
            return Persistence.RegisterFinanceCreditNoteAsync(Context, Command(creditNoteId, [effect], companyId, customerId));
        }

        internal Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> ReverseAsync(Guid creditNoteId, SalesCustomerReturnInvoiceAllocationRecord allocation, SalesCustomerReturnFinanceAllocationEffect effect, Guid reversalJournalId, Guid originalJournalId)
        {
            var command = new SalesCustomerReturnDownstreamReversalCommand(ReturnId, Tenant, "finance", "hold3-reversal", DateTimeOffset.UtcNow, creditNoteId, reversalJournalId, originalJournalId, "reversal-effect", "reversal-request", "Committed", "reversal-downstream", allocation.InvoiceId, allocation.FinanceOpenItemId, originalJournalId, [allocation.Id], [TaxJournalId], effect.NetAmount, effect.TaxAmount, effect.GrossAmount, "SAR", "source", $"effect-{creditNoteId:N}", $"downstream-{creditNoteId:N}", Company, Customer);
            return Persistence.RecordDownstreamReversalAsync(Context, command);
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
