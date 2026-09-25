# Mini ERP SaaS Platform - Technology Architecture Baseline

> **Historical capability-record note - 30 August 2026.** The dated MESP-131
> and MESP-132 overlays below preserve architecture evidence from their original
> handoffs. They are not live project-state authority; current status is
> maintained in `TASK.md`, `.ai/CURRENT_STATE.md`, and `docs/staticts.md`.

<!-- MESP-131-ARCH-START -->
## Current MESP-131 valuation architecture overlay — 24 August 2026

MESP-131 is accepted and merged through PR #75 at
`a8664d6a0d006e463a1a03fadd76c28475475f58`; it extends the existing Inventory
module and does not create a parallel Finance or Reporting domain. MESP-132 is
the merged Finance foundation from PR #76; Jira closure remains with Sol.

- **Ordering:** every physical Inventory movement receives a durable Tenant + Company `LedgerSequence`; future valuation does not use `PostedAt` or `EffectiveDate` as commit-order authority.
- **Bootstrap:** pre-MESP-131 movement sequence is deterministically backfilled by Tenant, Company, `PostedAt`, and movement ID as a one-time pre-production starting order.
- **Policy:** valuation is Tenant-owned, Company-specific, effective-dated and versioned, with Warehouse/Product/UOM or Warehouse/Product/UOM/Tracking scope, functional currency, precision and explicit rounding. Version numbers are monotonic; compatible transitions carry the pool state, while incompatible transitions fail closed for rebaseline.
- **MWA:** decimal deterministic inbound weighted-cost accumulation and outbound consumption at the persisted rounded prior average; empty current-MWA adjustments/count variances remain Pending rather than inventing zero cost.
- **FX:** exact active effective-dated MESP-120 Exchange Rate version/provenance evidence is snapshotted; no client rate, external feed, inverse guess or silent latest-rate fallback.
- **History:** current valuation state is a projection; applied valuation events are append-only and preserve source, policy, rate, prior/new state, movement value, actor and correlation.
- **Pending predecessor:** unknown-policy evidence uses a conservative base pool; once policy scope is known, unresolved cost/policy/FX/link evidence stops only the derived valuation scope. Tracking policies therefore isolate LOT-A and LOT-B; non-tracking policies deliberately combine them. Process mutation accepts only safe Company/Branch/Warehouse/Product/UOM filters and always orders by LedgerSequence.
- **Transfers:** shipment uses source MWA; In-Transit quantity and value conserve shipped minus received/lost/returned quantities; receipt/loss/return inherit linked transfer valuation.
- **Corrections:** original valuation evidence is never rewritten; physical corrections append linked valuation reversal/delta evidence.
- **Finance boundary:** Inventory emits versioned valuation handoff facts only. Journals, GL/AP/AR, account mapping, periods and financial posting are owned by the bounded MESP-132 Finance foundation and its explicitly deferred downstream capabilities.
- **Reporting boundary:** MESP-131 owns Inventory valuation/reconciliation views and bounded audited CSV export only; generic Reporting remains MESP-139.
- **Concurrency:** durable pool serialization, Serializable persistence, rowversion/uniqueness, SHA-256 fingerprints, existing Inventory idempotency replay, and first-scope conflict handling prevent duplicate/forked applied valuation.
- **Finance handoff:** `inventory-valuation-finance.v1` exposes Direction, non-negative BaseAmount, signed inbound/outbound effect, and immutable rounding-adjustment evidence; Inventory creates no journals.
- **Closeout invariant:** full depletion uses stored prior value as actual movement value, preserves rounded formula value and `RoundingAdjustmentAmount`, and leaves quantity, value, and average at zero. Persistence rejects impossible negative or zero-quantity/non-zero-value state transitions; reconciliation reports legacy impossible state as `ValuationMismatch` and summary becomes partial.
- **Summary:** `/summary` is a dedicated Warehouse aggregate with explicit IsComplete/IsPartial and Pending/Blocked/InTransit facts; no warehouse-level AverageUnitCost is exposed.
- **Migration:** additive `20260823124304_MESP131MovingWeightedAverageValuation`, `20260823180537_MESP131SolFinancialIntegrityRemediation`, and regenerated final additive `20260823225921_MESP131SolFinalValuationIntegrity`; its populated Designer carries the exact three approved evidence columns, and prior migrations remain unchanged.

See `docs/34_MESP-131_MWA_Valuation_Architecture.md` for the full bounded handoff.
<!-- MESP-131-ARCH-END -->

<!-- MESP-132-ARCH-START -->
## Current MESP-132 Finance foundation overlay - 24 August 2026

MESP-132 adds the bounded Company-owned Finance / GL foundation, squash-merged
through PR #76 into `main` at `ccc52a892c8258778f57c55c12fa0032bd3e276b` from
accepted feature head `c0e04553db3c7b04fa7f7870b60fc439ec8a40b7`. The retained
feature branch is `feat/MESP-132-finance-foundation`; its source/test
implementation commit is `dcae7e231bd264580c33e60c35f5cc8436c4f050` from
starting SHA `2f523582fbd3394b1eb11580eff490ba83aa9afb`, based on
`fcec241dfedb529fef89d4336adf1e571917c52a`. MESP-132 remains Jira In Progress
pending Sol closure and MESP-10 reconciliation. It follows the existing four-project direction:
Contracts expose
Finance records, App owns request context/authorization seams, Infrastructure
owns `FinanceDbContext`, `finance` schema entities/migrations and provider
composition, and Api maps the REST/OpenAPI surface without a direct EF
dependency.

- **Company boundary:** server-configured active Companies are the Finance
  book boundary. Each Company owns functional currency, COA, fiscal calendar,
  periods, dimensions, journals, GL facts and posting rules; no Tenant-wide
  accounting currency or cross-Company book is inferred.
- **COA:** normalized Company-unique account codes, five standard account
  types, hierarchy/cycle checks, posting/grouping eligibility, lifecycle and
  effective dates, optimistic versions, and account code/name snapshots on
  journal lines.
- **Fiscal control:** explicit Company Calendar, Year and non-overlapping
  Period boundaries. Periods are Draft/Open/SoftClosed/Closed; posting-date
  resolution is server-side and ambiguous/no-period resolution fails closed.
  No automatic year-end retained-earnings, P&L close, carry-forward or
  opening journal is fabricated.
- **Finance dimension:** Cost Center is the bounded approved dimension. The
  repository had no existing persisted Master Data Cost Center to reuse, so
  the narrow Company-applicable structure is Finance-owned; no other generic
  dimensions are introduced.
- **GL:** Draft/Submitted/Approved/Rejected/Cancelled/Posted/Reversed
  journals, immutable Posted lines, exact functional-currency balancing,
  account/dimension/period checks, controlled equal-and-opposite reversal,
  and derived GL inquiry over Posted Journal Lines.
- **Rules and lineage:** versioned effective-dated source/event mappings,
  explicit lifecycle, deterministic single-rule selection, exact
  `inventory-valuation-finance.v1` handoff consumption, and Tenant/Company/
  contract/evidence/version uniqueness for source financial effects.
- **Security and resilience:** trusted Tenant/Company context, exact
  operation permissions, existing approval/SoD seams, antiforgery,
  idempotency, If-Match concurrency, Serializable transactions, safe Problem
  Details and immutable audit evidence.
- **Frontend:** lazy `/app/finance` with server-provided selectors, bounded
  Finance and GL views, EN/AR and RTL support, accessible responsive forms,
  and no raw GUID entry. Finance does not compute accounting authority in the
  browser.

