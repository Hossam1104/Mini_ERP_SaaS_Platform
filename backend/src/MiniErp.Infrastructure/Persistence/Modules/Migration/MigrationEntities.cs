#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

/// <summary>Module-owned durable identity for one logical migration run.</summary>
internal sealed class MigrationRunEntity : ITenantOwned
{
    private MigrationRunEntity()
    {
        CorrelationId = string.Empty;
        DefinitionId = string.Empty;
        DefinitionVersion = string.Empty;
        SourceProfileId = string.Empty;
        SourceProfileVersion = string.Empty;
    }

    internal MigrationRunEntity(MigrationRun run)
    {
        RunId = run.RunId;
        TenantId = run.TenantId;
        ActorId = run.ActorId;
        CorrelationId = run.CorrelationId.Value;
        DefinitionId = run.Definition.DefinitionId;
        DefinitionVersion = run.Definition.Version;
        SourceProfileId = run.SourceProfile.ProfileId;
        SourceProfileVersion = run.SourceProfile.ProfileVersion;
        Status = run.Status;
        CreatedAt = run.CreatedAt;
        UpdatedAt = run.UpdatedAt;
    }

    internal Guid RunId { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid ActorId { get; private set; }

    internal string CorrelationId { get; private set; }

    internal string DefinitionId { get; private set; }

    internal string DefinitionVersion { get; private set; }

    internal string SourceProfileId { get; private set; }

    internal string SourceProfileVersion { get; private set; }

    internal MigrationRunStatus Status { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    /// <summary>
    /// Moves the durable run to <paramref name="target"/> only when the domain
    /// transition map permits it from the row's own persisted status.
    /// </summary>
    /// <remarks>
    /// This replaces an earlier method that assigned a caller-supplied status
    /// directly. That let any caller write a lifecycle position the domain
    /// would have refused — for example a post-execution run back to a
    /// pre-commit state — so the durable row could contradict the state
    /// machine. The check is delegated to <see cref="MigrationRun"/> so the map
    /// stays the single authority, and it is evaluated against the persisted
    /// <see cref="Status"/> rather than any value the caller passes in.
    /// </remarks>
    internal void ApplyDomainTransition(MigrationRunStatus target, DateTimeOffset updatedAt)
    {
        if (!MigrationRun.IsTransitionAllowed(Status, target))
        {
            throw new InvalidOperationException(
                "A migration run cannot be moved outside the domain transition map.");
        }

        Status = target;
        UpdatedAt = updatedAt;
        Version = Guid.NewGuid().ToByteArray();
    }
}

/// <summary>Module-owned retry lineage for one logical migration run.</summary>
internal sealed class MigrationAttemptEntity : ITenantOwned
{
    private MigrationAttemptEntity()
    {
        IdempotencyKey = string.Empty;
        RequestFingerprint = string.Empty;
    }

    internal MigrationAttemptEntity(MigrationAttempt attempt)
    {
        AttemptId = attempt.AttemptId;
        TenantId = attempt.TenantId;
        RunId = attempt.RunId;
        Sequence = attempt.Sequence;
        PreviousAttemptId = attempt.PreviousAttemptId;
        Operation = attempt.Operation;
        Outcome = attempt.Outcome;
        IdempotencyKey = attempt.IdempotencyKey.Value;
        RequestFingerprint = attempt.RequestFingerprint.Value;
        StartedAt = attempt.StartedAt;
        FinishedAt = attempt.FinishedAt;
        SafeOutcomeCode = attempt.SafeOutcomeCode;
    }

    internal Guid AttemptId { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid RunId { get; private set; }

    internal int Sequence { get; private set; }

    internal Guid? PreviousAttemptId { get; private set; }

    internal MigrationOperationKind Operation { get; private set; }

    internal MigrationAttemptOutcome Outcome { get; private set; }

    internal string IdempotencyKey { get; private set; }

    internal string RequestFingerprint { get; private set; }

    internal DateTimeOffset StartedAt { get; private set; }

    internal DateTimeOffset? FinishedAt { get; private set; }

    internal string? SafeOutcomeCode { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    /// <summary>
    /// Records the single terminal outcome of a Pending attempt. An attempt
    /// that already carries an outcome is never rewritten, so attempt history
    /// stays append-only evidence (BRD section 16.1, M40-RULE-028).
    /// </summary>
    internal bool TryRecordOutcome(
        MigrationAttemptOutcome outcome,
        string safeOutcomeCode,
        DateTimeOffset finishedAt)
    {
        if (Outcome != MigrationAttemptOutcome.Pending || outcome == MigrationAttemptOutcome.Pending)
        {
            return false;
        }

        Outcome = outcome;
        SafeOutcomeCode = safeOutcomeCode;
        FinishedAt = finishedAt;
        Version = Guid.NewGuid().ToByteArray();
        return true;
    }
}

/// <summary>Module-owned idempotency identity; no request payload is stored.</summary>
internal sealed class MigrationIdempotencyEntity : ITenantOwned
{
    private MigrationIdempotencyEntity()
    {
        IdempotencyKey = string.Empty;
        RequestFingerprint = string.Empty;
        ResultCode = string.Empty;
    }

    internal MigrationIdempotencyEntity(
        TenantId tenantId,
        Guid runId,
        MigrationOperationKind operation,
        MigrationIdempotencyKey idempotencyKey,
        MigrationRequestFingerprint requestFingerprint,
        Guid? attemptId,
        MigrationResultKind resultKind,
        string resultCode,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        RunId = runId;
        Operation = operation;
        IdempotencyKey = idempotencyKey.Value;
        RequestFingerprint = requestFingerprint.Value;
        AttemptId = attemptId;
        ResultKind = resultKind;
        ResultCode = resultCode;
        CreatedAt = createdAt;
    }

    public TenantId TenantId { get; private set; }

    internal Guid RunId { get; private set; }

    internal MigrationOperationKind Operation { get; private set; }

    internal string IdempotencyKey { get; private set; }

    internal string RequestFingerprint { get; private set; }

    internal Guid? AttemptId { get; private set; }

    internal MigrationResultKind ResultKind { get; private set; }

    internal string ResultCode { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

/// <summary>Module-owned immutable source snapshot captured at intake.</summary>
internal sealed class MigrationIntakeEntity : ITenantOwned
{
    private MigrationIntakeEntity()
    {
        IdempotencyKey = string.Empty;
        FingerprintVersion = string.Empty;
        RequestFingerprint = string.Empty;
        SourceSha256 = string.Empty;
    }

    internal MigrationIntakeEntity(
        MigrationRun run,
        MigrationOperationKind operation,
        MigrationIdempotencyKey idempotencyKey,
        MigrationRequestFingerprint requestFingerprint,
        string fingerprintVersion,
        MigrationSourceArtifactSnapshot source)
    {
        RunId = run.RunId;
        TenantId = run.TenantId;
        Operation = operation;
        IdempotencyKey = idempotencyKey.Value;
        FingerprintVersion = fingerprintVersion;
        RequestFingerprint = requestFingerprint.Value;
        SourceObjectId = source.ObjectId;
        SourceTenantId = source.TenantId;
        SourceCompanyId = source.CompanyId;
        SourceBranchId = source.BranchId;
        SourceWarehouseId = source.WarehouseId;
        SourceSha256 = source.Sha256;
        SourceLength = source.Length;
        SourceConcurrencyVersion = source.ConcurrencyVersion;
        CapturedAt = run.CreatedAt;
    }

    internal Guid RunId { get; private set; }

    public TenantId TenantId { get; private set; }

    internal MigrationOperationKind Operation { get; private set; }

    internal string IdempotencyKey { get; private set; }

    internal string FingerprintVersion { get; private set; }

    internal string RequestFingerprint { get; private set; }

    internal Guid SourceObjectId { get; private set; }

    internal TenantId SourceTenantId { get; private set; }

    internal Guid? SourceCompanyId { get; private set; }

    internal Guid? SourceBranchId { get; private set; }

    internal Guid? SourceWarehouseId { get; private set; }

    internal string SourceSha256 { get; private set; }

    internal long SourceLength { get; private set; }

    internal long SourceConcurrencyVersion { get; private set; }

    internal DateTimeOffset CapturedAt { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

internal sealed class MigrationStagedRecordEntity : ITenantOwned
{
    private MigrationStagedRecordEntity() { }

    internal MigrationStagedRecordEntity(MigrationStagedRecord record, string packageHash)
    {
        StagedRecordId = record.StagedRecordId;
        TenantId = record.TenantId;
        RunId = record.RunId;
        SourceSequence = record.SourceSequence;
        SourceRecordId = record.SourceRecordId;
        RecordType = record.RecordType;
        CanonicalPayload = record.CanonicalPayload;
        PayloadHash = record.PayloadHash;
        PackageHash = packageHash;
        PackageVersion = record.PackageVersion;
        SourceObjectId = record.SourceObjectId;
        SourceSnapshotHash = record.SourceSnapshotHash;
        CapturedAt = record.CapturedAt;
    }

    internal Guid StagedRecordId { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal int SourceSequence { get; private set; }
    internal string? SourceRecordId { get; private set; }
    internal MigrationCanonicalRecordType RecordType { get; private set; }
    internal string CanonicalPayload { get; private set; } = string.Empty;
    internal string PayloadHash { get; private set; } = string.Empty;
    internal string PackageHash { get; private set; } = string.Empty;
    internal string PackageVersion { get; private set; } = string.Empty;
    internal Guid SourceObjectId { get; private set; }
    internal string SourceSnapshotHash { get; private set; } = string.Empty;
    internal DateTimeOffset CapturedAt { get; private set; }
}

internal sealed class MigrationValidationResultEntity : ITenantOwned
{
    private MigrationValidationResultEntity() { }

    internal MigrationValidationResultEntity(MigrationValidationSummary summary)
    {
        ValidationResultId = summary.ValidationResultId;
        TenantId = summary.TenantId;
        RunId = summary.RunId;
        AttemptId = summary.AttemptId;
        PackageHash = summary.PackageHash;
        SourceSnapshotHash = summary.SourceSnapshotHash;
        TotalStagedRecords = summary.TotalStagedRecords;
        AcceptedCount = summary.AcceptedCount;
        RejectedCount = summary.RejectedCount;
        QuarantinedCount = summary.QuarantinedCount;
        FindingCountsJson = System.Text.Json.JsonSerializer.Serialize(summary.FindingCounts);
        CompletedAt = summary.CompletedAt;
    }

    internal Guid ValidationResultId { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal string PackageHash { get; private set; } = string.Empty;
    internal string SourceSnapshotHash { get; private set; } = string.Empty;
    internal int TotalStagedRecords { get; private set; }
    internal int AcceptedCount { get; private set; }
    internal int RejectedCount { get; private set; }
    internal int QuarantinedCount { get; private set; }
    internal string FindingCountsJson { get; private set; } = "{}";
    internal DateTimeOffset CompletedAt { get; private set; }
}

internal sealed class MigrationValidationRecordEntity : ITenantOwned
{
    private MigrationValidationRecordEntity() { }

    internal MigrationValidationRecordEntity(MigrationValidationRecordResult result, TenantId tenantId, Guid runId, Guid attemptId)
    {
        TenantId = tenantId;
        RunId = runId;
        AttemptId = attemptId;
        StagedRecordId = result.StagedRecordId;
        SourceSequence = result.SourceSequence;
        RecordType = result.RecordType;
        Disposition = result.Disposition;
        FindingCodesJson = System.Text.Json.JsonSerializer.Serialize(result.FindingCodes);
    }

    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal Guid StagedRecordId { get; private set; }
    internal int SourceSequence { get; private set; }
    internal MigrationCanonicalRecordType RecordType { get; private set; }
    internal MigrationRecordDisposition Disposition { get; private set; }
    internal string FindingCodesJson { get; private set; } = "[]";
}

internal sealed class MigrationValidationFindingEntity : ITenantOwned
{
    private MigrationValidationFindingEntity() { }

    internal MigrationValidationFindingEntity(MigrationValidationFinding finding)
    {
        FindingId = finding.FindingId;
        TenantId = finding.TenantId;
        RunId = finding.RunId;
        AttemptId = finding.AttemptId;
        StagedRecordId = finding.StagedRecordId;
        Category = finding.Category;
        Severity = finding.Severity;
        IsBlocking = finding.IsBlocking;
        Code = finding.Code;
        Message = finding.Message;
        ReferenceId = finding.ReferenceId;
        CreatedAt = finding.CreatedAt;
    }

    internal Guid FindingId { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal Guid? StagedRecordId { get; private set; }
    internal MigrationFindingCategory Category { get; private set; }
    internal MigrationFindingSeverity Severity { get; private set; }
    internal bool IsBlocking { get; private set; }
    internal string Code { get; private set; } = string.Empty;
    internal string Message { get; private set; } = string.Empty;
    internal string? ReferenceId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
}

internal sealed class MigrationDryRunPreviewEntity : ITenantOwned
{
    private MigrationDryRunPreviewEntity() { }

    internal MigrationDryRunPreviewEntity(MigrationDryRunPreview preview)
    {
        PreviewId = preview.PreviewId;
        TenantId = preview.TenantId;
        RunId = preview.RunId;
        AttemptId = preview.AttemptId;
        ValidationAttemptId = preview.ValidationAttemptId;
        PackageHash = preview.PackageHash;
        SourceSnapshotHash = preview.SourceSnapshotHash;
        TotalStagedRecords = preview.TotalStagedRecords;
        AcceptedCount = preview.AcceptedCount;
        RejectedCount = preview.RejectedCount;
        QuarantinedCount = preview.QuarantinedCount;
        FindingCountsJson = System.Text.Json.JsonSerializer.Serialize(preview.FindingCounts);
        ControlTotalsJson = System.Text.Json.JsonSerializer.Serialize(preview.ControlTotals);
        UnresolvedDependencyCount = preview.UnresolvedDependencyCount;
        ExceptionCount = preview.ExceptionCount;
        CompletedAt = preview.CompletedAt;
    }

    internal Guid PreviewId { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid AttemptId { get; private set; }
    internal Guid ValidationAttemptId { get; private set; }
    internal string PackageHash { get; private set; } = string.Empty;
    internal string SourceSnapshotHash { get; private set; } = string.Empty;
    internal int TotalStagedRecords { get; private set; }
    internal int AcceptedCount { get; private set; }
    internal int RejectedCount { get; private set; }
    internal int QuarantinedCount { get; private set; }
    internal string FindingCountsJson { get; private set; } = "{}";
    internal string ControlTotalsJson { get; private set; } = "{}";
    internal int UnresolvedDependencyCount { get; private set; }
    internal int ExceptionCount { get; private set; }
    internal DateTimeOffset CompletedAt { get; private set; }
}

internal sealed class MigrationDryRunPreviewRowEntity : ITenantOwned
{
    private MigrationDryRunPreviewRowEntity() { }

    internal MigrationDryRunPreviewRowEntity(MigrationDryRunPreview preview, MigrationPreviewRow row)
    {
        TenantId = preview.TenantId;
        RunId = preview.RunId;
        PreviewId = preview.PreviewId;
        StagedRecordId = row.StagedRecordId;
        SourceSequence = row.SourceSequence;
        RecordType = row.RecordType;
        Disposition = row.Disposition;
        PlannedAction = row.PlannedAction;
        Projection = row.Projection;
    }

    public TenantId TenantId { get; private set; }
    internal Guid RunId { get; private set; }
    internal Guid PreviewId { get; private set; }
    internal Guid StagedRecordId { get; private set; }
    internal int SourceSequence { get; private set; }
    internal MigrationCanonicalRecordType RecordType { get; private set; }
    internal MigrationRecordDisposition Disposition { get; private set; }
    internal MigrationPlannedAction PlannedAction { get; private set; }
    internal string? Projection { get; private set; }
}

#pragma warning restore CS1591
