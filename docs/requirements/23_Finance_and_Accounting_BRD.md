# Finance and Accounting Business Requirements Document

> **Version:** v0.1 — Approved Business Baseline
>
> **Jira:** MESP-34 — Produce Finance and Accounting BRD
>
> **Parent:** MESP-10 — Finance and Accounting
>
> **BRD sequence:** 9 of 15
>
> **Date:** 10 August 2026
>
> **Scope:** Release 1 B2B ERP only; Wafra is validation-only
> **Status:** Approved business baseline; documentation-only; no implementation authorization

> **Independent-review reconciliation overlay:** MESP-109 is **Done** after
> reconciling the
> accepted non-blocking findings O5-FIN-001 through O5-FIN-010 from the
> independent Opus 5 Finance checkpoint. The approved MESP-34 baseline remains
> historically Done; this overlay adds traceability and bounded governance
> corrections only. See `docs/99_Independent_Opus_5_Finance_BRD_Reconciliation.md`.

## 1. Document control and reading rules

This document is the Release 1 business-requirements baseline for Finance and
Accounting. It defines the business outcomes, actors, workflows, controls,
cross-module handoffs, acceptance scenarios, and decisions needed before the
Finance domain can be implemented and before B2B Sales can rely on its posting
foundation.

It is intentionally business-level. It does not define database tables,
entities, migrations, APIs, controllers, screens, framework behavior, provider
selection, deployment topology, or an automated test plan. A future
implementation specification must preserve this document and the approved
decision evidence; it cannot turn a recommendation or an open decision into a
requirement silently.

### 1.1 Status and evidence conventions

| Label                         | Meaning                                                                                                                                          |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Approved Product Baseline** | Explicitly approved in the PRD, an approved owning BRD, or a named Jira/Product Decision Register record.                                        |
| **BRD Requirement**           | Business behavior required by this document after Owner approval, subject to the named open decisions and gates.                                 |
| **Open Decision / Gate**      | Not approved. The row must remain visible and must not be implemented as a hidden default.                                                       |
| **Recommendation only**       | A safe working proposal recorded to accelerate a named decision; it is not a requirement.                                                        |
| **External validation**       | Requires a qualified Saudi tax, compliance, privacy, legal, banking, or other specialist decision before the affected scope is production-ready. |

The term “Finance” in this document means the Finance and Accounting business
domain. It does not imply that a particular person may approve, post, pay,
change a period, or access a Tenant. Those authorities are governed by the
approved Permission catalogue, Tenant context, Company/Legal Entity scope, and
the policy decisions recorded below.

## 2. Authority, source baseline, and traceability

### 2.1 Source priority

Where sources disagree, the higher source controls and the discrepancy must be
recorded rather than silently reconciled:

1. Named Owner approval or an approved Product Decision Register record.
2. The canonical approved PRD v1.2: `docs/MESP_PRD_v1.2.docx`.
3. The approved owning BRD or upstream BRD for the affected concept.
4. Approved architecture and ADR boundaries for feasibility and safety.
5. The approved glossary and the live MESP-23 decision register.
6. The Product Delivery Master Plan and other planning material.

The PRD is a product baseline, not an implementation authorization. The
Foundation Release 1 specification and ADR index constrain Tenant ownership,
scope, audit, concurrency, idempotency, and production gates; they do not
invent Finance policy.

### 2.2 Primary PRD anchors

| Anchor          | Finance requirement carried into this BRD                                                                                                                                                                                                                     |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FIN-001         | A controlled Chart of Accounts is maintained per accounting Company/Legal Entity, with hierarchy, account type, posting eligibility, currency behavior, and effective dates.                                                                                  |
| FIN-002         | Purchasing, sales, inventory, cash, AP, and AR source/subledger events produce traceable, balanced GL effects through documented posting paths.                                                                                                               |
| FIN-003         | Journals have a controlled lifecycle, source links, dates, periods, currencies, rates, dimensions, balanced debit/credit lines, and reversal behavior.                                                                                                        |
| FIN-004         | AP supports supplier invoices and credit notes, due dates, payment proposals, approval, settlement, and aging.                                                                                                                                                |
| FIN-005         | AR supports invoices, receipts, credit notes, allocations, aging, customer statements, and overdue visibility.                                                                                                                                                |
| FIN-006         | Cash and bank accounts support transfers, charges, deposits, statement import, matching, reconciliation, and controlled adjustments.                                                                                                                          |
| FIN-007         | Tax rules are configurable and effective-dated; the calculation basis and reviewable summaries are retained.                                                                                                                                                  |
| FIN-008         | Authorized Finance users control fiscal periods through open, soft-close, close, and policy-controlled reopen behavior.                                                                                                                                       |
| FIN-009         | Authorized users can obtain the trial balance, GL, P&L, balance sheet, cash movement, AR/AP aging, tax, and bank-reconciliation views within approved scope.                                                                                                  |
| FIN-010         | Document, functional, and applicable reporting currency amounts, exchange-rate facts, and rounding differences are preserved.                                                                                                                                 |
| FIN-011         | Release 1 includes balanced core GL, AP, AR, cash/bank, tax posting, period controls, and source traceability; consolidation, advanced statutory certification, fixed assets, payroll, and other later country packs are excluded unless separately approved. |
| BR-008 / BR-009 | Finance is the accounting control point for posting, reconciliation, and financial truth; accounting records are attributable, auditable, and protected from silent mutation.                                                                                 |

### 2.3 Required related baselines

| Source                                                                         | Relevance to this BRD                                                                                                                                                                                                                                                                                                                                                                |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `docs/21_Procurement_and_Purchase_to_Pay_BRD.md` (MESP-32)                     | Approved P2P commercial chain and the handoff where a Purchase Invoice creates AP/input-tax accounting without changing stock. Matching, supplier confirmation, payment method, and procurement approvals remain decision-controlled.                                                                                                                                                |
| `docs/22_Inventory_and_Warehouse_Management_BRD.md` (MESP-33)                  | Approved physical stock boundary, immutable stock ledger, moving-weighted-average evidence, and Inventory-to-Finance valuation/reconciliation handoff. Finance must not duplicate or overwrite the physical stock ledger.                                                                                                                                                            |
| `docs/00_ERP_Business_Glossary.md`                                             | Controlled meanings for Company/Legal Entity, GL, subledger, journal, posting, reversal, AP, AR, allocation, reconciliation, currency, and valuation. Terms marked Draft or Requires Business Decision remain open.                                                                                                                                                                  |
| MESP-22 Jira Product Decision Register and MESP-23 live Jira decision register | Named approved decisions and the unresolved decision register. MESP-52/PD-020 and MESP-56/PD-021 are preserved exactly; no other decision is closed by this BRD.                                                                                                                                                                                                                     |
| `docs/15_Foundation_Release_1_Lean_Implementation_Specification.md`            | Server-derived Tenant context, downward Company/Branch/Warehouse scope, immutable audit, approval separation, optimistic-concurrency and idempotency expectations, and explicit MESP-48/MESP-50 gates.                                                                                                                                                                               |
| `docs/Decisions.md` and applicable ADRs                                        | ADR-002 project/module ownership, ADR-004 authorization/session seam, ADR-006 module persistence and cross-module transaction boundary, ADR-007/008 durable work and reconciliation, ADR-009 private files, ADR-010 OpenTelemetry exporter and operational-data retention, ADR-011 localization requirement, ADR-012 production hosting/RPO/RTO, ADR-014 retention/privacy/purge, ADR-015 Saudi e-invoicing adapter and credential boundary, ADR-016 isolation review, ADR-017 external partner/API authentication, and ADR-018 test/production-like gates. |
| `docs/94_Product_Delivery_Master_Plan.md`                                      | Phase 2 BRD exit criteria, domain sequence, one active item at a time, and the separate MESP-35 handoff.                                                                                                                                                                                                                                                                             |

## 3. Business purpose and outcomes

Finance and Accounting provides the financial control plane for each Release 1
Company/Legal Entity. It must turn approved business events into traceable
financial facts, expose the state of obligations and receivables, protect
posted history, and make every balance reconcilable to its source.

The desired outcomes are:

- one controlled GL per Company/Legal Entity, supported by transparent AP, AR,
  inventory, purchasing, sales, cash, and tax subledger paths;
- financial facts that identify their source document, actor, Company, period,
  currency and rate facts, tax basis, approval evidence, and correction path;
- predictable period control: no accidental posting into a closed period and
  no silent reopening or editing of posted history;
- clear separation between approval, posting, payment, reconciliation, and
  administration, with business decisions and audit evidence visible;
- correct handling of partial settlement, credit notes, returns, unmatched
  bank activity, unknown external outcomes, retries, and concurrency conflict;
- a posting foundation that Procurement, Inventory, and the later B2B Sales
  BRD can consume without redefining Finance ownership;
- Saudi-localized behavior that is ready for qualified external validation
  without making unverified tax, ZATCA, banking, privacy, or legal claims; and
- reports and operational measures that identify stale, failed, unmatched,
  unreconciled, or policy-blocked financial work.

## 4. Scope

### 4.1 In scope for Release 1 B2B Finance

1. Company/Legal Entity accounting boundary and Finance configuration.
2. Chart of Accounts, account status, posting eligibility, and mapping policy.
3. General Ledger, journals, journal lines, posting, reversal, and source
   lineage.
4. AP supplier invoices, supplier credit notes, due dates, allocations,
   payment proposals, settlements, aging, and statements.
5. AR customer invoices, customer receipts, credit notes, allocations,
   settlement, aging, statements, and overdue visibility.
6. Cash accounts, bank accounts, transfers, charges, deposits, statement
   imports, matching, and reconciliation.
