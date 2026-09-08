#pragma warning disable CS1591

using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Identity;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.BuildingBlocks.Rest;

/// <summary>
/// A server-derived request context. Client headers and request payloads cannot
/// construct this value.
/// </summary>
public sealed class FoundationRequestContext
{
    private FoundationRequestContext(
        FoundationSecurityProfile securityProfile,
        Guid? actorId,
        Guid? sessionId,
        TenantContext? tenantContext,
        PlatformGovernanceContext? platformGovernanceContext,
        string permission,
        string lifecycleState)
    {
        if (securityProfile is FoundationSecurityProfile.Anonymous)
        {
            if (actorId.HasValue || sessionId.HasValue || tenantContext is not null || platformGovernanceContext is not null)
            {
                throw new ArgumentException("Anonymous context cannot contain server authorization facts.", nameof(securityProfile));
            }
        }
        else
        {
            if (!actorId.HasValue || actorId.Value == Guid.Empty || !sessionId.HasValue || sessionId.Value == Guid.Empty)
            {
                throw new ArgumentException("Protected context requires a server actor and session.", nameof(actorId));
            }

            var hasTenant = tenantContext is not null;
            var hasPlatform = platformGovernanceContext is not null;
            if (securityProfile is FoundationSecurityProfile.AuthenticatedSession)
            {
                if (hasTenant || hasPlatform)
                {
                    throw new ArgumentException("An authenticated session context cannot contain a Tenant or platform path.", nameof(tenantContext));
                }
            }
            else if (hasTenant == hasPlatform)
            {
                throw new ArgumentException("Protected context requires exactly one Tenant or platform path.", nameof(tenantContext));
            }

            if (securityProfile is FoundationSecurityProfile.PlatformGovernanceContext && !hasPlatform)
            {
                throw new ArgumentException("Platform profile requires PlatformGovernanceContext.", nameof(platformGovernanceContext));
            }

            if (securityProfile is FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant
                && !hasTenant)
            {
                throw new ArgumentException("Tenant profile requires TenantContext.", nameof(tenantContext));
            }
        }

        if (securityProfile is not FoundationSecurityProfile.Anonymous && string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("Protected context requires a permission.", nameof(permission));
        }

        if (string.IsNullOrWhiteSpace(lifecycleState))
        {
            throw new ArgumentException("Lifecycle state must not be empty.", nameof(lifecycleState));
        }

        SecurityProfile = securityProfile;
        ActorId = actorId;
        SessionId = sessionId;
        TenantContext = tenantContext;
        PlatformGovernanceContext = platformGovernanceContext;
        Permission = permission.Trim();
        LifecycleState = lifecycleState.Trim();
    }

    public FoundationSecurityProfile SecurityProfile { get; }

    public Guid? ActorId { get; }

    public Guid? SessionId { get; }

    public TenantContext? TenantContext { get; }

    public PlatformGovernanceContext? PlatformGovernanceContext { get; }

    public string Permission { get; }

    public string LifecycleState { get; }

    internal static FoundationRequestContext Unauthenticated() => new(
        FoundationSecurityProfile.Anonymous,
        actorId: null,
        sessionId: null,
        tenantContext: null,
        platformGovernanceContext: null,
        permission: string.Empty,
        lifecycleState: "Unauthenticated");

    internal static FoundationRequestContext ForAuthenticatedSession(Guid actorId, Guid sessionId, string permission = "authenticated.session", string lifecycleState = "Active") => new(
        FoundationSecurityProfile.AuthenticatedSession,
        actorId,
        sessionId,
        tenantContext: null,
        platformGovernanceContext: null,
        permission,
        lifecycleState);

