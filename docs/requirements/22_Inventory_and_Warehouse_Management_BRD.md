# Mini ERP SaaS Platform - Inventory and Warehouse Management BRD

> **Current bounded session - 10 August 2026.** This document is the
> documentation-only MESP-33 business-requirements baseline. It contains no
> application source, API contract, database/schema design, migration script,
> user-interface specification, provider decision, or production-readiness
> claim. MESP-34 Finance and all later domain work are not started by this
> document.

## 1. Document Control

| Field | Value |
|---|---|
| Document | Inventory and Warehouse Management Business Requirements Document |
| Jira | MESP-33 - Produce Inventory and Warehouse Management BRD |
| Parent Epic | MESP-8 - EPIC 08 - Inventory and Warehouse Management |
| BRD sequence | Position 8 of 15, confirmed by MESP-25 comment 10057 |
| Version | v0.1 - Approved Business Baseline |
| Status | **Approved Business Baseline.** This document is a business-requirements baseline only and does not authorize source implementation by itself. |
| Accountable Owner | Hossam, Product Owner and founder approver; Inventory, Organization, Master Data, Procurement, Finance, Sales, Reporting, Security, Migration, and Saudi validation owners are consulted within their decision boundaries |
| Prepared for | Release 1 B2B ERP |
| Date | 10 August 2026 |
| Canonical PRD | docs/MESP_PRD_v1.2.docx, PRD v1.2 Final Approved Baseline, approved 31 July 2026 |
| Primary PRD anchors | INV-001 through INV-008; BR-006; BR-007 |
| Required glossary | docs/00_ERP_Business_Glossary.md |
| Related approved BRDs | docs/11_SaaS_Platform_Administration_BRD.md; docs/12_Identity_and_Access_BRD.md; docs/13_Multi_Tenancy_BRD.md; docs/14_Organization_and_Company_Structure_BRD.md; docs/16_Master_Data_and_Product_Catalog_BRD.md; docs/21_Procurement_and_Purchase_to_Pay_BRD.md |
| Decision register | MESP-23 and its Jira-decomposed rows MESP-41 through MESP-56 |
| Jira activation evidence | MESP-33 comment 10741 |
| Jira approval evidence | MESP-33 comment 10743 |
| Delivery reference | docs/94_Product_Delivery_Master_Plan.md |
| Architecture references | docs/Decisions.md; ADR-002, ADR-004, ADR-006, ADR-007, ADR-008, ADR-009, and ADR-018; constraint references only |

### 1.1 Classification legend

| Classification | Meaning in this BRD |
|---|---|
| **Confirmed baseline** | Directly supported by the approved PRD, approved glossary, an approved upstream BRD, or an explicitly approved Jira decision. |
| **Open decision** | The named Owner decision is still open in Jira. The BRD records the required policy branch and implementation gate without choosing an option. |
| **Deferred gate** | The requirement is understood but must be validated before a later implementation, production, migration, or external launch gate. |
| **Recommended default - not approved** | A recommendation preserved for decision-making only. It is not a requirement, acceptance criterion, or implementation instruction. |
| **External validation** | A qualified external, legal, tax, banking, Saudi, privacy, or business validation is required. This BRD makes no statutory conclusion. |
| **Out of scope** | Release 1 does not include the behavior in this BRD. |

The Founder Decision Pack is not an approval catalogue. Recommendations are
repeated only when labelled **Recommended default - not approved**. The only
broadly approved rows in the current MESP-23 register are MESP-52 / PD-020 and
MESP-56 / PD-021; neither silently answers Inventory policy.

## 2. Executive Summary

Release 1 Inventory and Warehouse Management provides the B2B ERP's
authoritative physical-stock boundary. It records quantity-changing events at
authorized Warehouses, preserves an immutable chronological stock ledger, and
derives on-hand, reserved, available, expected, damaged, and in-transit views
without double counting. Every posted movement remains linked to its source
document, actor, organizational scope, posting time, cost basis, and
correction or reversal chain.

The central business invariant is:

> A quantity change is an authorized posting that appends a stock-ledger
> movement. A balance is a projection of those movements and is never edited
> directly. Opening balances enter through controlled ledger postings; a
> purchase order, supplier confirmation, purchase invoice, or customer
> commercial document does not independently create stock.

Inventory owns the physical receipt, transfer, count, adjustment, issue, and
return stock effects and the operational inventory valuation evidence.
Organization owns Warehouse identity and hierarchy. Master Data owns Product,
Item/UOM identity and Product-side tracking configuration. Procurement owns
the purchase and supplier-return commercial handoff. B2B Sales owns customer
orders, delivery authorization, and the customer-return commercial handoff.
Finance owns accounting, AP/AR, tax, fiscal periods, exchange-rate policy, and
the general-ledger effect. No boundary in this BRD creates a Finance,
Procurement, Sales, Saudi, or migration policy that belongs elsewhere.

The Release 1 valuation baseline is Moving Weighted Average. The BRD keeps
valuation scope, landed-cost treatment, opening-cost treatment, return
treatment, backdated correction policy, and exchange-rate sourcing explicit
where the named Owner decision remains open.

## 3. Business Purpose and Outcomes

### 3.1 Purpose

The purpose of this module is to give each Tenant's authorized Companies,
Branches, Warehouses, Inventory users, Procurement users, Sales users, Finance
users, and auditors a traceable way to:

1. establish opening stock through a controlled, reconciled opening process;
2. receive accepted goods from the Procurement-owned commercial chain;
3. move goods between authorized Warehouses while representing in-transit
   ownership exactly once;
4. correct, count, return, or issue stock through explicit business events;
5. expose availability and projected balances without silently inventing
   reservations or expected stock;
6. calculate and preserve Moving Weighted Average evidence at the approved
   valuation scope;
7. hand quantity and value effects to Finance, Procurement, Sales, Reporting,
   Migration, and Notifications through authenticated, Tenant-scoped
   contracts; and
8. reconcile the ledger, projected balances, physical counts, valuation, and
   downstream effects with accountable evidence.

### 3.2 Intended outcomes

The Release 1 business outcome is a reliable stock-control chain with:

- a single authoritative movement history per Tenant and applicable
  Company/Branch/Warehouse scope;
- no direct balance mutation and no silent edit or deletion of posted history;
- clear distinction between on-hand, reserved, available, expected, damaged,
  and in-transit quantities;
- partial, rejected, cancelled, returned, corrected, and failed workflows that
  leave a visible business outcome;
- policy-controlled tracking, negative-stock, reservation, approval, count,
  adjustment, delegation, and migration behavior;
- deterministic valuation evidence that can be reconciled with Finance without
  inventing accounts or tax rules; and
- Tenant-safe reports, exports, audit evidence, notifications, and
  reconciliation suitable for later implementation and production review.

## 4. Scope

### 4.1 In scope

This BRD covers the Release 1 B2B business requirements for:

- Opening Balance onboarding and controlled migration postings;
- Goods Receipt of accepted and rejected quantities from Procurement;
- Warehouse Transfer request, shipment, in-transit representation, receipt,
  variance, and reconciliation;
- Stock Adjustment with reason, evidence, permissions, and any approved
  material-approval branch;
- Inventory Count, including full/cycle assignment, blind counting, variance
  review, approval, and posting;
- Supplier Return of goods previously accepted into stock;
- Customer Return of goods previously delivered by the B2B Sales process;
- Stock Issue as an authorized non-sales inventory-out event;
- immutable stock-ledger movements, projected balances, availability views,
  reservations only if the approved downstream policy enables them, and
  tracking attributes only where approved;
- Release 1 Moving Weighted Average valuation evidence;
- lifecycle, correction, reversal, rejection, cancellation, reopening,
  concurrency, idempotency, audit, failure, and reconciliation requirements;
- permissions, approval boundaries, separation of duties, delegation
  dependencies, and server-derived Tenant/organization scope;
- Product, Item, UOM, tracking, Organization/Warehouse, Procurement, Finance,
  B2B Sales, Reporting, Notification, Integration, Import/Export, and
  Migration boundaries needed to make the stock process complete; and
- business-level acceptance scenarios and unresolved decision traceability.

### 4.2 Required process chain

The normal stock chain is:

Tenant and organization scope -> Product/UOM validation -> authorized source
document -> Inventory business event -> posted stock-ledger movement ->
projected balance and valuation -> reconciliation and downstream event

The required flows are distinct:

| Flow | Entry evidence | Inventory effect |
|---|---|---|
| Opening Balance | Controlled migration or onboarding batch with approved source evidence | Creates an opening movement through the ledger; does not create a purchase payable |
| Goods Receipt | Procurement receipt handoff and physical arrival | Increases stock only for accepted quantity when the receipt is posted |
| Warehouse Transfer | Authorized source and destination Warehouses | Moves quantity through shipped/in-transit/received states without double counting |
| Stock Adjustment | Authorized reason and evidence | Appends a positive or negative correction movement |
| Inventory Count | Count assignment and submitted count evidence | Calculates variance; posts only after the approved review/approval branch |
| Supplier Return | Previously accepted stock and supplier-return evidence | Decreases stock on posted return; Finance owns credit/accounting |
| Customer Return | Previously delivered customer stock and Sales handoff | Increases stock only for accepted/postable returned quantity |
| Stock Issue | Authorized internal or non-sales inventory-out source | Decreases stock on posted issue; it is not a B2B delivery or supplier return |

### 4.3 Out of scope

This BRD does not include:

- application source, EF entities, tables, migrations, endpoints, API
  contracts, UI, provider selection, database provisioning, or automated-test
  behavior;
