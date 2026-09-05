#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

public sealed class CustomerReturnPersistence(DbContextOptions options) : ISalesCustomerReturnPersistence, ISalesCustomerReturnSourceProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private SalesDbContext Create(ProcurementRequestContext context) => new(options, context.TenantContext);
    private SalesDbContext Create(TenantContext context) => new(options, context);

    public async Task<IReadOnlyList<SalesCustomerReturnSourceRecord>> ListEligibleSourcesAsync(ProcurementRequestContext context, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var deliveries = await db.Deliveries.AsNoTracking().Where(item => item.Status == SalesDeliveryStatus.Posted).OrderByDescending(item => item.PostedAt).Take(500).ToListAsync(cancellationToken);
        var result = new List<SalesCustomerReturnSourceRecord>(deliveries.Count);
        foreach (var delivery in deliveries)
        {
            var source = await BuildSourceAsync(db, delivery, null, cancellationToken);
            if (source is not null && source.Lines.Any(item => item.EligibleQuantity > 0m)) result.Add(source);
        }
        return result;
    }

    public async Task<SalesCustomerReturnSourceRecord?> GetEligibleSourceAsync(ProcurementRequestContext context, Guid deliveryId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var delivery = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deliveryId && item.Status == SalesDeliveryStatus.Posted, cancellationToken);
        return delivery is null ? null : await BuildSourceAsync(db, delivery, null, cancellationToken);
    }

    public async Task<SalesCustomerReturnResponse?> GetAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var item = await db.CustomerReturns.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return item is null || !InScope(context, item.CompanyId, item.BranchId) ? null : ToResponse(item);
    }

    public async Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.History.AsNoTracking().Where(item => item.DocumentType == "customer-return" && item.DocumentId == id).OrderByDescending(item => item.OccurredAt).ToListAsync(cancellationToken)).Select(item => new SalesHistoryResponse(item.Id, item.DocumentType, item.DocumentId, item.Action.ToString(), item.FromStatus, item.ToStatus, item.ActorId, item.OccurredAt, item.Reason, item.PolicyId, item.PolicyVersion, item.CreditOutcome, item.SnapshotHash, item.SnapshotJson)).ToArray();
    }

    public async Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.Audit.AsNoTracking().Where(item => item.DocumentType == "customer-return" && item.DocumentId == id).OrderByDescending(item => item.OccurredAt).ToListAsync(cancellationToken)).Select(item => new SalesAuditResponse(item.Id, item.OperationId, item.DocumentType, item.DocumentId, item.ActorId, item.OccurredAt, item.Decision, item.Reason, item.BeforeSummary, item.AfterSummary, item.IdempotencyKey, item.CorrelationId)).ToArray();
    }

    public async Task<SalesCustomerReturnSourceRecord?> GetCustomerReturnSourceAsync(TenantContext context, Guid returnId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var item = await db.CustomerReturns.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == returnId, cancellationToken);
        if (item is null) return null;
        var delivery = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(value => value.Id == item.DeliveryId && value.Status == SalesDeliveryStatus.Posted, cancellationToken);
        return delivery is null ? null : await BuildSourceAsync(db, delivery, item, cancellationToken);
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateAsync(ProcurementRequestContext context, SalesCustomerReturnCreateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var operation = "sales.customer-return.create";
        var replay = await ReadReplayAsync(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var delivery = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.Request.DeliveryId && item.Status == SalesDeliveryStatus.Posted, cancellationToken);
        var source = delivery is null ? null : await BuildSourceAsync(db, delivery, null, cancellationToken);
        if (source is null) return Failure("return_source_not_found");
        var sourceInvoiceIds = source.InvoiceAllocations?.Select(item => item.InvoiceId).Distinct().ToArray() ?? [];
        if (command.Request.InvoiceId is { } invoiceId && !sourceInvoiceIds.Contains(invoiceId)) return Failure("invoice_source_mismatch");
        if (command.Request.Consequence == SalesCustomerReturnConsequence.CreditNote && sourceInvoiceIds.Length == 0) return Failure("recognized_invoice_required");
        var lines = command.Request.Lines.ToDictionary(item => item.OrderLineId);
        foreach (var requestLine in command.Request.Lines)
        {
            var sourceLine = source.Lines.SingleOrDefault(item => item.OrderLineId == requestLine.OrderLineId);
            if (sourceLine is null || requestLine.Quantity > sourceLine.EligibleQuantity) return Failure("return_quantity_conflict");
        }
        var entity = new SalesCustomerReturnEntity(context.TenantId, command.Id, command.Request, source, command.ActorId, command.OccurredAt);
        foreach (var requestLine in command.Request.Lines)
        {
            var sourceLine = source.Lines.Single(item => item.OrderLineId == requestLine.OrderLineId);
            entity.Lines.Add(new SalesCustomerReturnLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, source.DeliveryId, requestLine, sourceLine));
        }
        foreach (var sourceLine in entity.Lines)
        {
            var remaining = sourceLine.ReturnQuantity;
            foreach (var candidate in (source.InvoiceAllocations ?? []).Where(item => item.OrderLineId == sourceLine.OrderLineId && (command.Request.InvoiceId is null || item.InvoiceId == command.Request.InvoiceId)).OrderBy(item => item.InvoiceId).ThenBy(item => item.Id))
            {
                if (remaining <= 0m) break;
                var quantity = Math.Min(remaining, candidate.RemainingCreditableQuantity);
                if (quantity <= 0m) continue;
                var ratio = candidate.RecognizedQuantity == 0m ? 0m : quantity / candidate.RecognizedQuantity;
                var allocation = candidate with
                {
                    Id = candidate.Id,
                    ReturnQuantity = quantity,
                    CommerciallyAcceptedQuantity = 0m,
                    PreviouslyCreditedQuantity = 0m,
                    RemainingCreditableQuantity = quantity,
                    NetAmount = Round(candidate.NetAmount * ratio),
                    TaxAmount = Round(candidate.TaxAmount * ratio),
                    GrossAmount = Round(candidate.GrossAmount * ratio)
                };
                db.CustomerReturnInvoiceAllocations.Add(new SalesCustomerReturnInvoiceAllocationEntity(context.TenantId, Guid.NewGuid(), entity.Id, allocation));
                remaining -= quantity;
            }
        }
        db.CustomerReturns.Add(entity);
        AddHistory(db, context, entity.Id, SalesHistoryAction.Created, null, entity.Status, command.Request.Reason, command.RequestFingerprint);
        AddAudit(db, context, operation, entity.Id, "Allowed", command.Request.Reason, null, $"status={entity.Status};delivery={entity.DeliveryId}", command.IdempotencyKey, command.RequestFingerprint, command.OccurredAt);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        await SaveReplayAsync(db, context, operation, command, response, "customer-return", entity.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(response);
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> MutateAsync(ProcurementRequestContext context, SalesCustomerReturnActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var operation = $"sales.customer-return.{command.Action.ToString().ToLowerInvariant()}";
        var replay = await ReadReplayAsync(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (entity is null || !InScope(context, entity.CompanyId, entity.BranchId)) return Failure("customer_return_not_found");
        if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure("concurrency_conflict");
        var target = command.Action switch
        {
            SalesCustomerReturnMutation.Submit when entity.Status == SalesCustomerReturnStatus.Draft => SalesCustomerReturnStatus.Submitted,
            SalesCustomerReturnMutation.Approve when entity.Status == SalesCustomerReturnStatus.Submitted => SalesCustomerReturnStatus.AwaitingReceipt,
            SalesCustomerReturnMutation.Reject when entity.Status is SalesCustomerReturnStatus.Draft or SalesCustomerReturnStatus.Submitted => SalesCustomerReturnStatus.Rejected,
            SalesCustomerReturnMutation.Cancel when entity.Status is SalesCustomerReturnStatus.Draft or SalesCustomerReturnStatus.Submitted => SalesCustomerReturnStatus.Cancelled,
            SalesCustomerReturnMutation.Reverse when entity.Status is SalesCustomerReturnStatus.Approved or SalesCustomerReturnStatus.AwaitingReceipt && entity.InventoryEffectId is null => SalesCustomerReturnStatus.Reversed,
            SalesCustomerReturnMutation.Reverse when entity.Status is SalesCustomerReturnStatus.Received or SalesCustomerReturnStatus.Completed && entity.InventoryCommitState == "Reversed" && entity.ActiveFinanceCreditNoteCount == 0 => SalesCustomerReturnStatus.Reversed,
            _ => (SalesCustomerReturnStatus?)null
        };
        if (target is null) return Failure(entity.InventoryEffectId is not null ? "downstream_reversal_required" : "customer_return_transition_invalid");
        var before = entity.Status;
        entity.SetStatus(target.Value, command.OccurredAt);
        AddHistory(db, context, entity.Id, command.Action == SalesCustomerReturnMutation.Reverse ? SalesHistoryAction.Reversed : ActionToHistory(command.Action), before, target.Value, command.Reason, command.RequestFingerprint);
        AddAudit(db, context, operation, entity.Id, "Allowed", command.Reason, $"status={before}", $"status={target}", command.IdempotencyKey, command.RequestFingerprint, command.OccurredAt);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        await SaveReplayAsync(db, context, operation, command, response, "customer-return", entity.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(response);
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> AcknowledgeInventoryAsync(TenantContext context, SalesCustomerReturnInventoryAcknowledgementCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.ReturnId && item.TenantId.Value == command.TenantId, cancellationToken);
        if (entity is null) return Failure("customer_return_not_found");
        if (entity.Status == SalesCustomerReturnStatus.Reversed) return Failure("customer_return_transition_invalid");
        if (command.InventoryEffectId == Guid.Empty || string.IsNullOrWhiteSpace(command.EffectFingerprint) || string.IsNullOrWhiteSpace(command.RequestFingerprint) || string.IsNullOrWhiteSpace(command.PhysicalEvidenceReference) || command.Lines is null || command.Lines.Count != entity.Lines.Count || command.Lines.Select(item => item.OrderLineId).Distinct().Count() != command.Lines.Count || command.Lines.Any(item => item.OrderLineId == Guid.Empty || entity.Lines.All(line => line.OrderLineId != item.OrderLineId))) return Failure("inventory_effect_mismatch");
        try { entity.AcknowledgeInventory(command, command.OccurredAt); }
        catch (InvalidOperationException exception) { return Failure(exception.Message); }
        var allocations = await db.CustomerReturnInvoiceAllocations.Where(item => item.CustomerReturnId == entity.Id).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        foreach (var line in entity.Lines)
        {
            var accepted = line.CommerciallyAcceptedQuantity;
            foreach (var allocation in allocations.Where(item => item.OrderLineId == line.OrderLineId))
            {
                var quantity = Math.Min(allocation.ReturnQuantity, accepted);
                allocation.SetCommerciallyAccepted(quantity);
                accepted -= quantity;
            }
        }
        AddHistory(db, context, entity.Id, SalesHistoryAction.Edited, null, entity.Status, "inventory-acknowledged", command.RequestFingerprint);
        AddAudit(db, context, "sales.customer-return.inventory-acknowledge", entity.Id, "Allowed", null, null, $"status={entity.Status};effect={command.InventoryEffectId:D}", command.DownstreamIdempotencyKey, command.RequestFingerprint, command.OccurredAt);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(ToResponse(entity, allocations));
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordInventoryFailureAsync(TenantContext context, SalesCustomerReturnInventoryFailureCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.ReturnId && item.TenantId.Value == command.TenantId, cancellationToken);
        if (entity is null) return Failure("customer_return_not_found");
        try { entity.RecordInventoryFailure(command); }
        catch (InvalidOperationException exception) { return Failure(exception.Message); }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(ToResponse(entity));
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RecordDownstreamReversalAsync(TenantContext context, SalesCustomerReturnDownstreamReversalCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.ReturnId, cancellationToken);
        if (entity is null || entity.TenantId.Value != command.TenantId) return Failure("customer_return_not_found");
        try
        {
            if (string.Equals(command.Downstream, "finance", StringComparison.OrdinalIgnoreCase) && command.CreditNoteId is { } creditNoteId)
            {
                var effect = await db.CustomerReturnFinanceEffects.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.CustomerReturnId == entity.Id && item.CreditNoteId == creditNoteId, cancellationToken);
                if (effect is null) return Failure("finance_effect_mismatch");
                effect.Reverse(command, command.OccurredAt);
            }
            entity.RecordDownstreamReversal(command);
        }
        catch (InvalidOperationException exception) { return Failure(exception.Message); }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(ToResponse(entity));
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> RegisterFinanceCreditNoteAsync(TenantContext context, SalesCustomerReturnFinanceEffectCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.ReturnId, cancellationToken);
        if (entity is null || entity.TenantId.Value != command.TenantId) return Failure("customer_return_not_found");
        if (command.SourceAllocationIds is null || command.SourceAllocationIds.Count == 0 || command.SourceAllocationIds.Distinct().Count() != command.SourceAllocationIds.Count || command.SourceAllocationIds.Any(item => item == Guid.Empty)) return Failure("finance_effect_mismatch");
        var existing = await db.CustomerReturnFinanceEffects.Include(item => item.Allocations).SingleOrDefaultAsync(item => item.CustomerReturnId == entity.Id && item.CreditNoteId == command.CreditNoteId, cancellationToken);
        var selected = await db.CustomerReturnInvoiceAllocations.Where(item => item.CustomerReturnId == entity.Id && command.SourceAllocationIds.Contains(item.Id)).ToListAsync(cancellationToken);
        if (selected.Count != command.SourceAllocationIds.Count || selected.Any(item => item.InvoiceId != command.InvoiceId || item.FinanceOpenItemId != command.FinanceOpenItemId || item.CurrencyCode != command.CurrencyCode || item.CommerciallyAcceptedQuantity <= 0m)) return Failure("finance_effect_mismatch");
        var activeEffects = await db.CustomerReturnFinanceEffects.AsNoTracking().Where(item => item.CustomerReturnId == entity.Id && item.State == "Active").Select(item => item.Id).ToListAsync(cancellationToken);
        if (existing is not null) activeEffects.Remove(existing.Id);
        var priorConsumption = await db.CustomerReturnFinanceEffectAllocations.AsNoTracking().Where(item => command.SourceAllocationIds.Contains(item.SourceAllocationId)).ToListAsync(cancellationToken);
        var priorByAllocation = priorConsumption.Where(item => activeEffects.Contains(item.FinanceEffectId)).GroupBy(item => item.SourceAllocationId).ToDictionary(group => group.Key, group => new { Quantity = group.Sum(item => item.Quantity), Net = group.Sum(item => item.NetAmount), Tax = group.Sum(item => item.TaxAmount), Gross = group.Sum(item => item.GrossAmount) });
        var evidence = command.Allocations?.ToArray() ?? selected.Select(item => new SalesCustomerReturnFinanceAllocationEffect(item.Id, item.CommerciallyAcceptedQuantity, item.NetAmount, item.TaxAmount, item.GrossAmount, item.SourceAllocationFingerprint)).ToArray();
        if (evidence.Length != selected.Count || evidence.Select(item => item.SourceAllocationId).Distinct().Count() != evidence.Length || evidence.Any(item => item.SourceAllocationId == Guid.Empty || item.Quantity <= 0m || item.NetAmount < 0m || item.TaxAmount < 0m || item.GrossAmount <= 0m || Round(item.NetAmount + item.TaxAmount) != item.GrossAmount)) return Failure("finance_effect_mismatch");
        foreach (var allocation in selected)
        {
            var item = evidence.SingleOrDefault(value => value.SourceAllocationId == allocation.Id);
            if (item is null || !string.Equals(item.SourceAllocationFingerprint, allocation.SourceAllocationFingerprint, StringComparison.Ordinal)) return Failure("finance_effect_mismatch");
            var prior = priorByAllocation.GetValueOrDefault(allocation.Id);
            var remainingQuantity = allocation.CommerciallyAcceptedQuantity - (prior?.Quantity ?? 0m);
            if (item.Quantity > remainingQuantity) return Failure("finance_effect_mismatch");
            var eligibleNet = Round(allocation.ReturnQuantity == 0m ? 0m : allocation.NetAmount * allocation.CommerciallyAcceptedQuantity / allocation.ReturnQuantity);
            var eligibleTax = Round(allocation.ReturnQuantity == 0m ? 0m : allocation.TaxAmount * allocation.CommerciallyAcceptedQuantity / allocation.ReturnQuantity);
            var eligibleGross = Round(allocation.ReturnQuantity == 0m ? 0m : allocation.GrossAmount * allocation.CommerciallyAcceptedQuantity / allocation.ReturnQuantity);
            var expectedNet = item.Quantity == remainingQuantity ? Round(eligibleNet - (prior?.Net ?? 0m)) : Round(eligibleNet * item.Quantity / allocation.CommerciallyAcceptedQuantity);
            var expectedTax = item.Quantity == remainingQuantity ? Round(eligibleTax - (prior?.Tax ?? 0m)) : Round(eligibleTax * item.Quantity / allocation.CommerciallyAcceptedQuantity);
            var expectedGross = item.Quantity == remainingQuantity ? Round(eligibleGross - (prior?.Gross ?? 0m)) : Round(expectedNet + expectedTax);
            if (Round(item.NetAmount - expectedNet) != 0m || Round(item.TaxAmount - expectedTax) != 0m || Round(item.GrossAmount - expectedGross) != 0m) return Failure("finance_effect_mismatch");
        }
        if (Round(evidence.Sum(item => item.NetAmount)) != command.NetAmount || Round(evidence.Sum(item => item.TaxAmount)) != command.TaxAmount || Round(evidence.Sum(item => item.GrossAmount)) != command.GrossAmount) return Failure("finance_effect_mismatch");
        if (existing is not null)
        {
            if (!existing.MatchesPost(command, evidence) || existing.State != "Active") return Failure("finance_effect_mismatch");
            return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(ToResponse(entity, selected));
        }
        try
        {
            entity.RegisterFinanceCreditNote(command, command.OccurredAt);
            if (command.CompanyId is not null && command.CustomerId is not null && command.FinanceOpenItemId is not null && command.PostingJournalId is not null && command.TaxJournalIds is not null && command.NetAmount is not null && command.TaxAmount is not null && command.GrossAmount is not null && !string.IsNullOrWhiteSpace(command.CurrencyCode) && !string.IsNullOrWhiteSpace(command.SourceFingerprint) && !string.IsNullOrWhiteSpace(command.EffectFingerprint) && !string.IsNullOrWhiteSpace(command.RequestFingerprint) && !string.IsNullOrWhiteSpace(command.DownstreamIdempotencyKey))
            {
                db.CustomerReturnFinanceEffects.Add(new SalesCustomerReturnFinanceEffectEntity(context.TenantId, Guid.NewGuid(), command, evidence, command.OccurredAt));
            }
        }
        catch (InvalidOperationException exception) { return Failure(exception.Message); }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(ToResponse(entity));
    }

    private async Task<SalesCustomerReturnSourceRecord?> BuildSourceAsync(SalesDbContext db, SalesDeliveryEntity delivery, SalesCustomerReturnEntity? current, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == delivery.OrderId && item.RevisionNumber == delivery.OrderRevisionNumber, cancellationToken);
        if (order is null) return null;
        var orderLines = Lines(order.LinesJson).ToDictionary(item => item.Id);
        var deliveryLines = JsonSerializer.Deserialize<IReadOnlyList<SalesDeliveryRequestLine>>(delivery.LinesJson, Json) ?? [];
        var activeReturnIds = (await db.CustomerReturns.AsNoTracking().Where(item => item.DeliveryId == delivery.Id && item.Status != SalesCustomerReturnStatus.Rejected && item.Status != SalesCustomerReturnStatus.Cancelled && item.Status != SalesCustomerReturnStatus.Reversed).Select(item => item.Id).ToListAsync(cancellationToken)).Where(item => current?.Id != item).ToHashSet();
        var consumed = await db.CustomerReturnLines.AsNoTracking().Where(item => item.DeliveryId == delivery.Id && activeReturnIds.Contains(item.CustomerReturnId)).GroupBy(item => item.OrderLineId).Select(group => new { group.Key, Quantity = group.Sum(item => item.ReturnQuantity) }).ToDictionaryAsync(item => item.Key, item => item.Quantity, cancellationToken);
        var currentLines = current?.Lines.ToDictionary(item => item.OrderLineId) ?? [];
        var postedInvoices = await db.InvoiceRequests.AsNoTracking()
            .Where(item => item.OrderId == delivery.OrderId
                && item.OrderRevisionNumber == delivery.OrderRevisionNumber
                && item.CompanyId == delivery.CompanyId
                && item.CustomerId == delivery.CustomerId
                && item.Status == SalesInvoiceRequestStatus.Posted)
            .ToListAsync(cancellationToken);
        var persistedAllocations = current is null ? [] : await db.CustomerReturnInvoiceAllocations.AsNoTracking().Where(item => item.CustomerReturnId == current.Id).ToListAsync(cancellationToken);
        var invoiceAllocations = current is null
            ? BuildInvoiceAllocations(postedInvoices, delivery.Id, delivery.OrderRevisionNumber, order.CurrencyCode, cancellationToken)
            : persistedAllocations.Select(ToAllocationRecord).ToArray();
        if (current is null && invoiceAllocations.Count > 0)
        {
            var consumedAllocations = await db.CustomerReturnInvoiceAllocations.AsNoTracking().Where(item => activeReturnIds.Contains(item.CustomerReturnId)).GroupBy(item => item.SourceAllocationFingerprint).Select(group => new { group.Key, Quantity = group.Sum(item => item.ReturnQuantity) }).ToDictionaryAsync(item => item.Key, item => item.Quantity, cancellationToken);
            invoiceAllocations = invoiceAllocations.Select(item => item with { PreviouslyCreditedQuantity = consumedAllocations.GetValueOrDefault(item.SourceAllocationFingerprint), RemainingCreditableQuantity = Math.Max(0m, item.RecognizedQuantity - consumedAllocations.GetValueOrDefault(item.SourceAllocationFingerprint)) }).ToArray();
        }
        var sourceLines = new List<SalesCustomerReturnSourceLineRecord>();
        foreach (var deliveryLine in deliveryLines)
        {
            if (!orderLines.TryGetValue(deliveryLine.OrderLineId, out var orderLine) || deliveryLine.Quantity <= 0m) return null;
            var prior = consumed.GetValueOrDefault(deliveryLine.OrderLineId);
            var currentLine = currentLines.GetValueOrDefault(deliveryLine.OrderLineId);
            var unitGross = decimal.Round(orderLine.LineTotal / orderLine.Quantity, 8, MidpointRounding.ToEven);
            var unitTax = decimal.Round(orderLine.TaxAmount / orderLine.Quantity, 8, MidpointRounding.ToEven);
            sourceLines.Add(new(deliveryLine.OrderLineId, orderLine.ProductId, orderLine.ProductSku, orderLine.ProductName, orderLine.UnitOfMeasureId, orderLine.UnitOfMeasureCode, deliveryLine.Quantity, prior, Math.Max(0m, deliveryLine.Quantity - prior), decimal.Round(unitGross - unitTax, 8, MidpointRounding.ToEven), unitTax, unitGross, null, currentLine?.ReturnQuantity ?? 0m, currentLine?.Id, orderLine.TaxId, orderLine.TaxRateVersionId, currentLine?.ReceivedQuantity ?? 0m, currentLine?.InspectedQuantity ?? 0m, currentLine?.CommerciallyAcceptedQuantity ?? 0m, currentLine?.RestockedQuantity ?? 0m, currentLine?.NonRestockableAcceptedQuantity ?? 0m, currentLine?.RejectedQuantity ?? 0m, currentLine?.StockDisposition ?? "PendingInspection", currentLine is null ? [] : DeserializeIds(currentLine.InventoryMovementIdsJson), currentLine is null ? [] : DeserializeIds(currentLine.DeliveryMovementIdsJson), currentLine?.DeliveryUnitCost));
        }
        var invoices = invoiceAllocations.Select(item => item.InvoiceId).Distinct().ToArray();
        return new(current?.Id ?? Guid.Empty, delivery.Id, delivery.OrderId, delivery.OrderRevisionNumber, delivery.TenantId.Value, delivery.CompanyId, delivery.BranchId, delivery.CustomerId, delivery.WarehouseId, delivery.PostedAt, invoices.Length == 1 ? invoices[0] : null, invoiceAllocations.Where(item => item.InvoiceId == (invoices.Length == 1 ? invoices[0] : Guid.Empty)).Select(item => item.FinanceOpenItemId).FirstOrDefault(), order.CurrencyCode, sourceLines, current?.Status ?? SalesCustomerReturnStatus.Approved, current?.Consequence ?? SalesCustomerReturnConsequence.None, current?.Version ?? delivery.Version, invoiceAllocations);
    }

    private static IReadOnlyList<SalesCustomerReturnInvoiceAllocationRecord> BuildInvoiceAllocations(IReadOnlyList<SalesInvoiceRequestEntity> invoices, Guid deliveryId, int orderRevision, string currencyCode, CancellationToken cancellationToken)
    {
        var result = new List<SalesCustomerReturnInvoiceAllocationRecord>();
        foreach (var invoice in invoices.OrderBy(item => item.InvoiceDate).ThenBy(item => item.CreatedAt).ThenBy(item => item.Id))
        {
            var persisted = ReadInvoiceLines(invoice.LinesJson);
            var invoiceFingerprint = Hash(new { invoice.Id, invoice.Version, invoice.SourceSnapshotJson, invoice.LinesJson });
            foreach (var evidence in persisted.Evidence)
            {
                var complete = evidence.Allocations
                    .Where(item => item.OrderRevisionNumber == orderRevision && item.ConsumedQuantity > 0m)
                    .ToArray();
                var totalQuantity = complete.Sum(item => item.ConsumedQuantity);
                if (totalQuantity <= 0m) continue;
                var netUsed = 0m; var taxUsed = 0m; var grossUsed = 0m;
                for (var index = 0; index < complete.Length; index++)
                {
                    var allocation = complete[index];
                    var isLast = index == complete.Length - 1;
                    var net = isLast ? evidence.NetAmount - netUsed : Round(evidence.NetAmount * allocation.ConsumedQuantity / totalQuantity);
                    var tax = isLast ? evidence.TaxAmount - taxUsed : Round(evidence.TaxAmount * allocation.ConsumedQuantity / totalQuantity);
                    var gross = isLast ? evidence.GrossAmount - grossUsed : Round(evidence.GrossAmount * allocation.ConsumedQuantity / totalQuantity);
                    netUsed += net; taxUsed += tax; grossUsed += gross;
                    var sourceFingerprint = Hash(new { invoice.Id, allocation.DeliveryId, allocation.OrderLineId, allocation.OrderRevisionNumber, allocation.SourceQuantity, allocation.ConsumedQuantity, index });
                    if (allocation.DeliveryId == deliveryId) result.Add(new(StableId(sourceFingerprint), invoice.Id, invoice.FinanceOpenItemId, allocation.DeliveryId, allocation.OrderLineId, allocation.OrderRevisionNumber, allocation.ConsumedQuantity, 0m, 0m, 0m, allocation.ConsumedQuantity, net, tax, gross, currencyCode, evidence.TaxEvidence?.TaxId, evidence.TaxEvidence?.RateVersionId, evidence.TaxEvidence?.RateVersionNumber, sourceFingerprint, invoiceFingerprint));
                }
            }
        }
        return result;
    }

    private static SalesCustomerReturnInvoiceAllocationRecord ToAllocationRecord(SalesCustomerReturnInvoiceAllocationEntity item) => new(item.Id, item.InvoiceId, item.FinanceOpenItemId, item.DeliveryId, item.OrderLineId, item.OrderRevisionNumber, item.RecognizedQuantity, item.ReturnQuantity, item.CommerciallyAcceptedQuantity, item.PreviouslyCreditedQuantity, item.RemainingCreditableQuantity, item.NetAmount, item.TaxAmount, item.GrossAmount, item.CurrencyCode, item.TaxId, item.TaxRateVersionId, item.TaxRateVersionNumber, item.SourceAllocationFingerprint, item.SourceInvoiceFingerprint);
    private static IReadOnlyList<Guid> DeserializeIds(string json) => JsonSerializer.Deserialize<IReadOnlyList<Guid>>(json, Json) ?? [];
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    private static decimal Round(decimal value) => decimal.Round(value, 8, MidpointRounding.ToEven);

    private static (IReadOnlyList<SalesInvoiceRequestLine> Lines, IReadOnlyList<SalesInvoiceLineEvidence> Evidence) ReadInvoiceLines(string json)
    {
        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedInvoiceLines>(json, Json);
            if (persisted?.Lines is not null && persisted.Evidence is not null) return (persisted.Lines, persisted.Evidence);
        }
        catch (JsonException) { }
        return (JsonSerializer.Deserialize<IReadOnlyList<SalesInvoiceRequestLine>>(json, Json) ?? [], []);
    }

    private sealed record PersistedInvoiceLines(IReadOnlyList<SalesInvoiceRequestLine> Lines, IReadOnlyList<SalesInvoiceLineEvidence> Evidence);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));

    private static IReadOnlyList<SalesQuotationLineResponse> Lines(string json) => JsonSerializer.Deserialize<IReadOnlyList<SalesQuotationLineResponse>>(json, Json) ?? [];
    private static SalesCustomerReturnResponse ToResponse(SalesCustomerReturnEntity item, IReadOnlyList<SalesCustomerReturnInvoiceAllocationEntity>? allocations = null) => new(item.Id, item.TenantId.Value, item.DeliveryId, item.OrderId, item.OrderRevisionNumber, item.CompanyId, item.BranchId, item.CustomerId, item.WarehouseId, item.InvoiceId, item.FinanceOpenItemId, item.Status, item.Consequence, item.ReturnDate, item.Reason, item.HandoffJson, item.CreatedAt, item.UpdatedAt, item.Lines.Select(line => new SalesCustomerReturnLineResponse(line.Id, line.OrderLineId, line.DeliveredQuantity, line.PreviouslyReturnedQuantity, line.ReturnQuantity, line.Reason)).ToArray(), JsonSerializer.Deserialize<IReadOnlyList<SalesCustomerReturnEvidenceReference>>(item.EvidenceJson, Json) ?? [], item.Version);
    private static bool InScope(ProcurementRequestContext context, Guid companyId, Guid? branchId) => context.TenantContext.Scope is not { } scope || ScopeMatches(scope.Value, companyId, branchId);
    private static bool ScopeMatches(string value, Guid companyId, Guid? branchId) { var parts = value.Split(':', 2); return parts.Length == 2 && Guid.TryParse(parts[1], out var id) && (parts[0] switch { "Tenant" => true, "Company" => companyId == id, "Branch" => branchId == id, _ => false }); }
    private static SalesHistoryAction ActionToHistory(SalesCustomerReturnMutation action) => action switch { SalesCustomerReturnMutation.Submit => SalesHistoryAction.Submitted, SalesCustomerReturnMutation.Approve => SalesHistoryAction.Approved, SalesCustomerReturnMutation.Reject => SalesHistoryAction.Rejected, _ => SalesHistoryAction.Cancelled };
    private static SalesCustomerReturnOperationResult<SalesCustomerReturnResponse> Failure(string code) => SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure(code);
    private static void AddHistory(SalesDbContext db, ProcurementRequestContext context, Guid id, SalesHistoryAction action, Enum? from, Enum? to, string? reason, string fingerprint) => db.History.Add(new SalesHistoryEntity(context.TenantId, "customer-return", id, action, from?.ToString(), to?.ToString(), context.ActorId, reason, null, null, null, fingerprint, DateTimeOffset.UtcNow));
    private static void AddHistory(SalesDbContext db, TenantContext context, Guid id, SalesHistoryAction action, Enum? from, Enum? to, string? reason, string fingerprint) => db.History.Add(new SalesHistoryEntity(context.TenantId, "customer-return", id, action, from?.ToString(), to?.ToString(), context.ActorId ?? Guid.Empty, reason, null, null, null, fingerprint, DateTimeOffset.UtcNow));
    private static void AddAudit(SalesDbContext db, ProcurementRequestContext context, string operation, Guid id, string decision, string? reason, string? before, string? after, string? key, string fingerprint, DateTimeOffset at) => db.Audit.Add(new SalesAuditEntity(context.TenantId, operation, "customer-return", id, context.ActorId, at, decision, reason, before, after, key, context.CorrelationId?.Value ?? "sales"));
    private static void AddAudit(SalesDbContext db, TenantContext context, string operation, Guid id, string decision, string? reason, string? before, string? after, string? key, string fingerprint, DateTimeOffset at) => db.Audit.Add(new SalesAuditEntity(context.TenantId, operation, "customer-return", id, context.ActorId ?? Guid.Empty, at, decision, reason, before, after, key, context.CorrelationId?.Value ?? "sales"));
    private static async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>?> ReadReplayAsync(SalesDbContext db, ProcurementRequestContext context, string operation, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var row = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.OperationId == operation && item.Key == key, cancellationToken);
        if (row is null) return null;
        return row.Fingerprint == fingerprint ? SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(JsonSerializer.Deserialize<SalesCustomerReturnResponse>(row.ResponseJson, Json)!) : SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("idempotency_conflict");
    }
    private static Task SaveReplayAsync<TCommand>(SalesDbContext db, ProcurementRequestContext context, string operation, TCommand command, SalesCustomerReturnResponse response, string type, Guid id, CancellationToken cancellationToken) where TCommand : notnull
    {
        var key = command switch { SalesCustomerReturnCreateCommand create => create.IdempotencyKey, SalesCustomerReturnActionCommand action => action.IdempotencyKey, _ => null };
        var fingerprint = command switch { SalesCustomerReturnCreateCommand create => create.RequestFingerprint, SalesCustomerReturnActionCommand action => action.RequestFingerprint, _ => string.Empty };
        if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new SalesIdempotencyEntity(context.TenantId, operation, key!, fingerprint, type, id, JsonSerializer.Serialize(response, Json), DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
}

#pragma warning restore CS1591
