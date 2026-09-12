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
    // Authoritative source: MESP-40 BRD section 7.2 lifecycle, WF-03/WF-04/WF-05
    // entry and failure paths, sections 13.7/13.8, M40-REQ-029, M40-RULE-013,
    // M40-RULE-019, M40-RULE-028, M40-AC-018 through M40-AC-022, M40-AC-033 and
    // M40-AC-036. The graph encodes these safety rules:
    //
    //  * Executing is reachable ONLY from Approved. WF-03 entry criteria require
    //    an approved run before execution, so Validated must not jump the
    //    approval gate.
    //  * "Cancelled is a terminal pre-commit outcome" (section 7.2), so no state
    //    at or after the effect boundary may reach Cancelled. Claiming
    //    cancellation after execution started would assert "no authoritative
    //    business effect" for a run that may already have caused one
    //    (M40-AC-033).
    //  * OutcomeUnknown is a reconciliation-required outcome that "requires
    //    reconciliation and is not automatically replayed" (section 13.7) and
    //    where "automatic replay stops and reconciliation is required"
    //    (M40-AC-022). Its only exit is therefore ReconciliationPending; it can
    //    reach neither Corrected nor Cancelled directly.
    //  * OutcomeUnknown is defined as an outcome "after an effect boundary when
    //    the result cannot be proved" (section 7.2). Validation has no
    //    authoritative business effect (M40-RULE-017, M40-AC-014), so Validating
    //    does not route to OutcomeUnknown; an unprovable validation is a
    //    ValidationFailed outcome that is corrected and re-validated, which
    //    loses no capability because validation crossed no effect boundary.
    //  * Execution outcomes route through reconciliation before handover
    //    (section 7.2, M40-RULE-019, M40-AC-036), so Completed is not terminal.
    //  * Correction never cancels: Corrected leads only to Prepared. A run that
    //    should be abandoned pre-commit is cancelled from Prepared, so no
    //    legitimate cancellation is lost while a post-effect run can never
    //    launder itself into a pre-commit cancellation through Corrected.
    private static readonly IReadOnlyDictionary<MigrationRunStatus, IReadOnlySet<MigrationRunStatus>> AllowedTransitions =
        new Dictionary<MigrationRunStatus, IReadOnlySet<MigrationRunStatus>>
        {
            [MigrationRunStatus.Draft] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Prepared,
                MigrationRunStatus.Cancelled
            },
            [MigrationRunStatus.Prepared] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Validating,
                MigrationRunStatus.Cancelled
            },
            [MigrationRunStatus.Validating] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.ValidationFailed,
                MigrationRunStatus.Validated,
                MigrationRunStatus.Cancelled
            },
            [MigrationRunStatus.ValidationFailed] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Corrected,
                MigrationRunStatus.Prepared,
                MigrationRunStatus.Cancelled
            },
            [MigrationRunStatus.Validated] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.AwaitingApproval,
                MigrationRunStatus.Approved,
                MigrationRunStatus.Cancelled
            },
            [MigrationRunStatus.AwaitingApproval] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Approved,
                MigrationRunStatus.Cancelled
            },
            [MigrationRunStatus.Approved] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Executing,
                MigrationRunStatus.Cancelled
            },
            [MigrationRunStatus.Executing] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Completed,
                MigrationRunStatus.PartiallyCompleted,
                MigrationRunStatus.Failed,
                MigrationRunStatus.OutcomeUnknown
            },
            [MigrationRunStatus.Completed] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.ReconciliationPending
            },
            [MigrationRunStatus.PartiallyCompleted] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.ReconciliationPending
            },
            [MigrationRunStatus.Failed] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.ReconciliationPending
            },
            [MigrationRunStatus.OutcomeUnknown] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.ReconciliationPending
            },
            [MigrationRunStatus.ReconciliationPending] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Reconciled
            },
            [MigrationRunStatus.Reconciled] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.ReadyForHandover
            },
            [MigrationRunStatus.ReadyForHandover] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Closed
            },
            [MigrationRunStatus.Corrected] = new HashSet<MigrationRunStatus>
            {
                MigrationRunStatus.Prepared
            },
            [MigrationRunStatus.Cancelled] = new HashSet<MigrationRunStatus>(),
            [MigrationRunStatus.Closed] = new HashSet<MigrationRunStatus>()
        };

    /// <summary>
    /// States at or after the execution effect boundary. A run in one of these
    /// states may already have caused an authoritative business effect, so it
    /// can never be cancelled (BRD section 7.2, M40-AC-033).
    /// </summary>
    private static readonly IReadOnlySet<MigrationRunStatus> EffectBoundaryStatesCore =
        new HashSet<MigrationRunStatus>
        {
            MigrationRunStatus.Executing,
            MigrationRunStatus.Completed,
            MigrationRunStatus.PartiallyCompleted,
            MigrationRunStatus.Failed,
            MigrationRunStatus.OutcomeUnknown,
            MigrationRunStatus.ReconciliationPending,
            MigrationRunStatus.Reconciled,
            MigrationRunStatus.ReadyForHandover,
            MigrationRunStatus.Closed
        };

    /// <summary>
    /// Reconciliation-required states. Automatic replay stops here and an
    /// attempt can never be started from them (BRD section 13.7, M40-AC-022).
    /// </summary>
    private static readonly IReadOnlySet<MigrationRunStatus> ReconciliationRequiredStatesCore =
        new HashSet<MigrationRunStatus>
        {
            MigrationRunStatus.OutcomeUnknown,
            MigrationRunStatus.ReconciliationPending
        };

    private static readonly IReadOnlySet<MigrationRunStatus> TerminalStatesCore =
        new HashSet<MigrationRunStatus>
        {
            MigrationRunStatus.Cancelled,
            MigrationRunStatus.Closed
        };

    private MigrationRun(
        Guid runId,
        TenantId tenantId,
        Guid actorId,
        CorrelationId correlationId,
        MigrationDefinitionReference definition,
        MigrationSourceProfileReference sourceProfile,
        MigrationRunStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
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

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        RunId = runId;
        TenantId = tenantId;
        ActorId = actorId;
        CorrelationId = correlationId;
        Definition = definition;
        SourceProfile = sourceProfile;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Status = status;
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

    /// <summary>
    /// True only for the two genuinely terminal states. Completed is NOT
    /// terminal: BRD section 7.2 routes every execution outcome through
    /// reconciliation and handover before Closed.
    /// </summary>
    public bool IsTerminal => TerminalStatesCore.Contains(Status);

    /// <summary>
    /// True when the run may already have crossed the execution effect
    /// boundary and therefore can never report a pre-commit cancellation.
    /// </summary>
    public bool HasReachedEffectBoundary => EffectBoundaryStatesCore.Contains(Status);

    /// <summary>
    /// True when the run requires reconciliation before any further attempt.
    /// </summary>
    public bool RequiresReconciliation => ReconciliationRequiredStatesCore.Contains(Status);

    public static IReadOnlySet<MigrationRunStatus> EffectBoundaryStates => EffectBoundaryStatesCore;

    public static IReadOnlySet<MigrationRunStatus> ReconciliationRequiredStates => ReconciliationRequiredStatesCore;

    public static IReadOnlySet<MigrationRunStatus> TerminalStates => TerminalStatesCore;

    /// <summary>
    /// Creates one new Draft run. Run identity and creation time are
    /// server-derived only: a caller cannot supply, predict or reuse either.
    /// </summary>
    public static MigrationRun Create(
        TenantContext trustedTenantContext,
        MigrationDefinitionReference definition,
        MigrationSourceProfileReference sourceProfile,
        TimeProvider? timeProvider = null)
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

        // The correlation travels into every evidence record for this run, so it
        // is validated here against the same Foundation bound the audit sink
        // enforces. Rejecting it at creation fails loudly; accepting it would
        // silently degrade every later evidence append for the run instead.
        if (!FoundationCorrelation.IsValid(correlation.Value))
        {
            throw new ArgumentException(
                "Migration runs require a correlation identifier within the Foundation bound.",
                nameof(trustedTenantContext));
        }

        var createdAt = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new MigrationRun(
            Guid.NewGuid(),
            trustedTenantContext.TenantId,
            trustedTenantContext.ActorId.Value,
            correlation,
            definition,
            sourceProfile,
            MigrationRunStatus.Draft,
            createdAt,
            createdAt);
    }

    /// <summary>
    /// Rebuilds the authoritative run from its persisted record so a transition
    /// is always evaluated against durable state rather than a caller-supplied
    /// object. This is the only supported way to obtain a run that is not new.
    /// </summary>
    public static MigrationRun Rehydrate(MigrationRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new MigrationRun(
            record.RunId,
            record.TenantId,
            record.ActorId,
            record.CorrelationId,
            record.Definition,
            record.SourceProfile,
            record.Status,
            record.CreatedAt,
            record.UpdatedAt);
    }

    /// <summary>
    /// Whether the persisted run state permits starting an attempt for this
    /// operation. Derived from the BRD section 7.2 ordering and the WF-03
    /// requirement that execution follows approval. Every unlisted state fails
    /// closed.
    /// </summary>
    public bool PermitsAttempt(MigrationOperationKind operation)
    {
        if (!Enum.IsDefined(operation) || IsTerminal || RequiresReconciliation)
        {
            return false;
        }

        return operation switch
        {
            // Validation runs while the run is prepared or already validating.
            MigrationOperationKind.Validation =>
                Status is MigrationRunStatus.Prepared or MigrationRunStatus.Validating,

            // Preview/dry run follows a passed validation and precedes approval
            // (BRD section 7.2). It has no authoritative effect (M40-RULE-017).
            MigrationOperationKind.DryRun =>
                Status is MigrationRunStatus.Validated or MigrationRunStatus.AwaitingApproval,

            // Execution requires an approved run (WF-03 entry criteria). A
            // linked retry inside the same batch is permitted while Executing;
            // the attempt lineage separately refuses to supersede an open or
            // unprovable prior attempt (M40-RULE-013, M40-AC-022).
            MigrationOperationKind.Execution =>
                Status is MigrationRunStatus.Approved or MigrationRunStatus.Executing,

            _ => false
        };
    }

    public MigrationStateTransitionResult TryTransition(
        MigrationRunStatus target,
        TimeProvider? timeProvider = null)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        if (!IsTransitionAllowed(Status, target))
        {
            return MigrationStateTransitionResult.Denied(Status, target);
        }

        var from = Status;
        Status = target;
        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return MigrationStateTransitionResult.Accepted(from, target);
    }

    /// <summary>
    /// The single authority for whether one lifecycle move is permitted.
    /// Persistence consults this so a durable row can never hold a position
    /// the state machine would have refused, and every unmapped state fails
    /// closed.
    /// </summary>
    public static bool IsTransitionAllowed(MigrationRunStatus from, MigrationRunStatus to) =>
        Enum.IsDefined(from)
        && Enum.IsDefined(to)
        && AllowedTransitions.TryGetValue(from, out var permitted)
        && permitted.Contains(to);

    internal static IReadOnlyDictionary<MigrationRunStatus, IReadOnlySet<MigrationRunStatus>> TransitionMap => AllowedTransitions;
}

