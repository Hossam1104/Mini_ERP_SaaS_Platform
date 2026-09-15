using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationExecutionTests
{
    [Fact]
    public async Task Approved_owner_commit_replays_same_key_without_a_second_owner_effect()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);

        var first = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "execution-key",
            prepared.Run.Version);
        var second = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "execution-key",
            prepared.Run.Version);

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(MigrationResultKind.Replayed, second.Kind);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
        Assert.Single(second.Value!.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.Committed);
        Assert.Equal(MigrationRunStatus.Completed, second.Value.RunStatus);
    }

    [Fact]
    public async Task Execution_requires_an_approved_run_before_owner_effect()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create],
            approve: false);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "approval-key",
            prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Rejected, result.Kind);
        Assert.Equal("migration_execution_approval_required", result.Code);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
    }

    [Fact]
    public async Task Same_key_changed_authoritative_fingerprint_conflicts_without_a_new_effect()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);

        var first = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "fingerprint-key",
            prepared.Run.Version);
        fixture.Validation.Validation = fixture.Validation.Validation! with { ValidationResultId = Guid.NewGuid() };
        var conflict = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "fingerprint-key",
            prepared.Run.Version);

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(MigrationResultKind.Rejected, conflict.Kind);
        Assert.Equal("migration_idempotency_conflict", conflict.Code);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
    }

    [Fact]
    public async Task Concurrent_same_key_execution_converges_across_service_instances()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);

        var results = await Task.WhenAll(
            fixture.Service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "concurrent-key", prepared.Run.Version),
            fixture.NewService().ExecuteAsync(fixture.Request, prepared.Run.RunId, "concurrent-key", prepared.Run.Version));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Code));
        Assert.Single(results, result => result.Kind == MigrationResultKind.Succeeded);
        Assert.Single(results, result => result.Kind == MigrationResultKind.Replayed);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
        Assert.Equal(1, fixture.Owner.BatchCount);
    }

    [Fact]
    public async Task Different_keys_against_one_run_claim_one_execution_before_owner_effect()
    {
        await using var fixture = await ExecutionFixture.CreateAsync(blockFirstOwnerExecute: true);
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);

        var firstTask = fixture.Service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "claim-key-a", prepared.Run.Version);
        await fixture.Owner.FirstExecuteEntered.Task;
        var secondTask = fixture.NewService().ExecuteAsync(fixture.Request, prepared.Run.RunId, "claim-key-b", prepared.Run.Version);
        var second = await secondTask;
        fixture.Owner.ReleaseFirstExecute();
        var first = await firstTask;
        var results = new[] { first, second };

        Assert.Single(results, result => result.Kind == MigrationResultKind.Succeeded);
        Assert.Single(results, result => result.Kind == MigrationResultKind.Rejected);
        Assert.Contains(results, result => result.Code is "migration_execution_batch_claim_conflict" or "migration_execution_approval_required");
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
        Assert.Equal(1, fixture.Owner.BatchCount);
    }

    [Fact]
    public async Task Owner_drift_is_a_known_failure_without_silent_skip_or_duplicate()
    {
        await using var fixture = await ExecutionFixture.CreateAsync(driftOnExecute: true);
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "drift-key",
            prepared.Run.Version);
        var evidence = await fixture.Service.ReadAsync(fixture.Request, prepared.Run.RunId);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("migration_execution_group_failed", result.Code);
        Assert.Equal(MigrationRunStatus.Failed, evidence!.RunStatus);
        Assert.Single(evidence.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.Failed);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
    }

    [Fact]
    public async Task Migration_audit_failure_after_owner_commit_is_unknown_and_never_replays_owner_write()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);
        var service = fixture.NewService(new FailAfterAuditSink(3));

        var first = await service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "audit-key", prepared.Run.Version);
        var second = await service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "audit-key", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.UnknownOutcome, second.Kind);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
        Assert.Equal(1, fixture.Owner.BatchCount);
    }

    [Fact]
    public async Task Unsupported_record_is_rejected_before_attempt_or_owner_effect()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Organization, "{\"code\":\"ORG-1\"}")],
            [MigrationPlannedAction.MatchReference]);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "unsupported-key",
            prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Rejected, result.Kind);
        Assert.Equal("migration_execution_record_type_not_supported", result.Code);
        Assert.Equal(0, fixture.Owner.CreateCalls);
        Assert.DoesNotContain(
            await fixture.Persistence.ListAttemptsAsync(fixture.Tenant, prepared.Run.RunId),
            item => item.Operation == MigrationOperationKind.Execution);
        Assert.Empty(await fixture.Persistence.ListBatchesAsync(fixture.Tenant, prepared.Run.RunId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Earlier_owner_group_commit_is_retained_when_a_later_group_fails()
    {
        await using var fixture = await ExecutionFixture.CreateAsync(failingKinds: new HashSet<OwnerResourceKind> { OwnerResourceKind.Customer });
        var prepared = await fixture.PrepareAsync(
            [
                fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}"),
                fixture.Record(MigrationCanonicalRecordType.Customer, "{\"code\":\"CUS-1\",\"nameEnglish\":\"Customer\"}")
            ],
            [MigrationPlannedAction.Create, MigrationPlannedAction.Create]);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "partial-key",
            prepared.Run.Version);
        var evidence = await fixture.Service.ReadAsync(fixture.Request, prepared.Run.RunId);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("migration_execution_partially_completed", result.Code);
        Assert.Equal(MigrationRunStatus.PartiallyCompleted, evidence!.RunStatus);
        Assert.Contains(evidence.Effects, item => item.RecordType == MigrationCanonicalRecordType.Supplier && item.Disposition == MigrationExecutionEffectDisposition.Committed);
        Assert.Contains(evidence.Effects, item => item.RecordType == MigrationCanonicalRecordType.Customer && item.Disposition == MigrationExecutionEffectDisposition.Failed);
        Assert.Equal(2, fixture.Owner.ExecuteCalls);
    }

    [Fact]
    public async Task Unknown_owner_read_recovers_the_same_batch_without_a_second_commit()
    {
        await using var fixture = await ExecutionFixture.CreateAsync(hideEvidenceAfterExecute: true);
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);

        var first = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "crash-key",
            prepared.Run.Version);
        var recovered = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "crash-key",
            prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.Replayed, recovered.Kind);
        Assert.Equal("migration_execution_recovered_owner_evidence", recovered.Code);
        Assert.Equal(1, fixture.Owner.CreateCalls);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
        Assert.Single(recovered.Value!.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.Committed);
    }

    private sealed class ExecutionFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ExecutionFixture(
            SqliteConnection connection,
            TenantContext tenant,
            FoundationRequestContext request,
            MigrationPersistence persistence,
            MigrationExecutionService service,
            OwnerGateway owner,
            InMemoryValidationPersistence validation)
        {
            this.connection = connection;
            Tenant = tenant;
            Request = request;
            Persistence = persistence;
            Service = service;
            Owner = owner;
            Validation = validation;
        }

        internal TenantContext Tenant { get; }
        internal FoundationRequestContext Request { get; }
        internal MigrationPersistence Persistence { get; }
        internal MigrationExecutionService Service { get; }
        internal OwnerGateway Owner { get; }
        internal InMemoryValidationPersistence Validation { get; }

        internal static async Task<ExecutionFixture> CreateAsync(
            IReadOnlySet<OwnerResourceKind>? failingKinds = null,
            bool hideEvidenceAfterExecute = false,
            bool driftOnExecute = false,
            bool blockFirstOwnerExecute = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenant = TenantContext.ForOrdinaryMembership(
                new TenantId(Guid.NewGuid()),
                new MembershipReference(Guid.NewGuid()),
                correlationId: new CorrelationId("migration-execution-test"),
                actorId: Guid.NewGuid());
            var request = FoundationRequestContext.ForTenant(
                tenant.ActorId!.Value,
                Guid.NewGuid(),
                tenant,
                "tenant.migration.execute");
            await using (var db = new MigrationDbContext(options, tenant))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var persistence = new MigrationPersistence(options);
            var owner = new OwnerGateway(hideEvidenceAfterExecute, failingKinds ?? new HashSet<OwnerResourceKind>(), driftOnExecute, blockFirstOwnerExecute);
            var validation = new InMemoryValidationPersistence(persistence);
            var service = new MigrationExecutionService(
                new MigrationFoundationService(persistence, new NoopAuditSink()),
                persistence,
                validation,
                persistence,
                new TenantWideScopeResolver(),
                new TenantWideScopeResolver(),
                owner);
            return new ExecutionFixture(connection, tenant, request, persistence, service, owner, validation);
        }

        internal MigrationExecutionService NewService(IFoundationAuditEvidenceSink? auditSink = null) => new(
            new MigrationFoundationService(Persistence, auditSink ?? new NoopAuditSink()),
            Persistence,
            Validation,
            Persistence,
            new TenantWideScopeResolver(),
            new TenantWideScopeResolver(),
            Owner);

        internal MigrationStagedRecord Record(MigrationCanonicalRecordType type, string payload)
        {
            var sourceObjectId = Guid.NewGuid();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
            return new MigrationStagedRecord(
                Guid.NewGuid(),
                Tenant.TenantId,
                Guid.Empty,
                0,
                type.ToString(),
                type,
                payload,
                hash,
                PackageHash,
                MigrationCanonicalPackageParser.Version,
                sourceObjectId,
                SourceHash,
                DateTimeOffset.UtcNow);
        }

        internal async Task<PreparedRun> PrepareAsync(
            IReadOnlyList<MigrationStagedRecord> input,
            IReadOnlyList<MigrationPlannedAction> actions,
            bool approve = true)
        {
            var run = MigrationRun.Create(
                Tenant,
                new MigrationDefinitionReference("tenant-onboarding.foundation", "1"),
                new MigrationSourceProfileReference("neutral-source-profile", "1"));
            var source = new MigrationSourceArtifactSnapshot(
                Guid.NewGuid(),
                Tenant.TenantId,
                null,
                null,
                null,
                SourceHash,
                1,
                1);
            var intakeKey = new MigrationIdempotencyKey($"intake-{Guid.NewGuid():N}");
            var intake = await Persistence.CreateIntakeAsync(
                Tenant,
                new CreateMigrationIntakeCommand(
                    run,
                    MigrationOperationKind.Validation,
                    intakeKey,
                    new MigrationRequestFingerprint("intake-fingerprint"),
                    MigrationIntakeFingerprint.Version,
                    source));
            Assert.True(intake.Succeeded, intake.Code);
            var intakeEvidence = await Persistence.SetEvidenceStateAsync(
                Tenant,
                new MigrationEvidenceReference(intake.Value!.Run.RunId, MigrationOperationKind.Validation, intakeKey.Value),
                true);
            Assert.True(intakeEvidence.Succeeded, intakeEvidence.Code);
            var persisted = (await Persistence.FindRunAsync(Tenant, intake.Value!.Run.RunId))!;
            var staged = input.Select((item, index) => item with
            {
                RunId = persisted.RunId,
                SourceSequence = index + 1,
                SourceObjectId = source.ObjectId,
                SourceSnapshotHash = SourceHash
            }).ToArray();
            var stagedResult = await Persistence.StagePackageAsync(
                Tenant,
                new StageMigrationPackageCommand(
                    persisted.RunId,
                    source.ObjectId,
                    SourceHash,
                    PackageHash,
                    MigrationCanonicalPackageParser.Version,
                    DateTimeOffset.UtcNow,
                    staged));
            Assert.True(stagedResult.Succeeded, stagedResult.Code);
            Validation.Staged = staged;

            var current = await TransitionAsync(persisted, MigrationRunStatus.Prepared);
            current = await TransitionAsync(current, MigrationRunStatus.Validating);
            var validationAttempt = await StartAttemptAsync(current, MigrationOperationKind.Validation, "validation-key", "validation-fingerprint");
            var validation = new MigrationValidationSummary(
                Guid.NewGuid(),
                Tenant.TenantId,
                current.RunId,
                validationAttempt.AttemptId,
                PackageHash,
                SourceHash,
                staged.Length,
                staged.Length,
                0,
                0,
                new Dictionary<string, int>(),
                staged.Select((item, index) => new MigrationValidationRecordResult(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, [])).ToArray(),
                DateTimeOffset.UtcNow);
            var validationSaved = await Persistence.SaveValidationAsync(Tenant, new SaveMigrationValidationCommand(validation, []));
            Assert.True(validationSaved.Succeeded, validationSaved.Code);
            Validation.Validation = validation;
            await CompleteAttemptAsync(current, validationAttempt, MigrationAttemptOutcome.Succeeded, "validation_completed");
            current = (await Persistence.FindRunAsync(Tenant, current.RunId))!;
            current = await TransitionAsync(current, MigrationRunStatus.Validated);

            var dryAttempt = await StartAttemptAsync(current, MigrationOperationKind.DryRun, "dry-run-key", "dry-run-fingerprint");
            var dryRun = new MigrationDryRunPreview(
                Guid.NewGuid(),
                Tenant.TenantId,
                current.RunId,
                dryAttempt.AttemptId,
                validationAttempt.AttemptId,
                PackageHash,
                SourceHash,
                staged.Length,
                staged.Length,
                0,
                0,
                new Dictionary<string, int>(),
                new Dictionary<string, decimal>(),
                0,
                0,
                staged.Select((item, index) => new MigrationPreviewRow(item.StagedRecordId, item.SourceSequence, item.RecordType, MigrationRecordDisposition.Accepted, actions[index], null)).ToArray(),
                DateTimeOffset.UtcNow);
            var drySaved = await Persistence.SaveDryRunAsync(Tenant, new SaveMigrationDryRunCommand(dryRun));
            Assert.True(drySaved.Succeeded, drySaved.Code);
            Validation.DryRun = dryRun;
            await CompleteAttemptAsync(current, dryAttempt, MigrationAttemptOutcome.Succeeded, "dry_run_completed");
            current = (await Persistence.FindRunAsync(Tenant, current.RunId))!;
            if (approve)
                current = await TransitionAsync(current, MigrationRunStatus.Approved);
            return new PreparedRun(current, staged);
        }

        private async Task<MigrationRunRecord> TransitionAsync(MigrationRunRecord current, MigrationRunStatus target)
        {
            var result = await new MigrationFoundationService(Persistence, new NoopAuditSink()).TransitionRunAsync(
                Request,
                current.RunId,
                target,
                current.Version);
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        private async Task<MigrationAttemptRecord> StartAttemptAsync(MigrationRunRecord current, MigrationOperationKind operation, string key, string fingerprint)
        {
            var result = await new MigrationFoundationService(Persistence, new NoopAuditSink()).StartAttemptAsync(
                Request,
                current.RunId,
                operation,
                key,
                fingerprint);
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        private async Task CompleteAttemptAsync(MigrationRunRecord current, MigrationAttemptRecord attempt, MigrationAttemptOutcome outcome, string code)
        {
            var result = await new MigrationFoundationService(Persistence, new NoopAuditSink()).RecordAttemptOutcomeAsync(
                Request,
                current.RunId,
                attempt.AttemptId,
                outcome,
                code,
                attempt.Version);
            Assert.True(result.Succeeded, result.Code);
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }

        private const string PackageHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        private const string SourceHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    }

    private sealed class InMemoryValidationPersistence : IMigrationValidationPersistence
    {
        private readonly MigrationPersistence persistence;

        internal InMemoryValidationPersistence(MigrationPersistence persistence) => this.persistence = persistence;

        internal IReadOnlyList<MigrationStagedRecord> Staged { get; set; } = [];
        internal MigrationValidationSummary? Validation { get; set; }
        internal MigrationDryRunPreview? DryRun { get; set; }

        public Task<MigrationIntakeRecord?> FindIntakeAsync(TenantContext tenantContext, Guid runId, CancellationToken cancellationToken = default) =>
            persistence.FindIntakeAsync(tenantContext, runId, cancellationToken);

        public Task<IReadOnlyList<MigrationStagedRecord>> ListStagedRecordsAsync(TenantContext tenantContext, Guid runId, int offset = 0, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MigrationStagedRecord>>(Staged);

        public Task<MigrationValidationSummary?> FindLatestValidationAsync(TenantContext tenantContext, Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Validation);

        public Task<MigrationDryRunPreview?> FindLatestDryRunAsync(TenantContext tenantContext, Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DryRun);

        public Task<MigrationPersistenceResult<MigrationStagingResult>> StagePackageAsync(TenantContext tenantContext, StageMigrationPackageCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MigrationPersistenceResult<MigrationValidationSummary>> SaveValidationAsync(TenantContext tenantContext, SaveMigrationValidationCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MigrationValidationSummary?> FindValidationAsync(TenantContext tenantContext, Guid runId, Guid attemptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Validation);

        public Task<IReadOnlyList<MigrationValidationFinding>> ListFindingsAsync(TenantContext tenantContext, Guid runId, Guid? attemptId = null, int offset = 0, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MigrationValidationFinding>>([]);

        public Task<MigrationPersistenceResult<MigrationDryRunPreview>> SaveDryRunAsync(TenantContext tenantContext, SaveMigrationDryRunCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MigrationDryRunPreview?> FindDryRunAsync(TenantContext tenantContext, Guid runId, Guid attemptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DryRun);
    }

    private sealed record PreparedRun(MigrationRunRecord Run, IReadOnlyList<MigrationStagedRecord> Staged);

    private sealed class OwnerGateway : IOwnerExecutionGateway
    {
        private readonly object sync = new();
        private readonly SemaphoreSlim mutationGate = new(1, 1);
        private readonly Dictionary<Guid, (OwnerImportRequest Request, OwnerEvidence Evidence)> batches = [];
        private readonly bool hideEvidenceAfterExecute;
        private readonly bool driftOnExecute;
        private readonly bool blockFirstOwnerExecute;
        private readonly TaskCompletionSource<bool> allowFirstExecute = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int firstExecuteWait = 1;
        private bool hideNextRead;

        internal OwnerGateway(
            bool hideEvidenceAfterExecute,
            IReadOnlySet<OwnerResourceKind> failingKinds,
            bool driftOnExecute = false,
            bool blockFirstOwnerExecute = false)
        {
            this.hideEvidenceAfterExecute = hideEvidenceAfterExecute;
            FailingKinds = failingKinds;
            this.driftOnExecute = driftOnExecute;
            this.blockFirstOwnerExecute = blockFirstOwnerExecute;
        }

        internal IReadOnlySet<OwnerResourceKind> FailingKinds { get; }
        internal int CreateCalls { get; private set; }
        internal int ExecuteCalls { get; private set; }
        internal int BatchCount { get { lock (sync) return batches.Count; } }
        internal TaskCompletionSource<bool> FirstExecuteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void ReleaseFirstExecute() => allowFirstExecute.TrySetResult(true);

        public Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(FoundationRequestContext context, OwnerImportRequest request, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                CreateCalls++;
                if (!batches.TryGetValue(request.BatchId, out var current))
                {
                    current = (request, Evidence(request, OwnerBatchStatus.Draft, []));
                    batches[request.BatchId] = current;
                }
                return Task.FromResult(Success(current.Evidence.Batch));
            }
        }

        public Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(FoundationRequestContext context, Guid batchId, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                var current = batches[batchId];
                if (current.Evidence.Batch.Status is OwnerBatchStatus.Completed or OwnerBatchStatus.CompletedWithErrors)
                    return Task.FromResult(Success(current.Evidence.Batch));
                var evidence = Evidence(current.Request, OwnerBatchStatus.Validated, current.Evidence.Rows);
                batches[batchId] = (current.Request, evidence);
                return Task.FromResult(Success(evidence.Batch));
            }
        }

        public async Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(FoundationRequestContext context, Guid batchId, byte[] expectedVersion, CancellationToken cancellationToken = default)
        {
            await mutationGate.WaitAsync(cancellationToken);
            try
            {
                if (blockFirstOwnerExecute && Interlocked.Exchange(ref firstExecuteWait, 0) == 1)
                {
                    FirstExecuteEntered.TrySetResult(true);
                    await allowFirstExecute.Task.WaitAsync(cancellationToken);
                }

                lock (sync)
                {
                    var current = batches[batchId];
                    if (current.Evidence.Batch.Status is OwnerBatchStatus.Completed or OwnerBatchStatus.CompletedWithErrors)
                        return Success(current.Evidence.Batch);
                    ExecuteCalls++;
                    var failed = driftOnExecute || FailingKinds.Contains(current.Request.ResourceKind);
                    var rows = current.Request.Rows.Select(item => new OwnerRowEvidence(
                        item.RowNumber,
                        failed ? OwnerRowOutcome.Rejected : OwnerRowOutcome.Accepted,
                        failed ? OwnerMutationDisposition.Failed : OwnerMutationDisposition.Committed,
                        Guid.NewGuid(),
                        failed ? null : Guid.NewGuid(),
                        failed ? null : $"{current.Request.ResourceKind}-{item.RowNumber}",
                        failed ? [new OwnerDiagnosticEvidence("owner_duplicate")] : [])).ToArray();
                    batches[batchId] = (current.Request, Evidence(current.Request, failed ? OwnerBatchStatus.CompletedWithErrors : OwnerBatchStatus.Completed, rows));
                    hideNextRead = hideEvidenceAfterExecute;
                    return Success(batches[batchId].Evidence.Batch);
                }
            }
            finally
            {
                mutationGate.Release();
            }
        }

        public Task<OwnerEvidence?> ReadEvidenceAsync(FoundationRequestContext context, Guid batchId, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                if (hideNextRead)
                {
                    hideNextRead = false;
                    return Task.FromResult<OwnerEvidence?>(null);
                }
                return Task.FromResult<OwnerEvidence?>(batches.TryGetValue(batchId, out var value) ? value.Evidence : null);
            }
        }

        private static OwnerEvidence Evidence(OwnerImportRequest request, OwnerBatchStatus status, IReadOnlyList<OwnerRowEvidence> rows) =>
            new(new OwnerBatchEvidence(request.BatchId, status, Guid.NewGuid().ToByteArray()), rows);

        private static OwnerOperationResult<OwnerBatchEvidence> Success(OwnerBatchEvidence value) =>
            new(true, "owner_ok", value);
    }

    private sealed class TenantWideScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));

        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FailAfterAuditSink(int successfulAppends) : IFoundationAuditEvidenceSink
    {
        private int appends;

        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref appends) > successfulAppends)
                throw new FoundationAuditAppendException("audit_failure");
            return ValueTask.CompletedTask;
        }
    }
}
