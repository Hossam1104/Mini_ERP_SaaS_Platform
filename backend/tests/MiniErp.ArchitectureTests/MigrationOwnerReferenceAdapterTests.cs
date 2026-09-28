#pragma warning disable CS1591

using System.Reflection;
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
using MiniErp.Infrastructure.Persistence.Adapters;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationOwnerReferenceAdapterTests
{
    [Fact]
    public async Task Foreign_currency_opening_is_rejected_before_exchange_rate_lookup()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var currency = Currency(tenant, "USD");
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR")]),
            currencies: Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name == "ListCurrenciesAsync" ? (object)new[] { currency } : null),
            exchangeRates: Stub<IMasterDataExchangeRatePersistence>(method => method.Name == "ListExchangeRatesAsync" ? (object)Array.Empty<MasterDataExchangeRateRecord>() : null));

        var findings = await adapter.ValidateAsync(
            request,
            new MigrationParsedCanonicalRow(
                1,
                "gl-foreign",
                MigrationCanonicalRecordType.GlOpening,
                new MigrationGlOpeningPayload(companyId, Guid.NewGuid(), 10m, 0m, "USD", new DateOnly(2026, 1, 1)),
                "{}"));

        Assert.Contains(findings, item => item.Code == "migration_gl_opening_currency_not_functional");
    }

    [Fact]
    public async Task Inventory_opening_blocks_non_base_uom_without_a_known_conversion()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var fromUnitId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var conversionLookups = 0;
        var conversionApplications = 0;
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR")]),
            catalog: Stub<IMasterDataCatalogPersistence>(method =>
            {
                if (method.Name == "FindConversionAsync") conversionLookups++;
                if (method.Name == "ConvertQuantityAsync") conversionApplications++;
                return method.Name == "FindUnitOfMeasureAsync" ? Unit(tenant, fromUnitId) : null;
            }),
            currencies: Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name == "ListCurrenciesAsync" ? (object)new[] { Currency(tenant, "SAR") } : null),
            inventoryProducts: Stub<IInventoryProductProvider>(method => method.Name == "FindAsync"
                ? new InventoryProductReference(tenant.TenantId.Value, productId, "SKU-1", "Product 1", baseUnitId, "EA", true, true, false)
                : null));

        var findings = await adapter.ValidateAsync(
            request,
            new MigrationParsedCanonicalRow(
                1,
                "inventory-uom",
                MigrationCanonicalRecordType.InventoryOpening,
                new MigrationInventoryOpeningPayload(companyId, null, null, productId, fromUnitId, 2m, 10m, "SAR", new DateOnly(2026, 1, 1)),
                "{}"));

        Assert.Contains(findings, item => item.Code == "migration_inventory_unit_of_measure_invalid");
        Assert.Equal(0, conversionLookups);
        Assert.Equal(0, conversionApplications);
    }

    [Fact]
    public async Task Cash_bank_opening_rejects_legacy_control_account_and_requires_open_period()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR")]),
            currencies: Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name == "ListCurrenciesAsync" ? (object)new[] { Currency(tenant, "SAR") } : null));

        var findings = await adapter.ValidateAsync(
            request,
            new MigrationParsedCanonicalRow(
                1,
                "cash-control-period",
                MigrationCanonicalRecordType.CashBankOpening,
                new MigrationCashBankOpeningPayload(companyId, Guid.NewGuid(), "CASH-OPEN-1", 10m, "SAR", new DateOnly(2026, 1, 1)) { ControlAccountId = Guid.NewGuid() },
                "{}"));

        Assert.Contains(findings, item => item.Code == "migration_cash_bank_control_account_not_allowed");
        Assert.DoesNotContain(findings, item => item.Code == "migration_account_missing");
        Assert.Contains(findings, item => item.Code == "migration_fiscal_period_invalid");
    }

    [Fact]
    public void Business_identity_resolution_uses_each_owner_policy_and_rejects_invalid_values()
    {
        var (tenant, _) = Context();
        var adapter = Create(tenant, new ConfiguredFinanceCompanyProvider([]));
        var product = adapter.ResolveBusinessIdentity(new MigrationParsedCanonicalRow(
            1, "product", MigrationCanonicalRecordType.Product,
            new MigrationProductPayload("  Abc-1  "), "{}"));
        var productCase = adapter.ResolveBusinessIdentity(new MigrationParsedCanonicalRow(
            2, "product-case", MigrationCanonicalRecordType.Product,
            new MigrationProductPayload("ABC-1"), "{}"));
        var supplier = adapter.ResolveBusinessIdentity(new MigrationParsedCanonicalRow(
            3, "supplier", MigrationCanonicalRecordType.Supplier,
            new MigrationSupplierPayload("  sup-1  "), "{}"));
        var customer = adapter.ResolveBusinessIdentity(new MigrationParsedCanonicalRow(
            4, "customer", MigrationCanonicalRecordType.Customer,
            new MigrationCustomerPayload("  cus-1  "), "{}"));
        var invalid = adapter.ResolveBusinessIdentity(new MigrationParsedCanonicalRow(
            5, "invalid", MigrationCanonicalRecordType.Product,
            new MigrationProductPayload("bad\u0001sku"), "{}"));

        Assert.Equal(product.Key, productCase.Key);
        Assert.StartsWith("supplier:", supplier.Key, StringComparison.Ordinal);
        Assert.StartsWith("customer:", customer.Key, StringComparison.Ordinal);
        Assert.Equal(MigrationBusinessIdentityState.Invalid, invalid.State);
        Assert.Equal("migration_business_identity_invalid", invalid.Code);
    }

    [Fact]
    public void Business_duplicate_keys_are_stable_for_every_supported_domain()
    {
        var (tenant, _) = Context();
        var adapter = Create(tenant, new ConfiguredFinanceCompanyProvider([]));
        var companyId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            Row(1, MigrationCanonicalRecordType.Product, new MigrationProductPayload("sku-1")),
            Row(2, MigrationCanonicalRecordType.Supplier, new MigrationSupplierPayload("sup-1")),
            Row(3, MigrationCanonicalRecordType.Customer, new MigrationCustomerPayload("cus-1")),
            Row(4, MigrationCanonicalRecordType.Currency, new MigrationReferencePayload(Code: "sar")),
            Row(5, MigrationCanonicalRecordType.Tax, new MigrationReferencePayload(Code: "vat", EffectiveDate: date)),
            Row(6, MigrationCanonicalRecordType.PaymentTerm, new MigrationReferencePayload(Code: "net30")),
            Row(7, MigrationCanonicalRecordType.UnitOfMeasure, new MigrationReferencePayload(Code: "ea")),
            Row(8, MigrationCanonicalRecordType.PriceList, new MigrationReferencePayload(Code: "retail")),
            Row(9, MigrationCanonicalRecordType.ExchangeRate, new MigrationReferencePayload(SourceCurrencyCode: "USD", TargetCurrencyCode: "SAR", EffectiveDate: date)),
            Row(10, MigrationCanonicalRecordType.InventoryOpening, new MigrationInventoryOpeningPayload(companyId, null, Guid.NewGuid(), productId, unitId, 1m, 2m, "SAR", date, TrackingIdentity: "lot-1", SourceLineReference: "inv-1")),
            Row(11, MigrationCanonicalRecordType.GlOpening, new MigrationGlOpeningPayload(companyId, Guid.NewGuid(), 10m, 0m, "SAR", date, "gl-1")),
            Row(12, MigrationCanonicalRecordType.ApOpening, new MigrationApOpeningPayload(companyId, partyId, Guid.NewGuid(), 10m, "SAR", date, "ap-1")),
            Row(13, MigrationCanonicalRecordType.ArOpening, new MigrationArOpeningPayload(companyId, partyId, "ar-1", date, date, 10m, "SAR", date)),
            Row(14, MigrationCanonicalRecordType.CashBankOpening, new MigrationCashBankOpeningPayload(companyId, Guid.NewGuid(), "cash-1", 10m, "SAR", date))
        };
        var identities = new Dictionary<int, MigrationBusinessIdentityResolution>();
        foreach (var row in rows)
        {
            var duplicate = row with { SourceSequence = row.SourceSequence + 100, SourceRecordId = $"duplicate-{row.SourceSequence}" };
            var firstIdentity = adapter.ResolveBusinessIdentity(row);
            var duplicateIdentity = adapter.ResolveBusinessIdentity(duplicate);
            Assert.Equal(MigrationBusinessIdentityState.Valid, firstIdentity.State);
            Assert.Equal(firstIdentity.Key, duplicateIdentity.Key);
            identities.Add(row.SourceSequence, firstIdentity);
            identities.Add(duplicate.SourceSequence, duplicateIdentity);
        }

        Assert.Equal(rows.Length * 2, MigrationValidationService.DuplicateBusinessKeySequences(identities).Count);

        static MigrationParsedCanonicalRow Row(int sequence, MigrationCanonicalRecordType type, MigrationCanonicalPayload payload) =>
            new(sequence, $"source-{sequence}", type, payload, "{}");
    }

    [Fact]
    public void Organization_business_duplicates_use_the_exact_hierarchical_tuple()
    {
        var (tenant, _) = Context();
        var adapter = Create(tenant, new ConfiguredFinanceCompanyProvider([]));
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var rows = new[]
        {
            Row(1, new MigrationOrganizationPayload(companyId)),
            Row(2, new MigrationOrganizationPayload(companyId)),
            Row(3, new MigrationOrganizationPayload(companyId, branchId)),
            Row(4, new MigrationOrganizationPayload(companyId, branchId)),
            Row(5, new MigrationOrganizationPayload(companyId, branchId, warehouseId)),
            Row(6, new MigrationOrganizationPayload(companyId, branchId, warehouseId)),
            Row(7, new MigrationOrganizationPayload(Guid.NewGuid(), branchId))
        };
        var identities = rows.ToDictionary(row => row.SourceSequence, adapter.ResolveBusinessIdentity);

        Assert.All(identities.Values, identity => Assert.Equal(MigrationBusinessIdentityState.Valid, identity.State));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, MigrationValidationService.DuplicateBusinessKeySequences(identities).Order().ToArray());
        Assert.NotEqual(identities[3].Key, identities[7].Key);

        static MigrationParsedCanonicalRow Row(int sequence, MigrationOrganizationPayload payload) =>
            new(sequence, $"organization-{sequence}", MigrationCanonicalRecordType.Organization, payload, "{}");
    }

    [Fact]
    public void Tax_reference_identity_includes_the_effective_owner_version_date()
    {
        var (tenant, _) = Context();
        var adapter = Create(tenant, new ConfiguredFinanceCompanyProvider([]));
        var jan = new MigrationParsedCanonicalRow(
            1,
            "tax-jan",
            MigrationCanonicalRecordType.Tax,
            new MigrationReferencePayload(Code: "VAT", EffectiveDate: new DateOnly(2026, 1, 1)),
            "{}");
        var janDuplicate = jan with { SourceSequence = 2, SourceRecordId = "tax-jan-duplicate" };
        var feb = jan with
        {
            SourceSequence = 3,
            SourceRecordId = "tax-feb",
            Payload = new MigrationReferencePayload(Code: "VAT", EffectiveDate: new DateOnly(2026, 2, 1))
        };

        Assert.Equal(adapter.ResolveBusinessIdentity(jan).Key, adapter.ResolveBusinessIdentity(janDuplicate).Key);
        Assert.NotEqual(adapter.ResolveBusinessIdentity(jan).Key, adapter.ResolveBusinessIdentity(feb).Key);
    }

    [Fact]
    public async Task Missing_and_inactive_configuration_references_name_the_dependency()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 1);
        var inactiveTax = new MasterDataTaxRecord(
            Guid.NewGuid(), tenant.TenantId, "VAT", "STD", new LocalizedName("Standard"), new LocalizedName("VAT"),
            TaxDirection.Sales, MasterDataLifecycleState.Inactive, 1, [], [1]);
        var inactiveTerm = new MasterDataPaymentTermRecord(
            Guid.NewGuid(), tenant.TenantId, "NET30", new LocalizedName("Net 30"), MasterDataLifecycleState.Inactive, 1, [], [1]);
        var inactivePriceList = new MasterDataPriceListRecord(
            Guid.NewGuid(), tenant.TenantId, "PL-1", new LocalizedName("List"), Guid.NewGuid(), "SAR", null, null, null,
            1, MasterDataLifecycleState.Inactive, 1, [], [1]);
        var inactiveRate = new MasterDataExchangeRateRecord(
            Guid.NewGuid(), tenant.TenantId, Guid.NewGuid(), Guid.NewGuid(), "USD", "SAR", MasterDataLifecycleState.Inactive, 1,
            [new(Guid.NewGuid(), 1, date, null, 3.75m, 2, ExchangeRateProvenance.Manual, null, "USD", "SAR")], [1]);
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR")]),
            catalog: Stub<IMasterDataCatalogPersistence>(method => method.Name == "ListUnitsOfMeasureAsync"
                ? new[] { Unit(tenant, Guid.NewGuid(), MasterDataLifecycleState.Inactive) }
                : null),
            currencies: Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name switch
            {
                "ListCurrenciesAsync" => new[] { Currency(tenant, "USD", MasterDataLifecycleState.Inactive) },
                "ListPaymentTermsAsync" => new[] { inactiveTerm },
                _ => null
            }),
            exchangeRates: Stub<IMasterDataExchangeRatePersistence>(method => method.Name == "ListExchangeRatesAsync" ? new[] { inactiveRate } : null),
            taxes: Stub<IMasterDataTaxPersistence>(method => method.Name == "ListTaxesAsync" ? new[] { inactiveTax } : null),
            priceLists: Stub<IMasterDataPriceListPersistence>(method => method.Name == "ListPriceListsAsync" ? new[] { inactivePriceList } : null));

        var checks = new (MigrationCanonicalRecordType Type, MigrationReferencePayload Payload, string Code)[]
        {
            (MigrationCanonicalRecordType.Currency, new(Code: "USD"), "migration_currency_inactive"),
            (MigrationCanonicalRecordType.Tax, new(Code: "VAT", EffectiveDate: date), "migration_tax_inactive"),
            (MigrationCanonicalRecordType.PaymentTerm, new(Code: "NET30"), "migration_payment_term_inactive"),
            (MigrationCanonicalRecordType.UnitOfMeasure, new(Code: "EA"), "migration_unit_of_measure_inactive"),
            (MigrationCanonicalRecordType.PriceList, new(Code: "PL-1"), "migration_price_list_inactive"),
            (MigrationCanonicalRecordType.ExchangeRate, new(SourceCurrencyCode: "USD", TargetCurrencyCode: "SAR", EffectiveDate: date), "migration_exchange_rate_missing")
        };
        foreach (var (type, payload, code) in checks)
        {
            var findings = await adapter.ValidateAsync(request, new MigrationParsedCanonicalRow(1, "reference-1", type, payload, "{}"));
            Assert.Contains(findings, item => item.Code == code);
        }
    }

    [Fact]
    public async Task Price_list_reference_rejects_an_invalid_code_as_row_data()
    {
        var (tenant, request) = Context();
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([]),
            priceLists: Stub<IMasterDataPriceListPersistence>(method => method.Name == "ListPriceListsAsync"
                ? Array.Empty<MasterDataPriceListRecord>()
                : null));

        var findings = await adapter.ValidateAsync(request, new MigrationParsedCanonicalRow(
            1,
            "invalid-price-list",
            MigrationCanonicalRecordType.PriceList,
            new MigrationReferencePayload(Code: new string('A', 129)),
            "{}"));

        Assert.Contains(findings, item => item.Code == "migration_price_list_invalid");
    }

    [Fact]
    public async Task Missing_product_party_account_and_organization_references_name_the_dependency()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var companyProvider = new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR")]);
        var adapter = Create(
            tenant,
            companyProvider,
            currencies: Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name == "ListCurrenciesAsync"
                ? new[] { Currency(tenant, "SAR") }
                : null),
            products: Stub<IProductIdentityPersistence>(method => null),
            suppliers: Stub<ISupplierPersistence>(method => null),
            customers: Stub<ICustomerPersistence>(method => null));
        var rows = new[]
        {
            new MigrationParsedCanonicalRow(1, "product", MigrationCanonicalRecordType.Product, new MigrationProductPayload(ProductId: Guid.NewGuid()), "{}"),
            new MigrationParsedCanonicalRow(2, "supplier", MigrationCanonicalRecordType.Supplier, new MigrationSupplierPayload(SupplierId: Guid.NewGuid()), "{}"),
            new MigrationParsedCanonicalRow(3, "customer", MigrationCanonicalRecordType.Customer, new MigrationCustomerPayload(CustomerId: Guid.NewGuid()), "{}"),
            new MigrationParsedCanonicalRow(4, "account", MigrationCanonicalRecordType.GlOpening, new MigrationGlOpeningPayload(companyId, Guid.NewGuid(), 10m, 0m, "SAR", new DateOnly(2026, 1, 1)), "{}")
        };
        var expected = new[] { "migration_product_missing", "migration_supplier_missing", "migration_customer_missing", "migration_account_missing" };
        for (var index = 0; index < rows.Length; index++)
        {
            var findings = await adapter.ValidateAsync(request, rows[index]);
            Assert.Contains(expected[index], findings.Select(item => item.Code));
        }

        var organization = Create(tenant, new ConfiguredFinanceCompanyProvider([]));
        var organizationFindings = await organization.ValidateAsync(request, new MigrationParsedCanonicalRow(
            5,
            "organization",
            MigrationCanonicalRecordType.Organization,
            new MigrationOrganizationPayload(companyId, Guid.NewGuid(), Guid.NewGuid()),
            "{}"));
        Assert.Contains(organizationFindings, item => item.Code == "migration_company_missing");
        Assert.Contains(organizationFindings, item => item.Code == "migration_branch_missing");
        Assert.Contains(organizationFindings, item => item.Code == "migration_warehouse_missing");
    }

    [Fact]
    public async Task Tax_requires_one_owner_rate_version_for_the_declared_effective_date()
    {
        var (tenant, request) = Context();
        var tax = new MasterDataTaxRecord(
            Guid.NewGuid(), tenant.TenantId, "VAT", "STD", new LocalizedName("Standard"), new LocalizedName("VAT"),
            TaxDirection.Sales, MasterDataLifecycleState.Active, 1, [], [1]);
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([]),
            taxes: Stub<IMasterDataTaxPersistence>(method => method.Name == "ListTaxesAsync" ? new[] { tax } : null));

        var findings = await adapter.ValidateAsync(request, new MigrationParsedCanonicalRow(
            1,
            "tax-effective-date",
            MigrationCanonicalRecordType.Tax,
            new MigrationReferencePayload(Code: "VAT", EffectiveDate: new DateOnly(2026, 1, 1)),
            "{}"));

        Assert.Contains(findings, item => item.Code == "migration_tax_effective_version_missing");
    }

    [Fact]
    public async Task Explicit_exchange_rate_reference_requires_a_distinct_currency_pair_and_owner_version()
    {
        var (tenant, request) = Context();
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([]),
            exchangeRates: Stub<IMasterDataExchangeRatePersistence>(method => method.Name == "ListExchangeRatesAsync"
                ? Array.Empty<MasterDataExchangeRateRecord>()
                : null));

        var findings = await adapter.ValidateAsync(request, new MigrationParsedCanonicalRow(
            1,
            "self-rate",
            MigrationCanonicalRecordType.ExchangeRate,
            new MigrationReferencePayload(SourceCurrencyCode: "USD", TargetCurrencyCode: "USD", EffectiveDate: new DateOnly(2026, 1, 1)),
            "{}"));

        Assert.Contains(findings, item => item.Code == "migration_exchange_rate_invalid");
    }

    [Fact]
    public async Task Foreign_ar_opening_requires_an_exact_effective_owner_rate_and_payment_term()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR")]),
            currencies: Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name switch
            {
                "ListCurrenciesAsync" => new[] { Currency(tenant, "USD") },
                "ListPaymentTermsAsync" => Array.Empty<MasterDataPaymentTermRecord>(),
                _ => null
            }),
            exchangeRates: Stub<IMasterDataExchangeRatePersistence>(method => method.Name == "ListExchangeRatesAsync"
                ? Array.Empty<MasterDataExchangeRateRecord>()
                : null),
            customers: Stub<ICustomerPersistence>(method => null));
        var date = new DateOnly(2026, 1, 1);
        var findings = await adapter.ValidateAsync(request, new MigrationParsedCanonicalRow(
            1,
            "ar-fx-term",
            MigrationCanonicalRecordType.ArOpening,
            new MigrationArOpeningPayload(companyId, Guid.NewGuid(), "AR-FX-1", date, date, 100m, "USD", date, Guid.NewGuid()),
            "{}"));

        Assert.Contains(findings, item => item.Code == "migration_exchange_rate_missing");
        Assert.Contains(findings, item => item.Code == "migration_payment_term_missing");
    }

    [Fact]
    public void Inventory_business_identity_keeps_tracking_and_source_lines_distinct()
    {
        var (tenant, _) = Context();
        var adapter = Create(tenant, new ConfiguredFinanceCompanyProvider([]));
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 1);

        MigrationBusinessIdentityResolution Resolve(string? tracking, string sourceLine) => adapter.ResolveBusinessIdentity(new MigrationParsedCanonicalRow(
            1,
            sourceLine,
            MigrationCanonicalRecordType.InventoryOpening,
            new MigrationInventoryOpeningPayload(companyId, branchId, warehouseId, productId, unitId, 1m, 10m, "SAR", date, TrackingIdentity: tracking, SourceLineReference: sourceLine),
            "{}"));

        var first = Resolve("LOT-A", "line-1");
        var same = Resolve(" LOT-A ", " line-1 ");
        var otherTracking = Resolve("LOT-B", "line-1");
        var otherSourceLine = Resolve("LOT-A", "line-2");

        Assert.Equal(first.Key, same.Key);
        Assert.NotEqual(first.Key, otherTracking.Key);
        Assert.NotEqual(first.Key, otherSourceLine.Key);
    }

    [Fact]
    public async Task Inactive_finance_company_blocks_every_financial_opening_before_owner_checks()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR", active: false)]),
            currencies: Stub<IMasterDataCurrencyPaymentTermPersistence>(method => method.Name == "ListCurrenciesAsync" ? (object)new[] { Currency(tenant, "SAR") } : null));
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            new MigrationParsedCanonicalRow(1, "inventory", MigrationCanonicalRecordType.InventoryOpening,
                new MigrationInventoryOpeningPayload(companyId, null, null, null, null, 1m, 10m, "SAR", date), "{}"),
            new MigrationParsedCanonicalRow(2, "gl", MigrationCanonicalRecordType.GlOpening,
                new MigrationGlOpeningPayload(companyId, Guid.NewGuid(), 10m, 0m, "SAR", date), "{}"),
            new MigrationParsedCanonicalRow(3, "ap", MigrationCanonicalRecordType.ApOpening,
                new MigrationApOpeningPayload(companyId, null, Guid.NewGuid(), 10m, "SAR", date), "{}"),
            new MigrationParsedCanonicalRow(4, "ar", MigrationCanonicalRecordType.ArOpening,
                new MigrationArOpeningPayload(companyId, null, "AR-OPENING", date, date, 10m, "SAR", date, null), "{}"),
            new MigrationParsedCanonicalRow(5, "cash", MigrationCanonicalRecordType.CashBankOpening,
                 new MigrationCashBankOpeningPayload(companyId, Guid.NewGuid(), "CASH-OPEN-1", 10m, "SAR", date), "{}")
        };

        foreach (var row in rows)
        {
            var findings = await adapter.ValidateAsync(request, row);
            Assert.Contains(findings, item => item.Code == "migration_company_inactive");
            Assert.DoesNotContain(findings, item => item.Code is "migration_account_missing" or "migration_fiscal_period_invalid" or "migration_exchange_rate_missing");
        }
    }

    private static MigrationOwnerReferenceAdapter Create(
        TenantContext tenant,
        IFinanceCompanyProvider companies,
        IMasterDataCatalogPersistence? catalog = null,
        IMasterDataExchangeRatePersistence? exchangeRates = null,
        IMasterDataCurrencyPaymentTermPersistence? currencies = null,
        IInventoryProductProvider? inventoryProducts = null,
        IProductIdentityPersistence? products = null,
        ISupplierPersistence? suppliers = null,
        ICustomerPersistence? customers = null,
        IMasterDataTaxPersistence? taxes = null,
        IMasterDataPriceListPersistence? priceLists = null) =>
        new(
            products ?? new UnavailableProductIdentityPersistence(),
            suppliers ?? new UnavailableSupplierPersistence(),
            customers ?? new UnavailableCustomerPersistence(),
            catalog ?? new UnavailableMasterDataCatalogPersistence(),
            exchangeRates ?? new UnavailableMasterDataExchangeRatePersistence(),
            currencies ?? new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            taxes ?? new UnavailableMasterDataTaxPersistence(),
            companies,
            new UnavailableFinancePersistence(),
            inventoryProducts ?? new NoInventoryProductProvider(),
            new NoInventoryWarehouseProvider(),
            priceLists: priceLists);

    private static (TenantContext Tenant, FoundationRequestContext Request) Context()
    {
        var tenant = TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("migration-owner-adapter"),
            actorId: Guid.NewGuid());
        return (tenant, FoundationRequestContext.ForTenant(
            tenant.ActorId!.Value,
            Guid.NewGuid(),
            tenant,
            "migration.validation.start"));
    }

    private static FinanceCompanyOption Company(TenantContext tenant, Guid companyId, string currency, bool active = true) =>
        new(tenant.TenantId.Value, companyId, "Company", currency, IsActive: active);

    private static MasterDataCurrencyRecord Currency(TenantContext tenant, string code, MasterDataLifecycleState lifecycle = MasterDataLifecycleState.Active) =>
        new(Guid.NewGuid(), tenant.TenantId, code, new LocalizedName(code), lifecycle, 1, [1]);

    private static MasterDataUnitOfMeasureRecord Unit(TenantContext tenant, Guid id, MasterDataLifecycleState lifecycle = MasterDataLifecycleState.Active) =>
        new(id, tenant.TenantId, "EA", new LocalizedName("Each"), lifecycle, [1]);

    private static T Stub<T>(Func<MethodInfo, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private class StubProxy : DispatchProxy
    {
        internal Func<MethodInfo, object?> Handler { get; set; } = _ => null;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            var value = Handler(targetMethod);
            if (targetMethod.ReturnType.IsGenericType
                && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0])
                    .Invoke(null, [value]);
            }

            return targetMethod.ReturnType == typeof(Task) ? Task.CompletedTask : value;
        }
    }
}

#pragma warning restore CS1591
