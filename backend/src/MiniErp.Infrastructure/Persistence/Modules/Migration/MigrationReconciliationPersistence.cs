#pragma warning disable CS1591

using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

internal sealed partial class MigrationPersistence
{
    public async Task<MigrationPersistenceResult<MigrationReconciliationRecord>> SaveAsync(
        TenantContext tenant,
        SaveMigrationReconciliationCommand command,
        CancellationToken cancellationToken = default)
    {
        var record = command.Reconciliation;
        if (record.TenantId != tenant.TenantId || record.Id == Guid.Empty || record.RunId == Guid.Empty || record.AttemptId == Guid.Empty
            || record.VersionNumber < 1 || string.IsNullOrWhiteSpace(record.EvidenceFingerprint) || record.EvidenceFingerprint.Length != 64
            || command.ExpectedRunVersion is not { Length: > 0 }
            || !FoundationCorrelation.IsValid(record.IdempotencyKey)
            || record.SubmittedCount != record.AcceptedCount + record.RejectedCount + record.DuplicateCount + record.SkippedCount + record.QuarantinedCount + record.UnresolvedCount)
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.InvalidReference, "migration_reconciliation_invalid");

        await using var db = CreateContext(tenant);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await LockRunAsync(db, tenant, record.RunId, cancellationToken);
        var keyed = await db.Reconciliations.SingleOrDefaultAsync(item => item.RunId == record.RunId && item.IdempotencyKey == record.IdempotencyKey, cancellationToken);
        if (keyed is not null)
            return keyed.EvidenceFingerprint == record.EvidenceFingerprint
                ? MigrationPersistenceResult<MigrationReconciliationRecord>.Replay(await ReadReconciliationAsync(db, keyed, cancellationToken))
                : MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_idempotency_conflict");
        var run = await db.Runs.SingleOrDefaultAsync(item => item.RunId == record.RunId, cancellationToken);
        if (run is null)
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.NotFound, "migration_run_not_found");
        var existing = await db.Reconciliations.SingleOrDefaultAsync(
            item => item.RunId == record.RunId && item.EvidenceFingerprint == record.EvidenceFingerprint,
            cancellationToken);
        if (existing is not null)
        {
            var value = await ReadReconciliationAsync(db, existing, cancellationToken);
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Replay(value);
        }

        if (!run.Version.AsSpan().SequenceEqual(command.ExpectedRunVersion))
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_run_version_conflict");
        if (run.Status is not (MigrationRunStatus.Completed or MigrationRunStatus.PartiallyCompleted or MigrationRunStatus.Failed
            or MigrationRunStatus.OutcomeUnknown or MigrationRunStatus.ReconciliationPending or MigrationRunStatus.Reconciled))
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_state_invalid");

        var latestVersion = await db.Reconciliations.Where(item => item.RunId == record.RunId)
            .Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0;
        if (record.VersionNumber != latestVersion + 1)
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_version_conflict");

        if (run.Status != MigrationRunStatus.ReconciliationPending)
            run.ApplyDomainTransition(MigrationRunStatus.ReconciliationPending, record.CalculatedAt);
        db.Reconciliations.Add(new MigrationReconciliationEntity(record));
        db.ReconciliationDetails.AddRange(record.Details.Select(item => new MigrationReconciliationDetailEntity(tenant.TenantId, record.RunId, record.Id, item)));
        if (record.ApprovalPolicyId is { } policyId && record.ApprovalPolicyVersion is { } policyVersion)
            db.ReconciliationRequirements.AddRange(record.Requirements.Select(item => new MigrationReconciliationRequirementEntity(tenant.TenantId, record.RunId, record.Id, policyId, policyVersion, record.ApprovalEnforcesSeparationOfDuties, item)));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            var saved = await db.Reconciliations.SingleAsync(item => item.Id == record.Id, cancellationToken);
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Success(await ReadReconciliationAsync(db, saved, cancellationToken) with { IsCurrent = true });
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            await using var fresh = CreateContext(tenant);
            var replay = await fresh.Reconciliations.SingleOrDefaultAsync(
                item => item.RunId == record.RunId && item.EvidenceFingerprint == record.EvidenceFingerprint,
                cancellationToken);
            return replay is not null
                ? MigrationPersistenceResult<MigrationReconciliationRecord>.Replay(await ReadReconciliationAsync(fresh, replay, cancellationToken))
                : MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_version_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationReconciliationRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_reconciliation_persistence_unknown");
        }
    }

    public async Task<MigrationReconciliationRecord?> FindLatestAsync(TenantContext tenant, Guid runId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenant);
        var entity = await db.Reconciliations.Where(item => item.RunId == runId)
            .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : await ReadReconciliationAsync(db, entity, cancellationToken);
    }

    public async Task<MigrationReconciliationRecord?> FindAsync(TenantContext tenant, Guid reconciliationId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenant);
        var entity = await db.Reconciliations.SingleOrDefaultAsync(item => item.Id == reconciliationId, cancellationToken);
        return entity is null ? null : await ReadReconciliationAsync(db, entity, cancellationToken);
    }

    public async Task<MigrationPersistenceResult<MigrationReconciliationApprovalRecord>> SaveApprovalAsync(
        TenantContext tenant,
        CreateMigrationReconciliationApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        var record = command.Approval;
        if (record.TenantId != tenant.TenantId || record.Id == Guid.Empty || record.ActorId == Guid.Empty || record.PolicyVersion < 1
            || !FoundationCorrelation.IsValid(record.IdempotencyKey)
            || string.IsNullOrWhiteSpace(record.RequirementKey) || record.RequirementKey.Length > 128
            || command.ExpectedReconciliationVersion is not { Length: > 0 })
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.InvalidReference, "migration_approval_invalid");

        await using var db = CreateContext(tenant);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await LockReconciliationAsync(db, tenant, record.ReconciliationId, cancellationToken);
        var parent = await db.Reconciliations.SingleOrDefaultAsync(item => item.Id == record.ReconciliationId && item.RunId == record.RunId, cancellationToken);
        if (parent is null)
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.NotFound, "migration_reconciliation_not_found");
        if (parent.VersionNumber != record.ReconciliationVersion || parent.AttemptId != record.AttemptId || parent.EvidenceFingerprint != record.EvidenceFingerprint)
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_stale");

        var keyed = await db.ReconciliationApprovals.SingleOrDefaultAsync(item => item.RunId == record.RunId && item.IdempotencyKey == record.IdempotencyKey, cancellationToken);
        if (keyed is not null)
            return SameApproval(keyed, record)
                ? MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Replay(ToRecord(keyed))
                : MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_approval_idempotency_conflict");

        var latestVersion = await db.Reconciliations.Where(item => item.RunId == record.RunId)
            .Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0;
        if (!parent.Version.AsSpan().SequenceEqual(command.ExpectedReconciliationVersion) || latestVersion != record.ReconciliationVersion)
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_stale");

        var existing = await db.ReconciliationApprovals.SingleOrDefaultAsync(item => item.ReconciliationId == record.ReconciliationId
            && item.ReconciliationVersion == record.ReconciliationVersion && item.Domain == record.Domain
            && item.RequirementKey == record.RequirementKey && item.ActorId == record.ActorId, cancellationToken);
        if (existing is not null)
            return SameApproval(existing, record)
                ? MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Replay(ToRecord(existing))
                : MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_approval_conflict");

        db.ReconciliationApprovals.Add(new MigrationReconciliationApprovalEntity(record));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Success(record);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            await using var fresh = CreateContext(tenant);
            var saved = await fresh.ReconciliationApprovals.SingleOrDefaultAsync(item => item.ReconciliationId == record.ReconciliationId
                && item.ReconciliationVersion == record.ReconciliationVersion && item.Domain == record.Domain
                && item.RequirementKey == record.RequirementKey && item.ActorId == record.ActorId, cancellationToken);
            return saved is not null && SameApproval(saved, record)
                ? MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Replay(ToRecord(saved))
                : MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.Conflict, "migration_approval_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_approval_persistence_unknown");
        }
    }

    public async Task<MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>> SaveReadinessAsync(
        TenantContext tenant,
        CreateMigrationHandoverReadinessCommand command,
        CancellationToken cancellationToken = default)
    {
        var record = command.Snapshot;
        if (record.TenantId != tenant.TenantId || record.Id == Guid.Empty || !FoundationCorrelation.IsValid(record.IdempotencyKey)
            || record.BusinessReady && record.ResultCode != "ready_for_handover"
            || command.ExpectedReconciliationVersion is not { Length: > 0 })
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.InvalidReference, "migration_readiness_invalid");
        await using var db = CreateContext(tenant);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await LockReconciliationAsync(db, tenant, record.ReconciliationId, cancellationToken);
        var parent = await db.Reconciliations.SingleOrDefaultAsync(item => item.Id == record.ReconciliationId && item.RunId == record.RunId, cancellationToken);
        if (parent is null)
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.NotFound, "migration_reconciliation_not_found");
        if (parent.VersionNumber != record.ReconciliationVersion || parent.AttemptId != record.AttemptId || parent.EvidenceFingerprint != record.EvidenceFingerprint)
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_stale");
        var keyed = await db.HandoverReadiness.SingleOrDefaultAsync(item => item.RunId == record.RunId && item.IdempotencyKey == record.IdempotencyKey, cancellationToken);
        if (keyed is not null)
            return SameReadiness(keyed, record)
                ? MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Replay(ToRecord(keyed))
                : MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.Conflict, "migration_readiness_idempotency_conflict");
        if (!parent.Version.AsSpan().SequenceEqual(command.ExpectedReconciliationVersion))
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_version_conflict");
        var latestVersion = await db.Reconciliations.Where(item => item.RunId == record.RunId)
            .Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0;
        if (latestVersion != record.ReconciliationVersion)
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_stale");
        var run = await db.Runs.SingleOrDefaultAsync(item => item.RunId == record.RunId, cancellationToken);
        if (run is not { Status: MigrationRunStatus.Reconciled })
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.Conflict, "migration_reconciliation_blocked");
        var existing = await db.HandoverReadiness.SingleOrDefaultAsync(item => item.ReconciliationId == record.ReconciliationId
            && item.ReconciliationVersion == record.ReconciliationVersion, cancellationToken);
        if (existing is not null)
            return SameReadiness(existing, record)
                ? MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Replay(ToRecord(existing))
                : MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.Conflict, "migration_readiness_conflict");
        db.HandoverReadiness.Add(new MigrationHandoverReadinessEntity(record));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Success(record);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            await using var fresh = CreateContext(tenant);
            var saved = await fresh.HandoverReadiness.SingleOrDefaultAsync(item => item.ReconciliationId == record.ReconciliationId
                && item.ReconciliationVersion == record.ReconciliationVersion, cancellationToken);
            return saved is not null && SameReadiness(saved, record)
                ? MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Replay(ToRecord(saved))
                : MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.Conflict, "migration_readiness_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationHandoverReadinessSnapshot>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_readiness_persistence_unknown");
        }
    }

    public async Task<MigrationPersistenceResult<MigrationReconciliationApprovalRecord>> ConfirmApprovalEvidenceAsync(
        TenantContext tenant, Guid approvalId, CancellationToken cancellationToken = default)
    {
        if (approvalId == Guid.Empty)
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.InvalidReference, "migration_approval_invalid");
        await using var db = CreateContext(tenant);
        var approval = await db.ReconciliationApprovals.SingleOrDefaultAsync(item => item.Id == approvalId, cancellationToken);
        if (approval is null)
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.NotFound, "migration_approval_not_found");
        if (approval.EvidenceConfirmed)
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Replay(ToRecord(approval));
        approval.ConfirmEvidence();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Success(ToRecord(approval));
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationReconciliationApprovalRecord>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_approval_persistence_unknown");
        }
    }

    private async Task<MigrationReconciliationRecord> ReadReconciliationAsync(MigrationDbContext db, MigrationReconciliationEntity entity, CancellationToken cancellationToken)
    {
        var details = await db.ReconciliationDetails.Where(item => item.ReconciliationId == entity.Id).OrderBy(item => item.Domain).ThenBy(item => item.ScopeKey).ToListAsync(cancellationToken);
        var approvals = await db.ReconciliationApprovals.Where(item => item.ReconciliationId == entity.Id)
            .OrderBy(item => item.DecidedAt).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var requirements = await db.ReconciliationRequirements.Where(item => item.ReconciliationId == entity.Id)
            .OrderBy(item => item.Domain).ThenBy(item => item.RequirementKey).ToListAsync(cancellationToken);
        var readiness = await db.HandoverReadiness.SingleOrDefaultAsync(item => item.ReconciliationId == entity.Id, cancellationToken);
        return new(entity.Id, entity.TenantId, entity.RunId, entity.AttemptId, entity.VersionNumber, entity.EvidenceFingerprint,
            entity.IdempotencyKey, entity.Status, entity.CreatedAt, entity.CalculatedAt, entity.SubmittedCount, entity.AcceptedCount, entity.RejectedCount,
            entity.DuplicateCount, entity.SkippedCount, entity.QuarantinedCount, entity.UnresolvedCount, entity.RequiredApprovalCount,
            approvals.Count(item => item.EvidenceConfirmed && item.Decision == MigrationApprovalDecision.Approved), entity.SourceDebit, entity.SourceCredit,
            entity.TargetDebit, entity.TargetCredit, entity.Variance, entity.Version, false, entity.ApprovalPolicyId, entity.ApprovalPolicyVersion,
            entity.ApprovalPolicyCode, entity.ApprovalPolicyEffectiveFrom, entity.ApprovalPolicyEffectiveTo,
            entity.ApprovalEnforcesSeparationOfDuties, requirements.Select(item => item.ToRecord()).ToArray(), details.Select(ToRecord).ToArray(),
            approvals.Select(ToRecord).ToArray(), readiness is null ? null : ToRecord(readiness));
    }

    private static bool SameApproval(MigrationReconciliationApprovalEntity a, MigrationReconciliationApprovalRecord b) =>
        a.RunId == b.RunId && a.ReconciliationId == b.ReconciliationId && a.ReconciliationVersion == b.ReconciliationVersion
        && a.AttemptId == b.AttemptId && a.EvidenceFingerprint == b.EvidenceFingerprint && a.IdempotencyKey == b.IdempotencyKey
        && a.Domain == b.Domain && a.RequirementKey == b.RequirementKey && a.PolicyId == b.PolicyId && a.PolicyVersion == b.PolicyVersion
        && a.ActorId == b.ActorId && a.Decision == b.Decision && a.Reason == b.Reason;

    private static bool SameReadiness(MigrationHandoverReadinessEntity a, MigrationHandoverReadinessSnapshot b) =>
        a.IdempotencyKey == b.IdempotencyKey && a.EvidenceFingerprint == b.EvidenceFingerprint && a.BusinessReady == b.BusinessReady && a.ProductionReady == b.ProductionReady
        && a.Mesp48Complete == b.Mesp48Complete && a.Mesp50Complete == b.Mesp50Complete && a.ResultCode == b.ResultCode;

    private static MigrationReconciliationDetail ToRecord(MigrationReconciliationDetailEntity x) => new(x.Id, x.Domain, x.ScopeKey,
        x.CompanyId, x.OpeningDate, x.CurrencyCode, x.TransactionCurrencyCode, x.FunctionalCurrencyCode, x.SourceCount, x.SourceDebit, x.SourceCredit, x.TargetDebit, x.TargetCredit,
        x.Variance, x.SourceAmount, x.TargetAmount, x.AmountVariance, x.OwnerRoundingDifference, x.TransactionAmount, x.FunctionalAmount, x.SubsidiaryEstablishedAmount, x.GlControlAmount, x.ExchangeRateId, x.ExchangeRateVersionId, x.ExchangeRateVersionNumber, x.AppliedRate, x.SourceQuantity, x.TargetQuantity, x.QuantityVariance,
        x.ControlAccountId, x.PostingRuleId, x.PostingRuleVersionNumber, x.OwnerSourceId, x.WarehouseId, x.ProductId,
        x.UnitOfMeasureId, x.SourceContract, x.SourceEvent, x.RoundingPolicyId, x.RoundingPolicyVersionNumber,
        x.RoundingScale, x.RoundingMode, x.IsBlocking, x.FindingCode, x.Explanation, x.EffectId, x.OwnerReferenceId, x.LinkedAccountId);

    private static MigrationReconciliationApprovalRecord ToRecord(MigrationReconciliationApprovalEntity x) => new(x.Id, x.TenantId,
        x.RunId, x.ReconciliationId, x.ReconciliationVersion, x.AttemptId, x.EvidenceFingerprint, x.IdempotencyKey, x.Domain, x.RequirementKey,
        x.PolicyId, x.PolicyVersion, x.ActorId, x.Decision, x.Reason, x.DecidedAt, x.Version, x.EvidenceConfirmed);

    private static MigrationHandoverReadinessSnapshot ToRecord(MigrationHandoverReadinessEntity x) => new(x.Id, x.TenantId,
        x.RunId, x.ReconciliationId, x.ReconciliationVersion, x.AttemptId, x.EvidenceFingerprint, x.IdempotencyKey, x.CreatedAt, x.BusinessReady,
        x.ProductionReady, x.Mesp48Complete, x.Mesp50Complete, x.TenantActivationPerformed, x.ResultCode, x.Version);

    private static Task<int> LockRunAsync(MigrationDbContext db, TenantContext tenant, Guid runId, CancellationToken cancellationToken) =>
        db.Database.IsSqlServer()
            ? db.Database.ExecuteSqlInterpolatedAsync($"SELECT [RunId] FROM [migration].[MigrationRuns] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenant.TenantId.Value} AND [RunId] = {runId}", cancellationToken)
            : Task.FromResult(0);

    private static Task<int> LockReconciliationAsync(MigrationDbContext db, TenantContext tenant, Guid reconciliationId, CancellationToken cancellationToken) =>
        db.Database.IsSqlServer()
            ? db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [migration].[MigrationReconciliations] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenant.TenantId.Value} AND [Id] = {reconciliationId}", cancellationToken)
            : Task.FromResult(0);
}

#pragma warning restore CS1591
