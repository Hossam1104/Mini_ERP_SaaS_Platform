# Reporting and Analytics Business Requirements Document

> **Version:** v0.1 - Approved Business Baseline
> **Jira:** MESP-36 - Produce Reporting and Analytics BRD
> **Parent:** MESP-11 - EPIC 11 - Reporting and Analytics
> **BRD sequence:** Position 11 of 15, following the approved B2B Sales baseline
> **Date:** 11 August 2026
> **Scope:** Release 1 B2B ERP only; Wafra is validation-only
> **Status:** Approved business baseline; documentation-only; no implementation authorization
> **Approval evidence:** Jira comments 10770 (validation) and 10771 (Owner approval), reviewed content head a7a7ae8aed0d601bd16609acc3edf8ab27bdd97f

## 1. Document control and reading rules

This document is the bounded Release 1 business-requirements baseline for
Reporting and Analytics. It describes how an authorized Tenant user obtains
read-only operational and financial information, how a report identifies its
source facts and freshness, and how the result exposes a controlled
reconciliation path. It also records the conditional branches that must remain
visible until their owning decision is approved.

The Reporting domain is a consumer and presentation boundary. It is never a
second source of transactional, stock, subledger, or General Ledger truth.
Reporting may read approved source facts, calculate a measure only from an
approved versioned definition, record report metadata and reconciliation
evidence, and expose authorized results. It must not create, post, reserve,
receive, deliver, invoice, pay, collect, adjust, revalue, reverse, or otherwise
mutate a source business record.

This is a business document. It does not define database tables, entities,
migrations, API contracts, controllers, screens, framework behavior, provider
selection, deployment topology, production configuration, or an automated
test implementation. It authorizes none of those activities. A later
implementation-readiness item must preserve the evidence and must not turn an
open decision, recommendation, or conditional branch into a hidden default.

The approved Procurement, Inventory, Finance, and B2B Sales BRDs were read
before authoring this baseline. Their ownership boundaries remain authoritative.
Reporting describes the read and lineage handoffs at those boundaries; it does
not close a domain decision or take ownership of a source process.

### 1.1 Classification legend

| Classification | Meaning in this BRD |
|---|---|
| **Confirmed baseline** | Directly supported by the approved PRD, approved glossary, approved upstream BRD, ADR boundary, or named approved Jira decision. |
| **BRD requirement** | Business behavior required by this baseline after Owner approval, subject to the named gates and open decisions. |
| **Open decision / gate** | Not approved. The affected branch must remain visible and cannot be implemented as an implicit default. |
| **Conditional branch** | A business path described so the end-to-end process is coherent, while its policy details require the named Owner or external validation. |
| **Recommendation only** | A proposal retained for later decision. It is not a requirement, acceptance criterion, or implementation instruction. |
| **External validation** | Qualified tax, Saudi, legal, banking, privacy, security, or other specialist validation required before the affected release or production gate. |
| **Out of scope** | Excluded from Release 1 or from this Reporting domain baseline. |

The Founder Decision Pack is not an approval catalogue. Its defaults are not
requirements unless a named approval record says otherwise. The MESP-23 living
register remains the control point for unresolved decisions. MESP-52 / PD-020
(entitlement approval) and MESP-56 / PD-021 (multiple legal entities with no
Release 1 consolidation, intercompany, elimination, or transfer pricing) are
the applicable approved decisions carried into this baseline; they do not
resolve Reporting-specific open decisions.

### 1.2 Critical entry and dependency position

The entry gate was reverified immediately before MESP-36 activation on
11 August 2026 and recorded in Jira comment 10769. The live position is:

| Item | Live status before MESP-36 activation | Reporting consequence |
|---|---|---|
| MESP-35 | Done | The approved B2B Sales source boundary is available to consume. |
| MESP-109 | Done, accepted PASS WITH NON-BLOCKING FINDINGS | Finance reconciliation evidence is available; its non-blocking findings remain evidence, not new Reporting authority. |
| MESP-36 | To Do, then activated as the single intended active item | This BRD is the only bounded session executed here. |
| MESP-23 | In Progress | Open decisions remain governed by the living register. |
| MESP-53 | To Do and unapproved | **Critical Reporting dependency** for the final catalogue, figure definitions, named business owners, reconciliation ownership, and any scheduled/export branch. |
| MESP-54 | To Do and unapproved | Reporting Currency, exchange-rate sourcing/update/approval, and rounding remain unresolved Finance policy. |
| FIN-OD-09 / MESP-110 | To Do and unapproved | Fiscal-year/year-end, Payment Term, due-date/aging, settlement history, and Finance posting dimensions remain unresolved Finance policy. |
| MESP-37 | To Do | Saudi/localization BRD is not activated or executed in this session. |
| Currency / M95-SL-06 | Unexecuted and unstarted | Currency and Exchange Rate work remains outside this session and gated by MESP-54. |

The table is a status record, not an approval of any row. MESP-53 is
intentionally carried as a hard gate: this BRD defines the minimum PRD
reporting baseline and the contracts required to make a later catalogue
decision safe, but it does not approve the final Release 1 report catalogue,
KPI formulas, named reconciliation owners, or scheduled distribution policy.

## 2. Executive summary

Release 1 Reporting and Analytics provides a controlled way for an authorized
user to view operational dashboards and repeatable reports across the B2B ERP
domains. Every result must identify the Tenant and authorized organizational
scope, the time and currency facts used, the source records or snapshots
consulted, whether the data is transactional or asynchronously projected, the
data-as-of point, the freshness state, and any reconciliation result or
unresolved exception.

The minimum baseline is the approved PRD catalogue in section 19.2:
executive, procurement, inventory, sales, finance, and SaaS administration
reporting. That table is a validation baseline, not the final Release 1
catalogue. MESP-53 must decide the final set, figure definitions, named
business owners, reconciliation accountability, and whether configurable
parameters, saved views, scheduled delivery, or export distribution are
permitted.

Reporting consumes source truth as follows:

- Procurement owns the commercial purchasing chain and exception meaning.
- Inventory owns physical receipts, stock movements, quantity balances,
  tracking, and valuation evidence within its approved boundary.
- B2B Sales owns the commercial selling chain and its status meaning.
- Finance owns subledgers, GL, tax, cash, periods, currencies, rates,
  posting, reversal, and financial reconciliation.
- SaaS Administration and audit sources own platform access, entitlement,
  support, job, and audit event meaning.
- Reporting owns the publication boundary, report metadata, source lineage,
  freshness presentation, and read-only reconciliation evidence. It does not
  own or mutate the facts above.

The core invariant is:

> A report is an authorized, time- and scope-bounded view of source facts. It
> may expose a difference or a pending/unknown state, but it cannot repair the
> difference, post a financial entry, change stock, or silently select an
> unapproved policy.

## 3. Purpose and desired outcomes

The Reporting domain must provide these business outcomes:

- authorized Tenant-scoped dashboards and reports for the approved PRD
  minimum baseline;
- a clear distinction between posted, pending, projected, stale, partial,
  missing, denied, and unknown source information;
- repeatable report parameters and a data-as-of point that allow an authorized
  user to understand what was included;
- drill-through or traceable references to authorized source records without
  leaking records outside the user's server-derived scope;
- lineage from master data and each operational or financial source to the
  report figure or row;
- a documented reconciliation path for every report that presents a posted
  financial or stock quantity/value fact;
- immutable evidence of report definition versions, effective dates,
  generation attempts, exceptions, corrections, exports, and access;
- safe behavior under delayed, duplicate, concurrent, retryable, dead-letter,
  or unknown delivery outcomes;
- Arabic and English presentation support within the approved localization
  boundary, without declaring Saudi statutory compliance;
- a migration and opening-state reporting path that preserves source snapshots,
  rejection evidence, and reconciliation results under MESP-51; and
- explicit decision gates so a later implementation cannot invent KPI,
  currency, tax, aging, year-end, dimension, ownership, or distribution policy.

## 4. Scope and boundaries

### 4.1 In scope for this Reporting BRD

- operational dashboards and standard reports for the PRD minimum baseline;
- report definition, publication, version, effective-date, and supersession
  requirements;
- report filters, server-derived scope, drill-through, and bounded export
  requirements;
- source semantics and lineage across master data, Procurement, Inventory,
  B2B Sales, Finance, SaaS Administration, and audit;
- data freshness and data-as-of requirements for transactional and projected
  results;
- posted, pending, unknown, partial, stale, rejected, and unavailable result
  presentation;
- reconciliation paths, difference evidence, and unresolved owner gates;
- permissions, approval controls, separation of duties, audit, privacy, and
  export evidence;
- conditional report branches for tracking/expiry, Payment Term/aging,
  Reporting Currency, tax/localization, migration, and scheduled delivery;
- integration, asynchronous work, idempotency, retry, dead-letter, recovery,
  volume, retention, residency, and backup gate requirements; and
- business-level Given/When/Then acceptance scenarios.

### 4.2 Explicitly out of scope

- final approval of the Release 1 report catalogue, KPI formulas, named
  business owners, or named reconciliation owners;
- a report scheduler, distribution list, default frequency, recipient policy,
  saved-view policy, or automatic export policy;
- creation or mutation of transaction, stock, subledger, GL, tax, customer,
  supplier, Product, currency, or audit source data;
- Product/Item implementation, SKU/Barcode rules, Category/UOM implementation,
  or any other Master Data implementation;
- Inventory posting, stock ledger, reservation, receipt, valuation, batch,
  lot, serial, or expiry mechanics;
- Procurement request, quote, order, receipt, invoice, supplier, or payment
  process ownership;
- B2B Sales quotation, order, fulfillment, invoice, receipt, return, or credit
  process ownership;
