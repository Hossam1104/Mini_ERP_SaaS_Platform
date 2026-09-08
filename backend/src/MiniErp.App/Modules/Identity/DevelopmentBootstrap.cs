#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.Modules.Identity;

/// <summary>
/// Development-only bootstrap service to seed local development runtime user, tenant, and permissions.
/// Disabled by default; impossible to enable in non-Development environments.
/// </summary>
public static class DevelopmentBootstrap
{
    /// <summary>Generic Development Tenant identity (MiniERP Development).</summary>
    public static readonly TenantId DevTenantId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    /// <summary>Enables development bootstrap seeding if configured and running in Development environment.</summary>
    public static IHost SeedDevelopmentBootstrap(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var environment = host.Services.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return host;
        }

        var config = host.Services.GetRequiredService<IConfiguration>();
        var enabled = string.Equals(config["MESP_DEV_BOOTSTRAP_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            return host;
        }

        var login = config["MESP_DEV_ADMIN_LOGIN"] ?? "admin@minierp.local";
        var password = config["MESP_DEV_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(password))
        {
            if (!DevelopmentAuthBypassPolicy.IsEnabled(config))
            {
                throw new InvalidOperationException(
                    "Development bootstrap is enabled via MESP_DEV_BOOTSTRAP_ENABLED=true, but MESP_DEV_ADMIN_PASSWORD is not set. " +
                    "Please supply a local development password via environment variable MESP_DEV_ADMIN_PASSWORD, " +
                    "or explicitly enable the Development-only auth bypass.");
            }

            // The bypass authenticates the configured server actor without
            // accepting a client credential. A random in-memory password keeps
            // the normal bootstrap invariant without creating a local secret.
            password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        }

        using var scope = host.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityAuthorizationService>();

        lock (identity.Store.SyncRoot)
        {
            SeedDevelopmentUserAndPermissions(identity, login.Trim(), password, config);
        }

        return host;
    }

    private static void SeedDevelopmentUserAndPermissions(
        IdentityAuthorizationService identity,
        string login,
        string password,
        IConfiguration config)
    {
        var store = identity.Store;
        var normalizedAdminEmail = login.Trim().ToLowerInvariant();

        // 1. Idempotently get or create System Approver User
        const string approverEmail = "system.approver@minierp.local";
        if (!store.UsersByEmail.TryGetValue(approverEmail, out var approverUser))
        {
            var approverId = identity.CreateUser(approverEmail, Guid.NewGuid().ToString("N") + "A1!", mfaEnabled: false);
            approverUser = store.Users[approverId];
        }

        // 2. Idempotently get or create Admin Global User
        if (!store.UsersByEmail.TryGetValue(normalizedAdminEmail, out var adminUser))
        {
            var adminId = identity.CreateUser(login, password, mfaEnabled: false);
            adminUser = store.Users[adminId];
        }

        // 3. Idempotently get or create Tenant Membership
        var membership = store.Memberships.Values.FirstOrDefault(m =>
            m.UserId == adminUser.Id && m.TenantId == DevTenantId && m.Status == MembershipStatus.Active);

        if (membership is null)
        {
            var membershipId = identity.AddMembership(adminUser.Id, DevTenantId);
            membership = store.Memberships[membershipId];
        }

        // 4. Idempotently get or create Development Admin Role
        var role = store.Roles.Values.FirstOrDefault(r =>
            r.TenantId == DevTenantId && string.Equals(r.Name, "Development Administrator", StringComparison.Ordinal));

        if (role is null)
        {
            var permissions = new HashSet<PermissionCode>
            {
                IdentityPermissions.ContextRead,
                IdentityPermissions.TargetRead,
                IdentityPermissions.Read,
                IdentityPermissions.Export
            };

            foreach (var op in FoundationOperationCatalog.PublicOperations)
            {
                if (!string.IsNullOrWhiteSpace(op.ExactPermissionCode)
                    && IdentityPermissions.TryResolve(op.ExactPermissionCode, out var perm))
                {
                    permissions.Add(perm);
                }
            }

            var roleId = identity.CreateRole("Development Administrator", DevTenantId, platformOwned: false, permissions);
            role = store.Roles[roleId];
        }

        // 5. Idempotently assign Role to Membership with separate approver
        if (!store.RoleAssignments.TryGetValue(membership.Id, out var assignments))
        {
            assignments = [];
            store.RoleAssignments[membership.Id] = assignments;
        }

        if (!assignments.Any(a => a.RoleId == role.Id && a.IsActive))
        {
            assignments.Add(new RoleAssignment(role.Id, membership.Id, adminUser.Id, DevTenantId, approverUser.Id));
        }

        // 6. Idempotently add Tenant Scope Grant with separate approver
        if (!store.ScopeGrantsByMembership.TryGetValue(membership.Id, out var scopeGrantIds))
        {
            scopeGrantIds = [];
            store.ScopeGrantsByMembership[membership.Id] = scopeGrantIds;
        }

        var hasTenantScope = scopeGrantIds.Any(id =>
            store.ScopeGrants.TryGetValue(id, out var grant)
            && grant.IsActive
            && grant.Scope.Kind == ScopeKind.Tenant);

        if (!hasTenantScope)
        {
            var scopeId = new ScopeGrantId(Guid.NewGuid());
            var grant = new AccessScopeGrant(scopeId, membership.Id, adminUser.Id, OrganizationScope.ForTenant(DevTenantId), approverUser.Id);
            store.ScopeGrants.Add(scopeId, grant);
            scopeGrantIds.Add(scopeId);
        }

        // The Development operational-context fixture is intentionally
        // configuration-led. Keep its published Company/Branch/Warehouse
        // candidates backed by the same Identity-owned parent graph consumed
        // by Reporting's trusted scope resolver. This is not a production
        // organization provider and is only seeded in the Development
        // bootstrap path.
        var configuredTenant = Guid.TryParse(config["MESP_DEV_ORG_SCOPE_TENANT_ID"], out var tenantId)
            ? tenantId
            : DevTenantId.Value;
        if (configuredTenant != DevTenantId.Value)
        {
            return;
        }

        var companyId = Guid.TryParse(config["MESP_DEV_ORG_SCOPE_COMPANY_ID"], out var configuredCompany)
            ? configuredCompany
            : Guid.Parse("99999999-9999-9999-9999-999999999999");
        identity.SetOrganizationParent(
            OrganizationScope.ForCompany(DevTenantId, companyId),
            OrganizationScope.ForTenant(DevTenantId));

        if (Guid.TryParse(config["MESP_DEV_ORG_SCOPE_BRANCH_ID"], out var branchId))
        {
            identity.SetOrganizationParent(
                OrganizationScope.ForBranch(DevTenantId, branchId),
                OrganizationScope.ForCompany(DevTenantId, companyId));

            if (Guid.TryParse(config["MESP_DEV_ORG_SCOPE_WAREHOUSE_ID"], out var warehouseId))
            {
                identity.SetOrganizationParent(
                    OrganizationScope.ForWarehouse(DevTenantId, warehouseId),
                    OrganizationScope.ForBranch(DevTenantId, branchId));
            }
        }
    }
}
