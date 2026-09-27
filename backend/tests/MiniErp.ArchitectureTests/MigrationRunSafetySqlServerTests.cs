using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationRunSafetySqlServerSafetyTests(SqlServerSafetyFixture fixture)
{
    private static readonly MigrationDefinitionReference Definition = new("tenant-onboarding.foundation", "1");
    private static readonly MigrationSourceProfileReference Profile = new("neutral-source-profile", "1");

    [Fact]
    public async Task MESP169_sql_server_non_authoritative_outcomes_are_distinct_and_leave_owner_snapshots_unchanged()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenant = NewTenant("mesp169-preview");
        var request = Request(tenant);
        var persistence = new MigrationPersistence(options);
        var audit = new CapturingAuditSink();
        var resolver = new MutableScopeResolver(TenantWorkScopeRequest.TenantWide());
        var objectId = Guid.NewGuid();
        var content = Package(objectId,
            new { SourceSequence = 1, SourceRecordId = "product-1", RecordType = "Product", Payload = new { Sku = "M169-P1", NameEnglish = "Preview product 1" } },
            new { SourceSequence = 2, SourceRecordId = "product-2", RecordType = "Product", Payload = new { Sku = "M169-P2", NameEnglish = "Preview product 2" } });
        var storage = Storage(tenant, objectId, content);
        var intake = await RegisterAsync(persistence, storage, resolver, audit, request, objectId);
        Assert.True(intake.Succeeded, intake.Code);
        var runId = intake.Value!.Run.RunId;
        var service = ValidationService(persistence, storage, resolver, new RecordingReferenceAuthority());

        var before = SnapshotOwnerStores(connection.ConnectionString, tenant);
        var validation = await service.ValidateAsync(request, runId, "mesp169-validation-only");
        Assert.True(validation.Succeeded, validation.Code);
        Assert.Equal("validation-only", validation.Value!.Outcome);
        Assert.DoesNotContain(validation.Value.Records, item => item.CanonicalPayload is null);
        AssertOwnerSnapshotUnchanged(before, connection.ConnectionString, tenant);

        before = SnapshotOwnerStores(connection.ConnectionString, tenant);
        var dryRun = await service.DryRunAsync(request, runId, "mesp169-dry-run");
        Assert.True(dryRun.Succeeded, dryRun.Code);
        Assert.Equal("dry-run", dryRun.Value!.Outcome);
        AssertOwnerSnapshotUnchanged(before, connection.ConnectionString, tenant);

        before = SnapshotOwnerStores(connection.ConnectionString, tenant);
        var preview = await service.ReadPreviewAsync(tenant, runId);
        Assert.NotNull(preview);
        Assert.Equal("preview", preview!.Outcome);
        Assert.False(preview.AuthoritativeImport);
        Assert.False(preview.ApprovalCreated);
        Assert.False(preview.ReadinessCreated);
        Assert.False(preview.RunStateChanged);
        Assert.Equal(2, preview.ExpectedAdditions);
        Assert.Equal(0, preview.DuplicateOutcomes);
        Assert.Equal(dryRun.Value.UnresolvedDependencyCount, preview.UnresolvedDependencies);
        Assert.Equal(
            dryRun.Value.ControlTotals.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            preview.ControlTotals.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());
        Assert.Equal(dryRun.Value.ExceptionCount, preview.Exceptions);
        Assert.Equal(2, preview.Rows.Count);
        AssertOwnerSnapshotUnchanged(before, connection.ConnectionString, tenant);

        var reconciliation = new MigrationReconciliationService(
            new MigrationFoundationService(persistence, audit),
            persistence,
            persistence,
            persistence,
            service,
            execution: null!,
            approvalPolicy: null!,
            audit: audit);
        before = SnapshotOwnerStores(connection.ConnectionString, tenant);
        var reconciliationPreview = await reconciliation.ReadPreviewAsync(request, runId);
        Assert.NotNull(reconciliationPreview);
        Assert.Equal("reconciliation-preview", reconciliationPreview!.Outcome);
        Assert.False(reconciliationPreview.AuthoritativeImport);
        Assert.False(reconciliationPreview.ApprovalCreated);
        Assert.False(reconciliationPreview.ReadinessCreated);
        Assert.False(reconciliationPreview.RunStateChanged);
        Assert.Equal(preview.Rows, reconciliationPreview.Rows);
        AssertOwnerSnapshotUnchanged(before, connection.ConnectionString, tenant);
    }

    [Fact]
    public async Task MESP169_sql_server_cancellation_is_reasoned_idempotent_audited_and_fail_closed()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenant = NewTenant("mesp169-cancel");
        var request = Request(tenant);
        var persistence = new MigrationPersistence(options);
        var audit = new CapturingAuditSink();
        var resolver = new MutableScopeResolver(TenantWorkScopeRequest.TenantWide());
        var objectId = Guid.NewGuid();
        var content = Package(objectId,
            new { SourceSequence = 1, SourceRecordId = "cancel-product", RecordType = "Product", Payload = new { Sku = "M169-C1", NameEnglish = "Cancellation product" } });
        var storage = Storage(tenant, objectId, content);
        var intake = await RegisterAsync(persistence, storage, resolver, audit, request, objectId);
        Assert.True(intake.Succeeded, intake.Code);
        var foundation = new MigrationFoundationService(persistence, audit);
        var run = (await foundation.FindRunAsync(tenant, intake.Value!.Run.RunId)).Value!;
        var validation = ValidationService(persistence, storage, resolver, new RecordingReferenceAuthority());
        var cancellation = new MigrationRunSafetyService(foundation, validation, persistence);

        var foreignTenant = NewTenant("mesp169-foreign");
        var foreign = await cancellation.CancelAsync(
            Request(foreignTenant), run.RunId, "foreign-cancel", run.Version, "foreign request");
        Assert.False(foreign.Succeeded);

        resolver.Current = TenantWorkScopeRequest.ForCompany(Guid.NewGuid());
        var wrongScope = await cancellation.CancelAsync(request, run.RunId, "wrong-scope", run.Version, "wrong scope");
        Assert.Equal("migration_source_scope_denied", wrongScope.Code);
        resolver.Current = TenantWorkScopeRequest.TenantWide();

        var missingReason = await cancellation.CancelAsync(request, run.RunId, "missing-reason", run.Version, "  ");
        Assert.Equal("migration_cancellation_request_invalid", missingReason.Code);

        var ownerSnapshotBeforeCancellation = SnapshotOwnerStores(connection.ConnectionString, tenant);
        var cancelled = await cancellation.CancelAsync(request, run.RunId, "cancel-once", run.Version, "  owner requested stop  ");
        Assert.True(cancelled.Succeeded, cancelled.Code);
        Assert.Equal(MigrationRunStatus.Cancelled, cancelled.Value!.RunStatus);
        Assert.Equal("owner requested stop", cancelled.Value.Reason);
        AssertOwnerSnapshotUnchanged(ownerSnapshotBeforeCancellation, connection.ConnectionString, tenant);
        var stored = await persistence.FindRunAsync(tenant, run.RunId);
        Assert.Equal(MigrationRunStatus.Cancelled, stored!.Status);
        Assert.Equal("owner requested stop", stored.CancellationReason);
        Assert.Contains(audit.Evidence, item => item.OperationId == "migration.run.cancel");

        var replay = await cancellation.CancelAsync(request, run.RunId, "cancel-once", run.Version, "owner requested stop");
        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(cancelled.Value.AttemptId, replay.Value!.AttemptId);
        Assert.Equal("owner requested stop", replay.Value.Reason);
        var conflict = await cancellation.CancelAsync(request, run.RunId, "cancel-once", run.Version, "different reason");
        Assert.Equal("migration_idempotency_conflict", conflict.Code);

        var unknownObjectId = Guid.NewGuid();
        var unknownContent = Package(unknownObjectId,
            new { SourceSequence = 1, SourceRecordId = "unknown-product", RecordType = "Product", Payload = new { Sku = "M169-U1", NameEnglish = "Unknown outcome product" } });
        var unknownStorage = Storage(tenant, unknownObjectId, unknownContent);
        var unknownIntake = await RegisterAsync(persistence, unknownStorage, resolver, audit, request, unknownObjectId);
        Assert.True(unknownIntake.Succeeded, unknownIntake.Code);
        var unknownRun = (await foundation.FindRunAsync(tenant, unknownIntake.Value!.Run.RunId)).Value!;
        foreach (var status in new[]
                 {
                     MigrationRunStatus.Prepared,
                     MigrationRunStatus.Validating,
                     MigrationRunStatus.Validated,
                     MigrationRunStatus.Approved,
                     MigrationRunStatus.Executing,
                     MigrationRunStatus.OutcomeUnknown
                 })
        {
            var transition = await foundation.TransitionRunAsync(request, unknownRun.RunId, status, unknownRun.Version);
            Assert.True(transition.Succeeded, transition.Code);
            unknownRun = transition.Value!;
        }

        var unknownCancellation = await cancellation.CancelAsync(
            request, unknownRun.RunId, "cancel-unknown", unknownRun.Version, "cannot prove outcome");
        Assert.Equal("migration_cancellation_effect_boundary_crossed", unknownCancellation.Code);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, (await persistence.FindRunAsync(tenant, unknownRun.RunId))!.Status);
    }

    [Fact]
    public async Task MESP169_sql_server_corrected_retry_only_revalidates_rejected_rows_and_preserves_history()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenant = NewTenant("mesp169-correction");
        var request = Request(tenant);
        var persistence = new MigrationPersistence(options);
        var audit = new CapturingAuditSink();
        var resolver = new MutableScopeResolver(TenantWorkScopeRequest.TenantWide());
        var objectId = Guid.NewGuid();
        var content = Package(objectId,
            new { SourceSequence = 1, SourceRecordId = "rejected-product", RecordType = "Product", Payload = new { Sku = "M169-R1", NameEnglish = (string?)null } },
            new { SourceSequence = 2, SourceRecordId = "accepted-product", RecordType = "Product", Payload = new { Sku = "M169-A1", NameEnglish = "Already accepted" } });
        var storage = Storage(tenant, objectId, content);
        var intake = await RegisterAsync(persistence, storage, resolver, audit, request, objectId);
        Assert.True(intake.Succeeded, intake.Code);
        var serviceReferences = new RecordingReferenceAuthority();
        var service = ValidationService(persistence, storage, resolver, serviceReferences);
        var first = await service.ValidateAsync(request, intake.Value!.Run.RunId, "mesp169-initial-validation");
        Assert.Equal("migration_validation_failed", first.Code);

        var initial = (await persistence.FindLatestValidationAsync(tenant, intake.Value.Run.RunId))!;
        var rejected = Assert.Single(initial.Records, item => item.Disposition == MigrationRecordDisposition.Rejected);
        var accepted = Assert.Single(initial.Records, item => item.Disposition == MigrationRecordDisposition.Accepted);
        var failedRun = (await persistence.FindRunAsync(tenant, intake.Value.Run.RunId))!;

        var acceptedRetry = await service.RetryCorrectedAsync(
            request,
            failedRun.RunId,
            "mesp169-accepted-row-retry",
            failedRun.Version,
            [new MigrationCorrectionSubmission(accepted.StagedRecordId, "{\"sku\":\"M169-A1\",\"nameEnglish\":\"Duplicate attempt\"}", "Owner supplied")]);
        Assert.Equal("migration_correction_row_ineligible", acceptedRetry.Code);
        Assert.Single(await persistence.ListAttemptsAsync(tenant, failedRun.RunId), item => item.Operation == MigrationOperationKind.Validation);

        serviceReferences.Sequences.Clear();
        var correction = new MigrationCorrectionSubmission(
            rejected.StagedRecordId,
            "{\"sku\":\"M169-R1\",\"nameEnglish\":\"Corrected product\"}",
            "Owner supplied by requester");
        var retried = await service.RetryCorrectedAsync(
            request, failedRun.RunId, "mesp169-corrected-retry", failedRun.Version, [correction]);
        Assert.True(retried.Succeeded, retried.Code);
        Assert.Equal("validation-only", retried.Value!.Outcome);
        Assert.Equal(2, retried.Value.AcceptedCount);
        Assert.Equal(new[] { 1 }, serviceReferences.Sequences);
        var corrected = Assert.Single(retried.Value.Records, item => item.StagedRecordId == rejected.StagedRecordId);
        Assert.Equal(MigrationRecordDisposition.Accepted, corrected.Disposition);
        Assert.Equal("rejected-product", corrected.SourceRecordId);
        Assert.Equal("Owner supplied by requester", corrected.CorrectionOwner);
        Assert.Contains("Corrected product", corrected.CanonicalPayload, StringComparison.Ordinal);
        var preserved = Assert.Single(retried.Value.Records, item => item.StagedRecordId == accepted.StagedRecordId);
        Assert.Equal(MigrationRecordDisposition.Accepted, preserved.Disposition);
        Assert.Equal(accepted.CanonicalPayload, preserved.CanonicalPayload);

        var staged = await persistence.ListStagedRecordsAsync(tenant, failedRun.RunId, 0, 100);
        Assert.Equal(rejected.CanonicalPayload, Assert.Single(staged, item => item.StagedRecordId == rejected.StagedRecordId).CanonicalPayload);
        var findings = await persistence.ListFindingsAsync(tenant, failedRun.RunId, null, 0, 100);
        Assert.Contains(findings, item => item.AttemptId == initial.AttemptId && item.StagedRecordId == rejected.StagedRecordId);
        Assert.Equal(2, (await persistence.ListAttemptsAsync(tenant, failedRun.RunId)).Count(item => item.Operation == MigrationOperationKind.Validation));
        Assert.DoesNotContain(await persistence.ListAttemptsAsync(tenant, failedRun.RunId), item => item.Operation == MigrationOperationKind.Execution);

        var replay = await service.RetryCorrectedAsync(
            request, failedRun.RunId, "mesp169-corrected-retry", failedRun.Version, [correction]);
        Assert.Equal(MigrationResultKind.Replayed, replay.Kind);
        Assert.Equal(retried.Value.AttemptId, replay.Value!.AttemptId);
    }

    [Fact]
    public async Task MESP169_sql_server_quarantine_persists_all_five_required_fields()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(
            connection.ConnectionString,
            SqlServerMigrationConfiguration.MigrationHistoryTable);
        var tenant = NewTenant("mesp169-quarantine");
        var request = Request(tenant);
        var persistence = new MigrationPersistence(options);
        var audit = new CapturingAuditSink();
        var resolver = new MutableScopeResolver(TenantWorkScopeRequest.TenantWide());
        var objectId = Guid.NewGuid();
        var content = Package(objectId,
            new { SourceSequence = 1, SourceRecordId = "quarantined-product", RecordType = "Product", CorrectionOwner = "Owner supplied in source", Payload = new { Sku = "M169-Q1", NameEnglish = "Quarantined product" } },
            new { SourceSequence = 2, SourceRecordId = (string?)null, RecordType = "Product", Payload = new { Sku = "M169-Q2", NameEnglish = "Missing quarantine metadata" } });
        var storage = Storage(tenant, objectId, content);
        var intake = await RegisterAsync(persistence, storage, resolver, audit, request, objectId);
        Assert.True(intake.Succeeded, intake.Code);
        var references = new RecordingReferenceAuthority(_ => new MigrationReferenceCheck(
            MigrationReferenceState.Unavailable,
            MigrationFindingCategory.Reference,
            "migration_reference_authority_unavailable",
            "The owner reference could not be verified."));
        var service = ValidationService(persistence, storage, resolver, references);

        var result = await service.ValidateAsync(request, intake.Value!.Run.RunId, "mesp169-quarantine-validation");
        Assert.Equal("migration_validation_failed", result.Code);
        var saved = (await persistence.FindLatestValidationAsync(tenant, intake.Value.Run.RunId))!;
        Assert.Equal(1, saved.QuarantinedCount);
        Assert.Equal(1, saved.RejectedCount);
        var row = Assert.Single(saved.Records, item => item.Disposition == MigrationRecordDisposition.Quarantined);
        Assert.Equal(MigrationRecordDisposition.Quarantined, row.Disposition);
        Assert.Equal("quarantined-product", row.SourceRecordId);
        Assert.Equal("Owner supplied in source", row.CorrectionOwner);
        Assert.Equal("Reference", row.ErrorClass);
        Assert.Equal("The owner reference could not be verified.", row.ActionableMessage);
        var missingMetadata = Assert.Single(saved.Records, item => item.Disposition == MigrationRecordDisposition.Rejected);
        Assert.Contains("migration_source_record_id_required", missingMetadata.FindingCodes);
        Assert.Contains("migration_correction_owner_required", missingMetadata.FindingCodes);
    }

    private static TenantContext NewTenant(string correlation) => TenantContext.ForOrdinaryMembership(
        new TenantId(Guid.NewGuid()),
        new MembershipReference(Guid.NewGuid()),
        correlationId: new CorrelationId(correlation),
        actorId: Guid.NewGuid());

    private static FoundationRequestContext Request(TenantContext tenant) => FoundationRequestContext.ForTenant(
        tenant.ActorId!.Value,
        Guid.NewGuid(),
        tenant,
        "tenant.migration.intake");

    private static async Task<MigrationOperationResult<MigrationIntakeRecord>> RegisterAsync(
        MigrationPersistence persistence,
        IPrivateObjectStorage storage,
        ICurrentOrganizationScopeResolver resolver,
        IFoundationAuditEvidenceSink audit,
        FoundationRequestContext request,
        Guid objectId) => await new MigrationIntakeService(persistence, storage, resolver, audit).RegisterAsync(
            request,
            new MigrationIntakeRegistrationRequest(Definition, Profile, MigrationOperationKind.Validation, objectId),
            $"mesp169-intake-{Guid.NewGuid():N}");

    private static MigrationValidationService ValidationService(
        MigrationPersistence persistence,
        IPrivateObjectStorage storage,
        ICurrentOrganizationScopeResolver resolver,
        IMigrationReferenceAuthority references) => new(
            new MigrationFoundationService(persistence, new CapturingAuditSink()),
            persistence,
            storage,
            resolver,
            references,
            (IOrganizationScopeOwnershipResolver)resolver);

    private static StaticPrivateObjectStorage Storage(TenantContext tenant, Guid objectId, byte[] content) => new(
        tenant,
        objectId,
        content,
        new MigrationSourceArtifactSnapshot(objectId, tenant.TenantId, null, null, null, new string('A', 64), content.Length, 1));

    private static byte[] Package(Guid objectId, params object[] records)
    {
        var length = 0;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var content = JsonSerializer.SerializeToUtf8Bytes(new
            {
                PackageVersion = MigrationCanonicalPackageParser.Version,
                DefinitionId = Definition.DefinitionId,
                DefinitionVersion = Definition.Version,
                SourceProfileId = Profile.ProfileId,
                SourceProfileVersion = Profile.ProfileVersion,
                LogicalDataset = "MESP-169",
                SourceSnapshot = new { ObjectId = objectId, Sha256 = new string('A', 64), Length = length, ConcurrencyVersion = 1 },
                Records = records
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (content.Length == length)
                return content;
            length = content.Length;
        }

        throw new InvalidOperationException("The MESP-169 test package length did not stabilize.");
    }

    private static string[] SnapshotOwnerStores(string connectionString, TenantContext tenant)
    {
        using var masterData = new MasterDataDbContext(SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable), tenant);
        using var parties = new BusinessPartiesDbContext(SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.BusinessPartiesHistoryTable), tenant);
        using var procurement = new ProcurementDbContext(SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.ProcurementHistoryTable), tenant);
        using var inventory = new InventoryDbContext(SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.InventoryHistoryTable), tenant);
        using var finance = new FinanceDbContext(SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable), tenant);
        using var sales = new SalesDbContext(SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.SalesHistoryTable), tenant);
        var contexts = new DbContext[] { masterData, parties, procurement, inventory, finance, sales };
        var rows = new List<string>();
        foreach (var context in contexts)
        {
            foreach (var entityType in context.Model.GetEntityTypes()
                         .Where(item => item.GetTableName() is not null && !item.IsOwned() && item.BaseType is null)
                         .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                var keyProperties = entityType.FindPrimaryKey()?.Properties ?? [];
                var properties = entityType.GetProperties().OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
                foreach (var entity in QueryEntities(context, entityType.ClrType))
                {
                    var key = keyProperties.Select(item => SnapshotValue(item.GetGetter().GetClrValue(entity))).ToArray();
                    var values = properties.Select(item => new
                    {
                        item.Name,
                        Value = SnapshotValue(item.GetGetter().GetClrValue(entity))
                    }).ToArray();
                    rows.Add(JsonSerializer.Serialize(new
                    {
                        Store = context.GetType().Name,
                        Entity = entityType.Name,
                        Key = key,
                        Values = values
                    }));
                }
            }
        }

        return rows.OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    private static string SnapshotValue(object? value) => value switch
    {
        null => "null",
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => JsonSerializer.Serialize(value, value.GetType())
    };

    private static IEnumerable<object> QueryEntities(DbContext context, Type entityType)
    {
        var set = typeof(DbContext).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == nameof(DbContext.Set)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0)
            .MakeGenericMethod(entityType)
            .Invoke(context, null)!;
        return ((IQueryable)set).Cast<object>();
    }

    private static void AssertOwnerSnapshotUnchanged(string[] before, string connectionString, TenantContext tenant) =>
        Assert.Equal(before, SnapshotOwnerStores(connectionString, tenant));

    private sealed class StaticPrivateObjectStorage(
        TenantContext tenant,
        Guid objectId,
        byte[] content,
        MigrationSourceArtifactSnapshot source) : IPrivateObjectStorage
    {
        private readonly PrivateFileMetadata metadata = new(
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

        public ValueTask<PrivateFileMetadata> StoreAsync(TenantContext tenantContext, TenantWorkScope scope, string originalFileName, string contentType, Stream contentStream, DateTimeOffset? expiresAt = null, PrivateFileSafetyRequirement safetyRequirement = PrivateFileSafetyRequirement.ExternalScanRequired, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<PrivateFileAccessResult> ReadAsync(TenantContext tenantContext, Guid requestedObjectId, CancellationToken cancellationToken = default) =>
            tenantContext.TenantId == tenant.TenantId && requestedObjectId == objectId
                ? ValueTask.FromResult(PrivateFileAccessResult.AllowedResult(metadata, content))
                : ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));

        public ValueTask<PrivateFileOverwriteResult> OverwriteAsync(TenantContext tenantContext, Guid requestedObjectId, long expectedConcurrencyVersion, Stream contentStream, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MutableScopeResolver(TenantWorkScopeRequest current) : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeRequest Current { get; set; } = current;

        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, Current));

        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class RecordingReferenceAuthority(Func<MigrationParsedCanonicalRow, MigrationReferenceCheck?>? result = null)
        : IMigrationReferenceAuthority
    {
        public List<int> Sequences { get; } = [];

        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(
            FoundationRequestContext requestContext,
            MigrationParsedCanonicalRow row,
            CancellationToken cancellationToken = default)
        {
            Sequences.Add(row.SourceSequence);
            var check = result?.Invoke(row);
            return Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>(check is null ? [] : [check]);
        }
    }

    private sealed class CapturingAuditSink : IFoundationAuditEvidenceSink
    {
        public List<FoundationAuditEvidence> Evidence { get; } = [];

        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default)
        {
            Evidence.Add(evidence);
            return ValueTask.CompletedTask;
        }
    }
}
