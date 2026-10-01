using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MiniErp.App.Modules.Identity;
using MiniErp.Contracts.Modules.Foundation;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class PermissionDenialStatusTests
{
    [Fact]
    public async Task Permission_denial_returns_forbidden_and_preserves_the_selected_membership()
    {
        using var factory = new HostSecurityTests.HostFactory();
        using var client = factory.CreateClient();
        await SignInAndSelectOwnerMembershipAsync(factory, client);

        var denied = await client.GetAsync($"/api/v1/foundation/targets/{Guid.NewGuid():D}");
        var permitted = await client.GetAsync("/api/v1/foundation/tenant-context");

        Assert.Equal(
            (HttpStatusCode.Forbidden, HttpStatusCode.OK),
            (denied.StatusCode, permitted.StatusCode));
        Assert.Equal(factory.TenantA.Value.ToString("D"), (await ReadJsonAsync(permitted)).GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task No_session_still_returns_unauthorized_for_the_same_tenant_operation()
    {
        using var factory = new HostSecurityTests.HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();

        var response = await client.GetAsync($"/api/v1/foundation/targets/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_selected_membership_keeps_its_existing_status_and_clears_selection()
    {
        using var factory = new HostSecurityTests.HostFactory();
        using var client = factory.CreateClient();
        await SignInAndSelectOwnerMembershipAsync(factory, client);
        factory.Identity.Store.Memberships[factory.MembershipA].Status = MembershipStatus.Suspended;

        var denied = await client.GetAsync($"/api/v1/foundation/targets/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        factory.Identity.Store.Memberships[factory.MembershipA].Status = MembershipStatus.Active;
        var afterReactivation = await client.GetAsync("/api/v1/foundation/tenant-context");
        Assert.Equal(HttpStatusCode.Forbidden, afterReactivation.StatusCode);
    }

    private static async Task SignInAndSelectOwnerMembershipAsync(
        HostSecurityTests.HostFactory factory,
        HttpClient client)
    {
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/sign-in",
                new FoundationSignInRequest("owner@example.com", factory.Password))).StatusCode);

        var roleAssignment = factory.Identity.Store.RoleAssignments[factory.MembershipA].Single();
        var role = factory.Identity.Store.Roles[roleAssignment.RoleId];
        role.Permissions.Add(IdentityPermissions.ContextSwitch);

        var tokenResponse = await client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = tokenResponse.Headers.GetValues("X-CSRF-TOKEN").Single();
        using var contextsResponse = await client.GetAsync("/api/v1/auth/contexts");
        Assert.Equal(HttpStatusCode.OK, contextsResponse.StatusCode);
        using var contextsDocument = JsonDocument.Parse(await contextsResponse.Content.ReadAsStringAsync());
        var eligibilityVersion = contextsDocument.RootElement
            .GetProperty("contexts")
            .EnumerateArray()
            .Single(item => item.GetProperty("contextId").GetGuid() == factory.MembershipA.Value)
            .GetProperty("eligibilityVersion")
            .GetInt64();

        using var switchRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(
                factory.MembershipA.Value,
                0,
                eligibilityVersion))
        };
        switchRequest.Headers.TryAddWithoutValidation("Idempotency-Key", "permission-denial-context");
        switchRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(switchRequest)).StatusCode);

        role.Permissions.Remove(IdentityPermissions.ContextSwitch);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
