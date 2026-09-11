#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

/// <summary>Neutral, versioned migration definition identity.</summary>
public sealed record MigrationDefinitionReference
{
    public MigrationDefinitionReference(string definitionId, string version)
    {
        DefinitionId = Bounded(definitionId, nameof(definitionId));
        Version = Bounded(version, nameof(version));
    }

    public string DefinitionId { get; }

    public string Version { get; }

    private static string Bounded(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A bounded migration identity is required.", name);
        }

        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded migration identity is required.", name);
        }

        return normalized;
    }
}

/// <summary>Neutral source/profile identity for a future adapter.</summary>
public sealed record MigrationSourceProfileReference
{
    public MigrationSourceProfileReference(string profileId, string profileVersion)
    {
        ProfileId = Bounded(profileId, nameof(profileId));
        ProfileVersion = Bounded(profileVersion, nameof(profileVersion));
    }

    public string ProfileId { get; }

    public string ProfileVersion { get; }

    private static string Bounded(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A bounded source/profile identity is required.", name);
        }

        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded source/profile identity is required.", name);
        }

        return normalized;
    }
}

/// <summary>Untrusted request shape used before server Tenant binding.</summary>
public sealed record MigrationRunCreationRequest(
    Guid? RequestedTenantId,
    MigrationDefinitionReference Definition,
    MigrationSourceProfileReference SourceProfile,
    MigrationOperationKind Operation,
    string IdempotencyKey,
    string RequestFingerprint);

/// <summary>Validated, Tenant-qualified idempotency identity.</summary>
public sealed record MigrationIdempotencyKey
{
    public MigrationIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The idempotency key is invalid or unbounded.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
}

/// <summary>Bounded request identity; raw payloads are never retained.</summary>
public sealed record MigrationRequestFingerprint
{
    public MigrationRequestFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A request fingerprint is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The request fingerprint is invalid or unbounded.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
}

/// <summary>Safe result used for expected foundation boundary outcomes.</summary>
public sealed record MigrationOperationResult<T>(
    MigrationResultKind Kind,
    string Code,
    T? Value,
    bool IsSafeToRetry)
{
    public bool Succeeded => Kind is MigrationResultKind.Succeeded or MigrationResultKind.Replayed;

    public static MigrationOperationResult<T> Success(T value, string code = "succeeded") =>
        new(MigrationResultKind.Succeeded, code, value, false);

    public static MigrationOperationResult<T> Replay(T value) =>
        new(MigrationResultKind.Replayed, "idempotent_replay", value, false);

    public static MigrationOperationResult<T> Rejected(string code) =>
        new(MigrationResultKind.Rejected, code, default, false);

    public static MigrationOperationResult<T> Failure(string code, bool safeToRetry = false) =>
        new(MigrationResultKind.KnownFailure, code, default, safeToRetry);

    public static MigrationOperationResult<T> Unknown(string code = "migration_outcome_unknown") =>
        new(MigrationResultKind.UnknownOutcome, code, default, false);
}

/// <summary>Validated state transition result.</summary>
public sealed record MigrationStateTransitionResult(
    bool Allowed,
    MigrationRunStatus From,
    MigrationRunStatus To,
    string Code)
{
    public static MigrationStateTransitionResult Accepted(MigrationRunStatus from, MigrationRunStatus to) =>
        new(true, from, to, "transition_allowed");

    public static MigrationStateTransitionResult Denied(MigrationRunStatus from, MigrationRunStatus to) =>
        new(false, from, to, "invalid_state_transition");
}

