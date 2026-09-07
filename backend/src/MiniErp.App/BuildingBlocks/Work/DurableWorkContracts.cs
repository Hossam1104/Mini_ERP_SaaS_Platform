#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>Lifecycle states for a Tenant-owned durable work item.</summary>
public enum DurableWorkLifecycle
{
    Pending = 1,
    Claimed = 2,
    Completed = 3,
    RetryScheduled = 4,
    DeadLetter = 5,

    /// <summary>
    /// The protected-effect boundary was reached but its completion could not
    /// be proven: a caught post-boundary exception, a caught cancellation, a
    /// provider-reported uncertainty, or a completion-recording failure
    /// observed by the still-running process. This is a dedicated
    /// Tenant-scoped reconciliation state: normal polling never selects it
    /// and ordinary dead-letter/replay handling never restarts it. Only
    /// explicit, Tenant-scoped reconciliation access may read it.
    /// An actual process crash is a different, unhandled failure mode: it
    /// loses this in-memory ledger entirely rather than recording
    /// OutcomeUnknown. Production durable crash recovery for this local
    /// Foundation seam remains deferred; nothing here claims to survive a
    /// real process crash.
    /// </summary>
    OutcomeUnknown = 6
}

/// <summary>Safe, provider-neutral failure categories.</summary>
public enum DurableWorkFailureCategory
{
    None = 0,
    ValidationFailed = 1,
    TenantMismatch = 2,
    AuthorizationDenied = 3,
    ConcurrencyConflict = 4,
    HandlerFailed = 5,
    ProviderUnavailable = 6,
    Unknown = 7,
    Cancelled = 8
}

/// <summary>Safe notification delivery states.</summary>
public enum NotificationDeliveryState
{
    Pending = 1,
    Delivered = 2,
    RetryScheduled = 3,
    DeadLetter = 4,
    Duplicate = 5
}

/// <summary>Safe file metadata dispositions. No physical purge is implied.</summary>
public enum PrivateFileDisposition
{
    Available = 1,
    Expired = 2,
    Disposed = 3,
    ChecksumFailed = 4
}

/// <summary>
/// Safe file-access outcomes that do not reveal foreign targets. A missing
/// object and an object that exists but belongs to a different Tenant are
/// externally indistinguishable: both surface as <see cref="NotFound"/> on
/// every <see cref="PrivateFileAccessResult"/> returned to a caller.
/// <see cref="TenantDenied"/> exists only as an internal safe audit-evidence
/// classification and is never the <see cref="PrivateFileAccessResult.Outcome"/>
/// of a caller-visible result.
/// </summary>
public enum PrivateFileAccessOutcome
{
    Allowed = 1,
    NotFound = 2,
    TenantDenied = 3,
    Expired = 4,
    ChecksumFailed = 5,
    ConcurrencyConflict = 6,
    Disposed = 7
}

/// <summary>Marker for a handler-specific, typed durable-work payload.</summary>
public interface IWorkPayload
{
}

/// <summary>
/// An untrusted, client/request-level organization target. It is deliberately
/// separate from <see cref="TenantWorkScope"/> so raw identifiers cannot be
/// mistaken for verified ownership evidence.
/// </summary>
public sealed class TenantWorkScopeRequest
{
    public TenantWorkScopeRequest(
        Guid? companyId = null,
        Guid? branchId = null,
        Guid? warehouseId = null)
    {
        ValidateIdentifiers(companyId, branchId, warehouseId);
        if (branchId.HasValue && !companyId.HasValue)
        {
            throw new ArgumentException("A branch scope requires its company scope.", nameof(companyId));
        }

        if (warehouseId.HasValue && !branchId.HasValue)
        {
            throw new ArgumentException("A warehouse scope requires its branch scope.", nameof(branchId));
        }

        CompanyId = companyId;
        BranchId = branchId;
        WarehouseId = warehouseId;
    }

    public Guid? CompanyId { get; }

    public Guid? BranchId { get; }

    public Guid? WarehouseId { get; }

    public static TenantWorkScopeRequest TenantWide() => new();

    public static TenantWorkScopeRequest ForCompany(Guid companyId) => new(companyId: companyId);

    public static TenantWorkScopeRequest ForBranch(Guid companyId, Guid branchId) =>
        new(companyId, branchId);

    public static TenantWorkScopeRequest ForWarehouse(Guid companyId, Guid branchId, Guid warehouseId) =>
        new(companyId, branchId, warehouseId);

    private static void ValidateIdentifiers(Guid? companyId, Guid? branchId, Guid? warehouseId)
    {
        if (companyId == Guid.Empty || branchId == Guid.Empty || warehouseId == Guid.Empty)
        {
            throw new ArgumentException("Organization scope identifiers must not be empty.");
        }
    }
}

/// <summary>Safe result returned by an organization ownership resolver.</summary>
public sealed class TenantWorkScopeResolution
{
    private TenantWorkScopeResolution(bool allowed, TenantWorkScope? scope, string safeReason)
    {
        Allowed = allowed;
        Scope = scope;
        SafeReason = safeReason;
    }

    public bool Allowed { get; }

    public TenantWorkScope? Scope { get; }

    public string SafeReason { get; }

    public static TenantWorkScopeResolution Resolved(TenantWorkScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new TenantWorkScopeResolution(true, scope, "scope_verified");
    }

    public static TenantWorkScopeResolution Denied(string safeReason) =>
        new(false, null, SafeReasonValue(safeReason));

    private static string SafeReasonValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 64 || value.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded safe reason is required.", nameof(value));
        }

        return value.Trim();
    }
}

