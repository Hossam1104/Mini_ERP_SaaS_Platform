#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.App.Modules.Inventory;

public interface IInventoryCustomerReturnPersistence
{
    Task<InventoryCustomerReturnResponse?> GetAsync(InventoryRequestContext context, Guid salesCustomerReturnId, CancellationToken cancellationToken = default);
    Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReceiveAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, InventoryCustomerReturnReceiptRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryOperationResult<InventoryCustomerReturnResponse>> InspectAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, InventoryCustomerReturnInspectionRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReverseAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
}

public sealed class UnavailableInventoryCustomerReturnPersistence : IInventoryCustomerReturnPersistence
{
    public Task<InventoryCustomerReturnResponse?> GetAsync(InventoryRequestContext c, Guid id, CancellationToken x = default) => Task.FromResult<InventoryCustomerReturnResponse?>(null);
    public Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReceiveAsync(InventoryRequestContext c, Guid id, byte[] v, InventoryCustomerReturnReceiptRequest r, string? k, string f, CancellationToken x = default) => Task.FromResult(InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("inventory_customer_return_persistence_unavailable"));
    public Task<InventoryOperationResult<InventoryCustomerReturnResponse>> InspectAsync(InventoryRequestContext c, Guid id, byte[] v, InventoryCustomerReturnInspectionRequest r, string? k, string f, CancellationToken x = default) => Task.FromResult(InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("inventory_customer_return_persistence_unavailable"));
    public Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReverseAsync(InventoryRequestContext c, Guid id, byte[] v, string? k, string f, CancellationToken x = default) => Task.FromResult(InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("inventory_customer_return_persistence_unavailable"));
}

public sealed class InventoryCustomerReturnService(
    IInventoryCustomerReturnPersistence persistence,
    InventoryResourceAuthorizationService authorization,
    ISalesCustomerReturnSourceProvider salesReturns)
{
    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReceiveAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryCustomerReturnReceiptRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || expectedVersion is null || expectedVersion.Length == 0 || request is null || request.ReceiptDate == default || string.IsNullOrWhiteSpace(request.PhysicalEvidenceReference) || request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(item => item.OrderLineId == Guid.Empty || item.Quantity <= 0m) || request.Lines.Select(item => item.OrderLineId).Distinct().Count() != request.Lines.Count) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("validation_failed");
        var source = await salesReturns.GetCustomerReturnSourceAsync(context.TenantContext, id, cancellationToken);
        if (source is null || source.Status is not (SalesCustomerReturnStatus.AwaitingReceipt or SalesCustomerReturnStatus.PartiallyReceived or SalesCustomerReturnStatus.ReconciliationRequired or SalesCustomerReturnStatus.Unknown)) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("sales_return_not_awaiting_receipt");
        if (source.Version is null || !source.Version.SequenceEqual(expectedVersion)) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("concurrency_conflict");
        if (!authorization.IsAllowed(context, "inventory.customer-return.receive", new InventoryScope(source.TenantId, source.CompanyId, source.BranchId, source.WarehouseId))) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("permission_denied");
        foreach (var line in request.Lines)
        {
            var sourceLine = source.Lines.SingleOrDefault(item => item.OrderLineId == line.OrderLineId);
            if (sourceLine is null || line.Quantity > sourceLine.ReturnQuantity) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("return_quantity_conflict");
        }
        return await persistence.ReceiveAsync(context, id, expectedVersion, request, Normalize(idempotencyKey), Fingerprint(request), cancellationToken);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> InspectAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryCustomerReturnInspectionRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || expectedVersion is null || expectedVersion.Length == 0 || request is null || string.IsNullOrWhiteSpace(request.InspectionEvidenceReference) || request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(item => item.OrderLineId == Guid.Empty || item.Quantity <= 0m) || request.Lines.Select(item => item.OrderLineId).Distinct().Count() != request.Lines.Count) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("validation_failed");
        var value = await persistence.GetAsync(context, id, cancellationToken);
        if (value is null) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("inventory_customer_return_not_found");
        if (!authorization.IsAllowed(context, "inventory.customer-return.inspect", new InventoryScope(value.TenantId, value.CompanyId, value.BranchId, value.WarehouseId))) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("permission_denied");
        return await persistence.InspectAsync(context, id, expectedVersion, request, Normalize(idempotencyKey), Fingerprint(request), cancellationToken);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReverseAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || expectedVersion is null || expectedVersion.Length == 0) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("validation_failed");
        var value = await persistence.GetAsync(context, id, cancellationToken);
        if (value is null) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("inventory_customer_return_not_found");
        if (!authorization.IsAllowed(context, "inventory.customer-return.reverse", new InventoryScope(value.TenantId, value.CompanyId, value.BranchId, value.WarehouseId))) return InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("permission_denied");
        return await persistence.ReverseAsync(context, id, expectedVersion, Normalize(idempotencyKey), Fingerprint(new { id, expectedVersion }), cancellationToken);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> GetAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        var value = await persistence.GetAsync(context, id, cancellationToken);
        return value is null ? InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("inventory_customer_return_not_found") : authorization.IsAllowed(context, "inventory.customer-return.read", new InventoryScope(value.TenantId, value.CompanyId, value.BranchId, value.WarehouseId)) ? InventoryOperationResult<InventoryCustomerReturnResponse>.Success(value) : InventoryOperationResult<InventoryCustomerReturnResponse>.Failure("permission_denied");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}

#pragma warning restore CS1591
