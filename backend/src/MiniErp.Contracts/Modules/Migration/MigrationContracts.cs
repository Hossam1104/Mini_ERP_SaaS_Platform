#pragma warning disable CS1591

using System.Text.Json.Serialization;

namespace MiniErp.Contracts.Modules.Migration;

/// <summary>Bounded lifecycle vocabulary for one migration run.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationRunStatus
{
    Draft = 1,
    Prepared = 2,
    Validating = 3,
    ValidationFailed = 4,
    Validated = 5,
    AwaitingApproval = 6,
    Approved = 7,
    Executing = 8,
    Failed = 9,
    OutcomeUnknown = 10,
    Completed = 11,
    Cancelled = 12,
    Corrected = 13
}

/// <summary>Outcome of one explicit execution or validation attempt.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationAttemptOutcome
{
    Pending = 1,
    Succeeded = 2,
    KnownFailure = 3,
    UnknownOutcome = 4,
    Cancelled = 5
}

/// <summary>Operation boundary used by idempotency and attempt lineage.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationOperationKind
{
    Validation = 1,
    DryRun = 2,
    Execution = 3
}

/// <summary>Safe result classification for the migration foundation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationResultKind
{
    Succeeded = 1,
    Replayed = 2,
    Rejected = 3,
    KnownFailure = 4,
    UnknownOutcome = 5
}

/// <summary>Decision when a scoped idempotency identity is reserved.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationIdempotencyDecision
{
    NewReservation = 1,
    Replay = 2,
    Conflict = 3
}

/// <summary>
/// Transport-neutral source/profile identity. It carries no parser, file,
/// storage, encoding, delimiter, batch-size, or provider decision.
/// </summary>
public sealed record MigrationSourceProfileContract(
    string ProfileId,
    string ProfileVersion);

/// <summary>
/// Safe evidence classification for a migration state or attempt outcome.
/// </summary>
public sealed record MigrationEvidenceContract(
    Guid EvidenceId,
    Guid TenantId,
    Guid RunId,
    Guid AttemptId,
    Guid ActorId,
    string CorrelationId,
    string Operation,
    string State,
    string Outcome,
    DateTimeOffset OccurredAt,
    string? SafeMetadata);

#pragma warning restore CS1591
