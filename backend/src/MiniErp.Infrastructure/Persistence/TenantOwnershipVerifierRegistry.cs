using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Internal registration for the one stored-owner verifier associated with a
/// concrete mapped ITenantOwned CLR type.
/// </summary>
internal sealed class TenantOwnershipVerifierRegistration
{
    internal TenantOwnershipVerifierRegistration(
        Type entityType,
        Func<TenantPersistenceDbContext, EntityEntry, TenantId?> readStoredTenantId,
        Func<TenantPersistenceDbContext, EntityEntry, CancellationToken, Task<TenantId?>> readStoredTenantIdAsync)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        ReadStoredTenantId = readStoredTenantId ?? throw new ArgumentNullException(nameof(readStoredTenantId));
        ReadStoredTenantIdAsync = readStoredTenantIdAsync
            ?? throw new ArgumentNullException(nameof(readStoredTenantIdAsync));

        if (!typeof(ITenantOwned).IsAssignableFrom(entityType) || entityType.IsAbstract)
        {
            throw new ArgumentException(
                "A verifier registration must target a concrete ITenantOwned type.",
                nameof(entityType));
        }
    }

    internal Type EntityType { get; }

    internal Func<TenantPersistenceDbContext, EntityEntry, TenantId?> ReadStoredTenantId { get; }

    internal Func<TenantPersistenceDbContext, EntityEntry, CancellationToken, Task<TenantId?>>
        ReadStoredTenantIdAsync { get; }
}

/// <summary>
/// Immutable, infrastructure-only registry for stored-owner verification.
/// </summary>
internal sealed class TenantOwnershipVerifierRegistry
{
    private readonly IReadOnlyDictionary<Type, TenantOwnershipVerifierRegistration> _registrations;

    internal TenantOwnershipVerifierRegistry(
        IEnumerable<TenantOwnershipVerifierRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var registrationList = registrations.ToArray();
        var duplicate = registrationList
            .GroupBy(registration => registration.EntityType)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate tenant ownership verifier registration for '{duplicate.Key.FullName}'.");
        }

