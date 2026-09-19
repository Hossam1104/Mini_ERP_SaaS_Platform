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
