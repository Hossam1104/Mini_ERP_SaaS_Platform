#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;

namespace MiniErp.App.Modules.Inventory;

public static class InventoryModuleRegistration
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<InventoryTenantContextResolver>();
        services.AddSingleton<InventoryResourceAuthorizationService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<ISalesInventoryPort>(provider => provider.GetRequiredService<InventoryService>());
        services.AddSingleton<InventoryValuationService>();
        services.AddSingleton<IInventoryWarehouseProvider, NoInventoryWarehouseProvider>();
        services.AddSingleton<IInventoryProductProvider, NoInventoryProductProvider>();
        services.AddSingleton<IInventoryGoodsReceiptSourceProvider, NoInventoryGoodsReceiptSourceProvider>();
        services.AddSingleton<IInventorySupplierReturnSourceProvider, NoInventorySupplierReturnSourceProvider>();
        services.AddSingleton<IInventorySupplierReturnStateProvider, NoInventorySupplierReturnStateProvider>();
        services.AddSingleton<IInventorySupplierReturnHandoffWriter, NoInventorySupplierReturnHandoffWriter>();
        services.AddSingleton<IInventoryApprovalPolicyProvider, NoInventoryApprovalPolicyProvider>();
        services.AddSingleton<IInventoryApprovalDelegationProvider, NoInventoryApprovalDelegationProvider>();
        services.AddSingleton<IInventoryPersistence, UnavailableInventoryPersistence>();
        services.AddSingleton<IInventoryCustomerReturnPersistence, UnavailableInventoryCustomerReturnPersistence>();
        services.AddSingleton<InventoryCustomerReturnService>();
        services.AddSingleton<IInventoryValuationPersistence, UnavailableInventoryValuationPersistence>();
        services.AddSingleton<IGoodsReceiptInventoryEffectReader, InventoryPersistenceGoodsReceiptEffectReader>();
        services.AddSingleton<ISupplierReturnInventoryEffectReader, InventoryPersistenceSupplierReturnEffectReader>();
        return services;
    }
}

#pragma warning restore CS1591
