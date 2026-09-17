#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;
using System.Text.Json;

namespace MiniErp.Infrastructure.Persistence.Adapters;

/// <summary>Infrastructure adapter for existing owner-module read contracts.</summary>
internal sealed class MigrationOwnerReferenceAdapter : IMigrationReferenceAuthority
{
    private readonly IProductIdentityPersistence products;
    private readonly ISupplierPersistence suppliers;
    private readonly ICustomerPersistence customers;
    private readonly IMasterDataCatalogPersistence catalog;
    private readonly IMasterDataExchangeRatePersistence exchangeRates;
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
        IMasterDataExchangeRatePersistence exchangeRates,
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
        this.exchangeRates = exchangeRates;
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
            var findings = new List<MigrationReferenceCheck>(row.Payload switch
            {
                MigrationProductPayload product => await ProductAsync(tenant, product, cancellationToken),
                MigrationSupplierPayload supplier => await PartyAsync(tenant, supplier.SupplierId, supplier.Code, suppliers, "supplier", cancellationToken),
                MigrationCustomerPayload customer => await PartyAsync(tenant, customer.CustomerId, customer.Code, customers, "customer", cancellationToken),
                MigrationOrganizationPayload organization => await OrganizationAsync(requestContext, organization, cancellationToken),
                MigrationInventoryOpeningPayload inventory => await InventoryAsync(requestContext, inventory, cancellationToken),
                MigrationApOpeningPayload ap => await PartyAsync(tenant, ap.SupplierId, null, suppliers, "supplier", cancellationToken),
                MigrationArOpeningPayload ar => await PartyAsync(tenant, ar.CustomerId, null, customers, "customer", cancellationToken),
                MigrationGlOpeningPayload gl => [],
                MigrationCashBankOpeningPayload cash => [],
                MigrationReferencePayload reference => await ReferenceAsync(tenant, row.RecordType, reference, cancellationToken),
                _ => []
            });

