# ARCHITECTURE.md

## 1. Document Status and Scope

[CONFIRMED] This document describes the architecture of the `Mini_ERP_SaaS_Platform` as established by the accepted repository architecture records and the project state through merged `main` commit `13ede0af8234c3dc1d58272532f27258f1c87ed6`.

[CONFIRMED] The product is a Release 1 B2B ERP SaaS platform for small and medium businesses, with Wafra as the first production/validation Tenant only.

[CONFIRMED] No workflow, schema, accounting rule, permission model, report, migration behavior, or reusable UX behavior may be hard-coded for Wafra.

[CONFIRMED] The organizational hierarchy is:

`Platform → Tenant → Company / Legal Entity → Branch → Warehouse`

[CONFIRMED] `Tenant` and `Company / Legal Entity` are different concepts and must not be treated as interchangeable.

[CONFIRMED] Release 1 is B2B ERP only.

[CONFIRMED] Retail POS, cashier operations, cash drawers, retail shifts, and retail checkout are outside the Mini ERP Release 1 architecture.

---

# 2. Architectural Style

[CONFIRMED] The server architecture is a **Modular Monolith**.

[CONFIRMED] The product uses one repository and one primary application/deployment boundary rather than a microservice-per-module architecture.

[CONFIRMED] Business modules are explicitly separated by application contracts, namespaces, persistence ownership, schemas, and architecture tests.

[CONFIRMED] One module must not directly update another module's owned database tables.

[CONFIRMED] Cross-module interaction is performed through approved application/public contracts, source evidence, durable internal evidence, or explicitly defined handoff contracts.

[CONFIRMED] The approved Release 1 database shape is one shared SQL Server database with module-owned schemas and mandatory Tenant isolation.

[CONFIRMED] A database-per-Tenant architecture is not part of the Release 1 baseline.

[CONFIRMED] Kubernetes, service mesh, a distributed transaction coordinator, a mandatory message broker, a distributed cache, and a search cluster are not part of the initial Release 1 architecture.

---

# 3. Backend Project Layers

## 3.1 Four-Project Production Topology

[CONFIRMED] ADR-002 establishes four backend production projects:

| Project | Responsibility |
|---|---|
| `MiniErp.Contracts` | [CONFIRMED] Stable public records, request/response contracts, public value objects, module descriptors, cross-module contracts, and event contracts. |
| `MiniErp.App` | [CONFIRMED] Application behavior, use-case orchestration, server-authoritative authorization seams, business policies, module services, and module-internal behavior. |
| `MiniErp.Infrastructure` | [CONFIRMED] EF Core, SQL Server/SQLite providers, DbContexts, mappings, repositories/persistence implementations, provider adapters, migrations, and persistence composition. |
| `MiniErp.Api` | [CONFIRMED] ASP.NET Core host/composition root, HTTP endpoints, middleware, authentication integration, OpenAPI composition, health endpoints, and dependency registration. |

## 3.2 Allowed Project Dependencies

[CONFIRMED] The approved dependency direction is:

```text
MiniErp.Api
   ├──> MiniErp.Infrastructure
   ├──> MiniErp.App
   └──> MiniErp.Contracts

MiniErp.Infrastructure
   ├──> MiniErp.App
   └──> MiniErp.Contracts

MiniErp.App
   └──> MiniErp.Contracts

MiniErp.Contracts
   └──> no production-project dependency
```

[CONFIRMED] `MiniErp.Contracts` must not depend on `MiniErp.App`, `MiniErp.Infrastructure`, or `MiniErp.Api`.

[CONFIRMED] `MiniErp.Contracts` must not contain EF Core or provider-specific persistence implementation.

[CONFIRMED] `MiniErp.App` must not depend on `MiniErp.Infrastructure` or `MiniErp.Api`.

[CONFIRMED] `MiniErp.App` must not directly reference EF Core.

[CONFIRMED] `MiniErp.Infrastructure` may depend on App and Contracts but must not depend on Api.

[CONFIRMED] The dependency graph must remain acyclic.

[CONFIRMED] Architecture tests enforce the project-reference graph and forbidden reverse dependencies.

---

# 4. Backend Responsibility Rules

