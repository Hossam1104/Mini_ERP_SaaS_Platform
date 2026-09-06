using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>HOLD-138-T/U disposable LocalDB evidence at the real Sales persistence boundary.</summary>
[Collection(SqlServerSafetyCollection.Name)]
public sealed class CustomerReturnHold5SqlServerSafetyTests(SqlServerSafetyFixture fixture)
{
    [Fact]
    public async Task U1_to_U7_forward_schema_repair_is_registered_physical_and_queryable()
    {
        var connectionString = await ConnectionStringAsync();
        var options = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.SalesHistoryTable);
        await using var db = new SalesDbContext(options, fixture.TenantA);

        Assert.Contains("20260906095311_MESP138Hold5SalesSchemaIntegrity", await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        // This is deliberately a model query, rather than metadata inspection alone: it proves that
        // the current order model can address all repaired columns in the migrated SQL schema.
        _ = await db.Orders.Select(item => new { item.Id, item.CurrentApprovalsJson, item.RevisionNumber }).Take(1).ToListAsync();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COL_LENGTH(N'[sales].[SalesOrders]', N'CurrentApprovalsJson'), COL_LENGTH(N'[sales].[SalesOrders]', N'RevisionNumber'), COL_LENGTH(N'[sales].[SalesHistory]', N'SnapshotJson');";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.IsDBNull(0)); // nvarchar(max) is reported as -1 by COL_LENGTH.
        Assert.False(reader.IsDBNull(1));
        Assert.False(reader.IsDBNull(2));
    }

