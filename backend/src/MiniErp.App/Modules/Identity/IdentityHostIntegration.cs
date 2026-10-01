#pragma warning disable CS1591

using System.Security.Claims;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.Modules.Identity;

/// <summary>Server-owned context kinds exposed to the first-party shell.</summary>
public enum FoundationHostContextKind
{
    OrdinaryMembership = 1,
    SupportGrant = 2,
    PlatformGovernanceContext = 3
}

/// <summary>Safe context candidate; it contains no role, permission or token.</summary>
public sealed record FoundationHostContextCandidate(
    Guid ContextId,
    FoundationHostContextKind Kind,
    Guid? TenantId,
    string DisplayName,
    long EligibilityVersion);

/// <summary>Safe Company/Branch context candidate exposed to the shell.</summary>
public sealed record FoundationHostOperationalContextCandidate(
    Guid ContextId,
    Guid TenantId,
    string Kind,
    string DisplayName,
    long EligibilityVersion);

/// <summary>Safe session facts returned by the Foundation host.</summary>
public sealed record FoundationHostSessionState(
    bool Authenticated,
    Guid? ActorId,
    Guid? SessionId,
    string LifecycleState,
    DateTimeOffset? AbsoluteExpiresAt,
    long SelectionVersion,
    FoundationHostContextCandidate? SelectedContext,
    IReadOnlyList<FoundationHostContextCandidate> Contexts,
    string? DisplayName,
    string? Login);

/// <summary>Safe sign-in outcome. The raw opaque token is never a public property.</summary>
public sealed class FoundationHostSignInResult
{
    internal FoundationHostSignInResult(
        bool succeeded,
        string code,
        Guid? actorId,
        Guid? sessionId,
        ClaimsPrincipal? principal,
        FoundationHostSessionState? state)
    {
        Succeeded = succeeded;
        Code = code;
        ActorId = actorId;
        SessionId = sessionId;
        Principal = principal;
        State = state;
    }

    public bool Succeeded { get; }
    public string Code { get; }
    public Guid? ActorId { get; }
    public Guid? SessionId { get; }

    /// <summary>
    /// The claims principal for the host to sign in via cookie authentication
    /// on success. Public and unrelated to the durable-work effect ledger
    /// (H92-06): it carries only the claims this module already issues
    /// through <see cref="FoundationIdentityClaims"/>, never a raw credential.
    /// </summary>
    public ClaimsPrincipal? Principal { get; }

    public FoundationHostSessionState? State { get; }
}

/// <summary>Safe context-switch outcome.</summary>
public sealed record FoundationHostContextSwitchResult(
    bool Succeeded,
    string Code,
    FoundationHostSessionState? State);

/// <summary>Safe operational context switch outcome.</summary>
public sealed record FoundationHostOperationalContextSwitchResult(
    bool Succeeded,
    string Code,
    FoundationHostOperationalContextCandidate? SelectedContext,
    long SelectionVersion);

/// <summary>
/// Public host adapter over the internal MESP identity authority. Operations are
/// resolved from an approved descriptor; callers cannot supply permission text.
/// </summary>
public interface IFoundationIdentityHost
{
    FoundationHostSignInResult SignIn(string login, string password);

    FoundationHostSignInResult DevelopmentBypass(string login);

    bool ValidatePrincipal(ClaimsPrincipal principal);

    FoundationRequestContext ResolveContext(
        ClaimsPrincipal principal,
        string correlationId,
        FoundationOperationDescriptor descriptor);

    FoundationHostSessionState GetSession(ClaimsPrincipal principal);

    IReadOnlyList<FoundationHostContextCandidate> ListContexts(ClaimsPrincipal principal);

    FoundationHostContextSwitchResult SwitchContext(
        ClaimsPrincipal principal,
        Guid contextId,
        long expectedSelectionVersion,
        long expectedEligibilityVersion);

    bool SelectAuthorizedContext(ClaimsPrincipal principal, Guid contextId);

    void ClearSelectedContext(ClaimsPrincipal principal);

    IReadOnlyList<FoundationHostOperationalContextCandidate> ListOperationalContexts(ClaimsPrincipal principal);

    FoundationHostOperationalContextCandidate? GetSelectedOperationalContext(ClaimsPrincipal principal);

    long GetOperationalSelectionVersion(ClaimsPrincipal principal);

    bool SelectAuthorizedOperationalContext(ClaimsPrincipal principal, Guid contextId);

    void ClearSelectedOperationalContext(ClaimsPrincipal principal);

    FoundationHostOperationalContextSwitchResult SwitchOperationalContext(
        ClaimsPrincipal principal,
        Guid contextId,
        long expectedSelectionVersion,
        long expectedEligibilityVersion);

