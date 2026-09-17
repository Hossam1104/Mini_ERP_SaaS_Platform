#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Inventory;

public sealed record InventoryOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static InventoryOperationResult<T> Success(T value) => new(true, "succeeded", value);

    public static InventoryOperationResult<T> Failure(string code) => new(false, code, default);
}

public enum InventoryPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public sealed record InventoryPersistenceResult<T>(InventoryPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == InventoryPersistenceOutcome.Succeeded;

    public static InventoryPersistenceResult<T> Success(T value) =>
        new(InventoryPersistenceOutcome.Succeeded, "persisted", value);

    public static InventoryPersistenceResult<T> Denied(InventoryPersistenceOutcome outcome, string code) =>
        new(outcome, code, default);
}

public sealed class InventoryRequestContext
{
    private InventoryRequestContext(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        ScopeReference? trustedScope,
        Guid actorId,
        Guid sessionId,
        CorrelationId? correlationId,
        FoundationRequestContext foundationContext)
    {
        TenantId = tenantId;
        AuthorizationPath = authorizationPath;
        TrustedScope = trustedScope;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        FoundationContext = foundationContext;
    }

    public TenantId TenantId { get; }
    public TenantAuthorizationPath AuthorizationPath { get; }
    public ScopeReference? TrustedScope { get; }
    public Guid ActorId { get; }
    public Guid SessionId { get; }
    public CorrelationId? CorrelationId { get; }
    internal FoundationRequestContext FoundationContext { get; }
    public TenantContext TenantContext => FoundationContext.TenantContext
        ?? throw new InvalidOperationException("An Inventory context must carry a trusted Tenant context.");

    internal static InventoryRequestContext FromFoundationContext(FoundationRequestContext foundationContext)
    {
        ArgumentNullException.ThrowIfNull(foundationContext);
        if (foundationContext.SecurityProfile is not (
                FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant)
            || foundationContext.TenantContext is null
            || foundationContext.PlatformGovernanceContext is not null
            || foundationContext.ActorId is not { } actorId
            || foundationContext.SessionId is not { } sessionId
            || actorId == Guid.Empty
            || sessionId == Guid.Empty)
        {
            throw new ArgumentException("A Tenant-bound Foundation context is required.", nameof(foundationContext));
        }

        var tenant = foundationContext.TenantContext;
        var expectedProfile = tenant.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationSecurityProfile.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationSecurityProfile.SupportGrant,
            _ => throw new ArgumentException("The Tenant authorization path is not supported.", nameof(foundationContext))
        };

        if (foundationContext.SecurityProfile != expectedProfile
            || tenant.ActorId is { } tenantActorId && tenantActorId != actorId)
        {
            throw new ArgumentException("The Foundation context contains inconsistent authorization facts.", nameof(foundationContext));
        }

        return new InventoryRequestContext(
            tenant.TenantId,
            tenant.AuthorizationPath,
            tenant.Scope,
            actorId,
            sessionId,
            tenant.CorrelationId,
            foundationContext);
    }
}

public sealed record InventoryContextResolution(bool Allowed, string Code, InventoryRequestContext? Context)
{
    public static InventoryContextResolution Success(InventoryRequestContext context) => new(true, "resolved", context);
    public static InventoryContextResolution Denied(string code) => new(false, code, null);
}

public sealed class InventoryTenantContextResolver
{
    public InventoryContextResolution Resolve(FoundationRequestContext trustedContext)
    {
        ArgumentNullException.ThrowIfNull(trustedContext);
        try
        {
            return InventoryContextResolution.Success(InventoryRequestContext.FromFoundationContext(trustedContext));
        }
        catch (ArgumentException)
        {
            return InventoryContextResolution.Denied("tenant_context_failed");
        }
    }
}

public sealed record InventoryScope(Guid TenantId, Guid CompanyId, Guid? BranchId, Guid WarehouseId)
{
    public string CanonicalReference => $"Warehouse:{WarehouseId:D}";
}

