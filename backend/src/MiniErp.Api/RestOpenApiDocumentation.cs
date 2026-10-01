#pragma warning disable CS1591

using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.Api;

/// <summary>
/// Project-wide generated OpenAPI identity and boundary statement. The
/// runtime document remains generated from mapped endpoints; this transformer
/// supplies the durable developer-facing contract that minimal handlers do
/// not infer on their own.
/// </summary>
public sealed class MiniErpOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Mini ERP SaaS Platform API",
            Version = context.DocumentName,
            Description = "Generated first-party REST contract for the reusable B2B ERP platform. "
                + "Requests are resolved against server-derived authentication, Tenant ownership, "
                + "permission, scope, correlation, antiforgery, idempotency, and optimistic-concurrency "
                + "facts. Mutation evidence is mandatory where the operation catalogue says so. "
                + "Master Data Tax/VAT behavior is internal configuration-led reference data and a "
                + "deterministic engine contract over an explicit taxable base and other explicit inputs; this API does not claim statutory "
                + "certification, government submission, external provider connectivity, Finance posting, "
                + "or posted-document correction behavior."
        };

        SetModuleTags(document);
        AddSelfDocumentOperation(document);
        MiniErpOpenApiSchemaTransformer.DescribeUnannotatedComponentProperties(document);

        return Task.CompletedTask;
    }

    private static void SetModuleTags(OpenApiDocument document)
    {
        document.Tags ??= new HashSet<OpenApiTag>();
        document.Tags.Clear();
        document.Tags.Add(new OpenApiTag
        {
            Name = "Foundation/Auth",
            Description = "Shared REST foundation, authorization-context resolution, audit evidence search, and notification intent dispatch. This tag groups cross-cutting services; it does not grant Tenant or Platform authority."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Identity",
            Description = "Authentication, server-side sessions, membership-derived context candidates, and authorized Tenant or organization context selection."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Master Data",
            Description = "Reusable Tenant reference data and controlled imports, including product, pricing, tax, currency, exchange-rate, and payment-term configuration. Supplier and Business Customer identities remain owned by Business Parties."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Procurement",
            Description = "Purchase Requests, Supplier Quotations, Purchase Orders, supplier confirmations, receipt evidence, invoice handoff, matching, and supplier returns. Procurement owns source documents, not stock truth or accounting."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Inventory",
            Description = "Tenant and organization-scoped stock movements, stock ledger, stock control, warehouse processes, and moving-weighted-average valuation. Finance journals remain owned by Finance."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Sales",
            Description = "B2B quotations, orders, credit controls, reservations, fulfillment, delivery, invoice eligibility, and Customer Returns. Sales owns its source and fulfillment records, not accounting truth."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Finance",
            Description = "Company books, chart of accounts, fiscal periods, journals and GL, posting rules, AP, AR, cash and bank, settlements, FX, revaluation, close, and reconciliation. Finance alone owns accounting postings."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Reporting",
            Description = "Authorized read models, reports, exports, and report schedules. Reporting does not own or mutate transactional ledgers."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Migration",
            Description = "Tenant migration intake, validation, quarantine, dry-run, execution orchestration, opening data, reconciliation, approval, and handover evidence. Migration does not activate a Tenant or replace Finance and Inventory algorithms."
        });
        document.Tags.Add(new OpenApiTag
        {
            Name = "Platform",
            Description = "Platform availability, module registration, and Platform administration or Tenant lifecycle operations. Platform administration alone does not authorize access to Tenant ERP data."
        });
    }

    private static void AddSelfDocumentOperation(OpenApiDocument document)
    {
        var descriptor = FoundationOperationCatalog.GetRequired("platform.openapi");
        var operations = new Dictionary<System.Net.Http.HttpMethod, OpenApiOperation>();
        if (document.Paths.TryGetValue(descriptor.Route, out var existingPath)
            && existingPath is OpenApiPathItem concretePath
            && concretePath.Operations is not null)
        {
            foreach (var (method, existingOperation) in concretePath.Operations)
            {
                if (existingOperation is not null)
                {
                    operations[method] = existingOperation;
                }
            }
        }

        operations[System.Net.Http.HttpMethod.Get] = new OpenApiOperation
        {
            OperationId = descriptor.OperationId,
            Summary = "Read the generated API contract",
            Description = "Purpose: returns the generated OpenAPI document for the first-party REST API. Owner: Platform; responsibility: platform availability, module registration, and platform administration. Preconditions and state transition: the Development/QA API host must be reachable; this read does not transition state. Side effects: none. Authorization: anonymous, with no Tenant or Platform scope and no permission. Tenant boundary: no Tenant ERP data is read, and a Tenant identifier cannot grant authority. Headers and effective date: no idempotency, concurrency, or effective-date input is required. Errors: this route returns the generated document when available; it does not enable Scalar agent actions or make Scalar available in Production.",
            Tags = new HashSet<OpenApiTagReference> { new("Platform", document, null) },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "The generated OpenAPI document." }
            }
        };
        document.Paths[descriptor.Route] = new OpenApiPathItem { Operations = operations };
    }
}

