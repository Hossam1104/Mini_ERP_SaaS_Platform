# Mini ERP SaaS Platform — ERP Business Glossary

| | |
|---|---|
| **Document** | ERP Business Glossary |
| **Jira Task** | MESP-18 |
| **Epic** | MESP-1 — EPIC 01 Product Governance and BRD Foundation |
| **Release** | R0 — BRD and Product Foundation |
| **Source baseline** | `docs/MESP_PRD_v1.2.docx` — PRD v1.2, Final Approved Baseline, approved 31 July 2026 (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| **Status** | Draft — ready for Product Owner and Business Owner review |
| **Prepared** | 31 July 2026 |

## Purpose

This glossary is the mandatory vocabulary for every Business Requirements Document produced for the Mini ERP SaaS Platform. Every domain BRD (MESP-27 … MESP-40), every business rule, every acceptance scenario, and every future implementation Story must use these terms with these meanings. Where a term is ambiguous in everyday usage, this glossary states explicitly what the term **is not**.

## How to read an entry

Each entry carries eight fields:

- **Approved definition** — the binding definition.
- **Business meaning** — why the concept exists in business terms.
- **What it is not** — the boundary that prevents misuse.
- **Owning module** — the single domain accountable for the definition.
- **Related entities or documents** — what it connects to.
- **Example** — a concrete illustration.
- **Approval status** — one of the three values below.
- **Source** — where the definition comes from.

## Approval status legend

| Status | Meaning |
|---|---|
| **Approved Product Baseline** | Fixed by PRD v1.2 or by the approved decision list recorded in MESP-16. Must not be reopened during BRD work. |
| **Draft for BRD Validation** | Standard ERP meaning proposed by business analysis. Requires confirmation in the relevant BRD workshop. Not a product decision. |
| **Requires Business Decision** | Cannot be finalised until a named open decision (MESP-41 … MESP-56) is answered with approved evidence. |

## Scope statement

This glossary does **not** answer any open decision. Where a term depends on an unresolved decision, the entry names the Jira decision Task and stops there. No unresolved business rule has been invented, and no Wafra-specific vocabulary has been introduced.

---

# 1. SaaS and Organization

## Platform

**Approved definition:** The single cloud-native, multi-tenant software service that hosts and operates the Mini ERP product for all tenants.

**Business meaning:** The commercial and technical entity that the Platform Owner runs and sells subscriptions to. It is the outermost level of the approved hierarchy: Platform → Tenant → Company / Legal Entity → Branch → Warehouse.

**What it is not:** Not a customer's installation. Not a per-customer deployment. Not a synonym for Tenant. Not an on-premise product.

**Owning module:** SaaS Platform Administration.

**Related entities or documents:** Tenant, Subscription, Plan, Module, Entitlement, Country Pack.

**Example:** The Mini ERP SaaS Platform serves Wafra as Tenant #1 while remaining a generic multi-tenant product.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — multi-tenant SaaS product definition; approved decision list (MESP-16).

## Platform Owner

**Approved definition:** The organization that owns, operates, sells, and governs the Platform, and the internal roles acting on its behalf.

**Business meaning:** Accountable for tenant provisioning, subscription plans, entitlements, platform-wide configuration, release management, and platform-level support.

**What it is not:** Not a Tenant Administrator. Not a business customer of a tenant. The Platform Owner does not own tenant business data and must not act inside tenant data without an authorized, audited support mechanism.

**Owning module:** SaaS Platform Administration.

**Related entities or documents:** Platform, Tenant, Subscription, Audit Event.

**Example:** The Platform Owner creates a new tenant, assigns the subscription plan, and hands the Tenant Administrator role to the customer.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — platform administration scope (PLT / ADM requirement families).

## Tenant

**Approved definition:** An isolated customer subscription boundary on the Platform that owns its own users, master data, transactions, configuration, and reports.

**Business meaning:** The unit of commercial subscription and the primary unit of data isolation. All tenant business data is segregated so that no tenant can read or affect another tenant's data.

**What it is not:** Not a Company. A Tenant may contain one or many Companies. Not a Branch. Not a user. Not a database concept exposed to business users. Not the Platform.

**Owning module:** Multi-Tenancy.

**Related entities or documents:** Platform, Company / Legal Entity, Subscription, Tenant Administrator, Tenant Membership, Country Pack.

**Example:** Wafra is Tenant #1. A second, unrelated SME onboarded later is a separate Tenant with completely separate data.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — multi-tenancy and organization hierarchy; approved decision list (MESP-16).

## Tenant Administrator

**Approved definition:** The role inside a Tenant that is accountable for that tenant's companies, branches, warehouses, users, roles, permissions, and tenant-level configuration.

**Business meaning:** The customer-side owner of the tenant. Grants and revokes access, defines the organization structure, and approves tenant configuration.

**What it is not:** Not the Platform Owner. Cannot change subscription plans or entitlements that are controlled by the Platform Owner. Not automatically an approver of business documents — that is granted through Role and Permission.

**Owning module:** Identity and Access.

**Related entities or documents:** Tenant, User, Tenant Membership, Role, Permission, Entitlement.

**Example:** The Wafra Tenant Administrator creates the Riyadh branch, adds a purchasing user, and assigns the Purchasing role.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — identity, access, and tenant administration requirements.

## Subscription

**Approved definition:** The effective-dated commercial agreement that links a Tenant to a versioned Plan for a defined period and determines the Entitlements and limits available to that Tenant.

**Business meaning:** Selects the approved Plan version and dates from which its Entitlements apply. Release 1 assignment is manual and audited; Subscription metadata does not calculate a charge or create a subscription invoice, payment, or accounting transaction.

**What it is not:** Not a Sales Order or Sales Invoice in the tenant's ERP data, not a Trial, and not a direct Entitlement override. An Entitlement change requires a versioned Plan change or an effective-dated Subscription change.

**Owning module:** SaaS Platform Administration.

**Related entities or documents:** Tenant, Plan, Entitlement, Module.

**Example:** Tenant #1 holds an active subscription to the standard plan covering Procurement, Inventory, Sales, and Finance.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — subscription and plan requirements; MESP-52 decision approved by Hossam on 1 August 2026 and specified in MESP-27 BRD v0.10.

## Plan

**Approved definition:** A named, reusable, versioned commercial package that defines Modules, features, configurable limits, service/support tier, non-calculating price metadata, and effective dates.

**Business meaning:** Standardises what the Platform Owner makes available. Release 1 has one production Plan containing all approved B2B ERP Modules. A separate Restricted Validation Plan exists only in non-production to prove Entitlement denial and cannot be sold, assigned in production, or treated as a Trial.

**What it is not:** Not a per-Tenant customization, Trial offering, Role, Permission, pricing engine, invoice, payment, or accounting transaction. Price metadata is descriptive and non-calculating. A Plan governs what is *available*; Roles and Permissions govern what a *User* may do.

**Owning module:** SaaS Platform Administration.

**Related entities or documents:** Subscription, Entitlement, Module, Tenant.

**Example:** The production Release 1 Plan enables all approved B2B ERP Modules, carries a named support tier and price metadata, and excludes Retail POS.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — subscription and plan requirements; MESP-52 decision approved by Hossam on 1 August 2026 and specified in MESP-27 BRD v0.10.

## Entitlement

**Approved definition:** The effective, evaluated right of a Tenant to use a specific Module, feature, or capacity, derived from its Subscription and Plan.

**Business meaning:** The evaluated answer to "is this capability available to this Tenant now?" — a commercial gate derived from the effective Plan and Subscription, not a security Permission.

**What it is not:** **Not a Permission and not a per-Tenant override.** Entitlement is commercial and Tenant-wide; Permission is security and User-specific. Entitlement changes use a versioned Plan or effective-dated Subscription change. A security or safety restriction may temporarily block an Entitlement but cannot grant an unapproved one. See clarification 18.

**Owning module:** SaaS Platform Administration.

**Related entities or documents:** Subscription, Plan, Module, Permission, Tenant.

**Example:** A tenant is entitled to the Inventory module; within it, only users holding the stock-adjustment permission may post an adjustment.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — entitlement model; MESP-52 decision approved by Hossam on 1 August 2026 and specified in MESP-27 BRD v0.10.

## Module

**Approved definition:** A cohesive functional area of the ERP product with its own business capabilities, data, rules, and reports — for example Procurement, Inventory, Finance, B2B Sales.

**Business meaning:** The unit of product packaging, BRD authorship, and business ownership. Each glossary term has exactly one owning module.

**What it is not:** Not a microservice. The approved architecture baseline is a Modular Monolith, so a Module is a business and code boundary inside one deployable, not a separately deployed service.

**Owning module:** Product Governance.

**Related entities or documents:** Plan, Entitlement, Jira Epic, BRD Task.

**Example:** The Procurement module owns Purchase Request, Purchase Order, Goods Receipt, and Purchase Invoice.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — modular monolith architecture baseline; approved decision list (MESP-16).

## Company

**Approved definition:** An operating business unit inside a Tenant that owns its own chart of accounts, fiscal calendar, accounting books, and business documents.

**Business meaning:** The legal and accounting boundary. A Tenant may contain multiple Companies / Legal Entities, each with its own books and financial statements.

**What it is not:** Not a Tenant — a Tenant may hold several Companies. Not a Branch, Department, or Cost Center. Multiple Companies do not imply financial consolidation, intercompany automation, elimination entries, transfer pricing, or consolidated statements in Release 1.

**Owning module:** Organization Structure.

**Related entities or documents:** Tenant, Legal Entity, Branch, Chart of Accounts, Fiscal Calendar, General Ledger.

**Example:** A tenant operating two legally separate businesses holds two Companies, each with its own books.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — organization hierarchy; approved decision list (MESP-16); MESP-56 decision approved by Hossam on 1 August 2026. Detailed operating rules remain in MESP-30.

## Legal Entity

**Approved definition:** The legally registered identity of a Company, carrying its registration identifiers, tax identifiers, legal name, and statutory reporting obligations.

**Business meaning:** Determines who is legally liable, which accounting boundary and tax registration apply, and what appears on legally binding documents. One Tenant may contain multiple Legal Entities.

**What it is not:** Not a Branch. Not a separate hierarchy level — Company and Legal Entity are the same level of the approved hierarchy. Release 1 does not provide financial consolidation, intercompany automation, elimination entries, transfer pricing, or consolidated statements.

**Owning module:** Organization Structure.

**Related entities or documents:** Company, Country Pack, Tax Category, Sales Invoice, Purchase Invoice.

**Example:** A Saudi legal entity with its own commercial registration and VAT registration number printed on every tax invoice it issues.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — legal entity requirements; MESP-56 decision approved by Hossam on 1 August 2026. Detailed operating rules remain in MESP-30.

## Branch

**Approved definition:** An operational location or business unit belonging to a Company, used to organise operations, responsibility, and reporting below Company level.

**Business meaning:** Where business activity is performed and attributed. Documents are normally raised in the context of a Branch.

**What it is not:** **Not a Warehouse.** A Branch is an operational and organizational unit; a Warehouse is a physical stock-holding location. A Branch may have several Warehouses, and a Warehouse belongs to exactly one Branch. A Branch is also not a Legal Entity and does not issue statutory financial statements of its own.

**Owning module:** Organization Structure.

**Related entities or documents:** Company, Warehouse, Department, Cost Center, business documents.

**Example:** The Riyadh branch of a company, holding a main warehouse and a returns warehouse.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — organization hierarchy; approved decision list (MESP-16).

## Warehouse

**Approved definition:** A physical or logical location belonging to a Branch where stock is held and where stock quantities and values are maintained.

**Business meaning:** The lowest level of the approved organization hierarchy and the level at which stock balances are meaningful. Every stock movement names a source warehouse, a destination warehouse, or both.

**What it is not:** Not a Branch. Not a Company. Not an accounting entity — a Warehouse does not have its own chart of accounts. Not a bin or shelf location unless bin-level tracking is later approved.

**Owning module:** Inventory.

**Related entities or documents:** Branch, Stock Balance, Stock Movement, Goods Receipt, Warehouse Transfer, Inventory Count.

**Example:** The Riyadh main warehouse holding 400 units of an item, and the Riyadh returns warehouse holding 12 units of the same item.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — organization hierarchy and inventory model; approved decision list (MESP-16).

## Department

**Approved definition:** An internal organizational grouping used to attribute responsibility and, where enabled, cost.

**Business meaning:** Supports "who asked for this" and "who is responsible for this spend", most visibly on Purchase Requests.

**What it is not:** Not a Branch. Not a Cost Center, although a Department may be mapped to one. Not a security boundary — access is controlled by Role, Permission, and Access Scope.

**Owning module:** Organization Structure.

**Related entities or documents:** Branch, Cost Center, Purchase Request, Employee.

**Example:** The Maintenance department raises a purchase request for spare parts.

**Approval status:** Draft for BRD Validation

**Source:** Standard ERP practice. Requires confirmation in the Organization and Company Structure BRD (MESP-30).

## Cost Center

**Approved definition:** A financial reporting dimension used to accumulate costs for a defined area of responsibility.

**Business meaning:** Enables cost reporting below account level without creating additional general ledger accounts.

**What it is not:** Not a Department, although the two are frequently aligned. Not a Branch. Not a Company. Not a profit centre, and not a project.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Journal Entry, Department, Chart of Accounts, Purchase Invoice.

**Example:** Maintenance costs posted to the maintenance cost center while sitting in a single expense account.

**Approval status:** Approved Product Baseline

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §8.2 defines the Finance-owned reporting-dimension concept. The Release 1 dimension catalogue and Cost Center policy remain open in FIN-OD-09 / MESP-110; this is not a circular dependency on closed MESP-34.

## Fiscal Calendar

**Approved definition:** The definition of a Company's financial year and the Fiscal Periods within it.

**Business meaning:** Determines which period a posting falls into and when a period may be closed so that results become final.

**What it is not:** Not the Gregorian or Hijri calendar used for display. Not a tax filing calendar, although the two may align.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Company, Fiscal Period, Posting, Journal Entry.

**Example:** A financial year running January to December with twelve monthly periods.

**Approval status:** Approved Product Baseline

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §9.9 defines the Finance-owned Company/Legal Entity calendar concept. Exact Fiscal Year boundary, year-end, carry-forward, retained-earnings, reopen, and reclose mechanics remain FIN-OD-01 / FIN-OD-09 / MESP-110.

## Fiscal Period

**Approved definition:** A bounded date range inside a Fiscal Calendar into which financial transactions are posted, and which can be open or closed.

**Business meaning:** The control that stops results changing after they have been reported. Once a period is closed, corrections are made by Reversal in an open period, never by editing history.

**What it is not:** Not a document status. Not a reporting filter alone — it enforces posting control. Closing a period is not the same as closing a document.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Fiscal Calendar, Posting, Journal Entry, Reversal, Closed.

**Example:** July is closed, so a July correction is posted as a reversal dated in August.

**Approval status:** Approved Product Baseline

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §9.9 defines the open/soft-close/closed period control and immutable-history boundary. Exact close/reopen/year-end policy remains FIN-OD-01 / FIN-OD-09 / MESP-110.

## Country Pack

**Approved definition:** A reusable set of country-specific configuration, rules, tax behaviour, document formats, and compliance requirements applied to a Company operating in that country.

**Business meaning:** Keeps localisation out of the core product. Saudi Arabia is the initial launch market, so the Saudi Country Pack carries VAT behaviour, Arabic and English document requirements, and e-invoicing obligations.

**What it is not:** Not tenant customisation. Not Wafra-specific configuration. A Country Pack is generic to every tenant operating in that country and must never encode one customer's preferences.

**Owning module:** Saudi Country Pack.

**Related entities or documents:** Legal Entity, Tax Category, Sales Invoice, Base Currency, compliance reporting.

**Example:** The Saudi Country Pack setting SAR as base currency, applying Saudi VAT treatment, and requiring bilingual invoice output.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — Saudi localization requirements (KSA family). E-invoicing launch scope is open in **MESP-49 — Confirm Saudi e-invoicing launch scope**.

---

# 2. Identity and Access

## User

**Approved definition:** An authenticated identity that can sign in to the Platform and act inside one or more Tenants according to granted Roles and Permissions.

**Business meaning:** The actor recorded on every business action for accountability and audit.

**What it is not:** **Not an Employee.** A User is a login identity; an Employee is an HR record. Not every employee is a user, and a user may not be an employee (for example an external accountant). A Supplier is never a User in Release 1.

**Owning module:** Identity and Access.

**Related entities or documents:** Tenant Membership, Role, Permission, Audit Event, Employee.

**Example:** A purchasing officer signs in as a user and creates a Purchase Order; the audit trail records that user, not a department.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — identity and access requirements; approved decision list (MESP-16).

## Employee

**Approved definition:** A person recorded as working for a Company, used for business attribution such as requester, buyer, salesperson, or approver reference.

**Business meaning:** Lets the business express "who in the organization is this about" independently of who logged in.

**What it is not:** Not a User. Not a login. Not a full HR module — payroll, contracts, leave, and performance are out of scope for Release 1.

**Owning module:** Organization Structure.

**Related entities or documents:** User, Department, Branch, Purchase Request, Sales Order.

**Example:** A purchase request records the requesting employee, while the audit trail records the user who entered it.

**Approval status:** Draft for BRD Validation

**Source:** Standard ERP practice. Employee scope requires confirmation in MESP-30. No HR scope is implied.

## Tenant Membership

**Approved definition:** The link that grants a User access to a specific Tenant, together with the roles and access scope that apply within that Tenant.

**Business meaning:** Makes multi-tenant access explicit and revocable. Removing the membership removes all access to that tenant's data without deleting the user identity.

**What it is not:** Not a Role by itself. Not a Subscription. Not a permission grant on its own.

**Owning module:** Identity and Access.

**Related entities or documents:** User, Tenant, Role, Access Scope, Audit Event.

**Example:** An external accountant holds memberships in two tenants and sees only the data of the tenant currently in context.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — tenant isolation and access requirements.

## Role

**Approved definition:** A named, reusable bundle of Permissions assigned to Users to express a job function.

**Business meaning:** Keeps access administration manageable and consistent. Access is granted by job function, not by individual capability.

**What it is not:** Not a job title. Not an Employee. Not an approval authority by itself — approval authority is an explicit permission with limits, defined in the governance model.

**Owning module:** Identity and Access.

**Related entities or documents:** Permission, User, Tenant Membership, Separation of Duties, Approver.

**Example:** A Purchasing role bundling the create-purchase-request, create-purchase-order, and record-supplier-confirmation permissions.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — role-based access control requirements.

## Permission

**Approved definition:** The atomic right to perform a specific action on a specific object type, such as create, read, update, submit, approve, post, reverse, or cancel.

**Business meaning:** The security control that determines whether a user may perform an action.

**What it is not:** **Not an Entitlement.** Permission is per user and enforces security; Entitlement is per tenant and enforces commercial packaging. Both must allow an action for it to succeed. See clarification 18.

**Owning module:** Identity and Access.

**Related entities or documents:** Role, User, Entitlement, Access Scope, Separation of Duties.

**Example:** Post Goods Receipt is a distinct permission from Create Goods Receipt.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — permission model requirements.

## Access Scope

**Approved definition:** The data boundary within which a User's permissions apply — for example a specific Company, Branch, or Warehouse.

**Business meaning:** Two users may hold the same role yet see and act on different data because their scopes differ.

**What it is not:** Not a Permission. Not a Role. Not a report filter — it is enforced, not cosmetic.

**Owning module:** Identity and Access.

**Related entities or documents:** Tenant Membership, Role, Permission, Company, Branch, Warehouse.

**Example:** A storekeeper holds the goods-receipt permission scoped to the Riyadh warehouse only.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — access control requirements. Scope granularity requires confirmation in the Identity and Access BRD (MESP-28).

## Separation of Duties

**Approved definition:** The control that prevents one User from performing two conflicting steps of the same business transaction.

**Business meaning:** Reduces fraud and error risk — for example the person who creates a purchase order should not be the sole approver, and the person who receives goods should not also release the supplier payment.

**What it is not:** Not the same as an approval workflow. An approval workflow defines *who signs*; separation of duties defines *who must not sign* because of a conflicting prior action.

**Owning module:** Security and Audit.

**Related entities or documents:** Role, Permission, Approval, Approver, Audit Event.

**Example:** The user who posted the goods receipt is blocked from approving the matching purchase invoice.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — control and audit requirements. Specific conflict pairs require confirmation in the Security, Audit, and Data Governance BRD (MESP-38).

## Approval

**Approved definition:** A recorded business authorisation that allows a document to move from a submitted state to an authorised state.

**Business meaning:** Evidence that an accountable person agreed to the transaction, retained for audit.

**What it is not:** **Not Posting.** Approval is a business authorisation; Posting is the accounting or inventory effect. A document may be approved and not yet posted. See clarification 15.

**Owning module:** Product Governance, applied by every transactional module.

**Related entities or documents:** Approver, Document Status, Submitted, Approved, Audit Event, Separation of Duties.

**Example:** A purchase order above a threshold is approved by a manager before it is issued to the supplier.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — approval requirements. Workflow thresholds and levels are open in **MESP-42**; delegation and escalation are open in **MESP-55**.

## Approver

**Approved definition:** A User authorised to grant or refuse an Approval for a defined document type within defined limits.

**Business meaning:** Names accountability for authorising business commitments and spend.

**What it is not:** Not automatically a Tenant Administrator. Not a document owner. Not a reviewer — a reviewer comments, an approver authorises.

**Owning module:** Identity and Access.

**Related entities or documents:** Approval, Role, Permission, Separation of Duties.

**Example:** A branch manager approving purchase orders up to an agreed value limit.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — approval requirements. Approval limits, delegation, escalation, and out-of-office behaviour are open in **MESP-55**.

## Audit Event

**Approved definition:** An immutable record of a significant action, capturing who performed it, what changed, when, in which tenant and company context, and from what source document.

**Business meaning:** The evidence base for control, dispute resolution, and compliance review.

**What it is not:** Not an application log for engineers. Not a report. Not editable or deletable by tenant users.

**Owning module:** Security and Audit.

**Related entities or documents:** User, Business Document, Immutable Record, Correlation Identifier.

**Example:** An audit event recording that a specific user posted a stock adjustment of minus 5 units with a stated reason.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — audit and traceability requirements. Retention period is open in **MESP-50 — Confirm tenant data residency and retention policy**.

---

# 3. Business Parties

## Business Party

**Approved definition:** The umbrella business concept covering Supplier and Business Customer: an external counterparty recorded as master data inside a Tenant.

**Business meaning:** Names what a Supplier and a Business Customer share — external identity, contact/address structure, and duplicate-detection treatment — without merging their distinct approved meanings, roles, or lifecycles.

**Duplicate detection across the two roles:** Duplicate detection prevents a second record **within the same party role** — a duplicate Supplier, or a duplicate Business Customer. It does **not** treat a match across the two roles as a duplicate. As the Supplier entry below states, the same legal company may legitimately exist as both a Supplier record and a Business Customer record; a matching tax registration number or legal name across those roles is therefore surfaced for review and optional linkage only, and must never automatically reject the second role.

**What it is not:** Not a new record type, and **not a unified Party database or record** — Supplier and Business Customer remain distinct business roles with distinct records unless a later approved party-unification decision changes that. Not a system User under any circumstance. Not a Tenant. Does not replace or narrow the existing Supplier or Business Customer definitions below.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Supplier, Business Customer, Supplier Contact, Customer Contact.

**Example:** A distributor a Tenant both buys from and sells to is recorded as one Supplier and one Business Customer. Creating the second record succeeds; the system flags the shared tax registration number for review and optional linkage rather than rejecting it as a duplicate.

**Approval status:** Draft for BRD Validation

**Source:** New entry proposed by the Master Data and Product Catalog BRD (MESP-31) to name a concept the glossary previously left implicit.

## Supplier

**Approved definition:** An external business party from whom a Company procures goods or services, recorded as master data inside a Tenant.

**Business meaning:** The counterparty in the Purchase-to-Pay lifecycle and the holder of a payable balance.

**What it is not:** **Not a system User.** Suppliers do not log in, do not receive system accounts, and do not enter data. Not a Tenant — a supplier is business master data belonging to one tenant, not a subscriber to the Platform. Not a Business Customer, although the same legal company may exist as both records.

**Owning module:** Procurement.

**Related entities or documents:** Supplier Contact, Purchase Order, Goods Receipt, Purchase Invoice, Supplier Payment, Accounts Payable, Payment Terms.

**Example:** A parts distributor is recorded as a supplier; its confirmation of a purchase order is typed in by an authorized purchasing user.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — suppliers are external business parties and are not system users, and supplier responses are manually recorded by authorized purchasing users.

## Supplier Contact

**Approved definition:** A named person at a Supplier, recorded for communication and document correspondence.

**Business meaning:** Identifies who was dealt with when an order was placed or a confirmation received.

**What it is not:** Not a User. Not an Employee. Recording a contact grants no system access whatsoever.

**Owning module:** Procurement.

**Related entities or documents:** Supplier, Purchase Order, Supplier Confirmation.

**Example:** The supplier's sales representative whose emailed confirmation is recorded against the purchase order by a purchasing user.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — supplier master data requirements.

## Business Customer

**Approved definition:** An external business party to whom a Company sells goods or services under B2B terms, recorded as master data inside a Tenant.

**Business meaning:** The counterparty in the Order-to-Cash lifecycle, holding a receivable balance and, where approved, credit terms and limits.

**What it is not:** **Not a Platform Customer.** A Business Customer is the tenant's customer; a Platform Customer is the Tenant itself, which subscribes to the Platform. Not an anonymous retail consumer — retail consumers belong to Retail POS, which is excluded from Release 1. Not a User.

**Owning module:** B2B Sales.

**Related entities or documents:** Customer Contact, Quotation, Sales Order, Delivery, Sales Invoice, Customer Receipt, Accounts Receivable, Credit Limit.

**Example:** A contracting company that orders on account with 30-day payment terms and an approved credit limit.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — B2B sales scope; approved decision list (MESP-16) — Release 1 supports B2B ERP operations only.

## Customer Contact

**Approved definition:** A named person at a Business Customer, recorded for communication and document correspondence.

**Business meaning:** Identifies who placed or confirmed an order on the customer side.

**What it is not:** Not a User. Not a portal login — no customer self-service access is in Release 1 scope.

**Owning module:** B2B Sales.

**Related entities or documents:** Business Customer, Quotation, Sales Order, Delivery.

**Example:** The customer's procurement officer named on the sales order.

**Approval status:** Draft for BRD Validation

**Source:** Standard ERP practice. Requires confirmation in the B2B Sales BRD (MESP-35).

## Payment Terms

**Approved definition:** The agreed rules that determine when an invoice becomes due, expressed as a defined interval or schedule from an agreed base date.

**Business meaning:** Drives due dates, ageing, collection activity, and cash forecasting on both the payable and receivable sides.

**What it is not:** Not a Credit Limit — terms govern *when* payment is due, a credit limit governs *how much* exposure is allowed. Not a payment method. Not a discount scheme unless early-settlement discounts are separately approved.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Supplier, Business Customer, Purchase Invoice, Sales Invoice, Accounts Payable, Accounts Receivable.

**Example:** Net 30 days from invoice date.

**Approval status:** Approved Product Baseline

**Source:** The Payment Terms concept is defined in `docs/23_Finance_and_Accounting_BRD.md` §§6, 9.3, 9.5 and the Master Data contract. Exact Release 1 shape, base date, interval versus schedule/installments, discount semantics, due-date ownership, and historical preservation remain FIN-OD-09 / MESP-110.

## Credit Limit

**Approved definition:** The maximum outstanding exposure permitted for a Business Customer before further orders require authorisation or are blocked.

**Business meaning:** Controls the risk of selling on account to a customer who has not paid.

**What it is not:** Not Payment Terms. Not Credit Exposure — the limit is the ceiling, the exposure is the current usage against it.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Business Customer, Credit Exposure, Sales Order, Accounts Receivable, Approval.

**Example:** A customer with a defined limit whose new order requires authorisation because the limit would be exceeded.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — credit control requirement. Enforcement points, blocking versus warning, and override authority are open in **MESP-46 — Confirm B2B customer credit-control policy**.

---

# 4. Product and Catalog

## Product

**Approved definition:** A catalogued good or service that a Company can buy, sell, or hold, defined once as master data and reused across all transactions.

**Business meaning:** The definition of *what* a thing is — its description, category, units, tax treatment, and whether it is stock-tracked.

**What it is not:** **Not Stock.** A product is a definition; stock is a quantity of that product held in a warehouse at a point in time. A product with zero stock still exists. See clarification 7.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Item, SKU, Category, Unit of Measure, Price List, Tax Category, Stock Balance.

**Example:** A 20 mm copper fitting exists as a product whether or not any unit is currently in a warehouse.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — master data and catalog requirements.

## Item

**Approved definition:** In Release 1, the Product master record is the
transactable Item concept used on business documents and in the stock ledger;
Product and Item are one master-data identity unless a later variant decision
explicitly changes that boundary.

**Business meaning:** What is actually ordered, received, issued, sold, and valued.

**What it is not:** Not a document line by itself. Not a Batch or Serial Number,
which identify specific instances of an item. Not a separate variant or second
master-data entity in the M95-SL-03 Product slice.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Product, SKU, Stock Movement, Purchase Order line, Goods Receipt Line.

**Example:** The item appearing on a goods receipt line with an accepted quantity of 100.

**Approval status:** Approved Product Baseline

**Source:** Approved MESP-31 business baseline and MESP-101 Product-only owner
decision evidence (Jira comment `10671`). Product-versus-variant behavior is
not part of the Release-1 Product identity slice.

## SKU

**Approved definition:** The unique, stable identifier used within a Tenant to
reference a specific Product/Item. The value may be manually entered,
imported, or generated; the Product slice does not define a generator or
sequence policy.

**Business meaning:** The single code everyone uses to mean the same thing across procurement, inventory, sales, and reporting.

**What it is not:** Not a Barcode. Not a supplier's own part number. Not a batch or serial identifier.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Product, Item, Barcode, Stock Balance.

**Example:** An internal SKU printed on the stock count sheet.

**Approval status:** Approved Product Baseline

**Source:** MESP-31 approved business baseline and MESP-101 MD-OD-003
Product-only owner decision evidence (Jira comment `10671`).

## Barcode

**Approved definition:** A scannable code mapped to a Product/Item and used to
identify it during physical handling. A Product may have zero or more
Tenant-unique barcode values.

**Business meaning:** Speeds and de-risks receiving, transfer, counting, and picking.

**What it is not:** Not the SKU, although it may carry the same value. Not
proof of ownership or stock. No EAN, GS1, symbology, or checksum rule is
invented by the Product slice.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Item, SKU, Goods Receipt, Inventory Count.

**Example:** Scanning a carton barcode while recording a goods receipt.

**Approval status:** Approved Product Baseline

**Source:** MESP-31 approved business baseline and MESP-101 MD-OD-003
Product-only owner decision evidence (Jira comment `10671`).

## Category

**Approved definition:** A classification applied to Products for grouping, reporting, default rules, and analysis.

**Business meaning:** Enables spend analysis, sales analysis, and default settings such as tax or account determination.

**What it is not:** Not a Warehouse. Not a Price List. Not a permission boundary.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Product, Tax Category, Posting Rule, reporting.

**Example:** Grouping all plumbing consumables under one category for purchase spend reporting.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — catalog structure. Hierarchy depth requires confirmation in MESP-31.

## Unit of Measure

**Approved definition:** The standard quantity unit in which an Item is counted, bought, sold, stored, or valued.

**Business meaning:** Prevents quantity errors and makes conversion between purchase, stock, and sales units explicit and auditable.

**What it is not:** Not packaging text. Not a free-text field. A unit of measure without a defined conversion to the Base Unit cannot be used for stock movement.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Base Unit, Purchase Unit, Sales Unit, Item, Stock Movement.

**Example:** Box, carton, and piece defined with conversions to the base unit.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — unit of measure requirements.

## Base Unit

**Approved definition:** The single unit of measure in which an Item's stock quantity and inventory valuation are always maintained.

**Business meaning:** Guarantees that all stock balances and valuations for an item are comparable, regardless of how it was bought or sold.

**What it is not:** Not necessarily the purchase or sales unit. Not changeable once stock transactions exist, because it would invalidate historical quantities and valuation.

**Owning module:** Inventory.

**Related entities or documents:** Unit of Measure, Purchase Unit, Sales Unit, Stock Balance, Moving Weighted Average.

**Example:** An item bought in boxes of 12 and sold in pieces, held in stock in pieces as the base unit.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — inventory quantity and valuation requirements.

## Purchase Unit

**Approved definition:** The default unit of measure used when procuring an Item, together with its conversion to the Base Unit.

**Business meaning:** Lets buyers order the way suppliers sell while stock stays in one consistent unit.

**What it is not:** Not the Base Unit. Not the Sales Unit. Not a per-supplier price agreement.

**Owning module:** Procurement.

**Related entities or documents:** Unit of Measure, Base Unit, Purchase Order, Goods Receipt Line.

**Example:** Ordering 10 boxes, receiving stock as 120 pieces in the base unit.

**Approval status:** Draft for BRD Validation

**Source:** Standard ERP practice aligned to PRD v1.2. Requires confirmation in the Procurement BRD (MESP-32).

## Sales Unit

**Approved definition:** The default unit of measure used when selling an Item, together with its conversion to the Base Unit.

**Business meaning:** Lets sales quote and invoice in the customer's expected unit without changing how stock is held.

**What it is not:** Not the Base Unit. Not the Purchase Unit. Not a pricing rule by itself.

**Owning module:** B2B Sales.

**Related entities or documents:** Unit of Measure, Base Unit, Sales Order, Delivery, Sales Invoice.

**Example:** Selling by the piece while purchasing by the box.

**Approval status:** Draft for BRD Validation

**Source:** Standard ERP practice aligned to PRD v1.2. Requires confirmation in MESP-35.

## Price List

**Approved definition:** A named set of prices for Items, valid for a defined context such as customer group, currency, or period.

**Business meaning:** Controls what price is proposed on quotations, sales orders, and invoices, and keeps pricing consistent and auditable.

**What it is not:** Not a Credit Limit. Not a discount approval. Not a cost — price is what the customer pays, cost is what inventory valuation records.

**Owning module:** B2B Sales.

**Related entities or documents:** Business Customer, Quotation, Sales Order, Sales Invoice, Transaction Currency.

**Example:** A SAR price list for standard B2B customers and a separate one for contract customers.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — sales pricing requirements. Structure and precedence require confirmation in MESP-35.

## Tax

**Approved definition:** A configured, effective-dated rate or rule applied to a transaction.

**Business meaning:** The reusable master-data fact that a rate exists, what it is, and from when — independent of which country-specific treatment ultimately selects it.

**What it is not:** Not Tax Category, which is the Saudi-Country-Pack-owned classification that determines *which* tax treatment applies to a given Item or transaction. Not a hard-coded value inside transaction logic.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Tax Category, Product, Sales Invoice, Purchase Invoice.

**Example:** A 15% VAT rate effective from a stated date is a Tax master record; whether a specific Item is standard-rated or zero-rated is Tax Category.

**Approval status:** Draft for BRD Validation

**Source:** New entry proposed by the Master Data and Product Catalog BRD (MESP-31); PRD v1.2 FIN-007, KSA-002 — tax rates are configured and effective-dated, never hard-coded into transaction logic.

## Tax Category

**Approved definition:** The classification that determines the tax treatment applied to an Item or transaction under the applicable Country Pack.

**Business meaning:** Drives correct tax calculation, correct tax accounts, and correct statutory reporting.

**What it is not:** Not a Category used for analysis. Not a price. Not a country-independent value — tax treatment is resolved through the Country Pack.

**Owning module:** Saudi Country Pack.

**Related entities or documents:** Country Pack, Product, Sales Invoice, Purchase Invoice, Legal Entity.

**Example:** A standard-rated item and a zero-rated item resolving to different tax treatments on the same invoice.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — Saudi tax requirements. Detailed treatment and e-invoicing coupling are open in **MESP-49**.

## Active / Inactive

**Approved definition:** The two ordinary lifecycle states of a master-data record. Active means the record may be selected for a new transaction, price, or assignment; Inactive means it may not, but remains visible for historical reference and reporting.

**Business meaning:** Lets a Tenant retire a Product, Supplier, Business Customer, Price List, Tax, Payment Term, Currency, or Exchange Rate from future use without deleting it or disturbing any transaction that already referenced it.

**What it is not:** Not deletion. Not a statement that the record's history is invalid. Not a domain-specific status such as Tenant lifecycle status (see `docs/13_Multi_Tenancy_BRD.md`) or Organization lifecycle status (see `docs/14_Organization_and_Company_Structure_BRD.md`), which remain their own approved vocabularies.

**Owning module:** Master Data and Catalog (this is the first BRD to define the term generically; other modules may reuse it without redefining it).

**Related entities or documents:** Product, Category, Unit of Measure, Supplier, Business Customer, Price List, Tax, Payment Term, Currency, Exchange Rate.

**Example:** A discontinued Product is set Inactive; it can no longer be added to a new Purchase Order, but every historical Purchase Order that used it is unchanged.

**Approval status:** Draft for BRD Validation

**Source:** New entry proposed by the Master Data and Product Catalog BRD (MESP-31); PRD v1.2 PLT-003 — authorized users can create, review, activate, deactivate, import, export, and search shared business master data.

---

# 5. Procurement

## Purchase Request

**Approved definition:** An internal document raised by an authorised requester asking for goods or services to be procured, subject to internal approval.

**Business meaning:** The first step of the approved Purchase-to-Pay lifecycle. It captures internal need and demand ownership before any commitment to a supplier exists.

**What it is not:** Not a commitment to a supplier. Not a Purchase Order. It creates no supplier obligation, no liability, and no stock effect. It is not sent to a supplier.

**Owning module:** Procurement.

**Related entities or documents:** Employee, Department, Purchase Order, Approval.

**Example:** Maintenance requests 20 units of a spare part; the request is approved internally before a purchase order is raised.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved procurement lifecycle: Purchase Request to Purchase Order to Supplier Confirmation to Goods Receipt to Purchase Invoice to Supplier Payment. Approval workflow detail is open in **MESP-42**.

## Supplier Quotation

**Approved definition:** A supplier's recorded offer of prices, quantities, and terms for requested items, entered into the system by an authorised purchasing user.

**Business meaning:** Provides comparable evidence for supplier selection and price justification.

**What it is not:** Not a Purchase Order. Not a commitment by the Company. Not entered by the supplier — suppliers are not system users, so a purchasing user records the offer.

**Owning module:** Procurement.

**Related entities or documents:** Supplier, Purchase Request, Purchase Order.

**Example:** Three supplier quotations recorded against one purchase request before a supplier is chosen.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — procurement scope; approved decision list (MESP-16) — supplier responses are manually recorded. Sourcing and comparison detail requires confirmation in MESP-32.

## Purchase Order

**Approved definition:** An authorised commitment issued to a Supplier to deliver specified items, quantities, prices, and terms.

**Business meaning:** The commercial commitment. It creates an expected obligation to receive and to pay, and it establishes the outstanding quantity the business is waiting for.

**What it is not:** **A Purchase Order does not increase stock.** It creates no stock movement, no stock ledger entry, and no financial posting to inventory or payables. It is not a Goods Receipt and not a Purchase Invoice. See clarification 8.

**Owning module:** Procurement.

**Related entities or documents:** Supplier, Purchase Request, Supplier Confirmation, Goods Receipt, Purchase Invoice, Expected Quantity, Outstanding Quantity.

**Example:** A purchase order for 100 units raises an expected quantity of 100 and zero stock on hand.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — a Purchase Order does not increase stock.

## Supplier Confirmation

**Approved definition:** The recorded acknowledgement by a Supplier of what will be supplied against a Purchase Order, including quantities and dates, entered by an authorised purchasing user.

**Business meaning:** Turns an assumption into a tracked commitment and gives the business a reliable expected delivery position.

**What it is not:** Not a Goods Receipt — nothing has physically arrived. Not a stock increase. Not a supplier-entered record. Not an invoice.

**Owning module:** Procurement.

**Related entities or documents:** Purchase Order, Supplier, Supplier Contact, Partial Supplier Confirmation, Expected Quantity.

**Example:** A supplier confirms 80 of 100 units for the requested date; the confirmation is typed in by the purchasing user with the supplier's message retained as evidence.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — approved lifecycle; approved decision list (MESP-16). Confirmation and partial-confirmation rules are open in **MESP-43 — Confirm supplier confirmation and partial-confirmation rules**.

## Partial Supplier Confirmation

**Approved definition:** A Supplier Confirmation covering less than the full ordered quantity, or covering different dates for different quantities of the same order line.

**Business meaning:** Reflects real supply behaviour where a supplier commits to part of an order now and the remainder later, or not at all.

**What it is not:** Not a cancellation of the unconfirmed balance. Not a partial receipt. Not a change to the ordered quantity unless the purchase order is formally amended.

**Owning module:** Procurement.

**Related entities or documents:** Supplier Confirmation, Purchase Order, Outstanding Quantity, Partially Completed.

**Example:** 60 units confirmed for this week, 40 units unconfirmed and still outstanding.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — open clarification. Open in **MESP-43**.

## Goods Receipt

**Approved definition:** The document recording that goods have physically arrived at a Warehouse and been accepted into stock.

**Business meaning:** **The event that increases stock.** Stock increases only through a posted Goods Receipt or another authorized inventory-in document.

**What it is not:** Not a Purchase Order. **Not a Purchase Invoice** — a goods receipt records physical arrival, an invoice records the supplier's financial claim. A goods receipt does not create a supplier liability by itself. See clarifications 8 and 9.

**Owning module:** Inventory, executed within the Procurement lifecycle.

**Related entities or documents:** Purchase Order, Goods Receipt Line, Stock Movement, Stock Ledger, Purchase Invoice, Three-Way Match, Warehouse.

**Example:** 80 of 100 ordered units arrive and are accepted; posting the goods receipt raises on-hand stock by 80 and leaves 20 outstanding.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — stock increases only through a posted Goods Receipt or another authorized inventory-in document.

## Goods Receipt Line

**Approved definition:** A single item line of a Goods Receipt, stating the item, the accepted quantity, the rejected quantity, the receiving warehouse, and any tracking attributes.

**Business meaning:** The level at which stock actually moves and at which matching against the purchase order and invoice is performed.

**What it is not:** Not a purchase order line, although it references one. Not an invoice line.

**Owning module:** Inventory.

**Related entities or documents:** Goods Receipt, Item, Accepted Quantity, Rejected Quantity, Warehouse, Batch, Serial Number.

**Example:** One line receiving 80 accepted and 2 rejected units into the Riyadh main warehouse.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — receiving requirements. Line-level tracking attributes depend on **MESP-41** and require confirmation in the Inventory BRD (MESP-33).

## Accepted Quantity

**Approved definition:** The quantity on a Goods Receipt Line that is taken into stock as usable.

**Business meaning:** The only received quantity that increases on-hand stock and enters inventory valuation.

**What it is not:** Not the ordered quantity. Not the invoiced quantity. Not the rejected quantity.

**Owning module:** Inventory.

**Related entities or documents:** Goods Receipt Line, On-Hand Quantity, Stock Movement, Three-Way Match.

**Example:** 78 accepted of 80 delivered.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — receiving and stock increase rules.

## Rejected Quantity

**Approved definition:** The quantity presented at receipt that is refused and therefore not taken into usable stock.

**Business meaning:** Records supply quality problems and supports supplier performance measurement and returns.

**What it is not:** Not Damaged Quantity held in stock — rejected goods are not accepted into usable stock at all. Not a Supplier Return, which returns goods that were previously accepted.

**Owning module:** Inventory.

**Related entities or documents:** Goods Receipt Line, Supplier Return, Damaged Quantity, Supplier.

**Example:** 2 of 80 units refused at the door for visible damage.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — receiving requirements. Treatment of rejected goods requires confirmation in MESP-33.

## Outstanding Quantity

**Approved definition:** The quantity of a Purchase Order line that has been ordered but not yet received.

**Business meaning:** The open supply position — what the business is still waiting for and what remains committed to the supplier.

**What it is not:** Not Expected Quantity in the stock sense, and not stock. Not a backorder to a customer.

**Owning module:** Procurement.

**Related entities or documents:** Purchase Order, Goods Receipt, Partially Completed, Expected Quantity.

**Example:** 100 ordered, 80 received, 20 outstanding.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — procurement tracking requirements.

## Purchase Invoice

**Approved definition:** The supplier's financial claim recorded against the Company for goods or services supplied.

**Business meaning:** **Creates a supplier liability in Accounts Payable.** It is the basis for payment and for input tax recording.

**What it is not:** **A Purchase Invoice does not independently increase stock.** Not a Goods Receipt. **Not a Supplier Payment** — the invoice creates the obligation, the payment settles it. See clarifications 9 and 10.

**Owning module:** Finance and Accounting, executed within the Procurement lifecycle.

**Related entities or documents:** Supplier, Purchase Order, Goods Receipt, Three-Way Match, Accounts Payable, Supplier Payment, Payment Terms.

**Example:** An invoice for 80 received units creating a payable balance due in 30 days.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — a Purchase Invoice creates a supplier liability but does not independently increase stock.

## Supplier Credit Note

**Approved definition:** A document that reduces an amount owed to a Supplier, issued in respect of returns, overcharges, or agreed adjustments.

**Business meaning:** The controlled way to correct an overstated payable without editing a posted invoice.

**What it is not:** Not a Supplier Payment. Not a reversal of a goods receipt. Not a stock movement by itself — the related Supplier Return moves the stock.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Purchase Invoice, Supplier Return, Accounts Payable, Reversal, Allocation.

**Example:** A credit note for 2 returned units reducing the payable balance.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — posted documents are corrected by reversal, return, credit note, debit note, or adjustment.

## Supplier Return

**Approved definition:** The document recording that previously accepted goods are sent back to the Supplier, reducing stock.

**Business meaning:** Removes goods from stock when they are returned, and provides the basis for a supplier credit note.

**What it is not:** Not a Rejected Quantity at receipt — those goods were never accepted into stock. Not a Stock Adjustment. Not a credit note, although it normally triggers one.

**Owning module:** Inventory.

**Related entities or documents:** Goods Receipt, Supplier Credit Note, Stock Movement, Stock Ledger, Warehouse.

**Example:** Returning 5 defective units discovered a week after acceptance, reducing on-hand stock by 5.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved inventory lifecycle including Supplier Return.

## Three-Way Match

**Approved definition:** The control that compares the Purchase Order, the Goods Receipt, and the Purchase Invoice before a purchase invoice is approved for payment.

**Business meaning:** Prevents paying for goods that were never ordered, never received, or priced differently from the agreed order.

**What it is not:** Not an Approval workflow by itself. Not a payment. Not a stock movement. A successful match does not automatically release payment.

**Owning module:** Procurement.

**Related entities or documents:** Purchase Order, Goods Receipt, Purchase Invoice, Matching Tolerance, Approval, Separation of Duties.

**Example:** Ordered 100 at a fixed price, received 80, invoiced 80 at the same price — the match succeeds on quantity and price.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — matching requirement. Tolerances and exception handling are open in **MESP-44 — Confirm purchase-order, receipt, and invoice matching tolerances**.

## Matching Tolerance

**Approved definition:** The permitted variance in quantity, price, or value between the Purchase Order, Goods Receipt, and Purchase Invoice within which a match is treated as successful.

**Business meaning:** Avoids blocking payment for trivial differences while still catching material discrepancies.

**What it is not:** Not an approval limit. Not a rounding rule. Not a discount.

**Owning module:** Procurement.

**Related entities or documents:** Three-Way Match, Purchase Invoice, Approval.

**Example:** A small percentage price variance treated as within tolerance while a quantity variance is not.

**Approval status:** Requires Business Decision

**Source:** Open in **MESP-44**. No tolerance value is defined in PRD v1.2 and none is assumed here.

## Supplier Payment

**Approved definition:** The document recording the settlement of amounts owed to a Supplier, reducing Accounts Payable.

**Business meaning:** The final step of the approved Purchase-to-Pay lifecycle. Funds leave the Company and the payable is settled.

**What it is not:** **Not a Purchase Invoice.** The invoice creates the obligation; the payment discharges it. Not a stock movement. Not an approval of the invoice.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Purchase Invoice, Accounts Payable, Allocation, Settlement, Cash Account, Bank Account.

**Example:** A bank transfer settling two supplier invoices, allocated across both.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — approved lifecycle. Supported payment and receipt methods are open in **MESP-47 — Confirm supported payment and receipt methods**.

---

# 6. Inventory

## Inventory

**Approved definition:** The business function accountable for holding, moving, counting, and valuing stock across Warehouses.

**Business meaning:** Owns the physical truth of what the Company holds and the financial value attached to it.

**What it is not:** Not the product catalog. Not procurement. Not a warehouse management system with bin, wave, and labour optimisation — that is not Release 1 scope.

**Owning module:** Inventory.

**Related entities or documents:** Stock, Stock Ledger, Warehouse, Inventory Valuation.

**Example:** The inventory function reconciles counted stock to the stock ledger at period end.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — inventory module scope.

## Stock

**Approved definition:** The quantity of an Item physically held in a Warehouse at a point in time, together with its value.

**Business meaning:** What the Company actually has and can use, sell, or transfer.

**What it is not:** **Not a Product.** A product is a catalog definition; stock is a held quantity of it. Not an order. Not an expectation. See clarification 7.

**Owning module:** Inventory.

**Related entities or documents:** Item, Warehouse, Stock Balance, Stock Movement, On-Hand Quantity.

**Example:** 400 units of an item held in the Riyadh main warehouse.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — inventory requirements.

## Stock Ledger

**Approved definition:** The complete, immutable, chronological record of every Stock Movement, with quantity and value effects.

**Business meaning:** The auditable history that explains how stock reached its current position and value. It is the source of truth for inventory reconciliation.

**What it is not:** **Not a Stock Balance.** The ledger is the history of movements; the balance is the current position derived from it. The ledger is never edited or deleted — corrections are new entries created by reversal, return, or adjustment. See clarification 14.

**Owning module:** Inventory.

**Related entities or documents:** Stock Movement, Stock Balance, Immutable Record, Moving Weighted Average, Subledger, Reconciliation.

**Example:** A ledger showing a receipt of 100, an issue of 60, and an adjustment of minus 2, explaining a balance of 38.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — immutable stock ledger requirement; approved decision list (MESP-16) — posted documents are corrected by reversal, return, credit note, debit note, or adjustment.

## Stock Movement

**Approved definition:** A single recorded change in stock quantity and value for one Item in one Warehouse, created by a posted inventory document.

**Business meaning:** The atomic event of inventory. Every increase or decrease in stock is one or more stock movements traceable to its source document.

**What it is not:** Not a document — a document may create many movements. Not a Purchase Order, which creates none. Not a plan or forecast.

**Owning module:** Inventory.

**Related entities or documents:** Stock Ledger, Goods Receipt, Warehouse Transfer, Stock Adjustment, Stock Issue, Source Document.

**Example:** A goods receipt of one item into one warehouse creating a single positive movement of 80 units.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — stock movement requirements.

## Stock Balance

**Approved definition:** The current quantity and value of an Item in a Warehouse, derived from the Stock Ledger.

**Business meaning:** The answer to "how much do we have right now and what is it worth".

**What it is not:** Not the Stock Ledger. Not an independently maintained number that can be edited directly — it must always be explainable by the ledger. See clarification 14.

**Owning module:** Inventory.

**Related entities or documents:** Stock Ledger, On-Hand Quantity, Available Quantity, Inventory Valuation, Warehouse.

**Example:** A balance of 38 units valued at the current moving weighted average cost.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — inventory balance and valuation requirements.

## On-Hand Quantity

**Approved definition:** The quantity of an Item physically present in a Warehouse, regardless of whether it is reserved.

**Business meaning:** What is actually in the building.

**What it is not:** Not Available Quantity, which excludes reservations. Not Expected Quantity, which has not arrived. Not In-Transit Quantity.

**Owning module:** Inventory.

**Related entities or documents:** Stock Balance, Reserved Quantity, Available Quantity.

**Example:** 100 on hand, of which 30 are reserved, leaving 70 available.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — inventory quantity requirements.

## Reserved Quantity

**Approved definition:** The portion of On-Hand Quantity committed to a specific demand, typically a confirmed Sales Order, and therefore not available to other demand.

**Business meaning:** Prevents the same physical units from being promised twice.

**What it is not:** Not a stock decrease — reserved stock is still on hand until it is issued or delivered. Not a Delivery. Not a Stock Movement.

**Owning module:** Inventory.

**Related entities or documents:** Stock Reservation, Sales Order, On-Hand Quantity, Available Quantity, Delivery.

**Example:** 30 units reserved against a confirmed sales order and excluded from availability for a second customer.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — reservation requirement. Reservation trigger points interact with **MESP-45 — Confirm negative-stock policy** and **MESP-46**; rules require confirmation in MESP-33 and MESP-35.

## Available Quantity

**Approved definition:** On-Hand Quantity minus Reserved Quantity, expressing what can still be promised to new demand.

**Business meaning:** The number sales should look at before promising delivery.

**What it is not:** Not On-Hand Quantity. Not a forecast. Does not include Expected Quantity unless a forward-availability rule is separately approved.

**Owning module:** Inventory.

**Related entities or documents:** On-Hand Quantity, Reserved Quantity, Sales Order, Stock Reservation.

**Example:** 100 on hand minus 30 reserved gives 70 available.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — availability requirement. Whether expected stock counts toward availability requires confirmation in MESP-33.

## Expected Quantity

**Approved definition:** The quantity of an Item that is committed to arrive but has not yet been received into stock.

**Business meaning:** Forward visibility of incoming supply, based on confirmed purchase orders.

**What it is not:** **Not stock.** It is not on hand, not available for issue, and carries no inventory value. Not an Outstanding Quantity in the commercial sense, although the two are closely related.

**Owning module:** Inventory.

**Related entities or documents:** Purchase Order, Supplier Confirmation, Outstanding Quantity, Goods Receipt.

**Example:** 20 units expected from a confirmed purchase order, with zero of them counted in stock or valuation.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — supply visibility requirement. Requires confirmation in MESP-33.

## Damaged Quantity

**Approved definition:** Stock that is on hand but recorded as not usable for normal sale or issue because of damage or quality failure.

**Business meaning:** Keeps unusable stock visible and controlled instead of quietly disappearing or being sold by mistake.

**What it is not:** Not Rejected Quantity — rejected goods were never accepted into stock. Not automatically written off; a Stock Adjustment is required to remove it.

**Owning module:** Inventory.

**Related entities or documents:** Stock Adjustment, Available Quantity, Supplier Return, Warehouse.

**Example:** 3 units marked damaged and excluded from available quantity pending a write-off decision.

**Approval status:** Draft for BRD Validation

**Source:** Standard ERP practice. Stock status handling requires confirmation in MESP-33.

## In-Transit Quantity

**Approved definition:** Stock that has left a source Warehouse on a Transfer Shipment but has not yet been received at the destination Warehouse.

**Business meaning:** Makes stock that is between locations visible and accountable, so it is neither double counted nor lost.

**What it is not:** Not on hand at either warehouse. Not Expected Quantity from a supplier. Not a loss.

**Owning module:** Inventory.

**Related entities or documents:** Warehouse Transfer, Transfer Shipment, Transfer Receipt, Stock Movement.

**Example:** 50 units shipped from Riyadh to Jeddah, in transit until the transfer receipt is posted.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — transfer requirements. Whether transfers are one-step or two-step requires confirmation in MESP-33.

## Opening Balance

**Approved definition:** The initial stock quantity and value loaded for an Item in a Warehouse when the system starts being used, or at the start of a fiscal period.

**Business meaning:** The first step of the approved inventory lifecycle and the starting point of the stock ledger for migrated tenants.

**What it is not:** Not a Goods Receipt from a supplier. Not a Stock Adjustment. Not a purchase — no payable is created.

**Owning module:** Inventory, prepared with Migration and Onboarding.

**Related entities or documents:** Stock Ledger, Inventory Valuation, Migration, Journal Entry.

**Example:** Loading counted stock and its agreed value on the go-live date as the opening balance.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — approved inventory lifecycle. Migration sources and opening-balance requirements are open in **MESP-51 — Confirm Wafra migration sources and opening-balance requirements**.

## Warehouse Transfer

**Approved definition:** The movement of stock from one Warehouse to another within the same Company.

**Business meaning:** Redistributes stock without changing ownership, total company stock, or creating a purchase or sale.

**What it is not:** Not a sale. Not a purchase. Not a Stock Adjustment. Total quantity across warehouses is unchanged, apart from any recorded transfer loss.

**Owning module:** Inventory.

**Related entities or documents:** Transfer Shipment, Transfer Receipt, In-Transit Quantity, Stock Movement, Warehouse.

**Example:** Moving 50 units from the Riyadh main warehouse to the Jeddah warehouse.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved inventory lifecycle including Warehouse Transfer.

## Transfer Shipment

**Approved definition:** The document recording that stock has left the source Warehouse as part of a Warehouse Transfer.

**Business meaning:** Removes stock from the source location and places it in transit, making accountability explicit while goods are moving.

**What it is not:** Not a Delivery to a customer. Not a receipt at destination. Not a stock write-off.

**Owning module:** Inventory.

**Related entities or documents:** Warehouse Transfer, Transfer Receipt, In-Transit Quantity, Stock Movement.

**Example:** Shipping 50 units out of Riyadh, reducing Riyadh on-hand by 50.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — transfer requirements. One-step versus two-step transfer requires confirmation in MESP-33.

## Transfer Receipt

**Approved definition:** The document recording that transferred stock has arrived and been accepted at the destination Warehouse.

**Business meaning:** Completes the transfer, clears the in-transit position, and increases destination stock.

**What it is not:** Not a Goods Receipt from a supplier — no purchase order, supplier, or payable is involved. Not a Customer Return.

**Owning module:** Inventory.

**Related entities or documents:** Warehouse Transfer, Transfer Shipment, In-Transit Quantity, Stock Movement.

**Example:** Receiving 48 of 50 transferred units, leaving a 2-unit variance to investigate.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — transfer requirements. Variance handling requires confirmation in MESP-33.

## Stock Adjustment

**Approved definition:** An authorised correction of stock quantity or value that is not the result of a purchase, sale, transfer, or return.

**Business meaning:** The controlled route for write-offs, write-ons, damage, loss, and count corrections, always with a reason and an approver.

**What it is not:** Not an edit of history — it is a new, forward-dated movement. Not a Warehouse Transfer. Not a substitute for a Supplier Return or Customer Return.

**Owning module:** Inventory.

**Related entities or documents:** Stock Movement, Stock Ledger, Inventory Count, Count Variance, Approval, Journal Entry.

**Example:** Writing off 3 damaged units with a stated reason and an approval.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved inventory lifecycle; approved decision list (MESP-16) — posted documents are corrected by reversal, return, credit note, debit note, or adjustment.

## Inventory Count

**Approved definition:** The controlled process of physically counting stock in a Warehouse and comparing the counted quantity to the recorded Stock Balance.

**Business meaning:** Verifies that the system reflects physical reality, and produces the variance that drives corrective adjustments.

**What it is not:** Not a Stock Adjustment itself — the count produces a variance, and a separate authorised adjustment corrects the balance. Not a valuation exercise.

**Owning module:** Inventory.

**Related entities or documents:** Count Variance, Stock Adjustment, Stock Balance, Warehouse, Approval.

**Example:** Counting 396 units against a recorded balance of 400, producing a variance of minus 4.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved inventory lifecycle including Inventory Count.

## Count Variance

**Approved definition:** The difference between the counted quantity and the recorded Stock Balance for an Item in a Warehouse at the time of a count.

**Business meaning:** Measures inventory accuracy and quantifies the correction that requires approval.

**What it is not:** Not automatically a loss — it may be positive. Not posted until an authorised Stock Adjustment is made.

**Owning module:** Inventory.

**Related entities or documents:** Inventory Count, Stock Adjustment, Approval, Audit Event.

**Example:** A variance of minus 4 units investigated and then adjusted with approval.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — count requirements. Variance thresholds and approval levels require confirmation in MESP-33.

## Batch

**Approved definition:** A quantity of an Item produced or received together and tracked as one identifiable group.

**Business meaning:** Enables traceability and recall for items where the group matters.

**What it is not:** Not a Serial Number, which identifies a single unit. Not a Lot unless the two are formally treated as one concept. Not a delivery.

**Owning module:** Inventory.

**Related entities or documents:** Lot, Expiry Date, Goods Receipt Line, Stock Movement, Serial Number.

**Example:** A received batch tracked so that all units from it can be identified later.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 section 13.2 records batch, lot, serial, and expiry tracking as an open clarification. Open in **MESP-41 — Confirm batch, lot, serial, and expiry tracking scope**. Whether batch tracking is in Release 1 is not decided.

## Lot

**Approved definition:** An identifiable grouping of stock of the same Item, used interchangeably with Batch unless the business defines a distinction.

**Business meaning:** Traceability grouping, commonly aligned to a supplier delivery or production run.

**What it is not:** Not a Serial Number. Not a warehouse. Whether Lot and Batch are one concept or two is not decided.

**Owning module:** Inventory.

**Related entities or documents:** Batch, Expiry Date, Stock Movement.

**Example:** A supplier lot reference recorded at receipt for traceability.

**Approval status:** Requires Business Decision

**Source:** Open in **MESP-41**. No distinction between Lot and Batch is assumed here.

## Serial Number

**Approved definition:** A unique identifier assigned to a single physical unit of an Item, tracked individually through its movements.

**Business meaning:** Enables unit-level traceability, warranty handling, and service history for high-value items.

**What it is not:** Not a Batch or Lot, which cover groups. Not a Barcode. Not an SKU.

**Owning module:** Inventory.

**Related entities or documents:** Batch, Item, Stock Movement, Delivery, Customer Return.

**Example:** A single serialised unit traced from receipt to a specific customer delivery.

**Approval status:** Requires Business Decision

**Source:** Open in **MESP-41**. Serial tracking is not confirmed as Release 1 scope.

## Expiry Date

**Approved definition:** The date after which a Batch or Lot of an Item may no longer be sold or issued.

**Business meaning:** Protects against selling expired goods and drives near-expiry reporting and stock rotation.

**What it is not:** Not a warranty date. Not a document date. Not applicable to every item.

**Owning module:** Inventory.

**Related entities or documents:** Batch, Lot, Stock Balance, Delivery, reporting.

**Example:** Blocking issue of a batch whose expiry date has passed.

**Approval status:** Requires Business Decision

**Source:** Open in **MESP-41**. Expiry handling is not confirmed as Release 1 scope.

## Moving Weighted Average

**Approved definition:** The Release 1 inventory valuation method, in which an Item's unit cost is recalculated as a weighted average of existing stock value and newly received stock value each time stock is received.

**Business meaning:** Produces one blended cost per item per valuation scope, used to value issues, deliveries, and closing stock.

**What it is not:** Not FIFO, not LIFO, and not standard costing — these are not Release 1 methods. Not a sales price. Not a supplier price list.

**Owning module:** Inventory.

**Related entities or documents:** Inventory Valuation, Stock Ledger, Goods Receipt, Stock Issue, Journal Entry.

**Example:** Existing stock at one average cost plus a new receipt at a higher cost produces a new blended average applied to subsequent issues.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — Moving Weighted Average is the Release 1 inventory valuation method. The valuation scope, for example company-wide or per warehouse, requires confirmation in MESP-33.

## Inventory Valuation

**Approved definition:** The monetary value assigned to stock, calculated using the approved valuation method and reflected in the Stock Ledger and the General Ledger.

**Business meaning:** Determines the balance sheet value of inventory and the cost recognised when stock is issued or sold.

**What it is not:** Not a sales price. Not a market value. Not an independent figure — it must reconcile between the inventory subledger and the general ledger.

**Owning module:** Finance and Accounting, calculated by Inventory.

**Related entities or documents:** Moving Weighted Average, Stock Ledger, Subledger, General Ledger, Reconciliation.

**Example:** Closing inventory value reconciled to the inventory control account at period end.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — inventory valuation and subledger-to-general-ledger reconciliation requirements.

---

# 7. B2B Sales

## Quotation

**Approved definition:** A priced offer issued to a Business Customer for specified items, quantities, and terms, valid for a stated period.

**Business meaning:** The first step of the approved Order-to-Cash lifecycle. It communicates price and terms without committing stock or revenue.

**What it is not:** Not a Sales Order. Not a commitment by the customer. Creates no stock reservation, no delivery obligation, and no receivable.

**Owning module:** B2B Sales.

**Related entities or documents:** Business Customer, Price List, Sales Order, Transaction Currency.

**Example:** A quotation valid for 14 days that converts to a sales order once the customer accepts it.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved B2B Sales lifecycle: Quotation to Sales Order to Delivery to Sales Invoice to Customer Receipt.

## Sales Order

**Approved definition:** A confirmed commitment to supply a Business Customer with specified items, quantities, prices, and terms.

**Business meaning:** The commercial agreement to sell. It drives fulfilment, may reserve stock, and consumes credit exposure where credit control applies.

**What it is not:** **Not a Delivery** — the order commits, the delivery moves the goods. It does not reduce stock by itself and creates no receivable. Not a Sales Invoice. See clarification 11.

**Owning module:** B2B Sales.

**Related entities or documents:** Business Customer, Quotation, Stock Reservation, Delivery, Sales Invoice, Credit Limit, Credit Exposure.

**Example:** An order for 30 units that reserves stock but leaves on-hand quantity unchanged until delivery.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved B2B Sales lifecycle.

## Stock Reservation

**Approved definition:** The act of committing on-hand stock to a specific Sales Order so that it cannot be promised to other demand.

**Business meaning:** Protects a customer promise without moving goods.

**What it is not:** Not a stock decrease. Not a Delivery. Not a guarantee of a future receipt.

**Owning module:** Inventory, triggered by B2B Sales.

**Related entities or documents:** Sales Order, Reserved Quantity, Available Quantity, Delivery.

**Example:** Reserving 30 units on order confirmation, reducing available quantity but not on-hand quantity.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — reservation requirement. Trigger point, expiry, and interaction with negative stock are open in **MESP-45** and require confirmation in MESP-35.

## Delivery

**Approved definition:** The document recording that goods have physically left a Warehouse and been dispatched to a Business Customer.

**Business meaning:** **The event that reduces stock on a sale.** It also releases the related reservation and creates the basis for invoicing.

**What it is not:** **Not a Sales Order** — the order commits, the delivery ships. **Not a Sales Invoice** — the delivery moves goods, the invoice creates the receivable. Not a Customer Receipt. See clarifications 11 and 12.

**Owning module:** Inventory, executed within the B2B Sales lifecycle.

**Related entities or documents:** Sales Order, Delivery Note, Stock Movement, Sales Invoice, Warehouse.

**Example:** Delivering 30 of 30 ordered units, reducing on-hand stock by 30 and clearing the reservation.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved B2B Sales lifecycle.

## Delivery Note

**Approved definition:** The printed or electronic document accompanying a Delivery that lists the items and quantities dispatched.

**Business meaning:** Evidence handed to the customer and the basis for proof of delivery.

**What it is not:** Not a Sales Invoice and not a demand for payment. Not a tax document.

**Owning module:** B2B Sales.

**Related entities or documents:** Delivery, Sales Order, Business Customer.

**Example:** A signed delivery note returned as proof of receipt by the customer.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — sales documentation. Content, bilingual layout, and signature requirements require confirmation in MESP-35 and MESP-37.

## Sales Invoice

**Approved definition:** The Company's financial claim on a Business Customer for goods or services supplied, including applicable tax.

**Business meaning:** **Creates a receivable in Accounts Receivable** and recognises revenue and output tax. In Saudi Arabia it is also the statutory tax document.

**What it is not:** **Not a Delivery** — an invoice does not move goods. **Not a Customer Receipt** — the invoice creates the claim, the receipt settles it. Not a Quotation. See clarifications 12 and 13.

**Owning module:** Finance and Accounting, executed within the B2B Sales lifecycle.

**Related entities or documents:** Business Customer, Sales Order, Delivery, Accounts Receivable, Customer Receipt, Tax Category, Country Pack.

**Example:** An invoice for delivered goods creating a receivable due under the customer's payment terms.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — approved lifecycle and Saudi tax invoice requirements. E-invoicing obligations at launch are open in **MESP-49**.

## Customer Receipt

**Approved definition:** The document recording money received from a Business Customer, reducing Accounts Receivable.

**Business meaning:** The final step of the approved Order-to-Cash lifecycle. Cash or bank funds enter the Company and the receivable is settled.

**What it is not:** **Not a Sales Invoice.** The invoice creates the claim; the receipt discharges it. Not a stock movement. Not revenue recognition.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Sales Invoice, Accounts Receivable, Allocation, Settlement, Cash Account, Bank Account, Credit Exposure.

**Example:** A bank transfer received and allocated across two outstanding customer invoices.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — approved lifecycle. Supported receipt methods are open in **MESP-47**.

## Customer Return

**Approved definition:** The document recording that goods previously delivered to a Business Customer are returned and taken back into stock.

**Business meaning:** Increases stock again and provides the basis for a credit note to the customer.

**What it is not:** Not a Credit Note — the return moves goods, the credit note adjusts money. Not a Supplier Return. Not a Stock Adjustment.

**Owning module:** Inventory, triggered by B2B Sales.

**Related entities or documents:** Delivery, Credit Note, Stock Movement, Stock Ledger, Warehouse.

**Example:** Taking back 5 units, increasing on-hand stock by 5 and triggering a credit note.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approved inventory lifecycle including Customer Return.

## Credit Note

**Approved definition:** A document that reduces an amount owed by a Business Customer, issued for returns, overcharges, or agreed adjustments.

**Business meaning:** The controlled way to correct an overstated receivable without editing a posted invoice.

**What it is not:** Not a Customer Receipt — no money is received. Not a stock movement by itself. Not a discount applied to a future order.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Sales Invoice, Customer Return, Accounts Receivable, Reversal, Allocation.

**Example:** A credit note for 5 returned units reducing the customer's outstanding balance.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — posted documents are corrected by reversal, return, credit note, debit note, or adjustment.

## Accounts Receivable

**Approved definition:** The subledger recording amounts owed to the Company by Business Customers, by customer and by document, reconciled to the receivables control account in the General Ledger.

**Business meaning:** The Company's collection position — who owes what, since when, and when it is due.

**What it is not:** Not revenue. Not cash. Not Credit Exposure, which may include commitments beyond invoiced amounts.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Sales Invoice, Customer Receipt, Credit Note, Allocation, Subledger, Reconciliation, Payment Terms.

**Example:** A customer balance of several open invoices ageing across 30 and 60 day buckets.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — finance and subledger requirements.

## Credit Exposure

**Approved definition:** The total current risk position for a Business Customer, measured against the Credit Limit.

**Business meaning:** Tells the business how much of the customer's approved credit is already used before accepting a new order.

**What it is not:** Not the Credit Limit, which is the ceiling. Not necessarily equal to Accounts Receivable — whether open orders and undelivered items count toward exposure is not decided.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Credit Limit, Accounts Receivable, Sales Order, Sales Invoice, Approval.

**Example:** Invoiced but unpaid amounts plus, if approved, confirmed but uninvoiced orders.

**Approval status:** Requires Business Decision

**Source:** Open in **MESP-46 — Confirm B2B customer credit-control policy**. The components of exposure are not defined in PRD v1.2 and none is assumed here.

## Retail POS

**Approved definition:** Point-of-sale retail selling to walk-in consumers, including cashier operations, cash drawers, retail shifts, and retail checkout.

**Business meaning:** A distinct retail sales channel with different actors, controls, and cash handling from B2B trading.

**What it is not:** **Retail POS is not part of Release 1.** It is explicitly excluded. **Retail POS must not be used as a synonym for B2B Sales.** A Sales Order, Delivery, Sales Invoice, and Customer Receipt in this product are B2B documents raised against a named Business Customer; they are not retail checkout transactions. No cashier, cash drawer, shift-management, or retail checkout concept exists in Release 1. See clarification 19.

**Owning module:** Not assigned. Out of Release 1 scope.

**Related entities or documents:** None in Release 1. If POS is approved as a future module, it would consume the shared Sales, Inventory, Finance, Tax, and Payment capabilities rather than duplicating them.

**Example:** No Release 1 example exists. Any requirement describing a cashier, a cash drawer, a retail shift, or an anonymous walk-in consumer is out of scope and must be raised as future scope, not built into a Release 1 BRD.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — Retail POS is excluded from Release 1, and Retail POS, cashier operations, cash drawers, retail shifts, and retail checkout are future scope. Recorded as an explicit exclusion on MESP-9 and MESP-35.

---

# 8. Finance

*Accounts Receivable is owned by Finance and Accounting and is defined in section 7, next to the sales documents that create it.*

## Chart of Accounts

**Approved definition:** The structured list of general ledger accounts of a Company, used to classify every financial posting.

**Business meaning:** The backbone of financial reporting. Every posting lands in exactly one account per line.

**What it is not:** Not a Cost Center. Not a Category. Not shared across Companies unless a shared structure is explicitly approved — each Company owns its own accounting books.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Company, General Ledger, Journal Entry, Posting Rule.

**Example:** Separate accounts for inventory, accounts payable, accounts receivable, VAT input, and VAT output.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — finance foundation requirements.

## General Ledger

**Approved definition:** The complete record of all financial postings of a Company, organised by account and period, from which the financial statements are produced.

**Business meaning:** The single financial truth of the Company.

**What it is not:** Not a Subledger — subledgers hold the counterparty and document detail behind control accounts. Not the Stock Ledger, which records quantities and values of goods.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Chart of Accounts, Journal Entry, Subledger, Reconciliation, Fiscal Period.

**Example:** A trial balance drawn from the general ledger at period end.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — general ledger requirements.

## Subledger

**Approved definition:** A detailed ledger holding transaction-level records behind a General Ledger control account — principally Accounts Payable, Accounts Receivable, and Inventory.

**Business meaning:** Answers "which supplier, which customer, which item, which document" behind a single control account balance.

**What it is not:** Not the General Ledger. Not an independent source of truth — it must always reconcile to its control account.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Accounts Payable, Accounts Receivable, Stock Ledger, Reconciliation, General Ledger.

**Example:** The payables subledger listing each open supplier invoice that adds up to the payables control account balance.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — subledger-to-general-ledger reconciliation requirement.

## Journal

**Approved definition:** A defined grouping of financial postings of the same type, used to organise and control how entries reach the General Ledger.

**Business meaning:** Separates purchases, sales, cash, inventory, and manual corrections so that origin and control differ by type.

**What it is not:** Not a Journal Entry, which is a single transaction within a journal. Not an account.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Journal Entry, General Ledger, Posting Rule.

**Example:** A purchases journal, a sales journal, and a manual journal with tighter approval control.

**Approval status:** Approved Product Baseline

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §§6, 8.4 and 11 define Journal as a controlled grouping of balanced postings; exact catalogue and approval policy remain FIN-OD-01 where applicable.

## Journal Entry

**Approved definition:** A balanced set of debit and credit lines recording one financial transaction, carrying a date, a fiscal period, a currency, and a reference to its source document.

**Business meaning:** The unit of accounting record. Total debits always equal total credits.

**What it is not:** Not a business document such as an invoice — the invoice is the business event, the journal entry is its accounting effect. Not editable after posting; corrections are made by Reversal.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Journal, General Ledger, Posting, Reversal, Source Document, Fiscal Period.

**Example:** A purchase invoice posting that debits inventory or expense and credits accounts payable.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — posting and accounting requirements.

## Debit

**Approved definition:** The left side of a Journal Entry line, increasing assets and expenses and decreasing liabilities, equity, and income.

**Business meaning:** One half of double-entry bookkeeping.

**What it is not:** Not "money out" in plain language. Not a debit note. Not a payment.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Credit, Journal Entry, Chart of Accounts.

**Example:** Debiting inventory when goods are received into stock.

**Approval status:** Approved Product Baseline

**Source:** Standard accounting principle applied by PRD v1.2 posting requirements.

## Credit

**Approved definition:** The right side of a Journal Entry line, increasing liabilities, equity, and income and decreasing assets and expenses.

**Business meaning:** The other half of double-entry bookkeeping.

**What it is not:** Not a Credit Note. Not a Credit Limit. Not "money in" in plain language. The word must never be used loosely in a BRD — state which of the three meanings applies.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Debit, Journal Entry, Credit Note, Credit Limit.

**Example:** Crediting accounts payable when a purchase invoice is posted.

**Approval status:** Approved Product Baseline

**Source:** Standard accounting principle applied by PRD v1.2 posting requirements.

## Posting

**Approved definition:** The act of committing a business document's financial and inventory effects to the ledgers, after which the record becomes immutable.

**Business meaning:** The point at which the transaction becomes part of the official books and the stock position.

**What it is not:** **Not Approval.** Approval is a business authorisation; posting is the ledger effect. An approved document is not necessarily posted, and posting is not a second approval. Not reversible by editing — only by Reversal. See clarification 15.

**Owning module:** Finance and Accounting, applied by every transactional module.

**Related entities or documents:** Journal Entry, Stock Movement, Posted, Reversal, Fiscal Period, Immutable Record.

**Example:** Posting a goods receipt, which creates both a stock movement and the corresponding accounting entry.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — posting and immutability requirements; approved decision list (MESP-16).

## Posting Rule

**Approved definition:** The configured logic that determines which accounts a business document posts to, based on document type, item category, warehouse, tax treatment, and company configuration.

**Business meaning:** Keeps accounting consistent and removes the need for users to choose accounts manually.

**What it is not:** Not a Journal Entry. Not a Permission. Not hard-coded logic that a business owner cannot see or govern.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Chart of Accounts, Journal Entry, Category, Tax Category, Warehouse.

**Example:** A rule directing goods receipts of a given item category to a specific inventory account.

**Approval status:** Approved Product Baseline

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §8.3 defines the Finance-owned Posting Rule concept and visible versioned catalogue. Exact account, dimension, valuation, and exception policy remains FIN-OD-01 / FIN-OD-09 / MESP-110.

## Reversal

**Approved definition:** The creation of an equal and opposite posting that cancels the effect of a previously posted document, leaving both the original and the reversal visible.

**Business meaning:** The only permitted way to undo a posted transaction. History is never erased.

**What it is not:** **Not a Cancellation.** Cancellation applies to a document that was never posted; reversal applies to one that was. Not a deletion. Not an edit. See clarification 16.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Posting, Journal Entry, Stock Movement, Cancelled, Immutable Record, Audit Event.

**Example:** Reversing an incorrectly posted purchase invoice and posting a corrected one, with all three records retained.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — posted documents are corrected by reversal, return, credit note, debit note, or adjustment.

## Accounts Payable

**Approved definition:** The subledger recording amounts owed by the Company to Suppliers, by supplier and by document, reconciled to the payables control account in the General Ledger.

**Business meaning:** The Company's obligation position — who is owed what, since when, and when it is due.

**What it is not:** Not an expense. Not cash. Not a Purchase Order, which creates a commitment but no liability.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Purchase Invoice, Supplier Payment, Supplier Credit Note, Allocation, Subledger, Reconciliation, Payment Terms.

**Example:** A supplier balance made up of three open invoices, two of them due this month.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — payables and subledger requirements.

## Cash Account

**Approved definition:** A general ledger account representing physical cash held by the Company.

**Business meaning:** Tracks cash on hand used for receipts and payments.

**What it is not:** Not a Bank Account. Not a cash drawer or till — cash drawers belong to Retail POS, which is excluded from Release 1.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Bank Account, Supplier Payment, Customer Receipt, Journal Entry.

**Example:** A petty cash account used for small payments.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — payment handling. Which payment and receipt instruments are supported is open in **MESP-47**.

## Bank Account

**Approved definition:** A general ledger account representing an account held by the Company at a financial institution, with its own identifiers and currency.

**Business meaning:** The route through which most supplier payments and customer receipts flow, and the basis for bank reconciliation.

**What it is not:** Not a Cash Account. Not a payment gateway integration. Not a customer's bank details.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Cash Account, Supplier Payment, Customer Receipt, Reconciliation, Base Currency.

**Example:** A SAR operating account used to pay suppliers.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — payment handling. Bank integration and statement reconciliation scope are open in **MESP-47** and **MESP-53**.

## Allocation

**Approved definition:** The act of matching a payment or receipt, in whole or in part, against one or more specific invoices or credit notes.

**Business meaning:** Turns "money moved" into "this invoice is settled", which is what ageing and collection depend on.

**What it is not:** Not the payment itself. Not Settlement, which is the resulting state. An unallocated payment is real money that has not yet been matched to a document.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Supplier Payment, Customer Receipt, Purchase Invoice, Sales Invoice, Credit Note, Settlement.

**Example:** One receipt allocated across two invoices, partly settling the second.

**Approval status:** Approved Product Baseline

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §§6, 9.6 and 15 define whole/partial matching and reconciliation evidence. Exact payment/receipt policy remains MESP-47 and applicable Finance decisions.

## Settlement

**Approved definition:** The state in which an invoice or document is fully discharged by allocated payments, receipts, or credit notes.

**Business meaning:** Tells the business that nothing further is owed on that document.

**What it is not:** Not Allocation, which is the act. Not Reconciliation, which compares two records. Not document closure in the workflow sense.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Allocation, Accounts Payable, Accounts Receivable, Closed.

**Example:** An invoice moving to fully settled once the final partial payment is allocated.

**Approval status:** Approved Product Baseline

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §§6, 9.4, 9.6 and 15 define Settlement as the resulting fully discharged state under approved allocation rules. Payment methods and settlement policy remain MESP-47 and applicable Finance decisions.

## Reconciliation

**Approved definition:** The controlled comparison of two independent records to confirm they agree, and the investigation of any difference.

**Business meaning:** Proves that the books are trustworthy — subledger to control account, stock ledger to inventory account, bank statement to bank account, counted stock to recorded stock.

**What it is not:** Not Allocation. Not Settlement. Not a report alone — it produces an accountable outcome and an owner for any difference.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Subledger, General Ledger, Stock Ledger, Bank Account, Inventory Count.

**Example:** Reconciling the inventory subledger value to the inventory control account at month end.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — reconciliation requirement. Reconciliation ownership and report catalogue are open in **MESP-53 — Confirm report catalogue and reconciliation ownership**.

## Currency

**Approved definition:** A recognized unit of monetary value maintained as Tenant master data.

**Business meaning:** The reusable master-data fact that a currency exists and is available for use; distinct from the *usage role* a currency plays on a given document or ledger.

**What it is not:** Not Base Currency, Transaction Currency, or Reporting Currency, which are the specific usage roles the glossary defines below for a given document or ledger context. Not limited to SAR — Release 1 provides genuine multi-currency capability.

**Owning module:** Master Data and Catalog.

**Related entities or documents:** Base Currency, Transaction Currency, Reporting Currency, Exchange Rate, Price List.

**Example:** USD, EUR, and SAR are each a Currency master record; a specific Tenant document then uses one of them as its Transaction Currency.

**Approval status:** Draft for BRD Validation

**Source:** New entry proposed by the Master Data and Product Catalog BRD (MESP-31); PRD v1.2 FIN-010 — transactions retain document, functional, and reporting currency amounts.

## Base Currency

**Approved definition:** The currency in which a Company keeps its books and in which all postings are ultimately recorded.

**Business meaning:** The single currency in which the Company's results are measured. SAR is the default base currency for Saudi tenants.

**What it is not:** **Not the Transaction Currency**, which is the currency of an individual document. Not the Reporting Currency, which is used for presentation. Not changeable once transactions exist. See clarification 17.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Company, Transaction Currency, Reporting Currency, Exchange Rate, General Ledger.

**Example:** A Saudi company keeping its books in SAR while buying from a supplier in USD.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — SAR is the default base currency for Saudi tenants and multi-currency transactions are supported.

## Transaction Currency

**Approved definition:** The currency in which an individual business document is agreed and expressed.

**Business meaning:** Preserves what was actually agreed with the supplier or customer, independent of the Company's books.

**What it is not:** Not the Base Currency. Not the Reporting Currency. Both the transaction amount and the converted base-currency amount are retained; the transaction amount is never overwritten by conversion.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Base Currency, Exchange Rate, Purchase Invoice, Sales Invoice, Price List.

**Example:** A purchase invoice issued in USD, recorded in USD and converted to SAR for the ledger.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — multi-currency transactions are supported.

## Reporting Currency

**Approved definition:** A currency, other than the Base Currency, in which financial information may be presented for management or group reporting.

**Business meaning:** Lets stakeholders read results in a familiar currency without changing the books.

**What it is not:** Not the Base Currency and not a second set of books. Not a consolidation mechanism — consolidation is not in Release 1 scope.

**Owning module:** Reporting and Analytics.

**Related entities or documents:** Base Currency, Exchange Rate, reporting.

**Example:** Presenting a SAR-based company's results in USD for a stakeholder review.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — multi-currency scope. Whether Reporting Currency is in Release 1 remains governed by **MESP-54**; consolidated reporting is excluded by the approved **MESP-56** decision.

## Exchange Rate

**Approved definition:** The rate used to convert an amount from the Transaction Currency to the Base Currency for posting.

**Business meaning:** Determines the value recorded in the books for a foreign-currency transaction, and therefore any gain or loss on settlement.

**What it is not:** Not a price. Not a fixed constant. Not something a user should be able to change silently after posting.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Base Currency, Transaction Currency, Exchange Rate Date, Exchange Rate Source, Journal Entry, Rounding Difference.

**Example:** Converting a USD invoice to SAR at the rate applicable on the invoice date.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — multi-currency requirement. Rate source and update process are open in **MESP-54 — Confirm multi-currency exchange-rate source and update process**.

## Exchange Rate Date

**Approved definition:** The date whose Exchange Rate is applied to a specific document.

**Business meaning:** Fixes which rate is correct for the conversion so that the result is repeatable and auditable.

**What it is not:** Not necessarily the posting date, the document date, or the delivery date — which of these governs is a business decision.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Exchange Rate, Journal Entry, Posting, Fiscal Period.

**Example:** Using the invoice date rather than the payment date to convert a purchase invoice.

**Approval status:** Requires Business Decision

**Source:** Open in **MESP-54**. PRD v1.2 does not specify the governing date and none is assumed here.

## Exchange Rate Source

**Approved definition:** The authoritative origin of Exchange Rates used by a Company, together with how and how often rates are updated.

**Business meaning:** Determines who is accountable for rate accuracy and whether rates are entered manually or obtained from an external service.

**What it is not:** Not the rate itself. Not an integration design decision — the business must first decide the source of truth and the update cadence.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Exchange Rate, Exchange Rate Date, Integrations, Audit Event.

**Example:** A single approved daily rate table maintained by finance, versus rates obtained from an external provider.

**Approval status:** Requires Business Decision

**Source:** Open in **MESP-54**. No source, cadence, or provider is assumed here.

## Rounding Difference

**Approved definition:** The small residual amount arising from currency conversion, tax calculation, or unit price multiplication that must be preserved and routed through the approved rounding treatment so entries remain balanced.

**Business meaning:** Keeps the books balanced without hiding or manually forcing differences.

**What it is not:** Not an error to be corrected by editing a posted document. Not a Matching Tolerance. Not a discount.

**Owning module:** Finance and Accounting.

**Related entities or documents:** Journal Entry, Exchange Rate, Tax Category, Posting Rule.

**Example:** A minor residual on a converted foreign-currency invoice posted to the rounding account.

**Approval status:** Requires Business Decision

**Source:** `docs/23_Finance_and_Accounting_BRD.md` §8.3 and FIN-OD-04 / MESP-54. Exact precision, destination, conversion, revaluation, and rounding treatment remain gated; MESP-37 is not authorized to infer them.

---

# 9. Documents and Controls

## Business Document

**Approved definition:** A structured record of a business event that has a defined type, number, status, owner, audit trail, and lifecycle — for example a Purchase Order, Goods Receipt, Sales Invoice, or Stock Adjustment.

**Business meaning:** The unit of business work, control, and evidence. Business rules, permissions, approvals, and postings attach to document types.

**What it is not:** Not a file or attachment. Not a report. Not a master data record such as a Product or Supplier.

**Owning module:** Product Governance, applied by every transactional module.

**Related entities or documents:** Document Number, Document Status, Approval, Posting, Audit Event, Attachment.

**Example:** A purchase order is a business document; the supplier's emailed PDF confirmation stored against it is an attachment.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — document model requirements.

## Document Number

**Approved definition:** The unique, human-readable identifier assigned to a Business Document within its type and defined numbering scope.

**Business meaning:** How people and auditors refer to a specific transaction. For statutory documents such as tax invoices it must be sequential and gapless within its scope.

**What it is not:** Not an internal technical identifier. Not reusable — a cancelled document's number is never reissued.

**Owning module:** Product Governance.

**Related entities or documents:** Business Document, Legal Entity, Country Pack, Cancelled.

**Example:** A sales invoice number that remains unique and traceable within the legal entity.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — numbering requirement. Numbering scope and statutory sequencing under Saudi e-invoicing are open in **MESP-49**.

## Document Status

**Approved definition:** The current state of a Business Document within its approved lifecycle, controlling which actions are permitted.

**Business meaning:** Makes the state machine explicit so that everyone knows what can and cannot be done to a document right now.

**What it is not:** Not a Jira issue status. Not a Fiscal Period state. Not a free-text field.

**Owning module:** Product Governance.

**Related entities or documents:** Draft, Submitted, Approved, Posted, Partially Completed, Completed, Rejected, Cancelled, Closed.

**Example:** A purchase order that is approved may be received against; one still in draft may not.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — document lifecycle requirements. Per-document state machines are specified in each domain BRD.

## Draft

**Approved definition:** A document status indicating the document is being prepared, is editable, and has no business or accounting effect.

**Business meaning:** A safe working state before commitment.

**What it is not:** Not a commitment. Not visible to counterparties. Creates no stock, no liability, and no receivable.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Submitted, Cancelled.

**Example:** A purchase order being prepared and still freely editable.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — document lifecycle requirements.

## Submitted

**Approved definition:** A document status indicating the document has been sent for Approval and is no longer freely editable.

**Business meaning:** Marks the handover from preparer to approver and freezes the content being judged.

**What it is not:** Not Approved. Not Posted. Not a commitment to a counterparty.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Draft, Approved, Rejected, Approver.

**Example:** A purchase request submitted and awaiting the department manager's decision.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — document lifecycle requirements.

## Approved

**Approved definition:** A document status indicating an authorised Approver has accepted the document, allowing it to proceed to its next step.

**Business meaning:** Records that accountability has been taken for the transaction.

**What it is not:** **Not Posted.** An approved document may still have no ledger effect. Not Completed. See clarification 15.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Approval, Approver, Posted, Audit Event.

**Example:** An approved purchase order that may now be issued to the supplier.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approval and lifecycle requirements.

## Posted

**Approved definition:** A document status indicating the document's financial and inventory effects have been committed to the ledgers and the record is now immutable.

**Business meaning:** The transaction is part of the official books and stock position.

**What it is not:** Not editable. Not deletable. Not the same as Approved. Correction is only by Reversal.

**Owning module:** Product Governance.

**Related entities or documents:** Posting, Journal Entry, Stock Movement, Reversal, Immutable Record.

**Example:** A posted goods receipt that has increased stock and created the corresponding accounting entry.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — posting and immutability requirements; approved decision list (MESP-16).

## Partially Completed

**Approved definition:** A document status indicating some but not all of the document's expected follow-on activity has occurred.

**Business meaning:** Keeps partly fulfilled commitments visible so the remaining balance is not forgotten.

**What it is not:** Not Completed. Not Cancelled. Not an error state.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Outstanding Quantity, Goods Receipt, Delivery, Allocation.

**Example:** A purchase order for 100 units with 80 received and 20 still outstanding.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — lifecycle and partial fulfilment requirements.

## Completed

**Approved definition:** A document status indicating all expected follow-on activity for the document has occurred.

**Business meaning:** Nothing further is expected against this document operationally.

**What it is not:** Not Closed, which is an administrative decision to stop expecting anything further. Not Settled, which concerns money. Not Posted.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Partially Completed, Closed, Settlement.

**Example:** A purchase order fully received and fully invoiced.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — lifecycle requirements. Completion criteria per document type require confirmation in each domain BRD.

## Rejected

**Approved definition:** A document status indicating an authorised Approver refused the document, stopping its progress.

**Business meaning:** Records a deliberate refusal with a reason and an accountable person.

**What it is not:** Not Cancelled — rejection is an approval outcome, cancellation is a lifecycle termination. Not Rejected Quantity, which is a receiving concept.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Submitted, Approver, Audit Event.

**Example:** A purchase request refused with a stated reason and returned to the requester.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — approval requirements.

## Cancelled

**Approved definition:** A document status indicating the document has been terminated before posting and will have no business effect.

**Business meaning:** Ends a document that is no longer wanted while keeping the record for audit.

**What it is not:** **Not a Reversal.** Cancellation applies before posting; reversal applies after. Not a deletion — the cancelled record and its number are retained. See clarification 16.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Reversal, Document Number, Audit Event.

**Example:** Cancelling an approved but unposted purchase order that the business no longer needs.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2; approved decision list (MESP-16) — posted documents are corrected by reversal, not cancellation.

## Closed

**Approved definition:** A document status indicating no further activity will be accepted against the document, whether or not it was fully completed.

**Business meaning:** Lets the business stop chasing a residual balance that will never be fulfilled, with an accountable decision and reason.

**What it is not:** Not Completed. Not Cancelled. Not a Fiscal Period close, which is a finance control over posting.

**Owning module:** Product Governance.

**Related entities or documents:** Document Status, Completed, Partially Completed, Approval.

**Example:** Closing a purchase order with 20 units still outstanding that the supplier will never deliver.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — lifecycle requirements. Closure authority requires confirmation in each domain BRD.

## Attachment

**Approved definition:** A file stored against a Business Document as supporting evidence.

**Business meaning:** Retains the external proof behind a transaction — a supplier confirmation email, a signed delivery note, a bank advice.

**What it is not:** Not a Business Document. Carries no status, no posting effect, and no numbering. Not a substitute for recording the business data in the system.

**Owning module:** Security and Audit.

**Related entities or documents:** Business Document, Audit Event, Supplier Confirmation, Delivery Note.

**Example:** The supplier's emailed confirmation attached to the purchase order as evidence for the manually recorded confirmation.

**Approval status:** Requires Business Decision

**Source:** PRD v1.2 — evidence requirements. Retention and residency of attachments are open in **MESP-50**.

## Source Document

**Approved definition:** The originating Business Document that caused a downstream record such as a Stock Movement, Journal Entry, or Audit Event.

**Business meaning:** Makes every ledger entry explainable by pointing back to the business event that created it.

**What it is not:** Not an Attachment. Not a report. Not optional — every posting must identify its source document.

**Owning module:** Product Governance.

**Related entities or documents:** Business Document, Stock Movement, Journal Entry, Audit Event, Correlation Identifier.

**Example:** A stock movement whose source document is a specific goods receipt.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — traceability requirements.

## Idempotency

**Approved definition:** The property that repeating the same business request produces the same single result rather than duplicating it.

**Business meaning:** Protects the business from double-posting a receipt, invoice, or payment when a user resubmits or a connection is retried.

**What it is not:** Not a duplicate-check report run afterwards. Not a technical retry policy alone — the business must state what counts as the same request.

**Owning module:** Security and Audit.

**Related entities or documents:** Business Document, Posting, Correlation Identifier, Integrations.

**Example:** Submitting the same goods receipt twice results in one posted receipt, not two.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — control requirements. Duplicate-definition rules require confirmation in MESP-38 and MESP-39.

## Correlation Identifier

**Approved definition:** An identifier that links all records created by a single business action or integration exchange, across documents, postings, and audit events.

**Business meaning:** Lets an auditor or investigator follow one business action end to end without guessing.

**What it is not:** Not a Document Number. Not a user identifier. Not visible business data.

**Owning module:** Security and Audit.

**Related entities or documents:** Audit Event, Source Document, Idempotency, Integrations.

**Example:** One identifier tying a posted goods receipt, its stock movements, its journal entry, and its audit events together.

**Approval status:** Draft for BRD Validation

**Source:** PRD v1.2 — traceability requirements. Requires confirmation in MESP-38.

## Immutable Record

**Approved definition:** A record that cannot be changed or deleted after creation, and that can only be superseded by a new record such as a Reversal or Adjustment.

**Business meaning:** The foundation of trust in the ledgers and the audit trail. Applies to posted journal entries, stock ledger entries, and audit events.

**What it is not:** Not a locked draft. Not a permission setting that an administrator can waive. Not append-only in name while allowing silent edits.

**Owning module:** Security and Audit.

**Related entities or documents:** Stock Ledger, Journal Entry, Audit Event, Reversal, Posted.

**Example:** A posted stock movement that is corrected by a new reversing movement, with both retained.

**Approval status:** Approved Product Baseline

**Source:** PRD v1.2 — immutable stock ledger and audit requirements; approved decision list (MESP-16).

---

# 10. Required clarifications

These nineteen distinctions are the terms most often confused in ERP discussions. Every BRD, business rule, and acceptance scenario must respect them.

### 1. Tenant versus Company

A **Tenant** is the subscription and data-isolation boundary on the Platform. A **Company** is an operating and accounting entity inside a Tenant. One Tenant may contain several Companies; a Company never spans Tenants. Subscriptions, entitlements, and tenant isolation are Tenant concepts. Chart of accounts, fiscal calendar, and financial statements are Company concepts.

### 2. Company versus Branch

A **Company** owns the books and produces financial statements. A **Branch** is an operational location inside a Company used to organise activity and responsibility. A Branch has no separate chart of accounts and issues no statutory financial statements.

### 3. Branch versus Warehouse

A **Branch** is an organizational and operational unit. A **Warehouse** is a stock-holding location belonging to exactly one Branch. Stock balances exist at Warehouse level, never at Branch level directly. A Branch may hold several Warehouses.

### 4. User versus Employee

A **User** is a login identity that acts in the system and is recorded in the audit trail. An **Employee** is a person recorded as working for the Company, used for business attribution such as requester or salesperson. Not every employee is a user, and not every user is an employee. Access is granted to Users, never to Employees.

### 5. Supplier versus Tenant

A **Supplier** is external business master data belonging to one Tenant, and is never a system user. A **Tenant** is a paying subscriber to the Platform with its own users and data. A supplier does not log in, does not enter data, and has no visibility of tenant data. Supplier responses are manually recorded by authorized purchasing users.

### 6. Business Customer versus Platform Customer

A **Business Customer** is the tenant's own customer, recorded as master data and party to the Order-to-Cash lifecycle. A **Platform Customer** is the Tenant itself, which subscribes to the Platform under a Plan. Sales Invoices are issued to Business Customers by the tenant; Subscriptions are agreed between the Platform Owner and the Tenant. The two must never appear in the same requirement without being named explicitly.

### 7. Product versus Stock

A **Product** is a catalog definition — what a thing is. **Stock** is a quantity of that product held in a Warehouse at a point in time, with a value. A product exists with zero stock. Creating a product creates no stock. Only a posted inventory document creates stock.

### 8. Purchase Order versus Goods Receipt

A **Purchase Order** is a commitment to a Supplier. **A Purchase Order does not increase stock.** A **Goods Receipt** records physical arrival and acceptance, and **stock increases only through a posted Goods Receipt or another authorized inventory-in document.** An order creates an expected and outstanding position; a receipt creates a stock movement.

### 9. Goods Receipt versus Purchase Invoice

A **Goods Receipt** is the physical event: goods arrived, stock increased. A **Purchase Invoice** is the financial event: the supplier claims payment and a liability is created in Accounts Payable. **A Purchase Invoice does not independently increase stock.** The two are matched, not merged.

### 10. Purchase Invoice versus Supplier Payment

A **Purchase Invoice** creates the obligation to pay. A **Supplier Payment** discharges it and reduces Accounts Payable. Approving an invoice is not paying it. An unpaid approved invoice is a liability, not a settled transaction.

### 11. Sales Order versus Delivery

A **Sales Order** is the commitment to supply a Business Customer; it may reserve stock but does not reduce it. A **Delivery** records goods physically leaving the Warehouse and reduces stock. An order is a promise; a delivery is a movement.

### 12. Delivery versus Sales Invoice

A **Delivery** moves goods and reduces stock. A **Sales Invoice** creates the receivable, recognises revenue and output tax, and is the statutory tax document in Saudi Arabia. Delivering is not invoicing, and invoicing is not delivering.

### 13. Sales Invoice versus Customer Receipt

A **Sales Invoice** creates the claim on the Business Customer. A **Customer Receipt** records money received and reduces Accounts Receivable. An issued invoice is not collected cash.

### 14. Stock Balance versus Stock Ledger

The **Stock Ledger** is the immutable, chronological history of every stock movement with quantity and value. The **Stock Balance** is the current position derived from that history. The balance is never adjusted directly; it changes only because a new ledger entry was created.

### 15. Posting versus Approval

**Approval** is a business authorisation by an accountable person. **Posting** is the committing of financial and inventory effects to the ledgers, after which the record is immutable. A document may be approved and not yet posted. Posting is not a second approval, and approval is not an accounting event.

### 16. Cancellation versus Reversal

**Cancellation** terminates a document that has **not** been posted; it has no ledger effect and the record and its number are retained. **Reversal** creates an equal and opposite posting to undo a document that **has** been posted; both the original and the reversal remain visible. Nothing is ever deleted, and posted documents are never edited.

### 17. Base Currency versus Transaction Currency

The **Base Currency** is the currency of the Company's books — SAR by default for Saudi tenants. The **Transaction Currency** is the currency of an individual document as agreed with the counterparty. Both amounts are retained; the transaction amount is never overwritten by conversion. The **Reporting Currency** is a third, presentation-only concept.

### 18. Permission versus Entitlement

A **Permission** is a user-level security right to perform an action. An **Entitlement** is a tenant-level commercial right to use a module or capacity, derived from the Subscription and Plan. Both must allow an action for it to succeed. Granting a permission for a module the tenant is not entitled to does not enable access, and being entitled to a module does not authorise any individual user.

### 19. Retail POS versus B2B Sales

**B2B Sales** is the Release 1 scope: named Business Customers, Quotation, Sales Order, Delivery, Sales Invoice, Customer Receipt, credit terms, and account trading. **Retail POS** is point-of-sale selling to walk-in consumers with cashier operations, cash drawers, retail shifts, and retail checkout.

- **Retail POS is not part of Release 1.**
- **Retail POS must not be used as a synonym for B2B Sales.** The two have different actors, controls, cash handling, and documents.
- **POS may be considered only as a future module** consuming the shared Sales, Inventory, Finance, Tax, and Payment capabilities, rather than duplicating them.

Any requirement that mentions a cashier, a cash drawer, a retail shift, or a retail checkout is out of Release 1 scope and must be recorded as future scope, never written into a Release 1 BRD.

---

# 11. Terms requiring business validation

## Requires Business Decision — blocked on a named open decision

These terms cannot be finalised until the linked Jira decision Task is answered with approved evidence. No answer has been assumed or invented.

| Term | Blocking decision |
|---|---|
| Country Pack, Tax Category, Sales Invoice, Document Number | MESP-49 — Saudi e-invoicing launch scope |
| Approval, Approver | MESP-42 — purchase approval workflow; MESP-55 — delegation, escalation, out-of-office |
| Credit Limit, Credit Exposure | MESP-46 — B2B customer credit-control policy |
| Supplier Confirmation, Partial Supplier Confirmation | MESP-43 — supplier confirmation and partial-confirmation rules |
| Three-Way Match, Matching Tolerance | MESP-44 — purchase order, receipt, and invoice matching tolerances |
| Batch, Lot, Serial Number, Expiry Date | MESP-41 — batch, lot, serial, and expiry tracking scope |
| Reserved Quantity, Stock Reservation | MESP-45 — negative-stock policy; MESP-46 |
| Opening Balance | MESP-51 — migration sources and opening-balance requirements |
| Supplier Payment, Customer Receipt, Cash Account, Bank Account | MESP-47 — supported payment and receipt methods |
| Reconciliation | MESP-53 — report catalogue and reconciliation ownership |
| Exchange Rate, Exchange Rate Date, Exchange Rate Source | MESP-54 — exchange-rate source and update process |
| Reporting Currency | MESP-54; consolidated reporting remains excluded by the approved MESP-56 decision |
| Rounding Difference | MESP-54 / FIN-OD-04 — conversion, precision, rounding, and revaluation treatment |
| Attachment, Audit Event retention | MESP-50 — tenant data residency and retention policy |

## Draft for BRD Validation — requires workshop confirmation

These terms use standard ERP meanings proposed by business analysis. They are not product decisions and must be confirmed in the named domain BRD, but they do not block on an open decision.

Department, Access Scope, Separation of Duties, Employee, Customer Contact, Item, SKU, Barcode, Category, Purchase Unit, Sales Unit, Price List, Supplier Quotation, Goods Receipt Line, Rejected Quantity, Available Quantity, Expected Quantity, Damaged Quantity, In-Transit Quantity, Transfer Shipment, Transfer Receipt, Count Variance, Delivery Note, Completed, Closed, Idempotency, Correlation Identifier.

---

# 12. Banned and deprecated vocabulary

To keep BRD language consistent, the following terms must not be used in any Release 1 BRD, business rule, or acceptance scenario.

| Do not use | Use instead | Reason |
|---|---|---|
| Cashier, cash drawer, till, shift, checkout | No Release 1 equivalent | Retail POS concepts, excluded from Release 1 |
| POS sale, retail sale, walk-in customer | Sales Order, Delivery, Sales Invoice to a Business Customer | Release 1 is B2B only |
| GRN used to mean the invoice | Goods Receipt for the physical event; Purchase Invoice for the financial claim | The two events are distinct |
| Bill | Purchase Invoice or Sales Invoice, stated explicitly | Ambiguous direction |
| Stock card, stock file | Stock Ledger for history; Stock Balance for the current position | The two are different concepts |
| Client, account, buyer | Business Customer, or Tenant where the Platform's own customer is meant | Ambiguous between the tenant's customer and the Platform's customer |
| Vendor account, vendor login | Supplier; suppliers have no login | Suppliers are not system users |
| Delete, edit posted document | Reversal, return, credit note, debit note, or adjustment | Posted records are immutable |
| Approve to mean post | Approval for authorisation; Posting for the ledger effect | Distinct controls |
| Branch stock | Warehouse stock | Stock exists at Warehouse level |
| Wafra process, Wafra rule | The generic product requirement it validates | No Wafra-specific core logic is permitted |

---

# 13. Governance of this glossary

- This glossary is the mandatory vocabulary for MESP-27 … MESP-40 and for every future implementation Story.
- It does **not** answer any open decision. Every term that depends on one names the Jira decision Task and stops there.
- It introduces **no Wafra-specific vocabulary**. Wafra is referred to only as the first validation tenant.
- Changes follow the BRD governance and approval process defined in MESP-17. A term whose approval status changes must be versioned, with the change recorded in the Product Decision Register (MESP-22).
- When an open decision (MESP-41 … MESP-56 or a separately recorded bundle such as FIN-OD-09 / MESP-110) is answered, the affected gated detail moves from *Requires Business Decision* to *Approved Product Baseline* and the Traceability Matrix (MESP-19) is updated. A term may already have an approved concept while its named implementation detail remains gated.

**Approval required from:** Product Owner, Business Sponsor, and the Business Process Owner of each affected domain. Named owners are pending — see MESP-20.