public sealed class InventoryResourceAuthorizationService
{
    public bool IsAllowed(InventoryRequestContext context, string operationId, InventoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operationId);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.TenantId != context.TenantId.Value
            || !FoundationOperationCatalog.TryGet(operationId, out var descriptor)
            || descriptor.SecurityProfile is not FoundationSecurityProfile.OrdinaryMembership
                and not FoundationSecurityProfile.SupportGrant
            || descriptor.ExactPermissionCode is null
            || context.AuthorizationPath != (descriptor.SecurityProfile == FoundationSecurityProfile.OrdinaryMembership
                ? TenantAuthorizationPath.OrdinaryMembership
                : TenantAuthorizationPath.SupportGrant)
            || !string.Equals(context.FoundationContext.Permission, descriptor.ExactPermissionCode, StringComparison.Ordinal))
        {
            return false;
        }

        return ScopeAllows(context.TrustedScope, scope);
    }

    internal bool IsMigrationAllowed(InventoryRequestContext context, InventoryScope scope) =>
        scope.TenantId == context.TenantId.Value
        && IsMigrationExecutionContext(context.FoundationContext)
        && ScopeAllows(context.TrustedScope, scope);

    public static bool IsMigrationExecutionContext(FoundationRequestContext? context) =>
        context is not null
        && context.SecurityProfile is FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant
        && context.TenantContext is { } tenant
        && context.PlatformGovernanceContext is null
        && context.ActorId is { } actorId
        && actorId != Guid.Empty
        && context.SessionId is { } sessionId
        && sessionId != Guid.Empty
        && tenant.AuthorizationPath is TenantAuthorizationPath.OrdinaryMembership or TenantAuthorizationPath.SupportGrant
        && (tenant.ActorId is null || tenant.ActorId == actorId)
        && string.Equals(context.Permission, FoundationOperationCatalog.GetRequired("migration.execution.start").ExactPermissionCode, StringComparison.Ordinal);

    public bool IsAllowed(InventoryRequestContext context, string operationId, Guid tenantId, Guid companyId, Guid? branchId, Guid warehouseId) =>
        IsAllowed(context, operationId, new InventoryScope(tenantId, companyId, branchId, warehouseId));

    public bool IsCompanyAllowed(InventoryRequestContext context, string operationId, Guid tenantId, Guid companyId, Guid? branchId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (tenantId != context.TenantId.Value
            || !FoundationOperationCatalog.TryGet(operationId, out var descriptor)
            || descriptor.SecurityProfile is not FoundationSecurityProfile.OrdinaryMembership
                and not FoundationSecurityProfile.SupportGrant
            || descriptor.ExactPermissionCode is null
            || context.AuthorizationPath != (descriptor.SecurityProfile == FoundationSecurityProfile.OrdinaryMembership
                ? TenantAuthorizationPath.OrdinaryMembership
                : TenantAuthorizationPath.SupportGrant)
            || !string.Equals(context.FoundationContext.Permission, descriptor.ExactPermissionCode, StringComparison.Ordinal))
        {
            return false;
        }

        return ScopeAllows(context.TrustedScope, new InventoryScope(tenantId, companyId, branchId, Guid.Empty), allowWarehouseScope: false);
    }

    private static bool ScopeAllows(ScopeReference? trustedScope, InventoryScope target, bool allowWarehouseScope = true)
    {
        if (trustedScope is null)
        {
            return true;
        }

        var value = trustedScope.Value.Value;
        if (value.Equals($"Tenant:{target.TenantId:D}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(value["Company:".Length..], out var companyId))
        {
            return companyId == target.CompanyId;
        }

        if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(value["Branch:".Length..], out var branchId))
        {
            return target.BranchId == branchId;
        }

        if (allowWarehouseScope && value.StartsWith("Warehouse:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(value["Warehouse:".Length..], out var warehouseId))
        {
            return target.WarehouseId == warehouseId;
        }

        return false;
    }
}

public interface IInventoryWarehouseProvider
{
    Task<IReadOnlyList<InventoryWarehouseOption>> ListAsync(
        InventoryRequestContext context,
        Guid? companyId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);

    Task<InventoryWarehouseOption?> FindAsync(
        InventoryRequestContext context,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventoryWarehouseProvider : IInventoryWarehouseProvider
{
    public Task<IReadOnlyList<InventoryWarehouseOption>> ListAsync(InventoryRequestContext context, Guid? companyId = null, Guid? branchId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InventoryWarehouseOption>>([]);

    public Task<InventoryWarehouseOption?> FindAsync(InventoryRequestContext context, Guid warehouseId, CancellationToken cancellationToken = default) =>
        Task.FromResult<InventoryWarehouseOption?>(null);
}

public sealed class ConfiguredInventoryWarehouseProvider : IInventoryWarehouseProvider
{
    private readonly IReadOnlyList<InventoryWarehouseOption> options;

    public ConfiguredInventoryWarehouseProvider(IEnumerable<InventoryWarehouseOption> options) =>
        this.options = options?.ToArray() ?? throw new ArgumentNullException(nameof(options));

    public Task<IReadOnlyList<InventoryWarehouseOption>> ListAsync(InventoryRequestContext context, Guid? companyId = null, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var result = options.Where(item => item.TenantId == context.TenantId.Value && item.IsActive);
        if (companyId.HasValue) result = result.Where(item => item.CompanyId == companyId.Value);
        if (branchId.HasValue) result = result.Where(item => item.BranchId == branchId.Value);
        return Task.FromResult<IReadOnlyList<InventoryWarehouseOption>>(result.OrderBy(item => item.Code).ThenBy(item => item.Name).ToArray());
    }

    public Task<InventoryWarehouseOption?> FindAsync(InventoryRequestContext context, Guid warehouseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(options.FirstOrDefault(item => item.TenantId == context.TenantId.Value && item.WarehouseId == warehouseId));
}

public interface IInventoryProductProvider
{
    Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default);
}

public sealed class NoInventoryProductProvider : IInventoryProductProvider
{
    public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult<InventoryProductReference?>(null);
}

public sealed class MasterDataInventoryProductProvider(
    IProductIdentityPersistence products,
    IMasterDataCatalogPersistence catalog) : IInventoryProductProvider
{
    public async Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await products.FindProductAsync(context.TenantContext, productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var baseUnit = await catalog.FindUnitOfMeasureAsync(
            context.TenantContext,
            product.BaseUnitOfMeasureId,
            cancellationToken);
        if (baseUnit is null
            || baseUnit.TenantId != context.TenantId
            || baseUnit.LifecycleState != Contracts.Modules.MasterData.MasterDataLifecycleState.Active
            || string.IsNullOrWhiteSpace(baseUnit.Code))
        {
            return null;
        }

        return new InventoryProductReference(
            product.TenantId.Value,
            product.Id,
            product.Sku,
            product.Name.English ?? product.Name.Arabic ?? product.Sku,
            product.BaseUnitOfMeasureId,
            baseUnit.Code,
            product.LifecycleState == Contracts.Modules.MasterData.MasterDataLifecycleState.Active,
            product.IsInventoryRelevant,
            product.TrackingEnabled);
    }
}

public sealed record InventoryGoodsReceiptSourceRecord(
    GoodsReceiptRecord Receipt,
    GoodsReceiptLineRecord Line,
    InventoryProductReference Product,
    InventoryWarehouseOption Warehouse,
    Guid UnitOfMeasureId);

public interface IInventoryGoodsReceiptSourceProvider
{
    Task<InventoryGoodsReceiptSourceRecord?> FindAsync(
        InventoryRequestContext context,
        Guid goodsReceiptId,
        Guid goodsReceiptLineId,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventoryGoodsReceiptSourceProvider : IInventoryGoodsReceiptSourceProvider
{
    public Task<InventoryGoodsReceiptSourceRecord?> FindAsync(InventoryRequestContext context, Guid goodsReceiptId, Guid goodsReceiptLineId, CancellationToken cancellationToken = default) =>
        Task.FromResult<InventoryGoodsReceiptSourceRecord?>(null);
}

public sealed class ProcurementInventoryGoodsReceiptSourceProvider(
    IGoodsReceiptPersistence goodsReceipts,
    IInventoryProductProvider products,
    IInventoryWarehouseProvider warehouses) : IInventoryGoodsReceiptSourceProvider
{
    public async Task<InventoryGoodsReceiptSourceRecord?> FindAsync(
        InventoryRequestContext context,
        Guid goodsReceiptId,
        Guid goodsReceiptLineId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await goodsReceipts.FindAsync(context.TenantContext, goodsReceiptId, cancellationToken);
        if (receipt is null || receipt.TenantId != context.TenantId.Value || receipt.Status != GoodsReceiptStatus.Recorded)
        {
            return null;
        }

        var line = receipt.Lines.SingleOrDefault(item => item.Id == goodsReceiptLineId);
        if (line is null || line.AcceptedQuantity <= 0m)
        {
            return null;
        }

        var product = await products.FindAsync(context, line.ProductId, cancellationToken);
        if (product is null
            || !product.IsActive
            || !product.IsInventoryRelevant
            || !string.Equals(product.BaseUnitOfMeasureCode, line.UnitOfMeasureCode, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var warehouse = await warehouses.FindAsync(context, receipt.WarehouseId, cancellationToken);
        if (warehouse is null
            || !warehouse.IsActive
            || warehouse.CompanyId != receipt.Scope.CompanyId
            || warehouse.BranchId != receipt.Scope.BranchId)
        {
            return null;
        }

        return new InventoryGoodsReceiptSourceRecord(receipt, line, product, warehouse, product.BaseUnitOfMeasureId);
    }
}

public sealed record InventorySupplierReturnLineSourceRecord(
    SupplierReturnLineRecord ReturnLine,
    GoodsReceiptLineRecord ReceiptLine,
    InventoryProductReference Product,
    Guid UnitOfMeasureId);

public sealed record InventorySupplierReturnSourceRecord(
    SupplierReturnRecord SupplierReturn,
    GoodsReceiptRecord GoodsReceipt,
    InventoryWarehouseOption Warehouse,
    IReadOnlyList<InventorySupplierReturnLineSourceRecord> Lines);

public interface IInventorySupplierReturnSourceProvider
{
    Task<InventorySupplierReturnSourceRecord?> FindAsync(
        InventoryRequestContext context,
        Guid supplierReturnId,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventorySupplierReturnSourceProvider : IInventorySupplierReturnSourceProvider
{
    public Task<InventorySupplierReturnSourceRecord?> FindAsync(InventoryRequestContext context, Guid supplierReturnId, CancellationToken cancellationToken = default) =>
        Task.FromResult<InventorySupplierReturnSourceRecord?>(null);
}

public enum InventorySupplierReturnStateLookupOutcome
{
    Found = 1,
    NotFound = 2,
    Unavailable = 3
}

public sealed record InventorySupplierReturnStateLookup(
    InventorySupplierReturnStateLookupOutcome Outcome,
    SupplierReturnRecord? Record)
{
    public static InventorySupplierReturnStateLookup Found(SupplierReturnRecord record) => new(InventorySupplierReturnStateLookupOutcome.Found, record);
    public static readonly InventorySupplierReturnStateLookup NotFound = new(InventorySupplierReturnStateLookupOutcome.NotFound, null);
    public static readonly InventorySupplierReturnStateLookup Unavailable = new(InventorySupplierReturnStateLookupOutcome.Unavailable, null);
}

public interface IInventorySupplierReturnStateProvider
{
    Task<InventorySupplierReturnStateLookup> FindAsync(
        InventoryRequestContext context,
        Guid supplierReturnId,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventorySupplierReturnStateProvider : IInventorySupplierReturnStateProvider
{
    public Task<InventorySupplierReturnStateLookup> FindAsync(InventoryRequestContext context, Guid supplierReturnId, CancellationToken cancellationToken = default) =>
        Task.FromResult(InventorySupplierReturnStateLookup.Unavailable);
}

public sealed class ProcurementInventorySupplierReturnStateProvider(ISupplierReturnPersistence supplierReturns) : IInventorySupplierReturnStateProvider
{
    public async Task<InventorySupplierReturnStateLookup> FindAsync(
        InventoryRequestContext context,
        Guid supplierReturnId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var record = await supplierReturns.FindAsync(context.TenantContext, supplierReturnId, cancellationToken);
            return record is null
                ? InventorySupplierReturnStateLookup.NotFound
                : InventorySupplierReturnStateLookup.Found(record);
        }
        catch
        {
            return InventorySupplierReturnStateLookup.Unavailable;
        }
    }
}

public sealed class ProcurementInventorySupplierReturnSourceProvider(
    ISupplierReturnPersistence supplierReturns,
    IGoodsReceiptPersistence goodsReceipts,
    IInventoryProductProvider products,
    IInventoryWarehouseProvider warehouses) : IInventorySupplierReturnSourceProvider
{
    public async Task<InventorySupplierReturnSourceRecord?> FindAsync(
        InventoryRequestContext context,
        Guid supplierReturnId,
        CancellationToken cancellationToken = default)
    {
        var supplierReturn = await supplierReturns.FindAsync(context.TenantContext, supplierReturnId, cancellationToken);
        if (supplierReturn is null
            || supplierReturn.TenantId != context.TenantId.Value
            || supplierReturn.Status != SupplierReturnStatus.AwaitingInventory)
        {
            return null;
        }

        var receipt = await goodsReceipts.FindAsync(context.TenantContext, supplierReturn.GoodsReceiptId, cancellationToken);
        if (receipt is null
            || receipt.Status != GoodsReceiptStatus.Recorded
            || receipt.Scope.CompanyId != supplierReturn.Scope.CompanyId
            || receipt.Scope.BranchId != supplierReturn.Scope.BranchId)
        {
            return null;
        }

        var warehouse = await warehouses.FindAsync(context, supplierReturn.WarehouseId, cancellationToken);
        if (warehouse is null
            || !warehouse.IsActive
            || warehouse.CompanyId != supplierReturn.Scope.CompanyId
            || warehouse.BranchId != supplierReturn.Scope.BranchId)
        {
            return null;
        }

        var receiptLines = receipt.Lines.ToDictionary(item => item.Id);
        var lines = new List<InventorySupplierReturnLineSourceRecord>(supplierReturn.Lines.Count);
        foreach (var returnLine in supplierReturn.Lines)
        {
            if (!receiptLines.TryGetValue(returnLine.GoodsReceiptLineId, out var receiptLine)
                || returnLine.ReturnQuantity <= 0m
                || returnLine.ReturnQuantity > receiptLine.AcceptedQuantity)
            {
                return null;
            }

            var product = await products.FindAsync(context, returnLine.ProductId, cancellationToken);
            if (product is null
                || !product.IsActive
                || !product.IsInventoryRelevant
                || product.ProductId != receiptLine.ProductId
                || !string.Equals(product.BaseUnitOfMeasureCode, receiptLine.UnitOfMeasureCode, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            lines.Add(new InventorySupplierReturnLineSourceRecord(returnLine, receiptLine, product, product.BaseUnitOfMeasureId));
        }

        return lines.Count == 0 ? null : new InventorySupplierReturnSourceRecord(supplierReturn, receipt, warehouse, lines);
    }
}

public sealed record InventorySupplierReturnHandoffResult(bool Succeeded, string Code, Guid? HandoffEvidenceId)
{
    public static InventorySupplierReturnHandoffResult Recorded(Guid id) => new(true, "recorded", id);
    public static InventorySupplierReturnHandoffResult Pending(string code) => new(false, code, null);
}

public interface IInventorySupplierReturnHandoffWriter
{
    Task<InventorySupplierReturnHandoffResult> RecordAsync(
        InventoryRequestContext context,
        InventorySupplierReturnSourceRecord source,
        string handoffReference,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventorySupplierReturnHandoffWriter : IInventorySupplierReturnHandoffWriter
{
    public Task<InventorySupplierReturnHandoffResult> RecordAsync(InventoryRequestContext context, InventorySupplierReturnSourceRecord source, string handoffReference, CancellationToken cancellationToken = default) =>
        Task.FromResult(InventorySupplierReturnHandoffResult.Pending("handoff_unavailable"));
}

public sealed class ProcurementInventorySupplierReturnHandoffWriter(
    ISupplierReturnPersistence supplierReturns,
    ISupplierReturnPhysicalEffectGate? physicalEffectGate = null) : IInventorySupplierReturnHandoffWriter
{
    public async Task<InventorySupplierReturnHandoffResult> RecordAsync(
        InventoryRequestContext context,
        InventorySupplierReturnSourceRecord source,
        string handoffReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handoffReference) || handoffReference.Length > 256)
        {
            return InventorySupplierReturnHandoffResult.Pending("handoff_reference_invalid");
        }

        try
        {
            using var lease = await (physicalEffectGate ?? new SupplierReturnPhysicalEffectGate()).AcquireAsync(source.SupplierReturn.Id, cancellationToken);
            var evidenceId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var evidence = new SupplierReturnAuditEvidence(
                evidenceId,
                source.SupplierReturn.Id,
                now,
                "procurement.supplier-return.inventory-handoff.record",
                context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
                context.TenantId.Value,
                context.ActorId,
                context.SessionId,
                context.AuthorizationPath.ToString(),
                "Allowed",
                "Inventory physical posting",
                source.SupplierReturn.Status,
                SupplierReturnStatus.AwaitingFinance,
                source.SupplierReturn.Scope.CompanyId,
                source.SupplierReturn.Scope.BranchId,
                source.SupplierReturn.Status.ToString(),
                SupplierReturnStatus.AwaitingFinance.ToString(),
                null,
                null);
            var command = new SupplierReturnActionCommand(
                source.SupplierReturn.Id,
                source.SupplierReturn.Version,
                SupplierReturnMutationAction.RecordInventoryHandoff,
                context.ActorId,
                "Inventory physical posting",
                now,
                null,
                handoffReference,
                null,
                null,
                null,
                null);
            var result = await supplierReturns.ActionAsync(context.TenantContext, command, evidence, cancellationToken);
            return result.Succeeded && result.Value is not null
                ? InventorySupplierReturnHandoffResult.Recorded(evidenceId)
                : InventorySupplierReturnHandoffResult.Pending(result.Code);
        }
        catch
        {
            return InventorySupplierReturnHandoffResult.Pending("handoff_unavailable");
        }
    }
}

public sealed record InventoryOpeningBalanceCommand(
    Guid Id,
    InventoryScope Scope,
    string WarehouseCode,
    string WarehouseName,
    DateOnly AsOfDate,
    string SourceOwner,
    string SourceSystem,
    DateTimeOffset ExtractedAt,
    string? SourceReference,
    IReadOnlyList<InventoryOpeningBalanceRowCommand> Rows,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryOpeningBalanceRowCommand(
    Guid Id,
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    decimal UnitCost,
    string CurrencyCode,
    string? TrackingIdentity,
    string? SourceLineReference,
    InventoryProductReference? Product,
    string? ValidationCode);

public static class InventorySourceIdentity
{
    public static string Create(
        TenantId tenantId,
        InventoryOpeningBalanceCommand command,
        string? sourceLineReference,
        string? trackingIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        var canonical = new
        {
            TenantId = tenantId.Value,
            command.Scope.CompanyId,
            command.Scope.BranchId,
            command.Scope.WarehouseId,
            command.AsOfDate,
            SourceOwner = Normalize(command.SourceOwner),
            SourceSystem = Normalize(command.SourceSystem),
            SourceReference = Normalize(command.SourceReference),
            SourceLineReference = Normalize(sourceLineReference),
            TrackingIdentity = Normalize(trackingIdentity)
        };

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))))
            .ToLowerInvariant();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record InventoryReservationCommand(
    Guid Id,
    InventoryScope Scope,
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal RequestedQuantity,
    string SourceType,
    string SourceReference,
    bool AllowPartialAllocation,
    string? TrackingIdentity,
    InventoryProductReference Product,
    string WarehouseCode,
    string WarehouseName,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint,
    Guid? SourceDocumentId = null,
    Guid? SourceLineId = null,
    int? SourceRevision = null,
    decimal? SourceQuantityLimit = null);

public sealed record InventorySalesDeliveryLineCommand(
    Guid ReservationId,
    byte[] ExpectedReservationVersion,
    Guid SourceLineId,
    decimal Quantity,
    string SourceReference);

public sealed record InventorySalesDeliveryPostCommand(
    Guid DeliveryId,
    Guid SalesOrderId,
    int SalesOrderRevision,
    string SourceReference,
    DateOnly EffectiveDate,
    IReadOnlyList<InventorySalesDeliveryLineCommand> Lines,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventorySalesDeliveryPostingRecord(
    Guid DeliveryId,
    Guid SalesOrderId,
    IReadOnlyList<Guid> MovementIds,
    decimal PostedQuantity,
    DateTimeOffset PostedAt,
    Guid? TenantId = null,
    int? SalesOrderRevision = null);

public sealed record InventoryGoodsReceiptPostCommand(
    Guid PostingId,
    InventoryGoodsReceiptSourceRecord Source,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventorySupplierReturnPostCommand(
    Guid PostingId,
    InventorySupplierReturnSourceRecord Source,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryTransferCreateCommand(
    Guid Id,
    InventoryScope Scope,
    InventoryWarehouseOption SourceWarehouse,
    InventoryWarehouseOption DestinationWarehouse,
    Guid ProductId,
    Guid UnitOfMeasureId,
    InventoryProductReference Product,
    decimal Quantity,
    InventoryTransferMode Mode,
    string? TrackingIdentity,
    string? Reason,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryTransferActionCommand(
    Guid TransferId,
    byte[] ExpectedVersion,
    decimal? Quantity,
    string? Reference,
    string? Reason,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryReplayRecord(string ResourceType, Guid ResourceId, string Fingerprint, string SnapshotJson);

public enum InventoryReplayOutcome
{
    NotFound = 1,
    Replay = 2,
    Conflict = 3
}

public sealed record InventoryReplayProbe<T>(InventoryReplayOutcome Outcome, T? Value)
{
    public static InventoryReplayProbe<T> NotFound => new(InventoryReplayOutcome.NotFound, default);
    public static InventoryReplayProbe<T> Conflict => new(InventoryReplayOutcome.Conflict, default);
    public static InventoryReplayProbe<T> ForReplay(T value) => new(InventoryReplayOutcome.Replay, value);
}

public partial interface IInventoryPersistence
{
    Task<IReadOnlyList<InventoryMovementRecord>> ListMovementsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default);
    Task<InventoryMovementRecord?> FindMovementAsync(InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryOpeningBalanceRecord>> ListOpeningBalancesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> FindOpeningBalanceAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> CreateOpeningBalanceAsync(InventoryRequestContext context, InventoryOpeningBalanceCommand command, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryReservationRecord>> ListReservationsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCommand command, decimal availableQuantity, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> AllocateReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventorySalesDeliveryPostingRecord?> PostSalesDeliveryAsync(InventoryRequestContext context, InventorySalesDeliveryPostCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryReservationHistoryRecord>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryAvailabilityRecord?> GetAvailabilityAsync(InventoryRequestContext context, InventoryScope scope, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, InventoryProductReference product, InventoryWarehouseOption warehouse, CancellationToken cancellationToken = default);
    Task<InventoryGoodsReceiptPostingRecord?> PostGoodsReceiptAsync(InventoryRequestContext context, InventoryGoodsReceiptPostCommand command, CancellationToken cancellationToken = default);
    Task<InventoryReplayProbe<InventorySupplierReturnPostingRecord>> ProbeSupplierReturnReplayAsync(InventoryRequestContext context, string? idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default);
    Task<InventorySupplierReturnPostingRecord?> PostSupplierReturnAsync(InventoryRequestContext context, InventorySupplierReturnPostCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryTransferRecord>> ListTransfersAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default);
    Task<InventoryTransferRecord?> FindTransferAsync(InventoryRequestContext context, Guid transferId, CancellationToken cancellationToken = default);
    Task<InventoryTransferRecord?> CreateTransferAsync(InventoryRequestContext context, InventoryTransferCreateCommand command, CancellationToken cancellationToken = default);
    Task<InventoryTransferRecord?> PostDirectTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryTransferRecord?> ShipTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryTransferRecord?> ReceiveTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryTransferRecord?> ResolveTransferShortageAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryTransferRecord?> CancelTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryTransferEventRecord>> ReadTransferHistoryAsync(InventoryRequestContext context, Guid transferId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveGoodsReceiptEffectAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveSupplierReturnEffectAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryAuditRecord>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default);
}

public sealed class InventoryPersistenceGoodsReceiptEffectReader(IInventoryPersistence persistence) : IGoodsReceiptInventoryEffectReader
{
    public async Task<GoodsReceiptInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await persistence.HasActiveGoodsReceiptEffectAsync(tenantContext, goodsReceiptId, cancellationToken)
                ? GoodsReceiptInventoryEffectVerification.ActiveEffectExists
                : GoodsReceiptInventoryEffectVerification.NoActiveEffect;
        }
        catch
        {
            return GoodsReceiptInventoryEffectVerification.Unavailable;
        }
    }
}

public sealed class InventoryPersistenceSupplierReturnEffectReader(IInventoryPersistence persistence) : ISupplierReturnInventoryEffectReader
{
    public async Task<SupplierReturnInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await persistence.HasActiveSupplierReturnEffectAsync(tenantContext, supplierReturnId, cancellationToken)
                ? SupplierReturnInventoryEffectVerification.ActiveEffectExists
                : SupplierReturnInventoryEffectVerification.NoActiveEffect;
        }
        catch
        {
            return SupplierReturnInventoryEffectVerification.Unavailable;
        }
    }
}

public sealed partial class UnavailableInventoryPersistence : IInventoryPersistence
{
    private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("Inventory persistence is unavailable."));
    public Task<IReadOnlyList<InventoryMovementRecord>> ListMovementsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryMovementRecord>>();
    public Task<InventoryMovementRecord?> FindMovementAsync(InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default) => Unavailable<InventoryMovementRecord?>();
    public Task<IReadOnlyList<InventoryOpeningBalanceRecord>> ListOpeningBalancesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryOpeningBalanceRecord>>();
    public Task<InventoryOpeningBalanceRecord?> FindOpeningBalanceAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> CreateOpeningBalanceAsync(InventoryRequestContext context, InventoryOpeningBalanceCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>();
    public Task<IReadOnlyList<InventoryReservationRecord>> ListReservationsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryReservationRecord>>();
    public Task<InventoryReservationRecord?> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventoryReservationRecord?> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCommand command, decimal availableQuantity, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventoryReservationRecord?> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventoryReservationRecord?> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventoryReservationRecord?> AllocateReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventorySalesDeliveryPostingRecord?> PostSalesDeliveryAsync(InventoryRequestContext context, InventorySalesDeliveryPostCommand command, CancellationToken cancellationToken = default) => Unavailable<InventorySalesDeliveryPostingRecord?>();
    public Task<IReadOnlyList<InventoryReservationHistoryRecord>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryReservationHistoryRecord>>();
    public Task<InventoryAvailabilityRecord?> GetAvailabilityAsync(InventoryRequestContext context, InventoryScope scope, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, InventoryProductReference product, InventoryWarehouseOption warehouse, CancellationToken cancellationToken = default) => Unavailable<InventoryAvailabilityRecord?>();
    public Task<InventoryGoodsReceiptPostingRecord?> PostGoodsReceiptAsync(InventoryRequestContext context, InventoryGoodsReceiptPostCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryGoodsReceiptPostingRecord?>();
    public Task<InventoryReplayProbe<InventorySupplierReturnPostingRecord>> ProbeSupplierReturnReplayAsync(InventoryRequestContext context, string? idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default) => Task.FromException<InventoryReplayProbe<InventorySupplierReturnPostingRecord>>(new InvalidOperationException("Inventory persistence is unavailable."));
    public Task<InventorySupplierReturnPostingRecord?> PostSupplierReturnAsync(InventoryRequestContext context, InventorySupplierReturnPostCommand command, CancellationToken cancellationToken = default) => Unavailable<InventorySupplierReturnPostingRecord?>();
    public Task<IReadOnlyList<InventoryTransferRecord>> ListTransfersAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryTransferRecord>>();
    public Task<InventoryTransferRecord?> FindTransferAsync(InventoryRequestContext context, Guid transferId, CancellationToken cancellationToken = default) => Unavailable<InventoryTransferRecord?>();
    public Task<InventoryTransferRecord?> CreateTransferAsync(InventoryRequestContext context, InventoryTransferCreateCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryTransferRecord?>();
    public Task<InventoryTransferRecord?> PostDirectTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryTransferRecord?>();
    public Task<InventoryTransferRecord?> ShipTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryTransferRecord?>();
    public Task<InventoryTransferRecord?> ReceiveTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryTransferRecord?>();
    public Task<InventoryTransferRecord?> ResolveTransferShortageAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryTransferRecord?>();
    public Task<InventoryTransferRecord?> CancelTransferAsync(InventoryRequestContext context, InventoryTransferActionCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryTransferRecord?>();
    public Task<IReadOnlyList<InventoryTransferEventRecord>> ReadTransferHistoryAsync(InventoryRequestContext context, Guid transferId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryTransferEventRecord>>();
    public Task<bool> HasActiveGoodsReceiptEffectAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) => Unavailable<bool>();
    public Task<bool> HasActiveSupplierReturnEffectAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default) => Unavailable<bool>();
    public Task<IReadOnlyList<InventoryAuditRecord>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryAuditRecord>>();
}

public static class InventoryFingerprints
{
    public static string Create<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

#pragma warning restore CS1591
