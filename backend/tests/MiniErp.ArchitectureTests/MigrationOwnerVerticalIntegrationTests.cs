using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationOwnerVerticalIntegrationTests
{
    [Fact]
    public async Task Real_owner_gateway_creates_supplier_customer_product_and_preserves_tenant_isolation_on_replay()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ImportReferenceAsync(
            MasterDataResourceKind.ProductCategory,
            new Dictionary<string, string?> { ["code"] = "CAT-REAL", ["englishName"] = "Real category" },
            "category-real");
        await fixture.ImportReferenceAsync(
            MasterDataResourceKind.UnitOfMeasure,
            new Dictionary<string, string?> { ["code"] = "EA", ["englishName"] = "Each" },
            "unit-real");
        var categoryId = Assert.Single(await fixture.Catalog.ListCategoriesAsync(fixture.Tenant)).Id;
        var unitId = Assert.Single(await fixture.Catalog.ListUnitsOfMeasureAsync(fixture.Tenant)).Id;
        Assert.NotEqual(Guid.Empty, categoryId);
        Assert.NotEqual(Guid.Empty, unitId);
        var references = await fixture.Catalog.ValidateReferencesAsync(fixture.Tenant, categoryId, unitId);
        Assert.True(references.IsValid, references.Code);

        await fixture.ImportOwnerAsync(
            new OwnerImportRequest(
                Guid.NewGuid(),
                OwnerResourceKind.Supplier,
                new OwnerImportSource("migration", null, "supplier-real"),
                "supplier-real-key",
                "supplier-real-fingerprint",
                [new OwnerImportRowInput(1, new Dictionary<string, string?>
                {
                    ["code"] = "SUP-REAL",
                    ["legalNameEnglish"] = "Real Supplier"
                })]));
        await fixture.ImportOwnerAsync(
            new OwnerImportRequest(
                Guid.NewGuid(),
                OwnerResourceKind.Customer,
                new OwnerImportSource("migration", null, "customer-real"),
                "customer-real-key",
                "customer-real-fingerprint",
                [new OwnerImportRowInput(1, new Dictionary<string, string?>
                {
                    ["code"] = "CUS-REAL",
                    ["legalNameEnglish"] = "Real Customer"
                })]));
        var productRequest = new OwnerImportRequest(
            Guid.NewGuid(),
            OwnerResourceKind.Product,
            new OwnerImportSource("migration", null, "product-real"),
            "product-real-key",
            "product-real-fingerprint",
            [new OwnerImportRowInput(1, new Dictionary<string, string?>
            {
                ["sku"] = "SKU-REAL",
                ["englishName"] = "Real Product",
                ["categoryId"] = categoryId.ToString("D"),
                ["baseUnitOfMeasureId"] = unitId.ToString("D")
            })]);
        await fixture.ImportOwnerAsync(productRequest);
        var replay = await fixture.Gateway.CreateBatchAsync(fixture.OwnerContext, productRequest);

        Assert.True(replay.Succeeded, replay.Code);
        Assert.Single(await fixture.Catalog.ListProductsAsync(fixture.Tenant));
        Assert.Single(await fixture.Suppliers.ListSuppliersAsync(fixture.Tenant));
        Assert.Single(await fixture.Customers.ListCustomersAsync(fixture.Tenant));
        Assert.Null(await fixture.Gateway.ReadEvidenceAsync(fixture.OtherTenantContext, productRequest.BatchId));
        Assert.Empty(await fixture.Catalog.ListProductsAsync(fixture.OtherTenant));
        Assert.Empty(await fixture.Suppliers.ListSuppliersAsync(fixture.OtherTenant));
        Assert.Empty(await fixture.Customers.ListCustomersAsync(fixture.OtherTenant));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection masterConnection;
        private readonly SqliteConnection partiesConnection;

        private Fixture(
            SqliteConnection masterConnection,
            SqliteConnection partiesConnection,
            TenantContext tenant,
            TenantContext otherTenant,
            FoundationRequestContext ownerContext,
            FoundationRequestContext otherTenantContext,
            MasterDataRequestContext importContext,
            MasterDataImportService service,
            MasterDataOwnerExecutionGateway gateway,
            MasterDataCatalogPersistence catalog,
            BusinessPartiesSupplierPersistence suppliers,
            BusinessPartiesCustomerPersistence customers)
        {
            this.masterConnection = masterConnection;
            this.partiesConnection = partiesConnection;
            Tenant = tenant;
            OtherTenant = otherTenant;
            OwnerContext = ownerContext;
            OtherTenantContext = otherTenantContext;
            ImportContext = importContext;
            Service = service;
            Gateway = gateway;
            Catalog = catalog;
            Suppliers = suppliers;
            Customers = customers;
        }

        internal TenantContext Tenant { get; }
        internal TenantContext OtherTenant { get; }
        internal FoundationRequestContext OwnerContext { get; }
        internal FoundationRequestContext OtherTenantContext { get; }
        internal MasterDataRequestContext ImportContext { get; }
        internal MasterDataImportService Service { get; }
        internal MasterDataOwnerExecutionGateway Gateway { get; }
        internal MasterDataCatalogPersistence Catalog { get; }
        internal BusinessPartiesSupplierPersistence Suppliers { get; }
        internal BusinessPartiesCustomerPersistence Customers { get; }

        internal static async Task<Fixture> CreateAsync()
        {
            var masterConnection = new SqliteConnection("Data Source=:memory:");
            var partiesConnection = new SqliteConnection("Data Source=:memory:");
            await masterConnection.OpenAsync();
            await partiesConnection.OpenAsync();
            var masterOptions = new DbContextOptionsBuilder().UseSqlite(masterConnection).Options;
            var partiesOptions = new DbContextOptionsBuilder().UseSqlite(partiesConnection).Options;
            var tenant = TenantContext.ForOrdinaryMembership(
                new TenantId(Guid.NewGuid()),
                new MembershipReference(Guid.NewGuid()),
                correlationId: new CorrelationId("owner-vertical-test"),
                actorId: Guid.NewGuid());
            var otherTenant = TenantContext.ForOrdinaryMembership(
                new TenantId(Guid.NewGuid()),
                new MembershipReference(Guid.NewGuid()),
                correlationId: new CorrelationId("owner-vertical-other"),
                actorId: Guid.NewGuid());
            await using (var db = new MasterDataDbContext(masterOptions, tenant))
                await db.Database.EnsureCreatedAsync();
            await using (var db = new BusinessPartiesDbContext(partiesOptions, tenant))
                await db.Database.EnsureCreatedAsync();

            var catalog = new MasterDataCatalogPersistence(masterOptions);
            var importPersistence = new MasterDataImportPersistence(masterOptions);
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
            var service = new MasterDataImportService(
                new MasterDataImportAuthorizationComposition(new AllCapabilities()),
                importPersistence,
                processors);
            var ownerContext = FoundationRequestContext.ForTenant(tenant.ActorId!.Value, Guid.NewGuid(), tenant, "tenant.migration.execute");
            var otherTenantContext = FoundationRequestContext.ForTenant(otherTenant.ActorId!.Value, Guid.NewGuid(), otherTenant, "tenant.migration.execute");
            var importContext = MasterDataRequestContext.FromFoundationContext(
                FoundationRequestContext.ForTenant(tenant.ActorId!.Value, Guid.NewGuid(), tenant, "tenant.master-data.import"));
            return new Fixture(masterConnection, partiesConnection, tenant, otherTenant, ownerContext, otherTenantContext, importContext, service, new MasterDataOwnerExecutionGateway(service), catalog, suppliers, customers);
        }

        internal async Task<MasterDataImportBatchRecord> ImportReferenceAsync(
            MasterDataResourceKind resourceKind,
            IReadOnlyDictionary<string, string?> fields,
            string key)
        {
            var created = await Service.CreateBatchAsync(
                ImportContext,
                new MasterDataImportBatchRequest(
                    resourceKind,
                    new MasterDataImportSourceRequest("migration-test", null, key),
                    null,
                    MasterDataImportDuplicatePolicy.Reject,
                    MasterDataImportMode.Commit,
                    [new MasterDataImportRowInput(1, fields)]),
                $"create-{key}");
            Assert.True(created.Succeeded, created.Code);
            var simulated = await Service.SimulateAsync(ImportContext, created.Value!.Id);
            Assert.True(simulated.Succeeded, simulated.Code);
            var executed = await Service.ExecuteAsync(ImportContext, created.Value.Id, simulated.Value!.Version);
            Assert.True(executed.Succeeded, executed.Code);
            Assert.Equal(MasterDataImportStatus.Completed, executed.Value!.Status);
            return executed.Value;
        }

        internal async Task ImportOwnerAsync(OwnerImportRequest request)
        {
            var created = await Gateway.CreateBatchAsync(OwnerContext, request);
            Assert.True(created.Succeeded, created.Code);
            var simulated = await Gateway.SimulateAsync(OwnerContext, request.BatchId);
            Assert.True(simulated.Succeeded, simulated.Code);
            Assert.Equal(OwnerBatchStatus.Validated, simulated.Value!.Status);
            var executed = await Gateway.ExecuteAsync(OwnerContext, request.BatchId, simulated.Value.Version);
            Assert.True(executed.Succeeded, executed.Code);
            if (executed.Value!.Status != OwnerBatchStatus.Completed)
            {
                var evidence = await Gateway.ReadEvidenceAsync(OwnerContext, request.BatchId);
                Assert.Fail(string.Join(", ", evidence!.Rows.SelectMany(row => row.Diagnostics.Select(diagnostic => diagnostic.Code))));
            }
        }

        public async ValueTask DisposeAsync()
        {
            await partiesConnection.DisposeAsync();
            await masterConnection.DisposeAsync();
        }

        private sealed class AllCapabilities : IMasterDataCapabilityResolver
        {
            private static readonly IReadOnlySet<MasterDataCapability> Capabilities = Enum.GetValues<MasterDataCapability>().ToHashSet();

            public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => Capabilities;
        }
    }
}
