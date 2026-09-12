using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationIntakeTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Fingerprint_is_deterministic_and_excludes_transport_identity()
    {
        var source = Snapshot(TenantA, Guid.Parse("11111111-1111-1111-1111-111111111111"), "AA");
        var first = MigrationIntakeFingerprint.Compute(
            Context(TenantA, "first-correlation", Actor), Definition(), Profile(), MigrationOperationKind.Validation, source);
        var second = MigrationIntakeFingerprint.Compute(
            Context(TenantA, "second-correlation", Guid.NewGuid()), Definition(), Profile(), MigrationOperationKind.Validation, source);

        Assert.Equal(first.Value, second.Value);
        Assert.NotEqual(first.Value, MigrationIntakeFingerprint.Compute(
            Context(TenantA, "same", Actor), new MigrationDefinitionReference("other-definition", "1"), Profile(), MigrationOperationKind.Validation, source).Value);
        Assert.NotEqual(first.Value, MigrationIntakeFingerprint.Compute(
            Context(TenantA, "same", Actor), Definition(), Profile(), MigrationOperationKind.DryRun, source).Value);
    }

    [Fact]
    public void Public_intake_request_has_no_caller_authority_fields()
    {
        var names = typeof(MigrationIntakeCreateRequest)
            .GetProperties()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("TenantId", names);
        Assert.DoesNotContain("RequestedTenantId", names);
        Assert.DoesNotContain("RequestFingerprint", names);
        Assert.DoesNotContain("RunId", names);
        Assert.DoesNotContain("ActorId", names);
        Assert.DoesNotContain("CreatedAt", names);
    }

    [Fact]
    public async Task Same_key_and_same_source_snapshot_replays_one_durable_intake()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.StoreAsync("source-a");
        var request = fixture.Request(source.ObjectId);

        var first = await fixture.Service.RegisterAsync(fixture.Foundation, request, "intake-key");
        var replay = await fixture.Service.RegisterAsync(fixture.Foundation, request, "intake-key");

        Assert.Equal(MigrationResultKind.Succeeded, first.Kind);
        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(first.Value!.Run.RunId, replay.Value!.Run.RunId);
        Assert.Equal(MigrationRunStatus.Draft, first.Value.Run.Status);
        Assert.Equal(1, await fixture.CountRunsAsync());
        Assert.Equal(1, await fixture.CountIntakesAsync());
        Assert.Equal(2, fixture.Audit.Appended.Count(item => item.OperationId == MigrationIntakeService.OperationId));
    }

    [Fact]
    public async Task Same_key_with_changed_definition_profile_operation_or_source_is_conflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.StoreAsync("source-a");
        var original = fixture.Request(source.ObjectId);
        var first = await fixture.Service.RegisterAsync(fixture.Foundation, original, "same-key");
        Assert.True(first.Succeeded, first.Code);
        var storedKey = await fixture.Persistence.FindIdempotencyAsync(fixture.Tenant, MigrationOperationKind.Validation, "same-key");
        Assert.NotNull(storedKey);
        Assert.Equal(first.Value!.RequestFingerprint, storedKey!.RequestFingerprint);
        foreach (var changed in new[]
        {
            original with { Definition = new MigrationDefinitionReference("changed-definition", "1") },
            original with { SourceProfile = new MigrationSourceProfileReference("changed-profile", "1") },
            original with { Operation = MigrationOperationKind.DryRun }
        })
        {
            Assert.NotEqual(
                first.Value!.RequestFingerprint,
                MigrationIntakeFingerprint.Compute(fixture.Tenant, changed.Definition, changed.SourceProfile, changed.Operation, first.Value.Source).Value);
            var result = await fixture.Service.RegisterAsync(fixture.Foundation, changed, "same-key");
            Assert.True(
                result.Code == "migration_idempotency_conflict",
                $"definition={changed.Definition.DefinitionId};profile={changed.SourceProfile.ProfileId};operation={changed.Operation};code={result.Code}");
            Assert.Equal(MigrationResultKind.Rejected, result.Kind);
        }

        var overwritten = await fixture.Storage.OverwriteAsync(
            fixture.Tenant,
            source.ObjectId,
            source.ConcurrencyVersion,
            Content("source-b"));
        Assert.True(overwritten.Mutated);

        var sourceChanged = await fixture.Service.RegisterAsync(fixture.Foundation, original, "same-key");
        Assert.Equal(MigrationResultKind.Rejected, sourceChanged.Kind);
        Assert.Equal("migration_idempotency_conflict", sourceChanged.Code);

        var newKey = await fixture.Service.RegisterAsync(fixture.Foundation, original, "new-key");
        Assert.True(newKey.Succeeded, newKey.Code);
        Assert.NotEqual(first.Value!.Run.RunId, newKey.Value!.Run.RunId);
        Assert.Equal(2, await fixture.CountIntakesAsync());
    }

    [Fact]
    public async Task Foreign_missing_expired_disposed_and_unsafe_sources_fail_closed_without_intake()
    {
        await using var fixture = await Fixture.CreateAsync();
        var foreign = await fixture.Storage.StoreAsync(
            fixture.ForeignTenant,
            TenantWorkScope.IssueFromVerifiedAuthority(fixture.ForeignTenant, TenantWorkScopeRequest.TenantWide()),
            "foreign.txt",
            "text/plain",
            Content("foreign"),
            safetyRequirement: PrivateFileSafetyRequirement.TrustedGenerated);
        var expired = await fixture.StoreAsync("expired", DateTimeOffset.UtcNow.AddMinutes(-1));
        var disposed = await fixture.StoreAsync("disposed");
        disposed.Disposition = PrivateFileDisposition.Disposed;
        var unsafeSource = await fixture.Storage.StoreAsync(
            fixture.Tenant,
            TenantWorkScope.IssueFromVerifiedAuthority(fixture.Tenant, TenantWorkScopeRequest.TenantWide()),
            "unsafe.txt",
            "text/plain",
            Content("unsafe"));

        var missing = await fixture.Service.RegisterAsync(fixture.Foundation, fixture.Request(Guid.NewGuid()), "missing");
        var foreignResult = await fixture.Service.RegisterAsync(fixture.Foundation, fixture.Request(foreign.ObjectId), "foreign");
        var expiredResult = await fixture.Service.RegisterAsync(fixture.Foundation, fixture.Request(expired.ObjectId), "expired");
        var disposedResult = await fixture.Service.RegisterAsync(fixture.Foundation, fixture.Request(disposed.ObjectId), "disposed");
        var unsafeResult = await fixture.Service.RegisterAsync(fixture.Foundation, fixture.Request(unsafeSource.ObjectId), "unsafe");

        Assert.Equal("migration_source_not_found", missing.Code);
        Assert.Equal(missing.Code, foreignResult.Code);
        Assert.Equal("migration_source_expired", expiredResult.Code);
        Assert.Equal("migration_source_disposed", disposedResult.Code);
        Assert.Equal("migration_source_safety_blocked", unsafeResult.Code);
        Assert.Equal(0, await fixture.CountRunsAsync());
        Assert.Equal(0, await fixture.CountIntakesAsync());
    }

    [Fact]
    public async Task Tenant_scoped_persistence_does_not_expose_a_foreign_intake()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.Storage.StoreAsync(
            fixture.ForeignTenant,
            TenantWorkScope.IssueFromVerifiedAuthority(fixture.ForeignTenant, TenantWorkScopeRequest.TenantWide()),
            "foreign.txt",
            "text/plain",
            Content("foreign"),
            safetyRequirement: PrivateFileSafetyRequirement.TrustedGenerated);
        var foreign = await fixture.Service.RegisterAsync(
            fixture.ForeignFoundation,
            fixture.Request(source.ObjectId) with { },
            "foreign-intake");

        Assert.True(foreign.Succeeded, foreign.Code);
        Assert.Null(await fixture.Persistence.FindRunAsync(fixture.Tenant, foreign.Value!.Run.RunId));
        Assert.Equal(0, await fixture.CountRunsAsync());
        Assert.Equal(1, await fixture.CountRunsAsync(fixture.ForeignTenant));
    }

    private static MigrationDefinitionReference Definition() => new("tenant-onboarding.foundation", "1");

    private static MigrationSourceProfileReference Profile() => new("neutral-source-profile", "1");

    private static TenantContext Context(Guid tenantId, string correlation, Guid actor) =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(tenantId),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference($"Tenant:{tenantId:D}"),
            new CorrelationId(correlation),
            actor);

    private static MigrationSourceArtifactSnapshot Snapshot(Guid tenantId, Guid objectId, string hashPrefix) =>
        new(objectId, new TenantId(tenantId), null, null, null, hashPrefix.PadRight(64, '0'), 4, 1);

    private static Stream Content(string value) => new MemoryStream(Encoding.UTF8.GetBytes(value));

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(SqliteConnection connection, DbContextOptions options)
        {
            this.connection = connection;
            this.options = options;
            Tenant = Context(TenantA, "intake-tenant-a", Actor);
            ForeignTenant = Context(TenantB, "intake-tenant-b", Actor);
            Foundation = FoundationRequestContext.ForTenant(Actor, Session, Tenant, "tenant.migration.intake");
            ForeignFoundation = FoundationRequestContext.ForTenant(Actor, Session, ForeignTenant, "tenant.migration.intake");
            Storage = new InMemoryPrivateObjectStorage();
            Audit = new RecordingAuditSink();
            Persistence = new MigrationPersistence(options);
            Service = new MigrationIntakeService(Persistence, Storage, Audit);
        }

        internal TenantContext Tenant { get; }
        internal TenantContext ForeignTenant { get; }
        internal FoundationRequestContext Foundation { get; }
        internal FoundationRequestContext ForeignFoundation { get; }
        internal InMemoryPrivateObjectStorage Storage { get; }
        internal RecordingAuditSink Audit { get; }
        internal MigrationPersistence Persistence { get; }
        internal MigrationIntakeService Service { get; }

        internal static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var fixture = new Fixture(connection, options);
            await using var db = new MigrationDbContext(options, fixture.Tenant);
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }

        internal MigrationIntakeRegistrationRequest Request(Guid sourceObjectId) => new(
            Definition(),
            Profile(),
            MigrationOperationKind.Validation,
            sourceObjectId);

        internal async Task<PrivateFileMetadata> StoreAsync(string name, DateTimeOffset? expiresAt = null) =>
            await Storage.StoreAsync(
                Tenant,
                TenantWorkScope.IssueFromVerifiedAuthority(Tenant, TenantWorkScopeRequest.TenantWide()),
                $"{name}.txt",
                "text/plain",
                Content(name),
                expiresAt,
                PrivateFileSafetyRequirement.TrustedGenerated);

        internal async Task<int> CountRunsAsync(TenantContext? tenant = null)
        {
            await using var db = new MigrationDbContext(options, tenant ?? Tenant);
            return await db.Runs.CountAsync();
        }

        internal async Task<int> CountIntakesAsync()
        {
            await using var db = new MigrationDbContext(options, Tenant);
            return await db.Intakes.CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }

    private sealed class RecordingAuditSink : IFoundationAuditEvidenceSink
    {
        internal List<FoundationAuditEvidence> Appended { get; } = [];

        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default)
        {
            Appended.Add(evidence);
            return ValueTask.CompletedTask;
        }
    }

}