/// <summary>
/// Narrow, read-only organization ownership port. Identity owns the graph and
/// issues the resulting verified work scope; callers never receive the graph.
/// </summary>
public interface IOrganizationScopeOwnershipResolver
{
    TenantWorkScopeResolution Resolve(
        TenantContext trustedTenantContext,
        TenantWorkScopeRequest requestedScope);
}

/// <summary>Optional extension for resolving the server-selected scope marker.</summary>
public interface ICurrentOrganizationScopeResolver
{
    TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext);
}

/// <summary>Verified organization scope derived by the ownership resolver.</summary>
public sealed class TenantWorkScope
{
    private TenantWorkScope(
        TenantId tenantId,
        Guid? companyId,
        Guid? branchId,
        Guid? warehouseId,
        TenantContext authorityContext)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        BranchId = branchId;
        WarehouseId = warehouseId;
        AuthorizationPath = authorityContext.AuthorizationPath;
        Membership = authorityContext.Membership;
        SupportGrant = authorityContext.SupportGrant;
        ActorId = authorityContext.ActorId;
        AuthorizationScope = authorityContext.Scope;
    }

    public TenantId TenantId { get; }

    public Guid? CompanyId { get; }

    public Guid? BranchId { get; }

    public Guid? WarehouseId { get; }

    private TenantAuthorizationPath AuthorizationPath { get; }

    private MembershipReference? Membership { get; }

    private SupportGrantReference? SupportGrant { get; }

    private Guid? ActorId { get; }

    private ScopeReference? AuthorizationScope { get; }

    /// <summary>
    /// Issues a scope only from an already verified server authority context.
    /// The Identity ownership resolver is the shipping issuer; architecture
    /// tests may use this internal seam for isolated local fixtures.
    /// </summary>
    internal static TenantWorkScope IssueFromVerifiedAuthority(
        TenantContext authorityContext,
        TenantWorkScopeRequest requestedScope)
    {
        ArgumentNullException.ThrowIfNull(authorityContext);
        ArgumentNullException.ThrowIfNull(requestedScope);
        return new TenantWorkScope(
            authorityContext.TenantId,
            requestedScope.CompanyId,
            requestedScope.BranchId,
            requestedScope.WarehouseId,
            authorityContext);
    }

    internal bool IsBoundTo(TenantContext authorityContext)
    {
        ArgumentNullException.ThrowIfNull(authorityContext);
        return TenantId == authorityContext.TenantId
            && AuthorizationPath == authorityContext.AuthorizationPath
            && Membership == authorityContext.Membership
            && SupportGrant == authorityContext.SupportGrant
            && ActorId == authorityContext.ActorId
            && AuthorizationScope == authorityContext.Scope;
    }

    /// <summary>
    /// Compares only the organization boundary carried by two verified work
    /// scopes. Authorization facts are checked separately by the durable-work
    /// binding validator.
    /// </summary>
    internal bool ExactlyMatchesOrganizationScope(TenantWorkScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return TenantId == other.TenantId
            && CompanyId == other.CompanyId
            && BranchId == other.BranchId
            && WarehouseId == other.WarehouseId;
    }

    /// <summary>
    /// Requires both exact authority identity and the canonical server-issued
    /// organization scope representation used by Identity revalidation.
    /// </summary>
    internal bool IsExactlyBoundTo(TenantContext authorityContext)
    {
        ArgumentNullException.ThrowIfNull(authorityContext);
        return IsBoundTo(authorityContext)
            && authorityContext.Scope is { } suppliedScope
            && string.Equals(
                suppliedScope.Value,
                CanonicalScopeReferenceValue,
                StringComparison.Ordinal);
    }

    private string CanonicalScopeReferenceValue => WarehouseId is { } warehouseId
        ? $"Warehouse:{warehouseId}"
        : BranchId is { } branchId
            ? $"Branch:{branchId}"
            : CompanyId is { } companyId
                ? $"Company:{companyId}"
                : $"Tenant:{TenantId.Value}";

    /// <summary>
    /// Whether this verified scope authorizes reading a record at
    /// <paramref name="candidate"/>'s exact organization boundary: itself or
    /// any verified descendant. A narrower authorized scope never contains a
    /// broader or sibling boundary, and a sibling Company, Branch or
    /// Warehouse is always excluded.
    /// </summary>
    internal bool ContainsDescendant(TenantWorkScope candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (TenantId != candidate.TenantId)
        {
            return false;
        }

        if (WarehouseId is { } warehouseId)
        {
            return candidate.WarehouseId == warehouseId;
        }

        if (BranchId is { } branchId)
        {
            return candidate.BranchId == branchId && candidate.CompanyId == CompanyId;
        }

        if (CompanyId is { } companyId)
        {
            return candidate.CompanyId == companyId;
        }

        // A Tenant-wide authorized scope was itself granted explicitly (see
        // IdentityAuthorizationService.AuthorizeReconciliationReadUnsafe); it
        // then contains every record within the same Tenant.
        return true;
    }

    /// <summary>
    /// Safe organization-only containment check for read adapters. The
    /// authorization facts remain private and are still validated by the
    /// issuing resolver.
    /// </summary>
    public bool ContainsAuthorizedDescendant(TenantWorkScope candidate) => ContainsDescendant(candidate);
}

/// <summary>Immutable initiating authorization facts for durable work.</summary>
public sealed class DurableWorkInitiator
{
    private DurableWorkInitiator(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        MembershipReference? membership,
        SupportGrantReference? supportGrant,
        ScopeReference? scope,
        Guid? actorId,
        Guid? sessionId,
        DurableWorkOperationDescriptor operation,
        CorrelationId correlationId)
    {
        TenantId = tenantId;
        ArgumentNullException.ThrowIfNull(operation);
        AuthorizationPath = authorizationPath;
        Membership = membership;
        SupportGrant = supportGrant;
        Scope = scope;
        ActorId = actorId;
        SessionId = sessionId;
        Operation = operation;
        CorrelationId = correlationId;
    }

