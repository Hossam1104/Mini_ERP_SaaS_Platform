#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public sealed class PurchaseInvoiceMatchPersistence : IPurchaseInvoiceMatchPersistence, IPurchaseInvoiceMatchReportingReadPort
{
    private const int ReplayResponseSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions options;

    internal PurchaseInvoiceMatchPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<PurchaseInvoiceMatchListRecord>> ListAsync(
        TenantContext tenantContext,
        Guid? handoffId,
        PurchaseInvoiceMatchResult? result,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = db.PurchaseInvoiceMatchEvaluations.AsNoTracking();
        if (handoffId is { } selectedHandoffId)
        {
            query = query.Where(item => item.PurchaseInvoiceHandoffId == selectedHandoffId);
        }

        if (result is { } selectedResult)
        {
            query = query.Where(item => item.Result == selectedResult);
        }

        var entities = await query.ToListAsync(cancellationToken);
        return entities
            .OrderByDescending(item => item.EvaluatedAt)
            .ThenByDescending(item => item.Id)
            .Select(ToListRecord)
            .ToArray();
    }

    public async Task<ReportingSourcePage<PurchaseInvoiceMatchReportingRecord>> ListReportingPageAsync(
        TenantContext tenantContext,
        PurchaseInvoiceMatchResult? result,
        DateOnly? fromDate,
        DateOnly? toDate,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var evaluations = ApplyTrustedScope(db.PurchaseInvoiceMatchEvaluations.AsNoTracking(), tenantContext.Scope);
        if (result is { } selectedResult) evaluations = evaluations.Where(item => item.Result == selectedResult);
        if (fromDate is { } from) evaluations = evaluations.Where(item => item.EvaluatedAt >= new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));
        if (toDate is { } to) evaluations = evaluations.Where(item => item.EvaluatedAt < new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));

        var total = await evaluations.CountAsync(cancellationToken);
        DateTimeOffset? dataAsOf = null;
        if (total > 0)
        {
            dataAsOf = db.Database.IsSqlite()
                ? (await evaluations.Select(item => item.EvaluatedAt).ToListAsync(cancellationToken)).Max()
                : await evaluations.Select(item => (DateTimeOffset?)item.EvaluatedAt).MaxAsync(cancellationToken);
        }
        var ordered = page.SortBy?.ToLowerInvariant() switch
        {
            "status" => page.SortDirection == "desc" ? evaluations.OrderByDescending(item => item.Result).ThenByDescending(item => item.Id) : evaluations.OrderBy(item => item.Result).ThenBy(item => item.Id),
            "lifecycle" => page.SortDirection == "desc" ? evaluations.OrderByDescending(item => item.Lifecycle).ThenByDescending(item => item.Id) : evaluations.OrderBy(item => item.Lifecycle).ThenBy(item => item.Id),
            _ => page.SortDirection == "asc" ? evaluations.OrderBy(item => item.EvaluatedAt).ThenBy(item => item.Id) : evaluations.OrderByDescending(item => item.EvaluatedAt).ThenByDescending(item => item.Id)
        };
        var values = await ordered.Skip(page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        return new ReportingSourcePage<PurchaseInvoiceMatchReportingRecord>(values.Select(ToReportingRecord).ToArray(), total, "evaluatedAt,id", dataAsOf);
    }

    public async Task<PurchaseInvoiceMatchRecord?> FindAsync(
        TenantContext tenantContext,
        Guid matchEvaluationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PurchaseInvoiceMatchEvaluations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == matchEvaluationId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<PurchaseInvoiceMatchRecord?> FindCurrentForHandoffAsync(
        TenantContext tenantContext,
        Guid handoffId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PurchaseInvoiceMatchEvaluations
            .AsNoTracking()
            .Where(item => item.PurchaseInvoiceHandoffId == handoffId && item.Lifecycle == PurchaseInvoiceMatchLifecycle.Current)
            .OrderByDescending(item => item.EvaluatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> EvaluateAsync(
        TenantContext tenantContext,
        PurchaseInvoiceMatchEvaluateCommand command,
        PurchaseInvoiceMatchAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        string? fingerprint = null;
        try
        {
            PurchaseInvoiceMatchRecord? replay;
            try { replay = await FindReplayAsync(db, evidence, cancellationToken); }
            catch (ReplayConflictException) { return Denied(PurchaseInvoiceMatchPersistenceOutcome.Conflict, "idempotency_conflict"); }
            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>.Success(replay);
            }

            var handoff = await LoadHandoffAsync(db, command.PurchaseInvoiceHandoffId, cancellationToken);
            if (handoff is null)
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.NotFound, "invoice_handoff_not_found");
            }

            if (!handoff.Version.SequenceEqual(command.ExpectedHandoffVersion))
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            if (handoff.Status != PurchaseInvoiceHandoffStatus.Recorded)
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.InvalidState, "invoice_handoff_not_active");
            }

            var order = await db.PurchaseOrders
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == handoff.PurchaseOrderId, cancellationToken);
            if (order is null)
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.NotFound, "purchase_order_not_found");
            }

            var receipts = await db.GoodsReceipts
                .Include(item => item.Lines)
                .Where(item => item.PurchaseOrderId == order.Id)
                .ToListAsync(cancellationToken);
            var activeHandoffs = await LoadActiveHandoffsAsync(db, order.Id, cancellationToken);
            var evidenceVersion = handoff.DeclaredEvidenceVersions.SingleOrDefault(item => item.IsCurrent);
            var source = BuildSourceSnapshot(handoff, order, receipts, activeHandoffs, evidenceVersion, command.Policy, command.AppliedExchangeRate);
            var sourceJson = JsonSerializer.Serialize(source, JsonOptions);
            fingerprint = Fingerprint(sourceJson);

            var existing = await db.PurchaseInvoiceMatchEvaluations
                .SingleOrDefaultAsync(item => item.PurchaseInvoiceHandoffId == handoff.Id && item.SourceFingerprint == fingerprint, cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>.Success(ToRecord(existing));
            }

            var evaluation = EvaluateSources(handoff, order, receipts, activeHandoffs, evidenceVersion, command.Policy, command.AppliedExchangeRate);
            var id = evidence.MatchEvaluationId == Guid.Empty ? Guid.NewGuid() : evidence.MatchEvaluationId;
            var entity = new PurchaseInvoiceMatchEvaluationEntity(
                tenantContext.TenantId,
                command,
                id,
                order.Id,
                new PurchaseRequestScope(handoff.TenantId.Value, handoff.CompanyId, handoff.BranchId),
                evaluation.Result,
                fingerprint,
                order.Version.ToArray(),
                handoff.Version.ToArray(),
                evidenceVersion?.Id,
                evidenceVersion?.VersionNumber,
                JsonSerializer.Serialize(command.Policy, JsonOptions),
                command.AppliedExchangeRate is null ? null : JsonSerializer.Serialize(command.AppliedExchangeRate, JsonOptions),
                JsonSerializer.Serialize(evaluation.Variances, JsonOptions),
                sourceJson);

            var current = await db.PurchaseInvoiceMatchEvaluations
                .Where(item => item.PurchaseInvoiceHandoffId == handoff.Id && item.Lifecycle == PurchaseInvoiceMatchLifecycle.Current)
                .ToListAsync(cancellationToken);
            foreach (var previous in current)
            {
                previous.Supersede();
                previous.TouchVersion();
            }

            db.PurchaseInvoiceMatchEvaluations.Add(entity);
            db.PurchaseInvoiceMatchHistory.Add(new PurchaseInvoiceMatchHistoryEntity(
                tenantContext.TenantId,
                Guid.NewGuid(),
                entity.Id,
                handoff.Id,
                entity.Result,
                "evaluated",
                command.ActorId,
                null,
                command.OccurredAt,
                command.CorrelationId));
            var audit = new PurchaseInvoiceMatchAuditEntity(evidence with { MatchEvaluationId = entity.Id });
            db.PurchaseInvoiceMatchAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(PurchaseInvoiceMatchPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var winner = fingerprint is null
                ? null
                : await FindByFingerprintAsync(tenantContext, command.PurchaseInvoiceHandoffId, fingerprint, cancellationToken);
            return winner is null
                ? Denied(PurchaseInvoiceMatchPersistenceOutcome.Duplicate, "match_evaluation_duplicate")
                : PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>.Success(winner);
        }
        catch (DbUpdateException)
        {
            return Denied(PurchaseInvoiceMatchPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(PurchaseInvoiceMatchPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> ResolveAsync(
        TenantContext tenantContext,
        PurchaseInvoiceMatchResolveCommand command,
        PurchaseInvoiceMatchAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            PurchaseInvoiceMatchRecord? replay;
            try { replay = await FindReplayAsync(db, evidence, cancellationToken); }
            catch (ReplayConflictException) { return Denied(PurchaseInvoiceMatchPersistenceOutcome.Conflict, "idempotency_conflict"); }
            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>.Success(replay);
            }

            var entity = await db.PurchaseInvoiceMatchEvaluations.SingleOrDefaultAsync(item => item.Id == command.MatchEvaluationId, cancellationToken);
            if (entity is null)
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.NotFound, "match_evaluation_not_found");
            }

            if (!entity.Version.SequenceEqual(command.ExpectedMatchVersion))
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            if (entity.Lifecycle != PurchaseInvoiceMatchLifecycle.Current)
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.InvalidState, "stale_evaluation");
            }

            if (entity.Result != PurchaseInvoiceMatchResult.ExceptionHold)
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.InvalidState, "match_resolution_not_allowed");
            }

            var handoff = await LoadHandoffAsync(db, entity.PurchaseInvoiceHandoffId, cancellationToken);
            var order = handoff is null
                ? null
                : await db.PurchaseOrders.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == handoff.PurchaseOrderId, cancellationToken);
            if (handoff is null || order is null || handoff.Status != PurchaseInvoiceHandoffStatus.Recorded)
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.InvalidState, "stale_evaluation");
            }

            var receipts = await db.GoodsReceipts.Include(item => item.Lines).Where(item => item.PurchaseOrderId == order.Id).ToListAsync(cancellationToken);
            var activeHandoffs = await LoadActiveHandoffsAsync(db, order.Id, cancellationToken);
            var evidenceVersion = handoff.DeclaredEvidenceVersions.SingleOrDefault(item => item.IsCurrent);
            var policy = JsonSerializer.Deserialize<PurchaseInvoiceMatchingToleranceDefinition>(entity.PolicySnapshotJson, JsonOptions) ?? PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(command.OccurredAt);
            var fx = string.IsNullOrWhiteSpace(entity.ExchangeRateSnapshotJson)
                ? null
                : JsonSerializer.Deserialize<PurchaseInvoiceMatchExchangeRateRecord>(entity.ExchangeRateSnapshotJson, JsonOptions);
            var sourceJson = JsonSerializer.Serialize(BuildSourceSnapshot(handoff, order, receipts, activeHandoffs, evidenceVersion, policy, fx), JsonOptions);
            if (!string.Equals(entity.SourceFingerprint, Fingerprint(sourceJson), StringComparison.Ordinal)
                || !entity.HandoffVersion.SequenceEqual(handoff.Version)
                || !entity.PurchaseOrderVersion.SequenceEqual(order.Version))
            {
                return Denied(PurchaseInvoiceMatchPersistenceOutcome.InvalidState, "stale_evaluation");
            }

            entity.Resolve(
                command.ActorId,
                command.Reason,
                JsonSerializer.Serialize(command.Policy, JsonOptions),
                command.OccurredAt);
            db.PurchaseInvoiceMatchHistory.Add(new PurchaseInvoiceMatchHistoryEntity(
                tenantContext.TenantId,
                Guid.NewGuid(),
                entity.Id,
                entity.PurchaseInvoiceHandoffId,
                entity.Result,
                "exception-resolved",
                command.ActorId,
                command.Reason,
                command.OccurredAt,
                command.CorrelationId));
            var audit = new PurchaseInvoiceMatchAuditEntity(evidence);
            db.PurchaseInvoiceMatchAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(PurchaseInvoiceMatchPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Denied(PurchaseInvoiceMatchPersistenceOutcome.Duplicate, "match_resolution_duplicate");
        }
        catch (DbUpdateException)
        {
            return Denied(PurchaseInvoiceMatchPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(PurchaseInvoiceMatchPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseInvoiceMatchHistory.AsNoTracking()
            .Where(item => item.PurchaseInvoiceMatchEvaluationId == matchEvaluationId)
            .ToListAsync(cancellationToken);
        return records
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
            .Select(item => new PurchaseInvoiceMatchHistoryRecord(item.Id, item.PurchaseInvoiceMatchEvaluationId, item.PurchaseInvoiceHandoffId, item.Result, item.Action, item.ActorId, item.Reason, item.OccurredAt, item.CorrelationId))
            .ToArray();
    }

    public async Task<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseInvoiceMatchAudit.AsNoTracking()
            .Where(item => item.PurchaseInvoiceMatchEvaluationId == matchEvaluationId)
            .ToListAsync(cancellationToken);
        return records
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
            .Select(item => new PurchaseInvoiceMatchAuditRecord(item.Id, item.PurchaseInvoiceMatchEvaluationId, item.PurchaseInvoiceHandoffId, item.OperationId, item.TenantId.Value, item.ActorId, item.Decision, item.Reason, item.OccurredAt, item.IdempotencyKey, item.RequestFingerprint))
            .ToArray();
    }

    private static async Task<PurchaseInvoiceHandoffEntity?> LoadHandoffAsync(ProcurementDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.PurchaseInvoiceHandoffs
            .Include(item => item.Sources)
            .Include(item => item.DeclaredEvidenceVersions)
                .ThenInclude(item => item.Lines)
                    .ThenInclude(item => item.Allocations)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private static async Task<IReadOnlyList<PurchaseInvoiceHandoffEntity>> LoadActiveHandoffsAsync(
        ProcurementDbContext db,
        Guid purchaseOrderId,
        CancellationToken cancellationToken) =>
        await db.PurchaseInvoiceHandoffs
            .Where(item => item.PurchaseOrderId == purchaseOrderId && item.Status == PurchaseInvoiceHandoffStatus.Recorded)
            .Include(item => item.Sources)
            .Include(item => item.DeclaredEvidenceVersions)
                .ThenInclude(item => item.Lines)
            .ToListAsync(cancellationToken);

    private static MatchCalculation EvaluateSources(
        PurchaseInvoiceHandoffEntity handoff,
        PurchaseOrderEntity order,
        IReadOnlyList<GoodsReceiptEntity> receipts,
        IReadOnlyList<PurchaseInvoiceHandoffEntity> activeHandoffs,
        PurchaseInvoiceDeclaredEvidenceEntity? declared,
        PurchaseInvoiceMatchingToleranceDefinition policy,
        PurchaseInvoiceMatchExchangeRateRecord? exchangeRate)
    {
        var variances = new List<PurchaseInvoiceMatchVarianceRecord>();
        if (declared is null)
        {
            variances.Add(new("InvoiceEvidenceMissing", null, null, null, null, null, 0m, null, "Supplier-declared invoice evidence is required for three-way matching."));
            return new(PurchaseInvoiceMatchResult.NotMatchReady, variances);
        }

        var receiptLines = receipts
            .Where(item => item.Status == GoodsReceiptStatus.Recorded)
            .SelectMany(item => item.Lines.Select(line => (Receipt: item, Line: line)))
            .ToDictionary(item => item.Line.Id);
        var handoffSources = handoff.Sources
            .GroupBy(item => item.GoodsReceiptLineId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var handoffQuantitiesByPurchaseOrderLine = handoff.Sources
            .GroupBy(item => item.PurchaseOrderLineId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var orderLines = order.Lines.ToDictionary(item => item.Id);
        var acceptedQuantitiesByPurchaseOrderLine = receipts
            .Where(item => item.Status == GoodsReceiptStatus.Recorded)
            .SelectMany(item => item.Lines)
            .GroupBy(item => item.PurchaseOrderLineId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.AcceptedQuantity));
        var cumulativeDeclaredQuantitiesByPurchaseOrderLine = activeHandoffs
            .SelectMany(item => item.DeclaredEvidenceVersions
                .SingleOrDefault(version => version.IsCurrent)?.Lines ?? [])
            .GroupBy(item => item.PurchaseOrderLineId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var declaredQuantitiesByPurchaseOrderLine = declared.Lines
            .GroupBy(item => item.PurchaseOrderLineId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var comparableCurrency = string.Equals(declared.CurrencyCode, order.CurrencyCode, StringComparison.OrdinalIgnoreCase);
        var validExchangeRate = IsValidExchangeRate(exchangeRate, declared.CurrencyCode, order.CurrencyCode);
        var convert = comparableCurrency ? (Func<decimal, decimal>)(value => value) : value => validExchangeRate ? value * exchangeRate!.Rate / exchangeRate.Scale : value;
        if (!comparableCurrency && !validExchangeRate)
        {
            variances.Add(new("CurrencyNotComparable", null, null, null, null, null, 0m, order.CurrencyCode, "An immutable applied exchange-rate evidence snapshot is required for different currencies."));
        }

        foreach (var (purchaseOrderLineId, cumulativeDeclaredQuantity) in cumulativeDeclaredQuantitiesByPurchaseOrderLine)
        {
            var confirmedQuantity = orderLines.GetValueOrDefault(purchaseOrderLineId)?.ConfirmedQuantity ?? 0m;
            var acceptedQuantity = acceptedQuantitiesByPurchaseOrderLine.GetValueOrDefault(purchaseOrderLineId);
            var sourceLimit = acceptedQuantity > 0m ? acceptedQuantity : confirmedQuantity;
            if (cumulativeDeclaredQuantity > sourceLimit)
            {
                variances.Add(new(
                    "CumulativeQuantityLimitExceeded",
                    purchaseOrderLineId,
                    null,
                    sourceLimit,
                    cumulativeDeclaredQuantity,
                    cumulativeDeclaredQuantity - sourceLimit,
                    0m,
                    null,
                    "Cumulative active supplier-declared invoice quantity exceeds the active accepted or confirmed source quantity."));
            }
        }

        foreach (var (purchaseOrderLineId, declaredQuantity) in declaredQuantitiesByPurchaseOrderLine)
        {
            if (!orderLines.ContainsKey(purchaseOrderLineId)) continue;

            var expectedQuantity = handoffQuantitiesByPurchaseOrderLine.GetValueOrDefault(purchaseOrderLineId);
            Compare(variances, "QuantityVariance", purchaseOrderLineId, null, expectedQuantity, declaredQuantity, policy.QuantityAbsoluteTolerance, policy.QuantityPercentageTolerance, null, "Aggregated supplier-declared invoice quantity differs from the current handoff/source quantity.");
        }

        foreach (var line in declared.Lines)
        {
            if (!orderLines.TryGetValue(line.PurchaseOrderLineId, out var poLine))
            {
                variances.Add(new("InvoiceLineNotOnPurchaseOrder", line.PurchaseOrderLineId, null, null, null, null, 0m, order.CurrencyCode, "Invoice line does not reference a line on the selected Purchase Order."));
                continue;
            }

            var allocationTotal = 0m;
            foreach (var allocation in line.Allocations)
            {
                allocationTotal += allocation.Quantity;
                var handedOff = handoffSources.GetValueOrDefault(allocation.GoodsReceiptLineId);
                if (!receiptLines.TryGetValue(allocation.GoodsReceiptLineId, out var receiptLine)
                    || receiptLine.Receipt.Id != allocation.GoodsReceiptId
                    || receiptLine.Line.PurchaseOrderLineId != line.PurchaseOrderLineId
                    || receiptLine.Line.AcceptedQuantity <= 0
                    || allocation.Quantity > handedOff)
                {
                    variances.Add(new("InvoiceAllocationNotEligible", line.PurchaseOrderLineId, allocation.GoodsReceiptLineId, handedOff, allocation.Quantity, allocation.Quantity - handedOff, 0m, order.CurrencyCode, "Invoice allocation must reference an active accepted receipt line within this handoff."));
                }
            }

            if (allocationTotal > line.Quantity)
            {
                // Supplier-declared quantity is independent evidence. The
                // allocation is only the supported portion, so a smaller
                // allocation is intentionally not an intake failure.
                variances.Add(new("InvoiceAllocationExceedsDeclaredQuantity", line.PurchaseOrderLineId, null, line.Quantity, allocationTotal, allocationTotal - line.Quantity, 0m, null, "Receipt allocation cannot exceed the supplier-declared invoice quantity."));
            }

            Compare(variances, "PriceVariance", line.PurchaseOrderLineId, null, poLine.UnitPrice, convert(line.UnitPrice), policy.PriceAbsoluteTolerance, policy.PricePercentageTolerance, order.CurrencyCode, "Supplier unit price differs from the Purchase Order.");
            if (line.TaxRatePercentage is not null || poLine.TaxRatePercentage is not null)
            {
                var expected = poLine.TaxRatePercentage ?? 0m;
                var actual = line.TaxRatePercentage ?? 0m;
                Compare(variances, "TaxRateVariance", line.PurchaseOrderLineId, null, expected, actual, policy.TaxAbsoluteTolerance, policy.TaxPercentageTolerance, null, "Supplier tax rate evidence is missing or differs from the Purchase Order snapshot.");
            }

            if (line.TaxCode is not null || poLine.TaxCode is not null)
            {
                if (!string.Equals(line.TaxCode, poLine.TaxCode, StringComparison.OrdinalIgnoreCase))
                {
                    variances.Add(new("TaxCodeVariance", line.PurchaseOrderLineId, null, null, null, null, 0m, null, "Supplier tax code evidence is missing or differs from the Purchase Order snapshot."));
                }
            }

            var expectedDiscount = poLine.DiscountAmount is { } discount && poLine.OrderedQuantity > 0m
                ? discount * line.Quantity / poLine.OrderedQuantity
                : 0m;
            if (line.DiscountAmount is { } actualDiscount)
            {
                Compare(variances, "DiscountVariance", line.PurchaseOrderLineId, null, expectedDiscount, convert(actualDiscount), policy.AmountAbsoluteTolerance, policy.AmountPercentageTolerance, order.CurrencyCode, "Supplier discount differs from the Purchase Order snapshot.");
            }

            var expectedNet = Math.Max(0m, line.Quantity * poLine.UnitPrice - expectedDiscount);
            var expectedTax = poLine.TaxRatePercentage is { } rate ? Math.Round(expectedNet * rate / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
            var expectedGross = expectedNet + expectedTax;
            if (line.NetAmount is { } net)
            {
                Compare(variances, "AmountVariance", line.PurchaseOrderLineId, null, expectedNet, convert(net), policy.AmountAbsoluteTolerance, policy.AmountPercentageTolerance, order.CurrencyCode, "Supplier net amount differs from the Purchase Order/receipt calculation.");
            }

            if (line.TaxAmount is { } tax)
            {
                Compare(variances, "TaxAmountVariance", line.PurchaseOrderLineId, null, expectedTax, convert(tax), policy.TaxAbsoluteTolerance, policy.TaxPercentageTolerance, order.CurrencyCode, "Supplier tax amount differs from the Purchase Order calculation.");
            }

            if (line.GrossAmount is { } gross)
            {
                Compare(variances, "AmountVariance", line.PurchaseOrderLineId, null, expectedGross, convert(gross), policy.AmountAbsoluteTolerance, policy.AmountPercentageTolerance, order.CurrencyCode, "Supplier gross amount differs from the Purchase Order/receipt calculation.");
            }
        }

        var declaredAllocationsByGoodsReceiptLine = declared.Lines
            .SelectMany(line => line.Allocations.Select(allocation => new
            {
                line.PurchaseOrderLineId,
                Allocation = allocation
            }))
            .GroupBy(item => item.Allocation.GoodsReceiptLineId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Allocation.Quantity));

        foreach (var (goodsReceiptLineId, totalDeclaredAllocation) in declaredAllocationsByGoodsReceiptLine)
        {
            if (!receiptLines.TryGetValue(goodsReceiptLineId, out var receiptLine)) continue;

            var handoffQuantity = handoffSources.GetValueOrDefault(goodsReceiptLineId);
            var supportedQuantity = Math.Min(handoffQuantity, receiptLine.Line.AcceptedQuantity);
            if (totalDeclaredAllocation > supportedQuantity)
            {
                variances.Add(new(
                    "InvoiceAllocationExceedsSupportedQuantity",
                    receiptLine.Line.PurchaseOrderLineId,
                    goodsReceiptLineId,
                    supportedQuantity,
                    totalDeclaredAllocation,
                    totalDeclaredAllocation - supportedQuantity,
                    0m,
                    order.CurrencyCode,
                    "Aggregated invoice allocations exceed the active accepted quantity represented by this handoff receipt line."));
            }
        }

        var expectedSubtotal = declared.Lines.Sum(line =>
        {
            if (!orderLines.TryGetValue(line.PurchaseOrderLineId, out var poLine)) return 0m;
            var discount = poLine.DiscountAmount is { } value && poLine.OrderedQuantity > 0m ? value * line.Quantity / poLine.OrderedQuantity : 0m;
            return Math.Max(0m, line.Quantity * poLine.UnitPrice - discount);
        });
        var expectedTaxTotal = declared.Lines.Sum(line =>
        {
            if (!orderLines.TryGetValue(line.PurchaseOrderLineId, out var poLine)) return 0m;
            var discount = poLine.DiscountAmount is { } value && poLine.OrderedQuantity > 0m ? value * line.Quantity / poLine.OrderedQuantity : 0m;
            var net = Math.Max(0m, line.Quantity * poLine.UnitPrice - discount);
            return poLine.TaxRatePercentage is { } rate ? Math.Round(net * rate / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
        });
        if (declared.SubtotalAmount is { } subtotal)
        {
            Compare(variances, "HeaderSubtotalVariance", null, null, expectedSubtotal, convert(subtotal), policy.AmountAbsoluteTolerance, policy.AmountPercentageTolerance, order.CurrencyCode, "Supplier subtotal differs from line-derived subtotal.");
        }

        if (declared.TaxAmount is { } totalTax)
        {
            Compare(variances, "HeaderTaxVariance", null, null, expectedTaxTotal, convert(totalTax), policy.TaxAbsoluteTolerance, policy.TaxPercentageTolerance, order.CurrencyCode, "Supplier tax total differs from line-derived tax.");
        }

        var expectedDiscountTotal = declared.Lines.Sum(line =>
        {
            if (!orderLines.TryGetValue(line.PurchaseOrderLineId, out var poLine)) return 0m;
            return poLine.DiscountAmount is { } discount && poLine.OrderedQuantity > 0m
                ? discount * line.Quantity / poLine.OrderedQuantity
                : 0m;
        });
        if (declared.DiscountAmount is { } discountTotal)
        {
            Compare(variances, "HeaderDiscountVariance", null, null, expectedDiscountTotal, convert(discountTotal), policy.AmountAbsoluteTolerance, policy.AmountPercentageTolerance, order.CurrencyCode, "Supplier discount total differs from line-derived discount.");
        }

        if (declared.GrossAmount is { } grossTotal)
        {
            Compare(variances, "HeaderGrossVariance", null, null, expectedSubtotal + expectedTaxTotal, convert(grossTotal), policy.AmountAbsoluteTolerance, policy.AmountPercentageTolerance, order.CurrencyCode, "Supplier gross total differs from line-derived total.");
        }

        var blocking = variances.Any(item => item.Classification is "InvoiceEvidenceMissing" or "CurrencyNotComparable" or "InvoiceLineNotOnPurchaseOrder" or "InvoiceAllocationNotEligible" or "InvoiceAllocationExceedsDeclaredQuantity" or "InvoiceAllocationExceedsSupportedQuantity" or "CumulativeQuantityLimitExceeded" || item.Variance is { } variance && Math.Abs(variance) > item.AllowedTolerance);
        var result = blocking
            ? (variances.Any(item => item.Classification is "InvoiceEvidenceMissing" or "CurrencyNotComparable") ? PurchaseInvoiceMatchResult.NotMatchReady : PurchaseInvoiceMatchResult.ExceptionHold)
            : variances.Count == 0 ? PurchaseInvoiceMatchResult.ExactMatch : PurchaseInvoiceMatchResult.WithinTolerance;
        return new(result, variances);
    }

    private static void Compare(
        ICollection<PurchaseInvoiceMatchVarianceRecord> variances,
        string classification,
        Guid? purchaseOrderLineId,
        Guid? goodsReceiptLineId,
        decimal expected,
        decimal actual,
        decimal absoluteTolerance,
        decimal percentageTolerance,
        string? currencyCode,
        string details)
    {
        var allowed = absoluteTolerance + Math.Abs(expected) * percentageTolerance / 100m;
        var variance = actual - expected;
        if (variance != 0m)
        {
            variances.Add(new(classification, purchaseOrderLineId, goodsReceiptLineId, expected, actual, variance, allowed, currencyCode, details));
        }
    }

    private static object BuildSourceSnapshot(
        PurchaseInvoiceHandoffEntity handoff,
        PurchaseOrderEntity order,
        IReadOnlyList<GoodsReceiptEntity> receipts,
        IReadOnlyList<PurchaseInvoiceHandoffEntity> activeHandoffs,
        PurchaseInvoiceDeclaredEvidenceEntity? evidence,
        PurchaseInvoiceMatchingToleranceDefinition policy,
        PurchaseInvoiceMatchExchangeRateRecord? exchangeRate) => new
        {
            handoff = new { handoff.Id, Version = Convert.ToBase64String(handoff.Version), handoff.Status, handoff.PurchaseOrderId, sources = handoff.Sources.OrderBy(item => item.Id).Select(source => new { source.Id, source.GoodsReceiptId, source.GoodsReceiptLineId, source.PurchaseOrderLineId, source.Quantity }) },
            purchaseOrder = new { order.Id, Version = Convert.ToBase64String(order.Version), order.CurrencyCode, lines = order.Lines.OrderBy(item => item.Id).Select(item => new { item.Id, item.OrderedQuantity, item.UnitPrice, item.DiscountAmount, item.TaxCode, item.TaxRatePercentage, item.TaxAmount }) },
            goodsReceipts = receipts.Where(item => item.Status == GoodsReceiptStatus.Recorded).OrderBy(item => item.Id).Select(item => new { item.Id, Version = Convert.ToBase64String(item.Version), item.Status, lines = item.Lines.OrderBy(line => line.Id).Select(line => new { line.Id, line.PurchaseOrderLineId, line.AcceptedQuantity, line.RejectedQuantity }) }),
            activeDeclaredEvidence = activeHandoffs.OrderBy(item => item.Id).Select(item => new
            {
                item.Id,
                Version = Convert.ToBase64String(item.Version),
                evidence = item.DeclaredEvidenceVersions
                    .SingleOrDefault(version => version.IsCurrent) is { } current
                    ? new
                    {
                        current.Id,
                        current.VersionNumber,
                        lines = current.Lines.OrderBy(line => line.Id).Select(line => new { line.Id, line.PurchaseOrderLineId, line.Quantity })
                    }
                    : null
            }),
            declaredEvidence = evidence is null ? null : new { evidence.Id, evidence.VersionNumber, evidence.IsCurrent, evidence.CurrencyCode, evidence.SupplierInvoiceReference, evidence.SupplierInvoiceDate, evidence.SubtotalAmount, evidence.DiscountAmount, evidence.TaxAmount, evidence.GrossAmount, lines = evidence.Lines.OrderBy(item => item.Id).Select(line => new { line.Id, line.PurchaseOrderLineId, line.Quantity, line.UnitPrice, line.DiscountAmount, line.TaxRatePercentage, line.TaxCode, line.TaxAmount, line.NetAmount, line.GrossAmount, allocations = line.Allocations.OrderBy(item => item.Id).Select(allocation => new { allocation.GoodsReceiptId, allocation.GoodsReceiptLineId, allocation.Quantity }) }) },
            policy,
            exchangeRate
        };

    private static bool IsValidExchangeRate(PurchaseInvoiceMatchExchangeRateRecord? rate, string source, string target) =>
        rate is not null && rate.ExchangeRateId != Guid.Empty && rate.ExchangeRateVersionId != Guid.Empty && rate.VersionNumber > 0
        && rate.Rate > 0m && rate.Scale > 0 && rate.Scale <= 1_000_000
        && string.Equals(rate.SourceCurrencyCode, source, StringComparison.OrdinalIgnoreCase)
        && string.Equals(rate.TargetCurrencyCode, target, StringComparison.OrdinalIgnoreCase)
        && rate.EffectiveFrom <= rate.EffectiveOn
        && (rate.EffectiveTo is null || rate.EffectiveOn <= rate.EffectiveTo.Value);

    private async Task<PurchaseInvoiceMatchRecord?> FindByFingerprintAsync(TenantContext tenantContext, Guid handoffId, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PurchaseInvoiceMatchEvaluations.AsNoTracking()
            .Where(item => item.PurchaseInvoiceHandoffId == handoffId && item.SourceFingerprint == fingerprint)
            .OrderByDescending(item => item.EvaluatedAt).ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    private async Task<PurchaseInvoiceMatchRecord?> FindReplayAsync(ProcurementDbContext db, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evidence.IdempotencyKey)) return null;
        var candidate = await db.PurchaseInvoiceMatchAudit
            .Where(item => item.ActorId == evidence.ActorId && item.OperationId == evidence.OperationId && item.IdempotencyKey == evidence.IdempotencyKey)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return null;
        if (!string.Equals(candidate.RequestFingerprint, evidence.RequestFingerprint, StringComparison.Ordinal)) throw new ReplayConflictException();
        if (string.IsNullOrWhiteSpace(candidate.ReplayResponseSnapshotJson)) return null;
        return JsonSerializer.Deserialize<PurchaseInvoiceMatchRecord>(candidate.ReplayResponseSnapshotJson, JsonOptions);
    }

    private static PurchaseInvoiceMatchListRecord ToListRecord(PurchaseInvoiceMatchEvaluationEntity entity) => new(entity.Id, new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId), entity.PurchaseInvoiceHandoffId, entity.PurchaseOrderId, entity.Lifecycle, entity.Result, entity.EvaluatedAt, entity.ResolvedByActorId, CountVariances(entity.VariancesJson), entity.Version.ToArray());

    private static PurchaseInvoiceMatchReportingRecord ToReportingRecord(PurchaseInvoiceMatchEvaluationEntity entity) => new(
        entity.Id,
        entity.TenantId.Value,
        new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
        entity.PurchaseInvoiceHandoffId,
        entity.PurchaseOrderId,
        entity.Lifecycle,
        entity.Result,
        entity.EvaluatedAt,
        entity.ResolvedByActorId,
        entity.ResolvedAt,
        entity.ResolutionReason,
        entity.DeclaredEvidenceId,
        entity.DeclaredEvidenceVersion,
        entity.SourceFingerprint,
        JsonSerializer.Deserialize<IReadOnlyList<PurchaseInvoiceMatchVarianceRecord>>(entity.VariancesJson, JsonOptions) ?? [],
        entity.Version.ToArray());

    private static IQueryable<PurchaseInvoiceMatchEvaluationEntity> ApplyTrustedScope(IQueryable<PurchaseInvoiceMatchEvaluationEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference) return query;
        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return query.Where(_ => false);
        return parts[0] switch
        {
            "Tenant" => query,
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            _ => query.Where(_ => false)
        };
    }

    private static PurchaseInvoiceMatchRecord ToRecord(PurchaseInvoiceMatchEvaluationEntity entity)
    {
        var policy = JsonSerializer.Deserialize<PurchaseInvoiceMatchingToleranceDefinition>(entity.PolicySnapshotJson, JsonOptions) ?? PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(entity.EvaluatedAt);
        var resolutionPolicy = string.IsNullOrWhiteSpace(entity.ResolutionPolicySnapshotJson)
            ? null
            : JsonSerializer.Deserialize<PurchaseInvoiceMatchingResolutionPolicyDefinition>(entity.ResolutionPolicySnapshotJson, JsonOptions);
        var exchangeRate = string.IsNullOrWhiteSpace(entity.ExchangeRateSnapshotJson) ? null : JsonSerializer.Deserialize<PurchaseInvoiceMatchExchangeRateRecord>(entity.ExchangeRateSnapshotJson, JsonOptions);
        var variances = JsonSerializer.Deserialize<IReadOnlyList<PurchaseInvoiceMatchVarianceRecord>>(entity.VariancesJson, JsonOptions) ?? [];
        return new(
            entity.Id,
            entity.TenantId.Value,
            new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
            entity.PurchaseInvoiceHandoffId,
            entity.PurchaseOrderId,
            entity.Lifecycle,
            entity.Result,
            entity.EvaluatedAt,
            entity.EvaluatedByActorId,
            entity.ResolvedByActorId,
            entity.ResolvedAt,
            entity.ResolutionReason,
            entity.SourceFingerprint,
            entity.PurchaseOrderVersion.ToArray(),
            entity.HandoffVersion.ToArray(),
            entity.DeclaredEvidenceId,
            entity.DeclaredEvidenceVersion,
            policy,
            resolutionPolicy,
            exchangeRate,
            variances,
            entity.SourceSnapshotJson,
            entity.Version.ToArray());
    }

    private static int CountVariances(string json)
    {
        try { return JsonSerializer.Deserialize<IReadOnlyList<PurchaseInvoiceMatchVarianceRecord>>(json, JsonOptions)?.Count ?? 0; }
        catch { return 0; }
    }

    private static string SerializeReplayResponse(PurchaseInvoiceMatchRecord record) => JsonSerializer.Serialize(record, JsonOptions);
    private static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord> Denied(PurchaseInvoiceMatchPersistenceOutcome outcome, string code) => PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>.Denied(outcome, code);
    private ProcurementDbContext CreateContext(TenantContext tenantContext) => new(options, tenantContext);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 } || current is SqliteException { SqliteErrorCode: 19 }) return true;
        }
        return false;
    }

    private sealed record MatchCalculation(PurchaseInvoiceMatchResult Result, IReadOnlyList<PurchaseInvoiceMatchVarianceRecord> Variances);
    private sealed class ReplayConflictException : Exception;
}

#pragma warning restore CS1591
