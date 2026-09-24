#pragma warning disable CS1591

using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.Api;

public sealed record MigrationExecutionResponse(
    Guid RunId,
    Guid TenantId,
    Guid AttemptId,
    string FingerprintVersion,
    string Fingerprint,
    MigrationRunStatus RunStatus,
    MigrationAttemptOutcome AttemptOutcome,
    string OutcomeCode,
    IReadOnlyList<MigrationExecutionBatchResponse> Batches,
    IReadOnlyList<MigrationExecutionEffectResponse> Effects,
    IReadOnlyList<MigrationEconomicRepresentationResponse>? Representations,
    IReadOnlyList<MigrationEconomicReconciliationRecord>? EconomicReconciliations,
    IReadOnlyList<MigrationArEconomicReconciliationRecord>? ArEconomicReconciliations,
    IReadOnlyList<MigrationApEconomicReconciliationRecord>? ApEconomicReconciliations,
    IReadOnlyList<MigrationCashBankEconomicReconciliationRecord>? CashBankEconomicReconciliations,
    IReadOnlyList<MigrationGlEconomicReconciliationRecord>? GlEconomicReconciliations,
    bool ZeroEconomicEffects);

public sealed record MigrationExecutionBatchResponse(
    Guid Id, Guid TenantId, Guid RunId, Guid AttemptId, MigrationCanonicalRecordType RecordType,
    MigrationExecutionBatchState State, Guid OwnerBatchId, string Fingerprint, DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string CorrelationId, byte[] Version);

public sealed record MigrationExecutionEffectResponse(
    Guid Id, Guid TenantId, Guid RunId, Guid AttemptId, Guid StagedRecordId, int SourceSequence,
    MigrationCanonicalRecordType RecordType, Guid OwnerBatchId, Guid? OwnerRowId, Guid? ResultingResourceId,
    string? ResultingResourceCode, MigrationExecutionEffectDisposition Disposition, string? SafeCode,
    DateTimeOffset CreatedAt, DateTimeOffset? EffectStartedAt, DateTimeOffset? CompletedAt,
    string CorrelationId, byte[] Version);

public sealed record MigrationEconomicRepresentationResponse(
    Guid Id, Guid TenantId, Guid RunId, Guid AttemptId, Guid EffectId, MigrationEconomicOwnerModule OwnerModule,
    MigrationEconomicRepresentationKind Kind, Guid OwnerId, string? OwnerReference, string Status,
    string EvidenceVersion, DateTimeOffset OccurredAt, DateTimeOffset RecordedAt, bool EvidenceConfirmed,
    byte[] Version, string? SourceContract, string? SourceEvent, decimal? FunctionalAmount, Guid? PostingRuleId,
    int? PostingRuleVersionNumber, Guid? ControlAccountId, Guid? OffsetAccountId, bool? Reversal,
    Guid? SourceEvidenceId, int? SourceEvidenceVersion, Guid? OwnerSourceId, string? TransactionCurrencyCode,
    decimal? TransactionAmount, string? ExpectedFunctionalCurrencyCode, DateOnly? RateDate, Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId, int? ExchangeRateVersionNumber, decimal? AppliedRate, Guid? MonetaryPolicyId,
    int? MonetaryPolicyVersionNumber, int? RoundingScale, string? RoundingMode, string? ReportingCurrencyCode,
    Guid? ReportingExchangeRateId, Guid? ReportingExchangeRateVersionId, int? ReportingExchangeRateVersionNumber,
    decimal? ReportingAppliedRate);

public sealed record MigrationReconciliationResponse(
    Guid Id, Guid TenantId, Guid RunId, Guid AttemptId, int VersionNumber, string EvidenceFingerprint, string IdempotencyKey,
    MigrationReconciliationStatus Status, DateTimeOffset CreatedAt, DateTimeOffset CalculatedAt, int SubmittedCount,
    int AcceptedCount, int RejectedCount, int DuplicateCount, int SkippedCount, int QuarantinedCount, int UnresolvedCount,
    int RequiredApprovalCount, int ObtainedApprovalCount, decimal SourceDebit, decimal SourceCredit, decimal TargetDebit,
    decimal TargetCredit, decimal Variance, byte[] Version, bool IsCurrent, string? ApprovalPolicyId,
    int? ApprovalPolicyVersion, string ApprovalPolicyCode, DateTimeOffset? ApprovalPolicyEffectiveFrom,
    DateTimeOffset? ApprovalPolicyEffectiveTo, bool ApprovalEnforcesSeparationOfDuties,
    IReadOnlyList<MigrationReconciliationApprovalRequirement> Requirements, IReadOnlyList<MigrationReconciliationDetailResponse> Details,
    IReadOnlyList<MigrationReconciliationApprovalResponse> Approvals, MigrationHandoverReadinessResponse? Readiness);

