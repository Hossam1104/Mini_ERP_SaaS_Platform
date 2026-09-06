#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

public static class InventoryPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryPersistence(this IServiceCollection services, Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(configureOptions);
        var optionsBuilder = new DbContextOptionsBuilder(); configureOptions(optionsBuilder);
        services.AddSingleton<IInventoryPersistence>(new InventoryPersistence(optionsBuilder.Options));
        services.AddSingleton<IInventoryCustomerReturnPersistence>(serviceProvider =>
            new CustomerReturnInventoryPersistence(optionsBuilder.Options, serviceProvider.GetRequiredService<ISalesCustomerReturnSourceProvider>()));
        services.AddSingleton<IInventoryValuationPersistence>(serviceProvider =>
            new InventoryValuationPersistence(
                optionsBuilder.Options,
                serviceProvider.GetService<IGoodsReceiptPersistence>(),
                serviceProvider.GetService<IPurchaseOrderPersistence>(),
                serviceProvider.GetService<IMasterDataExchangeRatePersistence>()));
        return services;
    }

    public static IServiceCollection AddInventorySqlServerPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddInventoryPersistence(options => options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(SqlServerMigrationConfiguration.InventoryHistoryTable, SqlServerMigrationConfiguration.HistorySchema)));
    }

    public static IServiceCollection AddInventorySqlitePersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddInventoryPersistence(options => options.UseSqlite(connectionString));
    }

    public static void EnsureDevelopmentSqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DevelopmentSqliteDatabaseInitializer.EnsureCreated(connectionString, (options, tenantContext) => new InventoryDbContext(options, tenantContext));
    }
}

#pragma warning restore CS1591