7. Configured, effective-dated tax treatment and retained tax evidence.
8. Fiscal calendar and period lifecycle: open, soft-close, close, and
   controlled reopen/adjustment behavior.
9. Reconciliation from source documents and subledgers through GL control
   accounts and inventory valuation evidence.
10. Document/functional/reporting currency facts, rate evidence, precision,
    and rounding-difference handling, subject to MESP-54.
11. Core financial statements, operational reports, KPIs, notifications,
    exports, import controls, audit evidence, and business-level observability.
12. Opening-balance and financial migration requirements, subject to MESP-51.
13. B2B posting handoffs needed by Procurement, Inventory, and later B2B Sales.

### 4.2 Explicitly out of scope

- Retail POS, cash-register or store-specific behavior, and Wafra-specific core
  code or policy. Wafra remains validation-only.
- Consolidated statements, automated intercompany, elimination entries,
  transfer pricing, or consolidation currency. Multiple legal entities may
  exist in one Tenant, but Release 1 does not consolidate them. This preserves
  the exact approved MESP-56/PD-021 decision.
- Payroll, fixed assets, advanced treasury, budgeting, manufacturing,
  subscription billing, metered usage, overage billing, or payment accounting
  for SaaS plans. MESP-52/PD-020 remains the exact approved subscription
  boundary.
- A legal, tax, banking, ZATCA, privacy, residency, or statutory-certification
  conclusion. Such conclusions require the named external validation gates.
- Creating or resolving MESP-41 through MESP-55, except for recording their
  Finance impact and preserving their current status.
- Physical persistence, EF entities, migrations, APIs, UI, provider selection,
  infrastructure, production deployment, supported-volume claims, retention,
  purge, legal hold, backup, restoration, or residency implementation.

## 5. Actors and responsibilities

The following are business roles and accountability boundaries. They are not a
permission catalogue and do not grant access by name alone.

| Actor                                  | Business responsibility                                                                                                                 | Cannot infer from this table                                                                              |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Finance Controller / Head of Finance   | Owns Finance policy, Chart of Accounts governance, posting policy, periods, reconciliation ownership, and Finance decision bundle.      | A universal approval threshold, rate source, payment method, or tax conclusion.                           |
| Accountant                             | Maintains permitted Finance data, prepares journals, invoices, allocations, reconciliations, and reports within assigned Company scope. | Permission to approve their own work, post every type of journal, reopen periods, or pay suppliers.       |
| Finance Approver                       | Reviews and approves configured financial actions or exceptions.                                                                        | Authority to post, pay, or approve outside the approved scope or policy.                                  |
| Treasury / Cash Officer                | Owns cash/bank method, statement, matching, deposit, transfer, and bank reconciliation decisions.                                       | A bank integration, payment gateway, or automated feed. MESP-47 is open.                                  |
| Procurement Owner                      | Owns P2P commercial controls and confirms procurement-side source/matching inputs.                                                      | AP posting or payment authority.                                                                          |
| Inventory Owner                        | Owns physical stock movements and stock-ledger evidence; supplies valuation facts to Finance.                                           | GL account mapping, tax, AP, AR, or period policy.                                                        |
| Sales Owner                            | Owns B2B order, delivery, return, and customer lifecycle inputs; later MESP-35 owns detailed O2C behavior.                              | Revenue recognition, AR posting policy, credit-control decision, or receipts method.                      |
| Company / Legal Entity Administrator   | Maintains authorized Company configuration under Tenant scope.                                                                          | Permission to change posted history or cross legal-entity records.                                        |
| Tenant Administrator                   | Manages authorized Tenant administration under the Foundation scope model.                                                              | Access to Finance facts without the appropriate Finance permission and Company scope.                     |
| Reconciliation Owner                   | Investigates and signs off a named reconciliation, records variance disposition, and escalates unresolved differences.                  | Permission to erase, rewrite, or hide the underlying facts.                                               |
| Product Owner                          | Resolves cross-module product choices and ensures the BRD sequence remains controlled.                                                  | Authority to provide a tax, banking, privacy, or legal specialist conclusion.                             |
| Qualified Saudi tax/compliance adviser | Validates Saudi tax/e-invoicing content and applicable external obligations for MESP-49.                                                | A general Owner approval or this BRD itself.                                                              |
| Qualified privacy/legal adviser        | Validates residency, retention, legal hold, purge, and privacy implications for MESP-50.                                                | A default region or retention period.                                                                     |
| External bank/payment partner          | Provides an approved external outcome or statement where an integration is later authorized.                                            | A silent success signal, internal posting authority, or a recovery policy not agreed by Finance/Treasury. |

## 6. Controlled terminology

The glossary remains authoritative. The following terms are used exactly as
business boundaries in this BRD:

| Term                          | Required meaning in this BRD                                                                                                                                                                                           |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Company / Legal Entity        | One Tenant-owned accounting boundary. A Tenant may contain multiple Companies/Legal Entities. Each has its own books and functional currency.                                                                          |
| General Ledger                | Complete, balanced financial posting record and the financial truth for one Company/Legal Entity.                                                                                                                      |
| Subledger                     | Detailed AP, AR, inventory, cash, purchasing, or sales financial facts that reconcile to GL control accounts.                                                                                                          |
| Journal / Journal Entry       | A controlled grouping of balanced debit/credit lines with source, date, period, currency, rate, and dimensions.                                                                                                        |
| Posting                       | The controlled act that commits financial effect. Posting is distinct from approval and makes the resulting record immutable.                                                                                          |
| Posting rule                  | Governed logic that determines account and dimension treatment from an approved source/event, Company, product/category/warehouse context, tax, and policy version. Exact catalogue values remain subject to decision. |
| Reversal                      | A linked equal-and-opposite correction for a posted financial effect. It does not delete or edit the original.                                                                                                         |
| AP / AR                       | Supplier obligation and customer receivable subledgers, respectively, each reconciling to a GL control account.                                                                                                        |
| Allocation                    | Whole or partial matching of a payment/receipt/credit to an invoice or other eligible open item.                                                                                                                       |
| Settlement                    | A controlled state showing that an obligation or receivable is fully discharged under the approved allocation rules.                                                                                                   |
| Reconciliation                | A controlled comparison between source, subledger, control account, GL, bank, or inventory valuation, with variance evidence and ownership.                                                                            |
| Functional/base currency      | Company/Legal Entity currency used for its books. For Saudi Companies, SAR is the approved product default, not a universal rule for every Tenant.                                                                     |
| Transaction/document currency | Currency in which a source document or monetary event is expressed and preserved.                                                                                                                                      |
| Reporting currency            | A later reporting view/currency owned by Reporting only where MESP-54 approves it; it is not consolidation, does not create second books, and does not transfer Finance ownership of monetary facts or rate evidence. |
| Exchange-rate fact            | Rate, date, source, effective context, precision, and conversion evidence used by Finance; source, approval, conversion, rounding, and revaluation policy remain MESP-54 / FIN-OD-04. |

## 7. Non-negotiable business invariants

The following are required business controls. They must remain true regardless
of implementation shape:

1. Every Finance fact belongs to exactly one Tenant and one applicable
   Company/Legal Entity; a client-supplied identifier never expands authority.
2. Company, Branch, and Warehouse scope flows downward from the server-derived
   Tenant context. A Branch/Warehouse user cannot read or post outside the
   authorized path or upward into ungranted Tenant-wide data.
3. One Tenant may have multiple legal entities. Their books, periods, balances,
   currencies, reconciliations, and reports remain separate in Release 1.
4. Every posted financial effect is balanced, attributable, source-linked,
   period-controlled, and immutable.
5. A posted document or journal is never silently edited, deleted, or reused.
   Correction uses a linked reversal, credit/debit note, return, or other
   approved forward correction.
6. A source business event cannot create two financial effects because of a
   retry, duplicate message, repeated click, re-import, or concurrent worker.
7. Every subledger/control-account balance has a documented path to the GL;
   unmatched, stale, failed, and unreconciled items remain visible.
8. A Purchase Order, supplier confirmation, sales order, reservation, or
   quotation is not an AP/AR liability or cash movement merely because it
   exists. The owning event determines when an accounting effect is created.
9. A Purchase Invoice creates the AP/input-tax accounting effect when approved
   for posting; it does not create stock. Goods Receipt is an Inventory event,
   and the valuation/accounting handoff is explicit.
10. Tax, currency, rate, precision, mapping, period, and policy versions used
    for a posted fact are retained so the result can be explained later.
11. A closed period rejects ordinary posting. Any controlled exception must be
    authorized, recorded, and posted in the approved adjustment path; a silent
    back-post is prohibited.
12. Approval and posting are separate decisions. Payment and reconciliation
    are separate controls. No role name in this BRD bypasses those boundaries.
13. An audit record distinguishes authorized, rejected, failed, reversed,
    unknown, and reconciled outcomes without storing unnecessary secrets or
    cross-Tenant payload.
14. Exact approved PD-020 and PD-021 remain in force; an open MESP decision is
    not closed by a Finance assumption.

## 8. Finance configuration and accounting foundation

### 8.1 Company and accounting setup

Before a Company/Legal Entity can post, the business must have an approved
accounting configuration for that Company:

- legal/accounting identity and Tenant ownership;
- fiscal calendar and period policy;
- functional/base currency and permitted transaction currencies;
- Chart of Accounts version and posting eligibility;
- required account mappings and dimensions for approved source events;
- tax configuration and effective dates;
- bank/cash account ownership and permitted methods, when applicable; and
- named Finance and reconciliation owners.

The Company is the accounting boundary. A Branch or Warehouse can be a
reporting or operational dimension only when the owning policy permits it; it
does not become a separate ledger by implication. Used Company ownership,
functional currency, or document-number boundary must not be silently rewritten.