    public TenantId TenantId { get; }

    public TenantAuthorizationPath AuthorizationPath { get; }

    public MembershipReference? Membership { get; }

    public SupportGrantReference? SupportGrant { get; }

    public ScopeReference? Scope { get; }

    public Guid? ActorId { get; }

    public Guid? SessionId { get; }

    public DurableWorkOperationDescriptor Operation { get; }

    public CorrelationId CorrelationId { get; }

    /// <summary>Captures only server-derived authorization facts.</summary>
    internal static DurableWorkInitiator FromServerContext(
        TenantContext tenantContext,
        DurableWorkOperationDescriptor operation,
        Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(operation);

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier must not be empty.", nameof(sessionId));
        }

        if (!tenantContext.ActorId.HasValue || tenantContext.ActorId.Value == Guid.Empty)
        {
            throw new ArgumentException("Durable work requires a trusted actor identifier.", nameof(tenantContext));
        }

        return new DurableWorkInitiator(
            tenantContext.TenantId,
            tenantContext.AuthorizationPath,
            tenantContext.Membership,
            tenantContext.SupportGrant,
            tenantContext.Scope,
            tenantContext.ActorId,
            sessionId,
            operation,
            tenantContext.CorrelationId
                ?? throw new ArgumentException("Durable work requires a trusted correlation identifier.", nameof(tenantContext)));
    }
}

/// <summary>Stable identity for one durable work item.</summary>
public sealed class DurableWorkIdentity
{
    private DurableWorkIdentity(
        Guid workItemId,
        DurableWorkOperationDescriptor operation,
        CorrelationId correlationId,
        string idempotencyKey)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("Work item identifier must not be empty.", nameof(workItemId));
        }

        ArgumentNullException.ThrowIfNull(operation);
        WorkItemId = workItemId;
        Operation = operation;
        OperationId = operation.OperationId;
        CorrelationId = correlationId.Value is { Length: > 0 }
            ? correlationId
            : throw new ArgumentException("Correlation identifier is required.", nameof(correlationId));
        IdempotencyKey = Required(idempotencyKey, nameof(idempotencyKey));
    }

    /// <summary>
    /// Creates identity from the canonical descriptor selected by operation ID.
    /// A caller cannot supply or override the permission independently.
    /// </summary>
    public static DurableWorkIdentity Create(
        Guid workItemId,
        IDurableWorkOperationCatalogue operationCatalogue,
        string operationId,
        CorrelationId correlationId,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(operationCatalogue);
        if (!operationCatalogue.TryGet(operationId, out var operation))
        {
            throw new ArgumentException("The durable-work operation is not registered.", nameof(operationId));
        }

        return new DurableWorkIdentity(workItemId, operation, correlationId, idempotencyKey);
    }

    public Guid WorkItemId { get; }

    public string OperationId { get; }

    public DurableWorkOperationDescriptor Operation { get; }

    public CorrelationId CorrelationId { get; }

    public string IdempotencyKey { get; }

    public string WorkType => Operation.WorkType;

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128)
        {
            throw new ArgumentException("The work identity value is required and bounded.", name);
        }

        var normalized = value.Trim();
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The work identity value contains a control character.", name);
        }

        return normalized;
    }
}

/// <summary>Tenant-owned work item with immutable ownership and an immutable typed payload envelope.</summary>
public sealed class DurableWorkItem : ITenantOwned
{
    private DurableWorkItem(
        DurableWorkIdentity identity,
        DurableWorkInitiator initiator,
        TenantWorkScope scope,
        DurableWorkPayloadEnvelope payloadEnvelope,
        int maximumAttempts,
        DateTimeOffset createdAt)
    {
        Identity = identity;
        Initiator = initiator;
        Scope = scope;
        PayloadEnvelope = payloadEnvelope;
        MaximumAttempts = maximumAttempts;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        NextAttemptAt = createdAt;
        Lifecycle = DurableWorkLifecycle.Pending;
        ConcurrencyVersion = 1;
    }

    public DurableWorkIdentity Identity { get; }

    public TenantId TenantId => Initiator.TenantId;

    public DurableWorkInitiator Initiator { get; }

    public TenantWorkScope Scope { get; }

    /// <summary>
    /// Immutable, checksummed snapshot captured at submission time. No original
    /// caller payload reference is retained; a typed instance is produced only
    /// by decoding this envelope through the registered payload registry.
    /// </summary>
    public DurableWorkPayloadEnvelope PayloadEnvelope { get; }

