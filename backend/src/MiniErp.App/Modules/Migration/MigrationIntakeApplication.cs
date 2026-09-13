#pragma warning disable CS1591

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

/// <summary>Client-facing intake shape after the server has validated its references.</summary>
public sealed record MigrationIntakeRegistrationRequest(
    MigrationDefinitionReference Definition,
    MigrationSourceProfileReference SourceProfile,
    MigrationOperationKind Operation,
    Guid SourceObjectId);

/// <summary>Immutable private-source facts captured at intake time.</summary>
public sealed record MigrationSourceArtifactSnapshot(
    Guid ObjectId,
    TenantId TenantId,
    Guid? CompanyId,
    Guid? BranchId,
    Guid? WarehouseId,
    string Sha256,
    long Length,
    long ConcurrencyVersion);

/// <summary>Durable intake record; it contains metadata and no source bytes.</summary>
public sealed record MigrationIntakeRecord(
    MigrationRunRecord Run,
    MigrationOperationKind Operation,
    string FingerprintVersion,
    string RequestFingerprint,
    MigrationSourceArtifactSnapshot Source,
    DateTimeOffset CapturedAt,
    byte[] Version);

/// <summary>Atomic creation contract for the run, source snapshot and idempotency key.</summary>
public sealed record CreateMigrationIntakeCommand(
    MigrationRun Run,
    MigrationOperationKind Operation,
    MigrationIdempotencyKey IdempotencyKey,
    MigrationRequestFingerprint RequestFingerprint,
    string FingerprintVersion,
    MigrationSourceArtifactSnapshot Source);

/// <summary>
/// The single canonical request identity authority for Migration intake.
/// Fields are length-prefixed in a fixed order, so values cannot collide through
/// delimiter ambiguity or runtime serialization details.
/// </summary>
public static class MigrationIntakeFingerprint
{
    public const string Version = "migration-intake-v1";

    public static MigrationRequestFingerprint Compute(
        TenantContext tenantContext,
        MigrationDefinitionReference definition,
        MigrationSourceProfileReference sourceProfile,
        MigrationOperationKind operation,
        MigrationSourceArtifactSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(sourceProfile);
        ArgumentNullException.ThrowIfNull(source);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (source.TenantId != tenantContext.TenantId)
        {
            throw new ArgumentException("The source snapshot must belong to the trusted Tenant.", nameof(source));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, Version);
        Append(hash, tenantContext.TenantId.Value.ToString("D", CultureInfo.InvariantCulture));
        Append(hash, tenantContext.Scope?.Value);
        Append(hash, definition.DefinitionId);
        Append(hash, definition.Version);
        Append(hash, sourceProfile.ProfileId);
        Append(hash, sourceProfile.ProfileVersion);
        Append(hash, ((int)operation).ToString(CultureInfo.InvariantCulture));
        Append(hash, source.ObjectId.ToString("D", CultureInfo.InvariantCulture));
        Append(hash, source.TenantId.Value.ToString("D", CultureInfo.InvariantCulture));
        Append(hash, source.CompanyId?.ToString("D", CultureInfo.InvariantCulture));
        Append(hash, source.BranchId?.ToString("D", CultureInfo.InvariantCulture));
        Append(hash, source.WarehouseId?.ToString("D", CultureInfo.InvariantCulture));
        Append(hash, NormalizeSha256(source.Sha256));
        Append(hash, source.Length.ToString(CultureInfo.InvariantCulture));
        Append(hash, source.ConcurrencyVersion.ToString(CultureInfo.InvariantCulture));

        return new MigrationRequestFingerprint(Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static void Append(IncrementalHash hash, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        Span<byte> length = stackalloc byte[4];
        BitConverter.TryWriteBytes(length, bytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            length.Reverse();
        }

        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    internal static string NormalizeSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The source checksum is required.", nameof(value));
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("The source checksum is invalid.", nameof(value));
        }

        return normalized;
    }
}

/// <summary>Bounded orchestration for MESP-141 Slice 2 source intake.</summary>
public sealed class MigrationIntakeService
{
    internal const string OperationId = "migration.intake.create";
    internal const string EvidenceUnavailableCode = "migration_audit_evidence_unavailable";
    internal const string SourceScopeDeniedCode = "migration_source_scope_denied";

    private readonly IMigrationFoundationPersistence persistence;
    private readonly IPrivateObjectStorage privateStorage;
    private readonly ICurrentOrganizationScopeResolver currentScopeResolver;
    private readonly IFoundationAuditEvidenceSink auditSink;
    private readonly TimeProvider timeProvider;

