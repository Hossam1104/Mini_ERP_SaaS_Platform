#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.BuildingBlocks.Work;

public enum SupportAccessValidationState
{
    Active = 1,
    Expired = 2,
    Revoked = 3,
    Denied = 4
}

public sealed record SupportAccessValidationResult(
    SupportAccessValidationState State,
    DateTimeOffset? GrantExpiresAt,
    string SafeReason)
{
    public bool Allowed => State == SupportAccessValidationState.Active;
}

/// <summary>
/// Identity-owned live validator. It proves an already-resolved support
/// context without issuing a context, permission, Tenant or organization scope.
/// </summary>
public interface ISupportAccessContextValidator
{
    ValueTask<SupportAccessValidationResult> ValidateAsync(
        FoundationRequestContext trustedRequestContext,
        string purpose,
        string permission,
        CancellationToken cancellationToken = default);
}

public enum SupportAccessSessionState
{
    Requested = 1,
    Approved = 2,
    Active = 3,
    Expired = 4,
    Revoked = 5,
    Rejected = 6
}

/// <summary>Evidence-only support session; it cannot mint Tenant authority.</summary>
public sealed class SupportAccessSession
{
    internal SupportAccessSession(
        Guid sessionId,
        Guid authenticationSessionId,
        TenantId tenantId,
        Guid actorId,
        SupportGrantReference grant,
        string purpose,
        string permission,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        SessionId = sessionId;
        AuthenticationSessionId = authenticationSessionId;
        TenantId = tenantId;
        ActorId = actorId;
        GrantId = grant.GrantId;
        CaseId = grant.CaseId;
        Purpose = purpose;
        Permission = permission;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        State = SupportAccessSessionState.Active;
        Version = 1;
    }

    public Guid SessionId { get; }

    /// <summary>Server session that authenticated the support actor.</summary>
    public Guid AuthenticationSessionId { get; }

    public TenantId TenantId { get; }

    public Guid ActorId { get; }

    public Guid GrantId { get; }

    public Guid CaseId { get; }

    public string Purpose { get; }

    public string Permission { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public SupportAccessSessionState State { get; internal set; }

    public long Version { get; internal set; }
}

public sealed record SupportAccessSessionResult(
    bool Succeeded,
    string SafeCode,
    SupportAccessSession? Session)
{
    public static SupportAccessSessionResult Success(SupportAccessSession session) =>
        new(true, "support_session_active", session);

    public static SupportAccessSessionResult Failure(string code) =>
        new(false, code, null);
}

/// <summary>
/// Bounded local support-session evidence store. It tracks a session only
/// after Identity has live-revalidated the exact case-bound grant. It never
/// creates a TenantContext and never expands the grant's scope or permissions.
/// </summary>
public sealed class SupportAccessSessionStore
{
    public const int MaximumLifetimeHours = 8;

    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, SupportAccessSession> sessions = [];
    private readonly ISupportAccessContextValidator validator;
    private readonly TimeProvider timeProvider;
    private readonly int capacity;

