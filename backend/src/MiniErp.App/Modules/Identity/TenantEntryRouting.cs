#pragma warning disable CS1591

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.Modules.Identity;

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

/// <summary>Server-owned account entry and presentation authority.</summary>
public interface ITenantEntryAuthority
{
    FoundationEntryResponse BuildResponse(
        ClaimsPrincipal principal,
        bool isDevelopment = false,
        string? developmentAccountHint = null);
}

internal sealed class TenantEntryAuthority : ITenantEntryAuthority
{
    private readonly IFoundationIdentityHost identityHost;
    private readonly IFoundationTenantBrandingProvider branding;

    public TenantEntryAuthority(
        IFoundationIdentityHost identityHost,
        IFoundationTenantBrandingProvider branding)
    {
        this.identityHost = identityHost ?? throw new ArgumentNullException(nameof(identityHost));
        this.branding = branding ?? throw new ArgumentNullException(nameof(branding));
    }

    public FoundationEntryResponse BuildResponse(
        ClaimsPrincipal principal,
        bool isDevelopment = false,
        string? developmentAccountHint = null)
    {
        var state = identityHost.GetSession(principal);
        if (!state.Authenticated)
        {
            return EntryResponse("SignIn", [], null, 0, MespBranding(), isDevelopment, developmentAccountHint);
        }

        if (state.IsEmergencySuperAdministrator && state.SelectedContext is null)
        {
            return EntryResponse("EmergencySuperAdministrator", [], null, 0, MespBranding(), isDevelopment, developmentAccountHint);
        }

        var tenantId = state.SelectedContext?.Kind is FoundationHostContextKind.OrdinaryMembership or FoundationHostContextKind.EmergencySuperAdministrator
            ? state.SelectedContext.TenantId
            : null;
        if (tenantId is null)
        {
            return EntryResponse("NoAccess", [], null, 0, MespBranding(), isDevelopment, developmentAccountHint, "access_denied");
        }

        var tenantBranding = branding.Get(new TenantId(tenantId.Value));
        if (!tenantBranding.TenantConfigured)
        {
            tenantBranding = null;
        }

        var brand = tenantBranding is null
            ? MespBranding()
            : new FoundationBrandingResponse(
                tenantBranding.DisplayName,
                tenantBranding.LogoLightUrl,
                tenantBranding.LogoDarkUrl,
                tenantBranding.LogoAltText,
                tenantBranding.TenantConfigured,
                tenantBranding.DefaultTheme,
                tenantBranding.ArabicDisplayName);
        var presentation = tenantBranding is null
            ? new FoundationCurrencyPresentationResponse("SAR", null, "SAR")
            : new FoundationCurrencyPresentationResponse(
                tenantBranding.CurrencyCode,
                tenantBranding.CurrencySymbolAssetUrl,
                tenantBranding.CurrencySymbolTextFallback);
        var operationalContexts = identityHost.ListOperationalContexts(principal)
            .Select(ToOperationalResponse)
            .ToArray();
        return EntryResponse(
            "Tenant",
            operationalContexts,
            identityHost.GetSelectedOperationalContext(principal)?.ContextId,
            identityHost.GetOperationalSelectionVersion(principal),
            brand,
            isDevelopment,
            developmentAccountHint,
            currencyPresentation: presentation);
    }

    private static FoundationEntryResponse EntryResponse(
        string entryMode,
        IReadOnlyList<FoundationOperationalContextResponse> operationalContexts,
        Guid? selectedOperationalContextId,
        long operationalSelectionVersion,
        FoundationBrandingResponse branding,
        bool isDevelopment,
        string? developmentAccountHint,
        string? code = null,
        FoundationCurrencyPresentationResponse? currencyPresentation = null) =>
        new(
            entryMode,
            operationalContexts,
            selectedOperationalContextId,
            operationalSelectionVersion,
            branding,
            currencyPresentation ?? new FoundationCurrencyPresentationResponse("SAR", null, "SAR"),
            code,
            isDevelopment,
            developmentAccountHint);

    private static FoundationBrandingResponse MespBranding() =>
        new("MESP", null, null, "MESP", false);

    private static FoundationOperationalContextResponse ToOperationalResponse(FoundationHostOperationalContextCandidate candidate) =>
        new(candidate.ContextId, candidate.Kind, candidate.DisplayName, candidate.EligibilityVersion);
}
#pragma warning restore CS1591
