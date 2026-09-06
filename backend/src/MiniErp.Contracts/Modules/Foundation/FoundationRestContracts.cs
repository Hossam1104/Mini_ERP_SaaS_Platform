namespace MiniErp.Contracts.Modules.Foundation;

/// <summary>
/// The public security profiles used by the Foundation REST contract.
/// Each public operation selects exactly one profile.
/// </summary>
public enum FoundationSecurityProfile
{
    /// <summary>Anonymous operation with no authorization context.</summary>
    Anonymous = 1,
    /// <summary>Authenticated session without a Tenant or platform path.</summary>
    AuthenticatedSession = 2,
    /// <summary>Active ordinary Tenant membership.</summary>
    OrdinaryMembership = 3,
    /// <summary>Case-bound support access for one Tenant.</summary>
    SupportGrant = 4,
    /// <summary>Purpose-bound Platform governance context.</summary>
    PlatformGovernanceContext = 5,
    /// <summary>High-risk operation requiring additional assurance.</summary>
    HighRisk = 6
}

/// <summary>
/// Visibility of an operation in the public contract catalogue.
/// </summary>
public enum FoundationOperationVisibility
{
    /// <summary>Included in the public REST contract.</summary>
    Public = 1,
    /// <summary>Internal application operation, never mapped publicly.</summary>
    Internal = 2
}

/// <summary>
/// The server-owned authorization scope required by a Foundation operation.
/// </summary>
public enum FoundationScopePolicy
{
    /// <summary>No business authorization scope is selected.</summary>
    None = 1,
    /// <summary>An active ordinary Tenant membership and organization scope.</summary>
    Tenant = 2,
    /// <summary>An active case-bound support grant and organization scope.</summary>
    SupportGrant = 3,
    /// <summary>A purpose-bound platform governance context.</summary>
    PlatformGovernance = 4
}

/// <summary>Concurrency evidence expected by a public operation.</summary>
public enum FoundationConcurrencyPolicy
{
    /// <summary>No optimistic-concurrency header is required.</summary>
    None = 1,
    /// <summary>The current entity version must be supplied in If-Match.</summary>
    IfMatch = 2
}

/// <summary>Idempotency evidence expected by a public operation.</summary>
public enum FoundationIdempotencyPolicy
{
    /// <summary>No idempotency key is required.</summary>
    None = 1,
    /// <summary>The mutation requires a server-bound Idempotency-Key.</summary>
    Required = 2
}

/// <summary>Effective-date contract expected by a public operation.</summary>
public enum FoundationEffectiveDatePolicy
{
    /// <summary>The operation does not select an effective date.</summary>
    None = 1,
    /// <summary>The operation requires an effective date query parameter.</summary>
    QueryRequired = 2,
    /// <summary>The request body must carry an effective date.</summary>
    RequestRequired = 3
}

/// <summary>
/// Stable mapping between one versioned endpoint and one application operation.
/// </summary>
public sealed record FoundationOperationDescriptor(
    string OperationId,
    string Route,
    string HttpMethod,
    FoundationSecurityProfile SecurityProfile,
    FoundationOperationVisibility Visibility,
    string? ExactPermissionCode = null,
    FoundationScopePolicy ScopePolicy = FoundationScopePolicy.None,
    bool RequiresMfa = false,
    bool RequiresFreshAuthentication = false,
    bool RequiresAntiforgery = false,
    bool RequiresMandatoryAudit = false,
    bool IsUnsafe = false,
    FoundationConcurrencyPolicy Concurrency = FoundationConcurrencyPolicy.None,
    FoundationIdempotencyPolicy Idempotency = FoundationIdempotencyPolicy.None,
    FoundationEffectiveDatePolicy EffectiveDate = FoundationEffectiveDatePolicy.None);

