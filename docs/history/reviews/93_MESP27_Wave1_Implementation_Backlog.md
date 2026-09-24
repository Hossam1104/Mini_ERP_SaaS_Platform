# MESP-27 Wave 1 Implementation Backlog

## Document control

| Field | Value |
|---|---|
| Artifact | Proposed implementation backlog for SaaS Platform Administration Wave 1 |
| Status | Synchronized to Jira. Historical as of 6 August 2026: the Technical Enablers TE-01 through TE-08 (MESP-57 through MESP-64) are **Done and merged**; User Stories US-01 through US-21 (MESP-65 through MESP-85) remain **To Do**. See [`.ai/CURRENT_STATE.md`](../.ai/CURRENT_STATE.md) for the verified current position. |
| Authority | Approved MESP-27 BRD v0.10 and approved Wave 1 wireframes |
| Product boundary | Release 1 B2B ERP platform administration only |
| Owner for approval | Hossam |
| Jira Epic | MESP-2 |
| Technical Enablers | TE-01 through TE-08 mapped to MESP-57 through MESP-64 |
| User Stories | US-01 through US-21 mapped to MESP-65 through MESP-85 |
| Sprint | Not created — requires manual Jira setup |
| Current implementation item | MESP-57 / TE-01 — To Do |
| MESP-28 | To Do |
| Jira action | Existing Epic MESP-2 reused; approved Enablers and Stories are synchronized; no Sprint created |
| Downstream work | MESP-28 and all Procurement, Inventory, Finance, Sales, and Retail POS work remain out of scope |

This is a backlog-authoring and synchronization artifact. It does not authorize coding, create a Sprint in Jira, or replace the owning BRDs and decisions named below.

## 1. Source map and extraction rules

### Approved sources

| Alias | Source | Use in this backlog |
|---|---|---|
| BRD | `docs/11_SaaS_Platform_Administration_BRD.md`, v0.10 | Business scope, rules, requirements, states, permissions, audit, acceptance evidence, dependencies |
| ARCH | `docs/01_Technology_Architecture_Baseline.md`, Approved Architecture Baseline | Modular Monolith structure, Angular/.NET/SQL boundaries, API, authentication, jobs, files, telemetry, and testing enablers |
| WF-01 | `wireframes/Wireframes - Overview & Catalogue.dc.html` | Platform Overview and Tenant Catalogue, states, filters, pagination, EN/AR presentation |
| WF-02 | `wireframes/Wireframes - Provisioning.dc.html` | Create Tenant wizard, validation, progress, retry, and provisioning operations |
| WF-03 | `wireframes/Wireframes - Tenant Workspace & Lifecycle.dc.html` | Tenant Workspace, lifecycle, suspension, reactivation, and high-risk isolation |
| WF-04 | `wireframes/Wireframes - Plans, Entitlements & Modules.dc.html` | Plan, Subscription, Entitlement, module activation, limits, and branding screens |
| WF-05 | `wireframes/Wireframes - Support & Audit.dc.html` | Support authorization, active-session monitor, and Platform Audit |
| WF-06 | `wireframes/Wireframes - Offboarding & Purge.dc.html` | Export, offboarding, legal hold, purge review, cooling-off, and certificate |
| WF-07 | `wireframes/Wireframes - Layout Alternatives.dc.html` | Recommended Tenant Workspace spine (Alternative B) and high-risk action isolation |

### Decomposition rules

- The BRD is the authority for behavior. A wireframe contributes interaction evidence and screen coverage; it does not approve new business rules.
- No value shown as illustrative in a wireframe becomes a production default. This includes sample limits, support duration, export expiry, cooling-off duration, retention periods, backup cycles, tenant counts, and dates.
- MESP-48 remains the owner of reference volumes and supported-volume validation. MESP-50 remains the owner of residency, retention, legal hold, support-access duration, subprocessors, backup treatment, purge timing, and production offboarding policy.
- Identity and Access, Multi-Tenancy, and Organization details remain dependencies on MESP-28, MESP-29, and MESP-30. Their behavior is not redefined here.
- No story introduces Retail POS, finance posting, procurement, inventory execution, B2B Sales behavior, or a tenant's ERP workspace.
- No story is a subtask. US-01 through US-21 are the approved backlog identifiers mapped to Jira MESP-65 through MESP-85.

## 2. Jira Epic mapping

> The proposed documentation Epic was mapped to the existing Jira Epic MESP-2 to prevent duplicate Epic creation.

### MESP-2 — SaaS Platform Administration Wave 1

**Jira mapping:** Existing Epic MESP-2 (reused; no duplicate Epic created)

**Epic outcome:** A Platform Administrator can operate the approved SaaS administration surface for a Tenant from controlled onboarding through lifecycle management, Plan/Entitlement visibility, support authorization, audit evidence, export, and governed offboarding, with strict tenant isolation and no hidden high-risk shortcuts.

**In-scope capability groups:**

- Platform shell, overview, Tenant Catalogue, and Tenant Workspace.
- Controlled Tenant onboarding, duplicate review, provisioning progress, safe retry, and handover.
- One production Release 1 Plan, Subscription history, Entitlement visibility, module readiness, configurable usage visibility, and branding/document identity.
- Lifecycle transitions, suspension/reactivation, support access, Platform Audit, export/offboarding, purge review, and truthful purge certification.
- Arabic/English presentation, RTL behavior, accessibility signals, auditability, correlation, and critical-flow validation.

**Explicitly excluded from the Epic:**

- MESP-28 Identity and Access implementation, except for the minimum contract/seam required to enforce Platform/Tenant authorization.
- Detailed MESP-29 Multi-Tenancy, MESP-30 Organization, Finance, Procurement, Inventory, B2B Sales, Saudi compliance, migration, and external integration behavior.
- Retail POS, trials, automated subscription invoicing, metered billing, pricing-engine behavior, entitlement overrides, microservices, Kubernetes, and database-per-tenant infrastructure.

**Epic completion evidence:**

1. Every included story has BRD and/or approved wireframe traceability.
2. Platform-owned metadata and Tenant-owned data remain separated.
3. Cross-Tenant Platform registers are denied to Tenant actors.
4. High-risk lifecycle, support, export, and purge actions are authorized, audited, and reversible where the BRD permits.
5. Critical browser/API journeys and backend invariants pass through the approved xUnit and Playwright validation approach.
6. Open MESP-48 and MESP-50 decisions are not silently converted into implementation defaults.

## 3. Minimum technical enablers

These are the smallest technical foundations needed to implement the stories safely. They are proposed backlog records, not Jira subtasks.

Estimates below are provisional planning points for decomposition only; they are not source commitments or Jira estimates.

