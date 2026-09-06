#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Finance;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinanceCreditNoteEntity : FinanceEntity
{
    private FinanceCreditNoteEntity()
    {
        CurrencyCode = FunctionalCurrencyCode = SourceEvidence = HandoffState = TaxReversalJournalIdsJson = string.Empty;
        FinanceCommitState = SalesAcknowledgementState = ReconciliationState = "Pending";
        ReversalFinanceCommitState = ReversalSalesAcknowledgementState = ReversalReconciliationState = "Pending";
        Lines = [];
    }
    internal FinanceCreditNoteEntity(TenantId tenantId, FinanceCreditNoteCreateRequest request, Guid id, Guid deliveryId, Guid? invoiceId, Guid financeOpenItemId, Guid companyId, Guid customerId, string currencyCode, string functionalCurrencyCode, decimal net, decimal tax, decimal gross, decimal functionalAmount, string evidence, decimal? exchangeRate = null, Guid? exchangeRateId = null, Guid? exchangeRateVersionId = null, int? exchangeRateVersionNumber = null) : base(tenantId, id)
    { SalesCustomerReturnId = request.SalesCustomerReturnId; DeliveryId = deliveryId; InvoiceId = invoiceId; FinanceOpenItemId = financeOpenItemId; CompanyId = companyId; CustomerId = customerId; Status = FinanceCreditNoteStatus.Draft; CurrencyCode = currencyCode; FunctionalCurrencyCode = functionalCurrencyCode; NetAmount = net; TaxAmount = tax; GrossAmount = gross; FunctionalAmount = functionalAmount; ExchangeRate = exchangeRate; ExchangeRateId = exchangeRateId; ExchangeRateVersionId = exchangeRateVersionId; ExchangeRateVersionNumber = exchangeRateVersionNumber; SourceEvidence = evidence; HandoffState = "NotCommitted"; TaxReversalJournalIdsJson = "[]"; CreditNoteDate = request.CreditNoteDate; Reason = request.Reason; CreatedAt = DateTimeOffset.UtcNow; }
    internal Guid SalesCustomerReturnId { get; private set; }
    internal Guid DeliveryId { get; private set; }
    internal Guid? InvoiceId { get; private set; }
    internal Guid FinanceOpenItemId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal FinanceCreditNoteStatus Status { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal decimal NetAmount { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal decimal GrossAmount { get; private set; }
    internal decimal FunctionalAmount { get; private set; }
    internal decimal? ExchangeRate { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal string SourceEvidence { get; private set; }
    internal string HandoffState { get; private set; }
    internal DateOnly CreditNoteDate { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal string? Reason { get; private set; }
    internal Guid? CustomerCreditId { get; private set; }
    internal Guid? PostingJournalId { get; private set; }
    internal Guid? ReversalJournalId { get; private set; }
    internal string TaxReversalJournalIdsJson { get; private set; }
    internal string FinanceCommitState { get; private set; } = "NotCommitted";
    internal string SalesAcknowledgementState { get; private set; } = "NotAcknowledged";
    internal string ReconciliationState { get; private set; } = "Pending";
    internal Guid? FinanceEffectId { get; private set; }
    internal string? EffectFingerprint { get; private set; }
    internal string? RequestFingerprint { get; private set; }
    internal string? SourceFingerprint { get; private set; }
    internal string? DownstreamIdempotencyKey { get; private set; }
    internal int AttemptCount { get; private set; }
    internal string? LastError { get; private set; }
    internal DateTimeOffset? LastAttemptAt { get; private set; }
    internal DateTimeOffset? AcknowledgedAt { get; private set; }
    internal string ReversalFinanceCommitState { get; private set; } = "NotCommitted";
    internal string ReversalSalesAcknowledgementState { get; private set; } = "NotAcknowledged";
    internal string ReversalReconciliationState { get; private set; } = "Pending";
    internal Guid? ReversalFinanceEffectId { get; private set; }
    internal string? ReversalEffectFingerprint { get; private set; }
    internal string? ReversalRequestFingerprint { get; private set; }
    internal string? ReversalDownstreamIdempotencyKey { get; private set; }
    internal int ReversalAttemptCount { get; private set; }
    internal string? ReversalLastError { get; private set; }
    internal DateTimeOffset? ReversalLastAttemptAt { get; private set; }
    internal DateTimeOffset? ReversalAcknowledgedAt { get; private set; }
    internal List<FinanceCreditNoteLineEntity> Lines { get; private set; } = [];
    internal void SetStatus(FinanceCreditNoteStatus status, DateTimeOffset at) { Status = status; if (status == FinanceCreditNoteStatus.Posted) PostedAt = at; HandoffState = status == FinanceCreditNoteStatus.Posted ? "Committed" : status is FinanceCreditNoteStatus.Unknown or FinanceCreditNoteStatus.ReconciliationRequired ? "ReconciliationRequired" : HandoffState; TouchVersion(); }
    internal void SetCredit(Guid id) { CustomerCreditId = id; TouchVersion(); }
    internal void SetPostingJournal(Guid id) { PostingJournalId = id; TouchVersion(); }
    internal void SetReversalJournal(Guid id) { ReversalJournalId = id; TouchVersion(); }
    internal void SetTaxReversalJournals(IEnumerable<Guid> ids) { TaxReversalJournalIdsJson = System.Text.Json.JsonSerializer.Serialize(ids.Distinct()); TouchVersion(); }
    internal void SetPostCoordination(Guid effectId, string effectFingerprint, string requestFingerprint, string sourceFingerprint, string downstreamIdempotencyKey)
    {
        FinanceCommitState = "Committed";
        SalesAcknowledgementState = "NotAcknowledged";
        ReconciliationState = "Required";
        FinanceEffectId = effectId;
        EffectFingerprint = effectFingerprint;
        RequestFingerprint = requestFingerprint;
        SourceFingerprint = sourceFingerprint;
        DownstreamIdempotencyKey = downstreamIdempotencyKey;
        LastError = null;
        TouchVersion();
    }
    internal void RecordPostAcknowledgement(bool acknowledged, string? error, DateTimeOffset at)
    {
        SalesAcknowledgementState = acknowledged ? "Acknowledged" : "NotAcknowledged";
        ReconciliationState = acknowledged ? "Reconciled" : "Required";
        LastError = acknowledged ? null : error;
        AttemptCount++;
        LastAttemptAt = at;
        AcknowledgedAt = acknowledged ? at : AcknowledgedAt;
        HandoffState = acknowledged ? "Committed" : "ReconciliationRequired";
        TouchVersion();
    }
    internal void SetReversalCoordination(Guid effectId, string effectFingerprint, string requestFingerprint, string downstreamIdempotencyKey)
    {
        ReversalFinanceCommitState = "Committed";
        ReversalSalesAcknowledgementState = "NotAcknowledged";
        ReversalReconciliationState = "Required";
        ReversalFinanceEffectId = effectId;
        ReversalEffectFingerprint = effectFingerprint;
        ReversalRequestFingerprint = requestFingerprint;
        ReversalDownstreamIdempotencyKey = downstreamIdempotencyKey;
        ReversalLastError = null;
        TouchVersion();
    }
    internal void RecordReversalAcknowledgement(bool acknowledged, string? error, DateTimeOffset at)
    {
        ReversalSalesAcknowledgementState = acknowledged ? "Acknowledged" : "NotAcknowledged";
        ReversalReconciliationState = acknowledged ? "Reconciled" : "Required";
        ReversalLastError = acknowledged ? null : error;
        ReversalAttemptCount++;
        ReversalLastAttemptAt = at;
        ReversalAcknowledgedAt = acknowledged ? at : ReversalAcknowledgedAt;
        TouchVersion();
    }
}

internal sealed class FinanceCreditNoteLineEntity : FinanceEntity
{
    private FinanceCreditNoteLineEntity() { CurrencyCode = string.Empty; }
    internal FinanceCreditNoteLineEntity(TenantId tenantId, Guid id, Guid creditNoteId, Guid orderLineId, decimal quantity, decimal net, decimal tax, decimal gross, string currencyCode, Guid? taxId, Guid? taxRateVersionId, int? taxRateVersionNumber, Guid sourceAllocationId, decimal recognizedQuantity, decimal recognizedNetAmount, decimal recognizedTaxAmount, decimal recognizedGrossAmount, string sourceAllocationFingerprint) : base(tenantId, id)
    { CreditNoteId = creditNoteId; OrderLineId = orderLineId; Quantity = quantity; NetAmount = net; TaxAmount = tax; GrossAmount = gross; CurrencyCode = currencyCode; TaxId = taxId; TaxRateVersionId = taxRateVersionId; TaxRateVersionNumber = taxRateVersionNumber; SourceAllocationId = sourceAllocationId; RecognizedQuantity = recognizedQuantity; RecognizedNetAmount = recognizedNetAmount; RecognizedTaxAmount = recognizedTaxAmount; RecognizedGrossAmount = recognizedGrossAmount; SourceAllocationFingerprint = sourceAllocationFingerprint; }
    internal Guid CreditNoteId { get; private set; }
    internal Guid OrderLineId { get; private set; }
    internal decimal Quantity { get; private set; }
    internal decimal NetAmount { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal decimal GrossAmount { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal Guid? TaxId { get; private set; }
    internal Guid? TaxRateVersionId { get; private set; }
    internal int? TaxRateVersionNumber { get; private set; }
    internal Guid SourceAllocationId { get; private set; }
    internal decimal RecognizedQuantity { get; private set; }
    internal decimal RecognizedNetAmount { get; private set; }
    internal decimal RecognizedTaxAmount { get; private set; }
    internal decimal RecognizedGrossAmount { get; private set; }
    internal string SourceAllocationFingerprint { get; private set; } = string.Empty;
}

internal sealed class FinanceCustomerCreditEntity : FinanceEntity
{
    private FinanceCustomerCreditEntity() { CurrencyCode = string.Empty; }
    internal FinanceCustomerCreditEntity(TenantId tenantId, Guid id, FinanceCreditNoteEntity note) : base(tenantId, id)
    { CompanyId = note.CompanyId; CustomerId = note.CustomerId; CreditNoteId = note.Id; CurrencyCode = note.CurrencyCode; OriginalAmount = note.GrossAmount; Status = FinanceCustomerCreditStatus.Available; }
    internal Guid CompanyId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal Guid CreditNoteId { get; private set; }
    internal Guid? AppliedToOpenItemId { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal decimal OriginalAmount { get; private set; }
    internal decimal AppliedAmount { get; private set; }
    internal FinanceCustomerCreditStatus Status { get; private set; }
    internal decimal OutstandingAmount => Math.Max(0m, OriginalAmount - AppliedAmount);
    internal void Apply(decimal amount, Guid? openItemId) { if (amount < 0m || amount > OutstandingAmount) throw new InvalidOperationException("credit_over_application"); AppliedAmount += amount; AppliedToOpenItemId = openItemId; Status = OutstandingAmount == 0m ? FinanceCustomerCreditStatus.FullyApplied : FinanceCustomerCreditStatus.PartiallyApplied; TouchVersion(); }
    internal void Reverse() { AppliedAmount = 0m; AppliedToOpenItemId = null; Status = FinanceCustomerCreditStatus.Reversed; TouchVersion(); }
}

internal sealed class FinanceCustomerCreditApplicationEntity : FinanceEntity
{
    private FinanceCustomerCreditApplicationEntity() { CurrencyCode = string.Empty; }
    internal FinanceCustomerCreditApplicationEntity(TenantId tenantId, Guid id, Guid creditId, Guid openItemId, Guid companyId, Guid customerId, decimal amount, string currencyCode, Guid creditNoteId, DateOnly date) : base(tenantId, id)
    { CustomerCreditId = creditId; OpenItemId = openItemId; CompanyId = companyId; CustomerId = customerId; Amount = amount; CurrencyCode = currencyCode; CreditNoteId = creditNoteId; ApplicationDate = date; }
    internal Guid CustomerCreditId { get; private set; }
    internal Guid OpenItemId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal decimal Amount { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal Guid CreditNoteId { get; private set; }
    internal DateOnly ApplicationDate { get; private set; }
    internal bool Reversed { get; private set; }
    internal void Reverse() { Reversed = true; TouchVersion(); }
}

#pragma warning restore CS1591
