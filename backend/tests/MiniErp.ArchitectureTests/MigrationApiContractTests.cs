using System.Reflection;
using System.Text.Json;
using MiniErp.Api;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Migration;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MigrationApiContractTests
{
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