| ID | Proposed enabler | Estimate | Minimum outcome / acceptance evidence | Dependencies | Sprint 1 |
|---|---|---:|---|---|---|
| TE-01 | Modular Monolith solution and module seam | 3 | The approved backend/frontend structure exists conceptually with a Platform Administration boundary, explicit contracts, and no direct cross-module table mutation. Architecture tests are planned for forbidden references. | ARCH §§4–6; later implementation entry gate | Yes |
| TE-02 | Shared SQL persistence and tenant-context guard | 5 | EF Core 10/SQL Server 2025 persistence conventions, module-owned schema ownership, trusted Tenant context, deny-by-default query/command guard, and isolation test fixture are defined. Database-per-tenant and per-Tenant schemas are not introduced. | ARCH §§4, 7; MESP-29; BRD M27-REQ-001/004, RULE-001/002 | Yes |
| TE-03 | Authentication and authorization seam | 5 | ASP.NET Core Identity, secure HTTP-only first-party cookie session, policy evaluation seam, Platform actor versus Tenant actor scope, and separate export authorization checkpoints are represented without implementing MESP-28 behavior. | ARCH §2; MESP-28; BRD M27-REQ-045–049, 071–073, 095 | Yes |
| TE-04 | REST/OpenAPI, error, correlation, and idempotency foundation | 3 | Versioned API contract conventions, safe validation/error envelope, stable request/correlation identity, optimistic concurrency approach, and idempotent command shape support provisioning, export, lifecycle, and audit flows. | ARCH §§2, 5; BRD M27-REQ-010/011/026/065/080 | Yes |
| TE-05 | Durable work, notification, and private-file adapters | 5 | SQL-backed work records/hosted-worker seam, retryable notifications, private object-storage adapter, export artifact metadata, expiry, and checksum/integrity fields are defined. No broker or search cluster is added. | ARCH §§1–3; MESP-50; BRD M27-REQ-059/063/076 | No — needed before async stories |
| TE-06 | Immutable audit and OpenTelemetry evidence | 3 | Common audit event contract, actor/Tenant/scope/correlation fields, authorized audit retrieval/export, structured logs, metrics, traces, freshness/data-as-of fields, and high-risk event taxonomy are defined. | ARCH §§2–3; MESP-38; BRD M27-REQ-007/074/075/078 | Yes |
| TE-07 | Angular Wave 1 shell, component, and RTL baseline | 5 | Angular 22 standalone feature-route shell follows WF-07 Alternative B: Catalogue → tabbed Tenant Workspace; status uses icon/text, high-risk actions are isolated, EN/AR direction and LTR-embedded identifiers are supported, and accessibility-safe defaults exist. | ARCH §6; WF-01/WF-03/WF-07; BRD M27-REQ-081/082 | Yes |
| TE-08 | Local and critical-flow test harness | 3 | Docker Compose local dependency profile, xUnit unit/integration/architecture test conventions, Playwright TypeScript browser/API fixtures, and a Restricted Validation Plan fixture for entitlement-denial tests are defined. | ARCH §§1–2, 8; MESP-48; BRD AC-009/041/042 | Yes |

**Enabler guardrail:** TE-03 must not become an unapproved replacement for MESP-28. It supplies only the contract needed by this Epic; session lifetime, MFA, access-scope detail, and full Identity behavior remain MESP-28/MESP-38 decisions.

## 4. Sequenced user stories

The sequence is dependency order. Provisional planning-point estimates are recorded in the enabler and Story quality tables for the requested recalculation; dates, capacity, assignees, and sprint commitments are not invented.

| Seq. | ID | Story summary | Primary source trace | Earliest safe position |
|---:|---|---|---|---|
| 1 | US-01 | Platform Overview and exception navigation | BRD M27-REQ-010/078/079; WF-01 Screen 1 | Sprint 1 after TE-01–04, 06–07 |
| 2 | US-02 | Tenant Catalogue with bounded views and states | BRD M27-REQ-001/004/067/070/071/072/097; WF-01 Screen 2 | Sprint 1 |
| 3 | US-03 | Tenant Workspace read-only spine | BRD M27-REQ-004/010/078; WF-03 Screen 5; WF-07 Alternative B | Sprint 1 |
| 4 | US-04 | Create Tenant draft wizard | BRD M27-REQ-003/024/030/093/094; WF-02 Screen 3 | After MESP-29/30 input contracts |
| 5 | US-05 | Duplicate, completeness, and review gate | BRD M27-REQ-025/069/070; AC-004; WF-02 Screen 3 | After US-04 and TE-07 |
| 6 | US-06 | Idempotent provisioning run and operations | BRD M27-REQ-026–029/065/066/076; AC-001–003; WF-02 Screens 3–4 | After US-05, TE-05, and TE-07 |
| 7 | US-07 | Plan Catalogue and version history | BRD M27-REQ-016/019; MESP-52; WF-04 Screen 7 | After TE-02–04, 06–07 |
| 8 | US-08 | Subscription change preview and effective dating | BRD M27-REQ-017/018; RULE-004/005; AC-005/006; WF-04 Screen 7 | After US-07 |
| 9 | US-09 | Entitlement visibility and denial proof | BRD M27-REQ-020–024/092; AC-007–010/042; WF-04 Screen 8 | After US-08 and TE-03/07 |
| 10 | US-10 | Module readiness, dependency block, and rollback evidence | BRD M27-REQ-031–035; RULE-008/009; AC-011–014; WF-04 Screen 8 | After US-09, TE-07, and MESP-30 contracts |
| 11 | US-11 | Limits and Usage visibility | BRD M27-REQ-036–041; AC-015–017; WF-04 Screen 9 | After TE-05/06/07 and MESP-48 evidence; no invented thresholds |
| 12 | US-12 | Feature cohort rollout and rollback | BRD M27-REQ-050–053; AC-024/025; WF-01/03 operational visibility | After TE-03/04/06/07 and approved capability |
| 13 | US-13 | Branding, templates, numbering, and document identity | BRD M27-REQ-042–044/094; AC-022/023; WF-04 Screen 10 | After TE-07 and MESP-30/37 contracts |
| 14 | US-14 | Lifecycle workspace and suspension confirmation | BRD M27-REQ-012/013/054–056; RULE-028; AC-018–020/038; WF-03 Screens 5–6 | After US-03 and TE-03/07 |
| 15 | US-15 | Reactivation and interrupted-work review | BRD M27-REQ-057/058; AC-021; WF-03 Screen 6 | After US-14 and TE-05/07 |
| 16 | US-16 | Support authorization and active-session monitor | BRD M27-REQ-045–049/095; AC-026–029; WF-05 Screens 11–12 | After TE-03/07, MESP-28/38, and MESP-50 duration decision |
| 17 | US-17 | Platform Audit dashboard and authorized evidence export | BRD M27-REQ-074/075/078/084; AC-028/038; WF-05 Screen 13 | After TE-06/07 and US-14/16 |
| 18 | US-18 | Bounded Tenant export and artifact integrity | BRD M27-REQ-059/065/067/068/076/077; AC-029/030; WF-06 Screen 14 | After TE-03/04/05/06/07 and MESP-50 export policy |
| 19 | US-19 | Offboarding disposition and termination gate | BRD M27-REQ-060/061; AC-031; WF-06 Screen 14 | After US-18 and MESP-50 policy |
| 20 | US-20 | Purge review, cooling-off, and truthful certificate | BRD M27-REQ-062–064/096; AC-032–035; WF-06 Screens 15–16 | After US-19; last high-risk workflow; blocked by MESP-50 |
| 21 | US-21 | EN/AR localization and RTL completion across Wave 1 | BRD M27-REQ-077/081/082; AC-036/037; all WF mirrors | Cross-cutting acceptance overlay; Sprint 1 slice only, completion before Epic exit |

### US-01 — Platform Overview and exception navigation

**User story:** As an authorized Platform Administrator, I want a Platform Overview that links every operational figure to its bounded register or workspace so that I can act on exceptions without relying on static vanity metrics.

**Acceptance criteria:**

- Overview cards link to the relevant Tenant Catalogue, provisioning operations, lifecycle, support-session, limits, export/offboarding, purge, or audit view.
- Each card exposes scope, data-as-of/freshness, and an authorized drill-through; stale or asynchronously prepared data is visible as such.
- Platform/Tenant authorization is enforced server-side; Auditor and Support Operator views are read-only and scoped.
- Empty, loading, error, no-results, and restricted states are usable and do not reveal cross-Tenant information.

**Trace:** BRD M27-REQ-010/078/079; WF-01 Screen 1.

**Dependencies:** TE-01/02/03/04/06/07; MESP-29 isolation and MESP-38 evidence contracts.

**Not included:** Invented KPI thresholds, production volume promises, or downstream ERP module dashboards.

### US-02 — Tenant Catalogue with bounded views and states

**User story:** As an authorized Platform actor, I want to search and filter the Tenant Catalogue so that I can locate a Tenant and open its governed workspace without unsafe row-level high-risk actions.

**Acceptance criteria:**

- Search/filter supports the wireframe fields: Tenant identity/code, lifecycle, country, Plan, risk, and saved views where the owning contract permits.
- The catalogue shows stable identity, bilingual name, country, legal-entity count, Plan, Subscription, module status, usage, last activity, provisioning state, risk, and an Open action.
- Large results paginate server-side and never truncate silently; no sample count becomes a production assumption.
- Suspend and Purge are not row actions; they are reached through the Tenant Workspace and governed confirmation flows.
- Restricted users receive a safe denial and no cross-Tenant register data.