The Foundation and MESP-30 establish the normal business-number boundary as
Company/Legal Entity plus Document Type. Optional Branch subdivision requires
later owning-domain or Saudi justification. Warehouse-level numbering and
automatic reset are not assumed, and allocated numbers are never reused.

### 8.2 Chart of Accounts and dimensions

Finance owns the controlled Chart of Accounts for each Company/Legal Entity.
Each account requires, at minimum, an identity, name, hierarchy/parent where
applicable, type, posting eligibility, active/effective dates, currency
behavior, and an auditable status history.

The business must be able to distinguish:

- posting accounts from grouping/header accounts;
- active accounts from retired/inactive accounts;
- control accounts from detailed subledger accounts;
- tax, rounding, clearing, suspense, variance, cash, AP, AR, revenue, cost,
  inventory, and equity treatment where approved; and
- account mapping versions and effective dates.

Cost center and other posting dimensions are Finance-owned concepts. This BRD
requires dimensions to be attributable and reportable when an approved posting
policy requires them; it does not invent a mandatory dimension catalogue or a
universal account tree. The Release 1 dimension catalogue and Cost Center
policy remain an explicit open Finance detail bundle in FIN-OD-09 / MESP-110,
not a circular confirmation request against the closed MESP-34 task.

### 8.3 Posting-rule catalogue

Finance must govern a visible posting-rule catalogue. A rule identifies the
source event, Company, document type, product/category or service context,
warehouse/branch dimensions where approved, tax treatment, currency behavior,
effective dates, account mapping, and the policy/version that authorized it.

The catalogue must:

1. have one effective interpretation for a posted event;
2. reject an incomplete or ambiguous mapping rather than guess;
3. preserve the rule/version used by a posted result;
4. permit a future rule version without rewriting history; and
5. expose a controlled exception when a source event has no valid rule.

The exact mapping of inventory valuation, landed cost, returns, adjustments,
negative stock, payment methods, tax, and currency differences remains an open
decision bundle. A recommendation in section 22.1 is not a posted rule.

### 8.4 Journal and posting foundation

Finance supports both source-generated journals and authorized manual journals.
Every journal entry must carry, as applicable:

- Company/Legal Entity and Tenant scope;
- journal/document type and non-reusable business number;
- source document and source event identity;
- accounting date, posting date, fiscal period, and policy versions;
- currency, functional/base amount, and rate facts where monetary;
- debit/credit lines with account, amount, dimensions, tax basis, and
  descriptive reason; and
- actor, approval, posting, reversal, and reconciliation evidence.

An entry is balanced before posting. A posted entry is immutable. An error in a
posted entry creates a linked reversal and, where needed, a corrected new entry;
it never overwrites the original. A source module may request a posting, but
Finance owns the accounting interpretation, period guard, and GL result.

### 8.5 Product, category, UOM, and tax-master boundary

Master Data owns Product/Item identity, Product Category, Unit of Measure, and
the reusable master-data records that Finance references. Finance consumes
stable master-data identity, effective status, permitted Company/Tenant scope,
quantity and UOM facts, and the approved tax/category attributes needed for a
posting. Finance does not redefine Product/Item identity, variant behavior,
operational availability, tracking, or UOM ownership.

When a source event depends on a UOM conversion, the conversion used for the
financial result must be identifiable and valid for the event. A missing,
ambiguous, inactive, or cross-Tenant Product/UOM reference blocks the affected
posting or routes it to the owning exception queue. A later master-data change
does not rewrite the quantity, cost, tax, or mapping facts already used by a
posted result. Inventory remains the owner of operational tracking and physical
valuation evidence; Finance remains the owner of accounting meaning.

## 9. Core Finance workflows

Each workflow below states the business trigger and preconditions, main path,
alternative path, and stop conditions. Detailed account values and decision
rows remain in section 22.1.

### 9.1 Period and configuration readiness

**Trigger:** A Company is being prepared for a new fiscal period or a change to
an approved Finance configuration.

**Preconditions:** The actor has authorized Company scope; the Company is
active; the Chart of Accounts, currency, tax, period, and required mapping
versions are present; no used ownership or posted history is being rewritten.

**Main path:** Finance reviews the configuration, opens the applicable period
under policy, records the effective version and owner, and makes the Company
available for the permitted source events.

**Alternative / exception paths:** An incomplete mapping, conflicting effective
date, inactive account, missing tax/rate fact, or unowned reconciliation blocks
the affected posting and creates a visible exception. A configuration change
takes effect prospectively unless an approved correction path says otherwise.

### 9.2 Manual journal

**Trigger:** An authorized Finance user needs a manual adjustment or a
Finance-owned accounting entry not generated by another source module.

**Preconditions:** Company and period are eligible; the user has the required
Finance authority; accounts and dimensions are posting-eligible; the entry has
a reason, source/reference, currency/rate facts where required, and balanced
lines.

**Main path:** Prepare draft → submit for any policy-required review → record
approval or rejection → post in the open period → expose in GL and relevant
reports → reconcile.

**Alternative / exception paths:** A rejected journal returns to a non-posted
state with evidence. An unbalanced, incomplete, duplicate, stale, or
out-of-period journal is denied. A posted journal is corrected only by linked
reversal and a new authorized entry.

### 9.3 Accounts Payable

**Trigger:** A supplier invoice or supplier credit note is received against an
approved procurement event or other authorized supplier obligation.

**Preconditions:** Supplier and Company are valid; invoice identity is not a
duplicate; source references and tax/currency facts are present; the applicable
matching, approval, period, and posting policies are known; the invoice is not
already posted or settled.

**Main path:**

1. Capture the supplier invoice/credit note and preserve the source identity.
2. Validate supplier, Company, dates, amounts, tax basis, currency, and
   duplicate status.
3. Match to the approved Purchase Order and accepted Goods Receipt when the
   approved policy requires matching.
4. Route a mismatch or missing source to a named exception owner; do not
   silently auto-post.
5. Obtain configured approval where required.
6. Post the AP liability and input-tax effect through the approved Finance
   mapping. The Purchase Invoice does not create physical stock.
7. Track due date, open balance, aging, payment proposal, settlement, and
   reconciliation to AP control and GL.

**Alternative paths:** Partial receipt or partial match may leave an open
remainder where the approved policy permits. A credit note may be applied to a
supplier invoice or remain unapplied with a visible balance. A supplier return
must link Inventory evidence and the Finance credit/correction path; it cannot
silently reverse stock or AP.

**Exception paths:** A duplicate, mismatch, invalid tax/rate, closed-period,
inactive-account, unknown supplier, or missing rule blocks or routes the item
to exception. A rejected invoice is not a posted liability. An external payment
with unknown outcome remains pending/unknown until evidence resolves it; it is
not marked paid merely because a request was sent.

### 9.4 Supplier payment and settlement

**Trigger:** An approved supplier payment proposal or authorized payment is
ready to be recorded.

**Preconditions:** The supplier obligation is eligible; payment amount,
Company, currency, method, bank/cash source, approvals, and allocation intent
are known; no conflicting payment is already recorded.

**Main path:** Record the payment outcome, create the cash/bank and AP effect
through approved mapping, allocate whole or partial amounts to eligible supplier
items, show any unapplied remainder, and reconcile to the bank/cash evidence.

**Alternative paths:** A partial payment leaves a measurable open AP balance.
An on-account or unapplied payment remains separately visible until allocated.
A reversal or bank correction creates a linked Finance correction and does not
rewrite the original payment.

**Exception paths:** Duplicate, rejected, returned, or unknown bank outcomes
remain separately classified. A payment cannot settle an item outside the
authorized Company/Tenant or a closed/blocked period without the approved
controlled path.

### 9.5 Accounts Receivable

**Trigger:** A later approved B2B Sales event requests an invoice, customer
credit note, or receivable correction.

**Preconditions:** The customer, Company, source delivery or authorized service
milestone, tax/currency facts, pricing/totals, period, and revenue/account
mapping are valid; the source has not already produced the same accounting
effect.

**Main path:** Validate the source, preserve invoice identity and numbering,
calculate and retain the configured tax basis, obtain required approval, post
AR/revenue/tax, issue the authorized customer document, track due date and
aging, and expose the open item for receipt allocation and reconciliation.

The detailed order-to-cash lifecycle remains MESP-35. This BRD defines the
Finance contract it must consume: a Sales source must identify the Company,
source event, quantities or service milestone, customer, totals, tax, currency,
and posting policy version; Finance returns a traceable AR result or a visible
business exception.

**Alternative paths:** A customer receipt may be partial, fully allocated,
on-account, or temporarily unapplied. A credit note may apply to an invoice or
remain open. A return must link the Sales and Inventory evidence and use the
approved credit/correction path.

**Exception paths:** Credit-limit policy, tax/rate, duplicate, closed-period,
missing mapping, customer status, or source mismatch blocks or routes the item
to the named owner. MESP-46 remains open and is not resolved here.

### 9.6 Customer receipt and allocation

**Trigger:** A customer receipt or other authorized AR settlement outcome is
received.

**Preconditions:** Customer, Company, amount, currency, method, receipt date,
cash/bank source, and evidence are known; allocation targets are in the same
Tenant/Company and remain open; the receipt is not a duplicate.

**Main path:** Record the receipt, create the cash/bank and AR effect through
the approved mapping, allocate it to one or more eligible invoices/credits,
show partial or unapplied balance, and reconcile to bank/cash and AR control.

**Alternative / exception paths:** A receipt may remain on account, be
partially allocated, or be classified as unidentified while investigation is
open. Rejected, returned, duplicate, or unknown bank outcomes must preserve
their status and evidence; they cannot silently close an invoice.

