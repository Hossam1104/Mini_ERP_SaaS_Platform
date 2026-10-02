using Microsoft.Extensions.Configuration;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class TenantEntryRoutingTests
{
    [Fact]
    public void Operational_context_provider_is_tenant_scoped_and_uses_stable_server_ids()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var provider = new ConfiguredFoundationOperationalContextProvider(
        [
            new FoundationOperationalContextOption(tenantA, companyA, null, "Alpha Company", null),
            new FoundationOperationalContextOption(tenantA, companyA, branchA, "Alpha Company", "Riyadh Branch"),
            new FoundationOperationalContextOption(tenantB, Guid.NewGuid(), null, "Other Company", null)
        ]);

        var first = provider.List(new TenantId(tenantA));
        var second = new ConfiguredFoundationOperationalContextProvider(
        [new FoundationOperationalContextOption(tenantA, companyA, branchA, "Alpha Company", "Riyadh Branch")])
            .List(new TenantId(tenantA));

        Assert.Equal(2, first.Count);
        Assert.Single(second);
        Assert.Equal("Branch", first.Single(item => item.TargetId == branchA).Kind);
        Assert.Equal(first.Single(item => item.TargetId == branchA).ContextId, second[0].ContextId);
        Assert.Empty(provider.List(new TenantId(Guid.NewGuid())));
        Assert.True(provider.TryGet(first[0].ContextId, out var candidate));
        Assert.Equal(tenantA, candidate.TenantId);
    }

    [Fact]
    public void Branding_profile_rejects_unsafe_asset_paths_and_keeps_currency_presentational()
    {
        var tenantId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"MESP_TENANT_DISPLAY_NAMES:{tenantId:D}"] = "Example ERP",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:DisplayName"] = "Example ERP",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:ArabicDisplayName"] = "مثال",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:LogoLightUrl"] = "/assets/example/logo-light.svg",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:LogoDarkUrl"] = "../secrets/logo.svg",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:CurrencySymbolAssetUrl"] = "//untrusted.example/riyal.svg",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:CurrencyCode"] = "SAR",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:CurrencySymbolTextFallback"] = "SAR",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:DefaultTheme"] = " forest "
            })
            .Build();
        var names = new ConfiguredTenantDisplayNameProvider(configuration);
        var branding = new ConfiguredFoundationTenantBrandingProvider(configuration, names)
            .Get(new TenantId(tenantId));

        Assert.Equal("Example ERP", branding.DisplayName);
        Assert.Equal("مثال", names.GetArabicDisplayName(new TenantId(tenantId)));
        Assert.Equal("مثال", branding.ArabicDisplayName);
        Assert.Equal("/assets/example/logo-light.svg", branding.LogoLightUrl);
        Assert.Null(branding.LogoDarkUrl);
        Assert.Null(branding.CurrencySymbolAssetUrl);
        Assert.Equal("SAR", branding.CurrencyCode);
        Assert.Equal("SAR", branding.CurrencySymbolTextFallback);
        Assert.Equal("forest", branding.DefaultTheme);
    }
}
