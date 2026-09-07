#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.Procurement;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public static class ProcurementPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddProcurementPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var optionsBuilder = new DbContextOptionsBuilder();
        configureOptions(optionsBuilder);
        services.AddSingleton<IPurchaseRequestPersistence>(
            new PurchaseRequestPersistence(optionsBuilder.Options));
        services.AddSingleton<ISupplierQuotationPersistence>(
            new SupplierQuotationPersistence(optionsBuilder.Options));
        services.AddSingleton<IPurchaseOrderPersistence>(
            new PurchaseOrderPersistence(optionsBuilder.Options));
        services.AddSingleton<IGoodsReceiptPersistence>(
            new GoodsReceiptPersistence(optionsBuilder.Options));
        services.AddSingleton<ISupplierReturnPersistence>(
            new SupplierReturnPersistence(optionsBuilder.Options));
        services.AddSingleton<IPurchaseInvoiceHandoffPersistence>(
            new PurchaseInvoiceHandoffPersistence(optionsBuilder.Options));
        services.AddSingleton<IPurchaseInvoiceMatchPersistence>(
            new PurchaseInvoiceMatchPersistence(optionsBuilder.Options));
        services.AddSingleton<IPurchaseInvoiceMatchReportingReadPort>(provider =>
            (IPurchaseInvoiceMatchReportingReadPort)provider.GetRequiredService<IPurchaseInvoiceMatchPersistence>());
        return services;
    }

    public static IServiceCollection AddProcurementSqlServerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddProcurementPersistence(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(
                    SqlServerMigrationConfiguration.ProcurementHistoryTable,
                    SqlServerMigrationConfiguration.HistorySchema)));
    }

    public static IServiceCollection AddProcurementSqlitePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddProcurementPersistence(options => options.UseSqlite(connectionString));
    }

    public static void EnsureDevelopmentSqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DevelopmentSqliteDatabaseInitializer.EnsureCreated(
            connectionString,
            (options, tenantContext) => new ProcurementDbContext(options, tenantContext));
    }
}

#pragma warning restore CS1591