    internal static FoundationRequestContext ForTenant(
        Guid actorId,
        Guid sessionId,
        TenantContext tenantContext,
        string permission,
        string lifecycleState = "Active")
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        var profile = tenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationSecurityProfile.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationSecurityProfile.SupportGrant,
            _ => throw new ArgumentOutOfRangeException(nameof(tenantContext))
        };

        return new FoundationRequestContext(
            profile,
            actorId,
            sessionId,
            tenantContext,
            platformGovernanceContext: null,
            permission,
            lifecycleState);
    }

    internal static FoundationRequestContext ForPlatform(
        Guid actorId,
        Guid sessionId,
        PlatformGovernanceContext governanceContext,
        string permission,
        string lifecycleState = "Active")
    {
        ArgumentNullException.ThrowIfNull(governanceContext);
        return new FoundationRequestContext(
            FoundationSecurityProfile.PlatformGovernanceContext,
            actorId,
            sessionId,
            tenantContext: null,
            governanceContext,
            permission,
            lifecycleState);
    }
}

/// <summary>
/// Server-side resolver seam. Implementations are responsible for resolving
/// validated authentication and Tenant facts; request identifiers are not
/// authority.
/// </summary>
public interface ITrustedRequestContextResolver
{
    ValueTask<FoundationRequestContext> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default resolver for the Foundation host. It deliberately has no implicit
/// client-to-Tenant mapping and returns an anonymous context until a validated
/// authentication adapter supplies server facts.
/// </summary>
public sealed class DefaultTrustedRequestContextResolver : ITrustedRequestContextResolver
{
    private readonly IFoundationIdentityHost identityHost;
    private readonly ITenantEntryAuthority? tenantEntryAuthority;

    public DefaultTrustedRequestContextResolver(
        IFoundationIdentityHost identityHost,
        ITenantEntryAuthority? tenantEntryAuthority = null)
    {
        this.identityHost = identityHost ?? throw new ArgumentNullException(nameof(identityHost));
        this.tenantEntryAuthority = tenantEntryAuthority;
    }

    public ValueTask<FoundationRequestContext> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var correlationId = httpContext.Items.TryGetValue("MiniErp.Foundation.CorrelationId", out var rawCorrelation)
            && rawCorrelation is string correlation
            ? correlation
            : FoundationCorrelation.Resolve(httpContext.Request);
        var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<FoundationOperationMetadata>();
        if (metadata is null || !FoundationOperationCatalog.TryGet(metadata.OperationId, out var descriptor)
            || descriptor.Visibility != FoundationOperationVisibility.Public
            || !ReferenceEquals(metadata.Descriptor, descriptor) && metadata.Descriptor != descriptor)
        {
            // An operation without an approved descriptor is not allowed to
            // manufacture a permission or fall back to context-read semantics.
            return ValueTask.FromResult(FoundationRequestContext.Unauthenticated());
        }

        // Host routing is a candidate hint only. The entry authority can select
        // an exact server-side membership/platform path, but it never accepts
        // Tenant headers or request payloads as authorization.
        // A common host is a routing entry point. Do not silently activate its
        // single Tenant membership merely because a legacy business endpoint
        // was called; the explicit entry response owns that transition. A
        // Tenant-specific host still enforces its exact candidate on every
        // protected request.
        tenantEntryAuthority?.Prepare(httpContext.User, httpContext.Request.Host.Value, activateCommonHost: false);
        return ValueTask.FromResult(identityHost.ResolveContext(httpContext.User, correlationId, descriptor));
    }
}

/// <summary>
/// Correlation validation shared by the API boundary and application evidence.
/// </summary>
public static class FoundationCorrelation
{
    public const string HeaderName = "X-Correlation-Id";
    public const int MaximumLength = 128;

