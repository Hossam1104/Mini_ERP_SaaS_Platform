#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;

namespace MiniErp.App.BuildingBlocks.Owners;

public enum OwnerResourceKind
{
    Product = 1,
    Supplier = 2,
    Customer = 3,
    Currency = 4,
    Tax = 5,
    PaymentTerm = 6,
    UnitOfMeasure = 7
}

public enum OwnerBatchStatus
{
    Draft = 1,
    Validated = 2,
    Completed = 3,
    CompletedWithErrors = 4
}

public enum OwnerRowOutcome
{
    Rejected = 1,
    Quarantined = 2,
    Accepted = 3
}

public enum OwnerMutationDisposition
{
    Committed = 1,
    Updated = 2,
    Failed = 3,
    NotAttempted = 4
}

public sealed record OwnerImportSource(
    string SourceSystemCategory,
    string? SourceFileReference,
    string? BatchReference);

public sealed record OwnerImportRowInput(
    int RowNumber,
    IReadOnlyDictionary<string, string?> Fields);

public sealed record OwnerImportRequest(
    Guid BatchId,
    OwnerResourceKind ResourceKind,
    OwnerImportSource Source,
    string IdempotencyKey,
    string Fingerprint,
    IReadOnlyList<OwnerImportRowInput> Rows);

public sealed record OwnerBatchEvidence(
    Guid Id,
    OwnerBatchStatus Status,
    byte[] Version);

public sealed record OwnerDiagnosticEvidence(string Code);

public sealed record OwnerRowEvidence(
    int OriginalRowNumber,
    OwnerRowOutcome Outcome,
    OwnerMutationDisposition MutationDisposition,
    Guid? Id,
    Guid? ResultingResourceId,
    string? ResultingResourceCode,
    IReadOnlyList<OwnerDiagnosticEvidence> Diagnostics);

public sealed record OwnerEvidence(
    OwnerBatchEvidence Batch,
    IReadOnlyList<OwnerRowEvidence> Rows);

public sealed record OwnerOperationResult<T>(
    bool Succeeded,
    string Code,
    T? Value,
    int StatusCode = 200);

/// <summary>Internal owner execution seam, never a public REST contract.</summary>
public interface IOwnerExecutionGateway
{
    Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(
        FoundationRequestContext trustedContext,
        OwnerImportRequest request,
        CancellationToken cancellationToken = default);

    Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default);

    Task<OwnerEvidence?> ReadEvidenceAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        CancellationToken cancellationToken = default);
}

#pragma warning restore CS1591