- Retail POS, point-of-sale stock, cash-register behavior, barcode-scanner
  workflows, or Wafra-specific reusable core behavior;
- bin, shelf, zone, wave, pick-pack-ship, labor, route optimization, mobile
  warehouse management, replenishment algorithms, or advanced WMS behavior;
- Product/Item identity, SKU/barcode uniqueness, Category/UOM master
  maintenance, or Product lifecycle decisions owned by Master Data;
- Purchase Order, Supplier Confirmation, invoice, payment, tax, AR/AP,
  account-mapping, fiscal-period, or Finance approval policy;
- B2B order, delivery, credit control, reservation trigger, or customer-credit
  policy owned by Sales or Finance;
- statutory, legal, VAT, ZATCA, banking, Saudi e-invoicing, or residency
  conclusions;
- a production supported-volume, provider, capacity, retention, recovery,
  migration cutover, or go-live approval; or
- a resolution of MESP-41 through MESP-55, ADR-011, or another domain's open
  decision by implication.

## 5. Source Traceability

### 5.1 Primary PRD anchors

| Anchor | Requirement carried into this BRD |
|---|---|
| INV-001 | Every quantity-changing event is an immutable stock-ledger entry with item, location, quantity, unit, cost basis, source, actor, and timestamp. |
| INV-002 | On-hand, reserved, available, expected, damaged, and in-transit states are distinct and cannot double count quantity. |
| INV-003 | Warehouse transfer supports authorized shipment, in-transit ownership, receipt, and reconciliation. |
| INV-004 | Positive and negative adjustments require reason codes, permission, evidence, and any approved material-approval branch. |
| INV-005 | Full/cycle counts support assignment, blind counting, variance review, approval, and posting without overwriting ledger history. |
| INV-006 | Product tracking configuration may enable batch, lot, serial, manufacturing-date, or expiry validation; exact operational policy remains open. |
| INV-007 | Release 1 uses deterministic Moving Weighted Average with no silent historical recalculation; scope, landed cost, returns, and openings require explicit treatment. |
| INV-008 | Negative stock is blocked or warned according to Tenant policy and authorization; an override is explicit and audited if approved. |
| BR-006 | Maintain accurate stock by Product and Warehouse through movements, reservations, transfers, counts, adjustments, returns, and reconciliation. |
| BR-007 | Support B2B order-to-cash handoffs through reservation, delivery, return, and availability boundaries without moving Sales ownership into Inventory. |

### 5.2 Supporting baseline traceability

| Source | Relevant boundary |
|---|---|
| PRD sections 5, 7, 8, 11, 12, and 13 | B2B Release 1 scope, organization hierarchy, inventory lifecycle, controls, integration, and open-question discipline |
| docs/00_ERP_Business_Glossary.md | Warehouse, stock, ledger, movement, balance, returns, opening balance, count, adjustment, transfer, tracking, MWA, posting, reversal, and reconciliation terminology |
| docs/14_Organization_and_Company_Structure_BRD.md | Tenant -> Company/Legal Entity -> Branch -> Warehouse hierarchy, downward access scope, Warehouse identity ownership, inactive/closed behavior, and MESP-56 boundary |
| docs/16_Master_Data_and_Product_Catalog_BRD.md | Product/Item single Release-1 identity, Tenant-scoped Product/UOM reuse, Product-side tracking configuration, and Inventory operational ownership |
| docs/21_Procurement_and_Purchase_to_Pay_BRD.md | Procurement-owned commercial process, Inventory-owned accepted Goods Receipt and Supplier Return stock effects, Supplier external-party boundary, and MESP-41/42/43/44/45 dependency rows |
| MESP-23 / Jira comments 10062 and 10063 | Living decision register; only PD-020/MESP-52 and PD-021/MESP-56 are approved broad rows |
| MESP-25 comment 10057 | Approved domain sequence: Procurement, then Inventory, then Finance |
| MESP-26 comment 10058 | BRD entry gate under the solo-founder governance model |
| MESP-32 comment 10740 | Completed upstream Procurement baseline and separately authorized MESP-33 handoff |
| docs/94_Product_Delivery_Master_Plan.md | Phase 2 BRD exit criteria, domain sequence, and no automatic next-task execution |
| ADR-002, ADR-004, ADR-006, ADR-007, ADR-008, ADR-009, ADR-018 | Modular-monolith project boundary, server authority, Tenant isolation, transaction/outbox, worker, private-file, validation, and production-gate constraints |

### 5.3 Requirement traceability convention

INV-REQ identifiers in this document are business requirement labels for
review and acceptance. They are not API routes, database names, permission
catalogue entries, migration identifiers, or implementation authorization.
An open decision remains open even when a requirement describes both policy
branches.

## 6. Actors and Responsibilities

| Actor | Business responsibility | Boundary |
|---|---|---|
| Inventory owner | Owns the stock process, ledger invariants, movement state, operational valuation evidence, count/adjustment policy input, and reconciliation accountability | Does not own Product identity, Finance accounts, or Sales commercial documents |
| Warehouse operator | Records physical receipt, transfer, count, return, and issue evidence within server-authorized scope | Cannot expand Tenant, Company, Branch, or Warehouse scope from client input |
| Receiving user | Records arrival, accepted/rejected quantities, condition evidence, and tracking facts where policy enables it | Does not post an invoice or create a payable |
| Transfer requester | Requests movement between authorized Warehouses and records dispatch evidence | Cannot receive into an unauthorized destination or erase an in-transit movement |
| Transfer receiver | Records destination receipt and variance evidence | Cannot alter source history or silently close an unreceived shipment |
| Count supervisor | Assigns count work, controls count evidence, reviews variance, and performs the approved posting action | Exact approval and movement-window rules remain open |
| Counter | Records physical count, including blind count where the approved policy requires it | Cannot approve own material variance unless an approved policy explicitly permits it |
| Adjustment/issue requester | Submits reason, quantity, location, evidence, and business purpose | Cannot bypass the approved posting and negative-stock policy |
| Inventory approver | Reviews a count, adjustment, issue, transfer exception, or other material event when the approved catalogue requires approval | Exact role, threshold, delegation, and self-approval policy remains open |
| Procurement user | Owns the Purchase Order, Supplier Confirmation, receipt handoff, and supplier-return commercial context | Does not independently create the Inventory stock effect |
| Supplier | External business party providing goods and evidence | No User, login, credential, Tenant membership, or platform session |
| B2B Sales user | Owns delivery authorization, customer return initiation, order status, and customer-commercial evidence | Does not edit Inventory ledger history |
| Customer | External B2B party associated with the Sales process | No User, login, credential, Tenant membership, or platform session |
| Finance controller | Owns valuation-accounting policy, AP/AR, tax, fiscal period, exchange-rate source, financial posting, and reconciliation with the ledger | This BRD does not choose account mappings, tax treatment, or rate source |
| Tenant administrator | Configures authorized Tenant and organizational access within approved platform capabilities | Cannot override server-owned stock, audit, approval, or cross-Tenant controls |
| Auditor or support user | Reads authorized history, audit, reconciliation, and evidence for the permitted Tenant/scope or case-bound support grant | Read access never grants mutation authority or sibling-scope visibility |
| Reporting owner | Owns report definitions, reconciliation ownership, freshness, and retention input | MESP-53 remains open |
| Migration owner | Owns source extract, mapping, cleansing, preview, sign-off, dry runs, cutover, and rollback evidence | MESP-51 remains open; Wafra is validation-only |
| Integration/notification worker | Delivers authenticated, correlated, idempotent downstream effects and reports failures | It must reconstruct trusted Tenant/organization scope; it is not a privileged global scanner |

## 7. Controlled Terminology

| Term | Requirement in this BRD | Status |
|---|---|---|
| Tenant | Top business ownership and isolation boundary; every Inventory record and event is Tenant-owned | Confirmed baseline |
| Company / Legal Entity | Tenant-owned legal/accounting boundary; multiple Companies may exist under one Tenant | Confirmed by MESP-56 / PD-021 |
| Branch | Operational and reporting child of a Company | Confirmed baseline |
| Warehouse | Approved stock-holding location under the organization hierarchy; Inventory owns stock effects at the Warehouse and Organization owns its identity/relationship | Confirmed baseline |
| Product / Item | One Release-1 master-data identity; no separate variant or Item behavior is introduced by this BRD | Product-only scope approved in MESP-31 |
| Unit of Measure / Base Unit | Master-data identity and conversion supplied by Master Data; Inventory validates quantity and conversion and does not redefine UOM policy | Confirmed boundary; detail owned by Master Data |
| Stock | Quantity physically held at a Warehouse together with its operational value evidence | Confirmed glossary baseline |
| Stock Ledger | Complete immutable chronological record of every posted quantity/value movement; correction is a new linked event | Confirmed baseline |
| Stock Movement | One quantity/value change for an Item at a Warehouse from a posted source document | Confirmed baseline |
| Stock Balance | Current quantity/value projection derived from ledger movements; never directly edited | Confirmed baseline |
| On-hand | Physically present quantity, regardless of reservation | Confirmed glossary baseline |
| Reserved | On-hand quantity committed to demand; it is not itself a stock decrease | Open policy dependency on MESP-45 and MESP-46 |
| Available | Policy-defined availability, normally related to on-hand and any approved reservation; no unapproved reservation rule is selected | Open decision |
| Expected | Committed to arrive but not received; not on-hand, available, or valued stock | Open Inventory policy |
| Damaged | On-hand but unusable quantity; it is not automatically rejected, returned, or written off | Open treatment policy |
| In-transit | Quantity shipped from a source Warehouse and not yet received at the destination; it is not on-hand at either location | Open one/two-step policy details |
| Opening Balance | Initial quantity/value entered through a controlled opening batch and opening ledger movement; not a purchase or payable | MESP-51 remains open |
| Goods Receipt | Physical arrival and acceptance record; only posted accepted quantity increases stock | Confirmed Procurement/Inventory boundary |
| Warehouse Transfer | Movement between Warehouses within the approved organization boundary; no purchase or sale | Confirmed glossary baseline; mechanics open |
| Stock Adjustment | Authorized correction movement with reason/evidence; it does not edit history | Confirmed baseline |
| Inventory Count | Physical count compared with a balance; it produces variance but is not itself an adjustment | Confirmed baseline |
| Supplier Return | Previously accepted stock sent back to the supplier; rejected-at-receipt is not a Supplier Return | Confirmed baseline |
| Customer Return | Previously delivered goods returned through the Sales handoff; Inventory controls physical acceptance and stock effect | Confirmed boundary |
| Stock Issue | BRD-local term for an authorized non-sales inventory-out event; it is distinct from B2B Delivery and Supplier Return | Business term defined here; final catalogue is an implementation gate |
| Posting | Controlled act committing the authorized Inventory effect to the immutable ledger and associated projections | Confirmed baseline |
| Reversal | Equal/opposite linked posting that preserves the original history | Confirmed baseline |
| Moving Weighted Average | Release 1 valuation method; a deterministic weighted cost recalculation at the approved valuation scope | Confirmed method; scope/treatment open |
| Tracking attribute | Batch, lot, serial, manufacturing-date, or expiry data required only when the approved Product/Inventory policy enables it | MESP-41 remains open |

