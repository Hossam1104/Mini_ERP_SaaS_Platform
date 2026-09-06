#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

public static class FinancePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddFinancePersistence(this IServiceCollection services, Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(configureOptions);
        var optionsBuilder = new DbContextOptionsBuilder(); configureOptions(optionsBuilder);
        services.AddSingleton<IFinancePersistence>(provider => new FinancePersistence(
            optionsBuilder.Options,
            provider.GetRequiredService<IFinanceCompanyProvider>(),
            provider.GetRequiredService<IInventoryValuationPersistence>(),
            provider.GetRequiredService<IMasterDataExchangeRatePersistence>(),
            provider.GetRequiredService<IFinanceSourceApprovalPolicy>()));
        services.AddSingleton<IFinanceCustomerReturnPersistence>(provider => new CustomerReturnFinancePersistence(
            optionsBuilder.Options,
            provider.GetRequiredService<ISalesCustomerReturnSourceProvider>(),
            provider.GetRequiredService<IFinanceCompanyProvider>(),
            provider.GetRequiredService<IMasterDataExchangeRatePersistence>()));
        services.AddSingleton<IFinanceSettlementPersistence>(provider => new FinanceSettlementPersistence(
            optionsBuilder.Options,
            provider.GetRequiredService<IFinanceCompanyProvider>(),
            provider.GetRequiredService<IMasterDataExchangeRatePersistence>(),
            provider.GetRequiredService<IBusinessCustomerReferenceReader>(),
            provider.GetRequiredService<ISupplierPersistence>(),
            provider.GetRequiredService<IMasterDataCurrencyPaymentTermPersistence>(),
            provider.GetRequiredService<IFinanceSupplierInvoiceSourceProvider>(),
            provider.GetRequiredService<IFinanceSourceApprovalPolicy>()));
        services.AddSingleton<IFinanceMesp134Persistence>(provider => new FinanceMesp134Persistence(
            optionsBuilder.Options,
            provider.GetRequiredService<IFinanceCompanyProvider>(),
            provider.GetRequiredService<IMasterDataCurrencyPaymentTermPersistence>(),
            provider.GetRequiredService<IMasterDataExchangeRatePersistence>(),
            provider.GetRequiredService<MasterDataTaxService>(),
            provider.GetRequiredService<IFinanceSupplierInvoiceSourceProvider>()));
        services.AddSingleton<IFinanceMesp135Persistence>(provider => new FinanceMesp135Persistence(
            optionsBuilder.Options,
            provider.GetRequiredService<IFinanceCompanyProvider>(),
            provider.GetRequiredService<IFinanceSettlementPersistence>(),
            provider.GetRequiredService<IFinanceMesp134Persistence>(),
            provider.GetRequiredService<IMasterDataExchangeRatePersistence>()));
        services.AddSingleton<IFinanceSupplierInvoiceSourceProvider>(provider => new ProcurementFinanceSupplierInvoiceSourceProvider(
            provider.GetRequiredService<IPurchaseInvoiceHandoffPersistence>(),
            provider.GetRequiredService<IPurchaseInvoiceMatchPersistence>(),
            provider.GetRequiredService<IFinanceCompanyProvider>(),
            provider.GetRequiredService<IPurchaseOrderPersistence>(),
            provider.GetRequiredService<IMasterDataCurrencyPaymentTermPersistence>(),
            provider.GetRequiredService<ISupplierPersistence>()));
        return services;
    }

    public static IServiceCollection AddFinanceSqlServerPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddFinancePersistence(options => options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(SqlServerMigrationConfiguration.FinanceHistoryTable, SqlServerMigrationConfiguration.HistorySchema)));
    }

    public static IServiceCollection AddFinanceSqlitePersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString); return services.AddFinancePersistence(options => options.UseSqlite(connectionString));
    }

    public static void EnsureDevelopmentSqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString); DevelopmentSqliteDatabaseInitializer.EnsureCreated(connectionString, (options, tenantContext) => new FinanceDbContext(options, tenantContext));
    }
}

#pragma warning restore CS1591
