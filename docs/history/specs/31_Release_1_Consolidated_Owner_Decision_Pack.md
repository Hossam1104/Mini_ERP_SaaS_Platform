# Release 1 Consolidated Owner Decision Pack

**Status:** Approved A/B decision baseline after MESP-116; C1-C9 remain open gates
**Date:** 12 August 2026
**Decision owner:** Hossam (Owner)
**Register owner:** MESP-23 — Open Questions Register
**Related direction:** PD-024 records only the explicit directions in the fast-track brief; this pack does not silently approve its recommendations.

## 1. How to use this pack

This is one consolidated decision pack for the unresolved Release 1
implementation boundaries. It prevents duplicate Jira owners while preserving
the original issue and BRD row as the source of truth. The pack alone does not
close an existing Jira issue; MESP-116 supplied the explicit approval evidence
and bounded Done transitions for the approved A/B rows. A row becomes approved
only when the Owner signs the specific position in append-only governance
evidence.

Before MESP-116, every recommended position below was treated as:

> **NOT APPROVED UNTIL OWNER SIGNS**

MESP-116 now records the Owner's exact approval for A1-A16 and B1-B6. The
row-level approval evidence and the applied review clarifications are
append-only governance facts; C1-C9 remain open gates.

The pack contains **31 canonical entries**:

| Class | Count | Meaning |
|---|---:|---|
| A — Owner-decidable now | 16 | A bounded business default can be selected without external/legal certification; specialist input can refine implementation without changing the declared boundary. |
| B — Specialist/input-dependent but safe to contract-bound | 6 | Owner direction is needed together with Finance, Inventory, Reporting, or migration input; safe contract work may proceed only at an explicitly bounded boundary. |
| C — Production-only/external/legal gates | 9 | Do not resolve by product guesswork. Preserve as gates for volume, privacy/legal, external/statutory, SQL/provider, infrastructure, or production validation. |

MESP-39 is intentionally not a Release 1 decision row. It remains a future
release and is not activated by this pack. MESP-40 remains a Release 1
requirement but is not activated by this pack.

## 2. Approval record

| Field | MESP-116 completion value |
|---|---|
| Owner | Hossam |
| Decision-pack version | 1.0 — 12 August 2026 |
| Owner approval status | **Approved** for A1-A16 and B1-B6 at the exact bounded positions, subject to the applied review clarifications below. |
| Approved rows | **22** — A1-A16 and B1-B6; Class B is a Release 1 product/implementation contract with mandatory specialist validation before production or irreversible accounting/cutover decisions. |
| Rejected/deferred rows | **0** applicable A/B rows. C1-C9 are not approved or closed; they remain production/external/legal/provider/infrastructure gates. |
| Resulting Product Decision | **PD-025-PD-046**, appended in MESP-22 comment `10958` from Owner approval evidence in MESP-116 comment `10957`. |
| MESP-23 effect | Reconciled by comment `10976`; remains **In Progress** as the living register because C gates and their required evidence remain open. |
| First capability handoff | **MESP-117**, To Do/not activated, prepared in Jira comment `10977`; no capability implementation started in MESP-116. |
| Repository evidence | Focused PR #59 reviewed at `8b3f7b61c0128f97aa6a775dec23e623c1fde70e` and merged to `main` at `b58bcaaeb4103c8fbdfb6a1c933c5239e228c5bd`; post-merge state/tracker synchronization is `66183c1`; `main`/`origin/main` synchronized. |

### 2.1 Applied review amendments and clarifications

The Owner approval incorporates the immediately preceding ChatGPT review. The
following constraints are part of every approved row and are not optional
interpretations:

1. Approval is limited to the exact row scope. It does not approve statutory
   compliance, external integrations, production credentials/providers or
   infrastructure, legal conclusions, volume numbers, or wider module scope.
2. A1 keeps Product tracking configuration separate from Inventory operational
   identity, capture, movement, availability, uniqueness, and history; no
   EAN/GS1 or external traceability rule is inferred.
3. A2 is reusable and configuration-led, with server-derived authority, no
   self-approval, explicit stages/returns, delegation expiry, SoD, audit, and
   a controlled-transition block; no thresholds are invented.
4. A3 preserves original/revised supplier values, re-enters approval for
   material changes, and leaves an unconfirmed remainder explicitly pending;
   A4 approves configurable matching/tolerance states but no tolerance values,
   automatic posting, or automatic approval.