### 9.7 Cash, bank, and reconciliation

**Trigger:** A cash/bank transaction, statement, transfer, deposit, charge, or
reconciliation cycle is due.

**Preconditions:** The Company-owned cash/bank account is active; permitted
method and ownership are known; source/evidence has a stable identity; the
actor has Treasury/Finance authority; the period and mapping are eligible.

**Main path:** Import or capture the statement/evidence, classify or match
transactions to known Finance items, record approved transfers/charges/deposits,
post the controlled cash/bank effect, reconcile statement balance to the
Finance balance, and record owner/sign-off and any variance.

**Alternative paths:** An item may remain unmatched, be matched partially, or
be held for investigation. A controlled manual adjustment may be recorded only
with reason, evidence, approval, and a link to the affected reconciliation.

**Exception paths:** Duplicate statement rows, missing evidence, conflicting
matches, unknown external result, stale period, or unauthorized adjustment
blocks the item. No payment or receipt method is assumed; MESP-47 remains open.

### 9.8 Tax treatment

Finance must support effective-dated tax configuration for purchase and sales
contexts, including the applicable rate/category, calculation basis, input or
output classification, exemption/exception evidence where approved, rounding,
and tax-summary reporting.

The tax rule and rate used by a posted fact are preserved with the source and
posting evidence. A future effective date must not change a historical result.
The PRD identifies a configurable Saudi VAT seed of 15%; that is a product
configuration baseline, not a universal legal conclusion and not a hard-coded
tax guarantee. Qualified Saudi validation remains required before production.

This BRD does not decide statutory treatment, tax registration, filing,
certification, invoice obligation, or ZATCA timing. MESP-49 owns the Saudi
e-invoicing scope and external validation gate.

### 9.9 Fiscal periods, close, and reopen

Finance controls period states at Company level. The business lifecycle is:

`Not configured → Open → Soft-close / review → Closed`

An authorized Finance decision may reopen or use an adjustment path only under
the approved policy, with reason, actor, scope, affected period, and audit
evidence. Ordinary users and source modules cannot bypass a closed period.

Closing a period requires the named close checklist, including relevant AP/AR,
cash/bank, tax, inventory valuation, and GL reconciliation status. A failed or
unresolved reconciliation remains visible and blocks close when the approved
policy says it is blocking. Each accounting Company/Legal Entity has a
Finance-owned Fiscal Calendar with defined Fiscal Year and Fiscal Period
boundaries. Closing a Fiscal Year must preserve immutable posted history and
produce controlled, attributable, and reproducible year-end evidence;
corrections use approved reversal/adjustment and, where permitted, a
controlled reclose rather than rewriting prior effects. The exact profit/loss
closing or carry-forward, retained-earnings or equity entry, reopen authority,
and derived-reporting treatment remain open in FIN-OD-01 and the bounded
FIN-OD-09 / MESP-110 decision bundle. This BRD does not invent those mechanics.

### 9.10 Reversal, correction, and cancellation

Before posting, an authorized document may be rejected, withdrawn, or
cancelled according to its lifecycle and without financial effect. After
posting, correction is forward-only:

- reverse the original with a linked, attributable equal-and-opposite effect;
- create the corrected invoice, credit/debit note, payment, allocation, or
  journal with its own identity and period guard; and
- preserve the original, reversal, replacement, reason, approval, and
  reconciliation chain.

Cancellation does not erase a posted record. A correction failure does not
leave the original silently changed or create a second unlinked effect.

## 10. Posting foundation and cross-module lineage

### 10.1 Required lineage chain

Every financial effect must be explainable through this business chain:

`Source document/event → approved source status → Finance validation/mapping → subledger fact → balanced GL journal → report/reconciliation`

The chain may contain an approved exception or asynchronous handoff, but the
exception must identify its owner, status, retry/recovery decision, and whether
the financial effect has posted. A source event is not considered financially
complete merely because a downstream request was sent.

### 10.2 Posting foundation matrix

The matrix is a business contract and a boundary map. It deliberately does not
assign account numbers, tax rates, or valuation formulas. “Policy-controlled”
means the named decision must be approved before the affected posting is
enabled.

| Source event                                     | Owning business domain          | Finance effect / subledger requirement                                                                                                                           | Required guard or open policy                                                                             |
| ------------------------------------------------ | ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Purchase Request / quotation                     | Procurement                     | No AP liability or cash effect. Commercial intent remains traceable.                                                                                             | Procurement approval policy; no Finance posting.                                                          |
| Purchase Order                                   | Procurement                     | No stock and no AP liability solely because the PO exists; commitment reporting may be exposed without GL liability.                                             | MESP-42 approval and MESP-44 matching policy remain open.                                                 |
| Supplier confirmation                            | Procurement                     | No stock and no AP liability solely because confirmation exists.                                                                                                 | MESP-43 supplier-confirmation policy remains open.                                                        |
| Goods Receipt                                    | Inventory                       | Physical stock ledger is authoritative. Where an approved policy recognizes inventory value before a Purchase Invoice, Finance preserves a balanced, visible, source-linked interim effect without creating AP before the invoice. | MESP-33 valuation boundary plus FIN-OD-01 account/valuation policy; MESP-41/44/45 and FIN-OD-09 dependencies remain open. |
| Warehouse Transfer                               | Inventory                       | Physical source/destination or in-transit movement remains Inventory-owned. Within the same legal/accounting boundary it is not revenue, AP, or AR; Finance interprets only an approved value/variance handoff. | Inventory transfer/valuation policy and FIN-OD-01 remain open; no double-counting and full source linkage are required. |
| Stock Adjustment                                 | Inventory                       | Inventory records the authorized physical correction. A value-affecting adjustment or write-off reaches Finance only through an approved handoff with balanced, attributable, immutable evidence. | MESP-45 and FIN-OD-01 determine the accounting treatment; this row assigns no account or automatic write-off. |
| Inventory Count variance                         | Inventory                       | Inventory preserves the count and variance evidence. A financial effect occurs only after the approved review/handoff; differences remain visible and are not hidden by a balancing entry. | MESP-45 and FIN-OD-01 remain open for thresholds, approval, valuation, and mapping. |
| Stock Issue                                     | Inventory                       | A non-sales inventory-out event is physical Inventory evidence. It does not create AR or revenue merely by issue; Finance receives an effect only where approved policy requires it. | MESP-45 and FIN-OD-01 remain open; no account or COGS rule is invented here. |
| Purchase Invoice                                 | Finance with Procurement source | AP liability and input-tax effect when valid and approved for posting; no stock effect. Source, match result, tax basis, currency/rate, and period are retained. | MESP-44 matching, MESP-47 payment, MESP-49 Saudi e-invoice, MESP-54 rate policy.                          |
| Supplier Payment                                 | Finance / Treasury              | Cash/bank reduction and AP allocation/settlement, or visible unapplied/unknown status.                                                                           | MESP-47 payment/receipt method and bank outcome policy.                                                   |
| Supplier Return / credit                         | Inventory + Finance             | Physical return evidence and linked supplier credit/AP correction; no silent reversal or disconnected stock/finance result.                                      | MESP-33 return policy and MESP-44/47/54 dependencies.                                                     |
| Sales order / reservation                        | B2B Sales                       | No AR liability or revenue solely from order/reservation; Finance receives no posted effect until approved invoice/valuation event.                              | MESP-35 source lifecycle and MESP-46 credit policy.                                                       |
| Customer delivery / authorized service milestone | B2B Sales + Inventory           | Delivery alone does not create AR or revenue. It is a source for later inventory/COGS and/or service accounting only under an approved policy; preserve quantity, cost, source, and Company evidence without inventing unbilled revenue or revenue recognition. | MESP-33 valuation and MESP-35 delivery/milestone policy; exact mapping remains Finance-owned.             |
| Sales Invoice                                    | Finance with Sales source       | AR, revenue, and output-tax effect when valid and approved; source totals, tax basis, currency/rate, period, and document identity are retained.                 | MESP-35 invoice source; MESP-46 credit; MESP-49 Saudi; MESP-54 rate.                                      |
| Customer Receipt                                 | Finance / Treasury              | Cash/bank increase and AR allocation/settlement, or visible unapplied/unknown status.                                                                            | MESP-47 method and bank outcome policy.                                                                   |
| Customer Return / credit note                    | Sales + Inventory + Finance     | Linked inventory, AR/revenue/tax correction with preserved original and reason; no silent deletion.                                                              | MESP-33 return evidence, MESP-35 lifecycle, MESP-49/54 policy.                                            |
| Manual Finance journal                           | Finance                         | Balanced GL effect with reason, source/reference, period, currency/rate, approval, and reconciliation evidence.                                                  | Period, account, dimension, SoD, and journal approval policy.                                             |
| Bank statement / reconciliation adjustment       | Treasury / Finance              | Statement evidence, matching status, controlled cash/bank effect, variance, owner, and sign-off.                                                                 | MESP-47 method and MESP-53 report/reconciliation catalogue.                                               |

### 10.3 Handoff rules

When Inventory Goods Receipt creates a recognized inventory value before the
Purchase Invoice, the approved Finance posting policy must preserve a balanced,
visible, and reconcilable interim effect linked to the receipt, accepted
quantity, and valuation evidence. It must not create AP before the invoice.
The later invoice clears, reclassifies, or otherwise reconciles that interim
position under the approved policy while preserving the original receipt,
invoice, matching, partial/unmatched, correction, and audit history. Unmatched
or partial balances remain attributable, aged, visible, and owned; quantity,
price, and valuation differences may not be hidden. Exact accounts, mappings,
and clearing/accrual policy remain FIN-OD-01 and the owning decision, not a
new requirement in this BRD.