    public int MaximumAttempts { get; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public Guid? LeaseOwnerId { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long ConcurrencyVersion { get; private set; }

    public DurableWorkLifecycle Lifecycle { get; private set; }

    public DurableWorkFailureCategory FailureCategory { get; private set; }

    public string? SafeFailureReason { get; private set; }

    /// <summary>
    /// The actual transition time into <see cref="DurableWorkLifecycle.OutcomeUnknown"/>.
    /// Never derived from <see cref="NextAttemptAt"/>, <see cref="UpdatedAt"/>,
    /// lease time, creation time or current time, none of which represent
    /// when this item's outcome actually became unknown.
    /// </summary>
    public DateTimeOffset? OutcomeUnknownAt { get; private set; }

    public bool IsDeadLettered => Lifecycle == DurableWorkLifecycle.DeadLetter;

    public static DurableWorkItem Create<TPayload>(
        TenantContext trustedTenantContext,
        TenantWorkScope scope,
        DurableWorkIdentity identity,
        TPayload payload,
        IDurableWorkPayloadRegistry payloadRegistry,
        Guid initiatingSessionId,
        int maximumAttempts = 3,
        DateTimeOffset? createdAt = null)
        where TPayload : IWorkPayload
    {
        ArgumentNullException.ThrowIfNull(trustedTenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payloadRegistry);
        if (scope.TenantId != trustedTenantContext.TenantId
            || !scope.IsBoundTo(trustedTenantContext))
        {
            throw new ArgumentException("Organization scope must be verified for the trusted authorization context.", nameof(scope));
        }

        if (!identity.Operation.AllowedAuthorizationPaths.Contains(trustedTenantContext.AuthorizationPath))
        {
            throw new ArgumentException("The operation is not registered for the trusted authorization path.", nameof(identity));
        }

        if (!identity.Operation.RequiresMandatorySecurityEvidence)
        {
            throw new ArgumentException(
                "Protected durable work requires the mandatory security-evidence policy.",
                nameof(identity));
        }

        if (identity.Operation.ScopePolicy == DurableWorkScopePolicy.TenantWideOnly
            && (scope.CompanyId.HasValue || scope.BranchId.HasValue || scope.WarehouseId.HasValue))
        {
            throw new ArgumentException("The operation requires a Tenant-wide verified scope.", nameof(scope));
        }

        if (maximumAttempts is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        var initiator = DurableWorkInitiator.FromServerContext(
            trustedTenantContext,
            identity.Operation,
            initiatingSessionId);
        if (!string.Equals(identity.CorrelationId.Value, initiator.CorrelationId.Value, StringComparison.Ordinal))
        {
            throw new ArgumentException("Work correlation must match the trusted context.", nameof(identity));
        }

        // Snapshot immediately: only the immutable encoded envelope is stored.
        // The caller's original payload reference is never retained.
        var envelope = payloadRegistry.Capture(payload);
        return new DurableWorkItem(identity, initiator, scope, envelope, maximumAttempts, createdAt ?? DateTimeOffset.UtcNow);
    }

    internal bool IsEligible(TenantContext context, DateTimeOffset now)
    {
        return Lifecycle is DurableWorkLifecycle.Pending or DurableWorkLifecycle.RetryScheduled
            && NextAttemptAt <= now
            && (LeaseExpiresAt is null || LeaseExpiresAt <= now)
            && context.TenantId == TenantId
            && context.AuthorizationPath == Initiator.AuthorizationPath;
    }

    internal DurableWorkLease Claim(Guid workerId, DateTimeOffset leaseExpiresAt)
    {
        if (workerId == Guid.Empty)
        {
            throw new ArgumentException("Worker identifier must not be empty.", nameof(workerId));
        }

        Lifecycle = DurableWorkLifecycle.Claimed;
        LeaseOwnerId = workerId;
        LeaseExpiresAt = leaseExpiresAt;
        UpdatedAt = DateTimeOffset.UtcNow;
        ConcurrencyVersion++;
        return new DurableWorkLease(Identity.WorkItemId, TenantId, workerId, leaseExpiresAt, ConcurrencyVersion);
    }

    internal DurableWorkCompletion Complete(
        DurableWorkLease lease,
        DurableWorkHandlerResult result,
        DateTimeOffset now)
    {
        if (Lifecycle != DurableWorkLifecycle.Claimed
            || LeaseOwnerId != lease.WorkerId
            || lease.ConcurrencyVersion != ConcurrencyVersion
            || LeaseExpiresAt is null
            || LeaseExpiresAt < now)
        {
            return DurableWorkCompletion.CreateDenied(DurableWorkFailureCategory.ConcurrencyConflict);
        }

        AttemptCount++;
        FailureCategory = result.FailureCategory;
        SafeFailureReason = result.SafeReason;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        if (result.Success)
        {
            Lifecycle = DurableWorkLifecycle.Completed;
        }
        else if (result.IsOutcomeUnknown)
        {
            // Uncertain effects are never auto-retried, regardless of the
            // remaining attempt budget: only explicit reconciliation applies.
            Lifecycle = DurableWorkLifecycle.OutcomeUnknown;
            OutcomeUnknownAt = now;
        }
        else if (result.DeadLetter || AttemptCount >= MaximumAttempts)
        {
            Lifecycle = DurableWorkLifecycle.DeadLetter;
        }
        else
        {
            Lifecycle = DurableWorkLifecycle.RetryScheduled;
            NextAttemptAt = now.Add(result.RetryAfter);
        }

        UpdatedAt = now;
        ConcurrencyVersion++;
        return DurableWorkCompletion.CreateAccepted(Lifecycle, AttemptCount, FailureCategory);
    }

    internal void MarkExpiredLease(DateTimeOffset now)
    {
        if (Lifecycle == DurableWorkLifecycle.Claimed && LeaseExpiresAt <= now)
        {
            Lifecycle = DurableWorkLifecycle.RetryScheduled;
            LeaseOwnerId = null;
            LeaseExpiresAt = null;
            NextAttemptAt = now;
            UpdatedAt = now;
            ConcurrencyVersion++;
        }
    }
}

/// <summary>Lease proof used to complete exactly one claimed item.</summary>
public sealed record DurableWorkLease(
    Guid WorkItemId,
    TenantId TenantId,
    Guid WorkerId,
    DateTimeOffset LeaseExpiresAt,
    long ConcurrencyVersion);

/// <summary>Reusable exact-binding validator for server-issued work authority.</summary>
internal static class DurableWorkAuthorizationBinding
{
    internal static bool ExactlyMatches(
        Guid authorizationWorkItemId,
        TenantId authorizationTenantId,
        CorrelationId authorizationCorrelationId,
        DurableWorkItem workItem,
        DurableWorkOperationDescriptor operation,
        TenantContext executionTenantContext,
        TenantWorkScope scope,
        Guid actorId,
        Guid sessionId)
    {
        var initiator = workItem.Initiator;
        var executionPathIsConsistent = executionTenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership =>
                executionTenantContext.Membership.HasValue
                && !executionTenantContext.SupportGrant.HasValue,
            TenantAuthorizationPath.SupportGrant =>
                !executionTenantContext.Membership.HasValue
                && executionTenantContext.SupportGrant.HasValue,
            _ => false
        };
        var initiatorPathIsConsistent = initiator.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership =>
                initiator.Membership.HasValue
                && !initiator.SupportGrant.HasValue,
            TenantAuthorizationPath.SupportGrant =>
                !initiator.Membership.HasValue
                && initiator.SupportGrant.HasValue,
            _ => false
        };

        return authorizationWorkItemId == workItem.Identity.WorkItemId
            && operation.RequiresMandatorySecurityEvidence
            && workItem.Identity.Operation.ExactlyMatches(operation)
            && initiator.Operation.ExactlyMatches(operation)
            && authorizationCorrelationId == workItem.Identity.CorrelationId
            && initiator.CorrelationId == authorizationCorrelationId
            && executionTenantContext.CorrelationId is { } executionCorrelation
            && executionCorrelation == authorizationCorrelationId
            && authorizationTenantId == workItem.TenantId
            && executionTenantContext.TenantId == authorizationTenantId
            && scope.ExactlyMatchesOrganizationScope(workItem.Scope)
            && scope.IsExactlyBoundTo(executionTenantContext)
            && executionPathIsConsistent
            && initiatorPathIsConsistent
            && executionTenantContext.AuthorizationPath == initiator.AuthorizationPath
            && executionTenantContext.Membership == initiator.Membership
            && executionTenantContext.SupportGrant == initiator.SupportGrant
            && initiator.ActorId is { } storedActorId
            && storedActorId != Guid.Empty
            && actorId != Guid.Empty
            && actorId == storedActorId
            && executionTenantContext.ActorId == actorId
            && initiator.SessionId is { } storedSessionId
            && storedSessionId != Guid.Empty
            && sessionId != Guid.Empty
            && sessionId == storedSessionId;
    }
}

/// <summary>
/// Server-issued authority that binds one work item to the exact current
/// operation, actor, session, authorization path and organization scope.
/// </summary>
public sealed class VerifiedDurableWorkAuthorization
{
    internal VerifiedDurableWorkAuthorization(
        DurableWorkItem workItem,
        Guid workItemId,
        TenantId tenantId,
        CorrelationId correlationId,
        DurableWorkOperationDescriptor operation,
        TenantContext executionTenantContext,
        TenantWorkScope scope,
        Guid actorId,
        Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(executionTenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        if (!DurableWorkAuthorizationBinding.ExactlyMatches(
                workItemId,
                tenantId,
                correlationId,
                workItem,
                operation,
                executionTenantContext,
                scope,
                actorId,
                sessionId))
        {
            throw new ArgumentException("The durable-work authorization is not an exact server-issued match.", nameof(executionTenantContext));
        }

        WorkItemId = workItemId;
        TenantId = tenantId;
        CorrelationId = correlationId;
        Operation = operation;
        ExecutionTenantContext = executionTenantContext;
        Scope = scope;
        ActorId = actorId;
        SessionId = sessionId;
    }

    public Guid WorkItemId { get; }

    public TenantId TenantId { get; }

    public CorrelationId CorrelationId { get; }

    public DurableWorkOperationDescriptor Operation { get; }

    public TenantContext ExecutionTenantContext { get; }

    public TenantWorkScope Scope { get; }

    public Guid ActorId { get; }

    public Guid SessionId { get; }
}

/// <summary>Typed execution context reconstructed only from verified authority.</summary>
public sealed class DurableWorkExecutionContext
{
    internal DurableWorkExecutionContext(
        DurableWorkItem workItem,
        VerifiedDurableWorkAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(authorization);
        if (!DurableWorkAuthorizationBinding.ExactlyMatches(
                authorization.WorkItemId,
                authorization.TenantId,
                authorization.CorrelationId,
                workItem,
                authorization.Operation,
                authorization.ExecutionTenantContext,
                authorization.Scope,
                authorization.ActorId,
                authorization.SessionId))
        {
            throw new ArgumentException("Execution context does not match the verified work authorization.", nameof(authorization));
        }

        WorkItemId = workItem.Identity.WorkItemId;
        TenantContext = authorization.ExecutionTenantContext;
        Scope = authorization.Scope;
        Operation = authorization.Operation;
        OperationId = authorization.Operation.OperationId;
        CorrelationId = authorization.CorrelationId;
        ActorId = authorization.ActorId;
        SessionId = authorization.SessionId;
    }

