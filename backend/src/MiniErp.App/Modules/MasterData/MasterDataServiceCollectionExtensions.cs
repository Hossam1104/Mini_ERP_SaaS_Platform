using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.BuildingBlocks.Owners;

namespace MiniErp.App.Modules.MasterData;

/// <summary>Composition helpers for the bounded Master Data authorization seam.</summary>
public static class MasterDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the bounded Master Data authorization policy composition.
    /// Capability resolution is derived from the trusted Foundation operation
    /// permission and fails closed for unknown permissions.
    /// </summary>
    public static IServiceCollection AddMasterDataAuthorization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMasterDataCapabilityResolver, FoundationPermissionMasterDataCapabilityResolver>();
        services.AddSingleton<IMasterDataCatalogPersistence, UnavailableMasterDataCatalogPersistence>();
        services.AddSingleton<IMasterDataCurrencyPaymentTermPersistence, UnavailableMasterDataCurrencyPaymentTermPersistence>();
        services.AddSingleton<IMasterDataExchangeRatePersistence, UnavailableMasterDataExchangeRatePersistence>();
        services.AddSingleton<IMasterDataPriceListPersistence, UnavailableMasterDataPriceListPersistence>();
        services.AddSingleton<IMasterDataTaxPersistence, UnavailableMasterDataTaxPersistence>();
        services.AddSingleton<IMasterDataImportPersistence, UnavailableMasterDataImportPersistence>();
        services.AddSingleton<IMasterDataScopePolicy, CategoryUomScopePolicy>();
        services.AddSingleton<IMasterDataResourcePolicy, CategoryUomResourcePolicy>();
        services.AddSingleton<IMasterDataApprovalPolicy, CategoryUomApprovalPolicy>();
        services.AddSingleton<MasterDataResourceAuthorizationService>();
        services.AddSingleton<MasterDataCategoryHierarchyPolicy>();
        services.AddSingleton<MasterDataCategoryUomService>();
        services.AddSingleton<CurrencyPaymentTermScopePolicy>();
        services.AddSingleton<CurrencyPaymentTermResourcePolicy>();
        services.AddSingleton<CurrencyPaymentTermApprovalPolicy>();
        services.AddSingleton<MasterDataCurrencyPaymentTermService>(servicesProvider =>
            new MasterDataCurrencyPaymentTermService(
                new MasterDataResourceAuthorizationService(
                    servicesProvider.GetRequiredService<IMasterDataCapabilityResolver>(),
                    servicesProvider.GetRequiredService<CurrencyPaymentTermResourcePolicy>(),
                    servicesProvider.GetRequiredService<CurrencyPaymentTermApprovalPolicy>(),
                    servicesProvider.GetRequiredService<CurrencyPaymentTermScopePolicy>()),
                servicesProvider.GetRequiredService<IMasterDataCurrencyPaymentTermPersistence>()));
        services.AddSingleton<TaxScopePolicy>();
        services.AddSingleton<TaxResourcePolicy>();
        services.AddSingleton<TaxApprovalPolicy>();
        services.AddSingleton<MasterDataTaxService>(servicesProvider =>
            new MasterDataTaxService(
                new MasterDataResourceAuthorizationService(
                    servicesProvider.GetRequiredService<IMasterDataCapabilityResolver>(),
                    servicesProvider.GetRequiredService<TaxResourcePolicy>(),
                    servicesProvider.GetRequiredService<TaxApprovalPolicy>(),
                    servicesProvider.GetRequiredService<TaxScopePolicy>()),
                servicesProvider.GetRequiredService<IMasterDataTaxPersistence>()));
        services.AddSingleton<ExchangeRateScopePolicy>();
        services.AddSingleton<ExchangeRateResourcePolicy>();
        services.AddSingleton<ExchangeRateApprovalPolicy>();
        services.AddSingleton<MasterDataExchangeRateService>(servicesProvider =>
            new MasterDataExchangeRateService(
                new MasterDataResourceAuthorizationService(
                    servicesProvider.GetRequiredService<IMasterDataCapabilityResolver>(),
                    servicesProvider.GetRequiredService<ExchangeRateResourcePolicy>(),
                    servicesProvider.GetRequiredService<ExchangeRateApprovalPolicy>(),
                    servicesProvider.GetRequiredService<ExchangeRateScopePolicy>()),
                servicesProvider.GetRequiredService<IMasterDataExchangeRatePersistence>()));
        services.AddSingleton<PriceListScopePolicy>();
        services.AddSingleton<PriceListResourcePolicy>();
        services.AddSingleton<PriceListApprovalPolicy>();
        services.AddSingleton<MasterDataPriceListService>(servicesProvider =>
            new MasterDataPriceListService(
                new MasterDataResourceAuthorizationService(
                    servicesProvider.GetRequiredService<IMasterDataCapabilityResolver>(),
                    servicesProvider.GetRequiredService<PriceListResourcePolicy>(),
                    servicesProvider.GetRequiredService<PriceListApprovalPolicy>(),
                    servicesProvider.GetRequiredService<PriceListScopePolicy>()),
                servicesProvider.GetRequiredService<IMasterDataPriceListPersistence>(),
                servicesProvider.GetService<IBusinessCustomerReferenceReader>()));
        return services;
    }

    /// <summary>
    /// Registers the Phase-A structured import orchestration and its ten
    /// resource adapters. The host may replace only the module-owned
    /// persistence ports; the processing and authorization rules remain
    /// application-owned.
    /// </summary>
    public static IServiceCollection AddMasterDataImport(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<MasterDataImportAuthorizationComposition>();
        services.AddSingleton<MasterDataImportProcessorRegistry>(serviceProvider =>
            new MasterDataImportProcessorRegistry(
                MasterDataImportProcessorFactory.Create(
                    serviceProvider.GetRequiredService<IMasterDataCatalogPersistence>(),
                    serviceProvider.GetRequiredService<IProductIdentityPersistence>(),
                    serviceProvider.GetRequiredService<IMasterDataCurrencyPaymentTermPersistence>(),
                    serviceProvider.GetRequiredService<IMasterDataTaxPersistence>(),
                    serviceProvider.GetRequiredService<IMasterDataExchangeRatePersistence>(),
                    serviceProvider.GetRequiredService<IMasterDataPriceListPersistence>(),
                    serviceProvider.GetRequiredService<ISupplierPersistence>(),
                    serviceProvider.GetRequiredService<ICustomerPersistence>())));
        services.AddSingleton<MasterDataImportService>();
        services.AddSingleton<IOwnerExecutionGateway, MasterDataOwnerExecutionGateway>();
        return services;
    }

    /// <summary>
    /// Registers Product's independently owned authorization and application
    /// seam. Persistence remains fail-closed until the host supplies the
    /// module-owned provider adapter.
    /// </summary>
    public static IServiceCollection AddProductIdentity(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ProductScopePolicy>();
        services.AddSingleton<ProductResourcePolicy>();
        services.AddSingleton<ProductApprovalPolicy>();
        services.AddSingleton<ProductResourceAuthorizationService>();
        services.AddSingleton<MasterDataTenantContextResolver>();
        services.AddSingleton<IProductIdentityPersistence, UnavailableProductIdentityPersistence>();
        services.AddSingleton<IProductBaseUomChangePolicy, ProductBaseUomChangePolicyUnavailable>();
        services.AddSingleton<ProductIdentityService>();
        return services;
    }

}