The formal Finance migration creates only Finance-owned tables and preserves
Tenancy ownership of shared `tenancy.TenantOwnedRecords`. Production SQL
provider selection, migration/cutover, backup/restore, legal/specialist
validation and external/statutory integration remain production gates.

See `docs/35_MESP-132_Finance_Foundation_Architecture.md` for the bounded
implementation handoff and explicit deferred scope.
<!-- MESP-132-ARCH-END -->

> **Current MESP-123 B2 implementation overlay - 16 August 2026.** The
> approved shared SQL Server direction now has a bounded local Development
> execution path: an explicit `MESP_SQLSERVER_CONNECTION_STRING` selects the
> formal module-owned migrations for server `.` / database `MESP`, with
> distinct history tables and Tenancy-only ownership of the shared
> `TenantOwnedRecords` table. A dedicated inventory-first utility copied the
> existing local SQLite rows with IDs/Tenant IDs, foreign-key lineage, source
> hashes, and recoverable backups verified; SQLite originals remain retained.
> Production startup never auto-migrates, and this local cutover is not a
> production deployment or readiness approval. The final B2 evidence is
> Release build 0/0, backend 752/752, Angular 190/190 across 20 spec files,
> Angular initial build 459.20 kB, Playwright 4/4, and a real browser pass of
> light/dark branding, RTL/collapsed shell behavior, server-derived Tenant
> naming, and migrated Purchase Request records. Owner source assets under
> `frontend/assets` remain unchanged.

> **MESP-108 validation-scope clarification - 10 August 2026.** The current
> normal backend gate is 670/670. The separately gated 21 SQL safety cases are
> Foundation-only and exercise the disposable LocalDB
> `TenantPersistenceDbContext` harness, not `MasterDataDbContext` or
> `BusinessPartiesDbContext`. They do not prove module SQL mappings, indexes,
> transactions, collation, or Arabic linguistic behavior. ADR-011 and
> module-owned provider validation remain open at their existing scope; see
> `docs/98_Independent_Opus_5_Checkpoint_Reconciliation.md`.

> **Authoritative current Product-readiness overlay - 9 August 2026.**
> MESP-99 / M95-SL-02 Category and UOM is Done through PR #33, correction PR
> #34, and final audit-semantics correction PR #35. MESP-101 completed the
> M95-SL-03 Product identity readiness gate through PR #36 at
> `c7392a55e0b60fd83e48447e3f9218f82cfaccea`, with closure evidence `10672`.
> MESP-102 completed the bounded Product implementation through PR #37 at
> `202d59068caac5d1fac402794627e41d7f452456`; Jira activation, validation,
> and closure evidence are `10675`, `10676`, and `10677`. The approved
> Product-only bounds are MD-OD-001, MD-OD-003, MD-OD-005, MD-OD-008,
> MD-OD-010, and MD-OD-011. ADR-002's four-project topology remains binding:
> `MiniErp.Api`, `MiniErp.App`, `MiniErp.Contracts`, and
> `MiniErp.Infrastructure`; ADR-006 remains authoritative for shared SQL
> Server, Tenant ownership, module-owned contexts/schemas/migrations, and
> provider/production gates. Product persistence remains module-owned and no
> migration or production/provider validation is claimed because the SQL safety
> gate is unavailable. ADR-005 remains the approved baseline policy and
> resource-authorization record; ADR-011 is a required future decision before
> localized search, forms, or bilingual/RTL document behavior. MESP-48,
> MESP-49, and MESP-50 remain open.

> **Authoritative current Supplier implementation overlay - 10 August 2026.**
> MESP-104 / M95-SL-04 is **Done** through PR #39, whose implementation head
> `9bf9afcd8a9ea427ed32b63ad9b655081e9592d3` merged to `main` at
> `721adeb27c366d2b8aedde66d006ac6a49956f99`. The bounded Supplier slice is
> module-owned in the Business Parties application/infrastructure path and
> includes Tenant-filtered ownership, server-derived authorization,
> optimistic concurrency, append-before-effect audit, and Supplier-only API
> contracts/endpoints. Release validation was 0/0, Supplier tests 7/7, and
> non-SQL tests 609/609. The 21 SQL Server safety tests remain gated by the
> unavailable `MESP_SQLSERVER_CONNECTION_STRING`; no migration, provider, or
> production deployment claim is made. MESP-105 / M95-SL-05 Business Customer
> readiness is now Done with Owner disposition evidence `10691`; MESP-107 is
> the separate active implementation item with activation evidence `10692`.
> No downstream module or unified Party behavior is authorized by that
> activation.

> **Authoritative current Business Customer implementation overlay - 10 August 2026.**
> MESP-105 is Done for the readiness/decision gate. The approved Customer-only
> bounds are Tenant-wide Customer identity inside the owning Tenant with no
> cross-Tenant sharing, server-derived Tenant/resource authority, no separate
> approver for routine Customer master-data maintenance, and no Draft with
> Active-on-authorized-create plus guarded Deactivate/Reactivate. MESP-107 is
> the single active implementation item. ADR-002, ADR-005, ADR-006, and
> ADR-011 remain authoritative; MD-OD-007 and MESP-48/MESP-49/MESP-50 remain
> open gates. No Customer source behavior was added by the readiness/activation
> handoff, and downstream commercial or statutory behavior remains outside
> MESP-107.

> **Historical MESP-100 state overlay - 9 August 2026.** MESP-100 is Done
> (closure evidence Jira comment `10663`) and MESP-99 is In Progress
> (activation evidence `10664`). PR #32 merged at
> `511f6be9f005e54930f993aead9758d7a66b75a8`. ADR-002 is now published and
> reconciles the actual four-project topology; MESP-100 added no Category/UOM
> persistence or business behavior. MESP-48, MESP-49, and MESP-50 remain
> separately gated.

> **Current ADR-002 reconciliation overlay — 9 August 2026.** The repository's
> approved Release 1 production topology is four projects: `MiniErp.Api`,
> `MiniErp.App`, `MiniErp.Contracts`, and the existing
> `MiniErp.Infrastructure`. ADR-002 is published at
> `docs/ADR-002_Backend_Project_Structure_and_Module_Enforcement.md`.
> The permitted project direction is `MiniErp.Api -> MiniErp.Infrastructure ->
> MiniErp.App -> MiniErp.Contracts`, with Api also directly consuming the
> existing App/Contracts host seams. Infrastructure does not depend on Api;
> Contracts does not depend on App/Infrastructure/Api. ADR-006 remains the
> authority for shared SQL Server, Tenant ownership, module-owned schemas,
> migrations, and production/provider gates. No Category/UOM persistence is
> created by this reconciliation.

