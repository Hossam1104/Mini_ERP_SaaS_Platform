using System.Text;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Notifications;
using MiniErp.Contracts.Modules.Audit;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class Mesp140CrossCuttingControlTests
{
    private static readonly TenantId TenantA = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly TenantId TenantB = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    private static readonly Guid ActorA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SessionA = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Audit_search_is_bounded_filtered_and_tenant_bound()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var contextA = OrdinaryContext(TenantA, ActorA, SessionA, "Tenant");
        var contextB = OrdinaryContext(TenantB, Guid.NewGuid(), Guid.NewGuid(), "Tenant");
        var scopeA = TenantWorkScope.IssueFromVerifiedAuthority(contextA.TenantContext!, TenantWorkScopeRequest.TenantWide());
        var scopeB = TenantWorkScope.IssueFromVerifiedAuthority(contextB.TenantContext!, TenantWorkScopeRequest.TenantWide());

        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            contextA,
            "finance.journal.post",
            "corr-audit-a-1",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            source: "finance",
            targetType: "journal",
            targetReference: "journal-a",
            changeSummary: "journal posted"));
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            contextA,
            "finance.journal.post",
            "corr-audit-a-2",
            FoundationAuditDecision.Denied,
            FoundationAuditReason.PermissionDenied,
            source: "security-control",
            targetType: "journal",
            targetReference: "journal-b",
            changeSummary: "permission denied"));
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            contextB,
            "finance.journal.post",
            "corr-audit-b-1",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            source: "finance"));

        var search = FoundationAuditSearch.Create(
            ReportingPageRequest.Create(1, 10, "occurredAt", "desc"),
            source: "security-control",
            decision: FoundationAuditDecision.Denied,
            targetType: "journal");
        var result = await store.SearchAsync(contextA.TenantContext!, scopeA, search);

        var row = Assert.Single(result.Rows);
        Assert.Equal(TenantA.Value, row.TenantId);
        Assert.Equal("security-control", row.Source);
        Assert.Equal("journal-b", row.TargetReference);
        Assert.Equal(1, result.TotalRows);

        var foreignSearch = FoundationAuditSearch.Create(ReportingPageRequest.Create(1, 10, null, "desc"));
        var foreignResult = await store.SearchAsync(contextA.TenantContext!, scopeB, foreignSearch);
        Assert.Empty(foreignResult.Rows);
    }

    [Fact]
    public void Audit_search_rejects_unbounded_windows_and_deep_pages()
    {
        Assert.Throws<ArgumentException>(() => FoundationAuditSearch.Create(
            ReportingPageRequest.Create(1, 10, null, "desc"),
            from: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)));
        Assert.Throws<ArgumentException>(() => FoundationAuditSearch.Create(
            ReportingPageRequest.Create(100_001, 10, null, "desc")));
    }

    [Fact]
    public async Task Private_file_clock_and_scan_state_are_honest()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        var context = OrdinaryTenantContext(TenantA, ActorA, "Tenant");
        var scope = TenantWorkScope.IssueFromVerifiedAuthority(context, TenantWorkScopeRequest.TenantWide());
        var storage = new InMemoryPrivateObjectStorage(clock);

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("private"));
        var metadata = await storage.StoreAsync(context, scope, "private.txt", "text/plain", content, clock.GetUtcNow().AddMinutes(5));

        Assert.Equal(clock.GetUtcNow(), metadata.CreatedAt);
        Assert.Equal(PrivateFileScanState.Unavailable, metadata.ScanState);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(PrivateFileAccessOutcome.Expired, (await storage.ReadAsync(context, metadata.ObjectId)).Outcome);
    }

    [Fact]
    public async Task Notification_dispatch_audits_local_evidence_and_rejects_foreign_scope()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 8, 13, 0, 0, TimeSpan.Zero));
        var context = OrdinaryRequestContext(TenantA, ActorA, SessionA, "Tenant");
        var scope = TenantWorkScope.IssueFromVerifiedAuthority(context.TenantContext!, TenantWorkScopeRequest.TenantWide());
        var auditStore = new LocalImmutableAuditEvidenceStore();
        var audit = CreateAudit(auditStore, clock);
        var application = new NotificationDeliveryApplication(
            new ApprovingRecipientAuthorizer(),
            new InMemoryNotificationAdapter(),
            audit,
            clock);

        var delivered = await application.DispatchAsync(
            context,
            scope,
            new NotificationRecipientReference(Guid.NewGuid()),
            "invoice-ready",
            "en",
            "notification-mesp140-1");

        Assert.Equal(NotificationRequestOutcome.Delivered, delivered.Outcome);
        Assert.Equal("local-test-adapter", delivered.EvidenceSource);
        Assert.Contains((await auditStore.ReadForTenantAsync(context.TenantContext!)), item => item.Source == "notification-control");

        var unavailable = new NotificationDeliveryApplication(
            new ApprovingRecipientAuthorizer(),
            new UnavailableNotificationDeliveryAdapter(),
            audit,
            clock);
        var providerMissing = await unavailable.DispatchAsync(
            context,
            scope,
            new NotificationRecipientReference(Guid.NewGuid()),
            "invoice-ready",
            "en",
            "notification-mesp140-no-provider");
        Assert.Equal(NotificationRequestOutcome.Unavailable, providerMissing.Outcome);
        Assert.Equal("no-provider", providerMissing.EvidenceSource);

        var foreignContext = OrdinaryTenantContext(TenantB, Guid.NewGuid(), "Tenant");
        var foreignScope = TenantWorkScope.IssueFromVerifiedAuthority(foreignContext, TenantWorkScopeRequest.TenantWide());
        var denied = await application.DispatchAsync(
            context,
            foreignScope,
            new NotificationRecipientReference(Guid.NewGuid()),
            "invoice-ready",
            "en",
            "notification-mesp140-2");

        Assert.Equal(NotificationRequestOutcome.Denied, denied.Outcome);
        Assert.Null(denied.IntentId);
    }

    [Fact]
    public async Task Support_session_is_evidence_only_and_revalidates_expiry_and_revocation()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 8, 14, 0, 0, TimeSpan.Zero));
        var tenant = new TenantId(Guid.NewGuid());
        var actor = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grantId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var supportContext = TenantContext.ForSupportGrant(
            tenant,
            new SupportGrantReference(grantId, caseId),
            new ScopeReference("Tenant"),
            new CorrelationId("corr-support-mesp140"),
            actor);
        var requestContext = FoundationRequestContext.ForTenant(actor, sessionId, supportContext, "support.tenant.read");
        var validator = new MutableSupportValidator(clock, clock.GetUtcNow().AddHours(1));
        var sessions = new SupportAccessSessionStore(validator, clock);

        var opened = await sessions.OpenAsync(requestContext, "incident diagnosis", "support.tenant.read", TimeSpan.FromHours(2));
        Assert.True(opened.Succeeded);
        Assert.NotNull(opened.Session);
        Assert.Equal(SupportAccessSessionState.Active, opened.Session!.State);
        Assert.Equal(clock.GetUtcNow().AddHours(1), opened.Session.ExpiresAt);
        Assert.Null(typeof(SupportAccessSession).GetMethod("CreateTenantContext"));

        clock.Advance(TimeSpan.FromHours(1));
        var expired = await sessions.RevalidateAsync(requestContext, opened.Session.SessionId);
        Assert.False(expired.Succeeded);
        Assert.Equal("support_session_expired", expired.SafeCode);

        clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 8, 15, 0, 0, TimeSpan.Zero));
        validator = new MutableSupportValidator(clock, clock.GetUtcNow().AddHours(1));
        sessions = new SupportAccessSessionStore(validator, clock);
        opened = await sessions.OpenAsync(requestContext, "incident diagnosis", "support.tenant.read", TimeSpan.FromMinutes(30));
        Assert.True(opened.Succeeded);
        Assert.True(sessions.Revoke(requestContext, opened.Session!.SessionId));
        var revoked = await sessions.RevalidateAsync(requestContext, opened.Session.SessionId);
        Assert.False(revoked.Succeeded);
        Assert.Equal("support_session_revoked", revoked.SafeCode);
    }

    private static FoundationAuditCoordinator CreateAudit(LocalImmutableAuditEvidenceStore store, TimeProvider clock) =>
        new(store, new LocalFoundationAuditTelemetrySink(), new LocalFoundationAuditOperationalSignalSink(), clock);

    private static FoundationRequestContext OrdinaryRequestContext(TenantId tenant, Guid actor, Guid session, string scope) =>
        FoundationRequestContext.ForTenant(actor, session, OrdinaryTenantContext(tenant, actor, scope), "tenant.audit.read");

    private static TenantContext OrdinaryTenantContext(TenantId tenant, Guid actor, string scope) =>
        TenantContext.ForOrdinaryMembership(
            tenant,
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference(scope),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            actor);

    private static FoundationRequestContext OrdinaryContext(TenantId tenant, Guid actor, Guid session, string scope) =>
        FoundationRequestContext.ForTenant(actor, session, OrdinaryTenantContext(tenant, actor, scope), "tenant.audit.read");

    private sealed class ApprovingRecipientAuthorizer : INotificationRecipientAuthorizer
    {
        public ValueTask<NotificationRecipientAuthorizationResult> AuthorizeAsync(
            TenantContext currentTenantContext,
            NotificationRecipientReference recipient,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(NotificationRecipientAuthorizationResult.Approved(
                new VerifiedNotificationRecipient(currentTenantContext.TenantId, recipient.UserId)));
    }

    private sealed class MutableSupportValidator : ISupportAccessContextValidator
    {
        private readonly ManualTimeProvider clock;
        private readonly DateTimeOffset grantExpiresAt;

        public MutableSupportValidator(ManualTimeProvider clock, DateTimeOffset grantExpiresAt)
        {
            this.clock = clock;
            this.grantExpiresAt = grantExpiresAt;
        }

        public ValueTask<SupportAccessValidationResult> ValidateAsync(
            FoundationRequestContext trustedRequestContext,
            string purpose,
            string permission,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(clock.GetUtcNow() >= grantExpiresAt
                ? new SupportAccessValidationResult(SupportAccessValidationState.Expired, grantExpiresAt, "support_expired")
                : new SupportAccessValidationResult(SupportAccessValidationState.Active, grantExpiresAt, "support_active"));
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset current;

        public ManualTimeProvider(DateTimeOffset current) => this.current = current;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
