#pragma warning disable CS1591

using System.Text.Json.Serialization;

namespace MiniErp.Contracts.Modules.Migration;

/// <summary>
/// Bounded lifecycle vocabulary for one migration run, aligned to the
/// authoritative MESP-40 BRD section 7.2 lifecycle.
/// </summary>
/// <remarks>
/// Numeric values are durable: they are persisted by the Migration module and
/// must never be renumbered. New states are appended only.
/// <para>
/// Slice 1 deliberately truncates two BRD wording positions because their
/// owning capability is out of scope: BRD "Uploaded" is represented by
/// <see cref="Prepared"/> (Slice 1 excludes file ingestion and upload, so an
/// "Uploaded" name would overstate the implemented behavior), and BRD
/// "Preview/Dry Run Complete" has no distinct state because the validation and
/// dry-run engine is excluded from Slice 1. The transition map fails closed
/// beyond the implemented vocabulary rather than inferring the missing states.
/// </para>
/// </remarks>
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
    Corrected = 13,
    PartiallyCompleted = 14,
    ReconciliationPending = 15,
    Reconciled = 16,
    ReadyForHandover = 17,
    Closed = 18
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

/// <summary>
/// Safe evidence projection for a migration run-level or attempt-level event.
/// </summary>
/// <remarks>
/// <see cref="AttemptId"/> is nullable because a run-level event — run
/// creation, or a run state transition that belongs to the run rather than to
/// one attempt — has no owning attempt. Requiring an attempt identity here
/// would make those mandatory BRD section 16.1 events inexpressible.
/// </remarks>
public sealed record MigrationEvidenceContract(
    Guid EvidenceId,
    Guid TenantId,
    Guid RunId,
    Guid? AttemptId,
    Guid ActorId,
    string CorrelationId,
    string Operation,
    string State,
    string Outcome,
    DateTimeOffset OccurredAt,
    string? SafeMetadata);

#pragma warning restore CS1591
