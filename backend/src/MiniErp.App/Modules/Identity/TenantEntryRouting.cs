#pragma warning disable CS1591

using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.Modules.Identity;

/// <summary>High-level route selected by the normalized request host.</summary>
public enum TenantEntryMode
{
    CommonHost = 1,
    TenantHost = 2,
    PlatformAdminHost = 3,
    NoAccess = 4
}

/// <summary>Configuration-led binding between a host and one Tenant.</summary>
public sealed record TenantHostBinding(
    string Host,
    TenantId TenantId,
    bool Active,
    string CanonicalHost);

/// <summary>Normalized host-routing result. Host data is never authorization.</summary>
public sealed record TenantHostResolution(
    TenantEntryMode Mode,
    TenantHostBinding? Binding,
    string? CanonicalHost,
    bool IsKnown);

/// <summary>
/// Validates and resolves configured hosts. It deliberately treats the host as
/// a candidate routing hint only; membership and platform authority remain in
/// the identity host.
/// </summary>
public sealed class TenantHostRegistry
{
    private static readonly Regex SafeHostPattern = new(
        "^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IReadOnlyDictionary<string, TenantHostBinding> bindings;
    private readonly IReadOnlySet<string> commonHosts;
    private readonly IReadOnlySet<string> platformHosts;

    public TenantHostRegistry(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var parsedBindings = new Dictionary<string, TenantHostBinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in configuration.GetSection("MESP_TENANT_HOST_BINDINGS").GetChildren())
        {
            var host = child["Host"] ?? child["host"] ?? child.Value;
            var tenantText = child["TenantId"] ?? child["tenantId"];
            if (!TryNormalizeHost(host, out var normalizedHost)
                || !Guid.TryParse(tenantText, out var tenantId)
                || tenantId == Guid.Empty)
            {
                throw new InvalidOperationException("Every MESP tenant host binding must contain one valid host and TenantId.");
            }

            if (!bool.TryParse(child["Active"] ?? child["active"], out var active))
            {
                active = true;
            }

            var configuredCanonical = child["CanonicalHost"] ?? child["canonicalHost"];
            var canonicalHost = string.IsNullOrWhiteSpace(configuredCanonical)
                ? normalizedHost
                : NormalizeRequired(configuredCanonical, "CanonicalHost");
            if (parsedBindings.ContainsKey(normalizedHost))
            {
                throw new InvalidOperationException($"Duplicate tenant host binding for '{normalizedHost}'.");
            }

            parsedBindings.Add(
                normalizedHost,
                new TenantHostBinding(normalizedHost, new TenantId(tenantId), active, canonicalHost));
        }

        var common = ReadHostSet(
            configuration,
            "MESP_ENTRY_COMMON_HOSTS",
            ["localhost", "127.0.0.1", "::1", "mesp.localhost"]);
        var platform = ReadHostSet(
            configuration,
            "MESP_ENTRY_PLATFORM_HOSTS",
            ["admin.localhost", "admin.mesp.localhost"]);

        foreach (var host in parsedBindings.Keys)
        {
            if (common.Contains(host) || platform.Contains(host))
            {
                throw new InvalidOperationException($"Tenant host '{host}' collides with a common or platform host.");
            }
        }

        var canonicalOwners = new Dictionary<string, TenantId>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in parsedBindings.Values)
        {
            if (!parsedBindings.TryGetValue(binding.CanonicalHost, out var canonicalBinding)
                || canonicalBinding.TenantId != binding.TenantId
                || binding.Active && !canonicalBinding.Active
                || !string.Equals(canonicalBinding.CanonicalHost, binding.CanonicalHost, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Canonical Tenant host '{binding.CanonicalHost}' must resolve to a canonical binding for the same Tenant; active aliases require an active canonical binding.");
            }

            if (canonicalOwners.TryGetValue(binding.CanonicalHost, out var existingTenant)
                && existingTenant != binding.TenantId)
            {
                throw new InvalidOperationException($"Canonical Tenant host '{binding.CanonicalHost}' is assigned to more than one Tenant.");
            }

            canonicalOwners[binding.CanonicalHost] = binding.TenantId;
            if ((parsedBindings.TryGetValue(binding.CanonicalHost, out var hostOwner)
                    && hostOwner.TenantId != binding.TenantId)
                || common.Contains(binding.CanonicalHost)
                || platform.Contains(binding.CanonicalHost))
            {
                throw new InvalidOperationException($"Canonical Tenant host '{binding.CanonicalHost}' collides with another entry host.");
            }
        }

        foreach (var host in common)
        {
            if (platform.Contains(host))
            {
                throw new InvalidOperationException($"Host '{host}' cannot be both common and platform administration.");
            }
        }

        bindings = parsedBindings;
        commonHosts = common;
        platformHosts = platform;
    }

