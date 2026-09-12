using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Migration;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Migrations.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerSafetyCollection : ICollectionFixture<SqlServerSafetyFixture>
{
    public const string Name = "SQL Server safety harness";
}

/// <summary>
/// The exact configuration-safety check the SQL Server harness relies on to
/// refuse a missing, non-LocalDB or non-disposable connection string. Extracted
/// so a test can exercise the real rejection path directly instead of
/// re-asserting an already-accepted connection string against constants.
/// </summary>
internal static class SqlServerSafetyConfigurationValidator
{
    internal const string RequiredDataSource = @"(localdb)\MSSQLLocalDB";
    internal const string DisposableDatabasePattern = "^MiniErpFoundation_[A-Za-z0-9_]+$";

    public static SqlConnectionStringBuilder ValidateSafeConnectionString(string? rawConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException(
                "MESP_SQLSERVER_SAFETY_CONNECTION_STRING is required for the disposable SQL Server safety harness. "
                + "Set it to a LocalDB integrated-security connection targeting a MiniErpFoundation_* database. "
                + "Do not point it at the persistent MESP runtime database.");
        }

        var builder = new SqlConnectionStringBuilder(rawConnectionString);
        if (!string.Equals(builder.DataSource, RequiredDataSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The disposable SQL Server safety harness accepts only the machine-supported LocalDB instance "
                + $"'(localdb)\\MSSQLLocalDB'. The supplied MESP_SQLSERVER_SAFETY_CONNECTION_STRING targets a "
                + "different server and is rejected to protect any persistent database.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                builder.InitialCatalog,
                DisposableDatabasePattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "The SQL Server safety harness requires a disposable database whose name matches "
                + "the MiniErpFoundation_<AlphaNumeric> pattern. The supplied MESP_SQLSERVER_SAFETY_CONNECTION_STRING "
                + "targets a database outside that pattern and is rejected to protect any persistent database.");
        }

        return builder;
    }
}

/// <summary>
/// Disposable SQL Server LocalDB fixture. The safety checks intentionally fail
/// when an explicit, safe LocalDB connection is not supplied.
/// </summary>
public sealed class SqlServerSafetyFixture : IAsyncLifetime
{
    private string _connectionString = string.Empty;
    private string _inventoryConnectionString = string.Empty;
    private DbContextOptions _options = null!;

    public TenantContext TenantA { get; private set; } = null!;

    public TenantContext TenantB { get; private set; } = null!;

    public TenantPersistenceSessionFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Reads only the dedicated safety variable. The persistent runtime variable
        // MESP_SQLSERVER_CONNECTION_STRING must never be used here: the harness
        // creates and drops its database, which would be destructive against MESP.
        var rawConnectionString = Environment.GetEnvironmentVariable(
            "MESP_SQLSERVER_SAFETY_CONNECTION_STRING");

        var builder = SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(rawConnectionString);

        builder.IntegratedSecurity = true;
        builder.TrustServerCertificate = true;
        builder.ConnectTimeout = 15;
        _connectionString = builder.ConnectionString;

        await EnsureDatabaseExistsAsync(builder);