    public Guid WorkItemId { get; }

    public TenantContext TenantContext { get; }

    public TenantWorkScope Scope { get; }

    public DurableWorkOperationDescriptor Operation { get; }

    public string OperationId { get; }

    public CorrelationId CorrelationId { get; }

    public Guid ActorId { get; }

    public Guid SessionId { get; }
}

/// <summary>Safe result returned by a typed work handler.</summary>
public sealed class DurableWorkHandlerResult
{
    private DurableWorkHandlerResult(
        bool success,
        bool deadLetter,
        bool isOutcomeUnknown,
        TimeSpan retryAfter,
        DurableWorkFailureCategory failureCategory,
        string? safeReason)
    {
        Success = success;
        DeadLetter = deadLetter;
        IsOutcomeUnknown = isOutcomeUnknown;
        RetryAfter = retryAfter;
        FailureCategory = failureCategory;
        SafeReason = safeReason;
    }

    public bool Success { get; }

    public bool DeadLetter { get; }

    /// <summary>
    /// The protected-effect boundary was reached but completion could not be
    /// proven. Distinct from an ordinary <see cref="DeadLetter"/>: it is
    /// never automatically retried and requires explicit reconciliation.
    /// </summary>
    public bool IsOutcomeUnknown { get; }

    public TimeSpan RetryAfter { get; }