| Field | Value |
|---|---|
| Document | Technology Architecture Baseline |
| Version | 1.0 |
| Status | Approved Architecture Baseline |
| Product release | Release 1 - B2B ERP |
| Jira foundation | MESP-22 - Create Product Decision Register |
| Source PRD | `docs/MESP_PRD_v1.2.docx` (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Source glossary | docs/00_ERP_Business_Glossary.md |
| Prepared | 1 August 2026 |
| Architecture owner | Hossam |

## Purpose and authority

This document turns the approved product-level technology direction into a practical architecture baseline for the Mini ERP SaaS Platform. It is intended to guide the later solution design and implementation backlog after the applicable BRD gates have been approved.

The PRD and ERP Business Glossary remain the authority for product scope, business terminology, business rules, accounting meaning, state transitions, and Release 1 boundaries. This document owns technology structure and engineering controls only. It does not answer unresolved business decisions, create implementation Stories, or authorize development before BRD entry and module exit criteria are met.

The approved organizational hierarchy remains:

> Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse

Release 1 remains B2B ERP only. Retail POS, cashier operations, cash drawers, retail shifts, and retail checkout are excluded.

---

# 1. Executive architecture decision

Release 1 will be built as a single, well-structured Modular Monolith using Angular 22 and TypeScript for the first-party web application, ASP.NET Core Web API on .NET 10 LTS for the server application, Entity Framework Core 10 for persistence, and Microsoft SQL Server 2025 as the shared relational database.

The system will be one product and one primary deployment unit, organized into explicit business modules with controlled dependencies and database schemas owned by those modules. Modules may communicate through in-process contracts and durable internal events, but one module must never directly update another module's owned tables.

The first-party Angular application will authenticate through ASP.NET Core Identity using secure HTTP-only cookies. Authorization will be enforced on the server through policies, tenant membership, organizational scope, permissions, document state, and relevant business context.

All tenants will share one SQL Server database in Release 1. Tenant isolation is mandatory in the application, persistence model, jobs, files, exports, audit records, and database constraints. A database-per-tenant model is not part of this baseline.

Files will be stored in private object storage, with metadata and business links stored in SQL Server. Local development will use Docker Compose for dependencies and repeatable setup. Logging, metrics, and tracing will use OpenTelemetry-compatible instrumentation. xUnit will validate backend behavior, while Playwright TypeScript will cover critical browser and API journeys.

This architecture is deliberately sized for one developer:

- One repository and one primary application.
- A small number of production projects rather than one project per business concept.
- No Kubernetes, service mesh, message broker, distributed cache, or search cluster in the initial baseline.
- One database with module-owned schemas.
- Background processing using .NET hosted workers and durable SQL-backed work records.
- Provider abstractions only where the PRD already requires replaceable external infrastructure, such as object storage, email, e-invoicing, and observability exporters.

# 2. Technology stack and versions

| Layer | Approved technology | Baseline use |
|---|---|---|
| Web application | Angular 22 | First-party responsive B2B ERP user interface |
| Web language | TypeScript | Strictly typed application and Playwright tests |
| Server runtime | .NET 10 LTS | Supported runtime for the Release 1 server application |
| Server framework | ASP.NET Core Web API | REST endpoints, Identity, policies, health checks, middleware, and hosted workers |
| Persistence | Entity Framework Core 10 | SQL mapping, transactions, migrations, concurrency, and query composition |
| Database | Microsoft SQL Server 2025 | Shared transactional database with module schemas and tenant controls |
| Architecture style | Modular Monolith | One deployable with enforced module ownership and extraction seams |
| API style | REST and OpenAPI | Versioned HTTP contracts and generated API documentation |
| Authentication | ASP.NET Core Identity | User, credential, lockout, security-stamp, recovery, and MFA foundation |
| Browser session | Secure HTTP-only cookie | First-party Angular session; no browser token storage |
| Authorization | ASP.NET Core policy-based authorization | Permission, tenant, organizational scope, state, and resource checks |
| File persistence | Private object storage | Attachments, imports, exports, and generated reports |
| Local orchestration | Docker Compose | Repeatable SQL Server and supporting dependency setup |
| Telemetry | OpenTelemetry-compatible logs, metrics, and traces | Provider-neutral operational visibility |
| Backend validation | xUnit | Unit, integration, contract, security, and invariant tests |
| End-to-end validation | Playwright TypeScript | Critical UI journeys and HTTP API validation |

Version policy:

- Pin the approved major versions and commit deterministic dependency lock files.
- Use the latest security-supported patch within the approved major version after automated validation.
- Do not take a major framework, runtime, ORM, or database upgrade without an ADR and compatibility test.
- Confirm vendor support status, container images, licensing, hosting compatibility, and upgrade paths before the production design is frozen.

# 3. Reasons for each selection

## Angular 22 and TypeScript

Angular provides a structured application model suitable for a large form- and workflow-oriented ERP. Typed forms, routing, dependency injection, HTTP tooling, accessibility support, and mature test integration reduce the number of architectural choices a single developer must maintain. TypeScript strict mode reduces contract errors between features and the API.

The application will use standalone components and lazy feature routes. Angular signals and small feature services are preferred for local state. A separate global state-management library is deferred until a measured requirement cannot be handled cleanly by Angular primitives.

## ASP.NET Core Web API and .NET 10 LTS

ASP.NET Core provides Identity, policy authorization, antiforgery, rate limiting, health checks, background services, dependency injection, structured logging, OpenAPI integration, and high-performance HTTP handling in one coherent platform. The LTS runtime supports a stable Release 1 maintenance horizon.

## Entity Framework Core 10

EF Core provides a productive persistence layer for one developer while retaining transactions, optimistic concurrency, migrations, interceptors, query filters, and direct SQL escape hatches when carefully reviewed. EF Core abstractions are guardrails, not the only tenant-isolation control.

## Microsoft SQL Server 2025

The ERP requires strong transactional consistency, decimal precision, relational constraints, indexing, backup and restore, and reliable reporting over finance and inventory data. SQL Server aligns with the selected .NET stack and supports one shared database with business-module schemas.

## Modular Monolith

The PRD already approves a Modular Monolith. It avoids the operational and consistency cost of distributed services while allowing strong business ownership, explicit contracts, independent internal testing, and future extraction if measured scale or team ownership requires it.

## REST and OpenAPI

REST keeps the client/server interaction conventional and testable. OpenAPI is the contract authority for the Angular client, Playwright API tests, and approved future integrations. GraphQL and multiple competing API styles are not needed for Release 1.

The running API is the single documentation source: each public operation is
registered in the Foundation operation catalogue, mapped with its exact route
and security metadata, and transformed into an explicit generated OpenAPI
operation identifier, summary, boundary description, and response outcomes.
Scalar is the developer-facing view over that generated document in
Development and QA only, with its agent capability disabled. Scalar is not a
second contract, an integration gateway, or a production authorization path.

## ASP.NET Core Identity, secure cookies, and policies

Identity supplies a maintained credential and session foundation. A secure HTTP-only cookie prevents the Angular application from reading or persisting bearer tokens in browser storage. Policy authorization lets the server evaluate the full business context instead of relying on UI visibility or broad role names.

## Shared SQL database with module schemas

A shared database keeps deployment, backup, transactions, and operations manageable for one developer. Separate schemas and code ownership prevent the shared database from becoming an unstructured shared-data model. Strict tenant controls apply to every tenant-owned table.

## Object storage

Binary files do not belong in transactional database rows. Private object storage provides durable file handling, controlled access, retention capabilities, and independent capacity scaling while SQL Server retains authoritative metadata and business relationships.

## Docker Compose

Docker Compose makes local SQL Server and optional supporting services reproducible without introducing a production orchestrator. The developer can run the Angular and .NET processes on the host for fast reload while dependencies run in containers.

## OpenTelemetry

OpenTelemetry-compatible instrumentation avoids binding the codebase to one monitoring vendor. The same trace and correlation context can cover the Angular request, API command, SQL work, background job, file operation, and external adapter.

## Playwright and xUnit

xUnit is the primary backend test framework for domain rules, persistence, authorization, concurrency, and module integration. Playwright TypeScript validates the actual first-party browser behavior and critical APIs, including cookies, antiforgery, RTL layout, and tenant-isolation failures.

# 4. Modular Monolith boundaries

The initial modules are business boundaries, not separate deployments:

| Module | Primary ownership | Allowed upstream dependencies |
|---|---|---|
| Platform Administration | Tenant lifecycle, subscriptions, plans, entitlements, quotas, platform operations | Shared Kernel only |
| Identity and Access | Users, credentials, memberships, roles, permissions, sessions, access scopes | Platform contracts |
| Organization | Company/legal-entity, Branch, and Warehouse identity and relationships; organizational scopes and numbering scopes | Platform and Identity contracts |
| Master Data and Catalog | Products, items, units, categories, terms, and shared reference data | Organization contracts |
| Business Parties | Generic party identity, names, contacts, addresses, and cross-role identity | Organization contracts |
| Procurement | Supplier purchasing profile, purchase requests, supplier quotations, purchase orders, supplier confirmations, and procurement controls | Organization, Business Parties, and Master Data contracts |
| Inventory | Stock ledger, balances, reservations, Goods Receipts, Deliveries, Supplier/Customer Returns, transfers, counts, adjustments, and costing | Organization and Master Data contracts; Procurement and Sales events/contracts |
| B2B Sales | Customer commercial profile, quotations, sales orders, pricing/credit controls, and fulfillment orchestration | Organization, Business Parties, Master Data, and Inventory availability contracts |
| Finance | Fiscal calendars, chart of accounts, purchase/sales invoices and credits, payments/receipts, posting rules, GL, AP, AR, cash/bank, tax posting, periods, and reconciliation | Organization, Business Parties, and versioned posting contracts from transactional modules |
| Reporting and Analytics | Read models, report definitions, report jobs, exports, freshness | Approved read contracts from all modules |
| Saudi Country Pack | Saudi defaults, tax/e-invoice rules, statutory document configuration, adapter contracts | Organization, Finance, Sales, Procurement contracts |
| Security and Audit | Audit events, privileged-support records, security evidence | Receives material event contracts from all modules |
| Files and Integrations | Attachment metadata, file lifecycle, imports/exports, email/webhook/external adapter execution | Versioned contracts from owning business modules |

Boundary rules:

1. Each business concept has one owning module.
2. Only the owning module may mutate its tables.
3. Other modules reference stable identifiers and public contracts, not entity classes or repositories.
4. Cross-module calls use application-level interfaces. Direct cross-module DbSet or table writes are prohibited.
5. Authoritative commands execute in an explicit transaction. Cross-module atomic work is coordinated through module APIs under one SQL transaction only when a BRD-approved invariant requires immediate consistency.
6. Non-authoritative propagation, notifications, reports, and external delivery use durable internal events and an outbox. This is not event sourcing.
7. Events describe completed facts. They do not allow another module to bypass its own validation.
8. Reporting may use approved read models or read-only views; it must not mutate source tables or bypass authorization.
9. Shared Kernel contains technical primitives only, such as identifiers, result types, clock abstraction, money primitives, and correlation metadata. It must not become a shared business-model dumping ground.
10. Architecture tests will enforce forbidden references and module dependency direction.

## Source ownership reconciliation

The PRD domain table and the glossary's Owning module fields do not always assign the same concept to the same module. The architecture therefore distinguishes organizational identity, commercial process responsibility, physical inventory execution, and financial posting:

- Organization owns the Warehouse's place in the hierarchy; Inventory owns stock, movements, receipts, deliveries, and returns performed at that Warehouse.
- Business Parties owns common counterparty identity; Procurement owns the Supplier purchasing role and Sales owns the Business Customer commercial role.
- Procurement owns the process through Purchase Order and Supplier Confirmation; Inventory owns the physical Goods Receipt and Supplier Return; Finance owns Purchase Invoice, Supplier Credit Note, and Supplier Payment.
- B2B Sales owns Quotation and Sales Order; Inventory owns the physical Delivery and Customer Return; Finance owns Sales Invoice, Credit Note, and Customer Receipt.
- Finance owns Fiscal Calendar and Fiscal Period behavior even though Organization provides the Company scope.

This mapping is a provisional architecture seam that reconciles the two source documents without changing business meaning. Final aggregate, document, and schema ownership must be confirmed in the Organization, Master Data, Procurement, Inventory, B2B Sales, and Finance BRDs and then approved in ADR-001/ADR-006. Until that approval, no implementation may rely on a different ownership model.

# 5. Backend solution structure

The backend should begin with a small project count:

    backend/
      MiniErp.sln
      src/
        MiniErp.Api/
          Authentication/
          Middleware/
          OpenApi/
          Health/
          Program.cs
        MiniErp.App/
          BuildingBlocks/
            Application/
            Domain/
            Infrastructure/
            Tenancy/
            Security/
            Observability/
          Modules/
            Platform/
              Domain/
              Application/
              Infrastructure/
              Api/
            Identity/
            Organization/
            MasterData/
            Parties/
            Procurement/
            Inventory/
            Sales/
            Finance/
            Reporting/
            SaudiCountryPack/
            Audit/
            Files/
            Integrations/
        MiniErp.Contracts/
          Api/
          Modules/
          Events/
        MiniErp.Infrastructure/
          Persistence/
          Modules/
            <module-owned contexts, mappings, schemas, migrations>
      Directory.Build.props
      Directory.Packages.props

    tests/
      backend/
        MiniErp.UnitTests/
        MiniErp.IntegrationTests/
        MiniErp.ArchitectureTests/

Practical rules for one developer:

- Start with the four existing production projects: Api, App, Contracts, and
  Infrastructure. Api is the host/composition root; Infrastructure is the
  provider/persistence implementation project.
- Enforce the project direction `Api -> Infrastructure -> App -> Contracts`;
  Api may also reference App and Contracts for host composition. Contracts has
  no production-project dependency, App has no EF Core or Infrastructure
  dependency, and Infrastructure never references Api.
- Keep module internals internal to MiniErp.App and expose only explicit contracts.
- Do not create one assembly per module initially. Split a module into its own project only when architecture tests and internal access controls are insufficient.
- Keep endpoint definitions thin. They authenticate, validate transport data, call an application use case, and map its result.
- Keep business invariants in domain code, not controllers, EF configurations, Angular code, or database triggers.
- Keep provider-specific integrations behind interfaces implemented in Infrastructure.
- Keep provider-specific EF Core implementation in Infrastructure. Each
  business module owns its context/model, mappings, schema namespace, and
  migrations inside the shared Infrastructure project; the DbContext must not
  be exposed to feature endpoints or another module. See ADR-002 and ADR-006.
- Use explicit command/query use cases. Do not introduce a mediator or CQRS framework unless an ADR shows that it reduces, rather than adds, complexity.

# 6. Angular application structure

    frontend/
      src/
        app/
          core/
            auth/
            http/
            tenant-context/
            configuration/
            error-handling/
            observability/
          layout/
            shell/
            navigation/
            page-header/
          shared/
            ui/
            forms/
            validation/
            tables/
            pipes/
            accessibility/
          features/
            platform-admin/
            identity-access/
            organization/
            master-data/
            business-parties/
            procurement/
            inventory/
            b2b-sales/
            finance/
            reporting/
            audit/
          i18n/
            en/
            ar/
          app.config.ts
          app.routes.ts
        styles/
          tokens/
          themes/
          rtl/
        environments/
      e2e/
        fixtures/
        pages/
        specs/
        api/

Frontend rules:

- Use standalone components and lazy routes by business feature.
- Keep core singletons limited to authentication, tenant context, HTTP, configuration, error handling, and telemetry.
- Shared UI must remain business-neutral. A component specific to a purchase order belongs to Procurement, not Shared.
- Generate or validate TypeScript API contracts from the approved OpenAPI document. Do not maintain two unrelated manual contract definitions.
- Use strict TypeScript, strict Angular templates, typed reactive forms, and explicit null handling.
- Prefer signals and small feature services. Add a state-management library only after an ADR identifies a concrete cross-feature problem.
- Do not place authorization rules in the UI. Route guards and hidden actions improve experience only; the API remains the security boundary.
- Keep the selected tenant/company/branch/warehouse context visible when ambiguity could affect a business action.
- Retail POS routes, components, terminology, and layouts are prohibited in Release 1.

# 7. Database schema strategy

Release 1 uses one SQL Server 2025 database. Schemas reflect module ownership:

| Schema | Owner | Examples |
|---|---|---|
| platform | Platform Administration | tenants, subscriptions, plans, entitlements, quotas |
| identity | Identity and Access | users, memberships, roles, permissions, sessions |
| organization | Organization | companies, legal entities, branches, warehouse identities, organizational relationships, sequences |
| masterdata | Master Data and Catalog | products, items, units, categories, terms, shared reference data |
| parties | Business Parties | generic party identity, names, contacts, addresses, cross-role links |
| procurement | Procurement | supplier purchasing profiles, requests, quotations, purchase orders, confirmations |
| inventory | Inventory | goods receipts, deliveries, supplier/customer returns, stock ledger, balances, reservations, transfers, counts, valuation |
| sales | B2B Sales | customer commercial profiles, quotations, sales orders, fulfillment coordination |
| finance | Finance | fiscal calendars, accounts, journals, purchase/sales invoices and credits, AP, AR, payments/receipts, cash/bank, periods, reconciliation |
| reporting | Reporting and Analytics | report definitions, read models, report jobs, export metadata |
| country_sa | Saudi Country Pack | effective-dated country rules and e-invoice adapter state |
| audit | Security and Audit | immutable business audit events and privileged-access records |
| files | Files | attachment metadata, quarantine state, retention state |
| integration | Integrations | outbox, inbox, webhook delivery, adapter execution |
| jobs | Background processing | durable job records, leases, attempts, failure state |

Database rules:

- Every tenant-owned table has a non-null TenantId.
- CompanyId, BranchId, and WarehouseId are present where the glossary and owning BRD require that scope.
- Unique constraints for tenant-owned business keys include TenantId and the appropriate organizational scope.
- Parent/child relationships use composite tenant-aware constraints where practical so a row cannot reference another tenant's parent.
- Cross-module references use stable identifiers. Cross-schema cascade deletes are prohibited.
- Posted ledgers and audit events are append-only through application rules and restricted database permissions.
- Posted financial and inventory facts use exact decimal types; binary floating-point is prohibited for money, quantity, exchange rate, and tax calculations.
- Mutable drafts use optimistic concurrency, normally SQL Server rowversion.
- Migrations are committed, reviewed, repeatable, and associated with the owning module. Production migrations are an explicit deployment step, not an application-startup side effect.
- Destructive schema changes require an expand/migrate/contract sequence and a rollback or recovery plan.
- Reporting queries must have bounded filters and appropriate indexes. A separate reporting database is deferred until measured load justifies it.
- Raw SQL is exceptional. It must be tenant-scoped, parameterized, tested, and reviewed for module ownership.

# 8. Multi-tenant isolation strategy

Tenant isolation is a system invariant, not a convention.

## Request context

- The authenticated User establishes identity.
- The selected Tenant is validated against active Tenant Membership.
- Company, Branch, and Warehouse selections are validated against both tenant ownership and the User's Access Scope.
- Tenant and organizational scope are constructed server-side. A client-supplied identifier may select among authorized scopes but can never expand access.
- Tenant identifiers are not copied blindly from request bodies into entities.

## Persistence controls

- TenantId is required on all tenant-owned rows.
- EF Core global query filters provide a default read filter.
- SaveChanges interceptors or equivalent persistence guards stamp new rows and reject tenant changes or missing scope.
- Tenant-aware unique constraints and foreign keys prevent cross-tenant relationships.
- Repositories and query services require an explicit tenant context.
- Background jobs, imports, exports, and internal events carry TenantId and re-establish a validated tenant context before accessing data.
- Administrative and support access use separate, time-bound, audited policies; there is no hidden superuser bypass.

## Validation controls

- Every repository/query pattern has positive and negative isolation integration tests.
- API tests attempt identifier substitution across tenants.
- Tests cover UI, API, jobs, files, exports, audit search, report generation, integration retries, and any cache introduced later.
- Production security testing includes horizontal and vertical privilege escalation attempts.

## Database Row-Level Security

SQL Server Row-Level Security is a defense-in-depth option, not assumed silently. Before production, ADR-016 must either:

1. Approve RLS using a carefully controlled SQL session context with connection-pool reset tests; or
2. Defer RLS with documented acceptance of the residual risk and evidence that application filters, constraints, permissions, and isolation tests satisfy the security owner.

# 9. Authentication and authorization strategy

## First-party authentication

- ASP.NET Core Identity owns credentials, password hashing, lockout, security stamps, recovery, and MFA capability.
- The Angular application uses a secure HTTP-only authentication cookie.
- Production cookies are Secure and HttpOnly with a deliberately selected SameSite policy.
- The preferred production topology serves Angular and the API from the same site, reducing CORS and cookie complexity.
- The Angular application never stores access or refresh tokens in localStorage, sessionStorage, IndexedDB, or JavaScript-readable cookies.
- State-changing requests require ASP.NET Core antiforgery validation.
- Session renewal, idle timeout, absolute timeout, concurrent session behavior, privileged-role reauthentication, and MFA enforcement require a security ADR and production policy.

## Authorization

Server authorization combines:

- Active User and session.
- Active Tenant Membership.
- Tenant entitlement for the module or feature.
- Role Permissions.
- Company, Branch, and Warehouse Access Scope.
- Document ownership or assignment where applicable.
- Document status and state transition.
- Amount, approval, separation-of-duties, and other context only after the BRD defines them.

Endpoint policies handle broad access; resource-level authorization handlers validate the specific record and intended action. High-risk actions such as approval, posting, reversal, export, privileged configuration, support access, and tenant purge use explicit policies and audit events.

External partner and machine-to-machine API authentication is not covered by the browser cookie decision. It requires a later ADR, likely using a standards-based confidential-client mechanism. It must not reuse human cookies or introduce browser bearer-token storage.

# 10. API standards

- Base path: /api/v1.
- JSON over HTTPS for Release 1.
- OpenAPI generated from the running API and validated in CI.
- Every public REST operation has Foundation catalogue metadata, a generated
  OpenAPI operationId/summary/description/response set, and a corresponding
  architecture test; placeholder documentation is a Definition-of-Done
  failure.
- Scalar renders the generated `v1` document only in Development/QA and is
  disabled in production; the generated document remains the contract source
  for Angular and API tests.
- Resource names use stable business vocabulary from the glossary.
- Transport models are separate from domain entities and EF entities.
- Errors use Problem Details with a stable business error code, safe message, trace identifier, and field errors where relevant.
- Dates and times use ISO 8601. Authoritative timestamps are stored in UTC; user display applies the approved locale and time zone.
- Monetary amounts, quantities, rates, and tax values are serialized as decimal-compatible values with explicit currency or unit context.
- Commands that may be retried or create authoritative effects accept an idempotency key and enforce a defined operation scope.
- Correlation and trace identifiers flow through API, database work, background jobs, audit events, files, and external adapters.
- Mutable resources use optimistic concurrency with rowversion exposed as an ETag or equivalent version token.
- Collection endpoints have bounded page sizes, stable ordering, authorization-aware filters, and export paths for large results.
- Long-running reports, imports, exports, and integration actions return an accepted job identifier and expose status.
- Breaking changes require a new API version or a documented compatibility period.
- Sensitive internal fields, tenant security details, stack traces, and secrets are never returned.
- CORS is same-origin by default. Additional origins require an explicit allow-list and security review.

# 11. Background processing strategy

Release 1 uses ASP.NET Core hosted services with durable SQL-backed job and outbox tables.

Initial background work includes:

- Email and in-app notification delivery.
- Import validation and commit.
- Large report and export generation.
- File scanning and retention operations.
- Webhook and approved external adapter delivery.
- Saudi e-invoicing submission and retry after MESP-49 is approved.
- Reconciliation and integrity checks.
- Tenant provisioning and controlled offboarding steps.

Required controls:

- Every job is tenant-scoped where applicable.
- Job payloads contain identifiers, not full sensitive documents.
- Handlers are idempotent.
- Work acquisition uses a lease, heartbeat, attempt count, timeout, and safe recovery after process failure.
- Retries use bounded exponential backoff with a terminal failed state and authorized manual retry.
- Outbox records are committed in the same SQL transaction as the business fact they announce.
- Consumer/inbox records prevent duplicate authoritative effects.
- Failures are observable and reconciled; no inventory, accounting, tax, payment, or audit effect may disappear silently.

For the first deployment, the worker may run in the API process. It may later run as a separate process or container built from the same codebase when job duration, isolation, or scaling justifies it. A message broker is deferred.

# 12. File-storage strategy

Private object storage is the production system of record for binary files. SQL Server stores attachment metadata, tenant scope, source document, object key, content type, size, hash, scan state, retention state, uploader, timestamps, and audit links.

Controls:

- Objects are private by default.
- Object keys include a non-guessable identifier and tenant partition; the key itself is never treated as authorization.
- Download requires a server authorization check. If a signed URL is used, it is short-lived and issued only after authorization.
- Uploads use allow-listed size and type rules, content sniffing, filename normalization, cryptographic hash, and malware quarantine.
- A file is not available to ordinary users until its scan policy passes.
- Executable content and unsafe inline rendering are blocked.
- Encryption in transit and at rest is required.
- File metadata and business document links remain immutable after posting where evidence integrity requires it.
- Deletion, retention, legal hold, tenant export, and purge behavior remain blocked on MESP-50 and must be implemented only after approval.

For local development, a filesystem adapter may be used behind the same interface. It is development-only. An object-storage emulator may be included in Docker Compose once the production provider family is selected.

# 13. Audit and observability strategy

Business audit and technical telemetry are separate but correlated.

## Business audit

The audit module stores immutable events for material create, submit, approve, reject, post, reverse, cancel, permission, configuration, export, support-access, integration, and tenant-lifecycle actions.

Each event includes:

- Actor and authentication context.
- Tenant and applicable Company, Branch, and Warehouse scope.
- Action and outcome.
- Target type and stable identifier.
- Source document and document version where relevant.
- UTC timestamp.
- Correlation and trace identifiers.
- Safe before/after values or a change summary.
- Reason, approval evidence, or override evidence where required.

Secrets, password material, complete payment credentials, and unnecessarily sensitive personal data are prohibited in audit payloads.

## Technical observability

- Structured logs use stable event names and safe properties.
- Distributed traces cover HTTP, application use cases, SQL calls, background jobs, object storage, and external adapters.
- Metrics cover latency, failure rate, saturation, tenant/job quotas, queue age, retries, report duration, file scan state, and adapter outcomes.
- Health endpoints distinguish liveness, readiness, and dependency health.
- Every production alert has an owner, threshold, severity, and response runbook.
- Telemetry exporters remain provider-neutral through OpenTelemetry-compatible interfaces.
- Logs and traces must not leak tenant data across operational users or external support boundaries.

# 14. Localization and RTL strategy

Arabic and English are foundational Release 1 capabilities.

- All UI text uses translation keys. Business text is not hard-coded inside components.
- English and Arabic catalogs are version-controlled and reviewed with the glossary.
- The runtime translation approach must support switching language without introducing separate business behavior.
- The root HTML direction changes between ltr and rtl. Components use logical CSS properties rather than hard-coded left/right positioning.
- Navigation, forms, tables, validation summaries, dialogs, charts, icons, and document previews are tested in both directions.
- API errors expose stable codes; Angular translates user-facing messages. Server-generated documents use approved bilingual templates.
- SQL Server stores Unicode text. Search collation and Arabic normalization require an ADR and representative search tests.
- Dates, numbers, decimal separators, units, and currency formats are locale-aware, while stored values remain culture-independent.
- Timestamps are stored in UTC. Saudi display defaults to Asia/Riyadh only where the PRD and Company configuration require it.
- Bilingual document templates, fallback behavior, translation ownership, and terminology approval must be validated in the BRDs and Saudi Country Pack work.

# 15. Testing minimums

No implementation is complete without tests proportionate to its business and isolation risk.

## xUnit unit tests

Minimum coverage areas:

- Domain calculations and invariants.
- State transitions and prohibited transitions.
- Money, quantity, tax, exchange-rate, rounding, and moving-weighted-average behavior after the corresponding BRD decisions are approved.
- Approval and posting rule behavior.
- Permission and resource-authorization decisions.
- Idempotency and duplicate prevention.
- Mapping of module events and posting contracts.

## xUnit integration tests

Integration tests run against Microsoft SQL Server, not an in-memory database, for:

- EF mappings and migrations.
- Tenant query filters, stamps, constraints, and cross-tenant denial.
- Composite organizational relationships.
- Transactions, rowversion concurrency, ledger immutability, and outbox atomicity.
- ASP.NET Core Identity, cookies, antiforgery, lockout, session invalidation, and authorization policies.
- Background job lease, retry, duplicate, and recovery behavior.
- File metadata, authorization, quarantine, and provider adapter behavior.
- Audit completeness and correlation.
- OpenAPI contract and error format.

## Playwright TypeScript

Minimum critical journeys:

- Sign in, sign out, session expiry, denied access, and tenant switching.
- Cross-tenant identifier substitution is denied.
- Platform/Tenant/Company/Branch/Warehouse context is visible and enforced.
- Purchase request to purchase order to supplier confirmation to goods receipt.
- Stock movement, transfer, count, adjustment, and reconciliation evidence.
- B2B quotation/order to delivery to invoice to customer receipt.
- Finance posting, reversal, subledger/GL reconciliation, and period control after BRD approval.
- Attachment upload, scan state, authorized download, and denied download.
- Arabic and English critical flows, RTL layout, keyboard use, and accessibility checks.
- API idempotency, validation errors, concurrency conflict, and long-running job status.

## Release gates

- All approved BRD acceptance scenarios for the release slice are automated or have approved manual evidence.
- Tenant-isolation and authorization negative tests are mandatory and cannot be waived for schedule.
- No known unbalanced journal, unexplained stock difference, duplicate posting, or cross-tenant exposure is releasable.
- Performance, browser, accessibility, backup/restore, recovery, and security suites pass the approved production thresholds.
- Code-coverage percentage is supporting evidence, not a substitute for invariant and risk coverage.

# 16. Local development setup

The preferred daily workflow is:

1. Install the approved .NET 10 SDK, Node.js version required by Angular 22 tooling, Docker Desktop or an equivalent Docker engine, and Git.
2. Start required dependencies with Docker Compose.
3. Apply development migrations explicitly.
4. Run the ASP.NET Core API with hot reload.
5. Run the Angular development server with the API proxy and cookie credentials enabled.
6. Run xUnit and Playwright from documented commands.

Docker Compose should provide:

- Microsoft SQL Server 2025 with a named development volume.
- An optional object-storage emulator after the provider ADR.
- An optional OpenTelemetry collector profile for local trace inspection.
- A full-stack profile only when container-to-container behavior must be tested.

Local controls:

- Development secrets use .NET user secrets or an ignored local environment file.
- No production secret or tenant data is copied into source control.
- Seed data creates synthetic tenants and deliberately similar identifiers for isolation testing.
- HTTPS is used for cookie/security validation.
- Migrations are never applied automatically to production because they were convenient in development.
- A clean checkout must have a documented, repeatable path to a running system and passing smoke test.

# 17. Production deployment options

## Option A - Managed single-region container or application platform

Recommended for Release 1:

- Angular static assets and the API are served from the same site or trusted parent domain.
- One ASP.NET Core application deployment initially hosts the API and worker.
- SQL Server 2025 runs on an approved managed or highly available service/topology.
- Private object storage holds files and generated artifacts.
- A managed edge/reverse proxy terminates TLS and applies standard security headers.
- OpenTelemetry exports to the selected logging, metrics, and tracing platform.
- Scale-out remains possible if session, job lease, and telemetry controls pass multi-instance tests.

This is the preferred balance of low operational burden and production controls.

## Option B - Hardened virtual machine with Docker Compose

Acceptable only for a controlled pilot when managed hosting is unavailable. It requires explicit ownership of patching, TLS, firewalling, backups, restore testing, monitoring, disk capacity, process restart, and high-availability limitations. It is not the preferred general-availability topology.

## Option C - Separate API and worker deployments

Use the same codebase and database, but deploy API and worker independently when long-running jobs or integration retries affect HTTP service quality. This is an evolution of the Modular Monolith, not a move to microservices.

Production topology must preserve same-site cookie behavior where practical. Kubernetes is not required for any Release 1 option.

# 18. Security controls

| Area | Required baseline control |
|---|---|
| Transport | TLS only, HSTS, secure headers, approved ciphers, no mixed content |
| Browser session | Secure HttpOnly cookie, antiforgery, deliberate SameSite, session expiry, security-stamp invalidation |
| Authentication | Identity password policy, lockout, recovery controls, MFA capability, credential event audit |
| Authorization | Deny by default; server policies plus resource, tenant, scope, state, and SoD checks |
| Tenant isolation | Required TenantId, persistence guards, constraints, negative tests, controlled support access |
| Input and API | Typed validation, bounded inputs, parameterized data access, rate limits, safe errors, idempotency |
| Files | Private storage, size/type policy, malware quarantine, safe download, retention control |
| Secrets | External secret store in production, rotation, least privilege, never logged or committed |
| Data | Encryption at rest, encrypted backups, restricted database accounts, restore testing |
| Audit | Immutable material events, correlation, privileged-access evidence, protected retention |
| Application | Dependency scanning, secret scanning, static analysis, security tests, timely patching |
| Operations | Environment separation, least-privilege deployment identity, reviewed production access, incident runbooks |
| Exports and reports | Authorization at generation and download, bounded scope, expiry, audit, no spreadsheet formula injection |

A documented threat model and production security review are mandatory. The architecture does not claim regulatory compliance by technology selection alone.

# 19. Technologies explicitly deferred

The following are outside the Release 1 technology baseline unless a later ADR and approved need justify them:

- Microservices.
- Kubernetes.
- Service mesh.
- Event sourcing.
- Multiple databases per tenant.
- Database-per-tenant deployment.
- Retail POS technology, peripherals, offline store operation, or cashier applications.
- Message broker.
- Distributed cache such as Redis.
- Dedicated search engine.
- GraphQL.
- Separate operational data warehouse or data lake.
- Multi-region active/active deployment.
- Native mobile applications.
- Offline-first synchronization.
- Customer-authored code, per-tenant source branches, or per-tenant schemas.
- Browser-stored bearer tokens for the first-party Angular application.
- Unapproved payment gateway, bank API, identity provider, or e-invoicing provider.

Deferral means "not required now," not "forbidden forever." Introduction requires measured need, ownership, operational capacity, security review, and an approved ADR.

# 20. Architecture risks and mitigations

| Risk | Consequence | Mitigation |
|---|---|---|
| Tenant filter bypass | Critical confidentiality incident | Layered tenant context, filters, save guards, constraints, authorization, and mandatory negative tests |
| Shared database noisy tenant | Other tenants experience degraded service | Bounded queries, indexes, quotas, job concurrency, monitoring, and reference-volume tests |
| Modular boundary erosion | Unmaintainable monolith | Module ownership, internal types, contract-only access, schema ownership, and architecture tests |
| Cookie/CSRF misconfiguration | Unauthorized state-changing requests | Same-site topology, antiforgery validation, CORS allow-list, secure cookie tests |
| Inventory or finance concurrency error | Stock drift or incorrect books | Explicit transactions, rowversion, immutable ledgers, idempotency, reconciliation, invariant tests |
| Duplicate background execution | Duplicate external or ledger effect | Outbox/inbox, idempotent handlers, leases, attempt records, reconciliation |
| Coupled schema migration | Risky deployment and rollback | Module-tagged migrations, expand/migrate/contract, backup and rollback plan |
| Unsafe file upload | Malware or data disclosure | Quarantine, scanning, private objects, content checks, authorized download |
| RTL treated as late styling | Rework and unusable Arabic workflows | Translation keys, logical CSS, RTL tests from foundation |
| Public API reuses browser auth | Weak machine integration security | Separate external-auth ADR; no human cookies for machine clients |
| Single-developer knowledge concentration | Delivery and support fragility | Small architecture, explicit docs, runbooks, automated setup/tests, ADRs, recovery exercises |
| Unvalidated product volumes | Wrong indexes, hosting, job capacity | MESP volume decision, representative load model, performance tests before production |
| Version or licensing mismatch | Unsupported or unexpectedly costly production environment | Validate vendor support, images, licensing, hosting, and upgrade plan before design freeze |
| Saudi compliance assumption | Incorrect tax, privacy, or e-invoice behavior | Qualified Saudi review, versioned country pack, certification evidence, adapter isolation |

## MESP-129 Supplier Return lifecycle coordination constraint

The current MESP-129 Supplier Return physical/commercial lifecycle exclusion
uses a process-local `ConcurrentDictionary<Guid, SemaphoreSlim>` gate and is
valid only while exactly one API process is active. Horizontal API scale-out is
not approved while this process-local gate is the only cross-module lifecycle
coordinator. Before multiple API instances are enabled, the gate must be
replaced or supplemented by durable cross-instance coordination. This
deployment constraint is specific to the current MESP-129 Supplier Return
physical/commercial coordination implementation; it does not state that the
whole ERP can never scale out, and it does not change the existing MESP-128
stock concurrency anchors.

# 21. Architecture decision records required

The following ADRs must exist in docs/Decisions.md or linked files before the affected implementation begins:

| ADR | Decision | Approval or dependency |
|---|---|---|
| ADR-001 | Modular Monolith, module dependency rules, and source-ownership reconciliation | Already aligned to PRD D-001; Hossam and affected domain owners |
| ADR-002 | Backend project structure and module enforcement | Hossam; published in `ADR-002_Backend_Project_Structure_and_Module_Enforcement.md` before MESP-99 |
| ADR-003 | Shared-database tenant isolation controls | Hossam plus Security owner |
| ADR-004 | Identity cookie, antiforgery, session, and MFA policy | Hossam plus Security owner |
| ADR-005 | Policy and resource authorization model | Security owner and business control owners |
| ADR-006 | Module schemas, EF context, migrations, and cross-module transaction rules | Hossam |
| ADR-007 | Internal events, transactional outbox/inbox, and reconciliation | Hossam |
| ADR-008 | SQL-backed job execution and worker deployment | Hossam and Operations owner |
| ADR-009 | Object-storage provider, access pattern, scanning, and retention | Hossam plus Security/Privacy owners; MESP-50 |
| ADR-010 | OpenTelemetry exporter and operational data retention | Hossam and Operations owner |
| ADR-011 | Runtime localization, Arabic search, RTL, and bilingual document generation | Hossam plus Product/Business owners |
| ADR-012 | Production hosting topology, region, availability, RPO, and RTO | Hossam plus Sponsor; MESP-48 and MESP-50 |
| ADR-013 | Secret and encryption-key management | Security/Operations owner |
| ADR-014 | Data residency, retention, legal hold, export, and purge | MESP-50; qualified privacy/legal validation |
| ADR-015 | Saudi e-invoicing adapter and credential boundary | MESP-49; Finance/Compliance approval |
| ADR-016 | SQL Server Row-Level Security adoption or documented deferral | Hossam plus Security owner |
| ADR-017 | External partner/API authentication | Hossam plus Security owner; only when an approved integration needs it |
| ADR-018 | Testing environments, SQL Server test containers, and production-like gates | Hossam and QA owner |

An ADR records the decision, alternatives, rationale, consequences, owner, approval date, status, and superseding ADR. It does not replace the Product Decision Register.

# 22. Jira updates required

MESP-22 is currently the in-progress Product Decision Register task. It should record the approved technology direction as one product-level decision and link to this architecture baseline for implementation detail. No new implementation Story is required.

## Concise MESP-22 update plan

1. Add a comment to MESP-22 linking docs/01_Technology_Architecture_Baseline.md.
2. Allocate the next unused immutable PD-NNN identifier. Do not renumber or edit an earlier decision.
3. Record only the product-level technology decision below. Keep implementation detail in this baseline and its ADRs.
4. Set the accountable owner to Hossam.
5. Link the decision to MESP-19 traceability and to PRD D-001, PLT-001, PLT-008, PLT-010, PLT-014, BR-001, BR-010, BR-011, BR-014, BR-016, RULE-001, RULE-002, RULE-014, RULE-016, and RULE-018.
6. Record the decision as Approved with approval date 1 August 2026.
7. Do not create implementation Stories or move MESP-22 to Done solely because this decision was added. MESP-22 closes only when its full definition of done, named ownership, and traceability are satisfied.

## Copy-ready decision summary for MESP-22

**Decision statement:** Release 1 will use Angular 22 with TypeScript; ASP.NET Core Web API on .NET 10 LTS; Entity Framework Core 10; Microsoft SQL Server 2025; a Modular Monolith; REST and OpenAPI; ASP.NET Core Identity with secure HTTP-only cookie authentication for the first-party Angular application; policy-based server authorization; one shared SQL Server database with strict tenant isolation and module-owned schemas; private object storage; Docker Compose for local development; OpenTelemetry-compatible telemetry; Playwright TypeScript; and xUnit.

**Alternatives rejected for Release 1:** Microservices, Kubernetes, event sourcing, multiple databases per tenant, browser-stored bearer tokens for the first-party Angular application, and unnecessary distributed infrastructure.

**Rationale:** The selected stack satisfies the approved PRD architecture and tenant-isolation direction while minimizing operational burden for one developer. It preserves module ownership and future extraction seams without accepting distributed-system complexity before measured need.

**Affected modules:** All Release 1 modules.

**Owner:** Hossam.

**Status:** Approved on 1 August 2026; record as an immutable decision in MESP-22.

**Detailed baseline:** docs/01_Technology_Architecture_Baseline.md.

---

# Decision timing after baseline approval

The top-level technology stack and Hossam's architecture ownership are approved. The following detailed decisions remain intentionally open until their owning BRD, affected implementation, or production gate:

1. Resolve the provisional module-ownership reconciliation for Organization/Warehouse, Parties/Supplier/Customer, physical inventory documents, and financial documents in the owning BRDs.
2. Select the production hosting provider, Saudi region, network topology, and domain model before production.
3. Select the SQL Server 2025 production service/topology and confirm licensing before production.
4. Select the object-storage provider, storage region, encryption/key ownership, malware scanner, and signed-download approach before production.
5. Approve application-plus-database tenant isolation with or without SQL Server Row-Level Security before production.
6. Approve session timeout, MFA enforcement, privileged reauthentication, and production support-access policy before the affected implementation and validate them before production.
7. Approve the runtime translation approach, Arabic search/collation behavior, and bilingual document-generation method before the affected implementation.
8. Approve whether the worker remains in-process for launch or deploys separately before deployment design is finalized.
9. Select the OpenTelemetry backend and retention/access policy before production.
10. Approve external API authentication only when an approved partner integration requires it.

# Production validation gates

Before production, the team must validate:

- Named PRD and BRD owners, approval evidence, and module BRD gates.
- Current vendor support, patches, container images, browser support, and licensing for the approved versions.
- Reference volumes for tenants, users, products, warehouses, transaction lines, jobs, files, exports, and reports.
- Tenant isolation across API, EF queries, raw SQL, jobs, files, reports, exports, audit search, support access, and any future cache/search.
- Threat model, vulnerability scanning, penetration testing, dependency and secret scanning, and security remediation.
- Cookie, antiforgery, CORS, CSP, HSTS, session invalidation, MFA, and privilege boundaries.
- Load, concurrency, noisy-tenant, long-running job, and report/export behavior against the approved NFRs.
- Backup encryption, point-in-time recovery, full restore, RPO/RTO, disaster recovery, and business-continuity runbooks.
- Data residency, retention, legal hold, export, offboarding, deletion, backup location, subprocessors, and support access under MESP-50.
- Object upload quarantine, malware scanning, content handling, authorized download, retention, and purge.
- Inventory/finance posting invariants, idempotency, reconciliation, reversal, and migration/opening-balance evidence.
- Arabic/English terminology, complete RTL behavior, locale-safe formatting, Arabic search, accessibility, and supported browsers.
- Saudi VAT, PDPL, e-invoicing applicability, ZATCA adapter behavior, credential handling, rejection/retry, evidence, and qualified legal/compliance approval.
- Monitoring dashboards, alerts, audit retrieval, incident response, job recovery, operational ownership, and on-call expectations.

# Source alignment and governance gaps

1. The canonical PRD is `docs/MESP_PRD_v1.2.docx`. References in older Jira comments and documents to `MiniERPSaaSPlatform_PRD_v1.2.docx` or `MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx` are historical labels for the same approved v1.2 baseline; the file contents are unchanged and only the repository path moved.
2. PRD section 5.1 and the glossary's Owning module fields conflict on ownership of Warehouse, Supplier/Business Customer, Goods Receipt/Supplier Return, Delivery/Customer Return, and Fiscal Calendar. Section 4 records a provisional reconciliation; final ownership is resolved in the owning BRDs and recorded in ADR-001/ADR-006 before affected implementation.
3. Hossam is the interim Product Owner, Business Sponsor, Business Analysis Lead, Architecture Owner, QA Lead, and Implementation Lead. Finance, Saudi compliance, privacy, residency, retention, and security conclusions still require the appropriate external specialist validation before production approval.
4. The glossary remains a controlled working baseline and explicitly leaves terms dependent on MESP-41 through MESP-56 open. This architecture does not convert any recommended default into an approved business decision.
5. The PRD requires architecture decisions before affected implementation. `docs/Decisions.md` is the ADR index; full ADR content is created only when the related implementation or production decision becomes due.

# Conformance statement

Subject to the explicit source-ownership reconciliation above, this baseline conforms to the PRD and glossary by preserving the approved hierarchy, Modular Monolith decision, shared relational database posture, strict tenant isolation, Arabic/English and RTL requirement, immutable audit and ledger principles, object storage requirement, and B2B-only Release 1 boundary. It introduces no Retail POS scope and does not resolve any open business decision.
