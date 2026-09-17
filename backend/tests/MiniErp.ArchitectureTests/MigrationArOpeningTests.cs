using MiniErp.App.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationArOpeningTests
{
    [Fact]
    public void Ar_opening_requires_the_finance_owned_source_contract()
    {
        var row = new MigrationParsedCanonicalRow(
            1,
            "ar-1",
            MigrationCanonicalRecordType.ArOpening,
            new MigrationArOpeningPayload(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "AR-001",
                new DateOnly(2025, 12, 31),
                new DateOnly(2026, 1, 1),
                100m,
                "SAR",
                new DateOnly(2026, 2, 15),
                null),
            "{}");

        var findings = MigrationValidationRules.Validate(row);

        Assert.Empty(findings);
    }

    [Fact]
    public void Legacy_ar_control_account_is_rejected_deterministically()
    {
        var row = new MigrationParsedCanonicalRow(
            1,
            "ar-1",
            MigrationCanonicalRecordType.ArOpening,
            new MigrationArOpeningPayload(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "AR-001",
                new DateOnly(2025, 12, 31),
                new DateOnly(2026, 1, 1),
                100m,
                "SAR",
                new DateOnly(2026, 2, 15),
                null),
            "{}",
            HasForbiddenControlAccountId: true);

        var finding = Assert.Single(MigrationValidationRules.Validate(row), item => item.Code == "migration_ar_control_account_not_allowed");

        Assert.Equal(MigrationFindingCategory.FinancialBalance, finding.Category);
    }

    [Fact]
    public void Parser_preserves_legacy_control_account_as_a_validation_finding()
    {
        var json = """
        {
          "packageVersion": "migration-package-v1",
          "definitionId": "tenant-onboarding.foundation",
          "definitionVersion": "1",
          "sourceProfileId": "neutral-source-profile",
          "sourceProfileVersion": "1",
          "logicalDataset": "opening",
          "sourceSnapshot": { "objectId": "11111111-1111-1111-1111-111111111111", "sha256": "ABC", "length": 1, "concurrencyVersion": 1 },
          "records": [
            { "sourceSequence": 1, "sourceRecordId": "ar-1", "recordType": "ArOpening", "payload": {
              "companyId": "22222222-2222-2222-2222-222222222222",
              "customerId": "33333333-3333-3333-3333-333333333333",
              "sourceReference": "AR-001",
              "documentDate": "2025-12-31",
              "openingDate": "2026-01-01",
              "amount": 100,
              "currencyCode": "SAR",
              "dueDate": "2026-02-15",
              "controlAccountId": "44444444-4444-4444-4444-444444444444"
            }}
          ]
        }
        """;

        var parsed = MigrationCanonicalPackageParser.Parse(System.Text.Encoding.UTF8.GetBytes(json));

        Assert.True(parsed.Succeeded, parsed.ErrorMessage);
        Assert.True(parsed.Rows[0].HasForbiddenControlAccountId);
    }
}