    public static string Resolve(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = request.Headers[HeaderName];
        var candidate = values.Count == 1 ? values[0] : null;
        return values.Count == 1 && IsValid(candidate)
            ? candidate!.Trim()
            : Guid.NewGuid().ToString("N");
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsControl(character) || character is '\r' or '\n' or '\u2028' or '\u2029')
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Safe application result used by the API adapter. The error values are
/// deliberately allow-listed and contain no provider or target details.
/// </summary>
public sealed record FoundationOperationResult<T>(
    bool Succeeded,
    int StatusCode,
    string Code,
    string Title,
    string Detail,
    T? Value,
    string OperationId,
    string CorrelationId,
    bool Replayed = false)
{
    public static FoundationOperationResult<T> Success(
        T value,
        string operationId,
        string correlationId,
        bool replayed = false) => new(
        true,
        StatusCodes.Status200OK,
        "success",
        "Success",
        "The operation completed.",
        value,
        operationId,
        correlationId,
        replayed);

    public static FoundationOperationResult<T> Failure(
        int statusCode,
        string code,
        string title,
        string detail,
        string operationId,
        string correlationId) => new(
        false,
        statusCode,
        code,
        title,
        detail,
        default,
        operationId,
        correlationId);
}


public sealed record FoundationIdempotencyBinding(
    Guid ActorId,
    Guid? TenantId,
    FoundationSecurityProfile SecurityProfile,
    string OperationId);

public enum FoundationIdempotencyDecision
{
    New = 1,
    Replay = 2,
    RequestConflict = 3,
    InProgress = 4
}

public sealed record FoundationIdempotencyCheck(
    FoundationIdempotencyDecision Decision,
    FoundationIdempotencyResponse? Response = null);

/// <summary>
/// Bounded local idempotency seam. Durable production storage is intentionally
/// deferred; callers must not treat this implementation as a production store.
/// </summary>
public sealed class LocalFoundationIdempotencyStore
{
    private sealed record Entry(
        FoundationIdempotencyBinding Binding,
        string Fingerprint,
        DateTimeOffset ExpiresAt,
        FoundationIdempotencyResponse? Response);

    private readonly object syncRoot = new();
    private readonly Dictionary<(FoundationIdempotencyBinding Binding, string Key), Entry> entries = [];

    public FoundationIdempotencyCheck Begin(
        string key,
        FoundationIdempotencyBinding binding,
        string fingerprint,
        DateTimeOffset now,
        TimeSpan validity)
    {
        if (!FoundationCorrelation.IsValid(key))
        {
            return new FoundationIdempotencyCheck(FoundationIdempotencyDecision.RequestConflict);
        }

        lock (syncRoot)
        {
            RemoveExpiredUnsafe(now);
            var normalizedKey = key.Trim();
            var compositeKey = (binding, normalizedKey);
            if (!entries.TryGetValue(compositeKey, out var existing))
            {
                entries[compositeKey] = new Entry(binding, fingerprint, now.Add(validity), Response: null);
                return new FoundationIdempotencyCheck(FoundationIdempotencyDecision.New);
            }

            if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return new FoundationIdempotencyCheck(FoundationIdempotencyDecision.RequestConflict);
            }

            return existing.Response is null
                ? new FoundationIdempotencyCheck(FoundationIdempotencyDecision.InProgress)
                : new FoundationIdempotencyCheck(FoundationIdempotencyDecision.Replay, existing.Response);
        }
    }

    public void Commit(string key, FoundationIdempotencyBinding binding, FoundationIdempotencyResponse response)
    {
        lock (syncRoot)
        {
            var compositeKey = (binding, key.Trim());
            if (entries.TryGetValue(compositeKey, out var existing))
            {
                entries[compositeKey] = existing with { Response = response };
            }
        }
    }

    public void Release(string key, FoundationIdempotencyBinding binding)
    {
        lock (syncRoot)
        {
            var compositeKey = (binding, key.Trim());
            if (entries.TryGetValue(compositeKey, out var existing) && existing.Response is null)
            {
                entries.Remove(compositeKey);
            }
        }
    }

    private void RemoveExpiredUnsafe(DateTimeOffset now)
    {
        foreach (var key in entries
                     .Where(pair => pair.Value.ExpiresAt <= now)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            entries.Remove(key);
        }
    }
}

