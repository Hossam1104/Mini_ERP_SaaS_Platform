#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record GoodsReceiptOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static GoodsReceiptOperationResult<T> Success(T value) => new(true, "succeeded", value);

    public static GoodsReceiptOperationResult<T> Failure(string code) => new(false, code, default);
}

public interface IGoodsReceiptReportingReadPort
{
    Task<ReportingSourcePage<GoodsReceiptListRecord>> ListReportingPageAsync(
        TenantContext tenantContext,
        GoodsReceiptStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);
}

public sealed record GoodsReceiptEligibleLineRecord(
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal UnitPrice,
    decimal ConfirmedQuantity,
    decimal AlreadyReceivedAcceptedQuantity,
    decimal RemainingReceivableQuantity);

public sealed record GoodsReceiptEligibleSourceRecord(
    Guid PurchaseOrderId,
    PurchaseRequestScope Scope,
    PurchaseOrderStatus Status,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    IReadOnlyList<GoodsReceiptEligibleLineRecord> Lines);

public sealed record GoodsReceiptLineRecord(
    Guid Id,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal OrderedQuantityAtReceipt,
    decimal ReceivedQuantity,
    decimal AcceptedQuantity,
    decimal RejectedQuantity,
    decimal? DamagedQuantity,
    string? DamageNotes,
    decimal RemainingReceivableQuantityAfter,
    string? Notes);

public sealed record GoodsReceiptRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    Guid ReceivedByActorId,
    GoodsReceiptStatus Status,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    DateOnly ReceivedDate,
    string? ReferenceNote,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    IReadOnlyList<GoodsReceiptLineRecord> Lines,
    byte[] Version);

public sealed record GoodsReceiptListRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    GoodsReceiptStatus Status,
    string SupplierCode,
    string SupplierName,
    DateOnly ReceivedDate,
    int LineCount,
    decimal TotalAcceptedQuantity,
    DateTimeOffset CreatedAt,
    byte[] Version);

public sealed record GoodsReceiptHistoryRecord(
    Guid EvidenceId,
    Guid GoodsReceiptId,
    DateTimeOffset OccurredAt,
    GoodsReceiptStatus FromStatus,
    GoodsReceiptStatus ToStatus,
    GoodsReceiptHistoryAction Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId);

