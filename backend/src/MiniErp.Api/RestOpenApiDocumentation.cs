#pragma warning disable CS1591

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

        return Task.CompletedTask;
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
        operation.Summary = SummaryFor(descriptor.OperationId);
        operation.Description = DescriptionFor(descriptor);
        AddExchangeRateReferenceParameter(operation, descriptor);
        AddPriceListReferenceParameters(operation, descriptor);
        operation.Responses ??= new OpenApiResponses();
        AddResponse(operation, "200", SuccessResponseFor(descriptor.OperationId));
        AddResponse(operation, "400", "The request shape, effective date, explicit calculation input, or validation rule is invalid.");
        AddResponse(operation, "401", "The caller has no authenticated first-party session for this operation.");
        AddResponse(operation, "403", "The server-derived Tenant, permission, scope, or antiforgery authority does not allow the operation.");
        AddResponse(operation, "404", "The Tenant-owned resource or effective-dated version is not available.");
        AddResponse(operation, "409", "The request conflicts with an existing identity, effective window, idempotency binding, or optimistic-concurrency version.");
        AddResponse(operation, "503", "The required persistence, authorization, or audit boundary is unavailable; no success is claimed.");

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

    private static void AddResponse(OpenApiOperation operation, string statusCode, string description)
    {
        if (!operation.Responses!.ContainsKey(statusCode))
        {
            operation.Responses[statusCode] = new OpenApiResponse { Description = description };
        }
    }

    private static string SummaryFor(string operationId) => operationId switch
    {
        "platform.health" => "Check platform availability",
        "platform.openapi" => "Read the generated API contract",
        "platform.module-registration" => "Read registered module boundaries",
        "auth.development-bypass" => "Establish the configured Development QA session",
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
        _ => GenericSummary(operationId)
    };

    private static string GenericSummary(string operationId)
    {
        var parts = operationId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return "Use the documented platform operation";
        var resource = ToDisplay(parts[^2]);
        var action = parts[^1] switch
        {
            "list" => "List",
            "read" => "Read",
            "create" => "Create",
            "edit" => "Edit",
            "deactivate" => "Deactivate",
            "reactivate" => "Reactivate",
            "audit" => "Read audit evidence",
            "preview" => "Preview",
            _ => "Use"
        };
        return $"{action} {resource}";
    }

    private static string DescriptionFor(FoundationOperationDescriptor descriptor)
    {
        if (descriptor.OperationId == "auth.development-bypass")
        {
            return "Development-only, loopback-only session establishment for the configured server-side Development actor. "
                + "The request accepts no login, password, Tenant, role, permission, or identity input. "
                + "The path is unavailable unless the exact Development environment and explicit MESP_DEV_AUTH_BYPASS=true setting are both present; "
                + "it must remain disabled outside local Development and never substitutes for production authentication.";
        }

        var contextRules = $"Security profile: {descriptor.SecurityProfile}. Scope policy: {descriptor.ScopePolicy}. "
            + $"Exact permission: {descriptor.ExactPermissionCode ?? "none"}. "
            + $"Antiforgery required: {descriptor.RequiresAntiforgery}. "
            + $"Mandatory audit evidence: {descriptor.RequiresMandatoryAudit}. "
            + $"Concurrency contract: {descriptor.Concurrency}. "
            + $"Idempotency contract: {descriptor.Idempotency}. "
            + $"Effective-date contract: {descriptor.EffectiveDate}. "
            + "Tenant and actor authority are derived by the server; request fields cannot select a foreign Tenant or broaden scope. ";

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
            + "The operation is part of the reusable internal ERP contract. Response failures use Problem Details "
            + "with a stable code, correlation identifier, and operation identifier; provider details and internal "
            + "implementation types are not exposed."
            + (descriptor.IsUnsafe
                ? " The operation is a state-changing boundary and must preserve idempotency, concurrency, and audit rules."
                : string.Empty);
    }

    private static string SuccessResponseFor(string operationId) => operationId switch
    {
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
        _ => "The documented operation result with no provider or internal implementation details."
    };

    private static string ToDisplay(string value) =>
        string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(item =>
            item.Length == 0 ? item : char.ToUpperInvariant(item[0]) + item[1..]));
}

#pragma warning restore CS1591
