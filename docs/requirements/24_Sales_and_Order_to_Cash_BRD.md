# B2B Sales and Order-to-Cash Business Requirements Document

> **Version:** v0.1 - Approved Business Baseline
> **Jira:** MESP-35 - Produce B2B Sales and Order-to-Cash BRD
> **Parent:** MESP-9 - EPIC 09 - Sales and Order-to-Cash
> **BRD sequence:** Position 10 of 15, following the approved Finance baseline
> **Date:** 11 August 2026
> **Scope:** Release 1 B2B ERP only; Wafra is validation-only
> **Status:** Approved business baseline; documentation-only; no implementation authorization

## 1. Document control and reading rules

This document is the bounded Release 1 business-requirements baseline for
B2B Sales and the Order-to-Cash process. It defines the commercial chain from
quotation through order, fulfillment, invoice, receipt, settlement,
reconciliation, return, and credit correction. It also defines the controls
and cross-module handoffs that a later implementation specification must
preserve.

This is a business document. It does not define database tables, entities,
migrations, API contracts, controllers, screens, framework behavior, provider
selection, deployment topology, production configuration, or an automated
test implementation. It authorizes none of those activities. A later
implementation item must preserve the approved evidence and must not turn an
open decision, recommendation, or conditional branch into a hidden default.

The approved Finance BRD was analysed before this Sales BRD. Finance remains
the accounting control point. The approved Inventory BRD remains the physical
stock and valuation control point. This document describes the Sales
responsibility at their boundaries; it does not close either domain's
decisions.

### 1.1 Classification legend

| Classification | Meaning in this BRD |
|---|---|
| **Confirmed baseline** | Directly supported by the approved PRD, approved glossary, approved upstream BRD, ADR boundary, or named approved Jira decision. |
| **BRD requirement** | Business behavior required by this baseline after Owner approval, subject to named gates and open decisions. |
| **Open decision / gate** | Not approved. The affected branch must remain visible and cannot be implemented as an implicit default. |
| **Conditional branch** | A business path that is described so the end-to-end process is coherent, but whose policy details require the named Owner or external validation. |
| **Recommendation only** | A proposal retained for a later decision. It is not a requirement, acceptance criterion, or implementation instruction. |
| **External validation** | Qualified tax, Saudi, legal, banking, privacy, security, or other specialist validation required before the affected release or production gate. |
| **Out of scope** | Excluded from Release 1 or from this Sales domain baseline. |

The Founder Decision Pack is not an approval catalogue. Its defaults are not
requirements unless a named approval record says otherwise. The only
applicable broadly approved MESP-23 decisions carried into this document are
MESP-52 / PD-020 and MESP-56 / PD-021, and neither silently resolves a Sales,
Finance, Inventory, or localization policy.

## 2. Executive summary

Release 1 B2B Sales provides a controlled commercial process for a Business
Customer within its owning Tenant. An authorized internal user may prepare a
quotation, obtain any required commercial approval, convert an accepted
quotation to an order, validate the order against the customer, Product,
pricing, tax, availability, credit, Company, Branch, Warehouse, currency,
and authority context, and confirm it only when the applicable policy permits.

Confirmed orders create a demand and fulfillment handoff. Inventory owns
physical reservation, allocation, delivery posting, stock movement, tracking,
and valuation evidence. Sales coordinates the commercial status and preserves
the links between the order, delivery evidence, invoice request, receipt,
return, and credit correction. Finance owns the AR, revenue, tax, receipt,
allocation, period, currency, posting, reversal, and financial
reconciliation decisions and records.

The central business invariant is:

> A quotation is a commercial proposal, a confirmed order is a controlled
> customer commitment, a reservation is not a stock decrease, a delivery is
> not automatically an AR or revenue posting, a valid invoice creates a
> Finance-controlled customer obligation, a receipt settles that obligation
> only through Finance allocation, and a return or credit correction must
> remain linked to its source without silently mutating posted history.

The process must support partial and exceptional outcomes without pretending
that an unapproved policy has been selected. Where credit enforcement,
reservation timing, backorder, substitution, payment method, tax,
exchange-rate, Payment Term, return/refund, approval, or reporting details
remain open, the process records the gate and exposes the pending or blocked
branch.

## 3. Purpose and desired outcomes

The Sales domain must provide the following outcomes:

- a Tenant-scoped B2B customer-account process with traceable commercial
  documents and external-party identity;
- quotations and revisions that preserve source and revision history;
- sales orders that are validated against current authorized master and policy
  facts before confirmation;
- controlled demand and fulfillment handoffs to Inventory, including partial
  quantities, exceptions, reservation release, and delivery evidence;
- invoice requests that contain sufficient source facts for Finance to decide
  whether AR, revenue, and tax posting is valid;
- receipt visibility and allocation links that never bypass Finance ownership;
- returns and credit-note requests that link to original documents and
  distinguish commercial authorization, physical disposition, and financial
  correction;
- server-derived authorization, separation of duties, append-only audit,
  concurrency protection, idempotent retries, and explainable unknown
  outcomes;
- reports and reconciliation views that retain source lineage and never cross
  Tenant or unauthorized Company/Branch/Warehouse scope; and
- an explicit handoff for unresolved MESP-23 decisions and external
  validation, with no silent policy invention.

## 4. Scope

### 4.1 In scope for this B2B BRD

- Business Customer account use, contacts, addresses, status, commercial
  eligibility, and source references;
- quotation preparation, revision, expiry, approval branch, sending evidence,
  and conversion;
- sales order creation, validation, approval branch, confirmation,
  cancellation before posting, and lifecycle;
- customer, Product/Item, Category, UOM, price, discount, tax, availability,
  credit, Company, Branch, Warehouse, currency, and requested-date checks;
- Inventory reservation and fulfillment handoff, including partial delivery,
  release, backorder and substitution as explicit policy branches;
- delivery evidence and Sales status synchronization without owning the
  physical stock ledger;
- invoice-request preparation from approved shipment quantities or an
  authorized service milestone, with Finance validation and posting;
- customer receipt visibility, partial or on-account outcomes, allocation,
  reversal, and reconciliation handoffs owned by Finance;
- customer return authorization, partial return, Inventory disposition
  handoff, credit-note request, and refund or settlement branch;
- authority, approval, delegation gate, separation of duties, numbering,
  cancellation, immutable history, correction, audit, notifications,
  reports, import/export, integrations, observability, recovery,
  migration, and external-validation boundaries.

### 4.2 Required B2B chain

The normal commercial chain is:

1. configure or consume an eligible Business Customer account;
2. prepare and revise a quotation;
3. obtain the applicable approval or record that no approval is required
   under an approved policy;
4. send or otherwise record the quotation outcome;
5. convert an accepted quotation, or create a direct order if an approved
   policy permits, while preserving the source;
6. validate and confirm the sales order;
7. request, reserve, allocate, and fulfill eligible quantities through
   Inventory;
8. record delivery evidence and any partial, backorder, or exception result;
9. request invoicing from the approved delivery quantity or authorized
   service milestone;
10. allow Finance to validate and post the invoice and related AR, revenue,
    and tax effects where its policy permits;
11. record and allocate the customer receipt through Finance;
12. reconcile the order, delivery, invoice, receipt, and outstanding balance;
13. authorize a customer return where applicable; and
14. process physical disposition and a linked Finance credit or refund
    correction without editing posted history.

### 4.3 Explicit exclusions

This BRD does not include:

- Retail POS, cashiers, cashier sessions, tills, retail checkout, store cash
  drawers, walk-in consumers, loyalty, promotions, or retail price behavior;
- a customer portal, customer login, external Tenant membership, or customer
  credentials;
- Wafra-specific product, tax, workflow, or integration behavior;
- Supplier identity or Purchase-to-Pay ownership, except the documented
  upstream continuity boundary;
- Product, Item, SKU, barcode, Category, UOM, Business Customer, tax master,
  currency, exchange-rate, or Payment Term master-data implementation;
- Finance chart of accounts, account mappings, posting dimensions, fiscal-year
  mechanics, year-end close, retained earnings, payment methods, or rate
  mechanics;
- Inventory stock ledger, valuation algorithm, tracking policy, warehouse
  execution, or physical disposition policy;
- implementation specifications, source code, EF entities, tables,
  migrations, endpoints, APIs, UI, providers, infrastructure, production
  environments, or automated-test behavior;
- statutory, tax, ZATCA, banking, privacy, legal, or Saudi compliance
  conclusions; and
- Currency, MESP-36 Reporting and Analytics, MESP-37 Saudi/localization, or
  any next task.

## 5. Authority, source baseline, and traceability

### 5.1 Source priority

When sources disagree, the higher source controls and the discrepancy remains
visible:

1. named Owner approval or an approved Product Decision Register record;
2. canonical approved PRD v1.2, docs/MESP_PRD_v1.2.docx;
3. approved owning or upstream BRD;
4. approved ADR and Foundation Release 1 constraints;
5. approved glossary and the live MESP-23 register;
6. Product Delivery Master Plan and other planning material.

The PRD is a product baseline, not an implementation authorization. A
recommended default, common ERP practice, or existing source behavior cannot
close an open decision.

### 5.2 Primary PRD anchors

| Anchor | Sales requirement carried into this BRD |
|---|---|
| SAL-001 | Authorized internal users manage or consume a Business Customer account with identity, addresses, tax attributes, contacts, Payment Term reference, price-list reference, credit limit, status, and supporting evidence, subject to the owning master-data boundaries. |
| SAL-002 | Quotations can be created, revised, approved where required, expired, sent, and converted while preserving revision history and source links. |
| SAL-003 | Sales orders validate customer status, price, discount authority, tax, availability, requested dates, fulfillment location, currency, and credit policy before confirmation. |
| SAL-004 | Confirmed orders hand off eligible demand to Inventory for reservation and fulfillment, including release of reservations and partial allocation or fulfillment where policy allows, with delivery evidence. |
| SAL-005 | Invoices are requested from approved shipment quantities or authorized service milestones, with configurable partial invoicing, taxes, discounts, and rounding governed by the approved Finance and tax policy. |
| SAL-006 | Customer receipts can be recorded, allocated to one or more invoices, partially settled, held on account or unresolved, reversed, and reconciled under Finance control. |
| SAL-007 | Returns and credit notes carry reason, authorization, source links, disposition, stock effects, and balanced accounting correction responsibilities across Sales, Inventory, and Finance. |
| SAL-008 | Credit exposure is calculated from open invoices and any approved commitments; the warning, block, and override behavior is a named policy gate. |
| BR-005 | Sales preserves the upstream and downstream source chain across the approved B2B transaction flow and does not absorb Procurement, Inventory, or Finance ownership. |
| BR-009 | Transaction, base, and applicable reporting currency facts and posting-time rate evidence are preserved; currency and rate policy remain Finance-owned and open where named. |

### 5.3 Related approved baselines

| Source | Boundary carried into this BRD |
|---|---|
| docs/21_Procurement_and_Purchase_to_Pay_BRD.md | Procurement owns the supplier commercial chain. Sales may consume product, supplier, or inbound availability facts only through the approved handoff; Sales does not create a supplier obligation or change a receipt. |
| docs/22_Inventory_and_Warehouse_Management_BRD.md | Inventory owns reservation facts, physical delivery posting, stock ledger, tracking, valuation, and physical return disposition. Sales owns the commercial request and status link. |
| docs/23_Finance_and_Accounting_BRD.md | Finance owns AR, revenue, tax, receipts, allocations, periods, currency and rates, posting, reversal, and financial reconciliation. Sales supplies source facts and does not assign GL truth. |
| docs/00_ERP_Business_Glossary.md | Business Customer is an external B2B party, not a User; Product and Item are one Release 1 identity; documents and posted financial history are immutable; Payment Term, credit, tax, receipt, reconciliation, exchange rate, and rounding decisions remain open where named. |
| docs/16_Master_Data_and_Product_Catalog_BRD.md | Product, Category, UOM, price-list, tax, and customer master facts are consumed as approved, effective, Tenant-scoped facts; this BRD does not redefine their identity or persistence. |
| docs/15_Foundation_Release_1_Lean_Implementation_Specification.md | Tenant context, downward Company/Branch/Warehouse scope, server-derived authorization, audit, concurrency, idempotency, background-work revalidation, and production gates apply to every Sales action. |
| docs/Decisions.md and applicable ADRs | ADR-002, ADR-004, ADR-006, ADR-007, ADR-008, ADR-009, ADR-011, ADR-014, ADR-015, ADR-016, ADR-017, and ADR-018 constrain ownership, authorization, durability, files, localization, isolation, integration authentication, and validation. |
| docs/94_Product_Delivery_Master_Plan.md | MESP-25/MESP-26 sequence and entry gate, one active item, Phase 2 BRD exit, and the separate MESP-36 handoff. |
| MESP-23 live register | All open Sales-affecting decisions remain open. MESP-52 / PD-020 and MESP-56 / PD-021 are preserved exactly and are not extended by this BRD. |

### 5.4 Trace convention

Every business requirement in a later implementation specification must retain
its source anchor, decision status, owning module, Tenant and organizational
scope, audit event, and correction path. An unapproved policy branch must
carry its gate into the implementation readiness record. A source snapshot
must be retained where a later master or policy change must not rewrite the
historical commercial document.

## 6. Business actors and authority

### 6.1 Actors

| Actor | Business role | Access boundary |
|---|---|---|
| Sales preparer | Creates or revises quotations and orders, requests fulfillment and invoicing, records commercial explanations, and monitors exceptions. | Authenticated internal User with exact server-derived Sales permission and Tenant/Company/Branch scope. |
| Sales approver | Approves a quotation, order, discount, credit override, return, or other action only if an approved policy requires that authority. | Named permission and scope; no self-approval; exact catalogue remains open where MESP-55 applies. |
| Sales manager or delegated authority | Performs an approved approval or override within the policy and time/scope boundary. | Delegation is a gate under MESP-55; no default delegation authority is invented here. |
| Finance preparer | Validates or prepares AR, tax, receipt, allocation, credit, and correction inputs in the Finance process. | Finance permission and Company/Legal Entity scope; Sales cannot substitute for Finance posting authority. |
| Finance approver/poster | Approves or posts financial documents and corrections under Finance SoD. | Finance-owned approval/posting policy, closed-period control, and exact permission. |
| Inventory operator | Reserves, allocates, picks, packs, delivers, receives, and disposes of physical stock under Inventory policy. | Inventory permission and Branch/Warehouse scope. |
| Inventory supervisor | Resolves physical exceptions or approves disposition where the Inventory baseline requires it. | Inventory-specific authority; Sales cannot approve physical stock movement. |
| Business Customer | External B2B counterparty named on commercial documents. | No platform User, login, Tenant membership, credential, session, or implicit portal access. |
| Customer Contact | External contact person for commercial communication and evidence. | No platform User or session through this BRD. |
| Procurement user | Maintains upstream purchasing facts or inbound commitments that may affect availability. | Procurement scope only; no Sales authority inferred. |
| Tenant/Company administrator | Maintains approved organizational or reference configuration. | Administrative permission does not grant Sales, Finance, Inventory, or posting authority automatically. |
| Auditor or support user | Reads permitted evidence or investigates a controlled exception. | Read-only or explicitly approved support grant; no broad Tenant access or authority escalation. |
| External validator | Qualified Saudi, tax, banking, legal, privacy, security, or other specialist who validates a named gate. | Validation evidence only; no platform permission is implied. |

### 6.2 Authorization invariants

- The authenticated session, active membership, role, permission, and
  Company/Branch/Warehouse scope are server-derived.
- A client-supplied Tenant, Company, Branch, Warehouse, role, approval flag,
  discount authority, credit result, or posting state cannot create authority.
- Every create, read, search, export, notification, job, file, and integration
  path fails closed on cross-Tenant scope.
- A Tenant may contain multiple Companies/Legal Entities. MESP-56 / PD-021
  remains the approved boundary: each Company is a separate
  accounting/legal boundary, with no Release 1 consolidation, intercompany,
  elimination, transfer-pricing, or consolidated-statement behavior.
- A Branch and Warehouse are downward operating scopes. Sales cannot infer
  stock ownership from Branch membership or read another Company by
  convenience.
- A user who prepares a document cannot approve or post the same controlled
  effect where SoD requires separation. The exact Sales approval catalogue
  and delegation policy remain open where named.
- Background work revalidates stored Tenant, organization, authority, and
  lifecycle state before effect. It does not fall back to a global or
  ambient Tenant.

## 7. Core business vocabulary

The approved glossary controls terms. This section applies them to the
Sales chain:

- **Business Customer:** external B2B counterparty in the Tenant. It is not a
  platform User, a consumer, or an authenticated customer account.
- **Quotation:** commercial proposal with lines, terms references, validity
  evidence, revisions, and an outcome. It is not an invoice or AR posting.
- **Sales Order:** customer commitment accepted under the applicable policy,
  with source quotation or direct-order evidence, requested fulfillment, and
  validation results.
- **Reservation:** Inventory-controlled commitment of eligible stock. It is
  not a stock decrease or a financial posting.
- **Delivery:** commercial and physical handoff evidence. Inventory owns the
  physical posting; delivery alone does not automatically create AR or
  revenue.
- **Sales Invoice:** a commercial request/document whose AR, revenue, tax,
  period, currency, and posting truth are Finance-controlled.
- **Customer Receipt:** a Finance-controlled money-received event that may be
  allocated, held on account, remain unapplied, be unresolved, or be
  reversed according to approved policy.