5. A5 blocks negative stock by default; no exception role, expiry, or valuation
   bypass is approved by implication. A6 leaves exposure components and
   thresholds to Finance-owned validation and controlled override evidence.
6. A7 is an internal configurable manual payment/receipt catalogue only; it
   does not authorize a gateway, bank feed, provider, credential, or external
   payment integration. A8 delegation is explicit, scoped, time-bounded,
   server-authorized, conflict-checked, and cannot broaden permissions.
7. A9 is Tenant-wide reusable identity with explicit Company/Branch
   applicability and no cross-Tenant sharing. A10 is deterministic,
   effective-dated price precedence with a source snapshot and no promotion
   engine.
8. A11 distinguishes routine authorized mutation from an explicit high-risk
   approval catalogue and invents no generic approval or Draft lifecycle.
   A12/A13 preserve historical references and prohibit destructive delete,
   overlapping versions, silent reactivation, or history rewrite.
9. A14 requires explicit Warehouse-scoped reservations, controlled
   release/reduction, visible partial allocation/backorder, and no delivery
   without available authority. A15 requires linked authorized returns,
   controlled receipt/quarantine, internal Credit Note consequence, and audit;
   no automatic external refund or statutory submission is implied. A16 is
   server-checked invoice eligibility with traceable partial invoicing and no
   client-side bypass.
10. B1-B6 are approved as bounded product/implementation contracts only.
    Finance, Inventory, Reporting, Migration, Security/Audit, and other named
    specialist validation remains mandatory before production acceptance,
    irreversible accounting/valuation/posting, destructive migration,
    cutover, rollback commitment, or production distribution. MESP-40 remains
    unactivated and no migration is executed.
11. Internal Tax/VAT is reusable configuration-led Release 1 capability only;
    it does not authorize ZATCA/FATOORA, statutory interpretation, government
    submission, signing, clearance, certification, external providers,
    credentials, or legal conclusions. C1-C9 remain open exactly as classified.

## 3. Class A — Owner-decidable now

### A1 — MESP-41 / PROC-OD-005 / INV-OD-001 — tracking policy

- **Owner/module and status:** Procurement + Inventory; MESP-41 To Do; open
  tracking decision.
- **Question:** What does Release 1 track, at which master-data/warehouse
  boundary, and how is identity/history enforced?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Support a
  configuration-led, Tenant-safe tracking policy for batch/lot, serial, and
  expiry where the Product/Item and Inventory contract requires it. Product
  stores tracking configuration; Inventory owns operational capture,
  uniqueness, movement, availability, and historical evidence. Do not invent
  EAN/GS1 or external traceability rules.
- **Alternatives:** No tracking; batch/lot only; serial only; external
  provider-driven tracking. Each alternative reduces full-cycle coverage or
  introduces an unapproved external dependency.
- **Impact/dependencies:** Product, Goods Receipt, transfers, counts,
  reservations, returns, valuation, Sales delivery, reporting, migration, and
  audit. MESP-119/128/129/130/141 depend on the contract.
- **Input:** Product, Inventory, Finance valuation, Security/Audit; specialist
  confirmation may refine fields but may not add statutory claims.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-025**, MESP-22 comment `10958`.

### A2 — MESP-42 / PROC-OD-001 / INV-OD-003 / SAL-OD-02 / FIN-OD-02 — approvals

- **Owner/module and status:** Cross-module reusable approval policy; MESP-42
  To Do; open.
- **Question:** Which documents require approval, based on which thresholds,
  and with what delegation, sequencing, expiry, and SoD?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Implement a
  reusable configuration-led approval model by document type, Company/Legal
  Entity, threshold, currency, and effective period. Support sequential and
  parallel stages only where configured, explicit approve/reject/return,
  delegation with expiry and audit, no self-approval, server-derived
  authority, and immutable decision history. A missing approval blocks the
  controlled transition.
- **Alternatives:** Per-module hard-coded approvals; one global approver;
  post-hoc approval; no approval. These weaken reuse, SoD, or auditability.
- **Impact/dependencies:** Purchase Request/PO, Supplier Confirmation, Sales
  Order, credit overrides, Inventory adjustments, Finance journals/payments,
  delegation, audit, and Angular workflow UX. MESP-123/124/126/130/132/133/
  136 depend on it.