**Trace:** BRD M27-REQ-001/004/010/067/070–072/097; WF-01 Screen 2.

**Dependencies:** TE-02/03/04/06/07; MESP-29 isolation contract.

### US-03 — Tenant Workspace read-only spine

**User story:** As a Platform Administrator, I want one Tenant Workspace with explicit tabs so that ordinary Tenant review is separated from high-risk lifecycle actions.

**Acceptance criteria:**

- Opening a catalogue row lands on a Tenant-scoped Workspace showing bilingual identity, stable code, lifecycle, country, legal-entity count, Plan, active-module summary, and recent activity.
- The tab structure follows the approved spine: Overview, Lifecycle, Companies, Plan & Subscription, Entitlements & Modules, Limits & Usage, Branding, Support Access, Exports, Offboarding, and Audit.
- The Platform view does not expose Procurement, Inventory, Finance, or other Tenant ERP navigation.
- Lifecycle high-risk actions are reached from the Lifecycle tab, not from a catalogue row.

**Trace:** BRD M27-REQ-004/010/078; WF-03 Screen 5; WF-07 Alternative B.

**Dependencies:** TE-02/03/06/07; MESP-29 and MESP-30 contracts.

### US-04 — Create Tenant draft wizard

**User story:** As a Platform Administrator, I want to save and review a Tenant onboarding draft so that all required identity, locale, organization, administrator, Plan, limits, branding, and hosting inputs are captured before provisioning.

**Acceptance criteria:**

- The wizard captures the BRD-required input categories: Tenant code and identity, bilingual names/contact, country/locale, initial Company/Legal Entity, initial administrator, Plan/Subscription/modules/limits, branding/templates/numbering, contracted hosting region, cross-border support terms, and subprocessor restrictions.
- The current wireframe’s seven-step grouping is treated as review-friendly provisional grouping; steps may be regrouped without inventing or dropping required fields.
- There is no Trial option, no entitlement-override control, and no production assignment of the Restricted Validation Plan.
- Field-level validation state is visible; draft save/cancel/back/edit behavior preserves safe input.
- MESP-50-controlled fields cannot be activated with unresolved or contradictory production policy values.

**Trace:** BRD M27-REQ-003/009/024/030/093/094; MESP-52; WF-02 Screen 3.

**Dependencies:** TE-02/03/04/07; MESP-29, MESP-30, MESP-37, and MESP-50. Keep out of Sprint 1.

### US-05 — Duplicate, completeness, and review gate

**User story:** As a Platform Administrator, I want possible duplicates and missing prerequisites surfaced before submission so that the Platform never silently creates or rejects the wrong Tenant.

**Acceptance criteria:**

- Tenant code and legal/customer identity are checked before authoritative creation.
- Validation distinguishes unique, confirmed duplicate, and possible match requiring authorized review.
- The Review & Confirm view links each summary section back to its source step and blocks submission when mandatory controls fail.
- The review records the input snapshot, validation results, owner, and decision evidence.

**Trace:** BRD M27-REQ-025/029/069/070; AC-004; WF-02 Screen 3.

**Dependencies:** US-04; TE-02/04/06/07; duplicate rules from MESP-29/30.

### US-06 — Idempotent provisioning run and operations

**User story:** As a Platform Operations owner, I want provisioning progress, success, failure, and retry states to be visible so that a failed run can resume safely without duplicate Tenants or usable partial access.

**Acceptance criteria:**

- A submission creates one stable request/correlation identity and visible stage timeline.
- Stages expose Pending, Running, Succeeded, Failed, Skipped, or Compensated with owner, blocker, completed stages, safe retry point, and user-safe message.
- Retry uses the same request identity and cannot create a second Tenant, invitation, Entitlement, or authoritative run.
- Partial provisioning cannot produce Active state, a usable invitation, operational module use, or a false success state.
- Success reaches Ready for Activation with a handover/readiness checklist and zero operational transactions.

**Trace:** BRD M27-REQ-026–029/065/066/076/080; AC-001–003; WF-02 Screens 3–4.

**Dependencies:** US-05; TE-04/05/06/07; MESP-29/30/37 contracts.

### US-07 — Plan Catalogue and version history

**User story:** As a Platform Administrator, I want to view Plan versions and their effective metadata so that the one production Release 1 Plan and non-production Restricted Validation Plan remain distinguishable and auditable.

**Acceptance criteria:**

- The catalogue distinguishes one production Release 1 Plan from the non-production Restricted Validation Plan and never labels the latter as Trial.
- Each Plan version displays status, effective interval, included modules/features, configurable limits, service/support tier, non-calculating price metadata, environment eligibility, owner, and approval evidence.
- Historical versions remain visible and immutable after use; no screen calculates a charge or creates a subscription invoice, payment, or accounting transaction.
- Retail POS is absent and cannot be assigned.

**Trace:** BRD M27-REQ-016/019/023; MESP-52; WF-04 Screen 7.

**Dependencies:** TE-02/03/04/06/07; MESP-52 already approved. Keep out of Sprint 1.

### US-08 — Subscription change preview and effective dating

**User story:** As an authorized Platform/Commercial actor, I want a reviewed preview of a Subscription change so that current Entitlements remain unchanged until the approved effective time.

**Acceptance criteria:**

- Current and scheduled Plan/Subscription records show Tenant, Plan version, status, effective dates, reason, assigner/approver, and history.
- A change is previewed before save, including the expected limit/module impact and effective time.
- Current Entitlements remain unchanged before the effective time; historical evidence is not rewritten.
- A change cannot be represented as a direct per-Tenant Entitlement override.

**Trace:** BRD M27-REQ-017/018/092; RULE-004/005/007; AC-005/006; WF-04 Screen 7.

**Dependencies:** US-07; TE-03/04/06/07; MESP-52.

### US-09 — Entitlement visibility and denial proof

**User story:** As an authorized Platform or Tenant administrator, I want to see the source, effective interval, dependency, restriction, and audit of each Entitlement so that commercial availability is not confused with user Permission.

**Acceptance criteria:**

- Entitlement view shows capability/module, status, effective/expiry interval, source Plan/Subscription, dependency, temporary restriction, and audit link.
- A User with Permission but no effective Entitlement is denied; an Entitlement without User Permission is also denied.
- Security or operational-safety restriction can block an effective Entitlement but never grant one.
- No override control, hidden action, import, job, or integration path can grant a missing Entitlement.
- Restricted Validation Plan denial evidence is executable only in non-production.

**Trace:** BRD M27-REQ-020–024/092; RULE-006/007; AC-007–010/042; WF-04 Screen 8.

**Dependencies:** US-08; TE-02/03/06/07/08; MESP-28/MESP-29 authorization contracts.

### US-10 — Module readiness, dependency block, and rollback evidence

**User story:** As a Platform Administrator, I want a module activation checklist with explicit dependency and rollback evidence so that a module cannot become partially usable or unsafe.

**Acceptance criteria:**

- Checklist shows effective Entitlement, dependencies, configuration readiness, master/opening-data readiness, rollback-plan approval, owner, and activation approval.
- A failed dependency identifies the blocking module/configuration and prevents partial business use.
- Activation is allowed only for Active Tenant + effective Entitlement + ready dependencies/configuration/evidence + approved rollback plan + authorized approval.
- Read-Only and Deactivation Pending are shown as words and preserve data and required access.

**Trace:** BRD M27-REQ-031–035; RULE-008/009; AC-011–014; WF-04 Module Activation.

**Dependencies:** US-09; TE-02/03/06/07; MESP-30 and later domain readiness contracts. Keep out of Sprint 1.

### US-11 — Limits and Usage visibility

**User story:** As an authorized administrator, I want fresh usage and limit status so that warnings are actionable and hard limits affect only the capacity-increasing action.

**Acceptance criteria:**

- Each measure shows scope, unit, current value, source Plan value, measurement time, freshness, and status.
- Warning status notifies authorized administrators without blocking the triggering action.
- Hard-limit status names the blocked capacity-increasing action, preserves existing data/read access, and provides escalation.
- Unknown/stale values do not become invoiceable amounts or invented production thresholds.
- Final thresholds and supported-volume claims remain blocked on MESP-48 evidence.

