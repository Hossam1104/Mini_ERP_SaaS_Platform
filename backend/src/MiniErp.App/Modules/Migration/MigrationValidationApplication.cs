#pragma warning disable CS1591

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

public sealed class MigrationValidationService
{
    public const string ValidationOperationId = "migration.validation.start";
    public const string DryRunOperationId = "migration.dry-run.start";
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMigrationFoundationPersistence foundation;
    private readonly IMigrationValidationPersistence persistence;
    private readonly IPrivateObjectStorage privateStorage;
    private readonly ICurrentOrganizationScopeResolver scopeResolver;
    private readonly IMigrationReferenceAuthority references;
    private readonly IFoundationAuditEvidenceSink auditSink;
    private readonly TimeProvider timeProvider;

    public MigrationValidationService(
        IMigrationFoundationPersistence foundation,
        IMigrationValidationPersistence persistence,
        IPrivateObjectStorage privateStorage,
        ICurrentOrganizationScopeResolver scopeResolver,
        IMigrationReferenceAuthority references,
        IFoundationAuditEvidenceSink auditSink,
        TimeProvider? timeProvider = null)
    {
        this.foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.privateStorage = privateStorage ?? throw new ArgumentNullException(nameof(privateStorage));
        this.scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MigrationOperationResult<MigrationValidationSummary>> ValidateAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant<MigrationValidationSummary>(requestContext, runId, out var tenant, out var rejected))
            return rejected!;

        var runRecord = await foundation.FindRunAsync(tenant!, runId, cancellationToken);
        var intake = await persistence.FindIntakeAsync(tenant!, runId, cancellationToken);
        if (runRecord is null || intake is null)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_run_not_found");

