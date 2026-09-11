# Mini ERP SaaS Platform — Data Migration and Tenant Onboarding BRD

## 1. Document control

| Field | Value |
|---|---|
| Document | Data Migration and Tenant Onboarding Business Requirements Document |
| Work item | MESP-40 |
| GitHub Issue | #129 |
| Parent / Epic | MESP-15 / GitHub #104 |
| Version | 0.1 — requirements review candidate |
| Date | 10 September 2026 |
| Status | Ready for independent requirements review; not yet accepted |
| Business owner | Product Owner / Owner, with Finance, Inventory, Platform, Security/Audit, and Tenant-owner input |
| Release | Release 1 B2B ERP |
| Classification | Prerequisite / governance and requirements |
| Product implementation | None authorized or included |
| Canonical source | This Markdown document |

This document is the MESP-40 business authority for the later migration and
onboarding capability. It is implementation-neutral. It does not authorize
MESP-141, MESP-142, a production migration, a cutover, or a tenant activation.

The document uses these classifications:

| Classification | Meaning |
|---|---|
| Confirmed | Accepted project requirement or architecture consequence |
| Approved contract-bound | Owner-approved bounded contract that still requires named specialist or production validation |
| Open Product Decision | Material choice not selected by current authority |
| Deferred Gate | Production, legal, provider, volume, residency, or specialist evidence still required |
| Optional | Permitted only when the tenant and owning domain explicitly choose it |
| Out of Scope | Not delivered or claimed by Release 1 MESP-40 |

## 2. Purpose and business outcome

MESP-40 defines a repeatable, reviewable way to prepare a new Tenant, load
approved configuration and initial business data, validate it before effect,
reconcile it to source evidence, and hand the Tenant to the separate platform
activation process.

The desired outcome is not “a file import.” It is a controlled opening state
whose ownership, mappings, errors, financial meaning, stock meaning, audit
trail, and approval are understandable to Product, Engineering, QA,
Architecture, Operations, Finance, Inventory, and the tenant business owner.

The current approved migration contract is B1 / PD-041 from MESP-51 and
MESP-116: configuration, master data, opening stock and valuation, GL/AP/AR,
cash/bank, tax/rate/terms references, validation, quarantine, dry runs,
reconciliation, cutover, rollback, and repeatable Tenant onboarding are
required at a bounded contract level. Specialist validation remains mandatory
before production or irreversible accounting, stock, or cutover action.

## 3. Authority, source order, and reconciliation

### 3.1 Source order

For this BRD, conflicts are resolved in this order:

1. Current accepted repository governance and architecture documents.
2. Live GitHub Issues and the GitHub Project.
3. Current repository PRD, BRD, and specification documents.
4. Preserved Jira migration provenance and comments.
5. Accepted merged implementation evidence where it constrains a business contract.
6. Existing project conventions.

Jira is historical tracker and migration provenance only. It is not mutated by
this work.

### 3.2 Sources consumed

The following sources were read and are reused rather than duplicated:

| Source | Reused authority |
|---|---|
| docs/MESP_PRD_v1.2.docx | PRD v1.2 approved 31 July 2026; ADM-003, BR-013, and migration baseline section 19.1 |
| docs/11_SaaS_Platform_Administration_BRD.md | M27 Tenant catalogue, provisioning run, lifecycle, Plan/Entitlement, activation and module readiness |
| docs/12_Identity_and_Access_BRD.md | Membership, user/role/scope authority, duplicate identity handling, support boundary |
| docs/13_Multi_Tenancy_BRD.md | One-Tenant context, default-deny isolation, migration mapping and quarantine |
| docs/14_Organization_and_Company_Structure_BRD.md | Platform → Tenant → Company/Legal Entity → Branch → Warehouse hierarchy and organization migration |
| docs/16_Master_Data_and_Product_Catalog_BRD.md | Master-data ownership, identity, lifecycle, duplicate and import expectations |
| docs/21_Procurement_and_Purchase_to_Pay_BRD.md | Supplier, AP, purchasing, receiving and migration handoff boundaries |
| docs/22_Inventory_and_Warehouse_Management_BRD.md | Opening balance, inventory ledger, quantity/value and stock reconciliation |
| docs/23_Finance_and_Accounting_BRD.md | GL, AP, AR, cash/bank, tax, currency, period and opening-balance controls |
| docs/24_Sales_and_Order_to_Cash_BRD.md | Business Customer, price, order and opening-state boundary |
| docs/28_Release_1_Saudi_Localization_BRD.md | Arabic/English, RTL, country-pack and Saudi presentation boundaries |
| docs/29_Security_Audit_and_Data_Governance_BRD.md | Immutable evidence, sensitive data, access, export, retention and audit controls |
| docs/31_Release_1_Consolidated_Owner_Decision_Pack.md | B1/PD-041 migration contract, B3/PD-043 currency contract, B4/PD-044 Finance contract, B5/PD-046 Inventory contract, C gates |
| docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md | Tenant host/context and branding invariants |
| docs/94_Product_Delivery_Master_Plan.md | BRD gate, phase boundaries and no implementation before requirements acceptance |
| GitHub MESP-51 Issue #140 | B1/PD-041 is approved contract-bound; MESP-40 remains the detailed BRD |
| GitHub MESP-141 Issue #229 | Downstream implementation scope and named dependency handoff |
| GitHub MESP-142 Issue #230 | Later stabilization/release-candidate dependency only; not activated |
| GitHub MESP-48 Issue #137 | Open volume, capacity and production-governance gate |
| GitHub MESP-50 Issue #139 | Open residency, retention, privacy, legal-hold, purge, backup and production-governance gate |

### 3.3 Source conflicts recorded

| Conflict | Resolution |
|---|---|
| The migrated MESP-40 body and several older BRD passages say Jira is current authority or that MESP-51 remains open. | Current governance and live GitHub state resolve this: GitHub is work-management authority; MESP-51/#140 is closed at approved contract-bound B1/PD-041; MESP-40/#129 remains open pending this BRD review. |
| MESP-51 defines the approved contract but does not select historical transaction or open-document treatment. | Preserve as M40-DEC-001. No historical or open-document implementation contract is assumed. |
| Older documents contain recommended migration or currency defaults that were not all approved at the time they were written. | Consume only current approved contract-bound decisions: B1/PD-041 and B3/PD-043, with their specialist/production conditions. |
| MESP-48 and MESP-50 are referenced by accepted documents but are not closed. | Preserve as Deferred Gates; no numeric volume, residency, retention, legal, provider, or backup value is invented. |

## 4. Business purpose, scope, and exclusions

### 4.1 In scope

- A Tenant onboarding lifecycle that reuses the M27 lifecycle vocabulary.
- A clear boundary between Tenant provisioning and business data migration.
- Tenant, Company/Legal Entity, Branch, Warehouse, locale, country-pack,
  currency, period, numbering, template, Plan, Entitlement, and module-readiness
  prerequisites.
- Source ownership, extraction context, cleansing, mapping, duplicate handling,
  quarantine, versioning, and accountable exception decisions.
- Initial configuration, master data, and controlled opening positions needed
  for Release 1 B2B ERP operation.
- Validation-only, preview, dry-run, import, retry, failure, reconciliation,
  sign-off, handoff, and readiness evidence.
- Financial, inventory, AR/AP, cash/bank, tax-reference, currency, and
  localization consequences where the owning BRD supports them.
- Tenant isolation, permissions, separation of duties, sensitive-data controls,
  auditability, error reporting, and business continuity expectations.
- Six implementation-neutral business workflows and testable acceptance criteria.

### 4.2 Explicit exclusions

- Product, backend, frontend, database, EF, schema, migration, API, queue,
  parser, storage, cloud-provider, deployment, or infrastructure design.
- Physical data migration, production execution, cutover, reset, purge, or
  tenant activation in this session.
- MESP-141 activation or implementation.
- MESP-142 stabilization, UAT, performance, or release-candidate work.
- Retail POS, cashier, store, loyalty, retail pricing, or offline checkout.
- Full historical transaction migration unless M40-DEC-001 is explicitly
  decided later.
- ZATCA/FATOORA connectivity, signing, submission, clearance, certification,
  or statutory/legal conclusions.
- Production volume, concurrency, performance, availability, storage,
  retention, residency, backup, restoration, or support commitments.
- Wafra-specific workflow, role, schema, threshold, report, numbering, or
  exception logic. Wafra is validation evidence only.
- Arbitrary database rollback or a promise that committed accounting or stock
  history can be erased.

## 5. Controlled vocabulary and boundaries

The glossary and accepted BRDs remain authoritative. For this document:

| Term | Meaning in MESP-40 |
|---|---|
| Tenant | The customer subscription and primary data-isolation boundary |
| Provisioning | Creation and enablement of the Tenant platform foundation and configuration under M27 |
| Migration | Controlled transfer/bootstrap of Tenant business data from an identified source into an approved opening state |
| Source system | The named system or business record set from which a domain extract is obtained |
| Extract | A versioned source snapshot with an extraction date/time, owner, scope and integrity evidence |
| Batch/session | One immutable migration business run identity containing domain attempts and row outcomes |
| Mapping | A reviewed relationship between a source business value and a target business concept |
| Quarantine | A non-operational holding outcome for a rejected, ambiguous, unsupported or review-required record |
| Opening state | The approved balances, stock, configuration and master records at the agreed transition boundary |
| Dry run | A validation/simulation run that produces planned outcomes and no authoritative business effect |
| Reconciliation | Comparison of source control totals and meanings with the target opening state |
| Cutover | An explicitly approved transition from legacy/source operation to the target Tenant opening state |
| Rollback | A business recovery decision after a failed or unsafe cutover; it is not an implied database undo |
| Active | The M27 Tenant lifecycle state in which entitled, authorized B2B operation may occur |

### 5.1 Provisioning is not migration

Provisioning owns:

- Tenant catalogue identity and lifecycle.
- Initial Tenant Administrator invitation.
- Company/Legal Entity and supported organization foundation.
- Country Pack, locale, Plan, Subscription, Entitlements and module readiness.
- Governed numbering, document-template and branding configuration.
- Provisioning-run steps, safe retry, cleanup/compensation and configuration snapshot.

Migration owns:

- Source manifest, extracts, mapping, duplicate and validation outcomes.
- Initial master data and opening positions.
- Row/domain/batch outcomes, rejected records and exception ownership.
- Dry runs, reconciliation, sign-off, cutover readiness and correction evidence.

No single “Create Tenant” action may conceal an unreviewed business-data import.
Provisioning may reach Configuration Required while migration is incomplete.
The Tenant cannot reach Active merely because provisioning or migration has
finished; the activation gate in section 14 is separate.

## 6. Actors and responsibilities