1. The source domain owns the source document lifecycle and physical or
   commercial facts; Finance owns accounting interpretation and GL effect.
2. Inventory owns the immutable physical stock ledger. Finance consumes
   valuation facts and owns the financial meaning/mapping; it must not create a
   second physical stock truth.
3. Procurement owns the commercial P2P chain. Finance owns Purchase Invoice,
   AP, payment, tax, period, and reconciliation effects.
4. B2B Sales will own order/delivery/return source lifecycle in MESP-35. Finance
   owns AR, revenue, tax, receipt, allocation, and related GL effects.
5. A failed downstream handoff must remain visible and reconcilable. A retry
   must not duplicate the source or financial effect.
6. Cross-module identifiers are references for lineage, not permission grants.
   Tenant and Company scope is rechecked at the receiving boundary.

## 11. Document and financial lifecycle

### 11.1 Common financial document states

The common business vocabulary is:

`Draft → Submitted / In review → Approved → Posted → Settled / Closed`

Not every document needs every state. Approval is policy-controlled; posting is
the separate Finance act. A document may move to `Rejected`, `Cancelled before
posting`, `Partially settled`, `Unapplied`, `Disputed`, `Reversed`, or
`Unknown external outcome` when the corresponding business condition exists.

### 11.2 Transition rules

| From                     | To                                        | Required condition                                                                                                          |
| ------------------------ | ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Draft                    | Submitted                                 | Required source, totals, tax/currency facts, owner, and period context are present.                                         |
| Submitted                | Approved                                  | Named approver decides under the applicable policy; rejection evidence is retained.                                         |
| Submitted / Approved     | Posted                                    | Validation passes, Company/period/account/rule mapping is eligible, and a Finance actor or approved source path posts once. |
| Draft / Submitted        | Rejected                                  | Validation or approval fails; no financial effect is created.                                                               |
| Draft / Submitted        | Cancelled                                 | Authorized pre-post cancellation; identity and reason remain auditable.                                                     |
| Posted                   | Reversed                                  | Authorized correction creates a linked equal-and-opposite effect.                                                           |
| Posted                   | Settled / Closed                          | All permitted allocations and reconciliation conditions are satisfied; history remains immutable.                           |
| Posted                   | Partially settled / Unapplied / Disputed  | The open balance or exception is measured and visible.                                                                      |
| Pending external outcome | Confirmed / Rejected / Returned / Unknown | Evidence resolves the outcome; Unknown is not treated as success.                                                           |

No transition may alter Tenant, Company, period, source identity, or posted
amounts after use. A new policy version applies prospectively unless an
approved correction explicitly governs historical treatment.

## 12. Data and validation requirements

### 12.1 Business data required

Depending on the document, Finance must retain:

- Tenant, Company/Legal Entity, and permitted Branch/Warehouse/dimension scope;
- document type, non-reusable number, status, source identity, and related
  document/reversal/credit/debit references;
- actor, preparer, approver, poster, payer/collector, reconciliation owner, and
  timestamps;
- account, control account, tax category/rule/version, dimension, and mapping
  version;
- document currency, functional amount, applicable reporting amount, rate,
  rate date/source, precision, rounding difference, and conversion evidence;
- invoice/receipt/payment terms, due date, open balance, allocation,
  settlement, and dispute status;
- period/calendar status and any adjustment/reopen evidence;
- source quantities/cost/valuation facts needed to reconcile Inventory effects;
- interim receipt-to-invoice accounting status, clearing/reconciliation
  reference, match or clear disposition, amount, age, owner, variance, and
  correction chain where a Goods Receipt has a recognized value before the
  Purchase Invoice;
- bank/cash account, statement reference, matching status, and reconciliation
  disposition; and
- audit, correlation, failure, retry, unknown-outcome, notification, import,
  export, and correction evidence appropriate to the business event.

### 12.2 Validation rules

An operation is rejected or routed to a named exception when:

1. Tenant, Company, or scope is missing, inconsistent, inactive, or outside
   the actor's server-derived authority.
2. The source is duplicate, already posted, already reversed, or incompatible
   with the requested correction.
3. A journal is unbalanced, an account is not postable, a mapping is ambiguous,
   or a required dimension/tax/currency fact is missing.
4. The period is closed, the date is invalid for the period, or a reopen /
   adjustment decision is absent.
5. A tax, rate, precision, source, payment, approval, or allocation rule is
   required but not approved or not effective for the event.
6. A supplier/customer/bank/cash/account/Company is inactive or unrelated to
   the source context.
7. Matching, credit-control, valuation, or reconciliation conditions fail.
8. A retry or concurrent command would create a second business effect.
9. A notification or external call failed after the financial result: the
   result and delivery failure are recorded separately.

Validation must return a safe business reason and a corrective owner. It must
not expose another Tenant's existence, data, or internal security details.

## 13. Permissions, approval, and separation of duties

### 13.1 Authority principles

- Finance actions require an authenticated actor, an active server-derived
  Tenant context, applicable Company/Branch/Warehouse scope, and the precise
  Finance permission for the operation.
- Platform governance context is not a Tenant Finance path. A Platform
  Administrator alone cannot query or change Tenant accounting data.
- Privileged access requires MFA and operation-bound fresh authentication under
  the Foundation security baseline where applicable.
- Access to one Company/Legal Entity does not grant access to another Company
  in the same Tenant.
- Reports, exports, imports, notifications, background work, audit, and
  reconciliations carry the initiating Tenant and approved scope.

### 13.2 Business action separation

At minimum, the business must be able to distinguish and audit:

| Control separation                     | Required evidence                                                                  |
| -------------------------------------- | ---------------------------------------------------------------------------------- |
| Prepare vs approve                     | Preparer, approver, decision, time, policy/version, rejection reason.              |
| Approve vs post                        | Approval does not itself imply posting; poster/source path is distinct.            |
| Post vs reverse                        | Reversal authority, reason, original link, replacement entry, and period decision. |
| Propose payment vs authorize payment   | Proposal, approval, payment outcome, method, and bank/cash evidence.               |
| Record receipt vs allocate/reconcile   | Receipt evidence, allocation decision, reconciliation owner, and variance.         |
| Configure rule/account/tax vs use rule | Effective version, change owner, approver, and resulting posting evidence.         |
| Close/reopen period vs post adjustment | Period decision, reason, authorizer, affected entries, and audit.                  |
| Investigate variance vs sign off       | Variance owner, disposition, supporting evidence, and sign-off.                    |

The exact approval catalogue, threshold, delegation, escalation, self-approval,
parallel/serial, and out-of-office behavior remain open under MESP-42,
MESP-44, MESP-46, and MESP-55. This BRD requires separate named decision
evidence; it does not invent a universal threshold or delegation rule.

### 13.3 Denial and safe failure

Unauthorized, cross-Tenant, wrong-Company, self-conflicting, stale, duplicate,
closed-period, missing-mapping, and policy-blocked actions are denied or routed
to a visible business exception. A denial is not represented as a successful
posting, approval, settlement, or reconciliation. Rejection evidence is
distinct from reversal evidence.

## 14. Concurrency, idempotency, failure, and unknown outcomes

These are business requirements, not implementation instructions:

- A repeated submission, retry, import, notification callback, or worker
  attempt produces one business effect and one auditable lineage, not duplicate
  AP, AR, cash, tax, or GL results.
- If two actors change the same Finance document, period, mapping, allocation,
  or reconciliation decision concurrently, the stale decision is rejected or
  re-reviewed; the last writer must not silently erase the earlier decision.
- A source posting and its Finance result are either visibly completed and
  traceable, or visibly pending/failed/unknown with an owner and recovery path.
- External bank/payment responses are classified as confirmed, rejected,
  returned, or unknown. Unknown is not paid, received, settled, or reconciled.
- A failed report, export, notification, or integration does not undo or invent
  a financial posting. It creates an operational failure linked to the result.
- A retry after an uncertain outcome must first inspect existing evidence and
  reconcile before creating a new effect.
- Background work rechecks Tenant, Company, permission/scope, lifecycle,
  policy, and source ownership before applying a financial effect.
- All denied, failed, unknown, reversed, and reconciled outcomes retain enough
  business evidence for an authorized owner to investigate without exposing
  secrets or another Tenant's data.

## 15. Reconciliation requirements

Finance must provide a named reconciliation path for each material balance:

| Reconciliation      | From / to                                                                            | Minimum business evidence                                                                    |
| ------------------- | ------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| Receipt-to-invoice interim | Inventory Goods Receipt/valuation evidence -> interim Finance effect -> Purchase Invoice/clearing/reconciliation | Receipt identity, accepted quantity, valuation, balanced effect, no AP before invoice, match status, amount, age, owner, variance, and disposition. |
| AP control          | Supplier invoices, credit notes, payments, allocations → AP control account and GL   | Item counts, gross/open balances, aged exceptions, period, owner, variance, disposition.     |
| AR control          | Customer invoices, credits, receipts, allocations → AR control account and GL        | Same evidence plus unapplied/on-account and overdue classification.                          |
| Cash/bank           | Bank/cash statement/evidence → Finance cash/bank balance                             | Statement identity, match status, unmatched/duplicate rows, adjustments, variance, sign-off. |
| Inventory valuation | Inventory immutable ledger/valuation evidence → Finance inventory/COGS effect and GL | Quantity/cost/valuation version, source movement, timing, variance, correction path.         |
| Tax                 | Purchase/sales tax facts → tax control/reporting result                              | Rule/rate/version, basis, input/output classification, rounding, exceptions, owner.          |
| Source-to-GL        | Source document/event → subledger → journal → report                                 | Stable lineage, posting status, reversals, failures, duplicates, and period.                 |
| Opening balances    | Approved migration source → opening subledger/GL balances                            | Source extract, mapping, preview, tie-out, approval, and rollback/cutover evidence.          |