/// <summary>
/// Documents every mapped public operation from the immutable Foundation
/// operation catalogue. This avoids undocumented minimal-API handlers while
/// preserving the catalogue as the source of permission and boundary truth.
/// </summary>
public sealed class MiniErpOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<FoundationOperationMetadata>()
            .SingleOrDefault();
        if (metadata is null || metadata.Visibility != FoundationOperationVisibility.Public)
        {
            return Task.CompletedTask;
        }

        var descriptor = metadata.Descriptor;
        operation.OperationId = descriptor.OperationId;
        operation.Summary = SummaryFor(descriptor.OperationId, descriptor.HttpMethod);
        operation.Description = DescriptionFor(descriptor);
        SetModuleTag(operation, descriptor, context.Document ?? throw new InvalidOperationException("OpenAPI operation has no document context."));
        DescribeContractHeaders(operation, descriptor);
        AddExchangeRateReferenceParameter(operation, descriptor);
        AddPriceListReferenceParameters(operation, descriptor);
        DescribeParameters(operation, descriptor);
        operation.Responses ??= new OpenApiResponses();
        // Describe the success status the endpoint declares (e.g. 202, 204); untyped endpoints default to 200.
        var successStatus = operation.Responses.Keys.FirstOrDefault(key => key.StartsWith('2')) ?? "200";
        SetResponse(operation, successStatus, SuccessResponseFor(descriptor), isProblem: false);
        SetResponse(operation, "400", "The request body, route/query input, effective-date input, required concurrency value, or business validation is invalid. Common codes include `validation_failed`, `idempotency_key_invalid`, `version_required`, and `if_match_required`; operation-specific validation codes are returned unchanged.");
        SetResponse(operation, "401", "Authentication is required for a protected operation or the presented first-party session is not valid. Common codes include `authentication_required` and `authentication_failed`.");
        SetResponse(operation, "403", "The server-derived permission, Tenant membership, organization scope, support grant, Platform context, or antiforgery check denies the request. Common codes include `permission_denied`, `access_denied`, `scope_denied`, `company_scope_denied`, `branch_scope_denied`, `warehouse_scope_denied`, and `antiforgery_failed`.");
        SetResponse(operation, "404", "The requested Tenant-owned resource or effective-dated version is absent or belongs to another Tenant. Foreign-Tenant resources are indistinguishable from missing resources. Resource-specific `*_not_found` codes are returned by the owning module.");
        SetResponse(operation, "409", "The request conflicts with current resource state, a duplicate identity, an idempotency binding, or an optimistic-concurrency version. Common codes include `idempotency_conflict` and `concurrency_conflict`; the owning module preserves its operation-specific state-conflict code.");
        SetResponse(operation, "503", "A required persistence, authorization, audit, or module boundary is unavailable, so no successful effect is claimed. Common codes include `persistence_unavailable`, `audit_unavailable`, and module-specific `*_unavailable` codes.");
        if (descriptor.OperationId.StartsWith("finance.", StringComparison.Ordinal)
            || descriptor.OperationId.StartsWith("reporting.", StringComparison.Ordinal))
        {
            SetResponse(operation, "422", "The request is structurally readable but the owning Finance or Reporting rule rejects its business values or current state. The endpoint returns the exact validation or domain result code, such as `schedule_status_invalid` for an unsupported Reporting schedule status.");
        }
        if (operation.Responses.ContainsKey("502"))
        {
            SetResponse(operation, "502", "The downstream delivery path failed after the request was authorized; the body is the operation's own result, not a Problem Details document.", isProblem: false);
        }

        return Task.CompletedTask;
    }

    private static void AddExchangeRateReferenceParameter(OpenApiOperation operation, FoundationOperationDescriptor descriptor)
    {
        if (descriptor.OperationId != "master-data.exchange-rate.reference.read")
        {
            return;
        }

        operation.Parameters ??= [];
        var parameter = operation.Parameters
            .OfType<OpenApiParameter>()
            .SingleOrDefault(item => item.Name == "effectiveOn" && item.In == ParameterLocation.Query);

        if (parameter is null)
        {
            parameter = new OpenApiParameter
            {
                Name = "effectiveOn",
                In = ParameterLocation.Query,
                Required = true
            };
            operation.Parameters.Add(parameter);
        }

        parameter.Description = "Required ISO 8601 calendar date used to select exactly one effective-dated version.";
        parameter.Required = true;
        parameter.Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "date"
        };
    }

    private static void AddPriceListReferenceParameters(OpenApiOperation operation, FoundationOperationDescriptor descriptor)
    {
        if (descriptor.OperationId != "master-data.price-list.reference.read")
        {
            return;
        }

        operation.Parameters ??= [];
        AddQueryParameter(operation, "priceListId", "Optional Tenant-owned Price List identity used to constrain resolution.", JsonSchemaType.String, "uuid", false);
        AddQueryParameter(operation, "productId", "Required existing active Product identity.", JsonSchemaType.String, "uuid", true);
        AddQueryParameter(operation, "unitOfMeasureId", "Required existing active UOM identity; no implicit conversion is applied.", JsonSchemaType.String, "uuid", true);
        AddQueryParameter(operation, "currencyId", "Required existing active Currency identity; the selected Price List must match exactly.", JsonSchemaType.String, "uuid", true);
        AddQueryParameter(operation, "customerId", "Optional same-Tenant Business Customer applicability input.", JsonSchemaType.String, "uuid", false);
        AddQueryParameter(operation, "organizationScopeKind", "Optional Company or Branch applicability kind.", JsonSchemaType.String, null, false);
        AddQueryParameter(operation, "organizationScopeId", "Optional server-authorized Company or Branch applicability identity.", JsonSchemaType.String, "uuid", false);
        AddQueryParameter(operation, "effectiveOn", "Required ISO 8601 calendar date used to select exactly one effective-dated price.", JsonSchemaType.String, "date", true);
    }

    private static void AddQueryParameter(OpenApiOperation operation, string name, string description, JsonSchemaType type, string? format, bool required)
    {
        operation.Parameters ??= [];
        var parameter = operation.Parameters
            .OfType<OpenApiParameter>()
            .SingleOrDefault(item => item.Name == name && item.In == ParameterLocation.Query);
        if (parameter is null)
        {
            parameter = new OpenApiParameter { Name = name, In = ParameterLocation.Query };
            operation.Parameters.Add(parameter);
        }

        parameter.Description = description;
        parameter.Required = required;
        parameter.Schema = new OpenApiSchema { Type = type, Format = format };
    }

    private static void SetModuleTag(OpenApiOperation operation, FoundationOperationDescriptor descriptor, OpenApiDocument document)
    {
        var moduleTag = descriptor.OperationId.Split('.', StringSplitOptions.RemoveEmptyEntries)[0] switch
        {
            "auth" => "Identity",
            "foundation" or "audit" or "notification" => "Foundation/Auth",
            "master-data" => "Master Data",
            "procurement" => "Procurement",
            "inventory" => "Inventory",
            "sales" => "Sales",
            "finance" => "Finance",
            "reporting" => "Reporting",
            "migration" => "Migration",
            "platform" => "Platform",
            _ => throw new InvalidOperationException($"No OpenAPI module tag is defined for {descriptor.OperationId}.")
        };

        operation.Tags ??= new HashSet<OpenApiTagReference>();
        operation.Tags.Clear();
        operation.Tags.Add(new OpenApiTagReference(moduleTag, document, null));
    }

    private static void DescribeParameters(OpenApiOperation operation, FoundationOperationDescriptor descriptor)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                continue;
            }

            var name = parameter.Name ?? string.Empty;
            var normalized = name.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            parameter.Description = normalized switch
            {
                "idempotencykey" => "Stable key for this mutation. The server binds it to the authorized identity and request; reusing it for a different request conflicts.",
                "ifmatch" => "Current ETag/version of the resource. The server rejects a stale value and requires this header only where the operation declares optimistic concurrency.",
                "requestverificationtoken" or "xcsrf" => "Antiforgery evidence paired with the authenticated first-party session for an unsafe request.",
                "companyid" => "Company context inside the already authorized Tenant; this value cannot widen Tenant or membership scope.",
                "branchid" => "Branch context inside the already authorized Tenant; this value cannot widen Tenant or membership scope.",
                "warehouseid" => "Warehouse context inside the already authorized Tenant and Company/Branch scope.",
                "tenantid" => "Tenant identity associated with this request; Tenant authority is resolved and enforced by the server.",
                "effectiveon" => "Calendar date used to resolve the effective version for this operation.",
                "asofdate" => "Business date through which the requested read or calculation is evaluated.",
                "fromdate" => "Start date supplied to this operation's date filter.",
                "todate" => "End date supplied to this operation's date filter.",
                "page" => "Requested page number for a paged result.",
                "pagesize" => "Maximum number of items requested in one page.",
                _ when parameter.In == ParameterLocation.Path && normalized.EndsWith("id", StringComparison.Ordinal)
                    => $"Identifier of the route resource used by `{descriptor.OperationId}`; the server enforces Tenant ownership and authorized scope.",
                _ => $"The `{name}` input used by `{descriptor.OperationId}` to select, constrain, or describe this request."
            };
        }
    }

    private static void DescribeContractHeaders(OpenApiOperation operation, FoundationOperationDescriptor descriptor)
    {
        if (descriptor.Idempotency == FoundationIdempotencyPolicy.Required)
        {
            AddHeader(operation, "Idempotency-Key", "Stable key bound to the authorized identity and request; reusing it for the same request returns its bound result, while changing the request conflicts.");
        }
        if (descriptor.Concurrency == FoundationConcurrencyPolicy.IfMatch)
        {
            AddHeader(operation, "If-Match", "Current resource ETag required for this optimistic-concurrency mutation; a stale value conflicts.");
        }
        if (descriptor.RequiresAntiforgery)
        {
            AddHeader(operation, "X-CSRF-TOKEN", "Antiforgery token required with the authenticated first-party session for this unsafe request.");
        }
    }

    private static void AddHeader(OpenApiOperation operation, string name, string description)
    {
        operation.Parameters ??= [];
        var parameter = operation.Parameters
            .OfType<OpenApiParameter>()
            .SingleOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)
                && item.In == ParameterLocation.Header);
        if (parameter is null)
        {
            parameter = new OpenApiParameter { Name = name, In = ParameterLocation.Header };
            operation.Parameters.Add(parameter);
        }

        parameter.Description = description;
        parameter.Required = true;
        parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String };
    }

    private static void SetResponse(OpenApiOperation operation, string statusCode, string description, bool isProblem = true)
    {
        operation.Responses ??= new OpenApiResponses();
        if (isProblem)
        {
            // Keep any body the endpoint itself declares for this status; pre-dispatch guards add Problem Details.
            var content = operation.Responses.TryGetValue(statusCode, out var declared) && declared.Content is { Count: > 0 } declaredContent
                ? new Dictionary<string, OpenApiMediaType>(declaredContent, StringComparer.Ordinal)
                : new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
            content["application/problem+json"] = new OpenApiMediaType { Schema = ProblemDetailsSchema() };
            operation.Responses[statusCode] = new OpenApiResponse { Description = description, Content = content };
            return;
        }

        if (!operation.Responses.TryGetValue(statusCode, out var response))
        {
            response = new OpenApiResponse();
            operation.Responses[statusCode] = response;
        }
        response.Description = description;
    }

    private static OpenApiSchema ProblemDetailsSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Problem type URI when the endpoint supplies one; the URI may include the stable error code." },
            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Short, safe summary of the failure." },
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "HTTP status returned by the endpoint." },
            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Safe description of why the request was rejected or unavailable." },
            ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Request instance when supplied by the Problem Details writer." },
            ["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable machine-readable error code when the endpoint emits this extension." },
            ["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Request correlation identifier when emitted by the endpoint." },
            ["operationId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Foundation catalogue operation identifier when emitted by the endpoint." }
        }
    };

    private static string SummaryFor(string operationId, string httpMethod = "GET") => operationId switch
    {
        "platform.health" => "Check platform availability",
        "platform.openapi" => "Read the generated API contract",
        "platform.module-registration" => "Read registered module boundaries",
        "auth.session.read" => "Read the authenticated user's session and identity",
        "auth.entry.read" => "Resolve the current host's entry mode and public branding",
        "auth.antiforgery.read" => "Read antiforgery evidence for first-party writes",
        "auth.sign-in" => "Authenticate and establish a first-party session",
        "auth.sign-out" => "Revoke the current first-party session",
        "auth.context-switch" => "Select an authorized Tenant or Platform context",
        "auth.operational-context-switch" => "Select an authorized Company or Branch context",
        "auth.development-bypass" => "Establish the configured Development QA session",
        "audit.evidence.search" => "Search Tenant-scoped audit evidence",
        "notification.intent.dispatch" => "Dispatch an authorized notification intent",
        "master-data.tax.list" => "List Tenant-owned Tax rules",
        "master-data.tax.read" => "Read one Tenant-owned Tax rule",
        "master-data.tax.history.read" => "Read Tax rate-version history",
        "master-data.tax.reference.read" => "Resolve a Tax version for an effective date",
        "master-data.tax.calculate" => "Calculate Tax from explicit engine inputs",
        "master-data.tax.create" => "Create a Tenant-owned Tax rule",
        "master-data.tax.edit" => "Edit Tax identity and append a rate version",
        "master-data.tax.deactivate" => "Deactivate a Tax rule",
        "master-data.tax.reactivate" => "Reactivate a Tax rule",
        "master-data.tax.audit.read" => "Read Tax audit evidence",
        "master-data.exchange-rate.list" => "List Tenant-owned Exchange Rates",
        "master-data.exchange-rate.read" => "Read one Tenant-owned Exchange Rate",
        "master-data.exchange-rate.history.read" => "Read Exchange Rate version history",
        "master-data.exchange-rate.reference.read" => "Resolve an Exchange Rate for an effective date",
        "master-data.exchange-rate.create" => "Create a Tenant-owned Exchange Rate",
        "master-data.exchange-rate.edit" => "Edit an Exchange Rate and append evidence",
        "master-data.exchange-rate.deactivate" => "Deactivate an Exchange Rate",
        "master-data.exchange-rate.reactivate" => "Reactivate an Exchange Rate",
        "master-data.exchange-rate.audit.read" => "Read Exchange Rate audit evidence",
        "master-data.price-list.list" => "List Tenant-owned Price Lists",
        "master-data.price-list.read" => "Read one Tenant-owned Price List",
        "master-data.price-list.history.read" => "Read Price List price-version history",
        "master-data.price-list.create" => "Create a Tenant-owned Price List",
        "master-data.price-list.edit" => "Edit Price List identity and applicability",
        "master-data.price-list.price.append" => "Append an effective-dated Price List price",
        "master-data.price-list.deactivate" => "Deactivate a Price List",
        "master-data.price-list.reactivate" => "Reactivate a Price List",
        "master-data.price-list.reference.read" => "Resolve a deterministic B2B price reference",
        "master-data.price-list.audit.read" => "Read Price List audit evidence",
        "master-data.import.create" => "Create a Tenant-owned Master Data import batch",
        "master-data.import.simulate" => "Validate a Master Data import without mutations",
        "master-data.import.execute" => "Execute a validated Master Data import batch",
        "master-data.import.list" => "List Tenant-owned Master Data import batches",
        "master-data.import.read" => "Read one Tenant-owned Master Data import batch",
        "master-data.import.status.read" => "Read Master Data import status",
        "master-data.import.rows.read" => "Read Master Data import row evidence",
        "master-data.import.reconciliation.read" => "Read Master Data import reconciliation",
        "master-data.import.audit.read" => "Read Master Data import audit evidence",
        "master-data.import.evidence.read" => "Read complete Master Data import evidence",
        "master-data.import.replay" => "Replay one quarantined Master Data import row",
        "migration.validation.corrected-retry" => "Retry corrected eligible migration rows",
        "migration.preview.read" => "Read a non-authoritative migration preview",
        "migration.reconciliation-preview.read" => "Read a non-authoritative reconciliation preview",
        "migration.run.cancel" => "Cancel a migration run before execution",
        "procurement.organization-scope.list" => "List server-authorized Purchase Request organization scopes",
        "procurement.purchase-request.list" => "List Tenant-scoped Purchase Requests",
        "procurement.purchase-request.read" => "Read one Purchase Request",
        "procurement.purchase-request.create" => "Create a Purchase Request draft",
        "procurement.purchase-request.edit" => "Edit a Purchase Request draft",
        "procurement.purchase-request.submit" => "Submit a Purchase Request for approval",
        "procurement.purchase-request.approve" => "Approve a Purchase Request",
        "procurement.purchase-request.reject" => "Reject a Purchase Request",
        "procurement.purchase-request.return-for-change" => "Return a Purchase Request for change",
        "procurement.purchase-request.cancel" => "Cancel an eligible Purchase Request",
        "procurement.purchase-request.history.read" => "Read Purchase Request lifecycle history",
        "procurement.purchase-request.audit.read" => "Read Purchase Request audit evidence",
        "procurement.quotation.list" => "List Supplier Quotations for an approved Purchase Request",
        "procurement.quotation.read" => "Read one Supplier Quotation",
        "procurement.quotation.create" => "Capture a Supplier Quotation against an approved Purchase Request",
        "procurement.quotation.edit" => "Edit a Draft Supplier Quotation",
        "procurement.quotation.submit" => "Submit a Supplier Quotation for comparison",
        "procurement.quotation.withdraw" => "Withdraw a submitted Supplier Quotation",
        "procurement.quotation.disqualify" => "Disqualify a submitted Supplier Quotation",
        "procurement.quotation.compare" => "Compare captured Supplier Quotations deterministically",
        "procurement.source-decision.read" => "Read the current Purchase Request source decision",
        "procurement.source-decision.history.read" => "Read Purchase Request source-decision history",
        "procurement.source-decision.record" => "Record the Purchase Request source decision",
        "procurement.quotation.history.read" => "Read Supplier Quotation lifecycle history",
        "procurement.quotation.audit.read" => "Read Supplier Quotation audit evidence",
        "procurement.purchase-order.source.list" => "List eligible approved source decisions for Purchase Order creation",
        "procurement.purchase-order.list" => "List Tenant-scoped Purchase Orders",
        "procurement.purchase-order.read" => "Read one Purchase Order with source lineage and confirmation state",
        "procurement.purchase-order.create" => "Create a Draft Purchase Order from an eligible source decision",
        "procurement.purchase-order.edit" => "Edit a Draft or ReturnedForChange Purchase Order",
        "procurement.purchase-order.submit" => "Submit a Purchase Order for approval",
        "procurement.purchase-order.approve" => "Approve a Purchase Order",
        "procurement.purchase-order.reject" => "Reject a Purchase Order",
        "procurement.purchase-order.return-for-change" => "Return a Purchase Order for change",
        "procurement.purchase-order.issue" => "Issue an approved Purchase Order to the supplier",
        "procurement.purchase-order.cancel" => "Cancel an eligible Purchase Order",
        "procurement.purchase-order.confirmation.read" => "Read Purchase Order supplier confirmations",
        "procurement.purchase-order.confirmation.capture" => "Record a manual supplier confirmation or rejection",
        "procurement.purchase-order.supplier-change.approve" => "Approve a proposed supplier change and reissue the Purchase Order",
        "procurement.purchase-order.supplier-change.reject" => "Reject a proposed supplier change",
        "procurement.purchase-order.history.read" => "Read Purchase Order lifecycle and confirmation history",
        "procurement.purchase-order.audit.read" => "Read Purchase Order audit evidence",
        "procurement.goods-receipt.eligible-source.list" => "List Purchase Orders eligible for a Goods Receipt",
        "procurement.warehouse.list" => "List server-authorized Warehouse options for Goods Receipt",
        "procurement.goods-receipt.list" => "List Tenant-scoped Goods Receipts",
        "procurement.goods-receipt.read" => "Read one Goods Receipt with accepted/rejected/damaged line evidence",
        "procurement.goods-receipt.create" => "Record a manual Goods Receipt against an eligible Purchase Order",
        "procurement.goods-receipt.cancel" => "Cancel an eligible Goods Receipt",
        "procurement.goods-receipt.history.read" => "Read Goods Receipt lifecycle history",
        "procurement.goods-receipt.audit.read" => "Read Goods Receipt audit evidence",
        "procurement.invoice-handoff.eligible-source.list" => "List Goods Receipts eligible for a Purchase Invoice handoff",
        "procurement.invoice-handoff.list" => "List Tenant-scoped Purchase Invoice handoffs",
        "procurement.invoice-handoff.read" => "Read one Purchase Invoice handoff with lineage and line evidence",
        "procurement.invoice-handoff.create" => "Create a non-posted Purchase Invoice handoff from an eligible Goods Receipt",
        "procurement.invoice-handoff.evidence.capture" => "Capture or correct independent supplier-declared invoice evidence",
        "procurement.invoice-handoff.cancel" => "Cancel an eligible Purchase Invoice handoff",
        "procurement.invoice-handoff.history.read" => "Read Purchase Invoice handoff lifecycle history",
        "procurement.invoice-handoff.audit.read" => "Read Purchase Invoice handoff audit evidence",
        "procurement.matching.list" => "List durable three-way match evaluations",
        "procurement.matching.read" => "Read one durable three-way match evaluation and source snapshot",
        "procurement.matching.evaluate" => "Evaluate a Purchase Order, accepted Goods Receipt, and supplier invoice evidence",
        "procurement.matching.resolve-exception" => "Resolve a three-way match exception with controlled authorization",
        "procurement.matching.history.read" => "Read three-way match evaluation history",
        "procurement.matching.audit.read" => "Read three-way match evaluation audit evidence",
        "inventory.valuation.policy.read" => "Read effective Moving Weighted Average valuation policies",
        "inventory.valuation.policy.create" => "Create an effective-dated Moving Weighted Average valuation policy",
        "inventory.valuation.state.read" => "Read current Moving Weighted Average state by valuation scope",
        "inventory.valuation.history.read" => "Read immutable movement valuation evidence and status history",
        "inventory.valuation.process" => "Process physical movements through the Moving Weighted Average valuation policy",
        "inventory.valuation.reconciliation.read" => "Reconcile physical movement quantities with valuation evidence",
        "inventory.valuation.finance-handoff.read" => "Read Finance-ready valuation handoff facts without posting journals",
        "inventory.valuation.summary.read" => "Read the Inventory valuation summary by authorized scope",
        "inventory.valuation.pending.read" => "Read pending Inventory valuation events and their blocking reasons",
        "inventory.valuation.in-transit.read" => "Read in-transit quantity and valuation reconciliation facts",
        "inventory.valuation.export" => "Export immutable Inventory valuation evidence with scope and freshness metadata",
        "inventory.valuation.correction" => "Request a source-linked Inventory valuation correction",
        _ => GenericSummary(operationId, httpMethod)
    };

    private static string GenericSummary(string operationId, string httpMethod)
    {
        var parts = operationId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Read the documented API operation";
        var actionSuffix = IsActionSuffix(parts[^1]);
        var resourceParts = parts.Length > 1
            ? parts.Skip(1).Take(parts.Length - (actionSuffix ? 2 : 1))
            : parts;
        if (!resourceParts.Any()) resourceParts = parts.Take(1);
        var resource = string.Join(' ', resourceParts.Select(ToDisplay));
        var action = SummaryVerb(parts[^1], httpMethod);
        return $"{action} {ToDisplay(parts[0])} {resource}".Trim();
    }

    private static bool IsActionSuffix(string value) => value is
        "list" or "read" or "detail" or "history" or "audit" or "create" or "edit" or "update" or
        "deactivate" or "reactivate" or "submit" or "approve" or "reject" or "return" or
        "return-for-change" or "cancel" or "issue" or "capture" or "confirm" or "withdraw" or
        "disqualify" or "compare" or "record" or "resolve-exception" or "evaluate" or "process" or
        "correct" or "correction" or "close" or "reopen" or "post" or "recognize" or "reverse" or
        "receive" or "reserve" or "send" or "dispatch" or "replay" or "simulate" or "execute" or
        "validate" or "preview" or "reconcile" or "ready" or "start" or "sign-in" or "sign-out" or
        "context-switch" or "operational-context-switch" or "write" or "period" or "direct" or
        "shortage-resolve" or "variance-reason" or "resnapshot" or "recount" or "reduce" or
        "convert" or "append" or "override" or "corrected-retry" or "calculate" or "export";

    private static string SummaryVerb(string action, string httpMethod) => action switch
    {
        "list" or "read" or "detail" or "history" or "audit" or "state" or "runs" or "inquiry" or "aging" or "ap-aging" or "ar-aging" or "balance-sheet" or "profit-loss" or "trial-balance" or "general-ledger" or "exposure" or "reconciliation" or "unrealized-reconciliation" or "readiness" or "source" => "Read",
        "search" => "Search",
        "create" or "issue" or "request" => "Create",
        "edit" or "update" => "Update",
        "deactivate" => "Deactivate",
        "reactivate" or "activate" => "Reactivate",
        "submit" => "Submit",
        "approve" => "Approve",
        "reject" => "Reject",
        "return" or "return-for-change" => "Return",
        "cancel" => "Cancel",
        "capture" => "Capture",
        "confirm" => "Confirm",
        "withdraw" => "Withdraw",
        "disqualify" => "Disqualify",
        "compare" => "Compare",
        "record" => "Record",
        "resolve-exception" => "Resolve",
        "evaluate" => "Evaluate",
        "process" => "Process",
        "correct" or "correction" => "Correct",
        "close" => "Close",
        "reopen" => "Reopen",
        "post" or "recognize" => "Post",
        "reverse" => "Reverse",
        "receive" => "Receive",
        "reserve" => "Reserve",
        "send" => "Send",
        "dispatch" => "Dispatch",
        "replay" => "Replay",
        "simulate" => "Simulate",
        "execute" => "Execute",
        "validate" => "Validate",
        "preview" => "Preview",
        "reconcile" => "Reconcile",
        "ready" => "Mark ready for handover",
        "start" => "Start",
        "sign-in" => "Authenticate",
        "sign-out" => "Revoke",
        "context-switch" or "operational-context-switch" => "Select",
        "health" => "Check",
        "openapi" => "Read",
        "module-registration" => "Read",
        "development-bypass" => "Establish",
        "write" => "Record",
        "period" => "Close",
        "direct" => "Transfer",
        "shortage-resolve" => "Resolve",
        "variance-reason" => "Record",
        "resnapshot" => "Refresh",
        "recount" => "Recount",
        "reduce" => "Reduce",
        "convert" => "Convert",
        "append" => "Append",
        "override" => "Override",
        "corrected-retry" => "Retry corrected",
        _ when string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase) => "Read",
        _ => ToDisplay(action)
    };

    private static string ToDisplay(string value)
    {
        var display = string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(item =>
            item.Length == 0 ? item : char.ToUpperInvariant(item[0]) + item[1..]));
        return display switch
        {
            "Ap" => "AP",
            "Ar" => "AR",
            "Gl" => "GL",
            "Coa" => "COA",
            "Fx" => "FX",
            "Uom" => "UOM",
            "Sku" => "SKU",
            "B2b" => "B2B",
            "Id" => "ID",
            _ => display
        };
    }

    private static string OwnerFor(string operationId) => operationId.Split('.', StringSplitOptions.RemoveEmptyEntries)[0] switch
    {
        "auth" => "Identity",
        "foundation" => "Foundation",
        "audit" => "Audit",
        "notification" => "Notifications",
        "master-data" when operationId.Contains(".supplier.", StringComparison.Ordinal)
            || operationId.Contains(".customer.", StringComparison.Ordinal) => "Business Parties",
        "master-data" => "Master Data",
        "procurement" => "Procurement",
        "inventory" => "Inventory",
        "sales" => "Sales",
        "finance" => "Finance",
        "reporting" => "Reporting",
        "migration" => "Migration",
        "platform" => "Platform",
        _ => "Foundation REST host"
    };

    private static string ResponsibilityFor(string owner) => owner switch
    {
        "Identity" => "authentication, sessions, membership, permissions, and server-authorized Tenant/organization context",
        "Foundation" => "shared REST authorization context, safe operation probes, and first-party request controls",
        "Audit" => "immutable, reconstructable evidence of material actions",
        "Notifications" => "cross-cutting notification delivery intents without business authority",
        "Master Data" => "reusable Category, UOM, Product, Price List, Tax, Currency, Exchange Rate, Payment Term, and import data",
        "Business Parties" => "Supplier and Business Customer identity, separate from purchasing or sales process roles",
        "Procurement" => "Purchase Requests, quotations, Purchase Orders, receipt evidence, invoice handoff/matching, and supplier returns; not stock or accounting truth",
        "Inventory" => "stock movement and ledger, stock control, and moving-weighted-average valuation; not journals",
        "Sales" => "B2B quotation/order/credit, reservation, fulfillment/delivery, invoice eligibility, and Customer Return; not accounting truth",
        "Finance" => "books, journals/GL, AP, AR, cash/bank, settlement, FX/revaluation, close, and reconciliation",
        "Reporting" => "read models, reports, and exports; not a transactional ledger",
        "Migration" => "Tenant migration intake, validation, quarantine, dry-run, execution orchestration, reconciliation, approval, and handover evidence",
        "Platform" => "Platform administration and Tenant lifecycle, without granting Tenant ERP data access",
        _ => "the responsibility defined by the owning module's public contract"
    };

    private static string PreconditionsFor(FoundationOperationDescriptor descriptor)
    {
        if (!descriptor.IsUnsafe)
        {
            return "The input and any effective-date selectors must be valid, and the server-derived caller context must be authorized. This operation reads or calculates and does not transition business state.";
        }

        var requirements = new List<string>
        {
            "The request must be valid and any target must be in a state accepted by the owning module's application logic. That module validates the exact transition; a rejected request does not claim a successful transition."
        };
        if (descriptor.Idempotency == FoundationIdempotencyPolicy.Required)
        {
            requirements.Add("A valid `Idempotency-Key` is required; a replay returns the bound result and a changed binding conflicts.");
        }
        if (descriptor.Concurrency == FoundationConcurrencyPolicy.IfMatch)
        {
            requirements.Add("The current resource version must be supplied in `If-Match`; a stale version is rejected.");
        }
        if (descriptor.RequiresAntiforgery)
        {
            requirements.Add("A valid antiforgery token for the authenticated first-party session is required.");
        }
        if (descriptor.RequiresMfa || descriptor.RequiresFreshAuthentication)
        {
            requirements.Add($"The server also requires MFA: {descriptor.RequiresMfa} and fresh authentication: {descriptor.RequiresFreshAuthentication}.");
        }

        return string.Join(' ', requirements);
    }

    private static string SideEffectsFor(FoundationOperationDescriptor descriptor)
    {
        if (!descriptor.IsUnsafe)
        {
            return "No business state is changed by this read or calculation. A denial may still produce security evidence when the endpoint's audit policy requires it.";
        }

        var owner = OwnerFor(descriptor.OperationId);
        var effect = owner switch
        {
            "Master Data" or "Business Parties" => "The successful command changes only the owning reference-data record or evidence; it does not create stock or accounting ledger effects.",
            "Procurement" => "The successful command changes Procurement-owned source-document or evidence state. Procurement does not directly mutate the stock ledger or Finance journals.",
            "Inventory" => "The successful command changes Inventory-owned stock, movement, control, or valuation state as specified by this operation. Inventory does not post Finance journals.",
            "Sales" => "The successful command changes Sales-owned source, reservation, fulfillment, or return state. Finance postings remain separate Finance-owned operations.",
            "Finance" => "The successful command changes Finance-owned configuration, source, or ledger state. Journal and ledger effects are owned and applied by Finance; operational modules do not fabricate them.",
            "Reporting" => descriptor.OperationId.Contains("schedule", StringComparison.Ordinal)
                ? "The successful command changes the Reporting schedule; it does not mutate a transactional ledger."
                : "The successful command creates or returns the requested report/export result; it does not mutate a transactional ledger.",
            "Migration" => "The operation changes only the migration state named by its operation-specific contract; previews are read-only, and execution effects remain behind the Migration execution boundary.",
            "Identity" or "Foundation" or "Platform" or "Audit" or "Notifications" => "The successful command changes the session, context, audit, notification, or Platform state owned by the named service; it does not grant Tenant ERP access outside the authorized context.",
            _ => "The successful command applies the effect owned by its module through its application contract."
        };

        return effect + (descriptor.RequiresMandatoryAudit
            ? " Mandatory immutable audit evidence is required before the protected effect; the operation fails closed if that evidence boundary is unavailable."
            : " The catalogue does not require mandatory audit evidence for this operation.");
    }

    private static string AuthorizationFor(FoundationOperationDescriptor descriptor)
    {
        var scope = descriptor.ScopePolicy switch
        {
            FoundationScopePolicy.Tenant => "an active Tenant membership and the server-authorized organization scope",
            FoundationScopePolicy.SupportGrant => "an active, case-bound Support Grant for one Tenant and its authorized organization scope",
            FoundationScopePolicy.PlatformGovernance => "a purpose-bound Platform governance context",
            _ => "no Tenant or Platform business scope"
        };
        var permission = descriptor.ExactPermissionCode is null
            ? "no exact permission is declared"
            : $"the exact permission `{descriptor.ExactPermissionCode}`";
        return $"The catalogue security profile is `{descriptor.SecurityProfile}` and the required scope is {scope}; the caller must also hold {permission}. Client-supplied Tenant, role, permission, or scope values do not create authority.";
    }

    private static string TenantBoundaryFor(FoundationOperationDescriptor descriptor) => descriptor.ScopePolicy switch
    {
        FoundationScopePolicy.Tenant or FoundationScopePolicy.SupportGrant => "Tenant isolation is enforced server-side: a foreign-Tenant resource is returned as 404 like a missing resource, while permission or organization-scope denial is 403.",
        _ => "This operation has no Tenant ERP data scope; it does not use a client-supplied Tenant identifier to grant access."
    };

    private static string ContractFactsFor(FoundationOperationDescriptor descriptor)
    {
        var effectiveDate = descriptor.EffectiveDate switch
        {
            FoundationEffectiveDatePolicy.QueryRequired => "An effective-date query value is required.",
            FoundationEffectiveDatePolicy.RequestRequired => "The request body must carry the required effective date.",
            _ => "No effective-date input is declared by the Foundation descriptor."
        };
        var idempotency = descriptor.Idempotency == FoundationIdempotencyPolicy.Required
            ? "Mutation idempotency uses the `Idempotency-Key` header bound to server-resolved authority and request identity."
            : "The Foundation descriptor does not require an `Idempotency-Key` header.";
        var concurrency = descriptor.Concurrency == FoundationConcurrencyPolicy.IfMatch
            ? "Optimistic concurrency uses the current resource ETag in `If-Match`; stale versions conflict."
            : "The Foundation descriptor does not require an `If-Match` version.";
        return $"{effectiveDate} {idempotency} {concurrency}";
    }

    private static string ErrorContractFor(FoundationOperationDescriptor descriptor)
    {
        var detail = "Problem responses use the endpoint's safe Problem Details writer and preserve the exact returned machine-readable `code`; endpoints that emit `type` use a problem URI for that code. `400` covers invalid transport input or preconditions; known shared codes include `validation_failed`, `idempotency_key_invalid`, `version_required`, and `if_match_required`. `401` covers missing or invalid authentication; known shared codes include `authentication_required` and `authentication_failed`. `403` covers permission/scope denial or antiforgery failure; known shared codes include `permission_denied`, `access_denied`, `scope_denied`, `company_scope_denied`, `branch_scope_denied`, `warehouse_scope_denied`, and `antiforgery_failed`. `404` covers absent or foreign-Tenant resources; `409` covers idempotency, concurrency, or current-state conflicts; and `503` means a required dependency/evidence boundary is unavailable. The owning module's domain result code is returned unchanged and identifies the rejected rule. The Foundation descriptor does not define a closed, per-operation domain-code catalogue; where the endpoint forwards an application result, the exact possible code set is defined by that owning module operation.";
        if (descriptor.OperationId.StartsWith("finance.", StringComparison.Ordinal)
            || descriptor.OperationId.StartsWith("reporting.", StringComparison.Ordinal))
        {
            detail += " `422` is also used when Finance or Reporting can parse the request but rejects an operation-specific business value or state.";
        }
        return detail;
    }

    private static string DescriptionFor(FoundationOperationDescriptor descriptor)
    {
        var owner = OwnerFor(descriptor.OperationId);
        var contextRules = $"Purpose: {SummaryFor(descriptor.OperationId, descriptor.HttpMethod)}. Owner: {owner}; responsibility: {ResponsibilityFor(owner)}. "
            + $"Preconditions and state transition: {PreconditionsFor(descriptor)} "
            + $"Side effects: {SideEffectsFor(descriptor)} "
            + $"Authorization: {AuthorizationFor(descriptor)} "
            + $"Tenant boundary: {TenantBoundaryFor(descriptor)} "
            + $"Headers and effective date: {ContractFactsFor(descriptor)} "
            + $"Errors: {ErrorContractFor(descriptor)} ";

        if (descriptor.BoundaryDescription is { } boundaryDescription)
        {
            return $"{contextRules}{boundaryDescription}";
        }

        if (descriptor.OperationId == "auth.entry.read")
        {
            return contextRules + "Resolves the request Host against configured common, Tenant, and Platform entry hosts. Anonymous callers receive only resolved entry mode and public branding; Tenant identifiers and operational-context details are not exposed. Hostname is a candidate hint and never grants Tenant authority.";
        }

        if (descriptor.OperationId == "auth.development-bypass")
        {
            return contextRules + "Development-only, loopback-only session establishment for the server-configured Development actor. The request accepts no login, password, Tenant, role, permission, or identity input. The route is available only under the exact Development environment and explicit `MESP_DEV_AUTH_BYPASS=true` setting; it never substitutes for production authentication.";
        }

        var identityBoundary = descriptor.OperationId switch
        {
            "auth.sign-in" => "Validates the supplied login and password against Identity and, on success, establishes a server-side authenticated session. It returns no Tenant authorization path; the server resolves eligible contexts separately.",
            "auth.sign-out" => "Revokes the current server-side session. When a Tenant or Platform path is selected, safe evidence is written before revocation; a session-only sign-out follows the conditional evidence policy and is not a Tenant business effect.",
            "auth.session.read" => "Returns the current first-party session summary after server-side session validation; it does not accept client-supplied Tenant or permission claims.",
            "auth.contexts.read" => "Returns only Tenant or Platform context candidates already authorized for the authenticated user. Candidate identifiers do not grant authority.",
            "auth.operational-contexts.read" => "Returns Company/Branch contexts inside the selected authorized Tenant and the current selection version; client context values cannot widen membership scope.",
            "auth.context-switch" => "Switches to a server-authorized Tenant, support, or Platform candidate only when the supplied selection and eligibility versions still match server state. A stale version conflicts; a client cannot create a new path or permission.",
            "auth.operational-context-switch" => "Switches Company/Branch context inside the already authorized Tenant only when the candidate remains eligible and the selection/eligibility versions match server state. A stale version conflicts and cannot widen the selected Tenant scope.",
            "auth.antiforgery.read" => "Issues or returns antiforgery evidence associated with the authenticated session for unsafe browser requests; it does not select a Tenant or authorize a business operation.",
            _ => null
        };
        if (identityBoundary is not null)
        {
            return contextRules + identityBoundary;
        }

        var migrationBoundary = descriptor.OperationId switch
        {
            "migration.validation.corrected-retry" => "A corrected retry revalidates only supplied rejected or quarantined source rows. It preserves the staged source package, accepted source identities, and prior findings; it creates no execution attempt or owner effect.",
            "migration.preview.read" => "The preview projects expected additions, duplicate outcomes, dependencies, control totals, and exceptions from stored dry-run evidence. It is not an authoritative import and does not create approval or readiness or change run state.",
            "migration.reconciliation-preview.read" => "The reconciliation preview projects controls from dry-run evidence only. It does not observe owner effects, create reconciliation evidence, request approval, create readiness, or change run state.",
            "migration.run.cancel" => "Cancellation requires a reason and the current run version. It is permitted only before execution starts; committed, in-flight, or unknown owner effects are rejected. The reason and audit evidence are retained, and cancellation performs no compensation or reset.",
            _ => null
        };
        if (migrationBoundary is not null)
            return contextRules + migrationBoundary;

        if (descriptor.OperationId.StartsWith("master-data.tax", StringComparison.Ordinal))
        {
            return contextRules
                + "Tax is reusable Tenant-wide internal configuration-led Master Data with bilingual identity, "
                + "Active/Inactive lifecycle, effective-dated rate versions, and historical reference snapshots. "
                + "The calculation operation accepts an explicit taxable base, currency, rounding scale/mode, "
                + "transaction direction, effective date, and source lineage. It performs no accounting posting "
                + "and does not invent inclusive/exclusive price derivation, discount/charge/freight base policy, "
                + "exemption policy, statutory meaning, government submission, external provider behavior, or "
                + "posted-document correction. Writes require Idempotency-Key; identity/version edits and lifecycle "
                + "changes also require the current If-Match value."
                + (descriptor.OperationId == "master-data.tax.calculate"
                    ? " A successful calculation is side-effect-free and is not a mutation; a denied or invalid attempt may still append denial evidence."
                    : string.Empty);
        }

        if (descriptor.OperationId.StartsWith("master-data.exchange-rate", StringComparison.Ordinal))
        {
            return contextRules
                + "Exchange Rate is reusable Tenant-wide internal reference data over two existing active Currency identities. "
                + "The pair is directional (source units to target units), the rate is positive with an explicit precision scale, "
                + "and edits append effective-dated versions while preserving manual/configured provenance, source notes, and "
                + "historical Currency-code snapshots. Reference resolution selects one version only when the requested date is "
                + "inside its effective window; unknown or inactive references fail safely. This API does not invent inverse, "
                + "reciprocal, triangulated, provider-fed, bid/ask, average, daily-close, Finance posting, rounding, realized or "
                + "unrealized FX, revaluation, settlement, reconciliation, or external integration behavior. Writes require "
                + "Idempotency-Key; version edits and lifecycle changes also require the current If-Match value.";
        }

        if (descriptor.OperationId.StartsWith("master-data.price-list", StringComparison.Ordinal))
        {
            return contextRules
                + "Price Lists are reusable Tenant-owned B2B configuration over existing Product, UOM, Currency, and optional same-Tenant Business Customer identities. "
                + "Each price carries an effective window, provenance, source reference, and immutable Product/UOM/Currency applicability evidence. "
                + "Reference resolution requires exact Product, UOM, Currency, customer applicability, organization applicability, lifecycle, and effective-date matches. "
                + "The configured priority uses lower numeric values as higher precedence; equal best priority is an explicit conflict and never falls back to edit order, identifier order, or UI order. "
                + "The endpoint does not implement quantity breaks, promotions, coupons, campaigns, POS, Sales Orders, manual override bypasses, Finance, automatic FX, or accounting rounding. "
                + "Writes require Idempotency-Key, and Price List identity/price/lifecycle writes require the current If-Match value where the catalogue declares concurrency.";
        }

        if (descriptor.OperationId.StartsWith("master-data.import", StringComparison.Ordinal))
        {
            return contextRules
                + "This Phase-A import boundary is a structured, Tenant-owned, evidence-first workflow for Category, UOM, Product, Supplier, "
                + "Business Customer, Currency, Payment Term, Tax/VAT, Exchange Rate, and Price List resources. Each batch preserves source "
                + "provenance, original payload/row identity, normalized fields, diagnostics, outcome, mutation disposition, deterministic result "
                + "references, replay lineage, and audit evidence. Simulation validates and resolves references without calling target mutation "
                + "adapters; execution is permitted only for a validated Commit-mode batch and applies the configured Reject, SkipExisting, or "
                + "UpdateMutableFields duplicate policy. Server-derived Tenant and actor authority cannot be supplied by row data. Partial success "
                + "is reconciled as TotalRows = Accepted + Rejected + Quarantined, with committed, skipped, and failed mutation counts shown "
                + "separately. Replay creates a new current attempt while preserving the original quarantined evidence and lineage. This boundary "
                + "does not perform MESP-40 migration/cutover, retention or legal-hold behavior, residency/PDPL certification, provider integration, "
                + "or irreversible production migration decisions.";
        }

        if (descriptor.OperationId == "procurement.organization-scope.list")
        {
            return contextRules
                + "Organization scopes are the trusted, server-configured set of Company/Branch options a caller may select when creating "
                + "or editing a Purchase Request. Options are filtered to the caller's Tenant and further narrowed by the caller's own "
                + "trusted authorization scope; a request can never widen this to an arbitrary Company or Branch identity. This is not a "
                + "Company/Branch CRUD boundary; it exposes read-only display names for an existing or Development-configured organization "
                + "structure so the client never needs to type or display a raw internal identifier as the primary means of selection.";
        }

        if (descriptor.OperationId == "procurement.warehouse.list")
        {
            return contextRules
                + "Warehouse options are the trusted, server-configured set of physical warehouse locations a caller may select when recording "
                + "a Goods Receipt. Options are filtered to the caller's Tenant and Company/Branch scope; client-supplied warehouse identifiers "
                + "are server-authoritatively validated and never self-authorizing.";
        }

        if (descriptor.OperationId.StartsWith("procurement.purchase-request", StringComparison.Ordinal))
        {
            return contextRules
                + "Purchase Request is an internal Tenant/company/branch demand signal containing Product, UOM, quantity, need-by date, and purpose lines. "
                + "The lifecycle is Draft, PendingApproval, Approved, Rejected, ReturnedForChange, or Cancelled. Submission freezes the reviewed request version; "
                + "approval is configuration-led and records immutable history, including bounded delegation evidence where configured. Self-approval is denied, "
                + "missing or expired authority blocks the decision, and cancellation is available only in eligible states. This boundary creates no stock, supplier "
                + "commitment, Purchase Order, receipt, invoice, AP, payment, or accounting effect. Mutations require Idempotency-Key, the current If-Match value where "
                + "declared, antiforgery, and mandatory audit evidence.";
        }

        if (descriptor.OperationId.StartsWith("procurement.quotation", StringComparison.Ordinal)
            || descriptor.OperationId.StartsWith("procurement.source-decision", StringComparison.Ordinal))
        {
            return contextRules
                + "Supplier Quotation is a buyer-recorded external offer captured only against an Approved Purchase Request. The persisted record snapshots Supplier, Currency, optional Payment Term, Product/UOM/requested-line identity, quantities, prices, discounts, tax facts, delivery facts, notes, and evidence references. "
                + "Comparison is deterministic and preserves transaction currencies; mixed currencies are not ranked or converted because no FX source is invoked. Source decision records the selected quotation, rationale, actor/time, policy-stage evidence, and a hashed comparison snapshot. This boundary creates no Purchase Order, supplier portal account, receipt, invoice, AP, payment, stock, accounting, or external-provider effect. Mutations require Idempotency-Key, antiforgery, mandatory audit evidence, and the current If-Match value where declared.";
        }

        if (descriptor.OperationId.StartsWith("procurement.purchase-order", StringComparison.Ordinal))
        {
            return contextRules
                + "Purchase Order creation is permitted only from an Approved Purchase Request, a Submitted eligible Supplier Quotation, and the current server-authorized source decision. The persisted order preserves immutable Tenant, Company/Branch, Supplier, Currency, payment-term, source-decision, requested-line, quantity, price, discount, tax, and delivery snapshots. "
                + "The lifecycle is Draft, PendingApproval, Approved, Issued, Confirmed, PartiallyConfirmed, ChangedPendingApproval, Rejected, ReturnedForChange, NoResponse, or Cancelled. Approval reuses the configured policy with separation of duties and bounded delegation evidence. Supplier confirmation is manual and evidence-first; partial quantities preserve the remainder, rejection and no-response are explicit, and proposed supplier quantity/price/date changes preserve previous and proposed values until an authorized reapproval decision. "
                + "This boundary creates no receipt, stock movement, warehouse effect, invoice, AP, payment, three-way match, accounting, supplier portal, external integration, or government submission. Mutations require Idempotency-Key, antiforgery, mandatory audit evidence, and the current If-Match value where declared.";
        }

        if (descriptor.OperationId.StartsWith("procurement.goods-receipt", StringComparison.Ordinal))
        {
            return contextRules
                + "Goods Receipt is a manually recorded, Inventory-owned physical acceptance evidence boundary against an Issued/Confirmed/PartiallyConfirmed Purchase Order. "
                + "Each line preserves the Purchase Order's immutable source lineage and requires AcceptedQuantity + RejectedQuantity = ReceivedQuantity, with an optional non-additive DamagedQuantity no greater than ReceivedQuantity; "
                + "over-receipt beyond the server-derived eligible remainder is rejected, and client-supplied remainder values are never authoritative. "
                + "This boundary does not fabricate or mutate a stock ledger, warehouse balance, or inventory movement (no such ledger exists yet), and creates no AP liability, "
                + "Posted Purchase Invoice, tax posting, GL entry, or payment. A Goods Receipt referenced by an active Purchase Invoice handoff cannot be cancelled. "
                + "Mutations require Idempotency-Key, antiforgery, mandatory audit evidence, and the current If-Match value where declared.";
        }

        if (descriptor.OperationId.StartsWith("procurement.invoice-handoff", StringComparison.Ordinal))
        {
            return contextRules
                + "Purchase Invoice handoff is a non-posted Finance handoff record created only from an eligible recorded Goods Receipt, preserving immutable Purchase Order, "
                + "Supplier, Currency, and accepted-quantity/commercial line lineage for the eventual three-way-match and posting boundary (MESP-126), which this operation does not implement. "
                + "It creates no AP liability, supplier payable, payment, GL entry, or Posted Purchase Invoice, and performs no statutory tax submission; any tax figures are reproduced "
                + "from prior commercial snapshots, not recalculated or posted. Cancellation of a handoff never blocks or reverses its source Goods Receipt. "
                + "Mutations require Idempotency-Key, antiforgery, mandatory audit evidence, and the current If-Match value where declared.";
        }

        if (descriptor.OperationId.StartsWith("procurement.matching", StringComparison.Ordinal))
        {
            return contextRules
                + "Three-way matching is a durable, non-posting comparison of the Purchase Order commercial snapshot, active accepted Goods Receipt evidence, and an independent supplier-declared invoice evidence snapshot. "
                + "Legacy handoffs without supplier-declared invoice evidence remain readable but are explicitly NotMatchReady. Rejected or cancelled receipt evidence is excluded; current partial-handoff quantity is matched against independent declared quantity, and cumulative active declared quantity cannot exceed accepted or confirmed source limits. "
                + "Tolerance and resolution policy are selected from server configuration with an exact-safe zero-tolerance fallback. Cross-currency evaluation accepts only a Tenant-owned Exchange Rate identity/effective-date reference; rate, scale, version, effective window, and provenance are resolved from MESP-120 and snapshotted by the evaluation. Raw caller-supplied FX facts are not authoritative. "
                + "Source versions, policy, variance classifications, and any applied exchange-rate evidence are immutable on the evaluation. "
                + "The boundary creates no AP liability, invoice posting, tax posting, GL entry, payment, stock movement, or statutory submission. Mutations require Idempotency-Key, antiforgery, mandatory audit evidence, and If-Match.";
        }

        return contextRules
            + "The owning application contract validates operation-specific inputs and eligibility. It returns a successful result only for the effect described by that module; no additional lifecycle rule or cross-module effect is inferred here. Provider details and internal implementation types are not exposed in Problem Details.";
    }

    private static string SuccessResponseFor(FoundationOperationDescriptor descriptor)
    {
        var operationId = descriptor.OperationId;
        return operationId switch
        {
        "auth.session.read" => "The authenticated caller's server-validated session, nullable display name, and own login identifier.",
        "auth.entry.read" => "The host entry mode, public branding, and (only for an authenticated caller) authorized context candidates.",
        "auth.development-bypass" => "An authenticated session for the server-configured Development actor with server-derived context candidates.",
        "master-data.tax.calculate" => "A deterministic Tax amount and immutable reference snapshot for the explicit inputs.",
        "master-data.tax.reference.read" => "The active Tax rate version selected for the requested effective date, including applied reference evidence.",
        "master-data.tax.history.read" => "The Tenant-owned Tax rate-version windows in stable version order.",
        "master-data.tax.audit.read" => "Tenant-filtered audit evidence for the Tax resource.",
        "master-data.exchange-rate.reference.read" => "The active Exchange Rate version selected for the requested effective date, including applied pair and version evidence.",
        "master-data.exchange-rate.history.read" => "The Tenant-owned Exchange Rate version windows with preserved Currency-code snapshots.",
        "master-data.exchange-rate.audit.read" => "Tenant-filtered audit evidence for the Exchange Rate resource.",
        "master-data.price-list.reference.read" => "The single deterministic Tenant-owned Price List price reference, including Product/UOM/Currency applicability, effective window, configured priority, provenance, and immutable version evidence.",
        "master-data.price-list.history.read" => "The Tenant-owned Price List price-version history with preserved applicability and provenance snapshots.",
        "master-data.price-list.audit.read" => "Tenant-filtered audit evidence for the Price List resource and pricing-reference decisions.",
        "master-data.import.create" => "The persisted import batch identity, source provenance, policy, mode, initial status, version, and reconciliation counters.",
        "master-data.import.simulate" => "The validated import batch with row-level normalized evidence, diagnostics, duplicate/reference outcomes, and no target mutations.",
        "master-data.import.execute" => "The completed or partially completed import batch with row outcomes, mutation dispositions, deterministic target references, and reconciliation counters.",
        "master-data.import.list" => "Tenant-filtered import batch summaries with status, provenance, mode, policy, version, and reconciliation counters.",
        "master-data.import.read" => "One Tenant-filtered import batch summary with its durable lifecycle and reconciliation state.",
        "master-data.import.status.read" => "The current import status, correlation identifier, and optimistic-concurrency version.",
        "master-data.import.rows.read" => "Tenant-filtered row evidence including source fields, normalized fields, diagnostics, outcome, mutation disposition, target reference, and replay lineage.",
        "master-data.import.reconciliation.read" => "The persisted import reconciliation counters and the TotalRows = Accepted + Rejected + Quarantined consistency result.",
        "master-data.import.audit.read" => "Tenant-filtered batch, row, and mutation audit evidence with source reference and correlation context.",
        "master-data.import.evidence.read" => "The complete Tenant-filtered import batch, row, reconciliation, and audit evidence package.",
        "master-data.import.replay" => "The updated import batch after replaying one quarantined row, preserving the original evidence and adding a new current attempt.",
        "procurement.organization-scope.list" => "The Tenant- and scope-filtered set of server-authorized Company/Branch options with human-readable display names.",
        "procurement.purchase-request.list" => "Tenant-filtered Purchase Request summaries with scope, status, line count, and concurrency version.",
        "procurement.purchase-request.read" => "One Tenant-filtered Purchase Request with its lines, approval snapshot, lifecycle state, and server-derived action affordances.",
        "procurement.purchase-request.create" => "The persisted Draft Purchase Request and its Product/UOM line snapshots.",
        "procurement.purchase-request.edit" => "The updated Draft or ReturnedForChange Purchase Request with a new optimistic-concurrency version.",
        "procurement.purchase-request.submit" => "The Purchase Request in PendingApproval with the effective approval-policy snapshot.",
        "procurement.purchase-request.approve" => "The Purchase Request after the immutable approval decision and any resulting stage transition.",
        "procurement.purchase-request.reject" => "The rejected Purchase Request with the recorded reason and approval evidence.",
        "procurement.purchase-request.return-for-change" => "The Purchase Request returned for change with the recorded reason and approval evidence.",
        "procurement.purchase-request.cancel" => "The eligible Purchase Request in Cancelled status with immutable cancellation evidence.",
        "procurement.purchase-request.history.read" => "Immutable Tenant-filtered lifecycle and approval history for the Purchase Request.",
        "procurement.purchase-request.audit.read" => "Immutable Tenant-filtered operation audit evidence for the Purchase Request.",
        "procurement.quotation.list" => "Tenant- and scope-filtered Supplier Quotation summaries for the approved Purchase Request.",
        "procurement.quotation.read" => "One Tenant-filtered Supplier Quotation with source-line snapshots, evidence references, lifecycle state, and concurrency version.",
        "procurement.quotation.create" => "The persisted Draft Supplier Quotation with immutable Supplier, Currency, line, and evidence snapshots.",
        "procurement.quotation.edit" => "The updated Draft Supplier Quotation with refreshed offer snapshots and a new optimistic-concurrency version.",
        "procurement.quotation.submit" => "The Supplier Quotation in Submitted status, ready for deterministic comparison.",
        "procurement.quotation.withdraw" => "The submitted Supplier Quotation in Withdrawn status with immutable reason and audit evidence.",
        "procurement.quotation.disqualify" => "The submitted Supplier Quotation in Disqualified status with immutable reason and audit evidence.",
        "procurement.quotation.compare" => "A deterministic comparison view grouped by transaction currency with coverage, commercial totals, qualification issues, and no hidden winner.",
        "procurement.source-decision.read" => "The current source decision with selected quotation, rationale, policy-stage evidence, comparison snapshot reference, and version.",
        "procurement.source-decision.history.read" => "Immutable Tenant-filtered source-decision selections with rationale, supersession lineage, and comparison snapshot references.",
        "procurement.source-decision.record" => "The persisted source decision and immutable selection history; no Purchase Order or downstream accounting effect is created.",
        "procurement.quotation.history.read" => "Immutable Tenant-filtered Supplier Quotation lifecycle history.",
        "procurement.quotation.audit.read" => "Immutable Tenant-filtered Supplier Quotation operation audit evidence.",
        "procurement.purchase-order.source.list" => "The server-filtered approved source decisions that the caller may use to create a Purchase Order.",
        "procurement.purchase-order.list" => "Tenant- and scope-filtered Purchase Order summaries with supplier, currency, total, lifecycle status, and concurrency version.",
        "procurement.purchase-order.read" => "One Tenant-filtered Purchase Order with immutable source lineage, line snapshots, approval state, pending supplier changes, and server-derived action affordances.",
        "procurement.purchase-order.create" => "The persisted Draft Purchase Order with immutable source-decision, Supplier, Currency, payment-term, and commercial-line snapshots.",
        "procurement.purchase-order.edit" => "The updated Draft or ReturnedForChange Purchase Order with a new optimistic-concurrency version.",
        "procurement.purchase-order.submit" => "The Purchase Order in PendingApproval with the effective approval-policy snapshot.",
        "procurement.purchase-order.approve" => "The Purchase Order after the immutable approval decision and any resulting policy-stage transition.",
        "procurement.purchase-order.reject" => "The rejected Purchase Order with the recorded reason and approval evidence.",
        "procurement.purchase-order.return-for-change" => "The Purchase Order returned for change with the recorded reason and approval evidence.",
        "procurement.purchase-order.issue" => "The approved Purchase Order in Issued status with immutable issue evidence and no downstream stock or accounting mutation.",
        "procurement.purchase-order.cancel" => "The eligible Purchase Order in Cancelled status with immutable cancellation evidence.",
        "procurement.purchase-order.confirmation.read" => "Immutable supplier confirmation records, line-level quantities, evidence references, and proposed supplier changes for the Purchase Order.",
        "procurement.purchase-order.confirmation.capture" => "The Purchase Order after a manual supplier confirmation, partial confirmation, rejection, or no-response record, preserving remaining quantities and proposed changes.",
        "procurement.purchase-order.supplier-change.approve" => "The Purchase Order after authorized reapproval of proposed supplier changes, with previous/proposed values and decision evidence preserved.",
        "procurement.purchase-order.supplier-change.reject" => "The Purchase Order after rejection of proposed supplier changes, preserving the original commercial values and the rejection evidence.",
        "procurement.purchase-order.history.read" => "Immutable Tenant-filtered Purchase Order lifecycle, approval, confirmation, and supplier-change history.",
        "procurement.purchase-order.audit.read" => "Immutable Tenant-filtered Purchase Order operation audit evidence with authorization, concurrency, and idempotency context.",
        "procurement.goods-receipt.eligible-source.list" => "The server-filtered Issued/Confirmed/PartiallyConfirmed Purchase Orders with a remaining eligible quantity that the caller may receive against.",
        "procurement.warehouse.list" => "The Tenant- and scope-filtered set of server-authorized Warehouse options with human-readable display names.",
        "procurement.goods-receipt.list" => "Tenant- and scope-filtered Goods Receipt summaries with source Purchase Order, status, and concurrency version.",
        "procurement.goods-receipt.read" => "One Tenant-filtered Goods Receipt with immutable source lineage, accepted/rejected/damaged line evidence, and server-derived action affordances.",
        "procurement.goods-receipt.create" => "The persisted Goods Receipt with immutable Purchase Order source lineage and accepted/rejected/damaged line snapshots.",
        "procurement.goods-receipt.cancel" => "The eligible Goods Receipt in Cancelled status with immutable cancellation evidence.",
        "procurement.goods-receipt.history.read" => "Immutable Tenant-filtered Goods Receipt lifecycle history.",
        "procurement.goods-receipt.audit.read" => "Immutable Tenant-filtered Goods Receipt operation audit evidence.",
        "procurement.invoice-handoff.eligible-source.list" => "The server-filtered recorded Goods Receipts with a remaining eligible quantity that the caller may hand off to Purchase Invoice.",
        "procurement.invoice-handoff.list" => "Tenant- and scope-filtered Purchase Invoice handoff summaries with source Goods Receipt, status, and concurrency version.",
        "procurement.invoice-handoff.read" => "One Tenant-filtered Purchase Invoice handoff with immutable Goods Receipt/Purchase Order lineage, commercial line snapshots, and server-derived action affordances.",
        "procurement.invoice-handoff.create" => "The persisted non-posted Purchase Invoice handoff with immutable Goods Receipt source lineage and commercial line snapshots.",
        "procurement.invoice-handoff.evidence.capture" => "The persisted independent supplier-declared invoice header, line, tax, and Goods Receipt allocation evidence snapshot; corrections supersede prior evidence and require a reason.",
        "procurement.invoice-handoff.cancel" => "The eligible Purchase Invoice handoff in Cancelled status with immutable cancellation evidence and no effect on its source Goods Receipt.",
        "procurement.invoice-handoff.history.read" => "Immutable Tenant-filtered Purchase Invoice handoff lifecycle history.",
        "procurement.invoice-handoff.audit.read" => "Immutable Tenant-filtered Purchase Invoice handoff operation audit evidence.",
        "procurement.matching.list" => "Tenant- and scope-filtered durable three-way match evaluation summaries.",
        "procurement.matching.read" => "A durable three-way match evaluation with source versions, immutable tolerance/FX snapshots, explicit variance classifications, and source evidence.",
        "procurement.matching.evaluate" => "The deterministic Purchase Order / active accepted Goods Receipt / independent supplier invoice evaluation result, including current partial-handoff quantity, cumulative quantity protection, configured tolerance snapshot, and server-owned MESP-120 FX evidence when applicable; exact-safe zero tolerance is used when no configured policy applies.",
        "procurement.matching.resolve-exception" => "A controlled, reasoned, optimistic-concurrency-protected exception resolution whose different-actor requirement is policy-driven and which never posts Finance/AP/accounting entries.",
        "procurement.matching.history.read" => "Immutable Tenant-filtered three-way match evaluation history.",
        "procurement.matching.audit.read" => "Immutable Tenant-filtered three-way match evaluation audit evidence.",
        "inventory.valuation.policy.read" => "Tenant-filtered effective-dated Moving Weighted Average valuation policies.",
        "inventory.valuation.policy.create" => "The persisted valuation policy with effective window, functional currency, rounding, and scope-mode evidence.",
        "inventory.valuation.state.read" => "The current durable Moving Weighted Average quantity, value, and unit-cost state for each authorized scope.",
        "inventory.valuation.history.read" => "Immutable valuation events with ledger sequence, source lineage, cost evidence, FX evidence, status, and resulting state.",
        "inventory.valuation.process" => "The durable idempotent valuation run result with applied, pending, blocked, and skipped movement counts.",
        "inventory.valuation.reconciliation.read" => "Physical-versus-valued quantity, value, in-transit, and Finance-handoff reconciliation facts.",
        "inventory.valuation.finance-handoff.read" => "Per-movement Finance handoff facts with source and valuation evidence; no journal or GL posting is created.",
        "inventory.valuation.summary.read" => "The authorized Inventory valuation reconciliation summary.",
        "inventory.valuation.pending.read" => "Pending valuation events with explicit blocking diagnostics and predecessor sequence context.",
        "inventory.valuation.in-transit.read" => "Transfer in-transit quantities and their source/receipt valuation evidence.",
        "inventory.valuation.export" => "A bounded CSV export of immutable valuation evidence with tenant, scope, policy, currency, as-of, freshness, actor, and correlation metadata.",
        "inventory.valuation.correction" => "A source-linked correction result or a truthful diagnostic when authoritative revised source evidence is unavailable.",
            _ => $"The successful result described by `{SummaryFor(operationId, descriptor.HttpMethod)}`. Read operations leave business state unchanged; a write returns the result of the owning module command."
        };
    }
}