/// <summary>
/// Metadata attached to every public endpoint.
/// </summary>
public sealed class FoundationOperationMetadata
{
    /// <summary>Creates metadata directly from the approved descriptor.</summary>
    public FoundationOperationMetadata(FoundationOperationDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    // Compatibility adapter for isolated endpoint tests. Production endpoint
    // registration uses the catalog-backed constructor above.
    /// <summary>Creates metadata after verifying the supplied values match the catalogue.</summary>
    public FoundationOperationMetadata(
        string operationId,
        FoundationSecurityProfile securityProfile,
        FoundationOperationVisibility visibility = FoundationOperationVisibility.Public,
        bool RequiresMandatoryAudit = false,
        bool IsUnsafe = false)
        : this(FoundationOperationCatalog.GetRequired(operationId))
    {
        if (Descriptor.SecurityProfile != securityProfile
            || Descriptor.Visibility != visibility
            || Descriptor.RequiresMandatoryAudit != RequiresMandatoryAudit
            || Descriptor.IsUnsafe != IsUnsafe)
        {
            throw new ArgumentException("Endpoint metadata must match the approved operation descriptor.", nameof(operationId));
        }
    }

    /// <summary>The authoritative operation descriptor.</summary>
    public FoundationOperationDescriptor Descriptor { get; }

    /// <summary>The stable operation identifier.</summary>
    public string OperationId => Descriptor.OperationId;

    /// <summary>The operation security profile.</summary>
    public FoundationSecurityProfile SecurityProfile => Descriptor.SecurityProfile;

    /// <summary>The operation visibility.</summary>
    public FoundationOperationVisibility Visibility => Descriptor.Visibility;

    /// <summary>The exact permission code, when the operation has one.</summary>
    public string? ExactPermissionCode => Descriptor.ExactPermissionCode;

    /// <summary>The operation scope policy.</summary>
    public FoundationScopePolicy ScopePolicy => Descriptor.ScopePolicy;

    /// <summary>Whether MFA is required.</summary>
    public bool RequiresMfa => Descriptor.RequiresMfa;

    /// <summary>Whether fresh authentication is required.</summary>
    public bool RequiresFreshAuthentication => Descriptor.RequiresFreshAuthentication;

    /// <summary>Whether ASP.NET Core antiforgery is required.</summary>
    public bool RequiresAntiforgery => Descriptor.RequiresAntiforgery;

    /// <summary>Whether mandatory evidence is required.</summary>
    public bool RequiresMandatoryAudit => Descriptor.RequiresMandatoryAudit;

    /// <summary>Whether the operation is unsafe.</summary>
    public bool IsUnsafe => Descriptor.IsUnsafe;

    /// <summary>The required optimistic-concurrency contract.</summary>
    public FoundationConcurrencyPolicy Concurrency => Descriptor.Concurrency;

    /// <summary>The required idempotency contract.</summary>
    public FoundationIdempotencyPolicy Idempotency => Descriptor.Idempotency;

    /// <summary>The effective-date contract.</summary>
    public FoundationEffectiveDatePolicy EffectiveDate => Descriptor.EffectiveDate;
}

/// <summary>Safe first-party authentication request.</summary>
public sealed record FoundationSignInRequest(string? Login, string? Password);

/// <summary>Safe session summary returned to the first-party shell.</summary>
public sealed record FoundationSessionResponse(
    bool Authenticated,
    Guid? ActorId,
    Guid? SessionId,
    string LifecycleState,
    DateTimeOffset? AbsoluteExpiresAt,
    string? SelectedPath,
    Guid? SelectedTenantId,
    Guid? SelectedContextId,
    long SelectionVersion,
    bool Replayed = false);

/// <summary>One safe server-derived context candidate.</summary>
public sealed record FoundationContextCandidateResponse(
    Guid ContextId,
    string Kind,
    Guid? TenantId,
    string DisplayName,
    long EligibilityVersion);

/// <summary>Authorized-context list response.</summary>
public sealed record FoundationContextsResponse(
    IReadOnlyList<FoundationContextCandidateResponse> Contexts);

/// <summary>One safe Tenant candidate returned by the entry-routing seam.</summary>
public sealed record FoundationTenantCandidateResponse(
    Guid TenantId,
    string DisplayName,
    string? CanonicalHost);

/// <summary>One safe Company/Branch operational context candidate.</summary>
public sealed record FoundationOperationalContextResponse(
    Guid ContextId,
    string Kind,
    string DisplayName,
    long EligibilityVersion);

/// <summary>Safe tenant-aware branding configuration.</summary>
public sealed record FoundationBrandingResponse(
    string DisplayName,
    string? LogoLightUrl,
    string? LogoDarkUrl,
    string LogoAltText,
    bool TenantConfigured);

/// <summary>Presentation-only currency symbol configuration.</summary>
public sealed record FoundationCurrencyPresentationResponse(
    string CurrencyCode,
    string? SymbolAssetUrl,
    string SymbolTextFallback);

/// <summary>
/// Server-owned entry resolution returned after authentication. The response
/// contains only authorized candidates and safe presentation data; it never
/// grants authority to a client-supplied Tenant identifier.
/// </summary>
public sealed record FoundationEntryResponse(
    string EntryMode,
    string? CanonicalHost,
    Guid? CandidateTenantId,
    string? CandidateTenantDisplayName,
    IReadOnlyList<FoundationTenantCandidateResponse> AuthorizedTenants,
    IReadOnlyList<FoundationOperationalContextResponse> OperationalContexts,
    Guid? SelectedOperationalContextId,
    long OperationalSelectionVersion,
    FoundationBrandingResponse Branding,
    FoundationCurrencyPresentationResponse CurrencyPresentation,
    string? Code = null);

/// <summary>Authorized operational-context list response.</summary>
public sealed record FoundationOperationalContextsResponse(
    IReadOnlyList<FoundationOperationalContextResponse> Contexts,
    Guid? SelectedContextId,
    long SelectionVersion);

/// <summary>Server-confirmed context switch request.</summary>
public sealed record FoundationContextSwitchRequest(
    Guid ContextId,
    long ExpectedSelectionVersion = 0,
    long ExpectedEligibilityVersion = 0);

/// <summary>Server-confirmed Company/Branch context switch request.</summary>
public sealed record FoundationOperationalContextSwitchRequest(
    Guid ContextId,
    long ExpectedSelectionVersion = 0,
    long ExpectedEligibilityVersion = 0);

/// <summary>Result of selecting one operational context.</summary>
public sealed record FoundationOperationalContextSwitchResponse(
    FoundationOperationalContextResponse SelectedContext,
    long SelectionVersion);

/// <summary>
/// Stable, safe response for the representative Foundation context operation.
/// </summary>
public sealed record FoundationContextResponse(
    string OperationId,
    string CorrelationId,
    string AuthorizationPath,
    string LifecycleState,
    string Permission,
    string? TenantId,
    string? Scope,
    bool PlatformGovernance);

/// <summary>
/// Stable, safe response for the representative Foundation write operation.
/// </summary>
public sealed record FoundationWriteResponse(
    string OperationId,
    string CorrelationId,
    string Result,
    string Version,
    bool Replayed);

/// <summary>Typed safe idempotency response; replay never rebuilds mutable state.</summary>
public sealed record FoundationIdempotencyResponse(
    FoundationWriteResponse? WriteResponse = null,
    FoundationSessionResponse? SessionResponse = null)
{
    /// <summary>Wraps a write response for idempotent storage.</summary>
    public static FoundationIdempotencyResponse ForWrite(FoundationWriteResponse response) =>
        new(response ?? throw new ArgumentNullException(nameof(response)), null);

    /// <summary>Wraps a session response for idempotent storage.</summary>
    public static FoundationIdempotencyResponse ForSession(FoundationSessionResponse response) =>
        new(null, response ?? throw new ArgumentNullException(nameof(response)));
}

/// <summary>
/// The allow-listed request body for the non-business Foundation write probe.
/// </summary>
public sealed record FoundationWriteRequest(
    string? Value,
    Guid? TargetId = null);

/// <summary>
/// Stable, safe response for the target-existence demonstration.
/// </summary>
public sealed record FoundationTargetResponse(
    string OperationId,
    string CorrelationId,
    string Result,
    string TargetId);

/// <summary>
/// The single public operation catalogue for the Foundation seam.
/// </summary>
public static class FoundationOperationCatalog
{
    /// <summary>The complete public operation catalogue.</summary>
    public static IReadOnlyList<FoundationOperationDescriptor> PublicOperations { get; } =
    [
        new("platform.health", "/health", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("platform.openapi", "/openapi/v1.json", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("platform.module-registration", "/api/v1/module-registration", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("foundation.tenant-context.read", "/api/v1/foundation/tenant-context", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.foundation.context.read", FoundationScopePolicy.Tenant),
        new("foundation.support-context.read", "/api/v1/foundation/support-context", "GET", FoundationSecurityProfile.SupportGrant, FoundationOperationVisibility.Public, "support.tenant.read", FoundationScopePolicy.SupportGrant, RequiresMfa: true, RequiresFreshAuthentication: true),
        new("foundation.platform-context.read", "/api/v1/foundation/platform-context", "GET", FoundationSecurityProfile.PlatformGovernanceContext, FoundationOperationVisibility.Public, "platform.metadata.read", FoundationScopePolicy.PlatformGovernance, RequiresMfa: true, RequiresFreshAuthentication: true),
        new("foundation.target.read", "/api/v1/foundation/targets/{targetId}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.foundation.target.read", FoundationScopePolicy.Tenant),
        new("foundation.probe.write", "/api/v1/foundation/probe", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.foundation.probe.write", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.list", "/api/v1/master-data/categories", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.view", FoundationScopePolicy.Tenant),
        new("master-data.category.read", "/api/v1/master-data/categories/{categoryId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.view", FoundationScopePolicy.Tenant),
        new("master-data.category.create", "/api/v1/master-data/categories", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.edit", "/api/v1/master-data/categories/{categoryId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.deactivate", "/api/v1/master-data/categories/{categoryId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.reactivate", "/api/v1/master-data/categories/{categoryId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.audit.read", "/api/v1/master-data/categories/{categoryId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.audit", FoundationScopePolicy.Tenant),
        new("master-data.unit.list", "/api/v1/master-data/units-of-measure", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.view", FoundationScopePolicy.Tenant),
        new("master-data.unit.read", "/api/v1/master-data/units-of-measure/{unitId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.view", FoundationScopePolicy.Tenant),
        new("master-data.unit.create", "/api/v1/master-data/units-of-measure", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.edit", "/api/v1/master-data/units-of-measure/{unitId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.deactivate", "/api/v1/master-data/units-of-measure/{unitId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.reactivate", "/api/v1/master-data/units-of-measure/{unitId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.audit.read", "/api/v1/master-data/units-of-measure/{unitId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.audit", FoundationScopePolicy.Tenant),
        new("master-data.product.list", "/api/v1/master-data/products", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.view", FoundationScopePolicy.Tenant),
        new("master-data.product.read", "/api/v1/master-data/products/{productId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.view", FoundationScopePolicy.Tenant),
        new("master-data.product.create", "/api/v1/master-data/products", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.edit", "/api/v1/master-data/products/{productId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.deactivate", "/api/v1/master-data/products/{productId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.reactivate", "/api/v1/master-data/products/{productId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.audit.read", "/api/v1/master-data/products/{productId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.audit", FoundationScopePolicy.Tenant),
        new("master-data.supplier.list", "/api/v1/master-data/suppliers", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.view", FoundationScopePolicy.Tenant),
        new("master-data.supplier.read", "/api/v1/master-data/suppliers/{supplierId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.view", FoundationScopePolicy.Tenant),
        new("master-data.supplier.create", "/api/v1/master-data/suppliers", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.edit", "/api/v1/master-data/suppliers/{supplierId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.deactivate", "/api/v1/master-data/suppliers/{supplierId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.reactivate", "/api/v1/master-data/suppliers/{supplierId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.audit.read", "/api/v1/master-data/suppliers/{supplierId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.audit", FoundationScopePolicy.Tenant),
        new("master-data.customer.list", "/api/v1/master-data/customers", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.view", FoundationScopePolicy.Tenant),
        new("master-data.customer.read", "/api/v1/master-data/customers/{customerId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.view", FoundationScopePolicy.Tenant),
        new("master-data.customer.create", "/api/v1/master-data/customers", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.edit", "/api/v1/master-data/customers/{customerId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.deactivate", "/api/v1/master-data/customers/{customerId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.reactivate", "/api/v1/master-data/customers/{customerId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.audit.read", "/api/v1/master-data/customers/{customerId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.audit", FoundationScopePolicy.Tenant),
        new("master-data.currency.list", "/api/v1/master-data/currencies", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.view", FoundationScopePolicy.Tenant),
        new("master-data.currency.read", "/api/v1/master-data/currencies/{currencyId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.view", FoundationScopePolicy.Tenant),
        new("master-data.currency.create", "/api/v1/master-data/currencies", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.edit", "/api/v1/master-data/currencies/{currencyId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.deactivate", "/api/v1/master-data/currencies/{currencyId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.reactivate", "/api/v1/master-data/currencies/{currencyId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.audit.read", "/api/v1/master-data/currencies/{currencyId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.audit", FoundationScopePolicy.Tenant),
        new("master-data.currency.reference.read", "/api/v1/master-data/currencies/{currencyId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.list", "/api/v1/master-data/payment-terms", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.create", "/api/v1/master-data/payment-terms", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.edit", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.deactivate", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.reactivate", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.audit.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.audit", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.history.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.reference.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.preview.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/due-date-preview", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.list", "/api/v1/master-data/taxes", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.read", "/api/v1/master-data/taxes/{taxId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.history.read", "/api/v1/master-data/taxes/{taxId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.reference.read", "/api/v1/master-data/taxes/{taxId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.calculate", "/api/v1/master-data/taxes/{taxId:guid}/calculate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.create", "/api/v1/master-data/taxes", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.edit", "/api/v1/master-data/taxes/{taxId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.deactivate", "/api/v1/master-data/taxes/{taxId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.reactivate", "/api/v1/master-data/taxes/{taxId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.audit.read", "/api/v1/master-data/taxes/{taxId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.audit", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.list", "/api/v1/master-data/exchange-rates", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.history.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.reference.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.create", "/api/v1/master-data/exchange-rates", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.edit", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.deactivate", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.reactivate", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.audit.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.audit", FoundationScopePolicy.Tenant),
        new("master-data.price-list.list", "/api/v1/master-data/price-lists", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant),
        new("master-data.price-list.read", "/api/v1/master-data/price-lists/{priceListId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant),
        new("master-data.price-list.history.read", "/api/v1/master-data/price-lists/{priceListId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant),
        new("master-data.price-list.create", "/api/v1/master-data/price-lists", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.edit", "/api/v1/master-data/price-lists/{priceListId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.price.append", "/api/v1/master-data/price-lists/{priceListId:guid}/prices", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required, EffectiveDate: FoundationEffectiveDatePolicy.RequestRequired),
        new("master-data.price-list.deactivate", "/api/v1/master-data/price-lists/{priceListId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.reactivate", "/api/v1/master-data/price-lists/{priceListId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.reference.read", "/api/v1/master-data/price-lists/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant, EffectiveDate: FoundationEffectiveDatePolicy.QueryRequired),
        new("master-data.price-list.audit.read", "/api/v1/master-data/price-lists/{priceListId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.audit", FoundationScopePolicy.Tenant),
        new("master-data.import.create", "/api/v1/master-data/imports", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.import.simulate", "/api/v1/master-data/imports/{batchId:guid}/simulate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.import.execute", "/api/v1/master-data/imports/{batchId:guid}/execute", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.import.list", "/api/v1/master-data/imports", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.read", "/api/v1/master-data/imports/{batchId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.status.read", "/api/v1/master-data/imports/{batchId:guid}/status", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.rows.read", "/api/v1/master-data/imports/{batchId:guid}/rows", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.reconciliation.read", "/api/v1/master-data/imports/{batchId:guid}/reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.audit.read", "/api/v1/master-data/imports/{batchId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.audit", FoundationScopePolicy.Tenant),
        new("master-data.import.evidence.read", "/api/v1/master-data/imports/{batchId:guid}/evidence", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.replay", "/api/v1/master-data/imports/{batchId:guid}/rows/{rowId:guid}/replay", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.organization-scope.list", "/api/v1/procurement/organization-scopes", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.organization-scope.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.list", "/api/v1/procurement/purchase-requests", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.create", "/api/v1/procurement/purchase-requests", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.edit", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.submit", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.approve", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.reject", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.return-for-change", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/return-for-change", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.cancel", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.history.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.history", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.audit.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.audit", FoundationScopePolicy.Tenant),
        new("procurement.quotation.list", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotations", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.view", FoundationScopePolicy.Tenant),
        new("procurement.quotation.read", "/api/v1/procurement/quotations/{quotationId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.view", FoundationScopePolicy.Tenant),
        new("procurement.quotation.create", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.edit", "/api/v1/procurement/quotations/{quotationId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.submit", "/api/v1/procurement/quotations/{quotationId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.withdraw", "/api/v1/procurement/quotations/{quotationId:guid}/withdraw", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.withdraw", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.disqualify", "/api/v1/procurement/quotations/{quotationId:guid}/disqualify", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.disqualify", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.compare", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotation-comparison", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.compare", FoundationScopePolicy.Tenant),
        new("procurement.source-decision.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.source-decision.view", FoundationScopePolicy.Tenant),
        new("procurement.source-decision.history.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.source-decision.view", FoundationScopePolicy.Tenant),
        new("procurement.source-decision.record", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.source-decision.record", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.history.read", "/api/v1/procurement/quotations/{quotationId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.history", FoundationScopePolicy.Tenant),
        new("procurement.quotation.audit.read", "/api/v1/procurement/quotations/{quotationId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.audit", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.source.list", "/api/v1/procurement/purchase-order-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.list", "/api/v1/procurement/purchase-orders", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.create", "/api/v1/procurement/purchase-orders", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.edit", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.submit", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.approve", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.reject", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.return-for-change", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/return-for-change", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.issue", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/issue", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.issue", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.cancel", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.confirmation.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/confirmations", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.confirmation.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.confirmation.capture", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/confirmations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.confirmation.capture", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.supplier-change.approve", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/supplier-change/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.supplier-change.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.supplier-change.reject", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/supplier-change/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.supplier-change.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.history.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.history", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.audit.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.audit", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.eligible-source.list", "/api/v1/procurement/goods-receipt-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.view", FoundationScopePolicy.Tenant),
        new("procurement.warehouse.list", "/api/v1/procurement/warehouses", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.view", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.list", "/api/v1/procurement/goods-receipts", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.view", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.read", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.view", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.create", "/api/v1/procurement/goods-receipts", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.goods-receipt.cancel", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.goods-receipt.history.read", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.history", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.audit.read", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.audit", FoundationScopePolicy.Tenant),
        new("procurement.supplier-return.eligible-source.list", "/api/v1/procurement/supplier-return-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.view", FoundationScopePolicy.Tenant),
        new("procurement.supplier-return.list", "/api/v1/procurement/supplier-returns", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.view", FoundationScopePolicy.Tenant),
        new("procurement.supplier-return.read", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.view", FoundationScopePolicy.Tenant),
        new("procurement.supplier-return.create", "/api/v1/procurement/supplier-returns", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.submit", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.approve", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.reject", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.cancel", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.reverse", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.reverse", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.correct", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/correct", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.correct", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.inventory-handoff.record", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/inventory-handoff", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.inventory-handoff", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.finance-reference.record", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/finance-reference", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.finance-reference", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.supplier-return.history.read", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.history", FoundationScopePolicy.Tenant),
        new("procurement.supplier-return.audit.read", "/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.audit", FoundationScopePolicy.Tenant),
        new("procurement.supplier-return.report.read", "/api/v1/procurement/supplier-returns/report", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.supplier-return.report", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.eligible-source.list", "/api/v1/procurement/purchase-invoice-handoff-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.view", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.list", "/api/v1/procurement/purchase-invoice-handoffs", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.view", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.read", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.view", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.create", "/api/v1/procurement/purchase-invoice-handoffs", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.invoice-handoff.evidence.capture", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/declared-evidence", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.evidence.capture", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.invoice-handoff.cancel", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.invoice-handoff.history.read", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.history", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.audit.read", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.audit", FoundationScopePolicy.Tenant),
        new("procurement.matching.list", "/api/v1/procurement/purchase-invoice-matches", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.matching.view", FoundationScopePolicy.Tenant),
        new("procurement.matching.read", "/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.matching.view", FoundationScopePolicy.Tenant),
        new("procurement.matching.evaluate", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/evaluate-match", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.matching.evaluate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.matching.resolve-exception", "/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}/resolve-exception", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.matching.resolve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.matching.history.read", "/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.matching.view", FoundationScopePolicy.Tenant),
        new("procurement.matching.audit.read", "/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.matching.view", FoundationScopePolicy.Tenant),
        new("inventory.warehouse.list", "/api/v1/inventory/warehouses", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.warehouse.view", FoundationScopePolicy.Tenant),
        new("inventory.ledger.list", "/api/v1/inventory/ledger", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.ledger.view", FoundationScopePolicy.Tenant),
        new("inventory.ledger.read", "/api/v1/inventory/ledger/{movementId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.ledger.view", FoundationScopePolicy.Tenant),
        new("inventory.availability.read", "/api/v1/inventory/availability", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.availability.view", FoundationScopePolicy.Tenant),
        new("inventory.opening.list", "/api/v1/inventory/opening-balances", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.view", FoundationScopePolicy.Tenant),
        new("inventory.opening.read", "/api/v1/inventory/opening-balances/{openingBalanceId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.view", FoundationScopePolicy.Tenant),
        new("inventory.opening.create", "/api/v1/inventory/opening-balances", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.opening.validate", "/api/v1/inventory/opening-balances/{openingBalanceId:guid}/validate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.validate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.opening.post", "/api/v1/inventory/opening-balances/{openingBalanceId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.opening.correct", "/api/v1/inventory/opening-balances/{openingBalanceId:guid}/correct", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.correct", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.opening.history.read", "/api/v1/inventory/opening-balances/{openingBalanceId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.history", FoundationScopePolicy.Tenant),
        new("inventory.opening.audit.read", "/api/v1/inventory/opening-balances/{openingBalanceId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.opening.audit", FoundationScopePolicy.Tenant),
        new("inventory.reservation.list", "/api/v1/inventory/reservations", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reservation.view", FoundationScopePolicy.Tenant),
        new("inventory.reservation.read", "/api/v1/inventory/reservations/{reservationId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reservation.view", FoundationScopePolicy.Tenant),
        new("inventory.reservation.create", "/api/v1/inventory/reservations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reservation.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.reservation.reduce", "/api/v1/inventory/reservations/{reservationId:guid}/reduce", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reservation.reduce", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.reservation.release", "/api/v1/inventory/reservations/{reservationId:guid}/release", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reservation.release", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.reservation.history.read", "/api/v1/inventory/reservations/{reservationId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reservation.history", FoundationScopePolicy.Tenant),
        new("inventory.reservation.audit.read", "/api/v1/inventory/reservations/{reservationId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reservation.audit", FoundationScopePolicy.Tenant),
        new("inventory.goods-receipt.post", "/api/v1/inventory/goods-receipts/{goodsReceiptId:guid}/lines/{goodsReceiptLineId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.goods-receipt.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.supplier-return.post", "/api/v1/inventory/supplier-returns/{supplierReturnId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.supplier-return.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.transfer.list", "/api/v1/inventory/transfers", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.view", FoundationScopePolicy.Tenant),
        new("inventory.transfer.read", "/api/v1/inventory/transfers/{transferId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.view", FoundationScopePolicy.Tenant),
        new("inventory.transfer.create", "/api/v1/inventory/transfers", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.transfer.direct", "/api/v1/inventory/transfers/{transferId:guid}/complete-direct", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.complete", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.transfer.ship", "/api/v1/inventory/transfers/{transferId:guid}/ship", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.ship", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.transfer.receive", "/api/v1/inventory/transfers/{transferId:guid}/receive", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.receive", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.transfer.shortage-resolve", "/api/v1/inventory/transfers/{transferId:guid}/resolve-shortage", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.resolve-shortage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.transfer.cancel", "/api/v1/inventory/transfers/{transferId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.transfer.history.read", "/api/v1/inventory/transfers/{transferId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.history", FoundationScopePolicy.Tenant),
        new("inventory.transfer.audit.read", "/api/v1/inventory/transfers/{transferId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.transfer.audit", FoundationScopePolicy.Tenant),
        new("inventory.reason.list", "/api/v1/inventory/reason-codes", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reason.view", FoundationScopePolicy.Tenant),
        new("inventory.reason.read", "/api/v1/inventory/reason-codes/{reasonCodeId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reason.view", FoundationScopePolicy.Tenant),
        new("inventory.reason.create", "/api/v1/inventory/reason-codes", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reason.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.reason.update", "/api/v1/inventory/reason-codes/{reasonCodeId:guid}/update", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.reason.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.adjustment.list", "/api/v1/inventory/adjustments", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.view", FoundationScopePolicy.Tenant),
        new("inventory.adjustment.read", "/api/v1/inventory/adjustments/{adjustmentId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.view", FoundationScopePolicy.Tenant),
        new("inventory.adjustment.create", "/api/v1/inventory/adjustments", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.adjustment.submit", "/api/v1/inventory/adjustments/{adjustmentId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.adjustment.approve", "/api/v1/inventory/adjustments/{adjustmentId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.adjustment.reject", "/api/v1/inventory/adjustments/{adjustmentId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.adjustment.return", "/api/v1/inventory/adjustments/{adjustmentId:guid}/return", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.adjustment.post", "/api/v1/inventory/adjustments/{adjustmentId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.adjustment.history.read", "/api/v1/inventory/adjustments/{adjustmentId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.history", FoundationScopePolicy.Tenant),
        new("inventory.adjustment.audit.read", "/api/v1/inventory/adjustments/{adjustmentId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.adjustment.audit", FoundationScopePolicy.Tenant),
        new("inventory.count.list", "/api/v1/inventory/counts", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.view", FoundationScopePolicy.Tenant),
        new("inventory.count.read", "/api/v1/inventory/counts/{countId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.view", FoundationScopePolicy.Tenant),
        new("inventory.count.counter.read", "/api/v1/inventory/counts/{countId:guid}/counter-view", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.counter-view", FoundationScopePolicy.Tenant),
        new("inventory.count.create", "/api/v1/inventory/counts", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.submit", "/api/v1/inventory/counts/{countId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.approve", "/api/v1/inventory/counts/{countId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.variance-reason", "/api/v1/inventory/counts/{countId:guid}/variance-reason", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.reject", "/api/v1/inventory/counts/{countId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.return", "/api/v1/inventory/counts/{countId:guid}/return", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.recount", "/api/v1/inventory/counts/{countId:guid}/recount", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.recount", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.resnapshot", "/api/v1/inventory/counts/{countId:guid}/resnapshot", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.resnapshot", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.post", "/api/v1/inventory/counts/{countId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.count.history.read", "/api/v1/inventory/counts/{countId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.history", FoundationScopePolicy.Tenant),
        new("inventory.count.audit.read", "/api/v1/inventory/counts/{countId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.count.audit", FoundationScopePolicy.Tenant),
        new("inventory.issue.list", "/api/v1/inventory/stock-issues", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.view", FoundationScopePolicy.Tenant),
        new("inventory.issue.read", "/api/v1/inventory/stock-issues/{stockIssueId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.view", FoundationScopePolicy.Tenant),
        new("inventory.issue.create", "/api/v1/inventory/stock-issues", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.issue.submit", "/api/v1/inventory/stock-issues/{stockIssueId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.issue.approve", "/api/v1/inventory/stock-issues/{stockIssueId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.issue.reject", "/api/v1/inventory/stock-issues/{stockIssueId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.issue.return", "/api/v1/inventory/stock-issues/{stockIssueId:guid}/return", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.issue.post", "/api/v1/inventory/stock-issues/{stockIssueId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.issue.history.read", "/api/v1/inventory/stock-issues/{stockIssueId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.history", FoundationScopePolicy.Tenant),
        new("inventory.issue.audit.read", "/api/v1/inventory/stock-issues/{stockIssueId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.issue.audit", FoundationScopePolicy.Tenant),
        new("inventory.movement.correct", "/api/v1/inventory/ledger/{movementId:guid}/correct", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.movement.correct", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.valuation.policy.read", "/api/v1/inventory/valuation/policies", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.policy.view", FoundationScopePolicy.Tenant),
        new("inventory.valuation.policy.create", "/api/v1/inventory/valuation/policies", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.policy.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.valuation.state.read", "/api/v1/inventory/valuation/states", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.view", FoundationScopePolicy.Tenant),
        new("inventory.valuation.history.read", "/api/v1/inventory/valuation/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.history", FoundationScopePolicy.Tenant),
        new("inventory.valuation.process", "/api/v1/inventory/valuation/process", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.process", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.valuation.reconciliation.read", "/api/v1/inventory/valuation/reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.reconciliation", FoundationScopePolicy.Tenant),
        new("inventory.valuation.finance-handoff.read", "/api/v1/inventory/valuation/finance-handoffs", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.finance-handoff", FoundationScopePolicy.Tenant),
        new("inventory.valuation.summary.read", "/api/v1/inventory/valuation/summary", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.view", FoundationScopePolicy.Tenant),
        new("inventory.valuation.pending.read", "/api/v1/inventory/valuation/pending", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.history", FoundationScopePolicy.Tenant),
        new("inventory.valuation.in-transit.read", "/api/v1/inventory/valuation/in-transit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.reconciliation", FoundationScopePolicy.Tenant),
        new("inventory.valuation.export", "/api/v1/inventory/valuation/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.export", FoundationScopePolicy.Tenant),
        new("inventory.valuation.correction", "/api/v1/inventory/valuation/history/{valuationEventId:guid}/correction", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.valuation.correct", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.customer-return.boundary.read", "/api/v1/inventory/customer-returns/boundary", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.customer-return.view", FoundationScopePolicy.Tenant),
        new("finance.company.list", "/api/v1/finance/companies", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.company.view", FoundationScopePolicy.Tenant),
        new("finance.account.list", "/api/v1/finance/accounts", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.account.view", FoundationScopePolicy.Tenant),
        new("finance.account.create", "/api/v1/finance/accounts", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.account.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.account.edit", "/api/v1/finance/accounts/{accountId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.account.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.account.lifecycle", "/api/v1/finance/accounts/{accountId:guid}/lifecycle", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.account.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.calendar.list", "/api/v1/finance/calendars", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.calendar.view", FoundationScopePolicy.Tenant),
        new("finance.calendar.create", "/api/v1/finance/calendars", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.calendar.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.year.list", "/api/v1/finance/calendars/{calendarId:guid}/years", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.calendar.view", FoundationScopePolicy.Tenant),
        new("finance.year.create", "/api/v1/finance/years", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.calendar.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.period.list", "/api/v1/finance/years/{yearId:guid}/periods", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.calendar.view", FoundationScopePolicy.Tenant),
        new("finance.period.create", "/api/v1/finance/periods", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.calendar.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.period.state", "/api/v1/finance/periods/{periodId:guid}/state", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.calendar.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.cost-center.list", "/api/v1/finance/cost-centers", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.cost-center.view", FoundationScopePolicy.Tenant),
        new("finance.cost-center.create", "/api/v1/finance/cost-centers", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.cost-center.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.posting-rule.list", "/api/v1/finance/posting-rules", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.posting-rule.view", FoundationScopePolicy.Tenant),
        new("finance.posting-rule.create", "/api/v1/finance/posting-rules", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.posting-rule.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.posting-rule.lifecycle", "/api/v1/finance/posting-rules/{ruleId:guid}/lifecycle", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.posting-rule.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.journal.list", "/api/v1/finance/journals", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.view", FoundationScopePolicy.Tenant),
        new("finance.journal.create", "/api/v1/finance/journals", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.journal.submit", "/api/v1/finance/journals/{journalId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.journal.approve", "/api/v1/finance/journals/{journalId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.journal.reject", "/api/v1/finance/journals/{journalId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.journal.cancel", "/api/v1/finance/journals/{journalId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.journal.post", "/api/v1/finance/journals/{journalId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.journal.reverse", "/api/v1/finance/journals/{journalId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.journal.reverse", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.gl.inquiry", "/api/v1/finance/gl", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.gl.view", FoundationScopePolicy.Tenant),
        new("finance.handoff.list", "/api/v1/finance/inventory-handoffs", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.handoff.view", FoundationScopePolicy.Tenant),
        new("finance.handoff.process", "/api/v1/finance/inventory-handoffs/{handoffId:guid}/process", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.handoff.process", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.ap.list", "/api/v1/finance/ap/open-items", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ap.detail", "/api/v1/finance/ap/open-items/{itemId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ap.source-ready", "/api/v1/finance/ap/source-ready", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ap.recognize", "/api/v1/finance/ap/recognize", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.ap.aging", "/api/v1/finance/ap/aging", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ap.reconciliation", "/api/v1/finance/ap/reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ar.list", "/api/v1/finance/ar/open-items", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ar.detail", "/api/v1/finance/ar/open-items/{itemId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ar.create", "/api/v1/finance/ar/manual", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.ar.aging", "/api/v1/finance/ar/aging", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ar.exposure", "/api/v1/finance/ar/exposure", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.ar.reconciliation", "/api/v1/finance/ar/reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.payment-method.list", "/api/v1/finance/payment-methods", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.payment-method.create", "/api/v1/finance/payment-methods", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.configure", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment-method.edit", "/api/v1/finance/payment-methods/{methodId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.configure", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment-method.lifecycle", "/api/v1/finance/payment-methods/{methodId:guid}/lifecycle", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.configure", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.cash-account.list", "/api/v1/finance/cash-accounts", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.cash-account.create", "/api/v1/finance/cash-accounts", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.configure", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.cash-account.edit", "/api/v1/finance/cash-accounts/{accountId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.configure", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.cash-account.lifecycle", "/api/v1/finance/cash-accounts/{accountId:guid}/lifecycle", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.configure", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment.list", "/api/v1/finance/payments", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.payment.detail", "/api/v1/finance/payments/{documentId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.payment.create", "/api/v1/finance/payments", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment.edit", "/api/v1/finance/payments/{documentId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment.submit", "/api/v1/finance/payments/{documentId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment.approve", "/api/v1/finance/payments/{documentId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment.reject", "/api/v1/finance/payments/{documentId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment.post", "/api/v1/finance/payments/{documentId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.payment.reverse", "/api/v1/finance/payments/{documentId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.receipt.list", "/api/v1/finance/receipts", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.receipt.detail", "/api/v1/finance/receipts/{documentId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.receipt.create", "/api/v1/finance/receipts", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.receipt.edit", "/api/v1/finance/receipts/{documentId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.receipt.submit", "/api/v1/finance/receipts/{documentId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.receipt.approve", "/api/v1/finance/receipts/{documentId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.receipt.reject", "/api/v1/finance/receipts/{documentId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.receipt.post", "/api/v1/finance/receipts/{documentId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.receipt.reverse", "/api/v1/finance/receipts/{documentId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.allocation.list", "/api/v1/finance/allocations", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.allocation.create", "/api/v1/finance/allocations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.allocation.reverse", "/api/v1/finance/allocations/{allocationId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.settlement.reconciliation", "/api/v1/finance/settlement/reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.monetary-policy.list", "/api/v1/finance/monetary-policy", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.monetary-policy.create", "/api/v1/finance/monetary-policy", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.configure", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.tax-accounting.list", "/api/v1/finance/tax-accounting", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.tax-accounting.preview", "/api/v1/finance/tax-accounting/preview", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true),
        new("finance.tax-accounting.post", "/api/v1/finance/tax-accounting", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.tax-accounting.reverse", "/api/v1/finance/tax-accounting/{effectId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.tax-accounting.reconciliation", "/api/v1/finance/tax-accounting/reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.revaluation.list", "/api/v1/finance/revaluation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.revaluation.detail", "/api/v1/finance/revaluation/{batchId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.revaluation.create", "/api/v1/finance/revaluation", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.revaluation.calculate", "/api/v1/finance/revaluation/{batchId:guid}/calculate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.revaluation.post", "/api/v1/finance/revaluation/{batchId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.revaluation.reverse", "/api/v1/finance/revaluation/{batchId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.revaluation.reconciliation", "/api/v1/finance/fx-reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.revaluation.unrealized-reconciliation", "/api/v1/finance/unrealized-fx-reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.reporting-currency.reconciliation", "/api/v1/finance/reporting-currency-reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.settlement.view", FoundationScopePolicy.Tenant),
        new("finance.close.readiness", "/api/v1/finance/periods/{periodId:guid}/close-readiness", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.view", FoundationScopePolicy.Tenant),
        new("finance.close.runs", "/api/v1/finance/period-close-runs", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.view", FoundationScopePolicy.Tenant),
        new("finance.close.history", "/api/v1/finance/periods/{periodId:guid}/close-history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.view", FoundationScopePolicy.Tenant),
        new("finance.close.period", "/api/v1/finance/periods/{periodId:guid}/close", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.close.reopen", "/api/v1/finance/periods/{periodId:guid}/reopen", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.year-end.list", "/api/v1/finance/year-end", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.view", FoundationScopePolicy.Tenant),
        new("finance.year-end.calculate", "/api/v1/finance/year-end/calculate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.manage", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.year-end.post", "/api/v1/finance/year-end/{runId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.year-end.reverse", "/api/v1/finance/year-end/{runId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.correction.create", "/api/v1/finance/journals/{journalId:guid}/correction", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.correction.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.reconciliation.close", "/api/v1/finance/reconciliation/close", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.close.view", FoundationScopePolicy.Tenant),
        new("finance.reconciliation.close.export", "/api/v1/finance/reconciliation/close/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.export", FoundationScopePolicy.Tenant),
        new("finance.report.trial-balance", "/api/v1/finance/reports/trial-balance", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.view", FoundationScopePolicy.Tenant),
        new("finance.report.trial-balance.export", "/api/v1/finance/reports/trial-balance/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.export", FoundationScopePolicy.Tenant),
        new("finance.report.general-ledger", "/api/v1/finance/reports/general-ledger", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.view", FoundationScopePolicy.Tenant),
        new("finance.report.general-ledger.export", "/api/v1/finance/reports/general-ledger/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.export", FoundationScopePolicy.Tenant),
        new("finance.report.ap-aging", "/api/v1/finance/reports/ap-aging", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.view", FoundationScopePolicy.Tenant),
        new("finance.report.ap-aging.export", "/api/v1/finance/reports/ap-aging/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.export", FoundationScopePolicy.Tenant),
        new("finance.report.ar-aging", "/api/v1/finance/reports/ar-aging", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.view", FoundationScopePolicy.Tenant),
        new("finance.report.ar-aging.export", "/api/v1/finance/reports/ar-aging/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.export", FoundationScopePolicy.Tenant),
        new("finance.report.profit-loss", "/api/v1/finance/reports/profit-loss", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.view", FoundationScopePolicy.Tenant),
        new("finance.report.profit-loss.export", "/api/v1/finance/reports/profit-loss/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.export", FoundationScopePolicy.Tenant),
        new("finance.report.balance-sheet", "/api/v1/finance/reports/balance-sheet", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.view", FoundationScopePolicy.Tenant),
        new("finance.report.balance-sheet.export", "/api/v1/finance/reports/balance-sheet/export", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.report.export", FoundationScopePolicy.Tenant),
        new("sales.quotation.list", "/api/v1/sales/quotations", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.view", FoundationScopePolicy.Tenant),
        new("sales.quotation.read", "/api/v1/sales/quotations/{quotationId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.view", FoundationScopePolicy.Tenant),
        new("sales.quotation.create", "/api/v1/sales/quotations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.edit", "/api/v1/sales/quotations/{quotationId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.submit", "/api/v1/sales/quotations/{quotationId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.approve", "/api/v1/sales/quotations/{quotationId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.reject", "/api/v1/sales/quotations/{quotationId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.return", "/api/v1/sales/quotations/{quotationId:guid}/return", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.send", "/api/v1/sales/quotations/{quotationId:guid}/send", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.send", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.withdraw", "/api/v1/sales/quotations/{quotationId:guid}/withdraw", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.withdraw", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.cancel", "/api/v1/sales/quotations/{quotationId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.convert", "/api/v1/sales/quotations/{quotationId:guid}/convert", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.convert", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.quotation.revisions.read", "/api/v1/sales/quotations/{quotationId:guid}/revisions", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.history", FoundationScopePolicy.Tenant),
        new("sales.quotation.history.read", "/api/v1/sales/quotations/{quotationId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.history", FoundationScopePolicy.Tenant),
        new("sales.quotation.audit.read", "/api/v1/sales/quotations/{quotationId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.quotation.audit", FoundationScopePolicy.Tenant),
        new("sales.order.list", "/api/v1/sales/orders", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.view", FoundationScopePolicy.Tenant),
        new("sales.order.read", "/api/v1/sales/orders/{orderId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.view", FoundationScopePolicy.Tenant),
        new("sales.order.edit", "/api/v1/sales/orders/{orderId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.order.submit", "/api/v1/sales/orders/{orderId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.order.approve", "/api/v1/sales/orders/{orderId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.order.reject", "/api/v1/sales/orders/{orderId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.order.return", "/api/v1/sales/orders/{orderId:guid}/return", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.order.confirm", "/api/v1/sales/orders/{orderId:guid}/confirm", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.confirm", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.order.cancel", "/api/v1/sales/orders/{orderId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.order.credit.read", "/api/v1/sales/orders/{orderId:guid}/credit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.credit.view", FoundationScopePolicy.Tenant),
        new("sales.order.credit.override", "/api/v1/sales/orders/{orderId:guid}/credit/override", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.credit.override", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.fulfillment.read", "/api/v1/sales/orders/{orderId:guid}/fulfillment", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.fulfillment.view", FoundationScopePolicy.Tenant),
        new("sales.fulfillment.reserve", "/api/v1/sales/orders/{orderId:guid}/reservations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.fulfillment.reserve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.delivery.post", "/api/v1/sales/orders/{orderId:guid}/deliveries", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.delivery.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.delivery.read", "/api/v1/sales/deliveries/{deliveryId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.delivery.view", FoundationScopePolicy.Tenant),
        new("sales.customer-return.eligible-source.list", "/api/v1/sales/customer-return-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.view", FoundationScopePolicy.Tenant),
        new("sales.customer-return.eligible-source.read", "/api/v1/sales/customer-return-sources/{deliveryId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.view", FoundationScopePolicy.Tenant),
        new("sales.customer-return.create", "/api/v1/sales/customer-returns", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.customer-return.read", "/api/v1/sales/customer-returns/{returnId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.view", FoundationScopePolicy.Tenant),
        new("sales.customer-return.submit", "/api/v1/sales/customer-returns/{returnId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.customer-return.approve", "/api/v1/sales/customer-returns/{returnId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.customer-return.reject", "/api/v1/sales/customer-returns/{returnId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.customer-return.cancel", "/api/v1/sales/customer-returns/{returnId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.customer-return.reverse", "/api/v1/sales/customer-returns/{returnId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.reverse", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.customer-return.history.read", "/api/v1/sales/customer-returns/{returnId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.history", FoundationScopePolicy.Tenant),
        new("sales.customer-return.audit.read", "/api/v1/sales/customer-returns/{returnId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.customer-return.audit", FoundationScopePolicy.Tenant),
        new("inventory.customer-return.receive", "/api/v1/inventory/customer-returns/{salesCustomerReturnId:guid}/receive", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.customer-return.receive", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.customer-return.inspect", "/api/v1/inventory/customer-returns/{salesCustomerReturnId:guid}/inspect", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.customer-return.inspect", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.customer-return.reverse", "/api/v1/inventory/customer-returns/{salesCustomerReturnId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.customer-return.reverse", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("inventory.customer-return.read", "/api/v1/inventory/customer-returns/{salesCustomerReturnId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.inventory.customer-return.view", FoundationScopePolicy.Tenant),
        new("finance.credit-note.create", "/api/v1/finance/credit-notes", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.credit-note.read", "/api/v1/finance/credit-notes/{creditNoteId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.view", FoundationScopePolicy.Tenant),
        new("finance.credit-note.submit", "/api/v1/finance/credit-notes/{creditNoteId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.credit-note.approve", "/api/v1/finance/credit-notes/{creditNoteId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.credit-note.reject", "/api/v1/finance/credit-notes/{creditNoteId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.credit-note.cancel", "/api/v1/finance/credit-notes/{creditNoteId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.credit-note.post", "/api/v1/finance/credit-notes/{creditNoteId:guid}/post", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.post", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("finance.credit-note.reverse", "/api/v1/finance/credit-notes/{creditNoteId:guid}/reverse", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.finance.credit-note.reverse", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.invoice.eligibility.read", "/api/v1/sales/orders/{orderId:guid}/invoice-eligibility", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.invoice.view", FoundationScopePolicy.Tenant),
        new("sales.invoice.request", "/api/v1/sales/orders/{orderId:guid}/invoice-requests", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.invoice.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("sales.invoice.request.read", "/api/v1/sales/invoice-requests/{requestId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.invoice.view", FoundationScopePolicy.Tenant),
        new("sales.order.history.read", "/api/v1/sales/orders/{orderId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.history", FoundationScopePolicy.Tenant),
        new("sales.order.audit.read", "/api/v1/sales/orders/{orderId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.sales.order.audit", FoundationScopePolicy.Tenant),
        new("auth.antiforgery.read", "/api/v1/auth/antiforgery", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("auth.sign-in", "/api/v1/auth/sign-in", "POST", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public, RequiresAntiforgery: false, IsUnsafe: true),
        new("auth.development-bypass", "/api/v1/auth/development-bypass", "POST", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public, RequiresAntiforgery: false, IsUnsafe: true),
        new("auth.sign-out", "/api/v1/auth/sign-out", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session", FoundationScopePolicy.None, RequiresAntiforgery: true, RequiresMandatoryAudit: false, IsUnsafe: true),
        new("auth.session.read", "/api/v1/auth/session", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.contexts.read", "/api/v1/auth/contexts", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.entry.read", "/api/v1/auth/entry", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.operational-contexts.read", "/api/v1/auth/operational-contexts", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.context-switch", "/api/v1/auth/context-switch", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "foundation.context.switch", FoundationScopePolicy.None, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("auth.operational-context-switch", "/api/v1/auth/operational-context-switch", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "foundation.context.switch", FoundationScopePolicy.None, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true)
    ];

    /// <summary>Internal operations deliberately excluded from public routing.</summary>
    public static IReadOnlyList<FoundationOperationDescriptor> InternalOperations { get; } =
    [
        new("identity.invitation.issue", "", "INTERNAL", FoundationSecurityProfile.HighRisk, FoundationOperationVisibility.Internal),
        new("identity.recovery.consume", "", "INTERNAL", FoundationSecurityProfile.HighRisk, FoundationOperationVisibility.Internal)
    ];

    /// <summary>Gets one public or internal descriptor or throws when unknown.</summary>
    public static FoundationOperationDescriptor GetRequired(string operationId) =>
        PublicOperations.Concat(InternalOperations).SingleOrDefault(item => string.Equals(item.OperationId, operationId, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Unknown Foundation operation: {operationId}");

    /// <summary>Resolves one public or internal descriptor without throwing.</summary>
    public static bool TryGet(string? operationId, out FoundationOperationDescriptor descriptor)
    {
        descriptor = null!;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return false;
        }

        var found = PublicOperations.Concat(InternalOperations)
            .SingleOrDefault(item => string.Equals(item.OperationId, operationId.Trim(), StringComparison.Ordinal));
        if (found is null)
        {
            return false;
        }

        descriptor = found;
        return true;
    }
}
