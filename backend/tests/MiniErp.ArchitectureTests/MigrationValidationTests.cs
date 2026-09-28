using System.Text;
using System.Text.Json;
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
    public void Parser_blocks_unknown_domain_contract_versions_before_staging()
    {
        var package = Package("{\"sourceSequence\":1,\"sourceRecordId\":\"product-1\",\"recordType\":\"Product\",\"payload\":{\"sku\":\"A\"}}")
            .Replace("migration-product-v1", "migration-product-v9", StringComparison.Ordinal);
        var result = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(package));
        var staleOrganizationContract = Package("{\"sourceSequence\":1,\"sourceRecordId\":\"company-1\",\"recordType\":\"Organization\",\"payload\":{\"companyId\":\"11111111-1111-1111-1111-111111111111\"}}")
            .Replace("migration-organization-v2", "migration-organization-v1", StringComparison.Ordinal);
        var staleResult = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(staleOrganizationContract));

        Assert.False(result.Succeeded);
        Assert.Equal("migration_package_domain_contract_incompatible", result.ErrorCode);
        Assert.Empty(result.Rows);
        Assert.False(staleResult.Succeeded);
        Assert.Equal("migration_package_domain_contract_incompatible", staleResult.ErrorCode);
        Assert.Empty(staleResult.Rows);
    }

    [Fact]
    public void Domain_contract_catalog_documents_every_versioned_record_shape()
    {
        var definitions = MigrationCanonicalDomainContractCatalog.All;

        Assert.Equal(Enum.GetValues<MigrationCanonicalRecordType>().Length, definitions.Count);
        Assert.All(definitions, definition =>
        {
            Assert.StartsWith("migration-", definition.Version, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(definition.Schema));
            Assert.True(
                definition.Schema.StartsWith("Required:", StringComparison.Ordinal)
                || definition.Schema.StartsWith("Fields:", StringComparison.Ordinal),
                $"{definition.RecordType} must document its required-field contract.");
        });
    }

    [Fact]
    public void Validation_rejects_source_target_authority_fields()
    {
        var result = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            "{\"sourceSequence\":1,\"sourceRecordId\":\"product-1\",\"recordType\":\"Product\",\"payload\":{\"sku\":\"A\",\"targetResourceId\":\"11111111-1111-1111-1111-111111111111\",\"approvalStatus\":\"Approved\"}}")));

        Assert.True(result.Succeeded, result.ErrorMessage);
        var row = Assert.Single(result.Rows);
        Assert.True(row.HasForbiddenTargetAuthority);
        Assert.Contains(MigrationValidationRules.Validate(row), item => item.Code == "migration_source_target_authority_not_allowed");
    }

    [Fact]
    public void Missing_stable_source_id_rejects_the_row_while_an_incompatible_contract_blocks_the_batch()
    {
        var rowPackage = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            "{\"sourceSequence\":1,\"recordType\":\"Product\",\"payload\":{\"sku\":\"A\",\"nameEnglish\":\"Product\"}}")));

        Assert.True(rowPackage.Succeeded, rowPackage.ErrorCode);
        Assert.Contains(MigrationValidationRules.Validate(Assert.Single(rowPackage.Rows)), item =>
            item.Code == "migration_required_field_missing" && item.Message.Contains("sourceRecordId", StringComparison.Ordinal));

        var missingDomainField = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            "{\"sourceSequence\":1,\"sourceRecordId\":\"product-2\",\"recordType\":\"Product\",\"payload\":{\"nameEnglish\":\"Product\"}}")));
        Assert.True(missingDomainField.Succeeded, missingDomainField.ErrorCode);
        Assert.Contains(MigrationValidationRules.Validate(Assert.Single(missingDomainField.Rows)), item =>
            item.Code == "migration_required_field_missing" && item.Message.Contains("sku", StringComparison.Ordinal));

        var incompatible = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(
            Package("{\"sourceSequence\":1,\"recordType\":\"Product\",\"payload\":{\"sku\":\"A\"}}")
                .Replace("migration-product-v1", "migration-product-v2", StringComparison.Ordinal)));
        Assert.False(incompatible.Succeeded);
        Assert.Equal("migration_package_domain_contract_incompatible", incompatible.ErrorCode);
        Assert.Empty(incompatible.Rows);
    }

    [Fact]
    public void Parser_blocks_invalid_date_shape_and_unknown_domain_fields()
    {
        var invalidDate = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            "{\"sourceSequence\":1,\"sourceRecordId\":\"ap-1\",\"recordType\":\"ApOpening\",\"payload\":{\"companyId\":\"11111111-1111-1111-1111-111111111111\",\"supplierId\":\"22222222-2222-2222-2222-222222222222\",\"sourceReference\":\"AP-1\",\"documentDate\":\"not-a-date\",\"openingDate\":\"2026-01-01\",\"amount\":10,\"currencyCode\":\"SAR\",\"dueDate\":\"2026-01-31\"}}")));
        var unknownField = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            "{\"sourceSequence\":1,\"sourceRecordId\":\"product-1\",\"recordType\":\"Product\",\"payload\":{\"sku\":\"A\",\"nameEnglish\":\"Product\",\"uncontracted\":true}}")));

        Assert.False(invalidDate.Succeeded);
        Assert.Equal("migration_package_invalid_json", invalidDate.ErrorCode);
        Assert.False(unknownField.Succeeded);
        Assert.Equal("migration_package_payload_invalid", unknownField.ErrorCode);
    }

    [Fact]
    public void Parser_rejects_reference_fields_outside_each_domain_contract()
    {
        var cases = new (string RecordType, string Payload)[]
        {
            ("Currency", "{\"sourceCurrencyCode\":\"USD\"}"),
            ("Tax", "{\"sourceCurrencyCode\":\"USD\"}"),
            ("PaymentTerm", "{\"effectiveDate\":\"2026-01-01\"}"),
            ("UnitOfMeasure", "{\"effectiveDate\":\"2026-01-01\"}"),
            ("PriceList", "{\"effectiveDate\":\"2026-01-01\"}"),
            ("ExchangeRate", "{\"code\":\"USD\"}")
        };

        foreach (var (recordType, payload) in cases)
        {
            var record = $$"""{"sourceSequence":1,"sourceRecordId":"reference-1","recordType":"{{recordType}}","payload":{{payload}}}""";
            var result = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(record)));

            Assert.False(result.Succeeded, recordType);
            Assert.Equal("migration_package_payload_invalid", result.ErrorCode);
        }
    }

    [Fact]
    public void Duplicate_business_keys_are_counted_by_the_shared_validation_policy()
    {
        var recordTypes = new[]
        {
            MigrationCanonicalRecordType.Product, MigrationCanonicalRecordType.Supplier, MigrationCanonicalRecordType.Customer,
            MigrationCanonicalRecordType.Organization, MigrationCanonicalRecordType.Currency, MigrationCanonicalRecordType.Tax, MigrationCanonicalRecordType.PaymentTerm,
            MigrationCanonicalRecordType.UnitOfMeasure, MigrationCanonicalRecordType.PriceList, MigrationCanonicalRecordType.ExchangeRate,
            MigrationCanonicalRecordType.InventoryOpening, MigrationCanonicalRecordType.GlOpening, MigrationCanonicalRecordType.ApOpening,
            MigrationCanonicalRecordType.ArOpening, MigrationCanonicalRecordType.CashBankOpening
        };
        var identities = recordTypes.SelectMany((type, index) => new[]
            {
                new KeyValuePair<int, MigrationBusinessIdentityResolution>(index * 2 + 1, MigrationBusinessIdentityResolution.Valid($"{type}:same-key")),
                new KeyValuePair<int, MigrationBusinessIdentityResolution>(index * 2 + 2, MigrationBusinessIdentityResolution.Valid($"{type}:same-key"))
            })
            .ToDictionary(item => item.Key, item => item.Value);

        var duplicateSequences = MigrationValidationService.DuplicateBusinessKeySequences(identities);

        Assert.Equal(recordTypes.Length * 2, duplicateSequences.Count);
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

    [Theory]
    [InlineData("exchangeRate", "3.75")]
    [InlineData("exchangeRateId", "\"11111111-1111-1111-1111-111111111111\"")]
    [InlineData("rateVersion", "2")]
    [InlineData("functionalAmount", "375")]
    [InlineData("historicalCarryingValue", "375")]
    public void S10_p20_canonical_source_fx_fields_are_rejected_at_the_package_boundary(string field, string value)
    {
        var result = MigrationCanonicalPackageParser.Parse(Encoding.UTF8.GetBytes(Package(
            $"{{\"sourceSequence\":1,\"recordType\":\"ArOpening\",\"payload\":{{\"companyId\":\"11111111-1111-1111-1111-111111111111\",\"customerId\":\"22222222-2222-2222-2222-222222222222\",\"sourceReference\":\"P20\",\"documentDate\":\"2026-01-10\",\"openingDate\":\"2026-01-15\",\"amount\":100,\"currencyCode\":\"USD\",\"dueDate\":\"2026-02-14\",\"{field}\":{value}}}}}")));

        Assert.True(result.Succeeded, result.ErrorCode);
        var row = Assert.Single(result.Rows);
        Assert.True(row.HasForbiddenMonetaryInput);
        Assert.Contains(MigrationValidationRules.Validate(row), item => item.Code == "migration_opening_source_monetary_fields_not_allowed");
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

    private static string Package(params string[] records)
    {
        var domainContracts = JsonSerializer.Serialize(
            MigrationDomainContractTestData.ForJsonRecords(records),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return $$"""
        {
          "packageVersion": "migration-package-v2",
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
          "domainContracts": {{domainContracts}},
          "records": [{{string.Join(",", records)}}]
        }
        """;
    }
}

internal static class MigrationDomainContractTestData
{
    public static MigrationCanonicalDomainContract[] For(params MigrationCanonicalRecordType[] recordTypes) =>
        recordTypes.Distinct().Select(Create).ToArray();

    public static MigrationCanonicalDomainContract[] ForJsonRecords(IEnumerable<string> records)
    {
        var recordTypes = new List<MigrationCanonicalRecordType>();
        foreach (var record in records)
        {
            using var document = JsonDocument.Parse(record);
            if (document.RootElement.TryGetProperty("recordType", out var typeElement)
                && Enum.TryParse<MigrationCanonicalRecordType>(typeElement.GetString(), true, out var type)
                && Enum.IsDefined(type))
                recordTypes.Add(type);
        }
        return For(recordTypes.ToArray());
    }

    public static MigrationCanonicalDomainContract[] ForRecordObjects(IEnumerable<object> records)
    {
        var recordTypes = records.Select(record => record.GetType().GetProperty("RecordType")?.GetValue(record)?.ToString())
            .Where(value => Enum.TryParse<MigrationCanonicalRecordType>(value, true, out _))
            .Select(value => Enum.Parse<MigrationCanonicalRecordType>(value!, true));
        return For(recordTypes.ToArray());
    }

    private static MigrationCanonicalDomainContract Create(MigrationCanonicalRecordType recordType) => new(
        recordType,
        MigrationCanonicalDomainContractCatalog.For(recordType).Version,
        "Source owner supplied",
        "Target owner supplied",
        "Source set supplied",
        DateTimeOffset.UnixEpoch,
        "Source scope supplied",
        "Extracted",
        "");
}
