#pragma warning disable CS1591

using Microsoft.Extensions.Configuration;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.Modules.Identity;

/// <summary>
/// Resolves human Tenant labels from server-owned configuration. No client
/// supplied label participates in authorization or context selection.
/// </summary>
internal interface ITenantDisplayNameProvider
{
    string GetDisplayName(TenantId tenantId);
    string? GetArabicDisplayName(TenantId tenantId);
}

internal sealed class ConfiguredTenantDisplayNameProvider : ITenantDisplayNameProvider
{
    private readonly IReadOnlyDictionary<Guid, string> configuredNames;
    private readonly IReadOnlyDictionary<Guid, string> configuredArabicNames;

    public ConfiguredTenantDisplayNameProvider(IConfiguration configuration)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var entry in configuration.GetSection("MESP_TENANT_DISPLAY_NAMES").GetChildren())
        {
            if (Guid.TryParse(entry.Key, out var tenantId)
                && !string.IsNullOrWhiteSpace(entry.Value))
            {
                names[tenantId] = entry.Value.Trim();
            }
        }

        var arabicNames = new Dictionary<Guid, string>();
        foreach (var entry in configuration.GetSection("MESP_TENANT_BRANDING").GetChildren())
        {
            if (Guid.TryParse(entry.Key, out var tenantId)
                && !string.IsNullOrWhiteSpace(entry["ArabicDisplayName"]))
            {
                arabicNames[tenantId] = entry["ArabicDisplayName"]!.Trim();
            }
        }

        var developmentName = configuration["MESP_DEV_TENANT_DISPLAY_NAME"];
        if (!string.IsNullOrWhiteSpace(developmentName))
        {
            names[DevelopmentBootstrap.DevTenantId.Value] = developmentName.Trim();
        }

        configuredNames = names;
        configuredArabicNames = arabicNames;
    }

    public string GetDisplayName(TenantId tenantId) =>
        configuredNames.TryGetValue(tenantId.Value, out var displayName)
            ? displayName
            : "MESP";

    public string? GetArabicDisplayName(TenantId tenantId) =>
        configuredArabicNames.TryGetValue(tenantId.Value, out var displayName)
            ? displayName
            : null;
}

internal sealed class DefaultTenantDisplayNameProvider : ITenantDisplayNameProvider
{
    public string GetDisplayName(TenantId tenantId) => "MESP";

    public string? GetArabicDisplayName(TenantId tenantId) => null;
}

#pragma warning restore CS1591