Reconciliation is not satisfied by a matching total alone. The business must be
able to locate the source, explain timing differences, classify unknown or
unmatched items, and preserve variance disposition. MESP-53 owns the approved
report and reconciliation catalogue and named report owners.

## 16. Reports, KPIs, notifications, and audit

### 16.1 Minimum Finance report set

The PRD minimum set is the baseline for report catalogue review:

- trial balance;
- general ledger and journal detail;
- profit and loss;
- balance sheet;
- cash movement;
- AP aging and supplier statement;
- AR aging and customer statement;
- tax summary;
- bank/cash reconciliation; and
- source-to-subledger-to-GL reconciliation exceptions.

Each report must identify Company, period/as-of date, currency/view, scope,
data freshness/status, filters or policy version where material, and whether
the result is complete, pending, failed, or contains unreconciled exceptions.
Exported reports preserve Tenant/Company scope, report definition/version,
requester, time, and delivery outcome.

MESP-53 remains open for the final report catalogue, reconciliation ownership,
scheduled/export behavior, and operational views. No statutory completeness or
reporting deadline is claimed here.

### 16.2 KPIs and operational measures

The business should be able to monitor, at authorized scope:

- posted vs pending/failed/unknown source events;
- AP and AR open balance, overdue balance, days-to-due, and unapplied amount;
- aged unmatched bank items and reconciliation variance;
- source-to-GL or subledger-to-control breaks;
- invoices blocked by mismatch, tax, rate, period, credit, mapping, or
  approval policy;
- reversal/correction volume and reason categories;
- period close readiness and outstanding close exceptions;
- duplicate/retry/concurrency conflicts; and
- notification/export/integration failure status.

Targets, thresholds, retention, supported volume, and alert routing require
approved operational policy. MESP-48 and MESP-50 remain explicit gates.

### 16.3 Notifications and audit

Notifications may inform an owner about approval, exception, due date, payment,
reconciliation, period, or integration status. A notification is not an
authority grant and cannot itself change Finance state.

Immutable audit evidence must capture actor, action, purpose, exact Tenant and
Company scope, source/target identity, decision/result, policy and mapping
version, correlation/time, and relevant before/after status. It must
distinguish ordinary Tenant authorization from exceptional support access and
must not store passwords, tokens, private payloads, or unnecessary financial
data. Retention, legal hold, purge, residency, and evidence export remain
MESP-50 decisions.

### 16.4 Business observability

Finance operations must expose business-safe status and correlation evidence for
each source-to-GL path: received, validated, awaiting approval, posted,
reversed, reconciled, failed, retried, dead-lettered/held, or unknown. Owners
must be able to identify the affected Tenant/Company, source, period, policy
version, outcome, next action, and age without seeing another Tenant's data or
secrets. Operational dashboards and alerts are subject to the MESP-48 volume /
performance gate and MESP-50 privacy/retention gate; this BRD sets no capacity
or retention value.

## 17. Integration, import, and export requirements

### 17.1 Integration principles

- Integration boundaries carry a stable source identity, Tenant, Company,
  scope, event/document type, policy version, currency/rate facts, and
  correlation evidence.
- A missing, late, duplicate, rejected, or unknown external response is
  visible and reconcilable; no silent success is assumed.
- Bank feeds are CSV/import-first in the PRD direction, with APIs only after a
  separate approved integration decision. Payment gateway behavior is not
  assumed for Release 1.
- Saudi tax/e-invoicing integration is country-specific and remains MESP-49;
  this BRD does not name a provider or certify a payload.
- Cross-module Procurement, Inventory, and Sales integrations must preserve
  the source-to-GL chain and never grant authority through a payload ID.

### 17.2 Imports

An import must validate file/source identity, Tenant/Company, row identity,
document number, dates/period, currency/rate, account/tax/mapping facts,
duplicate status, totals, and permitted scope before creating any business
effect. It must provide a preview or validation outcome, error ownership,
replay/retry behavior, and a reconciliation summary. Invalid rows cannot be
silently dropped or partially posted without explicit policy and evidence.

### 17.3 Exports

An export must be requested by an authorized actor for an authorized
Tenant/Company scope, retain report definition/as-of date/currency/status, and
record success/failure and recipient/delivery evidence appropriate to the
approved policy. It cannot cross Tenant or Company scope and cannot bypass
MESP-50 retention/privacy or MESP-48 volume gates.

## 18. Migration and opening-balance requirements

Migration is a business cutover and reconciliation activity, not an excuse to
weaken posted-history controls.

The PRD baseline requires configuration/master data first and opening balances
for inventory, AP, AR, cash, and GL, with reconciliation of quantities,
valuation, customer/supplier balances, trial balance, cash, tax, and document
counts. It also calls for two dry runs, rehearsal, and rollback/recovery
evidence.

MESP-51 remains open and must decide the Wafra migration boundary. The Finance
BRD records the following safe gate:

1. Approve source ownership, scope, legal entity mapping, opening date, and
   historical vs opening-only boundary.
2. Map accounts, customers, suppliers, products/UOM, tax, currencies, bank/cash
   accounts, and inventory valuation evidence without Wafra-specific core
   behavior.
3. Validate duplicate handling, source totals, number non-reuse, period,
   currency/rate, tax, AP, AR, cash, inventory, and GL tie-outs.
4. Produce a preview and exception register; no unresolved material variance
   is hidden by a balancing journal.
5. Execute two dry runs and a cutover rehearsal with named approval and
   rollback/recovery evidence before production data is accepted.
6. Preserve source extracts, mapping versions, opening journals, approvals,
   reconciliation reports, and post-cutover corrections.

Full transaction-history migration, open PO/SO scope, unpaid documents,
attachments, and Wafra-specific conversion behavior remain choices in MESP-51,
not Finance requirements.

## 19. Saudi and localization requirements

Release 1 must support Arabic and English presentation, RTL-aware business
documents, Company time zone/calendar/currency configuration, and localized
numbers/dates/currency without changing the underlying financial facts.

For Saudi validation, the product baseline records Saudi defaults such as SAR
and Asia/Riyadh and a configurable VAT seed. These defaults do not decide legal
applicability, tax registration, invoice language/content, e-invoicing phases,
data residency, banking, or statutory reporting.

The Saudi readiness boundary is:

- preserve Arabic/English and bilingual document requirements as a product
  behavior to validate;
- retain tax rule/version/rate/basis, currency/rate/rounding, invoice identity,
  correction, payload/status/hash or equivalent evidence where a later approved
  e-invoice contract requires it;
- validate the applicable Saudi e-invoicing/ZATCA scope through MESP-49 and a
  qualified Saudi tax/compliance adviser before production commitment;
- keep privacy, residency, retention, legal hold, purge, backup, and restoration
  decisions in MESP-50 with qualified privacy/legal and operations review; and
- complete ADR-011 before localized search, forms, and bilingual business
  document implementation, and preserve ADR-012/013/014/016 production gates;
- keep Finance operational telemetry, exporter access, redaction, and
  operational-data retention bounded by ADR-010;
- keep any Saudi e-invoicing adapter and credential boundary behind qualified
  MESP-49 validation and ADR-015; and
- use ADR-017 only when an approved external partner/API integration requires
  machine authentication, never by reusing first-party browser cookies or
  inventing an integration in this BRD.

No statement in this BRD is legal, tax, banking, or statutory advice.

## 20. Operational readiness and production gates

This document defines business readiness evidence; it does not close
production gates.

Before any production approval, the responsible owners must provide evidence
for:

- Tenant isolation, Company scope, authorization, approval separation,
  immutable audit, correction, concurrency, idempotency, and safe failure;
- supported volume, performance, skew, import/export, and recovery evidence
  under MESP-48;
- retention, privacy, legal hold, purge, residency, backup/restoration, and
  evidence-governance decisions under MESP-50;
- Saudi e-invoicing/tax/compliance validation under MESP-49;
- localization/Arabic/RTL and bilingual document evidence under ADR-011;
- operational telemetry, redaction, controlled access, exporter, and retention
  evidence under ADR-010;
- the isolated Saudi e-invoicing adapter and credential boundary under ADR-015
  after qualified MESP-49 validation; and
- the approved-integration-only external partner/API authentication boundary
  under ADR-017;
- production hosting, availability, RPO/RTO, key/secret, storage, and
  production-like test decisions under ADR-012, ADR-013, ADR-014, ADR-016, and
  ADR-018; and
- implementation readiness for the Finance module, including a reviewed
  contract with Procurement, Inventory, and B2B Sales.

The PRD records 99.9% availability and p95 read/command targets under an
agreed reference load, plus RPO/RTO targets subject to commercial tier. This
BRD carries those as validation targets only; it makes no production claim or
capacity assertion.

## 21. Given / When / Then acceptance scenarios

These scenarios are business acceptance examples for BRD validation. They are
not an implementation test specification. Every scenario assumes the actor is
authorized for the named Tenant, Company, and operation unless the scenario
says otherwise.

### Foundation, scope, and GL