/// <summary>One immutable identity-bearing migration run.</summary>
public sealed class MigrationRun : ITenantOwned
{
    private static readonly IReadOnlyDictionary<MigrationRunStatus, IReadOnlySet<MigrationRunStatus>> AllowedTransitions =
        new Dictionary<MigrationRunStatus, IReadOnlySet<MigrationRunStatus>>
        {
            [MigrationRunStatus.Draft] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Prepared, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Prepared] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Validating, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Validating] = new HashSet<MigrationRunStatus> { MigrationRunStatus.ValidationFailed, MigrationRunStatus.Validated, MigrationRunStatus.OutcomeUnknown, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.ValidationFailed] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Corrected, MigrationRunStatus.Prepared, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Validated] = new HashSet<MigrationRunStatus> { MigrationRunStatus.AwaitingApproval, MigrationRunStatus.Approved, MigrationRunStatus.Executing, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.AwaitingApproval] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Approved, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Approved] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Executing, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Executing] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Completed, MigrationRunStatus.Failed, MigrationRunStatus.OutcomeUnknown, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Failed] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Corrected, MigrationRunStatus.Prepared, MigrationRunStatus.OutcomeUnknown, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.OutcomeUnknown] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Corrected, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Corrected] = new HashSet<MigrationRunStatus> { MigrationRunStatus.Prepared, MigrationRunStatus.Cancelled },
            [MigrationRunStatus.Completed] = new HashSet<MigrationRunStatus>(),
            [MigrationRunStatus.Cancelled] = new HashSet<MigrationRunStatus>()
        };

    private MigrationRun(
        Guid runId,
        TenantId tenantId,
        Guid actorId,
        CorrelationId correlationId,
        MigrationDefinitionReference definition,
        MigrationSourceProfileReference sourceProfile,
        DateTimeOffset createdAt)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Migration run identity must not be empty.", nameof(runId));
        }

        if (tenantId == default)
        {
            throw new ArgumentException("Migration runs require a Tenant owner.", nameof(tenantId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Migration runs require a server actor.", nameof(actorId));
        }

        RunId = runId;
        TenantId = tenantId;
        ActorId = actorId;
        CorrelationId = correlationId;
        Definition = definition;
        SourceProfile = sourceProfile;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = MigrationRunStatus.Draft;
    }

    public Guid RunId { get; }

    public TenantId TenantId { get; }

    public Guid ActorId { get; }

    public CorrelationId CorrelationId { get; }

    public MigrationDefinitionReference Definition { get; }

    public MigrationSourceProfileReference SourceProfile { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public MigrationRunStatus Status { get; private set; }

    public bool IsTerminal => Status is MigrationRunStatus.Completed or MigrationRunStatus.Cancelled;

    public static MigrationRun Create(
        TenantContext trustedTenantContext,
        MigrationDefinitionReference definition,
        MigrationSourceProfileReference sourceProfile,
        DateTimeOffset? createdAt = null,
        Guid? runId = null)
    {
        ArgumentNullException.ThrowIfNull(trustedTenantContext);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(sourceProfile);

        if (!trustedTenantContext.ActorId.HasValue || trustedTenantContext.ActorId.Value == Guid.Empty)
        {
            throw new ArgumentException("Migration runs require a server-derived actor.", nameof(trustedTenantContext));
        }

        var correlation = trustedTenantContext.CorrelationId
            ?? throw new ArgumentException("Migration runs require a server-derived correlation identifier.", nameof(trustedTenantContext));

        return new MigrationRun(
            runId ?? Guid.NewGuid(),
            trustedTenantContext.TenantId,
            trustedTenantContext.ActorId.Value,
            correlation,
            definition,
            sourceProfile,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public MigrationStateTransitionResult TryTransition(MigrationRunStatus target, DateTimeOffset? changedAt = null)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        if (!AllowedTransitions[Status].Contains(target))
        {
            return MigrationStateTransitionResult.Denied(Status, target);
        }

        var from = Status;
        Status = target;
        UpdatedAt = changedAt ?? DateTimeOffset.UtcNow;
        return MigrationStateTransitionResult.Accepted(from, target);
    }

    internal static IReadOnlyDictionary<MigrationRunStatus, IReadOnlySet<MigrationRunStatus>> TransitionMap => AllowedTransitions;
}

/// <summary>One explicit attempt in a run's retry lineage.</summary>
public sealed class MigrationAttempt : ITenantOwned
{
    private MigrationAttempt(
        Guid attemptId,
        TenantId tenantId,
        Guid runId,
        int sequence,
        Guid? previousAttemptId,
        MigrationOperationKind operation,
        MigrationIdempotencyKey idempotencyKey,
        MigrationRequestFingerprint requestFingerprint,
        DateTimeOffset startedAt)
    {
        if (attemptId == Guid.Empty || runId == Guid.Empty)
        {
            throw new ArgumentException("Migration attempt and run identities must not be empty.");
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        AttemptId = attemptId;
        TenantId = tenantId;
        RunId = runId;
        Sequence = sequence;
        PreviousAttemptId = previousAttemptId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        StartedAt = startedAt;
        Outcome = MigrationAttemptOutcome.Pending;
    }

    public Guid AttemptId { get; }

    public TenantId TenantId { get; }

    public Guid RunId { get; }

    public int Sequence { get; }

    public Guid? PreviousAttemptId { get; }

    public MigrationOperationKind Operation { get; }

    public MigrationIdempotencyKey IdempotencyKey { get; }

    public MigrationRequestFingerprint RequestFingerprint { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public MigrationAttemptOutcome Outcome { get; private set; }

    public string? SafeOutcomeCode { get; private set; }

    public bool IsSafeToRetry => Outcome == MigrationAttemptOutcome.KnownFailure;

    public static MigrationAttempt Start(
        MigrationRun run,
        MigrationOperationKind operation,
        MigrationIdempotencyKey idempotencyKey,
        MigrationRequestFingerprint requestFingerprint,
        int sequence,
        Guid? previousAttemptId = null,
        DateTimeOffset? startedAt = null,
        Guid? attemptId = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        ArgumentNullException.ThrowIfNull(requestFingerprint);
        if (run.IsTerminal || run.Status == MigrationRunStatus.OutcomeUnknown)
        {
            throw new InvalidOperationException("A terminal or unknown-outcome run cannot start an attempt.");
        }

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        return new MigrationAttempt(
            attemptId ?? Guid.NewGuid(),
            run.TenantId,
            run.RunId,
            sequence,
            previousAttemptId,
            operation,
            idempotencyKey,
            requestFingerprint,
            startedAt ?? DateTimeOffset.UtcNow);
    }

    public MigrationOperationResult<MigrationAttempt> RecordOutcome(
        MigrationAttemptOutcome outcome,
        string safeOutcomeCode,
        DateTimeOffset? finishedAt = null)
    {
        if (!Enum.IsDefined(outcome) || outcome == MigrationAttemptOutcome.Pending)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (Outcome != MigrationAttemptOutcome.Pending)
        {
            return MigrationOperationResult<MigrationAttempt>.Rejected("migration_attempt_already_finished");
        }

        if (string.IsNullOrWhiteSpace(safeOutcomeCode) || safeOutcomeCode.Length > 128 || safeOutcomeCode.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded safe attempt outcome is required.", nameof(safeOutcomeCode));
        }

        Outcome = outcome;
        SafeOutcomeCode = safeOutcomeCode.Trim();
        FinishedAt = finishedAt ?? DateTimeOffset.UtcNow;
        return outcome switch
        {
            MigrationAttemptOutcome.Succeeded => MigrationOperationResult<MigrationAttempt>.Success(this),
            MigrationAttemptOutcome.KnownFailure => MigrationOperationResult<MigrationAttempt>.Failure(SafeOutcomeCode, safeToRetry: true),
            MigrationAttemptOutcome.UnknownOutcome => MigrationOperationResult<MigrationAttempt>.Unknown(),
            MigrationAttemptOutcome.Cancelled => MigrationOperationResult<MigrationAttempt>.Rejected("migration_attempt_cancelled"),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}

/// <summary>Safe migration-specific evidence descriptor over Foundation audit.</summary>
public static class MigrationAuditEvidenceFactory
{
    public static FoundationAuditEvidence Create(
        FoundationRequestContext requestContext,
        MigrationRun run,
        MigrationAttempt attempt,
        string operation,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string outcome,
        string? safeMetadata = null,
        DateTimeOffset? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(attempt);
        if (requestContext.TenantContext is null
            || requestContext.TenantContext.TenantId != run.TenantId
            || attempt.TenantId != run.TenantId
            || attempt.RunId != run.RunId)
        {
            throw new ArgumentException("Migration evidence requires one exact server-owned Tenant and run boundary.");
        }

        var safeOutcome = Bounded(outcome, nameof(outcome));
        var metadata = safeMetadata is null ? null : Bounded(safeMetadata, nameof(safeMetadata));
        var targetReference = $"run={run.RunId:D};attempt={attempt.AttemptId:D}";
        var summary = metadata is null ? $"outcome={safeOutcome}" : $"outcome={safeOutcome};metadata={metadata}";

        return FoundationAuditEvidenceFactory.Create(
            requestContext,
            Bounded(operation, nameof(operation)),
            run.CorrelationId.Value,
            decision,
            reason,
            idempotencyKey: attempt.IdempotencyKey.Value,
            operationVersion: run.Definition.Version,
            attempt: attempt.Sequence,
            occurredAt: occurredAt,
            source: "migration",
            targetType: "migration-run",
            targetReference: targetReference,
            changeSummary: summary);
    }

    private static string Bounded(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Safe evidence metadata is required.", name);
        }

        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Safe evidence metadata is invalid or unbounded.", name);
        }

        return normalized;
    }
}

/// <summary>Persistence record for the migration run foundation.</summary>
public sealed record MigrationRunRecord(
    Guid RunId,
    TenantId TenantId,
    Guid ActorId,
    CorrelationId CorrelationId,
    MigrationDefinitionReference Definition,
    MigrationSourceProfileReference SourceProfile,
    MigrationRunStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);

/// <summary>Persistence record for one explicit retry-lineage attempt.</summary>
public sealed record MigrationAttemptRecord(
    Guid AttemptId,
    TenantId TenantId,
    Guid RunId,
    int Sequence,
    Guid? PreviousAttemptId,
    MigrationOperationKind Operation,
    MigrationAttemptOutcome Outcome,
    string IdempotencyKey,
    string RequestFingerprint,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? SafeOutcomeCode,
    byte[] Version);

/// <summary>Persistence record containing identifiers only, never raw payload.</summary>
public sealed record MigrationIdempotencyRecord(
    Guid TenantId,
    Guid RunId,
    MigrationOperationKind Operation,
    string IdempotencyKey,
    string RequestFingerprint,
    Guid? AttemptId,
    MigrationResultKind ResultKind,
    string ResultCode,
    DateTimeOffset CreatedAt,
    byte[] Version);

/// <summary>Atomic creation contract for a Tenant-owned run and its key.</summary>
public sealed record CreateMigrationRunCommand(
    MigrationRun Run,
    MigrationOperationKind Operation,
    MigrationIdempotencyKey IdempotencyKey,
    MigrationRequestFingerprint RequestFingerprint);

/// <summary>Atomic creation contract for a Tenant-owned attempt and its key.</summary>
public sealed record CreateMigrationAttemptCommand(
    MigrationAttempt Attempt);

public enum MigrationPersistenceOutcome
{
    Succeeded = 1,
    Replayed = 2,
    NotFound = 3,
    Conflict = 4,
    InvalidReference = 5,
    Failure = 6
}

public sealed record MigrationPersistenceResult<T>(
    MigrationPersistenceOutcome Outcome,
    string Code,
    T? Value)
{
    public bool Succeeded => Outcome is MigrationPersistenceOutcome.Succeeded or MigrationPersistenceOutcome.Replayed;

    public static MigrationPersistenceResult<T> Success(T value) => new(MigrationPersistenceOutcome.Succeeded, "persisted", value);

    public static MigrationPersistenceResult<T> Replay(T value) => new(MigrationPersistenceOutcome.Replayed, "idempotent_replay", value);

    public static MigrationPersistenceResult<T> Denied(MigrationPersistenceOutcome outcome, string code) => new(outcome, code, default);
}

/// <summary>
/// Explicit Tenant-bound persistence port. There is no unscoped read method.
/// </summary>
public interface IMigrationFoundationPersistence
{
    Task<MigrationPersistenceResult<MigrationRunRecord>> CreateRunAsync(
        TenantContext tenantContext,
        CreateMigrationRunCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationRunRecord?> FindRunAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<MigrationPersistenceResult<MigrationAttemptRecord>> CreateAttemptAsync(
        TenantContext tenantContext,
        CreateMigrationAttemptCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationAttemptRecord?> FindAttemptAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    Task<MigrationIdempotencyRecord?> FindIdempotencyAsync(
        TenantContext tenantContext,
        MigrationOperationKind operation,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>Application orchestration for Slice 1 only.</summary>
public sealed class MigrationFoundationService
{
    private readonly IMigrationFoundationPersistence persistence;

    public MigrationFoundationService(IMigrationFoundationPersistence persistence)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public async Task<MigrationOperationResult<MigrationRunRecord>> CreateRunAsync(
        TenantContext tenantContext,
        MigrationRunCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestedTenantId.HasValue && request.RequestedTenantId.Value != tenantContext.TenantId.Value)
        {
            return MigrationOperationResult<MigrationRunRecord>.Rejected("migration_tenant_context_mismatch");
        }

        MigrationIdempotencyKey key;
        MigrationRequestFingerprint fingerprint;
        try
        {
            key = new MigrationIdempotencyKey(request.IdempotencyKey);
            fingerprint = new MigrationRequestFingerprint(request.RequestFingerprint);
        }
        catch (ArgumentException)
        {
            return MigrationOperationResult<MigrationRunRecord>.Rejected("migration_idempotency_shape_invalid");
        }

        var run = MigrationRun.Create(tenantContext, request.Definition, request.SourceProfile);
        var saved = await persistence.CreateRunAsync(
            tenantContext,
            new CreateMigrationRunCommand(run, request.Operation, key, fingerprint),
            cancellationToken);
        return saved.Outcome switch
        {
            MigrationPersistenceOutcome.Succeeded => MigrationOperationResult<MigrationRunRecord>.Success(saved.Value!),
            MigrationPersistenceOutcome.Replayed => MigrationOperationResult<MigrationRunRecord>.Replay(saved.Value!),
            MigrationPersistenceOutcome.Conflict => MigrationOperationResult<MigrationRunRecord>.Rejected(saved.Code),
            _ => MigrationOperationResult<MigrationRunRecord>.Failure(saved.Code)
        };
    }

    public async Task<MigrationOperationResult<MigrationRunRecord>> FindRunAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (runId == Guid.Empty)
        {
            return MigrationOperationResult<MigrationRunRecord>.Rejected("migration_run_id_invalid");
        }

        var run = await persistence.FindRunAsync(tenantContext, runId, cancellationToken);
        return run is null
            ? MigrationOperationResult<MigrationRunRecord>.Rejected("migration_run_not_found")
            : MigrationOperationResult<MigrationRunRecord>.Success(run);
    }
}

#pragma warning restore CS1591