/// <summary>
/// Server-derived attempt lineage. Sequence and previous-attempt identity are
/// computed from the persisted attempts of one run inside the persistence
/// transaction; a caller cannot supply, skip or reuse either value
/// (M40-REQ-029, M40-RULE-013).
/// </summary>
public sealed class MigrationAttemptLineage
{
    private MigrationAttemptLineage(
        TenantId tenantId,
        Guid runId,
        int nextSequence,
        Guid? previousAttemptId,
        string? blockingCode)
    {
        TenantId = tenantId;
        RunId = runId;
        NextSequence = nextSequence;
        PreviousAttemptId = previousAttemptId;
        BlockingCode = blockingCode;
    }

    public TenantId TenantId { get; }

    public Guid RunId { get; }

    public int NextSequence { get; }

    public Guid? PreviousAttemptId { get; }

    /// <summary>Safe allow-listed code when a next attempt is not permitted.</summary>
    public string? BlockingCode { get; }

    public bool CanStartNext => BlockingCode is null;

    /// <summary>
    /// Derives the next lineage position from the persisted attempts of exactly
    /// one Tenant-owned run.
    /// </summary>
    public static MigrationAttemptLineage FromPersistedAttempts(
        TenantId tenantId,
        Guid runId,
        IReadOnlyCollection<MigrationAttemptRecord> persistedAttempts)
    {
        ArgumentNullException.ThrowIfNull(persistedAttempts);
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Attempt lineage requires a run identity.", nameof(runId));
        }