/// <summary>
/// Tenant-bound local version state for the non-business Foundation probe.
/// </summary>
public sealed class LocalFoundationProbeStore
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, long> versions = [];

    public long CurrentVersion(TenantId tenantId)
    {
        lock (syncRoot)
        {
            return versions.TryGetValue(tenantId.Value, out var version) ? version : 1;
        }
    }

    public bool TryAdvance(TenantId tenantId, long expectedVersion, out long newVersion)
    {
        lock (syncRoot)
        {
            var current = versions.TryGetValue(tenantId.Value, out var version) ? version : 1;
            if (current != expectedVersion)
            {
                newVersion = current;
                return false;
            }

            newVersion = current + 1;
            versions[tenantId.Value] = newVersion;
            return true;
        }
    }

    /// <summary>Clears local version state for an isolated test host.</summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            versions.Clear();
        }
    }
}

/// <summary>
/// Narrow resource directory used solely to prove missing/foreign-target
/// equivalence. Future business modules own their own resource repositories.
/// </summary>
public interface IFoundationTargetDirectory
{
    bool TryGetTenant(Guid targetId, out TenantId tenantId);
}

public sealed class EmptyFoundationTargetDirectory : IFoundationTargetDirectory
{
    public bool TryGetTenant(Guid targetId, out TenantId tenantId)
    {
        tenantId = default;
        return false;
    }
}

/// <summary>
/// Application operations backing the public Foundation endpoints.
/// </summary>
public sealed class FoundationRestApplication
{
    private static readonly TimeSpan IdempotencyValidity = TimeSpan.FromMinutes(10);
    private readonly LocalFoundationIdempotencyStore idempotencyStore;
    private readonly LocalFoundationProbeStore probeStore;
    private readonly IFoundationTargetDirectory targetDirectory;
    private readonly TimeProvider timeProvider;
    private readonly FoundationAuditCoordinator auditCoordinator;

