# Mini ERP SaaS Platform - Security, Audit, and Data Governance BRD

> **Jira:** MESP-38 - Produce Security, Audit, and Data Governance BRD
> **Parent:** MESP-13 - EPIC 13 - Security, Audit, and Data Governance
> **Scope:** Release 1 Saudi-localized Core ERP B2B baseline; Wafra is
> validation-only
> **Status:** Approved bounded business baseline
> **Canonical artifact:** docs/29_Security_Audit_and_Data_Governance_BRD.md
> **Document type:** Documentation-only business-requirements baseline

This document defines the business evidence, control consequences, and
data-governance boundaries required for a trustworthy Release 1 B2B ERP. It
does not authorize source implementation, persistence, APIs, UI, providers,
credentials, infrastructure, deployment, production configuration, or
production capability.

## 1. Document control and reading rules

### 1.1 Authority and classification

The approved PRD, approved Product Decisions, the current MESP-23 living
register, the approved upstream BRDs, the architecture baseline, and the ADR
index are read in that order of authority. A later named decision may change a
deferred row; a recommendation, draft, Jira creation, or implementation
assumption does not.

| Classification | Meaning in this BRD |
| --- | --- |
| **Confirmed baseline** | A business requirement supported by the approved PRD, approved upstream BRD, approved scope overlay, glossary, architecture boundary, or named approved Jira decision. |
| **Control consequence** | A security, audit, or governance outcome that MESP-38 requires from an owning domain without taking ownership of that domain's source business meaning. |
| **Deferred gate** | A named decision, production validation, legal review, capacity review, or later BRD that remains open. No value is invented here. |
| **Out of scope** | A topic excluded by Release 1 scope or owned by a later item. It is not an implicit requirement. |

The words **must** and **shall** indicate a business requirement at this
bounded scope. They do not prescribe a technical design.

### 1.2 Current entry position

MESP-38 was activated as the single bounded BRD item after fresh live
verification on 12 August 2026. MESP-27 through MESP-37 are Done at their
approved bounded BRD scopes. MESP-23 remains In Progress as the living Open
Questions Register. MESP-48, MESP-50, MESP-53, MESP-54, MESP-110, and
MESP-113 remain open or unapproved as named below. MESP-39 was verified as To
Do and is not activated by this document.

The entry verification is recorded in Jira MESP-38 activation comment 10934.
The focused validation is comment 10935, Owner approval is comment 10936, and
the MESP-23 handoff is comment 10937. Final audit, closure, merge, and
final-main evidence are recorded in Jira after the focused artifact review.

### 1.3 Documentation-only rule

This BRD authorizes no:

- application source, automated tests, EF entities, tables, schemas, or
  migrations;
- endpoints, API contracts, controllers, background-host composition, or UI;
- object-storage, telemetry, identity, secrets, key, scanning, or hosting
  provider selection;
- credentials, deployment, production infrastructure, production
  configuration, supported-volume promise, retention value, or production
  capability; or
- implementation Story, parallel task, or automatic activation of MESP-39.

## 2. Executive summary

Security, audit, and data governance are business controls that make every
material ERP action attributable, bounded, reviewable, and safe to reconcile.
MESP-38 consumes the approved User, Tenant Membership, Role, Permission,
Tenant, Company, Branch, Warehouse, lifecycle, and SoD meanings. It adds the
evidence and governance consequences that allow an authorized reviewer to
answer:

1. who or what attempted the action;
2. which Tenant and organizational scope were involved;
3. which business object, source document, file, export, or configuration was
   affected;
4. which authority, decision, approval, or denial was applied;
5. what happened, including failure or unknown outcome; and
6. how the result can be reviewed, corrected, retained, exported, held, or
   reconciled without weakening Tenant isolation or changing source facts.

The business baseline requires private-by-default files and exports,
controlled downloads, quarantine-aware attachment use, immutable audit
history, safe retrieval and review, purpose-bound support evidence, safe
technical telemetry, incident evidence, and explicit governance gates for
retention, deletion, legal hold, residency, privacy, backup, restoration, and
offboarding.

No legal, regulatory, provider, production, or unresolved domain decision is
silently converted into a Release 1 requirement.

## 3. Business purpose and desired outcomes

### 3.1 Purpose

The purpose of MESP-38 is to establish the cross-module business contract for
security evidence, audit history, and data-governance consequences across the
Saudi-localized Core ERP B2B baseline. The contract must remain reusable for
Wafra and future Tenants without customer-specific core behavior.

### 3.2 Desired outcomes

| Outcome | Business meaning |
| --- | --- |
| Accountable action | A reviewer can identify the acting User or controlled process, authority context, scope, reason, and outcome for a material event. |
| Tenant-safe evidence | Records, files, exports, search results, background work, telemetry references, and audit views cannot cross a Tenant boundary. |
| Explainable decisions | Allow, deny, reject, fail, unknown, and reconciled outcomes are distinguishable and do not pretend that an unresolved result succeeded. |
| Reviewable history | Authorized reviewers can retrieve and search evidence without Tenant Users rewriting or deleting history. |
| Safe private artifacts | Attachments and exports remain private, scope-bound, scan-aware, expiring where applicable, and unavailable when authorization or safety conditions fail. |
| Governed data lifecycle | Retention, deletion, legal hold, residency, privacy, backup, restoration, and offboarding remain explicit decisions with accountable evidence. |
| Cross-module integrity | Security and audit controls preserve the owning module's source facts, posting rules, stock effects, commercial status, and reconciliation paths. |
| Production honesty | Open decisions and validation gates remain visible; this BRD makes no provider, legal, capacity, or production claim. |

## 4. Scope and boundaries

### 4.1 In scope

At business-requirements level, this BRD covers:

- security and audit evidence for material business, configuration, access,
  denial, privileged, support, export, file, integration-boundary,
  reconciliation, and lifecycle outcomes;
- the evidence meaning of actor, acting context, Tenant,
  Company/Legal Entity, Branch, Warehouse, object, source document,
  correlation, decision, before/after or safe change summary, and outcome;
- immutable audit-history meaning, authorized retrieval, search, review, and
  bounded export without choosing physical storage;
- the consequences of server-derived authority, Tenant isolation,
  organization scope, object state, support access, and SoD as defined by the
  owning BRDs;
- private attachments, downloads, exports, quarantine, scan state, expiry,
  and authorization boundaries;
- separation of business audit from technical observability, safe telemetry
  properties, monitoring ownership, incident evidence, and failure/unknown
  handling without selecting a production telemetry provider or retention;
- governance consequences for retention, deletion, legal hold, export,
  residency, privacy, offboarding, backup, and restoration while unresolved
  values remain gated;
- cross-module ownership and control handoffs across Platform, IAM,
  Multi-Tenancy, Organization, Master Data, Procurement, Inventory, Finance,
  Sales, Reporting, Saudi localization, Migration, and future Integrations;
- generic Finance evidence and SoD consequences for privileged period
  close/reopen/reclose and future posting dimensions, without deciding
  Finance mechanics; and
- business-testable Given/When/Then acceptance scenarios.

### 4.2 Explicit exclusions and preserved gates

The following are not requirements or completion claims from this BRD:

| Exclusion or gate | Treatment |
| --- | --- |
| Source, tests, persistence, schema, EF, migrations, APIs, controllers, UI, providers, credentials, infrastructure, deployment, and production configuration | Explicitly excluded. |
| Legal advice, PDPL compliance, privacy certification, DPO/controller status, data-subject rights workflows, transfer-impact assessments, SCCs/BCRs, regulator approval, certification, or external validation | Explicitly excluded; qualified future validation remains required where applicable. |
| Retention duration, purge schedule, legal-hold duration, residency or hosting conclusion, backup schedule, restoration/DR behavior, RPO/RTO, support geography, or production deletion mechanics | Deferred to MESP-50 and named production/legal gates. |
| MESP-48 supported-volume, performance, capacity, and recovery gates | Remain open; no numeric value or promise is selected. |
| MESP-50 retention, privacy, legal-hold, purge, residency, backup, restoration, hosting, or production-governance gates | Remain open; this BRD records consequences and evidence needs only. |
| MESP-53 report catalogue, KPI/figure definitions, named reconciliation owners, schedules, and distribution policy | Remain open; MESP-38 requires audit/export consequences without closing Reporting policy. |
| MESP-54 exchange-rate source, cadence, effective date, conversion, precision, rounding, Reporting Currency, or approval policy | Remain open; generic configuration evidence may be required without choosing rate policy. |
| MESP-110 Finance year-end, Payment Term, due-date, aging, settlement, fiscal close mechanics, retained earnings, and posting-dimension catalogue | Remain open; only generic evidence and SoD consequences are covered. |
| MESP-113 / INV-OD-004 Inventory tracking policy | Remain open; MESP-38 does not decide batch, lot, serial, expiry, or other physical-stock behavior. |
| Currency implementation, statutory tax, ZATCA/FATOORA, e-invoicing, banking, payment-provider, government, or other external production integrations | Out of Release 1 scope or separately gated. |
| Retail POS, consumer checkout, cashier, cash drawer, restaurant, retail shift, or Wafra-specific core behavior | Explicitly excluded. |
| Automatic activation of MESP-39 or any later item | Prohibited by this session. |