    public DurableWorkFailureCategory FailureCategory { get; }

    public string? SafeReason { get; }

    public static DurableWorkHandlerResult Succeeded() =>
        new(true, false, false, TimeSpan.Zero, DurableWorkFailureCategory.None, null);

    public static DurableWorkHandlerResult Retry(
        DurableWorkFailureCategory category,
        TimeSpan retryAfter,
        string safeReason)
    {
        if (category == DurableWorkFailureCategory.None || retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentException("Retry requires a bounded failure category and delay.");
        }

        return new(false, false, false, retryAfter > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : retryAfter, category, SanitizeReason(safeReason));
    }

    public static DurableWorkHandlerResult DeadLettered(
        DurableWorkFailureCategory category,
        string safeReason) =>
        new(false, true, false, TimeSpan.Zero, category == DurableWorkFailureCategory.None ? DurableWorkFailureCategory.Unknown : category, SanitizeReason(safeReason));

    /// <summary>
    /// Records an explicit uncertain outcome. Never automatically repeats;
    /// requires reconciliation through the Tenant-scoped reconciliation port.
    /// </summary>
    public static DurableWorkHandlerResult OutcomeUnknown(string safeReason) =>
        new(false, false, true, TimeSpan.Zero, DurableWorkFailureCategory.Unknown, SanitizeReason(safeReason));

    private static string SanitizeReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Failure reason must be safe and bounded.", nameof(value));
        }

        return value.Trim();
    }
}

/// <summary>Result of a guarded state transition.</summary>
public sealed record DurableWorkCompletion(
    bool Accepted,
    DurableWorkLifecycle Lifecycle,
    int AttemptCount,
    DurableWorkFailureCategory FailureCategory)
{
    internal static DurableWorkCompletion CreateAccepted(
        DurableWorkLifecycle lifecycle,
        int attemptCount,
        DurableWorkFailureCategory failureCategory) =>
        new(true, lifecycle, attemptCount, failureCategory);

    internal static DurableWorkCompletion CreateDenied(DurableWorkFailureCategory failureCategory) =>
        new(false, DurableWorkLifecycle.Claimed, 0, failureCategory);
}

/// <summary>
/// Handler-specific typed work contract. <see cref="ExecuteAsync"/> runs only
/// inside the approved effect executor, after the effect reservation
/// boundary: it must return an explicit <see cref="DurableWorkProtectedEffectResult"/>
/// outcome. A bare generic retry is not a representable return value, so a
/// handler cannot apply an effect and then release its own reservation by
/// accident.
/// </summary>
public interface IDurableWorkHandler<TPayload>
    where TPayload : IWorkPayload
{
    DurableWorkOperationDescriptor Operation { get; }

    ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
        TPayload payload,
        DurableWorkExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Safe outcome of live durable-work authority revalidation.</summary>
public enum DurableWorkAuthorityValidationOutcome
{
    Approved = 1,
    Denied = 2,
    TemporarilyUnavailable = 3,
    Cancelled = 4
}

/// <summary>Safe outcome of live durable-work authority revalidation.</summary>
public sealed class DurableWorkAuthorityValidationResult
{
    private DurableWorkAuthorityValidationResult(
        DurableWorkAuthorityValidationOutcome outcome,
        DurableWorkFailureCategory failureCategory,
        string safeReason,
        VerifiedDurableWorkAuthorization? authorization)
    {
        Outcome = outcome;
        Allowed = outcome == DurableWorkAuthorityValidationOutcome.Approved;
        FailureCategory = failureCategory;
        SafeReason = safeReason;
        Authorization = authorization;
    }

    public DurableWorkAuthorityValidationOutcome Outcome { get; }

    public bool Allowed { get; }

    public DurableWorkFailureCategory FailureCategory { get; }

    public string SafeReason { get; }

    public VerifiedDurableWorkAuthorization? Authorization { get; }

    public static DurableWorkAuthorityValidationResult Approved(
        VerifiedDurableWorkAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        return new(
            DurableWorkAuthorityValidationOutcome.Approved,
            DurableWorkFailureCategory.None,
            "authority_verified",
            authorization);
    }

    public static DurableWorkAuthorityValidationResult Denied(string safeReason) =>
        new(
            DurableWorkAuthorityValidationOutcome.Denied,
            DurableWorkFailureCategory.AuthorizationDenied,
            SafeReasonValue(safeReason),
            null);

    public static DurableWorkAuthorityValidationResult TemporarilyUnavailable(string safeReason) =>
        new(
            DurableWorkAuthorityValidationOutcome.TemporarilyUnavailable,
            DurableWorkFailureCategory.ProviderUnavailable,
            SafeReasonValue(safeReason),
            null);

    public static DurableWorkAuthorityValidationResult Cancelled(string safeReason) =>
        new(
            DurableWorkAuthorityValidationOutcome.Cancelled,
            DurableWorkFailureCategory.Cancelled,
            SafeReasonValue(safeReason),
            null);

    private static string SafeReasonValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 64 || value.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded safe reason is required.", nameof(value));
        }

        return value.Trim();
    }
}

