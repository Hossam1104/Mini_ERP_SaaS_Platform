#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Finance;

public static class FinanceModuleRegistration
{
    public static IServiceCollection AddFinanceApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IFinanceCompanyProvider, NoFinanceCompanyProvider>();
        services.AddSingleton<IFinanceSourceApprovalPolicy, UnconfiguredFinanceSourceApprovalPolicy>();
        services.AddSingleton<IFinancePersistence, UnavailableFinancePersistence>();
        services.AddSingleton<IFinanceSettlementPersistence, UnavailableFinanceSettlementPersistence>();
        services.AddSingleton<IFinanceMesp134Persistence, UnavailableFinanceMesp134Persistence>();
        services.AddSingleton<IFinanceMesp135Persistence, UnavailableFinanceMesp135Persistence>();
        services.AddSingleton<IFinanceSupplierInvoiceSourceProvider, UnavailableFinanceSupplierInvoiceSourceProvider>();
        services.AddSingleton<IFinanceCustomerReturnPersistence, UnavailableFinanceCustomerReturnPersistence>();
        services.AddSingleton<FinanceCustomerReturnService>();
        services.AddSingleton<FinanceAuthorizationService>();
        return services;
    }
}

#pragma warning restore CS1591