Release 1 remains a Saudi-localized Core ERP B2B product. Generic security,
audit, privacy-oriented minimization, and immutable financial history are not
statutory, legal, regulatory, or certification claims.

## 5. Source baseline and traceability

### 5.1 Approved PRD anchors

| Anchor | MESP-38 consequence |
| --- | --- |
| PLT-001 and the Tenant-isolation rules | Security evidence, audit, files, exports, jobs, search, and review must preserve the server-established Tenant boundary and default denial. |
| PLT-006 | Attachments and private artifacts require ownership, authorization, safe handling, and governed lifecycle consequences. |
| PLT-008 and BR-011 | Material business, access, configuration, posting, reversal, support, export, and lifecycle actions require attributable, immutable evidence. |
| PLT-009 | Authorized search and bounded exports require scope, status, expiry, and audit consequences. |
| ADM-002 | Material configuration changes require history, authority, effective context, and reviewable evidence. |
| BR-010 | Tenant, Company, Branch, Warehouse, module, Role, document-state, and contextual access are consumed as authorization inputs. |
| BR-016 | Availability, recovery, observability, localization, accessibility, and data-portability expectations remain subject to validation and production gates. |
| PRD section 8.3 | Authentication, authorization, secure sessions, support, encryption, security events, and privileged actions are business control inputs; production policy remains gated. |
| PRD sections 9, 11, 13, 17, 18, and 19 | Recovery, auditability, support/offboarding, business rules, and integration boundaries require evidence without authorizing implementation. |

### 5.2 Approved related baselines

| Baseline | What MESP-38 consumes |
| --- | --- |
| MESP-27 Platform Administration | Platform/Tenant administration, lifecycle, support case and time boundaries, export/offboarding, Platform-owned records, suspension/reactivation, and purge approval boundaries. |
| MESP-28 Identity and Access | User, Tenant Membership, Role, Permission, Access Scope, authentication/session meaning, privileged access, self-approval prohibition, and approved Release 1 SoD. |
| MESP-29 Multi-Tenancy | Tenant meaning, context, lifecycle, isolation, default-deny behavior, Tenant-owned versus Platform-owned boundary, and cross-Tenant consequences. |
| MESP-30 Organization | Company/Legal Entity, Branch, Warehouse, relationships, lifecycle, and downward organization scope. |
| MESP-31 through MESP-37 | Approved domain and localization business facts, lifecycle events, source ownership, audit expectations, and explicit open-decision boundaries. |
| MESP-23 | Living Open Questions Register; no open row is closed, answered, or silently superseded here. |
| MESP-48, MESP-50, MESP-53, MESP-54, MESP-110, MESP-113 | Named gates preserved without invented policy. |

### 5.3 Architecture baseline consequences

The Technology Architecture Baseline is a constraint on later work, not a
source of new BRD scope. MESP-38 consumes these business consequences:

- section 8 establishes server-derived request context, Tenant ownership,
  default denial, and the unresolved SQL Server RLS adoption/deferral gate;
- section 9 combines identity, membership, entitlement, Role/Permission,
  organization scope, object state, amount/approval, and SoD authority;
- section 12 keeps objects private by default and requires authorized,
  scan-aware downloads;
- section 13 distinguishes business audit from technical telemetry and
  requires safe, correlated, attributable evidence; and
- sections 18 and 21 identify security controls and ADR decisions that must
  be validated before production.

### 5.4 ADR and dependency status

The following statuses are boundaries, not implementation authorization:

| ADR | Current status and MESP-38 treatment |
| --- | --- |
| ADR-002 | Published four-project structure and module-enforcement record. Preserve the actual topology; do not invent projects or module ownership. |
| ADR-003 | Approved shared-database Tenant-isolation baseline. Detailed implementation and provider validation remain gated. |
| ADR-004 | Accepted Foundation identity, cookie, session, antiforgery, and context baseline. Production providers and policy values remain separately gated. |
| ADR-005 | Approved policy/resource-authorization baseline. MESP-38 consumes it and does not redefine permission semantics. |
| ADR-006 | Foundation persistence, module-schema, and transaction baseline. Production provider, migration, and SQL validation remain gated. |
| ADR-007 | Foundation internal events/outbox/inbox baseline. Broker, operational production delivery, and retention remain deferred. |
| ADR-008 | Foundation worker seam. Deployment topology, capacity, and hosting remain deferred. |
| ADR-009 | Private-object-storage contract baseline. Provider, region, scanning, retention, purge, residency, and key-management decisions remain gates. |
| ADR-010 | Required production decision for OpenTelemetry exporter, operational-data access, and retention. MESP-38 does not resolve it. |
| ADR-011 | Cross-module localization dependency for runtime localization, Arabic search/collation/tokenization, RTL details, and bilingual documents; not the primary MESP-38 owner. |
| ADR-013 | Required production decision for secrets and encryption-key management. No provider or lifecycle is selected. |
| ADR-014 | Required production decision for residency, retention, legal hold, export, and purge. No policy value is selected. |
| ADR-016 | Index-only entry for SQL Server RLS adoption or formal deferral. No missing ADR is manufactured and no position is selected. |
| ADR-018 | Foundation testing harness and validation boundary. Production equivalence and production-like gates remain deferred. |

## 6. Business actors and responsibilities

| Actor | Responsibility within this BRD | Boundary |
| --- | --- | --- |
| Tenant User | Performs permitted ERP work and supplies truthful business reasons or supporting evidence. | Cannot widen Tenant, organization, object, or export authority. |
| Requester / preparer | Starts a material business, configuration, file, export, or governance action. | Cannot self-approve where the owning policy requires separation. |
| Approver / delegated authority | Makes an authorized decision within the exact policy, scope, and effective interval. | Delegation and approval catalogue remain owned by the relevant BRD and open decision. |
| Finance control owner | Reviews generic period, posting, reconciliation, and correction evidence. | Does not receive an MESP-38 decision about Finance mechanics or MESP-110. |
| Inventory control owner | Reviews stock, count, adjustment, tracking, and reconciliation evidence. | Does not receive an MESP-38 decision about INV-OD-004 or MESP-113. |
| Tenant Administrator | Manages approved Tenant administration and acknowledges authorized support or governance actions where required. | Does not gain Platform audit, purge, Finance, Inventory, or cross-Tenant authority automatically. |
| Platform Administrator | Operates Platform-owned lifecycle, support coordination, export/offboarding, and governance records. | Cannot access Tenant business data by default or bypass authorization and audit. |
| Authorized support User | Investigates a named case within the approved Tenant, scope, purpose, and time boundary. | No shared account, hidden superuser, unrestricted impersonation, or support export authority. |
| Auditor / authorized reviewer | Reviews permitted audit, security, configuration, incident, file, export, lifecycle, and reconciliation evidence. | Read-only at the business boundary; cannot change source facts or audit history. |
| Security / incident owner | Owns security review, incident evidence, triage, escalation, and closure evidence. | Does not silently decide legal notifications, retention, residency, or provider policy. |
| Data-governance owner | Coordinates classification, retention/hold/offboarding evidence, and decision dependencies. | Does not invent legal conclusions, schedules, residency, or purge authority. |
| Reporting owner | Owns report definitions, reconciliation ownership, freshness, and publication policy after MESP-53. | MESP-38 may require audit/export evidence but cannot create the final catalogue or owner. |
| Migration owner | Owns source mapping, validation, quarantine, cutover, sign-off, and rollback evidence under the migration boundary. | MESP-38 consumes migration evidence and does not define migration mechanics. |
| Module owner | Owns source business facts, lifecycle, permissions, and reconciliation for the module. | Must expose the evidence consequences named here without transferring domain ownership. |
| Platform operations | Maintains technical observability and operational response under approved production gates. | Technical telemetry does not replace business audit and must not expose sensitive payloads. |
| Qualified external adviser | May validate a named legal, privacy, tax, security, or regulatory gate in a future scope. | No legal or regulatory conclusion is implied by this BRD. |

## 7. Controlled terminology

