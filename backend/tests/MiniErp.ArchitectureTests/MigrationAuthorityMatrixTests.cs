using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniErp.Api;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Identity;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationAuthorityMatrixTests : IClassFixture<RestFoundationTests.ApiFactory>
{
    private const string SourceRowSentinel = "MESP171-SOURCE-ROW-SENTINEL";
    private readonly RestFoundationTests.ApiFactory factory;

    public MigrationAuthorityMatrixTests(RestFoundationTests.ApiFactory factory)
    {
        this.factory = factory;
        factory.Reset();
    }

    public static TheoryData<string> MigrationOperationIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var operation in FoundationOperationCatalog.PublicOperations.Where(item =>
                         item.OperationId.StartsWith("migration.", StringComparison.Ordinal)))
                data.Add(operation.OperationId);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MigrationOperationIds))]
    public async Task Migration_operation_denies_a_tenant_membership_without_its_permission(string operationId)
    {
        var operation = GetMigrationOperation(operationId);
        Assert.False(string.IsNullOrWhiteSpace(operation.ExactPermissionCode));
        Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);

        var migrationPermissions = MigrationPermissionCodes();
        var grantedPermissions = migrationPermissions
            .Where(permission => !string.Equals(permission, operation.ExactPermissionCode, StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(grantedPermissions);
        Assert.DoesNotContain(operation.ExactPermissionCode!, grantedPermissions);

        var identity = factory.Services.GetRequiredService<IdentityAuthorizationService>();
        var identityHost = factory.Services.GetRequiredService<IFoundationIdentityHost>();
        var tenantId = new TenantId(Guid.NewGuid());
        var context = CreateResolvedContext(identity, identityHost, tenantId, grantedPermissions, operation);
        Assert.Null(context.TenantContext);
        factory.Resolver.Context = context;

        using var client = factory.CreateClient();
        var requestedId = Guid.NewGuid();
        using var response = await SendAsync(client, operation, requestedId);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("tenant_context_required", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(SourceRowSentinel, body, StringComparison.Ordinal);
        Assert.DoesNotContain(requestedId.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(tenantId.Value.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Migration_operations_hide_foreign_tenant_sources_and_runs()
    {
        using var isolatedFactory = new MigrationIsolationApiFactory();
        var identity = isolatedFactory.Services.GetRequiredService<IdentityAuthorizationService>();
        var identityHost = isolatedFactory.Services.GetRequiredService<IFoundationIdentityHost>();
        var intakeOperation = GetMigrationOperation("migration.intake.create");
        var ownerTenantId = new TenantId(Guid.NewGuid());
        var foreignTenantId = new TenantId(Guid.NewGuid());
        var ownerContext = CreateResolvedContext(identity, identityHost, ownerTenantId,
            [intakeOperation.ExactPermissionCode!], intakeOperation);
        var ownerTenant = Assert.IsType<TenantContext>(ownerContext.TenantContext);
        var ownerSource = await isolatedFactory.Storage.StoreAsync(
            ownerTenant,
            TenantWorkScope.IssueFromVerifiedAuthority(ownerTenant, TenantWorkScopeRequest.TenantWide()),
            "owner-only-source.txt",
            "text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes(SourceRowSentinel)),
            safetyRequirement: PrivateFileSafetyRequirement.TrustedGenerated);

        isolatedFactory.Resolver.Context = ownerContext;
        using var client = isolatedFactory.CreateClient();
        using var intakeResponse = await SendAsync(client, intakeOperation, Guid.NewGuid(), ownerSource.ObjectId);
        var intakeBody = await intakeResponse.Content.ReadAsStringAsync();
        Assert.True(intakeResponse.StatusCode == HttpStatusCode.OK, intakeBody);
        using var intakeDocument = JsonDocument.Parse(intakeBody);
        var ownerRunId = intakeDocument.RootElement.GetProperty("runId").GetGuid();

        foreach (var operation in FoundationOperationCatalog.PublicOperations.Where(item =>
                     item.OperationId.StartsWith("migration.", StringComparison.Ordinal)))
        {
            var foreignContext = CreateResolvedContext(identity, identityHost, foreignTenantId,
                [operation.ExactPermissionCode!], operation);
            isolatedFactory.Resolver.Context = foreignContext;

            using var response = await SendAsync(
                client,
                operation,
                ownerRunId,
                operation.OperationId == "migration.intake.create" ? ownerSource.ObjectId : null);
            var body = await response.Content.ReadAsStringAsync();
            var expected = ExpectedForeignTenantOutcome(operation.OperationId);

            Assert.True(expected.Status == response.StatusCode, operation.OperationId);
            using var document = JsonDocument.Parse(body);
            Assert.True(expected.Code == document.RootElement.GetProperty("code").GetString(), operation.OperationId);
            Assert.DoesNotContain(SourceRowSentinel, body, StringComparison.Ordinal);
            Assert.DoesNotContain(ownerTenantId.Value.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ownerSource.ObjectId.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ownerRunId.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Resource_scoped_reads_distinguish_scope_denial_from_missing_evidence()
    {
        using var isolatedFactory = new MigrationIsolationApiFactory();
        var identity = isolatedFactory.Services.GetRequiredService<IdentityAuthorizationService>();
        var identityHost = isolatedFactory.Services.GetRequiredService<IFoundationIdentityHost>();
        var intakeOperation = GetMigrationOperation("migration.intake.create");
        var tenantId = new TenantId(Guid.NewGuid());
        var sourceCompanyId = Guid.NewGuid();
        var callerCompanyId = Guid.NewGuid();
        var ownerContext = CreateResolvedContext(identity, identityHost, tenantId,
            [intakeOperation.ExactPermissionCode!], intakeOperation);
        var ownerTenant = Assert.IsType<TenantContext>(ownerContext.TenantContext);
        var ownerSource = await isolatedFactory.Storage.StoreAsync(
            ownerTenant,
            TenantWorkScope.IssueFromVerifiedAuthority(ownerTenant, TenantWorkScopeRequest.ForCompany(sourceCompanyId)),
            "scoped-source.txt",
            "text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes(SourceRowSentinel)),
            safetyRequirement: PrivateFileSafetyRequirement.TrustedGenerated);

        isolatedFactory.ScopeResolver.CurrentScope = TenantWorkScopeRequest.ForCompany(sourceCompanyId);
        isolatedFactory.Resolver.Context = ownerContext;
        using var client = isolatedFactory.CreateClient();
        using var intakeResponse = await SendAsync(client, intakeOperation, Guid.NewGuid(), ownerSource.ObjectId);
        var intakeBody = await intakeResponse.Content.ReadAsStringAsync();
        Assert.True(intakeResponse.StatusCode == HttpStatusCode.OK, intakeBody);
        using var intakeDocument = JsonDocument.Parse(intakeBody);
        var runId = intakeDocument.RootElement.GetProperty("runId").GetGuid();

        foreach (var operation in new[]
                 {
                     "migration.validation.read",
                     "migration.validation.findings.read",
                     "migration.staged-records.read",
                     "migration.dry-run.read",
                     "migration.reconciliation-preview.read",
                     "migration.reconciliation.read",
                     "migration.handover.read"
                 }.Select(GetMigrationOperation))
        {
            var outOfScopeContext = CreateResolvedContext(identity, identityHost, tenantId,
                [operation.ExactPermissionCode!], operation);
            isolatedFactory.ScopeResolver.CurrentScope = TenantWorkScopeRequest.ForCompany(callerCompanyId);
            isolatedFactory.Resolver.Context = outOfScopeContext;

            using var denied = await SendAsync(client, operation, runId);
            var deniedBody = await denied.Content.ReadAsStringAsync();
            Assert.True(denied.StatusCode == HttpStatusCode.Forbidden, operation.OperationId);
            using var deniedDocument = JsonDocument.Parse(deniedBody);
            Assert.Equal("migration_source_scope_denied", deniedDocument.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain(SourceRowSentinel, deniedBody, StringComparison.Ordinal);
            Assert.DoesNotContain(sourceCompanyId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ownerSource.ObjectId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(runId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
        }

        var validationRead = GetMigrationOperation("migration.validation.read");
        isolatedFactory.Resolver.Context = CreateResolvedContext(identity, identityHost, tenantId,
            [validationRead.ExactPermissionCode!], validationRead);
        isolatedFactory.ScopeResolver.CurrentScope = TenantWorkScopeRequest.ForCompany(sourceCompanyId);
        using var missingEvidence = await SendAsync(client, validationRead, runId);
        var missingEvidenceBody = await missingEvidence.Content.ReadAsStringAsync();
        Assert.True(missingEvidence.StatusCode == HttpStatusCode.NotFound, missingEvidenceBody);
        using var missingEvidenceDocument = JsonDocument.Parse(missingEvidenceBody);
        Assert.Equal("migration_validation_not_found", missingEvidenceDocument.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(ownerSource.ObjectId.ToString("D"), missingEvidenceBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(runId.ToString("D"), missingEvidenceBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reconciliation_report_exposes_utc_freshness_and_approval_ownership()
    {
        var requestTime = DateTimeOffset.UtcNow;
        var createdAt = requestTime.AddMinutes(-1);
        var calculatedAt = requestTime.AddSeconds(-1);
        var tenantId = new TenantId(Guid.NewGuid());
        var runId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var requirement = new MigrationReconciliationApprovalRequirement("migration", "opening", 1, [actorId]);
        var approval = new MigrationReconciliationApprovalRecord(
            Guid.NewGuid(), tenantId, runId, reconciliationId, 1, attemptId,
            "evidence-fingerprint", "approval-key", "migration", "opening", "test-policy", 1,
            actorId, MigrationApprovalDecision.Approved, "reviewed", calculatedAt, [1], EvidenceConfirmed: true);
        var reconciliation = new MigrationReconciliationRecord(
            reconciliationId, tenantId, runId, attemptId, 1, "evidence-fingerprint", "reconciliation-key",
            MigrationReconciliationStatus.Reconciled, createdAt, calculatedAt, 1, 1, 0, 0, 0, 0, 0, 1, 1,
            10m, 0m, 10m, 0m, 0m, [1], IsCurrent: true, "test-policy", 1, "test-policy-v1",
            createdAt, null, ApprovalEnforcesSeparationOfDuties: true, [requirement], [], [approval], null);
        var mapper = typeof(MigrationEndpoints).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "ToResponse"
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(MigrationReconciliationRecord));
        var projected = Assert.IsType<MigrationReconciliationResponse>(mapper.Invoke(null, [reconciliation]));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(projected, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var report = document.RootElement;

        var reportedCreatedAt = report.GetProperty("createdAt").GetDateTimeOffset();
        var reportedCalculatedAt = report.GetProperty("calculatedAt").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, reportedCreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, reportedCalculatedAt.Offset);
        Assert.True(reportedCreatedAt <= requestTime);
        Assert.True(reportedCalculatedAt <= requestTime);
        Assert.Equal(tenantId.Value, report.GetProperty("tenantId").GetGuid());
        Assert.Equal(runId, report.GetProperty("runId").GetGuid());
        Assert.Equal(1, report.GetProperty("submittedCount").GetInt32());
        var reportedApproval = Assert.Single(report.GetProperty("approvals").EnumerateArray());
        Assert.Equal(actorId, reportedApproval.GetProperty("actorId").GetGuid());
        Assert.Equal("opening", reportedApproval.GetProperty("requirementKey").GetString());
    }

    private static FoundationOperationDescriptor GetMigrationOperation(string operationId) =>
        Assert.Single(FoundationOperationCatalog.PublicOperations, item =>
            item.OperationId.StartsWith("migration.", StringComparison.Ordinal)
            && string.Equals(item.OperationId, operationId, StringComparison.Ordinal));

    private static string[] MigrationPermissionCodes() => FoundationOperationCatalog.PublicOperations
        .Where(item => item.OperationId.StartsWith("migration.", StringComparison.Ordinal))
        .Select(item => item.ExactPermissionCode)
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Distinct(StringComparer.Ordinal)
        .Select(code => code!)
        .ToArray();

    private static (HttpStatusCode Status, string Code) ExpectedForeignTenantOutcome(string operationId) => operationId switch
    {
        "migration.intake.create" => (HttpStatusCode.NotFound, "migration_source_not_found"),
        "migration.validation.read" or
        "migration.validation.findings.read" or
        "migration.staged-records.read" or
        "migration.dry-run.read" or
        "migration.reconciliation-preview.read" or
        "migration.reconciliation.calculate" or
        "migration.reconciliation.read" or
        "migration.reconciliation.approve" or
        "migration.handover.ready" or
        "migration.handover.read" => (HttpStatusCode.Forbidden, "migration_source_scope_denied"),
        "migration.dry-run.start" => (HttpStatusCode.Conflict, "migration_validation_required"),
        "migration.validation.start" or
        "migration.validation.corrected-retry" or
        "migration.run.cancel" or
        "migration.execution.start" => (HttpStatusCode.NotFound, "migration_run_not_found"),
        "migration.preview.read" => (HttpStatusCode.NotFound, "migration_preview_not_found"),
        "migration.execution.read" => (HttpStatusCode.NotFound, "migration_execution_not_found"),
        _ => throw new Xunit.Sdk.XunitException($"No foreign-tenant expectation is defined for {operationId}.")
    };

    private static FoundationRequestContext CreateResolvedContext(
        IdentityAuthorizationService identity,
        IFoundationIdentityHost identityHost,
        TenantId tenantId,
        IReadOnlyCollection<string> permissionCodes,
        FoundationOperationDescriptor operation)
    {
        var password = Guid.NewGuid().ToString("N") + "A1!";
        var email = $"matrix-{Guid.NewGuid():N}@example.test";
        var user = identity.CreateUser(email, password, mfaEnabled: false);
        var approver = identity.CreateUser($"approver-{Guid.NewGuid():N}@example.test", password, mfaEnabled: false);
        var membership = identity.AddMembership(user, tenantId);
        var role = identity.CreateRole(
            $"migration-matrix-{Guid.NewGuid():N}",
            tenantId,
            platformOwned: false,
            permissionCodes.Select(ResolvePermission).Append(IdentityPermissions.ContextRead));
        identity.Store.RoleAssignments[membership].Add(new RoleAssignment(role, membership, user, tenantId, approver));

        var scopeGrantId = new ScopeGrantId(Guid.NewGuid());
        identity.Store.ScopeGrants.Add(
            scopeGrantId,
            new AccessScopeGrant(scopeGrantId, membership, user, OrganizationScope.ForTenant(tenantId), approver));
        identity.Store.ScopeGrantsByMembership[membership].Add(scopeGrantId);

        var signIn = identityHost.SignIn(email, password);
        Assert.True(signIn.Succeeded, signIn.Code);
        Assert.NotNull(signIn.Principal);
        var candidate = Assert.Single(identityHost.ListContexts(signIn.Principal!));
        Assert.Equal(tenantId.Value, candidate.TenantId);
        Assert.True(identityHost.SelectAuthorizedContext(signIn.Principal!, candidate.ContextId));

        return identityHost.ResolveContext(signIn.Principal!, "mesp171-authority-matrix", operation);
    }

    private static PermissionCode ResolvePermission(string code)
    {
        Assert.True(IdentityPermissions.TryResolve(code, out var permission), $"Unresolvable catalogue permission: {code}");
        return permission;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        FoundationOperationDescriptor operation,
        Guid requestedId,
        Guid? sourceObjectId = null)
    {
        var path = operation.Route
            .Replace("{runId:guid}", requestedId.ToString("D"), StringComparison.Ordinal)
            .Replace("{reconciliationId:guid}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(new HttpMethod(operation.HttpMethod), path);

        if (operation.HttpMethod == "POST")
        {
            request.Content = JsonContent.Create(RequestBody(operation.OperationId, sourceObjectId));
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
            request.Headers.TryAddWithoutValidation("If-Match", "\"AQ==\"");
        }

        if (operation.RequiresAntiforgery)
        {
            using var tokenResponse = await client.GetAsync("/api/v1/auth/antiforgery");
            Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", tokenResponse.Headers.GetValues("X-CSRF-TOKEN").Single());
        }

        return await client.SendAsync(request);
    }

    private static object RequestBody(string operationId, Guid? sourceObjectId = null) => operationId switch
    {
        "migration.intake.create" => new
        {
            definitionId = "tenant-onboarding.foundation",
            definitionVersion = "1",
            sourceProfileId = "neutral-source-profile",
            sourceProfileVersion = "1",
            operation = 1,
            sourceObjectId = sourceObjectId ?? Guid.NewGuid()
        },
        "migration.validation.corrected-retry" => new
        {
            corrections = new[]
            {
                new
                {
                    stagedRecordId = Guid.NewGuid(),
                    correctedPayload = SourceRowSentinel,
                    correctionOwner = SourceRowSentinel
                }
            }
        },
        "migration.run.cancel" => new { reason = SourceRowSentinel },
        "migration.reconciliation.approve" => new
        {
            domain = "migration",
            requirementKey = "authority-matrix",
            decision = 0,
            reason = SourceRowSentinel
        },
        _ => new { }
    };

    private sealed class MigrationIsolationApiFactory : WebApplicationFactory<Program>
    {
        private SqliteConnection? connection;

        internal readonly RestFoundationTests.TestResolver Resolver = new();
        internal readonly MigrationCurrentScopeResolver ScopeResolver = new();
        internal InMemoryPrivateObjectStorage Storage { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MESP_SQLSERVER_CONNECTION_STRING"] = " ",
                    ["MESP_DEV_MASTERDATA_SQLITE_CONNECTION_STRING"] = "Data Source=:memory:",
                    ["MESP_DEV_BUSINESS_PARTIES_SQLITE_CONNECTION_STRING"] = "Data Source=:memory:",
                    ["MESP_DEV_PROCUREMENT_SQLITE_CONNECTION_STRING"] = "Data Source=:memory:",
                    ["MESP_DEV_INVENTORY_SQLITE_CONNECTION_STRING"] = "Data Source=:memory:",
                    ["MESP_DEV_FINANCE_SQLITE_CONNECTION_STRING"] = "Data Source=:memory:",
                    ["MESP_DEV_SALES_SQLITE_CONNECTION_STRING"] = "Data Source=:memory:",
                    ["MESP_DEV_MIGRATION_SQLITE_CONNECTION_STRING"] = "Data Source=:memory:"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITrustedRequestContextResolver>();
                services.AddSingleton<ITrustedRequestContextResolver>(Resolver);
                services.RemoveAll<ICurrentOrganizationScopeResolver>();
                services.AddSingleton<ICurrentOrganizationScopeResolver>(ScopeResolver);
                services.RemoveAll<IOrganizationScopeOwnershipResolver>();
                services.AddSingleton<IOrganizationScopeOwnershipResolver>(ScopeResolver);
                services.RemoveAll<IPrivateObjectStorage>();
                services.AddSingleton<IPrivateObjectStorage>(Storage);

                connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
                var schemaTenant = TenantContext.ForOrdinaryMembership(
                    new TenantId(Guid.NewGuid()),
                    new MembershipReference(Guid.NewGuid()),
                    correlationId: new CorrelationId("authority-matrix-schema"),
                    actorId: Guid.NewGuid());
                using (var db = new MigrationDbContext(options, schemaTenant))
                    db.Database.EnsureCreated();

                services.RemoveAll<MigrationPersistence>();
                services.RemoveAll<IMigrationFoundationPersistence>();
                services.RemoveAll<IMigrationValidationPersistence>();
                services.RemoveAll<IMigrationExecutionPersistence>();
                services.RemoveAll<IMigrationReconciliationPersistence>();
                services.AddMigrationPersistence(options => options.UseSqlite(connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                connection?.Dispose();
        }
    }

    private sealed class MigrationCurrentScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        internal TenantWorkScopeRequest CurrentScope { get; set; } = TenantWorkScopeRequest.TenantWide();

        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(
                TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, CurrentScope));

        public TenantWorkScopeResolution Resolve(
            TenantContext trustedTenantContext,
            TenantWorkScopeRequest requestedScope) =>
            TenantWorkScopeResolution.Resolved(
                TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }
}
