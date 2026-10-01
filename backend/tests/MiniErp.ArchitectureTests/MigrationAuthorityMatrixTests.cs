using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using MiniErp.Contracts.Modules.Migration;
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
    public async Task Validation_only_membership_can_read_evidence_but_cannot_use_execution_authority()
    {
        using var isolatedFactory = new MigrationIsolationApiFactory();
        var identity = isolatedFactory.Services.GetRequiredService<IdentityAuthorizationService>();
        var identityHost = isolatedFactory.Services.GetRequiredService<IFoundationIdentityHost>();
        var tenantId = new TenantId(Guid.NewGuid());
        const string validationPermission = "tenant.migration.intake";
        const string executionPermission = "tenant.migration.execute";
        var principal = CreateTenantPrincipal(identity, identityHost, tenantId, [validationPermission]);
        Assert.Contains(validationPermission, MigrationPermissionCodes());
        Assert.Contains(executionPermission, MigrationPermissionCodes());
        var tenantWideOperation = GetMigrationOperation("migration.intake.create");
        var tenant = Assert.IsType<TenantContext>(identityHost.ResolveContext(
            principal, "mesp171-validation-only", tenantWideOperation).TenantContext);
        var sourceObjectId = Guid.NewGuid();
        var package = ValidationPackage(sourceObjectId);
        isolatedFactory.Storage.RegisterValidationPackage(tenant, sourceObjectId, package);
        using var client = isolatedFactory.CreateClient();

        isolatedFactory.Resolver.Context = identityHost.ResolveContext(principal, "mesp171-validation-only", tenantWideOperation);
        using var intakeResponse = await SendAsync(client, tenantWideOperation, Guid.NewGuid(), sourceObjectId);
        var intakeBody = await intakeResponse.Content.ReadAsStringAsync();
        Assert.True(intakeResponse.StatusCode == HttpStatusCode.OK, intakeBody);
        using var intakeDocument = JsonDocument.Parse(intakeBody);
        var runId = intakeDocument.RootElement.GetProperty("runId").GetGuid();

        var validationStart = GetMigrationOperation("migration.validation.start");
        isolatedFactory.Resolver.Context = identityHost.ResolveContext(principal, "mesp171-validation-only", validationStart);
        using var validationResponse = await SendAsync(client, validationStart, runId);
        var validationBody = await validationResponse.Content.ReadAsStringAsync();
        Assert.True(validationResponse.StatusCode == HttpStatusCode.OK, validationBody);

        var baseline = await isolatedFactory.ReadRunStateAsync(tenant, runId);
        var deniedOperations = FoundationOperationCatalog.PublicOperations.Where(operation =>
            operation.OperationId.StartsWith("migration.", StringComparison.Ordinal)
            && string.Equals(operation.ExactPermissionCode, executionPermission, StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(deniedOperations);
        Assert.Contains(deniedOperations, operation => operation.OperationId == "migration.execution.start");
        Assert.Contains(deniedOperations, operation => operation.OperationId == "migration.reconciliation.approve");
        Assert.Contains(deniedOperations, operation => operation.OperationId == "migration.handover.ready");

        // BRD M40-AC-029 separates validation permission from execution/approval authority; intake and validation mutations share the validation permission.
        foreach (var operation in deniedOperations)
        {
            isolatedFactory.Resolver.Context = identityHost.ResolveContext(principal, "mesp171-validation-only", operation);
            using var denied = await SendAsync(client, operation, runId);
            var deniedBody = await denied.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var deniedDocument = JsonDocument.Parse(deniedBody);
            Assert.Equal("tenant_context_required", deniedDocument.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain(runId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sourceObjectId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(tenantId.Value.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
        }

        // SQLite cannot translate the DateTimeOffset ORDER BY in ListFindingsAsync; Sql_server_mesp171_validation_findings_read_is_tenant_scoped covers this read on LocalDB.
        foreach (var operationId in new[] { "migration.validation.read", "migration.staged-records.read" })
        {
            var operation = GetMigrationOperation(operationId);
            isolatedFactory.Resolver.Context = identityHost.ResolveContext(principal, "mesp171-validation-only", operation);
            using var evidence = await SendAsync(client, operation, runId);
            var evidenceBody = await evidence.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, evidence.StatusCode);
            Assert.DoesNotContain(SourceRowSentinel, evidenceBody, StringComparison.Ordinal);
        }

        var foreignTenantId = new TenantId(Guid.NewGuid());
        var foreignPrincipal = CreateTenantPrincipal(identity, identityHost, foreignTenantId, [validationPermission]);
        var foreignTenant = Assert.IsType<TenantContext>(identityHost.ResolveContext(
            foreignPrincipal, "mesp171-validation-only-foreign", tenantWideOperation).TenantContext);
        var foreignSourceId = Guid.NewGuid();
        isolatedFactory.Storage.RegisterValidationPackage(foreignTenant, foreignSourceId, ValidationPackage(foreignSourceId));
        isolatedFactory.Resolver.Context = identityHost.ResolveContext(
            foreignPrincipal, "mesp171-validation-only-foreign", tenantWideOperation);
        using var foreignIntake = await SendAsync(client, tenantWideOperation, Guid.NewGuid(), foreignSourceId);
        var foreignIntakeBody = await foreignIntake.Content.ReadAsStringAsync();
        Assert.True(foreignIntake.StatusCode == HttpStatusCode.OK, foreignIntakeBody);
        using var foreignIntakeDocument = JsonDocument.Parse(foreignIntakeBody);
        var foreignRunId = foreignIntakeDocument.RootElement.GetProperty("runId").GetGuid();

        var foreignSnapshots = new Dictionary<Guid, MigrationRunSnapshot>
        {
            [runId] = baseline,
            [foreignRunId] = await isolatedFactory.ReadRunStateAsync(foreignTenant, foreignRunId)
        };
        foreach (var operationId in new[]
                 {
                     "migration.validation.read",
                     "migration.validation.findings.read",
                     "migration.staged-records.read"
                 })
        {
            var operation = GetMigrationOperation(operationId);
            isolatedFactory.Resolver.Context = identityHost.ResolveContext(principal, "mesp171-validation-only", operation);
            using var hidden = await SendAsync(client, operation, foreignRunId);
            var hiddenBody = await hidden.Content.ReadAsStringAsync();
            var expected = ExpectedForeignTenantOutcome(operationId);
            Assert.Equal(expected.Status, hidden.StatusCode);
            using var hiddenDocument = JsonDocument.Parse(hiddenBody);
            Assert.Equal(expected.Code, hiddenDocument.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain(foreignRunId.ToString("D"), hiddenBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(foreignSourceId.ToString("D"), hiddenBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(foreignTenantId.Value.ToString("D"), hiddenBody, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(baseline, await isolatedFactory.ReadRunStateAsync(tenant, runId));
        Assert.Equal(foreignSnapshots[foreignRunId], await isolatedFactory.ReadRunStateAsync(foreignTenant, foreignRunId));
    }

    [Fact]
    public async Task Resource_scoped_operations_deny_company_scope_without_changing_run_state()
    {
        using var isolatedFactory = new MigrationIsolationApiFactory();
        var identity = isolatedFactory.Services.GetRequiredService<IdentityAuthorizationService>();
        var identityHost = isolatedFactory.Services.GetRequiredService<IFoundationIdentityHost>();
        var intakeOperation = GetMigrationOperation("migration.intake.create");
        var tenantId = new TenantId(Guid.NewGuid());
        var sourceCompanyId = Guid.NewGuid();
        var callerCompanyId = Guid.NewGuid();
        var ownerPrincipal = CreateTenantPrincipal(identity, identityHost, tenantId, [intakeOperation.ExactPermissionCode!]);
        var ownerContext = identityHost.ResolveContext(ownerPrincipal, "mesp171-company-scope-owner", intakeOperation);
        var ownerTenant = Assert.IsType<TenantContext>(ownerContext.TenantContext);
        var sourceObjectId = Guid.NewGuid();
        isolatedFactory.Storage.RegisterValidationPackage(ownerTenant, sourceObjectId,
            ValidationPackage(sourceObjectId), sourceCompanyId);

        isolatedFactory.ScopeResolver.CurrentScope = TenantWorkScopeRequest.ForCompany(sourceCompanyId);
        isolatedFactory.Resolver.Context = ownerContext;
        using var client = isolatedFactory.CreateClient();
        using var intakeResponse = await SendAsync(client, intakeOperation, Guid.NewGuid(), sourceObjectId);
        var intakeBody = await intakeResponse.Content.ReadAsStringAsync();
        Assert.True(intakeResponse.StatusCode == HttpStatusCode.OK, intakeBody);
        using var intakeDocument = JsonDocument.Parse(intakeBody);
        var runId = intakeDocument.RootElement.GetProperty("runId").GetGuid();

        var validationStart = GetMigrationOperation("migration.validation.start");
        isolatedFactory.Resolver.Context = identityHost.ResolveContext(ownerPrincipal, "mesp171-company-scope-owner", validationStart);
        using var validationResponse = await SendAsync(client, validationStart, runId);
        var validationBody = await validationResponse.Content.ReadAsStringAsync();
        Assert.True(validationResponse.StatusCode == HttpStatusCode.OK, validationBody);
        var dryRunStart = GetMigrationOperation("migration.dry-run.start");
        isolatedFactory.Resolver.Context = identityHost.ResolveContext(ownerPrincipal, "mesp171-company-scope-owner", dryRunStart);
        using var dryRunResponse = await SendAsync(client, dryRunStart, runId);
        var dryRunBody = await dryRunResponse.Content.ReadAsStringAsync();
        Assert.True(dryRunResponse.StatusCode == HttpStatusCode.OK, dryRunBody);

        var baseline = await isolatedFactory.ReadRunStateAsync(ownerTenant, runId);
        var resourceScopedOperations = FoundationOperationCatalog.PublicOperations.Where(operation =>
            operation.OperationId.StartsWith("migration.", StringComparison.Ordinal)
            && (operation.IsUnsafe || Regex.IsMatch(operation.Route, @"\{[^}]+:guid\}", RegexOptions.CultureInvariant))).ToArray();
        Assert.NotEmpty(resourceScopedOperations);
        Assert.Contains(resourceScopedOperations, operation => operation.OperationId == "migration.intake.create");
        var hiddenCompanyScopeDenialCodes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["migration.execution.read"] = "migration_execution_not_found",
            ["migration.preview.read"] = "migration_preview_not_found"
        };
        Assert.Equal(
            new[] { "migration.execution.read", "migration.preview.read" },
            hiddenCompanyScopeDenialCodes.Keys.OrderBy(operationId => operationId, StringComparer.Ordinal));

        var previewRead = GetMigrationOperation("migration.preview.read");
        isolatedFactory.ScopeResolver.CurrentScope = TenantWorkScopeRequest.ForCompany(sourceCompanyId);
        isolatedFactory.Resolver.Context = identityHost.ResolveContext(ownerPrincipal, "mesp171-company-scope-owner", previewRead);
        using (var allowedPreview = await SendAsync(client, previewRead, runId))
        {
            var allowedPreviewBody = await allowedPreview.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, allowedPreview.StatusCode);
            using var allowedPreviewDocument = JsonDocument.Parse(allowedPreviewBody);
            Assert.Equal(runId, allowedPreviewDocument.RootElement.GetProperty("runId").GetGuid());
            Assert.Equal(tenantId.Value, allowedPreviewDocument.RootElement.GetProperty("tenantId").GetGuid());
        }
        Assert.Equal(baseline, await isolatedFactory.ReadRunStateAsync(ownerTenant, runId));

        foreach (var operation in resourceScopedOperations)
        {
            var outOfScopeContext = CreateResolvedContext(identity, identityHost, tenantId,
                [operation.ExactPermissionCode!], operation);
            isolatedFactory.ScopeResolver.CurrentScope = TenantWorkScopeRequest.ForCompany(callerCompanyId);
            isolatedFactory.Resolver.Context = outOfScopeContext;

            using var denied = await SendAsync(client, operation, runId,
                operation.OperationId == "migration.intake.create" ? sourceObjectId : null);
            var deniedBody = await denied.Content.ReadAsStringAsync();
            using var deniedDocument = JsonDocument.Parse(deniedBody);
            if (hiddenCompanyScopeDenialCodes.TryGetValue(operation.OperationId, out var hiddenCode))
            {
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                Assert.Equal(hiddenCode, deniedDocument.RootElement.GetProperty("code").GetString());
                // ReadDryRunAsync and MigrationExecutionService.ReadAsync hide scoped denials; MESP-183 (#324) must update both assertions.
                var responseType = operation.OperationId switch
                {
                    "migration.preview.read" => typeof(MigrationNonAuthoritativePreviewResponse),
                    "migration.execution.read" => typeof(MigrationExecutionResponse),
                    _ => throw new Xunit.Sdk.XunitException($"No hidden response contract is defined for {operation.OperationId}.")
                };
                foreach (var property in responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Assert.False(deniedDocument.RootElement.TryGetProperty(JsonNamingPolicy.CamelCase.ConvertName(property.Name), out _),
                        $"{operation.OperationId} response field '{property.Name}' was disclosed.");
            }
            else
            {
                Assert.True(denied.StatusCode == HttpStatusCode.Forbidden, operation.OperationId);
                Assert.Equal("migration_source_scope_denied", deniedDocument.RootElement.GetProperty("code").GetString());
            }
            Assert.DoesNotContain(SourceRowSentinel, deniedBody, StringComparison.Ordinal);
            Assert.DoesNotContain(sourceCompanyId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sourceObjectId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(runId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(tenantId.Value.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(callerCompanyId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(baseline, await isolatedFactory.ReadRunStateAsync(ownerTenant, runId));
        }

        var missingEvidenceSourceObjectId = Guid.NewGuid();
        isolatedFactory.Storage.RegisterValidationPackage(ownerTenant, missingEvidenceSourceObjectId,
            ValidationPackage(missingEvidenceSourceObjectId), sourceCompanyId);
        isolatedFactory.ScopeResolver.CurrentScope = TenantWorkScopeRequest.ForCompany(sourceCompanyId);
        isolatedFactory.Resolver.Context = ownerContext;
        using var missingEvidenceIntake = await SendAsync(client, intakeOperation, Guid.NewGuid(), missingEvidenceSourceObjectId);
        var missingEvidenceIntakeBody = await missingEvidenceIntake.Content.ReadAsStringAsync();
        Assert.True(missingEvidenceIntake.StatusCode == HttpStatusCode.OK, missingEvidenceIntakeBody);
        using var missingEvidenceIntakeDocument = JsonDocument.Parse(missingEvidenceIntakeBody);
        var missingEvidenceRunId = missingEvidenceIntakeDocument.RootElement.GetProperty("runId").GetGuid();
        var originalRunAfterSecondIntake = await isolatedFactory.ReadRunStateAsync(ownerTenant, runId);
        Assert.Equal(baseline.Status, originalRunAfterSecondIntake.Status);
        Assert.Equal(baseline.Version, originalRunAfterSecondIntake.Version);
        var missingEvidenceBaseline = await isolatedFactory.ReadRunStateAsync(ownerTenant, missingEvidenceRunId);

        var validationRead = GetMigrationOperation("migration.validation.read");
        isolatedFactory.Resolver.Context = identityHost.ResolveContext(ownerPrincipal, "mesp171-company-scope-owner", validationRead);
        using var missingEvidence = await SendAsync(client, validationRead, missingEvidenceRunId);
        var missingEvidenceBody = await missingEvidence.Content.ReadAsStringAsync();
        Assert.True(missingEvidence.StatusCode == HttpStatusCode.NotFound, missingEvidenceBody);
        using var missingEvidenceDocument = JsonDocument.Parse(missingEvidenceBody);
        Assert.Equal("migration_validation_not_found", missingEvidenceDocument.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(missingEvidenceSourceObjectId.ToString("D"), missingEvidenceBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(missingEvidenceRunId.ToString("D"), missingEvidenceBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(tenantId.Value.ToString("D"), missingEvidenceBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(missingEvidenceBaseline, await isolatedFactory.ReadRunStateAsync(ownerTenant, missingEvidenceRunId));
        Assert.Equal(originalRunAfterSecondIntake, await isolatedFactory.ReadRunStateAsync(ownerTenant, runId));
    }

    [Fact]
    public void Reconciliation_response_mapper_preserves_contract_fields_with_realistic_readiness_flags()
    {
        var requestTime = DateTimeOffset.UtcNow;
        var createdAt = requestTime.AddMinutes(-1);
        var calculatedAt = requestTime.AddSeconds(-1);
        var tenantId = new TenantId(Guid.NewGuid());
        var runId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var requirement = new MigrationReconciliationApprovalRequirement("migration", "opening", 1, [actorId]);
        var detail = new MigrationReconciliationDetail(
            Guid.NewGuid(), MigrationReconciliationDomain.Inventory, $"company:{companyId:N}", companyId,
            new DateOnly(2026, 9, 1), "USD", "USD", "USD", 1,
            10m, 2m, 9m, 1m, 2m, 10m, 9m, 1m, 1m, 10m, 9m, 9m, 9m,
            Guid.NewGuid(), Guid.NewGuid(), 1, 1.25m, 10m, 9m, 1m,
            Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "migration-inventory-opening.v1", "opening", Guid.NewGuid(), 1, 2, "AwayFromZero", true,
            "inventory_variance", "seeded report detail", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var approval = new MigrationReconciliationApprovalRecord(
            Guid.NewGuid(), tenantId, runId, reconciliationId, 1, attemptId,
            "evidence-fingerprint", "approval-key", "migration", "opening", "test-policy", 1,
            actorId, MigrationApprovalDecision.Approved, "reviewed", calculatedAt, [1], EvidenceConfirmed: true);
        var readiness = new MigrationHandoverReadinessSnapshot(
            Guid.NewGuid(), tenantId, runId, reconciliationId, 1, attemptId, "evidence-fingerprint", "readiness-key",
            calculatedAt, true, false, false, false, false, "ready_for_handover", [1]);
        var reconciliation = new MigrationReconciliationRecord(
            reconciliationId, tenantId, runId, attemptId, 1, "evidence-fingerprint", "reconciliation-key",
            MigrationReconciliationStatus.Reconciled, createdAt, calculatedAt, 6, 1, 1, 1, 1, 1, 1, 1, 1,
            10m, 2m, 9m, 1m, 2m, [1, 2], IsCurrent: true, "test-policy", 1, "test-policy-v1",
            createdAt, calculatedAt.AddDays(1), ApprovalEnforcesSeparationOfDuties: true,
            [requirement], [detail], [approval], readiness);
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
        Assert.Equal(6, report.GetProperty("submittedCount").GetInt32());
        var reportedApproval = Assert.Single(report.GetProperty("approvals").EnumerateArray());
        Assert.Equal(actorId, reportedApproval.GetProperty("actorId").GetGuid());
        Assert.Equal("opening", reportedApproval.GetProperty("requirementKey").GetString());
        var reportedReadiness = report.GetProperty("readiness");
        Assert.True(reportedReadiness.GetProperty("businessReady").GetBoolean());
        Assert.False(reportedReadiness.GetProperty("productionReady").GetBoolean());
        Assert.False(reportedReadiness.GetProperty("mesp48Complete").GetBoolean());
        Assert.False(reportedReadiness.GetProperty("mesp50Complete").GetBoolean());
        Assert.False(reportedReadiness.GetProperty("tenantActivationPerformed").GetBoolean());
        Assert.Equal("ready_for_handover", reportedReadiness.GetProperty("resultCode").GetString());

        // No exclusions: this mapper input populates every response field and uses a producible readiness snapshot.
        var excludedProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in typeof(MigrationReconciliationResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (excludedProperties.ContainsKey(property.Name))
                continue;
            var value = property.GetValue(projected);
            Assert.False(IsNullOrDefault(value, property.PropertyType),
                $"Reconciliation report property '{property.Name}' is null, empty, or default.");
        }
    }

    private static bool IsNullOrDefault(object? value, Type propertyType)
    {
        if (value is null)
            return true;
        if (value is string text)
            return string.IsNullOrWhiteSpace(text);
        if (value is System.Collections.IEnumerable sequence)
            return !sequence.Cast<object?>().Any();
        var effectiveType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return effectiveType.IsValueType && value.Equals(Activator.CreateInstance(effectiveType));
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
        => identityHost.ResolveContext(
            CreateTenantPrincipal(identity, identityHost, tenantId, permissionCodes),
            "mesp171-authority-matrix",
            operation);

    private static ClaimsPrincipal CreateTenantPrincipal(
        IdentityAuthorizationService identity,
        IFoundationIdentityHost identityHost,
        TenantId tenantId,
        IReadOnlyCollection<string> permissionCodes)
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

        return signIn.Principal!;
    }

    private static byte[] ValidationPackage(Guid sourceObjectId)
    {
        var definition = new MigrationDefinitionReference("tenant-onboarding.foundation", "1");
        var profile = new MigrationSourceProfileReference("neutral-source-profile", "1");
        var content = ValidationPackage(sourceObjectId, definition, profile, 0);
        for (var pass = 0; pass < 5; pass++)
        {
            var next = ValidationPackage(sourceObjectId, definition, profile, content.Length);
            if (next.Length == content.Length)
                return next;
            content = next;
        }
        return content;
    }

    private static byte[] ValidationPackage(
        Guid sourceObjectId,
        MigrationDefinitionReference definition,
        MigrationSourceProfileReference profile,
        int length) => JsonSerializer.SerializeToUtf8Bytes(new
        {
            PackageVersion = MigrationCanonicalPackageParser.Version,
            DefinitionId = definition.DefinitionId,
            DefinitionVersion = definition.Version,
            SourceProfileId = profile.ProfileId,
            SourceProfileVersion = profile.ProfileVersion,
            LogicalDataset = "authority-matrix",
            SourceSnapshot = new { ObjectId = sourceObjectId, Sha256 = new string('A', 64), Length = length, ConcurrencyVersion = 1 },
            DomainContracts = MigrationDomainContractTestData.For(MigrationCanonicalRecordType.Product),
            Records = new[]
            {
                new { SourceSequence = 1, SourceRecordId = "matrix-product-1", RecordType = "Product", Payload = new { Sku = "MESP171-MATRIX-1", NameEnglish = "Matrix product 1" } },
                new { SourceSequence = 2, SourceRecordId = "matrix-product-2", RecordType = "Product", Payload = new { Sku = "MESP171-MATRIX-2", NameEnglish = "Matrix product 2" } }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

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
        internal MigrationTestObjectStorage Storage { get; } = new();
        private DbContextOptions Options { get; set; } = null!;

        internal async Task<MigrationRunSnapshot> ReadRunStateAsync(TenantContext tenant, Guid runId)
        {
            await using var db = new MigrationDbContext(Options, tenant);
            var run = await db.Runs.AsNoTracking().SingleAsync(item => item.TenantId == tenant.TenantId && item.RunId == runId);
            var runCount = await db.Runs.CountAsync(item => item.TenantId == tenant.TenantId);
            return new MigrationRunSnapshot(run.Status, Convert.ToBase64String(run.Version), runCount);
        }

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
                Options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
                var schemaTenant = TenantContext.ForOrdinaryMembership(
                    new TenantId(Guid.NewGuid()),
                    new MembershipReference(Guid.NewGuid()),
                    correlationId: new CorrelationId("authority-matrix-schema"),
                    actorId: Guid.NewGuid());
                using (var db = new MigrationDbContext(Options, schemaTenant))
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

    private sealed record MigrationRunSnapshot(MigrationRunStatus Status, string Version, int TenantRunCount);

    private sealed class MigrationTestObjectStorage : IPrivateObjectStorage
    {
        private readonly InMemoryPrivateObjectStorage inner = new();
        private readonly Dictionary<(Guid TenantId, Guid ObjectId), (PrivateFileMetadata Metadata, byte[] Content)> packages = [];

        internal void RegisterValidationPackage(TenantContext tenant, Guid objectId, byte[] content, Guid? companyId = null)
        {
            var scopeRequest = companyId is { } company
                ? TenantWorkScopeRequest.ForCompany(company)
                : TenantWorkScopeRequest.TenantWide();
            var scope = TenantWorkScope.IssueFromVerifiedAuthority(tenant, scopeRequest);
            var snapshot = new MigrationSourceArtifactSnapshot(objectId, tenant.TenantId, companyId, null, null,
                new string('A', 64), content.Length, 1);
            var metadata = new PrivateFileMetadata(objectId, tenant.TenantId, scope, "migration.json", "application/json",
                content.Length, snapshot.Sha256, DateTimeOffset.UnixEpoch, null, PrivateFileSafetyRequirement.TrustedGenerated);
            packages[(tenant.TenantId.Value, objectId)] = (metadata, content);
        }

        public ValueTask<PrivateFileMetadata> StoreAsync(
            TenantContext tenantContext,
            TenantWorkScope scope,
            string originalFileName,
            string contentType,
            Stream content,
            DateTimeOffset? expiresAt = null,
            PrivateFileSafetyRequirement safetyRequirement = PrivateFileSafetyRequirement.ExternalScanRequired,
            CancellationToken cancellationToken = default) =>
            inner.StoreAsync(tenantContext, scope, originalFileName, contentType, content, expiresAt, safetyRequirement, cancellationToken);

        public ValueTask<PrivateFileAccessResult> ReadAsync(
            TenantContext tenantContext,
            Guid objectId,
            CancellationToken cancellationToken = default) =>
            packages.TryGetValue((tenantContext.TenantId.Value, objectId), out var package)
                ? ValueTask.FromResult(PrivateFileAccessResult.AllowedResult(package.Metadata, package.Content))
                : inner.ReadAsync(tenantContext, objectId, cancellationToken);

        public ValueTask<PrivateFileOverwriteResult> OverwriteAsync(
            TenantContext tenantContext,
            Guid objectId,
            long expectedConcurrencyVersion,
            Stream content,
            CancellationToken cancellationToken = default) =>
            inner.OverwriteAsync(tenantContext, objectId, expectedConcurrencyVersion, content, cancellationToken);
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