        var fingerprint = Fingerprint("validation", runRecord, intake);
        var existing = await foundation.FindIdempotencyAsync(tenant!, MigrationOperationKind.Validation, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint.Value, StringComparison.Ordinal))
                return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_idempotency_conflict");

            if (existing.AttemptId is not { } existingAttemptId)
                return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_validation_replay_unavailable");

            var replay = await persistence.FindValidationAsync(tenant!, runId, existingAttemptId, cancellationToken);
            return replay is not null
                ? MigrationOperationResult<MigrationValidationSummary>.Replay(replay)
                : MigrationOperationResult<MigrationValidationSummary>.Failure("migration_validation_in_progress", safeToRetry: true);
        }

        var validatingRunRecord = await PrepareForValidationAsync(requestContext!, tenant!, runRecord, cancellationToken);
        if (validatingRunRecord is null)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_validation_not_permitted");
        var run = MigrationRun.Rehydrate(validatingRunRecord);

        var started = await foundation.StartAttemptAsync(
            tenant!,
            new StartMigrationAttemptCommand(
                runId,
                MigrationOperationKind.Validation,
                new MigrationIdempotencyKey(idempotencyKey),
                fingerprint),
            cancellationToken);
        if (started.Outcome == MigrationPersistenceOutcome.Replayed && started.Value is { } replayAttempt)
        {
            var replay = await persistence.FindValidationAsync(tenant!, runId, replayAttempt.AttemptId, cancellationToken);
            return replay is not null
                ? MigrationOperationResult<MigrationValidationSummary>.Replay(replay)
                : MigrationOperationResult<MigrationValidationSummary>.Failure("migration_validation_in_progress", safeToRetry: true);
        }

        if (started.Value is not { } attempt)
            return MigrationOperationResult<MigrationValidationSummary>.Failure(started.Code, safeToRetry: true);

        if (!await AppendAttemptEvidenceAsync(requestContext!, run!, attempt, ValidationOperationId, started.Code, cancellationToken))
            return MigrationOperationResult<MigrationValidationSummary>.Failure("migration_audit_evidence_unavailable", safeToRetry: true);

        var sourceRead = await privateStorage.ReadAsync(tenant!, intake.Source.ObjectId, cancellationToken);
        if (!sourceRead.Allowed || sourceRead.Metadata is not { } metadata)
        {
            return await FinishFailedValidationAsync(
                requestContext!,
                validatingRunRecord,
                tenant!,
                attempt,
                "migration_source_snapshot_changed",
                "The immutable source snapshot is no longer available or does not match intake.",
                MigrationFindingCategory.FileTemplate,
                cancellationToken);
        }

        var currentScope = scopeResolver.ResolveCurrent(tenant!);
        if (!currentScope.Allowed
            || currentScope.Scope is not { } authorizedScope
            || !authorizedScope.ContainsAuthorizedDescendant(metadata.Scope))
        {
            return await FinishFailedValidationAsync(
                requestContext!,
                validatingRunRecord,
                tenant!,
                attempt,
                "migration_source_scope_denied",
                "The accepted source is outside the current authorized organization scope.",
                MigrationFindingCategory.Scope,
                cancellationToken);
        }

        if (sourceRead.Content is not { } content || !MatchesSnapshot(intake.Source, metadata))
        {
            return await FinishFailedValidationAsync(
                requestContext!,
                validatingRunRecord,
                tenant!,
                attempt,
                "migration_source_snapshot_changed",
                "The immutable source snapshot is no longer available or does not match intake.",
                MigrationFindingCategory.FileTemplate,
                cancellationToken);
        }

        var parsed = MigrationCanonicalPackageParser.Parse(content);
        if (!parsed.Succeeded || parsed.Package is not { } package)
        {
            return await FinishFailedValidationAsync(
                requestContext!,
                validatingRunRecord,
                tenant!,
                attempt,
                parsed.ErrorCode ?? "migration_package_invalid",
                parsed.ErrorMessage ?? "The canonical package could not be validated.",
                parsed.ErrorCode == "migration_package_record_type_unsupported"
                    ? MigrationFindingCategory.Unsupported
                    : MigrationFindingCategory.FileTemplate,
                cancellationToken);
        }

        if (!PackageMatches(package, run!, intake.Source))
        {
            return await FinishFailedValidationAsync(
                requestContext!,
                validatingRunRecord,
                tenant!,
                attempt,
                "migration_package_identity_mismatch",
                "The canonical package does not match the accepted intake contract.",
                MigrationFindingCategory.FileTemplate,
                cancellationToken);
        }

        var packageHash = MigrationCanonicalPackageParser.Hash(package, parsed.Rows);
        var staged = parsed.Rows.Select(row => new MigrationStagedRecord(
            DeterministicId(runId, packageHash, row.SourceSequence),
            tenant!.TenantId,
            runId,
            row.SourceSequence,
            row.SourceRecordId,
            row.RecordType,
            row.PayloadJson,
            Hash(row.PayloadJson),
            packageHash,
            package.PackageVersion,
            intake.Source.ObjectId,
            intake.Source.Sha256,
            timeProvider.GetUtcNow())).ToArray();
        var staging = await persistence.StagePackageAsync(
            tenant!,
            new StageMigrationPackageCommand(
                runId,
                intake.Source.ObjectId,
                intake.Source.Sha256,
                packageHash,
                package.PackageVersion,
                timeProvider.GetUtcNow(),
                staged),
            cancellationToken);
        if (!staging.Succeeded || staging.Value is not { } stagedPackage)
        {
            return await FinishFailedValidationAsync(
                requestContext!,
                validatingRunRecord,
                tenant!,
                attempt,
                staging.Code,
                "The canonical package could not be staged as one immutable snapshot.",
                MigrationFindingCategory.FileTemplate,
                cancellationToken);
        }

        var findings = new List<MigrationValidationFinding>();
        var resultBySequence = new Dictionary<int, (MigrationRecordDisposition Disposition, List<string> Codes)>();
        var duplicateSourceIds = parsed.Rows
            .Where(row => !string.IsNullOrWhiteSpace(row.SourceRecordId))
            .GroupBy(row => row.SourceRecordId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .Select(row => row.SourceSequence)
            .ToHashSet();
        var duplicateBusinessKeys = parsed.Rows
            .Select(row => (Row: row, Key: MigrationValidationRules.BusinessKey(row)))
            .Where(item => item.Key is not null)
            .GroupBy(item => item.Key!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.Row.SourceSequence))
            .ToHashSet();

        foreach (var row in parsed.Rows.OrderBy(item => item.SourceSequence))
        {
            var rowFindings = new List<MigrationValidationFinding>();
            var codes = new List<string>();
            foreach (var rule in MigrationValidationRules.Validate(row))
                AddFinding(rowFindings, codes, stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence), attempt, rule.Category, MigrationFindingSeverity.Error, rule.Code, rule.Message);

            if (ScopeFinding(authorizedScope, row) is { } scopeFinding)
                AddFinding(
                    rowFindings,
                    codes,
                    stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence),
                    attempt,
                    scopeFinding.Category,
                    MigrationFindingSeverity.Error,
                    scopeFinding.Code,
                    scopeFinding.Message);

            if (duplicateSourceIds.Contains(row.SourceSequence) || duplicateBusinessKeys.Contains(row.SourceSequence))
                AddFinding(rowFindings, codes, stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence), attempt, MigrationFindingCategory.Duplicate, MigrationFindingSeverity.Error, "migration_duplicate_source_identity", "The canonical business identity occurs more than once in this package.");

            try
            {
                foreach (var check in await references.ValidateAsync(requestContext!, row, cancellationToken))
                {
                    if (check.State is MigrationReferenceState.NotApplicable or MigrationReferenceState.Active)
                        continue;

                    var severity = check.State is MigrationReferenceState.Ambiguous or MigrationReferenceState.Unavailable
                        ? MigrationFindingSeverity.Warning
                        : MigrationFindingSeverity.Error;
                    AddFinding(
                        rowFindings,
                        codes,
                        stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence),
                        attempt,
                        check.Category,
                        severity,
                        check.Code,
                        check.Message,
                        check.ReferenceId);
                }
            }
            catch
            {
                AddFinding(
                    rowFindings,
                    codes,
                    stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence),
                    attempt,
                    MigrationFindingCategory.Reference,
                    MigrationFindingSeverity.Warning,
                    "migration_reference_authority_unavailable",
                    "A required owner-module reference could not be verified.");
            }

            var disposition = rowFindings.Any(item => item.Severity == MigrationFindingSeverity.Error)
                ? MigrationRecordDisposition.Rejected
                : rowFindings.Count > 0
                    ? MigrationRecordDisposition.Quarantined
                    : MigrationRecordDisposition.Accepted;
            resultBySequence[row.SourceSequence] = (disposition, codes);
            findings.AddRange(rowFindings);
        }

        AddGlBalanceFinding(parsed.Rows, stagedPackage.Records, attempt, resultBySequence, findings);
        var summary = BuildValidationSummary(
            tenant!,
            runId,
            attempt.AttemptId,
            packageHash,
            intake.Source.Sha256,
            stagedPackage.Records,
            resultBySequence,
            findings,
            timeProvider.GetUtcNow());
        var saved = await persistence.SaveValidationAsync(
            tenant!,
            new SaveMigrationValidationCommand(summary, findings),
            cancellationToken);
        if (!saved.Succeeded || saved.Value is not { } completed)
            return MigrationOperationResult<MigrationValidationSummary>.Failure(saved.Code, safeToRetry: true);

        var outcome = completed.IsValid ? MigrationAttemptOutcome.Succeeded : MigrationAttemptOutcome.KnownFailure;
        var outcomeResult = await foundation.RecordAttemptOutcomeAsync(
            tenant!,
            new RecordMigrationAttemptOutcomeCommand(attempt.RunId, attempt.AttemptId, outcome, completed.IsValid ? "validated" : "validation_failed", attempt.Version),
            cancellationToken);
        if (!outcomeResult.Succeeded)
            return MigrationOperationResult<MigrationValidationSummary>.Failure(outcomeResult.Code, safeToRetry: true);

        var target = completed.IsValid ? MigrationRunStatus.Validated : MigrationRunStatus.ValidationFailed;
        var transitioned = await foundation.ApplyRunTransitionAsync(
            tenant!,
            new ApplyMigrationRunTransitionCommand(runId, target, validatingRunRecord.Version),
            cancellationToken);
        if (!transitioned.Succeeded || transitioned.Value is not { } transitionedRun)
            return MigrationOperationResult<MigrationValidationSummary>.Failure(transitioned.Code, safeToRetry: true);

        if (!await AppendRunEvidenceAsync(
                requestContext!,
                MigrationRun.Rehydrate(transitionedRun),
                attempt,
                ValidationOperationId,
                completed.IsValid ? "validated" : "validation_failed",
                cancellationToken))
            return MigrationOperationResult<MigrationValidationSummary>.Failure("migration_audit_evidence_unavailable", safeToRetry: true);

        return completed.IsValid
            ? MigrationOperationResult<MigrationValidationSummary>.Success(completed, "validated")
            : MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_validation_failed");
    }

    public async Task<MigrationOperationResult<MigrationDryRunPreview>> DryRunAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant<MigrationDryRunPreview>(requestContext, runId, out var tenant, out var rejected))
            return rejected!;

        var runRecord = await foundation.FindRunAsync(tenant!, runId, cancellationToken);
        var validation = await persistence.FindLatestValidationAsync(tenant!, runId, cancellationToken);
        if (runRecord is null || validation is null || !validation.IsValid)
            return MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_validation_required");

        var fingerprint = Fingerprint("dry-run", runRecord, validation);
        var existing = await foundation.FindIdempotencyAsync(tenant!, MigrationOperationKind.DryRun, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint.Value, StringComparison.Ordinal))
                return MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_idempotency_conflict");
            if (existing.AttemptId is not { } replayAttemptId)
                return MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_dry_run_replay_unavailable");
            var replay = await persistence.FindDryRunAsync(tenant!, runId, replayAttemptId, cancellationToken);
            return replay is not null
                ? MigrationOperationResult<MigrationDryRunPreview>.Replay(replay)
                : MigrationOperationResult<MigrationDryRunPreview>.Failure("migration_dry_run_in_progress", safeToRetry: true);
        }

        var run = MigrationRun.Rehydrate(runRecord);
        if (!run.PermitsAttempt(MigrationOperationKind.DryRun))
            return MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_dry_run_not_permitted");

        var started = await foundation.StartAttemptAsync(
            tenant!,
            new StartMigrationAttemptCommand(
                runId,
                MigrationOperationKind.DryRun,
                new MigrationIdempotencyKey(idempotencyKey),
                fingerprint),
            cancellationToken);
        if (started.Outcome == MigrationPersistenceOutcome.Replayed && started.Value is { } replayAttempt)
        {
            var replay = await persistence.FindDryRunAsync(tenant!, runId, replayAttempt.AttemptId, cancellationToken);
            return replay is not null
                ? MigrationOperationResult<MigrationDryRunPreview>.Replay(replay)
                : MigrationOperationResult<MigrationDryRunPreview>.Failure("migration_dry_run_in_progress", safeToRetry: true);
        }

        if (started.Value is not { } attempt)
            return MigrationOperationResult<MigrationDryRunPreview>.Failure(started.Code, safeToRetry: true);
        if (!await AppendAttemptEvidenceAsync(requestContext!, run, attempt, DryRunOperationId, started.Code, cancellationToken))
            return MigrationOperationResult<MigrationDryRunPreview>.Failure("migration_audit_evidence_unavailable", safeToRetry: true);

        var staged = await persistence.ListStagedRecordsAsync(tenant!, runId, 0, int.MaxValue, cancellationToken);
        if (staged.Count != validation.TotalStagedRecords
            || staged.Select(item => item.StagedRecordId).ToHashSet().SetEquals(validation.Records.Select(item => item.StagedRecordId)) is false
            || staged.Any(item => item.PackageHash != validation.PackageHash || item.SourceSnapshotHash != validation.SourceSnapshotHash))
        {
            var mismatch = await foundation.RecordAttemptOutcomeAsync(
                tenant!,
                new RecordMigrationAttemptOutcomeCommand(runId, attempt.AttemptId, MigrationAttemptOutcome.KnownFailure, "migration_dry_run_staged_records_mismatch", attempt.Version),
                cancellationToken);
            return mismatch.Succeeded
                ? MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_dry_run_staged_records_mismatch")
                : MigrationOperationResult<MigrationDryRunPreview>.Failure(mismatch.Code, safeToRetry: true);
        }
        var previewRows = validation.Records
            .OrderBy(item => item.SourceSequence)
            .Select(item => new MigrationPreviewRow(
                item.StagedRecordId,
                item.SourceSequence,
                item.RecordType,
                item.Disposition,
                item.Disposition == MigrationRecordDisposition.Accepted
                    ? PlannedAction(item.RecordType)
                    : MigrationPlannedAction.Blocked,
                item.Disposition == MigrationRecordDisposition.Accepted ? "would be evaluated by the owning module" : null))
            .ToArray();
        var controlTotals = ComputeControlTotals(staged);
        var preview = new MigrationDryRunPreview(
            Guid.NewGuid(),
            tenant!.TenantId,
            runId,
            attempt.AttemptId,
            validation.AttemptId,
            validation.PackageHash,
            validation.SourceSnapshotHash,
            validation.TotalStagedRecords,
            validation.AcceptedCount,
            validation.RejectedCount,
            validation.QuarantinedCount,
            validation.FindingCounts,
            controlTotals,
            validation.FindingCounts
                .Where(item => item.Key is nameof(MigrationFindingCategory.Reference) or nameof(MigrationFindingCategory.Currency) or nameof(MigrationFindingCategory.Uom) or nameof(MigrationFindingCategory.Scope))
                .Sum(item => item.Value),
            previewRows.Count(item => item.PlannedAction == MigrationPlannedAction.Blocked),
            previewRows,
            timeProvider.GetUtcNow());
        var saved = await persistence.SaveDryRunAsync(tenant!, new SaveMigrationDryRunCommand(preview), cancellationToken);
        if (!saved.Succeeded || saved.Value is not { } completed)
            return MigrationOperationResult<MigrationDryRunPreview>.Failure(saved.Code, safeToRetry: true);

        var outcome = await foundation.RecordAttemptOutcomeAsync(
            tenant!,
            new RecordMigrationAttemptOutcomeCommand(runId, attempt.AttemptId, MigrationAttemptOutcome.Succeeded, "dry_run_completed", attempt.Version),
            cancellationToken);
        if (!outcome.Succeeded)
            return MigrationOperationResult<MigrationDryRunPreview>.Failure(outcome.Code, safeToRetry: true);
        if (!await AppendRunEvidenceAsync(requestContext!, run, attempt, DryRunOperationId, "dry_run_completed", cancellationToken))
            return MigrationOperationResult<MigrationDryRunPreview>.Failure("migration_audit_evidence_unavailable", safeToRetry: true);

        return MigrationOperationResult<MigrationDryRunPreview>.Success(completed, "dry_run_completed");
    }

    public Task<MigrationValidationSummary?> ReadValidationAsync(TenantContext tenant, Guid runId, CancellationToken cancellationToken = default) =>
        persistence.FindLatestValidationAsync(tenant, runId, cancellationToken);

    public Task<IReadOnlyList<MigrationValidationFinding>> ReadFindingsAsync(TenantContext tenant, Guid runId, int offset, int pageSize, CancellationToken cancellationToken = default) =>
        persistence.ListFindingsAsync(tenant, runId, null, offset, pageSize, cancellationToken);

    public Task<IReadOnlyList<MigrationStagedRecord>> ReadStagedRecordsAsync(TenantContext tenant, Guid runId, int offset, int pageSize, CancellationToken cancellationToken = default) =>
        persistence.ListStagedRecordsAsync(tenant, runId, offset, pageSize, cancellationToken);

    public Task<MigrationDryRunPreview?> ReadDryRunAsync(TenantContext tenant, Guid runId, CancellationToken cancellationToken = default) =>
        persistence.FindLatestDryRunAsync(tenant, runId, cancellationToken);

    private async Task<MigrationRunRecord?> PrepareForValidationAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord runRecord,
        CancellationToken cancellationToken)
    {
        var run = MigrationRun.Rehydrate(runRecord);
        if (run.Status == MigrationRunStatus.Draft)
        {
            var prepared = await foundation.ApplyRunTransitionAsync(
                tenant,
                new ApplyMigrationRunTransitionCommand(run.RunId, MigrationRunStatus.Prepared, runRecord.Version),
                cancellationToken);
            if (!prepared.Succeeded || prepared.Value is null)
                return null;
            runRecord = prepared.Value;
            run = MigrationRun.Rehydrate(runRecord);
            if (!await AppendTransitionEvidenceAsync(requestContext, run, MigrationRunStatus.Draft, MigrationRunStatus.Prepared, cancellationToken))
                return null;
        }

        if (run.Status == MigrationRunStatus.Prepared)
        {
            var validating = await foundation.ApplyRunTransitionAsync(
                tenant,
                new ApplyMigrationRunTransitionCommand(run.RunId, MigrationRunStatus.Validating, runRecord.Version),
                cancellationToken);
            if (!validating.Succeeded || validating.Value is null)
                return null;
            runRecord = validating.Value;
            run = MigrationRun.Rehydrate(runRecord);
            if (!await AppendTransitionEvidenceAsync(requestContext, run, MigrationRunStatus.Prepared, MigrationRunStatus.Validating, cancellationToken))
                return null;
        }

        return run.Status == MigrationRunStatus.Validating ? runRecord : null;
    }

    private async Task<MigrationOperationResult<MigrationValidationSummary>> FinishFailedValidationAsync(
        FoundationRequestContext requestContext,
        MigrationRunRecord runRecord,
        TenantContext tenant,
        MigrationAttemptRecord attempt,
        string code,
        string message,
        MigrationFindingCategory category,
        CancellationToken cancellationToken)
    {
        var run = MigrationRun.Rehydrate(runRecord);
        var finding = new MigrationValidationFinding(
            Guid.NewGuid(),
            run.TenantId,
            run.RunId,
            attempt.AttemptId,
            null,
            category,
            MigrationFindingSeverity.Error,
            true,
            code,
            message,
            null,
            timeProvider.GetUtcNow());
        var summary = new MigrationValidationSummary(
            Guid.NewGuid(),
            run.TenantId,
            run.RunId,
            attempt.AttemptId,
            "",
            "",
            0,
            0,
            0,
            0,
            new Dictionary<string, int> { [category.ToString()] = 1 },
            [],
            timeProvider.GetUtcNow());
        var saved = await persistence.SaveValidationAsync(
            tenant,
            new SaveMigrationValidationCommand(summary, [finding]),
            cancellationToken);
        if (!saved.Succeeded)
            return MigrationOperationResult<MigrationValidationSummary>.Failure(saved.Code, safeToRetry: true);
        var outcome = await foundation.RecordAttemptOutcomeAsync(
            tenant,
            new RecordMigrationAttemptOutcomeCommand(run.RunId, attempt.AttemptId, MigrationAttemptOutcome.KnownFailure, code, attempt.Version),
            cancellationToken);
        if (!outcome.Succeeded)
            return MigrationOperationResult<MigrationValidationSummary>.Failure(outcome.Code, safeToRetry: true);
        var transition = await foundation.ApplyRunTransitionAsync(
            tenant,
            new ApplyMigrationRunTransitionCommand(run.RunId, MigrationRunStatus.ValidationFailed, runRecord.Version),
            cancellationToken);
        if (!transition.Succeeded || transition.Value is null)
            return MigrationOperationResult<MigrationValidationSummary>.Failure(transition.Code, safeToRetry: true);
        await AppendRunEvidenceAsync(requestContext, MigrationRun.Rehydrate(transition.Value), attempt, ValidationOperationId, code, cancellationToken);
        return MigrationOperationResult<MigrationValidationSummary>.Rejected(code);
    }

    private async Task<bool> AppendAttemptEvidenceAsync(
        FoundationRequestContext requestContext,
        MigrationRun run,
        MigrationAttemptRecord attempt,
        string operation,
        string outcome,
        CancellationToken cancellationToken) =>
        await AppendEvidenceAsync(requestContext, run, attempt, operation, outcome, FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, cancellationToken);

    private async Task<bool> AppendRunEvidenceAsync(
        FoundationRequestContext requestContext,
        MigrationRun run,
        MigrationAttemptRecord attempt,
        string operation,
        string outcome,
        CancellationToken cancellationToken) =>
        await AppendEvidenceAsync(requestContext, run, attempt, operation, outcome, FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, cancellationToken);

    private async Task<bool> AppendTransitionEvidenceAsync(
        FoundationRequestContext requestContext,
        MigrationRun run,
        MigrationRunStatus from,
        MigrationRunStatus to,
        CancellationToken cancellationToken)
    {
        var metadata = MigrationAuditMetadata.Create().With("from", from.ToString()).With("to", to.ToString());
        try
        {
            var evidence = MigrationAuditEvidenceFactory.Create(
                requestContext,
                run,
                "migration.run.transition",
                FoundationAuditDecision.Allowed,
                FoundationAuditReason.Allowed,
                "transition_allowed",
                safeMetadata: metadata,
                occurredAt: timeProvider.GetUtcNow());
            await auditSink.AppendAsync(evidence, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is FoundationAuditAppendException or ArgumentException)
        {
            return false;
        }
    }

    private async Task<bool> AppendEvidenceAsync(
        FoundationRequestContext requestContext,
        MigrationRun run,
        MigrationAttemptRecord attempt,
        string operation,
        string outcome,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var evidence = MigrationAuditEvidenceFactory.Create(
                requestContext,
                run,
                operation,
                decision,
                reason,
                outcome,
                MigrationAttempt.Rehydrate(attempt),
                occurredAt: timeProvider.GetUtcNow());
            await auditSink.AppendAsync(evidence, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is FoundationAuditAppendException or ArgumentException)
        {
            return false;
        }
    }

    private static MigrationValidationSummary BuildValidationSummary(
        TenantContext tenant,
        Guid runId,
        Guid attemptId,
        string packageHash,
        string sourceSnapshotHash,
        IReadOnlyList<MigrationStagedRecord> staged,
        IReadOnlyDictionary<int, (MigrationRecordDisposition Disposition, List<string> Codes)> results,
        IReadOnlyList<MigrationValidationFinding> findings,
        DateTimeOffset completedAt)
    {
        var records = staged
            .OrderBy(item => item.SourceSequence)
            .Select(item => new MigrationValidationRecordResult(
                item.StagedRecordId,
                item.SourceSequence,
                item.RecordType,
                results[item.SourceSequence].Disposition,
                results[item.SourceSequence].Codes.ToArray()))
            .ToArray();
        return new(
            Guid.NewGuid(),
            tenant.TenantId,
            runId,
            attemptId,
            packageHash,
            sourceSnapshotHash,
            records.Length,
            records.Count(item => item.Disposition == MigrationRecordDisposition.Accepted),
            records.Count(item => item.Disposition == MigrationRecordDisposition.Rejected),
            records.Count(item => item.Disposition == MigrationRecordDisposition.Quarantined),
            findings
                .GroupBy(item => item.Category.ToString(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            records,
            completedAt);
    }

    private static void AddFinding(
        ICollection<MigrationValidationFinding> findings,
        ICollection<string> codes,
        MigrationStagedRecord row,
        MigrationAttemptRecord attempt,
        MigrationFindingCategory category,
        MigrationFindingSeverity severity,
        string code,
        string message,
        string? referenceId = null)
    {
        codes.Add(code);
        findings.Add(new(
            Guid.NewGuid(),
            row.TenantId,
            row.RunId,
            attempt.AttemptId,
            row.StagedRecordId,
            category,
            severity,
            true,
            code,
            message,
            referenceId,
            DateTimeOffset.UtcNow));
    }

    private static void AddGlBalanceFinding(
        IReadOnlyList<MigrationParsedCanonicalRow> rows,
        IReadOnlyList<MigrationStagedRecord> staged,
        MigrationAttemptRecord attempt,
        IDictionary<int, (MigrationRecordDisposition Disposition, List<string> Codes)> results,
        ICollection<MigrationValidationFinding> findings)
    {
        var gl = rows
            .Where(item => item.Payload is MigrationGlOpeningPayload)
            .Select(item => (Row: item, Payload: (MigrationGlOpeningPayload)item.Payload))
            .ToArray();
        if (gl.Length == 0)
            return;

        var debit = gl.Sum(item => item.Payload.Debit ?? 0m);
        var credit = gl.Sum(item => item.Payload.Credit ?? 0m);
        if (Math.Abs(debit - credit) <= 0.00000001m)
            return;

        foreach (var item in gl)
        {
            results[item.Row.SourceSequence] =
                (MigrationRecordDisposition.Rejected, [.. results[item.Row.SourceSequence].Codes, "migration_gl_opening_imbalanced"]);
        }

        var first = staged.OrderBy(item => item.SourceSequence).First();
        findings.Add(new(
            Guid.NewGuid(),
            first.TenantId,
            first.RunId,
            attempt.AttemptId,
            null,
            MigrationFindingCategory.FinancialBalance,
            MigrationFindingSeverity.Error,
            true,
            "migration_gl_opening_imbalanced",
            "GL opening debit and credit control totals must balance.",
            null,
            DateTimeOffset.UtcNow));
    }

    private static IReadOnlyDictionary<string, decimal> ComputeControlTotals(IReadOnlyList<MigrationStagedRecord> staged)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var item in staged)
        {
            try
            {
                switch (item.RecordType)
                {
                    case MigrationCanonicalRecordType.InventoryOpening:
                        var inventory = JsonSerializer.Deserialize<MigrationInventoryOpeningPayload>(item.CanonicalPayload, CanonicalJsonOptions);
                        totals["inventoryQuantity"] = totals.GetValueOrDefault("inventoryQuantity") + (inventory?.Quantity ?? 0m);
                        totals["inventoryValue"] = totals.GetValueOrDefault("inventoryValue") + (inventory?.Quantity ?? 0m) * (inventory?.UnitCost ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.GlOpening:
                        var gl = JsonSerializer.Deserialize<MigrationGlOpeningPayload>(item.CanonicalPayload, CanonicalJsonOptions);
                        totals["glDebit"] = totals.GetValueOrDefault("glDebit") + (gl?.Debit ?? 0m);
                        totals["glCredit"] = totals.GetValueOrDefault("glCredit") + (gl?.Credit ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.ApOpening:
                        totals["apAmount"] = totals.GetValueOrDefault("apAmount") + (JsonSerializer.Deserialize<MigrationApOpeningPayload>(item.CanonicalPayload, CanonicalJsonOptions)?.Amount ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.ArOpening:
                        totals["arAmount"] = totals.GetValueOrDefault("arAmount") + (JsonSerializer.Deserialize<MigrationArOpeningPayload>(item.CanonicalPayload, CanonicalJsonOptions)?.Amount ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.CashBankOpening:
                        totals["cashBankAmount"] = totals.GetValueOrDefault("cashBankAmount") + (JsonSerializer.Deserialize<MigrationCashBankOpeningPayload>(item.CanonicalPayload, CanonicalJsonOptions)?.Amount ?? 0m);
                        break;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // The canonical payload was already typed before staging; a
                // defensive skip here cannot create an authoritative effect.
            }
        }
        return totals;
    }

    private static MigrationPlannedAction PlannedAction(MigrationCanonicalRecordType type) =>
        type is MigrationCanonicalRecordType.Product
            or MigrationCanonicalRecordType.Supplier
            or MigrationCanonicalRecordType.Customer
            ? MigrationPlannedAction.Create
            : MigrationPlannedAction.MatchReference;

    private static MigrationReferenceCheck? ScopeFinding(
        TenantWorkScope authorizedScope,
        MigrationParsedCanonicalRow row)
    {
        var (companyId, branchId, warehouseId) = row.Payload switch
        {
            MigrationOrganizationPayload organization => (organization.CompanyId, organization.BranchId, organization.WarehouseId),
            MigrationInventoryOpeningPayload inventory => (inventory.CompanyId, inventory.BranchId, inventory.WarehouseId),
            MigrationGlOpeningPayload gl => (gl.CompanyId, null, null),
            MigrationApOpeningPayload ap => (ap.CompanyId, null, null),
            MigrationArOpeningPayload ar => (ar.CompanyId, null, null),
            MigrationCashBankOpeningPayload cash => (cash.CompanyId, null, null),
            _ => (null, null, null)
        };

        if (companyId is null && branchId is null && warehouseId is null)
            return null;

        try
        {
            var requested = new TenantWorkScopeRequest(companyId, branchId, warehouseId);
            var allowed = authorizedScope.WarehouseId is { } authorizedWarehouse
                ? requested.WarehouseId == authorizedWarehouse
                : authorizedScope.BranchId is { } authorizedBranch
                    ? requested.BranchId == authorizedBranch && requested.CompanyId == authorizedScope.CompanyId
                    : authorizedScope.CompanyId is { } authorizedCompany
                        ? requested.CompanyId == authorizedCompany
                        : true;
            return allowed
                ? null
                : new(MigrationReferenceState.Missing, MigrationFindingCategory.Scope, "migration_row_scope_denied", "The row organization scope is outside the current authorized scope.");
        }
        catch (ArgumentException)
        {
            return new(MigrationReferenceState.Missing, MigrationFindingCategory.Scope, "migration_row_scope_invalid", "The row organization scope hierarchy is invalid.");
        }
    }

    private static bool PackageMatches(MigrationCanonicalPackage package, MigrationRun run, MigrationSourceArtifactSnapshot source) =>
        string.Equals(package.PackageVersion, MigrationCanonicalPackageParser.Version, StringComparison.Ordinal)
        && string.Equals(package.DefinitionId, run.Definition.DefinitionId, StringComparison.Ordinal)
        && string.Equals(package.DefinitionVersion, run.Definition.Version, StringComparison.Ordinal)
        && string.Equals(package.SourceProfileId, run.SourceProfile.ProfileId, StringComparison.Ordinal)
        && string.Equals(package.SourceProfileVersion, run.SourceProfile.ProfileVersion, StringComparison.Ordinal)
        && package.SourceSnapshot.ObjectId == source.ObjectId
        && string.Equals(package.SourceSnapshot.Sha256, source.Sha256, StringComparison.OrdinalIgnoreCase)
        && package.SourceSnapshot.Length == source.Length
        && package.SourceSnapshot.ConcurrencyVersion == source.ConcurrencyVersion
        && !string.IsNullOrWhiteSpace(package.LogicalDataset);

    private static bool MatchesSnapshot(MigrationSourceArtifactSnapshot snapshot, PrivateFileMetadata metadata) =>
        metadata.TenantId == snapshot.TenantId
        && metadata.ObjectId == snapshot.ObjectId
        && string.Equals(metadata.Sha256, snapshot.Sha256, StringComparison.OrdinalIgnoreCase)
        && metadata.Length == snapshot.Length
        && metadata.ConcurrencyVersion == snapshot.ConcurrencyVersion;

    private static MigrationRequestFingerprint Fingerprint(string operation, MigrationRunRecord run, MigrationIntakeRecord intake) =>
        new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            "|",
            "migration-validation-v1",
            operation,
            run.RunId.ToString("D", CultureInfo.InvariantCulture),
            run.TenantId.Value.ToString("D", CultureInfo.InvariantCulture),
            run.Definition.DefinitionId,
            run.Definition.Version,
            run.SourceProfile.ProfileId,
            run.SourceProfile.ProfileVersion,
            intake.Source.ObjectId.ToString("D", CultureInfo.InvariantCulture),
            intake.Source.Sha256,
            intake.Source.Length.ToString(CultureInfo.InvariantCulture),
            intake.Source.ConcurrencyVersion.ToString(CultureInfo.InvariantCulture))))));

    private static MigrationRequestFingerprint Fingerprint(string operation, MigrationRunRecord run, MigrationValidationSummary validation) =>
        new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            "|",
            "migration-validation-v1",
            operation,
            run.RunId.ToString("D", CultureInfo.InvariantCulture),
            validation.AttemptId.ToString("D", CultureInfo.InvariantCulture),
            validation.PackageHash,
            validation.SourceSnapshotHash)))));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static Guid DeterministicId(Guid runId, string packageHash, int sourceSequence)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{runId:D}|{packageHash}|{sourceSequence.ToString(CultureInfo.InvariantCulture)}"));
        return new Guid(bytes[..16]);
    }

    private static bool TryTenant<T>(
        FoundationRequestContext? requestContext,
        Guid runId,
        out TenantContext? tenant,
        out MigrationOperationResult<T>? rejected)
    {
        tenant = requestContext?.TenantContext;
        rejected = null;
        if (tenant is null)
        {
            rejected = MigrationOperationResult<T>.Rejected("migration_tenant_context_required");
            return false;
        }
        if (runId == Guid.Empty)
        {
            rejected = MigrationOperationResult<T>.Rejected("migration_run_id_invalid");
            return false;
        }
        return true;
    }
}

#pragma warning restore CS1591
