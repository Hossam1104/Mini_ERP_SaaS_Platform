using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Reporting;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class ReportingHold1Tests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ForeignTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Company = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid ForeignCompany = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid Branch = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid SiblingBranch = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Warehouse = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ForeignBranch = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SiblingWarehouse = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ForeignWarehouse = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void Reporting_page_contract_is_bounded_typed_and_explicitly_sorted()
    {
        Assert.Throws<ArgumentException>(() => ReportingPageRequest.Create(0, 100, null, "asc"));
        Assert.Throws<ArgumentException>(() => ReportingPageRequest.Create(1, 501, null, "asc"));
        Assert.Throws<ArgumentException>(() => ReportingPageRequest.Create(1, 100, null, "sideways"));

        var page = ReportingPageRequest.Create(2, 25, "updatedAt", "DESC");
        Assert.Equal(2, page.Page);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(25, page.Offset);
        Assert.Equal("updatedAt", page.SortBy);
        Assert.Equal("desc", page.SortDirection);

        AssertPageParameter<IFinanceReportingReadPort>(nameof(IFinanceReportingReadPort.QueryGeneralLedgerPageAsync));
        AssertPageParameter<IFinanceReportingReadPort>(nameof(IFinanceReportingReadPort.QueryAgingPageAsync));
        AssertPageParameter<IInventoryReportingReadPort>(nameof(IInventoryReportingReadPort.ListStatesReportingPageAsync));
        AssertPageParameter<IInventoryReportingReadPort>(nameof(IInventoryReportingReadPort.ListEventsReportingPageAsync));
        AssertPageParameter<IPurchaseOrderReportingReadPort>(nameof(IPurchaseOrderReportingReadPort.ListReportingPageAsync));
        AssertPageParameter<IGoodsReceiptReportingReadPort>(nameof(IGoodsReceiptReportingReadPort.ListReportingPageAsync));
        AssertPageParameter<ISalesReportingReadPort>(nameof(ISalesReportingReadPort.ListOrdersReportingPageAsync));
        AssertPageParameter<IFoundationAuditScopedEvidenceReader>(nameof(IFoundationAuditScopedEvidenceReader.ReadForTenantScopeAsync));
    }

    [Fact]
    public void Reporting_catalogue_distinguishes_implemented_and_unavailable_capabilities_without_schedule_inference()
    {
        var service = CreateService(new AllowingScopeResolver());
        var definitions = service.Catalogue();

        Assert.Equal(22, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(definitions, item =>
        {
            Assert.Equal("1.0", item.DefinitionVersion);
            Assert.False(item.PendingDecision);
            Assert.False(item.SchedulingEnabled);
        });
        Assert.Contains(definitions, item => item.ExportEnabled && !item.SchedulingEnabled);

        var unavailable = definitions
            .Where(item => item.ImplementationState == ReportingImplementationState.SOURCE_CAPABILITY_UNAVAILABLE)
            .Select(item => item.Code)
            .OrderBy(item => item)
            .ToArray();
        Assert.Equal(
            [
                "finance.bank-reconciliation",
                "finance.cash-movement",
                "inventory.count-variance",
                "procurement.match-exceptions",
                "sales.fulfillment",
                "sales.returns-credits"
            ],
            unavailable);

        var taxSummary = definitions.Single(item => item.Code == "finance.tax-summary");
        Assert.Equal(ReportingImplementationState.IMPLEMENTABLE_NOW, taxSummary.ImplementationState);
        Assert.Contains("internal VAT/accounting facts", taxSummary.SourceOwnership, StringComparison.Ordinal);
        Assert.DoesNotContain("ZATCA", taxSummary.SourceOwnership, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Database_backed_detail_adapters_implement_source_owned_reporting_ports()
    {
        Assert.Contains(typeof(IFinanceReportingReadPort), typeof(FinanceMesp135Persistence).GetInterfaces());
        Assert.Contains(typeof(IInventoryReportingReadPort), typeof(InventoryValuationPersistence).GetInterfaces());
        Assert.Contains(typeof(IPurchaseOrderReportingReadPort), typeof(PurchaseOrderPersistence).GetInterfaces());
        Assert.Contains(typeof(IGoodsReceiptReportingReadPort), typeof(GoodsReceiptPersistence).GetInterfaces());
        Assert.Contains(typeof(ISalesReportingReadPort), typeof(SalesPersistence).GetInterfaces());
    }

    [Fact]
    public async Task Unavailable_source_is_explicit_and_unsupported_filters_are_rejected()
    {
        var service = CreateService(new AllowingScopeResolver(allowedCompany: Company));
        var context = CreateReportingContext(Tenant);

        var unavailable = await service.ExecuteAsync(
            context,
            "finance.cash-movement",
            new ReportingQuery(CompanyId: Company));

        Assert.NotNull(unavailable);
        Assert.Equal(ReportingResultState.Unavailable, unavailable!.Metadata.State);
        Assert.Equal("source_capability_unavailable", unavailable.Metadata.Sources.Single().Detail);
        Assert.Empty(unavailable.Rows);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(
            context,
            "finance.cash-movement",
            new ReportingQuery(CompanyId: Company, Status: "Posted")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(
            context,
            "finance.cash-movement",
            new ReportingQuery(CompanyId: Company, PageSize: 501)));
    }

    [Fact]
    public async Task Reporting_rejects_a_foreign_company_before_source_execution()
    {
        var service = CreateService(new AllowingScopeResolver(allowedCompany: Company));
        var context = CreateReportingContext(Tenant);

        var result = await service.ExecuteAsync(
            context,
            "finance.cash-movement",
            new ReportingQuery(CompanyId: ForeignCompany));

        Assert.NotNull(result);
        Assert.Equal(ReportingResultState.Denied, result!.Metadata.State);
        Assert.Equal("scope_denied", result.Metadata.Sources.Single().Detail);
    }

    [Fact]
    public async Task Organization_scope_matrix_rejects_widening_and_sibling_targets()
    {
        var service = CreateService(new HierarchicalScopeResolver());

        await AssertDenied(service, CreateReportingContext(Tenant), "sales.fulfillment", new ReportingQuery(CompanyId: ForeignCompany));
        await AssertDenied(service, CreateReportingContext(Tenant, $"Company:{Company:D}"), "sales.fulfillment", new ReportingQuery(CompanyId: ForeignCompany));
        await AssertDenied(service, CreateReportingContext(Tenant, $"Company:{Company:D}"), "sales.orders", new ReportingQuery(CompanyId: ForeignCompany, BranchId: ForeignBranch));
        await AssertDenied(service, CreateReportingContext(Tenant, $"Branch:{Branch:D}"), "sales.orders", new ReportingQuery(CompanyId: Company));
        await AssertDenied(service, CreateReportingContext(Tenant, $"Branch:{Branch:D}"), "sales.orders", new ReportingQuery(CompanyId: Company, BranchId: SiblingBranch));
        await AssertDenied(service, CreateReportingContext(Tenant, $"Branch:{Branch:D}"), "sales.fulfillment", new ReportingQuery(CompanyId: Company, BranchId: SiblingBranch, WarehouseId: SiblingWarehouse));
        await AssertDenied(service, CreateReportingContext(Tenant, $"Warehouse:{Warehouse:D}"), "sales.fulfillment", new ReportingQuery(CompanyId: Company, BranchId: Branch, WarehouseId: ForeignWarehouse));
    }

    [Fact]
    public void Endpoint_authorization_uses_the_identity_current_scope_for_warehouse_queries()
    {
        var authorization = new ReportingAuthorizationService(
            new ConfiguredFinanceCompanyProvider([]),
            new CurrentWarehouseScopeResolver());
        var definition = new ReportingDefinition(
            "inventory.valuation",
            "Inventory valuation",
            "تقييم المخزون",
            "Inventory",
            "1.0",
            "Inventory",
            "Inventory",
            ["company", "branch", "warehouse"],
            true,
            false,
            true,
            false);
        var foundation = FoundationRequestContext.ForTenant(
            Actor,
            Guid.NewGuid(),
            CreateTenantContext(Tenant, $"Warehouse:{Warehouse:D}"),
            "tenant.reporting.report.view");

        var result = authorization.Resolve(
            foundation,
            "reporting.report.execute",
            definition,
            new ReportingQuery());

        Assert.True(result.Succeeded, result.Code);
        var normalized = authorization.NormalizeQuery(result.Value!, definition, new ReportingQuery());
        Assert.True(normalized.Succeeded, normalized.Code);
        Assert.Equal(Company, normalized.Value!.CompanyId);
        Assert.Equal(Branch, normalized.Value.BranchId);
        Assert.Equal(Warehouse, normalized.Value.WarehouseId);
    }

    [Fact]
    public async Task Audit_scope_reads_are_exact_and_tenant_bound()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var tenantContext = CreateTenantContext(Tenant);
        var branchScope = TenantWorkScope.IssueFromVerifiedAuthority(
            tenantContext,
            TenantWorkScopeRequest.ForBranch(Company, Branch));

        await AppendAuditEvidence(store, tenantContext, $"Company:{Company:D}", "audit-company", DateTimeOffset.UtcNow.AddMinutes(-3));
        await AppendAuditEvidence(store, tenantContext, $"Branch:{Branch:D}", "audit-branch", DateTimeOffset.UtcNow.AddMinutes(-2));
        await AppendAuditEvidence(store, tenantContext, $"Branch:{SiblingBranch:D}", "audit-sibling", DateTimeOffset.UtcNow.AddMinutes(-1));
        await AppendAuditEvidence(store, CreateTenantContext(ForeignTenant), $"Branch:{Branch:D}", "audit-foreign", DateTimeOffset.UtcNow);

        var page = await store.ReadForTenantScopeAsync(
            tenantContext,
            branchScope,
            ReportingPageRequest.Create(1, 10, "occurredAt", "desc"));

        Assert.Equal(1, page.TotalRows);
        var evidence = Assert.Single(page.Rows);
        Assert.Equal($"Branch:{Branch:D}", evidence.OrganizationScope);
        Assert.Equal("audit-branch", evidence.CorrelationId);
    }

    [Fact]
    public void Runtime_records_are_tenant_bound_even_when_actor_is_not_used_for_lookup()
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var store = new ReportingRuntimeStore(clock);
        var now = clock.GetUtcNow();
        var job = new ReportingJobRecord(
            Guid.NewGuid(), Tenant, Actor, "sales.orders", "fingerprint", ReportingJobStatus.Completed,
            now, now, null, null, null, $"Company:{Company:D}");
        var artifact = new ReportingArtifactRecord(
            Guid.NewGuid(), Tenant, Actor, "sales.orders", Guid.NewGuid(), "sales.csv", "text/csv", now,
            job.JobId, $"Company:{Company:D}");
        var schedule = new ReportingScheduleRecord(
            Guid.NewGuid(), Tenant, Actor, "sales.orders", new ReportingQuery(CompanyId: Company), "0 9 * * 1", "UTC",
            ReportingScheduleStatus.Disabled, "local-test-sink", now, now, [1], $"Company:{Company:D}");

        store.SaveJob(job);
        store.SaveArtifact(artifact);
        store.SaveSchedule(schedule);

        Assert.Equal(job.JobId, store.FindJob(Tenant, job.JobId)?.JobId);
        Assert.Equal(artifact.ArtifactId, store.FindArtifact(Tenant, artifact.ArtifactId)?.ArtifactId);
        Assert.Equal(schedule.ScheduleId, store.FindSchedule(Tenant, schedule.ScheduleId)?.ScheduleId);
        Assert.Null(store.FindJob(ForeignTenant, job.JobId));
        Assert.Null(store.FindArtifact(ForeignTenant, artifact.ArtifactId));
        Assert.Null(store.FindSchedule(ForeignTenant, schedule.ScheduleId));
        Assert.Equal($"Company:{Company:D}", store.ListSchedules(Tenant).Single().OrganizationScope);
    }

    [Fact]
    public async Task Shared_runtime_records_reauthorize_stored_scope_before_access()
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var runtime = new ReportingRuntimeStore(clock);
        var privateFiles = new InMemoryPrivateObjectStorage();
        var service = CreateService(new HierarchicalScopeResolver(), runtime, privateFiles);
        var ownerContext = CreateReportingContext(Tenant, $"Company:{Company:D}");
        var restrictedContext = CreateReportingContext(Tenant, $"Branch:{Branch:D}");
        var now = clock.GetUtcNow();
        var ownerScope = TenantWorkScope.IssueFromVerifiedAuthority(
            ownerContext.TenantContext,
            TenantWorkScopeRequest.ForCompany(Company));
        await using var content = new MemoryStream("owner-only"u8.ToArray());
        var file = await privateFiles.StoreAsync(
            ownerContext.TenantContext,
            ownerScope,
            "owner-only.csv",
            "text/csv",
            content);
        var job = new ReportingJobRecord(
            Guid.NewGuid(), Tenant, Actor, "sales.orders", "owner-job", ReportingJobStatus.Completed,
            now, now, null, null, null, $"Company:{Company:D}");
        var artifact = new ReportingArtifactRecord(
            Guid.NewGuid(), Tenant, Actor, "sales.orders", file.ObjectId, "owner-only.csv", "text/csv", now,
            job.JobId, $"Company:{Company:D}");
        var schedule = new ReportingScheduleRecord(
            Guid.NewGuid(), Tenant, Actor, "sales.orders", new ReportingQuery(CompanyId: Company), "0 9 * * 1", "UTC",
            ReportingScheduleStatus.Disabled, "local-test-sink", now, now, [1], $"Company:{Company:D}");

        runtime.SaveJob(job);
        runtime.SaveArtifact(artifact);
        runtime.SaveSchedule(schedule);

        Assert.Null(service.FindJob(restrictedContext, job.JobId));
        var artifactRead = await service.ReadArtifactAsync(restrictedContext, artifact.ArtifactId);
        Assert.False(artifactRead.Succeeded);
        Assert.Equal("artifact_not_found", artifactRead.Code);
        Assert.Empty(service.ListSchedules(restrictedContext));
        var scheduleUpdate = await service.SetScheduleStatusAsync(
            restrictedContext,
            schedule.ScheduleId,
            ReportingScheduleStatus.Enabled,
            schedule.Version,
            "restricted-update");
        Assert.False(scheduleUpdate.Succeeded);
        Assert.Equal("schedule_not_found", scheduleUpdate.Code);
    }

    [Fact]
    public async Task Export_does_not_create_an_executable_schedule_without_explicit_catalogue_eligibility()
    {
        var service = CreateService(new AllowingScopeResolver(allowedCompany: Company));
        var result = await service.CreateScheduleAsync(
            CreateReportingContext(Tenant),
            "sales.orders",
            new ReportingQuery(CompanyId: Company),
            "0 9 * * 1",
            "UTC",
            "local-test-sink",
            "schedule-hold1");

        Assert.False(result.Succeeded);
        Assert.Equal("scheduling_not_available", result.Code);
    }

    [Fact]
    public void As_of_and_freshness_are_clock_and_source_facts_not_process_static_state()
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 23, 59, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 9, 7), ReportingTimeSemantics.DefaultAsOf(clock));

        clock.UtcNow = new DateTimeOffset(2026, 9, 8, 0, 1, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 9, 8), ReportingTimeSemantics.DefaultAsOf(clock));

        var older = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var newer = older.AddMinutes(5);
        var sources = new[]
        {
            new ReportingSourceEvidence("Finance", "Ready", "1", newer),
            new ReportingSourceEvidence("Inventory", "Ready", "1", older)
        };
        Assert.Equal(older, ReportingTimeSemantics.AggregateDataAsOf(sources));
        Assert.Equal(ReportingResultState.Fresh, ReportingTimeSemantics.AggregateState(sources, ReportingResultState.Fresh));
        Assert.Equal(
            ReportingResultState.Unknown,
            ReportingTimeSemantics.AggregateState(
                [.. sources, new ReportingSourceEvidence("Audit", "Unknown", null, null)],
                ReportingResultState.Fresh));
        Assert.Null(ReportingTimeSemantics.AggregateDataAsOf(
            [.. sources, new ReportingSourceEvidence("Audit", "Unknown", null, null)]));
    }

    private static void AssertPageParameter<T>(string methodName)
    {
        var method = typeof(T).GetMethod(methodName);
        Assert.NotNull(method);
        Assert.Contains(method!.GetParameters(), parameter => parameter.ParameterType == typeof(ReportingPageRequest));
    }

    private static ReportingService CreateService(
        IOrganizationScopeOwnershipResolver resolver,
        ReportingRuntimeStore? runtime = null,
        IPrivateObjectStorage? privateFiles = null) =>
        new(
            new UnavailableFinanceMesp135Persistence(),
            new UnavailableInventoryValuationPersistence(),
            new UnavailablePurchaseOrderPersistence(),
            new UnavailableGoodsReceiptPersistence(),
            new UnavailableSalesPersistence(),
            new FoundationAuditCoordinator(
                new LocalImmutableAuditEvidenceStore(),
                new LocalFoundationAuditTelemetrySink(),
                new LocalFoundationAuditOperationalSignalSink()),
            new LocalImmutableAuditEvidenceStore(),
            resolver,
            privateFiles ?? new InMemoryPrivateObjectStorage(),
            runtime ?? new ReportingRuntimeStore());

    private static async Task AssertDenied(
        ReportingService service,
        ReportingRequestContext context,
        string reportCode,
        ReportingQuery query)
    {
        var result = await service.ExecuteAsync(context, reportCode, query);
        Assert.NotNull(result);
        Assert.Equal(ReportingResultState.Denied, result!.Metadata.State);
    }

    private static ReportingRequestContext CreateReportingContext(Guid tenantId, string? scope = null) =>
        ReportingRequestContext.TryCreate(
            FoundationRequestContext.ForTenant(
                Actor,
                Guid.NewGuid(),
                CreateTenantContext(tenantId, scope),
                "tenant.reporting.report.view"),
            out var context)
            ? context!
            : throw new InvalidOperationException("The test context could not be created.");

    private static TenantContext CreateTenantContext(Guid tenantId, string? scope = null) =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(tenantId),
            new MembershipReference(Guid.NewGuid()),
            scope is null ? null : new ScopeReference(scope),
            correlationId: new CorrelationId($"hold1-{tenantId:N}"[..Math.Min(32, $"hold1-{tenantId:N}".Length)]),
            actorId: Actor);

    private static async Task AppendAuditEvidence(
        LocalImmutableAuditEvidenceStore store,
        TenantContext tenantContext,
        string scope,
        string correlation,
        DateTimeOffset occurredAt)
    {
        var context = FoundationRequestContext.ForTenant(
            Actor,
            Guid.NewGuid(),
            TenantContext.ForOrdinaryMembership(
                tenantContext.TenantId,
                tenantContext.Membership!.Value,
                new ScopeReference(scope),
                new CorrelationId(correlation),
                Actor),
            "foundation.probe.write");
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            context,
            "foundation.probe.write",
            correlation,
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            occurredAt: occurredAt));
    }

    private sealed class AllowingScopeResolver(Guid? allowedCompany = null) : IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope)
        {
            if (requestedScope.CompanyId is { } company && allowedCompany is { } expected && company != expected)
                return TenantWorkScopeResolution.Denied("scope_not_authorized");

            return TenantWorkScopeResolution.Resolved(
                TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
        }
    }

    private sealed class HierarchicalScopeResolver : IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope)
        {
            var marker = trustedTenantContext.Scope?.Value;
            var allowed = marker switch
            {
                { } value when value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase) =>
                    requestedScope.CompanyId == Company,
                { } value when value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase) =>
                    requestedScope.CompanyId == Company && requestedScope.BranchId == Branch,
                { } value when value.StartsWith("Warehouse:", StringComparison.OrdinalIgnoreCase) =>
                    requestedScope.CompanyId == Company && requestedScope.BranchId == Branch && requestedScope.WarehouseId == Warehouse,
                _ => requestedScope.CompanyId is null || requestedScope.CompanyId == Company
            };

            return allowed
                ? TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope))
                : TenantWorkScopeResolution.Denied("scope_not_authorized");
        }
    }

    private sealed class CurrentWarehouseScopeResolver : IOrganizationScopeOwnershipResolver, ICurrentOrganizationScopeResolver
    {
        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) =>
            requestedScope.CompanyId == Company
                && requestedScope.BranchId == Branch
                && requestedScope.WarehouseId == Warehouse
                ? TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope))
                : TenantWorkScopeResolution.Denied("scope_not_authorized");

        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(
                TenantWorkScope.IssueFromVerifiedAuthority(
                    trustedTenantContext,
                    TenantWorkScopeRequest.ForWarehouse(Company, Branch, Warehouse)));
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