## 8. Ownership Boundaries and Business Invariants

### 8.1 Domain ownership matrix

| Capability or document | Owning domain | Inventory responsibility |
|---|---|---|
| Tenant, Company, Branch, Warehouse identity and hierarchy | Organization / Platform | Validate server-authorized Warehouse scope and active status |
| Product, Item, SKU, barcode, Category, UOM identity | Master Data | Read the authorized identity and conversion; do not redefine it |
| Product tracking configuration | Master Data | Enforce operational tracking only after the approved policy enables it |
| Purchase Request, Purchase Order, Supplier Confirmation | Procurement | Consume the approved source and preserve lineage |
| Goods Receipt physical acceptance and stock posting | Inventory | Own accepted/rejected quantity and the posted stock effect |
| Supplier Return stock movement | Inventory with Procurement handoff | Validate prior accepted stock and post the decrease; Procurement owns commercial context |
| Sales order, delivery authorization, customer credit | B2B Sales / Finance | Consume only the authorized delivery/return handoff |
| Customer Return physical acceptance and stock posting | Inventory with Sales handoff | Own the returned stock effect and disposition evidence |
| Stock ledger, balances, availability projections | Inventory | Own the authoritative movement and projection invariants |
| Stock valuation evidence | Inventory | Calculate and preserve operational cost evidence under approved MWA policy |
| GL, AP/AR, tax, periods, accounting entries | Finance | Receive linked valuation/movement events; no account policy is invented |
| Exchange-rate source and update policy | Finance / Treasury | Preserve rate facts supplied at posting; MESP-54 remains open |
| Report definitions and reconciliation ownership | Reporting with domain owners | Supply source-of-truth data and freshness/status evidence |
| Audit, Tenant access, support scope, file evidence, notifications | Platform / Identity / Security / Files | Use approved shared contracts; no cross-Tenant or unapproved provider behavior |

### 8.2 Non-negotiable invariants

1. Every Inventory record, command, job, import, export, report, audit event,
   attachment, and downstream event is Tenant-scoped and is checked against
   server-derived Company, Branch, and Warehouse ownership where applicable.
2. A quantity or operational value change exists only as an authorized,
   traceable stock-ledger movement from a posted source event.
3. The stock ledger is append-only. No user, import, repair, report, or
   integration edits or deletes a posted movement.
4. Stock balances and availability are projections of movements and approved
   reservation/status facts; direct balance editing is denied.
5. Opening balances enter through controlled opening movements and never bypass
   the ledger by seeding a hidden balance.
6. A source document, actor, posting time, scope, correlation/idempotency
   reference, UOM, quantity, cost basis, currency/rate facts where applicable,
   and reversal/return chain are preserved.
7. On-hand, reserved, available, expected, damaged, and in-transit quantities
   are represented so that one physical quantity is not counted twice.
8. A Purchase Order, Supplier Confirmation, Purchase Invoice, customer order,
   payment, or notification does not independently change Inventory stock.
9. A correction, return, or cancellation after posting creates the allowed
   linked forward event; it does not silently rewrite the original.
10. New work uses active authorized masters and organizational units. Historical
    documents remain readable to authorized users under current scope.
11. A Warehouse Transfer does not become an intercompany sale or purchase.
    The exact transfer variance, loss, and financial treatment is an approved
    Finance/Inventory policy branch.
12. Tracking, reservations, expected stock, damaged treatment, negative-stock
    override, transfer mechanics, count movement handling, and approval
    thresholds are not inferred from common practice.
13. Moving Weighted Average is deterministic and reproducible from posted
    movements and preserved currency/rate facts; historical recalculation is
    controlled and never silent.
14. Finance owns the accounting result. Inventory does not invent GL accounts,
    tax classifications, payment methods, exchange-rate sources, or
    consolidation behavior.
15. Material business effects are audited before or atomically with the effect
    according to the approved platform pattern; authorization failure fails
    closed without leaking foreign scope.

## 9. Business Process Requirements

Each process below is a business requirement, not a screen, endpoint, schema,
or implementation sequence. Exact status labels and approval permission names
remain implementation decisions after the applicable Owner policies are
approved.

### 9.1 Opening Balance

**Trigger and preconditions**

- A controlled onboarding or cutover batch is authorized for a Tenant and
  approved organization scope.
- The source owner, extract time, as-of date, Product/Item identity, Warehouse,
  UOM, quantity, cost/value, currency/rate facts, and evidence are present.
- Product/UOM and organization masters are configured and active, or the
  approved migration process records a controlled precondition exception.
- The source rows have been previewed, mapped, cleansed, reconciled, and
  signed off under the MESP-51 migration policy.

**Main path**

1. The migration owner submits a versioned opening batch.
2. Inventory validates scope, identities, UOM, quantity signs, tracking
   requirements if enabled, duplicate source rows, and valuation evidence.
3. Invalid rows are quarantined with actionable reasons; they do not create
   stock or financial effects.
4. An authorized approval/posting action creates an opening movement per valid
   row through the immutable stock ledger.
5. Projected balances and valuation evidence are reconciled to the signed
   source totals, with batch, row, actor, timestamp, and correlation evidence.

**Alternatives and exceptions**

- A partial batch may post only when the approved migration policy permits
  partial completion and the remainder is explicit; otherwise the batch
  remains unposted.
- Duplicate, unmapped, inactive, negative, or unexplained rows remain rejected
  or quarantined; no silent coercion is allowed.
- A posting error is corrected through an allowed reversal/adjustment linked to
  the opening batch. The original opening movement remains immutable.
- An opening balance is not a Goods Receipt, Purchase Order, Purchase Invoice,
  Supplier Return, or payable. MESP-51 must define the final cutover scope,
  history depth, sign-off, and rollback evidence before implementation.

### 9.2 Goods Receipt

**Trigger and preconditions**

- Procurement provides an authorized receipt handoff tied to its approved
  commercial source where the later policy requires it.
- The receiving Warehouse is active and within the user's server-derived
  scope. Product/Item, UOM, quantity, and any enabled tracking facts validate.
- The physical arrival, accepted quantity, rejected quantity, condition
  evidence, supplier reference, and receiving actor are recorded.

**Main path**

1. The receiving user records the arrival and receipt version.
2. Accepted and rejected quantities are separated with reason/evidence.
3. Inventory validates source quantity, UOM conversion, Warehouse, tracking,
   duplicate receipt/idempotency reference, and any approved confirmation or
   matching prerequisite.
4. Posting creates stock movements only for the accepted quantity. The posted
   source and ledger movement remain linked.
5. The remaining/open quantity is visible to Procurement; it is not silently
   treated as received.

**Alternatives and exceptions**

- Partial receipt is supported: accepted, rejected, and outstanding quantities
  remain explicit and the source stays open when appropriate.
- A rejected-at-receipt quantity is not automatically damaged stock, a
  Supplier Return, or a write-off. The approved treatment must say which
  branch applies.
- Missing tracking data blocks or routes the receipt only according to the
  approved MESP-41 policy. This BRD does not choose batch/lot/serial/expiry
  behavior.
- A Purchase Invoice creates or adjusts the supplier obligation and its
  accounting/tax evidence under Finance. It does not independently increase
  stock.
- Before posting, a receipt may be rejected or cancelled under its approved
  lifecycle. After posting, correction uses a linked reversal, return, or
  adjustment.

### 9.3 Warehouse Transfer

**Trigger and preconditions**

- The source and destination Warehouses are active, authorized, and within
  the approved organization boundary.
- The transfer Item, UOM, quantity, source balance, reason, requester, and
  destination are valid.
- The later policy decides whether request/approval is required and whether the
  transfer is one-step or two-step. No option is selected here.

**Main path**

1. An authorized requester creates a versioned transfer request when the
   policy requires one.
2. The approved transfer records source, destination, quantity, UOM, tracking
   facts if enabled, reason, and correlation reference.
