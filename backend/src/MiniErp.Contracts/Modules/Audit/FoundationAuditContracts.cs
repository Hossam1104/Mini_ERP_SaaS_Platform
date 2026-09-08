#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Audit;

/// <summary>
/// The only authorization paths that may be persisted in Foundation evidence.
/// </summary>
public enum FoundationAuditAuthorizationPath
{
    OrdinaryMembership = 1,
    SupportGrant = 2,
    PlatformGovernanceContext = 3
}

/// <summary>
/// The bounded result categories emitted by the Foundation evidence seam.
/// </summary>
public enum FoundationAuditDecision
{
    Allowed = 1,
    Denied = 2,
    Conflict = 3,
    Retry = 4,
    EvidenceFailure = 5,
    EffectFailed = 6
}

/// <summary>
/// Safe, allow-listed reason categories. Provider and exception text never
/// enters this contract.
/// </summary>
public enum FoundationAuditReason
{
    Allowed = 1,
    AuthenticationFailed = 2,
    AuthorizationDenied = 3,
    CrossTenantTargetDenied = 4,
    PermissionDenied = 5,
    LifecycleDenied = 6,
    AntiforgeryFailed = 7,
    ValidationFailed = 8,
    NotFound = 9,
    ConcurrencyConflict = 10,
    IdempotencyConflict = 11,
    RetryAccepted = 12,
    EvidenceAppendFailed = 13,
    InternalFailure = 14,
    EffectFailed = 15
}

/// <summary>
/// A safe structured log, metric or trace event derived only from evidence.
/// </summary>
public enum FoundationAuditTelemetryKind
{
    StructuredLog = 1,
    Metric = 2,
    Trace = 3
}

/// <summary>
/// Immutable evidence for one protected operation. The constructor is
/// internal so only the trusted application factory can create evidence.
/// </summary>
public sealed class FoundationAuditEvidence
{
    internal FoundationAuditEvidence(
        Guid evidenceId,
        DateTimeOffset occurredAt,
        string operationId,
        string correlationId,
        Guid actorId,
        Guid? sessionId,
        FoundationAuditAuthorizationPath authorizationPath,
        Guid? tenantId,
        string? organizationScope,
        string? purpose,
        Guid? supportUserId,
        Guid? supportGrantId,
        Guid? supportCaseId,
        DateTimeOffset? supportGrantExpiresAt,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotencyKey,
        string? operationVersion,
        Guid? retryOfEvidenceId,
        int attempt,
        string source,
        string? targetType,
        string? targetReference,
        string? changeSummary)
    {
        EvidenceId = evidenceId;
        OccurredAt = occurredAt;
        OperationId = operationId;
        CorrelationId = correlationId;
        ActorId = actorId;
        SessionId = sessionId;
        AuthorizationPath = authorizationPath;
        TenantId = tenantId;
        OrganizationScope = organizationScope;
        Purpose = purpose;
        SupportUserId = supportUserId;
        SupportGrantId = supportGrantId;
        SupportCaseId = supportCaseId;
        SupportGrantExpiresAt = supportGrantExpiresAt;
        Decision = decision;
        Reason = reason;
        IdempotencyKey = idempotencyKey;
        OperationVersion = operationVersion;
        RetryOfEvidenceId = retryOfEvidenceId;
        Attempt = attempt;
        Source = source;
        TargetType = targetType;
        TargetReference = targetReference;
        ChangeSummary = changeSummary;
    }

    public Guid EvidenceId { get; }

    public DateTimeOffset OccurredAt { get; }

    public string OperationId { get; }

    public string CorrelationId { get; }

    public Guid ActorId { get; }

    public Guid? SessionId { get; }

    public FoundationAuditAuthorizationPath AuthorizationPath { get; }

    public Guid? TenantId { get; }

    public string? OrganizationScope { get; }

    public string? Purpose { get; }

    public Guid? SupportUserId { get; }

    public Guid? SupportGrantId { get; }

    public Guid? SupportCaseId { get; }

    public DateTimeOffset? SupportGrantExpiresAt { get; }

    public FoundationAuditDecision Decision { get; }

    public FoundationAuditReason Reason { get; }

    public string? IdempotencyKey { get; }

    public string? OperationVersion { get; }

    public Guid? RetryOfEvidenceId { get; }

    public int Attempt { get; }

    /// <summary>Safe source module/control that produced the evidence.</summary>
    public string Source { get; }

    /// <summary>Allow-listed target type, never a serialized request payload.</summary>
    public string? TargetType { get; }

    /// <summary>Opaque bounded target reference when one is required.</summary>
    public string? TargetReference { get; }

    /// <summary>Bounded human-readable change summary without sensitive payload.</summary>
    public string? ChangeSummary { get; }
}

/// <summary>
/// A telemetry projection with no request payload, target identifier or
/// provider detail.
/// </summary>
public sealed record FoundationAuditTelemetryEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    FoundationAuditTelemetryKind Kind,
    Guid EvidenceId,
    string OperationId,
    string CorrelationId,
    FoundationAuditAuthorizationPath AuthorizationPath,
    Guid? TenantId,
    FoundationAuditDecision Decision,
    FoundationAuditReason Reason,
    bool IsRetry);

/// <summary>
/// Safe operational signal raised when mandatory evidence cannot be appended.
/// </summary>
public sealed record FoundationAuditOperationalSignal(
    Guid SignalId,
    DateTimeOffset OccurredAt,
    string Code,
    string OperationId,
    string CorrelationId,
    FoundationAuditDecision Decision);
