using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class ReportingHold1SourceAdapterTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Branch = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Warehouse = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Actor = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Customer = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task Finance_cash_movement_adapter_filters_dates_and_pages_from_settlement_truth()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(Tenant), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("report-finance"), actorId: Actor);
        var linkedAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000100");
        var paymentMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000101");
        var cashAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000102");
        await using (var db = new FinanceDbContext(options, tenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            var account = new FinanceAccountEntity(new TenantId(Tenant), linkedAccountId, new FinanceAccountCommand(Company, "REPORTING-CASH-GL", "Reporting cash GL", null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "reporting-cash-gl", "reporting-cash-gl"));
            var paymentMethod = new FinancePaymentMethodEntity(new TenantId(Tenant), new FinancePaymentMethodCommand(Company, "REPORTING-PM", "Reporting payment method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2026, 1, 1), null, paymentMethodId, null, "reporting-pm", "reporting-pm"));
            var cashAccount = new FinanceCashAccountEntity(new TenantId(Tenant), new FinanceCashAccountCommand(Company, "REPORTING-CASH", "Reporting cash account", null, FinanceCashAccountKind.Bank, "SAR", linkedAccountId, null, new DateOnly(2026, 1, 1), null, cashAccountId, null, "reporting-cash", "reporting-cash"), "SAR");
            db.Accounts.Add(account);
            db.PaymentMethods.Add(paymentMethod);
            db.CashAccounts.Add(cashAccount);
            var older = new FinanceSettlementDocumentEntity(new TenantId(Tenant), SettlementCommand(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), new DateOnly(2026, 1, 10), 10m, cashAccountId, paymentMethodId), "SAR", "SAR", 10m, Actor, DateTimeOffset.UtcNow.AddMinutes(-2));
            var newer = new FinanceSettlementDocumentEntity(new TenantId(Tenant), SettlementCommand(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), new DateOnly(2026, 1, 20), 20m, cashAccountId, paymentMethodId), "SAR", "SAR", 20m, Actor, DateTimeOffset.UtcNow);
            older.SetStatus(FinanceSettlementDocumentStatus.Posted, Actor, DateTimeOffset.UtcNow.AddMinutes(-1));
            newer.SetStatus(FinanceSettlementDocumentStatus.Posted, Actor, DateTimeOffset.UtcNow);
            db.SettlementDocuments.AddRange(older, newer);
            await db.SaveChangesAsync();
        }

        var persistence = new FinanceSettlementPersistence(
            options,
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(Tenant, Company, "Reporting Company", "SAR")]),
            new UnavailableMasterDataExchangeRatePersistence(),
            new UnavailableCustomerPersistence(),
            new UnavailableSupplierPersistence(),
            new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            new UnavailableFinanceSupplierInvoiceSourceProvider());
        var context = FinanceContext(tenantContext);
        var page = await persistence.ListCashMovementReportingPageAsync(context, Company, new DateOnly(2026, 1, 15), null, ReportingPageRequest.Create(1, 1, "amount", "desc"));

        Assert.Equal(1, page.TotalRows);
        var row = Assert.Single(page.Rows);
        Assert.Equal(20m, row.Amount);
        Assert.Equal(FinanceSettlementDocumentStatus.Posted, row.Status);
        Assert.Equal(Company, row.CompanyId);
        Assert.Equal("documentDate,id", page.StableSortKey);
    }

    [Fact]
    public async Task Procurement_match_adapter_preserves_exception_variances_and_scope()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(Tenant), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{Company:D}"), new CorrelationId("report-procurement"), Actor);
        var purchaseRequestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000013");
        var quotationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000014");
        var decisionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000015");
        var evaluationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000010");
        var handoffId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000011");
        var orderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000012");
        var evaluatedAt = new DateTimeOffset(2026, 2, 5, 10, 0, 0, TimeSpan.Zero);
        var variance = new PurchaseInvoiceMatchVarianceRecord("Quantity", Guid.NewGuid(), Guid.NewGuid(), 10m, 12m, 2m, 0m, "SAR", "over-received");
        await using (var db = new ProcurementDbContext(options, tenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            var scope = new PurchaseRequestScope(Tenant, Company, Branch);
            var purchaseRequest = new PurchaseRequestEntity(purchaseRequestId, new TenantId(Tenant), Company, Branch, Actor, "reporting fixture", evaluatedAt);
            var quotationCommand = new SupplierQuotationCreateCommand(quotationId, purchaseRequestId, scope, Actor, new SupplierQuotationSupplierSnapshot(Guid.NewGuid(), "SUP-REPORT", "Reporting Supplier"), "QUOT-REPORT", new DateOnly(2026, 2, 1), null, new SupplierQuotationCurrencySnapshot(Guid.NewGuid(), "SAR", "Saudi Riyal"), null, null, null, null, null, [], [], evaluatedAt, "quotation-reporting");
            var quotation = new SupplierQuotationEntity(quotationCommand, new TenantId(Tenant));
            var quotationRecord = new SupplierQuotationRecord(quotationId, Tenant, purchaseRequestId, scope, Actor, quotationCommand.Supplier, SupplierQuotationStatus.Draft, quotationCommand.SupplierQuotationReference, quotationCommand.OfferDate, quotationCommand.ValidUntil, quotationCommand.Currency, quotationCommand.PaymentTerm, quotationCommand.DeliveryTerms, quotationCommand.OfferedDeliveryDate, quotationCommand.OfferedDeliveryLeadTime, quotationCommand.Notes, [], [], evaluatedAt, evaluatedAt, null, Guid.NewGuid().ToByteArray());
            var decisionCommand = new SupplierSourceDecisionCommand(decisionId, purchaseRequestId, scope, quotationId, Actor, evaluatedAt, "reporting fixture", null, null, null, "reporting-comparison", "{}", [], "decision-reporting");
            var decision = new SupplierSourceDecisionEntity(decisionCommand, new TenantId(Tenant), quotationRecord);
            var orderCommand = new PurchaseOrderCreateCommand(orderId, scope, Actor, new PurchaseOrderSourceResponse(purchaseRequestId, quotationId, decisionId, "PR-REPORT", "reporting fixture", "QUOT-REPORT", new PurchaseOrderSupplierResponse(quotationCommand.Supplier.Id, quotationCommand.Supplier.Code, quotationCommand.Supplier.Name), new PurchaseOrderCurrencyResponse(quotationCommand.Currency.Id, quotationCommand.Currency.Code, quotationCommand.Currency.Name), null, "reporting fixture", evaluatedAt), [], evaluatedAt, "order-reporting");
            var purchaseOrder = new PurchaseOrderEntity(orderCommand, new TenantId(Tenant));
            var handoffCommand = new PurchaseInvoiceHandoffCreateCommand(handoffId, scope, orderId, Actor, "INV-REPORT", new DateOnly(2026, 2, 5), "reporting fixture", [], evaluatedAt, "handoff-reporting");
            var handoff = new PurchaseInvoiceHandoffEntity(handoffCommand, new TenantId(Tenant), quotationCommand.Supplier.Id, quotationCommand.Supplier.Code, quotationCommand.Supplier.Name, quotationCommand.Currency.Code);
            db.PurchaseRequests.Add(purchaseRequest);
            db.SupplierQuotations.Add(quotation);
            db.SupplierSourceDecisions.Add(decision);
            db.PurchaseOrders.Add(purchaseOrder);
            db.PurchaseInvoiceHandoffs.Add(handoff);
            var command = new PurchaseInvoiceMatchEvaluateCommand(handoffId, [1], Actor, PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(evaluatedAt), null, evaluatedAt, "report-procurement", null, null);
            var entity = new PurchaseInvoiceMatchEvaluationEntity(new TenantId(Tenant), command, evaluationId, orderId, new PurchaseRequestScope(Tenant, Company, Branch), PurchaseInvoiceMatchResult.ExceptionHold, "match-fingerprint", [1], [1], Guid.NewGuid(), 1, "{}", null, JsonSerializer.Serialize(new[] { variance }), "{}");
            entity.TouchVersion();
            db.PurchaseInvoiceMatchEvaluations.Add(entity);
            await db.SaveChangesAsync();
        }

        var persistence = new PurchaseInvoiceMatchPersistence(options);
        var page = await persistence.ListReportingPageAsync(tenantContext, PurchaseInvoiceMatchResult.ExceptionHold, null, null, ReportingPageRequest.Create(1, 10, "status", "asc"));

        Assert.Equal(1, page.TotalRows);
        var row = Assert.Single(page.Rows);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, row.Result);
        Assert.Equal(PurchaseInvoiceMatchLifecycle.Current, row.Lifecycle);
        Assert.Equal("Quantity", Assert.Single(row.Variances).Classification);
        Assert.Equal(Company, row.Scope.CompanyId);
        Assert.Equal("evaluatedAt,id", page.StableSortKey);
    }

    [Fact]
    public async Task Sales_fulfillment_adapter_reads_delivery_handoff_with_warehouse_scope()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(Tenant), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Warehouse:{Warehouse:D}"), new CorrelationId("report-sales-delivery"), Actor);
        var deliveryId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000020");
        var movementId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000021");
        var secondDeliveryId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000022");
        var secondMovementId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000023");
        await using (var db = new SalesDbContext(options, tenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            var delivery = new SalesDeliveryEntity(new TenantId(Tenant), deliveryId, Guid.NewGuid(), 2, Company, Branch, Customer, Warehouse, JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(Guid.NewGuid(), Guid.NewGuid(), 3m) }), "{}", Actor, "report-delivery", DateTimeOffset.UtcNow.AddMinutes(-1));
            delivery.Posted([movementId], DateTimeOffset.UtcNow);
            var secondDelivery = new SalesDeliveryEntity(new TenantId(Tenant), secondDeliveryId, Guid.NewGuid(), 1, Company, Branch, Customer, Warehouse, JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(Guid.NewGuid(), Guid.NewGuid(), 5m) }), "{}", Actor, "report-delivery-2", DateTimeOffset.UtcNow);
            secondDelivery.Posted([secondMovementId], DateTimeOffset.UtcNow);
            db.Deliveries.AddRange(delivery, secondDelivery);
            await db.SaveChangesAsync();
        }

        var persistence = new SalesPersistence(options);
        var context = ProcurementContext(tenantContext);
        var page = await persistence.ListFulfillmentReportingPageAsync(context, SalesDeliveryStatus.Posted, ReportingPageRequest.Create(1, 1, "warehouse", "asc"));

        Assert.Equal(2, page.TotalRows);
        var row = Assert.Single(page.Rows);
        Assert.Equal(deliveryId, row.Id);
        Assert.Equal(Warehouse, row.WarehouseId);
        Assert.Equal(3m, row.RequestedQuantity);
        Assert.Equal(1, row.MovementCount);
        Assert.Equal("Committed", row.Handoff.DownstreamCommitState);
        Assert.Equal("Reconciled", row.Handoff.ReconciliationStatus);
        Assert.NotNull(page.DataAsOf);

        var secondPage = await persistence.ListFulfillmentReportingPageAsync(context, SalesDeliveryStatus.Posted, ReportingPageRequest.Create(2, 1, "warehouse", "asc"));
        var secondRow = Assert.Single(secondPage.Rows);
        Assert.Equal(secondDeliveryId, secondRow.Id);
        Assert.Equal(5m, secondRow.RequestedQuantity);
        Assert.Equal(secondMovementId, Assert.Single(secondRow.Handoff.DownstreamEffectIds));
    }

    [Fact]
    public async Task Sales_returns_adapter_preserves_return_quantity_and_finance_credit_evidence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(Tenant), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Warehouse:{Warehouse:D}"), new CorrelationId("report-sales-return"), Actor);
        var returnId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000030");
        var deliveryId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000031");
        var orderLineId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000032");
        var invoiceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000033");
        var creditNoteId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000034");
        var secondCreditNoteId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000035");
        var sourceLine = new SalesCustomerReturnSourceLineRecord(orderLineId, Guid.NewGuid(), "SKU-1", "Product", Guid.NewGuid(), "EA", 2m, 0m, 2m, 100m, 0m, 100m, null);
        var source = new SalesCustomerReturnSourceRecord(returnId, deliveryId, Guid.NewGuid(), 1, Tenant, Company, Branch, Customer, Warehouse, DateTimeOffset.UtcNow, invoiceId, Guid.NewGuid(), "SAR", [sourceLine]);
        var request = new SalesCustomerReturnCreateRequest(deliveryId, new DateOnly(2026, 3, 10), SalesCustomerReturnConsequence.CreditNote, invoiceId, [new SalesCustomerReturnLineRequest(orderLineId, 2m)], "quality");
        var effectAllocation = new SalesCustomerReturnFinanceAllocationEffect(Guid.NewGuid(), 2m, 90m, 10m, 100m, "allocation-fingerprint");
        var effectCommand = new SalesCustomerReturnFinanceEffectCommand(returnId, Tenant, creditNoteId, invoiceId, [effectAllocation.SourceAllocationId], DateTimeOffset.UtcNow, source.FinanceOpenItemId, Guid.NewGuid(), [Guid.NewGuid()], 90m, 10m, 100m, "SAR", "source-fingerprint", "effect-fingerprint", "request-fingerprint", "Committed", "credit-note-key", [effectAllocation], Company, Customer);
        var secondEffectCommand = effectCommand with { CreditNoteId = secondCreditNoteId, SourceFingerprint = "source-fingerprint-2", EffectFingerprint = "effect-fingerprint-2", RequestFingerprint = "request-fingerprint-2", DownstreamIdempotencyKey = "credit-note-key-2" };
        await using (var db = new SalesDbContext(options, tenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            var entity = new SalesCustomerReturnEntity(new TenantId(Tenant), returnId, request, source, Actor, DateTimeOffset.UtcNow);
            entity.Lines.Add(new SalesCustomerReturnLineEntity(new TenantId(Tenant), Guid.NewGuid(), returnId, deliveryId, request.Lines[0], sourceLine));
            var effect = new SalesCustomerReturnFinanceEffectEntity(new TenantId(Tenant), Guid.NewGuid(), effectCommand, [effectAllocation], DateTimeOffset.UtcNow);
            var secondEffect = new SalesCustomerReturnFinanceEffectEntity(new TenantId(Tenant), Guid.NewGuid(), secondEffectCommand, [effectAllocation], DateTimeOffset.UtcNow);
            entity.RegisterFinanceCreditNote(effectCommand, DateTimeOffset.UtcNow);
            entity.RegisterFinanceCreditNote(secondEffectCommand, DateTimeOffset.UtcNow);
            db.CustomerReturns.Add(entity);
            db.CustomerReturnFinanceEffects.AddRange(effect, secondEffect);
            await db.SaveChangesAsync();
        }

        var persistence = new CustomerReturnPersistence(options);
        var context = ProcurementContext(tenantContext);
        var page = await persistence.ListReportingPageAsync(context, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), SalesCustomerReturnStatus.Draft, ReportingPageRequest.Create(1, 10, "returnDate", "asc"));

        Assert.Equal(1, page.TotalRows);
        var row = Assert.Single(page.Rows);
        Assert.Equal(2m, row.ReturnQuantity);
        Assert.Equal(SalesCustomerReturnConsequence.CreditNote, row.Consequence);
        Assert.Equal(2, row.FinanceEffects.Count);
        var effectRow = row.FinanceEffects.Single(item => item.CreditNoteId == creditNoteId);
        Assert.Equal("Active", effectRow.State);
        Assert.Contains(row.FinanceEffects, item => item.CreditNoteId == secondCreditNoteId);
        Assert.Equal("Committed", row.FinanceEffectState);
        Assert.Contains(creditNoteId, row.FinanceCreditNoteIds);
        Assert.Contains(secondCreditNoteId, row.FinanceCreditNoteIds);
        Assert.Equal("returnDate,id", page.StableSortKey);
    }

    private static FinanceSettlementDocumentCommand SettlementCommand(Guid id, DateOnly date, decimal amount, Guid cashAccountId, Guid paymentMethodId) =>
        new(FinancePaymentMethodDirection.Receipt, Company, null, Customer, cashAccountId, paymentMethodId, date, "SAR", amount, amount, 1m, null, null, null, null, null, id, $"report-{id:N}", $"report-{id:N}");

    private static FinanceRequestContext FinanceContext(TenantContext tenantContext)
    {
        var foundation = FoundationRequestContext.ForTenant(Actor, Guid.NewGuid(), tenantContext, "tenant.finance.settlement.read");
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private static ProcurementRequestContext ProcurementContext(TenantContext tenantContext)
    {
        var foundation = FoundationRequestContext.ForTenant(Actor, Guid.NewGuid(), tenantContext, "tenant.reporting.report.view");
        var result = new ProcurementTenantContextResolver().Resolve(foundation);
        Assert.True(result.Allowed, result.Code);
        return result.Context!;
    }
}
