#pragma warning disable CS1591

using System.Reflection;
using System.Text.Json;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationGlOpeningEconomicTests
{
    [Fact]
    public void Source_group_fingerprint_is_canonical_and_order_independent()
    {
        var company = Guid.NewGuid();
        var first = new MigrationGlOpeningPayload(company, Guid.NewGuid(), 100m, 0m, "SAR", new DateOnly(2026, 1, 1), "fixed-assets");
        var second = new MigrationGlOpeningPayload(company, Guid.NewGuid(), 0m, 100m, "SAR", new DateOnly(2026, 1, 1), "opening-equity");
        var method = typeof(MigrationValidationService).Assembly
            .GetType("MiniErp.App.Modules.Migration.MigrationGlOpeningExecutionCoordinator")!
            .GetMethod("Fingerprint", BindingFlags.Static | BindingFlags.NonPublic)!;

        var left = method.Invoke(null, [new[] { first, second }]) as string;
        var right = method.Invoke(null, [new[] { second, first }]) as string;

        Assert.False(string.IsNullOrWhiteSpace(left));
        Assert.Equal(left, right);
    }

    [Fact]
    public void Derived_offset_reconciliation_has_no_fabricated_source_reference()
    {
        var line = new MigrationGlEconomicReconciliationLineRecord(
            Guid.NewGuid(), null, "derived_offset_clearing", 100m, 0m, 100m, 0m, false, Guid.NewGuid(), "GL opening derived finance offset clearing");

        var json = JsonSerializer.Serialize(line, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("derived_offset_clearing", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceLineReference\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_owner_projection_is_explicit_at_the_finance_boundary()
    {
        var projection = new FinanceMigrationOpeningProjection("migration-ar-opening.v1", "recognition", 100m, true);

        Assert.True(projection.AlreadyEstablishedExact);
        Assert.Equal(100m, projection.Amount);
    }
}

#pragma warning restore CS1591
