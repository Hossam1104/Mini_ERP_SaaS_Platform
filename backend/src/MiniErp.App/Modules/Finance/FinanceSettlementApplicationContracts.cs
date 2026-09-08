#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Finance;

public sealed record FinancePaymentMethodCommand(
    Guid CompanyId,
    string Code,
    string EnglishName,
    string? ArabicName,
    FinancePaymentMethodDirection Direction,
    bool IsManual,
    bool RequiresReference,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid Id,
    byte[]? ExpectedVersion,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceCashAccountCommand(
    Guid CompanyId,
    string Code,
    string EnglishName,
    string? ArabicName,
    FinanceCashAccountKind Kind,
    string CurrencyCode,
    Guid LinkedAccountId,
    string? BankReference,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid Id,
    byte[]? ExpectedVersion,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceManualReceivableCommand(
    Guid CompanyId,
    Guid CustomerId,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    Guid? PaymentTermId,
    string CurrencyCode,
    decimal Amount,
    decimal? FunctionalAmount,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    string? Reference,
    string? Description,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceSalesInvoiceCommand(
    Guid CompanyId,
    Guid CustomerId,
    Guid SalesOrderId,
    int SalesOrderRevision,
    Guid InvoiceRequestId,
    DateOnly DocumentDate,
    Guid PaymentTermId,
    string CurrencyCode,
    decimal Amount,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    string SourceSnapshot,
    string? Reference,
    string IdempotencyKey,
    string RequestFingerprint,
    decimal? NetAmount = null,
    decimal? TaxAmount = null,
    IReadOnlyList<FinanceSalesInvoiceLine>? Lines = null,
    FinancePaymentTermSnapshotRecord? PaymentTerm = null);

public sealed record FinanceSalesInvoiceLine(
    Guid OrderLineId,
    decimal Quantity,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    Guid? TaxId,
    string? TaxCode,
    Guid? TaxRateVersionId,
    int? TaxRateVersionNumber,
    DateOnly? TaxEffectiveFrom,
    DateOnly? TaxEffectiveTo,
    decimal? TaxRatePercentage,
    decimal? TaxableBase,
    string? TaxReferenceValue);

public sealed record FinanceSalesInvoiceEligibilityRecord(
    bool Eligible,
    string Code,
    decimal Amount,
    string CurrencyCode,
    Guid SalesOrderId,
    int SalesOrderRevision,
    DateOnly DocumentDate,
    FinancePaymentTermSnapshotRecord? PaymentTerm,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber);

public sealed record FinanceSupplierInvoiceRecognitionCommand(
    Guid SourceEvidenceId,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceSettlementDocumentCommand(
    FinancePaymentMethodDirection Direction,
    Guid CompanyId,
    Guid? SupplierId,
    Guid? CustomerId,
    Guid CashAccountId,
    Guid PaymentMethodId,
    DateOnly DocumentDate,
    string CurrencyCode,
    decimal Amount,
    decimal? FunctionalAmount,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    string? ExternalReference,
    string? Description,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceSettlementActionCommand(
    Guid DocumentId,
    byte[] ExpectedVersion,
    string? Reason,
    string IdempotencyKey,
    string RequestFingerprint,
    FinancePaymentMethodDirection? ExpectedDirection = null);

public sealed record FinanceSettlementReversalCommand(
    Guid DocumentId,
    DateOnly PostingDate,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint,
    FinancePaymentMethodDirection? ExpectedDirection = null);

public sealed record FinanceAllocationCommand(
    Guid SettlementDocumentId,
    Guid OpenItemId,
    decimal Amount,
    DateOnly AllocationDate,
    string? Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceAllocationReversalCommand(
    Guid AllocationId,
    byte[] ExpectedVersion,
    string Reason,
    Guid Id,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceSettlementQuery(Guid CompanyId, FinancePaymentMethodDirection? Direction = null);

public sealed record FinanceCashMovementReportingRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    FinanceSettlementDocumentStatus Status,
    FinancePaymentMethodDirection Direction,
    Guid CashAccountId,
    Guid PaymentMethodId,
    DateOnly DocumentDate,
    string CurrencyCode,
    decimal Amount,
    string FunctionalCurrencyCode,
    decimal FunctionalAmount,
    string? ExternalReference,
    string? Description,
    Guid? PostedJournalId,
    Guid? ReversalJournalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    byte[] Version);

public interface IFinanceSettlementReportingReadPort
{
    Task<ReportingSourcePage<FinanceCashMovementReportingRecord>> ListCashMovementReportingPageAsync(
        FinanceRequestContext context,
        Guid companyId,
        DateOnly? fromDate,
        DateOnly? toDate,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);
}

public sealed record FinanceAgingQuery(Guid CompanyId, DateOnly AsOfDate, FinanceOpenItemKind? Kind = null, Guid? PartyId = null);

public sealed record FinanceExposureQuery(Guid CompanyId, Guid CustomerId, DateOnly AsOfDate);

public sealed record FinanceSupplierInvoiceSourceRecord(
    Guid TenantId,
    Guid CompanyId,
    Guid SupplierId,
    string SourceContract,
    Guid SourceDocumentId,
    int SourceDocumentVersion,
    Guid SourceEvidenceId,
    int SourceEvidenceVersion,
    string? Reference,
    DateOnly DocumentDate,
    string CurrencyCode,
    decimal Amount,
    string FunctionalCurrencyCode,
    decimal FunctionalAmount,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    FinancePaymentTermSnapshotRecord? PaymentTerm,
    DateOnly? DueDate,
    Guid MatchEvidenceId,
    int MatchEvidenceVersion,
    string? SourceSnapshot,
    string CorrelationId,
    string? SupplierCode = null,
    string? SupplierName = null,
    PurchaseInvoiceMatchResult MatchResult = PurchaseInvoiceMatchResult.ExactMatch,
    string? DeclaredCurrencyCode = null,
    DateOnly? DeclaredInvoiceDate = null,
    decimal? DeclaredTaxAmount = null,
    IReadOnlyList<FinanceSupplierDeclaredTaxRecord>? DeclaredTaxes = null);

public sealed record FinanceSupplierDeclaredTaxRecord(
    string? TaxCode,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    decimal? TaxableBase);

public sealed record FinanceApSourceReadyRecord(
    Guid SourceEvidenceId,
    Guid CompanyId,
    Guid SupplierId,
    string? SupplierCode,
    string? SupplierName,
    string? SupplierInvoiceReference,
    DateOnly InvoiceDate,
    string CurrencyCode,
    decimal Amount,
    DateOnly DueDate,
    FinancePaymentTermSnapshotRecord PaymentTerm,
    PurchaseInvoiceMatchResult MatchResult,
    bool AlreadyRecognized,
    int SourceEvidenceVersion);

public interface IFinanceSupplierInvoiceSourceProvider
{
    Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>> ListAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>>([]);
}

public sealed class UnavailableFinanceSupplierInvoiceSourceProvider : IFinanceSupplierInvoiceSourceProvider
{
    public Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default) =>
        Task.FromResult<FinanceSupplierInvoiceSourceRecord?>(null);

    public Task<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>> ListAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>>([]);
}

public interface IFinanceSettlementPersistence
{
    Task<IReadOnlyList<FinancePaymentMethodRecord>> ListPaymentMethodsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinancePaymentMethodRecord>> CreatePaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinancePaymentMethodRecord>> EditPaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinancePaymentMethodRecord>> SetPaymentMethodLifecycleAsync(FinanceRequestContext context, Guid methodId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceCashAccountRecord>> ListCashAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceCashAccountRecord>> CreateCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceCashAccountRecord>> EditCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceCashAccountRecord>> SetCashAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, string resource, Guid resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceOpenItemRecord>> ListOpenItemsAsync(FinanceRequestContext context, FinanceOpenItemKind kind, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOpenItemRecord?> GetOpenItemAsync(FinanceRequestContext context, Guid itemId, FinanceOpenItemKind? expectedKind = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceApSourceReadyRecord>> ListApSourceReadyAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceOpenItemRecord>> RecognizeSupplierInvoiceAsync(FinanceRequestContext context, FinanceSupplierInvoiceRecognitionCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateManualReceivableAsync(FinanceRequestContext context, FinanceManualReceivableCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>> EvaluateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceSettlementDocumentRecord>> ListSettlementDocumentsAsync(FinanceRequestContext context, FinanceSettlementQuery query, CancellationToken cancellationToken = default);
    Task<FinanceSettlementDocumentRecord?> GetSettlementDocumentAsync(FinanceRequestContext context, Guid documentId, FinancePaymentMethodDirection? expectedDirection = null, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> CreateSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> EditSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> TransitionSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, FinanceSettlementDocumentStatus target, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> PostSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> ReverseSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementReversalCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceAllocationRecord>> ListAllocationsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceAllocationRecord>> CreateAllocationAsync(FinanceRequestContext context, FinanceAllocationCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceAllocationRecord>> ReverseAllocationAsync(FinanceRequestContext context, FinanceAllocationReversalCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceAgingRecord>> GetAgingAsync(FinanceRequestContext context, FinanceAgingQuery query, CancellationToken cancellationToken = default);
    Task<FinanceCustomerExposureRecord?> GetExposureAsync(FinanceRequestContext context, FinanceExposureQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}

public sealed class UnavailableFinanceSettlementPersistence : IFinanceSettlementPersistence, IFinanceSettlementReportingReadPort
{
    private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
    private static Task<IReadOnlyList<T>> EmptyList<T>() => Task.FromResult<IReadOnlyList<T>>([]);
    private static FinanceOperationResult<T> Failure<T>() => FinanceOperationResult<T>.Failure("finance_unavailable");
    public Task<IReadOnlyList<FinancePaymentMethodRecord>> ListPaymentMethodsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyList<FinancePaymentMethodRecord>();
    public Task<FinanceOperationResult<FinancePaymentMethodRecord>> CreatePaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinancePaymentMethodRecord>());
    public Task<FinanceOperationResult<FinancePaymentMethodRecord>> EditPaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinancePaymentMethodRecord>());
    public Task<FinanceOperationResult<FinancePaymentMethodRecord>> SetPaymentMethodLifecycleAsync(FinanceRequestContext context, Guid methodId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinancePaymentMethodRecord>());
    public Task<IReadOnlyList<FinanceCashAccountRecord>> ListCashAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyList<FinanceCashAccountRecord>();
    public Task<FinanceOperationResult<FinanceCashAccountRecord>> CreateCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceCashAccountRecord>());
    public Task<FinanceOperationResult<FinanceCashAccountRecord>> EditCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceCashAccountRecord>());
    public Task<FinanceOperationResult<FinanceCashAccountRecord>> SetCashAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceCashAccountRecord>());
    public Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, string resource, Guid resourceId, CancellationToken cancellationToken = default) => Empty<Guid?>();
    public Task<IReadOnlyList<FinanceOpenItemRecord>> ListOpenItemsAsync(FinanceRequestContext context, FinanceOpenItemKind kind, Guid companyId, CancellationToken cancellationToken = default) => EmptyList<FinanceOpenItemRecord>();
    public Task<FinanceOpenItemRecord?> GetOpenItemAsync(FinanceRequestContext context, Guid itemId, FinanceOpenItemKind? expectedKind = null, CancellationToken cancellationToken = default) => Empty<FinanceOpenItemRecord?>();
    public Task<IReadOnlyList<FinanceApSourceReadyRecord>> ListApSourceReadyAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) => EmptyList<FinanceApSourceReadyRecord>();
    public Task<FinanceOperationResult<FinanceOpenItemRecord>> RecognizeSupplierInvoiceAsync(FinanceRequestContext context, FinanceSupplierInvoiceRecognitionCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceOpenItemRecord>());
    public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateManualReceivableAsync(FinanceRequestContext context, FinanceManualReceivableCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceOpenItemRecord>());
    public Task<FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>> EvaluateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceSalesInvoiceEligibilityRecord>());
    public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceOpenItemRecord>());
    public Task<IReadOnlyList<FinanceSettlementDocumentRecord>> ListSettlementDocumentsAsync(FinanceRequestContext context, FinanceSettlementQuery query, CancellationToken cancellationToken = default) => EmptyList<FinanceSettlementDocumentRecord>();
    public Task<FinanceSettlementDocumentRecord?> GetSettlementDocumentAsync(FinanceRequestContext context, Guid documentId, FinancePaymentMethodDirection? expectedDirection = null, CancellationToken cancellationToken = default) => Empty<FinanceSettlementDocumentRecord?>();
    public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> CreateSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceSettlementDocumentRecord>());
    public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> EditSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceSettlementDocumentRecord>());
    public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> TransitionSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, FinanceSettlementDocumentStatus target, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceSettlementDocumentRecord>());
    public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> PostSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceSettlementDocumentRecord>());
    public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> ReverseSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementReversalCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceSettlementDocumentRecord>());
    public Task<IReadOnlyList<FinanceAllocationRecord>> ListAllocationsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyList<FinanceAllocationRecord>();
    public Task<FinanceOperationResult<FinanceAllocationRecord>> CreateAllocationAsync(FinanceRequestContext context, FinanceAllocationCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceAllocationRecord>());
    public Task<FinanceOperationResult<FinanceAllocationRecord>> ReverseAllocationAsync(FinanceRequestContext context, FinanceAllocationReversalCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Failure<FinanceAllocationRecord>());
    public Task<IReadOnlyList<FinanceAgingRecord>> GetAgingAsync(FinanceRequestContext context, FinanceAgingQuery query, CancellationToken cancellationToken = default) => EmptyList<FinanceAgingRecord>();
    public Task<FinanceCustomerExposureRecord?> GetExposureAsync(FinanceRequestContext context, FinanceExposureQuery query, CancellationToken cancellationToken = default) => Empty<FinanceCustomerExposureRecord?>();
    public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyList<FinanceReconciliationRecord>();
    public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default) => EmptyList<FinanceReconciliationRecord>();
    public Task<ReportingSourcePage<FinanceCashMovementReportingRecord>> ListCashMovementReportingPageAsync(FinanceRequestContext context, Guid companyId, DateOnly? fromDate, DateOnly? toDate, ReportingPageRequest page, CancellationToken cancellationToken = default) => throw new InvalidOperationException("finance_settlement_reporting_unavailable");
}

#pragma warning restore CS1591
