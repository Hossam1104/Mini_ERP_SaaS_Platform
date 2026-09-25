# Architecture

This document describes the architecture as built on `main` (after the 2026-09-25 cleanup).
When this document and the code disagree, the code is the fact and this document is the defect.
Decisions and their rationale are in [DECISIONS.md](DECISIONS.md). Per-capability design
records (MESP-131 to MESP-135, CI/CD governance, the original technology baseline) are archived
under [history/architecture/](history/architecture/).

## 1. Style

- **Modular monolith.** One repository, one deployable backend, one Angular client. There are
  no microservices, broker, distributed transactions, service mesh, distributed cache or search
  cluster in Release 1 (ADR-001).
- **Business modules** are separated by namespace, persistence ownership, SQL schema and
  architecture tests, not by deployment.
- **One shared SQL Server database** with module-owned schemas and mandatory Tenant isolation.
  Database-per-Tenant is not part of Release 1 (ADR-003, ADR-006).
- **Hierarchy:** `Platform → Tenant → Company / Legal Entity → Branch → Warehouse`. Tenant is
  the security/data-isolation boundary. Tenant is **not** Company (ADR-019).

## 2. Backend projects and dependency rules (ADR-002)

| Project | Owns |
|---|---|
| `MiniErp.Contracts` | Public records, request/response contracts, value objects, module descriptors, cross-module and event contracts. No production-project dependency, no EF Core. |
| `MiniErp.App` | Use cases, business policies, server-side authorization seams, module services. No EF Core, no Infrastructure or Api reference. |
| `MiniErp.Infrastructure` | EF Core, SQL Server/SQLite providers, DbContexts, mappings, persistence, migrations, provider adapters. No Api reference. |
| `MiniErp.Api` | ASP.NET Core host and composition root: endpoints, middleware, authentication, OpenAPI, health, DI. |

```text
MiniErp.Api ──► MiniErp.Infrastructure ──► MiniErp.App ──► MiniErp.Contracts
     └──────────────────────────────────────►┘ (host composition also uses App and Contracts)
```

The graph is acyclic. `InternalsVisibleTo` grants: `MiniErp.Contracts` → `MiniErp.App` and
`MiniErp.ArchitectureTests`; `MiniErp.App` and `MiniErp.Infrastructure` → `MiniErp.ArchitectureTests` only.
`FriendAssemblyPolicyTests` rejects any friend grant to `MiniErp.Api`.

### Responsibility rules

- Endpoint handlers are thin: authenticate/authorize, validate transport input, call App, map the result.
- Business invariants never live in endpoints, Angular components, EF mappings or database triggers.
- DbContexts are never exposed to endpoints. No module touches another module's DbContext or tables.
- Explicit application commands/queries; no mandatory mediator/CQRS framework.
- EF migrations are additive. Accepted historical migrations are never edited.
- Money, quantity, tax, rate and valuation values use exact decimals. High-risk writes use the
  established optimistic-concurrency (`rowversion`), idempotency and Serializable-transaction patterns.
- Historical economic/approval evidence is replayed as recorded, never re-resolved from mutable
  current configuration.

## 3. Modules and ownership

App modules live in `backend/src/MiniErp.App/Modules/<Module>`; persisted modules have one internal
DbContext each in `backend/src/MiniErp.Infrastructure/Persistence/Modules/<Module>` (7 module contexts
plus `TenantPersistenceDbContext`, which physically owns `tenancy.TenantOwnedRecords`).

| Module | Owns | Does not own |
|---|---|---|
| Platform | Platform administration, Tenant lifecycle (M27 activation) | Tenant ERP business data |
| Identity | Authentication/session, membership, permissions, server-side Tenant/organization context, host-aware entry | — |
| MasterData | Category, UOM, Product, Price List, Tax, Currency, Exchange Rate, Payment Term, import | Party identity |
| BusinessParties | Supplier and Business Customer identity | Purchasing/commercial process roles |
| Procurement | PR, Supplier Quotation/comparison, PO, Supplier Confirmation, Goods Receipt evidence, Purchase Invoice handoff/matching, Supplier Return | Stock truth, accounting truth |
| Inventory | Stock movement and ledger, stock control, moving-weighted-average valuation, `inventory-valuation-finance.v1` handoff | Journals |
| Finance | Books, COA, periods, Journals/GL, Posting Rules, AP, AR, Cash/Bank, settlement, FX/revaluation, close, reconciliation | Source documents of other modules |
| Sales | B2B quotation/order/credit, reservation, fulfillment/delivery, invoice eligibility, Customer Return | Stock and accounting effects |
| Reporting | Read models, reports, exports | Any transactional ledger |
| Migration | Intake, validation, quarantine, dry-run, execution orchestration, opening data, reconciliation, approval, Ready-for-Handover evidence (MESP-141) | Finance/Inventory algorithms, Master Data truth, Tenant activation |
| Audit | Immutable, reconstructable evidence of material actions | — |
| Notifications | Cross-cutting delivery | Business authority |

