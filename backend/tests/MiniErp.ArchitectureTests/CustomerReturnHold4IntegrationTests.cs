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

public sealed class CustomerReturnHold4IntegrationTests
{
    private static readonly Guid Tenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Company = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Customer = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static TenantId TenantValue => new(Tenant);

    // HOLD-138-O: eligibility must be decided from source.InvoiceAllocations authority, not the
    // single-invoice root FinanceOpenItemId shortcut, which incorrectly blocked valid multi-invoice returns.

    [Fact]
    public async Task O1_multi_invoice_source_with_null_root_open_item_is_eligible_for_credit_note()
    {
        var invoiceA = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var source = Source(deliveryId, orderLineId, recognizedInvoiceId: null, recognizedOpenItemId: null,
            allocations: [Allocation(invoiceA, Guid.NewGuid(), orderLineId), Allocation(invoiceB, Guid.NewGuid(), orderLineId)]);
        var persistence = new EligibilitySpy(source);
        var service = Service(persistence);

        var result = await service.CreateAsync(Context(), Request(deliveryId, SalesCustomerReturnConsequence.CreditNote, null, orderLineId), "hold4-o1");

        Assert.True(result.Succeeded, result.Code);
        Assert.NotNull(persistence.CapturedCommand);
    }

    [Fact]
    public async Task O2_explicit_invoice_filter_matching_an_allocation_is_eligible()
    {
        var invoiceA = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var source = Source(deliveryId, orderLineId, recognizedInvoiceId: null, recognizedOpenItemId: null,
            allocations: [Allocation(invoiceA, Guid.NewGuid(), orderLineId), Allocation(invoiceB, Guid.NewGuid(), orderLineId)]);
        var persistence = new EligibilitySpy(source);
        var service = Service(persistence);

        var result = await service.CreateAsync(Context(), Request(deliveryId, SalesCustomerReturnConsequence.CreditNote, invoiceB, orderLineId), "hold4-o2");

        Assert.True(result.Succeeded, result.Code);
    }

    [Fact]
    public async Task O3_explicit_invoice_filter_not_present_in_allocations_fails_closed()
    {
        var invoiceA = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var source = Source(deliveryId, orderLineId, recognizedInvoiceId: null, recognizedOpenItemId: null,
            allocations: [Allocation(invoiceA, Guid.NewGuid(), orderLineId)]);
        var persistence = new EligibilitySpy(source);
        var service = Service(persistence);

        var result = await service.CreateAsync(Context(), Request(deliveryId, SalesCustomerReturnConsequence.CreditNote, Guid.NewGuid(), orderLineId), "hold4-o3");

        Assert.False(result.Succeeded);
        Assert.Equal("invoice_source_mismatch", result.Code);
        Assert.Null(persistence.CapturedCommand);
    }

    [Fact]
    public async Task O4_zero_invoice_allocation_authority_fails_closed_for_credit_note_but_uninvoiced_remainder_allows_other_consequences()
    {
        var deliveryId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var source = Source(deliveryId, orderLineId, recognizedInvoiceId: null, recognizedOpenItemId: null, allocations: []);
        var persistence = new EligibilitySpy(source);
        var service = Service(persistence);

        var creditNoteResult = await service.CreateAsync(Context(), Request(deliveryId, SalesCustomerReturnConsequence.CreditNote, null, orderLineId), "hold4-o4-a");
        Assert.False(creditNoteResult.Succeeded);
        Assert.Equal("recognized_invoice_required", creditNoteResult.Code);
        Assert.Null(persistence.CapturedCommand);

        var replacementResult = await service.CreateAsync(Context(), Request(deliveryId, SalesCustomerReturnConsequence.ReplacementRequested, null, orderLineId), "hold4-o4-b");
        Assert.True(replacementResult.Succeeded, replacementResult.Code);
    }

    private static SalesCustomerReturnService Service(ISalesCustomerReturnPersistence persistence) =>
        new(persistence, new SalesAuthorizationService(new PurchaseRequestAuthorizationService()));

