using System.Text.Json;
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

/// <summary>HOLD-138-S: use the real Sales relational source, service, and persistence boundary.</summary>
public sealed class CustomerReturnHold5IntegrationTests
{
    private static readonly Guid Tenant = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Company = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Customer = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static TenantId TenantValue => new(Tenant);

    [Fact]
    public async Task S1_real_multi_invoice_create_persists_exact_invoice_open_item_allocations()
    {
        await using var fixture = await Fixture.CreateAsync(5m, [new InvoiceSpec(2m), new InvoiceSpec(3m)]);
        var created = await fixture.CreateAsync(fixture.PrimaryLineId, 5m);

        Assert.True(created.Succeeded, created.Code);
        await using var db = fixture.CreateDb();
        var allocations = await db.CustomerReturnInvoiceAllocations.OrderBy(item => item.InvoiceId).ToListAsync();
        Assert.Equal(2, allocations.Count);
        Assert.Null((await db.CustomerReturns.SingleAsync()).FinanceOpenItemId);
        Assert.Equal(5m, allocations.Sum(item => item.ReturnQuantity));
        Assert.All(allocations, item => Assert.NotNull(item.FinanceOpenItemId));
        Assert.Equal(fixture.InvoiceIds.OrderBy(item => item), allocations.Select(item => item.InvoiceId).OrderBy(item => item));
    }

    [Fact]
    public async Task S2_real_post_create_acknowledgements_keep_each_invoice_open_item_lineage()
    {
        await using var fixture = await Fixture.CreateAsync(5m, [new InvoiceSpec(2m), new InvoiceSpec(3m)]);
        var created = await fixture.CreateAsync(fixture.PrimaryLineId, 5m);
        Assert.True(created.Succeeded, created.Code);
        var accepted = await fixture.AcceptAsync(created.Value!.Id, fixture.PrimaryLineId, 5m);
        Assert.True(accepted.Succeeded, accepted.Code);
        var posted = await fixture.PostEachAllocationAsync(created.Value.Id);
        Assert.All(posted, result => Assert.True(result.Succeeded, result.Code));

        await using var db = fixture.CreateDb();
        var effects = await db.CustomerReturnFinanceEffects.Include(item => item.Allocations).OrderBy(item => item.InvoiceId).ToListAsync();
        Assert.Equal(2, effects.Count);
        Assert.Equal(2, effects.SelectMany(item => item.Allocations).Count());
        Assert.Equal(fixture.InvoiceIds.OrderBy(item => item), effects.Select(item => item.InvoiceId).OrderBy(item => item));
        Assert.Equal(fixture.OpenItemIds.OrderBy(item => item), effects.Select(item => item.FinanceOpenItemId).OrderBy(item => item));
    }

