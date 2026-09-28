using Microsoft.Extensions.Configuration;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class TenantEntryRoutingTests
{
    [Theory]
    [InlineData(" TENANT.EXAMPLE.COM.:443 ", "tenant.example.com")]
    [InlineData("[::1]:443", "::1")]
    [InlineData("127.0.0.1:8443", "127.0.0.1")]
    [InlineData("xn--bcher-kva.example", "xn--bcher-kva.example")]
    public void Host_normalization_removes_transport_detail_but_preserves_route_identity(string raw, string expected)
    {
        Assert.True(TenantHostRegistry.TryNormalizeHost(raw, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("https://tenant.example.com")]
    [InlineData("tenant.example.com/path")]
    [InlineData("tenant.example.com?tenant=other")]
    [InlineData("tenant.example.com:invalid")]
    [InlineData("tenant..example.com")]
    public void Host_normalization_rejects_values_that_could_smuggle_route_data(string raw)
    {
        Assert.False(TenantHostRegistry.TryNormalizeHost(raw, out _));
    }

    [Fact]
    public void Registry_distinguishes_common_tenant_platform_and_disabled_hosts()
    {
        var tenantId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MESP_TENANT_HOST_BINDINGS:0:Host"] = "wafra.example.com.",
                ["MESP_TENANT_HOST_BINDINGS:0:TenantId"] = tenantId.ToString("D"),
                ["MESP_TENANT_HOST_BINDINGS:0:CanonicalHost"] = "wafra.example.com",
                ["MESP_TENANT_HOST_BINDINGS:1:Host"] = "wafra-alias.example.com",
                ["MESP_TENANT_HOST_BINDINGS:1:TenantId"] = tenantId.ToString("D"),
                ["MESP_TENANT_HOST_BINDINGS:1:CanonicalHost"] = "wafra.example.com",
                ["MESP_TENANT_HOST_BINDINGS:2:Host"] = "disabled.example.com",
                ["MESP_TENANT_HOST_BINDINGS:2:TenantId"] = Guid.NewGuid().ToString("D"),
                ["MESP_TENANT_HOST_BINDINGS:2:CanonicalHost"] = "disabled.example.com",
                ["MESP_TENANT_HOST_BINDINGS:2:Active"] = "false",
                ["MESP_ENTRY_COMMON_HOSTS:0"] = "mesp.example.com",
                ["MESP_ENTRY_PLATFORM_HOSTS:0"] = "admin.example.com"
            })
            .Build();
        var registry = new TenantHostRegistry(configuration);

        Assert.Equal(TenantEntryMode.TenantHost, registry.Resolve("WAFRA.EXAMPLE.COM:443").Mode);
        Assert.Equal("wafra.example.com", registry.Resolve("wafra-alias.example.com").CanonicalHost);
        Assert.Equal(tenantId, registry.Resolve("wafra.example.com").Binding!.TenantId.Value);
        Assert.Equal(TenantEntryMode.CommonHost, registry.Resolve("mesp.example.com.").Mode);
        Assert.Equal(TenantEntryMode.PlatformAdminHost, registry.Resolve("admin.example.com").Mode);
        Assert.Equal(TenantEntryMode.NoAccess, registry.Resolve("disabled.example.com").Mode);
        Assert.Equal(TenantEntryMode.NoAccess, registry.Resolve("unknown.example.com").Mode);
    }

    [Fact]
    public void Registry_rejects_host_collisions_at_startup()
    {
        var tenantId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MESP_TENANT_HOST_BINDINGS:0:Host"] = "mesp.example.com",
                ["MESP_TENANT_HOST_BINDINGS:0:TenantId"] = tenantId.ToString("D"),
                ["MESP_ENTRY_COMMON_HOSTS:0"] = "mesp.example.com"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new TenantHostRegistry(configuration));
    }

    [Fact]
    public void Registry_rejects_canonical_host_collisions_across_Tenants()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MESP_TENANT_HOST_BINDINGS:0:Host"] = "one.example.com",
                ["MESP_TENANT_HOST_BINDINGS:0:TenantId"] = Guid.NewGuid().ToString("D"),
                ["MESP_TENANT_HOST_BINDINGS:0:CanonicalHost"] = "shared.example.com",
                ["MESP_TENANT_HOST_BINDINGS:1:Host"] = "two.example.com",
                ["MESP_TENANT_HOST_BINDINGS:1:TenantId"] = Guid.NewGuid().ToString("D"),
                ["MESP_TENANT_HOST_BINDINGS:1:CanonicalHost"] = "shared.example.com"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new TenantHostRegistry(configuration));
    }

    [Fact]
    public void Operational_context_provider_is_tenant_scoped_and_uses_stable_server_ids()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var provider = new ConfiguredFoundationOperationalContextProvider(
        [
            new FoundationOperationalContextOption(tenantA, companyA, null, "Wafra Company", null),
            new FoundationOperationalContextOption(tenantA, companyA, branchA, "Wafra Company", "Riyadh Branch"),
            new FoundationOperationalContextOption(tenantB, Guid.NewGuid(), null, "Other Company", null)
        ]);

        var first = provider.List(new TenantId(tenantA));
        var second = new ConfiguredFoundationOperationalContextProvider(
        [new FoundationOperationalContextOption(tenantA, companyA, branchA, "Wafra Company", "Riyadh Branch")])
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
                [$"MESP_TENANT_DISPLAY_NAMES:{tenantId:D}"] = "Wafra",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:DisplayName"] = "Wafra ERP",
                [$"MESP_TENANT_BRANDING:{tenantId:D}:LogoLightUrl"] = "/assets/wafra/logo-light.svg",
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

        Assert.Equal("Wafra ERP", branding.DisplayName);
        Assert.Equal("/assets/wafra/logo-light.svg", branding.LogoLightUrl);
        Assert.Null(branding.LogoDarkUrl);
        Assert.Null(branding.CurrencySymbolAssetUrl);
        Assert.Equal("SAR", branding.CurrencyCode);
        Assert.Equal("SAR", branding.CurrencySymbolTextFallback);
        Assert.Equal("forest", branding.DefaultTheme);
    }
}
