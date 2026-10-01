using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MiniErp.Api;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class DevelopmentBootstrapTests
{
    [Fact]
    public void TenantDisplayNames_AreServerConfiguredAndKeepGenericFallbacks()
    {
        var configuredTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"MESP_TENANT_DISPLAY_NAMES:{configuredTenantId:D}"] = "North Star"
            })
            .Build();
        var provider = new ConfiguredTenantDisplayNameProvider(configuration);

        Assert.Equal("North Star", provider.GetDisplayName(new TenantId(configuredTenantId)));
        Assert.Equal(
            $"Tenant {DevelopmentBootstrap.DevTenantId.Value:D}",
            provider.GetDisplayName(DevelopmentBootstrap.DevTenantId));
    }

    [Fact]
    public void DevelopmentBypassPolicy_RequiresExactDevelopmentEnabledSettingAndLoopbackAddress()
    {
        var development = CreateHostEnvironment(Environments.Development);
        var enabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DevelopmentAuthBypassPolicy.SettingName] = "true"
            })
            .Build();

        Assert.True(DevelopmentAuthBypassPolicy.IsAllowed(development, enabled, IPAddress.Loopback));
        Assert.True(DevelopmentAuthBypassPolicy.IsAllowed(development, enabled, IPAddress.IPv6Loopback));
        Assert.False(DevelopmentAuthBypassPolicy.IsAllowed(development, enabled, IPAddress.Parse("192.0.2.10")));
        Assert.False(DevelopmentAuthBypassPolicy.IsAllowed(development, enabled, remoteIpAddress: null));
    }

    [Fact]
    public void DevelopmentBypassPolicy_DeniesWhenEnvironmentIsNotExactDevelopmentOrSettingIsDisabled()
    {
        var enabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DevelopmentAuthBypassPolicy.SettingName] = "true"
            })
            .Build();
        var disabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DevelopmentAuthBypassPolicy.SettingName] = "false"
            })
            .Build();

        Assert.False(DevelopmentAuthBypassPolicy.IsAllowed(
            CreateHostEnvironment(Environments.Production),
            enabled,
            IPAddress.Loopback));
        Assert.False(DevelopmentAuthBypassPolicy.IsAllowed(
            CreateHostEnvironment("development"),
            enabled,
            IPAddress.Loopback));
        Assert.False(DevelopmentAuthBypassPolicy.IsAllowed(
            CreateHostEnvironment(Environments.Development),
            disabled,
            IPAddress.Loopback));
    }

    [Fact]
    public void Bootstrap_DisabledByDefault_DoesNotSeedUser()
    {
        using var factory = new CustomTestWebApplicationFactory(new Dictionary<string, string?>());
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityAuthorizationService>();

        var result = identity.Authenticate("admin@minierp.local", "SomePassword123!");
        Assert.False(result.Succeeded);
        Assert.Equal("authentication_failed", result.PublicCode);
    }

    [Fact]
    public void Bootstrap_DummyAccountAndPublicHintAreDevelopmentOnly()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_LOGIN"] = DevelopmentBootstrap.DefaultAdminLogin,
            ["MESP_DEV_ADMIN_PASSWORD"] = DevelopmentBootstrap.DefaultAdminPassword
        };
        using var productionHost = Host.CreateDefaultBuilder()
            .UseEnvironment(Environments.Production)
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings))
            .ConfigureServices((_, services) => services.AddSingleton(new IdentityAuthorizationService()))
            .Build();
        var identity = productionHost.Services.GetRequiredService<IdentityAuthorizationService>();

        productionHost.SeedDevelopmentBootstrap();

        Assert.False(identity.Authenticate(
            DevelopmentBootstrap.DefaultAdminLogin,
            DevelopmentBootstrap.DefaultAdminPassword).Succeeded);
        Assert.Null(DevelopmentBootstrap.GetPublicDefaultAccountHint(
            productionHost.Services.GetRequiredService<IHostEnvironment>(),
            productionHost.Services.GetRequiredService<IConfiguration>()));
        var developmentConfiguration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Assert.Equal(
            "admin@mesp.com / 123",
            DevelopmentBootstrap.GetPublicDefaultAccountHint(
                CreateHostEnvironment(Environments.Development),
                developmentConfiguration));
    }

    [Fact]
    public void Bootstrap_DefaultDummyAccountAuthenticatesWithItsTenantMembership()
    {
        using var factory = new CustomTestWebApplicationFactory(new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_PASSWORD"] = DevelopmentBootstrap.DefaultAdminPassword
        });
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IFoundationIdentityHost>();

        var signIn = identity.SignIn(
            DevelopmentBootstrap.DefaultAdminLogin,
            DevelopmentBootstrap.DefaultAdminPassword);

        Assert.True(signIn.Succeeded);
        Assert.NotNull(signIn.Principal);
        Assert.Single(identity.ListContexts(signIn.Principal));
        Assert.Equal(DevelopmentBootstrap.DevTenantId.Value, identity.ListContexts(signIn.Principal)[0].TenantId);
    }

    [Fact]
    public async Task DevelopmentBypass_DisabledByDefault_IsUnavailable()
    {
        using var factory = new CustomTestWebApplicationFactory(new Dictionary<string, string?>
        {
            ["MESP_DEV_AUTH_BYPASS"] = "false"
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var bypassResponse = await client.PostAsync("/api/v1/auth/development-bypass", content: null);

        Assert.Equal(HttpStatusCode.NotFound, bypassResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
    }

    [Fact]
    public void Bootstrap_EnabledWithPassword_SeedsUserAndPermissionsIdempotently()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_LOGIN"] = "admin@minierp.local",
            ["MESP_DEV_ADMIN_PASSWORD"] = "LocalDevSecret123!"
        };

        using var factory = new CustomTestWebApplicationFactory(settings);
        using var scope = factory.Services.CreateScope();

        var host = scope.ServiceProvider.GetRequiredService<IFoundationIdentityHost>();

        var signIn = host.SignIn("admin@minierp.local", "LocalDevSecret123!");
        Assert.True(signIn.Succeeded);
        Assert.NotNull(signIn.Principal);
        Assert.NotNull(signIn.SessionId);

        var contexts = host.ListContexts(signIn.Principal);
        Assert.Single(contexts);
        Assert.Equal(DevelopmentBootstrap.DevTenantId.Value, contexts[0].TenantId);
    }

    [Fact]
    public void Bootstrap_WrongPassword_Fails()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_LOGIN"] = "admin@minierp.local",
            ["MESP_DEV_ADMIN_PASSWORD"] = "LocalDevSecret123!"
        };

        using var factory = new CustomTestWebApplicationFactory(settings);
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityAuthorizationService>();

        var result = identity.Authenticate("admin@minierp.local", "WrongPassword!");
        Assert.False(result.Succeeded);
        Assert.Equal("authentication_failed", result.PublicCode);
    }

    [Fact]
    public void DevelopmentSqliteInitialization_IsModuleScopedAndIdempotent()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MiniErp-ArchitectureTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var masterDataConnectionString = $"Data Source={Path.Combine(directory, "masterdata.db")}";
        var businessPartiesConnectionString = $"Data Source={Path.Combine(directory, "business-parties.db")}";

        try
        {
            MasterDataPersistenceServiceCollectionExtensions.EnsureDevelopmentSqliteDatabase(
                masterDataConnectionString);
            BusinessPartiesPersistenceServiceCollectionExtensions.EnsureDevelopmentSqliteDatabase(
                businessPartiesConnectionString);

            // Startup can be repeated without a catch-and-ignore path hiding a
            // malformed or partially initialized schema.
            MasterDataPersistenceServiceCollectionExtensions.EnsureDevelopmentSqliteDatabase(
                masterDataConnectionString);
            BusinessPartiesPersistenceServiceCollectionExtensions.EnsureDevelopmentSqliteDatabase(
                businessPartiesConnectionString);

            var masterDataTables = ReadTableNames(masterDataConnectionString);
            var businessPartiesTables = ReadTableNames(businessPartiesConnectionString);
            Assert.Contains("PriceLists", masterDataTables);
            Assert.DoesNotContain("Customers", masterDataTables);
            Assert.Contains("Customers", businessPartiesTables);
            Assert.DoesNotContain("PriceLists", businessPartiesTables);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Bootstrap_FullSignInSessionContextFlow_Succeeds()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_LOGIN"] = "admin@minierp.local",
            ["MESP_DEV_ADMIN_PASSWORD"] = "LocalDevSecret123!"
        };

        using var factory = new CustomTestWebApplicationFactory(settings);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // 1. Sign in
        var signInResponse = await client.PostAsJsonAsync("/api/v1/auth/sign-in", new FoundationSignInRequest("admin@minierp.local", "LocalDevSecret123!"));
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);

        // 2. Fetch Antiforgery token
        var antiforgeryMessage = await client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, antiforgeryMessage.StatusCode);
        var csrfToken = antiforgeryMessage.Headers.GetValues("X-CSRF-TOKEN").Single();

        // 3. Read session
        var sessionResponse = await client.GetFromJsonAsync<FoundationSessionResponse>("/api/v1/auth/session");
        Assert.NotNull(sessionResponse);
        Assert.True(sessionResponse.Authenticated);

        // 4. List contexts
        var contextsResponse = await client.GetFromJsonAsync<FoundationContextsResponse>("/api/v1/auth/contexts");
        Assert.NotNull(contextsResponse);
        Assert.Single(contextsResponse.Contexts);

        var targetContext = contextsResponse.Contexts[0];
        Assert.Equal(DevelopmentBootstrap.DevTenantId.Value, targetContext.TenantId);

        // 5. Switch context with CSRF and Idempotency headers
        using var switchRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(
                targetContext.ContextId,
                sessionResponse.SelectionVersion,
                targetContext.EligibilityVersion))
        };
        switchRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrfToken);
        switchRequest.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var switchResponse = await client.SendAsync(switchRequest);
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);

        // 6. Access Price List list endpoint as authenticated Tenant user
        var priceListResponse = await client.GetAsync("/api/v1/master-data/price-lists");
        Assert.Equal(HttpStatusCode.OK, priceListResponse.StatusCode);
    }

    [Fact]
    public async Task DevelopmentBypass_EnabledInDevelopment_UsesDefaultBootstrapLoginAndHumanTenantName()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_AUTH_BYPASS"] = "true",
            ["MESP_DEV_ADMIN_PASSWORD"] = null,
            ["MESP_DEV_TENANT_DISPLAY_NAME"] = "Wafra"
        };

        using var factory = new CustomTestWebApplicationFactory(settings);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        using var bypassRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/development-bypass")
        {
            Content = null
        };
        var bypassResponse = await client.SendAsync(bypassRequest);

        Assert.Equal(HttpStatusCode.OK, bypassResponse.StatusCode);
        var session = await bypassResponse.Content.ReadFromJsonAsync<FoundationSessionResponse>();
        Assert.NotNull(session);
        Assert.True(session.Authenticated);

        var contexts = await client.GetFromJsonAsync<FoundationContextsResponse>("/api/v1/auth/contexts");
        Assert.NotNull(contexts);
        Assert.Single(contexts.Contexts);
        Assert.Equal("Wafra", contexts.Contexts[0].DisplayName);
        Assert.DoesNotContain(DevelopmentBootstrap.DevTenantId.Value.ToString("D"), contexts.Contexts[0].DisplayName, StringComparison.Ordinal);

        var csrfResponse = await client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrfToken = csrfResponse.Headers.GetValues("X-CSRF-TOKEN").Single();
        var initialSession = await client.GetFromJsonAsync<FoundationSessionResponse>("/api/v1/auth/session");
        Assert.NotNull(initialSession);

        using var switchRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(
                contexts.Contexts[0].ContextId,
                initialSession.SelectionVersion,
                contexts.Contexts[0].EligibilityVersion))
        };
        switchRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrfToken);
        switchRequest.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var switchResponse = await client.SendAsync(switchRequest);
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);

        var repeatedBypassResponse = await client.PostAsync("/api/v1/auth/development-bypass", content: null);
        Assert.Equal(HttpStatusCode.OK, repeatedBypassResponse.StatusCode);
        var repeatedSession = await repeatedBypassResponse.Content.ReadFromJsonAsync<FoundationSessionResponse>();
        Assert.NotNull(repeatedSession);
        Assert.Equal(contexts.Contexts[0].ContextId, repeatedSession.SelectedContextId);
        Assert.Equal("OrdinaryMembership", repeatedSession.SelectedPath);
    }

    private sealed class CustomTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> customSettings;
        private SqliteConnection? connection;

        public CustomTestWebApplicationFactory(Dictionary<string, string?> customSettings)
        {
            this.customSettings = customSettings;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(customSettings);
                // The canonical validation command supplies a disposable SQL
                // connection for the SQL safety fixture. These bootstrap
                // tests own an isolated SQLite test store and must not make
                // the application startup migrator race that disposable DB.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MESP_SQLSERVER_CONNECTION_STRING"] = " "
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter, LoopbackRemoteAddressStartupFilter>();
                connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
                using (var db = new MasterDataDbContext(options, TenantContext.ForOrdinaryMembership(DevelopmentBootstrap.DevTenantId, new MembershipReference(Guid.NewGuid()), null, new CorrelationId("bootstrap-init"), Guid.NewGuid())))
                {
                    db.Database.EnsureCreated();
                }

                var persistence = new MasterDataPriceListPersistence(options);
                services.AddSingleton<IMasterDataPriceListPersistence>(persistence);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                connection?.Dispose();
            }
        }
    }

    private static IHostEnvironment CreateHostEnvironment(string environmentName) =>
        new TestHostEnvironment(environmentName);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = typeof(DevelopmentBootstrapTests).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class LoopbackRemoteAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            application =>
            {
                application.Use(async (context, nextRequest) =>
                {
                    context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                    await nextRequest();
                });
                next(application);
            };
    }

    private static HashSet<string> ReadTableNames(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        using var reader = command.ExecuteReader();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
