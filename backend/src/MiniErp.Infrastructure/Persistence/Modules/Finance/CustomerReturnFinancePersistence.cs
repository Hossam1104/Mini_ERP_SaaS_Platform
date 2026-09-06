#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Sales;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

public sealed class CustomerReturnFinancePersistence(
    DbContextOptions options,
    ISalesCustomerReturnSourceProvider salesReturns,
    IFinanceCompanyProvider companies,
    IMasterDataExchangeRatePersistence exchangeRates) : IFinanceCustomerReturnPersistence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private FinanceDbContext Create(FinanceRequestContext context) => new(options, context.TenantContext);

    public async Task<FinanceCreditNoteResponse?> GetAsync(FinanceRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var item = await db.CreditNotes.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return item is null || !InScope(context, item.CompanyId) ? null : ToResponse(item, db);
    }

    public async Task<FinanceOperationResult<FinanceCreditNoteResponse>> CreateAsync(FinanceRequestContext context, FinanceCreditNoteCreateRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync(db, context, "finance.credit-note.create", idempotencyKey, fingerprint, cancellationToken);
        if (replay is not null) return replay;
        var source = await salesReturns.GetCustomerReturnSourceAsync(context.TenantContext, request.SalesCustomerReturnId, cancellationToken);
        if (source is null || source.Status is not (SalesCustomerReturnStatus.Received or SalesCustomerReturnStatus.Completed)) return Failure("sales_return_not_creditable");
        if (source.Consequence != SalesCustomerReturnConsequence.CreditNote) return Failure("credit_note_not_requested");
        var company = companies.List(context.TenantId).SingleOrDefault(item => item.CompanyId == source.CompanyId && item.IsActive);
        if (company is null || string.IsNullOrWhiteSpace(company.FunctionalCurrencyCode)) return Failure("company_currency_unavailable");
        var allocations = source.InvoiceAllocations?.Where(item => item.CommerciallyAcceptedQuantity > 0m).ToArray() ?? [];
        if (allocations.Length == 0) return Failure("credit_note_lines_missing");
        var invoiceIds = allocations.Select(item => item.InvoiceId).Distinct().ToArray();
        var selectedInvoiceId = request.InvoiceId ?? (invoiceIds.Length == 1 ? invoiceIds[0] : Guid.Empty);
        if (selectedInvoiceId == Guid.Empty || invoiceIds.Any(item => item == selectedInvoiceId) == false) return Failure("invoice_selector_required");
        var selected = allocations.Where(item => item.InvoiceId == selectedInvoiceId).ToArray();
        var openItemIds = selected.Select(item => item.FinanceOpenItemId).Distinct().ToArray();
        if (openItemIds.Length != 1 || openItemIds[0] == Guid.Empty) return Failure("recognized_invoice_required");
        var openItem = await db.OpenItems.SingleOrDefaultAsync(item => item.Id == openItemIds[0] && item.CompanyId == source.CompanyId && item.CustomerId == source.CustomerId && item.Kind == FinanceOpenItemKind.Receivable && item.CurrencyCode == selected[0].CurrencyCode, cancellationToken);
        if (openItem is null || openItem.RecognitionState != FinanceOpenItemRecognitionState.Recognized) return Failure("finance_open_item_unavailable");
        var previousIds = selected.Select(item => item.Id).ToArray();
        var previous = await (from line in db.CreditNoteLines
                              join note in db.CreditNotes on line.CreditNoteId equals note.Id
                              where previousIds.Contains(line.SourceAllocationId) && note.Status != FinanceCreditNoteStatus.Rejected && note.Status != FinanceCreditNoteStatus.Cancelled && note.Status != FinanceCreditNoteStatus.Reversed
                              select new { line.SourceAllocationId, line.Quantity, line.NetAmount, line.TaxAmount, line.GrossAmount }).ToListAsync(cancellationToken);
        var requested = request.Lines is { Count: > 0 }
            ? request.Lines.GroupBy(item => item.SourceAllocationId).ToDictionary(item => item.Key, item => item.Sum(value => value.Quantity))
            : selected.ToDictionary(item => item.Id, item => item.CommerciallyAcceptedQuantity - previous.Where(value => value.SourceAllocationId == item.Id).Sum(value => value.Quantity));
        if (requested.Any(item => item.Key == Guid.Empty || item.Value <= 0m)) return Failure("credit_note_lines_invalid");
        if (requested.Keys.Any(item => selected.All(value => value.Id != item))) return Failure("credit_note_source_mismatch");
        var noteLines = new List<(SalesCustomerReturnInvoiceAllocationRecord Source, decimal Quantity, decimal Net, decimal Tax, decimal Gross, decimal RecognizedQuantity, decimal RecognizedNet, decimal RecognizedTax, decimal RecognizedGross)>();
        foreach (var item in selected)
        {
            if (!requested.TryGetValue(item.Id, out var quantity) || quantity <= 0m) continue;
            var prior = previous.Where(value => value.SourceAllocationId == item.Id).ToArray();
            var priorQuantity = prior.Sum(value => value.Quantity);
            var remainingQuantity = item.CommerciallyAcceptedQuantity - priorQuantity;
            if (quantity > remainingQuantity) return Failure("credit_note_quantity_conflict");
            var acceptedRatio = item.ReturnQuantity == 0m ? 0m : item.CommerciallyAcceptedQuantity / item.ReturnQuantity;
            var recognizedNet = Round(item.NetAmount * acceptedRatio);
            var recognizedTax = Round(item.TaxAmount * acceptedRatio);
            var recognizedGross = Round(item.GrossAmount * acceptedRatio);
            if (Round(item.NetAmount + item.TaxAmount) != item.GrossAmount) return Failure("credit_note_amount_invalid");
            var net = quantity == remainingQuantity ? Round(recognizedNet - prior.Sum(value => value.NetAmount)) : Round(recognizedNet * quantity / item.CommerciallyAcceptedQuantity);
            var tax = quantity == remainingQuantity ? Round(recognizedTax - prior.Sum(value => value.TaxAmount)) : Round(recognizedTax * quantity / item.CommerciallyAcceptedQuantity);
            var gross = quantity == remainingQuantity ? Round(recognizedGross - prior.Sum(value => value.GrossAmount)) : Round(net + tax);
            if (net < 0m || tax < 0m || gross <= 0m || Round(net + tax) != gross) return Failure("credit_note_amount_invalid");
            noteLines.Add((item, quantity, net, tax, gross, item.CommerciallyAcceptedQuantity, recognizedNet, recognizedTax, recognizedGross));
        }
        if (noteLines.Count == 0) return Failure("credit_note_lines_missing");
        var netTotal = Round(noteLines.Sum(item => item.Net));
        var taxTotal = Round(noteLines.Sum(item => item.Tax));
        var grossTotal = Round(noteLines.Sum(item => item.Gross));
        if (grossTotal <= 0m || Round(netTotal + taxTotal) != grossTotal) return Failure("credit_note_amount_invalid");
        var currency = selected[0].CurrencyCode;
        var rate = string.Equals(currency, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase) ? 1m : openItem.ExchangeRate;
        if (string.Equals(currency, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            if (openItem.ExchangeRate is not null || openItem.ExchangeRateId is not null || openItem.ExchangeRateVersionId is not null || openItem.ExchangeRateVersionNumber is not null) return Failure("exchange_rate_evidence_mismatch");
        }
        else if (rate is not > 0m || openItem.ExchangeRateId is not { } rateId || openItem.ExchangeRateVersionId is not { } versionId || openItem.ExchangeRateVersionNumber is not { } versionNumber || !await ExchangeRateMatchesAsync(context, currency, company.FunctionalCurrencyCode, openItem.DocumentDate, rate.Value, rateId, versionId, versionNumber, cancellationToken)) return Failure("exchange_rate_evidence_mismatch");
        if (rate is null) return Failure("exchange_rate_evidence_mismatch");
        var functionalTotal = Round(grossTotal * rate.Value);
        var entity = new FinanceCreditNoteEntity(context.TenantId, request, Guid.NewGuid(), source.DeliveryId, selectedInvoiceId, openItem.Id, source.CompanyId, source.CustomerId, currency, company.FunctionalCurrencyCode, netTotal, taxTotal, grossTotal, functionalTotal, JsonSerializer.Serialize(new { Source = source, Request = request, Allocations = noteLines.Select(item => item.Source) }, Json), rate, string.Equals(currency, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase) ? null : openItem.ExchangeRateId, string.Equals(currency, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase) ? null : openItem.ExchangeRateVersionId, string.Equals(currency, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase) ? null : openItem.ExchangeRateVersionNumber);
        foreach (var line in noteLines) entity.Lines.Add(new FinanceCreditNoteLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, line.Source.OrderLineId, line.Quantity, line.Net, line.Tax, line.Gross, currency, line.Source.TaxId, line.Source.TaxRateVersionId, line.Source.TaxRateVersionNumber, line.Source.Id, line.RecognizedQuantity, line.RecognizedNet, line.RecognizedTax, line.RecognizedGross, line.Source.SourceAllocationFingerprint));
        db.CreditNotes.Add(entity);
        AddAudit(db, context, "finance.credit-note.create", entity.Id, "Succeeded", null, idempotencyKey);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity, db);
        await SaveReplayAsync(db, context, "finance.credit-note.create", idempotencyKey, fingerprint, "credit-note", entity.Id, response, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FinanceOperationResult<FinanceCreditNoteResponse>.Success(response);
    }

    public async Task<FinanceOperationResult<FinanceCreditNoteResponse>> MutateAsync(FinanceRequestContext context, Guid id, byte[] expectedVersion, FinanceCreditNoteMutation action, string? reason, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        if (action is FinanceCreditNoteMutation.Post or FinanceCreditNoteMutation.Reverse) return await MutatePostedOrReversedAsync(context, id, expectedVersion, action, reason, idempotencyKey, fingerprint, cancellationToken);
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var operation = $"finance.credit-note.{action.ToString().ToLowerInvariant()}";
        var replay = await ReplayAsync(db, context, operation, idempotencyKey, fingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await db.CreditNotes.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null || !InScope(context, entity.CompanyId)) return Failure("credit_note_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure("concurrency_conflict");
        var target = action switch
        {
            FinanceCreditNoteMutation.Submit when entity.Status == FinanceCreditNoteStatus.Draft => FinanceCreditNoteStatus.Submitted,
            FinanceCreditNoteMutation.Approve when entity.Status == FinanceCreditNoteStatus.Submitted => FinanceCreditNoteStatus.Approved,
            FinanceCreditNoteMutation.Reject when entity.Status is FinanceCreditNoteStatus.Draft or FinanceCreditNoteStatus.Submitted => FinanceCreditNoteStatus.Rejected,
            FinanceCreditNoteMutation.Cancel when entity.Status is FinanceCreditNoteStatus.Draft or FinanceCreditNoteStatus.Submitted => FinanceCreditNoteStatus.Cancelled,
            _ => (FinanceCreditNoteStatus?)null
        };
        if (target is null) return Failure("credit_note_transition_invalid");
        var before = entity.Status;
        entity.SetStatus(target.Value, DateTimeOffset.UtcNow);
        AddAudit(db, context, operation, entity.Id, "Succeeded", reason, idempotencyKey);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity, db);
        await SaveReplayAsync(db, context, operation, idempotencyKey, fingerprint, "credit-note", entity.Id, response, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FinanceOperationResult<FinanceCreditNoteResponse>.Success(response);
    }

    private async Task<FinanceOperationResult<FinanceCreditNoteResponse>> MutatePostedOrReversedAsync(FinanceRequestContext context, Guid id, byte[] expectedVersion, FinanceCreditNoteMutation action, string? reason, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        var operation = $"finance.credit-note.{action.ToString().ToLowerInvariant()}";
        SalesCustomerReturnFinanceEffectCommand? financeCommand = null;
        SalesCustomerReturnDownstreamReversalCommand? reversalCommand = null;
        try
        {
            await using var db = Create(context);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var replay = await ReadIdempotencyAsync(db, context, operation, idempotencyKey, cancellationToken);
            if (replay is not null && replay.Fingerprint != fingerprint) return Failure("idempotency_conflict");
            var entity = await db.CreditNotes.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null || !InScope(context, entity.CompanyId)) return Failure("credit_note_not_found");
            if (action == FinanceCreditNoteMutation.Post && entity.FinanceCommitState == "Committed" && entity.PostingJournalId is not null && entity.CustomerCreditId is not null)
            {
                if (!string.Equals(entity.RequestFingerprint, fingerprint, StringComparison.Ordinal) || !string.Equals(entity.DownstreamIdempotencyKey, DownstreamKey(idempotencyKey, "post", entity.Id), StringComparison.Ordinal)) return Failure("idempotency_conflict");
                if (entity.SalesAcknowledgementState == "Acknowledged") return FinanceOperationResult<FinanceCreditNoteResponse>.Success(ToResponse(entity, db));
                financeCommand = PostCommand(context, entity);
            }
            else if (action == FinanceCreditNoteMutation.Reverse && entity.ReversalFinanceCommitState == "Committed" && entity.ReversalJournalId is not null)
            {
                if (!string.Equals(entity.ReversalRequestFingerprint, fingerprint, StringComparison.Ordinal) || !string.Equals(entity.ReversalDownstreamIdempotencyKey, DownstreamKey(idempotencyKey, "reverse", entity.Id), StringComparison.Ordinal)) return Failure("idempotency_conflict");
                if (entity.ReversalSalesAcknowledgementState == "Acknowledged") return FinanceOperationResult<FinanceCreditNoteResponse>.Success(ToResponse(entity, db));
                reversalCommand = ReversalCommand(context, entity);
            }
            else
            {
                if (!entity.Version.SequenceEqual(expectedVersion)) return Failure("concurrency_conflict");
                if (action == FinanceCreditNoteMutation.Post)
                {
                    if (entity.Status != FinanceCreditNoteStatus.Approved) return Failure("credit_note_transition_invalid");
                    var openItem = await db.OpenItems.SingleOrDefaultAsync(item => item.Id == entity.FinanceOpenItemId && item.CompanyId == entity.CompanyId && item.CustomerId == entity.CustomerId && item.Kind == FinanceOpenItemKind.Receivable, cancellationToken);
                    if (openItem is null || openItem.CurrencyCode != entity.CurrencyCode || openItem.RecognitionState != FinanceOpenItemRecognitionState.Recognized) return Failure("finance_open_item_unavailable");
                    var allocated = await db.Allocations.Where(item => item.OpenItemId == openItem.Id && item.Status == FinanceAllocationStatus.Active).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
                    var credited = await db.CustomerCreditApplications.Where(item => item.OpenItemId == openItem.Id && !item.Reversed).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
                    var apply = Math.Min(entity.GrossAmount, Math.Max(0m, openItem.OriginalAmount - allocated - credited));
                    var rule = await FindRuleAsync(db, entity.CompanyId, "sales-credit-note.v1", "posting", entity.CreditNoteDate, cancellationToken);
                    if (rule is null) return Failure("pending_mapping");
                    var journal = await CreatePostedJournalAsync(db, context, entity, openItem, rule, cancellationToken);
                    if (!journal.Succeeded || journal.Value is null) return Failure(journal.Code);
                    var taxJournals = await CreateTaxReversalJournalsAsync(db, context, entity, rule, openItem.DocumentDate, cancellationToken);
                    if (!taxJournals.Succeeded) return Failure(taxJournals.Code);
                    var credit = new FinanceCustomerCreditEntity(context.TenantId, Guid.NewGuid(), entity);
                    db.CustomerCredits.Add(credit);
                    if (apply > 0m)
                    {
                        credit.Apply(apply, openItem.Id);
                        db.CustomerCreditApplications.Add(new FinanceCustomerCreditApplicationEntity(context.TenantId, Guid.NewGuid(), credit.Id, openItem.Id, entity.CompanyId, entity.CustomerId, apply, entity.CurrencyCode, entity.Id, entity.CreditNoteDate));
                    }
                    entity.SetCredit(credit.Id);
                    entity.SetPostingJournal(journal.Value.Value);
                    entity.SetTaxReversalJournals(taxJournals.JournalIds);
                    entity.SetStatus(FinanceCreditNoteStatus.Posted, DateTimeOffset.UtcNow);
                    var sourceFingerprint = Hash(entity.SourceEvidence);
                    var effectFingerprint = Hash(new { entity.Id, PostingJournalId = journal.Value.Value, TaxJournalIds = taxJournals.JournalIds, entity.NetAmount, entity.TaxAmount, entity.GrossAmount, entity.CurrencyCode });
                    entity.SetPostCoordination(journal.Value.Value, effectFingerprint, fingerprint, sourceFingerprint, DownstreamKey(idempotencyKey, "post", entity.Id));
                    AddAudit(db, context, operation, entity.Id, "Succeeded", reason, idempotencyKey);
                    await db.SaveChangesAsync(cancellationToken);
                    var response = ToResponse(entity, db);
                    await SaveOrUpdateReplayAsync(db, context, operation, idempotencyKey, fingerprint, "credit-note", entity.Id, response, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    financeCommand = PostCommand(context, entity);
                }
                else
                {
                    if (entity.Status != FinanceCreditNoteStatus.Posted || entity.PostingJournalId is not { } postingJournalId || entity.ReversalJournalId is not null) return Failure("credit_note_transition_invalid");
                    var original = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == postingJournalId, cancellationToken);
                    if (original is null) return Failure("credit_note_posting_missing");
                    var reversal = await CreateReversalJournalAsync(db, context, original, entity.CreditNoteDate, reason ?? "Credit note reversal", cancellationToken);
                    if (!reversal.Succeeded || reversal.Value is null) return Failure(reversal.Code);
                    entity.SetReversalJournal(reversal.Value.Value);
                    var taxJournalIds = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(entity.TaxReversalJournalIdsJson, Json) ?? [];
                    var reversalIds = new List<Guid> { reversal.Value.Value };
                    foreach (var taxJournalId in taxJournalIds)
                    {
                        var taxJournal = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == taxJournalId, cancellationToken);
                        if (taxJournal is null || taxJournal.ReversalJournalId is not null) return Failure("tax_reversal_lineage_missing");
                        var taxReversal = await CreateReversalJournalAsync(db, context, taxJournal, entity.CreditNoteDate, reason ?? "Credit note tax reversal", cancellationToken);
                        if (!taxReversal.Succeeded || taxReversal.Value is null) return Failure(taxReversal.Code);
                        reversalIds.Add(taxReversal.Value.Value);
                    }
                    if (entity.CustomerCreditId is not { } creditId) return Failure("customer_credit_missing");
                    var credit = await db.CustomerCredits.SingleOrDefaultAsync(item => item.Id == creditId, cancellationToken);
                    if (credit is not null) { credit.Reverse(); foreach (var application in await db.CustomerCreditApplications.Where(item => item.CustomerCreditId == creditId && !item.Reversed).ToListAsync(cancellationToken)) application.Reverse(); }
                    entity.SetStatus(FinanceCreditNoteStatus.Reversed, DateTimeOffset.UtcNow);
                    var effectFingerprint = Hash(new { entity.Id, OriginalJournalId = postingJournalId, ReversalJournalIds = reversalIds, entity.GrossAmount });
                    entity.SetReversalCoordination(reversal.Value.Value, effectFingerprint, fingerprint, DownstreamKey(idempotencyKey, "reverse", entity.Id));
                    AddAudit(db, context, operation, entity.Id, "Succeeded", reason, idempotencyKey);
                    await db.SaveChangesAsync(cancellationToken);
                    var response = ToResponse(entity, db);
                    await SaveOrUpdateReplayAsync(db, context, operation, idempotencyKey, fingerprint, "credit-note", entity.Id, response, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    reversalCommand = ReversalCommand(context, entity, postingJournalId);
                }
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("finance_commit_failed");
        }

        var acknowledged = false;
        var acknowledgementCode = action == FinanceCreditNoteMutation.Post ? "sales_finance_acknowledgement_required" : "sales_finance_reversal_required";
        try
        {
            var result = action == FinanceCreditNoteMutation.Post
                ? await salesReturns.RegisterFinanceCreditNoteAsync(context.TenantContext, financeCommand!, cancellationToken)
                : await salesReturns.RecordDownstreamReversalAsync(context.TenantContext, reversalCommand!, cancellationToken);
            acknowledged = result.Succeeded;
            if (!acknowledged && !string.IsNullOrWhiteSpace(result.Code)) acknowledgementCode = result.Code;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            acknowledgementCode = exception.Message;
        }

        try
        {
            await using var db = Create(context);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var entity = await db.CreditNotes.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null) return Failure("credit_note_not_found");
            if (action == FinanceCreditNoteMutation.Post) entity.RecordPostAcknowledgement(acknowledged, acknowledged ? null : acknowledgementCode, DateTimeOffset.UtcNow);
            else entity.RecordReversalAcknowledgement(acknowledged, acknowledged ? null : acknowledgementCode, DateTimeOffset.UtcNow);
            AddAudit(db, context, operation, entity.Id, acknowledged ? "Succeeded" : "ReconciliationRequired", acknowledged ? null : acknowledgementCode, idempotencyKey);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToResponse(entity, db);
            await SaveOrUpdateReplayAsync(db, context, operation, idempotencyKey, fingerprint, "credit-note", entity.Id, response, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return acknowledged ? FinanceOperationResult<FinanceCreditNoteResponse>.Success(response) : Failure(acknowledgementCode);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("finance_reconciliation_persist_failed");
        }
    }

    private static decimal Round(decimal value) => decimal.Round(value, 8, MidpointRounding.ToEven);
    private async Task<bool> ExchangeRateMatchesAsync(FinanceRequestContext context, string sourceCurrency, string targetCurrency, DateOnly date, decimal rate, Guid rateId, Guid versionId, int versionNumber, CancellationToken cancellationToken)
    {
        var record = await exchangeRates.FindExchangeRateAsync(context.TenantContext, rateId, cancellationToken);
        var version = record?.Versions.SingleOrDefault(item => item.Id == versionId && item.VersionNumber == versionNumber);
        return record is not null && version is not null && string.Equals(record.SourceCurrencyCode, sourceCurrency, StringComparison.OrdinalIgnoreCase) && string.Equals(record.TargetCurrencyCode, targetCurrency, StringComparison.OrdinalIgnoreCase) && version.Rate == rate && version.EffectiveFrom <= date && (version.EffectiveTo is null || version.EffectiveTo >= date) && string.Equals(version.SourceCurrencyCode, sourceCurrency, StringComparison.OrdinalIgnoreCase) && string.Equals(version.TargetCurrencyCode, targetCurrency, StringComparison.OrdinalIgnoreCase);
    }
    private async Task<(bool Succeeded, string Code, Guid? Value)> CreatePostedJournalAsync(FinanceDbContext db, FinanceRequestContext context, FinanceCreditNoteEntity entity, FinanceOpenItemEntity openItem, FinancePostingRuleEntity rule, CancellationToken cancellationToken)
    {
        var period = await db.FiscalPeriods.Where(item => item.CompanyId == entity.CompanyId && item.StartDate <= entity.CreditNoteDate && item.EndDate >= entity.CreditNoteDate).SingleOrDefaultAsync(cancellationToken);
        if (period is null) return (false, "period_not_configured", null);
        if (period.State != FinanceFiscalPeriodState.Open) return (false, period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", null);
        var accounts = await db.Accounts.Where(item => item.CompanyId == entity.CompanyId && (item.Id == rule.DebitAccountId || item.Id == rule.CreditAccountId)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (accounts.Count != 2 || accounts.Values.Any(item => !item.IsPostingAccount || item.Lifecycle != FinanceAccountLifecycle.Active || item.EffectiveFrom > entity.CreditNoteDate || item.EffectiveTo < entity.CreditNoteDate)) return (false, "account_not_postable", null);
        var recognition = openItem.RecognitionJournalId is { } recognitionId
            ? await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == recognitionId, cancellationToken)
            : null;
        if (recognition is null || recognition.Status != FinanceJournalStatus.Posted || recognition.SourceContract != "sales-invoice.v1" || recognition.SourceEvent != "recognition" || recognition.SourceEvidenceId != openItem.SourceEvidenceId || recognition.SourceEvidenceVersion != openItem.SourceEvidenceVersion || !recognition.Lines.Any(item => item.AccountId == rule.CreditAccountId && item.FunctionalDebit == openItem.OriginalFunctionalAmount) || !recognition.Lines.Any(item => item.AccountId == rule.DebitAccountId && item.FunctionalCredit > 0m)) return (false, "posting_lineage_missing", null);
        if (await db.Journals.AnyAsync(item => item.SourceContract == "sales-credit-note.v1" && item.SourceEvidenceId == entity.Id && item.SourceEvidenceVersion == 1, cancellationToken)) return (false, "source_effect_exists", null);
        var journalId = Guid.NewGuid();
        var rate = entity.ExchangeRate ?? 1m;
        var functionalGross = Round(entity.GrossAmount * rate);
        var command = new FinanceJournalCommand(entity.CompanyId, entity.CreditNoteDate, entity.CreditNoteDate, entity.CurrencyCode, rate, entity.ExchangeRateId, entity.ExchangeRateVersionId, entity.ExchangeRateVersionNumber, "sales-credit-note.v1", "posting", entity.Id, 1, rule.Id, entity.Reason ?? "Customer credit note", [new(rule.DebitAccountId, entity.GrossAmount, 0m, entity.GrossAmount, entity.CurrencyCode, null, "Customer credit note revenue consequence"), new(rule.CreditAccountId, 0m, entity.GrossAmount, entity.GrossAmount, entity.CurrencyCode, null, "Customer credit note AR reduction")], journalId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var journal = new FinanceJournalEntity(context.TenantId, journalId, command, await NextJournalSequenceAsync(db, entity.CompanyId, cancellationToken), entity.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow);
        journal.SetCorrelation(context.CorrelationId); journal.SetPeriod(period.FiscalYearId, period.Id); journal.SetRule(rule.Id, rule.VersionNumber); journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow);
        journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 1, accounts[rule.DebitAccountId], command.Lines[0], null, functionalGross, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 2, accounts[rule.CreditAccountId], command.Lines[1], null, 0m, functionalGross, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        var evidence = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, entity.CompanyId, openItem.DocumentDate, entity.CurrencyCode, entity.GrossAmount, entity.FunctionalCurrencyCode, functionalGross, entity.ExchangeRate, entity.ExchangeRateId, entity.ExchangeRateVersionId, entity.ExchangeRateVersionNumber, cancellationToken);
        if (!evidence.Succeeded) return (false, evidence.Code, null);
        db.Journals.Add(journal);
        if (evidence.Evidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), journal.Id, entity.CompanyId, null, evidence.Evidence, DateTimeOffset.UtcNow));
        db.SourceEffects.Add(new FinanceSourceEffectEntity(context.TenantId, Guid.NewGuid(), entity.CompanyId, "sales-credit-note.v1", entity.Id, 1, journal.Id, DateTimeOffset.UtcNow));
        return (true, "succeeded", journal.Id);
    }

    private async Task<(bool Succeeded, string Code, IReadOnlyList<Guid> JournalIds)> CreateTaxReversalJournalsAsync(FinanceDbContext db, FinanceRequestContext context, FinanceCreditNoteEntity entity, FinancePostingRuleEntity postingRule, DateOnly sourceDate, CancellationToken cancellationToken)
    {
        var taxable = entity.Lines.Where(item => item.TaxAmount > 0m).ToArray();
        if (taxable.Length == 0) return (true, "succeeded", []);
        if (taxable.Any(item => item.TaxId is null || item.TaxRateVersionId is null || item.TaxRateVersionNumber is not > 0)) return (false, "tax_evidence_not_authoritative", []);
        var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.CompanyId == entity.CompanyId && item.StartDate <= entity.CreditNoteDate && item.EndDate >= entity.CreditNoteDate, cancellationToken);
        if (period is null) return (false, "period_not_configured", []);
        if (period.State != FinanceFiscalPeriodState.Open) return (false, period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", []);
        var rate = entity.ExchangeRate ?? 1m;
        var result = new List<Guid>();
        foreach (var group in taxable.GroupBy(item => new { item.TaxId, item.TaxRateVersionId, item.TaxRateVersionNumber }))
        {
            var amount = Round(group.Sum(item => item.TaxAmount));
            if (amount <= 0m) continue;
            var originalEffect = await db.TaxAccountingEffects.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == entity.CompanyId && item.OpenItemId == entity.FinanceOpenItemId && item.TaxId == group.Key.TaxId!.Value && item.ReversalJournalId == null && item.Status == FinanceEvidenceStatus.Captured, cancellationToken);
            if (originalEffect is null) return (false, "tax_source_effect_missing", []);
            var original = await db.Journals.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == originalEffect.JournalId, cancellationToken);
            if (original is null || original.Status != FinanceJournalStatus.Posted || original.SourceContract != "finance-tax.v1" || original.SourceEvent != "output" || originalEffect.PostingRuleId != original.PostingRuleId || original.Lines.Count < 2) return (false, "tax_source_effect_missing", []);
            var originalTaxLine = original.Lines.Where(item => item.FunctionalCredit > 0m).OrderBy(item => item.LineNumber).FirstOrDefault();
            var originalSourceLine = original.Lines.Where(item => item.FunctionalDebit > 0m).OrderBy(item => item.LineNumber).FirstOrDefault();
            if (originalTaxLine is null || originalSourceLine is null || originalTaxLine.AccountId == originalSourceLine.AccountId || amount > Round(originalEffect.TaxAmount)) return (false, "tax_source_effect_mismatch", []);
            var priorTax = await db.CreditNoteLines.Where(item => item.TaxId == group.Key.TaxId!.Value && item.CreditNoteId != entity.Id && db.CreditNotes.Any(note => note.Id == item.CreditNoteId && note.FinanceOpenItemId == entity.FinanceOpenItemId && note.Status == FinanceCreditNoteStatus.Posted)).SumAsync(item => (decimal?)item.TaxAmount, cancellationToken) ?? 0m;
            if (Round(priorTax + amount) > Round(originalEffect.TaxAmount)) return (false, "tax_amount_exceeds_source_effect", []);
            var accountIds = new[] { originalTaxLine.AccountId, originalSourceLine.AccountId }.Distinct().ToArray();
            var accounts = await db.Accounts.Where(item => item.CompanyId == entity.CompanyId && accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            if (accounts.Count != accountIds.Length || accounts.Values.Any(item => !item.IsPostingAccount || item.Lifecycle != FinanceAccountLifecycle.Active || item.EffectiveFrom > entity.CreditNoteDate || item.EffectiveTo < entity.CreditNoteDate)) return (false, "account_not_postable", []);
            var sourceEvidenceId = StableGuid(entity.Id, group.Key.TaxId!.Value);
            if (await db.SourceEffects.AnyAsync(item => item.SourceContract == "sales-credit-note.tax.v1" && item.SourceEvidenceId == sourceEvidenceId && item.SourceEvidenceVersion == 1, cancellationToken)) return (false, "source_effect_exists", []);
            var functionalAmount = Round(amount * rate);
            var journalId = Guid.NewGuid();
            var command = new FinanceJournalCommand(entity.CompanyId, entity.CreditNoteDate, entity.CreditNoteDate, entity.CurrencyCode, rate, entity.ExchangeRateId, entity.ExchangeRateVersionId, entity.ExchangeRateVersionNumber, "sales-credit-note.tax.v1", "output-reversal", sourceEvidenceId, 1, original.PostingRuleId, entity.Reason ?? "Customer credit note tax reversal", [new(originalTaxLine.AccountId, amount, 0m, amount, entity.CurrencyCode, null, "Customer credit note tax reversal"), new(originalSourceLine.AccountId, 0m, amount, amount, entity.CurrencyCode, null, "Customer credit note tax reversal")], journalId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
            var now = DateTimeOffset.UtcNow;
            var journal = new FinanceJournalEntity(context.TenantId, journalId, command, await NextJournalSequenceAsync(db, entity.CompanyId, cancellationToken), entity.FunctionalCurrencyCode, context.ActorId, now);
            journal.SetCorrelation(context.CorrelationId); journal.SetPeriod(period.FiscalYearId, period.Id); if (original.PostingRuleId is { } originalRuleId && original.PostingRuleVersionNumber is { } originalRuleVersion) journal.SetRule(originalRuleId, originalRuleVersion); journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, now);
            journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 1, accounts[originalTaxLine.AccountId], command.Lines[0], null, functionalAmount, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 2, accounts[originalSourceLine.AccountId], command.Lines[1], null, 0m, functionalAmount, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            var evidence = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, entity.CompanyId, sourceDate, entity.CurrencyCode, amount, entity.FunctionalCurrencyCode, functionalAmount, entity.ExchangeRate, entity.ExchangeRateId, entity.ExchangeRateVersionId, entity.ExchangeRateVersionNumber, cancellationToken);
            if (!evidence.Succeeded || evidence.Evidence is null) return (false, evidence.Code, []);
            db.Journals.Add(journal); db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), journal.Id, entity.CompanyId, null, evidence.Evidence, now)); db.SourceEffects.Add(new FinanceSourceEffectEntity(context.TenantId, Guid.NewGuid(), entity.CompanyId, "sales-credit-note.tax.v1", sourceEvidenceId, 1, journal.Id, now)); result.Add(journal.Id);
        }
        return (true, "succeeded", result);
    }

    private static Guid StableGuid(Guid first, Guid second) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{first:D}:{second:D}"))[..16]);

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));

    private static string DownstreamKey(string? idempotencyKey, string action, Guid id) => $"finance-credit-note:{action}:{idempotencyKey ?? id.ToString("N")}";

    private static SalesCustomerReturnFinanceEffectCommand PostCommand(FinanceRequestContext context, FinanceCreditNoteEntity entity) =>
        new(entity.SalesCustomerReturnId, context.TenantId.Value, entity.Id, entity.InvoiceId ?? Guid.Empty, entity.Lines.Select(item => item.SourceAllocationId).ToArray(), DateTimeOffset.UtcNow, entity.FinanceOpenItemId, entity.PostingJournalId, JsonSerializer.Deserialize<IReadOnlyList<Guid>>(entity.TaxReversalJournalIdsJson, Json) ?? [], entity.NetAmount, entity.TaxAmount, entity.GrossAmount, entity.CurrencyCode, entity.SourceFingerprint, entity.EffectFingerprint, entity.RequestFingerprint, entity.FinanceCommitState, entity.DownstreamIdempotencyKey, entity.Lines.Select(item => new SalesCustomerReturnFinanceAllocationEffect(item.SourceAllocationId, item.Quantity, item.NetAmount, item.TaxAmount, item.GrossAmount, item.SourceAllocationFingerprint)).ToArray(), entity.CompanyId, entity.CustomerId);

    private static SalesCustomerReturnDownstreamReversalCommand ReversalCommand(FinanceRequestContext context, FinanceCreditNoteEntity entity, Guid? originalJournalId = null) =>
        new(entity.SalesCustomerReturnId, context.TenantId.Value, "finance", context.CorrelationId, DateTimeOffset.UtcNow, entity.Id, entity.ReversalJournalId, originalJournalId ?? entity.PostingJournalId, entity.ReversalEffectFingerprint, entity.ReversalRequestFingerprint, entity.ReversalFinanceCommitState, entity.ReversalDownstreamIdempotencyKey, entity.InvoiceId, entity.FinanceOpenItemId, originalJournalId ?? entity.PostingJournalId, entity.Lines.Select(item => item.SourceAllocationId).ToArray(), JsonSerializer.Deserialize<IReadOnlyList<Guid>>(entity.TaxReversalJournalIdsJson, Json) ?? [], entity.NetAmount, entity.TaxAmount, entity.GrossAmount, entity.CurrencyCode, entity.SourceFingerprint, entity.EffectFingerprint, entity.DownstreamIdempotencyKey, entity.CompanyId, entity.CustomerId);

    private async Task<(bool Succeeded, string Code, Guid? Value)> CreateReversalJournalAsync(FinanceDbContext db, FinanceRequestContext context, FinanceJournalEntity original, DateOnly postingDate, string reason, CancellationToken cancellationToken)
    {
        var period = await db.FiscalPeriods.Where(item => item.CompanyId == original.CompanyId && item.StartDate <= postingDate && item.EndDate >= postingDate).SingleOrDefaultAsync(cancellationToken);
        if (period is null) return (false, "period_not_configured", null);
        if (period.State != FinanceFiscalPeriodState.Open) return (false, period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", null);
        var accountIds = original.Lines.Select(item => item.AccountId).Distinct().ToArray();
        var accounts = await db.Accounts.Where(item => item.CompanyId == original.CompanyId && accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (accounts.Count != accountIds.Length || accounts.Values.Any(item => !item.IsPostingAccount || item.Lifecycle != FinanceAccountLifecycle.Active || item.EffectiveFrom > postingDate || item.EffectiveTo < postingDate)) return (false, "account_not_postable", null);
        var journalId = Guid.NewGuid();
        var command = new FinanceJournalCommand(original.CompanyId, original.JournalDate, postingDate, original.TransactionCurrencyCode, original.ExchangeRate, original.ExchangeRateId, original.ExchangeRateVersionId, original.ExchangeRateVersionNumber, "finance-reversal.v1", "credit-note-reversal", original.Id, 1, original.PostingRuleId, reason, original.Lines.OrderBy(item => item.LineNumber).Select(line => new FinanceJournalLineCommand(line.AccountId, line.Credit, line.Debit, line.TransactionAmount, line.TransactionCurrencyCode, line.CostCenterId, reason)).ToArray(), journalId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var reversal = new FinanceJournalEntity(context.TenantId, journalId, command, await NextJournalSequenceAsync(db, original.CompanyId, cancellationToken), original.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow);
        reversal.SetCorrelation(context.CorrelationId); reversal.LinkOriginal(original.Id); reversal.SetPeriod(period.FiscalYearId, period.Id); if (original.PostingRuleId is { } ruleId && original.PostingRuleVersionNumber is { } ruleVersion) reversal.SetRule(ruleId, ruleVersion); reversal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow);
        foreach (var line in original.Lines.OrderBy(item => item.LineNumber)) reversal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), reversal.Id, line.LineNumber, accounts[line.AccountId], command.Lines[line.LineNumber - 1], null, line.FunctionalCredit, line.FunctionalDebit, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        var evidence = await db.JournalMonetaryEvidence.AsNoTracking().SingleOrDefaultAsync(item => item.JournalId == original.Id, cancellationToken);
        db.Journals.Add(reversal);
        if (evidence is not null)
        {
            var parsed = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(evidence.MonetaryEvidenceJson);
            if (parsed is null) return (false, "reporting_evidence_invalid", null);
            db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), reversal.Id, original.CompanyId, null, FinanceJournalMonetaryEvidenceFactory.Negate(parsed), DateTimeOffset.UtcNow));
        }
        original.LinkReversal(reversal.Id);
        original.SetStatus(FinanceJournalStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow);
        return (true, "succeeded", reversal.Id);
    }

    private static async Task<FinancePostingRuleEntity?> FindRuleAsync(FinanceDbContext db, Guid companyId, string sourceContract, string sourceEvent, DateOnly date, CancellationToken cancellationToken) =>
        await db.PostingRules.Where(item => item.CompanyId == companyId && item.SourceContract == sourceContract && item.SourceEvent == sourceEvent && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= date && (item.EffectiveTo == null || item.EffectiveTo >= date)).SingleOrDefaultAsync(cancellationToken);

    private static async Task<long> NextJournalSequenceAsync(FinanceDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var sequence = await db.Journals.Where(item => item.CompanyId == companyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L;
        while (db.ChangeTracker.Entries<FinanceJournalEntity>().Any(entry => entry.State != EntityState.Deleted && entry.Entity.CompanyId == companyId && entry.Entity.JournalSequence == sequence + 1L)) sequence++;
        return sequence + 1L;
    }

    private static FinanceCreditNoteResponse ToResponse(FinanceCreditNoteEntity item, FinanceDbContext db)
    {
        FinanceCustomerCreditStatus? creditStatus = null;
        if (item.CustomerCreditId is { } id) creditStatus = db.CustomerCredits.Local.SingleOrDefault(value => value.Id == id)?.Status ?? db.CustomerCredits.AsNoTracking().SingleOrDefault(value => value.Id == id)?.Status;
        return new(item.Id, item.TenantId.Value, item.SalesCustomerReturnId, item.DeliveryId, item.InvoiceId, item.FinanceOpenItemId, item.CompanyId, item.CustomerId, item.Status, item.CurrencyCode, item.FunctionalCurrencyCode, item.NetAmount, item.TaxAmount, item.GrossAmount, item.FunctionalAmount, creditStatus, item.CustomerCreditId, item.SourceEvidence, item.HandoffState, item.CreditNoteDate, item.PostedAt, item.Lines.Select(line => new FinanceCreditNoteLineResponse(line.Id, line.OrderLineId, line.Quantity, line.NetAmount, line.TaxAmount, line.GrossAmount, line.CurrencyCode, line.TaxId, line.TaxRateVersionId)).ToArray(), item.Version, item.PostingJournalId, item.ReversalJournalId, item.FinanceCommitState, item.SalesAcknowledgementState, item.ReconciliationState, item.FinanceEffectId, item.EffectFingerprint, item.RequestFingerprint, item.SourceFingerprint, item.DownstreamIdempotencyKey, item.AttemptCount, item.LastError, item.LastAttemptAt, item.AcknowledgedAt, item.ReversalFinanceCommitState, item.ReversalSalesAcknowledgementState, item.ReversalReconciliationState, item.ReversalFinanceEffectId, item.ReversalEffectFingerprint, item.ReversalRequestFingerprint, item.ReversalDownstreamIdempotencyKey, item.ReversalAttemptCount, item.ReversalLastError, item.ReversalLastAttemptAt, item.ReversalAcknowledgedAt);
    }
    private static bool InScope(FinanceRequestContext context, Guid companyId) => context.TenantContext.Scope is not { } scope || scope.Value.StartsWith("Tenant:", StringComparison.OrdinalIgnoreCase) || (scope.Value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(scope.Value[8..], out var id) && id == companyId);
    private static FinanceOperationResult<FinanceCreditNoteResponse> Failure(string code) => FinanceOperationResult<FinanceCreditNoteResponse>.Failure(code);
    private static void AddAudit(FinanceDbContext db, FinanceRequestContext context, string operation, Guid id, string result, string? reason, string? key) => db.AuditEvents.Add(new FinanceAuditEntity(context.TenantId, Guid.NewGuid(), operation, "credit-note", id, context.ActorId, context.SessionId, result, reason, context.CorrelationId, key, DateTimeOffset.UtcNow));
    private static async Task<FinanceOperationResult<FinanceCreditNoteResponse>?> ReplayAsync(FinanceDbContext db, FinanceRequestContext context, string operation, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var row = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.OperationId == operation && item.Key == key, cancellationToken);
        if (row is null) return null;
        return row.Fingerprint == fingerprint ? FinanceOperationResult<FinanceCreditNoteResponse>.Success(JsonSerializer.Deserialize<FinanceCreditNoteResponse>(row.SnapshotJson, Json)!) : FinanceOperationResult<FinanceCreditNoteResponse>.Failure("idempotency_conflict");
    }
    private static Task<FinanceIdempotencyEntity?> ReadIdempotencyAsync(FinanceDbContext db, FinanceRequestContext context, string operation, string? key, CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(key) ? Task.FromResult<FinanceIdempotencyEntity?>(null) : db.Idempotency.SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.OperationId == operation && item.Key == key, cancellationToken);
    private static async Task SaveOrUpdateReplayAsync(FinanceDbContext db, FinanceRequestContext context, string operation, string? key, string fingerprint, string type, Guid id, FinanceCreditNoteResponse response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var snapshot = JsonSerializer.Serialize(response, Json);
        var row = await ReadIdempotencyAsync(db, context, operation, key, cancellationToken);
        if (row is null) db.Idempotency.Add(new FinanceIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, key!, fingerprint, type, id, snapshot, DateTimeOffset.UtcNow));
        else row.SetSnapshot(snapshot);
    }
    private static async Task SaveReplayAsync(FinanceDbContext db, FinanceRequestContext context, string operation, string? key, string fingerprint, string type, Guid id, FinanceCreditNoteResponse response, CancellationToken cancellationToken)
    { if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new FinanceIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, key!, fingerprint, type, id, JsonSerializer.Serialize(response, Json), DateTimeOffset.UtcNow)); await Task.CompletedTask; }
}

#pragma warning restore CS1591