- **Customer Return:** a Sales-authorized commercial return request linked to
  the source delivery or invoice. Inventory controls physical acceptance and
  disposition; Finance controls credit/refund and accounting correction.
- **Credit Note:** a linked forward correction to the customer obligation,
  subject to Finance and tax policy; it is not an edit of the original
  posted invoice.
- **Credit Exposure:** the calculated customer obligation and approved
  commitment view used by credit-control policy. MESP-46 remains open.

## 8. Domain ownership and end-to-end invariants

### 8.1 Ownership matrix

| Business fact or action | Sales | Product / Business Parties | Inventory | Finance | Procurement | Reporting |
|---|---|---|---|---|---|---|
| Customer identity and master status | Consumes and uses | Owns approved master boundary | Validates where needed | Uses counterparty identity | Uses supplier identity separately | Reads permitted snapshot |
| Product, Item, SKU, Category, UOM | Consumes | Owns master identity and effective facts | Consumes for stock | Consumes for accounting mapping | Consumes for purchasing | Reads approved facts |
| Price list and discount authority | Uses in quote/order | Owns reference facts where approved | No ownership | Validates financial/tax effects | No ownership | Reports effective source |
| Credit limit and exposure | Requests and displays result | Customer source may be maintained in owning boundary | Supplies inventory commitment facts if approved | Validates financial exposure and override effect | No ownership | Reports approved exposure |
| Quotation and sales order | Owns commercial document | Provides master facts | Receives eligible demand | Receives source facts | May provide upstream continuity | Reads source |
| Reservation, allocation, stock availability | Requests and displays | Product identity only | Owns physical and reservation facts | May consume valuation/accounting events | May supply inbound facts | Reads posted facts |
| Delivery and physical quantity | Owns commercial orchestration and evidence link | No ownership | Owns physical posting and ledger | Receives approved event if policy requires | No ownership | Reads source and outcome |
| Invoice, AR, revenue, tax | Prepares source request | Provides approved references | Supplies physical evidence | Owns validation, posting, correction, and truth | Owns AP separately | Reads Finance-controlled facts |
| Receipt, allocation, settlement | Displays status and requests follow-up | No ownership | No ownership | Owns event, allocation, reversal, and reconciliation | Owns supplier payment separately | Reads Finance-controlled facts |
| Return and credit | Owns commercial authorization and link | Provides master facts | Owns physical acceptance/disposition | Owns credit, refund, tax, and accounting correction | No ownership | Reads linked outcomes |
| Report catalogue and KPI definitions | Supplies Sales source meaning | Supplies master dimensions | Supplies physical facts | Supplies financial truth | Supplies P2P facts | Owns report publication boundary |

### 8.2 Non-negotiable invariants

1. A Sales document is Tenant-owned and cannot be read, changed, reported,
   exported, or notified across Tenant boundaries.
2. Every order line references an effective Product/Item and UOM fact. No
   Sales variant or separate Item identity is invented.
3. A quotation or order preserves the customer, line, price, discount,
   tax-reference, currency, requested date, organizational scope, and source
   facts used for its decision.
4. A reservation is not a stock decrease. Only Inventory can post physical
   stock movement.
5. A delivery is not automatically an AR, revenue, tax, receipt, or
   reconciliation result.
6. Finance alone decides whether an invoice, credit note, receipt, allocation,
   or correction produces a financial posting.
7. Posted financial history is immutable. Corrections are linked forward
   reversals or credit/correction documents.
8. Retrying a request or receiving a duplicate event produces one business
   effect, or an explicit unknown/reconciliation state.
9. A stale or conflicting source does not overwrite a newer decision.
10. Audit evidence is attributable, Tenant-scoped, append-before-effect where
    required, and retained behind MESP-50 controls.

## 9. Customer account and commercial preconditions

### 9.1 Customer account use

Before a quotation or order is accepted, Sales must resolve the Business
Customer from the authorized Tenant scope and validate the current status and
required commercial facts. The account may include:

- legal or trading identity and external reference;
- billing and delivery addresses;
- authorized contacts and communication evidence;
- tax attributes supplied by the owning master-data process;
- approved Payment Term reference, without defining its Release 1 shape;
- approved price-list reference;
- credit limit and exposure facts, subject to MESP-46;
- status, hold, suspension, or closure facts; and
- supporting documents or opaque file references, subject to MESP-50.

An inactive, closed, or otherwise ineligible customer cannot be silently
treated as active. The user receives a reason and a controlled exception
path. A customer master update cannot rewrite historical document snapshots.

### 9.2 Customer and organization checks

Sales validates:

- the customer belongs to the current Tenant;
- the customer is eligible for the Company/Legal Entity and requested Branch
  or delivery location;
- required contact, address, tax-reference, and commercial evidence exists
  when the approved policy requires it;
- the Company and Branch are active and accept new transactions;
- the Warehouse or fulfillment location is active and in scope;
- the user has exact Sales permission for the requested action; and
- no client-provided identity or scope field bypasses server-derived
  authorization.

The exact required fields remain the owning master-data or named policy
decision. Sales must expose a missing-fact gate rather than invent a
placeholder.

## 10. Quotation requirements

### 10.1 Quotation trigger and preconditions

An authorized Sales user may start a quotation for an eligible Business
Customer. The quotation must identify the Tenant, Company, customer,
contact, requested fulfillment context, lines, UOM, quantities, prices or
price references, discount facts, tax references where available, currency
facts, requested dates, validity evidence, and source or supporting evidence
needed by the approved policy.

The quotation must validate Product/Item and UOM identity, customer
eligibility, price-list or contract source, discount authority, tax
reference, currency acceptance, requested-date feasibility, and any
credit or approval precondition that the approved policy requires. A missing
or stale fact produces a visible pending or rejected outcome.

### 10.2 Revision and sending

- A revision creates a new traceable version or equivalent immutable
  revision evidence; it does not erase the previous proposal.
- The system records who prepared, revised, approved, rejected, sent,
  expired, or withdrew the quotation and why.
- Sending evidence identifies the recipient contact or external channel
  without granting that contact a platform session.
- A quotation may be converted only from the accepted or otherwise permitted
  revision, preserving the source revision and any approved snapshot.
- Expiry is a controlled outcome. The exact validity calculation and whether
  an expired proposal may be renewed or copied are policy details, not a
  hidden Sales default.
- A quotation cannot create stock, AR, revenue, tax, receipt, or GL truth.

### 10.3 Quotation approval and exceptions

If an approved policy requires approval, the quotation remains pending until
the named authority acts. Rejection records a reason and does not silently
convert the quotation. If no approval is required under an approved
catalogue, the evidence must say so.

Discount caps, margin thresholds, customer-specific pricing, validity
periods, and delegation are not invented here. They are represented as
inputs from approved policy or as explicit open gates. MESP-55 controls later
domain approval and delegation decisions; a quote prepared by a user cannot
be self-approved when the applicable SoD rule prohibits it.

## 11. Sales order requirements

### 11.1 Creation and source continuity

An order may originate from an accepted quotation or from a direct-order
path only if an approved policy allows direct orders. The order preserves:

- source quotation and revision, or the reason for an allowed direct order;
- Tenant, Company, Branch, and fulfillment Warehouse scope;
- Business Customer and contact snapshots;
- Product/Item, UOM, quantity, price, discount, tax-reference, currency, and
  requested-date snapshots;
- approval, credit, availability, and policy-check outcomes;
- idempotency and correlation evidence; and
- any external customer reference or communication evidence.

The order cannot silently inherit a later price, tax, currency, customer, or
Product change after confirmation. A user may request a new revision or
controlled change while the order remains in an allowed state.

### 11.2 Confirmation checks

Before confirmation, the server-derived Sales process validates:

1. Tenant, Company, Branch, and Warehouse scope;
2. user permission and any required approval state;
3. active customer, contact, address, and commercial eligibility;
4. active Product/Item, Category, UOM, and required tracking reference;
5. price source, discount authority, and effective-date validity;
6. tax category or tax rule availability, without claiming a legal result;
7. currency and rate facts accepted by Finance, without choosing rate policy;
8. requested date and fulfillment feasibility;
9. current Inventory availability or reservation response;
10. current credit exposure and the approved warning/block/override result;
11. duplicate or conflicting order detection; and
12. all required external evidence, attachment, or integration responses.

If a check cannot be made safely, confirmation is pending or rejected with a
reason. The client cannot force a confirmed state. Confirmation is not an
invoice, receipt, reservation posting, stock decrease, or GL posting.

### 11.3 Order change, cancellation, and closure

An unconfirmed order may be revised or cancelled under the applicable
permission. A confirmed order may be changed only through an approved
change path that revalidates affected price, quantity, credit, availability,
approval, and source facts. Already delivered, invoiced, posted, or settled
facts cannot be edited out of the chain.

