using System.Reflection;
using System.Text.Json;
using MiniErp.Api;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationApiContractTests : IClassFixture<RestFoundationTests.ApiFactory>
{
    private readonly RestFoundationTests.ApiFactory factory;

    public MigrationApiContractTests(RestFoundationTests.ApiFactory factory)
    {
        this.factory = factory;
        factory.Reset();
    }

    [Fact]
    public void Mesp169_validation_and_dry_run_responses_are_labeled_and_never_claim_authoritative_import()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var validation = new MigrationValidationSummary(
            Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), "package-hash", "source-hash",
            0, 0, 0, 0, new Dictionary<string, int>(), [], DateTimeOffset.UtcNow)
        {
            OwnerActorId = Guid.NewGuid(),
            StageStatus = MigrationRunStatus.Validated,
            AttemptOutcome = MigrationAttemptOutcome.Succeeded,
            NextAction = "Review the validation findings before any separately authorized operation."
        };
        var dryRun = new MigrationDryRunPreview(
            Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "package-hash", "source-hash",
            0, 0, 0, 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(), 0, 0, [], DateTimeOffset.UtcNow);

        var validationResponse = Project("ToValidationResponse", validation);
        Assert.Equal("validation-only", validationResponse.GetProperty("outcome").GetString());
        Assert.False(validationResponse.GetProperty("authoritativeImport").GetBoolean());
        Assert.Equal(validation.OwnerActorId!.Value, validationResponse.GetProperty("ownerActorId").GetGuid());
        Assert.Equal(MigrationRunStatus.Validated.ToString(), validationResponse.GetProperty("stageStatus").GetString());
        Assert.Equal(MigrationAttemptOutcome.Succeeded.ToString(), validationResponse.GetProperty("attemptOutcome").GetString());
        Assert.True(validationResponse.GetProperty("nextAction").GetString()!.Contains("authorized", StringComparison.Ordinal));

        var dryRunResponse = Project("ToDryRunResponse", dryRun);
        Assert.Equal("dry-run", dryRunResponse.GetProperty("outcome").GetString());
        Assert.False(dryRunResponse.GetProperty("authoritativeImport").GetBoolean());

        static JsonElement Project(string methodName, object value)
        {
            var method = typeof(MigrationEndpoints).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(
                method!.Invoke(null, [value]),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            return document.RootElement.Clone();
        }
    }

    [Fact]
    public void Reconciliation_preview_projects_ordered_typed_dry_run_evidence()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var runId = Guid.NewGuid();
        var validationAttemptId = Guid.NewGuid();
        var productAcceptedId = Guid.NewGuid();
        var productDuplicateId = Guid.NewGuid();
        var productQuarantinedId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var glDebitId = Guid.NewGuid();
        var glCreditId = Guid.NewGuid();
        var apId = Guid.NewGuid();
        var arId = Guid.NewGuid();
        var cashBankId = Guid.NewGuid();
        MigrationValidationRecordResult[] validationRecords =
        [
            new(productAcceptedId, 1, MigrationCanonicalRecordType.Product, MigrationRecordDisposition.Accepted, []),
            new(productDuplicateId, 2, MigrationCanonicalRecordType.Product, MigrationRecordDisposition.Rejected, ["migration_duplicate_business_key"]),
            new(productQuarantinedId, 3, MigrationCanonicalRecordType.Product, MigrationRecordDisposition.Quarantined, ["migration_reference_authority_unavailable"]),
            new(supplierId, 4, MigrationCanonicalRecordType.Supplier, MigrationRecordDisposition.Accepted, []),
            new(inventoryId, 5, MigrationCanonicalRecordType.InventoryOpening, MigrationRecordDisposition.Accepted, []),
            new(glDebitId, 6, MigrationCanonicalRecordType.GlOpening, MigrationRecordDisposition.Accepted, []),
            new(glCreditId, 7, MigrationCanonicalRecordType.GlOpening, MigrationRecordDisposition.Accepted, []),
            new(apId, 8, MigrationCanonicalRecordType.ApOpening, MigrationRecordDisposition.Accepted, []),
            new(arId, 9, MigrationCanonicalRecordType.ArOpening, MigrationRecordDisposition.Accepted, []),
            new(cashBankId, 10, MigrationCanonicalRecordType.CashBankOpening, MigrationRecordDisposition.Accepted, [])
        ];
        MigrationPreviewRow[] plannedRows =
        [
            new(productAcceptedId, 1, MigrationCanonicalRecordType.Product, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null),
            new(productDuplicateId, 2, MigrationCanonicalRecordType.Product, MigrationRecordDisposition.Rejected, MigrationPlannedAction.Blocked, null),
            new(productQuarantinedId, 3, MigrationCanonicalRecordType.Product, MigrationRecordDisposition.Quarantined, MigrationPlannedAction.Blocked, null),
            new(inventoryId, 5, MigrationCanonicalRecordType.InventoryOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null),
            new(glDebitId, 6, MigrationCanonicalRecordType.GlOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null),
            new(glCreditId, 7, MigrationCanonicalRecordType.GlOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null),
            new(apId, 8, MigrationCanonicalRecordType.ApOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null),
            new(arId, 9, MigrationCanonicalRecordType.ArOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null),
            new(cashBankId, 10, MigrationCanonicalRecordType.CashBankOpening, MigrationRecordDisposition.Accepted, MigrationPlannedAction.Create, null)
        ];
        var controlTotals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["inventoryQuantity"] = 3m,
            ["inventoryValue"] = 37.5m,
            ["glDebit"] = 125m,
            ["glCredit"] = 100m,
            ["apAmount"] = 20m,
            ["arAmount"] = 30m,
            ["cashBankAmount"] = 40m
        };
        var validation = new MigrationValidationSummary(
            Guid.NewGuid(), tenantId, runId, validationAttemptId, "package-hash", "source-hash",
            10, 8, 1, 1, new Dictionary<string, int> { ["migration_duplicate_business_key"] = 1 },
            validationRecords, DateTimeOffset.UtcNow);
        var dryRun = new MigrationDryRunPreview(
            Guid.NewGuid(), tenantId, runId, Guid.NewGuid(), validationAttemptId, "package-hash", "source-hash",
            10, 8, 1, 1, validation.FindingCounts, controlTotals, 0, 2, plannedRows, DateTimeOffset.UtcNow);

        var evidence = MigrationReconciliationService.ProjectDryRunEvidence(dryRun, validation);
        Assert.NotNull(evidence);
        var projected = evidence.Value;
        Assert.Equal(
            controlTotals.Append(new KeyValuePair<string, decimal>("glBalanceDifference", 25m))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new MigrationReconciliationControlTotal(item.Key, item.Value)),
            projected.Controls);
        Assert.Equal(10, projected.Counts.SourceRecordCount);
        Assert.Equal(8, projected.Counts.AcceptedCount);
        Assert.Equal(1, projected.Counts.RejectedCount);
        Assert.Equal(1, projected.Counts.QuarantinedCount);
        Assert.Equal(1, projected.Counts.DuplicateCount);
        var expectedByRecordType = new Dictionary<MigrationCanonicalRecordType, (int Source, int Accepted, int Rejected, int Quarantined, int Duplicate)>
        {
            [MigrationCanonicalRecordType.Product] = (3, 1, 1, 1, 1),
            [MigrationCanonicalRecordType.Supplier] = (1, 1, 0, 0, 0),
            [MigrationCanonicalRecordType.InventoryOpening] = (1, 1, 0, 0, 0),
            [MigrationCanonicalRecordType.GlOpening] = (2, 2, 0, 0, 0),
            [MigrationCanonicalRecordType.ApOpening] = (1, 1, 0, 0, 0),
            [MigrationCanonicalRecordType.ArOpening] = (1, 1, 0, 0, 0),
            [MigrationCanonicalRecordType.CashBankOpening] = (1, 1, 0, 0, 0)
        };
        Assert.Equal(expectedByRecordType.Keys, projected.Counts.ByRecordType.Select(item => item.RecordType));
        foreach (var recordCount in projected.Counts.ByRecordType)
        {
            var expected = expectedByRecordType[recordCount.RecordType];
            Assert.Equal(expected, (recordCount.SourceRecordCount, recordCount.AcceptedCount, recordCount.RejectedCount, recordCount.QuarantinedCount, recordCount.DuplicateCount));
        }

        var product = Assert.Single(projected.Counts.ByRecordType, item => item.RecordType == MigrationCanonicalRecordType.Product);
        Assert.Equal((3, 1, 1, 1, 1), (product.SourceRecordCount, product.AcceptedCount, product.RejectedCount, product.QuarantinedCount, product.DuplicateCount));
        Assert.Equal(
            new[] { MigrationPlannedAction.Create, MigrationPlannedAction.MatchReference, MigrationPlannedAction.Skip, MigrationPlannedAction.Blocked },
            product.PlannedActionCounts.Select(item => item.Action));
        Assert.Equal(new[] { 1, 0, 0, 2 }, product.PlannedActionCounts.Select(item => item.Count));
        Assert.Equal(MigrationReconciliationTargetBasisStatus.DryRunPlan, product.TargetBasisStatus);

        var supplier = Assert.Single(projected.Counts.ByRecordType, item => item.RecordType == MigrationCanonicalRecordType.Supplier);
        Assert.Equal(1, supplier.SourceRecordCount);
        Assert.Equal(MigrationReconciliationTargetBasisStatus.NotYetAvailable, supplier.TargetBasisStatus);
        Assert.Empty(supplier.PlannedActionCounts);

        var preview = new MigrationNonAuthoritativePreview(
            runId, tenantId, "reconciliation-preview", 8, 1, 0, controlTotals, 2,
            AuthoritativeImport: false, ApprovalCreated: false, ReadinessCreated: false, RunStateChanged: false, Rows: plannedRows)
        {
            ReconciliationControls = projected.Controls,
            ReconciliationCounts = projected.Counts
        };
        var method = typeof(MigrationEndpoints).GetMethod("ToPreviewResponse", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(
            method!.Invoke(null, [preview]), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var response = responseDocument.RootElement;
        Assert.Equal("reconciliation-preview", response.GetProperty("outcome").GetString());
        Assert.Equal(25m, Assert.Single(response.GetProperty("reconciliationControls").EnumerateArray(), item => item.GetProperty("name").GetString() == "glBalanceDifference").GetProperty("amount").GetDecimal());
        Assert.Equal(10, response.GetProperty("reconciliationCounts").GetProperty("sourceRecordCount").GetInt32());
        Assert.Equal("NotYetAvailable", Assert.Single(
            response.GetProperty("reconciliationCounts").GetProperty("byRecordType").EnumerateArray(),
            item => item.GetProperty("recordType").GetString() == "Supplier").GetProperty("targetBasisStatus").GetString());
    }

    [Fact]
    public async Task Reconciliation_preview_openapi_exposes_the_typed_evidence_contract()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var operation = root.GetProperty("paths")
            .GetProperty("/api/v1/migrations/{runId}/reconciliation-preview")
            .GetProperty("get");
        var responseSchema = operation.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var responseProperties = ResolveSchema(responseSchema, root).GetProperty("properties");
        Assert.True(responseProperties.TryGetProperty("reconciliationControls", out var controlsSchema));
        Assert.True(responseProperties.TryGetProperty("reconciliationCounts", out var countsSchema));

        var controlProperties = ResolveSchema(controlsSchema.GetProperty("items"), root).GetProperty("properties");
        Assert.True(controlProperties.TryGetProperty("name", out _));
        Assert.True(controlProperties.TryGetProperty("amount", out _));

        var resolvedCountsSchema = ResolveSchema(countsSchema, root);
        Assert.True(resolvedCountsSchema.TryGetProperty("properties", out var countsProperties), resolvedCountsSchema.GetRawText());
        foreach (var name in new[] { "sourceRecordCount", "acceptedCount", "rejectedCount", "quarantinedCount", "duplicateCount", "byRecordType" })
            Assert.True(countsProperties.TryGetProperty(name, out _), $"OpenAPI reconciliation count property missing: {name}");
        var recordProperties = ResolveSchema(countsProperties.GetProperty("byRecordType").GetProperty("items"), root).GetProperty("properties");
        foreach (var name in new[] { "recordType", "sourceRecordCount", "acceptedCount", "rejectedCount", "quarantinedCount", "duplicateCount", "targetBasisStatus", "plannedActionCounts" })
            Assert.True(recordProperties.TryGetProperty(name, out _), $"OpenAPI record count property missing: {name}");
        var actionProperties = ResolveSchema(recordProperties.GetProperty("plannedActionCounts").GetProperty("items"), root).GetProperty("properties");
        Assert.True(actionProperties.TryGetProperty("action", out _));
        Assert.True(actionProperties.TryGetProperty("count", out _));

        static JsonElement ResolveSchema(JsonElement schema, JsonElement documentRoot)
        {
            if (schema.TryGetProperty("$ref", out var reference))
                return ResolveSchema(
                    documentRoot.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1]),
                    documentRoot);
            if (schema.TryGetProperty("anyOf", out var alternatives)
                || schema.TryGetProperty("oneOf", out alternatives))
                foreach (var alternative in alternatives.EnumerateArray())
                {
                    var isNullSchema = alternative.ValueKind == JsonValueKind.Null
                        || alternative.ValueKind == JsonValueKind.Object
                            && alternative.TryGetProperty("type", out var type)
                            && type.GetString() == "null";
                    if (!isNullSchema)
                        return ResolveSchema(alternative, documentRoot);
                }
            return schema;
        }
    }

    [Fact]
    public void Execution_response_exposes_cash_bank_reconciliation_and_preserves_existing_fields()
    {
        var reconciliation = new MigrationCashBankEconomicReconciliationRecord(
            Guid.NewGuid(), 1, "reconciled", null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CASH-OPEN-001", 100m, 100m, "SAR", new DateOnly(2026, 1, 15), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var result = new MigrationExecutionResult(
            Guid.NewGuid(), new TenantId(Guid.NewGuid()), Guid.NewGuid(), MigrationExecutionService.FingerprintVersion,
            "fingerprint", MigrationRunStatus.Completed, MigrationAttemptOutcome.Succeeded, "migration_execution_completed",
            [], [], [], [new MigrationEconomicReconciliationRecord(Guid.NewGuid(), 1, "reconciled", null, true, true, true, 1m, 1m, 100m, 100m, 0m, 100m, "SAR", DateTimeOffset.UtcNow)],
            [new MigrationArEconomicReconciliationRecord(Guid.NewGuid(), 2, "reconciled", null, Guid.NewGuid(), Guid.NewGuid(), "AR-1", 10m, 10m, 10m, 10m, 0m, "SAR", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow)],
            [new MigrationApEconomicReconciliationRecord(Guid.NewGuid(), 3, "reconciled", null, Guid.NewGuid(), Guid.NewGuid(), "AP-1", 10m, 10m, 10m, 10m, 0m, "SAR", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow)],
            [reconciliation]);

        var method = typeof(MigrationEndpoints).GetMethod("ToExecutionResponse", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var projected = method!.Invoke(null, [result]);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(projected, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = document.RootElement;

        var cash = root.GetProperty("cashBankEconomicReconciliations");
        Assert.Equal("reconciled", cash[0].GetProperty("status").GetString());
        Assert.Equal(100m, cash[0].GetProperty("canonicalAmount").GetDecimal());
        Assert.Equal("SAR", cash[0].GetProperty("currencyCode").GetString());
        Assert.True(root.TryGetProperty("economicReconciliations", out _));
        Assert.True(root.TryGetProperty("arEconomicReconciliations", out _));
        Assert.True(root.TryGetProperty("apEconomicReconciliations", out _));
        Assert.True(root.GetProperty("zeroEconomicEffects").GetBoolean());
    }
}