| Term | Meaning in this BRD |
| --- | --- |
| Audit evidence | Business evidence that allows an authorized reviewer to reconstruct a material decision, access outcome, or state-changing event. |
| Audit history | The protected, reviewable history of audit evidence. Tenant Users cannot edit or delete it. |
| Business audit | Evidence of business meaning, authority, decision, effect, and outcome. |
| Technical telemetry | Logs, metrics, traces, health signals, and alerts used to operate and investigate the service. It is not a substitute for business audit. |
| Material action | An action that changes business, configuration, access, lifecycle, file, export, posting, approval, reconciliation, or governance state, or that attempts such a change. |
| Outcome unknown | The authoritative result is not yet established. It must remain visible as unknown or pending until reconciled. |
| Safe change summary | A before/after or change description sufficient for review without copying secrets, private file content, credentials, or unnecessary personal data. |
| Private artifact | A file, attachment, export, report result, or evidence artifact available only through an authorized scope and purpose. |
| Quarantine | A business state in which a file or artifact is not available for ordinary use until the required safety review is complete. |
| Legal hold | A documented governance condition that prevents deletion or purge for an approved scope. Its duration and legal basis remain gated. |
| Data class | A business category with an owner, purpose, access boundary, retention dependency, and governance consequence. It is not a physical schema. |
| Source fact | The business record owned by the originating module. Audit, reporting, export, and localization may describe it but cannot rewrite it. |
| Correlation | A stable review reference that links related action, audit, telemetry, job, export, file, integration, and reconciliation evidence. |
| Evidence owner | The accountable business or operational owner responsible for the completeness and reviewability of a named evidence path. |

## 8. Ownership boundaries and consume-don't-redefine matrix

MESP-38 owns cross-cutting evidence requirements and consequences. It does
not become the source of truth for the following domains:

| Concern | Owning baseline | MESP-38 consequence |
| --- | --- | --- |
| User, Tenant Membership, Role, Permission, authentication/session, Access Scope, support fundamentals, and approved SoD | MESP-28 | Evidence must identify the authority and outcome; MESP-38 does not add Role or Permission semantics. |
| Tenant meaning, context, lifecycle, isolation, default deny, and Tenant-owned/Platform-owned records | MESP-29 | Every evidence, file, export, job, search, and review path remains Tenant-bound. |
| Company/Legal Entity, Branch, Warehouse, hierarchy, lifecycle, and downward scope | MESP-30 | Evidence identifies the applicable organization scope; MESP-38 does not redefine organization relationships. |
| Platform plan, Entitlement, lifecycle, support case, export, offboarding, and purge approval | MESP-27 | Governance evidence records actor, case, purpose, approval, interval, scope, and outcome; MESP-50 remains the production gate. |
| Product, supplier, customer, price, tax, UOM, and other approved master facts | MESP-31 and related approved domain baselines | Audit describes identity, source, authority, and change outcome; it does not create variant, tax, or availability policy. |
| Procurement commercial facts and handoffs | MESP-32 | Evidence follows the purchase-to-pay decision, exception, receipt, return, and reconciliation path. |
| Physical stock events, tracking behavior, count, adjustment, and valuation boundaries | MESP-33 | Evidence covers the event and control outcome; MESP-113 / INV-OD-004 remains unresolved. |
| Accounting, posting, period, payment, tax, currency, and reconciliation facts | MESP-34 | Evidence covers generic authority, source, period, posting, correction, and outcome; MESP-54 and MESP-110 remain open. |
| B2B quotation, order, fulfillment, invoice handoff, receipt, return, and customer facts | MESP-35 | Evidence preserves commercial lineage and does not grant Finance or Inventory authority. |
| Report definitions, KPIs, lineage, freshness, reconciliation owners, schedules, and distribution | MESP-36 and MESP-53 | Security and export evidence is required, but MESP-53 controls final catalogue and named owners. |
| Arabic/English, RTL, locale, and Saudi-oriented presentation | MESP-37 and ADR-011 dependency | Evidence remains language-neutral in business meaning; localization cannot widen security scope or change source facts. |
| Migration mapping, quarantine, cutover, rollback, and sign-off | MESP-40 future boundary | MESP-38 requires accountable evidence and reconciliation visibility without defining migration workflow. |
| Future integrations and external services | MESP-39 future boundary | MESP-38 records integration-boundary evidence and unknown outcomes only; it does not define external production behavior. |

## 9. Security control consequences

### 9.1 Tenant and organization isolation

The active Tenant context and organization scope are established by the
approved Multi-Tenancy, IAM, and Organization baselines. MESP-38 requires the
following consequences:

- a user-supplied Tenant, Company, Branch, Warehouse, object, file, report,
  export, job, or source identifier cannot expand authority;
- a read, write, search, file, export, notification, background, audit,
  telemetry-review, or reconciliation path must fail closed when the
  requested scope is absent, stale, revoked, suspended, terminated, or
  otherwise invalid;
- a denial must not disclose another Tenant's existence, data, file name,
  source value, audit detail, export status, or operational state;
- a valid artifact or working state belonging to Tenant A must not be reused
  as Tenant B state after a context switch;
- Platform-owned governance records may reference Tenants only for their
  authorized purpose and cannot become a Tenant user's cross-Tenant view; and
- audit evidence itself is Tenant- or Platform-scoped and is subject to the
  same access boundary it records.

### 9.2 Server-derived authority and stale access

Every material outcome must be attributable to the authority evaluated at the
time of the action. Evidence must distinguish:

- authenticated User and active Tenant Membership;
- Role, Permission, Entitlement, organization scope, object state, and
  contextual conditions;
- approved support or delegated context, where applicable;
- the requested operation and source object;
- the decision and reason category; and
- the resulting allow, deny, reject, fail, unknown, or reconciled outcome.

An earlier page, export request, prepared file, session, support grant, or
approval is not proof that authority remains valid. A revoked or expired
authority must not be reused silently.

### 9.3 Object state and material actions

The owning domain remains authoritative for lifecycle and object-state rules.
MESP-38 requires that a material action:

1. identifies the object and source document within its authorized scope;
2. records the applicable state and authority decision;
3. preserves any required approval, SoD, reason, and effective-time evidence;
4. does not represent a denied, failed, or unknown action as successful; and
5. provides a linked correction or reconciliation outcome instead of silently
   changing history.

Posted, approved, closed, issued, received, reconciled, or otherwise
historically material source facts remain governed by their owning domain.
Audit evidence may explain a correction but cannot authorize silent rewriting.

### 9.4 Support and privileged access consequences

MESP-27 and MESP-28 own the Release 1 support and privileged-access
fundamentals. MESP-38 requires evidence of their consequences:

- a support action identifies the named case, named support User, Tenant,
  exact scope, purpose, approval or authorization, start, expiry, and
  outcome;
- support access is least-privilege and purpose-bound; it does not create a
  hidden superuser or unrestricted impersonation path;
- support access expires or is revoked according to the owning baseline, and
  post-expiry attempts are denied and evidenced;
- support access does not imply export, purge, posting, approval, or source
  mutation authority; each remains separately authorized;
- emergency or break-glass behavior is not invented by this BRD; if later
  approved, it requires its own authority, evidence, and review boundary; and
- Platform operational access is kept distinct from Tenant business-data
  access and from ordinary Tenant administration.

### 9.5 Approval and separation of duties

MESP-38 consumes the approved SoD model and later domain-specific approval
policies. It requires:

- requester, approver, poster, reviewer, reconciler, and support roles to be
  distinguishable where the owning policy requires separation;
- self-approval to be denied where the approved Release 1 policy prohibits it;
- a delegated or substitute authority to be evidenced only when the owning
  policy permits it;
- a material decision to retain the authority, scope, policy/version
  context, reason, and outcome used; and
- evidence review to remain separate from source mutation and purge execution.

MESP-38 does not select a new approval catalogue, parallel-approval rule,
escalation rule, or delegation policy.

### 9.6 Failure and unknown security outcomes

The business must be able to distinguish an action that was:

| Outcome | Required meaning |
| --- | --- |
| Allowed / completed | Authority and required evidence were present and the owning domain accepted the effect. |
| Denied | Authority or a required condition was absent; no unauthorized effect is represented. |
| Rejected | The request was understood but failed an owning business rule or approval condition. |
| Failed | The attempted operation did not complete; the reason or safe failure category is reviewable. |
| Unknown / pending | The authoritative result is not established; it remains open for reconciliation and is not presented as success. |
| Reconciled | An unknown, failed, or conflicting outcome has a documented authoritative resolution linked to the original evidence. |

## 10. Audit evidence requirements

### 10.1 Audit objective

