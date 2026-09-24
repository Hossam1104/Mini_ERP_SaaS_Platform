# Mini ERP SaaS Platform - MVP Founder Decision Pack

| Field | Value |
|---|---|
| Purpose | Minimum founder approvals required to begin the first detailed BRD |
| Founder and accountable approver | Hossam |
| Date prepared | 1 August 2026 |
| Canonical PRD | `docs/MESP_PRD_v1.2.docx` (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Recommended first BRD | MESP-27 - SaaS Platform Administration |

## 1. Executive recommendation

**The first detailed BRD can start after Hossam completes the approval block in section 10 and MESP-26 records that approval.** No unresolved business decision in MESP-41 through MESP-56 must be answered before MESP-27 starts.

MESP-26 is ready for Hossam's approval now, subject to one governance decision: accept the solo-founder operating model and the decision-timing classifications in this pack. After that approval, Claude may close the BRD Foundation items identified in `docs/91_Jira_Simplification_Update.md`, move MESP-26 to Done, and move only MESP-27 to In Progress.

There are **no unresolved product-scope, domain, architecture-provider, or production-compliance decisions that genuinely block the start of MESP-27**. MESP-52 was resolved while writing MESP-27 and is incorporated into BRD v0.10. Production hosting, ZATCA, Saudi VAT, PDPL, residency, retention, penetration testing, backup topology, and vendor choices remain mandatory gates at their stated times; they do not block business analysis.

True entry controls that remain:

1. Hossam must approve this pack in writing.
2. MESP-26 must contain the approval evidence and move to Done.
3. MESP-27 must remain business-requirements work only: no implementation design, source code, development Stories, or Retail POS scope.

## 2. Decisions Hossam must approve now

| Decision | Recommended Default | Reason | Risk | Hossam Decision |
|---|---|---|---|---|
| Approve solo-founder BRD governance, accept the timing plan for all open decisions, approve MESP-26, and start only MESP-27 | **Approve**. Hossam temporarily holds Product Owner, Business Sponsor, Business Analysis Lead, Architecture Owner, QA Lead, and Implementation Lead accountability. Preserve system separation-of-duties controls and require external specialist validation before production where stated. | This is the minimum human authorization needed to replace the former all-TBD and all-decisions-first gate without weakening product or production controls. | Without written approval, Jira has no human evidence that the foundation gate changed and MESP-27 must remain To Do. | Approve / Request changes |

No MESP-41 through MESP-56 answer was approved by the original MESP-26 decision. **Subsequent controlled update, 1 August 2026:** Hossam approved MESP-52 and MESP-56 during the MESP-27 correction cycle. Their rows below now record approved product decisions; all other defaults remain unapproved starting positions for the owning BRD or later gate.

## 3. Governance items accepted without detailed reading

| Jira key | Classification | Recommendation and important correction | Must Hossam read the full item? |
|---|---|---|---|
| MESP-17 | Simplify for solo-founder operation | Keep the governance principles, immutable approvals, change control, and BRD requirement record. Replace multi-person quorum as an entry condition with Hossam's accountable approval; preserve external specialist gates and system separation of duties. | No - read the approval comment only. |
| MESP-18 | Accept as-is | Approve the glossary as the controlled working vocabulary. This does not approve terms marked Draft or Requires Business Decision; those remain open for their owning BRDs. | No - use it as reference while writing BRDs. |
| MESP-19 | Accept with a small correction | Accept the traceability model and the v0.2 correction: MESP-34 is Finance and MESP-35 is B2B Sales. Populate detailed rows progressively as each BRD is written. | No - read the correction and approval comment only. |
| MESP-20 | Simplify for solo-founder operation | Replace all-TBD project ownership with Hossam for the six interim roles. Record external specialist validation before production for Finance, Saudi compliance, privacy, residency/retention, and security. Do not demand fictitious deputies. | No - approve the concise ownership record only. |
| MESP-21 | Simplify for solo-founder operation | Retain the sequence, inputs, outputs, and validation evidence. Run focused sessions inside each owning BRD rather than pre-scheduling a multi-person 18-day workshop program. | No - use the relevant workshop section when its BRD starts. |
| MESP-22 | Accept with a small correction | Set Hossam as owner and append the approved technology decision as the next immutable PD-NNN entry dated 1 August 2026. Keep technical detail in the architecture baseline. | No - read the new decision entry only. |
| MESP-23 | Keep open during BRD | Approve the register structure, apply the timing classifications in this pack, and keep the register active. Open questions are closed only by approved decisions or explicit deferral. | No - review only questions when they become due. |
| MESP-24 | Accept as-is | Preserve Wafra as validation evidence only. No Wafra-specific core behavior, schema, permission, report, or workflow may enter the product. | No - apply the classification control. |
| MESP-25 | Accept with a small correction | Use the verified sequence in section 8. Finance is MESP-34 and precedes B2B Sales at MESP-35. Disregard the retracted v0.1 key mapping. | No - approve the sequence shown here. |

MESP-26 is **Accept with a small correction**: replace the former "15 of 17 criteria outstanding" conclusion with the solo-founder gate in this pack. It may move to Done only after Hossam completes section 10.

## 4. Decisions resolved during domain BRDs

Legend: **H** = Hossam approval required; **W** = Wafra validation required as evidence only; **E** = external specialist validation. Unless explicitly marked **APPROVED**, each row has timing category **Must decide during its owning domain BRD**.

| Jira | Decision and owning domain / BRD | Decision/default and current status | Validators | Risk of delaying beyond the owning BRD |
|---|---|---|---|---|
| MESP-41 | Batch, lot, serial, and expiry scope - Master Data / Inventory; MESP-31 and MESP-33 | Configurable per product or category; disabled by default; enforce end-to-end when enabled. | H: Yes; W: Yes; E: only if regulated products enter scope | Product identity, receipt, issue, return, count, migration, and ledger requirements remain ambiguous. |
| MESP-42 | Purchase approval workflow - Procurement; MESP-32 | Purchase Request required; quotation comparison optional; one configurable amount threshold; no self-approval. | H: Yes; W: Yes; E: Finance/control review before production | Approval states, permissions, and audit evidence cannot be finalized. |
| MESP-43 | Supplier confirmation and partial confirmation - Procurement; MESP-32 | Record confirmation manually for information; allow partial confirmation; quantity or price change requires an explicit reviewed change. | H: Yes; W: Yes; E: No | Purchase Order states and receipt readiness remain unclear. |
| MESP-44 | PO/receipt/invoice matching - Procurement and Finance; MESP-32 with MESP-34 validation | Three-way match with zero tolerance initially; route mismatches to an authorized manual exception. | H: Yes; W: Yes; E: Finance/accounting | Supplier liability controls and exception handling cannot be approved. |
| MESP-45 | Negative stock - Inventory; MESP-33 | Never permit negative stock in Release 1; block the movement and require a controlled correction. | H: Yes; W: Yes; E: Finance/accounting for valuation | Moving Weighted Average, backdating, fulfillment, and period-close rules remain unsafe. |
| MESP-46 | B2B credit control - B2B Sales with Finance; MESP-35 | Hard check at Sales Order confirmation with an audited Finance override; define exposure components in the BRD. | H: Yes; W: Yes; E: Finance/accounting | Sales states, permissions, and receivables risk controls remain incomplete. |
| MESP-47 | Payment and receipt methods - Finance; MESP-34 | Manual bank transfer and cash recording; support partial allocation and on-account balances; defer gateways and bank feeds. | H: Yes; W: Yes; E: Finance/accounting | Settlement, reconciliation, evidence, and cash/bank controls remain incomplete. |
| MESP-51 | Migration and opening balances - Data Migration; MESP-40, informed by MESP-33/MESP-34 | Migrate master data plus reconciled stock, GL, AP, and AR opening balances; include open documents only where Wafra evidence proves a launch need; no full history. | H: Yes; W: Yes; E: Finance/accounting | Cutover scope, data templates, reconciliation, and go-live acceptance remain undefined. |
| MESP-52 | Plans, modules, limits, and entitlements - Platform Administration; MESP-27 | **APPROVED by Hossam, 1 August 2026.** One production Release 1 Plan contains all approved B2B ERP Modules and configurable limits. It records service/support tier, non-calculating price metadata, and effective dates. Assignment is manual, effective-dated, and audited. Trial tenants, metered billing, automated subscription invoicing, and pricing-engine behavior are excluded. Entitlements cannot be overridden per Tenant; they change only through a versioned Plan or effective-dated Subscription change. A separate Restricted Validation Plan may exist only in non-production for denial testing. | H: Approved; W: No requirement authority; E: No | Closed for MESP-27. MESP-48 still supplies measurable reference volumes for limits and performance validation. |
| MESP-53 | Report catalogue and reconciliation ownership - Reporting; MESP-36 | Minimum statutory and core operational set; Finance owns subledger-to-GL reconciliation and Inventory/Finance jointly own quantity-to-value reconciliation. | H: Yes; W: Yes; E: Finance/accounting and Saudi tax where statutory | Report scope can grow without control and close evidence remains undefined. |
| MESP-54 | Exchange-rate source and process - Finance; MESP-34 | Manual, effective-dated rates maintained and approved by Finance; preserve the applied rate on documents; defer automated feeds. | H: Yes; W: Yes; E: Finance/accounting | Multi-currency posting, revaluation, realized differences, and audit evidence remain ambiguous. |
| MESP-55 | Delegation, escalation, and out-of-office behavior - Identity and Access; MESP-28, applied by later domains | One named approver with controlled administrator reassignment; prevent self-approval; defer parallel approval and automatic escalation. | H: Yes; W: Yes; E: Security/control review | Approval workflows may become inconsistent or unauditable across modules. |
| MESP-56 | Legal entity support and consolidation exclusion - Organization; MESP-30 with MESP-34 validation | **APPROVED by Hossam, 1 August 2026.** A Tenant may contain multiple Companies / Legal Entities. Each is a separate legal and accounting boundary. Release 1 excludes financial consolidation, intercompany automation, elimination entries, transfer pricing, and consolidated financial statements. Detailed operating rules remain owned by MESP-30, with Finance validation in MESP-34. | H: Approved; W: Evidence during MESP-30; E: Finance/accounting validation | Closed as a scope decision; MESP-30 must still define numbering, calendars, posting boundaries, permissions, and organization behavior. |

## 5. Decisions required before implementation

### Product decision

| Jira | Owning domain | Timing category | Recommended MVP default - not yet approved | Validators | Risk of delay |
|---|---|---|---|---|---|
| MESP-48 - Reference tenant volumes | Platform/NFR; validate during MESP-27, finalize before affected implementation | Must decide before implementing the affected module | Use measured Wafra pilot volumes plus one conservative SME reference profile covering tenants, users, products, warehouses, lines, monthly transactions, files, exports, and reports. | H: Yes; W: Yes; E: performance validation before production | Data shapes, indexes, report/export limits, load tests, and subscription limits lack measurable targets. |

### Architecture decisions

Before affected coding, complete the relevant ADR for exact SQL schema and EF Core context ownership; cross-module transaction boundaries; session timeouts; privileged-user MFA; Arabic SQL collation and search normalization; runtime localization and bilingual document generation; worker separation; test-environment topology; and any external API authentication required by an approved integration. Exact storage and telemetry adapters may wait until deployment integration, but must be decided before their code is finalized.

These decisions do not authorize implementation. They become due only after the owning BRD section is approved.

## 6. Decisions required before production

| Jira or gate | Timing category | Recommended safe position until validated | Validators | Risk of delay |
|---|---|---|---|---|
| MESP-49 - Saudi e-invoicing launch scope | Must decide before production | Do not claim live compliance or enable production Saudi invoicing until a qualified Saudi tax/ZATCA specialist confirms applicable phases and document types. Keep the adapter boundary isolated. | H: Yes; W: Yes as evidence; E: Saudi VAT and ZATCA specialist required | Live invoicing may be non-compliant or launch may be blocked late. |
| MESP-50 - Residency and retention | Must decide before production | Make no tenant contract promise until qualified privacy/legal review confirms residency, backup, support access, retention, export, deletion, and cross-border rules. | H: Yes; W: Yes as customer evidence; E: PDPL/privacy/legal required | Hosting contracts, backups, support, offboarding, and tenant commitments may be invalid. |

The production gate must also validate:

- Finance and accounting design, postings, reversals, valuation, close, reconciliation, and opening balances.
- Saudi VAT and ZATCA e-invoicing applicability and evidence.
- PDPL, privacy, data residency, retention, legal hold, export, purge, and support access.
- Security threat model, tenant-isolation tests, privileged access, secrets, encryption keys, vulnerability management, and penetration testing.
- Hosting region and topology, SQL Server 2025 topology and licensing, object storage, malware scanning, and observability backend.
- Encrypted backups, tested restoration, disaster recovery, approved RPO/RTO, and operational ownership.
- Performance at the approved reference volumes, including noisy-tenant, reports, exports, background jobs, and attachments.

There are **no decisions classified Must approve before the first detailed BRD** and none of the still-open MESP-41 through MESP-56 decisions is classified **Defer to post-MVP**. MESP-52 and MESP-56 are now approved. Individual advanced options such as automated rate feeds, bank feeds, gateways, parallel approvals, automated escalation, metered billing, intercompany automation, and consolidation remain Release 1 exclusions inside their owning decisions.

## 7. Verified corrections

- The canonical PRD is `docs/MESP_PRD_v1.2.docx`. It was at the repository root as `MiniERPSaaSPlatform_PRD_v1.2.docx` when this pack was prepared; the file contents are unchanged and only the repository path moved.
- The Technology Architecture Baseline source path is corrected and its status is Approved Architecture Baseline.
- Hossam is Architecture Owner.
- Retail POS, cashier operations, cash drawers, retail shifts, retail checkout, and POS receipt processing remain excluded.
- MESP-34 is Finance and Accounting; it precedes MESP-35 B2B Sales and Order-to-Cash.
- The empty module documentation placeholders remain untouched.

## 8. Simplified BRD sequence

1. MESP-27 - SaaS Platform Administration
2. MESP-28 - Identity and Access
3. MESP-29 - Multi-Tenancy and Tenant Lifecycle
4. MESP-30 - Organization and Company Structure
5. MESP-31 - Master Data and Product Catalog
6. MESP-32 - Procurement and Purchase-to-Pay
7. MESP-33 - Inventory and Warehouse Management
8. MESP-34 - Finance and Accounting
9. MESP-35 - B2B Sales and Order-to-Cash
10. MESP-36 - Reporting and Analytics
11. MESP-37 - Saudi Localization and Compliance
12. MESP-38 - Security, Audit, and Data Governance
13. MESP-39 - Integrations and External Services
14. MESP-40 - Data Migration and Tenant Onboarding

## 9. Immediate next task

Start exactly one Jira Task after MESP-26 is approved:

**MESP-27 - Produce SaaS Platform Administration BRD**

MESP-52 is resolved and incorporated into MESP-27 BRD v0.10. Complete Hossam's review of the corrected MESP-27 package and gather MESP-48 volume evidence without blocking BRD approval. Do not start MESP-28 through MESP-40.

## 10. Hossam approval block

Approved decisions:

I approve the solo-founder BRD governance model, the timing classification
for the open decisions, and the approved technology architecture baseline.

I approve starting the detailed BRD stage with MESP-27 only.

Requested changes:

None.

Approved to start detailed BRD:

Yes

Approved first Task:

MESP-27

Approved by:

Hossam

Date:

1 August 2026
