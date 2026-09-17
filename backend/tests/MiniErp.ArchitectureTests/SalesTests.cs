using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class SalesTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CustomerA = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ProductA = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid UomA = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CurrencyA = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid PriceListA = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid TaxA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid ExchangeRateA = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
    private static readonly Guid PaymentTermA = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid PaymentTermVersionA = Guid.Parse("dddddddd-1111-1111-1111-111111111111");
    private static readonly Guid Creator = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid Approver = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private static readonly Guid ApproverTwo = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private static readonly Guid ApproverThree = Guid.Parse("14141414-1414-1414-1414-141414141414");

    [Fact]
    public async Task Quotation_revision_history_is_immutable_and_stale_edits_fail()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-1", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);

        var editedModel = fixture.Model(created.Value!.Id) with { CustomerReference = "revised-reference", Reason = "commercial correction" };
        var edited = await fixture.Persistence.EditQuotationAsync(fixture.Context(Creator), created.Value.Id, editedModel, created.Value.Version, "edit-1", "fingerprint-2");
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal(2, edited.Value!.RevisionNumber);
        Assert.Equal("revised-reference", edited.Value.CustomerReference);

        var stale = await fixture.Persistence.EditQuotationAsync(fixture.Context(Creator), created.Value.Id, editedModel, created.Value.Version, "edit-2", "fingerprint-3");
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var revisions = await fixture.Persistence.ListQuotationRevisionsAsync(fixture.Context(Creator), created.Value.Id);
        Assert.Equal(2, revisions.Count);
        Assert.Contains(revisions, item => item.RevisionNumber == 1 && item.Snapshot.CustomerReference is null);
        Assert.Contains(revisions, item => item.RevisionNumber == 2 && item.Snapshot.CustomerReference == "revised-reference");
        Assert.NotEqual(revisions[0].SnapshotHash, revisions[1].SnapshotHash);
    }

    [Fact]
    public async Task Quote_to_order_preserves_source_revision_and_idempotent_retry_creates_one_order()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-2", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), created.Value!.Id, SalesQuotationStatus.PendingApproval, null, created.Value.Version, "submit-2", "fingerprint-2", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, "approved", submitted.Value!.Version, "approve-2", "fingerprint-3", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);

        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value!.Version, "convert-2", "fingerprint-4", fixture.Policy());
        Assert.True(converted.Succeeded, converted.Code);
        Assert.Equal(created.Value.Id, converted.Value!.SourceQuotationId);
        Assert.Equal(1, converted.Value.SourceQuotationRevision);
        Assert.Equal(approved.Value.Lines.Single().PriceSourceReference, converted.Value.Lines.Single().PriceSourceReference);

        var replay = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value.Version, "convert-2", "fingerprint-4", fixture.Policy());
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(converted.Value.Id, replay.Value!.Id);
        Assert.Single(await fixture.Persistence.ListOrdersAsync(fixture.Context(Approver), CompanyA, null));

        var competing = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value.Version, "convert-3", "fingerprint-5", fixture.Policy());
        Assert.False(competing.Succeeded);
        Assert.Equal("concurrency_conflict", competing.Code);
    }

    [Fact]
    public async Task Expired_quote_cannot_convert_even_when_approved()
    {
        await using var fixture = await Fixture.CreateAsync();
        var expiredModel = fixture.Model() with
        {
            QuotationDate = new DateOnly(2025, 1, 1),
            ValidUntil = new DateOnly(2025, 1, 31)
        };
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), expiredModel, "create-3", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), created.Value!.Id, SalesQuotationStatus.PendingApproval, null, created.Value.Version, "submit-3", "fingerprint-2", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, null, submitted.Value!.Version, "approve-3", "fingerprint-3", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);

        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value!.Version, "convert-3", "fingerprint-4", fixture.Policy());
        Assert.False(converted.Succeeded);
        Assert.Equal("quotation_expired", converted.Code);
        Assert.Empty(await fixture.Persistence.ListOrdersAsync(fixture.Context(Approver), CompanyA, null));
    }

    [Fact]
    public async Task Tenant_query_filters_prevent_cross_tenant_quote_and_order_visibility()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-4", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);

        var foreignContext = fixture.Context(Creator, TenantB, CompanyB);
        Assert.Null(await fixture.Persistence.GetQuotationAsync(foreignContext, created.Value!.Id));
        Assert.Empty(await fixture.Persistence.ListQuotationsAsync(foreignContext, null, null));
        Assert.Null(await fixture.Persistence.GetOrderAsync(foreignContext, created.Value.Id));
    }

    [Fact]
    public async Task Trusted_company_scope_filters_same_tenant_quote_visibility()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-company-scope", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);

        var otherCompanyContext = fixture.Context(Creator, TenantA, CompanyB);

        Assert.Null(await fixture.Persistence.GetQuotationAsync(otherCompanyContext, created.Value!.Id));
        Assert.Empty(await fixture.Persistence.ListQuotationsAsync(otherCompanyContext, null, null));
    }

    [Fact]
    public async Task History_and_audit_capture_approval_conversion_and_credit_evidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-5", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), created.Value!.Id, SalesQuotationStatus.PendingApproval, null, created.Value.Version, "submit-5", "fingerprint-2", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, "approved", submitted.Value!.Version, "approve-5", "fingerprint-3", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);
        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value!.Version, "convert-5", "fingerprint-4", fixture.Policy());
        Assert.True(converted.Succeeded, converted.Code);

        var quotationHistory = await fixture.Persistence.ListHistoryAsync(fixture.Context(Approver), "quotation", created.Value.Id);
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Created));
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Submitted) && item.PolicyId == fixture.Policy().PolicyId && item.PolicyVersion == fixture.Policy().Version);
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Approved));
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Converted));

        var orderAudit = await fixture.Persistence.ListAuditAsync(fixture.Context(Approver), "order", converted.Value!.Id);
        Assert.Contains(orderAudit, item => item.OperationId == "sales.quotation.convert" && item.Decision == "Allowed");
    }

    [Fact]
    public async Task Approval_state_enforces_sequential_stages_counts_sod_and_survives_reload()
    {
        await using var fixture = await Fixture.CreateAsync();
        var policy = new SalesApprovalPolicyDefinition("sales.multi-stage", 4,
            [
                new SalesApprovalStageDefinition("commercial", 1, 2, [Approver, ApproverTwo], false),
                new SalesApprovalStageDefinition("finance", 2, 1, [ApproverThree], false)
            ], true, false, DateTimeOffset.MinValue, null, 100m, 200m, "SAR", true);
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "approval-create", "approval-create-fp", policy);
        Assert.True(created.Succeeded, created.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), created.Value!.Id, SalesQuotationStatus.PendingApproval, null, created.Value.Version, "approval-submit", "approval-submit-fp", policy);
        Assert.True(submitted.Succeeded, submitted.Code);

        var first = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, null, submitted.Value!.Version, "approval-first", "approval-first-fp", policy);
        Assert.True(first.Succeeded, first.Code);
        Assert.Equal(SalesQuotationStatus.PendingApproval, first.Value!.Status);
        Assert.Equal(1, first.Value.ApprovalState!.CurrentStageApprovalCount);
        Assert.Single(first.Value.ApprovalState.Decisions);

        var duplicate = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, null, first.Value.Version, "approval-duplicate", "approval-duplicate-fp", policy);
        Assert.False(duplicate.Succeeded);
        Assert.Equal("approval_already_recorded", duplicate.Code);
        var earlyLaterStage = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(ApproverThree), created.Value.Id, SalesQuotationStatus.Approved, null, first.Value.Version, "approval-early", "approval-early-fp", policy);
        Assert.False(earlyLaterStage.Succeeded);
        Assert.Equal("approver_not_eligible", earlyLaterStage.Code);

        var second = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(ApproverTwo), created.Value.Id, SalesQuotationStatus.Approved, null, first.Value.Version, "approval-second", "approval-second-fp", policy);
        Assert.True(second.Succeeded, second.Code);
        Assert.Equal(SalesQuotationStatus.PendingApproval, second.Value!.Status);
        Assert.Equal("finance", second.Value.ApprovalState!.CurrentStageKey);
        Assert.Equal(2, second.Value.ApprovalState.Decisions.Count);

        var reloaded = await fixture.Persistence.GetQuotationAsync(fixture.Context(Creator), created.Value.Id);
        Assert.Equal(2, reloaded!.ApprovalState!.Decisions.Count);
        var final = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(ApproverThree), created.Value.Id, SalesQuotationStatus.Approved, null, reloaded.Version, "approval-final", "approval-final-fp", policy);
        Assert.True(final.Succeeded, final.Code);
        Assert.Equal(SalesQuotationStatus.Approved, final.Value!.Status);
        Assert.Equal(3, final.Value.ApprovalState!.Decisions.Count);
        Assert.Equal(ApproverThree, final.Value.ApprovalState.Decisions[^1].ActorId);
        Assert.Equal("finance", final.Value.ApprovalState.Decisions[^1].StageKey);
    }

    [Fact]
    public async Task Quotation_scope_is_immutable_but_same_scope_edit_succeeds()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "scope-create", "scope-create-fp", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);

        var otherCompany = await fixture.Persistence.EditQuotationAsync(fixture.Context(Creator), created.Value!.Id, fixture.Model(created.Value.Id) with { CompanyId = CompanyB }, created.Value.Version, "scope-company", "scope-company-fp");
        Assert.False(otherCompany.Succeeded);
        Assert.Equal("quotation_scope_immutable", otherCompany.Code);
        var otherBranch = await fixture.Persistence.EditQuotationAsync(fixture.Context(Creator), created.Value.Id, fixture.Model(created.Value.Id) with { BranchId = null }, created.Value.Version, "scope-branch", "scope-branch-fp");
        Assert.False(otherBranch.Succeeded);
        Assert.Equal("quotation_scope_immutable", otherBranch.Code);

        var unchanged = await fixture.Persistence.GetQuotationAsync(fixture.Context(Creator), created.Value.Id);
        Assert.Equal(CompanyA, unchanged!.CompanyId);
        Assert.Equal(BranchA, unchanged.BranchId);
        var valid = await fixture.Persistence.EditQuotationAsync(fixture.Context(Creator), created.Value.Id, fixture.Model(created.Value.Id) with { CustomerReference = "same-scope" }, created.Value.Version, "scope-valid", "scope-valid-fp");
        Assert.True(valid.Succeeded, valid.Code);
        Assert.Equal("same-scope", valid.Value!.CustomerReference);
    }

    [Fact]
    public async Task Returned_order_can_be_revised_resubmitted_and_prior_snapshot_is_retained()
    {
        await using var fixture = await Fixture.CreateAsync();
        var quote = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "order-edit-create", "order-edit-create-fp", fixture.Policy());
        var submittedQuote = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), quote.Value!.Id, SalesQuotationStatus.PendingApproval, null, quote.Value.Version, "order-edit-submit-q", "order-edit-submit-q-fp", fixture.Policy());
        var approvedQuote = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), quote.Value.Id, SalesQuotationStatus.Approved, null, submittedQuote.Value!.Version, "order-edit-approve-q", "order-edit-approve-q-fp", fixture.Policy());
        var order = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Creator), quote.Value.Id, approvedQuote.Value!.Version, "order-edit-convert", "order-edit-convert-fp", fixture.Policy());
        Assert.True(order.Succeeded, order.Code);
        var submitted = await fixture.Persistence.TransitionOrderAsync(fixture.Context(Creator), order.Value!.Id, SalesOrderStatus.PendingApproval, null, order.Value.Version, "order-edit-submit", "order-edit-submit-fp", null, fixture.Policy());
        var returned = await fixture.Persistence.TransitionOrderAsync(fixture.Context(Approver), order.Value.Id, SalesOrderStatus.ReturnedForChange, "change quantity", submitted.Value!.Version, "order-edit-return", "order-edit-return-fp", null, fixture.Policy());
        Assert.True(returned.Succeeded, returned.Code);

        var edited = await fixture.Persistence.EditOrderAsync(fixture.Context(Creator), order.Value.Id, fixture.Model() with { Id = order.Value.Id, Lines = [fixture.Model().Lines[0] with { Quantity = 5m, LineTotal = 250m }], Subtotal = 250m, Total = 250m }, returned.Value!.Version, "order-edit-edit", "order-edit-edit-fp", fixture.Policy());
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal(SalesOrderStatus.Draft, edited.Value!.Status);
        Assert.Equal(2, edited.Value.RevisionNumber);
        Assert.Equal(5m, edited.Value.Lines.Single().Quantity);
        Assert.Null(edited.Value.ApprovalState);

        var history = await fixture.Persistence.ListHistoryAsync(fixture.Context(Creator), "order", order.Value.Id);
        Assert.Contains(history, item => item.Action == nameof(SalesHistoryAction.Edited) && item.SnapshotJson is not null && item.SnapshotJson.Contains("\"total\":150", StringComparison.OrdinalIgnoreCase));
        var resubmitted = await fixture.Persistence.TransitionOrderAsync(fixture.Context(Creator), order.Value.Id, SalesOrderStatus.PendingApproval, null, edited.Value.Version, "order-edit-resubmit", "order-edit-resubmit-fp", null, fixture.Policy());
        Assert.True(resubmitted.Succeeded, resubmitted.Code);
        Assert.Equal(SalesOrderStatus.PendingApproval, resubmitted.Value!.Status);
        var approved = await fixture.Persistence.TransitionOrderAsync(fixture.Context(Approver), order.Value.Id, SalesOrderStatus.Approved, null, resubmitted.Value.Version, "order-edit-approve", "order-edit-approve-fp", null, fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);
        Assert.Equal(SalesOrderStatus.Approved, approved.Value!.Status);
        Assert.Single(approved.Value.ApprovalState!.Decisions);
    }

    [Fact]
    public async Task Pending_cancellation_is_limited_to_the_requester_and_configured_policy()
    {
        await using var fixture = await Fixture.CreateAsync();
        var deniedPolicy = fixture.Policy() with { AllowRequesterCancellationWhilePending = false };
        var deniedQuote = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "cancel-denied-create", "cancel-denied-create-fp", deniedPolicy);
        Assert.True(deniedQuote.Succeeded, deniedQuote.Code);
        var deniedSubmit = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), deniedQuote.Value!.Id, SalesQuotationStatus.PendingApproval, null, deniedQuote.Value.Version, "cancel-denied-submit", "cancel-denied-submit-fp", deniedPolicy);
        Assert.True(deniedSubmit.Succeeded, deniedSubmit.Code);
        var denied = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), deniedQuote.Value.Id, SalesQuotationStatus.Cancelled, "withdraw", deniedSubmit.Value!.Version, "cancel-denied", "cancel-denied-fp", deniedPolicy);
        Assert.False(denied.Succeeded);
        Assert.Equal("cancellation_not_allowed", denied.Code);

        var allowedPolicy = fixture.Policy();
        var allowedQuote = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "cancel-allowed-create", "cancel-allowed-create-fp", allowedPolicy);
        Assert.True(allowedQuote.Succeeded, allowedQuote.Code);
        var allowedSubmit = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), allowedQuote.Value!.Id, SalesQuotationStatus.PendingApproval, null, allowedQuote.Value.Version, "cancel-allowed-submit", "cancel-allowed-submit-fp", allowedPolicy);
        Assert.True(allowedSubmit.Succeeded, allowedSubmit.Code);
        var otherActor = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), allowedQuote.Value.Id, SalesQuotationStatus.Cancelled, "withdraw", allowedSubmit.Value!.Version, "cancel-other", "cancel-other-fp", allowedPolicy);
        Assert.False(otherActor.Succeeded);
        Assert.Equal("cancellation_not_allowed", otherActor.Code);
        var cancelled = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), allowedQuote.Value.Id, SalesQuotationStatus.Cancelled, "withdraw", allowedSubmit.Value.Version, "cancel-allowed", "cancel-allowed-fp", allowedPolicy);
        Assert.True(cancelled.Succeeded, cancelled.Code);
        Assert.Equal(SalesQuotationStatus.Cancelled, cancelled.Value!.Status);
    }

    [Fact]
    public async Task Runtime_sales_configuration_uses_application_composition_and_fails_closed_when_missing()
    {
        var now = DateTimeOffset.UtcNow;
        var values = new Dictionary<string, string?>
        {
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:TenantId"] = TenantA.ToString(),
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:CompanyId"] = CompanyA.ToString(),
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:BranchId"] = BranchA.ToString(),
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:DocumentType"] = "quotation",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:PolicyId"] = "runtime-policy",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:Version"] = "2",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:EffectiveFrom"] = now.AddMinutes(-5).ToString("O"),
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:EffectiveTo"] = now.AddMinutes(5).ToString("O"),
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:Stages:0:StageKey"] = "commercial",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:Stages:0:Sequence"] = "1",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:Stages:0:RequiredApprovals"] = "1",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:Stages:0:EligibleApproverIds:0"] = Approver.ToString(),
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:MinimumTotal"] = "100",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:MaximumTotal"] = "200",
            ["MESP_SALES_POLICIES:ApprovalPolicies:0:CurrencyCode"] = "SAR",
            ["MESP_SALES_POLICIES:CreditLimits:0:TenantId"] = TenantA.ToString(),
            ["MESP_SALES_POLICIES:CreditLimits:0:CompanyId"] = CompanyA.ToString(),
            ["MESP_SALES_POLICIES:CreditLimits:0:CustomerId"] = CustomerA.ToString(),
            ["MESP_SALES_POLICIES:CreditLimits:0:CurrencyCode"] = "SAR",
            ["MESP_SALES_POLICIES:CreditLimits:0:Limit"] = "500"
        };
        using var provider = new ServiceCollection().AddSalesApplication(new ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
        Assert.IsType<ConfigurationSalesApprovalPolicyProvider>(provider.GetRequiredService<ISalesApprovalPolicyProvider>());
        Assert.IsType<ConfigurationSalesCreditLimitProvider>(provider.GetRequiredService<ISalesCreditLimitProvider>());
        var context = Context(Creator, TenantA, CompanyA);
        var approval = await provider.GetRequiredService<ISalesApprovalPolicyProvider>().ResolveAsync(context, new SalesScope(TenantA, CompanyA, BranchA), "quotation", 150m, now, currencyCode: "SAR");
        Assert.Equal("runtime-policy", approval!.PolicyId);
        Assert.Null(await provider.GetRequiredService<ISalesApprovalPolicyProvider>().ResolveAsync(context, new SalesScope(TenantA, CompanyA, BranchA), "quotation", 99m, now, currencyCode: "SAR"));
        Assert.Null(await provider.GetRequiredService<ISalesApprovalPolicyProvider>().ResolveAsync(context, new SalesScope(TenantA, CompanyA, BranchA), "quotation", 150m, now, currencyCode: "USD"));
        Assert.Null(await provider.GetRequiredService<ISalesApprovalPolicyProvider>().ResolveAsync(context, new SalesScope(TenantA, CompanyA, BranchA), "quotation", 150m, now.AddHours(1), currencyCode: "SAR"));
        Assert.Equal(500m, await provider.GetRequiredService<ISalesCreditLimitProvider>().ResolveLimitAsync(context, CompanyA, CustomerA, "SAR", new DateOnly(2026, 8, 28)));

        using var missingProvider = new ServiceCollection().AddSalesApplication(new ConfigurationBuilder().Build()).BuildServiceProvider();
        Assert.Null(await missingProvider.GetRequiredService<ISalesApprovalPolicyProvider>().ResolveAsync(context, new SalesScope(TenantA, CompanyA, BranchA), "quotation", 150m, now, currencyCode: "SAR"));
        Assert.Null(await missingProvider.GetRequiredService<ISalesCreditLimitProvider>().ResolveLimitAsync(context, CompanyA, CustomerA, "SAR", new DateOnly(2026, 8, 28)));
    }

    [Fact]
    public async Task Configured_commercial_authority_delegation_and_credit_are_scope_and_time_bounded()
    {
        var now = DateTimeOffset.UtcNow;
        var scope = new SalesScope(TenantA, CompanyA, BranchA);
        var authority = new SalesCommercialAuthority(TenantA, CompanyA, BranchA, "quotation", 15m, true, "sales-authority-1", 3, [Approver], now.AddMinutes(-1), now.AddMinutes(10));
        var authorityProvider = new ConfiguredSalesCommercialAuthorityProvider([authority]);
        var context = Context(Approver, TenantA, CompanyA);
        Assert.Equal(authority, await authorityProvider.ResolveAsync(context, scope, "quotation", Approver, now));
        Assert.Null(await authorityProvider.ResolveAsync(context, scope, "quotation", Creator, now));
        Assert.Null(await authorityProvider.ResolveAsync(context, scope, "quotation", Approver, now.AddDays(1)));

        var stage = new SalesApprovalStageDefinition("commercial", 1, 1, [Creator], true);
        var delegation = new SalesApprovalDelegation(TenantA, CompanyA, BranchA, "quotation", "commercial", Creator, Approver, now.AddMinutes(-5), now.AddMinutes(5));
        var delegationProvider = new ConfiguredSalesApprovalDelegationProvider([delegation]);
        Assert.Equal(delegation, await delegationProvider.ResolveAsync(context, scope, "quotation", stage, Approver, now));
        Assert.Null(await delegationProvider.ResolveAsync(context, scope, "quotation", stage, Approver, now.AddHours(1)));
        Assert.Null(await delegationProvider.ResolveAsync(context, new SalesScope(TenantB, CompanyA, BranchA), "quotation", stage, Approver, now));

        var limits = new ConfiguredSalesCreditLimitProvider([
            new SalesCreditLimit(TenantA, CompanyA, CustomerA, "SAR", 100m, new DateOnly(2026, 1, 1), null),
            new SalesCreditLimit(TenantA, CompanyA, CustomerA, "SAR", 250m, new DateOnly(2026, 8, 1), null)
        ]);
        Assert.Equal(250m, await limits.ResolveLimitAsync(context, CompanyA, CustomerA, "sar", new DateOnly(2026, 8, 28)));
        Assert.Null(await limits.ResolveLimitAsync(context, CompanyB, CustomerA, "SAR", new DateOnly(2026, 8, 28)));
    }

    [Fact]
    public void Sales_authorization_reuses_foundation_catalogue_and_scope_rules()
    {
        var authorization = new SalesAuthorizationService(new PurchaseRequestAuthorizationService());
        var context = Context(Creator, TenantA, CompanyA, "tenant.sales.quotation.create");
        Assert.True(authorization.Authorize(context, "sales.quotation.create", new SalesScope(TenantA, CompanyA, BranchA)));
        Assert.False(authorization.Authorize(context, "sales.quotation.create", new SalesScope(TenantB, CompanyA, BranchA)));
        Assert.False(authorization.Authorize(Context(Creator, TenantA, CompanyA, "tenant.sales.order.confirm"), "sales.quotation.create", new SalesScope(TenantA, CompanyA, BranchA)));
    }

    [Fact]
    public async Task Sales_service_rebuilds_server_totals_and_denies_manual_price_without_authority()
    {
        var persistence = new CapturingSalesPersistence();
        var service = new SalesService(
            persistence,
            new SalesAuthorizationService(new PurchaseRequestAuthorizationService()),
            new DefaultSalesApprovalPolicyProvider(),
            new NoSalesCommercialAuthorityProvider(),
            new NoSalesApprovalDelegationProvider(),
            new NoSalesCreditLimitProvider(),
            new UnavailableFinanceSettlementPersistence(),
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantA, CompanyA, "Company A", "SAR", BranchA)]),
            new CustomerReferenceFake(),
            new ProductReferenceFake(),
            new PriceReferenceFake(),
            new UnavailableSalesTaxReferenceProvider(),
            new UnavailableSalesExchangeRateReferenceProvider());

        var request = new SalesQuotationCreateRequest(
            CompanyA,
            BranchA,
            CustomerA,
            new DateOnly(2026, 8, 28),
            new DateOnly(2026, 9, 30),
            CurrencyA,
            PriceListA,
            null,
            null,
            null,
            [new SalesQuotationLineRequest(ProductA, UomA, 2m)]);

        var created = await service.CreateQuotationAsync(Context(Creator), request, "service-create-1");

        Assert.True(created.Succeeded, created.Code);
        Assert.NotNull(persistence.Captured);
        Assert.Equal(100m, persistence.Captured!.Total);
        Assert.Equal(50m, persistence.Captured.Lines.Single().UnitPrice);
        Assert.Equal(100m, persistence.Captured.Lines.Single().LineTotal);
        Assert.False(persistence.Captured.Lines.Single().ManualPriceApplied);

        var manualPrice = request with
        {
            Lines = [request.Lines[0] with { UnitPriceOverride = 40m }]
        };
        var rejected = await service.CreateQuotationAsync(Context(Creator), manualPrice, "service-create-2");

        Assert.False(rejected.Succeeded);
        Assert.Equal("commercial_reference_invalid", rejected.Code);
    }

    [Fact]
    public async Task Sales_service_snapshots_tax_and_exchange_evidence_from_existing_contracts()
    {
        var persistence = new CapturingSalesPersistence();
        var service = new SalesService(
            persistence,
            new SalesAuthorizationService(new PurchaseRequestAuthorizationService()),
            new DefaultSalesApprovalPolicyProvider(),
            new NoSalesCommercialAuthorityProvider(),
            new NoSalesApprovalDelegationProvider(),
            new NoSalesCreditLimitProvider(),
            new UnavailableFinanceSettlementPersistence(),
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantA, CompanyA, "Company A", "SAR", BranchA)]),
            new CustomerReferenceFake(),
            new ProductReferenceFake(),
            new PriceReferenceFake("USD"),
            new TaxReferenceFake(),
            new ExchangeRateReferenceFake(),
            paymentTerms: new PaymentTermReferenceFake());

        var request = new SalesQuotationCreateRequest(
            CompanyA,
            BranchA,
            CustomerA,
            new DateOnly(2026, 8, 28),
            new DateOnly(2026, 9, 30),
            CurrencyA,
            PriceListA,
            null,
            null,
            null,
            [new SalesQuotationLineRequest(ProductA, UomA, 2m, null, 0m, null, TaxA)],
            ExchangeRateA,
            PaymentTermA);

        var created = await service.CreateQuotationAsync(Context(Creator), request, "service-tax-fx-1");

        Assert.True(created.Succeeded, created.Code);
        Assert.NotNull(persistence.Captured);
        Assert.Equal(100m, persistence.Captured!.Subtotal);
        Assert.Equal(15m, persistence.Captured.TaxAmount);
        Assert.Equal(115m, persistence.Captured.Total);
        Assert.Equal(TaxA, persistence.Captured.Lines.Single().TaxEvidence!.TaxId);
        Assert.Equal(ExchangeRateA, persistence.Captured.ExchangeRateEvidence!.ExchangeRateId);
        Assert.Equal("USD", persistence.Captured.ExchangeRateEvidence.SourceCurrencyCode);
        Assert.Equal("SAR", persistence.Captured.ExchangeRateEvidence.TargetCurrencyCode);
        Assert.Equal(PaymentTermA, persistence.Captured.PaymentTerm!.Id);
        Assert.Equal(PaymentTermVersionA, persistence.Captured.PaymentTerm.VersionId);
        Assert.Equal(4, persistence.Captured.PaymentTerm.DueOffsetDays);
        Assert.Equal("masterdata.payment-term", persistence.Captured.PaymentTerm.Provenance);
    }

    [Fact]
    public async Task Sales_service_rejects_destination_scope_before_rebuilding_or_persisting_edit()
    {
        var quotationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var existing = new SalesQuotationResponse(quotationId, "SQ-TEST", TenantA, CompanyA, BranchA, CustomerA, "CUST-001", "Customer A", Creator, new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 30), CurrencyA, "SAR", null, null, null, 0m, 0m, 0m, 0m, SalesQuotationStatus.Draft, 1, [], [1], now, now);
        var persistence = new CapturingSalesPersistence(existing);
        var service = new SalesService(
            persistence,
            new SalesAuthorizationService(new PurchaseRequestAuthorizationService()),
            new DefaultSalesApprovalPolicyProvider(),
            new NoSalesCommercialAuthorityProvider(),
            new NoSalesApprovalDelegationProvider(),
            new NoSalesCreditLimitProvider(),
            new UnavailableFinanceSettlementPersistence(),
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantA, CompanyA, "Company A", "SAR", BranchA)]),
            new CustomerReferenceFake(),
            new ProductReferenceFake(),
            new PriceReferenceFake(),
            new UnavailableSalesTaxReferenceProvider(),
            new UnavailableSalesExchangeRateReferenceProvider());

        var request = new SalesQuotationEditRequest(CompanyB, BranchA, new DateOnly(2026, 9, 30), CurrencyA, PriceListA, null, null, null, [new SalesQuotationLineRequest(ProductA, UomA, 1m)]);
        var result = await service.EditQuotationAsync(Context(Creator, TenantA, CompanyA, "tenant.sales.quotation.edit"), quotationId, request, [1], "service-scope-edit");

        Assert.False(result.Succeeded);
        Assert.Equal("quotation_scope_immutable", result.Code);
        Assert.Null(persistence.Captured);
    }

    [Fact]
    public async Task Sales_invoice_request_persistence_is_idempotent_and_blocks_cumulative_overinvoice()
    {
        await using var fixture = await Fixture.CreateAsync();
        var baseModel = fixture.Model();
        var expandedModel = baseModel with { Lines = [baseModel.Lines.Single() with { Quantity = 10m, LineTotal = 500m }], Subtotal = 500m, Total = 500m };
        var quotation = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), expandedModel, "invoice-source-create", "invoice-source-create-fp", fixture.Policy());
        Assert.True(quotation.Succeeded, quotation.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), quotation.Value!.Id, SalesQuotationStatus.PendingApproval, null, quotation.Value.Version, "invoice-source-submit", "invoice-source-submit-fp", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), quotation.Value.Id, SalesQuotationStatus.Approved, null, submitted.Value!.Version, "invoice-source-approve", "invoice-source-approve-fp", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);
        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), quotation.Value.Id, approved.Value!.Version, "invoice-source-convert", "invoice-source-convert-fp", fixture.Policy());
        Assert.True(converted.Succeeded, converted.Code);
        var order = converted.Value!;
        var line = order.Lines.Single();
        var deliveryId = Guid.NewGuid();
        var secondDeliveryId = Guid.NewGuid();
        await using (var db = new SalesDbContext(fixture.Options, fixture.Context(Creator).TenantContext))
        {
            var delivery = new SalesDeliveryEntity(new TenantId(TenantA), deliveryId, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(line.Id, Guid.NewGuid(), 5m) }), "{}", Creator, "invoice-source-delivery", DateTimeOffset.UtcNow);
            delivery.Posted([Guid.NewGuid()], DateTimeOffset.UtcNow);
            var secondDelivery = new SalesDeliveryEntity(new TenantId(TenantA), secondDeliveryId, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(line.Id, Guid.NewGuid(), 5m) }), "{}", Creator, "invoice-source-delivery-2", DateTimeOffset.UtcNow.AddMinutes(1));
            secondDelivery.Posted([Guid.NewGuid()], DateTimeOffset.UtcNow.AddMinutes(1));
            db.Deliveries.AddRange(delivery, secondDelivery);
            await db.SaveChangesAsync();
        }

        var firstModel = new SalesInvoiceRequestWriteModel(Guid.NewGuid(), order.Id, order.RevisionNumber, deliveryId, CompanyA, BranchA, CustomerA, new DateOnly(2026, 8, 29), 100m, "SAR", [new SalesInvoiceRequestLine(line.Id, 2m)], "invoice-source-snapshot", Creator);
        var first = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), firstModel, "invoice-source-key", "invoice-source-fp");
        Assert.True(first.Succeeded, first.Code);
        var replay = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), firstModel with { Id = Guid.NewGuid() }, "invoice-source-key", "invoice-source-fp");
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);

        var second = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), firstModel with { Id = Guid.NewGuid(), Lines = [new SalesInvoiceRequestLine(line.Id, 3m)] }, "invoice-source-key-2", "invoice-source-fp-2");
        Assert.True(second.Succeeded, second.Code);
        Assert.Equal(deliveryId, second.Value!.DeliveryId);

        var third = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), firstModel with { Id = Guid.NewGuid(), DeliveryId = secondDeliveryId, Lines = [new SalesInvoiceRequestLine(line.Id, 5m)] }, "invoice-source-key-3", "invoice-source-fp-3");
        Assert.True(third.Succeeded, third.Code);
        Assert.Equal(secondDeliveryId, third.Value!.DeliveryId);

        var competing = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), firstModel with { Id = Guid.NewGuid(), Lines = [new SalesInvoiceRequestLine(line.Id, 1m)] }, "invoice-source-key-4", "invoice-source-fp-4");
        Assert.False(competing.Succeeded);
        Assert.Equal("invoice_quantity_conflict", competing.Code);

        var mismatchedDeliveries = new[]
        {
            new SalesDeliveryEntity(new TenantId(TenantA), Guid.NewGuid(), Guid.NewGuid(), order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), "[]", "{}", Creator, "invoice-source-other-order", DateTimeOffset.UtcNow),
            new SalesDeliveryEntity(new TenantId(TenantA), Guid.NewGuid(), order.Id, order.RevisionNumber + 1, CompanyA, BranchA, CustomerA, Guid.NewGuid(), "[]", "{}", Creator, "invoice-source-other-revision", DateTimeOffset.UtcNow),
            new SalesDeliveryEntity(new TenantId(TenantA), Guid.NewGuid(), order.Id, order.RevisionNumber, CompanyB, BranchA, CustomerA, Guid.NewGuid(), "[]", "{}", Creator, "invoice-source-other-company", DateTimeOffset.UtcNow),
            new SalesDeliveryEntity(new TenantId(TenantA), Guid.NewGuid(), order.Id, order.RevisionNumber, CompanyA, Guid.NewGuid(), CustomerA, Guid.NewGuid(), "[]", "{}", Creator, "invoice-source-other-branch", DateTimeOffset.UtcNow),
            new SalesDeliveryEntity(new TenantId(TenantA), Guid.NewGuid(), order.Id, order.RevisionNumber, CompanyA, BranchA, Guid.NewGuid(), Guid.NewGuid(), "[]", "{}", Creator, "invoice-source-other-customer", DateTimeOffset.UtcNow)
        };
        foreach (var mismatchedDelivery in mismatchedDeliveries) mismatchedDelivery.Posted([Guid.NewGuid()], DateTimeOffset.UtcNow);
        await using (var db = new SalesDbContext(fixture.Options, fixture.Context(Creator).TenantContext))
        {
            db.Deliveries.AddRange(mismatchedDeliveries);
            await db.SaveChangesAsync();
        }

        var tenantMismatch = new SalesDeliveryEntity(new TenantId(TenantB), Guid.NewGuid(), order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), "[]", "{}", Creator, "invoice-source-other-tenant", DateTimeOffset.UtcNow);
        tenantMismatch.Posted([Guid.NewGuid()], DateTimeOffset.UtcNow);
        await using (var db = new SalesDbContext(fixture.Options, fixture.Context(Creator, TenantB, CompanyB).TenantContext))
        {
            db.Deliveries.Add(tenantMismatch);
            await db.SaveChangesAsync();
        }

        foreach (var mismatchedDelivery in mismatchedDeliveries.Append(tenantMismatch))
        {
            var mismatch = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), firstModel with { Id = Guid.NewGuid(), DeliveryId = mismatchedDelivery.Id }, $"invoice-source-mismatch-{mismatchedDelivery.Id}", $"invoice-source-mismatch-fp-{mismatchedDelivery.Id}");
            Assert.False(mismatch.Succeeded);
            Assert.Equal("delivery_not_posted", mismatch.Code);
        }
    }

    [Fact]
    public async Task Concurrent_invoice_requests_cannot_consume_one_delivery_quantity_twice()
    {
        await using var fixture = await Fixture.CreateAsync();
        var baseModel = fixture.Model();
        var expandedModel = baseModel with { Lines = [baseModel.Lines.Single() with { Quantity = 5m, LineTotal = 250m }], Subtotal = 250m, Total = 250m };
        var quotation = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), expandedModel, "invoice-race-create", "invoice-race-create-fp", fixture.Policy());
        Assert.True(quotation.Succeeded, quotation.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), quotation.Value!.Id, SalesQuotationStatus.PendingApproval, null, quotation.Value.Version, "invoice-race-submit", "invoice-race-submit-fp", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), quotation.Value.Id, SalesQuotationStatus.Approved, null, submitted.Value!.Version, "invoice-race-approve", "invoice-race-approve-fp", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);
        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), quotation.Value.Id, approved.Value!.Version, "invoice-race-convert", "invoice-race-convert-fp", fixture.Policy());
        Assert.True(converted.Succeeded, converted.Code);
        var order = converted.Value!;
        var line = order.Lines.Single();
        var deliveryId = Guid.NewGuid();
        await using (var db = new SalesDbContext(fixture.Options, fixture.Context(Creator).TenantContext))
        {
            var delivery = new SalesDeliveryEntity(new TenantId(TenantA), deliveryId, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(line.Id, Guid.NewGuid(), 5m) }), "{}", Creator, "invoice-race-delivery", DateTimeOffset.UtcNow);
            delivery.Posted([Guid.NewGuid()], DateTimeOffset.UtcNow);
            db.Deliveries.Add(delivery);
            await db.SaveChangesAsync();
        }

        var firstModel = new SalesInvoiceRequestWriteModel(Guid.NewGuid(), order.Id, order.RevisionNumber, deliveryId, CompanyA, BranchA, CustomerA, new DateOnly(2026, 8, 29), 250m, "SAR", [new SalesInvoiceRequestLine(line.Id, 5m)], "invoice-race-snapshot", Creator);
        var results = await Task.WhenAll(
            AttemptAsync(firstModel with { Id = Guid.NewGuid() }, "invoice-race-key-1", "invoice-race-fp-1"),
            AttemptAsync(firstModel with { Id = Guid.NewGuid() }, "invoice-race-key-2", "invoice-race-fp-2"));

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);

        async Task<(bool Succeeded, string? Code)> AttemptAsync(SalesInvoiceRequestWriteModel model, string key, string fingerprint)
        {
            try
            {
                var result = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), model, key, fingerprint);
                return (result.Succeeded, result.Code);
            }
            catch (SqliteException)
            {
                return (false, "concurrency_conflict");
            }
            catch (DbUpdateException)
            {
                return (false, "concurrency_conflict");
            }
        }
    }

    [Fact]
    public async Task Invoice_money_uses_persisted_prior_evidence_and_reconciles_net_tax_and_gross()
    {
        await using var fixture = await Fixture.CreateAsync();
        var baseModel = fixture.Model();
        var taxEvidence = new SalesTaxEvidence(TaxA, "VAT-10", Guid.Parse("cccccccc-2222-2222-2222-222222222222"), 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, 10m, 100m, 10m, "SAR", "VAT-10;v1");
        var expandedModel = baseModel with
        {
            Lines = [baseModel.Lines.Single() with { Quantity = 3m, LineTotal = 100m, TaxAmount = 10m, TaxEvidence = taxEvidence }],
            Subtotal = 100m,
            TaxAmount = 10m,
            Total = 110m
        };
        var quotation = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), expandedModel, "invoice-residual-create", "invoice-residual-create-fp", fixture.Policy());
        Assert.True(quotation.Succeeded, quotation.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), quotation.Value!.Id, SalesQuotationStatus.PendingApproval, null, quotation.Value.Version, "invoice-residual-submit", "invoice-residual-submit-fp", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), quotation.Value.Id, SalesQuotationStatus.Approved, null, submitted.Value!.Version, "invoice-residual-approve", "invoice-residual-approve-fp", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);
        var order = (await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), quotation.Value.Id, approved.Value!.Version, "invoice-residual-convert", "invoice-residual-convert-fp", fixture.Policy())).Value!;
        var line = order.Lines.Single();
        var deliveryId = Guid.NewGuid();
        await using (var db = new SalesDbContext(fixture.Options, fixture.Context(Creator).TenantContext))
        {
            var delivery = new SalesDeliveryEntity(new TenantId(TenantA), deliveryId, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(line.Id, Guid.NewGuid(), 3m) }), "{}", Creator, "invoice-residual-delivery", DateTimeOffset.UtcNow);
            delivery.Posted([Guid.NewGuid()], DateTimeOffset.UtcNow);
            db.Deliveries.Add(delivery);
            await db.SaveChangesAsync();
        }

        for (var index = 0; index < 3; index++)
        {
            var request = new SalesInvoiceRequestWriteModel(Guid.NewGuid(), order.Id, order.RevisionNumber, deliveryId, CompanyA, BranchA, CustomerA, new DateOnly(2026, 8, 29), 999m, "USD", [new SalesInvoiceRequestLine(line.Id, 1m)], "stale-request-snapshot", Creator, 999m, 999m);
            var result = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), request, $"invoice-residual-key-{index}", $"invoice-residual-fp-{index}");
            Assert.True(result.Succeeded, result.Code);
            var evidence = Assert.Single(result.Value!.LineEvidence!);
            Assert.Equal(index == 2 ? 33.34m : 33.33m, evidence.NetAmount);
            Assert.Equal(index == 2 ? 3.34m : 3.33m, evidence.TaxAmount);
            Assert.Equal(evidence.NetAmount + evidence.TaxAmount, evidence.GrossAmount);
        }

        var persisted = await fixture.Persistence.ListInvoiceRequestsAsync(fixture.Context(Creator), order.Id);
        Assert.Equal(3, persisted.Count);
        Assert.Equal(100m, persisted.Sum(item => item.NetAmount));
        Assert.Equal(10m, persisted.Sum(item => item.TaxAmount));
        Assert.Equal(110m, persisted.Sum(item => item.Amount));
        Assert.Equal(persisted.Sum(item => item.NetAmount) + persisted.Sum(item => item.TaxAmount), persisted.Sum(item => item.Amount));
    }

    [Fact]
    public async Task Concurrent_one_unit_invoice_requests_reconcile_exact_residual_money()
    {
        await using var fixture = await Fixture.CreateAsync();
        var baseModel = fixture.Model();
        var taxEvidence = new SalesTaxEvidence(TaxA, "VAT-10", Guid.Parse("cccccccc-2222-2222-2222-222222222222"), 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, 10m, 100m, 10m, "SAR", "VAT-10;v1");
        var quoteModel = baseModel with
        {
            Lines = [baseModel.Lines.Single() with { Quantity = 3m, LineTotal = 100m, TaxAmount = 10m, TaxEvidence = taxEvidence }],
            Subtotal = 100m,
            TaxAmount = 10m,
            Total = 110m
        };
        var quotation = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), quoteModel, "invoice-concurrency-create", "invoice-concurrency-create-fp", fixture.Policy());
        Assert.True(quotation.Succeeded, quotation.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), quotation.Value!.Id, SalesQuotationStatus.PendingApproval, null, quotation.Value.Version, "invoice-concurrency-submit", "invoice-concurrency-submit-fp", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), quotation.Value.Id, SalesQuotationStatus.Approved, null, submitted.Value!.Version, "invoice-concurrency-approve", "invoice-concurrency-approve-fp", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);
        var order = (await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), quotation.Value.Id, approved.Value!.Version, "invoice-concurrency-convert", "invoice-concurrency-convert-fp", fixture.Policy())).Value!;
        var line = order.Lines.Single();
        var deliveryId = Guid.NewGuid();
        await using (var db = new SalesDbContext(fixture.Options, fixture.Context(Creator).TenantContext))
        {
            var delivery = new SalesDeliveryEntity(new TenantId(TenantA), deliveryId, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), JsonSerializer.Serialize(new[] { new SalesDeliveryRequestLine(line.Id, Guid.NewGuid(), 3m) }), "{}", Creator, "invoice-concurrency-delivery", DateTimeOffset.UtcNow);
            delivery.Posted([Guid.NewGuid()], DateTimeOffset.UtcNow);
            db.Deliveries.Add(delivery);
            await db.SaveChangesAsync();
        }

        var requests = Enumerable.Range(0, 3).Select(index =>
            (Model: new SalesInvoiceRequestWriteModel(Guid.NewGuid(), order.Id, order.RevisionNumber, deliveryId, CompanyA, BranchA, CustomerA, new DateOnly(2026, 8, 29), 999m, "USD", [new SalesInvoiceRequestLine(line.Id, 1m)], "stale-request-snapshot", Creator, 999m, 999m),
             Key: $"invoice-concurrency-key-{index}", Fingerprint: $"invoice-concurrency-fp-{index}")).ToArray();
        var results = await Task.WhenAll(requests.Select(item => AttemptAsync(item.Model, item.Key, item.Fingerprint)));

        for (var index = 0; index < results.Length; index++)
        {
            if (!results[index].Succeeded)
            {
                var retry = await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), requests[index].Model, requests[index].Key, requests[index].Fingerprint);
                Assert.True(retry.Succeeded, retry.Code);
            }
        }

        var persisted = await fixture.Persistence.ListInvoiceRequestsAsync(fixture.Context(Creator), order.Id);
        Assert.Equal(3, persisted.Count);
        Assert.Equal([33.33m, 33.33m, 33.34m], persisted.Select(item => item.NetAmount).OrderBy(item => item).ToArray());
        Assert.Equal(100m, persisted.Sum(item => item.NetAmount));
        Assert.Equal(10m, persisted.Sum(item => item.TaxAmount));
        Assert.Equal(110m, persisted.Sum(item => item.Amount));

        async Task<SalesOperationResult<SalesInvoiceRequestResponse>> AttemptAsync(SalesInvoiceRequestWriteModel model, string key, string fingerprint)
        {
            try
            {
                return await fixture.Persistence.CreateInvoiceRequestAsync(fixture.Context(Creator), model, key, fingerprint);
            }
            catch (SqliteException)
            {
                return SalesOperationResult<SalesInvoiceRequestResponse>.Failure("concurrency_conflict");
            }
            catch (DbUpdateException)
            {
                return SalesOperationResult<SalesInvoiceRequestResponse>.Failure("concurrency_conflict");
            }
        }
    }

    [Fact]
    public async Task Delivery_and_invoice_handoffs_are_durable_recoverable_and_duplicate_safe()
    {
        await using var fixture = await Fixture.CreateAsync();
        var deliveryId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var deliveryMovementId = Guid.NewGuid();
        var financeOpenItemId = Guid.NewGuid();
        await using (var db = new SalesDbContext(fixture.Options, fixture.Context(Creator).TenantContext))
        {
            db.Deliveries.Add(new SalesDeliveryEntity(new TenantId(TenantA), deliveryId, orderId, 1, CompanyA, BranchA, CustomerA, Guid.NewGuid(), "[]", "{}", Creator, "handoff-delivery", DateTimeOffset.UtcNow));
            db.InvoiceRequests.Add(new SalesInvoiceRequestEntity(new TenantId(TenantA), invoiceId, orderId, 1, null, CompanyA, BranchA, CustomerA, new DateOnly(2026, 8, 29), "[]", 1m, "SAR", "{}", Creator, "handoff-invoice", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var deliveryKey = "inventory-handoff-key";
        var deliveryFingerprint = "inventory-handoff-fingerprint";
        var failedDelivery = await fixture.Persistence.FailDeliveryAsync(fixture.Context(Creator), deliveryId, "downstream_ack_lost", true, downstream: new SalesDownstreamEvidence([deliveryMovementId], "Committed", "NotAcknowledged", "Required", deliveryKey, deliveryFingerprint));
        Assert.True(failedDelivery.Succeeded, failedDelivery.Code);
        Assert.Equal(SalesDeliveryStatus.Unknown, failedDelivery.Value!.Status);
        Assert.Equal([deliveryMovementId], failedDelivery.Value.Handoff!.DownstreamEffectIds);
        Assert.Equal("Committed", failedDelivery.Value.Handoff.DownstreamCommitState);
        Assert.Equal(deliveryKey, failedDelivery.Value.Handoff.DownstreamIdempotencyKey);
        Assert.Equal(deliveryFingerprint, failedDelivery.Value.Handoff.DownstreamRequestFingerprint);
        Assert.Equal("Required", failedDelivery.Value.Handoff.ReconciliationStatus);
        Assert.Equal(1, failedDelivery.Value.Handoff.AttemptCount);

        var completedDelivery = await fixture.Persistence.CompleteDeliveryAsync(fixture.Context(Creator), deliveryId, [deliveryMovementId], "handoff-delivery-complete", "handoff-delivery-complete-fp", downstreamIdempotencyKey: deliveryKey, downstreamRequestFingerprint: deliveryFingerprint);
        Assert.True(completedDelivery.Succeeded, completedDelivery.Code);
        Assert.Equal(SalesDeliveryStatus.Posted, completedDelivery.Value!.Status);
        Assert.Equal("Reconciled", completedDelivery.Value.Handoff!.ReconciliationStatus);

        var deliveryRetry = await fixture.Persistence.CompleteDeliveryAsync(fixture.Context(Creator), deliveryId, [deliveryMovementId], "handoff-delivery-retry", "handoff-delivery-retry-fp", downstreamIdempotencyKey: deliveryKey, downstreamRequestFingerprint: deliveryFingerprint);
        Assert.True(deliveryRetry.Succeeded, deliveryRetry.Code);
        var deliveryMismatch = await fixture.Persistence.CompleteDeliveryAsync(fixture.Context(Creator), deliveryId, [Guid.NewGuid()], "handoff-delivery-mismatch", "handoff-delivery-mismatch-fp", downstreamIdempotencyKey: deliveryKey, downstreamRequestFingerprint: deliveryFingerprint);
        Assert.False(deliveryMismatch.Succeeded);
        Assert.Equal("delivery_effect_mismatch", deliveryMismatch.Code);

        var invoiceKey = "finance-handoff-key";
        var invoiceFingerprint = "finance-handoff-fingerprint";
        var failedInvoice = await fixture.Persistence.FailInvoiceRequestAsync(fixture.Context(Creator), invoiceId, "finance_ack_lost", true, downstream: new SalesDownstreamEvidence([financeOpenItemId], "Committed", "NotAcknowledged", "Required", invoiceKey, invoiceFingerprint));
        Assert.True(failedInvoice.Succeeded, failedInvoice.Code);
        Assert.Equal(SalesInvoiceRequestStatus.Unknown, failedInvoice.Value!.Status);
        Assert.Equal([financeOpenItemId], failedInvoice.Value.Handoff!.DownstreamEffectIds);
        Assert.Equal(financeOpenItemId, failedInvoice.Value.FinanceOpenItemId);
        Assert.Equal("Committed", failedInvoice.Value.Handoff.DownstreamCommitState);
        Assert.Equal(invoiceKey, failedInvoice.Value.Handoff.DownstreamIdempotencyKey);
        Assert.Equal(invoiceFingerprint, failedInvoice.Value.Handoff.DownstreamRequestFingerprint);
        Assert.Equal("Required", failedInvoice.Value.Handoff.ReconciliationStatus);

        var completedInvoice = await fixture.Persistence.CompleteInvoiceRequestAsync(fixture.Context(Creator), invoiceId, financeOpenItemId, "handoff-invoice-complete", "handoff-invoice-complete-fp", downstreamIdempotencyKey: invoiceKey, downstreamRequestFingerprint: invoiceFingerprint);
        Assert.True(completedInvoice.Succeeded, completedInvoice.Code);
        Assert.Equal(SalesInvoiceRequestStatus.Posted, completedInvoice.Value!.Status);
        Assert.Equal("Reconciled", completedInvoice.Value.Handoff!.ReconciliationStatus);

        var invoiceRetry = await fixture.Persistence.CompleteInvoiceRequestAsync(fixture.Context(Creator), invoiceId, financeOpenItemId, "handoff-invoice-retry", "handoff-invoice-retry-fp", downstreamIdempotencyKey: invoiceKey, downstreamRequestFingerprint: invoiceFingerprint);
        Assert.True(invoiceRetry.Succeeded, invoiceRetry.Code);
        var invoiceMismatch = await fixture.Persistence.CompleteInvoiceRequestAsync(fixture.Context(Creator), invoiceId, Guid.NewGuid(), "handoff-invoice-mismatch", "handoff-invoice-mismatch-fp", downstreamIdempotencyKey: invoiceKey, downstreamRequestFingerprint: invoiceFingerprint);
        Assert.False(invoiceMismatch.Succeeded);
        Assert.Equal("invoice_effect_mismatch", invoiceMismatch.Code);
        var invoiceFingerprintMismatch = await fixture.Persistence.CompleteInvoiceRequestAsync(fixture.Context(Creator), invoiceId, financeOpenItemId, "handoff-invoice-fingerprint-mismatch", "handoff-invoice-fingerprint-mismatch-fp", downstreamIdempotencyKey: invoiceKey, downstreamRequestFingerprint: "changed");
        Assert.False(invoiceFingerprintMismatch.Succeeded);
        Assert.Equal("invoice_effect_mismatch", invoiceFingerprintMismatch.Code);
    }

    [Fact]
    public async Task Service_marks_delivery_unknown_when_inventory_commits_before_sales_finalization_fails()
    {
        var order = HandoffOrder();
        var persistence = new CapturingSalesPersistence(order);
        var inventory = new HandoffInventoryPort(order.Id, order.Lines.Single().Id);
        var service = CreateHandoffService(persistence, inventory, new ControllableFinanceSettlementPersistence());

        var result = await service.CreateDeliveryAsync(
            Context(Creator, permission: "tenant.sales.delivery.post"),
            order.Id,
            new SalesDeliveryRequest(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new DateOnly(2026, 8, 29), [new SalesDeliveryRequestLine(order.Lines.Single().Id, inventory.ReservationId, 1m)]),
            "service-delivery-handoff",
            order.Version);

        Assert.False(result.Succeeded);
        Assert.Equal("sales_finalization_failed", result.Code);
        Assert.True(inventory.DeliveryCommitted);
        Assert.True(persistence.DeliveryFailedUnknown);
        Assert.Equal(SalesDeliveryStatus.Unknown, persistence.Delivery!.Status);
        Assert.Single(inventory.MovementIds);
    }

    [Fact]
    public async Task Service_marks_invoice_unknown_when_finance_commits_before_sales_finalization_fails()
    {
        var order = HandoffOrder();
        var persistence = new CapturingSalesPersistence(order)
        {
            Delivery = new SalesDeliveryResponse(Guid.NewGuid(), TenantA, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), SalesDeliveryStatus.Posted, null, [new SalesDeliveryRequestLine(order.Lines.Single().Id, Guid.NewGuid(), 1m)], [Guid.NewGuid()], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [1], new SalesHandoffEvidence("inventory.sales-delivery.post", [], "Committed", "Acknowledged", "Reconciled", null, 1, DateTimeOffset.UtcNow, "delivery-fp"))
        };
        var finance = new ControllableFinanceSettlementPersistence
        {
            SalesInvoiceResult = new FinanceOpenItemRecord(Guid.NewGuid(), TenantA, CompanyA, FinanceOpenItemKind.Receivable, null, CustomerA, "sales-invoice.v1", order.Id, order.RevisionNumber, Guid.NewGuid(), 1, "sales-invoice", new DateOnly(2026, 8, 29), new DateOnly(2026, 9, 28), "SAR", 100m, "SAR", 100m, null, null, null, null, order.PaymentTerm is null ? null : new FinancePaymentTermSnapshotRecord(order.PaymentTerm.Id, order.PaymentTerm.Code, order.PaymentTerm.EnglishName, order.PaymentTerm.ArabicName, order.PaymentTerm.VersionNumber, order.PaymentTerm.VersionId, order.PaymentTerm.EffectiveOn, new DateOnly(2026, 9, 28)), null, null, FinanceOpenItemRecognitionState.Recognized, Guid.NewGuid(), 0m, 100m, FinanceOpenItemStatus.Open, [1])
        };
        var service = CreateHandoffService(persistence, new HandoffInventoryPort(order.Id, order.Lines.Single().Id), finance);

        var result = await service.CreateInvoiceRequestAsync(
            Context(Creator, permission: "tenant.sales.invoice.create"),
            order.Id,
            new SalesInvoiceEligibilityRequest(persistence.Delivery!.Id, null, new DateOnly(2026, 8, 29), [new SalesInvoiceRequestLine(order.Lines.Single().Id, 1m)]),
            "service-invoice-handoff",
            order.Version);

        Assert.False(result.Succeeded);
        Assert.Equal("sales_finalization_failed", result.Code);
        Assert.True(finance.SalesInvoiceCommitted);
        Assert.True(persistence.InvoiceFailedUnknown);
        Assert.Equal(SalesInvoiceRequestStatus.Unknown, persistence.Invoice!.Status);
    }

    [Fact]
    public async Task Service_retries_known_inventory_handoff_without_creating_a_second_effect()
    {
        var order = HandoffOrder();
        var persistence = new CapturingSalesPersistence(order);
        var inventory = new HandoffInventoryPort(order.Id, order.Lines.Single().Id);
        var service = CreateHandoffService(persistence, inventory, new ControllableFinanceSettlementPersistence());
        var request = new SalesDeliveryRequest(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), new DateOnly(2026, 8, 29), [new SalesDeliveryRequestLine(order.Lines.Single().Id, inventory.ReservationId, 1m)]);

        var first = await service.CreateDeliveryAsync(Context(Creator, permission: "tenant.sales.delivery.post"), order.Id, request, "service-delivery-recovery", order.Version);
        Assert.False(first.Succeeded);
        Assert.Equal("sales_finalization_failed", first.Code);
        Assert.Equal(SalesDeliveryStatus.Unknown, persistence.Delivery!.Status);
        Assert.Equal(inventory.MovementIds, persistence.Delivery.Handoff!.DownstreamEffectIds);
        Assert.Equal("Committed", persistence.Delivery.Handoff.DownstreamCommitState);
        Assert.Equal("NotAcknowledged", persistence.Delivery.Handoff.SalesAcknowledgementState);
        Assert.Equal("Required", persistence.Delivery.Handoff.ReconciliationStatus);
        Assert.NotNull(persistence.Delivery.Handoff.DownstreamRequestFingerprint);

        var second = await service.CreateDeliveryAsync(Context(Creator, permission: "tenant.sales.delivery.post"), order.Id, request, "service-delivery-recovery", order.Version);
        Assert.True(second.Succeeded, second.Code);
        Assert.Equal(SalesDeliveryStatus.Posted, second.Value!.Status);
        Assert.Equal("Reconciled", second.Value.Handoff!.ReconciliationStatus);
        Assert.Equal(2, inventory.PostAttempts);
        Assert.Single(inventory.MovementIds.Distinct());
    }

    [Fact]
    public async Task Service_retries_known_finance_handoff_without_creating_a_second_effect()
    {
        var order = HandoffOrder();
        var deliveryId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var persistence = new CapturingSalesPersistence(order)
        {
            Delivery = new SalesDeliveryResponse(deliveryId, TenantA, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), SalesDeliveryStatus.Posted, null, [new SalesDeliveryRequestLine(order.Lines.Single().Id, Guid.NewGuid(), 1m)], [movementId], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [1], new SalesHandoffEvidence("inventory.sales-delivery.post", [movementId], "Committed", "Acknowledged", "Reconciled", null, 1, DateTimeOffset.UtcNow, "delivery-fp"))
        };
        var financeEffectId = Guid.NewGuid();
        var finance = new ControllableFinanceSettlementPersistence
        {
            SalesInvoiceResult = new FinanceOpenItemRecord(financeEffectId, TenantA, CompanyA, FinanceOpenItemKind.Receivable, null, CustomerA, "sales-invoice.v1", order.Id, order.RevisionNumber, Guid.NewGuid(), 1, "sales-invoice", new DateOnly(2026, 8, 29), new DateOnly(2026, 9, 28), "SAR", 100m, "SAR", 100m, null, null, null, null, new FinancePaymentTermSnapshotRecord(order.PaymentTerm!.Id, order.PaymentTerm.Code, order.PaymentTerm.EnglishName, order.PaymentTerm.ArabicName, order.PaymentTerm.VersionNumber, order.PaymentTerm.VersionId, order.PaymentTerm.EffectiveOn, new DateOnly(2026, 9, 2)), null, null, FinanceOpenItemRecognitionState.Recognized, Guid.NewGuid(), 0m, 100m, FinanceOpenItemStatus.Open, [1])
        };
        var service = CreateHandoffService(persistence, new HandoffInventoryPort(order.Id, order.Lines.Single().Id), finance);
        var request = new SalesInvoiceEligibilityRequest(deliveryId, null, new DateOnly(2026, 8, 29), [new SalesInvoiceRequestLine(order.Lines.Single().Id, 1m)]);

        var first = await service.CreateInvoiceRequestAsync(Context(Creator, permission: "tenant.sales.invoice.create"), order.Id, request, "service-invoice-recovery", order.Version);
        Assert.False(first.Succeeded);
        Assert.Equal("sales_finalization_failed", first.Code);
        Assert.Equal(SalesInvoiceRequestStatus.Unknown, persistence.Invoice!.Status);
        Assert.Equal([financeEffectId], persistence.Invoice.Handoff!.DownstreamEffectIds);
        Assert.Equal(financeEffectId, persistence.Invoice.FinanceOpenItemId);
        Assert.Equal("Committed", persistence.Invoice.Handoff.DownstreamCommitState);
        Assert.Equal("NotAcknowledged", persistence.Invoice.Handoff.SalesAcknowledgementState);
        Assert.Equal("Required", persistence.Invoice.Handoff.ReconciliationStatus);

        var second = await service.CreateInvoiceRequestAsync(Context(Creator, permission: "tenant.sales.invoice.create"), order.Id, request, "service-invoice-recovery", order.Version);
        Assert.True(second.Succeeded, second.Code);
        Assert.Equal(SalesInvoiceRequestStatus.Posted, second.Value!.Status);
        Assert.Equal("Reconciled", second.Value.Handoff!.ReconciliationStatus);
        Assert.Equal(2, finance.SalesInvoicePostAttempts);
        Assert.Single(finance.SalesInvoiceEffects);
        Assert.Equal(persistence.Invoice.Amount, finance.LastSalesInvoiceCommand!.Amount);
        var persistedLine = Assert.Single(persistence.Invoice.LineEvidence!);
        var financeLine = Assert.Single(finance.LastSalesInvoiceCommand.Lines!);
        Assert.Equal(persistedLine.OrderLineId, financeLine.OrderLineId);
        Assert.Equal(persistedLine.RequestedQuantity, financeLine.Quantity);
        Assert.Equal(persistedLine.NetAmount, financeLine.NetAmount);
        Assert.Equal(persistedLine.TaxAmount, financeLine.TaxAmount);
        Assert.Equal(persistedLine.GrossAmount, financeLine.GrossAmount);
        Assert.Equal(persistedLine.TaxEvidence?.TaxId, financeLine.TaxId);
    }

    [Fact]
    public async Task Service_rejects_inventory_effects_with_wrong_tenant_or_revision()
    {
        foreach (var corrupt in new Action<HandoffInventoryPort>[]
        {
            inventory => inventory.ReturnedTenantId = TenantB,
            inventory => inventory.ReturnedOrderRevision = 2
        })
        {
            var order = HandoffOrder();
            var persistence = new CapturingSalesPersistence(order);
            var inventory = new HandoffInventoryPort(order.Id, order.Lines.Single().Id);
            corrupt(inventory);
            var service = CreateHandoffService(persistence, inventory, new ControllableFinanceSettlementPersistence());
            var result = await service.CreateDeliveryAsync(Context(Creator, permission: "tenant.sales.delivery.post"), order.Id, new SalesDeliveryRequest(inventory.WarehouseId, new DateOnly(2026, 8, 29), [new SalesDeliveryRequestLine(order.Lines.Single().Id, inventory.ReservationId, 1m)]), $"service-delivery-corrupt-{Guid.NewGuid():N}", order.Version);
            Assert.False(result.Succeeded);
            Assert.Equal("inventory_effect_mismatch", result.Code);
        }
    }

    [Fact]
    public async Task Service_fails_closed_when_finance_effect_provenance_is_wrong()
    {
        foreach (var corrupt in new Action<ControllableFinanceSettlementPersistence>[]
        {
            finance => finance.ReturnWrongSalesInvoiceEvidence = true,
            finance => finance.ReturnedSalesInvoiceTenantId = TenantB,
            finance => finance.ReturnedSalesInvoiceRevision = 2
        })
        {
            var order = HandoffOrder();
            var persistence = new CapturingSalesPersistence(order)
            {
                Delivery = new SalesDeliveryResponse(Guid.NewGuid(), TenantA, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), SalesDeliveryStatus.Posted, null, [new SalesDeliveryRequestLine(order.Lines.Single().Id, Guid.NewGuid(), 1m)], [Guid.NewGuid()], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [1], new SalesHandoffEvidence("inventory.sales-delivery.post", [], "Committed", "Acknowledged", "Reconciled", null, 1, DateTimeOffset.UtcNow, "delivery-fp"))
            };
            var finance = new ControllableFinanceSettlementPersistence
            {
                SalesInvoiceResult = new FinanceOpenItemRecord(Guid.NewGuid(), TenantA, CompanyA, FinanceOpenItemKind.Receivable, null, CustomerA, "sales-invoice.v1", order.Id, order.RevisionNumber, Guid.NewGuid(), 1, "sales-invoice", new DateOnly(2026, 8, 29), new DateOnly(2026, 9, 28), "SAR", 100m, "SAR", 100m, null, null, null, null, new FinancePaymentTermSnapshotRecord(order.PaymentTerm!.Id, order.PaymentTerm.Code, order.PaymentTerm.EnglishName, order.PaymentTerm.ArabicName, order.PaymentTerm.VersionNumber, order.PaymentTerm.VersionId, order.PaymentTerm.EffectiveOn, new DateOnly(2026, 9, 2)), null, null, FinanceOpenItemRecognitionState.Recognized, Guid.NewGuid(), 0m, 100m, FinanceOpenItemStatus.Open, [1])
            };
            corrupt(finance);
            var service = CreateHandoffService(persistence, new HandoffInventoryPort(order.Id, order.Lines.Single().Id), finance);
            var result = await service.CreateInvoiceRequestAsync(Context(Creator, permission: "tenant.sales.invoice.create"), order.Id, new SalesInvoiceEligibilityRequest(persistence.Delivery!.Id, null, new DateOnly(2026, 8, 29), [new SalesInvoiceRequestLine(order.Lines.Single().Id, 1m)]), $"service-invoice-corrupt-{Guid.NewGuid():N}", order.Version);
            Assert.False(result.Succeeded);
            Assert.Equal("finance_effect_mismatch", result.Code);
            Assert.Equal(SalesInvoiceRequestStatus.Unknown, persistence.Invoice!.Status);
            Assert.Empty(persistence.Invoice.Handoff!.DownstreamEffectIds);
            Assert.Equal(1, finance.SalesInvoicePostAttempts);
        }
    }

    [Fact]
    public async Task Service_rejects_invoice_for_wrong_delivery_before_finance_handoff()
    {
        var order = HandoffOrder();
        var persistence = new CapturingSalesPersistence(order)
        {
            Delivery = new SalesDeliveryResponse(Guid.NewGuid(), TenantA, order.Id, order.RevisionNumber, CompanyA, BranchA, CustomerA, Guid.NewGuid(), SalesDeliveryStatus.Posted, null, [new SalesDeliveryRequestLine(order.Lines.Single().Id, Guid.NewGuid(), 1m)], [Guid.NewGuid()], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [1], new SalesHandoffEvidence("inventory.sales-delivery.post", [], "Committed", "Acknowledged", "Reconciled", null, 1, DateTimeOffset.UtcNow, "delivery-fp"))
        };
        var finance = new ControllableFinanceSettlementPersistence();
        var service = CreateHandoffService(persistence, new HandoffInventoryPort(order.Id, order.Lines.Single().Id), finance);

        var result = await service.CreateInvoiceRequestAsync(Context(Creator, permission: "tenant.sales.invoice.create"), order.Id, new SalesInvoiceEligibilityRequest(Guid.NewGuid(), null, new DateOnly(2026, 8, 29), [new SalesInvoiceRequestLine(order.Lines.Single().Id, 1m)]), "service-invoice-wrong-delivery", order.Version);

        Assert.False(result.Succeeded);
        Assert.Equal("delivery_not_posted", result.Code);
        Assert.Null(persistence.Invoice);
        Assert.Equal(0, finance.SalesInvoicePostAttempts);
    }

    [Fact]
    public async Task Approval_policy_is_snapshotted_at_submission_for_quotations_and_orders()
    {
        await using var fixture = await Fixture.CreateAsync();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-a", 1, Approver),
            ApprovalOption("order", "order-a", 1, Approver)));
        var service = fixture.CreateService(options, new ControllableFinanceSettlementPersistence());

        var quotation = await service.CreateQuotationAsync(
            fixture.Context(Creator, permission: "tenant.sales.quotation.create"),
            CreateQuotationRequest(),
            "service-policy-quotation-create");
        Assert.True(quotation.Succeeded, quotation.Code);

        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-b", 2, ApproverTwo),
            ApprovalOption("order", "order-a", 1, Approver)));
        var submittedQuotation = await service.TransitionQuotationAsync(
            fixture.Context(Creator, permission: "tenant.sales.quotation.submit"),
            quotation.Value!.Id,
            SalesQuotationStatus.PendingApproval,
            null,
            quotation.Value.Version,
            "service-policy-quotation-submit");
        Assert.True(submittedQuotation.Succeeded, submittedQuotation.Code);
        Assert.Equal("quotation-b", submittedQuotation.Value!.ApprovalState!.PolicyId);
        Assert.Equal(2, submittedQuotation.Value.ApprovalState.PolicyVersion);

        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-c", 3, Approver),
            ApprovalOption("order", "order-a", 1, Approver)));
        var approvedQuotation = await service.TransitionQuotationAsync(
            fixture.Context(ApproverTwo, permission: "tenant.sales.quotation.approve"),
            quotation.Value.Id,
            SalesQuotationStatus.Approved,
            "approved using submitted snapshot",
            submittedQuotation.Value.Version,
            "service-policy-quotation-approve");
        Assert.True(approvedQuotation.Succeeded, approvedQuotation.Code);

        var order = await service.ConvertQuotationAsync(
            fixture.Context(Creator, permission: "tenant.sales.quotation.convert"),
            quotation.Value.Id,
            approvedQuotation.Value!.Version,
            "service-policy-order-convert");
        Assert.True(order.Succeeded, order.Code);

        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-c", 3, Approver),
            ApprovalOption("order", "order-b", 2, ApproverTwo)));
        var submittedOrder = await service.TransitionOrderAsync(
            fixture.Context(Creator, permission: "tenant.sales.order.submit"),
            order.Value!.Id,
            SalesOrderStatus.PendingApproval,
            null,
            order.Value.Version,
            "service-policy-order-submit");
        Assert.True(submittedOrder.Succeeded, submittedOrder.Code);
        Assert.Equal("order-b", submittedOrder.Value!.ApprovalState!.PolicyId);
        Assert.Equal(2, submittedOrder.Value.ApprovalState.PolicyVersion);

        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-c", 3, Approver),
            ApprovalOption("order", "order-c", 3, Approver)));
        var approvedOrder = await service.TransitionOrderAsync(
            fixture.Context(ApproverTwo, permission: "tenant.sales.order.approve"),
            order.Value.Id,
            SalesOrderStatus.Approved,
            "approved using submitted snapshot",
            submittedOrder.Value.Version,
            "service-policy-order-approve");
        Assert.True(approvedOrder.Succeeded, approvedOrder.Code);
        Assert.Equal(SalesOrderStatus.Approved, approvedOrder.Value!.Status);
        Assert.Equal("order-b", approvedOrder.Value.ApprovalState!.PolicyId);
    }

    [Fact]
    public async Task Pending_cancellation_uses_the_stored_policy_snapshot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-cancel-a", 1, Approver, allowCancellation: true),
            ApprovalOption("order", "order-a", 1, Approver)));
        var service = fixture.CreateService(options, new ControllableFinanceSettlementPersistence());

        var quotation = await service.CreateQuotationAsync(fixture.Context(Creator), CreateQuotationRequest(), "service-cancel-create");
        Assert.True(quotation.Succeeded, quotation.Code);
        var submitted = await service.TransitionQuotationAsync(
            fixture.Context(Creator, permission: "tenant.sales.quotation.submit"),
            quotation.Value!.Id,
            SalesQuotationStatus.PendingApproval,
            null,
            quotation.Value.Version,
            "service-cancel-submit");
        Assert.True(submitted.Succeeded, submitted.Code);

        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-cancel-b", 2, Approver, allowCancellation: false),
            ApprovalOption("order", "order-a", 1, Approver)));
        var cancelled = await service.TransitionQuotationAsync(
            fixture.Context(Creator, permission: "tenant.sales.quotation.cancel"),
            quotation.Value.Id,
            SalesQuotationStatus.Cancelled,
            "requester cancellation",
            submitted.Value!.Version,
            "service-cancel-cancel");

        Assert.True(cancelled.Succeeded, cancelled.Code);
        Assert.Equal(SalesQuotationStatus.Cancelled, cancelled.Value!.Status);
    }

    [Fact]
    public async Task Credit_evaluation_persists_eligible_warning_blocked_hold_and_unknown_outcomes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-credit", 1, Approver),
            ApprovalOption("order", "order-credit", 1, Approver),
            creditLimit: 500m));
        var service = fixture.CreateService(options, finance);
        var order = await CreateApprovedOrderAsync(fixture, service);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);

        finance.Exposure = Exposure(100m, 0m, 100m, creditHold: false, asOf);
        var eligible = await ConfirmOrderAsync(fixture, service, order, "credit-eligible");
        Assert.True(eligible.Succeeded, eligible.Code);
        Assert.Equal(SalesOrderStatus.Confirmed, eligible.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Eligible, eligible.Value.CreditOutcome);
        var eligibleCredit = await service.GetOrderCreditAsync(fixture.Context(Approver), order.Id);
        Assert.Equal(SalesCreditOutcome.Eligible, eligibleCredit!.Outcome);
        Assert.Equal("SAR", eligibleCredit.CurrencyCode);
        Assert.Equal("SAR", eligibleCredit.TransactionCurrencyCode);
        Assert.Equal(150m, eligibleCredit.TransactionAmount);
        Assert.Equal(150m, eligibleCredit.ConvertedOrderCommitment);
        Assert.Null(eligibleCredit.ExchangeRateEvidence);
        Assert.Equal(250m, eligibleCredit.ProposedExposure);
        Assert.Equal(500m, eligibleCredit.CreditLimit);

        var warningOrder = await CreateApprovedOrderAsync(fixture, service, "credit-warning");
        finance.Exposure = Exposure(100m, 25m, 100m, creditHold: false, asOf);
        var warning = await ConfirmOrderAsync(fixture, service, warningOrder, "credit-warning-confirm");
        Assert.True(warning.Succeeded, warning.Code);
        Assert.Equal(SalesOrderStatus.Confirmed, warning.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Warning, warning.Value.CreditOutcome);

        var blockedOrder = await CreateApprovedOrderAsync(fixture, service, "credit-blocked");
        finance.Exposure = Exposure(400m, 0m, 400m, creditHold: false, asOf);
        var blocked = await ConfirmOrderAsync(fixture, service, blockedOrder, "credit-blocked-confirm");
        Assert.True(blocked.Succeeded, blocked.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, blocked.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Blocked, blocked.Value.CreditOutcome);
        Assert.Equal("credit_limit_exceeded", blocked.Value.CreditReason);

        var financeHoldOrder = await CreateApprovedOrderAsync(fixture, service, "credit-finance-hold");
        finance.Exposure = Exposure(100m, 0m, 100m, creditHold: true, asOf, "Finance hold");
        var financeHold = await ConfirmOrderAsync(fixture, service, financeHoldOrder, "credit-finance-hold-confirm");
        Assert.True(financeHold.Succeeded, financeHold.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, financeHold.Value!.Status);
        Assert.Equal("finance_credit_hold", financeHold.Value.CreditReason);

        var unknownOrder = await CreateApprovedOrderAsync(fixture, service, "credit-unknown");
        finance.Exposure = null;
        var unknown = await ConfirmOrderAsync(fixture, service, unknownOrder, "credit-unknown-confirm");
        Assert.True(unknown.Succeeded, unknown.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, unknown.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Unknown, unknown.Value.CreditOutcome);
        Assert.Equal("credit_truth_unavailable", unknown.Value.CreditReason);
    }

    [Fact]
    public async Task Credit_evaluation_converts_foreign_order_into_finance_currency_and_reloads_durable_evidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-credit-fx", 1, Approver, currencyCode: null),
            ApprovalOption("order", "order-credit-fx", 1, Approver, currencyCode: null),
            creditLimit: 3000m));
        var service = fixture.CreateService(options, finance, "USD", new ExchangeRateReferenceFake());
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        finance.Exposure = Exposure(1000m, 0m, 1000m, creditHold: false, asOf);

        var order = await CreateApprovedOrderAsync(fixture, service, "credit-fx", 10m, ExchangeRateA);
        Assert.Equal("USD", order.CurrencyCode);
        Assert.Equal(500m, order.Total);
        var confirmed = await ConfirmOrderAsync(fixture, service, order, "credit-fx-confirm");

        Assert.True(confirmed.Succeeded, confirmed.Code);
        Assert.Equal(SalesOrderStatus.Confirmed, confirmed.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Eligible, confirmed.Value.CreditOutcome);

        var credit = await service.GetOrderCreditAsync(fixture.Context(Approver), order.Id);
        Assert.NotNull(credit);
        Assert.Equal("SAR", credit!.CurrencyCode);
        Assert.Equal("USD", credit.TransactionCurrencyCode);
        Assert.Equal(500m, credit.TransactionAmount);
        Assert.Equal(1875m, credit.ConvertedOrderCommitment);
        Assert.Equal(2875m, credit.ProposedExposure);
        Assert.Equal(3000m, credit.CreditLimit);
        Assert.Equal(1, credit.OrderRevisionNumber);
        Assert.Equal("USD", credit.ExchangeRateEvidence!.SourceCurrencyCode);
        Assert.Equal("SAR", credit.ExchangeRateEvidence.TargetCurrencyCode);
        Assert.Equal(3.75m, credit.ExchangeRateEvidence.Rate);

        var history = await fixture.Persistence.ListHistoryAsync(fixture.Context(Approver), "order", order.Id);
        Assert.Contains(history, item => item.Action == nameof(SalesHistoryAction.CreditEvaluated)
            && item.SnapshotJson?.Contains("\"convertedOrderCommitment\":1875", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Credit_evaluation_converts_before_comparing_against_the_finance_currency_limit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-credit-limit-currency", 1, Approver, currencyCode: null),
            ApprovalOption("order", "order-credit-limit-currency", 1, Approver, currencyCode: null),
            creditLimit: 1500m));
        var service = fixture.CreateService(options, finance, "USD", new ExchangeRateReferenceFake());
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        finance.Exposure = Exposure(1000m, 0m, 1000m, creditHold: false, asOf);

        var order = await CreateApprovedOrderAsync(fixture, service, "credit-limit-currency", 10m, ExchangeRateA);
        var result = await ConfirmOrderAsync(fixture, service, order, "credit-limit-currency-confirm");

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, result.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Blocked, result.Value.CreditOutcome);
        Assert.Equal("credit_limit_exceeded", result.Value.CreditReason);
        var credit = await service.GetOrderCreditAsync(fixture.Context(Approver), order.Id);
        Assert.Equal("SAR", credit!.CurrencyCode);
        Assert.Equal(1875m, credit.ConvertedOrderCommitment);
        Assert.Equal(2875m, credit.ProposedExposure);
        Assert.Equal(1500m, credit.CreditLimit);
    }

    [Fact]
    public async Task Credit_evaluation_does_not_use_a_transaction_currency_limit_when_the_required_limit_is_missing()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-credit-limit-missing", 1, Approver, currencyCode: null),
            ApprovalOption("order", "order-credit-limit-missing", 1, Approver, currencyCode: null),
            creditLimit: 10000m,
            creditCurrencyCode: "USD"));
        var service = fixture.CreateService(options, finance, "USD", new ExchangeRateReferenceFake());
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        finance.Exposure = Exposure(1000m, 0m, 1000m, creditHold: false, asOf);

        var order = await CreateApprovedOrderAsync(fixture, service, "credit-limit-missing", 10m, ExchangeRateA);
        var result = await ConfirmOrderAsync(fixture, service, order, "credit-limit-missing-confirm");

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, result.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Unknown, result.Value.CreditOutcome);
        Assert.Equal("credit_limit_unavailable", result.Value.CreditReason);
        var credit = await service.GetOrderCreditAsync(fixture.Context(Approver), order.Id);
        Assert.Equal("SAR", credit!.CurrencyCode);
        Assert.Equal(1875m, credit.ConvertedOrderCommitment);
        Assert.Null(credit.CreditLimit);
    }

    [Fact]
    public async Task Credit_evaluation_fails_closed_for_missing_mismatched_and_invalid_persisted_fx_evidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-credit-fx-invalid", 1, Approver, currencyCode: null),
            ApprovalOption("order", "order-credit-fx-invalid", 1, Approver, currencyCode: null),
            creditLimit: 3000m));
        var service = fixture.CreateService(options, finance, "USD", new ExchangeRateReferenceFake());
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        finance.Exposure = Exposure(1000m, 0m, 1000m, creditHold: false, asOf);

        var missing = await CreateApprovedOrderAsync(fixture, service, "credit-fx-missing", 10m, ExchangeRateA);
        await fixture.SetOrderExchangeRateAsync(missing.Id, null);
        var missingResult = await ConfirmOrderAsync(fixture, service, missing, "credit-fx-missing-confirm");
        Assert.True(missingResult.Succeeded, missingResult.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, missingResult.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Unknown, missingResult.Value.CreditOutcome);
        Assert.Equal("credit_fx_evidence_missing", missingResult.Value.CreditReason);

        var mismatched = await CreateApprovedOrderAsync(fixture, service, "credit-fx-mismatch", 10m, ExchangeRateA);
        await fixture.SetOrderExchangeRateAsync(mismatched.Id, mismatched.ExchangeRateEvidence! with { TargetCurrencyCode = "EUR" });
        var mismatchedResult = await ConfirmOrderAsync(fixture, service, mismatched, "credit-fx-mismatch-confirm");
        Assert.True(mismatchedResult.Succeeded, mismatchedResult.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, mismatchedResult.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Unknown, mismatchedResult.Value.CreditOutcome);
        Assert.Equal("credit_fx_target_currency_mismatch", mismatchedResult.Value.CreditReason);

        var invalid = await CreateApprovedOrderAsync(fixture, service, "credit-fx-invalid", 10m, ExchangeRateA);
        await fixture.SetOrderExchangeRateAsync(invalid.Id, invalid.ExchangeRateEvidence! with { Rate = 0m });
        var invalidResult = await ConfirmOrderAsync(fixture, service, invalid, "credit-fx-invalid-confirm");
        Assert.True(invalidResult.Succeeded, invalidResult.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, invalidResult.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Unknown, invalidResult.Value.CreditOutcome);
        Assert.Equal("credit_fx_rate_invalid", invalidResult.Value.CreditReason);
    }

    [Fact]
    public async Task Credit_override_is_invalidated_when_persisted_fx_evidence_changes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-credit-fx-override", 1, Approver, currencyCode: null),
            ApprovalOption("order", "order-credit-fx-override", 1, Approver, currencyCode: null),
            creditLimit: 1500m));
        var service = fixture.CreateService(options, finance, "USD", new ExchangeRateReferenceFake());
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        finance.Exposure = Exposure(1000m, 0m, 1000m, creditHold: false, asOf);
        var order = await CreateApprovedOrderAsync(fixture, service, "credit-fx-override", 10m, ExchangeRateA);
        var hold = await ConfirmOrderAsync(fixture, service, order, "credit-fx-override-hold");
        Assert.True(hold.Succeeded, hold.Code);

        var overridden = await service.OverrideCreditAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.credit.override"),
            order.Id,
            new SalesCreditOverrideRequest("foreign currency exception", DateTimeOffset.UtcNow.AddHours(1), "finance", "fx-override-1"),
            hold.Value!.Version,
            "credit-fx-override-set");
        Assert.True(overridden.Succeeded, overridden.Code);

        await fixture.SetOrderExchangeRateAsync(order.Id, overridden.Value!.ExchangeRateEvidence! with { Rate = 4m });
        var changed = await ConfirmOrderAsync(fixture, service, overridden.Value, "credit-fx-override-changed");

        Assert.True(changed.Succeeded, changed.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, changed.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Blocked, changed.Value.CreditOutcome);
        var credit = await service.GetOrderCreditAsync(fixture.Context(Approver), order.Id);
        Assert.Equal(2000m, credit!.ConvertedOrderCommitment);
        Assert.Equal(3000m, credit.ProposedExposure);
    }

    [Fact]
    public async Task Credit_override_requires_authority_and_is_invalidated_by_expiry_exposure_and_limit_changes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-override", 1, Approver),
            ApprovalOption("order", "order-override", 1, Approver),
            creditLimit: 500m));
        var service = fixture.CreateService(options, finance);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);

        finance.Exposure = Exposure(400m, 0m, 400m, creditHold: false, asOf);
        var order = await CreateApprovedOrderAsync(fixture, service, "credit-override");
        var hold = await ConfirmOrderAsync(fixture, service, order, "credit-override-hold");
        Assert.True(hold.Succeeded, hold.Code);

        var self = await service.OverrideCreditAsync(
            fixture.Context(Creator, permission: "tenant.sales.order.credit.override"),
            order.Id,
            new SalesCreditOverrideRequest("self", DateTimeOffset.UtcNow.AddHours(1), "test", "self"),
            hold.Value!.Version,
            "credit-override-self");
        Assert.False(self.Succeeded);
        Assert.Equal("self_approval_denied", self.Code);

        var unauthorized = await service.OverrideCreditAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.confirm"),
            order.Id,
            new SalesCreditOverrideRequest("wrong permission", DateTimeOffset.UtcNow.AddHours(1), null, null),
            hold.Value.Version,
            "credit-override-unauthorized");
        Assert.False(unauthorized.Succeeded);
        Assert.Equal("permission_denied", unauthorized.Code);

        var overridden = await service.OverrideCreditAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.credit.override"),
            order.Id,
            new SalesCreditOverrideRequest("approved exception", DateTimeOffset.UtcNow.AddHours(1), "finance", "approval-1"),
            hold.Value.Version,
            "credit-override-valid");
        Assert.True(overridden.Succeeded, overridden.Code);
        Assert.Equal(SalesOrderStatus.Approved, overridden.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Overridden, overridden.Value.CreditOutcome);

        var confirmed = await ConfirmOrderAsync(fixture, service, overridden.Value, "credit-override-reuse");
        Assert.True(confirmed.Succeeded, confirmed.Code);
        Assert.Equal(SalesOrderStatus.Confirmed, confirmed.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Overridden, confirmed.Value.CreditOutcome);

        var history = await fixture.Persistence.ListHistoryAsync(fixture.Context(Approver), "order", order.Id);
        Assert.Contains(history, item => item.Action == nameof(SalesHistoryAction.CreditEvaluated));
        Assert.Contains(history, item => item.Action == nameof(SalesHistoryAction.CreditOverridden));
        var audit = await fixture.Persistence.ListAuditAsync(fixture.Context(Approver), "order", order.Id);
        Assert.Contains(audit, item => item.OperationId == "sales.order.credit.override" && item.Decision == "Allowed");

        var expiredOrder = await CreateApprovedOrderAsync(fixture, service, "credit-override-expired");
        var expiredHold = await ConfirmOrderAsync(fixture, service, expiredOrder, "credit-override-expired-hold");
        Assert.True(expiredHold.Succeeded, expiredHold.Code);
        var expired = await service.OverrideCreditAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.credit.override"),
            expiredOrder.Id,
            new SalesCreditOverrideRequest("short exception", DateTimeOffset.UtcNow.AddMilliseconds(150), null, null),
            expiredHold.Value!.Version,
            "credit-override-expired-set");
        Assert.True(expired.Succeeded, expired.Code);
        await Task.Delay(350);
        var expiredConfirmation = await ConfirmOrderAsync(fixture, service, expired.Value!, "credit-override-expired-confirm");
        Assert.True(expiredConfirmation.Succeeded, expiredConfirmation.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, expiredConfirmation.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Blocked, expiredConfirmation.Value.CreditOutcome);

        var exposureChangedOrder = await CreateApprovedOrderAsync(fixture, service, "credit-override-exposure");
        var exposureHold = await ConfirmOrderAsync(fixture, service, exposureChangedOrder, "credit-override-exposure-hold");
        Assert.True(exposureHold.Succeeded, exposureHold.Code);
        var exposureOverride = await service.OverrideCreditAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.credit.override"),
            exposureChangedOrder.Id,
            new SalesCreditOverrideRequest("exposure exception", DateTimeOffset.UtcNow.AddHours(1), null, null),
            exposureHold.Value!.Version,
            "credit-override-exposure-set");
        Assert.True(exposureOverride.Succeeded, exposureOverride.Code);
        finance.Exposure = Exposure(401m, 0m, 401m, creditHold: false, asOf);
        var exposureChanged = await ConfirmOrderAsync(fixture, service, exposureOverride.Value!, "credit-override-exposure-confirm");
        Assert.True(exposureChanged.Succeeded, exposureChanged.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, exposureChanged.Value!.Status);
        var exposureCredit = await service.GetOrderCreditAsync(fixture.Context(Approver), exposureChangedOrder.Id);
        Assert.Equal(401m, exposureCredit!.NetReceivableExposure);
        Assert.Equal(551m, exposureCredit.ProposedExposure);

        var limitChangedOrder = await CreateApprovedOrderAsync(fixture, service, "credit-override-limit");
        finance.Exposure = Exposure(100m, 0m, 100m, creditHold: false, asOf);
        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-override", 1, Approver),
            ApprovalOption("order", "order-override", 1, Approver),
            creditLimit: 200m));
        var limitHold = await ConfirmOrderAsync(fixture, service, limitChangedOrder, "credit-override-limit-hold");
        Assert.True(limitHold.Succeeded, limitHold.Code);
        var limitOverride = await service.OverrideCreditAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.credit.override"),
            limitChangedOrder.Id,
            new SalesCreditOverrideRequest("limit exception", DateTimeOffset.UtcNow.AddHours(1), null, null),
            limitHold.Value!.Version,
            "credit-override-limit-set");
        Assert.True(limitOverride.Succeeded, limitOverride.Code);
        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-override", 1, Approver),
            ApprovalOption("order", "order-override", 1, Approver),
            creditLimit: 240m));
        var limitChanged = await ConfirmOrderAsync(fixture, service, limitOverride.Value!, "credit-override-limit-confirm");
        Assert.True(limitChanged.Succeeded, limitChanged.Code);
        Assert.Equal(SalesOrderStatus.CreditHold, limitChanged.Value!.Status);
        var limitCredit = await service.GetOrderCreditAsync(fixture.Context(Approver), limitChangedOrder.Id);
        Assert.Equal(240m, limitCredit!.CreditLimit);
    }

    [Fact]
    public async Task Material_order_edit_resets_credit_and_approval_state_before_resubmission_snapshot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finance = new ControllableFinanceSettlementPersistence();
        var options = new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(
            ApprovalOption("quotation", "quotation-edit", 1, Approver),
            ApprovalOption("order", "order-edit-a", 1, Approver),
            creditLimit: 500m));
        var service = fixture.CreateService(options, finance);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        finance.Exposure = Exposure(400m, 0m, 400m, creditHold: false, asOf);
        var order = await CreateApprovedOrderAsync(fixture, service, "credit-edit");
        var hold = await ConfirmOrderAsync(fixture, service, order, "credit-edit-hold");
        Assert.True(hold.Succeeded, hold.Code);
        var overrideResult = await service.OverrideCreditAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.credit.override"),
            order.Id,
            new SalesCreditOverrideRequest("edit requires recheck", DateTimeOffset.UtcNow.AddHours(1), null, null),
            hold.Value!.Version,
            "credit-edit-override");
        Assert.True(overrideResult.Succeeded, overrideResult.Code);

        var returned = await service.TransitionOrderAsync(
            fixture.Context(Approver, permission: "tenant.sales.order.return"),
            order.Id,
            SalesOrderStatus.ReturnedForChange,
            "material commercial edit",
            overrideResult.Value!.Version,
            "credit-edit-return");
        Assert.True(returned.Succeeded, returned.Code);

        var edited = await service.EditOrderAsync(
            fixture.Context(Creator, permission: "tenant.sales.order.edit"),
            order.Id,
            new SalesOrderEditRequest(CurrencyA, PriceListA, [new SalesQuotationLineRequest(ProductA, UomA, 4m)]),
            returned.Value!.Version,
            "credit-edit-edit");
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal(SalesOrderStatus.Draft, edited.Value!.Status);
        Assert.Equal(SalesCreditOutcome.Unknown, edited.Value.CreditOutcome);
        Assert.Null(edited.Value.CreditOverrideExpiresAt);
        Assert.Null(edited.Value.ApprovalState);
        Assert.Equal(200m, edited.Value.Total);

        options.Set(RuntimeOptions(
            ApprovalOption("quotation", "quotation-edit", 1, Approver),
            ApprovalOption("order", "order-edit-b", 2, ApproverTwo),
            creditLimit: 500m));
        var resubmitted = await service.TransitionOrderAsync(
            fixture.Context(Creator, permission: "tenant.sales.order.submit"),
            order.Id,
            SalesOrderStatus.PendingApproval,
            null,
            edited.Value.Version,
            "credit-edit-resubmit");
        Assert.True(resubmitted.Succeeded, resubmitted.Code);
        Assert.Equal("order-edit-b", resubmitted.Value!.ApprovalState!.PolicyId);
        Assert.Equal(2, resubmitted.Value.ApprovalState.PolicyVersion);
    }

    private static SalesPolicyOptions RuntimeOptions(
        SalesApprovalPolicyOptions quotationPolicy,
        SalesApprovalPolicyOptions orderPolicy,
        decimal creditLimit = 500m,
        string creditCurrencyCode = "SAR") => new()
    {
        ApprovalPolicies = [quotationPolicy, orderPolicy],
        CreditLimits = [new SalesCreditLimitOptions
        {
            TenantId = TenantA,
            CompanyId = CompanyA,
            CustomerId = CustomerA,
            CurrencyCode = creditCurrencyCode,
            Limit = creditLimit,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        }]
    };

    private static SalesApprovalPolicyOptions ApprovalOption(string documentType, string policyId, int version, Guid eligibleApprover, bool allowCancellation = true, string? currencyCode = "SAR") => new()
    {
        TenantId = TenantA,
        CompanyId = CompanyA,
        BranchId = BranchA,
        DocumentType = documentType,
        PolicyId = policyId,
        Version = version,
        Stages = [new SalesApprovalStageOptions
        {
            StageKey = "commercial",
            Sequence = 1,
            RequiredApprovals = 1,
            EligibleApproverIds = [eligibleApprover]
        }],
        AllowRequesterCancellationWhilePending = allowCancellation,
        EffectiveFrom = DateTimeOffset.MinValue,
        CurrencyCode = currencyCode
    };

    private static SalesQuotationCreateRequest CreateQuotationRequest(decimal quantity = 3m, Guid? exchangeRateId = null) => new(
        CompanyA,
        BranchA,
        CustomerA,
        new DateOnly(2026, 8, 28),
        new DateOnly(2026, 9, 30),
        CurrencyA,
        PriceListA,
        null,
        "Sales service test",
        null,
        [new SalesQuotationLineRequest(ProductA, UomA, quantity)],
        exchangeRateId);

    private static FinanceCustomerExposureRecord Exposure(decimal open, decimal overdue, decimal net, bool creditHold, DateOnly asOf, string? holdReason = null) =>
        new(CompanyA, CustomerA, "SAR", open, overdue, 0m, net, asOf, creditHold, holdReason);

    private static async Task<SalesOrderResponse> CreateApprovedOrderAsync(Fixture fixture, SalesService service, string keyPrefix = "sales-service", decimal quantity = 3m, Guid? exchangeRateId = null)
    {
        var quotation = await service.CreateQuotationAsync(fixture.Context(Creator, permission: "tenant.sales.quotation.create"), CreateQuotationRequest(quantity, exchangeRateId), $"{keyPrefix}-quotation-create");
        Assert.True(quotation.Succeeded, quotation.Code);
        var submittedQuotation = await service.TransitionQuotationAsync(fixture.Context(Creator, permission: "tenant.sales.quotation.submit"), quotation.Value!.Id, SalesQuotationStatus.PendingApproval, null, quotation.Value.Version, $"{keyPrefix}-quotation-submit");
        Assert.True(submittedQuotation.Succeeded, submittedQuotation.Code);
        var approvedQuotation = await service.TransitionQuotationAsync(fixture.Context(Approver, permission: "tenant.sales.quotation.approve"), quotation.Value.Id, SalesQuotationStatus.Approved, null, submittedQuotation.Value!.Version, $"{keyPrefix}-quotation-approve");
        Assert.True(approvedQuotation.Succeeded, approvedQuotation.Code);
        var order = await service.ConvertQuotationAsync(fixture.Context(Creator, permission: "tenant.sales.quotation.convert"), quotation.Value.Id, approvedQuotation.Value!.Version, $"{keyPrefix}-order-convert");
        Assert.True(order.Succeeded, order.Code);
        var submittedOrder = await service.TransitionOrderAsync(fixture.Context(Creator, permission: "tenant.sales.order.submit"), order.Value!.Id, SalesOrderStatus.PendingApproval, null, order.Value.Version, $"{keyPrefix}-order-submit");
        Assert.True(submittedOrder.Succeeded, submittedOrder.Code);
        var approvedOrder = await service.TransitionOrderAsync(fixture.Context(Approver, permission: "tenant.sales.order.approve"), order.Value.Id, SalesOrderStatus.Approved, null, submittedOrder.Value!.Version, $"{keyPrefix}-order-approve");
        Assert.True(approvedOrder.Succeeded, approvedOrder.Code);
        return approvedOrder.Value!;
    }

    private static Task<SalesOperationResult<SalesOrderResponse>> ConfirmOrderAsync(Fixture fixture, SalesService service, SalesOrderResponse order, string key) =>
        service.TransitionOrderAsync(fixture.Context(Approver, permission: "tenant.sales.order.confirm"), order.Id, SalesOrderStatus.Confirmed, null, order.Version, key);

    private static SalesOrderResponse HandoffOrder()
    {
        var line = new SalesQuotationLineResponse(Guid.NewGuid(), ProductA, "SKU-001", "Product A", UomA, "EA", 1m, 100m, 100m, 0m, 0m, 0m, 100m, PriceListA, 1, new DateOnly(2026, 1, 1), "PriceList", "price", false, null, null, null, null);
        var term = new SalesPaymentTermSnapshot(PaymentTermA, "NET4", "Net 4", null, PaymentTermVersionA, 2, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, PaymentTermBaseDateRule.DocumentDate, PaymentTermScheduleMode.SingleDueDate, 4, 0, "masterdata.payment-term", "NET4;v2", []);
        return new SalesOrderResponse(Guid.NewGuid(), "SO-HANDOFF", TenantA, CompanyA, BranchA, CustomerA, "CUST-001", "Customer A", Creator, Guid.NewGuid(), "SQ-HANDOFF", 1, CurrencyA, "SAR", 100m, 0m, 0m, 100m, SalesOrderStatus.Confirmed, SalesCreditOutcome.Eligible, null, DateTimeOffset.UtcNow, null, [line], [1], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, 1, null, term);
    }

    private static SalesService CreateHandoffService(CapturingSalesPersistence persistence, ISalesInventoryPort inventory, IFinanceSettlementPersistence finance) => new(
        persistence,
        new SalesAuthorizationService(new PurchaseRequestAuthorizationService()),
        new ConfigurationSalesApprovalPolicyProvider(new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(ApprovalOption("quotation", "handoff", 1, Approver), ApprovalOption("order", "handoff", 1, Approver)))),
        new NoSalesCommercialAuthorityProvider(),
        new ConfigurationSalesApprovalDelegationProvider(new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(ApprovalOption("quotation", "handoff", 1, Approver), ApprovalOption("order", "handoff", 1, Approver)))),
        new ConfigurationSalesCreditLimitProvider(new MutableOptionsMonitor<SalesPolicyOptions>(RuntimeOptions(ApprovalOption("quotation", "handoff", 1, Approver), ApprovalOption("order", "handoff", 1, Approver)))),
        finance,
        new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantA, CompanyA, "Company A", "SAR", BranchA)]),
        new CustomerReferenceFake(),
        new ProductReferenceFake(),
        new PriceReferenceFake("SAR"),
        new UnavailableSalesTaxReferenceProvider(),
        new UnavailableSalesExchangeRateReferenceProvider(),
        inventory);

    private static ProcurementRequestContext Context(Guid actor, Guid tenantId = default, Guid companyId = default, string permission = "tenant.sales.quotation.create")
    {
        tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
        companyId = companyId == Guid.Empty ? CompanyA : companyId;
        var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{companyId:D}"), new CorrelationId($"sales-{Guid.NewGuid():N}"), actor);
        var foundation = FoundationRequestContext.ForTenant(actor, Guid.NewGuid(), tenantContext, permission);
        var resolution = new ProcurementTenantContextResolver().Resolve(foundation);
        return Assert.IsType<ProcurementRequestContext>(resolution.Context);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(SqliteConnection connection, DbContextOptions options)
        {
            this.connection = connection;
            this.options = options;
            Persistence = new SalesPersistence(options);
        }

        public SalesPersistence Persistence { get; }
        public DbContextOptions Options => options;

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            await using (var db = new SalesDbContext(options, TenantContext(TenantA, Creator, CompanyA))) await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, options);
        }

        public ProcurementRequestContext Context(Guid actor, Guid tenantId = default, Guid companyId = default, string permission = "tenant.sales.quotation.create") => SalesTests.Context(actor, tenantId == Guid.Empty ? TenantA : tenantId, companyId == Guid.Empty ? CompanyA : companyId, permission);

        public SalesService CreateService(IOptionsMonitor<SalesPolicyOptions> policies, IFinanceSettlementPersistence finance, string priceCurrencyCode = "SAR", ISalesExchangeRateReferenceProvider? exchangeRates = null) => new(
            Persistence,
            new SalesAuthorizationService(new PurchaseRequestAuthorizationService()),
            new ConfigurationSalesApprovalPolicyProvider(policies),
            new NoSalesCommercialAuthorityProvider(),
            new ConfigurationSalesApprovalDelegationProvider(policies),
            new ConfigurationSalesCreditLimitProvider(policies),
            finance,
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantA, CompanyA, "Company A", "SAR", BranchA)]),
            new CustomerReferenceFake(),
            new ProductReferenceFake(),
            new PriceReferenceFake(priceCurrencyCode),
            new UnavailableSalesTaxReferenceProvider(),
            exchangeRates ?? new UnavailableSalesExchangeRateReferenceProvider());

        public async Task SetOrderExchangeRateAsync(Guid orderId, SalesExchangeRateEvidence? evidence)
        {
            await using var db = new SalesDbContext(options, TenantContext(TenantA, Creator, CompanyA));
            if (evidence is null)
                await db.Database.ExecuteSqlRawAsync("UPDATE SalesOrders SET ExchangeRateJson = NULL WHERE Id = {0}", orderId);
            else
                await db.Database.ExecuteSqlRawAsync("UPDATE SalesOrders SET ExchangeRateJson = {0} WHERE Id = {1}", JsonSerializer.Serialize(evidence), orderId);
        }

        public SalesApprovalPolicyDefinition Policy() => new("sales.test.policy", 7, [new SalesApprovalStageDefinition("commercial", 1, 1, [Approver], false)], true, false, DateTimeOffset.MinValue, null);

        public SalesQuotationWriteModel Model(Guid? id = null) => new(
            id ?? Guid.NewGuid(), CompanyA, BranchA, CustomerA, "CUST-001", "Customer A", new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 30), CurrencyA, "SAR", "contact-1", "Commercial note", null,
            [new SalesLineWriteModel(Guid.NewGuid(), ProductA, "SKU-001", "Product A", UomA, "EA", 3m, 50m, 50m, 0m, 0m, 0m, 150m, PriceListA, 4, new DateOnly(2026, 8, 1), "PriceList", "price-source-4", false, null, null, null, "line note")],
            150m, 0m, 0m, 150m,
            PaymentTerm: new SalesPaymentTermSnapshot(PaymentTermA, "NET4", "Net 4", null, PaymentTermVersionA, 2, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, PaymentTermBaseDateRule.DocumentDate, PaymentTermScheduleMode.SingleDueDate, 4, 0, "masterdata.payment-term", "NET4;v2", []));

        private static TenantContext TenantContext(Guid tenantId, Guid actor, Guid companyId) => MiniErp.App.BuildingBlocks.Tenancy.TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{companyId:D}"), new CorrelationId($"sales-fixture-{Guid.NewGuid():N}"), actor);

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private sealed class MutableOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; private set; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;

        public void Set(T value) => CurrentValue = value;

        private sealed class NoopDisposable : IDisposable
        {
            internal static NoopDisposable Instance { get; } = new();
            public void Dispose() { }
        }
    }

    private sealed class ControllableFinanceSettlementPersistence : IFinanceSettlementPersistence
    {
        private readonly UnavailableFinanceSettlementPersistence fallback = new();
        public FinanceCustomerExposureRecord? Exposure { get; set; }
        public FinanceOpenItemRecord? SalesInvoiceResult { get; set; }
        public FinanceSalesInvoiceCommand? LastSalesInvoiceCommand { get; private set; }
        public bool SalesInvoiceCommitted { get; private set; }
        public int SalesInvoicePostAttempts { get; private set; }
        public bool ReturnWrongSalesInvoiceEvidence { get; set; }
        public Guid? ReturnedSalesInvoiceTenantId { get; set; }
        public int? ReturnedSalesInvoiceRevision { get; set; }
        private readonly HashSet<Guid> salesInvoiceEffects = [];
        public IReadOnlyCollection<Guid> SalesInvoiceEffects => salesInvoiceEffects;

        public Task<IReadOnlyList<FinancePaymentMethodRecord>> ListPaymentMethodsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListPaymentMethodsAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> CreatePaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default) => fallback.CreatePaymentMethodAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> EditPaymentMethodAsync(FinanceRequestContext context, FinancePaymentMethodCommand command, CancellationToken cancellationToken = default) => fallback.EditPaymentMethodAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinancePaymentMethodRecord>> SetPaymentMethodLifecycleAsync(FinanceRequestContext context, Guid methodId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => fallback.SetPaymentMethodLifecycleAsync(context, methodId, companyId, lifecycle, expectedVersion, idempotencyKey, fingerprint, cancellationToken);
        public Task<IReadOnlyList<FinanceCashAccountRecord>> ListCashAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListCashAccountsAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> CreateCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, CancellationToken cancellationToken = default) => fallback.CreateCashAccountAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> EditCashAccountAsync(FinanceRequestContext context, FinanceCashAccountCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default) => fallback.EditCashAccountAsync(context, command, expectedVersion, cancellationToken);
        public Task<FinanceOperationResult<FinanceCashAccountRecord>> SetCashAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinancePaymentMethodLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => fallback.SetCashAccountLifecycleAsync(context, accountId, companyId, lifecycle, expectedVersion, idempotencyKey, fingerprint, cancellationToken);
        public Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, string resource, Guid resourceId, CancellationToken cancellationToken = default) => fallback.ResolveCompanyIdAsync(context, resource, resourceId, cancellationToken);
        public Task<IReadOnlyList<FinanceOpenItemRecord>> ListOpenItemsAsync(FinanceRequestContext context, FinanceOpenItemKind kind, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListOpenItemsAsync(context, kind, companyId, cancellationToken);
        public Task<FinanceOpenItemRecord?> GetOpenItemAsync(FinanceRequestContext context, Guid itemId, FinanceOpenItemKind? expectedKind = null, CancellationToken cancellationToken = default) => fallback.GetOpenItemAsync(context, itemId, expectedKind, cancellationToken);
        public Task<IReadOnlyList<FinanceApSourceReadyRecord>> ListApSourceReadyAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) => fallback.ListApSourceReadyAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> RecognizeSupplierInvoiceAsync(FinanceRequestContext context, FinanceSupplierInvoiceRecognitionCommand command, CancellationToken cancellationToken = default) => fallback.RecognizeSupplierInvoiceAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateManualReceivableAsync(FinanceRequestContext context, FinanceManualReceivableCommand command, CancellationToken cancellationToken = default) => fallback.CreateManualReceivableAsync(context, command, cancellationToken);
        public Task<FinanceArOpeningPreflightResult> PreflightMigrationArOpeningAsync(FinanceRequestContext context, FinanceMigrationArOpeningCommand command, CancellationToken cancellationToken = default) => fallback.PreflightMigrationArOpeningAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateMigrationArOpeningAsync(FinanceRequestContext context, FinanceMigrationArOpeningCommand command, CancellationToken cancellationToken = default) => fallback.CreateMigrationArOpeningAsync(context, command, cancellationToken);
        public Task<FinanceMigrationArOpeningEvidence?> ReadMigrationArOpeningAsync(FinanceRequestContext context, FinanceMigrationArOpeningCommand command, CancellationToken cancellationToken = default) => fallback.ReadMigrationArOpeningAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>> EvaluateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default) => SalesInvoiceResult is null ? fallback.EvaluateSalesInvoiceAsync(context, command, cancellationToken) : Task.FromResult(FinanceOperationResult<FinanceSalesInvoiceEligibilityRecord>.Success(new(true, "eligible", command.Amount, command.CurrencyCode, command.SalesOrderId, command.SalesOrderRevision, command.DocumentDate, command.PaymentTerm, null, null, null, null)));
        public Task<FinanceOperationResult<FinanceOpenItemRecord>> CreateSalesInvoiceAsync(FinanceRequestContext context, FinanceSalesInvoiceCommand command, CancellationToken cancellationToken = default)
        {
            if (SalesInvoiceResult is null) return fallback.CreateSalesInvoiceAsync(context, command, cancellationToken);
            LastSalesInvoiceCommand = command;
            SalesInvoicePostAttempts++;
            SalesInvoiceCommitted = true;
            var effect = SalesInvoiceResult with
            {
                TenantId = ReturnedSalesInvoiceTenantId ?? context.TenantId.Value,
                CompanyId = command.CompanyId,
                CustomerId = command.CustomerId,
                SourceDocumentId = command.SalesOrderId,
                SourceDocumentVersion = ReturnedSalesInvoiceRevision ?? command.SalesOrderRevision,
                SourceEvidenceId = ReturnWrongSalesInvoiceEvidence ? Guid.NewGuid() : command.InvoiceRequestId,
                SourceEvidenceVersion = 1,
                DocumentDate = command.DocumentDate,
                CurrencyCode = command.CurrencyCode,
                OriginalAmount = command.Amount
            };
            salesInvoiceEffects.Add(effect.Id);
            return Task.FromResult(FinanceOperationResult<FinanceOpenItemRecord>.Success(effect));
        }
        public Task<IReadOnlyList<FinanceSettlementDocumentRecord>> ListSettlementDocumentsAsync(FinanceRequestContext context, FinanceSettlementQuery query, CancellationToken cancellationToken = default) => fallback.ListSettlementDocumentsAsync(context, query, cancellationToken);
        public Task<FinanceSettlementDocumentRecord?> GetSettlementDocumentAsync(FinanceRequestContext context, Guid documentId, FinancePaymentMethodDirection? expectedDirection = null, CancellationToken cancellationToken = default) => fallback.GetSettlementDocumentAsync(context, documentId, expectedDirection, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> CreateSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, CancellationToken cancellationToken = default) => fallback.CreateSettlementDocumentAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> EditSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementDocumentCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default) => fallback.EditSettlementDocumentAsync(context, command, expectedVersion, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> TransitionSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, FinanceSettlementDocumentStatus target, CancellationToken cancellationToken = default) => fallback.TransitionSettlementDocumentAsync(context, command, target, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> PostSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementActionCommand command, CancellationToken cancellationToken = default) => fallback.PostSettlementDocumentAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceSettlementDocumentRecord>> ReverseSettlementDocumentAsync(FinanceRequestContext context, FinanceSettlementReversalCommand command, CancellationToken cancellationToken = default) => fallback.ReverseSettlementDocumentAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceAllocationRecord>> ListAllocationsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.ListAllocationsAsync(context, companyId, cancellationToken);
        public Task<FinanceOperationResult<FinanceAllocationRecord>> CreateAllocationAsync(FinanceRequestContext context, FinanceAllocationCommand command, CancellationToken cancellationToken = default) => fallback.CreateAllocationAsync(context, command, cancellationToken);
        public Task<FinanceOperationResult<FinanceAllocationRecord>> ReverseAllocationAsync(FinanceRequestContext context, FinanceAllocationReversalCommand command, CancellationToken cancellationToken = default) => fallback.ReverseAllocationAsync(context, command, cancellationToken);
        public Task<IReadOnlyList<FinanceAgingRecord>> GetAgingAsync(FinanceRequestContext context, FinanceAgingQuery query, CancellationToken cancellationToken = default) => fallback.GetAgingAsync(context, query, cancellationToken);
        public Task<FinanceCustomerExposureRecord?> GetExposureAsync(FinanceRequestContext context, FinanceExposureQuery query, CancellationToken cancellationToken = default) => Task.FromResult(Exposure);
        public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => fallback.GetReconciliationAsync(context, companyId, cancellationToken);
        public Task<IReadOnlyList<FinanceReconciliationRecord>> GetReconciliationAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default) => fallback.GetReconciliationAsync(context, companyId, asOfDate, cancellationToken);
    }

    private sealed class CapturingSalesPersistence : ISalesPersistence
    {
        private readonly SalesQuotationResponse? quotation;
        private readonly SalesOrderResponse? order;

        public CapturingSalesPersistence(SalesQuotationResponse? quotation = null) => this.quotation = quotation;
        public CapturingSalesPersistence(SalesOrderResponse order) => this.order = order;
        public SalesQuotationWriteModel? Captured { get; private set; }
        public SalesDeliveryResponse? Delivery { get; set; }
        public SalesInvoiceRequestResponse? Invoice { get; private set; }
        public bool DeliveryFailedUnknown { get; private set; }
        public bool InvoiceFailedUnknown { get; private set; }
        public int DeliveryFinalizationFailuresRemaining { get; set; } = 1;
        public int InvoiceFinalizationFailuresRemaining { get; set; } = 1;

        public Task<IReadOnlyList<SalesQuotationSummaryResponse>> ListQuotationsAsync(ProcurementRequestContext context, Guid? companyId, SalesQuotationStatus? status, CancellationToken cancellationToken = default) => EmptyList<SalesQuotationSummaryResponse>();
        public Task<SalesQuotationResponse?> GetQuotationAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => Task.FromResult(quotation);
        public Task<SalesOperationResult<SalesQuotationResponse>> CreateQuotationAsync(ProcurementRequestContext context, SalesQuotationWriteModel model, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default)
        {
            Captured = model;
            return Task.FromResult(SalesOperationResult<SalesQuotationResponse>.Success(null!));
        }
        public Task<SalesOperationResult<SalesQuotationResponse>> EditQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default, SalesApprovalPolicyDefinition? policy = null) => Failure<SalesQuotationResponse>();
        public Task<SalesOperationResult<SalesOrderResponse>> EditOrderAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default) => Failure<SalesOrderResponse>();
        public Task<SalesOperationResult<SalesQuotationResponse>> TransitionQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default) => Failure<SalesQuotationResponse>();
        public Task<IReadOnlyList<SalesQuotationRevisionResponse>> ListQuotationRevisionsAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => EmptyList<SalesQuotationRevisionResponse>();
        public Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default) => EmptyList<SalesHistoryResponse>();
        public Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default) => EmptyList<SalesAuditResponse>();
        public Task<SalesApprovalPolicyDefinition?> GetApprovalPolicyAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default) => Empty<SalesApprovalPolicyDefinition?>();
        public Task<IReadOnlyList<SalesOrderSummaryResponse>> ListOrdersAsync(ProcurementRequestContext context, Guid? companyId, SalesOrderStatus? status, CancellationToken cancellationToken = default) => EmptyList<SalesOrderSummaryResponse>();
        public Task<SalesOrderResponse?> GetOrderAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => Task.FromResult(order?.Id == id ? order : null);
        public Task<SalesOperationResult<SalesOrderResponse>> ConvertQuotationAsync(ProcurementRequestContext context, Guid quotationId, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default) => Failure<SalesOrderResponse>();
        public Task<SalesOperationResult<SalesOrderResponse>> TransitionOrderAsync(ProcurementRequestContext context, Guid id, SalesOrderStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation? credit, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default) => Failure<SalesOrderResponse>();
        public Task<SalesOperationResult<SalesOrderResponse>> OverrideOrderCreditAsync(ProcurementRequestContext context, Guid id, string reason, DateTimeOffset expiresAt, string? scope, string? sourceReference, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation credit, CancellationToken cancellationToken = default) => Failure<SalesOrderResponse>();
        public Task<SalesCreditResponse?> GetOrderCreditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => Empty<SalesCreditResponse?>();
        public Task<IReadOnlyList<SalesDeliveryResponse>> ListDeliveriesAsync(ProcurementRequestContext context, Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SalesDeliveryResponse>>(Delivery is null ? [] : [Delivery]);
        public Task<SalesDeliveryResponse?> GetDeliveryAsync(ProcurementRequestContext context, Guid deliveryId, CancellationToken cancellationToken = default) => Task.FromResult(Delivery?.Id == deliveryId ? Delivery : null);
        public Task<SalesOperationResult<SalesDeliveryResponse>> CreateDeliveryAsync(ProcurementRequestContext context, SalesDeliveryWriteModel model, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default)
        {
            if (Delivery?.Id == model.Id) return Task.FromResult(SalesOperationResult<SalesDeliveryResponse>.Success(Delivery));
            Delivery = new SalesDeliveryResponse(model.Id, context.TenantId.Value, model.OrderId, model.OrderRevisionNumber, model.CompanyId, model.BranchId, model.CustomerId, model.WarehouseId, SalesDeliveryStatus.Draft, null, model.Lines, [], DateTimeOffset.UtcNow, null, [1]);
            return Task.FromResult(SalesOperationResult<SalesDeliveryResponse>.Success(Delivery));
        }
        public Task<SalesOperationResult<SalesDeliveryResponse>> CompleteDeliveryAsync(ProcurementRequestContext context, Guid deliveryId, IReadOnlyList<Guid> movementIds, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default, string? downstreamIdempotencyKey = null, string? downstreamRequestFingerprint = null)
        {
            if (DeliveryFinalizationFailuresRemaining-- > 0) return Failure<SalesDeliveryResponse>();
            if (Delivery?.Id != deliveryId) return Task.FromResult(SalesOperationResult<SalesDeliveryResponse>.Failure("delivery_not_found"));
            Delivery = Delivery with { Status = SalesDeliveryStatus.Posted, MovementIds = movementIds, PostedAt = DateTimeOffset.UtcNow, Handoff = new SalesHandoffEvidence("inventory.sales-delivery.post", movementIds, "Committed", "Acknowledged", "Reconciled", null, 1, DateTimeOffset.UtcNow, Delivery.Handoff?.RequestFingerprint ?? string.Empty, downstreamIdempotencyKey, downstreamRequestFingerprint) };
            return Task.FromResult(SalesOperationResult<SalesDeliveryResponse>.Success(Delivery));
        }
        public Task<SalesOperationResult<SalesDeliveryResponse>> FailDeliveryAsync(ProcurementRequestContext context, Guid deliveryId, string code, bool unknown, CancellationToken cancellationToken = default, SalesDownstreamEvidence? downstream = null)
        {
            if (Delivery?.Id != deliveryId) return Task.FromResult(SalesOperationResult<SalesDeliveryResponse>.Failure("delivery_not_found"));
            DeliveryFailedUnknown = unknown;
            Delivery = Delivery with { Status = unknown ? SalesDeliveryStatus.Unknown : SalesDeliveryStatus.Failed, ErrorCode = code, Handoff = downstream is null ? Delivery.Handoff : new SalesHandoffEvidence("inventory.sales-delivery.post", downstream.EffectIds, downstream.CommitState, downstream.AcknowledgementState, downstream.ReconciliationStatus, code, 1, DateTimeOffset.UtcNow, Delivery.Handoff?.RequestFingerprint ?? string.Empty, downstream.IdempotencyKey, downstream.RequestFingerprint) };
            return Task.FromResult(SalesOperationResult<SalesDeliveryResponse>.Success(Delivery));
        }
        public Task<IReadOnlyList<SalesInvoiceRequestResponse>> ListInvoiceRequestsAsync(ProcurementRequestContext context, Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SalesInvoiceRequestResponse>>(Invoice is null ? [] : [Invoice]);
        public Task<SalesInvoiceRequestResponse?> GetInvoiceRequestAsync(ProcurementRequestContext context, Guid requestId, CancellationToken cancellationToken = default) => Task.FromResult(Invoice?.Id == requestId ? Invoice : null);
        public Task<SalesOperationResult<SalesInvoiceRequestResponse>> CreateInvoiceRequestAsync(ProcurementRequestContext context, SalesInvoiceRequestWriteModel model, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default)
        {
            if (Invoice?.Id == model.Id) return Task.FromResult(SalesOperationResult<SalesInvoiceRequestResponse>.Success(Invoice));
            Invoice = new SalesInvoiceRequestResponse(model.Id, context.TenantId.Value, model.OrderId, model.OrderRevisionNumber, model.DeliveryId, SalesInvoiceRequestStatus.Pending, null, null, model.Amount, model.Lines, DateTimeOffset.UtcNow, null, [1], model.NetAmount, model.TaxAmount, model.PaymentTerm, model.LineEvidence, null, model.CurrencyCode, model.InvoiceDate, model.SourceSnapshot);
            return Task.FromResult(SalesOperationResult<SalesInvoiceRequestResponse>.Success(Invoice));
        }
        public Task<SalesOperationResult<SalesInvoiceRequestResponse>> CompleteInvoiceRequestAsync(ProcurementRequestContext context, Guid requestId, Guid financeOpenItemId, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default, string? downstreamIdempotencyKey = null, string? downstreamRequestFingerprint = null)
        {
            if (InvoiceFinalizationFailuresRemaining-- > 0) return Failure<SalesInvoiceRequestResponse>();
            if (Invoice?.Id != requestId) return Task.FromResult(SalesOperationResult<SalesInvoiceRequestResponse>.Failure("invoice_not_found"));
            Invoice = Invoice with { Status = SalesInvoiceRequestStatus.Posted, FinanceOpenItemId = financeOpenItemId, PostedAt = DateTimeOffset.UtcNow, Handoff = new SalesHandoffEvidence("finance.sales-invoice.create", [financeOpenItemId], "Committed", "Acknowledged", "Reconciled", null, 1, DateTimeOffset.UtcNow, Invoice.Handoff?.RequestFingerprint ?? string.Empty, downstreamIdempotencyKey, downstreamRequestFingerprint) };
            return Task.FromResult(SalesOperationResult<SalesInvoiceRequestResponse>.Success(Invoice));
        }
        public Task<SalesOperationResult<SalesInvoiceRequestResponse>> FailInvoiceRequestAsync(ProcurementRequestContext context, Guid requestId, string code, bool unknown, CancellationToken cancellationToken = default, SalesDownstreamEvidence? downstream = null)
        {
            if (Invoice?.Id != requestId) return Task.FromResult(SalesOperationResult<SalesInvoiceRequestResponse>.Failure("invoice_not_found"));
            InvoiceFailedUnknown = unknown;
            Invoice = Invoice with { Status = unknown ? SalesInvoiceRequestStatus.Unknown : SalesInvoiceRequestStatus.Failed, ErrorCode = code, FinanceOpenItemId = downstream is null ? Invoice.FinanceOpenItemId : downstream.EffectIds.FirstOrDefault(), Handoff = downstream is null ? Invoice.Handoff : new SalesHandoffEvidence("finance.sales-invoice.create", downstream.EffectIds, downstream.CommitState, downstream.AcknowledgementState, downstream.ReconciliationStatus, code, 1, DateTimeOffset.UtcNow, Invoice.Handoff?.RequestFingerprint ?? string.Empty, downstream.IdempotencyKey, downstream.RequestFingerprint) };
            return Task.FromResult(SalesOperationResult<SalesInvoiceRequestResponse>.Success(Invoice));
        }

        private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
        private static Task<IReadOnlyList<T>> EmptyList<T>() => Task.FromResult<IReadOnlyList<T>>([]);
        private static Task<SalesOperationResult<T>> Failure<T>() => Task.FromResult(SalesOperationResult<T>.Failure("not-called"));
    }

    private sealed class HandoffInventoryPort(Guid orderId, Guid lineId) : ISalesInventoryPort
    {
        private static readonly Guid HandoffWarehouseId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        public Guid WarehouseId => HandoffWarehouseId;
        public Guid ReservationId { get; } = Guid.NewGuid();
        public List<Guid> MovementIds { get; } = [Guid.NewGuid()];
        public bool DeliveryCommitted { get; private set; }
        public int PostAttempts { get; private set; }
        public Guid? ReturnedTenantId { get; set; }
        public int? ReturnedOrderRevision { get; set; }
        public IReadOnlyList<Guid>? ReturnedMovementIds { get; set; }

        public Task<InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>> ListSalesReservationsAsync(InventoryRequestContext context, string sourceReference, string operationId = "sales.fulfillment.read", CancellationToken cancellationToken = default) =>
            Task.FromResult(InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Success([
                new InventoryReservationRecord(ReservationId, TenantA, CompanyA, BranchA, HandoffWarehouseId, "WH-1", "Warehouse", ProductA, "SKU-001", "Product A", UomA, "EA", null, "sales-order", sourceReference, 1m, 1m, 0m, InventoryReservationStatus.Active, Creator, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [1], 0m, orderId, lineId, 1)
            ]));

        public Task<InventoryOperationResult<InventoryReservationRecord>> AllocateSalesReservationAsync(InventoryRequestContext context, InventoryReservationRecord reservation, decimal quantity, string? idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(InventoryOperationResult<InventoryReservationRecord>.Failure("not-called"));

        public Task<InventoryOperationResult<InventoryReservationRecord>> CreateSalesReservationAsync(InventoryRequestContext context, InventoryReservationCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(InventoryOperationResult<InventoryReservationRecord>.Failure("not-called"));

        public Task<InventoryOperationResult<InventorySalesDeliveryPostingRecord>> PostSalesDeliveryAsync(InventoryRequestContext context, InventorySalesDeliveryPostCommand command, CancellationToken cancellationToken = default)
        {
            PostAttempts++;
            DeliveryCommitted = true;
            return Task.FromResult(InventoryOperationResult<InventorySalesDeliveryPostingRecord>.Success(new InventorySalesDeliveryPostingRecord(command.DeliveryId, command.SalesOrderId, ReturnedMovementIds ?? MovementIds, command.Lines.Sum(item => item.Quantity), DateTimeOffset.UtcNow, ReturnedTenantId ?? context.TenantId.Value, ReturnedOrderRevision ?? command.SalesOrderRevision)));
        }
    }

    private sealed class CustomerReferenceFake : ICustomerPersistence
    {
        private static readonly CustomerRecord Record = new(CustomerA, new TenantId(TenantA), "CUST-001", new LocalizedName("Customer A"), null, MasterDataLifecycleState.Active, [1], []);

        public Task<IReadOnlyList<CustomerRecord>> ListCustomersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerRecord>>([]);
        public Task<CustomerRecord?> FindCustomerAsync(TenantContext tenantContext, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<CustomerRecord?>(customerId == CustomerA ? Record : null);
        public Task<MasterDataPersistenceResult<CustomerRecord>> CreateCustomerAsync(TenantContext tenantContext, Guid customerId, CreateCustomerCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<CustomerRecord>();
        public Task<MasterDataPersistenceResult<CustomerRecord>> EditCustomerAsync(TenantContext tenantContext, EditCustomerCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<CustomerRecord>();
        public Task<MasterDataPersistenceResult<CustomerRecord>> SetCustomerLifecycleAsync(TenantContext tenantContext, Guid customerId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<CustomerRecord>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataAuditRecord>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);

        private static Task<MasterDataPersistenceResult<T>> Failure<T>() => Task.FromResult(MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Failure, "not-called"));
    }

    private sealed class ProductReferenceFake : IProductIdentityPersistence
    {
        private static readonly ProductIdentityRecord Record = new(ProductA, new TenantId(TenantA), "SKU-001", new LocalizedName("Product A"), null, Guid.NewGuid(), UomA, false, null, false, true, false, false, MasterDataLifecycleState.Active, [1], []);

        public Task<IReadOnlyList<ProductIdentityRecord>> ListProductsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductIdentityRecord>>([]);
        public Task<ProductIdentityRecord?> FindProductAsync(TenantContext tenantContext, Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<ProductIdentityRecord?>(productId == ProductA ? Record : null);
        public Task<ProductReferenceValidation> ValidateReferencesAsync(TenantContext tenantContext, Guid categoryId, Guid baseUnitOfMeasureId, CancellationToken cancellationToken = default) => Task.FromResult(ProductReferenceValidation.Invalid());
        public Task<MasterDataPersistenceResult<ProductIdentityRecord>> CreateProductAsync(TenantContext tenantContext, Guid productId, CreateProductIdentityCommand command, ProductReferenceValidation references, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<ProductIdentityRecord>();
        public Task<MasterDataPersistenceResult<ProductIdentityRecord>> EditProductAsync(TenantContext tenantContext, EditProductIdentityCommand command, ProductReferenceValidation references, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<ProductIdentityRecord>();
        public Task<MasterDataPersistenceResult<ProductIdentityRecord>> SetProductLifecycleAsync(TenantContext tenantContext, Guid productId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<ProductIdentityRecord>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataAuditRecord>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);

        private static Task<MasterDataPersistenceResult<T>> Failure<T>() => Task.FromResult(MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Failure, "not-called"));
    }

    private sealed class TaxReferenceFake : ISalesTaxReferenceProvider
    {
        public Task<SalesTaxResolution> ResolveAsync(ProcurementRequestContext context, Guid taxId, DateOnly effectiveOn, decimal taxableBase, string currencyCode, string sourceLineage, CancellationToken cancellationToken = default) =>
            Task.FromResult(taxId == TaxA
                ? SalesTaxResolution.Success(new SalesTaxEvidence(TaxA, "VAT-15", Guid.Parse("cccccccc-1111-1111-1111-111111111111"), 2, effectiveOn, effectiveOn.AddDays(-10), null, 15m, taxableBase, decimal.Round(taxableBase * .15m, 2), currencyCode, "VAT-15;v2"))
                : SalesTaxResolution.Failure("tax_not_found"));
    }

    private sealed class ExchangeRateReferenceFake : ISalesExchangeRateReferenceProvider
    {
        public Task<SalesExchangeRateResolution> ResolveAsync(TenantContext tenantContext, Guid exchangeRateId, string sourceCurrencyCode, string targetCurrencyCode, DateOnly effectiveOn, CancellationToken cancellationToken = default) =>
            Task.FromResult(exchangeRateId == ExchangeRateA
                ? SalesExchangeRateResolution.Success(new SalesExchangeRateEvidence(ExchangeRateA, Guid.Parse("dddddddd-1111-1111-1111-111111111111"), 3, sourceCurrencyCode, targetCurrencyCode, 3.75m, 2, "Configured", "USD/SAR", effectiveOn, effectiveOn.AddDays(-30), null, $"{sourceCurrencyCode}->{targetCurrencyCode};v3"))
                : SalesExchangeRateResolution.Failure("exchange_rate_not_found"));
    }

    private sealed class PaymentTermReferenceFake : IMasterDataCurrencyPaymentTermPersistence
    {
        private static readonly MasterDataPaymentTermRecord Record = new(
            PaymentTermA,
            new TenantId(TenantA),
            "NET4",
            new LocalizedName("Net 4"),
            MasterDataLifecycleState.Active,
            2,
            [
                new MasterDataPaymentTermVersionRecord(
                    PaymentTermVersionA,
                    2,
                    new DateOnly(2026, 1, 1),
                    null,
                    PaymentTermBaseDateRule.DocumentDate,
                    PaymentTermScheduleMode.SingleDueDate,
                    new MasterDataPaymentTermOffset(4, 0),
                    [],
                    MasterDataEarlySettlementDiscount.Disabled(),
                    "NET4",
                    new LocalizedName("Net 4"))
            ],
            [1]);

        public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataCurrencyRecord>>([]);
        public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataCurrencyRecord?>(null);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataCurrencyRecord>();
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataCurrencyRecord>();
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataCurrencyRecord>();
        public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataPaymentTermRecord>>([Record]);
        public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataPaymentTermRecord?>(paymentTermId == PaymentTermA ? Record : null);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPaymentTermRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPaymentTermRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPaymentTermRecord>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataAuditRecord>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);

        private static Task<MasterDataPersistenceResult<T>> Failure<T>() => Task.FromResult(MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Failure, "not-called"));
    }

    private sealed class PriceReferenceFake : IMasterDataPriceListPersistence
    {
        private readonly string currencyCode;

        public PriceReferenceFake(string currencyCode = "SAR") => this.currencyCode = currencyCode;

        public Task<IReadOnlyList<MasterDataPriceListRecord>> ListPriceListsAsync(TenantContext tenantContext, string? search, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataPriceListRecord>>([]);
        public Task<MasterDataPriceListRecord?> FindPriceListAsync(TenantContext tenantContext, Guid priceListId, CancellationToken cancellationToken = default) => Empty<MasterDataPriceListRecord?>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> CreatePriceListAsync(TenantContext tenantContext, Guid priceListId, CreateMasterDataPriceListCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> EditPriceListAsync(TenantContext tenantContext, EditMasterDataPriceListCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> AppendPriceAsync(TenantContext tenantContext, AppendMasterDataPriceCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> SetPriceListLifecycleAsync(TenantContext tenantContext, Guid priceListId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>> ResolvePriceAsync(TenantContext tenantContext, ResolveMasterDataPriceQuery query, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Task.FromResult(MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Success(CreateRecord(query.EffectiveOn)));
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataAuditRecord>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? priceListId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);

        private MasterDataPriceListReferenceRecord CreateRecord(DateOnly effectiveOn)
        {
            var effective = effectiveOn;
            var price = new MasterDataPriceListPriceRecord(Guid.NewGuid(), 4, ProductA, "SKU-001", UomA, "EA", CurrencyA, currencyCode, CustomerA, OrganizationScopeKind.Branch, BranchA, 1, effective, null, 50m, 2, PriceListProvenance.Configured, "price-row-4", [1]);
            var configuration = new MasterDataPriceListCurrentConfiguration(CurrencyA, currencyCode, CustomerA, OrganizationScopeKind.Branch, BranchA, 1, MasterDataLifecycleState.Active);
            var snapshot = new ReferenceSnapshot(MasterDataResourceKind.PriceList, PriceListA, new TenantOwnership(TenantA), 4, "price-row-4", effective);
            return new MasterDataPriceListReferenceRecord(PriceListA, new TenantId(TenantA), "STANDARD", price, configuration, effectiveOn, snapshot, [1]);
        }

        private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
        private static Task<MasterDataPersistenceResult<T>> Failure<T>() => Task.FromResult(MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Failure, "not-called"));
    }
}
