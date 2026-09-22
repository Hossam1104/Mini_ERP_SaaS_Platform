#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;

namespace MiniErp.App.Modules.Migration;

internal static class MigrationValidationResultPolicy
{
    public static MigrationValidationSummary BuildSummary(
        TenantContext tenant,
        Guid runId,
        Guid attemptId,
        string packageHash,
        string sourceSnapshotHash,
        IReadOnlyList<MigrationStagedRecord> staged,
        IReadOnlyDictionary<int, (MigrationRecordDisposition Disposition, List<string> Codes)> results,
        IReadOnlyList<MigrationValidationFinding> findings,
        DateTimeOffset completedAt)
    {
        var records = staged
            .OrderBy(item => item.SourceSequence)
            .Select(item => new MigrationValidationRecordResult(
                item.StagedRecordId,
                item.SourceSequence,
                item.RecordType,
                results[item.SourceSequence].Disposition,
                results[item.SourceSequence].Codes.ToArray()))
            .ToArray();
        return new(
            Guid.NewGuid(),
            tenant.TenantId,
            runId,
            attemptId,
            packageHash,
            sourceSnapshotHash,
            records.Length,
            records.Count(item => item.Disposition == MigrationRecordDisposition.Accepted),
            records.Count(item => item.Disposition == MigrationRecordDisposition.Rejected),
            records.Count(item => item.Disposition == MigrationRecordDisposition.Quarantined),
            findings
                .GroupBy(item => item.Category.ToString(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            records,
            completedAt);
    }

    public static void AddFinding(
        ICollection<MigrationValidationFinding> findings,
        ICollection<string> codes,
        MigrationStagedRecord row,
        MigrationAttemptRecord attempt,
        MigrationFindingCategory category,
        MigrationFindingSeverity severity,
        string code,
        string message,
        string? referenceId = null)
    {
        codes.Add(code);
        findings.Add(new(
            Guid.NewGuid(),
            row.TenantId,
            row.RunId,
            attempt.AttemptId,
            row.StagedRecordId,
            category,
            severity,
            true,
            code,
            message,
            referenceId,
            DateTimeOffset.UtcNow));
    }

    public static void AddGlBalanceFindings(
        IReadOnlyList<MigrationParsedCanonicalRow> rows,
        IReadOnlyList<MigrationStagedRecord> staged,
        MigrationAttemptRecord attempt,
        IDictionary<int, (MigrationRecordDisposition Disposition, List<string> Codes)> results,
        ICollection<MigrationValidationFinding> findings)
    {
        var groups = rows
            .Where(item => item.Payload is MigrationGlOpeningPayload gl
                && gl.CompanyId is not null
                && !string.IsNullOrWhiteSpace(gl.CurrencyCode)
                && gl.OpeningDate is not null
                && gl.Debit is not null
                && gl.Credit is not null)
            .Select(item => (Row: item, Payload: (MigrationGlOpeningPayload)item.Payload))
            .GroupBy(item => (
                item.Payload.CompanyId!.Value,
                Currency: item.Payload.CurrencyCode!.Trim().ToUpperInvariant(),
                item.Payload.OpeningDate!.Value));

        foreach (var group in groups)
        {
            if (Math.Abs(group.Sum(item => item.Payload.Debit!.Value) - group.Sum(item => item.Payload.Credit!.Value)) <= 0.00000001m)
                continue;

            foreach (var item in group)
                results[item.Row.SourceSequence] = (
                    MigrationRecordDisposition.Rejected,
                    [.. results[item.Row.SourceSequence].Codes, "migration_gl_opening_imbalanced"]);

            var first = staged.Single(item => item.SourceSequence == group.First().Row.SourceSequence);
            findings.Add(new(
                Guid.NewGuid(),
                first.TenantId,
                first.RunId,
                attempt.AttemptId,
                null,
                MigrationFindingCategory.FinancialBalance,
                MigrationFindingSeverity.Error,
                true,
                "migration_gl_opening_imbalanced",
                "GL opening debit and credit control totals must balance per company, currency, and opening date.",
                null,
                DateTimeOffset.UtcNow));
        }
    }

    public static void AddGlDuplicateFindings(
        IReadOnlyList<MigrationParsedCanonicalRow> rows,
        IReadOnlyList<MigrationStagedRecord> staged,
        MigrationAttemptRecord attempt,
        IDictionary<int, (MigrationRecordDisposition Disposition, List<string> Codes)> results,
        ICollection<MigrationValidationFinding> findings)
    {
        var groups = rows
            .Where(item => item.Payload is MigrationGlOpeningPayload gl
                && gl.CompanyId is not null && gl.AccountId is not null
                && gl.CurrencyCode is not null && gl.OpeningDate is not null)
            .GroupBy(item =>
            {
                var gl = (MigrationGlOpeningPayload)item.Payload;
                return (gl.CompanyId!.Value, Currency: gl.CurrencyCode!.Trim().ToUpperInvariant(), gl.OpeningDate!.Value);
            });

        foreach (var group in groups)
        {
            AddDuplicate(group.GroupBy(item => ((MigrationGlOpeningPayload)item.Payload).AccountId), "migration_gl_opening_duplicate_account", "A GL opening group cannot contain more than one active source line for an account.");
            AddDuplicate(group.GroupBy(item => ((MigrationGlOpeningPayload)item.Payload).SourceLineReference?.Trim(), StringComparer.Ordinal), "migration_gl_opening_duplicate_source_line_reference", "A GL opening group cannot contain an ambiguous source-line reference.");

            void AddDuplicate<T>(IEnumerable<IGrouping<T, MigrationParsedCanonicalRow>> candidates, string code, string message)
            {
                foreach (var duplicate in candidates.Where(candidate => candidate.Count() > 1))
                {
                    foreach (var row in duplicate)
                    {
                        results[row.SourceSequence] = (MigrationRecordDisposition.Rejected, [.. results[row.SourceSequence].Codes, code]);
                        var stagedRow = staged.Single(item => item.SourceSequence == row.SourceSequence);
                        findings.Add(new(Guid.NewGuid(), stagedRow.TenantId, stagedRow.RunId, attempt.AttemptId, stagedRow.StagedRecordId, MigrationFindingCategory.Duplicate, MigrationFindingSeverity.Error, true, code, message, null, DateTimeOffset.UtcNow));
                    }
                }
            }
        }
    }
}

internal static class MigrationDryRunPreviewPolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static MigrationDryRunPreview Build(
        TenantId tenantId,
        Guid runId,
        Guid attemptId,
        MigrationValidationSummary validation,
        IReadOnlyList<MigrationStagedRecord> staged,
        DateTimeOffset completedAt)
    {
        var rows = validation.Records
            .OrderBy(item => item.SourceSequence)
            .Select(item => new MigrationPreviewRow(
                item.StagedRecordId,
                item.SourceSequence,
                item.RecordType,
                item.Disposition,
                item.Disposition == MigrationRecordDisposition.Accepted ? PlannedAction(item.RecordType) : MigrationPlannedAction.Blocked,
                item.Disposition == MigrationRecordDisposition.Accepted ? "would be evaluated by the owning module" : null))
            .ToArray();

        return new(
            Guid.NewGuid(),
            tenantId,
            runId,
            attemptId,
            validation.AttemptId,
            validation.PackageHash,
            validation.SourceSnapshotHash,
            validation.TotalStagedRecords,
            validation.AcceptedCount,
            validation.RejectedCount,
            validation.QuarantinedCount,
            validation.FindingCounts,
            ComputeControlTotals(staged),
            validation.FindingCounts
                .Where(item => item.Key is nameof(MigrationFindingCategory.Reference) or nameof(MigrationFindingCategory.Currency) or nameof(MigrationFindingCategory.Uom) or nameof(MigrationFindingCategory.Scope))
                .Sum(item => item.Value),
            rows.Count(item => item.PlannedAction == MigrationPlannedAction.Blocked),
            rows,
            completedAt);
    }

    private static IReadOnlyDictionary<string, decimal> ComputeControlTotals(IReadOnlyList<MigrationStagedRecord> staged)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var item in staged)
        {
            try
            {
                switch (item.RecordType)
                {
                    case MigrationCanonicalRecordType.InventoryOpening:
                        var inventory = JsonSerializer.Deserialize<MigrationInventoryOpeningPayload>(item.CanonicalPayload, JsonOptions);
                        totals["inventoryQuantity"] = totals.GetValueOrDefault("inventoryQuantity") + (inventory?.Quantity ?? 0m);
                        totals["inventoryValue"] = totals.GetValueOrDefault("inventoryValue") + (inventory?.Quantity ?? 0m) * (inventory?.UnitCost ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.GlOpening:
                        var gl = JsonSerializer.Deserialize<MigrationGlOpeningPayload>(item.CanonicalPayload, JsonOptions);
                        totals["glDebit"] = totals.GetValueOrDefault("glDebit") + (gl?.Debit ?? 0m);
                        totals["glCredit"] = totals.GetValueOrDefault("glCredit") + (gl?.Credit ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.ApOpening:
                        totals["apAmount"] = totals.GetValueOrDefault("apAmount") + (JsonSerializer.Deserialize<MigrationApOpeningPayload>(item.CanonicalPayload, JsonOptions)?.Amount ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.ArOpening:
                        totals["arAmount"] = totals.GetValueOrDefault("arAmount") + (JsonSerializer.Deserialize<MigrationArOpeningPayload>(item.CanonicalPayload, JsonOptions)?.Amount ?? 0m);
                        break;
                    case MigrationCanonicalRecordType.CashBankOpening:
                        totals["cashBankAmount"] = totals.GetValueOrDefault("cashBankAmount") + (JsonSerializer.Deserialize<MigrationCashBankOpeningPayload>(item.CanonicalPayload, JsonOptions)?.Amount ?? 0m);
                        break;
                }
            }
            catch (JsonException)
            {
                // The canonical payload was typed before staging; a defensive skip cannot create an authoritative effect.
            }
        }
        return totals;
    }

    private static MigrationPlannedAction PlannedAction(MigrationCanonicalRecordType type) =>
        type is MigrationCanonicalRecordType.Product or MigrationCanonicalRecordType.Supplier or MigrationCanonicalRecordType.Customer or MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.GlOpening or MigrationCanonicalRecordType.ArOpening or MigrationCanonicalRecordType.ApOpening or MigrationCanonicalRecordType.CashBankOpening
            ? MigrationPlannedAction.Create
            : MigrationPlannedAction.MatchReference;
}

#pragma warning restore CS1591
