namespace MiniErp.App.BuildingBlocks.Tenancy;

/// <summary>
/// The only Tenant authorization paths supported by Release 1.
/// </summary>
public enum TenantAuthorizationPath
{
    /// <summary>Access through an active Tenant Membership.</summary>
    OrdinaryMembership = 1,
    /// <summary>Case-bound, Tenant-approved exceptional support access.</summary>
    SupportGrant = 2
}

/// <summary>
/// A constrained reference to the active ordinary membership.
/// </summary>
public readonly record struct MembershipReference
{
    /// <summary>Creates a validated membership reference.</summary>
    public MembershipReference(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Membership reference must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The underlying membership identifier.</summary>
    public Guid Value { get; }
}

/// <summary>
/// A constrained reference to a case-bound support grant.
/// </summary>
public readonly record struct SupportGrantReference
{
    /// <summary>Creates a validated support grant and case reference.</summary>
    public SupportGrantReference(Guid grantId, Guid caseId)
    {
        if (grantId == Guid.Empty)
        {
            throw new ArgumentException("Support grant reference must not be empty.", nameof(grantId));
        }

        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("Support case reference must not be empty.", nameof(caseId));
        }

        GrantId = grantId;
        CaseId = caseId;
    }

    /// <summary>The support grant identifier.</summary>
    public Guid GrantId { get; }

    /// <summary>The named support case identifier.</summary>
    public Guid CaseId { get; }
}

/// <summary>
/// A validated optional downward organization scope reference.
/// </summary>
public readonly record struct ScopeReference
{
    /// <summary>Creates a validated downward scope reference.</summary>
    public ScopeReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Scope reference must not be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>The constrained scope value.</summary>
    public string Value { get; }
}

/// <summary>
/// A validated correlation identifier carried by work and evidence contracts.
/// </summary>
public readonly record struct CorrelationId
{
    /// <summary>Creates a validated correlation identifier.</summary>
    public CorrelationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("CorrelationId must not be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>The correlation value.</summary>
    public string Value { get; }
}

/// <summary>
/// Immutable, server-derived authorization context for exactly one Tenant.
/// </summary>
/// <remarks>
/// The application/security resolver assembles this value from validated server
/// facts. Client-supplied Tenant identifiers may select a requested resource,
/// but never construct or expand this authority context.
/// </remarks>
public sealed class TenantContext
{
    private TenantContext(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        MembershipReference? membership,
        SupportGrantReference? supportGrant,
        ScopeReference? scope,
        CorrelationId? correlationId,
        Guid? actorId,
        bool isEmergencySuperAdministrator)
    {
        if (tenantId == default)
        {
            throw new ArgumentException("TenantContext requires a TenantId.", nameof(tenantId));
        }

        var hasMembership = membership.HasValue;
        var hasSupportGrant = supportGrant.HasValue;
        if (isEmergencySuperAdministrator
            ? authorizationPath != TenantAuthorizationPath.OrdinaryMembership || hasMembership || hasSupportGrant
            : hasMembership == hasSupportGrant)
        {
            throw new ArgumentException("TenantContext requires exactly one authorization path or the server-created emergency super-administrator marker.", nameof(authorizationPath));
        }

        if (authorizationPath is not TenantAuthorizationPath.OrdinaryMembership
            and not TenantAuthorizationPath.SupportGrant)
        {
            throw new ArgumentOutOfRangeException(nameof(authorizationPath));
        }

        if (authorizationPath == TenantAuthorizationPath.OrdinaryMembership && !hasMembership && !isEmergencySuperAdministrator)
        {
            throw new ArgumentException("OrdinaryMembership requires a membership reference.", nameof(membership));
        }

        if (authorizationPath == TenantAuthorizationPath.SupportGrant && !hasSupportGrant)
        {
            throw new ArgumentException("SupportGrant requires a support-grant reference.", nameof(supportGrant));
        }

        if (scope.HasValue && string.IsNullOrWhiteSpace(scope.Value.Value))
        {
            throw new ArgumentException("Scope reference must contain a value when supplied.", nameof(scope));
        }

        if (correlationId.HasValue && string.IsNullOrWhiteSpace(correlationId.Value.Value))
        {
            throw new ArgumentException("CorrelationId must contain a value when supplied.", nameof(correlationId));
        }

        TenantId = tenantId;
        AuthorizationPath = authorizationPath;
        Membership = membership;
        SupportGrant = supportGrant;
        Scope = scope;
        CorrelationId = correlationId;
        ActorId = actorId;
        IsEmergencySuperAdministrator = isEmergencySuperAdministrator;
    }

    /// <summary>The one trusted Tenant boundary.</summary>
    public TenantId TenantId { get; }

    /// <summary>The one selected authorization path.</summary>
    public TenantAuthorizationPath AuthorizationPath { get; }

    /// <summary>The ordinary membership reference when that path is selected.</summary>
    public MembershipReference? Membership { get; }

    /// <summary>The support grant reference when that path is selected.</summary>
    public SupportGrantReference? SupportGrant { get; }

    /// <summary>The optional validated downward organization scope.</summary>
    public ScopeReference? Scope { get; }

    /// <summary>The optional correlation carried into evidence.</summary>
    public CorrelationId? CorrelationId { get; }

    /// <summary>The optional actor identity resolved by the server.</summary>
    public Guid? ActorId { get; }

    /// <summary>True only for the server-configured emergency super-administrator path.</summary>
    public bool IsEmergencySuperAdministrator { get; }

    /// <summary>
    /// Creates the ordinary membership path from server-resolved membership facts.
    /// </summary>
    public static TenantContext ForOrdinaryMembership(
        TenantId tenantId,
        MembershipReference membership,
        ScopeReference? scope = null,
        CorrelationId? correlationId = null,
        Guid? actorId = null)
    {
        return new TenantContext(
            tenantId,
            TenantAuthorizationPath.OrdinaryMembership,
            membership,
            supportGrant: null,
            scope,
            correlationId,
            actorId,
            isEmergencySuperAdministrator: false);
    }

    /// <summary>
    /// Creates the exceptional case-bound support path from server-resolved facts.
    /// </summary>
    public static TenantContext ForSupportGrant(
        TenantId tenantId,
        SupportGrantReference supportGrant,
        ScopeReference? scope = null,
        CorrelationId? correlationId = null,
        Guid? actorId = null)
    {
        return new TenantContext(
            tenantId,
            TenantAuthorizationPath.SupportGrant,
            membership: null,
            supportGrant,
            scope,
            correlationId,
            actorId,
            isEmergencySuperAdministrator: false);
    }

    /// <summary>Creates the exceptional Tenant path only after server configuration validation.</summary>
    internal static TenantContext ForEmergencySuperAdministrator(
        TenantId tenantId,
        ScopeReference? scope,
        CorrelationId correlationId,
        Guid actorId) => new(
            tenantId,
            TenantAuthorizationPath.OrdinaryMembership,
            membership: null,
            supportGrant: null,
            scope,
            correlationId,
            actorId,
            isEmergencySuperAdministrator: true);

    /// <summary>
    /// Internal validation seam used by architecture tests and the server resolver.
    /// </summary>
    internal static TenantContext CreateValidated(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        MembershipReference? membership,
        SupportGrantReference? supportGrant,
        ScopeReference? scope = null,
        CorrelationId? correlationId = null,
        Guid? actorId = null)
    {
        return new TenantContext(
            tenantId,
            authorizationPath,
            membership,
            supportGrant,
            scope,
            correlationId,
            actorId,
            isEmergencySuperAdministrator: false);
    }
}
