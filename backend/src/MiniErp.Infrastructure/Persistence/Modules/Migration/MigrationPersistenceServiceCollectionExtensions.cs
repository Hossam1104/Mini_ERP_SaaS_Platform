#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

/// <summary>Composition helpers for the bounded Migration foundation.</summary>
public static class MigrationPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMigrationPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var optionsBuilder = new DbContextOptionsBuilder();
        configureOptions(optionsBuilder);
        services.AddSingleton<IMigrationFoundationPersistence>(
            new MigrationPersistence(optionsBuilder.Options));
        return services;
    }

    public static IServiceCollection AddMigrationSqlServerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddMigrationPersistence(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(
                    SqlServerMigrationConfiguration.MigrationHistoryTable,
                    SqlServerMigrationConfiguration.HistorySchema)));
    }

    public static IServiceCollection AddMigrationSqlitePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddMigrationPersistence(options => options.UseSqlite(connectionString));
    }

    public static void EnsureDevelopmentSqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DevelopmentSqliteDatabaseInitializer.EnsureCreated(
            connectionString,
            (options, tenantContext) => new MigrationDbContext(options, tenantContext));
    }
}

#pragma warning restore CS1591