- Finance posting, fiscal period, fiscal year, year-end, subledger, GL, tax,
  AR/AP, cash, Payment Term, aging, rate, rounding, or posting-dimension
  policy;
- Reporting Currency, exchange-rate source/update/approval, realized or
  unrealized treatment, or rounding treatment;
- Saudi statutory reports, ZATCA conclusions, tax compliance, banking
  compliance, legal conclusions, or external certification;
- Retail POS, consumer checkout, cashier, restaurant, or Wafra-specific core
  behavior;
- Release 1 consolidation, intercompany, elimination, transfer pricing, or
  consolidated financial statements;
- implementation source code, EF entities, tables, migrations, endpoints,
  screens, providers, databases, infrastructure, deployment, or production
  readiness; and
- closure of MESP-23 open decisions, MESP-48 supported-volume/recovery gates,
  MESP-49 compliance gates, MESP-50 retention/privacy/legal-hold/purge/
  residency/backup gates, MESP-51 migration policy, MESP-53, MESP-54,
  FIN-OD-09 / MESP-110, MESP-37, or Currency.

## 5. Source baseline and traceability

### 5.1 Approved anchors

| Anchor | Baseline carried into this BRD |
|---|---|
| PRD v1.2, RPT-001 | Operational dashboards show current authorized KPIs/exceptions, freshness, and links to filtered source records. |
| PRD v1.2, RPT-002 | Standard reports support filters, sortable columns, bounded exports, asynchronous generation for large datasets, and repeatable parameters. |
| PRD v1.2, RPT-003 | Every dashboard/report exposes a data-as-of timestamp and whether its data is transactional or asynchronously projected. |
| PRD v1.2, BR-012 | Operational and financial reports reconcile to posted transactions and expose freshness. |
| PRD v1.2, section 19.1 | Migration reporting preserves source ownership, extract/cleansing/mapping/reject/sign-off evidence, preview/validation, immutable batch results, dry runs, rollback, and reconciliation. |
| PRD v1.2, section 19.2 | Executive, Procurement, Inventory, Sales, Finance, and SaaS/Admin minimum report catalogue. |
| PRD v1.2, section 19.3 | Authenticated, Tenant-scoped, authorized, validated, versioned, rate-limited, observable, idempotent integration and webhook behavior. |
| PRD v1.2, table 18 | BRD must cover reports/KPIs, filters, frequency, source, reconciliation, roles, permissions, audit, migration, integrations, and acceptance scenarios. Frequency remains a decision where policy is not approved. |
| MESP-23 / MESP-22 | The Product Decision Register and living open-questions register preserve approved decisions and unapproved deferred decisions. |
| MESP-52 / PD-020 | Tenant entitlements and approval controls are part of the platform boundary; Reporting cannot bypass them. |
| MESP-56 / PD-021 | Multiple legal entities remain separate in Release 1; no consolidation or intercompany behavior is implied. |

### 5.2 Supporting approved documents

This baseline was checked against:

- the approved Product Requirements Document, MESP_PRD_v1.2.docx;
- the approved 00 ERP Business Glossary;
- the approved 15 Foundation Release 1 Lean Implementation Specification;
- ADR-002 for the four-project modular-monolith direction and module
  boundaries;
- ADR-004 for server-derived authentication and authorization;
- ADR-006 for shared SQL Server, Tenant ownership, and module-owned
  persistence boundaries;
- ADR-007 and ADR-008 for scoped durable delivery, retry, dead-letter,
  duplicate handling, worker claims, and unknown outcomes;
- ADR-009 for private, Tenant-scoped object-storage evidence;
- ADR-018 and the foundation specification for non-production test/provider
  and MESP-48/MESP-50 gates;
- the approved Procurement and Purchase-to-Pay BRD;
- the approved Inventory and Warehouse Management BRD;
- the approved Finance and Accounting BRD, including MESP-109 reconciliation
  completion;
- the approved B2B Sales and Order-to-Cash BRD;
- the approved Master Data/product-catalogue BRD baseline; and
- the live Jira issues MESP-23, MESP-53, MESP-54, MESP-110, MESP-37, and the
  completed upstream BRD issues.

## 6. Actors, responsibilities, and ownership boundaries

### 6.1 Actors

| Actor | Reporting responsibility or use |
|---|---|
| Tenant user | Views dashboards/reports only within server-derived Tenant and organization scope; may request an authorized report or export if that permission is granted. |
| Executive or operational consumer | Uses approved figures and follows freshness, status, scope, and lineage indicators; does not treat a report as a posting authority. |
| Procurement role | Explains purchasing source meaning and exceptions; does not edit a Reporting result to repair a purchase record. |
| Inventory role | Explains stock ledger, movement, balance, count, tracking, and valuation source meaning; does not edit a Reporting result to repair stock. |
| B2B Sales role | Explains commercial order, fulfillment, invoice-request, return, and receipt source meaning; does not edit a Reporting result to repair a sale. |
| Finance role | Owns accounting source truth, posted status, periods, tax, currency/rate facts, subledger/GL reconciliation, and financial correction; Reporting does not replace this ownership. |
| SaaS administrator | Manages approved Tenant configuration and platform operations within granted scope; does not gain business-data access through a Reporting path by default. |
| Auditor or authorized reviewer | Reads report and evidence history within authorized scope and may investigate a difference; cannot alter source or evidence. |
| Support operator | Uses an exact, time-bounded, audited support grant when approved; cannot use a global report or client-supplied scope to cross Tenant boundaries. |
| Report-definition approver | Participates in the later approval process required by MESP-53 and relevant domain governance; no final person or approval catalogue is named here. |
| Reconciliation owner | Must be recorded for an approved report/reconciliation path after MESP-53 assigns the accountable business owner; this BRD deliberately does not name that owner. |
| Migration operator | Provides cutover batch, source, rejection, validation, and sign-off evidence under MESP-51; does not use Reporting to hide rejected or unposted opening data. |
| Integration worker | Delivers authorized source facts or report jobs with the initiating Tenant/scope context, correlation, idempotency, retry, and unknown-outcome handling. |
| External validator | Validates tax, Saudi, legal, banking, privacy, security, or other specialist requirements where a gate calls for it. |

### 6.2 Boundary matrix

| Concern | Authoritative owner | Reporting behavior |
|---|---|---|
| Tenant, Company, Branch, Warehouse, membership, entitlement, and session scope | Platform/foundation and owning domain controls | Consume server-derived authority; never trust a report filter, URL, export parameter, or client role as authority. |
| Product, Category, UOM, Supplier, Customer, price, tax, currency master facts | Owning Master Data or Finance domain as applicable | Read the approved source version and expose source identity/effective date; do not invent missing master facts. |
| Requests, quotes, purchase orders, confirmations, delivery and procurement exceptions | Procurement | Present source status and chain links; do not post receipt, invoice, payment, or stock. |
| Receipts, stock ledger, movements, balances, counts, tracking, and valuation evidence | Inventory, with Finance ownership of accounting policy | Present quantity/value source facts and differences; do not create movement, reservation, count, adjustment, or valuation entry. |
| Quotes, orders, fulfillment, invoices/returns requests, receipts, commercial status | B2B Sales, Inventory, and Finance at their approved boundaries | Present the chain and status; do not confirm, deliver, invoice, allocate, return, or credit. |
| Subledgers, GL, AP, AR, cash, tax, periods, rates, posting, reversals, reconciliation | Finance | Read posted Finance truth and source-to-GL links; never post, reverse, allocate, revalue, or select accounting policy. |
| Access, support, entitlement, job, integration, and audit events | Platform/SaaS Administration and audit source | Present authorized activity/failure evidence; do not grant access or rewrite audit history. |
| Report definition, publication metadata, lineage, freshness display, and result evidence | Reporting | Own the read-only publication boundary, subject to MESP-53 and the unresolved decisions. |

Source ownership in this table is not the final named business-owner or
reconciliation-owner decision. MESP-53 remains the critical dependency that
must assign and approve those fields before a final catalogue or implementation
scope is declared.

## 7. Triggers and preconditions

### 7.1 Triggers

Reporting can be invoked by:

- an authorized user opening an approved dashboard;
- an authorized user requesting an approved standard report with filters;
- an authorized user requesting a bounded export, when export permission is
  separately granted;
- a permitted internal process publishing a new report-definition version;
- a permitted source or projection update that makes a report result stale or
  eligible for refresh;
- a reconciliation review that requests a repeatable result and evidence
  snapshot;
- a migration validation or cutover rehearsal under MESP-51;
- an integration delivery or retry that produces a report-job outcome; or
- an authorized auditor/support investigation with exact scope and grant.

This list is not a scheduler approval. It describes possible business triggers;
MESP-53 must decide whether scheduled reports, automatic delivery, saved views,
or recurring exports exist in Release 1.

### 7.2 Preconditions

Before a report or dashboard is shown as a valid result:

1. The requester has an authenticated session or an approved, auditable
   support context.
2. The server derives exactly one effective Tenant and permitted
   Company/Branch/Warehouse scope.
3. The report definition is approved for the relevant baseline, versioned, and
   effective for the requested period.
4. Required entitlements, permissions, source access, and export permission
   are present.
5. Required source systems or snapshots identify their status, time basis,
   currency facts, and data-as-of point.
6. Required source lineage and reconciliation path are present. A named
   reconciliation owner is a required unresolved field until MESP-53 assigns
   one.
7. Any conditional dependency is approved or the result clearly displays that
   the branch is unavailable, pending, or not applicable.
8. The requested scope and filters do not cross a Company or other boundary
   without an explicit approved aggregation rule. Release 1 financial
   consolidation is not such an aggregation rule.