        if (persistedAttempts.Any(item => item.TenantId != tenantId || item.RunId != runId))
        {
            // A foreign row in the lineage input means the read was not scoped
            // to exactly one Tenant-owned run. Fail closed rather than derive a
            // sequence from another Tenant's history.
            return new MigrationAttemptLineage(
                tenantId,
                runId,
                nextSequence: 0,
                previousAttemptId: null,
                blockingCode: "migration_attempt_lineage_foreign_row");
        }

        if (persistedAttempts.Count == 0)
        {
            return new MigrationAttemptLineage(tenantId, runId, nextSequence: 1, previousAttemptId: null, blockingCode: null);
        }

        if (persistedAttempts.Select(item => item.Sequence).Distinct().Count() != persistedAttempts.Count)
        {
            return new MigrationAttemptLineage(
                tenantId,
                runId,
                nextSequence: 0,
                previousAttemptId: null,
                blockingCode: "migration_attempt_lineage_duplicate_sequence");
        }

        var latest = persistedAttempts.OrderByDescending(item => item.Sequence).First();

        // A prior attempt that is still Pending, or whose outcome could not be
        // proved, must not be superseded by a second authoritative attempt.
        // BRD section 13.7 and M40-AC-022 stop automatic replay and require
        // reconciliation instead.
        var blocking = latest.Outcome switch
        {
            MigrationAttemptOutcome.Pending => "migration_attempt_previous_still_open",
            MigrationAttemptOutcome.UnknownOutcome => "migration_attempt_previous_outcome_unknown",
            _ => null
        };