public sealed record MigrationReconciliationDetailResponse(
    Guid Id, MigrationReconciliationDomain Domain, string ScopeKey, Guid? CompanyId, DateOnly? OpeningDate,
    string? CurrencyCode, string? TransactionCurrencyCode, string? FunctionalCurrencyCode, int SourceCount,
    decimal? SourceDebit, decimal? SourceCredit, decimal? TargetDebit, decimal? TargetCredit, decimal? Variance,
    decimal? SourceAmount, decimal? TargetAmount, decimal? AmountVariance, decimal? OwnerRoundingDifference,
    decimal? TransactionAmount, decimal? FunctionalAmount, decimal? SubsidiaryEstablishedAmount, decimal? GlControlAmount,
    Guid? ExchangeRateId, Guid? ExchangeRateVersionId, int? ExchangeRateVersionNumber, decimal? AppliedRate,
    decimal? SourceQuantity, decimal? TargetQuantity, decimal? QuantityVariance, Guid? ControlAccountId,
    Guid? PostingRuleId, int? PostingRuleVersionNumber, Guid? OwnerSourceId, Guid? WarehouseId, Guid? ProductId,
    Guid? UnitOfMeasureId, string? SourceContract, string? SourceEvent, Guid? RoundingPolicyId,
    int? RoundingPolicyVersionNumber, int? RoundingScale, string? RoundingMode, bool IsBlocking, string? FindingCode,
    string? Explanation, Guid? EffectId, Guid? OwnerReferenceId, Guid? LinkedAccountId);

public sealed record MigrationReconciliationApprovalResponse(
    Guid Id, Guid TenantId, Guid RunId, Guid ReconciliationId, int ReconciliationVersion, Guid AttemptId,
    string EvidenceFingerprint, string IdempotencyKey, string Domain, string RequirementKey, string PolicyId,
    int PolicyVersion, Guid ActorId, MigrationApprovalDecision Decision, string? Reason, DateTimeOffset DecidedAt,
    byte[] Version, bool EvidenceConfirmed);

public sealed record MigrationHandoverReadinessResponse(
    Guid Id, Guid TenantId, Guid RunId, Guid ReconciliationId, int ReconciliationVersion, Guid AttemptId,
    string EvidenceFingerprint, string IdempotencyKey, DateTimeOffset CreatedAt, bool BusinessReady,
    bool ProductionReady, bool Mesp48Complete, bool Mesp50Complete, bool TenantActivationPerformed,
    string ResultCode, byte[] Version);

public sealed record MigrationApprovalDecisionRequest(string Domain, string RequirementKey, MigrationApprovalDecision Decision, string? Reason);

