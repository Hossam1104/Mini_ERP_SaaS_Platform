#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MiniErp.App.Modules.Finance;

namespace MiniErp.App.Modules.Sales;

public static class SalesModuleRegistration
{
    public static IServiceCollection AddSalesApplication(this IServiceCollection services, IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SalesAuthorizationService>();
        if (configuration is null)
        {
            services.AddSingleton<ISalesApprovalPolicyProvider, DefaultSalesApprovalPolicyProvider>();
            services.AddSingleton<ISalesCommercialAuthorityProvider, NoSalesCommercialAuthorityProvider>();
            services.AddSingleton<ISalesApprovalDelegationProvider, NoSalesApprovalDelegationProvider>();
            services.AddSingleton<ISalesCreditLimitProvider, NoSalesCreditLimitProvider>();
        }
        else
        {
            services.AddOptions<SalesPolicyOptions>().Bind(configuration.GetSection("MESP_SALES_POLICIES"));
            services.AddSingleton<ISalesApprovalPolicyProvider, ConfigurationSalesApprovalPolicyProvider>();
            services.AddSingleton<ISalesCommercialAuthorityProvider, ConfigurationSalesCommercialAuthorityProvider>();
            services.AddSingleton<ISalesApprovalDelegationProvider, ConfigurationSalesApprovalDelegationProvider>();
            services.AddSingleton<ISalesCreditLimitProvider, ConfigurationSalesCreditLimitProvider>();
        }
        services.AddSingleton<ISalesTaxReferenceProvider, MasterDataSalesTaxReferenceProvider>();
        services.AddSingleton<ISalesExchangeRateReferenceProvider, MasterDataSalesExchangeRateReferenceProvider>();
        services.AddSingleton<ISalesPersistence, UnavailableSalesPersistence>();
        services.AddSingleton<ISalesCustomerReturnPersistence, UnavailableSalesCustomerReturnPersistence>();
        services.AddSingleton<SalesCustomerReturnService>();
        services.AddSingleton<SalesService>();
        return services;
    }
}

#pragma warning restore CS1591
