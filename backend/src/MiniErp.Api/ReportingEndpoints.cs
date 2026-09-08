#pragma warning disable CS1591

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Reporting;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.Api;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/reporting/catalogue", (HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) => Read(http, resolver, auth, "reporting.catalogue.read", null, null, _ => Task.FromResult<IResult>(Results.Ok(service.Catalogue()))))
            .WithName("reporting.catalogue.read").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.catalogue.read")));

        endpoints.MapGet("/api/v1/reporting/reports/{reportCode}", (string reportCode, [AsParameters] ReportingQuery query, HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) => ExecuteReport(http, resolver, auth, service, reportCode, query))
            .WithName("reporting.report.execute").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.report.execute")));

        endpoints.MapPost("/api/v1/reporting/exports", async (ReportingExportRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) =>
        {
            if (request is null) return Problem(400, "request_required", "A reporting export request is required.");
            var definition = service.Catalogue().FirstOrDefault(item => string.Equals(item.Code, request.ReportCode, StringComparison.OrdinalIgnoreCase));
            return await ExecuteMutationAsync(http, resolver, auth, "reporting.report.export", definition, request.Query, false, async (context, key, _) =>
            {
                if (definition is null) return Problem(404, "report_not_found", "The reporting definition was not found.");
                var normalized = auth.NormalizeQuery(context, definition, request.Query);
                if (!normalized.Succeeded || normalized.Value is null) return Problem(StatusFor(normalized.Code), normalized.Code, "The report parameters are not authorized.");
                var result = await service.CreateExportJobAsync(context, request.ReportCode, normalized.Value, key, http.RequestAborted);
                return !result.Succeeded || result.Value is null ? Problem(StatusFor(result.Code), result.Code, "The reporting export was not completed.") : Results.Ok(result.Value);
            });
        }).WithName("reporting.report.export").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.report.export")));

        endpoints.MapGet("/api/v1/reporting/jobs/{jobId:guid}", (Guid jobId, HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) => Read(http, resolver, auth, "reporting.job.read", null, null, context =>
        {
            var job = service.FindJob(context, jobId);
            return Task.FromResult<IResult>(job is null ? Problem(404, "job_not_found", "The reporting job was not found.") : Results.Ok(job));
        })).WithName("reporting.job.read").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.job.read")));

        endpoints.MapGet("/api/v1/reporting/artifacts/{artifactId:guid}", async (Guid artifactId, HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) =>
        {
            var resolved = await Resolve(http, resolver, auth, "reporting.artifact.read", null, null);
            if (resolved.Error is not null) return resolved.Error;
            var result = await service.ReadArtifactAsync(resolved.Context!, artifactId, http.RequestAborted);
            return !result.Succeeded || result.Value is null
                ? Problem(404, result.Code, "The reporting artifact was not found.")
                : Results.File(result.Value.Content, result.Value.Artifact.ContentType, result.Value.Artifact.FileName);
        }).WithName("reporting.artifact.read").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.artifact.read")));

        endpoints.MapGet("/api/v1/reporting/schedules", (HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) => Read(http, resolver, auth, "reporting.schedule.list", null, null, context => Task.FromResult<IResult>(Results.Ok(service.ListSchedules(context)))))
            .WithName("reporting.schedule.list").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.schedule.list")));

        endpoints.MapPost("/api/v1/reporting/schedules", async (ReportingScheduleRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) =>
        {
            if (request is null) return Problem(400, "request_required", "A reporting schedule request is required.");
            var definition = service.Catalogue().FirstOrDefault(item => string.Equals(item.Code, request.ReportCode, StringComparison.OrdinalIgnoreCase));
            return await ExecuteMutationAsync(http, resolver, auth, "reporting.schedule.create", definition, request.Query, false, async (context, key, _) =>
            {
                if (definition is null) return Problem(404, "report_not_found", "The reporting definition was not found.");
                var normalized = auth.NormalizeQuery(context, definition, request.Query);
                if (!normalized.Succeeded || normalized.Value is null) return Problem(StatusFor(normalized.Code), normalized.Code, "The report parameters are not authorized.");
                var result = await service.CreateScheduleAsync(context, request.ReportCode, normalized.Value, request.Recurrence, request.TimeZone, request.DestinationKind, key, http.RequestAborted);
                return !result.Succeeded || result.Value is null ? Problem(StatusFor(result.Code), result.Code, "The reporting schedule was not created.") : Results.Ok(result.Value);
            });
        }).WithName("reporting.schedule.create").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.schedule.create")));

        endpoints.MapPost("/api/v1/reporting/schedules/{scheduleId:guid}/status", async (Guid scheduleId, ReportingScheduleStatusRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service) =>
        {
            if (request is null || !Enum.TryParse<ReportingScheduleStatus>(request.Status, true, out var status)) return Problem(422, "schedule_status_invalid", "A valid schedule status is required.");
            return await ExecuteMutationAsync(http, resolver, auth, "reporting.schedule.update", null, null, true, async (context, key, version) =>
            {
                var result = await service.SetScheduleStatusAsync(context, scheduleId, status, version!, key, http.RequestAborted);
                if (!result.Succeeded || result.Value is null) return Problem(StatusFor(result.Code), result.Code, "The reporting schedule was not updated.");
                http.Response.Headers.ETag = $"\"{Convert.ToBase64String(result.Value.Version)}\"";
                return Results.Ok(result.Value);
            });
        }).WithName("reporting.schedule.update").WithTags("Reporting").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("reporting.schedule.update")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReport(HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, IReportingService service, string reportCode, ReportingQuery query)
    {
        var definition = service.Catalogue().FirstOrDefault(item => string.Equals(item.Code, reportCode, StringComparison.OrdinalIgnoreCase));
        var resolved = await Resolve(http, resolver, auth, "reporting.report.execute", definition, query);
        if (resolved.Error is not null) return resolved.Error;
        var normalized = definition is null ? ReportingOperationResult<ReportingQuery>.Failure("report_not_found") : auth.NormalizeQuery(resolved.Context!, definition, query);
        if (!normalized.Succeeded || normalized.Value is null) return Problem(StatusFor(normalized.Code), normalized.Code, "The report parameters are not authorized.");
        try
        {
            var result = await service.ExecuteAsync(resolved.Context!, reportCode, normalized.Value, http.RequestAborted);
            return result is null ? Problem(404, "report_not_found", "The reporting definition was not found.") : Results.Ok(result);
        }
        catch (ArgumentException) { return Problem(422, "validation_failed", "The report parameters are not valid."); }
        catch { return Problem(503, "reporting_unavailable", "The reporting source is temporarily unavailable."); }
    }

    private static async Task<IResult> Read(HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, string operation, ReportingDefinition? definition, ReportingQuery? query, Func<ReportingRequestContext, Task<IResult>> action)
    {
        var resolved = await Resolve(http, resolver, auth, operation, definition, query);
        if (resolved.Error is not null) return resolved.Error;
        try { return await action(resolved.Context!); }
        catch { return Problem(503, "reporting_unavailable", "The reporting source is temporarily unavailable."); }
    }

    private static async Task<IResult> ExecuteMutationAsync(HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, string operation, ReportingDefinition? definition, ReportingQuery? query, bool requiresVersion, Func<ReportingRequestContext, string, byte[]?, Task<IResult>> action)
    {
        if (!await ValidateAntiforgery(http)) return Problem(403, "antiforgery_failed", "The request could not be validated.");
        var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key)) return Problem(400, "idempotency_key_invalid", "A valid Idempotency-Key is required.");
        byte[]? version = null;
        if (requiresVersion && !TryIfMatch(http, out version)) return Problem(400, "if_match_required", "A valid If-Match version is required.");
        var resolved = await Resolve(http, resolver, auth, operation, definition, query);
        if (resolved.Error is not null) return resolved.Error;
        try { return await action(resolved.Context!, key!, version); }
        catch (ArgumentException) { return Problem(422, "validation_failed", "The reporting parameters are not valid."); }
        catch { return Problem(503, "reporting_unavailable", "The reporting source is temporarily unavailable."); }
    }

    private static async Task<(ReportingRequestContext? Context, IResult? Error)> Resolve(HttpContext http, ITrustedRequestContextResolver resolver, ReportingAuthorizationService auth, string operation, ReportingDefinition? definition, ReportingQuery? query)
    {
        var foundation = await resolver.ResolveAsync(http, http.RequestAborted);
        var decision = auth.Resolve(foundation, operation, definition, query);
        if (decision.Succeeded && decision.Value is not null) return (decision.Value, null);
        var status = decision.Code == "authentication_required" ? 401 : StatusFor(decision.Code);
        return (null, Problem(status, decision.Code, "The reporting operation is not available for this security context."));
    }

    private static async Task<bool> ValidateAntiforgery(HttpContext http)
    {
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static bool TryIfMatch(HttpContext http, out byte[] version)
    {
        version = [];
        var value = http.Request.Headers.IfMatch.FirstOrDefault()?.Trim('"');
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { version = Convert.FromBase64String(value); return version.Length > 0; }
        catch (FormatException) { return false; }
    }

    private static int StatusFor(string code) => code is "permission_denied" or "company_scope_denied" or "branch_scope_denied" or "warehouse_scope_denied" or "scope_denied" or "scope_invalid" ? 403 : code.Contains("not_found", StringComparison.OrdinalIgnoreCase) ? 404 : code is "concurrency_conflict" or "idempotency_conflict" ? 409 : code is "source_unavailable" ? 503 : 422;
    private static IResult Problem(int status, string code, string detail) => Results.Problem(detail, statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });

    public sealed record ReportingExportRequest(string ReportCode, ReportingQuery Query);
    public sealed record ReportingScheduleRequest(string ReportCode, ReportingQuery Query, string Recurrence, string TimeZone, string DestinationKind = "local-test-sink");
    public sealed record ReportingScheduleStatusRequest(string Status);
}

#pragma warning restore CS1591