**Trace:** BRD M27-REQ-036–041; AC-015–017; WF-04 Screen 9.

**Dependencies:** US-07/08; TE-05/06/07; MESP-48. Illustrative wireframe values are not implementation defaults.

### US-12 — Feature cohort rollout and rollback

**User story:** As a Product or Operations owner, I want a controlled cohort rollout for an already-approved capability so that exposure can be limited, reversed, and reconstructed without bypassing Entitlements or authorization.

**Acceptance criteria:**

- A rollout record captures the approved capability, owner, eligible Entitlement, environment, Tenant/cohort scope, start time, success criteria, rollback trigger, and expiry/removal decision.
- Cohort selection is explicit and reviewable; the rollout cannot add scope, grant Permission, bypass an Entitlement, alter posted history, or enable Retail POS.
- Rollback stops new exposure while preserving the interpretation of existing data and the affected Tenant/activity history.
- Expired rollout flags are surfaced for review and are removed or renewed only through an authorized decision; every change is audited.

**Trace:** BRD M27-REQ-050–053; AC-024/025; WF-01 Overview exception/feature visibility and WF-03 Workspace activity evidence. The BRD is the behavior authority; no dedicated rollout screen is inferred from the wireframes.

**Dependencies:** TE-03/04/06/07; an approved capability and Entitlement contract; MESP-28/MESP-38 for final actor scope. No MESP-28 Identity behavior is introduced. Keep out of Sprint 1.

### US-13 — Branding, templates, numbering, and document identity

**User story:** As an authorized Tenant administrator, I want governed bilingual branding and document identity previews so that identity can be configured without changing authorization, tax meaning, audit, or numbering history.

**Acceptance criteria:**

- English/Arabic names, approved logo, accessible colors, contact details, document-template profile, and numbering profile can be reviewed with preview and validation evidence.
- Configuration records owner, Tenant/Company/document scope, Country Pack compatibility, version, effective interval, and history.
- Historical documents retain the template/number interpretation used; issued numbers are never silently changed or reused.
- Missing/rejected branding falls back to approved Platform defaults and does not disguise Platform/security identity.

**Trace:** BRD M27-REQ-042–044/094; AC-022/023; WF-04 Screen 10.

**Dependencies:** TE-02/03/06/07; MESP-30 and MESP-37 contracts. Keep out of Sprint 1 except shell defaults.

### US-14 — Lifecycle workspace and suspension confirmation

**User story:** As an authorized Platform Administrator, I want lifecycle transitions isolated in the Tenant Workspace so that suspension effects are explicit, reviewable, and auditable.

**Acceptance criteria:**

- Lifecycle view shows canonical state, allowed next transitions, blocked reason, history, actor, effective time, and evidence.
- Suspension captures type, scope, reason, authority, effective time, Grace Period decision, access mode, jobs/integrations behavior, notification, review date, and reactivation criteria.
- High-risk confirmation requires typed Tenant-specific confirmation and an exhaustive effect preview; it is not a single default-focused action.
- Security/legal suspension may bypass Grace Period only with the bypass reason recorded.
- Suspended state consistently enforces interactive and non-interactive restrictions without deleting data.

**Trace:** BRD M27-REQ-012/013/054–056; RULE-012/028; AC-018–020/038; WF-03 Screens 5–6.

**Dependencies:** US-03; TE-03/04/06/07; MESP-28/29 and unresolved suspension-policy confirmations. Keep out of Sprint 1.

### US-15 — Reactivation and interrupted-work review

**User story:** As an authorized Platform Administrator, I want reactivation checks and deliberate interrupted-work review so that access is restored only after the suspension reason and dependent work are safe.

**Acceptance criteria:**

- Security, Subscription, Entitlement, module-readiness, failed-job, and integration checks are visible.
- Reactivation remains disabled while any required check or acknowledgment is outstanding.
- Interrupted jobs/actions are reviewed and deliberately restarted; none is assumed complete or silently discarded.
- Reactivation closes/supersedes the reason, reevaluates access, and notifies owners of restored and still-restricted capabilities.

**Trace:** BRD M27-REQ-057/058; AC-021; WF-03 Screen 6.

**Dependencies:** US-14; TE-04/05/06/07; MESP-28/29 and policy decision for read-only/suspension behavior.

### US-16 — Support authorization and active-session monitor

**User story:** As a Security/Support owner, I want named, time-boxed support authorization and an active-session monitor so that support is least-privilege, visible, revocable, and export-blind unless separately authorized.

**Acceptance criteria:**

- Request captures case, Tenant, named user, purpose, scope, sensitivity, start/end, Tenant authorization, Platform approval, and separate export authorization state.
- A support session has a distinct case-labelled banner, one Tenant/scope, automatic expiry, immediate termination, and complete activity evidence.
- Downloads/exports remain blocked at zero unless separate export Permission, export authorization, and explicit Tenant authorization for the named artifact exist.
- Expiring sessions are flagged before lapse; cross-Tenant targeting is denied without revealing the target Tenant.
- Emergency access is not implemented or inferred; it remains separately governed by future approved policy.

**Trace:** BRD M27-REQ-045–049/095; AC-026–029; WF-05 Screens 11–12.

**Dependencies:** TE-03/06/07; MESP-28, MESP-38, and MESP-50 maximum-duration/policy decisions. Keep out of Sprint 1.

### US-17 — Platform Audit dashboard and authorized evidence export

**User story:** As an Auditor or authorized Platform actor, I want a searchable Platform Audit view with data freshness and authorized evidence export so that material actions and denied attempts can be reconstructed without exposing other Tenants.

**Acceptance criteria:**

- Filters include actor, Tenant, action type, risk level, date range, and saved views only within authorization.
- Rows expose trusted time, actor, Tenant/scope, action, object, result, risk, correlation, and drill-down evidence; data-as-of/freshness is visible.
- Denied cross-Tenant attempts are recorded without confirming the targeted Tenant to the unauthorized actor.
- Audit evidence export has its own authorization and is itself audited; Auditor view is read-only.

**Trace:** BRD M27-REQ-074/075/078/084; AC-028/038; WF-05 Screen 13.

**Dependencies:** TE-03/04/06/07; US-14/16; MESP-38. Keep out of Sprint 1 except the audit foundation and read-only shell contract.

### US-18 — Bounded Tenant export and artifact integrity

**User story:** As an authorized Platform/Privacy actor, I want a bounded, integrity-verified Tenant export so that an authorized recipient can receive only the approved scope and expired or unsupported artifacts cannot be used.

**Acceptance criteria:**

- Export records scope, categories, formats, identifiers/relationships, exclusions, data-as-of, requester/approver, artifact, integrity evidence, expiry, download authorization, and downloads.
- Export generation/download is asynchronous or stateful where required, retryable, bounded, and independently authorized.
- A support identity cannot reach export authorization through support scope alone.
- Expired artifacts are inaccessible and require a new authorized request; failed or partial generation remains visible and is not presented as complete.

**Trace:** BRD M27-REQ-059/065/067/068/076/077; AC-029/030; WF-06 Screen 14.

**Dependencies:** TE-03/04/05/06/07; MESP-50 export/retention policy. Keep out of Sprint 1.

### US-19 — Offboarding disposition and termination gate

**User story:** As an authorized Platform/Privacy actor, I want to review the Tenant’s offboarding disposition so that termination cannot proceed until export disposition, open matters, legal hold, Subscription end, access closure, and accountable approval are complete.

**Acceptance criteria:**

- Termination review records the accepted export or explicit waiver, open support/security matters, legal-hold result, Subscription end, access-closure plan, effective time, and responsible approver.
- Waiver is explicit and warns when no accepted/recoverable Tenant copy may remain before purge; the decision is audited and cannot be inferred from an expired artifact.
- A Tenant cannot enter Termination Pending or Terminated until the required disposition and approvals are complete; ordinary Tenant operation is then closed while governed retention remains.

**Trace:** BRD M27-REQ-060/061; AC-031; WF-06 Screen 14.

