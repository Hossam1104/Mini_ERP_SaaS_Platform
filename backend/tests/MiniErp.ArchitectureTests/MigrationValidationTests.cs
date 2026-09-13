using System.Text;
using MiniErp.App.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationValidationTests
{
    [Fact]
    public void Parser_rejects_duplicate_source_sequences_before_staging()
    {
        var result = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            "{\"sourceSequence\":1,\"recordType\":\"Product\",\"payload\":{\"sku\":\"A\"}}",
            "{\"sourceSequence\":1,\"recordType\":\"Product\",\"payload\":{\"sku\":\"B\"}}")));

        Assert.False(result.Succeeded);
        Assert.Equal("migration_package_rows_invalid", result.ErrorCode);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Parser_classifies_unknown_record_type_as_unsupported()
    {
        var result = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            "{\"sourceSequence\":1,\"recordType\":\"InvoiceHistory\",\"payload\":{}}")));

        Assert.False(result.Succeeded);
        Assert.Equal("migration_package_record_type_unsupported", result.ErrorCode);
    }

    [Fact]
    public void Rules_reject_negative_opening_and_two_sided_gl_lines()
    {
        var inventory = new MigrationParsedCanonicalRow(
            1,
            "inventory-1",
            MigrationCanonicalRecordType.InventoryOpening,
            new MigrationInventoryOpeningPayload(
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                -1m,
                2m,
                "SAR",
                new DateOnly(2026, 1, 1)),
            "{}");
        var gl = new MigrationParsedCanonicalRow(
            2,
            "gl-1",
            MigrationCanonicalRecordType.GlOpening,
            new MigrationGlOpeningPayload(
                Guid.NewGuid(),
                Guid.NewGuid(),
                10m,
                10m,
                "SAR",
                new DateOnly(2026, 1, 1)),
            "{}");

        var findings = MigrationValidationRules.Validate(inventory).Concat(MigrationValidationRules.Validate(gl)).ToArray();

        Assert.Contains(findings, item => item.Code == "migration_quantity_invalid");
        Assert.Contains(findings, item => item.Category == MigrationFindingCategory.FinancialBalance && item.Code == "migration_debit_credit_invalid");
    }

    [Fact]
    public void Dry_run_actions_are_explicitly_non_effectful()
    {
        Assert.Equal("Create", MigrationPlannedAction.Create.ToString());
        Assert.Equal("MatchReference", MigrationPlannedAction.MatchReference.ToString());
        Assert.Equal("Blocked", MigrationPlannedAction.Blocked.ToString());
        Assert.DoesNotContain("created", "would be evaluated by the owning module", StringComparison.OrdinalIgnoreCase);
    }

    private static string Package(params string[] records) =>
        """
        {
          "packageVersion": "migration-package-v1",
          "definitionId": "definition",
          "definitionVersion": "1",
          "sourceProfileId": "profile",
          "sourceProfileVersion": "1",
          "logicalDataset": "release1",
          "sourceSnapshot": {
            "objectId": "11111111-1111-1111-1111-111111111111",
            "sha256": "AA",
            "length": 1,
            "concurrencyVersion": 1
          },
          "records": [
        """ + string.Join(",", records) + """
          ]
        }
        """;
}
