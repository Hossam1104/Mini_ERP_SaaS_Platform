using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Identity;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Direct host security tests use the production Program graph. The only
/// substitution is a bounded in-memory identity/assurance provider so the
/// host adapter is exercised without a database or an external MFA service.
/// The trusted resolver itself is never replaced.
/// </summary>
public sealed class HostSecurityTests
{
    [Fact]
    public async Task Tenant_host_selects_only_exact_membership_and_denies_Tenant_B_only_user()
    {
        using var factory = new HostFactory { RequestHost = "tenant-a.example.com" };
        using var ownerClient = factory.CreateClient();
        factory.SeedCore();

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(ownerClient, "owner@example.com", factory.Password)).StatusCode);
        var ownerEntry = await ownerClient.GetAsync("/api/v1/auth/entry");
        Assert.Equal(HttpStatusCode.OK, ownerEntry.StatusCode);
        var ownerBody = await ReadJsonAsync(ownerEntry);
        Assert.Equal("TenantHost", ownerBody.GetProperty("entryMode").GetString());
        Assert.Equal(factory.TenantA.Value.ToString("D"), ownerBody.GetProperty("candidateTenantId").GetString());
        Assert.Equal("MESP", ownerBody.GetProperty("branding").GetProperty("displayName").GetString());
        Assert.False(ownerBody.GetProperty("branding").GetProperty("tenantConfigured").GetBoolean());
        var authorizedOwnerTenant = Assert.Single(ownerBody.GetProperty("authorizedTenants").EnumerateArray());
        Assert.Equal(factory.TenantA.Value.ToString("D"), authorizedOwnerTenant.GetProperty("tenantId").GetString());

        using var foreignClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(foreignClient, "foreign@example.com", factory.Password)).StatusCode);
        var foreignEntry = await foreignClient.GetAsync("/api/v1/auth/entry");
        Assert.Equal(HttpStatusCode.OK, foreignEntry.StatusCode);
        var foreignBody = await ReadJsonAsync(foreignEntry);
        Assert.Equal("NoAccess", foreignBody.GetProperty("entryMode").GetString());
        Assert.Empty(foreignBody.GetProperty("authorizedTenants").EnumerateArray());

        var deniedBusinessRead = await foreignClient.GetAsync("/api/v1/foundation/tenant-context");
        Assert.Equal(HttpStatusCode.Forbidden, deniedBusinessRead.StatusCode);
    }

    [Fact]
    public async Task Untrusted_forwarded_host_cannot_change_common_entry_resolution()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "owner@example.com", factory.Password)).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/entry");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "wafra.example.com");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("CommonHost", body.GetProperty("entryMode").GetString());
        Assert.NotEqual(0, body.GetProperty("authorizedTenants").GetArrayLength());
    }

    [Fact]
    public async Task Mandatory_evidence_failure_blocks_probe_and_releases_idempotency_reservation()
    {
        using var factory = new HostFactory { FailAuditEvidence = true };
        using var client = factory.CreateClient();
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "probe@example.com", factory.Password)).StatusCode);
        var token = await GetAntiforgeryTokenAsync(client);

        // Establish a valid selected context before exercising the protected
        // write.  Evidence failure must be the failing condition, not the
        // absence of a selected context.
        factory.FailingAuditSink!.Failing = false;
        var eligibilityVersion = (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/contexts")))
            .GetProperty("contexts").EnumerateArray()
            .Single(item => item.GetProperty("contextId").GetGuid() == factory.ProbeMembershipA.Value)
            .GetProperty("eligibilityVersion").GetInt64();
        Assert.Equal(HttpStatusCode.OK,
            (await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "seed-probe-context", token)).StatusCode);
        factory.FailingAuditSink.Failing = true;

        var failed = await PostProbeAsync(client, "audit-failure-probe", "blocked", "1", token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(1, factory.Services.GetRequiredService<LocalFoundationProbeStore>().CurrentVersion(factory.TenantA));

        factory.FailingAuditSink!.Failing = false;
        var retried = await PostProbeAsync(client, "audit-failure-probe", "blocked", "1", token);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("accepted", (await ReadJsonAsync(retried)).GetProperty("result").GetString());
        Assert.Equal(2, factory.Services.GetRequiredService<LocalFoundationProbeStore>().CurrentVersion(factory.TenantA));
    }

    [Fact]
    public async Task Mandatory_evidence_failure_blocks_context_selection_and_selected_signout()
    {
        using var factory = new HostFactory { FailAuditEvidence = true };
        using var client = factory.CreateClient();
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "probe@example.com", factory.Password)).StatusCode);
        var token = await GetAntiforgeryTokenAsync(client);
        var eligibilityVersion = (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/contexts")))
            .GetProperty("contexts").EnumerateArray()
            .Single(item => item.GetProperty("contextId").GetGuid() == factory.ProbeMembershipA.Value)
            .GetProperty("eligibilityVersion").GetInt64();

        var failedSwitch = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "audit-failure-switch", token);
        Assert.Equal(HttpStatusCode.Conflict, failedSwitch.StatusCode);
        var before = await ReadJsonAsync(await client.GetAsync("/api/v1/auth/session"));
        Assert.Equal(0, before.GetProperty("selectionVersion").GetInt64());

        factory.FailingAuditSink!.Failing = false;
        var switched = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "audit-failure-switch", token);
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        Assert.Equal(1, (await ReadJsonAsync(switched)).GetProperty("selectionVersion").GetInt64());

        factory.FailingAuditSink.Failing = true;
        using var signOut = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-out");
        signOut.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        var failedSignOut = await client.SendAsync(signOut);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedSignOut.StatusCode);
        var stillSelected = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, stillSelected.StatusCode);
        Assert.Equal(1, (await ReadJsonAsync(stillSelected)).GetProperty("selectionVersion").GetInt64());
    }

    [Fact]
    public async Task Exact_operation_permissions_separate_read_probe_and_unrelated_writes()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "owner@example.com", factory.Password)).StatusCode);
        var readOnlyResponse = await client.GetAsync("/api/v1/foundation/tenant-context");
        Assert.Equal(HttpStatusCode.Forbidden, readOnlyResponse.StatusCode);

        using var ownerProbe = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest("read-only"))
        };
        ownerProbe.Headers.TryAddWithoutValidation("Idempotency-Key", "read-only-probe");
        ownerProbe.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        ownerProbe.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", await GetAntiforgeryTokenAsync(client));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(ownerProbe)).StatusCode);

        // Establish the server-selected context with a temporary technical
        // context-switch grant, then remove that grant before the read.  The
        // read request itself is authorized only by ContextRead.
        var ownerRoleAssignment = factory.Identity.Store.RoleAssignments[factory.MembershipA].Single();
        var ownerRole = factory.Identity.Store.Roles[ownerRoleAssignment.RoleId];
        ownerRole.Permissions.Add(IdentityPermissions.ContextSwitch);
        var ownerToken = await GetAntiforgeryTokenAsync(client);
        var ownerEligibilityVersion = (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/contexts")))
            .GetProperty("contexts").EnumerateArray()
            .Single(item => item.GetProperty("contextId").GetGuid() == factory.MembershipA.Value)
            .GetProperty("eligibilityVersion").GetInt64();
        Assert.Equal(HttpStatusCode.OK,
            (await SwitchAsync(client, factory.MembershipA.Value, 0, ownerEligibilityVersion, "owner-read-context", ownerToken)).StatusCode);
        ownerRole.Permissions.Remove(IdentityPermissions.ContextSwitch);

        var authorizedRead = await client.GetAsync("/api/v1/foundation/tenant-context");
        Assert.Equal(HttpStatusCode.OK, authorizedRead.StatusCode);
        Assert.Equal(factory.TenantA.Value.ToString("D"), (await ReadJsonAsync(authorizedRead)).GetProperty("tenantId").GetString());

        using var otherClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(otherClient, "other-write@example.com", factory.Password)).StatusCode);
        var otherToken = await GetAntiforgeryTokenAsync(otherClient);
        using var otherProbe = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest("unrelated"))
        };
        otherProbe.Headers.TryAddWithoutValidation("Idempotency-Key", "unrelated-probe");
        otherProbe.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        otherProbe.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", otherToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.SendAsync(otherProbe)).StatusCode);

        using var probeClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(probeClient, "probe@example.com", factory.Password)).StatusCode);
        var probeToken = await GetAntiforgeryTokenAsync(probeClient);
        var probeEligibilityVersion = (await ReadJsonAsync(await probeClient.GetAsync("/api/v1/auth/contexts")))
            .GetProperty("contexts").EnumerateArray()
            .Single(item => item.GetProperty("contextId").GetGuid() == factory.ProbeMembershipA.Value)
            .GetProperty("eligibilityVersion").GetInt64();
        Assert.Equal(HttpStatusCode.OK,
            (await SwitchAsync(probeClient, factory.ProbeMembershipA.Value, 0, probeEligibilityVersion, "exact-probe-context", probeToken)).StatusCode);
        using var exactProbe = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest("exact"))
        };
        exactProbe.Headers.TryAddWithoutValidation("Idempotency-Key", "exact-probe");
        exactProbe.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        exactProbe.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", probeToken);
        Assert.Equal(HttpStatusCode.OK, (await probeClient.SendAsync(exactProbe)).StatusCode);
    }
    [Fact]
    public async Task Sign_in_uses_secure_http_only_cookie_and_never_serializes_secrets()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();

        var anonymousSession = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSession.StatusCode);
        var anonymousSessionBody = await ReadJsonAsync(anonymousSession);
        Assert.False(anonymousSessionBody.TryGetProperty("displayName", out _));
        Assert.False(anonymousSessionBody.TryGetProperty("login", out _));
        var anonymousEntry = await client.GetAsync("/api/v1/auth/entry");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousEntry.StatusCode);
        var anonymousEntryBody = await ReadJsonAsync(anonymousEntry);
        Assert.False(anonymousEntryBody.TryGetProperty("displayName", out _));
        Assert.False(anonymousEntryBody.TryGetProperty("login", out _));

        var response = await SignInAsync(client, "owner@example.com", factory.Password);
        var body = await ReadJsonAsync(response);
        var raw = body.GetRawText();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-MiniErp.Auth=", setCookie, StringComparison.Ordinal);
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FoundationIdentityClaims.SessionToken, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookieValue", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("owner@example.com", body.GetProperty("login").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("displayName").ValueKind);

        var session = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var sessionBody = await ReadJsonAsync(session);
        Assert.Equal("owner@example.com", sessionBody.GetProperty("login").GetString());
        Assert.Equal(JsonValueKind.Null, sessionBody.GetProperty("displayName").ValueKind);

        var entry = await ReadJsonAsync(await client.GetAsync("/api/v1/auth/entry"));
        Assert.False(entry.TryGetProperty("displayName", out _));
        Assert.False(entry.TryGetProperty("login", out _));

        using var foreignClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(foreignClient, "foreign@example.com", factory.Password)).StatusCode);
        var foreignSession = await ReadJsonAsync(await foreignClient.GetAsync("/api/v1/auth/session"));
        Assert.Equal("foreign@example.com", foreignSession.GetProperty("login").GetString());
        Assert.NotEqual(sessionBody.GetProperty("login").GetString(), foreignSession.GetProperty("login").GetString());

        factory.Identity.CreateUser("admin@mesp.com", factory.Password);
        using var adminClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(adminClient, "ADMIN@MESP.COM", factory.Password)).StatusCode);
        var adminSession = await ReadJsonAsync(await adminClient.GetAsync("/api/v1/auth/session"));
        Assert.Equal("admin@mesp.com", adminSession.GetProperty("login").GetString());
    }

    [Fact]
    public async Task Wrong_password_is_generic_and_fifth_failure_locks_the_account()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await SignInAsync(client, "owner@example.com", "wrong-password");
            var body = await ReadJsonAsync(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("authentication_failed", body.GetProperty("code").GetString());
        }

        var locked = await SignInAsync(client, "owner@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("authentication_failed", (await ReadJsonAsync(locked)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Contexts_are_server_derived_and_foreign_context_or_spoofed_headers_have_no_authority()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        var signIn = await SignInAsync(client, "owner@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var contextsResponse = await client.GetAsync("/api/v1/auth/contexts");
        var contexts = await ReadJsonAsync(contextsResponse);
        Assert.Equal(HttpStatusCode.OK, contextsResponse.StatusCode);
        var candidates = contexts.GetProperty("contexts").EnumerateArray().ToArray();
        var candidate = Assert.Single(candidates);
        Assert.Equal(factory.TenantA.Value, Guid.Parse(candidate.GetProperty("tenantId").GetString()!));
        Assert.DoesNotContain(candidates, item => item.GetProperty("tenantId").GetString() == factory.TenantB.Value.ToString("D"));

        using var spoofed = new HttpRequestMessage(HttpMethod.Get, "/api/v1/foundation/tenant-context");
        spoofed.Headers.TryAddWithoutValidation("X-Tenant-Id", factory.TenantA.Value.ToString("D"));
        spoofed.Headers.TryAddWithoutValidation("X-User-Id", factory.Owner.Value.ToString("D"));
        spoofed.Headers.TryAddWithoutValidation("X-Role", "tenant-admin");
        spoofed.Headers.TryAddWithoutValidation("X-Session-Id", Guid.NewGuid().ToString("D"));
        var spoofedResponse = await client.SendAsync(spoofed);
        Assert.Equal(HttpStatusCode.Forbidden, spoofedResponse.StatusCode);

        var antiToken = await GetAntiforgeryTokenAsync(client);
        var foreign = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(factory.MembershipB.Value, 0, 1))
        };
        foreign.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiToken);
        foreign.Headers.TryAddWithoutValidation("Idempotency-Key", "context-foreign-001");
        var foreignResponse = await client.SendAsync(foreign);
        Assert.Equal(HttpStatusCode.Forbidden, foreignResponse.StatusCode);
        Assert.Equal("access_denied", (await ReadJsonAsync(foreignResponse)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Framework_antiforgery_protects_context_switch_and_probe_with_idempotency_and_version_checks()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "probe@example.com", factory.Password)).StatusCode);

        var antiResponse = await client.GetAsync("/api/v1/auth/antiforgery");
        var antiToken = antiResponse.Headers.GetValues("X-CSRF-TOKEN").Single();
        Assert.DoesNotContain(antiToken, await antiResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var eligibilityVersion = (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/contexts")))
            .GetProperty("contexts").EnumerateArray()
            .Single(item => item.GetProperty("contextId").GetGuid() == factory.ProbeMembershipA.Value)
            .GetProperty("eligibilityVersion").GetInt64();
        var missingAnti = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "context-no-anti-001", token: null);
        Assert.Equal(HttpStatusCode.Forbidden, missingAnti.StatusCode);
        Assert.Equal("antiforgery_failed", (await ReadJsonAsync(missingAnti)).GetProperty("code").GetString());

        var wrongAnti = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "context-wrong-anti-001", "fixed-token");
        Assert.Equal(HttpStatusCode.Forbidden, wrongAnti.StatusCode);

        var switched = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "context-switch-001", antiToken);
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var switchedBody = await ReadJsonAsync(switched);
        Assert.Equal(factory.TenantA.Value, Guid.Parse(switchedBody.GetProperty("selectedTenantId").GetString()!));

        var replay = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "context-switch-001", antiToken);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, (await ReadJsonAsync(replay)).GetProperty("selectionVersion").GetInt64());

        var stale = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "context-stale-001", antiToken);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("context_version_conflict", (await ReadJsonAsync(stale)).GetProperty("code").GetString());

        var tenantContext = await client.GetAsync("/api/v1/foundation/tenant-context");
        Assert.Equal(HttpStatusCode.OK, tenantContext.StatusCode);

        var missingProbeAnti = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest("probe"))
        };
        missingProbeAnti.Headers.TryAddWithoutValidation("Idempotency-Key", "probe-no-anti-001");
        missingProbeAnti.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        var missingProbeResponse = await client.SendAsync(missingProbeAnti);
        Assert.Equal(HttpStatusCode.Forbidden, missingProbeResponse.StatusCode);

        using var probe = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest("probe"))
        };
        probe.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiToken);
        probe.Headers.TryAddWithoutValidation("Idempotency-Key", "probe-001");
        probe.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        var probeResponse = await client.SendAsync(probe);
        Assert.True(probeResponse.StatusCode == HttpStatusCode.OK, await probeResponse.Content.ReadAsStringAsync());
        Assert.Equal("accepted", (await ReadJsonAsync(probeResponse)).GetProperty("result").GetString());
    }

    [Fact]
    public async Task Context_switch_replay_returns_original_selection_after_a_later_switch()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "probe@example.com", factory.Password)).StatusCode);
        var token = await GetAntiforgeryTokenAsync(client);
        var contexts = (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/contexts")))
            .GetProperty("contexts").EnumerateArray().ToArray();
        var candidateA = contexts.Single(item => item.GetProperty("contextId").GetGuid() == factory.ProbeMembershipA.Value);
        var candidateB = contexts.Single(item => item.GetProperty("contextId").GetGuid() == factory.ProbeMembershipB.Value);

        var first = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, candidateA.GetProperty("eligibilityVersion").GetInt64(), "original-selection", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await ReadJsonAsync(first);
        Assert.Equal(factory.ProbeMembershipA.Value, firstBody.GetProperty("selectedContextId").GetGuid());
        Assert.Equal(1, firstBody.GetProperty("selectionVersion").GetInt64());

        var later = await SwitchAsync(client, factory.ProbeMembershipB.Value, 1, candidateB.GetProperty("eligibilityVersion").GetInt64(), "later-selection", token);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal(factory.ProbeMembershipB.Value, (await ReadJsonAsync(later)).GetProperty("selectedContextId").GetGuid());

        var replay = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, candidateA.GetProperty("eligibilityVersion").GetInt64(), "original-selection", token);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBody = await ReadJsonAsync(replay);
        Assert.True(replayBody.GetProperty("replayed").GetBoolean());
        Assert.Equal(factory.ProbeMembershipA.Value, replayBody.GetProperty("selectedContextId").GetGuid());
        Assert.Equal(1, replayBody.GetProperty("selectionVersion").GetInt64());

        var current = await ReadJsonAsync(await client.GetAsync("/api/v1/auth/session"));
        Assert.Equal(factory.ProbeMembershipB.Value, current.GetProperty("selectedContextId").GetGuid());
        Assert.Equal(2, current.GetProperty("selectionVersion").GetInt64());
    }

    [Fact]
    public async Task Context_switch_requires_current_eligibility_and_only_one_competing_switch_succeeds()
    {
        using var factory = new HostFactory();
        factory.SeedCore();
        var host = factory.IdentityHost;
        var signIn = host.SignIn("probe@example.com", factory.Password);
        Assert.True(signIn.Succeeded);
        var candidateA = host.ListContexts(signIn.Principal!).Single(item => item.ContextId == factory.ProbeMembershipA.Value);
        var candidateB = host.ListContexts(signIn.Principal!).Single(item => item.ContextId == factory.ProbeMembershipB.Value);

        factory.Identity.Store.Memberships[factory.ProbeMembershipA].Version++;
        var stale = host.SwitchContext(signIn.Principal!, factory.ProbeMembershipA.Value, 0, candidateA.EligibilityVersion);
        Assert.False(stale.Succeeded);
        Assert.Equal("context_version_conflict", stale.Code);

        var results = await Task.WhenAll(
            Task.Run(() => host.SwitchContext(signIn.Principal!, factory.ProbeMembershipA.Value, 0, factory.Identity.Store.Memberships[factory.ProbeMembershipA].Version)),
            Task.Run(() => host.SwitchContext(signIn.Principal!, factory.ProbeMembershipB.Value, 0, candidateB.EligibilityVersion)));
        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded && result.Code == "context_version_conflict");
        Assert.Equal(1, host.GetSession(signIn.Principal!).SelectionVersion);
    }

    [Fact]
    public async Task Sign_out_and_server_revocation_invalidate_the_cookie_session()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        var signIn = await SignInAsync(client, "probe@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var token = await GetAntiforgeryTokenAsync(client);
        var eligibilityVersion = (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/contexts")))
            .GetProperty("contexts").EnumerateArray()
            .Single(item => item.GetProperty("contextId").GetGuid() == factory.ProbeMembershipA.Value)
            .GetProperty("eligibilityVersion").GetInt64();
        var selected = await SwitchAsync(client, factory.ProbeMembershipA.Value, 0, eligibilityVersion, "signout-context-001", token);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        using var signOut = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-out");
        signOut.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        var signOutResponse = await client.SendAsync(signOut);
        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
        Assert.True(factory.Services.GetRequiredService<LocalImmutableAuditEvidenceStore>().Count >= 1);

        var secondSignIn = await SignInAsync(client, "probe@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.OK, secondSignIn.StatusCode);
        var secondBody = await ReadJsonAsync(secondSignIn);
        var secondSessionId = Guid.Parse(secondBody.GetProperty("sessionId").GetString()!);
        factory.Identity.RevokeSession(new SessionId(secondSessionId), "test-revocation");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
    }

    [Fact]
    public void Production_resolver_requires_a_server_session_and_ignores_claims_or_headers()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        var host = factory.IdentityHost;
        var resolver = new DefaultTrustedRequestContextResolver(host);
        var signIn = host.SignIn("probe@example.com", factory.Password);
        Assert.True(signIn.Succeeded);
        Assert.NotNull(signIn.Principal);

        var initial = Resolve(resolver, signIn.Principal!, FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"), factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.AuthenticatedSession, initial.SecurityProfile);

        var candidate = host.ListContexts(signIn.Principal!).Single(item => item.ContextId == factory.ProbeMembershipA.Value);
        var selected = host.SwitchContext(signIn.Principal!, factory.ProbeMembershipA.Value, 0, candidate.EligibilityVersion);
        Assert.True(selected.Succeeded);
        var ordinary = Resolve(resolver, signIn.Principal!, FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"), factory.TenantB.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, ordinary.SecurityProfile);
        Assert.Equal(factory.TenantA, ordinary.TenantContext!.TenantId);

        var spoofedIdentity = new ClaimsIdentity(FirstPartyCookieConfiguration.Scheme);
        spoofedIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, factory.Owner.Value.ToString("D")));
        spoofedIdentity.AddClaim(new Claim(FoundationIdentityClaims.SessionId, Guid.NewGuid().ToString("D")));
        spoofedIdentity.AddClaim(new Claim(FoundationIdentityClaims.SessionToken, "client-chosen-token"));
        var spoofed = Resolve(resolver, new ClaimsPrincipal(spoofedIdentity), FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"), factory.TenantB.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, spoofed.SecurityProfile);

        factory.Identity.RevokeSession(new SessionId(signIn.SessionId!.Value), "resolver-revocation");
        var revoked = Resolve(resolver, signIn.Principal!, FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"), factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, revoked.SecurityProfile);
    }

    [Fact]
    public void Production_resolver_rejects_missing_malformed_and_client_supplied_authority()
    {
        using var factory = new HostFactory();
        factory.SeedCore();
        var resolver = new DefaultTrustedRequestContextResolver(factory.IdentityHost);
        var ordinary = FoundationOperationCatalog.GetRequired("foundation.tenant-context.read");

        var unauthenticated = Resolve(
            resolver,
            new ClaimsPrincipal(new ClaimsIdentity()),
            ordinary,
            factory.TenantA.Value.ToString("D"),
            "/api/v1/foundation/tenant-context?tenantId=" + factory.TenantA.Value.ToString("D"),
            "?tenantId=" + factory.TenantA.Value.ToString("D"),
            "{\"tenantId\":\"" + factory.TenantA.Value.ToString("D") + "\",\"userId\":\"" + factory.Owner.Value.ToString("D") + "\"}");
        Assert.Equal(FoundationSecurityProfile.Anonymous, unauthenticated.SecurityProfile);

        var missingClaims = new ClaimsPrincipal(new ClaimsIdentity(FirstPartyCookieConfiguration.Scheme));
        Assert.Equal(FoundationSecurityProfile.Anonymous,
            Resolve(resolver, missingClaims, ordinary, factory.TenantA.Value.ToString("D")).SecurityProfile);

        var malformedIdentity = new ClaimsIdentity(FirstPartyCookieConfiguration.Scheme);
        malformedIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
        malformedIdentity.AddClaim(new Claim(FoundationIdentityClaims.SessionId, "also-not-a-guid"));
        malformedIdentity.AddClaim(new Claim(FoundationIdentityClaims.SessionToken, "client-chosen-token"));
        malformedIdentity.AddClaim(new Claim(ClaimTypes.Role, "platform-admin"));
        malformedIdentity.AddClaim(new Claim("permission", IdentityPermissions.ProbeWrite.Value));
        Assert.Equal(FoundationSecurityProfile.Anonymous,
            Resolve(resolver, new ClaimsPrincipal(malformedIdentity), ordinary, factory.TenantB.Value.ToString("D")).SecurityProfile);

        var signIn = factory.IdentityHost.SignIn("probe@example.com", factory.Password);
        Assert.True(signIn.Succeeded);
        Assert.NotNull(signIn.Principal);

        var unknown = ordinary with { OperationId = "foundation.unknown.operation", ExactPermissionCode = IdentityPermissions.ProbeWrite.Value };
        Assert.Equal(FoundationSecurityProfile.Anonymous,
            Resolve(resolver, signIn.Principal!, unknown, factory.TenantA.Value.ToString("D")).SecurityProfile);

        var internalDescriptor = FoundationOperationCatalog.GetRequired("identity.invitation.issue");
        Assert.Equal(FoundationSecurityProfile.Anonymous,
            Resolve(resolver, signIn.Principal!, internalDescriptor, factory.TenantA.Value.ToString("D")).SecurityProfile);

        var operationTextCannotGrant = ordinary with { ExactPermissionCode = IdentityPermissions.ProbeWrite.Value };
        Assert.Equal(FoundationSecurityProfile.Anonymous,
            Resolve(resolver, signIn.Principal!, operationTextCannotGrant, factory.TenantA.Value.ToString("D")).SecurityProfile);

        // A valid session without a selected business path remains a session
        // context; no request path, query, body or header can manufacture a
        // Tenant authorization path.
        var sessionOnly = Resolve(
            resolver,
            signIn.Principal!,
            ordinary,
            factory.TenantB.Value.ToString("D"),
            "/api/v1/foundation/tenant-context/" + factory.TenantB.Value.ToString("D"),
            "?tenantId=" + factory.TenantB.Value.ToString("D") + "&userId=" + factory.Owner.Value.ToString("D"),
            "{\"tenantId\":\"" + factory.TenantB.Value.ToString("D") + "\",\"role\":\"tenant-admin\"}");
        Assert.Equal(FoundationSecurityProfile.AuthenticatedSession, sessionOnly.SecurityProfile);
        Assert.Equal(IdentityPermissions.ContextRead.Value, sessionOnly.Permission);
    }

    [Fact]
    public void Production_resolver_covers_ordinary_exact_permission_mixed_paths_and_membership_lifecycle()
    {
        using var factory = new HostFactory();
        factory.SeedCore();
        var host = factory.IdentityHost;
        var resolver = new DefaultTrustedRequestContextResolver(host);
        var signIn = host.SignIn("probe@example.com", factory.Password);
        Assert.True(signIn.Succeeded);
        Assert.NotNull(signIn.Principal);

        var candidate = host.ListContexts(signIn.Principal!).Single(item => item.ContextId == factory.ProbeMembershipA.Value);
        Assert.Equal(1, candidate.EligibilityVersion);
        Assert.True(host.SwitchContext(signIn.Principal!, factory.ProbeMembershipA.Value, 0, candidate.EligibilityVersion).Succeeded);

        var read = Resolve(resolver, signIn.Principal!, FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"), factory.TenantB.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, read.SecurityProfile);
        Assert.Equal(IdentityPermissions.ContextRead.Value, read.Permission);
        Assert.Equal(factory.TenantA, read.TenantContext!.TenantId);

        var probe = Resolve(resolver, signIn.Principal!, FoundationOperationCatalog.GetRequired("foundation.probe.write"), factory.TenantB.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, probe.SecurityProfile);
        Assert.Equal(IdentityPermissions.ProbeWrite.Value, probe.Permission);

        // A selected ordinary path cannot be reused to satisfy a support path.
        var mixedPath = Resolve(resolver, signIn.Principal!, FoundationOperationCatalog.GetRequired("foundation.support-context.read"), factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, mixedPath.SecurityProfile);

        factory.Identity.Store.Memberships[factory.ProbeMembershipA].Status = MembershipStatus.Suspended;
        var suspendedMembership = Resolve(resolver, signIn.Principal!, FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"), factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, suspendedMembership.SecurityProfile);
    }

    [Fact]
    public void Production_resolver_rejects_revoked_expired_and_inactive_server_sessions()
    {
        using (var revokedFactory = new HostFactory())
        {
            revokedFactory.SeedCore();
            var revoked = revokedFactory.IdentityHost.SignIn("probe@example.com", revokedFactory.Password);
            Assert.True(revoked.Succeeded);
            revokedFactory.Identity.RevokeSession(new SessionId(revoked.SessionId!.Value), "resolver-revocation");
            var context = Resolve(
                new DefaultTrustedRequestContextResolver(revokedFactory.IdentityHost),
                revoked.Principal!,
                FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"),
                revokedFactory.TenantA.Value.ToString("D"));
            Assert.Equal(FoundationSecurityProfile.Anonymous, context.SecurityProfile);
        }

        using (var expiredFactory = new HostFactory())
        {
            expiredFactory.SeedCore();
            var expired = expiredFactory.IdentityHost.SignIn("probe@example.com", expiredFactory.Password);
            Assert.True(expired.Succeeded);
            expiredFactory.Clock.Advance(TimeSpan.FromHours(9));
            var context = Resolve(
                new DefaultTrustedRequestContextResolver(expiredFactory.IdentityHost),
                expired.Principal!,
                FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"),
                expiredFactory.TenantA.Value.ToString("D"));
            Assert.Equal(FoundationSecurityProfile.Anonymous, context.SecurityProfile);
        }

        using (var inactiveFactory = new HostFactory())
        {
            inactiveFactory.SeedCore();
            var inactive = inactiveFactory.IdentityHost.SignIn("probe@example.com", inactiveFactory.Password);
            Assert.True(inactive.Succeeded);
            inactiveFactory.Identity.Store.Users[inactiveFactory.ProbeUser].Status = GlobalUserStatus.Suspended;
            var context = Resolve(
                new DefaultTrustedRequestContextResolver(inactiveFactory.IdentityHost),
                inactive.Principal!,
                FoundationOperationCatalog.GetRequired("foundation.tenant-context.read"),
                inactiveFactory.TenantA.Value.ToString("D"));
            Assert.Equal(FoundationSecurityProfile.Anonymous, context.SecurityProfile);
        }
    }

    [Fact]
    public void Support_and_platform_contexts_require_assurance_and_resolve_through_the_same_host_seam()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedAll();
        var host = factory.IdentityHost;

        var support = host.SignIn("support@example.com", factory.Password);
        Assert.True(support.Succeeded);
        Assert.DoesNotContain(host.ListContexts(support.Principal!), item => item.Kind == FoundationHostContextKind.SupportGrant);
        var supportToken = RawToken(support.Principal!);
        var supportSession = new SessionId(support.SessionId!.Value);
        Assert.True(factory.Assurance.IssueMfaAndAccept(factory.Identity, supportToken, factory.SupportUser, supportSession, factory.Clock.GetUtcNow()));
        var supportOperation = $"support:{factory.SupportGrant.Value}:{IdentityPermissions.SupportRead.Value}";
        Assert.True(factory.Assurance.IssueFreshAndAccept(factory.Identity, supportToken, factory.SupportUser, supportSession, supportOperation, factory.Clock.GetUtcNow()));
        Assert.Contains(host.ListContexts(support.Principal!), item => item.ContextId == factory.SupportGrant.Value && item.Kind == FoundationHostContextKind.SupportGrant);
        var supportCandidate = host.ListContexts(support.Principal!).Single(item => item.ContextId == factory.SupportGrant.Value);
        Assert.True(host.SwitchContext(support.Principal!, factory.SupportGrant.Value, 0, supportCandidate.EligibilityVersion).Succeeded);
        Assert.Equal(FoundationSecurityProfile.SupportGrant, host.ResolveContext(support.Principal!, "support-correlation", FoundationOperationCatalog.GetRequired("foundation.support-context.read")).SecurityProfile);

        var platform = host.SignIn("platform@example.com", factory.Password);
        Assert.True(platform.Succeeded);
        var platformToken = RawToken(platform.Principal!);
        var platformSession = new SessionId(platform.SessionId!.Value);
        Assert.True(factory.Assurance.IssueMfaAndAccept(factory.Identity, platformToken, factory.PlatformUser, platformSession, factory.Clock.GetUtcNow()));
        Assert.True(factory.Assurance.IssueFreshAndAccept(factory.Identity, platformToken, factory.PlatformUser, platformSession, "foundation.platform-context.read", factory.Clock.GetUtcNow()));
        Assert.Contains(host.ListContexts(platform.Principal!), item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);
        var platformContext = host.ListContexts(platform.Principal!).Single(item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);
        Assert.True(host.SwitchContext(platform.Principal!, platformContext.ContextId, 0, platformContext.EligibilityVersion).Succeeded);
        Assert.Equal(FoundationSecurityProfile.PlatformGovernanceContext, host.ResolveContext(platform.Principal!, "platform-correlation", FoundationOperationCatalog.GetRequired("foundation.platform-context.read")).SecurityProfile);
    }

    [Fact]
    public void Production_resolver_rejects_revoked_and_expired_support_grants_and_keeps_permission_exact()
    {
        using var factory = new HostFactory();
        factory.SeedAll();
        var host = factory.IdentityHost;
        var resolver = new DefaultTrustedRequestContextResolver(host);

        var support = host.SignIn("support@example.com", factory.Password);
        Assert.True(support.Succeeded);
        var supportToken = RawToken(support.Principal!);
        var supportSession = new SessionId(support.SessionId!.Value);
        Assert.True(factory.Assurance.IssueMfaAndAccept(factory.Identity, supportToken, factory.SupportUser, supportSession, factory.Clock.GetUtcNow()));
        var supportOperation = $"support:{factory.SupportGrant.Value}:{IdentityPermissions.SupportRead.Value}";
        Assert.True(factory.Assurance.IssueFreshAndAccept(factory.Identity, supportToken, factory.SupportUser, supportSession, supportOperation, factory.Clock.GetUtcNow()));
        var supportCandidate = host.ListContexts(support.Principal!).Single(item => item.ContextId == factory.SupportGrant.Value);
        Assert.True(host.SwitchContext(support.Principal!, factory.SupportGrant.Value, 0, supportCandidate.EligibilityVersion).Succeeded);

        var supportContext = Resolve(resolver, support.Principal!, FoundationOperationCatalog.GetRequired("foundation.support-context.read"), factory.TenantB.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.SupportGrant, supportContext.SecurityProfile);
        Assert.Equal(IdentityPermissions.SupportRead.Value, supportContext.Permission);

        var supportProbe = Resolve(resolver, support.Principal!, FoundationOperationCatalog.GetRequired("foundation.probe.write"), factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, supportProbe.SecurityProfile);

        factory.Identity.Store.SupportGrants[factory.SupportGrant].RevokedAt = factory.Clock.GetUtcNow();
        var revoked = Resolve(resolver, support.Principal!, FoundationOperationCatalog.GetRequired("foundation.support-context.read"), factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, revoked.SecurityProfile);

        var expiringCase = factory.Identity.AddSupportCase(factory.TenantA);
        var expiringGrant = new SupportGrant(
            new SupportGrantId(Guid.NewGuid()),
            expiringCase,
            factory.SupportUser,
            factory.Approver,
            factory.TenantA,
            "short-lived diagnosis",
            OrganizationScope.ForTenant(factory.TenantA),
            [IdentityPermissions.SupportRead],
            factory.Clock.GetUtcNow().AddMinutes(1));
        factory.Identity.Store.SupportGrants.Add(expiringGrant.Id, expiringGrant);

        var expiring = host.SignIn("support@example.com", factory.Password);
        Assert.True(expiring.Succeeded);
        var expiringToken = RawToken(expiring.Principal!);
        var expiringSession = new SessionId(expiring.SessionId!.Value);
        Assert.True(factory.Assurance.IssueMfaAndAccept(factory.Identity, expiringToken, factory.SupportUser, expiringSession, factory.Clock.GetUtcNow()));
        var expiringOperation = $"support:{expiringGrant.Id.Value}:{IdentityPermissions.SupportRead.Value}";
        Assert.True(factory.Assurance.IssueFreshAndAccept(factory.Identity, expiringToken, factory.SupportUser, expiringSession, expiringOperation, factory.Clock.GetUtcNow()));
        var expiringCandidate = host.ListContexts(expiring.Principal!).Single(item => item.ContextId == expiringGrant.Id.Value);
        Assert.True(host.SwitchContext(expiring.Principal!, expiringGrant.Id.Value, 0, expiringCandidate.EligibilityVersion).Succeeded);

        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var expired = Resolve(resolver, expiring.Principal!, FoundationOperationCatalog.GetRequired("foundation.support-context.read"), factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, expired.SecurityProfile);
    }

    [Fact]
    public void Production_resolver_requires_exact_platform_permission_and_assurance()
    {
        using var factory = new HostFactory();
        factory.SeedAll();
        var host = factory.IdentityHost;
        var resolver = new DefaultTrustedRequestContextResolver(host);
        var descriptor = FoundationOperationCatalog.GetRequired("foundation.platform-context.read");
        var platform = host.SignIn("platform@example.com", factory.Password);
        Assert.True(platform.Succeeded);
        Assert.DoesNotContain(host.ListContexts(platform.Principal!), item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);

        factory.Identity.Store.PlatformPermissions[factory.PlatformUser] =
        [new PlatformPermissionAssignment(factory.PlatformUser, IdentityPermissions.AssignPlatformPermission, factory.Approver, "unrelated permission")];
        Assert.DoesNotContain(host.ListContexts(platform.Principal!), item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);
        Assert.NotEqual(FoundationSecurityProfile.PlatformGovernanceContext,
            Resolve(resolver, platform.Principal!, descriptor, factory.TenantA.Value.ToString("D")).SecurityProfile);

        factory.Identity.Store.PlatformPermissions[factory.PlatformUser] =
        [new PlatformPermissionAssignment(factory.PlatformUser, IdentityPermissions.PlatformMetadataRead, factory.Approver, "exact permission")];
        var platformToken = RawToken(platform.Principal!);
        var platformSession = new SessionId(platform.SessionId!.Value);
        Assert.True(factory.Assurance.IssueMfaAndAccept(factory.Identity, platformToken, factory.PlatformUser, platformSession, factory.Clock.GetUtcNow()));
        Assert.True(factory.Assurance.IssueFreshAndAccept(factory.Identity, platformToken, factory.PlatformUser, platformSession, descriptor.OperationId, factory.Clock.GetUtcNow()));
        var candidate = host.ListContexts(platform.Principal!).Single(item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);
        Assert.True(host.SwitchContext(platform.Principal!, candidate.ContextId, 0, candidate.EligibilityVersion).Succeeded);

        var resolved = Resolve(resolver, platform.Principal!, descriptor, factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.PlatformGovernanceContext, resolved.SecurityProfile);
        Assert.Equal(IdentityPermissions.PlatformMetadataRead.Value, resolved.Permission);
    }

    private static FoundationRequestContext Resolve(
        DefaultTrustedRequestContextResolver resolver,
        ClaimsPrincipal principal,
        FoundationOperationDescriptor descriptor,
        string spoofedTenant,
        string? spoofedPath = null,
        string? spoofedQuery = null,
        string? spoofedBody = null)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-Tenant-Id"] = spoofedTenant;
        httpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString("D");
        httpContext.Request.Headers["X-Session-Id"] = Guid.NewGuid().ToString("D");
        httpContext.Request.Headers["X-Role"] = "platform-admin";
        httpContext.Request.Headers["X-Permission"] = IdentityPermissions.ProbeWrite.Value;
        httpContext.Request.Path = spoofedPath ?? "/api/v1/foundation/tenant-context";
        httpContext.Request.QueryString = new QueryString(spoofedQuery ?? "?tenantId=" + spoofedTenant);
        if (spoofedBody is not null)
        {
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(spoofedBody));
        }
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new FoundationOperationMetadata(descriptor)), "test"));
        return resolver.ResolveAsync(httpContext).Result;
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string login, string password) =>
        await client.PostAsJsonAsync("/api/v1/auth/sign-in", new FoundationSignInRequest(login, password));

    private static async Task<HttpResponseMessage> PostProbeAsync(HttpClient client, string idempotencyKey, string value, string ifMatch, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest(value))
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SwitchAsync(HttpClient client, Guid contextId, long expectedSelectionVersion, long expectedEligibilityVersion, string idempotencyKey, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(contextId, expectedSelectionVersion, expectedEligibilityVersion))
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.GetValues("X-CSRF-TOKEN").Single();
    }

    private static string RawToken(ClaimsPrincipal principal) =>
        principal.FindFirstValue(FoundationIdentityClaims.SessionToken)!;

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    internal sealed class HostFactory : WebApplicationFactory<Program>
    {
        internal readonly ManualTimeProvider Clock = new();
        internal readonly TestAuthenticationAssuranceEvidenceSource Assurance = new();
        internal bool FailAuditEvidence { get; set; }
        internal FailingEvidenceSink? FailingAuditSink { get; private set; }
        internal readonly TenantId TenantA = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        internal readonly TenantId TenantB = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        internal readonly string Password = "Correct-horse-battery-1!";
        internal IdentityAuthorizationService Identity { get; private set; } = null!;
        internal IFoundationIdentityHost IdentityHost { get; private set; } = null!;
        internal UserId Owner { get; private set; }
        internal UserId ProbeUser { get; private set; }
        internal UserId OtherWriteUser { get; private set; }
        internal UserId Approver { get; private set; }
        internal UserId SupportUser { get; private set; }
        internal UserId PlatformUser { get; private set; }
        internal MembershipId MembershipA { get; private set; }
        internal MembershipId MembershipB { get; private set; }
        internal MembershipId ProbeMembershipA { get; private set; }
        internal MembershipId ProbeMembershipB { get; private set; }
        internal MembershipId OtherWriteMembershipA { get; private set; }
        internal SupportGrantId SupportGrant { get; private set; }
        internal string RequestHost { get; set; } = "localhost";

        private bool seeded;

        internal void SeedCore()
        {
            if (seeded)
            {
                return;
            }

            Identity = Services.GetRequiredService<IdentityAuthorizationService>();
            Approver = Identity.CreateUser("approver@example.com", Password);
            Owner = Identity.CreateUser("owner@example.com", Password);
            ProbeUser = Identity.CreateUser("probe@example.com", Password);
            OtherWriteUser = Identity.CreateUser("other-write@example.com", Password);
            var foreign = Identity.CreateUser("foreign@example.com", Password);
            MembershipA = Identity.AddMembership(Owner, TenantA);
            MembershipB = Identity.AddMembership(Owner, TenantB);
            ProbeMembershipA = Identity.AddMembership(ProbeUser, TenantA);
            ProbeMembershipB = Identity.AddMembership(ProbeUser, TenantB);
            OtherWriteMembershipA = Identity.AddMembership(OtherWriteUser, TenantA);
            Identity.AddMembership(foreign, TenantB);

            var role = Identity.CreateRole("tenant-read-only", TenantA, false, [IdentityPermissions.ContextRead]);
            Identity.Store.RoleAssignments[MembershipA].Add(new RoleAssignment(role, MembershipA, Owner, TenantA, Approver));
            var scope = new ScopeGrantId(Guid.NewGuid());
            Identity.Store.ScopeGrants.Add(scope, new AccessScopeGrant(scope, MembershipA, Owner, OrganizationScope.ForTenant(TenantA), Approver));
            Identity.Store.ScopeGrantsByMembership[MembershipA].Add(scope);

            var probeRole = Identity.CreateRole("foundation-probe", TenantA, false, [IdentityPermissions.ContextRead, IdentityPermissions.ContextSwitch, IdentityPermissions.ProbeWrite]);
            Identity.Store.RoleAssignments[ProbeMembershipA].Add(new RoleAssignment(probeRole, ProbeMembershipA, ProbeUser, TenantA, Approver));
            var probeScope = new ScopeGrantId(Guid.NewGuid());
            Identity.Store.ScopeGrants.Add(probeScope, new AccessScopeGrant(probeScope, ProbeMembershipA, ProbeUser, OrganizationScope.ForTenant(TenantA), Approver));
            Identity.Store.ScopeGrantsByMembership[ProbeMembershipA].Add(probeScope);

            var probeRoleB = Identity.CreateRole("foundation-probe-b", TenantB, false, [IdentityPermissions.ContextRead, IdentityPermissions.ContextSwitch, IdentityPermissions.ProbeWrite]);
            Identity.Store.RoleAssignments[ProbeMembershipB].Add(new RoleAssignment(probeRoleB, ProbeMembershipB, ProbeUser, TenantB, Approver));
            var probeScopeB = new ScopeGrantId(Guid.NewGuid());
            Identity.Store.ScopeGrants.Add(probeScopeB, new AccessScopeGrant(probeScopeB, ProbeMembershipB, ProbeUser, OrganizationScope.ForTenant(TenantB), Approver));
            Identity.Store.ScopeGrantsByMembership[ProbeMembershipB].Add(probeScopeB);

            var otherWriteRole = Identity.CreateRole("foundation-other-write", TenantA, false, [IdentityPermissions.ContextRead, IdentityPermissions.ContextSwitch, IdentityPermissions.AssignRole]);
            Identity.Store.RoleAssignments[OtherWriteMembershipA].Add(new RoleAssignment(otherWriteRole, OtherWriteMembershipA, OtherWriteUser, TenantA, Approver));
            var otherScope = new ScopeGrantId(Guid.NewGuid());
            Identity.Store.ScopeGrants.Add(otherScope, new AccessScopeGrant(otherScope, OtherWriteMembershipA, OtherWriteUser, OrganizationScope.ForTenant(TenantA), Approver));
            Identity.Store.ScopeGrantsByMembership[OtherWriteMembershipA].Add(otherScope);
            IdentityHost = Services.GetRequiredService<IFoundationIdentityHost>();
            seeded = true;
        }

        internal void SeedAll()
        {
            SeedCore();
            if (SupportUser != default)
            {
                return;
            }

            SupportUser = Identity.CreateUser("support@example.com", Password);
            var supportCase = Identity.AddSupportCase(TenantA);
            var supportGrant = new SupportGrant(
                new SupportGrantId(Guid.NewGuid()),
                supportCase,
                SupportUser,
                Approver,
                TenantA,
                "incident diagnosis",
                OrganizationScope.ForTenant(TenantA),
                [IdentityPermissions.SupportRead],
                Clock.GetUtcNow().AddHours(1));
            SupportGrant = supportGrant.Id;
            Identity.Store.SupportGrants.Add(SupportGrant, supportGrant);

            PlatformUser = Identity.CreateUser("platform@example.com", Password);
            Identity.Store.PlatformPermissions[PlatformUser] =
            [new PlatformPermissionAssignment(PlatformUser, IdentityPermissions.PlatformMetadataRead, Approver, "test seed")];
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // These host security assertions exercise the production cookie
            // contract. Development HTTP compatibility is covered by the
            // Development bootstrap/runtime smoke path instead.
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                if (!string.Equals(RequestHost, "localhost", StringComparison.OrdinalIgnoreCase))
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["MESP_TENANT_HOST_BINDINGS:0:Host"] = RequestHost,
                        ["MESP_TENANT_HOST_BINDINGS:0:TenantId"] = TenantA.Value.ToString("D"),
                        ["MESP_TENANT_HOST_BINDINGS:0:CanonicalHost"] = RequestHost,
                    });
                }
            });
            builder.ConfigureTestServices(services =>
            {
                if (FailAuditEvidence)
                {
                    FailingAuditSink = new FailingEvidenceSink();
                    services.RemoveAll<IFoundationAuditEvidenceSink>();
                    services.AddSingleton<IFoundationAuditEvidenceSink>(FailingAuditSink);
                    services.RemoveAll<FoundationAuditCoordinator>();
                    services.AddSingleton<FoundationAuditCoordinator>(serviceProvider =>
                        new FoundationAuditCoordinator(
                            serviceProvider.GetRequiredService<IFoundationAuditEvidenceSink>(),
                            serviceProvider.GetRequiredService<IFoundationAuditTelemetrySink>(),
                            serviceProvider.GetRequiredService<IFoundationAuditOperationalSignalSink>()));
                }
                services.RemoveAll<IdentityAuthorizationService>();
                services.RemoveAll<IdentityStore>();
                services.RemoveAll<IFoundationIdentityHost>();
                services.RemoveAll<IAuthenticationAssuranceEvidenceSource>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IAuthenticationAssuranceEvidenceSource>(Assurance);
                services.AddSingleton<IdentityAuthorizationService>(_ =>
                    new IdentityAuthorizationService(timeProvider: Clock, assuranceEvidenceSource: Assurance));
                services.AddSingleton<IFoundationIdentityHost>(serviceProvider =>
                    new FoundationIdentityHost(serviceProvider.GetRequiredService<IdentityAuthorizationService>()));
            });
        }

        internal new HttpClient CreateClient() => base.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{RequestHost}"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    }

    internal sealed class FailingEvidenceSink : IFoundationAuditEvidenceSink
    {
        internal bool Failing { get; set; } = true;

        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default)
        {
            if (Failing)
            {
                throw new FoundationAuditAppendException("test_failure");
            }

            return ValueTask.CompletedTask;
        }
    }

    internal sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset current = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => current;

        internal void Advance(TimeSpan duration) => current = current.Add(duration);
    }

    internal sealed class TestAuthenticationAssuranceEvidenceSource : IAuthenticationAssuranceEvidenceSource
    {
        private readonly Dictionary<string, AuthenticationAssuranceEvidence> evidenceByToken = new(StringComparer.Ordinal);

        internal bool IssueMfaAndAccept(IdentityAuthorizationService service, string sessionToken, UserId userId, SessionId sessionId, DateTimeOffset now)
        {
            var token = Issue(new AuthenticationAssuranceEvidence(userId, sessionId, AuthenticationAssuranceType.Mfa, null, now, now.AddMinutes(5), SourceId, true));
            return service.AcceptMfaEvidence(sessionToken, token);
        }

        internal bool IssueFreshAndAccept(IdentityAuthorizationService service, string sessionToken, UserId userId, SessionId sessionId, string operation, DateTimeOffset now)
        {
            var token = Issue(new AuthenticationAssuranceEvidence(userId, sessionId, AuthenticationAssuranceType.FreshAuthentication, operation, now, now.AddMinutes(5), SourceId, true));
            return service.AcceptFreshAuthenticationEvidence(sessionToken, operation, token);
        }

        public string SourceId => "test-assurance";

        public bool TryValidateMfaEvidence(string opaqueEvidence, UserId expectedUserId, SessionId expectedSessionId, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence) =>
            TryTake(opaqueEvidence, expectedUserId, expectedSessionId, AuthenticationAssuranceType.Mfa, null, now, out evidence);

        public bool TryValidateFreshAuthenticationEvidence(string opaqueEvidence, UserId expectedUserId, SessionId expectedSessionId, string operation, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence) =>
            TryTake(opaqueEvidence, expectedUserId, expectedSessionId, AuthenticationAssuranceType.FreshAuthentication, operation, now, out evidence);

        private string Issue(AuthenticationAssuranceEvidence evidence)
        {
            var token = $"test-evidence-{Guid.NewGuid():N}";
            evidenceByToken.Add(token, evidence);
            return token;
        }

        private bool TryTake(string token, UserId userId, SessionId sessionId, AuthenticationAssuranceType type, string? operation, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence)
        {
            if (!evidenceByToken.TryGetValue(token, out evidence!)
                || evidence.UserId != userId
                || evidence.SessionId != sessionId
                || evidence.Type != type
                || !string.Equals(evidence.Operation, operation, StringComparison.Ordinal)
                || evidence.IssuedAt > now
                || evidence.ExpiresAt <= now)
            {
                evidence = null!;
                return false;
            }

            evidenceByToken.Remove(token);
            return true;
        }
    }
}