**Dependencies:** US-18; TE-03/04/05/06/07; MESP-50 export/retention/legal-hold policy. Keep out of Sprint 1.

### US-20 — Purge review, cooling-off, and truthful certificate

**User story:** As a Security/Privacy owner, I want purge approval and certificate evidence bounded to a certified scope so that irreversible operations have dual control, a cooling-off/final-notice gate, and truthful residual-copy disclosure.

**Acceptance criteria:**

- Active legal hold is a hard stop for approval and execution; no override control is exposed.
- Purge approval requires dual control, retention expiry, exact Tenant/certified-scope confirmation, backup treatment, recovery plan, MESP-50-controlled cooling-off interval, final notice, and recheck before execution.
- Execution is blocked until the interval and final notice are complete; approval can be revoked before execution.
- Certificate states certified scope, included/excluded systems/data, residual backups/retained copies, legal-hold/retention restrictions, and whether restoration remains possible outside scope.
- Universal restoration impossibility is never claimed unless all residual copies are demonstrably removed; partial failure is not certified complete.

**Trace:** BRD M27-REQ-062–064/096; AC-032–035; WF-06 Screens 15–16.

**Dependencies:** US-19; TE-04/05/06/07; MESP-50 and qualified privacy/legal/security validation. Last high-risk workflow; excluded from Sprint 1.

### US-21 — EN/AR localization and RTL completion across Wave 1

**User story:** As an English- or Arabic-speaking Platform actor, I want the Wave 1 administration surface to preserve meaning and authority when language and direction change so that RTL is operationally equivalent to LTR.

**Acceptance criteria:**

- All Wave 1 screens provide approved EN/AR labels, validation, warnings, irreversible consequences, and fallback behavior.
- Layout, navigation, tables, drawers, steppers, icons, and state markers mirror as blocks; codes, emails, identifiers, dates, and digit groups remain correctly embedded LTR where required.
- Status is not conveyed by color alone; Read-Only, Warning, Hard Limit, Restricted, and high-risk states use text/icon cues.
- Language/direction changes do not alter authority, scope, state, values, effective times, or evidence.

**Trace:** BRD M27-REQ-077/081/082; AC-036/037; WF-01–WF-06 mirrored screens; WF-07 recommendation.

**Dependencies:** TE-07/08; approved glossary terminology and MESP-37 localization rules. Sprint 1 covers the shell/catalogue slice only; full completion is an Epic exit criterion after the Wave 1 screen outcomes are validated.

## 4A. Mandatory self-review pass 1 — coverage

The table below maps every approved MESP-27 functional section, requirement family, decision, and acceptance-scenario family to at least one proposed backlog item. “Covered — dependency retained” means the backlog carries the behavior or control without inventing the unresolved value; the owner remains explicit.

| MESP-27 section or requirement | Backlog item(s) | Coverage status | Deferred owner, when applicable |
|---|---|---|---|
| §3 Scope and §4 Out of scope; M27-REQ-001–006; M27-AC-039/041/043 | MESP-2; TE-02/03; US-02/03/07/09/10/12 | Covered; B2B administration boundary and exclusions are explicit | MESP-28/29/30 for detailed identity, tenancy, and organization behavior |
| §5 Source requirements and traceability | Source map; traces on TE-01–08 and US-01–21 | Covered; every item points to BRD and/or approved wireframe evidence | MESP-19 for traceability governance follow-up |
| §6 Definitions | Source map; TE-02/03/06; all stories use Tenant, Company/Legal Entity, Branch, Warehouse, Plan, Subscription, Entitlement, Permission, and Feature Flag distinctly | Covered; glossary terms are not redefined | MESP-18 / approved glossary owner |
| §7 Actors and responsibilities; M27-REQ-007/008 | TE-03/06; US-01/02/04/06/12/14/16/17/18/19/20 | Covered; actor, scope, approval, and audit responsibilities remain explicit | MESP-28/38 for final identity, membership, and permission detail |
| §8 Business assumptions; M27-REQ-009 | Decomposition rules; TE-05/06/07; US-04/07/11/12/18/19/20/21 | Covered — dependency retained; illustrative values are not defaults | MESP-37/48/50 and MESP-30 for unresolved business values |
| §9 Business processes; M27-REQ-010/011 | TE-04/05/06; US-01/04/05/06/08/14/15/16/17/18/19/20 | Covered; state, next action, blocker, owner, evidence, retry, and notification are represented | MESP-38/50 for final policy evidence |
| §10 Tenant lifecycle; M27-REQ-012–015 | US-03/06/14/15/18/19/20; TE-03/04/05/06 | Covered — dependency retained; canonical states, guarded transitions, data preservation, and purge truthfulness are explicit | M27-OQ-003/004 and MESP-50 for Grace Period, suspension, retention, and purge values |
| §11 Plan and subscription model; M27-REQ-016–019; MESP-52 | US-07/08; TE-02/03/04/06 | Covered; one production R1 Plan, Restricted Validation Plan, effective dating, audit, and no billing automation are preserved | MESP-52 is approved; MESP-38 for final control evidence |
| §12 Entitlement model; M27-REQ-020–024/092 | US-09; TE-03/06/08 | Covered; denial proof and no-override rule are testable | MESP-28/29 for final authorization and organizational scope |
| §13 Tenant provisioning; M27-REQ-025–030/093 | US-04/05/06; TE-02/03/04/05/06/07 | Covered — dependency retained; draft, duplicate gate, idempotent run, safe retry, handover, and hosting controls are represented | MESP-29/30/37/50 for input contracts and production policy |
| §14 Module activation; M27-REQ-031–035 | US-10; TE-02/03/06 | Covered; readiness, dependency blocking, deactivation/read-only, and rollback evidence are explicit | MESP-30 and later domain readiness contracts |
| §15 Limits and usage; M27-REQ-036–041/090/091 | US-11; TE-05/06/08 | Covered — dependency retained; measures, freshness, warning/hard-limit behavior, and no-threshold rule are explicit | MESP-48 for reference volumes, thresholds, and performance validation |
| §16 Branding; M27-REQ-042–044/094 | US-13; TE-07; TE-02/06 | Covered; governed branding, document identity, fallback, and numbering history are explicit | MESP-30/37 for legal/document rules |
| §17 Support access; M27-REQ-045–049/095 | US-16; TE-03/05/06 | Covered — dependency retained; named/time-boxed scope, revocation, monitor, and export separation are explicit | MESP-28/38 and MESP-50 for final identity and duration policy |
| §18 Feature rollout; M27-REQ-050–053 | US-12; TE-03/04/06/07 | Covered; cohort, Entitlement, expiry, rollback, and audit are explicit | Product/Operations owner for each approved capability; MESP-28/38 for actor scope |
| §19 Suspension and reactivation; M27-REQ-054–058 | US-14/15; TE-03/04/05/06 | Covered — dependency retained; confirmation, bypass evidence, checks, interrupted work, and notification are explicit | M27-OQ-003/004/005; MESP-28/29/38 |
| §20 Offboarding; M27-REQ-059–064/096 | US-18/19/20; TE-04/05/06 | Covered — dependency retained; bounded export, disposition, legal hold, dual control, cooling-off, certificate, and residual-copy truth are explicit | MESP-50 and qualified privacy/legal/security review |
| §21 Business rules; RULE-001–031 | TE-02/03/04/05/06/07; US-01–21 | Covered; rules are carried as acceptance constraints rather than invented workflows | Owning BRD decision where a rule contains an open value |
| §22 State machines; M27-REQ-065/066 | US-06/08/09/10/14/15/16/18/19/20; TE-04/05/06 | Covered; state transitions and terminal/blocked states are demonstrable | MESP-29/30/38/50 for detailed state policy |
| §23 Data requirements; M27-REQ-067/068/097 | TE-02/05/06; US-02/04/05/06/07/08/09/11/13/16/17/18/19/20 | Covered; ownership, scope, evidence, freshness, integrity, and retention metadata are mapped | MESP-29/30/38/50 for authoritative schemas and policy |
| §24 Validation rules; M27-REQ-069/070 | TE-04/08; US-04/05/06/09/10/18/19/20 | Covered; field, duplicate, dependency, state, authorization, and high-risk validation are testable | MESP-29/30/37/50 for final rule catalogues |
| §25 Permissions and authorization; M27-REQ-071–073 | TE-03; US-02/09/14/15/16/17/18/19/20 | Covered — contract-only where MESP-28 owns behavior; server-side scope and denial are explicit | MESP-28/38 |
| §26 Audit requirements; M27-REQ-074/075 | TE-06; US-06/08/09/10/12/14/15/16/17/18/19/20 | Covered; material, denied, support, export, lifecycle, and purge evidence are included | MESP-38 for final retention, access, and immutability controls |
| §27 Notifications; M27-REQ-076/077 | TE-05/06; US-06/11/14/15/16/18/19/20/21 | Covered; in-app/operational evidence, retry, escalation, and localization are explicit | MESP-50 and M27-OQ-007 for production notification policy |
| §28 Reports and KPIs; M27-REQ-078/079 | US-01/02/07/09/11/16/17; TE-06 | Covered; data-as-of/freshness, bounded drill-through, and authorized evidence are explicit | MESP-48 for capacity/volume validation |
| §29 Exceptions and recovery; M27-REQ-080 | TE-04/05; US-05/06/15/18/19/20 | Covered; safe retry, compensation, interrupted work, partial failure, and truthful completion are explicit | MESP-38/50 for operational recovery and purge policy |
| §30 Localization; M27-REQ-081/082 | TE-07/08; US-21; all Wave 1 stories | Covered; EN/AR, RTL, embedded LTR identifiers, accessibility, and status text/icon semantics are explicit | MESP-37 for approved terminology and Country Pack rules |
| §31 Security and privacy; M27-REQ-083/084 | TE-02/03/06; US-02/09/14/15/16/17/18/19/20 | Covered — dependency retained; isolation, least privilege, high-risk control, export separation, and purge truth are explicit | MESP-28/38/50 and qualified production validation |
| §32 Integration requirements; M27-REQ-085–087 | TE-04/05; US-06/12/15/18/19 | Covered at the platform seam; no unapproved external integration is invented | MESP-39 for provider and integration contracts |
| §33 Migration and opening requirements; M27-REQ-088/089 | US-04/05/06/10; TE-02/04 | Covered as controlled opening/configuration evidence only; business-data migration is not silently added | MESP-40 for migration and cutover behavior |
| §34 Non-functional expectations; M27-REQ-090/091 and M27-AC-044 | TE-01–08; US-01/02/03/06/09/11/14/16/17/18/19/20/21 | Covered as enablers, gates, and validation evidence; no production SLA/volume claim is invented | MESP-48, MESP-50, architecture ADRs, and production validation |
| §35 Given/When/Then acceptance scenarios M27-AC-001–044 | US-01–21; TE-04/05/06/08 | Covered; each scenario family is represented by one or more acceptance criteria or a protected exclusion | MESP-28/29/30/37/38/39/40/48/50 for owned dependencies |
| §36 Open questions M27-OQ-002–008 | DEP-01–07; affected stories explicitly held behind gates | Covered — blocked items remain visible and are not guessed | Named owner in each dependency/open-question row |
| §37 Decisions M27-DEC-001–005, MESP-52, MESP-56, PD-019 | Epic exclusions; US-07/08/09/10/13; TE-03/07 | Covered; approved Plan, legal-entity boundary, purge truth, and technology constraint are preserved | Hossam for approval/change control; MESP-30/38/50 for detailed decisions |
| §38 Dependencies | DEP-01–07; story dependency fields | Covered; blocking predecessors and owning BRDs are explicit | Named dependency owner |
| §39 Risks | Epic completion evidence; TE-02/03/05/06/08; US-09/14/15/16/18/19/20 | Covered; mitigation is represented as a control, evidence, or explicit deferral | Hossam, Security/Privacy, Wafra, MESP owners as named in BRD |
| §40 Approval criteria | §7 readiness gates; Pass 1–3 tables; no-unauthorized-Jira-write/no-code guardrail | Covered; approval remains a human gate before implementation | Hossam |

