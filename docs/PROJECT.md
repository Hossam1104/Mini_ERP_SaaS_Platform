# Project

Mini ERP SaaS Platform (**MESP**). This page covers what the product is, who uses it, what Release 1
includes, and the business rules each module must keep. The full requirements are in the approved
BRDs under [`requirements/`](requirements/). For how it is built, see
[`ARCHITECTURE.md`](ARCHITECTURE.md). For where it stands, see [`ROADMAP.md`](ROADMAP.md).

## 1. Goal and business purpose

MESP is a reusable ERP for Saudi small and mid-sized B2B businesses. It is multi-Tenant and bilingual
(English and Arabic, with right-to-left layout). Each Tenant is isolated from every other Tenant. One
codebase serves every Tenant, and anything Tenant-specific is configuration, not code.

Release 1 is the full-feature B2B ERP. It is not an MVP, and it is not a fork for one customer. It
covers Procurement, Inventory, Finance, B2B Sales, Reporting, and Tenant onboarding/migration. All
of this sits on a secure multi-Tenant foundation.

## 2. Users

| User | What they do |
|---|---|
| Tenant business users | Run purchasing, stock, sales, and accounting inside the Companies and Branches they are permitted to use. |
| Tenant approvers | Approve documents under configured policies. Separation of duties (SoD) and delegation apply. |
| Tenant administrators | Manage their Tenant's users, memberships, and configuration. |
| Platform administrators | Run the SaaS control plane: Tenants, plans, lifecycle, and support. Being a platform administrator gives **no** access to a Tenant's ERP data. That access needs an explicit, audited exact-Tenant support grant or a membership. |
| Onboarding/migration operators | Load, validate, reconcile, and approve a new Tenant's opening data. |

## 3. Scope

**In scope for Release 1:**
- Multi-Tenant B2B ERP.
- The organization hierarchy Tenant → Company → Branch.
- Master data, Procurement/P2P, Inventory, Finance, B2B Sales/O2C, and Reporting.
- Internal configuration-led Tax/VAT.
- Multi-currency.
- Audit.
- EN/AR RTL.
- Repeatable Tenant migration/onboarding.

**Permanently out of scope for Release 1:**
- **Retail POS.** Release 1 is B2B only.
- **Wafra-specific behavior.** Wafra is a validation Tenant only. It gets no specific schema, workflow,
  permission, pricing, approval, accounting, or lifecycle behavior. Its logo is Tenant branding
  configuration (ADR-019).
- **Statutory e-invoicing and statutory claims.** This means ZATCA/FATOORA implementation or readiness
  claims, legal certification, and government submission.