- **Input:** Finance, Security/Audit, module owners; specialist review may
  confirm thresholds without changing the security invariant.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-026**, MESP-22 comment `10958`.

### A3 — MESP-43 / PROC-OD-002 / INV-OD-011 — Supplier Confirmation

- **Owner/module and status:** Procurement; MESP-43 To Do; open.
- **Question:** What supplier responses and change states are supported?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Support full
  Confirmation, Rejection, Partial Confirmation, and Change Requested
  outcomes. A material supplier change re-enters the configured approval path;
  original and revised values remain linked and auditable. A partial
  confirmation may create only the confirmed operational obligation while the
  remainder stays explicitly pending/rejected.
- **Alternatives:** Confirmation-only; overwrite the PO; email-only supplier
  response; automatic acceptance. These lose variance and audit evidence.
- **Impact/dependencies:** Purchase Order, Goods Receipt, commitments,
  matching, tax/currency/terms, attachments, permissions, audit, and reports;
  MESP-124/125/126 depend on it.
- **Input:** Procurement, Finance, Inventory, Security/Audit.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-027**, MESP-22 comment `10958`.

### A4 — MESP-44 / PROC-OD-003 / INV-OD-012 / FIN-OD-01 — three-way matching

- **Owner/module and status:** Procurement + Finance; MESP-44 To Do; open.
- **Question:** Which document quantities/amounts are matched, what
  tolerances apply, and who may resolve exceptions?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Use deterministic
  Purchase Order–Goods Receipt–Purchase Invoice matching with configured
  quantity and amount tolerances, explicit no-match/partial-match states,
  retained evidence, and authorized exception resolution. Unresolved
  exceptions remain on hold; no silent auto-posting or auto-approval.
- **Alternatives:** Two-way match; invoice-only posting; unlimited tolerance;
  automatic override. These risk AP and stock integrity.
- **Impact/dependencies:** Supplier Confirmation, receiving, tax, currency,
  Finance source-to-GL, AP, reports, audit, SoD, and MESP-126/133/134.
- **Input:** Finance is the accounting owner; Procurement and Inventory
  define source evidence; Security/Audit validates authority and history.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-028**, MESP-22 comment `10958`.

### A5 — MESP-45 / INV-OD-002 — negative stock

- **Owner/module and status:** Inventory; MESP-45 To Do; open.
- **Question:** May stock fall below zero and under what authority?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Block negative
  stock by default, with the safest Moving Weighted Average and reconciliation
  behavior. A future narrowly scoped, explicitly approved exception would
  require reason, authority, expiry, audit, downstream warning/hold, and no
  silent valuation corruption; no exception is assumed now.
- **Alternatives:** Allow all negative stock; allow by Warehouse; allow by
  role without expiry. Each increases valuation and availability risk.
- **Impact/dependencies:** Reservation, Delivery, Stock Issue, counts,
  returns, MWA, Sales credit/availability, reporting, and migration.
- **Input:** Inventory and Finance must confirm valuation implications;
  Security/Audit confirms exception authority.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-029**, MESP-22 comment `10958`.

### A6 — MESP-46 / PROC-OD-014 / INV-OD-013 — credit control

- **Owner/module and status:** Sales + Finance; MESP-46 To Do; open.
- **Question:** What credit limit, exposure, hold, override, and expiry model
  applies to B2B customers?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Use a full
  customer credit model with configured credit limit, current exposure,
  outstanding due/overdue components, pending orders/reservations where
  approved, check points, hold state, authorized override, expiry, reason, and
  audit. The calculation source and allocation/settlement effects must be
  explicit before posting or release.
- **Alternatives:** No credit control; a single hard-coded limit; manual
  spreadsheet check; block every order. These either lose B2B control or make
  the system unusable.
- **Impact/dependencies:** Customer master, Sales Order/Delivery/Invoice,
  Receipts/allocation, returns/credit notes, AR aging, approvals, SoD,
  reporting, MESP-136/137/138.
- **Input:** Finance owns exposure and settlement; Sales owns operational
  holds; Security/Audit owns override evidence.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-030**, MESP-22 comment `10958`.

### A7 — MESP-47 / PROC-OD-004 / INV-OD-014 / FIN-OD-03 — payment and receipt methods