/// <summary>
/// Narrow live-authority port used immediately before durable-work dispatch.
/// The implementation re-resolves Identity state; stored work facts are not
/// treated as current permission or lifecycle evidence.
/// </summary>
public interface IDurableWorkAuthorityRevalidator
{
    ValueTask<DurableWorkAuthorityValidationResult> RevalidateAsync(
        DurableWorkItem workItem,
        TenantContext currentTenantContext,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

/// <summary>Safe outcome of live durable-work reconciliation-read authorization.</summary>
public enum DurableWorkReconciliationAuthorizationOutcome
{
    Approved = 1,
    Denied = 2
}

/// <summary>
/// Safe result of one live reconciliation-read authorization attempt. Never
/// exposes a foreign record's identity; a denial carries only a bounded,
/// generic safe reason.
/// </summary>
public sealed class DurableWorkReconciliationAuthorizationResult
{
    private DurableWorkReconciliationAuthorizationResult(
        DurableWorkReconciliationAuthorizationOutcome outcome,
        string safeReason,
        VerifiedDurableWorkReconciliationAuthorization? authorization)
    {
        Outcome = outcome;
        Allowed = outcome == DurableWorkReconciliationAuthorizationOutcome.Approved;
        SafeReason = safeReason;
        Authorization = authorization;
    }

    public DurableWorkReconciliationAuthorizationOutcome Outcome { get; }

    public bool Allowed { get; }

    public string SafeReason { get; }

    public VerifiedDurableWorkReconciliationAuthorization? Authorization { get; }

    public static DurableWorkReconciliationAuthorizationResult Approved(
        VerifiedDurableWorkReconciliationAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        return new(DurableWorkReconciliationAuthorizationOutcome.Approved, "reconciliation_authorized", authorization);
    }

    public static DurableWorkReconciliationAuthorizationResult Denied(string safeReason) =>
        new(DurableWorkReconciliationAuthorizationOutcome.Denied, SafeReasonValue(safeReason), null);

    private static string SafeReasonValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 64 || value.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded safe reason is required.", nameof(value));
        }

        return value.Trim();
    }
}