Cancellation must identify the actor, reason, time, source, remaining
quantity, reservation release outcome, and downstream notifications. A
cancelled order cannot be reused to create a second unrelated commercial
commitment. Closure is permitted only when the approved process has resolved
its remaining quantities and linked financial or exception outcomes.

## 12. Pricing, discount, tax, currency, and Payment Term boundaries

### 12.1 Pricing and discount

Sales must preserve the price source, effective date, currency, UOM,
quantity basis, discount amount or percentage, and authority evidence used
for a quotation or order. It must not silently recalculate historical
documents when a price list changes.

Price-list structure, precedence between list, customer, contract, quantity,
manual, or promotional sources, and discount thresholds are not approved by
this BRD. The implementation-readiness gate must name the owning decision,
effective-date behavior, authorization, rounding interaction, and snapshot
rule before implementation.

### 12.2 Tax

Sales carries the required tax category, taxable basis, exemption or
supporting evidence, and calculation inputs to Finance. Sales does not decide
tax law, tax registration, tax rate, ZATCA behavior, invoice certification,
or statutory document numbering. MESP-49 and qualified Saudi/tax validation
remain open gates.

### 12.3 Currency and rate facts

Sales preserves the transaction currency and any Finance-supplied base or
reporting currency facts on the relevant source. It does not select a rate
source, cadence, effective-time rule, override authority, revaluation rule,
rounding policy, or reporting-currency policy. Those are Finance-owned and
remain open under MESP-54 and the Currency work that is intentionally not
executed in this session.

### 12.4 Payment Term and FIN-OD-09 / MESP-110

FIN-OD-09 / MESP-110 is an open, unapproved Finance dependency. This Sales
BRD does not define:

- the Payment Term Release 1 field shape;
- base date, interval, schedule, installment, discount, due-date, aging,
  settlement, or historical-preservation mechanics;
- fiscal-year, year-end close, P&L carry-forward, retained-earnings,
  reopen, reclose, or derived-reporting mechanics; or
- the Finance posting-dimension catalogue, including Cost Center policy.

Sales may reference an approved Payment Term identity and preserve the
effective source/version supplied by Finance. Until MESP-110 is approved,
the affected quote, order, invoice, aging, credit, and settlement behavior
must remain Finance-gated or pending. No default due date, payment schedule,
aging calculation, year-end behavior, or posting dimension is implied.

MESP-54 remains a separate open exchange-rate and reporting-currency
dependency. MESP-110 does not resolve MESP-54, and this BRD does not resolve
either decision.

## 13. Credit control

Credit control is a required decision point, not an invented policy. Sales
requests the current customer credit limit and exposure facts from the owning
or Finance-controlled process. The exposure view may include open invoices
and approved commitments only as defined by MESP-46.

The result must be one of the policy-supported outcomes, such as eligible,
warning, blocked, pending review, or unknown. The exact set and the
calculation are open. A warning must not be treated as a block, and a block
must not be bypassed by a client flag.

MESP-46 remains open for credit limit/exposure, enforcement, and override
policy. If an override is approved later, the process must record the
authority, reason, scope, time, source exposure, and audit evidence. This BRD
does not approve the Founder Decision Pack recommendation of a hard
confirmation check or a Finance override.

Credit status is revalidated at the points chosen by approved policy,
including order confirmation and any material change before fulfillment or
invoicing. A stale or unavailable credit result produces a controlled
pending/unknown outcome rather than an unsafe confirmation.

## 14. Reservation, availability, and fulfillment handoff

### 14.1 Sales-to-Inventory request

After order confirmation, Sales sends or records a demand request containing
the authorized Tenant and organization scope, order and line identifiers,
Product/Item and UOM, requested quantity, requested date, fulfillment
Warehouse, customer delivery context, source revision, correlation key,
idempotency key, and any approved tracking or delivery constraints.

Inventory validates the request using its own authority and current stock
ledger. Sales consumes the result; it cannot write availability, reservation,
or stock quantities directly. A reservation result is linked back to the
order and line, and its status is visible to the commercial process.

### 14.2 Partial allocation, backorder, substitution, and release

The process must represent full, partial, unavailable, pending, rejected,
released, and unknown outcomes where the approved Inventory policy supports
them. It must not assume:

- whether reservation occurs at quotation, order confirmation, or another
  event;
- whether partial allocation creates a backorder, split order, or pending
  line;
- whether a substitute Product/Item is allowed;
- whether a reservation expires or is manually released;
- whether negative stock is allowed; or
- whether an inbound Procurement commitment may satisfy a Sales order.

MESP-41 tracking, MESP-45 reservation/negative-stock policy, and MESP-46
credit-control interaction remain open. Sales exposes these as Inventory or
credit gates and preserves the response received.

### 14.3 Concurrency and stale availability

Availability is a time-sensitive fact. A response may become stale before
confirmation, allocation, picking, or delivery. Inventory must reject or
revalidate conflicting quantities. Sales must show the conflict, preserve
the original request and response, and require a controlled retry or user
decision. A stale client response cannot authorize a second reservation.

## 15. Delivery and partial fulfillment

### 15.1 Delivery responsibility

Sales coordinates the commercial delivery request and consumes the
Inventory-posted delivery evidence. Inventory owns physical pick, pack,
ship, accepted quantity, tracking, Warehouse movement, immutable stock
ledger, and valuation evidence. A Sales user cannot mark stock delivered
merely by changing a commercial status.

The delivery link must identify the order and line, delivered and remaining
quantities, UOM, Warehouse, delivery date, customer or carrier evidence,
Inventory event, exception, and source correlation. The exact evidence
requirements remain subject to the approved Inventory and external policy.

### 15.2 Partial delivery

The process supports a delivery quantity less than the confirmed quantity
when the approved policy permits it. It must preserve:

- ordered, reserved, allocated, delivered, invoiced, returned, and remaining
  quantities by line;
- each delivery or split source;
- any backorder, substitution, cancellation, or exception result;
- the invoice eligibility of the delivered quantity; and
- the downstream Inventory and Finance responses.

No remaining quantity is silently cancelled, re-reserved, invoiced, or
returned. A partial result may be pending if Inventory or Finance has not
confirmed the required event.

### 15.3 Delivery failure and unknown outcomes

If the delivery request or downstream event fails, Sales records the
failure and prevents a false delivered state. A timeout or lost response is
an unknown outcome until the authoritative Inventory status is queried or
reconciled. Retrying with the same idempotency and correlation information
must not duplicate physical movement or commercial delivery.

## 16. Invoicing and Finance AR handoff

### 16.1 Invoice request source

Sales prepares a Finance invoice request from:

- an approved delivery or shipment quantity; or
- an authorized service milestone where the relevant policy explicitly
  permits milestone invoicing.

The request identifies Tenant, Company/Legal Entity, customer, source order,
delivery or milestone, lines, quantities, UOM, price and discount snapshots,
tax inputs, currency facts, Payment Term reference if approved, requested
invoice date, source revision, and audit/correlation evidence.

Sales does not decide whether the request is a valid AR, revenue, tax,
period, currency, or posting event. Finance validates the source, account
mapping, tax, period, currency/rate, dimensions, duplicate status, and
posting eligibility under its own approved policy.

### 16.2 Partial invoicing

Partial invoicing is represented by the relationship between eligible
delivered or milestone quantities and invoiced quantities. The exact
frequency, minimum quantity, advance or deposit treatment, service
milestone evidence, and rounding behavior remain subject to Finance,
commercial, tax, and MESP-110/MESP-54 decisions.

Sales must prevent duplicate invoice requests for the same eligible source
quantity. A rejected, pending, or unknown Finance result remains visible and
does not create a false invoiced state.

### 16.3 AR, revenue, tax, and posting boundary

The Finance BRD governs:

- whether and when AR is recognized;
- whether revenue follows shipment, service milestone, or another approved
  event;
- output-tax calculation and posting;
- account and posting-dimension mapping;
- fiscal-period acceptance;
- transaction, base, and reporting currency/rate facts;
- balanced journal creation;
- correction, reversal, and immutable posted history; and
- reconciliation of the source to Finance truth.

Sales supplies source facts and displays the Finance result. It does not
assign accounts, journal lines, revenue timing, tax law, due dates, periods,
Cost Centers, or posting status.

## 17. Customer receipts, allocation, and reconciliation

### 17.1 Receipt handoff

Customer receipts are Finance/Treasury-owned events. Sales may request a
follow-up, display a Finance-controlled status, or supply the customer and
invoice context. The Finance process determines the accepted payment method,
bank/cash evidence, date, currency, amount, receipt identity, and posting.

MESP-47 remains open. This BRD does not approve cash, bank transfer, gateway,
feed, deposit, or any other method catalogue, and it does not create a
cashier or retail cash process.

### 17.2 Partial, on-account, unapplied, unidentified, and unknown outcomes

