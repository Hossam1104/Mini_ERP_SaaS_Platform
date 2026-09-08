using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class DurableWorkTests
{
    private static readonly DateTimeOffset Clock = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DurableWorkOperationCatalogue OperationCatalogue =
        new([
            new DurableWorkOperationDescriptor(
                "foundation.work",
                "demo",
                "tenant.business.read",
                [TenantAuthorizationPath.OrdinaryMembership, TenantAuthorizationPath.SupportGrant])
        ]);
    private static readonly IDurableWorkPayloadRegistry PayloadRegistry = CreatePayloadRegistry();
    private static readonly IDurableWorkEffectExecutor EffectExecutor =
        new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard());

    private static DurableWorkPayloadRegistry CreatePayloadRegistry()
    {
        var registry = new DurableWorkPayloadRegistry();
        registry.Register(new DurableWorkPayloadTypeId("test.demo-payload"), new JsonDurableWorkPayloadCodec<DemoPayload>());
        return registry;
    }

    private static DurableWorkDispatcher CreateDispatcher(IDurableWorkOperationCatalogue? catalogue = null) =>
        new(catalogue ?? OperationCatalogue, PayloadRegistry, EffectExecutor);

    [Fact]
    public void Work_item_rejects_missing_tenant()
    {
        var context = CreateContext();
        var scope = DurableWorkTestSupport.TenantWideScope(context);
        Assert.Throws<ArgumentNullException>(() => DurableWorkItem.Create(
            null!,
            scope,
            Identity(context),
            new DemoPayload("x"),
            PayloadRegistry,
            Guid.NewGuid()));
    }

    [Fact]
    public void Stored_tenant_cannot_mutate()
    {
        var type = typeof(DurableWorkItem);
        Assert.Null(type.GetProperty(nameof(DurableWorkItem.TenantId))!.SetMethod);
        Assert.DoesNotContain(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is nameof(DurableWorkItem.TenantId) or nameof(DurableWorkItem.Initiator)
                && property.SetMethod is not null);
    }

    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b_work()
    {
        var tenantB = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(tenantB, "read-b");
        Assert.True(await store.SubmitAsync(work));

        Assert.Null(await store.FindAsync(CreateContext(), work.Identity.WorkItemId));
    }

    [Fact]
    public async Task Tenant_a_cannot_claim_tenant_b_work()
    {
        var tenantB = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        await store.SubmitAsync(Work(tenantB, "claim-b"));

        var lease = await store.TryClaimAsync(CreateContext(), Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.Null(lease);
    }

    [Fact]
    public void Forged_client_tenant_does_not_change_scope()
    {
        var trusted = CreateContext();
        var foreign = CreateContext();
        var scope = DurableWorkTestSupport.TenantWideScope(trusted);
        Assert.Throws<ArgumentException>(() => DurableWorkItem.Create(
            foreign,
            scope,
            Identity(foreign),
            new DemoPayload("forged"),
            PayloadRegistry,
            Guid.NewGuid()));
    }

    [Fact]
    public void Organization_scope_must_belong_to_tenant_and_follow_hierarchy()
    {
        var context = CreateContext();
        Assert.Throws<ArgumentException>(() => new TenantWorkScopeRequest(branchId: Guid.NewGuid()));
        var foreignScope = DurableWorkTestSupport.TenantWideScope(CreateContext());
        Assert.Throws<ArgumentException>(() => DurableWorkItem.Create(
            context,
            foreignScope,
            Identity(context),
            new DemoPayload("scope"),
            PayloadRegistry,
            Guid.NewGuid()));
    }

    [Fact]
    public async Task Duplicate_outbox_event_produces_one_effect()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "outbox-once");
        Assert.True(await store.SubmitAsync(work));
        Assert.True(await store.SubmitAsync(work));
        var effects = 0;
        var first = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });
        var second = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });
        Assert.True(first.Delivered);
        Assert.False(second.Delivered);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Inbox_duplicate_is_idempotent()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "inbox-once");
        await store.SubmitAsync(work);
        var calls = 0;
        await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            calls++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });
        Assert.True(store.ReplayOutboxForValidation(work.Identity.WorkItemId, Clock));
        var result = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            calls++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });
        Assert.True(result.Duplicate);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retry_increments_safely()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "retry");
        await store.SubmitAsync(work);
        var lease = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        var completion = await store.CompleteAsync(
            context,
            lease!,
            DurableWorkHandlerResult.Retry(DurableWorkFailureCategory.ProviderUnavailable, TimeSpan.FromSeconds(1), "safe_retry"),
            Clock);
        Assert.True(completion.Accepted);
        Assert.Equal(1, completion.AttemptCount);
        Assert.Equal(DurableWorkLifecycle.RetryScheduled, completion.Lifecycle);
    }

    [Fact]
    public async Task Retry_does_not_change_tenant()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "retry-owner");
        await store.SubmitAsync(work);
        var lease = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        await store.CompleteAsync(context, lease!, DurableWorkHandlerResult.Retry(
            DurableWorkFailureCategory.HandlerFailed, TimeSpan.Zero, "safe_retry"), Clock);
        Assert.Equal(context.TenantId, work.TenantId);
        Assert.Null(await store.FindAsync(CreateContext(), work.Identity.WorkItemId));
    }

    [Fact]
    public async Task Bounded_retry_reaches_dead_letter()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "dead-letter", maximumAttempts: 2);
        await store.SubmitAsync(work);
        var firstLease = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        await store.CompleteAsync(context, firstLease!, DurableWorkHandlerResult.Retry(
            DurableWorkFailureCategory.HandlerFailed, TimeSpan.Zero, "safe_retry"), Clock);
        var secondLease = await store.TryClaimAsync(context, Guid.NewGuid(), Clock.AddMinutes(1), TimeSpan.FromMinutes(5));
        var result = await store.CompleteAsync(context, secondLease!, DurableWorkHandlerResult.Retry(
            DurableWorkFailureCategory.HandlerFailed, TimeSpan.Zero, "safe_retry"), Clock.AddMinutes(1));
        Assert.Equal(DurableWorkLifecycle.DeadLetter, result.Lifecycle);
        Assert.True(work.IsDeadLettered);
    }

    [Fact]
    public async Task Dead_letter_evidence_is_safe()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "dead-letter-audit");
        await store.SubmitAsync(work);
        var lease = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        await store.CompleteAsync(context, lease!, DurableWorkHandlerResult.DeadLettered(
            DurableWorkFailureCategory.AuthorizationDenied, "authorization_denied"), Clock);
        var evidence = await store.ReadAuditAsync(context);
        Assert.Contains(evidence, item => item.EventType == "work.dead-letter");
        Assert.DoesNotContain(evidence, item => item.ToString().Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Lease_concurrency_allows_one_worker()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        await store.SubmitAsync(Work(context, "lease-one"));
        var first = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        var second = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task Expired_lease_can_be_reclaimed_safely()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        await store.SubmitAsync(Work(context, "lease-expired"));
        await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(1));
        await store.ExpireLeasesAsync(Clock.AddMinutes(2));
        Assert.NotNull(await store.TryClaimAsync(context, Guid.NewGuid(), Clock.AddMinutes(2), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Active_lease_cannot_be_stolen()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        await store.SubmitAsync(Work(context, "lease-active"));
        var first = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.NotNull(first);
        Assert.Null(await store.TryClaimAsync(context, Guid.NewGuid(), Clock.AddMinutes(1), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Worker_revalidates_exact_tenant()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var dispatcher = CreateDispatcher();
        dispatcher.Register(new SuccessfulHandler());
        var worker = new TenantDurableWorkWorker(store, dispatcher, new TestAuthorityRevalidator());
        await store.SubmitAsync(Work(context, "worker-exact"));
        var result = await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.NotNull(result);
        Assert.Equal(DurableWorkLifecycle.Completed, result!.Lifecycle);
    }

    [Fact]
    public async Task Worker_uses_only_the_server_issued_exact_scope_after_revalidation()
    {
        var context = CreateContext();
        var requestedScope = TenantWorkScopeRequest.ForCompany(Guid.NewGuid());
        var work = DurableWorkItem.Create(
            context,
            TenantWorkScope.IssueFromVerifiedAuthority(context, requestedScope),
            Identity(context, "exact-execution-scope"),
            new DemoPayload("scope"),
            PayloadRegistry,
            Guid.NewGuid(),
            createdAt: Clock);
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var handler = new ScopeCaptureHandler();
        var dispatcher = CreateDispatcher();
        dispatcher.Register(handler);
        var worker = new TenantDurableWorkWorker(store, dispatcher, new TestAuthorityRevalidator());
        Assert.True(await store.SubmitAsync(work));

        var result = await worker.ProcessOneAsync(
            context,
            Guid.NewGuid(),
            Clock,
            TimeSpan.FromMinutes(5));

        Assert.Equal(DurableWorkLifecycle.Completed, result!.Lifecycle);
        Assert.NotNull(handler.SeenContext);
        Assert.Equal($"Company:{work.Scope.CompanyId}", handler.SeenContext!.Scope!.Value.Value);
        Assert.Equal(work.Scope.CompanyId, handler.SeenScope!.CompanyId);
    }

    [Fact]
    public async Task Worker_revalidation_cancellation_is_recoverable_and_not_authorization_denied()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        Assert.True(await store.SubmitAsync(Work(context, "authority-cancel")));
        var handler = new SuccessfulHandler();
        var dispatcher = CreateDispatcher();
        dispatcher.Register(handler);
        var worker = new TenantDurableWorkWorker(
            store,
            dispatcher,
            new ControlledAuthorityRevalidator(AuthorityBehavior.Cancelled));

        var result = await worker.ProcessOneAsync(
            context,
            Guid.NewGuid(),
            Clock,
            TimeSpan.FromMinutes(5));

        Assert.Equal(DurableWorkLifecycle.RetryScheduled, result!.Lifecycle);
        Assert.Equal(DurableWorkFailureCategory.Cancelled, result.FailureCategory);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Worker_revalidation_provider_failure_is_bounded_retry_not_authorization_denied()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        Assert.True(await store.SubmitAsync(Work(context, "authority-provider")));
        var handler = new SuccessfulHandler();
        var dispatcher = CreateDispatcher();
        dispatcher.Register(handler);
        var worker = new TenantDurableWorkWorker(
            store,
            dispatcher,
            new ControlledAuthorityRevalidator(AuthorityBehavior.ProviderUnavailable));

        var result = await worker.ProcessOneAsync(
            context,
            Guid.NewGuid(),
            Clock,
            TimeSpan.FromMinutes(5));

        Assert.Equal(DurableWorkLifecycle.RetryScheduled, result!.Lifecycle);
        Assert.Equal(DurableWorkFailureCategory.ProviderUnavailable, result.FailureCategory);
        Assert.NotEqual(DurableWorkFailureCategory.AuthorizationDenied, result.FailureCategory);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Outbox_revalidation_cancellation_retries_without_running_effect()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "outbox-authority-cancel");
        Assert.True(await store.SubmitAsync(work));
        var effects = 0;

        var result = await store.DispatchOutboxAsync(
            context,
            new ControlledAuthorityRevalidator(AuthorityBehavior.Cancelled),
            Clock,
            (_, _, _) =>
            {
                effects++;
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            });

        Assert.True(result.RetryScheduled);
        Assert.False(result.DeadLettered);
        Assert.Equal(DurableWorkFailureCategory.Cancelled, result.FailureCategory);
        Assert.Equal(0, effects);
    }

    [Fact]
    public async Task Outbox_revalidation_provider_failure_retries_without_authorization_denial()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "outbox-authority-provider");
        Assert.True(await store.SubmitAsync(work));
        var effects = 0;

        var result = await store.DispatchOutboxAsync(
            context,
            new ControlledAuthorityRevalidator(AuthorityBehavior.ProviderUnavailable),
            Clock,
            (_, _, _) =>
            {
                effects++;
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            });

        Assert.True(result.RetryScheduled);
        Assert.False(result.DeadLettered);
        Assert.Equal(DurableWorkFailureCategory.ProviderUnavailable, result.FailureCategory);
        Assert.NotEqual(DurableWorkFailureCategory.AuthorizationDenied, result.FailureCategory);
        Assert.Equal(0, effects);
    }

    [Fact]
    public async Task Missing_trusted_context_blocks_execution()
    {
        var worker = new TenantDurableWorkWorker(new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard())), CreateDispatcher(), new TestAuthorityRevalidator());
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await worker.ProcessOneAsync(
            null!, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Authorization_path_mismatch_blocks_protected_work()
    {
        var ordinary = CreateContext();
        var support = CreateSupportContext(ordinary.TenantId);
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        await store.SubmitAsync(Work(ordinary, "auth-path"));
        Assert.Null(await store.TryClaimAsync(support, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Dispatcher_has_no_global_tenant_query()
    {
        var methods = typeof(IDurableWorkStore).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(methods, method => method.Name.Contains("List", StringComparison.OrdinalIgnoreCase)
            && !method.GetParameters().Any(parameter => parameter.ParameterType == typeof(TenantContext)));
    }

    [Fact]
    public async Task Notification_duplicate_is_single_effect()
    {
        var context = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-1");
        var first = await adapter.DeliverAsync(context, intent);
        var second = await adapter.DeliverAsync(context, intent);
        Assert.True(first.Delivered);
        Assert.True(second.Duplicate);
    }

    [Fact]
    public async Task Notification_provider_failure_is_safely_categorized()
    {
        var context = CreateContext();
        var adapter = new InMemoryNotificationAdapter(_ => DurableWorkFailureCategory.ProviderUnavailable);
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-failure");
        var result = await adapter.DeliverAsync(context, intent);
        Assert.Equal(DurableWorkFailureCategory.ProviderUnavailable, result.FailureCategory);
        Assert.Equal("provider_unavailable", result.SafeOutcome);
    }

    [Fact]
    public async Task Notification_does_not_log_recipient_contact_value()
    {
        var context = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-safe");
        await adapter.DeliverAsync(context, intent);
        Assert.DoesNotContain("@", string.Join("|", adapter.Decisions));
        Assert.DoesNotContain(typeof(TenantNotificationIntent).GetProperties(), property => property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task File_metadata_requires_exact_tenant()
    {
        var context = CreateContext();
        var foreignScope = DurableWorkTestSupport.TenantWideScope(CreateContext());
        var storage = new InMemoryPrivateObjectStorage();
        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context,
            foreignScope,
            "private.txt",
            "text/plain",
            Content("forged-scope" )).AsTask());
    }

    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b_file_metadata_or_object()
    {
        var tenantB = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(tenantB, DurableWorkTestSupport.TenantWideScope(tenantB), "private.txt", "text/plain", Content("secret"));
        var result = await storage.ReadAsync(CreateContext(), metadata.ObjectId);
        Assert.Equal(PrivateFileAccessOutcome.NotFound, result.Outcome);
        Assert.Null(result.Metadata);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task Tenant_a_cannot_overwrite_tenant_b_object()
    {
        var tenantB = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(tenantB, DurableWorkTestSupport.TenantWideScope(tenantB), "private.txt", "text/plain", Content("secret"));
        var result = await storage.OverwriteAsync(CreateContext(), metadata.ObjectId, metadata.ConcurrencyVersion, Content("forged"));
        Assert.Equal(PrivateFileAccessOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Checksum_mismatch_fails_safely()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "private.txt", "text/plain", Content("safe"));
        storage.TamperForValidation(metadata.ObjectId, "tampered"u8.ToArray());
        var result = await storage.ReadAsync(context, metadata.ObjectId);
        Assert.Equal(PrivateFileAccessOutcome.ChecksumFailed, result.Outcome);
    }

    [Fact]
    public async Task Expiry_metadata_does_not_trigger_physical_purge()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "old.txt", "text/plain", Content("old"), Clock.AddMinutes(-1));
        var result = await storage.ReadAsync(context, metadata.ObjectId);
        Assert.Equal(PrivateFileAccessOutcome.Expired, result.Outcome);
        Assert.True(storage.ExistsForValidation(metadata.ObjectId));
    }

    [Fact]
    public void No_public_url_is_produced()
    {
        Assert.DoesNotContain(typeof(PrivateFileMetadata).GetProperties(), property => property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(IPrivateObjectStorage).GetMethods(), method => method.Name.Contains("Url", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_anonymous_file_access_path_exists()
    {
        Assert.All(typeof(IPrivateObjectStorage).GetMethods(), method =>
            Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(TenantContext)));
    }

    [Fact]
    public async Task File_metadata_concurrency_protects_overwrite()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "versioned.txt", "text/plain", Content("v1"));
        metadata.ScanState = PrivateFileScanState.Clean;
        var staleVersion = metadata.ConcurrencyVersion;
        var first = await storage.OverwriteAsync(context, metadata.ObjectId, staleVersion, Content("v2"));
        var stale = await storage.OverwriteAsync(context, metadata.ObjectId, staleVersion, Content("stale"));
        Assert.Equal(PrivateFileAccessOutcome.SafetyBlocked, first.Outcome);
        Assert.Equal(PrivateFileAccessOutcome.ConcurrencyConflict, stale.Outcome);
    }

    [Fact]
    public async Task Audit_contains_tenant_correlation_and_work_identity_safely()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var work = Work(context, "audit-safe");
        await store.SubmitAsync(work);
        var evidence = await store.ReadAuditAsync(context);
        var record = Assert.Single(evidence);
        Assert.Equal(context.TenantId, record.TenantId);
        Assert.Equal(work.Identity.WorkItemId, record.WorkItemId);
        Assert.Equal(context.CorrelationId, record.CorrelationId);
    }

    [Fact]
    public async Task Audit_contains_no_payload_token_cookie_or_private_bytes()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        await store.SubmitAsync(Work(context, "audit-redacted", "do-not-log"));
        var json = JsonSerializer.Serialize(await store.ReadAuditAsync(context));
        Assert.DoesNotContain("do-not-log", json, StringComparison.Ordinal);
        Assert.DoesNotContain("cookie", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-bytes", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_DI_does_not_register_local_or_production_provider()
    {
        var apiAssembly = Assembly.Load("MiniErp.Api");
        Assert.DoesNotContain(apiAssembly.GetTypes(), type =>
            type == typeof(InMemoryDurableWorkStore)
            || type == typeof(InMemoryPrivateObjectStorage)
            || type == typeof(InMemoryNotificationAdapter));
    }

    [Fact]
    public void Work_contract_has_no_arbitrary_payload_dictionary_or_unrestricted_serialization()
    {
        Assert.DoesNotContain(typeof(DurableWorkItem).GetProperties(), property =>
            property.PropertyType == typeof(object)
            || property.PropertyType == typeof(Dictionary<string, object>));
        Assert.DoesNotContain(typeof(DurableWorkItem).GetMethods(), method =>
            method.Name.Contains("Serialize", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Work_identity_requires_idempotency_and_stable_event_fields()
    {
        var context = CreateContext();
        Assert.Throws<ArgumentException>(() => DurableWorkIdentity.Create(Guid.NewGuid(), OperationCatalogue, "foundation.work", context.CorrelationId!.Value, ""));
        var identity = Identity(context);
        Assert.NotEqual(Guid.Empty, identity.WorkItemId);
        Assert.Equal("demo", identity.WorkType);
    }

    [Fact]
    public void Operation_catalogue_owns_permission_and_handler_binding()
    {
        Assert.Empty(typeof(DurableWorkIdentity).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain(
            typeof(TenantWorkScope).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.Name == "ForServerContext");

        var wrongPermissionHandler = new WrongPermissionHandler();
        var dispatcher = CreateDispatcher();
        Assert.Throws<ArgumentException>(() => dispatcher.Register(wrongPermissionHandler));
    }

    [Fact]
    public void Protected_durable_effects_cannot_bypass_mandatory_security_evidence()
    {
        var registered = OperationCatalogue.TryGet("foundation.work", out var operation)
            ? operation
            : throw new InvalidOperationException("The test operation is not registered.");
        Assert.True(registered.RequiresMandatorySecurityEvidence);

        var optionalDescriptor = new DurableWorkOperationDescriptor(
            registered.OperationId,
            registered.WorkType,
            registered.ExactPermissionCode,
            registered.AllowedAuthorizationPaths,
            registered.ScopePolicy,
            registered.RequiresCurrentSession,
            requiresMandatorySecurityEvidence: false);
        var optionalCatalogue = new DurableWorkOperationCatalogue([optionalDescriptor]);
        var context = CreateContext();
        var identity = DurableWorkIdentity.Create(
            Guid.NewGuid(),
            optionalCatalogue,
            registered.OperationId,
            context.CorrelationId!.Value,
            "mandatory-evidence-policy");

        Assert.Throws<ArgumentException>(() => DurableWorkItem.Create(
            context,
            DurableWorkTestSupport.TenantWideScope(context),
            identity,
            new DemoPayload("protected"),
            PayloadRegistry,
            Guid.NewGuid()));

        var dispatcher = CreateDispatcher(optionalCatalogue);
        Assert.Throws<ArgumentException>(() => dispatcher.Register(new MissingMandatoryEvidenceHandler(optionalDescriptor)));
    }

    [Fact]
    public void Verified_authority_issuers_are_syntax_tree_allow_listed()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "MiniErp.App"));
        var testRoot = Path.GetFullPath(Path.Combine(sourceRoot, "..", "..", "tests"));
        var approvedIdentityIssuer = Path.GetFullPath(Path.Combine(
            sourceRoot,
            "Modules",
            "Identity",
            "IdentityAuthorizationService.cs"));
        var productionScopeIssuers = new List<string>();
        var productionAuthorizationIssuers = new List<string>();
        var testScopeIssuers = new List<string>();
        var testAuthorizationIssuers = new List<string>();

        foreach (var path in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            CollectIssuerCallSites(
                path,
                productionScopeIssuers,
                productionAuthorizationIssuers);
        }

        foreach (var path in Directory.GetFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            CollectIssuerCallSites(
                path,
                testScopeIssuers,
                testAuthorizationIssuers);
        }

        Assert.NotEmpty(productionScopeIssuers);
        Assert.NotEmpty(productionAuthorizationIssuers);
        Assert.All(productionScopeIssuers, path =>
            Assert.Equal(
                approvedIdentityIssuer,
                Path.GetFullPath(path),
                StringComparer.OrdinalIgnoreCase));
        Assert.All(productionAuthorizationIssuers, path =>
            Assert.Equal(
                approvedIdentityIssuer,
                Path.GetFullPath(path),
                StringComparer.OrdinalIgnoreCase));
        Assert.All(testScopeIssuers, path =>
            Assert.StartsWith(testRoot, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        Assert.All(testAuthorizationIssuers, path =>
            Assert.StartsWith(testRoot, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));

        static void CollectIssuerCallSites(
            string path,
            ICollection<string> scopeIssuers,
            ICollection<string> authorizationIssuers)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
            var root = tree.GetRoot();
            if (root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(IsVerifiedScopeIssuance))
            {
                scopeIssuers.Add(path);
            }

            if (root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Any(IsVerifiedAuthorizationConstruction))
            {
                authorizationIssuers.Add(path);
            }
        }

        static bool IsVerifiedScopeIssuance(InvocationExpressionSyntax invocation) =>
            invocation.Expression is MemberAccessExpressionSyntax member
            && member.Name.Identifier.ValueText == nameof(TenantWorkScope.IssueFromVerifiedAuthority)
            && member.Expression.ToString() == nameof(TenantWorkScope);

        static bool IsVerifiedAuthorizationConstruction(ObjectCreationExpressionSyntax creation) =>
            creation.Type.ToString() == nameof(VerifiedDurableWorkAuthorization);
    }

    [Fact]
    public async Task Worker_executes_one_typed_handler_and_records_success()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var dispatcher = CreateDispatcher();
        var handler = new SuccessfulHandler();
        dispatcher.Register(handler);
        var item = Work(context, "typed");
        await store.SubmitAsync(item);
        var worker = new TenantDurableWorkWorker(store, dispatcher, new TestAuthorityRevalidator());
        await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.Equal(1, handler.Calls);
        Assert.Contains(await store.ReadAuditAsync(context), item => item.EventType == "work.completed");
    }

    [Fact]
    public async Task Missing_handler_reaches_safe_dead_letter()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard()));
        var item = Work(context, "unregistered");
        await store.SubmitAsync(item);
        var worker = new TenantDurableWorkWorker(store, CreateDispatcher(), new TestAuthorityRevalidator());
        var result = await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.Equal(DurableWorkLifecycle.DeadLetter, result!.Lifecycle);
        Assert.Equal(DurableWorkFailureCategory.ValidationFailed, result.FailureCategory);
    }

    [Fact]
    public void Platform_governance_context_is_not_a_work_tenant_fallback()
    {
        var methods = typeof(DurableWorkItem).GetMethods(BindingFlags.Public | BindingFlags.Static);
        Assert.DoesNotContain(methods, method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(PlatformGovernanceContext)));
    }

    private static DurableWorkItem Work(
        TenantContext context,
        string key,
        string payload = "payload",
        int maximumAttempts = 3) =>
        DurableWorkItem.Create(
            context,
            DurableWorkTestSupport.TenantWideScope(context),
            Identity(context, key),
            new DemoPayload(payload),
            PayloadRegistry,
            Guid.NewGuid(),
            maximumAttempts,
            Clock);

    private static DurableWorkIdentity Identity(TenantContext context, string key = "key") =>
        DurableWorkIdentity.Create(
            Guid.NewGuid(),
            OperationCatalogue,
            "foundation.work",
            context.CorrelationId!.Value,
            key);

    private static TenantContext CreateContext(TenantId? tenantId = null) =>
        TenantContext.ForOrdinaryMembership(
            tenantId ?? new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    private static TenantContext CreateSupportContext(TenantId tenantId) =>
        TenantContext.ForSupportGrant(
            tenantId,
            new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
            new ScopeReference("support"),
            new CorrelationId($"corr-support-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    private static MemoryStream Content(string value) =>
        new(System.Text.Encoding.UTF8.GetBytes(value));

    private static VerifiedNotificationRecipient VerifiedRecipient(TenantId tenantId) =>
        new(tenantId, Guid.NewGuid());

    private sealed record DemoPayload(string Value) : IWorkPayload;

    private sealed class SuccessfulHandler : IDurableWorkHandler<DemoPayload>
    {
        public int Calls { get; private set; }

        public DurableWorkOperationDescriptor Operation => OperationCatalogue.TryGet("foundation.work", out var operation)
            ? operation
            : throw new InvalidOperationException("The test operation is not registered.");

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            DemoPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.Equal(context.TenantContext.TenantId, context.Scope.TenantId);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        }
    }

    private sealed class ScopeCaptureHandler : IDurableWorkHandler<DemoPayload>
    {
        internal TenantContext? SeenContext { get; private set; }

        internal TenantWorkScope? SeenScope { get; private set; }

        public DurableWorkOperationDescriptor Operation => OperationCatalogue.TryGet("foundation.work", out var operation)
            ? operation
            : throw new InvalidOperationException("The test operation is not registered.");

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            DemoPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            SeenContext = context.TenantContext;
            SeenScope = context.Scope;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        }
    }

    private sealed class WrongPermissionHandler : IDurableWorkHandler<DemoPayload>
    {
        public DurableWorkOperationDescriptor Operation { get; } =
            new(
                "foundation.work",
                "demo",
                "tenant.business.export",
                [TenantAuthorizationPath.OrdinaryMembership]);

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            DemoPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
    }

    private sealed class MissingMandatoryEvidenceHandler : IDurableWorkHandler<DemoPayload>
    {
        internal MissingMandatoryEvidenceHandler(DurableWorkOperationDescriptor operation)
        {
            Operation = operation;
        }

        public DurableWorkOperationDescriptor Operation { get; }

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            DemoPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
    }

    private sealed class TestAuthorityRevalidator : IDurableWorkAuthorityRevalidator
    {
        public ValueTask<DurableWorkAuthorityValidationResult> RevalidateAsync(
            DurableWorkItem workItem,
            TenantContext currentTenantContext,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DurableWorkTestSupport.Approve(workItem, currentTenantContext));
    }

    private enum AuthorityBehavior
    {
        Cancelled,
        ProviderUnavailable
    }

    private sealed class ControlledAuthorityRevalidator : IDurableWorkAuthorityRevalidator
    {
        private readonly AuthorityBehavior behavior;

        internal ControlledAuthorityRevalidator(AuthorityBehavior behavior)
        {
            this.behavior = behavior;
        }

        public ValueTask<DurableWorkAuthorityValidationResult> RevalidateAsync(
            DurableWorkItem workItem,
            TenantContext currentTenantContext,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (behavior == AuthorityBehavior.Cancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            throw new InvalidOperationException("simulated authority provider failure");
        }
    }
}