    public IReadOnlyList<TenantHostBinding> Bindings => bindings.Values.OrderBy(item => item.Host, StringComparer.Ordinal).ToArray();

    public TenantHostResolution Resolve(string? rawHost)
    {
        if (!TryNormalizeHost(rawHost, out var host))
        {
            return new TenantHostResolution(TenantEntryMode.NoAccess, null, null, false);
        }

        if (bindings.TryGetValue(host, out var binding))
        {
            return binding.Active
                ? new TenantHostResolution(TenantEntryMode.TenantHost, binding, binding.CanonicalHost, true)
                : new TenantHostResolution(TenantEntryMode.NoAccess, null, null, true);
        }

        if (platformHosts.Contains(host))
        {
            return new TenantHostResolution(TenantEntryMode.PlatformAdminHost, null, host, true);
        }

        if (commonHosts.Contains(host))
        {
            return new TenantHostResolution(TenantEntryMode.CommonHost, null, host, true);
        }

        return new TenantHostResolution(TenantEntryMode.NoAccess, null, null, false);
    }

    public string? GetCanonicalHost(TenantId tenantId)
    {
        var binding = bindings.Values.FirstOrDefault(item => item.Active && item.TenantId == tenantId);
        return binding?.CanonicalHost;
    }

    public static bool TryNormalizeHost(string? rawHost, out string normalizedHost)
    {
        normalizedHost = string.Empty;
        if (string.IsNullOrWhiteSpace(rawHost))
        {
            return false;
        }

        var candidate = rawHost.Trim();
        if (candidate.Contains("//", StringComparison.Ordinal)
            || candidate.Contains('/', StringComparison.Ordinal)
            || candidate.Contains('?', StringComparison.Ordinal)
            || candidate.Contains('#', StringComparison.Ordinal)
            || candidate.Contains('@', StringComparison.Ordinal)
            || candidate.Any(char.IsControl))
        {
            return false;
        }

        if (candidate.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = candidate.IndexOf(']');
            if (closingBracket <= 1)
            {
                return false;
            }

            var address = candidate[1..closingBracket];
            var suffix = candidate[(closingBracket + 1)..];
            if (!string.IsNullOrEmpty(suffix)
                && (!suffix.StartsWith(":", StringComparison.Ordinal) || !int.TryParse(suffix[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535))
            {
                return false;
            }

            return IPAddress.TryParse(address, out var ip)
                && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                && TryNormalizeAsciiHost(ip.ToString(), out normalizedHost, allowIp: true);
        }

        var colonCount = candidate.Count(character => character == ':');
        if (colonCount == 1)
        {
            var separator = candidate.LastIndexOf(':');
            var portText = candidate[(separator + 1)..];
            if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                || port is < 1 or > 65535)
            {
                return false;
            }

            candidate = candidate[..separator];
        }
        else if (colonCount > 1)
        {
            return IPAddress.TryParse(candidate, out var ip)
                && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                && TryNormalizeAsciiHost(ip.ToString(), out normalizedHost, allowIp: true);
        }

        return TryNormalizeAsciiHost(candidate, out normalizedHost, allowIp: true);
    }

    private static string NormalizeRequired(string raw, string settingName) =>
        TryNormalizeHost(raw, out var normalized)
            ? normalized
            : throw new InvalidOperationException($"{settingName} must contain a valid host.");

    private static IReadOnlySet<string> ReadHostSet(
        IConfiguration configuration,
        string settingName,
        IReadOnlyList<string> defaults)
    {
        var values = new List<string>();
        var direct = configuration[settingName];
        if (!string.IsNullOrWhiteSpace(direct))
        {
            values.AddRange(direct.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var child in configuration.GetSection(settingName).GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                values.Add(child.Value);
            }
        }

        if (values.Count == 0)
        {
            values.AddRange(defaults);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (!TryNormalizeHost(value, out var normalized))
            {
                throw new InvalidOperationException($"{settingName} contains an invalid host.");
            }

            result.Add(normalized);
        }

        return result;
    }

    private static bool TryNormalizeAsciiHost(string candidate, out string normalized, bool allowIp)
    {
        normalized = string.Empty;
        candidate = candidate.Trim().TrimEnd('.');
        if (candidate.Length == 0 || candidate.Length > 253)
        {
            return false;
        }

        if (IPAddress.TryParse(candidate, out var ip))
        {
            if (!allowIp)
            {
                return false;
            }

            normalized = ip.ToString().ToLowerInvariant();
            return true;
        }

        try
        {
            var ascii = new IdnMapping().GetAscii(candidate).TrimEnd('.').ToLowerInvariant();
            if (ascii.Length == 0 || ascii.Length > 253 || !SafeHostPattern.IsMatch(ascii))
            {
                return false;
            }

            normalized = ascii;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>One configuration-led Company or Branch candidate.</summary>
public sealed record FoundationOperationalContextOption(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    string CompanyDisplayName,
    string? BranchDisplayName,
    Guid? ContextId = null,
    bool Active = true);

/// <summary>Safe operational candidate used by the identity host.</summary>
public sealed record FoundationOperationalContextCandidate(
    Guid ContextId,
    Guid TenantId,
    string Kind,
    Guid TargetId,
    string DisplayName,
    long EligibilityVersion);

public interface IFoundationOperationalContextProvider
{
    IReadOnlyList<FoundationOperationalContextCandidate> List(TenantId tenantId);

    bool TryGet(Guid contextId, out FoundationOperationalContextCandidate candidate);
}

public sealed class NoFoundationOperationalContextProvider : IFoundationOperationalContextProvider
{
    public IReadOnlyList<FoundationOperationalContextCandidate> List(TenantId tenantId) => [];

    public bool TryGet(Guid contextId, out FoundationOperationalContextCandidate candidate)
    {
        candidate = null!;
        return false;
    }
}

public sealed class ConfiguredFoundationOperationalContextProvider : IFoundationOperationalContextProvider
{
    private readonly IReadOnlyList<FoundationOperationalContextCandidate> candidates;

    public ConfiguredFoundationOperationalContextProvider(IEnumerable<FoundationOperationalContextOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        candidates = options
            .Where(option => option.Active
                && option.TenantId != Guid.Empty
                && option.CompanyId != Guid.Empty
                && (!option.BranchId.HasValue || option.BranchId.Value != Guid.Empty)
                && !string.IsNullOrWhiteSpace(option.CompanyDisplayName))
            .Select(CreateCandidate)
            .GroupBy(item => item.ContextId)
            .Select(group => group.Count() == 1
                ? group.Single()
                : throw new InvalidOperationException("Operational context identifiers must be unique."))
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.ContextId)
            .ToArray();
    }

    public IReadOnlyList<FoundationOperationalContextCandidate> List(TenantId tenantId) =>
        candidates.Where(item => item.TenantId == tenantId.Value).ToArray();

    public bool TryGet(Guid contextId, out FoundationOperationalContextCandidate candidate)
    {
        candidate = candidates.SingleOrDefault(item => item.ContextId == contextId)!;
        return candidate is not null;
    }

    private static FoundationOperationalContextCandidate CreateCandidate(FoundationOperationalContextOption option)
    {
        var isBranch = option.BranchId.HasValue;
        var targetId = option.BranchId ?? option.CompanyId;
        var kind = isBranch ? "Branch" : "Company";
        var displayName = isBranch && !string.IsNullOrWhiteSpace(option.BranchDisplayName)
            ? $"{option.CompanyDisplayName.Trim()} - {option.BranchDisplayName.Trim()}"
            : option.CompanyDisplayName.Trim();
        var contextId = option.ContextId is { } configured && configured != Guid.Empty
            ? configured
            : StableContextId(option.TenantId, option.CompanyId, option.BranchId);
        return new FoundationOperationalContextCandidate(
            contextId,
            option.TenantId,
            kind,
            targetId,
            displayName,
            1);
    }

    private static Guid StableContextId(Guid tenantId, Guid companyId, Guid? branchId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:D}|{companyId:D}|{branchId:D}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}

/// <summary>Tenant-specific branding profile with safe relative asset paths.</summary>
public sealed record FoundationTenantBrandingProfile(
    TenantId TenantId,
    string DisplayName,
    string? LogoLightUrl,
    string? LogoDarkUrl,
    string LogoAltText,
    string CurrencyCode,
    string? CurrencySymbolAssetUrl,
    string CurrencySymbolTextFallback,
    string? DefaultTheme,
    bool TenantConfigured,
    string? ArabicDisplayName);

public interface IFoundationTenantBrandingProvider
{
    FoundationTenantBrandingProfile Get(TenantId tenantId);
}

internal sealed class ConfiguredFoundationTenantBrandingProvider : IFoundationTenantBrandingProvider
{
    private readonly IConfiguration configuration;
    private readonly ITenantDisplayNameProvider displayNames;

    public ConfiguredFoundationTenantBrandingProvider(
        IConfiguration configuration,
        ITenantDisplayNameProvider displayNames)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.displayNames = displayNames ?? throw new ArgumentNullException(nameof(displayNames));
    }

    public FoundationTenantBrandingProfile Get(TenantId tenantId)
    {
        var section = configuration.GetSection($"MESP_TENANT_BRANDING:{tenantId.Value:D}");
        var displayName = string.IsNullOrWhiteSpace(section["DisplayName"])
            ? displayNames.GetDisplayName(tenantId)
            : section["DisplayName"]!.Trim();
        var currencyCode = string.IsNullOrWhiteSpace(section["CurrencyCode"])
            ? "SAR"
            : section["CurrencyCode"]!.Trim().ToUpperInvariant();
        var fallback = string.IsNullOrWhiteSpace(section["CurrencySymbolTextFallback"])
            ? currencyCode
            : section["CurrencySymbolTextFallback"]!.Trim();
        return new FoundationTenantBrandingProfile(
            tenantId,
            displayName,
            SafeAssetPath(section["LogoLightUrl"]),
            SafeAssetPath(section["LogoDarkUrl"]),
            string.IsNullOrWhiteSpace(section["LogoAltText"]) ? displayName : section["LogoAltText"]!.Trim(),
            currencyCode,
            SafeAssetPath(section["CurrencySymbolAssetUrl"]),
            fallback,
            string.IsNullOrWhiteSpace(section["DefaultTheme"]) ? null : section["DefaultTheme"]!.Trim(),
            section.Exists(),
            displayNames.GetArabicDisplayName(tenantId));
    }

    private static string? SafeAssetPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.Contains("..", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('\r')
            || value.Contains('\n')
            || value.StartsWith("//", StringComparison.Ordinal)
            || value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.StartsWith("/assets/", StringComparison.Ordinal)
            || value.StartsWith("assets/", StringComparison.Ordinal)
            ? value
            : null;
    }
}

public sealed record FoundationOperationalContextSwitchResult(
    bool Succeeded,
    string Code,
    FoundationOperationalContextCandidate? SelectedContext,
    long SelectionVersion);

/// <summary>Server-owned tenant entry and presentation authority.</summary>
public interface ITenantEntryAuthority
{
    TenantHostResolution Prepare(
        ClaimsPrincipal principal,
        string? rawHost,
        bool activateCommonHost = true);

    FoundationEntryResponse BuildResponse(
        ClaimsPrincipal principal,
        string? rawHost,
        bool isDevelopment = false,
        string? developmentAccountHint = null);
}

internal sealed class TenantEntryAuthority : ITenantEntryAuthority
{
    private readonly TenantHostRegistry hosts;
    private readonly IFoundationIdentityHost identityHost;
    private readonly IFoundationTenantBrandingProvider branding;

    public TenantEntryAuthority(
        TenantHostRegistry hosts,
        IFoundationIdentityHost identityHost,
        IFoundationTenantBrandingProvider branding)
    {
        this.hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
        this.identityHost = identityHost ?? throw new ArgumentNullException(nameof(identityHost));
        this.branding = branding ?? throw new ArgumentNullException(nameof(branding));
    }

    public TenantHostResolution Prepare(
        ClaimsPrincipal principal,
        string? rawHost,
        bool activateCommonHost = true)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var resolution = hosts.Resolve(rawHost);
        if (!identityHost.GetSession(principal).Authenticated)
        {
            return resolution;
        }

        var contexts = identityHost.ListContexts(principal);
        switch (resolution.Mode)
        {
            case TenantEntryMode.TenantHost:
                var tenantCandidate = contexts.SingleOrDefault(item =>
                    item.Kind == FoundationHostContextKind.OrdinaryMembership
                    && item.TenantId == resolution.Binding!.TenantId.Value);
                if (tenantCandidate is null)
                {
                    identityHost.ClearSelectedContext(principal);
                }
                else
                {
                    identityHost.SelectAuthorizedContext(principal, tenantCandidate.ContextId);
                }

                break;
            case TenantEntryMode.CommonHost:
                var current = identityHost.GetSession(principal).SelectedContext;
                if (current is not null
                    && contexts.Any(item => item.ContextId == current.ContextId)
                    && (current.Kind == FoundationHostContextKind.OrdinaryMembership || !activateCommonHost))
                {
                    break;
                }

                identityHost.ClearSelectedContext(principal);

                break;
            case TenantEntryMode.PlatformAdminHost:
                var platformCandidate = contexts.SingleOrDefault(item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);
                if (platformCandidate is null)
                {
                    identityHost.ClearSelectedContext(principal);
                }
                else
                {
                    identityHost.SelectAuthorizedContext(principal, platformCandidate.ContextId);
                }

                break;
            default:
                identityHost.ClearSelectedContext(principal);
                break;
        }

        if ((resolution.Mode == TenantEntryMode.TenantHost
                || resolution.Mode == TenantEntryMode.CommonHost && activateCommonHost)
            && identityHost.GetSession(principal).SelectedContext?.Kind == FoundationHostContextKind.OrdinaryMembership)
        {
            var operational = identityHost.ListOperationalContexts(principal);
            var selectedOperational = identityHost.GetSelectedOperationalContext(principal);
            if (operational.Count == 1)
            {
                identityHost.SelectAuthorizedOperationalContext(principal, operational[0].ContextId);
            }
            else if (operational.Count == 0
                || selectedOperational is not null && operational.All(item => item.ContextId != selectedOperational.ContextId))
            {
                identityHost.ClearSelectedOperationalContext(principal);
            }
        }

        return resolution;
    }

    public FoundationEntryResponse BuildResponse(
        ClaimsPrincipal principal,
        string? rawHost,
        bool isDevelopment = false,
        string? developmentAccountHint = null)
    {
        var resolution = Prepare(principal, rawHost);
        var state = identityHost.GetSession(principal);
        if (!state.Authenticated)
        {
            if (resolution.Mode == TenantEntryMode.NoAccess)
            {
                return EmptyResponse(resolution, "access_denied", isDevelopment);
            }

            var publicTenantBranding = resolution.Mode == TenantEntryMode.TenantHost
                && resolution.Binding is { TenantId: { } publicTenantId }
                ? branding.Get(new TenantId(publicTenantId.Value))
                : null;
            var anonymousBranding = publicTenantBranding is { TenantConfigured: true }
                ? new FoundationBrandingResponse(
                    publicTenantBranding.DisplayName,
                    publicTenantBranding.LogoLightUrl,
                    publicTenantBranding.LogoDarkUrl,
                    publicTenantBranding.LogoAltText,
                    true,
                    publicTenantBranding.DefaultTheme,
                    publicTenantBranding.ArabicDisplayName)
                : new FoundationBrandingResponse("MESP", null, null, "MESP", false);
            return new FoundationEntryResponse(
                resolution.Mode.ToString(),
                resolution.CanonicalHost,
                null,
                null,
                [],
                [],
                null,
                0,
                anonymousBranding,
                new FoundationCurrencyPresentationResponse("SAR", null, "SAR"),
                null,
                isDevelopment,
                developmentAccountHint);
        }

        // Unknown, disabled, or otherwise invalid hosts are a safe no-access
        // state. In particular, they must not become a Tenant directory.
        if (resolution.Mode == TenantEntryMode.NoAccess)
        {
            return EmptyResponse(resolution, "access_denied", isDevelopment);
        }

        var allAuthorizedTenants = identityHost.ListContexts(principal)
            .Where(item => item.Kind == FoundationHostContextKind.OrdinaryMembership && item.TenantId.HasValue)
            .GroupBy(item => item.TenantId!.Value)
            .Select(group =>
            {
                var tenantId = new TenantId(group.Key);
                return new FoundationTenantCandidateResponse(
                    group.Key,
                    group.First().DisplayName,
                    hosts.GetCanonicalHost(tenantId),
                    group.First().ArabicDisplayName);
            })
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToArray();

        var selectedTenant = state.SelectedContext is { Kind: FoundationHostContextKind.OrdinaryMembership, TenantId: { } selectedTenantId }
            ? selectedTenantId
            : (Guid?)null;
        var authorizedTenants = resolution.Mode switch
        {
            TenantEntryMode.CommonHost => allAuthorizedTenants,
            TenantEntryMode.TenantHost when resolution.Binding is { TenantId: { } hostTenantId }
                && allAuthorizedTenants.Any(item => item.TenantId == hostTenantId.Value)
                => allAuthorizedTenants.Where(item => item.TenantId == hostTenantId.Value).ToArray(),
            _ => []
        };
        var selectedTenantName = selectedTenant is { } tenant
            ? authorizedTenants.SingleOrDefault(item => item.TenantId == tenant)?.DisplayName
            : null;
        var operationCandidates = selectedTenant is { } authorizedTenant
            ? identityHost.ListOperationalContexts(principal)
                .Select(ToOperationalResponse)
                .ToArray()
            : [];
        var selectedOperational = identityHost.GetSelectedOperationalContext(principal);
        var tenantBranding = resolution.Mode switch
        {
            TenantEntryMode.TenantHost when selectedTenant is { } tenantHostTenantId
                => branding.Get(new TenantId(tenantHostTenantId)),
            TenantEntryMode.CommonHost when selectedTenant is { } commonHostTenantId
                && authorizedTenants.Any(item => item.TenantId == commonHostTenantId)
                => branding.Get(new TenantId(commonHostTenantId)),
            _ => null
        };
        if (tenantBranding is { TenantConfigured: false })
        {
            tenantBranding = null;
        }
        var presentation = tenantBranding is null
            ? new FoundationCurrencyPresentationResponse("SAR", null, "SAR")
            : new FoundationCurrencyPresentationResponse(
                tenantBranding.CurrencyCode,
                tenantBranding.CurrencySymbolAssetUrl,
                tenantBranding.CurrencySymbolTextFallback);
        var brand = tenantBranding is null
            ? new FoundationBrandingResponse("MESP", null, null, "MESP", false)
            : new FoundationBrandingResponse(
                tenantBranding.DisplayName,
                tenantBranding.LogoLightUrl,
                tenantBranding.LogoDarkUrl,
                tenantBranding.LogoAltText,
                tenantBranding.TenantConfigured,
                tenantBranding.DefaultTheme,
                tenantBranding.ArabicDisplayName);
        var entryMode = resolution.Mode switch
        {
            TenantEntryMode.CommonHost when !authorizedTenants.Any()
                => TenantEntryMode.NoAccess.ToString(),
            TenantEntryMode.TenantHost when selectedTenant is null => TenantEntryMode.NoAccess.ToString(),
            TenantEntryMode.PlatformAdminHost when state.SelectedContext?.Kind != FoundationHostContextKind.PlatformGovernanceContext
                => TenantEntryMode.NoAccess.ToString(),
            _ => resolution.Mode.ToString()
        };
        var responseCanonicalHost = entryMode == TenantEntryMode.NoAccess.ToString()
            ? null
            : resolution.Mode == TenantEntryMode.CommonHost
                && selectedTenant is { } selectedCommonTenant
                ? hosts.GetCanonicalHost(new TenantId(selectedCommonTenant))
                : resolution.CanonicalHost;
        return new FoundationEntryResponse(
            entryMode,
            responseCanonicalHost,
            selectedTenant,
            selectedTenantName,
            authorizedTenants,
            operationCandidates,
            selectedOperational?.ContextId,
            identityHost.GetOperationalSelectionVersion(principal),
            brand,
            presentation,
            entryMode == TenantEntryMode.NoAccess.ToString() ? "access_denied" : null,
            isDevelopment,
            entryMode == TenantEntryMode.NoAccess.ToString() ? null : developmentAccountHint);
    }

    private static FoundationEntryResponse EmptyResponse(TenantHostResolution resolution, string code, bool isDevelopment = false) =>
        new(
            TenantEntryMode.NoAccess.ToString(),
            resolution.CanonicalHost,
            null,
            null,
            [],
            [],
            null,
            0,
            new FoundationBrandingResponse("MESP", null, null, "MESP", false),
            new FoundationCurrencyPresentationResponse("SAR", null, "SAR"),
            code,
            isDevelopment);

    private static FoundationOperationalContextResponse ToOperationalResponse(FoundationHostOperationalContextCandidate candidate) =>
        new(candidate.ContextId, candidate.Kind, candidate.DisplayName, candidate.EligibilityVersion);
}

#pragma warning restore CS1591
