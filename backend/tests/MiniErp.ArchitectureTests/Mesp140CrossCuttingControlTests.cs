using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task Notification_http_shipping_path_enforces_authorization_audit_idempotency_and_provider_truth()
    {
        using var factory = new NotificationApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiForgery = await client.GetAsync("/api/v1/auth/antiforgery");
        var antiForgeryToken = antiForgery.Headers.GetValues("X-CSRF-TOKEN").Single();
        var request = new NotificationDispatchRequest(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "invoice-ready",
            "en");

        var first = await PostNotificationAsync(client, request, antiForgeryToken, "http-notification-mesp140-1");
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<NotificationDispatchResponse>();
        Assert.NotNull(firstBody);
        Assert.Equal(NotificationRequestOutcome.Delivered, firstBody.Outcome);
        Assert.Equal("local-test-adapter", firstBody.EvidenceSource);
        Assert.Equal(1, factory.Adapter.AuditEvidenceCountAtFirstEffect);

        var duplicate = await PostNotificationAsync(client, request, antiForgeryToken, "http-notification-mesp140-1");
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<NotificationDispatchResponse>();
        Assert.NotNull(duplicateBody);
        Assert.Equal(NotificationDeliveryState.Duplicate, duplicateBody.DeliveryState);
        Assert.Equal("duplicate", duplicateBody.SafeCode);
        Assert.Equal(2, factory.Adapter.EffectCallCount);

        var missingIdempotencyKey = await PostNotificationAsync(client, request, antiForgeryToken, idempotencyKey: null);
        Assert.Equal(HttpStatusCode.BadRequest, missingIdempotencyKey.StatusCode);
        Assert.Equal(2, factory.Adapter.EffectCallCount);

        var invalidValidation = await PostNotificationAsync(
            client,
            request with { Locale = "fr-invalid" },
            antiForgeryToken,
            "http-notification-mesp140-validation-failed");
        Assert.Equal(HttpStatusCode.BadRequest, invalidValidation.StatusCode);
        var invalidValidationBody = await invalidValidation.Content.ReadFromJsonAsync<NotificationDispatchResponse>();
        Assert.NotNull(invalidValidationBody);
        Assert.Equal(NotificationRequestOutcome.ValidationFailed, invalidValidationBody.Outcome);
        Assert.Equal(2, factory.Adapter.EffectCallCount);

        factory.RecipientAuthorizer.Allowed = false;
        var recipientDenied = await PostNotificationAsync(
            client,
            request,
            antiForgeryToken,
            "http-notification-mesp140-recipient-denied");
        Assert.Equal(HttpStatusCode.Forbidden, recipientDenied.StatusCode);
        Assert.Equal(2, factory.Adapter.EffectCallCount);

        factory.RecipientAuthorizer.Allowed = true;
        factory.ScopeResolver.Allowed = false;
        var scopeDenied = await PostNotificationAsync(
            client,
            request,
            antiForgeryToken,
            "http-notification-mesp140-scope-denied");
        Assert.Equal(HttpStatusCode.Forbidden, scopeDenied.StatusCode);
        Assert.Equal(2, factory.Adapter.EffectCallCount);

        factory.ScopeResolver.Allowed = true;
        factory.Adapter.UseProvider = false;
        var unavailable = await PostNotificationAsync(
            client,
            request,
            antiForgeryToken,
            "http-notification-mesp140-no-provider");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        var unavailableBody = await unavailable.Content.ReadFromJsonAsync<NotificationDispatchResponse>();
        Assert.NotNull(unavailableBody);
        Assert.Equal(NotificationRequestOutcome.Unavailable, unavailableBody.Outcome);
        Assert.Equal("no-provider", unavailableBody.EvidenceSource);

        factory.Adapter.UseProvider = true;
        factory.Adapter.ThrowOnEffect = true;
        var unknown = await PostNotificationAsync(
            client,
            request,
            antiForgeryToken,
            "http-notification-mesp140-unknown");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unknown.StatusCode);
        var unknownBody = await unknown.Content.ReadFromJsonAsync<NotificationDispatchResponse>();
        Assert.NotNull(unknownBody);
        Assert.Equal(NotificationRequestOutcome.Unknown, unknownBody.Outcome);
        Assert.Equal("delivery_outcome_unknown", unknownBody.SafeCode);

        var evidence = await factory.AuditStore.ReadForTenantAsync(factory.Context.TenantContext!);
        Assert.Contains(evidence, item => item.OperationId == "notification.intent.dispatch" && item.ChangeSummary == "notification request accepted");
        Assert.Contains(evidence, item => item.OperationId == "notification.intent.dispatch" && item.ChangeSummary == "duplicate");
        Assert.Contains(evidence, item => item.OperationId == "notification.intent.dispatch" && item.ChangeSummary == "provider_unavailable");
        Assert.Contains(evidence, item => item.OperationId == "notification.intent.dispatch" && item.Decision == FoundationAuditDecision.EffectFailed);
    }

    [Fact]
    public async Task Notification_http_dispatch_uses_the_caller_current_scope_not_a_hardcoded_tenant_wide_scope()
    {
        using var factory = new NotificationApiFactory();
        var companyId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var branchId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        factory.ScopeResolver.CurrentScopeRequest = TenantWorkScopeRequest.ForBranch(companyId, branchId);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var antiForgery = await client.GetAsync("/api/v1/auth/antiforgery");
        var antiForgeryToken = antiForgery.Headers.GetValues("X-CSRF-TOKEN").Single();
        var request = new NotificationDispatchRequest(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "invoice-ready",
            "en");

        var response = await PostNotificationAsync(client, request, antiForgeryToken, "http-notification-mesp140-scope-preserved");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(factory.ScopeResolver.LastResolvedRequest);
        Assert.Equal(companyId, factory.ScopeResolver.LastResolvedRequest!.CompanyId);
        Assert.Equal(branchId, factory.ScopeResolver.LastResolvedRequest!.BranchId);
        Assert.Null(factory.ScopeResolver.LastResolvedRequest!.WarehouseId);
    }

    [Fact]
    public async Task Support_context_shipping_path_uses_identity_authority_and_records_audit()
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
        var auditStore = new LocalImmutableAuditEvidenceStore();
        var application = new FoundationRestApplication(CreateAudit(auditStore, clock), timeProvider: clock);

        var result = await application.ReadSupportContextAsync(requestContext, "corr-support-mesp140");

        Assert.True(result.Succeeded);
        var evidence = Assert.Single(await auditStore.ReadForTenantAsync(supportContext));
        Assert.Equal("foundation.support-context.read", evidence.OperationId);
        Assert.Equal(FoundationAuditAuthorizationPath.SupportGrant, evidence.AuthorizationPath);
        Assert.Equal(grantId, evidence.SupportGrantId);
        Assert.Equal(caseId, evidence.SupportCaseId);
        Assert.Equal("support-context-read", evidence.Purpose);

        var ordinary = FoundationRequestContext.ForTenant(
            actor,
            sessionId,
            TenantContext.ForOrdinaryMembership(
                tenant,
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Tenant"),
                new CorrelationId("corr-support-denied"),
                actor),
            "support.tenant.read");
        var denied = await application.ReadSupportContextAsync(ordinary, "corr-support-denied");
        Assert.False(denied.Succeeded);
    }

    private static FoundationAuditCoordinator CreateAudit(LocalImmutableAuditEvidenceStore store, TimeProvider clock) =>
        new(store, new LocalFoundationAuditTelemetrySink(), new LocalFoundationAuditOperationalSignalSink(), clock);

    private static async Task<HttpResponseMessage> PostNotificationAsync(
        HttpClient client,
        NotificationDispatchRequest request,
        string antiForgeryToken,
        string? idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notifications")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiForgeryToken);
        if (idempotencyKey is not null)
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(message);
    }

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

    private sealed class NotificationApiFactory : WebApplicationFactory<Program>
    {
        internal readonly NotificationApiResolver Resolver = new();
        internal readonly NotificationScopeResolver ScopeResolver = new();
        internal readonly NotificationRecipientAuthorizer RecipientAuthorizer = new();
        internal readonly LocalImmutableAuditEvidenceStore AuditStore = new();
        internal readonly RecordingNotificationAdapter Adapter;
        internal readonly FoundationRequestContext Context;

        internal NotificationApiFactory()
        {
            var tenant = new TenantId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            var actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Context = FoundationRequestContext.ForTenant(
                actor,
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TenantContext.ForOrdinaryMembership(
                    tenant,
                    new MembershipReference(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                    new ScopeReference("Tenant"),
                    new CorrelationId("corr-http-notification-mesp140"),
                    actor),
                "tenant.notification.dispatch");
            Resolver.Context = Context;
            Adapter = new RecordingNotificationAdapter(AuditStore);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MESP_SQLSERVER_CONNECTION_STRING"] = " "
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITrustedRequestContextResolver>();
                services.AddSingleton<ITrustedRequestContextResolver>(Resolver);
                services.RemoveAll<IOrganizationScopeOwnershipResolver>();
                services.AddSingleton<IOrganizationScopeOwnershipResolver>(ScopeResolver);
                services.RemoveAll<ICurrentOrganizationScopeResolver>();
                services.AddSingleton<ICurrentOrganizationScopeResolver>(ScopeResolver);
                services.RemoveAll<INotificationRecipientAuthorizer>();
                services.AddSingleton<INotificationRecipientAuthorizer>(RecipientAuthorizer);
                services.RemoveAll<INotificationDeliveryAdapter>();
                services.AddSingleton<INotificationDeliveryAdapter>(Adapter);
                services.RemoveAll<IFoundationAuditEvidenceSink>();
                services.AddSingleton<IFoundationAuditEvidenceSink>(AuditStore);
            });
        }
    }

    private sealed class NotificationApiResolver : ITrustedRequestContextResolver
    {
        internal FoundationRequestContext Context { get; set; } = FoundationRequestContext.Unauthenticated();

        public ValueTask<FoundationRequestContext> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Context);
    }

    private sealed class NotificationScopeResolver : IOrganizationScopeOwnershipResolver, ICurrentOrganizationScopeResolver
    {
        internal bool Allowed { get; set; } = true;

        internal TenantWorkScopeRequest CurrentScopeRequest { get; set; } = TenantWorkScopeRequest.TenantWide();

        internal TenantWorkScopeRequest? LastResolvedRequest { get; private set; }

        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope)
        {
            LastResolvedRequest = requestedScope;
            return Allowed
                ? TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope))
                : TenantWorkScopeResolution.Denied("scope_denied");
        }

        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            Allowed
                ? TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, CurrentScopeRequest))
                : TenantWorkScopeResolution.Denied("scope_denied");
    }

    private sealed class NotificationRecipientAuthorizer : INotificationRecipientAuthorizer
    {
        internal bool Allowed { get; set; } = true;

        public ValueTask<NotificationRecipientAuthorizationResult> AuthorizeAsync(
            TenantContext currentTenantContext,
            NotificationRecipientReference recipient,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Allowed
                ? NotificationRecipientAuthorizationResult.Approved(
                    new VerifiedNotificationRecipient(currentTenantContext.TenantId, recipient.UserId))
                : NotificationRecipientAuthorizationResult.Denied("recipient_denied"));
    }

    private sealed class RecordingNotificationAdapter : INotificationDeliveryAdapter
    {
        private readonly LocalImmutableAuditEvidenceStore auditStore;
        private readonly InMemoryNotificationAdapter local = new();
        private readonly UnavailableNotificationDeliveryAdapter unavailable = new();

        internal RecordingNotificationAdapter(LocalImmutableAuditEvidenceStore auditStore) => this.auditStore = auditStore;

        internal bool UseProvider { get; set; } = true;
        internal bool ThrowOnEffect { get; set; }
        internal int EffectCallCount { get; private set; }
        internal int AuditEvidenceCountAtFirstEffect { get; private set; }

        public async ValueTask<NotificationDeliveryResult> DeliverAsync(
            TenantContext tenantContext,
            TenantNotificationIntent intent,
            CancellationToken cancellationToken = default)
        {
            EffectCallCount++;
            AuditEvidenceCountAtFirstEffect = Math.Max(
                AuditEvidenceCountAtFirstEffect,
                (await auditStore.ReadForTenantAsync(tenantContext, cancellationToken)).Count);
            if (ThrowOnEffect)
            {
                throw new InvalidOperationException("test adapter failure");
            }

            return UseProvider
                ? await local.DeliverAsync(tenantContext, intent, cancellationToken)
                : await unavailable.DeliverAsync(tenantContext, intent, cancellationToken);
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