Audit history must allow an authorized reviewer to reconstruct material
business and security decisions without exposing unnecessary secrets, private
file content, credentials, payment details, or unrelated personal data.

Audit evidence is required for successful actions and for material attempted,
denied, rejected, failed, unknown, reversed, corrected, and reconciled
outcomes where the owning policy treats the action as material. Lack of a
business effect does not by itself remove the need for evidence of a material
security decision.

### 10.2 Minimum business evidence

The applicable evidence must include, as relevant to the action:

| Evidence element | Business requirement |
| --- | --- |
| Event identity and category | A reviewer can identify what kind of action or decision occurred. |
| Occurrence and effective time | The order of the attempt and the time at which a business effect became effective are distinguishable where necessary. |
| Actor and acting context | User, controlled process, support grant, delegated authority, or system-owned action is attributable without relying on a display name alone. |
| Tenant and organization scope | Tenant and applicable Company/Legal Entity, Branch, and Warehouse scope are clear when relevant. |
| Target and source | Business object, source document, file, export, configuration, or external-boundary reference is identifiable without copying private content. |
| Authority and decision | Permission/approval/support context, decision category, reason category, and applicable policy context are reviewable without redefining authorization. |
| Change evidence | Before/after or a safe change summary identifies the material difference without secrets or unnecessary personal data. |
| Outcome | Allowed, denied, rejected, failed, unknown, reversed, corrected, or reconciled status is explicit. |
| Correlation and related evidence | Related request, job, export, file, integration, incident, approval, and reconciliation evidence can be followed. |
| Source and version | Source module, business document version, configuration version, or effective policy reference is available where needed to reproduce meaning. |
| Reason and explanation | A business-safe reason or explanation distinguishes intentional denial, validation failure, exception, and operational uncertainty. |
| Evidence classification | The sensitivity and permitted reviewer boundary are known; raw secrets and unnecessary payloads are excluded. |

Not every event needs every element. The owning domain must not omit an
element that is necessary to reconstruct a material effect or security
decision.

### 10.3 Required event coverage

The cross-module evidence catalogue must cover, as applicable:

- authentication, session, membership, Role, Permission, scope, revocation,
  support, privileged, approval, SoD, and denial outcomes;
- Tenant creation, activation, suspension, reactivation, termination,
  offboarding, export, retention, hold, and governance decisions;
- material master-data create, update, activate, deactivate, and correction
  outcomes;
- Procurement request, approval, order, receipt, return, exception, and
  reconciliation handoffs;
- Inventory receipt, issue, transfer, count, adjustment, tracking, return,
  valuation, and unknown/reconciled outcomes when owned by Inventory;
- Finance journal, invoice, allocation, payment, reconciliation, posting,
  reversal, period close, reopen, reclose, and correction outcomes;
- B2B Sales quotation, order, fulfillment, invoice handoff, receipt, return,
  credit, cancellation, and correction outcomes;
- report definition/publication, source selection, denied drill-through,
  report generation, export request, download, expiry, and reconciliation
  review outcomes;
- file upload, metadata change, quarantine, scan result, authorization,
  download, link expiry, disposition, and hold outcomes;
- material configuration, locale/country-pack, exchange-rate/configuration
  change, approval, activation, supersession, and rollback outcomes without
  deciding MESP-54 or ADR-011;
- migration validation, mapping, quarantine, cutover, rollback, sign-off, and
  reconciliation outcomes under the future migration boundary;
- integration-boundary request, delivery, retry, duplicate prevention,
  rejection, unknown result, and reconciliation outcomes under MESP-39; and
- security incident detection, triage, containment, evidence access,
  remediation, and closure outcomes.

### 10.4 Immutable history and correction

Audit history is business-immutable:

- Tenant Users and ordinary business actions cannot edit or delete prior
  audit evidence;
- a correction, redaction, legal hold, or governance action is itself
  evidenced and linked to the affected evidence;
- a source correction does not erase the fact that the earlier value,
  decision, or attempt existed;
- a reviewer can distinguish original evidence, subsequent correction,
  reversal, reconciliation, and governance disposition; and
- retention, deletion, and purge of audit history remain subject to MESP-50,
  ADR-014, legal/privacy validation, and any other approved gate.

This does not prescribe append-only storage, a database mechanism, a physical
archive, or a retention duration.

### 10.5 Retrieval, search, review, and bounded export

An authorized reviewer must be able to:

- locate evidence by permitted Tenant, organization, object/source,
  actor/authority, event category, outcome, time, and correlation context;
- see enough surrounding evidence to understand a material decision without
  receiving unrelated Tenant data;
- distinguish no result, denied result, incomplete result, unavailable
  result, and unknown result;
- review audit evidence read-only at the business boundary;
- request a bounded audit export only with the required authority and scope;
- see the export request, generation, access, expiry, failure, and disposition
  evidence; and
- reconcile an audit entry to the owning source, report, file, job, incident,
  or governance decision.

The final report catalogue, KPI definitions, named reconciliation ownership,
scheduled distribution, and automatic delivery policy remain with MESP-53.

## 11. Files, private attachments, and downloads

### 11.1 Private-by-default business rule

Attachments, private documents, report results, export artifacts, incident
evidence, and governance evidence are private by default. A file or artifact
is available only when:

1. its Tenant and permitted organizational scope are known;
2. its business owner and source relationship are valid;
3. the requester has current authorization for the purpose and action;
4. required safety, quarantine, scan, or review conditions are satisfied; and
5. the access or denial outcome is evidenced where material.

A filename, object reference, preview, size, metadata, status, or download
response must not disclose another Tenant or an unauthorized private artifact.

### 11.2 Upload and quarantine consequences

The business must distinguish an uploaded artifact from an artifact safe for
ordinary use. The following outcomes are visible to authorized reviewers:

- accepted for review;
- quarantined or awaiting required safety assessment;
- rejected as unsafe, invalid, or unauthorized;
- available for the permitted purpose;
- unavailable because authorization, scan, hold, or lifecycle conditions fail;
- expired or dispositioned under an approved policy; and
- unknown or failed, pending reconciliation.

This BRD does not choose a scanner, provider, file type catalogue, size
limit, malware policy, encryption mechanism, or retention value.

### 11.3 Authorized downloads

A download or view must re-evaluate the current Tenant, organization,
object-state, file-state, purpose, support grant, and permission boundary. A
previously prepared link, export, session, or page is not permanent authority.

Private downloads must:

- remain bounded to the authorized Tenant and purpose;
- avoid unsafe public disclosure or unapproved inline use;
- identify the permitted artifact and its lifecycle state to authorized
  reviewers;
- record material request, allow, deny, failure, expiry, and review outcomes;
- avoid exposing raw secrets, credentials, or unrelated personal data; and
- remain subject to retention, legal hold, deletion, residency, backup, and
  restoration decisions under MESP-50.

### 11.4 File disposition and holds

No file is physically deleted, purged, or restored by this BRD. A disposition
decision must first establish the approved scope, ownership, hold status,
required export/offboarding evidence, authority, notice, and outcome under
the applicable MESP-50 and legal/privacy gates.

## 12. Exports and reporting evidence

### 12.1 Export authorization

An export is a controlled release of data, not an ordinary read. The business
must evaluate:

- requester identity and current Tenant/organization scope;
- export permission, purpose, approval, and any support separation;
- source data, report definition, filters, date/effective context, and
  whether the requested scope is bounded;
- whether the result contains private, sensitive, held, restricted, or
  unresolved data; and
- the allowed destination, reviewer, expiry, and disposition policy where
  approved.

Export authorization at request time does not remove authorization at
generation or download time.

### 12.2 Export lifecycle

The business export lifecycle is:

Not requested -> Requested -> Authorized -> Generating -> Available ->
Downloaded or Released -> Expired or Dispositioned

The lifecycle must also represent Denied, Rejected, Failed, and
Unknown/Pending outcomes. A failed or unknown export must not be described as
complete.

For each material export, evidence identifies the requester, authority,
Tenant/scope, purpose, source/report context, time, result status, download or
release, expiry/disposition, and related reconciliation or incident.

### 12.3 Reporting boundary

Reporting remains read-only and owns report definitions, lineage, freshness,
reconciliation ownership, and final catalogue decisions. MESP-38 requires
that:

- a report or drill-through cannot widen server-derived scope;
- a report showing pending, stale, partial, rejected, or unknown source data
  preserves that status;
- a report cannot repair, post, reverse, or rewrite a source fact;
- report generation, denied access, export, download, and review outcomes are
  auditable; and
- MESP-53 remains open for catalogue, KPI, scheduled distribution, and named
  ownership decisions.

### 12.4 Safe presentation and export content

