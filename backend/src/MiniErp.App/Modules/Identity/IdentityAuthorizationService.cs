using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;

namespace MiniErp.App.Modules.Identity;

/// <summary>
/// Server-side identity and authorization seam for the Release 1 foundation.
/// </summary>
/// <remarks>
/// This deliberately uses an isolated in-memory store. Durable EF persistence,
/// migrations, public endpoints and provider validation belong to later,
/// explicitly sequenced work. The security rules and contracts are kept here
/// so those later adapters cannot bypass the approved policy.
/// </remarks>
internal sealed class IdentityAuthorizationService :
    IOrganizationScopeOwnershipResolver,
    ICurrentOrganizationScopeResolver,
    IDurableWorkAuthorityRevalidator,
    IDurableWorkReconciliationAuthorizer,
    INotificationRecipientAuthorizer
{
    private const string GenericDeniedCode = "access_denied";
    private const string GenericAuthenticationCode = "authentication_failed";
    private readonly IdentityStore store;
    private readonly IdentitySecurityOptions options;
    private readonly TimeProvider timeProvider;
    private readonly IPasswordHasher<GlobalUser> passwordHasher;
    private readonly IAuthenticationAssuranceEvidenceSource assuranceEvidenceSource;
    private readonly IDurableWorkOperationCatalogue operationCatalogue;

    internal IdentityAuthorizationService(
        IdentityStore? store = null,
        IdentitySecurityOptions? options = null,
        TimeProvider? timeProvider = null,
        IPasswordHasher<GlobalUser>? passwordHasher = null,
        IAuthenticationAssuranceEvidenceSource? assuranceEvidenceSource = null,
        IDurableWorkOperationCatalogue? operationCatalogue = null)
    {
        this.store = store ?? new IdentityStore();
        this.options = options ?? IdentitySecurityOptions.Default;
        this.options.Validate();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.passwordHasher = passwordHasher ?? new PasswordHasher<GlobalUser>();
        this.assuranceEvidenceSource = assuranceEvidenceSource ?? new UnavailableAuthenticationAssuranceEvidenceSource();
        this.operationCatalogue = operationCatalogue ?? DurableWorkOperationCatalogue.Empty;
    }

    internal IdentityStore Store => store;

    internal IdentitySecurityOptions Options => options;

    internal IDurableWorkOperationCatalogue OperationCatalogue => operationCatalogue;

    internal DateTimeOffset Now => timeProvider.GetUtcNow();

    /// <summary>
    /// Resolves a requested work target through the Identity-owned organization
    /// graph and the currently selected Tenant authorization path.
    /// </summary>
    public TenantWorkScopeResolution Resolve(
        TenantContext trustedTenantContext,
        TenantWorkScopeRequest requestedScope)
    {
        ArgumentNullException.ThrowIfNull(trustedTenantContext);
        ArgumentNullException.ThrowIfNull(requestedScope);

        lock (store.SyncRoot)
        {
            if (!TryResolveOrganizationScopeUnsafe(
                    trustedTenantContext.TenantId,
                    requestedScope,
                    out var organizationScope)
                || !IsCurrentScopeContainedUnsafe(trustedTenantContext, organizationScope))
            {
                return TenantWorkScopeResolution.Denied("scope_not_authorized");
            }

            return TenantWorkScopeResolution.Resolved(
                TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
        }
    }

    public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext)
    {
        ArgumentNullException.ThrowIfNull(trustedTenantContext);
        lock (store.SyncRoot)
        {
            if (!TryResolveContextScopeUnsafe(trustedTenantContext, out var selected))
                return TenantWorkScopeResolution.Denied("scope_not_authorized");
            var request = selected.Kind switch
            {
                ScopeKind.Tenant => TenantWorkScopeRequest.TenantWide(),
                ScopeKind.Company => TenantWorkScopeRequest.ForCompany(selected.TargetId),
                ScopeKind.Branch when store.ParentScopes.TryGetValue((selected.TenantId, selected.Kind, selected.TargetId), out var company) && company is { Kind: ScopeKind.Company } => TenantWorkScopeRequest.ForBranch(company.Value.TargetId, selected.TargetId),
                ScopeKind.Warehouse when store.ParentScopes.TryGetValue((selected.TenantId, selected.Kind, selected.TargetId), out var branch) && branch is { Kind: ScopeKind.Branch } && store.ParentScopes.TryGetValue((branch.Value.TenantId, branch.Value.Kind, branch.Value.TargetId), out var warehouseCompany) && warehouseCompany is { Kind: ScopeKind.Company } => TenantWorkScopeRequest.ForWarehouse(warehouseCompany.Value.TargetId, branch.Value.TargetId, selected.TargetId),
                _ => null
            };
            return request is null ? TenantWorkScopeResolution.Denied("scope_not_authorized") : Resolve(trustedTenantContext, request);
        }
    }

    /// <summary>
    /// Re-resolves all durable-work authority facts immediately before a handler
    /// can be reached. No submission-time snapshot is used as current authority.
    /// </summary>
    public ValueTask<DurableWorkAuthorityValidationResult> RevalidateAsync(
        DurableWorkItem workItem,
        TenantContext currentTenantContext,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(currentTenantContext);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(
                DurableWorkAuthorityValidationResult.Cancelled("authority_validation_cancelled"));
        }

        DurableWorkAuthorityValidationResult result;
        lock (store.SyncRoot)
        {
            result = RevalidateDurableWorkUnsafe(workItem, currentTenantContext, now);
        }

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Live-authorizes exactly one Tenant-scoped durable-work reconciliation
    /// read using the same organization-scope ownership and current-authority
    /// checks as durable-work dispatch revalidation, gated by the dedicated
    /// <see cref="IdentityPermissions.DurableWorkReconciliationRead"/>
    /// catalogue permission. A raw TenantContext alone is never sufficient.
    /// </summary>
    public ValueTask<DurableWorkReconciliationAuthorizationResult> AuthorizeAsync(
        TenantContext currentTenantContext,
        Guid sessionId,
        TenantWorkScopeRequest requestedReadScope,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentTenantContext);
        ArgumentNullException.ThrowIfNull(requestedReadScope);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(
                DurableWorkReconciliationAuthorizationResult.Denied("reconciliation_authorization_cancelled"));
        }

        DurableWorkReconciliationAuthorizationResult result;
        lock (store.SyncRoot)
        {
            result = AuthorizeReconciliationReadUnsafe(currentTenantContext, sessionId, requestedReadScope, now);
        }

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Live-revalidates the caller's own Tenant authorization path (H93-02) --
    /// a structurally valid <see cref="TenantContext"/> is not by itself proof
    /// of current authority, since its factory methods are reachable from
    /// server-facing code with any caller-resolved reference -- and then
    /// live-proves the caller-supplied notification recipient reference is an
    /// active Global user with an active ordinary Membership in the caller's
    /// exact Tenant (M-8). A foreign-Tenant, suspended, revoked, pending or
    /// unknown recipient is always denied with a generic safe reason; no
    /// SupportGrant path can substitute for a genuine Tenant Membership on the
    /// recipient side.
    /// </summary>
    public ValueTask<NotificationRecipientAuthorizationResult> AuthorizeAsync(
        TenantContext currentTenantContext,
        NotificationRecipientReference recipient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentTenantContext);
        ArgumentNullException.ThrowIfNull(recipient);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(
                NotificationRecipientAuthorizationResult.Denied("recipient_authorization_cancelled"));
        }

        NotificationRecipientAuthorizationResult result;
        lock (store.SyncRoot)
        {
            result = AuthorizeNotificationRecipientUnsafe(currentTenantContext, recipient, Now);
        }

        return ValueTask.FromResult(result);
    }

    private NotificationRecipientAuthorizationResult AuthorizeNotificationRecipientUnsafe(
        TenantContext currentTenantContext,
        NotificationRecipientReference recipient,
        DateTimeOffset now)
    {
        if (!IsCurrentNotificationCallerAuthorityUnsafe(currentTenantContext, now))
        {
            return NotificationRecipientAuthorizationResult.Denied("caller_denied");
        }

        var recipientUserId = new UserId(recipient.UserId);
        if (!store.Users.TryGetValue(recipientUserId, out var user) || user.Status != GlobalUserStatus.Active)
        {
            return NotificationRecipientAuthorizationResult.Denied("recipient_denied");
        }

        var membership = store.Memberships.Values.FirstOrDefault(candidate =>
            candidate.UserId == recipientUserId && candidate.TenantId == currentTenantContext.TenantId);

        if (membership is null || membership.Status != MembershipStatus.Active)
        {
            return NotificationRecipientAuthorizationResult.Denied("recipient_denied");
        }

        return NotificationRecipientAuthorizationResult.Approved(
            new VerifiedNotificationRecipient(currentTenantContext.TenantId, recipient.UserId));
    }

    /// <summary>
    /// Live-revalidates that the caller's own <see cref="TenantContext"/> still
    /// reflects a currently valid Tenant authorization path (H93-02). A
    /// structurally valid context is not proof of current authority: its
    /// TenantId, MembershipReference, SupportGrantReference and ActorId are
    /// all matched against current server-owned records, exactly as durable-work
    /// dispatch and reconciliation-read revalidation already do. An ordinary
    /// context never falls back to a SupportGrant path and vice versa.
    /// </summary>
    private bool IsCurrentNotificationCallerAuthorityUnsafe(TenantContext currentTenantContext, DateTimeOffset now)
    {
        if (currentTenantContext.ActorId is not { } actorId || actorId == Guid.Empty)
        {
            return false;
        }

        if (!store.Users.TryGetValue(new UserId(actorId), out var actor) || actor.Status != GlobalUserStatus.Active)
        {
            return false;
        }

        switch (currentTenantContext.AuthorizationPath)
        {
            case TenantAuthorizationPath.OrdinaryMembership:
                if (currentTenantContext.SupportGrant.HasValue
                    || currentTenantContext.Membership is not { } membershipReference
                    || !store.Memberships.TryGetValue(new MembershipId(membershipReference.Value), out var membership)
                    || membership.UserId.Value != actorId
                    || membership.TenantId != currentTenantContext.TenantId
                    || membership.Status != MembershipStatus.Active)
                {
                    return false;
                }

                return true;

            case TenantAuthorizationPath.SupportGrant:
                if (currentTenantContext.Membership.HasValue
                    || currentTenantContext.SupportGrant is not { } supportGrantReference
                    || !store.SupportGrants.TryGetValue(new SupportGrantId(supportGrantReference.GrantId), out var supportGrant)
                    || supportGrant.CaseId.Value != supportGrantReference.CaseId
                    || supportGrant.UserId.Value != actorId
                    || supportGrant.TenantId != currentTenantContext.TenantId
                    || supportGrant.RevokedAt.HasValue
                    || supportGrant.ExpiresAt <= now
                    || !store.SupportCases.TryGetValue(supportGrant.CaseId, out var supportCase)
                    || !supportCase.IsActive)
                {
                    return false;
                }

                return true;

            default:
                return false;
        }
    }

    internal UserId CreateUser(string email, string password, bool mfaEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("A password is required.", nameof(password));
        }

        var normalizedEmail = NormalizeEmail(email);
        lock (store.SyncRoot)
        {
            if (store.UsersByEmail.ContainsKey(normalizedEmail))
            {
                throw new InvalidOperationException("The normalized email is already registered.");
            }

            var userId = new UserId(Guid.NewGuid());
            var user = new GlobalUser(userId, normalizedEmail, "pending-password-hash", mfaEnabled);
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            store.Users.Add(userId, user);
            store.UsersByEmail.Add(normalizedEmail, user);
            return userId;
        }
    }

    internal GlobalUserStatus GetUserStatus(UserId userId)
    {
        lock (store.SyncRoot)
        {
            return store.Users.TryGetValue(userId, out var user)
                ? user.Status
                : GlobalUserStatus.Offboarded;
        }
    }

    internal long GetUserVersion(UserId userId)
    {
        lock (store.SyncRoot)
        {
            return store.Users.TryGetValue(userId, out var user) ? user.Version : 0;
        }
    }

    internal MembershipId AddMembership(UserId userId, TenantId tenantId)
    {
        lock (store.SyncRoot)
        {
            RequireUserUnsafe(userId);
            var id = new MembershipId(Guid.NewGuid());
            store.Memberships.Add(id, new TenantMembership(id, userId, tenantId));
            store.RoleAssignments.Add(id, []);
            store.ScopeGrantsByMembership.Add(id, []);
            return id;
        }
    }

    internal MembershipStatus GetMembershipStatus(MembershipId membershipId)
    {
        lock (store.SyncRoot)
        {
            return store.Memberships.TryGetValue(membershipId, out var membership)
                ? membership.Status
                : MembershipStatus.Revoked;
        }
    }

    internal RoleId CreateRole(
        string name,
        TenantId? tenantId,
        bool platformOwned,
        IEnumerable<PermissionCode> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        lock (store.SyncRoot)
        {
            var permissionSet = permissions.ToHashSet();
            EnsureApprovedPermissionsUnsafe(permissionSet);
            if (!platformOwned && permissionSet.Contains(IdentityPermissions.AssignPlatformPermission))
            {
                throw new InvalidOperationException("Platform authority permissions are configuration-led and cannot be placed in a Tenant-owned role.");
            }

            var id = new RoleId(Guid.NewGuid());
            store.Roles.Add(id, new IdentityRole(id, name, tenantId, platformOwned, permissionSet));
            return id;
        }
    }

    internal AuthorityMutationResult AssignPlatformPermission(
        PlatformGovernanceContext governance,
        string actorCookieValue,
        UserId targetUserId,
        PermissionCode permission,
        string reason,
        long expectedVersion,
        string idempotencyKey)
    {
        const string operation = "platform.authority.assign";
        lock (store.SyncRoot)
        {
            if (!TryGetPlatformActorUnsafe(
                    governance,
                    actorCookieValue,
                    IdentityPermissions.AssignPlatformPermission,
                    operation,
                    reason,
                    out var actor,
                    out var actorSession))
            {
                return AuthorityDenied(governance.ActorId, operation, governance.CorrelationId, "platform_authorization_denied");
            }

            if (!store.Users.TryGetValue(targetUserId, out var target)
                || target.Status != GlobalUserStatus.Active
                || targetUserId == actor.Id)
            {
                return AuthorityDenied(actor.Id.Value, operation, governance.CorrelationId, "target_denied");
            }

            try
            {
                EnsureApprovedPermissionUnsafe(permission);
            }
            catch (InvalidOperationException)
            {
                return AuthorityDenied(actor.Id.Value, operation, governance.CorrelationId, "permission_denied");
            }

            if (!TryTakeIdempotencyUnsafe(idempotencyKey, operation, $"{targetUserId.Value:N}:{permission.Value}"))
            {
                return AuthorityDenied(actor.Id.Value, operation, governance.CorrelationId, "idempotency_conflict");
            }

            var assignments = store.PlatformPermissions.TryGetValue(targetUserId, out var existing)
                ? existing
                : store.PlatformPermissions[targetUserId] = [];
            var active = assignments.FirstOrDefault(item => item.Permission == permission && item.IsActive);
            if (active is not null)
            {
                TouchSessionUnsafe(actorSession);
                return AuthorityAllowed(actor.Id.Value, operation, "platform", governance.CorrelationId, actorSession.Id, target.Version, null, "already_applied");
            }

            if (assignments.Any(item => item.Permission == permission && !item.IsActive)
                || target.Version != expectedVersion)
            {
                return AuthorityDenied(actor.Id.Value, operation, governance.CorrelationId, "concurrency_or_nonresurrection_denied");
            }

            assignments.Add(new PlatformPermissionAssignment(targetUserId, permission, actor.Id, reason.Trim()));
            TouchSessionUnsafe(actorSession);
            return AuthorityAllowed(actor.Id.Value, operation, "platform", governance.CorrelationId, actorSession.Id, target.Version, targetUserId.Value, "authority_assigned");
        }
    }

    internal AuthorityMutationResult RevokePlatformPermission(
        PlatformGovernanceContext governance,
        string actorCookieValue,
        UserId targetUserId,
        PermissionCode permission,
        string reason,
        long expectedVersion,
        string idempotencyKey)
    {
        const string operation = "platform.authority.revoke";
        lock (store.SyncRoot)
        {
            if (!TryGetPlatformActorUnsafe(
                    governance,
                    actorCookieValue,
                    IdentityPermissions.AssignPlatformPermission,
                    operation,
                    reason,
                    out var actor,
                    out var actorSession)
                || actor.Id == targetUserId)
            {
                return AuthorityDenied(governance.ActorId, operation, governance.CorrelationId, "platform_authorization_denied");
            }

            if (!store.Users.TryGetValue(targetUserId, out var target)
                || !TryTakeIdempotencyUnsafe(idempotencyKey, operation, $"{targetUserId.Value:N}:{permission.Value}"))
            {
                return AuthorityDenied(actor.Id.Value, operation, governance.CorrelationId, "target_or_idempotency_denied");
            }

            var assignment = store.PlatformPermissions.TryGetValue(targetUserId, out var assignments)
                ? assignments.LastOrDefault(item => item.Permission == permission && item.IsActive)
                : null;
            if (assignment is null)
            {
                TouchSessionUnsafe(actorSession);
                return AuthorityAllowed(actor.Id.Value, operation, "platform", governance.CorrelationId, actorSession.Id, target.Version, targetUserId.Value, "already_applied");
            }

            if (target.Version != expectedVersion)
            {
                return AuthorityDenied(actor.Id.Value, operation, governance.CorrelationId, "concurrency_conflict");
            }

            assignment.IsActive = false;
            assignment.Version++;
            TouchSessionUnsafe(actorSession);
            return AuthorityAllowed(actor.Id.Value, operation, "platform", governance.CorrelationId, actorSession.Id, assignment.Version, targetUserId.Value, "authority_revoked");
        }
    }

    internal AuthorityMutationResult AssignRole(
        string actorCookieValue,
        TenantId tenantId,
        MembershipId targetMembershipId,
        RoleId roleId,
        OrganizationScope targetScope,
        string reason,
        long expectedVersion,
        string idempotencyKey,
        CorrelationId correlationId)
    {
        const string operation = "tenant.role.assign";
        lock (store.SyncRoot)
        {
            var decision = AuthorizeOrdinaryUnsafe(actorCookieValue, tenantId, IdentityPermissions.AssignRole, targetScope, correlationId, false, true, out var actorSession, out var actor);
            if (!decision.Allowed || string.IsNullOrWhiteSpace(reason))
            {
                return AuthorityDenied(actor?.Id.Value ?? Guid.Empty, operation, correlationId, "tenant_authorization_denied", tenantId);
            }

            if (!store.Memberships.TryGetValue(targetMembershipId, out var membership)
                || membership.TenantId != tenantId
                || membership.Status != MembershipStatus.Active
                || !store.Users.TryGetValue(membership.UserId, out var targetUser)
                || targetUser.Status != GlobalUserStatus.Active
                || !store.Roles.TryGetValue(roleId, out var role)
                || (role.TenantId.HasValue && role.TenantId.Value != tenantId)
                || (!role.PlatformOwned && !role.TenantId.HasValue)
                || membership.UserId == actor.Id)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "target_denied", tenantId);
            }

            if (!TryTakeIdempotencyUnsafe(idempotencyKey, operation, $"{targetMembershipId.Value:N}:{roleId.Value:N}"))
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "idempotency_conflict", tenantId);
            }

            var assignments = store.RoleAssignments[targetMembershipId];
            var active = assignments.FirstOrDefault(item => item.RoleId == roleId && item.IsActive);
            if (active is not null)
            {
                TouchSessionUnsafe(actorSession);
                return AuthorityAllowed(actor.Id.Value, operation, "ordinary", correlationId, actorSession.Id, membership.Version, null, "already_applied", tenantId);
            }

            if (assignments.Any(item => item.RoleId == roleId && !item.IsActive)
                || membership.Version != expectedVersion)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "concurrency_or_nonresurrection_denied", tenantId);
            }

            assignments.Add(new RoleAssignment(roleId, targetMembershipId, membership.UserId, tenantId, actor.Id));
            membership.Version++;
            TouchSessionUnsafe(actorSession);
            return AuthorityAllowed(actor.Id.Value, operation, "ordinary", correlationId, actorSession.Id, membership.Version, null, "authority_assigned", tenantId);
        }
    }

    internal void RevokeRole(MembershipId membershipId, RoleId roleId)
    {
        lock (store.SyncRoot)
        {
            if (!store.RoleAssignments.TryGetValue(membershipId, out var assignments))
            {
                return;
            }

            foreach (var assignment in assignments.Where(assignment => assignment.RoleId == roleId && assignment.IsActive))
            {
                assignment.IsActive = false;
                assignment.Version++;
            }
        }
    }

    internal AuthorityMutationResult AddScopeGrant(
        string actorCookieValue,
        TenantId tenantId,
        MembershipId targetMembershipId,
        OrganizationScope scope,
        string reason,
        long expectedVersion,
        string idempotencyKey,
        CorrelationId correlationId)
    {
        lock (store.SyncRoot)
        {
            const string operation = "tenant.scope.grant";
            var decision = AuthorizeOrdinaryUnsafe(actorCookieValue, tenantId, IdentityPermissions.AssignScopeGrant, scope, correlationId, false, true, out var actorSession, out var actor);
            if (!decision.Allowed || string.IsNullOrWhiteSpace(reason))
            {
                return AuthorityDenied(actor?.Id.Value ?? Guid.Empty, operation, correlationId, "tenant_authorization_denied", tenantId);
            }

            if (!store.Memberships.TryGetValue(targetMembershipId, out var membership)
                || membership.TenantId != tenantId
                || membership.Status != MembershipStatus.Active
                || !store.Users.TryGetValue(membership.UserId, out var targetUser)
                || targetUser.Status != GlobalUserStatus.Active
                || membership.UserId == actor.Id
                || scope.TenantId != tenantId)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "target_denied", tenantId);
            }

            if (!TryTakeIdempotencyUnsafe(idempotencyKey, operation, $"{targetMembershipId.Value:N}:{scope.Kind}:{scope.TargetId:N}"))
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "idempotency_conflict", tenantId);
            }

            var existing = store.ScopeGrantsByMembership[targetMembershipId]
                .Select(id => store.ScopeGrants[id])
                .SingleOrDefault(grant => grant.IsActive && grant.Scope == scope);
            if (existing is not null)
            {
                TouchSessionUnsafe(actorSession);
                return AuthorityAllowed(actor.Id.Value, operation, "ordinary", correlationId, actorSession.Id, membership.Version, existing.Id.Value, "already_applied", tenantId);
            }

            var revoked = store.ScopeGrantsByMembership[targetMembershipId]
                .Select(id => store.ScopeGrants[id])
                .Any(grant => !grant.IsActive && grant.Scope == scope);
            if (revoked)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "nonresurrection_denied", tenantId);
            }

            if (membership.Version != expectedVersion)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "concurrency_conflict", tenantId);
            }

            var id = new ScopeGrantId(Guid.NewGuid());
            store.ScopeGrants.Add(id, new AccessScopeGrant(id, targetMembershipId, membership.UserId, scope, actor.Id));
            store.ScopeGrantsByMembership[targetMembershipId].Add(id);
            membership.Version++;
            TouchSessionUnsafe(actorSession);
            return AuthorityAllowed(actor.Id.Value, operation, "ordinary", correlationId, actorSession.Id, membership.Version, id.Value, "authority_assigned", tenantId);
        }
    }

    internal void RevokeScope(ScopeGrantId grantId)
    {
        lock (store.SyncRoot)
        {
            if (store.ScopeGrants.TryGetValue(grantId, out var grant) && grant.IsActive)
            {
                grant.IsActive = false;
                grant.Version++;
            }
        }
    }

    internal void SetOrganizationParent(OrganizationScope child, OrganizationScope parent)
    {
        if (child.TenantId != parent.TenantId)
        {
            throw new ArgumentException("Organization parent and child must belong to the same Tenant.", nameof(parent));
        }

        if ((child.Kind, parent.Kind) is not (ScopeKind.Company, ScopeKind.Tenant)
            and not (ScopeKind.Branch, ScopeKind.Company)
            and not (ScopeKind.Warehouse, ScopeKind.Branch))
        {
            throw new ArgumentException("Organization scope hierarchy must be Tenant -> Company -> Branch -> Warehouse.", nameof(parent));
        }

        lock (store.SyncRoot)
        {
            store.ParentScopes[(child.TenantId, child.Kind, child.TargetId)] = parent;
        }
    }

    internal SupportCaseId AddSupportCase(TenantId tenantId)
    {
        lock (store.SyncRoot)
        {
            var id = new SupportCaseId(Guid.NewGuid());
            store.SupportCases.Add(id, new SupportCase(id, tenantId));
            return id;
        }
    }

    internal void CloseSupportCase(SupportCaseId caseId)
    {
        lock (store.SyncRoot)
        {
            if (store.SupportCases.TryGetValue(caseId, out var supportCase))
            {
                if (supportCase.IsActive)
                {
                    supportCase.IsActive = false;
                    supportCase.Version++;
                }
            }
        }
    }

    internal AuthorityMutationResult AddSupportGrant(
        string actorCookieValue,
        TenantId tenantId,
        UserId supportUserId,
        SupportCaseId caseId,
        string purpose,
        OrganizationScope scope,
        IEnumerable<PermissionCode> permissions,
        TimeSpan lifetime,
        string reason,
        long expectedVersion,
        string idempotencyKey,
        CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        lock (store.SyncRoot)
        {
            const string operation = "support.grant.approve";
            var decision = AuthorizeOrdinaryUnsafe(actorCookieValue, tenantId, IdentityPermissions.ApproveSupportGrant, scope, correlationId, false, true, out var actorSession, out var actor);
            if (!decision.Allowed || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(purpose))
            {
                return AuthorityDenied(actor?.Id.Value ?? Guid.Empty, operation, correlationId, "tenant_authorization_denied", tenantId);
            }

            if (!store.Users.TryGetValue(supportUserId, out var user)
                || !store.SupportCases.TryGetValue(caseId, out var supportCase)
                || supportCase.TenantId != tenantId
                || scope.TenantId != tenantId)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "target_denied", tenantId);
            }

            if (actor.Id == supportUserId || user.Status != GlobalUserStatus.Active || !supportCase.IsActive)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "approver_or_target_denied", tenantId);
            }

            var permissionSet = permissions.ToHashSet();
            try
            {
                EnsureApprovedPermissionsUnsafe(permissionSet);
            }
            catch (InvalidOperationException)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "permission_denied", tenantId);
            }

            if (permissionSet.Count == 0
                || permissionSet.Contains(IdentityPermissions.Export)
                || lifetime <= TimeSpan.Zero
                || lifetime > TimeSpan.FromHours(8)
                || !TryTakeIdempotencyUnsafe(idempotencyKey, operation, $"{caseId.Value:N}:{supportUserId.Value:N}"))
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "support_grant_policy_denied", tenantId);
            }

            var existing = store.SupportGrants.Values.FirstOrDefault(grant =>
                grant.CaseId == caseId
                && grant.UserId == supportUserId
                && grant.TenantApproverId == actor.Id
                && grant.RevokedAt is null);
            if (existing is not null)
            {
                TouchSessionUnsafe(actorSession);
                return AuthorityAllowed(actor.Id.Value, operation, "ordinary", correlationId, actorSession.Id, supportCase.Version, existing.Id.Value, "already_applied", tenantId, purpose, $"{scope.Kind}:{scope.TargetId}");
            }

            if (supportCase.Version != expectedVersion)
            {
                return AuthorityDenied(actor.Id.Value, operation, correlationId, "concurrency_conflict", tenantId);
            }

            var id = new SupportGrantId(Guid.NewGuid());
            store.SupportGrants.Add(
                id,
                new SupportGrant(id, caseId, supportUserId, actor.Id, tenantId, purpose, scope, permissionSet, Now.Add(lifetime)));
            supportCase.Version++;
            TouchSessionUnsafe(actorSession);
            return AuthorityAllowed(actor.Id.Value, operation, "ordinary", correlationId, actorSession.Id, supportCase.Version, id.Value, "authority_assigned", tenantId, purpose, $"{scope.Kind}:{scope.TargetId}");
        }
    }

    internal void RevokeSupportGrant(SupportGrantId grantId)
    {
        lock (store.SyncRoot)
        {
            if (store.SupportGrants.TryGetValue(grantId, out var grant) && grant.RevokedAt is null)
            {
                grant.RevokedAt = Now;
                grant.Version++;
            }
        }
    }

    internal AuthenticationResult Authenticate(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(password))
        {
            return FailedAuthentication();
        }

        lock (store.SyncRoot)
        {
            if (!store.UsersByEmail.TryGetValue(normalizedEmail, out var user))
            {
                return FailedAuthentication();
            }

            var now = Now;
            if (user.Status != GlobalUserStatus.Active || user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
            {
                return FailedAuthentication();
            }

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification is PasswordVerificationResult.Failed)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= options.MaxFailedAuthenticationAttempts)
                {
                    user.LockoutEnd = now.Add(options.LockoutDuration);
                }

                return FailedAuthentication();
            }

            if (verification is PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, password);
            }

            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            return CreateSessionUnsafe(user, now);
        }
    }

    internal AuthenticationResult AuthenticateDevelopment(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        lock (store.SyncRoot)
        {
            if (!store.UsersByEmail.TryGetValue(normalizedEmail, out var user)
                || user.Status != GlobalUserStatus.Active
                || user.LockoutEnd is { } lockoutEnd && lockoutEnd > Now)
            {
                return FailedAuthentication();
            }

            return CreateSessionUnsafe(user, Now);
        }
    }

    internal SessionValidation ValidateSession(string cookieValue)
    {
        lock (store.SyncRoot)
        {
            if (!TryGetValidSessionUnsafe(cookieValue, out var session, out var user))
            {
                return new SessionValidation(false, null, null, GenericDeniedCode);
            }

            TouchSessionUnsafe(session);
            return new SessionValidation(true, user.Id, session.Id, "session_valid");
        }
    }

    internal void RevokeSession(SessionId sessionId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A revocation reason is required.", nameof(reason));
        }

        lock (store.SyncRoot)
        {
            if (store.Sessions.TryGetValue(sessionId, out var session) && session.RevokedAt is null)
            {
                session.RevokedAt = Now;
                session.RevocationReason = reason.Trim();
                session.Version++;
            }
        }
    }

    internal bool AcceptMfaEvidence(string cookieValue, string opaqueEvidence)
    {
        lock (store.SyncRoot)
        {
            if (!TryGetValidSessionUnsafe(cookieValue, out var session, out var user)
                || !user.MfaEnabled
                || !assuranceEvidenceSource.TryValidateMfaEvidence(opaqueEvidence, user.Id, session.Id, Now, out var evidence)
                || evidence.SourceId != assuranceEvidenceSource.SourceId
                || evidence.Type != AuthenticationAssuranceType.Mfa
                || evidence.UserId != user.Id
                || evidence.SessionId != session.Id
                || evidence.IssuedAt > Now
                || evidence.ExpiresAt <= Now)
            {
                return false;
            }

            var evidenceHash = HashToken(opaqueEvidence);
            if (evidence.SingleUse && !store.ConsumedAssuranceEvidence.Add(evidenceHash))
            {
                return false;
            }

            session.MfaSatisfiedAt = Now;
            TouchSessionUnsafe(session);
            return true;
        }
    }

    internal bool AcceptFreshAuthenticationEvidence(string cookieValue, string operation, string opaqueEvidence)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        var normalizedOperation = operation.Trim();
        lock (store.SyncRoot)
        {
            if (!TryGetValidSessionUnsafe(cookieValue, out var session, out var user)
                || !assuranceEvidenceSource.TryValidateFreshAuthenticationEvidence(opaqueEvidence, user.Id, session.Id, normalizedOperation, Now, out var evidence)
                || evidence.SourceId != assuranceEvidenceSource.SourceId
                || evidence.Type != AuthenticationAssuranceType.FreshAuthentication
                || evidence.UserId != user.Id
                || evidence.SessionId != session.Id
                || !string.Equals(evidence.Operation, normalizedOperation, StringComparison.Ordinal)
                || evidence.IssuedAt > Now
                || evidence.ExpiresAt <= Now)
            {
                return false;
            }

            var evidenceHash = HashToken(opaqueEvidence);
            if (evidence.SingleUse && !store.ConsumedAssuranceEvidence.Add(evidenceHash))
            {
                return false;
            }

            session.FreshAuthenticationByOperation[normalizedOperation] = Now;
            TouchSessionUnsafe(session);
            return true;
        }
    }

    internal bool HasCurrentAuthenticationAssurance(string cookieValue, string operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        lock (store.SyncRoot)
        {
            return TryGetValidSessionUnsafe(cookieValue, out var session, out var user)
                && HasMfaAndFreshAuthenticationUnsafe(session, user, operation.Trim());
        }
    }

    /// <summary>
    /// Checks one exact, approved Platform permission and its operation-bound
    /// assurance. The caller cannot substitute an arbitrary active permission.
    /// </summary>
    internal bool AuthorizePlatformOperation(
        string cookieValue,
        UserId expectedUserId,
        PermissionCode permission,
        string operation,
        bool requireMfa,
        bool requireFreshAuthentication)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        lock (store.SyncRoot)
        {
            if (!TryGetValidSessionUnsafe(cookieValue, out var session, out var user)
                || user.Id != expectedUserId
                || !HasPlatformPermissionUnsafe(user.Id, permission))
            {
                return false;
            }

            if (requireMfa && (!user.MfaEnabled
                || session.MfaSatisfiedAt is not { } mfaAt
                || mfaAt.Add(options.FreshAuthenticationWindow) <= Now))
            {
                return false;
            }

            return !requireFreshAuthentication
                || session.FreshAuthenticationByOperation.TryGetValue(operation.Trim(), out var freshAt)
                    && freshAt.Add(options.FreshAuthenticationWindow) > Now;
        }
    }

    internal AuthorizationDecision AuthorizeOrdinary(
        string cookieValue,
        TenantId requestedTenant,
        PermissionCode permission,
        OrganizationScope requestedScope,
        CorrelationId correlationId,
        bool allowTenantScopeExpansion = false)
    {
        lock (store.SyncRoot)
        {
            if (requestedScope.TenantId != requestedTenant
                || !TryGetValidSessionUnsafe(cookieValue, out var session, out var user))
            {
                return Denied(userId: null, "ordinary", "tenant_or_session_denied", correlationId);
            }

            var membership = store.Memberships.Values.SingleOrDefault(candidate =>
                candidate.UserId == user.Id
                && candidate.TenantId == requestedTenant
                && candidate.Status == MembershipStatus.Active);
            if (membership is null)
            {
                return Denied(user.Id, "ordinary", "membership_denied", correlationId);
            }

            if (!HasPermissionUnsafe(membership, permission)
                || !HasScopeUnsafe(membership, requestedScope, allowTenantScopeExpansion))
            {
                return Denied(user.Id, "ordinary", "role_permission_or_scope_denied", correlationId);
            }

            if (HasActiveSupportGrantUnsafe(session, user.Id, requestedTenant, permission, requestedScope))
            {
                return Denied(user.Id, "ordinary", "authorization_path_conflict", correlationId);
            }

            session.MembershipReferences.Add(membership.Id);
            var context = TenantContext.ForOrdinaryMembership(
                requestedTenant,
                new MembershipReference(membership.Id.Value),
                new ScopeReference($"{requestedScope.Kind}:{requestedScope.TargetId}"),
                correlationId,
                user.Id.Value);
            TouchSessionUnsafe(session);
            return Allowed(user.Id, "ordinary", requestedTenant, session.Id, context, correlationId, "ordinary_authorized");
        }
    }

    internal AuthorizationDecision AuthorizeSupport(
        string cookieValue,
        TenantId requestedTenant,
        SupportGrantId grantId,
        PermissionCode permission,
        OrganizationScope requestedScope,
        string purpose,
        CorrelationId correlationId)
    {
        lock (store.SyncRoot)
        {
            if (requestedScope.TenantId != requestedTenant
                || string.IsNullOrWhiteSpace(purpose)
                || !TryGetValidSessionUnsafe(cookieValue, out var session, out var user))
            {
                return Denied(userId: null, "support", "support_denied", correlationId);
            }

            if (!store.SupportGrants.TryGetValue(grantId, out var grant)
                || grant.UserId != user.Id
                || grant.TenantId != requestedTenant
                || grant.RevokedAt.HasValue
                || grant.ExpiresAt <= Now
                || !store.SupportCases.TryGetValue(grant.CaseId, out var supportCase)
                || !supportCase.IsActive
                || !string.Equals(grant.Purpose, purpose.Trim(), StringComparison.Ordinal)
                || permission == IdentityPermissions.Export
                || !grant.Permissions.Contains(permission)
                || !ScopeContainsUnsafe(grant.Scope, requestedScope)
                || !HasMfaAndFreshAuthenticationUnsafe(session, user, $"support:{grantId.Value}:{permission.Value}"))
            {
                return Denied(user.Id, "support", "support_denied", correlationId);
            }

            if (HasActiveOrdinaryAuthorizationUnsafe(user.Id, requestedTenant, permission, requestedScope))
            {
                return Denied(user.Id, "support", "authorization_path_conflict", correlationId);
            }

            session.SupportGrantReferences.Add(grant.Id);
            var context = TenantContext.ForSupportGrant(
                requestedTenant,
                new SupportGrantReference(grant.Id.Value, grant.CaseId.Value),
                new ScopeReference($"{requestedScope.Kind}:{requestedScope.TargetId}"),
                correlationId,
                user.Id.Value);
            TouchSessionUnsafe(session);
            return Allowed(user.Id, "support", requestedTenant, session.Id, context, correlationId, "support_authorized", grant.Purpose, $"{requestedScope.Kind}:{requestedScope.TargetId}");
        }
    }

    internal LifecycleResult SuspendUser(
        PlatformGovernanceContext governance,
        string actorCookieValue,
        UserId targetUserId,
        string reason,
        long expectedVersion,
        string idempotencyKey)
    {
        return ChangeGlobalUserStatus(
            governance,
            actorCookieValue,
            targetUserId,
            IdentityPermissions.SuspendGlobalUser,
            "platform.user.suspend",
            GlobalUserStatus.Suspended,
            reason,
            expectedVersion,
            idempotencyKey);
    }

    internal LifecycleResult ReactivateUser(
        PlatformGovernanceContext governance,
        string actorCookieValue,
        UserId targetUserId,
        string reason,
        long expectedVersion,
        string idempotencyKey)
    {
        return ChangeGlobalUserStatus(
            governance,
            actorCookieValue,
            targetUserId,
            IdentityPermissions.ReactivateGlobalUser,
            "platform.user.reactivate",
            GlobalUserStatus.Active,
            reason,
            expectedVersion,
            idempotencyKey);
    }

    internal LifecycleResult OffboardUser(
        PlatformGovernanceContext governance,
        string actorCookieValue,
        UserId targetUserId,
        string reason,
        long expectedVersion,
        string idempotencyKey)
    {
        return ChangeGlobalUserStatus(
            governance,
            actorCookieValue,
            targetUserId,
            IdentityPermissions.OffboardGlobalUser,
            "platform.user.offboard",
            GlobalUserStatus.Offboarded,
            reason,
            expectedVersion,
            idempotencyKey);
    }

    internal LifecycleResult SuspendMembership(
        string actorCookieValue,
        TenantId tenantId,
        MembershipId targetMembershipId,
        string reason,
        long expectedVersion,
        string idempotencyKey,
        CorrelationId correlationId)
    {
        return ChangeMembershipStatus(
            actorCookieValue,
            tenantId,
            targetMembershipId,
            IdentityPermissions.SuspendMembership,
            "tenant.membership.suspend",
            MembershipStatus.Suspended,
            reason,
            expectedVersion,
            idempotencyKey,
            correlationId);
    }

    internal LifecycleResult ReactivateMembership(
        string actorCookieValue,
        TenantId tenantId,
        MembershipId targetMembershipId,
        string reason,
        long expectedVersion,
        string idempotencyKey,
        CorrelationId correlationId)
    {
        return ChangeMembershipStatus(
            actorCookieValue,
            tenantId,
            targetMembershipId,
            IdentityPermissions.ReactivateMembership,
            "tenant.membership.reactivate",
            MembershipStatus.Active,
            reason,
            expectedVersion,
            idempotencyKey,
            correlationId);
    }

    internal LifecycleResult RevokeMembership(
        string actorCookieValue,
        TenantId tenantId,
        MembershipId targetMembershipId,
        string reason,
        long expectedVersion,
        string idempotencyKey,
        CorrelationId correlationId)
    {
        return ChangeMembershipStatus(
            actorCookieValue,
            tenantId,
            targetMembershipId,
            IdentityPermissions.RevokeMembership,
            "tenant.membership.revoke",
            MembershipStatus.Revoked,
            reason,
            expectedVersion,
            idempotencyKey,
            correlationId);
    }

    internal InvitationHandle IssueInvitation(
        UserId userId,
        TenantId tenantId,
        MembershipId membershipId,
        string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        lock (store.SyncRoot)
        {
            var user = RequireUserUnsafe(userId);
            var membership = RequireMembershipUnsafe(membershipId);
            if (membership.UserId != userId
                || membership.TenantId != tenantId
                || membership.Status != MembershipStatus.PendingInvitation
                || user.NormalizedEmail != normalizedEmail)
            {
                throw new InvalidOperationException("Invitation identity and Tenant ownership must match.");
            }

            var token = CreateOpaqueToken();
            var invitationId = new InvitationId(Guid.NewGuid());
            store.InvitationsByTokenHash.Add(
                HashToken(token),
                new Invitation(invitationId, HashToken(token), normalizedEmail, userId, user.Version, tenantId, membershipId, membership.Version, Now.Add(options.InvitationLifetime)));
            return new InvitationHandle(invitationId, token);
        }
    }

    internal bool AcceptInvitation(string token, UserId userId, TenantId requestedTenant)
    {
        lock (store.SyncRoot)
        {
            if (!store.InvitationsByTokenHash.TryGetValue(HashToken(token), out var invitation)
                || invitation.Consumed
                || invitation.ExpiresAt <= Now
                || invitation.UserId != userId
                || invitation.TenantId != requestedTenant
                || !store.Users.TryGetValue(userId, out var user)
                || user.NormalizedEmail != invitation.NormalizedEmail
                || user.Version != invitation.UserVersionAtIssue
                || user.Status != GlobalUserStatus.Active
                || !store.Memberships.TryGetValue(invitation.MembershipId, out var membership)
                || membership.UserId != userId
                || membership.TenantId != requestedTenant
                || membership.Version != invitation.MembershipVersionAtIssue
                || membership.Status != MembershipStatus.PendingInvitation)
            {
                return false;
            }

            invitation.Consumed = true;
            membership.Status = MembershipStatus.Active;
            membership.Version++;
            return true;
        }
    }

    internal RecoveryHandle IssueRecovery(UserId userId)
    {
        lock (store.SyncRoot)
        {
            RequireUserUnsafe(userId);
            var token = CreateOpaqueToken();
            var id = new RecoveryId(Guid.NewGuid());
            var user = store.Users[userId];
            store.RecoveriesByTokenHash.Add(HashToken(token), new RecoveryArtifact(id, HashToken(token), userId, user.Version, Now.Add(options.RecoveryLifetime)));
            return new RecoveryHandle(id, token);
        }
    }

    internal bool UseRecovery(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        lock (store.SyncRoot)
        {
            if (!store.RecoveriesByTokenHash.TryGetValue(HashToken(token), out var recovery)
                || recovery.Consumed
                || recovery.ExpiresAt <= Now
                || !store.Users.TryGetValue(recovery.UserId, out var user)
                || user.Version != recovery.UserVersionAtIssue
                || user.Status == GlobalUserStatus.Offboarded)
            {
                return false;
            }

            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.Version++;
            recovery.Consumed = true;
            RevokeAllSessionsUnsafe(user.Id, "password-recovery");
            return true;
        }
    }

    internal IReadOnlyList<SafeSecurityEvidence> GetEvidence()
    {
        lock (store.SyncRoot)
        {
            return store.Evidence.ToArray();
        }
    }

    internal int CountMemberships(UserId userId, TenantId tenantId)
    {
        lock (store.SyncRoot)
        {
            return store.Memberships.Values.Count(membership => membership.UserId == userId && membership.TenantId == tenantId);
        }
    }

    internal int CountRoleAssignments(MembershipId membershipId)
    {
        lock (store.SyncRoot)
        {
            return store.RoleAssignments.TryGetValue(membershipId, out var assignments)
                ? assignments.Count(assignment => assignment.IsActive)
                : 0;
        }
    }

    internal int CountActiveScopeGrants(MembershipId membershipId)
    {
        lock (store.SyncRoot)
        {
            return store.ScopeGrantsByMembership.TryGetValue(membershipId, out var ids)
                ? ids.Count(id => store.ScopeGrants[id].IsActive)
                : 0;
        }
    }

    internal int CountActiveSessions(UserId userId)
    {
        lock (store.SyncRoot)
        {
            return store.Sessions.Values.Count(session =>
                session.UserId == userId && session.RevokedAt is null && session.AbsoluteExpiresAt > Now);
        }
    }

    private LifecycleResult ChangeGlobalUserStatus(
        PlatformGovernanceContext governance,
        string actorCookieValue,
        UserId targetUserId,
        PermissionCode permission,
        string operation,
        GlobalUserStatus desiredStatus,
        string reason,
        long expectedVersion,
        string idempotencyKey)
    {
        lock (store.SyncRoot)
        {
            var correlationId = governance.CorrelationId;
            if (!TryGetPlatformActorUnsafe(governance, actorCookieValue, permission, operation, reason, out var actor, out var actorSession))
            {
                return LifecycleDenied(governance.ActorId, operation, correlationId, "platform_authorization_denied");
            }

            if (!store.Users.TryGetValue(targetUserId, out var target))
            {
                return LifecycleDenied(actor.Id.Value, operation, correlationId, "target_denied");
            }

            if (!TryTakeIdempotencyUnsafe(idempotencyKey, operation, targetUserId.Value))
            {
                return LifecycleDenied(actor.Id.Value, operation, correlationId, "idempotency_conflict");
            }

            if (target.Status == desiredStatus)
            {
                TouchSessionUnsafe(actorSession);
                return LifecycleAllowed(actor.Id.Value, operation, target, actorSession.Id, correlationId, "already_applied");
            }

            if (target.Version != expectedVersion)
            {
                return LifecycleDenied(actor.Id.Value, operation, correlationId, "concurrency_conflict");
            }

            target.Status = desiredStatus;
            target.Version++;
            if (desiredStatus == GlobalUserStatus.Active)
            {
                target.AccessFailedCount = 0;
                target.LockoutEnd = null;
            }
            else
            {
                ContainGlobalUserAuthorityUnsafe(targetUserId, desiredStatus);
                RevokeAllSessionsUnsafe(targetUserId, operation);
            }

            TouchSessionUnsafe(actorSession);
            return LifecycleAllowed(actor.Id.Value, operation, target, actorSession.Id, correlationId, "lifecycle_changed");
        }
    }

    private LifecycleResult ChangeMembershipStatus(
        string actorCookieValue,
        TenantId tenantId,
        MembershipId targetMembershipId,
        PermissionCode permission,
        string operation,
        MembershipStatus desiredStatus,
        string reason,
        long expectedVersion,
        string idempotencyKey,
        CorrelationId correlationId)
    {
        lock (store.SyncRoot)
        {
            var actorDecision = AuthorizeOrdinaryUnsafe(
                actorCookieValue,
                tenantId,
                permission,
                OrganizationScope.ForTenant(tenantId),
                correlationId,
                false,
                false,
                out var actorSession,
                out var actor);
            if (!actorDecision.Allowed || string.IsNullOrWhiteSpace(reason))
            {
                return LifecycleDenied(actor?.Id.Value ?? Guid.Empty, operation, correlationId, "tenant_authorization_denied");
            }

            if (!store.Memberships.TryGetValue(targetMembershipId, out var target)
                || target.TenantId != tenantId)
            {
                return LifecycleDenied(actor.Id.Value, operation, correlationId, "target_denied");
            }

            if (!TryTakeIdempotencyUnsafe(idempotencyKey, operation, targetMembershipId.Value))
            {
                return LifecycleDenied(actor.Id.Value, operation, correlationId, "idempotency_conflict");
            }

            if (target.Status == desiredStatus)
            {
                TouchSessionUnsafe(actorSession);
                return LifecycleAllowed(actor.Id.Value, operation, target.Version, actorSession.Id, correlationId, "already_applied", tenantId);
            }

            if (target.Version != expectedVersion)
            {
                return LifecycleDenied(actor.Id.Value, operation, correlationId, "concurrency_conflict", tenantId);
            }

            target.Status = desiredStatus;
            target.Version++;
            if (desiredStatus is MembershipStatus.Suspended or MembershipStatus.Revoked)
            {
                foreach (var assignment in store.RoleAssignments[targetMembershipId].Where(assignment => assignment.IsActive))
                {
                    assignment.IsActive = false;
                    assignment.Version++;
                }

                foreach (var grantId in store.ScopeGrantsByMembership[targetMembershipId])
                {
                    if (store.ScopeGrants[grantId].IsActive)
                    {
                        store.ScopeGrants[grantId].IsActive = false;
                        store.ScopeGrants[grantId].Version++;
                    }
                }

                RevokeSessionsForMembershipUnsafe(targetMembershipId, operation);
            }

            TouchSessionUnsafe(actorSession);
            return LifecycleAllowed(actor.Id.Value, operation, target.Version, actorSession.Id, correlationId, "lifecycle_changed", tenantId);
        }
    }

    private bool TryGetPlatformActorUnsafe(
        PlatformGovernanceContext governance,
        string actorCookieValue,
        PermissionCode permission,
        string operation,
        string reason,
        out GlobalUser actor,
        out UserSession actorSession)
    {
        actor = null!;
        actorSession = null!;
        if (governance.Purpose != PlatformGovernancePurpose.SecurityEvidence
            || string.IsNullOrWhiteSpace(reason)
            || !TryGetValidSessionUnsafe(actorCookieValue, out actorSession, out actor)
            || actor.Id.Value != governance.ActorId
            || !HasPlatformPermissionUnsafe(actor.Id, permission)
            || !HasMfaAndFreshAuthenticationUnsafe(actorSession, actor, operation))
        {
            return false;
        }

        return true;
    }

    private AuthorizationDecision AuthorizeOrdinaryUnsafe(
        string cookieValue,
        TenantId requestedTenant,
        PermissionCode permission,
        OrganizationScope requestedScope,
        CorrelationId correlationId,
        bool renewActivity,
        bool allowTenantScopeExpansion,
        out UserSession session,
        out GlobalUser user)
    {
        session = null!;
        user = null!;
        if (requestedScope.TenantId != requestedTenant
            || !TryGetValidSessionUnsafe(cookieValue, out session, out user))
        {
            return Denied(null, "ordinary", "tenant_or_session_denied", correlationId);
        }

        var authenticatedUser = user;
        var membership = store.Memberships.Values.SingleOrDefault(candidate =>
            candidate.UserId == authenticatedUser.Id
            && candidate.TenantId == requestedTenant
            && candidate.Status == MembershipStatus.Active);
        if (membership is null || !HasPermissionUnsafe(membership, permission) || !HasScopeUnsafe(membership, requestedScope, allowTenantScopeExpansion))
        {
            return Denied(user.Id, "ordinary", "role_permission_or_scope_denied", correlationId);
        }

        if (HasActiveSupportGrantUnsafe(session, user.Id, requestedTenant, permission, requestedScope))
        {
            return Denied(user.Id, "ordinary", "authorization_path_conflict", correlationId);
        }

        session.MembershipReferences.Add(membership.Id);
        var context = TenantContext.ForOrdinaryMembership(
            requestedTenant,
            new MembershipReference(membership.Id.Value),
            new ScopeReference($"{requestedScope.Kind}:{requestedScope.TargetId}"),
            correlationId,
            user.Id.Value);
        if (renewActivity)
        {
            TouchSessionUnsafe(session);
        }

        return Allowed(user.Id, "ordinary", requestedTenant, session.Id, context, correlationId, "ordinary_authorized");
    }

    private bool TryGetValidSessionUnsafe(string cookieValue, out UserSession session, out GlobalUser user)
    {
        session = null!;
        user = null!;
        if (string.IsNullOrWhiteSpace(cookieValue)
            || !store.Sessions.Values.Any(candidate => candidate.CookieTokenHash == HashToken(cookieValue)))
        {
            return false;
        }

        session = store.Sessions.Values.Single(candidate => candidate.CookieTokenHash == HashToken(cookieValue));
        if (!store.Users.TryGetValue(session.UserId, out var resolvedUser))
        {
            return false;
        }

        user = resolvedUser;
        if (session.RevokedAt.HasValue
            || session.AbsoluteExpiresAt <= Now
            || session.LastActivityAt.Add(options.InactivityTimeout) <= Now
            || user.Status != GlobalUserStatus.Active
            || user.LockoutEnd is { } lockoutEnd && lockoutEnd > Now)
        {
            return false;
        }

        return true;
    }

    private bool TryGetValidSessionUnsafe(UserSession candidate, out GlobalUser user)
    {
        user = null!;
        if (!store.Users.TryGetValue(candidate.UserId, out var resolvedUser))
        {
            return false;
        }

        user = resolvedUser;

        var lockoutValid = user.LockoutEnd is null || user.LockoutEnd <= Now;
        return candidate.RevokedAt is null
            && candidate.AbsoluteExpiresAt > Now
            && candidate.LastActivityAt.Add(options.InactivityTimeout) > Now
            && user.Status == GlobalUserStatus.Active
            && lockoutValid;
    }

    private bool HasPermissionUnsafe(TenantMembership membership, PermissionCode permission)
    {
        return store.RoleAssignments.TryGetValue(membership.Id, out var assignments)
            && assignments.Any(assignment =>
                assignment.IsActive
                && store.Roles.TryGetValue(assignment.RoleId, out var role)
                && role.Permissions.Contains(permission));
    }

    private DurableWorkAuthorityValidationResult RevalidateDurableWorkUnsafe(
        DurableWorkItem workItem,
        TenantContext currentTenantContext,
        DateTimeOffset now)
    {
        var sameTenant = workItem.TenantId == currentTenantContext.TenantId
            && workItem.Initiator.TenantId == currentTenantContext.TenantId;
        if (!sameTenant)
        {
            return DurableWorkDenied(workItem, currentTenantContext, "tenant_mismatch", includeTenant: false);
        }

        if (!operationCatalogue.TryGet(workItem.Identity.OperationId, out var operation)
            || !operation.ExactlyMatches(workItem.Identity.Operation)
            || !workItem.Initiator.Operation.ExactlyMatches(workItem.Identity.Operation)
            || !operation.RequiresMandatorySecurityEvidence
            || !operation.AllowedAuthorizationPaths.Contains(currentTenantContext.AuthorizationPath)
            || (operation.ScopePolicy == DurableWorkScopePolicy.TenantWideOnly
                && (workItem.Scope.CompanyId.HasValue
                    || workItem.Scope.BranchId.HasValue
                    || workItem.Scope.WarehouseId.HasValue)))
        {
            return DurableWorkDenied(workItem, currentTenantContext, "operation_descriptor_denied", includeTenant: true);
        }

        if (workItem.Initiator.AuthorizationPath != currentTenantContext.AuthorizationPath
            || !workItem.Initiator.Operation.ExactlyMatches(operation))
        {
            return DurableWorkDenied(workItem, currentTenantContext, "authorization_path_conflict", includeTenant: true);
        }

        if (workItem.Initiator.ActorId is not { } actorId
            || currentTenantContext.ActorId != actorId
            || actorId == Guid.Empty
            || workItem.Initiator.SessionId is not { } sessionId
            || sessionId == Guid.Empty)
        {
            return DurableWorkDenied(workItem, currentTenantContext, "actor_or_session_denied", includeTenant: true);
        }

        if (!store.Users.TryGetValue(new UserId(actorId), out var user)
            || user.Status != GlobalUserStatus.Active
            || user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            return DurableWorkDenied(workItem, currentTenantContext, "user_inactive", includeTenant: true, actorId, sessionId);
        }

        if (!store.Sessions.TryGetValue(new SessionId(sessionId), out var session)
            || session.UserId != user.Id
            || !IsCurrentSessionUnsafe(session, now))
        {
            return DurableWorkDenied(workItem, currentTenantContext, "session_denied", includeTenant: true, actorId, sessionId);
        }

        if (!IdentityPermissions.TryResolve(operation.ExactPermissionCode, out var permission))
        {
            return DurableWorkDenied(workItem, currentTenantContext, "permission_denied", includeTenant: true, actorId, sessionId);
        }

        if (!TryResolveOrganizationScopeUnsafe(
                workItem.TenantId,
                new TenantWorkScopeRequest(workItem.Scope.CompanyId, workItem.Scope.BranchId, workItem.Scope.WarehouseId),
                out var organizationScope)
            || !IsCurrentScopeContainedUnsafe(currentTenantContext, organizationScope, now))
        {
            return DurableWorkDenied(workItem, currentTenantContext, "scope_ownership_denied", includeTenant: true, actorId, sessionId);
        }

        switch (currentTenantContext.AuthorizationPath)
        {
            case TenantAuthorizationPath.OrdinaryMembership:
                if (currentTenantContext.Membership != workItem.Initiator.Membership
                    || workItem.Initiator.SupportGrant.HasValue
                    || !currentTenantContext.Membership.HasValue
                    || !store.Memberships.TryGetValue(
                        new MembershipId(currentTenantContext.Membership.Value.Value),
                        out var membership)
                    || membership.UserId != user.Id
                    || membership.TenantId != workItem.TenantId
                    || membership.Status != MembershipStatus.Active
                    || !session.MembershipReferences.Contains(membership.Id)
                    || !HasPermissionUnsafe(membership, permission)
                    || !HasScopeUnsafe(membership, organizationScope))
                {
                    return DurableWorkDenied(workItem, currentTenantContext, "membership_authority_denied", includeTenant: true, actorId, sessionId);
                }

                break;

            case TenantAuthorizationPath.SupportGrant:
                if (currentTenantContext.Membership.HasValue
                    || !workItem.Initiator.SupportGrant.HasValue
                    || currentTenantContext.SupportGrant != workItem.Initiator.SupportGrant
                    || !currentTenantContext.SupportGrant.HasValue
                    || !store.SupportGrants.TryGetValue(
                        new SupportGrantId(currentTenantContext.SupportGrant.Value.GrantId),
                        out var supportGrant)
                    || supportGrant.CaseId.Value != currentTenantContext.SupportGrant.Value.CaseId
                    || supportGrant.UserId != user.Id
                    || supportGrant.TenantId != workItem.TenantId
                    || supportGrant.RevokedAt.HasValue
                    || supportGrant.ExpiresAt <= now
                    || !store.SupportCases.TryGetValue(supportGrant.CaseId, out var supportCase)
                    || !supportCase.IsActive
                    || permission == IdentityPermissions.Export
                    || !supportGrant.Permissions.Contains(permission)
                    || !session.SupportGrantReferences.Contains(supportGrant.Id)
                    || !ScopeContainsUnsafe(supportGrant.Scope, organizationScope))
                {
                    return DurableWorkDenied(workItem, currentTenantContext, "support_authority_denied", includeTenant: true, actorId, sessionId);
                }

                break;

            default:
                return DurableWorkDenied(workItem, currentTenantContext, "authorization_path_conflict", includeTenant: true, actorId, sessionId);
        }

        var exactScopeRequest = new TenantWorkScopeRequest(
            workItem.Scope.CompanyId,
            workItem.Scope.BranchId,
            workItem.Scope.WarehouseId);
        var exactScopeReference = new ScopeReference(
            $"{organizationScope.Kind}:{organizationScope.TargetId}");
        var executionTenantContext = currentTenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership when currentTenantContext.Membership is { } membershipReference =>
                TenantContext.ForOrdinaryMembership(
                    workItem.TenantId,
                    membershipReference,
                    exactScopeReference,
                    workItem.Identity.CorrelationId,
                    actorId),
            TenantAuthorizationPath.SupportGrant when currentTenantContext.SupportGrant is { } supportGrantReference =>
                TenantContext.ForSupportGrant(
                    workItem.TenantId,
                    supportGrantReference,
                    exactScopeReference,
                    workItem.Identity.CorrelationId,
                    actorId),
            _ => null
        };
        if (executionTenantContext is null)
        {
            return DurableWorkDenied(workItem, currentTenantContext, "authorization_path_conflict", includeTenant: true, actorId, sessionId);
        }

        var exactScope = TenantWorkScope.IssueFromVerifiedAuthority(
            executionTenantContext,
            exactScopeRequest);
        var authorization = new VerifiedDurableWorkAuthorization(
            workItem,
            workItem.Identity.WorkItemId,
            workItem.TenantId,
            workItem.Identity.CorrelationId,
            operation,
            executionTenantContext,
            exactScope,
            actorId,
            sessionId);
        return DurableWorkAuthorityValidationResult.Approved(authorization);
    }

    /// <summary>
    /// Reuses the same live actor/session/Membership/SupportGrant validity and
    /// organization-scope ownership logic as durable-work dispatch
    /// revalidation, gated additionally by the dedicated reconciliation-read
    /// permission. Ordinary Membership never falls back to a SupportGrant
    /// check or vice versa; a missing or malformed requested scope fails
    /// closed through <see cref="TryResolveOrganizationScopeUnsafe"/>.
    /// </summary>
    private DurableWorkReconciliationAuthorizationResult AuthorizeReconciliationReadUnsafe(
        TenantContext currentTenantContext,
        Guid sessionId,
        TenantWorkScopeRequest requestedReadScope,
        DateTimeOffset now)
    {
        if (currentTenantContext.ActorId is not { } actorId || actorId == Guid.Empty || sessionId == Guid.Empty)
        {
            return DurableWorkReconciliationAuthorizationResult.Denied("actor_or_session_denied");
        }

        if (!store.Users.TryGetValue(new UserId(actorId), out var user)
            || user.Status != GlobalUserStatus.Active
            || user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            return DurableWorkReconciliationAuthorizationResult.Denied("user_inactive");
        }

        if (!store.Sessions.TryGetValue(new SessionId(sessionId), out var session)
            || session.UserId != user.Id
            || !IsCurrentSessionUnsafe(session, now))
        {
            return DurableWorkReconciliationAuthorizationResult.Denied("session_denied");
        }

        // Reuses the identical live scope-containment logic used for
        // durable-work dispatch revalidation: an ordinary context's own
        // selected scope marker must resolve and itself contain the
        // requested read scope, and the underlying Membership scope grant
        // (or, on the support path, the exact case-bound SupportGrant --
        // never a client-visible marker) must independently contain it too.
        // A missing or malformed selected scope fails closed here.
        if (!TryResolveOrganizationScopeUnsafe(currentTenantContext.TenantId, requestedReadScope, out var organizationScope)
            || !IsCurrentScopeContainedUnsafe(currentTenantContext, organizationScope, now))
        {
            return DurableWorkReconciliationAuthorizationResult.Denied("scope_not_authorized");
        }

        switch (currentTenantContext.AuthorizationPath)
        {
            case TenantAuthorizationPath.OrdinaryMembership:
                if (!currentTenantContext.Membership.HasValue
                    || currentTenantContext.SupportGrant.HasValue
                    || !store.Memberships.TryGetValue(
                        new MembershipId(currentTenantContext.Membership.Value.Value),
                        out var membership)
                    || membership.UserId != user.Id
                    || membership.TenantId != currentTenantContext.TenantId
                    || membership.Status != MembershipStatus.Active
                    || !session.MembershipReferences.Contains(membership.Id)
                    || !HasPermissionUnsafe(membership, IdentityPermissions.DurableWorkReconciliationRead))
                {
                    return DurableWorkReconciliationAuthorizationResult.Denied("membership_authority_denied");
                }

                break;

            case TenantAuthorizationPath.SupportGrant:
                if (currentTenantContext.Membership.HasValue
                    || !currentTenantContext.SupportGrant.HasValue
                    || !store.SupportGrants.TryGetValue(
                        new SupportGrantId(currentTenantContext.SupportGrant.Value.GrantId),
                        out var supportGrant)
                    || supportGrant.CaseId.Value != currentTenantContext.SupportGrant.Value.CaseId
                    || supportGrant.UserId != user.Id
                    || supportGrant.TenantId != currentTenantContext.TenantId
                    || supportGrant.RevokedAt.HasValue
                    || supportGrant.ExpiresAt <= now
                    || !store.SupportCases.TryGetValue(supportGrant.CaseId, out var supportCase)
                    || !supportCase.IsActive
                    || !supportGrant.Permissions.Contains(IdentityPermissions.DurableWorkReconciliationRead)
                    || !session.SupportGrantReferences.Contains(supportGrant.Id))
                {
                    return DurableWorkReconciliationAuthorizationResult.Denied("support_authority_denied");
                }

                break;

            default:
                return DurableWorkReconciliationAuthorizationResult.Denied("authorization_path_conflict");
        }

        var exactScopeReference = new ScopeReference($"{organizationScope.Kind}:{organizationScope.TargetId}");
        var executionTenantContext = currentTenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership when currentTenantContext.Membership is { } membershipReference =>
                TenantContext.ForOrdinaryMembership(
                    currentTenantContext.TenantId,
                    membershipReference,
                    exactScopeReference,
                    currentTenantContext.CorrelationId,
                    actorId),
            TenantAuthorizationPath.SupportGrant when currentTenantContext.SupportGrant is { } supportGrantReference =>
                TenantContext.ForSupportGrant(
                    currentTenantContext.TenantId,
                    supportGrantReference,
                    exactScopeReference,
                    currentTenantContext.CorrelationId,
                    actorId),
            _ => null
        };
        if (executionTenantContext is null || currentTenantContext.CorrelationId is not { } correlationId)
        {
            return DurableWorkReconciliationAuthorizationResult.Denied("authorization_path_conflict");
        }

        var verifiedScope = TenantWorkScope.IssueFromVerifiedAuthority(executionTenantContext, requestedReadScope);
        var authorization = new VerifiedDurableWorkReconciliationAuthorization(
            executionTenantContext,
            verifiedScope,
            actorId,
            sessionId,
            correlationId);
        return DurableWorkReconciliationAuthorizationResult.Approved(authorization);
    }

    private DurableWorkAuthorityValidationResult DurableWorkDenied(
        DurableWorkItem workItem,
        TenantContext currentTenantContext,
        string reason,
        bool includeTenant,
        Guid? actorId = null,
        Guid? sessionId = null)
    {
        var evidence = new SafeSecurityEvidence(
            actorId ?? currentTenantContext.ActorId ?? Guid.Empty,
            "durable-work.dispatch",
            currentTenantContext.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership
                ? "ordinary"
                : "support",
            includeTenant ? currentTenantContext.TenantId.Value : null,
            "denied",
            reason,
            workItem.Identity.CorrelationId.Value,
            sessionId,
            null,
            null);
        store.Evidence.Add(evidence);
        return DurableWorkAuthorityValidationResult.Denied(reason);
    }

    private bool IsCurrentScopeContainedUnsafe(
        TenantContext tenantContext,
        OrganizationScope requestedScope,
        DateTimeOffset? authorityNow = null)
    {
        if (tenantContext.TenantId != requestedScope.TenantId)
        {
            return false;
        }

        if (tenantContext.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership
            && (!TryResolveContextScopeUnsafe(tenantContext, out var selectedScope)
                || !ScopeContainsUnsafe(selectedScope, requestedScope)))
        {
            return false;
        }

        // Support contexts may carry a display marker or stale client-visible
        // scope. The current case-bound SupportGrant record is authoritative;
        // never use the context marker as a support authorization fallback.
        if (tenantContext.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership)
        {
            if (!tenantContext.Membership.HasValue
                || !store.Memberships.TryGetValue(
                    new MembershipId(tenantContext.Membership.Value.Value),
                    out var membership)
                || !tenantContext.ActorId.HasValue
                || membership.UserId.Value != tenantContext.ActorId.Value
                || membership.TenantId != tenantContext.TenantId
                || membership.Status != MembershipStatus.Active)
            {
                return false;
            }

            return HasScopeUnsafe(membership, requestedScope);
        }

        if (tenantContext.AuthorizationPath == TenantAuthorizationPath.SupportGrant
            && tenantContext.SupportGrant is { } supportReference
            && store.SupportGrants.TryGetValue(new SupportGrantId(supportReference.GrantId), out var supportGrant)
            && supportGrant.CaseId.Value == supportReference.CaseId
            && supportGrant.UserId.Value == tenantContext.ActorId
            && supportGrant.TenantId == tenantContext.TenantId
            && supportGrant.RevokedAt is null
            && supportGrant.ExpiresAt > (authorityNow ?? Now)
            && store.SupportCases.TryGetValue(supportGrant.CaseId, out var supportCase)
            && supportCase.IsActive)
        {
            return ScopeContainsUnsafe(supportGrant.Scope, requestedScope);
        }

        return false;
    }

    private bool TryResolveOrganizationScopeUnsafe(
        TenantId tenantId,
        TenantWorkScopeRequest requestedScope,
        out OrganizationScope organizationScope)
    {
        organizationScope = requestedScope.WarehouseId is { } warehouseId
            ? OrganizationScope.ForWarehouse(tenantId, warehouseId)
            : requestedScope.BranchId is { } branchId
                ? OrganizationScope.ForBranch(tenantId, branchId)
                : requestedScope.CompanyId is { } companyId
                    ? OrganizationScope.ForCompany(tenantId, companyId)
                    : OrganizationScope.ForTenant(tenantId);

        if (!IsOrganizationOwnershipValidUnsafe(organizationScope, requestedScope))
        {
            return false;
        }

        return true;
    }

    private bool IsOrganizationOwnershipValidUnsafe(
        OrganizationScope requestedScope,
        TenantWorkScopeRequest requestedTarget)
    {
        if (requestedScope.Kind == ScopeKind.Tenant)
        {
            return requestedScope.TargetId == requestedScope.TenantId.Value;
        }

        var current = requestedScope;
        for (var depth = 0; depth < 4; depth++)
        {
            if (!store.ParentScopes.TryGetValue(
                    (current.TenantId, current.Kind, current.TargetId),
                    out var parent)
                || parent is null)
            {
                return false;
            }

            var expectedParentKind = current.Kind switch
            {
                ScopeKind.Company => ScopeKind.Tenant,
                ScopeKind.Branch => ScopeKind.Company,
                ScopeKind.Warehouse => ScopeKind.Branch,
                _ => (ScopeKind?)null
            };
            if (!expectedParentKind.HasValue
                || parent.Value.TenantId != requestedScope.TenantId
                || parent.Value.Kind != expectedParentKind.Value)
            {
                return false;
            }

            if (current.Kind == ScopeKind.Warehouse
                && requestedTarget.BranchId != parent.Value.TargetId)
            {
                return false;
            }

            if (current.Kind == ScopeKind.Branch
                && requestedTarget.CompanyId != parent.Value.TargetId)
            {
                return false;
            }

            current = parent.Value;
            if (current.Kind == ScopeKind.Tenant)
            {
                return current.TargetId == requestedScope.TenantId.Value;
            }
        }

        return false;
    }

    private bool TryResolveContextScopeUnsafe(
        TenantContext tenantContext,
        out OrganizationScope selectedScope)
    {
        selectedScope = default;
        if (!tenantContext.Scope.HasValue)
        {
            return false;
        }

        // Ordinary execution accepts only the server's canonical Kind:GUID
        // representation. Missing, marker, aliased or malformed values deny.
        var value = tenantContext.Scope.Value.Value;
        var separatorIndex = value.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1
            || separatorIndex != value.LastIndexOf(':'))
        {
            return false;
        }

        var kind = value[..separatorIndex] switch
        {
            "Tenant" => ScopeKind.Tenant,
            "Company" => ScopeKind.Company,
            "Branch" => ScopeKind.Branch,
            "Warehouse" => ScopeKind.Warehouse,
            _ => (ScopeKind?)null
        };
        if (!kind.HasValue
            || !Guid.TryParseExact(value[(separatorIndex + 1)..], "D", out var targetId)
            || targetId == Guid.Empty
            || !string.Equals(value, $"{value[..separatorIndex]}:{targetId}", StringComparison.Ordinal))
        {
            return false;
        }

        if (kind == ScopeKind.Tenant && targetId != tenantContext.TenantId.Value)
        {
            return false;
        }

        selectedScope = new OrganizationScope(tenantContext.TenantId, kind.Value, targetId);
        return true;
    }

    private bool IsCurrentSessionUnsafe(UserSession session, DateTimeOffset now)
    {
        return session.IssuedAt <= now
            && session.RevokedAt is null
            && session.AbsoluteExpiresAt > now
            && session.LastActivityAt.Add(options.InactivityTimeout) > now;
    }

    private bool HasScopeUnsafe(TenantMembership membership, OrganizationScope requestedScope, bool allowTenantScopeExpansion = false)
    {
        return store.ScopeGrantsByMembership.TryGetValue(membership.Id, out var ids)
            && ids.Select(id => store.ScopeGrants[id]).Any(grant => grant.IsActive && ScopeContainsUnsafe(grant.Scope, requestedScope, allowTenantScopeExpansion));
    }

    internal bool HasOrdinaryScope(
        TenantMembership membership,
        OrganizationScope requestedScope,
        bool allowTenantScopeExpansion = false) =>
        membership.Status == MembershipStatus.Active
        && membership.TenantId == requestedScope.TenantId
        && HasScopeUnsafe(membership, requestedScope, allowTenantScopeExpansion);

    private bool HasActiveOrdinaryAuthorizationUnsafe(UserId userId, TenantId tenantId, PermissionCode permission, OrganizationScope requestedScope)
    {
        var membership = store.Memberships.Values.SingleOrDefault(candidate =>
            candidate.UserId == userId && candidate.TenantId == tenantId && candidate.Status == MembershipStatus.Active);
        return membership is not null && HasPermissionUnsafe(membership, permission) && HasScopeUnsafe(membership, requestedScope);
    }

    private bool HasActiveSupportGrantUnsafe(
        UserSession session,
        UserId userId,
        TenantId tenantId,
        PermissionCode permission,
        OrganizationScope requestedScope)
    {
        var now = Now;
        return store.SupportGrants.Values.Any(grant =>
            grant.UserId == userId
            && grant.TenantId == tenantId
            && grant.RevokedAt is null
            && grant.ExpiresAt > now
            && store.SupportCases.TryGetValue(grant.CaseId, out var supportCase)
            && supportCase.IsActive
            && grant.Permissions.Contains(permission)
            && ScopeContainsUnsafe(grant.Scope, requestedScope)
            && HasMfaAndFreshAuthenticationUnsafe(session, store.Users[userId], $"support:{grant.Id.Value}:{permission.Value}"));
    }

    private bool ScopeContainsUnsafe(OrganizationScope grantScope, OrganizationScope requestedScope, bool allowTenantScopeExpansion = false)
    {
        if (grantScope.TenantId != requestedScope.TenantId)
        {
            return false;
        }

        if (allowTenantScopeExpansion && grantScope.Kind == ScopeKind.Tenant)
        {
            return true;
        }

        var current = requestedScope;
        for (var depth = 0; depth <= 4; depth++)
        {
            if (current == grantScope)
            {
                return true;
            }

            if (!store.ParentScopes.TryGetValue((current.TenantId, current.Kind, current.TargetId), out var parent)
                || parent is null)
            {
                return false;
            }

            current = parent.Value;
        }

        return false;
    }

    private bool HasPlatformPermissionUnsafe(UserId userId, PermissionCode permission) =>
        store.PlatformPermissions.TryGetValue(userId, out var assignments)
        && assignments.Any(assignment => assignment.Permission == permission && assignment.IsActive);

    private bool HasMfaAndFreshAuthenticationUnsafe(UserSession session, GlobalUser user, string operation)
    {
        return user.MfaEnabled
            && session.MfaSatisfiedAt is { } mfaAt
            && mfaAt.Add(options.FreshAuthenticationWindow) > Now
            && session.FreshAuthenticationByOperation.TryGetValue(operation, out var freshAt)
            && freshAt.Add(options.FreshAuthenticationWindow) > Now;
    }

    private void RevokeAllSessionsUnsafe(UserId userId, string reason)
    {
        foreach (var session in store.Sessions.Values.Where(session => session.UserId == userId && session.RevokedAt is null))
        {
            session.RevokedAt = Now;
            session.RevocationReason = reason;
            session.Version++;
        }
    }

    private void TouchSessionUnsafe(UserSession session)
    {
        // Inactivity renewal is deliberately independent from absolute expiry.
        // This method is called only after a valid session and a successful
        // protected operation have been established.
        session.LastActivityAt = Now;
        session.Version++;
    }

    private void RevokeSessionsForMembershipUnsafe(MembershipId membershipId, string reason)
    {
        foreach (var session in store.Sessions.Values.Where(session => session.MembershipReferences.Contains(membershipId) && session.RevokedAt is null))
        {
            session.MembershipReferences.Remove(membershipId);
            if (session.MembershipReferences.Count == 0 && session.SupportGrantReferences.Count == 0)
            {
                session.RevokedAt = Now;
                session.RevocationReason = reason;
            }

            session.Version++;
        }
    }

    private void ContainGlobalUserAuthorityUnsafe(UserId userId, GlobalUserStatus desiredStatus)
    {
        foreach (var membership in store.Memberships.Values.Where(membership => membership.UserId == userId && membership.Status == MembershipStatus.Active))
        {
            membership.Status = desiredStatus == GlobalUserStatus.Offboarded
                ? MembershipStatus.Revoked
                : MembershipStatus.Suspended;
            membership.Version++;

            foreach (var assignment in store.RoleAssignments[membership.Id].Where(assignment => assignment.IsActive))
            {
                assignment.IsActive = false;
                assignment.Version++;
            }

            foreach (var grantId in store.ScopeGrantsByMembership[membership.Id])
            {
                if (store.ScopeGrants[grantId].IsActive)
                {
                    store.ScopeGrants[grantId].IsActive = false;
                    store.ScopeGrants[grantId].Version++;
                }
            }
        }

        foreach (var supportGrant in store.SupportGrants.Values.Where(grant => grant.UserId == userId && grant.RevokedAt is null))
        {
            supportGrant.RevokedAt = Now;
            supportGrant.Version++;
        }
    }

    private bool TryTakeIdempotencyUnsafe(string idempotencyKey, string operation, Guid targetId)
        => TryTakeIdempotencyUnsafe(idempotencyKey, operation, targetId.ToString("N"));

    private bool TryTakeIdempotencyUnsafe(string idempotencyKey, string operation, string targetKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        var key = idempotencyKey.Trim();
        var value = $"{operation}:{targetKey}";
        if (store.LifecycleIdempotency.TryGetValue(key, out var existing))
        {
            return existing == value;
        }

        store.LifecycleIdempotency.Add(key, value);
        return true;
    }

    private static AuthenticationResult FailedAuthentication() =>
        new(false, null, null, GenericAuthenticationCode);

    private AuthenticationResult CreateSessionUnsafe(GlobalUser user, DateTimeOffset now)
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var cookieValue = CreateOpaqueToken();
        var session = new UserSession(
            sessionId,
            user.Id,
            HashToken(cookieValue),
            now,
            now.Add(options.AbsoluteSessionLifetime));
        store.Sessions.Add(sessionId, session);
        return new AuthenticationResult(true, sessionId, cookieValue, "authenticated");
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email must not be empty.", nameof(email));
        }

        return email.Trim().ToUpperInvariant();
    }

    private static string CreateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private GlobalUser RequireUserUnsafe(UserId userId) =>
        store.Users.TryGetValue(userId, out var user)
            ? user
            : throw new InvalidOperationException("User does not exist.");

    private TenantMembership RequireMembershipUnsafe(MembershipId membershipId) =>
        store.Memberships.TryGetValue(membershipId, out var membership)
            ? membership
            : throw new InvalidOperationException("Membership does not exist.");

    private IdentityRole RequireRoleUnsafe(RoleId roleId) =>
        store.Roles.TryGetValue(roleId, out var role)
            ? role
            : throw new InvalidOperationException("Role does not exist.");

    private SupportCase RequireSupportCaseUnsafe(SupportCaseId caseId) =>
        store.SupportCases.TryGetValue(caseId, out var supportCase)
            ? supportCase
            : throw new InvalidOperationException("Support case does not exist.");

    private void EnsureApprovedPermissionsUnsafe(IEnumerable<PermissionCode> permissions)
    {
        foreach (var permission in permissions)
        {
            EnsureApprovedPermissionUnsafe(permission);
        }
    }

    private void EnsureApprovedPermissionUnsafe(PermissionCode permission)
    {
        if (!store.ApprovedPermissions.Contains(permission))
        {
            throw new InvalidOperationException("Permission is not present in the approved catalogue.");
        }
    }

    private AuthorizationDecision Denied(UserId? userId, string authorizationPath, string reasonCategory, CorrelationId correlationId)
    {
        var evidence = new SafeSecurityEvidence(
            userId?.Value ?? Guid.Empty,
            "authorize",
            authorizationPath,
            null,
            "denied",
            reasonCategory,
            correlationId.Value,
            null,
            null,
            null);
        store.Evidence.Add(evidence);
        return new AuthorizationDecision(false, GenericDeniedCode, reasonCategory, null, evidence);
    }

    private AuthorizationDecision Allowed(
        UserId actorId,
        string authorizationPath,
        TenantId tenantId,
        SessionId sessionId,
        TenantContext context,
        CorrelationId correlationId,
        string reasonCategory,
        string? purpose = null,
        string? scope = null)
    {
        var evidence = new SafeSecurityEvidence(
            actorId.Value,
            "authorize",
            authorizationPath,
            tenantId.Value,
            "allowed",
            reasonCategory,
            correlationId.Value,
            sessionId.Value,
            purpose,
            scope);
        store.Evidence.Add(evidence);

        return new AuthorizationDecision(true, "authorized", reasonCategory, context, evidence);
    }

    private LifecycleResult LifecycleDenied(Guid actorId, string operation, CorrelationId correlationId, string reasonCategory, TenantId? tenantId = null)
    {
        var evidence = new SafeSecurityEvidence(actorId, operation, "platform", tenantId?.Value, "denied", reasonCategory, correlationId.Value, null, null, null);
        store.Evidence.Add(evidence);
        return new LifecycleResult(false, GenericDeniedCode, 0, evidence);
    }

    private LifecycleResult LifecycleAllowed(Guid actorId, string operation, GlobalUser target, SessionId sessionId, CorrelationId correlationId, string reasonCategory)
    {
        var evidence = new SafeSecurityEvidence(actorId, operation, "platform", null, "allowed", reasonCategory, correlationId.Value, sessionId.Value, null, null);
        store.Evidence.Add(evidence);
        return new LifecycleResult(true, "lifecycle_applied", target.Version, evidence);
    }

    private LifecycleResult LifecycleAllowed(Guid actorId, string operation, long version, SessionId sessionId, CorrelationId correlationId, string reasonCategory, TenantId tenantId)
    {
        var evidence = new SafeSecurityEvidence(actorId, operation, "ordinary", tenantId.Value, "allowed", reasonCategory, correlationId.Value, sessionId.Value, null, null);
        store.Evidence.Add(evidence);
        return new LifecycleResult(true, "lifecycle_applied", version, evidence);
    }

    private AuthorityMutationResult AuthorityDenied(
        Guid actorId,
        string operation,
        CorrelationId correlationId,
        string reasonCategory,
        TenantId? tenantId = null)
    {
        var evidence = new SafeSecurityEvidence(
            actorId,
            operation,
            tenantId.HasValue ? "ordinary" : "platform",
            tenantId?.Value,
            "denied",
            reasonCategory,
            correlationId.Value,
            null,
            null,
            null);
        store.Evidence.Add(evidence);
        return new AuthorityMutationResult(false, GenericDeniedCode, 0, null, evidence);
    }

    private AuthorityMutationResult AuthorityAllowed(
        Guid actorId,
        string operation,
        string authorizationPath,
        CorrelationId correlationId,
        SessionId sessionId,
        long version,
        Guid? resourceId,
        string reasonCategory,
        TenantId? tenantId = null,
        string? purpose = null,
        string? scope = null)
    {
        var evidence = new SafeSecurityEvidence(
            actorId,
            operation,
            authorizationPath,
            tenantId?.Value,
            "allowed",
            reasonCategory,
            correlationId.Value,
            sessionId.Value,
            purpose,
            scope);
        store.Evidence.Add(evidence);
        return new AuthorityMutationResult(true, "authority_applied", version, resourceId, evidence);
    }
}