/// <summary>REST adapter for the bounded MESP-141 migration lifecycle.</summary>
public static class MigrationEndpoints
{
    public static IEndpointRouteBuilder MapMigrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(
            "/api/v1/migrations/intakes",
            async (MigrationIntakeCreateRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MigrationIntakeService service) =>
                await ExecuteMutationAsync(request, httpContext, resolver, service))
            .WithName("migration.intake.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.intake.create")));

        endpoints.MapPost(
            "/api/v1/migrations/{runId:guid}/validation",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service) =>
                await ExecuteMutationAsync(runId, httpContext, resolver, service, dryRun: false))
            .WithName("migration.validation.start")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.validation.start")));

        endpoints.MapGet(
            "/api/v1/migrations/{runId:guid}/validation",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service) =>
                await ExecuteValidationReadAsync(runId, httpContext, resolver, service))
            .WithName("migration.validation.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.validation.read")));

        endpoints.MapGet(
            "/api/v1/migrations/{runId:guid}/validation/findings",
            async (Guid runId, int? offset, int? pageSize, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service) =>
                await ExecuteFindingsReadAsync(runId, offset ?? 0, pageSize ?? 100, httpContext, resolver, service))
            .WithName("migration.validation.findings.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.validation.findings.read")));

        endpoints.MapGet(
            "/api/v1/migrations/{runId:guid}/staged-records",
            async (Guid runId, int? offset, int? pageSize, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service) =>
                await ExecuteStagedReadAsync(runId, offset ?? 0, pageSize ?? 100, httpContext, resolver, service))
            .WithName("migration.staged-records.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.staged-records.read")));

        endpoints.MapPost(
            "/api/v1/migrations/{runId:guid}/dry-run",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service) =>
                await ExecuteMutationAsync(runId, httpContext, resolver, service, dryRun: true))
            .WithName("migration.dry-run.start")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.dry-run.start")));

        endpoints.MapGet(
            "/api/v1/migrations/{runId:guid}/dry-run",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service) =>
                await ExecuteDryRunReadAsync(runId, httpContext, resolver, service))
            .WithName("migration.dry-run.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.dry-run.read")));

        endpoints.MapPost(
            "/api/v1/migrations/{runId:guid}/execution",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationExecutionService service) =>
            await ExecuteMutationAsync(runId, httpContext, resolver, service))
            .WithName("migration.execution.start")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.execution.start")))
            .Produces<MigrationExecutionResponse>(StatusCodes.Status200OK);

        endpoints.MapGet(
            "/api/v1/migrations/{runId:guid}/execution",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationExecutionService service) =>
            await ExecuteExecutionReadAsync(runId, httpContext, resolver, service))
            .WithName("migration.execution.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.execution.read")))
            .Produces<MigrationExecutionResponse>(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/migrations/{runId:guid}/reconciliation",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service) =>
            await ExecuteMutationAsync(runId, httpContext, resolver, validation, service))
            .WithName("migration.reconciliation.calculate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.reconciliation.calculate")))
            .Produces<MigrationReconciliationResponse>(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/migrations/{runId:guid}/reconciliation",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service) =>
            await ExecuteReconciliationReadAsync(runId, httpContext, resolver, validation, service))
            .WithName("migration.reconciliation.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.reconciliation.read")))
            .Produces<MigrationReconciliationResponse>(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/migrations/{runId:guid}/reconciliation/{reconciliationId:guid}/approvals",
            async (Guid runId, Guid reconciliationId, MigrationApprovalDecisionRequest? request, HttpContext httpContext,
                ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service) =>
            await ExecuteMutationAsync(runId, reconciliationId, request, httpContext, resolver, validation, service))
            .WithName("migration.reconciliation.approve")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.reconciliation.approve")))
            .Produces<MigrationReconciliationApprovalResponse>(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v1/migrations/{runId:guid}/reconciliation/{reconciliationId:guid}/readiness",
            async (Guid runId, Guid reconciliationId, HttpContext httpContext, ITrustedRequestContextResolver resolver,
                MigrationValidationService validation, MigrationReconciliationService service) =>
            await ExecuteMutationAsync(runId, reconciliationId, httpContext, resolver, validation, service))
            .WithName("migration.handover.ready")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.handover.ready")))
            .Produces<MigrationHandoverReadinessResponse>(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/migrations/{runId:guid}/readiness",
            async (Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service) =>
            await ExecuteReadinessReadAsync(runId, httpContext, resolver, validation, service))
            .WithName("migration.handover.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("migration.handover.read")))
            .Produces<MigrationHandoverReadinessResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> ExecuteMutationAsync(Guid runId, HttpContext httpContext,
        ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service)
    {
        const string operationId = "migration.reconciliation.calculate";
        var context = await ResolveMutationContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null) return context.Error;
        if (!await validation.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", operationId);
        if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
            return Problem(httpContext, 400, "if_match_required", "If-Match required", "A valid If-Match run version is required.", operationId);
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key))
            return Problem(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", operationId);
        var result = await service.ReconcileAsync(context.Value!, new MigrationReconcileRequest(runId, expectedVersion, key!), httpContext.RequestAborted);
        if (result.Value is { } value) httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(value.Version)}\"";
        return MutationResponse(httpContext, result, operationId, "Migration reconciliation failed", ToResponse);
    }

    private static async Task<IResult> ExecuteReconciliationReadAsync(Guid runId, HttpContext httpContext,
        ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service)
    {
        const string operationId = "migration.reconciliation.read";
        var context = await ResolveReadContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null) return context.Error;
        if (!await validation.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", operationId);
        var value = await service.ReadAsync(context.Value!, runId, httpContext.RequestAborted);
        if (value is null) return Problem(httpContext, 404, "migration_reconciliation_not_found", "Not found", "The migration reconciliation was not found.", operationId);
        httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(value.Version)}\"";
        return Results.Json(ToResponse(value));
    }

    private static async Task<IResult> ExecuteMutationAsync(Guid runId, Guid reconciliationId, MigrationApprovalDecisionRequest? request,
        HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service)
    {
        const string operationId = "migration.reconciliation.approve";
        if (request is null) return Problem(httpContext, 400, "validation_failed", "Validation failed", "An approval decision is required.", operationId);
        var context = await ResolveMutationContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null) return context.Error;
        if (!await validation.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", operationId);
        if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
            return Problem(httpContext, 400, "if_match_required", "If-Match required", "A valid If-Match reconciliation version is required.", operationId);
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key))
            return Problem(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", operationId);
        var result = await service.ApproveAsync(context.Value!, new MigrationApprovalRequest(runId, reconciliationId, request.Domain, request.RequirementKey,
            request.Decision, request.Reason, expectedVersion, key!), httpContext.RequestAborted);
        return MutationResponse(httpContext, result, operationId, "Migration approval failed", ToResponse);
    }

    private static async Task<IResult> ExecuteMutationAsync(Guid runId, Guid reconciliationId, HttpContext httpContext,
        ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service)
    {
        const string operationId = "migration.handover.ready";
        var context = await ResolveMutationContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null) return context.Error;
        if (!await validation.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", operationId);
        if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
            return Problem(httpContext, 400, "if_match_required", "If-Match required", "A valid If-Match reconciliation version is required.", operationId);
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key))
            return Problem(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", operationId);
        var result = await service.CreateReadinessAsync(context.Value!, new MigrationHandoverRequest(runId, reconciliationId, expectedVersion, key!), httpContext.RequestAborted);
        return MutationResponse(httpContext, result, operationId, "Migration readiness failed", ToResponse);
    }

    private static async Task<IResult> ExecuteReadinessReadAsync(Guid runId, HttpContext httpContext,
        ITrustedRequestContextResolver resolver, MigrationValidationService validation, MigrationReconciliationService service)
    {
        const string operationId = "migration.handover.read";
        var context = await ResolveReadContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null) return context.Error;
        if (!await validation.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", operationId);
        var value = await service.ReadAsync(context.Value!, runId, httpContext.RequestAborted);
        return value?.Readiness is { } readiness
            ? Results.Json(ToResponse(readiness))
            : Problem(httpContext, 404, "migration_readiness_not_found", "Not found", "The migration readiness snapshot was not found.", operationId);
    }

    private static async Task<IResult> ExecuteValidationMutationAsync(
        Guid runId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MigrationValidationService service)
    {
        var operationId = MigrationValidationService.ValidationOperationId;
        var context = await ResolveMutationContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null)
            return context.Error;
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key))
            return Problem(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", operationId);
        var result = await service.ValidateAsync(context.Value!, runId, key!, httpContext.RequestAborted);
        return MutationResponse(httpContext, result, operationId, "Migration validation failed", value => ToValidationResponse(value));
    }

    private static Task<IResult> ExecuteMutationAsync(
        Guid runId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MigrationValidationService service,
        bool dryRun) => dryRun
            ? ExecuteDryRunMutationAsync(runId, httpContext, resolver, service)
            : ExecuteValidationMutationAsync(runId, httpContext, resolver, service);

    private static async Task<IResult> ExecuteDryRunMutationAsync(
        Guid runId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MigrationValidationService service)
    {
        var operationId = MigrationValidationService.DryRunOperationId;
        var context = await ResolveMutationContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null)
            return context.Error;
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key))
            return Problem(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", operationId);
        var result = await service.DryRunAsync(context.Value!, runId, key!, httpContext.RequestAborted);
        return MutationResponse(httpContext, result, operationId, "Migration dry-run failed", value => ToDryRunResponse(value));
    }

    private static async Task<IResult> ExecuteValidationReadAsync(Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service)
    {
        var context = await ResolveReadContextAsync(httpContext, resolver, "migration.validation.read");
        if (context.Error is not null) return context.Error;
        if (!await service.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", "migration.validation.read");
        var value = await service.ReadValidationAsync(context.Value!.TenantContext!, runId, httpContext.RequestAborted);
        return value is null ? Problem(httpContext, 404, "migration_validation_not_found", "Not found", "The migration validation result was not found.", "migration.validation.read") : Results.Json(ToValidationResponse(value));
    }

    private static async Task<IResult> ExecuteFindingsReadAsync(Guid runId, int offset, int pageSize, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service)
    {
        var context = await ResolveReadContextAsync(httpContext, resolver, "migration.validation.findings.read");
        if (context.Error is not null) return context.Error;
        if (!await service.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", "migration.validation.findings.read");
        var values = await service.ReadFindingsAsync(context.Value!.TenantContext!, runId, offset, pageSize, httpContext.RequestAborted);
        return Results.Json(values.Select(item => new { item.FindingId, item.RunId, item.AttemptId, item.StagedRecordId, item.Category, item.Severity, item.IsBlocking, item.Code, item.Message, item.ReferenceId, item.CreatedAt }));
    }

    private static async Task<IResult> ExecuteStagedReadAsync(Guid runId, int offset, int pageSize, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service)
    {
        var context = await ResolveReadContextAsync(httpContext, resolver, "migration.staged-records.read");
        if (context.Error is not null) return context.Error;
        if (!await service.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", "migration.staged-records.read");
        var values = await service.ReadStagedRecordsAsync(context.Value!.TenantContext!, runId, offset, pageSize, httpContext.RequestAborted);
        return Results.Json(values.Select(item => new { item.StagedRecordId, item.RunId, item.SourceSequence, item.SourceRecordId, item.RecordType, item.PayloadHash, item.PackageHash, item.PackageVersion, item.SourceObjectId, item.SourceSnapshotHash, item.CapturedAt }));
    }

    private static async Task<IResult> ExecuteDryRunReadAsync(Guid runId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MigrationValidationService service)
    {
        var context = await ResolveReadContextAsync(httpContext, resolver, "migration.dry-run.read");
        if (context.Error is not null) return context.Error;
        if (!await service.IsResourceAuthorizedAsync(context.Value!, runId, httpContext.RequestAborted))
            return Problem(httpContext, 403, "migration_source_scope_denied", "Forbidden", "The migration source is outside the current organization scope.", "migration.dry-run.read");
        var value = await service.ReadDryRunAsync(context.Value!.TenantContext!, runId, httpContext.RequestAborted);
        return value is null ? Problem(httpContext, 404, "migration_dry_run_not_found", "Not found", "The migration dry-run preview was not found.", "migration.dry-run.read") : Results.Json(ToDryRunResponse(value));
    }

    private static async Task<IResult> ExecuteMutationAsync(
        Guid runId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MigrationExecutionService service)
    {
        const string operationId = MigrationExecutionService.OperationId;
        var context = await ResolveMutationContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null)
            return context.Error;
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key))
            return Problem(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", operationId);
        if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
            return Problem(httpContext, 400, "if_match_required", "If-Match required", "A valid If-Match run version is required.", operationId);
        var result = await service.ExecuteAsync(context.Value!, runId, key!, expectedVersion, httpContext.RequestAborted);
        return MutationResponse(httpContext, result, operationId, "Migration execution failed", ToExecutionResponse);
    }

    private static async Task<IResult> ExecuteExecutionReadAsync(
        Guid runId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MigrationExecutionService service)
    {
        const string operationId = "migration.execution.read";
        var context = await ResolveReadContextAsync(httpContext, resolver, operationId);
        if (context.Error is not null)
            return context.Error;
        var value = await service.ReadAsync(context.Value!, runId, httpContext.RequestAborted);
        return value is null
            ? Problem(httpContext, 404, "migration_execution_not_found", "Not found", "The migration execution result was not found.", operationId)
            : Results.Json(ToExecutionResponse(value));
    }

    private static async Task<IResult> ExecuteMutationAsync(
        MigrationIntakeCreateRequest? request,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MigrationIntakeService service)
    {
        const string operationId = "migration.intake.create";
        if (request is null)
        {
            return Problem(httpContext, 400, "validation_failed", "Validation failed", "A migration intake request is required.", operationId);
        }

        if (!await EnsureAntiforgeryAsync(httpContext))
        {
            return Problem(httpContext, 403, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", operationId);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(idempotencyKey))
        {
            return Problem(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", operationId);
        }

        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        if (foundationContext.TenantContext is null)
        {
            return Problem(
                httpContext,
                foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403,
                "tenant_context_required",
                foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied",
                "The operation is not available for this security context.",
                operationId);
        }

        MigrationIntakeRegistrationRequest intake;
        try
        {
            intake = new MigrationIntakeRegistrationRequest(
                new MigrationDefinitionReference(request.DefinitionId!, request.DefinitionVersion!),
                new MigrationSourceProfileReference(request.SourceProfileId!, request.SourceProfileVersion!),
                request.Operation,
                request.SourceObjectId);
        }
        catch (ArgumentException)
        {
            return Problem(httpContext, 400, "validation_failed", "Validation failed", "The migration intake request is invalid.", operationId);
        }

        var result = await service.RegisterAsync(
            foundationContext,
            intake,
            idempotencyKey!,
            httpContext.RequestAborted);
        if (result.Succeeded && result.Value is { } value)
        {
            if (result.Kind == MigrationResultKind.Replayed)
            {
                httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
            }

            return Results.Json(ToResponse(value), statusCode: 200);
        }

        return Problem(
            httpContext,
            StatusCode(result),
            result.Code,
            result.Kind == MigrationResultKind.UnknownOutcome ? "Operation unavailable" : "Migration intake failed",
            "The migration intake could not be completed.",
            operationId);
    }

    private static async Task<(FoundationRequestContext? Value, IResult? Error)> ResolveMutationContextAsync(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        string operationId)
    {
        if (!await EnsureAntiforgeryAsync(httpContext))
            return (null, Problem(httpContext, 403, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", operationId));
        return await ResolveReadContextAsync(httpContext, resolver, operationId);
    }

    private static async Task<(FoundationRequestContext? Value, IResult? Error)> ResolveReadContextAsync(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        string operationId)
    {
        var context = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        if (context.TenantContext is null)
        {
            return (null, Problem(
                httpContext,
                context.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403,
                "tenant_context_required",
                context.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied",
                "The operation is not available for this security context.",
                operationId));
        }
        return (context, null);
    }

    private static IResult MutationResponse<T>(
        HttpContext httpContext,
        MigrationOperationResult<T> result,
        string operationId,
        string failureTitle,
        Func<T, object> project)
    {
        if (result.Succeeded && result.Value is { } value)
        {
            if (result.Kind == MigrationResultKind.Replayed)
                httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
            return Results.Json(project(value), statusCode: 200);
        }

        return Problem(
            httpContext,
            StatusCode(result),
            result.Code,
            result.Kind == MigrationResultKind.UnknownOutcome ? "Operation unavailable" : failureTitle,
            "The migration operation could not be completed.",
            operationId);
    }

    private static object ToValidationResponse(MigrationValidationSummary value) => new
    {
        value.ValidationResultId,
          tenantId = value.TenantId.Value,
        value.RunId,
        value.AttemptId,
        value.PackageHash,
        value.SourceSnapshotHash,
        value.TotalStagedRecords,
        value.AcceptedCount,
        value.RejectedCount,
        value.QuarantinedCount,
        value.FindingCounts,
        value.Records,
        value.IsValid,
        value.CompletedAt
    };

    private static object ToDryRunResponse(MigrationDryRunPreview value) => new
    {
        value.PreviewId,
          tenantId = value.TenantId.Value,
        value.RunId,
        value.AttemptId,
        value.ValidationAttemptId,
        value.PackageHash,
        value.SourceSnapshotHash,
        value.TotalStagedRecords,
        value.AcceptedCount,
        value.RejectedCount,
        value.QuarantinedCount,
        value.FindingCounts,
        value.ControlTotals,
        value.UnresolvedDependencyCount,
        value.ExceptionCount,
        value.Rows,
        value.CompletedAt,
        zeroAuthoritativeBusinessEffect = true
    };

    private static MigrationExecutionResponse ToExecutionResponse(MigrationExecutionResult value) => new(
        value.RunId,
        value.TenantId.Value,
        value.AttemptId,
        value.FingerprintVersion,
        value.Fingerprint,
        value.RunStatus,
        value.AttemptOutcome,
        value.OutcomeCode,
        value.Batches.Select(item => new MigrationExecutionBatchResponse(item.Id, item.TenantId.Value, item.RunId, item.AttemptId,
            item.RecordType, item.State, item.OwnerBatchId, item.Fingerprint, item.CreatedAt, item.StartedAt, item.CompletedAt,
            item.CorrelationId, item.Version)).ToArray(),
        value.Effects.Select(item => new MigrationExecutionEffectResponse(item.Id, item.TenantId.Value, item.RunId, item.AttemptId,
            item.StagedRecordId, item.SourceSequence, item.RecordType, item.OwnerBatchId, item.OwnerRowId, item.ResultingResourceId,
            item.ResultingResourceCode, item.Disposition, item.SafeCode, item.CreatedAt, item.EffectStartedAt, item.CompletedAt,
            item.CorrelationId, item.Version)).ToArray(),
        value.Representations?.Select(item => new MigrationEconomicRepresentationResponse(item.Id, item.TenantId.Value, item.RunId,
            item.AttemptId, item.EffectId, item.OwnerModule, item.Kind, item.OwnerId, item.OwnerReference, item.Status,
            item.EvidenceVersion, item.OccurredAt, item.RecordedAt, item.EvidenceConfirmed, item.Version, item.SourceContract,
            item.SourceEvent, item.FunctionalAmount, item.PostingRuleId, item.PostingRuleVersionNumber, item.ControlAccountId,
            item.OffsetAccountId, item.Reversal, item.SourceEvidenceId, item.SourceEvidenceVersion, item.OwnerSourceId,
            item.TransactionCurrencyCode, item.TransactionAmount, item.ExpectedFunctionalCurrencyCode, item.RateDate,
            item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.AppliedRate,
            item.MonetaryPolicyId, item.MonetaryPolicyVersionNumber, item.RoundingScale, item.RoundingMode,
            item.ReportingCurrencyCode, item.ReportingExchangeRateId, item.ReportingExchangeRateVersionId,
            item.ReportingExchangeRateVersionNumber, item.ReportingAppliedRate)).ToArray(),
        value.EconomicReconciliations,
        value.ArEconomicReconciliations,
        value.ApEconomicReconciliations,
        value.CashBankEconomicReconciliations,
        value.GlEconomicReconciliations,
        value.Representations is null or { Count: 0 });

    private static MigrationReconciliationResponse ToResponse(MigrationReconciliationRecord value) => new(
        value.Id, value.TenantId.Value, value.RunId, value.AttemptId, value.VersionNumber, value.EvidenceFingerprint,
        value.IdempotencyKey, value.Status, value.CreatedAt, value.CalculatedAt, value.SubmittedCount, value.AcceptedCount,
        value.RejectedCount, value.DuplicateCount, value.SkippedCount, value.QuarantinedCount, value.UnresolvedCount,
        value.RequiredApprovalCount, value.ObtainedApprovalCount, value.SourceDebit, value.SourceCredit, value.TargetDebit,
        value.TargetCredit, value.Variance, value.Version, value.IsCurrent, value.ApprovalPolicyId,
        value.ApprovalPolicyVersion, value.ApprovalPolicyCode, value.ApprovalPolicyEffectiveFrom,
        value.ApprovalPolicyEffectiveTo, value.ApprovalEnforcesSeparationOfDuties, value.Requirements,
        value.Details.Select(ToResponse).ToArray(),
        value.Approvals.Select(ToResponse).ToArray(), value.Readiness is null ? null : ToResponse(value.Readiness));

    private static MigrationReconciliationDetailResponse ToResponse(MigrationReconciliationDetail value) => new(
        value.Id, value.Domain, value.ScopeKey, value.CompanyId, value.OpeningDate, value.CurrencyCode,
        value.TransactionCurrencyCode, value.FunctionalCurrencyCode, value.SourceCount, value.SourceDebit, value.SourceCredit,
        value.TargetDebit, value.TargetCredit, value.Variance, value.SourceAmount, value.TargetAmount, value.AmountVariance,
        value.OwnerRoundingDifference, value.TransactionAmount, value.FunctionalAmount, value.SubsidiaryEstablishedAmount,
        value.GlControlAmount, value.ExchangeRateId, value.ExchangeRateVersionId, value.ExchangeRateVersionNumber,
        value.AppliedRate, value.SourceQuantity, value.TargetQuantity, value.QuantityVariance, value.ControlAccountId,
        value.PostingRuleId, value.PostingRuleVersionNumber, value.OwnerSourceId, value.WarehouseId, value.ProductId,
        value.UnitOfMeasureId, value.SourceContract, value.SourceEvent, value.RoundingPolicyId,
        value.RoundingPolicyVersionNumber, value.RoundingScale, value.RoundingMode, value.IsBlocking, value.FindingCode,
        value.Explanation, value.EffectId, value.OwnerReferenceId, value.LinkedAccountId);

    private static MigrationReconciliationApprovalResponse ToResponse(MigrationReconciliationApprovalRecord value) => new(
        value.Id, value.TenantId.Value, value.RunId, value.ReconciliationId, value.ReconciliationVersion, value.AttemptId,
        value.EvidenceFingerprint, value.IdempotencyKey, value.Domain, value.RequirementKey, value.PolicyId,
        value.PolicyVersion, value.ActorId, value.Decision, value.Reason, value.DecidedAt, value.Version, value.EvidenceConfirmed);

    private static MigrationHandoverReadinessResponse ToResponse(MigrationHandoverReadinessSnapshot value) => new(
        value.Id, value.TenantId.Value, value.RunId, value.ReconciliationId, value.ReconciliationVersion, value.AttemptId,
        value.EvidenceFingerprint, value.IdempotencyKey, value.CreatedAt, value.BusinessReady, value.ProductionReady,
        value.Mesp48Complete, value.Mesp50Complete, value.TenantActivationPerformed, value.ResultCode, value.Version);

    private static object ToResponse(MigrationIntakeRecord record) => new
    {
        runId = record.Run.RunId,
        tenantId = record.Run.TenantId.Value,
        status = record.Run.Status,
        operation = record.Operation,
        definitionId = record.Run.Definition.DefinitionId,
        definitionVersion = record.Run.Definition.Version,
        sourceProfileId = record.Run.SourceProfile.ProfileId,
        sourceProfileVersion = record.Run.SourceProfile.ProfileVersion,
        sourceObjectId = record.Source.ObjectId,
        sourceSha256 = record.Source.Sha256,
        sourceLength = record.Source.Length,
        sourceConcurrencyVersion = record.Source.ConcurrencyVersion,
        fingerprintVersion = record.FingerprintVersion,
        requestFingerprint = record.RequestFingerprint,
        capturedAt = record.CapturedAt,
        version = record.Version
    };

    private static int StatusCode<T>(MigrationOperationResult<T> result) => result.Code switch
    {
        "migration_source_not_found" => 404,
        "migration_source_expired" or
        "migration_source_disposed" or
        "migration_source_checksum_failed" or
        "migration_source_safety_blocked" or
        "migration_source_concurrency_conflict" or
        "migration_idempotency_conflict" => 409,
        "migration_source_scope_denied" => 403,
        "migration_validation_required" or
        "migration_validation_not_permitted" or
        "migration_dry_run_not_permitted" or
        "migration_validation_failed" => 409,
        "migration_run_not_found" or
        "migration_validation_not_found" or
        "migration_dry_run_not_found" or
        "migration_execution_not_found" => 404,
        "migration_execution_approval_required" or
        "migration_execution_blocking_row_present" or
        "migration_execution_record_type_not_supported" or
        "migration_execution_authoritative_snapshot_mismatch" or
        "migration_execution_state_invalid" => 409,
        "migration_execution_outcome_unknown" or
        "migration_owner_evidence_unavailable" or
        "migration_owner_effect_unproven" => 503,
        "migration_audit_evidence_unavailable" or
        "migration_intake_outcome_unknown" => 503,
        _ when result.Kind == MigrationResultKind.KnownFailure => 503,
        _ => 400
    };

    private static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext)
    {
        try
        {
            await httpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(httpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static bool TryReadExpectedVersion(HttpContext httpContext, out byte[] version)
    {
        version = [];
        var value = httpContext.Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            return false;
        value = value.Trim();
        if (value.Length > 1 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];
        try
        {
            version = Convert.FromBase64String(value);
            return version.Length is > 0 and <= 64;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string title,
        string detail,
        string operationId) => Results.Problem(
        statusCode: statusCode,
        title: title,
        detail: detail,
        type: $"https://api.minierp.local/problems/{code}",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = GetCorrelation(httpContext),
            ["operationId"] = operationId
        });

    private static string GetCorrelation(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId
            ? correlationId
            : FoundationCorrelation.Resolve(httpContext.Request);
}

#pragma warning restore CS1591