Generic Arabic, English, bilingual, RTL, locale, and Saudi-oriented
presentation must preserve source identifiers, values, statuses, scope, and
audit meaning. It must not create statutory, tax, privacy, or residency
claims. The localization owner and ADR-011 dependency remain authoritative
for runtime details.

## 13. Observability, monitoring, and incident evidence

### 13.1 Business audit and technical telemetry are different

| Concern | Business audit | Technical telemetry |
| --- | --- | --- |
| Primary question | What business/security decision or effect occurred? | What did the service or operational environment do? |
| Owner | Owning module, Platform governance, Security/Audit, or named evidence owner | Platform operations and the applicable operational owner |
| Content | Actor, Tenant, scope, object/source, authority, decision, change, outcome, reason, correlation | Safe event, trace, metric, health, timing, error category, dependency state, and correlation |
| Access | Authorized business/audit reviewers by purpose and scope | Authorized operational reviewers by operational purpose |
| Retention | MESP-50 and owning governance policy | ADR-010, MESP-50, and production operational policy |
| Mutation | Business-immutable history; corrections are linked evidence | Operational lifecycle may differ, but access and disposition are governed |

Technical telemetry can support an investigation, but a telemetry entry does
not replace required business audit. Business audit can reference a trace or
operational event without copying its unsafe payload.

### 13.2 Safe telemetry

Operational evidence must:

- carry a correlation reference when it relates to a material business event;
- identify a safe Tenant or scope reference only when operationally authorized;
- use categories and summaries rather than secrets, credentials, private file
  bytes, payment credentials, or unnecessary personal data;
- distinguish normal, denied, failed, unavailable, degraded, and unknown
  conditions;
- provide an accountable alert owner and review path where an alert is
  material; and
- avoid presenting diagnostic visibility as business permission.

No OpenTelemetry exporter, operational-data store, access model, or retention
period is selected here. ADR-010 remains a required production decision.

### 13.3 Monitoring and alert ownership

For each material monitoring signal or security alert, the business must know:

- what risk or condition the signal represents;
- the owning operational or security team;
- severity or review urgency as approved by the later operational policy;
- affected Tenant or scope, when safe and authorized;
- the linked incident, evidence, or reconciliation record;
- the expected acknowledgement, investigation, and closure evidence; and
- the outcome when the signal is false positive, duplicate, unavailable, or
  unresolved.

MESP-48 remains the gate for supported volumes, availability, freshness,
capacity, and recovery promises.

### 13.4 Incident evidence lifecycle

The business incident lifecycle is:

Detected -> Triage -> Contained or Monitoring -> Remediation -> Recovery
review -> Closed

An incident may also be Rejected, Duplicate, False positive, or
Unknown/Pending when that status is supported by the later operational
policy. Material incident evidence identifies:

- incident owner, severity/category, and discovery time;
- affected Tenant, scope, services, files, exports, or business processes
  where safe to identify;
- linked alerts, business audit, technical telemetry, support actions,
  approvals, and source/reconciliation records;
- containment, remediation, recovery, residual risk, and closure decision;
- access to sensitive incident evidence and the reason for that access; and
- any unresolved legal, privacy, customer-notification, regulatory, or
  production decision as a named gate rather than an invented conclusion.

This section does not create a legal incident-notification workflow or certify
an incident-response program.

## 14. Data governance consequences

### 14.1 Governance principles

Every relevant data class must have a business owner, purpose, permitted
access boundary, evidence consequence, and open policy dependencies. The
minimum principles are:

- collect and expose only what is needed for the approved business purpose;
- preserve source meaning and material history;
- keep Tenant-owned data and Platform-governance data distinct;
- protect private files, exports, audit, telemetry, support, and incident
  evidence according to purpose and sensitivity;
- record governance decisions and exceptions;
- keep unresolved values visible and approved before production commitment; and
- never use a generic security or audit statement as a legal compliance claim.

### 14.2 Data-governance consequence matrix

| Governance topic | MESP-38 requirement | Preserved gate |
| --- | --- | --- |
| Classification | The owner and permitted reviewer boundary are known for business data, private files, exports, audit, telemetry, support, incident, backup, and reconciliation evidence. | Detailed inventory and legal/privacy validation remain future work. |
| Purpose and minimization | Evidence and telemetry use safe summaries and do not copy secrets, private file bytes, credentials, payment credentials, or unnecessary personal data. | Actual processing inventory and qualified advice remain open. |
| Retention | Retention is an explicit governed decision with owner, scope, start/event basis, exception/hold interaction, review, and disposition evidence. | No duration or schedule; MESP-50 and ADR-014 remain open. |
| Legal hold | A valid hold blocks conflicting deletion/purge for the approved scope and is itself attributable and reviewable. | No legal basis, duration, jurisdiction, or legal conclusion is selected. |
| Deletion and purge | No deletion or purge is represented as complete without approved scope, authority, holds, export/offboarding, evidence, and required review gates. | Physical mechanics, cooling-off, residual copies, and irreversible production action remain MESP-50. |
| Export and portability | Authorized, bounded, private exports preserve scope, status, manifest/context, access, expiry, and disposition evidence. | Final policy, content catalogue, and legal portability obligation remain open. |
| Residency and hosting | Any residency or support-access value must be an explicit approved decision, not an inference from Saudi localization or Wafra. | MESP-50, ADR-014, legal/privacy, contractual, and infrastructure gates. |
| Privacy-oriented review | A reviewer can identify purpose, category, access, change, hold, export, and disposition consequences without a PDPL claim. | No DPO/controller/DSR/TIA/SCC/BCR/certification decision. |
| Offboarding | Termination revokes ordinary access, preserves required evidence, records export/hold/disposition decisions, and does not imply immediate purge. | MESP-27/MESP-50 and future qualified validation. |
| Backup and restoration | Backup/restoration decisions must preserve Tenant isolation, evidence integrity, source lineage, access control, and restoration review. | No backup topology, location, schedule, RPO/RTO, or restoration promise; MESP-48/MESP-50 remain open. |
| Subprocessors and operational access | Provider, subprocessor, support geography, and operational access are recorded only after approved production and legal decisions. | ADR-009, ADR-010, ADR-013, ADR-014, and MESP-50. |

### 14.3 Offboarding and termination evidence

Before a Tenant enters governed retention or a later purge review, the
business must be able to review:

- the termination authority, effective time, reason, and notice outcome;
- active-session, membership, support, integration, and background-work
  closure or restriction outcomes;
- authorized export request, scope, manifest/context, access, failure,
  expiry, and disposition;
- audit, file, report, incident, reconciliation, and hold dependencies;
- residual copies or restoration implications where the approved policy
  requires them; and
- the accountable review and unresolved gate status.

Termination does not itself delete, purge, anonymize, or make restoration
impossible.

### 14.4 Backup and restoration consequences

Any later approved backup or restoration process must demonstrate:

- the restored data remains inside the correct Tenant and organization
  boundary;
- source, audit, file, export, configuration, and incident evidence retain
  their relationship and historical meaning;
- restored or replayed work is not silently duplicated or reauthorized;
- access, support, credentials, keys, and operational review are controlled;
- unknown or partial restoration outcomes are visible and reconciled; and
- the process does not claim a recovery target or production equivalence
  before MESP-48/MESP-50 and related production gates close.

## 15. Cross-module control and evidence handoffs