    FoundationRequestContext? ResolveCandidateContext(
        ClaimsPrincipal principal,
        Guid contextId,
        FoundationOperationDescriptor descriptor,
        string correlationId);

    FoundationRequestContext? ResolveCandidateOperationalContext(
        ClaimsPrincipal principal,
        Guid contextId,
        FoundationOperationDescriptor descriptor,
        string correlationId);

    bool Revoke(ClaimsPrincipal principal, string reason);
}

/// <summary>Claim names used only to locate the server-side session.</summary>
public static class FoundationIdentityClaims
{
    public const string SessionToken = "mesp.session.token";
    public const string SessionId = "mesp.session.id";
}

internal sealed class FoundationIdentityHost : IFoundationIdentityHost
{
    private static readonly Guid PlatformContextId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly IdentityAuthorizationService identity;
    private readonly ITenantDisplayNameProvider tenantDisplayNames;
    private readonly IFoundationOperationalContextProvider operationalContextProvider;
    private readonly object selectionLock = new();
    private readonly Dictionary<SessionId, SelectedContext> selectedContexts = [];
    private readonly Dictionary<SessionId, SelectedOperationalContext> selectedOperationalContexts = [];

    private sealed record SelectedContext(
        FoundationHostContextKind Kind,
        Guid ContextId,
        long SelectionVersion,
        long EligibilityVersion);

    private sealed record SelectedOperationalContext(
        Guid ContextId,
        long SelectionVersion,
        long EligibilityVersion);

    internal FoundationIdentityHost(
        IdentityAuthorizationService identity,
        ITenantDisplayNameProvider? tenantDisplayNames = null,
        IFoundationOperationalContextProvider? operationalContextProvider = null)
    {
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        this.tenantDisplayNames = tenantDisplayNames ?? new DefaultTenantDisplayNameProvider();
        this.operationalContextProvider = operationalContextProvider ?? new NoFoundationOperationalContextProvider();
    }

    public FoundationHostSignInResult SignIn(string login, string password)
    {
        try
        {
            var result = identity.Authenticate(login, password);
            if (!result.Succeeded || result.SessionId is null || result.CookieValue is null)
            {
                return new FoundationHostSignInResult(false, "authentication_failed", null, null, null, null);
            }

            var validation = identity.ValidateSession(result.CookieValue);
            if (!validation.Valid || validation.UserId is null)
            {
                return new FoundationHostSignInResult(false, "authentication_failed", null, null, null, null);
            }

            var principal = CreatePrincipal(result, validation.UserId.Value);
            return new FoundationHostSignInResult(
                true,
                "authenticated",
                validation.UserId.Value.Value,
                result.SessionId.Value.Value,
                principal,
                GetSession(principal));
        }
        catch (ArgumentException)
        {
            return new FoundationHostSignInResult(false, "authentication_failed", null, null, null, null);
        }
    }

    public FoundationHostSignInResult DevelopmentBypass(string login)
    {
        try
        {
            var result = identity.AuthenticateDevelopment(login);
            if (!result.Succeeded || result.SessionId is null || result.CookieValue is null)
            {
                return new FoundationHostSignInResult(false, "authentication_failed", null, null, null, null);
            }

            var validation = identity.ValidateSession(result.CookieValue);
            if (!validation.Valid || validation.UserId is null)
            {
                return new FoundationHostSignInResult(false, "authentication_failed", null, null, null, null);
            }

            var principal = CreatePrincipal(result, validation.UserId.Value);
            return new FoundationHostSignInResult(
                true,
                "authenticated",
                validation.UserId.Value.Value,
                result.SessionId.Value.Value,
                principal,
                GetSession(principal));
        }
        catch (ArgumentException)
        {
            return new FoundationHostSignInResult(false, "authentication_failed", null, null, null, null);
        }
    }

    public bool ValidatePrincipal(ClaimsPrincipal principal) =>
        TryReadSession(principal, out var token, out var claimedSessionId, out var claimedActorId)
        && identity.ValidateSession(token) is { Valid: true, SessionId: { } sessionId, UserId: { } userId }
        && sessionId.Value == claimedSessionId
        && userId.Value == claimedActorId;