[CONFIRMED] HTTP endpoint handlers are thin transport/composition boundaries.

[CONFIRMED] Endpoint handlers authenticate/authorize the call, validate transport-level inputs, call application behavior, and map results to HTTP responses.

[CONFIRMED] Business invariants do not belong in controllers/endpoints, Angular components, EF mapping configurations, or database triggers.

[CONFIRMED] Application behavior belongs in `MiniErp.App`.

[CONFIRMED] Provider-specific persistence belongs in `MiniErp.Infrastructure`.

[CONFIRMED] Public transport/domain-boundary contracts belong in `MiniErp.Contracts`.

[CONFIRMED] DbContexts must not be exposed directly to feature endpoints.

[CONFIRMED] One business module must not access another module's DbContext or tables directly.

[CONFIRMED] The architecture uses explicit application commands/queries/use cases rather than a mandatory mediator/CQRS framework.

---

# 5. Business Modules and Ownership

## 5.1 Platform and Tenant Lifecycle

[CONFIRMED] Platform owns platform-administration concepts and reusable SaaS administration boundaries.

[CONFIRMED] Migration completion or Ready-for-Handover evidence must not activate a Tenant.

[CONFIRMED] Actual Tenant activation is owned by M27 Platform/Tenant lifecycle capability.

## 5.2 Identity and Access

[CONFIRMED] Identity owns authentication/session integration, membership authority, permissions, server-side authorization context, and entry-routing/security behavior.

[CONFIRMED] Tenant and organization context is established and validated server-side.

[CONFIRMED] Client-supplied identifiers may select among already authorized scopes but may not expand scope.

[CONFIRMED] Hostname resolution is routing information, not authorization.

[CONFIRMED] Frontend guards and hidden controls are not authorization authority.

## 5.3 Master Data

[CONFIRMED] Master Data owns reusable business/reference data including implemented Category/UOM, Product identity/catalog data, Price Lists, Tax, Currency, Exchange Rates, Payment Terms, and import/reference behavior.

[CONFIRMED] Migration must not duplicate Master Data truth.

## 5.4 Business Parties

[CONFIRMED] Business Parties owns Supplier and Business Customer identity/master records.

[CONFIRMED] Procurement owns the Supplier purchasing-process role rather than generic Supplier identity.

[CONFIRMED] Sales owns the Customer commercial-process role rather than generic Customer identity.

## 5.5 Procurement

[CONFIRMED] Procurement owns purchasing-process evidence including Purchase Request, Supplier Quotation/comparison, Purchase Order, Supplier Confirmation, Goods Receipt process evidence, Purchase Invoice handoff/matching, and Supplier Return behavior implemented under that boundary.

[CONFIRMED] Procurement does not own Inventory stock truth or Finance accounting truth.

## 5.6 Inventory

[CONFIRMED] Inventory owns physical stock movement and stock-ledger truth.

[CONFIRMED] Organization owns the Warehouse's place in the hierarchy; Inventory owns stock execution at the Warehouse.

[CONFIRMED] Inventory owns moving-weighted-average operational valuation.

[CONFIRMED] Inventory emits Finance handoff facts rather than creating Finance journals.

[CONFIRMED] The Finance valuation handoff contract is `inventory-valuation-finance.v1`.

## 5.7 Finance

[CONFIRMED] Finance owns Company accounting books, functional currency, COA, fiscal calendar/periods, Journals, GL facts, Posting Rules, AP, AR, Cash/Bank accounting, settlement/allocation, FX, revaluation, and Finance reconciliation.

[CONFIRMED] Finance owns historical applied FX/monetary evidence.

[CONFIRMED] Inventory and Migration must not reproduce Finance posting algorithms.

[CONFIRMED] Posted financial history is immutable; corrections are forward evidence such as reversal/successor behavior rather than silent rewrite.

## 5.8 B2B Sales

[CONFIRMED] Sales owns B2B commercial Sales behavior.

[CONFIRMED] Physical Inventory effects remain Inventory-owned and accounting/AR/GL effects remain Finance-owned.

## 5.9 Reporting

[CONFIRMED] Reporting owns report/query/read-model/export behavior within its domain.

