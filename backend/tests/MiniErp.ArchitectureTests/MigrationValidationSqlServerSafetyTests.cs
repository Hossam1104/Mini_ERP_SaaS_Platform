using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationValidationSqlServerSafetyTests
{
    private readonly SqlServerSafetyFixture fixture;

    public MigrationValidationSqlServerSafetyTests(SqlServerSafetyFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task MESP141_sql_server_concurrent_identical_staging_converges_to_one_snapshot()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenantId = new TenantId(Guid.NewGuid());
        var tenant = TenantContext.ForOrdinaryMembership(
            tenantId,
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-validation-stage"),
            actorId: Guid.NewGuid());
        var request = FoundationRequestContext.ForTenant(
            tenant.ActorId!.Value,
            Guid.NewGuid(),
            tenant,
            "tenant.migration.run.create");
        var foundation = new MigrationFoundationService(
            new MigrationPersistence(options),
            new NoopAuditSink());
        var created = await foundation.CreateRunAsync(
            request,
            new MigrationRunCreationRequest(
                tenantId.Value,
                new MigrationDefinitionReference("tenant-onboarding.foundation", "1"),
                new MigrationSourceProfileReference("neutral-source-profile", "1"),
                MigrationOperationKind.Validation,
                $"sql-validation-run-{Guid.NewGuid():N}",
                $"sql-validation-run-fp-{Guid.NewGuid():N}"));
        Assert.True(created.Succeeded, created.Code);

        var stagedId = Guid.NewGuid();
        var packageHash = new string('A', 64);
        var sourceHash = new string('B', 64);
        var record = new MigrationStagedRecord(
            stagedId,
            tenantId,
            created.Value!.RunId,
            1,
            "sql-product-1",
            MigrationCanonicalRecordType.Product,
            "{\"sku\":\"SQL-PRODUCT-1\"}",
            new string('C', 64),
            packageHash,
            MigrationCanonicalPackageParser.Version,
            Guid.NewGuid(),
            sourceHash,
            DateTimeOffset.UtcNow);
        var command = new StageMigrationPackageCommand(
            created.Value.RunId,
            record.SourceObjectId,
            sourceHash,
            packageHash,
            MigrationCanonicalPackageParser.Version,
            record.CapturedAt,
            [record]);
        var persistence = new MigrationPersistence(options);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            _ => persistence.StagePackageAsync(tenant, command)));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Code));
        Assert.Equal(1, results.Count(result => result.Outcome == MigrationPersistenceOutcome.Succeeded));
        Assert.Equal(7, results.Count(result => result.Outcome == MigrationPersistenceOutcome.Replayed));
        await using var db = new MigrationDbContext(options, tenant);
        Assert.Single(await db.StagedRecords.Where(item => item.RunId == created.Value.RunId).ToArrayAsync());
    }

    [Fact]
    public async Task MESP141_sql_server_application_validation_and_dry_run_races_are_idempotent()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenantId = new TenantId(Guid.NewGuid());
        var tenant = TenantContext.ForOrdinaryMembership(
            tenantId,
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-validation-service"),
            actorId: Guid.NewGuid());
        var request = FoundationRequestContext.ForTenant(
            tenant.ActorId!.Value,
            Guid.NewGuid(),
            tenant,
            "tenant.migration.run.create");
        var persistence = new MigrationPersistence(options);
        var foundation = new MigrationFoundationService(persistence, new NoopAuditSink());
        var definition = new MigrationDefinitionReference("tenant-onboarding.foundation", "1");
        var profile = new MigrationSourceProfileReference("neutral-source-profile", "1");
        var objectId = Guid.NewGuid();
        var content = Package(objectId, definition, profile, 0);
        for (var lengthPass = 0; lengthPass < 5; lengthPass++)
        {
            var next = Package(objectId, definition, profile, content.Length);
            if (next.Length == content.Length)
            {
                content = next;
                break;
            }
            content = next;
        }
        var source = new MigrationSourceArtifactSnapshot(objectId, tenantId, null, null, null, new string('A', 64), content.Length, 1);
        var storage = new StaticPrivateObjectStorage(tenant, objectId, content, source);
        var parsed = MigrationCanonicalPackageParser.Parse(content);
        Assert.True(parsed.Succeeded, parsed.ErrorCode);
        var expectedPackageHash = MigrationCanonicalPackageParser.Hash(parsed.Package!, parsed.Rows);
        var intake = await new MigrationIntakeService(
            persistence,
            storage,
            new TenantWideScopeResolver(),
            new NoopAuditSink()).RegisterAsync(
                request,
                new MigrationIntakeRegistrationRequest(definition, profile, MigrationOperationKind.Validation, objectId),
                $"sql-validation-service-intake-{Guid.NewGuid():N}");
        Assert.True(intake.Succeeded, intake.Code);
        var runId = intake.Value!.Run.RunId;

        var service = new MigrationValidationService(
            foundation,
            persistence,
            storage,
            new TenantWideScopeResolver(),
            new NoopReferenceAuthority());

        var validationResults = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            _ => service.ValidateAsync(request, runId, "sql-validation-service-key")));
        Assert.DoesNotContain(validationResults, result => result.Code == "migration_validation_not_permitted");
        Assert.Contains(validationResults, result => result.Succeeded);
        Assert.Equal(1, (await persistence.ListAttemptsAsync(tenant, runId)).Count(item => item.Operation == MigrationOperationKind.Validation));
        var freshValidationPersistence = new MigrationPersistence(options);
        var freshStaged = await freshValidationPersistence.ListStagedRecordsAsync(tenant, runId, 0, 100);
        var freshValidation = await freshValidationPersistence.FindLatestValidationAsync(tenant, runId);
        Assert.NotNull(freshValidation);
        Assert.Equal(expectedPackageHash, freshValidation!.PackageHash);
        Assert.Equal(new string('A', 64), freshValidation.SourceSnapshotHash);
        Assert.Equal(2, freshValidation.TotalStagedRecords);
        Assert.Equal(2, freshValidation.AcceptedCount);
        Assert.Equal(0, freshValidation.RejectedCount);
        Assert.Equal(0, freshValidation.QuarantinedCount);
        Assert.Equal([1, 2], freshValidation.Records.Select(item => item.SourceSequence).ToArray());
        Assert.Equal([1, 2], freshStaged.Select(item => item.SourceSequence).ToArray());
        Assert.Equal(freshValidation.Records.Select(item => item.StagedRecordId), freshStaged.Select(item => item.StagedRecordId));

        var dryRunResults = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            _ => service.DryRunAsync(request, runId, "sql-validation-service-dry-run-key")));
        Assert.DoesNotContain(dryRunResults, result => result.Code == "migration_dry_run_not_permitted");
        Assert.Contains(dryRunResults, result => result.Succeeded);
        Assert.Equal(1, (await persistence.ListAttemptsAsync(tenant, runId)).Count(item => item.Operation == MigrationOperationKind.DryRun));
        var freshDryRun = await new MigrationPersistence(options).FindLatestDryRunAsync(tenant, runId);
        Assert.NotNull(freshDryRun);
        Assert.Equal(expectedPackageHash, freshDryRun!.PackageHash);
        Assert.Equal(new string('A', 64), freshDryRun.SourceSnapshotHash);
        Assert.Equal(2, freshDryRun.TotalStagedRecords);
        Assert.Equal(2, freshDryRun.AcceptedCount);
        Assert.Equal(0, freshDryRun.RejectedCount);
        Assert.Equal(0, freshDryRun.QuarantinedCount);
        Assert.Equal(freshValidation.AttemptId, freshDryRun.ValidationAttemptId);
        Assert.Equal([1, 2], freshDryRun.Rows.Select(item => item.SourceSequence).ToArray());
        Assert.All(freshDryRun.Rows, item =>
        {
            Assert.Equal(MigrationRecordDisposition.Accepted, item.Disposition);
            Assert.Equal(MigrationPlannedAction.Create, item.PlannedAction);
            Assert.Equal("would be evaluated by the owning module", item.Projection);
        });
    }

    [Fact]
    public async Task MESP141_sql_server_run_scope_gates_mutations_and_all_read_surfaces()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenant = TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-validation-scope"),
            actorId: Guid.NewGuid());
        var context = FoundationRequestContext.ForTenant(tenant.ActorId!.Value, Guid.NewGuid(), tenant, "tenant.migration.run.create");
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var resolver = new MutableScopeResolver(TenantWorkScopeRequest.ForBranch(companyA, branchA));
        var storage = new InMemoryPrivateObjectStorage();
        var source = await storage.StoreAsync(
            tenant,
            TenantWorkScope.IssueFromVerifiedAuthority(tenant, TenantWorkScopeRequest.ForBranch(companyA, branchA)),
            "scope.txt",
            "text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes("scope")),
            safetyRequirement: PrivateFileSafetyRequirement.TrustedGenerated);
        var persistence = new MigrationPersistence(options);
        var audit = new NoopAuditSink();
        var intake = await new MigrationIntakeService(persistence, storage, resolver, audit).RegisterAsync(
            context,
            new MigrationIntakeRegistrationRequest(
                new MigrationDefinitionReference("tenant-onboarding.foundation", "1"),
                new MigrationSourceProfileReference("neutral-source-profile", "1"),
                MigrationOperationKind.Validation,
                source.ObjectId),
            "sql-validation-scope-intake");
        Assert.True(intake.Succeeded, intake.Code);

        var service = new MigrationValidationService(
            new MigrationFoundationService(persistence, audit),
            persistence,
            storage,
            resolver,
            new NoopReferenceAuthority());
        var runId = intake.Value!.Run.RunId;

        Assert.True(await service.IsResourceAuthorizedAsync(tenant, runId));
        resolver.Current = TenantWorkScopeRequest.ForBranch(companyA, branchB);
        Assert.False(await service.IsResourceAuthorizedAsync(tenant, runId));
        Assert.Null(await service.ReadValidationAsync(tenant, runId));
        Assert.Empty(await service.ReadFindingsAsync(tenant, runId, 0, 100));
        Assert.Empty(await service.ReadStagedRecordsAsync(tenant, runId, 0, 100));
        Assert.Null(await service.ReadDryRunAsync(tenant, runId));
        Assert.Equal("migration_source_scope_denied", (await service.ValidateAsync(context, runId, "scope-validation")).Code);
        Assert.Equal("migration_source_scope_denied", (await service.DryRunAsync(context, runId, "scope-dry-run")).Code);

        resolver.Current = TenantWorkScopeRequest.ForCompany(companyB);
        Assert.False(await service.IsResourceAuthorizedAsync(tenant, runId));
        var foreign = TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-validation-foreign"),
            actorId: Guid.NewGuid());
        Assert.False(await service.IsResourceAuthorizedAsync(foreign, runId));
    }

    private static byte[] Package(Guid objectId, MigrationDefinitionReference definition, MigrationSourceProfileReference profile, int length) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            PackageVersion = MigrationCanonicalPackageParser.Version,
            DefinitionId = definition.DefinitionId,
            DefinitionVersion = definition.Version,
            SourceProfileId = profile.ProfileId,
            SourceProfileVersion = profile.ProfileVersion,
            LogicalDataset = "sql-race",
            SourceSnapshot = new { ObjectId = objectId, Sha256 = new string('A', 64), Length = length, ConcurrencyVersion = 1 },
            Records = new[]
            {
                new { SourceSequence = 1, SourceRecordId = "product-1", RecordType = "Product", Payload = new { Sku = "SQL-PRODUCT-1", NameEnglish = "SQL product 1" } },
                new { SourceSequence = 2, SourceRecordId = "product-2", RecordType = "Product", Payload = new { Sku = "SQL-PRODUCT-2", NameEnglish = "SQL product 2" } }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private sealed class NoopReferenceAuthority : IMigrationReferenceAuthority
    {
        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>([]);
    }

    private sealed class TenantWideScopeResolver : ICurrentOrganizationScopeResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));
    }

    private sealed class MutableScopeResolver(TenantWorkScopeRequest current) : ICurrentOrganizationScopeResolver
    {
        public TenantWorkScopeRequest Current { get; set; } = current;

        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, Current));
    }

    private sealed class StaticPrivateObjectStorage : IPrivateObjectStorage
    {
        private readonly TenantContext tenant;
        private readonly byte[] content;
        private readonly PrivateFileMetadata metadata;

        public StaticPrivateObjectStorage(TenantContext tenant, Guid objectId, byte[] content, MigrationSourceArtifactSnapshot source)
        {
            this.tenant = tenant;
            this.content = content;
            metadata = new PrivateFileMetadata(
                objectId,
                tenant.TenantId,
                TenantWorkScope.IssueFromVerifiedAuthority(tenant, TenantWorkScopeRequest.TenantWide()),
                "migration.json",
                "application/json",
                source.Length,
                source.Sha256,
                DateTimeOffset.UnixEpoch,
                null,
                PrivateFileSafetyRequirement.TrustedGenerated);
        }

        public ValueTask<PrivateFileMetadata> StoreAsync(TenantContext tenantContext, TenantWorkScope scope, string originalFileName, string contentType, Stream content, DateTimeOffset? expiresAt = null, PrivateFileSafetyRequirement safetyRequirement = PrivateFileSafetyRequirement.ExternalScanRequired, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<PrivateFileAccessResult> ReadAsync(TenantContext tenantContext, Guid objectId, CancellationToken cancellationToken = default) =>
            tenantContext.TenantId == tenant.TenantId && objectId == metadata.ObjectId
                ? ValueTask.FromResult(PrivateFileAccessResult.AllowedResult(metadata, content))
                : ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));

        public ValueTask<PrivateFileOverwriteResult> OverwriteAsync(TenantContext tenantContext, Guid objectId, long expectedConcurrencyVersion, Stream content, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