    public FoundationRequestContext ResolveContext(
        ClaimsPrincipal principal,
        string correlationId,
        FoundationOperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!FoundationOperationCatalog.TryGet(descriptor.OperationId, out var catalogDescriptor)
            || catalogDescriptor.Visibility != FoundationOperationVisibility.Public
            || catalogDescriptor != descriptor)
        {
            return FoundationRequestContext.Unauthenticated();
        }

        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return FoundationRequestContext.Unauthenticated();
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.SessionId is null || validation.UserId is null)
        {
            return FoundationRequestContext.Unauthenticated();
        }

        var sessionId = validation.SessionId.Value;
        var actorId = validation.UserId.Value.Value;
        SelectedContext? selected;
        lock (selectionLock)
        {
            selectedContexts.TryGetValue(sessionId, out selected);
        }

        // Session-only operations remain session-scoped until a selected path
        // is needed for conditional lifecycle evidence (for example sign-out).
        if (selected is null)
        {
            return FoundationRequestContext.ForAuthenticatedSession(
                actorId,
                sessionId.Value,
                descriptor.ExactPermissionCode ?? "authenticated.session");
        }

        var effectiveDescriptor = descriptor.OperationId == "auth.sign-out"
            ? selected.Kind switch
            {
                FoundationHostContextKind.OrdinaryMembership => FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"),
                FoundationHostContextKind.SupportGrant => FoundationOperationCatalog.GetRequired("foundation.support-context.read"),
                _ => FoundationOperationCatalog.GetRequired("foundation.platform-context.read")
            }
            : descriptor;
        var correlation = new CorrelationId(string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId);

        lock (identity.Store.SyncRoot)
        {
            if (!identity.Store.Users.TryGetValue(validation.UserId.Value, out var user)
                || user.Status != GlobalUserStatus.Active)
            {
                return FoundationRequestContext.Unauthenticated();
            }

            if (selected.Kind == FoundationHostContextKind.OrdinaryMembership
                && identity.Store.Memberships.TryGetValue(new MembershipId(selected.ContextId), out var membership)
                && membership.UserId == validation.UserId.Value
                && membership.Status == MembershipStatus.Active)
            {
                if (!IdentityPermissions.TryResolve(effectiveDescriptor.ExactPermissionCode, out var permission)
                    || effectiveDescriptor.ScopePolicy is not (FoundationScopePolicy.Tenant or FoundationScopePolicy.None))
                {
                    return FoundationRequestContext.Unauthenticated();
                }

                var requestedScope = OrganizationScope.ForTenant(membership.TenantId);
                var selectedOperational = GetSelectedOperationalUnsafe(
                    validation.SessionId.Value,
                    membership);
                if (selectedOperational is not null)
                {
                    requestedScope = ToOrganizationScope(selectedOperational);
                }

                var decision = identity.AuthorizeOrdinary(
                    token,
                    membership.TenantId,
                    permission,
                    requestedScope,
                    correlation,
                    allowTenantScopeExpansion: selectedOperational is not null);
                if (decision.Allowed && decision.TenantContext is not null)
                {
                    return FoundationRequestContext.ForTenant(
                        actorId,
                        sessionId.Value,
                        decision.TenantContext,
                        permission.Value);
                }

                // The membership is still valid; only this operation is denied.
                // Keep the selection so the caller receives 403, not 401.
                return FoundationRequestContext.ForAuthenticatedSession(actorId, sessionId.Value);
            }

            if (selected.Kind == FoundationHostContextKind.SupportGrant
                && identity.Store.SupportGrants.TryGetValue(new SupportGrantId(selected.ContextId), out var grant)
                && grant.UserId == validation.UserId.Value
                && grant.RevokedAt is null
                && grant.ExpiresAt > identity.Now
                && identity.Store.SupportCases.TryGetValue(grant.CaseId, out var supportCase)
                && supportCase.IsActive)
            {
                if (!IdentityPermissions.TryResolve(effectiveDescriptor.ExactPermissionCode, out var permission)
                    || effectiveDescriptor.ScopePolicy is not (FoundationScopePolicy.SupportGrant or FoundationScopePolicy.None))
                {
                    return FoundationRequestContext.Unauthenticated();
                }

                var decision = identity.AuthorizeSupport(
                    token,
                    grant.TenantId,
                    grant.Id,
                    permission,
                    grant.Scope,
                    grant.Purpose,
                    correlation);
                if (decision.Allowed && decision.TenantContext is not null)
                {
                    return FoundationRequestContext.ForTenant(
                        actorId,
                        sessionId.Value,
                        decision.TenantContext,
                        permission.Value);
                }
            }

            if (selected.Kind == FoundationHostContextKind.PlatformGovernanceContext
                && IdentityPermissions.TryResolve(effectiveDescriptor.ExactPermissionCode, out var platformPermission)
                && effectiveDescriptor.ScopePolicy is FoundationScopePolicy.PlatformGovernance
                && identity.AuthorizePlatformOperation(
                    token,
                    validation.UserId.Value,
                    platformPermission,
                    effectiveDescriptor.OperationId,
                    effectiveDescriptor.RequiresMfa,
                    effectiveDescriptor.RequiresFreshAuthentication))
            {
                return FoundationRequestContext.ForPlatform(
                    actorId,
                    sessionId.Value,
                    new PlatformGovernanceContext(actorId, PlatformGovernancePurpose.PlatformMetadata, correlation),
                    platformPermission.Value);
            }
        }

        RemoveSelection(sessionId);
        return descriptor.SecurityProfile is FoundationSecurityProfile.AuthenticatedSession
            ? FoundationRequestContext.ForAuthenticatedSession(actorId, sessionId.Value)
            : FoundationRequestContext.Unauthenticated();
    }

    public FoundationHostSessionState GetSession(ClaimsPrincipal principal)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return AnonymousState();
        }

        var validation = identity.ValidateSession(token);
        return !validation.Valid || validation.SessionId is null || validation.UserId is null
            ? AnonymousState()
            : BuildState(validation.UserId.Value, validation.SessionId.Value, token);
    }

    public IReadOnlyList<FoundationHostContextCandidate> ListContexts(ClaimsPrincipal principal)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return [];
        }

        var validation = identity.ValidateSession(token);
        return !validation.Valid || validation.UserId is null
            ? []
            : ListContexts(validation.UserId.Value, token);
    }

    public bool SelectAuthorizedContext(ClaimsPrincipal principal, Guid contextId)
    {
        if (!TryReadSession(principal, out var token, out _, out _)
            || contextId == Guid.Empty)
        {
            return false;
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.SessionId is null || validation.UserId is null)
        {
            return false;
        }

        var candidate = ListContexts(validation.UserId.Value, token)
            .SingleOrDefault(item => item.ContextId == contextId);
        if (candidate is null)
        {
            return false;
        }

        lock (selectionLock)
        {
            selectedContexts.TryGetValue(validation.SessionId.Value, out var current);
            if (current is not null
                && current.Kind == candidate.Kind
                && current.ContextId == candidate.ContextId
                && current.EligibilityVersion == candidate.EligibilityVersion)
            {
                return true;
            }

            selectedContexts[validation.SessionId.Value] = new SelectedContext(
                candidate.Kind,
                candidate.ContextId,
                (current?.SelectionVersion ?? 0) + 1,
                candidate.EligibilityVersion);
            selectedOperationalContexts.Remove(validation.SessionId.Value);
        }

        return true;
    }

    public void ClearSelectedContext(ClaimsPrincipal principal)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return;
        }

        var validation = identity.ValidateSession(token);
        if (validation.Valid && validation.SessionId is not null)
        {
            RemoveSelection(validation.SessionId.Value);
        }
    }

    public IReadOnlyList<FoundationHostOperationalContextCandidate> ListOperationalContexts(ClaimsPrincipal principal)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return [];
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.UserId is null || validation.SessionId is null)
        {
            return [];
        }

        SelectedContext? selected;
        lock (selectionLock)
        {
            selectedContexts.TryGetValue(validation.SessionId.Value, out selected);
        }

        if (selected is not { Kind: FoundationHostContextKind.OrdinaryMembership })
        {
            return [];
        }

        lock (identity.Store.SyncRoot)
        {
            if (!identity.Store.Memberships.TryGetValue(new MembershipId(selected.ContextId), out var membership)
                || membership.UserId != validation.UserId.Value
                || membership.Status != MembershipStatus.Active
                || !HasOrdinaryContextReadUnsafe(membership))
            {
                return [];
            }

            return operationalContextProvider.List(membership.TenantId)
                .Where(candidate => CanUseOperationalContextUnsafe(membership, candidate))
                .Select(candidate => new FoundationHostOperationalContextCandidate(
                    candidate.ContextId,
                    candidate.TenantId,
                    candidate.Kind,
                    candidate.DisplayName,
                    candidate.EligibilityVersion))
                .ToArray();
        }
    }

    public FoundationHostOperationalContextCandidate? GetSelectedOperationalContext(ClaimsPrincipal principal)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return null;
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.SessionId is null)
        {
            return null;
        }

        SelectedOperationalContext? selected;
        lock (selectionLock)
        {
            selectedOperationalContexts.TryGetValue(validation.SessionId.Value, out selected);
        }

        return selected is null
            ? null
            : ListOperationalContexts(principal).SingleOrDefault(item => item.ContextId == selected.ContextId);
    }

    public long GetOperationalSelectionVersion(ClaimsPrincipal principal)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return 0;
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.SessionId is null)
        {
            return 0;
        }

        lock (selectionLock)
        {
            return selectedOperationalContexts.TryGetValue(validation.SessionId.Value, out var selected)
                ? selected.SelectionVersion
                : 0;
        }
    }

    public bool SelectAuthorizedOperationalContext(ClaimsPrincipal principal, Guid contextId)
    {
        if (!TryReadSession(principal, out var token, out _, out _)
            || contextId == Guid.Empty)
        {
            return false;
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.SessionId is null)
        {
            return false;
        }

        var candidate = ListOperationalContexts(principal).SingleOrDefault(item => item.ContextId == contextId);
        if (candidate is null)
        {
            return false;
        }

        lock (selectionLock)
        {
            selectedOperationalContexts.TryGetValue(validation.SessionId.Value, out var current);
            if (current is not null
                && current.ContextId == candidate.ContextId
                && current.EligibilityVersion == candidate.EligibilityVersion)
            {
                return true;
            }

            selectedOperationalContexts[validation.SessionId.Value] = new SelectedOperationalContext(
                candidate.ContextId,
                (current?.SelectionVersion ?? 0) + 1,
                candidate.EligibilityVersion);
        }

        return true;
    }

    public void ClearSelectedOperationalContext(ClaimsPrincipal principal)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return;
        }

        var validation = identity.ValidateSession(token);
        if (validation.Valid && validation.SessionId is not null)
        {
            lock (selectionLock)
            {
                selectedOperationalContexts.Remove(validation.SessionId.Value);
            }
        }
    }

    public FoundationHostOperationalContextSwitchResult SwitchOperationalContext(
        ClaimsPrincipal principal,
        Guid contextId,
        long expectedSelectionVersion,
        long expectedEligibilityVersion)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return new FoundationHostOperationalContextSwitchResult(false, "authentication_failed", null, 0);
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.UserId is null || validation.SessionId is null || contextId == Guid.Empty)
        {
            return new FoundationHostOperationalContextSwitchResult(false, "access_denied", null, 0);
        }

        var candidate = ListOperationalContexts(principal).SingleOrDefault(item => item.ContextId == contextId);
        if (candidate is null || candidate.EligibilityVersion != expectedEligibilityVersion || expectedSelectionVersion < 0)
        {
            return new FoundationHostOperationalContextSwitchResult(false, "context_version_conflict", null, GetOperationalSelectionVersion(principal));
        }

        lock (selectionLock)
        {
            selectedOperationalContexts.TryGetValue(validation.SessionId.Value, out var current);
            var currentVersion = current?.SelectionVersion ?? 0;
            if (currentVersion != expectedSelectionVersion)
            {
                return new FoundationHostOperationalContextSwitchResult(false, "context_version_conflict", null, currentVersion);
            }

            var nextVersion = currentVersion + 1;
            selectedOperationalContexts[validation.SessionId.Value] = new SelectedOperationalContext(
                candidate.ContextId,
                nextVersion,
                candidate.EligibilityVersion);
            return new FoundationHostOperationalContextSwitchResult(true, "operational_context_selected", candidate, nextVersion);
        }
    }

    public FoundationHostContextSwitchResult SwitchContext(
        ClaimsPrincipal principal,
        Guid contextId,
        long expectedSelectionVersion,
        long expectedEligibilityVersion)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return new FoundationHostContextSwitchResult(false, "authentication_failed", null);
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.UserId is null || validation.SessionId is null || contextId == Guid.Empty)
        {
            return new FoundationHostContextSwitchResult(false, "access_denied", null);
        }

        var descriptor = FoundationOperationCatalog.GetRequired("auth.context-switch");
        var candidateContext = ResolveCandidateContext(principal, contextId, descriptor, Guid.NewGuid().ToString("N"));
        if (candidateContext is null)
        {
            return new FoundationHostContextSwitchResult(false, "access_denied", null);
        }

        var available = ListContexts(validation.UserId.Value, token);
        var candidate = available.SingleOrDefault(item => item.ContextId == contextId);
        if (candidate is null || candidate.EligibilityVersion != expectedEligibilityVersion)
        {
            return new FoundationHostContextSwitchResult(false, "context_version_conflict", null);
        }

        lock (selectionLock)
        {
            selectedContexts.TryGetValue(validation.SessionId.Value, out var current);
            var currentSelectionVersion = current?.SelectionVersion ?? 0;
            if (currentSelectionVersion != expectedSelectionVersion)
            {
                return new FoundationHostContextSwitchResult(false, "context_version_conflict", null);
            }

            var nextSelectionVersion = currentSelectionVersion + 1;
            selectedContexts[validation.SessionId.Value] = new SelectedContext(
                candidate.Kind,
                candidate.ContextId,
                nextSelectionVersion,
                candidate.EligibilityVersion);
        }

        return new FoundationHostContextSwitchResult(true, "context_selected", BuildState(validation.UserId.Value, validation.SessionId.Value, token));
    }

    public FoundationRequestContext? ResolveCandidateContext(
        ClaimsPrincipal principal,
        Guid contextId,
        FoundationOperationDescriptor descriptor,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!FoundationOperationCatalog.TryGet(descriptor.OperationId, out var catalogDescriptor)
            || catalogDescriptor.Visibility != FoundationOperationVisibility.Public
            || catalogDescriptor != descriptor
            || !TryReadSession(principal, out var token, out _, out _))
        {
            return null;
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.UserId is null || validation.SessionId is null)
        {
            return null;
        }

        var candidate = ListContexts(validation.UserId.Value, token).SingleOrDefault(item => item.ContextId == contextId);
        if (candidate is null)
        {
            return null;
        }

        var correlation = new CorrelationId(string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId);
        lock (identity.Store.SyncRoot)
        {
            if (candidate.Kind == FoundationHostContextKind.OrdinaryMembership
                && identity.Store.Memberships.TryGetValue(new MembershipId(contextId), out var membership))
            {
                if (!IdentityPermissions.TryResolve(descriptor.ExactPermissionCode, out var permission))
                {
                    return null;
                }

                var decision = identity.AuthorizeOrdinary(
                    token,
                    membership.TenantId,
                    permission,
                    OrganizationScope.ForTenant(membership.TenantId),
                    correlation);
                return decision.Allowed && decision.TenantContext is not null
                    ? FoundationRequestContext.ForTenant(validation.UserId.Value.Value, validation.SessionId.Value.Value, decision.TenantContext, permission.Value)
                    : null;
            }

            if (candidate.Kind == FoundationHostContextKind.SupportGrant
                && identity.Store.SupportGrants.TryGetValue(new SupportGrantId(contextId), out var grant))
            {
                // Context selection is a session operation; SupportRead is the
                // exact grant permission required to select and read the case.
                var supportPermission = descriptor.OperationId == "auth.context-switch"
                    ? IdentityPermissions.SupportRead
                    : IdentityPermissions.TryResolve(descriptor.ExactPermissionCode, out var resolved)
                        ? resolved
                        : default;
                if (supportPermission.Equals(default(PermissionCode)))
                {
                    return null;
                }

                var decision = identity.AuthorizeSupport(
                    token,
                    grant.TenantId,
                    grant.Id,
                    supportPermission,
                    grant.Scope,
                    grant.Purpose,
                    correlation);
                return decision.Allowed && decision.TenantContext is not null
                    ? FoundationRequestContext.ForTenant(validation.UserId.Value.Value, validation.SessionId.Value.Value, decision.TenantContext, supportPermission.Value)
                    : null;
            }

            if (candidate.Kind == FoundationHostContextKind.PlatformGovernanceContext
                && (descriptor.OperationId == "auth.context-switch"
                    ? IdentityPermissions.TryResolve(
                        FoundationOperationCatalog.GetRequired("foundation.platform-context.read").ExactPermissionCode,
                        out var platformPermission)
                    : IdentityPermissions.TryResolve(descriptor.ExactPermissionCode, out platformPermission))
                && identity.AuthorizePlatformOperation(
                    token,
                    validation.UserId.Value,
                    platformPermission,
                    descriptor.OperationId,
                    descriptor.RequiresMfa,
                    descriptor.RequiresFreshAuthentication))
            {
                return FoundationRequestContext.ForPlatform(
                    validation.UserId.Value.Value,
                    validation.SessionId.Value.Value,
                    new PlatformGovernanceContext(validation.UserId.Value.Value, PlatformGovernancePurpose.PlatformMetadata, correlation),
                    platformPermission.Value);
            }
        }

        return null;
    }

    public FoundationRequestContext? ResolveCandidateOperationalContext(
        ClaimsPrincipal principal,
        Guid contextId,
        FoundationOperationDescriptor descriptor,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!FoundationOperationCatalog.TryGet(descriptor.OperationId, out var catalogDescriptor)
            || catalogDescriptor.Visibility != FoundationOperationVisibility.Public
            || catalogDescriptor != descriptor
            || !TryReadSession(principal, out var token, out _, out _))
        {
            return null;
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.UserId is null || validation.SessionId is null)
        {
            return null;
        }

        var candidate = ListOperationalContexts(principal).SingleOrDefault(item => item.ContextId == contextId);
        if (candidate is null || !IdentityPermissions.TryResolve(descriptor.ExactPermissionCode, out var permission))
        {
            return null;
        }

        SelectedContext? selected;
        lock (selectionLock)
        {
            selectedContexts.TryGetValue(validation.SessionId.Value, out selected);
        }

        if (selected is not { Kind: FoundationHostContextKind.OrdinaryMembership })
        {
            return null;
        }

        if (!operationalContextProvider.TryGet(candidate.ContextId, out var providerCandidate))
        {
            return null;
        }

        var requestedScope = ToOrganizationScope(providerCandidate);
        var correlationValue = new CorrelationId(string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId);
        lock (identity.Store.SyncRoot)
        {
            if (!identity.Store.Memberships.TryGetValue(new MembershipId(selected.ContextId), out var membership))
            {
                return null;
            }

            var decision = identity.AuthorizeOrdinary(
                token,
                membership.TenantId,
                permission,
                requestedScope,
                correlationValue,
                allowTenantScopeExpansion: true);
            return decision.Allowed && decision.TenantContext is not null
                ? FoundationRequestContext.ForTenant(validation.UserId.Value.Value, validation.SessionId.Value.Value, decision.TenantContext, permission.Value)
                : null;
        }
    }

    public bool Revoke(ClaimsPrincipal principal, string reason)
    {
        if (!TryReadSession(principal, out var token, out _, out _))
        {
            return false;
        }

        var validation = identity.ValidateSession(token);
        if (!validation.Valid || validation.SessionId is null)
        {
            return false;
        }

        identity.RevokeSession(validation.SessionId.Value, string.IsNullOrWhiteSpace(reason) ? "sign-out" : reason);
        RemoveSelection(validation.SessionId.Value);
        return true;
    }

    private FoundationHostSessionState BuildState(UserId userId, SessionId sessionId, string cookieValue)
    {
        DateTimeOffset? expiresAt = null;
        string? login;
        lock (identity.Store.SyncRoot)
        {
            if (!identity.Store.Sessions.TryGetValue(sessionId, out var session)
                || session.UserId != userId
                || !identity.Store.Users.TryGetValue(userId, out var user))
            {
                return AnonymousState();
            }

            expiresAt = session.AbsoluteExpiresAt;
            login = user.NormalizedEmail.ToLowerInvariant();
        }

        SelectedContext? selected;
        lock (selectionLock)
        {
            selectedContexts.TryGetValue(sessionId, out selected);
        }

        var contexts = ListContexts(userId, cookieValue);
        var selectedCandidate = selected is null
            ? null
            : contexts.SingleOrDefault(item => item.ContextId == selected.ContextId);
        if (selected is not null && selectedCandidate is null)
        {
            RemoveSelection(sessionId);
        }

        return new FoundationHostSessionState(
            true,
            userId.Value,
            sessionId.Value,
            "Active",
            expiresAt,
            selected?.SelectionVersion ?? 0,
            selectedCandidate,
            contexts,
            null,
            login);
    }

    private IReadOnlyList<FoundationHostContextCandidate> ListContexts(UserId userId, string cookieValue)
    {
        lock (identity.Store.SyncRoot)
        {
            if (!identity.Store.Users.TryGetValue(userId, out var user) || user.Status != GlobalUserStatus.Active)
            {
                return [];
            }

            var results = new List<FoundationHostContextCandidate>();
            foreach (var membership in identity.Store.Memberships.Values
                         .Where(item => item.UserId == userId && item.Status == MembershipStatus.Active)
                         .OrderBy(item => item.TenantId.Value))
            {
                if (HasOrdinaryContextReadUnsafe(membership))
                {
                    results.Add(new FoundationHostContextCandidate(
                        membership.Id.Value,
                        FoundationHostContextKind.OrdinaryMembership,
                        membership.TenantId.Value,
                        tenantDisplayNames.GetDisplayName(membership.TenantId),
                        membership.Version));
                }
            }

            foreach (var grant in identity.Store.SupportGrants.Values
                         .Where(item => item.UserId == userId && item.RevokedAt is null && item.ExpiresAt > identity.Now)
                         .OrderBy(item => item.TenantId.Value))
            {
                if (identity.Store.SupportCases.TryGetValue(grant.CaseId, out var supportCase)
                    && supportCase.IsActive
                    && grant.Permissions.Contains(IdentityPermissions.SupportRead)
                    && identity.HasCurrentAuthenticationAssurance(cookieValue, $"support:{grant.Id.Value}:{IdentityPermissions.SupportRead.Value}"))
                {
                    results.Add(new FoundationHostContextCandidate(
                        grant.Id.Value,
                        FoundationHostContextKind.SupportGrant,
                        grant.TenantId.Value,
                        $"Support · {tenantDisplayNames.GetDisplayName(grant.TenantId)}",
                        grant.Version));
                }
            }

            var platformDescriptor = FoundationOperationCatalog.GetRequired("foundation.platform-context.read");
            if (IdentityPermissions.TryResolve(platformDescriptor.ExactPermissionCode, out var platformPermission)
                && identity.AuthorizePlatformOperation(
                    cookieValue,
                    userId,
                    platformPermission,
                    platformDescriptor.OperationId,
                    platformDescriptor.RequiresMfa,
                    platformDescriptor.RequiresFreshAuthentication))
            {
                results.Add(new FoundationHostContextCandidate(
                    PlatformContextId,
                    FoundationHostContextKind.PlatformGovernanceContext,
                    null,
                    "Platform governance",
                    1));
            }

            return results;
        }
    }

    private bool HasOrdinaryContextReadUnsafe(TenantMembership membership) =>
        identity.Store.RoleAssignments.TryGetValue(membership.Id, out var assignments)
        && assignments.Any(assignment => assignment.IsActive
            && identity.Store.Roles.TryGetValue(assignment.RoleId, out var role)
            && role.Permissions.Contains(IdentityPermissions.ContextRead))
        && identity.Store.ScopeGrantsByMembership.TryGetValue(membership.Id, out var grants)
        && grants.Any(id => identity.Store.ScopeGrants.TryGetValue(id, out var grant) && grant.IsActive);

    private bool CanUseOperationalContextUnsafe(
        TenantMembership membership,
        FoundationOperationalContextCandidate candidate) =>
        candidate.TenantId == membership.TenantId.Value
        && identity.HasOrdinaryScope(membership, ToOrganizationScope(candidate), allowTenantScopeExpansion: true);

    private FoundationOperationalContextCandidate? GetSelectedOperationalUnsafe(
        SessionId sessionId,
        TenantMembership membership)
    {
        SelectedOperationalContext? selected;
        lock (selectionLock)
        {
            selectedOperationalContexts.TryGetValue(sessionId, out selected);
        }

        if (selected is null
            || !operationalContextProvider.TryGet(selected.ContextId, out var candidate)
            || candidate.EligibilityVersion != selected.EligibilityVersion
            || !CanUseOperationalContextUnsafe(membership, candidate))
        {
            if (selected is not null)
            {
                lock (selectionLock)
                {
                    selectedOperationalContexts.Remove(sessionId);
                }
            }

            return null;
        }

        return candidate;
    }

    private static OrganizationScope ToOrganizationScope(FoundationOperationalContextCandidate candidate)
    {
        var tenantId = new TenantId(candidate.TenantId);
        return candidate.Kind switch
        {
            "Company" => OrganizationScope.ForCompany(tenantId, candidate.TargetId),
            "Branch" => OrganizationScope.ForBranch(tenantId, candidate.TargetId),
            _ => throw new InvalidOperationException("Unknown operational context kind.")
        };
    }

    private void RemoveSelection(SessionId sessionId)
    {
        lock (selectionLock)
        {
            selectedContexts.Remove(sessionId);
            selectedOperationalContexts.Remove(sessionId);
        }
    }

    private static ClaimsPrincipal CreatePrincipal(AuthenticationResult result, UserId userId)
    {
        var identity = new ClaimsIdentity(
            FirstPartyCookieConfiguration.Scheme,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString("D")));
        identity.AddClaim(new Claim(FoundationIdentityClaims.SessionId, result.SessionId is null ? string.Empty : result.SessionId.Value.Value.ToString("D")));
        identity.AddClaim(new Claim(FoundationIdentityClaims.SessionToken, result.CookieValue ?? string.Empty));
        return new ClaimsPrincipal(identity);
    }

    private static bool TryReadSession(
        ClaimsPrincipal principal,
        out string token,
        out Guid sessionId,
        out Guid actorId)
    {
        token = principal.FindFirstValue(FoundationIdentityClaims.SessionToken) ?? string.Empty;
        sessionId = Guid.Empty;
        actorId = Guid.Empty;
        return principal.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirstValue(FoundationIdentityClaims.SessionId), out sessionId)
            && sessionId != Guid.Empty
            && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            && actorId != Guid.Empty
            && !string.IsNullOrWhiteSpace(token);
    }

    private static FoundationHostSessionState AnonymousState() =>
        new(false, null, null, "Unauthenticated", null, 0, null, [], null, null);
}

#pragma warning restore CS1591
