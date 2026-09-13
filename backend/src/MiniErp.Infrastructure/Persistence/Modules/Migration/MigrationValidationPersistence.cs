#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Migration;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

internal sealed partial class MigrationPersistence
{
    public async Task<MigrationIntakeRecord?> FindIntakeAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var intake = await db.Intakes.SingleOrDefaultAsync(item => item.RunId == runId, cancellationToken);
        if (intake is null)
            return null;
        var run = await db.Runs.SingleOrDefaultAsync(item => item.RunId == runId, cancellationToken);
        return run is null ? null : ToRecord(intake, run);
    }

    public async Task<MigrationPersistenceResult<MigrationStagingResult>> StagePackageAsync(
        TenantContext tenantContext,
        StageMigrationPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);
        if (command.RunId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.PackageHash)
            || command.Records.Count == 0
            || command.Records.Any(item => item.TenantId != tenantContext.TenantId
                || item.RunId != command.RunId
                || item.PackageHash != command.PackageHash
                || item.SourceObjectId != command.SourceObjectId
                || item.SourceSnapshotHash != command.SourceSnapshotHash
                || item.SourceSequence < 1
                || item.CanonicalPayload.Length > 2_000_000)
            || command.Records.Select(item => item.SourceSequence).Distinct().Count() != command.Records.Count)
        {
            return MigrationPersistenceResult<MigrationStagingResult>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                "migration_staging_contract_invalid");
        }

        await using var db = CreateContext(tenantContext);
        var existing = await db.StagedRecords
            .Where(item => item.RunId == command.RunId)
            .OrderBy(item => item.SourceSequence)
            .ToArrayAsync(cancellationToken);
        if (existing.Length > 0)
        {
            return SameStage(existing, command)
                ? MigrationPersistenceResult<MigrationStagingResult>.Replay(
                    new MigrationStagingResult(command.PackageHash, existing.Select(ToRecord).ToArray()))
                : MigrationPersistenceResult<MigrationStagingResult>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_staging_snapshot_conflict");
        }

        db.StagedRecords.AddRange(command.Records.Select(item => new MigrationStagedRecordEntity(item, command.PackageHash)));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationStagingResult>.Success(
                new MigrationStagingResult(command.PackageHash, command.Records.OrderBy(item => item.SourceSequence).ToArray()));
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await using var fresh = CreateContext(tenantContext);
            var winner = await fresh.StagedRecords
                .Where(item => item.RunId == command.RunId)
                .OrderBy(item => item.SourceSequence)
                .ToArrayAsync(cancellationToken);
            return SameStage(winner, command)
                ? MigrationPersistenceResult<MigrationStagingResult>.Replay(
                    new MigrationStagingResult(command.PackageHash, winner.Select(ToRecord).ToArray()))
                : MigrationPersistenceResult<MigrationStagingResult>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_staging_snapshot_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationStagingResult>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_staging_outcome_unknown");
        }
    }

    public async Task<MigrationPersistenceResult<MigrationValidationSummary>> SaveValidationAsync(
        TenantContext tenantContext,
        SaveMigrationValidationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);
        var summary = command.Summary;
        if (summary.TenantId != tenantContext.TenantId
            || summary.RunId == Guid.Empty
            || summary.AttemptId == Guid.Empty
            || summary.Records.Count != summary.TotalStagedRecords
            || summary.TotalStagedRecords != summary.AcceptedCount + summary.RejectedCount + summary.QuarantinedCount
            || summary.Records.Select(item => item.StagedRecordId).Distinct().Count() != summary.Records.Count
            || summary.Records.Select(item => item.SourceSequence).Distinct().Count() != summary.Records.Count
            || command.Findings.Any(item => item.TenantId != tenantContext.TenantId || item.RunId != summary.RunId || item.AttemptId != summary.AttemptId))
        {
            return MigrationPersistenceResult<MigrationValidationSummary>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                "migration_validation_contract_invalid");
        }

        await using var db = CreateContext(tenantContext);
        var existing = await db.ValidationResults
            .SingleOrDefaultAsync(item => item.RunId == summary.RunId && item.AttemptId == summary.AttemptId, cancellationToken);
        if (existing is not null)
        {
            var replay = await ReadValidationAsync(db, existing, cancellationToken);
            return SameValidation(replay, summary)
                ? MigrationPersistenceResult<MigrationValidationSummary>.Replay(replay)
                : MigrationPersistenceResult<MigrationValidationSummary>.Denied(
                    MigrationPersistenceOutcome.Conflict,
                    "migration_validation_snapshot_conflict");
        }

        var stagedIds = await db.StagedRecords
            .Where(item => item.RunId == summary.RunId)
            .Select(item => item.StagedRecordId)
            .ToArrayAsync(cancellationToken);
        if (stagedIds.Length != summary.Records.Count || summary.Records.Any(item => !stagedIds.Contains(item.StagedRecordId)))
        {
            return MigrationPersistenceResult<MigrationValidationSummary>.Denied(
                MigrationPersistenceOutcome.InvalidReference,
                "migration_validation_staged_records_mismatch");
        }

        db.ValidationResults.Add(new MigrationValidationResultEntity(summary));
        db.ValidationRecords.AddRange(summary.Records.Select(item => new MigrationValidationRecordEntity(item, summary.TenantId, summary.RunId, summary.AttemptId)));
        db.ValidationFindings.AddRange(command.Findings.Select(item => new MigrationValidationFindingEntity(item)));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationValidationSummary>.Success(summary);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await using var fresh = CreateContext(tenantContext);
            var winner = await fresh.ValidationResults
                .SingleOrDefaultAsync(item => item.RunId == summary.RunId && item.AttemptId == summary.AttemptId, cancellationToken);
            if (winner is null)
                return MigrationPersistenceResult<MigrationValidationSummary>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_validation_outcome_unknown");
            var replay = await ReadValidationAsync(fresh, winner, cancellationToken);
            return SameValidation(replay, summary)
                ? MigrationPersistenceResult<MigrationValidationSummary>.Replay(replay)
                : MigrationPersistenceResult<MigrationValidationSummary>.Denied(MigrationPersistenceOutcome.Conflict, "migration_validation_snapshot_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationValidationSummary>.Denied(
                MigrationPersistenceOutcome.UnknownOutcome,
                "migration_validation_outcome_unknown");
        }
    }

    public async Task<MigrationValidationSummary?> FindValidationAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ValidationResults.SingleOrDefaultAsync(item => item.RunId == runId && item.AttemptId == attemptId, cancellationToken);
        return entity is null ? null : await ReadValidationAsync(db, entity, cancellationToken);
    }

    public async Task<MigrationValidationSummary?> FindLatestValidationAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ValidationResults
            .Where(item => item.RunId == runId)
            .OrderByDescending(item => item.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : await ReadValidationAsync(db, entity, cancellationToken);
    }

    public async Task<IReadOnlyList<MigrationValidationFinding>> ListFindingsAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid? attemptId = null,
        int offset = 0,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        offset = Math.Max(0, offset);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        var query = db.ValidationFindings.Where(item => item.RunId == runId);
        if (attemptId is { } selectedAttempt)
            query = query.Where(item => item.AttemptId == selectedAttempt);
        var findings = await query.OrderBy(item => item.CreatedAt).ThenBy(item => item.FindingId).Skip(offset).Take(pageSize).ToArrayAsync(cancellationToken);
        return findings.Select(ToRecord).ToArray();
    }

    public async Task<IReadOnlyList<MigrationStagedRecord>> ListStagedRecordsAsync(
        TenantContext tenantContext,
        Guid runId,
        int offset = 0,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.StagedRecords
            .Where(item => item.RunId == runId)
            .OrderBy(item => item.SourceSequence)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(pageSize, 1, 1000))
            .ToArrayAsync(cancellationToken);
        return records.Select(ToRecord).ToArray();
    }

    public async Task<MigrationPersistenceResult<MigrationDryRunPreview>> SaveDryRunAsync(
        TenantContext tenantContext,
        SaveMigrationDryRunCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);
        var preview = command.Preview;
        if (preview.TenantId != tenantContext.TenantId
            || preview.RunId == Guid.Empty
            || preview.AttemptId == Guid.Empty
            || preview.ValidationAttemptId == Guid.Empty
            || preview.Rows.Count != preview.TotalStagedRecords
            || preview.TotalStagedRecords != preview.AcceptedCount + preview.RejectedCount + preview.QuarantinedCount
            || preview.Rows.Select(item => item.StagedRecordId).Distinct().Count() != preview.Rows.Count
            || preview.Rows.Select(item => item.SourceSequence).Distinct().Count() != preview.Rows.Count)
        {
            return MigrationPersistenceResult<MigrationDryRunPreview>.Denied(MigrationPersistenceOutcome.InvalidReference, "migration_dry_run_contract_invalid");
        }

        await using var db = CreateContext(tenantContext);
        var existing = await db.DryRunPreviews.SingleOrDefaultAsync(item => item.RunId == preview.RunId && item.AttemptId == preview.AttemptId, cancellationToken);
        if (existing is not null)
        {
            var replay = await ReadDryRunAsync(db, existing, cancellationToken);
            return SameDryRun(replay, preview)
                ? MigrationPersistenceResult<MigrationDryRunPreview>.Replay(replay)
                : MigrationPersistenceResult<MigrationDryRunPreview>.Denied(MigrationPersistenceOutcome.Conflict, "migration_dry_run_snapshot_conflict");
        }

        var stagedIds = await db.StagedRecords.Where(item => item.RunId == preview.RunId).Select(item => item.StagedRecordId).ToArrayAsync(cancellationToken);
        if (preview.Rows.Any(item => !stagedIds.Contains(item.StagedRecordId)))
        {
            return MigrationPersistenceResult<MigrationDryRunPreview>.Denied(MigrationPersistenceOutcome.InvalidReference, "migration_dry_run_staged_records_mismatch");
        }

        db.DryRunPreviews.Add(new MigrationDryRunPreviewEntity(preview));
        db.DryRunPreviewRows.AddRange(preview.Rows.Select(item => new MigrationDryRunPreviewRowEntity(preview, item)));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MigrationPersistenceResult<MigrationDryRunPreview>.Success(preview);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await using var fresh = CreateContext(tenantContext);
            var winner = await fresh.DryRunPreviews.SingleOrDefaultAsync(item => item.RunId == preview.RunId && item.AttemptId == preview.AttemptId, cancellationToken);
            if (winner is null)
                return MigrationPersistenceResult<MigrationDryRunPreview>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_dry_run_outcome_unknown");
            var replay = await ReadDryRunAsync(fresh, winner, cancellationToken);
            return SameDryRun(replay, preview)
                ? MigrationPersistenceResult<MigrationDryRunPreview>.Replay(replay)
                : MigrationPersistenceResult<MigrationDryRunPreview>.Denied(MigrationPersistenceOutcome.Conflict, "migration_dry_run_snapshot_conflict");
        }
        catch (DbUpdateException)
        {
            return MigrationPersistenceResult<MigrationDryRunPreview>.Denied(MigrationPersistenceOutcome.UnknownOutcome, "migration_dry_run_outcome_unknown");
        }
    }

    public async Task<MigrationDryRunPreview?> FindDryRunAsync(TenantContext tenantContext, Guid runId, Guid attemptId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.DryRunPreviews.SingleOrDefaultAsync(item => item.RunId == runId && item.AttemptId == attemptId, cancellationToken);
        return entity is null ? null : await ReadDryRunAsync(db, entity, cancellationToken);
    }

    public async Task<MigrationDryRunPreview?> FindLatestDryRunAsync(TenantContext tenantContext, Guid runId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.DryRunPreviews.Where(item => item.RunId == runId).OrderByDescending(item => item.CompletedAt).FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : await ReadDryRunAsync(db, entity, cancellationToken);
    }

    private async Task<MigrationValidationSummary> ReadValidationAsync(MigrationDbContext db, MigrationValidationResultEntity entity, CancellationToken cancellationToken)
    {
        var records = await db.ValidationRecords
            .Where(item => item.RunId == entity.RunId && item.AttemptId == entity.AttemptId)
            .OrderBy(item => item.SourceSequence)
            .ToArrayAsync(cancellationToken);
        return new(
            entity.ValidationResultId,
            entity.TenantId,
            entity.RunId,
            entity.AttemptId,
            entity.PackageHash,
            entity.SourceSnapshotHash,
            entity.TotalStagedRecords,
            entity.AcceptedCount,
            entity.RejectedCount,
            entity.QuarantinedCount,
            ReadDictionary<int>(entity.FindingCountsJson),
            records.Select(item => new MigrationValidationRecordResult(
                item.StagedRecordId,
                item.SourceSequence,
                item.RecordType,
                item.Disposition,
                ReadList(item.FindingCodesJson))).ToArray(),
            entity.CompletedAt);
    }

    private async Task<MigrationDryRunPreview> ReadDryRunAsync(MigrationDbContext db, MigrationDryRunPreviewEntity entity, CancellationToken cancellationToken)
    {
        var rows = await db.DryRunPreviewRows
            .Where(item => item.PreviewId == entity.PreviewId)
            .OrderBy(item => item.SourceSequence)
            .ToArrayAsync(cancellationToken);
        return new(
            entity.PreviewId,
            entity.TenantId,
            entity.RunId,
            entity.AttemptId,
            entity.ValidationAttemptId,
            entity.PackageHash,
            entity.SourceSnapshotHash,
            entity.TotalStagedRecords,
            entity.AcceptedCount,
            entity.RejectedCount,
            entity.QuarantinedCount,
            ReadDictionary<int>(entity.FindingCountsJson),
            ReadDictionary<decimal>(entity.ControlTotalsJson),
            entity.UnresolvedDependencyCount,
            entity.ExceptionCount,
            rows.Select(item => new MigrationPreviewRow(item.StagedRecordId, item.SourceSequence, item.RecordType, item.Disposition, item.PlannedAction, item.Projection)).ToArray(),
            entity.CompletedAt);
    }

    private static bool SameStage(MigrationStagedRecordEntity[] existing, StageMigrationPackageCommand command) =>
        existing.Length == command.Records.Count
        && existing.Zip(command.Records.OrderBy(item => item.SourceSequence), (stored, requested) =>
            stored.SourceSequence == requested.SourceSequence
            && stored.PayloadHash == requested.PayloadHash
            && stored.PackageHash == requested.PackageHash
            && stored.SourceSnapshotHash == requested.SourceSnapshotHash).All(item => item);

    private static bool SameValidation(MigrationValidationSummary stored, MigrationValidationSummary requested) =>
        stored.TenantId == requested.TenantId
        && stored.RunId == requested.RunId
        && stored.AttemptId == requested.AttemptId
        && stored.PackageHash == requested.PackageHash
        && stored.SourceSnapshotHash == requested.SourceSnapshotHash
        && stored.TotalStagedRecords == requested.TotalStagedRecords
        && stored.AcceptedCount == requested.AcceptedCount
        && stored.RejectedCount == requested.RejectedCount
        && stored.QuarantinedCount == requested.QuarantinedCount
        && SameDictionary(stored.FindingCounts, requested.FindingCounts)
        && stored.Records.Count == requested.Records.Count
        && stored.Records.Zip(requested.Records).All(item => SameValidationRecord(item.First, item.Second));

    private static bool SameValidationRecord(MigrationValidationRecordResult left, MigrationValidationRecordResult right) =>
        left.StagedRecordId == right.StagedRecordId
        && left.SourceSequence == right.SourceSequence
        && left.RecordType == right.RecordType
        && left.Disposition == right.Disposition
        && left.FindingCodes.SequenceEqual(right.FindingCodes, StringComparer.Ordinal);

    private static bool SameDryRun(MigrationDryRunPreview stored, MigrationDryRunPreview requested) =>
        stored.TenantId == requested.TenantId
        && stored.RunId == requested.RunId
        && stored.AttemptId == requested.AttemptId
        && stored.ValidationAttemptId == requested.ValidationAttemptId
        && stored.PackageHash == requested.PackageHash
        && stored.SourceSnapshotHash == requested.SourceSnapshotHash
        && stored.TotalStagedRecords == requested.TotalStagedRecords
        && stored.AcceptedCount == requested.AcceptedCount
        && stored.RejectedCount == requested.RejectedCount
        && stored.QuarantinedCount == requested.QuarantinedCount
        && SameDictionary(stored.FindingCounts, requested.FindingCounts)
        && SameDictionary(stored.ControlTotals, requested.ControlTotals)
        && stored.UnresolvedDependencyCount == requested.UnresolvedDependencyCount
        && stored.ExceptionCount == requested.ExceptionCount
        && stored.Rows.SequenceEqual(requested.Rows);

    private static bool SameDictionary<T>(IReadOnlyDictionary<string, T> left, IReadOnlyDictionary<string, T> right) =>
        left.Count == right.Count
        && left.All(item => right.TryGetValue(item.Key, out var value) && EqualityComparer<T>.Default.Equals(item.Value, value));

    private static MigrationStagedRecord ToRecord(MigrationStagedRecordEntity entity) => new(
        entity.StagedRecordId,
        entity.TenantId,
        entity.RunId,
        entity.SourceSequence,
        entity.SourceRecordId,
        entity.RecordType,
        entity.CanonicalPayload,
        entity.PayloadHash,
        entity.PackageHash,
        entity.PackageVersion,
        entity.SourceObjectId,
        entity.SourceSnapshotHash,
        entity.CapturedAt);

    private static MigrationValidationFinding ToRecord(MigrationValidationFindingEntity entity) => new(
        entity.FindingId,
        entity.TenantId,
        entity.RunId,
        entity.AttemptId,
        entity.StagedRecordId,
        entity.Category,
        entity.Severity,
        entity.IsBlocking,
        entity.Code,
        entity.Message,
        entity.ReferenceId,
        entity.CreatedAt);

    private static T ReadJson<T>(string json, T fallback)
    {
        try { return JsonSerializer.Deserialize<T>(json) ?? fallback; }
        catch (JsonException) { return fallback; }
    }

    private static IReadOnlyDictionary<string, T> ReadDictionary<T>(string json) =>
        ReadJson(json, new Dictionary<string, T>(StringComparer.Ordinal));

    private static IReadOnlyList<string> ReadList(string json) => ReadJson(json, Array.Empty<string>());
}

#pragma warning restore CS1591