Cross-module rules: consume another module's truth through its contracts or persisted source
evidence; reference other modules by stable identifiers; no cross-schema cascade deletes; App-level
coordination never bypasses Tenant checks, authorization or persistence ownership.

Migration specifics: foreign-currency opening applies to AR, AP and Cash/Bank (`RateDate = OpeningDate`);
Inventory and residual GL openings are functional-currency only; the execution fingerprint is
`migration-economic-execution-v2`; Ready-for-Handover never activates a Tenant.

## 4. Tenant isolation and security

- Tenant is resolved and authorized server-side. A hostname is a **candidate** only
  (`Host → candidate Tenant → authentication → exact membership → server-owned context → Overview`).
- Client-supplied identifiers may select among already-authorized scopes; they never widen scope.
  Frontend guards and hidden controls are not authorization.
- Tenant-owned rows carry immutable Tenant ownership; EF global query filters and stored-owner
  guards enforce it. Unscoped EF calls are restricted to the allowlist in §7.
- First-party browser auth is the `__Host-MiniErp.Auth` secure HTTP-only cookie locating a
  server-side session, plus framework antiforgery for unsafe requests (ADR-004). No bearer tokens
  in browser storage.
- Tenant branding and the SAR symbol are configuration/presentation only (ADR-019).

## 5. API and localization

- REST/JSON under `/api/v1`. Every public operation is in the Foundation operation catalogue,
  mapped, present in generated OpenAPI with a stable `operationId`, and contract-tested
  (REST/API Definition of Done in [AGENTS.md](../AGENTS.md)). Scalar renders the document in Development/QA only.
- Transport records are separate from EF entities. Retryable authoritative commands use idempotency.
- Arabic and English are Release 1 requirements; RTL/LTR are presentation modes of the same behavior.

## 6. Folder structure

```text
backend/
  MiniErp.sln, global.json, Directory.Build.props, Directory.Packages.props
  src/MiniErp.{Api,App,Contracts,Infrastructure}/     four production projects (§2)
  tests/MiniErp.ArchitectureTests/                    all backend tests (architecture, unit, REST, SQLite, LocalDB safety)
  tools/MiniErp.DevelopmentDataCutover/               Development-only data tool; never a production migration path
frontend/
  src/app/core/                                       API clients, auth, i18n, cross-cutting services
  src/app/features/<area>/                            auth, context, finance, inventory, master-data, procurement, reporting, sales, shell, workspace
  src/app/shared/                                     shared UI primitives
  e2e/                                                Playwright specs (mocked API fixtures)
  assets/                                             Owner-managed source assets — never modified by agents
scripts/                                              PowerShell launch and validation runners (see RUN.md)
docs/                                                 PROJECT, ARCHITECTURE, ROADMAP, DECISIONS, MODEL_ROUTING
  requirements/                                       approved BRDs and the business glossary
  audit/                                              2026-09 cleanup audit and reconciliation
  history/                                            verbatim archives (not current authority)
  assets/                                             PRD .docx, presentation .pptx, wireframes
.github/workflows/ci.yml                              hosted CI
```

## 7. Enforcement

All rules are xUnit tests in `backend/tests/MiniErp.ArchitectureTests`. They run in the local gate and in hosted CI.

| ID | Rule | Test (in `ModuleBoundaryTests.cs` unless noted) |
|---|---|---|
| R1 | Project graph follows §2; no cycles; no EF Core in App or Api | `Project_reference_direction_matches_adr_002`, `Known_project_dependency_graph_has_no_cycle`, `Application_and_api_do_not_reference_entity_framework_core` |
| R2 | App cross-module imports only along the frozen edge allowlist (shrink-only) | `App_module_edges_match_the_frozen_shrink_only_allowlist` |
| R3 | A module's persistence never references another module's persistence (composition files excepted) | `Module_persistence_does_not_reference_another_modules_persistence` |
| R4 | Unscoped EF calls only at 4 named sites; every allow-listed `ExecuteSql*` is a Tenant-predicated `UPDLOCK` read | `AssertApprovedUnscopedCalls` users, `Allow_listed_raw_sql_is_tenant_scoped_and_lock_only` |
| R5 | Every `[Collection(SqlServerSafetyCollection.Name)]` class name ends in `SqlServerSafetyTests` (hosted CI excludes them by that suffix) | `Sql_server_safety_collection_classes_keep_the_hosted_ci_exclusion_suffix` |
| R6 | `AGENTS.md` stays at the repository root (tests locate the root through it) | `MigrationFoundationTests.FindRepositoryRoot` |

