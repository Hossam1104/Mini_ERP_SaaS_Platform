# Mini ERP SaaS Platform - Multi-Tenancy and Tenant Lifecycle BRD

## 1. Document Control

| Field | Value |
|---|---|
| Document | Multi-Tenancy and Tenant Lifecycle Business Requirements Document |
| Jira | MESP-29 - Produce Multi-Tenancy and Tenant Lifecycle BRD |
| Parent Epic | MESP-4 - EPIC 04 - Multi-Tenancy and Tenant Lifecycle |
| Version | v0.2 — Approved Release 1 Baseline |
| Status | Approved Release 1 Baseline |
| Accountable owner | Hossam, Product Owner and founder approver |
| Prepared by | Luna Max, Senior Business Analyst and Product Requirements Lead |
| Date | 2 August 2026 |
| Canonical product baseline | `docs/MESP_PRD_v1.2.docx`, PRD v1.2 Final Approved Baseline (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Mandatory vocabulary | `docs/00_ERP_Business_Glossary.md` |
| Related approved BRDs | `docs/11_SaaS_Platform_Administration_BRD.md`; `docs/12_Identity_and_Access_BRD.md` |
| Architecture reference | `docs/01_Technology_Architecture_Baseline.md` (constraint reference only) |
| Delivery reference | `docs/94_Product_Delivery_Master_Plan.md` |
| Jira state at approval | MESP-4 In Progress; MESP-29 Done; MESP-29 outside all Sprints |

This is a business-requirements document. It authorizes no API, database, UI, code, automated test, Sprint, or implementation Jira work. Hossam approved this Release 1 baseline on 2 August 2026; the approval closes the MESP-29 requirements task without authorizing implementation.

### Classification legend

| Classification | Meaning |
|---|---|
| **Confirmed** | Directly supported by the approved PRD, glossary, approved MESP-27 or MESP-28 boundary, architecture baseline, or an existing Jira requirement. |
| **Confirmed — Founder-approved Release 1 requirement** | Explicitly approved by Hossam for the Release 1 business baseline and carried forward without adding implementation behavior. |
| **Open Decision** | A genuine business decision still requiring Hossam's recorded approval. Only the `MT-OD-*` register uses this classification for open founder decisions. |
| **Deferred Gate** | Deliberately owned by MESP-48, MESP-50, a later BRD, qualified validation, or a later approval. No value is invented here. |
| **Out of Scope** | Explicitly excluded from this BRD or owned by another domain. |

## 2. Executive Summary

**Classification: Confirmed.** Multi-Tenancy defines the Tenant as the customer subscription and data-isolation boundary of the Platform. The approved hierarchy remains:

`Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse`

**Classification: Confirmed — Founder-approved Release 1 requirement.** Every record is either Tenant-owned and belongs to exactly one Tenant, or Platform-owned because it exists for Platform governance, operation, security, subscription, provisioning, support, or audit purposes. Platform-owned records may reference one or more Tenants when required for governance or evidence, but they are not shared Tenant business data and cannot be used by a Tenant User to view another Tenant. Tenant data is private by default. A User may act in a Tenant only through an explicit active Tenant Membership approved by MESP-28 and the applicable Role, Permission, scope, lifecycle, Entitlement, and contextual controls.

**Classification: Confirmed — Founder-approved Release 1 requirement.** Every protected request, workspace, or authenticated session context operates within exactly one Tenant, and one operation never combines Tenant contexts. A User may have separate authorized sessions or workspaces for different Tenants where supported; each independently establishes and enforces its own Tenant context. Switching away from Tenant A never displays, reuses, submits, or interprets Tenant A state inside Tenant B, does not automatically delete valid Tenant A drafts or working state, and permits return to Tenant A state only after authorization and lifecycle are re-evaluated. Invalid, revoked, suspended, terminated, expired, or otherwise invalid state is not restored merely because the User returns to the Tenant.

**Classification: Confirmed.** A client-supplied Tenant identifier can identify a requested target, but it can never expand authority. Reads, writes, searches, reports, files, exports, background jobs, notifications, integrations, audit access, and support access crossing the active Tenant boundary are denied by default.

**Classification: Confirmed.** Platform administration does not, merely because of the Platform role, grant access to Tenant business data. Support access is a separate, named, case-bound, Tenant-approved, exact-scope, time-limited decision, and support authorization alone never grants export authority.

**Classification: Confirmed — Founder-approved Release 1 requirement.** A Tenant may contain multiple Companies / Legal Entities. Release 1 does not provide consolidation, intercompany automation, elimination entries, transfer pricing, or consolidated financial statements. Wafra is Tenant #1 for validation evidence only and creates no Wafra-specific core behavior. Retail POS remains excluded.

**Classification: Deferred Gate.** Reference volumes, supported-volume promises, residency, retention, legal hold, backup, restoration, and purge details remain governed by MESP-48 and MESP-50. This BRD records the business gates without inventing limits, durations, hosting regions, or purge execution rules.

## 3. Business Purpose

**Classification: Confirmed.** The purpose of MESP-29 is to define the reusable business operating model that keeps each Tenant's data, authority, lifecycle, and operational work isolated while allowing the Platform to serve multiple B2B ERP customers from one product.

The business objectives are:

| Objective | Required business outcome | Classification | Source / owner |
|---|---|---|---|
| Tenant boundary | A Tenant is the subscription and primary data-isolation boundary. | Confirmed | PRD PLT-001/PLT-002; glossary; MESP-4 |
| Private data | Tenant business data is not visible or mutable from another Tenant. | Confirmed — Founder-approved Release 1 requirement | Founder direction; PRD BR-001 |
| Trusted context | A User's active Tenant context is derived from approved membership and cannot be expanded by a client identifier. | Confirmed | PRD RULE-001; MESP-28; MESP-4 |
| Lifecycle control | Creation, activation, suspension, reactivation, termination, retention, and evidence states are explicit and reviewable. | Confirmed | MESP-27; MESP-4 |
| Safe operations | Interactive and asynchronous business work obeys the same Tenant and lifecycle boundary, while required Platform safety and governance work may continue during suspension. | Confirmed — Founder-approved Release 1 requirement | Founder direction; MESP-27 |
| Reusable product | Wafra validates the generic behavior; no Tenant-specific branch is introduced. | Confirmed | PRD BR-003; MESP-27 |
| Governed gates | MESP-48 and MESP-50 remain explicit production and capacity gates. | Deferred Gate | PRD; Decisions ADR-014; MESP-48/MESP-50 |

## 4. Scope

The following are in scope for this business baseline:

| In-scope area | Business requirement | Classification | Owner / dependency |
|---|---|---|---|
| Tenant boundary | Meaning of Tenant as subscription, privacy, and data-isolation boundary. | Confirmed | MESP-29 |
| Tenant context | Selection, establishment, switching, and denial of exactly one Tenant context per protected request, workspace, or authenticated session. | Confirmed — Founder-approved Release 1 requirement | MESP-29 with MESP-28 membership |
| Tenant lifecycle | Creation, onboarding, activation, suspension, reactivation, termination, retention, and governed offboarding meaning. | Confirmed | MESP-29/MESP-27 |
| Tenant isolation | Isolation of records, files, reports, exports, jobs, notifications, integrations, audit, and working-state results, with no global one-Tenant-at-a-time restriction across separate authorized sessions or workspaces. | Confirmed — Founder-approved Release 1 requirement | MESP-29; later implementation gate |
| Cross-Tenant denial | Default-deny behavior for every named access path and exception boundary. | Confirmed — Founder-approved Release 1 requirement | MESP-29/MESP-38 |
| Support boundary | Tenant approval, named case and User, exact scope, maximum eight hours, fresh extension approval, and separate export authority. | Confirmed | MESP-27/MESP-28 |
| Legal-entity boundary | Multiple legal entities may exist inside one Tenant without Release 1 consolidation. | Confirmed — Founder-approved Release 1 requirement | MESP-56; MESP-30 detail |
| Migration and onboarding | Business ownership, duplicate review, mapping, reconciliation, quarantine, and approval expectations. | Confirmed | PRD migration baseline; MESP-40 |
| Audit evidence | Evidence needed to reconstruct Tenant context, isolation denials, lifecycle, support, export, and offboarding decisions. | Confirmed | PRD BR-011; MESP-38/MESP-50 |
| Capacity and retention gates | Explicit dependency and evidence boundary for MESP-48 and MESP-50. | Deferred Gate | MESP-48/MESP-50 |

## 5. Out of Scope

| Exclusion | Classification | Owner / reason |
|---|---|---|
| APIs, endpoint names, headers, tokens, cookies, protocols, or interface payloads | Out of Scope | Architecture and later Lean Implementation Specification |
| Tables, columns, keys, schemas, row-level security, query filters, database-per-Tenant, or physical isolation mechanism | Out of Scope | Architecture/Phase 5; no database design in this BRD |
| Angular screens, navigation, controls, visual layout, or UI components | Out of Scope | Later Lean Implementation Specification |
| Source code, implementation Stories, Enablers, tests, Sprint creation, or Pull Request | Out of Scope | Delivery plan; this task is business analysis only |
| Company / Legal Entity, Branch, and Warehouse operating rules | Out of Scope | MESP-30 owns detailed Organization behavior |
| User, Role, Permission, membership, authentication, and session mechanism design | Out of Scope | MESP-28 owns identity meaning; technical design is downstream |
| Plan, Subscription, Entitlement, billing, commercial limits, and platform administration behavior | Out of Scope | MESP-27 owns the platform-administration baseline |
| Procurement, Inventory, Finance, B2B Sales, and Retail POS transactions | Out of Scope | Later domain BRDs; Retail POS is excluded from Release 1 |
| Consolidation, intercompany automation, elimination entries, transfer pricing, and consolidated statements | Out of Scope | Founder-approved Release 1 boundary; MESP-30/Finance detail |
| Invented MESP-48 volumes or MESP-50 retention, purge, residency, backup, or restoration values | Deferred Gate | MESP-48/MESP-50 |
| Physical production purge execution | Deferred Gate | MESP-50 and later production approval |
| Wafra-specific workflow, role, limit, schema, report, or permission branch | Out of Scope | Wafra is validation-only |

## 6. Source Traceability

| Source | Relevant authority used in this BRD | Sections | Classification |
|---|---|---|---|
| Jira MESP-29 | Required Tenant isolation, context, lifecycle, onboarding, export, retention, termination, data-separation outputs and business-only constraint. | 2-28 | Confirmed |
| Jira MESP-4 | Tenant isolation across records, jobs, files, cache keys, logs, and search; server-derived context; default-deny cross-Tenant access; BRD gate. | 2, 4, 9-24 | Confirmed |
| PRD v1.2 | Platform hierarchy, PLT-001/PLT-002/PLT-011, RULE-001/RULE-016, BR-001/BR-003/BR-011, migration baseline, B2B-only scope, offboarding gates. | 2-28 | Confirmed |
| `docs/11_SaaS_Platform_Administration_BRD.md` | Canonical lifecycle, support, export, suspension/reactivation, offboarding, audit, and MESP-48/MESP-50 boundaries. | 7, 10, 12-24 | Confirmed |
| `docs/12_Identity_and_Access_BRD.md` | Explicit membership, active context, multi-Tenant isolation, session effects, support boundary, and identity ownership. | 7-20, 24 | Confirmed |
| `docs/01_Technology_Architecture_Baseline.md` | Feasibility constraint only; modular monolith and shared database do not alter business requirements. | 9, 14, 23 | Confirmed boundary |
| `docs/Decisions.md` | ADR-003 shared-database isolation boundary, ADR-014 MESP-50 gate, ADR-016 production RLS decision gate, and no implementation from BRD. | 14, 23 | Confirmed dependency |
| `docs/00_ERP_Business_Glossary.md` | Controlled meanings of Tenant, User, Membership, Access Scope, Company, Legal Entity, Branch, Warehouse, Audit Event, and Retail POS. | 8-9, 14, 24 | Confirmed |
| `docs/90_MVP_Founder_Decision_Pack.md` | Founder defaults: hierarchy, Wafra validation-only, multiple legal entities without consolidation, B2B-only scope, and MESP-48/MESP-50 gates. | 2, 8-9, 23-25 | Confirmed |
| `docs/94_Product_Delivery_Master_Plan.md` | Sequential BRD delivery, no implementation before gates, current branch/status, and next founder-review action. | 1, 27-28 | Confirmed |

## 7. Actors and Responsibilities

| Actor | Responsibility in this BRD | Permitted business scope | Constraint | Classification |
|---|---|---|---|---|
| Hossam / Product Owner and founder approver | Approves this BRD, resolves genuine founder decisions, and controls delivery sequencing. | Platform governance. | Approval does not itself authorize implementation or code. | Confirmed |
| Platform Owner / Platform Administrator | Provisions, operates, suspends, reactivates, terminates, and reviews Tenants through approved Platform processes and controls Platform-owned governance, operation, security, subscription, provisioning, support, and audit records. | Platform-owned records and explicitly authorized lifecycle operations; a Platform-owned record may reference one or more Tenants for governance or evidence. | Platform administration alone does not grant referenced Tenant business-data access; access remains purpose-bound, authorized, and audited. | Confirmed |
| Tenant Administrator | Confirms Tenant setup, manages Tenant-side Users and organization assignments within approved boundaries, and participates in support approval. | One authorized Tenant and its approved hierarchy. | Cannot cross Tenant boundaries or alter Platform-owned Plans, Entitlements, or audit evidence. | Confirmed |
| Tenant User | Performs an approved B2B business function within exactly one Tenant context for each protected request, workspace, or authenticated session. | Active membership, Role, Permission, Entitlement, and organizational scope; separate authorized sessions or workspaces may establish different Tenant contexts. | Cannot combine contexts or use an identifier or context switch to expand authority. | Confirmed |
| Authorized Support User | Investigates a named case within one Tenant, exact scope, purpose, and approved interval. | One named Tenant and exact support scope. | No shared account, standing access, cross-Tenant access, or export authority from support alone. | Confirmed |
| Security / Privacy / Audit reviewer | Reviews privileged access, denials, lifecycle, export, legal-hold, retention, and evidence. | Authorized review scope. | Does not gain ordinary Tenant business-data access merely by being a reviewer. | Confirmed |
| Migration / Onboarding owner | Owns source mapping, duplicate review, reconciliation, exception ownership, and business sign-off. | Named source and target Tenant scope. | Ambiguous mappings remain quarantined until accountable approval. | Confirmed |
| Background Operator | Executes an already authorized Tenant-bound business operation or required Platform safety/governance operation and records its result. | Initiating Tenant and recorded scope for business work; approved Platform purpose and scope for safety/governance work. | Cannot infer or expand a different Tenant or scope; suspension does not stop required Platform safety/governance operations. | Confirmed |

## 8. Tenant Terminology

| Term | Business meaning used by MESP-29 | Boundary | Classification |
|---|---|---|---|
| Platform | The single multi-Tenant Mini ERP SaaS service. | Outer level of the hierarchy; not a Tenant. | Confirmed |
| Tenant | An isolated customer subscription boundary owning its Tenant-scoped Users, configuration, business data, files, reports, and evidence. | Not a Company, Branch, Warehouse, User, or database concept exposed to business users. | Confirmed |
| Platform-owned record | A record owned by the Platform because it exists for Platform governance, operation, security, subscription, provisioning, support, or audit purposes. Examples include Tenant catalogue records, Plan/Subscription/Entitlement administration, provisioning and lifecycle coordination, support cases and approvals, security/audit evidence, and operational/monitoring records. | May reference one or more Tenants for governance or evidence, but is not shared Tenant business data. A Tenant User cannot use it to view another Tenant. Platform Administrator status alone does not grant access to referenced Tenant business data. | Confirmed boundary |
| Tenant-owned business record | A record of a Tenant's master data, configuration, transaction, report result, file, export, or other business activity. | Exactly one Tenant; it cannot be shared across Tenant contexts. | Confirmed — Founder-approved Release 1 requirement |
| Tenant context | The Tenant boundary independently established for each protected request, workspace, or authenticated session. | Exactly one Tenant per context, from an active explicit membership; separate contexts may coexist, and a client identifier cannot create authority. | Confirmed — Founder-approved Release 1 requirement |
| Tenant Membership | The explicit link between a User and one Tenant, carrying the applicable Roles and Access Scope. | Revocable and isolated per Tenant; owned semantically by MESP-28. | Confirmed |
| Tenant lifecycle status | The governed business state of a Tenant, including activation, suspension, reactivation, termination, and retention states. | Status changes require authorized reason, evidence, and resulting-access rules. | Confirmed |
| Tenant suspension | A state in which ordinary interactive and asynchronous Tenant business operations are restricted for a recorded reason, while required Platform safety and governance operations continue where applicable. | Tenant access, sessions, business jobs, exports, and integrations follow the explicit suspension result; logging, audit capture, monitoring, alerts, backup/restoration controls, retention/legal-hold enforcement, lifecycle notices, access revocation, separately authorized controlled export, and termination/offboarding controls remain governed Platform operations. | Confirmed — Founder-approved Release 1 requirement |
| Tenant reactivation | A controlled restoration decision after the suspension reason is cleared. | Users, memberships, integrations, background work, files, exports, sessions, and pending work are reevaluated; invalid state is not restored merely by return. | Confirmed — Founder-approved Release 1 requirement |
| Tenant termination | The end of operational Tenant access after governed offboarding and closure. | Termination revokes active access and preserves evidence; it does not itself authorize purge. | Confirmed — Founder-approved Release 1 requirement |
| Tenant export | A bounded, authorized representation of a Tenant's approved data or evidence scope. | Separate export authority is required; support authorization alone is insufficient. | Confirmed |
| Support access | Named, case-bound, Tenant-approved, exact-scope, least-privilege access for a maximum approved interval. | Release 1 maximum is eight hours; extension requires fresh approval. | Confirmed |
| Audit evidence | Immutable business evidence needed to reconstruct a Tenant decision, access outcome, or material event. | Tenant Users cannot edit or delete it; retention remains an MESP-50 gate. | Confirmed |
| Legal hold | A governed prohibition on deletion or purge for an approved reason. | It blocks purge regardless of termination or commercial request. | Deferred Gate |

## 9. Ownership Boundaries

| Concern | MESP-29 owns | Adjacent owner / boundary | Classification |
|---|---|---|---|
| Tenant meaning | Tenant as subscription, privacy, and isolation boundary. | MESP-27 owns Plan/Subscription/Entitlement meaning. | Confirmed |
| Tenant context | Business selection, switching, and default-deny boundary independently established for each protected request, workspace, or authenticated session. | MESP-28 owns User and Membership meaning; technical session design is downstream. Separate authorized sessions or workspaces may use different Tenant contexts, but one operation never combines them. | Confirmed — Founder-approved Release 1 requirement |
| Company / Legal Entity / Branch / Warehouse | Only the fact that they are downward scopes inside a Tenant. | MESP-30 owns their identity, relationships, and operating rules. | Confirmed boundary |
| Platform administration | Tenant catalogue, Platform-owned governance/operation/security/subscription/provisioning/support/audit records, lifecycle coordination, support, export, and offboarding policy inherited from MESP-27. | MESP-27 remains authoritative for Platform-owned configuration; Platform-owned records may reference Tenants but do not become shared Tenant business data. | Confirmed boundary |
| Identity and access | Explicit active membership and identity-side denial inputs. | MESP-28 remains authoritative for User, Role, Permission, session, and membership business meaning. | Confirmed boundary |
| Security and audit | Tenant isolation evidence and event requirements. | MESP-38 owns detailed security, SoD, audit, and governance catalogues. | Confirmed boundary |
| Files, exports, reports, notifications, integrations | Tenant-bound business outcome, working-state preservation, and denial rule; valid Tenant A state is not deleted merely by switching away, but cannot be used in Tenant B. | MESP-36/MESP-39 and later specifications own detailed behavior. | Confirmed — Founder-approved Release 1 requirement |
| Migration | Tenant onboarding, mapping, duplicate, quarantine, reconciliation, and owner sign-off expectations. | MESP-40 owns detailed migration and cutover. | Confirmed boundary |
| Volumes | Evidence categories and the requirement not to publish unsupported limits. | MESP-48 owns reference volumes and supported-volume evidence. | Deferred Gate |
| Retention and purge | Business gate, no automatic production purge, and evidence boundary. | MESP-50 owns residency, retention, legal hold, backup, restoration, and purge policy. | Deferred Gate |

## 10. Tenant Lifecycle

The lifecycle vocabulary follows the approved MESP-27 baseline. MESP-29 applies the Tenant-isolation and Tenant-context consequences of each state; it does not silently change MESP-27 Plan, Subscription, Entitlement, support, or purge decisions.

| Status | Business meaning | Entry / exit expectation | Ordinary Tenant operation | Classification |
|---|---|---|---|---|
| Draft | An onboarding request exists but the Tenant is not operational. | Completeness, duplicate, and authority checks precede provisioning. | No sign-in, business data operation, or usable invitation. | Confirmed |
| Provisioning | Controlled Tenant setup is in progress. | Each stage has a visible outcome and safe recovery result. | No ordinary Tenant operation or manual bypass. | Confirmed |
| Configuration Required | The Tenant foundation exists but required setup remains incomplete. | Required configuration and ownership evidence precede readiness. | Setup only within the approved boundary; no production operation. | Confirmed |
| Ready for Activation | Required setup and validation are complete. | Authorized Platform decision and Tenant acknowledgement precede Active. | No production operation until activation. | Confirmed |
| Active | The Tenant may operate its entitled B2B ERP capabilities subject to identity, scope, state, and policy. | Authorized lifecycle decision can move it to Grace Period, Suspended, or Export Requested. | Allowed only within the active Tenant context and applicable scope. | Confirmed |
| Grace Period | A recorded, time-bounded commercial or administrative remedy state. | Resolution returns to Active; documented expiry may move to Suspended. | Only the explicitly permitted existing work remains available. | Confirmed |
| Suspended | Ordinary Tenant business access and operations are restricted for a recorded reason. | Reactivation requires clearance and restoration checks; termination may follow governed offboarding. Required Platform safety and governance operations continue where applicable. | Ordinary interactive and asynchronous business operations are denied; approved logging, audit capture, monitoring/alerting, backup/restoration controls, retention/legal-hold enforcement, lifecycle notifications, access revocation, controlled export, and termination/offboarding controls remain purpose-bound and audited, subject to MESP-50 where applicable. | Confirmed — Founder-approved Release 1 requirement |
| Reactivated | Restoration is being verified after suspension. | Users, memberships, integrations, background work, files, exports, sessions, and pending work are reevaluated before Active. | No assumption that prior authority or interrupted work is restored; invalid state is not restored merely because a User returns. | Confirmed — Founder-approved Release 1 requirement |
| Export Requested | A bounded, authorized offboarding or portability request is accepted. | Scope, authority, artifact, expiry, and disposition are recorded before termination scheduling. | Access follows the prior/explicit lifecycle decision; export does not authorize purge. | Confirmed |
| Termination Pending | Closure controls are underway. | Export disposition, access closure, legal-hold review, and accountable approval precede Terminated. | No new activation or commercial expansion. | Confirmed |
| Terminated | Operational Tenant access and active Entitlements have ended. | Governed retention and evidence handling follow; no ordinary sign-in or mutation. | Platform-only governed evidence and safety operations; no ordinary Tenant operation. | Confirmed — Founder-approved Release 1 requirement |
| Retained | Data is held under an approved retention or legal-hold condition. | Purge review is blocked until all required MESP-50 conditions are met. | No ordinary operation, sign-in, or reuse. | Deferred Gate |
| Purge Approved | A certified scope has passed the approved legal, retention, backup, evidence, and dual-control gates. | MESP-50 cooling-off, final notice, and rechecks precede any later execution. | No Tenant operation or Entitlement. | Deferred Gate |
| Purged | The certified purge scope is verified removed according to an approved policy. | Terminal for the certified scope; residual copies and restoration limits must be disclosed. | No operational reuse from the certified scope. | Deferred Gate |

## 11. Tenant Context Selection and Switching

### 11.1 Context selection

**Classification: Confirmed — Founder-approved Release 1 requirement.** Each protected request, workspace, or authenticated session must establish exactly one eligible active Tenant context from the User's active Memberships. A User with more than one eligible membership must explicitly select or enter the Tenant context for that request, workspace, or session; separate authorized sessions or workspaces may establish different Tenant contexts when supported.

**Classification: Confirmed.** The selected context is valid only when the User has an active Membership approved under MESP-28, the Tenant is in a permitted lifecycle status, and the requested action also passes Role, Permission, Entitlement, organizational scope, document state, and other applicable controls.

### 11.2 Context switching

**Classification: Confirmed — Founder-approved Release 1 requirement.** Establishing Tenant B requires a separate active Membership for Tenant B and independently re-evaluates Tenant B authorization and lifecycle status. Tenant A authority and state are never displayed, reused, submitted, or interpreted inside Tenant B. Switching away from Tenant A does not automatically delete valid Tenant A drafts or working state; those may be available again only after returning to Tenant A and successfully re-evaluating authorization and lifecycle status. Revoked, suspended, terminated, expired, or otherwise invalid state is not restored merely because the User returns.

**Classification: Confirmed.** Each request, workspace, or session has one active Tenant context at a time; this does not impose a global one-Tenant-at-a-time restriction across the User's separate authorized sessions or workspaces. If the target membership, Tenant status, scope, or authorization is invalid, establishment of that context is denied without revealing the target Tenant's protected data and no existing context is broadened.

### 11.3 Context failure and evidence

**Classification: Confirmed — Founder-approved Release 1 requirement.** A changed, guessed, stale, or client-supplied Tenant identifier cannot change the authenticated User's authority for any request, workspace, or session. The attempted context, safe denial outcome, actor, time, and reason category are business evidence subject to the audit boundary.

## 12. Main Business Processes

Each process is business-level. No screen, API, schema, token, or implementation behavior is prescribed.

### 12.1 Create and onboard a Tenant

- **Classification:** Confirmed process.
- **Trigger:** An approved onboarding request is received by the authorized Platform actor.
- **Preconditions:** Tenant identity, commercial authority, required onboarding information, duplicate checks, and accountable owner are available.
- **Main flow:** Validate the request; establish the Tenant boundary; associate the initial Company / Legal Entity without redefining MESP-30 behavior; record the Tenant lifecycle status; prepare the initial administrator and required configuration; retain the request and validation evidence.
- **Alternative / exception:** Duplicate or incomplete identity is held or rejected for reviewed resolution. A failed partial process cannot produce an Active Tenant or usable cross-Tenant access.
- **Outcome:** One reviewable Tenant onboarding record with status, owner, evidence, and next permitted action.

### 12.2 Activate a Tenant

- **Classification:** Confirmed process.
- **Trigger:** Required setup and validation are complete.
- **Preconditions:** Tenant identity, initial organization reference, administrator acknowledgement, applicable Plan/Entitlement readiness, and activation evidence are complete.
- **Main flow:** An authorized Platform actor reviews the readiness evidence, records the activation decision, and makes the Tenant Active only after the gate passes.
- **Alternative / exception:** Missing evidence, contradictory MESP-50-controlled information, duplicate identity, or failed setup keeps the Tenant outside Active and records the owner and reason.
- **Outcome:** Active status and an auditable readiness decision; no new commercial or organizational rule is invented.

### 12.3 Establish an active Tenant context

- **Classification:** Confirmed — Founder-approved Release 1 process.
- **Trigger:** A User begins a protected request, workspace, or authenticated session.
- **Preconditions:** An active User and one eligible active Tenant Membership are present for the context being established; multi-membership Users have selected one target Tenant for that request, workspace, or session.
- **Main flow:** Confirm the explicit Membership and Tenant status, establish exactly one active Tenant context for the request, workspace, or session, and evaluate the requested operation inside that boundary. Separate authorized sessions or workspaces may establish different Tenant contexts without combining authority.
- **Alternative / exception:** Missing, suspended, revoked, or unrelated Membership denies the operation without exposing another Tenant.
- **Outcome:** One authorized Tenant context for the specific request, workspace, or session, or a safe denial with evidence.

### 12.4 Switch Tenant context

- **Classification:** Confirmed — Founder-approved Release 1 process.
- **Trigger:** A multi-membership User requests a different Tenant.
- **Preconditions:** The target Tenant is explicitly represented by an active Membership and is eligible for the requested request, workspace, or session context.
- **Main flow:** Establish the target context independently, re-evaluate authority and lifecycle status, and keep the prior Tenant's authority and state unavailable inside the target context. Valid prior Tenant drafts and working state are not automatically deleted, but return to them requires a successful re-evaluation in the prior Tenant context.
- **Alternative / exception:** Invalid target, changed identifier, inactive Tenant, or unauthorized Membership denies the target context; revoked, suspended, terminated, expired, or otherwise invalid prior state is not restored merely by return.
- **Outcome:** An independently authorized target Tenant context or a denial that leaves no cross-Tenant authority.

### 12.5 Suspend a Tenant

- **Classification:** Confirmed — Founder-approved Release 1 process.
- **Trigger:** An authorized commercial, security, legal, administrative, or operational decision requires restriction.
- **Preconditions:** Reason, authority, affected scope, effective time, review condition, and permitted access mode are recorded.
- **Main flow:** Record the Suspended status; deny ordinary interactive and asynchronous business operations; revoke or invalidate affected sessions; stop or hold prohibited business jobs, exports, notifications, integrations, and other business work; continue required Platform-controlled security logging, incident investigation, audit-evidence capture, monitoring/alerting, backup/restoration controls, retention/legal-hold enforcement, lifecycle notifications, access revocation, separately authorized controlled export, and termination/offboarding controls; notify responsible actors; retain evidence.
- **Alternative / exception:** Platform-controlled safety and governance operations do not reactivate the Tenant or grant ordinary Tenant access. They remain purpose-bound and audited, and remain subject to MESP-50 where retention, backup, restoration, legal hold, residency, or purge is involved. Support or controlled export remains separately authorized and Tenant-bound.
- **Outcome:** Consistent business restriction without deleting Tenant data, stopping required Platform safety work, or weakening isolation.

### 12.6 Reactivate a Tenant

- **Classification:** Confirmed — Founder-approved Release 1 process.
- **Trigger:** The suspension reason is cleared and an authorized restoration review begins.
- **Preconditions:** Clearance, ownership, current Tenant status, Memberships, integrations, jobs, files, exports, and pending work are available for review.
- **Main flow:** Re-evaluate Users, Memberships, Roles/Permissions, Entitlements, sessions, integrations, background work, files, exports, and interrupted/pending work; record which capabilities are restored or remain restricted; move to Active only after the restoration gate passes. A User's return to the Tenant does not by itself restore invalid state.
- **Alternative / exception:** Failed checks keep the Tenant restricted and record the responsible owner; interrupted work is not replayed, duplicated, silently discarded, or automatically reauthorized.
- **Outcome:** Controlled restoration with evidence of restored and still-restricted capabilities.

### 12.7 Terminate and offboard a Tenant

- **Classification:** Confirmed process with Deferred Gate controls.
- **Trigger:** An authorized offboarding request or approved contractual/administrative termination decision.
- **Preconditions:** Tenant identity, export disposition, access closure, integration closure, support/security matters, legal-hold status, and accountable approval are known.
- **Main flow:** Generate or accept a bounded authorized export where applicable; close active access and operations at the effective time; preserve evidence; place data into the governed retention state; do not execute purge without the MESP-50 gate.
- **Alternative / exception:** Missing export disposition, active legal hold, unresolved ownership, or incomplete MESP-50 evidence blocks termination or later purge and records the reason.
- **Outcome:** Terminated/Retained Tenant with preserved evidence and an explicit next gate.

### 12.8 Approve support access involving a Tenant

- **Classification:** Confirmed process.
- **Trigger:** A named support case requires controlled investigation or assistance.
- **Preconditions:** Named User, named case, business purpose, one Tenant, exact scope, Tenant approval, Platform approval where required, start/end condition, and notification/evidence requirements.
- **Main flow:** Authorize only the approved boundary for no more than eight hours; record activity and downloads; expire or revoke access; review and close the case.
- **Alternative / exception:** Another Tenant, purpose, scope, extension, or export requires fresh authorization. Support authorization alone never grants export authority.
- **Outcome:** A closed, fully evidenced support decision with no standing or cross-Tenant authority.

### 12.9 Evaluate an asynchronous Tenant operation

- **Classification:** Confirmed — Founder-approved Release 1 process.
- **Trigger:** A background job, notification, report preparation, integration action, export, or other asynchronous work is scheduled or retried.
- **Preconditions:** An initiating Tenant context, authorized scope, purpose, lifecycle eligibility, and safe retry identity are recorded.
- **Main flow:** Execute ordinary business work only for the recorded Tenant and scope; re-evaluate lifecycle and authorization at the point of execution; record success, denial, failure, retry, or cancellation evidence. Required Platform-controlled safety and governance work may continue for its approved purpose and scope.
- **Alternative / exception:** Suspended, terminated, revoked, or cross-Tenant business work is denied or held according to the applicable lifecycle policy; required Platform safety/governance work does not reactivate the Tenant or grant ordinary access and remains audited and MESP-50-gated where applicable.
- **Outcome:** A Tenant-bound business result, an approved Platform safety/governance result, or a visible safe denial/failure with no scope expansion.

## 13. Alternative and Exception Paths

| Situation | Required business handling | Classification | Owner / gate |
|---|---|---|---|
| Duplicate Tenant identity | Hold or reject authoritative creation pending reviewed duplicate resolution. | Confirmed | Platform Administration / MESP-27 |
| Incomplete onboarding | Keep outside Active, identify missing owner/evidence, and allow safe correction. | Confirmed | Platform Administration |
| Unauthorized Tenant selection | Deny without exposing the target Tenant. | Confirmed — Founder-approved Release 1 requirement | MESP-29/MESP-28 |
| Changed or stale Tenant identifier | Re-evaluate trusted membership/context for the specific request, workspace, or session; identifier cannot expand authority. | Confirmed — Founder-approved Release 1 requirement | MESP-29 |
| User has multiple memberships | Require one selected Tenant context per request, workspace, or session; do not infer a combined Tenant context or impose a global one-Tenant-at-a-time restriction. | Confirmed — Founder-approved Release 1 requirement | MESP-28/MESP-29 |
| User has no active membership | Deny Tenant operation while retaining the global User identity and required evidence. | Confirmed | MESP-28 |
| Tenant is Suspended | Deny ordinary interactive and asynchronous business operations; preserve data and evidence while required Platform safety/governance operations continue where applicable. | Confirmed — Founder-approved Release 1 requirement | MESP-27/MESP-29 |
| Existing session after suspension | Revoke or invalidate affected authority before further operation; record the result. | Confirmed | MESP-27/MESP-28 |
| Background work after suspension | Do not run prohibited business work; keep state/reason visible and avoid affecting another Tenant. Required Platform safety/governance work remains purpose-bound, audited, and MESP-50-gated where applicable. | Confirmed — Founder-approved Release 1 requirement | MESP-27/MESP-29 |
| Reactivation checks fail | Keep restricted; identify the owner and capabilities that remain blocked, and do not restore invalid state merely because a User returns. | Confirmed — Founder-approved Release 1 requirement | MESP-29 |
| Termination with legal hold | Preserve the hold and block purge regardless of commercial request. | Deferred Gate | MESP-50 |
| Support targets another Tenant | Deny and record a security event; fresh authorization is required for any new Tenant. | Confirmed | MESP-27/MESP-28 |
| Support requests export | Apply separate export Permission, authorization, and explicit Tenant approval for the named artifact. | Confirmed | MESP-27/MESP-28/MESP-39 |
| Wafra-specific request | Use generic behavior and record Wafra only as validation evidence. | Confirmed | PRD BR-003 |
| Multiple legal entities | Preserve each entity's legal/accounting boundary; do not introduce consolidation. | Confirmed — Founder-approved Release 1 requirement | MESP-30/MESP-56 |
| Ambiguous migration mapping | Quarantine it, assign an accountable owner, reconcile, and approve before activation. | Confirmed | MESP-40 |
| Unsupported volume claim | Do not publish a threshold or capacity promise before MESP-48 evidence. | Deferred Gate | MESP-48 |
| Retention/purge ambiguity | Do not invent a duration or execute production purge before MESP-50 approval. | Deferred Gate | MESP-50 |

## 14. Tenant-Isolation Business Rules

The following register is the MESP-29 business-rule baseline. It contains no API, schema, query, storage, or framework prescription.

| ID | Rule | Classification | Source / dependency |
|---|---|---|---|
| MT-BR-001 | The hierarchy is Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse. | Confirmed | PRD PLT-002; glossary |
| MT-BR-002 | Every record is either Tenant-owned and belongs to exactly one Tenant, or Platform-owned because it exists for Platform governance, operation, security, subscription, provisioning, support, or audit purposes. A Platform-owned record may reference one or more Tenants for governance or evidence, but it is not shared Tenant business data and cannot be used by a Tenant User to view another Tenant. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27-RULE-002 |
| MT-BR-003 | Tenant business data is private by default and may be accessed only inside the authorized Tenant boundary. | Confirmed — Founder-approved Release 1 requirement | Founder direction; PRD BR-001 |
| MT-BR-004 | A User may perform Tenant operations only through an explicit active Tenant Membership approved under the MESP-28 access baseline. | Confirmed — Founder-approved Release 1 requirement | Founder direction; MESP-28 IAM-BR-002/003 |
| MT-BR-005 | A User establishes exactly one Tenant context per protected request, workspace, or authenticated session. Separate authorized sessions or workspaces may establish different Tenant contexts when supported, but one operation never combines Tenant contexts. | Confirmed — Founder-approved Release 1 requirement | Founder direction; MESP-28 IAM-BR-002/005 |
| MT-BR-006 | Tenant A state is never displayed, reused, submitted, or interpreted inside Tenant B. Switching away from Tenant A does not automatically delete valid Tenant A Roles, Permissions, scopes, drafts, filters, exports, files, cached results, search results, report results, notifications, or pending business state. Tenant A state may be available again only after returning to Tenant A and successfully re-evaluating authorization and lifecycle status; revoked, suspended, terminated, expired, or otherwise invalid state is not restored merely by return. | Confirmed — Founder-approved Release 1 requirement | Founder direction |
| MT-BR-007 | A client-supplied or changed Tenant identifier can never expand the User's authority. | Confirmed | PRD RULE-001; MESP-4 |
| MT-BR-008 | Cross-Tenant Tenant business-data reads, writes, searches, reports, files, exports, background jobs, notifications, integrations, audit access, support access, and cached/result reuse are denied by default. Platform-owned governance or evidence records may reference multiple Tenants only for an approved purpose, scope, authority, and audit trail, and never provide Tenant business-data access. | Confirmed — Founder-approved Release 1 requirement | Founder direction; PRD PLT-001 |
| MT-BR-009 | Platform Administrator status alone does not grant access to Tenant business data. Platform-owned operational, security, subscription, provisioning, support, and audit records remain purpose-bound, authorized, and audited even when they reference a Tenant. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27 actor boundary |
| MT-BR-010 | Platform-owned records do not become shared Tenant business data and cannot be used by a Tenant actor to access another Tenant. | Confirmed boundary | M27 section 6; glossary |
| MT-BR-011 | Any permitted cross-boundary review or support exception must identify the named actor, Tenant, case/purpose, exact scope, authority, effective interval, and evidence. | Confirmed | M27-REQ-045/048; MESP-28 |
| MT-BR-012 | Support access is named, Tenant-approved, case-bound, purpose-bound, least-privilege, and limited to a maximum of eight hours. | Confirmed | M27-REQ-045; IAM-BR-017 |
| MT-BR-013 | A support extension, another Tenant, another purpose, or another scope requires fresh approval. | Confirmed | M27-REQ-047; IAM-BR-019 |
| MT-BR-014 | Support authorization alone never grants export authority; export needs separate Permission, authorization, and explicit Tenant authorization for the named scope or artifact. | Confirmed | M27-REQ-095; IAM-BR-020 |
| MT-BR-015 | A Tenant may contain multiple Companies / Legal Entities, each retaining its own legal and accounting boundary. | Confirmed — Founder-approved Release 1 requirement | MESP-56; glossary |
| MT-BR-016 | Release 1 does not provide consolidation, intercompany automation, elimination entries, transfer pricing, or consolidated financial statements. | Confirmed — Founder-approved Release 1 requirement | MESP-56; PRD |
| MT-BR-017 | Wafra is Tenant #1 for validation evidence only; no Wafra-specific workflow, role, volume, permission, schema, or report becomes core behavior. | Confirmed — Founder-approved Release 1 requirement | PRD BR-003; M27-RULE-003 |
| MT-BR-018 | Retail POS is unavailable in Release 1, including through Tenant plans, imports, support, integrations, or feature requests. | Confirmed | PRD D-009; glossary |
| MT-BR-019 | A Suspended Tenant cannot perform ordinary interactive or asynchronous business operations. Required Platform-controlled safety and governance operations may continue where applicable without reactivating the Tenant or granting ordinary Tenant access. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27 lifecycle |
| MT-BR-020 | A Suspended Tenant cannot perform prohibited background, export, notification, integration, or other asynchronous business operations. Required Platform-controlled security logging, incident investigation, audit capture, monitoring/alerting, backup/restoration controls, retention/legal-hold enforcement, lifecycle notifications, access revocation, separately authorized controlled export, and termination/offboarding controls remain purpose-bound, authorized, and audited, subject to MESP-50 where applicable. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27-REQ-055 |
| MT-BR-021 | Suspension must address affected existing sessions so revoked Tenant authority cannot continue to operate. | Confirmed | M27-REQ-055/057; IAM-BR-023 |
| MT-BR-022 | Reactivation must reevaluate Users, Memberships, Roles/Permissions, Entitlements, sessions, integrations, background work, files, exports, and pending or interrupted work; a User's return does not itself restore invalid state. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27-REQ-057/058 |
| MT-BR-023 | Reactivation does not replay, duplicate, silently discard, or automatically reauthorize interrupted work. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27-REQ-057 |
| MT-BR-024 | Termination revokes active Tenant access and preserves required evidence; termination does not itself authorize physical deletion or purge. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27-RULE-022 |
| MT-BR-025 | No automated production purge may occur until MESP-50 approves the applicable retention, legal-hold, backup, restoration, scope, and evidence controls. | Deferred Gate | MESP-50; ADR-014 |
| MT-BR-026 | MESP-50 owns data-residency, retention, legal-hold, backup, restoration, purge, and residual-copy decisions; this BRD invents none of their values. | Deferred Gate | MESP-50 |
| MT-BR-027 | MESP-48 owns reference volumes and supported-volume evidence; this BRD invents no volume limits, concurrency limits, storage values, or performance promise. | Deferred Gate | MESP-48; M27-REQ-041 |
| MT-BR-028 | Tenant isolation applies consistently to application behavior, persistence meaning, reports, files, exports, jobs, notifications, integrations, audit evidence, caches, and support operations, while Platform-owned safety/governance records remain separately governed and cannot expose Tenant business data. | Confirmed — Founder-approved Release 1 requirement | Founder direction; PRD PLT-001 |
| MT-BR-029 | A Tenant-bound business operation must retain the initiating Tenant and scope boundary and must not infer a wider boundary from a later client value or retry. Platform-controlled safety/governance work retains its approved Platform purpose, Tenant references, scope, and audit boundary. | Confirmed — Founder-approved Release 1 requirement | Founder direction; M27-REQ-026 |
| MT-BR-030 | A Tenant export must be bounded, authorized, attributable, and prevented from including another Tenant's data. | Confirmed | M27-REQ-059; IAM-BR-020 |
| MT-BR-031 | Audit evidence for context selection, denial, lifecycle, support, export, suspension, reactivation, termination, migration, and Platform safety/governance decisions is retrievable within its authorized Tenant or Platform review scope and cannot be edited by Tenant Users. | Confirmed | PRD BR-011; MESP-28; MESP-38 |
| MT-BR-032 | An ambiguous Tenant, identity, organizational, or membership migration mapping remains quarantined until its accountable owner approves the reconciled outcome. | Confirmed | PRD migration baseline; MESP-40 |

## 15. Tenant Status and Transitions

The following transition controls are business guards. They do not prescribe a state-machine implementation.

| From | Trigger | Required business guard | To / result | Classification |
|---|---|---|---|---|
| No Tenant | Approved onboarding request | Identity, duplicate, ownership, and required input checks pass. | Draft | Confirmed |
| Draft | Completeness and duplicate checks pass | Authoritative provisioning is approved and traceable. | Provisioning | Confirmed |
| Provisioning | Foundation and configuration stages succeed | All stages and failures have recorded evidence; no partial Active use. | Configuration Required or Ready for Activation | Confirmed |
| Ready for Activation | Readiness and acknowledgements pass | Authorized actor records the activation decision. | Active | Confirmed |
| Active | Approved commercial or administrative remedy | Trigger, deadline, notice, permitted access, and next action are recorded. | Grace Period | Confirmed |
| Active | Approved security, legal, or administrative restriction | Reason, authority, effective time, access mode, session/job result, and notice are recorded; ordinary Tenant business work is restricted while required Platform safety/governance work remains available where applicable. | Suspended | Confirmed — Founder-approved Release 1 requirement |
| Suspended | Suspension reason cleared | Users, memberships, sessions, integrations, jobs, files, exports, and pending work are reevaluated; invalid state is not restored merely by return. | Reactivated | Confirmed — Founder-approved Release 1 requirement |
| Reactivated | Restoration checks pass | Restored and still-restricted capabilities are recorded. | Active | Confirmed — Founder-approved Release 1 requirement |
| Active or Suspended | Authorized offboarding request | Export disposition and closure authority are recorded; no purge is implied. | Export Requested or Termination Pending | Confirmed |
| Termination Pending | Closure controls pass | Access closure, legal-hold review, and accountable approval are complete. | Terminated | Confirmed |
| Terminated | Retention or legal-hold obligation exists | Operational reuse is prohibited and evidence remains governed. | Retained | Deferred Gate |
| Retained | MESP-50 purge conditions pass | Certified scope, no hold, required approvals, notice, cooling-off, and rechecks are evidenced. | Purge Approved | Deferred Gate |
| Purge Approved | Later approved execution begins | Execution remains prohibited until the MESP-50-controlled conditions are complete. | Purged or reviewed failure | Deferred Gate |

**Classification: Confirmed.** Any transition not listed or not satisfying its guard is denied and recorded as a business-control outcome. A failed transition never broadens another Tenant's access.

## 16. Validation Rules

| Validation condition | Required outcome | Classification | Owner / dependency |
|---|---|---|---|
| Tenant identity is incomplete or duplicated | Do not create a second authoritative Tenant; hold for reviewed resolution. | Confirmed | MESP-27 |
| Tenant context has no active Membership | Deny the specific request, workspace, or session operation without exposing protected data. | Confirmed — Founder-approved Release 1 requirement | MESP-28/MESP-29 |
| Target Tenant identifier differs from trusted Membership context | Re-evaluate authority and deny any expansion. | Confirmed | PRD RULE-001 |
| User has more than one Membership | Require one selected active Tenant context per request, workspace, or session; separate authorized contexts may coexist without being combined. | Confirmed — Founder-approved Release 1 requirement | MESP-28 |
| Target organization scope is unrelated, inactive, or owned by another Tenant | Reject the assignment or operation. | Confirmed | MESP-30/MESP-28 |
| Tenant is Suspended or Terminated | Deny ordinary interactive and asynchronous Tenant business operations; allow only explicitly authorized Platform safety/governance operations that do not reactivate the Tenant or grant ordinary access. | Confirmed — Founder-approved Release 1 requirement | MESP-27 |
| Session or background operation survives a critical suspension/revocation decision | Invalidate or stop the affected authority before allowing further work. | Confirmed | MESP-27/MESP-28 |
| Support request lacks case, named User, Tenant approval, exact scope, purpose, or interval | Reject or hold the request. | Confirmed | M27-REQ-045 |
| Support request exceeds eight hours or seeks extension | Require fresh approval; do not silently extend. | Confirmed | M27-REQ-047 |
| Support identity requests an export without separate authority | Deny the export. | Confirmed | M27-REQ-095 |
| Export includes another Tenant or lacks bounded scope | Do not generate or release the artifact; separately authorized Platform-controlled export must remain purpose-bound and audited. | Confirmed — Founder-approved Release 1 requirement | MESP-29/MESP-39 |
| Reactivation leaves users, memberships, integrations, jobs, files, exports, or pending work unreviewed | Keep the Tenant restricted, record the missing owner/evidence, and do not restore invalid state merely because a User returns. | Confirmed — Founder-approved Release 1 requirement | MESP-29 |
| Migration identity or scope is ambiguous | Quarantine the mapping until accountable approval. | Confirmed | MESP-40 |
| Volume or capacity value lacks MESP-48 evidence | Do not publish it as a supported production limit. | Deferred Gate | MESP-48 |
| Retention, residency, hold, backup, restoration, or purge value lacks MESP-50 approval | Keep the value/operation gated and do not invent a default. | Deferred Gate | MESP-50 |

## 17. Support Access Boundary

**Classification: Confirmed.** Support access is not a standing Platform privilege. It is a separate business decision with all of the following required: a named personal support User, one named case, one Tenant, one business purpose, exact requested scope, Tenant approval, applicable Platform/security approval, a maximum eight-hour interval, visible start/end, and full evidence of activity, denials, downloads, expiry, revocation, and closure.

**Classification: Confirmed.** Support access for Tenant A cannot be used against Tenant B. A new Tenant, purpose, scope, or extension requires fresh approval. Shared credentials, hidden superusers, unrestricted impersonation, and unaudited access are prohibited.

**Classification: Confirmed.** Support authorization alone never grants export authority. A support identity requesting a Tenant export must separately hold the applicable export Permission, separate export authorization, and explicit Tenant authorization for the named export scope or artifact.

## 18. Background Jobs and Asynchronous Work

**Classification: Confirmed — Founder-approved Release 1 requirement.** A background job, report preparation, export, notification, integration action, import, retry, or other asynchronous operation is a Tenant-scoped business operation. Its initiating Tenant, scope, purpose, and authorization remain part of the business decision throughout the work. Required Platform-controlled safety and governance operations have their own approved Platform purpose, scope, Tenant references where needed, and audit boundary.

**Classification: Confirmed — Founder-approved Release 1 requirement.** Before an asynchronous business operation executes or retries, its lifecycle eligibility and access boundary must be re-evaluated. A Suspended or Terminated Tenant cannot use asynchronous work to bypass its status. Required Platform safety/governance work may continue without reactivating the Tenant or granting ordinary access. A failed or held job remains visible with its Tenant and reason and cannot affect another Tenant.

**Classification: Confirmed.** A retry must continue or safely compensate the original business request; it must not create a second Tenant, duplicate export, duplicate notification, or cross-Tenant effect. Detailed retry and durable-work design is downstream.

## 19. Files, Exports, Reports, Notifications, and Integrations

| Surface | Business requirement | Classification | Deferred detail |
|---|---|---|---|
| Files and attachments | A file belongs to the Tenant and authorized scope that owns its business use; another Tenant cannot read, change, search, or download it. Valid Tenant A files are not deleted merely by switching away, but cannot be displayed or used in Tenant B. | Confirmed — Founder-approved Release 1 requirement | MESP-39 / later specification |
| Exports | An export is bounded, attributable, authorized, and cannot include another Tenant. Support authorization alone is insufficient. | Confirmed | MESP-27/MESP-39 |
| Reports | A report is evaluated in one Tenant and authorized scope; asynchronous results do not become reusable in another Tenant. Valid Tenant A reports remain Tenant A state and require re-evaluation before reuse after return. | Confirmed — Founder-approved Release 1 requirement | MESP-36 |
| Notifications | A notification is scoped to its initiating Tenant and recipient boundary; delivery failure does not authorize cross-Tenant disclosure. Required Platform lifecycle notifications may continue during suspension as purpose-bound governance operations. | Confirmed — Founder-approved Release 1 requirement | MESP-27 / Notifications detail |
| Integrations | An integration action carries its approved Tenant and scope; another Tenant cannot be reached through credentials, configuration, or retry. Required Platform safety/governance controls remain separately authorized and audited. | Confirmed — Founder-approved Release 1 requirement | MESP-39 |
| Cached/results state | Cached results, search results, filters, drafts, prepared exports, files, and pending artifacts are never displayed, reused, submitted, or interpreted in another Tenant. Switching away does not automatically delete valid state; return requires successful authorization and lifecycle re-evaluation. | Confirmed — Founder-approved Release 1 requirement | Downstream specification |

## 20. Audit Evidence

**Classification: Confirmed.** The business must be able to retrieve evidence for at least the following material Tenant events:

| Event | Minimum evidence | Classification |
|---|---|---|
| Tenant creation and onboarding | Request, identity, duplicate outcome, owner, status, validation, and next action. | Confirmed |
| Tenant activation | Readiness evidence, acknowledgements, approver, effective time, and resulting access mode. | Confirmed |
| Context selection and switching | User, request/workspace/session, source/target Tenant context, membership decision, time, outcome, and safe denial reason where applicable. | Confirmed |
| Cross-Tenant denial | Actor, attempted boundary, safe outcome, time, reason category, and escalation result where applicable. | Confirmed |
| Platform safety/governance operations | Purpose, authority, referenced Tenant(s), scope, time, outcome, revocation/closure, and evidence for logging, audit capture, monitoring, backup/restoration, retention/legal hold, lifecycle notice, access revocation, controlled export, and offboarding controls. | Confirmed |
| Suspension and reactivation | Reason, authority, effective time, affected sessions/jobs, required safety/governance operations, notices, restored capabilities, and still-restricted capabilities. | Confirmed |
| Support | Case, named User, Tenant, purpose, scope, approvals, activity, downloads, expiry/revocation, and closure. | Confirmed |
| Export | Scope, requester, approver, generator, artifact/manifest, integrity evidence, expiry, downloads, and Tenant authorization. | Confirmed |
| Termination/offboarding | Export disposition, access closure, hold review, status, owner, and retention/purge gate. | Confirmed |
| Migration | Source, mapping, duplicate/ambiguity result, owner, reconciliation, and sign-off. | Confirmed |

**Classification: Confirmed.** Tenant Users cannot edit or delete Tenant isolation and access evidence. **Classification: Deferred Gate.** Retention, residency, legal hold, backup, restoration, and purge treatment remain under MESP-50; this BRD authorizes no automated evidence purge.

## 21. Migration and Tenant Onboarding

**Classification: Confirmed.** Tenant onboarding and migration must identify the source system, data owner, Tenant target, identity and organization mapping, duplicate outcome, rejected rows or records, exception owner, reconciliation evidence, and business sign-off. The business baseline requires configuration and master data to be loaded and reviewed before operational use; open documents, opening positions, or other later-domain data follow their owning BRD and migration gate.

**Classification: Confirmed.** An ambiguous Tenant, User, Membership, Company / Legal Entity, Branch, or Warehouse mapping is quarantined and cannot authorize access or activate operations until an accountable owner approves the reconciled result. The BRD does not prescribe scripts, table structures, or load technology.

**Classification: Confirmed.** Wafra is the first validation Tenant for evidence and rehearsal. The onboarding approach must remain repeatable for future Saudi Tenants without Tenant-specific core behavior.

## 22. Suspension, Reactivation, Termination, and Offboarding

### Suspension

**Classification: Confirmed — Founder-approved Release 1 requirement.** Suspension restricts ordinary interactive and asynchronous Tenant business operation for a recorded reason and must apply consistently to new access, existing sessions, background work, imports, exports, notifications, integrations, and other prohibited paths. It preserves Tenant data and evidence while required Platform-controlled safety and governance operations continue where applicable, including security logging and incident investigation, audit-evidence capture, monitoring and alerting, backup/restoration controls, retention and legal-hold enforcement, lifecycle notifications, access revocation, separately authorized controlled export, and termination/offboarding controls. These operations do not reactivate the Tenant or grant ordinary Tenant access, remain purpose-bound and audited, and remain subject to MESP-50 where retention, backup, restoration, legal hold, residency, or purge is involved.

### Reactivation

**Classification: Confirmed — Founder-approved Release 1 requirement.** Reactivation is not a simple status toggle. It reevaluates Users, Memberships, Roles/Permissions, Entitlements, sessions, integrations, background work, files, exports, and pending/interrupted work. Only explicitly restored capabilities become available; interrupted work requires deliberate review, and invalid state is not restored merely because a User returns.

### Termination and offboarding

**Classification: Confirmed — Founder-approved Release 1 requirement.** Termination revokes active access and operational capability while preserving required evidence. Export disposition, access/integration closure, legal-hold review, and accountable approval must be recorded before the Tenant enters governed retention. Platform-controlled termination and offboarding controls may continue after suspension or termination without granting ordinary Tenant access.

**Classification: Deferred Gate.** Retention, residency, backup, restoration, legal hold, purge scope, cooling-off, final notice, and physical purge execution are governed by MESP-50. No production purge occurs from this BRD.

## 23. MESP-48 and MESP-50 Gates

| Gate | What this BRD records | What it does not decide | Classification |
|---|---|---|---|
| MESP-48 - reference volumes and supported-volume evidence | Volume evidence is required before publishing supported limits or capacity promises; categories include people/organization, master data, transactions, files, work, integrations, and concurrency. | No numeric limits, storage values, concurrency promises, p95 targets, or Wafra-specific threshold. | Deferred Gate |
| MESP-50 - residency, retention, legal hold, backup, restoration, and purge | Production Tenant data and evidence remain governed, isolated, reviewable, and non-purgeable by this BRD alone. | No hosting region, retention period, legal conclusion, backup schedule, restoration claim, cooling-off duration, purge scope, or physical purge execution. | Deferred Gate |

**Classification: Deferred Gate.** MESP-48 and MESP-50 are not implementation shortcuts. Their required evidence and approvals must be complete before any affected production commitment or irreversible operation is approved.

## 24. Business Acceptance Scenarios

These are business acceptance scenarios, not automated test instructions or a test-case document.

1. **MT-AC-001 - Tenant creation:** **Confirmed.** Given an approved, complete, unique onboarding request, when Tenant creation is authorized, then one reviewable Tenant boundary is recorded and no second authoritative Tenant is created.
2. **MT-AC-002 - Tenant activation:** **Confirmed.** Given required setup, ownership, acknowledgement, and validation evidence, when activation is approved, then the Tenant becomes Active with an auditable decision and no unresolved activation gate is silently bypassed.
3. **MT-AC-003 - Authorized context selection:** **Confirmed — Founder-approved Release 1 requirement.** Given a User with one active Membership, when a protected request, workspace, or authenticated session begins, then exactly one authorized Tenant context is established for that context and the action is evaluated inside it.
4. **MT-AC-004 - Multiple-membership selection:** **Confirmed — Founder-approved Release 1 requirement.** Given a User with active Memberships in Tenant A and Tenant B, when a protected request, workspace, or authenticated session begins, then the User must select or enter one Tenant context for that context; separate authorized sessions or workspaces may use different Tenants, but no operation combines them.
5. **MT-AC-005 - Tenant switching isolation:** **Confirmed — Founder-approved Release 1 requirement.** Given a User changes from Tenant A to Tenant B, when the Tenant B context is established, then Tenant A Roles, scopes, drafts, filters, exports, files, cached results, search results, report results, notifications, and pending state are never displayed, reused, submitted, or interpreted in Tenant B. Valid Tenant A drafts or working state are not automatically deleted and may be available after returning to Tenant A only following successful authorization and lifecycle re-evaluation; invalid state is not restored.
6. **MT-AC-006 - Unauthorized switch:** **Confirmed.** Given a User has no active Membership in Tenant B, when the User attempts to switch to Tenant B, then the switch and protected action are denied without revealing Tenant B data.
7. **MT-AC-007 - Changed Tenant identifier:** **Confirmed.** Given a valid User in Tenant A submits a Tenant identifier for Tenant B, when authorization is evaluated, then the identifier does not expand authority and the attempted boundary is evidenced safely.
8. **MT-AC-008 - Cross-Tenant read:** **Confirmed — Founder-approved Release 1 requirement.** Given a User is authorized for Tenant A in a protected request, workspace, or session, when the User requests a Tenant B business record, then the read is denied without disclosure.
9. **MT-AC-009 - Cross-Tenant write:** **Confirmed — Founder-approved Release 1 requirement.** Given a User is authorized for Tenant A, when the User attempts to create or change a Tenant B business record, then the write is denied and Tenant A authority is unchanged.
10. **MT-AC-010 - Cross-Tenant search:** **Confirmed — Founder-approved Release 1 requirement.** Given a search is initiated in Tenant A, when it includes Tenant B business data, then the result excludes Tenant B and the attempt is controlled and evidenced.
11. **MT-AC-011 - Cross-Tenant report:** **Confirmed — Founder-approved Release 1 requirement.** Given a report is prepared for Tenant A, when the report would include Tenant B business data, then it is denied or corrected before release and no mixed-Tenant result is exposed.
12. **MT-AC-012 - Cross-Tenant export:** **Confirmed.** Given an export is authorized for Tenant A, when its scope includes Tenant B, then generation or release is denied and the bounded scope remains visible to authorized reviewers.
13. **MT-AC-013 - Cross-Tenant file:** **Confirmed — Founder-approved Release 1 requirement.** Given a file belongs to Tenant A, when a Tenant B context requests it, then access and download are denied without disclosure; switching away from Tenant A does not itself delete the valid Tenant A file.
14. **MT-AC-014 - Cross-Tenant background job:** **Confirmed — Founder-approved Release 1 requirement.** Given a business job was initiated for Tenant A, when a retry or execution attempts Tenant B, then the work remains Tenant A-bound or is denied; Tenant B is not affected.
15. **MT-AC-015 - Cross-Tenant notification or integration:** **Confirmed — Founder-approved Release 1 requirement.** Given a notification or integration action belongs to Tenant A, when it targets Tenant B, then the action is denied or held and no cross-Tenant payload is delivered; required Platform safety/governance notifications remain separately authorized and audited.
16. **MT-AC-016 - Cross-Tenant cached/result reuse:** **Confirmed — Founder-approved Release 1 requirement.** Given a cached result, filter, draft, prepared export, report, file, or pending artifact belongs to Tenant A, when a User switches to Tenant B, then it is never displayed, reused, submitted, or interpreted as Tenant B data. Valid Tenant A state is not automatically deleted and requires successful Tenant A authorization/lifecycle re-evaluation before return; invalid state is not restored.
17. **MT-AC-017 - Platform Administrator data boundary:** **Confirmed — Founder-approved Release 1 requirement.** Given a Platform Administrator operates Tenant catalogue, subscription, provisioning, support, security, audit, or operational records that may reference a Tenant, when the actor attempts Tenant business-data access without separate purpose-bound authorization, then access is denied. Platform-owned records remain governed, authorized, and audited and cannot be used by a Tenant User to view another Tenant.
18. **MT-AC-018 - Approved support access:** **Confirmed.** Given a named case, named support User, Tenant approval, exact scope, purpose, and interval no longer than eight hours, when support starts, then only the approved Tenant boundary is available and activity is evidenced.
19. **MT-AC-019 - Support against another Tenant:** **Confirmed.** Given support is approved for Tenant A, when the same identity targets Tenant B, then access is denied without disclosure and the security outcome is recorded.
20. **MT-AC-020 - Support export separation:** **Confirmed.** Given support authorization exists, when the support User requests an export, then export remains denied until separate export Permission, authorization, and explicit Tenant authorization are present.
21. **MT-AC-021 - Tenant suspension:** **Confirmed — Founder-approved Release 1 requirement.** Given an authorized Tenant suspension, when an ordinary interactive or asynchronous business operation is attempted, then it is denied, the Tenant data remains preserved, and the reason/evidence are visible to authorized reviewers. Required Platform-controlled safety/governance operations may continue for their approved purpose without reactivating the Tenant or granting ordinary access.
22. **MT-AC-022 - Existing session after suspension:** **Confirmed.** Given a User has an active session when the Tenant is suspended, when the User attempts further prohibited work, then affected authority is revoked or invalidated before use and the outcome is evidenced.
23. **MT-AC-023 - Background work after suspension:** **Confirmed — Founder-approved Release 1 requirement.** Given a business background job belongs to a Suspended Tenant, when execution is due, then prohibited business work does not run, its state/reason remains visible, and no other Tenant is affected. Required Platform safety/governance work may continue for an approved purpose and remains audited and MESP-50-gated where applicable.
24. **MT-AC-024 - Reactivation review:** **Confirmed — Founder-approved Release 1 requirement.** Given a suspension reason is cleared, when reactivation is requested, then Users, Memberships, integrations, background work, files, exports, sessions, and pending work are reevaluated before Active operation resumes; returning to the Tenant does not itself restore invalid state.
25. **MT-AC-025 - Interrupted work after reactivation:** **Confirmed — Founder-approved Release 1 requirement.** Given work was pending at suspension, when the Tenant is reactivated, then the work is deliberately reviewed and is not duplicated, silently discarded, or automatically reauthorized.
26. **MT-AC-026 - Termination:** **Confirmed — Founder-approved Release 1 requirement.** Given export disposition, access closure, and required hold review are complete, when termination takes effect, then active Tenant access is revoked, evidence is preserved, required Platform offboarding controls may continue, and no purge is implied.
27. **MT-AC-027 - Multiple legal entities:** **Confirmed — Founder-approved Release 1 requirement.** Given one Tenant contains two legal entities, when each is configured or reviewed, then each retains its own legal/accounting boundary and no consolidation or intercompany automation is created.
28. **MT-AC-028 - Wafra-neutral behavior:** **Confirmed.** Given Wafra is Tenant #1 for validation, when the same Tenant lifecycle and isolation behavior is applied to another eligible Tenant, then no Wafra-specific rule is required.
29. **MT-AC-029 - Migration ambiguity:** **Confirmed.** Given a Tenant, User, Membership, or organization mapping is ambiguous, when migration validation runs, then the mapping is quarantined, an accountable owner is assigned, and activation waits for approval.
30. **MT-AC-030 - Audit retrieval:** **Confirmed.** Given an authorized reviewer requests Tenant context, denial, lifecycle, support, export, or offboarding evidence, when the evidence is retrieved, then actor, Tenant, scope, action, time, outcome, and safe decision context are available without Tenant-user editing or automated purge.
31. **MT-AC-031 - MESP-48 volume gate:** **Deferred Gate.** Given a production volume or supported-capacity claim is requested, when MESP-48 evidence is not approved, then no numeric limit or promise is published.
32. **MT-AC-032 - MESP-50 retention/purge gate:** **Deferred Gate.** Given a retention, legal-hold, restoration, or purge action is requested, when MESP-50 approval is absent, then the value or irreversible operation remains gated and no production purge executes.
33. **MT-AC-033 - Retail POS exclusion:** **Confirmed.** Given any Tenant plan, context, import, support, or integration request, when Retail POS is requested, then it remains unavailable and is routed to product change control.

## 25. Founder Decisions

### Approved founder direction carried forward

The following are confirmed founder-approved Release 1 direction, not new open decisions: the Platform/Tenant/Company/Branch/Warehouse hierarchy; Tenant-owned records belong to exactly one Tenant; Platform-owned governance, operation, security, subscription, provisioning, support, and audit records may reference Tenants without becoming shared Tenant business data; private-by-default Tenant data; exactly one Tenant context per protected request, workspace, or authenticated session without a global one-Tenant-at-a-time restriction; no cross-Tenant working-state reuse or automatic deletion of valid state on switch; client identifiers cannot expand authority; default-deny cross-Tenant behavior; Platform Administrator data boundary; named eight-hour support; multiple legal entities without consolidation; Wafra validation-only treatment; B2B-only Release 1; suspension/revocation/reactivation/termination evidence and Platform safety/governance continuity; and MESP-48/MESP-50 gates.

### Founder decision register

| ID | Genuine decision requiring Hossam | Required outcome | Status |
|---|---|---|---|
| MT-OD-001 | Approve this MESP-29 v0.2 document, including the four Tenant-isolation clarifications, as the Release 1 Multi-Tenancy and Tenant Lifecycle business baseline. | Founder approval recorded by Hossam on 2 August 2026; MESP-29 may move to Done. | Approved |

No other new founder decision is requested by this baseline. MESP-48 and MESP-50 remain deferred gates owned by their respective decisions and are not converted into invented defaults here.

## 26. Source Conflicts

| ID | Source conflict or ambiguity | Resolution / treatment | Status |
|---|---|---|---|
| SC-001 | Jira MESP-29 originally said not to move In Progress before entry criteria, while the later founder authorization explicitly permitted MESP-4 and MESP-29 to move In Progress for the draft. | The later founder authorization permitted the controlled draft; the approval recorded in this baseline closes the requirements task while implementation remains separately gated. | Resolved by founder authorization and approval |
| SC-002 | Jira/PRD references use a `Final_Approved_Baseline` filename alias, and older documents use `MiniERPSaaSPlatform_PRD_v1.2.docx`, while the canonical repository path is now `docs/MESP_PRD_v1.2.docx`. | All names resolve to the identical unchanged PRD v1.2 file; only the path moved. The aliases are retained for provenance and the Jira references remain stale. | Nonblocking |
| SC-003 | MESP-27 contains the broader Platform Administration lifecycle, Plan, support, export, retention, and purge model, while MESP-29 owns Tenant isolation and context. | MESP-29 applies the Tenant boundary and lifecycle consequences without redefining MESP-27 Plan, Entitlement, or production-gate policy. | Resolved boundary |
| SC-004 | The glossary marks Access Scope and Separation of Duties as Draft for BRD Validation, while MESP-28 records their approved Release 1 access meaning and MESP-38 owns detailed control catalogues. | MESP-29 consumes the approved identity boundary and does not create a new Access Scope or SoD catalogue. | Nonblocking boundary |

No blocking source conflict remains for the approved baseline. The four records are retained for traceability.

## 27. Coverage Checklist

| MESP-29 required output / functional area | Covered section(s) | Coverage status | Deferred owner, when applicable |
|---|---|---|---|
| Business purpose | 2-3 | Covered | None |
| Actors and responsibilities | 7 | Covered | Detailed Role/SoD catalogues: MESP-28/MESP-38 |
| Tenant terminology and ownership | 8-9 | Covered | Company/Branch/Warehouse detail: MESP-30 |
| Platform-owned governance and operational records | 2, 7-9, 14, 17, 20, 24 | Covered | Detailed Platform administration: MESP-27; security/audit catalogue: MESP-38 |
| Tenant creation and onboarding | 10, 12.1, 21 | Covered | Migration detail: MESP-40 |
| Tenant activation | 10, 12.2, 15-16 | Covered | Platform configuration detail: MESP-27 |
| Tenant context selection and switching | 11, 12.3-12.4 | Covered | Identity/session design: MESP-28 downstream; separate sessions/workspaces remain implementation-supported where applicable |
| Tenant-bound working state | 2, 11-13, 16, 19, 22, 24 | Covered | Detailed state handling: later Lean Implementation Specification |
| Tenant isolation and default denial | 2, 4, 14, 16, 19-20, 24 | Covered | Detailed enforcement: later Lean Implementation Specification |
| Alternative and exception paths | 13 | Covered | MESP-27/MESP-38/MESP-40 gates where named |
| Tenant lifecycle and status transitions | 10, 15, 22 | Covered | MESP-27 lifecycle authority; MESP-50 production gate; Platform safety/governance continuity during suspension |
| Validation rules | 16 | Covered | None beyond named gates |
| Permissions and access boundary | 7, 11, 14, 17 | Covered | User/Permission detail: MESP-28 |
| Approval and separation of duties | 7, 17, 20 | Covered | Detailed SoD catalogue: MESP-38 |
| Inventory impact | 9, 23-24 | Covered as boundary | Inventory transactions: MESP-33 |
| Accounting impact | 9, 23-24 | Covered as boundary | Accounting rules: MESP-34 |
| Multi-currency impact | 9, 23-24 | Covered as boundary | Currency behavior: MESP-34/MESP-54 |
| Saudi localization impact | 2, 8, 21, 23-24 | Covered as generic boundary | Saudi Country Pack and legal validation: MESP-37/MESP-49 |
| Reports and KPIs | 19-20, 24 | Covered as business boundary | Reporting detail: MESP-36 |
| Audit evidence | 7, 17, 20, 22, 24 | Covered | Platform safety/governance evidence; retention/purge: MESP-50; detailed catalogue: MESP-38 |
| Files and exports | 17, 19, 20, 24 | Covered | Files/integration detail: MESP-39 |
| Notifications and integrations | 18-19, 24 | Covered | Delivery/interface detail: MESP-39/later specification |
| Background jobs and safety operations | 12.9, 14, 18-20, 22, 24 | Covered | Durable-work implementation detail: downstream; MESP-50 where applicable |
| Migration and tenant onboarding | 12.1, 13, 21, 24 | Covered | MESP-40 |
| MESP-48 volume gate | 14, 16, 23, MT-AC-031 | Covered | MESP-48 |
| MESP-50 retention/purge gate | 10, 14-16, 20, 22-24, MT-AC-032 | Covered | MESP-50 |
| Given/When/Then acceptance scenarios | 24 | Covered | Business scenarios only; no test document |
| Open decisions and owner approval | 25, 28 | Covered | Hossam approval: MT-OD-001 |

**Coverage result: Covered.** Every MESP-29 required output and every approved functional area has a section, business rule, process, validation, or acceptance scenario. Deferred owners are explicit; no gap is filled by invention.

## 28. Founder Approval Record

### Review checklist

| Review item | Approval status |
|---|---|
| Tenant hierarchy and ownership boundaries | Approved |
| Tenant context selection and switching | Approved with four clarifications |
| Default-deny isolation across all named surfaces | Approved |
| Tenant-bound working state | Approved with return/re-evaluation rule |
| Platform-owned records and governance boundary | Approved |
| Tenant lifecycle, suspension, reactivation, and termination | Approved with safety-operation continuity |
| Support access and export separation | Approved |
| Multiple legal entities without Release 1 consolidation | Approved |
| Wafra-neutral and Retail POS exclusion | Approved |
| MESP-48 and MESP-50 gates | Deferred Gate preserved |
| Acceptance scenarios | Approved |
| MT-OD-001 founder approval | Approved |

### Approval block

| Field | Record |
|---|---|
| Approver | Hossam |
| Approval date | 2 August 2026 |
| Approved version | v0.2 — Approved Release 1 Baseline |
| Decision result | MT-OD-001 Approved |
| Implementation authorization | Not granted by this BRD; downstream implementation remains separately gated |
| MESP-29 Jira status | Done after approval; do not start MESP-30 or implementation from this document |

**Approval record:** Hossam approved `docs/13_Multi_Tenancy_BRD.md` v0.2 on 2 August 2026. The four Tenant-isolation clarifications are incorporated; MESP-48 and MESP-50 Deferred Gates remain unchanged. MESP-30, MESP-58, all implementation work, and Sprint activity remain outside this document.