| Actor | Responsibilities | Cannot do |
|---|---|---|
| Platform Administrator | Create/review onboarding draft, verify commercial and platform prerequisites, initiate provisioning, coordinate readiness and activation decision | View or export Tenant business data by role alone; bypass membership, Entitlement, audit or gates |
| Platform Operations Owner | Own provisioning/migration operational coordination, safe retry, incident escalation and run evidence | Grant Tenant business authority or silently mark unresolved work successful |
| Tenant Administrator | Confirm Tenant identity, organization, locale, master-data ownership and readiness; acknowledge handoff | Cross Tenant boundary, change Platform-owned Plan/Entitlement or approve its own prohibited authority |
| Finance owner / Controller | Own GL, AP, AR, cash/bank, tax-reference, currency/rate, period and financial reconciliation meaning | Accept an unexplained imbalance or use a balancing entry to hide source variance |
| Inventory / Operations owner | Own warehouses, items/UOM, stock quantity/value, tracking and inventory reconciliation | Bypass the Inventory ledger or silently change source quantity/value |
| Procurement owner | Own supplier, purchasing, receipt and open procurement implications where included | Approve an unresolved supplier, receipt or AP mapping by visibility alone |
| Sales owner | Own Business Customer, price, order and AR/open sales implications where included | Assume open-order/history migration is included without M40-DEC-001 |
| Migration operator / onboarding owner | Prepare source manifest, mapping, validation, dry runs, exception register and execution request | Approve their own material migration outcome where SoD policy forbids it |
| Reviewer / approver | Independently review evidence, exceptions, reconciliation and readiness under the applicable policy | Edit original evidence or authorize outside their Tenant and organization scope |
| Security / Audit reviewer | Review access, isolation, sensitive-data, audit and material exception evidence | Gain ordinary Tenant data access merely by being a reviewer |
| Authorized Support User | Investigate a named case within exact Tenant scope and time-bound support authorization | Use support access as export authority, migration approval, or standing Tenant access |

The final permission catalogue and role names remain owned by Identity and the
applicable domain. MESP-40 defines minimum authority outcomes, not a new
universal privileged role.

## 7. Tenant onboarding lifecycle

### 7.1 Tenant lifecycle

MESP-40 reuses the accepted M27 sequence:

Draft → Provisioning → Configuration Required → Ready for Activation →
Active → Grace Period → Suspended → Reactivated → Active → Export Requested →
Termination Pending → Terminated → Retained → Purge Approved → Purged

Migration activity can occur only in an allowed onboarding/configuration state
and must follow the active Tenant lifecycle, membership, organization scope,
Entitlement and business permissions. Migration completion does not transition
the Tenant to Active.

### 7.2 Migration batch/session lifecycle

The migration run has a separate business lifecycle:

Draft → Uploaded → Validation in Progress → Validation Failed or Validation
Passed → Preview/Dry Run Complete → Approved for Execution → Execution in
Progress → Execution Complete, Partially Completed, Execution Failed, or
Outcome Unknown → Reconciliation Pending → Reconciled → Ready for Handover →
Closed

Cancelled is a terminal pre-commit outcome. Outcome Unknown is a safe
reconciliation-required outcome after an effect boundary when the result
cannot be proved. A state label never hides unresolved rows, variances,
authority failures or production gates.

### 7.3 Onboarding entry and exit

Entry requires an approved onboarding request, a unique target Tenant
boundary, an accountable Tenant owner, an onboarding owner, required
provisioning prerequisites, source ownership, and an agreed scope for the
run. Exit to handover requires completed reconciliation, accepted exceptions
or a documented decision, named approvals, and a readiness snapshot. Exit to
Active is owned by M27 activation and requires the separate platform and
Tenant acknowledgements.

## 8. Business requirements

### 8.1 Governance and lifecycle

| ID | Requirement |
|---|---|
| M40-REQ-001 | The onboarding process shall produce a repeatable, reviewable Tenant opening state for Release 1 B2B ERP operation. |
| M40-REQ-002 | Every migration run shall identify exactly one target Tenant; any broader platform operation shall be separately authorized as a Platform governance action and shall not combine Tenant business contexts. |
| M40-REQ-003 | Imported source data shall not be allowed to assign, change or infer Tenant ownership; the server-owned target Tenant and authorized context remain authoritative. |
| M40-REQ-004 | Tenant provisioning and business data migration shall remain separate business activities with separate evidence and outcomes. |
| M40-REQ-005 | Tenant onboarding shall use the accepted M27 lifecycle and shall not move directly from incomplete setup or migration to Active. |
| M40-REQ-006 | Each provisioning stage and migration batch/domain attempt shall expose a reviewable status, owner, attempt identity, outcome, failure reason and safe next action. |
| M40-REQ-007 | The onboarding plan shall identify Platform Administration, Tenant Administration, Finance, Inventory/Operations, Migration, Reviewer/Approver and Support responsibility for each material action. |
| M40-REQ-008 | Tenant setup shall capture and validate stable Tenant identity, legal/customer identity, bilingual display identity, contacts, country and contractual reference where applicable. |
| M40-REQ-009 | Tenant setup shall establish the supported Company/Legal Entity, Branch and Warehouse hierarchy and shall reject ambiguous or cross-Tenant parent relationships. |
| M40-REQ-010 | Tenant setup shall establish default language, supported languages, locale, operating time zone, accounting calendar/period context and effective organizational scope. |
| M40-REQ-011 | Tenant setup shall establish functional/base currency, permitted transaction currencies, effective exchange-rate prerequisites, tax configuration references, document-template profile and numbering profile before dependent opening data is accepted. |
| M40-REQ-012 | Plan, Subscription, Entitlement, module dependency, configuration and opening-evidence prerequisites shall be valid before a module or Tenant is eligible for activation. |

### 8.2 Source, template and mapping

| ID | Requirement |
|---|---|
| M40-REQ-013 | Each domain migration shall identify a named source owner, target business owner, source system or record set, extraction date/time, scope, source status and cleansing responsibility. |
| M40-REQ-014 | Each extract shall be traceable to a source snapshot or equivalent source evidence, and any changes between extracts shall be visible as a new version or attempt. |
| M40-REQ-015 | The migration contract shall use versioned, domain-specific templates or structured source contracts with documented required fields, optional fields, reference meanings and version compatibility. |
| M40-REQ-016 | Each source record shall carry a stable source identifier and each domain shall define its approved duplicate key; target identifiers shall not be supplied as authority by an untrusted source. |
| M40-REQ-017 | Templates shall preserve Unicode, Arabic and English values, date meaning, decimal meaning, currency codes, units and source text without lossy transliteration or locale-dependent reinterpretation. |
| M40-REQ-018 | The load order shall validate and establish configuration and reference/master data before dependent opening positions or documents. |
| M40-REQ-019 | Release 1 migration shall support the required master-data domains in section 9, subject to each owning BRD's lifecycle, duplicate, scope and approval rules. |
| M40-REQ-020 | General-ledger opening data shall identify the Company/Legal Entity, fiscal context, account/dimension meaning, accounting date, currency basis and source control total required for Finance reconciliation, including the applicable relationship to any subsidiary opening representation. |
| M40-REQ-021 | Inventory opening data shall identify the Warehouse, item/product, UOM, quantity, valuation/cost basis, currency and opening date required by the Inventory ledger and valuation contract, including the relevant Inventory GL control-account relationship where the authoritative accounting model requires it. |
| M40-REQ-022 | Receivable and payable opening data shall identify the Customer or Supplier, Company/Legal Entity, document or source reference where available, accounting date, balance, currency and the applicable AR/AP GL control-account mapping or control total. |
| M40-REQ-023 | Cash and bank opening data shall identify the Company/Legal Entity, cash/bank account concept, accounting date, balance, currency, source reference and the applicable GL control/cash/bank account mapping or control total where the supported Finance capability includes the account. |
| M40-REQ-024 | Tax, exchange-rate, Payment Term, price-list and other configuration/reference inputs shall be validated before dependent opening data is accepted; no missing reference may be silently defaulted. |
| M40-REQ-025 | Historical transactions, open documents, attachments and other pre-opening activity shall be handled only according to an explicit decision recorded under M40-DEC-001; this BRD does not assume full history. |

### 8.3 Validation, execution and failure

| ID | Requirement |
|---|---|
| M40-REQ-026 | Before an authoritative effect, the process shall validate template shape, mandatory data, duplicates, references, Tenant scope, dates, amounts, quantities, currencies, UOMs, accounting balance and unsupported data. |
| M40-REQ-027 | The process shall provide validation-only, preview, dry-run and reconciliation-preview outcomes that are distinguishable from an authoritative import. |
| M40-REQ-028 | Upload, validate, execute, approve, retry, cancel and evidence-view actions shall require current server-side authority for the exact Tenant and applicable organization scope. |
| M40-REQ-029 | Every execution shall use one stable batch/session identity, safe duplicate protection and a recorded attempt identity so a retry cannot create a second authoritative effect. |
| M40-REQ-030 | A migration batch shall be atomic within each approved domain execution unit; domain-level partial success is allowed only when each completed unit is reconciled and the remaining units are visibly incomplete. |
| M40-REQ-031 | Failed or review-required records shall be quarantined with row-level outcome, error classification, actionable message, source identifier and correction ownership; they shall not become operational data. |
| M40-REQ-032 | Corrected records shall be retryable through a controlled later attempt linked to the original batch, without duplicating accepted records or erasing prior failures. |
| M40-REQ-033 | The process shall reconcile source counts, accepted/rejected/duplicate outcomes, financial control totals, inventory quantities and values, AR/AP balances, cash/bank balances and applicable document counts. |
| M40-REQ-034 | Reconciliation shall identify the preparer, domain owner, reviewer, approval outcome, exceptions, variance explanation, source basis, target basis and effective time. |
| M40-REQ-035 | The process shall retain immutable evidence for initiation, target Tenant, source/template version, batch/session, validation, execution, rejection, approvals, reconciliation, retries and handover. |

### 8.4 Isolation, currency, localization and correction

| ID | Requirement |
|---|---|
| M40-REQ-036 | Migration shall fail closed for cross-Tenant references, foreign organization parents, mismatched target scope, unsafe Tenant inference and evidence requests outside the viewer's authorized boundary. |
| M40-REQ-037 | Multi-currency migration shall distinguish transaction/source currency, functional/base currency, Reporting Currency where applicable, exchange-rate evidence, precision/rounding and unavailable-rate outcomes. |
| M40-REQ-038 | Arabic and English names, descriptions and business text shall be preserved as supplied where supported, remain Unicode-safe, and produce readable localized errors and review evidence in both languages where the UI supports them. |
| M40-REQ-039 | Sensitive or restricted source fields shall be minimized, masked or access-controlled in previews, errors, exports and audit evidence according to Security/Audit and MESP-50 policy. |
| M40-REQ-040 | Business rollback/correction shall distinguish pre-commit cancellation, failed import, correction/retry, compensating action, controlled non-production reset and production correction; it shall never promise arbitrary historical deletion. |
| M40-REQ-041 | A migration completion result shall not activate a Tenant. Handover shall require readiness evidence, financial and operational reconciliation, required acknowledgements, unresolved-decision review and an explicit M27 activation decision. |
| M40-REQ-042 | Migration reporting shall expose run status, source/target scope, row outcomes, exception and reconciliation status, age/freshness, owner and approval without presenting unapproved values as production capacity or compliance commitments. |
| M40-REQ-043 | For each applicable Tenant-scoped opening domain, subsidiary opening positions shall reconcile to the corresponding GL control-account opening balances under the authoritative accounting configuration; where detailed subsidiary openings and a full Trial Balance represent the same economic positions, migration shall establish one economic opening effect with multiple reconcilable representations, and a material mismatch shall be a BLOCKING RECONCILIATION FAILURE that prevents approval/readiness until correction and re-validation. |

