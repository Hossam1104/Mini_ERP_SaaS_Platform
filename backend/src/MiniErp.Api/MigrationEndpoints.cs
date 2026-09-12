#pragma warning disable CS1591

using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Migration;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.Api;

/// <summary>REST adapter for the bounded MESP-141 Slice 2 intake boundary.</summary>
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

        return endpoints;
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

    private static int StatusCode(MigrationOperationResult<MigrationIntakeRecord> result) => result.Code switch
    {
        "migration_source_not_found" => 404,
        "migration_source_expired" or
        "migration_source_disposed" or
        "migration_source_checksum_failed" or
        "migration_source_safety_blocked" or
        "migration_source_concurrency_conflict" or
        "migration_idempotency_conflict" => 409,
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