3. Shipment posts the source decrease and, for a two-step policy, creates the
   in-transit quantity exactly once.
4. Receipt at the destination validates the shipped quantity and posts the
   destination increase while clearing the applicable in-transit state.
5. The total movement, source/destination, variance, actors, timestamps, and
   status are available for reconciliation.

**Alternatives and exceptions**

- A partial shipment or receipt keeps the unshipped/unreceived remainder
  visible and cannot create destination stock twice.
- A short, over, lost, damaged, or late shipment is recorded as a variance
  branch. Exact threshold, reason, approval, and financial treatment remain
  an Owner decision.
- A transfer cannot cross Tenant ownership. Transfer between legal entities,
  intercompany automation, consolidation, and transfer pricing remain out of
  Release 1 under MESP-56 / PD-021.
- Before shipment, an authorized request may be rejected or cancelled. After a
  posted shipment, a cancellation is a linked reversal/return process; it is
  not deletion of the source movement.

### 9.4 Stock Adjustment

**Trigger and preconditions**

- An authorized user identifies a quantity or operational-value discrepancy
  that is not better represented by a receipt, transfer, count, or return.
- Reason, evidence, Product/Item, Warehouse, UOM, quantity sign, and source
  context are present.
- The approved policy determines whether the adjustment needs review,
  threshold approval, or separate posting authority.

**Main path**

1. The requester submits a versioned adjustment with a controlled reason.
2. Inventory validates scope, active masters, UOM, source evidence, duplicate
   submission, negative-stock policy, and any required approval.
3. An authorized posting appends a positive or negative movement and records
   the actor, reason, evidence, and valuation effect.
4. The new projection is reconciled to the movement and the adjustment appears
   in reports and audit.

**Alternatives and exceptions**

- A count variance should use the approved count/variance branch rather than
  an unexplained adjustment.
- A rejected or cancelled unposted request has no stock effect.
- A posted adjustment is corrected only through a linked reversal or forward
  adjustment. The reason and original evidence remain queryable.
- An adjustment that would create negative stock is blocked, warned, or
  permitted only according to MESP-45 and any approved Inventory policy. No
  default is adopted here.

### 9.5 Inventory Count

**Trigger and preconditions**

- A full or cycle count is assigned to an authorized Warehouse and scope.
- Count instructions, count basis/time, Product/UOM scope, counter, and any
  blind-count requirement are recorded.
- The approved policy defines how movements during the count are handled:
  freeze, cutoff, resnapshot, or another controlled branch. The BRD does not
  infer one.

**Main path**

1. A supervisor assigns count work and records the count version.
2. Counters record physical quantities and evidence without changing the
   authoritative ledger.
3. Inventory compares submitted counts with the applicable projected balance
   and calculates visible variances.
4. A reviewer examines evidence and applies the approved variance approval
   branch.
5. Posting creates new variance movements or linked adjustments; the count and
   original ledger remain immutable and traceable.

**Alternatives and exceptions**

- A partial or cycle count leaves uncounted scope explicit and cannot silently
  close the entire Warehouse.
- A recount, disputed result, or rejected count creates a new count version or
  controlled correction; it does not overwrite prior evidence.
- A count with an unresolved movement window, missing evidence, or unauthorized
  reviewer cannot post.
- Count accuracy, shrinkage, variance threshold, and report ownership are
  subject to MESP-53 and the Inventory control policy.

### 9.6 Supplier Return

**Trigger and preconditions**

- The goods were previously accepted and posted into Inventory.
- Procurement provides the supplier-return commercial context, original receipt
  or eligible movement reference, reason, quantity, and supplier evidence.
- The return Warehouse, Product/UOM, tracking facts, and any approved negative
  or period policy validate.

**Main path**

1. An authorized user creates a return linked to the original accepted stock.
2. Inventory validates that the return quantity is eligible and not already
   returned, reversed, or consumed by another controlled effect.
3. Posting appends the stock decrease and preserves original receipt,
   supplier, reason, actor, and correlation evidence.
4. Procurement tracks the commercial closure and Finance records the supplier
   credit/correction when applicable.

**Alternatives and exceptions**

- Partial return is supported when the eligible remainder and returned
  quantity remain explicit.
- Rejected-at-receipt goods are not retroactively reclassified as a Supplier
  Return without the approved business reason and evidence.
- A return exceeding eligible accepted quantity is blocked or routed through an
  explicit approved exception; the receipt is not edited.
- A posted return is corrected by a linked reversal or new authorized event.
  It does not delete the original receipt or return.

### 9.7 Customer Return

**Trigger and preconditions**

- B2B Sales provides an authorized customer-return handoff linked to a
  previously delivered quantity and its customer-commercial evidence.
- Inventory has a valid destination Warehouse, Product/UOM, and any enabled
  tracking or condition information.
- Finance/Sales own credit, refund, tax, and customer-account treatment.

**Main path**

1. Sales authorizes the commercial return and sends the physical-return
   context to Inventory.
2. Receiving records returned quantity, condition, accepted/rejected
   disposition, evidence, and any tracking facts.
3. Inventory posts an increase only for the accepted quantity that the approved
   policy permits to re-enter stock.
4. The customer return, delivery, stock movement, and any Finance credit
   reference remain linked for reconciliation.

**Alternatives and exceptions**

- Partial return keeps delivered, returned, accepted, rejected, and outstanding
  quantities visible.
- Damaged or non-restockable returned goods remain distinct from available
  stock; automatic write-off, quarantine, or resale treatment is not inferred.
- Missing source delivery, duplicate return, invalid tracking, or an
  unauthorized Warehouse blocks posting or routes to the approved exception.
- Finance/Sales decide customer credit, tax, and refund outcomes. Inventory
  does not create those effects.

### 9.8 Stock Issue

Stock Issue is the narrow BRD-local name for an authorized inventory-out
movement that is not a B2B Delivery, Supplier Return, or Warehouse Transfer.
It may represent an internal business purpose only when that purpose and
posting authority are approved.

**Trigger and preconditions**

- The requester supplies an authorized purpose, source/evidence reference,
  Warehouse, Product/UOM, quantity, and any tracking facts.
- The policy confirms that the event is not actually a Sales delivery,
  Supplier Return, adjustment, or transfer.
- The requested quantity passes the approved balance, reservation, period,
  negative-stock, and approval rules.

**Main path**

1. The requester creates a versioned issue request.
2. Inventory validates scope, source, identity, quantity, UOM, duplicate
   submission, approval, and any Finance cost/effect requirement.
3. An authorized poster appends the decrease to the stock ledger.
4. Finance receives the linked valuation/accounting handoff when the approved
   policy requires it; Inventory does not invent the account or cost centre.

**Alternatives and exceptions**

- Partial issue keeps the requested and posted remainder visible.
- An issue that would create negative stock follows MESP-45; it is not silently
  allowed because the requester has a general Warehouse permission.
- An unposted request may be rejected or cancelled under policy. A posted
  issue is corrected by a linked reversal or authorized adjustment.
- The event is not a Retail POS issue, a sales delivery, or Wafra-specific
  operational behavior.

## 10. Document Lifecycle and Status Transitions

### 10.1 Common lifecycle

The exact labels may be localized or represented by an approved state
catalogue, but the business meaning is:

| State family | Business meaning | Stock effect |
|---|---|---|
| Draft | Work is being prepared and can be validated or abandoned under policy | None |
| Submitted / Checked | A version is complete enough for review or posting checks | None |
| Approved | The required business approval has been recorded for that version | None until posting |
| Posted | The authorized effect is committed to the immutable ledger | Yes, exactly once |
| Completed / Reconciled | Required downstream and physical/business closure is evidenced | No new effect unless a new event is posted |
| Rejected / Cancelled | The unposted version is closed with reason and audit | None |
| Reversed / Corrected / Returned | A posted effect is addressed by a linked new event | New linked movement only |

Opening batches, receipts, transfers, adjustments, counts, returns, and issues
must expose the source version, current state, actor, timestamps, reason,
approval evidence, and any remaining quantity. A UI label or client-supplied
status cannot authorize a posting.

### 10.2 Rejection, cancellation, reopening, and correction

- Rejection before posting records reason and reviewer evidence and creates no
  stock effect.
- Cancellation before posting is allowed only where the applicable policy
  permits it and records the actor, reason, and version.
- Reopening is a controlled new version or explicit state transition for an
  unposted document. It is not a way to rewrite a posted movement.
- A posted document cannot be silently edited or deleted. The permitted
  correction is a linked reversal, return, adjustment, recount, or other
  forward event with the original still queryable.
- A failed downstream notification does not falsely change a posted source
  state. The authoritative effect and downstream delivery/reconciliation state
  remain separately visible.
- A repeated command with the same Tenant-scoped idempotency reference cannot
  create a second ledger effect.

## 11. Business Data Requirements

This is a business data inventory, not a schema or persistence design.

