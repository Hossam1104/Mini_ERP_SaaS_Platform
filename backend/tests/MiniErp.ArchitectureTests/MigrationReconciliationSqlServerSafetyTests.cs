using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Migration;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationReconciliationSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    [Fact]
    public async Task Sql_server_s11_r01_clean_all_domain_reconciliation_is_durable_and_read_only()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("S11-R01", 100m, includeCash: true, includeGl: true);
        var executed = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s11-r01-execute", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);
        Assert.Equal(MigrationAttemptOutcome.Succeeded, executed.Value!.AttemptOutcome);

        var service = Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy());
        var currentRun = await fixture.Migration.FindRunAsync(fixture.Tenant, prepared.Run.RunId);
        Assert.NotNull(currentRun);
        var reconciliation = await service.ReconcileAsync(fixture.Request,
            new MigrationReconcileRequest(prepared.Run.RunId, currentRun!.Version, "s11-r01-reconcile"));

        Assert.True(reconciliation.Succeeded, $"{reconciliation.Kind}:{reconciliation.Code}");
        Assert.True(reconciliation.Value!.Status == MigrationReconciliationStatus.Reconciled,
            $"{reconciliation.Value.Status}; {string.Join(" | ", reconciliation.Value.Details.Select(item => $"{item.Domain}:{item.ScopeKey}:{item.FindingCode}:{item.Variance}:{item.AmountVariance}"))}");
        Assert.Equal(prepared.Staged.Count, reconciliation.Value.SubmittedCount);
        Assert.Equal(prepared.Staged.Count, reconciliation.Value.AcceptedCount);
        Assert.True(reconciliation.Value.Variance == 0m, string.Join(" | ", reconciliation.Value.Details
            .Where(item => item.Domain == MigrationReconciliationDomain.Gl)
            .Select(item => $"{item.ScopeKey}:target={item.TargetAmount}:established={item.SubsidiaryEstablishedAmount}:posted={item.GlControlAmount}:source={item.SourceAmount}:variance={item.Variance}")));
        Assert.Equal(0, reconciliation.Value.RejectedCount + reconciliation.Value.DuplicateCount
            + reconciliation.Value.SkippedCount + reconciliation.Value.QuarantinedCount + reconciliation.Value.UnresolvedCount);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Inventory);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Ar);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Ap);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.CashBank);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Gl);
        Assert.DoesNotContain(reconciliation.Value.Details, item => item.IsBlocking);

        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        var persistedId = await db.Reconciliations.Where(item => item.RunId == prepared.Run.RunId).Select(item => item.Id).SingleAsync();
        var firstRead = await service.ReadAsync(fixture.Request, prepared.Run.RunId);
        var countBefore = await db.Reconciliations.CountAsync(item => item.RunId == prepared.Run.RunId);
        var secondRead = await service.ReadAsync(fixture.Request, prepared.Run.RunId);
        var countAfter = await db.Reconciliations.CountAsync(item => item.RunId == prepared.Run.RunId);
        Assert.Equal(persistedId, firstRead!.Id);
        Assert.Equal(firstRead.EvidenceFingerprint, secondRead!.EvidenceFingerprint);
        Assert.Equal(firstRead.Details, secondRead.Details);
        Assert.Equal(firstRead.Status, secondRead.Status);
        Assert.True(firstRead.IsCurrent);
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task Sql_server_s11_r02_unbalanced_gl_is_blocked_without_a_balancing_journal()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("S11-R02", 100m, includeCash: true, includeGl: true, invalidGl: true);

        var result = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s11-r02-execute", prepared.Run.Version);

        Assert.Equal(MigrationResultKind.KnownFailure, result.Kind);
        Assert.Equal("migration_gl_opening_imbalanced", result.Code);
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: false));
        await fixture.AssertNoAllFiveEffectsAsync();
    }

    [Fact]
    public async Task Sql_server_s11_r03_inventory_quantity_and_value_mismatch_is_blocked_without_stock_mutation()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareInventoryOnlyAsync("S11-R03", "SAR");
        var executed = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s11-r03-execute", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);
        var before = await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, executed.Value!.AttemptId);
        await using (var db = new InventoryDbContext(fixture.InventoryOptions, fixture.Tenant))
        {
            await db.StockMovements.Where(item => item.CompanyId == fixture.CompanyId && item.ProductId == fixture.ProductId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Quantity, item => item.Quantity + 1m));
            await db.MovementValuationEvents.Where(item => item.CompanyId == fixture.CompanyId && item.ProductId == fixture.ProductId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.MovementValue, item => item.MovementValue + 1m));
        }

        var service = Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy());
        var reconciliation = await ReconcileAsync(fixture, service, prepared.Run.RunId, "s11-r03-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var inventory = Assert.Single(reconciliation.Value!.Details, item => item.Domain == MigrationReconciliationDomain.Inventory);
        Assert.True(inventory.IsBlocking);
        Assert.NotEqual(0m, inventory.QuantityVariance);
        Assert.NotEqual(0m, inventory.AmountVariance);
        Assert.Equal(before, await fixture.ReadEconomicCountsAsync(prepared.Run.RunId, executed.Value.AttemptId));
    }

    [Fact]
    public async Task Sql_server_s11_r04_ar_to_gl_mismatch_preserves_exact_mapping_and_adds_no_journal()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareAsync("S11-R04", 100m);
        var executed = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s11-r04-execute", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);
        var journalsBefore = await fixture.CountFinanceRowsAsync("migration-ar-opening.v1", journals: true);
        await MutateFinanceJournalAsync(fixture, "migration-ar-opening.v1", 101m);

        var reconciliation = await ReconcileAsync(fixture, Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()), prepared.Run.RunId, "s11-r04-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Blocked, reconciliation.Value!.Status);
        var ar = Assert.Single(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Ar);
        Assert.True(ar.IsBlocking);
        Assert.NotEqual(Guid.Empty, ar.EffectId);
        Assert.NotNull(ar.ControlAccountId);
        Assert.NotNull(ar.PostingRuleId);
        Assert.NotNull(ar.PostingRuleVersionNumber);
        Assert.NotNull(ar.EffectId);
        var arMapping = await ReadHistoricalFinanceMappingAsync(fixture, ar.EffectId.Value);
        Assert.Equal(arMapping.ControlAccountId, ar.ControlAccountId);
        Assert.Equal(arMapping.PostingRuleId, ar.PostingRuleId);
        Assert.Equal(arMapping.PostingRuleVersionNumber, ar.PostingRuleVersionNumber);
        Assert.Equal(journalsBefore, await fixture.CountFinanceRowsAsync("migration-ar-opening.v1", journals: true));
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
    }

    [Fact]
    public async Task Sql_server_s11_r05_ap_to_gl_mismatch_preserves_exact_mapping_and_adds_no_journal()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareApAsync("S11-R05", 100m);
        var executed = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s11-r05-execute", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);
        var journalsBefore = await fixture.CountFinanceRowsAsync("migration-ap-opening.v1", journals: true);
        await MutateFinanceJournalAsync(fixture, "migration-ap-opening.v1", 101m);

        var reconciliation = await ReconcileAsync(fixture, Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()), prepared.Run.RunId, "s11-r05-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Blocked, reconciliation.Value!.Status);
        var ap = Assert.Single(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Ap);
        Assert.True(ap.IsBlocking);
        Assert.NotEqual(Guid.Empty, ap.EffectId);
        Assert.NotNull(ap.ControlAccountId);
        Assert.NotNull(ap.PostingRuleId);
        Assert.NotNull(ap.PostingRuleVersionNumber);
        Assert.NotNull(ap.EffectId);
        var apMapping = await ReadHistoricalFinanceMappingAsync(fixture, ap.EffectId.Value);
        Assert.Equal(apMapping.ControlAccountId, ap.ControlAccountId);
        Assert.Equal(apMapping.PostingRuleId, ap.PostingRuleId);
        Assert.Equal(apMapping.PostingRuleVersionNumber, ap.PostingRuleVersionNumber);
        Assert.Equal(journalsBefore, await fixture.CountFinanceRowsAsync("migration-ap-opening.v1", journals: true));
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
    }

    [Fact]
    public async Task Sql_server_s11_r06_cash_bank_to_gl_mismatch_preserves_linked_account_and_adds_no_journal()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareMixedAsync("S11-R06", 100m, includeCash: true);
        var executed = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s11-r06-execute", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);
        var journalsBefore = await fixture.CountFinanceRowsAsync("migration-cash-bank-opening.v1", journals: true);
        await MutateFinanceJournalAsync(fixture, "migration-cash-bank-opening.v1", 101m);

        var reconciliation = await ReconcileAsync(fixture, Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()), prepared.Run.RunId, "s11-r06-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Blocked, reconciliation.Value!.Status);
        var cash = Assert.Single(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.CashBank);
        Assert.True(cash.IsBlocking);
        Assert.NotNull(cash.ControlAccountId);
        Assert.NotNull(cash.LinkedAccountId);
        Assert.Equal(fixture.CashLinkedAccountId, cash.LinkedAccountId);
        Assert.Equal(journalsBefore, await fixture.CountFinanceRowsAsync("migration-cash-bank-opening.v1", journals: true));
        Assert.Equal(0, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
    }

    [Fact]
    public async Task Sql_server_s11_r07_all_domain_execution_creates_one_economic_effect_per_source()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R07");

        await fixture.AssertMixedEffectsAsync("S11-R07", 100m);
        await fixture.AssertOneCashEffectAsync("S11-R07-CASH", 100m);
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-cash-bank-opening.v1", journals: true));
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-cash-bank-opening.v1", journals: false));
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
        Assert.Equal(1, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: false));
        var counts = await fixture.ReadEconomicCountsAsync(scenario.RunId, scenario.Execution.AttemptId);
        Assert.Equal(5, counts.Journals);
        Assert.Equal(5, counts.SourceEffects);
        Assert.Equal(2, counts.OpenItems);
        Assert.Equal(1, counts.StockMovements);
        Assert.Equal(1, counts.ValuationEvents);
        Assert.Equal(1, counts.Handoffs);

        var reconciliation = await ReconcileAsync(fixture,
            Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()), scenario.RunId, "s11-r07-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Reconciled, reconciliation.Value!.Status);
        var subsidiaryDetails = reconciliation.Value.Details.Where(item => item.Domain is
            MigrationReconciliationDomain.Ar or MigrationReconciliationDomain.Ap
                or MigrationReconciliationDomain.CashBank or MigrationReconciliationDomain.Inventory).ToArray();
        Assert.NotEmpty(subsidiaryDetails);
        Assert.Contains(subsidiaryDetails, item => item.Domain == MigrationReconciliationDomain.Ar);
        Assert.Contains(subsidiaryDetails, item => item.Domain == MigrationReconciliationDomain.Ap);
        Assert.Contains(subsidiaryDetails, item => item.Domain == MigrationReconciliationDomain.CashBank);
        Assert.Contains(subsidiaryDetails, item => item.Domain == MigrationReconciliationDomain.Inventory);
        Assert.All(subsidiaryDetails, item =>
        {
            Assert.False(item.IsBlocking);
            Assert.Equal(0m, item.Variance);
        });
        var glControlDetails = reconciliation.Value.Details.Where(item => item.Domain == MigrationReconciliationDomain.Gl
            && item.ScopeKey.StartsWith("control:", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(glControlDetails);
        Assert.All(glControlDetails, item =>
        {
            Assert.NotNull(item.ControlAccountId);
            Assert.False(item.IsBlocking);
            Assert.Equal(0m, item.Variance);
        });
        var glControlLines = reconciliation.Value.Details.Where(item => item.Domain == MigrationReconciliationDomain.Gl
            && item.ScopeKey.StartsWith("line:", StringComparison.Ordinal) && item.ControlAccountId.HasValue).ToArray();
        Assert.NotEmpty(glControlLines);
        Assert.All(glControlLines, item =>
        {
            Assert.False(item.IsBlocking);
            Assert.Equal(0m, item.Variance);
        });
        Assert.All(subsidiaryDetails, subsidiary =>
        {
            Assert.NotNull(subsidiary.ControlAccountId);
            Assert.Contains(glControlLines, line => line.ControlAccountId == subsidiary.ControlAccountId
                && line.CompanyId == subsidiary.CompanyId && line.OpeningDate == subsidiary.OpeningDate
                && line.CurrencyCode == (subsidiary.FunctionalCurrencyCode ?? subsidiary.CurrencyCode)
                && line.TargetAmount == subsidiary.GlControlAmount);
        });
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        var representedEffectIds = await db.EconomicRepresentations.Where(item => item.RunId == scenario.RunId)
            .Select(item => item.EffectId).Distinct().ToArrayAsync();
        var executionEffects = await fixture.Migration.ListEffectsAsync(
            fixture.Tenant, scenario.RunId, scenario.Execution.AttemptId);
        Assert.Equal(counts.Effects, representedEffectIds.Length);
        Assert.All(representedEffectIds, effectId => Assert.Contains(executionEffects, item => item.Id == effectId));
    }

    [Fact]
    public async Task Sql_server_s11_r08_row_outcome_counts_are_disjoint_and_sum_to_staged_rows()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R08");
        var attempts = await fixture.Migration.ListAttemptsAsync(fixture.Tenant, scenario.RunId);
        var validationAttempt = Assert.Single(attempts, item => item.Operation == MigrationOperationKind.Validation);
        var previewAttempt = Assert.Single(attempts, item => item.Operation == MigrationOperationKind.DryRun);
        var rows = scenario.Staged.Take(5).ToArray();
        var duplicateCodes = System.Text.Json.JsonSerializer.Serialize(new[] { "duplicate_source_reference" });
        await using (var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant))
        {
            var preview = await db.DryRunPreviews.SingleAsync(item => item.RunId == scenario.RunId && item.AttemptId == previewAttempt.AttemptId);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationValidationRecords] SET [Disposition] = {(int)MigrationRecordDisposition.Rejected} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [AttemptId] = {validationAttempt.AttemptId} AND [StagedRecordId] = {rows[0].StagedRecordId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationValidationRecords] SET [Disposition] = {(int)MigrationRecordDisposition.Quarantined} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [AttemptId] = {validationAttempt.AttemptId} AND [StagedRecordId] = {rows[3].StagedRecordId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationValidationRecords] SET [FindingCodesJson] = {duplicateCodes} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [AttemptId] = {validationAttempt.AttemptId} AND [StagedRecordId] = {rows[1].StagedRecordId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationDryRunPreviewRows] SET [PlannedAction] = {(int)MigrationPlannedAction.Skip} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [PreviewId] = {preview.PreviewId} AND [StagedRecordId] = {rows[2].StagedRecordId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationExecutionEffects] SET [Disposition] = {(int)MigrationExecutionEffectDisposition.Unknown} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [AttemptId] = {scenario.Execution.AttemptId} AND [StagedRecordId] = {rows[4].StagedRecordId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationValidationResults] SET [AcceptedCount] = {scenario.Staged.Count - 2}, [RejectedCount] = {1}, [QuarantinedCount] = {1} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [AttemptId] = {validationAttempt.AttemptId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationDryRunPreviews] SET [AcceptedCount] = {scenario.Staged.Count - 2}, [RejectedCount] = {1}, [QuarantinedCount] = {1} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [AttemptId] = {previewAttempt.AttemptId}");
        }

        var reconciliation = await ReconcileAsync(fixture, Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()), scenario.RunId, "s11-r08-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(scenario.Staged.Count, reconciliation.Value!.SubmittedCount);
        Assert.Equal(scenario.Staged.Count, reconciliation.Value.AcceptedCount + reconciliation.Value.RejectedCount
            + reconciliation.Value.DuplicateCount + reconciliation.Value.SkippedCount + reconciliation.Value.QuarantinedCount
            + reconciliation.Value.UnresolvedCount);
        Assert.Equal(1, reconciliation.Value.RejectedCount);
        Assert.Equal(1, reconciliation.Value.DuplicateCount);
        Assert.Equal(1, reconciliation.Value.SkippedCount);
        Assert.Equal(1, reconciliation.Value.QuarantinedCount);
        Assert.Equal(1, reconciliation.Value.UnresolvedCount);
        Assert.Equal(MigrationReconciliationStatus.Blocked, reconciliation.Value.Status);
    }

    [Fact]
    public async Task Sql_server_s11_r09_configured_rounding_is_permitted_with_persisted_policy_evidence()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        await fixture.EnableMonetaryPolicyAsync();
        var scenario = await ExecuteMixedAsync(fixture, "S11-R09", 375.04m, arAmount: 100.01m, arCurrencyCode: "USD");

        var reconciliation = await ReconcileAsync(fixture, Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()), scenario.RunId, "s11-r09-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.True(reconciliation.Value!.Status == MigrationReconciliationStatus.Reconciled,
            $"{reconciliation.Value.Status}; {string.Join(" | ", reconciliation.Value.Details.Select(item => $"{item.Domain}:{item.ScopeKey}:control={item.ControlAccountId}:source={item.SourceAmount}:target={item.TargetAmount}:functional={item.FunctionalAmount}:gl={item.GlControlAmount}:{item.FindingCode}:var={item.Variance}:{item.AmountVariance}:{item.Explanation}"))}");
        var ar = Assert.Single(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Ar);
        Assert.False(ar.IsBlocking);
        Assert.Equal(100.01m, ar.TransactionAmount);
        Assert.Equal(375.04m, ar.FunctionalAmount);
        Assert.NotEqual(0m, ar.OwnerRoundingDifference);
        Assert.NotNull(ar.RoundingPolicyId);
        Assert.NotNull(ar.RoundingPolicyVersionNumber);
        Assert.Equal(2, ar.RoundingScale);
        Assert.Equal("AwayFromZero", ar.RoundingMode);
        Assert.Equal(3.75m, ar.AppliedRate);
        Assert.NotNull(ar.ExchangeRateId);
        Assert.NotNull(ar.ExchangeRateVersionId);
        Assert.False(string.IsNullOrWhiteSpace(ar.Explanation));
    }

    [Fact]
    public async Task Sql_server_s11_r10_unexplained_gl_difference_blocks_without_repair()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R10");
        var journalsBefore = await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true);
        await MutateFinanceJournalAsync(fixture, "migration-gl-opening.v1", 51m);

        var reconciliation = await ReconcileAsync(fixture, Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()), scenario.RunId, "s11-r10-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Blocked, reconciliation.Value!.Status);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Gl && item.IsBlocking);
        Assert.Equal(journalsBefore, await fixture.CountFinanceRowsAsync("migration-gl-opening.v1", journals: true));
    }

    [Fact]
    public async Task Sql_server_s11_r11_preparer_is_denied_and_independent_reviewer_can_approve()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R11");
        var reviewer = Guid.NewGuid();
        var policy = ApprovalPolicy(fixture.Request.ActorId!.Value, reviewer);
        var service = Service(fixture, new TestApprovalPolicy(policy));
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r11-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);

        var self = await service.ApproveAsync(fixture.Request, ApprovalRequest(reconciliation.Value!, "s11-r11-self"));
        Assert.Equal("migration_approval_self_approval_forbidden", self.Code);
        var independent = await service.ApproveAsync(RequestForActor(fixture, reviewer), ApprovalRequest(reconciliation.Value!, "s11-r11-reviewer"));

        Assert.True(independent.Succeeded, independent.Code);
        Assert.Equal(reviewer, independent.Value!.ActorId);
        Assert.True(independent.Value.EvidenceConfirmed);
    }

    [Fact]
    public async Task Sql_server_s11_r12_reconciliation_details_are_unique_and_prior_rows_are_unchanged()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R12-DETAIL-ID");
        var policy = ApprovalPolicy(fixture.Request.ActorId!.Value, Guid.NewGuid());
        var first = await ReconcileAsync(fixture, Service(fixture, new TestApprovalPolicy(policy)),
            scenario.RunId, "s11-r12-detail-first");
        Assert.True(first.Succeeded, first.Code);
        var firstRecord = first.Value!;
        MigrationReconciliationDetail[] firstDetailsBefore;
        await using (var firstDb = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant))
        {
            firstDetailsBefore = await ReadReconciliationDetailsAsync(firstDb, firstRecord.Id);
        }
        Assert.NotEmpty(firstDetailsBefore);

        var requirement = policy.Requirements.Single();
        var changedPolicy = policy with
        {
            Requirements = [requirement with { EligibleActorIds = [.. requirement.EligibleActorIds, Guid.NewGuid()] }]
        };
        var second = await ReconcileAsync(fixture, Service(fixture, new TestApprovalPolicy(changedPolicy)),
            scenario.RunId, "s11-r12-detail-second");
        Assert.True(second.Succeeded, second.Code);
        var secondRecord = second.Value!;
        Assert.NotEqual(firstRecord.Id, secondRecord.Id);
        Assert.NotEqual(firstRecord.EvidenceFingerprint, secondRecord.EvidenceFingerprint);

        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        Assert.Equal(2, await db.Reconciliations.CountAsync(item => item.RunId == scenario.RunId));
        var firstDetailsAfter = await ReadReconciliationDetailsAsync(db, firstRecord.Id);
        var secondDetails = await ReadReconciliationDetailsAsync(db, secondRecord.Id);
        Assert.Equal(firstDetailsBefore, firstDetailsAfter);
        Assert.Equal(firstDetailsAfter.Length + secondDetails.Length,
            await db.ReconciliationDetails.CountAsync(item => item.RunId == scenario.RunId));
        Assert.Empty(firstDetailsAfter.Select(item => item.Id).Intersect(secondDetails.Select(item => item.Id)));
    }

    [Fact]
    public async Task Sql_server_s11_r12_changed_owner_evidence_invalidates_prior_approval()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R12");
        var reviewer = Guid.NewGuid();
        var service = Service(fixture, new TestApprovalPolicy(ApprovalPolicy(fixture.Request.ActorId!.Value, reviewer)));
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r12-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var approval = await service.ApproveAsync(RequestForActor(fixture, reviewer), ApprovalRequest(reconciliation.Value!, "s11-r12-approve"));
        Assert.True(approval.Succeeded, approval.Code);
        await MutateFinanceJournalAsync(fixture, "migration-ar-opening.v1", 101m);

        var staleRead = await service.ReadAsync(fixture.Request, scenario.RunId);
        Assert.NotNull(staleRead);
        Assert.Equal(reconciliation.Value!.Id, staleRead!.Id);
        Assert.False(staleRead.IsCurrent);

        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value!.Id, reconciliation.Value.Version, "s11-r12-ready"));

        Assert.Equal("migration_reconciliation_stale", readiness.Code);
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        Assert.Equal(0, await db.HandoverReadiness.CountAsync(item => item.RunId == scenario.RunId));

        await using var approvalFixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var approvalScenario = await ExecuteMixedAsync(approvalFixture, "S11-R12-APPROVAL");
        var approvalReviewer = Guid.NewGuid();
        var approvalPolicy = ApprovalPolicy(approvalFixture.Request.ActorId!.Value, approvalReviewer);
        var approvalService = Service(approvalFixture, new TestApprovalPolicy(approvalPolicy));
        var approvedReconciliation = await ReconcileAsync(approvalFixture, approvalService, approvalScenario.RunId, "s11-r12-approval-reconcile");
        Assert.True(approvedReconciliation.Succeeded, approvedReconciliation.Code);
        var priorApproval = await approvalService.ApproveAsync(RequestForActor(approvalFixture, approvalReviewer),
            ApprovalRequest(approvedReconciliation.Value!, "s11-r12-approval-approve"));
        Assert.True(priorApproval.Succeeded, priorApproval.Code);

        var expandedEligibleActors = approvalPolicy.Requirements.Single() with
        {
            EligibleActorIds = [.. approvalPolicy.Requirements.Single().EligibleActorIds, Guid.NewGuid()]
        };
        var currentPolicy = approvalPolicy with { Requirements = [expandedEligibleActors] };
        var currentService = Service(approvalFixture, new TestApprovalPolicy(currentPolicy));
        var currentReconciliation = await ReconcileAsync(approvalFixture, currentService, approvalScenario.RunId, "s11-r12-current-reconcile");
        Assert.True(currentReconciliation.Succeeded, currentReconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Reconciled, currentReconciliation.Value!.Status);
        Assert.NotEqual(approvedReconciliation.Value!.Id, currentReconciliation.Value!.Id);
        Assert.NotEqual(approvedReconciliation.Value!.EvidenceFingerprint, currentReconciliation.Value.EvidenceFingerprint);
        var currentReadiness = await currentService.CreateReadinessAsync(approvalFixture.Request,
            new MigrationHandoverRequest(approvalScenario.RunId, currentReconciliation.Value.Id,
                currentReconciliation.Value.Version, "s11-r12-current-ready"));
        Assert.Equal("migration_approval_required", currentReadiness.Code);
        await using var approvalDb = new MigrationDbContext(approvalFixture.MigrationOptions, approvalFixture.Tenant);
        var persistedApproval = await approvalDb.ReconciliationApprovals.SingleAsync(item => item.RunId == approvalScenario.RunId);
        Assert.Equal(approvedReconciliation.Value.Id, persistedApproval.ReconciliationId);
        Assert.Equal(approvedReconciliation.Value.EvidenceFingerprint, persistedApproval.EvidenceFingerprint);
        Assert.Equal(0, await approvalDb.HandoverReadiness.CountAsync(item => item.RunId == approvalScenario.RunId));
    }

    [Fact]
    public async Task Sql_server_s11_r13_partial_domain_evidence_is_retained_and_readiness_is_blocked()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R13");
        await fixture.RemoveArSourceEffectAsync();
        var service = Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy());

        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r13-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Blocked, reconciliation.Value!.Status);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Inventory);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Ar && item.IsBlocking);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Ap);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.CashBank);
        Assert.Contains(reconciliation.Value.Details, item => item.Domain == MigrationReconciliationDomain.Gl);
        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value.Id, reconciliation.Value.Version, "s11-r13-ready"));
        Assert.Equal("migration_reconciliation_blocked", readiness.Code);
    }

    [Fact]
    public async Task Sql_server_s11_r14_unknown_outcome_blocks_handover()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R14");
        var unknown = Assert.Single(scenario.Execution.Effects, item => item.RecordType == MigrationCanonicalRecordType.ArOpening);
        await using (var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant))
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [migration].[MigrationExecutionEffects] SET [Disposition] = {(int)MigrationExecutionEffectDisposition.Unknown} WHERE [TenantId] = {fixture.Tenant.TenantId.Value} AND [RunId] = {scenario.RunId} AND [AttemptId] = {scenario.Execution.AttemptId} AND [Id] = {unknown.Id}");
        var service = Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy());

        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r14-reconcile");

        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        Assert.Equal(MigrationReconciliationStatus.Blocked, reconciliation.Value!.Status);
        Assert.True(reconciliation.Value.UnresolvedCount > 0);
        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value.Id, reconciliation.Value.Version, "s11-r14-ready"));
        Assert.Equal("migration_reconciliation_blocked", readiness.Code);
    }

    [Fact]
    public async Task Sql_server_s11_r15_foreign_tenant_cannot_read_or_mutate_reconciliation()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R15");
        var service = Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy());
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r15-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var foreign = ForeignTenantRequest();
        var run = await fixture.Migration.FindRunAsync(fixture.Tenant, scenario.RunId);
        Assert.NotNull(run);

        Assert.Null(await service.ReadAsync(foreign, scenario.RunId));
        var read = await service.ReconcileAsync(foreign,
            new MigrationReconcileRequest(scenario.RunId, run!.Version, "s11-r15-foreign-reconcile"));
        Assert.Equal("migration_source_scope_denied", read.Code);
        var approval = await service.ApproveAsync(foreign, ApprovalRequest(reconciliation.Value!, "s11-r15-foreign-approve"));
        Assert.Equal("migration_reconciliation_not_found", approval.Code);
        var readiness = await service.CreateReadinessAsync(foreign,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value!.Id, reconciliation.Value.Version, "s11-r15-foreign-ready"));
        Assert.Equal("migration_reconciliation_not_found", readiness.Code);
    }

    [Fact]
    public async Task Sql_server_s11_r16_configured_but_missing_approval_blocks_readiness()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R16");
        var service = Service(fixture, new TestApprovalPolicy(ApprovalPolicy(fixture.Request.ActorId!.Value, Guid.NewGuid())));
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r16-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value!.Id, reconciliation.Value.Version, "s11-r16-ready"));

        Assert.Equal("migration_approval_required", readiness.Code);
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        Assert.Equal(0, await db.HandoverReadiness.CountAsync(item => item.RunId == scenario.RunId));
    }

    [Fact]
    public async Task M40_dec_006_unconfigured_policy_fails_closed_without_selecting_production_quorum()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-DEC006-NO-POLICY");
        // The null policy proves fail-closed behavior and does not resolve M40-DEC-006.
        var service = Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy());
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-dec006-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);

        var approval = await service.ApproveAsync(fixture.Request,
            ApprovalRequest(reconciliation.Value!, "s11-dec006-approve"));
        Assert.Equal("approval_policy_not_configured", approval.Code);
        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value!.Id, reconciliation.Value.Version, "s11-dec006-ready"));
        Assert.Equal("approval_policy_not_configured", readiness.Code);

        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        Assert.Equal(0, await db.ReconciliationApprovals.CountAsync(item => item.RunId == scenario.RunId));
        Assert.Equal(0, await db.HandoverReadiness.CountAsync(item => item.RunId == scenario.RunId));
    }

    [Fact]
    public async Task Sql_server_s11_r17_test_policy_can_reach_business_ready_for_handover()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R17");
        var reviewer = Guid.NewGuid();
        var service = Service(fixture, new TestApprovalPolicy(ApprovalPolicy(fixture.Request.ActorId!.Value, reviewer)));
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r17-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var approved = await service.ApproveAsync(RequestForActor(fixture, reviewer), ApprovalRequest(reconciliation.Value!, "s11-r17-approve"));
        Assert.True(approved.Succeeded, approved.Code);

        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value!.Id, reconciliation.Value.Version, "s11-r17-ready"));

        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.True(readiness.Value!.BusinessReady);
        Assert.False(readiness.Value.ProductionReady);
        Assert.False(readiness.Value.Mesp48Complete);
        Assert.False(readiness.Value.Mesp50Complete);
        Assert.False(readiness.Value.TenantActivationPerformed);
    }

    [Fact]
    public async Task Sql_server_s11_r18_ready_for_handover_never_activates_the_tenant()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R18");
        var reviewer = Guid.NewGuid();
        var service = Service(fixture, new TestApprovalPolicy(ApprovalPolicy(fixture.Request.ActorId!.Value, reviewer)));
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-r18-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var approved = await service.ApproveAsync(RequestForActor(fixture, reviewer), ApprovalRequest(reconciliation.Value!, "s11-r18-approve"));
        Assert.True(approved.Succeeded, approved.Code);
        var schemaCountsBefore = await ReadSchemaRowCountsAsync(fixture);
        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value!.Id, reconciliation.Value.Version, "s11-r18-ready"));
        var schemaCountsAfter = await ReadSchemaRowCountsAsync(fixture);

        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.False(readiness.Value!.TenantActivationPerformed);
        Assert.Equal(MigrationRunStatus.ReadyForHandover, (await fixture.Migration.FindRunAsync(fixture.Tenant, scenario.RunId))!.Status);
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        var persistedReadiness = await db.HandoverReadiness.SingleAsync(item => item.RunId == scenario.RunId);
        Assert.False(persistedReadiness.TenantActivationPerformed);
        Assert.False(persistedReadiness.ProductionReady);
        Assert.False(persistedReadiness.Mesp48Complete);
        Assert.False(persistedReadiness.Mesp50Complete);
        var otherSchemasBefore = schemaCountsBefore.Where(item => item.Key.Schema != "migration")
            .OrderBy(item => item.Key.Schema, StringComparer.Ordinal).ThenBy(item => item.Key.Table, StringComparer.Ordinal).ToArray();
        var otherSchemasAfter = schemaCountsAfter.Where(item => item.Key.Schema != "migration")
            .OrderBy(item => item.Key.Schema, StringComparer.Ordinal).ThenBy(item => item.Key.Table, StringComparer.Ordinal).ToArray();
        Assert.Equal(otherSchemasBefore, otherSchemasAfter);
        Assert.DoesNotContain(typeof(MigrationReconciliationService).GetConstructors().Single().GetParameters(), parameter =>
            parameter.ParameterType.Namespace?.StartsWith("MiniErp.App.Modules.Platform", StringComparison.Ordinal) == true
            || parameter.ParameterType.Namespace?.StartsWith("MiniErp.App.Modules.Identity", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Sql_server_s11_mesp164_stale_reconcile_replay_after_ready_for_handover_is_replayed()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-MESP164");
        var reviewer = Guid.NewGuid();
        var service = Service(fixture, new TestApprovalPolicy(ApprovalPolicy(fixture.Request.ActorId!.Value, reviewer)));
        var capturedVersion = (await fixture.Migration.FindRunAsync(fixture.Tenant, scenario.RunId))!.Version;
        var reconcileRequest = new MigrationReconcileRequest(scenario.RunId, capturedVersion, "s11-mesp164-reconcile");
        var reconciliation = await service.ReconcileAsync(fixture.Request, reconcileRequest);
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var approved = await service.ApproveAsync(RequestForActor(fixture, reviewer), ApprovalRequest(reconciliation.Value!, "s11-mesp164-approve"));
        Assert.True(approved.Succeeded, approved.Code);
        var readiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, reconciliation.Value!.Id, reconciliation.Value.Version, "s11-mesp164-ready"));
        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(MigrationRunStatus.ReadyForHandover, (await fixture.Migration.FindRunAsync(fixture.Tenant, scenario.RunId))!.Status);

        var replay = await service.ReconcileAsync(fixture.Request, reconcileRequest);

        Assert.True(replay.Kind == MigrationResultKind.Replayed, replay.Code);
        Assert.Equal(reconciliation.Value.Id, replay.Value!.Id);
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        Assert.Equal(1, await db.Reconciliations.CountAsync(item => item.RunId == scenario.RunId));
        Assert.Equal(MigrationRunStatus.ReadyForHandover, (await fixture.Migration.FindRunAsync(fixture.Tenant, scenario.RunId))!.Status);
    }

    [Fact]
    public async Task Sql_server_s11_r19_repeated_reads_use_saved_mapping_without_reinterpreting_current_rules()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var prepared = await fixture.PrepareAsync("S11-R19", 100m);
        var executed = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId, "s11-r19-execute", prepared.Run.Version);
        Assert.True(executed.Succeeded, executed.Code);
        var service = Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy());
        var reconciliation = await ReconcileAsync(fixture, service, prepared.Run.RunId, "s11-r19-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var mapping = Assert.Single(reconciliation.Value!.Details, item => item.Domain == MigrationReconciliationDomain.Ar);
        var countBefore = await ReconciliationCountAsync(fixture, prepared.Run.RunId);
        await using (var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant))
        {
            var historicalRule = await db.PostingRules.SingleAsync(item => item.CompanyId == fixture.CompanyId
                && item.SourceContract == "migration-ar-opening.v1" && item.SourceEvent == "recognition");
            var debitCode = await db.Accounts.Where(item => item.Id == fixture.GlDebitAccountId).Select(item => item.Code).SingleAsync();
            var creditCode = await db.Accounts.Where(item => item.Id == fixture.GlCreditAccountId).Select(item => item.Code).SingleAsync();
            historicalRule.SetLifecycle(FinancePostingRuleLifecycle.Disabled);
            db.PostingRules.Add(new FinancePostingRuleEntity(fixture.Tenant.TenantId, Guid.NewGuid(),
                new FinancePostingRuleCommand(fixture.CompanyId, historicalRule.SourceContract, historicalRule.SourceEvent,
                    fixture.GlDebitAccountId, fixture.GlCreditAccountId, false, new DateOnly(2026, 1, 1), null,
                    Guid.NewGuid(), "s11-current-rule", "s11-current-rule"), historicalRule.VersionNumber + 1, debitCode, creditCode));
            await db.SaveChangesAsync();
        }

        var first = await service.ReadAsync(fixture.Request, prepared.Run.RunId);
        var second = await service.ReadAsync(fixture.Request, prepared.Run.RunId);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(reconciliation.Value.Id, first!.Id);
        Assert.Equal(mapping.ControlAccountId, Assert.Single(first.Details, item => item.Domain == MigrationReconciliationDomain.Ar).ControlAccountId);
        Assert.Equal(mapping.PostingRuleId, Assert.Single(second!.Details, item => item.Domain == MigrationReconciliationDomain.Ar).PostingRuleId);
        Assert.Equal(first.EvidenceFingerprint, second.EvidenceFingerprint);
        Assert.True(first.IsCurrent);
        Assert.Equal(countBefore, await ReconciliationCountAsync(fixture, prepared.Run.RunId));
    }

    [Fact]
    public async Task Sql_server_s11_r20_concurrent_repeated_actions_are_idempotent()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-R20");
        var reviewer = Guid.NewGuid();
        var policy = ApprovalPolicy(fixture.Request.ActorId!.Value, reviewer);
        var currentRun = (await fixture.Migration.FindRunAsync(fixture.Tenant, scenario.RunId))!;
        var reconcileRequest = new MigrationReconcileRequest(scenario.RunId, currentRun.Version, "s11-r20-reconcile");
        var reconciliations = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            Service(fixture, new TestApprovalPolicy(policy)).ReconcileAsync(fixture.Request, reconcileRequest)));
        Assert.All(reconciliations, item => Assert.True(item.Succeeded || item.Kind == MigrationResultKind.Replayed, item.Code));
        Assert.Single(reconciliations.Select(item => item.Value!.Id).Distinct());
        var reconciliation = reconciliations[0].Value!;
        var approvals = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            Service(fixture, new TestApprovalPolicy(policy)).ApproveAsync(RequestForActor(fixture, reviewer), ApprovalRequest(reconciliation, "s11-r20-approve"))));
        Assert.All(approvals, item => Assert.True(item.Succeeded || item.Kind == MigrationResultKind.Replayed, item.Code));
        Assert.Single(approvals.Select(item => item.Value!.Id).Distinct());
        var readinessRequest = new MigrationHandoverRequest(scenario.RunId, reconciliation.Id, reconciliation.Version, "s11-r20-ready");
        var snapshots = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            Service(fixture, new TestApprovalPolicy(policy)).CreateReadinessAsync(fixture.Request, readinessRequest)));
        Assert.All(snapshots, item => Assert.True(item.Succeeded || item.Kind == MigrationResultKind.Replayed, item.Code));
        Assert.Single(snapshots.Select(item => item.Value!.Id).Distinct());
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        Assert.Equal(1, await db.Reconciliations.CountAsync(item => item.RunId == scenario.RunId));
        Assert.Equal(1, await db.ReconciliationApprovals.CountAsync(item => item.RunId == scenario.RunId));
        Assert.Equal(1, await db.HandoverReadiness.CountAsync(item => item.RunId == scenario.RunId));
    }

    [Fact]
    public async Task Sql_server_s11_a5_stale_versions_are_rejected_without_persisting_records()
    {
        await using var fixture = await MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture.CreateAsync(safety);
        var scenario = await ExecuteMixedAsync(fixture, "S11-A5");
        var currentRun = (await fixture.Migration.FindRunAsync(fixture.Tenant, scenario.RunId))!;
        var reconciliationCountBeforeReconcile = await ReconciliationCountAsync(fixture, scenario.RunId);
        var approvalCountBeforeReconcile = await ApprovalCountAsync(fixture, scenario.RunId);
        var readinessCountBeforeReconcile = await ReadinessCountAsync(fixture, scenario.RunId);
        var staleReconcile = await Service(fixture, new UnconfiguredMigrationReconciliationApprovalPolicy()).ReconcileAsync(
            fixture.Request, new MigrationReconcileRequest(scenario.RunId, StaleVersion(currentRun.Version), "s11-a5-stale-reconcile"));
        Assert.Equal("migration_run_version_conflict", staleReconcile.Code);
        Assert.Equal(reconciliationCountBeforeReconcile, await ReconciliationCountAsync(fixture, scenario.RunId));
        Assert.Equal(approvalCountBeforeReconcile, await ApprovalCountAsync(fixture, scenario.RunId));
        Assert.Equal(readinessCountBeforeReconcile, await ReadinessCountAsync(fixture, scenario.RunId));

        var reviewer = Guid.NewGuid();
        var service = Service(fixture, new TestApprovalPolicy(ApprovalPolicy(fixture.Request.ActorId!.Value, reviewer)));
        var reconciliation = await ReconcileAsync(fixture, service, scenario.RunId, "s11-a5-current-reconcile");
        Assert.True(reconciliation.Succeeded, reconciliation.Code);
        var currentReconciliation = reconciliation.Value!;
        var reconciliationCountBeforeApproval = await ReconciliationCountAsync(fixture, scenario.RunId);
        var approvalCountBeforeApproval = await ApprovalCountAsync(fixture, scenario.RunId);
        var readinessCountBeforeApproval = await ReadinessCountAsync(fixture, scenario.RunId);

        var staleApproval = await service.ApproveAsync(RequestForActor(fixture, reviewer),
            ApprovalRequest(currentReconciliation, "s11-a5-stale-approve") with { ExpectedVersion = StaleVersion(currentReconciliation.Version) });
        Assert.Equal("migration_reconciliation_version_conflict", staleApproval.Code);
        Assert.Equal(reconciliationCountBeforeApproval, await ReconciliationCountAsync(fixture, scenario.RunId));
        Assert.Equal(approvalCountBeforeApproval, await ApprovalCountAsync(fixture, scenario.RunId));
        Assert.Equal(readinessCountBeforeApproval, await ReadinessCountAsync(fixture, scenario.RunId));

        var reconciliationCountBeforeReadiness = await ReconciliationCountAsync(fixture, scenario.RunId);
        var approvalCountBeforeReadiness = await ApprovalCountAsync(fixture, scenario.RunId);
        var readinessCountBeforeReadiness = await ReadinessCountAsync(fixture, scenario.RunId);
        var staleReadiness = await service.CreateReadinessAsync(fixture.Request,
            new MigrationHandoverRequest(scenario.RunId, currentReconciliation.Id, StaleVersion(currentReconciliation.Version), "s11-a5-stale-ready"));
        Assert.Equal("migration_reconciliation_version_conflict", staleReadiness.Code);
        Assert.Equal(reconciliationCountBeforeReadiness, await ReconciliationCountAsync(fixture, scenario.RunId));
        Assert.Equal(approvalCountBeforeReadiness, await ApprovalCountAsync(fixture, scenario.RunId));
        Assert.Equal(readinessCountBeforeReadiness, await ReadinessCountAsync(fixture, scenario.RunId));
    }

    private sealed record ExecutedScenario(Guid RunId, IReadOnlyList<MigrationStagedRecord> Staged, MigrationExecutionResult Execution);

    private static async Task<ExecutedScenario> ExecuteMixedAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        string sourceReference,
        decimal amount = 100m,
        decimal? arAmount = null,
        string arCurrencyCode = "SAR")
    {
        var prepared = await fixture.PrepareMixedAsync(sourceReference, amount, includeCash: true, includeGl: true,
            arAmount: arAmount, arCurrencyCode: arCurrencyCode);
        var execution = await fixture.NewMixedExecution().ExecuteAsync(fixture.Request, prepared.Run.RunId,
            $"{sourceReference.ToLowerInvariant()}-execute", prepared.Run.Version);
        Assert.True(execution.Succeeded, execution.Code);
        return new ExecutedScenario(prepared.Run.RunId, prepared.Staged, execution.Value!);
    }

    private static async Task<MigrationOperationResult<MigrationReconciliationRecord>> ReconcileAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        MigrationReconciliationService service,
        Guid runId,
        string idempotencyKey)
    {
        var run = await fixture.Migration.FindRunAsync(fixture.Tenant, runId);
        Assert.NotNull(run);
        return await service.ReconcileAsync(fixture.Request, new MigrationReconcileRequest(runId, run!.Version, idempotencyKey));
    }

    private static MigrationReconciliationApprovalPolicy ApprovalPolicy(Guid preparer, Guid reviewer) => new(
        "s11-provider-test", 1, DateTimeOffset.UtcNow.AddMinutes(-1), null, true,
        [new MigrationReconciliationApprovalRequirement("migration", "handover", 1, [preparer, reviewer])]);

    private sealed class TestApprovalPolicy(MigrationReconciliationApprovalPolicy policy) : IMigrationReconciliationApprovalPolicy
    {
        public Task<MigrationReconciliationApprovalPolicy?> ResolveAsync(TenantContext tenant, Guid runId,
            DateTimeOffset at, CancellationToken cancellationToken = default) => Task.FromResult<MigrationReconciliationApprovalPolicy?>(policy);
    }

    private static MigrationApprovalRequest ApprovalRequest(MigrationReconciliationRecord reconciliation, string key) =>
        new(reconciliation.RunId, reconciliation.Id, "migration", "handover", MigrationApprovalDecision.Approved,
            null, reconciliation.Version, key);

    private static FoundationRequestContext RequestForActor(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        Guid actorId)
    {
        var tenant = TenantContext.ForOrdinaryMembership(fixture.Tenant.TenantId, fixture.Tenant.Membership!.Value,
            fixture.Tenant.Scope, new CorrelationId($"s11-review-{Guid.NewGuid():N}"), actorId);
        return FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, fixture.Request.Permission, fixture.Request.LifecycleState);
    }

    private static FoundationRequestContext ForeignTenantRequest()
    {
        var actorId = Guid.NewGuid();
        var tenant = TenantContext.ForOrdinaryMembership(new TenantId(Guid.NewGuid()), new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId($"s11-foreign-{Guid.NewGuid():N}"), actorId: actorId);
        return FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), tenant, "tenant.migration.execute");
    }

    private static async Task MutateFinanceJournalAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        string sourceContract,
        decimal amount)
    {
        await using var db = new FinanceDbContext(fixture.FinanceOptions, fixture.Tenant);
        var journal = await db.Journals.Where(item => item.CompanyId == fixture.CompanyId && item.SourceContract == sourceContract)
            .OrderByDescending(item => item.CreatedAt).FirstAsync();
        var line = await db.JournalLines.Where(item => item.JournalId == journal.Id).OrderBy(item => item.LineNumber).FirstAsync();
        await db.JournalLines.Where(item => item.Id == line.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.Debit, amount).SetProperty(item => item.FunctionalDebit, amount));
    }

    private static async Task<int> ReconciliationCountAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        Guid runId)
    {
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        return await db.Reconciliations.CountAsync(item => item.RunId == runId);
    }

    private static async Task<int> ApprovalCountAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        Guid runId)
    {
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        return await db.ReconciliationApprovals.CountAsync(item => item.RunId == runId);
    }

    private static async Task<int> ReadinessCountAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        Guid runId)
    {
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        return await db.HandoverReadiness.CountAsync(item => item.RunId == runId);
    }

    private sealed record HistoricalFinanceMapping(Guid ControlAccountId, Guid PostingRuleId, int PostingRuleVersionNumber);

    private static async Task<MigrationReconciliationDetail[]> ReadReconciliationDetailsAsync(
        MigrationDbContext db, Guid reconciliationId) =>
        await db.ReconciliationDetails.AsNoTracking().Where(item => item.ReconciliationId == reconciliationId)
            .OrderBy(item => item.Id)
            .Select(item => new MigrationReconciliationDetail(item.Id, item.Domain, item.ScopeKey,
                item.CompanyId, item.OpeningDate, item.CurrencyCode, item.TransactionCurrencyCode, item.FunctionalCurrencyCode,
                item.SourceCount, item.SourceDebit, item.SourceCredit, item.TargetDebit, item.TargetCredit,
                item.Variance, item.SourceAmount, item.TargetAmount, item.AmountVariance, item.OwnerRoundingDifference,
                item.TransactionAmount, item.FunctionalAmount, item.SubsidiaryEstablishedAmount, item.GlControlAmount,
                item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.AppliedRate,
                item.SourceQuantity, item.TargetQuantity, item.QuantityVariance, item.ControlAccountId, item.PostingRuleId,
                item.PostingRuleVersionNumber, item.OwnerSourceId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId,
                item.SourceContract, item.SourceEvent, item.RoundingPolicyId, item.RoundingPolicyVersionNumber,
                item.RoundingScale, item.RoundingMode, item.IsBlocking, item.FindingCode, item.Explanation,
                item.EffectId, item.OwnerReferenceId, item.LinkedAccountId))
            .ToArrayAsync();

    private static async Task<HistoricalFinanceMapping> ReadHistoricalFinanceMappingAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        Guid effectId)
    {
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        var mappings = await db.EconomicRepresentations.Where(item => item.EffectId == effectId
                && item.OwnerModule == MigrationEconomicOwnerModule.Finance
                && item.ControlAccountId.HasValue && item.PostingRuleId.HasValue && item.PostingRuleVersionNumber.HasValue)
            .Select(item => new HistoricalFinanceMapping(item.ControlAccountId!.Value, item.PostingRuleId!.Value, item.PostingRuleVersionNumber!.Value))
            .Distinct()
            .ToArrayAsync();
        return Assert.Single(mappings);
    }

    private static async Task<Dictionary<(string Schema, string Table), long>> ReadSchemaRowCountsAsync(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture)
    {
        await using var db = new MigrationDbContext(fixture.MigrationOptions, fixture.Tenant);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT schema_name.name, table_name.name, SUM(p.rows)
            FROM sys.tables AS table_name
            INNER JOIN sys.schemas AS schema_name ON schema_name.schema_id = table_name.schema_id
            INNER JOIN sys.partitions AS p ON p.object_id = table_name.object_id
            WHERE table_name.is_ms_shipped = 0 AND p.index_id IN (0, 1)
            GROUP BY schema_name.name, table_name.name
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var counts = new Dictionary<(string Schema, string Table), long>();
        while (await reader.ReadAsync())
            counts.Add((reader.GetString(0), reader.GetString(1)), reader.GetInt64(2));
        return counts;
    }

    private static byte[] StaleVersion(byte[] version)
    {
        var stale = version.ToArray();
        stale[^1] ^= 0xFF;
        return stale;
    }

    private static MigrationReconciliationService Service(
        MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture fixture,
        IMigrationReconciliationApprovalPolicy policy)
    {
        var scope = new TestScopeResolver();
        var audit = new NoopAuditSink();
        var foundation = new MigrationFoundationService(fixture.Migration, audit);
        var validation = new MigrationValidationService(foundation, fixture.Migration, new InMemoryPrivateObjectStorage(), scope,
            new UnavailableMigrationReferenceAuthority(), scope);
        var execution = fixture.NewMixedExecution();
        return new MigrationReconciliationService(foundation, fixture.Migration, fixture.Migration, fixture.Migration,
            validation, execution, policy, audit);
    }

    private sealed class TestScopeResolver : ICurrentOrganizationScopeResolver, IOrganizationScopeOwnershipResolver
    {
        public TenantWorkScopeResolution ResolveCurrent(TenantContext trustedTenantContext) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, TenantWorkScopeRequest.TenantWide()));
        public TenantWorkScopeResolution Resolve(TenantContext trustedTenantContext, TenantWorkScopeRequest requestedScope) =>
            TenantWorkScopeResolution.Resolved(TenantWorkScope.IssueFromVerifiedAuthority(trustedTenantContext, requestedScope));
    }

    private sealed class NoopAuditSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