- **External production integrations.** This means providers, credentials, webhooks, payment gateways,
  bank feeds, automated FX feeds, and external SSO. See MESP-39 (#128); it is future-release work.
- **Consolidation across legal entities** (MESP-56).

## 4. Organization and entry model (ADR-019)

**Tenant.** The Tenant is the security and data-isolation boundary. The server resolves and
authorizes it before any Tenant data is readable. A Tenant is never a user-selectable filter.

**Company/Branch context.** The operational context sits inside an authorized Tenant and follows the
Company/Branch scope:
- One permitted context is selected automatically.
- Several contexts use the header switcher.
- Users never type raw GUIDs.

**Entry flow.** A Tenant host only *suggests* a candidate Tenant. The full flow is:
`Host → candidate Tenant → authentication → exact-Tenant membership → Tenant Overview → optional context switch`.

**Common host.** A common host shows:
- an automatic redirect when the user has one membership;
- a chooser limited to active memberships when there are several;
- a safe no-access page when there are none.

**Platform administration.** It is a separate control plane.

**SAR symbol.** The Saudi Riyal symbol is presentation only. It has no effect on FX, tax, accounting,
or stored amounts. Non-SAR currencies are unaffected.

## 5. Module requirements and business rules

Each section summarizes the approved BRD. **The BRD wins on any detail.**

### 5.1 Platform administration and Tenant lifecycle
- **BRDs:** [11](requirements/11_SaaS_Platform_Administration_BRD.md), [13](requirements/13_Multi_Tenancy_BRD.md).
- Platform operators manage:
  - Tenant provisioning;
  - plans and entitlements;
  - module readiness;
  - suspension and reactivation;
  - support grants;
  - export, offboarding, and purge review.
- Tenant activation belongs to Tenant lifecycle (M27). No other module activates a Tenant.
- Destructive purge is gated. It requires MESP-50 (#139) retention/legal decisions.

### 5.2 Identity and access
- **BRD:** [12](requirements/12_Identity_and_Access_BRD.md).
- Browser sessions use a server-side session. The cookie is a locator only.
- Every protected request re-validates exact membership, organization scope, and permission on the
  server.
- Unsafe requests need antiforgery.
- Protected effects are audited **before** they happen.
- Client-supplied Tenant or scope is never authority.

### 5.3 Organization and company structure
- **BRD:** [14](requirements/14_Organization_and_Company_Structure_BRD.md).
- Tenant → Company (legal entity) → Branch.
- A document's Company/Branch scope is fixed once it is created. Relocating a document to another
  scope is not allowed.

### 5.4 Master data
- **BRD:** [16](requirements/16_Master_Data_and_Product_Catalog_BRD.md).
- **Products.**
  - A Product is Tenant-wide and reusable by that Tenant's Companies and Branches. It is never shared
    across Tenants.
  - Product and Item are one identity; there is no variant entity.
  - SKUs are unique within the Tenant. A Product has zero or more barcodes, each unique within the
    Tenant.
  - Creation makes a Product Active. There is no Draft state; the lifecycle is Deactivate and
    Reactivate.
  - A Product stores tracking *configuration* only. Inventory owns operational batch, lot, serial, and
    expiry behavior.
- **Suppliers and Business Customers** are party identities. Their purchasing and sales roles belong to
  the process modules.
- **Price Lists** resolve deterministically on the server.
- **Currencies, Exchange Rates, Payment Terms, and Tax codes** are configuration-led master data.
  Exchange rates resolve by exact document date.

### 5.5 Procurement / Purchase-to-Pay
- **BRD:** [21](requirements/21_Procurement_and_Purchase_to_Pay_BRD.md).
- **Chain:** Purchase Request → Supplier Quotation and comparison → source decision → Purchase Order →
  **manual** Supplier Confirmation → Goods Receipt → Purchase Invoice handoff → three-way matching →
  Supplier Return.
- Supplier Confirmation can be full, partial, rejected, or no response.
- Approvals use reusable, configuration-led policies with SoD and time-bounded delegation.
- A supplier-proposed change triggers controlled re-approval.
- Matching tolerances and exception resolution require explicit authority.
- Procurement never writes stock or accounting truth directly. It hands off to Inventory and Finance
  through their contracts.

### 5.6 Inventory and warehouse
- **BRD:** [22](requirements/22_Inventory_and_Warehouse_Management_BRD.md).
- Inventory owns:
  - the stock ledger and opening balances;
  - availability and reservations;
  - receipts and returns;
  - Warehouse Transfer and In Transit;
  - adjustments, counts, and Stock Issue;
  - valuation using the moving weighted average method.
- Stock effects post only through the Inventory ledger.
- Valuation reaches Finance through the `inventory-valuation-finance.v1` handoff. Inventory never
  writes Journals.

### 5.7 Finance and accounting
- **BRD:** [23](requirements/23_Finance_and_Accounting_BRD.md).
- **Rule FIN-OD-01.** Finance alone owns:
  - balanced Journals;
  - source-to-GL mapping;
  - account and period validation;
  - subledger reconciliation;
  - the Inventory valuation handoff;
  - controlled corrections and reversals;
  - posting evidence.
  Operational modules own their source documents and never fabricate accounting entries.
- Finance scope covers:
  - Chart of Accounts, fiscal calendar, periods, and Posting Rules;
  - AP, AR, supplier payments, customer receipts, Cash/Bank, allocation, and settlement.
- Payment methods are manual only.
- Tax accounting.
- **Multi-currency.** This covers transaction, functional, and Reporting Currency, plus revaluation.
  Authorized rate evidence is persisted; functional-currency transactions carry no FX evidence.
- **Close.** Year-end close, reconciliation, and the core Finance reports.
- Posting into a closed period, or to a restricted account, fails closed.

### 5.8 B2B Sales / Order-to-Cash
- **BRD:** [24](requirements/24_Sales_and_Order_to_Cash_BRD.md).
- **Chain:** Quotation → Sales Order → reservation → partial fulfillment → Delivery → Sales Invoice →
  receipt and allocation → Customer Return and Credit Note.
- Pricing, tax, and FX evidence are captured immutably on the document.
- Order approval uses the policy that is effective **at submission**, with SoD and delegation.
- **Credit control.**
  - Finance's customer exposure is evaluated in the exposure currency.
  - Missing or mismatched truth fails closed as `Unknown`.
  - An override is controlled and currency-aware.
- Delivery posts stock through Inventory. Invoicing posts through Finance, from persisted Sales
  evidence.

### 5.9 Reporting and analytics
- **BRD:** [25](requirements/25_Reporting_and_Analytics_BRD.md).
- Reports are read-only views over module truth, scoped to the Tenant and the user's permissions.
- Each report carries source lineage and freshness.
- Export and distribution are controlled.
- Reporting owns no ledger.

### 5.10 Saudi localization
- **BRD:** [28](requirements/28_Release_1_Saudi_Localization_BRD.md).
- Saudi-localized Core ERP: bilingual EN/AR with RTL, SAR presentation, and internal configuration-led
  VAT.
- No statutory e-invoicing (ADR-015 remains open).
- ADR-011 (Arabic search and collation evidence) remains open.

### 5.11 Security, audit, and data governance
- **BRD:** [29](requirements/29_Security_Audit_and_Data_Governance_BRD.md).
- Audit evidence is immutable and reconstructable.
- Access is least-privilege.
- Every lookup is Tenant-scoped. A foreign record behaves exactly like a missing one.
- Retention, residency, legal hold, and purge are gated by MESP-50 (#139).

### 5.12 Data migration and Tenant onboarding
- **BRD:** [40](requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md).
- **Stages:** intake → validation → quarantine → dry run → execution → opening data (Inventory, GL,
  AR, AP, Cash/Bank, and multi-currency openings) → reconciliation → approval → Ready-for-Handover
  evidence.
- **Reconciliation.**
  - A material or unexplained mismatch blocks.
  - Reconciliation never creates balancing Journals or stock adjustments.
  - `Outcome Unknown` blocks.
  - Partial completion stays visible.
- **Approval.**
  - It uses a versioned, configuration-led policy.
  - It fails closed when no policy is configured.
  - The Production quorum is still open (M40-DEC-006).
- **Ready-for-Handover.** It creates eligibility evidence only. It never activates the Tenant.
- M40-DEC-001 through M40-DEC-006 remain open.

## 6. Requirements index

| Doc | Area | Status |
|---|---|---|
| [00](requirements/00_ERP_Business_Glossary.md) | ERP Business Glossary: the mandatory vocabulary | Draft |
| [11](requirements/11_SaaS_Platform_Administration_BRD.md) | SaaS Platform Administration | Approved |
| [12](requirements/12_Identity_and_Access_BRD.md) | Identity and Access | Approved |
| [13](requirements/13_Multi_Tenancy_BRD.md) | Multi-Tenancy and Tenant Lifecycle | Approved |
| [14](requirements/14_Organization_and_Company_Structure_BRD.md) | Organization and Company Structure | Approved |
| [16](requirements/16_Master_Data_and_Product_Catalog_BRD.md) | Master Data and Product Catalog | Approved |
| [21](requirements/21_Procurement_and_Purchase_to_Pay_BRD.md) | Procurement / P2P | Approved |
| [22](requirements/22_Inventory_and_Warehouse_Management_BRD.md) | Inventory and Warehouse | Approved |
| [23](requirements/23_Finance_and_Accounting_BRD.md) | Finance and Accounting | Approved |
| [24](requirements/24_Sales_and_Order_to_Cash_BRD.md) | B2B Sales / O2C | Approved |
| [25](requirements/25_Reporting_and_Analytics_BRD.md) | Reporting and Analytics | Approved |
| [28](requirements/28_Release_1_Saudi_Localization_BRD.md) | Saudi Localization | Approved |
| [29](requirements/29_Security_Audit_and_Data_Governance_BRD.md) | Security, Audit, Data Governance | Approved |
| [40](requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md) | Data Migration and Tenant Onboarding | Approved |

The BRDs are preserved verbatim. Paths written inside them, such as `docs/00_…`, predate the
2026-09-25 move. The move map is in [`audit/cleanup-plan.md`](audit/cleanup-plan.md) §d.

**Product baseline:**
- PRD v1.2, [`assets/MESP_PRD_v1.2.docx`](assets/MESP_PRD_v1.2.docx);
- the project presentation, [`assets/Mini_ERP_SaaS_Platform_Project_Presentation.pptx`](assets/Mini_ERP_SaaS_Platform_Project_Presentation.pptx);
- Platform Admin wireframes in [`assets/wireframes/`](assets/wireframes/).

## 7. Glossary (key terms)

[`requirements/00_ERP_Business_Glossary.md`](requirements/00_ERP_Business_Glossary.md) is the
mandatory vocabulary. The terms used most in current work:

| Term | Meaning |
|---|---|
| Tenant | A customer organization and the security/data-isolation boundary. Resolved on the server. |
| Company / Branch | A legal entity and an operating location inside a Tenant. Together they form the operational context. |
| Capability | One bounded tracker item delivering a module feature, e.g. MESP-137 (#225). |
| Capability completion | Accepted capabilities / planned capabilities (currently 24/26). **Not** production readiness. |
| Production readiness | A separate, conservative estimate (~47% overall, ~41% Procurement/P2P). Gated by MESP-48 and MESP-50. |
| Golden cycle | One end-to-end Release 1 business flow, from setup to reconciliation (see ROADMAP). |
| Outcome Unknown | A durable effect whose result cannot be proven. It always blocks downstream acceptance. |
| Ready-for-Handover | Migration evidence that a Tenant's opening data is reconciled and approved. It does not activate the Tenant. |
| Fail closed | A missing policy, truth, or authority blocks the action. It never falls back to allowing it. |

## 8. Documentation map

| Where | What |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | The architecture as built, its rules, and enforcement. |
| [`ROADMAP.md`](ROADMAP.md) | Done, in progress, next, blocked, and deferred work, with tracker links. |
| [`DECISIONS.md`](DECISIONS.md) | Owner and planner decisions, plus the ADRs. |
| [`MODEL_ROUTING.md`](MODEL_ROUTING.md) | The AI operating model. |
| [`requirements/`](requirements/) | The approved BRDs and the glossary. |
| [`audit/`](audit/) | The 2026-09-25 cleanup audit and plan. |
| [`history/`](history/) | Verbatim archives: the old governance, the state log, specs, reviews, the handover, and the old ADR files. Historical, not authority. |
| [`assets/`](assets/) | The PRD, the presentation, and the wireframes. |
