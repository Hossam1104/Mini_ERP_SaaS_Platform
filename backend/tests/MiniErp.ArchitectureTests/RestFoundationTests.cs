using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class RestFoundationTests : IClassFixture<RestFoundationTests.ApiFactory>
{
    private readonly ApiFactory factory;

    public RestFoundationTests(ApiFactory factory)
    {
        this.factory = factory;
        factory.Reset();
        factory.Resolver.Context = FoundationRequestContext.Unauthenticated();
        factory.Targets.Clear();
    }

    [Fact]
    public async Task Api_version_route_exists()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/foundation/tenant-context");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains(FoundationCorrelation.HeaderName));
    }

    [Fact]
    public void Authenticated_session_does_not_claim_a_tenant_or_platform_path()
    {
        var context = FoundationRequestContext.ForAuthenticatedSession(factory.ActorA, Guid.NewGuid());

        Assert.Equal(FoundationSecurityProfile.AuthenticatedSession, context.SecurityProfile);
        Assert.Null(context.TenantContext);
        Assert.Null(context.PlatformGovernanceContext);
    }

    [Fact]
    public async Task OpenApi_document_is_generated()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("foundation.probe.write", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/foundation/tenant-context", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reporting_catalogue_requires_the_exact_tenant_permission()
    {
        using var client = factory.CreateClient();

        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.reporting.report.view");
        var allowed = await client.GetAsync("/api/v1/reporting/catalogue");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("finance.trial-balance", await allowed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.finance.report.view");
        var denied = await client.GetAsync("/api/v1/reporting/catalogue");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Generated_openapi_documents_every_public_operation_and_tax_contract()
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        // The document endpoint serves the document and therefore is not
        // represented as a self-referential path inside that document.
        var publicOperations = FoundationOperationCatalog.PublicOperations
            .Where(descriptor => descriptor.OperationId != "platform.openapi");

        foreach (var descriptor in publicOperations)
        {
            var route = descriptor.Route.Replace(":guid", string.Empty, StringComparison.Ordinal);
            Assert.True(paths.TryGetProperty(route, out var path), $"OpenAPI path missing for {descriptor.OperationId}: {route}");
            var method = descriptor.HttpMethod.ToLowerInvariant();
            Assert.True(path.TryGetProperty(method, out var operation), $"OpenAPI method missing for {descriptor.OperationId}: {method} {route}");
            Assert.Equal(descriptor.OperationId, operation.GetProperty("operationId").GetString());
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            var description = operation.GetProperty("description").GetString() ?? string.Empty;
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.DoesNotContain("TBD", description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TODO", description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Gets data", description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Creates resource", description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Updates item", description, StringComparison.OrdinalIgnoreCase);
            Assert.True(operation.GetProperty("responses").EnumerateObject().Any(), $"Responses missing for {descriptor.OperationId}");
        }

        Assert.True(paths.TryGetProperty("/api/v1/master-data/taxes/{taxId}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/master-data/taxes/{taxId}/calculate", out _));
        Assert.Contains("explicit taxable base", document.RootElement.GetProperty("info").GetProperty("description").GetString()!, StringComparison.OrdinalIgnoreCase);

        var exchangeReference = paths
            .GetProperty("/api/v1/master-data/exchange-rates/{exchangeRateId}/reference")
            .GetProperty("get");
        var effectiveOn = Assert.Single(exchangeReference.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "effectiveOn");
        Assert.Equal("effectiveOn", effectiveOn.GetProperty("name").GetString());
        Assert.Equal("query", effectiveOn.GetProperty("in").GetString());
        Assert.True(effectiveOn.GetProperty("required").GetBoolean());
        Assert.Equal("date", effectiveOn.GetProperty("schema").GetProperty("format").GetString());
    }

    [Fact]
    public void Mesp133_settlement_operations_keep_tenant_and_mutation_security_contract()
    {
        var operationIds = new[]
        {
            "finance.ap.list", "finance.ap.detail", "finance.ap.source-ready", "finance.ap.recognize",
            "finance.ap.aging", "finance.ap.reconciliation", "finance.ar.list", "finance.ar.detail",
            "finance.ar.create", "finance.ar.aging", "finance.ar.exposure", "finance.ar.reconciliation",
            "finance.payment-method.list", "finance.payment-method.create", "finance.payment-method.edit",
            "finance.payment-method.lifecycle", "finance.cash-account.list", "finance.cash-account.create",
            "finance.cash-account.edit", "finance.cash-account.lifecycle", "finance.payment.list",
            "finance.payment.detail", "finance.payment.create", "finance.payment.edit", "finance.payment.submit",
            "finance.payment.approve", "finance.payment.reject", "finance.payment.post", "finance.payment.reverse",
            "finance.receipt.list", "finance.receipt.detail", "finance.receipt.create", "finance.receipt.edit",
            "finance.receipt.submit", "finance.receipt.approve", "finance.receipt.reject", "finance.receipt.post",
            "finance.receipt.reverse", "finance.allocation.list", "finance.allocation.create",
            "finance.allocation.reverse", "finance.settlement.reconciliation"
        };
        var operations = operationIds.Select(FoundationOperationCatalog.GetRequired).ToArray();

        Assert.All(operations, operation =>
        {
            Assert.Equal(FoundationOperationVisibility.Public, operation.Visibility);
            Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);
            Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, operation.SecurityProfile);
        });

        Assert.All(operations.Where(operation => operation.IsUnsafe), operation =>
        {
            Assert.True(operation.RequiresAntiforgery, operation.OperationId);
            Assert.True(operation.RequiresMandatoryAudit, operation.OperationId);
            Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
        });

        var concurrencyRequired = operations.Where(operation =>
            operation.OperationId.EndsWith(".edit", StringComparison.Ordinal)
            || operation.OperationId.EndsWith(".lifecycle", StringComparison.Ordinal)
            || operation.OperationId.EndsWith(".submit", StringComparison.Ordinal)
            || operation.OperationId.EndsWith(".approve", StringComparison.Ordinal)
            || operation.OperationId.EndsWith(".reject", StringComparison.Ordinal)
            || operation.OperationId.EndsWith(".post", StringComparison.Ordinal)).ToArray();
        Assert.All(concurrencyRequired, operation =>
            Assert.Equal(FoundationConcurrencyPolicy.IfMatch, operation.Concurrency));
    }

    [Fact]
    public void Mesp134_tax_fx_and_revaluation_operations_are_catalogued_and_tenant_scoped()
    {
        var operationIds = new[]
        {
            "finance.monetary-policy.list", "finance.monetary-policy.create",
            "finance.tax-accounting.list", "finance.tax-accounting.preview", "finance.tax-accounting.post",
            "finance.tax-accounting.reverse", "finance.tax-accounting.reconciliation",
            "finance.revaluation.list", "finance.revaluation.detail", "finance.revaluation.create",
            "finance.revaluation.calculate", "finance.revaluation.post", "finance.revaluation.reverse",
            "finance.revaluation.reconciliation"
        };
        var operations = operationIds.Select(FoundationOperationCatalog.GetRequired).ToArray();
        Assert.All(operations, operation =>
        {
            Assert.Equal(FoundationOperationVisibility.Public, operation.Visibility);
            Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);
            Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, operation.SecurityProfile);
        });
        Assert.All(operations.Where(operation => operation.IsUnsafe), operation =>
        {
            Assert.True(operation.RequiresAntiforgery, operation.OperationId);
            Assert.True(operation.RequiresMandatoryAudit, operation.OperationId);
            Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
        });
    }

    [Fact]
    public async Task Scalar_reference_is_available_only_in_the_non_production_test_host()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Scalar", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Module_registration_reports_the_composed_master_data_module()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/module-registration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var masterData = body.GetProperty("masterData");

        Assert.Equal("master-data-catalog", masterData.GetProperty("module").GetString());
        Assert.Equal("Master Data and Catalog", masterData.GetProperty("name").GetString());
        Assert.Equal("MasterDataAndCatalog", masterData.GetProperty("boundary").GetString());
        Assert.True(masterData.GetProperty("registered").GetBoolean());
    }

    [Fact]
    public void Every_public_endpoint_has_one_operation_identifier()
    {
        var endpoints = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<FoundationOperationMetadata>() is not null)
            .ToArray();

        Assert.Equal(FoundationOperationCatalog.PublicOperations.Count, endpoints.Length);
        Assert.All(endpoints, endpoint =>
        {
            var metadata = endpoint.Metadata.GetMetadata<FoundationOperationMetadata>();
            Assert.NotNull(metadata);
            Assert.False(string.IsNullOrWhiteSpace(metadata!.OperationId));
            Assert.Single(endpoint.Metadata.OfType<FoundationOperationMetadata>());
        });

        var actual = endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<FoundationOperationMetadata>()!.OperationId)
            .OrderBy(value => value, StringComparer.Ordinal);
        var expected = FoundationOperationCatalog.PublicOperations
            .Select(operation => operation.OperationId)
            .OrderBy(value => value, StringComparer.Ordinal);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Every_public_endpoint_has_one_security_profile()
    {
        var endpoints = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<FoundationOperationMetadata>() is not null)
            .ToArray();

        Assert.All(endpoints, endpoint =>
        {
            var metadata = endpoint.Metadata.GetMetadata<FoundationOperationMetadata>()!;
            Assert.True(Enum.IsDefined(metadata.SecurityProfile));
            Assert.Single(endpoint.Metadata.OfType<FoundationOperationMetadata>());
        });
    }

    [Fact]
    public void Public_endpoint_metadata_matches_the_approved_catalog_and_unsafe_operations_are_audited()
    {
        var endpoints = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<FoundationOperationMetadata>() is not null)
            .ToDictionary(
                endpoint => endpoint.Metadata.GetMetadata<FoundationOperationMetadata>()!.OperationId,
                StringComparer.Ordinal);

        foreach (var descriptor in FoundationOperationCatalog.PublicOperations)
        {
            Assert.True(endpoints.TryGetValue(descriptor.OperationId, out var endpoint));
            var metadata = endpoint!.Metadata.GetMetadata<FoundationOperationMetadata>()!;
            var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IHttpMethodMetadata>()?.HttpMethods ?? [];

            Assert.Equal(descriptor.Route, endpoint.RoutePattern.RawText);
            Assert.Contains(descriptor.HttpMethod, methods, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(descriptor.SecurityProfile, metadata.SecurityProfile);
            Assert.Equal(descriptor.Visibility, metadata.Visibility);
            Assert.Equal(descriptor.ExactPermissionCode, metadata.ExactPermissionCode);
            Assert.Equal(descriptor.ScopePolicy, metadata.ScopePolicy);
            Assert.Equal(descriptor.RequiresMfa, metadata.RequiresMfa);
            Assert.Equal(descriptor.RequiresFreshAuthentication, metadata.RequiresFreshAuthentication);
            Assert.Equal(descriptor.RequiresAntiforgery, metadata.RequiresAntiforgery);
            Assert.Equal(descriptor.RequiresMandatoryAudit, metadata.RequiresMandatoryAudit);
            Assert.Equal(descriptor.IsUnsafe, metadata.IsUnsafe);
        }

        Assert.All(
            FoundationOperationCatalog.PublicOperations.Where(operation => operation.RequiresMandatoryAudit),
            operation => Assert.True(operation.IsUnsafe));
        Assert.False(FoundationOperationCatalog.GetRequired("auth.sign-out").RequiresMandatoryAudit);
        Assert.DoesNotContain(
            FoundationOperationCatalog.PublicOperations,
            operation => operation.IsUnsafe
                && operation.SecurityProfile == FoundationSecurityProfile.Anonymous
                && operation.OperationId is not ("auth.sign-in" or "auth.development-bypass"));
    }

    [Fact]
    public void Every_non_anonymous_unsafe_handler_is_structurally_bound_to_protected_evidence()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MiniErp.Api"));
        var mapPosts = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression.ToString().EndsWith("MapPost", StringComparison.Ordinal)))
            .ToArray();

        foreach (var descriptor in FoundationOperationCatalog.PublicOperations
                     .Where(operation => operation.IsUnsafe && operation.SecurityProfile != FoundationSecurityProfile.Anonymous))
        {
            var route = Assert.Single(mapPosts, invocation =>
                invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                && literal.Token.ValueText == descriptor.Route);
            var handler = route.ArgumentList.Arguments.Skip(1).FirstOrDefault()?.Expression?.ToString() ?? string.Empty;
            if (descriptor.OperationId == "foundation.probe.write")
            {
                Assert.Contains("WriteProbeAsync", handler, StringComparison.Ordinal);
                var applicationSourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MiniErp.App", "BuildingBlocks", "Rest", "FoundationRestApplication.cs"));
                var applicationSource = File.ReadAllText(applicationSourcePath);
                Assert.Contains("FoundationAuditCoordinator auditCoordinator", applicationSource, StringComparison.Ordinal);
                Assert.Contains("ExecuteProtectedAsync", applicationSource, StringComparison.Ordinal);
                Assert.DoesNotContain("auditCoordinator is null", applicationSource, StringComparison.Ordinal);
                Assert.DoesNotContain("fallback", applicationSource, StringComparison.OrdinalIgnoreCase);
            }
            else if (descriptor.OperationId == "notification.intent.dispatch")
            {
                Assert.Contains("EnsureAntiforgeryAsync", handler, StringComparison.Ordinal);
                Assert.Contains("scopeOwnership.Resolve", handler, StringComparison.Ordinal);
                Assert.Contains("DispatchAsync", handler, StringComparison.Ordinal);
                Assert.Contains("notification.intent.dispatch", handler, StringComparison.Ordinal);
            }
            else if (handler.Contains("ExecuteProtectedAsync", StringComparison.Ordinal))
            {
                // The handler directly owns the mandatory audit boundary.
            }
            else
            {
                Assert.Contains("ExecuteMutationAsync", handler, StringComparison.Ordinal);
                var endpointSource = File.ReadAllText(Path.Combine(sourceRoot, "ProductIdentityEndpoints.cs"));
                Assert.Contains("ExecuteProtectedAsync", endpointSource, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Internal_operations_are_not_public()
    {
        var publicIds = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<FoundationOperationMetadata>()?.OperationId)
            .Where(operationId => operationId is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(
            FoundationOperationCatalog.InternalOperations,
            operation => publicIds.Contains(operation.OperationId));
    }

    [Fact]
    public async Task Missing_correlation_generates_a_valid_correlation()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var correlation = response.Headers.GetValues(FoundationCorrelation.HeaderName).Single();

        Assert.True(FoundationCorrelation.IsValid(correlation));
    }

    [Fact]
    public async Task Valid_correlation_is_preserved()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FoundationCorrelation.HeaderName, "corr-foundation-001");

        var response = await client.GetAsync("/health");

        Assert.Equal("corr-foundation-001", response.Headers.GetValues(FoundationCorrelation.HeaderName).Single());
    }

    [Fact]
    public async Task Malicious_correlation_is_replaced_safely()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FoundationCorrelation.HeaderName, new string('x', FoundationCorrelation.MaximumLength + 1));

        var response = await client.GetAsync("/health");
        var correlation = response.Headers.GetValues(FoundationCorrelation.HeaderName).Single();

        Assert.True(FoundationCorrelation.IsValid(correlation));
        Assert.NotEqual(new string('x', FoundationCorrelation.MaximumLength + 1), correlation);
    }

    [Fact]
    public async Task Unauthenticated_failure_is_safe()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FoundationCorrelation.HeaderName, "corr-unauth");

        var response = await client.GetAsync("/api/v1/foundation/tenant-context");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_failed", body.GetProperty("code").GetString());
        Assert.Equal("corr-unauth", body.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("stack", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unauthorized_tenant_profile_failure_is_safe()
    {
        factory.Resolver.Context = CreateSupportContext("support.tenant.read");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/foundation/tenant-context");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("access_denied", body.GetProperty("code").GetString());
        Assert.DoesNotContain(factory.TenantB.Value.ToString(), body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_and_foreign_target_responses_are_equivalent()
    {
        var context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.target.read");
        factory.Resolver.Context = context;
        var foreignTarget = Guid.NewGuid();
        factory.Targets.Add(foreignTarget, factory.TenantB);
        using var client = factory.CreateClient();

        var missingResponse = await client.GetAsync($"/api/v1/foundation/targets/{Guid.NewGuid():D}");
        var foreignResponse = await client.GetAsync($"/api/v1/foundation/targets/{foreignTarget:D}");
        var missing = await ReadJsonAsync(missingResponse);
        var foreign = await ReadJsonAsync(foreignResponse);

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(missing.GetProperty("code").GetString(), foreign.GetProperty("code").GetString());
        Assert.Equal(missing.GetProperty("title").GetString(), foreign.GetProperty("title").GetString());
        Assert.Equal(missing.GetProperty("detail").GetString(), foreign.GetProperty("detail").GetString());
        Assert.DoesNotContain(foreignTarget.ToString(), foreign.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.TenantB.Value.ToString(), foreign.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_details_and_stack_traces_are_absent_from_errors()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/foundation/targets/not-a-guid");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Sql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authorized_request_succeeds_with_server_context()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.context.read");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/foundation/tenant-context");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(factory.TenantA.Value.ToString("D"), body.GetProperty("tenantId").GetString());
        Assert.Equal("OrdinaryMembership", body.GetProperty("authorizationPath").GetString());
    }

    [Fact]
    public async Task Server_not_client_selects_tenant_authority()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.context.read");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", factory.TenantB.Value.ToString("D"));

        var response = await client.GetAsync("/api/v1/foundation/tenant-context");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(factory.TenantA.Value.ToString("D"), body.GetProperty("tenantId").GetString());
        Assert.DoesNotContain(factory.TenantB.Value.ToString(), body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Idempotent_replay_returns_one_safe_result()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();

        var first = await PostProbeAsync(client, "idem-replay", "alpha", "1", includeAntiforgery: true);
        var replay = await PostProbeAsync(client, "idem-replay", "alpha", "1", includeAntiforgery: true);
        var firstBody = await ReadJsonAsync(first);
        var replayBody = await ReadJsonAsync(replay);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(firstBody.GetProperty("version").GetString(), replayBody.GetProperty("version").GetString());
        Assert.True(replayBody.GetProperty("replayed").GetBoolean());
    }

    [Fact]
    public async Task Same_key_with_changed_payload_conflicts()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();

        _ = await PostProbeAsync(client, "idem-payload", "alpha", "1", includeAntiforgery: true);
        var response = await PostProbeAsync(client, "idem-payload", "bravo", "2", includeAntiforgery: true);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("idempotency_conflict", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Concurrent_first_requests_produce_one_effect_and_no_abandoned_reservation()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();

        using var antiResponse = await client.GetAsync(new Uri("https://localhost/api/v1/auth/antiforgery"));
        var antiToken = antiResponse.Headers.GetValues("X-CSRF-TOKEN").Single();
        var responses = await Task.WhenAll(
            PostProbeAsync(client, "idem-concurrent", "alpha", "1", includeAntiforgery: true, antiforgeryToken: antiToken),
            PostProbeAsync(client, "idem-concurrent", "alpha", "1", includeAntiforgery: true, antiforgeryToken: antiToken));
        var bodies = await Task.WhenAll(responses.Select(ReadJsonAsync));

        Assert.Equal(2, factory.Services.GetRequiredService<LocalFoundationProbeStore>().CurrentVersion(factory.TenantA));
        var successfulBodies = bodies.Where(body => body.TryGetProperty("result", out var result) && result.GetString() == "accepted").ToArray();
        Assert.Single(successfulBodies, body => !body.GetProperty("replayed").GetBoolean());
        Assert.All(responses, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Cross_tenant_key_is_independent()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();
        _ = await PostProbeAsync(client, "idem-tenant", "alpha", "1", includeAntiforgery: true);

        // Keep the actor constant so the Tenant component of the composite
        // binding is the only changed namespace dimension.
        factory.Resolver.Context = CreateTenantContext(factory.TenantB, factory.ActorA, "tenant.foundation.probe.write");
        var response = await PostProbeAsync(client, "idem-tenant", "alpha", "1", includeAntiforgery: true);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("accepted", body.GetProperty("result").GetString());
    }

    [Fact]
    public async Task Cross_user_key_is_independent()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();
        _ = await PostProbeAsync(client, "idem-user", "alpha", "1", includeAntiforgery: true);

        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorB, "tenant.foundation.probe.write");
        var response = await PostProbeAsync(client, "idem-user", "alpha", "2", includeAntiforgery: true);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("accepted", body.GetProperty("result").GetString());
    }

    [Fact]
    public async Task Stale_version_returns_safe_conflict()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();
        _ = await PostProbeAsync(client, "idem-current", "alpha", "1", includeAntiforgery: true);

        var response = await PostProbeAsync(client, "idem-stale", "bravo", "1", includeAntiforgery: true);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("concurrency_conflict", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Missing_required_version_fails_safely()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();

        var response = await PostProbeAsync(client, "idem-version", "alpha", ifMatch: null, includeAntiforgery: true);
        var body = await ReadJsonAsync(response);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.Equal("version_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Cookie_style_write_without_antiforgery_denies()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();

        var response = await PostProbeAsync(client, "idem-antiforgery", "alpha", "1", includeAntiforgery: false);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("antiforgery_failed", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Valid_antiforgery_compatible_write_succeeds()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.probe.write");
        using var client = factory.CreateClient();

        var response = await PostProbeAsync(client, "idem-antiforgery-valid", "alpha", "1", includeAntiforgery: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_has_no_state_changing_behavior()
    {
        factory.Resolver.Context = CreateTenantContext(factory.TenantA, factory.ActorA, "tenant.foundation.context.read");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/foundation/tenant-context");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Platform_governance_context_is_not_tenant_context()
    {
        var actor = Guid.NewGuid();
        factory.Resolver.Context = FoundationRequestContext.ForPlatform(
            actor,
            Guid.NewGuid(),
            new PlatformGovernanceContext(actor, PlatformGovernancePurpose.PlatformMetadata, new CorrelationId("platform")),
            "platform.metadata.read");
        using var client = factory.CreateClient();

        var platformResponse = await client.GetAsync("/api/v1/foundation/platform-context");
        var tenantResponse = await client.GetAsync("/api/v1/foundation/tenant-context");
        var platform = await ReadJsonAsync(platformResponse);
        var tenant = await ReadJsonAsync(tenantResponse);

        Assert.Equal(HttpStatusCode.OK, platformResponse.StatusCode);
        Assert.True(platform.GetProperty("platformGovernance").GetBoolean());
        Assert.False(platform.GetProperty("tenantId").ValueKind is not JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.Forbidden, tenantResponse.StatusCode);
        Assert.Equal("access_denied", tenant.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ordinary_membership_and_support_grant_profiles_are_not_combined()
    {
        factory.Resolver.Context = CreateSupportContext("support.tenant.read");
        using var client = factory.CreateClient();

        var supportResponse = await client.GetAsync("/api/v1/foundation/support-context");
        var ordinaryResponse = await client.GetAsync("/api/v1/foundation/tenant-context");
        var support = await ReadJsonAsync(supportResponse);
        var ordinary = await ReadJsonAsync(ordinaryResponse);

        Assert.Equal(HttpStatusCode.OK, supportResponse.StatusCode);
        Assert.Equal("SupportGrant", support.GetProperty("authorizationPath").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, ordinaryResponse.StatusCode);
        Assert.Equal("access_denied", ordinary.GetProperty("code").GetString());
    }

    [Fact]
    public async Task OpenApi_contains_no_secret_or_internal_schema()
    {
        using var client = factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.DoesNotContain("cookie", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IdentityAuthorizationService", document, StringComparison.Ordinal);
        Assert.DoesNotContain("identity.invitation.issue", document, StringComparison.Ordinal);
        Assert.DoesNotContain("identity.recovery.consume", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_manual_journal_request_exposes_no_source_authority_fields()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var schema = root.GetProperty("paths")
            .GetProperty("/api/v1/finance/journals")
            .GetProperty("post")
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var schemaName = reference.GetString()!.Split('/').Last();
            schema = root.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        }

        var properties = new HashSet<string>(StringComparer.Ordinal);
        CollectOpenApiProperties(schema, root, properties);
        foreach (var sourceOwnedField in new[] { "sourceContract", "sourceEvent", "sourceEvidenceId", "sourceEvidenceVersion", "postingRuleId" })
        {
            Assert.DoesNotContain(sourceOwnedField, properties);
        }
    }

    private static void CollectOpenApiProperties(JsonElement schema, JsonElement root, ISet<string> properties)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var schemaName = reference.GetString()!.Split('/').Last();
            CollectOpenApiProperties(root.GetProperty("components").GetProperty("schemas").GetProperty(schemaName), root, properties);
        }

        if (schema.TryGetProperty("properties", out var directProperties))
        {
            foreach (var property in directProperties.EnumerateObject()) properties.Add(property.Name);
        }

        foreach (var composition in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(composition, out var alternatives)) continue;
            foreach (var alternative in alternatives.EnumerateArray()) CollectOpenApiProperties(alternative, root, properties);
        }
    }

    private async Task<HttpResponseMessage> PostProbeAsync(
        HttpClient client,
        string idempotencyKey,
        string value,
        string? ifMatch,
        bool includeAntiforgery,
        string? antiforgeryToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://localhost/api/v1/foundation/probe"))
        {
            Content = JsonContent.Create(new FoundationWriteRequest(value))
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }

        if (includeAntiforgery)
        {
            var token = antiforgeryToken;
            if (token is null)
            {
                using var tokenResponse = await client.GetAsync(new Uri("https://localhost/api/v1/auth/antiforgery"));
                token = tokenResponse.Headers.GetValues("X-CSRF-TOKEN").Single();
            }

            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private FoundationRequestContext CreateTenantContext(TenantId tenantId, Guid actorId, string permission) =>
        FoundationRequestContext.ForTenant(
            actorId,
            Guid.NewGuid(),
            TenantContext.ForOrdinaryMembership(
                tenantId,
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("tenant"),
                new CorrelationId("server-correlation"),
                actorId),
            permission);

    private FoundationRequestContext CreateSupportContext(string permission) =>
        FoundationRequestContext.ForTenant(
            factory.ActorA,
            Guid.NewGuid(),
            TenantContext.ForSupportGrant(
                factory.TenantA,
                new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
                new ScopeReference("support-case"),
                new CorrelationId("server-support"),
                factory.ActorA),
            permission);

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        internal readonly TestResolver Resolver = new();
        internal readonly TestTargets Targets = new();
        internal readonly TenantId TenantA = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        internal readonly TenantId TenantB = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        internal readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        internal readonly Guid ActorB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        internal void Reset()
        {
            Resolver.Context = FoundationRequestContext.Unauthenticated();
            Targets.Clear();
            Services.GetRequiredService<LocalFoundationProbeStore>().Clear();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // REST foundation tests use in-memory collaborators and must
                // remain independent of the disposable SQL safety database.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MESP_SQLSERVER_CONNECTION_STRING"] = " "
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITrustedRequestContextResolver>();
                services.AddSingleton<ITrustedRequestContextResolver>(Resolver);
                services.RemoveAll<IFoundationTargetDirectory>();
                services.AddSingleton<IFoundationTargetDirectory>(Targets);
            });
        }
    }

    internal sealed class TestResolver : ITrustedRequestContextResolver
    {
        internal FoundationRequestContext Context { get; set; } = FoundationRequestContext.Unauthenticated();

        public ValueTask<FoundationRequestContext> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Context);
    }

    internal sealed class TestTargets : IFoundationTargetDirectory
    {
        private readonly Dictionary<Guid, TenantId> owners = [];

        internal void Add(Guid targetId, TenantId owner) => owners[targetId] = owner;

        internal void Clear() => owners.Clear();

        public bool TryGetTenant(Guid targetId, out TenantId tenantId) => owners.TryGetValue(targetId, out tenantId);
    }
}
