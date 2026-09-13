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
    public async Task Foreign_currency_opening_requires_one_exact_active_exchange_rate()
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

        Assert.Contains(findings, item => item.Code == "migration_exchange_rate_missing");
    }

    [Fact]
    public async Task Inventory_opening_requires_owner_product_uom_conversion()
    {
        var (tenant, request) = Context();
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var fromUnitId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var conversion = new MasterDataConversionRecord(Guid.NewGuid(), tenant.TenantId, fromUnitId, baseUnitId, 2m, [1]);
        var adapter = Create(
            tenant,
            new ConfiguredFinanceCompanyProvider([Company(tenant, companyId, "SAR")]),
            catalog: Stub<IMasterDataCatalogPersistence>(method => method.Name switch
            {
                "FindUnitOfMeasureAsync" => Unit(tenant, fromUnitId),
                "FindConversionAsync" => conversion,
                "ConvertQuantityAsync" => new MasterDataQuantityConversionResult(false, "conversion_invalid", null),
                _ => null
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

        Assert.Contains(findings, item => item.Code == "migration_uom_conversion_invalid");
    }

    [Fact]
    public async Task Finance_opening_requires_owner_control_accounts_and_open_period()
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
                new MigrationCashBankOpeningPayload(companyId, Guid.NewGuid(), Guid.NewGuid(), 10m, "SAR", new DateOnly(2026, 1, 1)),
                "{}"));

        Assert.Equal(2, findings.Count(item => item.Code == "migration_account_missing"));
        Assert.Contains(findings, item => item.Code == "migration_fiscal_period_invalid");
    }

    private static MigrationOwnerReferenceAdapter Create(
        TenantContext tenant,
        IFinanceCompanyProvider companies,
        IMasterDataCatalogPersistence? catalog = null,
        IMasterDataExchangeRatePersistence? exchangeRates = null,
        IMasterDataCurrencyPaymentTermPersistence? currencies = null,
        IInventoryProductProvider? inventoryProducts = null) =>
        new(
            new UnavailableProductIdentityPersistence(),
            new UnavailableSupplierPersistence(),
            new UnavailableCustomerPersistence(),
            catalog ?? new UnavailableMasterDataCatalogPersistence(),
            exchangeRates ?? new UnavailableMasterDataExchangeRatePersistence(),
            currencies ?? new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            new UnavailableMasterDataTaxPersistence(),
            companies,
            new UnavailableFinancePersistence(),
            inventoryProducts ?? new NoInventoryProductProvider(),
            new NoInventoryWarehouseProvider());

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

    private static FinanceCompanyOption Company(TenantContext tenant, Guid companyId, string currency) =>
        new(tenant.TenantId.Value, companyId, "Company", currency);

    private static MasterDataCurrencyRecord Currency(TenantContext tenant, string code) =>
        new(Guid.NewGuid(), tenant.TenantId, code, new LocalizedName(code), MasterDataLifecycleState.Active, 1, [1]);

    private static MasterDataUnitOfMeasureRecord Unit(TenantContext tenant, Guid id) =>
        new(id, tenant.TenantId, "EA", new LocalizedName("Each"), MasterDataLifecycleState.Active, [1]);

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
