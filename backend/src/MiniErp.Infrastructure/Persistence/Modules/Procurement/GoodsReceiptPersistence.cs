#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public sealed class GoodsReceiptPersistence : IGoodsReceiptPersistence, IGoodsReceiptReportingReadPort
{
    private const string CreateOperationId = "procurement.goods-receipt.create";
    private const int ReplayResponseSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly PurchaseOrderStatus[] EligibleSourceStatuses =
    [
        PurchaseOrderStatus.Confirmed,
        PurchaseOrderStatus.PartiallyConfirmed
    ];
    private readonly DbContextOptions options;

    internal GoodsReceiptPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<GoodsReceiptListRecord>> ListAsync(
        TenantContext tenantContext,
        GoodsReceiptStatus? status,
        Guid? purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = ApplyTrustedScope(db.GoodsReceipts.AsNoTracking(), tenantContext.Scope);
        if (status is { } selectedStatus)
        {
            query = query.Where(item => item.Status == selectedStatus);
        }

        if (purchaseOrderId is { } selectedPurchaseOrderId)
        {
            query = query.Where(item => item.PurchaseOrderId == selectedPurchaseOrderId);
        }

        var entities = await query.Include(item => item.Lines).ToListAsync(cancellationToken);
        var records = entities
            .Select(item => new GoodsReceiptListRecord(
                item.Id,
                item.TenantId.Value,
                new PurchaseRequestScope(item.TenantId.Value, item.CompanyId, item.BranchId),
                item.PurchaseOrderId,
                item.WarehouseId,
                item.Status,
                item.SupplierCode,
                item.SupplierName,
                item.ReceivedDate,
                item.Lines.Count,
                item.Lines.Sum(line => line.AcceptedQuantity),
                item.CreatedAt,
                item.Version.ToArray()))
            .ToArray();
        return records.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).ToArray();
    }

    public async Task<ReportingSourcePage<GoodsReceiptListRecord>> ListReportingPageAsync(
        TenantContext tenantContext,
        GoodsReceiptStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = ApplyTrustedScope(db.GoodsReceipts.AsNoTracking(), tenantContext.Scope);
        if (status is { } selectedStatus) query = query.Where(item => item.Status == selectedStatus);
        if (fromDate is { } from) query = query.Where(item => item.ReceivedDate >= from);
        if (toDate is { } to) query = query.Where(item => item.ReceivedDate <= to);
        var total = await query.CountAsync(cancellationToken);
        var asOf = total == 0 ? null : await query.Select(item => (DateTimeOffset?)item.UpdatedAt).MaxAsync(cancellationToken);
        var ordered = page.SortBy?.ToLowerInvariant() switch
        {
            "receiveddate" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.ReceivedDate).ThenByDescending(item => item.Id) : query.OrderBy(item => item.ReceivedDate).ThenBy(item => item.Id),
            "status" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.Status).ThenByDescending(item => item.Id) : query.OrderBy(item => item.Status).ThenBy(item => item.Id),
            "supplier" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.SupplierName).ThenByDescending(item => item.Id) : query.OrderBy(item => item.SupplierName).ThenBy(item => item.Id),
            _ => page.SortDirection == "asc" ? query.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Id) : query.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
        };
        var entities = await ordered.Include(item => item.Lines).Skip(page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        var records = entities.Select(item => new GoodsReceiptListRecord(
            item.Id, item.TenantId.Value, new PurchaseRequestScope(item.TenantId.Value, item.CompanyId, item.BranchId), item.PurchaseOrderId,
            item.WarehouseId, item.Status, item.SupplierCode, item.SupplierName, item.ReceivedDate, item.Lines.Count,
            item.Lines.Sum(line => line.AcceptedQuantity), item.CreatedAt, item.Version.ToArray())).ToArray();
        return new ReportingSourcePage<GoodsReceiptListRecord>(records, total, "updatedAt,id", asOf);
    }

    public async Task<IReadOnlyList<GoodsReceiptEligibleSourceRecord>> ListEligibleSourcesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var orders = await db.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => EligibleSourceStatuses.Contains(item.Status))
            .ToListAsync(cancellationToken);
        var records = new List<GoodsReceiptEligibleSourceRecord>(orders.Count);
        foreach (var order in orders)
        {
            records.Add(await ToEligibleSourceRecordAsync(db, order, cancellationToken));
        }

        return records;
    }

    public async Task<GoodsReceiptEligibleSourceRecord?> FindEligibleSourceAsync(
        TenantContext tenantContext,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken);
        return order is null ? null : await ToEligibleSourceRecordAsync(db, order, cancellationToken);
    }

    public async Task<GoodsReceiptReplayProbe> ProbeReplayAsync(
        TenantContext tenantContext,
        GoodsReceiptReplayQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = CreateContext(tenantContext);
        var lookup = await FindReplayAsync(db, query, cancellationToken);
        return lookup.Outcome switch
        {
            ReplayOutcome.Replay when lookup.Record is not null => GoodsReceiptReplayProbe.ForReplay(lookup.Record),
            ReplayOutcome.Conflict => GoodsReceiptReplayProbe.Conflict,
            _ => GoodsReceiptReplayProbe.NotFound
        };
    }

    public async Task<GoodsReceiptRecord?> FindAsync(
        TenantContext tenantContext,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.GoodsReceipts
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == goodsReceiptId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<GoodsReceiptPersistenceResult<GoodsReceiptRecord>> CreateAsync(
        TenantContext tenantContext,
        GoodsReceiptCreateCommand command,
        GoodsReceiptAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(GoodsReceiptPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return GoodsReceiptPersistenceResult<GoodsReceiptRecord>.Success(replay.Record!);
            }

            if (command.Scope.TenantId != tenantContext.TenantId.Value || command.Id == Guid.Empty)
            {
                return Denied(GoodsReceiptPersistenceOutcome.Duplicate, "goods_receipt_duplicate");
            }

            if (await db.GoodsReceipts.AnyAsync(item => item.Id == command.Id, cancellationToken))
            {
                return Denied(GoodsReceiptPersistenceOutcome.Duplicate, "goods_receipt_duplicate");
            }

            var order = await db.PurchaseOrders
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == command.PurchaseOrderId, cancellationToken);
            if (order is null)
            {
                return Denied(GoodsReceiptPersistenceOutcome.NotFound, "purchase_order_not_found");
            }

            if (order.Status is not (PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyConfirmed))
            {
                return Denied(GoodsReceiptPersistenceOutcome.InvalidState, "goods_receipt_source_not_eligible");
            }

            var accepted = await AcceptedByLineAsync(db, order.Lines.Select(item => item.Id).ToArray(), cancellationToken);
            var lineMap = order.Lines.ToDictionary(item => item.Id);
            var entity = new GoodsReceiptEntity(command, tenantContext.TenantId, order.SupplierId, order.SupplierCode, order.SupplierName);
            foreach (var lineCommand in command.Lines)
            {
                if (!lineMap.TryGetValue(lineCommand.PurchaseOrderLineId, out var line))
                {
                    return Denied(GoodsReceiptPersistenceOutcome.InvalidState, "goods_receipt_line_not_eligible");
                }

                var already = accepted.TryGetValue(line.Id, out var sum) ? sum : 0m;
                var remaining = Math.Max(0m, line.ConfirmedQuantity - already);
                if (lineCommand.AcceptedQuantity > remaining)
                {
                    return Denied(GoodsReceiptPersistenceOutcome.InvalidState, "over_receipt_not_allowed");
                }

                entity.Lines.Add(new GoodsReceiptLineEntity(
                    tenantContext.TenantId,
                    entity.Id,
                    Guid.NewGuid(),
                    lineCommand,
                    line.ProductId,
                    line.ProductSku,
                    line.ProductName,
                    line.UnitOfMeasureCode,
                    line.ConfirmedQuantity,
                    remaining - lineCommand.AcceptedQuantity));
            }

            order.TouchVersion();
            entity.TouchVersion();
            db.GoodsReceipts.Add(entity);
            db.GoodsReceiptHistory.Add(new GoodsReceiptHistoryEntity(
                evidence.EvidenceId,
                tenantContext.TenantId,
                entity.Id,
                GoodsReceiptStatus.Recorded,
                GoodsReceiptStatus.Recorded,
                GoodsReceiptHistoryAction.Recorded,
                command.ReceivedByActorId,
                null,
                evidence.CorrelationId,
                command.OccurredAt));
            var audit = new GoodsReceiptAuditEntity(evidence);
            db.GoodsReceiptAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GoodsReceiptPersistenceResult<GoodsReceiptRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(GoodsReceiptPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Denied(GoodsReceiptPersistenceOutcome.Duplicate, "goods_receipt_duplicate");
        }
        catch (DbUpdateException)
        {
            return Denied(GoodsReceiptPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(GoodsReceiptPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<GoodsReceiptPersistenceResult<GoodsReceiptRecord>> CancelAsync(
        TenantContext tenantContext,
        GoodsReceiptActionCommand command,
        GoodsReceiptAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        if (command.ExpectedVersion is null || command.ExpectedVersion.Length == 0)
        {
            return Denied(GoodsReceiptPersistenceOutcome.Conflict, "concurrency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await db.GoodsReceipts
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
            if (entity is null)
            {
                return Denied(GoodsReceiptPersistenceOutcome.NotFound, "goods_receipt_not_found");
            }

            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(GoodsReceiptPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return GoodsReceiptPersistenceResult<GoodsReceiptRecord>.Success(replay.Record!);
            }

            if (!VersionMatches(entity.Version, command.ExpectedVersion))
            {
                return Denied(GoodsReceiptPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            if (entity.Status != GoodsReceiptStatus.Recorded)
            {
                return Denied(GoodsReceiptPersistenceOutcome.InvalidState, "cancel_not_allowed");
            }

            var referencedByActiveHandoff = await db.PurchaseInvoiceHandoffSources
                .Where(source => source.GoodsReceiptId == entity.Id)
                .Join(
                    db.PurchaseInvoiceHandoffs,
                    source => new { source.TenantId, source.PurchaseInvoiceHandoffId },
                    handoff => new { handoff.TenantId, PurchaseInvoiceHandoffId = handoff.Id },
                    (source, handoff) => handoff.Status)
                .AnyAsync(status => status != PurchaseInvoiceHandoffStatus.Cancelled, cancellationToken);
            if (referencedByActiveHandoff)
            {
                return Denied(GoodsReceiptPersistenceOutcome.InvalidState, "goods_receipt_referenced_by_active_invoice_handoff");
            }

            var fromStatus = entity.Status;
            entity.Cancel(command.Reason ?? string.Empty, command.OccurredAt);
            entity.TouchVersion();
            db.GoodsReceiptHistory.Add(new GoodsReceiptHistoryEntity(
                Guid.NewGuid(),
                tenantContext.TenantId,
                entity.Id,
                fromStatus,
                GoodsReceiptStatus.Cancelled,
                GoodsReceiptHistoryAction.Cancelled,
                command.ActorId,
                command.Reason,
                evidence.CorrelationId,
                command.OccurredAt));
            var audit = new GoodsReceiptAuditEntity(evidence with
            {
                BeforeStatus = evidence.BeforeStatus ?? fromStatus,
                AfterStatus = evidence.AfterStatus ?? entity.Status,
                BeforeSummary = evidence.BeforeSummary ?? fromStatus.ToString(),
                AfterSummary = evidence.AfterSummary ?? entity.Status.ToString()
            });
            db.GoodsReceiptAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GoodsReceiptPersistenceResult<GoodsReceiptRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(GoodsReceiptPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return Denied(GoodsReceiptPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(GoodsReceiptPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<IReadOnlyList<GoodsReceiptHistoryRecord>> ReadHistoryAsync(
        TenantContext tenantContext,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.GoodsReceiptHistory
            .AsNoTracking()
            .Where(item => item.GoodsReceiptId == goodsReceiptId)
            .Select(item => new GoodsReceiptHistoryRecord(
                item.Id,
                item.GoodsReceiptId,
                item.OccurredAt,
                item.FromStatus,
                item.ToStatus,
                item.Action,
                item.ActorId,
                item.Reason,
                item.CorrelationId))
            .ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    public async Task<IReadOnlyList<GoodsReceiptAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.GoodsReceiptAudit
            .AsNoTracking()
            .Where(item => item.GoodsReceiptId == goodsReceiptId)
            .Select(item => new GoodsReceiptAuditRecord(
                item.Id,
                item.GoodsReceiptId,
                item.OccurredAt,
                item.OperationId,
                item.CorrelationId,
                item.TenantId.Value,
                item.ActorId,
                item.SessionId,
                item.AuthorizationPath,
                item.Decision,
                item.Reason,
                item.BeforeStatus,
                item.AfterStatus,
                item.CompanyId,
                item.BranchId,
                item.BeforeSummary,
                item.AfterSummary,
                item.IdempotencyKey))
            .ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    private static async Task<Dictionary<Guid, decimal>> AcceptedByLineAsync(
        ProcurementDbContext db,
        IReadOnlyCollection<Guid> purchaseOrderLineIds,
        CancellationToken cancellationToken)
    {
        if (purchaseOrderLineIds.Count == 0)
        {
            return [];
        }

        var grouped = await (
            from line in db.GoodsReceiptLines
            join receipt in db.GoodsReceipts
                on new { line.TenantId, line.GoodsReceiptId } equals new { receipt.TenantId, GoodsReceiptId = receipt.Id }
            where receipt.Status == GoodsReceiptStatus.Recorded && purchaseOrderLineIds.Contains(line.PurchaseOrderLineId)
            group line.AcceptedQuantity by line.PurchaseOrderLineId into g
            select new { PurchaseOrderLineId = g.Key, Accepted = g.Sum() })
            .ToListAsync(cancellationToken);
        return grouped.ToDictionary(item => item.PurchaseOrderLineId, item => item.Accepted);
    }

    private async Task<GoodsReceiptEligibleSourceRecord> ToEligibleSourceRecordAsync(
        ProcurementDbContext db,
        PurchaseOrderEntity order,
        CancellationToken cancellationToken)
    {
        var accepted = await AcceptedByLineAsync(db, order.Lines.Select(item => item.Id).ToArray(), cancellationToken);
        var lines = order.Lines
            .OrderBy(item => item.Id)
            .Select(line =>
            {
                var already = accepted.TryGetValue(line.Id, out var sum) ? sum : 0m;
                var remaining = Math.Max(0m, line.ConfirmedQuantity - already);
                return new GoodsReceiptEligibleLineRecord(
                    line.Id,
                    line.ProductId,
                    line.ProductSku,
                    line.ProductName,
                    line.UnitOfMeasureId,
                    line.UnitOfMeasureCode,
                    line.UnitPrice,
                    line.ConfirmedQuantity,
                    already,
                    remaining);
            })
            .ToArray();
        return new GoodsReceiptEligibleSourceRecord(
            order.Id,
            new PurchaseRequestScope(order.TenantId.Value, order.CompanyId, order.BranchId),
            order.Status,
            order.SupplierId,
            order.SupplierCode,
            order.SupplierName,
            order.CurrencyCode,
            lines);
    }

    private Task<ReplayLookup> FindReplayAsync(
        ProcurementDbContext db,
        GoodsReceiptAuditEvidence evidence,
        CancellationToken cancellationToken) =>
        FindReplayAsync(db, ToReplayQuery(evidence), cancellationToken);

    // Create has no pre-existing target: `evidence.GoodsReceiptId` is a freshly generated id for this
    // call and must not be compared against the previously created target's id.
    private static GoodsReceiptReplayQuery ToReplayQuery(GoodsReceiptAuditEvidence evidence) => new(
        evidence.ActorId,
        evidence.OperationId,
        evidence.IdempotencyKey,
        evidence.OperationId == CreateOperationId ? null : evidence.GoodsReceiptId,
        evidence.RequestFingerprint);

    private async Task<ReplayLookup> FindReplayAsync(
        ProcurementDbContext db,
        GoodsReceiptReplayQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.IdempotencyKey))
        {
            return ReplayLookup.NotFound;
        }

        var replayCandidates = await db.GoodsReceiptAudit
            .Where(item => item.ActorId == query.ActorId
                && item.OperationId == query.OperationId
                && item.IdempotencyKey == query.IdempotencyKey)
            .Select(item => new
            {
                item.Id,
                item.GoodsReceiptId,
                item.OccurredAt,
                item.RequestFingerprint,
                item.ReplayResponseSchemaVersion,
                item.ReplayResponseSnapshotJson
            })
            .ToListAsync(cancellationToken);
        if (replayCandidates.Count == 0)
        {
            return ReplayLookup.NotFound;
        }

        if (replayCandidates.Select(item => item.GoodsReceiptId).Distinct().Count() != 1)
        {
            return ReplayLookup.Conflict;
        }

        if (query.GoodsReceiptId is { } targetId
            && replayCandidates.Any(item => item.GoodsReceiptId != targetId))
        {
            return ReplayLookup.Conflict;
        }

        if (replayCandidates.Any(item => !string.Equals(item.RequestFingerprint, query.RequestFingerprint, StringComparison.Ordinal)))
        {
            return ReplayLookup.Conflict;
        }

        var original = replayCandidates
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .First();
        if (original.ReplayResponseSchemaVersion != ReplayResponseSchemaVersion
            || string.IsNullOrWhiteSpace(original.ReplayResponseSnapshotJson))
        {
            return ReplayLookup.Conflict;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<ReplayResponseSnapshot>(original.ReplayResponseSnapshotJson, JsonOptions);
            if (snapshot is null
                || snapshot.SchemaVersion != ReplayResponseSchemaVersion
                || snapshot.Response is null
                || snapshot.Response.Id != original.GoodsReceiptId)
            {
                return ReplayLookup.Conflict;
            }

            return ReplayLookup.ForReplay(snapshot.Response);
        }
        catch (JsonException)
        {
            return ReplayLookup.Conflict;
        }
    }

    private static string SerializeReplayResponse(GoodsReceiptRecord response) =>
        JsonSerializer.Serialize(new ReplayResponseSnapshot(ReplayResponseSchemaVersion, response), JsonOptions);

    private static GoodsReceiptRecord ToRecord(GoodsReceiptEntity entity) => new(
        entity.Id,
        entity.TenantId.Value,
        new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
        entity.PurchaseOrderId,
        entity.WarehouseId,
        entity.ReceivedByActorId,
        entity.Status,
        entity.SupplierId,
        entity.SupplierCode,
        entity.SupplierName,
        entity.ReceivedDate,
        entity.ReferenceNote,
        entity.Notes,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.CancelledAt,
        entity.CancellationReason,
        entity.Lines
            .OrderBy(item => item.Id)
            .Select(item => new GoodsReceiptLineRecord(
                item.Id,
                item.PurchaseOrderLineId,
                item.ProductId,
                item.ProductSku,
                item.ProductName,
                item.UnitOfMeasureCode,
                item.OrderedQuantityAtReceipt,
                item.ReceivedQuantity,
                item.AcceptedQuantity,
                item.RejectedQuantity,
                item.DamagedQuantity,
                item.DamageNotes,
                item.RemainingReceivableQuantityAfter,
                item.Notes))
            .ToArray(),
        entity.Version.ToArray());

    private ProcurementDbContext CreateContext(TenantContext tenantContext) => new(options, tenantContext);

    private static IQueryable<GoodsReceiptEntity> ApplyTrustedScope(IQueryable<GoodsReceiptEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference)
        {
            return query;
        }

        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
        {
            return query.Where(_ => false);
        }

        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Warehouse" => query.Where(item => item.WarehouseId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static bool VersionMatches(byte[] actual, byte[] expected) => actual.SequenceEqual(expected);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 }
                || current is SqliteException { SqliteErrorCode: 19 })
            {
                return true;
            }
        }

        return false;
    }

    private static GoodsReceiptPersistenceResult<GoodsReceiptRecord> Denied(GoodsReceiptPersistenceOutcome outcome, string code) =>
        GoodsReceiptPersistenceResult<GoodsReceiptRecord>.Denied(outcome, code);

    private enum ReplayOutcome
    {
        NotFound,
        Replay,
        Conflict
    }

    private sealed record ReplayLookup(ReplayOutcome Outcome, GoodsReceiptRecord? Record)
    {
        internal static readonly ReplayLookup NotFound = new(ReplayOutcome.NotFound, null);
        internal static readonly ReplayLookup Conflict = new(ReplayOutcome.Conflict, null);
        internal static ReplayLookup ForReplay(GoodsReceiptRecord record) => new(ReplayOutcome.Replay, record);
    }

    private sealed record ReplayResponseSnapshot(int SchemaVersion, GoodsReceiptRecord Response);
}

#pragma warning restore CS1591