9. The report can be generated without changing a source record.

If a precondition fails, the result must be denied, partial, stale, blocked,
or unknown with an explainable reason. It must not silently substitute a
different Tenant, time basis, currency, source status, formula, or owner.

## 8. Main process

The following is the business-level read and publication flow. It is not an
API, UI, database, or implementation sequence.

1. **Establish authority.** The system identifies the authenticated user,
   Tenant, membership/support grant, permitted Company/Branch/Warehouse scope,
   entitlements, permissions, language, and audit context from the server.
2. **Select an approved definition.** The requester selects an approved
   dashboard/report and parameter set. The definition records its version,
   effective dates, source set, permitted filters, measure definitions,
   freshness semantics, and reconciliation path.
3. **Validate parameters.** The system validates the period or event-time
   boundary, organizational scope, source status options, language, and
   currency presentation facts. Client-supplied scope can narrow a permitted
   scope but cannot widen or replace it.
4. **Resolve source facts.** Reporting reads the owning source or an approved
   projection/snapshot. It records source identity, source version or
   snapshot, source status, data-as-of, generated-at, and whether the result is
   transactional or projected.
5. **Apply the approved definition.** The system includes only records and
   measures allowed by the definition. It keeps posted, pending, unknown,
   rejected, and unavailable facts distinct. It does not choose a formula,
   exchange-rate source, rounding rule, Payment Term rule, fiscal rule, tax
   conclusion, or posting dimension that is not approved.
6. **Check lineage and reconciliation.** Each posted financial or stock fact
   points to its source chain and reconciliation evidence. Missing, stale,
   mismatched, or unresolved evidence is shown as a state or exception, not
   silently cleared.
7. **Present the result.** The dashboard/report shows scope, filters,
   time/currency basis, data-as-of, freshness, source status, definition
   version, exceptions, and authorized drill-through links.
8. **Handle export if permitted.** A separately authorized user may request a
   bounded export. The output keeps the same scope, filters, lineage,
   freshness, privacy, and audit evidence. Large output may be asynchronous.
   No default frequency, recipient, schedule, or delivery channel is chosen.
9. **Record evidence.** The report access, generation result, source snapshot
   references, definition version, exceptions, export artifact, and
   reconciliation review are retained subject to MESP-50.
10. **Correct at the source.** When a difference is found, the responsible
    source domain corrects it through its approved process, such as a
    reversal, return, credit/debit correction, stock adjustment, or source-data
    correction. Reporting refreshes or reruns after the source change; it does
    not mutate the source or erase the earlier result.

## 9. Alternative and exception paths

### 9.1 Valid transactional result

When all required sources are available and current for the requested
definition, the result is shown with a transactional data-as-of point and
links to authorized source records. “Current” is a reported state; a numeric
freshness SLA is not invented here.

### 9.2 Valid projected result

When a report uses an asynchronously projected source, it must say so, show
the projection data-as-of and generation time, and identify the source
projection/version. A projection must not be presented as an immutable source
ledger or posted GL record.

### 9.3 Partial result

If an allowed source or partition is unavailable while the remaining data can
be safely identified, the result is labeled Partial and identifies included,
excluded, and unavailable sources or scopes. Totals and KPIs must not imply
complete coverage.

### 9.4 Stale result

If the data-as-of point falls outside the approved freshness condition for the
definition, the result is labeled Stale. The report may remain viewable if the
definition permits it, but it cannot be presented as current. A user cannot
override Stale by changing a client field.

### 9.5 Unknown result

If the system cannot prove whether a source delivery, job, or reconciliation
effect was applied, the result is labeled Unknown or unavailable for the
affected scope. The unknown state is retained for scoped reconciliation. A
retry must not duplicate a delivered source fact or claim success without
evidence.

### 9.6 Pending or unposted source fact

Operational in-flight records may be shown as Pending when the approved report
includes them. Financial reports that represent posted accounting truth must
exclude or separately label pending/unposted facts. Reporting must not promote
a pending fact to posted status.

### 9.7 Denied or redacted drill-through

When the aggregate result is authorized but a source record is not, the report
shows a permitted aggregate or a neutral unavailable/redacted outcome. It
does not expose identifiers, amounts, names, attachments, or error details
that reveal the denied record.

### 9.8 Conditional dependency unavailable

If an approved report depends on tracking/expiry, Payment Term/aging,
Reporting Currency, exchange rates, tax/localization validation, or another
open decision, the result must state the blocked or conditional branch. It
must not select a default policy to produce a more convenient number.

### 9.9 Concurrent definition or source change

If a definition or source version changes while generation is in progress, the
result uses one captured version or fails with a retryable/concurrency
outcome. It must never combine incompatible definition versions without
identifying the boundary.

### 9.10 Correction, reversal, or return

A report rerun after an approved source correction links the corrected result
to the original evidence and source correction. Prior generated evidence is
immutable and remains discoverable according to retention policy. A report
does not rewrite history to make the original result appear never to have
existed.

## 10. Business rules

| ID | Rule |
|---|---|
| RPT-BR-001 | Every Tenant-owned report, dashboard, export, result, job, and reconciliation evidence item is scoped to exactly one owning Tenant. |
| RPT-BR-002 | Company, Branch, and Warehouse scope is derived by the server from current authority and may only be narrowed by a permitted filter. |
| RPT-BR-003 | A client-supplied TenantId, CompanyId, BranchId, WarehouseId, role, permission, or support flag never creates authority. |
| RPT-BR-004 | A report never writes, posts, reserves, receives, delivers, invoices, pays, allocates, adjusts, revalues, reverses, or deletes source business data. |
| RPT-BR-005 | Every result exposes its report-definition version, effective dates, parameters, scope, source set, data-as-of, generated-at, and freshness state. |
| RPT-BR-006 | Transactional and asynchronously projected data are visibly distinguished; a projection cannot be described as the source ledger or posted GL. |
| RPT-BR-007 | Posted, pending, unknown, rejected, partial, stale, and unavailable records are not silently combined. |
| RPT-BR-008 | Financial reports use Finance-owned posted truth for accounting figures and preserve source-to-subledger-to-GL lineage where applicable. |
| RPT-BR-009 | Inventory quantity and valuation reporting uses Inventory-owned ledger/balance/evidence and preserves the distinction between immutable movement history and a projected balance. |
| RPT-BR-010 | A report that presents a posted financial or stock quantity/value fact must have a documented reconciliation path and a reconciliation-owner field; MESP-53 must assign the named owner before final release approval. |
| RPT-BR-011 | Reporting may describe source-domain responsibility for semantics, but it does not silently assign the final named report owner or reconciliation owner. |
| RPT-BR-012 | A KPI is publishable only when its definition, source semantics, inclusion/exclusion rules, unit, time basis, and effective version are approved; this BRD does not approve final formulas. |
| RPT-BR-013 | A missing or unapproved formula is a visible pending-definition state, not a zero, estimate, or implicit default. |
| RPT-BR-014 | No report selects an exchange-rate source, rate date, realized/unrealized treatment, Reporting Currency policy, or rounding rule without MESP-54 and Finance approval. |
| RPT-BR-015 | No report defines Payment Term shape, due-date, aging bucket, settlement, historical preservation, fiscal-year, or year-end mechanics without FIN-OD-09 / MESP-110 approval. |
| RPT-BR-016 | No report invents Finance posting dimensions or Cost Center behavior. |
| RPT-BR-017 | Multiple Companies remain separate Release 1 legal/accounting boundaries; no report creates consolidation, intercompany, elimination, transfer pricing, or consolidated statements. |
| RPT-BR-018 | Report filters and drill-through preserve the same authorized scope; a drill link cannot widen access. |
| RPT-BR-019 | An export is subject to separate permission, bounded scope, private artifact handling, retention, audit, and privacy controls. |
| RPT-BR-020 | Scheduled reports, automatic distribution, recipients, frequency, and saved-view behavior remain conditional on MESP-53 and do not arise from the existence of an export requirement. |
| RPT-BR-021 | A report may show an exception or difference, but it cannot resolve the exception by mutating a source or reconciliation record. |
| RPT-BR-022 | Corrections are made in the owning source domain through its approved correction/reversal process; Reporting preserves links between prior and refreshed evidence. |
| RPT-BR-023 | Duplicate delivery or duplicate generation is idempotent and cannot create duplicate source facts or duplicate financial/stock effects. |
| RPT-BR-024 | Retryable, terminal, dead-letter, and unknown outcomes are visible and auditable; unknown outcomes require a scoped reconciliation path. |
| RPT-BR-025 | A report cannot conceal a missing source, rejected migration row, stale projection, denied drill-through, or delayed integration behind a complete-looking total. |
| RPT-BR-026 | Report evidence is append-only from the business perspective; supersession or correction adds linked evidence rather than overwriting the earlier fact. |
| RPT-BR-027 | A report must retain enough source, definition, scope, and time/currency context for an authorized reviewer to reproduce or explain the displayed result, subject to privacy and retention gates. |
| RPT-BR-028 | Arabic/English and date/time presentation may vary by authorized preference, but presentation translation cannot change a source amount, status, scope, or business meaning. |
| RPT-BR-029 | Saudi statutory, tax, banking, privacy, and regulatory conclusions require the named external and MESP gates; a standard report label is not a compliance conclusion. |
| RPT-BR-030 | Retail POS and Wafra-specific core behavior are excluded; generic B2B ERP reporting must not acquire retail vocabulary or behavior by example. |
| RPT-BR-031 | A report result must identify whether a figure is source-provided, derived from an approved definition, or unavailable because a required decision is open. |
| RPT-BR-032 | A user can request a repeatable parameter set only within the approved definition; repeatability does not create a saved-view or schedule policy. |
| RPT-BR-033 | Background report work keeps the initiating Tenant and permitted scope, revalidates authority before effect, and cannot process a global cross-Tenant scan. |
| RPT-BR-034 | Report and export failure must fail closed for authorization and must not make a source or financial/stock effect appear successful. |

