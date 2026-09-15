#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record MasterDataImportEvidenceReadModel(
    MasterDataImportBatchRecord Batch,
    IReadOnlyList<MasterDataImportRowRecord> Rows,
    IReadOnlyList<MasterDataImportAuditRecord> Audit,
    MasterDataImportReconciliation Reconciliation);

/// <summary>
/// Application orchestration for the structured Master Data import seam.
/// It owns the durable lifecycle and never writes a business table directly.
/// </summary>
public sealed class MasterDataImportService
{
    public const int MaximumRows = 10000;
    public const int MaximumSourceReferenceLength = 1024;

    private readonly MasterDataResourceAuthorizationService authorization;
    private readonly IMasterDataImportPersistence persistence;
    private readonly MasterDataImportProcessorRegistry processors;

    public MasterDataImportService(
        MasterDataImportAuthorizationComposition authorizationComposition,
        IMasterDataImportPersistence persistence,
        MasterDataImportProcessorRegistry processors)
    {
        ArgumentNullException.ThrowIfNull(authorizationComposition);
        this.authorization = authorizationComposition.Authorization;
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.processors = processors ?? throw new ArgumentNullException(nameof(processors));
    }

    public async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> CreateBatchAsync(
        MasterDataRequestContext context,
        MasterDataImportBatchRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var authorizationFailure = Authorize(context, null, MasterDataOperation.Import);
        if (authorizationFailure is not null)
        {
            return Failure<MasterDataImportBatchRecord>(authorizationFailure);
        }

        return await CreateBatchCoreAsync(context, request, idempotencyKey, null, cancellationToken);
    }

    internal async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> CreateTrustedOwnerBatchAsync(
        FoundationRequestContext trustedContext,
        TrustedOwnerImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trustedContext);
        ArgumentNullException.ThrowIfNull(request);

