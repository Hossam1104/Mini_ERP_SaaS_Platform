# Mini ERP SaaS Platform - Procurement and Purchase-to-Pay BRD

> **Current bounded session - 10 August 2026.** This document is the
> documentation-only MESP-32 business-requirements baseline. It contains no
> source implementation, API contract, database/schema design, migration
> script, user-interface specification, provider decision, or production
> readiness claim. MESP-33 and later domain work are not started by this
> document.

> **Implementation reconciliation - MESP-124, 19 August 2026.** The approved
> business baseline above is preserved. The bounded MESP-124 source slice
> (Purchase Orders and manual Supplier Confirmation) is complete, independently
> reviewed by Claude Opus 5 (APPROVE FOR MERGE), and squash-merged to `main` at
> commit `c742d9c897edb715c7e3c25df7e9ca2c4f30d1e6` (PR #68). It implements
> Purchase Order and manual Supplier Confirmation behavior against the approved
> MESP-123 sourcing chain: approved PR, submitted quotation, current Source
> Decision, immutable commercial snapshots, reusable approval/SoD/delegation,
> issue evidence, full/partial/rejected/no-response confirmation, supplier-proposed
> change records, controlled reapproval, lifetime Tenant-scoped source decision
> uniqueness consumption, durable idempotent replay, history, audit, and
> Tenant/Company/Branch scope. The planned next candidate is MESP-125 (Goods
> Receipt and Purchase Invoice handoff), which is currently In Progress / activated
> under Epic MESP-7 (FIN-OD-01 resolved contract-bound under MESP-116 / PD-046).
> Goods Receipt, stock, invoice, AP, payment, accounting, and three-way matching
> remain downstream and are not implemented by MESP-124.

## 1. Document Control

| Field | Value |
|---|---|
| Document | Procurement and Purchase-to-Pay Business Requirements Document |
| Jira | MESP-32 - Produce Procurement and Purchase-to-Pay BRD |
| Parent Epic | `MESP-7 - EPIC 07 - Procurement and Purchase-to-Pay` |
| BRD sequence | Position 7 of 15, confirmed by MESP-25 comment `10057` |
| Version | v0.1 - Approved Business Baseline |
| Status | **Approved Business Baseline.** This document is a business baseline only and does not authorize source implementation by itself. |
| Accountable Owner | Hossam, Product Owner and founder approver; Finance, Procurement, Inventory, Security, Reporting, and Saudi validation owners are consulted within their decision boundaries |
| Prepared for | Release 1 B2B ERP |
| Date | 10 August 2026 |
| Canonical PRD | `docs/MESP_PRD_v1.2.docx`, PRD v1.2 Final Approved Baseline, approved 31 July 2026 |
| Primary PRD anchors | `PROC-001` through `PROC-008`; `BR-005` |
| Required glossary | `docs/00_ERP_Business_Glossary.md` |
| Related approved BRDs | `docs/11_SaaS_Platform_Administration_BRD.md`; `docs/12_Identity_and_Access_BRD.md`; `docs/13_Multi_Tenancy_BRD.md`; `docs/14_Organization_and_Company_Structure_BRD.md`; `docs/16_Master_Data_and_Product_Catalog_BRD.md` |
| Decision register | MESP-23 and its Jira-decomposed rows MESP-41 through MESP-56 |
| Jira approval evidence | MESP-32 comment `10739` |
| Approved reviewed content head | `5e4e2122fe3346af96a90a7152602410769f0cf9` on draft PR #45 |
| Delivery reference | `docs/94_Product_Delivery_Master_Plan.md` |
| Architecture references | `docs/Decisions.md`; ADR-002, ADR-004, ADR-006, ADR-007, ADR-008, ADR-009, and ADR-018; constraint references only |

### 1.1 Classification legend

| Classification | Meaning in this BRD |
|---|---|
| **Confirmed baseline** | Directly supported by the approved PRD, approved glossary, an approved upstream BRD, or an explicitly approved Jira decision. |
| **Open decision** | The named Owner decision is still open in Jira. The BRD records the required policy branch and the implementation gate without choosing an option. |
| **Deferred gate** | The requirement is understood but must be validated before a later implementation, production, migration, or external launch gate. |
| **Recommended default - not approved** | A recommendation preserved for decision-making only. It is not a requirement, acceptance criterion, or implementation instruction. |
| **External validation** | A qualified external, legal, tax, banking, Saudi, privacy, or business validation is required. This BRD makes no statutory conclusion. |
| **Out of scope** | Release 1 does not include the behavior in this BRD. |

The Founder Decision Pack is not an approval catalogue. Recommendations from
`docs/90_MVP_Founder_Decision_Pack.md` are repeated only when labelled
**Recommended default - not approved**. The only broadly approved rows in the
current MESP-23 register are MESP-52 / PD-020 and MESP-56 / PD-021; neither
silently answers Procurement policy.

## 2. Executive Summary

Release 1 Procurement and Purchase-to-Pay provides a controlled B2B business
process from internal demand through supplier sourcing, purchase order,
manually recorded supplier response, physical receipt, supplier invoice,
payment, reconciliation, and supplier return. The process must preserve the
relationship between the commercial commitment, the physical stock event, the
financial claim, the settlement, and any correction or return.

The central business invariant is:

> A purchase commitment does not create stock; a goods receipt creates stock
> only when the owning Inventory process posts the accepted quantity; a
> purchase invoice creates the supplier obligation and tax/accounting evidence;
> payment settles that obligation; and no posted document may be silently
> edited or disconnected from its source, reversal, return, credit, or payment.

Procurement is the process owner for the commercial chain and exception
coordination. Inventory owns the physical receipt, stock ledger, tracking, and
supplier-return stock movement. Finance owns invoice accounting, AP, tax,
currency/rate policy, payment, posting, fiscal-period control, and financial
reconciliation. Suppliers are external business parties and never become
platform Users, Tenant members, login holders, credential holders, or session
participants through this process.

The BRD is intentionally decision-neutral where MESP-42, MESP-43, MESP-44,
MESP-47, MESP-54, or another open row controls a policy choice. It defines the
required business capabilities and control points while leaving the named
Owner to choose the policy before the affected implementation specification or
production gate.

## 3. Business Purpose and Outcomes

### 3.1 Purpose

The purpose of this module is to give each Tenant's authorized Companies,
Branches, Warehouses, Procurement users, and Finance users a traceable way to:

1. express and approve a purchasing need;
2. source and compare supplier offers when the approved policy requires it;
3. issue and control a purchase order;
4. record a supplier's external response without pretending that the supplier
   used the platform;
5. receive goods through the Inventory-owned posting boundary;
6. match and account for supplier invoices through Finance;
7. settle and reconcile supplier liabilities through Finance; and
8. return accepted goods with linked stock, commercial, and financial evidence.

### 3.2 Intended outcomes

The Release 1 business outcome is a reliable source-to-settlement chain with:

- clear ownership at every handoff;
- no stock, AP, tax, or payment side effect at the wrong document stage;
- policy-controlled approvals, separation of duties, delegation, and
  exception handling;
- partial ordering, confirmation, receipt, invoicing, payment, cancellation,
  rejection, return, and controlled reopening;
- immutable history and evidence sufficient to explain who did what, in which
  Tenant and organizational scope, against which version of the business
  policy; and
- operational reporting that can reconcile commitments, receipts, invoices,
  payments, returns, and exceptions.

## 4. Scope

### 4.1 In scope

This BRD covers the Release 1 B2B business requirements for:

- Purchase Request creation, review, approval, rejection, cancellation,
  revision, and closure;
- supplier quotation capture and comparison by authorized purchasing users;
- Purchase Order creation, approval, issue, supplier response recording,
  partial fulfillment, cancellation, and closure;
- manual Supplier Confirmation recording, including confirmed, partially
  confirmed, rejected, and no-response outcomes;
- Goods Receipt coordination, including partial accepted and rejected
  quantities, with the stock-posting boundary owned by Inventory;
- Purchase Invoice capture, matching, exception routing, approval, posting,
  and correction, with AP and accounting owned by Finance;
- Supplier Payment request, approval, execution evidence, allocation,
  settlement, and reconciliation, with policy owned by Finance;
- supplier returns of previously accepted goods and the linked credit or
  correction evidence;
- document lifecycle and status transition requirements;
- business data, validation, permissions, approval controls, separation of
  duties, concurrency, idempotency, audit, and immutable-history requirements;
- Inventory, Finance, Tax, Currency, Supplier, Product, Unit of Measure,
  Organization, Reporting, Notifications, Integration, and Migration
  boundaries required to make the process complete; and
- business-level acceptance scenarios and unresolved decision traceability.

### 4.2 Required process chain

The normal chain is:

`Purchase Request -> approved demand -> quotation/source decision -> Purchase Order -> manually recorded Supplier Confirmation -> Goods Receipt -> Purchase Invoice and match -> Supplier Payment and reconciliation`

Supplier return is a controlled branch from accepted receipt and is linked to
the original order, receipt, stock movement, invoice or credit note, and
payment allocation as applicable.

### 4.3 Out of scope

The following are not requirements of this BRD:

- Retail POS, consumer checkout, cash-register behavior, or Wafra-specific
  core behavior;
- supplier login, supplier membership, supplier credentials, supplier
  sessions, supplier portal identity, or treating a supplier as a User;
- a unified Party model, Customer/AR behavior, B2B credit policy, or sales
  order-to-cash;
- deciding Product/Item, SKU, Barcode, Category, UOM, Tax, Payment Term,
  Currency, or Exchange Rate master-data identity owned by MESP-31;
- choosing the open MESP-41 tracking policy, MESP-42 approval catalogue,
  MESP-43 confirmation rule, MESP-44 matching tolerance, MESP-45 negative
  stock rule, MESP-47 payment method catalogue, MESP-48 supported volume,
  MESP-49 Saudi e-invoicing production position, MESP-50 retention/legal hold,
  MESP-51 migration scope, MESP-53 report catalogue, MESP-54 rate source, or
  MESP-55 delegation policy;
- legal, tax, VAT, ZATCA, banking, statutory, privacy, or Saudi compliance
  conclusions;
- source code, endpoint/API design, database tables, EF models, migrations,
  UI screens, automated test design, deployment, provider, infrastructure,
  performance certification, or production operations implementation;
- resolving the open ADR-011 Arabic linguistic/search/form decision;
- inventing posting accounts, tax calculations, exchange-rate sourcing,
  statutory numbering, or reconciliation formulas owned by Finance or a
  country pack; or
- automatic activation of MESP-33, MESP-34, or any later Jira item.

## 5. Source Traceability

### 5.1 Primary PRD anchors

| Trace ID | Approved baseline | Requirement carried into this BRD |
|---|---|---|
| PROC-001 | Purchase Requests | Capture demand, business purpose, item/quantity, need-by date, organizational location, cost-center information where applicable, attachments, justification, and policy-controlled approval. |
| PROC-002 | Supplier Quotations | Allow an authorized buyer to record supplier offers, validity, price, currency, tax, delivery terms, and evidence, then compare qualified offers. |
| PROC-003 | Purchase Orders | Convert approved demand into one or more supplier-specific orders with ship-to Warehouse, terms, expected dates, taxes, discounts, and approval. |
| PROC-004 | Partial Receipt | Support all or part of an order, accepted/rejected quantities, delivery evidence, and tracking details where the approved Inventory policy enables them. |
| PROC-005 | Three-Way Control | Match the supplier invoice with Purchase Order and Goods Receipt evidence and route exceptions under approved tolerances. |
| PROC-006 | Returns and Cancellation | Cancel eligible open quantity and return received goods while preserving links to order, receipt, stock, accounting, and credit evidence. |
| PROC-007 | Supplier Visibility | Provide authorized visibility into open commitments, delivery performance, invoice exceptions, supplier spend, balances, and history. |
| PROC-008 | Supplier Confirmation | Permit an authorized purchasing user to manually record external supplier response, partial quantities, expected delivery, supplier reference, contact, notes, and attachments. |
| BR-005 | Must | Complete purchase-to-pay from request, purchase order, and manually recorded supplier confirmation through goods receipt, supplier invoice, payment, and reconciliation. |

### 5.2 Supporting baseline traceability

| Area | Supporting anchors and boundary |
|---|---|
| Platform and tenancy | `PLT-001`, `PLT-002`, `PLT-003`, `PLT-004` through `PLT-010`: Tenant isolation, organization scope, document identity, attachments, notifications, audit, search/export, and idempotent business commands. |
| Finance | `FIN-001`, `FIN-003`, `FIN-004`, `FIN-007`, `FIN-008`, `FIN-010`, and `FIN-011`: chart/accounting lifecycle, AP, tax, fiscal periods, currency, and Finance-owned scope. |
| Inventory | The PRD Inventory anchors and MESP-33: receipt posting, stock ledger, valuation, tracking, and supplier-return stock movement remain Inventory-owned. |
| Saudi and localization | `KSA-001` through `KSA-008`: SAR and Riyadh defaults, configurable tax/country pack, bilingual document facts, RTL/localization, PDPL/residency gates, and country-pack evidence. No legal conclusion is made here. |
| Business rules | `RULE-003` through `RULE-018`, especially no stock at PO, stock only at posted receipt, invoice creates AP not stock, posted correction by reversal/return, source trace, policy-version reproducibility, currency facts, and no POS. |
| Approved upstream BRDs | Platform administration, Identity and Access, Multi-Tenancy, Organization and Company Structure, and Master Data/Product Catalog BRDs are reused for ownership, scope, access, audit, supplier, product, UOM, tax, payment-term, currency, and exchange-rate boundaries. |

### 5.3 Requirement traceability convention

Each requirement in this document has a stable `PROC-BR`, `PROC-DATA`,
`PROC-VR`, `PROC-AC`, or `PROC-OD` identifier. A future implementation
specification must preserve the identifier, cite the decision or upstream
contract it depends on, and identify its acceptance evidence. A Jira issue,
ticket count, or recommendation is not itself acceptance evidence.

## 6. Actors and Responsibilities

| Actor | Business responsibility | Explicit boundary |
|---|---|---|
| Requester | Describes the need, quantity, need-by date, business purpose, branch/location, and supporting evidence. | Cannot create supplier commitments or financial postings merely by requesting. |
| Buyer / Procurement user | Sources suppliers, records quotations, prepares and issues approved orders, records external Supplier Confirmation, monitors delivery, and coordinates exceptions. | Does not impersonate a supplier or post stock/AP/payment. |
| Supplier | External party that communicates an offer, confirmation, delivery, invoice, or return agreement outside the platform's user identity boundary. | No User, login, credential, Tenant membership, session, or supplier self-service is assumed. |
| Approver | Reviews a request, quote decision, order, invoice, return, or payment according to the approved policy and authority. | Exact threshold, hierarchy, self-approval, delegation, and escalation rules remain MESP-42/MESP-55 decisions. |
| Warehouse operator | Checks delivered goods and records received, rejected, damaged, or outstanding quantities and evidence. | Inventory owns the physical receipt and stock-posting rules; a warehouse operator cannot silently alter a posted ledger. |
| Finance / AP accountant | Records and validates supplier invoices, performs match control, resolves or routes financial exceptions, posts AP/tax evidence, and maintains liability history. | Does not change procurement evidence or bypass receipt/match controls without an authorized exception. |
| Treasury / payment operator | Prepares, executes, allocates, and reconciles supplier payments under Finance policy. | Payment methods and banking boundaries remain MESP-47 and external validation. |
| Inventory owner | Defines stock, tracking, valuation, negative-stock, receipt, reversal, and supplier-return behavior. | MESP-33 and MESP-41/MESP-45 remain authoritative for those decisions. |
| Finance owner | Defines posting matrix, fiscal periods, tax treatment, payment methods, exchange rates, and financial reconciliation. | This BRD does not invent GL accounts, statutory rules, or rate sources. |
| Tenant administrator | Maintains authorized Tenant-level policy and membership within the scope approved by IAM and the domain owner. | Cannot widen Tenant or Company scope through client-supplied values. |
| Auditor / security reviewer | Reviews immutable audit evidence, approval history, exceptions, reversals, and access decisions. | Read access remains Tenant and resource scoped. |
| Reporting owner | Defines report meaning, reconciliation ownership, freshness, and retention expectations. | The report catalogue remains MESP-53; this BRD proposes operational coverage without closing it. |
| Migration owner | Owns source-data mapping, cleansing, reconciliation, sign-off, and cutover evidence if migration is approved. | MESP-51 remains open; migration is not authorized by this BRD. |

## 7. Controlled Terminology

The glossary is authoritative. The following terms are restated because their
distinction prevents accounting and stock errors.

| Term | Meaning and control boundary |
|---|---|
| Supplier | External business master party. A supplier is not a User or Tenant member. |
| Purchase Request | Internal demand signal. It does not create a supplier commitment, stock, AP, or payment. |
| Supplier Quotation | Offer recorded by an authorized buyer from an external supplier. It is not a supplier login or a posted financial event. |
| Purchase Order | Authorized commercial commitment to a supplier. It does not create stock or AP liability. |
| Supplier Confirmation | Manually recorded external response to a Purchase Order. It records intent and expected delivery only; it does not create stock, AP, invoice, or payment. |
| Goods Receipt | Evidence that delivered goods were checked. Inventory owns the posted stock event and accepted quantity. |
| Purchase Invoice | Supplier financial claim recorded by Finance. It creates the AP/tax/accounting obligation when posted; it does not increase stock quantity. |
| Supplier Payment | Finance-controlled settlement or allocation against AP. It does not create stock. |
| Supplier Return | Authorized return of previously accepted goods, linked to the original receipt and any credit or correction. |
| Three-Way Match | Comparison of Purchase Order, Goods Receipt, and Purchase Invoice evidence. Exact tolerance and exception policy is MESP-44. |
| Posted | A controlled business event whose history is immutable except through linked reversal, credit, debit, return, or adjustment. |
| Reopen | A controlled resumption of an unposted or otherwise eligible document under an approved policy. It is never a silent rewrite of a posted event. |
| Tenant / Company / Branch / Warehouse | Tenant is the isolation boundary; Company is the legal/accounting boundary; Branch is the operating subdivision; Warehouse is the stock location. |

## 8. Ownership Boundaries and Business Invariants

### 8.1 Domain ownership matrix

| Capability | Procurement | Inventory | Finance | Master Data / Platform |
|---|---:|---:|---:|---:|
| Request, quotation, PO, supplier response | **Owns** | Consulted | Consulted | Reuses active master records |
| Receipt quantity and acceptance | Coordinates | **Owns** | Consulted | Reuses Product/UOM/Warehouse |
| Stock ledger and valuation | No | **Owns** | Consulted | Reuses Product/Category/UOM |
| Purchase invoice and AP liability | Coordinates source match | Consulted for receipt | **Owns** | Reuses Tax/Currency/Payment Terms |
| Payment and settlement | Supplies source context | No | **Owns** | Reuses Supplier/Currency |
| Supplier return stock movement | Coordinates commercial reason | **Owns** | Owns credit/posting consequence | Reuses active masters |
| Approval catalogue and authority | Uses approved policy | Uses for owned actions | Uses for owned actions | IAM provides access primitives |
| Audit and evidence | Owns process evidence | Owns receipt/stock evidence | Owns financial evidence | Platform owns immutable audit boundary |

### 8.2 Non-negotiable invariants

1. All records, attachments, searches, jobs, exports, reports, and audit views
   are Tenant-scoped and resource-authorized by the server-side business
   authority.
2. A Purchase Order and Supplier Confirmation represent commitment or expected
   delivery only. Neither creates inventory quantity, AP liability, tax, or
   payment.
3. Only Inventory's authorized posted Goods Receipt or another explicitly
   approved Inventory event changes stock quantity.
4. A Purchase Invoice is not a receipt. It creates a supplier claim and
   financial evidence only when Finance posts it.
5. A Supplier Payment settles or allocates AP only when Finance authorizes and
   posts it. It cannot conceal an unmatched or unapproved invoice.
6. Posted facts are immutable. Correction uses a linked reversal, return,
   credit/debit note, or adjustment with a reason and audit trail.
7. Every downstream event retains a link to its source document, source line,
   actor, Tenant, Company/Branch/Warehouse scope, policy/version evidence,
   correlation, and correction chain.
8. A repeated command must not create a second stock, AP, payment, or return
   effect within the same business idempotency scope.
9. An inactive or unauthorized Supplier, Product, UOM, Tax, Payment Term,
   Currency, Company, Branch, Warehouse, or policy cannot be used for new
   work, while historical references remain readable to authorized users.
10. No Wafra-specific field, rule, code path, supplier behavior, or master
    record is required by this reusable baseline.

## 9. Business Process Requirements

### 9.1 Purchase Request

**Trigger:** An authorized requester identifies a business need.

**Preconditions:** The requester is authorized in the current Tenant and
Company/Branch scope; referenced Product, UOM, Company, Branch, Warehouse,
Cost Center, and needed date are valid for the request; required evidence and
business purpose are available.

**Main path:**

1. The requester creates a draft request with one or more lines.
2. Each line identifies the requested Product or approved description, UOM,
   quantity, need-by date, destination, and justification. Cost-center and
   project information is recorded when the owning policy requires it.
3. The requester submits the request. Submission freezes the reviewed version
   for the approval decision and records an audit event.
4. The approved policy routes the request for approval, returns it for change,
   rejects it, or permits a direct next step where that policy is approved.
5. An approved request becomes eligible for sourcing or order creation. An
   unapproved request cannot create a committed Purchase Order.

**Alternative and exception paths:**

- A requester withdraws or cancels an uncommitted request only if policy
  permits; the original submission and reason remain visible.
- An approver rejects or returns the request with a reason; a revised request
  is a new reviewed version linked to the original.
- A request with inactive master data, insufficient quantity precision, an
  invalid scope, or missing mandatory evidence is rejected before submission.
- Partial approval is permitted only where the approved approval policy
  defines line-level or quantity-level approval; it must leave the remainder
  explicit and traceable.

### 9.2 Supplier quotation capture and comparison

**Trigger:** Approved demand requires sourcing or a buyer needs to document
the selected supplier.

**Main path:**

1. An authorized buyer records one or more external supplier offers.
2. Each offer records supplier, offer date, validity, line prices, currency,
   tax information, delivery terms, payment terms, attachments, and notes
   available from the supplier.
3. The buyer compares qualified offers on the business criteria defined by the
   Tenant policy, such as total value, delivery date, terms, and compliance
   evidence.
4. The selected offer and selection rationale are retained with the request or
   Purchase Order.

**Open policy:** Whether quotation comparison is mandatory, optional, or
threshold-controlled is MESP-42. The BRD supports all approved policy branches
without adopting the Founder Decision Pack recommendation.

**Boundary:** There is no supplier platform account or supplier-side action.
All offer details are entered by an authorized purchasing user.

### 9.3 Purchase Order

**Trigger:** Demand is approved and the buyer has a supplier selection or an
approved direct-order path.

**Preconditions:** Supplier, Product, UOM, destination Warehouse, Company,
Branch, currency, tax references, payment terms, and delivery dates are valid;
the source request and reviewed quotation evidence are linked where applicable.

**Main path:**

1. The buyer prepares a PO from one or more approved request lines.
2. The PO identifies Supplier, Company/legal entity, Branch, ship-to
   Warehouse, lines, quantities, UOM, agreed prices, transaction currency,
   tax information, discounts, payment/delivery terms, expected dates,
   attachments, and source references.
3. The buyer submits the reviewed version for the approved order policy.
4. After approval, the PO is issued and its commitment is visible as an open
   commitment. It does not create stock or AP.
5. Open quantity and commercial changes remain attributable to the PO version
   and the approving authority.

**Alternative and exception paths:**

- An approved order may be split into multiple supplier POs when the business
  need or source decision requires it; each child PO retains source-line
  quantities and rationale.
- An order may be partially confirmed, partially received, partially invoiced,
  or partially paid. Remaining quantity and value are never hidden.
- An eligible unposted open quantity may be cancelled with reason and audit.
- Material changes after approval return the order to the approved policy's
  review path. No material change is silently edited into a posted document.
- A supplier rejection or no response closes only the affected execution path
  or remaining quantity according to the approved policy; it does not erase the
  original PO history.

### 9.4 Manual Supplier Confirmation

**Trigger:** An external supplier communicates acceptance, a partial response,
rejection, expected delivery, or no response.

**Main path:**

1. An authorized buyer selects the issued PO and records the supplier response.
2. The record captures response status, response date, supplier contact/name
   as business evidence, supplier reference, confirmed quantity by line,
   expected delivery date, changed price or terms if communicated, notes, and
   attachments.
3. The buyer links the response to the PO version and records any material
   change or rejection reason.
4. The process exposes confirmed, partially confirmed, rejected, and no-response
   outcomes and leaves unconfirmed/remainder quantity visible.

**Policy branches held open by MESP-43:**

- whether receipt may proceed without confirmation;
- whether confirmation is informational or a hard gate;
- whether partial confirmation is allowed and how remainder is managed; and
- whether quantity, price, date, or term changes require reapproval.

The BRD requires the selected policy to be explicit before implementation. A
Supplier Confirmation itself never creates stock, AP, invoice, tax, or payment.

### 9.5 Goods Receipt coordination

**Trigger:** Goods arrive at the destination Warehouse.

**Preconditions:** The receiving user is authorized; the source PO and open
quantity are available; the Product, UOM, Warehouse, and any approved tracking
requirements are valid.

**Main path:**

1. The warehouse operator identifies the PO, delivery evidence, and received
   lines.
2. The operator records received, accepted, rejected/damaged, and outstanding
   quantities by line, with reason and evidence.
3. Partial receipt is allowed. The remaining open quantity stays linked to the
   PO and confirmation.
4. Inventory validates and posts the accepted quantity under its ownership.
5. The posted receipt is available for three-way matching and reporting.

**Boundary:** A draft or unposted receipt does not change authoritative stock.
Product tracking configuration is reused from MESP-31; batch/lot/serial/expiry
operational behavior remains the open MESP-41 decision and the Inventory-owned
MESP-33 boundary. Negative-stock behavior is MESP-45. This BRD does not choose
either decision.

### 9.6 Purchase Invoice and matching

**Trigger:** A supplier invoice or credit note is received and Finance is
authorized to record it.

**Main path:**

1. Finance records the supplier invoice with supplier reference/date, Company,
   currency, lines, taxes, discounts, totals, source PO/receipt references,
   due terms, attachments, and external evidence.
2. The invoice is compared with the Purchase Order and posted Goods Receipt
   evidence.
3. The configured matching outcome is recorded as accepted, exception,
   rejected, or another Finance-approved state.
4. An exception is assigned to an accountable owner with reason, evidence,
   resolution, and approval. An unresolved exception cannot be silently
   released to posting.
5. Finance posts the approved invoice and supplier liability in the valid fiscal
   period, preserving the source/match/tax/currency facts.

**Matching policy held open by MESP-44:** The required business process must
support the approved matching model and quantity/value tolerance, whether
two-way or three-way and whether zero or configurable. The BRD does not choose
the option or any amount/percentage. Until the named Finance/Procurement owner
decides, a mismatch remains an explicit exception rather than an implied auto
acceptance.

**Boundary:** A Purchase Invoice never increases stock quantity. A credit note,
debit note, reversal, or correction is linked to the original invoice and
does not silently edit posted history.

### 9.7 Supplier Payment and reconciliation

**Trigger:** An AP liability is approved for settlement and Finance's payment
policy permits the action.

**Main path:**

1. Finance identifies the payable invoice(s), due terms, Company, currency,
   payment amount, and any required approvals.
2. An authorized payment operator prepares the payment with evidence of method,
   execution/reference, date, amount, currency, bank/cash source where
   applicable, and allocation.
3. The approved payment is executed or recorded according to Finance policy.
4. The payment is posted and allocated fully or partially to supplier
   liabilities. Unallocated or on-account amounts remain explicit.
5. Finance reconciles the payment to the AP ledger and retains the result,
   exception, or unresolved status.

**Policy held open by MESP-47:** Supported methods, partial/on-account rules,
bank/cash/cheque/card/gateway boundaries, and any feed behavior are Finance
decisions. This BRD requires method-neutral payment evidence and settlement
trace without choosing a catalogue.

### 9.8 Supplier return

**Trigger:** Previously accepted goods must be sent back to the supplier.

**Main path:**

1. An authorized user identifies the original PO, Supplier Confirmation,
   Goods Receipt, stock location, and accepted quantity.
2. The user records return reason, quantity, condition, evidence, expected
   supplier response, and any replacement or credit expectation.
3. Inventory validates and posts the stock decrease or return event under its
   approved rules.
4. Finance records or links the Supplier Credit Note, debit/correction, or
   other financial consequence when applicable.
5. Procurement closes or leaves open the affected commercial quantity and
   records the supplier outcome.

Goods rejected before acceptance are receipt outcomes, not a supplier return.
A return of accepted goods is never implemented as deletion of the original
receipt. Tracking and valuation follow Inventory policy.

## 10. Document Lifecycle and Status Transitions

Statuses must communicate business state, not hide an exception. Exact status
names may be refined by the owning implementation specification only if the
meaning and audit transitions below are preserved.

| Document | Normal lifecycle | Terminal or controlled branches |
|---|---|---|
| Purchase Request | Draft -> Submitted -> Approved -> Sourcing/Ordered -> Closed | Returned for change, Rejected, Cancelled; partial approval or conversion leaves the remainder visible. |
| Supplier Quotation | Captured -> Compared -> Selected or Not Selected | Expired, Withdrawn, Rejected, or Superseded with evidence. |
| Purchase Order | Draft -> Submitted -> Approved -> Issued -> Awaiting Supplier Response -> Supplier Confirmed or Partially Confirmed -> Partially Received or Fully Received -> Closed | Rejected, No Response Closed, Cancelled, or controlled return/reopen of eligible unposted work. |
| Supplier Confirmation | Recorded as Confirmed, Partially Confirmed, Rejected, or No Response | A later response is a linked revision/event; it does not rewrite the prior response. |
| Goods Receipt | Draft/Checked -> Posted | Reversal or Supplier Return; Inventory owns exact posted/reversed statuses. |
| Purchase Invoice | Draft -> Matched or Exception -> Approved -> Posted -> Partially Paid or Paid | Rejected, Credit Note, Debit Note, Reversal, or correction; Finance owns exact posting statuses. |
| Supplier Payment | Draft -> Approved -> Executed/Recorded -> Posted -> Partially Allocated or Allocated/Reconciled | Rejected, Failed/Unknown requiring reconciliation, Reversed, or Unallocated/on-account according to Finance policy. |
| Supplier Return | Draft -> Approved -> Posted | Rejected, Reversed, or linked credit/correction; Inventory and Finance own their respective effects. |

### 10.1 Rejection, cancellation, reopening, and correction

- Rejection records actor, time, scope, reason, affected lines, and next
  action. It does not delete the rejected version.
- Cancellation is available only for eligible unposted/open quantity under the
  approved policy. It records who cancelled, why, and what remains open.
- Reopening is a controlled action for an eligible unposted document or
  remainder. It must create a new reviewed version or linked continuation and
  cannot be used to alter a posted event.
- After posting, correction uses a linked reversal, return, credit/debit note,
  or adjustment. The original remains queryable and the correction explains
  the changed business outcome.

## 11. Business Data Requirements

The following is a business data inventory, not a database design. Exact field
types and storage are implementation concerns.

| Data group | Required business facts |
|---|---|
| Common identity and scope | Stable document identity, business number, Tenant, Company/legal entity, Branch, Warehouse where relevant, document type, version/revision, status, created/updated actor and time, source link, and correlation/reference. |
| Request | Requester, purpose/justification, request date, need-by date, lines, Product/description, UOM, quantity, destination, cost center/project where applicable, attachments, approval outcome, and remaining quantity. |
| Quotation | Supplier, offer/reference, offer date, validity, lines, quantities, prices, currency, taxes, discounts, delivery/payment terms, evidence, comparison facts, selection rationale, and selected/not-selected outcome. |
| Purchase Order | Supplier, legal entity, Branch, ship-to Warehouse, source requests/quotes, lines, Product/UOM, ordered quantity, open/confirmed/received/invoiced quantity, price, currency, tax, discount, delivery dates, terms, attachments, approval, issue date, and cancellation/closure reason. |
| Supplier Confirmation | External supplier reference/contact evidence, response status, response date, confirmed quantity by line, expected date, changed commercial facts, notes, attachments, recorder, and link to PO version. |
| Goods Receipt | PO and confirmation links, delivery evidence, arrival/check date, Warehouse, line quantities received/accepted/rejected/outstanding, reason codes, condition, tracking facts where enabled, posting outcome, and return/reversal link. |
| Purchase Invoice | Supplier invoice/reference/date, Company, lines, quantities, prices, taxes, discounts, transaction currency, totals, due terms/date, PO/GR links, match result, exception, approval, posting period, AP reference, attachments, and correction/credit links. |
| Supplier Payment | Supplier, Company, source invoice/AP references, amount, currency, payment date, approved method evidence, execution/reference, source account evidence where allowed, allocation, unallocated amount, posting, reconciliation, and reversal/failure evidence. |
| Supplier Return | Original receipt/PO/confirmation, Supplier, Warehouse, return quantity, condition/reason, return date, evidence, stock posting/valuation reference, credit/correction reference, authorization, and outcome. |
| Audit and evidence | Actor, server-derived Tenant and scope, action, before/after business state, reason, policy/version, approval evidence, correlation/idempotency reference, attachment event, failure/exception, reversal chain, and access review evidence. |

Material values must preserve the source snapshot needed to explain a historical
decision even when a reusable master record later changes. Supplier, Product,
UOM, Tax, Payment Term, Currency, and Exchange Rate identity remains owned by
the relevant Master Data or Finance domain.

## 12. Validation Rules

| ID | Business validation |
|---|---|
| PROC-VR-001 | The server must validate the current Tenant and resource scope; client-supplied Tenant, Company, Branch, Warehouse, role, or permission values cannot widen authority. |
| PROC-VR-002 | A new document must reference active, authorized Supplier, Product, UOM, Company, Branch, Warehouse, currency, and other required masters. |
| PROC-VR-003 | Quantities, prices, dates, currencies, tax facts, and totals must be present and internally consistent for the document type and approved policy. |
| PROC-VR-004 | A Purchase Request cannot be submitted without required purpose, lines, quantities, destination, need-by date, and evidence required by policy. |
| PROC-VR-005 | A Purchase Order cannot be issued without an approved demand path or an explicitly approved direct-order path and valid supplier/destination data. |
| PROC-VR-006 | Supplier Confirmation can be recorded only against an issued or otherwise eligible PO and must preserve external response evidence. |
| PROC-VR-007 | A receipt cannot exceed the allowed open quantity unless an approved over-receipt policy and exception are present; the decision is not inferred. |
| PROC-VR-008 | Only Inventory may post accepted receipt quantity to stock; rejected, draft, or unposted quantity remains outside authoritative stock. |
| PROC-VR-009 | An invoice must retain supplier reference, date, Company, currency, lines, totals, source links, and match/exception evidence before Finance approval or posting. |
| PROC-VR-010 | A mismatch must be visible and assigned; it cannot be silently rounded, accepted, or released without the approved tolerance and authority. |
| PROC-VR-011 | A payment cannot be posted for an unknown, duplicate, cancelled, or unapproved liability; an on-account or unallocated amount remains explicit. |
| PROC-VR-012 | A supplier return must identify previously accepted receipt quantity and cannot exceed the eligible returned quantity without an approved exception. |
| PROC-VR-013 | Every cancellation, rejection, reopen, reversal, return, credit, debit, and correction requires an actor, reason, timestamp, affected scope, and link to the prior event. |
| PROC-VR-014 | A posted document cannot be edited in place. A correction creates a linked business event and preserves the original evidence. |
| PROC-VR-015 | A repeated request in the same idempotency scope must have one business effect and a queryable outcome, including after a timeout or retry. |
| PROC-VR-016 | A stale document version cannot overwrite a newer approved or posted version; the user must reconcile the conflict. |
| PROC-VR-017 | Transaction, base, and reporting currency facts preserve amount, currency, rate, rate date/source as available, conversion, precision, and rounding evidence. The rate source decision remains MESP-54. |
| PROC-VR-018 | Financial posting requires an open and valid fiscal period under Finance rules; a closed period creates a visible exception. |
| PROC-VR-019 | Tax facts use the active configured Tax/country-pack reference and preserve the applied version; this BRD does not assert statutory correctness. |
| PROC-VR-020 | Attachments and external references must be authorized, linked to the correct Tenant and document, and auditable; failure must not falsely mark the business event complete. |

## 13. Permissions and Access Scope

Permissions are business capabilities. IAM defines the server-side membership,
role, and resource-scope mechanism; Procurement defines the actions and
document-state checks below.

| Capability | Minimum business authorization |
|---|---|
| Create/edit draft request | Requester role in the permitted Tenant/Company/Branch scope. |
| Submit or withdraw request | Requester or authorized Procurement role, subject to state and policy. |
| Approve/reject request or PO | Approver authority for the document type, amount/scope, and policy version. Exact catalogue is MESP-42. |
| Record quotation | Buyer/Procurement role with supplier and source scope. |
| Issue/cancel PO | Authorized Procurement role after required approval; cancellation requires eligible state and reason. |
| Record Supplier Confirmation | Authorized purchasing user; no supplier identity/session is required. |
| Check or post Goods Receipt | Warehouse/Inventory role for the destination Warehouse and receipt state. |
| Record/match/post Purchase Invoice | Finance/AP role for the Company/fiscal scope and approved exception authority. |
| Prepare/approve/post Supplier Payment | Finance/Treasury role with the approved payment authority and separation controls. |
| Create/approve/post Supplier Return | Procurement/Inventory role for the stock event plus Finance authority for the financial consequence. |
| Resolve matching exception | The named Procurement or Finance owner for the exception type; approval cannot be inferred from visibility. |
| Search/export/report | Authorized Tenant/resource scope with report-specific read/export permission; exports are audited. |
| View audit/history | Authorized audit, security, owner, or domain role; history remains Tenant-scoped. |

All denied, cross-Tenant, out-of-scope, inactive-resource, state-conflict, and
privileged actions must fail closed and leave auditable evidence. Support or
operations access is not a substitute for normal business authorization.

## 14. Approval Controls and Separation of Duties

### 14.1 Approval controls

The process must use a versioned, effective-dated approval policy that can
describe document type, amount/value, Company/Branch, request role, approver
authority, required evidence, and decision outcome. The approved policy version
must be recorded with the decision. An approval authorizes the business action;
it does not itself post stock, AP, tax, or payment.

MESP-42 remains open on whether Purchase Requests, quotation comparison, and/or
Purchase Orders are mandatory and how thresholds and approver structures are
defined. This BRD therefore requires policy evaluation and explicit pending
state, but does not select an option.

MESP-55 remains open on delegation, escalation, out-of-office, reassignment,
and approval expiry. A future approved policy must identify the acting authority
and preserve the original delegation/reassignment evidence.

### 14.2 Separation of duties

The business process must support separation between request, supplier
selection/order, receipt, invoice approval/posting, and payment approval where
the approved policy requires it. The BRD does not assert a universal role
matrix. A policy may require different people for one or more stages, forbid
self-approval, limit financial authority, or require an exception approver.
Those choices are recorded as an explicit policy version and audit evidence.

Recommended Founder Decision Pack positions such as “no self-approval” are
**recommended defaults - not approved** until MESP-42/MESP-55 owners decide.
No user may use a client-supplied role, amount, or approval flag to bypass the
server-side authority or separation policy.

## 15. Matching and Exception Ownership

| Exception | First accountable owner | Required evidence |
|---|---|---|
| PO quantity/price/date differs from supplier response | Procurement | External response, affected lines, supplier reference, revised approval if policy requires it. |
| Receipt differs from PO/confirmation | Warehouse/Inventory with Procurement coordination | Delivery evidence, accepted/rejected/outstanding quantities, reason, and stock posting outcome. |
| Invoice differs from PO or receipt | Finance/AP with Procurement and Inventory input | Match comparison, tolerance decision, exception owner, resolution, and approval. |
| Tax or currency fact cannot be validated | Finance | Applied master/rate version, source/date, tax evidence, and Finance decision. |
| Duplicate/unknown payment result | Finance/Treasury | Execution/reference evidence, reconciliation result, and correction/reversal if needed. |
| Return quantity or credit differs from accepted receipt | Procurement + Inventory + Finance | Original receipt, return evidence, stock event, credit/correction, and authorization. |
| Supplier no response or overdue delivery | Procurement | Contact/attempt evidence, expected date, escalation/outcome, and remaining commitment. |

MESP-44 controls the precise auto-acceptance and tolerance policy. Until the
decision is approved, the business requirement is conservative exception
visibility: no mismatch may be hidden or auto-posted by an unapproved rule.

## 16. Concurrency, Idempotency, and Failure Behavior

These are business control requirements, not an implementation design.

- Two authorized users acting on the same request, PO, receipt, invoice,
  payment, or return must not silently overwrite one another. The losing action
  receives a clear stale-version or state-conflict outcome and must reconcile.
- A retry after an uncertain response must be safe. The business result is
  queryable by a stable correlation/idempotency reference before a second
  effect is accepted.
- A timeout after receipt, posting, payment, return, or correction must be
  treated as an unknown outcome requiring reconciliation, not as permission to
  repeat the effect blindly.
- A failure before the business effect leaves no false “posted” status. A
  failure after the effect leaves the authoritative status and evidence
  visible, with compensating correction required for any new outcome.
- Notifications, attachments, exports, and downstream handoffs may fail
  independently but must expose failure and retry/recovery state without
  changing the authority of the underlying document.
- Approval, receipt, invoice, payment, and return actions must validate the
  current server-derived Tenant and resource scope at the time of action, not
  rely on an earlier screen or cached role.

## 17. Inventory, Accounting, Currency, and Saudi Boundaries

### 17.1 Inventory impact

Procurement supplies the PO, confirmation, expected receipt, and source-line
context. Inventory owns receipt validation, accepted/rejected quantities,
stock posting, tracking, valuation, reversal, and supplier-return movement.
The business chain must show:

`PO commitment -> expected receipt -> posted accepted receipt -> stock history`

There is no stock increase at request, quote, PO, confirmation, invoice, or
payment. MESP-41 tracking and MESP-45 negative-stock decisions remain open.

### 17.2 Accounting and AP impact

Finance owns the posting matrix, AP liability, tax evidence, fiscal period,
credit/debit/reversal, due terms, supplier balance, payment, and reconciliation.
The BRD requires a traceable relation between PO, receipt, invoice, payment,
return, credit, and correction, but does not invent account mappings, GRNI or
accrual policy, journal rules, or posting dates beyond the approved Finance
baseline.

### 17.3 Multi-currency impact

The process must preserve transaction currency and amount, Company functional
or base currency, reporting currency where defined, applied rate and rate date,
rate/source evidence when available, converted amount, precision, and rounding.
Finance owns the applicable rate source/update and reporting-currency decision;
MESP-54 remains open. A document may not silently change currency facts because
a master record or current rate later changes.

### 17.4 Saudi and localization impact

The PRD baseline permits Saudi defaults such as SAR and Asia/Riyadh and requires
configurable tax, bilingual document facts, and country-pack integration
boundaries. The BRD requires configurable, versioned evidence and Arabic/English
presentation support where the approved localization baseline requires it.

MESP-49 remains an external Finance/tax-advisor production gate for Saudi
e-invoicing. This BRD does not declare VAT, ZATCA, invoice numbering, FATOORA,
banking, residency, or statutory compliance complete. PDPL, residency,
retention, legal hold, purge, backup, and restoration remain their existing
external or production-gated decisions, especially MESP-50.

### 17.5 Organization and approved decisions

The process is Tenant-isolated and Company/legal-entity scoped. MESP-56 / PD-021
is approved: a Tenant may have multiple legal entities, each with its own
accounting boundary; Release 1 excludes consolidation, intercompany
automation, eliminations, transfer pricing, and consolidated statements.
MESP-52 / PD-020 is approved in its own scope but does not answer Procurement
approval, supplier, receipt, invoice, or payment policy.

## 18. Reports, KPIs, Notifications, and Audit

### 18.1 Required report coverage

The report catalogue and reconciliation ownership remain MESP-53. The BRD
requires the following business coverage for authorized users:

| Report or view | Minimum business meaning |
|---|---|
| Purchase Request aging | Submitted, pending, approved, rejected, cancelled, converted, and overdue demand by Tenant/Company/Branch/owner. |
| Open commitments | Issued PO quantity/value, confirmed/remainder, expected dates, cancelled quantity, and open commitment by Supplier and scope. |
| Supplier confirmation status | Confirmed, partial, rejected, no response, overdue, and response-age measures with source evidence. |
| Delivery and receipt | Expected versus received, accepted/rejected/outstanding, overdue deliveries, and return quantities by Warehouse/Supplier. |
| Matching exceptions | PO/receipt/invoice comparison, exception type, tolerance/policy version, owner, age, resolution, and posting impact. |
| Supplier spend and performance | Approved/posting-based spend, delivery performance, rejection/return rate, response behavior, and source date/period. |
| AP and payment | Invoice status, due/overdue liability, partial/paid/unallocated payment, supplier balance, and reconciliation exceptions. |
| Returns and credits | Returned accepted quantity, reason, stock effect, credit/correction status, and unresolved commercial balance. |
| End-to-end reconciliation | Counts and values from request/order/receipt/invoice/payment/return with documented inclusion, exclusions, and difference ownership. |

Every report must state its scope, time basis, source status, currency basis,
refresh/freshness, authorized drill-down behavior, and reconciliation owner.
KPI formulas and mandatory catalogue remain open under MESP-53.

### 18.2 Notifications

The process should notify authorized users of assigned approvals, returned or
rejected requests, PO approval/issue, supplier no response or overdue delivery,
receipt exceptions, invoice mismatch, payment failure/unknown outcome,
reconciliation difference, and return/credit outcome. Notification delivery
state, retry, and failure must be visible. A notification is not the source of
authority and cannot replace an audit event.

### 18.3 Audit evidence

The audit trail must make it possible to answer who, what, when, where, why,
under which policy/version, and with which prior/current value for:

- create, edit, submit, approve, reject, withdraw, cancel, reopen, issue,
  confirm, receive, post, match, exception, approve, pay, allocate, return,
  reverse, credit, debit, reconcile, export, and access-denial actions;
- Tenant, Company, Branch, Warehouse, Supplier, source document, source line,
  amount/quantity, currency/rate, tax, status, and actor scope;
- attachment/reference creation or failure;
- duplicate, stale-version, permission, cross-Tenant, idempotency, posting,
  and downstream failure outcomes; and
- correction links and original history.

Audit records are immutable and queryable by authorized users. Retention,
legal hold, purge, residency, and deletion behavior remains MESP-50 and the
applicable privacy/legal validation gate.

## 19. Integration, Import/Export, and Migration Requirements

### 19.1 Integrations

The business integration boundary requires any future command or event to be
Tenant-authenticated, authorized, validated, versioned, correlated,
idempotent, rate-aware, and observable. External callbacks, if later approved,
must be authenticated/signed as applicable, sequence-aware, duplicate-safe,
retryable, and recoverable from visible failure or dead-letter state.

No supplier integration or supplier portal is implied. The Release 1 supplier
response path is manual recording by an authorized purchasing user. Finance,
Inventory, Master Data, Reporting, Notification, file, and country-pack
integrations must preserve the ownership boundaries in this BRD. ADR-007 and
ADR-009 are constraint references only; this BRD does not select a broker,
storage provider, or production connector.

### 19.2 Import and export

Authorized exports must be Tenant/resource scoped, identify the source status,
time/currency basis, filters, and export actor, and be auditable. Exports must
not expose another Tenant or silently omit exception rows.

Imports, if separately authorized, must validate supplier/product/UOM/tax/
currency identity, source document links, duplicate business references,
quantities, amounts, dates, scope, and required evidence. Invalid rows are
quarantined with actionable errors; valid rows do not imply approval or posting.

### 19.3 Migration

Migration is not authorized by MESP-32. If MESP-51 later approves it, the
business process must provide source/data owners, extraction and cleansing
rules, mapping, rejected-row results, duplicate handling, immutable batch/row
evidence, sign-off, and reconciliation for open orders, receipts, supplier
balances, AP, payments, stock, tax, and document counts. Master data and
configuration precede open transaction migration. At least two dry runs,
rehearsal, rollback/BCP evidence, and Owner sign-off remain required gates.

## 20. Operational and Quality Requirements

The PRD NFR baseline applies as a business expectation subject to later
architecture, provider, volume, privacy, and production validation. The
Procurement process must be:

- available and recoverable according to the agreed Release 1 service tier;
- responsive for normal reads and commands under the agreed reference load;
- transactionally consistent for authoritative status, stock, AP, payment,
  and correction effects;
- isolated so one Tenant's load, data, exports, attachments, or reports cannot
  affect another Tenant's authority or confidentiality;
- observable through business outcomes, audit, correlation, failure, retry,
  reconciliation, and exception indicators;
- usable in approved English/Arabic and RTL contexts where the localization
  baseline requires it, without closing ADR-011's open linguistic decision;
- accessible and understandable for the approved user roles; and
- portable, recoverable, and retained according to MESP-48, MESP-49, MESP-50,
  privacy, legal, and production gates.

No percentage, volume, RPO/RTO, provider, or production claim in this section
overrides the open gate owner decisions.

## 21. Given / When / Then Acceptance Scenarios

These are business acceptance scenarios, not automated test specifications.

| ID | Given | When | Then |
|---|---|---|---|
| PROC-AC-001 | An authorized requester has a valid need and active masters | They save a Purchase Request draft | The request stores purpose, lines, quantities, UOM, destination, need-by date, evidence, and Tenant scope without commitment or stock. |
| PROC-AC-002 | A request has all required policy evidence | The requester submits it | The reviewed version is frozen for approval and an audit event is recorded. |
| PROC-AC-003 | An approver lacks authority for the request or policy branch | They attempt approval | The action is denied, no downstream commitment is created, and the denial is auditable. |
| PROC-AC-004 | A request is approved and sourcing is permitted | A buyer records supplier offers | Each offer preserves supplier, price, currency, tax/delivery/payment terms, validity, evidence, and comparison rationale. |
| PROC-AC-005 | Demand is approved and supplier/destination masters are valid | A buyer issues a Purchase Order | The PO becomes an authorized commitment and no stock, AP, tax, or payment effect occurs. |
| PROC-AC-006 | An issued PO receives a partial external response | An authorized user records Supplier Confirmation | Confirmed quantity, remainder, expected date, supplier reference, and evidence are visible; no stock or liability is created. |
| PROC-AC-007 | Confirmation is required by the later approved policy but is absent | A user attempts receipt | The result follows the approved MESP-43 policy branch; the BRD does not permit an unrecorded default. |
| PROC-AC-008 | Goods arrive for an issued PO | The warehouse accepts only part of the delivery | The posted receipt contains accepted/rejected/outstanding quantities, stock changes only for accepted posted quantity, and the PO remains partially open. |
| PROC-AC-009 | Goods are rejected before acceptance | The operator records rejected quantity | Rejected quantity has reason/evidence and does not increase authoritative stock. |
| PROC-AC-010 | A supplier invoice matches PO and posted receipt under the approved policy | Finance approves and posts it | AP/tax/accounting evidence is created in the valid period and stock quantity is unchanged. |
| PROC-AC-011 | Invoice quantity or value is outside the approved match rule | Finance attempts release | A visible exception is assigned; the invoice cannot silently auto-post under an unapproved tolerance. |
| PROC-AC-012 | An approved payable exists and Finance permits settlement | A payment is posted for part of the amount | The payment and allocation are traceable; the remaining liability is explicit. |
| PROC-AC-013 | Accepted goods must be returned | Authorized users post a supplier return | Inventory records the stock effect, Procurement links the commercial reason, and Finance records the credit/correction when applicable. |
| PROC-AC-014 | A posted receipt or invoice contains an error | An authorized correction is required | A linked reversal/return/credit/debit/adjustment is created; the original remains immutable and queryable. |
| PROC-AC-015 | A user from another Tenant supplies a valid-looking document ID | They request or mutate Procurement data | The server denies the action and no data, attachment, report, or audit scope leaks across Tenants. |
| PROC-AC-016 | A referenced Supplier, Product, UOM, Company, Branch, Warehouse, Tax, or Currency is inactive | A user starts new work | The new action is rejected with an actionable reason; historical documents remain readable to authorized users. |
| PROC-AC-017 | Two users load the same PO version | Both attempt material changes | One outcome is accepted and the stale outcome is rejected or reconciled without silent overwrite. |
| PROC-AC-018 | A receipt, invoice posting, payment, or return request times out | The user retries with the same idempotency reference | The business effect occurs once and the outcome remains queryable. |
| PROC-AC-019 | A supplier has no platform account | A buyer records external confirmation | The process records supplier evidence without creating a User, login, credential, membership, or session. |
| PROC-AC-020 | A payment or posting result is unknown after external failure | Finance reconciles the result | The uncertain state and evidence are visible; a duplicate financial effect is not created by blind retry. |
| PROC-AC-021 | A document is in a closed fiscal period | Finance attempts posting | The posting is blocked or routed under the approved Finance policy and the period exception is audited. |
| PROC-AC-022 | A material PO or confirmation change is requested after approval | The buyer submits the change | The approved policy determines whether reapproval is required; the prior version and change rationale remain visible. |
| PROC-AC-023 | A return quantity exceeds accepted receipt quantity | The user attempts posting | The action is rejected or routed to an explicit approved exception; the original receipt is not edited. |
| PROC-AC-024 | The Tenant has multiple legal entities | Users process orders and invoices | Company/accounting scope remains distinct; no consolidation or intercompany automation is inferred from MESP-56. |
| PROC-AC-025 | An authorized user exports a matching-exception report | The export is requested | The result is Tenant/resource scoped, identifies filters and time/currency basis, includes exception ownership, and creates audit evidence. |
| PROC-AC-026 | An import contains duplicate or invalid source rows | The migration/import is previewed | Invalid rows are quarantined with reasons and no approval/posting/stock/AP effect is implied. |
| PROC-AC-027 | An attachment or notification service fails | The business document action completes or fails | The document authority is not falsely changed; failure, retry/recovery state, and actor evidence are visible. |
| PROC-AC-028 | A report compares order, receipt, invoice, payment, and return totals | An authorized owner runs reconciliation | The report states its source statuses, scope, currency/time basis, freshness, differences, and accountable reconciliation owner. |

## 22. Open Decision Register and Deferred Gates

MESP-23 comment `10731` is the current living-register reconciliation. No open
row below is resolved by this BRD. Where a recommendation is shown, it is a
decision aid only.

| BRD row | Jira | Current status | Named owner / required input | Procurement consequence |
|---|---|---|---|---|
| PROC-OD-001 | MESP-42 | Open / To Do | Head of Procurement with Finance concurrence | Decide request/quotation/PO approval requirements, thresholds, approver structure, and Tenant configurability. Until then, the BRD supports policy branches and blocks implementation selection. |
| PROC-OD-002 | MESP-43 | Open / To Do | Head of Procurement | Decide confirmation gate, partial confirmation, changed quantity/price/date, rejection, and no-response behavior. The manual record and no-stock/no-liability boundary are confirmed; the gate is not. |
| PROC-OD-003 | MESP-44 | Open / To Do | Finance Controller with Procurement concurrence | Decide two-way/three-way model and quantity/value tolerance. Mismatch visibility and exception ownership are required; exact auto-acceptance is not chosen. |
| PROC-OD-004 | MESP-47 | Open / To Do | Finance Controller / Treasury | Decide bank transfer, cash, cheque, card/gateway, feed, partial, on-account, and reconciliation policy. The BRD remains method-neutral. |
| PROC-OD-005 | MESP-41 | Open / To Do | Product/Inventory owner | Decide batch/lot/serial/expiry operational tracking. The BRD records tracking facts where enabled but does not choose behavior. |
| PROC-OD-006 | MESP-45 | Open / To Do | Inventory owner | Decide negative-stock behavior and its receipt/return exception impact. No negative-stock default is adopted. |
| PROC-OD-007 | MESP-48 | Open / To Do | Product/Platform Operations | Establish supported volume and performance evidence before affected implementation/production capacity claims. |
| PROC-OD-008 | MESP-49 | Open / To Do | Finance Controller and Saudi tax advisor | Validate Saudi e-invoicing, VAT, tax, invoice, credit/debit, and production country-pack obligations. No legal conclusion is made. |
| PROC-OD-009 | MESP-50 | Open / To Do | Data Protection/Compliance, Platform Operations, and legal | Decide retention, privacy, legal hold, purge, residency, backup, restoration, and evidence policy. |
| PROC-OD-010 | MESP-51 | Open / To Do | Wafra business owner and Finance | Decide migration scope, openings/history, reconciliation, and cutover evidence. Wafra remains validation-only; no customer-specific behavior is added. |
| PROC-OD-011 | MESP-53 | Open / To Do | Finance Controller, Product owner, and report owners | Approve report catalogue, definitions, reconciliation owners, and freshness/retention expectations. |
| PROC-OD-012 | MESP-54 | Open / To Do | Finance Controller / Treasury | Decide exchange-rate source, update cadence, effective date, reporting currency, and correction policy. |
| PROC-OD-013 | MESP-55 | Open / To Do | Finance Controller and Product owner | Decide delegation, escalation, out-of-office, reassignment, expiry, and self-approval controls. |
| PROC-OD-014 | MESP-46 | Open / To Do | B2B Sales/Finance owner | B2B credit policy is downstream and not answered by Procurement; preserve the dependency. |
| PROC-OD-015 | MESP-52 | Done / approved PD-020 | Product/Founder decision already recorded | Approved plan/metadata boundary is preserved but does not authorize Procurement approval or payment behavior. |
| PROC-OD-016 | MESP-56 | Done / approved PD-021 | Product/Founder decision already recorded | Multiple legal entities per Tenant and no R1 consolidation are adopted only as the organization boundary; Finance mechanics remain MESP-34. |

### 22.1 Decision status and implementation gate

The open rows do not prevent this document from describing a coherent business
process because each unresolved choice is represented as a named policy branch,
with an explicit owner, consequence, and pre-implementation gate. They do
prevent selecting a final approval catalogue, confirmation gate, matching
tolerance, payment method catalogue, tracking behavior, rate source, retention
policy, or migration scope in a later implementation specification until the
appropriate owner records the decision in Jira and the affected downstream
BRD/LIS accepts it.

No Owner response was fabricated or inferred during this session. The normal
decision process remains open for the named owners above.

## 23. Source Conflicts, Corrections, and Review Notes

### MESP-124 source-decision consumption and terminal recovery reconciliation

For the bounded MESP-124 Purchase Order slice, one Supplier Source Decision
creates at most one Purchase Order per Tenant. Once a Purchase Order has been
created, its Source Decision remains consumed for its lifetime; a Cancelled or
Rejected Purchase Order does not release that source for another Purchase
Order. Recovery currently requires a new sourcing action and a new Source
Decision. This preserves the duplicate-spend and duplicate-commercial-
commitment invariant.

Controlled reopening of the same Purchase Order, or replacement semantics for
eligible unposted work, is a **FUTURE EXPLICIT CAPABILITY / DECISION** and is
not implemented by MESP-124. Any future reopen must preserve the original PO,
source lineage, actor, reason, timestamp, scope, prior status, current status,
and linked audit/history. The MESP-124 UI communicates the current new-source
recovery rule for terminal Cancelled/Rejected states and must not imply that
the full BRD reopen capability is available.

1. The approved current sequence is MESP-25 comment `10057`: MESP-31 Master
   Data, then MESP-32 Procurement, then MESP-33 Inventory, then MESP-34
   Finance. Older sequencing notes that reverse Finance and Sales are not used.
2. MESP-26 comment `10058` is the approved BRD entry gate. It authorizes the
   BRD wave but does not resolve MESP-41 through MESP-55.
3. The canonical PRD is `docs/MESP_PRD_v1.2.docx`. Older file names in Jira
   descriptions refer to the same approved baseline and do not create a second
   source.
4. `docs/04_Procurement.md` is a legacy placeholder. This file is the canonical
   MESP-32 BRD artifact and does not silently treat the placeholder as a
   completed requirements baseline.
5. MESP-31's approved Master Data BRD is reused for Supplier, Product, UOM,
   Tax, Payment Term, Currency, and Exchange Rate identity boundaries. This
   BRD owns the procurement process around those records and does not redefine
   their identity or lifecycle.
6. ADR-002, ADR-004, ADR-006, ADR-007, ADR-008, ADR-009, and ADR-018 constrain
   later feasibility and safety review. They do not authorize implementation
   work in this BRD session.

## 24. Coverage Checklist

| Required MESP-32 output | Location | Status |
|---|---|---|
| Business purpose and outcomes | Sections 2-3 | Covered |
| Actors and responsibilities | Section 6 | Covered |
| Triggers and preconditions | Section 9 | Covered for each lifecycle stage |
| Main process and alternatives | Sections 9-10 | Covered |
| Exceptions and ownership | Sections 9, 15, 21 | Covered |
| Business rules | Sections 8, 12, 14-18 | Covered |
| Document lifecycle and status transitions | Section 10 | Covered with owner-boundary qualifications |
| Data requirements | Section 11 | Covered as business inventory, not schema |
| Validation rules | Section 12 | Covered |
| Permissions and approval controls | Sections 13-14 | Covered; unresolved choices remain open |
| Separation of duties and concurrency | Sections 14 and 16 | Covered |
| Inventory impact | Section 17.1 | Covered; MESP-33 remains owner |
| Accounting/AP/payment impact | Sections 9.6-9.7 and 17.2 | Covered; MESP-34 remains owner |
| Multi-currency and Saudi impact | Sections 17.3-17.4 | Covered with external gates |
| Reports, KPIs, notifications, audit | Section 18 | Covered; MESP-53/MESP-50 remain open |
| Integration and migration | Section 19 | Covered as boundaries and gates |
| Operational requirements | Section 20 | Covered without production claims |
| Given/When/Then scenarios | Section 21 | Covered by 28 business scenarios |
| Open decisions and named owners | Section 22 | Covered; no open answer inferred |
| Business-owner approval | Jira handoff and final document-control update | Recorded in MESP-32 Jira comment `10739`; approval is limited to the business baseline and preserves all open decision gates |

## 25. Review and Approval Status

This v0.1 document is an Approved Business Baseline. The Owner review verified
that:

- the PRD primary anchors and BR-005 are covered;
- all required Procurement/P2P lifecycle stages and partial/exception paths
  are represented;
- Supplier remains external and manually recorded;
- stock, AP, payment, tax, currency, approval, audit, Tenant, and correction
  boundaries are not crossed;
- MESP-41 through MESP-55 remain visible and unresolved except the exact
  approved MESP-52/MESP-56 rows;
- no Retail POS, Wafra-specific core behavior, legal conclusion, or
  implementation instruction has been introduced; and
- the next implementation or domain BRD is not activated by this document.

Owner approval is recorded in MESP-32 comment `10739` against reviewed content
head `5e4e2122fe3346af96a90a7152602410769f0cf9`. The normal PR merge and Jira
closure evidence are tracked separately from this business content. Approval
of this BRD is a business requirements baseline only; it is not source
implementation authorization.
