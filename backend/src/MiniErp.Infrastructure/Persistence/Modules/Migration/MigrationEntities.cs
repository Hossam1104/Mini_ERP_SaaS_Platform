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

    internal void Apply(MigrationRunRecord record)
    {
        if (record.RunId != RunId || record.TenantId != TenantId)
        {
            throw new InvalidOperationException("A migration run cannot change its identity or Tenant owner.");
        }

        Status = record.Status;
        UpdatedAt = record.UpdatedAt;
        Version = record.Version;
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
