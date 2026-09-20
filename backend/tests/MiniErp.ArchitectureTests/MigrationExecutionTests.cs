using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationExecutionTests
{
    [Fact]
    public void Migration_execution_start_catalog_declares_the_complete_mutation_contract()
    {
        var operation = FoundationOperationCatalog.GetRequired(MigrationExecutionService.OperationId);

        Assert.Equal(FoundationConcurrencyPolicy.IfMatch, operation.Concurrency);
        Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
        Assert.True(operation.RequiresAntiforgery);
        Assert.True(operation.RequiresMandatoryAudit);
        Assert.True(operation.IsUnsafe);
        Assert.Equal("tenant.migration.execute", operation.ExactPermissionCode);
    }

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
        Assert.Equal(MigrationExecutionService.HistoricalFingerprintVersion, first.Value!.FingerprintVersion);
    }

    [Fact]
    public async Task Historical_fingerprint_bytes_match_the_pre_slice6_contract_and_ar_selects_v2()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);
        var intake = (await fixture.Persistence.FindIntakeAsync(fixture.Tenant, prepared.Run.RunId))!;
        var validation = fixture.Validation.Validation!;
        var dryRun = fixture.Validation.DryRun!;
        var scope = TenantWorkScope.IssueFromVerifiedAuthority(fixture.Tenant, TenantWorkScopeRequest.TenantWide());
        var common = new[]
        {
            prepared.Run.RunId.ToString("D"),
            prepared.Run.TenantId.Value.ToString("D"),
            prepared.Run.Definition.DefinitionId,
            prepared.Run.Definition.Version,
            prepared.Run.SourceProfile.ProfileId,
            prepared.Run.SourceProfile.ProfileVersion,
            intake.Source.Sha256,
            validation.ValidationResultId.ToString("D"),
            validation.PackageHash,
            dryRun.PreviewId.ToString("D"),
            dryRun.AttemptId.ToString("D"),
            dryRun.PackageHash,
            $"Tenant:{prepared.Run.TenantId.Value:D}",
            "master-data-owner-import-v1"
        };

        var legacy = MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, []);
        var inventory = MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.InventoryOpening]);
        var ar = MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.ArOpening]);
        var ap = MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.ApOpening]);
        var mixed = MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.InventoryOpening, MigrationCanonicalRecordType.ArOpening, MigrationCanonicalRecordType.ApOpening]);
        var cash = MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.CashBankOpening]);
        var mixedWithCash = MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.CashBankOpening, MigrationCanonicalRecordType.ApOpening, MigrationCanonicalRecordType.InventoryOpening, MigrationCanonicalRecordType.ArOpening]);

        Assert.Equal(MigrationExecutionService.HistoricalFingerprintVersion, legacy.Version);
        Assert.Equal(MigrationFingerprintEncoder.Compute(MigrationExecutionService.HistoricalFingerprintVersion, common), legacy.Fingerprint);
        Assert.Equal(MigrationExecutionService.HistoricalFingerprintVersion, inventory.Version);
        Assert.Equal(MigrationFingerprintEncoder.Compute(MigrationExecutionService.HistoricalFingerprintVersion, [.. common, "inventory-economic-opening-v1"]), inventory.Fingerprint);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, ar.Version);
        Assert.Equal(MigrationFingerprintEncoder.Compute(MigrationExecutionService.FingerprintVersion, [.. common, "ar-economic-opening-v1"]), ar.Fingerprint);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, ap.Version);
        Assert.Equal(MigrationFingerprintEncoder.Compute(MigrationExecutionService.FingerprintVersion, [.. common, "ap-economic-opening-v1"]), ap.Fingerprint);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, mixed.Version);
        Assert.Equal(MigrationFingerprintEncoder.Compute(MigrationExecutionService.FingerprintVersion, [.. common, "inventory-economic-opening-v1", "ap-economic-opening-v1", "ar-economic-opening-v1"]), mixed.Fingerprint);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, cash.Version);
        Assert.Equal(MigrationFingerprintEncoder.Compute(MigrationExecutionService.FingerprintVersion, [.. common, "cash-bank-economic-opening-v1"]), cash.Fingerprint);
        Assert.Equal(MigrationExecutionService.FingerprintVersion, mixedWithCash.Version);
        Assert.Equal(MigrationFingerprintEncoder.Compute(MigrationExecutionService.FingerprintVersion, [.. common, "inventory-economic-opening-v1", "ap-economic-opening-v1", "ar-economic-opening-v1", "cash-bank-economic-opening-v1"]), mixedWithCash.Fingerprint);
        Assert.NotEqual(ar.Fingerprint, ap.Fingerprint);
        Assert.NotEqual(cash.Fingerprint, ar.Fingerprint);
        Assert.NotEqual(cash.Fingerprint, ap.Fingerprint);
        Assert.Equal(mixedWithCash.Fingerprint, MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.ApOpening, MigrationCanonicalRecordType.CashBankOpening, MigrationCanonicalRecordType.ArOpening, MigrationCanonicalRecordType.InventoryOpening]).Fingerprint);
        Assert.Equal(ap.Fingerprint, MigrationExecutionService.ComputeFingerprint(prepared.Run, intake, validation, dryRun, scope, [MigrationCanonicalRecordType.ApOpening]).Fingerprint);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Ar_effect_uncertainty_requires_complete_owner_readback(bool throwAfterCreate, bool hideReadback, bool expectedCommitted)
    {
        var proxy = DispatchProxy.Create<IFinanceSettlementPersistence, ArFinanceProxy>();
        var finance = (ArFinanceProxy)(object)proxy;
        finance.ThrowAfterCreate = throwAfterCreate;
        finance.HideReadback = hideReadback;
        await using var fixture = await ExecutionFixture.CreateAsync(arFinance: proxy);
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var prepared = await PrepareArAsync(fixture, companyId, customerId, "AR-UNIT-001", 100m);

        var result = await fixture.Service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "ar-unit-key", prepared.Run.Version);
        var evidence = await fixture.Service.ReadAsync(fixture.Request, prepared.Run.RunId);

        Assert.Equal(1, finance.CreateCalls);
        if (expectedCommitted)
        {
            Assert.True(result.Succeeded, result.Code);
            Assert.Equal(MigrationExecutionEffectDisposition.Committed, Assert.Single(result.Value!.Effects, item => item.RecordType == MigrationCanonicalRecordType.ArOpening).Disposition);
            Assert.Equal(MigrationRunStatus.Completed, evidence!.RunStatus);
        }
        else
        {
            Assert.Equal(MigrationResultKind.UnknownOutcome, result.Kind);
            Assert.Equal(MigrationRunStatus.OutcomeUnknown, evidence!.RunStatus);
            Assert.Contains(evidence.Effects, item => item.RecordType == MigrationCanonicalRecordType.ArOpening && item.Disposition == MigrationExecutionEffectDisposition.Unknown);
        }
    }

    [Fact]
    public async Task Ar_outcome_unknown_is_a_hard_stop_for_same_and_different_keys()
    {
        var proxy = DispatchProxy.Create<IFinanceSettlementPersistence, ArFinanceProxy>();
        var finance = (ArFinanceProxy)(object)proxy;
        finance.HideReadback = true;
        await using var fixture = await ExecutionFixture.CreateAsync(arFinance: proxy);
        var prepared = await PrepareArAsync(fixture, Guid.NewGuid(), Guid.NewGuid(), "AR-UNIT-STOP", 100m);

        var first = await fixture.Service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "ar-stop-key", prepared.Run.Version);
        var sameKey = await fixture.Service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "ar-stop-key", prepared.Run.Version);
        var differentKey = await fixture.Service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "ar-stop-different", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.UnknownOutcome, sameKey.Kind);
        Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
        Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
        Assert.Equal(1, finance.CreateCalls);
    }

    private static async Task<PreparedRun> PrepareArAsync(ExecutionFixture fixture, Guid companyId, Guid customerId, string sourceReference, decimal amount)
    {
        var payload = JsonSerializer.Serialize(new
        {
            companyId,
            customerId,
            sourceReference,
            documentDate = new DateOnly(2026, 1, 10),
            openingDate = new DateOnly(2026, 1, 15),
            amount,
            currencyCode = "SAR",
            dueDate = new DateOnly(2026, 2, 14)
        });
        return await fixture.PrepareAsync([fixture.Record(MigrationCanonicalRecordType.ArOpening, payload)], [MigrationPlannedAction.Create]);
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
    public async Task Owner_preflight_failure_stops_before_executing_or_owner_effect()
    {
        await using var fixture = await ExecutionFixture.CreateAsync(failOwnerCreate: true);
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "preflight-key",
            prepared.Run.Version);
        var run = await fixture.Persistence.FindRunAsync(fixture.Tenant, prepared.Run.RunId);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("owner_preflight_failed", result.Code);
        Assert.Equal(MigrationRunStatus.Approved, run!.Status);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
        var attempts = await fixture.Persistence.ListAttemptsAsync(fixture.Tenant, prepared.Run.RunId);
        var attempt = Assert.Single(attempts, item => item.Operation == MigrationOperationKind.Execution);
        Assert.All(
            await fixture.Persistence.ListBatchesAsync(fixture.Tenant, prepared.Run.RunId, attempt.AttemptId),
            item => Assert.Equal(MigrationExecutionBatchState.Failed, item.State));
        Assert.All(
            await fixture.Persistence.ListEffectsAsync(fixture.Tenant, prepared.Run.RunId, attempt.AttemptId),
            item => Assert.Equal(MigrationExecutionEffectDisposition.Failed, item.Disposition));
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
    public async Task Per_row_sequence_or_type_drift_is_rejected_before_owner_preflight()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);
        fixture.Validation.Validation = fixture.Validation.Validation! with
        {
            Records = [fixture.Validation.Validation.Records[0] with { SourceSequence = 99 }]
        };

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "snapshot-key",
            prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Rejected, result.Kind);
        Assert.Equal("migration_execution_authoritative_snapshot_mismatch", result.Code);
        Assert.Equal(0, fixture.Owner.CreateCalls);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
    }

    [Theory]
    [InlineData("validation-sequence")]
    [InlineData("dry-run-sequence")]
    [InlineData("validation-type")]
    [InlineData("dry-run-type")]
    [InlineData("staged-id")]
    [InlineData("staged-sequence")]
    [InlineData("validation-id")]
    [InlineData("validation-sequence-duplicate")]
    [InlineData("dry-run-id")]
    [InlineData("dry-run-sequence-duplicate")]
    public async Task Authoritative_snapshot_negative_matrix_rejects_before_attempt_or_owner_preflight(string mutation)
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [
                fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}"),
                fixture.Record(MigrationCanonicalRecordType.Customer, "{\"code\":\"CUS-1\",\"nameEnglish\":\"Customer\"}")
            ],
            [MigrationPlannedAction.Create, MigrationPlannedAction.Create]);

        var staged = prepared.Staged.ToArray();
        var validation = fixture.Validation.Validation!;
        var dryRun = fixture.Validation.DryRun!;
        switch (mutation)
        {
            case "validation-sequence":
                fixture.Validation.Validation = validation with { Records = [validation.Records[0] with { SourceSequence = 99 }, validation.Records[1]] };
                break;
            case "dry-run-sequence":
                fixture.Validation.DryRun = dryRun with { Rows = [dryRun.Rows[0] with { SourceSequence = 99 }, dryRun.Rows[1]] };
                break;
            case "validation-type":
                fixture.Validation.Validation = validation with { Records = [validation.Records[0] with { RecordType = MigrationCanonicalRecordType.Customer }, validation.Records[1]] };
                break;
            case "dry-run-type":
                fixture.Validation.DryRun = dryRun with { Rows = [dryRun.Rows[0] with { RecordType = MigrationCanonicalRecordType.Customer }, dryRun.Rows[1]] };
                break;
            case "staged-id":
                fixture.Validation.Staged = [staged[0], staged[1] with { StagedRecordId = staged[0].StagedRecordId }];
                break;
            case "staged-sequence":
                fixture.Validation.Staged = [staged[0], staged[1] with { SourceSequence = staged[0].SourceSequence }];
                break;
            case "validation-id":
                fixture.Validation.Validation = validation with { Records = [validation.Records[0], validation.Records[1] with { StagedRecordId = validation.Records[0].StagedRecordId }] };
                break;
            case "validation-sequence-duplicate":
                fixture.Validation.Validation = validation with { Records = [validation.Records[0], validation.Records[1] with { SourceSequence = validation.Records[0].SourceSequence }] };
                break;
            case "dry-run-id":
                fixture.Validation.DryRun = dryRun with { Rows = [dryRun.Rows[0], dryRun.Rows[1] with { StagedRecordId = dryRun.Rows[0].StagedRecordId }] };
                break;
            case "dry-run-sequence-duplicate":
                fixture.Validation.DryRun = dryRun with { Rows = [dryRun.Rows[0], dryRun.Rows[1] with { SourceSequence = dryRun.Rows[0].SourceSequence }] };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            $"snapshot-{mutation}",
            prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Rejected, result.Kind);
        Assert.Equal("migration_execution_authoritative_snapshot_mismatch", result.Code);
        Assert.Equal(0, fixture.Owner.CreateCalls);
        Assert.Equal(0, fixture.Owner.SimulateCalls);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
        Assert.DoesNotContain(
            await fixture.Persistence.ListAttemptsAsync(fixture.Tenant, prepared.Run.RunId),
            item => item.Operation == MigrationOperationKind.Execution);
    }

    [Theory]
    [InlineData(MigrationCanonicalRecordType.Currency)]
    [InlineData(MigrationCanonicalRecordType.Tax)]
    [InlineData(MigrationCanonicalRecordType.PaymentTerm)]
    [InlineData(MigrationCanonicalRecordType.UnitOfMeasure)]
    public async Task Reference_only_drift_fails_before_executing(MigrationCanonicalRecordType type)
    {
        await using var fixture = await ExecutionFixture.CreateAsync(referenceState: MigrationReferenceState.Active);
        var parsedReference = new MigrationParsedCanonicalRow(
            1,
            "reference-1",
            type,
            new MigrationReferencePayload(Guid.Parse("00000000-0000-0000-0000-000000000001"), "REF-1"),
            "{}");
        Assert.Equal(MigrationReferenceState.Active, Assert.Single(await fixture.References.ValidateAsync(fixture.Request, parsedReference)).State);
        fixture.References.State = MigrationReferenceState.Missing;
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(type, "{\"referenceId\":\"00000000-0000-0000-0000-000000000001\",\"code\":\"REF-1\"}")],
            [MigrationPlannedAction.MatchReference]);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            $"reference-drift-{type}",
            prepared.Run.Version);
        var read = await fixture.Service.ReadAsync(fixture.Request, prepared.Run.RunId);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("migration_reference_drift", result.Code);
        Assert.Equal(MigrationRunStatus.Approved, read!.RunStatus);
        Assert.Equal(0, fixture.Owner.CreateCalls);
        Assert.Equal(0, fixture.Owner.SimulateCalls);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
        Assert.Single(read.Batches, item => item.State == MigrationExecutionBatchState.Failed);
        Assert.Single(read.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.Failed);
    }

    [Theory]
    [InlineData(MigrationCanonicalRecordType.Currency)]
    [InlineData(MigrationCanonicalRecordType.Tax)]
    [InlineData(MigrationCanonicalRecordType.PaymentTerm)]
    [InlineData(MigrationCanonicalRecordType.UnitOfMeasure)]
    public async Task Valid_reference_only_rows_become_non_effects(MigrationCanonicalRecordType type)
    {
        await using var fixture = await ExecutionFixture.CreateAsync(referenceState: MigrationReferenceState.Active);
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(type, "{\"referenceId\":\"00000000-0000-0000-0000-000000000001\",\"code\":\"REF-1\"}")],
            [MigrationPlannedAction.MatchReference]);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            $"reference-valid-{type}",
            prepared.Run.Version);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MigrationRunStatus.Completed, result.Value!.RunStatus);
        Assert.Single(result.Value.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.NonEffect);
        Assert.Equal(0, fixture.Owner.CreateCalls);
        Assert.Equal(0, fixture.Owner.SimulateCalls);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
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
        Assert.Contains(results, result => result.Code is "migration_execution_attempt_claim_conflict" or "migration_execution_batch_claim_conflict" or "migration_execution_approval_required");
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
        Assert.Equal(1, fixture.Owner.BatchCount);
    }

    [Fact]
    public async Task Different_key_is_known_conflict_while_execution_start_audit_is_pending()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.Supplier, "{\"code\":\"SUP-1\",\"nameEnglish\":\"Supplier\"}")],
            [MigrationPlannedAction.Create]);
        var audit = new BlockingExecutionTransitionAuditSink();
        var firstTask = fixture.NewService(audit).ExecuteAsync(
            fixture.Request, prepared.Run.RunId, "audit-claim-key-a", prepared.Run.Version);
        await audit.WaitAsync();

        MigrationOperationResult<MigrationExecutionResult> second;
        try
        {
            second = await fixture.NewService().ExecuteAsync(
                fixture.Request, prepared.Run.RunId, "audit-claim-key-b", prepared.Run.Version);
        }
        finally
        {
            audit.Release();
        }
        var first = await firstTask;

        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(MigrationResultKind.Rejected, second.Kind);
        Assert.Equal("migration_execution_attempt_claim_conflict", second.Code);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
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

    [Theory]
    [InlineData(MigrationCanonicalRecordType.GlOpening)]
    public async Task Unsupported_economic_opening_types_are_rejected_before_attempt_or_owner_effect(MigrationCanonicalRecordType type)
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(type, "{}")],
            [MigrationPlannedAction.MatchReference]);

        var result = await fixture.Service.ExecuteAsync(
            fixture.Request, prepared.Run.RunId, $"unsupported-economic-{type}", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Rejected, result.Kind);
        Assert.Equal("migration_execution_record_type_not_supported", result.Code);
        Assert.Equal(0, fixture.Owner.CreateCalls);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
        Assert.DoesNotContain(
            await fixture.Persistence.ListAttemptsAsync(fixture.Tenant, prepared.Run.RunId),
            item => item.Operation == MigrationOperationKind.Execution);
    }

    [Fact]
    public async Task Inventory_opening_requires_inventory_and_finance_owner_services_before_attempt()
    {
        await using var fixture = await ExecutionFixture.CreateAsync();
        var prepared = await fixture.PrepareAsync(
            [fixture.Record(MigrationCanonicalRecordType.InventoryOpening, "{\"companyId\":\"11111111-1111-1111-1111-111111111111\",\"branchId\":\"22222222-2222-2222-2222-222222222222\",\"warehouseId\":\"33333333-3333-3333-3333-333333333333\",\"productId\":\"44444444-4444-4444-4444-444444444444\",\"unitOfMeasureId\":\"55555555-5555-5555-5555-555555555555\",\"quantity\":1,\"unitCost\":2,\"currencyCode\":\"SAR\",\"openingDate\":\"2026-01-01\",\"sourceLineReference\":\"opening-1\"}")],
            [MigrationPlannedAction.Create]);

        var result = await fixture.Service.ExecuteAsync(fixture.Request, prepared.Run.RunId, "inventory-services-key", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.Rejected, result.Kind);
        Assert.Equal("migration_inventory_execution_unavailable", result.Code);
        Assert.DoesNotContain(await fixture.Persistence.ListAttemptsAsync(fixture.Tenant, prepared.Run.RunId), item => item.Operation == MigrationOperationKind.Execution);
        Assert.Equal(0, fixture.Owner.CreateCalls);
        Assert.Equal(0, fixture.Owner.ExecuteCalls);
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
    public async Task Outcome_unknown_is_a_hard_stop_without_automatic_reconciliation_or_retry()
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
        var retry = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "crash-key",
            prepared.Run.Version);
        var differentKey = await fixture.Service.ExecuteAsync(
            fixture.Request,
            prepared.Run.RunId,
            "different-crash-key",
            prepared.Run.Version);

        Assert.Equal(MigrationResultKind.UnknownOutcome, first.Kind);
        Assert.Equal(MigrationResultKind.UnknownOutcome, retry.Kind);
        Assert.Equal("migration_execution_outcome_unknown", retry.Code);
        Assert.Equal(1, fixture.Owner.CreateCalls);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
        Assert.DoesNotContain(retry.Value!.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.Committed);
        Assert.Contains(retry.Value.Effects, item => item.Disposition == MigrationExecutionEffectDisposition.Unknown);
        Assert.Equal(MigrationRunStatus.OutcomeUnknown, retry.Value.RunStatus);
        Assert.Equal(MigrationResultKind.Rejected, differentKey.Kind);
        Assert.Equal("migration_run_requires_reconciliation", differentKey.Code);
        Assert.Equal(1, fixture.Owner.ExecuteCalls);
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
            InMemoryValidationPersistence validation,
            TestReferenceAuthority references,
            IFinanceSettlementPersistence? arFinance)
        {
            this.connection = connection;
            Tenant = tenant;
            Request = request;
            Persistence = persistence;
            Service = service;
            Owner = owner;
            Validation = validation;
            References = references;
            ArFinance = arFinance;
        }

        internal TenantContext Tenant { get; }
        internal FoundationRequestContext Request { get; }
        internal MigrationPersistence Persistence { get; }
        internal MigrationExecutionService Service { get; }
        internal OwnerGateway Owner { get; }
        internal InMemoryValidationPersistence Validation { get; }
        internal TestReferenceAuthority References { get; }
        internal IFinanceSettlementPersistence? ArFinance { get; }

        internal static async Task<ExecutionFixture> CreateAsync(
            IReadOnlySet<OwnerResourceKind>? failingKinds = null,
            bool hideEvidenceAfterExecute = false,
            bool driftOnExecute = false,
            bool blockFirstOwnerExecute = false,
            bool failOwnerCreate = false,
            MigrationReferenceState referenceState = MigrationReferenceState.NotApplicable,
            IFinanceSettlementPersistence? arFinance = null)
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
            var owner = new OwnerGateway(hideEvidenceAfterExecute, failingKinds ?? new HashSet<OwnerResourceKind>(), driftOnExecute, blockFirstOwnerExecute, failOwnerCreate);
            var validation = new InMemoryValidationPersistence(persistence);
            var references = new TestReferenceAuthority(referenceState);
            var service = new MigrationExecutionService(
                new MigrationFoundationService(persistence, new NoopAuditSink()),
                persistence,
                validation,
                persistence,
                new TenantWideScopeResolver(),
                new TenantWideScopeResolver(),
                owner,
                references,
                null,
                arFinance is null ? null : new MigrationArOpeningExecutionCoordinator(persistence, references, arFinance));
            return new ExecutionFixture(connection, tenant, request, persistence, service, owner, validation, references, arFinance);
        }

        internal MigrationExecutionService NewService(IFoundationAuditEvidenceSink? auditSink = null) => new(
            new MigrationFoundationService(Persistence, auditSink ?? new NoopAuditSink()),
            Persistence,
            Validation,
            Persistence,
            new TenantWideScopeResolver(),
            new TenantWideScopeResolver(),
            Owner,
            References,
            null,
            ArFinance is null ? null : new MigrationArOpeningExecutionCoordinator(Persistence, References, ArFinance));

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
        private readonly bool failOwnerCreate;
        private readonly TaskCompletionSource<bool> allowFirstExecute = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int firstExecuteWait = 1;
        private bool hideNextRead;

        internal OwnerGateway(
            bool hideEvidenceAfterExecute,
            IReadOnlySet<OwnerResourceKind> failingKinds,
            bool driftOnExecute = false,
            bool blockFirstOwnerExecute = false,
            bool failOwnerCreate = false)
        {
            this.hideEvidenceAfterExecute = hideEvidenceAfterExecute;
            FailingKinds = failingKinds;
            this.driftOnExecute = driftOnExecute;
            this.blockFirstOwnerExecute = blockFirstOwnerExecute;
            this.failOwnerCreate = failOwnerCreate;
        }

        internal IReadOnlySet<OwnerResourceKind> FailingKinds { get; }
        internal int CreateCalls { get; private set; }
        internal int SimulateCalls { get; private set; }
        internal int ExecuteCalls { get; private set; }
        internal int BatchCount { get { lock (sync) return batches.Count; } }
        internal TaskCompletionSource<bool> FirstExecuteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void ReleaseFirstExecute() => allowFirstExecute.TrySetResult(true);

        public Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(FoundationRequestContext context, OwnerImportRequest request, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                CreateCalls++;
                if (failOwnerCreate)
                    return Task.FromResult(new OwnerOperationResult<OwnerBatchEvidence>(false, "owner_preflight_failed", null, 422));
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
                SimulateCalls++;
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

    private class ArFinanceProxy : DispatchProxy
    {
        internal int CreateCalls { get; private set; }
        internal bool ThrowAfterCreate { get; set; }
        internal bool HideReadback { get; set; }
        private FinanceMigrationArOpeningEvidence? evidence;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IFinanceSettlementPersistence.PreflightMigrationArOpeningAsync) => Preflight((FinanceRequestContext)args![0]!, (FinanceMigrationArOpeningCommand)args[1]!),
                nameof(IFinanceSettlementPersistence.CreateMigrationArOpeningAsync) => Create((FinanceRequestContext)args![0]!, (FinanceMigrationArOpeningCommand)args[1]!),
                nameof(IFinanceSettlementPersistence.ReadMigrationArOpeningAsync) => Read(),
                _ => DefaultTask(targetMethod?.ReturnType ?? typeof(Task))
            };
        }

        private static Task<FinanceArOpeningPreflightResult> Preflight(FinanceRequestContext context, FinanceMigrationArOpeningCommand command) =>
            Task.FromResult(new FinanceArOpeningPreflightResult(true, "ready", "SAR", FinanceApprovalRequirement.NotRequired, command.DueDate, null));

        private Task<FinanceOperationResult<FinanceOpenItemRecord>> Create(FinanceRequestContext context, FinanceMigrationArOpeningCommand command)
        {
            CreateCalls++;
            evidence ??= BuildEvidence(context, command);
            if (ThrowAfterCreate) throw new InvalidOperationException("test response lost after commit");
            return Task.FromResult(FinanceOperationResult<FinanceOpenItemRecord>.Success(evidence.OpenItem));
        }

        private Task<FinanceMigrationArOpeningEvidence?> Read() =>
            Task.FromResult(HideReadback ? null : evidence);

        private static FinanceMigrationArOpeningEvidence BuildEvidence(FinanceRequestContext context, FinanceMigrationArOpeningCommand command)
        {
            var itemId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            var evidenceId = Guid.NewGuid();
            var debitAccount = Guid.NewGuid();
            var creditAccount = Guid.NewGuid();
            var openItem = new FinanceOpenItemRecord(itemId, context.TenantId.Value, command.CompanyId, FinanceOpenItemKind.Receivable, null, command.CustomerId, "migration-ar-opening.v1", evidenceId, 1, evidenceId, 1, command.SourceReference.Trim(), command.DocumentDate, command.DueDate!.Value, "SAR", command.Amount, "SAR", command.Amount, 1m, null, null, null, null, null, null, FinanceOpenItemRecognitionState.Recognized, journalId, 0m, command.Amount, FinanceOpenItemStatus.Open, Guid.NewGuid().ToByteArray());
            var lines = new FinanceJournalLineRecord[]
            {
                new(Guid.NewGuid(), 1, debitAccount, "AR", "AR", command.Amount, 0m, command.Amount, 0m, command.Amount, "SAR", null, null, "AR opening"),
                new(Guid.NewGuid(), 2, creditAccount, "OFFSET", "OFFSET", 0m, command.Amount, 0m, command.Amount, command.Amount, "SAR", null, null, "AR opening")
            };
            var journal = new FinanceJournalRecord(journalId, context.TenantId.Value, command.CompanyId, 1, "00000001", command.DocumentDate, command.OpeningDate, null, null, "SAR", "SAR", 1m, null, null, null, "migration-ar-opening.v1", "recognition", evidenceId, 1, Guid.NewGuid(), 1, "AR opening", FinanceJournalStatus.Posted, context.ActorId, null, null, context.ActorId, null, null, null, context.CorrelationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, lines, Guid.NewGuid().ToByteArray(), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
            var sourceEffect = new FinanceSourceEffectRecord(Guid.NewGuid(), context.TenantId.Value, command.CompanyId, "migration-ar-opening.v1", evidenceId, 1, journalId, DateTimeOffset.UtcNow);
            return new FinanceMigrationArOpeningEvidence(openItem, journal, sourceEffect);
        }

        private static object? DefaultTask(Type returnType)
        {
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>)) return null;
            var valueType = returnType.GetGenericArguments()[0];
            var value = valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(valueType).Invoke(null, [value]);
        }
    }

    private sealed class TenantWideScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));

        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class TestReferenceAuthority(MigrationReferenceState state) : IMigrationReferenceAuthority
    {
        internal MigrationReferenceState State { get; set; } = state;

        public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(
            FoundationRequestContext requestContext,
            MigrationParsedCanonicalRow row,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>(
                [new(State, MigrationFindingCategory.Reference, State == MigrationReferenceState.NotApplicable ? "reference_not_required" : "migration_reference_drift", "Test reference authority state.")]);
    }

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class BlockingExecutionTransitionAuditSink : IFoundationAuditEvidenceSink
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default)
        {
            if (evidence.OperationId != "migration.run.transition")
                return;

            entered.TrySetResult(true);
            await released.Task.WaitAsync(cancellationToken);
        }

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        internal void Release() => released.TrySetResult(true);
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