## 9. Release 1 migration domains

The following classification is the MESP-40 business boundary. “Required where
enabled” means the domain is required to make the corresponding approved
Release 1 capability operational, not that an unavailable capability may be
invented.

| Domain | Source business fields | Target business concept | Required? | Validation / duplicate key | Dependency / currency / UOM behavior | Error class | Release 1 status |
|---|---|---|---|---|---|---|---|
| Tenant identity | Legal/customer name, code, contacts, country, source ID | Tenant identity and catalogue entry | Yes | Unique code/legal identity; possible matches reviewed | Exactly one target Tenant; bilingual values preserved | DUPLICATE / TENANT SCOPE | REQUIRED FOR RELEASE 1 |
| Company / Legal Entity | Legal name, registration references, fiscal identity | Company/Legal Entity | Yes | Unique within Tenant; parent Tenant must match | Owns accounting boundary and functional currency | REFERENCE DATA / TENANT SCOPE | REQUIRED FOR RELEASE 1 |
| Branch | Code, name, parent Company, status | Branch | Required where supported | Unique within Company; active parent | Same Tenant; organization scope preserved | REFERENCE DATA | REQUIRED FOR RELEASE 1 |
| Warehouse | Code, name, parent Branch, status | Warehouse | Required for Inventory | Unique within Branch; active parent | Stock quantities and UOMs target this Warehouse | REFERENCE DATA | REQUIRED WHERE INVENTORY ENABLED |
| Locale and country-pack configuration | Language, locale, time zone, country, supported scripts | Tenant configuration | Yes | Supported configuration/version; no hidden default | Presentation only unless owning domain says otherwise | FILE/TEMPLATE / REFERENCE DATA | REQUIRED FOR RELEASE 1 |
| Calendar, periods and numbering | Fiscal calendar, opening date, numbering profile/version | Finance and document configuration | Yes | Effective date and non-overlap; policy validation | Accounting date must belong to valid Company period | REFERENCE DATA / FINANCIAL RECONCILIATION | REQUIRED FOR RELEASE 1 |
| Plan / Subscription / Entitlement | Approved plan/version, dates, modules, limits | Platform provisioning configuration | Yes | Effective version and environment eligibility | No entitlement override; no Retail POS | AUTHORIZATION / REFERENCE DATA | REQUIRED FOR RELEASE 1 |
| Chart of Accounts and dimensions | Source account code/name/type, dimension meaning, opening date | Finance account/dimension concepts | Yes for GL opening | Unique approved mapping; debit/credit tie-out | Functional currency and fiscal boundary apply | REFERENCE DATA / FINANCIAL RECONCILIATION | REQUIRED FOR GL |
| Currency | ISO/business code, name, precision reference, active state | Currency master/configuration | Yes for every used currency | Valid active code; no ambiguous code | Source and functional currency remain distinct | REFERENCE DATA / CURRENCY | REQUIRED FOR RELEASE 1 |
| Exchange-rate prerequisite | Source/target currency, effective date, rate/version/provenance | Finance rate evidence | Required for foreign-currency openings | Exact applicable effective rate; unavailable rate blocks dependent amount | No invented latest rate; functional-currency data needs no FX evidence | CURRENCY / FINANCIAL RECONCILIATION | REQUIRED WHERE MULTI-CURRENCY USED |
| Tax configuration/reference | Tax identity/classification/rule reference, effective date | Internal configuration-led tax concept | Required for tax-bearing data | Valid effective reference; no statutory inference | Saudi VAT configuration is not a ZATCA claim | REFERENCE DATA / FINANCIAL RECONCILIATION | REQUIRED WHERE TAX APPLIES |
| Payment Terms | Term identity, base-date meaning, interval/schedule reference | Master Data / Finance term concept | Required where AP/AR/Sales uses terms | Valid active term and effective date | Due-date meaning owned by Finance contract | REFERENCE DATA | REQUIRED WHERE USED |
| Categories | Category source ID, code, name, parent | Master Data category | Required where Product requires it | Parent integrity and duplicate key | No cross-Tenant category; bilingual values | REFERENCE DATA / DUPLICATE | REQUIRED WHERE PRODUCT MODEL REQUIRES |
| Units of Measure | UOM code/name, dimension, conversion reference | Master Data UOM | Yes for stock/product quantities | Valid active UOM and conversion policy | Quantity cannot be converted by guesswork | REFERENCE DATA / UOM | REQUIRED FOR INVENTORY/PRODUCTS |
| Products / Items | Source ID, code/SKU, names, category, base UOM, status, tracking references | Master Data Product/Item | Yes for Inventory/Procurement/Sales | Stable source/business key; active references | Preserve base/transaction UOM and tracking decision | DUPLICATE / REFERENCE DATA | REQUIRED FOR B2B ERP DOMAINS USING ITEMS |
| Suppliers | Source ID, business identity, contacts, tax/reference data, terms | Supplier master | Yes for Procurement/AP opening | Business identity duplicate review | Supplier is not a platform login; currency/terms validated | DUPLICATE / REFERENCE DATA | REQUIRED WHERE PROCUREMENT/AP USED |
| Business Customers | Source ID, legal/business identity, contacts, tax/reference data, terms | Business Customer master | Yes for Sales/AR opening | Business identity duplicate review | Customer is Tenant-scoped; currency/terms validated | DUPLICATE / REFERENCE DATA | REQUIRED WHERE SALES/AR USED |
| Price Lists | Price-list identity/version, Product, currency, effective interval | Commercial price reference | Required where Sales/Procurement uses price lists | Valid Product/currency/effective interval | Currency and UOM must match domain contract | REFERENCE DATA / CURRENCY | REQUIRED WHERE COMMERCIAL DOCUMENTS USE IT |
| Opening inventory quantity/value | Warehouse, Product/Item, UOM, quantity, cost/value, currency, date | Inventory opening movement/evidence | Yes where Inventory enabled | Exact quantity/value reconciliation; no direct balance bypass | Must enter through Inventory ledger; valuation handoff to Finance | INVENTORY RECONCILIATION | REQUIRED FOR INVENTORY |
| Opening GL balances | Account/dimension, debit/credit or signed balance, currency, date | Finance opening journals/balances | Yes where Finance enabled | Balanced trial balance; period/account validation | Finance owns posting meaning and evidence | FINANCIAL RECONCILIATION | REQUIRED FOR FINANCE |
| Opening AR balances | Customer, source document/reference, due date, amount, currency, date | AR opening subledger | Required where outstanding AR exists | Customer/reference/amount/currency tie-out | No fabricated due date or FX rate | FINANCIAL RECONCILIATION | REQUIRED WHERE AR OPENING EXISTS |
| Opening AP balances | Supplier, source document/reference, due date, amount, currency, date | AP opening subledger | Required where outstanding AP exists | Supplier/reference/amount/currency tie-out | No fabricated due date or FX rate | FINANCIAL RECONCILIATION | REQUIRED WHERE AP OPENING EXISTS |
| Opening cash/bank | Account, balance, currency, date, source statement/reference | Cash/bank opening balance | Required where supported and used | Account and source control-total tie-out | Finance owns linked account meaning | FINANCIAL RECONCILIATION | REQUIRED WHERE CASH/BANK USED |
| Open documents | PO/SO/quote/invoice/receipt/return references and current state | Existing operational documents | Open Product Decision | Scope, source state, references and ownership | No implementation contract until M40-DEC-001 | OPEN DECISION | OPEN DECISION |
| Historical transactions | Transaction identity, date, lines, status, financial/stock effects | Historical reporting/operational history | Open Product Decision | Source authority and period/history policy | No full-history assumption; no silent journal recreation | OPEN DECISION | OPEN DECISION |
| Attachments and source documents | Metadata, classification, source reference | Private file/document evidence | Optional | Permission, malware/privacy/provider policy | No storage provider or retention claim | UNSUPPORTED DATA / AUTHORIZATION | OPTIONAL / GATED |
| Users, memberships, roles and scopes | Source identity, normalized email, membership, role, organization scope | Identity and access assignments | Yes for tenant handover | Global identity duplicate; ambiguous mapping quarantined | Never grant broader authority than source/approved role | DUPLICATE / AUTHORIZATION | REQUIRED FOR CONTROLLED HANDOVER |

No physical table, column, database, file extension, parser, queue, endpoint or
storage provider is implied by these business contracts.

## 10. Template and source contract requirements

### 10.1 Common batch metadata

Every submission shall carry or be accompanied by:

| Business field | Requirement |
|---|---|
| Target Tenant | Server-selected Tenant; a supplied value is checked, never trusted |
| Batch/session identity | Stable identity for the business run and all attempts |
| Template/source contract version | Identifies field meanings and supported version |
| Source system and extract identity | Identifies source, snapshot/extract and scope |
| Source owner and target owner | Accountable business owners |
| Extraction date/time and opening date | Separates source snapshot time from target effective boundary |
| Domain and dependency order | Identifies domain, prerequisites and intended sequence |
| Language/script declaration | States Arabic, English or mixed content expectations |
| Currency/UOM basis | States currencies, quantity units and source precision meaning |
| Confidentiality classification | Determines preview, error, export and audit visibility |
| Requested operation | Validation, preview, dry run, execute, retry or correction |

### 10.2 Template semantics

- A supported template shall be versioned and published with field definitions,
  required/optional status, reference meanings, duplicate keys, valid lifecycle
  values, examples and rejection rules.
- The business contract shall support a structured tabular representation that
  preserves Unicode and explicit field boundaries. The exact file extension,
  transport and parser remain implementation decisions.
- If a delimited format is selected later, its encoding, delimiter, quoting,
  line-ending, header and escape rules must be explicit. UTF-8 is the required
  semantic baseline for Arabic/English preservation.
- Dates must have an unambiguous calendar and time-zone meaning. Display
  locale shall not change the business date or accounting date.
- Decimal quantities and amounts must not rely on thousands separators or
  locale-specific decimal interpretation. Domain precision and rounding are
  owned by the target domain and must be recorded where material.
- Currency codes and UOM codes must be explicit when a domain supports them.
  A missing or invalid code is not silently replaced by Tenant default.
- Required reference fields must identify the source key or approved target
  mapping. Free-text names alone are insufficient where identity matters.
- Source identifiers and template versions are retained for replay, duplicate
  detection, audit and reconciliation.

## 11. Validation and error classification

### 11.1 Validation stages

1. File/template contract validation: readable structure, version, encoding,
   headers, required columns and declared domain.
2. Record validation: mandatory values, data types, dates, amounts, quantities,
   currencies, UOMs, statuses and supported values.
3. Reference validation: Tenant, Company, Branch, Warehouse, account, Product,
   Customer, Supplier, Tax, Payment Term, Currency, rate and other dependencies.
4. Duplicate and mapping validation: source identifiers, business keys,
   possible matches, conflicting mappings and repeated batch attempts.