## 4B. Mandatory self-review pass 2 — story quality

Estimates are provisional sizing for decomposition only; they are not Jira commitments. Every Story is at or below 8, has one demonstrable outcome, has testable acceptance criteria, identifies blocking dependencies, and names an approved BRD/wireframe design reference.

| Story | Estimate | One outcome and workflow purity | Independent demonstration | Acceptance criteria testability | Later-BRD / scope guard | Dependencies and design reference | Sequence | Result |
|---|---:|---|---|---|---|---|---:|---|
| US-01 | 3 | One overview-to-exception navigation outcome | Fixture cards link to bounded views and show freshness/restricted states | Card targets, state behavior, scope, and freshness can be asserted | No ERP dashboards or invented KPIs | TE-01–04/06/07; WF-01 | 1 | Pass |
| US-02 | 5 | One bounded Tenant Catalogue outcome | Search/filter/page a fixture register and open one workspace | Fields, server paging, restricted state, and absent row actions are assertable | No high-risk row actions or downstream module data | TE-02/03/04/06/07; MESP-29; WF-01 | 2 | Pass |
| US-03 | 3 | One read-only Tenant Workspace spine outcome | Open a Tenant and inspect tabs, scope, and no-ERP navigation | Identity fields, tabs, isolation, and action location are assertable | No MESP-28 behavior or ERP workspace | TE-02/03/06/07; MESP-29/30; WF-03/07 | 3 | Pass |
| US-04 | 8 | One draft-capture/review outcome; no provisioning execution | Save, edit, and validate a draft without submitting it | Required categories, provisional grouping, validation, and unresolved policy block are assertable | No Trial, override, Restricted Plan production assignment, or implementation detail | TE-02/03/04/07; MESP-29/30/37/50; WF-02 | 4 | Pass |
| US-05 | 5 | One pre-authoritative duplicate/completeness gate | Show unique, duplicate, possible-match, and blocked review fixtures | Outcomes, source links, snapshot, and decision evidence are assertable | No invented duplicate algorithm or data migration | US-04; TE-02/04/06/07; MESP-29/30; WF-02 | 5 | Pass |
| US-06 | 8 | One idempotent provisioning-run/recovery outcome | Fail a stage, retry the same request, and show no duplicate/Active state | Stage states, identity, safe retry, partial-access prohibition, and handover are assertable | No invitations or business transactions beyond BRD handover evidence | US-05; TE-04/05/06/07; MESP-29/30/37; WF-02 | 6 | Pass |
| US-07 | 5 | One Plan catalogue/version-history outcome | Compare production and Restricted Validation Plan fixtures | Version fields, immutability, eligibility, and no-billing behavior are assertable | No POS, Trial, billing, payment, invoice, or accounting transaction | TE-02/03/04/06/07; MESP-52; WF-04 | 7 | Pass |
| US-08 | 5 | One reviewed effective-dated Subscription-change outcome | Preview current/future change and inspect unchanged current Entitlement | Effective dates, history, preview, and no-override behavior are assertable | No per-Tenant Entitlement override or pricing engine | US-07; TE-03/04/06/07; MESP-52; WF-04 | 8 | Pass |
| US-09 | 5 | One Entitlement/Permission denial-proof outcome | Exercise allowed, missing-Entitlement, missing-Permission, and restricted fixtures | Source, interval, dependency, denial, and non-production guard are assertable | No hidden grant, override, import, job, or integration bypass | US-08; TE-02/03/06/07/08; MESP-28/29; WF-04 | 9 | Pass |
| US-10 | 8 | One module-readiness/activation-gate outcome | Show ready, blocked dependency, Read-Only, and rollback-evidence fixtures | Preconditions, blocker, state, and approval evidence are assertable | No Procurement, Inventory, Finance, Sales, or POS transactions | US-09; TE-02/03/06/07; MESP-30; WF-04 | 10 | Pass |
| US-11 | 5 | One informational usage/limit-control outcome | Show fresh, stale, warning, and hard-limit fixture measures | Scope, unit, freshness, warning/non-block, hard-limit action, and escalation are assertable | No invented MESP-48 threshold or invoiceable amount | US-07/08; TE-05/06/07; MESP-48; WF-04 | 11 | Pass |
| US-12 | 5 | One controlled feature-cohort rollout/rollback outcome | Enable an eligible cohort, inspect audit, then roll back | Cohort, Entitlement, expiry, rollback, and audit fields are assertable | No new capability, Permission, POS, or MESP-28 behavior | TE-03/04/06/07; approved capability; BRD §18 + WF-01/03 | 12 | Pass |
| US-13 | 5 | One governed branding/document-identity outcome | Preview valid/invalid branding and numbering/template versions | Scope, fallback, version, effective interval, history, and accessibility are assertable | No tax meaning, authorization, or numbering-history rewrite | TE-02/03/06/07; MESP-30/37; WF-04 | 13 | Pass |
| US-14 | 8 | One guarded lifecycle suspension outcome | Suspend a fixture with typed confirmation and inspect effects/audit | Transition, reason, bypass, access mode, job behavior, notification, and review date are assertable | No data deletion or guessed Grace Period/suspension policy | US-03; TE-03/04/06/07; MESP-28/29; OQ-003/004; WF-03 | 14 | Pass |
| US-15 | 5 | One reactivation/interrupted-work review outcome | Block on an outstanding check, then clear it and restore deliberately | Checks, acknowledgments, restart review, reevaluation, and notices are assertable | No assumption that interrupted work completed; no emergency access | US-14; TE-04/05/06/07; MESP-28/29; WF-03 | 15 | Pass |
| US-16 | 8 | One named support-session lifecycle outcome | Request, start, monitor, expire, and terminate a fixture session | Case, scope, consent, time-box, banner, expiry, denial, and export separation are assertable | No standing privilege, emergency access, or support-to-export shortcut | TE-03/06/07; MESP-28/38/50; WF-05 | 16 | Pass |
| US-17 | 5 | One searchable audit-evidence outcome | Filter audit, inspect denial evidence, and request separately authorized export | Fields, freshness, scope, denial non-disclosure, authorization, and self-audit are assertable | No audit retention/identity policy invented | TE-03/04/06/07; US-14/16; MESP-38; WF-05 | 17 | Pass |
| US-18 | 5 | One bounded export/integrity outcome | Generate an export, inspect integrity/expiry, and verify unauthorized/expired download denial | Manifest, scope, artifact, expiry, authorization, retry, and integrity evidence are assertable | No offboarding state transition or production purge execution | TE-03/04/05/06/07; MESP-50; WF-06 | 18 | Pass |
| US-19 | 5 | One offboarding-disposition/termination-gate outcome | Review accepted export or waiver, open matters, hold, closure plan, and approval | Disposition, waiver warning, legal hold, Subscription end, closure, and state transition are assertable | No purge execution or invented MESP-50 retention value | US-18; TE-03/04/05/06/07; MESP-50; WF-06 | 19 | Pass |
| US-20 | 8 | One purge-review/certificate-truth outcome | Hold blocks approval; fixture approval/cooling-off yields truthful scoped certificate | Hold, dual control, scope, final notice, recheck, residual copies, and partial failure are assertable | No physical purge execution in Sprint 1 and no invented duration/retention | US-19; TE-04/05/06/07; MESP-50; WF-06 | 20 | Pass |
| US-21 | 5 | One cross-cutting EN/AR/RTL equivalence outcome | Toggle language/direction over the shell and representative Wave 1 screens | Labels, warnings, direction, embedded identifiers, status cues, and authority invariance are assertable | No new localization/business behavior beyond MESP-37 | TE-07/08; MESP-37; WF-01–WF-07 | 21 | Pass |

