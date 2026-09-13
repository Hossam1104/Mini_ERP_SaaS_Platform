#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Adapters;

/// <summary>Infrastructure adapter for existing owner-module read contracts.</summary>
internal sealed class MigrationOwnerReferenceAdapter : IMigrationReferenceAuthority
{
    private readonly IProductIdentityPersistence products;
    private readonly ISupplierPersistence suppliers;
    private readonly ICustomerPersistence customers;
    private readonly IMasterDataCatalogPersistence catalog;
    private readonly IMasterDataCurrencyPaymentTermPersistence currencies;
    private readonly IMasterDataTaxPersistence taxes;
    private readonly IFinanceCompanyProvider companies;
    private readonly IFinancePersistence finance;
    private readonly IInventoryProductProvider inventoryProducts;
    private readonly IInventoryWarehouseProvider warehouses;

    public MigrationOwnerReferenceAdapter(
        IProductIdentityPersistence products,
        ISupplierPersistence suppliers,
        ICustomerPersistence customers,
        IMasterDataCatalogPersistence catalog,
        IMasterDataCurrencyPaymentTermPersistence currencies,
        IMasterDataTaxPersistence taxes,
        IFinanceCompanyProvider companies,
        IFinancePersistence finance,
        IInventoryProductProvider inventoryProducts,
        IInventoryWarehouseProvider warehouses)
    {
        this.products = products;
        this.suppliers = suppliers;
        this.customers = customers;
        this.catalog = catalog;
        this.currencies = currencies;
        this.taxes = taxes;
        this.companies = companies;
        this.finance = finance;
        this.inventoryProducts = inventoryProducts;
        this.warehouses = warehouses;
    }