| ID         | Given                                                                                                 | When                                                        | Then                                                                                                                                    |
| ---------- | ----------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| FIN-AC-001 | A Tenant has two active Companies/Legal Entities.                                                     | An accountant opens Finance for Company A.                  | Only Company A books, periods, balances, and scope are available; Company B is not exposed.                                             |
| FIN-AC-002 | A request contains a client-supplied Company or Tenant identifier outside the server-derived context. | The actor submits a Finance command or report.              | The operation is denied and no cross-Tenant or cross-Company data is displayed, changed, searched, exported, or reused.                 |
| FIN-AC-003 | A Chart of Accounts contains a header/inactive account.                                               | A journal uses it for posting.                              | The journal is rejected with an actionable business exception; no partial GL effect exists.                                             |
| FIN-AC-004 | A manual journal has missing source, period, mapping, or required currency/rate facts.                | The accountant submits it.                                  | The journal remains non-posted and the missing-owner exception is visible.                                                              |
| FIN-AC-005 | A journal has unequal debit and credit totals.                                                        | The accountant requests posting.                            | Posting is denied; no subledger or GL effect exists.                                                                                    |
| FIN-AC-006 | A valid journal is approved under the applicable policy.                                              | A separate authorized Finance path posts it once.           | The balanced GL result, source, policy/version, actor, period, currency, and audit evidence are visible.                                |
| FIN-AC-007 | A posted journal contains an error.                                                                   | An authorized accountant corrects it.                       | The original remains unchanged; a linked reversal and corrected entry are created, with reconciliation evidence.                        |
| FIN-AC-008 | A Company period is closed.                                                                           | A normal source or manual journal requests posting into it. | The operation is blocked; only an approved reopen/adjustment path can proceed with reason and audit.                                    |
| FIN-AC-009 | A used account, Company, number boundary, or functional currency is active in posted history.         | An administrator attempts to rewrite ownership or history.  | The rewrite is denied; a prospective or correction path is required.                                                                    |
| FIN-AC-010 | A Tenant has multiple legal entities.                                                                 | A user requests a consolidated statement.                   | Release 1 provides separate Company results and an explicit out-of-scope response; no consolidation or intercompany result is invented. |

### Procurement, AP, Inventory, and supplier settlement

| ID         | Given                                                                | When                                    | Then                                                                                                                              |
| ---------- | -------------------------------------------------------------------- | --------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| FIN-AC-011 | A Purchase Order is approved but no invoice is posted.               | Finance reviews AP/GL.                  | The PO is traceable as a commercial commitment, but no AP liability or cash effect is created solely by the PO.                   |
| FIN-AC-012 | Inventory has posted a Goods Receipt.                                | Finance receives the valuation handoff. | Inventory remains the physical stock authority; the handoff carries source/cost evidence and does not create AP.                  |
| FIN-AC-013 | A supplier invoice is valid and matched under the approved policy.   | Finance posts it.                       | AP and input-tax effects are created once, with source/match/tax/currency/rate/period lineage; stock is unchanged by the invoice. |
| FIN-AC-014 | A supplier invoice has a quantity/value/missing-receipt mismatch.    | Finance attempts normal posting.        | The invoice is held or routed to the named exception owner; no silent auto-post occurs.                                           |
| FIN-AC-015 | A supplier invoice is posted and only part of the amount is paid.    | The payment is allocated.               | Cash/bank and AP effects reflect the payment; the remaining open balance and aging remain visible.                                |
| FIN-AC-016 | A supplier payment request receives no reliable external outcome.    | Finance retries or reconciles it.       | The item remains Unknown/pending until evidence resolves it; it is not marked paid or settled twice.                              |
| FIN-AC-017 | A supplier return has Inventory evidence and a supplier credit note. | Finance processes the correction.       | The stock and AP/credit effects are linked, attributable, and reconciled; neither original posted record is silently erased.      |
| FIN-AC-018 | A duplicate supplier invoice is imported or submitted again.         | Finance validates it.                   | The duplicate is rejected or held with evidence and does not create a second AP/tax/GL effect.                                    |

### AR, receipts, cash, and tax

| ID         | Given                                                                   | When                                             | Then                                                                                                                                                           |
| ---------- | ----------------------------------------------------------------------- | ------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FIN-AC-019 | Sales provides one valid B2B delivery/service milestone.                | Finance receives the approved invoice request.   | Finance validates Company, customer, totals, tax, currency/rate, period, and mapping, then creates one traceable AR/revenue/tax result or a visible exception. |
| FIN-AC-020 | A customer receipt is less than the invoice amount.                     | An authorized collector allocates it.            | The receipt is posted once, partially allocated, and the remaining AR balance and aging remain visible.                                                        |
| FIN-AC-021 | A customer receipt has no eligible invoice.                             | Finance records it.                              | It remains on-account/unapplied or unidentified under policy; no invoice is silently closed.                                                                   |
| FIN-AC-022 | A customer return/credit correction is approved.                        | Finance posts it.                                | The original invoice and related Sales/Inventory evidence remain linked; AR/revenue/tax correction is forward-only.                                            |
| FIN-AC-023 | A bank statement contains a duplicate or conflicting match.             | Treasury reconciles it.                          | The item is held as an exception; no duplicate cash or receipt effect is posted.                                                                               |
| FIN-AC-024 | An approved tax rule has an effective date.                             | A document is posted before and after that date. | Each posting retains the rule/version and basis effective for its date; later configuration does not rewrite the earlier result.                               |
| FIN-AC-025 | A rounding residual occurs during an approved currency/tax calculation. | Finance posts the document.                      | The residual is preserved and routed to the approved rounding treatment; no unexplained imbalance is hidden.                                                   |

### Currency, valuation, reconciliation, and reports

| ID         | Given                                                                            | When                            | Then                                                                                                                                                              |
| ---------- | -------------------------------------------------------------------------------- | ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FIN-AC-026 | A transaction is in a currency different from the Company's functional currency. | Finance posts it.               | Document amount, functional amount, rate, rate date/source, precision, and rounding facts are retained; the result is not converted using an undisclosed default. |
| FIN-AC-027 | The required exchange-rate source or revaluation policy is not approved.         | A posting depends on it.        | The affected operation is held or rejected at the decision gate; no recommendation is silently promoted.                                                          |
| FIN-AC-028 | Inventory provides valuation evidence for a stock movement.                      | Finance reconciles the period.  | The reconciliation identifies source movement, quantity/cost/evidence version, Finance effect, timing, and any variance without duplicating the stock ledger.     |
| FIN-AC-029 | AP/AR subledger totals differ from the GL control account.                       | A reconciliation runs.          | The difference, affected source items, period, owner, and disposition are visible; the totals are not forced to agree by an unexplained journal.                  |
| FIN-AC-030 | A Finance report is requested for one Company and period.                        | The user runs or exports it.    | The report identifies scope, as-of date, currency/view, freshness/status, definition/version, and unreconciled exceptions; another Company/Tenant is excluded.    |
| FIN-AC-031 | A required report or export fails after the posting succeeded.                   | The user checks Finance status. | The posting remains traceable; the delivery failure has its own status, owner, correlation, and retry/recovery evidence.                                          |

### Approval, concurrency, audit, migration, and Saudi gates

| ID         | Given                                                                                                 | When                                                | Then                                                                                                                                                      |
| ---------- | ----------------------------------------------------------------------------------------------------- | --------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| FIN-AC-032 | A user prepares an action and is also the proposed approver where policy prohibits self-approval.     | The approval is attempted.                          | The conflict is denied or rerouted under the approved SoD policy; no self-approval evidence is accepted.                                                  |
| FIN-AC-033 | Two Finance users edit the same journal, payment, period, or reconciliation.                          | The stale user submits after the other decision.    | The stale decision is rejected or re-reviewed; the earlier result is not silently overwritten.                                                            |
| FIN-AC-034 | A source event is retried after a timeout.                                                            | Finance checks existing lineage before applying it. | One source/subledger/GL effect exists, or the item remains visibly pending/unknown; no duplicate effect is created.                                       |
| FIN-AC-035 | A financial fact is posted and later corrected.                                                       | An authorized reviewer inspects history.            | Original, reversal/correction, reason, actors, policy versions, and reconciliation chain are readable and immutable.                                      |
| FIN-AC-036 | A Wafra opening-balance file contains an unresolved material variance.                                | Migration preview is reviewed.                      | Cutover is blocked or quarantined under MESP-51; no balancing entry hides the variance.                                                                   |
| FIN-AC-037 | A migration rehearsal has completed two dry runs and reconciled openings.                             | Finance and Product approve cutover.                | Opening facts, mappings, approvals, rollback/recovery evidence, and post-cutover exceptions are retained; Wafra-specific core behavior is not introduced. |
| FIN-AC-038 | Saudi invoice/tax/e-invoice behavior has not been validated by the named specialist.                  | A production launch decision is requested.          | The affected Saudi scope remains a gate; this BRD does not assert legal, tax, or ZATCA compliance.                                                        |
| FIN-AC-039 | A report/export/audit query is run through Platform governance context without a Tenant Finance path. | The request is evaluated.                           | Tenant Finance data is denied; only purpose-bound platform governance records remain available.                                                           |
| FIN-AC-040 | Retention, residency, purge, backup, or recovery policy is not approved.                              | Finance production readiness is assessed.           | MESP-50 remains open and no retention/purge/residency or production claim is made by this BRD.                                                            |

## 22. Open decisions and deferred gates

### 22.1 Decision discipline

The following rows are a small decision bundle for the named owners. Each row
has a recommendation to make the consequences explicit, but the recommendation
is **not approved**. A decision becomes a requirement only when the named Owner
or qualified specialist records approval in Jira or the immutable Product
Decision Register with scope and effective point.