- **Owner/module and status:** Finance; MESP-47 To Do; open.
- **Question:** Which payment/receipt methods are supported in Release 1?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Support an
  internal, configurable catalogue of manual payment and receipt methods,
  including cash, bank/manual transfer, cheque or other Owner-approved
  internal methods, with account mapping, currency, reference, date, status,
  allocation, settlement, reconciliation, permissions, SoD, and audit. This
  is an internal ERP capability, not a payment gateway or bank-feed
  integration.
- **Alternatives:** Cash-only; external gateway; automated bank feed; free-text
  method. The external options are out of Release 1; free text loses control.
- **Impact/dependencies:** AP/AR, cash/bank, allocation, settlement, credit,
  due dates, reporting, migration, and MESP-133.
- **Input:** Finance and Security/Audit; provider/infrastructure input is
  intentionally deferred.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-031**, MESP-22 comment `10958`.

### A8 — MESP-55 / PROC-OD-013 / INV-OD-003 / FIN-OD-02 / RPT-OD-012 — delegation

- **Owner/module and status:** Cross-module governance; MESP-55 To Do; open.
- **Question:** When and how may authority be delegated?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Use explicit
  named or role-based delegation with scope, start/end time, reason, tenant
  and company boundary, no self-approval, conflict handling, server-derived
  authorization, and complete audit. Delegation must not silently expand
  permissions or survive expiry.
- **Alternatives:** Permanent delegation; implicit manager substitution;
  shared accounts; no delegation. These weaken SoD and audit.
- **Impact/dependencies:** A2 approvals, credit overrides, inventory variance,
  Finance posting/payments, reporting distribution, support access.
- **Input:** Security/Audit, Finance, module owners.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-032**, MESP-22 comment `10958`.

### A9 — MD-OD-001 — remaining Master Data availability

- **Owner/module and status:** Master Data; remaining global/open scope after
  bounded Product/Supplier/Customer slices.
- **Question:** How are master records shared across Companies/Branches while
  remaining Tenant-safe?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Keep reusable
  Tenant-wide master identity inside its owning Tenant, with explicit
  Company/Branch applicability and no cross-Tenant sharing. Operational
  documents snapshot the required reference values for history.
- **Alternatives:** Company-only duplication; Branch-only duplication;
  cross-Tenant shared catalogue. The alternatives reduce reuse or violate
  isolation.
- **Impact/dependencies:** Currency, terms, tax, price lists, Products,
  Suppliers, Customers, imports, downstream APIs, reporting, migration.
- **Input:** Master Data, Platform, Security/Audit.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-033**, MESP-22 comment `10958`.

### A10 — MD-OD-004 / SAL-OD-01 — pricing precedence

- **Owner/module and status:** Master Data + Sales; Price List and Sales open
  rows.
- **Question:** Which price wins when customer, customer group, product,
  currency, quantity, date, and promotion-like conditions overlap?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Use a
  deterministic, effective-dated precedence hierarchy with explicit tie
  breaking, currency and UOM rules, no ambiguous fallback, a visible source
  reference, and an audit snapshot on the Sales document. No unapproved
  promotion engine is implied.
- **Alternatives:** Last edited wins; manual salesperson override; one global
  price; external pricing provider. The alternatives are non-deterministic or
  out of scope.
- **Impact/dependencies:** Price List, Product/UOM, Customer, Sales Order,
  returns/credit, tax, multi-currency, reporting, MESP-121/136/137.
- **Input:** Sales, Master Data, Finance for tax/currency interaction.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-034**, MESP-22 comment `10958`.

### A11 — MD-OD-005 — Master Data approval catalogue

- **Owner/module and status:** Master Data; approval catalogue open beyond
  bounded Product/Supplier/Customer decisions.
- **Question:** Which master-data mutations need permission and audit, and
  which need a separate approver?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Routine
  authorized create/update/deactivate/reactivate actions use permission,
  server-derived authority, and audit. A separate approver is required only
  for the explicit configured catalogue of high-risk changes; no generic
  approval is invented. No Draft lifecycle is assumed where the bounded slice
  says Active/Deactivate/Reactivate.
- **Alternatives:** Approve every mutation; approve none; per-screen implicit
  approval. These create friction or control gaps.
