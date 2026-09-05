#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

internal sealed class SalesCustomerReturnEntity : ITenantOwned
{
    private SalesCustomerReturnEntity()
    {
        CurrencyCode = string.Empty;
        EvidenceJson = "[]";
        HandoffJson = "{}";
        FinanceCreditNoteIdsJson = "[]";
        FinanceReversedCreditNoteIdsJson = "[]";
        FinanceEffects = [];
    }

    internal SalesCustomerReturnEntity(TenantId tenantId, Guid id, SalesCustomerReturnCreateRequest request, SalesCustomerReturnSourceRecord source, Guid actorId, DateTimeOffset at)
    {
        Id = id;
        TenantId = tenantId;
        DeliveryId = source.DeliveryId;
        OrderId = source.OrderId;
        OrderRevisionNumber = source.OrderRevisionNumber;
        CompanyId = source.CompanyId;
        BranchId = source.BranchId;
        CustomerId = source.CustomerId;
        WarehouseId = source.WarehouseId;
        InvoiceId = request.InvoiceId ?? source.RecognizedInvoiceId;
        FinanceOpenItemId = source.FinanceOpenItemId;
        CurrencyCode = source.CurrencyCode;
        Status = SalesCustomerReturnStatus.Draft;
        Consequence = request.Consequence;
        ReturnDate = request.ReturnDate;
        Reason = request.Reason;
        EvidenceJson = JsonSerializer.Serialize(request.Evidence ?? []);
        HandoffJson = JsonSerializer.Serialize(new { State = "NotCommitted", Reconciliation = "Pending", RequestFingerprint = string.Empty });
        InventoryCommitState = "NotCommitted";
        InventoryAcknowledgementState = "NotAcknowledged";
        InventoryReconciliationState = "Pending";
        InventoryPhysicalEvidenceReference = string.Empty;
        InventoryInspectionEvidenceReference = string.Empty;
        InventoryCorrelationId = string.Empty;
        CreatedByActorId = actorId;
        CreatedAt = at;
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid DeliveryId { get; private set; }
    internal Guid OrderId { get; private set; }
    internal int OrderRevisionNumber { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid? InvoiceId { get; private set; }
    internal Guid? FinanceOpenItemId { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal SalesCustomerReturnStatus Status { get; private set; }
    internal SalesCustomerReturnConsequence Consequence { get; private set; }
    internal DateOnly ReturnDate { get; private set; }
    internal string? Reason { get; private set; }
    internal string EvidenceJson { get; private set; } = "[]";
    internal string HandoffJson { get; private set; } = "{}";
    internal Guid? InventoryEffectId { get; private set; }
    internal string? InventoryEffectFingerprint { get; private set; }
    internal string? InventoryRequestFingerprint { get; private set; }
    internal string InventoryCommitState { get; private set; } = "NotCommitted";
    internal string InventoryAcknowledgementState { get; private set; } = "NotAcknowledged";
    internal string InventoryReconciliationState { get; private set; } = "Pending";
    internal string InventoryPhysicalEvidenceReference { get; private set; } = string.Empty;
    internal string InventoryInspectionEvidenceReference { get; private set; } = string.Empty;
    internal int InventoryAttemptCount { get; private set; }
    internal string? InventoryLastError { get; private set; }
    internal DateTimeOffset? InventoryLastAttemptAt { get; private set; }
    internal string InventoryCorrelationId { get; private set; } = string.Empty;
    internal string FinanceEffectState { get; private set; } = "NotRequired";
    internal int ActiveFinanceCreditNoteCount { get; private set; }
    internal string FinanceCreditNoteIdsJson { get; private set; } = "[]";
    internal string FinanceReversedCreditNoteIdsJson { get; private set; } = "[]";
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<SalesCustomerReturnLineEntity> Lines { get; } = [];
    internal List<SalesCustomerReturnFinanceEffectEntity> FinanceEffects { get; } = [];

    internal void SetStatus(SalesCustomerReturnStatus status, DateTimeOffset at)
    {
        Status = status;
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }

    internal void AcknowledgeInventory(SalesCustomerReturnInventoryAcknowledgementCommand command, DateTimeOffset at)
    {
        if (command.TenantId != TenantId.Value) throw new InvalidOperationException("tenant_mismatch");
        if (command.InventoryEffectId == Guid.Empty || string.IsNullOrWhiteSpace(command.EffectFingerprint) || string.IsNullOrWhiteSpace(command.RequestFingerprint) || string.IsNullOrWhiteSpace(command.PhysicalEvidenceReference) || command.Lines is null || command.Lines.Count != Lines.Count || command.Lines.Select(item => item.OrderLineId).Distinct().Count() != command.Lines.Count || command.Lines.Any(item => item.OrderLineId == Guid.Empty || Lines.All(line => line.OrderLineId != item.OrderLineId))) throw new InvalidOperationException("inventory_effect_mismatch");
        if (InventoryEffectId is { } effect && (effect != command.InventoryEffectId || !string.Equals(InventoryEffectFingerprint, command.EffectFingerprint, StringComparison.Ordinal))) throw new InvalidOperationException("inventory_effect_mismatch");
        InventoryEffectId = command.InventoryEffectId;
        InventoryEffectFingerprint = command.EffectFingerprint;
        InventoryRequestFingerprint = command.RequestFingerprint;
        InventoryCommitState = command.CommitState;
        InventoryAcknowledgementState = "Acknowledged";
        InventoryReconciliationState = "Reconciled";
        InventoryPhysicalEvidenceReference = command.PhysicalEvidenceReference;
        InventoryInspectionEvidenceReference = command.InspectionEvidenceReference;
        InventoryLastError = null;
        InventoryAttemptCount++;
        InventoryLastAttemptAt = at;
        InventoryCorrelationId = command.CorrelationId;
        foreach (var line in Lines)
        {
            var result = command.Lines.SingleOrDefault(value => value.OrderLineId == line.OrderLineId);
            if (result is not null) line.ApplyInventoryEvidence(result);
        }
        var received = Lines.Sum(item => item.ReceivedQuantity);
        var inspected = Lines.Sum(item => item.InspectedQuantity);
        var requested = Lines.Sum(item => item.ReturnQuantity);
        SetStatus(inspected < received || received < requested
            ? received == 0m ? SalesCustomerReturnStatus.AwaitingReceipt : received < requested ? SalesCustomerReturnStatus.PartiallyReceived : SalesCustomerReturnStatus.Received
            : SalesCustomerReturnStatus.Completed, at);
        HandoffJson = JsonSerializer.Serialize(new { State = InventoryCommitState, Acknowledgement = InventoryAcknowledgementState, Reconciliation = InventoryReconciliationState, EffectId = InventoryEffectId, EffectFingerprint = InventoryEffectFingerprint, RequestFingerprint = InventoryRequestFingerprint, PhysicalEvidence = InventoryPhysicalEvidenceReference, InspectionEvidence = InventoryInspectionEvidenceReference, AttemptCount = InventoryAttemptCount, LastAttemptAt = InventoryLastAttemptAt, CorrelationId = InventoryCorrelationId });
    }

    internal void RecordInventoryFailure(SalesCustomerReturnInventoryFailureCommand command)
    {
        if (command.TenantId != TenantId.Value) throw new InvalidOperationException("tenant_mismatch");
        if (command.InventoryEffectId == Guid.Empty || string.IsNullOrWhiteSpace(command.EffectFingerprint) || string.IsNullOrWhiteSpace(command.RequestFingerprint) || string.IsNullOrWhiteSpace(command.Error) || InventoryEffectId is { } effect && (effect != command.InventoryEffectId || !string.Equals(InventoryEffectFingerprint, command.EffectFingerprint, StringComparison.Ordinal))) throw new InvalidOperationException("inventory_effect_mismatch");
        InventoryEffectId = command.InventoryEffectId;
        InventoryEffectFingerprint = command.EffectFingerprint;
        InventoryRequestFingerprint = command.RequestFingerprint;
        InventoryCommitState = "Committed";
        InventoryAcknowledgementState = "NotAcknowledged";
        InventoryReconciliationState = "Required";
        InventoryLastError = command.Error;
        InventoryAttemptCount++;
        InventoryLastAttemptAt = command.OccurredAt;
        InventoryCorrelationId = command.CorrelationId;
        SetStatus(SalesCustomerReturnStatus.ReconciliationRequired, command.OccurredAt);
        HandoffJson = JsonSerializer.Serialize(new { State = InventoryCommitState, Acknowledgement = InventoryAcknowledgementState, Reconciliation = InventoryReconciliationState, EffectId = InventoryEffectId, EffectFingerprint = InventoryEffectFingerprint, RequestFingerprint = InventoryRequestFingerprint, Error = InventoryLastError, AttemptCount = InventoryAttemptCount, LastAttemptAt = InventoryLastAttemptAt, CorrelationId = InventoryCorrelationId });
    }

    internal void RecordDownstreamReversal(SalesCustomerReturnDownstreamReversalCommand command)
    {
        if (command.TenantId != TenantId.Value) throw new InvalidOperationException("tenant_mismatch");
        if (string.Equals(command.Downstream, "inventory", StringComparison.OrdinalIgnoreCase))
        {
            InventoryCommitState = "Reversed";
            InventoryAcknowledgementState = "Acknowledged";
            InventoryReconciliationState = "Reconciled";
        }
        else if (string.Equals(command.Downstream, "finance", StringComparison.OrdinalIgnoreCase))
        {
            var activeIds = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(FinanceCreditNoteIdsJson) ?? [];
            var reversedIds = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(FinanceReversedCreditNoteIdsJson) ?? [];
            if (command.CreditNoteId is { } creditNoteId)
            {
                if (creditNoteId == Guid.Empty || command.ReversalJournalId is not { } reversalJournalId || reversalJournalId == Guid.Empty || command.OriginalJournalId is not { } originalJournalId || originalJournalId == Guid.Empty || string.IsNullOrWhiteSpace(command.EffectFingerprint) || string.IsNullOrWhiteSpace(command.RequestFingerprint) || !string.Equals(command.CommitState, "Committed", StringComparison.Ordinal)) throw new InvalidOperationException("finance_effect_mismatch");
                if (reversedIds.Contains(creditNoteId)) return;
                if (!activeIds.Contains(creditNoteId)) throw new InvalidOperationException("finance_effect_mismatch");
                activeIds = activeIds.Where(item => item != creditNoteId).ToArray();
                reversedIds = reversedIds.Append(creditNoteId).Distinct().ToArray();
                FinanceCreditNoteIdsJson = JsonSerializer.Serialize(activeIds);
                FinanceReversedCreditNoteIdsJson = JsonSerializer.Serialize(reversedIds);
            }
            else
            {
                if (ActiveFinanceCreditNoteCount == 0) throw new InvalidOperationException("finance_effect_mismatch");
                ActiveFinanceCreditNoteCount = Math.Max(0, ActiveFinanceCreditNoteCount - 1);
                if (activeIds.Count > ActiveFinanceCreditNoteCount)
                {
                    activeIds = activeIds.Take(ActiveFinanceCreditNoteCount).ToArray();
                    FinanceCreditNoteIdsJson = JsonSerializer.Serialize(activeIds);
                }
            }
            if (command.CreditNoteId is not null) ActiveFinanceCreditNoteCount = activeIds.Count;
            FinanceEffectState = ActiveFinanceCreditNoteCount == 0 ? "Reversed" : "Committed";
        }
        else throw new InvalidOperationException("downstream_reversal_mismatch");
        UpdatedAt = command.OccurredAt;
        Version = Guid.NewGuid().ToByteArray();
    }

    internal void RegisterFinanceCreditNote(SalesCustomerReturnFinanceEffectCommand command, DateTimeOffset at)
    {
        if (command.TenantId != TenantId.Value || command.ReturnId != Id || command.CreditNoteId == Guid.Empty || command.InvoiceId == Guid.Empty || command.SourceAllocationIds is null || command.SourceAllocationIds.Count == 0 || command.SourceAllocationIds.Any(item => item == Guid.Empty)) throw new InvalidOperationException("finance_effect_mismatch");
        var richEffect = command.FinanceOpenItemId is not null || command.PostingJournalId is not null || command.TaxJournalIds is not null || command.NetAmount is not null || command.TaxAmount is not null || command.GrossAmount is not null || command.CurrencyCode is not null || command.SourceFingerprint is not null || command.EffectFingerprint is not null || command.RequestFingerprint is not null || command.CommitState is not null || command.DownstreamIdempotencyKey is not null;
        if (richEffect && (command.CompanyId is not { } companyId || companyId != CompanyId || command.CustomerId is not { } customerId || customerId != CustomerId || command.FinanceOpenItemId is not { } openItemId || FinanceOpenItemId is { } rootOpenItem && openItemId != rootOpenItem || command.PostingJournalId is not { } postingJournalId || postingJournalId == Guid.Empty || command.TaxJournalIds is null || command.TaxJournalIds.Any(item => item == Guid.Empty) || command.TaxJournalIds.Distinct().Count() != command.TaxJournalIds.Count || (command.TaxAmount is { } positiveTaxCheck && positiveTaxCheck > 0m && command.TaxJournalIds.Count == 0) || command.NetAmount is not { } net || command.TaxAmount is not { } tax || command.GrossAmount is not { } gross || net < 0m || tax < 0m || gross <= 0m || Math.Round(net + tax, 8, MidpointRounding.ToEven) != gross || string.IsNullOrWhiteSpace(command.CurrencyCode) || !string.Equals(command.CommitState, "Committed", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(command.SourceFingerprint) || string.IsNullOrWhiteSpace(command.EffectFingerprint) || string.IsNullOrWhiteSpace(command.RequestFingerprint) || string.IsNullOrWhiteSpace(command.DownstreamIdempotencyKey))) throw new InvalidOperationException("finance_effect_mismatch");
        var ids = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(FinanceCreditNoteIdsJson) ?? [];
        if (ids.Contains(command.CreditNoteId)) return;
        FinanceCreditNoteIdsJson = JsonSerializer.Serialize(ids.Append(command.CreditNoteId).Distinct());
        ActiveFinanceCreditNoteCount = ids.Append(command.CreditNoteId).Distinct().Count();
        FinanceEffectState = "Committed";
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }
}

internal sealed class SalesCustomerReturnFinanceEffectEntity : ITenantOwned
{
    private SalesCustomerReturnFinanceEffectEntity()
    {
        CurrencyCode = SourceFingerprint = EffectFingerprint = RequestFingerprint = DownstreamIdempotencyKey = ReversalEffectFingerprint = ReversalRequestFingerprint = ReversalDownstreamIdempotencyKey = SourceAllocationIdsJson = TaxJournalIdsJson = string.Empty;
        State = ReversalState = "Active";
        Allocations = [];
    }

    internal SalesCustomerReturnFinanceEffectEntity(TenantId tenantId, Guid id, SalesCustomerReturnFinanceEffectCommand command, IReadOnlyList<SalesCustomerReturnFinanceAllocationEffect> allocations, DateTimeOffset at)
    {
        Id = id;
        TenantId = tenantId;
        CustomerReturnId = command.ReturnId;
        CreditNoteId = command.CreditNoteId;
        CompanyId = command.CompanyId!.Value;
        CustomerId = command.CustomerId!.Value;
        InvoiceId = command.InvoiceId;
        FinanceOpenItemId = command.FinanceOpenItemId!.Value;
        PostingJournalId = command.PostingJournalId!.Value;
        TaxJournalIdsJson = JsonSerializer.Serialize((command.TaxJournalIds ?? []).Distinct().OrderBy(item => item).ToArray());
        SourceAllocationIdsJson = JsonSerializer.Serialize(allocations.Select(item => item.SourceAllocationId).OrderBy(item => item).ToArray());
        NetAmount = command.NetAmount!.Value;
        TaxAmount = command.TaxAmount!.Value;
        GrossAmount = command.GrossAmount!.Value;
        CurrencyCode = command.CurrencyCode!;
        SourceFingerprint = command.SourceFingerprint!;
        EffectFingerprint = command.EffectFingerprint!;
        RequestFingerprint = command.RequestFingerprint!;
        DownstreamIdempotencyKey = command.DownstreamIdempotencyKey!;
        State = "Active";
        ReversalState = "NotReversed";
        AcknowledgedAt = at;
        Version = Guid.NewGuid().ToByteArray();
        Allocations = allocations.Select(item => new SalesCustomerReturnFinanceEffectAllocationEntity(tenantId, Guid.NewGuid(), id, item, at)).ToList();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CustomerReturnId { get; private set; }
    internal Guid CreditNoteId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal Guid InvoiceId { get; private set; }
    internal Guid FinanceOpenItemId { get; private set; }
    internal Guid PostingJournalId { get; private set; }
    internal string TaxJournalIdsJson { get; private set; }
    internal decimal NetAmount { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal decimal GrossAmount { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal string SourceAllocationIdsJson { get; private set; }
    internal string SourceFingerprint { get; private set; }
    internal string EffectFingerprint { get; private set; }
    internal string RequestFingerprint { get; private set; }
    internal string DownstreamIdempotencyKey { get; private set; }
    internal string State { get; private set; }
    internal DateTimeOffset AcknowledgedAt { get; private set; }
    internal string ReversalState { get; private set; }
    internal Guid? ReversalJournalId { get; private set; }
    internal Guid? ReversalEffectId { get; private set; }
    internal string? ReversalEffectFingerprint { get; private set; }
    internal string? ReversalRequestFingerprint { get; private set; }
    internal string? ReversalDownstreamIdempotencyKey { get; private set; }
    internal DateTimeOffset? ReversedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<SalesCustomerReturnFinanceEffectAllocationEntity> Allocations { get; private set; } = [];

    internal bool MatchesPost(SalesCustomerReturnFinanceEffectCommand command, IReadOnlyList<SalesCustomerReturnFinanceAllocationEffect> allocations) =>
        command.CreditNoteId == CreditNoteId && command.CompanyId == CompanyId && command.CustomerId == CustomerId && command.InvoiceId == InvoiceId && command.FinanceOpenItemId == FinanceOpenItemId && command.PostingJournalId == PostingJournalId && command.NetAmount == NetAmount && command.TaxAmount == TaxAmount && command.GrossAmount == GrossAmount && string.Equals(command.CurrencyCode, CurrencyCode, StringComparison.Ordinal) && string.Equals(command.SourceFingerprint, SourceFingerprint, StringComparison.Ordinal) && string.Equals(command.EffectFingerprint, EffectFingerprint, StringComparison.Ordinal) && string.Equals(command.RequestFingerprint, RequestFingerprint, StringComparison.Ordinal) && string.Equals(command.DownstreamIdempotencyKey, DownstreamIdempotencyKey, StringComparison.Ordinal) && TaxJournalIdsMatch(command.TaxJournalIds) && allocations.Select(item => item.SourceAllocationId).OrderBy(item => item).SequenceEqual(Allocations.Select(item => item.SourceAllocationId).OrderBy(item => item)) && allocations.All(item => Allocations.Any(stored => stored.SourceAllocationId == item.SourceAllocationId && stored.Quantity == item.Quantity && stored.NetAmount == item.NetAmount && stored.TaxAmount == item.TaxAmount && stored.GrossAmount == item.GrossAmount && string.Equals(stored.SourceAllocationFingerprint, item.SourceAllocationFingerprint, StringComparison.Ordinal)));

    private bool TaxJournalIdsMatch(IReadOnlyList<Guid>? incoming)
    {
        if (incoming is null || incoming.Any(item => item == Guid.Empty) || incoming.Distinct().Count() != incoming.Count) return false;
        var stored = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(TaxJournalIdsJson) ?? [];
        return incoming.OrderBy(item => item).SequenceEqual(stored.OrderBy(item => item));
    }

    internal void Reverse(SalesCustomerReturnDownstreamReversalCommand command, DateTimeOffset at)
    {
        if (command.CreditNoteId != CreditNoteId || command.OriginalCompanyId != CompanyId || command.OriginalCustomerId != CustomerId || command.OriginalInvoiceId != InvoiceId || command.OriginalFinanceOpenItemId != FinanceOpenItemId || command.OriginalPostingJournalId != PostingJournalId || command.OriginalSourceAllocationIds is null || !JsonSerializer.Deserialize<IReadOnlyList<Guid>>(SourceAllocationIdsJson)!.OrderBy(item => item).SequenceEqual(command.OriginalSourceAllocationIds.OrderBy(item => item)) || command.OriginalTaxJournalIds is null || !JsonSerializer.Deserialize<IReadOnlyList<Guid>>(TaxJournalIdsJson)!.OrderBy(item => item).SequenceEqual(command.OriginalTaxJournalIds.OrderBy(item => item)) || command.OriginalNetAmount != NetAmount || command.OriginalTaxAmount != TaxAmount || command.OriginalGrossAmount != GrossAmount || !string.Equals(command.OriginalCurrencyCode, CurrencyCode, StringComparison.Ordinal) || !string.Equals(command.OriginalSourceFingerprint, SourceFingerprint, StringComparison.Ordinal) || !string.Equals(command.OriginalEffectFingerprint, EffectFingerprint, StringComparison.Ordinal) || !string.Equals(command.OriginalDownstreamIdempotencyKey, DownstreamIdempotencyKey, StringComparison.Ordinal) || command.ReversalJournalId is not { } reversalJournalId || reversalJournalId == Guid.Empty || string.IsNullOrWhiteSpace(command.EffectFingerprint) || string.IsNullOrWhiteSpace(command.RequestFingerprint) || string.IsNullOrWhiteSpace(command.DownstreamIdempotencyKey) || !string.Equals(command.CommitState, "Committed", StringComparison.Ordinal)) throw new InvalidOperationException("finance_effect_mismatch");
        if (State == "Reversed")
        {
            if (ReversalJournalId == command.ReversalJournalId && ReversalEffectId == command.ReversalJournalId && string.Equals(ReversalEffectFingerprint, command.EffectFingerprint, StringComparison.Ordinal) && string.Equals(ReversalRequestFingerprint, command.RequestFingerprint, StringComparison.Ordinal) && string.Equals(ReversalDownstreamIdempotencyKey, command.DownstreamIdempotencyKey, StringComparison.Ordinal)) return;
            throw new InvalidOperationException("finance_effect_mismatch");
        }
        if (State != "Active") throw new InvalidOperationException("finance_effect_mismatch");
        State = "Reversed";
        ReversalState = "Reversed";
        ReversalJournalId = command.ReversalJournalId;
        ReversalEffectId = command.ReversalJournalId;
        ReversalEffectFingerprint = command.EffectFingerprint;
        ReversalRequestFingerprint = command.RequestFingerprint;
        ReversalDownstreamIdempotencyKey = command.DownstreamIdempotencyKey;
        ReversedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }
}

internal sealed class SalesCustomerReturnFinanceEffectAllocationEntity : ITenantOwned
{
    private SalesCustomerReturnFinanceEffectAllocationEntity() { SourceAllocationFingerprint = string.Empty; }
    internal SalesCustomerReturnFinanceEffectAllocationEntity(TenantId tenantId, Guid id, Guid effectId, SalesCustomerReturnFinanceAllocationEffect evidence, DateTimeOffset at)
    {
        Id = id; TenantId = tenantId; FinanceEffectId = effectId; SourceAllocationId = evidence.SourceAllocationId; Quantity = evidence.Quantity; NetAmount = evidence.NetAmount; TaxAmount = evidence.TaxAmount; GrossAmount = evidence.GrossAmount; SourceAllocationFingerprint = evidence.SourceAllocationFingerprint; ConsumedAt = at; Version = Guid.NewGuid().ToByteArray();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid FinanceEffectId { get; private set; }
    internal Guid SourceAllocationId { get; private set; }
    internal decimal Quantity { get; private set; }
    internal decimal NetAmount { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal decimal GrossAmount { get; private set; }
    internal string SourceAllocationFingerprint { get; private set; }
    internal DateTimeOffset ConsumedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SalesCustomerReturnLineEntity : ITenantOwned
{
    private SalesCustomerReturnLineEntity()
    {
        Reason = null;
    }

    internal SalesCustomerReturnLineEntity(TenantId tenantId, Guid id, Guid returnId, Guid deliveryId, SalesCustomerReturnLineRequest request, SalesCustomerReturnSourceLineRecord source)
    {
        Id = id;
        TenantId = tenantId;
        CustomerReturnId = returnId;
        DeliveryId = deliveryId;
        OrderLineId = request.OrderLineId;
        DeliveredQuantity = source.DeliveredQuantity;
        PreviouslyReturnedQuantity = source.AlreadyReturnedQuantity;
        ReturnQuantity = request.Quantity;
        Reason = request.Reason;
        Version = Guid.NewGuid().ToByteArray();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CustomerReturnId { get; private set; }
    internal Guid DeliveryId { get; private set; }
    internal Guid OrderLineId { get; private set; }
    internal decimal DeliveredQuantity { get; private set; }
    internal decimal PreviouslyReturnedQuantity { get; private set; }
    internal decimal ReturnQuantity { get; private set; }
    internal string? Reason { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal decimal ReceivedQuantity { get; private set; }
    internal decimal InspectedQuantity { get; private set; }
    internal decimal CommerciallyAcceptedQuantity { get; private set; }
    internal decimal RestockedQuantity { get; private set; }
    internal decimal NonRestockableAcceptedQuantity { get; private set; }
    internal decimal RejectedQuantity { get; private set; }
    internal string StockDisposition { get; private set; } = "PendingInspection";
    internal string InventoryMovementIdsJson { get; private set; } = "[]";
    internal string DeliveryMovementIdsJson { get; private set; } = "[]";
    internal decimal? DeliveryUnitCost { get; private set; }

    internal void ApplyInventoryEvidence(SalesCustomerReturnInventoryAcknowledgementLine result)
    {
        if (result.ReceivedQuantity < 0m || result.InspectedQuantity < 0m || result.CommerciallyAcceptedQuantity < 0m || result.RestockedQuantity < 0m || result.NonRestockableAcceptedQuantity < 0m || result.RejectedQuantity < 0m || result.ReceivedQuantity > ReturnQuantity || result.InspectedQuantity > result.ReceivedQuantity || result.CommerciallyAcceptedQuantity + result.RejectedQuantity > result.InspectedQuantity || result.RestockedQuantity + result.NonRestockableAcceptedQuantity > result.CommerciallyAcceptedQuantity || string.IsNullOrWhiteSpace(result.StockDisposition) || result.InventoryMovementIds is null || result.DeliveryMovementIds is null) throw new InvalidOperationException("inventory_quantity_mismatch");
        ReceivedQuantity = result.ReceivedQuantity;
        InspectedQuantity = result.InspectedQuantity;
        CommerciallyAcceptedQuantity = result.CommerciallyAcceptedQuantity;
        RestockedQuantity = result.RestockedQuantity;
        NonRestockableAcceptedQuantity = result.NonRestockableAcceptedQuantity;
        RejectedQuantity = result.RejectedQuantity;
        StockDisposition = result.StockDisposition;
        InventoryMovementIdsJson = JsonSerializer.Serialize(result.InventoryMovementIds.Distinct());
        DeliveryMovementIdsJson = JsonSerializer.Serialize(result.DeliveryMovementIds.Distinct());
        DeliveryUnitCost = result.DeliveryUnitCost;
        Version = Guid.NewGuid().ToByteArray();
    }

}

internal sealed class SalesCustomerReturnInvoiceAllocationEntity : ITenantOwned
{
    private SalesCustomerReturnInvoiceAllocationEntity() { CurrencyCode = SourceAllocationFingerprint = SourceInvoiceFingerprint = string.Empty; }

    internal SalesCustomerReturnInvoiceAllocationEntity(TenantId tenantId, Guid id, Guid customerReturnId, SalesCustomerReturnInvoiceAllocationRecord record)
    {
        Id = id; TenantId = tenantId; CustomerReturnId = customerReturnId; InvoiceId = record.InvoiceId; FinanceOpenItemId = record.FinanceOpenItemId; DeliveryId = record.DeliveryId; OrderLineId = record.OrderLineId; OrderRevisionNumber = record.OrderRevisionNumber; RecognizedQuantity = record.RecognizedQuantity; ReturnQuantity = record.ReturnQuantity; CommerciallyAcceptedQuantity = record.CommerciallyAcceptedQuantity; PreviouslyCreditedQuantity = record.PreviouslyCreditedQuantity; RemainingCreditableQuantity = record.RemainingCreditableQuantity; NetAmount = record.NetAmount; TaxAmount = record.TaxAmount; GrossAmount = record.GrossAmount; CurrencyCode = record.CurrencyCode; TaxId = record.TaxId; TaxRateVersionId = record.TaxRateVersionId; TaxRateVersionNumber = record.TaxRateVersionNumber; SourceAllocationFingerprint = record.SourceAllocationFingerprint; SourceInvoiceFingerprint = record.SourceInvoiceFingerprint; Version = Guid.NewGuid().ToByteArray();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CustomerReturnId { get; private set; }
    internal Guid InvoiceId { get; private set; }
    internal Guid? FinanceOpenItemId { get; private set; }
    internal Guid DeliveryId { get; private set; }
    internal Guid OrderLineId { get; private set; }
    internal int OrderRevisionNumber { get; private set; }
    internal decimal RecognizedQuantity { get; private set; }
    internal decimal ReturnQuantity { get; private set; }
    internal decimal CommerciallyAcceptedQuantity { get; private set; }
    internal decimal PreviouslyCreditedQuantity { get; private set; }
    internal decimal RemainingCreditableQuantity { get; private set; }
    internal decimal NetAmount { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal decimal GrossAmount { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal Guid? TaxId { get; private set; }
    internal Guid? TaxRateVersionId { get; private set; }
    internal int? TaxRateVersionNumber { get; private set; }
    internal string SourceAllocationFingerprint { get; private set; }
    internal string SourceInvoiceFingerprint { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void SetCommerciallyAccepted(decimal quantity) { if (quantity < 0m || quantity > ReturnQuantity) throw new InvalidOperationException("inventory_quantity_mismatch"); CommerciallyAcceptedQuantity = quantity; RemainingCreditableQuantity = Math.Max(0m, quantity - PreviouslyCreditedQuantity); Version = Guid.NewGuid().ToByteArray(); }
}

#pragma warning restore CS1591
