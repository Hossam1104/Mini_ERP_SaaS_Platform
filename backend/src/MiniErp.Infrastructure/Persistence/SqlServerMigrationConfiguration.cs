using Microsoft.EntityFrameworkCore;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Keeps SQL Server migration history unambiguous when several module-owned
/// contexts share one physical database and migration assembly.
/// </summary>
internal static class SqlServerMigrationConfiguration
{
    internal const string HistorySchema = "dbo";
    internal const string TenancyHistoryTable = "__EFMigrationsHistory_Tenancy";
    internal const string MasterDataHistoryTable = "__EFMigrationsHistory_MasterData";
    internal const string BusinessPartiesHistoryTable = "__EFMigrationsHistory_BusinessParties";
    internal const string ProcurementHistoryTable = "__EFMigrationsHistory_Procurement";
    internal const string InventoryHistoryTable = "__EFMigrationsHistory_Inventory";
    internal const string FinanceHistoryTable = "__EFMigrationsHistory_Finance";
    internal const string SalesHistoryTable = "__EFMigrationsHistory_Sales";
    internal const string MigrationHistoryTable = "__EFMigrationsHistory_Migration";

    internal static DbContextOptions Configure(
        string connectionString,
        string historyTable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(historyTable);

        var options = new DbContextOptionsBuilder();
        options.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable(historyTable, HistorySchema));
        return options.Options;
    }
}