5. Scope and authority validation: target Tenant, organization boundary,
   actor, permission, lifecycle, module and approval.
6. Reconciliation validation: control totals, quantity/value tie-outs,
   debit/credit balance, currency basis and exception disposition.
7. Readiness validation: dry-run result, approved corrections, required
   rehearsal evidence, gate status and handover acknowledgements.

### 11.2 Error taxonomy

| Class | Meaning | Default severity and handling |
|---|---|---|
| FILE/TEMPLATE | Missing, malformed, unsupported or incompatible template/source contract | Blocking batch; no business effect |
| MANDATORY DATA | Required field absent or empty | Row-level reject unless it invalidates the batch contract |
| REFERENCE DATA | Missing, inactive, invalid or ambiguous dependency | Row-level reject or review-required quarantine; blocking for dependent domain |
| DUPLICATE | Repeated source/business key or possible existing target match | Reject or review-required quarantine; never silent merge |
| TENANT SCOPE | Cross-Tenant, foreign organization or unsafe ownership attempt | Blocking security outcome; no revealing foreign data |
| CURRENCY | Invalid currency, missing required rate, wrong effective rate or precision meaning | Row/domain blocking for dependent monetary data |
| UOM | Invalid unit, missing conversion or incompatible quantity basis | Row/domain blocking for dependent quantity data |
| FINANCIAL RECONCILIATION | Unbalanced GL, AR/AP/cash mismatch, unexplained amount or tax variance | Blocking execution completion and cutover |
| INVENTORY RECONCILIATION | Quantity, valuation, warehouse, UOM or tracking mismatch | Blocking inventory completion and cutover |
| AUTHORIZATION | Missing, stale, revoked or insufficient permission/approval/scope | Blocking action; evidence required |
| UNSUPPORTED DATA | Domain or field outside the accepted Release 1 contract | Row reject or decision-required quarantine |
| SYSTEM | Operational failure before effect can be proved | Failed or retryable only before effect boundary |
| OUTCOME UNKNOWN | Effect may have occurred but result is not provable | Reconciliation-required; no automatic duplicate retry |

Warnings may inform without blocking only when they do not hide a mandatory
field, scope failure, duplicate, financial/stock variance or unresolved
decision. Review-required outcomes remain non-operational until approved.

### 11.3 Error message requirements

Every row-level or batch-level error shall identify the safe source row/key,
domain, classification, business field or rule, plain-language reason, action
needed, owner, and whether correction/retry is allowed. Messages and review
summaries shall be readable in the active language; raw secrets, credentials,
foreign-Tenant content and untrusted exception text are excluded.

## 12. Preview, dry run and execution

### 12.1 Validation-only

Validation-only checks the source contract and data without creating or
changing Tenant business records. It returns a complete result summary and
row outcomes, including unsupported and quarantined records.

### 12.2 Preview

Preview presents the intended mapping, additions, updates if permitted by the
owning domain, duplicate outcomes, dependencies, control totals and expected
exceptions. Preview is informative and does not authorize effect.

### 12.3 Dry run

Dry run exercises the approved mapping and dependency order as a simulation
and produces planned execution and reconciliation evidence without
authoritative business effect. A dry run must be distinguishable from an
executed import and cannot be reported as a cutover.

### 12.4 Controlled execution

Execution requires:

- A passed validation and recorded preview/dry-run result.
- Current exact-Tenant authority and appropriate organization scope.
- Approved source mapping and exception decisions.
- Required domain-owner and reviewer approvals.
- A stable batch/session identity and declared attempt.
- An approved recovery/correction plan for the run.

Execution records the result of every domain and record. It may preserve
successful domain units when a later unit fails, but the overall batch remains
incomplete until all required units reconcile.

## 13. Atomicity, partial success, retry and idempotency

The business atomicity decision is explicit:

1. Validation, preview and dry run have zero authoritative business effect.
2. Each approved domain execution unit is atomic: it either commits its
   accepted records as one coherent unit or commits none of that unit's
   records.
3. Domain-level partial success is allowed across independent units only when
   completed units retain evidence and the overall batch remains
   Partially Completed or Reconciliation Pending.
4. A row-level rejection never becomes operational data. It is quarantined and
   counted.
5. A corrected retry uses the original batch identity plus a new attempt and
   processes only eligible unresolved/corrected records. Already accepted
   source identifiers are protected from duplicate effect.
6. A failure known to occur before the effect boundary may be retried under
   the recorded safe retry point.
7. An outcome that may have crossed the effect boundary is Outcome Unknown,
   requires reconciliation and is not automatically replayed.
8. A duplicate request returns or continues the original batch outcome rather
   than creating a second authoritative Tenant, opening balance, stock effect,
   journal, or other protected result.

This is a business contract, not a promise of distributed exactly-once
delivery or arbitrary database rollback.

## 14. Reconciliation, approval and activation gate

### 14.1 Required reconciliation measures

The reconciliation pack shall include applicable measures with source basis,
target basis, unit/currency, extract/opening date, result, variance and owner:

- Source record count.
- Accepted, rejected, duplicate, skipped and quarantined counts; outcomes are
  mutually exclusive and sum to the submitted scope.
- Master-data counts by domain and duplicate outcome.
- GL debit total, credit total, net balance and trial-balance variance.
- Inventory quantity by Warehouse, Product/Item and UOM.
- Inventory value by Warehouse, Product/Item and currency/cost basis.
- AR total by Customer, Company/Legal Entity and currency.
- AP total by Supplier, Company/Legal Entity and currency.
- Cash/bank total by account, Company/Legal Entity and currency.
- Cross-ledger opening-balance control totals, where applicable, using the
  authoritative Tenant accounting configuration and mapping:
  - AR subsidiary total = the corresponding AR GL control-account opening
    balance.
  - AP subsidiary total = the corresponding AP GL control-account opening
    balance.
  - Detailed cash/bank opening total = the corresponding GL cash/bank control
    account or accounts.
  - Inventory opening valuation = the corresponding Inventory GL control
    account or accounts where the accepted accounting model requires that
    reconciliation.
- If the authoritative accounting configuration groups control accounts by
  currency, Company/Legal Entity, Branch, customer or supplier grouping,
  Warehouse, or another mapping dimension, reconciliation follows that
  configuration; MESP-40 invents no grouping model.
- A full Trial Balance/control opening and subsidiary detail may both be
  represented only as reconcilable views of the same opening state. Loading
  subsidiary detail shall not create a second economic opening effect or
  double-count a value already represented in the GL control balance.
- Tax and exchange-rate reference completeness where used.
- Open-document or historical-document counts only if M40-DEC-001 selects
  them.
- Rejected and unresolved exception counts with disposition.

Financial and quantity variances must be zero or explicitly explained and
approved under the owning domain policy. MESP-40 invents no tolerance.
Rounding differences may be accepted only when the owning Finance/Inventory
contract defines the basis and the evidence preserves both source and target
meaning. A balancing journal or stock adjustment may not conceal a source
mapping or control-total defect.

A material mismatch between an applicable subsidiary opening and its
corresponding GL control-account opening balance is a **BLOCKING
RECONCILIATION FAILURE**. It prevents migration approval and readiness until
the source, mapping, or opening data is corrected and the reconciliation is
run again. Any exact tolerance or rounding treatment remains governed by the
existing authoritative accounting precision/rounding rules; this BRD does not
invent a tolerance.

### 14.2 Approval

The migration operator prepares the pack. The applicable Finance, Inventory,
Procurement, Sales and Tenant owners review their domains. A reviewer/approver
confirms the evidence under the effective approval and SoD policy. The
Platform onboarding owner confirms provisioning/readiness prerequisites.
Required acknowledgements are recorded; exact approver identities and any
policy-specific quorum remain M40-DEC-006.

### 14.3 Activation gate

Handover to M27 activation requires:

- Provisioning completed or safely compensated, with its own evidence.
- Tenant, organization, locale, country-pack, numbering, period, currency,
  tax/reference, Plan/Entitlement and module-readiness checks passed.
- Required master data and opening state reconciled.
- No unresolved blocking error, cross-Tenant mapping, unknown effect,
  material financial/stock variance or unapproved exception.
- Required dry runs and the cutover rehearsal recorded.
- Recovery/correction plan reviewed and production gate dependencies checked.
- Tenant Administrator acknowledgement and Platform activation approval.

MESP-40 completion creates eligibility evidence only; M27 owns the actual
Tenant lifecycle transition to Active.

## 15. Permissions and separation of duties

| Action | Minimum business authority | Additional control |
|---|---|---|
| Create onboarding draft | Platform onboarding authority | Commercial/platform prerequisites and duplicate review |
| Upload source/template | Migration operator within target Tenant run | Source ownership and exact scope |
| Validate / preview / dry run | Migration operator or authorized reviewer | Current Tenant/organization scope; no effect |
| Approve mapping/exceptions | Accountable domain owner | Ambiguous mappings remain quarantined until approval |
| Execute migration | Authorized migration/onboarding owner | Passed validation, approved run and current lifecycle/Entitlement |
| Approve reconciliation | Domain owner plus required reviewer | Operator self-approval prohibited where policy requires SoD |
| Retry/correct | Migration/onboarding owner | Linked batch/attempt; no duplicate accepted effect |
| Cancel | Authorized run owner before effect | Reason and cancellation evidence |
| View run/evidence | Authorized Tenant/domain/audit reviewer | Results are filtered to permitted Tenant and organization scope |
| Export evidence | Separate export authority | Support access alone is insufficient; export is audited |
| Activate Tenant | M27 Platform activation authority | Tenant acknowledgement and readiness snapshot |

The process must re-evaluate membership, Role, Permission, organization scope,
Tenant lifecycle, Entitlement and approval immediately before every material
action. No browser field, source row, support grant, background job or
integration may expand authority.

## 16. Auditability and sensitive data

### 16.1 Minimum migration evidence

For each batch and material action, retain:

- Event identity/category, occurrence time and effective time where distinct.
- Initiating actor, acting context, support/delegation/approval context where
  applicable.
- Target Tenant and applicable Company/Branch/Warehouse scope.
- Source system, extract identity/date, template or contract version, domain
  and source record identifier.
- Mapping version, duplicate outcome, validation result and error class.
- Batch/session identity, attempt identity, correlation and related evidence.
- Execution result, accepted/rejected/quarantined/duplicate counts, failure or
  unknown outcome and safe next action.
- Reconciliation pack, variance explanation, reviewer, approval and decision.
- Retry/re-import/correction links without deleting original history.
- Handover/readiness result and activation decision reference, if any.

### 16.2 Sensitive data handling

Migration should carry only fields needed for the approved business contract.
Personal contact data, credentials, payment details, private attachments,
support evidence and other restricted fields are access-controlled and
minimized. Preview and error views show safe identifiers or masked values.
Audit stores business evidence and change meaning, not raw secrets or full
source payloads. Authorized exports identify scope, source basis, filters,
generator, expiry and download evidence.

Audit is immutable at the business boundary. A correction, redaction,
retention, legal hold or governance action creates linked evidence and does
not erase the fact that the original attempt occurred. MESP-50 determines
retention, residency, legal hold, backup, restoration and purge commitments.