## 11. Report definition and result lifecycle

### 11.1 Report-definition lifecycle

The business lifecycle for a report definition is:

1. **Proposed** — a candidate exists for review; it is not publishable.
2. **Semantically specified** — purpose, source meaning, scope, measures,
   filters, freshness, lineage, and reconciliation fields are documented.
3. **Pending decision** — an open dependency such as MESP-53, MESP-54,
   MESP-110, MESP-37, MESP-49, MESP-50, or MESP-51 remains unresolved.
4. **Approved for publication** — the relevant Owner decision and any external
   gate are evidenced. This status is not granted to a report merely because
   this BRD lists it as a minimum baseline.
5. **Active** — the approved definition can be used for its effective period.
6. **Superseded** — a later approved version replaces it for a later effective
   period; earlier evidence remains linked and immutable.
7. **Retired** — no new result is generated, while retained evidence remains
   accessible according to the applicable retention decision.

This generic lifecycle does not choose the MESP-53 approval workflow, named
approvers, frequency, or distribution policy.

### 11.2 Report-result lifecycle

An individual report or dashboard result can be:

- **Requested** — authorized request accepted for evaluation;
- **Validating** — authority, parameters, source availability, and definition
  are being checked;
- **Generating** — source facts are being read or an approved projection is
  being assembled;
- **Published** — result is available with complete metadata;
- **Partial** — result is available with identified incomplete coverage;
- **Stale** — result is available but outside the approved freshness condition;
- **Pending** — included source facts are not yet posted or a conditional
  dependency is awaiting decision;
- **Unknown** — delivery, generation, or reconciliation outcome is not proven;
- **Failed** — no valid result was produced and a reason is recorded;
- **Superseded** — a later result or definition version is preferred; and
- **Archived** — retained evidence is no longer a current view.

Only an approved definition may produce a Published result. A Partial, Stale,
Pending, or Unknown state is not a silent success.

### 11.3 Required status transitions

| Current state | Event | Next state | Control |
|---|---|---|---|
| Proposed | Semantic review begins | Semantically specified or Pending decision | No publication. |
| Pending decision | Required Owner/external decision recorded | Approved for publication or remains blocked | Decision evidence must name the scope and effective date. |
| Approved for publication | Effective date reached | Active | No backdating without an approved correction. |
| Active | Authorized request | Requested | Scope and permission are revalidated. |
| Requested | Validation passes | Validating / Generating | One definition and source boundary are captured. |
| Generating | Complete and reconciled | Published | Data-as-of, freshness, lineage, and reconciliation evidence required. |
| Generating | Incomplete but bounded | Partial, Stale, Pending, Unknown, or Failed | The affected scope and reason are visible. |
| Active | Approved replacement | Superseded | Existing evidence remains immutable. |
| Any result | Source correction or re-run | Linked new result | Prior evidence is retained; no silent overwrite. |

## 12. Data requirements

### 12.1 Report-definition data

Every approved definition must specify, at business level:

- stable report identifier and human-readable name;
- purpose, audience, domain boundary, and non-purpose;
- required Tenant/Company/Branch/Warehouse scope;
- permitted filters and their semantics;
- source systems, source fields/facts, source status, and lineage path;
- measure or KPI identifier, display label, unit, and approved definition
  version;
- inclusion, exclusion, null, unknown, rejected, and duplicate treatment;
- time basis, timezone, period boundary, and event-date semantics;
- transaction, base, and any permitted presentation-currency facts;
- freshness definition, data-as-of rule, generated-at rule, and stale behavior;
- reconciliation path, comparison facts, difference representation,
  reconciliation status, and the MESP-53 owner field;
- permissions, drill-through, export, redaction, and audit requirements;
- effective-from and effective-to dates;
- conditional dependency and blocking decision, if any; and
- retention, privacy, legal-hold, residency, and backup classification gate.

The final report catalogue and named owners are not approved by this list.

### 12.2 Report-result data

Each generated result must carry or link to:

- Tenant and authorized organizational scope;
- report identifier, definition version, and effective dates;
- request identity, authorization context, and correlation identifier;
- exact parameters, filters, language, timezone, and time boundary;
- source identifiers, source versions or snapshots, source status, and
  data-as-of point;
- generated-at timestamp and transactional/projected indicator;
- freshness state and any partial/stale/pending/unknown explanation;
- measure identifiers and calculation-definition versions;
- row or aggregate lineage to authorized source records or source groups;
- currency code and whether the value is transaction/base/presentation fact;
- precision/rounding metadata as provided by the source or approved Finance
  policy, without Reporting selecting a policy;
- reconciliation comparison reference, difference, status, and unresolved
  owner field;
- export/job artifact reference if an export was authorized;
- access, error, retry, dead-letter, and correction links; and
- retention/privacy classification and audit evidence reference.

### 12.3 Common semantic statuses

| Status | Required meaning |
|---|---|
| Posted | The owning source has recorded the fact as posted under its approved process. Reporting does not make a fact Posted. |
| Pending | The source process has not reached the relevant posted/complete state. |
| Projected | The value is from an approved asynchronous projection, not directly from the source record/ledger at read time. |
| Unknown | The system cannot prove the source or delivery state. |
| Rejected | The source or migration process rejected the row/event; the reason and batch are traceable. |
| Stale | The data-as-of point no longer satisfies the approved definition's freshness condition. |
| Partial | Only an identified subset of the requested scope or sources was available. |
| Denied/unavailable | The user cannot access the source or the source is unavailable; no sensitive reason is leaked. |

### 12.4 Time and period facts

Reporting must distinguish, when applicable:

- source event time;
- document date;
- posting date;
- settlement/receipt date as supplied by Finance;
- data-as-of time;
- generation time;
- projection time;
- timezone and local display date; and
- requested period boundary.

Reporting does not define fiscal-year start, fiscal period close, year-end
rollover, Payment Term due date, aging interval, or historical settlement
mechanics. Those remain FIN-OD-09 / MESP-110 gates.

### 12.5 Currency facts

The result must identify the currency fact it presents:

- Transaction Currency from the source document, when applicable;
- Company Base Currency from the owning Company, when applicable;
- Reporting Currency only if the MESP-54 decision and Finance source facts
  authorize it; and
- source-provided rate/date/precision/rounding metadata when applicable.

Reporting must not choose an exchange-rate source, update/approval workflow,
rate date, realized or unrealized treatment, Reporting Currency policy, or
rounding rule. A result with a missing or unapproved required currency fact is
Pending, Unavailable, or explicitly limited; it is not silently converted.

## 13. Source lineage and semantic ownership

### 13.1 Lineage requirement

A report must allow an authorized reviewer to follow a figure or row through
the report definition, source snapshot/version, source domain record or
approved aggregate, and applicable reconciliation evidence. The path can be
  an aggregate lineage group when row-level exposure is not authorized, but it
must remain explainable without disclosing protected data.

Lineage records are references and evidence. They do not copy ownership of the
source fact to Reporting and do not permit the report to edit the source.

### 13.2 Source matrix

| Source family | Authoritative meaning | Reporting lineage and display | Required boundary |
|---|---|---|---|
| Master Data | Product, Category, UOM, Supplier, Customer, price, tax, and other approved master facts | Source identity, status, effective date, and source version; missing master data is visible | No Product/Item/SKU/Barcode or master-data implementation in this session. |
| Procurement | Requests, quotations, orders, confirmations, delivery commitments, receipts/invoice/payment references, and exceptions within Procurement's boundary | Chain links and status; commitment, receipt, match, and supplier measures use source semantics | Inventory owns physical receipt/stock; Finance owns invoice/AP/payment/currency/posting. |
| Inventory | Immutable stock movements/ledger, projected balances, availability, counts, tracking, and valuation evidence | Quantity/value lineage to ledger or approved projection; ledger/projection distinction and freshness | No movement, reservation, count, adjustment, tracking, expiry, or valuation mutation. |
| B2B Sales | Quotes, orders, fulfillment links, invoices/returns/receipts references, commercial status, and exceptions | Commercial chain and authorized drill-through | Inventory owns physical fulfillment; Finance owns AR/revenue/tax/receipt/posting. |
| Finance | Subledgers, GL, AP, AR, cash, tax, posted status, periods, currency/rate facts, reversals, and reconciliation | Posted accounting truth and source-to-GL/subledger lineage | No posting, allocation, reversal, tax conclusion, rate selection, or dimensions. |
| SaaS/Admin/Audit | Tenant status, entitlement usage, privileged access, platform/audit events, jobs, integrations, and support evidence | Platform-event lineage with access/audit boundaries | No privilege grant, audit rewrite, or global business-data path. |
| Migration/Integration | Source system, owner, extract, batch, mapping, reject, delivery, retry, and sign-off evidence | Cutover and delivery lineage, including rejected/unknown outcomes | MESP-51, ADR-007, ADR-008, MESP-48, and MESP-50 gates remain open where applicable. |

### 13.3 Cross-domain chain rules

The following paths are read-only traceability templates:

- procurement commitment → receipt evidence → invoice/AP reference →
  payment/reference → return or exception;
- inventory posted movement → immutable stock ledger → projected balance →
  count/valuation comparison;
- sales order → fulfillment/delivery reference → invoice/AR reference →
  receipt/allocation reference → return/credit reference;
