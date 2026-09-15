using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationOwnerExecutionAuthorityTests
{
    [Fact]
    public async Task Owner_gateway_requires_the_exact_migration_permission_and_tenant_profile()
    {
        var imports = new MasterDataImportService(
            new MasterDataImportAuthorizationComposition(new AllCapabilities()),
            new UnavailableMasterDataImportPersistence(),
            new MasterDataImportProcessorRegistry(
                MasterDataImportProcessorFactory.Create(
                    new UnavailableMasterDataCatalogPersistence(),
                    new UnavailableProductIdentityPersistence(),
                    new UnavailableMasterDataCurrencyPaymentTermPersistence(),
                    new UnavailableMasterDataTaxPersistence(),
                    new UnavailableMasterDataExchangeRatePersistence(),
                    new UnavailableMasterDataPriceListPersistence(),
                    new UnavailableSupplierPersistence(),
                    new UnavailableCustomerPersistence())));
        var gateway = new MasterDataOwnerExecutionGateway(imports);
        var tenant = TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("owner-authority-test"),
            actorId: Guid.NewGuid());
        var request = new OwnerImportRequest(
            Guid.NewGuid(),
            OwnerResourceKind.Supplier,
            new OwnerImportSource("migration", null, "authority-test"),
            "authority-key",
            "authority-fingerprint",
            [new OwnerImportRowInput(1, new Dictionary<string, string?> { ["code"] = "SUP-1" })]);

        var fabricatedContext = FoundationRequestContext.ForTenant(
            tenant.ActorId!.Value,
            Guid.NewGuid(),
            tenant,
            "tenant.master-data.import");
        var denied = await gateway.CreateBatchAsync(fabricatedContext, request);

        Assert.False(denied.Succeeded);
        Assert.Equal("migration_execution_authority_required", denied.Code);
        Assert.Equal(403, denied.StatusCode);
        Assert.Null(await gateway.ReadEvidenceAsync(fabricatedContext, request.BatchId));
    }

    private sealed class AllCapabilities : IMasterDataCapabilityResolver
    {
        private static readonly IReadOnlySet<MasterDataCapability> Capabilities = Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => Capabilities;
    }
}