        TenantA = TenantContext.ForOrdinaryMembership(
            NewTenantId(),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-a"));
        TenantB = TenantContext.ForSupportGrant(
            NewTenantId(),
            new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
            correlationId: new CorrelationId("sql-b"));

        _inventoryConnectionString = _connectionString;
        _options = SqlServerMigrationConfiguration.Configure(
            _connectionString,
            SqlServerMigrationConfiguration.TenancyHistoryTable);

        // This is intentionally the same disposable catalog for every module.
        // It proves the committed Development migration order and shared-table
        // ownership instead of validating isolated module databases.
        await using (var tenancy = new TenantPersistenceDbContext(_options, TenantA))
        {
            await tenancy.Database.MigrateAsync();
        }

        await using (var masterData = new MasterDataDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable),
                         TenantA))
        {
            await masterData.Database.MigrateAsync();
        }

        await using (var businessParties = new BusinessPartiesDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.BusinessPartiesHistoryTable),
                         TenantA))
        {
            await businessParties.Database.MigrateAsync();
        }

        await using (var procurement = new ProcurementDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.ProcurementHistoryTable),
                         TenantA))
        {
            await procurement.Database.MigrateAsync();
        }

        await using (var inventory = new InventoryDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.InventoryHistoryTable),
                         TenantA))
        {
            await inventory.Database.MigrateAsync();
        }

        await using (var finance = new FinanceDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable),
                         TenantA))
        {
            await finance.Database.MigrateAsync();
        }

        await using (var sales = new SalesDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.SalesHistoryTable),
                         TenantA))
        {
            await sales.Database.MigrateAsync();
        }

        await using (var migration = new MigrationDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.MigrationHistoryTable),
                         TenantA))
        {
            await migration.Database.MigrateAsync();
        }

        await CreateProbeTablesAsync();
        Factory = new TenantPersistenceSessionFactory(_options);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        await DropDatabaseAsync(_connectionString);
        _inventoryConnectionString = string.Empty;
    }

    internal TenantPersistenceDbContext CreateDbContext(TenantContext tenantContext)
    {
        return new TenantPersistenceDbContext(_options, tenantContext);
    }

    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<SqlConnection> OpenInventoryConnectionAsync()
    {
        var connection = new SqlConnection(_inventoryConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task AddAsync(TenantContext tenantContext, TenantOwnedRecord record)
    {
        await using var session = Factory.Create(tenantContext);
        session.Records.Add(record);
        await session.SaveChangesAsync();
    }

    private static TenantId NewTenantId() => new(Guid.NewGuid());

    private static async Task EnsureDatabaseExistsAsync(SqlConnectionStringBuilder databaseBuilder)
    {
        var databaseName = databaseBuilder.InitialCatalog;
        var masterBuilder = new SqlConnectionStringBuilder(databaseBuilder.ConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{EscapeLiteral(databaseName)}') IS NULL "
            + $"CREATE DATABASE [{EscapeIdentifier(databaseName)}];";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{EscapeLiteral(databaseName)}') IS NOT NULL "
            + $"BEGIN ALTER DATABASE [{EscapeIdentifier(databaseName)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{EscapeIdentifier(databaseName)}]; END;";
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateProbeTablesAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF SCHEMA_ID(N'test') IS NULL EXEC(N'CREATE SCHEMA [test]');
            IF OBJECT_ID(N'[test].[DurableWorkProbe]', N'U') IS NULL
            BEGIN
                CREATE TABLE [test].[DurableWorkProbe]
                (
                    [WorkId] uniqueidentifier NOT NULL CONSTRAINT [PK_DurableWorkProbe] PRIMARY KEY,
                    [TenantId] uniqueidentifier NOT NULL,
                    [EventId] uniqueidentifier NOT NULL,
                    [LeaseOwner] nvarchar(100) NULL,
                    [LeaseVersion] int NOT NULL CONSTRAINT [DF_DurableWorkProbe_LeaseVersion] DEFAULT (0),
                    [State] nvarchar(30) NOT NULL,
                    [Kind] nvarchar(30) NOT NULL,
                    [Payload] nvarchar(200) NOT NULL
                );
                CREATE UNIQUE INDEX [UX_DurableWorkProbe_Tenant_Event]
                    ON [test].[DurableWorkProbe] ([TenantId], [EventId]);
            END;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string EscapeIdentifier(string value) => value.Replace("]", "]]", StringComparison.Ordinal);
}

[Collection(SqlServerSafetyCollection.Name)]
public sealed class SqlServerSafetyTests
{
    private readonly SqlServerSafetyFixture _fixture;
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ApproverId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public SqlServerSafetyTests(SqlServerSafetyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task All_module_sql_server_migrations_apply_in_development_order_to_one_disposable_database()
    {
        var connectionString = await GetConnectionStringAsync();
        var tenancyOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.TenancyHistoryTable);
        var masterDataOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable);
        var businessPartiesOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.BusinessPartiesHistoryTable);
        var procurementOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.ProcurementHistoryTable);
        var inventoryOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.InventoryHistoryTable);
        var financeOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var salesOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.SalesHistoryTable);
        var migrationOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);

        await using (var tenancy = new TenantPersistenceDbContext(tenancyOptions, _fixture.TenantA))
        {
            Assert.Equal(["20260815225855_InitialTenancy"], (await tenancy.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await tenancy.Database.GetPendingMigrationsAsync());
        }

        await using (var masterData = new MasterDataDbContext(masterDataOptions, _fixture.TenantA))
        {
            Assert.Equal(["20260815225908_InitialMasterData", "20260816073832_SharedTenantRuntimeModel"], (await masterData.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await masterData.Database.GetPendingMigrationsAsync());
        }

        await using (var businessParties = new BusinessPartiesDbContext(businessPartiesOptions, _fixture.TenantA))
        {
            Assert.Equal(["20260815225921_InitialBusinessParties", "20260816073853_SharedTenantRuntimeModel"], (await businessParties.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await businessParties.Database.GetPendingMigrationsAsync());
        }

        await using (var procurement = new ProcurementDbContext(procurementOptions, _fixture.TenantA))
        {
            Assert.Equal(
                [
                    "20260815225933_InitialProcurement",
                    "20260816073856_SharedTenantRuntimeModel",
                    "20260817143432_PurchaseOrderAndSupplierConfirmation",
                    "20260817211222_AddPurchaseOrderAuditRequestFingerprint",
                    "20260818103736_PurchaseOrderCommercialIntegrityAndDurableReplay",
                    "20260819102200_GoodsReceiptAndPurchaseInvoiceHandoff",
                    "20260820094805_ThreeWayMatchingAndDeclaredInvoiceEvidence",
                    "20260820102459_MESP126ResolutionPolicyEvidence",
                    "20260821031935_MESP127SupplierReturnEvidence"
                ],
                (await procurement.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await procurement.Database.GetPendingMigrationsAsync());
        }

        await using (var inventory = new InventoryDbContext(inventoryOptions, _fixture.TenantA))
        {
            Assert.Equal(
                [
                    "20260821113311_MESP128InventoryLedgerFoundation",
                    "20260821132738_MESP128StockIntegrityRemediation",
                    "20260821213832_MESP128OpusStockIntegrityRemediation",
                    "20260822092802_MESP129PhysicalStockMovements",
                    "20260822194250_MESP130StockControlAndCorrections",
                    "20260822220126_MESP130SolAcceptanceRemediation",
                    "20260822220521_MESP130SolAcceptanceCountApproval",
                    "20260823104702_MESP130InventoryCountLedgerFence",
                    "20260823124304_MESP131MovingWeightedAverageValuation",
                    "20260823180537_MESP131SolFinancialIntegrityRemediation",
                    "20260823225921_MESP131SolFinalValuationIntegrity",
                    "20260829150156_MESP137ReservationFulfillment",
                    "20260830222514_MESP138CustomerReturnFoundation",
                    "20260831102842_MESP138Hold1Remediation",
                    "20260831104654_MESP138Hold1InventoryReversal"
                ],
                (await inventory.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await inventory.Database.GetPendingMigrationsAsync());
        }

        await using (var finance = new FinanceDbContext(financeOptions, _fixture.TenantA))
        {
            Assert.Equal(
                [
                    "20260824125115_MESP132FinanceFoundation",
                    "20260824152331_MESP132SolFinanceCorrectnessRemediation",
                    "20260824220208_MESP133ApArCashSettlement",
                    "20260825225409_MESP134TaxFxReportingRevaluation",
                    "20260825232242_MESP134EvidenceSnapshots",
                    "20260826001524_MESP134Hold1CorrectiveEvidence",
                    "20260826133441_MESP135FinanceCloseReports",
                    "20260830222541_MESP138CustomerReturnFoundation",
                    "20260830223637_MESP138CreditNotePostingEffect",
                    "20260831102106_MESP138Hold1SourceEvidence",
                    "20260831151947_MESP138Hold2Durability"
                ],
                (await finance.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await finance.Database.GetPendingMigrationsAsync());
        }

        await using (var sales = new SalesDbContext(salesOptions, _fixture.TenantA))
        {
            Assert.Equal(
                [
                    "20260828113439_MESP136SalesCommercial",
                    "20260828125545_MESP136SalesCommercialEvidence",
                    "20260828224412_MESP136SalesHold3CurrencyIntegrity",
                    "20260829150203_MESP137DeliveryInvoice",
                    "20260829181835_MESP137Hold1Remediation",
                    "20260830222453_MESP138CustomerReturnFoundation",
                    "20260831102044_MESP138Hold1Remediation",
                    "20260831151959_MESP138Hold2FinanceReversalAck",
                    "20260905204444_MESP138Hold3FinanceEffectAuthority",
                    "20260906095311_MESP138Hold5SalesSchemaIntegrity"
                ],
                (await sales.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await sales.Database.GetPendingMigrationsAsync());
        }

        await using (var migration = new MigrationDbContext(migrationOptions, _fixture.TenantA))
        {
            // The attempt-lineage constraints are additive migrations rather
            // than edits of the already-pushed foundation migrations.
            // Asserting all four names keeps that ordering committed.
            Assert.Equal(
                [
                    "20260911183345_MESP141MigrationFoundation",
                    "20260912105244_MESP141MigrationAttemptLineage",
                    "20260912191429_MESP141MigrationRunQualifiedAttemptLineage",
                    "20260912224151_MESP141MigrationIntakeStaging"
                ],
                (await migration.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await migration.Database.GetPendingMigrationsAsync());
        }

        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'inventory'
              AND TABLE_NAME IN (N'OpeningBalances', N'OpeningBalanceRows', N'OpeningBalanceHistory', N'StockLedgerMovements', N'Transfers', N'TransferLines', N'TransferEvents', N'Reservations', N'ReservationHistory', N'AuditEvents', N'IdempotencyEntries', N'ConcurrencyAnchors', N'ReasonCodes', N'Adjustments', N'AdjustmentLines', N'Counts', N'CountSnapshots', N'CountLines', N'StockIssues', N'StockIssueLines', N'ControlHistory', N'CompanyLedgerSequenceAnchors', N'ValuationPolicies', N'ValuationScopeAnchors', N'ValuationStates', N'MovementValuationEvents', N'ValuationRuns', N'FinanceValuationHandoffs');
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'tenancy'
              AND TABLE_NAME = N'TenantOwnedRecords';
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'tenancy'
              AND tables.name = N'TenantOwnedRecords'
              AND indexes.name = N'IX_TenantOwnedRecords_TenantId_BusinessKey';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(28, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public async Task MESP138Hold4_sql_server_customer_return_finance_effect_tables_have_authoritative_constraints()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'sales'
              AND TABLE_NAME IN (N'SalesCustomerReturnFinanceEffects', N'SalesCustomerReturnFinanceEffectAllocations');
            SELECT COUNT(*)
            FROM sys.key_constraints AS constraints
            INNER JOIN sys.tables AS tables ON tables.object_id = constraints.parent_object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'sales' AND tables.name = N'SalesCustomerReturnFinanceEffects'
              AND constraints.name IN (N'PK_SalesCustomerReturnFinanceEffects', N'AK_SalesCustomerReturnFinanceEffects_TenantId_Id');
            SELECT COUNT(*)
            FROM sys.foreign_keys
            WHERE name = N'FK_SalesCustomerReturnFinanceEffects_SalesCustomerReturns_TenantId_CustomerReturnId';
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'sales' AND tables.name = N'SalesCustomerReturnFinanceEffects'
              AND indexes.name IN (N'IX_SalesCustomerReturnFinanceEffects_TenantId_CustomerReturnId_CreditNoteId', N'IX_SalesCustomerReturnFinanceEffects_TenantId_Id')
              AND indexes.is_unique = 1;
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'sales' AND tables.name = N'SalesCustomerReturnFinanceEffects'
              AND indexes.name = N'IX_SalesCustomerReturnFinanceEffects_TenantId_CustomerReturnId_InvoiceId_State'
              AND indexes.is_unique = 0;
            SELECT COUNT(*)
            FROM sys.key_constraints AS constraints
            INNER JOIN sys.tables AS tables ON tables.object_id = constraints.parent_object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'sales' AND tables.name = N'SalesCustomerReturnFinanceEffectAllocations'
              AND constraints.name = N'PK_SalesCustomerReturnFinanceEffectAllocations';
            SELECT COUNT(*)
            FROM sys.foreign_keys
            WHERE name = N'FK_SalesCustomerReturnFinanceEffectAllocations_SalesCustomerReturnFinanceEffects_TenantId_FinanceEffectId';
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'sales' AND tables.name = N'SalesCustomerReturnFinanceEffectAllocations'
              AND indexes.name IN (N'IX_SalesCustomerReturnFinanceEffectAllocations_TenantId_FinanceEffectId_SourceAllocationId', N'IX_SalesCustomerReturnFinanceEffectAllocations_TenantId_Id')
              AND indexes.is_unique = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
    }

    [Fact]
    public async Task MESP132_sql_server_creates_only_module_owned_tenant_finance_tables()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'finance'
              AND TABLE_NAME IN (N'Accounts', N'FiscalCalendars', N'FiscalYears', N'FiscalPeriods', N'CostCenters', N'PostingRules', N'Journals', N'JournalLines', N'AuditEvents', N'IdempotencyEntries', N'SourceEffects', N'PaymentMethods', N'CashAccounts', N'OpenItems', N'SettlementDocuments', N'Allocations');
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'finance'
              AND tables.name = N'SourceEffects'
              AND indexes.name = N'IX_SourceEffects_TenantId_CompanyId_SourceContract_SourceEvidenceId_SourceEvidenceVersion';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(16, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public async Task MESP133_sql_server_payment_method_same_code_race_is_unique_or_safe_conflict()
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var first = CreateSettlementPersistence(options, companyId);
        var second = CreateSettlementPersistence(options, companyId);
        var firstContext = FinanceContext("tenant.finance.settlement.configure");
        var secondContext = FinanceContext("tenant.finance.settlement.configure");
        var firstCommand = PaymentMethodCommand(companyId, "BANK-RACE", Guid.NewGuid(), "bank-race-a");
        var secondCommand = firstCommand with { Id = Guid.NewGuid(), IdempotencyKey = "bank-race-b", RequestFingerprint = "bank-race-b" };

        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => first.CreatePaymentMethodAsync(firstContext, firstCommand)),
            SafeFinanceOperationAsync(() => second.CreatePaymentMethodAsync(secondContext, secondCommand)));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "payment_method_duplicate" or "concurrency_conflict");
        Assert.Single(await first.ListPaymentMethodsAsync(firstContext, companyId));
    }

    [Fact]
    public async Task MESP133_sql_server_payment_method_lifecycle_race_has_one_committed_transition()
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var creator = CreateSettlementPersistence(options, companyId);
        var created = await creator.CreatePaymentMethodAsync(FinanceContext("tenant.finance.settlement.configure"), PaymentMethodCommand(companyId, "LIFECYCLE-RACE", Guid.NewGuid(), "lifecycle-create"));
        Assert.True(created.Succeeded, created.Code);
        var first = CreateSettlementPersistence(options, companyId);
        var second = CreateSettlementPersistence(options, companyId);
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => first.SetPaymentMethodLifecycleAsync(FinanceContext("tenant.finance.settlement.configure"), created.Value!.Id, companyId, FinancePaymentMethodLifecycle.Inactive, created.Value!.Version, "lifecycle-a", "lifecycle-a")),
            SafeFinanceOperationAsync(() => second.SetPaymentMethodLifecycleAsync(FinanceContext("tenant.finance.settlement.configure"), created.Value!.Id, companyId, FinancePaymentMethodLifecycle.Inactive, created.Value!.Version, "lifecycle-b", "lifecycle-b")));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code == "concurrency_conflict");
        Assert.Equal(FinancePaymentMethodLifecycle.Inactive, (await first.ListPaymentMethodsAsync(FinanceContext("tenant.finance.settlement.configure"), companyId)).Single().Lifecycle);
    }

    [Fact]
    public async Task MESP133_sql_server_cash_account_same_code_race_is_unique_or_safe_conflict()
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var account = await CreateSettlementLinkedAccountAsync(options, companyId, "cash-code-race");
        var first = CreateSettlementPersistence(options, companyId);
        var second = CreateSettlementPersistence(options, companyId);
        var firstCommand = CashAccountCommand(companyId, account.Id, "CASH-RACE", Guid.NewGuid(), "cash-race-a");
        var secondCommand = firstCommand with { Id = Guid.NewGuid(), IdempotencyKey = "cash-race-b", RequestFingerprint = "cash-race-b" };

        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => first.CreateCashAccountAsync(FinanceContext("tenant.finance.settlement.configure"), firstCommand)),
            SafeFinanceOperationAsync(() => second.CreateCashAccountAsync(FinanceContext("tenant.finance.settlement.configure"), secondCommand)));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "cash_account_duplicate" or "concurrency_conflict");
        Assert.Single(await first.ListCashAccountsAsync(FinanceContext("tenant.finance.settlement.configure"), companyId));
    }

    [Fact]
    public async Task MESP133_sql_server_cash_account_lifecycle_race_has_one_committed_transition()
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var account = await CreateSettlementLinkedAccountAsync(options, companyId, "cash-lifecycle-race");
        var creator = CreateSettlementPersistence(options, companyId);
        var created = await creator.CreateCashAccountAsync(FinanceContext("tenant.finance.settlement.configure"), CashAccountCommand(companyId, account.Id, "CASH-LIFECYCLE-RACE", Guid.NewGuid(), "cash-lifecycle-create"));
        Assert.True(created.Succeeded, created.Code);
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => CreateSettlementPersistence(options, companyId).SetCashAccountLifecycleAsync(FinanceContext("tenant.finance.settlement.configure"), created.Value!.Id, companyId, FinancePaymentMethodLifecycle.Inactive, created.Value!.Version, "cash-lifecycle-a", "cash-lifecycle-a")),
            SafeFinanceOperationAsync(() => CreateSettlementPersistence(options, companyId).SetCashAccountLifecycleAsync(FinanceContext("tenant.finance.settlement.configure"), created.Value!.Id, companyId, FinancePaymentMethodLifecycle.Inactive, created.Value!.Version, "cash-lifecycle-b", "cash-lifecycle-b")));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code == "concurrency_conflict");
        Assert.Equal(FinancePaymentMethodLifecycle.Inactive, (await creator.ListCashAccountsAsync(FinanceContext("tenant.finance.settlement.configure"), companyId)).Single().Lifecycle);
    }

    [Fact]
    public async Task MESP133_sql_server_payment_method_edit_same_version_race_has_one_authoritative_edit()
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var creator = CreateSettlementPersistence(options, companyId);
        var created = await creator.CreatePaymentMethodAsync(FinanceContext("tenant.finance.settlement.configure"), PaymentMethodCommand(companyId, "EDIT-RACE", Guid.NewGuid(), "edit-create"));
        Assert.True(created.Succeeded, created.Code);
        var firstCommand = PaymentMethodCommand(companyId, "EDIT-RACE-A", created.Value!.Id, "edit-a") with { ExpectedVersion = created.Value.Version };
        var secondCommand = PaymentMethodCommand(companyId, "EDIT-RACE-B", created.Value.Id, "edit-b") with { ExpectedVersion = created.Value.Version };
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => CreateSettlementPersistence(options, companyId).EditPaymentMethodAsync(FinanceContext("tenant.finance.settlement.configure"), firstCommand)),
            SafeFinanceOperationAsync(() => CreateSettlementPersistence(options, companyId).EditPaymentMethodAsync(FinanceContext("tenant.finance.settlement.configure"), secondCommand)));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code == "concurrency_conflict");
        var code = (await creator.ListPaymentMethodsAsync(FinanceContext("tenant.finance.settlement.configure"), companyId)).Single().Code;
        Assert.True(code is "EDIT-RACE-A" or "EDIT-RACE-B");
    }

    [Fact]
    public async Task MESP133_sql_server_same_ap_source_concurrent_recognition_has_one_source_effect()
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL AP Company", "SAR")]);
        var first = CreateFinanceSettlementPersistence(options, companyId, provider, new SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement.NotRequired), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value));
        var second = CreateFinanceSettlementPersistence(options, companyId, provider, new SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement.NotRequired), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value));
        var context = FinanceContext("tenant.finance.ap.recognize");
        var accounts = await CreateFinanceAccountsAsync(new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence()), context, companyId, "sql-ap-recognition");
        await CreateOpenPeriodAndRuleAsync(options, provider, context, companyId, accounts.Debit.Id, accounts.Credit.Id, "procurement-supplier-invoice.v1", "recognition");

        var supplierId = Guid.NewGuid();
        var source = new FinanceSupplierInvoiceSourceRecord(
            _fixture.TenantA.TenantId.Value,
            companyId,
            supplierId,
            "procurement-supplier-invoice.v1",
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            "AP-RACE",
            new DateOnly(2026, 1, 15),
            "SAR",
            100m,
            "SAR",
            100m,
            1m,
            null,
            null,
            null,
            new FinancePaymentTermSnapshotRecord(Guid.NewGuid(), "NET30", "Net 30", null, 1, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 14)),
            new DateOnly(2026, 2, 14),
            Guid.NewGuid(),
            1,
            "sql-ap-race",
            "sql-ap-race");
        var sourceProvider = new SqlStaticSupplierInvoiceSourceProvider(source);
        first = CreateFinanceSettlementPersistence(options, companyId, provider, new SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement.NotRequired), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, supplierId), sourceProvider);
        second = CreateFinanceSettlementPersistence(options, companyId, provider, new SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement.NotRequired), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, supplierId), sourceProvider);

        var firstTask = SafeFinanceOperationAsync(() => first.RecognizeSupplierInvoiceAsync(FinanceContext("tenant.finance.ap.recognize"), new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "sql-ap-recognize-a", "sql-ap-recognize-a")));
        var secondTask = SafeFinanceOperationAsync(() => second.RecognizeSupplierInvoiceAsync(FinanceContext("tenant.finance.ap.recognize"), new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "sql-ap-recognize-b", "sql-ap-recognize-b")));
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "source_effect_exists" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(options, _fixture.TenantA);
        Assert.Equal(1, await db.OpenItems.CountAsync(item => item.SourceEvidenceId == source.SourceEvidenceId));
        Assert.Equal(1, await db.SourceEffects.CountAsync(item => item.SourceEvidenceId == source.SourceEvidenceId));
        Assert.Equal(1, await db.Journals.CountAsync(item => item.SourceEvidenceId == source.SourceEvidenceId));
    }

    [Fact]
    public async Task MESP133_sql_server_same_payable_open_item_concurrent_allocation_cannot_over_allocate()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("allocation-item-race");
        var seeded = await SeedPayableSettlementAsync(scenario, secondItem: false);
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.FirstItemId, 40m, new DateOnly(2026, 1, 15), "allocation A", Guid.NewGuid(), "allocation-item-a", "allocation-item-a"))),
            SafeCodeAsync(() => second.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.FirstItemId, 40m, new DateOnly(2026, 1, 15), "allocation B", Guid.NewGuid(), "allocation-item-b", "allocation-item-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "allocation_exceeds_outstanding" or "source_effect_exists" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(40m, await db.Allocations.Where(item => item.OpenItemId == seeded.FirstItemId && item.Status == FinanceAllocationStatus.Active).SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task MESP133_sql_server_same_settlement_concurrent_allocations_cannot_over_allocate_document()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("allocation-document-race");
        var seeded = await SeedPayableSettlementAsync(scenario, secondItem: true);
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.FirstItemId, 40m, new DateOnly(2026, 1, 15), "allocation A", Guid.NewGuid(), "allocation-document-a", "allocation-document-a"))),
            SafeCodeAsync(() => second.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.SecondItemId!.Value, 40m, new DateOnly(2026, 1, 15), "allocation B", Guid.NewGuid(), "allocation-document-b", "allocation-document-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "allocation_exceeds_unallocated" or "source_effect_exists" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(40m, await db.Allocations.Where(item => item.SettlementDocumentId == seeded.DocumentId && item.Status == FinanceAllocationStatus.Active).SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task MESP133_sql_server_settlement_post_and_payment_method_lifecycle_have_one_consistent_order()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("post-lifecycle-race");
        var postPersistence = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var lifecyclePersistence = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => postPersistence.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(scenario.Approved.Id, scenario.Approved.Version, null, "post-lifecycle-post", "post-lifecycle-post", FinancePaymentMethodDirection.Payment))),
            SafeCodeAsync(() => lifecyclePersistence.SetPaymentMethodLifecycleAsync(FinanceContext("tenant.finance.settlement.configure"), scenario.Method.Id, scenario.CompanyId, FinancePaymentMethodLifecycle.Inactive, scenario.Method.Version, "post-lifecycle-method", "post-lifecycle-method")));

        Assert.Contains(results, item => item.Succeeded);
        Assert.All(results, item => Assert.True(item.Succeeded || item.Code is "settlement_configuration_invalid" or "cash_account_link_invalid" or "payment_method_in_use" or "concurrency_conflict" or "finance_conflict", item.Code));
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var document = await db.SettlementDocuments.SingleAsync(item => item.Id == scenario.Approved.Id);
        var method = await db.PaymentMethods.SingleAsync(item => item.Id == scenario.Method.Id);
        Assert.True(document.Status == FinanceSettlementDocumentStatus.Posted && method.Lifecycle == FinancePaymentMethodLifecycle.Active || document.Status == FinanceSettlementDocumentStatus.Approved && method.Lifecycle == FinancePaymentMethodLifecycle.Inactive);
    }

    [Fact]
    public async Task MESP133_sql_server_same_settlement_submit_version_race_has_one_transition()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("submit-race");
        var draft = await SeedDraftSettlementAsync(scenario);
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.TransitionSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.submit"), new FinanceSettlementActionCommand(draft.Id, draft.Version, null, "submit-a", "submit-a", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Submitted)),
            SafeCodeAsync(() => second.TransitionSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.submit"), new FinanceSettlementActionCommand(draft.Id, draft.Version, null, "submit-b", "submit-b", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Submitted)));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(FinanceSettlementDocumentStatus.Submitted, (await db.SettlementDocuments.SingleAsync(item => item.Id == draft.Id)).Status);
    }

    [Fact]
    public async Task MESP133_sql_server_same_payment_concurrent_post_has_one_authoritative_journal()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("payment-post-race");
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(scenario.Approved.Id, scenario.Approved.Version, null, "payment-post-a", "payment-post-a", FinancePaymentMethodDirection.Payment))),
            SafeCodeAsync(() => second.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(scenario.Approved.Id, scenario.Approved.Version, null, "payment-post-b", "payment-post-b", FinancePaymentMethodDirection.Payment))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "source_effect_exists" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var document = await db.SettlementDocuments.SingleAsync(item => item.Id == scenario.Approved.Id);
        Assert.Equal(FinanceSettlementDocumentStatus.Posted, document.Status);
        Assert.NotNull(document.PostedJournalId);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.SourceContract == "supplier-payment.v1" && item.SourceEvidenceId == document.Id));
    }

    [Fact]
    public async Task MESP133_sql_server_same_receipt_concurrent_post_has_one_authoritative_journal()
    {
        var scenario = await CreateReceiptSettlementScenarioAsync("receipt-post-race");
        var first = new FinanceSettlementPersistence(scenario.Options, scenario.Provider, new UnavailableMasterDataExchangeRatePersistence(), new UnavailableCustomerPersistence(), new UnavailableSupplierPersistence(), new UnavailableMasterDataCurrencyPaymentTermPersistence(), new UnavailableFinanceSupplierInvoiceSourceProvider(), new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required));
        var second = new FinanceSettlementPersistence(scenario.Options, scenario.Provider, new UnavailableMasterDataExchangeRatePersistence(), new UnavailableCustomerPersistence(), new UnavailableSupplierPersistence(), new UnavailableMasterDataCurrencyPaymentTermPersistence(), new UnavailableFinanceSupplierInvoiceSourceProvider(), new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(scenario.Approved.Id, scenario.Approved.Version, null, "receipt-post-a", "receipt-post-a", FinancePaymentMethodDirection.Receipt))),
            SafeCodeAsync(() => second.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(scenario.Approved.Id, scenario.Approved.Version, null, "receipt-post-b", "receipt-post-b", FinancePaymentMethodDirection.Receipt))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "source_effect_exists" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var document = await db.SettlementDocuments.SingleAsync(item => item.Id == scenario.Approved.Id);
        Assert.Equal(FinanceSettlementDocumentStatus.Posted, document.Status);
        Assert.NotNull(document.PostedJournalId);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.SourceContract == "customer-receipt.v1" && item.SourceEvidenceId == document.Id));
    }

    [Fact]
    public async Task MESP133_sql_server_same_posted_settlement_concurrent_reversal_has_one_reversal_lineage()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("reverse-race");
        var persistence = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var posted = await persistence.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(scenario.Approved.Id, scenario.Approved.Version, null, "reverse-race-post", "reverse-race-post", FinancePaymentMethodDirection.Payment));
        Assert.True(posted.Succeeded, posted.Code);
        Assert.NotNull(posted.Value);
        var postedRecord = posted.Value!;
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.ReverseSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.reverse"), new FinanceSettlementReversalCommand(postedRecord.Id, new DateOnly(2026, 1, 20), "reverse A", Guid.NewGuid(), "reverse-a", "reverse-a", FinancePaymentMethodDirection.Payment))),
            SafeCodeAsync(() => second.ReverseSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.reverse"), new FinanceSettlementReversalCommand(postedRecord.Id, new DateOnly(2026, 1, 20), "reverse B", Guid.NewGuid(), "reverse-b", "reverse-b", FinancePaymentMethodDirection.Payment))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "document_already_reversed" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var document = await db.SettlementDocuments.SingleAsync(item => item.Id == postedRecord.Id);
        Assert.Equal(FinanceSettlementDocumentStatus.Reversed, document.Status);
        Assert.NotNull(document.ReversalJournalId);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.ReversalOfJournalId == document.PostedJournalId));
    }

    [Fact]
    public async Task MESP133_sql_server_same_allocation_concurrent_reversal_has_one_active_state_transition()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("allocation-reverse-race");
        var seeded = await SeedPayableSettlementAsync(scenario, secondItem: false);
        var creator = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var allocation = await creator.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.FirstItemId, 25m, new DateOnly(2026, 1, 15), "allocation", Guid.NewGuid(), "allocation-reverse-create", "allocation-reverse-create"));
        Assert.True(allocation.Succeeded, allocation.Code);
        Assert.NotNull(allocation.Value);
        var allocationRecord = allocation.Value!;
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.ReverseAllocationAsync(FinanceContext("tenant.finance.allocation.reverse"), new FinanceAllocationReversalCommand(allocationRecord.Id, allocationRecord.Version, "reverse allocation A", Guid.NewGuid(), "allocation-reverse-a", "allocation-reverse-a"))),
            SafeCodeAsync(() => second.ReverseAllocationAsync(FinanceContext("tenant.finance.allocation.reverse"), new FinanceAllocationReversalCommand(allocationRecord.Id, allocationRecord.Version, "reverse allocation B", Guid.NewGuid(), "allocation-reverse-b", "allocation-reverse-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "allocation_already_reversed" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(1, await db.Allocations.CountAsync(item => item.ReversalOfAllocationId == allocationRecord.Id));
        Assert.Equal(FinanceAllocationStatus.Active, (await db.Allocations.SingleAsync(item => item.Id == allocationRecord.Id)).Status);
    }

    [Fact]
    public async Task MESP133_sql_server_allocation_vs_settlement_reversal_race_has_one_valid_serialization()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("allocation-settlement-reversal-race");
        var posted = await scenario.Persistence.PostSettlementDocumentAsync(
            FinanceContext("tenant.finance.settlement.post"),
            new FinanceSettlementActionCommand(
                scenario.Approved.Id,
                scenario.Approved.Version,
                null,
                "allocation-settlement-race-post",
                "allocation-settlement-race-post",
                FinancePaymentMethodDirection.Payment));
        Assert.True(posted.Succeeded, posted.Code);
        var itemId = await SeedCompatiblePayableItemAsync(scenario, "allocation-settlement-race-item");

        var first = CreateFinanceSettlementPersistence(
            scenario.Options,
            scenario.CompanyId,
            scenario.Provider,
            new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required),
            new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(
            scenario.Options,
            scenario.CompanyId,
            scenario.Provider,
            new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required),
            new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));

        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.CreateAllocationAsync(
                FinanceContext("tenant.finance.allocation.create"),
                new FinanceAllocationCommand(posted.Value!.Id, itemId, 25m, new DateOnly(2026, 1, 15), "allocate while reversing", Guid.NewGuid(), "allocation-settlement-race-a", "allocation-settlement-race-a"))),
            SafeCodeAsync(() => second.ReverseSettlementDocumentAsync(
                FinanceContext("tenant.finance.settlement.reverse"),
                new FinanceSettlementReversalCommand(posted.Value!.Id, new DateOnly(2026, 1, 20), "reverse while allocating", Guid.NewGuid(), "allocation-settlement-race-b", "allocation-settlement-race-b", FinancePaymentMethodDirection.Payment))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var document = await db.SettlementDocuments.SingleAsync(item => item.Id == posted.Value!.Id);
        var activeAllocations = await db.Allocations.CountAsync(item => item.SettlementDocumentId == document.Id && item.Status == FinanceAllocationStatus.Active && item.ReversalOfAllocationId == null);
        var reversalJournals = await db.Journals.CountAsync(item => item.ReversalOfJournalId == document.PostedJournalId);

        if (document.Status == FinanceSettlementDocumentStatus.Posted)
        {
            Assert.Null(document.ReversalJournalId);
            Assert.Equal(1, activeAllocations);
            Assert.Equal(0, reversalJournals);
            var allocation = await db.Allocations.SingleAsync(item => item.SettlementDocumentId == document.Id && item.Status == FinanceAllocationStatus.Active);
            Assert.NotNull(allocation.JournalId);
            Assert.True(await db.Journals.AnyAsync(item => item.Id == allocation.JournalId));
        }
        else
        {
            Assert.Equal(FinanceSettlementDocumentStatus.Reversed, document.Status);
            Assert.NotNull(document.ReversalJournalId);
            Assert.Equal(0, activeAllocations);
            Assert.Equal(1, reversalJournals);
        }
    }

    [Fact]
    public async Task MESP134_sql_server_FX01_concurrent_allocation_capacity_is_serialized()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("mesp134-fx01");
        var seeded = await SeedPayableSettlementAsync(scenario, secondItem: false);
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.FirstItemId, 40m, new DateOnly(2026, 1, 15), "FX-01 A", Guid.NewGuid(), "mesp134-fx01-a", "mesp134-fx01-a"))),
            SafeCodeAsync(() => second.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.FirstItemId, 40m, new DateOnly(2026, 1, 15), "FX-01 B", Guid.NewGuid(), "mesp134-fx01-b", "mesp134-fx01-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "allocation_exceeds_outstanding" or "source_effect_exists" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(40m, await db.Allocations.Where(item => item.OpenItemId == seeded.FirstItemId && item.Status == FinanceAllocationStatus.Active).SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task MESP134_sql_server_FX02_allocation_vs_settlement_reversal_has_one_valid_serialization()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("mesp134-fx02");
        var posted = await scenario.Persistence.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(scenario.Approved.Id, scenario.Approved.Version, null, "mesp134-fx02-post", "mesp134-fx02-post", FinancePaymentMethodDirection.Payment));
        Assert.True(posted.Succeeded, posted.Code);
        var itemId = await SeedCompatiblePayableItemAsync(scenario, "mesp134-fx02-item");
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(posted.Value!.Id, itemId, 25m, new DateOnly(2026, 1, 15), "FX-02 allocate", Guid.NewGuid(), "mesp134-fx02-a", "mesp134-fx02-a"))),
            SafeCodeAsync(() => second.ReverseSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.reverse"), new FinanceSettlementReversalCommand(posted.Value!.Id, new DateOnly(2026, 1, 20), "FX-02 reverse", Guid.NewGuid(), "mesp134-fx02-b", "mesp134-fx02-b", FinancePaymentMethodDirection.Payment))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var document = await db.SettlementDocuments.SingleAsync(item => item.Id == posted.Value!.Id);
        var activeAllocations = await db.Allocations.CountAsync(item => item.SettlementDocumentId == document.Id && item.Status == FinanceAllocationStatus.Active && item.ReversalOfAllocationId == null);
        if (document.Status == FinanceSettlementDocumentStatus.Posted) Assert.Equal(1, activeAllocations);
        else { Assert.Equal(FinanceSettlementDocumentStatus.Reversed, document.Status); Assert.Equal(0, activeAllocations); }
    }

    [Fact]
    public async Task MESP134_sql_server_FX03_duplicate_allocation_reversal_is_one_to_one()
    {
        var scenario = await CreatePaymentSettlementScenarioAsync("mesp134-fx03");
        var seeded = await SeedPayableSettlementAsync(scenario, secondItem: false);
        var creator = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var allocation = await creator.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(seeded.DocumentId, seeded.FirstItemId, 25m, new DateOnly(2026, 1, 15), "FX-03 allocation", Guid.NewGuid(), "mesp134-fx03-create", "mesp134-fx03-create"));
        Assert.True(allocation.Succeeded, allocation.Code);
        var first = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var second = CreateFinanceSettlementPersistence(scenario.Options, scenario.CompanyId, scenario.Provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, scenario.SupplierId));
        var results = await Task.WhenAll(
            SafeCodeAsync(() => first.ReverseAllocationAsync(FinanceContext("tenant.finance.allocation.reverse"), new FinanceAllocationReversalCommand(allocation.Value!.Id, allocation.Value.Version, "FX-03 A", Guid.NewGuid(), "mesp134-fx03-a", "mesp134-fx03-a"))),
            SafeCodeAsync(() => second.ReverseAllocationAsync(FinanceContext("tenant.finance.allocation.reverse"), new FinanceAllocationReversalCommand(allocation.Value!.Id, allocation.Value.Version, "FX-03 B", Guid.NewGuid(), "mesp134-fx03-b", "mesp134-fx03-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(1, await db.Allocations.CountAsync(item => item.ReversalOfAllocationId == allocation.Value!.Id));
    }

    [Fact]
    public async Task MESP134_sql_server_TAX01_concurrent_tax_post_has_one_effect_and_one_snapshot()
    {
        var scenario = await CreateMesp134ScenarioAsync("mesp134-tax01");
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => scenario.First.PostTaxAsync(scenario.FirstContext, scenario.TaxCommand("mesp134-tax01-a"))),
            SafeFinanceOperationAsync(() => scenario.Second.PostTaxAsync(scenario.SecondContext, scenario.TaxCommand("mesp134-tax01-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "tax_already_posted" or "concurrency_conflict" or "finance_conflict");
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(1, await db.TaxAccountingEffects.CountAsync(item => item.OpenItemId == scenario.OpenItemId && item.ReversalJournalId == null));
        Assert.Equal(1, await db.JournalMonetaryEvidence.CountAsync(item => item.CompanyId == scenario.CompanyId));
    }

    [Fact]
    public async Task MESP134_sql_server_TAX02_tax_post_vs_reversal_has_authoritative_lineage()
    {
        var scenario = await CreateMesp134ScenarioAsync("mesp134-tax02");
        var posted = await scenario.First.PostTaxAsync(scenario.FirstContext, scenario.TaxCommand("mesp134-tax02-create"));
        Assert.True(posted.Succeeded, posted.Code);
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => scenario.First.ReverseTaxAsync(scenario.FirstContext, new FinanceTaxAccountingReversalCommand(posted.Value!.Id, posted.Value.Version, "TAX-02 correction", Guid.NewGuid(), "mesp134-tax02-reverse", "mesp134-tax02-reverse"))),
            SafeFinanceOperationAsync(() => scenario.Second.PostTaxAsync(scenario.SecondContext, scenario.TaxCommand("mesp134-tax02-post-again"))));

        Assert.Contains(results, item => item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var effect = await db.TaxAccountingEffects.SingleAsync(item => item.Id == posted.Value!.Id);
        Assert.Equal(FinanceEvidenceStatus.Reversed, effect.Status);
        Assert.NotNull(effect.ReversalJournalId);
    }

    [Fact]
    public async Task MESP134_sql_server_REV01_concurrent_same_batch_post_has_one_authoritative_batch()
    {
        var scenario = await CreateMesp134ScenarioAsync("mesp134-rev01");
        var batch = await scenario.First.CreateRevaluationBatchAsync(scenario.FirstContext, scenario.RevaluationCommand("mesp134-rev01-create"));
        Assert.True(batch.Succeeded, batch.Code);
        var calculated = await scenario.First.CalculateRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(batch.Value!.Id, batch.Value.Version, null, Guid.NewGuid(), "mesp134-rev01-calculate", "mesp134-rev01-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => scenario.First.PostRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(calculated.Value!.Id, calculated.Value.Version, null, Guid.NewGuid(), "mesp134-rev01-a", "mesp134-rev01-a"))),
            SafeFinanceOperationAsync(() => scenario.Second.PostRevaluationBatchAsync(scenario.SecondContext, new FinanceRevaluationActionCommand(calculated.Value!.Id, calculated.Value.Version, null, Guid.NewGuid(), "mesp134-rev01-b", "mesp134-rev01-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(FinanceRevaluationBatchStatus.Posted, (await db.RevaluationBatches.SingleAsync(item => item.Id == batch.Value!.Id)).Status);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-revaluation.v1"));
    }

    [Fact]
    public async Task MESP134_sql_server_REV02_two_batches_same_source_have_one_active_post()
    {
        var scenario = await CreateMesp134ScenarioAsync("mesp134-rev02");
        var first = await scenario.First.CreateRevaluationBatchAsync(scenario.FirstContext, scenario.RevaluationCommand("mesp134-rev02-create-a"));
        var second = await scenario.Second.CreateRevaluationBatchAsync(scenario.SecondContext, scenario.RevaluationCommand("mesp134-rev02-create-b"));
        Assert.True(first.Succeeded && second.Succeeded);
        var firstCalculated = await scenario.First.CalculateRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(first.Value!.Id, first.Value.Version, null, Guid.NewGuid(), "mesp134-rev02-calc-a", "mesp134-rev02-calc-a"));
        var secondCalculated = await scenario.Second.CalculateRevaluationBatchAsync(scenario.SecondContext, new FinanceRevaluationActionCommand(second.Value!.Id, second.Value.Version, null, Guid.NewGuid(), "mesp134-rev02-calc-b", "mesp134-rev02-calc-b"));
        Assert.True(firstCalculated.Succeeded && secondCalculated.Succeeded);
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => scenario.First.PostRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(firstCalculated.Value!.Id, firstCalculated.Value.Version, null, Guid.NewGuid(), "mesp134-rev02-post-a", "mesp134-rev02-post-a"))),
            SafeFinanceOperationAsync(() => scenario.Second.PostRevaluationBatchAsync(scenario.SecondContext, new FinanceRevaluationActionCommand(secondCalculated.Value!.Id, secondCalculated.Value.Version, null, Guid.NewGuid(), "mesp134-rev02-post-b", "mesp134-rev02-post-b"))));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        Assert.Equal(1, await db.RevaluationBatches.CountAsync(item => item.CompanyId == scenario.CompanyId && item.Status == FinanceRevaluationBatchStatus.Posted));
    }

    [Fact]
    public async Task MESP134_sql_server_REV03_revaluation_post_vs_source_mutation_fails_closed_or_commits_consistently()
    {
        var scenario = await CreateMesp134ScenarioAsync("mesp134-rev03");
        var batch = await scenario.First.CreateRevaluationBatchAsync(scenario.FirstContext, scenario.RevaluationCommand("mesp134-rev03-create"));
        Assert.True(batch.Succeeded, batch.Code);
        var calculated = await scenario.First.CalculateRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(batch.Value!.Id, batch.Value.Version, null, Guid.NewGuid(), "mesp134-rev03-calculate", "mesp134-rev03-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        var post = SafeFinanceOperationAsync(() => scenario.First.PostRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(calculated.Value!.Id, calculated.Value.Version, null, Guid.NewGuid(), "mesp134-rev03-post", "mesp134-rev03-post")));
        var allocation = SafeFinanceOperationAsync(() => CreateMesp134AllocationAsync(scenario));
        await Task.WhenAll(post, allocation);
        var result = await post;
        var allocationResult = await allocation;
        Assert.True(result.Succeeded || result.Code is "revaluation_source_changed" or "concurrency_conflict", result.Code);
        Assert.True(allocationResult.Succeeded || allocationResult.Code is "concurrency_conflict", allocationResult.Code);
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var persisted = await db.RevaluationBatches.SingleAsync(item => item.Id == batch.Value!.Id);
        var source = await db.OpenItems.SingleAsync(item => item.Id == scenario.OpenItemId);
        var activeAllocations = await db.Allocations.Where(item => item.OpenItemId == scenario.OpenItemId && item.Status == FinanceAllocationStatus.Active && item.ReversalOfAllocationId == null).ToListAsync();
        var revaluationLines = await db.RevaluationLines.Where(item => item.BatchId == persisted.Id).ToListAsync();
        var revaluationJournals = await db.Journals.Include(item => item.Lines).Where(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-revaluation.v1" && item.SourceEvent == "unrealized").ToListAsync();
        var allocationJournals = await db.Journals.Include(item => item.Lines).Where(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "customer-receipt.v1" && item.SourceEvent == "allocation").ToListAsync();
        var allJournals = await db.Journals.Include(item => item.Lines).Where(item => item.CompanyId == scenario.CompanyId && (item.Status == FinanceJournalStatus.Posted || item.Status == FinanceJournalStatus.Reversed)).ToListAsync();

        Assert.Equal(100m, source.OriginalAmount);
        Assert.Equal(allocationResult.Succeeded ? 25m : 0m, activeAllocations.Sum(item => item.Amount));
        Assert.Equal(100m - activeAllocations.Sum(item => item.Amount), source.OriginalAmount - activeAllocations.Sum(item => item.Amount));
        var line = Assert.Single(revaluationLines);
        Assert.False(string.IsNullOrWhiteSpace(line.SourceSnapshotJson));
        using var snapshot = System.Text.Json.JsonDocument.Parse(line.SourceSnapshotJson);
        Assert.Equal(0, snapshot.RootElement.GetProperty("Allocations").GetArrayLength());
        Assert.Equal(result.Succeeded ? FinanceRevaluationBatchStatus.Posted : FinanceRevaluationBatchStatus.Calculated, persisted.Status);
        Assert.Equal(result.Succeeded ? 1 : 0, revaluationJournals.Count);
        Assert.Equal(allocationResult.Succeeded ? 1 : 0, allocationJournals.Count);
        Assert.All(allJournals, journal =>
        {
            Assert.Equal(journal.Lines.Sum(item => item.Debit), journal.Lines.Sum(item => item.Credit));
            Assert.Equal(journal.Lines.Sum(item => item.FunctionalDebit), journal.Lines.Sum(item => item.FunctionalCredit));
        });

        if (result.Succeeded)
        {
            Assert.Equal(FinanceRevaluationBatchStatus.Posted, persisted.Status);
            Assert.NotNull(line.JournalId);
            if (allocationResult.Succeeded)
            {
                Assert.True(revaluationJournals.Single().CreatedAt <= allocationJournals.Single().CreatedAt);
            }
        }
        else
        {
            Assert.Contains(result.Code, new[] { "revaluation_source_changed", "concurrency_conflict" });
            Assert.Null(line.JournalId);
            Assert.Empty(revaluationJournals);
        }
    }

    [Fact]
    public async Task MESP134_sql_server_REV04_reversal_vs_later_revaluation_has_one_valid_serialization()
    {
        var scenario = await CreateMesp134ScenarioAsync("mesp134-rev04");
        var first = await scenario.First.CreateRevaluationBatchAsync(scenario.FirstContext, scenario.RevaluationCommand("mesp134-rev04-create-a"));
        Assert.True(first.Succeeded, first.Code);
        var firstCalculated = await scenario.First.CalculateRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(first.Value!.Id, first.Value.Version, null, Guid.NewGuid(), "mesp134-rev04-calc-a", "mesp134-rev04-calc-a"));
        Assert.True(firstCalculated.Succeeded, firstCalculated.Code);
        var firstPosted = await scenario.First.PostRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(firstCalculated.Value!.Id, firstCalculated.Value.Version, null, Guid.NewGuid(), "mesp134-rev04-post-a", "mesp134-rev04-post-a"));
        Assert.True(firstPosted.Succeeded, firstPosted.Code);
        var later = await scenario.Second.CreateRevaluationBatchAsync(scenario.SecondContext, scenario.RevaluationCommand("mesp134-rev04-create-b"));
        Assert.True(later.Succeeded, later.Code);
        var laterCalculated = await scenario.Second.CalculateRevaluationBatchAsync(scenario.SecondContext, new FinanceRevaluationActionCommand(later.Value!.Id, later.Value.Version, null, Guid.NewGuid(), "mesp134-rev04-calc-b", "mesp134-rev04-calc-b"));
        Assert.True(laterCalculated.Succeeded, laterCalculated.Code);
        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => scenario.First.ReverseRevaluationBatchAsync(scenario.FirstContext, new FinanceRevaluationActionCommand(firstPosted.Value!.Id, firstPosted.Value.Version, "REV-04 correction", Guid.NewGuid(), "mesp134-rev04-reverse", "mesp134-rev04-reverse"))),
            SafeFinanceOperationAsync(() => scenario.Second.PostRevaluationBatchAsync(scenario.SecondContext, new FinanceRevaluationActionCommand(laterCalculated.Value!.Id, laterCalculated.Value.Version, null, Guid.NewGuid(), "mesp134-rev04-post-b", "mesp134-rev04-post-b"))));

        Assert.Contains(results, item => item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var posted = await db.RevaluationBatches.CountAsync(item => item.CompanyId == scenario.CompanyId && item.Status == FinanceRevaluationBatchStatus.Posted);
        Assert.InRange(posted, 0, 1);
    }

    private async Task<(DbContextOptions Options, Guid CompanyId)> CreateSettlementOptionsAsync()
    {
        var options = SqlServerMigrationConfiguration.Configure(await GetConnectionStringAsync(), SqlServerMigrationConfiguration.FinanceHistoryTable);
        return (options, Guid.NewGuid());
    }

    private FinanceSettlementPersistence CreateSettlementPersistence(DbContextOptions options, Guid companyId) =>
        new(
            options,
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL Settlement Company", "SAR")]),
            new UnavailableMasterDataExchangeRatePersistence(),
            new UnavailableCustomerPersistence(),
            new UnavailableSupplierPersistence(),
            new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            new UnavailableFinanceSupplierInvoiceSourceProvider());

    private FinanceSettlementPersistence CreateFinanceSettlementPersistence(
        DbContextOptions options,
        Guid companyId,
        IFinanceCompanyProvider provider,
        IFinanceSourceApprovalPolicy approvalPolicy,
        ISupplierPersistence suppliers,
        IFinanceSupplierInvoiceSourceProvider? sourceProvider = null) =>
        new(
            options,
            provider,
            new UnavailableMasterDataExchangeRatePersistence(),
            new UnavailableCustomerPersistence(),
            suppliers,
            new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            sourceProvider ?? new UnavailableFinanceSupplierInvoiceSourceProvider(),
            approvalPolicy);

    private static async Task CreateOpenPeriodAndRuleAsync(
        DbContextOptions options,
        IFinanceCompanyProvider provider,
        FinanceRequestContext context,
        Guid companyId,
        Guid debitAccountId,
        Guid creditAccountId,
        string sourceContract,
        string sourceEvent)
    {
        var persistence = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var calendar = await persistence.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(companyId, "SQL Safety FY", Guid.NewGuid(), $"calendar-{Guid.NewGuid():N}", "calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await persistence.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), $"year-{Guid.NewGuid():N}", "year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await persistence.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, $"period-{Guid.NewGuid():N}", "2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), $"period-{Guid.NewGuid():N}", "period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await persistence.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, $"open-{Guid.NewGuid():N}", "open"));
        Assert.True(opened.Succeeded, opened.Code);
        var rule = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(companyId, sourceContract, sourceEvent, debitAccountId, creditAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"rule-{Guid.NewGuid():N}", "rule"));
        Assert.True(rule.Succeeded, rule.Code);
        var allocationRule = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(companyId, sourceContract, "allocation", debitAccountId, creditAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"allocation-rule-{Guid.NewGuid():N}", "allocation rule"));
        Assert.True(allocationRule.Succeeded, allocationRule.Code);
    }

    private async Task<SqlMesp134Scenario> CreateMesp134ScenarioAsync(string key)
    {
        var connectionString = await GetConnectionStringAsync();
        var options = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var masterDataOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable);
        var companyId = Guid.NewGuid();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, $"SQL {key} Company", "SAR")]);
        var masterDataFoundation = FoundationRequestContext.ForTenant(Guid.NewGuid(), Guid.NewGuid(), _fixture.TenantA, "tenant.master-data.tax.create");
        var masterDataContext = MasterDataRequestContext.FromFoundationContext(masterDataFoundation);
        var taxAuthorization = new MasterDataResourceAuthorizationService(new GrantingMasterDataCapabilityResolver(), new TaxResourcePolicy(), new TaxApprovalPolicy(), new TaxScopePolicy());
        var taxService = new MasterDataTaxService(taxAuthorization, new MasterDataTaxPersistence(masterDataOptions));
        var tax = await taxService.CreateTaxAsync(masterDataContext, new CreateMasterDataTaxCommand(
            $"VAT-{key}", "VAT", new LocalizedName($"VAT {key}", "ضريبة"), new LocalizedName($"VAT {key}", "ضريبة"),
            TaxDirection.Both, new MasterDataTaxRateVersion(new DateOnly(2026, 1, 1), null, 15m)));
        Assert.True(tax.Succeeded, tax.Code);

        var rates = new SqlMesp134ExchangeRatePersistence(_fixture.TenantA.TenantId, "USD", "SAR", 3.5m, 3.75m);
        var currencies = new SqlMesp134CurrencyPersistence(_fixture.TenantA.TenantId);
        var finance = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), rates);
        var setupContext = FinanceContext("tenant.finance.setup");
        var accounts = await CreateFinanceAccountsAsync(finance, setupContext, companyId, key);
        var calendar = await finance.CreateCalendarAsync(setupContext, new FinanceFiscalCalendarCommand(companyId, $"{key} FY", Guid.NewGuid(), $"{key}-calendar", $"{key}-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await finance.CreateYearAsync(setupContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), $"{key}-year", $"{key}-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await finance.CreatePeriodAsync(setupContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, $"{key}-period", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), $"{key}-period-create", $"{key}-period-create"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await finance.SetPeriodStateAsync(setupContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, $"{key}-open", $"{key}-open"));
        Assert.True(opened.Succeeded, opened.Code);
        var taxRule = await finance.CreatePostingRuleAsync(setupContext, new FinancePostingRuleCommand(companyId, "finance-tax.v1", "output", accounts.Debit.Id, accounts.Credit.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"{key}-tax-rule", $"{key}-tax-rule"));
        Assert.True(taxRule.Succeeded, taxRule.Code);
        var fxRule = await finance.CreatePostingRuleAsync(setupContext, new FinancePostingRuleCommand(companyId, "finance-fx.v1", "unrealized", accounts.Debit.Id, accounts.Credit.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"{key}-fx-rule", $"{key}-fx-rule"));
        Assert.True(fxRule.Succeeded, fxRule.Code);
        var receiptOnAccount = await finance.CreatePostingRuleAsync(setupContext, new FinancePostingRuleCommand(companyId, "customer-receipt.v1", "on-account", accounts.Debit.Id, accounts.Credit.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"{key}-receipt-on-account", $"{key}-receipt-on-account"));
        Assert.True(receiptOnAccount.Succeeded, receiptOnAccount.Code);
        var receiptAllocation = await finance.CreatePostingRuleAsync(setupContext, new FinancePostingRuleCommand(companyId, "customer-receipt.v1", "allocation", accounts.Debit.Id, accounts.Credit.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"{key}-receipt-allocation", $"{key}-receipt-allocation"));
        Assert.True(receiptAllocation.Succeeded, receiptAllocation.Code);
        var realizedGain = await finance.CreateAccountAsync(setupContext, new FinanceAccountCommand(companyId, $"{key}-FX-GAIN", "SQL realized FX gain", null, null, FinanceAccountType.Revenue, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{key}-realized-gain", $"{key}-realized-gain"));
        Assert.True(realizedGain.Succeeded, realizedGain.Code);
        var realizedRule = await finance.CreatePostingRuleAsync(setupContext, new FinancePostingRuleCommand(companyId, "finance-fx.v1", "realized", accounts.Credit.Id, realizedGain.Value!.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"{key}-realized-rule", $"{key}-realized-rule"));
        Assert.True(realizedRule.Succeeded, realizedRule.Code);

        var openItemId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var recognitionJournalId = Guid.NewGuid();
        await using (var db = new FinanceDbContext(options, _fixture.TenantA))
        {
            var debit = await db.Accounts.SingleAsync(item => item.Id == accounts.Debit.Id);
            var credit = await db.Accounts.SingleAsync(item => item.Id == accounts.Credit.Id);
            var recognition = new FinanceJournalEntity(_fixture.TenantA.TenantId, recognitionJournalId, new FinanceJournalCommand(companyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "USD", 3.5m, rates.RateId, rates.HistoricalRateVersionId, 1, "manual-ar.v1", "recognition", openItemId, 1, null, $"{key} recognition", [new FinanceJournalLineCommand(credit.Id, 100m, 0m, 350m, "USD", null, "Revenue"), new FinanceJournalLineCommand(debit.Id, 0m, 100m, 350m, "USD", null, "AR")], recognitionJournalId, $"{key}-recognition", $"{key}-recognition", FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired), 1, "SAR", Guid.NewGuid(), DateTimeOffset.UtcNow);
            recognition.SetPeriod(year.Value.Id, period.Value.Id);
            recognition.SetStatus(FinanceJournalStatus.Posted, Guid.NewGuid(), DateTimeOffset.UtcNow);
            recognition.Lines.Add(new FinanceJournalLineEntity(_fixture.TenantA.TenantId, Guid.NewGuid(), recognition.Id, 1, credit, new FinanceJournalLineCommand(credit.Id, 100m, 0m, 350m, "USD", null, "Revenue"), null, 350m, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            recognition.Lines.Add(new FinanceJournalLineEntity(_fixture.TenantA.TenantId, Guid.NewGuid(), recognition.Id, 2, debit, new FinanceJournalLineCommand(debit.Id, 0m, 100m, 350m, "USD", null, "AR"), null, 0m, 350m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            var item = new FinanceOpenItemEntity(_fixture.TenantA.TenantId, openItemId, FinanceOpenItemKind.Receivable, companyId, null, customerId, "manual-ar.v1", openItemId, 1, openItemId, 1, $"{key}-AR", new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15), "USD", 100m, "SAR", 350m, 3.5m, rates.RateId, rates.HistoricalRateVersionId, 1, null, null, null, $"{key}-source");
            item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, recognition.Id);
            db.Journals.Add(recognition);
            db.OpenItems.Add(item);
            await db.SaveChangesAsync();
        }

        var settlement = new FinanceSettlementPersistence(
            options,
            provider,
            rates,
            new SqlStaticCustomerPersistence(_fixture.TenantA.TenantId.Value, customerId),
            new UnavailableSupplierPersistence(),
            currencies,
            new UnavailableFinanceSupplierInvoiceSourceProvider(),
            new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required));
        var settlementRace = new FinanceSettlementPersistence(
            options,
            provider,
            rates,
            new SqlStaticCustomerPersistence(_fixture.TenantA.TenantId.Value, customerId),
            new UnavailableSupplierPersistence(),
            currencies,
            new UnavailableFinanceSupplierInvoiceSourceProvider(),
            new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required));
        var first = new FinanceMesp134Persistence(options, provider, currencies, rates, taxService, new UnavailableFinanceSupplierInvoiceSourceProvider());
        var second = new FinanceMesp134Persistence(options, provider, currencies, rates, taxService, new UnavailableFinanceSupplierInvoiceSourceProvider());
        var firstContext = FinanceContext("tenant.finance.tax.post");
        var secondContext = FinanceContext("tenant.finance.tax.post");
        var policy = await first.CreateMonetaryPolicyAsync(firstContext, new FinanceMonetaryPolicyCommand(companyId, null, 2, "AwayFromZero", true, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"{key}-policy", $"{key}-policy"));
        Assert.True(policy.Succeeded, policy.Code);
        return new SqlMesp134Scenario(options, companyId, first, second, firstContext, secondContext, tax.Value!.Id, openItemId, rates, settlement, settlementRace, customerId, accounts.Debit, accounts.Credit);
    }

    private async Task<FinanceOperationResult<FinanceAllocationRecord>> CreateMesp134AllocationAsync(SqlMesp134Scenario scenario)
    {
        var context = FinanceContext("tenant.finance.settlement.create");
        var method = await scenario.Settlement.CreatePaymentMethodAsync(context, new FinancePaymentMethodCommand(scenario.CompanyId, "REV03-METHOD", "REV03 method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "mesp134-rev03-method", "mesp134-rev03-method"));
        Assert.True(method.Succeeded, method.Code);
        var cash = await scenario.Settlement.CreateCashAccountAsync(context, new FinanceCashAccountCommand(scenario.CompanyId, "REV03-CASH", "REV03 cash", null, FinanceCashAccountKind.Bank, "USD", scenario.DebitAccount.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "mesp134-rev03-cash", "mesp134-rev03-cash"));
        Assert.True(cash.Succeeded, cash.Code);
        var created = await scenario.Settlement.CreateSettlementDocumentAsync(context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, scenario.CompanyId, null, scenario.CustomerId, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 8, 25), "USD", 25m, null, 3.75m, scenario.Rates.RateId, scenario.Rates.CurrentRateVersionId, 2, "REV03 receipt", "REV03 race receipt", Guid.NewGuid(), "mesp134-rev03-settlement", "mesp134-rev03-settlement"));
        Assert.True(created.Succeeded, created.Code);
        var submitted = await scenario.Settlement.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, "mesp134-rev03-submit", "mesp134-rev03-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await scenario.Settlement.TransitionSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.approve"), new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "mesp134-rev03-approve", "mesp134-rev03-approve", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await scenario.Settlement.PostSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.post"), new FinanceSettlementActionCommand(approved.Value!.Id, approved.Value.Version, null, "mesp134-rev03-post-settlement", "mesp134-rev03-post-settlement", FinancePaymentMethodDirection.Receipt));
        Assert.True(posted.Succeeded, posted.Code);
        return await scenario.SettlementRace.CreateAllocationAsync(FinanceContext("tenant.finance.allocation.create"), new FinanceAllocationCommand(posted.Value!.Id, scenario.OpenItemId, 25m, new DateOnly(2026, 8, 25), "REV03 real allocation race", Guid.NewGuid(), "mesp134-rev03-allocation", "mesp134-rev03-allocation"));
    }

    private sealed record SqlMesp134Scenario(
        DbContextOptions Options,
        Guid CompanyId,
        FinanceMesp134Persistence First,
        FinanceMesp134Persistence Second,
        FinanceRequestContext FirstContext,
        FinanceRequestContext SecondContext,
        Guid TaxId,
        Guid OpenItemId,
        SqlMesp134ExchangeRatePersistence Rates,
        FinanceSettlementPersistence Settlement,
        FinanceSettlementPersistence SettlementRace,
        Guid CustomerId,
        FinanceAccountRecord DebitAccount,
        FinanceAccountRecord CreditAccount)
    {
        internal FinanceTaxAccountingCommand TaxCommand(string key) => new(CompanyId, OpenItemId, TaxId, 100m, "sql-mesp134", Guid.NewGuid(), key, key);
        internal FinanceRevaluationBatchCommand RevaluationCommand(string key) => new(CompanyId, new DateOnly(2026, 8, 25), FinanceRevaluationScopes.ApArAndUnallocatedSettlements, Guid.NewGuid(), key, key);
    }

    private async Task<SqlSettlementScenario> CreatePaymentSettlementScenarioAsync(string key)
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, $"SQL {key} Company", "SAR")]);
        var supplierId = Guid.NewGuid();
        var suppliers = new SqlStaticSupplierPersistence(_fixture.TenantA.TenantId.Value, supplierId);
        var persistence = CreateFinanceSettlementPersistence(options, companyId, provider, new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required), suppliers);
        var context = FinanceContext("tenant.finance.account.create");
        var finance = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var accounts = await CreateFinanceAccountsAsync(finance, context, companyId, $"sql-{key}");
        await CreateOpenPeriodAndRuleAsync(options, provider, context, companyId, accounts.Debit.Id, accounts.Credit.Id, "supplier-payment.v1", "on-account");
        var method = await persistence.CreatePaymentMethodAsync(context, new FinancePaymentMethodCommand(companyId, $"{key}-METHOD", $"{key} method", null, FinancePaymentMethodDirection.Payment, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{key}-method", $"{key}-method"));
        Assert.True(method.Succeeded, method.Code);
        var cash = await persistence.CreateCashAccountAsync(context, new FinanceCashAccountCommand(companyId, $"{key}-CASH", $"{key} cash", null, FinanceCashAccountKind.Bank, "SAR", accounts.Credit.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{key}-cash", $"{key}-cash"));
        Assert.True(cash.Succeeded, cash.Code);
        var created = await persistence.CreateSettlementDocumentAsync(
            context,
            new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Payment, companyId, supplierId, null, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 15), "SAR", 100m, null, null, null, null, null, $"{key}-reference", $"{key} settlement", Guid.NewGuid(), $"{key}-create", $"{key}-create"));
        Assert.True(created.Succeeded, created.Code);
        var submitted = await persistence.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, $"{key}-submit", $"{key}-submit", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await persistence.TransitionSettlementDocumentAsync(FinanceContext("tenant.finance.settlement.approve", Guid.NewGuid()), new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, $"{key}-approve", $"{key}-approve", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        return new(options, companyId, provider, persistence, supplierId, method.Value!, cash.Value!, created.Value!, submitted.Value!, approved.Value!);
    }

    private sealed record SqlSettlementScenario(
        DbContextOptions Options,
        Guid CompanyId,
        IFinanceCompanyProvider Provider,
        FinanceSettlementPersistence Persistence,
        Guid SupplierId,
        FinancePaymentMethodRecord Method,
        FinanceCashAccountRecord Cash,
        FinanceSettlementDocumentRecord Created,
        FinanceSettlementDocumentRecord Submitted,
        FinanceSettlementDocumentRecord Approved);

    private async Task<SqlReceiptSettlementScenario> CreateReceiptSettlementScenarioAsync(string key)
    {
        var (options, companyId) = await CreateSettlementOptionsAsync();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, $"SQL {key} Company", "SAR")]);
        var persistence = new FinanceSettlementPersistence(options, provider, new UnavailableMasterDataExchangeRatePersistence(), new UnavailableCustomerPersistence(), new UnavailableSupplierPersistence(), new UnavailableMasterDataCurrencyPaymentTermPersistence(), new UnavailableFinanceSupplierInvoiceSourceProvider(), new SqlSettlementApprovalPolicy(FinanceApprovalRequirement.Required));
        var context = FinanceContext("tenant.finance.account.create");
        var finance = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var accounts = await CreateFinanceAccountsAsync(finance, context, companyId, $"sql-{key}");
        await CreateOpenPeriodAndRuleAsync(options, provider, context, companyId, accounts.Debit.Id, accounts.Credit.Id, "customer-receipt.v1", "on-account");
        var method = await persistence.CreatePaymentMethodAsync(context, new FinancePaymentMethodCommand(companyId, $"{key}-METHOD", $"{key} method", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{key}-method", $"{key}-method"));
        Assert.True(method.Succeeded, method.Code);
        var cash = await persistence.CreateCashAccountAsync(context, new FinanceCashAccountCommand(companyId, $"{key}-CASH", $"{key} cash", null, FinanceCashAccountKind.Bank, "SAR", accounts.Debit.Id, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{key}-cash", $"{key}-cash"));
        Assert.True(cash.Succeeded, cash.Code);
        var customerId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        await using (var db = new FinanceDbContext(options, _fixture.TenantA))
        {
            var document = new FinanceSettlementDocumentEntity(
                context.TenantId,
                new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, companyId, null, customerId, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 15), "SAR", 100m, null, null, null, null, null, $"{key}-reference", $"{key} receipt", documentId, $"{key}-create", $"{key}-create"),
                "SAR",
                "SAR",
                100m,
                context.ActorId,
                DateTimeOffset.UtcNow);
            document.SetStatus(FinanceSettlementDocumentStatus.Submitted, ActorId, DateTimeOffset.UtcNow);
            document.SetStatus(FinanceSettlementDocumentStatus.Approved, ApproverId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        var approved = await persistence.GetSettlementDocumentAsync(context, documentId);
        Assert.NotNull(approved);
        return new(options, companyId, provider, customerId, approved!);
    }

    private sealed record SqlReceiptSettlementScenario(
        DbContextOptions Options,
        Guid CompanyId,
        IFinanceCompanyProvider Provider,
        Guid CustomerId,
        FinanceSettlementDocumentRecord Approved);

    private async Task<(Guid DocumentId, Guid FirstItemId, Guid? SecondItemId)> SeedPayableSettlementAsync(SqlSettlementScenario scenario, bool secondItem)
    {
        var context = FinanceContext("tenant.finance.settlement.post");
        var recognitionRule = await EnsurePayableRecognitionRuleAsync(scenario, context);
        var documentId = Guid.NewGuid();
        var firstItemId = Guid.NewGuid();
        Guid? secondItemId = secondItem ? Guid.NewGuid() : null;
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var document = new FinanceSettlementDocumentEntity(
            context.TenantId,
            new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Payment, scenario.CompanyId, scenario.SupplierId, null, scenario.Cash.Id, scenario.Method.Id, new DateOnly(2026, 1, 15), "SAR", 60m, null, null, null, null, null, "seeded payment", "seeded payment", documentId, $"seed-document-{documentId:N}", $"seed-document-{documentId:N}"),
            "SAR",
            "SAR",
            60m,
            context.ActorId,
            DateTimeOffset.UtcNow);
        document.SetStatus(FinanceSettlementDocumentStatus.Submitted, ActorId, DateTimeOffset.UtcNow);
        document.SetStatus(FinanceSettlementDocumentStatus.Approved, ApproverId, DateTimeOffset.UtcNow);
        db.SettlementDocuments.Add(document);
        var firstItem = CreatePayableItem(context, scenario.CompanyId, firstItemId, scenario.SupplierId, "AP-ALLOC-1");
        firstItem.SetRecognition(FinanceOpenItemRecognitionState.Recognized, await CreatePostedPayableRecognitionJournalAsync(scenario, context, recognitionRule.Id, recognitionRule.CreditAccountId, firstItemId));
        db.OpenItems.Add(firstItem);
        if (secondItemId is { } value)
        {
            var secondOpenItem = CreatePayableItem(context, scenario.CompanyId, value, scenario.SupplierId, "AP-ALLOC-2");
            secondOpenItem.SetRecognition(FinanceOpenItemRecognitionState.Recognized, await CreatePostedPayableRecognitionJournalAsync(scenario, context, recognitionRule.Id, recognitionRule.CreditAccountId, value));
            db.OpenItems.Add(secondOpenItem);
        }
        await db.SaveChangesAsync();
        var posted = await scenario.Persistence.PostSettlementDocumentAsync(
            context,
            new FinanceSettlementActionCommand(
                documentId,
                document.Version,
                null,
                $"seed-post-{documentId:N}",
                $"seed-post-{documentId:N}",
                FinancePaymentMethodDirection.Payment));
        Assert.True(posted.Succeeded, posted.Code);
        return (documentId, firstItemId, secondItemId);
    }

    private async Task<Guid> SeedCompatiblePayableItemAsync(SqlSettlementScenario scenario, string reference)
    {
        var context = FinanceContext("tenant.finance.settlement.post");
        var itemId = Guid.NewGuid();
        var recognitionRule = await EnsurePayableRecognitionRuleAsync(scenario, context);
        var recognitionJournalId = await CreatePostedPayableRecognitionJournalAsync(scenario, context, recognitionRule.Id, recognitionRule.CreditAccountId, itemId);
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var item = CreatePayableItem(context, scenario.CompanyId, itemId, scenario.SupplierId, reference);
        item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, recognitionJournalId);
        db.OpenItems.Add(item);
        await db.SaveChangesAsync();
        return itemId;
    }

    private async Task<FinancePostingRuleRecord> EnsurePayableRecognitionRuleAsync(SqlSettlementScenario scenario, FinanceRequestContext context)
    {
        await using var db = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        var controlAccountId = await db.Accounts
            .Where(item => item.CompanyId == scenario.CompanyId && item.Id != scenario.Cash.LinkedAccountId)
            .Select(item => item.Id)
            .SingleAsync();
        var finance = new FinancePersistence(scenario.Options, scenario.Provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var existing = await finance.ListPostingRulesAsync(context, scenario.CompanyId);
        var found = existing.SingleOrDefault(item => item.SourceContract == "procurement-supplier-invoice.v1" && item.SourceEvent == "recognition");
        if (found is not null) return found;
        var created = await finance.CreatePostingRuleAsync(
            context,
            new FinancePostingRuleCommand(scenario.CompanyId, "procurement-supplier-invoice.v1", "recognition", scenario.Cash.LinkedAccountId, controlAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "sql-ap-recognition-rule", "sql-ap-recognition-rule"));
        Assert.True(created.Succeeded, created.Code);
        return created.Value!;
    }

    private async Task<Guid> CreatePostedPayableRecognitionJournalAsync(SqlSettlementScenario scenario, FinanceRequestContext context, Guid ruleId, Guid controlAccountId, Guid evidenceId)
    {
        var finance = new FinancePersistence(scenario.Options, scenario.Provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var created = await finance.CreateJournalAsync(
            context,
            new FinanceJournalCommand(
                scenario.CompanyId,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 1),
                "SAR",
                1m,
                null,
                null,
                null,
                "procurement-supplier-invoice.v1",
                "recognition",
                evidenceId,
                1,
                ruleId,
                "SQL AP recognition",
                [
                    new FinanceJournalLineCommand(scenario.Cash.LinkedAccountId, 100m, 0m, 100m, "SAR", null, "AP recognition debit"),
                    new FinanceJournalLineCommand(controlAccountId, 0m, 100m, 100m, "SAR", null, "AP recognition credit")
                ],
                Guid.NewGuid(),
                $"sql-ap-recognition-{evidenceId:N}",
                $"sql-ap-recognition-{evidenceId:N}",
                FinanceJournalAmountAuthority.ManualTransactionCurrency,
                FinanceApprovalRequirement.NotRequired));
        Assert.True(created.Succeeded, created.Code);
        var posted = await finance.PostJournalAsync(
            context,
            new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, null, $"sql-ap-recognition-post-{evidenceId:N}", $"sql-ap-recognition-post-{evidenceId:N}"));
        Assert.True(posted.Succeeded, posted.Code);
        return posted.Value!.Id;
    }

    private static FinanceOpenItemEntity CreatePayableItem(FinanceRequestContext context, Guid companyId, Guid id, Guid supplierId, string reference) =>
        new(
            context.TenantId,
            id,
            FinanceOpenItemKind.Payable,
            companyId,
            supplierId,
            null,
            "procurement-supplier-invoice.v1",
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            reference,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            "SAR",
            100m,
            "SAR",
            100m,
            1m,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private async Task<SqlSettlementTarget> SeedDraftSettlementAsync(SqlSettlementScenario scenario)
    {
        var context = FinanceContext("tenant.finance.settlement.create");
        var id = Guid.NewGuid();
        await using (var db = new FinanceDbContext(scenario.Options, _fixture.TenantA))
        {
            db.SettlementDocuments.Add(new FinanceSettlementDocumentEntity(
                context.TenantId,
                new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Payment, scenario.CompanyId, scenario.SupplierId, null, scenario.Cash.Id, scenario.Method.Id, new DateOnly(2026, 1, 15), "SAR", 100m, null, null, null, null, null, "draft", "draft", id, $"draft-{id:N}", $"draft-{id:N}"),
                "SAR",
                "SAR",
                100m,
                context.ActorId,
                DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        await using var readDb = new FinanceDbContext(scenario.Options, _fixture.TenantA);
        return new(id, await readDb.SettlementDocuments.Where(item => item.Id == id).Select(item => item.Version).SingleAsync());
    }

    private sealed record SqlSettlementTarget(Guid Id, byte[] Version);

    private static FinancePaymentMethodCommand PaymentMethodCommand(Guid companyId, string code, Guid id, string key) =>
        new(companyId, code, code, null, FinancePaymentMethodDirection.Both, true, false, new DateOnly(2026, 1, 1), null, id, null, key, key);

    private static FinanceCashAccountCommand CashAccountCommand(Guid companyId, Guid linkedAccountId, string code, Guid id, string key) =>
        new(companyId, code, code, null, FinanceCashAccountKind.Bank, "SAR", linkedAccountId, null, new DateOnly(2026, 1, 1), null, id, null, key, key);

    private async Task<FinanceAccountRecord> CreateSettlementLinkedAccountAsync(DbContextOptions options, Guid companyId, string key)
    {
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL Settlement Company", "SAR")]);
        var persistence = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var context = FinanceContext("tenant.finance.account.create");
        var result = await persistence.CreateAccountAsync(context, new FinanceAccountCommand(companyId, $"{key}-ACCOUNT", "Settlement cash account", null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{key}-account", $"{key}-account"));
        Assert.True(result.Succeeded, result.Code);
        return result.Value!;
    }

    [Fact]
    public async Task MESP132_sql_server_period_close_and_post_have_one_consistent_committed_order()
    {
        var scenario = await CreateApprovedFinanceJournalAsync();
        var post = SafeFinanceOperationAsync(() => scenario.FirstPersistence.PostJournalAsync(
            scenario.FirstContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-period-post", "sql-period-post", "sql-period-post")));
        var close = SafeFinanceOperationAsync(() => scenario.SecondPersistence.SetPeriodStateAsync(
            scenario.SecondContext,
            new FinancePeriodStateCommand(scenario.Period.Id, FinanceFiscalPeriodState.Closed, "SQL race close", scenario.Period.Version, "sql-period-close", "sql-period-close")));

        await Task.WhenAll(post, close);
        var posted = await post;
        var closed = await close;

        Assert.True(closed.Succeeded, closed.Code);
        Assert.True(posted.Succeeded || posted.Code is "period_closed" or "concurrency_conflict", posted.Code);
        await using var db = new FinanceDbContext(scenario.Options, scenario.FirstContext.TenantContext);
        var persistedPeriod = await db.FiscalPeriods.SingleAsync(item => item.Id == scenario.Period.Id);
        var persistedJournal = await db.Journals.SingleAsync(item => item.Id == scenario.ApprovedJournal.Id);
        Assert.Equal(FinanceFiscalPeriodState.Closed, persistedPeriod.State);
        if (posted.Succeeded)
        {
            Assert.Equal(FinanceJournalStatus.Posted, persistedJournal.Status);
            Assert.Equal(scenario.Period.Id, persistedJournal.FiscalPeriodId);
        }
        else
        {
            Assert.NotEqual(FinanceJournalStatus.Posted, persistedJournal.Status);
        }
    }

    [Fact]
    public async Task MESP132_sql_server_account_restriction_and_post_have_one_consistent_committed_order()
    {
        var scenario = await CreateApprovedFinanceJournalAsync(foreignCurrency: true);
        var restrictiveChange = SafeFinanceOperationAsync(() => scenario.SecondPersistence.EditAccountAsync(
            scenario.SecondContext,
            new FinanceAccountCommand(
                scenario.CompanyId,
                scenario.DebitAccount.Code,
                scenario.DebitAccount.EnglishName,
                scenario.DebitAccount.ArabicName,
                scenario.DebitAccount.ParentAccountId,
                scenario.DebitAccount.AccountType,
                scenario.DebitAccount.IsPostingAccount,
                FinanceCurrencyBehavior.FunctionalOnly,
                scenario.DebitAccount.EffectiveFrom,
                scenario.DebitAccount.EffectiveTo,
                scenario.DebitAccount.Id,
                scenario.DebitAccount.Version,
                "sql-account-restrict",
                "sql-account-restrict")));
        var post = SafeFinanceOperationAsync(() => scenario.FirstPersistence.PostJournalAsync(
            scenario.FirstContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-account-post", "sql-account-post", "sql-account-post")));

        await Task.WhenAll(post, restrictiveChange);
        var posted = await post;
        var changed = await restrictiveChange;

        Assert.True(changed.Succeeded, changed.Code);
        Assert.True(posted.Succeeded || posted.Code is "account_currency_behavior_invalid" or "concurrency_conflict", posted.Code);
        await using var db = new FinanceDbContext(scenario.Options, scenario.FirstContext.TenantContext);
        var persistedAccount = await db.Accounts.SingleAsync(item => item.Id == scenario.DebitAccount.Id);
        var persistedJournal = await db.Journals.SingleAsync(item => item.Id == scenario.ApprovedJournal.Id);
        Assert.Equal(FinanceCurrencyBehavior.FunctionalOnly, persistedAccount.CurrencyBehavior);
        if (posted.Succeeded)
        {
            Assert.Equal(FinanceJournalStatus.Posted, persistedJournal.Status);
            var postedDebitLine = await db.JournalLines.Where(item => item.JournalId == persistedJournal.Id).SingleAsync(item => item.Debit > 0m);
            Assert.Equal(37.50m, postedDebitLine.FunctionalDebit);
        }
        else
        {
            Assert.NotEqual(FinanceJournalStatus.Posted, persistedJournal.Status);
        }
    }

    [Fact]
    public async Task MESP132_sql_server_same_approved_journal_concurrent_post_has_one_authoritative_effect()
    {
        var scenario = await CreateApprovedFinanceJournalAsync();
        var first = SafeFinanceOperationAsync(() => scenario.FirstPersistence.PostJournalAsync(
            scenario.FirstContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-same-journal-a", "sql-same-journal-a", "sql-same-journal-a")));
        var second = SafeFinanceOperationAsync(() => scenario.SecondPersistence.PostJournalAsync(
            scenario.SecondContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-same-journal-b", "sql-same-journal-b", "sql-same-journal-b")));

        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "concurrency_conflict" or "source_effect_exists");

        await using var db = new FinanceDbContext(scenario.Options, scenario.FirstContext.TenantContext);
        Assert.Equal(FinanceJournalStatus.Posted, (await db.Journals.SingleAsync(item => item.Id == scenario.ApprovedJournal.Id)).Status);
        Assert.Equal(2, await db.JournalLines.CountAsync(item => item.JournalId == scenario.ApprovedJournal.Id));
        Assert.Equal(1, await db.AuditEvents.CountAsync(item => item.ResourceId == scenario.ApprovedJournal.Id && item.OperationId == "finance.journal.post" && item.Result == "Succeeded"));
    }

    [Fact]
    public async Task MESP132_sql_server_same_inventory_handoff_concurrent_processing_has_one_source_effect()
    {
        var connectionString = await GetConnectionStringAsync();
        var financeOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var inventoryOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.InventoryHistoryTable);
        var companyId = Guid.NewGuid();
        var inventoryContext = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        var tenantId = _fixture.TenantA.TenantId;
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        await using (var inventoryDb = new InventoryDbContext(inventoryOptions, _fixture.TenantA))
        {
            inventoryDb.StockMovements.Add(SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 1m, 25m, "SAR", "sql-finance-handoff-race"));
            await inventoryDb.SaveChangesAsync();
        }

        var inventory = new InventoryValuationPersistence(inventoryOptions, null, null, null);
        var policy = await inventory.CreatePolicyAsync(inventoryContext, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 1, 1)), inventoryContext.ActorId, DateTimeOffset.UtcNow, "sql-finance-handoff-policy", "sql-finance-handoff-policy", "sql-finance-handoff-policy"));
        Assert.True(policy.Succeeded, policy.Code);
        var processed = await inventory.ProcessAsync(inventoryContext, new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, inventoryContext.ActorId, DateTimeOffset.UtcNow, "sql-finance-handoff-process", "sql-finance-handoff-process", "sql-finance-handoff-process"));
        Assert.True(processed.Succeeded, processed.Code);
        var handoff = Assert.Single(await inventory.ListFinanceHandoffsAsync(inventoryContext, new InventoryValuationQuery(companyId)));
        Assert.Equal(InventoryFinanceValuationHandoffStatus.ReadyForFinance, handoff.Status);

        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenantId.Value, companyId, "SQL Handoff Company", "SAR")]);
        var approval = new SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement.NotRequired);
        var first = new FinancePersistence(financeOptions, provider, inventory, new UnavailableMasterDataExchangeRatePersistence(), approval);
        var second = new FinancePersistence(financeOptions, provider, inventory, new UnavailableMasterDataExchangeRatePersistence(), approval);
        var firstContext = FinanceContext("tenant.finance.handoff.process");
        var secondContext = FinanceContext("tenant.finance.handoff.process");
        var accounts = await CreateFinanceAccountsAsync(first, firstContext, companyId, "sql-handoff");
        var calendar = await first.CreateCalendarAsync(firstContext, new FinanceFiscalCalendarCommand(companyId, "SQL Handoff FY", Guid.NewGuid(), "sql-handoff-calendar", "sql-handoff-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await first.CreateYearAsync(firstContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-handoff-year", "sql-handoff-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await first.CreatePeriodAsync(firstContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026", "2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-handoff-period", "sql-handoff-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await first.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "sql-handoff-open", "sql-handoff-open"));
        Assert.True(opened.Succeeded, opened.Code);
        var rule = await first.CreatePostingRuleAsync(firstContext, new FinancePostingRuleCommand(companyId, "inventory-valuation-finance.v1", "OpeningBalance:Inbound", accounts.Debit.Id, accounts.Credit.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "sql-handoff-rule", "sql-handoff-rule"));
        Assert.True(rule.Succeeded, rule.Code);

        var firstProcess = SafeFinanceOperationAsync(() => first.ProcessHandoffAsync(firstContext, new FinanceHandoffProcessCommand(handoff.Id, "sql-handoff-process-a", "sql-handoff-process-a")));
        var secondProcess = SafeFinanceOperationAsync(() => second.ProcessHandoffAsync(secondContext, new FinanceHandoffProcessCommand(handoff.Id, "sql-handoff-process-b", "sql-handoff-process-b")));
        var results = await Task.WhenAll(firstProcess, secondProcess);

        Assert.Contains(results, item => item.Succeeded);
        Assert.All(results, item => Assert.True(item.Succeeded || item.Code is "concurrency_conflict" or "source_effect_exists" or "finance_conflict", item.Code));
        await using var financeDb = new FinanceDbContext(financeOptions, _fixture.TenantA);
        Assert.Equal(1, await financeDb.Journals.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "inventory-valuation-finance.v1"));
        Assert.Equal(1, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "inventory-valuation-finance.v1"));
        var journalId = await financeDb.Journals.Where(journal => journal.CompanyId == companyId && journal.SourceContract == "inventory-valuation-finance.v1").Select(journal => journal.Id).SingleAsync();
        Assert.Equal(2, await financeDb.JournalLines.CountAsync(item => item.JournalId == journalId));
        Assert.Equal(1, await financeDb.AuditEvents.CountAsync(item => item.OperationId == "finance.journal.post" && item.Result == "Succeeded" && item.ResourceId == journalId));
    }

    [Fact]
    public async Task MESP132_sql_server_first_company_journal_sequence_concurrency_is_unique_or_safe_conflict()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var companyId = Guid.NewGuid();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL Sequence Company", "SAR")]);
        var first = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var second = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var contextA = FinanceContext("tenant.finance.journal.create");
        var contextB = FinanceContext("tenant.finance.journal.create");
        var accounts = await CreateFinanceAccountsAsync(first, contextA, companyId, "sql-sequence");
        var calendar = await first.CreateCalendarAsync(contextA, new FinanceFiscalCalendarCommand(companyId, "SQL Sequence FY", Guid.NewGuid(), "sql-sequence-calendar", "sql-sequence-calendar"));
        var year = await first.CreateYearAsync(contextA, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-sequence-year", "sql-sequence-year"));
        var period = await first.CreatePeriodAsync(contextA, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "sql-sequence-period", "sql-sequence-period"));
        Assert.True((await first.SetPeriodStateAsync(contextA, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "sql-sequence-open", "sql-sequence-open"))).Succeeded);
        var commandA = SqlManualJournal(companyId, accounts, "sql-sequence-a");
        var commandB = SqlManualJournal(companyId, accounts, "sql-sequence-b");

        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => first.CreateJournalAsync(contextA, commandA)),
            SafeFinanceOperationAsync(() => second.CreateJournalAsync(contextB, commandB)));

        Assert.All(results, item => Assert.True(item.Succeeded || item.Code is "concurrency_conflict" or "idempotency_conflict"));
        var sequences = results.Where(item => item.Succeeded).Select(item => item.Value!.JournalSequence).ToArray();
        Assert.Equal(sequences.Length, sequences.Distinct().Count());
        await using var db = new FinanceDbContext(options, _fixture.TenantA);
        var persistedSequences = await db.Journals.Where(item => item.CompanyId == companyId).Select(item => item.JournalSequence).ToArrayAsync();
        Assert.Equal(persistedSequences.Length, persistedSequences.Distinct().Count());
    }

    [Fact]
    public async Task MESP138Hold4_sql_server_concurrent_credit_notes_on_same_allocation_never_both_commit()
    {
        var connectionString = await GetConnectionStringAsync();
        var financeOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var (companyId, customerId, invoiceId, openItemId, allocationId, orderLineId, returnId) = await SeedCustomerReturnFixtureAsync(financeOptions, acceptedQuantity: 1m, netAmount: 100m);
        var sales = new CustomerReturnFinanceSourceSpy(SqlHoldQSource(returnId, companyId, customerId, invoiceId, openItemId, allocationId, orderLineId, 1m, 100m));
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL HOLD4 Company", "SAR")]);
        var first = new CustomerReturnFinancePersistence(financeOptions, sales, provider, new UnavailableMasterDataExchangeRatePersistence());
        var second = new CustomerReturnFinancePersistence(financeOptions, sales, provider, new UnavailableMasterDataExchangeRatePersistence());
        var firstContext = FinanceContext("tenant.finance.credit-note.create");
        var secondContext = FinanceContext("tenant.finance.credit-note.create");
        var firstRequest = new FinanceCreditNoteCreateRequest(returnId, new DateOnly(2026, 1, 20), null);
        var secondRequest = new FinanceCreditNoteCreateRequest(returnId, new DateOnly(2026, 1, 20), null);

        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => first.CreateAsync(firstContext, firstRequest, "sql-hold4-q1-a", "sql-hold4-q1-a")),
            SafeFinanceOperationAsync(() => second.CreateAsync(secondContext, secondRequest, "sql-hold4-q1-b", "sql-hold4-q1-b")));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "concurrency_conflict" or "credit_note_lines_invalid" or "credit_note_quantity_conflict");
        await using var db = new FinanceDbContext(financeOptions, _fixture.TenantA);
        var totalQuantity = await db.CreditNoteLines.Where(item => item.SourceAllocationId == allocationId).SumAsync(item => item.Quantity);
        Assert.True(totalQuantity <= 1m, $"cumulative credited quantity {totalQuantity} exceeded the authoritative allocation capacity of 1");
        Assert.Equal(1, await db.CreditNotes.CountAsync(item => item.SalesCustomerReturnId == returnId));
    }

    [Fact]
    public async Task MESP138Hold4_sql_server_concurrent_credit_notes_exceeding_remaining_capacity_never_both_commit()
    {
        var connectionString = await GetConnectionStringAsync();
        var financeOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var (companyId, customerId, invoiceId, openItemId, allocationId, orderLineId, returnId) = await SeedCustomerReturnFixtureAsync(financeOptions, acceptedQuantity: 10m, netAmount: 1000m);
        var sales = new CustomerReturnFinanceSourceSpy(SqlHoldQSource(returnId, companyId, customerId, invoiceId, openItemId, allocationId, orderLineId, 10m, 1000m));
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL HOLD4 Company", "SAR")]);
        var first = new CustomerReturnFinancePersistence(financeOptions, sales, provider, new UnavailableMasterDataExchangeRatePersistence());
        var second = new CustomerReturnFinancePersistence(financeOptions, sales, provider, new UnavailableMasterDataExchangeRatePersistence());
        var firstContext = FinanceContext("tenant.finance.credit-note.create");
        var secondContext = FinanceContext("tenant.finance.credit-note.create");
        var firstRequest = new FinanceCreditNoteCreateRequest(returnId, new DateOnly(2026, 1, 20), null, null, [new FinanceCreditNoteLineRequest(allocationId, 6m)]);
        var secondRequest = new FinanceCreditNoteCreateRequest(returnId, new DateOnly(2026, 1, 20), null, null, [new FinanceCreditNoteLineRequest(allocationId, 6m)]);

        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => first.CreateAsync(firstContext, firstRequest, "sql-hold4-q2-a", "sql-hold4-q2-a")),
            SafeFinanceOperationAsync(() => second.CreateAsync(secondContext, secondRequest, "sql-hold4-q2-b", "sql-hold4-q2-b")));

        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "concurrency_conflict" or "credit_note_quantity_conflict");
        await using var db = new FinanceDbContext(financeOptions, _fixture.TenantA);
        var totalQuantity = await db.CreditNoteLines.Where(item => item.SourceAllocationId == allocationId).SumAsync(item => item.Quantity);
        Assert.True(totalQuantity <= 10m, $"cumulative credited quantity {totalQuantity} exceeded the authoritative allocation capacity of 10");
        Assert.Equal(6m, totalQuantity);
    }

    private async Task<(Guid CompanyId, Guid CustomerId, Guid InvoiceId, Guid OpenItemId, Guid AllocationId, Guid OrderLineId, Guid ReturnId)> SeedCustomerReturnFixtureAsync(DbContextOptions financeOptions, decimal acceptedQuantity, decimal netAmount)
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var openItemId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        await using var db = new FinanceDbContext(financeOptions, _fixture.TenantA);
        var item = new FinanceOpenItemEntity(_fixture.TenantA.TenantId, openItemId, FinanceOpenItemKind.Receivable, companyId, null, customerId, "sales-invoice.v1", Guid.NewGuid(), 1, invoiceId, 1, "sql-hold4-invoice", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "SAR", netAmount, "SAR", netAmount, null, null, null, null, null, null, null, "sql-hold4-invoice");
        item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, Guid.NewGuid());
        db.OpenItems.Add(item);
        await db.SaveChangesAsync();
        return (companyId, customerId, invoiceId, openItemId, allocationId, orderLineId, returnId);
    }

    private static SalesCustomerReturnSourceRecord SqlHoldQSource(Guid returnId, Guid companyId, Guid customerId, Guid invoiceId, Guid openItemId, Guid allocationId, Guid orderLineId, decimal acceptedQuantity, decimal netAmount) => new(
        returnId, Guid.NewGuid(), Guid.NewGuid(), 1, Guid.Empty, companyId, null, customerId, Guid.NewGuid(), DateTimeOffset.UtcNow, invoiceId, openItemId, "SAR",
        [new(orderLineId, Guid.NewGuid(), "SQL-SKU", "SQL Product", Guid.NewGuid(), "EA", acceptedQuantity, 0m, acceptedQuantity, netAmount / acceptedQuantity, 0m, netAmount / acceptedQuantity, null, acceptedQuantity, null, null, null, acceptedQuantity, acceptedQuantity, acceptedQuantity, 0m, 0m, 0m, "Restockable", [], [], null)],
        SalesCustomerReturnStatus.Received, SalesCustomerReturnConsequence.CreditNote, [1],
        [new(allocationId, invoiceId, openItemId, Guid.NewGuid(), orderLineId, 1, acceptedQuantity, acceptedQuantity, acceptedQuantity, 0m, acceptedQuantity, netAmount, 0m, netAmount, "SAR", null, null, null, "sql-hold4-allocation", "sql-hold4-invoice")]);

    private sealed class CustomerReturnFinanceSourceSpy(SalesCustomerReturnSourceRecord source) : ISalesCustomerReturnSourceProvider
    {
        public Task<SalesCustomerReturnSourceRecord?> GetCustomerReturnSourceAsync(TenantContext context, Guid returnId, CancellationToken cancellationToken = default) => Task.FromResult<SalesCustomerReturnSourceRecord?>(returnId == source.ReturnSourceId ? source : null);
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext context, SalesCustomerReturnInventoryAcknowledgementCommand command, CancellationToken cancellationToken = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext context, SalesCustomerReturnInventoryFailureCommand command, CancellationToken cancellationToken = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext context, SalesCustomerReturnDownstreamReversalCommand command, CancellationToken cancellationToken = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
        public Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RegisterFinanceCreditNoteAsync(TenantContext context, SalesCustomerReturnFinanceEffectCommand command, CancellationToken cancellationToken = default) => Task.FromResult(SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("unused"));
    }

    [Fact]
    public async Task MESP131_sql_server_uses_durable_company_ledger_sequence_for_legacy_and_new_movement_shape()
    {
        var options = new DbContextOptionsBuilder().UseSqlServer(await GetConnectionStringAsync()).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                new InventoryStockMovementEntity(
                    tenantId, firstId, companyId, null, warehouseId, "WH-MWA", "MWA warehouse", productId,
                    "SKU-MWA", "MWA product", unitId, "EA", InventoryMovementDirection.Inbound, 10m, 10m, "SAR",
                    InventoryValuationStatus.Known, null, InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                    DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp131-first", DateTimeOffset.UtcNow),
                new InventoryStockMovementEntity(
                    tenantId, secondId, companyId, null, warehouseId, "WH-MWA", "MWA warehouse", productId,
                    "SKU-MWA", "MWA product", unitId, "EA", InventoryMovementDirection.Inbound, 5m, 20m, "SAR",
                    InventoryValuationStatus.Known, null, InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), actorId, "sql-mesp131-second", DateTimeOffset.UtcNow.AddDays(-10)));
            await db.SaveChangesAsync();
        }

        await using var connection = new SqlConnection(await GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [Id], [LedgerSequence]
            FROM [inventory].[StockLedgerMovements]
            WHERE [TenantId] = @tenantId AND [CompanyId] = @companyId
            ORDER BY [LedgerSequence];
            SELECT [NextSequence]
            FROM [inventory].[CompanyLedgerSequenceAnchors]
            WHERE [TenantId] = @tenantId AND [CompanyId] = @companyId;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId.Value);
        command.Parameters.AddWithValue("@companyId", companyId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(firstId, reader.GetGuid(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(secondId, reader.GetGuid(0));
        Assert.Equal(2L, reader.GetInt64(1));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(3L, reader.GetInt64(0));
    }

    [Fact]
    public async Task Shared_catalog_has_one_tenancy_owner_and_no_competing_inventory_tenant_table()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DB_NAME();
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'tenancy'
              AND TABLE_NAME = N'TenantOwnedRecords';
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'inventory'
              AND TABLE_NAME = N'TenantOwnedRecords';
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'inventory'
              AND TABLE_NAME = N'StockLedgerMovements';
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Matches("^MiniErpFoundation_[A-Za-z0-9_]+$", reader.GetString(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public async Task Query_filter_is_evaluated_for_each_sql_server_tenant_context()
    {
        var recordA = new TenantOwnedRecord(_fixture.TenantA.TenantId, $"sql-a-{Guid.NewGuid():N}");
        var recordB = new TenantOwnedRecord(_fixture.TenantB.TenantId, $"sql-b-{Guid.NewGuid():N}");
        await _fixture.AddAsync(_fixture.TenantA, recordA);
        await _fixture.AddAsync(_fixture.TenantB, recordB);

        var reads = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var session = _fixture.Factory.Create(_fixture.TenantB);
            var records = await session.Records.ListAsync();
            return (records, foreign: await session.Records.FindAsync(recordA.Id));
        }));

        Assert.All(reads, result =>
        {
            Assert.Single(result.records);
            Assert.Equal(recordB.Id, result.records[0].Id);
            Assert.Null(result.foreign);
        });
    }

    [Fact]
    public async Task Stored_owner_guard_denies_forged_sql_server_update_and_delete()
    {
        var foreign = new TenantOwnedRecord(_fixture.TenantB.TenantId, $"sql-forged-{Guid.NewGuid():N}");
        await _fixture.AddAsync(_fixture.TenantB, foreign);

        await using (var updateSession = _fixture.Factory.Create(_fixture.TenantA))
        {
            var typed = (TenantPersistenceSession)updateSession;
            var forged = AttachForgedRecord(typed, foreign.Id, _fixture.TenantA.TenantId);
            typed.DbContext.Entry(forged).State = EntityState.Modified;
            var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
                () => updateSession.SaveChangesAsync());
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
            Assert.Equal(TenantPersistenceOperation.Modify, exception.Operation);
        }

        await using (var deleteSession = _fixture.Factory.Create(_fixture.TenantA))
        {
            var typed = (TenantPersistenceSession)deleteSession;
            var forged = AttachForgedRecord(typed, foreign.Id, _fixture.TenantA.TenantId);
            typed.DbContext.Entry(forged).State = EntityState.Deleted;
            var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
                () => deleteSession.SaveChangesAsync());
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
            Assert.Equal(TenantPersistenceOperation.Delete, exception.Operation);
        }

        await using var tenantB = _fixture.Factory.Create(_fixture.TenantB);
        var persisted = await tenantB.Records.FindAsync(foreign.Id);
        Assert.NotNull(persisted);
        Assert.Equal(foreign.BusinessKey, persisted!.BusinessKey);
    }

    [Fact]
    public async Task Tenant_aware_unique_index_allows_cross_tenant_duplicate_and_rejects_same_tenant_duplicate()
    {
        var key = $"sql-unique-{Guid.NewGuid():N}";
        await _fixture.AddAsync(_fixture.TenantA, new TenantOwnedRecord(_fixture.TenantA.TenantId, key));
        await _fixture.AddAsync(_fixture.TenantB, new TenantOwnedRecord(_fixture.TenantB.TenantId, key));

        await using var duplicateSession = _fixture.Factory.Create(_fixture.TenantA);
        duplicateSession.Records.Add(new TenantOwnedRecord(_fixture.TenantA.TenantId, key));
        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => duplicateSession.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.PersistenceConflict, exception.Code);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("UNIQUE", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sql_server_rowversion_rejects_stale_update_and_delete()
    {
        var record = new TenantOwnedRecord(_fixture.TenantA.TenantId, $"sql-version-{Guid.NewGuid():N}");
        await _fixture.AddAsync(_fixture.TenantA, record);

        await using var first = _fixture.Factory.Create(_fixture.TenantA);
        await using var second = _fixture.Factory.Create(_fixture.TenantA);
        var firstRecord = await first.Records.FindAsync(record.Id);
        var secondRecord = await second.Records.FindAsync(record.Id);
        Assert.NotNull(firstRecord);
        Assert.NotNull(secondRecord);
        Assert.NotEmpty(firstRecord!.Version);
        Assert.Equal(firstRecord.Version, secondRecord!.Version);

        var firstSession = (TenantPersistenceSession)first;
        firstSession.DbContext.Entry(firstRecord).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
            $"sql-version-updated-{Guid.NewGuid():N}";
        Assert.Equal(1, await first.SaveChangesAsync());

        var secondSession = (TenantPersistenceSession)second;
        secondSession.DbContext.Entry(secondRecord).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
            $"sql-version-stale-{Guid.NewGuid():N}";
        var staleUpdate = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => second.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.PersistenceConflict, staleUpdate.Code);

        await using var staleDelete = _fixture.Factory.Create(_fixture.TenantA);
        var staleDeleteRecord = await staleDelete.Records.FindAsync(record.Id);
        Assert.NotNull(staleDeleteRecord);
        await using var current = _fixture.Factory.Create(_fixture.TenantA);
        var currentRecord = await current.Records.FindAsync(record.Id);
        Assert.NotNull(currentRecord);
        ((TenantPersistenceSession)current).DbContext.Entry(currentRecord!).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
            $"sql-version-current-{Guid.NewGuid():N}";
        await current.SaveChangesAsync();
        ((TenantPersistenceSession)staleDelete).DbContext.Entry(staleDeleteRecord!).State = EntityState.Deleted;
        var staleDeleteException = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => staleDelete.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.PersistenceConflict, staleDeleteException.Code);
    }

    [Fact]
    public async Task Inventory_sql_server_existing_anchor_touch_persists_a_real_update()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var anchorId = Guid.NewGuid();
        await using (var createDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            await createDb.Database.EnsureCreatedAsync();
            var anchor = CreateAnchor(_fixture.TenantA.TenantId, anchorId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            anchor.Touch();
            createDb.ConcurrencyAnchors.Add(anchor);
            await createDb.SaveChangesAsync();
        }

        long beforeSequence;
        byte[] beforeVersion;
        await using (var touchDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            var anchor = await touchDb.ConcurrencyAnchors.SingleAsync(item => item.Id == anchorId);
            beforeSequence = anchor.TouchSequence;
            beforeVersion = anchor.Version.ToArray();
            anchor.Touch();
            await touchDb.SaveChangesAsync();
        }

        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        var persisted = await verifyDb.ConcurrencyAnchors.AsNoTracking().SingleAsync(item => item.Id == anchorId);
        Assert.Equal(beforeSequence + 1, persisted.TouchSequence);
        Assert.NotEqual(beforeVersion, persisted.Version);
    }

    [Fact]
    public async Task Inventory_sql_server_branch_null_anchor_identity_is_database_unique_and_unfiltered()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        await using (var createDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            await createDb.Database.EnsureCreatedAsync();
            createDb.ConcurrencyAnchors.Add(CreateAnchor(_fixture.TenantA.TenantId, Guid.NewGuid(), companyId, warehouseId, productId, unitId));
            await createDb.SaveChangesAsync();
        }

        await using (var duplicateDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            duplicateDb.ConcurrencyAnchors.Add(CreateAnchor(_fixture.TenantA.TenantId, Guid.NewGuid(), companyId, warehouseId, productId, unitId));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
        }

        await using var connection = await _fixture.OpenConnectionAsync();
        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            SELECT filter_definition
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'[inventory].[ConcurrencyAnchors]')
              AND name = N'IX_ConcurrencyAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingKey';
            """;
        var filterDefinition = await indexCommand.ExecuteScalarAsync();
        Assert.True(
            filterDefinition is null
            || filterDefinition is DBNull
            || string.IsNullOrEmpty(filterDefinition.ToString()),
            $"The shared-catalog anchor uniqueness index must be unfiltered; actual filter: {filterDefinition ?? "<null>"}.");
    }

    [Fact]
    public async Task Inventory_sql_server_transfer_receipt_reference_case_variants_converge_without_second_movement()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString, sql => sql.CommandTimeout(15)).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var sourceWarehouseId = Guid.NewGuid();
        var destinationWarehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var source = new InventoryWarehouseOption(tenantId.Value, companyId, null, sourceWarehouseId, "SQL-SOURCE", "SQL source");
        var destination = new InventoryWarehouseOption(tenantId.Value, companyId, null, destinationWarehouseId, "SQL-DEST", "SQL destination");
        var product = new InventoryProductReference(tenantId.Value, productId, "SQL-RECEIPT", "SQL receipt product", unitId, "EA", true, true, false);
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.transfer.receive");

        await using (var seedDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId,
                Guid.NewGuid(),
                companyId,
                null,
                sourceWarehouseId,
                source.Code,
                source.Name,
                productId,
                product.Sku,
                product.Name,
                unitId,
                product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound,
                10m,
                1m,
                "SAR",
                null,
                InventoryMovementSourceType.OpeningBalance,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                new DateOnly(2026, 8, 22),
                Guid.NewGuid(),
                "sql-transfer-receipt-seed",
                DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var persistence = new InventoryPersistence(options);
        var created = Assert.IsType<InventoryTransferRecord>(await persistence.CreateTransferAsync(
            context,
            new InventoryTransferCreateCommand(
                Guid.NewGuid(),
                new InventoryScope(tenantId.Value, companyId, null, sourceWarehouseId),
                source,
                destination,
                productId,
                unitId,
                product,
                10m,
                InventoryTransferMode.InTransit,
                null,
                "SQL case-variant receipt",
                context.ActorId,
                DateTimeOffset.UtcNow,
                "sql-transfer-create",
                "sql-transfer-create-key",
                "sql-transfer-create-fingerprint")));
        var shipped = Assert.IsType<InventoryTransferRecord>(await persistence.ShipTransferAsync(
            context,
            new InventoryTransferActionCommand(created.Id, created.Version, null, null, "ship", context.ActorId, DateTimeOffset.UtcNow, "sql-transfer-ship", "sql-transfer-ship-key", "sql-transfer-ship-fingerprint")));
        var firstReceipt = Assert.IsType<InventoryTransferRecord>(await persistence.ReceiveTransferAsync(
            context,
            new InventoryTransferActionCommand(shipped.Id, shipped.Version, 4m, "RECEIVE-001", "receive", context.ActorId, DateTimeOffset.UtcNow, "sql-transfer-receive-a", "sql-transfer-receive-a-key", "sql-transfer-receive-a-fingerprint")));
        var duplicateReceipt = Assert.IsType<InventoryTransferRecord>(await persistence.ReceiveTransferAsync(
            context,
            new InventoryTransferActionCommand(firstReceipt.Id, firstReceipt.Version, 4m, "receive-001", "duplicate receive", context.ActorId, DateTimeOffset.UtcNow, "sql-transfer-receive-b", "sql-transfer-receive-b-key", "sql-transfer-receive-b-fingerprint")));

        Assert.Equal(4m, duplicateReceipt.ReceivedQuantity);
        Assert.Equal(6m, duplicateReceipt.InTransitQuantity);
        var movements = await persistence.ListMovementsAsync(context, null);
        Assert.Single(movements, item => item.SourceType == InventoryMovementSourceType.WarehouseTransferReceipt);
        var history = await persistence.ReadTransferHistoryAsync(context, created.Id);
        Assert.Equal("RECEIVE-001", Assert.Single(history, item => item.EventType == InventoryTransferEventType.Received).Reference);
        var audit = await persistence.ReadAuditAsync(context, "transfer", created.Id);
        Assert.Contains(audit, item => item.Decision == "Duplicate" && item.AfterSummary == "duplicate-receipt-reference");
    }

    [Fact]
    public async Task Inventory_sql_server_contention_is_classified_as_conflict_without_reservation_effect()
    {
        var connectionString = await GetConnectionStringAsync();
        var seedOptions = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, Guid.NewGuid(), null, warehouseId, "WH-CONTENTION", "Contention warehouse");
        var product = new InventoryProductReference(tenantId.Value, productId, "SKU-CONTENTION", "Contention product", unitId, "EA", true, true, false);

        await using (var seedDb = new InventoryDbContext(seedOptions, _fixture.TenantA))
        {
            await seedDb.Database.EnsureCreatedAsync();
            var anchor = CreateAnchor(tenantId, anchorId, warehouse.CompanyId, warehouseId, productId, unitId);
            anchor.Touch();
            seedDb.ConcurrencyAnchors.Add(anchor);
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId,
                Guid.NewGuid(),
                warehouse.CompanyId,
                warehouse.BranchId,
                warehouse.WarehouseId,
                warehouse.Code,
                warehouse.Name,
                product.ProductId,
                product.Sku,
                product.Name,
                product.BaseUnitOfMeasureId,
                product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound,
                10m,
                1m,
                "SAR",
                null,
                InventoryMovementSourceType.OpeningBalance,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                new DateOnly(2026, 8, 22),
                Guid.NewGuid(),
                "sql-contention-seed",
                DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var gate = new FirstInventoryConnectionClosedGate();
        var operationOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(5))
            .AddInterceptors(gate)
            .Options;
        var inventoryContext = InventoryContext(_fixture.TenantA, "tenant.inventory.reservation.create");
        var service = new InventoryService(
            new InventoryPersistence(operationOptions),
            new InventoryResourceAuthorizationService(),
            new ConfiguredInventoryWarehouseProvider([warehouse]),
            new StaticInventoryProductProvider(product));
        var request = new InventoryReservationCreateRequest(
            warehouse.CompanyId,
            warehouse.BranchId,
            warehouse.WarehouseId,
            product.ProductId,
            product.BaseUnitOfMeasureId,
            7m,
            "Demand",
            "SQL-CONTENTION-DEMAND",
            true,
            null);

        var operation = service.CreateReservationAsync(inventoryContext, request, "sql-contention-key");
        await gate.WaitAsync();

        await using var blocker = await _fixture.OpenInventoryConnectionAsync();
        await using var transaction = (SqlTransaction)await blocker.BeginTransactionAsync(IsolationLevel.Serializable);
        await using (var lockCommand = blocker.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                SELECT [TouchSequence]
                FROM [inventory].[ConcurrencyAnchors] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = @anchorId;
                """;
            lockCommand.Parameters.AddWithValue("@anchorId", anchorId);
            Assert.NotNull(await lockCommand.ExecuteScalarAsync());
        }

        var result = await operation;
        Assert.False(result.Succeeded);
        Assert.Equal("conflict", result.Code);

        await transaction.RollbackAsync();
        var persistence = new InventoryPersistence(seedOptions);
        var reservations = await persistence.ListReservationsAsync(inventoryContext, new InventoryScope(tenantId.Value, warehouse.CompanyId, warehouse.BranchId, warehouse.WarehouseId));
        Assert.Empty(reservations);
    }

    [Fact]
    public async Task Inventory_sql_server_overlapping_reservations_cannot_over_allocate()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString, sql => sql.CommandTimeout(15)).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, companyId, null, warehouseId, "WH-RESERVATION", "Reservation warehouse");
        var product = new InventoryProductReference(tenantId.Value, productId, "SKU-RESERVATION", "Reservation product", unitId, "EA", true, true, false);
        await using (var seedDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            await seedDb.Database.EnsureCreatedAsync();
            var anchor = CreateAnchor(tenantId, Guid.NewGuid(), companyId, warehouseId, productId, unitId);
            anchor.Touch();
            seedDb.ConcurrencyAnchors.Add(anchor);
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId,
                Guid.NewGuid(),
                companyId,
                null,
                warehouseId,
                warehouse.Code,
                warehouse.Name,
                productId,
                product.Sku,
                product.Name,
                unitId,
                product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound,
                10m,
                1m,
                "SAR",
                null,
                InventoryMovementSourceType.OpeningBalance,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                new DateOnly(2026, 8, 22),
                Guid.NewGuid(),
                "sql-reservation-seed",
                DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var context = InventoryContext(_fixture.TenantA);
        using var barrier = new Barrier(2);
        var firstPersistence = new InventoryPersistence(options);
        var secondPersistence = new InventoryPersistence(options);
        var first = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await firstPersistence.CreateReservationAsync(context, ReservationCommand(tenantId.Value, companyId, warehouseId, productId, unitId, warehouse, product, "SQL-RESERVATION-1"), 10m);
        });
        var second = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await secondPersistence.CreateReservationAsync(context, ReservationCommand(tenantId.Value, companyId, warehouseId, productId, unitId, warehouse, product, "SQL-RESERVATION-2"), 10m);
        });

        var results = await Task.WhenAll(first, second);
        var reservations = await firstPersistence.ListReservationsAsync(context, new InventoryScope(tenantId.Value, companyId, null, warehouseId));
        Assert.NotEmpty(reservations);
        Assert.True(reservations.Sum(item => item.ReservedQuantity) <= 10m);
        Assert.Contains(
            reservations.Sum(item => item.ReservedQuantity) + reservations.Sum(item => item.UnallocatedQuantity),
            new[] { 7m, 14m });
        Assert.Contains(results, item => item is not null);
    }

    [Fact]
    public async Task Same_tenant_relationship_is_allowed_and_cross_tenant_relationship_is_denied()
    {
        var allowed = new TenantOwnedRecord(
            _fixture.TenantA.TenantId,
            $"sql-related-{Guid.NewGuid():N}",
            TenantRelationshipKind.CompanyBranch,
            _fixture.TenantA.TenantId);
        await _fixture.AddAsync(_fixture.TenantA, allowed);

        await using var session = _fixture.Factory.Create(_fixture.TenantA);
        session.Records.Add(new TenantOwnedRecord(
            _fixture.TenantA.TenantId,
            $"sql-cross-related-{Guid.NewGuid():N}",
            TenantRelationshipKind.BranchWarehouse,
            _fixture.TenantB.TenantId));
        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.RelationshipMismatch, exception.Code);
    }

    [Theory]
    [InlineData(TenantRelationshipKind.CompanyBranch)]
    [InlineData(TenantRelationshipKind.BranchWarehouse)]
    [InlineData(TenantRelationshipKind.CompanyDepartment)]
    public async Task Same_tenant_composite_relationship_is_allowed_on_sql_server(TenantRelationshipKind relationshipKind)
    {
        var allowed = new TenantOwnedRecord(
            _fixture.TenantA.TenantId,
            $"sql-related-{relationshipKind}-{Guid.NewGuid():N}",
            relationshipKind,
            _fixture.TenantA.TenantId);
        await _fixture.AddAsync(_fixture.TenantA, allowed);

        await using var session = _fixture.Factory.Create(_fixture.TenantA);
        Assert.NotNull(await session.Records.FindAsync(allowed.Id));
    }

    [Fact]
    public async Task Sql_server_schema_has_tenant_business_unique_index_and_rowversion()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            SELECT COUNT(*)
            FROM
            (
                SELECT i.index_id
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic
                    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                INNER JOIN sys.columns AS c
                    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID(N'[tenancy].[TenantOwnedRecords]')
                  AND i.is_unique = 1
                  AND ic.is_included_column = 0
                GROUP BY i.index_id
                HAVING COUNT(*) = 2
                   AND SUM(CASE WHEN c.name = N'TenantId' THEN 1 ELSE 0 END) = 1
                   AND SUM(CASE WHEN c.name = N'BusinessKey' THEN 1 ELSE 0 END) = 1
            ) AS matching_indexes;
            """;
        Assert.Equal(1, Convert.ToInt32(await indexCommand.ExecuteScalarAsync()));

        await using var rowVersionCommand = connection.CreateCommand();
        rowVersionCommand.CommandText = """
            SELECT TYPE_NAME(c.user_type_id)
            FROM sys.columns AS c
            WHERE c.object_id = OBJECT_ID(N'[tenancy].[TenantOwnedRecords]')
              AND c.name = N'Version';
            """;
        Assert.Equal("timestamp", (string?)await rowVersionCommand.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Sql_server_collation_is_recorded_and_unicode_identifier_round_trips()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var collationCommand = connection.CreateCommand();
        collationCommand.CommandText = "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128));";
        var collation = (string?)await collationCommand.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(collation));

        var arabicKey = $"شركة-{Guid.NewGuid():N}";
        await _fixture.AddAsync(_fixture.TenantA, new TenantOwnedRecord(_fixture.TenantA.TenantId, arabicKey));
        await using var session = _fixture.Factory.Create(_fixture.TenantA);
        var records = await session.Records.ListAsync();
        Assert.Contains(records, item => item.BusinessKey == arabicKey);
    }

    [Fact]
    public async Task Durable_work_probe_transaction_rolls_back_work_and_outbox_together()
    {
        var tenantId = _fixture.TenantA.TenantId.Value;
        var workId = Guid.NewGuid();
        var workEventId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();

        await using var connection = await _fixture.OpenConnectionAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        await ExecuteProbeInsertAsync(connection, transaction, workId, tenantId, workEventId, "work");
        await ExecuteProbeInsertAsync(connection, transaction, outboxId, tenantId, outboxId, "outbox");
        await transaction.RollbackAsync();

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM [test].[DurableWorkProbe] WHERE [WorkId] IN (@workId, @outboxId);";
        countCommand.Parameters.AddWithValue("@workId", workId);
        countCommand.Parameters.AddWithValue("@outboxId", outboxId);
        Assert.Equal(0, Convert.ToInt32(await countCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Durable_work_probe_idempotency_is_tenant_scoped()
    {
        var eventId = Guid.NewGuid();
        var tenantA = _fixture.TenantA.TenantId.Value;
        var tenantB = _fixture.TenantB.TenantId.Value;
        await using var connection = await _fixture.OpenConnectionAsync();

        await ExecuteProbeInsertAsync(connection, transaction: null, Guid.NewGuid(), tenantA, eventId, "outbox");
        await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteProbeInsertAsync(connection, transaction: null, Guid.NewGuid(), tenantA, eventId, "outbox"));
        await ExecuteProbeInsertAsync(connection, transaction: null, Guid.NewGuid(), tenantB, eventId, "outbox");
    }

    [Fact]
    public async Task Durable_work_probe_claim_is_single_owner_and_optimistic()
    {
        var tenantId = _fixture.TenantA.TenantId.Value;
        var workId = Guid.NewGuid();
        await using var connection = await _fixture.OpenConnectionAsync();
        await ExecuteProbeInsertAsync(connection, transaction: null, workId, tenantId, Guid.NewGuid(), "work");

        await using var first = connection.CreateCommand();
        first.CommandText = """
            UPDATE [test].[DurableWorkProbe]
            SET [LeaseOwner] = @owner, [LeaseVersion] = [LeaseVersion] + 1, [State] = N'Leased'
            WHERE [WorkId] = @workId AND [State] = N'Pending' AND [LeaseOwner] IS NULL;
            """;
        first.Parameters.AddWithValue("@owner", "worker-a");
        first.Parameters.AddWithValue("@workId", workId);
        Assert.Equal(1, await first.ExecuteNonQueryAsync());

        await using var second = connection.CreateCommand();
        second.CommandText = first.CommandText;
        second.Parameters.AddWithValue("@owner", "worker-b");
        second.Parameters.AddWithValue("@workId", workId);
        Assert.Equal(0, await second.ExecuteNonQueryAsync());
    }

    [Theory]
    [InlineData(null, "a null connection string")]
    [InlineData("", "an empty connection string")]
    [InlineData(
        @"Server=tcp:prod-sql.example.com,1433;Database=MiniErpFoundation_x;Integrated Security=True;",
        "a non-LocalDB Server/DataSource")]
    [InlineData(
        @"Server=(localdb)\MSSQLLocalDB;Database=ProductionCustomers;Integrated Security=True;",
        "an InitialCatalog outside the disposable MiniErpFoundation_ prefix")]
    [InlineData(
        @"Server=(localdb)\MSSQLLocalDB;Database=MiniErpFoundation;Integrated Security=True;",
        "an InitialCatalog equal to the prefix with no disposable suffix")]
    [InlineData(
        @"Server=(localdb)\MSSQLLocalDB;Database=MiniErpFoundation_evil.database;Integrated Security=True;",
        "an InitialCatalog containing characters outside the disposable pattern")]
    public void Unsafe_sql_server_configuration_is_rejected_by_the_real_validator(
        string? unsafeConnectionString,
        string reason)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(unsafeConnectionString));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), $"Expected a safe-closed rejection for {reason}.");
    }

    [Fact]
    public void Safe_sql_server_configuration_passes_the_real_validator()
    {
        var builder = SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(
            @"Server=(localdb)\MSSQLLocalDB;Database=MiniErpFoundation_selftest01;Integrated Security=True;");

        Assert.Equal(@"(localdb)\MSSQLLocalDB", builder.DataSource);
        Assert.StartsWith("MiniErpFoundation_", builder.InitialCatalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_accepted_connection_string_is_the_one_the_real_validator_approved()
    {
        // Confirms the dedicated safety variable (not the runtime variable) is what the fixture uses,
        // and that the injected value actually passes the validator's LocalDB + MiniErpFoundation_* checks.
        var rawConnectionString = Environment.GetEnvironmentVariable("MESP_SQLSERVER_SAFETY_CONNECTION_STRING");
        var builder = SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(rawConnectionString);

        Assert.Equal(@"(localdb)\MSSQLLocalDB", builder.DataSource);
        Assert.StartsWith("MiniErpFoundation_", builder.InitialCatalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_connection_string_is_not_accepted_as_safety_configuration()
    {
        // Proves that the persistent MESP runtime variable cannot authorize destructive
        // safety-test execution even if its value happens to be set in the test process.
        // The safety harness must never fall back to the runtime connection.
        //
        // This test verifies the architectural boundary: MESP_SQLSERVER_CONNECTION_STRING
        // is for the persistent MESP application runtime; MESP_SQLSERVER_SAFETY_CONNECTION_STRING
        // is exclusively for the disposable LocalDB safety harness.
        var runtimeConnectionString = Environment.GetEnvironmentVariable("MESP_SQLSERVER_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(runtimeConnectionString))
        {
            // No runtime variable set — the validator would already reject null/empty;
            // this is already covered by the Unsafe_sql_server_configuration_is_rejected theory.
            return;
        }

        // If a runtime variable IS set, it must point at server '.' / database MESP.
        // That connection must always be rejected by the safety validator because:
        // (a) it does not target (localdb)\MSSQLLocalDB, or
        // (b) its database name does not start with MiniErpFoundation_.
        // Either condition alone is sufficient for rejection.
        Assert.Throws<InvalidOperationException>(
            () => SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(runtimeConnectionString));
    }

    private async Task<string> GetConnectionStringAsync()
    {
        await using var connection = await _fixture.OpenInventoryConnectionAsync();
        return connection.ConnectionString;
    }

    private static InventoryConcurrencyAnchorEntity CreateAnchor(
        TenantId tenantId,
        Guid id,
        Guid companyId = default,
        Guid warehouseId = default,
        Guid productId = default,
        Guid unitOfMeasureId = default)
    {
        return new InventoryConcurrencyAnchorEntity(
            tenantId,
            id,
            companyId == Guid.Empty ? Guid.Parse("11111111-1111-1111-1111-111111111111") : companyId,
            null,
            warehouseId == Guid.Empty ? Guid.Parse("cccccccc-1111-1111-1111-111111111111") : warehouseId,
            productId == Guid.Empty ? Guid.Parse("77777777-7777-7777-7777-777777777777") : productId,
            unitOfMeasureId == Guid.Empty ? Guid.Parse("88888888-8888-8888-8888-888888888888") : unitOfMeasureId,
            string.Empty);
    }

    private static InventoryRequestContext InventoryContext(
        TenantContext tenantContext,
        string permission = "tenant.inventory.ledger.view") =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                tenantContext,
                permission)).Context!;

    private static InventoryReservationCommand ReservationCommand(
        Guid tenantId,
        Guid companyId,
        Guid warehouseId,
        Guid productId,
        Guid unitId,
        InventoryWarehouseOption warehouse,
        InventoryProductReference product,
        string sourceReference) => new(
        Guid.NewGuid(),
        new InventoryScope(tenantId, companyId, warehouse.BranchId, warehouseId),
        productId,
        unitId,
        7m,
        "Demand",
        sourceReference,
        true,
        null,
        product,
        warehouse.Code,
        warehouse.Name,
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        DateTimeOffset.UtcNow,
        $"sql-reservation-{Guid.NewGuid():N}",
        $"sql-reservation-key-{Guid.NewGuid():N}",
        $"sql-reservation-fingerprint-{Guid.NewGuid():N}");

    private sealed class StaticInventoryProductProvider(InventoryProductReference product) : IInventoryProductProvider
    {
        public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryProductReference?>(product.TenantId == context.TenantId.Value && product.ProductId == productId ? product : null);
    }

    private sealed class FirstInventoryConnectionClosedGate : DbConnectionInterceptor
    {
        private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SET LOCK_TIMEOUT 1000;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (Interlocked.Exchange(ref signaled, 1) == 0)
            {
                completion.TrySetResult(true);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FullCountLedgerQueryGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        internal void Release() => release.TrySetResult(true);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[StockLedgerMovements]", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("[ProductSku]", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Exchange(ref signaled, 1) == 0)
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }

            return result;
        }
    }

    private sealed class SelectedIdentityLedgerCountGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        internal void Release() => release.TrySetResult(true);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[StockLedgerMovements]", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("COUNT_BIG", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref signaled, 1) == 0)
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }
    }

    private sealed class StockMovementInsertAttemptGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SignalIfMovementInsert(command);

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            SignalIfMovementInsert(command);
            return ValueTask.FromResult(result);
        }

        private void SignalIfMovementInsert(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO [inventory].[StockLedgerMovements]", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref signaled, 1) == 0)
            {
                entered.TrySetResult(true);
            }
        }
    }

    [Fact]
    public async Task MESP130_sql_server_full_count_boundary_blocks_insert_after_authoritative_read_and_uses_ledger_fence()
    {
        var connectionString = await GetConnectionStringAsync();
        var seedOptions = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productAId = Guid.NewGuid();
        var unitAId = Guid.NewGuid();
        var productBId = Guid.NewGuid();
        var unitBId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, companyId, null, warehouseId, "WH-FULL-RACE", "Full Count race warehouse");
        var productA = new InventoryProductReference(tenantId.Value, productAId, "SKU-FULL-A", "Full Count product A", unitAId, "EA", true, true, false);
        var productB = new InventoryProductReference(tenantId.Value, productBId, "SKU-FULL-B", "Full Count product B", unitBId, "EA", true, true, false);
        var context = InventoryContext(_fixture.TenantA);
        var scope = new InventoryScope(tenantId.Value, companyId, null, warehouseId);

        await using (var seedDb = new InventoryDbContext(seedOptions, _fixture.TenantA))
        {
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productAId, productA.Sku, productA.Name, unitAId, unitAId == Guid.Empty ? "EA" : productA.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 5m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), context.ActorId, "sql-full-race-seed-a", DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var gate = new FullCountLedgerQueryGate();
        var operationOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(gate)
            .Options;
        var persistence = new InventoryPersistence(operationOptions);
        var createTask = persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                Guid.NewGuid(), scope, warehouse.Code, warehouse.Name, InventoryCountType.Full, context.ActorId, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, productAId, unitAId, "", 0m, productA)],
                DateTimeOffset.UtcNow, context.ActorId, DateTimeOffset.UtcNow, "sql-full-race-create", "sql-full-race-key", "sql-full-race-fingerprint"));
        await gate.WaitAsync();

        var movementGate = new StockMovementInsertAttemptGate();
        var movementOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(movementGate)
            .Options;
        var movementPostedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var movementTask = Task.Run(async () =>
        {
            await using var movementDb = new InventoryDbContext(movementOptions, _fixture.TenantA);
            movementDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productBId, productB.Sku, productB.Name, unitBId, productB.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 3m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(movementPostedAt.UtcDateTime), context.ActorId, "sql-full-race-boundary-b", movementPostedAt));
            await movementDb.SaveChangesAsync();
        });
        await movementGate.WaitAsync();
        Assert.False(movementTask.IsCompleted);
        gate.Release();
        var created = Assert.IsType<InventoryCountRecord>(await createTask.WaitAsync(TimeSpan.FromSeconds(30)));
        await movementTask.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.DoesNotContain(created.Lines, item => item.ProductId == productBId);
        Assert.True(movementPostedAt < created.SnapshotCutoff);

        var persistenceAfterBoundary = new InventoryPersistence(seedOptions);
        var submitted = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(
                created.Id, created.Version,
                created.Lines.Select(item => new InventoryCountObservationRequest(item.Id, item.ExpectedQuantity ?? 0m)).ToArray(),
                context.ActorId, "sql-full-race-submit", "sql-full-race-submit-fingerprint", "sql-full-race-submit-correlation", DateTimeOffset.UtcNow)));
        var post = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.PostCountAsync(
            context,
            new InventoryControlActionCommand(
                submitted.Id, submitted.Version, context.ActorId, "sql full race", null, "sql-full-race-post-correlation", "sql-full-race-post", "sql-full-race-post-fingerprint", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, post.Status);
    }

    [Fact]
    public async Task MESP130_sql_server_cycle_count_boundary_blocks_selected_insert_after_fence_read()
    {
        var connectionString = await GetConnectionStringAsync();
        var seedOptions = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, companyId, null, warehouseId, "WH-CYCLE-FENCE", "Cycle Count fence warehouse");
        var product = new InventoryProductReference(tenantId.Value, productId, "SKU-CYCLE-FENCE", "Cycle Count fence product", unitId, "EA", true, true, false);
        var context = InventoryContext(_fixture.TenantA);
        var scope = new InventoryScope(tenantId.Value, companyId, null, warehouseId);

        await using (var seedDb = new InventoryDbContext(seedOptions, _fixture.TenantA))
        {
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productId, product.Sku, product.Name, unitId, product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 10m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), context.ActorId, "sql-cycle-fence-seed", DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var fenceGate = new SelectedIdentityLedgerCountGate();
        var operationOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(fenceGate)
            .Options;
        var persistence = new InventoryPersistence(operationOptions);
        var createTask = persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                Guid.NewGuid(), scope, warehouse.Code, warehouse.Name, InventoryCountType.Cycle, context.ActorId, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, productId, unitId, "", 0m, product)],
                DateTimeOffset.UtcNow, context.ActorId, DateTimeOffset.UtcNow, "sql-cycle-fence-create", "sql-cycle-fence-key", "sql-cycle-fence-fingerprint"));
        await fenceGate.WaitAsync();

        var movementGate = new StockMovementInsertAttemptGate();
        var movementOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(movementGate)
            .Options;
        var movementPostedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var movementTask = Task.Run(async () =>
        {
            await using var movementDb = new InventoryDbContext(movementOptions, _fixture.TenantA);
            movementDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productId, product.Sku, product.Name, unitId, product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 1m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(movementPostedAt.UtcDateTime), context.ActorId, "sql-cycle-fence-movement", movementPostedAt));
            await movementDb.SaveChangesAsync();
        });
        await movementGate.WaitAsync();
        Assert.False(movementTask.IsCompleted);
        fenceGate.Release();
        var created = Assert.IsType<InventoryCountRecord>(await createTask.WaitAsync(TimeSpan.FromSeconds(30)));
        await movementTask.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(movementPostedAt < created.SnapshotCutoff);

        var persistenceAfterBoundary = new InventoryPersistence(seedOptions);
        var line = Assert.Single(created.Lines);
        var submitted = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(
                created.Id, created.Version,
                [new InventoryCountObservationRequest(line.Id, 10m)],
                context.ActorId, "sql-cycle-fence-submit", "sql-cycle-fence-submit-fingerprint", "sql-cycle-fence-submit-correlation", DateTimeOffset.UtcNow)));
        var post = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.PostCountAsync(
            context,
            new InventoryControlActionCommand(
                submitted.Id, submitted.Version, context.ActorId, "sql cycle fence", null, "sql-cycle-fence-post-correlation", "sql-cycle-fence-post", "sql-cycle-fence-post-fingerprint", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, post.Status);
    }

    [Fact]
    public async Task MESP130_sql_server_correction_index_allows_one_direct_correction_only()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var originalId = Guid.NewGuid();
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using (var createDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            createDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, originalId, companyId, null, warehouseId, "WH-SQL", "SQL warehouse", productId,
                "SKU-SQL", "SQL product", unitId, "EA", InventoryMovementDirection.Inbound, 1m, null, null,
                InventoryValuationStatus.Pending, null, InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp130-original", DateTimeOffset.UtcNow));
            await createDb.SaveChangesAsync();
        }

        var correction = new InventoryStockMovementEntity(
            tenantId, Guid.NewGuid(), companyId, null, warehouseId, "WH-SQL", "SQL warehouse", productId,
            "SKU-SQL", "SQL product", unitId, "EA", InventoryMovementDirection.Outbound, 1m, null, null,
            InventoryValuationStatus.Pending, null, InventoryMovementSourceType.Correction, Guid.NewGuid(), Guid.NewGuid(), originalId,
            DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp130-correction-a", DateTimeOffset.UtcNow);
        await using (var firstCorrectionDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            firstCorrectionDb.StockMovements.Add(correction);
            await firstCorrectionDb.SaveChangesAsync();
        }

        await using (var duplicateCorrectionDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            duplicateCorrectionDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, "WH-SQL", "SQL warehouse", productId,
                "SKU-SQL", "SQL product", unitId, "EA", InventoryMovementDirection.Outbound, 1m, null, null,
                InventoryValuationStatus.Pending, null, InventoryMovementSourceType.Correction, Guid.NewGuid(), Guid.NewGuid(), originalId,
                DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp130-correction-b", DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateCorrectionDb.SaveChangesAsync());
        }

        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        Assert.Equal(1, await verifyDb.StockMovements.CountAsync(item => item.CorrectionOfMovementId == originalId));
        await using var indexConnection = new SqlConnection(connectionString);
        await indexConnection.OpenAsync();
        await using var indexCommand = indexConnection.CreateCommand();
        indexCommand.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name = N'StockLedgerMovements'
              AND indexes.name = N'IX_StockLedgerMovements_TenantId_CorrectionOfMovementId'
              AND indexes.is_unique = 1
              AND indexes.has_filter = 1
              AND indexes.filter_definition LIKE N'%CorrectionOfMovementId%IS NOT NULL%';
            """;
        Assert.Equal(1, Convert.ToInt32(await indexCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task MESP131_sql_server_process_fingerprint_is_bounded_and_durable_replay_conflict_is_explicit()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        var movement = SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 2m, 10m, "SAR", "sql-mesp131-fingerprint-movement");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.Add(movement);
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policyRequest = SqlPolicy(companyId, new DateOnly(2026, 1, 1));
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(Guid.NewGuid(), policyRequest, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-fingerprint-policy", "sql-mesp131-fingerprint-policy-key", "sql-mesp131-fingerprint-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        var restRequest = new InventoryValuationProcessRequest(companyId, null, warehouseId, productId, unitId);
        var fingerprint = InventoryFingerprints.Create(restRequest);
        Assert.InRange(fingerprint.Length, 1, 128);
        var command = new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-fingerprint-process", "sql-mesp131-fingerprint-key", fingerprint);
        var result = await persistence.ProcessAsync(context, command);
        Assert.True(result.Succeeded, result.Code);

        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            var run = await db.ValuationRuns.AsNoTracking().SingleAsync(item => item.IdempotencyKey == command.IdempotencyKey);
            Assert.Equal(fingerprint, run.RequestFingerprint);
            Assert.InRange(run.RequestFingerprint.Length, 1, 128);
        }

        var conflict = await persistence.ProcessAsync(context, command with { RequestFingerprint = fingerprint + "-different" });
        Assert.False(conflict.Succeeded);
        Assert.Equal("idempotency_conflict", conflict.Code);
    }

    [Fact]
    public async Task MESP131_sql_server_first_scope_concurrency_cannot_fork_state_anchor_or_event()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString, sql => sql.CommandTimeout(30)).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.Add(SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 1m, 10m, "SAR", "sql-mesp131-concurrency-movement"));
            await db.SaveChangesAsync();
        }

        var firstPersistence = new InventoryValuationPersistence(options, null, null, null);
        var secondPersistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await firstPersistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 1, 1)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-concurrency-policy", "sql-mesp131-concurrency-policy-key", "sql-mesp131-concurrency-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);
        var firstCommand = new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-concurrency-a", "sql-mesp131-concurrency-a-key", "sql-mesp131-concurrency-a-fingerprint");
        var secondCommand = firstCommand with { CorrelationId = "sql-mesp131-concurrency-b", IdempotencyKey = "sql-mesp131-concurrency-b-key", RequestFingerprint = "sql-mesp131-concurrency-b-fingerprint" };
        var results = await Task.WhenAll(
            Task.Run(() => firstPersistence.ProcessAsync(context, firstCommand)),
            Task.Run(() => secondPersistence.ProcessAsync(context, secondCommand)));

        Assert.All(results, result => Assert.True(result.Succeeded || result.Code == "valuation_concurrency_conflict", result.Code));
        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        Assert.Equal(1, await verifyDb.ValuationStates.CountAsync(item => item.CompanyId == companyId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitId));
        Assert.Equal(1, await verifyDb.ValuationScopeAnchors.CountAsync(item => item.CompanyId == companyId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitId));
        Assert.Equal(1, await verifyDb.MovementValuationEvents.CountAsync(item => item.CompanyId == companyId && item.Status == InventoryValuationEventStatus.Applied));
    }

    [Fact]
    public async Task MESP131_sql_server_compatible_policy_transition_persists_current_state_policy_version()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 2m, 10m, "SAR", "sql-mesp131-transition-first", new DateOnly(2026, 8, 2)),
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 2m, 20m, "SAR", "sql-mesp131-transition-second", new DateOnly(2026, 9, 2)));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var first = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-transition-policy-one", "sql-mesp131-transition-policy-one-key", "sql-mesp131-transition-policy-one-fingerprint"));
        var second = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 9, 1)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-transition-policy-two", "sql-mesp131-transition-policy-two-key", "sql-mesp131-transition-policy-two-fingerprint"));
        Assert.True(first.Succeeded, first.Code);
        Assert.True(second.Succeeded, second.Code);
        var processed = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-transition-process", "sql-mesp131-transition-process-key", "sql-mesp131-transition-process-fingerprint"));
        Assert.True(processed.Succeeded, processed.Code);

        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        var state = await verifyDb.ValuationStates.AsNoTracking().SingleAsync(item => item.CompanyId == companyId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitId);
        Assert.Equal(second.Value!.Id, state.CurrentPolicyId);
        Assert.Equal(2, state.CurrentPolicyVersionNumber);
        Assert.Equal(4m, state.Quantity);
        Assert.Equal(60m, state.Value);
    }

    [Fact]
    public async Task MESP131_sql_server_unique_pool_indexes_are_unique_and_policy_free()
    {
        await using var connection = new SqlConnection(await GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name IN (N'ValuationStates', N'ValuationScopeAnchors')
              AND indexes.is_unique = 1
              AND indexes.name IN (N'IX_ValuationStates_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity', N'IX_ValuationScopeAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity');
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.index_columns AS indexColumns ON indexColumns.object_id = indexes.object_id AND indexColumns.index_id = indexes.index_id
            INNER JOIN sys.columns AS columns ON columns.object_id = indexColumns.object_id AND columns.column_id = indexColumns.column_id
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name IN (N'ValuationStates', N'ValuationScopeAnchors')
              AND indexes.name IN (N'IX_ValuationStates_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity', N'IX_ValuationScopeAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity')
              AND columns.name = N'PolicyId';
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name = N'ValuationPolicies'
              AND indexes.name = N'IX_ValuationPolicies_TenantId_CompanyId_VersionNumber'
              AND indexes.is_unique = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public void MESP131_final_migration_has_populated_target_model_metadata()
    {
        const string migrationId = "20260823225921_MESP131SolFinalValuationIntegrity";
        var migrationType = typeof(MESP131SolFinalValuationIntegrity);
        var migrationAttribute = Assert.Single(migrationType.GetCustomAttributes<MigrationAttribute>());
        Assert.Equal(migrationId, migrationAttribute.Id);

        var targetModel = new MESP131SolFinalValuationIntegrity().TargetModel;
        Assert.NotEmpty(targetModel.GetEntityTypes());

        var valuationEvents = targetModel.FindEntityType(typeof(InventoryMovementValuationEventEntity));
        Assert.NotNull(valuationEvents);
        Assert.NotNull(valuationEvents.FindProperty(nameof(InventoryMovementValuationEventEntity.FormulaMovementValue)));
        Assert.NotNull(valuationEvents.FindProperty(nameof(InventoryMovementValuationEventEntity.RoundingAdjustmentAmount)));

        var financeHandoffs = targetModel.FindEntityType(typeof(InventoryFinanceValuationHandoffEntity));
        Assert.NotNull(financeHandoffs);
        Assert.NotNull(financeHandoffs.FindProperty(nameof(InventoryFinanceValuationHandoffEntity.RoundingAdjustmentAmount)));

        var snapshotModel = new InventoryDbContextModelSnapshot().Model;
        Assert.NotEmpty(snapshotModel.GetEntityTypes());
        Assert.NotNull(snapshotModel.FindEntityType(typeof(InventoryMovementValuationEventEntity))?
            .FindProperty(nameof(InventoryMovementValuationEventEntity.FormulaMovementValue)));
        Assert.NotNull(snapshotModel.FindEntityType(typeof(InventoryMovementValuationEventEntity))?
            .FindProperty(nameof(InventoryMovementValuationEventEntity.RoundingAdjustmentAmount)));
        Assert.NotNull(snapshotModel.FindEntityType(typeof(InventoryFinanceValuationHandoffEntity))?
            .FindProperty(nameof(InventoryFinanceValuationHandoffEntity.RoundingAdjustmentAmount)));
    }

    [Fact]
    public async Task MESP131_sql_server_finance_handoff_persists_direction_signed_amount_and_unique_evidence()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 10m, 10m, "SAR", "sql-mesp131-handoff-in"),
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Outbound, 2m, null, null, "sql-mesp131-handoff-out", new DateOnly(2026, 1, 2), InventoryMovementSourceType.StockIssue));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 1, 1)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-handoff-policy", "sql-mesp131-handoff-policy-key", "sql-mesp131-handoff-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);
        var processed = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-handoff-process", "sql-mesp131-handoff-process-key", "sql-mesp131-handoff-process-fingerprint"));
        Assert.True(processed.Succeeded, processed.Code);
        var handoffs = (await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(companyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal(2, handoffs.Length);
        Assert.Equal(100m, handoffs[0].BaseAmount);
        Assert.Equal(100m, handoffs[0].SignedBaseAmount);
        Assert.Equal(-20m, handoffs[1].SignedBaseAmount);
        Assert.All(handoffs, handoff => Assert.Equal("inventory-valuation-finance.v1", handoff.ContractVersion));

        await using var indexConnection = new SqlConnection(connectionString);
        await indexConnection.OpenAsync();
        await using var indexCommand = indexConnection.CreateCommand();
        indexCommand.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name = N'FinanceValuationHandoffs'
              AND indexes.name = N'IX_FinanceValuationHandoffs_TenantId_MovementId'
              AND indexes.is_unique = 1;
            """;
        Assert.Equal(1, Convert.ToInt32(await indexCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task MESP131_sql_server_final_integrity_persists_closeout_evidence_and_isolates_tracking_failure()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");

        var closeoutCompanyId = Guid.NewGuid();
        var closeoutWarehouseId = Guid.NewGuid();
        var closeoutProductId = Guid.NewGuid();
        var closeoutUnitId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, closeoutCompanyId, closeoutWarehouseId, closeoutProductId, closeoutUnitId, InventoryMovementDirection.Inbound, 2m, 33.33m, "SAR", "sql-mesp131-final-in-one"),
                SqlMovement(tenantId, closeoutCompanyId, closeoutWarehouseId, closeoutProductId, closeoutUnitId, InventoryMovementDirection.Inbound, 1m, 33.34m, "SAR", "sql-mesp131-final-in-two"),
                SqlMovement(tenantId, closeoutCompanyId, closeoutWarehouseId, closeoutProductId, closeoutUnitId, InventoryMovementDirection.Outbound, 3m, null, null, "sql-mesp131-final-out", new DateOnly(2026, 1, 3), InventoryMovementSourceType.StockIssue));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var closeoutPolicy = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(closeoutCompanyId, new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 4), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-closeout-policy", "sql-mesp131-final-closeout-policy-key", "sql-mesp131-final-closeout-policy-fingerprint"));
        Assert.True(closeoutPolicy.Succeeded, closeoutPolicy.Code);
        var closeoutProcess = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(closeoutCompanyId, null, closeoutWarehouseId, closeoutProductId, closeoutUnitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-closeout-process", "sql-mesp131-final-closeout-process-key", "sql-mesp131-final-closeout-process-fingerprint"));
        Assert.True(closeoutProcess.Succeeded, closeoutProcess.Code);
        var closeoutEvent = Assert.Single(await persistence.ListEventsAsync(context, new InventoryValuationQuery(closeoutCompanyId)), item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(99.99m, closeoutEvent.FormulaMovementValue);
        Assert.Equal(0.01m, closeoutEvent.RoundingAdjustmentAmount);
        Assert.Equal(100m, closeoutEvent.MovementValue);
        Assert.Equal(0m, closeoutEvent.NewQuantity);
        Assert.Equal(0m, closeoutEvent.NewValue);
        var closeoutHandoff = Assert.Single(
            await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(closeoutCompanyId)),
            item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(100m, closeoutHandoff.BaseAmount);
        Assert.Equal(-100m, closeoutHandoff.SignedBaseAmount);
        Assert.Equal(0.01m, closeoutHandoff.RoundingAdjustmentAmount);

        var trackingCompanyId = Guid.NewGuid();
        var trackingWarehouseId = Guid.NewGuid();
        var trackingProductId = Guid.NewGuid();
        var trackingUnitId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 10m, "USD", "sql-mesp131-final-lot-a-failure", trackingIdentity: "LOT-A"),
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", "sql-mesp131-final-lot-b", trackingIdentity: "LOT-B"),
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 30m, "SAR", "sql-mesp131-final-lot-a-successor", trackingIdentity: "LOT-A"),
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 40m, "SAR", "sql-mesp131-final-lot-b-successor", trackingIdentity: "LOT-B"));
            await db.SaveChangesAsync();
        }

        var trackingPolicy = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(trackingCompanyId, new DateOnly(2026, 1, 1), scopeMode: InventoryValuationScopeMode.WarehouseProductUomTracking), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-tracking-policy", "sql-mesp131-final-tracking-policy-key", "sql-mesp131-final-tracking-policy-fingerprint"));
        Assert.True(trackingPolicy.Succeeded, trackingPolicy.Code);
        var trackingProcess = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(trackingCompanyId, null, trackingWarehouseId, trackingProductId, trackingUnitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-tracking-process", "sql-mesp131-final-tracking-process-key", "sql-mesp131-final-tracking-process-fingerprint"));
        Assert.True(trackingProcess.Succeeded, trackingProcess.Code);
        Assert.Equal(2, trackingProcess.Value!.AppliedCount);
        Assert.Equal(2, trackingProcess.Value.PendingCount);
        var trackingEvents = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(trackingCompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal(InventoryValuationEventStatus.Pending, trackingEvents[0].Status);
        Assert.Equal(InventoryValuationEventStatus.Applied, trackingEvents[1].Status);
        Assert.Equal("pending_predecessor", trackingEvents[2].StatusCode);
        Assert.Equal(InventoryValuationEventStatus.Applied, trackingEvents[3].Status);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.columns
            INNER JOIN sys.tables ON sys.tables.object_id = sys.columns.object_id
            INNER JOIN sys.schemas ON sys.schemas.schema_id = sys.tables.schema_id
            WHERE sys.schemas.name = N'inventory'
              AND sys.tables.name = N'MovementValuationEvents'
              AND sys.columns.name IN (N'FormulaMovementValue', N'RoundingAdjustmentAmount');
            SELECT COUNT(*)
            FROM sys.columns
            INNER JOIN sys.tables ON sys.tables.object_id = sys.columns.object_id
            INNER JOIN sys.schemas ON sys.schemas.schema_id = sys.tables.schema_id
            WHERE sys.schemas.name = N'inventory'
              AND sys.tables.name = N'FinanceValuationHandoffs'
              AND sys.columns.name = N'RoundingAdjustmentAmount';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    private sealed record SqlFinanceScenario(
        DbContextOptions Options,
        Guid CompanyId,
        FinancePersistence FirstPersistence,
        FinancePersistence SecondPersistence,
        FinanceRequestContext FirstContext,
        FinanceRequestContext SecondContext,
        FinanceAccountRecord DebitAccount,
        FinanceAccountRecord CreditAccount,
        FinanceFiscalPeriodRecord Period,
        FinanceJournalRecord ApprovedJournal);

    private async Task<SqlFinanceScenario> CreateApprovedFinanceJournalAsync(bool foreignCurrency = false)
    {
        var connectionString = await GetConnectionStringAsync();
        var options = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var companyId = Guid.NewGuid();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL Finance Company", "SAR")]);
        IMasterDataExchangeRatePersistence exchangeRates = foreignCurrency
            ? new SqlFinanceExchangeRatePersistence(_fixture.TenantA.TenantId, "USD", "SAR", 3.75m)
            : new UnavailableMasterDataExchangeRatePersistence();
        var first = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), exchangeRates);
        var second = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), exchangeRates);
        var firstContext = FinanceContext("tenant.finance.journal.create");
        var secondContext = FinanceContext("tenant.finance.journal.post");
        var accounts = await CreateFinanceAccountsAsync(first, firstContext, companyId, "sql-race");
        var calendar = await first.CreateCalendarAsync(firstContext, new FinanceFiscalCalendarCommand(companyId, "SQL Finance FY", Guid.NewGuid(), "sql-race-calendar", "sql-race-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await first.CreateYearAsync(firstContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-race-year", "sql-race-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await first.CreatePeriodAsync(firstContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "sql-race-period", "sql-race-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await first.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "sql-race-open", "sql-race-open"));
        Assert.True(opened.Succeeded, opened.Code);

        var rateId = foreignCurrency ? ((SqlFinanceExchangeRatePersistence)exchangeRates).RateId : (Guid?)null;
        var rateVersionId = foreignCurrency ? ((SqlFinanceExchangeRatePersistence)exchangeRates).RateVersionId : (Guid?)null;
        var currency = foreignCurrency ? "USD" : (string?)null;
        var rate = foreignCurrency ? 3.75m : 1m;
        var command = new FinanceJournalCommand(
            companyId,
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 15),
            currency,
            rate,
            rateId,
            rateVersionId,
            foreignCurrency ? 1 : null,
            "manual-journal.v1",
            "manual",
            null,
            null,
            null,
            "SQL concurrency journal",
            [
                new FinanceJournalLineCommand(accounts.Debit.Id, 10m, 0m, foreignCurrency ? 10m : null, foreignCurrency ? "USD" : "SAR", null, null),
                new FinanceJournalLineCommand(accounts.Credit.Id, 0m, 10m, foreignCurrency ? 10m : null, foreignCurrency ? "USD" : "SAR", null, null)
            ],
            Guid.NewGuid(),
            "sql-race-create",
            "sql-race-create");
        var created = await first.CreateJournalAsync(firstContext, command);
        Assert.True(created.Succeeded, created.Code);
        var submitted = await first.TransitionJournalAsync(firstContext, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "SQL concurrency submit", "sql-race-submit", "sql-race-submit"), FinanceJournalStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approverContext = FinanceContext("tenant.finance.journal.approve");
        var approved = await first.TransitionJournalAsync(approverContext, new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "SQL concurrency approve", "sql-race-approve", "sql-race-approve"), FinanceJournalStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);

        return new SqlFinanceScenario(options, companyId, first, second, firstContext, secondContext, accounts.Debit, accounts.Credit, opened.Value!, approved.Value!);
    }

    [Fact]
    public async Task MESP141_sql_server_concurrent_run_creation_on_one_key_yields_one_run_and_replays_for_the_rest()
    {
        var (service, _, options) = await CreateMigrationServiceAsync();
        var tenant = MigrationTenant("sql-run-race");
        var request = MigrationRequest(tenant, "tenant.migration.run.create");
        var key = $"sql-run-race-{Guid.NewGuid():N}";
        var creation = MigrationCreationRequest(key);

        // Eight real connections against real SQL Server. The SQLite suite
        // cannot prove this path: only the provider raises 2627/2601 on the
        // idempotency primary key, which is what the replay recovery
        // classifies before re-reading the committed row.
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            _ => service.CreateRunAsync(request, creation)));

        Assert.All(results, item => Assert.True(item.Succeeded, item.Code));
        Assert.Equal(1, results.Count(item => item.Kind == MigrationResultKind.Succeeded));
        Assert.Equal(7, results.Count(item => item.Kind == MigrationResultKind.Replayed));
        Assert.Single(results.Select(item => item.Value!.RunId).Distinct());

        await using var db = new MigrationDbContext(options, tenant);
        Assert.Equal(1, await db.Idempotency.CountAsync(item => item.IdempotencyKey == key));
        Assert.Equal(1, await db.Runs.CountAsync(item => item.RunId == results[0].Value!.RunId));
    }

    [Fact]
    public async Task MESP141_sql_server_concurrent_attempt_start_on_one_run_yields_one_attempt_and_replays()
    {
        var (service, _, options) = await CreateMigrationServiceAsync();
        var tenant = MigrationTenant("sql-attempt-race");
        var request = MigrationRequest(tenant, "tenant.migration.attempt.start");
        var run = await PreparedMigrationRunAsync(service, request, $"sql-attempt-run-{Guid.NewGuid():N}");
        var key = $"sql-attempt-race-{Guid.NewGuid():N}";

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            _ => service.StartAttemptAsync(
                request, run.RunId, MigrationOperationKind.Validation, key, $"{key}-fingerprint")));

        Assert.All(results, item => Assert.True(item.Succeeded, item.Code));
        Assert.Equal(1, results.Count(item => item.Kind == MigrationResultKind.Succeeded));
        Assert.Single(results.Select(item => item.Value!.AttemptId).Distinct());
        Assert.All(results, item => Assert.Equal(1, item.Value!.Sequence));

        await using var db = new MigrationDbContext(options, tenant);
        Assert.Equal(1, await db.Attempts.CountAsync(item => item.RunId == run.RunId));
    }

    [Fact]
    public async Task MESP141_sql_server_rowversion_makes_a_stale_run_transition_a_safe_conflict()
    {
        var (service, _, _) = await CreateMigrationServiceAsync();
        var tenant = MigrationTenant("sql-version-race");
        var request = MigrationRequest(tenant, "tenant.migration.run.transition");
        var key = $"sql-version-run-{Guid.NewGuid():N}";
        var created = await service.CreateRunAsync(request, MigrationCreationRequest(key));
        Assert.True(created.Succeeded, created.Code);
        var draftVersion = created.Value!.Version;

        var prepared = await service.TransitionRunAsync(
            request, created.Value.RunId, MigrationRunStatus.Prepared, draftVersion);
        Assert.True(prepared.Succeeded, prepared.Code);
        Assert.NotEqual(draftVersion, prepared.Value!.Version);

        // On SQL Server Version is a real rowversion the database generates,
        // not the Guid the non-SQL-Server providers rotate in application
        // code. Presenting the now-superseded Draft version must therefore be
        // refused by the provider's own concurrency check rather than
        // silently overwriting the committed Prepared row.
        var stale = await service.TransitionRunAsync(
            request, created.Value.RunId, MigrationRunStatus.Validating, draftVersion);

        Assert.False(stale.Succeeded);
        Assert.Equal(MigrationResultKind.Rejected, stale.Kind);
        Assert.Equal("migration_run_version_conflict", stale.Code);

        // The refusal must leave the persisted lifecycle position untouched.
        var reread = await service.FindRunAsync(tenant, created.Value.RunId);
        Assert.True(reread.Succeeded, reread.Code);
        Assert.Equal(MigrationRunStatus.Prepared, reread.Value!.Status);
    }

    [Fact]
    public async Task MESP141_sql_server_database_refuses_an_attempt_predecessor_outside_its_own_run_or_tenant()
    {
        var (service, _, _) = await CreateMigrationServiceAsync();
        var foreignTenant = MigrationTenant("sql-lineage-foreign");
        var foreignRequest = MigrationRequest(foreignTenant, "tenant.migration.attempt.start");
        var foreignRun = await PreparedMigrationRunAsync(
            service, foreignRequest, $"sql-foreign-run-{Guid.NewGuid():N}");
        var foreignKey = $"sql-foreign-attempt-{Guid.NewGuid():N}";
        var foreignAttempt = await service.StartAttemptAsync(
            foreignRequest, foreignRun.RunId, MigrationOperationKind.Validation, foreignKey, $"{foreignKey}-fp");
        Assert.True(foreignAttempt.Succeeded, foreignAttempt.Code);

        var sameTenant = TenantContext.ForOrdinaryMembership(
            _fixture.TenantA.TenantId,
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-lineage-same"),
            actorId: Guid.NewGuid());
        var sameTenantRequest = MigrationRequest(sameTenant, "tenant.migration.attempt.start");
        var firstRun = await PreparedMigrationRunAsync(
            service, sameTenantRequest, $"sql-lineage-run-one-{Guid.NewGuid():N}");
        var firstAttempt = await service.StartAttemptAsync(
            sameTenantRequest,
            firstRun.RunId,
            MigrationOperationKind.Validation,
            $"sql-lineage-attempt-one-{Guid.NewGuid():N}",
            $"sql-lineage-attempt-one-fp-{Guid.NewGuid():N}");
        Assert.True(firstAttempt.Succeeded, firstAttempt.Code);

        var secondRun = await PreparedMigrationRunAsync(
            service, sameTenantRequest, $"sql-lineage-run-two-{Guid.NewGuid():N}");

        var localTenant = MigrationTenant("sql-lineage-local");
        var localRequest = MigrationRequest(localTenant, "tenant.migration.attempt.start");
        var localRun = await PreparedMigrationRunAsync(
            service, localRequest, $"sql-local-run-{Guid.NewGuid():N}");
        Assert.NotEqual(foreignTenant.TenantId.Value, localTenant.TenantId.Value);

        // The Tenant-qualified self foreign key must be enforced by the
        // database itself, not only by the EF model, so this bypasses the
        // application entirely. Before the lineage migration the column was a
        // loose Guid and both of these inserts would have been accepted.
        var sameTenantCrossRunPredecessor = await TryInsertMigrationAttemptAsync(
            sameTenant.TenantId.Value, secondRun.RunId, firstAttempt.Value!.AttemptId);
        var foreignPredecessor = await TryInsertMigrationAttemptAsync(
            localTenant.TenantId.Value, localRun.RunId, foreignAttempt.Value!.AttemptId);
        var absentPredecessor = await TryInsertMigrationAttemptAsync(
            localTenant.TenantId.Value, localRun.RunId, Guid.NewGuid());

        Assert.NotNull(sameTenantCrossRunPredecessor);
        Assert.Equal(547, sameTenantCrossRunPredecessor!.Number);
        Assert.NotNull(foreignPredecessor);
        Assert.Equal(547, foreignPredecessor!.Number);
        Assert.NotNull(absentPredecessor);
        Assert.Equal(547, absentPredecessor!.Number);
    }

    /// <summary>
    /// Inserts one attempt row through raw ADO.NET and returns the SQL error
    /// when the database refuses it, or null when the insert was accepted.
    /// </summary>
    private async Task<SqlError?> TryInsertMigrationAttemptAsync(
        Guid tenantId,
        Guid runId,
        Guid previousAttemptId)
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO [migration].[MigrationAttempts]
                ([AttemptId], [TenantId], [RunId], [Sequence], [PreviousAttemptId], [Operation],
                 [Outcome], [IdempotencyKey], [RequestFingerprint], [StartedAt], [FinishedAt], [SafeOutcomeCode])
            VALUES
                (@AttemptId, @TenantId, @RunId, @Sequence, @PreviousAttemptId, 2,
                 1, @IdempotencyKey, @RequestFingerprint, SYSDATETIMEOFFSET(), NULL, NULL);
            """;
        command.Parameters.AddWithValue("@AttemptId", Guid.NewGuid());
        command.Parameters.AddWithValue("@TenantId", tenantId);
        command.Parameters.AddWithValue("@RunId", runId);
        command.Parameters.AddWithValue("@Sequence", Random.Shared.Next(1000, 100000));
        command.Parameters.AddWithValue("@PreviousAttemptId", previousAttemptId);
        command.Parameters.AddWithValue("@IdempotencyKey", $"raw-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("@RequestFingerprint", $"raw-{Guid.NewGuid():N}");

        try
        {
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqlException exception)
        {
            return exception.Errors.Cast<SqlError>().First();
        }
    }

    private async Task<(MigrationFoundationService Service, List<FoundationAuditEvidence> Evidence, DbContextOptions Options)>
        CreateMigrationServiceAsync()
    {
        var options = SqlServerMigrationConfiguration.Configure(
            await GetConnectionStringAsync(), SqlServerMigrationConfiguration.MigrationHistoryTable);
        var evidence = new List<FoundationAuditEvidence>();
        var service = new MigrationFoundationService(
            new MigrationPersistence(options), new SqlMigrationAuditSink(evidence));
        return (service, evidence, options);
    }

    /// <summary>
    /// A fresh Tenant context carrying a server-derived actor. The shared
    /// harness contexts deliberately carry no actor, and a migration run
    /// refuses to be created without one.
    /// </summary>
    private static TenantContext MigrationTenant(string correlation) =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId(correlation),
            actorId: Guid.NewGuid());

    private static FoundationRequestContext MigrationRequest(TenantContext tenantContext, string permission) =>
        FoundationRequestContext.ForTenant(
            tenantContext.ActorId!.Value, Guid.NewGuid(), tenantContext, permission);

    private static async Task<MigrationRunRecord> PreparedMigrationRunAsync(
        MigrationFoundationService service,
        FoundationRequestContext request,
        string key)
    {
        var created = await service.CreateRunAsync(request, MigrationCreationRequest(key));
        Assert.True(created.Succeeded, created.Code);
        var prepared = await service.TransitionRunAsync(
            request, created.Value!.RunId, MigrationRunStatus.Prepared, created.Value.Version);
        Assert.True(prepared.Succeeded, prepared.Code);
        return prepared.Value!;
    }

    private static MigrationRunCreationRequest MigrationCreationRequest(string key) => new(
        null,
        new MigrationDefinitionReference("tenant-onboarding.foundation", "1"),
        new MigrationSourceProfileReference("neutral-source-profile", "1"),
        MigrationOperationKind.Validation,
        key,
        $"{key}-fingerprint");

    /// <summary>Collects appended evidence so a durable audit failure is never silent.</summary>
    private sealed class SqlMigrationAuditSink(List<FoundationAuditEvidence> appended)
        : MiniErp.App.Modules.Audit.IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(
            FoundationAuditEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            lock (appended)
            {
                appended.Add(evidence);
            }

            return ValueTask.CompletedTask;
        }
    }

    private async Task<(FinanceAccountRecord Debit, FinanceAccountRecord Credit)> CreateFinanceAccountsAsync(IFinancePersistence persistence, FinanceRequestContext context, Guid companyId, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var debit = await persistence.CreateAccountAsync(context, new FinanceAccountCommand(companyId, $"{prefix}-D-{suffix}", "SQL debit", null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{prefix}-debit-{suffix}", $"{prefix}-debit-{suffix}"));
        var credit = await persistence.CreateAccountAsync(context, new FinanceAccountCommand(companyId, $"{prefix}-C-{suffix}", "SQL credit", null, null, FinanceAccountType.Revenue, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{prefix}-credit-{suffix}", $"{prefix}-credit-{suffix}"));
        Assert.True(debit.Succeeded, debit.Code);
        Assert.True(credit.Succeeded, credit.Code);
        return (debit.Value!, credit.Value!);
    }

    private static FinanceJournalCommand SqlManualJournal(Guid companyId, (FinanceAccountRecord Debit, FinanceAccountRecord Credit) accounts, string key) =>
        new(companyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "SQL sequence journal", [new FinanceJournalLineCommand(accounts.Debit.Id, 10m, 0m, null, "SAR", null, null), new FinanceJournalLineCommand(accounts.Credit.Id, 0m, 10m, null, "SAR", null, null)], Guid.NewGuid(), key, key);

    private FinanceRequestContext FinanceContext(string permission, Guid actorId = default)
    {
        var foundation = FoundationRequestContext.ForTenant(actorId == Guid.Empty ? Guid.NewGuid() : actorId, Guid.NewGuid(), _fixture.TenantA, permission);
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private static async Task<FinanceOperationResult<T>> SafeFinanceOperationAsync<T>(Func<Task<FinanceOperationResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (DbUpdateConcurrencyException)
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (Exception exception) when (IsExpectedFinanceContention(exception))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
    }

    private static async Task<(bool Succeeded, string Code)> SafeCodeAsync<T>(Func<Task<FinanceOperationResult<T>>> action)
    {
        var result = await SafeFinanceOperationAsync(action);
        return (result.Succeeded, result.Code);
    }

    private static bool IsExpectedFinanceContention(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException) return true;
        if (exception is SqlException directSql && directSql.Number is 1205 or 2601 or 2627 or 3960) return true;
        if (exception is DbUpdateException dbUpdate
            && dbUpdate.InnerException is SqlException sql
            && sql.Errors.Cast<SqlError>().Any(error => error.Number is 1205 or 2601 or 2627 or 3960)) return true;
        return exception.InnerException is not null && IsExpectedFinanceContention(exception.InnerException);
    }

    private sealed class SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement requirement) : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) =>
            string.Equals(sourceContract, "inventory-valuation-finance.v1", StringComparison.OrdinalIgnoreCase)
                ? requirement
                : FinanceApprovalRequirement.NotConfigured;
    }

    private sealed class SqlSettlementApprovalPolicy(FinanceApprovalRequirement requirement) : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) =>
            sourceContract is "supplier-payment.v1" or "customer-receipt.v1"
                ? requirement
                : FinanceApprovalRequirement.NotConfigured;
    }

    private sealed class SqlStaticCustomerPersistence(Guid tenantId, Guid customerId) : IBusinessCustomerReferenceReader
    {
        public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid requestedCustomerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BusinessCustomerReference?>(requestedCustomerId == customerId && tenantContext.TenantId.Value == tenantId
                ? new BusinessCustomerReference(customerId, new TenantId(tenantId), "SQL-CUSTOMER", MasterDataLifecycleState.Active)
                : null);
    }

    private sealed class SqlStaticSupplierPersistence(Guid tenantId, Guid? supplierId = null) : ISupplierPersistence
    {
        private readonly UnavailableSupplierPersistence fallback = new();
        private readonly Guid supplierId = supplierId ?? Guid.NewGuid();

        public Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SupplierRecord>>(supplierId == Guid.Empty ? [] : [Record(tenantContext)]);

        public Task<SupplierRecord?> FindSupplierAsync(TenantContext tenantContext, Guid requestedSupplierId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SupplierRecord?>(requestedSupplierId == supplierId && tenantContext.TenantId.Value == tenantId ? Record(tenantContext) : null);

        public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(TenantContext tenantContext, Guid id, CreateSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateSupplierAsync(tenantContext, id, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(TenantContext tenantContext, EditSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditSupplierAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(TenantContext tenantContext, Guid id, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetSupplierLifecycleAsync(tenantContext, id, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid id, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, id, cancellationToken);

        private SupplierRecord Record(TenantContext tenantContext) =>
            new(supplierId, new TenantId(tenantId), "SQL-SUPPLIER", new LocalizedName("SQL Supplier", null), null, null, MasterDataLifecycleState.Active, [1], []);
    }

    private sealed class SqlStaticSupplierInvoiceSourceProvider(FinanceSupplierInvoiceSourceRecord source) : IFinanceSupplierInvoiceSourceProvider
    {
        public Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<FinanceSupplierInvoiceSourceRecord?>(sourceEvidenceId == source.SourceEvidenceId ? source : null);
    }

    private sealed class SqlMesp134CurrencyPersistence : IMasterDataCurrencyPaymentTermPersistence
    {
        private readonly UnavailableMasterDataCurrencyPaymentTermPersistence fallback = new();
        private readonly MasterDataCurrencyRecord sar;
        private readonly MasterDataCurrencyRecord usd;

        public SqlMesp134CurrencyPersistence(TenantId tenantId)
        {
            sar = new MasterDataCurrencyRecord(Guid.NewGuid(), tenantId, "SAR", new LocalizedName("Saudi Riyal"), MasterDataLifecycleState.Active, 1, [1]);
            usd = new MasterDataCurrencyRecord(Guid.NewGuid(), tenantId, "USD", new LocalizedName("US Dollar"), MasterDataLifecycleState.Active, 1, [1]);
            SarCurrencyId = sar.Id;
            UsdCurrencyId = usd.Id;
        }

        public Guid SarCurrencyId { get; }
        public Guid UsdCurrencyId { get; }
        public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataCurrencyRecord>>([sar, usd]);
        public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataCurrencyRecord?>(currencyId == sar.Id ? sar : currencyId == usd.Id ? usd : null);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateCurrencyAsync(tenantContext, currencyId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditCurrencyAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetCurrencyLifecycleAsync(tenantContext, currencyId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => fallback.ListPaymentTermsAsync(tenantContext, cancellationToken);
        public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CancellationToken cancellationToken = default) => fallback.FindPaymentTermAsync(tenantContext, paymentTermId, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreatePaymentTermAsync(tenantContext, paymentTermId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditPaymentTermAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetPaymentTermLifecycleAsync(tenantContext, paymentTermId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, resourceKind, resourceId, cancellationToken);
    }

    private sealed class SqlMesp134ExchangeRatePersistence : IMasterDataExchangeRatePersistence
    {
        private readonly MasterDataExchangeRateRecord record;
        private readonly UnavailableMasterDataExchangeRatePersistence fallback = new();

        public SqlMesp134ExchangeRatePersistence(TenantId tenantId, string sourceCurrency, string targetCurrency, decimal historicalRate, decimal currentRate)
        {
            RateId = Guid.NewGuid();
            HistoricalRateVersionId = Guid.NewGuid();
            CurrentRateVersionId = Guid.NewGuid();
            record = new MasterDataExchangeRateRecord(
                RateId,
                tenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                sourceCurrency,
                targetCurrency,
                MasterDataLifecycleState.Active,
                2,
                [
                    new MasterDataExchangeRateVersionRecord(HistoricalRateVersionId, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), historicalRate, 4, ExchangeRateProvenance.Configured, "SQL MESP-134 historical rate", sourceCurrency, targetCurrency),
                    new MasterDataExchangeRateVersionRecord(CurrentRateVersionId, 2, new DateOnly(2026, 7, 1), null, currentRate, 4, ExchangeRateProvenance.Configured, "SQL MESP-134 current rate", sourceCurrency, targetCurrency)
                ],
                [1, 2]);
        }

        public Guid RateId { get; }
        public Guid HistoricalRateVersionId { get; }
        public Guid CurrentRateVersionId { get; }
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataExchangeRateRecord>>([record]);
        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataExchangeRateRecord?>(record.Id == exchangeRateId ? record : null);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateExchangeRateAsync(tenantContext, exchangeRateId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditExchangeRateAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetExchangeRateLifecycleAsync(tenantContext, exchangeRateId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, exchangeRateId, cancellationToken);
    }

    private sealed class SqlFinanceExchangeRatePersistence : IMasterDataExchangeRatePersistence
    {
        private readonly UnavailableMasterDataExchangeRatePersistence fallback = new();
        private readonly MasterDataExchangeRateRecord record;

        public SqlFinanceExchangeRatePersistence(TenantId tenantId, string sourceCurrency, string targetCurrency, decimal rate)
        {
            RateId = Guid.NewGuid();
            RateVersionId = Guid.NewGuid();
            record = new MasterDataExchangeRateRecord(RateId, tenantId, Guid.NewGuid(), Guid.NewGuid(), sourceCurrency, targetCurrency, MasterDataLifecycleState.Active, 1, [new MasterDataExchangeRateVersionRecord(RateVersionId, 1, new DateOnly(2026, 1, 1), null, rate, 4, ExchangeRateProvenance.Configured, "SQL test", sourceCurrency, targetCurrency)], [1]);
        }

        public Guid RateId { get; }
        public Guid RateVersionId { get; }
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => fallback.ListExchangeRatesAsync(tenantContext, cancellationToken);
        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Task.FromResult(record.Id == exchangeRateId ? record : null);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateExchangeRateAsync(tenantContext, exchangeRateId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditExchangeRateAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetExchangeRateLifecycleAsync(tenantContext, exchangeRateId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, exchangeRateId, cancellationToken);
    }

    private static InventoryValuationPolicyRequest SqlPolicy(
        Guid companyId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null,
        InventoryValuationScopeMode scopeMode = InventoryValuationScopeMode.WarehouseProductUom,
        int unitCostScale = 8,
        int amountScale = 8,
        string currency = "SAR") =>
        new(companyId, Guid.Parse("99999999-9999-9999-9999-999999999999"), currency, scopeMode, effectiveFrom, effectiveTo, unitCostScale, amountScale, InventoryValuationRoundingMode.ToEven, "PurchaseOrderUnitPrice", "CurrentMovingAverage", "CurrentMovingAverage");

    private static InventoryStockMovementEntity SqlMovement(
        TenantId tenantId,
        Guid companyId,
        Guid warehouseId,
        Guid productId,
        Guid unitId,
        InventoryMovementDirection direction,
        decimal quantity,
        decimal? unitCost,
        string? currency,
        string sourceReference,
        DateOnly? effectiveDate = null,
        InventoryMovementSourceType sourceType = InventoryMovementSourceType.OpeningBalance,
        string? trackingIdentity = null) =>
        new(tenantId, Guid.NewGuid(), companyId, null, warehouseId, "WH-SQL-MWA", "SQL MWA warehouse", productId, "SKU-SQL-MWA", "SQL MWA product", unitId, "EA", direction, quantity, unitCost, currency, InventoryValuationStatus.Pending, trackingIdentity, sourceType, Guid.NewGuid(), Guid.NewGuid(), null, effectiveDate ?? new DateOnly(2026, 1, 1), Guid.Parse("44444444-4444-4444-4444-444444444444"), sourceReference, DateTimeOffset.UtcNow);

    private static async Task ExecuteProbeInsertAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid workId,
        Guid tenantId,
        Guid eventId,
        string kind)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [test].[DurableWorkProbe]
                ([WorkId], [TenantId], [EventId], [State], [Kind], [Payload])
            VALUES (@workId, @tenantId, @eventId, N'Pending', @kind, N'opaque-test-payload');
            """;
        command.Parameters.AddWithValue("@workId", workId);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@eventId", eventId);
        command.Parameters.AddWithValue("@kind", kind);
        await command.ExecuteNonQueryAsync();
    }

    private static TenantOwnedRecord AttachForgedRecord(
        TenantPersistenceSession session,
        Guid foreignRecordId,
        TenantId trustedTenantId)
    {
        var forged = (TenantOwnedRecord)Activator.CreateInstance(
            typeof(TenantOwnedRecord),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [],
            culture: null)!;
        var entry = session.DbContext.Entry(forged);
        entry.Property(nameof(TenantOwnedRecord.Id)).CurrentValue = foreignRecordId;
        entry.Property(nameof(TenantOwnedRecord.TenantId)).CurrentValue = trustedTenantId;
        entry.Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue = "forged-payload";
        entry.Property(nameof(TenantOwnedRecord.RelationshipKind)).CurrentValue = TenantRelationshipKind.None;
        entry.Property(nameof(TenantOwnedRecord.RelatedTenantId)).CurrentValue = null;
        return forged;
    }
}