- Finance subledger → posted GL entry → period and reconciliation evidence;
  and
- platform/admin action → job/integration event → audit/support evidence.

The templates do not assert that every chain is available, that a downstream
record is posted, or that a named owner has been approved. Missing or delayed
links are reported as such.

## 14. Freshness, availability, and reconciliation requirements

### 14.1 Freshness

Every report and dashboard must expose:

- source data-as-of timestamp or bounded time point;
- report generated-at timestamp;
- source or projection identifier/version;
- transactional or asynchronously projected classification;
- freshness state and definition of the state;
- affected source/domain when freshness is partial;
- last successful/known source or projection point where available; and
- whether the displayed result may be used for an operational decision,
  reconciliation review, or only historical reference.

This BRD does not set a universal numeric freshness SLA. MESP-53 and later
operational/volume decisions must set any report-specific threshold.

### 14.2 Reconciliation minimum

Every report that presents a posted financial or stock quantity/value fact must
carry a documented reconciliation path containing:

1. the independent records being compared;
2. the common scope, period, Company, currency basis, and data-as-of point;
3. the source snapshots or versions;
4. the comparison measure and difference representation;
5. treatment of missing, pending, rejected, stale, and unknown records;
6. reconciliation status, such as Not Started, In Review, Matched,
   Difference, Blocked, or Unknown;
7. a link to the source correction/reversal or investigation evidence; and
8. a ReconciliationOwner field that remains unresolved until MESP-53 assigns
   and approves the named business owner.

The status labels above describe evidence states; they do not assign a person,
approval authority, or final reconciliation policy.

### 14.3 Reconciliation path templates

| Report family | Independent records / path | Required difference visibility | Ownership gate |
|---|---|---|---|
| Procurement | Purchase commitments, receipts, invoices/AP references, payments, and returns/exceptions | Missing link, quantity/value mismatch, duplicate, delayed, or unposted state | Procurement/Inventory/Finance semantic boundaries remain; named reconciliation owner is MESP-53. |
| Inventory | Immutable stock ledger/movements versus projected balance, count evidence, and valuation comparison | Quantity, movement, count, valuation, source-status, and freshness difference | Inventory owns stock evidence; Finance owns valuation-accounting policy; named owner is MESP-53. |
| B2B Sales | Orders, fulfillment/delivery, invoices/AR, receipts/allocation, returns/credits | Chain omission, status mismatch, quantity/value mismatch, or unposted state | Sales/Inventory/Finance boundaries remain; named owner is MESP-53. |
| Finance | Subledger balances and source documents versus posted GL, cash/bank, tax, and period evidence as applicable | Difference, period mismatch, pending posting, unknown, or unavailable source | Finance owns accounting truth; final named reconciliation ownership remains MESP-53. |
| SaaS/Admin | Platform events, job/integration outcomes, support records, and audit evidence | Missing, duplicate, retry, dead-letter, stale, or unknown event | Platform/audit ownership remains; final report owner and retention gate remain open. |

Reporting may present a reconciliation difference and open an evidence path.
It must not approve an accounting or stock correction, change a source
balance, clear a difference, or claim a matched result without the required
independent evidence.

## 15. Report and KPI baseline

### 15.1 PRD minimum catalogue

The following is the approved PRD minimum reporting baseline. It is not the
final Release 1 catalogue. MESP-53 must confirm the final report set, each
figure's definition, named business owner, reconciliation accountability, and
any configurable, saved-view, scheduled, or distribution behavior.

| Family | PRD minimum baseline | Source/drill-through expectation | Decision and conditional notes |
|---|---|---|---|
| Executive | Sales, gross margin, purchases, stock value, cash, receivables/payables, and exceptions | Drill to authorized transactions and freshness | Gross margin, cash, AR/AP, stock value, currency, period, and aggregation semantics require source/domain decisions; no consolidated financial statement is implied. |
| Procurement | Open orders, overdue delivery, receipts, match exceptions, supplier spend/performance | Order, receipt, invoice, and payment chain where available | “Overdue,” spend, performance, and payment links use approved Procurement/Finance semantics; no Payment Term or aging default. |
| Inventory | Stock balance/ledger, availability, aging/expiry, valuation, movement, and count variance | Balance/projection to immutable movements | Tracking/expiry and aging depend on MESP-41; valuation/accounting semantics remain Inventory/Finance-owned. |
| Sales | Quotes/orders, fulfillment, invoices, returns, receipts, credit exposure, and product/customer sales | Order-delivery-invoice-receipt chain | Credit exposure and receipt/settlement semantics remain Finance/Sales-owned; no Payment Term or aging mechanics. |
| Finance | Trial balance, GL, P&L, balance sheet, cash movement, AP/AR aging, tax summary, and bank reconciliation | Subledgers/source documents to GL | Posted Finance truth only; aging/period/year-end/dimensions require MESP-110; tax/bank/Saudi branches require relevant gates. |
| SaaS/Admin | Tenant status, entitlement usage, privileged access, audit activity, and integration failures | Platform events, jobs, and support records | Support scope, privacy, retention, and scheduled delivery remain gated by platform decisions and MESP-50. |

The table establishes coverage to validate against the PRD. It does not
approve a user-facing report name, formula, frequency, owner, schedule, or
distribution rule. A later owner decision may narrow or configure it only with
traceable superseding evidence.

### 15.2 KPI definition contract

Before any KPI is approved for publication, its definition must record:

- stable KPI identifier and display name;
- business purpose and decision use;
- source facts and semantic owner;
- included and excluded statuses;
- numerator/denominator or other calculation description, if applicable;
- unit, quantity/value basis, and permitted aggregation;
- time/event/period basis and timezone;
- Company/Branch/Warehouse and Tenant scope;
- transaction/base/presentation currency context;
- data-as-of and freshness requirement;
- null, zero, negative, unknown, duplicate, and rejected treatment;
- version, effective-from, effective-to, and supersession link;
- source lineage and reconciliation path;
- rounding/precision metadata without inventing the policy; and
- named report/reconciliation ownership once MESP-53 approves it.

This contract deliberately does not supply final formulas for sales, gross
margin, purchases, stock value, cash, receivables, payables, availability,
aging, performance, credit exposure, tax, or any other KPI. An undefined
formula is not a zero and cannot be inferred from a display label.

### 15.3 Filters and drill-through

An approved report may define filters for:

- authorized organizational scope;
- date/event/posting/data-as-of interval;
- source status, when the report definition explicitly includes it;
- source document, product, customer, supplier, location, or category
  identifiers within authorized scope;
- language and presentation preferences; and
- approved currency facts.

Filter semantics must be repeatable and recorded in the result. A filter can
narrow access but cannot widen it, cross Tenant or unauthorized Company scope,
or turn a pending/unknown record into a posted one.

Drill-through must preserve the report scope and show the source-domain status.
If a source record is not authorized, the report returns a permitted
aggregate or neutral unavailable/redacted state rather than a revealing
authorization error.

### 15.4 Export and scheduled branch

RPT-002 requires bounded exports and asynchronous generation for large
datasets. That requirement does not approve scheduled distribution.

If MESP-53 later approves a scheduled/export branch, the later definition must
also specify, before implementation:

- who may create, run, cancel, or receive an export;
- permitted scope, parameter reuse, and data-as-of behavior;
- frequency, recurrence, time zone, and failure notification;
- recipient and delivery-channel controls;
- private artifact access, expiry, retention, legal hold, and deletion;
- size/rate limits, replay, duplicate, retry, and dead-letter behavior; and
- audit, privacy, support, and reconciliation evidence.

This BRD chooses none of those policy values.

## 16. Validation rules

| ID | Validation |
|---|---|
| RPT-VR-001 | The server resolves one effective Tenant and rejects cross-Tenant source access, mixed-Tenant aggregation, and stale support context. |
| RPT-VR-002 | Company/Branch/Warehouse scope is checked against current membership, grant, entitlement, and source ownership before data is read. |
| RPT-VR-003 | Client-supplied scope, role, permission, language, currency, or status cannot create authority or alter source semantics. |
| RPT-VR-004 | The requested report identifier and definition version must be approved/effective or return Pending decision/Unavailable. |
| RPT-VR-005 | Parameters must be valid for the definition; invalid dates, periods, identifiers, or incompatible filters are rejected without source mutation. |
| RPT-VR-006 | Source facts must identify source domain, record/snapshot/version, status, data-as-of, and time basis. |
| RPT-VR-007 | Transactional and projected facts cannot be silently merged without a definition that names the combination and labels both sources. |
| RPT-VR-008 | Posted financial measures must come from Finance-owned posted truth; pending/unposted facts must be excluded or separately labeled. |
| RPT-VR-009 | Inventory quantity/value measures must retain ledger/projection and tracking/expiry dependency facts where applicable. |
| RPT-VR-010 | Every posted financial or stock measure must resolve to a reconciliation path; an unresolved MESP-53 owner blocks final approval, not evidence capture. |
| RPT-VR-011 | A measure with no approved formula/version is rejected or displayed as Pending definition; it is not calculated by convention. |
| RPT-VR-012 | Missing, null, unknown, rejected, duplicate, partial, delayed, stale, denied, and unavailable source states must follow the definition and remain visible. |
| RPT-VR-013 | Currency presentation must identify transaction/base/reporting facts and source-provided rate metadata; Reporting cannot choose rate or rounding policy. |
| RPT-VR-014 | Financial time filters must preserve event/document/posting/data-as-of distinctions; fiscal and year-end behavior remains MESP-110-gated. |
| RPT-VR-015 | Payment Term, due-date, aging, settlement, and historical preservation logic cannot be inferred from a report label. |
| RPT-VR-016 | A definition cannot introduce Finance dimensions, Cost Center, tax compliance, statutory reporting, or banking conclusions without the owning gate. |
| RPT-VR-017 | Drill-through results are checked independently and cannot leak denied record content through counts, errors, filenames, or attachments. |
| RPT-VR-018 | Exports enforce separate permission, bounded scope, artifact privacy, retention, and audit checks. |
| RPT-VR-019 | Generation is idempotent for a stable request/source/definition boundary and does not duplicate source or accounting effects. |
| RPT-VR-020 | Retryable, terminal, dead-letter, and unknown outcomes are recorded with correlation and reconciliation evidence. |
| RPT-VR-021 | A source correction/reversal causes a linked new result or evidence update; prior evidence is not silently overwritten. |
| RPT-VR-022 | Migration batches, rejected rows, opening balances, and dry-run results remain distinguishable from live posted transactions. |
| RPT-VR-023 | Arabic/English display and date/time conversion do not alter numeric/source semantics or authority. |
| RPT-VR-024 | Report generation or export fails closed when required authorization, source, retention, privacy, or decision gates cannot be verified. |