[CONFIRMED] Reporting must not become an alternative transactional ledger.

## 5.10 Migration and Tenant Onboarding

[CONFIRMED] Migration owns migration intake, validation, quarantine, dry-run/preview, execution orchestration, migration lifecycle evidence, opening-data orchestration, and migration-specific reconciliation/handover evidence.

[CONFIRMED] Migration does not own Finance algorithms, Inventory valuation, Master Data truth, or Tenant activation.

[CONFIRMED] Foreign-currency opening support currently applies to AR, AP, and Cash/Bank opening.

[CONFIRMED] Inventory opening and residual GL opening remain functional-currency-only.

[CONFIRMED] `RateDate = OpeningDate` for the implemented foreign opening contract.

[CONFIRMED] Migration execution fingerprint remains `migration-economic-execution-v2`.

[CONFIRMED] Reporting Currency is evidence/presentation and not a second ledger.

[CONFIRMED] Slice 11 — Migration Reconciliation, Approval and Ready-for-Handover — is activated, but at the last verified state no Slice 11 implementation was committed.

## 5.11 Audit and Notifications

[CONFIRMED] Audit preserves immutable/reconstructable evidence for material business actions under the established audit boundary.

[CONFIRMED] Notifications is cross-cutting delivery behavior and does not become source-domain business authority.

---

# 6. Cross-Module Rules

[CONFIRMED] Modules consume another module's truth through explicit contracts or persisted source evidence.

[CONFIRMED] Direct cross-module DbContext/table access is prohibited.

[CONFIRMED] Cross-module database references use stable identifiers.

[CONFIRMED] Cross-schema cascade deletes are prohibited.

[CONFIRMED] Application-layer coordination may span modules but does not permit bypassing Tenant checks, authorization, or persistence ownership.

[CONFIRMED] Exact historical owner evidence is used where later replay/reconciliation must reproduce the original result rather than re-resolve mutable current configuration.

---

# 7. Database Architecture

[CONFIRMED] Release 1 uses one shared Microsoft SQL Server database.

[CONFIRMED] Business modules own separate EF Core models/DbContexts and schema namespaces inside `MiniErp.Infrastructure`.

[CONFIRMED] Persisted module contexts include Tenancy/Foundation, Master Data, Business Parties, Procurement, Inventory, Finance, Sales, and Migration.

[CONFIRMED] Shared physical tables have one owner; `tenancy.TenantOwnedRecords` is physically owned by Tenancy.

[CONFIRMED] Tenant-owned rows carry immutable Tenant ownership.

[CONFIRMED] Tenant-owned uniqueness includes Tenant and applicable organization scope where required.

[CONFIRMED] EF global query filters and stored-owner guards are part of Tenant isolation.

[CONFIRMED] `IgnoreQueryFilters`, unbounded raw SQL, bulk paths, and maintenance bypasses are restricted to explicit privileged boundaries.

[CONFIRMED] Exact decimal types are required for money, quantity, tax, Exchange Rates, and accounting/valuation values.

[CONFIRMED] Mutable records use optimistic concurrency where required; SQL Server `rowversion` or equivalent version evidence is an established pattern.

[CONFIRMED] Serializable transactions are used in high-risk race-sensitive persistence paths.

---

# 8. Folder Structure

[CONFIRMED] Current backend structure:

```text
backend/
├─ MiniErp.sln
├─ global.json
├─ Directory.Build.props
├─ Directory.Packages.props
├─ src/
│  ├─ MiniErp.Api/
│  ├─ MiniErp.App/
│  ├─ MiniErp.Contracts/
│  └─ MiniErp.Infrastructure/
├─ tests/
│  └─ MiniErp.ArchitectureTests/
└─ tools/
   └─ MiniErp.DevelopmentDataCutover/
```

[CONFIRMED] Current App module paths include Audit, BusinessParties, Finance, Identity, Inventory, MasterData, Migration, Notifications, Platform, Procurement, Reporting, and Sales.

[CONFIRMED] Infrastructure provider/persistence module folders include BusinessParties, Finance, Inventory, MasterData, Migration, Procurement, and Sales.

[CONFIRMED] Migration snapshots/migrations are under `Persistence/Migrations/Mesp141`.