    public MigrationIntakeService(
        IMigrationFoundationPersistence persistence,
        IPrivateObjectStorage privateStorage,
        ICurrentOrganizationScopeResolver currentScopeResolver,
        IFoundationAuditEvidenceSink auditSink,
        TimeProvider? timeProvider = null)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.privateStorage = privateStorage ?? throw new ArgumentNullException(nameof(privateStorage));
        this.currentScopeResolver = currentScopeResolver ?? throw new ArgumentNullException(nameof(currentScopeResolver));
        this.auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MigrationOperationResult<MigrationIntakeRecord>> RegisterAsync(
        FoundationRequestContext requestContext,
        MigrationIntakeRegistrationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(request);

        if (requestContext.TenantContext is not { } tenantContext)
        {
            return MigrationOperationResult<MigrationIntakeRecord>.Rejected("migration_tenant_context_required");
        }

        MigrationIdempotencyKey key;
        try
        {
            key = new MigrationIdempotencyKey(idempotencyKey);
            if (!Enum.IsDefined(request.Operation) || request.SourceObjectId == Guid.Empty)
            {
                return MigrationOperationResult<MigrationIntakeRecord>.Rejected("migration_intake_request_invalid");
            }
        }
        catch (ArgumentException)
        {
            return MigrationOperationResult<MigrationIntakeRecord>.Rejected("migration_intake_request_invalid");
        }

        var sourceRead = await privateStorage.ReadAsync(tenantContext, request.SourceObjectId, cancellationToken);
        if (!sourceRead.Allowed || sourceRead.Metadata is not { } metadata)
        {
            var refusal = SourceRefusal(sourceRead.Outcome);
            return await RecordSourceRefusalAsync(requestContext, key, refusal.Code, refusal.Reason, cancellationToken);
        }

        var currentScope = currentScopeResolver.ResolveCurrent(tenantContext);
        if (!currentScope.Allowed
            || currentScope.Scope is not { } authorizedScope
            || !authorizedScope.ContainsAuthorizedDescendant(metadata.Scope))
        {
            return await RecordSourceRefusalAsync(
                requestContext,
                key,
                SourceScopeDeniedCode,
                FoundationAuditReason.AuthorizationDenied,
                cancellationToken);
        }

        MigrationSourceArtifactSnapshot source;
        MigrationRequestFingerprint fingerprint;
        MigrationRun run;
        try
        {
            source = new MigrationSourceArtifactSnapshot(
                metadata.ObjectId,
                metadata.TenantId,
                metadata.Scope.CompanyId,
                metadata.Scope.BranchId,
                metadata.Scope.WarehouseId,
                MigrationIntakeFingerprint.NormalizeSha256(metadata.Sha256),
                metadata.Length,
                metadata.ConcurrencyVersion);
            fingerprint = MigrationIntakeFingerprint.Compute(
                tenantContext,
                request.Definition,
                request.SourceProfile,
                request.Operation,
                source);
            run = MigrationRun.Create(tenantContext, request.Definition, request.SourceProfile, timeProvider);
        }
        catch (ArgumentException)
        {
            return await RecordSourceRefusalAsync(
                requestContext,
                key,
                "migration_source_metadata_invalid",
                FoundationAuditReason.ValidationFailed,
                cancellationToken);
        }

        var saved = await persistence.CreateIntakeAsync(
            tenantContext,
            new CreateMigrationIntakeCommand(run, request.Operation, key, fingerprint, MigrationIntakeFingerprint.Version, source),
            cancellationToken);

        var persistedRun = saved.Value?.Run is { } record
            ? MigrationRun.Rehydrate(record)
            : run;
        var auditMetadata = MigrationAuditMetadata.Create()
            .With("operation", request.Operation.ToString())
            .With("to", persistedRun.Status.ToString());
        if (saved.Outcome == MigrationPersistenceOutcome.Replayed)
        {
            auditMetadata.With("replayed", "true");
        }

        if (!await AppendAsync(requestContext, persistedRun, saved, key, auditMetadata, cancellationToken))
        {
            return saved.Outcome is MigrationPersistenceOutcome.Succeeded
                or MigrationPersistenceOutcome.Replayed
                or MigrationPersistenceOutcome.UnknownOutcome
                ? MigrationOperationResult<MigrationIntakeRecord>.Unknown(EvidenceUnavailableCode)
                : MigrationOperationResult<MigrationIntakeRecord>.Failure(EvidenceUnavailableCode, safeToRetry: true);
        }

        return saved.Outcome switch
        {
            MigrationPersistenceOutcome.Succeeded => MigrationOperationResult<MigrationIntakeRecord>.Success(saved.Value!),
            MigrationPersistenceOutcome.Replayed => MigrationOperationResult<MigrationIntakeRecord>.Replay(saved.Value!),
            MigrationPersistenceOutcome.Conflict => MigrationOperationResult<MigrationIntakeRecord>.Rejected(saved.Code),
            MigrationPersistenceOutcome.NotFound => MigrationOperationResult<MigrationIntakeRecord>.Rejected(saved.Code),
            MigrationPersistenceOutcome.InvalidReference => MigrationOperationResult<MigrationIntakeRecord>.Rejected(saved.Code),
            MigrationPersistenceOutcome.UnknownOutcome => MigrationOperationResult<MigrationIntakeRecord>.Unknown(saved.Code),
            _ => MigrationOperationResult<MigrationIntakeRecord>.Failure(saved.Code)
        };
    }