| Data group | Required business facts |
|---|---|
| Scope and identity | Tenant, Company/Legal Entity where applicable, Branch, Warehouse, Product/Item, UOM, document number, source system/reference, and server-authorized scope |
| Source lineage | Source document type and identifier, source version, parent/line reference, external reference, correlation/idempotency key, and reason for the Inventory event |
| Quantity | Requested, accepted, rejected, outstanding, shipped, received, returned, issued, counted, variance, UOM, conversion evidence, sign, and precision |
| Location/status | Source and destination Warehouse, on-hand, reserved, available, expected, damaged, in-transit, quarantine/exception status if approved, and freshness |
| Tracking | Batch/lot/serial/manufacturing-date/expiry values only when the approved Product/Inventory policy requires them; uniqueness and duplicate evidence |
| Cost and value | Cost basis, MWA calculation inputs/output, valuation scope, transaction currency, rate source/date/value, base currency, precision/rounding, and correction chain where applicable |
| Count evidence | Count assignment, count basis/time, blind-count evidence, counter, reviewer, recount/version, variance reason, and posting approval |
| Approval and security | Requester, reviewer/approver, poster, delegation or escalation evidence, permission decision, Tenant/scope proof, and denial/failure reason |
| Audit and history | Actor, event time, posting time, source version, correlation, before/after business state, reversal/return links, evidence references, and immutable audit outcome |
| Reconciliation | Ledger total, projected balance total, physical count result, valuation total, downstream Finance/Procurement/Sales reference, difference, owner, status, and resolution |
| Integration | Contract version, event identity, Tenant and organization scope, delivery state, retry/dead-letter/unknown outcome, and replay/reconciliation reference |

## 12. Validation Rules

The following business validations are required. They do not prescribe code
structure or endpoint behavior.

| ID | Validation |
|---|---|
| INV-VR-001 | The server establishes Tenant and applicable organization scope from trusted authority; client identifiers cannot expand it. |
| INV-VR-002 | A Warehouse must belong to the selected Tenant and approved Company/Branch hierarchy and be eligible for the transaction. |
| INV-VR-003 | New work cannot use inactive, closed, deleted, or otherwise ineligible masters or organization units. |
| INV-VR-004 | Product/Item identity and UOM must resolve through the Master Data boundary; unknown or cross-Tenant identities fail closed. |
| INV-VR-005 | Quantity is non-null, uses a valid UOM/conversion, respects approved precision, and has an explicit sign and business reason. |
| INV-VR-006 | Every posted quantity/value effect has a source document, actor, time, scope, correlation, and cost/value evidence where applicable. |
| INV-VR-007 | A posted movement is immutable; correction requires a linked forward event. |
| INV-VR-008 | A projected balance cannot be posted or edited as a substitute for a ledger movement. |
| INV-VR-009 | Accepted, rejected, outstanding, shipped, received, returned, issued, and counted quantities cannot exceed the applicable source or approved exception. |
| INV-VR-010 | Partial flows preserve the remainder and cannot mark a source complete while eligible quantity remains unexplained. |
| INV-VR-011 | Tracking values are mandatory, validated, and duplicate-checked only when the approved policy enables the relevant tracking attribute. |
| INV-VR-012 | Tracking values cannot be invented, silently changed, or reused across a scope that the approved policy declares unique. |
| INV-VR-013 | Transfer source and destination are authorized Warehouses, and in-transit quantity is counted once rather than at both endpoints. |
| INV-VR-014 | A Supplier Return references previously accepted eligible stock; rejected-at-receipt is not silently converted into a return. |
| INV-VR-015 | A Customer Return references an authorized Sales delivery and posts only the accepted quantity allowed by policy. |
| INV-VR-016 | A Stock Issue has an approved non-sales purpose and cannot disguise a delivery, return, transfer, or unexplained adjustment. |
| INV-VR-017 | Negative-stock behavior follows the approved MESP-45 policy; an override requires explicit authorization and audit if permitted. |
| INV-VR-018 | Reservation and availability are exposed only according to an approved policy; a reservation is not a stock decrease. |
| INV-VR-019 | Count variance is based on a defined count version and movement-handling policy; an unreviewed variance cannot post. |
| INV-VR-020 | Opening rows pass source, mapping, sign, UOM, value, duplicate, scope, and sign-off validation before posting. |
| INV-VR-021 | Moving Weighted Average uses the approved valuation scope and preserves inputs, rate facts, precision, and rounding evidence. |
| INV-VR-022 | A closed or otherwise invalid Finance period blocks or routes posting according to Finance policy; Inventory does not invent the exception. |
| INV-VR-023 | Concurrent edits or postings use a version/concurrency check so a stale caller cannot silently overwrite a newer business result. |
| INV-VR-024 | A repeated idempotency reference produces one authoritative effect and a queryable result, including safe handling of unknown outcomes. |
| INV-VR-025 | Imports, exports, jobs, and events re-establish trusted Tenant and scope context and preserve correlation and delivery evidence. |
| INV-VR-026 | A foreign Tenant or sibling organization reference is denied without returning foreign data, attachments, balances, or audit details. |
| INV-VR-027 | Report and reconciliation results state scope, source status, currency/time basis, freshness, difference, and accountable owner. |
| INV-VR-028 | A downstream Finance, Procurement, Sales, notification, or reporting failure cannot silently create a second or falsely claim a missing stock effect. |

## 13. Permissions and Access Scope

Permission names and the final approval catalogue remain open. Every capability
below is subject to a server-derived Tenant and downward organization scope.

| Capability | Minimum business boundary |
|---|---|
| View stock and balances | Authorized Tenant and Company/Branch/Warehouse scope; projected data must identify freshness and status |
| View ledger and movement history | Authorized scope, source-lineage visibility, immutable history, and audit-safe denial |
| Create receipt/return/transfer/count/adjustment/issue draft | Authorized role, active masters, and eligible Warehouse scope |
| Submit or approve a count/adjustment/issue/transfer exception | Separate policy-controlled authority, versioned evidence, and no unauthorized self-expansion |
| Post Goods Receipt or Supplier Return | Authorized Inventory posting authority plus valid Procurement source and policy |
| Ship or receive transfer | Authorized source/destination Warehouse scope and exact transfer role |
| Post count variance | Approved count reviewer/poster authority and count-version evidence |
| Post Stock Adjustment or Stock Issue | Approved reason, permission, negative-stock/period controls, and any required review |
| View valuation evidence | Authorized Inventory/Finance scope; GL/account mapping remains Finance-owned |
| Reconcile ledger, balances, counts, and downstream effects | Explicit reconciliation authority and immutable result/evidence |
| Import or submit opening batch | Migration authority, source-owner sign-off, preview/quarantine, and MESP-51 controls |
| Export inventory data | Tenant/resource scope, filter/time/currency basis, redaction, audit, and approved file boundary |
| View audit and failure evidence | Authorized current scope or case-bound support grant; no raw foreign identifiers in denial |

The client may select among authorized scope values, but it cannot supply a
Tenant, Company, Branch, Warehouse, membership, support, or approval value as
authority. Background work and integration events carry stored Tenant/scope
evidence and are revalidated at execution.

## 14. Approval Controls and Separation of Duties

### 14.1 Approval controls

- The approval catalogue must be versioned, effective-dated, Tenant-scoped
  where configuration is approved, and tied to the exact document version.
- Approval may be required for material adjustments, count variances, negative
  overrides, transfer variances, Stock Issues, returns, opening batches, or
  other events only where the named policy says so.
- This BRD requires the control points but does not choose numeric thresholds,
  role names, self-approval rules, or the final Inventory approval matrix.
- An approval of one document version is invalidated or re-evaluated when an
  approved policy treats a material change as significant.
- A denial, expiry, rejection, delegation, escalation, or out-of-office result
  is visible and audited. It cannot be replaced by a client status.

### 14.2 Separation of duties

The final policy should distinguish requester, physical counter/receiver,
reviewer/approver, poster, and reconciler where risk requires it. The BRD does
not silently turn this recommendation into a universal permission rule.

At minimum, implementation must not assume that:

- a user who counts may always approve the variance;
- a requester may always approve or post their own material adjustment or
  Stock Issue;
- a Procurement or Sales user may post an Inventory effect without Inventory
  authority;
- Finance approval can be replaced by an Inventory status; or
- a delegated or out-of-office user has broader Tenant or Warehouse scope.

MESP-42 provides the upstream approval-workflow dependency and MESP-55 owns
delegation, escalation, reassignment, expiry, and out-of-office policy. Their
open status is preserved.

## 15. Concurrency, Idempotency, Failure, and Reconciliation

### 15.1 Concurrency and idempotency

- A material document, source line, balance projection, count version, and
  approval version must detect stale updates.
- A repeated receipt, transfer shipment/receipt, return, issue, adjustment,
  opening row, or downstream event with the same Tenant-scoped idempotency
  reference produces one authoritative stock effect.
- Concurrent postings are serialized or rejected according to the approved
  Inventory/Finance consistency policy; no last-writer-wins balance overwrite
  is acceptable.
- A source version remains linked even when a later correction, reversal, or
  return is posted.

### 15.2 Failure behavior

- A validation or authorization failure produces no stock effect and safe
  actionable evidence.
- A timeout after the irreversible posting boundary is an uncertain outcome
  to reconcile, not an instruction to blindly retry.
- A downstream notification, Finance, Procurement, Sales, or report projection
  failure cannot silently claim the effect was absent or create it twice.
- Retry, dead-letter, replay, and reconciliation state is Tenant-scoped and
  observable; provider-specific behavior remains outside this BRD.
- Attachment failure does not expose private content or falsely change posting
  state. File storage, scanning, retention, and purge remain ADR-009/MESP-50
  gates.

### 15.3 Reconciliation

Required reconciliation views include:

- stock-ledger movements to projected balance by Product/UOM/Warehouse;
- on-hand and status buckets without double counting;
- physical count and approved variance to resulting movements;
- MWA value evidence to the Inventory projection;
- Goods Receipt/Supplier Return to Procurement source and Finance credit;
- Customer Return/Delivery to Sales source and Finance credit;
- transfer shipment, in-transit, receipt, and variance;
- opening batch source totals to posted ledger and valuation; and
- downstream event, notification, integration, and unknown-outcome status.