        return new MigrationAttemptLineage(
            tenantId,
            runId,
            nextSequence: latest.Sequence + 1,
            previousAttemptId: latest.AttemptId,
            blockingCode: blocking);
    }
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
        MigrationAttemptOutcome outcome,
        DateTimeOffset startedAt,
        DateTimeOffset? finishedAt,
        string? safeOutcomeCode)
    {
        if (attemptId == Guid.Empty || runId == Guid.Empty)
        {
            throw new ArgumentException("Migration attempt and run identities must not be empty.");
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        if (previousAttemptId == attemptId)
        {
            throw new ArgumentException("A migration attempt cannot be its own predecessor.", nameof(previousAttemptId));
        }

        if (sequence == 1 && previousAttemptId is not null)
        {
            throw new ArgumentException("The first attempt in a run has no predecessor.", nameof(previousAttemptId));
        }

        if (sequence > 1 && previousAttemptId is null)
        {
            throw new ArgumentException("A later attempt must link to its predecessor.", nameof(previousAttemptId));
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
        FinishedAt = finishedAt;
        Outcome = outcome;
        SafeOutcomeCode = safeOutcomeCode;
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

    /// <summary>
    /// Starts the next attempt of a run using server-derived lineage. There is
    /// deliberately no parameter for sequence, predecessor, attempt identity or
    /// start time: all four are server-owned so a caller cannot forge lineage,
    /// replay an identity or backdate an attempt.
    /// </summary>
    public static MigrationOperationResult<MigrationAttempt> StartNext(
        MigrationRun run,
        MigrationOperationKind operation,
        MigrationIdempotencyKey idempotencyKey,
        MigrationRequestFingerprint requestFingerprint,
        MigrationAttemptLineage serverLineage,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        ArgumentNullException.ThrowIfNull(requestFingerprint);
        ArgumentNullException.ThrowIfNull(serverLineage);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (serverLineage.RunId != run.RunId || serverLineage.TenantId != run.TenantId)
        {
            return MigrationOperationResult<MigrationAttempt>.Rejected("migration_attempt_lineage_run_mismatch");
        }

        if (run.IsTerminal)
        {
            return MigrationOperationResult<MigrationAttempt>.Rejected("migration_run_terminal");
        }

        if (run.RequiresReconciliation)
        {
            return MigrationOperationResult<MigrationAttempt>.Rejected("migration_run_requires_reconciliation");
        }

        if (!run.PermitsAttempt(operation))
        {
            return MigrationOperationResult<MigrationAttempt>.Rejected("migration_operation_not_permitted_by_run_state");
        }

        if (!serverLineage.CanStartNext)
        {
            return MigrationOperationResult<MigrationAttempt>.Rejected(serverLineage.BlockingCode!);
        }

        var attempt = new MigrationAttempt(
            Guid.NewGuid(),
            run.TenantId,
            run.RunId,
            serverLineage.NextSequence,
            serverLineage.PreviousAttemptId,
            operation,
            idempotencyKey,
            requestFingerprint,
            MigrationAttemptOutcome.Pending,
            (timeProvider ?? TimeProvider.System).GetUtcNow(),
            finishedAt: null,
            safeOutcomeCode: null);
        return MigrationOperationResult<MigrationAttempt>.Success(attempt, "attempt_started");
    }

    /// <summary>
    /// Rebuilds an attempt from its persisted record so an outcome is always
    /// recorded against durable state.
    /// </summary>
    public static MigrationAttempt Rehydrate(MigrationAttemptRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new MigrationAttempt(
            record.AttemptId,
            record.TenantId,
            record.RunId,
            record.Sequence,
            record.PreviousAttemptId,
            record.Operation,
            new MigrationIdempotencyKey(record.IdempotencyKey),
            new MigrationRequestFingerprint(record.RequestFingerprint),
            record.Outcome,
            record.StartedAt,
            record.FinishedAt,
            record.SafeOutcomeCode);
    }

    public MigrationOperationResult<MigrationAttempt> RecordOutcome(
        MigrationAttemptOutcome outcome,
        string safeOutcomeCode,
        TimeProvider? timeProvider = null)
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
        FinishedAt = (timeProvider ?? TimeProvider.System).GetUtcNow();
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

/// <summary>
/// Allow-listed safe metadata for migration evidence. Free text is refused: an
/// unbounded operator-supplied string in business evidence is how sensitive
/// source data leaks into audit (BRD section 16, M40-RULE-029).
/// </summary>
public sealed class MigrationAuditMetadata
{
    /// <summary>
    /// The only keys migration evidence may carry. Each describes a
    /// server-derived lifecycle fact, never source data.
    /// </summary>
    private static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "from",
        "to",
        "operation",
        "sequence",
        "result",
        "replayed"
    };

    /// <summary>
    /// Longest allowed metadata value. It accommodates the longest lifecycle
    /// name in the vocabulary (<c>ReconciliationPending</c>, 21 characters)
    /// with headroom, and is short enough that the rendered metadata stays well
    /// inside the Foundation evidence bound.
    /// </summary>
    internal const int MaximumValueLength = 24;

    private readonly SortedDictionary<string, string> values = new(StringComparer.Ordinal);

    public static MigrationAuditMetadata Create() => new();

    public static IReadOnlySet<string> AllowedMetadataKeys => AllowedKeys;

    public MigrationAuditMetadata With(string key, string value)
    {
        if (!AllowedKeys.Contains(key))
        {
            throw new ArgumentException("Migration evidence metadata key is not allow-listed.", nameof(key));
        }

        values[key] = SafeValue(value);
        return this;
    }

    public MigrationAuditMetadata With(string key, int value) =>
        With(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public bool IsEmpty => values.Count == 0;

    /// <summary>Renders deterministic <c>key=value;key=value</c> text.</summary>
    public string Render() => string.Join(';', values.Select(pair => $"{pair.Key}={pair.Value}"));

    private static string SafeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Migration evidence metadata value is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > MaximumValueLength
            || normalized.Any(char.IsControl)
            || normalized.Any(character => character is ';' or '='))
        {
            throw new ArgumentException("Migration evidence metadata value is invalid or unbounded.", nameof(value));
        }

        return normalized;
    }
}

/// <summary>Safe migration-specific evidence descriptor over Foundation audit.</summary>
public static class MigrationAuditEvidenceFactory
{
    /// <summary>
    /// Builds one Foundation evidence record for a migration event.
    /// </summary>
    /// <remarks>
    /// <paramref name="attempt"/> is optional. Run creation and run-level state
    /// transitions are mandatory BRD section 16.1 events that belong to the run
    /// and have no owning attempt; requiring an attempt here would make those
    /// events inexpressible, which is exactly the gap this factory previously
    /// had.
    /// </remarks>
    public static FoundationAuditEvidence Create(
        FoundationRequestContext requestContext,
        MigrationRun run,
        string operation,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string outcome,
        MigrationAttempt? attempt = null,
        MigrationIdempotencyKey? idempotencyKey = null,
        MigrationAuditMetadata? safeMetadata = null,
        DateTimeOffset? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(run);
        if (requestContext.TenantContext is null
            || requestContext.TenantContext.TenantId != run.TenantId)
        {
            throw new ArgumentException("Migration evidence requires one exact server-owned Tenant boundary.");
        }

        if (attempt is not null
            && (attempt.TenantId != run.TenantId || attempt.RunId != run.RunId))
        {
            throw new ArgumentException("Migration evidence requires one exact server-owned run boundary.");
        }

        var safeOutcome = BoundedOutcome(outcome);
        var targetReference = attempt is null
            ? $"run={run.RunId:D}"
            : $"run={run.RunId:D};attempt={attempt.AttemptId:D}";
        var summary = ComposeSummary(safeOutcome, safeMetadata);
        var evidenceIdempotencyKey = attempt?.IdempotencyKey ?? idempotencyKey;

        return FoundationAuditEvidenceFactory.Create(
            requestContext,
            Bounded(operation, nameof(operation)),
            run.CorrelationId.Value,
            decision,
            reason,
            idempotencyKey: evidenceIdempotencyKey?.Value,
            operationVersion: run.Definition.Version,
            attempt: attempt?.Sequence ?? 1,
            occurredAt: occurredAt,
            source: "migration",
            targetType: "migration-run",
            targetReference: targetReference,
            changeSummary: summary);
    }

    /// <summary>
    /// Projects appended evidence into the safe Migration evidence contract.
    /// </summary>
    public static MigrationEvidenceContract ToContract(
        FoundationAuditEvidence evidence,
        MigrationRun run,
        MigrationAttempt? attempt,
        string outcome)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(run);
        return new MigrationEvidenceContract(
            evidence.EvidenceId,
            run.TenantId.Value,
            run.RunId,
            attempt?.AttemptId,
            evidence.ActorId,
            evidence.CorrelationId,
            evidence.OperationId,
            run.Status.ToString(),
            Bounded(outcome, nameof(outcome)),
            evidence.OccurredAt,
            evidence.ChangeSummary);
    }

    /// <summary>
    /// Maximum length of the evidence change summary, matching the Foundation
    /// evidence bound the audit sink enforces.
    /// </summary>
    private const int MaximumSummaryLength = 128;

    private const string OutcomePrefix = "outcome=";

    /// <summary>
    /// Composes the change summary within the Foundation bound. The outcome is
    /// the essential fact and is never truncated; the allow-listed metadata is
    /// supplementary, so on the pathological combination it is dropped rather
    /// than producing an over-long summary the sink would refuse. Refusing it
    /// would turn a completed effect into an unprovable one purely because its
    /// description was long, so the summary composition is deterministic and
    /// can never be the reason an append fails.
    /// </summary>
    private static string ComposeSummary(string safeOutcome, MigrationAuditMetadata? safeMetadata)
    {
        var outcomePart = OutcomePrefix + safeOutcome;
        if (safeMetadata is null || safeMetadata.IsEmpty)
        {
            return outcomePart;
        }

        var composed = $"{outcomePart};{safeMetadata.Render()}";
        return composed.Length <= MaximumSummaryLength ? composed : outcomePart;
    }

    /// <summary>
    /// Bounds the outcome code so that <c>outcome=</c> plus the code always
    /// fits inside the Foundation evidence bound.
    /// </summary>
    private static string BoundedOutcome(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A safe evidence outcome is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > MaximumSummaryLength - OutcomePrefix.Length
            || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The safe evidence outcome is invalid or unbounded.", nameof(value));
        }

        return normalized;
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

/// <summary>
/// Request to start the next attempt of an existing run. It carries no
/// sequence, predecessor, attempt identity or start time: persistence derives
/// all four from the durable lineage inside its transaction.
/// </summary>
public sealed record StartMigrationAttemptCommand(
    Guid RunId,
    MigrationOperationKind Operation,
    MigrationIdempotencyKey IdempotencyKey,
    MigrationRequestFingerprint RequestFingerprint);

/// <summary>
/// Applies one run state transition through the domain transition map under
/// optimistic concurrency.
/// </summary>
public sealed record ApplyMigrationRunTransitionCommand(
    Guid RunId,
    MigrationRunStatus Target,
    byte[] ExpectedVersion);

/// <summary>
/// Records the single terminal outcome of one Pending attempt under optimistic
/// concurrency.
/// </summary>
public sealed record RecordMigrationAttemptOutcomeCommand(
    Guid RunId,
    Guid AttemptId,
    MigrationAttemptOutcome Outcome,
    string SafeOutcomeCode,
    byte[] ExpectedVersion);

public enum MigrationPersistenceOutcome
{
    Succeeded = 1,
    Replayed = 2,
    NotFound = 3,
    Conflict = 4,
    InvalidReference = 5,
    Failure = 6,

    /// <summary>
    /// The write may or may not have taken effect and the result could not be
    /// proved. It is never reported as a business conflict and is never
    /// automatically replayed (BRD section 13.7, M40-AC-022).
    /// </summary>
    UnknownOutcome = 7
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

    /// <summary>
    /// Starts the next attempt of a run. Persistence reads the durable run
    /// state and attempt lineage, derives sequence and predecessor, and refuses
    /// a request the persisted state does not permit.
    /// </summary>
    Task<MigrationPersistenceResult<MigrationAttemptRecord>> StartAttemptAsync(
        TenantContext tenantContext,
        StartMigrationAttemptCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationAttemptRecord?> FindAttemptAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the attempt lineage of exactly one Tenant-owned run in sequence
    /// order.
    /// </summary>
    Task<IReadOnlyList<MigrationAttemptRecord>> ListAttemptsAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies one run transition through the domain transition map under
    /// optimistic concurrency. An invalid transition is refused by the domain
    /// map, never written directly.
    /// </summary>
    Task<MigrationPersistenceResult<MigrationRunRecord>> ApplyRunTransitionAsync(
        TenantContext tenantContext,
        ApplyMigrationRunTransitionCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the single terminal outcome of one Pending attempt under
    /// optimistic concurrency.
    /// </summary>
    Task<MigrationPersistenceResult<MigrationAttemptRecord>> RecordAttemptOutcomeAsync(
        TenantContext tenantContext,
        RecordMigrationAttemptOutcomeCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationIdempotencyRecord?> FindIdempotencyAsync(
        TenantContext tenantContext,
        MigrationOperationKind operation,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Application orchestration for Slice 1 only.
/// </summary>
/// <remarks>
/// Every state-changing operation appends exactly one durable business
/// evidence record through the existing Foundation audit sink. Migration does
/// not own a second audit subsystem, and evidence is business evidence, not a
/// log line (BRD section 16, M40-RULE-028).
/// <para>
/// Evidence failure semantics are deliberate. Evidence is appended after the
/// persisted outcome is known, so the record always states what actually
/// happened. If the append then fails and an effect had occurred, the operation
/// returns <see cref="MigrationResultKind.UnknownOutcome"/>: an effect that
/// cannot be evidenced is exactly the BRD section 13.7 condition where the
/// result cannot be proved, so it requires reconciliation and is never
/// automatically replayed. If no effect occurred, the append failure is a
/// retry-safe known failure instead.
/// </para>
/// </remarks>
public sealed class MigrationFoundationService
{
    internal const string CreateRunOperationId = "migration.run.create";
    internal const string StartAttemptOperationId = "migration.attempt.start";
    internal const string TransitionOperationId = "migration.run.transition";
    internal const string AttemptOutcomeOperationId = "migration.attempt.outcome";
    internal const string EvidenceUnavailableCode = "migration_audit_evidence_unavailable";

    private readonly IMigrationFoundationPersistence persistence;
    private readonly IFoundationAuditEvidenceSink auditSink;
    private readonly TimeProvider timeProvider;

    public MigrationFoundationService(
        IMigrationFoundationPersistence persistence,
        IFoundationAuditEvidenceSink auditSink,
        TimeProvider? timeProvider = null)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MigrationOperationResult<MigrationRunRecord>> CreateRunAsync(
        FoundationRequestContext requestContext,
        MigrationRunCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(request);
        if (requestContext.TenantContext is not { } tenantContext)
        {
            return MigrationOperationResult<MigrationRunRecord>.Rejected("migration_tenant_context_required");
        }

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

        var run = MigrationRun.Create(tenantContext, request.Definition, request.SourceProfile, timeProvider);
        var saved = await persistence.CreateRunAsync(
            tenantContext,
            new CreateMigrationRunCommand(run, request.Operation, key, fingerprint),
            cancellationToken);

        var persistedRun = saved.Value is null ? run : MigrationRun.Rehydrate(saved.Value);
        var metadata = MigrationAuditMetadata.Create()
            .With("operation", request.Operation.ToString())
            .With("to", persistedRun.Status.ToString());
        var evidence = await AppendAsync(
            requestContext,
            persistedRun,
            CreateRunOperationId,
            saved,
            attempt: null,
            idempotencyKey: key,
            metadata,
            cancellationToken);
        if (!evidence)
        {
            return EvidenceFailure<MigrationRunRecord>(saved);
        }

        return saved.Outcome switch
        {
            MigrationPersistenceOutcome.Succeeded => MigrationOperationResult<MigrationRunRecord>.Success(saved.Value!),
            MigrationPersistenceOutcome.Replayed => MigrationOperationResult<MigrationRunRecord>.Replay(saved.Value!),
            MigrationPersistenceOutcome.Conflict => MigrationOperationResult<MigrationRunRecord>.Rejected(saved.Code),
            MigrationPersistenceOutcome.InvalidReference => MigrationOperationResult<MigrationRunRecord>.Rejected(saved.Code),
            MigrationPersistenceOutcome.UnknownOutcome => MigrationOperationResult<MigrationRunRecord>.Unknown(saved.Code),
            _ => MigrationOperationResult<MigrationRunRecord>.Failure(saved.Code)
        };
    }

    public async Task<MigrationOperationResult<MigrationAttemptRecord>> StartAttemptAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        MigrationOperationKind operation,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        if (requestContext.TenantContext is not { } tenantContext)
        {
            return MigrationOperationResult<MigrationAttemptRecord>.Rejected("migration_tenant_context_required");
        }

        if (runId == Guid.Empty)
        {
            return MigrationOperationResult<MigrationAttemptRecord>.Rejected("migration_run_id_invalid");
        }

        MigrationIdempotencyKey key;
        MigrationRequestFingerprint fingerprint;
        try
        {
            key = new MigrationIdempotencyKey(idempotencyKey);
            fingerprint = new MigrationRequestFingerprint(requestFingerprint);
        }
        catch (ArgumentException)
        {
            return MigrationOperationResult<MigrationAttemptRecord>.Rejected("migration_idempotency_shape_invalid");
        }

        var saved = await persistence.StartAttemptAsync(
            tenantContext,
            new StartMigrationAttemptCommand(runId, operation, key, fingerprint),
            cancellationToken);

        var runRecord = await persistence.FindRunAsync(tenantContext, runId, cancellationToken);
        if (runRecord is null)
        {
            return MigrationOperationResult<MigrationAttemptRecord>.Rejected("migration_run_not_found");
        }

        var run = MigrationRun.Rehydrate(runRecord);
        var attempt = saved.Value is null ? null : MigrationAttempt.Rehydrate(saved.Value);
        var metadata = MigrationAuditMetadata.Create()
            .With("operation", operation.ToString())
            .With("to", run.Status.ToString());
        if (attempt is not null)
        {
            metadata.With("sequence", attempt.Sequence);
        }

        var evidence = await AppendAsync(
            requestContext,
            run,
            StartAttemptOperationId,
            saved,
            attempt,
            idempotencyKey: key,
            metadata,
            cancellationToken);
        if (!evidence)
        {
            return EvidenceFailure<MigrationAttemptRecord>(saved);
        }

        return MapPersistence(saved);
    }

    public async Task<MigrationOperationResult<MigrationRunRecord>> TransitionRunAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        MigrationRunStatus target,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        if (requestContext.TenantContext is not { } tenantContext)
        {
            return MigrationOperationResult<MigrationRunRecord>.Rejected("migration_tenant_context_required");
        }

        if (runId == Guid.Empty)
        {
            return MigrationOperationResult<MigrationRunRecord>.Rejected("migration_run_id_invalid");
        }

        var before = await persistence.FindRunAsync(tenantContext, runId, cancellationToken);
        if (before is null)
        {
            return MigrationOperationResult<MigrationRunRecord>.Rejected("migration_run_not_found");
        }

        var saved = await persistence.ApplyRunTransitionAsync(
            tenantContext,
            new ApplyMigrationRunTransitionCommand(runId, target, expectedVersion),
            cancellationToken);

        var run = MigrationRun.Rehydrate(saved.Value ?? before);
        var metadata = MigrationAuditMetadata.Create()
            .With("from", before.Status.ToString())
            .With("to", target.ToString());
        var evidence = await AppendAsync(
            requestContext,
            run,
            TransitionOperationId,
            saved,
            attempt: null,
            idempotencyKey: null,
            metadata,
            cancellationToken);
        if (!evidence)
        {
            return EvidenceFailure<MigrationRunRecord>(saved);
        }

        return MapPersistence(saved);
    }

    public async Task<MigrationOperationResult<MigrationAttemptRecord>> RecordAttemptOutcomeAsync(
        FoundationRequestContext requestContext,
        Guid runId,
        Guid attemptId,
        MigrationAttemptOutcome outcome,
        string safeOutcomeCode,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        if (requestContext.TenantContext is not { } tenantContext)
        {
            return MigrationOperationResult<MigrationAttemptRecord>.Rejected("migration_tenant_context_required");
        }

        var runRecord = await persistence.FindRunAsync(tenantContext, runId, cancellationToken);
        if (runRecord is null)
        {
            return MigrationOperationResult<MigrationAttemptRecord>.Rejected("migration_run_not_found");
        }

        var saved = await persistence.RecordAttemptOutcomeAsync(
            tenantContext,
            new RecordMigrationAttemptOutcomeCommand(runId, attemptId, outcome, safeOutcomeCode, expectedVersion),
            cancellationToken);

        var run = MigrationRun.Rehydrate(runRecord);
        var attempt = saved.Value is null ? null : MigrationAttempt.Rehydrate(saved.Value);
        var metadata = MigrationAuditMetadata.Create().With("result", outcome.ToString());
        if (attempt is not null)
        {
            metadata.With("sequence", attempt.Sequence);
        }

        var evidence = await AppendAsync(
            requestContext,
            run,
            AttemptOutcomeOperationId,
            saved,
            attempt,
            idempotencyKey: null,
            metadata,
            cancellationToken);
        if (!evidence)
        {
            return EvidenceFailure<MigrationAttemptRecord>(saved);
        }

        return MapPersistence(saved);
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

    /// <summary>
    /// Appends exactly one evidence record describing the persisted outcome.
    /// Returns false when mandatory evidence could not be appended.
    /// </summary>
    private async Task<bool> AppendAsync<T>(
        FoundationRequestContext requestContext,
        MigrationRun run,
        string operationId,
        MigrationPersistenceResult<T> saved,
        MigrationAttempt? attempt,
        MigrationIdempotencyKey? idempotencyKey,
        MigrationAuditMetadata metadata,
        CancellationToken cancellationToken)
    {
        var (decision, reason) = Classify(saved.Outcome);
        if (saved.Outcome == MigrationPersistenceOutcome.Replayed)
        {
            metadata.With("replayed", "true");
        }

        FoundationAuditEvidence evidence;
        try
        {
            evidence = MigrationAuditEvidenceFactory.Create(
                requestContext,
                run,
                operationId,
                decision,
                reason,
                saved.Code,
                attempt,
                idempotencyKey,
                metadata,
                occurredAt: timeProvider.GetUtcNow());
        }
        catch (ArgumentException)
        {
            return false;
        }

        try
        {
            await auditSink.AppendAsync(evidence, cancellationToken);
            return true;
        }
        catch (FoundationAuditAppendException)
        {
            return false;
        }
    }

    private static (FoundationAuditDecision Decision, FoundationAuditReason Reason) Classify(
        MigrationPersistenceOutcome outcome) => outcome switch
        {
            MigrationPersistenceOutcome.Succeeded => (FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed),
            MigrationPersistenceOutcome.Replayed => (FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed),
            MigrationPersistenceOutcome.Conflict => (FoundationAuditDecision.Conflict, FoundationAuditReason.IdempotencyConflict),
            MigrationPersistenceOutcome.NotFound => (FoundationAuditDecision.Denied, FoundationAuditReason.NotFound),
            MigrationPersistenceOutcome.InvalidReference => (FoundationAuditDecision.Denied, FoundationAuditReason.ValidationFailed),
            MigrationPersistenceOutcome.UnknownOutcome => (FoundationAuditDecision.Conflict, FoundationAuditReason.InternalFailure),
            _ => (FoundationAuditDecision.Denied, FoundationAuditReason.InternalFailure)
        };

    /// <summary>
    /// An effect that cannot be evidenced cannot be proved, so it becomes an
    /// unknown outcome requiring reconciliation. Where no effect occurred, the
    /// evidence failure is a retry-safe known failure instead.
    /// </summary>
    private static MigrationOperationResult<T> EvidenceFailure<T>(MigrationPersistenceResult<T> saved) =>
        saved.Outcome is MigrationPersistenceOutcome.Succeeded
            or MigrationPersistenceOutcome.Replayed
            or MigrationPersistenceOutcome.UnknownOutcome
            ? MigrationOperationResult<T>.Unknown(EvidenceUnavailableCode)
            : MigrationOperationResult<T>.Failure(EvidenceUnavailableCode, safeToRetry: true);

    private static MigrationOperationResult<T> MapPersistence<T>(MigrationPersistenceResult<T> saved) => saved.Outcome switch
    {
        MigrationPersistenceOutcome.Succeeded => MigrationOperationResult<T>.Success(saved.Value!, saved.Code),
        MigrationPersistenceOutcome.Replayed => MigrationOperationResult<T>.Replay(saved.Value!),
        MigrationPersistenceOutcome.Conflict => MigrationOperationResult<T>.Rejected(saved.Code),
        MigrationPersistenceOutcome.NotFound => MigrationOperationResult<T>.Rejected(saved.Code),
        MigrationPersistenceOutcome.InvalidReference => MigrationOperationResult<T>.Rejected(saved.Code),
        MigrationPersistenceOutcome.UnknownOutcome => MigrationOperationResult<T>.Unknown(saved.Code),
        _ => MigrationOperationResult<T>.Failure(saved.Code)
    };
}

#pragma warning restore CS1591