    private async Task<MigrationOperationResult<MigrationIntakeRecord>> RecordSourceRefusalAsync(
        FoundationRequestContext requestContext,
        MigrationIdempotencyKey key,
        string code,
        FoundationAuditReason reason,
        CancellationToken cancellationToken)
    {
        if (requestContext.TenantContext?.CorrelationId is not { } correlationId)
        {
            return MigrationOperationResult<MigrationIntakeRecord>.Failure(EvidenceUnavailableCode, safeToRetry: true);
        }

        try
        {
            var evidence = FoundationAuditEvidenceFactory.Create(
                requestContext,
                OperationId,
                correlationId.Value,
                FoundationAuditDecision.Denied,
                reason,
                idempotencyKey: key.Value,
                operationVersion: MigrationIntakeFingerprint.Version,
                occurredAt: timeProvider.GetUtcNow(),
                source: "migration",
                targetType: "migration-intake",
                changeSummary: $"outcome={code}");
            await auditSink.AppendAsync(evidence, cancellationToken);
        }
        catch (FoundationAuditAppendException)
        {
            return MigrationOperationResult<MigrationIntakeRecord>.Failure(EvidenceUnavailableCode, safeToRetry: true);
        }
        catch (ArgumentException)
        {
            return MigrationOperationResult<MigrationIntakeRecord>.Failure(EvidenceUnavailableCode, safeToRetry: true);
        }

        return MigrationOperationResult<MigrationIntakeRecord>.Rejected(code);
    }

    private async Task<bool> AppendAsync(
        FoundationRequestContext requestContext,
        MigrationRun run,
        MigrationPersistenceResult<MigrationIntakeRecord> saved,
        MigrationIdempotencyKey key,
        MigrationAuditMetadata metadata,
        CancellationToken cancellationToken)
    {
        var (decision, reason) = saved.Outcome switch
        {
            MigrationPersistenceOutcome.Succeeded or MigrationPersistenceOutcome.Replayed
                => (FoundationAuditDecision.Allowed, FoundationAuditReason.Allowed),
            MigrationPersistenceOutcome.Conflict => (FoundationAuditDecision.Conflict, FoundationAuditReason.IdempotencyConflict),
            MigrationPersistenceOutcome.NotFound or MigrationPersistenceOutcome.InvalidReference
                => (FoundationAuditDecision.Denied, FoundationAuditReason.ValidationFailed),
            _ => (FoundationAuditDecision.Conflict, FoundationAuditReason.InternalFailure)
        };

        try
        {
            var evidence = MigrationAuditEvidenceFactory.Create(
                requestContext,
                run,
                OperationId,
                decision,
                reason,
                saved.Code,
                idempotencyKey: key,
                safeMetadata: metadata,
                occurredAt: timeProvider.GetUtcNow());
            await auditSink.AppendAsync(evidence, cancellationToken);
            return true;
        }
        catch (FoundationAuditAppendException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static (string Code, FoundationAuditReason Reason) SourceRefusal(PrivateFileAccessOutcome outcome) => outcome switch
    {
        PrivateFileAccessOutcome.NotFound => ("migration_source_not_found", FoundationAuditReason.NotFound),
        PrivateFileAccessOutcome.Expired => ("migration_source_expired", FoundationAuditReason.ValidationFailed),
        PrivateFileAccessOutcome.Disposed => ("migration_source_disposed", FoundationAuditReason.ValidationFailed),
        PrivateFileAccessOutcome.ChecksumFailed => ("migration_source_checksum_failed", FoundationAuditReason.ValidationFailed),
        PrivateFileAccessOutcome.SafetyBlocked => ("migration_source_safety_blocked", FoundationAuditReason.ValidationFailed),
        PrivateFileAccessOutcome.ConcurrencyConflict => ("migration_source_concurrency_conflict", FoundationAuditReason.ConcurrencyConflict),
        _ => ("migration_source_unavailable", FoundationAuditReason.InternalFailure)
    };
}

#pragma warning restore CS1591