The commercial process must represent the Finance result when a receipt:

- settles one invoice partially;
- settles multiple invoices;
- remains on account;
- remains unapplied or unidentified;
- is rejected, reversed, or corrected; or
- has an unknown external outcome after a timeout or integration failure.

Sales cannot mark an invoice paid based on a client assertion or an
unconfirmed external response. An unknown receipt must be reconciled by the
Finance-controlled process before it is treated as settled.

### 17.3 Allocation and reconciliation

Finance owns allocation, settlement, unapplied balance, aging, receipt
reversal, bank or cash reconciliation, and financial truth. Sales provides
the source links needed to reconcile:

Quotation -> Sales Order -> Reservation/Delivery -> Sales Invoice ->
Customer Receipt -> Allocation -> Return/Credit correction.

MESP-53 remains open for the report and reconciliation catalogue. The
minimum business view must make missing, duplicate, partial, reversed,
unapplied, and inconsistent links visible without rewriting the source
documents.

## 18. Returns, credit notes, and correction

### 18.1 Commercial return request

Sales may initiate a customer return request only for an eligible source
delivery, invoice, or order outcome and must capture customer, Company,
source line, quantity, UOM, reason, requested date, evidence, and authority.
The request may be pending approval or Inventory/Finance validation.

The exact return window, reason catalogue, authorization, restocking,
replacement, refund, credit, shipping, and tax behavior are not silently
chosen by this BRD. A missing policy is a named gate.

### 18.2 Inventory physical acceptance

Inventory decides whether the physical quantity is received, rejected,
quarantined, inspected, restocked, scrapped, or otherwise disposed of under
its approved policy. Sales consumes the result and preserves the original
request, received quantity, disposition, and exception. A return request
does not implicitly put stock back or reduce stock.

Partial returns are supported where policy permits. The returned quantity
cannot exceed the eligible source quantity unless a separately approved
exception permits it. Tracking and condition evidence follow Inventory
policy, including the open MESP-41 gate.

### 18.3 Finance credit or refund correction

Finance decides whether the return produces a credit note, refund, receipt
reversal, account adjustment, tax correction, or another approved financial
effect. The correction must link to the original invoice and return
evidence, preserve the original posted history, and use forward-only
correction or reversal.

Sales does not approve a refund, choose a tax treatment, alter AR, or edit
the original invoice. MESP-47, MESP-49, MESP-54, MESP-53, and FIN-OD-09 /
MESP-110 remain applicable gates.

## 19. Lifecycle and status semantics

The following semantic lifecycle is required. The final status catalogue and
transition permissions must be confirmed in implementation readiness:

| Object | Semantic states and controls |
|---|---|
| Quotation | Prepared/revised, pending approval, approved or permitted, sent, expired, rejected, withdrawn, converted, or cancelled; every transition is authorized and audited. |
| Sales Order | Prepared, pending checks/approval, confirmed, partially fulfilled, fulfilled, invoicing pending, partially invoiced, completed, cancelled, rejected, or exception/unknown; confirmation requires all applicable checks. |
| Reservation request | Requested, pending, fully reserved, partially reserved, rejected, released, expired if approved, or unknown; Inventory owns physical reservation truth. |
| Delivery | Requested, allocated, partially delivered, delivered, rejected, cancelled, or unknown; Inventory owns posted physical evidence. |
| Invoice request | Prepared, pending Finance validation, accepted/posted by Finance, partially invoiced, rejected, cancelled before posting, or unknown; Finance owns financial status. |
| Customer receipt | Pending, recorded, partially allocated, allocated, on account, unapplied/unidentified, reversed, rejected, or unknown; Finance owns the event. |
| Customer return | Requested, approved if required, awaiting receipt, partially received, received, rejected, disposed, credited/refunded, cancelled, or exception; Inventory and Finance own their respective results. |

Status changes must be monotonic with respect to posted facts. A displayed
status cannot override an authoritative downstream event. Reopening,
reprocessing, or reusing a document is a policy-controlled action with
reason and audit; no posted record is edited.

## 20. Numbering, cancellation, and immutable history

- Sales quotation, order, delivery request, invoice request, return, and
  credit-related source references require a Tenant/Company/document-type
  traceable business identifier.
- Document numbers are not reused after issuance. Exact sequence, gap,
  statutory, and Saudi requirements remain MESP-49 and external-validation
  gates; this BRD makes no gapless or statutory claim.
- A number and source identity are assigned by the server-authorized process,
  not by an untrusted client.
- Cancellation before posting is allowed only from an eligible state and
  records actor, reason, time, affected quantity, downstream outcome, and
  notification.
- Posted Finance documents and Inventory ledger entries are immutable. A
  later return, credit, reversal, or correction links to the source and
  preserves both records.
- A historical snapshot retains the customer, Product, UOM, price, discount,
  tax input, currency, organizational scope, source policy version, and
  authority facts needed to explain the original decision.

## 21. Permissions, approvals, separation of duties, and delegation

### 21.1 Permission families

The exact catalogue is owned by the platform authorization and domain
decision process. The business actions that require distinct authorization
are:

- view, create, revise, send, withdraw, expire, and convert quotations;
- create, change, confirm, cancel, and close sales orders;
- approve a quote, order, discount, credit override, or return where policy
  requires approval;
- request or release a reservation and submit a delivery exception;
- request invoicing and view Finance results;
- initiate a return or credit request;
- view customer exposure, invoices, receipts, and reconciliation evidence;
- export, import, attach, notify, retry, reconcile, or resolve an unknown
  outcome; and
- administer configuration, permissions, or Tenant/Company scope.

Permission names, roles, and screen/API mappings are not defined here.
Server-side enforcement is required for every path.

### 21.2 Approval and SoD

Approval is a business decision distinct from preparation, posting, receipt
allocation, reconciliation, and administration. The same user cannot perform
incompatible actions when the approved SoD policy prohibits it. Finance
approval/posting and Inventory physical approval remain outside Sales.

MESP-55 remains open for later domain approval and delegation. This BRD
therefore uses an approval gate without selecting one approver, threshold,
reassignment, escalation, duration, or delegated authority rule. Any future
delegation must be explicit, bounded, auditable, and checked by the
server-derived authorization seam.

## 22. Validation, concurrency, idempotency, and failure

### 22.1 Validation

Validation is performed at the authoritative boundary and again whenever a
time-sensitive fact can have changed. The process validates identity,
Tenant/scope, lifecycle, source, quantity, UOM, price, tax input, currency,
authority, approval, credit, availability, duplicate status, and downstream
response. A client-side validation result is advisory only.

### 22.2 Concurrency

Conflicting changes to a quotation, order, reservation, delivery, return,
or invoice request produce a conflict outcome. The process preserves the
winning authoritative version and asks the user to review or create a new
controlled revision. Last-writer-wins is not an acceptable business rule for
quantities, approvals, credit overrides, posted facts, or source links.

### 22.3 Idempotency and duplicate effects

Every externally retried command or event carries a stable Tenant-scoped
idempotency and correlation identity. A duplicate request returns the
existing outcome or an explicit conflict; it does not create a second quote,
order, reservation, delivery, invoice request, receipt, credit, notification,
or audit effect.

### 22.4 Failure and unknown outcomes

The process distinguishes validation rejection, authorization denial,
business hold, dependency failure, retryable failure, dead-lettered work,
and unknown outcome. A timeout is not success. The user sees the authoritative
next action, and a reconciliation path can query or repair the link without
duplicating a downstream effect.

### 22.5 Notifications and recovery

Notifications are derived from an authorized source event and are
Tenant-scoped. A notification failure must not roll back a committed
business fact or falsely report delivery. Retry, dead-letter, replay, and
manual recovery preserve correlation, actor, source, attempt, and outcome
evidence under ADR-007/008 and the MESP-48/MESP-50 gates.

## 23. Cross-module handoffs

### 23.1 Product, Item, Category, UOM, and Business Customer

Sales consumes the approved Tenant-scoped Product/Item identity, SKU,
barcode/reference, Category, UOM, price, tax, and Business Customer facts.
Product and Item remain one Release 1 master-data identity; no variant entity
or variant behavior is created here. Sales cannot make a tenant-unique SKU,
barcode, Product, Category, UOM, or customer identity by bypassing the owning
module.

Customer contacts and suppliers are external business parties. Neither
becomes a platform User through a Sales document. Historical commercial
documents preserve source snapshots even when current master records later
change.

### 23.2 Organization and Company structure

Sales documents are owned by a Tenant and assigned to one authorized
Company/Legal Entity and applicable Branch/Warehouse scope. MESP-56 / PD-021
is exact and remains unchanged: multiple Companies may exist inside one
Tenant, but Release 1 does not consolidate or perform intercompany,
elimination, transfer-pricing, or consolidated-statement behavior.

