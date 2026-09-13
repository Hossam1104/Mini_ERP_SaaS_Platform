using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
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

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