public sealed record GoodsReceiptAuditEvidence(
    Guid EvidenceId,
    Guid GoodsReceiptId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    GoodsReceiptStatus? BeforeStatus,
    GoodsReceiptStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record GoodsReceiptAuditRecord(
    Guid EvidenceId,
    Guid GoodsReceiptId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    GoodsReceiptStatus? BeforeStatus,
    GoodsReceiptStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

public sealed record GoodsReceiptLineCreateCommand(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal ReceivedQuantity,
    decimal AcceptedQuantity,
    decimal RejectedQuantity,
    decimal? DamagedQuantity,
    string? DamageNotes,
    string? Notes);

public sealed record GoodsReceiptCreateCommand(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid PurchaseOrderId,
    Guid ReceivedByActorId,
    Guid WarehouseId,
    DateOnly ReceivedDate,
    string? ReferenceNote,
    string? Notes,
    IReadOnlyList<GoodsReceiptLineCreateCommand> Lines,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record GoodsReceiptActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record GoodsReceiptReplayQuery(
    Guid ActorId,
    string OperationId,
    string? IdempotencyKey,
    Guid? GoodsReceiptId,
    string? RequestFingerprint);

public enum GoodsReceiptReplayOutcome
{
    NotFound = 1,
    Replay = 2,
    Conflict = 3
}

public sealed record GoodsReceiptReplayProbe(GoodsReceiptReplayOutcome Outcome, GoodsReceiptRecord? Record)
{
    public static readonly GoodsReceiptReplayProbe NotFound = new(GoodsReceiptReplayOutcome.NotFound, null);
    public static readonly GoodsReceiptReplayProbe Conflict = new(GoodsReceiptReplayOutcome.Conflict, null);
    public static GoodsReceiptReplayProbe ForReplay(GoodsReceiptRecord record) => new(GoodsReceiptReplayOutcome.Replay, record);
}

public sealed record GoodsReceiptPersistenceResult<T>(GoodsReceiptPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == GoodsReceiptPersistenceOutcome.Succeeded;
    public static GoodsReceiptPersistenceResult<T> Success(T value) => new(GoodsReceiptPersistenceOutcome.Succeeded, "persisted", value);
    public static GoodsReceiptPersistenceResult<T> Denied(GoodsReceiptPersistenceOutcome outcome, string code) => new(outcome, code, default);
}

public enum GoodsReceiptPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public interface IGoodsReceiptPersistence
{
    Task<IReadOnlyList<GoodsReceiptListRecord>> ListAsync(TenantContext tenantContext, GoodsReceiptStatus? status, Guid? purchaseOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoodsReceiptEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default);
    Task<GoodsReceiptEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<GoodsReceiptReplayProbe> ProbeReplayAsync(TenantContext tenantContext, GoodsReceiptReplayQuery query, CancellationToken cancellationToken = default);
    Task<GoodsReceiptRecord?> FindAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default);
    Task<GoodsReceiptPersistenceResult<GoodsReceiptRecord>> CreateAsync(TenantContext tenantContext, GoodsReceiptCreateCommand command, GoodsReceiptAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<GoodsReceiptPersistenceResult<GoodsReceiptRecord>> CancelAsync(TenantContext tenantContext, GoodsReceiptActionCommand command, GoodsReceiptAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoodsReceiptHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoodsReceiptAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default);
}

public enum GoodsReceiptInventoryEffectVerification
{
    ActiveEffectExists = 1,
    NoActiveEffect = 2,
    Unavailable = 3
}

public interface IGoodsReceiptInventoryEffectReader
{
    Task<GoodsReceiptInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default);
}

public sealed class UnavailableGoodsReceiptInventoryEffectReader : IGoodsReceiptInventoryEffectReader
{
    public Task<GoodsReceiptInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GoodsReceiptInventoryEffectVerification.Unavailable);
}

public sealed class UnavailableGoodsReceiptPersistence : IGoodsReceiptPersistence
{
    private static Task<GoodsReceiptPersistenceResult<T>> Unavailable<T>() =>
        Task.FromResult(GoodsReceiptPersistenceResult<T>.Denied(GoodsReceiptPersistenceOutcome.Failure, "persistence_unavailable"));

    public Task<IReadOnlyList<GoodsReceiptListRecord>> ListAsync(TenantContext tenantContext, GoodsReceiptStatus? status, Guid? purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoodsReceiptListRecord>>([]);
    public Task<IReadOnlyList<GoodsReceiptEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoodsReceiptEligibleSourceRecord>>([]);
    public Task<GoodsReceiptEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<GoodsReceiptEligibleSourceRecord?>(null);
    public Task<GoodsReceiptReplayProbe> ProbeReplayAsync(TenantContext tenantContext, GoodsReceiptReplayQuery query, CancellationToken cancellationToken = default) => Task.FromResult(GoodsReceiptReplayProbe.NotFound);
    public Task<GoodsReceiptRecord?> FindAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) => Task.FromResult<GoodsReceiptRecord?>(null);
    public Task<GoodsReceiptPersistenceResult<GoodsReceiptRecord>> CreateAsync(TenantContext tenantContext, GoodsReceiptCreateCommand command, GoodsReceiptAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<GoodsReceiptRecord>();
    public Task<GoodsReceiptPersistenceResult<GoodsReceiptRecord>> CancelAsync(TenantContext tenantContext, GoodsReceiptActionCommand command, GoodsReceiptAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<GoodsReceiptRecord>();
    public Task<IReadOnlyList<GoodsReceiptHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoodsReceiptHistoryRecord>>([]);
    public Task<IReadOnlyList<GoodsReceiptAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoodsReceiptAuditRecord>>([]);
}

public static class GoodsReceiptValuePolicy
{
    public static bool TryText(string? value, int maxLength, bool allowEmpty, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return (allowEmpty || !string.IsNullOrWhiteSpace(normalized)) && (normalized?.Length ?? 0) <= maxLength;
    }

    public static bool TryReason(string? reason, out string normalized)
    {
        normalized = reason?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 4096;
    }

    public static bool TryNormalizeCreate(
        GoodsReceiptCreateRequest request,
        out DateOnly receivedDate,
        out string? referenceNote,
        out string? notes,
        out IReadOnlyList<GoodsReceiptLineCreateRequest> lines)
    {
        receivedDate = default;
        referenceNote = null;
        notes = null;
        lines = [];
        if (request is null
            || request.PurchaseOrderId == Guid.Empty
            || request.WarehouseId == Guid.Empty
            || request.Lines is null
            || request.Lines.Count == 0
            || request.Lines.Count > 500
            || !TryText(request.ReferenceNote, 256, true, out referenceNote)
            || !TryText(request.Notes, 4096, true, out notes))
        {
            return false;
        }

        receivedDate = request.ReceivedDate;

        var seenLines = new HashSet<Guid>();
        var normalized = new List<GoodsReceiptLineCreateRequest>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (line.PurchaseOrderLineId == Guid.Empty
                || !seenLines.Add(line.PurchaseOrderLineId)
                || line.ReceivedQuantity <= 0
                || line.AcceptedQuantity < 0
                || line.RejectedQuantity < 0
                || line.AcceptedQuantity + line.RejectedQuantity != line.ReceivedQuantity
                || line.DamagedQuantity is < 0
                || line.DamagedQuantity > line.ReceivedQuantity
                || line.Notes?.Trim().Length > 2048
                || line.DamageNotes?.Trim().Length > 2048)
            {
                return false;
            }

            normalized.Add(line with
            {
                Notes = string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim(),
                DamageNotes = string.IsNullOrWhiteSpace(line.DamageNotes) ? null : line.DamageNotes.Trim()
            });
        }

        lines = normalized;
        return true;
    }
}

public sealed record ProcurementWarehouseOption(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string Code,
    string Name,
    bool IsActive = true)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} - {Name}";
}