**Pass 2 split decision:** US-12 was added as a separate Story because Feature Rollout (§18 and AC-024/025) was not represented by a dedicated Story. The former combined Export/offboarding Story was split into US-18 (bounded export) and US-19 (offboarding disposition) because each has a separate state, authorization gate, and independent demonstration. The remaining Stories each retain one cohesive workflow outcome, have an estimate of 8 or below, and pass all checks above; no further split is required.

## 4C. Mandatory self-review pass 3 — scope protection

The following terms may appear in the backlog only as explicit exclusions, safety guards, dependencies, or deferred decisions. They are not deliverables.

| Protected scope item | Review result | Evidence |
|---|---|---|
| Retail POS | Pass — excluded from the Epic and guarded in US-07/US-12; no implementation item enables it | Document control, decomposition rules, US-07, US-12, Sprint 1 deferrals |
| Procurement transactions | Pass — no transaction workflow or story | US-03/US-10 scope guards and Epic boundary |
| Inventory transactions | Pass — no transaction workflow or story | US-03/US-10 scope guards and Epic boundary |
| Finance transactions | Pass — no posting, billing, payment, invoice, or accounting workflow | US-07 and Epic boundary |
| B2B Sales transactions | Pass — Release 1 is platform administration only | Document control, Epic boundary, US-03 |
| Production billing automation | Pass — explicitly prohibited | US-07 acceptance criteria and Epic exclusions |
| Per-Tenant Entitlement overrides | Pass — explicitly prohibited and tested as denial | US-08/US-09 and MESP-52 trace |
| Production physical purge execution in Sprint 1 | Pass — US-20 is last high-risk workflow and explicitly deferred; Sprint 1 is read-only | US-20; Sprint 1 scope and deferrals |
| Final MESP-28 Identity behavior | Pass — TE-03 is contract-only; final behavior remains a dependency | TE-03, DEP-01, US-12/US-16 |
| Invented MESP-48 volume limits | Pass — no threshold or capacity promise is assigned | TE-08, DEP-03, US-11 |
| Invented MESP-50 retention or purge durations | Pass — policy values remain unresolved and owned by MESP-50 | DEP-04, US-04/16/18/19/20 |
| Jira writes | Pass — only authorized updates to existing MESP-2 and MESP-57–MESP-85 records; no new issue, comment, or Sprint was created | Document control and §7 readiness gates |
| Source-code changes | Pass — this is a backlog artifact only | Document control and task boundary |
| Parallel execution recommendations | Pass — only dependency-ordered sequencing is proposed; no parallel work recommendation appears | §4 sequence and Sprint 1 candidate scope |

## 5. Dependency and uncertainty register

| ID | Dependency / uncertainty | Effect on backlog | Safe handling |
|---|---|---|---|
| DEP-01 | MESP-28 Identity and Access details (session, MFA, scope, membership, permissions) | Blocks production-grade authorization implementation and support identity behavior | Implement TE-03 contract only; keep affected stories out of Sprint 1 except contract tests |
| DEP-02 | MESP-29 Multi-Tenancy lifecycle/isolation and MESP-30 Organization/legal-entity rules | Affects provisioning inputs, Tenant Workspace Companies tab, activation dependencies, and isolation | Use BRD-approved boundary and stable contracts; do not invent organization workflows |
| DEP-03 | MESP-48 reference volumes and supported limits | Blocks production thresholds, capacity promises, and final Limits & Usage values | Treat wireframe values as illustrative; keep US-11 after evidence |
| DEP-04 | MESP-50 residency/hosting, cross-border support, subprocessors, retention, legal hold, support duration, backup treatment, export expiry, and purge timing | Blocks production provisioning/offboarding/purge policy | Capture fields and pending states; do not activate unresolved production policy |
| DEP-05 | MESP-37 Saudi Country Pack and document/numbering rules | Affects locale defaults, statutory document identity, templates, and numbering | Use explicit configuration contracts; defer detailed compliance behavior |
| DEP-06 | Wireframes are schematic and the seven wizard steps are provisional | Exact component behavior and grouping may change | Preserve required fields and invariants; refine visual/component detail during implementation design without changing BRD behavior |
| DEP-07 | MESP-19 traceability governance follow-up remains after synchronization | Existing Jira keys are recorded here; formal traceability approval remains a follow-up | Keep this artifact and Jira mapping aligned; no new issue or Sprint is required |

## 6. Proposed Development Sprint 1

### Sprint proposal

