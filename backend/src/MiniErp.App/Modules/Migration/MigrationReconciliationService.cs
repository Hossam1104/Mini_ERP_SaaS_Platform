#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

public sealed class MigrationReconciliationService
{
    private readonly MigrationFoundationService foundation;
    private readonly IMigrationFoundationPersistence foundationPersistence;
    private readonly IMigrationExecutionPersistence executionPersistence;
    private readonly IMigrationReconciliationPersistence persistence;
    private readonly MigrationValidationService validation;
    private readonly MigrationExecutionService execution;
    private readonly IMigrationReconciliationApprovalPolicy approvalPolicy;
    private readonly IFoundationAuditEvidenceSink audit;
    private readonly TimeProvider clock;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    // ponytail: keep one gate per touched migration run; add ref-counted eviction if migration volume makes this cache material.
    private readonly ConcurrentDictionary<(TenantId Tenant, Guid RunId), SemaphoreSlim> workflowGates = new();

    public MigrationReconciliationService(
        MigrationFoundationService foundation,
        IMigrationFoundationPersistence foundationPersistence,
        IMigrationExecutionPersistence executionPersistence,
        IMigrationReconciliationPersistence persistence,
        MigrationValidationService validation,
        MigrationExecutionService execution,
        IMigrationReconciliationApprovalPolicy approvalPolicy,
        IFoundationAuditEvidenceSink audit,
        TimeProvider? timeProvider = null)
    {
        this.foundation = foundation;
        this.foundationPersistence = foundationPersistence;
        this.executionPersistence = executionPersistence;
        this.persistence = persistence;
        this.validation = validation;
        this.execution = execution;
        this.approvalPolicy = approvalPolicy;
        this.audit = audit;
        clock = timeProvider ?? TimeProvider.System;
    }

    public Task<MigrationOperationResult<MigrationReconciliationRecord>> ReconcileAsync(
        FoundationRequestContext requestContext, MigrationReconcileRequest request, CancellationToken cancellationToken = default) =>
        WithRunGateAsync(requestContext, request.RunId, () => ReconcileCoreAsync(requestContext, request, cancellationToken), cancellationToken);

    private async Task<MigrationOperationResult<MigrationReconciliationRecord>> ReconcileCoreAsync(
        FoundationRequestContext requestContext, MigrationReconcileRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryCaller(requestContext, out var tenant, out _) || request.RunId == Guid.Empty
            || request.ExpectedRunVersion is not { Length: > 0 } || !FoundationCorrelation.IsValid(request.IdempotencyKey))
            return MigrationOperationResult<MigrationReconciliationRecord>.Rejected("migration_reconciliation_request_invalid");
        if (!await validation.IsResourceAuthorizedAsync(requestContext, request.RunId, cancellationToken))
            return MigrationOperationResult<MigrationReconciliationRecord>.Rejected("migration_source_scope_denied");

        var runResult = await foundation.FindRunAsync(tenant, request.RunId, cancellationToken);
        if (!runResult.Succeeded || runResult.Value is not { } run)
            return MigrationOperationResult<MigrationReconciliationRecord>.Rejected(runResult.Code);

        MigrationPersistenceResult<MigrationReconciliationRecord> saved;
        if (!run.Version.AsSpan().SequenceEqual(request.ExpectedRunVersion))
        {
            var replayRecord = await persistence.FindLatestAsync(tenant, run.RunId, cancellationToken);
            if (replayRecord?.IdempotencyKey != request.IdempotencyKey)
                return MigrationOperationResult<MigrationReconciliationRecord>.Rejected("migration_run_version_conflict");
            var replayCapture = await CaptureAsync(requestContext, run, cancellationToken);
            if (replayCapture is null || !FixedEquals(replayCapture.Fingerprint, replayRecord.EvidenceFingerprint))
                return MigrationOperationResult<MigrationReconciliationRecord>.Rejected("migration_reconciliation_idempotency_conflict");
            saved = MigrationPersistenceResult<MigrationReconciliationRecord>.Replay(replayRecord with { IsCurrent = true });
        }
        else
        {
            if (run.Status is not (MigrationRunStatus.Completed or MigrationRunStatus.PartiallyCompleted or MigrationRunStatus.Failed
                or MigrationRunStatus.OutcomeUnknown or MigrationRunStatus.ReconciliationPending or MigrationRunStatus.Reconciled))
                return MigrationOperationResult<MigrationReconciliationRecord>.Rejected("migration_reconciliation_state_invalid");

            var capture = await CaptureAsync(requestContext, run, cancellationToken);
            if (capture is null)
                return MigrationOperationResult<MigrationReconciliationRecord>.Rejected("reconciliation_evidence_incomplete");

            var latest = await persistence.FindLatestAsync(tenant, run.RunId, cancellationToken);
            var record = CreateRecord(tenant, run, capture, request.IdempotencyKey, (latest?.VersionNumber ?? 0) + 1);
            saved = await persistence.SaveAsync(tenant, new SaveMigrationReconciliationCommand(record, run.Version), cancellationToken);
        }
        if (!saved.Succeeded || saved.Value is null)
            return Map(saved, "migration_reconciliation_persistence_unknown");

        var auditRun = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
        if (auditRun.Value is { } currentAuditRun) run = currentAuditRun;
        if (!await AppendAuditAsync(requestContext, run, "migration.reconciliation.calculate", request.IdempotencyKey,
                saved.Outcome == MigrationPersistenceOutcome.Replayed ? "replayed" : "reconciliation_recorded", cancellationToken))
            return MigrationOperationResult<MigrationReconciliationRecord>.Unknown("migration_audit_evidence_unavailable");
        var evidence = await foundationPersistence.SetEvidenceStateAsync(tenant, new MigrationEvidenceReference(run.RunId), true, cancellationToken);
        if (!evidence.Succeeded)
            return MigrationOperationResult<MigrationReconciliationRecord>.Unknown("migration_audit_evidence_unavailable");

        if (IsClean(saved.Value))
        {
            var current = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
            if (current.Value is { Status: MigrationRunStatus.ReconciliationPending } pendingRun)
            {
                var reconciled = await foundation.TransitionRunAsync(requestContext, run.RunId, MigrationRunStatus.Reconciled, pendingRun.Version, cancellationToken);
                if (!reconciled.Succeeded)
                {
                    var after = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
                    if (after.Value is not { Status: MigrationRunStatus.Reconciled })
                        return reconciled.Kind == MigrationResultKind.UnknownOutcome
                            ? MigrationOperationResult<MigrationReconciliationRecord>.Unknown(reconciled.Code)
                            : MigrationOperationResult<MigrationReconciliationRecord>.Failure(reconciled.Code);
                }
            }
            else if (current.Value is not { Status: MigrationRunStatus.Reconciled }
                && (saved.Outcome != MigrationPersistenceOutcome.Replayed
                    || current.Value is not { Status: MigrationRunStatus.ReadyForHandover or MigrationRunStatus.Closed }))
                return MigrationOperationResult<MigrationReconciliationRecord>.Unknown("migration_reconciliation_lifecycle_unknown");
        }