## 17. Permissions, approval controls, and separation of duties

### 17.1 Permission model

Reporting requires separate business permissions for the relevant action,
subject to platform authorization:

- view an approved dashboard or report;
- drill through to authorized source records;
- view reconciliation evidence;
- request or download an export;
- view sensitive fields or attachments, where allowed;
- propose or maintain a report definition;
- approve a report definition or KPI; and
- review audit and integration evidence.

The final permission catalogue and role mapping are not invented here. The
server must evaluate the effective permission, Tenant, Company/Branch/
Warehouse scope, entitlement, and any exact support grant at the time of
access and again before an asynchronous effect or download.

### 17.2 Approval controls

The following approvals are gates, not assumptions:

- MESP-53 approval of the final catalogue, figure definitions, named business
  owners, reconciliation accountability, and any scheduled/export branch;
- relevant domain approval of source semantics and measure definitions;
- MESP-54 Finance approval for Reporting Currency, rate, and rounding policy;
- FIN-OD-09 / MESP-110 Finance approval for fiscal/year-end, Payment Term,
  aging, settlement history, and posting dimensions;
- MESP-41 approval for batch/lot/serial/expiry and any inventory aging/expiry
  report branch;
- MESP-37 and any MESP-49/external validation for Saudi/localization/tax/
  statutory branches;
- MESP-50 approval for retention, privacy, legal hold, purge, residency,
  attachment, backup, and restoration behavior; and
- MESP-48 approval for supported volumes, async thresholds, recovery, and
  operational availability.

No report may treat a decision's Jira creation, recommendation, or draft as
approval.

### 17.3 Separation of duties

At minimum, later implementation must preserve these separations:

- a report consumer cannot widen their own scope;
- a report definition author cannot implicitly approve the final definition
  where MESP-53 requires a separate Owner decision;
- a report author or exporter cannot post, correct, or reverse source data;
- source posting roles remain separate from reconciliation review;
- Finance posting/reversal/allocation remains separate from Reporting
  generation;
- Inventory stock movement/count/valuation actions remain separate from
  Reporting;
- support access is separate, time-bounded, exact, and audited;
- export/download access is separate from source mutation; and
- a background worker cannot use a global identity or bypass revalidation.

Any future delegation or approval-substitution rule must be explicitly
approved under the owning decision and cannot be inferred from a report role.

## 18. Inventory, accounting, currency, and localization impact

### 18.1 Inventory impact

Reporting consumes the Inventory-owned immutable stock ledger, movement
history, projected balances, availability, count evidence, tracking facts, and
valuation evidence according to the approved Inventory BRD.

The report must distinguish:

- posted movement history from a balance projection;
- available, reserved, held, damaged, or otherwise source-defined statuses;
- physical quantity from valuation/accounting value;
- transaction/base/presentation currency facts;
- count evidence from system balance;
- current facts from stale or delayed projections; and
- batch/lot/serial/expiry facts only when the approved MESP-41 branch exists.

Reporting cannot reserve, release, receive, transfer, count, adjust, revalue,
or otherwise change stock. Finance remains the owner of accounting valuation
policy and its reconciliation to the Inventory ledger.

### 18.2 Accounting impact

Financial reports consume Finance-owned posted subledger and GL truth and
preserve the source-document-to-subledger-to-GL path. Reporting cannot post,
allocate, close a period, reopen a period, reverse, revalue, alter tax, or
change a posting dimension.

Trial balance, GL, P&L, balance sheet, cash movement, AP/AR aging, tax
summary, and bank reconciliation are PRD minimum baseline branches, subject
to Finance source availability and the open decisions. In particular:

- Payment Term, due date, aging buckets, settlement, and historical
  preservation remain FIN-OD-09 / MESP-110;
- fiscal-year, period, and year-end mechanics remain FIN-OD-09 / MESP-110;
- Finance dimensions and Cost Center remain FIN-OD-09 / MESP-110;
- tax and Saudi statutory meaning requires MESP-37/MESP-49 and external
  validation as applicable; and
- MESP-54 remains the gate for Reporting Currency, rates, and rounding.

### 18.3 Multi-currency impact

Reporting preserves the currency and rate facts supplied by the owning source.
It may show a transaction amount in Transaction Currency and a Finance-owned
Company Base Currency amount when those source facts are available. It may
show a Reporting Currency amount only after the MESP-54 policy and source
facts are approved.

Reporting does not create a second book, consolidate Companies, select a rate,
choose an update/approval workflow, derive realized/unrealized treatment,
or choose rounding. If currencies cannot be compared under an approved
definition, the result is clearly limited or unavailable.

### 18.4 Saudi and localization impact

The PRD supports Arabic and English and a Saudi launch context. Reporting
must support language-aware labels, RTL-safe presentation, approved date/time
display, and SAR as the default Saudi Company currency where the source
master/configuration says so. Presentation support is not a statutory or tax
conclusion.

Saudi tax, ZATCA, e-invoicing, banking, statutory report, privacy, legal, or
regulatory behavior must remain conditional on MESP-37, MESP-49, and the
required external validation. This BRD does not name a Saudi statutory report,
assert compliance, or choose a tax formula.

## 19. Audit evidence and correction history

### 19.1 Required evidence

For a report definition, retain or reference:

- definition identifier, version, author, approval evidence, effective dates,
  supersession, and change reason;
- source semantics, lineage, formula/measure definition, and reconciliation
  path;
- decision and external-validation references;
- permission and scope requirements;
- retention/privacy/legal-hold classification; and
- any scheduled/export conditions only after MESP-53 approval.

For a report result or export, retain or reference:

- requester, Tenant, scope, permission decision, and correlation;
- parameters, filters, language, timezone, and definition version;
- source records/snapshots/versions, source status, data-as-of, generated-at,
  freshness, and transactional/projected indicator;
- result status, counts, partial/stale/unknown explanation, and errors;
- reconciliation comparison, difference, status, unresolved owner field, and
  correction/investigation links;
- export artifact identity, access, download, expiry, and failure evidence
  when export is authorized; and
- retention/privacy/residency/backup evidence required by MESP-50.

### 19.2 Immutability and correction

Published evidence is immutable from the business perspective. A source
correction, reversal, return, credit/debit correction, or migration correction
creates linked evidence and a new result or status; it does not erase the
earlier result. A later report may be more current while still preserving the
historical data-as-of point.

Reporting never edits the source record to reconcile a report. The owning
source process must produce the correction, and the reconciliation result must
show the difference before and after the correction where authorized.

### 19.3 Privacy, retention, and private artifacts

Report rows, drill-through details, exports, attachments, and audit evidence
must follow the Tenant, privacy, retention, legal-hold, purge, residency,
backup, and restoration gates in MESP-50 and ADR-009. This BRD requires
private, authorized access and auditability but does not select an object
storage provider or retention duration.

## 20. Integration, migration, and operational requirements

### 20.1 Integration and asynchronous work

Any later Reporting integration or worker must preserve:

- authenticated, Tenant-scoped, authorized, validated, versioned, observable,
  and idempotent delivery;
- correlation identifiers, source/definition version, and causation context;
- duplicate-safe handling and no duplicate source/accounting/stock effect;
- bounded retry with explicit Retryable, Terminal, Dead-letter, or Unknown
  outcomes;
- scoped reconciliation for unknown or dead-lettered deliveries;
- worker revalidation of Tenant, organization scope, entitlement, and
  permission before producing a result or artifact;
- fail-closed access and no global scan;
- private artifact handling and opaque references under ADR-009; and
- no silent loss, silent drop, or false success.

No provider, queue, database, endpoint, or infrastructure design is selected
by this BRD.

### 20.2 Migration and opening-state reporting

Under MESP-51, reporting for migration must retain:

- source system and data owner;
- extraction date/time and source snapshot;
- cleansing, mapping, transformation, and rejected-row reason;
- preview and validation result before commit;
- immutable batch and row outcomes;
- opening master/configuration, inventory quantity/value, customer/supplier
  balances, AR/AP, cash/bank, GL/trial balance, tax, and document-count
  reconciliation where applicable;
- two dry runs, timed rehearsal, rollback/continuity evidence, and sign-off;
- distinction between migrated/opening, posted-live, pending, rejected, and
  unknown records; and
- report definition version and data-as-of for each cutover result.

Reporting does not decide migration mapping, opening balances, posting,
valuation, fiscal period, or cutover approval.

### 20.3 Observability and recovery gates

