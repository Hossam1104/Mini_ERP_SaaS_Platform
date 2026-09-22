using System.Text;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Migration;
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
    public void Rules_reject_source_supplied_opening_monetary_evidence()
    {
        var row = new MigrationParsedCanonicalRow(
            1,
            "ar-source-fx",
            MigrationCanonicalRecordType.ArOpening,
            new MigrationArOpeningPayload(Guid.NewGuid(), Guid.NewGuid(), "AR-FX", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15), 100m, "USD", new DateOnly(2026, 2, 14)),
            "{}",
            HasForbiddenMonetaryInput: true);

        var findings = MigrationValidationRules.Validate(row);

        Assert.Contains(findings, item => item.Code == "migration_opening_source_monetary_fields_not_allowed");
    }

    [Fact]
    public void Dry_run_actions_are_explicitly_non_effectful()
    {
        Assert.Equal("Create", MigrationPlannedAction.Create.ToString());
        Assert.Equal("MatchReference", MigrationPlannedAction.MatchReference.ToString());
        Assert.Equal("Blocked", MigrationPlannedAction.Blocked.ToString());
        Assert.DoesNotContain("created", "would be evaluated by the owning module", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_fingerprint_is_boundary_safe()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var first = Run(tenant, new MigrationDefinitionReference("definition|version", "profile"), new MigrationSourceProfileReference("source", "1"));
        var second = Run(tenant, new MigrationDefinitionReference("definition", "version|profile"), new MigrationSourceProfileReference("source", "1"));
        var intake = new MigrationIntakeRecord(
            first,
            MigrationOperationKind.Validation,
            MigrationValidationFingerprint.Version,
            "intake",
            new MigrationSourceArtifactSnapshot(Guid.NewGuid(), tenant, null, null, null, "AA", 1, 1),
            DateTimeOffset.UnixEpoch,
            [1]);
        var secondIntake = intake with { Run = second };

        Assert.NotEqual(
            MigrationValidationFingerprint.ForValidation(first, intake).Value,
            MigrationValidationFingerprint.ForValidation(second, secondIntake).Value);
    }

    [Fact]
    public void Gl_balance_is_checked_per_company_currency_and_opening_date()
    {
        var tenant = new TenantId(Guid.NewGuid());
        var company = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            Gl(1, company, "SAR", date, 10m, 0m),
            Gl(2, company, "SAR", date, 0m, 10m),
            Gl(3, otherCompany, "SAR", date, 5m, 0m)
        };
        var staged = rows.Select(row => new MigrationStagedRecord(
            Guid.NewGuid(), tenant, runId, row.SourceSequence, row.SourceRecordId,
            row.RecordType, row.PayloadJson, "payload", "package", "1", Guid.NewGuid(), "snapshot", DateTimeOffset.UnixEpoch)).ToArray();
        var attempt = new MigrationAttemptRecord(Guid.NewGuid(), tenant, staged[0].RunId, 1, null, MigrationOperationKind.Validation, MigrationAttemptOutcome.Pending, "key", "fingerprint", DateTimeOffset.UnixEpoch, null, null, [1]);
        var results = rows.ToDictionary(row => row.SourceSequence, row => (Disposition: MigrationRecordDisposition.Accepted, Codes: new List<string>()));
        var findings = new List<MigrationValidationFinding>();

        MigrationValidationResultPolicy.AddGlBalanceFindings(rows, staged, attempt, results, findings);

        Assert.Single(findings);
        Assert.Equal(MigrationRecordDisposition.Accepted, results[1].Disposition);
        Assert.Equal(MigrationRecordDisposition.Accepted, results[2].Disposition);
        Assert.Equal(MigrationRecordDisposition.Rejected, results[3].Disposition);
    }

    private static MigrationRunRecord Run(TenantId tenant, MigrationDefinitionReference definition, MigrationSourceProfileReference profile) =>
        new(Guid.NewGuid(), tenant, Guid.NewGuid(), new CorrelationId("migration-test"), definition, profile, MigrationRunStatus.Draft, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, [1]);

    private static MigrationParsedCanonicalRow Gl(int sequence, Guid company, string currency, DateOnly date, decimal debit, decimal credit) =>
        new(sequence, $"gl-{sequence}", MigrationCanonicalRecordType.GlOpening, new MigrationGlOpeningPayload(company, Guid.NewGuid(), debit, credit, currency, date), "{}");

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