Each reconciliation identifies scope, as-of time, source statuses, currency
basis, freshness, difference, owner, disposition, and correction evidence.
Report catalogue and accountable ownership remain MESP-53 decisions.

## 16. Inventory, Master Data, Organization, and Finance Boundaries

### 16.1 Product, Item, UOM, and tracking

- MESP-31 establishes Product/Item as one Release-1 master-data identity with
  no separate variant behavior in this slice. Inventory does not create a
  variant or Product identity.
- Product SKU/barcode, Category, Product lifecycle, and UOM identity are
  supplied by Master Data. Inventory validates that the selected records are
  Tenant-owned, active, and usable for the event.
- Inventory uses the Product Base Unit and approved conversion facts. It does
  not silently change the Base Unit after stock or invent precision/rounding.
- Product stores tracking configuration only. Inventory owns operational
  tracking structures, validation, movement traceability, and batch/lot/
  serial/expiry behavior when MESP-41 is approved.
- MESP-41 remains open. No attribute is made mandatory merely because a
  Product configuration field exists.

### 16.2 Organization and Warehouse

- Organization owns Warehouse identity, parent relationships, status, and
  downward access scope.
- Inventory owns stock, movement, count, return, and valuation effects at the
  approved Warehouse.
- A Warehouse belongs to one approved organization path. A transaction cannot
  use a sibling or foreign Warehouse through client-provided identifiers.
- Inactive or closed Warehouses cannot receive new work unless a separately
  approved historical or correction policy permits it; existing history stays
  readable to authorized users.
- MESP-56 / PD-021 is adopted exactly: one Tenant may have multiple legal
  entities, but Release 1 does not infer consolidation, intercompany
  automation, eliminations, transfer pricing, or consolidated statements.

### 16.3 Finance, valuation, and accounting

- Inventory owns quantity movements and operational valuation evidence;
  Finance owns the account mapping, GL/subledger, AP/AR, tax, fiscal-period,
  financial posting, reversal, and reconciliation policy.
- Moving Weighted Average is the Release 1 method. The final policy must
  confirm valuation scope, landed-cost treatment, opening balances, returns,
  Stock Issues, negative stock, backdated corrections, and period closure.
- Each applicable posting preserves transaction currency, rate, source/date,
  base currency, precision, and rounding evidence. MESP-54 owns the exchange
  source, update cadence, effective date, reporting currency, and correction
  policy.
- A Purchase Invoice changes supplier liability/accounting under Finance, not
  stock quantity. Supplier Return may produce a Finance credit. Customer
  Return may produce a Sales/Finance credit. Stock Issue or adjustment has a
  Finance effect only where the approved policy configures one.
- This BRD does not invent chart-of-accounts values, tax codes, valuation
  accounts, payment methods, or statutory accounting conclusions.

### 16.4 Reservations, availability, and negative stock

- Reservation is not a stock decrease. Availability must identify whether it
  uses on-hand only or an approved reservation/expected-stock policy.
- B2B Sales owns the commercial trigger for reservation and delivery. Inventory
  supplies the authorized physical availability and posting boundary.
- MESP-45 remains open for negative stock and its interaction with reservation,
  receipt, return, transfer, adjustment, and issue. MESP-46 remains the
  downstream customer-credit dependency.
- The BRD records block, warn, and explicitly audited override branches
  without selecting one as the Tenant default.

## 17. Saudi, Localization, and External Boundaries

- Release 1 is B2B ERP only. No Retail POS, Wafra-specific core rule,
  statutory report, or country-specific inventory algorithm is introduced.
- The user-facing and business-document baseline may require English/Arabic
  labels, RTL layout, and localized identifiers. ADR-011 remains the required
  future decision for Arabic search/sort/tokenization, RTL, and bilingual
  document generation; this BRD does not close it.
- MESP-49 remains the external Saudi validation gate for e-invoicing, VAT,
  tax, invoice/credit/debit evidence, country-pack obligations, and any
  ZATCA or legal conclusion. Inventory preserves source and audit evidence
  but does not declare compliance.
- Finance, qualified Saudi tax/legal advisors, and the approved country-pack
  process decide statutory treatment. Banking, payment rails, and settlement
  methods remain Finance-owned.
- Any launch assertion about SAR, time zone, numbering, retention, residency,
  or statutory data must use an approved source and scope; this BRD does not
  convert a launch context into a legal or production approval.

## 18. Reports, KPIs, Notifications, and Audit

### 18.1 Required report coverage

The later report catalogue should provide, subject to MESP-53:

- stock on-hand, projected balance, and valuation by Product/UOM/Warehouse;
- movement ledger by source, reason, actor, date, Warehouse, and status;
- reserved, available, expected, damaged, and in-transit views with
  definitions and no double counting;
- low-stock, negative-stock, rejected, unallocated, blocked, and unknown
  outcome exceptions;
- Goods Receipt, Supplier Return, Customer Return, Stock Issue, adjustment,
  transfer, count, and opening-batch status and aging;
- count accuracy, variance, shrinkage, adjustment reason, and approval metrics;
- transfer turnaround, in-transit aging, receipt discrepancy, and variance;
- tracking and expiry/aging views only where the approved policy supplies the
  required tracking data;
- MWA valuation movement and reconciliation evidence;
- ledger-to-balance, count-to-ledger, opening-to-source, and
  Inventory-to-Finance/Procurement/Sales reconciliation; and
- integration, notification, retry, dead-letter, replay, and reconciliation
  exceptions.

Every report states Tenant/organization scope, as-of time, source statuses,
currency/time basis, freshness, filters, totals, and accountable owner. MESP-53
remains open for the final catalogue, definitions, reconciliation ownership,
and freshness/retention decisions.

### 18.2 Notifications

Business notifications may cover pending receipt, transfer in-transit,
count variance, approval, negative-stock exception, return status, unknown
outcome, reconciliation difference, and migration rejection. Notifications
are non-authoritative: failure or delay cannot create, suppress, or reverse a
stock effect. Recipient scope, content, language, retention, and delivery
provider remain subject to Platform, Security, Saudi, and MESP-50 gates.

### 18.3 Audit evidence

Audit evidence must make it possible to answer who, what, when, where, why,
under which Tenant and organization scope, against which document/version,
with which permission/approval, and with which source, correction, reversal,
return, or downstream reference. It must cover:

- successful and denied commands;
- before/after business state and source version;
- posting, reversal, return, count, adjustment, issue, and transfer events;
- approval, delegation, rejection, cancellation, and expiry;
- import, export, migration, report, support, and reconciliation access;
- integration retry, unknown, dead-letter, replay, and provider failure state;
  and
- attachment access outcomes without exposing private bytes or foreign
  identifiers.

Retention, legal hold, privacy, residency, purge, backup, and restoration are
MESP-50 gates. Audit history is not silently purged or made mutable by this
BRD.

## 19. Integration, Import/Export, and Migration Requirements

### 19.1 Domain integrations

| Boundary | Required contract behavior |
|---|---|
| Master Data -> Inventory | Tenant-scoped Product/Item, UOM, conversion, active status, and Product tracking configuration; no cross-Tenant identity |
| Organization -> Inventory | Tenant-scoped Company/Branch/Warehouse identity, ownership, status, and downward authorization |
| Procurement -> Inventory | Authenticated receipt and Supplier Return source, line, accepted/rejected/eligible quantity, supplier reference, version, and correlation |
| Inventory -> Procurement | Posted Goods Receipt/Supplier Return status, movement reference, partial remainder, exception, and reconciliation evidence |
| B2B Sales -> Inventory | Authorized Delivery and Customer Return source, customer/commercial reference, quantity, and physical disposition handoff |
| Inventory -> B2B Sales | Availability/status, posted delivery/return effect, freshness, and exception without changing Sales ownership |
| Inventory <-> Finance | MWA valuation evidence, source/rate facts, posting/reversal/correction references, period outcome, and reconciliation; Finance owns accounts and tax |
| Inventory -> Reporting | Read-only movement, balance, status, valuation, count, and reconciliation facts with freshness and scope |
| Inventory -> Notifications/Integrations | Versioned, authenticated, Tenant-scoped, correlated, idempotent delivery with retry/dead-letter/unknown outcome evidence |

All contracts must validate authorization and ownership server-side, carry
Tenant and applicable organization scope, preserve stable event/document
identity, avoid duplicate effects, and expose failure/reconciliation state.
No broker, provider, external credential, or production topology is selected.

### 19.2 Import and export

- Imports validate file/source owner, Tenant, organization scope, identity,
  UOM, quantity, tracking, cost, currency, duplicate, and source references
  before any posting.
- Preview results show accepted, rejected, quarantined, and warning rows with
  actionable reason and source-row reference.
- Approval/posting is separate from upload or preview. A file upload cannot
  itself create stock.
- Exports are Tenant/resource scoped, record filter/time/currency basis, use
  approved private-file access, and create audit evidence.
- Failed upload, export, attachment, or notification does not create a
  phantom ledger effect and does not expose foreign data.

### 19.3 Migration and cutover

MESP-51 owns the final migration and opening-balance decision. The required
business evidence is:

1. named source owner, extract date, source system, and in-scope history;
2. data dictionary, cleansing, mapping, UOM/Product/Warehouse crosswalk,
   tracking treatment, cost/value/currency basis, and rejected-row handling;
3. preview and immutable batch/row results with approval and sign-off;
4. two dry runs, a cutover rehearsal, reconciliation of quantity and valuation,
   rollback/backup/business-continuity evidence, and explicit go/no-go;
5. opening quantities and value entered through the Inventory ledger; and
6. post-cutover Inventory, Finance, and business-owner reconciliation.