## 17. Multi-Tenant isolation

- The target Tenant is selected from server-owned authorized context.
- The source cannot override the target Tenant with a row field.
- A missing, malformed, conflicting or foreign Tenant/Company/Branch/Warehouse
  reference fails closed or is quarantined without revealing the foreign data.
- Mapping, preview, errors, evidence, reports, exports and notifications are
  filtered to the authorized Tenant and organization scope.
- A Platform Administrator or Support User does not receive Tenant business
  access merely because a Platform-owned record references that Tenant.
- Cross-Tenant duplicate search is not exposed to ordinary Tenant users. A
  privileged platform duplicate check returns only the minimum safe decision
  and is separately authorized and audited.
- Separate authorized Tenant contexts never combine records or working state.
- Suspension, reactivation and termination re-evaluate migration sessions,
  pending work, exports and access; interrupted work is not silently replayed.

## 18. Multi-currency and accounting controls

Migration shall preserve the distinction between:

- Transaction/source currency: the currency in which a source amount or
  transaction is expressed.
- Functional/base currency: the Company/Legal Entity accounting basis.
- Reporting Currency: an applicable reporting presentation basis under the
  approved Finance contract.
- Exchange-rate evidence: source/target pair, effective date, rate/version,
  provenance, precision and applied result.

For a functional-currency opening, no FX evidence is fabricated. For a
foreign-currency opening, the required active effective-dated rate and its
approved source/version evidence must exist before dependent financial effect.
An unavailable or mismatched rate blocks the dependent record or domain. No
browser-supplied, latest-available, inverse, reciprocal or external-feed
assumption is introduced by this BRD.

Cross-ledger reconciliation preserves these existing currency meanings. A
subsidiary balance is compared with its GL control balance according to the
Tenant's authoritative accounting configuration; this BRD invents no currency
translation, exchange-rate, tolerance or grouping policy.

Finance owns journal balance, period/account validation, source-to-GL meaning,
subledger reconciliation, tax effects, controlled correction/reversal and
posting evidence. Inventory owns quantity/valuation evidence and the Finance
handoff. Opening balances do not bypass the Inventory ledger or create
unexplained balancing effects.

## 19. Arabic, English, RTL and Saudi boundary

- Arabic and English text is preserved as Unicode source meaning, including
  bilingual names and descriptions where the target domain supports them.
- Import validation must not strip Arabic characters, transliterate them,
  reverse mixed-direction content or change punctuation/number meaning.
- User-facing errors, review summaries and readiness states shall be readable
  in the active language; RTL presentation is required where the surrounding
  product surface supports it.
- Locale controls date, time, decimal and display formatting only; stored
  business meaning is not changed by display language.
- Saudi Arabia is the first country pack and SAR/Asia-Riyadh/Arabic/English
  are initial configured defaults where accepted platform configuration
  applies. They are not Wafra-specific logic.
- Internal, configuration-led tax/VAT references may be migrated when the
  owning Finance/Tax contract requires them. This does not claim ZATCA,
  FATOORA, statutory compliance, submission, certification or legal advice.

## 20. Rollback, correction and business continuity

“Rollback” means a controlled business recovery decision with evidence. It
does not mean that any committed database state can be erased.

| Situation | Required business treatment |
|---|---|
| Pre-commit cancellation | Authorized owner cancels before authoritative effect; no business records are changed; cancellation is evidenced |
| Validation or dry-run failure | No business effect; correct source/mapping and rerun validation or dry run |
| Failed domain unit | Unit is not reported successful; completed independent units remain visible; failure, safe retry point and owner are recorded |
| Corrected retry | New linked attempt processes eligible corrections and protects accepted source identities from duplication |
| Outcome Unknown | Stop automatic replay; reconcile with owning domain and record the proven result or compensating decision |
| Non-production onboarding reset | Permitted only under an explicit controlled reset decision, bounded scope and retained evidence |
| Production correction | Use owning Finance/Inventory/domain correction, reversal or compensating action under authorization; never silent overwrite |
| Cutover recovery | Follow the approved rehearsal/business-continuity plan and named go/no-go authority; no MESP-40 claim of arbitrary database restoration |

Production cutover cannot proceed without a reviewed recovery plan,
backup/restoration evidence where required by MESP-50, and the applicable
specialist and SQL/provider gates.

## 21. Workflows

### WF-01 — Tenant onboarding preparation

| Element | Definition |
|---|---|
| Actor | Platform Administrator, Platform Operations Owner, Tenant Administrator |
| Entry criteria | Approved onboarding request; unique target; commercial/platform prerequisites; accountable owners |
| Steps | Create/review draft; run duplicate/completeness checks; establish Tenant identity; confirm organization, locale, currency, calendar, Plan/Entitlement, numbering/template and source owners; initiate provisioning; record migration scope |
| Validation | M27 required fields, exact Tenant context, parent hierarchy, effective configuration and unresolved M50 fields |
| Failure path | Remain Draft or Configuration Required; return missing/ambiguous inputs; do not create Active Tenant or usable production invitation |
| Exit criteria | Provisioning foundation and configuration prerequisites are ready; migration batch can be prepared |
| Evidence | Draft, duplicate result, input snapshot, provisioning run, owner assignments, configuration snapshot and notices |

### WF-02 — Migration validation / dry run

| Element | Definition |
|---|---|
| Actor | Migration Operator, domain owners, Tenant Administrator |
| Entry criteria | Target Tenant and source manifest approved; versioned template/source contract available |
| Steps | Upload/declare source; validate template; validate records/references/scope; resolve deterministic mappings; quarantine duplicates/ambiguity; produce preview; run validation-only and dry run; issue exception register |
| Validation | All validation stages in section 11; cross-ledger opening mappings and control totals are validated where applicable; no authoritative effect; reconciliation preview is labeled non-committing |
| Failure path | Batch is Validation Failed or quarantine; provide actionable errors and owner; correct and submit linked attempt |
| Exit criteria | Passed validation and dry run, or an explicitly documented decision that blocks execution |
| Evidence | Template/version, row outcomes, mapping decisions, preview, dry-run summary, exception register and correlation |

### WF-03 — Controlled migration execution

| Element | Definition |
|---|---|
| Actor | Authorized Migration/Onboarding Owner with domain approvers |
| Entry criteria | Passed validation/dry run; approved mappings; exact current authority; recovery plan; batch/session identity |
| Steps | Confirm target and dependencies; authorize execution; load in approved order; apply the cross-ledger opening contract; record each domain/row outcome; stop at failure or unknown boundary; produce execution summary |
| Validation | Domain atomicity, source-key duplicate protection, scope, current lifecycle, control-total and no-double-effect checks |
| Failure path | Quarantine/reject row; fail domain unit; preserve completed units; mark overall batch incomplete; unknown outcome requires reconciliation |
| Exit criteria | Execution Complete only when required units have no blocking errors; otherwise Partially Completed, Failed or Outcome Unknown |
| Evidence | Execution approval, attempt, domain/row results, counts, failures, unknown states, linked effects and operator |

### WF-04 — Failure correction / retry

| Element | Definition |
|---|---|
| Actor | Migration Operator, owning domain owner, reviewer |
| Entry criteria | Failed, rejected, quarantined or reconciled-unknown outcome with safe correction path |
| Steps | Review classification; correct source/mapping/configuration; approve correction; submit linked attempt; validate corrected records; retry only eligible records; compare to original results |
| Validation | Same target Tenant, same batch lineage, no duplicate accepted source identity, current authority, dependency readiness |
| Failure path | Keep record quarantined; escalate to decision owner; do not retry unknown effects automatically |
| Exit criteria | Corrected records accepted and reconciled, or exception remains explicitly open and blocks handover |
| Evidence | Original error, correction reason, attempt link, changed mapping/source version, retry result and reviewer |

### WF-05 — Reconciliation and approval

| Element | Definition |
|---|---|
| Actor | Migration Owner, Finance, Inventory, Procurement/Sales as applicable, Tenant Administrator, Reviewer |
| Entry criteria | Execution result available; all required domain summaries and source controls present |
| Steps | Reconcile row counts; master data; GL; stock quantity/value; AR/AP; cash/bank; applicable cross-ledger subsidiary-to-GL controls; tax/rates; applicable documents; classify variances; obtain owner review; record approval or rejection |
| Validation | Source/target basis, currency/UOM, dates, zero or explained variance, subsidiary-to-GL control reconciliation where applicable, no unresolved material error, BLOCKING RECONCILIATION FAILURE or unknown effect |
| Failure path | Reconciliation Pending; create correction/decision; reject readiness; never hide variance with an unexplained adjustment |
| Exit criteria | Reconciled and approved, or explicitly blocked with named owner and decision |
| Evidence | Reconciliation pack, control totals, variance register, approvals, exception disposition and final status |

### WF-06 — Readiness handoff

| Element | Definition |
|---|---|
| Actor | Platform Onboarding Owner, Tenant Administrator, Platform Administrator, domain approvers |
| Entry criteria | Provisioning and migration evidence complete; reconciled opening state; dry runs/rehearsal; recovery plan; gates reviewed |
| Steps | Assemble readiness snapshot; check Tenant/organization/locale/currency/period/Plan/Entitlement/module readiness; confirm required acknowledgements; record go/no-go recommendation; hand off to M27 activation decision |
| Validation | No blocking decision, gate, unresolved cross-Tenant outcome, financial/stock or subsidiary-to-GL variance, unsupported effect or missing approval |
| Failure path | Return to Configuration Required or Migration Correction; keep Tenant non-Active |
| Exit criteria | Ready for Activation evidence is accepted by responsible reviewers; activation remains a separate authorized transition |
| Evidence | Readiness checklist, reconciliation approval, gate disposition, acknowledgements, handoff and activation decision reference |

## 22. Reports, KPIs and notifications

MESP-40 requires business-readable reports, not a new reporting implementation:

| Report/evidence view | Minimum content |
|---|---|
| Onboarding readiness view | Tenant, lifecycle, provisioning stage, configuration gaps, owners, migration state and next action |
| Validation/preview report | Batch/domain, template/source version, source scope, accepted/rejected/duplicate/quarantined counts, errors and mapping status |
| Execution summary | Attempt, start/end/effective time, completed units, row outcomes, failed/unknown units, retry identity and next action |
| Reconciliation pack | Source/target totals, currency/UOM basis, variance, explanation, domain owner, reviewer and approval |
| Exception register | Safe source key, error class, message, owner, state, correction/retry link and blocking effect |
| Readiness/handover pack | Required setup, domain approvals, dry-run/rehearsal evidence, open decisions, gates, acknowledgements and go/no-go |

KPIs shall include counts and states with measurement time, freshness, scope
and source. They shall not be presented as supported production capacity,
SLA, financial performance or compliance certification. Notifications shall be
Tenant-scoped, bilingual where supported, safe for sensitive data and
auditable for material failures, approvals, reconciliation and handover.

## 23. MESP-40 → MESP-141 implementation contract

MESP-141 may rely on this contract only after MESP-40 is independently
accepted and the remaining blocking decisions/gates are resolved or explicitly
bounded by Sol/Owner.

### 23.1 Stable scope MESP-141 may consume

