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
    private readonly IOrganizationScopeOwnershipResolver scopeOwnership;
    private readonly IMigrationReferenceAuthority references;
    private readonly TimeProvider timeProvider;

    public MigrationValidationService(
        MigrationFoundationService foundation,
        IMigrationValidationPersistence persistence,
        IPrivateObjectStorage privateStorage,
        ICurrentOrganizationScopeResolver scopeResolver,
        IMigrationReferenceAuthority references,
        IOrganizationScopeOwnershipResolver scopeOwnership,
        TimeProvider? timeProvider = null)
    {
        this.foundation = foundation ?? throw new ArgumentNullException(nameof(foundation));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.privateStorage = privateStorage ?? throw new ArgumentNullException(nameof(privateStorage));
        this.scopeResolver = scopeResolver ?? throw new ArgumentNullException(nameof(scopeResolver));
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.scopeOwnership = scopeOwnership ?? throw new ArgumentNullException(nameof(scopeOwnership));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MigrationOperationResult<MigrationValidationSummary>> ValidateAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        await ValidateCoreAsync(requestContext, runId, idempotencyKey, null, null, cancellationToken);

    private async Task<MigrationOperationResult<MigrationValidationSummary>> ValidateCoreAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        IReadOnlyDictionary<Guid, MigrationParsedCanonicalRow>? corrections,
        string? correctionFingerprint,
        CancellationToken cancellationToken)
    {
        if (!TryTenant<MigrationValidationSummary>(requestContext, runId, out var tenant, out var rejected))
            return rejected!;

        var runLookup = await foundation.FindRunAsync(tenant!, runId, cancellationToken);
        var runRecord = runLookup.Value;
        var intake = await persistence.FindIntakeAsync(tenant!, runId, cancellationToken);
        if (runRecord is null || intake is null)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_run_not_found");

        if (!runRecord.EvidenceConfirmed)
            return MigrationOperationResult<MigrationValidationSummary>.Unknown("migration_audit_recovery_required");

        if (!IsCurrentScopeAuthorized(tenant!, intake.Source))
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_source_scope_denied");

        var fingerprint = corrections is null
            ? MigrationValidationFingerprint.ForValidation(runRecord, intake)
            : MigrationValidationFingerprint.ForCorrectedValidation(runRecord, intake, correctionFingerprint!);

        var prepared = await PrepareForValidationAsync(requestContext!, tenant!, runRecord, corrections is not null, cancellationToken);
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
                ? MigrationOperationResult<MigrationValidationSummary>.Replay(WithAttemptContext(
                    replay with { RunVersion = validatingRunRecord.Version },
                    runRecord,
                    replayAttempt))
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
                staged,
                System.Text.Json.JsonSerializer.Serialize(package.DomainContracts!.OrderBy(item => item.RecordType))),
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

        var validationRows = parsed.Rows.ToDictionary(item => item.SourceSequence);
        MigrationValidationSummary? priorValidation = null;
        IReadOnlySet<int>? correctedSequences = null;
        IReadOnlyDictionary<Guid, MigrationValidationRecordResult>? priorRecords = null;
        if (corrections is not null)
        {
            priorValidation = await persistence.FindLatestValidationAsync(tenant!, runId, cancellationToken);
            if (priorValidation is null)
                return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_validation_required");
            priorRecords = priorValidation.Records.ToDictionary(item => item.StagedRecordId);
            correctedSequences = corrections.Keys
                .Select(id => stagedPackage.Records.Single(item => item.StagedRecordId == id).SourceSequence)
                .ToHashSet();

            foreach (var stagedRecord in stagedPackage.Records)
            {
                if (corrections.TryGetValue(stagedRecord.StagedRecordId, out var corrected))
                {
                    validationRows[stagedRecord.SourceSequence] = corrected;
                    continue;
                }

                if (!priorRecords.TryGetValue(stagedRecord.StagedRecordId, out var previous)
                    || string.IsNullOrWhiteSpace(previous.CanonicalPayload)
                    || !MigrationCanonicalPackageParser.TryParsePayload(
                        stagedRecord.SourceSequence,
                        stagedRecord.SourceRecordId,
                        stagedRecord.RecordType,
                        previous.CanonicalPayload,
                        previous.CorrectionOwner,
                        out var priorPayload)
                    || priorPayload is null)
                    return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_snapshot_invalid");

                validationRows[stagedRecord.SourceSequence] = priorPayload;
            }
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
        if (corrections is not null && priorValidation is not null)
        {
            var stagedById = stagedPackage.Records.ToDictionary(item => item.StagedRecordId);
            var priorFindings = await persistence.ListFindingsAsync(
                tenant!, runId, priorValidation.AttemptId, 0, int.MaxValue, cancellationToken);
            foreach (var previous in priorValidation.Records.Where(item => !corrections.ContainsKey(item.StagedRecordId)))
            {
                if (stagedById.TryGetValue(previous.StagedRecordId, out var unchanged))
                    resultBySequence[unchanged.SourceSequence] = (previous.Disposition, previous.FindingCodes.ToList());
                findings.AddRange(priorFindings
                    .Where(item => item.StagedRecordId == previous.StagedRecordId)
                    .Select(item => item with
                    {
                        FindingId = Guid.NewGuid(),
                        AttemptId = attempt.AttemptId,
                        CreatedAt = timeProvider.GetUtcNow()
                    }));
            }
        }

        var duplicateSourceIds = validationRows.Values
            .Where(row => !string.IsNullOrWhiteSpace(row.SourceRecordId))
            .GroupBy(row => (row.RecordType, row.SourceRecordId))
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .Select(row => row.SourceSequence)
            .ToHashSet();
        var businessIdentities = validationRows.Values.ToDictionary(
            row => row.SourceSequence,
            row => references.ResolveBusinessIdentity(row));
        var duplicateBusinessKeys = DuplicateBusinessKeySequences(businessIdentities);

        foreach (var row in validationRows.Values.OrderBy(item => item.SourceSequence))
        {
            if (correctedSequences is not null && !correctedSequences.Contains(row.SourceSequence))
                continue;
            var rowFindings = new List<MigrationValidationFinding>();
            var codes = new List<string>();
            foreach (var rule in MigrationValidationRules.Validate(row))
                MigrationValidationResultPolicy.AddFinding(rowFindings, codes, stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence), attempt, rule.Category, MigrationFindingSeverity.Error, rule.Code, rule.Message);

            if (ScopeFinding(tenant!, authorizedScope, row) is { } scopeFinding)
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

            var businessIdentity = businessIdentities[row.SourceSequence];
            if (businessIdentity.State is MigrationBusinessIdentityState.Invalid or MigrationBusinessIdentityState.Unavailable)
            {
                MigrationValidationResultPolicy.AddFinding(
                    rowFindings,
                    codes,
                    stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence),
                    attempt,
                    MigrationFindingCategory.Reference,
                    businessIdentity.State == MigrationBusinessIdentityState.Invalid
                        ? MigrationFindingSeverity.Error
                        : MigrationFindingSeverity.Warning,
                    businessIdentity.Code ?? "migration_reference_authority_unavailable",
                    businessIdentity.Message ?? "The owner-module identity authority could not verify this business identity.");
            }

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

            var requiresQuarantineMetadata = rowFindings.Count > 0
                && rowFindings.All(item => item.Severity != MigrationFindingSeverity.Error);
            if (requiresQuarantineMetadata && string.IsNullOrWhiteSpace(row.SourceRecordId))
            {
                MigrationValidationResultPolicy.AddFinding(
                    rowFindings,
                    codes,
                    stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence),
                    attempt,
                    MigrationFindingCategory.Unsupported,
                    MigrationFindingSeverity.Error,
                    "migration_source_record_id_required",
                    "A source record ID is required before this row can be quarantined.");
            }

            if (requiresQuarantineMetadata && string.IsNullOrWhiteSpace(row.CorrectionOwner))
            {
                MigrationValidationResultPolicy.AddFinding(
                    rowFindings,
                    codes,
                    stagedPackage.Records.Single(item => item.SourceSequence == row.SourceSequence),
                    attempt,
                    MigrationFindingCategory.Unsupported,
                    MigrationFindingSeverity.Error,
                    "migration_correction_owner_required",
                    "Supply the correction owner before this row can be quarantined.");
            }

            var disposition = rowFindings.Any(item => item.Severity == MigrationFindingSeverity.Error)
                ? MigrationRecordDisposition.Rejected
                : rowFindings.Count > 0
                    ? MigrationRecordDisposition.Quarantined
                    : MigrationRecordDisposition.Accepted;
            resultBySequence[row.SourceSequence] = (disposition, codes);
            findings.AddRange(rowFindings);
        }

        MigrationValidationResultPolicy.AddGlBalanceFindings(validationRows.Values.ToArray(), stagedPackage.Records, attempt, resultBySequence, findings, correctedSequences);
        MigrationValidationResultPolicy.AddGlDuplicateFindings(validationRows.Values.ToArray(), stagedPackage.Records, attempt, resultBySequence, findings, correctedSequences);
        var summary = MigrationValidationResultPolicy.BuildSummary(
            tenant!,
            runId,
            attempt.AttemptId,
            packageHash,
            intake.Source.Sha256,
            stagedPackage.Records,
            resultBySequence,
            findings,
            timeProvider.GetUtcNow(),
            validationRows,
            priorRecords);
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
        var currentRun = await foundation.FindRunAsync(tenant!, runId, cancellationToken);
        if (!currentRun.Succeeded || currentRun.Value is not { } confirmedRun)
            return currentRun.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(currentRun.Code)
                : MigrationOperationResult<MigrationValidationSummary>.Failure(currentRun.Code, currentRun.IsSafeToRetry);
        var transitioned = await foundation.TransitionRunAsync(
            requestContext!,
            runId,
            target,
            confirmedRun.Version,
            cancellationToken);
        if (!transitioned.Succeeded)
            return transitioned.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(transitioned.Code)
                : MigrationOperationResult<MigrationValidationSummary>.Failure(transitioned.Code, transitioned.IsSafeToRetry);

        return completed.IsValid
            ? MigrationOperationResult<MigrationValidationSummary>.Success(WithAttemptContext(
                completed with { RunVersion = transitioned.Value?.Version },
                transitioned.Value ?? confirmedRun,
                attempt with { Outcome = outcome, FinishedAt = timeProvider.GetUtcNow(), SafeOutcomeCode = "validated" }), "validated")
            : MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_validation_failed");
    }

    public async Task<MigrationOperationResult<MigrationValidationSummary>> RetryCorrectedAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        string idempotencyKey,
        byte[] expectedRunVersion,
        IReadOnlyList<MigrationCorrectionSubmission> submissions,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant<MigrationValidationSummary>(requestContext, runId, out var tenant, out var rejected))
            return rejected!;
        if (expectedRunVersion is not { Length: > 0 }
            || submissions is null
            || submissions.Count is 0 or > 100_000
            || submissions.Any(item => item.StagedRecordId == Guid.Empty)
            || submissions.Select(item => item.StagedRecordId).Distinct().Count() != submissions.Count)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_request_invalid");

        var runLookup = await foundation.FindRunAsync(tenant!, runId, cancellationToken);
        if (!runLookup.Succeeded || runLookup.Value is not { } run)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected(runLookup.Code);
        var intake = await persistence.FindIntakeAsync(tenant!, runId, cancellationToken);
        if (intake is null)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_run_not_found");
        if (!run.EvidenceConfirmed)
            return MigrationOperationResult<MigrationValidationSummary>.Unknown("migration_audit_recovery_required");
        if (!IsCurrentScopeAuthorized(tenant!, intake.Source))
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_source_scope_denied");

        var validation = await persistence.FindLatestValidationAsync(tenant!, runId, cancellationToken);
        if (validation is null)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_validation_required");
        var staged = await persistence.ListStagedRecordsAsync(tenant!, runId, 0, int.MaxValue, cancellationToken);
        var stagedById = staged.ToDictionary(item => item.StagedRecordId);
        var validationById = validation.Records.ToDictionary(item => item.StagedRecordId);
        var corrections = new Dictionary<Guid, MigrationParsedCanonicalRow>();
        foreach (var submission in submissions)
        {
            if (!stagedById.TryGetValue(submission.StagedRecordId, out var source)
                || !validationById.TryGetValue(submission.StagedRecordId, out var previous)
                || string.IsNullOrWhiteSpace(submission.CorrectionOwner)
                || submission.CorrectionOwner.Trim().Length > 128
                || submission.CorrectionOwner.Any(char.IsControl)
                || !MigrationCanonicalPackageParser.TryParsePayload(
                    source.SourceSequence,
                    source.SourceRecordId,
                    source.RecordType,
                    submission.CorrectedPayload,
                    submission.CorrectionOwner,
                    out var corrected)
                || corrected is null)
                return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_row_invalid");
            corrections.Add(source.StagedRecordId, corrected);
        }

        var fingerprintParts = corrections
            .OrderBy(item => item.Key)
            .SelectMany(item => new[]
            {
                item.Key.ToString("D", CultureInfo.InvariantCulture),
                item.Value.PayloadJson,
                item.Value.CorrectionOwner
            })
            .ToArray();
        var correctionFingerprint = MigrationFingerprintEncoder.Compute("migration-correction-retry-v1", fingerprintParts);
        var requestFingerprint = MigrationValidationFingerprint.ForCorrectedValidation(run, intake, correctionFingerprint).Value;
        var existing = await foundation.FindIdempotencyAsync(tenant!, MigrationOperationKind.Validation, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_idempotency_conflict");
            if (existing.AttemptId is not { } existingAttemptId)
                return MigrationOperationResult<MigrationValidationSummary>.Unknown("migration_validation_attempt_unavailable");
            var existingAttempt = await foundation.FindAttemptAsync(tenant!, runId, existingAttemptId, cancellationToken);
            if (existingAttempt is null)
                return MigrationOperationResult<MigrationValidationSummary>.Unknown("migration_validation_attempt_unavailable");
            if (!existingAttempt.EvidenceConfirmed)
                return MigrationOperationResult<MigrationValidationSummary>.Unknown("migration_audit_recovery_required");
            if (existingAttempt.Outcome == MigrationAttemptOutcome.Pending)
                return MigrationOperationResult<MigrationValidationSummary>.Failure("migration_validation_in_progress", safeToRetry: true);
            if (existingAttempt.Outcome == MigrationAttemptOutcome.UnknownOutcome)
                return MigrationOperationResult<MigrationValidationSummary>.Unknown(existingAttempt.SafeOutcomeCode ?? "migration_validation_outcome_unknown");
            var replay = await persistence.FindValidationAsync(tenant!, runId, existingAttemptId, cancellationToken);
            if (replay is null)
                return MigrationOperationResult<MigrationValidationSummary>.Unknown("migration_validation_result_unavailable");
            return existingAttempt.Outcome == MigrationAttemptOutcome.KnownFailure
                ? MigrationOperationResult<MigrationValidationSummary>.Rejected(existingAttempt.SafeOutcomeCode ?? "migration_validation_failed")
                : MigrationOperationResult<MigrationValidationSummary>.Replay(replay);
        }

        if (run.Status != MigrationRunStatus.ValidationFailed)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_state_invalid");
        if (!run.Version.AsSpan().SequenceEqual(expectedRunVersion))
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_run_version_conflict");
        if (MigrationRun.Rehydrate(run).HasReachedEffectBoundary)
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_after_effect_boundary");
        var attempts = await foundation.ListAttemptsAsync(tenant!, runId, cancellationToken);
        if (attempts.Any(item => item.Operation == MigrationOperationKind.Execution))
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_after_execution_not_permitted");
        if (submissions.Any(item => validationById[item.StagedRecordId].Disposition == MigrationRecordDisposition.Accepted))
            return MigrationOperationResult<MigrationValidationSummary>.Rejected("migration_correction_row_ineligible");

        return await ValidateCoreAsync(
            requestContext,
            runId,
            idempotencyKey,
            corrections,
            correctionFingerprint,
            cancellationToken);
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

        if (!runRecord.EvidenceConfirmed)
            return MigrationOperationResult<MigrationDryRunPreview>.Unknown("migration_audit_recovery_required");

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
        var validationById = validation.Records.ToDictionary(item => item.StagedRecordId);
        var effectiveStaged = staged.Select(item => validationById.TryGetValue(item.StagedRecordId, out var row)
            && row.CanonicalPayload is not null
                ? item with { CanonicalPayload = row.CanonicalPayload }
                : item).ToArray();
        var preview = MigrationDryRunPreviewPolicy.Build(
            tenant!.TenantId,
            runId,
            attempt.AttemptId,
            validation,
            effectiveStaged,
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
        var validation = await persistence.FindLatestValidationAsync(tenant, runId, cancellationToken);
        if (validation is null)
            return null;
        var run = await foundation.FindRunAsync(tenant, runId, cancellationToken);
        var attempt = await foundation.FindAttemptAsync(tenant, runId, validation.AttemptId, cancellationToken);
        return WithAttemptContext(
            run.Value is null ? validation : validation with { RunVersion = run.Value.Version },
            run.Value,
            attempt);
    }

    private static MigrationValidationSummary WithAttemptContext(
        MigrationValidationSummary validation,
        MigrationRunRecord? run,
        MigrationAttemptRecord? attempt)
    {
        var nextAction = attempt?.Outcome switch
        {
            MigrationAttemptOutcome.Pending => "Wait for the attempt to finish.",
            MigrationAttemptOutcome.Succeeded => "Review the validation findings before any separately authorized operation.",
            MigrationAttemptOutcome.KnownFailure => "Review the findings and correct eligible source rows through the authorized validation flow.",
            MigrationAttemptOutcome.UnknownOutcome => "Reconcile the unknown outcome before any retry.",
            MigrationAttemptOutcome.Cancelled => "Review the cancelled attempt; no completion is inferred.",
            _ => "Review the attempt outcome before continuing."
        };
        return validation with
        {
            OwnerActorId = run?.ActorId,
            StageStatus = run?.Status,
            AttemptOutcome = attempt?.Outcome,
            Failure = attempt?.Outcome is MigrationAttemptOutcome.KnownFailure or MigrationAttemptOutcome.UnknownOutcome
                ? attempt.SafeOutcomeCode
                : null,
            NextAction = nextAction
        };
    }

    internal static HashSet<int> DuplicateBusinessKeySequences(
        IReadOnlyDictionary<int, MigrationBusinessIdentityResolution> identities) => identities
            .Where(item => item.Value.State == MigrationBusinessIdentityState.Valid && item.Value.Key is not null)
            .GroupBy(item => item.Value.Key!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.Key))
            .ToHashSet();

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

    public async Task<MigrationNonAuthoritativePreview?> ReadPreviewAsync(
        TenantContext tenant,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var dryRun = await ReadDryRunAsync(tenant, runId, cancellationToken);
        if (dryRun is null)
            return null;
        var validation = await persistence.FindValidationAsync(tenant, runId, dryRun.ValidationAttemptId, cancellationToken);
        if (validation is null)
            return null;

        return new MigrationNonAuthoritativePreview(
            runId,
            tenant.TenantId,
            "preview",
            dryRun.Rows.Count(item => item.PlannedAction == MigrationPlannedAction.Create),
            validation.Records.Count(item => item.FindingCodes.Any(code => code.Contains("duplicate", StringComparison.OrdinalIgnoreCase))),
            dryRun.UnresolvedDependencyCount,
            dryRun.ControlTotals,
            dryRun.ExceptionCount,
            AuthoritativeImport: false,
            ApprovalCreated: false,
            ReadinessCreated: false,
            RunStateChanged: false,
            dryRun.Rows);
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
        bool correctedRetry,
        CancellationToken cancellationToken)
    {
        var run = MigrationRun.Rehydrate(runRecord);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var target = run.Status switch
            {
                MigrationRunStatus.Draft => MigrationRunStatus.Prepared,
                MigrationRunStatus.Prepared => MigrationRunStatus.Validating,
                MigrationRunStatus.ValidationFailed when correctedRetry => MigrationRunStatus.Corrected,
                MigrationRunStatus.Corrected when correctedRetry => MigrationRunStatus.Prepared,
                _ => (MigrationRunStatus?)null
            };
            if (target is null)
                break;
            var transitioned = await foundation.TransitionRunAsync(
                requestContext,
                run.RunId,
                target.Value,
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
            || (!correctedRetry && run.Status == MigrationRunStatus.ValidationFailed)
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
        var currentRun = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
        if (!currentRun.Succeeded || currentRun.Value is not { } confirmedRun)
            return currentRun.Kind == MigrationResultKind.UnknownOutcome
                ? MigrationOperationResult<MigrationValidationSummary>.Unknown(currentRun.Code)
                : MigrationOperationResult<MigrationValidationSummary>.Failure(currentRun.Code, currentRun.IsSafeToRetry);
        var transition = await foundation.TransitionRunAsync(
            requestContext,
            run.RunId,
            MigrationRunStatus.ValidationFailed,
            confirmedRun.Version,
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

        var authorized = scopeResolver.ResolveCurrent(tenant);
        var requested = ResolveRequestedScope(tenant, source.CompanyId, source.BranchId, source.WarehouseId);
        return authorized.Allowed
            && authorized.Scope is { } authorizedScope
            && requested.Allowed
            && requested.Scope is { } requestedScope
            && authorizedScope.ContainsAuthorizedDescendant(requestedScope);
    }

    private MigrationReferenceCheck? ScopeFinding(
        TenantContext tenant,
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
            var resolved = scopeOwnership.Resolve(tenant, requested);
            return resolved.Allowed
                && resolved.Scope is { } candidate
                && authorizedScope.ContainsAuthorizedDescendant(candidate)
                ? null
                : new(MigrationReferenceState.Missing, MigrationFindingCategory.Scope, "migration_row_scope_denied", "The row organization scope is outside the current authorized scope.");
        }
        catch (ArgumentException)
        {
            return new(MigrationReferenceState.Missing, MigrationFindingCategory.Scope, "migration_row_scope_invalid", "The row organization scope hierarchy is invalid.");
        }
    }

    private TenantWorkScopeResolution ResolveRequestedScope(
        TenantContext tenant,
        Guid? companyId,
        Guid? branchId,
        Guid? warehouseId)
    {
        try
        {
            return scopeOwnership.Resolve(tenant, new TenantWorkScopeRequest(companyId, branchId, warehouseId));
        }
        catch (ArgumentException)
        {
            return TenantWorkScopeResolution.Denied("scope_invalid");
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