    public SupportAccessSessionStore(
        ISupportAccessContextValidator validator,
        TimeProvider? timeProvider = null,
        int capacity = 512)
    {
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public async ValueTask<SupportAccessSessionResult> OpenAsync(
        FoundationRequestContext trustedRequestContext,
        string purpose,
        string permission,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trustedRequestContext);
        cancellationToken.ThrowIfCancellationRequested();
        if (trustedRequestContext.SecurityProfile != FoundationSecurityProfile.SupportGrant
            || trustedRequestContext.TenantContext?.SupportGrant is not { } grant
            || trustedRequestContext.TenantContext.ActorId is not { } actorId
            || trustedRequestContext.TenantContext.TenantId == default
            || trustedRequestContext.ActorId != actorId
            || trustedRequestContext.SessionId is null
            || lifetime <= TimeSpan.Zero
            || lifetime > TimeSpan.FromHours(MaximumLifetimeHours))
        {
            return SupportAccessSessionResult.Failure("support_session_denied");
        }

        var validation = await validator.ValidateAsync(trustedRequestContext, purpose, permission, cancellationToken);
        if (!validation.Allowed || validation.GrantExpiresAt is not { } grantExpiresAt)
        {
            return SupportAccessSessionResult.Failure(validation.State switch
            {
                SupportAccessValidationState.Expired => "support_session_expired",
                SupportAccessValidationState.Revoked => "support_session_revoked",
                _ => "support_session_denied"
            });
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(lifetime) <= grantExpiresAt ? now.Add(lifetime) : grantExpiresAt;
        if (expiresAt <= now)
        {
            return SupportAccessSessionResult.Failure("support_session_expired");
        }

        lock (syncRoot)
        {
            if (sessions.Count >= capacity)
            {
                return SupportAccessSessionResult.Failure("support_session_unavailable");
            }

            var session = new SupportAccessSession(
                Guid.NewGuid(),
                trustedRequestContext.SessionId.Value,
                trustedRequestContext.TenantContext.TenantId,
                actorId,
                grant,
                Bounded(purpose, nameof(purpose)),
                Bounded(permission, nameof(permission)),
                now,
                expiresAt);
            sessions.Add(session.SessionId, session);
            return SupportAccessSessionResult.Success(session);
        }
    }

    public async ValueTask<SupportAccessSessionResult> RevalidateAsync(
        FoundationRequestContext trustedRequestContext,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trustedRequestContext);
        cancellationToken.ThrowIfCancellationRequested();
        SupportAccessSession? session;
        lock (syncRoot)
        {
            if (!sessions.TryGetValue(sessionId, out session)
                || trustedRequestContext.TenantContext?.TenantId != session.TenantId
                || trustedRequestContext.ActorId != session.ActorId
                || trustedRequestContext.SessionId != session.AuthenticationSessionId
                || trustedRequestContext.TenantContext.SupportGrant is not { } grant
                || grant.GrantId != session.GrantId
                || grant.CaseId != session.CaseId)
            {
                return SupportAccessSessionResult.Failure("support_session_denied");
            }

            if (session.State == SupportAccessSessionState.Revoked)
            {
                return SupportAccessSessionResult.Failure("support_session_revoked");
            }

            if (session.ExpiresAt <= timeProvider.GetUtcNow())
            {
                session.State = SupportAccessSessionState.Expired;
                session.Version++;
                return SupportAccessSessionResult.Failure("support_session_expired");
            }
        }

        var validation = await validator.ValidateAsync(trustedRequestContext, session.Purpose, session.Permission, cancellationToken);
        if (validation.Allowed)
        {
            return SupportAccessSessionResult.Success(session);
        }

        lock (syncRoot)
        {
            if (sessions.TryGetValue(sessionId, out var current))
            {
                current.State = validation.State == SupportAccessValidationState.Expired
                    ? SupportAccessSessionState.Expired
                    : SupportAccessSessionState.Revoked;
                current.Version++;
            }
        }

        return SupportAccessSessionResult.Failure(validation.State == SupportAccessValidationState.Expired
            ? "support_session_expired"
            : "support_session_revoked");
    }

    public bool Revoke(FoundationRequestContext trustedRequestContext, Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(trustedRequestContext);
        lock (syncRoot)
        {
            if (!sessions.TryGetValue(sessionId, out var session)
                || trustedRequestContext.TenantContext?.TenantId != session.TenantId
                || trustedRequestContext.ActorId != session.ActorId)
            {
                return false;
            }

            if (session.State is SupportAccessSessionState.Expired or SupportAccessSessionState.Revoked)
            {
                return false;
            }

            session.State = SupportAccessSessionState.Revoked;
            session.Version++;
            return true;
        }
    }

    internal int Count
    {
        get
        {
            lock (syncRoot)
            {
                return sessions.Count;
            }
        }
    }

    private static string Bounded(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Support-session value is required and bounded.", name);
        }

        return value.Trim();
    }
}
#pragma warning restore CS1591
