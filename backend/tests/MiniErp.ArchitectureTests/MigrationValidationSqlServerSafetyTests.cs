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
    public async Task MESP141_sql_server_mutations_commit_unconfirmed_before_audit_confirmation()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenantId = new TenantId(Guid.NewGuid());
        var tenant = TenantContext.ForOrdinaryMembership(
            tenantId,
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-r6-atomic"),
            actorId: Guid.NewGuid());
        var request = FoundationRequestContext.ForTenant(
            tenant.ActorId!.Value,
            Guid.NewGuid(),
            tenant,
            "tenant.migration.run.create");
        var persistence = new MigrationPersistence(options);
        var setup = new MigrationFoundationService(persistence, new NoopAuditSink());
        var created = await setup.CreateRunAsync(
            request,
            new MigrationRunCreationRequest(
                tenantId.Value,
                new MigrationDefinitionReference("tenant-onboarding.foundation", "1"),
                new MigrationSourceProfileReference("neutral-source-profile", "1"),
                MigrationOperationKind.Validation,
                $"sql-r6-run-{Guid.NewGuid():N}",
                $"sql-r6-run-fp-{Guid.NewGuid():N}"));
        Assert.True(created.Succeeded, created.Code);

        var transitionAudit = new BlockingAuditSink("migration.run.transition");
        var transitionService = new MigrationFoundationService(persistence, transitionAudit);
        var transitionTask = transitionService.TransitionRunAsync(
            request, created.Value!.RunId, MigrationRunStatus.Prepared, created.Value.Version);
        try
        {
            await transitionAudit.WaitAsync();
            var beforeConfirmation = await new MigrationPersistence(options).FindRunAsync(tenant, created.Value.RunId);
            Assert.Equal(MigrationRunStatus.Prepared, beforeConfirmation!.Status);
            Assert.False(beforeConfirmation.EvidenceConfirmed);
        }
        finally
        {
            transitionAudit.Release();
        }

        var transitioned = await transitionTask;
        Assert.True(transitioned.Succeeded, transitioned.Code);
        var afterTransition = await new MigrationPersistence(options).FindRunAsync(tenant, created.Value.RunId);
        Assert.True(afterTransition!.EvidenceConfirmed);

        var outcomeAudit = new BlockingAuditSink("migration.attempt.outcome");
        var outcomeService = new MigrationFoundationService(persistence, outcomeAudit);
        var started = await outcomeService.StartAttemptAsync(
            request, created.Value.RunId, MigrationOperationKind.Validation, "sql-r6-attempt", "sql-r6-attempt-fp");
        Assert.True(started.Succeeded, started.Code);

        var outcomeTask = outcomeService.RecordAttemptOutcomeAsync(
            request,
            created.Value.RunId,
            started.Value!.AttemptId,
            MigrationAttemptOutcome.Succeeded,
            "completed",
            started.Value.Version);
        try
        {
            await outcomeAudit.WaitAsync();
            var beforeConfirmation = await new MigrationPersistence(options).FindAttemptAsync(
                tenant, created.Value.RunId, started.Value.AttemptId);
            Assert.Equal(MigrationAttemptOutcome.Succeeded, beforeConfirmation!.Outcome);
            Assert.False(beforeConfirmation.EvidenceConfirmed);
            var idempotencyBeforeConfirmation = await new MigrationPersistence(options).FindIdempotencyAsync(
                tenant, MigrationOperationKind.Validation, "sql-r6-attempt");
            Assert.NotNull(idempotencyBeforeConfirmation);
            Assert.False(idempotencyBeforeConfirmation!.EvidenceConfirmed);
            var runBeforeConfirmation = await new MigrationPersistence(options).FindRunAsync(
                tenant, created.Value.RunId);
            Assert.False(runBeforeConfirmation!.EvidenceConfirmed);

            var concurrentReplay = await outcomeService.StartAttemptAsync(
                request,
                created.Value.RunId,
                MigrationOperationKind.Validation,
                "sql-r6-attempt",
                "sql-r6-attempt-fp");
            Assert.Equal(MigrationResultKind.UnknownOutcome, concurrentReplay.Kind);
            Assert.Equal("migration_audit_recovery_required", concurrentReplay.Code);
            Assert.False(concurrentReplay.IsSafeToRetry);
            Assert.Single(await new MigrationPersistence(options).ListAttemptsAsync(tenant, created.Value.RunId));
        }
        finally
        {
            outcomeAudit.Release();
        }

        var recorded = await outcomeTask;
        Assert.True(recorded.Succeeded, recorded.Code);
        var afterOutcome = await new MigrationPersistence(options).FindAttemptAsync(
            tenant, created.Value.RunId, started.Value.AttemptId);
        Assert.True(afterOutcome!.EvidenceConfirmed);
        var afterOutcomeIdentity = await new MigrationPersistence(options).FindIdempotencyAsync(
            tenant, MigrationOperationKind.Validation, "sql-r6-attempt");
        Assert.True(afterOutcomeIdentity!.EvidenceConfirmed);
        var afterOutcomeRun = await new MigrationPersistence(options).FindRunAsync(tenant, created.Value.RunId);
        Assert.True(afterOutcomeRun!.EvidenceConfirmed);

        var replay = await outcomeService.StartAttemptAsync(
            request,
            created.Value.RunId,
            MigrationOperationKind.Validation,
            "sql-r6-attempt",
            "sql-r6-attempt-fp");
        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(started.Value.AttemptId, replay.Value!.AttemptId);
        Assert.Single(await new MigrationPersistence(options).ListAttemptsAsync(tenant, created.Value.RunId));

        var failedAudit = new ToggleAuditSink();
        var failedAuditPersistence = new MigrationPersistence(options);
        var failedAuditService = new MigrationFoundationService(failedAuditPersistence, failedAudit);
        var failedRun = await failedAuditService.CreateRunAsync(
            request,
            new MigrationRunCreationRequest(
                tenantId.Value,
                new MigrationDefinitionReference("tenant-onboarding.foundation", "1"),
                new MigrationSourceProfileReference("neutral-source-profile", "1"),
                MigrationOperationKind.Validation,
                $"sql-r6-failure-run-{Guid.NewGuid():N}",
                $"sql-r6-failure-run-fp-{Guid.NewGuid():N}"));
        Assert.True(failedRun.Succeeded, failedRun.Code);
        var failedPrepared = await failedAuditService.TransitionRunAsync(
            request, failedRun.Value!.RunId, MigrationRunStatus.Prepared, failedRun.Value.Version);
        Assert.True(failedPrepared.Succeeded, failedPrepared.Code);
        var failedAttempt = await failedAuditService.StartAttemptAsync(
            request, failedRun.Value.RunId, MigrationOperationKind.Validation, "sql-r6-failure", "sql-r6-failure-fp");
        Assert.True(failedAttempt.Succeeded, failedAttempt.Code);
        failedAudit.FailOn("migration.attempt.outcome");
        var failedOutcome = await failedAuditService.RecordAttemptOutcomeAsync(
            request,
            failedRun.Value.RunId,
            failedAttempt.Value!.AttemptId,
            MigrationAttemptOutcome.Succeeded,
            "completed",
            failedAttempt.Value.Version);
        Assert.Equal(MigrationResultKind.UnknownOutcome, failedOutcome.Kind);
        Assert.Equal("migration_audit_evidence_unavailable", failedOutcome.Code);
        Assert.False(failedOutcome.IsSafeToRetry);

        var failedFresh = new MigrationPersistence(options);
        var failedRunRecord = await failedFresh.FindRunAsync(tenant, failedRun.Value.RunId);
        var failedAttemptRecord = await failedFresh.FindAttemptAsync(tenant, failedRun.Value.RunId, failedAttempt.Value.AttemptId);
        var failedIdentity = await failedFresh.FindIdempotencyAsync(tenant, MigrationOperationKind.Validation, "sql-r6-failure");
        Assert.False(failedRunRecord!.EvidenceConfirmed);
        Assert.Equal(MigrationAttemptOutcome.Succeeded, failedAttemptRecord!.Outcome);
        Assert.False(failedAttemptRecord.EvidenceConfirmed);
        Assert.False(failedIdentity!.EvidenceConfirmed);
        var failedReplay = await failedAuditService.StartAttemptAsync(
            request, failedRun.Value.RunId, MigrationOperationKind.Validation, "sql-r6-failure", "sql-r6-failure-fp");
        Assert.Equal(MigrationResultKind.UnknownOutcome, failedReplay.Kind);
        Assert.Equal("migration_audit_recovery_required", failedReplay.Code);
        Assert.False(failedReplay.IsSafeToRetry);
        Assert.Single(await failedFresh.ListAttemptsAsync(tenant, failedRun.Value.RunId));
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
        var audit = new ToggleAuditSink();
        var foundation = new MigrationFoundationService(persistence, audit);
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
            audit).RegisterAsync(
                request,
                new MigrationIntakeRegistrationRequest(definition, profile, MigrationOperationKind.Validation, objectId),
                $"sql-validation-service-intake-{Guid.NewGuid():N}");
        Assert.True(intake.Succeeded, intake.Code);
        var runId = intake.Value!.Run.RunId;
        var currentRun = await persistence.FindRunAsync(tenant, runId);
        Assert.NotNull(currentRun);

        var prepared = await foundation.TransitionRunAsync(
            request,
            runId,
            MigrationRunStatus.Prepared,
            currentRun!.Version);
        Assert.True(prepared.Succeeded, prepared.Code);
        var validating = await foundation.TransitionRunAsync(
            request,
            runId,
            MigrationRunStatus.Validating,
            prepared.Value!.Version);
        Assert.True(validating.Succeeded, validating.Code);

        var service = new MigrationValidationService(
            foundation,
            persistence,
            storage,
            new TenantWideScopeResolver(),
            new NoopReferenceAuthority(),
            new TenantWideScopeResolver());

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

        async Task<Guid> RegisterRunAsync(string key)
        {
            var next = await new MigrationIntakeService(
                persistence,
                storage,
                new TenantWideScopeResolver(),
                audit).RegisterAsync(
                    request,
                    new MigrationIntakeRegistrationRequest(definition, profile, MigrationOperationKind.Validation, objectId),
                    key);
            Assert.True(next.Succeeded, next.Code);
            return next.Value!.Run.RunId;
        }

        audit.FailOn("migration.attempt.start");
        var startFailureKey = "sql-validation-audit-start";
        var startFailureRun = await RegisterRunAsync("sql-validation-audit-start-intake");
        var startFailure = await service.ValidateAsync(request, startFailureRun, startFailureKey);
        Assert.Equal(MigrationResultKind.UnknownOutcome, startFailure.Kind);
        Assert.Equal("migration_audit_evidence_unavailable", startFailure.Code);
        Assert.False(startFailure.IsSafeToRetry);
        Assert.Single(await persistence.ListAttemptsAsync(tenant, startFailureRun), item => item.Operation == MigrationOperationKind.Validation);
        var startFailureFresh = new MigrationPersistence(options);
        var startIdentity = await startFailureFresh.FindIdempotencyAsync(tenant, MigrationOperationKind.Validation, startFailureKey);
        Assert.NotNull(startIdentity);
        Assert.False(startIdentity!.EvidenceConfirmed);
        audit.Reset();
        var startRetry = await service.ValidateAsync(request, startFailureRun, startFailureKey);
        Assert.Equal(MigrationResultKind.UnknownOutcome, startRetry.Kind);
        Assert.Equal("migration_audit_recovery_required", startRetry.Code);
        Assert.False(startRetry.IsSafeToRetry);
        Assert.Single(await startFailureFresh.ListAttemptsAsync(tenant, startFailureRun), item => item.Operation == MigrationOperationKind.Validation);

        var dryRunFailureRun = await RegisterRunAsync("sql-validation-audit-dry-intake");
        var dryValidation = await service.ValidateAsync(request, dryRunFailureRun, "sql-validation-audit-dry-validation");
        Assert.True(dryValidation.Succeeded, dryValidation.Code);
        const string dryRunFailureKey = "sql-validation-audit-dry-run";
        audit.FailOn("migration.attempt.outcome");
        var dryFailure = await service.DryRunAsync(request, dryRunFailureRun, dryRunFailureKey);
        Assert.Equal(MigrationResultKind.UnknownOutcome, dryFailure.Kind);
        Assert.Equal("migration_audit_evidence_unavailable", dryFailure.Code);
        Assert.False(dryFailure.IsSafeToRetry);
        Assert.NotNull(await new MigrationPersistence(options).FindLatestDryRunAsync(tenant, dryRunFailureRun));
        audit.Reset();
        var dryRetry = await service.DryRunAsync(request, dryRunFailureRun, dryRunFailureKey);
        Assert.Equal(MigrationResultKind.UnknownOutcome, dryRetry.Kind);
        Assert.Equal("migration_audit_recovery_required", dryRetry.Code);
        Assert.False(dryRetry.IsSafeToRetry);
        Assert.Single(await new MigrationPersistence(options).ListAttemptsAsync(tenant, dryRunFailureRun), item => item.Operation == MigrationOperationKind.DryRun);

        var transitionFailureRun = await RegisterRunAsync("sql-validation-audit-transition-intake");
        audit.FailOn("migration.run.transition", occurrence: 3);
        var transitionFailure = await service.ValidateAsync(request, transitionFailureRun, "sql-validation-audit-transition");
        Assert.Equal(MigrationResultKind.UnknownOutcome, transitionFailure.Kind);
        Assert.Equal("migration_audit_evidence_unavailable", transitionFailure.Code);
        Assert.False(transitionFailure.IsSafeToRetry);
        var transitionRun = await new MigrationPersistence(options).FindRunAsync(tenant, transitionFailureRun);
        Assert.NotNull(transitionRun);
        Assert.Equal(MigrationRunStatus.Validated, transitionRun!.Status);
        Assert.False(transitionRun.EvidenceConfirmed);
        audit.Reset();
        var transitionRetry = await service.ValidateAsync(request, transitionFailureRun, "sql-validation-audit-transition");
        Assert.Equal(MigrationResultKind.UnknownOutcome, transitionRetry.Kind);
        Assert.Equal("migration_audit_recovery_required", transitionRetry.Code);
        Assert.False(transitionRetry.IsSafeToRetry);
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
            new NoopReferenceAuthority(),
            resolver);
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

    private sealed class TenantWideScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));

        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class MutableScopeResolver(TenantWorkScopeRequest current) : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeRequest Current { get; set; } = current;

        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, Current));

        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
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

    private sealed class ToggleAuditSink : IFoundationAuditEvidenceSink
    {
        private string? failedOperation;
        private int occurrence;
        private int failAt = 1;

        public void FailOn(string operation, int occurrence = 1)
        {
            failedOperation = operation;
            failAt = occurrence;
            this.occurrence = 0;
        }

        public void Reset()
        {
            failedOperation = null;
            occurrence = 0;
            failAt = 1;
        }

        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default)
        {
            if (string.Equals(evidence.OperationId, failedOperation, StringComparison.Ordinal)
                && ++occurrence == failAt)
                throw new FoundationAuditAppendException("evidence_store_unavailable");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingAuditSink(string operationId) : IFoundationAuditEvidenceSink
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int blocked;

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        internal void Release() => release.TrySetResult(true);

        public async ValueTask AppendAsync(
            FoundationAuditEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(evidence.OperationId, operationId, StringComparison.Ordinal)
                && Interlocked.Exchange(ref blocked, 1) == 0)
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }
    }
}