- **Impact/dependencies:** Category/UOM/Product/Supplier/Customer/Currency,
  tax, terms, price lists, import, downstream snapshots, audit, MESP-117/118/
  119/121/122.
- **Input:** Master Data, Security/Audit, Finance for accounting references.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-035**, MESP-22 comment `10958`.

### A12 — MD-OD-008 — remaining lifecycle behavior

- **Owner/module and status:** Master Data; open outside bounded slice-specific
  decisions.
- **Question:** What lifecycle and historical-reference behavior applies to
  master records not covered by existing bounded decisions?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Use explicit
  Active/Inactive or Deactivated state with permission, audit, effective date,
  downstream reference protection, and no destructive delete of referenced
  values. New transactions cannot use an unavailable record; historical
  documents keep their applied snapshot.
- **Alternatives:** Hard delete; universal Draft; immutable forever; silent
  reactivation. Each risks history or operational ambiguity.
- **Impact/dependencies:** All master data, imports, documents, tax, price,
  migration, reporting and audit.
- **Input:** Master Data, Security/Audit, Finance/Inventory/Sales.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-036**, MESP-22 comment `10958`.

### A13 — MD-OD-009 — effective-date reactivation

- **Owner/module and status:** Master Data; effective-dated mutation open.
- **Question:** How do effective dates, reactivation, and historical values
  behave when a master record returns to use?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Use explicit
  effective-from/effective-to values, no overlapping active versions for the
  same scope, audited reactivation, and document snapshots. Reopening a
  record does not rewrite historical documents or previously applied rates.
- **Alternatives:** Immediate global reactivation; overwrite history; permit
  overlapping versions. These make reporting and accounting non-repeatable.
- **Impact/dependencies:** Tax/VAT, currency/rates, price lists, payment
  terms, reports, migration, all consuming documents.
- **Input:** Master Data, Finance, Reporting, Security/Audit.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-037**, MESP-22 comment `10958`.

### A14 — SAL-OD-03 — reservation and partial allocation

- **Owner/module and status:** Sales + Inventory; open.
- **Question:** How do reservations, partial allocation, backorder, expiry,
  and release behave?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Reservations
  are explicit, Tenant/Warehouse scoped, auditable, quantity-aware, and
  released or reduced by controlled events. Partial allocation is supported;
  the unallocated remainder remains a visible backorder/pending quantity and
  cannot be delivered without available authority.
- **Alternatives:** Reserve all order quantity; no reservations; silent
  backorder; allocate at invoice. Each weakens availability truth.
- **Impact/dependencies:** Inventory ledger, negative stock, Sales Order,
  Delivery, returns, credit control, MESP-128/129/137.
- **Input:** Inventory owns stock truth; Sales owns order state; Finance
  confirms credit/payment interaction.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-038**, MESP-22 comment `10958`.

### A15 — SAL-OD-04 — Customer Return, Credit Note, and refund consequence

- **Owner/module and status:** Sales + Inventory + Finance; open.
- **Question:** What validates a return and how does it affect stock, tax,
  credit, invoice, and receipt/allocation?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Support
  authorized Customer Return linked to the originating delivery/invoice where
  available, explicit condition/reason/quantity, controlled stock receipt or
  quarantine, Credit Note with internal tax consequence, allocation/refund
  status, reversals, attachments, and complete audit. No automatic external
  refund or statutory submission is implied.
- **Alternatives:** Free-standing return; invoice-only credit; immediate
  refund; no stock consequence. Each loses lineage or creates financial/stock
  risk.
- **Impact/dependencies:** Inventory return/valuation, Sales invoice,
  Finance AR/tax/reversal, credit exposure, reporting, MESP-127/131/138.
- **Input:** Finance and Inventory are required; Security/Audit validates
  authority and evidence.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-039**, MESP-22 comment `10958`.

### A16 — SAL-OD-05 — invoice eligibility

- **Owner/module and status:** Sales + Finance; open.
- **Question:** What must be true before an invoice may be issued or posted?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Invoice
  eligibility is explicit and server-checked: valid approved order/delivery
  basis as configured, fulfilled quantity rules, customer/terms/currency/tax
  references, credit/approval holds, period, and no unresolved required
  exception. Partial invoicing is supported only with traceable quantity and
  remaining balance. No client-side bypass.
