using Microsoft.EntityFrameworkCore.Design;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence;

#pragma warning disable CS1591

internal static class SqlServerDesignTimeDbContextConfiguration
{
    internal static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("MESP_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "MESP_SQLSERVER_CONNECTION_STRING must be configured for EF Core SQL Server design-time operations.");
        }

        return connectionString;
    }

    internal static TenantContext CreateTenantContext() =>
        TenantContext.ForOrdinaryMembership(
            DevelopmentBootstrap.DevTenantId,
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("ef-design-time"),
            actorId: Guid.NewGuid());
}

/// <summary>EF Core design-time factory for the Foundation tenancy context.</summary>
public sealed class TenantPersistenceDbContextFactory
    : IDesignTimeDbContextFactory<TenantPersistenceDbContext>
{
    TenantPersistenceDbContext IDesignTimeDbContextFactory<TenantPersistenceDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.TenancyHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

/// <summary>EF Core design-time factory for the Master Data context.</summary>
public sealed class MasterDataDbContextFactory
    : IDesignTimeDbContextFactory<MasterDataDbContext>
{
    MasterDataDbContext IDesignTimeDbContextFactory<MasterDataDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.MasterDataHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

/// <summary>EF Core design-time factory for the Business Parties context.</summary>
public sealed class BusinessPartiesDbContextFactory
    : IDesignTimeDbContextFactory<BusinessPartiesDbContext>
{
    BusinessPartiesDbContext IDesignTimeDbContextFactory<BusinessPartiesDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.BusinessPartiesHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

/// <summary>EF Core design-time factory for the Procurement context.</summary>
public sealed class ProcurementDbContextFactory
    : IDesignTimeDbContextFactory<ProcurementDbContext>
{
    ProcurementDbContext IDesignTimeDbContextFactory<ProcurementDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.ProcurementHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

/// <summary>EF Core design-time factory for the Inventory context.</summary>
public sealed class InventoryDbContextFactory
    : IDesignTimeDbContextFactory<InventoryDbContext>
{
    InventoryDbContext IDesignTimeDbContextFactory<InventoryDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.InventoryHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

/// <summary>EF Core design-time factory for the Finance context.</summary>
public sealed class FinanceDbContextFactory
    : IDesignTimeDbContextFactory<FinanceDbContext>
{
    FinanceDbContext IDesignTimeDbContextFactory<FinanceDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.FinanceHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

/// <summary>EF Core design-time factory for the Sales context.</summary>
public sealed class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    SalesDbContext IDesignTimeDbContextFactory<SalesDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.SalesHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

/// <summary>EF Core design-time factory for the Migration foundation context.</summary>
public sealed class MigrationDbContextFactory : IDesignTimeDbContextFactory<MigrationDbContext>
{
    MigrationDbContext IDesignTimeDbContextFactory<MigrationDbContext>.CreateDbContext(string[] args) =>
        new(
            SqlServerMigrationConfiguration.Configure(
                SqlServerDesignTimeDbContextConfiguration.RequireConnectionString(),
                SqlServerMigrationConfiguration.MigrationHistoryTable),
            SqlServerDesignTimeDbContextConfiguration.CreateTenantContext());
}

#pragma warning restore CS1591