Wafra can provide validation-only evidence where the Owner explicitly labels
it, but no Wafra-specific field, rule, workflow, or integration is made part
of the reusable platform.

## 20. Operational and Quality Requirements

At the business-requirements level, Inventory must be:

- transactionally consistent at the authoritative posting boundary;
- explicit about projection freshness and any delayed downstream read model;
- resilient to duplicate, timeout, retry, worker lease, provider, notification,
  and unknown-outcome conditions;
- observable through safe business metrics, audit, correlation, retry,
  reconciliation, and alertable exception states;
- protected by Tenant isolation, server-derived authorization, immutable
  history, concurrency, idempotency, and fail-closed denial;
- usable in supported English/Arabic and RTL contexts after ADR-011 and
  localization validation; and
- recoverable, retained, exported, and purged only under MESP-48, MESP-49,
  MESP-50, security, privacy, legal, and production gates.

The PRD reference expectations for common read/command latency, noisy-Tenant
isolation, RPO/RTO, volume, recovery, and operational capacity are planning
inputs only. MESP-48 owns supported volume and performance evidence; MESP-50
owns retention, privacy, legal hold, purge, residency, backup, and restoration.
This BRD makes no production performance, provider, capacity, or recovery
claim.

## 21. Given / When / Then Acceptance Scenarios

These are business acceptance scenarios, not automated test specifications.

| ID | Given | When | Then |
|---|---|---|---|
| INV-AC-001 | A signed opening batch has valid Tenant, Warehouse, Product, UOM, quantity, value, and source evidence | An authorized poster posts it | An opening movement is appended to the ledger, the balance projection reconciles, and no PO/AP/payable is created |
| INV-AC-002 | An opening import contains unmapped or duplicate rows | The batch is previewed | Invalid rows are quarantined with reasons and no stock or financial effect is created for them |
| INV-AC-003 | A valid Procurement receipt is partially accepted | A receiver posts accepted and rejected quantities | Only accepted posted quantity increases stock, rejected quantity remains distinct, and the source remainder is visible |
| INV-AC-004 | A receipt needs tracking under an approved policy but the tracking value is missing or duplicated | The user attempts posting | Posting follows the approved block/exception path and does not create an unverifiable movement |
| INV-AC-005 | A purchase invoice is approved by Finance | The invoice is posted | AP/tax/accounting evidence is created under Finance policy and stock quantity is unchanged |
| INV-AC-006 | Source and destination Warehouses are authorized and the transfer policy requires shipment and receipt | The source ships a partial quantity | The source decrease and in-transit quantity are recorded once, the unshipped remainder is visible, and destination stock has not been doubled |
| INV-AC-007 | A transfer arrives with a short or damaged quantity | The receiver records the difference | The variance, evidence, owner, and remaining in-transit state follow the approved branch; no silent loss or destination overstatement occurs |
| INV-AC-008 | An adjustment requester has an eligible reason and evidence | They submit a positive or negative adjustment | The version is validated and either awaits the approved review or posts one linked movement; the balance is not edited directly |
| INV-AC-009 | An adjustment would create negative stock | The requester attempts posting | The result follows the approved MESP-45 block/warn/override policy and is auditable |
| INV-AC-010 | A blind count is assigned to a Warehouse | A counter submits a physical quantity | The count evidence is stored without changing the ledger and a visible variance is calculated |
| INV-AC-011 | A count variance requires review under the approved policy | A reviewer rejects or approves the count | Rejection creates no variance posting; approval posts a linked variance movement and preserves the count and ledger history |
| INV-AC-012 | Accepted stock is eligible for a Supplier Return | An authorized user posts only part of it | The stock decrease, supplier reason, original receipt, remaining eligible quantity, and Finance credit reference are linked |
| INV-AC-013 | A user tries to return goods that were rejected at receipt or already returned | They submit the Supplier Return | The action is denied or routed to an explicit exception; the original receipt is not changed |
| INV-AC-014 | Sales provides a valid customer return for a delivered quantity | Inventory accepts only part of the physical return | Only accepted permitted quantity increases stock, disposition is visible, and Sales/Finance credit remains separately owned |
| INV-AC-015 | An authorized internal purpose requires a non-sales inventory-out | A Stock Issue is posted | One linked decrease is appended with reason/evidence and any approved Finance handoff; it is not treated as POS or a Sales delivery |
| INV-AC-016 | A posted receipt or issue contains an error | An authorized correction is performed | A linked reversal/adjustment/return is posted and the original movement remains immutable and queryable |
| INV-AC-017 | A user attempts to edit a posted balance or ledger movement directly | The command is submitted | The server denies it and requires the approved forward correction path |
| INV-AC-018 | A client supplies a valid-looking foreign Tenant or sibling Warehouse identifier | The user requests or mutates Inventory data | The server fails closed without returning foreign balance, ledger, attachment, report, or audit data |
| INV-AC-019 | A selected Product, UOM, or Warehouse is inactive | A user starts new Inventory work | The action is blocked with an actionable reason while authorized historical documents remain readable |
| INV-AC-020 | A quantity is entered in an approved alternate UOM | The event is validated | The conversion is explicit, valid, and traceable; no hidden or cross-Tenant UOM is used |
| INV-AC-021 | A user lacks the required scope or posting authority | They attempt to approve, post, ship, receive, count, adjust, return, issue, import, or export | The action is denied, no stock effect occurs, and the denial is audited safely |
| INV-AC-022 | A requester and reviewer load the same document version | Both attempt material changes or approval | One current outcome succeeds and the stale outcome is rejected or reconciled without silent overwrite |
| INV-AC-023 | A receipt, transfer, return, issue, adjustment, or opening post times out after submission | The user retries with the same idempotency reference | The authoritative effect occurs once and the outcome remains queryable, including an unknown state when needed |
| INV-AC-024 | A downstream Finance or Procurement event fails after an Inventory posting | The system retries or reconciles | The ledger effect is not duplicated, the downstream state is visible, and no false completion is reported |
| INV-AC-025 | A receipt changes the approved MWA inputs | Inventory calculates valuation | The cost evidence is deterministic, preserves source/rate/precision facts, and is traceable to the movement |
| INV-AC-026 | A return or correction affects a previously valued movement | The event is posted | The original valuation evidence remains visible and the approved reversal/return policy explains the new value |
| INV-AC-027 | A Warehouse balance is compared with its ledger | An authorized owner runs reconciliation | The report identifies scope, as-of time, freshness, totals, differences, and accountable disposition |
| INV-AC-028 | A physical count does not match the projected balance | The count is reviewed | The variance is not silently written off; evidence and approved posting/rejection state remain visible |
| INV-AC-029 | An export is requested by an authorized user | The export is generated | It is Tenant/resource scoped, states filter/time/currency basis and freshness, and creates audit evidence |
| INV-AC-030 | An import contains invalid quantity, tracking, identity, or source rows | It is previewed or submitted | Rows are rejected/quarantined with source references and cannot create hidden stock |
| INV-AC-031 | A Warehouse has a pending in-transit shipment | A balance report is generated | In-transit quantity is separate from source on-hand, destination on-hand, expected stock, and available stock |
| INV-AC-032 | A Tenant contains multiple legal entities | Users process transfers and reports | Company/accounting scope remains distinct and no consolidation, intercompany automation, or transfer pricing is inferred |
| INV-AC-033 | A user requests a notification or report after a posting | The delivery service is unavailable | The authoritative Inventory state is not falsely changed; delivery/retry/reconciliation evidence is visible |
| INV-AC-034 | A user requests a count or transfer while the approved movement-window rule is unresolved | The action reaches the policy gate | The system follows only a later approved policy; this BRD does not invent a freeze, cutoff, resnapshot, or one/two-step default |
| INV-AC-035 | A caller requests a Retail POS, Wafra-specific, bin-optimization, or unapproved statutory behavior | The request is evaluated | The behavior is outside this Release 1 BRD and no reusable core requirement is created |

## 22. Open Decision Register and Deferred Gates

MESP-23 comment 10737 is the current Procurement/Inventory-linked register
handoff. No open row below is resolved by this BRD. Where a recommendation is
shown, it is a decision aid only.