The future implementation must expose enough evidence to monitor report
generation, freshness, source delivery, partial/stale/unknown outcomes,
reconciliation differences, export failures, retry/dead-letter queues, and
authorization denials. MESP-48 remains the supported-volume, performance,
recovery, and continuity gate. MESP-50 remains the retention, privacy, legal
hold, purge, residency, backup, and restoration gate.

This BRD does not set numeric throughput, report size, retention duration,
recovery objective, provider, production configuration, or deployment rule.

## 21. Given / When / Then acceptance scenarios

These scenarios are business acceptance evidence for the baseline. They do
not authorize implementation and do not close the named open decisions.

### 21.1 Authority and scope

**RPT-GWT-001 — Authorized Tenant dashboard**

**Given** an authenticated user has an approved dashboard permission in Tenant
T and a permitted Company/Branch scope
**When** the user opens an approved dashboard
**Then** the result uses the server-derived scope and shows its definition
version, parameters, source status, data-as-of, generated-at, and freshness.

**RPT-GWT-002 — Cross-Tenant request**

**Given** a user is authorized in Tenant T
**When** the request supplies Tenant U or a source identifier owned by U
**Then** the request is denied without revealing U's data or existence.

**RPT-GWT-003 — Client scope cannot widen access**

**Given** a user is authorized only for Company C1
**When** the user changes a client filter to C2 or removes the Company filter
**Then** the server preserves or narrows the permitted scope and never
includes C2 records.

**RPT-GWT-004 — Branch and Warehouse boundary**

**Given** a user is permitted for one Branch and Warehouse set
**When** the user drills into a stock or sales result
**Then** each drill-through is independently checked against the same
server-derived boundaries.

**RPT-GWT-005 — Exact support grant**

**Given** a support operator has an approved, time-bounded grant for one
Tenant and scope
**When** the operator requests a report
**Then** the result is limited to that grant, revalidated before any async
effect, and fully audited.

**RPT-GWT-006 — Platform path separation**

**Given** a platform administrator has platform-governance permissions
**When** the administrator requests Tenant business reporting without a
Tenant-scoped business grant
**Then** the request is denied and no platform path becomes a business-data
shortcut.

### 21.2 Definition, freshness, and status

**RPT-GWT-007 — Approved definition**

**Given** an approved effective report definition exists
**When** an authorized user requests it with valid parameters
**Then** the result uses exactly that definition version and records its
effective dates and source lineage.

**RPT-GWT-008 — Pending definition**

**Given** a report label exists in the PRD minimum baseline but its formula or
MESP-53 approval is not complete
**When** a user requests it
**Then** the result is Pending definition or Unavailable and does not invent a
formula, zero, owner, or frequency.

**RPT-GWT-009 — Transactional freshness**

**Given** all required source facts are read transactionally
**When** the result is generated
**Then** it identifies the transactional source, data-as-of, generated-at, and
freshness state.

**RPT-GWT-010 — Projected freshness**

**Given** a report reads an approved asynchronous projection
**When** the result is generated
**Then** it identifies the projection, projection version/time, data-as-of,
and projected classification.

**RPT-GWT-011 — Stale source**

**Given** the projection or source is outside the approved freshness condition
**When** a user views the result
**Then** it is labeled Stale, with the affected source and data-as-of, and is
not presented as current.

**RPT-GWT-012 — Partial source**

**Given** one permitted source partition is unavailable and the remaining
scope can be identified
**When** the report is generated
**Then** it is labeled Partial and states included, excluded, and unavailable
scope without claiming a complete total.

**RPT-GWT-013 — Unknown delivery**

**Given** a source or report-job delivery outcome cannot be proven
**When** generation or retry is evaluated
**Then** the affected result is Unknown or unavailable, the outcome is
audited, and a scoped reconciliation path is created.

**RPT-GWT-014 — Delayed source**

**Given** a source event is delayed beyond the report's known data-as-of point
**When** the report is viewed
**Then** the report shows the delay or stale/partial status and does not
silently add or estimate the missing event.

### 21.3 Lineage and reconciliation

**RPT-GWT-015 — Source drill-through**

**Given** an authorized report row has an available source record
**When** the user drills through
**Then** the link identifies the owning source record/status and preserves
Tenant and organization scope.

**RPT-GWT-016 — Denied drill-through**

**Given** an aggregate is authorized but a source row is not
**When** the user drills through
**Then** the result is redacted or unavailable without leaking identifiers,
amounts, filenames, or sensitive authorization details.

**RPT-GWT-017 — Posted financial lineage**

**Given** a Finance report displays a posted amount
**When** the result is generated
**Then** it identifies the Finance source status and source-document,
subledger, and GL path required by the definition.

**RPT-GWT-018 — Inventory ledger lineage**

**Given** an Inventory report displays quantity or value
**When** the result is generated
**Then** it distinguishes immutable movement/ledger facts from projected
balance and identifies freshness and valuation evidence.

**RPT-GWT-019 — Reconciliation difference**

**Given** independent source records do not match
**When** the reconciliation result is generated
**Then** it shows the comparison basis, difference, status, source snapshots,
and investigation/correction link without clearing or mutating the source.

**RPT-GWT-020 — Unresolved reconciliation owner**

**Given** MESP-53 has not assigned a named reconciliation owner
**When** a report result is generated
**Then** the required owner field is visibly unresolved and the final
publication/implementation gate remains blocked; no person is invented.

**RPT-GWT-021 — Matched evidence**

**Given** independent records match under the same scope, time, currency, and
data-as-of basis
**When** the reconciliation is reviewed
**Then** the result records the independent evidence and matched status; it
does not imply broader scope or a different accounting policy.

**RPT-GWT-022 — Source correction**

**Given** an owning domain posts an approved correction, return, reversal, or
credit/debit correction
**When** Reporting is rerun
**Then** the new result links to the correction and preserves the earlier
result and its original data-as-of.

### 21.4 Domain semantics and conditional branches

**RPT-GWT-023 — Procurement chain**

**Given** a procurement report includes an order, receipt, invoice, or payment
reference
**When** a user follows the chain
**Then** each source status and missing/delayed link is visible and Reporting
does not post receipt, invoice, or payment.

**RPT-GWT-024 — Sales chain**

**Given** a sales report includes order, fulfillment, invoice, receipt,
return, or credit information
**When** a user follows the chain
**Then** Sales, Inventory, and Finance source boundaries remain visible and
Reporting does not confirm, deliver, allocate, or credit.

**RPT-GWT-025 — Pending Finance fact**

**Given** a Finance source fact is pending or unposted
**When** a financial report is generated
**Then** the fact is excluded or separately labeled Pending and is not shown as
posted.

**RPT-GWT-026 — Payment Term and aging gate**

**Given** FIN-OD-09 / MESP-110 has not approved Payment Term, due-date, aging,
settlement, or historical mechanics
**When** AP/AR aging or overdue figures are requested
**Then** the report shows the conditional/pending gate and does not choose a
bucket, due-date rule, or default interval.

**RPT-GWT-027 — Fiscal/year-end gate**

**Given** fiscal-year and year-end mechanics remain unapproved
**When** a report is requested across a fiscal or year-end boundary
**Then** it preserves source period facts or returns a gated result and does
not infer rollover, close, or opening mechanics.

**RPT-GWT-028 — Finance dimensions gate**

**Given** Finance posting dimensions remain open
**When** a user requests a dimensioned report
**Then** the report does not invent Cost Center or another dimension and
identifies the missing Finance decision.

**RPT-GWT-029 — Reporting Currency gate**

**Given** MESP-54 has not approved Reporting Currency or exchange-rate/rounding
policy
**When** a user requests a presentation-currency report
**Then** the result preserves available Transaction/Base Currency facts and
shows Reporting Currency as unavailable or conditional.

**RPT-GWT-030 — Source rate facts**

**Given** a source provides a currency and rate fact
**When** an approved report displays it
**Then** the report identifies the source-provided fact and does not choose a
different source, date, approval workflow, or rounding method.

**RPT-GWT-031 — Inventory tracking/expiry gate**

**Given** MESP-41 has not approved tracking/expiry behavior
**When** an inventory aging or expiry report is requested
**Then** the report labels the branch conditional and does not invent batch,
lot, serial, expiry, or aging mechanics.

**RPT-GWT-032 — Saudi/statutory gate**

**Given** MESP-37, MESP-49, or required external validation is incomplete
**When** a tax, Saudi, or statutory report is requested
**Then** the result identifies the gate and does not assert compliance or
publish an invented statutory catalogue.

**RPT-GWT-033 — Separate Companies**

**Given** a Tenant has multiple legal Companies
**When** a user requests financial reporting
**Then** the result preserves Company boundaries and does not create
consolidation, intercompany, elimination, or transfer-pricing figures.

### 21.5 Exports, audit, and operational outcomes

**RPT-GWT-034 — Authorized bounded export**

**Given** a user has separate export permission for an approved report
**When** the user requests an export within permitted scope
**Then** the export preserves parameters, source status, data-as-of,
freshness, lineage, privacy, and audit metadata.

**RPT-GWT-035 — Unauthorized export**

**Given** a user can view a report but lacks export permission
**When** the user requests or guesses an export link
**Then** the export is denied and no artifact or sensitive error is disclosed.

**RPT-GWT-036 — Large asynchronous export**

**Given** a permitted export exceeds the approved synchronous boundary
**When** the export is requested
**Then** it becomes an auditable asynchronous job with the initiating scope,
definition version, retry/unknown outcome, and private artifact controls.

**RPT-GWT-037 — Scheduled delivery not implicit**

**Given** MESP-53 has not approved scheduled distribution
**When** a user asks for recurring delivery
**Then** the request remains a pending decision and no schedule, recipient,
frequency, or delivery artifact is created.

**RPT-GWT-038 — Duplicate request**

