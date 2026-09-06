#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

internal sealed class SalesDbContext(DbContextOptions options, TenantContext tenantContext)
    : TenantPersistenceDbContext(options, tenantContext, TenantOwnershipVerifierRegistry.CreateSales())
{
    internal DbSet<SalesQuotationEntity> Quotations => Set<SalesQuotationEntity>();
    internal DbSet<SalesQuotationRevisionEntity> QuotationRevisions => Set<SalesQuotationRevisionEntity>();
    internal DbSet<SalesOrderEntity> Orders => Set<SalesOrderEntity>();
    internal DbSet<SalesHistoryEntity> History => Set<SalesHistoryEntity>();
    internal DbSet<SalesAuditEntity> Audit => Set<SalesAuditEntity>();
    internal DbSet<SalesIdempotencyEntity> Idempotency => Set<SalesIdempotencyEntity>();
    internal DbSet<SalesCreditEntity> Credit => Set<SalesCreditEntity>();
    internal DbSet<SalesDeliveryEntity> Deliveries => Set<SalesDeliveryEntity>();
    internal DbSet<SalesInvoiceRequestEntity> InvoiceRequests => Set<SalesInvoiceRequestEntity>();
    internal DbSet<SalesCustomerReturnEntity> CustomerReturns => Set<SalesCustomerReturnEntity>();
    internal DbSet<SalesCustomerReturnLineEntity> CustomerReturnLines => Set<SalesCustomerReturnLineEntity>();
    internal DbSet<SalesCustomerReturnInvoiceAllocationEntity> CustomerReturnInvoiceAllocations => Set<SalesCustomerReturnInvoiceAllocationEntity>();
    internal DbSet<SalesCustomerReturnFinanceEffectEntity> CustomerReturnFinanceEffects => Set<SalesCustomerReturnFinanceEffectEntity>();
    internal DbSet<SalesCustomerReturnFinanceEffectAllocationEntity> CustomerReturnFinanceEffectAllocations => Set<SalesCustomerReturnFinanceEffectAllocationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        Configure(modelBuilder.Entity<SalesQuotationEntity>(), "SalesQuotations");
        Configure(modelBuilder.Entity<SalesQuotationRevisionEntity>(), "SalesQuotationRevisions");
        Configure(modelBuilder.Entity<SalesOrderEntity>(), "SalesOrders");
        Configure(modelBuilder.Entity<SalesHistoryEntity>(), "SalesHistory");
        Configure(modelBuilder.Entity<SalesAuditEntity>(), "SalesAudit");
        Configure(modelBuilder.Entity<SalesIdempotencyEntity>(), "SalesIdempotency");
        Configure(modelBuilder.Entity<SalesCreditEntity>(), "SalesCreditEvaluations");
        Configure(modelBuilder.Entity<SalesDeliveryEntity>(), "SalesDeliveries");
        Configure(modelBuilder.Entity<SalesInvoiceRequestEntity>(), "SalesInvoiceRequests");
        Configure(modelBuilder.Entity<SalesCustomerReturnEntity>(), "SalesCustomerReturns");
        Configure(modelBuilder.Entity<SalesCustomerReturnLineEntity>(), "SalesCustomerReturnLines");
        Configure(modelBuilder.Entity<SalesCustomerReturnInvoiceAllocationEntity>(), "SalesCustomerReturnInvoiceAllocations");
        Configure(modelBuilder.Entity<SalesCustomerReturnFinanceEffectEntity>(), "SalesCustomerReturnFinanceEffects");
        Configure(modelBuilder.Entity<SalesCustomerReturnFinanceEffectAllocationEntity>(), "SalesCustomerReturnFinanceEffectAllocations");

        var quote = modelBuilder.Entity<SalesQuotationEntity>();
        quote.Property(item => item.Id).ValueGeneratedNever();
        quote.Property(item => item.CompanyId).IsRequired();
        quote.Property(item => item.BranchId).IsRequired(false);
        quote.Property(item => item.CustomerId).IsRequired();
        quote.Property(item => item.QuotationDate).IsRequired();
        quote.Property(item => item.ValidUntil).IsRequired();
        quote.Property(item => item.CurrencyId).IsRequired();
        quote.Property(item => item.Subtotal).HasPrecision(28, 8).IsRequired();
        quote.Property(item => item.DiscountAmount).HasPrecision(28, 8).IsRequired();
        quote.Property(item => item.TaxAmount).HasPrecision(28, 8).IsRequired();
        quote.Property(item => item.Total).HasPrecision(28, 8).IsRequired();
        quote.Property(item => item.CreatedByActorId).IsRequired();
        quote.Property(item => item.CreatedAt).IsRequired();
        quote.Property(item => item.Number).HasMaxLength(64).IsRequired();
        quote.Property(item => item.CustomerCode).HasMaxLength(128).IsRequired();
        quote.Property(item => item.CustomerName).HasMaxLength(512).IsRequired();
        quote.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired();
        quote.Property(item => item.CustomerContactId).HasMaxLength(128);
        quote.Property(item => item.Notes).HasMaxLength(4096);
        quote.Property(item => item.CustomerReference).HasMaxLength(256);
        quote.Property(item => item.LinesJson).HasMaxLength(131072).IsRequired();
         quote.Property(item => item.ExchangeRateJson).HasMaxLength(4096).IsRequired(false);
         quote.Property(item => item.PaymentTermJson).HasMaxLength(8192).IsRequired(false);
        quote.Property(item => item.ApprovalPolicyJson).HasMaxLength(32768).IsRequired();
        quote.Property(item => item.CurrentApprovalsJson).HasMaxLength(8192).IsRequired();
        quote.Property(item => item.Status).IsRequired();
        quote.Property(item => item.RevisionNumber).IsRequired();
        quote.HasIndex(item => new { item.TenantId, item.Number }).IsUnique();
        quote.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.UpdatedAt });
        quote.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAt });

        var revision = modelBuilder.Entity<SalesQuotationRevisionEntity>();
        revision.Property(item => item.Id).ValueGeneratedNever();
        revision.Property(item => item.QuotationId).IsRequired();
        revision.Property(item => item.RevisionNumber).IsRequired();
        revision.Property(item => item.ActorId).IsRequired();
        revision.Property(item => item.OccurredAt).IsRequired();
        revision.Property(item => item.SnapshotJson).HasMaxLength(131072).IsRequired();
        revision.Property(item => item.SnapshotHash).HasMaxLength(128).IsRequired();
        revision.Property(item => item.Reason).HasMaxLength(2048);
        revision.Property(item => item.Status).IsRequired();
        revision.HasIndex(item => new { item.TenantId, item.QuotationId, item.RevisionNumber }).IsUnique();
        revision.HasIndex(item => new { item.TenantId, item.QuotationId, item.OccurredAt });

        var order = modelBuilder.Entity<SalesOrderEntity>();
        order.Property(item => item.Id).ValueGeneratedNever();
        order.Property(item => item.CompanyId).IsRequired();
        order.Property(item => item.BranchId).IsRequired(false);
        order.Property(item => item.CustomerId).IsRequired();
        order.Property(item => item.SourceQuotationId).IsRequired();
        order.Property(item => item.SourceQuotationRevision).IsRequired();
        order.Property(item => item.RevisionNumber).IsRequired();
        order.Property(item => item.CurrencyId).IsRequired();
        order.Property(item => item.Subtotal).HasPrecision(28, 8).IsRequired();
        order.Property(item => item.DiscountAmount).HasPrecision(28, 8).IsRequired();
        order.Property(item => item.TaxAmount).HasPrecision(28, 8).IsRequired();
        order.Property(item => item.Total).HasPrecision(28, 8).IsRequired();
        order.Property(item => item.CreditOutcome).IsRequired();
        order.Property(item => item.CreditEvaluatedAt).IsRequired(false);
        order.Property(item => item.CreditOverrideExpiresAt).IsRequired(false);
        order.Property(item => item.CreatedByActorId).IsRequired();
        order.Property(item => item.CreatedAt).IsRequired();
        order.Property(item => item.Number).HasMaxLength(64).IsRequired();
        order.Property(item => item.CustomerCode).HasMaxLength(128).IsRequired();
        order.Property(item => item.CustomerName).HasMaxLength(512).IsRequired();
        order.Property(item => item.SourceQuotationNumber).HasMaxLength(64).IsRequired();
        order.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired();
        order.Property(item => item.CreditReason).HasMaxLength(2048);
        order.Property(item => item.LinesJson).HasMaxLength(131072).IsRequired();
         order.Property(item => item.ExchangeRateJson).HasMaxLength(4096).IsRequired(false);
         order.Property(item => item.PaymentTermJson).HasMaxLength(8192).IsRequired(false);
        order.Property(item => item.ApprovalPolicyJson).HasMaxLength(32768).IsRequired();
        order.Property(item => item.CurrentApprovalsJson).HasMaxLength(32768).IsRequired();
        order.HasIndex(item => new { item.TenantId, item.Number }).IsUnique();
        order.HasIndex(item => new { item.TenantId, item.SourceQuotationId, item.SourceQuotationRevision }).IsUnique();
        order.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.UpdatedAt });
        order.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAt });

        var delivery = modelBuilder.Entity<SalesDeliveryEntity>();
        delivery.Property(item => item.Id).ValueGeneratedNever(); delivery.Property(item => item.OrderId).IsRequired(); delivery.Property(item => item.OrderRevisionNumber).IsRequired(); delivery.Property(item => item.CompanyId).IsRequired(); delivery.Property(item => item.BranchId).IsRequired(false); delivery.Property(item => item.CustomerId).IsRequired(); delivery.Property(item => item.WarehouseId).IsRequired(); delivery.Property(item => item.Status).IsRequired(); delivery.Property(item => item.ErrorCode).HasMaxLength(128).IsRequired(false); delivery.Property(item => item.LinesJson).HasMaxLength(131072).IsRequired(); delivery.Property(item => item.SourceSnapshotJson).HasMaxLength(262144).IsRequired(); delivery.Property(item => item.MovementIdsJson).HasMaxLength(8192).IsRequired(); delivery.Property(item => item.HandoffJson).HasMaxLength(16384).IsRequired(); delivery.Property(item => item.ActorId).IsRequired(); delivery.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(false); delivery.Property(item => item.CreatedAt).IsRequired(); delivery.Property(item => item.PostedAt).IsRequired(false); delivery.HasIndex(item => new { item.TenantId, item.OrderId, item.Status }); delivery.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique().HasFilter(Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "[IdempotencyKey] IS NOT NULL" : "IdempotencyKey IS NOT NULL");

        var invoice = modelBuilder.Entity<SalesInvoiceRequestEntity>();
        invoice.Property(item => item.Id).ValueGeneratedNever(); invoice.Property(item => item.OrderId).IsRequired(); invoice.Property(item => item.OrderRevisionNumber).IsRequired(); invoice.Property(item => item.DeliveryId).IsRequired(false); invoice.Property(item => item.CompanyId).IsRequired(); invoice.Property(item => item.BranchId).IsRequired(false); invoice.Property(item => item.CustomerId).IsRequired(); invoice.Property(item => item.InvoiceDate).IsRequired(); invoice.Property(item => item.LinesJson).HasMaxLength(131072).IsRequired(); invoice.Property(item => item.Amount).HasPrecision(28, 8).IsRequired(); invoice.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(); invoice.Property(item => item.SourceSnapshotJson).HasMaxLength(262144).IsRequired(); invoice.Property(item => item.HandoffJson).HasMaxLength(16384).IsRequired(); invoice.Property(item => item.PaymentTermJson).HasMaxLength(8192).IsRequired(false); invoice.Property(item => item.Status).IsRequired(); invoice.Property(item => item.ErrorCode).HasMaxLength(128).IsRequired(false); invoice.Property(item => item.FinanceOpenItemId).IsRequired(false); invoice.Property(item => item.ActorId).IsRequired(); invoice.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(false); invoice.Property(item => item.CreatedAt).IsRequired(); invoice.Property(item => item.PostedAt).IsRequired(false); invoice.HasIndex(item => new { item.TenantId, item.OrderId, item.Status }); invoice.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique().HasFilter(Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "[IdempotencyKey] IS NOT NULL" : "IdempotencyKey IS NOT NULL"); invoice.HasIndex(item => new { item.TenantId, item.DeliveryId, item.Status });

        var customerReturn = modelBuilder.Entity<SalesCustomerReturnEntity>();
        customerReturn.Property(item => item.Id).ValueGeneratedNever();
        customerReturn.Property(item => item.DeliveryId).IsRequired(); customerReturn.Property(item => item.OrderId).IsRequired(); customerReturn.Property(item => item.OrderRevisionNumber).IsRequired(); customerReturn.Property(item => item.CompanyId).IsRequired(); customerReturn.Property(item => item.BranchId).IsRequired(false); customerReturn.Property(item => item.CustomerId).IsRequired(); customerReturn.Property(item => item.WarehouseId).IsRequired(); customerReturn.Property(item => item.InvoiceId).IsRequired(false); customerReturn.Property(item => item.FinanceOpenItemId).IsRequired(false); customerReturn.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(); customerReturn.Property(item => item.Status).IsRequired(); customerReturn.Property(item => item.Consequence).IsRequired(); customerReturn.Property(item => item.ReturnDate).IsRequired(); customerReturn.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); customerReturn.Property(item => item.EvidenceJson).HasMaxLength(65536).IsRequired(); customerReturn.Property(item => item.HandoffJson).HasMaxLength(16384).IsRequired(); customerReturn.Property(item => item.InventoryEffectFingerprint).HasMaxLength(128).IsRequired(false); customerReturn.Property(item => item.InventoryRequestFingerprint).HasMaxLength(128).IsRequired(false); customerReturn.Property(item => item.InventoryCommitState).HasMaxLength(32).IsRequired(); customerReturn.Property(item => item.InventoryAcknowledgementState).HasMaxLength(32).IsRequired(); customerReturn.Property(item => item.InventoryReconciliationState).HasMaxLength(32).IsRequired(); customerReturn.Property(item => item.InventoryPhysicalEvidenceReference).HasMaxLength(2048).IsRequired(); customerReturn.Property(item => item.InventoryInspectionEvidenceReference).HasMaxLength(2048).IsRequired(); customerReturn.Property(item => item.InventoryLastError).HasMaxLength(256).IsRequired(false); customerReturn.Property(item => item.InventoryLastAttemptAt).IsRequired(false); customerReturn.Property(item => item.InventoryCorrelationId).HasMaxLength(128).IsRequired(); customerReturn.Property(item => item.FinanceEffectState).HasMaxLength(32).IsRequired(); customerReturn.Property(item => item.InventoryAttemptCount).IsRequired(); customerReturn.Property(item => item.ActiveFinanceCreditNoteCount).IsRequired(); customerReturn.Property(item => item.FinanceCreditNoteIdsJson).HasMaxLength(8192).IsRequired(); customerReturn.Property(item => item.FinanceReversedCreditNoteIdsJson).HasMaxLength(8192).IsRequired(); customerReturn.Property(item => item.CreatedByActorId).IsRequired(); customerReturn.Property(item => item.CreatedAt).IsRequired(); customerReturn.Property(item => item.UpdatedAt).IsRequired(); customerReturn.HasIndex(item => new { item.TenantId, item.DeliveryId, item.CreatedAt }); customerReturn.HasIndex(item => new { item.TenantId, item.FinanceOpenItemId, item.Status }); customerReturn.HasIndex(item => new { item.TenantId, item.InventoryEffectId }).IsUnique().HasFilter(Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "[InventoryEffectId] IS NOT NULL" : "InventoryEffectId IS NOT NULL");

        var customerReturnLine = modelBuilder.Entity<SalesCustomerReturnLineEntity>();
        customerReturnLine.Property(item => item.Id).ValueGeneratedNever(); customerReturnLine.Property(item => item.CustomerReturnId).IsRequired(); customerReturnLine.Property(item => item.DeliveryId).IsRequired(); customerReturnLine.Property(item => item.OrderLineId).IsRequired(); customerReturnLine.Property(item => item.DeliveredQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.PreviouslyReturnedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.ReturnQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.ReceivedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.InspectedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.CommerciallyAcceptedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.RestockedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.NonRestockableAcceptedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.RejectedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.StockDisposition).HasMaxLength(32).IsRequired(); customerReturnLine.Property(item => item.InventoryMovementIdsJson).HasMaxLength(8192).IsRequired(); customerReturnLine.Property(item => item.DeliveryMovementIdsJson).HasMaxLength(8192).IsRequired(); customerReturnLine.Property(item => item.DeliveryUnitCost).HasPrecision(28, 8).IsRequired(false); customerReturnLine.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); customerReturnLine.HasIndex(item => new { item.TenantId, item.CustomerReturnId, item.OrderLineId }).IsUnique(); customerReturnLine.HasIndex(item => new { item.TenantId, item.DeliveryId, item.OrderLineId }); customerReturnLine.HasOne<SalesCustomerReturnEntity>().WithMany(item => item.Lines).HasForeignKey(item => new { item.TenantId, item.CustomerReturnId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var allocation = modelBuilder.Entity<SalesCustomerReturnInvoiceAllocationEntity>();
        allocation.Property(item => item.Id).ValueGeneratedNever(); allocation.Property(item => item.CustomerReturnId).IsRequired(); allocation.Property(item => item.InvoiceId).IsRequired(); allocation.Property(item => item.FinanceOpenItemId).IsRequired(false); allocation.Property(item => item.DeliveryId).IsRequired(); allocation.Property(item => item.OrderLineId).IsRequired(); allocation.Property(item => item.OrderRevisionNumber).IsRequired(); allocation.Property(item => item.RecognizedQuantity).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.ReturnQuantity).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.CommerciallyAcceptedQuantity).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.PreviouslyCreditedQuantity).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.RemainingCreditableQuantity).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.NetAmount).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.TaxAmount).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.GrossAmount).HasPrecision(28, 8).IsRequired(); allocation.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(); allocation.Property(item => item.TaxId).IsRequired(false); allocation.Property(item => item.TaxRateVersionId).IsRequired(false); allocation.Property(item => item.TaxRateVersionNumber).IsRequired(false); allocation.Property(item => item.SourceAllocationFingerprint).HasMaxLength(128).IsRequired(); allocation.Property(item => item.SourceInvoiceFingerprint).HasMaxLength(128).IsRequired(); allocation.HasIndex(item => new { item.TenantId, item.CustomerReturnId, item.Id }).IsUnique(); allocation.HasIndex(item => new { item.TenantId, item.CustomerReturnId, item.InvoiceId, item.OrderLineId }).IsUnique(); allocation.HasOne<SalesCustomerReturnEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.CustomerReturnId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var financeEffect = modelBuilder.Entity<SalesCustomerReturnFinanceEffectEntity>();
        financeEffect.Property(item => item.CompanyId).IsRequired(); financeEffect.Property(item => item.CustomerId).IsRequired();
        financeEffect.Property(item => item.Id).ValueGeneratedNever(); financeEffect.Property(item => item.CustomerReturnId).IsRequired(); financeEffect.Property(item => item.CreditNoteId).IsRequired(); financeEffect.Property(item => item.InvoiceId).IsRequired(); financeEffect.Property(item => item.FinanceOpenItemId).IsRequired(); financeEffect.Property(item => item.PostingJournalId).IsRequired(); financeEffect.Property(item => item.TaxJournalIdsJson).HasMaxLength(8192).IsRequired(); financeEffect.Property(item => item.NetAmount).HasPrecision(28, 8).IsRequired(); financeEffect.Property(item => item.TaxAmount).HasPrecision(28, 8).IsRequired(); financeEffect.Property(item => item.GrossAmount).HasPrecision(28, 8).IsRequired(); financeEffect.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(); financeEffect.Property(item => item.SourceAllocationIdsJson).HasMaxLength(8192).IsRequired(); financeEffect.Property(item => item.SourceFingerprint).HasMaxLength(128).IsRequired(); financeEffect.Property(item => item.EffectFingerprint).HasMaxLength(128).IsRequired(); financeEffect.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired(); financeEffect.Property(item => item.DownstreamIdempotencyKey).HasMaxLength(256).IsRequired(); financeEffect.Property(item => item.State).HasMaxLength(32).IsRequired(); financeEffect.Property(item => item.AcknowledgedAt).IsRequired(); financeEffect.Property(item => item.ReversalState).HasMaxLength(32).IsRequired(); financeEffect.Property(item => item.ReversalJournalId).IsRequired(false); financeEffect.Property(item => item.ReversalEffectId).IsRequired(false); financeEffect.Property(item => item.ReversalEffectFingerprint).HasMaxLength(128).IsRequired(false); financeEffect.Property(item => item.ReversalRequestFingerprint).HasMaxLength(128).IsRequired(false); financeEffect.Property(item => item.ReversalDownstreamIdempotencyKey).HasMaxLength(256).IsRequired(false); financeEffect.Property(item => item.ReversedAt).IsRequired(false); financeEffect.HasIndex(item => new { item.TenantId, item.CustomerReturnId, item.CreditNoteId }).IsUnique(); financeEffect.HasIndex(item => new { item.TenantId, item.CustomerReturnId, item.InvoiceId, item.State }); financeEffect.HasOne<SalesCustomerReturnEntity>().WithMany(item => item.FinanceEffects).HasForeignKey(item => new { item.TenantId, item.CustomerReturnId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var financeEffectAllocation = modelBuilder.Entity<SalesCustomerReturnFinanceEffectAllocationEntity>();
        financeEffectAllocation.Property(item => item.Id).ValueGeneratedNever(); financeEffectAllocation.Property(item => item.FinanceEffectId).IsRequired(); financeEffectAllocation.Property(item => item.SourceAllocationId).IsRequired(); financeEffectAllocation.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); financeEffectAllocation.Property(item => item.NetAmount).HasPrecision(28, 8).IsRequired(); financeEffectAllocation.Property(item => item.TaxAmount).HasPrecision(28, 8).IsRequired(); financeEffectAllocation.Property(item => item.GrossAmount).HasPrecision(28, 8).IsRequired(); financeEffectAllocation.Property(item => item.SourceAllocationFingerprint).HasMaxLength(128).IsRequired(); financeEffectAllocation.Property(item => item.ConsumedAt).IsRequired(); financeEffectAllocation.HasIndex(item => new { item.TenantId, item.FinanceEffectId, item.SourceAllocationId }).IsUnique(); financeEffectAllocation.HasIndex(item => new { item.TenantId, item.SourceAllocationId }); financeEffectAllocation.HasOne<SalesCustomerReturnFinanceEffectEntity>().WithMany(item => item.Allocations).HasForeignKey(item => new { item.TenantId, item.FinanceEffectId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var history = modelBuilder.Entity<SalesHistoryEntity>();
        history.Property(item => item.Id).ValueGeneratedNever();
        history.Property(item => item.DocumentId).IsRequired();
        history.Property(item => item.ActorId).IsRequired();
        history.Property(item => item.OccurredAt).IsRequired();
        history.Property(item => item.DocumentType).HasMaxLength(32).IsRequired();
        history.Property(item => item.Action).IsRequired();
        history.Property(item => item.FromStatus).HasMaxLength(64);
        history.Property(item => item.ToStatus).HasMaxLength(64);
        history.Property(item => item.Reason).HasMaxLength(2048);
        history.Property(item => item.PolicyId).HasMaxLength(256);
        history.Property(item => item.PolicyVersion).IsRequired(false);
        history.Property(item => item.CreditOutcome).HasMaxLength(32);
        history.Property(item => item.SnapshotHash).HasMaxLength(128);
        history.Property(item => item.SnapshotJson).HasMaxLength(131072);
        history.HasIndex(item => new { item.TenantId, item.DocumentType, item.DocumentId, item.OccurredAt });

        var audit = modelBuilder.Entity<SalesAuditEntity>();
        audit.Property(item => item.Id).ValueGeneratedNever();
        audit.Property(item => item.DocumentId).IsRequired();
        audit.Property(item => item.ActorId).IsRequired();
        audit.Property(item => item.OccurredAt).IsRequired();
        audit.Property(item => item.OperationId).HasMaxLength(128).IsRequired();
        audit.Property(item => item.DocumentType).HasMaxLength(32).IsRequired();
        audit.Property(item => item.Decision).HasMaxLength(32).IsRequired();
        audit.Property(item => item.Reason).HasMaxLength(2048);
        audit.Property(item => item.BeforeSummary).HasMaxLength(4096);
        audit.Property(item => item.AfterSummary).HasMaxLength(4096);
        audit.Property(item => item.IdempotencyKey).HasMaxLength(256);
        audit.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        audit.HasIndex(item => new { item.TenantId, item.DocumentType, item.DocumentId, item.OccurredAt });

        var idem = modelBuilder.Entity<SalesIdempotencyEntity>();
        idem.Property(item => item.Id).ValueGeneratedNever();
        idem.Property(item => item.DocumentId).IsRequired();
        idem.Property(item => item.CreatedAt).IsRequired();
        idem.Property(item => item.OperationId).HasMaxLength(128).IsRequired();
        idem.Property(item => item.Key).HasMaxLength(256).IsRequired();
        idem.Property(item => item.Fingerprint).HasMaxLength(128).IsRequired();
        idem.Property(item => item.DocumentType).HasMaxLength(32).IsRequired();
        idem.Property(item => item.ResponseJson).HasMaxLength(131072).IsRequired();
        idem.HasIndex(item => new { item.TenantId, item.OperationId, item.Key }).IsUnique();

        var credit = modelBuilder.Entity<SalesCreditEntity>();
        credit.Property(item => item.Id).ValueGeneratedNever();
        credit.Property(item => item.DocumentId).IsRequired();
        credit.Property(item => item.CustomerId).IsRequired();
        credit.Property(item => item.CompanyId).IsRequired();
        credit.Property(item => item.OpenReceivables).HasPrecision(28, 8).IsRequired(false);
        credit.Property(item => item.OverdueReceivables).HasPrecision(28, 8).IsRequired(false);
        credit.Property(item => item.NetReceivableExposure).HasPrecision(28, 8).IsRequired(false);
        credit.Property(item => item.ProposedExposure).HasPrecision(28, 8).IsRequired(false);
        credit.Property(item => item.CreditLimit).HasPrecision(28, 8).IsRequired(false);
        credit.Property(item => item.AsOfDate).IsRequired();
        credit.Property(item => item.OverrideExpiresAt).IsRequired(false);
        credit.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(false);
        credit.Property(item => item.TransactionCurrencyCode).HasMaxLength(16).IsRequired(false);
        credit.Property(item => item.TransactionAmount).HasPrecision(28, 8).IsRequired(false);
        credit.Property(item => item.ConvertedOrderCommitment).HasPrecision(28, 8).IsRequired(false);
        credit.Property(item => item.ExchangeRateJson).HasMaxLength(4096).IsRequired(false);
        credit.Property(item => item.OrderRevisionNumber).IsRequired(false);
        credit.Property(item => item.Outcome).IsRequired();
        credit.Property(item => item.Reason).HasMaxLength(2048);
        credit.HasIndex(item => new { item.TenantId, item.DocumentId, item.EvaluatedAt });
    }

    private void Configure<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName) where TEntity : class, ITenantOwned
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer") entity.ToTable(tableName, "sales"); else entity.ToTable(tableName);
        entity.HasKey("Id");
        entity.Property("Id").ValueGeneratedNever();
        var tenant = entity.Property(item => item.TenantId);
        tenant.HasConversion(item => item.Value, value => new TenantId(value)).IsRequired();
        var version = entity.Property<byte[]>("Version").IsRequired().IsConcurrencyToken();
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer") version.IsRowVersion(); else version.ValueGeneratedNever();
        entity.HasIndex("TenantId", "Id").IsUnique();
        entity.HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }
}

#pragma warning restore CS1591
