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
        await SignInAsOwnerWithAutoSelectedMembershipAsync(factory, client);

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
    public async Task Suspended_auto_selected_membership_loses_context_and_selection_is_not_restored_after_reactivation()
    {
        using var factory = new HostSecurityTests.HostFactory();
        using var client = factory.CreateClient();
        await SignInAsOwnerWithAutoSelectedMembershipAsync(factory, client);
        factory.Identity.Store.Memberships[factory.MembershipA].Status = MembershipStatus.Suspended;

        var denied = await client.GetAsync($"/api/v1/foundation/targets/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/session")))
            .GetProperty("selectedTenantId").ValueKind);

        factory.Identity.Store.Memberships[factory.MembershipA].Status = MembershipStatus.Active;
        var afterReactivation = await client.GetAsync("/api/v1/foundation/tenant-context");
        Assert.Equal(HttpStatusCode.Forbidden, afterReactivation.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(await client.GetAsync("/api/v1/auth/session")))
            .GetProperty("selectedTenantId").ValueKind);
    }

    private static async Task SignInAsOwnerWithAutoSelectedMembershipAsync(
        HostSecurityTests.HostFactory factory,
        HttpClient client)
    {
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/sign-in",
                new FoundationSignInRequest("owner@example.com", factory.Password))).StatusCode);

        using var sessionResponse = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await ReadJsonAsync(sessionResponse);
        Assert.Equal(factory.TenantA.Value, session.GetProperty("selectedTenantId").GetGuid());
        Assert.Equal(factory.MembershipA.Value, session.GetProperty("selectedContextId").GetGuid());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
