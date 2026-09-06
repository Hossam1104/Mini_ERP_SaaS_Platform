#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinanceSettlementPersistence(
    DbContextOptions options,
    IFinanceCompanyProvider companies,
    IMasterDataExchangeRatePersistence exchangeRates,
    IBusinessCustomerReferenceReader customers,
    ISupplierPersistence suppliers,
    IMasterDataCurrencyPaymentTermPersistence paymentTerms,
    IFinanceSupplierInvoiceSourceProvider supplierInvoiceSources,
    IFinanceSourceApprovalPolicy? approvalPolicy = null) : IFinanceSettlementPersistence
{
    private const string ManualArContract = "manual-ar.v1";
    private const string ApContract = "procurement-supplier-invoice.v1";
    private const string SalesInvoiceContract = "sales-invoice.v1";
    private IFinanceSourceApprovalPolicy SourceApprovalPolicy => approvalPolicy ?? UnconfiguredFinanceSourceApprovalPolicy.Instance;

    private FinanceDbContext CreateContext(FinanceRequestContext context) => new(options, context.TenantContext);

    public async Task<IReadOnlyList<FinancePaymentMethodRecord>> ListPaymentMethodsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = CreateContext(context);
        return (await db.PaymentMethods.AsNoTracking().Where(item => item.CompanyId == companyId).OrderBy(item => item.Code).ToListAsync(cancellationToken)).Select(ToMethod).ToArray();
    }

    public async Task<FinanceOperationResult<FinancePaymentMethodRecord>> CreatePaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default)
    {
        if (!ValidText(command.Code, 64) || !ValidText(command.EnglishName, 256) || command.EffectiveTo < command.EffectiveFrom || command.Id == Guid.Empty) return Failure<FinancePaymentMethodRecord>("invalid_payment_method");
        if (!command.IsManual) return Failure<FinancePaymentMethodRecord>("payment_method_not_supported");
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinancePaymentMethodRecord>(db, context, "finance.payment-method.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        if (Company(context, command.CompanyId) is null) return Failure<FinancePaymentMethodRecord>("company_scope_denied");
        var code = NormalizeCode(command.Code)!; if (await db.PaymentMethods.AnyAsync(item => item.CompanyId == command.CompanyId && item.Code == code, cancellationToken)) return Failure<FinancePaymentMethodRecord>("payment_method_duplicate");
        var entity = new FinancePaymentMethodEntity(context.TenantId, command with { Code = code }); db.PaymentMethods.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.payment-method.create", "payment-method", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinancePaymentMethodRecord>.Success(ToMethod(entity)); AddReplay(db, context, "finance.payment-method.create", command.IdempotencyKey, command.RequestFingerprint, "payment-method", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinancePaymentMethodRecord>> EditPaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ExpectedVersion is null || !ValidText(command.Code, 64) || !ValidText(command.EnglishName, 256) || command.EffectiveTo < command.EffectiveFrom) return Failure<FinancePaymentMethodRecord>("invalid_payment_method");
        if (!command.IsManual) return Failure<FinancePaymentMethodRecord>("payment_method_not_supported");
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinancePaymentMethodRecord>(db, context, "finance.payment-method.edit", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.PaymentMethods.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || Company(context, entity.CompanyId) is null || entity.CompanyId != command.CompanyId) return Failure<FinancePaymentMethodRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinancePaymentMethodRecord>("concurrency_conflict");
        var code = NormalizeCode(command.Code)!; if (await db.PaymentMethods.AnyAsync(item => item.CompanyId == command.CompanyId && item.Code == code && item.Id != command.Id, cancellationToken)) return Failure<FinancePaymentMethodRecord>("payment_method_duplicate"); entity.Edit(command with { Code = code }); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.payment-method.edit", "payment-method", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinancePaymentMethodRecord>.Success(ToMethod(entity)); AddReplay(db, context, "finance.payment-method.edit", command.IdempotencyKey, command.RequestFingerprint, "payment-method", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinancePaymentMethodRecord>> SetPaymentMethodLifecycleAsync(FinanceRequestContext context, Guid methodId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinancePaymentMethodRecord>(db, context, "finance.payment-method.lifecycle", idempotencyKey, fingerprint, cancellationToken); if (replay is not null) return replay;
        if (lifecycle == FinancePaymentMethodLifecycle.Inactive && await db.SettlementDocuments.AnyAsync(item => item.CompanyId == companyId && item.PaymentMethodId == methodId && item.Status == FinanceSettlementDocumentStatus.Posted, cancellationToken)) return Failure<FinancePaymentMethodRecord>("payment_method_in_use");
        var entity = await db.PaymentMethods.SingleOrDefaultAsync(item => item.Id == methodId, cancellationToken); if (entity is null || Company(context, entity.CompanyId) is null || entity.CompanyId != companyId) return Failure<FinancePaymentMethodRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<FinancePaymentMethodRecord>("concurrency_conflict"); entity.SetLifecycle(lifecycle); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.payment-method.lifecycle", "payment-method", entity.Id, "Succeeded", lifecycle.ToString(), idempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinancePaymentMethodRecord>.Success(ToMethod(entity)); AddReplay(db, context, "finance.payment-method.lifecycle", idempotencyKey, fingerprint, "payment-method", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceCashAccountRecord>> ListCashAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = CreateContext(context); return (await db.CashAccounts.AsNoTracking().Where(item => item.CompanyId == companyId).OrderBy(item => item.Code).ToListAsync(cancellationToken)).Select(ToCash).ToArray();
    }

    public async Task<FinanceOperationResult<FinanceCashAccountRecord>> CreateCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, CancellationToken cancellationToken = default)
    {
        var company = Company(context, command.CompanyId); var currency = NormalizeCode(command.CurrencyCode); if (company is null) return Failure<FinanceCashAccountRecord>("company_scope_denied"); if (!ValidText(command.Code, 64) || !ValidText(command.EnglishName, 256) || currency is null || command.EffectiveTo < command.EffectiveFrom) return Failure<FinanceCashAccountRecord>("invalid_cash_account");
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceCashAccountRecord>(db, context, "finance.cash-account.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == command.LinkedAccountId && item.CompanyId == command.CompanyId, cancellationToken); if (account is null || account.Lifecycle != FinanceAccountLifecycle.Active || !account.IsPostingAccount || account.EffectiveFrom > command.EffectiveFrom || account.EffectiveTo < command.EffectiveFrom) return Failure<FinanceCashAccountRecord>("cash_account_link_invalid");
        var code = NormalizeCode(command.Code)!; if (await db.CashAccounts.AnyAsync(item => item.CompanyId == command.CompanyId && item.Code == code, cancellationToken)) return Failure<FinanceCashAccountRecord>("cash_account_duplicate"); var entity = new FinanceCashAccountEntity(context.TenantId, command with { Code = code }, currency); entity.SetLinkedAccountCode(account.Code); db.CashAccounts.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.cash-account.create", "cash-account", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceCashAccountRecord>.Success(ToCash(entity)); AddReplay(db, context, "finance.cash-account.create", command.IdempotencyKey, command.RequestFingerprint, "cash-account", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceCashAccountRecord>> EditCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default)
    {
        var company = Company(context, command.CompanyId); var currency = NormalizeCode(command.CurrencyCode); if (company is null || currency is null || !ValidText(command.Code, 64) || !ValidText(command.EnglishName, 256) || command.EffectiveTo < command.EffectiveFrom) return Failure<FinanceCashAccountRecord>("invalid_cash_account");
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceCashAccountRecord>(db, context, "finance.cash-account.edit", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var entity = await db.CashAccounts.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || entity.CompanyId != command.CompanyId || Company(context, entity.CompanyId) is null) return Failure<FinanceCashAccountRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<FinanceCashAccountRecord>("concurrency_conflict"); if (await db.SettlementDocuments.AnyAsync(item => item.CashAccountId == entity.Id && item.Status == FinanceSettlementDocumentStatus.Posted, cancellationToken)) return Failure<FinanceCashAccountRecord>("cash_account_history_locked"); var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == command.LinkedAccountId && item.CompanyId == command.CompanyId, cancellationToken); if (account is null || account.Lifecycle != FinanceAccountLifecycle.Active || !account.IsPostingAccount) return Failure<FinanceCashAccountRecord>("cash_account_link_invalid"); var code = NormalizeCode(command.Code)!; if (await db.CashAccounts.AnyAsync(item => item.CompanyId == command.CompanyId && item.Code == code && item.Id != command.Id, cancellationToken)) return Failure<FinanceCashAccountRecord>("cash_account_duplicate"); entity.Edit(command with { Code = code }, currency, account.Code); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.cash-account.edit", "cash-account", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceCashAccountRecord>.Success(ToCash(entity)); AddReplay(db, context, "finance.cash-account.edit", command.IdempotencyKey, command.RequestFingerprint, "cash-account", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceCashAccountRecord>> SetCashAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceCashAccountRecord>(db, context, "finance.cash-account.lifecycle", idempotencyKey, fingerprint, cancellationToken); if (replay is not null) return replay; var entity = await db.CashAccounts.SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken); if (entity is null || entity.CompanyId != companyId || Company(context, entity.CompanyId) is null) return Failure<FinanceCashAccountRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<FinanceCashAccountRecord>("concurrency_conflict"); entity.SetLifecycle(lifecycle); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.cash-account.lifecycle", "cash-account", entity.Id, "Succeeded", lifecycle.ToString(), idempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceCashAccountRecord>.Success(ToCash(entity)); AddReplay(db, context, "finance.cash-account.lifecycle", idempotencyKey, fingerprint, "cash-account", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, string resource, Guid resourceId, CancellationToken cancellationToken = default)
    {
        if (resourceId == Guid.Empty) return null;
        if (resource == "source-evidence") return (await supplierInvoiceSources.FindAsync(context, resourceId, cancellationToken))?.CompanyId;
        await using var db = CreateContext(context); return resource switch
        {
            "payment-method" => await db.PaymentMethods.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            "cash-account" => await db.CashAccounts.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            "open-item" => await db.OpenItems.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            "settlement" => await db.SettlementDocuments.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            "allocation" => await db.Allocations.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    public async Task<IReadOnlyList<FinanceOpenItemRecord>> ListOpenItemsAsync(FinanceRequestContext context, FinanceOpenItemKind kind, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return []; await using var db = CreateContext(context); var items = await db.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.Kind == kind).OrderBy(item => item.DueDate).Take(1000).ToListAsync(cancellationToken); return await ToOpenItemsAsync(db, items, cancellationToken);
    }

    public async Task<FinanceOpenItemRecord?> GetOpenItemAsync(FinanceRequestContext context, Guid itemId, FinanceOpenItemKind? expectedKind = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var item = await db.OpenItems.AsNoTracking().SingleOrDefaultAsync(value => value.Id == itemId && (expectedKind == null || value.Kind == expectedKind), cancellationToken); return item is null || Company(context, item.CompanyId) is null ? null : await ToOpenItemAsync(db, item, cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceApSourceReadyRecord>> ListApSourceReadyAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default)
    {
        if (companyId is { } requestedCompany && Company(context, requestedCompany) is null) return [];
        var sources = await supplierInvoiceSources.ListAsync(context, companyId, cancellationToken);
        if (sources.Count == 0) return [];
        await using var db = CreateContext(context);
        var evidenceIds = sources.Select(item => item.SourceEvidenceId).Distinct().ToArray();
        var recognized = await db.OpenItems.AsNoTracking()
            .Where(item => item.Kind == FinanceOpenItemKind.Payable && evidenceIds.Contains(item.SourceEvidenceId))
            .Select(item => new { item.SourceEvidenceId, item.SourceEvidenceVersion })
            .ToListAsync(cancellationToken);
        var recognizedKeys = recognized.Select(item => (item.SourceEvidenceId, item.SourceEvidenceVersion)).ToHashSet();
        return sources
            .Where(source => Company(context, source.CompanyId) is not null && source.PaymentTerm is not null && source.DueDate is not null)
            .Select(source => new FinanceApSourceReadyRecord(
                source.SourceEvidenceId,
                source.CompanyId,
                source.SupplierId,
                source.SupplierCode,
                source.SupplierName,
                source.Reference,
                source.DocumentDate,
                source.CurrencyCode,
                source.Amount,
                source.DueDate!.Value,
                source.PaymentTerm!,
                source.MatchResult,
                recognizedKeys.Contains((source.SourceEvidenceId, source.SourceEvidenceVersion)),
                source.SourceEvidenceVersion))
            .Where(item => !item.AlreadyRecognized)
            .ToArray();
    }

    public async Task<FinanceOperationResult<FinanceOpenItemRecord>> RecognizeSupplierInvoiceAsync(FinanceRequestContext context, FinanceSupplierInvoiceRecognitionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceOpenItemRecord>(db, context, "finance.ap.recognize", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var source = await supplierInvoiceSources.FindAsync(context, command.SourceEvidenceId, cancellationToken); if (source is null || source.TenantId != context.TenantId.Value) return Failure<FinanceOpenItemRecord>("source_not_ready"); if (Company(context, source.CompanyId) is null) return Failure<FinanceOpenItemRecord>("company_scope_denied"); if (source.SourceContract != ApContract) return Failure<FinanceOpenItemRecord>("source_contract_invalid"); if (source.PaymentTerm is null || source.DueDate is null) return Failure<FinanceOpenItemRecord>("payment_term_not_configured"); if (await db.OpenItems.AnyAsync(item => item.CompanyId == source.CompanyId && item.SourceContract == source.SourceContract && item.SourceEvidenceId == source.SourceEvidenceId && item.SourceEvidenceVersion == source.SourceEvidenceVersion, cancellationToken)) return Failure<FinanceOpenItemRecord>("source_effect_exists");
        if (!await SupplierIsActiveAsync(context, source.SupplierId, cancellationToken)) return Failure<FinanceOpenItemRecord>("party_scope_denied"); var rule = await FindRuleAsync(db, source.CompanyId, source.SourceContract, "recognition", source.DocumentDate, cancellationToken); if (rule.Code != "eligible") return Failure<FinanceOpenItemRecord>(rule.Code); if (source.Amount <= 0m || source.FunctionalAmount <= 0m) return Failure<FinanceOpenItemRecord>("invalid_amount");
        var item = new FinanceOpenItemEntity(context.TenantId, Guid.NewGuid(), FinanceOpenItemKind.Payable, source.CompanyId, source.SupplierId, null, source.SourceContract, source.SourceDocumentId, source.SourceDocumentVersion, source.SourceEvidenceId, source.SourceEvidenceVersion, source.Reference, source.DocumentDate, source.DueDate.Value, NormalizeCode(source.CurrencyCode)!, source.Amount, NormalizeCode(source.FunctionalCurrencyCode)!, source.FunctionalAmount, source.ExchangeRate, source.ExchangeRateId, source.ExchangeRateVersionId, source.ExchangeRateVersionNumber, source.PaymentTerm, source.MatchEvidenceId, source.MatchEvidenceVersion, source.SourceSnapshot);
        db.OpenItems.Add(item); var journal = await CreatePostedJournalAsync(db, context, source.CompanyId, source.DocumentDate, source.CurrencyCode, source.Amount, source.FunctionalAmount, source.ExchangeRate, source.ExchangeRateId, source.ExchangeRateVersionId, source.ExchangeRateVersionNumber, source.SourceContract, "recognition", source.SourceEvidenceId, source.SourceEvidenceVersion, rule.Value!, false, $"AP recognition {source.Reference ?? source.SourceDocumentId.ToString()}", cancellationToken); if (!journal.Succeeded || journal.Value is null) return Failure<FinanceOpenItemRecord>(journal.Code); item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journal.Value.Id); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.ap.recognize", "ap-open-item", item.Id, "Succeeded", source.Reference, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceOpenItemRecord>.Success(await ToOpenItemAsync(db, item, cancellationToken)); AddReplay(db, context, "finance.ap.recognize", command.IdempotencyKey, command.RequestFingerprint, "ap-open-item", item.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateManualReceivableAsync(FinanceRequestContext context, FinanceManualReceivableCommand command, CancellationToken cancellationToken = default)
    {
        var company = Company(context, command.CompanyId); if (company is null) return Failure<FinanceOpenItemRecord>("company_scope_denied"); var currency = NormalizeCode(command.CurrencyCode); if (currency is null || command.Amount <= 0m) return Failure<FinanceOpenItemRecord>("invalid_amount"); if (!await CustomerIsActiveAsync(context, command.CustomerId, cancellationToken)) return Failure<FinanceOpenItemRecord>("party_scope_denied");
        var term = await ResolvePaymentTermAsync(context, command.PaymentTermId, command.DueDate, command.DocumentDate, cancellationToken); if (!term.Succeeded || term.Value is null) return Failure<FinanceOpenItemRecord>(term.Code); var (dueDate, paymentTerm) = term.Value.Value; var rate = await ValidateCurrencyAsync(context, company, currency, command.DocumentDate, command.ExchangeRate, command.ExchangeRateId, command.ExchangeRateVersionId, command.ExchangeRateVersionNumber, cancellationToken); if (!rate.Succeeded) return Failure<FinanceOpenItemRecord>(rate.Code); var functionalAmount = currency == company.FunctionalCurrencyCode ? command.Amount : command.Amount * rate.Value;
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceOpenItemRecord>(db, context, "finance.ar.manual-create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; if (await db.OpenItems.AnyAsync(item => item.CompanyId == command.CompanyId && item.SourceContract == ManualArContract && item.SourceEvidenceId == command.Id && item.SourceEvidenceVersion == 1, cancellationToken)) return Failure<FinanceOpenItemRecord>("source_effect_exists"); var rule = await FindRuleAsync(db, command.CompanyId, ManualArContract, "recognition", command.DocumentDate, cancellationToken); if (rule.Code != "eligible") return Failure<FinanceOpenItemRecord>(rule.Code);
        var item = new FinanceOpenItemEntity(context.TenantId, command.Id, FinanceOpenItemKind.Receivable, command.CompanyId, null, command.CustomerId, ManualArContract, command.Id, 1, command.Id, 1, command.Reference, command.DocumentDate, dueDate, currency, command.Amount, company.FunctionalCurrencyCode, functionalAmount, currency == company.FunctionalCurrencyCode ? 1m : rate.Value, currency == company.FunctionalCurrencyCode ? null : command.ExchangeRateId, currency == company.FunctionalCurrencyCode ? null : command.ExchangeRateVersionId, currency == company.FunctionalCurrencyCode ? null : command.ExchangeRateVersionNumber, paymentTerm, null, null, JsonSerializer.Serialize(new { source = ManualArContract, command.CustomerId, command.Reference, command.DocumentDate })); db.OpenItems.Add(item); var journal = await CreatePostedJournalAsync(db, context, command.CompanyId, command.DocumentDate, currency, command.Amount, functionalAmount, currency == company.FunctionalCurrencyCode ? 1m : rate.Value, currency == company.FunctionalCurrencyCode ? null : command.ExchangeRateId, currency == company.FunctionalCurrencyCode ? null : command.ExchangeRateVersionId, currency == company.FunctionalCurrencyCode ? null : command.ExchangeRateVersionNumber, ManualArContract, "recognition", command.Id, 1, rule.Value!, false, command.Description ?? "Manual receivable recognition", cancellationToken); if (!journal.Succeeded || journal.Value is null) return Failure<FinanceOpenItemRecord>(journal.Code); item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journal.Value.Id); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.ar.manual-create", "ar-open-item", item.Id, "Succeeded", command.Reference, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceOpenItemRecord>.Success(await ToOpenItemAsync(db, item, cancellationToken)); AddReplay(db, context, "finance.ar.manual-create", command.IdempotencyKey, command.RequestFingerprint, "ar-open-item", item.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>> EvaluateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var validation = await ValidateSalesInvoiceAsync(context, db, command, cancellationToken);
        return validation.Eligibility is null ? Failure<FinanceSalesInvoiceEligibilityRecord>(validation.Code) : FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>.Success(validation.Eligibility);
    }

    public async Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceOpenItemRecord>(db, context, "finance.ar.sales-invoice.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var validation = await ValidateSalesInvoiceAsync(context, db, command, cancellationToken);
        if (validation.Eligibility is null || validation.Summary is null) return Failure<FinanceOpenItemRecord>(validation.Code);
        var company = Company(context, command.CompanyId)!;
        var effective = validation.Eligibility;
        var summary = validation.Summary;
        var rule = await FindRuleAsync(db, command.CompanyId, SalesInvoiceContract, "recognition", command.DocumentDate, cancellationToken);
        if (rule.Code != "eligible" || rule.Value is null) return Failure<FinanceOpenItemRecord>(rule.Code);
        var currency = NormalizeCode(command.CurrencyCode)!;
        var functionalAmount = decimal.Round(command.Amount * (effective.ExchangeRate ?? 1m), 8, MidpointRounding.ToEven);
        var item = new FinanceOpenItemEntity(context.TenantId, Guid.NewGuid(), FinanceOpenItemKind.Receivable, command.CompanyId, null, command.CustomerId, SalesInvoiceContract, command.SalesOrderId, command.SalesOrderRevision, command.InvoiceRequestId, 1, command.Reference, command.DocumentDate, effective.PaymentTerm!.DueDate!.Value, currency, command.Amount, company.FunctionalCurrencyCode, functionalAmount, effective.ExchangeRate, effective.ExchangeRateId, effective.ExchangeRateVersionId, effective.ExchangeRateVersionNumber, effective.PaymentTerm, null, null, command.SourceSnapshot);
        db.OpenItems.Add(item);
        var journal = await CreatePostedJournalAsync(db, context, command.CompanyId, command.DocumentDate, currency, command.Amount, functionalAmount, effective.ExchangeRate, effective.ExchangeRateId, effective.ExchangeRateVersionId, effective.ExchangeRateVersionNumber, SalesInvoiceContract, "recognition", command.InvoiceRequestId, 1, rule.Value, false, command.Reference ?? $"Sales invoice {command.SalesOrderId:D}", cancellationToken);
        if (!journal.Succeeded || journal.Value is null) return Failure<FinanceOpenItemRecord>(journal.Code);
        var taxPosting = await PostSalesInvoiceTaxAsync(db, context, command, item, summary, currency, effective.ExchangeRate, effective.ExchangeRateId, effective.ExchangeRateVersionId, effective.ExchangeRateVersionNumber, rule.Value, cancellationToken);
        if (!taxPosting.Succeeded) return Failure<FinanceOpenItemRecord>(taxPosting.Code);
        item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journal.Value.Id);
        var now = DateTimeOffset.UtcNow;
        AddAudit(db, context, "finance.ar.sales-invoice.create", "ar-open-item", item.Id, "Succeeded", command.Reference, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceOpenItemRecord>.Success(await ToOpenItemAsync(db, item, cancellationToken));
        AddReplay(db, context, "finance.ar.sales-invoice.create", command.IdempotencyKey, command.RequestFingerprint, "ar-open-item", item.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<FinanceSettlementDocumentRecord>> ListSettlementDocumentsAsync(FinanceRequestContext context, FinanceSettlementQuery query, CancellationToken cancellationToken = default)
    {
        if (Company(context, query.CompanyId) is null) return []; await using var db = CreateContext(context); var documents = db.SettlementDocuments.AsNoTracking().Where(item => item.CompanyId == query.CompanyId); if (query.Direction is { } direction) documents = documents.Where(item => item.Direction == direction); var values = await documents.OrderByDescending(item => item.DocumentDate).Take(1000).ToListAsync(cancellationToken); return await ToDocumentsAsync(db, values, cancellationToken);
    }

    public async Task<FinanceSettlementDocumentRecord?> GetSettlementDocumentAsync(FinanceRequestContext context, Guid documentId, FinancePaymentMethodDirection? expectedDirection = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var document = await db.SettlementDocuments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == documentId && (expectedDirection == null || item.Direction == expectedDirection), cancellationToken); return document is null || Company(context, document.CompanyId) is null ? null : await ToDocumentAsync(db, document, cancellationToken);
    }

    public async Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> CreateSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var company = Company(context, command.CompanyId); if (company is null) return Failure<FinanceSettlementDocumentRecord>("company_scope_denied"); var currency = NormalizeCode(command.CurrencyCode); if (currency is null || command.Amount <= 0m || command.Direction is not (FinancePaymentMethodDirection.Payment or FinancePaymentMethodDirection.Receipt)) return Failure<FinanceSettlementDocumentRecord>("invalid_settlement_document"); if (command.Direction == FinancePaymentMethodDirection.Payment ? command.SupplierId is null : command.CustomerId is null) return Failure<FinanceSettlementDocumentRecord>("party_scope_denied"); if (command.Direction == FinancePaymentMethodDirection.Payment && !await SupplierIsActiveAsync(context, command.SupplierId!.Value, cancellationToken) || command.Direction == FinancePaymentMethodDirection.Receipt && !await CustomerIsActiveAsync(context, command.CustomerId!.Value, cancellationToken)) return Failure<FinanceSettlementDocumentRecord>("party_scope_denied");
        var rate = await ValidateCurrencyAsync(context, company, currency, command.DocumentDate, command.ExchangeRate, command.ExchangeRateId, command.ExchangeRateVersionId, command.ExchangeRateVersionNumber, cancellationToken); if (!rate.Succeeded) return Failure<FinanceSettlementDocumentRecord>(rate.Code); var functionalAmount = currency == company.FunctionalCurrencyCode ? command.Amount : command.Amount * rate.Value;
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceSettlementDocumentRecord>(db, context, "finance.settlement.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var method = await db.PaymentMethods.SingleOrDefaultAsync(item => item.Id == command.PaymentMethodId && item.CompanyId == command.CompanyId, cancellationToken); if (method is null || !method.IsManual || method.Lifecycle != FinancePaymentMethodLifecycle.Active || method.EffectiveFrom > command.DocumentDate || method.EffectiveTo < command.DocumentDate || !MethodApplies(method.Direction, command.Direction)) return Failure<FinanceSettlementDocumentRecord>(method is { IsManual: false } ? "payment_method_not_supported" : "payment_method_not_configured"); var cash = await db.CashAccounts.SingleOrDefaultAsync(item => item.Id == command.CashAccountId && item.CompanyId == command.CompanyId, cancellationToken); if (cash is null || cash.Lifecycle != FinancePaymentMethodLifecycle.Active || cash.EffectiveFrom > command.DocumentDate || cash.EffectiveTo < command.DocumentDate || cash.CurrencyCode != currency) return Failure<FinanceSettlementDocumentRecord>("cash_account_not_configured"); if (method.RequiresReference && string.IsNullOrWhiteSpace(command.ExternalReference)) return Failure<FinanceSettlementDocumentRecord>("reference_required");
        var entity = new FinanceSettlementDocumentEntity(context.TenantId, command with { CurrencyCode = currency }, currency, company.FunctionalCurrencyCode, functionalAmount, context.ActorId, DateTimeOffset.UtcNow); db.SettlementDocuments.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.settlement.create", "settlement-document", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceSettlementDocumentRecord>.Success(await ToDocumentAsync(db, entity, cancellationToken)); AddReplay(db, context, "finance.settlement.create", command.IdempotencyKey, command.RequestFingerprint, "settlement-document", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> EditSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default)
    {
        var company = Company(context, command.CompanyId); var currency = NormalizeCode(command.CurrencyCode); if (company is null || currency is null || command.Amount <= 0m) return Failure<FinanceSettlementDocumentRecord>("invalid_settlement_document"); var rate = await ValidateCurrencyAsync(context, company, currency, command.DocumentDate, command.ExchangeRate, command.ExchangeRateId, command.ExchangeRateVersionId, command.ExchangeRateVersionNumber, cancellationToken); if (!rate.Succeeded) return Failure<FinanceSettlementDocumentRecord>(rate.Code); var functionalAmount = currency == company.FunctionalCurrencyCode ? command.Amount : command.Amount * rate.Value;
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceSettlementDocumentRecord>(db, context, "finance.settlement.edit", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var entity = await db.SettlementDocuments.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || entity.CompanyId != command.CompanyId || Company(context, entity.CompanyId) is null) return Failure<FinanceSettlementDocumentRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<FinanceSettlementDocumentRecord>("concurrency_conflict"); if (entity.Status is not (FinanceSettlementDocumentStatus.Draft or FinanceSettlementDocumentStatus.Rejected)) return Failure<FinanceSettlementDocumentRecord>("document_immutable"); if (entity.Direction != command.Direction) return Failure<FinanceSettlementDocumentRecord>("direction_immutable"); var method = await db.PaymentMethods.SingleOrDefaultAsync(item => item.Id == command.PaymentMethodId && item.CompanyId == command.CompanyId, cancellationToken); var cash = await db.CashAccounts.SingleOrDefaultAsync(item => item.Id == command.CashAccountId && item.CompanyId == command.CompanyId, cancellationToken); if (method is null || !method.IsManual || method.Lifecycle != FinancePaymentMethodLifecycle.Active || method.EffectiveFrom > command.DocumentDate || method.EffectiveTo < command.DocumentDate || cash is null || cash.Lifecycle != FinancePaymentMethodLifecycle.Active || cash.EffectiveFrom > command.DocumentDate || cash.EffectiveTo < command.DocumentDate || cash.CurrencyCode != currency || !MethodApplies(method.Direction, command.Direction)) return Failure<FinanceSettlementDocumentRecord>(method is { IsManual: false } ? "payment_method_not_supported" : "payment_method_not_configured"); var wasRejected = entity.Status == FinanceSettlementDocumentStatus.Rejected; entity.Edit(command with { CurrencyCode = currency }, currency, functionalAmount); if (wasRejected) entity.ReturnToDraft(context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.settlement.edit", "settlement-document", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceSettlementDocumentRecord>.Success(await ToDocumentAsync(db, entity, cancellationToken)); AddReplay(db, context, "finance.settlement.edit", command.IdempotencyKey, command.RequestFingerprint, "settlement-document", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> TransitionSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, FinanceSettlementDocumentStatus target, CancellationToken cancellationToken = default)
    {
        var operation = "finance.settlement." + target.ToString().ToLowerInvariant(); await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceSettlementDocumentRecord>(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var entity = await db.SettlementDocuments.SingleOrDefaultAsync(item => item.Id == command.DocumentId, cancellationToken); if (entity is null || Company(context, entity.CompanyId) is null) return Failure<FinanceSettlementDocumentRecord>("company_scope_denied"); if (command.ExpectedDirection is { } expectedDirection && entity.Direction != expectedDirection) return Failure<FinanceSettlementDocumentRecord>("settlement_direction_mismatch"); if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceSettlementDocumentRecord>("concurrency_conflict"); if (!AllowedDocumentTransition(entity.Status, target) || target is FinanceSettlementDocumentStatus.Posted or FinanceSettlementDocumentStatus.Reversed) return Failure<FinanceSettlementDocumentRecord>("invalid_settlement_transition"); if (target is FinanceSettlementDocumentStatus.Rejected or FinanceSettlementDocumentStatus.Cancelled && string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceSettlementDocumentRecord>("reason_required"); var approval = SourceApprovalPolicy.Resolve(ContractFor(entity.Direction), "on-account"); if (target == FinanceSettlementDocumentStatus.Approved) { if (approval == FinanceApprovalRequirement.NotConfigured) return Failure<FinanceSettlementDocumentRecord>("approval_policy_not_configured"); if (approval == FinanceApprovalRequirement.NotRequired) return Failure<FinanceSettlementDocumentRecord>("approval_not_required"); if (entity.CreatedBy == context.ActorId || entity.SubmittedBy == context.ActorId) return Failure<FinanceSettlementDocumentRecord>("self_approval_forbidden"); } entity.SetStatus(target, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, operation, "settlement-document", entity.Id, "Succeeded", command.Reason, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceSettlementDocumentRecord>.Success(await ToDocumentAsync(db, entity, cancellationToken)); AddReplay(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, "settlement-document", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> PostSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceSettlementDocumentRecord>(db, context, "finance.settlement.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var entity = await db.SettlementDocuments.SingleOrDefaultAsync(item => item.Id == command.DocumentId, cancellationToken); if (entity is null || Company(context, entity.CompanyId) is null) return Failure<FinanceSettlementDocumentRecord>("company_scope_denied"); if (command.ExpectedDirection is { } expectedDirection && entity.Direction != expectedDirection) return Failure<FinanceSettlementDocumentRecord>("settlement_direction_mismatch"); if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceSettlementDocumentRecord>("concurrency_conflict"); var approval = SourceApprovalPolicy.Resolve(ContractFor(entity.Direction), "on-account"); if (approval == FinanceApprovalRequirement.NotConfigured) return Failure<FinanceSettlementDocumentRecord>("approval_policy_not_configured"); if (approval == FinanceApprovalRequirement.Required && entity.Status != FinanceSettlementDocumentStatus.Approved || approval == FinanceApprovalRequirement.NotRequired && entity.Status is not (FinanceSettlementDocumentStatus.Submitted or FinanceSettlementDocumentStatus.Approved)) return Failure<FinanceSettlementDocumentRecord>("approval_required"); var cash = await db.CashAccounts.SingleOrDefaultAsync(item => item.Id == entity.CashAccountId && item.CompanyId == entity.CompanyId, cancellationToken); var method = await db.PaymentMethods.SingleOrDefaultAsync(item => item.Id == entity.PaymentMethodId && item.CompanyId == entity.CompanyId, cancellationToken); var postingDate = entity.DocumentDate; if (cash is null || method is null || !method.IsManual || cash.CurrencyCode != entity.CurrencyCode || !MethodApplies(method.Direction, entity.Direction) || cash.Lifecycle != FinancePaymentMethodLifecycle.Active || cash.EffectiveFrom > postingDate || cash.EffectiveTo < postingDate || method.Lifecycle != FinancePaymentMethodLifecycle.Active || method.EffectiveFrom > postingDate || method.EffectiveTo < postingDate) return Failure<FinanceSettlementDocumentRecord>("settlement_configuration_invalid"); var linkedAccount = await db.Accounts.SingleOrDefaultAsync(item => item.Id == cash.LinkedAccountId && item.CompanyId == entity.CompanyId, cancellationToken); if (linkedAccount is null || linkedAccount.Lifecycle != FinanceAccountLifecycle.Active || !linkedAccount.IsPostingAccount || linkedAccount.EffectiveFrom > postingDate || linkedAccount.EffectiveTo < postingDate) return Failure<FinanceSettlementDocumentRecord>("cash_account_link_invalid"); var eventName = "on-account"; var rule = await FindRuleAsync(db, entity.CompanyId, ContractFor(entity.Direction), eventName, postingDate, cancellationToken); if (rule.Code != "eligible") return Failure<FinanceSettlementDocumentRecord>(rule.Code); var cashAccountMatches = entity.Direction == FinancePaymentMethodDirection.Payment ? rule.Value!.CreditAccountId == cash.LinkedAccountId : rule.Value!.DebitAccountId == cash.LinkedAccountId; if (!cashAccountMatches) return Failure<FinanceSettlementDocumentRecord>("posting_rule_cash_account_mismatch"); var journal = await CreatePostedJournalAsync(db, context, entity.CompanyId, entity.DocumentDate, entity.CurrencyCode, entity.Amount, entity.FunctionalAmount, entity.ExchangeRate, entity.ExchangeRateId, entity.ExchangeRateVersionId, entity.ExchangeRateVersionNumber, ContractFor(entity.Direction), eventName, entity.Id, 1, rule.Value!, false, entity.Description ?? "Settlement on account", cancellationToken); if (!journal.Succeeded || journal.Value is null) return Failure<FinanceSettlementDocumentRecord>(journal.Code); entity.SetPostedJournal(journal.Value.Id); entity.SetStatus(FinanceSettlementDocumentStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.settlement.post", "settlement-document", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceSettlementDocumentRecord>.Success(await ToDocumentAsync(db, entity, cancellationToken)); AddReplay(db, context, "finance.settlement.post", command.IdempotencyKey, command.RequestFingerprint, "settlement-document", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> ReverseSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementReversalCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceSettlementDocumentRecord>(db, context, "finance.settlement.reverse", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var entity = await db.SettlementDocuments.SingleOrDefaultAsync(item => item.Id == command.DocumentId, cancellationToken); if (entity is null || Company(context, entity.CompanyId) is null) return Failure<FinanceSettlementDocumentRecord>("company_scope_denied"); if (command.ExpectedDirection is { } expectedDirection && entity.Direction != expectedDirection) return Failure<FinanceSettlementDocumentRecord>("settlement_direction_mismatch"); if (entity.Status != FinanceSettlementDocumentStatus.Posted) return Failure<FinanceSettlementDocumentRecord>("document_not_posted"); if (entity.ReversalJournalId is not null) return Failure<FinanceSettlementDocumentRecord>("document_already_reversed"); if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceSettlementDocumentRecord>("reason_required"); if (await ActiveAllocations(db).AnyAsync(item => item.SettlementDocumentId == entity.Id, cancellationToken)) return Failure<FinanceSettlementDocumentRecord>("active_allocations_require_reversal"); var original = entity.PostedJournalId is null ? null : await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == entity.PostedJournalId, cancellationToken); if (original is null) return Failure<FinanceSettlementDocumentRecord>("posting_lineage_missing"); var reversal = await CreateReversalJournalAsync(db, context, original, command.PostingDate, command.Reason, command.Id, cancellationToken); if (!reversal.Succeeded || reversal.Value is null) return Failure<FinanceSettlementDocumentRecord>(reversal.Code); entity.SetReversal(reversal.Value.Id); entity.SetStatus(FinanceSettlementDocumentStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.settlement.reverse", "settlement-document", entity.Id, "Succeeded", command.Reason, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceSettlementDocumentRecord>.Success(await ToDocumentAsync(db, entity, cancellationToken)); AddReplay(db, context, "finance.settlement.reverse", command.IdempotencyKey, command.RequestFingerprint, "settlement-document", reversal.Value.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceAllocationRecord>> ListAllocationsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return []; await using var db = CreateContext(context); return (await db.Allocations.AsNoTracking().Where(item => item.CompanyId == companyId).OrderByDescending(item => item.AllocationDate).Take(1000).ToListAsync(cancellationToken)).Select(ToAllocation).ToArray();
    }

    public async Task<FinanceOperationResult<FinanceAllocationRecord>> CreateAllocationAsync(FinanceRequestContext context, FinanceAllocationCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0m) return Failure<FinanceAllocationRecord>("invalid_amount");
        await using var db = CreateContext(context);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceAllocationRecord>(db, context, "finance.allocation.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var document = await db.SettlementDocuments.SingleOrDefaultAsync(item => item.Id == command.SettlementDocumentId, cancellationToken);
        var item = await db.OpenItems.SingleOrDefaultAsync(value => value.Id == command.OpenItemId, cancellationToken);
        if (document is null || item is null || document.CompanyId != item.CompanyId || Company(context, document.CompanyId) is null) return Failure<FinanceAllocationRecord>("company_scope_denied");
        if (document.Status != FinanceSettlementDocumentStatus.Posted) return Failure<FinanceAllocationRecord>("settlement_not_posted");
        if (item.RecognitionState != FinanceOpenItemRecognitionState.Recognized) return Failure<FinanceAllocationRecord>("open_item_not_recognized");
        if (document.Direction == FinancePaymentMethodDirection.Payment && item.Kind != FinanceOpenItemKind.Payable || document.Direction == FinancePaymentMethodDirection.Receipt && item.Kind != FinanceOpenItemKind.Receivable) return Failure<FinanceAllocationRecord>("allocation_direction_invalid");
        if (document.SupplierId != item.SupplierId || document.CustomerId != item.CustomerId) return Failure<FinanceAllocationRecord>("party_scope_denied");
        if (document.CurrencyCode != item.CurrencyCode) return Failure<FinanceAllocationRecord>("allocation_currency_mismatch");
        var recognition = item.RecognitionJournalId is { } recognitionId
            ? await db.Journals.Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == recognitionId, cancellationToken)
            : null;
        var controlAccountId = ResolveControlAccountId(item.Kind, recognition);
        if (controlAccountId is null) return Failure<FinanceAllocationRecord>("posting_lineage_missing");
        var allocationRule = await FindRuleAsync(db, document.CompanyId, ContractFor(document.Direction), "allocation", command.AllocationDate, cancellationToken);
        if (allocationRule.Code != "eligible") return Failure<FinanceAllocationRecord>(allocationRule.Code);
        var allocationControlAccountId = item.Kind == FinanceOpenItemKind.Payable ? allocationRule.Value!.DebitAccountId : allocationRule.Value!.CreditAccountId;
        if (allocationControlAccountId != controlAccountId) return Failure<FinanceAllocationRecord>("posting_rule_control_account_mismatch");
        var allocatedItem = await ActiveAllocations(db).Where(value => value.OpenItemId == item.Id).SumAsync(value => (decimal?)value.Amount, cancellationToken) ?? 0m;
        var allocatedDocument = await ActiveAllocations(db).Where(value => value.SettlementDocumentId == document.Id).SumAsync(value => (decimal?)value.Amount, cancellationToken) ?? 0m;
        if (command.Amount > item.OriginalAmount - allocatedItem) return Failure<FinanceAllocationRecord>("allocation_exceeds_outstanding");
        if (command.Amount > document.Amount - allocatedDocument) return Failure<FinanceAllocationRecord>("allocation_exceeds_unallocated");
        var itemFunctional = decimal.Round(command.Amount * item.OriginalFunctionalAmount / item.OriginalAmount, 8);
        var documentFunctional = decimal.Round(command.Amount * document.FunctionalAmount / document.Amount, 8);
        var realizedFx = ResolveRealizedFx(item.Kind, itemFunctional, documentFunctional);
        var realizedDifference = realizedFx.Difference;
        FinancePostingRuleEntity? realizedRule = null;
        if (Math.Abs(realizedDifference) > 0.00000001m)
        {
            var fxRule = await FindRuleAsync(db, document.CompanyId, "finance-fx.v1", "realized", command.AllocationDate, cancellationToken);
            if (fxRule.Code == "ambiguous_mapping") return Failure<FinanceAllocationRecord>("realized_fx_mapping_ambiguous");
            if (fxRule.Code != "eligible") return Failure<FinanceAllocationRecord>("fx_posting_rule_not_configured");
            realizedRule = fxRule.Value;
        }
        var postedSettlement = document.PostedJournalId is { } postedJournalId
            ? await db.Journals.Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == postedJournalId, cancellationToken)
            : null;
        var cash = await db.CashAccounts.SingleOrDefaultAsync(value => value.Id == document.CashAccountId && value.CompanyId == document.CompanyId, cancellationToken);
        var settlementAccountId = cash?.LinkedAccountId;
        if (postedSettlement is null || settlementAccountId is null || !postedSettlement.Lines.Any(line => line.AccountId == settlementAccountId)) return Failure<FinanceAllocationRecord>("settlement_posting_lineage_missing");
        var allocation = new FinanceAllocationEntity(context.TenantId, command, document.CompanyId, document.CurrencyCode, decimal.Round(itemFunctional, 8), context.ActorId);
        db.Allocations.Add(allocation);
        var journal = await CreateAllocationJournalAsync(db, context, document, item, allocation, controlAccountId.Value, settlementAccountId.Value, itemFunctional, documentFunctional, realizedDifference, allocationRule.Value!, realizedRule, command.Reason ?? "Settlement allocation", cancellationToken);
        if (!journal.Succeeded || journal.Value is null) return Failure<FinanceAllocationRecord>(journal.Code);
        allocation.SetJournal(journal.Value.Id);
        allocation.SetRealizedFx(itemFunctional, documentFunctional, Math.Abs(realizedDifference), realizedFx.Direction, Math.Abs(realizedDifference) > 0.00000001m ? journal.Value.Id : null, realizedRule?.Id, realizedRule?.VersionNumber);
        var now = DateTimeOffset.UtcNow;
        AddAudit(db, context, "finance.allocation.create", "allocation", allocation.Id, "Succeeded", command.Reason, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceAllocationRecord>.Success(ToAllocation(allocation));
        AddReplay(db, context, "finance.allocation.create", command.IdempotencyKey, command.RequestFingerprint, "allocation", allocation.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<FinanceOperationResult<FinanceAllocationRecord>> ReverseAllocationAsync(FinanceRequestContext context, FinanceAllocationReversalCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceAllocationRecord>("reason_required");
        await using var db = CreateContext(context);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceAllocationRecord>(db, context, "finance.allocation.reverse", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var original = await db.Allocations.SingleOrDefaultAsync(item => item.Id == command.AllocationId, cancellationToken);
        if (original is null || Company(context, original.CompanyId) is null) return Failure<FinanceAllocationRecord>("company_scope_denied");
        if (!original.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceAllocationRecord>("concurrency_conflict");
        if (original.Status != FinanceAllocationStatus.Active || original.ReversalOfAllocationId is not null || await db.Allocations.AnyAsync(item => item.ReversalOfAllocationId == original.Id, cancellationToken)) return Failure<FinanceAllocationRecord>("allocation_already_reversed");
        var document = await db.SettlementDocuments.SingleOrDefaultAsync(item => item.Id == original.SettlementDocumentId, cancellationToken);
        if (document is null || document.Status == FinanceSettlementDocumentStatus.Reversed) return Failure<FinanceAllocationRecord>("settlement_reversed");
        var originalJournal = original.JournalId is { } originalJournalId
            ? await db.Journals.Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == originalJournalId, cancellationToken)
            : null;
        if (originalJournal is null) return Failure<FinanceAllocationRecord>("posting_lineage_missing");
        var reversal = new FinanceAllocationEntity(context.TenantId, command, original, original.CompanyId, context.ActorId);
        db.Allocations.Add(reversal);
        var journal = await CreateReversalJournalAsync(db, context, originalJournal, reversal.AllocationDate, command.Reason, command.Id, cancellationToken);
        if (!journal.Succeeded || journal.Value is null) return Failure<FinanceAllocationRecord>(journal.Code);
        reversal.SetJournal(journal.Value.Id);
        var now = DateTimeOffset.UtcNow;
        AddAudit(db, context, "finance.allocation.reverse", "allocation", original.Id, "Succeeded", command.Reason, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceAllocationRecord>.Success(ToAllocation(reversal));
        AddReplay(db, context, "finance.allocation.reverse", command.IdempotencyKey, command.RequestFingerprint, "allocation", reversal.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<FinanceAgingRecord>> GetAgingAsync(FinanceRequestContext context, FinanceAgingQuery query, CancellationToken cancellationToken = default)
    {
        if (Company(context, query.CompanyId) is null) return [];
        await using var db = CreateContext(context);
        var source = db.OpenItems.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.RecognitionState == FinanceOpenItemRecognitionState.Recognized && item.DocumentDate <= query.AsOfDate);
        if (query.Kind is { } kind) source = source.Where(item => item.Kind == kind);
        if (query.PartyId is { } partyId) source = query.Kind == FinanceOpenItemKind.Payable ? source.Where(item => item.SupplierId == partyId) : source.Where(item => item.CustomerId == partyId);
        var items = await source.OrderBy(item => item.DueDate).Take(2000).ToListAsync(cancellationToken);
        var allocations = await EffectiveAllocations(db, query.AsOfDate).AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<FinanceAgingRecord>(items.Count);
        foreach (var item in items)
        {
            var itemAllocations = allocations.Where(value => value.OpenItemId == item.Id).ToArray();
            var allocated = itemAllocations.Sum(value => value.Amount);
            var outstanding = Math.Max(0m, item.OriginalAmount - allocated);
            var status = outstanding == 0m ? FinanceOpenItemStatus.Settled : allocated > 0m ? FinanceOpenItemStatus.PartiallySettled : FinanceOpenItemStatus.Open;
            var overdue = outstanding > 0m && item.DueDate < query.AsOfDate ? query.AsOfDate.DayNumber - item.DueDate.DayNumber : 0;
            result.Add(new(item.Id, item.Kind, item.SupplierId, item.CustomerId, item.Reference, item.DocumentDate, item.DueDate, query.AsOfDate, overdue, item.CurrencyCode, item.OriginalAmount, allocated, outstanding, status));
        }
        return result;
    }

    public async Task<FinanceCustomerExposureRecord?> GetExposureAsync(FinanceRequestContext context, FinanceExposureQuery query, CancellationToken cancellationToken = default)
    {
        var company = Company(context, query.CompanyId); if (company is null || !await CustomerIsActiveAsync(context, query.CustomerId, cancellationToken)) return null;
        await using var db = CreateContext(context);
        var allocations = await EffectiveAllocations(db, query.AsOfDate).AsNoTracking().ToListAsync(cancellationToken);
        var items = await db.OpenItems.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.Kind == FinanceOpenItemKind.Receivable && item.CustomerId == query.CustomerId && item.RecognitionState == FinanceOpenItemRecognitionState.Recognized && item.DocumentDate <= query.AsOfDate).ToListAsync(cancellationToken);
        decimal open = 0m, overdue = 0m;
        foreach (var item in items)
        {
            var allocatedFunctional = allocations.Where(value => value.OpenItemId == item.Id).Sum(value => value.FunctionalAmount);
            var outstandingFunctional = Math.Max(0m, item.OriginalFunctionalAmount - allocatedFunctional);
            open += outstandingFunctional;
            if (item.DueDate < query.AsOfDate) overdue += outstandingFunctional;
        }
        var receipts = await db.SettlementDocuments.AsNoTracking()
            .Where(item => item.CompanyId == query.CompanyId && item.CustomerId == query.CustomerId && item.Direction == FinancePaymentMethodDirection.Receipt && item.DocumentDate <= query.AsOfDate)
            .ToListAsync(cancellationToken);
        var receiptJournalIds = receipts.SelectMany(item => new[] { item.PostedJournalId, item.ReversalJournalId }).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var receiptJournals = await db.Journals.AsNoTracking().Where(item => receiptJournalIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var effectiveReceipts = receipts.Where(item => IsSettlementEffectEffective(item, receiptJournals, query.AsOfDate)).ToArray();
        var unapplied = effectiveReceipts.Sum(receipt => Math.Max(0m, receipt.FunctionalAmount - allocations.Where(value => value.SettlementDocumentId == receipt.Id).Sum(value => value.FunctionalAmount)));
        return new(query.CompanyId, query.CustomerId, company.FunctionalCurrencyCode, open, overdue, unapplied, open - unapplied, query.AsOfDate, false, null);
    }

    public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) =>
        GetReconciliationAsync(context, companyId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);

    public async Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var company = Company(context, companyId); if (company is null) return [];
        await using var db = CreateContext(context);
        var asOf = asOfDate;
        var items = await db.OpenItems.AsNoTracking().Where(item => item.CompanyId == companyId && item.RecognitionState == FinanceOpenItemRecognitionState.Recognized && item.DocumentDate <= asOf).ToListAsync(cancellationToken);
        var documents = await db.SettlementDocuments.AsNoTracking().Where(item => item.CompanyId == companyId && item.DocumentDate <= asOf).ToListAsync(cancellationToken);
        var allocations = await EffectiveAllocations(db, asOf).AsNoTracking().Where(item => item.CompanyId == companyId).ToListAsync(cancellationToken);
        var allocationHistory = await db.Allocations.AsNoTracking().Where(item => item.CompanyId == companyId && item.AllocationDate <= asOf).ToListAsync(cancellationToken);
        var journals = await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId && item.PostingDate <= asOf && (item.Status == FinanceJournalStatus.Posted || item.Status == FinanceJournalStatus.Reversed)).ToListAsync(cancellationToken);
        var journalById = journals.ToDictionary(item => item.Id);
        var output = new List<FinanceReconciliationRecord>();
        foreach (var kind in new[] { FinanceOpenItemKind.Payable, FinanceOpenItemKind.Receivable })
        {
            var subset = items.Where(item => item.Kind == kind).ToArray();
            var subledger = subset.Sum(item => item.OriginalFunctionalAmount - allocations.Where(value => value.OpenItemId == item.Id).Sum(value => value.FunctionalAmount));
            var missingRecognition = false;
            var missingAllocation = false;
            var mappingMismatch = false;
            var posted = 0m;
            foreach (var item in subset)
            {
                if (item.RecognitionJournalId is not { } recognitionId || !journalById.TryGetValue(recognitionId, out var recognitionJournal))
                {
                    missingRecognition = true;
                    continue;
                }

                var controlAccountId = ResolveControlAccountId(kind, recognitionJournal);
                if (controlAccountId is null)
                {
                    missingRecognition = true;
                    continue;
                }

                posted += ControlEffect(kind, recognitionJournal, controlAccountId.Value);
                foreach (var allocation in allocationHistory.Where(value => value.OpenItemId == item.Id))
                {
                    if (allocation.JournalId is not { } allocationJournalId || !journalById.TryGetValue(allocationJournalId, out var allocationJournal))
                    {
                        missingAllocation = true;
                        continue;
                    }

                    if (!allocationJournal.Lines.Any(line => line.AccountId == controlAccountId)) mappingMismatch = true;
                    posted += ControlEffect(kind, allocationJournal, controlAccountId.Value);
                }
            }

            var status = mappingMismatch
                ? FinanceReconciliationStatus.PendingMapping
                : missingRecognition || missingAllocation
                    ? FinanceReconciliationStatus.PendingPosting
                    : SameAmount(subledger, posted)
                        ? FinanceReconciliationStatus.Reconciled
                        : FinanceReconciliationStatus.AmountMismatch;
            output.Add(new(companyId, kind, kind == FinanceOpenItemKind.Payable ? "AP control account versus active AP outstanding" : "AR control account versus active AR outstanding", subledger, posted, subledger - posted, status, asOf));
        }
        foreach (var cash in await db.CashAccounts.AsNoTracking().Where(item => item.CompanyId == companyId).OrderBy(item => item.Code).ToListAsync(cancellationToken))
        {
            var linked = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == cash.LinkedAccountId, cancellationToken);
            var cashDocumentIds = documents.Where(item => item.CashAccountId == cash.Id).SelectMany(item => new[] { item.PostedJournalId, item.ReversalJournalId }).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
            var cashJournals = cashDocumentIds.Where(journalById.ContainsKey).Select(id => journalById[id]).ToDictionary(item => item.Id);
            var cashDocuments = documents.Where(item => item.CashAccountId == cash.Id && IsSettlementEffectEffective(item, journalById, asOf)).ToArray();
            var expected = cashDocuments.Sum(item => item.Direction == FinancePaymentMethodDirection.Receipt ? item.FunctionalAmount : -item.FunctionalAmount);
            var journalIds = cashDocuments.SelectMany(item => new[] { item.PostedJournalId, item.ReversalJournalId }).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var actual = linked is null ? 0m : cashJournals.Values.Where(item => journalIds.Contains(item.Id)).SelectMany(item => item.Lines).Where(line => line.AccountId == cash.LinkedAccountId).Sum(line => line.FunctionalDebit - line.FunctionalCredit);
            var status = linked is null || linked.Lifecycle != FinanceAccountLifecycle.Active || !linked.IsPostingAccount ? FinanceReconciliationStatus.PendingMapping : SameAmount(expected, actual) ? FinanceReconciliationStatus.Reconciled : FinanceReconciliationStatus.AmountMismatch;
            output.Add(new(companyId, null, $"Cash/Bank {cash.Code} document movement versus linked GL account {cash.LinkedAccountCode}", expected, actual, expected - actual, status, asOf));
        }
        return output;
    }

    private async Task<(bool Succeeded, string Code, decimal Value)> ValidateCurrencyAsync(FinanceRequestContext context, FinanceCompanyOption company, string currency, DateOnly date, decimal? rate, Guid? rateId, Guid? versionId, int? versionNumber, CancellationToken cancellationToken)
    {
        if (currency == company.FunctionalCurrencyCode) return rate is null or 1m && rateId is null && versionId is null && versionNumber is null ? (true, "eligible", 1m) : (false, "functional_currency_rate_must_be_explicit_one", 0m);
        if (rate is not > 0m || rateId is null || versionId is null || versionNumber is not > 0) return (false, "exact_exchange_rate_evidence_required", 0m); var record = await exchangeRates.FindExchangeRateAsync(context.TenantContext, rateId.Value, cancellationToken); var version = record?.Versions.SingleOrDefault(item => item.Id == versionId.Value && item.VersionNumber == versionNumber.Value); if (record is null || record.LifecycleState != MasterDataLifecycleState.Active || version is null || !string.Equals(record.SourceCurrencyCode, currency, StringComparison.OrdinalIgnoreCase) || !string.Equals(record.TargetCurrencyCode, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase) || version.Rate != rate.Value || version.EffectiveFrom > date || version.EffectiveTo < date) return (false, "exchange_rate_evidence_mismatch", 0m); return (true, "eligible", rate.Value);
    }

    private async Task<(string Code, FinanceSalesInvoiceEligibilityRecord? Eligibility, SalesInvoiceTaxSummary? Summary)> ValidateSalesInvoiceAsync(FinanceRequestContext context, FinanceDbContext db, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken)
    {
        var company = Company(context, command.CompanyId);
        var currency = NormalizeCode(command.CurrencyCode);
        if (company is null) return ("company_scope_denied", null, null);
        if (command.SalesOrderId == Guid.Empty || command.InvoiceRequestId == Guid.Empty || command.SalesOrderRevision <= 0 || command.DocumentDate == default || command.Amount <= 0m || command.PaymentTermId == Guid.Empty || string.IsNullOrWhiteSpace(command.SourceSnapshot)) return ("invalid_sales_invoice", null, null);
        var amounts = ValidateSalesInvoiceAmounts(command);
        if (amounts.Value is null) return (amounts.Code, null, null);
        if (!await CustomerIsActiveAsync(context, command.CustomerId, cancellationToken)) return ("party_scope_denied", null, null);

        FinancePaymentTermSnapshotRecord? paymentTerm;
        if (command.PaymentTerm is { } suppliedTerm)
        {
            if (suppliedTerm.Id != command.PaymentTermId || suppliedTerm.VersionId == Guid.Empty || suppliedTerm.VersionNumber <= 0 || string.IsNullOrWhiteSpace(suppliedTerm.Code) || suppliedTerm.EffectiveOn == default || suppliedTerm.DueDate is null) return ("payment_term_snapshot_mismatch", null, null);
            paymentTerm = suppliedTerm;
        }
        else
        {
            var resolved = await ResolvePaymentTermAsync(context, command.PaymentTermId, null, command.DocumentDate, cancellationToken);
            if (!resolved.Succeeded || resolved.Value is null) return (resolved.Code, null, null);
            paymentTerm = resolved.Value.Value.Snapshot;
        }

        var rate = await ValidateCurrencyAsync(context, company, currency ?? string.Empty, command.DocumentDate, command.ExchangeRate, command.ExchangeRateId, command.ExchangeRateVersionId, command.ExchangeRateVersionNumber, cancellationToken);
        if (!rate.Succeeded) return (rate.Code, null, null);
        if (await db.OpenItems.AnyAsync(item => item.CompanyId == command.CompanyId && item.SourceContract == SalesInvoiceContract && item.SourceEvidenceId == command.InvoiceRequestId && item.SourceEvidenceVersion == 1, cancellationToken)) return ("source_effect_exists", null, null);
        var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.CompanyId == command.CompanyId && item.StartDate <= command.DocumentDate && item.EndDate >= command.DocumentDate, cancellationToken);
        if (period is null) return ("period_not_configured", null, null);
        if (period.State != FinanceFiscalPeriodState.Open) return (period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", null, null);
        var rule = await FindRuleAsync(db, command.CompanyId, SalesInvoiceContract, "recognition", command.DocumentDate, cancellationToken);
        if (rule.Code != "eligible" || rule.Value is null) return (rule.Code, null, null);
        var normalized = currency!;
        return ("eligible", new FinanceSalesInvoiceEligibilityRecord(true, "eligible", command.Amount, normalized, command.SalesOrderId, command.SalesOrderRevision, command.DocumentDate, paymentTerm, normalized == company.FunctionalCurrencyCode ? 1m : rate.Value, normalized == company.FunctionalCurrencyCode ? null : command.ExchangeRateId, normalized == company.FunctionalCurrencyCode ? null : command.ExchangeRateVersionId, normalized == company.FunctionalCurrencyCode ? null : command.ExchangeRateVersionNumber), amounts.Value);
    }

    private static (string Code, SalesInvoiceTaxSummary? Value) ValidateSalesInvoiceAmounts(FinanceSalesInvoiceCommand command)
    {
        var lines = command.Lines?.ToArray();
        if (lines is null)
        {
            var tax = command.TaxAmount ?? 0m;
            var net = command.NetAmount ?? (command.Amount - tax);
            return tax < 0m || net < 0m || !SameAmount(net + tax, command.Amount) || tax > 0m ? (tax > 0m ? "tax_evidence_not_authoritative" : "invoice_amount_mismatch", null) : ("eligible", new SalesInvoiceTaxSummary(net, 0m, [], []));
        }

        if (lines.Length == 0 || lines.Any(line => line.OrderLineId == Guid.Empty || line.Quantity <= 0m || line.NetAmount < 0m || line.TaxAmount < 0m || line.GrossAmount < 0m || !SameAmount(line.NetAmount + line.TaxAmount, line.GrossAmount))) return ("invoice_amount_mismatch", null);
        var netAmount = lines.Sum(line => line.NetAmount);
        var taxAmount = lines.Sum(line => line.TaxAmount);
        var grossAmount = lines.Sum(line => line.GrossAmount);
        if (!SameAmount(grossAmount, command.Amount) || command.NetAmount is { } expectedNet && !SameAmount(expectedNet, netAmount) || command.TaxAmount is { } expectedTax && !SameAmount(expectedTax, taxAmount)) return ("invoice_amount_mismatch", null);
        var taxableLines = lines.Where(line => line.TaxAmount > 0m).ToArray();
        if (taxableLines.Length == 0) return (taxAmount == 0m ? "eligible" : "tax_evidence_not_authoritative", taxAmount == 0m ? new SalesInvoiceTaxSummary(netAmount, 0m, lines, []) : null);
        if (taxableLines.Any(line => line.TaxId is null || string.IsNullOrWhiteSpace(line.TaxCode) || line.TaxRateVersionId is null || line.TaxRateVersionNumber is not > 0 || line.TaxEffectiveFrom is null || line.TaxRatePercentage is null || line.TaxableBase is null || line.TaxableBase < 0m || string.IsNullOrWhiteSpace(line.TaxReferenceValue))) return ("tax_evidence_not_authoritative", null);
        var groups = taxableLines
            .GroupBy(line => new { line.TaxId, line.TaxCode, line.TaxRateVersionId, line.TaxRateVersionNumber, line.TaxEffectiveFrom, line.TaxEffectiveTo, line.TaxRatePercentage, line.TaxReferenceValue })
            .Select(group => new SalesInvoiceTaxGroup(group.First(), group.Sum(line => line.TaxableBase!.Value), group.Sum(line => line.TaxAmount)))
            .ToArray();
        return ("eligible", new SalesInvoiceTaxSummary(netAmount, taxAmount, lines, groups));
    }

    private async Task<(bool Succeeded, string Code)> PostSalesInvoiceTaxAsync(FinanceDbContext db, FinanceRequestContext context, FinanceSalesInvoiceCommand command, FinanceOpenItemEntity item, SalesInvoiceTaxSummary summary, string currency, decimal? exchangeRate, Guid? exchangeRateId, Guid? exchangeRateVersionId, int? exchangeRateVersionNumber, FinancePostingRuleEntity salesRule, CancellationToken cancellationToken)
    {
        if (summary.TaxAmount == 0m) return (true, "eligible");
        var taxRule = await FindRuleAsync(db, command.CompanyId, "finance-tax.v1", "output", command.DocumentDate, cancellationToken);
        if (taxRule.Code != "eligible" || taxRule.Value is null) return (false, taxRule.Code == "pending_mapping" ? "tax_posting_rule_not_configured" : taxRule.Code);
        if (taxRule.Value.DebitAccountId != salesRule.CreditAccountId || taxRule.Value.CreditAccountId == taxRule.Value.DebitAccountId) return (false, "tax_source_account_mismatch");
        for (var index = 0; index < summary.TaxGroups.Count; index++)
        {
            var group = summary.TaxGroups[index];
            var taxLine = group.TaxLine;
            var functionalTaxAmount = decimal.Round(group.TaxAmount * (exchangeRate ?? 1m), 8, MidpointRounding.ToEven);
            var evidence = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, command.CompanyId, command.DocumentDate, currency, group.TaxAmount, Company(context, command.CompanyId)!.FunctionalCurrencyCode, functionalTaxAmount, exchangeRate, currency == Company(context, command.CompanyId)!.FunctionalCurrencyCode ? null : exchangeRateId, currency == Company(context, command.CompanyId)!.FunctionalCurrencyCode ? null : exchangeRateVersionId, currency == Company(context, command.CompanyId)!.FunctionalCurrencyCode ? null : exchangeRateVersionNumber, cancellationToken);
            if (!evidence.Succeeded || evidence.Evidence is null) return (false, evidence.Code);
            var sourceEvidenceId = summary.TaxGroups.Count == 1 ? command.InvoiceRequestId : TaxSourceEvidenceId(command.InvoiceRequestId, taxLine);
            var journal = await CreatePostedJournalAsync(db, context, command.CompanyId, command.DocumentDate, currency, group.TaxAmount, functionalTaxAmount, exchangeRate, exchangeRateId, exchangeRateVersionId, exchangeRateVersionNumber, "finance-tax.v1", "output", sourceEvidenceId, 1, taxRule.Value, false, $"Sales invoice output tax {index + 1}", cancellationToken);
            if (!journal.Succeeded || journal.Value is null) return (false, journal.Code);
            db.TaxAccountingEffects.Add(new FinanceTaxAccountingEffectEntity(context.TenantId, sourceEvidenceId, command.CompanyId, item.Id, FinanceOpenItemKind.Receivable, taxLine.TaxId!.Value, taxLine.TaxCode!, taxLine.TaxRateVersionId!.Value, taxLine.TaxRateVersionNumber!.Value, taxLine.TaxEffectiveFrom!.Value, taxLine.TaxRatePercentage!.Value, group.TaxableBase, group.TaxAmount, currency, functionalTaxAmount, Company(context, command.CompanyId)!.FunctionalCurrencyCode, journal.Value.Id, taxRule.Value.Id, taxRule.Value.VersionNumber, evidence.Evidence, context.ActorId, DateTimeOffset.UtcNow));
            AddAudit(db, context, "finance.tax-accounting.post", "tax-accounting-effect", command.InvoiceRequestId, "Succeeded", "Sales invoice output tax", command.IdempotencyKey, DateTimeOffset.UtcNow);
        }
        return (true, "eligible");
    }

    private static Guid TaxSourceEvidenceId(Guid invoiceRequestId, FinanceSalesInvoiceLine taxLine)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { invoiceRequestId, taxLine.TaxId, taxLine.TaxCode, taxLine.TaxRateVersionId, taxLine.TaxRateVersionNumber, taxLine.TaxEffectiveFrom, taxLine.TaxEffectiveTo, taxLine.TaxRatePercentage, taxLine.TaxReferenceValue })));
        return new Guid(bytes[..16]);
    }

    private sealed record SalesInvoiceTaxSummary(decimal NetAmount, decimal TaxAmount, IReadOnlyList<FinanceSalesInvoiceLine> Lines, IReadOnlyList<SalesInvoiceTaxGroup> TaxGroups);
    private sealed record SalesInvoiceTaxGroup(FinanceSalesInvoiceLine TaxLine, decimal TaxableBase, decimal TaxAmount);

    private async Task<(bool Succeeded, string Code, (DateOnly DueDate, FinancePaymentTermSnapshotRecord Snapshot)? Value)> ResolvePaymentTermAsync(FinanceRequestContext context, Guid? paymentTermId, DateOnly? dueDate, DateOnly documentDate, CancellationToken cancellationToken)
    {
        if (paymentTermId is null) return (false, "payment_term_not_configured", null);
        var term = await paymentTerms.FindPaymentTermAsync(context.TenantContext, paymentTermId.Value, cancellationToken);
        if (term is null || term.LifecycleState != MasterDataLifecycleState.Active) return (false, "payment_term_not_configured", null);
        var version = term.Versions
            .Where(item => item.EffectiveFrom <= documentDate && (item.EffectiveTo is null || item.EffectiveTo >= documentDate))
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefault();
        if (version is null) return (false, "payment_term_not_configured", null);
        var baseDate = version.BaseDateRule switch
        {
            PaymentTermBaseDateRule.DocumentDate => documentDate,
            // Manual AR has no separately trusted invoice, receipt, or delivery
            // date. It therefore fails closed for those term bases rather than
            // silently treating the document date as another business fact.
            _ => (DateOnly?)null
        };
        if (baseDate is null) return (false, "payment_term_not_configured", null);
        var due = version.ScheduleMode == PaymentTermScheduleMode.SingleDueDate
            ? AddOffset(baseDate.Value, version.DueOffset)
            : version.Installments.OrderBy(item => item.Sequence).Select(item => AddOffset(baseDate.Value, item.Offset)).LastOrDefault(baseDate.Value);
        if (dueDate is { } explicitDue && explicitDue != due) return (false, "payment_term_snapshot_mismatch", null);
        var snapshot = new FinancePaymentTermSnapshotRecord(term.Id, version.Code, version.Name.English, version.Name.Arabic, version.VersionNumber, version.Id, documentDate, due);
        return (true, "eligible", (due, snapshot));
    }

    private async Task<(string Code, FinancePostingRuleEntity? Value)> FindRuleAsync(FinanceDbContext db, Guid companyId, string contract, string eventName, DateOnly date, CancellationToken cancellationToken)
    { var rules = await db.PostingRules.Where(item => item.CompanyId == companyId && item.SourceContract == contract && item.SourceEvent == eventName && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= date && (item.EffectiveTo == null || item.EffectiveTo >= date)).ToListAsync(cancellationToken); return rules.Count switch { 0 => ("pending_mapping", null), 1 => ("eligible", rules[0]), _ => ("ambiguous_mapping", null) }; }

    internal static (decimal Difference, string? Direction) ResolveRealizedFx(FinanceOpenItemKind kind, decimal historicalFunctionalAmount, decimal settlementFunctionalAmount)
    {
        var difference = decimal.Round(settlementFunctionalAmount - historicalFunctionalAmount, 8);
        if (difference == 0m) return (difference, null);
        var direction = kind == FinanceOpenItemKind.Payable
            ? difference > 0m ? "Loss" : "Gain"
            : difference > 0m ? "Gain" : "Loss";
        return (difference, direction);
    }

    private async Task<(bool Succeeded, string Code, FinanceJournalRecord? Value)> CreateAllocationJournalAsync(FinanceDbContext db, FinanceRequestContext context, FinanceSettlementDocumentEntity document, FinanceOpenItemEntity item, FinanceAllocationEntity allocation, Guid controlAccountId, Guid settlementAccountId, decimal historicalFunctionalAmount, decimal settlementFunctionalAmount, decimal realizedDifference, FinancePostingRuleEntity allocationRule, FinancePostingRuleEntity? realizedRule, string description, CancellationToken cancellationToken)
    {
        var company = Company(context, document.CompanyId); if (company is null) return (false, "company_scope_denied", null);
        var period = await db.FiscalPeriods.Where(value => value.CompanyId == document.CompanyId && value.StartDate <= allocation.AllocationDate && value.EndDate >= allocation.AllocationDate).SingleOrDefaultAsync(cancellationToken); if (period is null) return (false, "period_not_configured", null); if (period.State != FinanceFiscalPeriodState.Open) return (false, period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", null);
        var values = new List<(Guid AccountId, decimal Debit, decimal Credit, decimal Functional, string Description)>();
        var magnitude = Math.Abs(realizedDifference);
        var lossAccount = realizedRule?.DebitAccountId; var gainAccount = realizedRule?.CreditAccountId;
        if (item.Kind == FinanceOpenItemKind.Payable)
        {
            values.Add((controlAccountId, historicalFunctionalAmount, 0m, historicalFunctionalAmount, "Historical payable carrying value"));
            values.Add((settlementAccountId, 0m, settlementFunctionalAmount, settlementFunctionalAmount, "Settlement cash or bank"));
            if (realizedDifference > 0m) values.Add((lossAccount!.Value, magnitude, 0m, magnitude, "Realized FX loss"));
            else if (realizedDifference < 0m) values.Add((gainAccount!.Value, 0m, magnitude, magnitude, "Realized FX gain"));
        }
        else
        {
            values.Add((settlementAccountId, settlementFunctionalAmount, 0m, settlementFunctionalAmount, "Settlement cash or bank"));
            values.Add((controlAccountId, 0m, historicalFunctionalAmount, historicalFunctionalAmount, "Historical receivable carrying value"));
            if (realizedDifference > 0m) values.Add((gainAccount!.Value, 0m, magnitude, magnitude, "Realized FX gain"));
            else if (realizedDifference < 0m) values.Add((lossAccount!.Value, magnitude, 0m, magnitude, "Realized FX loss"));
        }
        var ids = values.Select(value => value.AccountId).Distinct().ToArray(); var accounts = await db.Accounts.Where(value => value.CompanyId == document.CompanyId && ids.Contains(value.Id)).ToDictionaryAsync(value => value.Id, cancellationToken); if (accounts.Count != ids.Length || accounts.Values.Any(value => !value.IsPostingAccount || value.Lifecycle != FinanceAccountLifecycle.Active || value.EffectiveFrom > allocation.AllocationDate || value.EffectiveTo < allocation.AllocationDate)) return (false, "account_not_postable", null);
        var journalId = Guid.NewGuid(); var sourceContract = ContractFor(document.Direction); var command = new FinanceJournalCommand(document.CompanyId, allocation.AllocationDate, allocation.AllocationDate, company.FunctionalCurrencyCode, null, null, null, null, sourceContract, "allocation", allocation.Id, 1, allocationRule.Id, description, values.Select(value => new FinanceJournalLineCommand(value.AccountId, value.Debit, value.Credit, value.Functional, company.FunctionalCurrencyCode, null, value.Description)).ToArray(), journalId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var transactionDebitTotal = values.Sum(value => value.Debit);
        var transactionCreditTotal = values.Sum(value => value.Credit);
        var functionalDebitTotal = values.Where(value => value.Debit > 0m).Sum(value => value.Functional);
        var functionalCreditTotal = values.Where(value => value.Credit > 0m).Sum(value => value.Functional);
        if (!SameAmount(transactionDebitTotal, transactionCreditTotal)
            || !SameAmount(functionalDebitTotal, functionalCreditTotal))
        {
            return (false, "journal_not_balanced", null);
        }

        // This journal is denominated in the Company's functional currency. Its
        // monetary evidence represents one balanced accounting effect, not the
        // sum of absolute magnitudes on both sides of the journal.
        var balancedFunctionalAmount = functionalDebitTotal;
        var journal = new FinanceJournalEntity(context.TenantId, journalId, command, (await db.Journals.Where(value => value.CompanyId == document.CompanyId).Select(value => (long?)value.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow); journal.SetCorrelation(context.CorrelationId); journal.SetPeriod(period.FiscalYearId, period.Id); journal.SetRule(allocationRule.Id, allocationRule.VersionNumber); journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); var number = 1; foreach (var value in values) journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journalId, number++, accounts[value.AccountId], command.Lines[number - 2], null, value.Debit > 0m ? value.Functional : 0m, value.Credit > 0m ? value.Functional : 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency)); var evidenceBuild = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, document.CompanyId, allocation.AllocationDate, company.FunctionalCurrencyCode, transactionDebitTotal, company.FunctionalCurrencyCode, balancedFunctionalAmount, null, null, null, null, cancellationToken); if (!evidenceBuild.Succeeded) return (false, evidenceBuild.Code, null); db.Journals.Add(journal); if (evidenceBuild.Evidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), journal.Id, document.CompanyId, null, evidenceBuild.Evidence, DateTimeOffset.UtcNow)); db.SourceEffects.Add(new FinanceSourceEffectEntity(context.TenantId, Guid.NewGuid(), document.CompanyId, sourceContract, allocation.Id, 1, journal.Id, DateTimeOffset.UtcNow)); return (true, "succeeded", ToJournal(journal));
    }

    private async Task<(bool Succeeded, string Code, FinanceJournalRecord? Value)> CreatePostedJournalAsync(FinanceDbContext db, FinanceRequestContext context, Guid companyId, DateOnly date, string currency, decimal amount, decimal functionalAmount, decimal? exchangeRate, Guid? exchangeRateId, Guid? exchangeRateVersionId, int? exchangeRateVersionNumber, string sourceContract, string sourceEvent, Guid sourceEvidenceId, int sourceEvidenceVersion, FinancePostingRuleEntity rule, bool reverse, string description, CancellationToken cancellationToken)
    {
        var company = Company(context, companyId); if (company is null) return (false, "company_scope_denied", null); var period = await db.FiscalPeriods.Where(item => item.CompanyId == companyId && item.StartDate <= date && item.EndDate >= date).SingleOrDefaultAsync(cancellationToken); if (period is null) return (false, "period_not_configured", null); if (period.State != FinanceFiscalPeriodState.Open) return (false, period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", null); var debitId = reverse ? rule.CreditAccountId : rule.DebitAccountId; var creditId = reverse ? rule.DebitAccountId : rule.CreditAccountId; var accounts = await db.Accounts.Where(item => item.CompanyId == companyId && (item.Id == debitId || item.Id == creditId)).ToDictionaryAsync(item => item.Id, cancellationToken); if (accounts.Count != 2 || accounts.Values.Any(item => !item.IsPostingAccount || item.Lifecycle != FinanceAccountLifecycle.Active || item.EffectiveFrom > date || item.EffectiveTo < date)) return (false, "account_not_postable", null); if (await db.Journals.AnyAsync(item => item.SourceContract == sourceContract && item.SourceEvidenceId == sourceEvidenceId && item.SourceEvidenceVersion == sourceEvidenceVersion, cancellationToken)) return (false, "source_effect_exists", null); var sequence = (await db.Journals.Where(item => item.CompanyId == companyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L; while (db.ChangeTracker.Entries<FinanceJournalEntity>().Any(entry => entry.State != EntityState.Deleted && entry.Entity.CompanyId == companyId && entry.Entity.JournalSequence == sequence)) sequence++; var txCurrency = NormalizeCode(currency)!; var rate = txCurrency == company.FunctionalCurrencyCode ? 1m : exchangeRate!.Value; var command = new FinanceJournalCommand(companyId, date, date, txCurrency, rate, txCurrency == company.FunctionalCurrencyCode ? null : exchangeRateId, txCurrency == company.FunctionalCurrencyCode ? null : exchangeRateVersionId, txCurrency == company.FunctionalCurrencyCode ? null : exchangeRateVersionNumber, sourceContract, sourceEvent, sourceEvidenceId, sourceEvidenceVersion, rule.Id, description, [new FinanceJournalLineCommand(debitId, amount, 0m, amount, txCurrency, null, description), new FinanceJournalLineCommand(creditId, 0m, amount, amount, txCurrency, null, description)], Guid.NewGuid(), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired); var journal = new FinanceJournalEntity(context.TenantId, command.Id, command, sequence, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow); journal.SetCorrelation(context.CorrelationId); journal.SetPeriod(period.FiscalYearId, period.Id); journal.SetRule(rule.Id, rule.VersionNumber); journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 1, accounts[debitId], command.Lines[0], null, reverse ? functionalAmount : functionalAmount, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency)); journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 2, accounts[creditId], command.Lines[1], null, 0m, functionalAmount, FinanceJournalAmountAuthority.ManualTransactionCurrency)); var evidenceBuild = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, companyId, date, txCurrency, amount, company.FunctionalCurrencyCode, functionalAmount, rate, txCurrency == company.FunctionalCurrencyCode ? null : exchangeRateId, txCurrency == company.FunctionalCurrencyCode ? null : exchangeRateVersionId, txCurrency == company.FunctionalCurrencyCode ? null : exchangeRateVersionNumber, cancellationToken); if (!evidenceBuild.Succeeded) return (false, evidenceBuild.Code, null); db.Journals.Add(journal); if (evidenceBuild.Evidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), journal.Id, companyId, null, evidenceBuild.Evidence, DateTimeOffset.UtcNow)); db.SourceEffects.Add(new FinanceSourceEffectEntity(context.TenantId, Guid.NewGuid(), companyId, sourceContract, sourceEvidenceId, sourceEvidenceVersion, journal.Id, DateTimeOffset.UtcNow)); return (true, "succeeded", ToJournal(journal));
    }

    private async Task<(bool Succeeded, string Code, FinanceJournalRecord? Value)> CreateReversalJournalAsync(FinanceDbContext db, FinanceRequestContext context, FinanceJournalEntity original, DateOnly postingDate, string reason, Guid commandId, CancellationToken cancellationToken)
    {
        var period = await db.FiscalPeriods.Where(item => item.CompanyId == original.CompanyId && item.StartDate <= postingDate && item.EndDate >= postingDate).SingleOrDefaultAsync(cancellationToken); if (period is null) return (false, "period_not_configured", null); if (period.State != FinanceFiscalPeriodState.Open) return (false, period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", null); var accounts = await db.Accounts.Where(item => item.CompanyId == original.CompanyId && original.Lines.Select(line => line.AccountId).Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken); if (accounts.Count != original.Lines.Select(line => line.AccountId).Distinct().Count() || accounts.Values.Any(item => !item.IsPostingAccount || item.Lifecycle != FinanceAccountLifecycle.Active)) return (false, "account_not_postable", null); var company = Company(context, original.CompanyId)!; var sequence = (await db.Journals.Where(item => item.CompanyId == original.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L; var command = new FinanceJournalCommand(original.CompanyId, original.JournalDate, postingDate, original.TransactionCurrencyCode, original.ExchangeRate, original.ExchangeRateId, original.ExchangeRateVersionId, original.ExchangeRateVersionNumber, "finance-reversal.v1", "settlement-reversal", null, null, null, reason, original.Lines.OrderBy(item => item.LineNumber).Select(line => new FinanceJournalLineCommand(line.AccountId, line.Credit, line.Debit, line.TransactionAmount, line.TransactionCurrencyCode, line.CostCenterId, reason)).ToArray(), commandId, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired); var reversal = new FinanceJournalEntity(context.TenantId, Guid.NewGuid(), command, sequence, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow); reversal.SetCorrelation(context.CorrelationId); reversal.LinkOriginal(original.Id); reversal.SetPeriod(period.FiscalYearId, period.Id); reversal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); foreach (var line in original.Lines.OrderBy(item => item.LineNumber)) { var account = accounts[line.AccountId]; reversal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), reversal.Id, line.LineNumber, account, new FinanceJournalLineCommand(line.AccountId, line.Credit, line.Debit, line.TransactionAmount, line.TransactionCurrencyCode, line.CostCenterId, reason), null, line.FunctionalCredit, line.FunctionalDebit, FinanceJournalAmountAuthority.ManualTransactionCurrency)); } FinanceMonetaryEvidence? reversalEvidence = null; var originalEvidence = await db.JournalMonetaryEvidence.AsNoTracking().SingleOrDefaultAsync(item => item.JournalId == original.Id, cancellationToken); if (originalEvidence is not null) { var parsed = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(originalEvidence.MonetaryEvidenceJson); if (parsed is null) return (false, "reporting_evidence_invalid", null); reversalEvidence = FinanceJournalMonetaryEvidenceFactory.Negate(parsed); } db.Journals.Add(reversal); if (reversalEvidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), reversal.Id, original.CompanyId, null, reversalEvidence, DateTimeOffset.UtcNow)); original.LinkReversal(reversal.Id); original.SetStatus(FinanceJournalStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow); return (true, "succeeded", ToJournal(reversal));
    }

    private static IQueryable<FinanceAllocationEntity> EffectiveAllocations(FinanceDbContext db, DateOnly? asOfDate = null)
    {
        var query = db.Allocations.Where(item => item.Status == FinanceAllocationStatus.Active && item.ReversalOfAllocationId == null);
        if (asOfDate is { } asOf)
        {
            query = query.Where(item => item.AllocationDate <= asOf);
        }

        if (asOfDate is { } cutoff)
        {
            return query.Where(item => !db.Allocations.Any(reversal => reversal.ReversalOfAllocationId == item.Id
                && reversal.Status == FinanceAllocationStatus.Reversed
                && reversal.AllocationDate <= cutoff));
        }

        return query.Where(item => !db.Allocations.Any(reversal => reversal.ReversalOfAllocationId == item.Id
            && reversal.Status == FinanceAllocationStatus.Reversed));
    }

    private static Guid? ResolveControlAccountId(FinanceOpenItemKind kind, FinanceJournalEntity? recognitionJournal)
    {
        if (recognitionJournal is null) return null;
        var controlLines = kind == FinanceOpenItemKind.Payable
            ? recognitionJournal.Lines.Where(line => line.FunctionalCredit > 0m).ToArray()
            : recognitionJournal.Lines.Where(line => line.FunctionalDebit > 0m).ToArray();
        return controlLines.Length == 1 ? controlLines[0].AccountId : null;
    }

    private static decimal ControlEffect(FinanceOpenItemKind kind, FinanceJournalEntity journal, Guid controlAccountId) =>
        journal.Lines.Where(line => line.AccountId == controlAccountId).Sum(line => kind == FinanceOpenItemKind.Payable
            ? line.FunctionalCredit - line.FunctionalDebit
            : line.FunctionalDebit - line.FunctionalCredit);

    internal static bool IsSettlementEffectEffective(
        FinanceSettlementDocumentEntity document,
        IReadOnlyDictionary<Guid, FinanceJournalEntity> journals,
        DateOnly asOf)
    {
        if (document.PostedJournalId is not { } postedId || !journals.TryGetValue(postedId, out var postedJournal) || postedJournal.PostingDate > asOf) return false;
        return document.ReversalJournalId is not { } reversalId
            || !journals.TryGetValue(reversalId, out var reversalJournal)
            || reversalJournal.PostingDate > asOf;
    }

    private IQueryable<FinanceAllocationEntity> ActiveAllocations(FinanceDbContext db) => EffectiveAllocations(db);
    private async Task<bool> SupplierIsActiveAsync(FinanceRequestContext context, Guid id, CancellationToken cancellationToken) { var value = await suppliers.FindSupplierAsync(context.TenantContext, id, cancellationToken); return value is not null && value.TenantId.Value == context.TenantId.Value && value.LifecycleState == MasterDataLifecycleState.Active; }
    private async Task<bool> CustomerIsActiveAsync(FinanceRequestContext context, Guid id, CancellationToken cancellationToken) { var value = await customers.FindCustomerReferenceAsync(context.TenantContext, id, cancellationToken); return value is not null && value.TenantId.Value == context.TenantId.Value && value.LifecycleState == MasterDataLifecycleState.Active; }
    private FinanceCompanyOption? Company(FinanceRequestContext context, Guid id) { var matches = companies.List(context.TenantId).Where(item => item.CompanyId == id && item.IsActive).ToArray(); if (matches.Length == 0 || matches.Select(item => NormalizeCode(item.FunctionalCurrencyCode)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) return null; if (context.TenantContext.Scope is { } scope) { var value = scope.Value; if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase) && (!Guid.TryParse(value["Company:".Length..], out var scoped) || scoped != id)) return null; if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase) && (!Guid.TryParse(value["Branch:".Length..], out var branch) || !matches.Any(item => item.BranchId == branch))) return null; } return matches[0] with { FunctionalCurrencyCode = NormalizeCode(matches[0].FunctionalCurrencyCode)! }; }
    private async Task<IReadOnlyList<FinanceOpenItemRecord>> ToOpenItemsAsync(FinanceDbContext db, IEnumerable<FinanceOpenItemEntity> entities, CancellationToken cancellationToken) { var result = new List<FinanceOpenItemRecord>(); foreach (var entity in entities) result.Add(await ToOpenItemAsync(db, entity, cancellationToken)); return result; }
    private async Task<FinanceOpenItemRecord> ToOpenItemAsync(FinanceDbContext db, FinanceOpenItemEntity entity, CancellationToken cancellationToken) { var allocated = await EffectiveAllocations(db).Where(item => item.OpenItemId == entity.Id).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m; var credited = await db.CustomerCreditApplications.Where(item => item.OpenItemId == entity.Id && !item.Reversed).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m; var outstanding = Math.Max(0m, entity.OriginalAmount - allocated - credited); var status = entity.RecognitionState != FinanceOpenItemRecognitionState.Recognized ? FinanceOpenItemStatus.OnHold : outstanding == 0m ? FinanceOpenItemStatus.Settled : allocated + credited > 0m ? FinanceOpenItemStatus.PartiallySettled : FinanceOpenItemStatus.Open; var term = entity.PaymentTermId is { } id ? new FinancePaymentTermSnapshotRecord(id, entity.PaymentTermCode ?? string.Empty, entity.PaymentTermEnglishName, entity.PaymentTermArabicName, entity.PaymentTermVersionNumber ?? 0, entity.PaymentTermVersionId ?? Guid.Empty, entity.PaymentTermEffectiveOn ?? entity.DocumentDate, entity.DueDate) : null; return new(entity.Id, entity.TenantId.Value, entity.CompanyId, entity.Kind, entity.SupplierId, entity.CustomerId, entity.SourceContract, entity.SourceDocumentId, entity.SourceDocumentVersion, entity.SourceEvidenceId, entity.SourceEvidenceVersion, entity.Reference, entity.DocumentDate, entity.DueDate, entity.CurrencyCode, entity.OriginalAmount, entity.FunctionalCurrencyCode, entity.OriginalFunctionalAmount, entity.ExchangeRate, entity.ExchangeRateId, entity.ExchangeRateVersionId, entity.ExchangeRateVersionNumber, term, entity.MatchEvidenceId, entity.MatchEvidenceVersion, entity.RecognitionState, entity.RecognitionJournalId, allocated, outstanding, status, entity.Version); }
    private async Task<IReadOnlyList<FinanceSettlementDocumentRecord>> ToDocumentsAsync(FinanceDbContext db, IEnumerable<FinanceSettlementDocumentEntity> entities, CancellationToken cancellationToken) { var result = new List<FinanceSettlementDocumentRecord>(); foreach (var entity in entities) result.Add(await ToDocumentAsync(db, entity, cancellationToken)); return result; }
    private async Task<FinanceSettlementDocumentRecord> ToDocumentAsync(FinanceDbContext db, FinanceSettlementDocumentEntity entity, CancellationToken cancellationToken) { var allocated = await ActiveAllocations(db).Where(item => item.SettlementDocumentId == entity.Id).SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m; return new(entity.Id, entity.TenantId.Value, entity.CompanyId, entity.Status, entity.Direction, entity.SupplierId, entity.CustomerId, entity.CashAccountId, entity.PaymentMethodId, entity.DocumentDate, entity.CurrencyCode, entity.Amount, entity.FunctionalCurrencyCode, entity.FunctionalAmount, entity.ExchangeRate, entity.ExchangeRateId, entity.ExchangeRateVersionId, entity.ExchangeRateVersionNumber, entity.ExternalReference, entity.Description, entity.CreatedBy, entity.SubmittedBy, entity.ApprovedBy, entity.PostedBy, entity.ReversedBy, entity.PostedJournalId, entity.ReversalJournalId, allocated, Math.Max(0m, entity.Amount - allocated), entity.Version, SourceApprovalPolicy.Resolve(ContractFor(entity.Direction), "on-account")); }
    private static FinancePaymentMethodRecord ToMethod(FinancePaymentMethodEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.Code, item.EnglishName, item.ArabicName, item.Direction, item.Lifecycle, item.IsManual, item.RequiresReference, item.EffectiveFrom, item.EffectiveTo, item.Version);
    private static FinanceCashAccountRecord ToCash(FinanceCashAccountEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.Code, item.EnglishName, item.ArabicName, item.Kind, item.CurrencyCode, item.LinkedAccountId, item.LinkedAccountCode, item.BankReference, item.Lifecycle, item.EffectiveFrom, item.EffectiveTo, item.Version);
    private static FinanceAllocationRecord ToAllocation(FinanceAllocationEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.SettlementDocumentId, item.OpenItemId, item.Amount, item.CurrencyCode, item.FunctionalAmount, item.AllocationDate, item.Status, item.ReversalOfAllocationId, item.JournalId, item.CreatedBy, item.Reason, item.Version, item.HistoricalFunctionalAmount, item.SettlementFunctionalAmount, item.RealizedFxAmount, item.RealizedFxDirection, item.RealizedFxJournalId, item.RealizedFxRuleId, item.RealizedFxRuleVersionNumber);
    private static FinanceJournalRecord ToJournal(FinanceJournalEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.JournalSequence, item.JournalNumber, item.JournalDate, item.PostingDate, item.FiscalYearId, item.FiscalPeriodId, item.FunctionalCurrencyCode, item.TransactionCurrencyCode, item.ExchangeRate, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.SourceContract, item.SourceEvent, item.SourceEvidenceId, item.SourceEvidenceVersion, item.PostingRuleId, item.PostingRuleVersionNumber, item.Description, item.Status, item.CreatedBy, item.SubmittedBy, item.ApprovedBy, item.PostedBy, item.ReversedBy, item.ReversalOfJournalId, item.ReversalJournalId, item.CorrelationId, item.CreatedAt, item.PostedAt, item.Lines.OrderBy(line => line.LineNumber).Select(line => new FinanceJournalLineRecord(line.Id, line.LineNumber, line.AccountId, line.AccountCode, line.AccountName, line.Debit, line.Credit, line.FunctionalDebit, line.FunctionalCredit, line.TransactionAmount, line.TransactionCurrencyCode, line.CostCenterId, line.CostCenterCode, line.Description)).ToArray(), item.Version, item.AmountAuthority, item.ApprovalRequirement);
    private void AddAudit(FinanceDbContext db, FinanceRequestContext context, string operation, string resourceType, Guid resourceId, string result, string? reason, string? key, DateTimeOffset at) => db.AuditEvents.Add(new FinanceAuditEntity(context.TenantId, Guid.NewGuid(), operation, resourceType, resourceId, context.ActorId, context.SessionId, result, reason, context.CorrelationId, key, at));
    private static async Task<FinanceOperationResult<T>?> ReadReplayAsync<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, CancellationToken cancellationToken) { var item = await db.Idempotency.SingleOrDefaultAsync(value => value.ActorId == context.ActorId && value.OperationId == operation && value.Key == key, cancellationToken); if (item is null) return null; if (item.Fingerprint != fingerprint) return FinanceOperationResult<T>.Failure("idempotency_conflict"); return FinanceOperationResult<T>.Success(JsonSerializer.Deserialize<T>(item.SnapshotJson)!); }
    private void AddReplay<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, string resourceType, Guid resourceId, T value, DateTimeOffset at) => db.Idempotency.Add(new FinanceIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, key, fingerprint, resourceType, resourceId, JsonSerializer.Serialize(value), at));
    private static FinanceOperationResult<T> Failure<T>(string code) => FinanceOperationResult<T>.Failure(code);
    private static bool ValidText(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static bool MethodApplies(FinancePaymentMethodDirection method, FinancePaymentMethodDirection document) => method == FinancePaymentMethodDirection.Both || method == document;
    private static bool SameAmount(decimal left, decimal right) => Math.Abs(left - right) <= 0.00000001m;
    private static string ContractFor(FinancePaymentMethodDirection direction) => direction == FinancePaymentMethodDirection.Payment ? "supplier-payment.v1" : "customer-receipt.v1";
    private static bool AllowedDocumentTransition(FinanceSettlementDocumentStatus from, FinanceSettlementDocumentStatus to) => (from, to) switch { (FinanceSettlementDocumentStatus.Draft, FinanceSettlementDocumentStatus.Submitted or FinanceSettlementDocumentStatus.Cancelled) => true, (FinanceSettlementDocumentStatus.Submitted, FinanceSettlementDocumentStatus.Approved or FinanceSettlementDocumentStatus.Rejected or FinanceSettlementDocumentStatus.Cancelled) => true, (FinanceSettlementDocumentStatus.Approved, FinanceSettlementDocumentStatus.Cancelled) => true, (FinanceSettlementDocumentStatus.Rejected, FinanceSettlementDocumentStatus.Draft or FinanceSettlementDocumentStatus.Cancelled) => true, _ => false };
    private static DateOnly AddOffset(DateOnly date, MasterDataPaymentTermOffset offset) => date.AddMonths(offset.Months).AddDays(offset.Days);
}

#pragma warning restore CS1591