- **Alternatives:** Invoice from quote; invoice on order without delivery;
  manual unrestricted invoice; invoice after cash only. These change revenue,
  stock, and AR integrity.
- **Impact/dependencies:** Sales Order, Delivery, Inventory, Finance AR,
  internal tax, Payment Terms, credit, returns/credit notes, reporting.
- **Input:** Finance owns posting eligibility; Sales and Inventory own source
  documents.
- **Approval:** **Approved** by Hossam in MESP-116 comment `10957`.
- **Resulting PD:** **PD-040**, MESP-22 comment `10958`.

## 4. Class B — specialist/input-dependent but safe to contract-bound

### B1 — MESP-51 / PROC-OD-010 / INV-OD-008 / FIN-OD-07 — migration

- **Question:** What complete data and onboarding contract is required for
  repeatable Release 1 migration?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** MESP-40 must
  cover configuration, master data, opening stock and valuation, GL/AP/AR,
  cash/bank, tax/rate/terms references, validation, quarantine, dry-run,
  reconciliation, cutover, rollback, repeatability, and Tenant onboarding.
  Every imported row has source/error/provenance status; no destructive
  overwrite is implicit.
- **Alternatives:** CSV-only best effort; manual opening balances; one-time
  cutover; skip quarantine/reconciliation. These are not safe full Release 1
  migration contracts.
- **Impact/dependencies:** Every module, MESP-40, MESP-141, SQL/provider,
  volume, backup/restore, accounting and stock reconciliation.
- **Input:** Finance, Inventory, Platform, Security/Audit, migration and
  production specialists. Contract can be prepared without running migration.
- **Approval:** **Approved contract-bound** by Hossam in MESP-116 comment `10957`; named specialist validation remains mandatory.
- **Resulting PD:** **PD-041**, MESP-22 comment `10958`.

### B2 — MESP-53 / PROC-OD-011 / INV-OD-009 / FIN-OD-05 / RPT-OD-001–003 — report catalogue

- **Question:** Which reports, owners, formulas, freshness, filters, exports,
  bilingual views, and schedules are in Release 1?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Keep the full
  catalogue named by the Release 1 scope: Finance, AR, AP, trial balance,
  stock, valuation, purchasing, sales, reconciliation, audit, security, and
  operational reporting. Each report has source ownership, formula/lineage,
  freshness, authorized filters, Arabic/English presentation, export rules,
  and audit. Scheduled distribution is included only if the final Owner
  decision approves its production boundary.
- **Alternatives:** Dashboard-only; module-local reports; ad hoc SQL; export
  without authorization; schedule everything. These lose lineage or create
  security/production risk.
- **Impact/dependencies:** All source modules, Finance/Inventory correctness,
  Tax/VAT, FX/Reporting Currency, aging, Payment Terms, dimensions,
  MESP-139, MESP-48/50/production scheduling gates.
- **Input:** Reporting owner, Finance, Inventory, Security/Audit; volume and
  production specialists for schedule/export limits.
- **Approval:** **Approved contract-bound** by Hossam in MESP-116 comment `10957`; named specialist validation remains mandatory.
- **Resulting PD:** **PD-042**, MESP-22 comment `10958`.

### B3 — MESP-54 / PROC-OD-012 / INV-OD-010 / FIN-OD-04 / RPT-OD-004 — currency and FX

- **Question:** What internal multi-currency and FX behavior is required
  without external rate providers?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Support a full
  manually configured internal model: transaction currency, base/functional
  currency, Reporting Currency, effective-dated rates and source notes,
  realized and unrealized FX, revaluation, rounding, historical applied-rate
  evidence, reconciliation, and authorized overrides. No automated external
  FX feed is in Release 1.
- **Alternatives:** Single SAR only; manual transaction conversion without
  history; external rate provider; no revaluation. The external option is
  deferred and the others do not meet full-feature Finance/reporting needs.
- **Impact/dependencies:** Currency master, Finance posting, AP/AR, tax,
  valuation, reports, migration, rounding, period/year-end and MESP-120/131/
  134/135.
- **Input:** Finance and Reporting; legal/external validation is not replaced
  by this internal contract.
- **Approval:** **Approved contract-bound** by Hossam in MESP-116 comment `10957`; Finance/Reporting validation remains mandatory.
- **Resulting PD:** **PD-043**, MESP-22 comment `10958`.