        var response = saved.Value with { IsCurrent = true };
        return saved.Outcome == MigrationPersistenceOutcome.Replayed
            ? MigrationOperationResult<MigrationReconciliationRecord>.Replay(response)
            : MigrationOperationResult<MigrationReconciliationRecord>.Success(response, response.Status == MigrationReconciliationStatus.Reconciled ? "migration_reconciled" : "migration_reconciliation_blocked");
    }

    public async Task<MigrationReconciliationRecord?> ReadAsync(FoundationRequestContext requestContext, Guid runId, CancellationToken cancellationToken = default)
    {
        if (!TryCaller(requestContext, out var tenant, out _) || runId == Guid.Empty
            || !await validation.IsResourceAuthorizedAsync(requestContext, runId, cancellationToken)) return null;
        var runResult = await foundation.FindRunAsync(tenant, runId, cancellationToken);
        var saved = await persistence.FindLatestAsync(tenant, runId, cancellationToken);
        if (!runResult.Succeeded || runResult.Value is not { } run || saved is null) return null;
        var capture = await CaptureAsync(requestContext, run, cancellationToken);
        var current = capture is not null && FixedEquals(capture.Fingerprint, saved.EvidenceFingerprint);
        return saved with { IsCurrent = current, Readiness = run.Status == MigrationRunStatus.ReadyForHandover && run.EvidenceConfirmed ? saved.Readiness : null };
    }

    public Task<MigrationOperationResult<MigrationReconciliationApprovalRecord>> ApproveAsync(
        FoundationRequestContext requestContext, MigrationApprovalRequest request, CancellationToken cancellationToken = default) =>
        WithRunGateAsync(requestContext, request.RunId, () => ApproveCoreAsync(requestContext, request, cancellationToken), cancellationToken);

    private async Task<MigrationOperationResult<MigrationReconciliationApprovalRecord>> ApproveCoreAsync(
        FoundationRequestContext requestContext, MigrationApprovalRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryCaller(requestContext, out var tenant, out var actor) || request.RunId == Guid.Empty || request.ReconciliationId == Guid.Empty
            || request.ExpectedVersion is not { Length: > 0 } || !FoundationCorrelation.IsValid(request.IdempotencyKey)
            || !Enum.IsDefined(request.Decision) || !ValidReason(request.Reason))
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_approval_request_invalid");

        var rec = await persistence.FindAsync(tenant, request.ReconciliationId, cancellationToken);
        if (rec is null) return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_reconciliation_not_found");
        if (rec.RunId != request.RunId) return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_reconciliation_not_found");
        if (!rec.Version.AsSpan().SequenceEqual(request.ExpectedVersion))
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_reconciliation_version_conflict");
        var runResult = await foundation.FindRunAsync(tenant, rec.RunId, cancellationToken);
        if (!runResult.Succeeded || runResult.Value is not { } run)
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_run_not_found");
        if (!await validation.IsResourceAuthorizedAsync(requestContext, run.RunId, cancellationToken))
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_source_scope_denied");
        var capture = await CaptureAsync(requestContext, run, cancellationToken);
        if (capture is null || !FixedEquals(capture.Fingerprint, rec.EvidenceFingerprint) || rec.Status != MigrationReconciliationStatus.Reconciled)
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_reconciliation_stale");

        var policy = await ResolvePolicyAsync(tenant, run.RunId, cancellationToken);
        if (policy is null) return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("approval_policy_not_configured");
        if (!PolicyMatches(rec, policy)) return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_approval_policy_changed");
        var requirement = rec.Requirements.SingleOrDefault(item => Same(item.Domain, request.Domain) && item.RequirementKey == request.RequirementKey);
        if (requirement is null) return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_approval_requirement_not_found");
        if (!requirement.EligibleActorIds.Contains(actor)) return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_approval_actor_ineligible");
        if (request.Decision == MigrationApprovalDecision.Approved && policy.EnforceSeparationOfDuties && actor == run.ActorId)
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Rejected("migration_approval_self_approval_forbidden");

        var approval = new MigrationReconciliationApprovalRecord(Guid.NewGuid(), tenant.TenantId, run.RunId, rec.Id, rec.VersionNumber,
            rec.AttemptId, rec.EvidenceFingerprint, request.IdempotencyKey, requirement.Domain, requirement.RequirementKey,
            policy.PolicyId, policy.Version, actor, request.Decision, request.Reason?.Trim(), clock.GetUtcNow(), Guid.NewGuid().ToByteArray());
        var saved = await persistence.SaveApprovalAsync(tenant, new CreateMigrationReconciliationApprovalCommand(approval, rec.Version), cancellationToken);
        if (!saved.Succeeded || saved.Value is null) return Map(saved, "migration_approval_persistence_unknown");
        if (!await AppendAuditAsync(requestContext, run, "migration.reconciliation.approve", request.IdempotencyKey,
                saved.Outcome == MigrationPersistenceOutcome.Replayed ? "approval_replayed" : "approval_recorded", cancellationToken))
        {
            await foundationPersistence.SetEvidenceStateAsync(tenant, new MigrationEvidenceReference(run.RunId), false, cancellationToken);
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Unknown("migration_audit_evidence_unavailable");
        }
        var confirmed = await persistence.ConfirmApprovalEvidenceAsync(tenant, saved.Value.Id, cancellationToken);
        if (!confirmed.Succeeded || confirmed.Value is null)
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Unknown("migration_approval_evidence_unavailable");
        var evidence = await foundationPersistence.SetEvidenceStateAsync(tenant, new MigrationEvidenceReference(run.RunId), true, cancellationToken);
        if (!evidence.Succeeded)
            return MigrationOperationResult<MigrationReconciliationApprovalRecord>.Unknown("migration_approval_evidence_unavailable");
        return saved.Outcome == MigrationPersistenceOutcome.Replayed
            ? MigrationOperationResult<MigrationReconciliationApprovalRecord>.Replay(confirmed.Value)
            : MigrationOperationResult<MigrationReconciliationApprovalRecord>.Success(confirmed.Value, "migration_approval_recorded");
    }

    public Task<MigrationOperationResult<MigrationHandoverReadinessSnapshot>> CreateReadinessAsync(
        FoundationRequestContext requestContext, MigrationHandoverRequest request, CancellationToken cancellationToken = default) =>
        WithRunGateAsync(requestContext, request.RunId, () => CreateReadinessCoreAsync(requestContext, request, cancellationToken), cancellationToken);

    private async Task<MigrationOperationResult<MigrationHandoverReadinessSnapshot>> CreateReadinessCoreAsync(
        FoundationRequestContext requestContext, MigrationHandoverRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryCaller(requestContext, out var tenant, out _) || request.RunId == Guid.Empty || request.ReconciliationId == Guid.Empty
            || request.ExpectedVersion is not { Length: > 0 } || !FoundationCorrelation.IsValid(request.IdempotencyKey))
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_readiness_request_invalid");
        var rec = await persistence.FindAsync(tenant, request.ReconciliationId, cancellationToken);
        if (rec is null) return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_reconciliation_not_found");
        if (rec.RunId != request.RunId) return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_reconciliation_not_found");
        if (!rec.Version.AsSpan().SequenceEqual(request.ExpectedVersion))
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_reconciliation_version_conflict");
        var runResult = await foundation.FindRunAsync(tenant, rec.RunId, cancellationToken);
        if (!runResult.Succeeded || runResult.Value is not { } run)
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_run_not_found");
        if (!await validation.IsResourceAuthorizedAsync(requestContext, run.RunId, cancellationToken))
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_source_scope_denied");
        var replayingReadyResult = run.Status == MigrationRunStatus.ReadyForHandover
            && rec.Readiness is { } priorReadiness && priorReadiness.IdempotencyKey == request.IdempotencyKey;
        if ((!replayingReadyResult && run.Status != MigrationRunStatus.Reconciled) || rec.Status != MigrationReconciliationStatus.Reconciled)
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_reconciliation_blocked");
        if (!run.EvidenceConfirmed && !replayingReadyResult)
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_audit_recovery_required");
        var capture = await CaptureAsync(requestContext, run, cancellationToken);
        if (capture is null || !FixedEquals(capture.Fingerprint, rec.EvidenceFingerprint))
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_reconciliation_stale");
        var policy = await ResolvePolicyAsync(tenant, run.RunId, cancellationToken);
        if (policy is null) return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("approval_policy_not_configured");
        if (!PolicyMatches(rec, policy)) return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_approval_policy_changed");
        if (rec.Details.Any(item => item.IsBlocking) || capture.HasUnknown || capture.HasIncompleteDomain
            || rec.RejectedCount + rec.QuarantinedCount + rec.UnresolvedCount > 0)
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_handover_prerequisites_incomplete");
        if (!ApprovalsSatisfied(rec, policy, run.ActorId)) return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected("migration_approval_required");

        var snapshot = new MigrationHandoverReadinessSnapshot(Guid.NewGuid(), tenant.TenantId, run.RunId, rec.Id, rec.VersionNumber,
            rec.AttemptId, rec.EvidenceFingerprint, request.IdempotencyKey, clock.GetUtcNow(), true, false, false, false, false,
            "ready_for_handover", Guid.NewGuid().ToByteArray());
        var saved = await persistence.SaveReadinessAsync(tenant, new CreateMigrationHandoverReadinessCommand(snapshot, rec.Version), cancellationToken);
        if (!saved.Succeeded || saved.Value is null) return Map(saved, "migration_readiness_persistence_unknown");

        var currentRun = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
        if (currentRun.Value is { Status: MigrationRunStatus.Reconciled } reconciledRun)
        {
            var transitioned = await foundation.TransitionRunAsync(requestContext, run.RunId, MigrationRunStatus.ReadyForHandover, reconciledRun.Version, cancellationToken);
            if (!transitioned.Succeeded)
            {
                var after = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
                if (after.Value is not { Status: MigrationRunStatus.ReadyForHandover })
                    return transitioned.Kind == MigrationResultKind.UnknownOutcome
                        ? MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Unknown(transitioned.Code)
                        : MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Rejected(transitioned.Code);
            }
            currentRun = await foundation.FindRunAsync(tenant, run.RunId, cancellationToken);
        }
        if (currentRun.Value is not { Status: MigrationRunStatus.ReadyForHandover } readyRun)
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Unknown("migration_readiness_lifecycle_unknown");
        if (!await AppendAuditAsync(requestContext, readyRun, "migration.handover.ready", request.IdempotencyKey,
                saved.Outcome == MigrationPersistenceOutcome.Replayed ? "readiness_replayed" : "ready_for_handover", cancellationToken))
        {
            await foundationPersistence.SetEvidenceStateAsync(tenant, new MigrationEvidenceReference(run.RunId), false, cancellationToken);
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Unknown("migration_audit_evidence_unavailable");
        }
        var readinessEvidence = await foundationPersistence.SetEvidenceStateAsync(tenant, new MigrationEvidenceReference(run.RunId), true, cancellationToken);
        if (!readinessEvidence.Succeeded)
            return MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Unknown("migration_readiness_evidence_unavailable");
        return saved.Outcome == MigrationPersistenceOutcome.Replayed
            ? MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Replay(saved.Value)
            : MigrationOperationResult<MigrationHandoverReadinessSnapshot>.Success(saved.Value, "ready_for_handover");
    }

    private async Task<EvidenceCapture?> CaptureAsync(FoundationRequestContext context, MigrationRunRecord run, CancellationToken cancellationToken)
    {
        if (context.TenantContext is not { } tenant) return null;
        var validationResult = await validation.ReadValidationAsync(tenant, run.RunId, cancellationToken);
        var preview = await validation.ReadDryRunAsync(tenant, run.RunId, cancellationToken);
        var executionResult = await execution.ReadAsync(context, run.RunId, cancellationToken);
        if (validationResult is null || preview is null || executionResult is null
            || validationResult.TenantId != tenant.TenantId || preview.TenantId != tenant.TenantId
            || validationResult.RunId != run.RunId || preview.RunId != run.RunId || executionResult.TenantId != tenant.TenantId)
            return null;

        var staged = new List<MigrationStagedRecord>();
        for (var offset = 0; ; offset += 1000)
        {
            var page = await validation.ReadStagedRecordsAsync(tenant, run.RunId, offset, 1000, cancellationToken);
            staged.AddRange(page);
            if (page.Count < 1000) break;
        }
        if (staged.Count != validationResult.TotalStagedRecords || staged.Count != preview.TotalStagedRecords
            || staged.Select(item => item.StagedRecordId).Distinct().Count() != staged.Count
            || validationResult.Records.Select(item => item.StagedRecordId).Distinct().Count() != staged.Count
            || preview.Rows.Select(item => item.StagedRecordId).Distinct().Count() != staged.Count)
            return null;

        var attempts = await foundationPersistence.ListAttemptsAsync(tenant, run.RunId, cancellationToken);
        var executionAttempts = attempts.Where(item => item.Operation == MigrationOperationKind.Execution).OrderBy(item => item.Sequence).ToArray();
        if (executionAttempts.Length == 0 || executionAttempts[^1].AttemptId != executionResult.AttemptId) return null;
        var allEffects = new List<MigrationExecutionEffectRecord>();
        var allRepresentations = new List<MigrationEconomicRepresentationRecord>();
        foreach (var attempt in executionAttempts)
        {
            allEffects.AddRange(await executionPersistence.ListEffectsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken));
            allRepresentations.AddRange(await executionPersistence.ListRepresentationsAsync(tenant, run.RunId, attempt.AttemptId, cancellationToken));
        }
        var latestEffects = allEffects.GroupBy(item => item.StagedRecordId)
            .Select(group => group.OrderByDescending(item => executionAttempts.Single(x => x.AttemptId == item.AttemptId).Sequence).First())
            .ToDictionary(item => item.StagedRecordId);
        var latestEffectIds = latestEffects.Values.Select(item => item.Id).ToHashSet();
        var representations = allRepresentations.Where(item => latestEffectIds.Contains(item.EffectId)).OrderBy(item => item.Id).ToArray();
        var validationById = validationResult.Records.ToDictionary(item => item.StagedRecordId);
        var previewById = preview.Rows.ToDictionary(item => item.StagedRecordId);
        var outcomeCounts = new int[6]; // accepted, rejected, duplicate, skipped, quarantined, unresolved
        foreach (var row in staged)
        {
            if (!validationById.TryGetValue(row.StagedRecordId, out var validationRow)
                || !previewById.TryGetValue(row.StagedRecordId, out var previewRow)) { outcomeCounts[5]++; continue; }
            if (validationRow.Disposition == MigrationRecordDisposition.Quarantined) { outcomeCounts[4]++; continue; }
            if (validationRow.FindingCodes.Any(IsDuplicateFinding)) { outcomeCounts[2]++; continue; }
            if (validationRow.Disposition == MigrationRecordDisposition.Rejected) { outcomeCounts[1]++; continue; }
            if (previewRow.PlannedAction == MigrationPlannedAction.Skip) { outcomeCounts[3]++; continue; }
            if (!latestEffects.TryGetValue(row.StagedRecordId, out var effect))
            {
                if (previewRow.PlannedAction == MigrationPlannedAction.MatchReference) outcomeCounts[3]++;
                else outcomeCounts[5]++;
                continue;
            }
            switch (effect.Disposition)
            {
                case MigrationExecutionEffectDisposition.Committed: outcomeCounts[0]++; break;
                case MigrationExecutionEffectDisposition.NonEffect:
                    if (previewRow.PlannedAction == MigrationPlannedAction.Skip) outcomeCounts[3]++;
                    else outcomeCounts[0]++;
                    break;
                case MigrationExecutionEffectDisposition.Failed: outcomeCounts[1]++; break;
                case MigrationExecutionEffectDisposition.Unknown:
                case MigrationExecutionEffectDisposition.Prepared:
                case MigrationExecutionEffectDisposition.Started:
                case MigrationExecutionEffectDisposition.PartialCompleted: outcomeCounts[5]++; break;
                default: outcomeCounts[5]++; break;
            }
        }
        var submitted = outcomeCounts.Sum();
        if (submitted != staged.Count) return null;
        var missingEconomicEffects = MissingEconomicEvidence(staged, latestEffects, executionResult);
        foreach (var missing in missingEconomicEffects.Where(item => item.Effect.Disposition == MigrationExecutionEffectDisposition.Committed))
        {
            outcomeCounts[0]--;
            outcomeCounts[5]++;
        }
        var details = BuildDetails(staged, validationResult, executionResult, latestEffects, representations, missingEconomicEffects);
        var latestOutcomeUnknown = executionAttempts[^1].Outcome == MigrationAttemptOutcome.UnknownOutcome
            || latestEffects.Values.Any(item => item.Disposition == MigrationExecutionEffectDisposition.Unknown)
            || executionResult.RunStatus == MigrationRunStatus.OutcomeUnknown;
        var latestRunIncomplete = run.Status is MigrationRunStatus.PartiallyCompleted or MigrationRunStatus.Failed or MigrationRunStatus.OutcomeUnknown
            || missingEconomicEffects.Count > 0;
        var policy = await ResolvePolicyAsync(tenant, run.RunId, cancellationToken);
        var fingerprint = Fingerprint(run, attempts, validationResult, preview, staged, allEffects, allRepresentations, details, policy);
        return new(validationResult, preview, executionResult, staged, attempts, latestEffects, representations, details,
            outcomeCounts, latestOutcomeUnknown, latestRunIncomplete, fingerprint, policy);
    }

    private IReadOnlyList<MigrationReconciliationDetail> BuildDetails(
        IReadOnlyList<MigrationStagedRecord> staged,
        MigrationValidationSummary validationResult,
        MigrationExecutionResult executionResult,
        IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord> effects,
        IReadOnlyList<MigrationEconomicRepresentationRecord> representations,
        IReadOnlyList<(MigrationStagedRecord Row, MigrationExecutionEffectRecord Effect)> missingEconomicEffects)
    {
        var stagedByEffect = effects.Values.ToDictionary(item => item.Id, item => staged.Single(row => row.StagedRecordId == item.StagedRecordId));
        var rows = new List<MigrationReconciliationDetail>();
        var financeMaps = representations.Where(item => item.OwnerModule == MigrationEconomicOwnerModule.Finance && item.ControlAccountId.HasValue)
            .GroupBy(item => item.EffectId).ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Kind == MigrationEconomicRepresentationKind.FinanceOpeningExpectation).ThenByDescending(item => item.RecordedAt).ToArray());

        foreach (var item in executionResult.EconomicReconciliations ?? [])
        {
            if (!stagedByEffect.TryGetValue(item.EffectId, out var source)) continue;
            MigrationInventoryOpeningPayload? payload;
            try { payload = JsonSerializer.Deserialize<MigrationInventoryOpeningPayload>(source.CanonicalPayload, JsonOptions); }
            catch (JsonException) { payload = null; }
            var mapping = Map(item.EffectId, financeMaps);
            var quantityVariance = item.CanonicalQuantity - item.InventoryQuantity;
            var amountVariance = item.InventoryValue - item.CanonicalValue + item.DeclaredRoundingAdjustment;
            var blocked = !string.Equals(item.Status, "reconciled", StringComparison.OrdinalIgnoreCase)
                || !item.PhysicalQuantityProven || !item.ValuationAmountProven || !item.FinanceAmountProven
                || quantityVariance != 0m || amountVariance != 0m || mapping.ControlAccountId is null;
            rows.Add(new(Guid.Empty, MigrationReconciliationDomain.Inventory, $"inventory:{item.EffectId:N}", payload?.CompanyId,
                payload?.OpeningDate, item.FunctionalCurrencyCode, payload?.CurrencyCode, item.FunctionalCurrencyCode, 1, null, null, null, null,
                amountVariance, item.CanonicalValue, item.InventoryValue, amountVariance, null, null, item.FinancePostedAmount,
                item.InventoryValue, Signed(item.FinancePostedAmount ?? 0m, mapping.Reversal, creditNormal: false), null, null, null, null, item.CanonicalQuantity, item.InventoryQuantity,
                quantityVariance, mapping.ControlAccountId, mapping.PostingRuleId, mapping.PostingRuleVersionNumber, mapping.OwnerSourceId,
                payload?.WarehouseId, payload?.ProductId, payload?.UnitOfMeasureId, mapping.SourceContract, mapping.SourceEvent,
                mapping.MonetaryPolicyId, mapping.MonetaryPolicyVersionNumber, item.InventoryAmountScale, item.InventoryRoundingMode,
                blocked, blocked ? item.SafeCode ?? "inventory_reconciliation_mismatch" : null,
                item.DeclaredRoundingAdjustment is { } adjustment && adjustment != 0m ? "Inventory recorded an explicit valuation rounding adjustment." : null,
                item.EffectId, payload?.ProductId, null));
        }

        foreach (var item in executionResult.ArEconomicReconciliations ?? [])
        {
            var mapping = Map(item.EffectId, financeMaps);
            var signed = Signed(item.FunctionalAmount ?? item.CanonicalAmount, mapping.Reversal, creditNormal: false);
            rows.Add(FinanceDetail(MigrationReconciliationDomain.Ar, item.EffectId, item.CompanyId,
                ReadArDate(stagedByEffect[item.EffectId]), item.CurrencyCode,
                item.TransactionCurrencyCode, item.FunctionalCurrencyCode, item.TransactionAmount ?? item.CanonicalAmount, item.OpenItemOriginalAmount,
                item.FunctionalAmount, item.RecognitionJournalAmount, item.OutstandingAmount, signed, item.FunctionalRoundingDifference, item.RoundingScale, item.RoundingMode,
                item.MonetaryPolicyId, item.MonetaryPolicyVersionNumber, item.ExchangeRateId, item.ExchangeRateVersionId,
                item.ExchangeRateVersionNumber, item.AppliedRate, item.Status, item.SafeCode, mapping, item.CustomerId));
        }
        foreach (var item in executionResult.ApEconomicReconciliations ?? [])
        {
            var mapping = Map(item.EffectId, financeMaps);
            var signed = Signed(item.FunctionalAmount ?? item.CanonicalAmount, mapping.Reversal, creditNormal: false);
            rows.Add(FinanceDetail(MigrationReconciliationDomain.Ap, item.EffectId, item.CompanyId,
                ReadApDate(stagedByEffect[item.EffectId]), item.CurrencyCode,
                item.TransactionCurrencyCode, item.FunctionalCurrencyCode, item.TransactionAmount ?? item.CanonicalAmount, item.OpenItemOriginalAmount,
                item.FunctionalAmount, item.RecognitionJournalAmount, item.OutstandingAmount, signed, item.FunctionalRoundingDifference, item.RoundingScale, item.RoundingMode,
                item.MonetaryPolicyId, item.MonetaryPolicyVersionNumber, item.ExchangeRateId, item.ExchangeRateVersionId,
                item.ExchangeRateVersionNumber, item.AppliedRate, item.Status, item.SafeCode, mapping, item.SupplierId));
        }
        foreach (var item in executionResult.CashBankEconomicReconciliations ?? [])
        {
            var mapping = Map(item.EffectId, financeMaps);
            var signed = Signed(item.FunctionalAmount ?? item.CanonicalAmount, mapping.Reversal, creditNormal: false);
            rows.Add(FinanceDetail(MigrationReconciliationDomain.CashBank, item.EffectId, item.CompanyId, item.OpeningDate, item.CurrencyCode,
                item.TransactionCurrencyCode ?? item.CurrencyCode, item.FunctionalCurrencyCode ?? item.CurrencyCode,
                item.TransactionAmount ?? item.CanonicalAmount, item.TransactionAmount ?? item.CanonicalAmount,
                item.FunctionalAmount ?? item.CanonicalAmount, item.PostedAmount, null, signed, item.FunctionalRoundingDifference, item.RoundingScale, item.RoundingMode,
                item.MonetaryPolicyId, item.MonetaryPolicyVersionNumber, item.ExchangeRateId, item.ExchangeRateVersionId,
                item.ExchangeRateVersionNumber, item.AppliedRate, item.Status, item.SafeCode, mapping, item.CashAccountId,
                item.LinkedAccountId == Guid.Empty ? null : item.LinkedAccountId));
        }

        var glInputs = new List<(MigrationGlOpeningPayload Payload, MigrationStagedRecord Row)>();
        foreach (var row in staged.Where(item => item.RecordType == MigrationCanonicalRecordType.GlOpening))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<MigrationGlOpeningPayload>(row.CanonicalPayload, JsonOptions);
                if (payload is not null) glInputs.Add((payload, row));
            }
            catch (JsonException) { }
        }
        var glGroups = (executionResult.GlEconomicReconciliations ?? []).GroupBy(item => (item.CompanyId, item.OpeningDate, Currency: item.CurrencyCode, item.JournalId))
            .Select(group => group.First()).ToArray();
        var glLines = (executionResult.GlEconomicReconciliations ?? [])
            .SelectMany(reconciliation => (reconciliation.Lines ?? []).Select(line => (Reconciliation: reconciliation, Line: line)))
            .GroupBy(item => (item.Line.AccountId, item.Reconciliation.CompanyId, item.Reconciliation.OpeningDate,
                item.Reconciliation.CurrencyCode, item.Reconciliation.JournalId))
            .Select(group => group.First()).ToArray();
        foreach (var group in glGroups)
        {
            foreach (var entry in glLines.Where(item => item.Reconciliation.EffectId == group.EffectId))
            {
                var line = entry.Line;
                var sourceRows = glInputs.Where(item => item.Payload.CompanyId == group.CompanyId
                    && item.Payload.OpeningDate == group.OpeningDate && item.Payload.CurrencyCode == group.CurrencyCode
                    && item.Payload.AccountId == line.AccountId).ToArray();
                var debit = sourceRows.Sum(item => item.Payload.Debit ?? 0m);
                var credit = sourceRows.Sum(item => item.Payload.Credit ?? 0m);
                var sourceAmount = debit - credit;
                var target = line.TargetSignedAmount;
                var established = line.EstablishedSignedAmount;
                var sourceVariance = string.IsNullOrWhiteSpace(line.SourceLineReference) ? 0m : target - sourceAmount;
                var posted = line.ResidualDebit - line.ResidualCredit;
                var blocking = sourceVariance != 0m || target - established != posted
                    || !string.Equals(group.Status, "reconciled", StringComparison.OrdinalIgnoreCase);
                rows.Add(new MigrationReconciliationDetail
                {
                    Id = Guid.Empty,
                    Domain = MigrationReconciliationDomain.Gl, ScopeKey = $"line:{group.CompanyId:N}:{group.OpeningDate:yyyyMMdd}:{group.CurrencyCode}:{group.JournalId:N}:{line.AccountId:N}",
                    CompanyId = group.CompanyId, OpeningDate = group.OpeningDate, CurrencyCode = group.CurrencyCode,
                    SourceCount = sourceRows.Length, SourceDebit = debit, SourceCredit = credit,
                    TargetDebit = Math.Max(target, 0m), TargetCredit = Math.Max(-target, 0m), Variance = target - established,
                    SourceAmount = sourceAmount, TargetAmount = target, AmountVariance = sourceVariance,
                    SubsidiaryEstablishedAmount = established, GlControlAmount = posted, ControlAccountId = line.IsControlAccount ? line.AccountId : null,
                    SourceContract = "migration-gl-opening.v1", SourceEvent = "opening", IsBlocking = blocking,
                    FindingCode = blocking ? "gl_opening_variance" : null,
                    Explanation = line.IsControlAccount ? "Persisted Finance GL control-line evidence." : null,
                    EffectId = group.EffectId, OwnerReferenceId = line.IsControlAccount ? line.AccountId : null
                });
            }
        }

        var mappedOwners = rows.Where(item => item.Domain != MigrationReconciliationDomain.Gl && item.ControlAccountId.HasValue && item.FunctionalAmount.HasValue).ToArray();
        foreach (var group in mappedOwners.GroupBy(item => (item.CompanyId, item.OpeningDate, item.FunctionalCurrencyCode, AccountId: item.ControlAccountId!.Value)))
        {
            var targetLine = glLines.Where(item => item.Line.AccountId == group.Key.AccountId
                && item.Reconciliation.CompanyId == group.Key.CompanyId && item.Reconciliation.OpeningDate == group.Key.OpeningDate
                && item.Reconciliation.CurrencyCode == group.Key.FunctionalCurrencyCode).ToArray();
            var expected = group.Sum(item => item.GlControlAmount!.Value);
            var target = targetLine.Sum(item => item.Line.TargetSignedAmount);
            var established = targetLine.Sum(item => item.Line.EstablishedSignedAmount);
            var diff = expected - target;
            var policyIds = group.Where(item => item.RoundingPolicyId.HasValue).Select(item => (item.RoundingPolicyId, item.RoundingPolicyVersionNumber, item.RoundingMode, item.RoundingScale)).Distinct().ToArray();
            var allowed = diff != 0m && policyIds.Length == 1 && group.All(item => item.RoundingPolicyId.HasValue)
                && group.Sum(item => item.AmountVariance ?? 0m) == diff;
            var block = targetLine.Length == 0 || diff != 0m && !allowed;
            rows.Add(new MigrationReconciliationDetail
            {
                Id = Guid.Empty,
                Domain = MigrationReconciliationDomain.Gl,
                ScopeKey = $"control:{group.Key.CompanyId:N}:{group.Key.OpeningDate:yyyyMMdd}:{group.Key.FunctionalCurrencyCode}:{group.Key.AccountId:N}",
                CompanyId = group.Key.CompanyId, OpeningDate = group.Key.OpeningDate, CurrencyCode = group.Key.FunctionalCurrencyCode,
                SourceCount = group.Sum(item => item.SourceCount), Variance = diff, SourceAmount = expected, TargetAmount = target,
                AmountVariance = diff, SubsidiaryEstablishedAmount = established, GlControlAmount = established, ControlAccountId = group.Key.AccountId,
                SourceContract = group.Select(item => item.SourceContract).Distinct().Count() == 1 ? group.First().SourceContract : null,
                SourceEvent = group.Select(item => item.SourceEvent).Distinct().Count() == 1 ? group.First().SourceEvent : null,
                RoundingPolicyId = allowed ? policyIds[0].RoundingPolicyId : null, RoundingPolicyVersionNumber = allowed ? policyIds[0].RoundingPolicyVersionNumber : null,
                RoundingScale = allowed ? policyIds[0].RoundingScale : null, RoundingMode = allowed ? policyIds[0].RoundingMode : null, IsBlocking = block,
                FindingCode = block ? targetLine.Length == 0 ? "reconciliation_evidence_incomplete" : "cross_ledger_control_variance" : null,
                Explanation = allowed ? "The persisted Finance monetary policy records this exact rounding difference." : null,
                OwnerReferenceId = group.Key.AccountId
            });
        }
        foreach (var missing in missingEconomicEffects)
            rows.Add(MissingDetail(missing.Row, missing.Effect));
        return rows;
    }

    private static MigrationReconciliationDetail FinanceDetail(
        MigrationReconciliationDomain domain, Guid effectId, Guid companyId, DateOnly? openingDate, string currency,
        string? transactionCurrency, string? functionalCurrency, decimal transactionAmount, decimal? sourceOwnerAmount,
        decimal? functionalAmount, decimal? establishedFunctional, decimal? establishedTransactionAmount, decimal signedFunctional, decimal? roundingDifference,
        int? roundingScale, string? roundingMode, Guid? policyId, int? policyVersion, Guid? rateId, Guid? rateVersionId,
        int? rateVersion, decimal? appliedRate, string status, string? safeCode, FinanceMapping mapping,
        Guid? ownerReferenceId = null, Guid? linkedAccountId = null)
    {
        var variance = signedFunctional - (establishedFunctional is null ? 0m
            : Signed(establishedFunctional.Value, mapping.Reversal, creditNormal: false));
        var transactionVariance = sourceOwnerAmount is null ? transactionAmount
            : transactionAmount - sourceOwnerAmount.Value;
        var mapped = mapping.ControlAccountId.HasValue && mapping.PostingRuleId.HasValue && mapping.PostingRuleVersionNumber.HasValue
            && !string.IsNullOrWhiteSpace(mapping.SourceContract) && !string.IsNullOrWhiteSpace(mapping.SourceEvent);
        var authorizedRounding = IsAuthorizedRounding(transactionCurrency, functionalCurrency, transactionAmount, functionalAmount,
            appliedRate, roundingDifference, roundingScale, roundingMode, policyId, policyVersion);
        var roundingExplainsVariance = authorizedRounding && roundingDifference is { } difference
            && (variance == 0m || variance == difference);
        var incomplete = !string.Equals(status, "reconciled", StringComparison.OrdinalIgnoreCase) || !mapped
            || establishedFunctional is null || openingDate is null || sourceOwnerAmount is null;
        var blocking = incomplete || transactionVariance != 0m || variance != 0m && !roundingExplainsVariance;
        return new MigrationReconciliationDetail
        {
            Id = Guid.Empty, Domain = domain, ScopeKey = $"{domain}:{effectId:N}", CompanyId = companyId, OpeningDate = openingDate,
            CurrencyCode = currency, TransactionCurrencyCode = transactionCurrency, FunctionalCurrencyCode = functionalCurrency,
            EffectId = effectId, OwnerReferenceId = ownerReferenceId, LinkedAccountId = linkedAccountId ?? mapping.ControlAccountId,
            SourceCount = 1, Variance = variance, SourceAmount = transactionAmount, TargetAmount = sourceOwnerAmount,
            AmountVariance = transactionVariance + (authorizedRounding ? roundingDifference ?? 0m : 0m),
            OwnerRoundingDifference = roundingDifference, TransactionAmount = transactionAmount, FunctionalAmount = functionalAmount,
            SubsidiaryEstablishedAmount = establishedTransactionAmount ?? establishedFunctional, GlControlAmount = signedFunctional,
            ExchangeRateId = rateId, ExchangeRateVersionId = rateVersionId,
            ExchangeRateVersionNumber = rateVersion, AppliedRate = appliedRate, ControlAccountId = mapping.ControlAccountId,
            PostingRuleId = mapping.PostingRuleId, PostingRuleVersionNumber = mapping.PostingRuleVersionNumber, OwnerSourceId = mapping.OwnerSourceId,
            SourceContract = mapping.SourceContract, SourceEvent = mapping.SourceEvent,
            RoundingPolicyId = authorizedRounding ? policyId : null, RoundingPolicyVersionNumber = authorizedRounding ? policyVersion : null,
            RoundingScale = authorizedRounding ? roundingScale : null, RoundingMode = authorizedRounding ? roundingMode : null,
            IsBlocking = blocking,
            FindingCode = blocking ? safeCode ?? (!mapped ? "reconciliation_evidence_incomplete" : "subsidiary_reconciliation_variance") : null,
            Explanation = authorizedRounding ? "Finance recorded the exact functional-currency rounding difference under the persisted monetary policy." : null
        };
    }

    private static bool IsAuthorizedRounding(string? transactionCurrency, string? functionalCurrency, decimal transactionAmount,
        decimal? functionalAmount, decimal? appliedRate, decimal? difference, int? scale, string? mode, Guid? policyId, int? policyVersion)
    {
        if (difference is not { } rounding || rounding == 0m || functionalAmount is not { } functional
            || scale is not { } roundingScale || mode is null || !policyId.HasValue || !policyVersion.HasValue
            || roundingScale is < 0 or > 8
            || !string.Equals(mode, "ToEven", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mode, "AwayFromZero", StringComparison.OrdinalIgnoreCase)) return false;
        var sameCurrency = string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        if (!sameCurrency && appliedRate is not > 0m) return false;
        var unrounded = transactionAmount * (appliedRate ?? 1m);
        var roundingMode = string.Equals(mode, "ToEven", StringComparison.OrdinalIgnoreCase)
            ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;
        return functional - unrounded == rounding && decimal.Round(unrounded, roundingScale, roundingMode) == functional;
    }

    private static FinanceMapping Map(Guid effectId, IReadOnlyDictionary<Guid, MigrationEconomicRepresentationRecord[]> byEffect)
    {
        if (!byEffect.TryGetValue(effectId, out var records) || records.Length == 0) return FinanceMapping.Empty;
        var selected = records[0];
        if (records.Select(item => (item.ControlAccountId, item.PostingRuleId, item.PostingRuleVersionNumber)).Distinct().Count() != 1)
            return FinanceMapping.Empty;
        return new(selected.ControlAccountId, selected.PostingRuleId, selected.PostingRuleVersionNumber, selected.OwnerSourceId,
            selected.SourceContract, selected.SourceEvent, selected.Reversal, selected.MonetaryPolicyId, selected.MonetaryPolicyVersionNumber);
    }

    private async Task<MigrationReconciliationApprovalPolicy?> ResolvePolicyAsync(TenantContext tenant, Guid runId, CancellationToken cancellationToken)
    {
        var policy = await approvalPolicy.ResolveAsync(tenant, runId, clock.GetUtcNow(), cancellationToken);
        if (policy is null) return null;
        if (string.IsNullOrWhiteSpace(policy.PolicyId) || policy.PolicyId.Length > 128 || policy.Version < 1
            || policy.EffectiveFrom > clock.GetUtcNow() || policy.EffectiveTo is { } to && to <= clock.GetUtcNow()
            || policy.Requirements is null || policy.Requirements.Count == 0)
            return null;
        if (policy.Requirements.Any(item => string.IsNullOrWhiteSpace(item.Domain) || string.IsNullOrWhiteSpace(item.RequirementKey)
            || item.RequirementKey.Length > 128 || item.RequiredCount < 1 || item.EligibleActorIds.Count < item.RequiredCount
            || item.EligibleActorIds.Any(id => id == Guid.Empty) || item.EligibleActorIds.Distinct().Count() != item.EligibleActorIds.Count)
            || policy.Requirements.GroupBy(item => (item.Domain, item.RequirementKey), StringTupleComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            return null;
        return policy;
    }

    private MigrationReconciliationRecord CreateRecord(TenantContext tenant, MigrationRunRecord run, EvidenceCapture x, string idempotencyKey, int versionNumber)
    {
        var id = Guid.NewGuid();
        var details = x.Details.Select(item => item with { Id = StableId(id, item.Domain, item.ScopeKey) }).ToArray();
        var policy = x.Policy;
        var blocked = details.Any(item => item.IsBlocking) || x.HasUnknown || x.HasIncompleteDomain
            || x.OutcomeCounts[1] + x.OutcomeCounts[4] + x.OutcomeCounts[5] > 0;
        var gl = details.Where(item => item.Domain == MigrationReconciliationDomain.Gl && item.SourceDebit.HasValue)
            .GroupBy(item => item.ScopeKey.StartsWith("line:", StringComparison.Ordinal) ? "ledger" : "control", StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();
        var sourceDebit = x.Staged.Where(item => item.RecordType == MigrationCanonicalRecordType.GlOpening)
            .Sum(item => ReadGl(item)?.Debit ?? 0m);
        var sourceCredit = x.Staged.Where(item => item.RecordType == MigrationCanonicalRecordType.GlOpening)
            .Sum(item => ReadGl(item)?.Credit ?? 0m);
        var targetDebit = x.Execution.GlEconomicReconciliations?.GroupBy(item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, item.JournalId))
            .Sum(group => group.First().TargetDebit) ?? 0m;
        var targetCredit = x.Execution.GlEconomicReconciliations?.GroupBy(item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, item.JournalId))
            .Sum(group => group.First().TargetCredit) ?? 0m;
        var actualDebit = x.Execution.GlEconomicReconciliations?.GroupBy(item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, item.JournalId))
            .Sum(group => group.First().EstablishedDebit) ?? 0m;
        var actualCredit = x.Execution.GlEconomicReconciliations?.GroupBy(item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, item.JournalId))
            .Sum(group => group.First().EstablishedCredit) ?? 0m;
        var required = policy?.Requirements.Sum(item => item.RequiredCount) ?? 0;
        return new(id, tenant.TenantId, run.RunId, x.Execution.AttemptId, versionNumber, x.Fingerprint, idempotencyKey,
            blocked ? MigrationReconciliationStatus.Blocked : MigrationReconciliationStatus.Reconciled, clock.GetUtcNow(), clock.GetUtcNow(),
            x.OutcomeCounts.Sum(), x.OutcomeCounts[0], x.OutcomeCounts[1], x.OutcomeCounts[2], x.OutcomeCounts[3], x.OutcomeCounts[4],
            x.OutcomeCounts[5], required, 0, sourceDebit, sourceCredit, targetDebit, targetCredit,
            (sourceDebit - sourceCredit) - (actualDebit - actualCredit), Guid.NewGuid().ToByteArray(), false,
            policy?.PolicyId, policy?.Version, policy is null ? "approval_policy_not_configured" : "configured",
            policy?.EffectiveFrom, policy?.EffectiveTo, policy?.EnforceSeparationOfDuties ?? false,
            policy?.Requirements ?? [], details, [], null);
    }

    private static string Fingerprint(MigrationRunRecord run, IReadOnlyList<MigrationAttemptRecord> attempts,
        MigrationValidationSummary validationResult, MigrationDryRunPreview preview, IReadOnlyList<MigrationStagedRecord> staged,
        IReadOnlyList<MigrationExecutionEffectRecord> effects, IReadOnlyList<MigrationEconomicRepresentationRecord> representations,
        IReadOnlyList<MigrationReconciliationDetail> details, MigrationReconciliationApprovalPolicy? policy)
    {
        var values = new List<string> { $"tenant:{run.TenantId.Value:D}", $"run:{run.RunId:D}", $"validation:{validationResult.ValidationResultId:D}:{validationResult.AttemptId:D}", $"preview:{preview.PreviewId:D}:{preview.AttemptId:D}" };
        values.AddRange(attempts.OrderBy(item => item.Sequence).Select(item => JsonSerializer.Serialize(new { item.AttemptId, item.Sequence, item.PreviousAttemptId, item.Operation, item.Outcome, item.RequestFingerprint, Version = Convert.ToHexString(item.Version) }, JsonOptions)));
        values.AddRange(staged.OrderBy(item => item.SourceSequence).Select(item => JsonSerializer.Serialize(new { item.StagedRecordId, item.SourceSequence, item.RecordType, item.PayloadHash, item.PackageHash, item.SourceSnapshotHash }, JsonOptions)));
        values.AddRange(validationResult.Records.OrderBy(item => item.SourceSequence).Select(item => JsonSerializer.Serialize(item, JsonOptions)));
        values.AddRange(preview.Rows.OrderBy(item => item.SourceSequence).Select(item => JsonSerializer.Serialize(item, JsonOptions)));
        values.AddRange(effects.OrderBy(item => item.StagedRecordId).ThenBy(item => item.AttemptId).Select(item => JsonSerializer.Serialize(new { item.Id, item.AttemptId, item.StagedRecordId, item.Disposition, item.SafeCode, Version = Convert.ToHexString(item.Version) }, JsonOptions)));
        values.AddRange(representations.OrderBy(item => item.Id).Select(item => JsonSerializer.Serialize(new { item.Id, item.AttemptId, item.EffectId, item.OwnerModule, item.Kind, item.OwnerId, item.Status, item.EvidenceVersion, item.SourceContract, item.SourceEvent, item.FunctionalAmount, item.PostingRuleId, item.PostingRuleVersionNumber, item.ControlAccountId, item.OffsetAccountId, item.Reversal, item.SourceEvidenceId, item.SourceEvidenceVersion, item.OwnerSourceId, item.TransactionCurrencyCode, item.TransactionAmount, item.ExpectedFunctionalCurrencyCode, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.AppliedRate, item.MonetaryPolicyId, item.MonetaryPolicyVersionNumber, item.RoundingScale, item.RoundingMode, Version = Convert.ToHexString(item.Version) }, JsonOptions)));
        values.AddRange(details.OrderBy(item => item.Domain).ThenBy(item => item.ScopeKey).Select(item => JsonSerializer.Serialize(item with { Id = FingerprintDetailId(item.Domain, item.ScopeKey) }, JsonOptions)));
        if (policy is not null) values.Add(JsonSerializer.Serialize(new { policy.PolicyId, policy.Version, policy.EffectiveFrom, policy.EffectiveTo, policy.EnforceSeparationOfDuties, Requirements = policy.Requirements.OrderBy(item => item.Domain).ThenBy(item => item.RequirementKey).Select(item => new { item.Domain, item.RequirementKey, item.RequiredCount, Actors = item.EligibleActorIds.OrderBy(id => id) }) }, JsonOptions));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));
    }

    private static bool ApprovalsSatisfied(MigrationReconciliationRecord rec, MigrationReconciliationApprovalPolicy policy, Guid preparerActorId) =>
        rec.Requirements.All(requirement => rec.Approvals.Where(item => item.ReconciliationVersion == rec.VersionNumber
            && item.EvidenceFingerprint == rec.EvidenceFingerprint && Same(item.Domain, requirement.Domain)
            && item.RequirementKey == requirement.RequirementKey && item.PolicyId == policy.PolicyId && item.PolicyVersion == policy.Version
            && item.EvidenceConfirmed && item.Decision == MigrationApprovalDecision.Approved && requirement.EligibleActorIds.Contains(item.ActorId)
            && !(policy.EnforceSeparationOfDuties && item.ActorId == preparerActorId))
            .Select(item => item.ActorId).Distinct().Count() >= requirement.RequiredCount);

    private static bool PolicyMatches(MigrationReconciliationRecord rec, MigrationReconciliationApprovalPolicy policy) =>
        rec.ApprovalPolicyId == policy.PolicyId && rec.ApprovalPolicyVersion == policy.Version
        && rec.ApprovalPolicyEffectiveFrom == policy.EffectiveFrom && rec.ApprovalPolicyEffectiveTo == policy.EffectiveTo
        && rec.ApprovalEnforcesSeparationOfDuties == policy.EnforceSeparationOfDuties;

    private async Task<bool> AppendAuditAsync(FoundationRequestContext context, MigrationRunRecord run, string operation, string idempotencyKey, string outcome, CancellationToken cancellationToken)
    {
        try
        {
            var evidence = MigrationAuditEvidenceFactory.Create(context, MigrationRun.Rehydrate(run), operation,
                FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed, outcome,
                idempotencyKey: new MigrationIdempotencyKey(idempotencyKey), occurredAt: clock.GetUtcNow());
            await audit.AppendAsync(evidence, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is FoundationAuditAppendException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCaller(FoundationRequestContext context, out TenantContext tenant, out Guid actor)
    {
        tenant = null!; actor = Guid.Empty;
        if (context.TenantContext is not { } value || context.ActorId is not { } caller || caller == Guid.Empty || value.ActorId != caller) return false;
        tenant = value; actor = caller; return true;
    }

    private async Task<T> WithRunGateAsync<T>(FoundationRequestContext context, Guid runId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (context.TenantContext is not { } tenant || context.ActorId is not { } actor || actor == Guid.Empty
            || actor != tenant.ActorId || runId == Guid.Empty)
            return await action();
        var gate = workflowGates.GetOrAdd((tenant.TenantId, runId), static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { gate.Release(); }
    }

    private static MigrationOperationResult<T> Map<T>(MigrationPersistenceResult<T> result, string unknownCode) => result.Outcome switch
    {
        MigrationPersistenceOutcome.Conflict or MigrationPersistenceOutcome.InvalidReference or MigrationPersistenceOutcome.NotFound => MigrationOperationResult<T>.Rejected(result.Code),
        MigrationPersistenceOutcome.UnknownOutcome => MigrationOperationResult<T>.Unknown(result.Code),
        MigrationPersistenceOutcome.Replayed when result.Value is not null => MigrationOperationResult<T>.Replay(result.Value),
        MigrationPersistenceOutcome.Succeeded when result.Value is not null => MigrationOperationResult<T>.Success(result.Value),
        _ => MigrationOperationResult<T>.Unknown(unknownCode)
    };

    private static bool IsClean(MigrationReconciliationRecord record) => record.Status == MigrationReconciliationStatus.Reconciled;
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
    private static bool Same(string left, string right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool IsDuplicateFinding(string value) => value.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    private static bool ValidReason(string? reason) => reason is null || reason.Length <= 512 && !reason.Any(char.IsControl);
    private static MigrationGlOpeningPayload? ReadGl(MigrationStagedRecord row) { try { return JsonSerializer.Deserialize<MigrationGlOpeningPayload>(row.CanonicalPayload, JsonOptions); } catch (JsonException) { return null; } }
    private static DateOnly? ReadArDate(MigrationStagedRecord row) { try { return JsonSerializer.Deserialize<MigrationArOpeningPayload>(row.CanonicalPayload, JsonOptions)?.OpeningDate; } catch (JsonException) { return null; } }
    private static DateOnly? ReadApDate(MigrationStagedRecord row) { try { return JsonSerializer.Deserialize<MigrationApOpeningPayload>(row.CanonicalPayload, JsonOptions)?.OpeningDate; } catch (JsonException) { return null; } }
    private static Guid StableId(Guid reconciliationId, MigrationReconciliationDomain domain, string scope)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{reconciliationId:N}:{domain}:{scope}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static Guid FingerprintDetailId(MigrationReconciliationDomain domain, string scope)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{domain}:{scope}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static IReadOnlyList<(MigrationStagedRecord Row, MigrationExecutionEffectRecord Effect)> MissingEconomicEvidence(
        IReadOnlyList<MigrationStagedRecord> staged,
        IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord> effects,
        MigrationExecutionResult execution)
    {
        var stagedById = staged.ToDictionary(item => item.StagedRecordId);
        var inventory = (execution.EconomicReconciliations ?? []).Select(item => item.EffectId).ToHashSet();
        var ar = (execution.ArEconomicReconciliations ?? []).Select(item => item.EffectId).ToHashSet();
        var ap = (execution.ApEconomicReconciliations ?? []).Select(item => item.EffectId).ToHashSet();
        var cash = (execution.CashBankEconomicReconciliations ?? []).Select(item => item.EffectId).ToHashSet();
        var gl = (execution.GlEconomicReconciliations ?? []).Where(item => item.Lines is { Count: > 0 }).Select(item => item.EffectId).ToHashSet();
        var missing = new List<(MigrationStagedRecord Row, MigrationExecutionEffectRecord Effect)>();
        foreach (var effect in effects.Values.Where(item => item.Disposition is MigrationExecutionEffectDisposition.Committed
            or MigrationExecutionEffectDisposition.Failed or MigrationExecutionEffectDisposition.Unknown
            or MigrationExecutionEffectDisposition.PartialCompleted or MigrationExecutionEffectDisposition.Prepared or MigrationExecutionEffectDisposition.Started))
        {
            if (!stagedById.TryGetValue(effect.StagedRecordId, out var row)) continue;
            var hasEvidence = row.RecordType switch
            {
                MigrationCanonicalRecordType.InventoryOpening => inventory.Contains(effect.Id),
                MigrationCanonicalRecordType.ArOpening => ar.Contains(effect.Id),
                MigrationCanonicalRecordType.ApOpening => ap.Contains(effect.Id),
                MigrationCanonicalRecordType.CashBankOpening => cash.Contains(effect.Id),
                MigrationCanonicalRecordType.GlOpening => gl.Contains(effect.Id),
                _ => true
            };
            if (!hasEvidence) missing.Add((row, effect));
        }
        return missing;
    }

    private static MigrationReconciliationDetail MissingDetail(MigrationStagedRecord row, MigrationExecutionEffectRecord effect)
    {
        var domain = row.RecordType switch
        {
            MigrationCanonicalRecordType.InventoryOpening => MigrationReconciliationDomain.Inventory,
            MigrationCanonicalRecordType.ArOpening => MigrationReconciliationDomain.Ar,
            MigrationCanonicalRecordType.ApOpening => MigrationReconciliationDomain.Ap,
            MigrationCanonicalRecordType.CashBankOpening => MigrationReconciliationDomain.CashBank,
            _ => MigrationReconciliationDomain.Gl
        };
        var scope = $"missing:{effect.Id:N}";
        Guid? company = null;
        DateOnly? date = null;
        string? currency = null;
        decimal? debit = null;
        decimal? credit = null;
        if (domain == MigrationReconciliationDomain.Inventory && TryPayload<MigrationInventoryOpeningPayload>(row, out var inventory))
        { company = inventory.CompanyId; date = inventory.OpeningDate; currency = inventory.CurrencyCode; }
        else if (domain == MigrationReconciliationDomain.Ar && TryPayload<MigrationArOpeningPayload>(row, out var ar))
        { company = ar.CompanyId; date = ar.OpeningDate; currency = ar.CurrencyCode; }
        else if (domain == MigrationReconciliationDomain.Ap && TryPayload<MigrationApOpeningPayload>(row, out var ap))
        { company = ap.CompanyId; date = ap.OpeningDate; currency = ap.CurrencyCode; }
        else if (domain == MigrationReconciliationDomain.CashBank && TryPayload<MigrationCashBankOpeningPayload>(row, out var cash))
        { company = cash.CompanyId; date = cash.OpeningDate; currency = cash.CurrencyCode; }
        else if (domain == MigrationReconciliationDomain.Gl && TryPayload<MigrationGlOpeningPayload>(row, out var gl))
        { company = gl.CompanyId; date = gl.OpeningDate; currency = gl.CurrencyCode; debit = gl.Debit; credit = gl.Credit; }
        return new MigrationReconciliationDetail
        {
            Id = Guid.Empty, Domain = domain, ScopeKey = scope, CompanyId = company, OpeningDate = date, CurrencyCode = currency,
            EffectId = effect.Id,
            SourceCount = 1, SourceDebit = debit, SourceCredit = credit, SourceAmount = (debit ?? 0m) - (credit ?? 0m),
            IsBlocking = true,
            FindingCode = effect.Disposition switch
            {
                MigrationExecutionEffectDisposition.Failed => effect.SafeCode ?? "migration_domain_execution_failed",
                MigrationExecutionEffectDisposition.Unknown => effect.SafeCode ?? "migration_execution_outcome_unknown",
                MigrationExecutionEffectDisposition.PartialCompleted => effect.SafeCode ?? "migration_domain_execution_partial",
                MigrationExecutionEffectDisposition.Started or MigrationExecutionEffectDisposition.Prepared => "migration_execution_incomplete",
                _ => "reconciliation_evidence_incomplete"
            },
            Explanation = effect.Disposition == MigrationExecutionEffectDisposition.Committed
                ? "The owner returned a committed effect without reconstructable economic reconciliation evidence."
                : "The owner effect did not provide a completed economic reconciliation record."
        };
    }

    private static bool TryPayload<T>(MigrationStagedRecord row, out T payload) where T : class
    {
        try
        {
            payload = JsonSerializer.Deserialize<T>(row.CanonicalPayload, JsonOptions)!;
            return payload is not null;
        }
        catch (JsonException) { payload = null!; return false; }
    }
    private static decimal Signed(decimal value, bool? reversal, bool creditNormal) => value * ((reversal ?? false) ? (creditNormal ? 1m : -1m) : (creditNormal ? -1m : 1m));

    private sealed record FinanceMapping(Guid? ControlAccountId, Guid? PostingRuleId, int? PostingRuleVersionNumber,
        Guid? OwnerSourceId, string? SourceContract, string? SourceEvent, bool? Reversal, Guid? MonetaryPolicyId, int? MonetaryPolicyVersionNumber)
    {
        internal static FinanceMapping Empty { get; } = new(null, null, null, null, null, null, null, null, null);
    }

    private sealed record EvidenceCapture(MigrationValidationSummary Validation, MigrationDryRunPreview Preview,
        MigrationExecutionResult Execution, IReadOnlyList<MigrationStagedRecord> Staged, IReadOnlyList<MigrationAttemptRecord> Attempts,
        IReadOnlyDictionary<Guid, MigrationExecutionEffectRecord> LatestEffects, IReadOnlyList<MigrationEconomicRepresentationRecord> Representations,
        IReadOnlyList<MigrationReconciliationDetail> Details, int[] OutcomeCounts, bool HasUnknown, bool HasIncompleteDomain,
        string Fingerprint, MigrationReconciliationApprovalPolicy? Policy);
}

internal sealed class StringTupleComparer : IEqualityComparer<(string Domain, string RequirementKey)>
{
    internal static StringTupleComparer OrdinalIgnoreCase { get; } = new();
    public bool Equals((string Domain, string RequirementKey) x, (string Domain, string RequirementKey) y) =>
        string.Equals(x.Domain, y.Domain, StringComparison.OrdinalIgnoreCase) && string.Equals(x.RequirementKey, y.RequirementKey, StringComparison.Ordinal);
    public int GetHashCode((string Domain, string RequirementKey) obj) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Domain), StringComparer.Ordinal.GetHashCode(obj.RequirementKey));
}

#pragma warning restore CS1591