- M27 provisioning and lifecycle vocabulary, with provisioning separate from
  migration and Active gated separately.
- Exactly one server-authorized target Tenant per business migration run.
- Configuration and reference/master data before dependent opening state.
- Required Release 1 domains in section 9, with conditional behavior only where
  an owning capability is enabled.
- Versioned source/template/mapping/batch/attempt identity and row outcomes.
- Validation-only, preview, dry run, controlled execution, quarantine,
  domain-level atomicity, partial-success visibility, safe retry and
  unknown-outcome reconciliation.
- Finance-owned GL/AP/AR/cash/tax/currency controls and Inventory-owned
  quantity/valuation/ledger controls.
- The cross-ledger opening contract: applicable AR, AP, cash/bank and Inventory
  subsidiary openings reconcile to their corresponding GL control-account
  openings under Tenant accounting configuration, while a full Trial Balance
  and subsidiary detail remain one economic opening effect rather than two.
- Immutable, Tenant-scoped evidence and actionable error classification.
- Arabic/English Unicode preservation and RTL/LTR presentation consequences.
- Reconciliation pack, named owners/reviewers and separate M27 activation gate.

### 23.2 MESP-141 must not infer

- Historical transactions or open documents without M40-DEC-001.
- Production volume, concurrency, performance, storage, retention, residency,
  backup, restoration, legal or compliance values.
- A parser, file extension, queue, storage provider, API shape, database
  schema, UI component or cloud service from this BRD.
- Cross-Tenant search, source-owned Tenant authority, support export authority,
  automatic FX, silent defaults, direct inventory balance changes or
  arbitrary rollback.
- Generic implementation assumptions about production volume, residency,
  retention, recovery, legal handling or other unresolved production policy.

### 23.3 MESP-141 contract gate partition

The following three gate types are distinct. A prerequisite in one category is
not silently promoted into another category.

#### A. Generic MESP-141 implementation-activation prerequisites

These answer **whether generic MESP-141 implementation may begin**. Before
activation, Sol must confirm MESP-40 independent acceptance and resolve or
explicitly bound the generic contracts represented by:

- M40-DEC-001, the historical/open-document boundary.
- M40-DEC-003, the implementation file/transport and operational-volume
  contract.
- M40-DEC-006, the exact approval, quorum and SoD contract where the owning
  policy has not already named it.
- Any other unresolved generic scope or safety contract that would otherwise
  make implementation behavior ambiguous.

These blockers define what engineering may safely implement generically. They
do not require actual production Tenant extracts, production volume evidence,
or unresolved residency/retention policy values to be invented early.

#### B. Per-Tenant onboarding and cutover prerequisites

These answer **whether a specific Tenant can execute this migration or
cutover**. They are onboarding inputs and evidence, not automatic generic
MESP-141 implementation blockers:

- Actual source systems, extracts, source owners, crosswalks and opening or
  cutover date under M40-DEC-002.
- Tenant-specific sensitive-field classification and support/export treatment
  under M40-DEC-004.
- Named Tenant, domain, reviewer and operational recovery authorities, with
  the approved policy/quorum applied to that Tenant.
- The actual domain scope, accounting mappings, source controls, rehearsal and
  reconciliation evidence for the Tenant.
- Production-specific volume evidence, hosting/data-governance inputs and
  recovery evidence when the Tenant is seeking a production cutover.

#### C. Production and release gates

These answer **whether the platform or Tenant may claim production readiness
under the unresolved gate**. MESP-48 and MESP-50 remain open production gates,
including their performance/capacity, residency, retention, privacy,
legal-hold, purge, backup/restore and related production-policy evidence.
They do not automatically block MESP-40 requirements acceptance, a
policy-neutral architecture/design, or bounded non-production implementation.
However, any MESP-141 implementation decision that would hard-code unresolved
performance, residency, retention, recovery or production-policy assumptions
must remain deferred or configurable until the relevant gate is resolved.

Named Finance, Inventory, Migration, Security/Audit and SQL/provider
validation remains required before destructive or production action, whether
the underlying need is generic implementation safety or a Tenant-specific
cutover prerequisite.

MESP-141 remains Open / Not Activated. This document does not activate it,
move it to In Progress, create its branch or authorize its implementation.

## 24. MESP-142 compatibility

MESP-142 may later consume MESP-141's executable migration and reconciliation
evidence as part of Release 1 stabilization. It does not change MESP-40
business rules, does not authorize production cutover and remains Open /
Not Activated. Performance, UAT, provider, deployment, legal, production and
release-candidate validation remain its later scope.

## 25. MESP-48 and MESP-50 gates

| Gate | MESP-40 requirement consequence | Not decided here |
|---|---|---|
| MESP-48 — reference tenant volume assumptions | Record measured run volume, duration, freshness, scope and source; do not publish capacity promises before evidence | Tenant counts, transaction volumes, lines, files, storage, concurrency, thresholds, performance, SLA and capacity |
| MESP-50 — residency, retention and data governance | Keep data/evidence Tenant-scoped, access-controlled, auditable and non-purgeable by this BRD alone; capture required hosting/support inputs during provisioning | Region, residency, retention duration, legal basis, subprocessors, backup/DR location/lifecycle, restoration promise, purge method, legal hold and compliance certification |

These gates remain OPEN. No MESP-40 statement closes or substitutes for them.
They are production/release gates, not generic MESP-141 activation blockers by
default. They may still block a particular Tenant's production cutover or any
implementation decision that would hard-code an unresolved gate assumption.

## 26. Open decisions register

| ID | Question | Options to decide | Impact | Owner | Blocking classification | Related issue/gate |
|---|---|---|---|---|---|---|
| M40-DEC-001 | Will Release 1 start from controlled opening state only, or also migrate open documents and/or historical transactions? | A. Configuration, masters and opening state only (recommended by the current bounded scope); B. Add selected open PO/SO/invoice/receipt documents; C. Add a defined historical period; D. Full history | Changes domains, templates, lineage, reconciliation, reports, cutover, correction and MESP-141 acceptance | Product Owner with Tenant business owner, Finance, Inventory, Procurement and Sales | B — Does not block MESP-40 acceptance but blocks MESP-141 activation | MESP-51/#140, MESP-141/#229, MESP-23 |
| M40-DEC-002 | What source systems, extracts, owners and opening date apply to each production Tenant? | One source system; multiple source systems with crosswalk; source-specific onboarding pack | Determines cleansing, mappings, evidence, responsibility and cutover timing | Tenant business owner and Migration Owner | C — Non-blocking to generic implementation; required per production onboarding | MESP-51/#140 |
| M40-DEC-003 | What file/transport profile and operational volume envelope will the implementation support? | One structured tabular profile; multiple versioned profiles; approved integration profile later | Determines operator workflow, limits, sizing and support evidence | Product/Architecture/Operations with MESP-48 evidence | B — Blocks MESP-141 activation | MESP-48/#137, MESP-141/#229 |
| M40-DEC-004 | Which source fields are sensitive/restricted for each Tenant and what export/support policy applies? | Standard classification; Tenant-specific contractual classification; stricter country/legal profile | Determines masking, reviewer access, exports and incident handling | Security/Privacy owner with Tenant owner | C — May be deferred, but blocks affected production data | MESP-50/#139, MESP-38 |
| M40-DEC-005 | What is the approved production recovery/correction authority after a cutover effect? | Domain compensating correction/reversal; controlled non-production reset only; separately approved production restoration path | Determines go/no-go, business continuity, unknown outcomes and correction evidence; no arbitrary database rollback | Product Owner with Finance, Inventory, Operations and qualified provider/backup owners | C — May be deferred; blocks destructive production cutover | MESP-51/#140, MESP-50/#139 |
| M40-DEC-006 | What exact approval quorum and SoD exceptions apply to migration, reconciliation and handover? | Named independent reviewer; domain-owner plus Platform/Tenant acknowledgements; policy-specific delegated approval | Determines who may approve, self-approval handling and readiness evidence | Product Owner with IAM, Finance, Inventory and Security/Audit | B — Blocks MESP-141 activation | MESP-27/#116, MESP-28/#117, MESP-38/#127 |

No open decision is silently treated as a default. A decision owner must record
the selected option, rejected alternatives, rationale, affected IDs, effective
date, evidence and implementation/production consequence in the living
decision register before the affected behavior is claimed.

## 27. Acceptance criteria

These are implementation-neutral, testable business acceptance criteria.

### Tenant, provisioning and isolation

| ID | Given / When / Then |
|---|---|
| M40-AC-001 | Given a complete unique onboarding request, when preparation is started, then one target Tenant, accountable owners, required configuration and a reviewable provisioning run are recorded without activating the Tenant. |
| M40-AC-002 | Given incomplete, duplicate or ambiguous Tenant/organization inputs, when validation runs, then provisioning remains non-operational and the issue identifies the missing or conflicting decision. |
| M40-AC-003 | Given a migration row that names another Tenant or foreign Company/Branch/Warehouse, when it is validated, then it is rejected or quarantined as TENANT SCOPE without revealing the foreign record. |
| M40-AC-004 | Given two separately authorized Tenant contexts, when evidence or migration data is requested in one context, then no data, error, report or working state from the other context is returned. |
| M40-AC-005 | Given a provisioning run that fails part-way, when an operator retries it, then the same run identity continues or safely compensates and cannot create a second Tenant, invitation or active module. |

### Templates, mappings and validation

| ID | Given / When / Then |
|---|---|
| M40-AC-006 | Given a versioned valid template with declared Tenant, domain, source, encoding and dependencies, when validation runs, then the result identifies the template/version and produces row/domain outcomes without business effect. |
| M40-AC-007 | Given a malformed, unsupported or incompatible template, when it is uploaded, then the batch is blocked as FILE/TEMPLATE with an actionable message and no row is imported. |
| M40-AC-008 | Given a record missing a mandatory field, when validation runs, then the record is a MANDATORY DATA reject or the batch is blocked when the missing field makes the contract unsafe. |
| M40-AC-009 | Given repeated source identifiers, duplicate business keys or possible target matches, when validation runs, then each outcome is counted and rejected or quarantined; no silent merge or overwrite occurs. |
| M40-AC-010 | Given a Product, Customer, Supplier, account, parent organization, Currency, Tax, Payment Term or UOM reference that is missing or inactive, when validation runs, then the dependent record cannot become operational and the error identifies the dependency. |
| M40-AC-011 | Given a source contract containing Arabic, English and mixed-direction text, when it is validated and previewed, then the original Unicode meaning is preserved and the review output remains readable in supported EN/AR/RTL views. |
| M40-AC-012 | Given a foreign-currency amount without an exact valid effective rate, when validation runs, then the dependent monetary record is blocked as CURRENCY and no rate is invented. |
| M40-AC-013 | Given a quantity with an invalid or missing UOM/conversion, when validation runs, then the dependent inventory record is blocked as UOM and no conversion is guessed. |

### Preview, dry run and execution safety

