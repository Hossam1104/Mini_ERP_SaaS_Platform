#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.App.Modules.Finance;

public enum FinanceCreditNoteMutation { Submit = 1, Approve = 2, Reject = 3, Cancel = 4, Post = 5, Reverse = 6 }

public interface IFinanceCustomerReturnPersistence
{
    Task<FinanceCreditNoteResponse?> GetAsync(FinanceRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceCreditNoteResponse>> CreateAsync(FinanceRequestContext context, FinanceCreditNoteCreateRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceCreditNoteResponse>> MutateAsync(FinanceRequestContext context, Guid id, byte[] expectedVersion, FinanceCreditNoteMutation action, string? reason, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
}

public sealed class UnavailableFinanceCustomerReturnPersistence : IFinanceCustomerReturnPersistence
{
    public Task<FinanceCreditNoteResponse?> GetAsync(FinanceRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<FinanceCreditNoteResponse?>(null);
    public Task<FinanceOperationResult<FinanceCreditNoteResponse>> CreateAsync(FinanceRequestContext c, FinanceCreditNoteCreateRequest r, string? k, string f, CancellationToken x = default) => Task.FromResult(FinanceOperationResult<FinanceCreditNoteResponse>.Failure("finance_customer_return_persistence_unavailable"));
    public Task<FinanceOperationResult<FinanceCreditNoteResponse>> MutateAsync(FinanceRequestContext c, Guid id, byte[] v, FinanceCreditNoteMutation a, string? reason, string? k, string f, CancellationToken x = default) => Task.FromResult(FinanceOperationResult<FinanceCreditNoteResponse>.Failure("finance_customer_return_persistence_unavailable"));
}

public sealed class FinanceCustomerReturnService(
    IFinanceCustomerReturnPersistence persistence,
    FinanceAuthorizationService authorization,
    ISalesCustomerReturnSourceProvider salesReturns)
{
    public async Task<FinanceOperationResult<FinanceCreditNoteResponse>> GetAsync(FinanceRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        var value = await persistence.GetAsync(context, id, cancellationToken);
        return value is null ? FinanceOperationResult<FinanceCreditNoteResponse>.Failure("credit_note_not_found") : authorization.Authorize(context, "finance.credit-note.read", value.CompanyId).Allowed ? FinanceOperationResult<FinanceCreditNoteResponse>.Success(value) : FinanceOperationResult<FinanceCreditNoteResponse>.Failure("permission_denied");
    }

    public async Task<FinanceOperationResult<FinanceCreditNoteResponse>> CreateAsync(FinanceRequestContext context, FinanceCreditNoteCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request is null || request.SalesCustomerReturnId == Guid.Empty || request.CreditNoteDate == default) return FinanceOperationResult<FinanceCreditNoteResponse>.Failure("validation_failed");
        var source = await salesReturns.GetCustomerReturnSourceAsync(context.TenantContext, request.SalesCustomerReturnId, cancellationToken);
        if (source is null || source.Consequence != SalesCustomerReturnConsequence.CreditNote || source.Status is not (SalesCustomerReturnStatus.Received or SalesCustomerReturnStatus.Completed) || source.Lines.Sum(item => item.CommerciallyAcceptedQuantity) <= 0m || source.Lines.Sum(item => item.InspectedQuantity) < source.Lines.Sum(item => item.CommerciallyAcceptedQuantity)) return FinanceOperationResult<FinanceCreditNoteResponse>.Failure("sales_return_not_creditable");
        if (source.InvoiceAllocations is null || source.InvoiceAllocations.Count == 0) return FinanceOperationResult<FinanceCreditNoteResponse>.Failure("recognized_invoice_required");
        var auth = authorization.Authorize(context, "finance.credit-note.create", source.CompanyId);
        if (!auth.Allowed) return FinanceOperationResult<FinanceCreditNoteResponse>.Failure(auth.Code);
        return await persistence.CreateAsync(context, request, Normalize(idempotencyKey), Fingerprint(request), cancellationToken);
    }

    public async Task<FinanceOperationResult<FinanceCreditNoteResponse>> MutateAsync(FinanceRequestContext context, Guid id, byte[] expectedVersion, FinanceCreditNoteMutation action, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var value = await persistence.GetAsync(context, id, cancellationToken);
        if (value is null) return FinanceOperationResult<FinanceCreditNoteResponse>.Failure("credit_note_not_found");
        if (expectedVersion is null || expectedVersion.Length == 0) return FinanceOperationResult<FinanceCreditNoteResponse>.Failure("concurrency_conflict");
        var operation = "finance.credit-note." + action.ToString().ToLowerInvariant();
        var auth = authorization.Authorize(context, operation, value.CompanyId);
        if (!auth.Allowed) return FinanceOperationResult<FinanceCreditNoteResponse>.Failure(auth.Code);
        return await persistence.MutateAsync(context, id, expectedVersion, action, Normalize(reason), Normalize(idempotencyKey), Fingerprint(new { id, action, reason }), cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}

#pragma warning restore CS1591