    private static ProcurementRequestContext Context()
    {
        var tenantContext = TenantContext.ForOrdinaryMembership(TenantValue, new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{Company:D}"), new CorrelationId($"hold4-{Guid.NewGuid():N}"));
        var foundation = FoundationRequestContext.ForTenant(Guid.NewGuid(), Guid.NewGuid(), tenantContext, "tenant.sales.customer-return.create");
        var resolution = new ProcurementTenantContextResolver().Resolve(foundation);
        Assert.True(resolution.Allowed, resolution.Code);
        return resolution.Context!;
    }

    private static SalesCustomerReturnCreateRequest Request(Guid deliveryId, SalesCustomerReturnConsequence consequence, Guid? invoiceId, Guid orderLineId) =>
        new(deliveryId, new DateOnly(2026, 9, 6), consequence, invoiceId, [new SalesCustomerReturnLineRequest(orderLineId, 1m)], "hold4");

    private static SalesCustomerReturnInvoiceAllocationRecord Allocation(Guid invoiceId, Guid openItemId, Guid orderLineId) =>
        new(Guid.NewGuid(), invoiceId, openItemId, Guid.NewGuid(), orderLineId, 1, 1m, 1m, 1m, 0m, 1m, 80m, 20m, 100m, "SAR", Guid.NewGuid(), Guid.NewGuid(), 1, $"allocation-{invoiceId:N}", $"invoice-{invoiceId:N}");

    private static SalesCustomerReturnSourceRecord Source(Guid deliveryId, Guid orderLineId, Guid? recognizedInvoiceId, Guid? recognizedOpenItemId, IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> allocations)
    {
        var sourceLine = new SalesCustomerReturnSourceLineRecord(orderLineId, Guid.NewGuid(), "SKU", "Product", Guid.NewGuid(), "EA", 1m, 0m, 1m, 80m, 20m, 100m, null, 0m, null, null, null, 0m, 0m, 0m, 0m, 0m, 0m, "PendingInspection", null, null, null);
        return new SalesCustomerReturnSourceRecord(Guid.NewGuid(), deliveryId, Guid.NewGuid(), 1, Tenant, Company, null, Customer, Guid.NewGuid(), DateTimeOffset.UtcNow, recognizedInvoiceId, recognizedOpenItemId, "SAR", [sourceLine], SalesCustomerReturnStatus.Approved, SalesCustomerReturnConsequence.None, null, allocations);
    }

    private sealed class EligibilitySpy(SalesCustomerReturnSourceRecord source) : ISalesCustomerReturnPersistence
    {
        internal SalesCustomerReturnCreateCommand? CapturedCommand { get; private set; }
        public Task<IReadOnlyList<SalesCustomerReturnSourceRecord>> ListEligibleSourcesAsync(ProcurementRequestContext c, CancellationToken x = default) => Task.FromResult<IReadOnlyList<SalesCustomerReturnSourceRecord>>([source]);
        public Task<SalesCustomerReturnSourceRecord?> GetEligibleSourceAsync(ProcurementRequestContext c, Guid deliveryId, CancellationToken x = default) => Task.FromResult<SalesCustomerReturnSourceRecord?>(deliveryId == source.DeliveryId ? source : null);
        public Task<SalesCustomerReturnResponse?> GetAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<SalesCustomerReturnResponse?>(null);
        public Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<IReadOnlyList<SalesHistoryResponse>>([]);
        public Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<IReadOnlyList<SalesAuditResponse>>([]);
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateAsync(ProcurementRequestContext c, SalesCustomerReturnCreateCommand command, CancellationToken x = default)
        {
            CapturedCommand = command;
            return Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(null!));
        }
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> MutateAsync(ProcurementRequestContext c, SalesCustomerReturnActionCommand m, CancellationToken x = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext c, SalesCustomerReturnInventoryAcknowledgementCommand m, CancellationToken x = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext c, SalesCustomerReturnInventoryFailureCommand m, CancellationToken x = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext c, SalesCustomerReturnDownstreamReversalCommand m, CancellationToken x = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RegisterFinanceCreditNoteAsync(TenantContext c, SalesCustomerReturnFinanceEffectCommand m, CancellationToken x = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
    }

    // HOLD-138-P: SalesCustomerReturnFinanceEffectEntity.MatchesPost must canonicalize and exactly
    // compare TaxJournalIds (order-insensitive, duplicate-rejecting, positive-tax-requires-non-empty).

    [Fact]
    public async Task P1_exact_retry_same_tax_journal_order_matches_and_converges()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var taxA = Guid.NewGuid();
        var taxB = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var first = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA, taxB]);
        var retry = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA, taxB]);

        Assert.True(first.Succeeded, first.Code);
        Assert.True(retry.Succeeded, retry.Code);
    }

    [Fact]
    public async Task P2_retry_with_same_set_different_order_matches_and_converges()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var taxA = Guid.NewGuid();
        var taxB = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var first = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA, taxB]);
        var retry = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxB, taxA]);

        Assert.True(first.Succeeded, first.Code);
        Assert.True(retry.Succeeded, retry.Code);
    }

    [Fact]
    public async Task P3_retry_with_replaced_tax_journal_id_fails_closed()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var taxA = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var first = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA]);
        var retry = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [Guid.NewGuid()]);

        Assert.True(first.Succeeded, first.Code);
        Assert.False(retry.Succeeded);
        Assert.Equal("finance_effect_mismatch", retry.Code);
    }

    [Fact]
    public async Task P4_retry_with_additional_tax_journal_id_fails_closed()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var taxA = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var first = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA]);
        var retry = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA, Guid.NewGuid()]);

        Assert.True(first.Succeeded, first.Code);
        Assert.False(retry.Succeeded);
        Assert.Equal("finance_effect_mismatch", retry.Code);
    }

    [Fact]
    public async Task P5_retry_with_fewer_tax_journal_ids_fails_closed()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var taxA = Guid.NewGuid();
        var taxB = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var first = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA, taxB]);
        var retry = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA]);

        Assert.True(first.Succeeded, first.Code);
        Assert.False(retry.Succeeded);
        Assert.Equal("finance_effect_mismatch", retry.Code);
    }

    [Fact]
    public async Task P6_duplicate_tax_journal_ids_in_the_incoming_command_are_rejected_on_first_post()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var taxA = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();

        var result = await fixture.CreateFinanceEffectDirectAsync(creditNoteId, fixture.Effect(allocation), [taxA, taxA]);

        Assert.False(result.Succeeded);
        Assert.Equal("finance_effect_mismatch", result.Code);
    }

    [Fact]
    public async Task P7_duplicate_tax_journal_ids_on_retry_never_match_the_canonical_stored_set()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var taxA = Guid.NewGuid();
        var creditNoteId = Guid.NewGuid();
        var first = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA]);
        var retry = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), [taxA, taxA]);

        Assert.True(first.Succeeded, first.Code);
        Assert.False(retry.Succeeded);
        Assert.Equal("finance_effect_mismatch", retry.Code);
    }

    [Fact]
    public async Task P8_guid_empty_tax_journal_id_is_rejected_on_first_post()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid());
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var creditNoteId = Guid.NewGuid();

        var result = await fixture.CreateFinanceEffectDirectAsync(creditNoteId, fixture.Effect(allocation), [Guid.Empty]);

        Assert.False(result.Succeeded);
        Assert.Equal("finance_effect_mismatch", result.Code);
    }

    [Fact]
    public async Task P9_positive_tax_amount_requires_at_least_one_tax_journal_id()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid(), net: 80m, tax: 20m, gross: 100m);
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var creditNoteId = Guid.NewGuid();

        var result = await fixture.CreateFinanceEffectDirectAsync(creditNoteId, fixture.Effect(allocation), []);

        Assert.False(result.Succeeded);
        Assert.Equal("finance_effect_mismatch", result.Code);
    }

    [Fact]
    public async Task P10_zero_tax_amount_permits_empty_tax_journal_ids_and_reversal_preserves_the_original_set()
    {
        var allocation = Allocation2(Guid.NewGuid(), Guid.NewGuid(), net: 100m, tax: 0m, gross: 100m);
        await using var fixture = await SalesFixture.CreateAsync([allocation]);
        var creditNoteId = Guid.NewGuid();

        var posted = await fixture.AcknowledgeAsync(creditNoteId, fixture.Effect(allocation), []);
        Assert.True(posted.Succeeded, posted.Code);

        await using var db = new SalesDbContext(fixture.Options, fixture.Context);
        var stored = await db.CustomerReturnFinanceEffects.SingleAsync();
        Assert.Equal("[]", stored.TaxJournalIdsJson);
    }

    private static SalesCustomerReturnInvoiceAllocationRecord Allocation2(Guid invoiceId, Guid openItemId, decimal net = 80m, decimal tax = 20m, decimal gross = 100m) =>
        new(Guid.NewGuid(), invoiceId, openItemId, Guid.NewGuid(), Guid.NewGuid(), 1, 1m, 1m, 1m, 0m, 1m, net, tax, gross, "SAR", Guid.NewGuid(), Guid.NewGuid(), 1, $"allocation-{invoiceId:N}", $"invoice-{invoiceId:N}");

    private static ProcurementRequestContext Hold4ProcurementContext()
    {
        var foundation = FoundationRequestContext.ForTenant(Guid.NewGuid(), Guid.NewGuid(), TenantContext.ForOrdinaryMembership(TenantValue, new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("hold4-sales")), "sales.customer-return");
        var resolution = new ProcurementTenantContextResolver().Resolve(foundation);
        Assert.True(resolution.Allowed, resolution.Code);
        return resolution.Context!;
    }

    private sealed class SalesFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private SalesFixture(SqliteConnection connection, DbContextOptions options, ProcurementRequestContext procurementContext, IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> allocations, Guid returnId, Guid deliveryId, Guid recognizedInvoiceId, Guid? recognizedOpenItemId)
        {
            this.connection = connection;
            Options = options;
            ProcurementContext = procurementContext;
            Context = procurementContext.TenantContext;
            Allocations = allocations;
            ReturnId = returnId;
            DeliveryId = deliveryId;
            RecognizedInvoiceId = recognizedInvoiceId;
            RecognizedOpenItemId = recognizedOpenItemId;
            Persistence = new CustomerReturnPersistence(options);
            PostingJournalId = Guid.NewGuid();
        }

        internal DbContextOptions Options { get; }
        internal ProcurementRequestContext ProcurementContext { get; }
        internal TenantContext Context { get; }
        internal IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> Allocations { get; }
        internal Guid ReturnId { get; }
        internal Guid DeliveryId { get; }
        internal Guid RecognizedInvoiceId { get; }
        internal Guid? RecognizedOpenItemId { get; }
        internal Guid PostingJournalId { get; }
        internal CustomerReturnPersistence Persistence { get; }

        internal static async Task<SalesFixture> CreateAsync(IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> allocations)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var procurementContext = Hold4ProcurementContext();
            var returnId = Guid.NewGuid();
            var deliveryId = allocations[0].DeliveryId;
            var orderId = Guid.NewGuid();
            var orderLineId = allocations[0].OrderLineId;
            var taxId = Guid.NewGuid();
            var recognizedInvoiceId = allocations[0].InvoiceId;
            var recognizedOpenItemId = allocations[0].FinanceOpenItemId;
            var sourceLine = new SalesCustomerReturnSourceLineRecord(orderLineId, Guid.NewGuid(), "SKU", "Product", Guid.NewGuid(), "EA", 1m, 0m, 1m, 80m, 20m, 100m, null, 1m, null, taxId, Guid.NewGuid(), 1m, 1m, 1m, 1m, 0m, 0m, "Restockable", [], [], null);
            var source = new SalesCustomerReturnSourceRecord(returnId, deliveryId, orderId, 1, Tenant, Company, null, Customer, Guid.NewGuid(), DateTimeOffset.UtcNow, recognizedInvoiceId, recognizedOpenItemId, "SAR", [sourceLine], SalesCustomerReturnStatus.Received, SalesCustomerReturnConsequence.CreditNote, [1], allocations);
            var request = new SalesCustomerReturnCreateRequest(deliveryId, new DateOnly(2026, 9, 6), SalesCustomerReturnConsequence.CreditNote, recognizedInvoiceId, [new SalesCustomerReturnLineRequest(orderLineId, 1m)], "hold4");
            var entity = new SalesCustomerReturnEntity(TenantValue, returnId, request, source, Guid.NewGuid(), DateTimeOffset.UtcNow);
            entity.Lines.Add(new SalesCustomerReturnLineEntity(TenantValue, Guid.NewGuid(), returnId, deliveryId, request.Lines[0], sourceLine));
            await using (var db = new SalesDbContext(options, procurementContext.TenantContext))
            {
                await db.Database.EnsureCreatedAsync();
                db.CustomerReturns.Add(entity);
                db.CustomerReturnInvoiceAllocations.AddRange(allocations.Select(item => new SalesCustomerReturnInvoiceAllocationEntity(TenantValue, item.Id, returnId, item)));
                await db.SaveChangesAsync();
            }
            return new SalesFixture(connection, options, procurementContext, allocations, returnId, deliveryId, recognizedInvoiceId, recognizedOpenItemId);
        }

        internal SalesCustomerReturnFinanceAllocationEffect Effect(SalesCustomerReturnInvoiceAllocationRecord allocation) =>
            new(allocation.Id, allocation.CommerciallyAcceptedQuantity, allocation.NetAmount, allocation.TaxAmount, allocation.GrossAmount, allocation.SourceAllocationFingerprint);

        internal SalesCustomerReturnFinanceEffectCommand Command(Guid creditNoteId, SalesCustomerReturnFinanceAllocationEffect effect, IReadOnlyList<Guid> taxJournalIds) =>
            new(ReturnId, Tenant, creditNoteId, RecognizedInvoiceId, [effect.SourceAllocationId], DateTimeOffset.UtcNow, RecognizedOpenItemId, PostingJournalId, taxJournalIds, effect.NetAmount, effect.TaxAmount, effect.GrossAmount, "SAR", "source", $"effect-{creditNoteId:N}", $"request-{creditNoteId:N}", "Committed", $"downstream-{creditNoteId:N}", [effect], Company, Customer);

        internal Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeAsync(Guid creditNoteId, SalesCustomerReturnFinanceAllocationEffect effect, IReadOnlyList<Guid> taxJournalIds) =>
            Persistence.RegisterFinanceCreditNoteAsync(Context, Command(creditNoteId, effect, taxJournalIds));

        internal Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateFinanceEffectDirectAsync(Guid creditNoteId, SalesCustomerReturnFinanceAllocationEffect effect, IReadOnlyList<Guid> taxJournalIds) =>
            Persistence.RegisterFinanceCreditNoteAsync(Context, Command(creditNoteId, effect, taxJournalIds));

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
