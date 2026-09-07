#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MiniErp.App.Modules.Procurement;

public static class PurchaseRequestServiceCollectionExtensions
{
    public static IServiceCollection AddPurchaseRequestApprovalFoundation(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ProcurementTenantContextResolver>();
        services.AddSingleton<PurchaseRequestAuthorizationService>();
        services.AddSingleton<IPurchaseRequestPersistence, UnavailablePurchaseRequestPersistence>();
        services.AddSingleton<ISupplierQuotationPersistence, UnavailableSupplierQuotationPersistence>();
        services.AddSingleton<IPurchaseOrderPersistence, UnavailablePurchaseOrderPersistence>();
        services.AddSingleton<IGoodsReceiptPersistence, UnavailableGoodsReceiptPersistence>();
        services.AddSingleton<ISupplierReturnPersistence, UnavailableSupplierReturnPersistence>();
        services.AddSingleton<ISupplierReturnInventoryEffectReader, UnavailableSupplierReturnInventoryEffectReader>();
        services.AddSingleton<ISupplierReturnPhysicalEffectGate, SupplierReturnPhysicalEffectGate>();
        services.AddSingleton<IPurchaseInvoiceHandoffPersistence, UnavailablePurchaseInvoiceHandoffPersistence>();
        services.AddSingleton<IPurchaseInvoiceMatchPersistence, UnavailablePurchaseInvoiceMatchPersistence>();
        services.AddSingleton<IPurchaseInvoiceMatchReportingReadPort, UnavailablePurchaseInvoiceMatchPersistence>();
        services.AddSingleton<IPurchaseRequestApprovalPolicyProvider, DefaultPurchaseRequestApprovalPolicyProvider>();
        services.AddSingleton<IPurchaseRequestApprovalDelegationProvider, NoPurchaseRequestApprovalDelegationProvider>();
        services.AddOptions<PurchaseInvoiceMatchingPolicyOptions>();
        if (configuration is not null)
        {
            services.Configure<PurchaseInvoiceMatchingPolicyOptions>(
                configuration.GetSection("MESP_PURCHASE_INVOICE_MATCHING"));
        }

        services.AddSingleton<IPurchaseInvoiceMatchingTolerancePolicyProvider, ConfigurationPurchaseInvoiceMatchingTolerancePolicyProvider>();
        services.AddSingleton<IPurchaseInvoiceMatchingResolutionPolicyProvider, ConfigurationPurchaseInvoiceMatchingResolutionPolicyProvider>();
        services.AddSingleton<IPurchaseInvoiceMatchingExchangeRateReferenceProvider, MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider>();
        services.AddSingleton<IProcurementOrganizationScopeProvider, NoProcurementOrganizationScopeProvider>();
        services.AddSingleton<IProcurementWarehouseProvider, NoProcurementWarehouseProvider>();
        services.AddSingleton<PurchaseRequestService>();
        services.AddSingleton<SupplierQuotationService>();
        services.AddSingleton<PurchaseOrderService>();
        services.AddSingleton<GoodsReceiptService>();
        services.AddSingleton<SupplierReturnService>();
        services.AddSingleton<PurchaseInvoiceHandoffService>();
        services.AddSingleton<PurchaseInvoiceMatchService>();
        return services;
    }
}

#pragma warning restore CS1591
