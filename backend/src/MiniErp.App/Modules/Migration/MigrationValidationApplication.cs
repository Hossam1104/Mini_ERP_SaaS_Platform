#pragma warning disable CS1591

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

public sealed class MigrationValidationService
{
    public const string ValidationOperationId = "migration.validation.start";
    public const string DryRunOperationId = "migration.dry-run.start";

    private readonly MigrationFoundationService foundation;
    private readonly IMigrationValidationPersistence persistence;
    private readonly IPrivateObjectStorage privateStorage;
    private readonly ICurrentOrganizationScopeResolver scopeResolver;
    private readonly IMigrationReferenceAuthority references;
    private readonly TimeProvider timeProvider;

    public MigrationValidationService(
        MigrationFoundationService foundation,
        IMigrationValidationPersistence persistence,
        IPrivateObjectStorage privateStorage,
        ICurrentOrganizationScopeResolver scopeResolver,
        IMigrationReferenceAuthority references,
        TimeProvider? timeProvider = null)
    {
        this.foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.privateStorage = privateStorage ?? throw new ArgumentNullException(nameof(privateStorage));
        this.scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        this.references = references ?? throw new ArgumentNullException(nameof(references));
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

        var runLookup = await foundation.FindRunAsync(tenant!, runId, cancellationToken);
        var runRecord = runLookup.Value;
        var intake = await persistence.FindIntakeAsync(tenant!, runId, cancellationToken);
        if (runRecord is null || intake is null)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_run_not_found");

        if (!IsCurrentScopeAuthorized(tenant!, intake.Source))
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_source_scope_denied");

        var fingerprint = MigrationValidationFingerprint.ForValidation(runRecord, intake);

        var prepared = await PrepareForValidationAsync(requestContext!, tenant!, runRecord, cancellationToken);
        if (!prepared.Succeeded || prepared.Value is not { } validatingRunRecord)
            return prepared.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(prepared.Code)
                : prepared.Kind == MigrationResultKind.KnownFailure
                    ? MigrationOperationResult<MigrationValidationSummary>.Failure(prepared.Code, prepared.IsSafeToRetry)
                    : MigrationOperationResult<MigrationValidationSummary>.Rejected(prepared.Code);

        var started = await foundation.StartAttemptAsync(
            requestContext!,
            runId,
            MigrationOperationKind.Validation,
            idempotencyKey,
            fingerprint.Value,
            cancellationToken);
        if (started.Kind == MigrationResultKind.Replayed && started.Value is { } replayAttempt)
        {
            var replay = await persistence.FindValidationAsync(tenant!, runId, replayAttempt.AttemptId, cancellationToken);
            return replay is not null
                ? MigrationOperationResult<MigrationValidationSummary>.Replay(replay)
                : MigrationOperationResult<MigrationValidationSummary>.Failure("migration_validation_in_progress", safeToRetry: true);
        }

        if (!started.Succeeded || started.Value is not { } attempt)
            return started.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(started.Code)
                : started.Kind == MigrationResultKind.Rejected
                    ? MigrationOperationResult<MigrationValidationSummary>.Rejected(started.Code)
                    : MigrationOperationResult<MigrationValidationSummary>.Failure(started.Code, started.IsSafeToRetry);

        var run = MigrationRun.Rehydrate(validatingRunRecord);

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

        if (!IsCurrentScopeAuthorized(
                tenant!,
                intake.Source with
                {
                    CompanyId = metadata.Scope.CompanyId,
                    BranchId = metadata.Scope.BranchId,
                    WarehouseId = metadata.Scope.WarehouseId
                }))
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
        var resolvedScope = scopeResolver.ResolveCurrent(tenant!);
        if (!resolvedScope.Allowed || resolvedScope.Scope is not { } authorizedScope)
            return await FinishFailedValidationAsync(
                requestContext!,
                validatingRunRecord,
                tenant!,
                attempt,
                "migration_source_scope_denied",
                "The current organization scope is not available for validation.",
                MigrationFindingCategory.Scope,
                cancellationToken);
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
                MigrationValidationResultPolicy.AddFinding(rowFindings, codes, stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence), attempt, rule.Category, MigrationFindingSeverity.Error, rule.Code, rule.Message);