        if (!IsTrustedMigrationContext(trustedContext))
        {
            return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                "migration_execution_authority_required",
                StatusCodes.Status403Forbidden);
        }

        MasterDataRequestContext context;
        try
        {
            context = MasterDataRequestContext.FromFoundationContext(trustedContext);
        }
        catch (ArgumentException)
        {
            return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                "tenant_context_failed",
                StatusCodes.Status403Forbidden);
        }

        return await CreateBatchCoreAsync(
            context,
            new MasterDataImportBatchRequest(
                request.ResourceKind,
                new MasterDataImportSourceRequest(
                    request.Source.SourceSystemCategory,
                    request.Source.SourceFileReference,
                    request.Source.BatchReference),
                request.Source.SourceFileReference,
                MasterDataImportDuplicatePolicy.Reject,
                MasterDataImportMode.Commit,
                request.Rows),
            request.IdempotencyKey,
            request.BatchId,
            cancellationToken);
    }

    private async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> CreateBatchCoreAsync(
        MasterDataRequestContext context,
        MasterDataImportBatchRequest request,
        string? idempotencyKey,
        Guid? requestedBatchId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        if (!processors.TryGet(request.ResourceKind, out _))
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_resource_unsupported",
                    StatusCodes.Status400BadRequest));
        }

        if (!Enum.IsDefined(request.DuplicatePolicy)
            || !Enum.IsDefined(request.Mode))
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_request_invalid",
                    StatusCodes.Status400BadRequest));
        }

        var rowsInput = request.Rows ?? [];
        if (rowsInput.Count > MaximumRows)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_row_limit_exceeded",
                    StatusCodes.Status400BadRequest));
        }

        var rowNumbers = new HashSet<int>();
        var rows = new List<MasterDataImportRowRecord>(rowsInput.Count);
        var now = DateTimeOffset.UtcNow;
        var batchId = requestedBatchId is { } suppliedBatchId && suppliedBatchId != Guid.Empty
            ? suppliedBatchId
            : Guid.NewGuid();
        foreach (var input in rowsInput)
        {
            if (input is null)
            {
                return Failure<MasterDataImportBatchRecord>(
                    MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                        "import_row_invalid",
                        StatusCodes.Status400BadRequest));
            }

            if (input.RowNumber < 1 || !rowNumbers.Add(input.RowNumber))
            {
                return Failure<MasterDataImportBatchRecord>(
                    MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                        "import_row_number_invalid",
                        StatusCodes.Status400BadRequest));
            }

            var sourceFields = input.Fields is null
                ? new Dictionary<string, string?>(StringComparer.Ordinal)
                : new Dictionary<string, string?>(input.Fields, StringComparer.Ordinal);
            if (sourceFields.Count > MasterDataImportFields.MaximumFieldCount)
            {
                return Failure<MasterDataImportBatchRecord>(
                    MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                        "import_row_payload_too_large",
                        StatusCodes.Status400BadRequest));
            }

            if (sourceFields.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
                    || pair.Key.Length > 128
                    || pair.Key.Any(char.IsControl)
                    || pair.Value?.Length > MasterDataImportFields.MaximumFieldValueLength))
            {
                return Failure<MasterDataImportBatchRecord>(
                    MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                        "import_field_too_large",
                        StatusCodes.Status400BadRequest));
            }

            rows.Add(new MasterDataImportRowRecord(
                Guid.NewGuid(),
                batchId,
                context.TenantId,
                input.RowNumber,
                0,
                true,
                request.ResourceKind,
                sourceFields,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                MasterDataImportRowOutcome.NotProcessed,
                [],
                MasterDataImportDiagnosticSeverity.Info,
                MasterDataImportMutationDisposition.NotAttempted,
                null,
                null,
                null,
                null,
                null,
                null,
                now,
                Guid.NewGuid().ToByteArray()));
        }

        var source = NormalizeSource(request);
        if (source is null)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_source_invalid",
                    StatusCodes.Status400BadRequest));
        }

        var correlationId = Correlation(context);
        var fingerprint = Fingerprint(request.ResourceKind, request.DuplicatePolicy, request.Mode, source, rows);
        var result = await TryAsync(() => persistence.CreateBatchAsync(
            context.TenantContext,
            new CreateMasterDataImportBatchCommand(
                batchId,
                request.ResourceKind,
                source,
                request.DuplicatePolicy,
                request.Mode,
                context.ActorId,
                now,
                correlationId,
                NormalizeIdempotencyKey(idempotencyKey),
                fingerprint,
                rows),
            cancellationToken));
        if (result is null)
        {
            return Failure<MasterDataImportBatchRecord>(Unavailable<MasterDataImportBatchRecord>());
        }

        if (!result.Succeeded || result.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(PersistenceFailure<MasterDataImportBatchRecord>(result.Code));
        }

        if (result.IsReplay)
        {
            return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(
                result.Value,
                "idempotent_replay");
        }

        var audit = await AppendAuditAsync(
            context,
            result.Value,
            null,
            "batch.created",
            "created",
            "The import batch was durably created.",
            cancellationToken);
        if (!audit)
        {
            return Failure<MasterDataImportBatchRecord>(Unavailable<MasterDataImportBatchRecord>("import_audit_persistence_failed"));
        }

        return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(
            result.Value,
            result.IsReplay ? "idempotent_replay" : "created");
    }

    public async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> SimulateAsync(
        MasterDataRequestContext context,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await FindAuthorizedBatchAsync(context, batchId, MasterDataOperation.Import, cancellationToken);
        if (!batch.Succeeded || batch.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(batch.Error!);
        }

        return await SimulateCoreAsync(context, batch.Value, cancellationToken);
    }

    internal async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> SimulateTrustedOwnerBatchAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var context = await TrustedContextAsync(trustedContext);
        if (context is null)
        {
            return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                "tenant_context_failed",
                StatusCodes.Status403Forbidden);
        }

        var batch = await TryReadAsync(() => persistence.FindBatchAsync(context.TenantContext, batchId, cancellationToken));
        if (!batch.Available)
        {
            return Unavailable<MasterDataImportBatchRecord>();
        }

        return batch.Value is null
            ? MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure("import_batch_not_found", StatusCodes.Status404NotFound)
            : await SimulateCoreAsync(context, batch.Value, cancellationToken);
    }

    private async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> SimulateCoreAsync(
        MasterDataRequestContext context,
        MasterDataImportBatchRecord batch,
        CancellationToken cancellationToken)
    {
        if (batch.Status is MasterDataImportStatus.Validated
            or MasterDataImportStatus.Completed
            or MasterDataImportStatus.CompletedWithErrors)
        {
            return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(batch, "already_simulated");
        }

        if (batch.Status != MasterDataImportStatus.Draft)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_batch_invalid_state",
                    StatusCodes.Status409Conflict));
        }

        var rowsRead = await TryReadAsync(() => persistence.ListRowsAsync(context.TenantContext, batch.Id, cancellationToken));
        if (!rowsRead.Available)
        {
            return Unavailable<MasterDataImportBatchRecord>();
        }

        var rows = rowsRead.Value ?? [];
        var start = DateTimeOffset.UtcNow;
        var simulating = await SaveAsync(
            context,
            batch,
            MasterDataImportStatus.Simulating,
            start,
            null,
            rows,
            cancellationToken);
        if (!simulating.Succeeded || simulating.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(simulating.Error!);
        }

        var processing = new MasterDataImportProcessingContext(
            context,
            simulating.Value,
            new HashSet<string>(StringComparer.Ordinal));
        var validatedRows = new List<MasterDataImportRowRecord>(rows.Count);
        foreach (var row in rows.OrderBy(item => item.OriginalRowNumber).ThenBy(item => item.ReplaySequence))
        {
            var processor = processors.GetRequired(row.ResourceKind);
            var validation = await processor.ValidateAsync(processing, row, cancellationToken);
            validatedRows.Add(row.WithValidation(validation, DateTimeOffset.UtcNow));
        }

        var reconciliation = MasterDataImportReconciliation.FromRows(validatedRows);
        var validated = await SaveAsync(
            context,
            simulating.Value,
            MasterDataImportStatus.Validated,
            start,
            DateTimeOffset.UtcNow,
            validatedRows,
            cancellationToken,
            reconciliation);
        if (!validated.Succeeded || validated.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(validated.Error!);
        }

        var audit = await AppendAuditAsync(
            context,
            validated.Value,
            null,
            "batch.simulated",
            "validated",
            "The import batch was validated without target mutations.",
            cancellationToken);
        if (!audit)
        {
            return Failure<MasterDataImportBatchRecord>(Unavailable<MasterDataImportBatchRecord>("import_audit_persistence_failed"));
        }

        return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(validated.Value, "validated");
    }

    public async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> ExecuteAsync(
        MasterDataRequestContext context,
        Guid batchId,
        byte[]? expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var batch = await FindAuthorizedBatchAsync(context, batchId, MasterDataOperation.Import, cancellationToken);
        if (!batch.Succeeded || batch.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(batch.Error!);
        }

        return await ExecuteCoreAsync(context, batch.Value, expectedVersion, cancellationToken);
    }

    internal async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> ExecuteTrustedOwnerBatchAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var context = await TrustedContextAsync(trustedContext);
        if (context is null)
        {
            return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                "tenant_context_failed",
                StatusCodes.Status403Forbidden);
        }

        var batch = await TryReadAsync(() => persistence.FindBatchAsync(context.TenantContext, batchId, cancellationToken));
        if (!batch.Available)
        {
            return Unavailable<MasterDataImportBatchRecord>();
        }

        return batch.Value is null
            ? MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure("import_batch_not_found", StatusCodes.Status404NotFound)
            : await ExecuteCoreAsync(context, batch.Value, expectedVersion, cancellationToken);
    }

    private async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> ExecuteCoreAsync(
        MasterDataRequestContext context,
        MasterDataImportBatchRecord batch,
        byte[]? expectedVersion,
        CancellationToken cancellationToken)
    {

        if (batch.Status is MasterDataImportStatus.Completed or MasterDataImportStatus.CompletedWithErrors)
        {
            return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(batch, "already_completed");
        }

        if (batch.Mode == MasterDataImportMode.DryRun)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_dry_run_no_commit",
                    StatusCodes.Status409Conflict));
        }

        if (batch.Status != MasterDataImportStatus.Validated)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_batch_invalid_state",
                    StatusCodes.Status409Conflict));
        }

        if (expectedVersion is null || !VersionMatches(batch.Version, expectedVersion))
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_concurrency_conflict",
                    StatusCodes.Status409Conflict));
        }

        var rowsRead = await TryReadAsync(() => persistence.ListRowsAsync(context.TenantContext, batch.Id, cancellationToken));
        if (!rowsRead.Available)
        {
            return Unavailable<MasterDataImportBatchRecord>();
        }

        var rows = rowsRead.Value ?? [];
        var processing = new MasterDataImportProcessingContext(
            context,
            batch,
            new HashSet<string>(
                rows.Where(item => item.IsCurrent && item.Outcome == MasterDataImportRowOutcome.Accepted)
                    .Select(item => item.IdentityKey),
                StringComparer.Ordinal));
        var executedRows = new List<MasterDataImportRowRecord>(rows.Count);
        foreach (var row in rows)
        {
            if (row.IsCurrent
                && row.Outcome == MasterDataImportRowOutcome.Accepted
                && row.MutationDisposition == MasterDataImportMutationDisposition.Eligible)
            {
                var mutation = await processors.GetRequired(row.ResourceKind).CommitAsync(processing, row, cancellationToken);
                executedRows.Add(row.WithMutation(mutation, DateTimeOffset.UtcNow));
            }
            else
            {
                executedRows.Add(row);
            }
        }

        var reconciliation = MasterDataImportReconciliation.FromRows(executedRows);
        var status = reconciliation.Rejected == 0 && reconciliation.Quarantined == 0
            ? MasterDataImportStatus.Completed
            : MasterDataImportStatus.CompletedWithErrors;
        var saved = await SaveAsync(
            context,
            batch,
            status,
            batch.StartedAt ?? DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            executedRows,
            cancellationToken,
            reconciliation);
        if (!saved.Succeeded || saved.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(saved.Error!);
        }

        foreach (var row in executedRows.Where(item => item.IsCurrent))
        {
            if (!await AppendMutationAuditAsync(context, saved.Value, row, cancellationToken))
            {
                return Failure<MasterDataImportBatchRecord>(Unavailable<MasterDataImportBatchRecord>("import_audit_persistence_failed"));
            }
        }

        var audit = await AppendAuditAsync(
            context,
            saved.Value,
            null,
            "batch.executed",
            status == MasterDataImportStatus.Completed ? "completed" : "completed_with_errors",
            "The import batch execution was reconciled without claiming failed mutations as committed.",
            cancellationToken);
        if (!audit)
        {
            return Failure<MasterDataImportBatchRecord>(Unavailable<MasterDataImportBatchRecord>("import_audit_persistence_failed"));
        }

        return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(saved.Value, "executed");
    }

    internal async Task<MasterDataImportEvidenceReadModel?> ReadTrustedOwnerEvidenceAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var context = await TrustedContextAsync(trustedContext);
        if (context is null)
        {
            return null;
        }

        var batch = await TryAsync(() => persistence.FindBatchAsync(context.TenantContext, batchId, cancellationToken));
        var rows = await TryAsync(() => persistence.ListRowsAsync(context.TenantContext, batchId, cancellationToken));
        var audit = await TryAsync(() => persistence.ListAuditAsync(context.TenantContext, batchId, cancellationToken));
        return batch is null || rows is null || audit is null
            ? null
            : new MasterDataImportEvidenceReadModel(
                batch,
                rows,
                audit,
                MasterDataImportReconciliation.FromRows(rows));
    }

    public async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> ReplayQuarantinedRowAsync(
        MasterDataRequestContext context,
        Guid batchId,
        Guid rowId,
        MasterDataImportReplayRequest request,
        string? replayIdempotencyKey,
        byte[]? expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batch = await FindAuthorizedBatchAsync(context, batchId, MasterDataOperation.Import, cancellationToken);
        if (!batch.Succeeded || batch.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(batch.Error!);
        }

        var alreadyExecuted = batch.Value.Status is
            MasterDataImportStatus.Completed
            or MasterDataImportStatus.CompletedWithErrors;

        var key = NormalizeIdempotencyKey(replayIdempotencyKey);
        if (key is null)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_replay_idempotency_required",
                    StatusCodes.Status400BadRequest));
        }

        if (expectedVersion is null || !VersionMatches(batch.Value.Version, expectedVersion))
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_concurrency_conflict",
                    StatusCodes.Status409Conflict));
        }

        var rowsRead = await TryReadAsync(() => persistence.ListRowsAsync(context.TenantContext, batchId, cancellationToken));
        if (!rowsRead.Available)
        {
            return Unavailable<MasterDataImportBatchRecord>();
        }

        var allRows = rowsRead.Value ?? [];
        var priorReplay = allRows.FirstOrDefault(item => item.ReplayIdempotencyKey == key);
        if (priorReplay is not null)
        {
            return priorReplay.ReplayOfRowId == rowId || priorReplay.OriginalRowId == rowId
                ? MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(batch.Value, "idempotent_replay")
                : Failure<MasterDataImportBatchRecord>(
                    MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                        "import_replay_idempotency_conflict",
                        StatusCodes.Status409Conflict));
        }

        var original = allRows.SingleOrDefault(item => item.Id == rowId);
        if (original is null)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_row_not_found",
                    StatusCodes.Status404NotFound));
        }

        if (!original.IsCurrent || original.Outcome != MasterDataImportRowOutcome.Quarantined)
        {
            return Failure<MasterDataImportBatchRecord>(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    "import_replay_not_allowed",
                    StatusCodes.Status409Conflict));
        }

        var correctedFields = request.CorrectedFields is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(request.CorrectedFields, StringComparer.OrdinalIgnoreCase);
        var replaySequence = allRows
            .Where(item => item.OriginalRowNumber == original.OriginalRowNumber)
            .Select(item => item.ReplaySequence)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var replay = new MasterDataImportRowRecord(
            Guid.NewGuid(),
            batchId,
            batch.Value.TenantId,
            original.OriginalRowNumber,
            replaySequence,
            true,
            original.ResourceKind,
            correctedFields,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            MasterDataImportRowOutcome.NotProcessed,
            [],
            MasterDataImportDiagnosticSeverity.Info,
            MasterDataImportMutationDisposition.NotAttempted,
            null,
            null,
            null,
            original.Id,
            original.OriginalRowId ?? original.Id,
            key,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToByteArray());

        var processing = new MasterDataImportProcessingContext(
            context,
            batch.Value,
            new HashSet<string>(
                allRows.Where(item => item.IsCurrent && item.Outcome == MasterDataImportRowOutcome.Accepted)
                    .Select(item => item.IdentityKey),
                StringComparer.Ordinal));
        var validation = await processors.GetRequired(replay.ResourceKind).ValidateAsync(processing, replay, cancellationToken);
        replay = replay.WithValidation(validation, DateTimeOffset.UtcNow);
        var updatedOriginal = original with { IsCurrent = false };
        var nextRows = allRows.Select(item => item.Id == original.Id ? updatedOriginal : item).Append(replay).ToArray();

        if (replay.Outcome == MasterDataImportRowOutcome.Accepted
            && replay.MutationDisposition == MasterDataImportMutationDisposition.Eligible
            && batch.Value.Mode == MasterDataImportMode.Commit
            && alreadyExecuted)
        {
            var mutation = await processors.GetRequired(replay.ResourceKind).CommitAsync(processing, replay, cancellationToken);
            replay = replay.WithMutation(mutation, DateTimeOffset.UtcNow);
            nextRows = allRows.Select(item => item.Id == original.Id ? updatedOriginal : item).Append(replay).ToArray();
        }

        var reconciliation = MasterDataImportReconciliation.FromRows(nextRows);
        var status = batch.Value.Mode == MasterDataImportMode.DryRun || !alreadyExecuted
            ? MasterDataImportStatus.Validated
            : reconciliation.Rejected == 0 && reconciliation.Quarantined == 0
                ? MasterDataImportStatus.Completed
                : MasterDataImportStatus.CompletedWithErrors;
        var saved = await SaveAsync(
            context,
            batch.Value,
            status,
            batch.Value.StartedAt ?? DateTimeOffset.UtcNow,
            status == MasterDataImportStatus.Validated ? batch.Value.CompletedAt : DateTimeOffset.UtcNow,
            nextRows,
            cancellationToken,
            reconciliation);
        if (!saved.Succeeded || saved.Value is null)
        {
            return Failure<MasterDataImportBatchRecord>(saved.Error!);
        }

        if (!await AppendMutationAuditAsync(context, saved.Value, replay, cancellationToken))
        {
            return Failure<MasterDataImportBatchRecord>(Unavailable<MasterDataImportBatchRecord>("import_audit_persistence_failed"));
        }

        var audit = await AppendAuditAsync(
            context,
            saved.Value,
            replay,
            "row.replayed",
            replay.Outcome.ToString(),
            "The original row evidence remains preserved and the corrected replay carries explicit lineage.",
            cancellationToken);
        if (!audit)
        {
            return Failure<MasterDataImportBatchRecord>(Unavailable<MasterDataImportBatchRecord>("import_audit_persistence_failed"));
        }

        return MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(saved.Value, "replayed");
    }

    public async Task<MasterDataImportOperationResult<IReadOnlyList<MasterDataImportBatchRecord>>> ListBatchesAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var failure = Authorize(context, null, MasterDataOperation.View);
        if (failure is not null)
        {
            return MasterDataImportOperationResult<IReadOnlyList<MasterDataImportBatchRecord>>.Failure(failure.Code, failure.StatusCode);
        }

        var batches = await TryAsync(() => persistence.ListBatchesAsync(context.TenantContext, cancellationToken));
        return batches is null
            ? MasterDataImportOperationResult<IReadOnlyList<MasterDataImportBatchRecord>>.Failure("import_unavailable", StatusCodes.Status503ServiceUnavailable)
            : MasterDataImportOperationResult<IReadOnlyList<MasterDataImportBatchRecord>>.Success(batches);
    }

    public Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> ReadBatchAsync(
        MasterDataRequestContext context,
        Guid batchId,
        CancellationToken cancellationToken = default) =>
        ReadAuthorizedAsync(context, batchId, MasterDataOperation.View, cancellationToken);

    public async Task<MasterDataImportOperationResult<IReadOnlyList<MasterDataImportRowRecord>>> ReadRowsAsync(
        MasterDataRequestContext context,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await FindAuthorizedBatchAsync(context, batchId, MasterDataOperation.View, cancellationToken);
        if (!batch.Succeeded)
        {
            return MasterDataImportOperationResult<IReadOnlyList<MasterDataImportRowRecord>>.Failure(batch.Error!.Code, batch.Error.StatusCode);
        }

        var rows = await TryAsync(() => persistence.ListRowsAsync(context.TenantContext, batchId, cancellationToken));
        return rows is null
            ? MasterDataImportOperationResult<IReadOnlyList<MasterDataImportRowRecord>>.Failure("import_unavailable", StatusCodes.Status503ServiceUnavailable)
            : MasterDataImportOperationResult<IReadOnlyList<MasterDataImportRowRecord>>.Success(rows);
    }

    public async Task<MasterDataImportOperationResult<MasterDataImportReconciliation>> ReadReconciliationAsync(
        MasterDataRequestContext context,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await FindAuthorizedBatchAsync(context, batchId, MasterDataOperation.View, cancellationToken);
        if (!batch.Succeeded)
        {
            return MasterDataImportOperationResult<MasterDataImportReconciliation>.Failure(batch.Error!.Code, batch.Error.StatusCode);
        }

        var rows = await TryAsync(() => persistence.ListRowsAsync(context.TenantContext, batchId, cancellationToken));
        return rows is null
            ? MasterDataImportOperationResult<MasterDataImportReconciliation>.Failure("import_unavailable", StatusCodes.Status503ServiceUnavailable)
            : MasterDataImportOperationResult<MasterDataImportReconciliation>.Success(MasterDataImportReconciliation.FromRows(rows));
    }

    public async Task<MasterDataImportOperationResult<IReadOnlyList<MasterDataImportAuditRecord>>> ReadAuditAsync(
        MasterDataRequestContext context,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var failure = Authorize(context, batchId, MasterDataOperation.ViewAuditHistory);
        if (failure is not null)
        {
            return MasterDataImportOperationResult<IReadOnlyList<MasterDataImportAuditRecord>>.Failure(failure.Code, failure.StatusCode);
        }

        var batchRead = await TryReadAsync(() => persistence.FindBatchAsync(context.TenantContext, batchId, cancellationToken));
        if (!batchRead.Available)
        {
            return MasterDataImportOperationResult<IReadOnlyList<MasterDataImportAuditRecord>>.Failure(
                "import_unavailable",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (batchRead.Value is null)
        {
            return MasterDataImportOperationResult<IReadOnlyList<MasterDataImportAuditRecord>>.Failure("import_batch_not_found", StatusCodes.Status404NotFound);
        }

        var audit = await TryAsync(() => persistence.ListAuditAsync(context.TenantContext, batchId, cancellationToken));
        return audit is null
            ? MasterDataImportOperationResult<IReadOnlyList<MasterDataImportAuditRecord>>.Failure("import_unavailable", StatusCodes.Status503ServiceUnavailable)
            : MasterDataImportOperationResult<IReadOnlyList<MasterDataImportAuditRecord>>.Success(audit);
    }

    public async Task<MasterDataImportOperationResult<MasterDataImportEvidenceReadModel>> ReadEvidenceAsync(
        MasterDataRequestContext context,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await FindAuthorizedBatchAsync(context, batchId, MasterDataOperation.View, cancellationToken);
        if (!batch.Succeeded || batch.Value is null)
        {
            return MasterDataImportOperationResult<MasterDataImportEvidenceReadModel>.Failure(batch.Error!.Code, batch.Error.StatusCode);
        }

        var rows = await TryAsync(() => persistence.ListRowsAsync(context.TenantContext, batchId, cancellationToken));
        var audit = await TryAsync(() => persistence.ListAuditAsync(context.TenantContext, batchId, cancellationToken));
        if (rows is null || audit is null)
        {
            return MasterDataImportOperationResult<MasterDataImportEvidenceReadModel>.Failure("import_unavailable", StatusCodes.Status503ServiceUnavailable);
        }

        return MasterDataImportOperationResult<MasterDataImportEvidenceReadModel>.Success(
            new MasterDataImportEvidenceReadModel(
                batch.Value,
                rows,
                audit,
                MasterDataImportReconciliation.FromRows(rows)));
    }

    private async Task<MasterDataImportOperationResult<MasterDataImportBatchRecord>> ReadAuthorizedAsync(
        MasterDataRequestContext context,
        Guid batchId,
        MasterDataOperation operation,
        CancellationToken cancellationToken)
    {
        var batch = await FindAuthorizedBatchAsync(context, batchId, operation, cancellationToken);
        return batch.Succeeded && batch.Value is not null
            ? MasterDataImportOperationResult<MasterDataImportBatchRecord>.Success(batch.Value)
            : Failure<MasterDataImportBatchRecord>(batch.Error!);
    }

    private async Task<AuthorizedBatchResult> FindAuthorizedBatchAsync(
        MasterDataRequestContext context,
        Guid batchId,
        MasterDataOperation operation,
        CancellationToken cancellationToken)
    {
        var failure = Authorize(context, batchId, operation);
        if (failure is not null)
        {
            return AuthorizedBatchResult.Failed(
                MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure(
                    failure.Code,
                    failure.StatusCode));
        }

        var batchRead = await TryReadAsync(() => persistence.FindBatchAsync(context.TenantContext, batchId, cancellationToken));
        if (!batchRead.Available)
        {
            return AuthorizedBatchResult.Failed(Unavailable<MasterDataImportBatchRecord>());
        }

        return batchRead.Value is null
            ? AuthorizedBatchResult.Failed(MasterDataImportOperationResult<MasterDataImportBatchRecord>.Failure("import_batch_not_found", StatusCodes.Status404NotFound))
            : AuthorizedBatchResult.Success(batchRead.Value);
    }

    private async Task<SaveResult> SaveAsync(
        MasterDataRequestContext context,
        MasterDataImportBatchRecord batch,
        MasterDataImportStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        IReadOnlyList<MasterDataImportRowRecord> rows,
        CancellationToken cancellationToken,
        MasterDataImportReconciliation? reconciliation = null)
    {
        var result = await TryAsync(() => persistence.SaveResultAsync(
            context.TenantContext,
            new SaveMasterDataImportResultCommand(
                batch.Id,
                status,
                startedAt,
                completedAt,
                batch.CorrelationId,
                batch.Fingerprint,
                batch.Version,
                rows,
                reconciliation ?? MasterDataImportReconciliation.FromRows(rows)),
            cancellationToken));
        return result is null
            ? SaveResult.Failed(Unavailable<MasterDataImportBatchRecord>())
            : result.Succeeded && result.Value is not null
                ? SaveResult.Success(result.Value)
                : SaveResult.Failed(PersistenceFailure<MasterDataImportBatchRecord>(result.Code));
    }

    private async Task<bool> AppendAuditAsync(
        MasterDataRequestContext context,
        MasterDataImportBatchRecord batch,
        MasterDataImportRowRecord? row,
        string operation,
        string outcome,
        string detail,
        CancellationToken cancellationToken)
    {
        var audit = new MasterDataImportAuditRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            $"master-data.import.{operation}",
            batch.CorrelationId,
            batch.TenantId,
            context.ActorId,
            batch.Id,
            row?.Id,
            row?.OriginalRowNumber,
            batch.ResourceKind,
            outcome,
            batch.Source.SourceFileReference ?? batch.Source.BatchReference ?? batch.Id.ToString("D"),
            detail);
        var result = await TryAsync(() => persistence.AppendAuditAsync(context.TenantContext, audit, cancellationToken));
        return result?.Succeeded == true;
    }

    private async Task<bool> AppendMutationAuditAsync(
        MasterDataRequestContext context,
        MasterDataImportBatchRecord batch,
        MasterDataImportRowRecord row,
        CancellationToken cancellationToken)
    {
        if (row.MutationDisposition is not (
            MasterDataImportMutationDisposition.Committed
            or MasterDataImportMutationDisposition.Updated
            or MasterDataImportMutationDisposition.SkippedExisting
            or MasterDataImportMutationDisposition.Failed))
        {
            return true;
        }

        var detail = $"mutation={row.MutationDisposition};resourceId={row.ResultingResourceId?.ToString("D") ?? string.Empty};code={row.ResultingResourceCode ?? string.Empty}";
        return await AppendAuditAsync(
            context,
            batch,
            row,
            "row.mutated",
            row.MutationDisposition.ToString(),
            detail,
            cancellationToken);
    }

    private AuthorizationFailure? Authorize(
        MasterDataRequestContext context,
        Guid? batchId,
        MasterDataOperation operation)
    {
        var resource = new MasterDataResourceReference(
            MasterDataResourceKind.Import,
            new TenantOwnership(context.TenantId.Value),
            batchId,
            batchId is null ? "imports" : null,
            MasterDataImportScopePolicy.CreateScope(context.TenantId));
        var decision = authorization.Authorize(context, resource, operation);
        return decision.Allowed
            ? null
            : new AuthorizationFailure(decision.Code, AuthorizationStatusCode(decision.Code));
    }

    private static int AuthorizationStatusCode(string code) => code switch
    {
        "permission_denied"
            or "resource_scope_denied"
            or "cross_tenant_target_denied"
            or "tenant_context_failed"
            or "authorization_denied"
            or "approval_required"
            or "approval_policy_not_configured"
            or "resource_policy_not_configured" => StatusCodes.Status403Forbidden,
        "permission_unavailable"
            or "scope_policy_unavailable"
            or "approval_policy_unavailable"
            or "resource_policy_unavailable"
            or "authorization_operation_unmapped" => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest
    };

    private static MasterDataImportSource? NormalizeSource(MasterDataImportBatchRequest request)
    {
        var source = request.Source;
        var category = string.IsNullOrWhiteSpace(source?.SourceSystemCategory)
            ? "structured-api"
            : source!.SourceSystemCategory!.Trim();
        var fileReference = string.IsNullOrWhiteSpace(request.SourceFileReference)
            ? source?.SourceFileReference?.Trim()
            : request.SourceFileReference!.Trim();
        var batchReference = source?.BatchReference?.Trim();
        if (category.Length > 128
            || category.Any(char.IsControl)
            || fileReference?.Length > MaximumSourceReferenceLength
            || fileReference?.Any(char.IsControl) == true
            || batchReference?.Length > 256
            || batchReference?.Any(char.IsControl) == true)
        {
            return null;
        }

        return new MasterDataImportSource(category, fileReference, batchReference);
    }

    private static string Fingerprint(
        MasterDataResourceKind resourceKind,
        MasterDataImportDuplicatePolicy duplicatePolicy,
        MasterDataImportMode mode,
        MasterDataImportSource source,
        IReadOnlyList<MasterDataImportRowRecord> rows)
    {
        var canonical = new
        {
            ResourceKind = resourceKind,
            DuplicatePolicy = duplicatePolicy,
            Mode = mode,
            Source = source,
            Rows = rows.OrderBy(item => item.OriginalRowNumber)
                .Select(item => new
                {
                    item.OriginalRowNumber,
                    Fields = item.SourceFields.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new { Key = pair.Key, pair.Value })
                        .ToArray()
                })
                .ToArray()
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Correlation(MasterDataRequestContext context) =>
        context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N");

    private static string? NormalizeIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length is > 256 or 0 || normalized.Any(char.IsControl) ? null : normalized;
    }

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        current.Length != 0 && expected.Length != 0 && current.SequenceEqual(expected);

    private static Task<MasterDataRequestContext?> TrustedContextAsync(FoundationRequestContext trustedContext)
    {
        if (!IsTrustedMigrationContext(trustedContext))
            return Task.FromResult<MasterDataRequestContext?>(null);
        try
        {
            return Task.FromResult<MasterDataRequestContext?>(MasterDataRequestContext.FromFoundationContext(trustedContext));
        }
        catch (ArgumentException)
        {
            return Task.FromResult<MasterDataRequestContext?>(null);
        }
    }

    private static bool IsTrustedMigrationContext(FoundationRequestContext? context) =>
        context is not null
        && context.SecurityProfile is (FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant)
        && context.TenantContext is { } tenant
        && context.PlatformGovernanceContext is null
        && context.ActorId is { } actorId
        && actorId != Guid.Empty
        && (tenant.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership && context.SecurityProfile == FoundationSecurityProfile.OrdinaryMembership
            || tenant.AuthorizationPath == TenantAuthorizationPath.SupportGrant && context.SecurityProfile == FoundationSecurityProfile.SupportGrant)
        && (tenant.ActorId is null || tenant.ActorId == actorId)
        && context.SessionId is { } sessionId
        && sessionId != Guid.Empty
        && string.Equals(context.Permission, "tenant.migration.execute", StringComparison.Ordinal);

    private static async Task<T?> TryAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch
        {
            return default;
        }
    }

    private static async Task<PersistenceRead<T>> TryReadAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return new PersistenceRead<T>(true, await action());
        }
        catch
        {
            return new PersistenceRead<T>(false, default);
        }
    }

    private static MasterDataImportOperationResult<T> Failure<T>(MasterDataImportOperationResult<T> failure) => failure;

    private static MasterDataImportOperationResult<T> Failure<T>(AuthorizationFailure failure) =>
        MasterDataImportOperationResult<T>.Failure(failure.Code, failure.StatusCode);

    private static MasterDataImportOperationResult<T> PersistenceFailure<T>(string code) =>
        MasterDataImportOperationResult<T>.Failure(
            code is "import_idempotency_conflict" or "import_duplicate" ? code : "import_persistence_failed",
            code is "import_concurrency_conflict" ? StatusCodes.Status409Conflict : StatusCodes.Status503ServiceUnavailable);

    private static MasterDataImportOperationResult<T> Unavailable<T>(string code = "import_unavailable") =>
        MasterDataImportOperationResult<T>.Failure(code, StatusCodes.Status503ServiceUnavailable);

    private sealed record AuthorizedBatchResult(
        bool Succeeded,
        MasterDataImportBatchRecord? Value,
        MasterDataImportOperationResult<MasterDataImportBatchRecord>? Error)
    {
        internal static AuthorizedBatchResult Success(MasterDataImportBatchRecord value) => new(true, value, null);

        internal static AuthorizedBatchResult Failed(MasterDataImportOperationResult<MasterDataImportBatchRecord> error) => new(false, null, error);
    }

    private sealed record SaveResult(
        bool Succeeded,
        MasterDataImportBatchRecord? Value,
        MasterDataImportOperationResult<MasterDataImportBatchRecord>? Error)
    {
        internal static SaveResult Success(MasterDataImportBatchRecord value) => new(true, value, null);

        internal static SaveResult Failed(MasterDataImportOperationResult<MasterDataImportBatchRecord> error) => new(false, null, error);
    }

    private sealed record AuthorizationFailure(string Code, int StatusCode);

    private sealed record PersistenceRead<T>(bool Available, T? Value);
}

#pragma warning restore CS1591