    [Fact]
    public async Task S3_requested_only_uninvoiced_line_fails_before_any_return_evidence_is_written()
    {
        await using var fixture = await Fixture.CreateAsync(1m, [new InvoiceSpec(1m, useSecondaryLine: true)], secondaryDeliveredQuantity: 1m);
        var result = await fixture.CreateAsync(fixture.PrimaryLineId, 1m);

        Assert.False(result.Succeeded);
        Assert.Equal("recognized_invoice_required", result.Code);
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.CustomerReturns.ToListAsync());
        Assert.Empty(await db.CustomerReturnInvoiceAllocations.ToListAsync());
        Assert.Empty(await db.History.Where(item => item.DocumentType == "customer-return").ToListAsync());
        Assert.Empty(await db.Audit.Where(item => item.DocumentType == "customer-return").ToListAsync());
    }

    [Fact]
    public async Task S4_explicit_invoice_on_a_different_requested_line_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync(1m, [new InvoiceSpec(1m, useSecondaryLine: true)], secondaryDeliveredQuantity: 1m);
        var result = await fixture.CreateAsync(fixture.PrimaryLineId, 1m, fixture.InvoiceIds.Single());

        Assert.False(result.Succeeded);
        Assert.Equal("invoice_source_mismatch", result.Code);
        await using var db = fixture.CreateDb();
        Assert.Empty(await db.CustomerReturns.ToListAsync());
        Assert.Empty(await db.CustomerReturnInvoiceAllocations.ToListAsync());
    }

    [Fact]
    public async Task S5_mixed_coverage_persists_full_physical_return_but_only_recognized_finance_authority()
    {
        await using var fixture = await Fixture.CreateAsync(5m, [new InvoiceSpec(3m)]);
        var created = await fixture.CreateAsync(fixture.PrimaryLineId, 5m);

        Assert.True(created.Succeeded, created.Code);
        await using var db = fixture.CreateDb();
        Assert.Equal(5m, (await db.CustomerReturnLines.SingleAsync()).ReturnQuantity);
        var allocation = await db.CustomerReturnInvoiceAllocations.SingleAsync();
        Assert.Equal(3m, allocation.ReturnQuantity);
        Assert.Equal(3m, allocation.RecognizedQuantity);
    }

    [Fact]
    public async Task S6_explicit_invoice_filter_persists_only_that_invoice_authority()
    {
        await using var fixture = await Fixture.CreateAsync(4m, [new InvoiceSpec(2m), new InvoiceSpec(2m)]);
        var invoiceB = fixture.InvoiceIds.Last();
        var created = await fixture.CreateAsync(fixture.PrimaryLineId, 2m, invoiceB);

        Assert.True(created.Succeeded, created.Code);
        await using var db = fixture.CreateDb();
        var allocation = await db.CustomerReturnInvoiceAllocations.SingleAsync();
        Assert.Equal(invoiceB, allocation.InvoiceId);
        Assert.Equal(2m, allocation.ReturnQuantity);
    }

    [Fact]
    public async Task S7_persisted_finance_coverage_never_exceeds_requested_quantity()
    {
        await using var fixture = await Fixture.CreateAsync(5m, [new InvoiceSpec(5m)]);
        var created = await fixture.CreateAsync(fixture.PrimaryLineId, 2m);

        Assert.True(created.Succeeded, created.Code);
        await using var db = fixture.CreateDb();
        var allocation = await db.CustomerReturnInvoiceAllocations.SingleAsync();
        Assert.Equal(2m, allocation.ReturnQuantity);
        Assert.Equal(80m, allocation.NetAmount);
        Assert.Equal(20m, allocation.TaxAmount);
        Assert.Equal(100m, allocation.GrossAmount);
    }

    private sealed record InvoiceSpec(decimal Quantity, bool useSecondaryLine = false);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ProcurementRequestContext procurement;
        private readonly DbContextOptions options;
        private Fixture(SqliteConnection connection, DbContextOptions options, ProcurementRequestContext procurement, Guid deliveryId, Guid primaryLineId, Guid secondaryLineId, IReadOnlyList<Guid> invoiceIds, IReadOnlyList<Guid> openItemIds)
        { this.connection = connection; this.options = options; this.procurement = procurement; DeliveryId = deliveryId; PrimaryLineId = primaryLineId; SecondaryLineId = secondaryLineId; InvoiceIds = invoiceIds; OpenItemIds = openItemIds; Persistence = new CustomerReturnPersistence(options); }
        internal Guid DeliveryId { get; }
        internal Guid PrimaryLineId { get; }
        internal Guid SecondaryLineId { get; }
        internal IReadOnlyList<Guid> InvoiceIds { get; }
        internal IReadOnlyList<Guid> OpenItemIds { get; }
        internal CustomerReturnPersistence Persistence { get; }
        internal SalesDbContext CreateDb() => new(options, procurement.TenantContext);

        internal static async Task<Fixture> CreateAsync(decimal primaryDeliveredQuantity, IReadOnlyList<InvoiceSpec> invoices, decimal secondaryDeliveredQuantity = 0m)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var procurement = Context();
            var now = DateTimeOffset.UtcNow;
            var orderId = Guid.NewGuid(); var deliveryId = Guid.NewGuid(); var primaryLineId = Guid.NewGuid(); var secondaryLineId = Guid.NewGuid();
            var product = Guid.NewGuid(); var uom = Guid.NewGuid();
            var primary = new SalesQuotationLineResponse(primaryLineId, product, "SKU-1", "Product 1", uom, "EA", primaryDeliveredQuantity, 50m, 50m, 0m, 0m, 50m, primaryDeliveredQuantity * 50m, null, null, null, "hold5", null, false, null, null, null, null);
            var lines = secondaryDeliveredQuantity > 0m
                ? new[] { primary, new SalesQuotationLineResponse(secondaryLineId, Guid.NewGuid(), "SKU-2", "Product 2", Guid.NewGuid(), "EA", secondaryDeliveredQuantity, 50m, 50m, 0m, 0m, 50m, secondaryDeliveredQuantity * 50m, null, null, null, "hold5", null, false, null, null, null, null) }
                : new[] { primary };
            var linesJson = JsonSerializer.Serialize(lines);
            var write = lines.Select(item => new SalesLineWriteModel(item.Id, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureId, item.UnitOfMeasureCode, item.Quantity, item.UnitPrice, item.UnitPrice, 0m, 0m, item.UnitPrice, item.LineTotal, null, null, null, "hold5", null, false, null, null, null, null)).ToArray();
            var quote = new SalesQuotationEntity(TenantValue, new SalesQuotationWriteModel(Guid.NewGuid(), Company, null, Customer, "CUST", "Customer", new DateOnly(2026, 9, 6), new DateOnly(2026, 12, 31), Guid.NewGuid(), "SAR", null, null, null, write, lines.Sum(item => item.LineTotal), 0m, 0m, lines.Sum(item => item.LineTotal)), "Q-H5", linesJson, "{}", now);
            var order = new SalesOrderEntity(TenantValue, quote, Guid.NewGuid(), "SO-H5", linesJson, "{}", now);
            var deliveryLines = new List<SalesDeliveryRequestLine> { new(primaryLineId, Guid.NewGuid(), primaryDeliveredQuantity) };
            if (secondaryDeliveredQuantity > 0m) deliveryLines.Add(new(secondaryLineId, Guid.NewGuid(), secondaryDeliveredQuantity));
            var delivery = new SalesDeliveryEntity(TenantValue, deliveryId, orderId, 1, Company, null, Customer, Guid.NewGuid(), JsonSerializer.Serialize(deliveryLines), "{}", Guid.NewGuid(), "hold5-delivery", now);
            // SalesOrderEntity owns its generated id, so construct the delivery against the actual order.
            delivery = new SalesDeliveryEntity(TenantValue, deliveryId, order.Id, 1, Company, null, Customer, Guid.NewGuid(), JsonSerializer.Serialize(deliveryLines), "{}", Guid.NewGuid(), "hold5-delivery", now);
            delivery.Posted([Guid.NewGuid()], now);
            var invoiceIds = new List<Guid>(); var openItemIds = new List<Guid>();
            var invoiceEntities = new List<SalesInvoiceRequestEntity>();
            foreach (var spec in invoices)
            {
                var invoiceId = Guid.NewGuid(); var openItemId = Guid.NewGuid(); var lineId = spec.useSecondaryLine ? secondaryLineId : primaryLineId;
                var taxId = Guid.NewGuid(); var taxVersion = Guid.NewGuid();
                var evidence = new SalesInvoiceLineEvidence(lineId, spec.Quantity, spec.Quantity, spec.Quantity, spec.Quantity * 40m, spec.Quantity * 10m, spec.Quantity * 50m, new SalesInvoiceTaxEvidence(taxId, "VAT", taxVersion, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, 15m, 10m, spec.Quantity * 10m, "VAT"), [new SalesInvoiceSourceAllocation(deliveryId, lineId, 1, spec.Quantity, 0m, spec.Quantity, 0m)]);
                var json = JsonSerializer.Serialize(new { Lines = new[] { new SalesInvoiceRequestLine(lineId, spec.Quantity) }, Evidence = new[] { evidence } });
                var invoice = new SalesInvoiceRequestEntity(TenantValue, invoiceId, order.Id, 1, deliveryId, Company, null, Customer, new DateOnly(2026, 9, 6), json, spec.Quantity * 50m, "SAR", "{}", Guid.NewGuid(), $"hold5-{invoiceId:N}", now);
                invoice.Posted(openItemId, now); invoiceEntities.Add(invoice); invoiceIds.Add(invoiceId); openItemIds.Add(openItemId);
            }
            await using (var db = new SalesDbContext(options, procurement.TenantContext))
            {
                await db.Database.EnsureCreatedAsync();
                db.Quotations.Add(quote); db.Orders.Add(order); db.Deliveries.Add(delivery); db.InvoiceRequests.AddRange(invoiceEntities);
                await db.SaveChangesAsync();
            }
            return new Fixture(connection, options, procurement, deliveryId, primaryLineId, secondaryLineId, invoiceIds, openItemIds);
        }

        internal Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateAsync(Guid lineId, decimal quantity, Guid? invoiceId = null) =>
            new SalesCustomerReturnService(Persistence, new SalesAuthorizationService(new PurchaseRequestAuthorizationService())).CreateAsync(procurement, new SalesCustomerReturnCreateRequest(DeliveryId, new DateOnly(2026, 9, 6), SalesCustomerReturnConsequence.CreditNote, invoiceId, [new SalesCustomerReturnLineRequest(lineId, quantity)], "hold5"), $"hold5-create-{Guid.NewGuid():N}");

        internal Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcceptAsync(Guid returnId, Guid lineId, decimal quantity) =>
            Persistence.AcknowledgeInventoryAsync(procurement.TenantContext, new SalesCustomerReturnInventoryAcknowledgementCommand(returnId, Tenant, Guid.NewGuid(), "inventory-effect", "inventory-request", "hold5-inventory", "physical", "inspection", [new SalesCustomerReturnInventoryAcknowledgementLine(lineId, quantity, quantity, quantity, quantity, 0m, 0m, "Restockable", [], [], null)], "Committed", "hold5", DateTimeOffset.UtcNow));

        internal async Task<IReadOnlyList<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>>> PostEachAllocationAsync(Guid returnId)
        {
            await using var db = CreateDb();
            var allocations = await db.CustomerReturnInvoiceAllocations.OrderBy(item => item.InvoiceId).ToListAsync();
            var results = new List<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>>();
            foreach (var allocation in allocations)
            {
                var effect = new SalesCustomerReturnFinanceAllocationEffect(allocation.Id, allocation.CommerciallyAcceptedQuantity, allocation.NetAmount, allocation.TaxAmount, allocation.GrossAmount, allocation.SourceAllocationFingerprint);
                var command = new SalesCustomerReturnFinanceEffectCommand(returnId, Tenant, Guid.NewGuid(), allocation.InvoiceId, [allocation.Id], DateTimeOffset.UtcNow, allocation.FinanceOpenItemId, Guid.NewGuid(), [Guid.NewGuid()], effect.NetAmount, effect.TaxAmount, effect.GrossAmount, "SAR", "hold5-source", $"hold5-effect-{Guid.NewGuid():N}", $"hold5-request-{Guid.NewGuid():N}", "Committed", $"hold5-downstream-{Guid.NewGuid():N}", [effect], Company, Customer);
                results.Add(await Persistence.RegisterFinanceCreditNoteAsync(procurement.TenantContext, command));
            }
            return results;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private static ProcurementRequestContext Context()
    {
        var foundation = FoundationRequestContext.ForTenant(Guid.NewGuid(), Guid.NewGuid(), TenantContext.ForOrdinaryMembership(TenantValue, new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{Company:D}"), new CorrelationId("hold5-sales")), "tenant.sales.customer-return.create");
        var result = new ProcurementTenantContextResolver().Resolve(foundation);
        Assert.True(result.Allowed, result.Code);
        return result.Context!;
    }
}