| Domain | Domain-owned facts and actions | MESP-38 evidence consequence |
| --- | --- | --- |
| Platform Administration | Tenant catalogue/lifecycle, Entitlement, support case, export/offboarding, governance and purge approval | Evidence identifies Platform actor, Tenant, case/purpose, scope, approval, effective time, notice, and outcome; no Tenant business-data bypass. |
| Identity and Access | User, Membership, Role, Permission, session, scope, support and SoD meaning | Every material allow/deny/assignment/revocation/support decision is attributable; MESP-38 does not redefine permissions. |
| Multi-Tenancy | Tenant context, lifecycle, isolation, Platform/Tenant ownership | All audit, file, export, job, search, report, telemetry-review, and incident paths preserve Tenant boundary and safe denial. |
| Organization | Company/Legal Entity, Branch, Warehouse, hierarchy and scope | Evidence names applicable downward organization scope without granting upward or cross-entity access. |
| Master Data | Product/Item identity, UOM, category, supplier/customer, pricing, tax and related facts | Change and access evidence preserves source identity, effective status, actor, scope, reason, and outcome; no new master-data policy. |
| Procurement | Purchase requests/orders, receipts, returns, matching, supplier and exception facts | Handoffs retain source identity, quantities, amounts, approvals, exceptions, downstream acceptance, and reconciliation links. |
| Inventory | Physical movements, count, adjustment, tracking, reservation, availability and valuation boundaries | Material outcomes, unknown/failure, correction, and reconciliation are evidenced; MESP-113/INV-OD-004 stays open. |
| Finance | Journals, subledgers, invoices, payments, periods, posting, reversal, reconciliation and currency facts | Evidence preserves source-to-posting lineage, authority, period, approval, SoD, close/reopen/reclose outcome, and unknown state; MESP-54/MESP-110 stay open. |
| B2B Sales | Quotation, order, fulfillment, invoice handoff, receipt, return, customer and commercial facts | Evidence preserves commercial lineage, scope, approval, status, source handoff, and correction without creating Retail POS. |
| Reporting | Definitions, lineage, freshness, data-as-of, results, reconciliation and export presentation | Report access/generation/download/review is evidenced; MESP-53 owns final catalogue, KPI, owner, schedule, and distribution decisions. |
| Saudi localization | Language, RTL, locale, country-pack and presentation consequences | Localization preserves authority, scope, values, statuses, and audit meaning; no statutory, tax, legal, privacy, or residency claim. |
| Migration | Source mapping, validation, quarantine, cutover, rollback, sign-off and reconciliation | Ambiguity, rejected rows, source owner, approval, cutover, rollback, and post-cutover reconciliation remain visible under MESP-40. |
| Future Integrations | External boundary, delivery, retry, idempotency, unknown result and reconciliation | MESP-38 requires safe evidence and no silent loss; MESP-39 owns the later detailed business contract and is not activated here. |
| Files and exports | Private artifacts, source relationships, quarantine, downloads, report/export result | Artifact identity, Tenant/scope, scan/disposition state, authorization, access, expiry, hold, and outcome are reviewable. |
| Security/operations | Alerts, telemetry, incidents, support and operational recovery | Technical evidence is safe and correlated; business audit remains authoritative for business effects. |

## 16. Security, audit, and governance business rules

The following stable identifiers are MESP-38 business requirements. They
describe business outcomes, not implementation contracts.

| ID | Requirement |
| --- | --- |
| SADG-BR-001 | Every material business, access, configuration, file, export, support, lifecycle, integration-boundary, incident, and governance outcome is attributable and reviewable at its owning scope. |
| SADG-BR-002 | Tenant and organization scope are server-derived from the approved IAM, Multi-Tenancy, and Organization baselines; client-provided identifiers cannot widen authority. |
| SADG-BR-003 | Cross-Tenant and out-of-scope reads, searches, files, exports, jobs, telemetry reviews, and writes fail closed without disclosing the protected scope. |
| SADG-BR-004 | A material decision identifies the actor or controlled process, authority context, requested action, target/source, scope, reason, and outcome as applicable. |
| SADG-BR-005 | Revoked, expired, suspended, terminated, stale, or otherwise invalid authority cannot be silently reused by a prepared action, link, export, job, or support context. |
| SADG-BR-006 | Object state, approval, SoD, amount, period, and contextual conditions remain owned by the relevant domain and are evidenced when material. |
| SADG-BR-007 | Support access is named, case-bound, purpose-bound, least-privilege, exact-scope, time-bounded, revocable, and fully evidenced; support does not imply export or source-mutation authority. |
| SADG-BR-008 | Self-approval and conflicting duties are denied where the approved owner policy prohibits them, and the denial is reviewable. |
| SADG-BR-009 | Material successful, denied, rejected, failed, unknown, reversed, corrected, and reconciled outcomes are distinguishable and are not falsely represented as success. |
| SADG-BR-010 | Audit evidence includes the business context needed to reconstruct actor, Tenant, organization scope, object/source, decision, change, outcome, reason, and correlation without unsafe payloads. |
| SADG-BR-011 | Business audit history is immutable to Tenant Users; correction, redaction, hold, disposition, and reconciliation are new linked evidence rather than silent history changes. |
| SADG-BR-012 | Authorized reviewers can retrieve and search permitted evidence by bounded context and can distinguish no result, denied result, unavailable result, incomplete result, and unknown result. |
| SADG-BR-013 | Audit and export access are themselves authorized, bounded, private, expiring or dispositioned where policy requires, and evidenced. |
| SADG-BR-014 | Files, attachments, reports, exports, incident evidence, and governance artifacts are private by default and unavailable until ownership, authorization, and required safety conditions are satisfied. |
| SADG-BR-015 | A quarantined, unsafe, rejected, expired, held, unavailable, or unknown artifact cannot be presented as ordinary usable content. |
| SADG-BR-016 | A download or view re-evaluates current Tenant, organization, object, file, purpose, support, and permission conditions; a prior link or page is not permanent authority. |
| SADG-BR-017 | Export generation and release preserve requested scope, source/report context, status, access, expiry/disposition, and related reconciliation evidence. |
| SADG-BR-018 | Reporting remains read-only and cannot widen scope, change source facts, close an open decision, or create a final catalogue or reconciliation owner outside MESP-53. |
| SADG-BR-019 | Business audit and technical telemetry remain distinct, correlated where useful, and protected from secrets, credentials, private file content, and unnecessary personal data. |
| SADG-BR-020 | Material alerts and incidents have an accountable owner, affected scope where safe, linked evidence, review status, remediation/closure outcome, and visible uncertainty. |
| SADG-BR-021 | Monitoring or telemetry failure does not silently erase required business audit or represent an unobserved effect as verified. |
| SADG-BR-022 | Each governed data class has a business purpose, owner, permitted access boundary, evidence consequence, and named open policy dependencies. |
| SADG-BR-023 | Retention, legal hold, deletion, purge, export, residency, privacy, backup, restoration, and offboarding decisions are explicit, attributable, reviewable, and not invented by MESP-38. |
| SADG-BR-024 | A valid legal hold blocks conflicting disposition for its approved scope; hold creation, review, release, conflict, and exception outcomes are evidenced without selecting legal duration or basis. |
| SADG-BR-025 | Termination revokes ordinary access and preserves required evidence while recording export, support, integration, file, incident, reconciliation, hold, and disposition consequences; termination does not imply purge. |
| SADG-BR-026 | Any later backup or restoration outcome preserves Tenant isolation, source lineage, audit/file relationships, access control, and non-duplication, with unknown results reconciled. |
| SADG-BR-027 | Finance close, reopen, reclose, posting, reversal, and reconciliation evidence is attributable and SoD-aware without deciding MESP-110 mechanics. |
| SADG-BR-028 | Inventory material events and privileged operations carry evidence and reconciliation consequences without deciding MESP-113 or INV-OD-004. |
| SADG-BR-029 | Configuration and locale/country-pack changes preserve effective version, authority, scope, safe change summary, and outcome without selecting MESP-54 or ADR-011 policy. |
| SADG-BR-030 | Cross-module handoffs retain source identity, scope, authority, correlation, business status, and unknown/failure/reconciliation state. |
| SADG-BR-031 | Wafra examples validate reusable requirements only and cannot create Wafra-specific security, audit, governance, workflow, or data behavior. |
| SADG-BR-032 | Generic Saudi-localized presentation preserves security scope, source meaning, evidence identity, and audit history and makes no statutory, legal, privacy, or residency claim. |
| SADG-BR-033 | No production provider, hosting region, telemetry exporter, secrets/key manager, retention value, RLS position, backup topology, or physical purge method is selected by this BRD. |
| SADG-BR-034 | Any later implementation or production decision must trace to these requirements, the owning domain, an approved ADR/decision, and evidence that the named gates are closed. |

## 17. Governance and evidence processes

### 17.1 Material action process

1. A requester or controlled process identifies the business purpose and
   intended scope.
2. The owning authority baseline evaluates current identity, membership,
   permission, organization, object state, support, approval, and SoD
   conditions.
3. The action is allowed, denied, rejected, failed, or left unknown according
   to the owning domain and the evidence requirements.
4. Required business audit evidence identifies the decision and any effect,
   with a correlation to related files, exports, jobs, incidents, or
   reconciliations.
5. An authorized reviewer can retrieve the result; an unknown or failed result
   remains visible until an accountable reconciliation closes it.

### 17.2 Evidence review process

1. The reviewer establishes an authorized Tenant, organization, purpose, and
   evidence boundary.
2. The reviewer searches or retrieves permitted evidence.
3. The reviewer distinguishes complete, partial, stale, denied, unavailable,
   failed, and unknown evidence.
4. The reviewer records a finding, correction, reconciliation, or escalation
   without rewriting the original history.
5. The review outcome is linked to the evidence and assigned to the owning
   domain or governance owner.

### 17.3 Governance decision process

1. A retention, hold, export, deletion, residency, backup, restoration, or
   offboarding question is identified with its data scope and owner.