Inactive or closed organizational units reject new transactions according to
the Foundation baseline. A user cannot move an order across Companies by
changing a request field. Any permitted transfer or correction is a named
business policy and is not invented here.

### 23.3 Procurement

Sales may consume approved inbound commitments, expected availability, or
supplier-related source evidence only through the Procurement/Inventory
handoff. A purchase request, purchase order, supplier confirmation, goods
receipt, or supplier invoice is not a Sales document. Sales cannot create AP,
change supplier stock receipt, or use a procurement recommendation as a
confirmed availability fact without the authoritative Inventory response.

### 23.4 Inventory

The Sales-to-Inventory contract is a business handoff containing order
identity, line quantities, UOM, fulfillment scope, dates, source policy,
correlation, and idempotency evidence. Inventory returns reservation,
allocation, delivery, physical return, exception, and valuation-relevant
facts. Inventory owns the immutable stock ledger and moving-weighted-average
evidence defined by its approved BRD; Sales never recomputes or overwrites
those facts.

### 23.5 Finance

The Sales-to-Finance handoff preserves the source order, delivery or
milestone, customer, Company, quantities, totals, tax inputs, currency,
Payment Term reference if approved, source snapshots, approval evidence,
correlation, and idempotency. Finance validates and owns AR, revenue, tax,
receipts, allocation, periods, rates, dimensions, posting, reversal, and
reconciliation.

Sales receives authoritative Finance outcomes and may show them in
commercial views. It must not assign GL accounts, post revenue, mark a
receipt settled, choose a due date, infer a year-end result, or repair a
financial document by editing Sales history.

### 23.6 Reporting and notifications

Sales publishes traceable source facts to the approved reporting boundary.
Reports must use the user's server-derived Tenant and organizational scope,
show source freshness and unknown states, and distinguish commercial status
from Inventory or Finance truth. MESP-53 remains open for the detailed report
catalogue and reconciliation rules. Notifications never widen access.

## 24. Saudi, localization, and external-validation boundary

Release 1 is B2B ERP. Saudi launch or localization is not a legal conclusion
in this BRD. Before any affected production or launch gate, qualified owners
must validate:

- tax registration, tax categories, tax calculation, exemptions, and
  supporting evidence;
- invoice and credit-note content, numbering, retention, and any electronic
  invoicing or ZATCA integration;
- date, time, language, currency, rounding, and address requirements;
- banking, payment, receipt, refund, and external-provider controls;
- privacy, retention, purge, residency, audit, and legal-hold obligations;
- external integration authentication, signing, sequencing, replay, and
  credential boundaries; and
- any country-specific return, credit, customer, or commercial requirement.

MESP-49, MESP-50, ADR-011, ADR-015, and the applicable qualified external
evidence remain gates. This BRD does not claim statutory compliance or
approve a Saudi behavior.

## 25. Reports, KPIs, audit, and reconciliation

### 25.1 Minimum Sales views

Subject to MESP-53 and the later Reporting work, the business must be able
to obtain Tenant-scoped views for:

- quotation volume, value, revision, approval, expiry, conversion, and
  rejection;
- order value, status, customer, Product, requested date, Company, Branch,
  Warehouse, credit result, and exception;
- reservation and fulfillment progress, partial quantities, backorders or
  policy exceptions, and delivery aging;
- invoiced versus delivered quantities and pending Finance outcomes;
- customer receipts, allocated, unapplied, on-account, reversed, and unknown
  status as supplied by Finance;
- customer returns, disposition, credits, refunds, and outstanding
  quantities;
- customer credit exposure and blocked or pending orders, subject to
  MESP-46;
- order-to-delivery-to-invoice-to-receipt reconciliation; and
- Product, customer, Company, Branch, Warehouse, and source-policy analysis.

Exact formulas, financial measures, fiscal-year/year-end behavior, reporting
currency, exchange-rate use, and report catalogue are not defined by this
BRD.

### 25.2 Audit evidence

The audit trail records at least Tenant, Company, Branch/Warehouse where
relevant, actor or authenticated service identity, action, object and
version, source, timestamp, old/new decision facts where permitted, reason,
approval, correlation, idempotency, dependency response, and recovery
outcome. Audit records are themselves protected from silent mutation and
subject to MESP-50 retention, privacy, purge, legal-hold, residency, backup,
and restoration gates.

### 25.3 Reconciliation

Reconciliation identifies missing, duplicate, stale, contradictory, partial,
reversed, or unknown links across the commercial, physical, and financial
chain. It must not resolve an inconsistency by deleting a source, editing a
posted document, or inventing a receipt, delivery, rate, tax, or credit
outcome. The authoritative owning process supplies the correction.

## 26. Imports, exports, integration, and observability

### 26.1 Import

An import is a controlled, Tenant-scoped command with an authorized
initiator, source file/reference, schema/version, row-level validation,
duplicate handling, idempotency, error report, and audit. It cannot bypass
customer, Product, price, tax, credit, approval, Inventory, or Finance
checks. MESP-51 remains open for migration and opening balances; no
historical Sales migration scope is approved here.

### 26.2 Export

An export is authorized by the user's server-derived scope and records the
filter, source time, data classification, recipient, format, and audit.
Exports do not reveal another Tenant or unauthorized Company/Branch/Warehouse
and do not expose private attachments without permission.

### 26.3 External integrations

An integration boundary must define authenticated caller, Tenant and
organization scope, schema/version, source identity, correlation and
idempotency, signing or credential ownership, rate limiting, timeout,
retry/dead-letter/replay, sequence, observability, and unknown outcome
handling before implementation. Webhooks cannot be trusted merely because a
payload says it is successful. External customer communication does not
create a User.

### 26.4 Operational evidence

The later implementation must emit correlation, authorization decision,
dependency, validation, retry, dead-letter, replay, and reconciliation
evidence appropriate to the action. Production telemetry, volume, retention,
backup, recovery, provider, and residency readiness remain MESP-48,
MESP-50, ADR-010, ADR-012, ADR-014, and other qualified gates. This BRD
does not claim those gates are complete.

## 27. Migration and opening-state boundary

MESP-51 remains open. This BRD does not choose whether migration includes
customers, Products, price lists, open quotes, open orders, reservations,
deliveries, invoices, receipts, credit, returns, or historical documents.
It does not choose opening AR, credit exposure, inventory quantities, tax
history, exchange rates, or reconciliation evidence.

Any later migration plan must define source authority, field mapping,
effective dates, Tenant and Company ownership, duplicate handling,
idempotency, rejected rows, audit, privacy, retention, cutover, rollback,
reconciliation, and dependency order with Inventory and Finance. No source
data may be overwritten or purged by this BRD.

## 28. Production and implementation-readiness gates

Before any Sales implementation or production launch, the owning plan must
show:

- approved Sales requirements and all named Owner decisions;
- server-derived Tenant, Company, Branch, Warehouse, permission, approval,
  SoD, and delegation behavior;
- approved Product/Item, Category, UOM, Business Customer, price, tax,
  Payment Term, credit, reservation, tracking, receipt, return, and
  reconciliation contracts;
- Finance approval for AR, revenue, tax, periods, currencies, rates,
  dimensions, Payment Terms, receipts, allocations, reversal, and
  correction mappings;
- Inventory approval for reservation, allocation, tracking, delivery,
  partials, negative stock, return disposition, valuation, and
  reconciliation facts;
- MESP-48 supported volume and performance evidence;
- MESP-49 qualified Saudi/tax/localization evidence;
- MESP-50 retention, privacy, legal hold, purge, residency, backup, and
  restoration evidence;
- ADR-011/015/017/018 and production provider/integration gates;
- concurrency, idempotency, failure, unknown, retry, dead-letter,
  reconciliation, and recovery evidence; and
- migration, report, notification, security, and Tenant-isolation review.

This MESP-35 session does not execute or satisfy those gates.

## 29. Given / When / Then acceptance scenarios

The following scenarios are business acceptance coverage for a later
implementation specification. They do not select any open policy option.

### 29.1 Customer, quotation, and order

1. **Create a quotation.** Given an authorized Sales user, an active
   Tenant-scoped Business Customer, and valid Product/UOM facts, when the
   user prepares a quote, then the quote stores the customer, source,
   organization, line, price, tax input, currency, validity, and audit facts.
2. **Reject a cross-Tenant customer.** Given a customer belonging to another
   Tenant, when it is supplied to a quote or order, then the request is
   denied and no customer or document data is disclosed.
3. **Preserve a revision.** Given an existing quote, when an authorized user
   changes a line or commercial fact, then the prior revision remains
   traceable and the new revision records its actor and reason.
4. **Pending quote approval.** Given a policy that requires approval, when a
   quote is submitted, then it remains pending until the named authority
   acts and cannot be converted as approved.
5. **Reject self-approval.** Given a user who prepared a quote and a policy
   requiring separation, when that user attempts approval, then the action
   is denied and audited.