    public FoundationRestApplication(
        FoundationAuditCoordinator auditCoordinator,
        LocalFoundationIdempotencyStore? idempotencyStore = null,
        LocalFoundationProbeStore? probeStore = null,
        IFoundationTargetDirectory? targetDirectory = null,
        TimeProvider? timeProvider = null)
    {
        this.auditCoordinator = auditCoordinator ?? throw new ArgumentNullException(nameof(auditCoordinator));
        this.idempotencyStore = idempotencyStore ?? new LocalFoundationIdempotencyStore();
        this.probeStore = probeStore ?? new LocalFoundationProbeStore();
        this.targetDirectory = targetDirectory ?? new EmptyFoundationTargetDirectory();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public FoundationOperationResult<FoundationContextResponse> ReadTenantContext(
        FoundationRequestContext context,
        string correlationId) => ReadContext(
        context,
        FoundationSecurityProfile.OrdinaryMembership,
        FoundationOperationCatalog.GetRequired("foundation.tenant-context.read").ExactPermissionCode!,
        "foundation.tenant-context.read",
        correlationId);

    public FoundationOperationResult<FoundationContextResponse> ReadSupportContext(
        FoundationRequestContext context,
        string correlationId) => ReadContext(
        context,
        FoundationSecurityProfile.SupportGrant,
        FoundationOperationCatalog.GetRequired("foundation.support-context.read").ExactPermissionCode!,
        "foundation.support-context.read",
        correlationId);

    /// <summary>
    /// Shipping support-context composition. Identity's selected
    /// SupportGrant and authenticated session remain the authority; this
    /// operation only records evidence after the normal context validation has
    /// succeeded. No process-local support session is created.
    /// </summary>
    public async Task<FoundationOperationResult<FoundationContextResponse>> ReadSupportContextAsync(
        FoundationRequestContext context,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        const string operationId = "foundation.support-context.read";
        var result = ReadContext(
            context,
            FoundationSecurityProfile.SupportGrant,
            FoundationOperationCatalog.GetRequired(operationId).ExactPermissionCode!,
            operationId,
            correlationId);
        if (!result.Succeeded || context.TenantContext?.SupportGrant is not { } grant)
        {
            return result;
        }

        var evidence = await auditCoordinator.RecordAsync(
            context,
            operationId,
            correlationId,
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            supportPurpose: "support-context-read",
            source: "identity-support-context",
            targetType: "support-grant",
            targetReference: grant.GrantId.ToString("N"),
            changeSummary: "support context resolved",
            cancellationToken: cancellationToken);
        return evidence.Succeeded
            ? result
            : Failure<FoundationContextResponse>(
                StatusCodes.Status503ServiceUnavailable,
                "audit_evidence_unavailable",
                "Operation unavailable",
                "The support context could not be evidenced.",
                operationId,
                correlationId);
    }

    public FoundationOperationResult<FoundationContextResponse> ReadPlatformContext(
        FoundationRequestContext context,
        string correlationId) => ReadContext(
        context,
        FoundationSecurityProfile.PlatformGovernanceContext,
        FoundationOperationCatalog.GetRequired("foundation.platform-context.read").ExactPermissionCode!,
        "foundation.platform-context.read",
        correlationId);

    public FoundationOperationResult<FoundationTargetResponse> ReadTarget(
        FoundationRequestContext context,
        Guid targetId,
        string correlationId)
    {
        const string operationId = "foundation.target.read";
        var profileFailure = ValidateContext<FoundationTargetResponse>(
            context,
            FoundationSecurityProfile.OrdinaryMembership,
            FoundationOperationCatalog.GetRequired(operationId).ExactPermissionCode!,
            operationId,
            correlationId);
        if (profileFailure is not null)
        {
            return profileFailure;
        }

        if (targetId == Guid.Empty
            || context.TenantContext is null
            || !targetDirectory.TryGetTenant(targetId, out var owner)
            || owner != context.TenantContext.TenantId)
        {
            return Failure<FoundationTargetResponse>(
                StatusCodes.Status404NotFound,
                "resource_not_found",
                "Resource not found",
                "The requested resource is not available.",
                operationId,
                correlationId);
        }

        return FoundationOperationResult<FoundationTargetResponse>.Success(
            new FoundationTargetResponse(operationId, correlationId, "available", targetId.ToString("D")),
            operationId,
            correlationId);
    }

    public async Task<FoundationOperationResult<FoundationWriteResponse>> WriteProbeAsync(
        FoundationRequestContext context,
        FoundationWriteRequest request,
        string? idempotencyKey,
        string? ifMatch,
        bool antiforgeryValidated,
        string correlationId)
    {
        const string operationId = "foundation.probe.write";
        var profileFailure = ValidateContext<FoundationWriteResponse>(
            context,
            FoundationSecurityProfile.OrdinaryMembership,
            FoundationOperationCatalog.GetRequired(operationId).ExactPermissionCode!,
            operationId,
            correlationId);
        if (profileFailure is not null)
        {
            return profileFailure;
        }

        if (!antiforgeryValidated)
        {
            return Failure<FoundationWriteResponse>(
                StatusCodes.Status403Forbidden,
                "antiforgery_failed",
                "Antiforgery validation failed",
                "The request could not be validated.",
                operationId,
                correlationId);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Value) || request.Value.Trim().Length > 64)
        {
            return Failure<FoundationWriteResponse>(
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "Validation failed",
                "The request is invalid.",
                operationId,
                correlationId);
        }

        if (request.TargetId is { } targetId
            && (targetId == Guid.Empty
                || context.TenantContext is null
                || !targetDirectory.TryGetTenant(targetId, out var owner)
                || owner != context.TenantContext.TenantId))
        {
            return Failure<FoundationWriteResponse>(
                StatusCodes.Status404NotFound,
                "resource_not_found",
                "Resource not found",
                "The requested resource is not available.",
                operationId,
                correlationId);
        }

        if (!long.TryParse(Unquote(ifMatch), out var expectedVersion) || expectedVersion < 1)
        {
            return Failure<FoundationWriteResponse>(
                StatusCodes.Status428PreconditionRequired,
                "version_required",
                "Version required",
                "A current resource version is required.",
                operationId,
                correlationId);
        }

        if (!FoundationCorrelation.IsValid(idempotencyKey))
        {
            return Failure<FoundationWriteResponse>(
                StatusCodes.Status400BadRequest,
                "idempotency_key_invalid",
                "Invalid idempotency key",
                "The request is invalid.",
                operationId,
                correlationId);
        }

        var tenantId = context.TenantContext!.TenantId;
        var binding = new FoundationIdempotencyBinding(
            context.ActorId!.Value,
            tenantId.Value,
            context.SecurityProfile,
            operationId);
        var fingerprint = BuildFingerprint(request, expectedVersion);
        var check = idempotencyStore.Begin(
            idempotencyKey!,
            binding,
            fingerprint,
            timeProvider.GetUtcNow(),
            IdempotencyValidity);

        switch (check.Decision)
        {
            case FoundationIdempotencyDecision.Replay when check.Response?.WriteResponse is { } original:
                return FoundationOperationResult<FoundationWriteResponse>.Success(
                    original with { Replayed = true },
                    operationId,
                    correlationId,
                    replayed: true);
            case FoundationIdempotencyDecision.Replay:
                return Failure<FoundationWriteResponse>(
                    StatusCodes.Status409Conflict,
                    "idempotency_conflict",
                    "Idempotency conflict",
                    "The stored idempotency response is unavailable.",
                    operationId,
                    correlationId);
            case FoundationIdempotencyDecision.RequestConflict:
            case FoundationIdempotencyDecision.InProgress:
                return Failure<FoundationWriteResponse>(
                    StatusCodes.Status409Conflict,
                    "idempotency_conflict",
                    "Idempotency conflict",
                    "The request cannot be replayed with different or incomplete input.",
                    operationId,
                    correlationId);
            case FoundationIdempotencyDecision.New:
                break;
            default:
                idempotencyStore.Release(idempotencyKey!, binding);
                return Failure<FoundationWriteResponse>(
                    StatusCodes.Status409Conflict,
                    "idempotency_conflict",
                    "Idempotency conflict",
                    "The idempotency decision is not recognized.",
                    operationId,
                    correlationId);
        }

        var committed = false;
        try
        {
            async Task<(bool Advanced, long Version)> AdvanceAsync()
            {
                var advanced = probeStore.TryAdvance(tenantId, expectedVersion, out var version);
                return await Task.FromResult((advanced, version));
            }

            var execution = await auditCoordinator.ExecuteProtectedAsync(
                context,
                operationId,
                correlationId,
                FoundationAuditReason.Allowed,
                AdvanceAsync,
                idempotencyKey,
                expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (execution.Succeeded && execution.Value is { Advanced: false } stale)
            {
                return Failure<FoundationWriteResponse>(
                    StatusCodes.Status409Conflict,
                    "concurrency_conflict",
                    "Concurrency conflict",
                    "The resource version is stale.",
                    operationId,
                    correlationId);
            }

            if (!execution.Succeeded || execution.Value is not { Advanced: true } advance)
            {
                return Failure<FoundationWriteResponse>(
                    execution.Code == "concurrency_conflict" ? StatusCodes.Status409Conflict : StatusCodes.Status503ServiceUnavailable,
                    execution.Code == "concurrency_conflict" ? "concurrency_conflict" : "audit_unavailable",
                    execution.Code == "concurrency_conflict" ? "Concurrency conflict" : "Operation unavailable",
                    execution.Code == "concurrency_conflict" ? "The resource version is stale." : "The operation could not be completed.",
                    operationId,
                    correlationId);
            }

            var response = new FoundationWriteResponse(
                operationId,
                correlationId,
                "accepted",
                advance.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Replayed: false);
            idempotencyStore.Commit(idempotencyKey!, binding, FoundationIdempotencyResponse.ForWrite(response));
            committed = true;
            return FoundationOperationResult<FoundationWriteResponse>.Success(response, operationId, correlationId);
        }
        catch
        {
            return Failure<FoundationWriteResponse>(
                StatusCodes.Status503ServiceUnavailable,
                "audit_unavailable",
                "Operation unavailable",
                "The operation could not be completed.",
                operationId,
                correlationId);
        }
        finally
        {
            if (!committed)
            {
                idempotencyStore.Release(idempotencyKey!, binding);
            }
        }
    }

    private FoundationOperationResult<FoundationContextResponse> ReadContext(
        FoundationRequestContext context,
        FoundationSecurityProfile expectedProfile,
        string permission,
        string operationId,
        string correlationId)
    {
        var profileFailure = ValidateContext<FoundationContextResponse>(
            context,
            expectedProfile,
            permission,
            operationId,
            correlationId);
        if (profileFailure is not null)
        {
            return profileFailure;
        }

        var tenant = context.TenantContext;
        var platform = context.PlatformGovernanceContext;
        return FoundationOperationResult<FoundationContextResponse>.Success(
            new FoundationContextResponse(
                operationId,
                correlationId,
                tenant?.AuthorizationPath.ToString() ?? "PlatformGovernanceContext",
                context.LifecycleState,
                context.Permission,
                tenant?.TenantId.Value.ToString("D"),
                tenant?.Scope?.Value,
                platform is not null),
            operationId,
            correlationId);
    }

    private static FoundationOperationResult<T>? ValidateContext<T>(
        FoundationRequestContext context,
        FoundationSecurityProfile expectedProfile,
        string permission,
        string operationId,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.SecurityProfile == FoundationSecurityProfile.Anonymous)
        {
            return Failure<T>(
                StatusCodes.Status401Unauthorized,
                "authentication_failed",
                "Authentication required",
                "Authentication is required.",
                operationId,
                correlationId);
        }

        if (context.SecurityProfile != expectedProfile)
        {
            return Failure<T>(
                StatusCodes.Status403Forbidden,
                "access_denied",
                "Access denied",
                "The operation is not available for this security context.",
                operationId,
                correlationId);
        }

        if (!string.Equals(context.Permission, permission, StringComparison.Ordinal))
        {
            return Failure<T>(
                StatusCodes.Status403Forbidden,
                "permission_denied",
                "Access denied",
                "The operation is not available for this permission.",
                operationId,
                correlationId);
        }

        if (!string.Equals(context.LifecycleState, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Failure<T>(
                StatusCodes.Status403Forbidden,
                "lifecycle_denied",
                "Access denied",
                "The operation is not available in the current lifecycle state.",
                operationId,
                correlationId);
        }

        if (expectedProfile is FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant
            && context.TenantContext is null)
        {
            return Failure<T>(
                StatusCodes.Status403Forbidden,
                "tenant_context_failed",
                "Tenant context unavailable",
                "A valid Tenant context is required.",
                operationId,
                correlationId);
        }

        if (expectedProfile == FoundationSecurityProfile.PlatformGovernanceContext
            && context.PlatformGovernanceContext is null)
        {
            return Failure<T>(
                StatusCodes.Status403Forbidden,
                "platform_context_failed",
                "Platform context unavailable",
                "A valid platform governance context is required.",
                operationId,
                correlationId);
        }

        return null;
    }

    private static string BuildFingerprint(FoundationWriteRequest request, long expectedVersion) =>
        $"{request.Value!.Trim().ToUpperInvariant()}|{request.TargetId?.ToString("N") ?? "none"}|v{expectedVersion}";

    private static string? Unquote(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is { Length: >= 2 } && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1]
            : trimmed;
    }

    private static FoundationOperationResult<T> Failure<T>(
        int statusCode,
        string code,
        string title,
        string detail,
        string operationId,
        string correlationId) => FoundationOperationResult<T>.Failure(
        statusCode,
        code,
        title,
        detail,
        operationId,
        correlationId);
}

#pragma warning restore CS1591