2. Conflicting open decisions, legal/privacy dependencies, and production
   gates are recorded.
3. An authorized decision or qualified validation is obtained where required.
4. The effective policy, exception, scope, and review outcome are evidenced.
5. No irreversible or production action is treated as approved before all
   named gates are closed.

### 17.4 Status outcomes

The business must make the following status meanings reviewable for any
material security, audit, artifact, incident, export, or governance process:

Not requested -> Requested -> Authorized -> In progress -> Completed

The same process must support Denied, Rejected, Failed, Unknown/Pending,
Reconciled, Expired, Held, and Dispositioned where the owning domain permits
those states. A status label cannot hide an unresolved dependency or an
unauthorized effect.

## 18. Given / When / Then acceptance scenarios

These are business acceptance scenarios, not automated test instructions or a
technical test specification.

### 18.1 Tenant, authority, and scope

**SADG-GWT-001 - Cross-Tenant record denial**

**Given** a User is authorized in Tenant A
**When** the User requests a Tenant B record
**Then** the request is denied without revealing Tenant B data or existence,
and the material security outcome is safely evidenced.

**SADG-GWT-002 - Client identifier cannot widen authority**

**Given** a User supplies a Tenant, Company, Branch, Warehouse, object, file,
or export identifier outside the current authorized scope
**When** the request is evaluated
**Then** the supplied identifier does not widen authority, no protected
content is disclosed, and the decision is reviewable.

**SADG-GWT-003 - Cross-Tenant search and export**

**Given** a search or export is requested for Tenant A
**When** its requested filters would include Tenant B
**Then** the result is denied or corrected before release, and no mixed-Tenant
result or export is made available.

**SADG-GWT-004 - Background work remains Tenant-bound**

**Given** a job or asynchronous business action was initiated for Tenant A
**When** it is retried, resumed, or reviewed
**Then** it remains Tenant A-bound or is denied; it cannot affect or expose
Tenant B.

**SADG-GWT-005 - Revoked authority**

**Given** a User's membership, permission, scope, or support grant is revoked
**When** a prepared action, link, export, or download is used afterward
**Then** current authority is re-evaluated, the action is denied or held, and
the stale-authority outcome is evidenced.

**SADG-GWT-006 - Organization scope**

**Given** a User is authorized for one Branch or Warehouse
**When** the User requests another out-of-scope organization object
**Then** the request is denied without upward or lateral scope expansion, and
the audit context identifies the permitted boundary.

### 18.2 Support, approval, and SoD

**SADG-GWT-007 - Approved support**

**Given** a named support case, support User, Tenant approval, exact purpose,
exact scope, and valid time interval exist
**When** support begins
**Then** only that boundary is available and the start, authority, scope,
purpose, and outcome are evidenced.

**SADG-GWT-008 - Support expiry**

**Given** a support interval has expired or been revoked
**When** the support User attempts another action
**Then** access is denied and the post-expiry attempt is evidenced without
granting a new scope.

**SADG-GWT-009 - Support export separation**

**Given** support access is authorized
**When** the support User requests an export
**Then** the export remains denied unless separate export authority and any
required Tenant authorization exist; support access alone is not enough.

**SADG-GWT-010 - Self-approval**

**Given** the owning policy requires a separate approver
**When** a requester attempts to approve their own material action
**Then** approval is denied and the conflict is reviewable.

**SADG-GWT-011 - Delegated authority**

**Given** a delegated authority is not approved by the owning policy
**When** a User attempts to act as a substitute approver
**Then** the action is denied or held and no delegation is inferred.

### 18.3 Audit evidence and history

**SADG-GWT-012 - Material action evidence**

**Given** an authorized User completes a material business or configuration
action
**When** the effect is accepted by the owning domain
**Then** evidence identifies the actor/context, Tenant and scope, target/source,
decision, safe change summary, time, outcome, and correlation as applicable.

**SADG-GWT-013 - Denied action evidence**

**Given** a material action is denied
**When** the decision is returned
**Then** the user receives no protected data, the reason is business-safe, and
the evidence distinguishes denial from failure or completion.

**SADG-GWT-014 - Unknown outcome**

**Given** an action's authoritative outcome cannot yet be established
**When** the action is reviewed
**Then** it remains Unknown/Pending, is not reported as successful, and has an
accountable reconciliation path.

**SADG-GWT-015 - Correction preserves history**

**Given** a material source or configuration value must be corrected
**When** the owning domain applies an authorized correction
**Then** the earlier evidence remains reviewable, the correction links to it,
and no silent history rewrite occurs.

**SADG-GWT-016 - Audit history protection**

**Given** a Tenant User attempts to edit or delete audit history
**When** the request is evaluated
**Then** it is denied and the history remains available to authorized
reviewers subject to governance gates.

**SADG-GWT-017 - Bounded audit retrieval**

**Given** an authorized reviewer requests evidence for a permitted Tenant,
scope, object, actor, time, or correlation
**When** the evidence is retrieved
**Then** the reviewer can distinguish no result, denied result, incomplete
result, unavailable result, and unknown result without seeing unrelated
Tenant evidence.

**SADG-GWT-018 - Audit export**

**Given** an authorized reviewer requests a bounded audit export
**When** it is generated and downloaded
**Then** request, authorization, scope, source context, generation, download,
expiry/disposition, and outcome are evidenced, and the artifact remains private.

### 18.4 Files and exports

**SADG-GWT-019 - Quarantined attachment**

**Given** an attachment has not passed the required safety or scan condition
**When** a User attempts ordinary use or download
**Then** the artifact remains unavailable or clearly quarantined and is not
presented as safe content.

**SADG-GWT-020 - Private attachment boundary**

**Given** a file belongs to Tenant A and an authorized context belongs to
Tenant B
**When** the file is requested
**Then** access is denied without revealing its metadata or content, and the
denial is safely evidenced.

**SADG-GWT-021 - Download reauthorization**

**Given** a private link or prepared download was created while authority was
valid
**When** the User's scope or purpose is later revoked
**Then** the download is denied or held after current authorization is
re-evaluated.

**SADG-GWT-022 - Export expiry**

**Given** an export artifact reaches its approved expiry or disposition state
**When** it is requested afterward
**Then** it is unavailable, the outcome is clear to the authorized reviewer,
and no new release is implied.

**SADG-GWT-023 - Export contains unresolved data**

**Given** a report or export contains pending, stale, partial, rejected, or
unknown source facts
**When** the artifact is generated
**Then** those statuses remain visible and the artifact cannot imply a complete
or reconciled business result.

### 18.5 Telemetry and incidents

**SADG-GWT-024 - Audit and telemetry distinction**

**Given** a material business action produces technical telemetry
**When** an authorized reviewer investigates it
**Then** business audit remains the source of business effect and telemetry is
linked only as supporting operational evidence.

**SADG-GWT-025 - Safe telemetry**

**Given** a security or operational error contains sensitive payload material
**When** technical evidence is recorded
**Then** the evidence uses a safe category or summary and does not expose
secrets, credentials, private file bytes, payment credentials, or unnecessary
personal data.

**SADG-GWT-026 - Alert ownership**

**Given** a material security or operational alert is raised
**When** it is reviewed
**Then** an accountable owner, severity/review status, affected scope where
safe, linked evidence, and acknowledgement or closure outcome are visible.

**SADG-GWT-027 - Incident closure**

**Given** an incident has been contained and remediated
**When** closure is approved
**Then** the incident retains its timeline, linked audit/telemetry/support
evidence, residual uncertainty, remediation, recovery review, and accountable
closure decision.

**SADG-GWT-028 - Monitoring failure**

**Given** a telemetry or monitoring path is unavailable
**When** a related business effect is reviewed
**Then** the missing telemetry is visible as an operational limitation and
cannot be treated as proof that the business effect did not occur or that it
was safe.

### 18.6 Governance, offboarding, and recovery

**SADG-GWT-029 - Retention value is unresolved**

**Given** a production user asks for a retention duration or purge schedule
**When** MESP-50 or the required legal/privacy validation is not approved
**Then** no duration or irreversible action is treated as a Release 1
production decision.

**SADG-GWT-030 - Legal hold**

**Given** an approved hold applies to a data or evidence scope
**When** a conflicting deletion or purge request is reviewed
**Then** the request is blocked or held, the conflict and authority are
evidenced, and no legal duration or conclusion is invented.

**SADG-GWT-031 - Tenant offboarding**

**Given** a Tenant is terminated
**When** offboarding is reviewed
**Then** ordinary access is revoked, export/hold/file/audit/incident/
reconciliation consequences are recorded, and termination does not imply purge.