public interface IProcurementWarehouseProvider
{
    Task<IReadOnlyList<ProcurementWarehouseOption>> ListAsync(
        ProcurementRequestContext context,
        Guid? companyId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);

    Task<ProcurementWarehouseOption?> FindAsync(
        ProcurementRequestContext context,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
}

public sealed class NoProcurementWarehouseProvider : IProcurementWarehouseProvider
{
    public Task<IReadOnlyList<ProcurementWarehouseOption>> ListAsync(
        ProcurementRequestContext context,
        Guid? companyId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProcurementWarehouseOption>>([]);

    public Task<ProcurementWarehouseOption?> FindAsync(
        ProcurementRequestContext context,
        Guid warehouseId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProcurementWarehouseOption?>(null);
}

public sealed class ConfiguredProcurementWarehouseProvider : IProcurementWarehouseProvider
{
    private readonly IReadOnlyList<ProcurementWarehouseOption> options;

    public ConfiguredProcurementWarehouseProvider(IEnumerable<ProcurementWarehouseOption> options)
    {
        this.options = options?.ToArray() ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<IReadOnlyList<ProcurementWarehouseOption>> ListAsync(
        ProcurementRequestContext context,
        Guid? companyId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var filtered = options.Where(item => item.TenantId == context.TenantId.Value && item.IsActive);
        if (companyId.HasValue)
        {
            filtered = filtered.Where(item => item.CompanyId == companyId.Value);
        }

        if (branchId.HasValue)
        {
            filtered = filtered.Where(item => item.BranchId == branchId.Value);
        }

        var results = filtered.OrderBy(item => item.Code).ThenBy(item => item.Name).ToArray();
        return Task.FromResult<IReadOnlyList<ProcurementWarehouseOption>>(results);
    }

    public Task<ProcurementWarehouseOption?> FindAsync(
        ProcurementRequestContext context,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var match = options.FirstOrDefault(item => item.TenantId == context.TenantId.Value && item.WarehouseId == warehouseId);
        return Task.FromResult(match);
    }
}

#pragma warning restore CS1591
