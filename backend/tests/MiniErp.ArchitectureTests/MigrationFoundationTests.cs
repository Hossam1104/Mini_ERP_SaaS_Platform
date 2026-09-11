using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Run_identity_is_server_owned_and_client_tenant_cannot_override_it()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options, Context(TenantA, "migration-tenant-a"));
        var service = new MigrationFoundationService(new MigrationPersistence(options));

        var rejected = await service.CreateRunAsync(
            Context(TenantA, "migration-tenant-a"),
            Request(TenantB, MigrationOperationKind.Validation, "run-key-a", "fingerprint-a"));

        Assert.False(rejected.Succeeded);
        Assert.Equal(MigrationResultKind.Rejected, rejected.Kind);
        Assert.Equal("migration_tenant_context_mismatch", rejected.Code);

        var created = await service.CreateRunAsync(
            Context(TenantA, "migration-tenant-a"),
            Request(null, MigrationOperationKind.Validation, "run-key-a", "fingerprint-a"));

        Assert.True(created.Succeeded, created.Code);
        Assert.Equal(TenantA, created.Value!.TenantId.Value);
        Assert.NotEqual(Guid.Empty, created.Value.RunId);
    }

    [Fact]
    public async Task Run_lookup_and_idempotency_are_tenant_scoped()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var contextA = Context(TenantA, "migration-tenant-a");
        var contextB = Context(TenantB, "migration-tenant-b");
        await EnsureCreatedAsync(options, contextA);
        var service = new MigrationFoundationService(new MigrationPersistence(options));

        var first = await service.CreateRunAsync(contextA, Request(null, MigrationOperationKind.DryRun, "shared-key", "same-request"));
        var replay = await service.CreateRunAsync(contextA, Request(null, MigrationOperationKind.DryRun, "shared-key", "same-request"));
        var conflict = await service.CreateRunAsync(contextA, Request(null, MigrationOperationKind.DryRun, "shared-key", "different-request"));
        var tenantBRun = await service.CreateRunAsync(contextB, Request(null, MigrationOperationKind.DryRun, "shared-key", "same-request"));
        var separateOperationRun = await service.CreateRunAsync(contextA, Request(null, MigrationOperationKind.Validation, "shared-key", "same-request"));

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(first.Value!.RunId, replay.Value!.RunId);
        Assert.Equal(MigrationResultKind.Rejected, conflict.Kind);
        Assert.Equal("migration_idempotency_conflict", conflict.Code);
        Assert.True(tenantBRun.Succeeded, tenantBRun.Code);
        Assert.NotEqual(first.Value.RunId, tenantBRun.Value!.RunId);
        Assert.True(separateOperationRun.Succeeded, separateOperationRun.Code);
        Assert.NotEqual(first.Value.RunId, separateOperationRun.Value!.RunId);

        var foreignRead = await service.FindRunAsync(contextB, first.Value.RunId);
        Assert.False(foreignRead.Succeeded);
        Assert.Equal("migration_run_not_found", foreignRead.Code);
    }

    [Fact]
    public void State_machine_rejects_invalid_and_terminal_transitions()
    {
        var run = MigrationRun.Create(Context(TenantA, "migration-state"), Definition(), Profile());

        Assert.True(run.TryTransition(MigrationRunStatus.Prepared).Allowed);
        Assert.False(run.TryTransition(MigrationRunStatus.Completed).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Validating).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.OutcomeUnknown).Allowed);
        Assert.False(run.TryTransition(MigrationRunStatus.Prepared).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Corrected).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Prepared).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Validating).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Validated).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Executing).Allowed);
        Assert.True(run.TryTransition(MigrationRunStatus.Completed).Allowed);
        Assert.False(run.TryTransition(MigrationRunStatus.Prepared).Allowed);
        Assert.True(run.IsTerminal);
    }

    [Fact]
    public void Attempts_preserve_lineage_and_unknown_outcome_is_not_safe_to_retry()
    {
        var run = MigrationRun.Create(Context(TenantA, "migration-attempt"), Definition(), Profile());
        var first = MigrationAttempt.Start(
            run,
            MigrationOperationKind.Execution,
            new MigrationIdempotencyKey("attempt-1"),
            new MigrationRequestFingerprint("request-1"),
            sequence: 1);
        var firstResult = first.RecordOutcome(MigrationAttemptOutcome.KnownFailure, "known_failure");
        var retry = MigrationAttempt.Start(
            run,
            MigrationOperationKind.Execution,
            new MigrationIdempotencyKey("attempt-2"),
            new MigrationRequestFingerprint("request-2"),
            sequence: 2,
            previousAttemptId: first.AttemptId);
        var unknown = retry.RecordOutcome(MigrationAttemptOutcome.UnknownOutcome, "provider_uncertain");

        Assert.Equal(MigrationResultKind.KnownFailure, firstResult.Kind);
        Assert.True(first.IsSafeToRetry);
        Assert.Equal(first.AttemptId, retry.PreviousAttemptId);
        Assert.Equal(MigrationResultKind.UnknownOutcome, unknown.Kind);
        Assert.False(unknown.IsSafeToRetry);
        Assert.False(retry.IsSafeToRetry);
        Assert.Equal(MigrationAttemptOutcome.UnknownOutcome, retry.Outcome);
    }

    [Fact]
    public async Task Attempt_idempotency_is_operation_and_tenant_scoped_without_creating_a_second_run()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, "migration-attempt-persistence");
        await EnsureCreatedAsync(options, context);
        var persistence = new MigrationPersistence(options);
        var service = new MigrationFoundationService(persistence);
        var runResult = await service.CreateRunAsync(context, Request(null, MigrationOperationKind.Execution, "run-key", "run-fingerprint"));
        Assert.True(runResult.Succeeded, runResult.Code);

        var run = MigrationRun.Create(context, Definition(), Profile(), runId: runResult.Value!.RunId);
        var attempt = MigrationAttempt.Start(
            run,
            MigrationOperationKind.Execution,
            new MigrationIdempotencyKey("attempt-key"),
            new MigrationRequestFingerprint("attempt-fingerprint"),
            sequence: 1);
        var first = await persistence.CreateAttemptAsync(context, new CreateMigrationAttemptCommand(attempt));
        var replay = await persistence.CreateAttemptAsync(context, new CreateMigrationAttemptCommand(attempt));

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(MigrationPersistenceOutcome.Replayed, replay.Outcome);
        Assert.Equal(first.Value!.AttemptId, replay.Value!.AttemptId);
        Assert.Equal(runResult.Value.RunId, replay.Value.RunId);

        var foreignAttempt = await persistence.FindAttemptAsync(
            Context(TenantB, "migration-attempt-foreign"),
            run.RunId,
            attempt.AttemptId);
        Assert.Null(foreignAttempt);
    }

    [Fact]
    public void Migration_evidence_uses_existing_foundation_audit_and_safe_run_attempt_references()
    {
        var tenantContext = Context(TenantA, "migration-audit");
        var requestContext = FoundationRequestContext.ForTenant(
            Actor,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            tenantContext,
            "tenant.migration.foundation");
        var run = MigrationRun.Create(tenantContext, Definition(), Profile());
        var attempt = MigrationAttempt.Start(
            run,
            MigrationOperationKind.Validation,
            new MigrationIdempotencyKey("audit-key"),
            new MigrationRequestFingerprint("audit-fingerprint"),
            sequence: 1);

        var evidence = MigrationAuditEvidenceFactory.Create(
            requestContext,
            run,
            attempt,
            "migration.run.validating",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            "validation_started",
            "rowCount=0");

        Assert.Equal(TenantA, evidence.TenantId);
        Assert.Equal(Actor, evidence.ActorId);
        Assert.Equal("migration", evidence.Source);
        Assert.Equal("migration-run", evidence.TargetType);
        Assert.Contains($"run={run.RunId:D}", evidence.TargetReference, StringComparison.Ordinal);
        Assert.Contains($"attempt={attempt.AttemptId:D}", evidence.TargetReference, StringComparison.Ordinal);
        Assert.Contains("rowCount=0", evidence.ChangeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-payload", evidence.ChangeSummary ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_module_does_not_take_cross_module_persistence_dependencies()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "backend", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains("MiniErp.App\\Modules\\Migration", StringComparison.Ordinal)
                || path.Contains("MiniErp.Infrastructure\\Persistence\\Modules\\Migration", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.NotEmpty(files);
        Assert.DoesNotContain(files, source => source.Contains("Modules.Finance", StringComparison.Ordinal));
        Assert.DoesNotContain(files, source => source.Contains("Modules.Inventory", StringComparison.Ordinal));
        Assert.DoesNotContain(files, source => source.Contains("Modules.MasterData", StringComparison.Ordinal));
        Assert.DoesNotContain(files, source => source.Contains("Modules.Procurement", StringComparison.Ordinal));
        Assert.DoesNotContain(files, source => source.Contains("Modules.Sales", StringComparison.Ordinal));
    }

    private static MigrationRunCreationRequest Request(
        Guid? requestedTenantId,
        MigrationOperationKind operation,
        string key,
        string fingerprint) => new(
        requestedTenantId,
        Definition(),
        Profile(),
        operation,
        key,
        fingerprint);

    private static MigrationDefinitionReference Definition() => new("tenant-onboarding.foundation", "1");

    private static MigrationSourceProfileReference Profile() => new("neutral-source-profile", "1");

    private static TenantContext Context(Guid tenantId, string correlation) =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(tenantId),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId(correlation),
            actorId: Actor);

    private static async Task EnsureCreatedAsync(DbContextOptions options, TenantContext context)
    {
        await using var db = new MigrationDbContext(options, context);
        await db.Database.EnsureCreatedAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