        _registrations = new ReadOnlyDictionary<Type, TenantOwnershipVerifierRegistration>(
            registrationList.ToDictionary(registration => registration.EntityType));
    }

    internal static TenantOwnershipVerifierRegistry CreateDefault()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync)
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateMasterData()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync),
            MasterDataTenantOwnershipVerifier.For<MasterDataCategoryEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataUnitOfMeasureEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataConversionEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataProductEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataProductBarcodeEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataCurrencyEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataPaymentTermEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataPaymentTermVersionEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataPaymentTermInstallmentEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataExchangeRateEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataExchangeRateVersionEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataTaxEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataTaxRateVersionEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataPriceListEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataPriceListPriceEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataAuditEventEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataImportBatchEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataImportRowEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataImportAuditEntity>()
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateBusinessParties()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync),
            BusinessPartiesTenantOwnershipVerifier.For<BusinessPartiesSupplierEntity>(),
            BusinessPartiesTenantOwnershipVerifier.For<BusinessPartiesSupplierContactEntity>(),
            BusinessPartiesTenantOwnershipVerifier.For<BusinessPartiesCustomerEntity>(),
            BusinessPartiesTenantOwnershipVerifier.For<BusinessPartiesCustomerContactEntity>(),
            BusinessPartiesTenantOwnershipVerifier.For<BusinessPartiesAuditEventEntity>()
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateProcurement()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync),
            ProcurementTenantOwnershipVerifier.For<PurchaseRequestEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseRequestLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseRequestHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseRequestAuditEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierQuotationEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierQuotationLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierQuotationEvidenceEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierQuotationHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierQuotationAuditEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierSourceDecisionEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierSourceDecisionHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderConfirmationEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderConfirmationLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderEvidenceEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderSupplierChangeEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseOrderAuditEntity>(),
            ProcurementTenantOwnershipVerifier.For<GoodsReceiptEntity>(),
            ProcurementTenantOwnershipVerifier.For<GoodsReceiptLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<GoodsReceiptHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<GoodsReceiptAuditEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceHandoffEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceHandoffLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceHandoffSourceEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceHandoffHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceHandoffAuditEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceDeclaredEvidenceEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceDeclaredEvidenceLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceDeclaredEvidenceAllocationEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceMatchEvaluationEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceMatchHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<PurchaseInvoiceMatchAuditEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierReturnEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierReturnLineEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierReturnEvidenceEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierReturnHistoryEntity>(),
            ProcurementTenantOwnershipVerifier.For<SupplierReturnAuditEntity>()
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateInventory()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync),
            InventoryTenantOwnershipVerifier.For<InventoryOpeningBalanceEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryOpeningBalanceRowEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryOpeningBalanceHistoryEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryStockMovementEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryTransferEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryTransferLineEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryTransferEventEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryReservationEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryReservationHistoryEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryAuditEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryIdempotencyEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryConcurrencyAnchorEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryReasonCodeEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryAdjustmentEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryAdjustmentLineEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryCountEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryCountSnapshotEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryCountLineEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryStockIssueEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryStockIssueLineEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryControlHistoryEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryCompanyLedgerSequenceAnchorEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryValuationPolicyEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryValuationScopeAnchorEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryValuationStateEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryMovementValuationEventEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryValuationRunEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryFinanceValuationHandoffEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryCustomerReturnEntity>(),
            InventoryTenantOwnershipVerifier.For<InventoryCustomerReturnLineEntity>()
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateFinance()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(typeof(TenantOwnedRecord), TenantOwnershipStoreVerifier.ReadStoredTenantId, TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync),
            FinanceTenantOwnershipVerifier.For<FinanceAccountEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceFiscalCalendarEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceFiscalYearEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceFiscalPeriodEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceCostCenterEntity>(),
            FinanceTenantOwnershipVerifier.For<FinancePostingRuleEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceJournalEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceJournalLineEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceJournalMonetaryEvidenceEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceAuditEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceIdempotencyEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceSourceEffectEntity>(),
            FinanceTenantOwnershipVerifier.For<FinancePaymentMethodEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceCashAccountEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceOpenItemEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceSettlementDocumentEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceAllocationEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceMonetaryPolicyEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceTaxAccountingEffectEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceRevaluationBatchEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceRevaluationLineEntity>(),
            FinanceTenantOwnershipVerifier.For<FinancePeriodCloseEvidenceEntity>(),
            FinanceTenantOwnershipVerifier.For<FinancePeriodCloseRunEntity>(),
            FinanceTenantOwnershipVerifier.For<FinancePeriodHistoryEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceYearEndRunEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceYearEndLineEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceCreditNoteEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceCreditNoteLineEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceCustomerCreditEntity>(),
            FinanceTenantOwnershipVerifier.For<FinanceCustomerCreditApplicationEntity>()
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateSales()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync),
            SalesTenantOwnershipVerifier.For<SalesQuotationEntity>(),
            SalesTenantOwnershipVerifier.For<SalesQuotationRevisionEntity>(),
            SalesTenantOwnershipVerifier.For<SalesOrderEntity>(),
            SalesTenantOwnershipVerifier.For<SalesHistoryEntity>(),
            SalesTenantOwnershipVerifier.For<SalesAuditEntity>(),
            SalesTenantOwnershipVerifier.For<SalesIdempotencyEntity>(),
            SalesTenantOwnershipVerifier.For<SalesCreditEntity>(),
            SalesTenantOwnershipVerifier.For<SalesDeliveryEntity>(),
            SalesTenantOwnershipVerifier.For<SalesInvoiceRequestEntity>(),
            SalesTenantOwnershipVerifier.For<SalesCustomerReturnEntity>(),
            SalesTenantOwnershipVerifier.For<SalesCustomerReturnLineEntity>(),
            SalesTenantOwnershipVerifier.For<SalesCustomerReturnInvoiceAllocationEntity>(),
            SalesTenantOwnershipVerifier.For<SalesCustomerReturnFinanceEffectEntity>(),
            SalesTenantOwnershipVerifier.For<SalesCustomerReturnFinanceEffectAllocationEntity>()
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateMigration()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            MigrationTenantOwnershipVerifier.For<MigrationRunEntity>(),
            MigrationTenantOwnershipVerifier.For<MigrationAttemptEntity>(),
            MigrationTenantOwnershipVerifier.For<MigrationIdempotencyEntity>(),
            MigrationTenantOwnershipVerifier.For<MigrationIntakeEntity>()
        ]);
    }

    internal bool TryGet(Type entityType, out TenantOwnershipVerifierRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _registrations.TryGetValue(entityType, out registration!);
    }

    internal void ValidateModel(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var mappedTenantOwnedTypes = model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => type is not null
                && !type.IsAbstract
                && typeof(ITenantOwned).IsAssignableFrom(type))
            .Distinct()
            .ToArray();

        var missing = mappedTenantOwnedTypes
            .Where(type => !_registrations.ContainsKey(type))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var orphaned = _registrations.Keys
            .Where(type => !mappedTenantOwnedTypes.Contains(type))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (missing.Length != 0 || orphaned.Length != 0)
        {
            throw new InvalidOperationException(
                "Tenant ownership verifier registry does not exactly match the concrete mapped "
                + $"ITenantOwned types. Missing: [{string.Join(", ", missing)}]. "
                + $"Orphaned: [{string.Join(", ", orphaned)}].");
        }
    }
}