### B4 — MESP-110 / FIN-OD-09 / RPT-OD-005–006 — fiscal calendar, terms, dimensions

- **Question:** What fiscal/year-end, Payment Terms, due-date/aging, Cost
  Center, and posting-dimension rules are required?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Support a
  configurable fiscal calendar and period lifecycle, controlled reopen/reclose
  and year-end/retained-earnings behavior; richer Payment Terms than a simple
  date-plus-N model where approved (due dates, installments, grace/discount or
  other explicit components); and Cost Center/posting dimensions that flow
  from source documents to GL and reports. No local legal tax conclusion is
  implied.
- **Alternatives:** Calendar-year only; date-plus-N only; free-text terms;
  report-only dimensions; permanent period lock. Alternatives either cut
  full Finance scope or weaken auditability.
- **Impact/dependencies:** Finance posting/periods, AP/AR, credit, aging,
  reporting, migration, approvals, MESP-132/133/135/139/141.
- **Input:** Finance owner, Reporting, Security/Audit, migration specialist.
- **Approval:** **Approved contract-bound** by Hossam in MESP-116 comment `10957`; Finance/Reporting/Migration validation remains mandatory.
- **Resulting PD:** **PD-044**, MESP-22 comment `10958`.

### B5 — MESP-113 / INV-OD-004 — transfers, counts, and Stock Issue

- **Question:** How do Warehouse Transfer, In Transit, count windows/variance,
  and Stock Issue work under Inventory authority?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Support explicit
  Warehouse Transfer with optional In Transit state, receipt/shortage,
  cancellation and audit; count snapshots with cutoff, recount, variance
  reason and approval; and Stock Issue with reason, destination/use, quantity,
  authority, valuation, and audit. No silent balance adjustment.
- **Alternatives:** Immediate transfer only; count overwrites balance; free-form
  issue; Finance-owned stock correction. These weaken ledger truth and SoD.
- **Impact/dependencies:** Inventory ledger, availability, MWA, tracking,
  Procurement/Sales returns, Finance valuation, reports, MESP-128–131.
- **Input:** Inventory is durable owner; Finance confirms valuation; Security/
  Audit confirms authority; Reporting confirms evidence.
- **Approval:** **Approved contract-bound** by Hossam in MESP-116 comment `10957`; Inventory/Finance/Reporting/Audit validation remains mandatory.
- **Resulting PD:** **PD-045**, MESP-22 comment `10958`.

### B6 — FIN-OD-01 and related Finance rows — posting, valuation, corrections

- **Question:** Which Finance source-of-truth, account mapping, correction,
  valuation, and reconciliation rules govern module handoffs?
- **Recommended position — NOT APPROVED UNTIL OWNER SIGNS:** Finance owns
  balanced journals, source-to-GL mapping, account/period validation,
  subledger reconciliation, inventory valuation handoff, controlled correction
  and reversal, and auditable posting evidence. Operational modules own their
  documents and may not fabricate accounting entries outside the approved
  Finance contract.
- **Alternatives:** Module-local posting; unbalanced provisional journals;
  direct database adjustments; manual reconciliation only. These are unsafe.
- **Impact/dependencies:** Procurement matching, Inventory MWA/returns,
  Sales invoices/credits, tax, FX, AP/AR, periods, reports, migration,
  MESP-125/126/131–135/138/141.
- **Input:** Finance is required; Security/Audit, Inventory, Procurement,
  Sales, Reporting, and migration provide source evidence.
- **Approval:** **Approved contract-bound** by Hossam in MESP-116 comment `10957`; Finance and named specialist validation remains mandatory.
- **Resulting PD:** **PD-046**, MESP-22 comment `10958`.

## 5. Class C — production-only, external, or legal gates

These entries are intentionally not resolved by a product recommendation.
They remain gates and are not implementation blockers for safe local work when
the capability has a bounded contract.

### C1 — MESP-48 — supported volume and production governance

Reference volumes and concurrency must come from validated evidence. No
number is fabricated in this pack. Load, capacity, availability, monitoring,
and production governance are later gates.

### C2 — MESP-49 / MD-OD-007 — statutory/e-invoice and Saudi external boundary