**SADG-GWT-032 - Restoration boundary**

**Given** a later approved restoration produces a Tenant's data and evidence
**When** the restored state is reviewed
**Then** Tenant isolation, source lineage, audit/file relationships, access
controls, non-duplication, and unknown/partial outcomes remain reviewable.

### 18.7 Cross-module and deferred-policy boundaries

**SADG-GWT-033 - Finance close evidence**

**Given** a privileged Finance close, reopen, or reclose action is allowed by
the owning policy
**When** the action completes or fails
**Then** actor, Company/Legal Entity, period, authority, SoD, reason, outcome,
and reconciliation context are evidenced without deciding MESP-110 mechanics.

**SADG-GWT-034 - Inventory unresolved tracking policy**

**Given** a material Inventory event depends on unresolved INV-OD-004
**When** the event is reviewed
**Then** the event and policy dependency are evidenced, but MESP-38 does not
choose batch, lot, serial, expiry, or MESP-113 policy.

**SADG-GWT-035 - Exchange-rate configuration**

**Given** an authorized material rate or currency configuration action occurs
**When** it is reviewed
**Then** the actor, scope, effective context, safe change summary, source/
policy reference, and outcome are evidenced without selecting MESP-54 policy.

**SADG-GWT-036 - Reporting catalogue gate**

**Given** an audit or governance report is requested before MESP-53 assigns
the final catalogue and reconciliation ownership
**When** the result is reviewed
**Then** the requested evidence remains bounded and traceable, while no final
report catalogue, KPI, schedule, distribution, or owner is invented.

**SADG-GWT-037 - Cross-module unknown handoff**

**Given** a Procurement, Inventory, Finance, Sales, Reporting, migration, or
future integration handoff has an unknown or failed outcome
**When** the downstream reviewer assesses it
**Then** the source identity, scope, correlation, status, owner, and
reconciliation path remain visible and no silent effect is assumed.

**SADG-GWT-038 - Localization preserves security meaning**

**Given** an authorized reviewer switches between Arabic, English, RTL, or
Saudi-oriented presentation
**When** security, audit, file, export, status, and source evidence is viewed
**Then** scope, identity, values, outcomes, and history remain unchanged and
no statutory, privacy, legal, or residency claim appears.

**SADG-GWT-039 - Wafra validation-only**

**Given** Wafra provides a sample Tenant, file, export, incident, or audit
case
**When** the case is assessed against this BRD
**Then** it validates a reusable requirement only and cannot create
Wafra-specific core security, workflow, provider, or governance behavior.

**SADG-GWT-040 - External production request**

**Given** a request asks for a government, banking, tax, e-invoicing,
payment-provider, external identity, or other production integration
**When** it is assessed against this BRD
**Then** it is routed to a separately approved future integration or statutory
boundary and is not treated as Release 1 production capability.

## 19. Open decisions, gates, and non-resolution record

This table preserves the live boundary at MESP-38 completion. It is a
traceability map, not a new Product Decision Register. No row is closed,
answered, or superseded by this BRD.

| ID / gate | Live position | MESP-38 treatment |
| --- | --- | --- |
| MESP-23 | In Progress; living Open Questions Register | Preserve every open row and record the MESP-38 handoff; do not answer a row here. |
| MESP-48 | To Do/open | No supported volume, capacity, freshness, availability, recovery, or async threshold is selected. |
| MESP-50 | To Do/open | No retention, privacy, legal hold, purge, residency, backup, restoration, hosting, subprocessor, or production deletion policy is selected. |
| MESP-53 | To Do/open | No final report catalogue, KPI/figure, named reconciliation owner, schedule, or distribution policy is selected. |
| MESP-54 | To Do/open | No exchange-rate source, update cadence, effective-date, Reporting Currency, conversion, rounding, or approval policy is selected. |
| MESP-110 / FIN-OD-09 | To Do/open | No fiscal-year, year-end, Payment Term, due-date, aging, settlement, posting-dimension, or retained-earnings mechanic is selected. |
| MESP-113 / INV-OD-004 | To Do/open/unapproved | No batch, lot, serial, expiry, tracking, or physical Inventory policy is selected. |
| ADR-003 | Approved baseline; detailed/provider validation gated | Consume shared-database Tenant-isolation boundary; do not claim production validation. |
| ADR-005 | Approved policy/resource baseline | Consume authorization semantics; do not create a competing permission catalogue. |
| ADR-010 | Required production decision | Do not select telemetry exporter, operational access, or telemetry retention. |
| ADR-013 | Required production decision | Do not select secrets or encryption-key provider/lifecycle. |
| ADR-014 | Required production decision | Do not select residency, retention, legal hold, export, or purge policy. |
| ADR-016 | Index-only entry | Do not manufacture a missing ADR or choose SQL Server RLS adoption/deferral. |
| ADR-011 | Localization dependency, not primary owner | Preserve runtime localization/search/RTL/bilingual implementation gate. |
| Legal/privacy/external validation | Outstanding where applicable | No PDPL, DPO, controller, DSR, TIA, SCC/BCR, regulator, certification, banking, tax, or statutory conclusion is made. |

## 20. Future implementation and production handoff

This section is a readiness boundary, not a coding authorization or Definition
of Ready for an implementation item.

Before a later implementation or production decision may use this BRD, the
delivery owner must have:

- a reviewed and approved trace from each implementation requirement to a
  SADG-BR identifier, an owning domain, and an acceptance scenario;
- the approved ownership and authorization baselines for Tenant, IAM,
  Organization, support, SoD, and the affected source module;
- a complete material-event catalogue and evidence ownership map;
- agreed business classifications and safe evidence rules for private files,
  exports, audit, telemetry, incident, support, backup, and reconciliation;
- approved treatment for all affected MESP-23 rows and named gates;
- MESP-48 evidence for supported volume, capacity, recovery, freshness, and
  availability where relevant;
- MESP-50 and qualified validation for retention, privacy, legal hold,
  residency, backup, restoration, export, offboarding, and purge where
  relevant;
- the applicable ADR-003/005/009/010/013/014/016 decisions and provider gates;
- domain scenarios for allow, deny, failure, unknown, correction,
  reconciliation, support, file, export, incident, and offboarding outcomes;
- independent review of Tenant isolation, accounting/data integrity, and
  destructive-operation risk before production enablement; and
- no unapproved Currency, statutory tax, ZATCA/FATOORA, external integration,
  Retail POS, or Wafra-specific behavior in the affected scope.

## 21. Definition of Done and approval handoff

### 21.1 Definition of Done

The bounded MESP-38 BRD session is complete when:

- this canonical artifact contains business purpose, actors, scope,
  ownership, traceability, evidence requirements, files/downloads, exports,
  telemetry, incidents, governance consequences, cross-module handoffs,
  business rules, open gates, and GWT scenarios;
- the consume-don't-redefine rule is respected for MESP-27 through MESP-37,
  MESP-23, MESP-113, and future MESP-39;
- no implementation, production, provider, legal, statutory, privacy,
  capacity, Currency, Finance, Inventory, Retail POS, or Wafra-specific
  decision is invented;
- all named open gates remain visible and unclosed;
- the focused Markdown diff passes whitespace/reference validation and
  contains no prohibited source or configuration changes;
- Jira contains activation, validation, Owner approval, MESP-23 handoff,
  final audit, closure, reviewed-content, merge, and final-main evidence; and
- the focused change is merged only after clean review, with main,
  origin/main, and the worktree synchronized.

### 21.2 Approval and handoff record

This document is an **Approved bounded Security, Audit, and Data Governance
business baseline**. The approval is limited to the business requirements
and scenarios in this document. It authorizes no implementation, production
configuration, legal conclusion, external integration, or closure of MESP-23,
MESP-48, MESP-50, MESP-53, MESP-54, MESP-110, MESP-113, or any ADR gate.

Validation is recorded in Jira comment 10935 and Hossam's standing Owner
approval is recorded in comment 10936. The MESP-23 handoff is comment 10937.
The approved content was initially reviewed at focused PR #57 head
315709856afc85dc0937880637af78e33e34a0e7; the final reviewed artifact head,
merge commit, and final-main synchronization are recorded in the final Jira
audit and closure evidence.

Jira evidence is the authoritative record for:

- MESP-38 activation;
- focused artifact validation and non-claim review;
- Hossam's Owner approval at this exact bounded scope;
- MESP-23 open-register handoff;
- final audit and closure;
- reviewed content head, Pull Request, merge commit, and final main
  synchronization.

The next exact session after genuine MESP-38 completion is MESP-39 -
Integrations and External Services BRD only. MESP-39 remains To Do and is not
activated by this document.