            var currency = row.Payload switch
            {
                MigrationInventoryOpeningPayload item => item.CurrencyCode,
                MigrationGlOpeningPayload item => item.CurrencyCode,
                MigrationApOpeningPayload item => item.CurrencyCode,
                MigrationArOpeningPayload item => item.CurrencyCode,
                MigrationCashBankOpeningPayload item => item.CurrencyCode,
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(currency))
                findings.AddRange(await CurrencyAsync(tenant, currency, cancellationToken));
            findings.AddRange(await FinancialAsync(requestContext, row.Payload, cancellationToken));
            return findings;
        }
        catch (InvalidOperationException)
        {
            return [Unavailable("migration_reference_authority_unavailable")];
        }
    }

    public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row)
    {
        try
        {
            return row.Payload switch
            {
                MigrationProductPayload product when !string.IsNullOrWhiteSpace(product.Sku)
                    => MigrationBusinessIdentityResolution.Valid($"product:{ProductIdentityValuePolicy.ComparisonKey(product.Sku)}"),
                MigrationSupplierPayload supplier when !string.IsNullOrWhiteSpace(supplier.Code)
                    => MigrationBusinessIdentityResolution.Valid($"supplier:{SupplierValuePolicy.ComparisonKey(supplier.Code)}"),
                MigrationCustomerPayload customer when !string.IsNullOrWhiteSpace(customer.Code)
                    => MigrationBusinessIdentityResolution.Valid($"customer:{CustomerValuePolicy.ComparisonKey(customer.Code)}"),
                MigrationInventoryOpeningPayload inventory when inventory.CompanyId is { } companyId
                    && inventory.WarehouseId is { } warehouseId
                    && inventory.ProductId is { } productId
                    && inventory.UnitOfMeasureId is { } unitId
                    && inventory.OpeningDate is { } openingDate
                    && !string.IsNullOrWhiteSpace(inventory.SourceLineReference)
                    => MigrationBusinessIdentityResolution.Valid("inventory-opening:" + JsonSerializer.Serialize(new
                    {
                        CompanyId = companyId,
                        inventory.BranchId,
                        WarehouseId = warehouseId,
                        ProductId = productId,
                        UnitOfMeasureId = unitId,
                        OpeningDate = openingDate,
                        TrackingIdentity = inventory.TrackingIdentity?.Trim(),
                        SourceLineReference = inventory.SourceLineReference.Trim()
                    })),
                MigrationArOpeningPayload ar when ar.CompanyId is { } companyId
                    && ar.CustomerId is { } customerId
                    && !string.IsNullOrWhiteSpace(ar.SourceReference)
                    => MigrationBusinessIdentityResolution.Valid("ar-opening:" + JsonSerializer.Serialize(new
                    {
                        CompanyId = companyId,
                        CustomerId = customerId,
                        SourceReference = ar.SourceReference.Trim(),
                        Contract = "migration-ar-opening.v1"
                    })),
                MigrationProductPayload or MigrationSupplierPayload or MigrationCustomerPayload
                    => MigrationBusinessIdentityResolution.NotApplicable(),
                _ => MigrationBusinessIdentityResolution.NotApplicable()
            };
        }
        catch (ArgumentException)
        {
            return MigrationBusinessIdentityResolution.Invalid("The owner-module business identity is outside its approved bounds.");
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
        else if (!string.IsNullOrWhiteSpace(payload.Sku))
        {
            try
            {
                var key = ProductIdentityValuePolicy.ComparisonKey(payload.Sku);
                if ((await products.ListProductsAsync(tenant, cancellationToken)).Any(item => string.Equals(ProductIdentityValuePolicy.ComparisonKey(item.Sku), key, StringComparison.Ordinal)))
                    findings.Add(Duplicate("product", payload.Sku));
            }
            catch (ArgumentException)
            {
                findings.Add(InvalidIdentity("product", payload.Sku));
            }
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
            bool exists;
            try
            {
                exists = owner switch
                {
                    ISupplierPersistence supplier => (await supplier.ListSuppliersAsync(tenant, cancellationToken)).Any(item => string.Equals(SupplierValuePolicy.ComparisonKey(item.Code), SupplierValuePolicy.ComparisonKey(code), StringComparison.Ordinal)),
                    ICustomerPersistence customer => (await customer.ListCustomersAsync(tenant, cancellationToken)).Any(item => string.Equals(CustomerValuePolicy.ComparisonKey(item.Code), CustomerValuePolicy.ComparisonKey(code), StringComparison.Ordinal)),
                    _ => false
                };
            }
            catch (ArgumentException)
            {
                return [InvalidIdentity(name, code)];
            }
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
            if (product is not null)
            {
                if (product.TrackingEnabled && string.IsNullOrWhiteSpace(payload.TrackingIdentity))
                    findings.Add(new(MigrationReferenceState.Missing, MigrationFindingCategory.MandatoryData, "inventory_tracking_identity_required", "Tracked inventory requires a lot, batch, or serial identity."));
                else if (!product.TrackingEnabled && !string.IsNullOrWhiteSpace(payload.TrackingIdentity))
                    findings.Add(new(MigrationReferenceState.Missing, MigrationFindingCategory.MandatoryData, "inventory_tracking_identity_not_supported", "This product does not use a tracking identity."));
            }
        }
        if (payload.UnitOfMeasureId is { } unitId)
        {
            findings.Add(await UnitAsync(requestContext.TenantContext!, unitId, cancellationToken));
            if (payload.ProductId is { } inventoryProductId && payload.CompanyId is not null && FinanceRequestContext.TryCreate(requestContext, out var inventoryContext))
            {
                var product = await inventoryProducts.FindAsync(inventoryContext!.ToInventoryRequestContext(), inventoryProductId, cancellationToken);
                if (product is not null && product.BaseUnitOfMeasureId != unitId)
                {
                    findings.Add(Invalid("inventory-unit-of-measure", $"{unitId:N}->{product.BaseUnitOfMeasureId:N}"));
                }
            }
        }
        return findings;
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> FinancialAsync(FoundationRequestContext requestContext, MigrationCanonicalPayload payload, CancellationToken cancellationToken)
    {
        var (companyId, openingDate, currency, accountIds) = payload switch
        {
            MigrationInventoryOpeningPayload item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, Array.Empty<Guid?>()),
            MigrationGlOpeningPayload item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, new[] { item.AccountId }),
            MigrationApOpeningPayload item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, new[] { item.ControlAccountId }),
            MigrationArOpeningPayload item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, Array.Empty<Guid?>()),
            MigrationCashBankOpeningPayload item => (item.CompanyId, item.OpeningDate, item.CurrencyCode, new[] { item.CashAccountId, item.ControlAccountId }),
            _ => (null, null, null, Array.Empty<Guid?>())
        };
        if (companyId is not { } company || !FinanceRequestContext.TryCreate(requestContext, out var context))
            return [];

        var findings = new List<MigrationReferenceCheck>();
        var companyOption = companies.ListAll(requestContext.TenantContext!.TenantId).FirstOrDefault(item => item.CompanyId == company);
        findings.Add(companyOption is null ? Missing("company", company) : Lifecycle(companyOption.IsActive, "company", company));
        if (companyOption is not { IsActive: true })
            return findings;

        if (payload is MigrationInventoryOpeningPayload or MigrationArOpeningPayload
            && !string.Equals(currency?.Trim(), companyOption.FunctionalCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            findings.Add(new(MigrationReferenceState.Missing, MigrationFindingCategory.Currency,
                payload is MigrationArOpeningPayload ? "migration_ar_opening_currency_not_functional" : "migration_inventory_opening_currency_not_functional",
                "Economic opening currency must equal the Company's Finance functional currency."));

        foreach (var accountId in accountIds.Where(item => item is not null).Select(item => item!.Value).Distinct())
            findings.AddRange(await AccountAsync(context!, company, accountId, openingDate, currency, cancellationToken));

        if (openingDate is { } date)
            findings.Add(await PeriodAsync(context!, company, date, cancellationToken));
        if (payload is not MigrationArOpeningPayload && companyOption is not null && !string.IsNullOrWhiteSpace(currency))
            findings.AddRange(await ExchangeRateAsync(requestContext.TenantContext!, currency, companyOption.FunctionalCurrencyCode, date: openingDate, cancellationToken));
        return findings;
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> AccountAsync(
        FinanceRequestContext context,
        Guid companyId,
        Guid accountId,
        DateOnly? openingDate,
        string? currency,
        CancellationToken cancellationToken)
    {
        var item = (await finance.ListAccountsAsync(context, companyId, cancellationToken)).FirstOrDefault(candidate => candidate.Id == accountId);
        if (item is null)
            return [Missing("account", accountId)];

        var findings = new List<MigrationReferenceCheck> { Lifecycle(item.Lifecycle == FinanceAccountLifecycle.Active, "account", accountId) };
        if (!item.IsPostingAccount)
            findings.Add(Invalid("account", $"{accountId:N}:not-posting"));
        if (openingDate is { } date && (date < item.EffectiveFrom || item.EffectiveTo is { } end && date > end))
            findings.Add(Invalid("account", $"{accountId:N}:not-effective"));
        if (!string.IsNullOrWhiteSpace(currency)
            && item.CurrencyBehavior == FinanceCurrencyBehavior.FunctionalOnly
            && !string.Equals(currency, companies.List(context.TenantId).FirstOrDefault(candidate => candidate.CompanyId == companyId)?.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
            findings.Add(Invalid("account", $"{accountId:N}:currency"));
        return findings;
    }

    private async Task<MigrationReferenceCheck> PeriodAsync(
        FinanceRequestContext context,
        Guid companyId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var calendars = await finance.ListCalendarsAsync(context, companyId, cancellationToken);
        foreach (var calendar in calendars.Where(item => item.Lifecycle == FinanceCalendarLifecycle.Active))
        {
            foreach (var year in (await finance.ListYearsAsync(context, calendar.Id, cancellationToken)).Where(item => item.State == FinanceFiscalYearState.Open && item.StartDate <= date && date <= item.EndDate))
            {
                if ((await finance.ListPeriodsAsync(context, year.Id, cancellationToken)).Any(item => item.State == FinanceFiscalPeriodState.Open && item.StartDate <= date && date <= item.EndDate))
                    return Active("fiscal-period", date);
            }
        }
        return Invalid("fiscal-period", date);
    }

    private async Task<IReadOnlyList<MigrationReferenceCheck>> ExchangeRateAsync(
        TenantContext tenant,
        string sourceCurrency,
        string targetCurrency,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (date is null || string.Equals(sourceCurrency.Trim(), targetCurrency.Trim(), StringComparison.OrdinalIgnoreCase))
            return [];

        var versions = (await exchangeRates.ListExchangeRatesAsync(tenant, cancellationToken))
            .Where(item => item.LifecycleState == MasterDataLifecycleState.Active)
            .SelectMany(item => item.Versions)
            .Where(item => string.Equals(item.SourceCurrencyCode, sourceCurrency.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.TargetCurrencyCode, targetCurrency.Trim(), StringComparison.OrdinalIgnoreCase)
                && item.EffectiveFrom <= date.Value
                && (item.EffectiveTo is null || date.Value <= item.EffectiveTo.Value))
            .ToArray();
        if (versions.Length == 0)
            return [new(MigrationReferenceState.Missing, MigrationFindingCategory.Currency, "migration_exchange_rate_missing", "No active exchange-rate version covers the opening date and currency pair.", $"{sourceCurrency}->{targetCurrency}@{date:yyyy-MM-dd}")];
        if (versions.Length != 1)
            return [new(MigrationReferenceState.Ambiguous, MigrationFindingCategory.Currency, "migration_exchange_rate_ambiguous", "More than one active exchange-rate version covers the opening date and currency pair.", $"{sourceCurrency}->{targetCurrency}@{date:yyyy-MM-dd}")];
        return versions[0].Rate > 0 && versions[0].RateScale > 0
            ? [Active("exchange-rate", $"{sourceCurrency}->{targetCurrency}@{date:yyyy-MM-dd}")]
            : [Invalid("exchange-rate", $"{sourceCurrency}->{targetCurrency}@{date:yyyy-MM-dd}")];
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
    private static MigrationReferenceCheck InvalidIdentity(string name, object id) => new(MigrationReferenceState.Missing, MigrationFindingCategory.Reference, "migration_business_identity_invalid", $"The {name} business identity is invalid according to its owner module.", id.ToString());
    private static MigrationReferenceCheck Missing(string name, object id) => new(MigrationReferenceState.Missing, MigrationFindingCategory.Reference, $"migration_{name}_missing", $"The {name} reference was not found in this Tenant.", id.ToString());
    private static MigrationReferenceCheck Invalid(string name, object id) => new(MigrationReferenceState.Missing, name is "uom-conversion" ? MigrationFindingCategory.Uom : name is "exchange-rate" ? MigrationFindingCategory.Currency : MigrationFindingCategory.Reference, $"migration_{name.Replace('-', '_')}_invalid", $"The {name} reference is invalid for this migration.", id.ToString());
    private static MigrationReferenceCheck Lifecycle(bool active, string name, object id) => active ? Active(name, id) : new(MigrationReferenceState.Inactive, MigrationFindingCategory.Reference, $"migration_{name}_inactive", $"The {name} reference is inactive.", id.ToString());
    private static MigrationReferenceCheck Unavailable(string code) => new(MigrationReferenceState.Unavailable, MigrationFindingCategory.Reference, code, "The owner-module reference authority is unavailable.");
}

#pragma warning restore CS1591