/// <summary>
/// Server-issued authority that binds one Tenant-scoped durable-work
/// reconciliation read to the exact current actor, session, authorization
/// path and organization scope. A raw <see cref="TenantContext"/> alone is
/// never sufficient evidence to read uncertain-effect records: this type is
/// issued only by <see cref="IDurableWorkReconciliationAuthorizer"/> after
/// live revalidation, mirroring the same Identity-owned revalidation and
/// organization-scope logic used for durable-work dispatch authority.
/// PlatformGovernanceContext is never a source for this authority: a Platform
/// actor has no Tenant Membership or SupportGrant path to authorize through.
/// </summary>
public sealed class VerifiedDurableWorkReconciliationAuthorization
{
    internal VerifiedDurableWorkReconciliationAuthorization(
        TenantContext executionTenantContext,
        TenantWorkScope scope,
        Guid actorId,
        Guid sessionId,
        CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(executionTenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        if (actorId == Guid.Empty || sessionId == Guid.Empty)
        {
            throw new ArgumentException("Reconciliation authorization requires an exact actor and session.", nameof(actorId));
        }

        if (executionTenantContext.ActorId != actorId || !scope.IsExactlyBoundTo(executionTenantContext))
        {
            throw new ArgumentException(
                "Reconciliation authorization scope must be exactly bound to the execution context.",
                nameof(scope));
        }

        ExecutionTenantContext = executionTenantContext;
        Scope = scope;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
    }

    public TenantId TenantId => Scope.TenantId;

    public TenantWorkScope Scope { get; }

    public TenantContext ExecutionTenantContext { get; }

    public Guid ActorId { get; }

    public Guid SessionId { get; }

    public CorrelationId CorrelationId { get; }

    /// <summary>
    /// Whether this authorized scope covers <paramref name="candidate"/>'s
    /// exact organization boundary: itself or a verified descendant only.
    /// </summary>
    internal bool Contains(TenantWorkScope candidate) => Scope.ContainsDescendant(candidate);
}

/// <summary>
/// Narrow port that authorizes exactly one Tenant-scoped durable-work
/// reconciliation read. The implementation re-resolves live Identity state
/// (actor, session, Membership or SupportGrant, exact catalogue-backed
/// permission and organization-scope ownership) using the same logic as
/// durable-work dispatch revalidation; a stored or cached fact is never
/// treated as current authority.
/// </summary>
public interface IDurableWorkReconciliationAuthorizer
{
    ValueTask<DurableWorkReconciliationAuthorizationResult> AuthorizeAsync(
        TenantContext currentTenantContext,
        Guid sessionId,
        TenantWorkScopeRequest requestedReadScope,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

/// <summary>Stable, Tenant-owned outbox envelope.</summary>
public sealed class TenantOutboxMessage : ITenantOwned
{
    internal TenantOutboxMessage(
        Guid eventId,
        DurableWorkItem workItem,
        DateTimeOffset occurredAt)
    {
        EventId = eventId;
        WorkItemId = workItem.Identity.WorkItemId;
        TenantId = workItem.TenantId;
        Scope = workItem.Scope;
        EventType = workItem.Identity.OperationId;
        CorrelationId = workItem.Identity.CorrelationId;
        OccurredAt = occurredAt;
        AttemptCount = 0;
        NextAttemptAt = occurredAt;
    }

    public Guid EventId { get; }

    public Guid WorkItemId { get; }

    public TenantId TenantId { get; }

    public TenantWorkScope Scope { get; }

    public string EventType { get; }

    public CorrelationId CorrelationId { get; }

    public DateTimeOffset OccurredAt { get; }

    public int AttemptCount { get; internal set; }

    public DateTimeOffset NextAttemptAt { get; internal set; }

    public DurableWorkLifecycle DeliveryState { get; internal set; } = DurableWorkLifecycle.Pending;

    public DurableWorkFailureCategory FailureCategory { get; internal set; }

    /// <summary>
    /// The actual transition time into <see cref="DurableWorkLifecycle.OutcomeUnknown"/>.
    /// Never derived from <see cref="NextAttemptAt"/>, which is a scheduling
    /// field unrelated to when the outcome actually became unknown.
    /// </summary>
    public DateTimeOffset? OutcomeUnknownAt { get; internal set; }

    /// <summary>
    /// The bounded, provider/executor-reported safe reason preserved when this
    /// message transitions to <see cref="DurableWorkLifecycle.OutcomeUnknown"/>.
    /// Never a raw provider exception message.
    /// </summary>
    public string? SafeFailureReason { get; internal set; }
}

/// <summary>Safe audit record emitted by local durable-work adapters.</summary>
public sealed record DurableWorkAuditRecord(
    DateTimeOffset OccurredAt,
    string EventType,
    TenantId TenantId,
    Guid WorkItemId,
    CorrelationId CorrelationId,
    DurableWorkLifecycle Lifecycle,
    DurableWorkFailureCategory FailureCategory,
    int AttemptCount);

/// <summary>
/// Safe, scope-authorized evidence of one effect currently in the explicit
/// <see cref="DurableWorkLifecycle.OutcomeUnknown"/> reconciliation state.
/// Identity is derived from the exact <see cref="DurableWorkEffectKey"/>: a
/// handler record therefore never carries an EventId, an outbox record always
/// carries its immutable EventId, and two uncertain outbox events for the
/// same work item remain distinguishable records. <see cref="Scope"/> is the
/// exact verified organization boundary the effect was raised under and is
/// what the reconciliation read port filters on before any record is
/// returned. Never carries payload bytes, provider exception text, SQL,
/// tokens, cookies or any other secret; only a bounded safe reason and
/// correlation are recorded.
/// </summary>
public sealed record DurableWorkUncertainEffectRecord(
    DurableWorkEffectKey EffectKey,
    TenantWorkScope Scope,
    CorrelationId CorrelationId,
    DateTimeOffset OutcomeUnknownAt,
    string SafeReason,
    long Version)
{
    public DurableWorkEffectPurpose Purpose => EffectKey.Purpose;

    public TenantId TenantId => EffectKey.TenantId;

    public Guid WorkItemId => EffectKey.WorkItemId;

    public string OperationId => EffectKey.OperationId;

    /// <summary>Always null for a handler-purpose record.</summary>
    public Guid? EventId => EffectKey.EventId;
}

/// <summary>
/// Transactional, Tenant-bound durable-work seam. The Foundation implementation
/// is a deterministic in-memory local adapter; it is not a relational, SQL-backed,
/// process-crash-durable, production-ready or distributed exactly-once store.
/// </summary>
public interface IDurableWorkStore
{
    ValueTask<bool> SubmitAsync(DurableWorkItem workItem, CancellationToken cancellationToken = default);

    ValueTask<DurableWorkItem?> FindAsync(
        TenantContext tenantContext,
        Guid workItemId,
        CancellationToken cancellationToken = default);

    ValueTask<DurableWorkLease?> TryClaimAsync(
        TenantContext tenantContext,
        Guid workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    ValueTask<DurableWorkCompletion> CompleteAsync(
        TenantContext tenantContext,
        DurableWorkLease lease,
        DurableWorkHandlerResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    ValueTask<OutboxDispatchResult> DispatchOutboxAsync(
        TenantContext tenantContext,
        IDurableWorkAuthorityRevalidator authorityRevalidator,
        DateTimeOffset now,
        Func<TenantOutboxMessage, VerifiedDurableWorkAuthorization, CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<DurableWorkAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scope-authorized reconciliation read port. Returns only records
    /// currently in the explicit <see cref="DurableWorkLifecycle.OutcomeUnknown"/>
    /// state whose exact Tenant and organization boundary is contained by
    /// <paramref name="authorization"/>'s verified scope; a sibling Company,
    /// Branch or Warehouse and another Tenant's records are never visible.
    /// This is a read-only evidence seam: it performs no production
    /// reconciliation action or provider decision.
    /// </summary>
    ValueTask<IReadOnlyList<DurableWorkUncertainEffectRecord>> ReadUncertainEffectsAsync(
        VerifiedDurableWorkReconciliationAuthorization authorization,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe outbox delivery result. <see cref="Delivered"/> means the protected
/// effect was Applied and will never automatically repeat. <see cref="RetryScheduled"/>
/// means the effect was proven NotAppliedRetryable and bounded retry may run.
/// <see cref="OutcomeUnknown"/> means the effect boundary was reached but its
/// completion could not be proven; it is never automatically repeated and
/// requires reconciliation.
/// </summary>
public sealed record OutboxDispatchResult(
    bool Delivered,
    bool Duplicate,
    bool RetryScheduled,
    bool DeadLettered,
    bool OutcomeUnknown,
    DurableWorkFailureCategory FailureCategory)
{
    internal static OutboxDispatchResult NoMessage() =>
        new(false, false, false, false, false, DurableWorkFailureCategory.None);

    internal static OutboxDispatchResult Applied(bool duplicate) =>
        new(true, duplicate, false, false, false, DurableWorkFailureCategory.None);

    internal static OutboxDispatchResult NotAppliedRetryable(DurableWorkFailureCategory category) =>
        new(false, false, true, false, false, category);

    internal static OutboxDispatchResult AsDeadLettered(DurableWorkFailureCategory category) =>
        new(false, false, false, true, false, category);

    internal static OutboxDispatchResult OutcomeUnknownResult() =>
        new(false, false, false, false, true, DurableWorkFailureCategory.Unknown);
}