6. **Expire a quote.** Given a quote whose approved validity has ended, when
   a user attempts conversion, then the system exposes the expiry outcome and
   requires the approved renewal or new-revision path.
7. **Convert the accepted revision.** Given an accepted quote revision, when
   an authorized user converts it, then the order preserves the exact source
   revision and relevant snapshots.
8. **Direct-order gate.** Given no quotation, when a direct order is
   attempted, then it is allowed only if an approved direct-order policy
   exists; otherwise the request is rejected with the missing gate.
9. **Reject an inactive customer.** Given an inactive or suspended customer,
   when order confirmation is attempted, then confirmation is blocked and
   the reason is visible.
10. **Reject a stale Product or UOM.** Given a retired or unavailable
    Product/UOM fact, when a line is confirmed, then the order is rejected or
    held and no substitute identity is invented.
11. **Price and discount authority.** Given a price or discount outside the
    effective approved source or user authority, when confirmation is
    attempted, then the order is held or rejected and the missing approval is
    recorded.
12. **Tax dependency.** Given a missing tax category or unresolved tax
    validation, when confirmation or invoicing requires it, then the process
    follows the tax gate and makes no statutory conclusion.
13. **Payment Term dependency.** Given a Payment Term reference whose
    Release 1 shape or due-date mechanics are not approved, when an action
    requires those mechanics, then the process remains Finance-gated under
    FIN-OD-09 / MESP-110 and does not invent a due date.
14. **Credit warning or block.** Given a Finance or approved credit response,
    when the order reaches the policy checkpoint, then the exact response is
    preserved; a warning is not silently changed to a block or override.
15. **Confirm an eligible order.** Given all applicable checks pass, when the
    authorized process confirms the order, then a confirmed source event is
    recorded without creating stock or financial posting.
16. **Idempotent confirmation.** Given a retried confirmation with the same
    idempotency key, when the request is received again, then one order
    confirmation effect exists and the existing outcome is returned.
17. **Stale concurrent edit.** Given two users edit the same order version,
    when the stale version is submitted, then it receives a conflict and
    cannot overwrite the newer source.

### 29.2 Reservation and delivery

18. **Request a reservation.** Given a confirmed order, when Sales requests
    fulfillment, then the request contains Tenant, organization, line,
    quantity, UOM, date, correlation, and idempotency facts.
19. **Reserve available quantity.** Given Inventory confirms an eligible
    quantity, when the response is received, then Sales links the reservation
    to the order and does not reduce stock itself.
20. **Partial reservation.** Given Inventory can reserve only part of a line,
    when the response is accepted, then reserved and remaining quantities are
    distinct and the remaining outcome is visible.
21. **Backorder decision.** Given an unavailable quantity, when an approved
    policy supports backorder, then the line follows that policy; without it,
    the line remains pending or is rejected rather than assuming backorder.
22. **Substitution decision.** Given a proposed substitute Product/Item,
    when the proposal is received, then it requires the approved substitution
    policy and authorized customer/commercial evidence.
23. **Reservation release.** Given a cancelled or changed order, when
    release is required, then Sales requests release and records Inventory's
    authoritative result.
24. **Stale availability.** Given an availability result that is no longer
    current, when fulfillment is attempted, then Inventory revalidates and
    Sales shows the conflict without duplicating a reservation.
25. **Partial delivery.** Given a confirmed order with less than the full
    quantity delivered, when Inventory posts delivery, then delivered,
    remaining, invoiced-eligible, and exception quantities are preserved.
26. **Duplicate delivery event.** Given a repeated Inventory delivery event,
    when it is received, then one physical and one commercial delivery link
    exists through idempotent handling.
27. **Unknown delivery outcome.** Given a timeout after a delivery request,
    when no authoritative response is available, then the status is unknown
    and no invoice or second delivery is created from the timeout alone.
28. **No false stock movement.** Given a user changes a Sales delivery status,
    when Inventory has not posted the physical event, then stock quantity and
    valuation remain unchanged.

### 29.3 Invoice, receipt, and reconciliation

29. **Invoice from approved delivery.** Given an approved delivered quantity,
    when Sales submits an invoice request, then it carries source, customer,
    Company, line, tax, currency, and audit facts to Finance.
30. **Service milestone gate.** Given a service milestone source, when
    invoicing is requested, then it is accepted only if an approved policy
    authorizes that milestone and Finance validates it.
31. **Partial invoice.** Given eligible delivered quantities are partial,
    when Finance accepts a partial invoice, then invoiced and remaining
    quantities remain linked without duplicate eligibility.
32. **Finance rejects invoice.** Given an invalid period, tax, currency,
    mapping, duplicate, or source fact, when Finance rejects the request, then
    Sales shows the reason and does not mark the order financially invoiced.
33. **Immutable posted invoice.** Given a posted Finance invoice, when a
    Sales user attempts an edit, then the edit is denied and a correction path
    is required.
34. **Partial receipt.** Given a Finance-recorded receipt for less than an
    invoice amount, when allocation occurs, then the invoice remains
    partially settled with the Finance allocation link.
35. **On-account or unapplied receipt.** Given Finance records a receipt that
    has no permitted allocation, when Sales reads the status, then it shows
    on-account, unapplied, or unidentified as authoritative and does not
    mark an invoice paid.
36. **Unknown external receipt.** Given an external receipt response times
    out, when no authoritative Finance result exists, then the outcome is
    unknown and retry/reconciliation uses the same source identity.
37. **Multiple-invoice allocation.** Given an approved receipt allocation to
    multiple invoices, when Finance confirms it, then each allocation and
    remaining amount is traceable to the single receipt.
38. **Receipt reversal.** Given a Finance-approved receipt reversal, when the
    result reaches Sales, then the commercial balance is refreshed without
    editing the original receipt record.
39. **Order-to-cash reconciliation.** Given order, delivery, invoice, receipt,
    and return records, when reconciliation runs, then missing, duplicate,
    partial, reversed, and unknown links are visible.
40. **No fabricated financial truth.** Given a delivery or customer claim
    without Finance posting evidence, when a Sales report is generated, then
    it distinguishes commercial status from AR, revenue, tax, and settlement.

### 29.4 Returns, controls, and operational safety

41. **Authorize a return.** Given an eligible source delivery or invoice,
    when an authorized user requests a return, then source, quantity, reason,
    customer, authority, and evidence are stored.
42. **Partial return.** Given only part of a delivered quantity is returned,
    when Inventory accepts it, then returned, retained, credited, and
    remaining quantities are distinct.
43. **Inventory disposition.** Given a return request, when Inventory
    receives physical goods, then only Inventory records physical acceptance
    and disposition and Sales consumes that result.
44. **Credit-note link.** Given an accepted return, when Finance creates a
    credit note, then it links to the original invoice and return and
    preserves the original posted history.
45. **Refund policy gate.** Given a customer requests a refund, when the
    process reaches Finance, then it follows the approved payment/refund
    policy and does not infer a method from the request.
46. **Permission denial.** Given a user without exact permission or scope,
    when the user attempts a Sales action, then it is denied without
    disclosing unauthorized data.
47. **Company isolation.** Given two Companies in one Tenant, when a user
    lacks the second Company scope, then Sales documents and reports from the
    second Company are not returned.
48. **Tenant isolation for exports.** Given an export request, when records
    span multiple Tenants in storage or search, then only the authorized
    Tenant records are exported and the filter is audited.
49. **Audit before effect.** Given a controlled approval, cancellation,
    override, return, or retry, when the action succeeds, then the required
    actor, reason, source, scope, and outcome evidence exists before the
    business effect is exposed.
50. **Downstream retry.** Given a retryable Inventory or Finance failure,
    when the work is retried, then correlation, idempotency, attempts, and
    final outcome are retained.
51. **Dead-letter recovery.** Given a dead-lettered or unknown event, when an
    authorized operator reconciles it, then the authoritative source is
    queried and no duplicate effect is created.
52. **Notification failure.** Given a committed order or delivery event,
    when notification delivery fails, then the business fact remains correct
    and the notification retry is separately visible.
53. **Import duplicate.** Given an imported order row with a previously used
    source identity, when import runs again, then one business effect exists
    and the duplicate result is reported.
54. **Report freshness.** Given a downstream Finance or Inventory response is
    delayed, when a Sales report is run, then it shows freshness or unknown
    state rather than presenting an inferred balance.
55. **Migration gate.** Given a proposed opening-order or AR migration, when
    no approved MESP-51 mapping exists, then the migration is blocked from
    implementation and no source data is changed.
56. **Saudi validation gate.** Given a statutory or Saudi-specific invoice
    requirement, when qualified validation is absent, then the affected
    production or launch path remains gated and this BRD makes no legal claim.

## 30. Open decisions and deferred gates

### 30.1 Sales-specific decision bundle

The following compact bundle is recorded for MESP-23 governance. It is not
approved by this BRD and no new Jira row is created merely by naming it.