            if (ScopeFinding(authorizedScope, row) is { } scopeFinding)
                MigrationValidationResultPolicy.AddFinding(
                    rowFindings,
                    codes,
                    stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence),
                    attempt,
                    scopeFinding.Category,
                    MigrationFindingSeverity.Error,
                    scopeFinding.Code,
                    scopeFinding.Message);

            if (duplicateSourceIds.Contains(row.SourceSequence) || duplicateBusinessKeys.Contains(row.SourceSequence))
                MigrationValidationResultPolicy.AddFinding(rowFindings, codes, stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence), attempt, MigrationFindingCategory.Duplicate, MigrationFindingSeverity.Error, "migration_duplicate_source_identity", "The canonical business identity occurs more than once in this package.");

            try
            {
                foreach (var check in await references.ValidateAsync(requestContext!, row, cancellationToken))
                {
                    if (check.State is MigrationReferenceState.NotApplicable or MigrationReferenceState.Active)
                        continue;

                    var severity = check.State is MigrationReferenceState.Ambiguous or MigrationReferenceState.Unavailable
                        ? MigrationFindingSeverity.Warning
                        : MigrationFindingSeverity.Error;
                    MigrationValidationResultPolicy.AddFinding(
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
                MigrationValidationResultPolicy.AddFinding(
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

        MigrationValidationResultPolicy.AddGlBalanceFindings(parsed.Rows, stagedPackage.Records, attempt, resultBySequence, findings);
        var summary = MigrationValidationResultPolicy.BuildSummary(
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
            requestContext!,
            runId,
            attempt.AttemptId,
            outcome,
            completed.IsValid ? "validated" : "validation_failed",
            attempt.Version,
            cancellationToken);
        if (!outcomeResult.Succeeded)
            return outcomeResult.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(outcomeResult.Code)
                : MigrationOperationResult<MigrationValidationSummary>.Failure(outcomeResult.Code, outcomeResult.IsSafeToRetry);

        var target = completed.IsValid ? MigrationRunStatus.Validated : MigrationRunStatus.ValidationFailed;
        var transitioned = await foundation.TransitionRunAsync(
            requestContext!,
            runId,
            target,
            validatingRunRecord.Version,
            cancellationToken);
        if (!transitioned.Succeeded)
            return transitioned.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(transitioned.Code)
                : MigrationOperationResult<MigrationValidationSummary>.Failure(transitioned.Code, transitioned.IsSafeToRetry);

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

        var runLookup = await foundation.FindRunAsync(tenant!, runId, cancellationToken);
        var runRecord = runLookup.Value;
        var intake = await persistence.FindIntakeAsync(tenant!, runId, cancellationToken);
        if (runRecord is null || intake is null)
            return MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_validation_required");

        if (!IsCurrentScopeAuthorized(tenant!, intake.Source))
            return MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_source_scope_denied");

        var validation = await persistence.FindLatestValidationAsync(tenant!, runId, cancellationToken);
        if (validation is null || !validation.IsValid)
            return MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_validation_required");

        var fingerprint = MigrationValidationFingerprint.ForDryRun(runRecord, validation);

        var started = await foundation.StartAttemptAsync(
            requestContext!,
            runId,
            MigrationOperationKind.DryRun,
            idempotencyKey,
            fingerprint.Value,
            cancellationToken);
        if (started.Kind == MigrationResultKind.Replayed && started.Value is { } replayAttempt)
        {
            var replay = await persistence.FindDryRunAsync(tenant!, runId, replayAttempt.AttemptId, cancellationToken);
            return replay is not null
                ? MigrationOperationResult<MigrationDryRunPreview>.Replay(replay)
                : MigrationOperationResult<MigrationDryRunPreview>.Failure("migration_dry_run_in_progress", safeToRetry: true);
        }

        if (!started.Succeeded || started.Value is not { } attempt)
            return started.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationDryRunPreview>.Unknown(started.Code)
                : started.Kind == MigrationResultKind.Rejected
                    ? MigrationOperationResult<MigrationDryRunPreview>.Rejected(started.Code)
                    : MigrationOperationResult<MigrationDryRunPreview>.Failure(started.Code, started.IsSafeToRetry);

        var staged = await persistence.ListStagedRecordsAsync(tenant!, runId, 0, int.MaxValue, cancellationToken);
        if (staged.Count != validation.TotalStagedRecords
            || staged.Select(item => item.StagedRecordId).ToHashSet().SetEquals(validation.Records.Select(item => item.StagedRecordId)) is false
            || staged.Any(item => item.PackageHash != validation.PackageHash || item.SourceSnapshotHash != validation.SourceSnapshotHash))
        {
            var mismatch = await foundation.RecordAttemptOutcomeAsync(
                requestContext!,
                runId,
                attempt.AttemptId,
                MigrationAttemptOutcome.KnownFailure,
                "migration_dry_run_staged_records_mismatch",
                attempt.Version,
                cancellationToken);
            return mismatch.Succeeded
                ? MigrationOperationResult<MigrationDryRunPreview>.Rejected("migration_dry_run_staged_records_mismatch")
                : mismatch.Kind == MigrationResultKind.UnknownOutcome
                    ? MigrationOperationResult<MigrationDryRunPreview>.Unknown(mismatch.Code)
                    : MigrationOperationResult<MigrationDryRunPreview>.Failure(mismatch.Code, mismatch.IsSafeToRetry);
        }
        var preview = MigrationDryRunPreviewPolicy.Build(
            tenant!.TenantId,
            runId,
            attempt.AttemptId,
            validation,
            staged,
            timeProvider.GetUtcNow());
        var saved = await persistence.SaveDryRunAsync(tenant!, new SaveMigrationDryRunCommand(preview), cancellationToken);
        if (!saved.Succeeded || saved.Value is not { } completed)
            return MigrationOperationResult<MigrationDryRunPreview>.Failure(saved.Code, safeToRetry: true);

        var outcome = await foundation.RecordAttemptOutcomeAsync(
            requestContext!,
            runId,
            attempt.AttemptId,
            MigrationAttemptOutcome.Succeeded,
            "dry_run_completed",
            attempt.Version,
            cancellationToken);
        if (!outcome.Succeeded)
            return outcome.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationDryRunPreview>.Unknown(outcome.Code)
                : MigrationOperationResult<MigrationDryRunPreview>.Failure(outcome.Code, outcome.IsSafeToRetry);

        return MigrationOperationResult<MigrationDryRunPreview>.Success(completed, "dry_run_completed");
    }

    public async Task<MigrationValidationSummary?> ReadValidationAsync(TenantContext tenant, Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await IsResourceAuthorizedAsync(tenant, runId, cancellationToken))
            return null;
        return await persistence.FindLatestValidationAsync(tenant, runId, cancellationToken);
    }

    public async Task<IReadOnlyList<MigrationValidationFinding>> ReadFindingsAsync(TenantContext tenant, Guid runId, int offset, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await IsResourceAuthorizedAsync(tenant, runId, cancellationToken))
            return [];
        return await persistence.ListFindingsAsync(tenant, runId, null, offset, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<MigrationStagedRecord>> ReadStagedRecordsAsync(TenantContext tenant, Guid runId, int offset, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await IsResourceAuthorizedAsync(tenant, runId, cancellationToken))
            return [];
        return await persistence.ListStagedRecordsAsync(tenant, runId, offset, pageSize, cancellationToken);
    }

    public async Task<MigrationDryRunPreview?> ReadDryRunAsync(TenantContext tenant, Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await IsResourceAuthorizedAsync(tenant, runId, cancellationToken))
            return null;
        return await persistence.FindLatestDryRunAsync(tenant, runId, cancellationToken);
    }

    public Task<bool> IsResourceAuthorizedAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        CancellationToken cancellationToken = default) =>
        requestContext.TenantContext is { } tenant
            ? IsResourceAuthorizedAsync(tenant, runId, cancellationToken)
            : Task.FromResult(false);

    public async Task<bool> IsResourceAuthorizedAsync(
        TenantContext tenant,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var intake = await persistence.FindIntakeAsync(tenant, runId, cancellationToken);
        return intake is not null && IsCurrentScopeAuthorized(tenant, intake.Source);
    }

    private async Task<MigrationOperationResult<MigrationRunRecord>> PrepareForValidationAsync(
        FoundationRequestContext requestContext,
        TenantContext tenant,
        MigrationRunRecord runRecord,
        CancellationToken cancellationToken)
    {
        var run = MigrationRun.Rehydrate(runRecord);
        for (var attempt = 0; attempt < 3 && run.Status is MigrationRunStatus.Draft or MigrationRunStatus.Prepared; attempt++)
        {
            var target = run.Status == MigrationRunStatus.Draft ? MigrationRunStatus.Prepared : MigrationRunStatus.Validating;
            var transitioned = await foundation.TransitionRunAsync(
                requestContext,
                run.RunId,
                target,
                runRecord.Version,
                cancellationToken);
            if (transitioned.Succeeded && transitioned.Value is { } value)
            {
                runRecord = value;
                run = MigrationRun.Rehydrate(runRecord);
                continue;
            }

            if (transitioned.Code != "migration_run_version_conflict")
                return transitioned.Kind == MigrationResultKind.UnknownOutcome
                    ? MigrationOperationResult<MigrationRunRecord>.Unknown(transitioned.Code)
                    : transitioned.Kind == MigrationResultKind.KnownFailure
                        ? MigrationOperationResult<MigrationRunRecord>.Failure(transitioned.Code, transitioned.IsSafeToRetry)
                        : MigrationOperationResult<MigrationRunRecord>.Rejected(transitioned.Code);

            var current = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
            if (!current.Succeeded || current.Value is not { } currentRun)
                return MigrationOperationResult<MigrationRunRecord>.Rejected(current.Code);
            runRecord = currentRun;
            run = MigrationRun.Rehydrate(runRecord);
        }

        return run.Status is MigrationRunStatus.Validating
            or MigrationRunStatus.Validated
            or MigrationRunStatus.ValidationFailed
            ? MigrationOperationResult<MigrationRunRecord>.Success(runRecord)
            : MigrationOperationResult<MigrationRunRecord>.Rejected("migration_validation_not_permitted");
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
            requestContext,
            run.RunId,
            attempt.AttemptId,
            MigrationAttemptOutcome.KnownFailure,
            code,
            attempt.Version,
            cancellationToken);
        if (!outcome.Succeeded)
            return outcome.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(outcome.Code)
                : MigrationOperationResult<MigrationValidationSummary>.Failure(outcome.Code, outcome.IsSafeToRetry);
        var transition = await foundation.TransitionRunAsync(
            requestContext,
            run.RunId,
            MigrationRunStatus.ValidationFailed,
            runRecord.Version,
            cancellationToken);
        if (!transition.Succeeded)
            return transition.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(transition.Code)
                : MigrationOperationResult<MigrationValidationSummary>.Failure(transition.Code, transition.IsSafeToRetry);
        return MigrationOperationResult<MigrationValidationSummary>.Rejected(code);
    }

    private bool IsCurrentScopeAuthorized(TenantContext tenant, MigrationSourceArtifactSnapshot source)
    {
        if (source.TenantId != tenant.TenantId)
            return false;

        var resolved = scopeResolver.ResolveCurrent(tenant);
        return resolved.Allowed
            && resolved.Scope is { } authorized
            && IsScopeWithin(authorized, source.CompanyId, source.BranchId, source.WarehouseId);
    }

    private static bool IsScopeWithin(
        TenantWorkScope authorized,
        Guid? companyId,
        Guid? branchId,
        Guid? warehouseId)
    {
        if (authorized.WarehouseId is { } warehouse)
            return warehouseId == warehouse;
        if (authorized.BranchId is { } branch)
            return companyId == authorized.CompanyId && branchId == branch;
        if (authorized.CompanyId is { } company)
            return companyId == company;
        return true;
    }

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
