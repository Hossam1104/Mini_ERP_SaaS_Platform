using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class CustomerReturnFoundationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Phase_A_operations_are_catalogued_as_tenant_bound_mutations_or_reads()
    {
        var operationIds = new[]
        {
            "sales.customer-return.eligible-source.list",
            "sales.customer-return.eligible-source.read",
            "sales.customer-return.create",
            "sales.customer-return.read",
            "sales.customer-return.submit",
            "sales.customer-return.approve",
            "sales.customer-return.reject",
            "sales.customer-return.cancel",
            "sales.customer-return.reverse",
            "sales.customer-return.history.read",
            "sales.customer-return.audit.read",
            "inventory.customer-return.receive",
            "inventory.customer-return.inspect",
            "inventory.customer-return.reverse",
            "inventory.customer-return.read",
            "finance.credit-note.create",
            "finance.credit-note.read",
            "finance.credit-note.submit",
            "finance.credit-note.approve",
            "finance.credit-note.reject",
            "finance.credit-note.cancel",
            "finance.credit-note.post",
            "finance.credit-note.reverse"
        };

        foreach (var operationId in operationIds)
        {
            var operation = FoundationOperationCatalog.GetRequired(operationId);
            Assert.Equal(FoundationOperationVisibility.Public, operation.Visibility);
            Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, operation.SecurityProfile);
            Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);

            if (operation.HttpMethod == "POST")
            {
                Assert.True(operation.RequiresAntiforgery);
                Assert.True(operation.RequiresMandatoryAudit);
                Assert.True(operation.IsUnsafe);
                Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
            }
        }
    }

    [Fact]
    public void Each_module_owns_a_tenant_filtered_customer_return_model()
    {
        var options = new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:").Options;

        using var sales = new SalesDbContext(options, Context("sales"));
        using var inventory = new InventoryDbContext(options, Context("inventory"));
        using var finance = new FinanceDbContext(options, Context("finance"));

        AssertTenantOwned(sales.Model, "MiniErp.Infrastructure.Persistence.Modules.Sales.SalesCustomerReturnEntity");
        AssertTenantOwned(sales.Model, "MiniErp.Infrastructure.Persistence.Modules.Sales.SalesCustomerReturnLineEntity");
        AssertTenantOwned(sales.Model, "MiniErp.Infrastructure.Persistence.Modules.Sales.SalesCustomerReturnInvoiceAllocationEntity");
        AssertTenantOwned(inventory.Model, "MiniErp.Infrastructure.Persistence.Modules.Inventory.InventoryCustomerReturnEntity");
        AssertTenantOwned(inventory.Model, "MiniErp.Infrastructure.Persistence.Modules.Inventory.InventoryCustomerReturnLineEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCreditNoteEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCreditNoteLineEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCustomerCreditEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCustomerCreditApplicationEntity");
    }

    [Fact]
    public void Inventory_return_line_separates_commercial_acceptance_from_stock_disposition()
    {
        var line = new InventoryCustomerReturnLineEntity(TenantIdValue, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m);

        line.Receive(5m);
        line.Dispose(2m, InventoryCustomerReturnDisposition.NonRestockable, commerciallyAccepted: true, "usable only commercially", null, null, null);

        Assert.Equal(2m, line.CommerciallyAcceptedQuantity);
        Assert.Equal(0m, line.RestockedQuantity);
        Assert.Equal(2m, line.NonRestockableAcceptedQuantity);
        Assert.Equal(0m, line.RejectedQuantity);
        Assert.Throws<InvalidOperationException>(() => line.Dispose(4m, InventoryCustomerReturnDisposition.Restockable, true, null, Guid.NewGuid(), Guid.NewGuid(), 10m));
    }

    [Fact]
    public void Sales_inventory_acknowledgement_moves_from_receipt_to_completed_only_after_inspection()
    {
        var entity = CreateSalesReturn(3m);
        var line = entity.Lines.Single();
        var effectId = Guid.NewGuid();

        entity.AcknowledgeInventory(Acknowledgement(entity, effectId, "receipt", 3m, 0m, 0m, 0m, 0m, 0m), DateTimeOffset.UtcNow);
        Assert.Equal(SalesCustomerReturnStatus.Received, entity.Status);

        entity.AcknowledgeInventory(Acknowledgement(entity, effectId, "inspection", 3m, 3m, 2m, 1m, 1m, 1m), DateTimeOffset.UtcNow);
        Assert.Equal(SalesCustomerReturnStatus.Completed, entity.Status);
        Assert.Equal(2m, line.CommerciallyAcceptedQuantity);
        Assert.Equal(1m, line.RestockedQuantity);
        Assert.Equal(1m, line.NonRestockableAcceptedQuantity);
        Assert.Equal(1m, line.RejectedQuantity);
    }

    [Fact]
    public void Sales_inventory_acknowledgement_rejects_wrong_effect_or_line_set()
    {
        var entity = CreateSalesReturn(1m);
        var effectId = Guid.NewGuid();
        entity.AcknowledgeInventory(Acknowledgement(entity, effectId, "receipt", 1m, 0m, 0m, 0m, 0m, 0m), DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => entity.AcknowledgeInventory(Acknowledgement(entity, Guid.NewGuid(), "retry", 1m, 0m, 0m, 0m, 0m, 0m), DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => entity.AcknowledgeInventory(new SalesCustomerReturnInventoryAcknowledgementCommand(entity.Id, TenantId, effectId, "receipt", "retry", null, "physical", string.Empty, [], "Committed", "test", DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Sales_inventory_failure_is_reconcilable_with_a_later_acknowledgement()
    {
        var entity = CreateSalesReturn(2m);
        var effectId = Guid.NewGuid();

        entity.RecordInventoryFailure(new SalesCustomerReturnInventoryFailureCommand(entity.Id, TenantId, effectId, "effect", "inspection", "sales unavailable", "test", DateTimeOffset.UtcNow));
        Assert.Equal(SalesCustomerReturnStatus.ReconciliationRequired, entity.Status);
        Assert.Equal("Required", entity.InventoryReconciliationState);

        entity.AcknowledgeInventory(Acknowledgement(entity, effectId, "inspection", 2m, 2m, 2m, 2m, 0m, 0m), DateTimeOffset.UtcNow);
        Assert.Equal(SalesCustomerReturnStatus.Completed, entity.Status);
        Assert.Equal("Reconciled", entity.InventoryReconciliationState);
    }

    [Fact]
    public void Sales_finance_effect_registration_is_idempotent_and_reversal_is_guarded()
    {
        var entity = CreateSalesReturn(1m);
        var effectId = Guid.NewGuid();
        entity.AcknowledgeInventory(Acknowledgement(entity, effectId, "receipt", 1m, 1m, 1m, 1m, 0m, 0m), DateTimeOffset.UtcNow);
        var command = new SalesCustomerReturnFinanceEffectCommand(entity.Id, TenantId, Guid.NewGuid(), entity.InvoiceId!.Value, [Guid.NewGuid()], DateTimeOffset.UtcNow);

        entity.RegisterFinanceCreditNote(command, DateTimeOffset.UtcNow);
        entity.RegisterFinanceCreditNote(command, DateTimeOffset.UtcNow);
        Assert.Equal(1, entity.ActiveFinanceCreditNoteCount);

        entity.RecordDownstreamReversal(new SalesCustomerReturnDownstreamReversalCommand(entity.Id, TenantId, "finance", "test", DateTimeOffset.UtcNow));
        Assert.Equal(0, entity.ActiveFinanceCreditNoteCount);
        Assert.Throws<InvalidOperationException>(() => entity.RecordDownstreamReversal(new SalesCustomerReturnDownstreamReversalCommand(entity.Id, TenantId, "finance", "test", DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void Inventory_return_reversal_records_equal_opposite_lineage_without_editing_original_effect()
    {
        var returnId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var originalMovementId = Guid.NewGuid();
        var deliveryMovementId = Guid.NewGuid();
        var entity = new InventoryCustomerReturnEntity(TenantIdValue, Guid.NewGuid(), returnId, Guid.NewGuid(), null, Guid.NewGuid(), new InventoryCustomerReturnReceiptRequest(new DateOnly(2026, 8, 31), [new InventoryCustomerReturnReceiptLineRequest(Guid.NewGuid(), 2m)], "physical"), Guid.NewGuid(), DateTimeOffset.UtcNow, "receipt", "return-key", "return-correlation");
        var line = new InventoryCustomerReturnLineEntity(TenantIdValue, lineId, entity.Id, Guid.NewGuid(), 2m);
        entity.Lines.Add(line);
        line.Dispose(1m, InventoryCustomerReturnDisposition.Restockable, commerciallyAccepted: true, "accepted", originalMovementId, deliveryMovementId, 10m);
        entity.SetInspected("inspection", InventoryCustomerReturnStatus.Posted, DateTimeOffset.UtcNow, "inspection");

        entity.BeginReversal(DateTimeOffset.UtcNow, "reversal");
        var reversalMovementId = Guid.NewGuid();
        line.RecordReversalMovement(reversalMovementId);
        entity.SetReversalHandoff(true, null, DateTimeOffset.UtcNow);

        Assert.Equal(InventoryCustomerReturnStatus.Reversed, entity.Status);
        Assert.Equal("Reversed", entity.CommitState);
        Assert.Contains(originalMovementId.ToString("D"), line.MovementIdsJson);
        Assert.Contains(reversalMovementId.ToString("D"), line.ReversalMovementIdsJson);
        Assert.Equal("Reconciled", entity.ReconciliationState);
    }

    [Fact]
    public void Customer_credit_rejects_over_application_and_restores_balance_on_reversal()
    {
        var request = new FinanceCreditNoteCreateRequest(Guid.NewGuid(), new DateOnly(2026, 8, 31), "test");
        var note = new FinanceCreditNoteEntity(TenantIdValue, request, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SAR", "SAR", 80m, 20m, 100m, 100m, "evidence");
        var credit = new FinanceCustomerCreditEntity(TenantIdValue, Guid.NewGuid(), note);

        Assert.Throws<InvalidOperationException>(() => credit.Apply(100.01m, Guid.NewGuid()));
        credit.Apply(40m, Guid.NewGuid());
        Assert.Equal(60m, credit.OutstandingAmount);
        credit.Reverse();
        Assert.Equal(FinanceCustomerCreditStatus.Reversed, credit.Status);
        Assert.Equal(0m, credit.AppliedAmount);
    }

    private static void AssertTenantOwned(IModel model, string entityName)
    {
        var entity = model.GetEntityTypes().Single(item => item.ClrType.FullName == entityName);
        Assert.NotNull(entity.FindProperty("TenantId"));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Any(property => property.Name == "TenantId"));
    }

    private static TenantContext Context(string purpose) => TenantContext.ForOrdinaryMembership(
        new TenantId(TenantId),
        new MembershipReference(Guid.NewGuid()),
        correlationId: new CorrelationId($"return-{purpose}"));

    private static TenantId TenantIdValue => new(TenantId);

    private static SalesCustomerReturnEntity CreateSalesReturn(decimal quantity)
    {
        var deliveryId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var source = new SalesCustomerReturnSourceRecord(
            Guid.Empty,
            deliveryId,
            orderId,
            1,
            TenantId,
            Guid.NewGuid(),
            null,
            invoiceId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SAR",
            [new SalesCustomerReturnSourceLineRecord(orderLineId, Guid.NewGuid(), "SKU", "Product", Guid.NewGuid(), "EA", quantity, 0m, quantity, 80m, 20m, 100m, null)],
            SalesCustomerReturnStatus.Approved,
            SalesCustomerReturnConsequence.CreditNote,
            Guid.NewGuid().ToByteArray(),
            [new SalesCustomerReturnInvoiceAllocationRecord(allocationId, invoiceId, Guid.NewGuid(), deliveryId, orderLineId, 1, quantity, quantity, 0m, 0m, quantity, 80m, 20m, 100m, "SAR", null, null, null, "allocation", "invoice")]);
        var request = new SalesCustomerReturnCreateRequest(deliveryId, new DateOnly(2026, 8, 31), SalesCustomerReturnConsequence.CreditNote, source.RecognizedInvoiceId, [new SalesCustomerReturnLineRequest(orderLineId, quantity)], "test");
        return new SalesCustomerReturnEntity(TenantIdValue, returnId, request, source, Guid.NewGuid(), DateTimeOffset.UtcNow) { Lines = { new SalesCustomerReturnLineEntity(TenantIdValue, Guid.NewGuid(), returnId, deliveryId, request.Lines[0], source.Lines[0]) } };
    }

    private static SalesCustomerReturnInventoryAcknowledgementCommand Acknowledgement(SalesCustomerReturnEntity entity, Guid effectId, string requestFingerprint, decimal received, decimal inspected, decimal accepted, decimal restocked, decimal nonRestockable, decimal rejected) =>
        new(entity.Id, TenantId, effectId, "effect", requestFingerprint, "inventory-key", "physical", inspected == 0m ? string.Empty : "inspection", [new(entity.Lines.Single().OrderLineId, received, inspected, accepted, restocked, nonRestockable, rejected, inspected == 0m ? "PendingInspection" : "Restockable", [], [], 10m)], "Committed", "test", DateTimeOffset.UtcNow);
}
