# Decisions

This file records the decisions that govern the project. Each is recorded once, with its reason.

- **Owner decisions** carry the date the owner (Hossam) made them.
- **Planner decisions** are the choices Opus 5.5 made within the owner's instructions.
- **Architecture decisions (ADRs)** are indexed below, and the eight ADRs with full records are
  embedded verbatim at the end.

Earlier history is archived and has no authority:
- the immutable Product Decision Register (PD-001..PD-046) and the old decision log, in
  [`history/Decisions_register_to_2026-09-25.md`](history/Decisions_register_to_2026-09-25.md);
- the 2026-09-24 handover, in [`history/handover-2026-09-24/`](history/handover-2026-09-24/).

Business decisions (BR/PD rows and the BRDs' open decisions) live with their BRDs in
[`requirements/`](requirements/). Open business questions go to MESP-23 (#112).

## 1. Owner decisions

### 1.1 Operating model: 2026-09-24

| # | Decision | Consequence |
|---|---|---|
| Q1 | Opus 5.5 is Planner/Architect. Luna 6 (xhigh) is the default executor. Sol 6 reviews at critical points only. Sonnet 5 fixes bugs. **GPT-5.6 Terra is retired.** | [`MODEL_ROUTING.md`](MODEL_ROUTING.md) §1–§2. |
| Q2 | **Opus 5.5 replaces Sol as acceptance authority.** | `AGENTS.md` § Executor authorization. |
| Q3 | Keep the CI pipeline and ruleset `22905800` until the app is published to a server. CD waits. | `.github/workflows/ci.yml` is unchanged. |
| Q4 | Handoff files: single-prompt `TASK.md`, the `RESULT.md` log, and a routing file. | The old TASK.md is archived verbatim in `history/`. |
| Q6 | References use the form `MESP-<n> (#<issue>)`. | `AGENTS.md` and `MODEL_ROUTING.md` §10. |
| Q7 | There is no prompt cap per Opus conversation. | `MODEL_ROUTING.md` §6. |
| Q8 | Fix the stale governance pointers. | Replaced by the new core docs. |
| Q9 | Retire the obsolete local script. | Its local exclude line was removed. |
| Q10 | ~~Commit via a PR to `main`~~. **Superseded** by Q-A. | — |

### 1.2 Cleanup: 2026-09-25

| # | Decision | Consequence |
|---|---|---|
| Q-A | Deliver the cleanup as one short-lived branch plus one PR. **The owner** merges it with a merge commit via the admin bypass. | This is a one-time exception to "no new branches/PRs". Ruleset `22905800` is unchanged. |
| Q-B | The acceptance review of MESP-141 Slice 11 is the first task after the cleanup. | MESP-150 (#265). |
| Q-C | Restore `Saudi_Riyal.svg` and `wafra-logo.jpeg` byte-for-byte into `frontend/assets/` for the Wafra tenant. | Explicit owner instruction for owner-managed assets. Branding stays configuration (ADR-019). |
| Q-D | The routing file is `docs/MODEL_ROUTING.md`. | — |
| Q-E | Add the enforcement ratchets R2–R5. The three existing two-way module pairs are frozen, not scheduled for removal. | `ARCHITECTURE.md` § Enforcement. MESP-154 (#269) tracks the reduction. |
| Q-F | Tracker reconciliation: apply T-04, T-08, T-09, T-10, T-11 and the tracker conventions. #154–#174 keep their `release-1` labels. | `MODEL_ROUTING.md` §11 and `audit/tracker-reconciliation.md`. |
| Q-G | BRDs and the glossary move to `docs/requirements/`. `staticts.md` and its update rule are retired to history. The component READMEs are kept but trimmed. | — |
| Q-H | Non-Markdown documents move to `docs/assets/`. | — |
| Q-I | Keep `.runtime/` (ignored local data). | — |
| Q-J | Drop the reviewed stashes. The stash that contains a credential literal is left to the owner. | — |
| Q-K | Close no epics. Each epic whose children are all Done gets a review comment and is listed in [`ROADMAP.md`](ROADMAP.md). **Only the owner closes epics.** | — |
| Q-L | Approve D-18: cancellation now propagates from the migration AP/AR/cash-bank reconciliation reads. | Commit `3cacf79`. |
| Q-M | Remove the `p` gate. Opus writes the next prompt into `TASK.md` in the same session as its review. The owner reviews it and either asks Opus to revise it or executes it. | `MODEL_ROUTING.md` §6 and §8. It supersedes the `p` gate of the 2026-09-24 operating model. |
| Q-N | Routing: Opus 5.5 plans and accepts, Luna 6 executes, Sonnet 5 fixes diagnosed bugs, Sol 6 reviews once every 10–15 executor prompts instead of at each critical point (release go/no-go stays). Luna restarts the local backend and frontend at the end of every prompt. | `MODEL_ROUTING.md` §1, §2, §4.7, §8 and §10; `AGENTS.md` §1. Owner decision 2026-09-26. |

### 1.3 Standing product decisions (still in force)

- **Release 1** is a full-feature, reusable **B2B ERP**. Retail POS is excluded.
- **Wafra** is a validation tenant only. There is no Wafra-specific schema, workflow, permission,
  pricing, approval, accounting or lifecycle behavior.
- **No statutory work:** no ZATCA/FATOORA implementation or readiness claim. Tax/VAT is internal and
  configuration-led (PD-024).
- **No external integrations.** There are no external production integrations, providers or
  credentials in Release 1. MESP-39 (#128) is a future release.
- **Accepted decision set.** MESP-116 approved A1–A16 and B1–B6. Class B is the product contract,
  subject to specialist validation before Production. C1–C9 stay open (PD-025..PD-046, archived
  register).
- **FIN-OD-01:** Finance owns balanced journals, source-to-GL mapping, periods, subledger
  reconciliation, valuation handoff, corrections and posting evidence. Operational modules never
  fabricate journals.
- **Sequence:** finish MESP-141, then the Golden cycle, then the functional/API baseline, then UI/UX
  modernization, then MESP-142 ([`ROADMAP.md`](ROADMAP.md)).

## 2. Planner decisions (Opus 5.5, within owner instructions)

| Date | Decision | Reason |
|---|---|---|
| 2026-09-25 | Move `docs/96_Foundation_Release1_Safety_Validation.md` to `history/`, and update `SafetyCatalogueValidationTests` in the same commit. | It is a historical validation record. The test reads it by path (D-19), so the move and the test update belong together. |
| 2026-09-25 | Leave `GeneralLedger…:120` unchanged in the D-18 fix. | That read is on the execute path and serves as evidence. Only the reconciliation reads needed cancellation propagation. |
| 2026-09-25 | New cleanup, CI and debt items go under a new epic, MESP-145 (#263). The Golden cycle, baseline and UI items go under MESP-1 (#90). The Slice 11 review goes under MESP-15 (#104). | Every non-epic item needs a parent, and no existing epic owned these items. |
| 2026-09-25 | The three unkeyed CI enablers (#238–#240) were given MESP-146..148. | One key space: Q-F and the tracker conventions. |
| 2026-09-25 | Gemini is not routed any work. | It is not in the owner's Q1 routing. |
| 2026-09-25 | The eight ADRs with full texts are embedded verbatim below. The historical ADR files were removed from the tree. They remain at the tag `pre-cleanup-20260925`. | One decisions file, with no duplicate copies to drift apart. |

## 3. Architecture decision index

"Full text" means the ADR's full record is embedded in §5. ADRs marked as index-only never had a
standalone file in the repository. For those, the row below is the decision of record.

| ADR | Decision | Status | Full text |
|---|---|---|---|
| ADR-001 | Modular monolith with explicit business-module ownership and controlled dependencies. Microservices and distributed infrastructure are rejected for Release 1. | Active | index-only |
| ADR-002 | Four production projects. The dependency direction is Api → Infrastructure → App → Contracts; Api also uses App and Contracts for host composition. | Active | §5 |
| ADR-003 | One shared SQL Server database with mandatory application/persistence Tenant isolation: Tenant-aware ownership, constraints, authorization and tests. Database-per-Tenant is rejected. | Active | index-only |
| ADR-004 | Browser authentication uses the `__Host-MiniErp.Auth` secure HTTP-only cookie as an encrypted locator for a server-side `UserSession`. The server revalidates authority on each protected request. Unsafe cookie requests use framework antiforgery. There are three authorization paths: Ordinary Membership, Support Grant, and Platform Governance Context. | Active | §5 |
| ADR-005 | Authorization is server-side, policy- and resource-based. It combines permission, Tenant membership, organization scope, document state and contextual controls. UI visibility and client context are never boundaries. | Active | index-only |
| ADR-006 | Each module owns its EF Core model, schema and migrations inside `MiniErp.Infrastructure`. There is one shared SQL Server. App coordinates cross-module effects. Cross-module DbContext or table access is prohibited. | Active; production validation is gated | §5 |
| ADR-007 | Durable internal effects use Tenant-owned outbox/effect evidence and duplicate-safe, single-effect execution. The `DurableWorkEffectKey` guard model (MESP-92) is the non-reversible boundary. | Active; the production delivery provider is deferred | §5 |
| ADR-008 | Background work uses a durable-work abstraction with atomic leases, revalidated live authority, bounded retry and dead-letter handling, and guarded single-effect execution. It is not composed into `MiniErp.Api`. | Active seam; the deployment topology is deferred | §5 |
| ADR-009 | Private files go through a provider-neutral private object-storage boundary with opaque identity, immutable ownership metadata and checksums. Ordinary use never physically purges. | Active contract; production storage is deferred | §5 |
| ADR-010 | OpenTelemetry-compatible instrumentation. The exporter, access and retention are a Production decision. | Open before Production | index-only |
| ADR-011 | Runtime localization, Arabic search, RTL and bilingual documents need explicit evidence before those Production claims close. | Open | index-only |
| ADR-012 | Production hosting topology, region, availability, RPO and RTO. | Open before Production | index-only |
| ADR-013 | Secret and encryption-key management. | Open before Production credentials | index-only |
| ADR-014 | Data residency, retention, legal hold, export and purge (qualified privacy/legal validation). | Open before Production | index-only |
| ADR-015 | The Saudi e-invoicing adapter and credential boundary (qualified VAT/ZATCA validation). | Open before live invoicing | index-only |
| ADR-016 | SQL Server Row-Level Security: either adopt it explicitly with session-context and pool-reset evidence, or defer it formally with accepted risk. It is never assumed. | Open before Production security approval | index-only |
| ADR-017 | External partner/machine API authentication is deferred. It never reuses human browser cookies or token storage. | Deferred | index-only |
| ADR-018 | SQL safety tests run against disposable SQL Server LocalDB with a strict `MiniErpFoundation_*` target and fail-closed validation. LocalDB is not Production-equivalent. Docker/Testcontainers need a separately approved change. | Active; Production equivalence is deferred | §5 |
| ADR-019 | Tenant is the security boundary. The operational context (Company/Branch) sits inside an authorized Tenant. The host resolves only a *candidate* Tenant. Users land on Overview first. Branding and host binding are configuration. SAR is presentation-only. | Active; implemented (MESP-143 (#231)) | §5 |

## 4. Supersessions and corrections

- The early three-project backend wording (`Api → App → Contracts`) is superseded by ADR-002's
  four-project topology.
- **ADR-007:** the original inbox-uniqueness marker as the single non-reversible boundary is superseded
  by the MESP-92 `DurableWorkEffectKey` guard model.
- **ADR-008, `InternalsVisibleTo` (corrected 2026-09-25, D-10):** the `MiniErp.Api` friend grant was
  removed. The current grants are:
  - `MiniErp.Contracts` → `MiniErp.App` and `MiniErp.ArchitectureTests`;
  - `MiniErp.App` and `MiniErp.Infrastructure` → `MiniErp.ArchitectureTests` only.

  `FriendAssemblyPolicyTests` rejects any friend grant to `MiniErp.Api`. The 2026-09-24 handover's
  statement that the grant goes "only to ArchitectureTests" was incomplete.
- The temporary flow `Login → Choose workspace/Tenant → ERP` is superseded by ADR-019's
  Tenant-host, Overview-first model.
- **Governance (2026-09-25):**
  - `.ai/AI_EXECUTION_POLICY.md`, the `AGENTS.md`/`CLAUDE.md` overlays, `.ai/CURRENT_STATE.md` and
    `docs/staticts.md` are superseded by `AGENTS.md`, `MODEL_ROUTING.md`, `ROADMAP.md` and
    `RESULT.md`. The old files are archived in `history/`.
  - Sol's acceptance authority is superseded by Opus 5.5 (Q2).
  - The `p` gate is superseded by same-session prompt release with owner review (Q-M).
  - Sol's per-critical-point reviews are superseded by a review every 10–15 executor prompts (Q-N).

## 5. ADR full texts (embedded verbatim)

The texts below are copied unchanged from `docs/ADR-*.md` as of tag `pre-cleanup-20260925`. The only
changes are that headings are demoted by two levels so they nest under this section, and ADR-019's
three trailing-space line breaks became `\` line breaks (the whitespace gate rejects trailing spaces). Their
"current state", Jira and Sol references are historical. §1–§4 above take precedence.

### ADR-002 — Backend project structure and module enforcement

| Field | Decision |
|---|---|
| Status | Approved for Release 1 module implementation; production/provider validation remains separately gated |
| Date | 9 August 2026 |
| Owners | Hossam / Solution Architecture |
| Related Jira | MESP-100, MESP-99, MESP-48, MESP-50 |
| Supersedes | The ADR-002 timing placeholder in `docs/Decisions.md` and the three-project wording in the Technology Architecture Baseline |
| Superseded by | None |

> **Current MESP-102 implementation evidence - 9 August 2026.** The bounded
> Product identity slice confirmed this four-project topology in source: public
> Product contracts remain in `MiniErp.Contracts`, Product application behavior
> and policy remain in `MiniErp.App`, Product EF/module persistence remains in
> `MiniErp.Infrastructure`, and API endpoint composition remains in
> `MiniErp.Api`. No fifth project, direct cross-module persistence path, or
> alternate composition route was introduced. Provider, migration, and
> production validation remain gated by ADR-006 and the open MESP-48/MESP-49/
> MESP-50 controls.

#### MESP-124 implementation evidence - 17 August 2026

The Purchase Order and Supplier Confirmation slice confirms the same four
project direction without adding a project or cross-module persistence path:
Contracts owns public Procurement records, App owns source-lineage validation
and lifecycle/approval orchestration, Infrastructure owns the Procurement EF
model and formal migration, and Api owns route composition and OpenAPI
metadata. Purchase Request and Supplier Quotation facts are consumed through
their approved application contracts; PO persistence does not reach their
tables directly.

#### Context

The approved Release 1 architecture is a modular monolith. The repository has
four existing production projects, not three:

- `MiniErp.Contracts`
- `MiniErp.App`
- `MiniErp.Infrastructure`
- `MiniErp.Api`

The original baseline described a three-project starting point and did not
define how the already-existing provider/persistence project participates in
the composition root. That omission is a readiness blocker before the first
data-bearing Master Data slice. The decision must also preserve ADR-006's
module-owned persistence and shared SQL Server direction without creating a
project per table, a fifth production project, or a microservice boundary.

#### Decision

Preserve one modular-monolith deployment and enforce the following production
project responsibilities and dependency direction:

```text
MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts
     |                 |                    |
     +---------------> MiniErp.App         +--> shared public contracts only
     +---------------> MiniErp.Contracts
```

The direct `MiniErp.Api -> MiniErp.Infrastructure` reference is the approved
composition-root path. The API host may also reference `MiniErp.App` and
`MiniErp.Contracts` for its existing host and public-contract composition. No
reverse dependency is permitted and the graph must remain acyclic.

##### `MiniErp.Contracts`

`MiniErp.Contracts` owns stable public contracts, module descriptors, public
request/response/value contracts, and cross-module event contracts. It has no
project dependency on `MiniErp.App`, `MiniErp.Infrastructure`, or
`MiniErp.Api`. It must not contain EF Core or provider-specific implementation.

##### `MiniErp.App`

`MiniErp.App` owns application and module behavior, use-case orchestration,
server-authoritative authorization seams, and module-internal implementation.
It depends on `MiniErp.Contracts` only. It must not depend on EF Core,
`MiniErp.Infrastructure`, or `MiniErp.Api`. Application module internals remain
internal and are exposed through explicit public contracts/composition seams.

##### `MiniErp.Infrastructure`

`MiniErp.Infrastructure` is the existing provider/persistence implementation
project. It may depend on `MiniErp.App` and `MiniErp.Contracts`; it must not
depend on `MiniErp.Api`. EF Core, SQL Server provider code, persistence
sessions, mappings, provider adapters, and migrations belong here.

Business-module ownership remains explicit inside this shared project. Future
Master Data persistence will use a Master Data-owned structure such as:

```text
MiniErp.Infrastructure/
  Persistence/
    Modules/
      MasterData/
      MasterDataDbContext.cs
      Configurations/
      Migrations/
```

The names above describe the ownership boundary; MESP-100 does not create the
context, entities, tables, mappings, or migrations. When a data-bearing slice
is implemented, the Master Data persistence boundary must ensure that:

1. the Master Data context, mappings, schema, and migrations are owned by the
   Master Data module;
2. another module cannot add a Master Data `DbSet`, repository, table mapping,
   or migration operation;
3. cross-module reads use approved contracts or application ports rather than
   direct table access;
4. Tenant ownership, query filters, stored-owner verification, concurrency,
   and transaction rules remain enforced at the approved persistence boundary;
5. architecture tests inspect the source/project graph and reject a cross-
   module persistence shortcut.

##### `MiniErp.Api`

`MiniErp.Api` remains the host and composition root. It directly references
`MiniErp.Infrastructure` so it can call provider/module registration methods
when a provider-backed composition is due. It may compose application and
contract seams directly, but it must not own business persistence or reach
Infrastructure through a new intermediary project. MESP-100 adds the explicit
project reference and architecture enforcement; it does not select a provider,
open a production database, or compose Category/UOM persistence.

#### Relationship to ADR-006

ADR-002 resolves project ownership, compile-time dependency direction, and the
host's legal route to Infrastructure. ADR-006 remains authoritative for
behavior and persistence ownership:

- Release 1 uses one shared SQL Server database shape.
- Tenant ownership and stored-owner checks are mandatory.
- Each module owns its EF model, mappings, repositories, schema namespace, and
  migrations inside the approved Infrastructure project structure.
- Direct cross-module persistence access is prohibited.
- Production migrations, provider selection, and production validation are
  reviewed separately.

ADR-002 does not replace or broaden ADR-006, and it does not turn the local
Foundation provider/test seam into a production provider decision.

#### Alternatives considered

1. **Keep the three-project graph (`Api -> App -> Contracts`)** — rejected
   because it omits the already-existing Infrastructure project and leaves the
   composition path ambiguous.
2. **Put EF Core and provider code in `MiniErp.App`** — rejected because it
   violates the application/provider boundary, weakens module ownership, and
   makes provider-specific dependencies available to business behavior.
3. **Create one Infrastructure project per module** — rejected for Release 1;
   explicit module folders/namespaces and architecture tests provide ownership
   without a project-per-table or project-per-module explosion. A later split
   would require a new decision and measured need.
4. **Compose Infrastructure indirectly through App or a new adapter project**
   — rejected because the host would lose an unambiguous provider composition
   path and a fifth production project would be introduced.
5. **Move to microservices or separate databases** — rejected because the
   approved Release 1 posture is a modular monolith with shared SQL Server and
   application-layer Tenant isolation.

#### Enforcement and evidence

The repository must keep focused architecture tests that prove:

- Contracts do not reference App, Infrastructure, or Api;
- App references Contracts and not EF Core, Infrastructure, or Api;
- Infrastructure references App/Contracts and not Api;
- Api's project references include Infrastructure as the composition-root
  path, alongside its existing App/Contracts host references;
- the four-project graph has no cycle;
- public Infrastructure surfaces do not expose unscoped EF shapes or Tenant
  override/bypass parameters.

MESP-100's focused `ModuleBoundaryTests` enforce the project-reference graph,
and the existing compiled/source architecture tests continue to enforce the
remaining rules. These tests are structural guardrails; they do not authorize
any domain persistence by themselves.

#### Consequences

Positive consequences include a truthful four-project architecture, a clear
host-to-provider composition route, preserved Contracts/App boundaries, and an
explicit legal home for future module-owned EF Core work. Master Data can add
Category/UOM persistence inside Infrastructure without granting another module
direct table access or requiring a fifth production project.

The trade-off is that Infrastructure becomes a shared provider assembly whose
internal module ownership must be maintained with folders, namespaces,
internal visibility, and architecture tests. A future independent project
split may be justified by measured boundary pressure, but it is not implied by
this ADR.

#### Scope and deferred decisions

This ADR does not create Category/UOM persistence or decide any Category/UOM
business behavior. It does not decide production hosting topology, SQL Server
vendor/host, RLS, retention, privacy, residency, backup/restore targets, legal
hold, purge, production credentials, or any other MESP-48/MESP-49/MESP-50 gate.
It introduces no Retail POS or Wafra-specific core behavior and no later Master
Data slice.

#### Approval and review

This decision is published as the MESP-100 readiness correction on 9 August
2026 under Hossam's standing Owner approval. It is the required project/
composition decision for MESP-99 Category/UOM implementation. Future changes
to project direction, module persistence ownership, or production provider
selection require a new reviewed ADR or an explicit superseding decision.

### ADR-004 — Identity cookie, server session, antiforgery, context resolution and authentication-assurance policy

- **Status:** Accepted for Foundation Release 1 implementation
- **Date:** 2026-08-04
- **Owner:** Product and Architecture
- **Related Jira:** MESP-28, MESP-38, MESP-55, MESP-59, MESP-89

#### Decision

Release 1 uses a first-party ASP.NET Core cookie only as an encrypted locator
for a server-side `UserSession`. The approved cookie is
`__Host-MiniErp.Auth`; it is `HttpOnly`, `Secure`, scoped to `/`, has no
`Domain`, and uses `SameSite=Lax` for the first-party Angular deployment
model. The session has an eight-hour absolute lifetime, a thirty-minute
inactivity timeout, and no sliding extension beyond the absolute expiry.
Every protected request revalidates the opaque session against the server-side
user, session, membership, support-grant and platform state. Cookie presence,
claims, headers, routes and request bodies are never authorization authority.
No authentication token is placed in browser localStorage or sessionStorage.

The host registers ASP.NET Core cookie authentication, authorization and
antiforgery services. Unsafe cookie-authenticated methods validate a framework-
generated request token against its framework-managed antiforgery cookie and
the configured request header. Static or predictable tokens are not accepted;
GET, HEAD and OPTIONS do not mutate state or require antiforgery validation.

The server owns exactly one authorization path for a request:

1. `OrdinaryMembership`;
2. `SupportGrant`; or
3. `PlatformGovernanceContext`, for Platform operations only.

Platform governance is not a Tenant path. The server lists eligible contexts,
stores the selected context in trusted session state, and confirms every
context switch against the server's `SelectionVersion` and the candidate's
`EligibilityVersion`. These are separate values: the former is the optimistic
version of the selected session path, while the latter is the current
membership, support-grant or platform eligibility version. A client-supplied
Tenant, path, role, permission, scope or grant cannot create or change
authority. Cross-Tenant, stale and mixed-path selections fail closed. MFA and
fresh-authentication evidence remain server-validated; the unavailable
production assurance provider fails closed whenever that assurance is required.

Every public operation is represented by one catalog-backed
`FoundationOperationDescriptor`. The descriptor carries the operation ID,
security profile, exact permission code, scope policy, assurance requirements,
antiforgery requirement and mandatory-evidence policy. Endpoint metadata,
OpenAPI, trusted context resolution, Identity authorization and downstream
validation all consume that same descriptor. The operation ID is never treated
as a granted permission; `FoundationRequestContext.Permission` contains only
the exact permission that the server successfully authorized. Unknown or
tampered descriptors fail closed, and unrelated Support or Platform
permissions cannot substitute for the operation's exact permission.

Successful protected writes append immutable safe evidence through the
non-nullable `FoundationAuditCoordinator` before applying their effect.
Protected endpoint registration and the architecture tests require every
unsafe non-anonymous handler to invoke this centralized executor; there is no
nullable executor or unaudited fallback. Evidence append failure prevents the
effect. If an effect fails after an allowed evidence record, the original
record is preserved and a linked neutral `EffectFailed` outcome is appended
without exception or provider details. A session-only sign-out with no
selected authorization path is a lifecycle revocation (not a Tenant or
Platform business effect); it still requires antiforgery and follows the
documented conditional evidence policy, while a sign-out from a selected path
is evidenced before revocation.

The local idempotency namespace is the composite of the authorized binding and
normalized opaque key. Tenant and User bindings therefore do not interfere
with one another. Every decision is handled explicitly; successful effects
commit a typed safe response, replays return that original response (including
its original selection version), and reservations are released in `finally`
unless a success commit completed.

#### Scope and provider levels

This ADR covers the Foundation HTTP host integration and its bounded local,
in-memory development seams. It does not claim production readiness for the
provider layer. Production deployment must separately select and validate:

- durable Identity persistence;
- an external identity provider, if required;
- a production MFA/fresh-authentication provider;
- production email/SMS delivery;
- distributed session storage;
- durable distributed idempotency;
- a durable audit store and exporter;
- SQL Server migrations and deployment configuration.

The local store and assurance source are intentionally bounded test/development
implementations. They are not a production persistence, key-management,
retention, purge, residency, legal-hold, backup or restore decision.

#### Consequences

The API host can safely establish the minimum session and context contract
needed by the Angular shell while preserving the MESP-59 server authority. The
implementation remains a modular monolith and does not introduce microservices,
an external IdP, a production database or a deployment topology. MESP-63
frontend implementation remains gated on review and approval of this host
integration.

#### Alternatives rejected

- bearer tokens in local/session storage;
- Tenant or authorization claims treated as sufficient authority;
- client-selected Tenant/path headers;
- static antiforgery tokens;
- a global raw idempotency-key namespace;
- treating a Membership or SupportGrant eligibility version as the session
  context-selection version;
- applying a protected effect before evidence is appended;
- treating Platform governance as a Tenant authorization path.

#### Review and supersession

This ADR is the Foundation Release 1 implementation baseline. Production
provider, deployment, retention and assurance decisions require separate
approved ADRs and validation before production. It may only be superseded by a
new approved ADR with explicit security, tenant-isolation and audit evidence.

### ADR-006 — Module schemas, EF Core contexts, migrations, and cross-module transactions

| Field | Decision |
|---|---|
| Status | Foundation implementation baseline; production validation remains gated |
| Date | 4 August 2026 |
| Owners | Solution Architecture / Persistence Engineering |
| Related Jira | MESP-61, MESP-64, MESP-48, MESP-50 |
| Supersedes | None |

#### MESP-123 B2 local-provider reconciliation — 16 August 2026

The bounded B2 implementation makes the shared SQL Server shape executable for
local Development without changing the production gate. When the explicit
`MESP_SQLSERVER_CONNECTION_STRING` is configured, the five module contexts
use server `.` / database `MESP` and apply formal migrations in this order:
Tenancy, Master Data, Business Parties, Procurement, then Inventory. Each
context has a distinct `dbo.__EFMigrationsHistory_*` table. `tenancy.TenantOwnedRecords`
is a shared runtime table but has one physical owner: the Tenancy context. The
other module alignment migrations are no-op database migrations whose model
snapshots reflect that shared ownership; they do not create duplicate tables.

The Development startup migrator is exact-environment-only and production
startup never calls it. A bounded inventory-first cutover utility moved the
existing local SQLite rows into the empty `MESP` database with a recoverable
backup, preserved IDs/Tenant IDs and foreign-key lineage, verified source
hashes, and retained the SQLite originals. This is local Development evidence,
not a production migration, deployment, backup/restore, capacity, HA/DR,
residency, retention, or MESP-48/MESP-50 approval.

#### MESP-124 Purchase Order persistence reconciliation - 17 August 2026

The bounded Purchase Order and Supplier Confirmation slice preserves this ADR's
module ownership. `ProcurementDbContext` owns the `procurement` tables for
Purchase Orders, lines, confirmations, confirmation lines, evidence, supplier
changes, lifecycle history, and audit. All eight entity types are Tenant-owned
and registered with the stored-owner verifier; Company/Branch scope remains an
application-authorized context inside the already authorized Tenant.

The formal EF migration
`20260817143432_PurchaseOrderAndSupplierConfirmation` adds these tables and
indexes without introducing a second database, a competing migration history,
or a shared-table ownership change. The application revalidates the approved
Purchase Request, Supplier Quotation, Source Decision, supplier, currency,
scope, and selected lines before creating immutable source/commercial snapshots.
The migration is a Development/runtime artifact subject to the existing
production migration, supported-volume, retention, privacy, backup/restore,
and cutover gates. The official disposable SQL safety runner remains the only
accepted full backend safety entry point.

#### Context

Release 1 uses a shared SQL Server database with strict application-layer
Tenant isolation and database schemas separated by business module. The
Foundation work needs a bounded persistence seam for durable work, outbox and
inbox records without turning the modular monolith into a collection of
microservices or granting a worker a global business-data query path.

#### Decision

1. Each module owns its EF Core model, mappings, repositories and schema
   namespace. A module may reference shared contracts/building blocks only
   through the approved dependency direction; it must not reach another
   module's DbContext or tables directly.
2. The shared SQL Server database remains the Release 1 deployment shape. A
   Tenant-owned row carries an immutable TenantId and the persistence guard
   verifies stored ownership on modified and deleted entities before saving.
3. Cross-module business effects are coordinated in the application layer and
   use one explicit transaction boundary when the owning module requires it.
   A transaction is not an excuse to bypass Tenant checks, query filters or
   authorization evidence.
4. MESP-61 may provide provider-neutral contracts and a deterministic local
   adapter. It does not choose a SQL deployment topology or claim production
   migration readiness.
5. MESP-64 owns disposable SQL Server provider validation, schema/index/
   concurrency probes and the evidence report. B2's local `MESP` migrations
   and data cutover are a separate Development convenience and do not replace
   the disposable safety gate or the separately reviewed production delivery
   step.

#### Alternatives considered

- A database per Tenant was rejected because the approved direction is one
  shared database with strict isolation.
- Microservices and a distributed transaction coordinator were rejected for
  the one-developer modular-monolith scope.
- A single unrestricted shared DbContext was rejected because it obscures
  module ownership and makes cross-Tenant access easier to introduce.
- SQLite-only evidence is insufficient for SQL Server-specific semantics and
  is therefore limited to fast local contract tests.

#### Consequences and guardrails

- New module entities require an owning module, Tenant ownership decision,
  schema mapping, index/unique-key review and targeted tests.
- `IgnoreQueryFilters`, raw SQL, bulk operations and maintenance paths remain
  restricted to an explicit privileged boundary and are not available to
  ordinary Tenant calls.
- The first production migration must be reviewed against the approved BRDs,
  MESP-48 supported-volume evidence and MESP-50 retention/privacy/legal-hold/
  purge requirements.
- SQL Server Row-Level Security is not selected here. ADR-016 remains the
  production decision record for adoption or formal deferral.

#### Explicitly deferred

This ADR does not decide production region, provider/vendor, backup or restore
targets, retention durations, legal hold, purge execution, residency, or RLS.
Those decisions remain owned by the approved MESP-48/MESP-50 gates and the
applicable production ADRs.

#### Evidence expected from MESP-64

The final Foundation safety report must identify which assertions are covered
by architecture tests, local provider tests and disposable SQL Server tests.
It must show stored-owner update/delete denial, same-Tenant relationship
integrity, unique/index behavior, rowversion/stale-write behavior and
transaction atomicity without touching a production or shared database.

### ADR-007 — Internal events and transactional outbox/inbox

| Field | Decision |
|---|---|
| Status | Foundation implementation baseline; MESP-92 single-effect correction merged and Done (PR #22, `322341e70e56270797d5770b4b90342c20b7833e`); production delivery provider deferred |
| Date | 4 August 2026; reconciled 6 August 2026; reconciled again 7 August 2026 |
| Owners | Solution Architecture / Application Engineering |
| Related Jira | MESP-61, MESP-64, MESP-91, MESP-92, MESP-93, MESP-48, MESP-50 |
| Supersedes | None |

#### Context

Durable work needs a recoverable hand-off from application state to a later
effect. The first implementation must preserve exact Tenant and organization
scope, survive duplicate delivery, and remain practical for a single developer.

#### Decision

1. The smallest approved seam is a Tenant-owned transactional outbox message
   created with the durable-work record. The event identity is stable and the
   idempotency key is unique within the Tenant boundary.
2. An inbox record keyed by `(TenantId, EventId)` makes duplicate delivery a
   safe no-op. A protected effect is acknowledged only after the effect
   callback succeeds; failures use a bounded retry and safe dead-letter state.
3. Outbox and inbox records carry Tenant, applicable Company/Branch/Warehouse
   scope, work identity and correlation, but never payload secrets, tokens,
   cookies, private file bytes or provider exception text.
4. MESP-61 supplies the typed contract and deterministic in-memory adapter for
   tests/development. No broker, Kafka/RabbitMQ cluster or production provider
   is selected.
5. A production relational implementation must preserve the same atomicity,
   unique-key and Tenant-ownership invariants and will be validated through
   MESP-64 before any production decision.

#### Alternatives considered

- A broker-first design was rejected because no distributed infrastructure is
  approved for the Foundation and it would obscure the transaction boundary.
- Fire-and-forget in-process events were rejected because process failure could
  lose a required protected effect.
- A global event table or dispatcher query was rejected because it would create
  an unscoped Tenant business-data path.

#### MESP-92 single-effect correction (Done — merged to `main` at `322341e70e56270797d5770b4b90342c20b7833e`)

The inbox uniqueness marker described in point 2 is superseded by a
server-owned `DurableWorkEffectKey` guarded by
`IDurableWorkEffectGuard`/`IDurableWorkEffectExecutor`. Reservation of that
key is the single non-reversible boundary; a protected effect is acknowledged
as `Applied` (recorded Completed, never repeats) only after the effect
callback returns an explicit `DurableWorkProtectedEffectResult.Applied`
outcome. Outbox dispatch now reports one of four explicit outcomes:

- **Applied** — the effect ran (or was proven already Completed) and is
  recorded so duplicate delivery safely replays the same result without
  repeating the effect.
- **NotAppliedRetryable** — the provider/handler positively confirms the
  effect did not occur (for example, a live-authority provider outage or
  cancellation before the reservation boundary, or an explicit provider
  report after it); bounded retry may run. A generic retry alone is never
  sufficient: only this explicit outcome may release a reservation.
- **TerminalNotApplied** — the effect is proven not to have started and will
  never succeed; a safe terminal result is recorded and never repeated.
- **OutcomeUnknown** — the effect boundary was reached but a caught
  exception, cancellation or completion-recording failure observed inside
  the running process means its outcome cannot be proven; delivery moves to
  the dedicated, Tenant-scoped `DurableWorkLifecycle.OutcomeUnknown`
  reconciliation state, is never automatically repeated by normal polling or
  generic redelivery, and requires explicit reconciliation through
  `IDurableWorkStore.ReadUncertainEffectsAsync`. An actual process crash
  loses this in-memory adapter's guard and lifecycle state entirely and is
  **not** represented as `OutcomeUnknown` or any other recorded outcome;
  production durable crash recovery remains deferred to a future SQL/durable
  provider.

##### H92-01 — effect-purpose and EventId keying

`DurableWorkEffectKey` carries a server-owned `DurableWorkEffectPurpose`
(`Handler` or `Outbox`) in addition to Tenant, WorkItemId and OperationId; an
outbox-purpose key also carries the immutable `EventId`. This keeps a handler
effect and an outbox effect for the identical Tenant/WorkItemId/OperationId
independent even when both are guarded by the same shared executor, while
redelivery of the same outbox `EventId` still resolves to the same key and
two different `EventId`s remain independent. The same effect key and one
shared guard protect both the outbox effect path and the direct
worker/dispatcher handler path, so a duplicate submission or a concurrent
redelivery across either path still produces exactly one effect invocation
per purpose. Provider exception text is never persisted in outbox or audit
evidence; only a safe, bounded category and reason are recorded.

##### H92-03 — one structurally enforced composition (focused review correction)

`DurableWorkEffectComposition.CreateSharedExecutor()` produced a new,
independent ledger on every call, so the production API previously permitted
the store, the dispatcher and a second dispatcher to each receive a different
executor. `DurableWorkLocalRuntime.Create(operationCatalogue, payloadRegistry)`
is now the single approved composition entry point: it is the only place
shipping code may construct `InMemoryDurableWorkEffectGuard`,
`DurableWorkEffectExecutor`, `InMemoryDurableWorkStore` or
`DurableWorkDispatcher` (all four constructors are `internal`), and it
supplies the identical executor instance to the store and dispatcher it
returns. A syntax-tree architecture test scans all of `src/MiniErp.App` and
fails if any of those four types is constructed anywhere outside
`DurableWorkLocalRuntime.cs`.

##### H92-04 — exact-scope reconciliation authorization (focused review correction)

`ReadUncertainEffectsAsync(TenantContext)` previously filtered only by
TenantId, so any same-Tenant context could see uncertain-effect records
belonging to a sibling Company, Branch or Warehouse. The port now takes a
server-issued `VerifiedDurableWorkReconciliationAuthorization`, issued only by
`IDurableWorkReconciliationAuthorizer` after live-revalidating actor, session,
Membership-or-SupportGrant validity and a dedicated catalogue-backed
`work.reconciliation.read` permission, reusing the identical
organization-scope ownership/containment logic as MESP-91 dispatch
revalidation. A missing or malformed selected scope fails closed, and
`PlatformGovernanceContext` has no path into this authorizer.

##### M92-03 — exact uncertain-effect identity (focused review correction)

`DurableWorkUncertainEffectRecord` now carries the exact
`DurableWorkEffectKey` (so `OperationId` is always present and `EventId` is
present only for an Outbox-purpose record), the exact verified
`TenantWorkScope`, the actual `OutcomeUnknownAt` transition time and a
preserved safe reason, instead of `NextAttemptAt` and a hard-coded reason.

#### Consequences and guardrails

- Every new event requires an owning module, stable event type, Tenant/scope
  facts, correlation and idempotency behavior.
- Duplicate delivery must be demonstrably single-effect; inbox uniqueness is
  part of the persistence contract, not merely a convention.
- Retry delay and attempt count are bounded. Dead-letter evidence contains only
  a safe category/reason and identifiers allowed by the audit policy.
- A production exporter, broker, delivery provider, retention period, purge
  policy, legal hold and residency are not implied by this ADR.

#### Gates

MESP-48 owns supported-volume/performance and operational capacity evidence.
MESP-50 owns retention, privacy, legal hold, purge, residency, backup and
restoration decisions. No production readiness claim is made until both gates
and the applicable operational ADRs are closed.

### ADR-008 — SQL-backed job execution and worker ownership

| Field | Decision |
|---|---|
| Status | Foundation worker seam; MESP-91 live authority correction merged and Done (PR #20, `f2cde57400fed470ab048776e05b56f353b36890`); MESP-92 single-effect/immutable-payload correction merged and Done (PR #22, `322341e70e56270797d5770b4b90342c20b7833e`); deployment topology deferred |
| Date | 4 August 2026; reconciled 6 August 2026; reconciled again 7 August 2026 |
| Owners | Solution Architecture / Background Processing |
| Related Jira | MESP-61, MESP-64, MESP-91, MESP-92, MESP-93, MESP-48, MESP-50 |
| Supersedes | None |

#### Context

Background work must not lose Tenant identity when execution is detached from
the request that created it. It must also avoid a privileged global scan of
business repositories and must provide bounded retry, lease ownership and
safe failure evidence.

#### Decision

1. Work is claimed from an owned durable-work store using an atomic lease and
   optimistic concurrency version. Only one worker can hold an active lease.
2. The worker/outbox consumer reconstructs a trusted execution context only
   from a server-issued `VerifiedDurableWorkAuthorization`. That result binds
   the exact stored WorkItemId and Tenant, operation descriptor, correlation,
   organization boundary, execution TenantContext, authorization path,
   Membership or SupportGrant, actor and session after live Identity
   revalidation through narrow ports. `DurableWorkExecutionContext` defensively
   repeats the same exact-binding check. Stored expiry, permission and scope
   snapshots are evidence only; they are not current authorization.
   Identity-owned hierarchy resolution proves Tenant -> Company -> Branch ->
   Warehouse ownership and downward containment. Missing, mismatched or
   unauthorized context fails closed; an ordinary context requires a canonical
   explicit selected scope, while a SupportGrant uses the current case-bound
   stored grant scope rather than a context marker. There is no fallback Tenant.
   `PlatformGovernanceContext` cannot execute Tenant work.
3. A dispatcher resolves one typed handler by the authoritative operation
   descriptor, including its exact permission, allowed authorization paths and
   scope policy and mandatory security-evidence requirement. A descriptor that
   opts out of mandatory evidence cannot create work, register a handler,
   dispatch, or produce verified authority. Only the Identity issuer may issue
   shipping verified authority; a structural architecture test allow-lists that
   issuer and keeps test-only fixtures in the test project. The dispatcher may
   inspect only the module-owned envelope needed to claim/execute that work and
   must not enumerate Tenant business data globally.
4. Handler outcomes are success, bounded retry or safe dead letter. Expired
   leases can be reclaimed; an active lease cannot be stolen.
5. MESP-61 implements this seam with a deterministic local adapter. The
   production SQL-backed worker store and hosting topology remain implementation
   and deployment decisions validated by MESP-64 and later operational review.

MESP-91 adds the live authority correction to this seam. A failed current
User/session, Membership, SupportGrant/SupportCase, Permission, scope or
organization-ownership check is a terminal `AuthorizationDenied` dead letter
with safe evidence; it does not retry indefinitely and it cannot reach a
handler or protected outbox effect. An authority-provider exception is
`TemporarilyUnavailable`/`ProviderUnavailable` and follows bounded retry; a
cancellation is a distinct recoverable `Cancelled` outcome. Neither is
converted into an authorization denial, and the lease/outbox state transition
uses the minimal detached cancellation token needed to preserve recovery.
The operation catalogue is the only source of the exact permission and
handler binding; unknown, mismatched or non-evidenced descriptors fail closed.
The focused H91-03/H91-04 regression suite covers missing/malformed ordinary
scope, support-grant authority, broader/sibling scope and every exact stored
binding. The
SQL/MESP-64 probes validate persistence, lease, transaction and idempotency
behavior only; they are not worker-authorization evidence.

MESP-92 adds single-effect and immutable-payload guarantees to this seam. A
submitted payload is captured immediately into an immutable, checksummed
envelope through an explicit `IDurableWorkPayloadRegistry`; no original
caller payload reference is retained, and unknown types, handler/payload
mismatches, checksum tampering and oversized payloads fail closed before a
handler runs. A focused ChatGPT re-review of PR #22 requires production code
to expose no payload-mutation fault-injection hook; checksum-corruption is
exercised only through bounded test-project reflection, and a custom codec's
encode/decode exception is always wrapped in the safe
`DurableWorkPayloadException`.

The dispatcher resolves a stable, purpose-qualified `DurableWorkEffectKey`
(Tenant, `DurableWorkEffectPurpose.Handler`, WorkItemId, OperationId — an
outbox-purpose key additionally carries the immutable `EventId`), and every
registered handler invocation is routed exclusively through
`IDurableWorkEffectExecutor.ExecuteHandlerEffectAsync` (architecture-enforced;
a handler cannot bypass this while remaining a protected durable-work
handler). The store's outbox dispatch and the dispatcher's handler execution
share one authoritative executor, obtained only through the single approved
composition entry point `DurableWorkLocalRuntime.Create(operationCatalogue,
payloadRegistry)` (H92-03 focused review correction: it is the only place
shipping code may construct the guard, executor, store or dispatcher, all
four constructors now being `internal`, and a syntax-tree architecture test
proves no other shipping construction site exists); the purpose and
EventId in the key keep the two effect categories independent within that one
shared guard. `DurableWorkLocalRuntime`'s public surface is limited to
`Store` and `Dispatcher` (H92-05 focused review correction): `EffectGuard`
and `EffectExecutor` are internal properties, and `IDurableWorkEffectGuard`,
`InMemoryDurableWorkEffectGuard`, `IDurableWorkEffectExecutor` and
`DurableWorkEffectExecutor` are internal types, so a shipping caller holding
the runtime cannot reserve, release, complete or mark an effect uncertain
outside the executor — closing a release-mid-flight path that would have let
a second dispatch execute an already-reserved effect twice. Reservation of
that key is the single non-reversible boundary:
an interruption before it permits bounded retry, and the protected callback
must return an explicit `DurableWorkProtectedEffectResult` outcome —
`Applied`, `NotAppliedRetryable`, `OutcomeUnknown` or `TerminalNotApplied` —
so a bare generic retry can never release a reservation after an effect may
already have run. A caught exception or cancellation observed inside the
running process after that boundary is recorded `OutcomeUnknown` — a
dedicated, Tenant-scoped reconciliation lifecycle state, never automatically
repeated and readable only through
`IDurableWorkStore.ReadUncertainEffectsAsync`, which requires a server-issued
`VerifiedDurableWorkReconciliationAuthorization` scoped to the exact
Tenant/Company/Branch/Warehouse boundary and its verified descendants only
(H92-04 focused review correction; a sibling organization is never visible).
The guard preserves its own safe reason on the OutcomeUnknown transition
(O92-01), but `IDurableWorkEffectGuard.GetOutcomeUnknownReason` is not a
public raw-key evidence path: the interface is internal (M92-05 focused
review correction), so the only publicly reachable uncertain-effect evidence
remains the scope-authorized `ReadUncertainEffectsAsync` port above. A
duplicate dispatch of an already-Completed effect replays the exact recorded
safe result instead of re-invoking the handler. `InMemoryRelationalDurableWorkStore`/
`IRelationalDurableWorkStore` are renamed to `InMemoryDurableWorkStore`/
`IDurableWorkStore` to remove the misleading relational/SQL-backed
implication.

**Maturity boundary, corrected:** this in-memory adapter preserves only a
caught post-boundary interruption (an exception or cancellation observed
inside the running process) as `OutcomeUnknown`. An actual process crash
loses the adapter's in-memory guard and lifecycle state entirely; that state
loss is **not** represented as `OutcomeUnknown` or any other recorded
outcome. Production durable crash recovery remains deferred to a future
SQL/durable provider; this adapter remains a non-crash-durable Foundation
seam only.

#### Alternatives considered

- A hosted worker that trusts request/client Tenant input was rejected because
  it permits context confusion and cross-Tenant execution.
- A global platform governance context was rejected as a Tenant execution path;
  governance remains purpose-bound control-plane work.
- An unbounded retry loop was rejected because it can amplify provider failure
  and hide dead-letter conditions.

#### Consequences and guardrails

- Stored owner verification is required on read, claim and completion. The
  worker never mutates ownership or scope.
- The initiating Tenant, verified organization ownership, authorized scope and
  live Identity authority are revalidated immediately before handler and
  outbox-effect dispatch. True denials are terminal and safe; provider
  unavailability and cancellation have bounded recovery outcomes.
- Lease duration, maximum attempts and backoff are bounded by code-level
  contracts; production values require an approved operational decision.
- The host may later run a dedicated worker process or the same deployable
  application, but this ADR does not select production topology, capacity,
  scheduler, region or provider.
- Worker telemetry and audit are allow-listed and redacted; payloads, tokens,
  cookies, private bytes and provider exception text are excluded.

#### Composition status

Verified on 6 August 2026: `DurableWorkLocalRuntime`,
`InMemoryDurableWorkStore`, `DurableWorkDispatcher` and
`TenantDurableWorkWorker` are **not referenced by `MiniErp.Api`**. This ADR
describes a contract and a local, in-memory, non-crash-durable adapter proven
by automated tests; **no worker is composed into the running host**, no worker
is scheduled, and nothing here is a production capability. A future host
composition root must call `DurableWorkLocalRuntime.Create` exactly once and
reuse the returned instance; the syntax-tree architecture test enforces that
no other shipping construction site exists. Verified at head
`576996f94ae9ddc251767445a7ebddd60c492c45` (H92-05/M92-05 correction, 7 August
2026): `MiniErp.Api` still does not reference any durable-work type, so
tightening `DurableWorkLocalRuntime`'s public surface to `Store`/`Dispatcher`
only changed nothing reachable from the host.

**H92-06/M92-07 correction (7 August 2026, head `e991641`):** at every head up
to and including `576996f94ae9ddc251767445a7ebddd60c492c45`, `MiniErp.App`
still granted `[assembly: InternalsVisibleTo("MiniErp.Api")]`. **That grant
alone made the preceding paragraphs' `internal` claims incomplete**: a friend
assembly sees another assembly's `internal` members exactly as if they were
public, so `MiniErp.Api` could still reach `EffectGuard`/`EffectExecutor`,
construct `InMemoryDurableWorkEffectGuard`/`DurableWorkEffectExecutor`
directly, and call `TryReserve`/`Release`/`RecordCompleted`/
`RecordOutcomeUnknown`/`GetOutcomeUnknownReason` — the H92-05/M92-05 `internal`
modifiers narrowed the *source-level* surface but did not close the
*compiled* shipping boundary. The correction removes that grant; `MiniErp.App`
now declares `InternalsVisibleTo` only for `MiniErp.ArchitectureTests`. The
one resulting `MiniErp.Api` compile break was unrelated to durable work
(`FoundationHostSignInResult.Principal`, needed by the sign-in endpoint to
call `HttpContext.SignInAsync`) and was resolved by making that one property
public rather than restoring friend access. No mutable ledger type is public.
M92-07 closes as a direct consequence: `GetOutcomeUnknownReason` is declared
only on the already-internal `IDurableWorkEffectGuard`, so removing the friend
grant removes `MiniErp.Api`'s only path to it too.
`FriendAssemblyPolicyTests.cs` proves this by full Roslyn compilation: source
compiled under the assembly name `MiniErp.Api` fails to compile (`CS0122`)
against the internal ledger surface, while identical source compiled under
`MiniErp.ArchitectureTests` still succeeds.

#### Gates

MESP-48 owns supported-volume, queue depth, throughput, lease and recovery
evidence. MESP-50 owns retention, privacy, legal hold, purge, residency,
backup and restoration requirements for durable records. No production worker
deployment or retention claim is authorized by this ADR.

### ADR-009 — Private object-storage adapter and access boundary

| Field | Decision |
|---|---|
| Status | Contract baseline; MESP-93 access-outcome and lifecycle hardening implemented, merged and Done (PR #24, `005c796629341ab9becfbc6d1abe2ae34b6a7332`); production storage decision deferred |
| Date | 4 August 2026; reconciled 7 August 2026 |
| Owners | Solution Architecture / Security Engineering |
| Related Jira | MESP-61, MESP-64, MESP-93, MESP-38, MESP-39, MESP-50 |
| Supersedes | None |

#### Context

Release 1 requires private files without allowing an object key, URL or client
Tenant value to expand authority. The Foundation needs a provider-neutral
boundary that can be tested before a production object-storage vendor, region,
retention or scanning policy is approved.

#### Decision

1. `IPrivateObjectStorage` accepts a trusted TenantContext and validated
   organization scope for every operation. Object identity is opaque and is
   never a public URL or an anonymous access token.
2. Metadata records immutable Tenant/scope ownership, safe original filename,
   content type, length, SHA-256, created/optional expiry metadata and an
   optimistic concurrency version. Store/read checksum validation is mandatory
   where the adapter supports bytes.
3. Cross-Tenant reads and overwrites fail closed without returning foreign
   metadata or content. Expiry changes access disposition only; it never causes
   physical purge. A future logical-disposition operation must preserve the
   same Tenant and concurrency checks.
4. MESP-61 implements a bounded in-memory adapter for tests/development only.
   It is not evidence of a production provider, signed-download mechanism,
   malware scanner, region, retention or purge policy.
5. Production object storage, private networking, key management, scanning,
   signed-download duration and lifecycle policy require a later decision and
   MESP-50 review.

#### Alternatives considered

- Public buckets/URLs were rejected because private-by-default access is an
  approved security requirement.
- Passing a caller-supplied object key or Tenant ID to the provider was
  rejected because it would make storage authority client-controlled.
- A file byte column in business tables was rejected for the Foundation because
  it couples module transactions to an unapproved storage/retention policy.

#### Consequences and guardrails

- File metadata is Tenant-owned and must be audited with safe allow-listed
  access outcomes; foreign target identifiers are not exposed in denied errors.
- The adapter does not physically delete data or claim expiration cleanup.
- Production provider, region, residency, encryption-key management, backup,
  retention, legal hold, purge and scanning remain separately approved.

#### Gates

MESP-48 owns supported-volume/performance and recovery evidence for file
operations. MESP-50 owns privacy, retention, residency, legal hold, purge,
backup and restoration. ADR-009 does not supersede ADR-016 or any production
security decision.

#### MESP-93 hardening (7 August 2026, merged and Done)

Point 3's "fail closed without returning foreign metadata or content" is now
enforced as external indistinguishability, not merely non-disclosure: a
foreign-Tenant object and a genuinely missing object return the identical
`PrivateFileAccessOutcome.NotFound` to the caller (M-1). The foreign-vs-missing
distinction is preserved only in the adapter's internal safe audit-evidence
list, never in the caller-visible result. Point 3's overwrite guarantee is
extended to any prohibited lifecycle state, not only a Tenant mismatch: an
expired object or one whose live-recomputed checksum no longer matches its
recorded hash also fails closed on `OverwriteAsync`, so an invalid existing
object cannot be silently resurrected by an ordinary overwrite (M-4). Original
filename validation now normalizes to Unicode Normalization Form C and
rejects, rather than tolerantly truncates, any value containing a path
separator, traversal sequence, or Unicode bidirectional/embedding/isolate/
mark/zero-width formatting character (M-5); valid Arabic and mixed
Arabic/English filenames remain fully supported. This remains the MESP-61
bounded in-memory adapter; no production object-storage provider, signed
URL, public download or malware scanner is introduced by this correction.

A focused re-review of the above correction (M93-02) found that an object
already recorded as `ChecksumFailed` or `Disposed` was misleadingly reported
as `Expired`, since the original check treated every non-`Available`
disposition alike. `PrivateFileAccessOutcome` now has a dedicated `Disposed`
classification, and both `ReadAsync` and `OverwriteAsync` report a
previously recorded `ChecksumFailed` or `Disposed` disposition with its
exact classification through a single shared evaluation path. Separately,
the filename policy was found to over-reject: an embedded `".."` substring
in an otherwise-safe filename (e.g. `report..final.txt`) is no longer
rejected now that path separators alone are sufficient to block real
traversal (L93-01), and U+200C/U+200D (ZWNJ/ZWJ) -- which have legitimate
Arabic-script shaping uses -- were removed from the rejected code-point list,
which was never intended to cover them.

### ADR-018 — Testing environments, SQL Server harness, and production-like gates

| Field | Decision |
|---|---|
| Status | Approved foundation test strategy; production equivalence remains deferred |
| Date | 4 August 2026; reconciled 7 August 2026 (MESP-94 validation-tooling correction) |
| Owners | Solution Architecture / Test Engineering |
| Related Jira | MESP-64, MESP-48, MESP-50, MESP-94 |
| Supersedes | None |

#### Context

The Foundation persistence seam is provider-neutral in its public contracts,
but SQL Server-specific behavior still needs evidence before later ERP modules
add physical schemas and migrations. Docker is not available on the current
developer machine. SQL Server LocalDB is installed and supports the machine,
so the one-developer baseline needs a deterministic, disposable option without
ever connecting to a production or shared database.

MESP-123 B2 adds a separate owner-managed local Development database path. It
is intentionally not the disposable test database described by this ADR.

#### Decision

1. MESP-64 uses SQL Server LocalDB instance `MSSQLLocalDB` for the current local
   harness. A run receives a unique database name with the
   `MiniErpFoundation_` prefix and uses Windows integrated authentication.
   No password, token, connection string, or production endpoint is committed.
2. The test fixture creates the run database from `master`, creates only the
   mapped foundation tables and test-only probe tables, and drops the database
   in fixture cleanup. A connection is accepted only when it targets the
   LocalDB instance and the required disposable name prefix; an unset or
   unsafe connection fails closed.
3. The harness separates provider-specific evidence (SQL Server schema,
   unique-index composition, `rowversion`, collation/Unicode round-trip and
   transaction behavior) from provider-neutral contract evidence (Tenant
   context, ownership, authorization path and durable-work contracts).
4. The repository command `scripts/validate-foundation.ps1` is the single
   canonical Foundation validation entry point (MESP-94 M-14). It discovers
   `SqlLocalDB.exe`/`sqlcmd.exe` dynamically but boundedly — PATH first, then
   a probe of only the known `<version>\Tools\Binn` and
   `Client SDK\ODBC\<version>\Tools\Binn` layouts under Program Files, never
   a full recursive scan of the (potentially large) SQL Server database-
   engine tree; no SQL Server release/version is ever hard-coded (MESP-94
   M-15). The automatic LocalDB instance is scoped by Windows user, not by
   logon session, so a named mutex coordinates every validation run for the
   same Windows user across sessions -- a Global-namespace name suffixed
   with the current user's SID, ACL-restricted to that SID, so it neither
   serializes unrelated Windows users nor lets one open or signal another's
   lock (MESP-94 F1). An abandoned lock from a prior run that terminated
   unexpectedly is recovered rather than treated as an ordinary competing
   run (MESP-94 F2). It starts LocalDB,
   removes any stale disposable database left by an interrupted prior run,
   supplies a fresh disposable connection string to the test process, runs
   backend restore/build/the full backend regression (including the targeted
   SQL Server suite and the safety-catalogue validator), the Angular unit
   tests and production build, the Playwright Foundation journeys, `npm
   audit`, `git diff --check` against the working tree, and `git diff --check
   origin/main...HEAD` against the live branch delta (MESP-94 R2). The
   fixture owns per-run database cleanup even when a test fails; the script
   additionally proves, in a `finally` block that always runs, that zero
   `MiniErpFoundation_*` databases remain on the instance (MESP-94 M-6), and
   restores the environment variable in its own nested `finally` guaranteed
   regardless of any other step's failure (MESP-94 R3). Every step fails the
   command closed.
5. Docker/Testcontainers remains a CI-compatible option to be introduced only
   through a separately approved change. This ADR does not claim that LocalDB
   is production-equivalent, nor does it select a production SQL topology.

#### B2 local `MESP` cutover boundary

When explicitly configured, normal exact-Development startup uses the local
SQL Server `MESP` database and formal module migrations. The bounded cutover
utility imports the existing module-owned SQLite data after an inventory and
empty-target check, creates a recoverable local backup, and verifies row counts,
IDs, Tenant IDs, foreign-key lineage, and source hashes. The source SQLite
files remain available for rollback/reference. The utility refuses a target
database other than `MESP` and never runs in production.

This path does not replace MESP-64: `scripts/validate-foundation.ps1` still
creates a disposable `MiniErpFoundation_*` LocalDB database, exercises the
provider safety assertions, and cleans it. The canonical run now passed the
complete Release validation at 752/752, including the SQL Server safety suite,
while leaving zero disposable databases. Neither result proves production
equivalence, deployment identity, sizing, HA/DR, backup/restore, or the
MESP-48/MESP-50 gates.

#### Connection variable separation (Post-B2 cutover clarification)

Two environment variables serve distinct, non-interchangeable purposes:

- **`MESP_SQLSERVER_CONNECTION_STRING`** — the persistent MiniERP application
  runtime connection for local `Development`. It targets SQL Server `.` /
  database `MESP`. This variable is owned by the application host and must
  never be read or overwritten by the disposable safety harness.

- **`MESP_SQLSERVER_SAFETY_CONNECTION_STRING`** — the disposable SQL safety-test
  connection. It must target `(localdb)\MSSQLLocalDB` with a
  `MiniErpFoundation_[A-Za-z0-9_]+` database name. This variable is set
  in process memory only by `scripts/validate-foundation.ps1` and
  `scripts/Test-MiniErpBackend.ps1` and is cleared in their `finally` blocks.

The safety fixture (`SqlServerSafetyFixture.InitializeAsync`) reads only
`MESP_SQLSERVER_SAFETY_CONNECTION_STRING` and fails closed on null/missing,
non-LocalDB server, `MESP` database name, or any non-`MiniErpFoundation_*`
name. A silent fallback to the runtime variable is prohibited; the safety
harness rejects it if the runtime variable is present and points at `MESP`.

#### Fixture lifecycle and limitations

- The test fixture creates an isolated database per execution and uses unique
  Tenant, record, work and event identifiers. Test-only durable-work probes are
  created under the `test` schema and are never production models or migrations.
- The fixture verifies rollback, duplicate `(TenantId, EventId)` handling,
  single-owner lease claims, query-filter evaluation per context, stored-owner
  update/delete denial, same-Tenant relationship guards, index shape,
  concurrency and Unicode storage.
- LocalDB is a developer validation provider. It does not prove production
  sizing, throughput, failover, backup/restore, high availability, network
  isolation, deployment identity, regional residency or operational alerting.
- A missing LocalDB instance, unsafe connection, failed assertion or failed
  cleanup is a failed validation, never a warning or a skipped test.

#### Provider-neutral versus provider-specific evidence

Provider-neutral architecture and SQLite tests continue to prove the public
Tenant-bound contracts and safe denial shape. SQL Server tests prove only the
semantics that require SQL Server. Neither provider grants an ordinary caller
an `IgnoreQueryFilters`, raw SQL, bulk or maintenance escape hatch.

#### CI and one-developer repeatability

The command is intentionally usable by one developer on a Windows machine with
LocalDB. CI may later run the same test class with an isolated SQL Server
container/Testcontainers adapter, but that infrastructure is deferred and is
not silently substituted in this release. The test report records the provider,
database name prefix, commit and assertion-catalogue version without publishing
Tenant target data or credentials.

#### Production gates and non-decisions

MESP-48 remains the owner of supported volume, throughput, queue depth, lease,
recovery and capacity thresholds. MESP-50 remains the owner of provider/vendor,
retention, residency, privacy, legal hold, purge, backup and restoration
decisions. This ADR authorizes no production migration, purge, retention
execution, performance claim, provider selection or database-per-Tenant shape.

#### Evidence produced by MESP-64

`docs/96_Foundation_Release1_Safety_Validation.md` records the exact 75
assertion catalogue, the applicable local evidence, safe Not Applicable
explanations for later-domain behavior, and the MESP-48/MESP-50 gates. The
report is a foundation checkpoint, not a production-readiness approval.

### ADR-019 — Tenant Host Resolution, Operational Workspace Context, and Configured Branding

**Status:** Accepted and Implemented; squash-merged to `main` at commit
`866cb75bb7d0d97c929216b1a449f458a2614097` (PR #67) following independent Claude Opus 5 approval
**Date:** 17 August 2026\
**Decision owner:** Product Owner / Mini ERP SaaS Platform\
**Primary Jira:** MESP-143\
**Related Jira:** MESP-2, MESP-4, MESP-12, MESP-67, MESP-77, MESP-123

#### Implementation note — 17 August 2026

The bounded host-resolution, Tenant-authority, Overview-first, operational
Company/Branch context, generic branding, and SAR-presentation seams described
by this decision are **fully implemented, independently reviewed by Claude
Opus 5 (APPROVE FOR MERGE), and merged to `main`** at commit
`866cb75bb7d0d97c929216b1a449f458a2614097` (PR #67).

Four non-blocking P3 follow-ups are carried forward:
- P3-1: Cross-host session continuity (`__Host-` cookie boundary between `mesp.com` and `*.mesp.com`; future cross-host SSO design item before production multi-host cutover).
- P3-2: Duplicate active membership invariant (enforce/test invariant explicitly or handle duplicate membership fail-closed).
- P3-3: OpenAPI operation summary quality (`auth.operational-context-switch` summary polish).
- P3-4: Canonical-host ambiguity (enforce single canonical host per Tenant startup constraint).

Specialist pre-production recommendation:
- GPT-5.6 Terra HIGH specialist security audit recommended before production host/TLS/proxy cutover.

Planned future production concerns (production DNS/TLS automation, cross-host SSO, real custom domain routing) remain future work before production cutover.

#### 1. Context

The current foundation UI exposes a pre-ERP “Choose a workspace” flow where the selected item is effectively the Tenant itself (for example, `Wafra · Tenant membership`). This is temporary foundation plumbing, not the target SaaS ERP experience.

For a normal user of one Tenant, the Tenant is not a business choice to make on every login. It is a security/data-isolation boundary that must be resolved and authorized before Tenant business data is loaded. A normal Wafra user must not need to know that other Tenants exist and must never be offered unrelated Tenant names or identifiers.

The platform backlog also uses **Tenant Workspace** for the Platform Administration control-plane surface (MESP-67). That is distinct from an ordinary ERP user's operational working context inside a Tenant.

The Owner has added a Wafra branding/logo asset and a Saudi Riyal symbol asset under `frontend/assets`. They are configuration/country-pack inputs, not authorization or customer-specific product rules.

#### 2. Decision

##### 2.1 Tenant and Workspace are distinct

Canonical model:

```text
MESP Platform
└── Tenant (security/data-isolation boundary)
    └── Company / Legal Entity
        └── Branch / bounded operational context
            └── ERP working context where applicable
```

- **Tenant** is server-authoritative and is the SaaS isolation boundary.
- **Operational Workspace / Context** is inside an already-authorized Tenant and should align with approved Company/Branch scope rather than invent a parallel authorization hierarchy.
- **Platform Tenant Workspace** (MESP-67) remains a separate control-plane concept and must not imply Tenant ERP business authority.

##### 2.2 Tenant-specific host is the normal ERP entry

A Tenant may have one or more verified host bindings. A canonical host may look like:

```text
wafra.mesp.com
```

The host resolves a **candidate Tenant** only. It does not grant authority.

```text
wafra.mesp.com
→ authenticate
→ resolve host binding to candidate Tenant
→ verify exact-Tenant membership/access
→ establish server-owned Tenant context
→ load Wafra Overview
```

An authenticated user without Wafra access receives a safe denial and no Wafra business data.

##### 2.3 Common host is a routing entry

Illustrative `mesp.com` behavior:

- exactly one authorized Tenant → redirect directly to its canonical host;
- multiple authorized Tenants → show only those legitimate memberships, then redirect;
- zero memberships → safe no-access/onboarding state;
- never expose an unrelated Tenant catalogue to an ordinary user.

Raw Tenant GUIDs are not user-facing selectors.

##### 2.4 Platform Administration is a separate control plane

Use a separate platform-admin surface/host (illustratively `admin.mesp.com`).

Platform Administrator authority is not Tenant business authority.

The control plane may expose purpose-bound administrative metadata such as Tenant identity, lifecycle, plans, entitlements, limits, branding governance, support access, exports/offboarding, and audit.

Any support/platform-admin entry into Tenant ERP data must use an approved exact-Tenant membership or bounded support grant and must be attributable/audited.

##### 2.5 Operational Workspace selection occurs after Overview and only when needed

Do not force ordinary Tenant users through a Tenant/Workspace chooser before Overview.

- one permitted operational context → auto-select;
- multiple permitted operational contexts → header/application-context selector;
- remove mandatory `Switch workspace` from ordinary-user primary navigation;
- `/app/workspaces` may remain for management/discovery, but not as a login gate;
- header should distinguish Organization/Tenant from Company/Branch/operational context;
- no normal flow requires typing Tenant/Company/Branch/Workspace GUIDs.

##### 2.6 Host binding is configuration-led and custom-domain ready

Do not hard-code `{tenant}.mesp.com` as the only model.

Introduce a generic host-binding abstraction (working name `TenantHostBinding`) capable of approved aliases/custom domains later.

```text
wafra.mesp.com       → Tenant Wafra
erp.wafra.example    → Tenant Wafra
customer-b.mesp.com  → Tenant Customer B
```

Host configuration must be validated, auditable, and collision-safe. Production DNS/TLS automation is separate infrastructure scope.

##### 2.7 Wafra branding is Tenant configuration

The owner-added Wafra logo asset under `frontend/assets` may be associated with Wafra through a generic Tenant branding profile.

Required behavior:

- inventory the exact owner-added filename(s) before implementation;
- Tenant branding is configuration/data, never a `Wafra` code branch;
- missing/rejected/unavailable Tenant branding falls back to MESP platform branding;
- branding never changes authorization, Tenant context, workflow, tax, numbering, or navigation permission;
- provide alt/accessibility and EN/AR, RTL/LTR, light/dark/fallback behavior;
- do not rename, recolor, re-encode, replace, or delete owner source assets without explicit approval.

Recommended visual ownership:
- `wafra.mesp.com`: Wafra Tenant logo may be primary in the Tenant ERP shell, with MESP as secondary/powered-by platform identity if desired.
- `mesp.com` and `admin.mesp.com`: MESP platform branding remains primary.

##### 2.8 Saudi Riyal symbol is Saudi/SAR presentation

The owner-added Saudi Riyal symbol asset under `frontend/assets` is a Saudi country-pack/currency-presentation asset.

It is not Wafra branding and not a global currency rule.

- SAR remains the currency identity; symbol rendering is presentation only.
- No FX conversion, tax rule, accounting meaning, or persisted amount changes.
- Non-SAR currencies retain their own configured presentation.
- Safe text fallback such as `SAR` remains available.
- In multi-currency/comparison/audit/export contexts, preserve an unambiguous currency code even when a symbol is rendered.
- Validate EN/AR, RTL/LTR, screen, print/document, sizing/alignment, and accessibility.
- Inventory the exact owner-added filename(s) and preserve source assets unchanged.

This complements MESP-12/MESP-37 and does not bypass their regulatory/accounting gates.

#### 3. Security invariants

1. Host resolution produces candidate Tenant context only.
2. Authentication plus exact-Tenant authorization is required before Tenant business data.
3. Client-provided Tenant identifiers cannot expand scope.
4. Cross-Tenant access is denied by default.
5. Tenant/organization ownership remains enforced on all reads/writes.
6. Platform Admin role alone grants no Tenant ERP access.
7. Support/admin entry is explicit and audited.
8. Forwarded-host/host handling must trust only configured proxies and resist spoofing/misrouting.
9. Branding and country presentation never influence authorization.

#### 4. UX consequence

Temporary foundation flow:

```text
Login → Choose workspace/Tenant → ERP
```

Target Tenant-user flow:

```text
Tenant host
→ Sign in
→ exact-Tenant membership verified
→ Tenant Overview
→ auto-select one operational context OR header-switch among permitted contexts
→ ERP modules
```

Target Platform Admin flow:

```text
admin.mesp.com
→ Platform Overview / Tenant Catalogue
→ administrative Tenant Workspace (MESP-67)
→ optional separately authorized/audited Tenant ERP entry
```

The Tenant Overview should evolve toward implemented business status/tasks/alerts instead of foundation/session diagnostics as primary content.

#### 5. Delivery sequence

1. Close MESP-123 Opus findings F-1/F-2/F-5 and targeted re-verification.
2. Merge/close MESP-123 only after Owner/GPT-5.6 Sol decision.
3. Activate MESP-143 before broad additional Tenant-facing UI expansion.
4. Coordinate MESP-143 with:
   - MESP-65 / MESP-66 / MESP-67 — Platform control plane;
   - MESP-77 — Tenant branding;
   - MESP-12 / MESP-37 — Saudi country pack and SAR presentation.
5. Continue downstream procurement UI against the corrected Tenant/Workspace model.

#### 6. Non-goals

This ADR does not itself implement:

- production DNS/TLS provisioning;
- subscription billing;
- new support impersonation mechanisms;
- Purchase Order / receipt / invoice / AP / accounting / payment / stock effects;
- statutory ZATCA/FATOORA behavior;
- customer-specific forks;
- a second Workspace authorization hierarchy separate from Company/Branch.

#### 7. Required implementation validation

MESP-143 must cover:

- valid/unknown host resolution;
- unauthorized user on a valid Tenant host;
- single-membership automatic routing;
- bounded multi-membership chooser;
- no unrelated Tenant enumeration;
- Platform Admin versus Tenant ERP authority;
- single-workspace auto-selection;
- multi-workspace header switching;
- Company/Branch scope preservation;
- EN/AR and RTL/LTR;
- accessibility/keyboard behavior;
- Tenant branding fallback and no-Wafra-branch tests;
- SAR/Riyal-symbol presentation plus text fallback;
- host/proxy spoofing tests;
- regression for existing procurement routes/Tenant isolation.

#### 8. Asset inventory note

The Owner reports new Wafra-logo branding and Saudi Riyal symbol files locally under `frontend/assets`.

At the time this ADR was prepared, their exact new filenames were not visible in the current remote branch. Do not guess them. The implementation executor must inventory the local working tree first and preserve the owner-managed files exactly.