/// <summary>Attaches contract-property documentation to generated request and response schemas.</summary>
public sealed class MiniErpOpenApiSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly Lazy<IReadOnlyDictionary<string, XElement>> ContractMembers = new(LoadContractMembers);

    public static void DescribeUnannotatedComponentProperties(OpenApiDocument document)
    {
        var schemas = document.Components?.Schemas;
        if (schemas is null)
        {
            return;
        }

        foreach (var (schemaName, schemaReference) in schemas)
        {
            if (schemaReference is not OpenApiSchema schema || schema.Properties is null)
            {
                continue;
            }

            foreach (var (propertyName, propertyReference) in schema.Properties.ToArray())
            {
                if (propertyReference is OpenApiSchemaReference reference)
                {
                    var referenceId = reference.Reference?.Id ?? reference.Id;
                    var targetSchema = referenceId is not null && schemas.TryGetValue(referenceId, out var referencedSchema)
                        ? referencedSchema
                        : null;
                    var referencedDescription = targetSchema?.Description;
                    var description = !string.IsNullOrWhiteSpace(reference.Description)
                        ? reference.Description
                        : !string.IsNullOrWhiteSpace(referencedDescription)
                            ? $"{Humanize(propertyName)} value; the `{referenceId}` schema defines its shape and accepted values. {referencedDescription}"
                            : $"{Humanize(propertyName)} value carried by the `{schemaName}` contract; the published property schema defines its shape and allowed values.";
                    schema.Properties[propertyName] = new OpenApiSchema
                    {
                        Description = description,
                        AllOf = new List<IOpenApiSchema> { reference }
                    };
                    continue;
                }

                if (propertyReference is OpenApiSchema property && string.IsNullOrWhiteSpace(property.Description))
                {
                    property.Description = $"{Humanize(propertyName)} value carried by the `{schemaName}` contract; the published property schema defines its shape and allowed values.";
                }
            }
        }
    }

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var jsonProperty = context.JsonPropertyInfo;
        if (jsonProperty is null || !string.IsNullOrWhiteSpace(schema.Description))
        {
            return Task.CompletedTask;
        }

        schema.Description = XmlDescription(jsonProperty) ?? PropertyDescription(jsonProperty, context.JsonTypeInfo);
        return Task.CompletedTask;
    }

    private static IReadOnlyDictionary<string, XElement> LoadContractMembers()
    {
        var assemblyPath = typeof(FoundationOperationCatalog).Assembly.Location;
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");
        if (!File.Exists(xmlPath))
        {
            return new Dictionary<string, XElement>(StringComparer.Ordinal);
        }

        return XDocument.Load(xmlPath)
            .Descendants("member")
            .Where(member => member.Attribute("name") is not null)
            .ToDictionary(member => (string)member.Attribute("name")!, StringComparer.Ordinal);
    }

    private static string? XmlDescription(JsonPropertyInfo jsonProperty)
    {
        if (jsonProperty.AttributeProvider is not MemberInfo member)
        {
            return null;
        }

        var declaringType = member.DeclaringType;
        if (declaringType is null)
        {
            return null;
        }

        var propertyName = $"P:{declaringType.FullName}.{member.Name}";
        if (TrySummary(propertyName, out var propertySummary))
        {
            return propertySummary;
        }

        var constructorPrefix = $"M:{declaringType.FullName}.#ctor(";
        foreach (var constructor in ContractMembers.Value.Where(entry => entry.Key.StartsWith(constructorPrefix, StringComparison.Ordinal)))
        {
            var parameter = constructor.Value.Elements("param")
                .SingleOrDefault(item => string.Equals((string?)item.Attribute("name"), member.Name, StringComparison.OrdinalIgnoreCase));
            if (parameter is not null)
            {
                var summary = Normalize(parameter.Value);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    return summary;
                }
            }
        }

        return null;
    }

    private static bool TrySummary(string memberName, out string summary)
    {
        summary = string.Empty;
        if (!ContractMembers.Value.TryGetValue(memberName, out var member))
        {
            return false;
        }

        summary = Normalize(member.Element("summary")?.Value ?? string.Empty);
        return !string.IsNullOrWhiteSpace(summary);
    }

    private static string PropertyDescription(JsonPropertyInfo jsonProperty, JsonTypeInfo typeInfo)
    {
        var name = jsonProperty.Name;
        var label = Humanize(name);
        var type = Nullable.GetUnderlyingType(jsonProperty.PropertyType) ?? jsonProperty.PropertyType;
        var typeName = Humanize(typeInfo.Type.Name.Replace("Request", string.Empty, StringComparison.Ordinal)
            .Replace("Response", string.Empty, StringComparison.Ordinal));

        var known = name.ToLowerInvariant() switch
        {
            "id" => "Stable identifier of the represented resource or evidence record.",
            "tenantid" => "Tenant identity associated with this value; Tenant authority is established by the server and never by this field.",
            "companyid" => "Company identity inside the already authorized Tenant; this value cannot widen Tenant or organization scope.",
            "branchid" => "Branch identity inside the already authorized Tenant; this value cannot widen Tenant or organization scope.",
            "warehouseid" => "Warehouse identity validated within the authorized Tenant and Company/Branch scope.",
            "version" or "runversion" or "reconciliationversion" => "Current opaque resource version used for optimistic concurrency; mutation requests use `If-Match` where required.",
            "idempotencykey" => "Idempotency key associated with this mutation or its immutable evidence.",
            "effectivefrom" => "First calendar date of the effective window.",
            "effectiveto" => "Optional final calendar date of the effective window.",
            "effectiveon" => "Calendar date used to select the effective version for this operation.",
            "asofdate" => "Business date through which the result is evaluated.",
            "reason" or "changereason" or "decisionreason" => "Reason recorded with the requested lifecycle or evidence change.",
            "status" or "state" or "lifecycle" or "runstatus" => "Current lifecycle or processing state returned by the owning module.",
            "code" or "safecode" => "Stable code used to identify the referenced resource or classified outcome.",
            "amount" or "transactionamount" or "grossamount" or "netamount" or "taxamount" or "value" => "Exact decimal amount recorded in the currency and accounting context carried by the contract.",
            "quantity" or "acceptedquantity" or "receivedquantity" or "orderedquantity" or "requestedquantity" => "Exact decimal quantity in the line's referenced unit of measure.",
            "rate" or "exchangerate" or "appliedrate" => "Exact decimal rate resolved or supplied under the owning module's precision and provenance rules.",
            "currencycode" => "Currency code associated with the amount; it does not perform currency conversion by itself.",
            "password" => "Credential supplied only to the first-party sign-in operation; it is never returned as a response value.",
            "login" => "User login identifier supplied to the first-party sign-in operation.",
            "correlationid" => "Identifier used to correlate this request with its safe evidence and result.",
            "reasoncode" or "findingcode" or "outcomecode" or "resultcode" => "Machine-readable classification returned by the owning module for the represented outcome.",
            _ => null
        };
        if (known is not null)
        {
            return known;
        }

        if (type == typeof(bool))
        {
            return $"Boolean indicating whether {label.ToLowerInvariant()} applies to this {typeName} value.";
        }
        if (type.IsEnum)
        {
            return $"Enumerated {label.ToLowerInvariant()} selection for this {typeName} value; accepted values are defined by the published enum.";
        }
        if (type == typeof(DateOnly))
        {
            return $"Calendar date for {label.ToLowerInvariant()} on this {typeName} value.";
        }
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return $"Timestamp for {label.ToLowerInvariant()} on this {typeName} value.";
        }
        if (type == typeof(decimal))
        {
            return $"Exact decimal {label.ToLowerInvariant()} recorded by this {typeName} contract; the owning module defines its unit, scale, and currency context.";
        }
        if (type == typeof(Guid))
        {
            return $"Stable identity for {label.ToLowerInvariant()} referenced by this {typeName} contract.";
        }
        if (type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            return $"Collection of {label.ToLowerInvariant()} values carried by this {typeName} contract.";
        }

        var typeSummaryName = $"T:{(jsonProperty.AttributeProvider is MemberInfo propertyMember ? propertyMember.DeclaringType?.FullName : typeInfo.Type.FullName)}";
        var summary = TrySummary(typeSummaryName, out var contractSummary)
            ? $" The containing contract is: {contractSummary}"
            : string.Empty;
        return $"Text or structured value for {label.ToLowerInvariant()} in this {typeName} contract.{summary}";
    }

    private static string Humanize(string value)
    {
        var separated = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2").Replace('_', ' ');
        return string.Join(' ', separated.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static string Normalize(string value) => Regex.Replace(value, "\\s+", " ").Trim();
}

#pragma warning restore CS1591