MESP-49 remains Done for its existing Release 1 external statutory/e-invoice
disposition. ZATCA/FATOORA, government submission, signing, clearance,
certification, taxpayer applicability, and legal compliance remain outside
Release 1. Internal Tax/VAT is restored by PD-024 and docs/32 without changing
this gate.

### C3 — MESP-50 — privacy, retention, legal hold, purge, residency, backup/restore

These production/legal decisions remain open. No privacy/legal certification,
retention claim, purge implementation, residency promise, or backup/DR claim
is inferred from this plan.

### C4 — ADR-010 — production data/volume or provider boundary

The applicable ADR gate remains authoritative. Local contracts may be built
only where the approved boundary is clear; production provider/volume claims
wait for its decision and evidence.

### C5 — ADR-013 — external/provider or deployment boundary

No external provider, credential, integration, deployment, or environment
configuration is introduced by the fast-track plan. The ADR remains a later
production gate.

### C6 — ADR-014 — production infrastructure and operational readiness

Infrastructure, observability, backup, restore, availability, and deployment
acceptance remain gated. A local build or preview is not production evidence.

### C7 — ADR-016 — SQL/database production validation

SQL Server behavior, migration safety, indexes, concurrency, backup/restore,
and production data validation remain subject to the SQL gate. The executor
must report unavailable local SQL tooling honestly.

### C8 — ADR-017 — legal/external validation and country-pack boundary

Generic Saudi-oriented configuration and bilingual localization remain in
scope; statutory, legal, and externally validated country behavior remains
gated. No country-specific hard coding is authorized.

### C9 — provider, credential, infrastructure, and external legal validation

This consolidated gate covers any remaining production-only external service,
credential, provider, infrastructure, privacy/legal, or regulatory validation
needed for a later release. It does not activate MESP-39.

**Approval for C1–C9:** No product approval is requested in this pack;
preserve as gates and re-open at the appropriate production/RC checkpoint.

## 6. Source and traceability inventory

The pack was assembled from the current source rows without deleting or
duplicating their Jira owners:

- Procurement open rows `PROC-OD-001–014`, with MESP-41–MESP-55 mapping and
  approved PD-020/PD-021 rows preserved.
- Inventory open rows `INV-OD-001–014`, including durable MESP-113 ownership of
  `INV-OD-004`; approved rows remain unchanged.
- Finance `FIN-OD-01–09`, including MESP-110 and the external statutory,
  migration, residency, and reporting gates.
- Sales `SAL-OD-01–05`, including pricing, approval, reservation/partial
  allocation, return, and invoice eligibility.
- Reporting `RPT-OD-001–014`, including catalogue, lineage, freshness,
  currency, fiscal/terms/aging, dimensions, delegation, migration, provider,
  and production rows.
- Master Data `MD-OD-001–011`, with bounded Product/Supplier/Customer
  decisions remaining scoped to those slices and no silent globalization.
- Approved MESP-38 Security/Audit/Governance requirements and named ADR/SQL,
  provider, privacy, volume, and legal gates.

The pack does not replace an approved BRD or activate implementation. The
approved A/B rows are bounded contracts, not permission to skip specialist,
security, accounting, stock, migration, SQL, provider, legal, or production
validation. In this dated 12 August 2026 approval-baseline snapshot,
MESP-117–MESP-142 remained To Do/not activated; MESP-117 was the prepared
first handoff and its implementation started only in a fresh later session.
This statement is historical; the current repository-health authority is the
MESP-144 checkpoint above.

## 7. MESP-116 completion record

MESP-116 completed the following bounded actions:

1. read this pack, the full-feature plan, Tax/VAT clarification, current
   state, relevant BRDs/ADRs, and live Jira;
2. record explicit Owner approval for A1-A16 and B1-B6 in MESP-116 comment
   `10957`, with the review amendments and specialist conditions preserved;
3. append PD-025-PD-046 in MESP-22 comment `10958` only for those explicit
   decisions;
4. reconcile MESP-23 in comment `10976` and update original decision owners
   without creating duplicates; approved open owner issues are closed at
   their exact scopes, while gates remain open;
5. publish the final dependency map in
   `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md` and
   select MESP-117 as the first capability handoff;
6. keep MESP-39 future-release/unactivated and MESP-40 Release-1-required but
   unactivated; and
7. stop with a fresh exact TASK handoff. No implementation starts in this
   decision-reconciliation session.
