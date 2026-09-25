# Mini ERP SaaS Platform — Organization and Company Structure BRD

## 1. Document Control

| Field | Value |
|---|---|
| Document | Organization and Company Structure Business Requirements Document |
| Jira | MESP-30 — Produce Organization and Company Structure BRD |
| Parent Epic | MESP-5 — EPIC 05 - Organization and Company Structure |
| Version | v0.2 — Approved Release 1 Baseline |
| Status | Approved Release 1 Baseline |
| Accountable owner | Hossam, Product Owner and founder approver |
| Prepared by | Luna Max, Senior Business Analyst and Product Requirements Lead |
| Date | 2 August 2026 |
| Canonical product baseline | `docs/MESP_PRD_v1.2.docx`, PRD v1.2 Final Approved Baseline (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Mandatory vocabulary | `docs/00_ERP_Business_Glossary.md` |
| Related approved BRDs | `docs/11_SaaS_Platform_Administration_BRD.md`; `docs/12_Identity_and_Access_BRD.md`; `docs/13_Multi_Tenancy_BRD.md` |
| Architecture reference | `docs/01_Technology_Architecture_Baseline.md` — constraint reference only |
| Delivery reference | `docs/94_Product_Delivery_Master_Plan.md` |
| Jira state at approval | MESP-5 In Progress; MESP-30 Done; MESP-30 remains outside all Sprints |
| Classification summary | 46 organization rules: 44 Confirmed, 0 Founder Default, 0 Open Decision, 2 Deferred Gate; 35 business acceptance scenarios; 7 approved founder decisions; 5 resolved source-conflict records |

This is a business-requirements document. It authorizes no API, database, UI,
code, automated test, Sprint, implementation Jira work, or Foundation Lean
Implementation Specification. MESP-29 remains the authority for Tenant
isolation and MESP-28 remains the authority for User, Membership, Role,
Permission, and Access Scope meaning. Hossam approved this Release 1 baseline
on 2 August 2026; implementation remains separately gated by the Product
Delivery Master Plan and the combined Foundation Release 1 Lean Implementation
Specification.

## 2. Executive Summary

**Classification: Confirmed.** Organization Structure defines the reusable
business hierarchy inside each Tenant:

`Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse`

Every Company / Legal Entity belongs to exactly one Tenant. A Tenant may hold
multiple Companies / Legal Entities, and each Company is a distinct legal and
accounting boundary. Every Branch belongs to exactly one Company, and every
Warehouse belongs to exactly one Branch. No relationship in this hierarchy may
cross a Tenant boundary.

**Classification: Confirmed.** Company / Legal Entity owns the organization
identity and the fiscal-accounting context. Finance owns detailed fiscal
period, closing, posting, and accounting behavior. Branch is the operating
location used for responsibility, reporting, permission scope, numbering scope,
and business attribution. Warehouse is the controlled stock-holding location;
Inventory owns stock movements and balances at that location.

**Classification: Confirmed.** Departments are optional organizational
classifications owned by exactly one Company / Legal Entity. A Department may
be used across that Company's Branches, but never belongs directly to a Branch
or Warehouse, never crosses a Tenant, and never grants a User access,
Permission, Role, Membership, or Access Scope.

**Classification: Confirmed.** Each Company / Legal Entity has exactly one
active fiscal calendar, one required operating time zone, and one active
functional currency. Fiscal configuration, the recommended Saudi defaults,
and currency values require explicit authorized confirmation; Finance owns
detailed fiscal, currency, exchange-rate, period, posting, and change behavior.

**Classification: Confirmed.** The normal Release 1 document-numbering boundary
is Company / Legal Entity plus Document Type. An optional Branch subdivision
requires an owning-domain, country-rule, or approved-process justification;
Tenant-level sequences are limited to Tenant or Platform administration records.
Numbers are never reused after allocation, including after cancellation,
rejection, or voiding; gaps remain attributable and auditable.

**Classification: Confirmed.** Release 1 provides no financial consolidation,
intercompany automation, elimination entries, transfer pricing, or consolidated
financial statements. Organization configuration must remain reusable for
Wafra and future Tenants, with no Wafra-specific Company, Branch, Warehouse,
Department, calendar, numbering, workflow, or permission behavior. Retail POS
remains excluded.

This BRD defines business meaning, ownership, lifecycle, relationships, access
scope impact, numbering, fiscal, time-zone, and currency boundaries, evidence,
migration expectations, and business acceptance scenarios. It does not define detailed Finance,
Inventory, Procurement, Sales, tax, technical, or user-interface behavior.

## 3. Business Purpose

The purpose of MESP-30 is to establish one understandable organization model
that can be reused by every Release 1 Tenant and consumed consistently by
Finance, Inventory, Procurement, B2B Sales, Reporting, Identity and Access,
Migration, and the Saudi Country Pack.

| Objective | Required business outcome | Classification | Source / owner |
|---|---|---|---|
| Legal accountability | Each Company / Legal Entity has one Tenant owner and its own legal and accounting boundary. | Confirmed | PRD PLT-002; MESP-56; glossary |
| Operational hierarchy | Branches operate inside one Company and Warehouses operate inside one Branch. | Confirmed | PRD PLT-002; glossary |
| Safe organizational change | Lifecycle changes preserve historical references and prevent new work against inactive units. | Confirmed | Founder direction; PRD §5.2 |
| Controlled access | Organization scope follows Tenant → Company → Branch → Warehouse downward only. | Confirmed | PRD BR-010; MESP-28 IAM-OD-022 |
| Reusable configuration | Names, codes, departments, calendars, numbering, time zones, and currencies remain governed configuration, not customer-specific code. | Confirmed | PRD PLT-013/014; MESP-27; founder approval |
| Traceable identity | Organization and document references remain understandable and auditable after later configuration changes. | Confirmed | PRD PLT-004/008; glossary |
| Controlled migration | Ambiguous organization mappings remain quarantined until an accountable owner approves them. | Confirmed | PRD migration baseline; MESP-40 |
| Explicit gates | Volume claims and retention, residency, legal-hold, backup, restoration, and purge values are not invented. | Deferred Gate | MESP-48; MESP-50 |

## 4. Scope

The following areas are in scope for this business baseline:

| In-scope area | Business requirement | Classification | Owner / dependency |
|---|---|---|---|
| Company / Legal Entity | Identity, legal/accounting boundary, ownership, status, fiscal context, and relationships. | Confirmed | MESP-30; Finance detail in MESP-34 |
| Branch | Operating location identity, Company relationship, lifecycle, reporting and scope boundary. | Confirmed | MESP-30 |
| Warehouse | Controlled stock-location identity, Branch relationship, lifecycle, and Inventory boundary. | Confirmed | MESP-30; Inventory detail in MESP-33 |
| Department | Optional responsibility/cost classification owned by exactly one Company and usable across that Company's Branches. | Confirmed | MESP-30; Finance cost-center detail in MESP-34 |
| Hierarchy integrity | One-Tenant ownership and no cross-Tenant parent/child relationship. | Confirmed | MESP-29 boundary; MESP-30 detail |
| Organization lifecycle | Draft, Active, Inactive, and Closed meanings, parent-lifecycle effects, descendant revalidation, and historical preservation. | Confirmed | MESP-30; MESP-27 lifecycle handoff |
| Organization access scope | Downward scope impact and no upward inheritance. | Confirmed | MESP-28; MESP-30 |
| Organization identity and duplicate control | Controlled names and codes with duplicate checking inside the approved business boundary. | Confirmed | MESP-30; exact formats remain downstream-owned |
| Fiscal-calendar boundary | Exactly one active Company fiscal calendar, explicit confirmation, and Finance ownership of detailed behavior. | Confirmed | MESP-34 |
| Document-numbering boundary | Immutable identity, Company + Document Type default boundary, justified optional Branch subdivision, traceability, and non-reuse. | Confirmed | MESP-30; MESP-34; MESP-37/49 where statutory |
| Operating time zone | One required Company operating time zone; Branches and Warehouses inherit it in Release 1. | Confirmed | Founder approval; Saudi Country Pack; downstream timestamp design |
| Functional currency | Exactly one active Company functional currency with explicit confirmation and Finance-owned change behavior. | Confirmed | Founder approval; MESP-34 |
| Historical references | Inactive or closed organization units remain referenceable by historical documents, reports, files, and audit evidence. | Confirmed | Domain BRDs; MESP-50 gate for retention |
| Migration and reconciliation | Source mapping, duplicate handling, quarantine, approval, and reconciliation of organization structure. | Confirmed | MESP-40 |
| Reports and audit | Organization register, integrity exceptions, lifecycle, scope, numbering, and change evidence. | Confirmed | MESP-36; MESP-38 |

## 5. Out of Scope

| Exclusion | Classification | Owner / reason |
|---|---|---|
| Tenant isolation, Tenant lifecycle, or cross-Tenant context mechanism | Out of Scope | MESP-29 |
| User, Membership, Role, Permission, authentication, session, or technical Access Scope design | Out of Scope | MESP-28 and downstream design |
| Detailed fiscal periods, posting, closing, chart of accounts, tax, or accounting calculations | Out of Scope | MESP-34 and MESP-37 |
| Inventory transactions, stock ledger, valuation, transfers, counts, receipts, or adjustments | Out of Scope | MESP-33 |
| Procurement, B2B Sales, Finance transactions, or document transaction workflows | Out of Scope | MESP-32, MESP-34, and MESP-35 |
| API contracts, endpoint names, database tables/columns/keys/schemas, query design, or framework behavior | Out of Scope | Architecture and later Lean Implementation Specification |
| Angular screens, navigation, controls, visual layout, or UI component specifications | Out of Scope | Later design work |
| Source code, automated tests, test-case documents, implementation Stories, Enablers, Sprint, or Pull Request | Out of Scope | Delivery gates |
| Exact code formats, technical sequence-generator behavior, or volume limits | Deferred Gate / downstream-owned boundary | MESP-48 and owning domains; no values are invented here |
| Retention, residency, legal hold, backup, restoration, purge duration, or physical purge execution | Deferred Gate | MESP-50 |
| Financial consolidation, intercompany automation, elimination entries, transfer pricing, or consolidated statements | Out of Scope | Founder-approved MESP-56 boundary |
| Retail POS, cashier, retail shift, cash drawer, or walk-in checkout behavior | Out of Scope | Release 1 B2B-only decision |
| Wafra-specific organization behavior | Out of Scope | Wafra is validation-only |

## 6. Source Traceability

| Source | Relevant authority used in this BRD | Sections / identifiers | Classification |
|---|---|---|---|
| Jira MESP-30 | Required Organization BRD outputs and business-only constraint. | 1-36 | Confirmed |
| Jira MESP-5 | Epic hierarchy, organization ownership, fiscal/numbering scope, and BRD gate. | 2, 4, 10, 23-24 | Confirmed |
| PRD PLT-002 | Platform → Tenant → Company / Legal Entity → Branch → Warehouse; multiple legal entities without consolidation. | 2, 4, 9-10 | Confirmed |
| PRD PLT-003/013 | Authorized master-data configuration, duplicate detection, company identity, departments, branches, warehouses, templates, and numbering. | 4, 14, 19-24 | Confirmed |
| PRD PLT-004 and RULE-013 | Immutable internal identity, human-readable business number, configured scope, and no reuse after posting. | 19, 24 | Confirmed |
| PRD PLT-005/008 and BR-010/011 | Approval history, organization scope, audit evidence, and authorized context. | 21-22, 29 | Confirmed |
| PRD §5.2 and §6 | Company accounting boundary, historical snapshots, lifecycle, and immutable historical effects. | 9, 11-13, 18-19, 24-26 | Confirmed |
| `docs/11_SaaS_Platform_Administration_BRD.md` | Tenant activation dependency on organization readiness, configuration history, numbering profile, and Platform/Tenant ownership boundary. | 3, 7-9, 13, 16, 23, 33 | Confirmed |
| `docs/12_Identity_and_Access_BRD.md` | Downward-only Access Scope, no upward inheritance, inactive-scope denial, and organization ownership boundary. | 7, 9, 14, 19, 26-27 | Confirmed |
| `docs/13_Multi_Tenancy_BRD.md` | Tenant ownership, no cross-Tenant relationships, migration quarantine, and MESP-48/MESP-50 gates. | 2, 4, 8-9, 21-24 | Confirmed |
| `docs/01_Technology_Architecture_Baseline.md` | Organization owns identity/relationships and numbering scopes; Finance owns fiscal calendars; Inventory owns stock effects. | 6-8 | Confirmed boundary |
| `docs/Decisions.md` | ADR-001 source ownership, ADR-003 tenant isolation, ADR-005 authorization, ADR-011 localization, ADR-014 retention/purge. | 6, 9, 21, 27, 29 | Confirmed dependency |
| `docs/00_ERP_Business_Glossary.md` | Controlled meanings of Company, Legal Entity, Branch, Warehouse, Department, Fiscal Calendar, Access Scope, and Document Number. | 7-9, 18-24 | Confirmed / Draft for BRD Validation |
| `docs/90_MVP_Founder_Decision_Pack.md` | Approved MESP-56 multiple-legal-entity boundary and organization BRD decision timing. | 2, 6, 8-9 | Confirmed |
| `docs/94_Product_Delivery_Master_Plan.md` | Sequential BRD delivery, MESP-30 next activity, no implementation before approval. | 1-3, 9-12 | Confirmed |

## 7. Actors and Responsibilities

The actors below describe business accountability. They do not create new
Roles or Permissions; MESP-28 owns the access catalogue and MESP-38 owns the
detailed Separation of Duties catalogue.

| Actor | Organization responsibility | Permitted business scope | Prohibited or constrained actions | Classification |
|---|---|---|---|---|
| Hossam / Product Owner and founder approver | Approves this Release 1 baseline and the recorded organization decisions. | Product governance. | Approval authorizes downstream requirements/design preparation only; implementation remains separately gated. | Confirmed |
| Platform Administrator | Coordinates Tenant setup readiness and Platform-owned provisioning evidence. | Platform metadata and explicitly authorized operations. | Does not gain Tenant organization business-data access merely from Platform status. | Confirmed |
| Tenant Administrator | Creates and maintains approved Company, Branch, Warehouse, and Department configuration for one Tenant. | One authorized Tenant and its approved hierarchy. | Cannot create a parent or child in another Tenant, bypass lifecycle checks, or change Platform-owned records. | Confirmed |
| Company / Legal Entity owner | Provides accountable legal identity, accounting context, fiscal calendar, operating time zone, functional currency, and organization approvals for the Company. | One Company / Legal Entity and its approved descendants. | Cannot silently rewrite historical ownership or create consolidation behavior. | Confirmed |
| Branch owner / operating manager | Maintains Branch identity, operating status, and authorized operating responsibility under one Company. | One Company and its descendant Warehouses. | Cannot move a used Branch across Companies by rewriting history. | Confirmed |
| Warehouse owner / operator | Maintains Warehouse identity and operational readiness for its Branch. | One Branch and its Warehouse scope. | Cannot assign stock or new work to an inactive Warehouse or move used ownership silently. | Confirmed |
| Department owner / business requester | Uses an approved Department classification for responsibility or cost attribution where enabled. | One owning Company and any of that Company's Branch contexts. | Department membership alone does not grant access, Permission, Role, Membership, or Access Scope. | Confirmed |
| Finance / control owner | Validates Company accounting boundary, fiscal context, numbering implications, and finance dependencies. | Finance review scope. | Does not redefine Company/Branch/Warehouse identity or create consolidation in Release 1. | Confirmed |
| Inventory owner | Validates Warehouse location meaning and downstream stock implications. | Inventory review scope. | Does not make a Warehouse belong to more than one Branch. | Confirmed |
| Identity and Access owner | Applies approved organization scopes to Users and Memberships. | MESP-28 authorization scope. | Does not grant upward inheritance or authorize inactive organization units for new work. | Confirmed |
| Migration / onboarding owner | Maps source organization data, resolves duplicates, and owns reconciliation exceptions. | Named source and target Tenant scope. | Ambiguous mappings remain quarantined until accountable approval. | Confirmed |
| Auditor / reviewer | Reviews organization, numbering, lifecycle, scope, and migration evidence. | Authorized review scope. | Read-only review; cannot mutate organization state. | Confirmed |

## 8. Organization Terminology

| Term | Business meaning used by MESP-30 | Boundary | Classification |
|---|---|---|---|
| Platform | The single Mini ERP SaaS service. | Outer hierarchy level; not a Tenant or Company. | Confirmed |
| Tenant | The isolated customer subscription and data boundary. | Every Company belongs to exactly one Tenant; MESP-29 owns isolation. | Confirmed |
| Company / Legal Entity | The operating and legally registered business boundary inside a Tenant. | Owns legal identity, accounting context, and statutory responsibility; Company and Legal Entity are the same hierarchy level. | Confirmed |
| Branch | An operating location or business unit inside one Company. | Not a separate Legal Entity or independent accounting ledger in Release 1. Branch-level operational reporting, registration references, statutory identifiers, or country-specific reporting may exist under the Company when defined by Finance or the Saudi Country Pack; this does not create independent consolidation behavior. | Confirmed |
| Warehouse | A physical or logical stock-holding location inside one Branch. | Stock is meaningful at Warehouse level; a Warehouse belongs to exactly one Branch. | Confirmed |
| Department | Optional internal grouping used for responsibility and, where enabled, cost attribution. | Owned by exactly one Company; may be used across that Company's Branches; not a Tenant-isolation boundary, Branch, or Cost Center. | Confirmed |
| Fiscal Calendar | A Company financial-year and period definition. | Exactly one active calendar governs an applicable accounting date; explicit confirmation is required; detailed periods and closing belong to Finance. | Confirmed |
| Operating time zone | The required Company operating time-zone context for business timestamps. | One per Company; Branches and Warehouses inherit it in Release 1; no child override. | Confirmed |
| Functional currency | The Company's active accounting currency context. | Exactly one active currency per Company; explicit confirmation is required; Finance owns changes and calculations. | Confirmed |
| Document Number | Human-readable identifier within an approved document sequence. | Default boundary is Company / Legal Entity + Document Type; optional Branch subdivision requires justification; not the immutable internal identity and never reused after allocation. | Confirmed |
| Organization code / name | Controlled human-readable identity for a Company, Branch, Warehouse, or Department. | Duplicate checked within its approved boundary; exact formats remain downstream-owned and are not defined here. | Confirmed |
| Access Scope | The organization boundary within which a User's approved Permission applies. | Downward from Tenant to Company, Branch, and Warehouse only; MESP-28 owns meaning. | Confirmed |
| Historical reference | A link from a document, report, file, ledger, or audit record to the organization that applied at the time. | Remains understandable after deactivation or later configuration changes. | Confirmed |
| Inactive | A lifecycle state in which an organization unit cannot receive new Users, documents, jobs, integrations, or transactions. | Authorized historical read-only access remains controlled by current IAM scope and never reactivates the unit. | Confirmed |

## 9. Ownership Boundaries

| Business concept | Organization meaning | Owning domain / boundary | Handoff or constraint |
|---|---|---|---|
| Tenant | Top-level subscription and isolation boundary. | MESP-29 / MESP-27 | MESP-30 never creates cross-Tenant relationships. |
| Company / Legal Entity identity | Legal identity, parent Tenant, statutory identity, and organization relationship. | MESP-30 Organization | Finance consumes the boundary for books and posting rules. |
| Fiscal-accounting context | Company-level context for one active fiscal calendar, one functional currency, and accounting boundary. | Company owns meaning; Finance (MESP-34) owns detailed behavior. | Explicit confirmation is required; missing or invalid context blocks affected operations; no accounting calculation is defined here. |
| Branch identity | Company-owned operating location and scope boundary. | MESP-30 Organization | Procurement, Sales, Reporting, and Identity consume approved Branch references. |
| Warehouse identity | Branch-owned stock-location identity and scope boundary. | MESP-30 Organization | Inventory owns stock effects and ledger behavior. |
| Department | Optional responsibility/cost classification owned by one Company. | MESP-30 for meaning; MESP-34 for cost behavior | May be used across the Company's Branches; never grants access or crosses Tenant/Company. |
| Access Scope | User-level organizational boundary. | MESP-28 Identity and Access | MESP-30 supplies valid organization relationships; no upward inheritance; historical access remains current-IAM controlled. |
| Numbering scope | Business boundary within which a sequence is evaluated. | MESP-30 defines the Company + Document Type default; owning domain defines justified variations | Optional Branch subdivision and fiscal-year reset require approved justification; numbers never reuse. |
| Operating time zone | Company-level time context inherited by descendants. | MESP-30 meaning; downstream design for timestamps | Saudi `Asia/Riyadh` may be prefilled but requires explicit confirmation; no Branch/Warehouse override. |
| Functional currency | Company-level active currency context. | MESP-30 boundary; Finance (MESP-34) behavior | Saudi SAR may be prefilled but requires explicit confirmation; no consolidation currency. |
| Platform provisioning metadata | Readiness and activation evidence. | MESP-27 SaaS Administration | Organization readiness is one activation dependency; no Platform ownership of Tenant business configuration. |
| Historical document effect | Reference and snapshot of organization meaning at the time of business activity. | Owning transactional domain | Deactivation never deletes history; detailed transaction behavior remains downstream. |

## 10. Approved Hierarchy

The approved hierarchy is:

```text
Platform
└── Tenant
    └── Company / Legal Entity (one or more)
        └── Branch (one or more per Company)
            └── Warehouse (one or more per Branch)
```

| Relationship | Required cardinality and rule | Classification |
|---|---|---|
| Platform → Tenant | The Platform hosts multiple isolated Tenants; MESP-29 owns the boundary. | Confirmed |
| Tenant → Company / Legal Entity | Every Company belongs to exactly one Tenant; a Tenant may contain multiple Companies. | Confirmed |
| Company → Branch | Every Branch belongs to exactly one Company; one Company may have multiple Branches. | Confirmed |
| Branch → Warehouse | Every Warehouse belongs to exactly one Branch; one Branch may have multiple Warehouses. | Confirmed |
| Warehouse → Branch | A Warehouse cannot belong to multiple Branches. | Confirmed |
| Department → Company | Every Department belongs to exactly one Company / Legal Entity and may be used across that Company's Branches; it never belongs directly to a Branch or Warehouse. | Confirmed |

No Company, Branch, Warehouse, Department, document, job, integration, report,
file, or access scope may use a parent from another Tenant. A Department also
cannot be used outside its owning Company. A client-selected identifier cannot
create or expand a relationship. Organization changes must preserve the
applicable Tenant and parent boundary.

## 11. Company / Legal Entity Lifecycle

### 11.1 Business meanings

| State | Business meaning | Permitted outcome | Classification |
|---|---|---|---|
| Draft | Company identity is being prepared and has not passed required readiness checks. | Complete, review, activate, or cancel according to approved process. | Confirmed |
| Active | Company is approved for supported downstream operations. | Maintain, deactivate, or close through controlled action. | Confirmed |
| Inactive | Company is not eligible for new Users, documents, jobs, integrations, or transactions. | Review, reactivate where supported, or close. | Confirmed |
| Closed | Company operations are ended under an accountable business decision. | Preserve historical references; no ordinary new work. | Confirmed |

### 11.2 Company activation

Activation requires, at minimum, a unique and complete identity, one Tenant
owner, valid hierarchy relationships, required ownership and validation checks,
one explicitly confirmed fiscal calendar, one explicitly confirmed operating
time zone, one explicitly confirmed functional currency, and any approved legal,
numbering, country, or access dependencies. A Company is not operational before
those checks pass. The exact field catalogue and approval matrix are downstream
business-owner outputs, not invented here.

### 11.3 Company deactivation and closure

Deactivation blocks assignment to new Users, documents, jobs, integrations, or
transactions and blocks new work for every Branch and Warehouse descendant.
It preserves historical documents, reports, files, ledger links, and audit
evidence. It does not silently delete records, rewrite prior Company ownership,
or rewrite descendant statuses or historical state. Reactivating the Company
does not automatically restore descendants; each affected descendant requires
its own lifecycle and readiness revalidation. Authorized historical read-only
access remains controlled by current Tenant Membership, Role, Permission, and
Access Scope and never reactivates the Company.

## 12. Branch Lifecycle

### 12.1 Business meanings

Branch uses the same supported lifecycle vocabulary as Company — Draft, Active,
Inactive, and Closed where the approved operating model supports each state.
Branch status cannot override its Company ownership or Tenant boundary.

### 12.2 Branch activation

A Branch may become Active only after its Company parent is valid and not
Inactive or Closed for the operation, the Branch identity is complete and
duplicate-checked, ownership and scope checks pass, and required business
approvals/evidence are recorded. A Branch cannot be operational while its
Company is Inactive or Closed.

### 12.3 Branch deactivation and parent changes

An Inactive or Closed Branch cannot receive new Users, documents, jobs,
integrations, or transactions and blocks new work for every Warehouse
descendant. Historical references remain intact. Deactivating or closing a
Company also blocks this Branch; neither parent change silently rewrites the
Branch status or historical state. Reactivating the Company does not restore the
Branch. The Branch must pass its own lifecycle and readiness revalidation before
new work resumes. A Branch with historical or transactional use cannot have its
Company parent silently rewritten. A material change uses controlled migration
or closure-and-recreation. An unused Draft Branch may change parent only under
the approved ORG-OD-007 conditions.

## 13. Warehouse Lifecycle

### 13.1 Business meanings

Warehouse uses Draft, Active, Inactive, and Closed where supported by approved
business meaning. Warehouse identity remains under exactly one Branch for its
entire historical interpretation.

### 13.2 Warehouse activation

A Warehouse may become Active only after its Branch parent is valid and not
Inactive or Closed for the operation, the Warehouse identity is complete and
duplicate-checked, ownership and scope checks pass, and the required readiness
evidence is recorded. Inventory owns the downstream stock effect; this section
does not define stock transactions. A Warehouse cannot be operational while its
Branch or Company parent is Inactive or Closed.

### 13.3 Warehouse deactivation and parent changes

An Inactive or Closed Warehouse cannot receive new Users, documents, jobs,
integrations, or transactions or new inventory/accounting activity. Historical
stock, inventory, documents, reports, files, and audit references remain
preserved. Deactivating or closing its Branch or Company blocks the Warehouse's
new work without silently rewriting the Warehouse status or historical state.
Reactivating a parent does not restore the Warehouse; the Warehouse must pass
its own lifecycle and readiness revalidation before new work resumes. A
Warehouse with historical or transactional use cannot have its Branch parent
silently rewritten. A material change uses controlled migration or closure-and-
recreation. An unused Draft Warehouse may change parent only under the approved
ORG-OD-007 conditions.

## 14. Department Model

Departments are optional organizational classifications used to attribute
responsibility and, where enabled, cost. Each Department belongs to exactly one
Company / Legal Entity and may be used across multiple Branches belonging to
that Company. A Department is not a Company, Branch, Warehouse, Cost Center,
User, Role, Permission, or Tenant-isolation boundary.

The approved Department boundary is:

- A Department belongs to exactly one Company / Legal Entity and the same
  Tenant.
- A Department does not belong directly to a Branch or Warehouse, but a
  transaction may reference both the Company's Department and its Branch or
  Warehouse operating context.
- Department association does not grant a User any Permission, Role,
  Membership, or Access Scope.
- A Department cannot be used to bypass Company, Branch, Warehouse, Role,
  Permission, Entitlement, lifecycle, or document-state controls.
- Department identity must be duplicate-checked inside its owning Company;
  exact code formats remain downstream-owned and are not invented here.

No Department behavior is allowed to create a new isolation boundary or cross
its owning Company or Tenant.

## 15. Main Business Processes

### ORG-PR-001 — Configure a Company / Legal Entity

| Element | Business requirement |
|---|---|
| Trigger | An authorized Tenant Administrator starts organization setup or an approved migration supplies a Company. |
| Preconditions | One Tenant context; accountable owner; complete identity; no unresolved duplicate; required parent and country/accounting dependencies known; one fiscal calendar, operating time zone, and functional currency explicitly confirmed. |
| Main process | Capture the Company / Legal Entity identity, Tenant ownership, legal/accounting context, required labels and references, status, fiscal calendar, operating time zone, functional currency, and evidence; validate duplicates and relationships; submit for activation review. |
| Outcome | A Draft or Active Company record with an accountable decision and preserved evidence. |
| Exceptions | Duplicate, missing ownership, invalid Tenant, incomplete required dependency, or ambiguous migration mapping. |

### ORG-PR-002 — Activate, deactivate, or close a Company

Activation, deactivation, and closure are controlled status decisions. Activation
requires readiness checks and a valid parent. Deactivation blocks new
assignments and new work for the Company and all descendants but preserves
history. Closure ends ordinary future use without deleting historical
references. Parent changes do not silently rewrite descendant state; reactivation
requires each descendant's own lifecycle and readiness revalidation. Every
material decision records actor, reason, effective time, scope, outcome, and
approval where required.

### ORG-PR-003 — Configure a Branch

An authorized Tenant Administrator establishes a Branch under one valid
Company, validates identity and duplicate rules, records responsible ownership,
and submits the Branch for activation. A Branch cannot use a Company in another
Tenant or become Active or receive new work while its parent is invalid,
Inactive, or Closed.

### ORG-PR-004 — Configure a Warehouse

An authorized Tenant Administrator establishes a Warehouse under one valid
Branch, validates identity and duplicate rules, records operational ownership,
and submits it for activation. A Warehouse cannot use a Branch in another
Tenant or become Active or receive new work while its Branch or Company parent
is invalid, Inactive, or Closed.

### ORG-PR-005 — Create and associate a Department

An authorized actor creates an optional Department owned by exactly one Company
within one Tenant. The Department may be referenced with any Branch or
Warehouse operating context belonging to that Company. Duplicate rules and
lifecycle status are validated; Department linkage does not grant access,
Permission, Role, Membership, or Access Scope.

### ORG-PR-006 — Request a material parent change

The requester identifies the current parent, proposed parent, reason, affected
history, and business impact. An unused Draft Branch or Warehouse may change
parent only when it has never been Active, has no transactions, stock, documents,
files, reports, numbering usage, integrations, jobs, User assignments, or
historical references, the proposed parent is valid in the same Tenant, and the
before/after relationship, actor, reason, and time are audited. Otherwise the
change is routed to controlled migration or closure-and-recreation. No history
is rewritten by a simple parent edit.

### ORG-PR-007 — Evaluate organization access scope

Identity and Access evaluates the selected Tenant, Membership, Role,
Permission, and organization scope. A valid parent scope may apply downward to
authorized descendants; it never inherits upward. Inactive or invalid units
cannot receive new assignments or new work.

### ORG-PR-008 — Establish fiscal and numbering boundaries

The Company records one active fiscal calendar, one operating time zone, one
active functional currency, and the approved numbering boundary needed by
downstream domains. Fiscal configuration is explicitly confirmed. The normal
numbering boundary is Company / Legal Entity + Document Type; an optional Branch
subdivision requires an approved justification. Finance and the owning domain
confirm detailed fiscal, currency, and numbering behavior. Missing required
context blocks the affected operation; no silent default is introduced.

### ORG-PR-009 — Migrate organization structure

The Migration owner maps source Tenant, Company, Branch, Warehouse, Department,
status, fiscal, and numbering information to the approved organization model.
Duplicates, invalid parents, missing ownership, and ambiguous mappings are
quarantined until an accountable owner approves the reconciled result.

## 16. Alternative and Exception Paths

| ID | Condition | Required business treatment | Classification / owner |
|---|---|---|---|
| ORG-EX-001 | Duplicate Company, Branch, Warehouse, Department, code, or name | Stop authoritative creation or activation, show a safe reviewed exception, and retain the duplicate decision. | Confirmed; MESP-30 |
| ORG-EX-002 | Parent belongs to another Tenant | Deny the relationship; do not expose or mutate the other Tenant's organization. | Confirmed; MESP-29 |
| ORG-EX-003 | Parent is inactive, closed, or invalid | Block activation or new assignment/work and identify the responsible correction. | Confirmed; MESP-30 / downstream domain |
| ORG-EX-004 | Used Branch or Warehouse parent change | Preserve existing history and require controlled migration or closure-and-recreation. | Confirmed; MESP-40 / owning domain |
| ORG-EX-005 | Unused Draft Branch or Warehouse parent change | Permit only when the approved no-use, same-Tenant, valid-parent, and audit conditions are all met; otherwise require controlled migration or closure-and-recreation. | Confirmed; ORG-OD-007 |
| ORG-EX-006 | Department parent not supported by the approved Company-owned model | Reject the association; a Department cannot belong directly to a Branch or Warehouse or cross its owning Company/Tenant. | Confirmed; ORG-OD-001 |
| ORG-EX-007 | Required fiscal calendar, operating time zone, functional currency, or numbering sequence is missing or unconfirmed | Block the affected operation and identify the dependency; do not silently invent or assume a value. | Confirmed; MESP-34 / owning domain |
| ORG-EX-008 | Migration mapping is ambiguous | Quarantine the mapping, assign an owner, reconcile, and require approval before activation or use. | Confirmed; MESP-40 |
| ORG-EX-009 | Wafra-specific request | Treat Wafra as validation evidence only and express the reusable product requirement instead. | Confirmed; Product Owner |
| ORG-EX-010 | Retail POS request | Reject as Release 1 scope and route to future product change control. | Out of Scope; Product Owner |

## 17. Organization Business Rules

The register below contains business rules only. It introduces no API, schema,
query, storage, or framework prescription.

| ID | Business rule | Classification | Source / dependency |
|---|---|---|---|
| ORG-BR-001 | The approved hierarchy is Platform → Tenant → Company / Legal Entity → Branch → Warehouse. | Confirmed | PRD PLT-002; MESP-5 |
| ORG-BR-002 | Every Company / Legal Entity belongs to exactly one Tenant. | Confirmed | PRD PLT-002; MESP-56 |
| ORG-BR-003 | A Tenant may contain multiple Companies / Legal Entities. | Confirmed | PRD PLT-002; MESP-56 |
| ORG-BR-004 | Each Company / Legal Entity is a distinct legal and accounting boundary; Release 1 provides no consolidation, intercompany automation, elimination entries, transfer pricing, or consolidated financial statements. | Confirmed | MESP-56; PRD |
| ORG-BR-005 | Every Branch belongs to exactly one Company / Legal Entity. | Confirmed | PRD PLT-002; glossary |
| ORG-BR-006 | Every Warehouse belongs to exactly one Branch, and a Warehouse cannot belong to multiple Branches. | Confirmed | PRD PLT-002; glossary |
| ORG-BR-007 | Company, Branch, Warehouse, Department, and their business relationships must never cross Tenant boundaries. | Confirmed | MESP-29; PRD PLT-001 |
| ORG-BR-008 | Approved organization access follows Tenant → Company → Branch → Warehouse downward scope. | Confirmed | MESP-28 IAM-OD-022 |
| ORG-BR-009 | Organization scope never inherits upward from Warehouse to Branch, Branch to Company, or Company to Tenant. | Confirmed | MESP-28 IAM-OD-022 |
| ORG-BR-010 | An Inactive or Closed Company, Branch, or Warehouse cannot be assigned to new Users, documents, jobs, integrations, or transactions. | Confirmed | Founder-approved Release 1 direction |
| ORG-BR-011 | Deactivation preserves historical references and audit evidence. | Confirmed | Founder direction; PRD §5.2/BR-011 |
| ORG-BR-012 | Deactivation does not delete historical transactions, reports, files, or evidence. | Confirmed | Founder direction; MESP-50 gate |
| ORG-BR-013 | When an organization unit has historical or transactional use, its parent ownership cannot be silently changed; a material change uses controlled migration or closure-and-recreation. | Confirmed | Founder approval; MESP-40 |
| ORG-BR-014 | An unused Draft Branch or Warehouse may change parent only when the approved no-use, same-Tenant, valid-parent, and audit conditions are all satisfied; otherwise controlled migration or closure-and-recreation is required. | Confirmed | Founder approval; ORG-OD-007 |
| ORG-BR-015 | Organization codes and names are controlled and duplicate-checked within their approved business boundary. | Confirmed | PRD PLT-003/013; exact formats remain downstream-owned |
| ORG-BR-016 | Exact code formats, numbering patterns, and sequence values must not be invented in this BRD. | Confirmed | Founder direction; MESP-48/49 boundaries |
| ORG-BR-017 | Departments are optional organizational classifications owned by exactly one Company / Legal Entity and usable across that Company's Branches. | Confirmed | Founder approval; glossary |
| ORG-BR-018 | A Department belongs directly to exactly one Company / Legal Entity, never directly to a Branch or Warehouse, and may be referenced with operating contexts from that Company. | Confirmed | Founder approval; ORG-OD-001 |
| ORG-BR-019 | Departments do not become a new Tenant-isolation boundary. | Confirmed | Founder direction; MESP-29 |
| ORG-BR-020 | A Department association does not automatically grant a User access or Permission. | Confirmed | MESP-28; founder direction |
| ORG-BR-021 | Company / Legal Entity owns the fiscal-accounting context; detailed periods, closing, posting, and accounting behavior belong to Finance. | Confirmed | PRD §5.2; MESP-34 |
| ORG-BR-022 | Each Company / Legal Entity has exactly one active fiscal calendar; historical or future effective versions may be retained, but only one calendar governs an applicable accounting date. | Confirmed | Founder approval; ORG-OD-002; MESP-34 |
| ORG-BR-023 | A fiscal calendar must be explicitly confirmed before affected financial operations are enabled; the Saudi Country Pack may prefill Gregorian January–December as a recommendation, never as a silent approval. | Confirmed | Founder approval; ORG-OD-003; MESP-34 |
| ORG-BR-024 | Every required business document retains one immutable internal identity and, where required, one human-readable business number with traceable sequence ownership and no reuse of an issued number. | Confirmed | PRD PLT-004; RULE-013; glossary |
| ORG-BR-025 | The normal Release 1 business-number sequence boundary is Company / Legal Entity + Document Type; a Branch subdivision is allowed only when required by the owning domain, Saudi Country Pack, or approved business process. | Confirmed | Founder approval; ORG-OD-004; MESP-34/37/49 |
| ORG-BR-026 | Numbering does not reset automatically by default; a fiscal-year reset may be explicitly configured for a specific sequence, calendar-year reset is not assumed, gaps are permitted but attributable, and allocated numbers are never reused. | Confirmed | Founder approval; ORG-OD-005; MESP-34/37/49 |
| ORG-BR-027 | Each organization unit may use Draft, Active, Inactive, and Closed states only where those states have approved business meaning. | Confirmed | Founder approval; MESP-30 |
| ORG-BR-028 | No Company, Branch, or Warehouse becomes operational before required ownership, hierarchy, identity, duplicate, dependency, and validation checks pass. | Confirmed | Founder approval; MESP-27 |
| ORG-BR-029 | Historical documents and evidence retain the organization reference and meaning applicable when the business event occurred. | Confirmed | PRD §5.2; glossary |
| ORG-BR-030 | Authorized Users may retrieve historical documents, reports, files, stock history, accounting history, and audit evidence linked to Inactive or Closed units when current Tenant Membership, Role, Permission, and Access Scope allow it; historical access never reactivates the unit. | Confirmed | Founder approval; ORG-OD-006; MESP-28/34/36/38 |
| ORG-BR-031 | An ambiguous organization migration mapping remains quarantined until an accountable owner approves the reconciled outcome. | Confirmed | PRD migration baseline; MESP-40 |
| ORG-BR-032 | Wafra is validation-only; no Wafra-specific Company, Branch, Warehouse, Department, fiscal-calendar, numbering, permission, or report behavior becomes core behavior. | Confirmed | PRD BR-003; MESP-24 |
| ORG-BR-033 | Retail POS remains excluded from Release 1, including organization or numbering behavior introduced only for retail checkout. | Confirmed | PRD D-009; glossary |
| ORG-BR-034 | MESP-48 owns reference volumes and supported-volume evidence; this BRD invents no volume, capacity, concurrency, or performance values. | Deferred Gate | MESP-48 |
| ORG-BR-035 | MESP-50 owns retention, residency, legal hold, backup, restoration, and purge values; this BRD invents none and authorizes no physical purge. | Deferred Gate | MESP-50 |
| ORG-BR-036 | Material organization creation, activation, deactivation, closure, parent change, numbering/fiscal configuration, migration decision, and exception outcome must be attributable and auditable. | Confirmed | PRD PLT-008/BR-011; MESP-38 |
| ORG-BR-037 | Organization Structure owns the business identity and relationships of Company, Branch, and Warehouse; downstream domains own their transaction effects and must preserve the approved organization boundary. | Confirmed | Architecture source-ownership reconciliation; PRD §5.1 |
| ORG-BR-038 | A child organization unit cannot be operational or receive new work when its required Company or Branch parent is Inactive or Closed. | Confirmed | Founder approval; parent lifecycle clarification |
| ORG-BR-039 | Deactivating or closing a Company blocks new work for its Branches and Warehouses without silently rewriting their stored status or historical state. | Confirmed | Founder approval; parent lifecycle clarification |
| ORG-BR-040 | Deactivating or closing a Branch blocks new work for its Warehouses without silently rewriting their stored status or historical state. | Confirmed | Founder approval; parent lifecycle clarification |
| ORG-BR-041 | Reactivating a Company or Branch does not automatically restore descendant eligibility or status. | Confirmed | Founder approval; parent lifecycle clarification |
| ORG-BR-042 | Each affected descendant must pass its own lifecycle and readiness revalidation before new work resumes after a parent lifecycle change. | Confirmed | Founder approval; parent lifecycle clarification |
| ORG-BR-043 | Historical organization references and descendant historical state remain unchanged across parent deactivation, closure, and reactivation. | Confirmed | Founder approval; PRD §5.2 |
| ORG-BR-044 | Every Company / Legal Entity has one required operating time zone; Saudi `Asia/Riyadh` may be prefilled but requires explicit confirmation, and Branches and Warehouses inherit it without Release 1 overrides. | Confirmed | Founder approval; Saudi Country Pack; downstream timestamp design |
| ORG-BR-045 | Every Company / Legal Entity has exactly one active functional currency; Saudi SAR may be prefilled but requires explicit confirmation, Finance owns currency changes and calculations, and no consolidation currency exists in Release 1. | Confirmed | Founder approval; MESP-34 |
| ORG-BR-046 | Changing functional currency after financial activity exists is not a simple configuration edit; it requires a future controlled Finance/migration decision and preserves historical currency meaning. | Confirmed | Founder approval; MESP-34; migration boundary |

## 18. Status Transitions

### 18.1 Organization-unit states

| Current state | Trigger / precondition | Result | Guardrail |
|---|---|---|---|
| Draft | Required identity and ownership are being prepared. | Remains non-operational until checks pass. | No new operational transaction, assignment, or integration may rely on an unapproved unit. |
| Draft → Active | Ownership, hierarchy, identity, duplicate, explicit fiscal/time-zone/currency dependencies where applicable, and required validation checks pass. | Unit becomes eligible for supported operations. | Accountable decision and evidence are required. |
| Active → Inactive | Authorized business, operational, or governance decision. | New assignments and new work are blocked; descendants are also blocked by parent state; history is preserved. | Descendant statuses and history are not silently rewritten. |
| Inactive → Active | Reactivation checks and required approval pass where supported. | Unit may be eligible again after its own readiness revalidation. | Descendants are not automatically restored. |
| Active / Inactive → Closed | Controlled closure decision. | Ordinary future use ends; historical references remain. | No purge or historical rewrite is implied. |
| Any invalid transition | Missing parent, duplicate, unresolved migration, prohibited scope, or missing required dependency. | Transition is denied or held with an accountable exception. | No silent default is introduced. |

### 18.2 Parent-change transition

An unused Draft Branch or Warehouse parent change is permitted only under the
approved ORG-OD-007 no-use, same-Tenant, valid-parent, and audit conditions. A
used Branch or Warehouse parent change never rewrites the historical
relationship; it uses controlled migration or closure-and-recreation. The
specific migration and document effects are owned by MESP-40 and the affected
domain BRD.

## 19. Data Requirements

This section describes business information, not physical tables, columns, or
storage design.

| Business information | Required meaning | Classification / owner |
|---|---|---|
| Organization identity | Human-readable name, approved code or identifier, language labels where required, type, status, and accountable owner. | Confirmed; exact formats remain downstream-owned |
| Tenant ownership | The one Tenant that owns the Company and all descendants. | Confirmed; MESP-29 |
| Parent relationship | Company parent Tenant; Branch parent Company; Warehouse parent Branch; Department parent Company; all in one Tenant. | Confirmed |
| Legal identity | Company / Legal Entity legal name, registration or statutory references, and country context required by applicable business process. | Confirmed boundary; MESP-34/MESP-37 detail |
| Fiscal context | Company reference to exactly one active fiscal calendar, one operating time zone, one active functional currency, and applicable effective meaning. | Confirmed boundary; MESP-34 |
| Numbering context | Document type, Company + Document Type default boundary, justified optional Branch subdivision, business number, effective policy, and non-reuse evidence. | Confirmed boundary; owning domain |
| Lifecycle evidence | State, reason, effective time, decision owner, approvals where required, and resulting permitted actions. | Confirmed |
| Historical references | Organization context captured by documents, reports, files, ledger effects, and audit evidence. | Confirmed; affected domain |
| Department association | Optional Department identity owned by one Company and usable across its Branch contexts, without access meaning. | Confirmed; MESP-34 where cost applies |
| Migration evidence | Source identity, target identity, mapping status, duplicate result, owner, reconciliation, quarantine reason, and approval. | Confirmed; MESP-40 |

## 20. Validation Rules

| ID | Validation requirement | Classification | Owner / dependency |
|---|---|---|---|
| ORG-VR-001 | Reject a Company, Branch, Warehouse, or Department with missing Tenant ownership or an invalid parent. | Confirmed | MESP-29 / MESP-30 |
| ORG-VR-002 | Reject a Branch whose Company belongs to another Tenant. | Confirmed | MESP-29 |
| ORG-VR-003 | Reject a Warehouse whose Branch belongs to another Tenant or Company. | Confirmed | MESP-29 / MESP-33 |
| ORG-VR-004 | Detect duplicate identity, code, or name within the approved business boundary before authoritative creation or activation. | Confirmed | ORG-BR-015 |
| ORG-VR-005 | Reject activation when ownership, hierarchy, identity, required dependency, or readiness evidence is incomplete. | Confirmed | ORG-BR-028 |
| ORG-VR-006 | Reject new User, document, job, integration, or transaction assignment to an Inactive or Closed organization unit. | Confirmed | ORG-BR-010; MESP-28 |
| ORG-VR-007 | Preserve and validate historical references when an organization unit becomes Inactive or Closed. | Confirmed | ORG-BR-011/029 |
| ORG-VR-008 | Hold a used-unit parent change for controlled migration or closure-and-recreation; never silently rewrite history. | Confirmed | ORG-BR-013; MESP-40 |
| ORG-VR-009 | Reject a Department association that is not owned by exactly one Company, crosses Tenant/Company boundaries, or attempts direct Branch/Warehouse ownership. | Confirmed | ORG-BR-018; ORG-OD-001 |
| ORG-VR-010 | Block the affected operation when a Company lacks one explicitly confirmed active fiscal calendar, operating time zone, or functional currency. | Confirmed | ORG-BR-022/023/044/045; MESP-34 |
| ORG-VR-011 | Apply Company + Document Type as the normal numbering boundary and require approved justification for an optional Branch subdivision; block missing sequence context. | Confirmed | ORG-BR-025/026; owning domain |
| ORG-VR-012 | Reject or quarantine an ambiguous migration mapping until the accountable owner approves reconciliation. | Confirmed | MESP-40 |
| ORG-VR-013 | Apply downward Access Scope only; do not infer upward authority from a descendant selection. | Confirmed | MESP-28 IAM-OD-022 |
| ORG-VR-014 | Keep Wafra-specific values out of reusable organization behavior and reject Retail POS-only organization requirements. | Confirmed / Out of Scope | PRD BR-003/D-009 |
| ORG-VR-015 | Do not publish MESP-48 limits or MESP-50 retention/purge values from this BRD. | Deferred Gate | MESP-48/MESP-50 |
| ORG-VR-016 | Block a child from activation or new work when a required Company or Branch parent is Inactive or Closed. | Confirmed | ORG-BR-038/039/040 |
| ORG-VR-017 | After parent reactivation, require each affected descendant's own lifecycle and readiness revalidation; do not automatically restore it. | Confirmed | ORG-BR-041/042 |

## 21. Permissions and Access Scope Impact

MESP-28 owns User, Membership, Role, Permission, and Access Scope meaning.
MESP-30 supplies valid organization relationships and lifecycle outcomes to
that model.

- A valid scope may be granted at Tenant, Company, Branch, or Warehouse level
  and may apply downward to authorized descendants.
- Scope never inherits upward. Warehouse scope does not grant all Branch data;
  Branch scope does not grant all Company data; Company scope does not grant
  Tenant administration.
- An Inactive or Closed Company, Branch, or Warehouse cannot be assigned to a
  new User or used for new work; parent lifecycle state also blocks affected
  descendants until their own revalidation passes.
- Department association does not grant a Permission, Role, Membership, or
  scope.
- A Platform Administrator or auditor does not gain Tenant business-data access
  merely by holding a Platform role.
- Cross-Tenant organization selection is denied by MESP-29, and a client
  identifier cannot expand authority.
- Parent removal, lifecycle change, or a material organization change must
  trigger the applicable access review and evidence; exact session behavior
  remains in MESP-28 and downstream design.

## 22. Approval and Separation-of-Duties Controls

Organization changes must be accountable and reviewable. The required approval
level depends on the organization action and the approved business policy; this
BRD does not invent amount thresholds or a detailed conflict matrix.

| Control | Business requirement | Owner / classification |
|---|---|---|
| Requester accountability | The actor proposing Company, Branch, Warehouse, Department, lifecycle, numbering, fiscal, or parent changes is recorded. | Confirmed; MESP-38 |
| Activation approval | Activation is permitted only after required ownership, hierarchy, identity, duplicate, and dependency checks pass and the accountable approval is recorded where policy requires. | Confirmed |
| Deactivation / closure | The decision includes reason, effective time, affected scope, historical-use review, and notification/evidence. | Confirmed |
| Used parent change | A controlled migration or closure-and-recreation decision identifies the owner, affected history, reconciliation, and downstream impacts. | Confirmed; MESP-40 |
| Separation of duties | Conflicting organization setup, approval, migration, or downstream posting responsibilities are evaluated under the MESP-38 catalogue. | Deferred boundary; MESP-38 |
| Self-approval | A user must not approve a prohibited self-request where the governing policy applies. | Confirmed boundary; MESP-28 |
| Emergency bypass | No unapproved emergency or break-glass organization authority is introduced in Release 1. | Confirmed; MESP-28 |

## 23. Fiscal Calendar Boundary

Company / Legal Entity owns the business meaning of fiscal-accounting context and
has exactly one active fiscal calendar. Historical or future effective versions
may be retained where required, but only one calendar governs an applicable
accounting date. The fiscal calendar must be explicitly confirmed before
affected financial operations are enabled. The Saudi Country Pack may prefill
Gregorian January–December as a recommendation, but a prefilled value is not
approved until an authorized Company administrator confirms it.

Finance owns fiscal periods, generation, open/close/reopen behavior, posting
rules, adjustments, calendar changes, and reconciliation. MESP-30 preserves
the Company association used to interpret historical business records and does
not define period, closing, journal, tax, or accounting calculations.

### Operating time-zone boundary

Every Company / Legal Entity has one required operating time zone. For a Saudi
Company, `Asia/Riyadh` may be prefilled by the Saudi Country Pack but must be
explicitly confirmed by an authorized Company administrator. Branches and
Warehouses inherit the confirmed Company time zone in Release 1; Branch- and
Warehouse-specific overrides are excluded. Historical business timestamps must
remain interpretable using the effective Company time-zone context. Detailed
technical timestamp storage and conversion behavior are downstream design
concerns and are not defined here.

## 24. Document Numbering Boundary

Every required business document has one immutable internal identity and, where
required, one human-readable business number. The normal Release 1 business
sequence boundary is Company / Legal Entity + Document Type. A Branch-specific
subdivision may be configured only where the owning domain requires it, the
Saudi Country Pack or another approved country rule requires it, or the approved
business process requires distinct Branch numbering. Tenant-level sequences are
limited to Tenant or Platform administration records that are not Company
business documents. Fiscal year may be a partition/reset dimension where
enabled; it is not the primary owner. Warehouse-level numbering is excluded
unless a later owning-domain decision provides a justified requirement.

The business number is traceable to its applicable Tenant and organization/
document boundary, preserves the sequence policy used at issuance, is never
reused after allocation (including cancellation, rejection, or voiding), and
remains linked to the historical Company, Branch, Warehouse, and policy context.
Number gaps are permitted but remain attributable and auditable. Automatic
reset is not the default; a fiscal-year reset may be explicitly configured for a
specific sequence. Calendar-year reset is not assumed. This BRD does not define
sequence values, technical generators, padding, prefixes, or code formats.

### Document lifecycle boundary

MESP-30 defines organization master-data lifecycle, not the Draft, Submitted,
Approved, Posted, Reversed, or Cancelled behavior of transactional documents.
Owning domain BRDs define those states. A document retains the organization
reference and applicable numbering interpretation from the business event;
deactivating an organization unit never deletes or silently changes that
historical reference.

## 25. Inventory Impact

Warehouse is the lowest approved organization level at which stock location
meaning is established. Inventory owns stock balances, movements, receipts,
deliveries, transfers, counts, valuation, and related transaction states.

MESP-30 requires that:

- a Warehouse belongs to exactly one Branch and never crosses a Tenant;
- an Inactive or Closed Warehouse cannot receive new inventory transactions or
  assignments;
- historical stock, movement, document, report, and audit references remain
  interpretable after deactivation; and
- a used Warehouse parent change is controlled and reconciled rather than
  silently rewriting inventory history.

No inventory transaction or valuation rule is created here.

## 26. Accounting and Multi-Currency Impact

Company / Legal Entity is the legal and accounting boundary. Every Company has
exactly one active functional currency, and the currency must be explicitly
confirmed before affected financial operations are enabled. SAR may be
prefilled for a Saudi Company, but a prefilled value is not approved until an
authorized Company administrator confirms it. Finance owns currency activation,
effective-date changes, exchange rates, transaction currencies, reporting
currencies, rounding, revaluation, and detailed accounting behavior.

Changing functional currency after financial activity exists is not a simple
configuration edit; it requires a future controlled Finance/migration decision
and preserves historical currency meaning. Release 1 introduces no
consolidation currency and does not consolidate multiple Companies or automate
intercompany accounting. No currency, exchange-rate, posting, tax, or
consolidation value is invented in this BRD.

## 27. Saudi Localization Impact

Saudi Arabia is the initial Country Pack and Wafra is validation-only. The
organization baseline must support reusable Arabic and English organization
labels, RTL-capable business terminology, and the legal/statutory identity
needed by downstream Saudi documents.

The Saudi Country Pack and MESP-37/MESP-49 own detailed VAT, e-invoicing,
registration, statutory document, effective-date, and compliance behavior.
SAR and `Asia/Riyadh` may be prefilled for a Saudi Company as recommended
initial values, but each requires explicit authorized confirmation. Branches and
Warehouses inherit the confirmed Company time zone in Release 1; no child
override is supported. No Wafra-only localization rule is introduced.

## 28. Reports and KPIs

Reports and KPIs are business definitions only; no dashboard or query design is
specified.

| Report / KPI | Business question | Reconciliation or evidence |
|---|---|---|
| Organization register | Which Companies, Branches, Warehouses, and Departments exist, under which Tenant and parent, and in what state? | Hierarchy and status evidence |
| Hierarchy integrity exceptions | Are there duplicate, orphaned, cross-Tenant, inactive-parent, or ambiguous relationships? | Exception owner and resolution evidence |
| Lifecycle register | Which units were activated, deactivated, reactivated, or closed, when, and why? | Status history and approvals |
| Access-scope impact | Which approved scopes reference each Company, Branch, or Warehouse, and which are blocked by lifecycle? | MESP-28 access evidence |
| Numbering policy register | Which numbering boundary, owner, effective policy, reset setting, and non-reuse evidence apply? | Owning-domain reconciliation; Company + Document Type default |
| Fiscal readiness | Which Companies lack an explicitly confirmed single active fiscal calendar, time zone, or functional currency? | Finance owner and exception evidence |
| Department usage | Where are Company-owned Departments used across Branch contexts for responsibility or cost attribution, and are associations valid? | MESP-30 boundary and Finance mapping |
| Migration reconciliation | Which source mappings are approved, rejected, quarantined, or unresolved? | MESP-40 batch and sign-off evidence |
| Historical reference coverage | Can historical documents, reports, files, ledger effects, and audit records still identify the organization context? | Domain and audit reconciliation |

No numeric KPI target is invented; volume and performance evidence remain
MESP-48 gates.

## 29. Audit Evidence

Material organization events must be attributable and retrievable within the
authorized Tenant or Platform review scope. Evidence includes:

- Company, Branch, Warehouse, and Department creation or attempted creation;
- duplicate detection, validation failure, and quarantine decisions;
- activation, deactivation, reactivation, and closure;
- parent changes, migration decisions, reconciliation, and closure-and-
  recreation outcomes;
- numbering and fiscal-context configuration or missing-dependency decisions;
- User or scope assignments affected by organization state;
- cross-Tenant or invalid-parent denial outcomes; and
- report, export, support, and audit review of organization evidence.

Each event carries the accountable actor or service, time, Tenant and
organization context, action, reason, approval where applicable, outcome, and
correlation to the relevant source or business record. Audit evidence is not
editable by Tenant Users. Retention, residency, legal hold, backup, restoration,
and purge remain MESP-50 gates; no retention duration is defined here.

## 30. Integration Requirements

This section defines business dependencies, not interface contracts.

| Dependency | Business exchange | Boundary |
|---|---|---|
| MESP-27 SaaS Administration | Organization readiness contributes to Tenant activation; Platform retains readiness/provisioning evidence. | MESP-27 owns Tenant lifecycle and Platform metadata. |
| MESP-28 Identity and Access | Valid Company, Branch, Warehouse relationships support downward Access Scope and lifecycle denial. | MESP-28 owns User, Membership, Role, Permission, and session meaning. |
| MESP-29 Multi-Tenancy | Tenant ownership and default-deny boundary apply to every organization relationship. | MESP-29 owns isolation and Tenant lifecycle. |
| MESP-31 Master Data | Products and shared master data consume valid organization scope where applicable. | MESP-31 owns product/catalog meaning. |
| MESP-32 Procurement | Procurement documents reference approved Branch, Warehouse, Company, and Department context where applicable. | MESP-32 owns purchase-to-pay behavior. |
| MESP-33 Inventory | Warehouse identity and status constrain stock operations. | MESP-33 owns stock effects and valuation. |
| MESP-34 Finance | Company fiscal, accounting, currency, and numbering dependencies are validated. | MESP-34 owns accounting behavior. |
| MESP-35 B2B Sales | Sales documents consume approved Company/Branch/Warehouse context. | MESP-35 owns B2B transaction behavior. |
| MESP-36 Reporting | Reports use authorized organization references and preserve data-as-of meaning. | MESP-36 owns report definitions and reconciliation. |
| MESP-37/MESP-49 Saudi | Company statutory identity, language, currency, and country rules are consumed where required. | Saudi Country Pack owns compliance detail. |
| MESP-38 Security/Audit | Organization changes, denials, approvals, and scope impacts are evidenced. | MESP-38 owns detailed control catalogue. |
| MESP-40 Migration | Source organization data is mapped, quarantined, reconciled, and approved. | MESP-40 owns migration execution requirements. |

When a dependency is unavailable or inconsistent, the affected organization
operation is held with a visible owner and reason; no wider authority or
silent default is inferred.

## 31. Migration Requirements

Migration of organization structure must provide:

1. A named source, extract/cutover context, data owner, and target Tenant.
2. Mappings for Tenant, Company / Legal Entity, Branch, Warehouse, Department,
   status, one active fiscal context, operating time zone, functional currency,
   numbering context, and relevant historical references.
3. Duplicate and parent/child validation before activation.
4. Row or record-level outcome categories for accepted, rejected, corrected,
   and quarantined mappings.
5. Reconciliation of counts, ownership, hierarchy, statuses, identifiers,
   fiscal/time-zone/currency/numbering dependencies, and historical references.
6. An accountable owner and approval for every ambiguous or materially changed
   mapping.
7. A preview, safe correction path, immutable batch identity, and rollback or
   recovery decision owned by MESP-40.

An ambiguous mapping must not become an operational organization unit merely to
make migration complete. No Wafra-specific mapping rule becomes core behavior.

## 32. Business Acceptance Scenarios

These are business acceptance scenarios, not automated test instructions or a
test-case document.

1. **ORG-AC-001 — Create a Company:** Given an authorized Tenant Administrator and a complete unique Company identity, when Company creation is submitted, then exactly one Company is recorded under the selected Tenant with the requested status and evidence.
2. **ORG-AC-002 — Duplicate Company:** Given an existing Company with a duplicate identity or approved business key, when another Company is submitted, then authoritative creation or activation is blocked and the duplicate review remains visible.
3. **ORG-AC-003 — Activate a Company:** Given a Draft Company with valid ownership, hierarchy, identity, duplicate, and required dependency checks, when activation is approved, then the Company becomes Active and the decision is auditable.
4. **ORG-AC-004 — Deactivate a Company:** Given an Active Company with an authorized deactivation decision, when deactivation takes effect, then new Users, documents, jobs, integrations, and transactions cannot target it while historical references and evidence remain preserved.
5. **ORG-AC-005 — Multiple Companies in one Tenant:** Given one Tenant with two valid Companies, when each is configured, then each retains its own legal/accounting boundary and neither is assigned to another Tenant.
6. **ORG-AC-006 — No consolidation:** Given two Companies in one Tenant, when a consolidation, intercompany automation, elimination, transfer-pricing, or consolidated-statement request is made, then it remains outside Release 1 and is routed to future decision control.
7. **ORG-AC-007 — Create a Branch:** Given an Active or otherwise valid Company and a unique Branch identity, when Branch creation is submitted, then one Branch is associated with exactly one Company in the same Tenant.
8. **ORG-AC-008 — Branch under another Tenant's Company:** Given a Branch request whose Company belongs to another Tenant, when validation runs, then the relationship is denied without exposing the other Tenant's organization.
9. **ORG-AC-009 — Used Branch parent change:** Given a Branch with historical or transactional use, when a parent change is requested, then history is preserved and the request is routed to controlled migration or closure-and-recreation rather than silently rewriting the parent.
10. **ORG-AC-010 — Create a Warehouse:** Given a valid Branch and a unique Warehouse identity, when Warehouse creation is submitted, then one Warehouse is associated with exactly one Branch in the same Tenant.
11. **ORG-AC-011 — Warehouse under another Tenant's Branch:** Given a Warehouse request whose Branch belongs to another Tenant, when validation runs, then the relationship is denied without exposing the other Tenant's organization.
12. **ORG-AC-012 — Used Warehouse parent change:** Given a Warehouse with historical or transactional use, when a parent change is requested, then stock and document history are preserved and controlled migration or closure-and-recreation is required.
13. **ORG-AC-013 — Inactive unit used in new work:** Given an Inactive or Closed Company, Branch, or Warehouse, when a new User, document, job, integration, or transaction targets it, then the assignment or operation is denied and the reason is evidenced.
14. **ORG-AC-014 — Historical document references inactive unit:** Given a historical document that references a unit later made Inactive, when an authorized reviewer retrieves it, then the historical organization reference remains interpretable and is not deleted or silently changed.
15. **ORG-AC-015 — Department creation and assignment:** Given an authorized actor creates an optional Department, when it is assigned to exactly one Company, then it may be referenced with Branch or Warehouse operating contexts from that Company, without granting access or creating a Tenant boundary.
16. **ORG-AC-016 — Invalid Department ownership:** Given a Department association attempts direct Branch/Warehouse ownership or belongs to another Company or Tenant, when assignment is attempted, then it is rejected and no unsupported association is created.
17. **ORG-AC-017 — Downward scope inheritance:** Given a User with approved Company scope, when the User acts on an authorized descendant Branch or Warehouse, then the action is evaluated within the approved downward scope.
18. **ORG-AC-018 — No upward access inheritance:** Given a User with approved Warehouse or Branch scope, when the User targets an unrelated parent or sibling organization, then access is denied and no upward authority is inferred.
19. **ORG-AC-019 — Fiscal calendar confirmation:** Given a Company with no explicitly confirmed active fiscal calendar, when an affected financial operation is requested, then it is blocked; one calendar must govern the applicable accounting date, and a Saudi Gregorian January–December prefill is not silently approved.
20. **ORG-AC-020 — Company and document-type numbering:** Given a Company business document, when a number is issued, then the normal sequence boundary is Company / Legal Entity + Document Type; any Branch subdivision requires approved justification and no Warehouse-level sequence is assumed.
21. **ORG-AC-021 — Duplicate, reset, or reused business number:** Given an allocated business number, when the same number is requested again or a cancellation, rejection, voiding, gap, or reset is evaluated, then the number is never reused, permitted gaps remain attributable, and any fiscal-year reset is explicitly configured within the approved boundary.
22. **ORG-AC-022 — Migration ambiguity:** Given an ambiguous Company, Branch, Warehouse, Department, fiscal, or numbering mapping, when migration validation runs, then the mapping is quarantined, an accountable owner is assigned, and activation waits for approval.
23. **ORG-AC-023 — Wafra-neutral behavior:** Given Wafra is Tenant #1 for validation, when the same organization behavior is applied to another eligible Tenant, then no Wafra-specific rule, structure, numbering, or permission is required.
24. **ORG-AC-024 — Retail POS exclusion:** Given any Tenant, plan, organization, import, or integration request, when Retail POS organization or checkout behavior is requested, then it remains unavailable in Release 1 and is routed to product change control.
25. **ORG-AC-025 — MESP-48 volume gate:** Given a request to publish organization volume, capacity, concurrency, or performance commitments, when MESP-48 evidence is not approved, then no numeric promise is published.
26. **ORG-AC-026 — MESP-50 retention/purge gate:** Given a request to retain, purge, restore, or delete organization-linked history, when MESP-50 approval is absent, then the value or irreversible action remains gated and no physical purge executes.
27. **ORG-AC-027 — Audit retrieval:** Given an authorized reviewer requests organization creation, lifecycle, parent-change, numbering, fiscal, scope, duplicate, or migration evidence, when the evidence is retrieved, then actor, Tenant, organization context, reason, time, outcome, and approval are available without Tenant-user editing.
28. **ORG-AC-028 — Parent/child lifecycle dependency:** Given a Branch or Warehouse has an invalid, Inactive, or Closed required parent, when activation or new work is requested, then the operation is blocked and the responsible parent correction is identified.
29. **ORG-AC-029 — Draft parent-change control:** Given an unused Draft Branch or Warehouse, when a parent change is requested, then it is permitted only if it has never been Active, has no transactions, stock, documents, files, reports, numbering usage, integrations, jobs, User assignments, or historical references, the proposed parent is valid in the same Tenant, and the before/after relationship, actor, reason, and time are audited; otherwise controlled migration or closure-and-recreation is required.
30. **ORG-AC-030 — Historical read-only access:** Given an Inactive or Closed organization unit with valid historical references, when an authorized User requests historical documents, reports, files, stock history, accounting history, or audit evidence, then access is allowed only under current Tenant Membership, Role, Permission, and Access Scope, and the unit is not reactivated.
31. **ORG-AC-031 — Company parent deactivation:** Given an Active Company with Branches and Warehouses, when the Company becomes Inactive or Closed, then new work for all descendants is blocked while descendant statuses and historical references remain unchanged.
32. **ORG-AC-032 — Branch parent deactivation:** Given an Active Branch with Warehouses, when the Branch becomes Inactive or Closed, then new work for its Warehouses is blocked while Warehouse statuses and historical references remain unchanged.
33. **ORG-AC-033 — Parent reactivation and descendant revalidation:** Given a Company or Branch parent is reactivated, when a descendant is considered for new work, then the descendant is not automatically restored and must pass its own lifecycle and readiness revalidation.
34. **ORG-AC-034 — Operating time zone:** Given a Saudi Company, when its operating time zone is configured, then `Asia/Riyadh` may be prefilled but requires explicit confirmation, Branches and Warehouses inherit it, no child override is available, and historical timestamps remain interpretable under the effective Company context.
35. **ORG-AC-035 — Functional currency:** Given a Company, when functional currency is configured, then exactly one active currency is explicitly confirmed, SAR may be prefilled for a Saudi Company, Finance owns currency changes and calculations, and a post-activity change requires controlled Finance/migration treatment without introducing consolidation currency.

## 33. Founder Decisions — Resolved

Hossam approved the following Release 1 decisions on 2 August 2026. They are
recorded for traceability and are no longer open Organization decisions.

| ID | Approved Release 1 decision | Approved treatment | Owner / downstream boundary | Status |
|---|---|---|---|---|
| ORG-OD-001 | A Department belongs to exactly one Company / Legal Entity, not directly to a Branch or Warehouse. | A Department may be used across multiple Branches of its Company; a transaction may carry both its Branch/Warehouse context and same-Company Department; Department association never grants access and never crosses Company/Tenant. | Organization; Finance for cost behavior | Approved |
| ORG-OD-002 | Each Company / Legal Entity has exactly one active fiscal calendar. | Historical or future effective versions may be retained where required, but only one calendar governs an applicable accounting date; Finance owns period and change behavior. | Finance (MESP-34) | Approved |
| ORG-OD-003 | Fiscal configuration must be explicitly confirmed. | Saudi Gregorian January–December may be prefilled as a recommendation, but no calendar is silently assumed or treated as approved. | Finance and Saudi Country Pack | Approved |
| ORG-OD-004 | The normal numbering boundary is Company / Legal Entity + Document Type. | Branch subdivision requires approved justification; Tenant-level sequences are limited to Tenant/Platform administration records; fiscal year may partition but is not the primary owner; Warehouse-level numbering is excluded absent later justified domain decision. | Owning domain; Saudi Country Pack where applicable | Approved |
| ORG-OD-005 | Numbering does not reset automatically by default. | A fiscal-year reset may be explicitly configured; calendar-year reset is not assumed; allocated numbers are never reused, gaps are attributable/auditable, and reset uniqueness uses the approved Company/document/optional Branch/fiscal boundary. | Owning domain; Finance and Saudi Country Pack | Approved |
| ORG-OD-006 | Authorized historical read-only access is allowed for inactive or closed units. | Current Tenant Membership, Role, Permission, and Access Scope still govern retrieval; new work remains blocked and historical access never reactivates the unit. | MESP-28, Finance, Reporting, Security, owning domains | Approved |
| ORG-OD-007 | An unused Draft Branch or Warehouse may change parent only under strict no-use conditions. | It must never have been Active and must have no transactions, stock, documents, files, reports, numbering usage, integrations, jobs, User assignments, or historical references; the same-Tenant valid parent and audited before/after change are required; otherwise controlled migration or closure-and-recreation applies. | Organization and Migration | Approved |

No genuine Organization founder decision remains open. MESP-48 and MESP-50
remain deferred gates and are not converted into invented values.

## 34. Source Conflicts

| ID | Source conflict or ambiguity | Resolution / treatment | Status |
|---|---|---|---|
| ORG-SC-001 | The PRD domain table lists Organization with fiscal calendar and sequence, while the architecture source-ownership reconciliation places fiscal-calendar behavior in Finance and numbering scopes in Organization. | Organization owns identity and relationship meaning; Finance owns fiscal behavior; approved Company/document numbering boundary is retained while owning domains define detailed use. | Resolved by founder approval |
| ORG-SC-002 | The glossary marked Department as Draft for BRD Validation and required MESP-30 confirmation. | Founder approval resolves Department ownership to exactly one Company, with cross-Branch use and no access meaning. | Resolved by founder approval |
| ORG-SC-003 | The glossary marked Fiscal Calendar as Draft for BRD Validation, while PRD and MESP-5 require a Company fiscal context. | Founder approval resolves one active Company calendar and explicit confirmation; Finance owns detailed periods and changes. | Resolved by founder approval |
| ORG-SC-004 | The glossary assigns Warehouse to Inventory, while architecture assigns Organization the Warehouse place in the hierarchy. | Organization owns Warehouse identity/relationship; Inventory owns stock, movement, and valuation effects. | Resolved boundary |
| ORG-SC-005 | MESP-5 describes configured document sequences, while the founder direction forbids inventing formats, technical generators, or unapproved values. | Founder approval resolves the Company + Document Type default and non-reuse/reset policy; technical generator and exact formats remain downstream-owned. | Resolved by founder approval |

No blocking source conflict remains. The five records remain for traceability;
all seven founder decisions are approved, while MESP-48 and MESP-50 remain
explicit deferred gates.

## 35. Coverage Checklist

| Jira MESP-30 required output / functional area | Covered section(s) / IDs | Coverage status | Deferred owner, when applicable |
|---|---|---|---|
| Business purpose | 2-4 | Covered | None |
| Actors and responsibilities | 7 | Covered | Detailed roles/SoD: MESP-28/MESP-38 |
| Trigger and preconditions | 15-16 | Covered | Owning domain details where named |
| Main process | 15 | Covered | BPMN only if required to remove material ambiguity |
| Alternative paths | 16 | Covered | Approved ORG-OD treatments and MESP-40 where named |
| Exception scenarios | 16, 20, 32 | Covered | None beyond explicit gates |
| Organization business rules | 17 | Covered | 46 stable ORG-BR rules |
| Document lifecycle boundary | 24 | Covered | Transactional lifecycles: MESP-32/33/34/35 |
| Organization status transitions | 18 | Covered | Parent lifecycle effects and descendant revalidation are explicit |
| Data requirements | 19 | Covered | Business information only; no schema design |
| Validation rules | 20 | Covered | None beyond named decisions/gates |
| Permissions and access scopes | 21 | Covered | MESP-28 owns detailed access meaning |
| Approval controls | 22 | Covered | Detailed policy/SoD: MESP-38 and MESP-28 |
| Separation of duties | 22 | Covered as boundary | MESP-38 catalogue |
| Inventory impact | 25 | Covered as boundary | Inventory transactions: MESP-33 |
| Accounting impact | 23, 26 | Covered as boundary | Finance behavior: MESP-34 |
| Multi-currency impact | 26 | Covered as boundary | Finance/exchange-rate decision: MESP-34/MESP-54 |
| Saudi localization impact | 27 | Covered as boundary | Saudi detail: MESP-37/MESP-49 |
| Reports and KPIs | 28 | Covered | Reporting detail: MESP-36; volume evidence: MESP-48 |
| Audit evidence | 22, 29, 32 | Covered | Retention/purge: MESP-50; catalogue: MESP-38 |
| Integration requirements | 30 | Covered | Interface detail is downstream |
| Migration requirements | 31 | Covered | MESP-40 |
| Given/When/Then acceptance scenarios | 32 | Covered | 35 business scenarios; no test document |
| Founder decisions | 33 | Covered | ORG-OD-001 through ORG-OD-007 approved |
| Source conflicts | 34 | Covered | Five resolved traceability records |
| Business-owner approval | 36 | Approved | Hossam, 2 August 2026 |

**Coverage result: Approved baseline covered.** Every required MESP-30 output and
each approved functional area has a section, rule, process, validation, or
acceptance scenario. No Organization founder decision remains open; only the
explicit MESP-48 and MESP-50 deferred gates remain.

## 36. Founder Review and Approval

### 36.1 Approval checklist

| Review item | Final status |
|---|---|
| Approved hierarchy and one-Tenant ownership | Approved |
| Multiple Companies / Legal Entities without Release 1 consolidation | Approved |
| Company, Branch, and Warehouse lifecycle, parent effects, and historical-reference rules | Approved |
| Department Company ownership and non-isolation boundary | Approved by ORG-OD-001 |
| Downward-only organization scope and no upward inheritance | Approved |
| Fiscal-calendar and numbering boundaries | Approved by ORG-OD-002 through ORG-OD-005 |
| Inactive historical read-only behavior | Approved by ORG-OD-006 |
| Draft unused parent-change behavior | Approved by ORG-OD-007 |
| Company operating time zone and descendant inheritance | Approved clarification |
| Company functional currency and Finance ownership | Approved clarification |
| Wafra-neutral and Retail POS exclusion | Approved |
| MESP-48 and MESP-50 gates | Preserved as Deferred Gates |
| Business acceptance scenarios | Included for review |
| No technical or implementation scope | Confirmed |

### 36.2 Approval block

| Approval field | Record |
|---|---|
| Approver | Hossam |
| Approval date | 2 August 2026 |
| Baseline | Organization and Company Structure BRD v0.2 — Approved Release 1 Baseline |
| Rule result | 46 rules: 44 Confirmed, 0 Founder Default, 0 Open Decision, 2 Deferred Gate |
| Acceptance result | 35 business acceptance scenarios |
| Decision result | ORG-OD-001 through ORG-OD-007 approved; no genuine Organization decision remains open |
| Source result | Five resolved source-conflict records retained |
| Delivery boundary | Approval authorizes downstream requirements/design preparation only; it does not authorize implementation, a Sprint, or code |
| Jira state | MESP-5 In Progress; MESP-30 Done; MESP-30 remains outside all Sprints |
| Deferred gates | MESP-48 volume/capacity evidence; MESP-50 residency, retention, legal-hold, backup, restoration, and purge policy |

**Approval recorded:** Hossam approved `docs/14_Organization_and_Company_Structure_BRD.md`
v0.2 on 2 August 2026. The next activity is the combined Foundation Release 1
Lean Implementation Specification for Identity and Access, Multi-Tenancy, and
Organization; that specification is not started by this document action.