    public async Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(FoundationRequestContext requestContext, MigrationParsedCanonicalRow row, CancellationToken cancellationToken = default)
    {
        if (requestContext.TenantContext is not { } tenant)
            return [Unavailable("migration_reference_tenant_unavailable")];
        try
        {
            var findings = row.Payload switch
            {
                MigrationProductPayload product => await ProductAsync(tenant, product, cancellationToken),
                MigrationSupplierPayload supplier => await PartyAsync(tenant, supplier.SupplierId, supplier.Code, suppliers, "supplier", cancellationToken),
                MigrationCustomerPayload customer => await PartyAsync(tenant, customer.CustomerId, customer.Code, customers, "customer", cancellationToken),
                MigrationOrganizationPayload organization => await OrganizationAsync(requestContext, organization, cancellationToken),
                MigrationInventoryOpeningPayload inventory => await InventoryAsync(requestContext, inventory, cancellationToken),
                MigrationApOpeningPayload ap => await PartyAsync(tenant, ap.SupplierId, null, suppliers, "supplier", cancellationToken),
                MigrationArOpeningPayload ar => await PartyAsync(tenant, ar.CustomerId, null, customers, "customer", cancellationToken),
                MigrationGlOpeningPayload gl => await AccountAsync(requestContext, gl.CompanyId, gl.AccountId, cancellationToken),
                MigrationCashBankOpeningPayload cash => await AccountAsync(requestContext, cash.CompanyId, cash.CashAccountId, cancellationToken),
                MigrationReferencePayload reference => await ReferenceAsync(tenant, row.RecordType, reference, cancellationToken),
                _ => []
            };

            var currency = row.Payload switch
            {
                MigrationInventoryOpeningPayload item => item.CurrencyCode,
                MigrationGlOpeningPayload item => item.CurrencyCode,
                MigrationApOpeningPayload item => item.CurrencyCode,
                MigrationArOpeningPayload item => item.CurrencyCode,
                MigrationCashBankOpeningPayload item => item.CurrencyCode,
                _ => null
            };
            return string.IsNullOrWhiteSpace(currency)
                ? findings
                : [.. findings, .. await CurrencyAsync(tenant, currency, cancellationToken)];
        }
        catch (InvalidOperationException)
        {
            return [Unavailable("migration_reference_authority_unavailable")];
        }
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> ProductAsync(TenantContext tenant, MigrationProductPayload payload, CancellationToken cancellationToken)
    {
        var findings = new List<MigrationReferenceCheck>();
        if (payload.ProductId is { } productId)
        {
            var product = await products.FindProductAsync(tenant, productId, cancellationToken);
            findings.Add(product is null ? Missing("product", productId) : Lifecycle(product.LifecycleState == MasterDataLifecycleState.Active, "product", productId));
        }
        else if (!string.IsNullOrWhiteSpace(payload.Sku)
            && (await products.ListProductsAsync(tenant, cancellationToken)).Any(item => string.Equals(ProductIdentityValuePolicy.ComparisonKey(item.Sku), ProductIdentityValuePolicy.ComparisonKey(payload.Sku), StringComparison.Ordinal)))
        {
            findings.Add(Duplicate("product", payload.Sku));
        }
        if (payload.CategoryId is { } categoryId && payload.BaseUnitOfMeasureId is { } unitId)
        {
            var refs = await products.ValidateReferencesAsync(tenant, categoryId, unitId, cancellationToken);
            if (!refs.Available) findings.Add(Unavailable("migration_product_reference_unavailable"));
            else
            {
                if (!refs.CategoryActive) findings.Add(Missing("category", categoryId));
                if (!refs.BaseUnitOfMeasureActive) findings.Add(Missing("unit-of-measure", unitId));
            }
        }
        else
        {
            if (payload.CategoryId is { } singleCategoryId) findings.Add(await CategoryAsync(tenant, singleCategoryId, cancellationToken));
            if (payload.BaseUnitOfMeasureId is { } singleUnitId) findings.Add(await UnitAsync(tenant, singleUnitId, cancellationToken));
        }
        return findings;
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> PartyAsync<T>(TenantContext tenant, Guid? id, string? code, T owner, string name, CancellationToken cancellationToken) where T : class
    {
        if (id is not { } value)
        {
            if (string.IsNullOrWhiteSpace(code)) return [];
            var exists = owner switch
            {
                ISupplierPersistence supplier => (await supplier.ListSuppliersAsync(tenant, cancellationToken)).Any(item => string.Equals(SupplierValuePolicy.ComparisonKey(item.Code), SupplierValuePolicy.ComparisonKey(code), StringComparison.Ordinal)),
                ICustomerPersistence customer => (await customer.ListCustomersAsync(tenant, cancellationToken)).Any(item => string.Equals(CustomerValuePolicy.ComparisonKey(item.Code), CustomerValuePolicy.ComparisonKey(code), StringComparison.Ordinal)),
                _ => false
            };
            return exists ? [Duplicate(name, code)] : [];
        }
        object? record = owner switch
        {
            ISupplierPersistence supplier => await supplier.FindSupplierAsync(tenant, value, cancellationToken),
            ICustomerPersistence customer => await customer.FindCustomerAsync(tenant, value, cancellationToken),
            _ => null
        };
        return record switch
        {
            SupplierRecord supplier => [Lifecycle(supplier.LifecycleState == MasterDataLifecycleState.Active, name, value)],
            CustomerRecord customer => [Lifecycle(customer.LifecycleState == MasterDataLifecycleState.Active, name, value)],
            _ => [Missing(name, value)]
        };
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> OrganizationAsync(FoundationRequestContext requestContext, MigrationOrganizationPayload payload, CancellationToken cancellationToken)
    {
        if (payload.CompanyId is not { } companyId) return [];
        var options = companies.List(requestContext.TenantContext!.TenantId);
        var company = options.FirstOrDefault(item => item.CompanyId == companyId);
        var findings = new List<MigrationReferenceCheck> { company is null ? Missing("company", companyId) : Lifecycle(company.IsActive, "company", companyId) };
        if (payload.BranchId is { } branchId)
            findings.Add(options.Any(item => item.CompanyId == companyId && item.BranchId == branchId && item.IsActive) ? Active("branch", branchId) : Missing("branch", branchId));
        if (payload.WarehouseId is { } warehouseId && FinanceRequestContext.TryCreate(requestContext, out var financeContext))
        {
            var warehouse = await warehouses.FindAsync(financeContext!.ToInventoryRequestContext(), warehouseId, cancellationToken);
            findings.Add(warehouse is null ? Missing("warehouse", warehouseId) : Lifecycle(warehouse.IsActive && warehouse.CompanyId == companyId && warehouse.BranchId == payload.BranchId, "warehouse", warehouseId));
        }
        return findings;
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> InventoryAsync(FoundationRequestContext requestContext, MigrationInventoryOpeningPayload payload, CancellationToken cancellationToken)
    {
        var findings = new List<MigrationReferenceCheck>(await OrganizationAsync(requestContext, new MigrationOrganizationPayload(payload.CompanyId, payload.BranchId, payload.WarehouseId), cancellationToken));
        if (payload.ProductId is { } productId && FinanceRequestContext.TryCreate(requestContext, out var financeContext))
        {
            var product = await inventoryProducts.FindAsync(financeContext!.ToInventoryRequestContext(), productId, cancellationToken);
            findings.Add(product is null ? Missing("product", productId) : Lifecycle(product.IsActive && product.IsInventoryRelevant, "product", productId));
        }
        if (payload.UnitOfMeasureId is { } unitId)
            findings.Add(await UnitAsync(requestContext.TenantContext!, unitId, cancellationToken));
        if (payload.ControlAccountId is { } accountId)
            findings.AddRange(await AccountAsync(requestContext, payload.CompanyId, accountId, cancellationToken));
        return findings;
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> AccountAsync(FoundationRequestContext requestContext, Guid? companyId, Guid? accountId, CancellationToken cancellationToken)
    {
        if (companyId is not { } company || accountId is not { } account || !FinanceRequestContext.TryCreate(requestContext, out var context)) return [];
        var item = (await finance.ListAccountsAsync(context!, company, cancellationToken)).FirstOrDefault(candidate => candidate.Id == account);
        return [item is null ? Missing("account", account) : Lifecycle(item.Lifecycle == FinanceAccountLifecycle.Active, "account", account)];
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> CurrencyAsync(TenantContext tenant, string code, CancellationToken cancellationToken)
    {
        var item = (await currencies.ListCurrenciesAsync(tenant, cancellationToken)).FirstOrDefault(candidate => string.Equals(candidate.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        return [item is null ? Missing("currency", code) : Lifecycle(item.LifecycleState == MasterDataLifecycleState.Active, "currency", code)];
    }

    private async Task<MigrationReferenceCheck> CategoryAsync(TenantContext tenant, Guid id, CancellationToken cancellationToken)
    {
        var item = await catalog.FindCategoryAsync(tenant, id, cancellationToken);
        return item is null ? Missing("category", id) : Lifecycle(item.LifecycleState == MasterDataLifecycleState.Active, "category", id);
    }

    private async Task<MigrationReferenceCheck> UnitAsync(TenantContext tenant, Guid id, CancellationToken cancellationToken)
    {
        var item = await catalog.FindUnitOfMeasureAsync(tenant, id, cancellationToken);
        return item is null ? Missing("unit-of-measure", id) : Lifecycle(item.LifecycleState == MasterDataLifecycleState.Active, "unit-of-measure", id);
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> UnitReferenceAsync(TenantContext tenant, MigrationReferencePayload payload, CancellationToken cancellationToken)
    {
        if (payload.ReferenceId is { } id)
            return [await UnitAsync(tenant, id, cancellationToken)];
        if (string.IsNullOrWhiteSpace(payload.Code))
            return [];
        var key = MasterDataCategoryUomValuePolicy.NormalizeCode(payload.Code).ToUpperInvariant();
        var item = (await catalog.ListUnitsOfMeasureAsync(tenant, cancellationToken)).FirstOrDefault(candidate => string.Equals(candidate.Code.Trim().ToUpperInvariant(), key, StringComparison.Ordinal));
        return [item is null ? Missing("unit-of-measure", payload.Code) : Lifecycle(item.LifecycleState == MasterDataLifecycleState.Active, "unit-of-measure", item.Id)];
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> ReferenceAsync(TenantContext tenant, MigrationCanonicalRecordType type, MigrationReferencePayload payload, CancellationToken cancellationToken) => type switch
    {
        MigrationCanonicalRecordType.Currency => await CurrencyReferenceAsync(tenant, payload, cancellationToken),
        MigrationCanonicalRecordType.UnitOfMeasure => await UnitReferenceAsync(tenant, payload, cancellationToken),
        MigrationCanonicalRecordType.Tax => await TaxReferenceAsync(tenant, payload, cancellationToken),
        MigrationCanonicalRecordType.PaymentTerm => await PaymentTermReferenceAsync(tenant, payload, cancellationToken),
        _ => []
    };

    private async Task<IReadOnlyList<MigrationReferenceCheck>> CurrencyReferenceAsync(TenantContext tenant, MigrationReferencePayload payload, CancellationToken cancellationToken)
    {
        var items = await currencies.ListCurrenciesAsync(tenant, cancellationToken);
        var item = items.FirstOrDefault(candidate => (payload.ReferenceId is { } id && candidate.Id == id) || (!string.IsNullOrWhiteSpace(payload.Code) && string.Equals(candidate.Code, payload.Code, StringComparison.OrdinalIgnoreCase)));
        return [item is null ? Missing("currency", payload.ReferenceId ?? (object?)payload.Code ?? "reference") : Lifecycle(item.LifecycleState == MasterDataLifecycleState.Active, "currency", item.Id)];
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> TaxReferenceAsync(TenantContext tenant, MigrationReferencePayload payload, CancellationToken cancellationToken)
    {
        var items = await taxes.ListTaxesAsync(tenant, cancellationToken);
        var item = items.FirstOrDefault(candidate => (payload.ReferenceId is { } id && candidate.Id == id) || (!string.IsNullOrWhiteSpace(payload.Code) && string.Equals(candidate.Code, payload.Code, StringComparison.OrdinalIgnoreCase)));
        return [item is null ? Missing("tax", payload.ReferenceId ?? (object?)payload.Code ?? "reference") : Lifecycle(item.LifecycleState == MasterDataLifecycleState.Active, "tax", item.Id)];
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> PaymentTermReferenceAsync(TenantContext tenant, MigrationReferencePayload payload, CancellationToken cancellationToken)
    {
        var items = await currencies.ListPaymentTermsAsync(tenant, cancellationToken);
        var item = items.FirstOrDefault(candidate => (payload.ReferenceId is { } id && candidate.Id == id) || (!string.IsNullOrWhiteSpace(payload.Code) && string.Equals(candidate.Code, payload.Code, StringComparison.OrdinalIgnoreCase)));
        return [item is null ? Missing("payment-term", payload.ReferenceId ?? (object?)payload.Code ?? "reference") : Lifecycle(item.LifecycleState == MasterDataLifecycleState.Active, "payment-term", item.Id)];
    }

    private static MigrationReferenceCheck Active(string name, object id) => new(MigrationReferenceState.Active, MigrationFindingCategory.Reference, $"migration_{name}_active", $"The {name} reference is active.", id.ToString());
    private static MigrationReferenceCheck Duplicate(string name, object id) => new(MigrationReferenceState.Missing, MigrationFindingCategory.Duplicate, $"migration_{name}_already_exists", $"The {name} identity already exists in this Tenant.", id.ToString());
    private static MigrationReferenceCheck Missing(string name, object id) => new(MigrationReferenceState.Missing, MigrationFindingCategory.Reference, $"migration_{name}_missing", $"The {name} reference was not found in this Tenant.", id.ToString());
    private static MigrationReferenceCheck Lifecycle(bool active, string name, object id) => active ? Active(name, id) : new(MigrationReferenceState.Inactive, MigrationFindingCategory.Reference, $"migration_{name}_inactive", $"The {name} reference is inactive.", id.ToString());
    private static MigrationReferenceCheck Unavailable(string code) => new(MigrationReferenceState.Unavailable, MigrationFindingCategory.Reference, code, "The owner-module reference authority is unavailable.");
}

#pragma warning restore CS1591