**Given** the same stable report/export request is delivered twice
**When** both requests are processed
**Then** duplicate handling is idempotent, evidence is correlated, and no
duplicate source, financial, stock, or artifact effect is claimed.

**RPT-GWT-039 — Retryable failure**

**Given** a report source or export job fails with a retryable outcome
**When** the job is retried
**Then** the retry remains bounded, scoped, correlated, and cannot duplicate
an already proven result.

**RPT-GWT-040 — Dead-letter outcome**

**Given** a job reaches a terminal/dead-letter outcome
**When** a reviewer inspects the report status
**Then** the reason, attempts, scope, and reconciliation action are visible
and no success is implied.

**RPT-GWT-041 — Audit history**

**Given** an authorized user views, exports, or reviews reconciliation evidence
**When** the action completes
**Then** the identity, Tenant, scope, report/definition, parameters, outcome,
and time are recorded subject to MESP-50.

**RPT-GWT-042 — Immutable report evidence**

**Given** a result has been published
**When** a later source correction or definition version is approved
**Then** the earlier evidence remains immutable and the later result links to
it as superseding or correcting evidence.

**RPT-GWT-043 — Worker revalidation**

**Given** an authorized user requested an asynchronous result
**When** the worker begins producing the result or artifact
**Then** it revalidates the initiating Tenant, scope, permission, entitlement,
and definition before effect.

**RPT-GWT-044 — Privacy/retention gate**

**Given** MESP-50 has not approved retention, purge, legal hold, residency, or
attachment rules
**When** a report includes sensitive rows or an export artifact
**Then** the result remains subject to the gate and does not invent a
retention period, public link, or deletion policy.

### 21.6 Migration, localization, and release boundaries

**RPT-GWT-045 — Migration rejection**

**Given** a source row is rejected during an MESP-51 migration batch
**When** a cutover report is generated
**Then** the row, reason, batch, source, and reconciliation impact remain
visible and are not counted as a successful live transaction.

**RPT-GWT-046 — Opening-state distinction**

**Given** opening inventory, AR/AP, cash, tax, or GL data is loaded or
rehearsed
**When** a migration report is generated
**Then** opening/migrated, pending, rejected, unknown, and posted-live states
are distinguishable with batch and sign-off evidence.

**RPT-GWT-047 — Arabic and English**

**Given** an authorized user selects Arabic or English
**When** the same report is displayed
**Then** labels, direction, dates, and number presentation adapt without
changing source meaning, scope, amount, or status.

**RPT-GWT-048 — No Retail POS behavior**

**Given** an example or requested report uses retail/POS terminology
**When** it is assessed against this BRD
**Then** it is rejected as out of scope unless a later approved scope change
explicitly authorizes it.

**RPT-GWT-049 — Wafra validation-only**

**Given** Wafra provides a validation example
**When** a report requirement is assessed
**Then** it may validate generic B2B ERP behavior but cannot create
Wafra-specific core reporting behavior.

**RPT-GWT-050 — No source mutation**

**Given** a report shows a mismatch or exception
**When** a user attempts to correct it through the report
**Then** Reporting offers only an authorized source-domain path or evidence
reference and never mutates the source.

**RPT-GWT-051 — Final catalogue gate**

**Given** this BRD has been approved but MESP-53 remains To Do
**When** a later team asks for implementation scope
**Then** the PRD minimum table is treated as a baseline for decision, not as a
final catalogue or named-owner approval, and implementation cannot proceed
under this document alone.

## 22. Open decisions and gates

The following decisions remain open. This table is a traceability map, not a
new approval record. The exact live decisions remain in Jira/MESP-23 and their
owning issues.

| ID | Open decision/gate | Owner/evidence path | Reporting consequence |
|---|---|---|---|
| RPT-OD-001 | Final Release 1 report catalogue, figure definition, named business owners, and named reconciliation ownership | MESP-53; Product Owner, Finance Controller, and relevant domain owners through the Product Decision Register | **Critical blocker** for final catalogue, formulas/semantic publication, named ownership, and implementation activation. |
| RPT-OD-002 | Configurable parameters, saved views, scheduled report/export frequency, recipients, and distribution channels | MESP-53, subject to MESP-50/privacy and platform controls | No schedule or distribution behavior is implied by RPT-002. |
| RPT-OD-003 | Final KPI formulas, aggregation, inclusion/exclusion, freshness and reconciliation semantics | MESP-53 plus relevant source-domain semantic evidence | KPI labels remain baseline candidates; no final formula is supplied here. |
| RPT-OD-004 | Reporting Currency policy, exchange-rate source/update/approval/date, realized/unrealized treatment, and rounding | MESP-54 / Finance | Currency facts remain source-provided or conditional; no conversion policy is chosen. |
| RPT-OD-005 | Fiscal-year, fiscal-period, year-end, Payment Term, due-date, aging, settlement, and historical preservation mechanics | FIN-OD-09 / MESP-110 / Finance | Finance report branches remain conditional and cannot infer mechanics from labels. |
| RPT-OD-006 | Finance posting dimensions, including Cost Center and any reportable dimensions | FIN-OD-09 / MESP-110 / Finance | Reporting cannot introduce dimensions or dimension formulas. |
| RPT-OD-007 | Batch/lot/serial/expiry and inventory aging/expiry behavior | MESP-41 / Inventory | Inventory aging/expiry branches remain conditional. |
| RPT-OD-008 | Saudi statutory, tax, localization, ZATCA, e-invoicing, banking, and external compliance requirements | MESP-37, MESP-49, and external validation | No statutory report catalogue or compliance conclusion is approved. |
| RPT-OD-009 | Retention, privacy, legal hold, purge, residency, attachment, backup, and restoration | MESP-50 / ADR-009 | Report and export evidence is private and gated; no duration/provider is selected. |
| RPT-OD-010 | Supported volume, freshness SLAs, async thresholds, recovery, and continuity | MESP-48 and later operational readiness | No numeric performance or recovery value is invented. |
| RPT-OD-011 | Migration mapping, opening balances, cutover, and reconciliation sign-off | MESP-51 | Migration reporting preserves evidence but does not authorize a load or posting. |
| RPT-OD-012 | Delegation, approval substitution, and support scope for report definitions/exports/reconciliation | MESP-55 and platform authorization controls | No delegation or substituted approver is silently assigned. |
| RPT-OD-013 | Integration provider, production delivery, monitoring, and operational recovery implementation | ADR-006/007/008/009, MESP-48, MESP-50, and implementation readiness | This BRD sets business outcomes only; provider and production gates remain open. |
| RPT-OD-014 | Currency / M95-SL-06 execution sequence and Exchange Rate activation | Currency remains unexecuted; MESP-54 gates Exchange Rate | This session does not activate Currency or Exchange Rate. |

### 22.1 Decision handling rule

An open decision may be changed only by a traceable superseding approval in
the owning Jira/decision record. A recommendation, example, report label,
source field, or implementation convenience cannot answer it. When a decision
is approved, the affected report definition must record the decision
identifier, effective date, scope, and supersession relationship.

## 23. Definition of Ready for a later implementation item

A later implementation item must not activate from this BRD alone. Before
implementation, it must demonstrate:

- MESP-53 has approved the final catalogue, definitions, named business
  owners, reconciliation accountability, and any scheduled/export branch;
- each selected report/KPI has source semantics, formula/version, effective
  dates, filters, scope, freshness, lineage, and reconciliation evidence;
- MESP-54 has resolved any Reporting Currency/rate/rounding requirement;
- FIN-OD-09 / MESP-110 has resolved any fiscal/year-end, Payment Term/aging,
  settlement, and Finance-dimension requirement;
- MESP-41, MESP-37, MESP-49, MESP-50, MESP-48, and MESP-51 gates are resolved
  for the selected branch;
- Procurement, Inventory, Finance, and B2B Sales source ownership and
  correction/reversal paths remain intact;
- authorization, Tenant isolation, Company/Branch/Warehouse scope,
  separation of duties, support grants, privacy, export, audit, and
  immutable evidence are mapped to approved platform controls;
- migration, integration, retry, dead-letter, unknown, recovery, volume,
  retention, residency, backup, and production evidence is approved; and
- the implementation plan is a separately authorized Jira item. No source
  implementation is authorized by MESP-36.

## 24. Business-owner approval

The bounded Owner approval of this document baseline is recorded in Jira
comment 10771, following focused validation in comment 10770, against the
reviewed content head identified in the document control block. Approval means
that this document correctly records the approved PRD minimum reporting
baseline, source ownership boundaries, lineage, freshness, reconciliation
requirements, conditional branches, and decision gates.

The approval does not answer MESP-53, MESP-54, FIN-OD-09 / MESP-110, MESP-37,
Currency, MESP-41, MESP-48, MESP-49, MESP-50, MESP-51, or any other open
decision. It does not authorize implementation.

## 25. Completion and handoff record

At completion of the single MESP-36 session, the repository and Jira record
must show:

- this BRD as the canonical Reporting and Analytics artifact;
- no source, schema, migration, API, UI, provider, database, infrastructure,
  production, Currency, MESP-37, or next-task implementation changes;
- MESP-53 explicitly open and critical;
- the complete entry gate and all open decisions preserved;
- focused Markdown/link/decision-boundary validation and complete diff
  review;
- Jira activation, validation, Owner approval, MESP-23 handoff, and closure
  evidence;
- the focused review Pull Request merged cleanly to main;
- TASK.md updated to the exact next separately authorized task without
  executing it;
- .ai/CURRENT_STATE.md, the delivery plan, genuinely affected state files,
  and docs/staticts.md updated conservatively; and
- the branch synchronized and the session stopped.
