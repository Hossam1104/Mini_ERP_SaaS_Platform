using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Applies the committed SQL Server migrations in deterministic ownership
/// order for the local Development runtime. Production startup never calls
/// this helper.
/// </summary>
public static class DevelopmentSqlServerDatabaseMigrator
{
    private const int DatabaseAlreadyExistsErrorNumber = 1801;
    private const int DatabaseCreationRaceRetryCount = 5;

    /// <summary>Applies all committed local SQL Server module migrations.</summary>
    public static void Migrate(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // The shared Foundation table is created first. Module contexts do
        // not include it in their models and therefore cannot compete for it.
        using (var tenancy = new TenantPersistenceDbContext(
                   SqlServerMigrationConfiguration.Configure(
                       connectionString,
                       SqlServerMigrationConfiguration.TenancyHistoryTable),
                   SqlServerDesignTimeDbContextConfiguration.CreateTenantContext()))
        {
            MigrateWithDatabaseCreationRaceRetry(tenancy);
        }

        using (var migration = new MigrationDbContext(
                   SqlServerMigrationConfiguration.Configure(
                       connectionString,
                       SqlServerMigrationConfiguration.MigrationHistoryTable),
                   SqlServerDesignTimeDbContextConfiguration.CreateTenantContext()))
        {
            MigrateWithDatabaseCreationRaceRetry(migration);
        }

        using (var masterData = new MasterDataDbContext(
                   SqlServerMigrationConfiguration.Configure(
                       connectionString,
                       SqlServerMigrationConfiguration.MasterDataHistoryTable),
                   SqlServerDesignTimeDbContextConfiguration.CreateTenantContext()))
        {
            MigrateWithDatabaseCreationRaceRetry(masterData);
        }

        using (var businessParties = new BusinessPartiesDbContext(
                   SqlServerMigrationConfiguration.Configure(
                       connectionString,
                       SqlServerMigrationConfiguration.BusinessPartiesHistoryTable),
                   SqlServerDesignTimeDbContextConfiguration.CreateTenantContext()))
        {
            MigrateWithDatabaseCreationRaceRetry(businessParties);
        }

        using (var procurement = new ProcurementDbContext(
                   SqlServerMigrationConfiguration.Configure(
                       connectionString,
                       SqlServerMigrationConfiguration.ProcurementHistoryTable),
                   SqlServerDesignTimeDbContextConfiguration.CreateTenantContext()))
        {
            MigrateWithDatabaseCreationRaceRetry(procurement);
        }

        using (var inventory = new InventoryDbContext(
                   SqlServerMigrationConfiguration.Configure(
                       connectionString,
                       SqlServerMigrationConfiguration.InventoryHistoryTable),
                   SqlServerDesignTimeDbContextConfiguration.CreateTenantContext()))
        {
            MigrateWithDatabaseCreationRaceRetry(inventory);
        }

        using (var finance = new FinanceDbContext(
                   SqlServerMigrationConfiguration.Configure(
                       connectionString,
                       SqlServerMigrationConfiguration.FinanceHistoryTable),
                   SqlServerDesignTimeDbContextConfiguration.CreateTenantContext()))
        {
            MigrateWithDatabaseCreationRaceRetry(finance);
        }
    }

    private static void MigrateWithDatabaseCreationRaceRetry(DbContext context)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                context.Database.Migrate();
                return;
            }
            catch (SqlException exception)
                when (attempt < DatabaseCreationRaceRetryCount
                      && IsDatabaseAlreadyExistsError(exception))
            {
                // Multiple Development TestServer hosts can start against the
                // same disposable SQL database concurrently. SQL Server lets
                // only one host create it; the losing host retries against the
                // database that now exists. EF's SQL Server migration lock then
                // serializes the pending migration work.
                Thread.Sleep(TimeSpan.FromMilliseconds(250 * (attempt + 1)));
            }
        }
    }

    private static bool IsDatabaseAlreadyExistsError(SqlException exception) =>
        exception.Errors
            .Cast<SqlError>()
            .Any(error => error.Number == DatabaseAlreadyExistsErrorNumber);
}
