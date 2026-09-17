#pragma warning disable CS1591

using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed partial class InventoryPersistence(DbContextOptions options) : IInventoryPersistence
{
    private InventoryDbContext CreateContext(InventoryRequestContext context) => new(options, context.TenantContext);

    public async Task<IReadOnlyList<InventoryMovementRecord>> ListMovementsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var query = db.StockMovements.AsNoTracking().AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId);
        if (productId.HasValue) query = query.Where(item => item.ProductId == productId.Value);
        var values = await query.ToListAsync(cancellationToken);
        return values.OrderByDescending(item => item.LedgerSequence).ThenByDescending(item => item.PostedAt).Select(ToMovement.Compile()).ToArray();
    }

    public async Task<InventoryMovementRecord?> FindMovementAsync(InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        return await db.StockMovements.AsNoTracking().Where(item => item.Id == movementId).Select(ToMovement).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryOpeningBalanceRecord>> ListOpeningBalancesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var query = db.OpeningBalances.AsNoTracking().Include(item => item.Rows).AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId);
        var values = await query.ToListAsync(cancellationToken);
        values = values.OrderByDescending(item => item.CreatedAt).ToList();
        return values.Select(ToOpening).ToArray();
    }

    public async Task<InventoryOpeningBalanceRecord?> FindOpeningBalanceAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var value = await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return value is null ? null : ToOpening(value);
    }

    public async Task<InventoryOpeningBalanceRecord?> CreateOpeningBalanceAsync(InventoryRequestContext context, InventoryOpeningBalanceCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, "inventory.opening.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var sourceFingerprints = command.Rows
                .Select(row => InventorySourceIdentity.Create(context.TenantId, command, row.SourceLineReference, row.TrackingIdentity))
                .ToArray();
            var consumedSourceFingerprints = await ReadConsumedSourceFingerprintsAsync(
                db,
                sourceFingerprints,
                cancellationToken);
            var batchSourceFingerprints = new HashSet<string>(StringComparer.Ordinal);
            var batch = new InventoryOpeningBalanceEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.WarehouseCode, command.WarehouseName, command.AsOfDate, command.SourceOwner, command.SourceSystem, command.ExtractedAt, command.SourceReference, command.ActorId, command.OccurredAt);
            db.OpeningBalances.Add(batch);
            for (var index = 0; index < command.Rows.Count; index++)
            {
                var row = command.Rows[index];
                var sourceFingerprint = sourceFingerprints[index];
                var validationCode = row.ValidationCode;
                if (validationCode is null && string.IsNullOrWhiteSpace(row.SourceLineReference))
                {
                    validationCode = "source_provenance_required";
                }
                else if (validationCode is null
                    && (!batchSourceFingerprints.Add(sourceFingerprint)
                        || consumedSourceFingerprints.Contains(sourceFingerprint)))
                {
                    validationCode = "duplicate_source_row";
                }

                var status = validationCode is null ? InventoryOpeningRowStatus.Valid : InventoryOpeningRowStatus.Quarantined;
                batch.Rows.Add(new InventoryOpeningBalanceRowEntity(context.TenantId, row.Id, batch.Id, row.ProductId, row.Product?.Sku ?? string.Empty, row.Product?.Name ?? string.Empty, row.UnitOfMeasureId, row.Product?.BaseUnitOfMeasureCode ?? string.Empty, row.Quantity, row.UnitCost, row.CurrencyCode, row.TrackingIdentity, row.SourceLineReference, sourceFingerprint, status, validationCode));
            }
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), batch.Id, InventoryOpeningBalanceStatus.Draft, InventoryOpeningBalanceStatus.Draft, "created", command.ActorId, null, command.CorrelationId, command.OccurredAt));
            AddAudit(db, context, "opening-balance", batch.Id, "inventory.opening.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, "draft-created", command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == batch.Id, cancellationToken));
            AddReplay(db, context, "inventory.opening.create", command.IdempotencyKey, command.RequestFingerprint, "opening-balance", result.Id, result, command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public Task<InventoryOpeningBalanceRecord?> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        ValidateOpeningAsync(context, id, expectedVersion, actorId, reason, correlationId, idempotencyKey, fingerprint, cancellationToken);

    public Task<InventoryOpeningBalanceRecord?> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        PostOpeningAsync(context, id, expectedVersion, actorId, reason, correlationId, idempotencyKey, fingerprint, cancellationToken);

    public Task<InventoryOpeningBalanceRecord?> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        CorrectOpeningAsync(context, id, expectedVersion, actorId, reason, correlationId, idempotencyKey, fingerprint, cancellationToken);

    private async Task<InventoryOpeningBalanceRecord?> ValidateOpeningAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, "inventory.opening.validate", idempotencyKey, fingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = await db.OpeningBalances.Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (batch is null || !batch.Version.SequenceEqual(expectedVersion)) return null;

            var consumedSourceFingerprints = await ReadConsumedSourceFingerprintsAsync(
                db,
                batch.Rows.Select(item => item.SourceFingerprint),
                cancellationToken);
            foreach (var row in batch.Rows.Where(item => item.Status is InventoryOpeningRowStatus.Valid or InventoryOpeningRowStatus.Pending))
            {
                if (consumedSourceFingerprints.Contains(row.SourceFingerprint))
                {
                    row.Validate(InventoryOpeningRowStatus.Quarantined, "duplicate_source_row");
                }
                else
                {
                    row.Validate(InventoryOpeningRowStatus.Valid, null);
                }
            }

            var now = DateTimeOffset.UtcNow;
            var from = batch.Status;
            var to = batch.Rows.Any(item => item.Status == InventoryOpeningRowStatus.Valid)
                ? InventoryOpeningBalanceStatus.Validated
                : InventoryOpeningBalanceStatus.Draft;
            batch.SetStatus(to, now);
            batch.TouchVersion();
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, from, to, "validate", actorId, reason, correlationId, now));
            AddAudit(db, context, "opening-balance", id, "inventory.opening.validate", actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, from.ToString(), to.ToString(), now);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == id, cancellationToken));
            AddReplay(db, context, "inventory.opening.validate", idempotencyKey, fingerprint, "opening-balance", result.Id, result, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private async Task<InventoryOpeningBalanceRecord?> ActOpeningAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, string operationId, Func<InventoryOpeningBalanceEntity, DateTimeOffset, InventoryOpeningBalanceStatus> transition, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, operationId, idempotencyKey, fingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = await db.OpeningBalances.Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (batch is null || !batch.Version.SequenceEqual(expectedVersion)) return null;
            var from = batch.Status;
            var to = transition(batch, DateTimeOffset.UtcNow);
            if (to == from) return null;
            batch.SetStatus(to, DateTimeOffset.UtcNow);
            batch.TouchVersion();
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, from, to, operationId[(operationId.LastIndexOf('.') + 1)..], actorId, reason, correlationId, DateTimeOffset.UtcNow));
            AddAudit(db, context, "opening-balance", id, operationId, actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, from.ToString(), to.ToString(), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == id, cancellationToken));
            AddReplay(db, context, operationId, idempotencyKey, fingerprint, "opening-balance", result.Id, result, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private async Task<InventoryOpeningBalanceRecord?> PostOpeningAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, "inventory.opening.post", idempotencyKey, fingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = await db.OpeningBalances.Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (batch is null || !batch.Version.SequenceEqual(expectedVersion) || batch.Status != InventoryOpeningBalanceStatus.Validated) return null;
            var consumedSourceFingerprints = await ReadConsumedSourceFingerprintsAsync(
                db,
                batch.Rows.Where(item => item.Status == InventoryOpeningRowStatus.Valid).Select(item => item.SourceFingerprint),
                cancellationToken);
            foreach (var row in batch.Rows.Where(item => item.Status == InventoryOpeningRowStatus.Valid)
                         .Where(item => consumedSourceFingerprints.Contains(item.SourceFingerprint)))
            {
                row.Validate(InventoryOpeningRowStatus.Quarantined, "duplicate_source_row");
            }

            if (batch.Rows.Any(item => item.Status != InventoryOpeningRowStatus.Valid))
            {
                var blockedAt = DateTimeOffset.UtcNow;
                db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, batch.Status, batch.Status, "post-blocked", actorId, "opening_quarantined_rows", correlationId, blockedAt));
                AddAudit(db, context, "opening-balance", id, "inventory.opening.post", actorId, "Failed", "opening_quarantined_rows", correlationId, idempotencyKey, fingerprint, batch.Status.ToString(), "post-blocked", blockedAt);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var rows = batch.Rows.Where(item => item.Status == InventoryOpeningRowStatus.Valid).ToArray();
            if (rows.Length == 0) return null;
            var now = DateTimeOffset.UtcNow;
            var movements = rows.Select(row => new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), batch.CompanyId, batch.BranchId, batch.WarehouseId, batch.WarehouseCode, batch.WarehouseName, row.ProductId, row.ProductSku, row.ProductName, row.UnitOfMeasureId, row.UnitOfMeasureCode, InventoryMovementDirection.Inbound, row.Quantity, row.UnitCost, row.CurrencyCode, row.TrackingIdentity, InventoryMovementSourceType.OpeningBalance, batch.Id, row.Id, null, batch.AsOfDate, actorId, correlationId, now)).ToArray();
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, movements.Select(StockIdentityKey.From), cancellationToken);
            db.StockMovements.AddRange(movements);
            foreach (var row in rows) row.MarkPosted(now);
            var from = batch.Status; batch.SetStatus(InventoryOpeningBalanceStatus.Posted, now); batch.TouchVersion();
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, from, batch.Status, "posted", actorId, reason, correlationId, now));
            AddAudit(db, context, "opening-balance", id, "inventory.opening.post", actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, from.ToString(), "Posted", now);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == id, cancellationToken));
            AddReplay(db, context, "inventory.opening.post", idempotencyKey, fingerprint, "opening-balance", id, result, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private async Task<InventoryOpeningBalanceRecord?> CorrectOpeningAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, "inventory.opening.correct", idempotencyKey, fingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = await db.OpeningBalances.Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (batch is null || !batch.Version.SequenceEqual(expectedVersion) || batch.Status != InventoryOpeningBalanceStatus.Posted) return null;
            var original = await db.StockMovements.Where(item => item.SourceType == InventoryMovementSourceType.OpeningBalance && item.SourceDocumentId == id).ToListAsync(cancellationToken);
            if (original.Count == 0) return null;

            var affectedKeys = original
                .Select(StockIdentityKey.From)
                .Distinct()
                .OrderBy(item => item.CompanyId)
                .ThenBy(item => item.BranchId ?? Guid.Empty)
                .ThenBy(item => item.WarehouseId)
                .ThenBy(item => item.ProductId)
                .ThenBy(item => item.UnitOfMeasureId)
                .ThenBy(item => item.TrackingKey, StringComparer.Ordinal)
                .ToArray();
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, affectedKeys, cancellationToken);

            var plannedReversals = original
                .GroupBy(StockIdentityKey.From)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
            foreach (var planned in plannedReversals)
            {
                var onHand = await SignedQuantityAsync(db, planned.Key, cancellationToken);
                var reserved = await ActiveReservedQuantityAsync(db, planned.Key, cancellationToken);
                if (onHand - planned.Value < reserved)
                {
                    var blockedAt = DateTimeOffset.UtcNow;
                    db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, batch.Status, batch.Status, "correction-blocked", actorId, "active_reservation_would_be_unsupported", correlationId, blockedAt));
                    AddAudit(db, context, "opening-balance", id, "inventory.opening.correct", actorId, "Failed", "active_reservation_would_be_unsupported", correlationId, idempotencyKey, fingerprint, batch.Status.ToString(), "correction-blocked", blockedAt);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return null;
                }
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var movement in original)
            {
                db.StockMovements.Add(new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), batch.CompanyId, batch.BranchId, batch.WarehouseId, batch.WarehouseCode, batch.WarehouseName, movement.ProductId, movement.ProductSku, movement.ProductName, movement.UnitOfMeasureId, movement.UnitOfMeasureCode, InventoryMovementDirection.Outbound, movement.Quantity, movement.UnitCost, movement.CurrencyCode, movement.TrackingIdentity, InventoryMovementSourceType.Correction, id, Guid.NewGuid(), movement.Id, batch.AsOfDate, actorId, correlationId, now));
            }
            foreach (var row in batch.Rows.Where(item => item.Status == InventoryOpeningRowStatus.Posted)) row.MarkCorrected();
            var from = batch.Status; batch.SetStatus(InventoryOpeningBalanceStatus.Corrected, now); batch.TouchVersion();
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, from, batch.Status, "corrected", actorId, reason, correlationId, now));
            AddAudit(db, context, "opening-balance", id, "inventory.opening.correct", actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, from.ToString(), "Corrected", now);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == id, cancellationToken));
            AddReplay(db, context, "inventory.opening.correct", idempotencyKey, fingerprint, "opening-balance", id, result, now);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var values = await db.OpeningBalanceHistory.AsNoTracking().Where(item => item.OpeningBalanceId == id).ToListAsync(cancellationToken);
        return values.OrderBy(item => item.OccurredAt).Select(item => new InventoryOpeningBalanceHistoryRecord(item.Id, item.OpeningBalanceId, item.FromStatus, item.ToStatus, item.Action, item.ActorId, item.Reason, item.CorrelationId, item.OccurredAt, item.Version)).ToArray();
    }

    public async Task<IReadOnlyList<InventoryReservationRecord>> ListReservationsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var query = db.Reservations.AsNoTracking().AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId);
        if (productId.HasValue) query = query.Where(item => item.ProductId == productId.Value);
        var values = await query.ToListAsync(cancellationToken);
        return values.OrderByDescending(item => item.CreatedAt).Select(ToReservation.Compile()).ToArray();
    }

    public async Task<InventoryReservationRecord?> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); return await db.Reservations.AsNoTracking().Where(item => item.Id == id).Select(ToReservation).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<InventoryReservationRecord?> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCommand command, decimal availableQuantity, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryReservationRecord>(db, context, "inventory.reservation.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Handled) return replay.Value;
            var stockIdentity = StockIdentityKey.From(command);
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, [stockIdentity], cancellationToken);
            if (command.SourceQuantityLimit is { } sourceLimit)
            {
                if (sourceLimit <= 0m) return null;
                var sourceReservations = await db.Reservations
                    .Where(item => item.SourceDocumentId == command.SourceDocumentId
                        && item.SourceLineId == command.SourceLineId
                        && item.SourceRevision == command.SourceRevision
                        && item.Status != InventoryReservationStatus.Released)
                    .ToListAsync(cancellationToken);
                var committed = sourceReservations.Sum(item => item.Status == InventoryReservationStatus.Active
                    ? item.FulfilledQuantity + item.ReservedQuantity + item.UnallocatedQuantity
                    : item.FulfilledQuantity);
                var sourceRemaining = sourceLimit - committed;
                if (sourceRemaining <= 0m) return null;
                command = command with { RequestedQuantity = Math.Min(command.RequestedQuantity, sourceRemaining) };
            }
            var available = await CalculateAvailableAsync(db, command.Scope, command.ProductId, command.UnitOfMeasureId, command.TrackingIdentity, cancellationToken);
            if (command.RequestedQuantity > available && !command.AllowPartialAllocation) return null;
            var reserved = Math.Min(command.RequestedQuantity, Math.Max(0, available)); var unallocated = command.RequestedQuantity - reserved; var now = command.OccurredAt;
            var reservation = new InventoryReservationEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.WarehouseCode, command.WarehouseName, command.ProductId, command.Product.Sku, command.Product.Name, command.UnitOfMeasureId, command.Product.BaseUnitOfMeasureCode, command.TrackingIdentity, command.SourceType, command.SourceReference, command.RequestedQuantity, reserved, unallocated, command.ActorId, now, command.SourceDocumentId, command.SourceLineId, command.SourceRevision);
            db.Reservations.Add(reservation); db.ReservationHistory.Add(new InventoryReservationHistoryEntity(context.TenantId, Guid.NewGuid(), reservation.Id, InventoryReservationAction.Created, reserved, reserved, unallocated, 0m, command.ActorId, null, command.CorrelationId, now));
            AddAudit(db, context, "reservation", reservation.Id, "inventory.reservation.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"reserved:{reserved}", now);
            await db.SaveChangesAsync(cancellationToken);
            var result = await db.Reservations.AsNoTracking().Where(item => item.Id == reservation.Id).Select(ToReservation).SingleAsync(cancellationToken);
            AddReplay(db, context, "inventory.reservation.create", command.IdempotencyKey, command.RequestFingerprint, "reservation", result.Id, result, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public Task<InventoryReservationRecord?> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, quantity, actorId, reason, correlationId, idempotencyKey, fingerprint, "inventory.reservation.reduce", (reservation, now) => { if (reservation.Status != InventoryReservationStatus.Active || quantity > reservation.ReservedQuantity) return false; reservation.Reduce(quantity, now); reservation.TouchVersion(); return true; }, cancellationToken);

    public Task<InventoryReservationRecord?> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, 0, actorId, reason, correlationId, idempotencyKey, fingerprint, "inventory.reservation.release", (reservation, now) => { if (reservation.Status != InventoryReservationStatus.Active) return false; reservation.Release(now); reservation.TouchVersion(); return true; }, cancellationToken);

    public Task<InventoryReservationRecord?> AllocateReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, quantity, actorId, reason, correlationId, idempotencyKey, fingerprint, "inventory.reservation.allocate", (reservation, now) => { if (!reservation.Allocate(quantity, now)) return false; reservation.TouchVersion(); return true; }, cancellationToken);

    public async Task<InventorySalesDeliveryPostingRecord?> PostSalesDeliveryAsync(InventoryRequestContext context, InventorySalesDeliveryPostCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventorySalesDeliveryPostingRecord>(db, context, "inventory.sales-delivery.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            if (command.Lines.Count == 0 || command.Lines.Select(item => item.SourceLineId).Distinct().Count() != command.Lines.Count) return null;

            var existing = await db.StockMovements.AsNoTracking()
                .Where(item => item.SourceType == InventoryMovementSourceType.SalesDelivery && item.SourceDocumentId == command.DeliveryId)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                return existing.Count == command.Lines.Count
                    ? new InventorySalesDeliveryPostingRecord(command.DeliveryId, command.SalesOrderId, existing.OrderBy(item => item.LedgerSequence).Select(item => item.Id).ToArray(), existing.Sum(item => item.Quantity), existing.Max(item => item.PostedAt), context.TenantId.Value, command.SalesOrderRevision)
                    : null;
            }

            var reservationIds = command.Lines.Select(item => item.ReservationId).Distinct().ToArray();
            if (reservationIds.Length != command.Lines.Count) return null;
            var reservations = await db.Reservations.Where(item => reservationIds.Contains(item.Id)).ToListAsync(cancellationToken);
            if (reservations.Count != command.Lines.Count) return null;
            var byId = reservations.ToDictionary(item => item.Id);
            var identities = reservations.Select(StockIdentityKey.From).ToArray();
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, identities, cancellationToken);

            foreach (var line in command.Lines)
            {
                if (!byId.TryGetValue(line.ReservationId, out var reservation)
                    || reservation.Status != InventoryReservationStatus.Active
                    || reservation.SourceDocumentId != command.SalesOrderId
                    || reservation.SourceLineId != line.SourceLineId
                    || reservation.SourceRevision != command.SalesOrderRevision
                    || !string.Equals(reservation.SourceReference, line.SourceReference, StringComparison.Ordinal)
                    || !reservation.Version.SequenceEqual(line.ExpectedReservationVersion)
                    || line.Quantity > reservation.ReservedQuantity)
                {
                    return null;
                }
            }

            foreach (var group in command.Lines.GroupBy(item => StockIdentityKey.From(byId[item.ReservationId])))
            {
                var onHand = await SignedQuantityAsync(db, group.Key, cancellationToken);
                if (group.Sum(item => item.Quantity) > onHand) return null;
            }

            var now = command.OccurredAt;
            var movements = new List<InventoryStockMovementEntity>(command.Lines.Count);
            foreach (var line in command.Lines)
            {
                var reservation = byId[line.ReservationId];
                var movement = new InventoryStockMovementEntity(
                    context.TenantId, Guid.NewGuid(), reservation.CompanyId, reservation.BranchId, reservation.WarehouseId,
                    reservation.WarehouseCode, reservation.WarehouseName, reservation.ProductId, reservation.ProductSku,
                    reservation.ProductName, reservation.UnitOfMeasureId, reservation.UnitOfMeasureCode,
                    InventoryMovementDirection.Outbound, line.Quantity, null, null, InventoryValuationStatus.Pending,
                    reservation.TrackingIdentity, InventoryMovementSourceType.SalesDelivery, command.DeliveryId, line.SourceLineId,
                    null, command.EffectiveDate, command.ActorId, command.CorrelationId, now, sourceReference: command.SourceReference);
                db.StockMovements.Add(movement);
                movements.Add(movement);
                if (!reservation.Consume(line.Quantity, now)) return null;
                reservation.TouchVersion();
                db.ReservationHistory.Add(new InventoryReservationHistoryEntity(context.TenantId, Guid.NewGuid(), reservation.Id, InventoryReservationAction.Consumed, line.Quantity, reservation.ReservedQuantity, reservation.UnallocatedQuantity, reservation.FulfilledQuantity, command.ActorId, "sales-delivery-posted", command.CorrelationId, now));
            }

            var result = new InventorySalesDeliveryPostingRecord(command.DeliveryId, command.SalesOrderId, movements.Select(item => item.Id).ToArray(), movements.Sum(item => item.Quantity), now, context.TenantId.Value, command.SalesOrderRevision);
            AddAudit(db, context, "sales-delivery", command.DeliveryId, "inventory.sales-delivery.post", command.ActorId, "Succeeded", "physical quantity posted", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"movements:{movements.Count};quantity:{result.PostedQuantity}", now);
            await db.SaveChangesAsync(cancellationToken);
            AddReplay(db, context, "inventory.sales-delivery.post", command.IdempotencyKey, command.RequestFingerprint, "sales-delivery", command.DeliveryId, result, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private async Task<InventoryReservationRecord?> ActReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, string operationId, Func<InventoryReservationEntity, DateTimeOffset, bool> mutate, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryReservationRecord>(db, context, operationId, idempotencyKey, fingerprint, cancellationToken); if (replay.Handled) return replay.Value;
            var reference = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (reference is null) return null;
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, [StockIdentityKey.From(reference)], cancellationToken);
            if (operationId.EndsWith("allocate", StringComparison.Ordinal))
            {
                var available = await CalculateAvailableAsync(db, new InventoryScope(context.TenantId.Value, reference.CompanyId, reference.BranchId, reference.WarehouseId), reference.ProductId, reference.UnitOfMeasureId, reference.TrackingIdentity, cancellationToken);
                if (quantity > available + reference.ReservedQuantity) return null;
            }
            var reservation = await db.Reservations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (reservation is null || !reservation.Version.SequenceEqual(expectedVersion) || !mutate(reservation, DateTimeOffset.UtcNow)) return null;
            var now = DateTimeOffset.UtcNow; var action = operationId.EndsWith("release", StringComparison.Ordinal) ? InventoryReservationAction.Released : operationId.EndsWith("allocate", StringComparison.Ordinal) ? InventoryReservationAction.Allocated : InventoryReservationAction.Reduced;
            db.ReservationHistory.Add(new InventoryReservationHistoryEntity(context.TenantId, Guid.NewGuid(), id, action, quantity == 0 ? reservation.RequestedQuantity : quantity, reservation.ReservedQuantity, reservation.UnallocatedQuantity, reservation.FulfilledQuantity, actorId, reason, correlationId, now));
            AddAudit(db, context, "reservation", id, operationId, actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, "active", reservation.Status.ToString(), now);
            await db.SaveChangesAsync(cancellationToken); var result = await db.Reservations.AsNoTracking().Where(item => item.Id == id).Select(ToReservation).SingleAsync(cancellationToken);
            AddReplay(db, context, operationId, idempotencyKey, fingerprint, "reservation", result.Id, result, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<IReadOnlyList<InventoryReservationHistoryRecord>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await db.ReservationHistory.AsNoTracking().Where(item => item.ReservationId == id).ToListAsync(cancellationToken); return values.OrderBy(item => item.OccurredAt).Select(item => new InventoryReservationHistoryRecord(item.Id, item.ReservationId, item.Action, item.Quantity, item.ReservedQuantityAfter, item.UnallocatedQuantityAfter, item.FulfilledQuantityAfter, item.ActorId, item.Reason, item.CorrelationId, item.OccurredAt, item.Version)).ToArray();
    }

    public async Task<InventoryAvailabilityRecord?> GetAvailabilityAsync(InventoryRequestContext context, InventoryScope scope, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, InventoryProductReference product, InventoryWarehouseOption warehouse, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var onHand = await SignedQuantityAsync(db, scope.CompanyId, scope.BranchId, scope.WarehouseId, productId, unitOfMeasureId, trackingIdentity, cancellationToken); var reserved = await db.Reservations.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitOfMeasureId && item.TrackingIdentity == trackingIdentity && item.Status == InventoryReservationStatus.Active).SumAsync(item => (decimal?)item.ReservedQuantity, cancellationToken) ?? 0;
        var transitTransferIds = await db.Transfers.AsNoTracking()
            .Where(item => item.Mode == InventoryTransferMode.InTransit && item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.DestinationWarehouseId == scope.WarehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitOfMeasureId && item.TrackingIdentity == trackingIdentity)
            .Select(item => item.Id).ToArrayAsync(cancellationToken);
        var transitEvents = await db.TransferEvents.AsNoTracking().Where(item => transitTransferIds.Contains(item.TransferId)).ToListAsync(cancellationToken);
        var inTransit = transitEvents.Where(item => item.EventType == InventoryTransferEventType.Shipped).Sum(item => item.Quantity);
        var received = transitEvents.Where(item => item.EventType == InventoryTransferEventType.Received).Sum(item => item.Quantity);
        var lost = transitEvents.Where(item => item.EventType == InventoryTransferEventType.ShortageResolved).Sum(item => item.Quantity);
        inTransit = Math.Max(0, inTransit - received - lost);
        var anchor = await db.ConcurrencyAnchors.AsNoTracking().FirstOrDefaultAsync(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitOfMeasureId && item.TrackingKey == (trackingIdentity ?? string.Empty), cancellationToken);
        return new InventoryAvailabilityRecord(context.TenantId.Value, scope.CompanyId, scope.BranchId, scope.WarehouseId, warehouse.Code, warehouse.Name, productId, product.Sku, product.Name, unitOfMeasureId, product.BaseUnitOfMeasureCode, trackingIdentity, product.TrackingEnabled, onHand, reserved, Math.Max(0, onHand - reserved), 0, 0, inTransit, DateTimeOffset.UtcNow, anchor?.Version ?? []);
    }

    public async Task<InventoryGoodsReceiptPostingRecord?> PostGoodsReceiptAsync(InventoryRequestContext context, InventoryGoodsReceiptPostCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryGoodsReceiptPostingRecord>(db, context, "inventory.goods-receipt.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var source = command.Source;
            if (source.Product.TrackingEnabled) return null;
            var existing = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(item => item.SourceType == InventoryMovementSourceType.GoodsReceipt && item.SourceDocumentId == source.Receipt.Id && item.SourceLineId == source.Line.Id, cancellationToken);
            if (existing is not null)
            {
                var existingResult = ToGoodsReceiptPosting(existing, true);
                AddAudit(db, context, "goods-receipt", source.Receipt.Id, "inventory.goods-receipt.post", command.ActorId, "Duplicate", "physical source line already posted", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, $"movement:{existing.Id}", $"movement:{existing.Id}", command.OccurredAt);
                AddReplay(db, context, "inventory.goods-receipt.post", command.IdempotencyKey, command.RequestFingerprint, "goods-receipt", source.Receipt.Id, existingResult, command.OccurredAt);
                await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return existingResult;
            }

            var identity = new StockIdentityKey(source.Receipt.Scope.CompanyId, source.Receipt.Scope.BranchId, source.Warehouse.WarehouseId, source.Line.ProductId, source.UnitOfMeasureId, source.Product.TrackingEnabled ? string.Empty : string.Empty);
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, [identity], cancellationToken);
            var movement = new InventoryStockMovementEntity(context.TenantId, command.PostingId, source.Receipt.Scope.CompanyId, source.Receipt.Scope.BranchId, source.Warehouse.WarehouseId, source.Warehouse.Code, source.Warehouse.Name, source.Line.ProductId, source.Product.Sku, source.Product.Name, source.UnitOfMeasureId, source.Line.UnitOfMeasureCode, InventoryMovementDirection.Inbound, source.Line.AcceptedQuantity, null, null, InventoryValuationStatus.Pending, null, InventoryMovementSourceType.GoodsReceipt, source.Receipt.Id, source.Line.Id, null, source.Receipt.ReceivedDate, command.ActorId, command.CorrelationId, command.OccurredAt, source.Receipt.Id, source.Line.Id, null, null, source.Receipt.PurchaseOrderId, source.Line.PurchaseOrderLineId, null, null, null);
            db.StockMovements.Add(movement);
            AddAudit(db, context, "goods-receipt", source.Receipt.Id, "inventory.goods-receipt.post", command.ActorId, "Succeeded", "physical quantity posted", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"accepted:{source.Line.AcceptedQuantity}", command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToGoodsReceiptPosting(movement, false);
            AddReplay(db, context, "inventory.goods-receipt.post", command.IdempotencyKey, command.RequestFingerprint, "goods-receipt", source.Receipt.Id, result, command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<InventoryReplayProbe<InventorySupplierReturnPostingRecord>> ProbeSupplierReturnReplayAsync(
        InventoryRequestContext context,
        string? idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        return await ProbeReplayAsync<InventorySupplierReturnPostingRecord>(
            db,
            context,
            "inventory.supplier-return.post",
            idempotencyKey,
            requestFingerprint,
            cancellationToken);
    }

    public async Task<InventorySupplierReturnPostingRecord?> PostSupplierReturnAsync(InventoryRequestContext context, InventorySupplierReturnPostCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventorySupplierReturnPostingRecord>(db, context, "inventory.supplier-return.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var source = command.Source;
            if (source.Lines.Any(line => line.Product.TrackingEnabled)) return null;
            var existing = await db.StockMovements.AsNoTracking().Where(item => item.SourceType == InventoryMovementSourceType.SupplierReturn && item.SourceDocumentId == source.SupplierReturn.Id).ToListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                var existingResult = new InventorySupplierReturnPostingRecord(source.SupplierReturn.Id, existing.Select(item => item.Id).ToArray(), existing.Sum(item => item.Quantity), $"inventory-movement:{existing[0].Id:N}", InventoryValuationStatus.Pending, existing.Min(item => item.PostedAt), true, false, source.SupplierReturn.Scope.CompanyId, source.SupplierReturn.Scope.BranchId, source.Warehouse.WarehouseId);
                AddAudit(db, context, "supplier-return", source.SupplierReturn.Id, "inventory.supplier-return.post", command.ActorId, "Duplicate", "physical source document already posted", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, $"movement:{existing[0].Id}", $"movement:{existing[0].Id}", command.OccurredAt);
                AddReplay(db, context, "inventory.supplier-return.post", command.IdempotencyKey, command.RequestFingerprint, "supplier-return", source.SupplierReturn.Id, existingResult, command.OccurredAt);
                await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return existingResult;
            }

            var lines = source.Lines
                .Select(line =>
                {
                    var identity = new StockIdentityKey(
                        source.SupplierReturn.Scope.CompanyId,
                        source.SupplierReturn.Scope.BranchId,
                        source.Warehouse.WarehouseId,
                        line.ReturnLine.ProductId,
                        line.UnitOfMeasureId,
                        string.Empty);
                    return (Line: line, Identity: identity);
                })
                .ToArray();
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, lines.Select(item => item.Identity), cancellationToken);

            var availabilityByIdentity = new Dictionary<StockIdentityKey, (decimal OnHand, decimal Reserved)>();
            foreach (var identity in lines.Select(item => item.Identity).Distinct())
            {
                availabilityByIdentity[identity] = (
                    await SignedQuantityAsync(db, identity, cancellationToken),
                    await ActiveReservedQuantityAsync(db, identity, cancellationToken));
            }

            var stagedOutboundByIdentity = new Dictionary<StockIdentityKey, decimal>();
            foreach (var item in lines)
            {
                var staged = stagedOutboundByIdentity.GetValueOrDefault(item.Identity);
                var cumulativeOutbound = staged + item.Line.ReturnLine.ReturnQuantity;
                var availability = availabilityByIdentity[item.Identity];
                if (availability.OnHand - cumulativeOutbound < availability.Reserved) return null;
                stagedOutboundByIdentity[item.Identity] = cumulativeOutbound;
            }

            var movements = new List<InventoryStockMovementEntity>(lines.Length);
            foreach (var item in lines)
            {
                var line = item.Line;
                var movement = new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), source.SupplierReturn.Scope.CompanyId, source.SupplierReturn.Scope.BranchId, source.Warehouse.WarehouseId, source.Warehouse.Code, source.Warehouse.Name, line.ReturnLine.ProductId, line.Product.Sku, line.Product.Name, line.UnitOfMeasureId, line.ReceiptLine.UnitOfMeasureCode, InventoryMovementDirection.Outbound, line.ReturnLine.ReturnQuantity, null, null, InventoryValuationStatus.Pending, null, InventoryMovementSourceType.SupplierReturn, source.SupplierReturn.Id, line.ReturnLine.Id, null, source.SupplierReturn.ReturnDate, command.ActorId, command.CorrelationId, command.OccurredAt, source.GoodsReceipt.Id, line.ReceiptLine.Id, source.SupplierReturn.Id, line.ReturnLine.Id, source.SupplierReturn.PurchaseOrderId, line.ReturnLine.PurchaseOrderLineId, null, null, null);
                movements.Add(movement);
                db.StockMovements.Add(movement);
            }
            await db.SaveChangesAsync(cancellationToken);
            var result = new InventorySupplierReturnPostingRecord(source.SupplierReturn.Id, movements.Select(item => item.Id).ToArray(), movements.Sum(item => item.Quantity), $"inventory-movement:{movements[0].Id:N}", InventoryValuationStatus.Pending, command.OccurredAt, false, false, source.SupplierReturn.Scope.CompanyId, source.SupplierReturn.Scope.BranchId, source.Warehouse.WarehouseId);
            AddAudit(db, context, "supplier-return", source.SupplierReturn.Id, "inventory.supplier-return.post", command.ActorId, "Succeeded", "physical quantity posted", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"outbound:{result.Quantity}", command.OccurredAt);
            AddReplay(db, context, "inventory.supplier-return.post", command.IdempotencyKey, command.RequestFingerprint, "supplier-return", source.SupplierReturn.Id, result, command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<IReadOnlyList<InventoryTransferRecord>> ListTransfersAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var query = db.Transfers.AsNoTracking().Include(item => item.Lines).AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId);
        var transfers = (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).ToArray();
        var ids = transfers.Select(item => item.Id).ToArray();
        var events = (await db.TransferEvents.AsNoTracking().Where(item => ids.Contains(item.TransferId)).ToListAsync(cancellationToken)).OrderBy(item => item.OccurredAt).ToArray();
        return transfers.Select(item => ToTransfer(item, events.Where(eventItem => eventItem.TransferId == item.Id).ToArray())).ToArray();
    }

    public async Task<InventoryTransferRecord?> FindTransferAsync(InventoryRequestContext context, Guid transferId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var transfer = await db.Transfers.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == transferId, cancellationToken);
        if (transfer is null) return null;
        var events = (await db.TransferEvents.AsNoTracking().Where(item => item.TransferId == transferId).ToListAsync(cancellationToken)).OrderBy(item => item.OccurredAt).ToArray();
        return ToTransfer(transfer, events);
    }

    public async Task<InventoryTransferRecord?> CreateTransferAsync(InventoryRequestContext context, InventoryTransferCreateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryTransferRecord>(db, context, "inventory.transfer.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Handled) return replay.Value;
            if (command.SourceWarehouse.WarehouseId == command.DestinationWarehouse.WarehouseId) return null;
            var transfer = new InventoryTransferEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.SourceWarehouse.WarehouseId, command.SourceWarehouse.Code, command.SourceWarehouse.Name, command.DestinationWarehouse.WarehouseId, command.DestinationWarehouse.Code, command.DestinationWarehouse.Name, command.ProductId, command.Product.Sku, command.Product.Name, command.UnitOfMeasureId, command.Product.BaseUnitOfMeasureCode, command.Quantity, command.Mode, command.TrackingIdentity, command.Reason, command.ActorId, command.OccurredAt);
            var line = new InventoryTransferLineEntity(context.TenantId, Guid.NewGuid(), transfer.Id, command.Quantity); transfer.Lines.Add(line); db.Transfers.Add(transfer);
            db.TransferEvents.Add(new InventoryTransferEventEntity(context.TenantId, Guid.NewGuid(), transfer.Id, line.Id, InventoryTransferEventType.Created, command.Quantity, null, command.Reason, command.ActorId, command.CorrelationId, command.OccurredAt));
            AddAudit(db, context, "transfer", transfer.Id, "inventory.transfer.create", command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, "Draft", command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken); var result = ToTransfer(transfer, await db.TransferEvents.AsNoTracking().Where(item => item.TransferId == transfer.Id).ToListAsync(cancellationToken));
            AddReplay(db, context, "inventory.transfer.create", command.IdempotencyKey, command.RequestFingerprint, "transfer", transfer.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public Task<InventoryTransferRecord?> PostDirectTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => MutateTransferAsync(context, command, "inventory.transfer.direct", InventoryTransferEventType.DirectCompleted, async (db, transfer, line, now) =>
    {
        if (transfer.Mode != InventoryTransferMode.Direct || transfer.Status != InventoryTransferStatus.Draft) return null;
        var sourceKey = new StockIdentityKey(transfer.CompanyId, transfer.BranchId, transfer.SourceWarehouseId, transfer.ProductId, transfer.UnitOfMeasureId, transfer.TrackingIdentity ?? string.Empty);
        var destinationKey = new StockIdentityKey(transfer.CompanyId, transfer.BranchId, transfer.DestinationWarehouseId, transfer.ProductId, transfer.UnitOfMeasureId, transfer.TrackingIdentity ?? string.Empty);
        await AcquireConcurrencyAnchorsAsync(db, context.TenantId, [sourceKey, destinationKey], cancellationToken);
        if (!await HasOutboundCapacityAsync(db, sourceKey, transfer.Quantity, cancellationToken)) return null;
        var sourceMovement = NewTransferMovement(context, transfer, InventoryMovementDirection.Outbound, InventoryMovementSourceType.WarehouseTransferShipment, Guid.NewGuid(), line.Id, transfer.SourceWarehouseId, transfer.SourceWarehouseCode, transfer.SourceWarehouseName, transfer.Quantity, now);
        var destinationMovement = NewTransferMovement(context, transfer, InventoryMovementDirection.Inbound, InventoryMovementSourceType.WarehouseTransferReceipt, Guid.NewGuid(), line.Id, transfer.DestinationWarehouseId, transfer.DestinationWarehouseCode, transfer.DestinationWarehouseName, transfer.Quantity, now);
        db.StockMovements.AddRange(sourceMovement, destinationMovement); transfer.SetStatus(InventoryTransferStatus.Completed, now); return new TransferMutationOutcome(transfer, line, sourceMovement, destinationMovement, transfer.Quantity);
    }, cancellationToken);

    public Task<InventoryTransferRecord?> ShipTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => MutateTransferAsync(context, command, "inventory.transfer.ship", InventoryTransferEventType.Shipped, async (db, transfer, line, now) =>
    {
        if (transfer.Mode != InventoryTransferMode.InTransit || transfer.Status != InventoryTransferStatus.Draft || command.Quantity is not null && command.Quantity != transfer.Quantity) return null;
        var sourceKey = new StockIdentityKey(transfer.CompanyId, transfer.BranchId, transfer.SourceWarehouseId, transfer.ProductId, transfer.UnitOfMeasureId, transfer.TrackingIdentity ?? string.Empty);
        await AcquireConcurrencyAnchorsAsync(db, context.TenantId, [sourceKey], cancellationToken);
        if (!await HasOutboundCapacityAsync(db, sourceKey, transfer.Quantity, cancellationToken)) return null;
        var sourceMovement = NewTransferMovement(context, transfer, InventoryMovementDirection.Outbound, InventoryMovementSourceType.WarehouseTransferShipment, Guid.NewGuid(), line.Id, transfer.SourceWarehouseId, transfer.SourceWarehouseCode, transfer.SourceWarehouseName, transfer.Quantity, now);
        db.StockMovements.Add(sourceMovement); transfer.SetStatus(InventoryTransferStatus.Shipped, now); return new TransferMutationOutcome(transfer, line, sourceMovement, null, transfer.Quantity);
    }, cancellationToken);

    public Task<InventoryTransferRecord?> ReceiveTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default)
    {
        var canonicalCommand = command with { Reference = InventoryTransferReferencePolicy.Normalize(command.Reference) };
        return MutateTransferAsync(context, canonicalCommand, "inventory.transfer.receive", InventoryTransferEventType.Received, async (db, transfer, line, now) =>
    {
        if (transfer.Mode != InventoryTransferMode.InTransit || transfer.Status is not (InventoryTransferStatus.Shipped or InventoryTransferStatus.PartiallyReceived) || canonicalCommand.Quantity is not > 0 || string.IsNullOrWhiteSpace(canonicalCommand.Reference)) return null;
        var destinationKey = new StockIdentityKey(transfer.CompanyId, transfer.BranchId, transfer.DestinationWarehouseId, transfer.ProductId, transfer.UnitOfMeasureId, transfer.TrackingIdentity ?? string.Empty);
        await AcquireConcurrencyAnchorsAsync(db, context.TenantId, [destinationKey], cancellationToken);
        var prior = await db.TransferEvents.Where(item => item.TransferId == transfer.Id).ToListAsync(cancellationToken);
        if (prior.Any(item => item.EventType == InventoryTransferEventType.Received
            && InventoryTransferReferencePolicy.Normalize(item.Reference) == canonicalCommand.Reference))
        {
            return new TransferMutationOutcome(transfer, line, null, null, 0m, false);
        }

        var shipped = prior.Where(item => item.EventType == InventoryTransferEventType.Shipped).Sum(item => item.Quantity); var received = prior.Where(item => item.EventType == InventoryTransferEventType.Received).Sum(item => item.Quantity); var lost = prior.Where(item => item.EventType == InventoryTransferEventType.ShortageResolved).Sum(item => item.Quantity);
        if (canonicalCommand.Quantity.Value > shipped - received - lost) return null;
        var destinationMovement = NewTransferMovement(context, transfer, InventoryMovementDirection.Inbound, InventoryMovementSourceType.WarehouseTransferReceipt, Guid.NewGuid(), Guid.NewGuid(), transfer.DestinationWarehouseId, transfer.DestinationWarehouseCode, transfer.DestinationWarehouseName, canonicalCommand.Quantity.Value, now, line.Id);
        db.StockMovements.Add(destinationMovement); var newReceived = received + canonicalCommand.Quantity.Value; transfer.SetStatus(newReceived == transfer.Quantity ? InventoryTransferStatus.Completed : InventoryTransferStatus.PartiallyReceived, now); return new TransferMutationOutcome(transfer, line, null, destinationMovement, canonicalCommand.Quantity.Value);
    }, cancellationToken);
    }

    public Task<InventoryTransferRecord?> ResolveTransferShortageAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => MutateTransferAsync(context, command, "inventory.transfer.shortage-resolve", InventoryTransferEventType.ShortageResolved, async (db, transfer, line, now) =>
    {
        if (transfer.Mode != InventoryTransferMode.InTransit || transfer.Status is not (InventoryTransferStatus.Shipped or InventoryTransferStatus.PartiallyReceived) || string.IsNullOrWhiteSpace(command.Reference)) return null;
        var prior = await db.TransferEvents.Where(item => item.TransferId == transfer.Id).ToListAsync(cancellationToken); var shipped = prior.Where(item => item.EventType == InventoryTransferEventType.Shipped).Sum(item => item.Quantity); var received = prior.Where(item => item.EventType == InventoryTransferEventType.Received).Sum(item => item.Quantity); var lost = prior.Where(item => item.EventType == InventoryTransferEventType.ShortageResolved).Sum(item => item.Quantity); var remaining = shipped - received - lost;
        if (remaining <= 0) return null;
        transfer.SetStatus(InventoryTransferStatus.LossResolved, now); return new TransferMutationOutcome(transfer, line, null, null, remaining);
    }, cancellationToken);

    public Task<InventoryTransferRecord?> CancelTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => MutateTransferAsync(context, command, "inventory.transfer.cancel", InventoryTransferEventType.Cancelled, (db, transfer, line, now) =>
    {
        if (transfer.Status != InventoryTransferStatus.Draft) return Task.FromResult<TransferMutationOutcome?>(null);
        transfer.SetStatus(InventoryTransferStatus.Cancelled, now); return Task.FromResult<TransferMutationOutcome?>(new TransferMutationOutcome(transfer, line, null, null, 0m));
    }, cancellationToken);

    public async Task<IReadOnlyList<InventoryTransferEventRecord>> ReadTransferHistoryAsync(InventoryRequestContext context, Guid transferId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); return (await db.TransferEvents.AsNoTracking().Where(item => item.TransferId == transferId).ToListAsync(cancellationToken)).OrderBy(item => item.OccurredAt).Select(ToTransferEvent).ToArray();
    }

    private async Task<InventoryTransferRecord?> MutateTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, string operationId, InventoryTransferEventType eventType, Func<InventoryDbContext, InventoryTransferEntity, InventoryTransferLineEntity, DateTimeOffset, Task<TransferMutationOutcome?>> mutate, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryTransferRecord>(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Handled) return replay.Value;
            var transfer = await db.Transfers.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.TransferId, cancellationToken); if (transfer is null || !transfer.Version.SequenceEqual(command.ExpectedVersion)) return null;
            var line = transfer.Lines.Single(); var outcome = await mutate(db, transfer, line, command.OccurredAt); if (outcome is null) return null;
            if (!outcome.RecordEvent)
            {
                AddAudit(db, context, "transfer", transfer.Id, operationId, command.ActorId, "Duplicate", "physical receipt reference already recorded", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, "duplicate-receipt-reference", command.OccurredAt);
                await db.SaveChangesAsync(cancellationToken);
                var existingEvents = (await db.TransferEvents.AsNoTracking().Where(item => item.TransferId == transfer.Id).ToListAsync(cancellationToken)).OrderBy(item => item.OccurredAt).ToArray();
                var existingResult = ToTransfer(transfer, existingEvents);
                AddReplay(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, "transfer", transfer.Id, existingResult, command.OccurredAt);
                await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return existingResult;
            }

            var value = outcome; var eventEntity = new InventoryTransferEventEntity(context.TenantId, Guid.NewGuid(), transfer.Id, line.Id, eventType, value.Quantity, command.Reference, command.Reason, command.ActorId, command.CorrelationId, command.OccurredAt, value.SourceMovement?.Id, value.DestinationMovement?.Id); db.TransferEvents.Add(eventEntity);
            AddAudit(db, context, "transfer", transfer.Id, operationId, command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, transfer.Status.ToString(), command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken); var events = (await db.TransferEvents.AsNoTracking().Where(item => item.TransferId == transfer.Id).ToListAsync(cancellationToken)).OrderBy(item => item.OccurredAt).ToArray(); var result = ToTransfer(transfer, events); AddReplay(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, "transfer", transfer.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<bool> HasActiveGoodsReceiptEffectAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default)
    {
        await using var db = new InventoryDbContext(options, tenantContext); return await db.StockMovements.AsNoTracking().AnyAsync(item => item.GoodsReceiptId == goodsReceiptId && item.Direction == InventoryMovementDirection.Inbound && item.SourceType == InventoryMovementSourceType.GoodsReceipt, cancellationToken);
    }

    public async Task<bool> HasActiveSupplierReturnEffectAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default)
    {
        await using var db = new InventoryDbContext(options, tenantContext);
        return await db.StockMovements.AsNoTracking().AnyAsync(
            item => item.SupplierReturnId == supplierReturnId
                && item.Direction == InventoryMovementDirection.Outbound
                && item.SourceType == InventoryMovementSourceType.SupplierReturn,
            cancellationToken);
    }

    private static InventoryGoodsReceiptPostingRecord ToGoodsReceiptPosting(InventoryStockMovementEntity movement, bool wasExisting) =>
        new(movement.Id, movement.TenantId.Value, movement.GoodsReceiptId ?? movement.SourceDocumentId, movement.GoodsReceiptLineId ?? movement.SourceLineId, movement.CompanyId, movement.BranchId, movement.WarehouseId, movement.WarehouseCode, movement.WarehouseName, movement.ProductId, movement.ProductSku, movement.ProductName, movement.UnitOfMeasureId, movement.UnitOfMeasureCode, movement.Quantity, movement.ValuationStatus, movement.PostedAt, wasExisting);

    private static InventoryStockMovementEntity NewTransferMovement(
        InventoryRequestContext context,
        InventoryTransferEntity transfer,
        InventoryMovementDirection direction,
        InventoryMovementSourceType sourceType,
        Guid movementId,
        Guid sourceLineId,
        Guid warehouseId,
        string warehouseCode,
        string warehouseName,
        decimal quantity,
        DateTimeOffset at,
        Guid? transferLineId = null) =>
        new(context.TenantId, movementId, transfer.CompanyId, transfer.BranchId, warehouseId, warehouseCode, warehouseName, transfer.ProductId, transfer.ProductSku, transfer.ProductName, transfer.UnitOfMeasureId, transfer.UnitOfMeasureCode, direction, quantity, null, null, InventoryValuationStatus.Pending, transfer.TrackingIdentity, sourceType, transfer.Id, sourceLineId, null, DateOnly.FromDateTime(at.UtcDateTime), context.ActorId, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), at, null, null, null, null, null, null, transfer.Id, transferLineId ?? sourceLineId, null);

    private static async Task<bool> HasOutboundCapacityAsync(InventoryDbContext db, StockIdentityKey identity, decimal quantity, CancellationToken cancellationToken)
    {
        var onHand = await SignedQuantityAsync(db, identity, cancellationToken);
        var reserved = await ActiveReservedQuantityAsync(db, identity, cancellationToken);
        return quantity > 0m && onHand - quantity >= reserved;
    }

    private static InventoryTransferRecord ToTransfer(InventoryTransferEntity transfer, IReadOnlyList<InventoryTransferEventEntity> events)
    {
        var shipped = events.Where(item => item.EventType == InventoryTransferEventType.Shipped).Sum(item => item.Quantity);
        var received = events.Where(item => item.EventType == InventoryTransferEventType.Received).Sum(item => item.Quantity);
        var lost = events.Where(item => item.EventType == InventoryTransferEventType.ShortageResolved).Sum(item => item.Quantity);
        if (events.Any(item => item.EventType == InventoryTransferEventType.DirectCompleted)) { shipped = transfer.Quantity; received = transfer.Quantity; }
        var inTransit = Math.Max(0m, shipped - received - lost);
        var remainingToShip = Math.Max(0m, transfer.Quantity - shipped);
        return new InventoryTransferRecord(transfer.Id, transfer.TenantId.Value, transfer.CompanyId, transfer.BranchId, transfer.SourceWarehouseId, transfer.SourceWarehouseCode, transfer.SourceWarehouseName, transfer.DestinationWarehouseId, transfer.DestinationWarehouseCode, transfer.DestinationWarehouseName, transfer.ProductId, transfer.ProductSku, transfer.ProductName, transfer.UnitOfMeasureId, transfer.UnitOfMeasureCode, transfer.Quantity, transfer.Mode, transfer.Status, transfer.TrackingIdentity, shipped, received, lost, inTransit, remainingToShip, transfer.Reason, transfer.ActorId, transfer.CreatedAt, transfer.UpdatedAt, events.Select(ToTransferEvent).ToArray(), transfer.Version);
    }

    private static InventoryTransferEventRecord ToTransferEvent(InventoryTransferEventEntity item) =>
        new(item.Id, item.TransferId, item.TransferLineId, item.EventType, item.Quantity, item.Reference, item.Reason, item.ActorId, item.CorrelationId, item.OccurredAt, item.SourceMovementId, item.DestinationMovementId, item.Version);

    private sealed record TransferMutationOutcome(
        InventoryTransferEntity Transfer,
        InventoryTransferLineEntity Line,
        InventoryStockMovementEntity? SourceMovement,
        InventoryStockMovementEntity? DestinationMovement,
        decimal Quantity,
        bool RecordEvent = true);

    public async Task<IReadOnlyList<InventoryAuditRecord>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await db.Audit.AsNoTracking().Where(item => item.ResourceType == resourceType && item.ResourceId == resourceId).ToListAsync(cancellationToken); return values.OrderBy(item => item.OccurredAt).Select(item => new InventoryAuditRecord(item.Id, item.TenantId.Value, item.ResourceType, item.ResourceId, item.OperationId, item.ActorId, item.SessionId, item.AuthorizationPath, item.Decision, item.Reason, item.CorrelationId, item.IdempotencyKey, item.RequestFingerprint, item.BeforeSummary, item.AfterSummary, item.OccurredAt, item.Version)).ToArray();
    }

    private static async Task<HashSet<string>> ReadConsumedSourceFingerprintsAsync(
        InventoryDbContext db,
        IEnumerable<string> sourceFingerprints,
        CancellationToken cancellationToken)
    {
        var requested = sourceFingerprints
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
        if (requested.Count == 0)
        {
            return [];
        }

        var consumed = await db.OpeningBalanceRows
            .AsNoTracking()
            .Where(item => item.SourceIdentityConsumed)
            .Select(item => item.SourceFingerprint)
            .ToListAsync(cancellationToken);
        consumed.RemoveAll(item => !requested.Contains(item));
        return consumed.ToHashSet(StringComparer.Ordinal);
    }

    private static async Task AcquireConcurrencyAnchorsAsync(
        InventoryDbContext db,
        TenantId tenantId,
        IEnumerable<StockIdentityKey> identities,
        CancellationToken cancellationToken)
    {
        foreach (var identity in identities
                     .Distinct()
                     .OrderBy(item => item.CompanyId)
                     .ThenBy(item => item.BranchId ?? Guid.Empty)
                     .ThenBy(item => item.WarehouseId)
                     .ThenBy(item => item.ProductId)
                     .ThenBy(item => item.UnitOfMeasureId)
                     .ThenBy(item => item.TrackingKey, StringComparer.Ordinal))
        {
            var anchor = await db.ConcurrencyAnchors.SingleOrDefaultAsync(
                item => item.CompanyId == identity.CompanyId
                    && item.BranchId == identity.BranchId
                    && item.WarehouseId == identity.WarehouseId
                    && item.ProductId == identity.ProductId
                    && item.UnitOfMeasureId == identity.UnitOfMeasureId
                    && item.TrackingKey == identity.TrackingKey,
                cancellationToken);
            if (anchor is null)
            {
                anchor = new InventoryConcurrencyAnchorEntity(
                    tenantId,
                    Guid.NewGuid(),
                    identity.CompanyId,
                    identity.BranchId,
                    identity.WarehouseId,
                    identity.ProductId,
                    identity.UnitOfMeasureId,
                    identity.TrackingKey);
                anchor.Touch();
                db.ConcurrencyAnchors.Add(anchor);
            }
            else
            {
                anchor.Touch();
            }
        }

        // Keep the anchor write inside the caller's transaction. The write lock
        // then serializes reservations and corrections for the same identity.
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task<decimal> SignedQuantityAsync(
        InventoryDbContext db,
        StockIdentityKey identity,
        CancellationToken cancellationToken) =>
        SignedQuantityAsync(
            db,
            identity.CompanyId,
            identity.BranchId,
            identity.WarehouseId,
            identity.ProductId,
            identity.UnitOfMeasureId,
            identity.TrackingKey == string.Empty ? null : identity.TrackingKey,
            cancellationToken);

    private static async Task<decimal> SignedQuantityAsync(InventoryDbContext db, Guid companyId, Guid? branchId, Guid warehouseId, Guid productId, Guid uomId, string? trackingIdentity, CancellationToken cancellationToken)
    {
        var normalizedTracking = trackingIdentity == string.Empty ? null : trackingIdentity;
        var inbound = await db.StockMovements.Where(item => item.CompanyId == companyId && item.BranchId == branchId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == uomId && item.TrackingIdentity == normalizedTracking && item.Direction == InventoryMovementDirection.Inbound).SumAsync(item => (decimal?)item.Quantity, cancellationToken) ?? 0;
        var outbound = await db.StockMovements.Where(item => item.CompanyId == companyId && item.BranchId == branchId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == uomId && item.TrackingIdentity == normalizedTracking && item.Direction == InventoryMovementDirection.Outbound).SumAsync(item => (decimal?)item.Quantity, cancellationToken) ?? 0;
        return inbound - outbound;
    }

    private static async Task<decimal> ActiveReservedQuantityAsync(
        InventoryDbContext db,
        StockIdentityKey identity,
        CancellationToken cancellationToken)
    {
        return await db.Reservations
            .Where(item => item.CompanyId == identity.CompanyId
                && item.BranchId == identity.BranchId
                && item.WarehouseId == identity.WarehouseId
                && item.ProductId == identity.ProductId
                && item.UnitOfMeasureId == identity.UnitOfMeasureId
                && item.TrackingIdentity == (identity.TrackingKey == string.Empty ? null : identity.TrackingKey)
                && item.Status == InventoryReservationStatus.Active)
            .SumAsync(item => (decimal?)item.ReservedQuantity, cancellationToken) ?? 0;
    }

    private static async Task<decimal> CalculateAvailableAsync(InventoryDbContext db, InventoryScope scope, Guid productId, Guid uomId, string? trackingIdentity, CancellationToken cancellationToken)
    {
        var onHand = await SignedQuantityAsync(db, scope.CompanyId, scope.BranchId, scope.WarehouseId, productId, uomId, trackingIdentity, cancellationToken);
        var reserved = await db.Reservations.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId && item.ProductId == productId && item.UnitOfMeasureId == uomId && item.TrackingIdentity == trackingIdentity && item.Status == InventoryReservationStatus.Active).SumAsync(item => (decimal?)item.ReservedQuantity, cancellationToken) ?? 0;
        return Math.Max(0, onHand - reserved);
    }

    private static void AddAudit(InventoryDbContext db, InventoryRequestContext context, string resourceType, Guid resourceId, string operationId, Guid actorId, string decision, string? reason, string correlationId, string? idempotencyKey, string? fingerprint, string? before, string? after, DateTimeOffset at) => db.Audit.Add(new InventoryAuditEntity(context.TenantId, Guid.NewGuid(), resourceType, resourceId, operationId, actorId, context.SessionId, context.AuthorizationPath.ToString(), decision, reason, correlationId, idempotencyKey, fingerprint, before, after, at));

    private static void AddReplay<T>(InventoryDbContext db, InventoryRequestContext context, string operationId, string? key, string fingerprint, string resourceType, Guid resourceId, T snapshot, DateTimeOffset at)
    { if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new InventoryIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operationId, key!, fingerprint, resourceType, resourceId, JsonSerializer.Serialize(snapshot), at)); }

    private static async Task<(bool Handled, T? Value)> ReadReplayAsync<T>(InventoryDbContext db, InventoryRequestContext context, string operationId, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return (false, default);
        var entry = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.OperationId == operationId && item.Key == key, cancellationToken);
        if (entry is null) return (false, default);
        return entry.Fingerprint == fingerprint ? (true, JsonSerializer.Deserialize<T>(entry.SnapshotJson)) : (true, default);
    }

    private static async Task<InventoryReplayProbe<T>> ProbeReplayAsync<T>(InventoryDbContext db, InventoryRequestContext context, string operationId, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return InventoryReplayProbe<T>.NotFound;
        }

        var entry = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == context.TenantId
                && item.ActorId == context.ActorId
                && item.OperationId == operationId
                && item.Key == key,
            cancellationToken);
        if (entry is null)
        {
            return InventoryReplayProbe<T>.NotFound;
        }

        if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return InventoryReplayProbe<T>.Conflict;
        }

        var value = JsonSerializer.Deserialize<T>(entry.SnapshotJson);
        return value is null ? InventoryReplayProbe<T>.Conflict : InventoryReplayProbe<T>.ForReplay(value);
    }

    private static readonly System.Linq.Expressions.Expression<Func<InventoryStockMovementEntity, InventoryMovementRecord>> ToMovement = item => new InventoryMovementRecord(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureId, item.UnitOfMeasureCode, item.Direction, item.Quantity, item.UnitCost, item.CurrencyCode, item.TrackingIdentity, item.SourceType, item.SourceDocumentId, item.SourceLineId, item.CorrectionOfMovementId, item.EffectiveDate, item.ActorId, item.CorrelationId, item.PostedAt, item.Version, item.ValuationStatus, item.GoodsReceiptId, item.GoodsReceiptLineId, item.SupplierReturnId, item.SupplierReturnLineId, item.PurchaseOrderId, item.PurchaseOrderLineId, item.TransferId, item.TransferLineId, item.SourceReference, item.LedgerSequence);
    private static readonly System.Linq.Expressions.Expression<Func<InventoryReservationEntity, InventoryReservationRecord>> ToReservation = item => new InventoryReservationRecord(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureId, item.UnitOfMeasureCode, item.TrackingIdentity, item.SourceType, item.SourceReference, item.RequestedQuantity, item.ReservedQuantity, item.UnallocatedQuantity, item.Status, item.ActorId, item.CreatedAt, item.UpdatedAt, item.Version, item.FulfilledQuantity, item.SourceDocumentId, item.SourceLineId, item.SourceRevision);

    private static InventoryOpeningBalanceRecord ToOpening(InventoryOpeningBalanceEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.AsOfDate, item.SourceOwner, item.SourceSystem, item.ExtractedAt, item.SourceReference, item.Status, item.Rows.Count, item.Rows.Count(row => row.Status is InventoryOpeningRowStatus.Valid or InventoryOpeningRowStatus.Posted), item.Rows.Count(row => row.Status == InventoryOpeningRowStatus.Quarantined), item.Rows.Where(row => row.Status is InventoryOpeningRowStatus.Valid or InventoryOpeningRowStatus.Posted).Sum(row => row.Quantity), item.CreatedAt, item.UpdatedAt, item.Rows.OrderBy(row => row.Id).Select(row => new InventoryOpeningBalanceRowRecord(row.Id, row.ProductId, row.ProductSku, row.ProductName, row.UnitOfMeasureId, row.UnitOfMeasureCode, row.Quantity, row.UnitCost, row.CurrencyCode, row.TrackingIdentity, row.SourceLineReference, row.Status, row.ValidationCode, row.PostedAt, row.Version, row.SourceFingerprint)).ToArray(), item.Version);

    private sealed record StockIdentityKey(
        Guid CompanyId,
        Guid? BranchId,
        Guid WarehouseId,
        Guid ProductId,
        Guid UnitOfMeasureId,
        string TrackingKey)
    {
        internal static StockIdentityKey From(InventoryReservationCommand command) =>
            new(command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.ProductId, command.UnitOfMeasureId, command.TrackingIdentity ?? string.Empty);

        internal static StockIdentityKey From(InventoryStockMovementEntity movement) =>
            new(movement.CompanyId, movement.BranchId, movement.WarehouseId, movement.ProductId, movement.UnitOfMeasureId, movement.TrackingIdentity ?? string.Empty);

        internal static StockIdentityKey From(InventoryReservationEntity reservation) =>
            new(reservation.CompanyId, reservation.BranchId, reservation.WarehouseId, reservation.ProductId, reservation.UnitOfMeasureId, reservation.TrackingIdentity ?? string.Empty);

        internal static StockIdentityKey From(InventoryAdjustmentLineEntity line) =>
            new(line.Adjustment.CompanyId, line.Adjustment.BranchId, line.Adjustment.WarehouseId, line.ProductId, line.UnitOfMeasureId, line.TrackingIdentity);

        internal static StockIdentityKey From(InventoryStockIssueLineEntity line) =>
            new(line.StockIssue.CompanyId, line.StockIssue.BranchId, line.StockIssue.WarehouseId, line.ProductId, line.UnitOfMeasureId, line.TrackingIdentity);

        internal static StockIdentityKey From(InventoryCountLineEntity line) =>
            new(line.Count.CompanyId, line.Count.BranchId, line.Count.WarehouseId, line.ProductId, line.UnitOfMeasureId, line.TrackingIdentity);
    }
}

#pragma warning restore CS1591