### Ratcheted exceptions (may only shrink)

**R2 — 25 App module edges** (importer → imported). Three pairs are two-way and frozen, not scheduled for removal:
Finance↔Sales, Inventory↔Sales, MasterData↔BusinessParties.

```text
BusinessParties→MasterData   Finance→Identity      Finance→Inventory     Finance→Sales
Inventory→MasterData         Inventory→Procurement Inventory→Sales       MasterData→Audit
MasterData→BusinessParties   Migration→Audit       Migration→Finance     Migration→Inventory
Notifications→Audit          Procurement→BusinessParties                 Procurement→MasterData
Reporting→Audit              Reporting→Finance     Reporting→Inventory   Reporting→Procurement
Reporting→Sales              Sales→BusinessParties Sales→Finance         Sales→Inventory
Sales→MasterData             Sales→Procurement
```

**R4 — 4 unscoped sites:** `IgnoreQueryFilters` in `Persistence/TenantOwnershipStoreVerifier.cs` (1);
`ExecuteSqlInterpolatedAsync` row locks in `Persistence/Modules/Migration/MigrationPersistence.cs` (1)
and `MigrationReconciliationPersistence.cs` (2).

**Exception policy.** Each exception is named and justified. Adding one needs an owner-approved task
that cites this section. An executor never widens a list to make a build green; when code stops
needing an entry, the entry is removed in the same change.

## 8. Known patterns and watch list

- **Fail-closed catch pattern.** About 227 `catch { return await FailedAsync(…) }` blocks in
  `backend/src` turn any fault (including cancellation) into an audited failure. Consistent and
  fail-closed; not mass-refactored. New code catches `Exception ex when (ex is not OperationCanceledException)`,
  as the Migration reconciliation reads now do (D-18).
- **Complexity watch list** (size alone does not justify a split; pin behavior with tests first):
  `App/Modules/Identity/IdentityAuthorizationService.cs` (~2,489 lines),
  `frontend/src/app/core/i18n/language.service.ts` (~1,924),
  `App/Modules/MasterData/MasterDataImportProcessors.cs` (~1,786),
  `MiniErp.Api/Program.cs` (~1,361), `MigrationReconciliationService.cs` (~828),
  `MigrationExecutionService` (do not add new concerns; add a cohesive collaborator instead).
- **Frontend budget.** Initial bundle ~514 kB versus the 500 kB budget: accepted debt, warning retained, budget not raised.

## 9. Stack

| Area | Version |
|---|---|
| .NET SDK / TFM / C# | `10.0.400` / `net10.0` / 14.0; nullable on, warnings as errors, analyzers `latest` |
| EF Core (SQL Server, SQLite) | `10.0.10` |
| OpenAPI / Scalar | `Microsoft.AspNetCore.OpenApi 10.0.10`, `Microsoft.OpenApi 2.7.5`, `Scalar.AspNetCore 2.16.16` |
| Database | SQL Server 2025 baseline; LocalDB `MSSQLLocalDB` for disposable safety tests |
| Angular / TypeScript / RxJS | `22.1.x` / `~6.0.2` / `~7.8.0` |
| Node / npm (CI) | `24.18.0` / `12.0.1` |
| Tests | xUnit `2.9.2`, Test SDK `17.12.0`, Vitest `^4.0.8`, jsdom `^28.0.0`, Playwright `^1.62.1` |

There is no separate lint script: `TreatWarningsAsErrors` and the .NET analyzers are the backend lint;
`ng build` is the frontend type-check.

## 10. Environments and CI/CD

- **Local development.** `MESP_SQLSERVER_CONNECTION_STRING` (persistent runtime database, supplied
  externally). Development may apply formal module migrations; production startup never auto-migrates.
- **SQL safety.** `MESP_SQLSERVER_SAFETY_CONNECTION_STRING` is generated transiently by the runners and
  must target `(localdb)\MSSQLLocalDB` / `MiniErpFoundation_*`. It is never interchangeable with the runtime variable.
- **Hosted CI (kept by owner decision).** `.github/workflows/ci.yml` on PRs to `main`, pushes to `main`
  and manual dispatch. Required checks: `Repository Validation` (`git diff --check`), `Backend`
  (Windows; Release build; tests with `FullyQualifiedName!~SqlServerSafetyTests`), `Frontend` (Ubuntu;
  unit tests, production build, Chromium Playwright with mocked fixtures, both npm audits).
  Ruleset `22905800` requires a PR into `main`.
- **CD / production deployment:** not implemented. No persistent QA or Staging environment. Production
  topology, secrets, retention/residency, RLS and e-invoicing are open decisions (ADR-012 to ADR-016)
  gated by MESP-48 and MESP-50.