[CONFIRMED] The current frontend is feature-oriented under `frontend/src/app/features/...` with cross-cutting API/core code under `frontend/src/app/core/...`.

---

# 9. Technology Stack and Versions

| Area | Technology / Version |
|---|---|
| Server SDK | [CONFIRMED] .NET SDK `10.0.400`. |
| Target framework | [CONFIRMED] `net10.0`. |
| C# | [CONFIRMED] `14.0`. |
| Compiler policy | [CONFIRMED] Nullable enabled, implicit usings enabled, warnings treated as errors. |
| API | [CONFIRMED] ASP.NET Core Web API. |
| OpenAPI | [CONFIRMED] `Microsoft.AspNetCore.OpenApi 10.0.10`; `Microsoft.OpenApi 2.7.5`; `Scalar.AspNetCore 2.16.16`. |
| ORM | [CONFIRMED] Entity Framework Core `10.0.10`. |
| SQL Server EF provider | [CONFIRMED] `10.0.10`. |
| SQLite EF provider | [CONFIRMED] `10.0.10`. |
| DB baseline | [CONFIRMED] Microsoft SQL Server 2025. |
| Local SQL safety | [CONFIRMED] SQL Server LocalDB `MSSQLLocalDB`. |
| Frontend | [CONFIRMED] Angular `22.1.x`. |
| TypeScript | [CONFIRMED] `~6.0.2`. |
| RxJS | [CONFIRMED] `~7.8.0`. |
| npm | [CONFIRMED] `12.0.1`. |
| Node CI | [CONFIRMED] `24.18.0`; no repository `engines` pin exists. |
| Playwright | [CONFIRMED] `^1.62.1`. |
| Vitest | [CONFIRMED] `^4.0.8`. |
| jsdom | [CONFIRMED] `^28.0.0`. |
| xUnit | [CONFIRMED] `2.9.2`. |
| .NET Test SDK | [CONFIRMED] `17.12.0`. |

[CONFIRMED] No authoritative npm lint script exists.

[CONFIRMED] No separate authoritative npm type-check script exists; Angular production compilation performs configured compilation checks.

---

# 10. API, Authentication and Localization

[CONFIRMED] Release 1 public API style is REST/JSON under `/api/v1`.

[CONFIRMED] Public operations use generated OpenAPI with stable operation catalogue metadata.

[CONFIRMED] Transport records are separate from EF entities.

[CONFIRMED] Retryable authoritative commands use idempotency where their contract requires it.

[CONFIRMED] Secure HTTP-only cookie authentication with server-side session authority is the first-party browser architecture.

[CONFIRMED] Browser bearer tokens are not stored in `localStorage`, `sessionStorage`, IndexedDB, or JS-readable cookies.

[CONFIRMED] Arabic and English are Release 1 requirements.

[CONFIRMED] RTL/LTR are presentation modes of the same business implementation, not separate business logic.

---

# 11. Test Data Strategy

[CONFIRMED] Backend tests are currently consolidated in `backend/tests/MiniErp.ArchitectureTests`.

[CONFIRMED] That test project contains architecture, unit-style, application, REST/host, SQLite/provider-neutral, and SQL Server safety/provider tests.

[CONFIRMED] SQL Server-specific tests use disposable LocalDB databases whose names match `MiniErpFoundation_*`.

[CONFIRMED] The SQL safety fixture fails closed for unsafe/non-LocalDB targets and never falls back to the persistent Development database.

[CONFIRMED] Test identifiers and Tenant/business data are synthetic.

[CONFIRMED] Multiple synthetic Tenants and deliberately similar identifiers are used for isolation tests.

[CONFIRMED] Migration/Finance/Inventory provider tests use real provider paths where persistence/economic truth matters.

[CONFIRMED] Hosted Playwright uses mocked browser/API fixtures and does not connect to a backend/Tenant DB/Production/external provider.

[CONFIRMED] Real local browser/API acceptance runs have also been used separately from hosted Playwright.

---

# 12. Environments

## 12.1 Local Development

[CONFIRMED] Persistent local SQL Server Development configuration is supplied externally by environment configuration; no connection-string value is recorded here.

