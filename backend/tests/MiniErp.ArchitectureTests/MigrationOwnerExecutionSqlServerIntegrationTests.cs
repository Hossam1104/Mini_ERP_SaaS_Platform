using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Adapters;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationOwnerExecutionSqlServerSafetyTests
{
    private readonly SqlServerSafetyFixture safety;

    public MigrationOwnerExecutionSqlServerSafetyTests(SqlServerSafetyFixture safety) => this.safety = safety;

    [Fact]
    public async Task Sql_server_full_migration_execution_reaches_real_owner_records_with_replay_and_isolation()
    {
        var fixture = await Fixture.CreateAsync(safety);
        var (categoryId, unitId) = await fixture.CreateProductReferencesAsync();
        var prepared = await fixture.PrepareAsync(categoryId, unitId);
        fixture.Owner.RunId = prepared.Run.RunId;

        var result = await fixture.Execution.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            prepared.ExecutionKey,
            prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationRunStatus.Completed, result.Value!.RunStatus);
        Assert.Equal(MigrationExecutionService.HistoricalFingerprintVersion, result.Value.FingerprintVersion);
        Assert.Equal(MigrationAttemptOutcome.Succeeded, result.Value.AttemptOutcome);
        Assert.Equal(3, result.Value.Effects.Count(item => item.Disposition == MigrationExecutionEffectDisposition.Committed));
        Assert.All(result.Value.Batches, batch => Assert.Equal(MigrationExecutionBatchState.Completed, batch.State));
        Assert.Equal(MigrationRunStatus.Executing, fixture.Owner.StatusBeforeOwnerExecute);

        var suppliers = await fixture.Suppliers.ListSuppliersAsync(fixture.Tenant);
        var customers = await fixture.Customers.ListCustomersAsync(fixture.Tenant);
        var products = await fixture.Catalog.ListProductsAsync(fixture.Tenant);
        var supplier = Assert.Single(suppliers);
        var customer = Assert.Single(customers);
        var product = Assert.Single(products);
        Assert.Equal(fixture.Tenant.TenantId, supplier.TenantId);
        Assert.Equal(fixture.Tenant.TenantId, customer.TenantId);
        Assert.Equal(fixture.Tenant.TenantId, product.TenantId);
        Assert.Equal("SUP-SQL-REAL", supplier.Code);
        Assert.Equal("SQL Supplier", supplier.LegalName.English);
        Assert.Equal("CUS-SQL-REAL", customer.Code);
        Assert.Equal("SQL Customer", customer.LegalName.English);
        Assert.Equal("SKU-SQL-REAL", product.Sku);
        Assert.Equal("SQL Product", product.Name.English);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal(unitId, product.BaseUnitOfMeasureId);

        AssertEffect(result.Value, MigrationCanonicalRecordType.Supplier, supplier.Id, supplier.Code);
        AssertEffect(result.Value, MigrationCanonicalRecordType.Customer, customer.Id, customer.Code);
        AssertEffect(result.Value, MigrationCanonicalRecordType.Product, product.Id, product.Sku);

        foreach (var effect in result.Value.Effects)
        {
            var evidence = await fixture.Gateway.ReadEvidenceAsync(fixture.Request, effect.OwnerBatchId);
            var row = Assert.Single(evidence!.Rows);
            Assert.Equal(effect.OwnerRowId, row.Id);
            Assert.Equal(effect.ResultingResourceId, row.ResultingResourceId);
            Assert.Equal(effect.ResultingResourceCode, row.ResultingResourceCode);
            Assert.NotEmpty(await fixture.Imports.ListRowsAsync(fixture.Tenant, effect.OwnerBatchId));
            Assert.NotEmpty(await fixture.Imports.ListAuditAsync(fixture.Tenant, effect.OwnerBatchId));
        }

        var batchCount = (await fixture.Imports.ListBatchesAsync(fixture.Tenant)).Count;
        var replay = await fixture.Execution.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            prepared.ExecutionKey,
            prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(result.Value.AttemptId, replay.Value!.AttemptId);
        Assert.Equal(batchCount, (await fixture.Imports.ListBatchesAsync(fixture.Tenant)).Count);
        Assert.Single(await fixture.Suppliers.ListSuppliersAsync(fixture.Tenant));
        Assert.Single(await fixture.Customers.ListCustomersAsync(fixture.Tenant));
        Assert.Single(await fixture.Catalog.ListProductsAsync(fixture.Tenant));

        var tenantB = fixture.OtherTenant;
        var otherRequest = fixture.OtherRequest;
        Assert.Null(await fixture.Migration.FindRunAsync(tenantB, prepared.Run.RunId));
        Assert.Empty(await fixture.Imports.ListBatchesAsync(tenantB));
        Assert.Empty(await fixture.Suppliers.ListSuppliersAsync(tenantB));
        Assert.Empty(await fixture.Customers.ListCustomersAsync(tenantB));
        Assert.Empty(await fixture.Catalog.ListProductsAsync(tenantB));
        Assert.Null(await fixture.Gateway.ReadEvidenceAsync(otherRequest, result.Value.Effects[0].OwnerBatchId));
    }

    [Fact]
    public async Task Sql_server_historical_v1_execution_replays_after_slice6_upgrade_without_new_attempt_or_owner_effect()
    {
        var fixture = await Fixture.CreateAsync(safety);
        var (categoryId, unitId) = await fixture.CreateProductReferencesAsync();
        var prepared = await fixture.PrepareAsync(categoryId, unitId);
        var intake = (await fixture.Migration.FindIntakeAsync(fixture.Tenant, prepared.Run.RunId))!;
        var validation = (await fixture.Migration.FindLatestValidationAsync(fixture.Tenant, prepared.Run.RunId))!;
        var dryRun = (await fixture.Migration.FindLatestDryRunAsync(fixture.Tenant, prepared.Run.RunId))!;
        var common = new[]
        {
            prepared.Run.RunId.ToString("D"),
            prepared.Run.TenantId.Value.ToString("D"),
            prepared.Run.Definition.DefinitionId,
            prepared.Run.Definition.Version,
            prepared.Run.SourceProfile.ProfileId,
            prepared.Run.SourceProfile.ProfileVersion,
            intake.Source.Sha256,
            validation.ValidationResultId.ToString("D"),
            validation.PackageHash,
            dryRun.PreviewId.ToString("D"),
            dryRun.AttemptId.ToString("D"),
            dryRun.PackageHash,
            $"Tenant:{prepared.Run.TenantId.Value:D}",
            "master-data-owner-import-v1"
        };
        var historicalFingerprint = MigrationFingerprintEncoder.Compute(MigrationExecutionService.HistoricalFingerprintVersion, common);
        var foundation = new MigrationFoundationService(fixture.Migration, new NoopAuditSink());
        var historicalKey = "historical-v1-replay";
        var started = await foundation.StartAttemptAsync(fixture.Request, prepared.Run.RunId, MigrationOperationKind.Execution, historicalKey, historicalFingerprint);
        Assert.True(started.Succeeded, started.Code);
        var completed = await foundation.RecordAttemptOutcomeAsync(fixture.Request, prepared.Run.RunId, started.Value!.AttemptId, MigrationAttemptOutcome.Succeeded, "historical_execution_completed", started.Value.Version);
        Assert.True(completed.Succeeded, completed.Code);

        var replay = await fixture.Execution.ExecuteAsync(fixture.Request, prepared.Run.RunId, historicalKey, prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(MigrationExecutionService.HistoricalFingerprintVersion, replay.Value!.FingerprintVersion);
        Assert.Equal(historicalFingerprint, replay.Value.Fingerprint);
        Assert.Single(await fixture.Migration.ListAttemptsAsync(fixture.Tenant, prepared.Run.RunId), item => item.Operation == MigrationOperationKind.Execution);
        Assert.Empty(await fixture.Migration.ListBatchesAsync(fixture.Tenant, prepared.Run.RunId, started.Value.AttemptId));
    }

    private static void AssertEffect(
        MigrationExecutionResult result,
        MigrationCanonicalRecordType type,
        Guid resourceId,
        string resourceCode)
    {
        var effect = Assert.Single(result.Effects, item => item.RecordType == type);
        Assert.Equal(MigrationExecutionEffectDisposition.Committed, effect.Disposition);
        Assert.Equal(resourceId, effect.ResultingResourceId);
        Assert.Equal(resourceCode, effect.ResultingResourceCode);
        Assert.NotEqual(Guid.Empty, effect.OwnerBatchId);
        Assert.NotEqual(Guid.Empty, effect.OwnerRowId);
    }

    private sealed class Fixture
    {
        private const string PackageHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        private const string SourceHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        private Fixture(
            TenantContext tenant,
            TenantContext otherTenant,
            FoundationRequestContext request,
            FoundationRequestContext otherRequest,
            MasterDataRequestContext importContext,
            MasterDataImportService importsService,
            MasterDataImportPersistence imports,
            MasterDataOwnerExecutionGateway gateway,
            OrderingOwnerGateway owner,
            MasterDataCatalogPersistence catalog,
            BusinessPartiesSupplierPersistence suppliers,
            BusinessPartiesCustomerPersistence customers,
            MigrationPersistence migration,
            MigrationExecutionService execution)
        {
            Tenant = tenant;
            OtherTenant = otherTenant;
            Request = request;
            OtherRequest = otherRequest;
            ImportContext = importContext;
            ImportsService = importsService;
            Imports = imports;
            Gateway = gateway;
            Owner = owner;
            Catalog = catalog;
            Suppliers = suppliers;
            Customers = customers;
            Migration = migration;
            Execution = execution;
        }

        internal TenantContext Tenant { get; }
        internal TenantContext OtherTenant { get; }
        internal FoundationRequestContext Request { get; }
        internal FoundationRequestContext OtherRequest { get; }
        internal MasterDataRequestContext ImportContext { get; }
        internal MasterDataImportService ImportsService { get; }
        internal MasterDataImportPersistence Imports { get; }
        internal MasterDataOwnerExecutionGateway Gateway { get; }
        internal OrderingOwnerGateway Owner { get; }
        internal MasterDataCatalogPersistence Catalog { get; }
        internal BusinessPartiesSupplierPersistence Suppliers { get; }
        internal BusinessPartiesCustomerPersistence Customers { get; }
        internal MigrationPersistence Migration { get; }
        internal MigrationExecutionService Execution { get; }

        internal static async Task<Fixture> CreateAsync(SqlServerSafetyFixture safety)
        {
            await using var connection = await safety.OpenConnectionAsync();
            var masterOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable);
            var partiesOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.BusinessPartiesHistoryTable);
            var migrationOptions = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.MigrationHistoryTable);
            var tenant = TenantContext.ForOrdinaryMembership(
                new TenantId(Guid.NewGuid()),
                new MembershipReference(Guid.NewGuid()),
                correlationId: new CorrelationId("sql-migration-owner-a"),
                actorId: Guid.NewGuid());
            var otherTenant = TenantContext.ForOrdinaryMembership(
                new TenantId(Guid.NewGuid()),
                new MembershipReference(Guid.NewGuid()),
                correlationId: new CorrelationId("sql-migration-owner-b"),
                actorId: Guid.NewGuid());
            var request = FoundationRequestContext.ForTenant(tenant.ActorId!.Value, Guid.NewGuid(), tenant, "tenant.migration.execute");
            var otherRequest = FoundationRequestContext.ForTenant(otherTenant.ActorId!.Value, Guid.NewGuid(), otherTenant, "tenant.migration.execute");
            var importContext = MasterDataRequestContext.FromFoundationContext(
                FoundationRequestContext.ForTenant(tenant.ActorId!.Value, Guid.NewGuid(), tenant, "tenant.master-data.import"));

            var catalog = new MasterDataCatalogPersistence(masterOptions);
            var imports = new MasterDataImportPersistence(masterOptions);
            var suppliers = new BusinessPartiesSupplierPersistence(partiesOptions);
            var customers = new BusinessPartiesCustomerPersistence(partiesOptions);
            var processors = new MasterDataImportProcessorRegistry(
                MasterDataImportProcessorFactory.Create(
                    catalog,
                    catalog,
                    new UnavailableMasterDataCurrencyPaymentTermPersistence(),
                    new UnavailableMasterDataTaxPersistence(),
                    new UnavailableMasterDataExchangeRatePersistence(),
                    new UnavailableMasterDataPriceListPersistence(),
                    suppliers,
                    customers));
            var importsService = new MasterDataImportService(
                new MasterDataImportAuthorizationComposition(new AllCapabilities()),
                imports,
                processors);
            var gateway = new MasterDataOwnerExecutionGateway(importsService);
            var migration = new MigrationPersistence(migrationOptions);
            var references = new MigrationOwnerReferenceAdapter(
                catalog,
                suppliers,
                customers,
                catalog,
                new UnavailableMasterDataExchangeRatePersistence(),
                new UnavailableMasterDataCurrencyPaymentTermPersistence(),
                new UnavailableMasterDataTaxPersistence(),
                new NoFinanceCompanyProvider(),
                new UnavailableFinancePersistence(),
                new NoInventoryProductProvider(),
                new NoInventoryWarehouseProvider());
            var owner = new OrderingOwnerGateway(gateway, migration, tenant);
            var execution = new MigrationExecutionService(
                new MigrationFoundationService(migration, new NoopAuditSink()),
                migration,
                migration,
                migration,
                new TenantWideScopeResolver(),
                new TenantWideScopeResolver(),
                owner,
                references);
            return new Fixture(tenant, otherTenant, request, otherRequest, importContext, importsService, imports, gateway, owner, catalog, suppliers, customers, migration, execution);
        }

        internal async Task<(Guid CategoryId, Guid UnitId)> CreateProductReferencesAsync()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            await ImportReferenceAsync(MasterDataResourceKind.ProductCategory, new Dictionary<string, string?>
            {
                ["code"] = $"CAT-SQL-{suffix}",
                ["englishName"] = "SQL category"
            }, $"category-{suffix}");
            await ImportReferenceAsync(MasterDataResourceKind.UnitOfMeasure, new Dictionary<string, string?>
            {
                ["code"] = $"EA-SQL-{suffix}",
                ["englishName"] = "SQL each"
            }, $"unit-{suffix}");
            return (
                Assert.Single(await Catalog.ListCategoriesAsync(Tenant)).Id,
                Assert.Single(await Catalog.ListUnitsOfMeasureAsync(Tenant)).Id);
        }

        private async Task ImportReferenceAsync(
            MasterDataResourceKind kind,
            IReadOnlyDictionary<string, string?> fields,
            string key)
        {
            var created = await ImportsService.CreateBatchAsync(
                ImportContext,
                new MasterDataImportBatchRequest(
                    kind,
                    new MasterDataImportSourceRequest("migration-sql-fixture", null, key),
                    null,
                    MasterDataImportDuplicatePolicy.Reject,
                    MasterDataImportMode.Commit,
                    [new MasterDataImportRowInput(1, fields)]),
                $"fixture-{key}");
            Assert.True(created.Succeeded, created.Code);
            var simulated = await ImportsService.SimulateAsync(ImportContext, created.Value!.Id);
            Assert.True(simulated.Succeeded, simulated.Code);
            var executed = await ImportsService.ExecuteAsync(ImportContext, created.Value.Id, simulated.Value!.Version);
            Assert.True(executed.Succeeded, executed.Code);
            Assert.Equal(MasterDataImportStatus.Completed, executed.Value!.Status);
        }

        internal async Task<PreparedRun> PrepareAsync(Guid categoryId, Guid unitId)
        {
            var run = MigrationRun.Create(
                Tenant,
                new MigrationDefinitionReference("tenant-onboarding.foundation", "1"),
                new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(Guid.NewGuid(), Tenant.TenantId, null, null, null, SourceHash, 3, 1);
            var intakeKey = new MigrationIdempotencyKey($"sql-intake-{Guid.NewGuid():N}");
            var intake = await Migration.CreateIntakeAsync(
                Tenant,
                new CreateMigrationIntakeCommand(
                    run,
                    MigrationOperationKind.Validation,
                    intakeKey,
                    new MigrationRequestFingerprint("sql-intake-fingerprint"),
                    MigrationIntakeFingerprint.Version,
                    source));
            Assert.True(intake.Succeeded, intake.Code);
            Assert.True((await Migration.SetEvidenceStateAsync(
                Tenant,
                new MigrationEvidenceReference(run.RunId, MigrationOperationKind.Validation, intakeKey.Value),
                true)).Succeeded);

            var current = (await Migration.FindRunAsync(Tenant, run.RunId))!;
            var payloads = new (MigrationCanonicalRecordType Type, string SourceId, string Payload)[]
            {
                (MigrationCanonicalRecordType.Supplier, "supplier-sql", JsonSerializer.Serialize(new MigrationSupplierPayload("SUP-SQL-REAL", "SQL Supplier"))),
                (MigrationCanonicalRecordType.Customer, "customer-sql", JsonSerializer.Serialize(new MigrationCustomerPayload("CUS-SQL-REAL", "SQL Customer"))),
                (MigrationCanonicalRecordType.Product, "product-sql", JsonSerializer.Serialize(new MigrationProductPayload("SKU-SQL-REAL", "SQL Product", null, null, categoryId, unitId)))
            };
            var staged = payloads.Select((item, index) =>
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Payload)));
                return new MigrationStagedRecord(Guid.NewGuid(), Tenant.TenantId, run.RunId, index + 1, item.SourceId, item.Type, item.Payload, hash, PackageHash, MigrationCanonicalPackageParser.Version, source.ObjectId, SourceHash, DateTimeOffset.UtcNow);
            }).ToArray();
            var stagedResult = await Migration.StagePackageAsync(
                Tenant,
                new StageMigrationPackageCommand(run.RunId, source.ObjectId, SourceHash, PackageHash, MigrationCanonicalPackageParser.Version, DateTimeOffset.UtcNow, staged));
            Assert.True(stagedResult.Succeeded, stagedResult.Code);

            current = await TransitionAsync(current, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, "sql-validation", "sql-validation-fingerprint");
            var validation = new MigrationValidationSummary(
                Guid.NewGuid(), Tenant.TenantId, run.RunId, validationAttempt.AttemptId, PackageHash, SourceHash, staged.Length, staged.Length, 0, 0,
                new Dictionary<string, int>(), staged.Select(item => new MigrationValidationRecordResult(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, [])).ToArray(), DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []))).Succeeded);
            current = await CompleteAttemptAsync(current, validationAttempt, MigrationAttemptOutcome.Succeeded, "validation_completed");
            current = await TransitionAsync(current, MigrationRunStatus.Validated);

            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, "sql-dry-run", "sql-dry-run-fingerprint");
            var dryRun = new MigrationDryRunPreview(
                Guid.NewGuid(), Tenant.TenantId, run.RunId, dryAttempt.AttemptId, validationAttempt.AttemptId, PackageHash, SourceHash, staged.Length, staged.Length, 0, 0,
                new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0,
                staged.Select(item => new MigrationPreviewRow(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null)).ToArray(), DateTimeOffset.UtcNow);
            Assert.True((await Migration.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun))).Succeeded);
            current = await CompleteAttemptAsync(current, dryAttempt, MigrationAttemptOutcome.Succeeded, "dry_run_completed");
            current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, $"sql-execution-{Guid.NewGuid():N}");
        }

        private async Task<MigrationRunRecord> TransitionAsync(MigrationRunRecord current, MigrationRunStatus target)
        {
            var result = await new MigrationFoundationService(Migration, new NoopAuditSink()).TransitionRunAsync(Request, current.RunId, target, current.Version);
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        private async Task<MigrationAttemptRecord> StartAttemptAsync(MigrationRunRecord current, MigrationOperationKind operation, string key, string fingerprint)
        {
            var result = await new MigrationFoundationService(Migration, new NoopAuditSink()).StartAttemptAsync(Request, current.RunId, operation, key, fingerprint);
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        private async Task<MigrationRunRecord> CompleteAttemptAsync(MigrationRunRecord current, MigrationAttemptRecord attempt, MigrationAttemptOutcome outcome, string code)
        {
            var result = await new MigrationFoundationService(Migration, new NoopAuditSink()).RecordAttemptOutcomeAsync(Request, current.RunId, attempt.AttemptId, outcome, code, attempt.Version);
            Assert.True(result.Succeeded, result.Code);
            return (await Migration.FindRunAsync(Tenant, current.RunId))!;
        }

        internal sealed record PreparedRun(MigrationRunRecord Run, string ExecutionKey);
    }

    private sealed class OrderingOwnerGateway(
        IOwnerExecutionGateway inner,
        MigrationPersistence migration,
        TenantContext tenant) : IOwnerExecutionGateway
    {
        internal Guid RunId { get; set; }
        internal MigrationRunStatus? StatusBeforeOwnerExecute { get; private set; }

        public Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(FoundationRequestContext context, OwnerImportRequest request, CancellationToken cancellationToken = default) => inner.CreateBatchAsync(context, request, cancellationToken);

        public Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(FoundationRequestContext context, Guid batchId, CancellationToken cancellationToken = default) => inner.SimulateAsync(context, batchId, cancellationToken);

        public async Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(FoundationRequestContext context, Guid batchId, byte[] expectedVersion, CancellationToken cancellationToken = default)
        {
            StatusBeforeOwnerExecute = (await migration.FindRunAsync(tenant, RunId, cancellationToken))!.Status;
            return await inner.ExecuteAsync(context, batchId, expectedVersion, cancellationToken);
        }

        public Task<OwnerEvidence?> ReadEvidenceAsync(FoundationRequestContext context, Guid batchId, CancellationToken cancellationToken = default) => inner.ReadEvidenceAsync(context, batchId, cancellationToken);
    }

    private sealed class TenantWideScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) => TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));

        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) => TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class AllCapabilities : IMasterDataCapabilityResolver
    {
        private static readonly IReadOnlySet<MasterDataCapability> Capabilities = Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => Capabilities;
    }

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