| BRD row | Jira | Current status | Named owner / required input | Inventory consequence and due point |
|---|---|---|---|---|
| INV-OD-001 | MESP-41 | Open / To Do | Product and Inventory owner | Decide batch/lot/serial/manufacturing-date/expiry scope, required attributes, uniqueness, correction, and operational enforcement. **Recommended default - not approved:** Product configuration enables explicit Inventory validation and missing/duplicate enabled values fail closed. Alternatives are no tracking, selected attributes, or Tenant-configured policies. This blocks tracking-dependent LIS/implementation. |
| INV-OD-002 | MESP-45 | Open / To Do | Inventory owner with Finance and B2B Sales concurrence | Decide block, warn, or explicitly audited override for negative stock and its interaction with reservations, returns, transfers, adjustments, and issues. **Recommended default - not approved:** block by default with a narrowly permissioned audited exception. Final policy is required before balance-affecting implementation. |
| INV-OD-003 | MESP-42 and MESP-55 | Open / To Do | Product owner, Inventory owner, Finance controller | Decide Inventory approval catalogue, thresholds, requester/reviewer/poster separation, delegation, escalation, expiry, out-of-office, and self-approval. **Recommended default - not approved:** versioned effective-dated policy with separate material-event review and no client-supplied authority. Required before approval-aware implementation. |
| INV-OD-004 | **MESP-113** - Decide Inventory transfer, in-transit, count-window, variance, and Stock Issue policy; linked to MESP-23 reconciliation comment 10894 | Not yet dispositioned | Hossam with Inventory and Finance input | Decide one-step versus two-step transfer, in-transit ownership, short/over/loss treatment, count movement freeze/cutoff/resnapshot, variance thresholds, and Stock Issue purpose catalogue. **Recommended default - not approved:** retain explicit in-transit and versioned count evidence, but the options and consequences must be chosen before LIS. |
| INV-OD-005 | MESP-48 | Open / To Do | Product and Platform Operations | Establish supported Tenant/Warehouse volume, ledger/report scale, throughput, queue depth, recovery, and performance evidence. This remains a production/capacity gate, not a BRD numeric promise. |
| INV-OD-006 | MESP-49 | Open / To Do | Finance controller and qualified Saudi tax/legal advisor | Validate Saudi e-invoicing, VAT, tax, credit/debit, document, and country-pack obligations. No ZATCA, statutory, banking, or legal conclusion is made; external validation is required before launch-dependent capability. |
| INV-OD-007 | MESP-50 | Open / To Do | Data Protection/Compliance, Platform Operations, and legal | Decide retention, privacy, legal hold, purge, export, residency, backup, restoration, and audit-evidence policy. This remains a production gate. |
| INV-OD-008 | MESP-51 | Open / To Do | Migration owner, Finance, and business source owner | Decide opening-balance scope, historical depth, source mapping, rejection, reconciliation, dry runs, cutover, rollback, and Wafra validation evidence. Required before migration or opening implementation. |
| INV-OD-009 | MESP-53 | Open / To Do | Inventory, Finance, Reporting, and Product owners | Approve report catalogue, status definitions, reconciliation owners, freshness, variance, valuation, and retention expectations. Required before final reporting acceptance. |
| INV-OD-010 | MESP-54 | Open / To Do | Finance controller / Treasury | Decide exchange-rate source, update cadence, effective date, reporting currency, precision, and correction policy. Inventory preserves facts but does not choose the source. Required before final valuation/accounting integration. |
| INV-OD-011 | MESP-43 | Open / To Do | Procurement owner | Decide Supplier Confirmation requirement, partial confirmation, changed quantity/date/price, rejection, and no-response behavior. Inventory receipt eligibility remains policy-dependent. |
| INV-OD-012 | MESP-44 | Open / To Do | Finance controller with Procurement concurrence | Decide matching model and tolerance. Inventory exposes receipt quantity and evidence; it does not auto-accept a Finance/Procurement mismatch. |
| INV-OD-013 | MESP-46 | Open / To Do | B2B Sales and Finance owner | Decide customer credit control and reservation/delivery dependency. Inventory availability is not a credit decision. |
| INV-OD-014 | MESP-47 | Open / To Do | Finance controller / Treasury | Decide payment and settlement methods. This does not create an Inventory payment behavior. |
| INV-OD-015 | MESP-52 | Done / approved PD-020 | Product/Founder decision already recorded | Adopt the approved one-Release-1 B2B ERP scope; no POS or Wafra-specific core behavior is authorized by this row. |
| INV-OD-016 | MESP-56 | Done / approved PD-021 | Product/Founder decision already recorded | Adopt multiple legal entities per Tenant and no Release 1 consolidation/intercompany automation; Finance details remain MESP-34-owned. |

### 22.1 Consolidated Owner decision bundle

The genuinely blocking Inventory mechanics are kept in one small review bundle
rather than invented as new policy. The bundle has four parts:

1. tracking and traceability (MESP-41);
2. negative stock, reservations, and availability (MESP-45 with downstream
   MESP-46);
3. approval, separation of duties, and delegation (MESP-42/MESP-55); and
4. transfer/count/issue mechanics and migration/reconciliation dependencies
   (MESP-33 Owner review, MESP-51, and MESP-53).

For each part, the options, recommended default, consequences, affected scope,
and due point are explicit in the table above. No recommendation is promoted
to a requirement. The BRD remains coherent by preserving each policy branch,
but the affected implementation specification, production acceptance, or
country/migration gate must not choose an option until the named Owner or
qualified external owner records the decision in Jira or the immutable
decision record.

## 23. Source Conflicts, Corrections, and Review Notes

1. The approved current sequence is MESP-25 comment 10057: MESP-31 Master
   Data, MESP-32 Procurement, MESP-33 Inventory, then MESP-34 Finance. Older
   sequencing notes are not used.
2. MESP-26 comment 10058 is the approved BRD entry gate. It authorizes the
   bounded BRD wave but does not resolve MESP-41 through MESP-55.
3. The canonical PRD is docs/MESP_PRD_v1.2.docx. Older file names in Jira
   descriptions refer to the same approved baseline and do not create a
   second source.
4. docs/05_Inventory.md is a legacy placeholder, not a completed Inventory
   requirements baseline. This file is the canonical MESP-33 BRD artifact.
5. The approved Organization BRD owns Warehouse identity and hierarchy while
   Inventory owns stock effects. This reconciles the provisional architecture
   ownership note without creating a new ADR or implementation behavior.
6. The approved Master Data Product-only bounds are reused: Product/Item is one
   identity, Product tracking is configuration, and Inventory owns operational
   tracking. MESP-41 remains open for the operational policy.
7. The Procurement BRD owns the commercial chain and confirms that Inventory
   owns accepted Goods Receipt and Supplier Return stock effects. Supplier
   remains an external business party.
8. MESP-52 / PD-020 and MESP-56 / PD-021 are adopted only at their exact
   approved scope. No Finance, Sales, Inventory, Saudi, migration, or
   consolidation policy is inferred from them.
9. ADR-002, ADR-004, ADR-006, ADR-007, ADR-008, ADR-009, and ADR-018 constrain
   later feasibility and safety review. ADR-011 remains open and this BRD
   creates no runtime localization decision.
10. The document was reviewed structurally against the PRD text and repository
    references. Visual rendering of the source DOCX was not claimed because
    this environment lacks the optional LibreOffice/pdf2image rendering
    dependencies; no DOCX content was modified.

## 24. Coverage Checklist

| Required MESP-33 output | Location | Status |
|---|---|---|
| Business purpose and outcomes | Sections 2-3 | Covered |
| Opening Balance | Section 9.1 and scenarios 001-002 | Covered; MESP-51 remains open |
| Goods Receipt | Section 9.2 and scenarios 003-005 | Covered with Procurement/Finance boundaries |
| Warehouse Transfer | Section 9.3 and scenarios 006-007, 031, 034 | Covered with one/two-step and variance policy branches |
| Stock Adjustment | Section 9.4 and scenarios 008-009 | Covered |
| Inventory Count | Section 9.5 and scenarios 010-011, 028, 034 | Covered with movement-window and approval branches |
| Supplier Return | Section 9.6 and scenarios 012-013 | Covered |
| Customer Return | Section 9.7 and scenario 014 | Covered with Sales/Finance boundary |
| Stock Issue | Section 9.8 and scenario 015 | Covered as a bounded non-sales event |
| Immutable ledger and projected balances | Sections 2, 8, 10-12, 15, 21 | Covered |
| Availability, reservations, and tracking | Sections 7, 12, 16, 22 | Covered; MESP-41/MESP-45/MESP-46 remain open |
| Moving Weighted Average valuation | Sections 2, 8, 11, 16, scenarios 025-026 | Covered; scope/treatment/rate decisions remain open |
| Permissions, approvals, SoD, delegation | Sections 6, 13-15, 22 | Covered; MESP-42/MESP-55 remain open |
| Audit, immutability, failure, concurrency, reconciliation | Sections 8, 10, 12, 15, 18, 21 | Covered |
| Product/UOM/Organization/Procurement/Finance/Sales boundaries | Sections 8, 16, 19 | Covered |
| Saudi/localization and external gates | Section 17 and open rows 005-007 | Covered without legal/tax claims |
| Reports, KPIs, notifications, imports/exports | Sections 18-19 | Covered; MESP-53/MESP-50 remain open |
| Migration and opening evidence | Sections 9.1 and 19.3 | Covered; MESP-51 remains open |
| Operational readiness and production gates | Section 20 and open rows 005-007 | Covered without production claim |
| Given/When/Then scenarios | Section 21 | Covered by 35 business scenarios |
| Open decisions and named owners | Section 22 | Covered; no open answer inferred |
| Owner approval | Jira handoff and final document-control update | Approved in Jira comment 10743 against reviewed content head f17720fa5e574212856c547279672c1fcb8b1c42 |

## 25. Review and Approval Status

This v0.1 document is an Approved Business Baseline. The completed review
verified that:

- PRD anchors INV-001 through INV-008 and BR-006/BR-007 are covered;
- all eight Inventory flows, partial paths, exception paths, and correction
  rules are represented;
- the ledger is immutable, balances are projections, and opening balances use
  the ledger;
- Product/UOM/tracking, Organization/Warehouse, Procurement, Finance, Sales,
  Reporting, Migration, Saudi, and production boundaries are explicit;
- MESP-41 through MESP-55 remain visible and unresolved except the exact
  approved MESP-52/MESP-56 rows;
- no Retail POS, Wafra-specific core behavior, legal conclusion, or
  implementation instruction has been introduced; and
- MESP-34 and later work are not activated by this BRD.

Owner approval is recorded in MESP-33 comment 10743 against reviewed content
head f17720fa5e574212856c547279672c1fcb8b1c42. Approval of this BRD is a
business-requirements baseline only; it is not source implementation
authorization and does not close the named decision or production gates. The
normal PR merge and Jira closure evidence are tracked separately from this
business content.