**Sprint name:** Proposed Sprint 1 — Platform Read-Only Vertical Slice

**Sprint goal:** Prove a tenant-isolated, authorized, bilingual Platform shell → Overview → Tenant Catalogue → Tenant Workspace read-only journey, with auditable API calls and denial behavior, before onboarding or high-risk mutations are attempted.

**Duration/capacity:** Not specified in the approved sources. Confirm the team’s sprint length and one-developer capacity before scheduling. This is a scope proposal, not a Jira Sprint.

**Jira status:** Not created — requires manual Jira setup.

### Candidate Sprint 1 scope

| Include | Record | Estimate | Sprint result |
|---|---|---:|---|
| Technical enabler | TE-01 | 3 | Minimal Modular Monolith solution/module seam and architecture-test conventions |
| Technical enabler | TE-02 | 5 | Trusted Tenant context, Platform-owned metadata read boundary, and isolation fixture |
| Technical enabler | TE-03 | 5 | Platform/Tenant policy seam and secure first-party cookie contract; no MESP-28 implementation |
| Technical enabler | TE-04 | 3 | REST/OpenAPI, error, correlation, and read-request safety conventions |
| Technical enabler | TE-06 | 3 | Audit event contract, correlation propagation, and OpenTelemetry-compatible evidence seam |
| Technical enabler | TE-07 | 5 | Angular 22 shell, navigation, Workspace spine, EN/AR/RTL baseline, accessible state markers |
| Technical enabler | TE-08 | 3 | xUnit/Playwright fixtures, Docker Compose test dependencies, and cross-Tenant denial coverage |
| User story | US-01 | 3 | Overview cards are data-backed links with freshness and restricted/error states |
| User story | US-02 | 5 | Tenant Catalogue supports bounded search/filter/pagination and safe restricted state |
| User story | US-03 | 3 | Tenant Workspace opens the approved read-only tabbed spine without ERP module navigation |
| User story | US-21 (slice) | 2 | EN/AR/RTL behavior is proven for the shell, Overview, Catalogue, and Workspace slice |

**Sprint 1 provisional total:** **40 planning points** — 27 enabler points (TE-01/02/03/04/06/07/08) plus 13 Story/slice points (US-01 3 + US-02 5 + US-03 3 + US-21 shell slice 2). US-21’s full Story estimate remains 5; only its 2-point Sprint 1 slice is included. These are planning estimates, not Jira commitments.

### Sprint 1 exit evidence

- A Platform-authorized actor can open Overview, Catalogue, and a Tenant Workspace using server-enforced scope.
- A Tenant actor cannot read the cross-Tenant Platform register; denial does not reveal other Tenant existence or data.
- Overview figures link to bounded views and expose data-as-of/freshness; static vanity KPIs are not accepted.
- Catalogue has server-side pagination, approved filter categories, empty/loading/error/no-results/restricted states, and no Suspend/Purge row actions.
- Workspace shows bilingual identity, stable code, lifecycle, country, legal-entity count, Plan/module summary, recent activity, and the approved tab structure.
- EN/AR direction changes preserve authority, values, identifiers, state, and warning meaning; status is not color-only.
- xUnit covers tenant-isolation/authorization invariants and Playwright covers the critical browser/API read journey.
- Audit/correlation evidence exists for the read requests and denied cross-Tenant attempt.

### Explicitly deferred from Sprint 1

- Create Tenant wizard submission, provisioning execution, retries, invitations, and module activation.
- Plan/Subscription changes, Entitlement changes, Limits & Usage thresholds, branding writes, support authorization, audit export, export/offboarding, purge, and any high-risk lifecycle mutation.
- MESP-28 implementation, MESP-29/MESP-30 detailed behavior, MESP-48 threshold approval, MESP-50 production policy, all downstream ERP modules, and Retail POS.

## 7. Backlog readiness and approval gates

Before implementation starts or a Jira Sprint is created:

1. Hossam confirms the synchronized Jira mapping (MESP-2, MESP-57 through MESP-85) and the proposed Sprint 1 capacity/setup.
2. MESP-27 BRD remains approved and its Founder Review approval evidence is recorded according to the existing workflow.
3. TE-03, DEP-01, and the boundary with MESP-28 are explicitly accepted as a contract-only seam.
4. MESP-48 and MESP-50 remain visible as blockers for the affected later stories; no illustrative wireframe number is promoted to a production value.
5. The implementation team confirms Sprint 1 capacity and the definition of done; no sprint is created in Jira by this artifact.

## 8. Scope and quality check

- The approved implementation backlog is mapped to the existing Jira Epic MESP-2; no duplicate Epic was created.
- Eight minimum technical enablers are defined with 32 provisional planning points in total.
- Twenty-one sequenced User Stories are defined without subtasks; their provisional estimates total 119 points and the maximum is 8.
- One proposed Sprint 1 is defined and limited to a read-only, tenant-isolated vertical slice; its provisional total is 40 points including enablers and the US-21 shell slice.
- Pass 1 — Coverage: **PASS**. Every approved MESP-27 section, requirement family, decision, and acceptance-scenario family maps to at least one enabler or Story; unresolved values retain a named owner.
- Pass 2 — Story quality: **PASS**. All twenty-one Stories deliver one demonstrable outcome, use testable acceptance criteria, have explicit dependencies and design references, remain within estimate 8, and are dependency-sequenced. US-12 was added to close the Feature Rollout gap, and the former combined Export/offboarding Story was split into US-18 and US-19.
- Pass 3 — Scope protection: **PASS**. No prohibited transaction scope, final MESP-28 behavior, invented MESP-48/MESP-50 values, unauthorized Jira artifact, source-code change, production purge execution in Sprint 1, or parallel-execution recommendation is present as a deliverable.
- No application code, new Jira issue, new Jira comment, Jira Sprint, test-case document, MESP-28 implementation, downstream ERP module work, or Retail POS work was created; existing MESP-2 and MESP-57–MESP-85 records were synchronized only.
- BRD NFRs, Trial exclusion, no-override rule, Restricted Validation Plan, support/export separation, purge truthfulness, multiple legal entities, and MESP-48/MESP-50 gates are preserved.

## 9. Final recalculated order and Sprint 1 estimate

| Seq. | Story | Final Story title | Estimate |
|---:|---|---|---:|
| 1 | US-01 | Platform Overview and exception navigation | 3 |
| 2 | US-02 | Tenant Catalogue with bounded views and states | 5 |
| 3 | US-03 | Tenant Workspace read-only spine | 3 |
| 4 | US-04 | Create Tenant draft wizard | 8 |
| 5 | US-05 | Duplicate, completeness, and review gate | 5 |
| 6 | US-06 | Idempotent provisioning run and operations | 8 |
| 7 | US-07 | Plan Catalogue and version history | 5 |
| 8 | US-08 | Subscription change preview and effective dating | 5 |
| 9 | US-09 | Entitlement visibility and denial proof | 5 |
| 10 | US-10 | Module readiness, dependency block, and rollback evidence | 8 |
| 11 | US-11 | Limits and Usage visibility | 5 |
| 12 | US-12 | Feature cohort rollout and rollback | 5 |
| 13 | US-13 | Branding, templates, numbering, and document identity | 5 |
| 14 | US-14 | Lifecycle workspace and suspension confirmation | 8 |
| 15 | US-15 | Reactivation and interrupted-work review | 5 |
| 16 | US-16 | Support authorization and active-session monitor | 8 |
| 17 | US-17 | Platform Audit dashboard and authorized evidence export | 5 |
| 18 | US-18 | Bounded Tenant export and artifact integrity | 5 |
| 19 | US-19 | Offboarding disposition and termination gate | 5 |
| 20 | US-20 | Purge review, cooling-off, and truthful certificate | 8 |
| 21 | US-21 | EN/AR localization and RTL completion across Wave 1 | 5 |

**Final totals:** 8 Enablers; 21 User Stories; 119 provisional Story points; maximum Story estimate 8. Sprint 1 includes TE-01/02/03/04/06/07/08, US-01, US-02, US-03, and the US-21 shell slice for **40 provisional planning points**.
