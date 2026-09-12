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

#pragma warning restore CS1591
