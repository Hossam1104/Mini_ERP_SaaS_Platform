#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryDbContext(DbContextOptions options, TenantContext tenantContext)
    : TenantPersistenceDbContext(options, tenantContext, TenantOwnershipVerifierRegistry.CreateInventory())
{
    internal DbSet<InventoryOpeningBalanceEntity> OpeningBalances => Set<InventoryOpeningBalanceEntity>();
    internal DbSet<InventoryOpeningBalanceRowEntity> OpeningBalanceRows => Set<InventoryOpeningBalanceRowEntity>();
    internal DbSet<InventoryOpeningBalanceHistoryEntity> OpeningBalanceHistory => Set<InventoryOpeningBalanceHistoryEntity>();
    internal DbSet<InventoryStockMovementEntity> StockMovements => Set<InventoryStockMovementEntity>();
    internal DbSet<InventoryTransferEntity> Transfers => Set<InventoryTransferEntity>();
    internal DbSet<InventoryTransferLineEntity> TransferLines => Set<InventoryTransferLineEntity>();
    internal DbSet<InventoryTransferEventEntity> TransferEvents => Set<InventoryTransferEventEntity>();
    internal DbSet<InventoryReservationEntity> Reservations => Set<InventoryReservationEntity>();
    internal DbSet<InventoryReservationHistoryEntity> ReservationHistory => Set<InventoryReservationHistoryEntity>();
    internal DbSet<InventoryAuditEntity> Audit => Set<InventoryAuditEntity>();
    internal DbSet<InventoryIdempotencyEntity> Idempotency => Set<InventoryIdempotencyEntity>();
    internal DbSet<InventoryConcurrencyAnchorEntity> ConcurrencyAnchors => Set<InventoryConcurrencyAnchorEntity>();
    internal DbSet<InventoryReasonCodeEntity> ReasonCodes => Set<InventoryReasonCodeEntity>();
    internal DbSet<InventoryAdjustmentEntity> Adjustments => Set<InventoryAdjustmentEntity>();
    internal DbSet<InventoryAdjustmentLineEntity> AdjustmentLines => Set<InventoryAdjustmentLineEntity>();
    internal DbSet<InventoryCountEntity> Counts => Set<InventoryCountEntity>();
    internal DbSet<InventoryCountSnapshotEntity> CountSnapshots => Set<InventoryCountSnapshotEntity>();
    internal DbSet<InventoryCountLineEntity> CountLines => Set<InventoryCountLineEntity>();
    internal DbSet<InventoryStockIssueEntity> StockIssues => Set<InventoryStockIssueEntity>();
    internal DbSet<InventoryStockIssueLineEntity> StockIssueLines => Set<InventoryStockIssueLineEntity>();
    internal DbSet<InventoryControlHistoryEntity> ControlHistory => Set<InventoryControlHistoryEntity>();
    internal DbSet<InventoryCompanyLedgerSequenceAnchorEntity> CompanyLedgerSequenceAnchors => Set<InventoryCompanyLedgerSequenceAnchorEntity>();
    internal DbSet<InventoryValuationPolicyEntity> ValuationPolicies => Set<InventoryValuationPolicyEntity>();
    internal DbSet<InventoryValuationScopeAnchorEntity> ValuationScopeAnchors => Set<InventoryValuationScopeAnchorEntity>();
    internal DbSet<InventoryValuationStateEntity> ValuationStates => Set<InventoryValuationStateEntity>();
    internal DbSet<InventoryMovementValuationEventEntity> MovementValuationEvents => Set<InventoryMovementValuationEventEntity>();
    internal DbSet<InventoryValuationRunEntity> ValuationRuns => Set<InventoryValuationRunEntity>();
    internal DbSet<InventoryFinanceValuationHandoffEntity> FinanceValuationHandoffs => Set<InventoryFinanceValuationHandoffEntity>();
    internal DbSet<InventoryCustomerReturnEntity> CustomerReturns => Set<InventoryCustomerReturnEntity>();
    internal DbSet<InventoryCustomerReturnLineEntity> CustomerReturnLines => Set<InventoryCustomerReturnLineEntity>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureLedgerSequences(CancellationToken.None);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await EnsureLedgerSequencesAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureLedgerSequences(CancellationToken cancellationToken)
    {
        EnsureLedgerSequencesAsync(cancellationToken).GetAwaiter().GetResult();
    }

    private async Task EnsureLedgerSequencesAsync(CancellationToken cancellationToken)
    {
        var pending = ChangeTracker.Entries<InventoryStockMovementEntity>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.LedgerSequence == 0)
            .Select(entry => entry.Entity)
            .ToArray();
        foreach (var group in pending.GroupBy(item => item.CompanyId).OrderBy(item => item.Key))
        {
            var anchor = await CompanyLedgerSequenceAnchors.SingleOrDefaultAsync(item => item.CompanyId == group.Key, cancellationToken);
            if (anchor is null)
            {
                var currentMaximum = await StockMovements
                    .Where(item => item.CompanyId == group.Key)
                    .Select(item => (long?)item.LedgerSequence)
                    .MaxAsync(cancellationToken) ?? 0L;
                anchor = new InventoryCompanyLedgerSequenceAnchorEntity(TrustedTenantId, group.Key, checked(currentMaximum + 1));
                CompanyLedgerSequenceAnchors.Add(anchor);
            }

            // DbSet.Local preserves the command's deterministic Add order: opening rows,
            // control lines, or transfer shipment before receipt. The order is part of the
            // physical posting contract and never depends on PostedAt or EffectiveDate.
            var ordered = StockMovements.Local.Where(item => group.Contains(item)).ToArray();
            if (ordered.Length == 0) ordered = group.ToArray();
            var first = anchor.Reserve(ordered.Length);
            for (var index = 0; index < ordered.Length; index++) ordered[index].AssignLedgerSequence(checked(first + index));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var opening = modelBuilder.Entity<InventoryOpeningBalanceEntity>();
        ConfigureBase(opening, "OpeningBalances");
        opening.Property(item => item.WarehouseCode).HasMaxLength(128).IsRequired(); opening.Property(item => item.WarehouseName).HasMaxLength(256).IsRequired();
        opening.Property(item => item.CompanyId).IsRequired(); opening.Property(item => item.BranchId).IsRequired(false); opening.Property(item => item.WarehouseId).IsRequired();
        opening.Property(item => item.AsOfDate).IsRequired(); opening.Property(item => item.SourceOwner).HasMaxLength(256).IsRequired(); opening.Property(item => item.SourceSystem).HasMaxLength(256).IsRequired(); opening.Property(item => item.ExtractedAt).IsRequired(); opening.Property(item => item.SourceReference).HasMaxLength(512).IsRequired(false); opening.Property(item => item.Status).IsRequired(); opening.Property(item => item.CreatedByActorId).IsRequired(); opening.Property(item => item.CreatedAt).IsRequired(); opening.Property(item => item.UpdatedAt).IsRequired();
        opening.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); opening.HasIndex(item => new { item.TenantId, item.WarehouseId, item.AsOfDate });

        var row = modelBuilder.Entity<InventoryOpeningBalanceRowEntity>();
        ConfigureBase(row, "OpeningBalanceRows");
        row.Property(item => item.OpeningBalanceId).IsRequired(); row.Property(item => item.ProductId).IsRequired(); row.Property(item => item.ProductSku).HasMaxLength(128).IsRequired(); row.Property(item => item.ProductName).HasMaxLength(256).IsRequired(); row.Property(item => item.UnitOfMeasureId).IsRequired(); row.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired(); row.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); row.Property(item => item.UnitCost).HasPrecision(28, 8).IsRequired(); row.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(); row.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(false); row.Property(item => item.SourceLineReference).HasMaxLength(256).IsRequired(false); row.Property(item => item.SourceFingerprint).HasMaxLength(128).IsRequired(); row.Property(item => item.SourceIdentityConsumed).IsRequired(); row.Property(item => item.Status).IsRequired(); row.Property(item => item.ValidationCode).HasMaxLength(128).IsRequired(false); row.Property(item => item.PostedAt).IsRequired(false);
        row.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); row.HasIndex(item => new { item.TenantId, item.OpeningBalanceId }); row.HasIndex(item => new { item.TenantId, item.SourceFingerprint }).IsUnique().HasFilter("[SourceIdentityConsumed] = 1");
        row.HasOne<InventoryOpeningBalanceEntity>().WithMany(item => item.Rows).HasForeignKey(item => new { item.TenantId, item.OpeningBalanceId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var openingHistory = modelBuilder.Entity<InventoryOpeningBalanceHistoryEntity>();
        ConfigureBase(openingHistory, "OpeningBalanceHistory");
        openingHistory.Property(item => item.OpeningBalanceId).IsRequired(); openingHistory.Property(item => item.FromStatus).IsRequired(); openingHistory.Property(item => item.ToStatus).IsRequired(); openingHistory.Property(item => item.Action).HasMaxLength(128).IsRequired(); openingHistory.Property(item => item.ActorId).IsRequired(); openingHistory.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); openingHistory.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); openingHistory.Property(item => item.OccurredAt).IsRequired();
        openingHistory.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); openingHistory.HasIndex(item => new { item.TenantId, item.OpeningBalanceId, item.OccurredAt });
        openingHistory.HasOne<InventoryOpeningBalanceEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.OpeningBalanceId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        var movement = modelBuilder.Entity<InventoryStockMovementEntity>();
        ConfigureBase(movement, "StockLedgerMovements");
        movement.Property(item => item.WarehouseCode).HasMaxLength(128).IsRequired(); movement.Property(item => item.WarehouseName).HasMaxLength(256).IsRequired();
        movement.Property(item => item.CompanyId).IsRequired(); movement.Property(item => item.BranchId).IsRequired(false); movement.Property(item => item.WarehouseId).IsRequired(); movement.Property(item => item.ProductId).IsRequired(); movement.Property(item => item.ProductSku).HasMaxLength(128).IsRequired(); movement.Property(item => item.ProductName).HasMaxLength(256).IsRequired(); movement.Property(item => item.UnitOfMeasureId).IsRequired(); movement.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired(); movement.Property(item => item.Direction).IsRequired(); movement.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); movement.Property(item => item.UnitCost).HasPrecision(28, 8).IsRequired(false); movement.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(false); movement.Property(item => item.ValuationStatus).IsRequired(); movement.Property(item => item.LedgerSequence).IsRequired(); movement.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(false); movement.Property(item => item.SourceType).IsRequired(); movement.Property(item => item.SourceDocumentId).IsRequired(); movement.Property(item => item.SourceLineId).IsRequired(); movement.Property(item => item.CorrectionOfMovementId).IsRequired(false); movement.Property(item => item.GoodsReceiptId).IsRequired(false); movement.Property(item => item.GoodsReceiptLineId).IsRequired(false); movement.Property(item => item.SupplierReturnId).IsRequired(false); movement.Property(item => item.SupplierReturnLineId).IsRequired(false); movement.Property(item => item.PurchaseOrderId).IsRequired(false); movement.Property(item => item.PurchaseOrderLineId).IsRequired(false); movement.Property(item => item.TransferId).IsRequired(false); movement.Property(item => item.TransferLineId).IsRequired(false); movement.Property(item => item.SourceReference).HasMaxLength(512).IsRequired(false); movement.Property(item => item.EffectiveDate).IsRequired(); movement.Property(item => item.ActorId).IsRequired(); movement.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); movement.Property(item => item.PostedAt).IsRequired();
        movement.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); movement.HasIndex(item => new { item.TenantId, item.CompanyId, item.LedgerSequence }).IsUnique(); movement.HasIndex(item => new { item.TenantId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingIdentity }); movement.HasIndex(item => new { item.TenantId, item.SourceType, item.SourceDocumentId, item.SourceLineId }).IsUnique(); movement.HasIndex(item => new { item.TenantId, item.CorrectionOfMovementId }).IsUnique().HasFilter(Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "[CorrectionOfMovementId] IS NOT NULL" : "CorrectionOfMovementId IS NOT NULL");

        var ledgerAnchor = modelBuilder.Entity<InventoryCompanyLedgerSequenceAnchorEntity>();
        ConfigureBase(ledgerAnchor, "CompanyLedgerSequenceAnchors");
        ledgerAnchor.Property(item => item.CompanyId).IsRequired(); ledgerAnchor.Property(item => item.NextSequence).IsRequired();
        ledgerAnchor.HasIndex(item => new { item.TenantId, item.CompanyId }).IsUnique(); ledgerAnchor.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();

        var valuationPolicy = modelBuilder.Entity<InventoryValuationPolicyEntity>();
        ConfigureBase(valuationPolicy, "ValuationPolicies");
        valuationPolicy.Property(item => item.CompanyId).IsRequired(); valuationPolicy.Property(item => item.FunctionalCurrencyId).IsRequired(); valuationPolicy.Property(item => item.FunctionalCurrencyCode).HasMaxLength(16).IsRequired(); valuationPolicy.Property(item => item.ScopeMode).IsRequired(); valuationPolicy.Property(item => item.EffectiveFrom).IsRequired(); valuationPolicy.Property(item => item.EffectiveTo).IsRequired(false); valuationPolicy.Property(item => item.VersionNumber).IsRequired(); valuationPolicy.Property(item => item.SupersedesPolicyId).IsRequired(false); valuationPolicy.Property(item => item.UnitCostScale).IsRequired(); valuationPolicy.Property(item => item.AmountScale).IsRequired(); valuationPolicy.Property(item => item.RoundingMode).IsRequired(); valuationPolicy.Property(item => item.GoodsReceiptCostBasis).HasMaxLength(128).IsRequired(); valuationPolicy.Property(item => item.PositiveAdjustmentCostBasis).HasMaxLength(128).IsRequired(); valuationPolicy.Property(item => item.SupplierReturnCostBasis).HasMaxLength(128).IsRequired(); valuationPolicy.Property(item => item.IsActive).IsRequired(); valuationPolicy.Property(item => item.ActorId).IsRequired(); valuationPolicy.Property(item => item.CreatedAt).IsRequired(); valuationPolicy.Property(item => item.UpdatedAt).IsRequired(); valuationPolicy.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); valuationPolicy.HasIndex(item => new { item.TenantId, item.CompanyId, item.EffectiveFrom, item.EffectiveTo }); valuationPolicy.HasIndex(item => new { item.TenantId, item.CompanyId, item.VersionNumber }).IsUnique();

        var scopeAnchor = modelBuilder.Entity<InventoryValuationScopeAnchorEntity>();
        ConfigureBase(scopeAnchor, "ValuationScopeAnchors");
        scopeAnchor.Property(item => item.CompanyId).IsRequired(); scopeAnchor.Property(item => item.BranchId).IsRequired(false); scopeAnchor.Property(item => item.WarehouseId).IsRequired(); scopeAnchor.Property(item => item.ProductId).IsRequired(); scopeAnchor.Property(item => item.UnitOfMeasureId).IsRequired(); scopeAnchor.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(); scopeAnchor.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingIdentity }).IsUnique().HasFilter(null); scopeAnchor.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();

        var valuationState = modelBuilder.Entity<InventoryValuationStateEntity>();
        ConfigureBase(valuationState, "ValuationStates");
        valuationState.Property(item => item.CompanyId).IsRequired(); valuationState.Property(item => item.BranchId).IsRequired(false); valuationState.Property(item => item.WarehouseId).IsRequired(); valuationState.Property(item => item.ProductId).IsRequired(); valuationState.Property(item => item.UnitOfMeasureId).IsRequired(); valuationState.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(); valuationState.Property(item => item.CurrentPolicyId).IsRequired(false); valuationState.Property(item => item.CurrentPolicyVersionNumber).IsRequired(false); valuationState.Property(item => item.FunctionalCurrencyCode).HasMaxLength(16).IsRequired(); valuationState.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); valuationState.Property(item => item.Value).HasPrecision(28, 8).IsRequired(); valuationState.Property(item => item.AverageUnitCost).HasPrecision(28, 8).IsRequired(); valuationState.Property(item => item.LastAppliedLedgerSequence).IsRequired(); valuationState.Property(item => item.UpdatedAt).IsRequired(); valuationState.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingIdentity }).IsUnique().HasFilter(null); valuationState.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();

        var valuationEvent = modelBuilder.Entity<InventoryMovementValuationEventEntity>();
        ConfigureBase(valuationEvent, "MovementValuationEvents");
        valuationEvent.Property(item => item.MovementId).IsRequired(); valuationEvent.Property(item => item.SourceType).IsRequired(); valuationEvent.Property(item => item.SourceDocumentId).IsRequired(); valuationEvent.Property(item => item.SourceLineId).IsRequired(); valuationEvent.Property(item => item.CorrectionOfMovementId).IsRequired(false); valuationEvent.Property(item => item.GoodsReceiptId).IsRequired(false); valuationEvent.Property(item => item.GoodsReceiptLineId).IsRequired(false); valuationEvent.Property(item => item.SupplierReturnId).IsRequired(false); valuationEvent.Property(item => item.SupplierReturnLineId).IsRequired(false); valuationEvent.Property(item => item.PurchaseOrderId).IsRequired(false); valuationEvent.Property(item => item.PurchaseOrderLineId).IsRequired(false); valuationEvent.Property(item => item.TransferId).IsRequired(false); valuationEvent.Property(item => item.TransferLineId).IsRequired(false); valuationEvent.Property(item => item.SourceReference).HasMaxLength(512).IsRequired(false); valuationEvent.Property(item => item.LedgerSequence).IsRequired(); valuationEvent.Property(item => item.Status).IsRequired(); valuationEvent.Property(item => item.StatusCode).HasMaxLength(128).IsRequired(); valuationEvent.Property(item => item.CompanyId).IsRequired(); valuationEvent.Property(item => item.BranchId).IsRequired(false); valuationEvent.Property(item => item.WarehouseId).IsRequired(); valuationEvent.Property(item => item.ProductId).IsRequired(); valuationEvent.Property(item => item.UnitOfMeasureId).IsRequired(); valuationEvent.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(); valuationEvent.Property(item => item.PolicyId).IsRequired(false); valuationEvent.Property(item => item.PolicyVersionNumber).IsRequired(false); valuationEvent.Property(item => item.FunctionalCurrencyCode).HasMaxLength(16).IsRequired(false); valuationEvent.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); valuationEvent.Property(item => item.Direction).IsRequired(); valuationEvent.Property(item => item.TransactionUnitCost).HasPrecision(28, 8).IsRequired(false); valuationEvent.Property(item => item.TransactionCurrencyCode).HasMaxLength(16).IsRequired(false); valuationEvent.Property(item => item.ExchangeRateId).IsRequired(false); valuationEvent.Property(item => item.ExchangeRateVersionId).IsRequired(false); valuationEvent.Property(item => item.ExchangeRateVersionNumber).IsRequired(false); valuationEvent.Property(item => item.ExchangeRate).HasPrecision(28, 12).IsRequired(false); valuationEvent.Property(item => item.ExchangeRateScale).IsRequired(false); valuationEvent.Property(item => item.ExchangeRateProvenance).HasMaxLength(128).IsRequired(false); valuationEvent.Property(item => item.EffectiveOn).IsRequired(); valuationEvent.Property(item => item.BaseUnitCost).HasPrecision(28, 8).IsRequired(false); valuationEvent.Property(item => item.PriorQuantity).HasPrecision(28, 8).IsRequired(); valuationEvent.Property(item => item.PriorValue).HasPrecision(28, 8).IsRequired(); valuationEvent.Property(item => item.NewQuantity).HasPrecision(28, 8).IsRequired(); valuationEvent.Property(item => item.NewValue).HasPrecision(28, 8).IsRequired(); valuationEvent.Property(item => item.MovementValue).HasPrecision(28, 8).IsRequired(false); valuationEvent.Property(item => item.FormulaMovementValue).HasPrecision(28, 8).IsRequired(false); valuationEvent.Property(item => item.RoundingAdjustmentAmount).HasPrecision(28, 8).IsRequired(false); valuationEvent.Property(item => item.UnitCostScale).IsRequired(false); valuationEvent.Property(item => item.AmountScale).IsRequired(false); valuationEvent.Property(item => item.RoundingMode).IsRequired(false); valuationEvent.Property(item => item.CorrectionOfValuationEventId).IsRequired(false); valuationEvent.Property(item => item.SourceRevisionId).IsRequired(false); valuationEvent.Property(item => item.IsBackdated).IsRequired(); valuationEvent.Property(item => item.PendingReason).HasMaxLength(512).IsRequired(false); valuationEvent.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); valuationEvent.Property(item => item.ActorId).IsRequired(); valuationEvent.Property(item => item.OccurredAt).IsRequired(); valuationEvent.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); valuationEvent.HasIndex(item => new { item.TenantId, item.MovementId, item.Status }).IsUnique().HasFilter(Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "[Status] = 2" : "Status = 2"); valuationEvent.HasIndex(item => new { item.TenantId, item.CompanyId, item.LedgerSequence }); valuationEvent.HasIndex(item => new { item.TenantId, item.SourceRevisionId }).IsUnique().HasFilter(Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "[SourceRevisionId] IS NOT NULL" : "SourceRevisionId IS NOT NULL");

        var valuationRun = modelBuilder.Entity<InventoryValuationRunEntity>();
        ConfigureBase(valuationRun, "ValuationRuns");
        valuationRun.Property(item => item.ActorId).IsRequired(); valuationRun.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(); valuationRun.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired(); valuationRun.Property(item => item.ResultJson).HasMaxLength(262144).IsRequired(); valuationRun.Property(item => item.CreatedAt).IsRequired(); valuationRun.HasIndex(item => new { item.TenantId, item.ActorId, item.IdempotencyKey }).IsUnique(); valuationRun.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();

        var financeHandoff = modelBuilder.Entity<InventoryFinanceValuationHandoffEntity>();
        ConfigureBase(financeHandoff, "FinanceValuationHandoffs");
        financeHandoff.Property(item => item.CompanyId).IsRequired(); financeHandoff.Property(item => item.BranchId).IsRequired(false); financeHandoff.Property(item => item.WarehouseId).IsRequired(); financeHandoff.Property(item => item.MovementId).IsRequired(); financeHandoff.Property(item => item.LedgerSequence).IsRequired(); financeHandoff.Property(item => item.SourceType).IsRequired(); financeHandoff.Property(item => item.SourceDocumentId).IsRequired(); financeHandoff.Property(item => item.SourceLineId).IsRequired(); financeHandoff.Property(item => item.ValuationEvidenceId).IsRequired(); financeHandoff.Property(item => item.ValuationEvidenceVersion).IsRequired(); financeHandoff.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); financeHandoff.Property(item => item.Direction).IsRequired(); financeHandoff.Property(item => item.BaseUnitCost).HasPrecision(28, 8).IsRequired(); financeHandoff.Property(item => item.BaseAmount).HasPrecision(28, 8).IsRequired(); financeHandoff.Property(item => item.SignedBaseAmount).HasPrecision(28, 8).IsRequired(); financeHandoff.Property(item => item.RoundingAdjustmentAmount).HasPrecision(28, 8).IsRequired(); financeHandoff.Property(item => item.PolicyId).IsRequired(); financeHandoff.Property(item => item.PolicyVersionNumber).IsRequired(); financeHandoff.Property(item => item.FunctionalCurrencyCode).HasMaxLength(16).IsRequired(); financeHandoff.Property(item => item.TransactionUnitCost).HasPrecision(28, 8).IsRequired(false); financeHandoff.Property(item => item.TransactionCurrencyCode).HasMaxLength(16).IsRequired(false); financeHandoff.Property(item => item.ExchangeRateId).IsRequired(false); financeHandoff.Property(item => item.ExchangeRateVersionId).IsRequired(false); financeHandoff.Property(item => item.ExchangeRateVersionNumber).IsRequired(false); financeHandoff.Property(item => item.ExchangeRate).HasPrecision(28, 12).IsRequired(false); financeHandoff.Property(item => item.ExchangeRateScale).IsRequired(false); financeHandoff.Property(item => item.ExchangeRateProvenance).HasMaxLength(128).IsRequired(false); financeHandoff.Property(item => item.ProductId).IsRequired(); financeHandoff.Property(item => item.UnitOfMeasureId).IsRequired(); financeHandoff.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(false); financeHandoff.Property(item => item.CorrectionOfMovementId).IsRequired(false); financeHandoff.Property(item => item.Status).IsRequired(); financeHandoff.Property(item => item.ContractVersion).HasMaxLength(128).IsRequired(); financeHandoff.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); financeHandoff.Property(item => item.AsOf).IsRequired(); financeHandoff.Property(item => item.CreatedAt).IsRequired(); financeHandoff.HasIndex(item => new { item.TenantId, item.MovementId }).IsUnique(); financeHandoff.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.WarehouseId, item.LedgerSequence }).IsUnique().HasFilter(null); financeHandoff.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();

        var transfer = modelBuilder.Entity<InventoryTransferEntity>();
        ConfigureBase(transfer, "Transfers");
        transfer.Property(item => item.CompanyId).IsRequired(); transfer.Property(item => item.BranchId).IsRequired(false);
        transfer.Property(item => item.SourceWarehouseId).IsRequired(); transfer.Property(item => item.SourceWarehouseCode).HasMaxLength(128).IsRequired(); transfer.Property(item => item.SourceWarehouseName).HasMaxLength(256).IsRequired();
        transfer.Property(item => item.DestinationWarehouseId).IsRequired(); transfer.Property(item => item.DestinationWarehouseCode).HasMaxLength(128).IsRequired(); transfer.Property(item => item.DestinationWarehouseName).HasMaxLength(256).IsRequired();
        transfer.Property(item => item.ProductId).IsRequired(); transfer.Property(item => item.ProductSku).HasMaxLength(128).IsRequired(); transfer.Property(item => item.ProductName).HasMaxLength(256).IsRequired(); transfer.Property(item => item.UnitOfMeasureId).IsRequired(); transfer.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired();
        transfer.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); transfer.Property(item => item.Mode).IsRequired(); transfer.Property(item => item.Status).IsRequired(); transfer.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(false); transfer.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); transfer.Property(item => item.ActorId).IsRequired(); transfer.Property(item => item.CreatedAt).IsRequired(); transfer.Property(item => item.UpdatedAt).IsRequired();
        transfer.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); transfer.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.Status, item.CreatedAt });

        var transferLine = modelBuilder.Entity<InventoryTransferLineEntity>();
        ConfigureBase(transferLine, "TransferLines");
        transferLine.Property(item => item.TransferId).IsRequired(); transferLine.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired();
        transferLine.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); transferLine.HasIndex(item => new { item.TenantId, item.TransferId }).IsUnique();
        transferLine.HasOne<InventoryTransferEntity>().WithMany(item => item.Lines).HasForeignKey(item => new { item.TenantId, item.TransferId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var transferEvent = modelBuilder.Entity<InventoryTransferEventEntity>();
        ConfigureBase(transferEvent, "TransferEvents");
        transferEvent.Property(item => item.TransferId).IsRequired(); transferEvent.Property(item => item.TransferLineId).IsRequired(); transferEvent.Property(item => item.EventType).IsRequired(); transferEvent.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); transferEvent.Property(item => item.Reference).HasMaxLength(512).IsRequired(false); transferEvent.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); transferEvent.Property(item => item.ActorId).IsRequired(); transferEvent.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); transferEvent.Property(item => item.OccurredAt).IsRequired(); transferEvent.Property(item => item.SourceMovementId).IsRequired(false); transferEvent.Property(item => item.DestinationMovementId).IsRequired(false);
        transferEvent.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); transferEvent.HasIndex(item => new { item.TenantId, item.TransferId, item.EventType, item.Reference }).IsUnique().HasFilter(null); transferEvent.HasIndex(item => new { item.TenantId, item.TransferId, item.OccurredAt });
        transferEvent.HasOne<InventoryTransferEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.TransferId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        transferEvent.HasOne<InventoryTransferLineEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.TransferLineId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        var reservation = modelBuilder.Entity<InventoryReservationEntity>();
        ConfigureBase(reservation, "Reservations");
        reservation.Property(item => item.WarehouseCode).HasMaxLength(128).IsRequired(); reservation.Property(item => item.WarehouseName).HasMaxLength(256).IsRequired();
        reservation.Property(item => item.CompanyId).IsRequired(); reservation.Property(item => item.BranchId).IsRequired(false); reservation.Property(item => item.WarehouseId).IsRequired(); reservation.Property(item => item.ProductId).IsRequired(); reservation.Property(item => item.ProductSku).HasMaxLength(128).IsRequired(); reservation.Property(item => item.ProductName).HasMaxLength(256).IsRequired(); reservation.Property(item => item.UnitOfMeasureId).IsRequired(); reservation.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired(); reservation.Property(item => item.TrackingIdentity).HasMaxLength(256).IsRequired(false); reservation.Property(item => item.SourceType).HasMaxLength(128).IsRequired(); reservation.Property(item => item.SourceReference).HasMaxLength(512).IsRequired(); reservation.Property(item => item.SourceDocumentId).IsRequired(false); reservation.Property(item => item.SourceLineId).IsRequired(false); reservation.Property(item => item.SourceRevision).IsRequired(false); reservation.Property(item => item.RequestedQuantity).HasPrecision(28, 8).IsRequired(); reservation.Property(item => item.ReservedQuantity).HasPrecision(28, 8).IsRequired(); reservation.Property(item => item.UnallocatedQuantity).HasPrecision(28, 8).IsRequired(); reservation.Property(item => item.FulfilledQuantity).HasPrecision(28, 8).IsRequired(); reservation.Property(item => item.Status).IsRequired(); reservation.Property(item => item.ActorId).IsRequired(); reservation.Property(item => item.CreatedAt).IsRequired(); reservation.Property(item => item.UpdatedAt).IsRequired();
        reservation.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); reservation.HasIndex(item => new { item.TenantId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingIdentity, item.Status });

        var reservationHistory = modelBuilder.Entity<InventoryReservationHistoryEntity>();
        ConfigureBase(reservationHistory, "ReservationHistory");
        reservationHistory.Property(item => item.ReservationId).IsRequired(); reservationHistory.Property(item => item.Action).IsRequired(); reservationHistory.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired(); reservationHistory.Property(item => item.ReservedQuantityAfter).HasPrecision(28, 8).IsRequired(); reservationHistory.Property(item => item.UnallocatedQuantityAfter).HasPrecision(28, 8).IsRequired(); reservationHistory.Property(item => item.FulfilledQuantityAfter).HasPrecision(28, 8).IsRequired(); reservationHistory.Property(item => item.ActorId).IsRequired(); reservationHistory.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); reservationHistory.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); reservationHistory.Property(item => item.OccurredAt).IsRequired();
        reservationHistory.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); reservationHistory.HasIndex(item => new { item.TenantId, item.ReservationId, item.OccurredAt });
        reservationHistory.HasOne<InventoryReservationEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.ReservationId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        var audit = modelBuilder.Entity<InventoryAuditEntity>();
        ConfigureBase(audit, "AuditEvents");
        audit.Property(item => item.ResourceType).HasMaxLength(128).IsRequired(); audit.Property(item => item.ResourceId).IsRequired(); audit.Property(item => item.OperationId).HasMaxLength(128).IsRequired(); audit.Property(item => item.ActorId).IsRequired(); audit.Property(item => item.SessionId).IsRequired(); audit.Property(item => item.AuthorizationPath).HasMaxLength(64).IsRequired(); audit.Property(item => item.Decision).HasMaxLength(64).IsRequired(); audit.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); audit.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); audit.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(false); audit.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired(false); audit.Property(item => item.BeforeSummary).HasMaxLength(4096).IsRequired(false); audit.Property(item => item.AfterSummary).HasMaxLength(4096).IsRequired(false); audit.Property(item => item.OccurredAt).IsRequired();
        audit.HasIndex(item => new { item.TenantId, item.Id }).IsUnique(); audit.HasIndex(item => new { item.TenantId, item.ResourceType, item.ResourceId, item.OccurredAt });

        var idem = modelBuilder.Entity<InventoryIdempotencyEntity>();
        ConfigureBase(idem, "IdempotencyEntries");
        idem.Property(item => item.ActorId).IsRequired(); idem.Property(item => item.OperationId).HasMaxLength(128).IsRequired(); idem.Property(item => item.Key).HasMaxLength(256).IsRequired(); idem.Property(item => item.Fingerprint).HasMaxLength(128).IsRequired(); idem.Property(item => item.ResourceType).HasMaxLength(128).IsRequired(); idem.Property(item => item.ResourceId).IsRequired(); idem.Property(item => item.SnapshotJson).HasMaxLength(262144).IsRequired(); idem.Property(item => item.CreatedAt).IsRequired(); idem.HasIndex(item => new { item.TenantId, item.ActorId, item.OperationId, item.Key }).IsUnique(); idem.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();

        var anchor = modelBuilder.Entity<InventoryConcurrencyAnchorEntity>();
        ConfigureBase(anchor, "ConcurrencyAnchors");
        anchor.Property(item => item.CompanyId).IsRequired(); anchor.Property(item => item.BranchId).IsRequired(false); anchor.Property(item => item.WarehouseId).IsRequired(); anchor.Property(item => item.ProductId).IsRequired(); anchor.Property(item => item.UnitOfMeasureId).IsRequired(); anchor.Property(item => item.TrackingKey).HasMaxLength(256).IsRequired(); anchor.Property(item => item.TouchSequence).IsRequired(); anchor.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingKey }).IsUnique().HasFilter(null); anchor.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();

        InventoryStockControlModelBuilder.Configure(modelBuilder, Database.ProviderName);
        var customerReturn = modelBuilder.Entity<InventoryCustomerReturnEntity>();
        ConfigureBase(customerReturn, "CustomerReturns");
         customerReturn.Property(item => item.SalesCustomerReturnId).IsRequired(); customerReturn.Property(item => item.CompanyId).IsRequired(); customerReturn.Property(item => item.BranchId).IsRequired(false); customerReturn.Property(item => item.WarehouseId).IsRequired(); customerReturn.Property(item => item.Status).IsRequired(); customerReturn.Property(item => item.PhysicalEvidenceReference).HasMaxLength(2048).IsRequired(); customerReturn.Property(item => item.InspectionEvidenceReference).HasMaxLength(2048).IsRequired(); customerReturn.Property(item => item.HandoffState).HasMaxLength(64).IsRequired(); customerReturn.Property(item => item.CommitState).HasMaxLength(32).IsRequired(); customerReturn.Property(item => item.AcknowledgementState).HasMaxLength(32).IsRequired(); customerReturn.Property(item => item.ReconciliationState).HasMaxLength(32).IsRequired(); customerReturn.Property(item => item.EffectFingerprint).HasMaxLength(128).IsRequired(); customerReturn.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired(); customerReturn.Property(item => item.DownstreamIdempotencyKey).HasMaxLength(256).IsRequired(false); customerReturn.Property(item => item.LastError).HasMaxLength(256).IsRequired(false); customerReturn.Property(item => item.LastAttemptAt).IsRequired(false); customerReturn.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); customerReturn.Property(item => item.AttemptCount).IsRequired(); customerReturn.Property(item => item.ReceiptDate).IsRequired(false); customerReturn.Property(item => item.PostedAt).IsRequired(false); customerReturn.Property(item => item.ActorId).IsRequired(); customerReturn.Property(item => item.CreatedAt).IsRequired(); customerReturn.Property(item => item.UpdatedAt).IsRequired(); customerReturn.HasIndex(item => new { item.TenantId, item.SalesCustomerReturnId }).IsUnique(); customerReturn.HasIndex(item => new { item.TenantId, item.EffectFingerprint });
        var customerReturnLine = modelBuilder.Entity<InventoryCustomerReturnLineEntity>();
        ConfigureBase(customerReturnLine, "CustomerReturnLines");
         customerReturnLine.Property(item => item.InventoryCustomerReturnId).IsRequired(); customerReturnLine.Property(item => item.OrderLineId).IsRequired(); customerReturnLine.Property(item => item.ReceivedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.DispositionedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.CommerciallyAcceptedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.RestockedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.NonRestockableAcceptedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.RejectedQuantity).HasPrecision(28, 8).IsRequired(); customerReturnLine.Property(item => item.MovementIdsJson).HasMaxLength(8192).IsRequired(); customerReturnLine.Property(item => item.DeliveryMovementIdsJson).HasMaxLength(8192).IsRequired(); customerReturnLine.Property(item => item.ReversalMovementIdsJson).HasMaxLength(8192).IsRequired(); customerReturnLine.Property(item => item.Disposition).IsRequired(); customerReturnLine.Property(item => item.MovementId).IsRequired(false); customerReturnLine.Property(item => item.DeliveryMovementId).IsRequired(false); customerReturnLine.Property(item => item.DeliveryUnitCost).HasPrecision(28, 8).IsRequired(false); customerReturnLine.Property(item => item.Notes).HasMaxLength(2048).IsRequired(false); customerReturnLine.HasIndex(item => new { item.TenantId, item.InventoryCustomerReturnId, item.OrderLineId }).IsUnique(); customerReturnLine.HasOne<InventoryCustomerReturnEntity>().WithMany(item => item.Lines).HasForeignKey(item => new { item.TenantId, item.InventoryCustomerReturnId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InventoryReasonCodeEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryAdjustmentEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryAdjustmentLineEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryCountEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryCountSnapshotEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryCountLineEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryStockIssueEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryStockIssueLineEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
        modelBuilder.Entity<InventoryControlHistoryEntity>().HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }

    private void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName) where TEntity : class, ITenantOwned
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer") entity.ToTable(tableName, "inventory"); else entity.ToTable(tableName);
        entity.HasKey("Id"); entity.Property("Id").ValueGeneratedNever();
        entity.Property(item => item.TenantId).HasConversion(item => item.Value, value => new TenantId(value)).IsRequired();
        var version = entity.Property<byte[]>("Version").IsRequired().IsConcurrencyToken();
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer") version.IsRowVersion(); else version.ValueGeneratedNever();
        entity.HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }
}

#pragma warning restore CS1591
