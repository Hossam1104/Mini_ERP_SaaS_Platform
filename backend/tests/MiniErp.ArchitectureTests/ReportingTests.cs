using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Reporting;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class ReportingTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Actor = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Company = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid OtherCompany = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void Reporting_operations_are_tenant_scoped_and_mutations_are_explicit()
    {
        var catalogue = FoundationOperationCatalog.PublicOperations
            .Where(item => item.OperationId.StartsWith("reporting.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(8, catalogue.Length);
        Assert.All(catalogue, item =>
        {
            Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, item.SecurityProfile);
            Assert.Equal(FoundationScopePolicy.Tenant, item.ScopePolicy);
            Assert.NotNull(item.ExactPermissionCode);
        });

        Assert.All(catalogue.Where(item => item.HttpMethod == "POST"), item =>
        {
            Assert.True(item.RequiresAntiforgery);
            Assert.True(item.RequiresMandatoryAudit);
            Assert.True(item.IsUnsafe);
            Assert.Equal(FoundationIdempotencyPolicy.Required, item.Idempotency);
        });
    }

    [Fact]
    public void Authorization_requires_the_catalogue_permission_and_owned_company()
    {
        var provider = new ConfiguredFinanceCompanyProvider([
            new FinanceCompanyOption(Tenant, Company, "Test Company", "SAR")]);
        var authorization = new ReportingAuthorizationService(provider);
        var definition = new ReportingDefinition("finance.trial-balance", "Trial balance", "ميزان المراجعة", "Finance", "1.0", "Finance", "Finance", ["company"], true, false, true, false);
        var query = new ReportingQuery(CompanyId: Company);

        var allowed = authorization.Resolve(Context("tenant.reporting.report.view"), "reporting.report.execute", definition, query);
        Assert.True(allowed.Succeeded, allowed.Code);

        var wrongPermission = authorization.Resolve(Context("tenant.reporting.schedule.view"), "reporting.report.execute", definition, query);
        Assert.False(wrongPermission.Succeeded);
        Assert.Equal("permission_denied", wrongPermission.Code);

        var foreignCompany = authorization.Resolve(Context("tenant.reporting.report.view"), "reporting.report.execute", definition, query with { CompanyId = OtherCompany });
        Assert.False(foreignCompany.Succeeded);
        Assert.Equal("company_scope_denied", foreignCompany.Code);
    }

    [Fact]
    public void Runtime_idempotency_is_actor_and_tenant_bound()
    {
        var store = new ReportingRuntimeStore();
        var now = DateTimeOffset.UtcNow;
        var job = new ReportingJobRecord(Guid.NewGuid(), Tenant, Actor, "sales.orders", "fingerprint-a", ReportingJobStatus.Completed, now, now, null, null, null);
        store.SaveJob(job, "export-key");

        var replay = store.FindJobByIdempotency(Tenant, Actor, "export-key", "fingerprint-a", out var conflict);
        Assert.False(conflict);
        Assert.Equal(job.JobId, replay?.JobId);

        var mismatch = store.FindJobByIdempotency(Tenant, Actor, "export-key", "fingerprint-b", out conflict);
        Assert.True(conflict);
        Assert.Null(mismatch);
        Assert.Null(store.FindJob(Guid.NewGuid(), Actor, job.JobId));
    }

    [Fact]
    public void Branch_context_cannot_be_expanded_to_another_branch()
    {
        var branch = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var otherBranch = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var provider = new ConfiguredFinanceCompanyProvider([
            new FinanceCompanyOption(Tenant, Company, "Test Company", "SAR", branch)]);
        var authorization = new ReportingAuthorizationService(provider);
        var definition = new ReportingDefinition("inventory.valuation", "Inventory valuation", "تقييم المخزون", "Inventory", "1.0", "Inventory", "Inventory", ["company", "branch"], true, false, true, false);

        var allowed = authorization.Resolve(Context("tenant.reporting.report.view", new ScopeReference($"Branch:{branch:D}")), "reporting.report.execute", definition, new ReportingQuery());
        Assert.True(allowed.Succeeded, allowed.Code);

        var denied = authorization.Resolve(Context("tenant.reporting.report.view", new ScopeReference($"Branch:{branch:D}")), "reporting.report.execute", definition, new ReportingQuery(Company, otherBranch));
        Assert.False(denied.Succeeded);
        Assert.Equal("branch_scope_denied", denied.Code);
    }

    [Fact]
    public void Schedule_idempotency_and_versioned_status_are_atomic()
    {
        var store = new ReportingRuntimeStore();
        var now = DateTimeOffset.UtcNow;
        var schedule = new ReportingScheduleRecord(Guid.NewGuid(), Tenant, Actor, "finance.trial-balance", new ReportingQuery(Company), "0 09 * * 1", "UTC", ReportingScheduleStatus.Disabled, "local-test-sink", now, now, [1]);
        store.SaveSchedule(schedule, "schedule-key", "fingerprint-a");

        var replay = store.FindScheduleByIdempotency(Tenant, Actor, "schedule-key", "fingerprint-a", out var conflict);
        Assert.False(conflict);
        Assert.Equal(schedule.ScheduleId, replay?.ScheduleId);

        var mismatch = store.FindScheduleByIdempotency(Tenant, Actor, "schedule-key", "fingerprint-b", out conflict);
        Assert.True(conflict);
        Assert.Null(mismatch);

        var updated = store.TryUpdateSchedule(Tenant, Actor, schedule.ScheduleId, ReportingScheduleStatus.Enabled, [1], "update-key", "update-fingerprint-a", out var code);
        Assert.Equal("succeeded", code);
        Assert.Equal(ReportingScheduleStatus.Enabled, updated?.Status);

        var replayUpdate = store.TryUpdateSchedule(Tenant, Actor, schedule.ScheduleId, ReportingScheduleStatus.Enabled, [1], "update-key", "update-fingerprint-a", out code);
        Assert.Equal("succeeded", code);
        Assert.Equal(updated?.Version, replayUpdate?.Version);

        var mismatchUpdate = store.TryUpdateSchedule(Tenant, Actor, schedule.ScheduleId, ReportingScheduleStatus.Disabled, [1], "update-key", "update-fingerprint-b", out code);
        Assert.Null(mismatchUpdate);
        Assert.Equal("idempotency_conflict", code);

        var stale = store.TryUpdateSchedule(Tenant, Actor, schedule.ScheduleId, ReportingScheduleStatus.Disabled, [1], "other-update-key", "update-fingerprint-c", out code);
        Assert.Null(stale);
        Assert.Equal("concurrency_conflict", code);
    }

    private static FoundationRequestContext Context(string permission, ScopeReference? scope = null)
    {
        var tenantContext = TenantContext.ForOrdinaryMembership(
            new TenantId(Tenant),
            new MembershipReference(Guid.NewGuid()),
            scope: scope,
            correlationId: new CorrelationId("reporting-test"),
            actorId: Actor);
        return FoundationRequestContext.ForTenant(Actor, Guid.NewGuid(), tenantContext, permission);
    }
}