| ID | Given / When / Then |
|---|---|
| M40-AC-014 | Given a valid source, when validation-only is requested, then no Tenant business record, journal, stock effect or operational document changes and the result is clearly labeled validation-only. |
| M40-AC-015 | Given a valid mapping, when preview is requested, then expected additions, duplicate outcomes, dependencies, control totals and exceptions are visible without authorizing effect. |
| M40-AC-016 | Given an approved source and mapping, when dry run is executed, then it produces planned execution/reconciliation evidence and leaves authoritative business state unchanged. |
| M40-AC-017 | Given passed validation, approved mappings, current authority and recovery evidence, when execution is authorized, then records are processed in dependency order and every row/domain has an outcome. |
| M40-AC-018 | Given the same batch/session is submitted twice, when the second request is processed, then it returns or continues the original outcome and cannot duplicate a Tenant, journal, stock effect or other protected result. |
| M40-AC-019 | Given concurrent requests for the same source/batch identity, when they are processed, then only one authoritative execution outcome is accepted and the other is treated as a duplicate or safe continuation. |
| M40-AC-020 | Given one independent domain fails after another domain completed, when the batch summary is produced, then completed units remain visible and reconciled, the overall batch is incomplete, and no false Ready-for-Handover result is issued. |
| M40-AC-021 | Given a quarantined record is corrected, when a linked retry is submitted, then only eligible corrected data is processed, accepted source identifiers remain protected, and original rejection history remains visible. |
| M40-AC-022 | Given an effect may have occurred but its result cannot be proved, when the run detects the uncertainty, then it enters Outcome Unknown, automatic replay stops and reconciliation is required. |

### Reconciliation and financial/stock integrity

| ID | Given / When / Then |
|---|---|
| M40-AC-023 | Given a GL opening extract, when reconciliation runs, then debit and credit totals, accounting date, Company/Legal Entity, account/dimension mapping and source control total are shown and an imbalance blocks readiness. |
| M40-AC-024 | Given an inventory opening extract, when reconciliation runs, then quantity by Warehouse/Product/UOM and value by currency/cost basis tie to source evidence; a mismatch blocks inventory readiness. |
| M40-AC-025 | Given AR, AP or cash/bank opening data, when reconciliation runs, then totals by party/account and currency tie to source controls and the applicable GL control-account balance, and unexplained variance blocks handover. |
| M40-AC-026 | Given a permitted rounding difference under an owning Finance/Inventory policy, when reconciliation runs, then source value, target value, rounding basis and approval are visible; an unexplained variance is not hidden. |
| M40-AC-027 | Given a scope that includes no historical/open documents under the current decision, when reconciliation runs, then no historical/document count is presented as migrated and the opening boundary is explicit. |
| M40-AC-037 | Given a Tenant-scoped opening package contains a full Trial Balance with applicable AR/AP/cash/bank/Inventory control balances and subsidiary opening records for the same economic positions, when validation or execution occurs, then the applicable subsidiary totals reconcile to the GL control balances using authoritative accounting mappings, one economic opening effect is established with no duplicate posting or double counting from subsidiary detail, and any material mismatch is a BLOCKING RECONCILIATION FAILURE that blocks approval/readiness until correction and re-validation. |

### Authorization, audit and sensitive data

| ID | Given / When / Then |
|---|---|
| M40-AC-028 | Given a caller without current exact-Tenant upload/validation authority, when the action is attempted, then it is denied and the result does not disclose foreign or sensitive source data. |
| M40-AC-029 | Given a caller with validation permission but no execution/approval authority, when execute is attempted, then it is denied; validation evidence remains read-only and Tenant-scoped. |
| M40-AC-030 | Given an operator who prepared a material migration, when the operator attempts prohibited self-approval, then the applicable SoD policy blocks it or records the approved exception explicitly. |
| M40-AC-031 | Given a completed, failed, rejected, retried or reconciled batch, when an authorized reviewer views evidence, then actor, Tenant, source/template, batch, timestamps, outcomes, errors, approvals and related attempts are reconstructable. |
| M40-AC-032 | Given sensitive source fields, when preview, error, audit or export is viewed, then values are minimized or masked according to authority and no raw secret or unrelated Tenant data is exposed. |

### Correction, readiness and gates

| ID | Given / When / Then |
|---|---|
| M40-AC-033 | Given a pre-commit run, when an authorized owner cancels it, then no authoritative business effect is reported and the cancellation reason is retained. |
| M40-AC-034 | Given a failed or unknown outcome, when correction or recovery is selected, then the business path distinguishes correction/retry, reconciliation, compensating action, non-production reset and production recovery without promising arbitrary database rollback. |
| M40-AC-035 | Given all required setup and domain reconciliations pass but MESP-48 or MESP-50 remains open, when readiness is reviewed, then the applicable production/cutover claim remains blocked and the open gate is visible. |
| M40-AC-036 | Given a reconciled opening state and required acknowledgements, when handover is completed, then the Tenant reaches Ready for Activation evidence only; Active requires the separate authorized M27 activation decision. |

## 28. QA and testability readiness

This section defines business-risk coverage for later implementation QA. It
does not create automated tests or prescribe a test framework.

| Risk | Required coverage | Acceptance criteria |
|---|---|---|
| Positive onboarding | Complete request, provisioning/configuration, valid source, validation-only, preview, dry run, reconciliation and handoff | M40-AC-001, 006, 014, 015, 016, 017, 023–025, 036 |
| Validation failures | Malformed template, missing fields, invalid data, unsupported domain | M40-AC-007, 008, 010, 027 |
| Duplicate imports | Repeated source key, business duplicate, repeated batch, concurrent submission and duplicate representation of opening balances | M40-AC-009, 018, 019, 037 |
| Partial/batch failure | One domain succeeds and another fails; row reject/quarantine remains visible | M40-AC-020, 021 |
| Retry | Corrected retry, safe pre-effect retry, accepted-record protection | M40-AC-005, 018, 021 |
| Unknown outcome | Effect uncertainty stops replay and creates reconciliation work | M40-AC-022, 034 |
| Cross-Tenant attempts | Foreign Tenant/organization reference and evidence lookup | M40-AC-003, 004 |
| Unauthorized actions | Upload, validate, execute, approve, retry, cancel and evidence access | M40-AC-028–030 |
| Invalid currency/rate | Foreign amount, wrong effective rate, missing rate, functional-currency no-FX case | M40-AC-012, 026 |
| Invalid references | Customer, Supplier, Product, account, Tax, Term, UOM and organization parent | M40-AC-010, 013 |
| Bilingual/RTL data | Unicode preservation, Arabic/English fields, mixed direction, readable errors | M40-AC-011 |
| Financial imbalance | GL debit/credit, AR/AP/cash controls, subsidiary-to-GL reconciliation, currency basis and rounding evidence | M40-AC-023, 025, 026, 037 |
| Inventory mismatch | Warehouse/Product/UOM quantity and valuation tie-out; ledger boundary | M40-AC-024 |
| Audit evidence | Initiation through correction, approval, reconciliation and handover | M40-AC-031 |
| Sensitive data | Masking, least privilege, safe errors and export boundaries | M40-AC-032 |
| Concurrency/idempotency | Same batch submitted concurrently and after interruption | M40-AC-018, 019, 022 |
| Activation leakage | Migration completion cannot activate an incomplete or gated Tenant | M40-AC-002, 020, 035, 036 |
| Rollback/correction | Cancellation, failed unit, compensating action and production recovery boundary | M40-AC-033, 034 |

## 29. Traceability matrix

The matrix connects every M40 requirement to at least one rule, workflow,
acceptance criterion and dependency/decision. Rule and workflow IDs are
defined below or in section 21.

| Requirement | Business rule(s) | Workflow(s) | Acceptance | Dependency / decision |
|---|---|---|---|---|
| M40-REQ-001 | M40-RULE-001, 003 | WF-01, WF-06 | M40-AC-001, 036 | B1/PD-041; MESP-15 |
| M40-REQ-002 | M40-RULE-002 | WF-01, WF-03 | M40-AC-003, 004 | MESP-29; M40-DEC-002 |
| M40-REQ-003 | M40-RULE-002, 007 | WF-02, WF-03 | M40-AC-003 | MESP-13/29; ADR-019 |
| M40-REQ-004 | M40-RULE-004 | WF-01, WF-06 | M40-AC-001, 036 | M27 / MESP-27 |
| M40-REQ-005 | M40-RULE-005, 006 | WF-01, WF-06 | M40-AC-002, 036 | M27 lifecycle |
| M40-REQ-006 | M40-RULE-005, 006, 013, 028 | WF-01, WF-03, WF-05 | M40-AC-001, 005, 020, 031 | M27 provisioning run |
| M40-REQ-007 | M40-RULE-027 | WF-01, WF-05, WF-06 | M40-AC-030, 036 | M27/MESP-28/MESP-38 |
| M40-REQ-008 | M40-RULE-003, 007 | WF-01 | M40-AC-001, 002 | M27 / MESP-29 |
| M40-REQ-009 | M40-RULE-007, 008 | WF-01, WF-02 | M40-AC-002, 003 | MESP-30 |
| M40-REQ-010 | M40-RULE-009 | WF-01 | M40-AC-001, 002 | MESP-30/34/37 |
| M40-REQ-011 | M40-RULE-009, 023, 024 | WF-01, WF-02 | M40-AC-010, 012 | PD-043/044; MESP-54 |
| M40-REQ-012 | M40-RULE-005, 006 | WF-01, WF-06 | M40-AC-001, 036 | M27 Plan/Entitlement |
| M40-REQ-013 | M40-RULE-010 | WF-01, WF-02 | M40-AC-006 | M40-DEC-002 |
| M40-REQ-014 | M40-RULE-010, 011 | WF-02, WF-04 | M40-AC-006, 021 | BR-013/ADM-003 |
| M40-REQ-015 | M40-RULE-012 | WF-02 | M40-AC-006, 007 | ADM-003; M40-DEC-003 |
| M40-REQ-016 | M40-RULE-011, 013 | WF-02, WF-03, WF-04 | M40-AC-009, 018, 021 | MD-BR-005; M40-DEC-002 |
| M40-REQ-017 | M40-RULE-012, 025 | WF-02 | M40-AC-011, 012 | MESP-28; MESP-37; MESP-50 |
| M40-REQ-018 | M40-RULE-014 | WF-02, WF-03 | M40-AC-010, 017 | PRD section 19.1; domain BRDs |
| M40-REQ-019 | M40-RULE-014, 015 | WF-02, WF-03 | M40-AC-010, 017 | Master Data BRD; PD-041 |
| M40-REQ-020 | M40-RULE-019, 020, 031 | WF-03, WF-05 | M40-AC-023, 037 | Finance BRD; PD-044 |
| M40-REQ-021 | M40-RULE-019, 021, 022, 031 | WF-03, WF-05 | M40-AC-024, 037 | Inventory BRD; PD-046 |
| M40-REQ-022 | M40-RULE-019, 020, 023, 031 | WF-03, WF-05 | M40-AC-025, 037 | Finance BRD; PD-044 |
| M40-REQ-023 | M40-RULE-019, 020, 023, 031 | WF-03, WF-05 | M40-AC-025, 037 | Finance BRD; PD-041 |
| M40-REQ-024 | M40-RULE-014, 023, 024 | WF-02, WF-03 | M40-AC-010, 012, 013 | Master Data/Finance BRDs |
| M40-REQ-025 | M40-RULE-016 | WF-02, WF-06 | M40-AC-027, 035 | M40-DEC-001; MESP-141 |
| M40-REQ-026 | M40-RULE-017, 018 | WF-02, WF-05 | M40-AC-007–013, 023–026 | BR-013/ADM-003 |
| M40-REQ-027 | M40-RULE-018 | WF-02, WF-05 | M40-AC-014–016 | ADR-007 boundary; PD-041 |
| M40-REQ-028 | M40-RULE-027 | WF-02, WF-03, WF-04 | M40-AC-028–030 | MESP-28/29/38 |
| M40-REQ-029 | M40-RULE-013, 014 | WF-03, WF-04 | M40-AC-018, 019, 021 | PD-041; MESP-141 |
| M40-REQ-030 | M40-RULE-015, 016 | WF-03, WF-04 | M40-AC-020, 022 | M40 atomicity decision; PD-041 |
| M40-REQ-031 | M40-RULE-017 | WF-02, WF-03, WF-04 | M40-AC-008–010, 020, 021 | Security/Audit BRD |
| M40-REQ-032 | M40-RULE-013, 015 | WF-04 | M40-AC-021, 022 | PD-041; M40-DEC-005 |
| M40-REQ-033 | M40-RULE-019–022 | WF-05 | M40-AC-023–027 | Finance/Inventory BRDs |
| M40-REQ-034 | M40-RULE-020, 027 | WF-05, WF-06 | M40-AC-026, 031, 036 | M40-DEC-006 |
| M40-REQ-035 | M40-RULE-028, 029 | WF-02–WF-06 | M40-AC-031 | Security/Audit; MESP-50 |
| M40-REQ-036 | M40-RULE-002, 007, 030 | WF-02, WF-03 | M40-AC-003, 004 | MESP-29; MESP-50 |
| M40-REQ-037 | M40-RULE-023, 024 | WF-02, WF-03, WF-05 | M40-AC-012, 025, 026 | PD-043; MESP-54 |
| M40-REQ-038 | M40-RULE-025, 026 | WF-02, WF-05 | M40-AC-011 | Saudi Localization BRD |
| M40-REQ-039 | M40-RULE-029, 030 | WF-02, WF-05 | M40-AC-028, 032 | M40-DEC-004; MESP-50; Security/Audit |
| M40-REQ-040 | M40-RULE-015, 016, 028 | WF-04, WF-06 | M40-AC-033, 034 | M40-DEC-005; PD-041 |
| M40-REQ-041 | M40-RULE-005, 006, 030 | WF-05, WF-06 | M40-AC-035, 036 | M27 activation; MESP-48/50 |
| M40-REQ-042 | M40-RULE-028, 030 | WF-05, WF-06 | M40-AC-031, 035, 036 | MESP-48/50; MESP-53 |
| M40-REQ-043 | M40-RULE-019, 031 | WF-02, WF-03, WF-05, WF-06 | M40-AC-037 | Finance/Inventory BRDs; MESP-33/#122; MESP-34/#123; MESP-51/#140 |