| Bundle    | Decision / linked Jira                                                                                      | Recommended safe default (not approved)                                                                                                                               | Alternatives that remain open                                                                                                         | Consequence and due point                                                                                                                                              | Decision owner / specialist                                                                              |
| --------- | ----------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| FIN-OD-01 | Posting, account/dimension determination, valuation, correction, period, and source-to-Finance interim policy; Finance impact of MESP-41, 44, 45 | Use explicit, versioned posting rules; require three-way/valuation exceptions to be resolved before affected posting; block negative-stock accounting until approved. | Matching tolerance, interim clearing/accrual accounts, negative-stock treatment, valuation/landed-cost/return/adjustment policy, year-end interaction, or controlled exception path. | Without this, affected P2P, Inventory, and source posting cannot be enabled safely. Decide before Finance implementation and before MESP-35 depends on the foundation. | Finance Controller with Procurement and Inventory concurrence. |
| FIN-OD-02 | Approval and delegation policy; MESP-42, MESP-44, MESP-46, MESP-55                                          | One named approver where required, controlled reassignment with evidence, no self-approval, and no unbounded delegation.                                              | Thresholds, serial/parallel tiers, escalation, expiry, out-of-office delegation, and source-specific approval.                        | Without this, approval boundaries and SoD cannot be made implementation-ready. Decide before workflow implementation.                                                  | Finance Controller/Product Owner with Procurement and Sales owners; specialist review for SoD as needed. |
| FIN-OD-03 | Cash, bank, payment, receipt, and external outcome policy; MESP-47                                          | Manual bank transfer/cash methods first, with explicit pending/unknown/rejected outcomes and reconciliation.                                                          | Cheque lifecycle, card/gateway, bank feed, auto-match, or other methods.                                                              | Determines cash/bank data, integration, reconciliation, and settlement behavior. Decide before cash/payment implementation and production pilot.                       | Treasury / Finance Controller; qualified banking partner where integration is chosen.                    |
| FIN-OD-04 | Functional/transaction/reporting currency contract, Reporting Currency presentation boundary, exchange-rate source, cadence, effective date, override, rounding, conversion evidence, and revaluation; MESP-54 | No Reporting Currency or rate policy is approved by this BRD; preserve Finance monetary facts and require an Owner decision before enabling the affected use. | Additional Release 1 Reporting Currency or no additional Reporting Currency; approved external feed, manual effective-dated rates, override, period-end revaluation, and other conversion/rate-evidence policies. | Determines multi-currency accounting and later reporting consumption; MESP-54 must be decided before multi-currency or Reporting Currency implementation and before MESP-35 money flows. Reporting Currency is not consolidation and does not create second books. | Finance/Treasury and Reporting owners; qualified accounting adviser if revaluation/statutory treatment is selected. |
| FIN-OD-05 | Report catalogue, freshness, reconciliation ownership, saved views, scheduling/export; MESP-53              | Minimum core Finance reports with named owners, explicit freshness/status, and controlled export.                                                                     | Statutory-only, operational catalogue, configurable/saved views, scheduled delivery, and broader reconciliation.                      | Determines Finance acceptance and MESP-36 Reporting handoff. Decide before Reporting BRD implementation.                                                               | Finance Controller, Product Owner, and named report owners.                                              |
| FIN-OD-06 | Saudi e-invoicing/evidence launch scope; MESP-49                                                            | Obtain qualified Saudi validation before committing to a production e-invoice phase; preserve configurable bilingual/tax evidence.                                    | Content/Arabic only, generate/archive, applicable integration phase, or phased pilot.                                                 | Determines external integration, invoice evidence, launch claim, and compliance risk. Decide before MESP-37 / production.                                              | Finance Controller plus qualified Saudi tax/compliance adviser.                                          |
| FIN-OD-07 | Wafra migration and opening balances; MESP-51                                                               | Configuration/masters plus reconciled opening inventory, AP, AR, cash, and GL; no full history until explicitly approved.                                             | Open POs/SOs/unpaid invoices, historical transactions, attachments, or broader scope.                                                 | Determines cutover, reconciliation, rollback, and data-integrity risk. Decide before MESP-40/migration execution.                                                      | Wafra business owner, Finance Controller, Product Owner; migration specialist as needed.                 |
| FIN-OD-08 | Residency, retention, privacy, legal hold, purge, backup/recovery; MESP-50                                  | Keep all production claims deferred until qualified privacy/legal and operations evidence exists.                                                                     | Single region, KSA residency, tiered, or contract-driven policy.                                                                      | Determines production, contracts, audit evidence, and recovery posture. Decide before production and affected data handling.                                           | Data Protection/Compliance, Platform Operations, qualified Saudi legal/privacy adviser.                  |
| FIN-OD-09 | Finance detail bundle: fiscal-year/year-end closing and carry-forward, Payment Term Release 1 shape, and posting-dimension catalogue including Cost Center; MESP-110 | Keep the details open; publish an explicit versioned contract with effective point, historical preservation, reconciliation evidence, and named owner before dependent readiness. | Retained-earnings/closing-entry versus derived-reporting treatment; Payment Term base-date/interval/schedule/installment and early-discount alternatives; dimension catalogue and attribution/reportability choices. | Blocks Finance implementation detail, M95-SL-07 Payment Term readiness, and any MESP-35 money-flow dependency until the accountable owner decides. It does not resolve MESP-54. | Finance Controller / Head of Finance with Product, Procurement, Inventory, and later Sales concurrence. |

### 22.2 Preserved decision register status

As of this BRD session, the live MESP-23 register contains fourteen open rows
among MESP-41 through MESP-55 and two approved decisions:

- **MESP-52 / PD-020 — Approved:** one Release 1 B2B ERP subscription plan per
  Tenant with simple limits/support tier/effective dates; no pricing,
  metered-billing, overage, subscription-invoice/payment/accounting behavior,
  and no per-Tenant entitlement override. This BRD does not add Finance plan
  accounting.
- **MESP-56 / PD-021 — Approved:** a Tenant may contain multiple legal
  entities; each is its own legal/accounting boundary. Release 1 excludes
  consolidation, intercompany automation, elimination entries, transfer
  pricing, and consolidated statements.

MESP-41, MESP-42, MESP-43, MESP-44, MESP-45, MESP-46, MESP-47, MESP-48,
MESP-49, MESP-50, MESP-51, MESP-53, MESP-54, and MESP-55 remain open. This
document records their Finance impact and does not close them. Inventory
decisions are not closed merely because Finance depends on their handoff.

FIN-OD-09 is a new open Finance detail bundle recorded as MESP-110 under the
MESP-23 governance register. It is **To Do**, unapproved, and does not alter
the status or scope of MESP-41 through MESP-56.

### 22.3 Production and architecture gates not resolved here

MESP-48 supported volume/performance, MESP-49 Saudi e-invoicing/external
validation, MESP-50 retention/privacy/legal hold/purge/residency/backup and
restoration, ADR-011 localized search/forms/bilingual document behavior,
ADR-012 production hosting/availability/RPO/RTO, ADR-013 secrets/keys,
ADR-014 retention/purge, ADR-016 isolation review, and ADR-018 production-like
testing remain explicit gates. This BRD does not waive, answer, or activate
any of them.

## 23. BRD acceptance and handoff

This BRD is ready for Owner approval when the following are true:

- all MESP-34 required outputs are present: purpose, actors, triggers,
  preconditions, main/alternative/exception paths, rules, lifecycle, data,
  validation, permissions, approval/SoD, inventory/accounting/currency/Saudi
  impacts, reports/KPIs, audit, integrations, migration, GWT scenarios, and
  open decisions;
- the posting foundation is explicit for Procurement, Inventory, and the later
  B2B Sales source without redefining ownership;
- immutable posted history, reversal/correction, period control, source-to-GL
  lineage, subledger reconciliation, Tenant/Company scope, concurrency,
  idempotency, failure, and unknown outcomes are covered;
- recommendations are visibly labelled and every unresolved policy is named,
  owned, and given a due point;
- MESP-41 through MESP-55 are preserved open except the separately approved
  MESP-52 and MESP-56 records, and MESP-48/49/50 remain gates; and
- no implementation artifact, production claim, legal/tax/banking conclusion,
  or next Jira activation is implied.

The original Owner approval, reviewed content head, focused PR, and MESP-34
closure are recorded in the table below. The bounded independent-review
correction was reviewed and merged after the complete documentation diff passed
validation. The reconciliation links the canonical document, correction PR
merge evidence, Jira validation and MESP-23 handoff comments, updated
repository state/tracker, the open FIN-OD-09 decision, and the separately
prepared but **not executed** MESP-35 next-session handoff. MESP-34 remains
historically Done; this correction does not reopen or redesign its approved
Finance domain.

## 24. Approval record

| Item                  | Evidence                                                                                                 |
| --------------------- | -------------------------------------------------------------------------------------------------------- |
| Entry activation      | MESP-34 Jira comment `10746`; status In Progress.                                                        |
| Owner approval        | MESP-34 Jira comment `10748`; Hossam standing Owner approval for the bounded BRD session.                |
| Reviewed content head | `7d9de5d` — approved requirements head; the later evidence metadata update does not change requirements. |
| Merge/closure         | Original MESP-34 closure: PR #47 merged at `a6f1960e9ae748c9809b6addbfd7e8d7ea510a1b`; final branch head `72aa210d462f783671f1b3b33fcdea4955567b9c`; Jira closure comment `10751`. Independent-review correction MESP-109: PR #50 reviewed at `cf3f6941523551a3d8a0ecdca39256b3e349c6f2` and merged at `cfb17878a0145cb99fc571da211e01dec6a66f28`; live Jira carries the post-merge validation, closure, and MESP-23 handoff evidence. |
| Open decisions        | MESP-23 register; no open decision is silently resolved here.                                            |

**Stop condition:** This document must not be followed in this session by
MESP-35, Currency work, implementation, or any later task. After the MESP-109
correction and MESP-34 closure handoffs are synchronized, stop for ChatGPT
review.