| ID | Decision needed | Alternatives to preserve | Consequence if open | Owner and due point |
|---|---|---|---|---|
| SAL-OD-01 | Price-list precedence, manual price, discount authority, thresholds, and effective-date/snapshot behavior | List-only; customer/contract precedence; quantity tiers; controlled manual override; another approved catalogue | Quote/order confirmation and margin/price reporting remain gated | Sales/Product Owner with Finance and Tax concurrence before Sales implementation readiness |
| SAL-OD-02 | Quote/order approval triggers, authority catalogue, SoD, delegation, reassignment, and escalation | No approval for defined low-risk cases; threshold/category/customer approval; named approver workflow; another approved model | Quotes, discounts, credit overrides, returns, or orders may remain pending | Product Owner with Security and Finance concurrence before implementation readiness |
| SAL-OD-03 | Reservation timing, partial allocation, expiry/release, backorder, substitution, inbound supply, and negative-stock interaction | Confirmation reservation; later reservation; split/backorder; substitution; no substitution; explicit exception | Order confirmation and fulfillment cannot choose a safe default | Inventory Owner with Sales and Finance concurrence; MESP-45/MESP-46 register |
| SAL-OD-04 | Return window, reason/authorization, physical disposition, replacement, credit, refund, shipping, and tax correction | Credit-only; refund; replacement; inspection/disposition branches; other approved policy | Return and credit flows remain conditional | Sales, Inventory, and Finance Owners with qualified tax validation where required |
| SAL-OD-05 | Invoice eligibility for shipment quantities, service milestones, partial invoicing, advance/deposit, and source completion | Delivered quantity; approved milestone; another controlled source; partial or consolidated invoice | Invoice request and revenue/AR handoff remain Finance-gated | Sales and Finance Owners before Sales implementation readiness |

These rows are recommendations for decision organization only, not defaults.
MESP-46, MESP-47, MESP-49, MESP-50, MESP-51, MESP-53, MESP-54, and MESP-55
remain the named MESP-23 dependencies described below.

### 30.2 Preserved MESP-23 decisions and dependencies

| Jira row | Current boundary carried into Sales |
|---|---|
| MESP-41 | Batch/lot/serial/expiry tracking remains open. Sales stores or passes tracking requirements only when the approved Inventory policy supplies them. |
| MESP-42 | Procurement approval remains open and is not converted into Sales approval. Any upstream dependency remains a gate. |
| MESP-43 | Supplier confirmation remains open and is not a Sales customer confirmation rule. |
| MESP-44 | Procurement matching remains open and is not an invoice or delivery rule. |
| MESP-45 | Reservation and negative-stock behavior remains open; Sales does not choose timing, expiry, release, or negative-stock treatment. |
| MESP-46 | B2B credit limit, exposure, enforcement, warning/block, and override remain open. |
| MESP-47 | Customer receipt and payment-method policy remains open. Sales does not select methods or refund mechanics. |
| MESP-48 | Supported volume/performance is an open production gate; this BRD makes no capacity claim. |
| MESP-49 | Saudi e-invoicing, tax, and statutory validation remains open; no legal or ZATCA conclusion is made. |
| MESP-50 | Retention, privacy, legal hold, purge, residency, backup, and restoration remain open production gates. |
| MESP-51 | Migration and opening-state scope remains open; this BRD authorizes no migration. |
| MESP-52 / PD-020 | The exact approved Release 1 Plan boundary is preserved: one Release 1 Plan containing all approved modules, simple fixed limits, and no metered billing. Sales does not extend that Plan scope or invent entitlement behavior. |
| MESP-53 | Reports and reconciliation catalogue remains open; Sales provides source semantics only. |
| MESP-54 | Exchange-rate, reporting-currency, rounding, and related Finance policy remains open. |
| MESP-55 | Later domain approval and delegation remains open; no approver or delegation default is selected. |
| MESP-56 / PD-021 | Multiple Companies/Legal Entities per Tenant remain separate legal and accounting boundaries; no consolidation/intercompany/elimination/transfer pricing/consolidated statements are added. |
| FIN-OD-09 / MESP-110 | Open, unapproved Finance dependency for fiscal-year/year-end, Payment Term Release 1 shape and mechanics, and posting-dimension policy. It is not resolved here and remains separate from MESP-54. |

### 30.3 Explicit Finance non-resolution

For avoidance of doubt, this BRD does not approve or recommend a Payment
Term due-date formula, schedule, installment model, discount, aging or
settlement mechanics; fiscal-year or year-end behavior; P&L carry-forward,
retained earnings, reopen, reclose, or derived-reporting mechanics; or a
posting-dimension or Cost Center catalogue. Any affected Sales branch is
Finance-gated until MESP-110 and the other named decisions are approved.

## 31. Review notes and source conflicts

- Finance was analysed before Sales as required by the corrected domain
  sequence. The Finance posting foundation is a dependency, not a Sales
  design space.
- Procurement and Inventory baselines use adjacent business concepts with
  different ownership. Sales preserves the handoffs instead of treating
  supplier commitment, reservation, delivery, or physical disposition as its
  own ledger.
- The glossary marks Payment Term, credit, receipt, reconciliation, tax,
  exchange-rate, rounding, and related concepts as decision-controlled where
  applicable. This document retains those markers.
- MESP-52 / PD-020 and MESP-56 / PD-021 are carried exactly: one Release 1
  Plan with all approved modules, simple fixed limits, and no metered billing;
  and multiple Companies/Legal Entities per Tenant as separate legal and
  accounting boundaries with no consolidation, intercompany, elimination,
  transfer-pricing, or consolidated-statement behavior. Neither decision is
  generalized into other approvals or migration rules.
- The PRD's B2B chain is consistent with the requirement to distinguish
  quotation, order, reservation, delivery, invoice, receipt, allocation,
  return, credit, and reconciliation. No recommendation from a Founder
  Decision Pack is promoted to a requirement.
- No source implementation, production provider, migration, Saudi legal
  conclusion, or external infrastructure claim is made.

## 32. Definition of Ready for a later Sales implementation item

A later implementation item may begin only after the owning plan confirms:

1. this baseline and all correction evidence are approved;
2. every SAL-OD row needed by the selected slice has named approval;
3. MESP-46 credit, MESP-45 reservation, MESP-47 receipt, MESP-49 Saudi/tax,
   MESP-50 retention/privacy, MESP-51 migration, MESP-53 reporting,
   MESP-54 exchange/rate, MESP-55 approval/delegation, and FIN-OD-09 /
   MESP-110 are either approved or explicitly excluded from that slice;
4. Finance and Inventory have accepted the source contracts and ownership
   boundaries;
5. Tenant, Company, Branch, Warehouse, authorization, SoD, audit,
   concurrency, idempotency, unknown, and recovery behavior is ready;
6. implementation scope names the exact project/module and does not cross
   module-owned persistence or migration ownership; and
7. MESP-48/MESP-49/MESP-50 and the relevant ADR/provider/production gates are
   passed or visibly deferred by the approved release plan.

## 33. Definition of Done for this BRD session

This MESP-35 session is complete when:

- the canonical document is published and reviewed;
- SAL-001 through SAL-008, BR-005, and BR-009 are traceable;
- B2B quote-to-cash, partials, denial, credit, Inventory, Finance,
  reconciliation, reporting, migration, integration, Saudi, audit,
  observability, recovery, and Tenant-isolation coverage is present;
- FIN-OD-09 / MESP-110, MESP-54, and every applicable open MESP-23 row
  remain explicit and unapproved;
- no implementation or later task is started;
- Jira contains activation, validation, approval, MESP-23 handoff, and
  closure evidence; and
- the repository state, plan, tracker, branch, review PR, and merged main are
  synchronized.

## 34. Approval and handoff record

This section is completed by the bounded MESP-35 review session and records
evidence without changing the business meaning above:

| Evidence | Recorded value |
|---|---|
| Live entry-gate recheck | MESP-109 Done with PASS WITH NON-BLOCKING FINDINGS; MESP-110 / FIN-OD-09 To Do and unapproved; MESP-34 Done; MESP-23 In Progress; MESP-35 activated only for this session. |
| Canonical artifact | docs/24_Sales_and_Order_to_Cash_BRD.md |
| Jira validation | MESP-35 comment 10763 records the focused content validation and local checks. |
| Owner approval | MESP-35 comment 10764 records bounded Owner approval for review and merge; it does not close any open decision. |
| MESP-23 handoff | MESP-23 comment 10765 records the Sales handoff and preserves 14 open rows plus the exact MESP-52/MESP-56 closures. |
| Review and merge | Focused PR #51; the final clean merge outcome is recorded in the MESP-35 closure evidence and repository state. |
| Implementation authorization | None. Currency, MESP-36, MESP-37, and all implementation work remain unstarted. |
