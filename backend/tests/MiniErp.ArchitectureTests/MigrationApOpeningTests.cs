using MiniErp.App.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationApOpeningTests
{
    [Fact]
    public void Ap_opening_requires_the_finance_owned_source_contract()
    {
        var row = new MigrationParsedCanonicalRow(
            1,
            "ap-1",
            MigrationCanonicalRecordType.ApOpening,
            new MigrationApOpeningPayload(
                CompanyId: Guid.NewGuid(),
                SupplierId: Guid.NewGuid(),
                Amount: 100m,
                CurrencyCode: "SAR",
                OpeningDate: new DateOnly(2026, 1, 1),
                SourceReference: "AP-001",
                DocumentDate: new DateOnly(2025, 12, 31),
                DueDate: new DateOnly(2026, 2, 15)),
            "{}");

        Assert.Empty(MigrationValidationRules.Validate(row));
    }

    [Fact]
    public void Ap_opening_rejects_a_caller_supplied_control_account()
    {
        var row = new MigrationParsedCanonicalRow(
            1,
            "ap-1",
            MigrationCanonicalRecordType.ApOpening,
            new MigrationApOpeningPayload(
                CompanyId: Guid.NewGuid(),
                SupplierId: Guid.NewGuid(),
                Amount: 100m,
                CurrencyCode: "SAR",
                OpeningDate: new DateOnly(2026, 1, 1),
                SourceReference: "AP-001",
                DocumentDate: new DateOnly(2025, 12, 31),
                DueDate: new DateOnly(2026, 2, 15),
                ControlAccountId: Guid.NewGuid()),
            "{}",
            HasForbiddenControlAccountId: true);

        var finding = Assert.Single(MigrationValidationRules.Validate(row), item => item.Code == "migration_ap_control_account_not_allowed");

        Assert.Equal(MigrationFindingCategory.FinancialBalance, finding.Category);
    }

    [Fact]
    public void Parser_detects_a_legacy_ap_control_account()
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
            { "sourceSequence": 1, "sourceRecordId": "ap-1", "recordType": "ApOpening", "payload": {
              "companyId": "22222222-2222-2222-2222-222222222222",
              "supplierId": "33333333-3333-3333-3333-333333333333",
              "sourceReference": "AP-001",
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
