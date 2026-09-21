using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.Modules.Finance;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using Xunit;

namespace MiniErp.ArchitectureTests;

[Collection(SqlServerSafetyCollection.Name)]
public sealed class MigrationGlOpeningSqlServerSafetyTests(SqlServerSafetyFixture safety)
{
    [Theory]
    [InlineData("S9-SQL-01")]
    [InlineData("S9-SQL-02")]
    [InlineData("S9-SQL-03")]
    [InlineData("S9-SQL-04")]
    [InlineData("S9-SQL-05")]
    [InlineData("S9-SQL-06")]
    [InlineData("S9-SQL-07")]
    [InlineData("S9-SQL-08")]
    [InlineData("S9-SQL-09")]
    [InlineData("S9-SQL-10")]
    [InlineData("S9-SQL-11")]
    [InlineData("S9-SQL-12")]
    [InlineData("S9-SQL-13")]
    [InlineData("S9-SQL-14")]
    [InlineData("S9-SQL-15")]
    [InlineData("S9-SQL-16")]
    [InlineData("S9-SQL-17")]
    [InlineData("S9-SQL-18")]
    [InlineData("S9-SQL-19")]
    [InlineData("S9-SQL-20")]
    [InlineData("S9-SQL-21")]
    [InlineData("S9-SQL-22")]
    [InlineData("S9-SQL-23")]
    [InlineData("S9-SQL-24")]
    [InlineData("S9-SQL-25")]
    [InlineData("S9-SQL-26")]
    [InlineData("S9-SQL-27")]
    [InlineData("S9-SQL-28")]
    [InlineData("S9-SQL-29")]
    [InlineData("S9-SQL-30")]
    [InlineData("S9-SQL-31")]
    [InlineData("S9-SQL-32")]
    [InlineData("S9-SQL-33")]
    [InlineData("S9-SQL-34")]
    [InlineData("S9-SQL-35")]
    [InlineData("S9-SQL-36")]
    [InlineData("S9-SQL-37")]
    [InlineData("S9-SQL-38")]
    public async Task Sql_server_s9_matrix(string scenario)
    {
        await using var fixture = await MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture.CreateAsync(safety);
        switch (scenario)
        {
            case "S9-SQL-01": await OrdinaryGlOnlyAsync(fixture); break;
            case "S9-SQL-02": await DistinctOffsetsAsync(fixture); break;
            case "S9-SQL-03": await SharedOffsetAsync(fixture); break;
            case "S9-SQL-04": await ControlMismatchAsync(fixture, "inventory-valuation-finance.v1", InventoryEvent()); break;
            case "S9-SQL-05": await ControlMismatchAsync(fixture, "migration-ar-opening.v1", "recognition"); break;
            case "S9-SQL-06": await ControlMismatchAsync(fixture, "migration-ap-opening.v1", "recognition", true); break;
            case "S9-SQL-07": await ControlMismatchAsync(fixture, "migration-cash-bank-opening.v1", "recognition"); break;
            case "S9-SQL-08": await ControlWithoutEvidenceAsync(fixture); break;
            case "S9-SQL-09": await ControlOffsetCollisionAsync(fixture); break;
            case "S9-SQL-10": await HistoricalRuleUpgradeAsync(fixture); break;
            case "S9-SQL-11": await HistoricalAndNewRuleUpgradeAsync(fixture); break;
            case "S9-SQL-12": await AllFiveProjectionAsync(fixture); break;
            case "S9-SQL-13": await PrepareFailureHasNoEffectAsync(fixture); break;
            case "S9-SQL-14": await InventoryApprovalAsync(fixture); break;
            case "S9-SQL-15": await ApprovalMatrixAsync(fixture, FinanceApprovalRequirement.NotRequired, true); break;
            case "S9-SQL-16": await ApprovalMatrixAsync(fixture, FinanceApprovalRequirement.Required, false); break;
            case "S9-SQL-17": await ApprovalMatrixAsync(fixture, FinanceApprovalRequirement.NotConfigured, false); break;
            case "S9-SQL-18": await ExactReplayAsync(fixture); break;
            case "S9-SQL-19": await ReorderedReplayAsync(fixture); break;
            case "S9-SQL-20": await ChangedSourceReferenceAsync(fixture); break;
            case "S9-SQL-21": await ChangedAmountAsync(fixture); break;
            case "S9-SQL-22": await ChangedAccountAsync(fixture); break;
            case "S9-SQL-23": await ChangedDateAsync(fixture); break;
            case "S9-SQL-24": await ForeignCurrencyAsync(fixture); break;
            case "S9-SQL-25": await ConcurrentReplayAsync(fixture); break;
            case "S9-SQL-26": await LostResponseReadbackAsync(fixture); break;
            case "S9-SQL-27": await MissingJournalAsync(fixture); break;
            case "S9-SQL-28": await MissingSourceEffectAsync(fixture); break;
            case "S9-SQL-29": await MismatchedJournalLinesAsync(fixture); break;
            case "S9-SQL-30": await PreparedAmountMismatchAsync(fixture); break;
            case "S9-SQL-31": await PreparedRuleMismatchAsync(fixture); break;
            case "S9-SQL-32": await PreparedControlOffsetMismatchAsync(fixture); break;
            case "S9-SQL-33": await IncompleteFinanceEvidenceAsync(fixture); break;
            case "S9-SQL-34": await NoneffectAsync(fixture); break;
            case "S9-SQL-35": await PriorNonOpeningActivityAsync(fixture); break;
            case "S9-SQL-36": await PartialResumeAsync(fixture); break;
            case "S9-SQL-37": await ReadOnlyReconciliationAsync(fixture); break;
            case "S9-SQL-38": await PublicLineReconciliationAsync(fixture); break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private static async Task OrdinaryGlOnlyAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var result = await CreateAsync(f, [Line(f.CashLinkedAccountId, "ordinary-debit", 100m, 0m), Line(f.OffsetAccountId, "ordinary-credit", 0m, 100m)]);
        Assert.NotNull(result.Journal);
        Assert.NotNull(result.SourceEffect);
        await AssertCountsAsync(f, 1, 1);
        await AssertLedgerAsync(f, new Dictionary<Guid, decimal> { [f.CashLinkedAccountId] = 100m, [f.OffsetAccountId] = -100m });
    }

    private static async Task DistinctOffsetsAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var inventory = await AddPairAsync(f, "inventory-valuation-finance.v1", InventoryEvent());
        var ar = await AddPairAsync(f, "migration-ar-opening.v1", "recognition");
        var ap = await AddPairAsync(f, "migration-ap-opening.v1", "recognition");
        await SeedAsync(f, "inventory-valuation-finance.v1", InventoryEvent(), inventory, 100m);
        await SeedAsync(f, "migration-ar-opening.v1", "recognition", ar, 300m);
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 50m);
        await SeedAsync(f, "migration-ap-opening.v1", "recognition", ap, 200m);
        var fixedAssets = await AddAccountAsync(f, "S9-FIXED");
        var loans = await AddAccountAsync(f, "S9-LOANS");
        var equity = await AddAccountAsync(f, "S9-EQUITY");
        var result = await CreateAsync(f,
        [
            Line(inventory.Debit, "inventory-control", 100m, 0m),
            Line(ar.Debit, "ar-control", 300m, 0m),
            Line(f.CashLinkedAccountId, "cash-control", 50m, 0m),
            Line(ap.Credit, "ap-control", 0m, 200m),
            Line(fixedAssets, "fixed-assets", 400m, 0m),
            Line(loans, "loans", 0m, 150m),
            Line(equity, "opening-equity", 0m, 500m)
        ]);
        Assert.NotNull(result.Journal);
        await AssertLedgerAsync(f, new Dictionary<Guid, decimal>
        {
            [inventory.Debit] = 100m, [ar.Debit] = 300m, [f.CashLinkedAccountId] = 50m,
            [ap.Credit] = -200m, [fixedAssets] = 400m, [loans] = -150m, [equity] = -500m,
            [inventory.Credit] = 0m, [ar.Credit] = 0m, [f.OffsetAccountId] = 0m, [ap.Debit] = 0m
        });
    }

    private static async Task SharedOffsetAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var control = await AddAccountAsync(f, "S9-SHARED-CONTROL");
        var rule = await AddRuleAsync(f, "migration-ar-opening.v1", "recognition", control, f.OffsetAccountId, 1);
        var balancing = await AddAccountAsync(f, "S9-SHARED-BALANCING");
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 50m);
        await SeedAsync(f, "migration-ar-opening.v1", "recognition", rule, 75m);
        var preflight = await PreflightAsync(f,
        [
            Line(f.CashLinkedAccountId, "cash-target", 50m, 0m),
            Line(control, "ar-target", 75m, 0m),
            Line(f.OffsetAccountId, "shared-target", 0m, 100m),
            Line(balancing, "balancing-credit", 0m, 25m)
        ]);
        Assert.True(preflight.Ready, preflight.Code);
        Assert.Contains(preflight.ResidualLines, item => item.AccountId == f.OffsetAccountId && item.Debit == 25m);
    }

    private static async Task ControlMismatchAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, string contract, string @event, bool reversal = false)
    {
        var pair = contract == "migration-cash-bank-opening.v1" ? BasePair(f) : await AddPairAsync(f, contract, @event);
        await SeedAsync(f, contract, @event, pair, 100m);
        var control = reversal ? pair.Credit : pair.Debit;
        var offset = reversal ? pair.Debit : pair.Credit;
        var preflight = await PreflightAsync(f, [Line(control, "mismatch-control", reversal ? 0m : 101m, reversal ? 101m : 0m), Line(offset, "mismatch-offset", reversal ? 101m : 0m, reversal ? 0m : 101m)]);
        Assert.False(preflight.Ready);
        Assert.Equal("migration_gl_control_account_mismatch", preflight.Code);
        await AssertCountsAsync(f, 0, 0);
    }

    private static async Task ControlWithoutEvidenceAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var pair = BasePair(f);
        var projection = Projection("migration-cash-bank-opening.v1", "recognition", 100m, false, pair, Guid.NewGuid());
        var preflight = await PreflightAsync(f, [Line(pair.Debit, "cash-control", 101m, 0m), Line(pair.Credit, "cash-offset", 0m, 101m)], [projection]);
        Assert.False(preflight.Ready);
        Assert.Equal("migration_gl_control_account_mismatch", preflight.Code);
    }

    private static async Task ControlOffsetCollisionAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var arOffset = await AddAccountAsync(f, "S9-COLLISION-OFFSET");
        var ar = await AddRuleAsync(f, "migration-ar-opening.v1", "recognition", f.OffsetAccountId, arOffset, 1);
        var projections = new[]
        {
            Projection("migration-cash-bank-opening.v1", "recognition", 10m, false, BasePair(f), Guid.NewGuid()),
            Projection("migration-ar-opening.v1", "recognition", 10m, false, ar, Guid.NewGuid())
        };
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "cash", 10m, 0m), Line(arOffset, "ar", 0m, 10m)], projections);
        Assert.False(preflight.Ready);
        Assert.Equal("migration_gl_control_offset_collision", preflight.Code);
    }

    private static async Task HistoricalRuleUpgradeAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 100m);
        await f.DisableOpeningRuleAsync();
        await f.AddOpeningRuleAsync(f.CashLinkedAccountId, f.OffsetAccountId);
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "cash", 100m, 0m), Line(f.OffsetAccountId, "offset", 0m, 100m)],
            [Projection("migration-cash-bank-opening.v1", "recognition", 100m, true, BasePair(f), Guid.NewGuid(), sourceEvidenceKnown: true)]);
        Assert.True(preflight.Ready, preflight.Code);
        Assert.True(preflight.NonEffect);
        Assert.Empty(preflight.ResidualLines);
    }

    private static async Task HistoricalAndNewRuleUpgradeAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 100m);
        await f.DisableOpeningRuleAsync();
        await f.AddOpeningRuleAsync(f.CashLinkedAccountId, f.OffsetAccountId);
        var projections = new[]
        {
            Projection("migration-cash-bank-opening.v1", "recognition", 100m, true, BasePair(f), Guid.NewGuid(), sourceEvidenceKnown: true),
            new FinanceMigrationOpeningProjection("migration-cash-bank-opening.v1", "recognition", 50m, false, Guid.NewGuid())
        };
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "cash", 150m, 0m), Line(f.OffsetAccountId, "offset", 0m, 150m)], projections);
        Assert.True(preflight.Ready, preflight.Code);
        Assert.Contains(preflight.Expectations!, item => item.SourceRecordId == projections[1].SourceRecordId && item.PostingRuleVersionNumber == 2);
    }

    private static async Task AllFiveProjectionAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var inventory = await AddPairAsync(f, "inventory-valuation-finance.v1", InventoryEvent());
        var ar = await AddPairAsync(f, "migration-ar-opening.v1", "recognition");
        var ap = await AddPairAsync(f, "migration-ap-opening.v1", "recognition");
        var fixedAssets = await AddAccountAsync(f, "S9-ALL-FIVE-ASSET");
        var equity = await AddAccountAsync(f, "S9-ALL-FIVE-EQUITY");
        var projections = new[]
        {
            new FinanceMigrationOpeningProjection("inventory-valuation-finance.v1", InventoryEvent(), 10m, false, Guid.NewGuid()),
            new FinanceMigrationOpeningProjection("migration-ar-opening.v1", "recognition", 20m, false, Guid.NewGuid()),
            new FinanceMigrationOpeningProjection("migration-ap-opening.v1", "recognition", 30m, false, Guid.NewGuid()),
            new FinanceMigrationOpeningProjection("migration-cash-bank-opening.v1", "recognition", 40m, false, Guid.NewGuid())
        };
        var preflight = await PreflightAsync(f,
        [
            Line(inventory.Debit, "inventory", 10m, 0m), Line(ar.Debit, "ar", 20m, 0m),
            Line(f.CashLinkedAccountId, "cash", 40m, 0m), Line(ap.Credit, "ap", 0m, 30m),
            Line(fixedAssets, "asset", 50m, 0m), Line(equity, "equity", 0m, 90m)
        ], projections);
        Assert.True(preflight.Ready, preflight.Code);
        Assert.Equal(4, preflight.Expectations!.Count);
        var result = await CreateAsync(f,
        [
            Line(inventory.Debit, "inventory", 10m, 0m), Line(ar.Debit, "ar", 20m, 0m),
            Line(f.CashLinkedAccountId, "cash", 40m, 0m), Line(ap.Credit, "ap", 0m, 30m),
            Line(fixedAssets, "asset", 50m, 0m), Line(equity, "equity", 0m, 90m)
        ], projections);
        Assert.NotNull(result.Journal);
        await AssertCountsAsync(f, 1, 1);
    }

    private static async Task PrepareFailureHasNoEffectAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "unbalanced", 100m, 0m)]);
        Assert.False(preflight.Ready);
        Assert.Equal("migration_gl_opening_imbalanced", preflight.Code);
        await AssertCountsAsync(f, 0, 0);
    }

    private static async Task InventoryApprovalAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        f.Approval.Requirement = FinanceApprovalRequirement.Required;
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        Assert.False(preflight.Ready);
        Assert.Equal("approval_required", preflight.Code);
        await AssertCountsAsync(f, 0, 0);
    }

    private static async Task ApprovalMatrixAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, FinanceApprovalRequirement requirement, bool shouldExecute)
    {
        f.Approval.Requirement = requirement;
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        var preflight = await f.Settlement.PreflightMigrationGlOpeningAsync(f.FinanceContext, command);
        Assert.Equal(shouldExecute, preflight.Ready);
        if (shouldExecute) Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, command)).Succeeded);
        else Assert.Equal(requirement == FinanceApprovalRequirement.Required ? "approval_required" : "approval_policy_not_configured", preflight.Code);
    }

    private static async Task ExactReplayAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, command)).Succeeded);
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, command)).Succeeded);
        await AssertCountsAsync(f, 1, 1);
    }

    private static async Task ReorderedReplayAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var lines = new[] { Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m) };
        var command = Command(f, lines);
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, command)).Succeeded);
        var reordered = command with { Lines = lines.Reverse().ToArray() };
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, reordered)).Succeeded);
        await AssertCountsAsync(f, 1, 1);
    }

    private static async Task ChangedSourceReferenceAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var first = Command(f, [Line(f.CashLinkedAccountId, "old-reference", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], "old");
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, first)).Succeeded);
        var changed = Command(f, [Line(f.CashLinkedAccountId, "new-reference", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], "new");
        var result = await f.Settlement.PreflightMigrationGlOpeningAsync(f.FinanceContext, changed);
        Assert.False(result.Ready);
        Assert.Equal("migration_gl_source_conflict", result.Code);
    }

    private static async Task ChangedAmountAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], "old"))).Succeeded);
        var result = await f.Settlement.PreflightMigrationGlOpeningAsync(f.FinanceContext, Command(f, [Line(f.CashLinkedAccountId, "debit", 101m, 0m), Line(f.OffsetAccountId, "credit", 0m, 101m)], "new"));
        Assert.False(result.Ready);
        Assert.Equal("migration_gl_source_conflict", result.Code);
    }

    private static async Task ChangedAccountAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], "old"))).Succeeded);
        var replacement = await AddAccountAsync(f, "S9-CHANGED-ACCOUNT");
        var result = await f.Settlement.PreflightMigrationGlOpeningAsync(f.FinanceContext, Command(f, [Line(replacement, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], "new"));
        Assert.False(result.Ready);
        Assert.Equal("migration_gl_source_conflict", result.Code);
    }

    private static async Task ChangedDateAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], "same"))).Succeeded);
        var second = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], "same", new DateOnly(2026, 2, 15));
        var result = await f.Settlement.PreflightMigrationGlOpeningAsync(f.FinanceContext, second);
        Assert.False(result.Ready);
        Assert.Equal("migration_gl_source_conflict", result.Code);
        await AssertCountsAsync(f, 1, 1);
    }

    private static async Task ForeignCurrencyAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var result = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)], currency: "USD");
        Assert.False(result.Ready);
        Assert.Equal("migration_gl_opening_currency_not_functional", result.Code);
    }

    private static async Task ConcurrentReplayAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        using var gate = new Barrier(2);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            gate.SignalAndWait();
            return await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, command);
        })));
        Assert.Contains(results, item => item.Succeeded);
        await AssertCountsAsync(f, 1, 1);
    }

    private static async Task LostResponseReadbackAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        Assert.True((await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, command)).Succeeded);
        var recovered = await f.Settlement.ReadMigrationGlOpeningAsync(f.FinanceContext, command);
        Assert.NotNull(recovered?.Journal);
        Assert.NotNull(recovered?.SourceEffect);
        await AssertCountsAsync(f, 1, 1);
    }

    private static async Task MissingJournalAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        var created = await CreateAsync(f, command.Lines);
        await using (var db = new FinanceDbContext(f.FinanceOptions, f.Tenant))
        {
            db.Journals.Remove(await db.Journals.SingleAsync(item => item.Id == created.Journal!.Id));
            await db.SaveChangesAsync();
        }
        Assert.Null(await f.Settlement.ReadMigrationGlOpeningAsync(f.FinanceContext, command));
    }

    private static async Task MissingSourceEffectAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        var created = await CreateAsync(f, command.Lines);
        await using (var db = new FinanceDbContext(f.FinanceOptions, f.Tenant))
        {
            db.SourceEffects.Remove(await db.SourceEffects.SingleAsync(item => item.Id == created.SourceEffect!.Id));
            await db.SaveChangesAsync();
        }
        Assert.Null(await f.Settlement.ReadMigrationGlOpeningAsync(f.FinanceContext, command));
    }

    private static async Task MismatchedJournalLinesAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        var created = await CreateAsync(f, command.Lines);
        await using (var db = new FinanceDbContext(f.FinanceOptions, f.Tenant))
        {
            var line = await db.JournalLines.SingleAsync(item => item.JournalId == created.Journal!.Id && item.LineNumber == 1);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [finance].[JournalLines] SET [FunctionalDebit] = {101m}, [Debit] = {101m} WHERE [TenantId] = {f.Tenant.TenantId.Value} AND [Id] = {line.Id}");
        }
        Assert.Null(await f.Settlement.ReadMigrationGlOpeningAsync(f.FinanceContext, command));
    }

    private static async Task PreparedAmountMismatchAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 101m);
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "cash", 100m, 0m), Line(f.OffsetAccountId, "offset", 0m, 100m)],
            [Projection("migration-cash-bank-opening.v1", "recognition", 100m, false, BasePair(f), Guid.NewGuid())]);
        Assert.False(preflight.Ready);
        Assert.Equal("migration_gl_control_account_mismatch", preflight.Code);
    }

    private static async Task PreparedRuleMismatchAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var alternate = await AddPairAsync(f, "migration-ar-opening.v1", "recognition");
        var projection = Projection("migration-ar-opening.v1", "recognition", 100m, false, alternate, Guid.NewGuid());
        var preflight = await PreflightAsync(f, [Line(alternate.Debit, "control", 100m, 0m), Line(alternate.Credit, "offset", 0m, 100m)], [projection]);
        Assert.True(preflight.Ready, preflight.Code);
        Assert.Equal(alternate.RuleId, Assert.Single(preflight.Expectations!).PostingRuleId);
    }

    private static async Task PreparedControlOffsetMismatchAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var pair = await AddPairAsync(f, "migration-ar-opening.v1", "recognition");
        var projection = Projection("migration-ar-opening.v1", "recognition", 100m, false, pair, Guid.NewGuid());
        var preflight = await PreflightAsync(f, [Line(pair.Credit, "wrong-control", 100m, 0m), Line(pair.Debit, "wrong-offset", 0m, 100m)], [projection]);
        Assert.False(preflight.Ready);
        Assert.Equal("migration_gl_control_account_mismatch", preflight.Code);
    }

    private static async Task IncompleteFinanceEvidenceAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await MissingSourceEffectAsync(f);
    }

    private static async Task NoneffectAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 100m);
        var result = await CreateAsync(f, [Line(f.CashLinkedAccountId, "cash", 100m, 0m), Line(f.OffsetAccountId, "offset", 0m, 100m)],
            [Projection("migration-cash-bank-opening.v1", "recognition", 100m, true, BasePair(f), Guid.NewGuid(), sourceEvidenceKnown: true)]);
        Assert.Null(result.Journal);
        Assert.Null(result.SourceEffect);
        await AssertCountsAsync(f, 0, 0);
    }

    private static async Task PriorNonOpeningActivityAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await SeedManualJournalAsync(f);
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        Assert.False(preflight.Ready);
        Assert.Equal("migration_gl_opening_prior_ledger_activity", preflight.Code);
    }

    private static async Task PartialResumeAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 100m);
        var ar = await AddPairAsync(f, "migration-ar-opening.v1", "recognition");
        var projections = new[]
        {
            Projection("migration-cash-bank-opening.v1", "recognition", 100m, true, BasePair(f), Guid.NewGuid(), sourceEvidenceKnown: true),
            new FinanceMigrationOpeningProjection("migration-ar-opening.v1", "recognition", 50m, false, Guid.NewGuid())
        };
        var preflight = await PreflightAsync(f, [Line(f.CashLinkedAccountId, "cash", 100m, 0m), Line(ar.Debit, "ar", 50m, 0m), Line(f.OffsetAccountId, "offset", 0m, 150m)], projections);
        Assert.True(preflight.Ready, preflight.Code);
        Assert.Single(preflight.Expectations!, item => item.SourceRecordId == projections[1].SourceRecordId);
    }

    private static async Task ReadOnlyReconciliationAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var command = Command(f, [Line(f.CashLinkedAccountId, "debit", 100m, 0m), Line(f.OffsetAccountId, "credit", 0m, 100m)]);
        var before = await CountsAsync(f);
        _ = await f.Settlement.ReadMigrationGlOpeningAsync(f.FinanceContext, command);
        var after = await CountsAsync(f);
        Assert.Equal(before, after);
    }

    private static async Task PublicLineReconciliationAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await SeedAsync(f, "migration-cash-bank-opening.v1", "recognition", BasePair(f), 100m);
        var fixedAssets = await AddAccountAsync(f, "S9-RECON-ASSET");
        var equity = await AddAccountAsync(f, "S9-RECON-EQUITY");
        var preflight = await PreflightAsync(f,
        [
            Line(f.CashLinkedAccountId, "cash", 100m, 0m),
            Line(fixedAssets, "fixed-assets", 50m, 0m),
            Line(equity, "equity", 0m, 150m)
        ]);
        Assert.True(preflight.Ready, preflight.Code);
        Assert.Contains(preflight.ResidualLines, item => item.AccountId == f.OffsetAccountId && item.AccountingTreatment == "derived_offset_clearing" && item.SourceLineReference is null);
        Assert.Contains(preflight.ResidualLines, item => item.AccountId == fixedAssets && item.AccountingTreatment == "source_residual");
    }

    private static FinanceMigrationOpeningProjection Projection(string contract, string @event, decimal amount, bool established, Pair pair, Guid sourceRecordId, bool sourceEvidenceKnown = false) =>
        new(contract, @event, amount, established, sourceRecordId, Guid.NewGuid(), $"s9-{sourceRecordId:N}", pair.RuleId, pair.Version, pair.Debit, pair.Credit, pair.Reversal, sourceEvidenceKnown ? Guid.NewGuid() : null, sourceEvidenceKnown ? 1 : null);

    private static Pair BasePair(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f) =>
        new(f.CashLinkedAccountId, f.OffsetAccountId, f.OpeningRuleId, 1, false);

    private static FinanceMigrationGlOpeningLine Line(Guid accountId, string reference, decimal debit, decimal credit) =>
        new(accountId, reference, debit, credit);

    private static string InventoryEvent() =>
        FinanceInventoryPostingClassifier.Classify(InventoryMovementSourceType.OpeningBalance, InventoryMovementDirection.Inbound);

    private static FinanceMigrationGlOpeningCommand Command(
        MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f,
        IReadOnlyList<FinanceMigrationGlOpeningLine> lines,
        string fingerprint = "default",
        DateOnly? openingDate = null,
        string currency = "SAR",
        IReadOnlyList<FinanceMigrationOpeningProjection>? projections = null)
    {
        var hash = Fingerprint(fingerprint);
        return new(f.CompanyId, openingDate ?? new DateOnly(2026, 1, 15), currency, lines, projections ?? [], hash, $"s9-gl-{fingerprint}-{Guid.NewGuid():N}", hash, hash);
    }

    private static async Task<FinanceGlOpeningPreflightResult> PreflightAsync(
        MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f,
        IReadOnlyList<FinanceMigrationGlOpeningLine> lines,
        IReadOnlyList<FinanceMigrationOpeningProjection>? projections = null,
        string currency = "SAR") =>
        await f.Settlement.PreflightMigrationGlOpeningAsync(f.FinanceContext, Command(f, lines, Guid.NewGuid().ToString("N"), currency: currency, projections: projections));

    private static async Task<FinanceMigrationGlOpeningEvidence> CreateAsync(
        MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f,
        IReadOnlyList<FinanceMigrationGlOpeningLine> lines,
        IReadOnlyList<FinanceMigrationOpeningProjection>? projections = null)
    {
        var result = await f.Settlement.CreateMigrationGlOpeningAsync(f.FinanceContext, Command(f, lines, Guid.NewGuid().ToString("N"), projections: projections));
        Assert.True(result.Succeeded, result.Code);
        return result.Value!;
    }

    private static async Task<Guid> AddAccountAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, string label)
    {
        var id = Guid.NewGuid();
        var code = $"{label}-{id:N}"[..Math.Min(32, label.Length + 1 + 32)];
        await using var db = new FinanceDbContext(f.FinanceOptions, f.Tenant);
        db.Accounts.Add(new FinanceAccountEntity(f.Tenant.TenantId, id, new FinanceAccountCommand(f.CompanyId, code, code, null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, id, null, $"s9-account-{id:N}", $"s9-account-{id:N}")));
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Pair> AddPairAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, string contract, string @event, bool reversal = false)
    {
        var debit = await AddAccountAsync(f, $"S9-{contract[..Math.Min(10, contract.Length)]}-D");
        var credit = await AddAccountAsync(f, $"S9-{contract[..Math.Min(10, contract.Length)]}-C");
        return await AddRuleAsync(f, contract, @event, reversal ? credit : debit, reversal ? debit : credit, 1, reversal);
    }

    private static async Task<Pair> AddRuleAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, string contract, string @event, Guid debit, Guid credit, int version, bool reversal = false)
    {
        await using var db = new FinanceDbContext(f.FinanceOptions, f.Tenant);
        var accounts = await db.Accounts.Where(item => item.Id == debit || item.Id == credit).ToDictionaryAsync(item => item.Id);
        var command = new FinancePostingRuleCommand(f.CompanyId, contract, @event, debit, credit, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), $"s9-rule-{Guid.NewGuid():N}", $"s9-rule-{Guid.NewGuid():N}");
        db.PostingRules.Add(new FinancePostingRuleEntity(f.Tenant.TenantId, command.Id, command, version, accounts[debit].Code, accounts[credit].Code));
        await db.SaveChangesAsync();
        return new(debit, credit, command.Id, version, reversal);
    }

    private static async Task SeedAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, string contract, string @event, Pair pair, decimal amount)
    {
        var evidence = Guid.NewGuid();
        var journalId = Guid.NewGuid();
        await using var db = new FinanceDbContext(f.FinanceOptions, f.Tenant);
        var accounts = await db.Accounts.Where(item => item.Id == pair.Debit || item.Id == pair.Credit).ToDictionaryAsync(item => item.Id);
        var period = await db.FiscalPeriods.SingleAsync(item => item.Id == f.PeriodId);
        var command = new FinanceJournalCommand(f.CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "SAR", 1m, null, null, null, contract, @event, evidence, 1, pair.RuleId, "S9 accepted subsidiary", [new(pair.Debit, amount, 0m, amount, "SAR", null, "control"), new(pair.Credit, 0m, amount, amount, "SAR", null, "offset")], journalId, $"s9-seed-{journalId:N}", $"s9-seed-{journalId:N}", FinanceJournalAmountAuthority.SourceFunctionalCurrency, FinanceApprovalRequirement.NotRequired);
        var sequence = (await db.Journals.Where(item => item.CompanyId == f.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync() ?? 0) + 1;
        var journal = new FinanceJournalEntity(f.Tenant.TenantId, journalId, command, sequence, "SAR", f.Tenant.ActorId!.Value, DateTimeOffset.UtcNow);
        journal.SetCorrelation($"s9-seed-{journalId:N}");
        journal.SetPeriod(period.FiscalYearId, period.Id);
        journal.SetRule(pair.RuleId, pair.Version);
        journal.SetStatus(FinanceJournalStatus.Posted, f.Tenant.ActorId.Value, DateTimeOffset.UtcNow);
        journal.Lines.Add(new FinanceJournalLineEntity(f.Tenant.TenantId, Guid.NewGuid(), journalId, 1, accounts[pair.Debit], command.Lines[0], null, amount, 0m, FinanceJournalAmountAuthority.SourceFunctionalCurrency));
        journal.Lines.Add(new FinanceJournalLineEntity(f.Tenant.TenantId, Guid.NewGuid(), journalId, 2, accounts[pair.Credit], command.Lines[1], null, 0m, amount, FinanceJournalAmountAuthority.SourceFunctionalCurrency));
        db.Journals.Add(journal);
        db.SourceEffects.Add(new FinanceSourceEffectEntity(f.Tenant.TenantId, Guid.NewGuid(), f.CompanyId, contract, evidence, 1, journalId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private static async Task SeedManualJournalAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        var evidence = Guid.NewGuid();
        var journalId = Guid.NewGuid();
        await using var db = new FinanceDbContext(f.FinanceOptions, f.Tenant);
        var accounts = await db.Accounts.Where(item => item.Id == f.CashLinkedAccountId || item.Id == f.OffsetAccountId).ToDictionaryAsync(item => item.Id);
        var period = await db.FiscalPeriods.SingleAsync(item => item.Id == f.PeriodId);
        var command = new FinanceJournalCommand(f.CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "SAR", 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "S9 prior non-opening", [new(f.CashLinkedAccountId, 1m, 0m, 1m, "SAR", null, "prior"), new(f.OffsetAccountId, 0m, 1m, 1m, "SAR", null, "prior")], journalId, $"s9-manual-{journalId:N}", $"s9-manual-{journalId:N}", FinanceJournalAmountAuthority.SourceFunctionalCurrency, FinanceApprovalRequirement.NotRequired);
        var sequence = (await db.Journals.Where(item => item.CompanyId == f.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync() ?? 0) + 1;
        var journal = new FinanceJournalEntity(f.Tenant.TenantId, journalId, command, sequence, "SAR", f.Tenant.ActorId!.Value, DateTimeOffset.UtcNow);
        journal.SetCorrelation($"s9-manual-{journalId:N}");
        journal.SetPeriod(period.FiscalYearId, period.Id);
        journal.SetStatus(FinanceJournalStatus.Posted, f.Tenant.ActorId.Value, DateTimeOffset.UtcNow);
        journal.Lines.Add(new FinanceJournalLineEntity(f.Tenant.TenantId, Guid.NewGuid(), journalId, 1, accounts[f.CashLinkedAccountId], command.Lines[0], null, 1m, 0m, FinanceJournalAmountAuthority.SourceFunctionalCurrency));
        journal.Lines.Add(new FinanceJournalLineEntity(f.Tenant.TenantId, Guid.NewGuid(), journalId, 2, accounts[f.OffsetAccountId], command.Lines[1], null, 0m, 1m, FinanceJournalAmountAuthority.SourceFunctionalCurrency));
        db.Journals.Add(journal);
        await db.SaveChangesAsync();
    }

    private static async Task AssertCountsAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, int journals, int effects)
    {
        var counts = await CountsAsync(f);
        Assert.Equal(journals, counts.Journals);
        Assert.Equal(effects, counts.Effects);
    }

    private static async Task<(int Journals, int Effects)> CountsAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f)
    {
        await using var db = new FinanceDbContext(f.FinanceOptions, f.Tenant);
        return (await db.Journals.CountAsync(item => item.CompanyId == f.CompanyId && item.SourceContract == "migration-gl-opening.v1"), await db.SourceEffects.CountAsync(item => item.CompanyId == f.CompanyId && item.SourceContract == "migration-gl-opening.v1"));
    }

    private static async Task AssertLedgerAsync(MigrationCashBankOpeningSqlServerSafetyTests.CashSqlFixture f, IReadOnlyDictionary<Guid, decimal> expected)
    {
        await using var db = new FinanceDbContext(f.FinanceOptions, f.Tenant);
        var actual = await db.Journals.Where(item => item.CompanyId == f.CompanyId).SelectMany(item => item.Lines).GroupBy(item => item.AccountId).ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.FunctionalDebit - item.FunctionalCredit));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual.GetValueOrDefault(pair.Key));
    }

    private static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record Pair(Guid Debit, Guid Credit, Guid RuleId, int Version, bool Reversal);
}
