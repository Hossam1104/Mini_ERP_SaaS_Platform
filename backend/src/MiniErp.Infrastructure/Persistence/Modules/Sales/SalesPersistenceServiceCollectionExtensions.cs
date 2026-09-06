#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.Sales;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

public static class SalesPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSalesPersistence(this IServiceCollection services, Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        var options = new DbContextOptionsBuilder();
        configureOptions(options);
        services.AddSingleton<ISalesPersistence>(new SalesPersistence(options.Options));
        var customerReturns = new CustomerReturnPersistence(options.Options);
        services.AddSingleton<ISalesCustomerReturnPersistence>(customerReturns);
        services.AddSingleton<ISalesCustomerReturnSourceProvider>(customerReturns);
        return services;
    }

    public static IServiceCollection AddSalesSqlServerPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddSalesPersistence(options => options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(SqlServerMigrationConfiguration.SalesHistoryTable, SqlServerMigrationConfiguration.HistorySchema)));
    }

    public static IServiceCollection AddSalesSqlitePersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddSalesPersistence(options => options.UseSqlite(connectionString));
    }

    public static void EnsureDevelopmentSqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DevelopmentSqliteDatabaseInitializer.EnsureCreated(connectionString, (options, tenantContext) => new SalesDbContext(options, tenantContext));
    }
}

#pragma warning restore CS1591