    [Fact]
    public async Task T1_T4_same_allocation_race_commits_at_most_one_active_sales_finance_effect()
    {
        var state = await RaceState.CreateAsync(fixture, 2m);
        var first = new CustomerReturnPersistence(state.Options);
        var second = new CustomerReturnPersistence(state.Options);
        var results = await Task.WhenAll(
            ObserveAsync(() => first.RegisterFinanceCreditNoteAsync(fixture.TenantA, state.Command(2m))),
            ObserveAsync(() => second.RegisterFinanceCreditNoteAsync(fixture.TenantA, state.Command(2m))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.All(results.Where(item => !item.Succeeded), item => Assert.True(item.SafeConflict));
        await state.AssertActiveConsumptionAsync(2m, 80m, 20m, 100m, 1, 1);
    }

    [Fact]
    public async Task T2_T4_residual_capacity_race_never_consumes_more_than_remaining_authority()
    {
        var state = await RaceState.CreateAsync(fixture, 2m);
        var initial = new CustomerReturnPersistence(state.Options);
        var consumed = await initial.RegisterFinanceCreditNoteAsync(fixture.TenantA, state.Command(1m));
        Assert.True(consumed.Succeeded, consumed.Code);

        var first = new CustomerReturnPersistence(state.Options);
        var second = new CustomerReturnPersistence(state.Options);
        var results = await Task.WhenAll(
            ObserveAsync(() => first.RegisterFinanceCreditNoteAsync(fixture.TenantA, state.Command(1m))),
            ObserveAsync(() => second.RegisterFinanceCreditNoteAsync(fixture.TenantA, state.Command(1m))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.All(results.Where(item => !item.Succeeded), item => Assert.True(item.SafeConflict));
        await state.AssertActiveConsumptionAsync(2m, 80m, 20m, 100m, 2, 2);
    }

    /// <summary>
    /// U8. Simulates a historical environment where the orphan migration's physical effect had
    /// already occurred for two of the three repaired columns before the HOLD-5 forward repair
    /// migration runs, proving the COL_LENGTH guard converges the schema instead of failing on an
    /// already-materialized column.
    /// </summary>
    [Fact]
    public async Task U8_forward_repair_converges_when_two_of_three_columns_already_preexist()
    {
        var baseConnectionString = await ConnectionStringAsync();
        var databaseName = "MiniErpFoundation_Hold5U8_" + Guid.NewGuid().ToString("N")[..8];
        var databaseBuilder = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        var connectionString = databaseBuilder.ConnectionString;
        var masterBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

        await using (var master = new SqlConnection(masterBuilder.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{databaseName}];";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.SalesHistoryTable);

            await using (var sales = new SalesDbContext(options, fixture.TenantA))
            {
                var migrator = sales.GetService<IMigrator>();
                await migrator.MigrateAsync("20260905204444_MESP138Hold3FinanceEffectAuthority");
            }

            // Two of the three orphan-intended columns already exist (as if created out of band);
            // SnapshotJson does not. The forward migration must add only the missing column.
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = @"
ALTER TABLE [sales].[SalesOrders] ADD [RevisionNumber] int NOT NULL DEFAULT 1;
ALTER TABLE [sales].[SalesOrders] ADD [CurrentApprovalsJson] nvarchar(max) NOT NULL DEFAULT N'[]';
";
                await command.ExecuteNonQueryAsync();
            }

            await using (var sales = new SalesDbContext(options, fixture.TenantA))
            {
                await sales.Database.MigrateAsync();
            }

            await using (var sales = new SalesDbContext(options, fixture.TenantA))
            {
                Assert.Contains("20260906095311_MESP138Hold5SalesSchemaIntegrity", await sales.Database.GetAppliedMigrationsAsync());
                Assert.Empty(await sales.Database.GetPendingMigrationsAsync());
                _ = await sales.Orders.Select(item => new { item.Id, item.CurrentApprovalsJson, item.RevisionNumber }).Take(1).ToListAsync();
            }

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COL_LENGTH(N'[sales].[SalesOrders]', N'CurrentApprovalsJson'), COL_LENGTH(N'[sales].[SalesOrders]', N'RevisionNumber'), COL_LENGTH(N'[sales].[SalesHistory]', N'SnapshotJson');";
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.False(reader.IsDBNull(0));
                Assert.False(reader.IsDBNull(1));
                Assert.False(reader.IsDBNull(2));
            }
        }
        finally
        {
            await using var drop = new SqlConnection(masterBuilder.ConnectionString);
            await drop.OpenAsync();
            await using var dropCommand = drop.CreateCommand();
            dropCommand.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END;";
            await dropCommand.ExecuteNonQueryAsync();
        }
    }

    private async Task<string> ConnectionStringAsync()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        return connection.ConnectionString;
    }

    private static async Task<(bool Succeeded, bool SafeConflict)> ObserveAsync(Func<Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>>> action)
    {
        try
        {
            var result = await action();
            return (result.Succeeded, !result.Succeeded && result.Code == "finance_effect_mismatch");
        }
        catch (Exception exception) when (IsSafeConcurrencyConflict(exception))
        {
            return (false, true);
        }
    }

    // SqlServerExecutionStrategy wraps a detected-transient DbUpdateException in an InvalidOperationException
    // ("...enabling transient error resiliency...") when EnableRetryOnFailure is not configured, so the real
    // SqlException (deadlock/serialization/unique-constraint) can sit two or three levels deep. Walk the chain
    // rather than assuming a fixed wrapping depth.
    private static bool IsSafeConcurrencyConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException) return true;
            if (current is SqlException sqlException && sqlException.Number is 1205 or 3960 or 2601 or 2627) return true;
        }
        return false;
    }

    private sealed class RaceState
    {
        private RaceState(string connectionString, DbContextOptions options, Guid tenantId, Guid companyId, Guid customerId, Guid returnId, Guid allocationId, Guid invoiceId, Guid openItemId)
        { ConnectionString = connectionString; Options = options; TenantId = tenantId; CompanyId = companyId; CustomerId = customerId; ReturnId = returnId; AllocationId = allocationId; InvoiceId = invoiceId; OpenItemId = openItemId; }
        internal string ConnectionString { get; }
        internal DbContextOptions Options { get; }
        internal Guid TenantId { get; }
        internal Guid CompanyId { get; }
        internal Guid CustomerId { get; }
        internal Guid ReturnId { get; }
        internal Guid AllocationId { get; }
        internal Guid InvoiceId { get; }
        internal Guid OpenItemId { get; }

        internal static async Task<RaceState> CreateAsync(SqlServerSafetyFixture fixture, decimal authority)
        {
            await using var connection = await fixture.OpenConnectionAsync();
            var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.SalesHistoryTable);
            var tenant = fixture.TenantA.TenantId;
            var returnId = Guid.NewGuid(); var deliveryId = Guid.NewGuid(); var orderId = Guid.NewGuid(); var lineId = Guid.NewGuid(); var companyId = Guid.NewGuid(); var customerId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid(); var openItemId = Guid.NewGuid(); var allocationId = Guid.NewGuid();
            var sourceLine = new SalesCustomerReturnSourceLineRecord(lineId, Guid.NewGuid(), "SQL-H5", "SQL HOLD 5", Guid.NewGuid(), "EA", authority, 0m, authority, 40m, 10m, 50m, null, authority, null, Guid.NewGuid(), Guid.NewGuid(), authority, authority, authority, authority, 0m, 0m, "Restockable", [], [], null);
            var allocation = new SalesCustomerReturnInvoiceAllocationRecord(allocationId, invoiceId, openItemId, deliveryId, lineId, 1, authority, authority, authority, 0m, authority, 80m, 20m, 100m, "SAR", Guid.NewGuid(), Guid.NewGuid(), 1, $"sql-h5-allocation-{returnId:N}", $"sql-h5-invoice-{invoiceId:N}");
            var source = new SalesCustomerReturnSourceRecord(returnId, deliveryId, orderId, 1, tenant.Value, companyId, null, customerId, Guid.NewGuid(), DateTimeOffset.UtcNow, invoiceId, openItemId, "SAR", [sourceLine], SalesCustomerReturnStatus.Completed, SalesCustomerReturnConsequence.CreditNote, [1], [allocation]);
            var request = new SalesCustomerReturnCreateRequest(deliveryId, new DateOnly(2026, 9, 6), SalesCustomerReturnConsequence.CreditNote, invoiceId, [new SalesCustomerReturnLineRequest(lineId, authority)], "sql-h5");
            var entity = new SalesCustomerReturnEntity(tenant, returnId, request, source, Guid.NewGuid(), DateTimeOffset.UtcNow);
            entity.Lines.Add(new SalesCustomerReturnLineEntity(tenant, Guid.NewGuid(), returnId, deliveryId, request.Lines[0], sourceLine));
            await using (var db = new SalesDbContext(options, fixture.TenantA))
            {
                db.CustomerReturns.Add(entity);
                db.CustomerReturnInvoiceAllocations.Add(new SalesCustomerReturnInvoiceAllocationEntity(tenant, allocationId, returnId, allocation));
                await db.SaveChangesAsync();
            }
            return new RaceState(connection.ConnectionString, options, tenant.Value, companyId, customerId, returnId, allocationId, invoiceId, openItemId);
        }

        internal SalesCustomerReturnFinanceEffectCommand Command(decimal quantity)
        {
            var net = quantity * 40m; var tax = quantity * 10m; var gross = quantity * 50m;
            var effect = new SalesCustomerReturnFinanceAllocationEffect(AllocationId, quantity, net, tax, gross, $"sql-h5-allocation-{ReturnId:N}");
            return new SalesCustomerReturnFinanceEffectCommand(ReturnId, TenantId, Guid.NewGuid(), InvoiceId, [AllocationId], DateTimeOffset.UtcNow, OpenItemId, Guid.NewGuid(), [Guid.NewGuid()], net, tax, gross, "SAR", "sql-h5-source", $"sql-h5-effect-{Guid.NewGuid():N}", $"sql-h5-request-{Guid.NewGuid():N}", "Committed", $"sql-h5-downstream-{Guid.NewGuid():N}", [effect], CompanyId, CustomerId);
        }

        internal async Task AssertActiveConsumptionAsync(decimal quantity, decimal net, decimal tax, decimal gross, int effectCount, int allocationCount)
        {
            var tenant = TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()));
            await using var db = new SalesDbContext(Options, tenant);
            var effects = await db.CustomerReturnFinanceEffects.Where(item => item.CustomerReturnId == ReturnId && item.State == "Active").Include(item => item.Allocations).ToListAsync();
            var allocations = effects.SelectMany(item => item.Allocations).ToArray();
            Assert.Equal(effectCount, effects.Count);
            Assert.Equal(allocationCount, allocations.Length);
            Assert.True(allocations.Sum(item => item.Quantity) <= quantity);
            Assert.True(allocations.Sum(item => item.NetAmount) <= net);
            Assert.True(allocations.Sum(item => item.TaxAmount) <= tax);
            Assert.True(allocations.Sum(item => item.GrossAmount) <= gross);
        }
    }
}