### 29.1 Business rules register

| ID | Binding rule |
|---|---|
| M40-RULE-001 | Migration exists to establish a reconciled Tenant opening state, not to create an unbounded generic ETL facility. |
| M40-RULE-002 | One migration business run has one target Tenant and one authorized context; source data cannot broaden it. |
| M40-RULE-003 | Wafra is validation Tenant #1 only; no Wafra value becomes reusable product behavior without an approved generic decision. |
| M40-RULE-004 | Provisioning and migration have separate business owners, statuses, evidence and failure outcomes. |
| M40-RULE-005 | M27 lifecycle and activation rules apply; no migration result alone makes a Tenant Active. |
| M40-RULE-006 | Partial provisioning or unresolved migration cannot create an operational module, usable production access or Active Tenant. |
| M40-RULE-007 | Tenant and organization ownership are server-authoritative; foreign or ambiguous relationships fail closed or remain quarantined. |
| M40-RULE-008 | A used organization parent cannot be silently rewritten; controlled migration or closure/recreation preserves history. |
| M40-RULE-009 | Effective locale, time zone, accounting period, numbering, currency, tax and template references must exist before dependent data. |
| M40-RULE-010 | Source owner, target owner, extract identity/date and scope are required for every domain. |
| M40-RULE-011 | Stable source identifiers and approved duplicate keys protect identity and history; target identifiers from source are not trusted authority. |
| M40-RULE-012 | Template version, encoding, field meaning, date, decimal, currency, UOM and bilingual semantics must be explicit. |
| M40-RULE-013 | A retry reuses the original batch or provisioning-run lineage and cannot duplicate an accepted authoritative effect. |
| M40-RULE-014 | Configuration and master/reference data precede dependent openings or documents. |
| M40-RULE-015 | Each domain execution unit is atomic; independent completed units remain visible when another unit fails. |
| M40-RULE-016 | Failed, rejected, unsupported or ambiguous records never become operational data; correction creates linked evidence. |
| M40-RULE-017 | Validation, preview and dry run have no authoritative business effect. |
| M40-RULE-018 | A warning cannot conceal a blocking scope, duplicate, unsupported, financial, inventory or unresolved-decision outcome. |
| M40-RULE-019 | Financial and inventory control totals are reconciled to source basis and, where applicable, subsidiary opening totals are reconciled to corresponding GL control-account balances under the authoritative Tenant accounting mapping; an unexplained or material mismatch is a BLOCKING RECONCILIATION FAILURE that blocks completion and readiness. |
| M40-RULE-020 | Finance owns GL/AP/AR/cash/tax/period and monetary posting meaning; no operational module fabricates accounting truth. |
| M40-RULE-021 | Inventory opening quantity/value enters through the Inventory ledger and retains valuation evidence. |
| M40-RULE-022 | Quantity and valuation use explicit Product/Item, Warehouse, UOM, date, cost and currency meaning. |
| M40-RULE-023 | Foreign-currency amounts require an exact approved effective-rate reference; functional-currency amounts do not receive fabricated FX evidence. |
| M40-RULE-024 | Missing, invalid, stale or mismatched currency/rate evidence blocks dependent monetary data. |
| M40-RULE-025 | Arabic and English source text is preserved as Unicode; presentation locale never changes business meaning. |
| M40-RULE-026 | Saudi country-pack and SAR defaults are generic configuration and make no ZATCA, legal or compliance claim. |
| M40-RULE-027 | Every material action requires current server-side identity, membership, permission, lifecycle, scope and approval checks; no new superuser is implied. |
| M40-RULE-028 | Material attempts and outcomes, including denied, rejected, failed, unknown, retried and reconciled states, are auditable and linked. |
| M40-RULE-029 | Sensitive data is minimized and masked in errors, previews, evidence and exports; support access is not export authority. |
| M40-RULE-030 | MESP-48, MESP-50 and unresolved M40 decisions remain visible blockers for the claims or operations they govern. |
| M40-RULE-031 | A full Trial Balance/control opening and applicable subsidiary opening detail that represent the same positions are multiple reconcilable representations of ONE ECONOMIC OPENING EFFECT; loading both shall not create a second posting or double-count the opening value, and the Finance-owned accounting model governs how the representations are recorded. |

## 30. Assumptions and dependency register

### 30.1 Assumptions used

- M27 Tenant Catalogue, Create Tenant draft workflow, provisioning run,
  lifecycle, Plan/Entitlement and activation concepts are accepted and remain
  authoritative.
- Current Finance, Inventory, Master Data, Procurement, Sales, Localization
  and Security/Audit BRDs own their domain meanings.
- B1/PD-041, B3/PD-043, B4/PD-044 and B5/PD-046 are contract-bound and still
  require the named specialist/production conditions.
- No production migration is performed by this requirements session.
- “Where supported/enabled” is a capability boundary, not permission to create
  a missing product capability.

### 30.2 Dependency register

| Dependency | Effect |
|---|---|
| MESP-15 / GitHub #104 | Parent Epic owns migration/onboarding sequence and cutover boundary |
| MESP-27 / GitHub #116 | Provisioning, Tenant lifecycle, Entitlement, module readiness and activation |
| MESP-28 / GitHub #117 | Identity, membership, roles, scopes and approval authority |
| MESP-29 / GitHub #118 | Tenant isolation and context |
| MESP-30 / GitHub #119 | Company, Branch and Warehouse organization behavior |
| MESP-31 / GitHub #120 | Shared master-data identity and lifecycle contract |
| MESP-33 / GitHub #122 | Inventory opening, ledger and valuation contract |
| MESP-34 / GitHub #123 | Finance opening, currency, tax and reconciliation contract |
| MESP-35 / GitHub #124 | Sales and AR opening-state boundary |
| MESP-38 / GitHub #127 | Security, audit, data governance, support and sensitive data |
| MESP-51 / GitHub #140 | Approved B1/PD-041 migration contract |
| MESP-48 / GitHub #137 | Open supported-volume/capacity/production-governance gate |
| MESP-50 / GitHub #139 | Open residency/retention/privacy/backup/restore/legal gate |
| MESP-141 / GitHub #229 | Future implementation; may start only after MESP-40 acceptance and blockers |
| MESP-142 / GitHub #230 | Later stabilization/release-candidate work; not activated |

## 31. Review and approval handoff

### 31.1 Definition of Ready for independent requirements review

MESP-40 is ready for independent review when:

- The canonical BRD is present in the repository and linked from MESP-40.
- Requirements, rules, workflows, acceptance criteria, domain classification,
  error taxonomy and traceability are complete.
- Provisioning and migration are distinct.
- Tenant isolation, currency, bilingual handling, accounting, inventory,
  audit, retry/idempotency, correction and activation gating are explicit.
- Cross-ledger opening balances reconcile subsidiary detail to the applicable
  GL control accounts, with one economic opening effect and blocking mismatch
  behavior explicit.
- Generic MESP-141 activation blockers, per-Tenant cutover prerequisites and
  MESP-48/MESP-50 production gates are partitioned explicitly.
- MESP-141 and MESP-142 remain not activated.
- MESP-48 and MESP-50 remain open.
- No product implementation or owner-managed asset was changed.

### 31.2 Approval block

| Role | Decision | Name/date |
|---|---|---|
| Product Owner / Owner | Approve, return for correction, or record open decisions | Pending independent review |
| Finance owner | Confirm financial/currency/opening contract | Pending named review |
| Inventory owner | Confirm quantity/value/ledger opening contract | Pending named review |
| Security/Audit owner | Confirm access, evidence and sensitive-data contract | Pending named review |
| Tenant business owner | Confirm onboarding/source/cutover applicability | Pending named review |

This document remains a review candidate until the approval record is completed.

## 32. Session boundary and non-mutation record

- Product implementation: 0.
- MESP-141 activation: 0.
- MESP-142 activation: 0.
- Jira mutations: 0.
- Wafra-specific core behavior: 0.
- Retail POS scope: 0.
- frontend/assets changes: 0.
- CI claim: GitHub Actions — ACTIVE / VERIFIED; Repository Validation, Backend
  and Frontend are required checks.