[CONFIRMED] The runtime configuration key is `MESP_SQLSERVER_CONNECTION_STRING`.

[CONFIRMED] Development startup can use formal module migrations when explicitly configured.

[CONFIRMED] Production startup must not automatically apply Development migrations.

[CONFIRMED] `backend/tools/MiniErp.DevelopmentDataCutover` is Development-only and is not a production migration mechanism.

## 12.2 SQL Safety Environment

[CONFIRMED] Destructive/provider-safety tests use `MESP_SQLSERVER_SAFETY_CONNECTION_STRING`, generated/supplied transiently by approved scripts.

[CONFIRMED] It must target `(localdb)\MSSQLLocalDB` and a disposable `MiniErpFoundation_*` database.

[CONFIRMED] Persistent runtime and disposable safety connections are non-interchangeable.

## 12.3 Hosted CI

[CONFIRMED] GitHub Actions is active/verified CI.

[CONFIRMED] Repository Validation runs on Ubuntu; Backend on Windows; Frontend on Ubuntu.

[CONFIRMED] Hosted Backend excludes the disposable LocalDB SQL safety subset.

[CONFIRMED] CI contains no Production deployment step or Production credentials.

## 12.4 QA / Staging / Production

[UNVERIFIED] A separately deployed persistent QA environment is not established by the current repository evidence reviewed for this handover.

[CONFIRMED] Automated Staging promotion is not implemented.

[CONFIRMED] Continuous deployment and Production deployment are not implemented.

[CONFIRMED] MESP-48 and MESP-50 remain production-governance gates.

---

# 13. CI/CD Pipeline Stages

[CONFIRMED] `.github/workflows/ci.yml` triggers on PRs to `main`, pushes to `main`, and manual dispatch.

[CONFIRMED] Stable required check identities are `Repository Validation`, `Backend`, and `Frontend`.

## Repository Validation

[CONFIRMED] Checkout with history and `git diff --check` against the relevant delta.

## Backend

[CONFIRMED] Windows runner.

[CONFIRMED] Install .NET `10.0.400`.

[CONFIRMED] Restore NuGet, Release-build `backend/MiniErp.sln`, and run hosted-compatible backend tests excluding `SqlServerSafetyTests`.

[CONFIRMED] Upload TRX test results for seven days when present.

## Frontend

[CONFIRMED] Ubuntu runner.

[CONFIRMED] Install Node `24.18.0`, npm `12.0.1`, `npm ci`, Angular unit tests, production build, Chromium Playwright, production dependency audit and full dependency audit.

[CONFIRMED] Upload browser diagnostics for seven days when present.

## CD

[CONFIRMED] `CD = NOT IMPLEMENTED`.

[CONFIRMED] `Production deployment = NOT IMPLEMENTED`.

[CONFIRMED] CI executes no Production database migrations.

---

# 14. Critical Constraints

[CONFIRMED] Preserve four-project dependency direction unless explicitly superseded.

[CONFIRMED] Preserve module-owned persistence and prohibit direct cross-module DbContext access.

[CONFIRMED] Preserve one shared SQL Server Release 1 database and explicit Tenant ownership.

[CONFIRMED] Preserve `Platform → Tenant → Company → Branch → Warehouse`; Tenant is not Company.

[CONFIRMED] Do not hard-code Wafra.

[CONFIRMED] Finance remains authoritative for accounting, monetary evidence, FX, rounding, mapping, and posting.

[CONFIRMED] Inventory remains authoritative for stock and valuation.

[CONFIRMED] Master Data remains authoritative for reference/master records.

[CONFIRMED] Migration remains orchestrator/evidence owner rather than a duplicate owner-domain engine.

[CONFIRMED] M27 Tenant activation remains separate from Migration Ready-for-Handover.

[CONFIRMED] Production migration/deployment remains separate from Development convenience automation.

[CONFIRMED] Destructive SQL tests remain isolated to disposable LocalDB.

[CONFIRMED] Historical economic/approval evidence must not be silently reinterpreted using mutable current configuration.

[CONFIRMED] Unresolved production gates are not implementation defaults.

[CONFIRMED] No credentials, connection-string values, tokens, passwords, API keys, or secrets belong in repository handover documents.
