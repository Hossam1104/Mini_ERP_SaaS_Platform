# Mini ERP SaaS Platform — Master Data and Product Catalog BRD

> **MESP-108 checkpoint clarification - 10 August 2026.** The 21 SQL safety
> tests referenced in historical overlays are a separately gated
> Foundation-only LocalDB harness; they do not validate Master Data or Business
> Parties SQL behavior. Current arithmetic is 670 passing non-SQL tests plus
> 21 Foundation SQL cases = 691. SQL Server collation/unique-index parity,
> Arabic linguistic behavior, and ADR-011 remain open at their existing scope.
> The checkpoint changes no approved business requirement.

> **Authoritative current Product-slice overlay - 9 August 2026.** MESP-99 /
> M95-SL-02 Category and UOM is Done through PR #33, correction PR #34, and
> final audit-semantics correction PR #35. MESP-101 is **Done** for the
> M95-SL-03 Product identity readiness gate after PR #36 merged to `main` at
> `c7392a55e0b60fd83e48447e3f9218f82cfaccea`; closure evidence is Jira comment
> `10672` and activation/owner evidence is `10671`. MESP-102 delivered the
> bounded Product identity implementation through PR #37, merged at
> `202d59068caac5d1fac402794627e41d7f452456`; activation, validation, and
> closure evidence are Jira comments `10675`, `10676`, and `10677`. The six
> Product-only bounds remain MD-OD-001, MD-OD-003, MD-OD-005, MD-OD-008,
> MD-OD-010, and MD-OD-011: Tenant-wide ownership, hybrid Tenant-unique
> SKU/barcode coding, permissioned/audited routine lifecycle without separate
> approval, Active-on-create with Deactivate/Reactivate, Product-side tracking
> configuration only, and one Product/Item identity without variant behavior.
> The approved BRD requirements and remaining decision register are unchanged;
> no downstream behavior, migration, or production readiness claim is made.

> **Authoritative current Supplier-readiness overlay - 9 August 2026.** MESP-103
> is **Done** as the bounded M95-SL-04 Supplier readiness item. The Owner
> disposition in Jira comment `10681`, with closure evidence `10682`, resolves
> MD-OD-001, MD-OD-005, and MD-OD-008 for Supplier only: Tenant-wide
> availability inside the owning Tenant, no separate approver for routine
> Supplier maintenance, and Active-on-authorized-create with guarded
> Deactivate/Reactivate and preserved history. Permission, trusted
> server-derived Tenant authorization, optimistic concurrency, audit, and
> fail-closed controls remain mandatory. Supplier remains an external Business
> Party role with no login, credentials, membership, or consumer session; no
> Supplier source behavior has started. MD-OD-007 remains an external Saudi
> statutory-validation/production gate under MESP-49. MESP-104 is now Done
> through PR #39; MESP-105 is Done for the separately activated Customer
> readiness item and MESP-107 is Done through PR #41 at
> `fb632982d06fd4f6bf965fb15dff7701a0bddcec`, with Jira activation,
> validation, and closure evidence `10692`/`10726`/`10727`.
> See
> `docs/19_Supplier_M95_SL_04_Readiness.md` for the bounded contract and exact
> implementation handoff.

> **Authoritative current Business Customer implementation overlay - 10 August 2026.**
> MESP-105 is **Done** for the dedicated M95-SL-05 readiness and decision-gate
> item under MESP-6; the Customer-only Owner disposition is Jira comment
> `10691`. MESP-107 is the separate implementation item, activated in Jira
> comment `10692`; its bounded source implementation is complete on branch
> `agent/mesp-107-business-customer` at commit
> `8d8d8fddfa79a8e08f2566fcdd2499dfd594277d`; PR #41 merged to `main` at
> `fb632982d06fd4f6bf965fb15dff7701a0bddcec`.
> Business Customer remains an external B2B role, not a User, login,
> membership, credential holder, consumer, or unified Party. The implemented
> boundary is Tenant-wide identity inside the owning Tenant with no cross-Tenant
> sharing, server-derived Tenant/resource authorization, no separate approver
> for routine master-data maintenance, no Draft, Active-on-authorized-create,
> guarded Deactivate/Reactivate, same-role integrity, contacts, concurrency,
> audit, contracts/routes, and module-owned persistence. No statutory
> registration, downstream Sales/AR/Finance behavior, migration, provider, or
> production-readiness claim is made. MD-OD-007 remains an external Saudi
> statutory/legal and production gate under MESP-49; MESP-106 is now Done
> through PR #42; MESP-48, MESP-49, and MESP-50 remain open.

> **Authoritative current MESP-106 hardening overlay - 10 August 2026.**
> MESP-106 is **Done** through [PR #42](https://github.com/Hossam1104/mini-erp-saas-platform/pull/42),
> merged to `main` at `0f712edcf58119057d614000721fe41227383bc1` from reviewed
> head `678a5598877f55f1b32b012de692ebdf28408acd`. The bounded correction
> classifies Product/Supplier authorization dependency outages as internal
> service/configuration failures, preserves genuine permission/resource/scope/
> Tenant denials, classifies deterministic Supplier duplicates as validation
> conflicts, and preserves failure audit evidence. Focused classification tests
> are 82/82, the full non-SQL suite is 670/670, and the Release build is 0/0.
> Customer source behavior, domain fields, tables, migrations, provider,
> production, downstream, and cross-Tenant scope behavior are unchanged. The
> 21 SQL safety tests remain gated by the missing connection string.
> Jira activation, validation, and closure evidence are comments
> `10728`/`10729`/`10730`.

> **Historical MESP-100/MESP-99 state overlay - 9 August 2026.** MESP-100 is Done with Jira closure evidence 10663; PR #32 merged at 511f6be9f005e54930f993aead9758d7a66b75a8. MESP-99 was In Progress with activation evidence 10664 for M95-SL-02. The five Category/UOM-only bounds are MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006. The approved v0.3 business requirements and all other Open Decisions remain preserved; no MESP-99 behavior was implemented by MESP-100.

> **Historical MESP-100 readiness-correction overlay - 9 August 2026.** MESP-100 was the bounded readiness item for M95-SL-02. MESP-96 was Done, MESP-99 remained To Do until that correction was validated, merged, and activated, and no Category/UOM persistence or behavior was implemented here. The five Category/UOM-only bounds were MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006; the rest of the Open Decision Register remains preserved.

> **Historical delivery overlay - 8 August 2026 (approved business baseline unchanged).** MESP-31 is **Done**. PR #28 remains merged at final head `8396197b54189cb550f07bd4bb6779fd38ac30cb` and actual merge commit `1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b`. MESP-95 is **Done** after PR #29 merged at approved head `c465d660e49a254f2fffbb95e0d07c5fcf17a193` with actual merge commit `93f4e83992ef46f498cfbfacbb513cfc3d8dda7d`; closure evidence is Jira comment `10654`. MESP-96 was **In Progress** for contract-only/non-persistent M95-SL-01 at that historical point. MD-OD-001 through MD-OD-011 remained open and unresolved; no Master Data persistence existed.
>
> The approved requirements, classifications, recommendations, acceptance criteria, and Open Decision Register below are unchanged. This overlay records delivery state only and does not authorize Product/Item, SKU/Barcode, tracking, business-availability, approval-catalogue, or Draft/Active behavior.

> **Historical post-merge state overlay — 8 August 2026 (approved business baseline unchanged).** MESP-31 is **Done**. PR #28 is merged: final PR head `8396197b54189cb550f07bd4bb6779fd38ac30cb`, actual merge commit `1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b`; Hossam's approval is recorded in Jira comment `10649` and final closure evidence in comment `10650`. MESP-95 was **In Progress**, and PR #29 was the active, open, non-draft implementation-readiness review at that historical point. MD-OD-001 through MD-OD-011 remained open and unresolved; Master Data source implementation had not started.
>
> The approved requirements, classifications, recommendations, acceptance criteria, and Open Decision Register below are unchanged. Historical status paragraphs are retained for provenance and are superseded by this overlay for current-state purposes.

> **Historical MESP-100 readiness overlay - 9 August 2026.** MESP-100 was the
> active, bounded readiness correction for M95-SL-02 and MESP-99 remains To Do
> until the correction is validated, merged, and activated. The five
> Category/UOM-only Owner bounds are MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002,
> and MD-OD-006; they do not resolve the remaining Open Decision Register for
> other Master Data domains. No Category/UOM persistence or business behavior
> is implemented by MESP-100, and the approved v0.3 requirements remain
> unchanged.

## 1. Document Control

| Field | Value |
|---|---|
| Document | Master Data and Product Catalog Business Requirements Document |
| Jira | MESP-31 — Produce Master Data and Product Catalog BRD |
| Parent Epic | `MESP-6 — EPIC 06 - Master Data and Product Catalog` — verified directly against live Jira, not inferred. |
| Version | v0.3 — Approved Business Baseline (v0.1 corrected after the first business-requirements review of PR #28 to produce v0.2; v0.2 corrected after the second business-requirements review — M31-R10 through M31-R13 — to produce v0.3) |
| Status | **Approved Business Baseline.** This document remains a business baseline and does not authorize source implementation by itself. |
| Approved by | Hossam |
| Approval date | 8 August 2026 |
| Jira approval evidence | MESP-31 comment `10649` |
| Approved reviewed content head | `1e2d055354f0ddde833190948d09fa426707484c` |
| Accountable owner | Hossam, Product Owner and founder approver |
| Prepared by | Claude (Sonnet 5), acting as the delivery agent under Hossam's direction, following the drafting role used for MESP-27/28/29/30 |
| Date | 8 August 2026 |
| Canonical PRD | `docs/MESP_PRD_v1.2.docx`, PRD v1.2 Final Approved Baseline (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Required glossary | `docs/00_ERP_Business_Glossary.md` |
| Related approved BRDs | `docs/11_SaaS_Platform_Administration_BRD.md`; `docs/12_Identity_and_Access_BRD.md`; `docs/13_Multi_Tenancy_BRD.md`; `docs/14_Organization_and_Company_Structure_BRD.md` |
| Architecture reference | `docs/01_Technology_Architecture_Baseline.md` (constraint reference only; does not dictate business requirements) |
| Delivery reference | `docs/94_Product_Delivery_Master_Plan.md`; `docs/90_MVP_Founder_Decision_Pack.md` |
| Jira state | MESP-31 is **Done**. Owner authorization comments `10615` and `10616`, approval comment `10649`, and closure evidence `10650` are recorded in live Jira. MESP-95 is **Done** with closure evidence `10654`; MESP-102 is **Done** for the bounded Product implementation through PR #37 with closure evidence `10677`. Product implementation is separate from BRD authorization; no unrelated Master Data persistence, downstream behavior, migration, or production claim is implied. |
| Development environment decision (non-business) | The Owner has selected local SQL Server (instance `.`, database `MESP`) as the development environment for the later, separately gated implementation phase. This is an implementation/environment decision, not a business rule, carries no business meaning, and is Out of Scope for this BRD's content. No credential of any kind appears in this document. |
| Classification summary | See §43 Coverage Checklist for the exact rule/scenario/decision counts produced by this draft. |

### Classification legend

| Classification | Meaning |
|---|---|
| **Confirmed** | Directly supported by the approved PRD, the approved glossary, an approved adjacent BRD, or an existing Jira/founder-decision-pack requirement. |
| **Confirmed — Founder-approved Release 1 requirement** | Explicitly approved by Hossam for the Release 1 business baseline (including the scope authorization recorded for this BRD on 8 August 2026) and carried forward without adding implementation behavior. |
| **Open Decision** | A genuine business decision still requiring Hossam's recorded approval. Only the `MD-OD-*` register uses this classification. |
| **Deferred Gate** | Deliberately owned by a later domain BRD (MESP-32 through MESP-40, MESP-46, MESP-49), MESP-48, MESP-50, or a later approval. No value is invented here. |
| **Deferred Gate / Recommended Default — not yet approved** | A `docs/90_MVP_Founder_Decision_Pack.md` recommended default that Hossam has **not** approved. The pack states it as a starting position for its owning BRD, not as an approved requirement. This BRD records the recommendation and its owner; it does not adopt, approve, or build behavior on it. Used for MESP-41 (see MD-OD-010) and MESP-54 (owned by MESP-34). |
| **External Validation Required** | Requires confirmation from Saudi legal, tax, or accounting authority beyond this BRD's business-analysis scope. |
| **Out of Scope** | Explicitly excluded from this BRD or owned by another domain in full. |

This is a business-requirements document. It authorizes no API, database, UI, code, automated test, Sprint, or implementation Jira work. See §41 for the exact Owner authorizations this draft relies on and the condition that still gates implementation.

## 2. Executive Summary

**Classification: Confirmed.** Master Data and Product Catalog is the shared, reusable business-fact layer that every other Release 1 domain consumes: Procurement reads Supplier and Product facts to raise a Purchase Order; Inventory reads Product and Unit of Measure facts to move stock; B2B Sales reads Business Customer, Product, and Price List facts to quote and invoice; Finance reads Currency, Exchange Rate, Payment Term, and Tax facts to post a balanced ledger entry. A Product, Supplier, or Tax rate defined once and reused everywhere is what keeps these domains consistent with each other; defining the same fact twice, inconsistently, in two domains is the specific failure this BRD prevents.

**Classification: Confirmed.** Master Data lives inside the approved Tenant boundary (`docs/13_Multi_Tenancy_BRD.md`). Every master record this BRD defines is Tenant-owned business data: it is private to its Tenant by default, is never visible or reusable across a Tenant boundary, and its duplicate checks, search, import, and export never leak another Tenant's values. That isolation boundary is mandatory and settled. **It is a separate question from business scope inside the owning Tenant** — whether a record is usable by every Company/Legal Entity in that Tenant or restricted to some of them is undecided and is Open Decision MD-OD-001 (§30). Tenant ownership is not read anywhere in this BRD as automatic Tenant-wide business availability. Nothing here weakens or reinterprets the Multi-Tenancy or Organization BRDs' approved isolation, scope, or hierarchy rules.

**Classification: Confirmed — Founder-approved Release 1 requirement.** This BRD is reusable, configuration-led product content. It defines no Wafra-specific schema, rule, permission, workflow, report, status, price rule, tax behavior, or UX. Wafra is validation evidence only, exactly as the approved PRD and the Multi-Tenancy/Organization BRDs already require.

**Classification: Confirmed.** A master record that a posted transaction has already referenced is never deleted. It is deactivated. This single rule — restated for every one of the ten domains in this BRD — is what keeps historical Purchase Orders, Sales Orders, invoices, and ledger entries reproducible after the Product, Supplier, Tax rate, or Exchange Rate they used has since changed or been retired.

**Classification: Confirmed.** Several of the ten domains this BRD is required to cover — Supplier, Business Customer, Price List, Tax, Payment Term, Currency, and Exchange Rate — are, in the currently approved glossary, formally *owned* by a later domain module (Procurement, B2B Sales, Finance, or the Saudi Country Pack), not by "Master Data and Catalog." §9 Ownership Boundaries resolves this directly: MESP-31 establishes the shared master-record identity, lifecycle, and cross-module data contract for all ten domains; the named owning module's own BRD (MESP-32 through MESP-40, MESP-46, MESP-49) remains authoritative for that domain's transactional business behavior. Nothing here silently reassigns glossary ownership.

## 3. Business Purpose

**Classification: Confirmed.** MESP-31 exists to define the reusable, Tenant-isolated business meaning of the master data every downstream ERP transaction depends on, before any of Procurement, Inventory, B2B Sales, or Finance is built against an undefined foundation.

| Objective | Required business outcome | Classification | Source |
|---|---|---|---|
| Consistent product identity | A Product is defined once and reused unchanged across Procurement, Inventory, and Sales. | Confirmed | PRD PLT-003; glossary Product |
| Controlled party identity | A Supplier and a Business Customer are identifiable, duplicate-checked, external business parties, never system Users. | Confirmed | PRD PROC-008; glossary Supplier/Business Customer |
| Dependable pricing | A Price List is a reusable, effective-dated, non-ambiguous source of a Product's price for a given Customer/Currency/period. | Confirmed | PRD SAL-001; glossary Price List |
| Governed taxation | Tax rates are effective-dated configuration; they are never hard-coded into transaction logic and never silently rewrite history. | Confirmed | PRD FIN-007, KSA-002 |
| Auditable commercial terms | A Payment Term is reusable configuration whose meaning on an already-posted document survives any later change to the term itself. | Confirmed | PRD FIN-004/FIN-005; glossary Payment Terms |
| Controlled currencies | Release 1 supports multiple currencies; SAR is the Saudi default, not a hard-coded ceiling. | Confirmed | PRD FIN-010; glossary Base/Transaction/Reporting Currency |
| Reproducible exchange rates | An Exchange Rate is effective-dated; a posted transaction keeps the rate it actually used forever. | Confirmed | PRD FIN-003, FIN-010; glossary Exchange Rate. (How rates are *sourced and approved* is the separate, unapproved MESP-54 default owned by MESP-34 — see §20.) |
| Reuse across downstream domains | Every fact this BRD defines is read, not redefined, by Procurement (MESP-32), Inventory (MESP-33), B2B Sales (MESP-35), and Finance (MESP-34). | Confirmed | PRD bounded-context table (Catalog/Parties contexts) |
| Governed gates preserved | MESP-48 and MESP-50 remain explicit production and capacity gates; this BRD invents no volume, retention, or purge value. | Deferred Gate | MESP-48/MESP-50 |

## 4. Scope

| In-scope area | Business requirement | Classification |
|---|---|---|
| Product | Business identity, classification, tax linkage, lifecycle, duplicate control. | Confirmed |
| Product Category | Classification structure, lifecycle, product assignment. | Confirmed |
| Unit of Measure | Base/alternate units, conversions, lifecycle. | Confirmed |
| Supplier | External-party identity, lifecycle, purchasing/AP dependency. | Confirmed |
| Business Customer | B2B counterparty identity, lifecycle, sales/AR dependency. | Confirmed |
| Price List | Reusable effective-dated pricing container, lifecycle. | Confirmed |
| Tax | Effective-dated tax configuration, lifecycle. | Confirmed |
| Payment Term | Reusable due-date/settlement configuration, lifecycle. | Confirmed |
| Currency | Multi-currency master, lifecycle. | Confirmed |
| Exchange Rate | Effective-dated currency-pair conversion, lifecycle. | Confirmed |
| Master-record lifecycle | Deactivate-not-delete, effective dating, reactivation across all ten domains. | Confirmed — Founder-approved Release 1 requirement |
| Tenant isolation | Ownership, duplicate-check, search/export/import isolation for all ten domains. | Confirmed — Founder-approved Release 1 requirement |
| Ownership boundaries | Where MESP-31 ends and MESP-32/33/34/35/37/40/46/49 begin. | Confirmed |
| Audit evidence | Reconstruction of actor, action, before/after value, and reason for sensitive master-data changes. | Confirmed |
| Migration | Business ownership, mapping, duplicate review, reconciliation, and sign-off expectations for master-data migration. | Confirmed |

## 5. Out of Scope

| Exclusion | Classification | Owner |
|---|---|---|
| API/endpoint contracts, headers, tokens, protocols, request/response payloads | Out of Scope | Later Lean Implementation Specification |
| Database tables, columns, keys, indexes, ORM design, physical schema | Out of Scope | Phase 5 of `docs/94_Product_Delivery_Master_Plan.md` |
| Angular components, screens, layouts, navigation | Out of Scope | Later Lean Implementation Specification |
| Source code, migrations, automated tests, Sprint creation, Pull Requests | Out of Scope | This task is business analysis only; see §41 for the implementation gate |
| Production deployment, infrastructure topology, hosting region | Out of Scope | MESP-50 |
| Local SQL Server development-environment topology (instance, database name) | Out of Scope | Implementation/environment decision only, not a business rule (see §1) |
| Detailed Purchase Order / Purchase Requisition / supplier-confirmation workflow | Out of Scope | MESP-32 Procurement BRD |
| Detailed Sales Order / quotation / credit-check / fulfillment workflow | Out of Scope | MESP-35 B2B Sales BRD |
| Detailed stock ledger, costing, batch/lot/serial, warehouse-transfer behavior | Out of Scope | MESP-33 Inventory BRD |
| Detailed journal posting, AP/AR ledger mechanics, fiscal-period control | Out of Scope | MESP-34 Finance BRD |
| Credit Limit business rule and exposure calculation | Out of Scope | MESP-46 (Finance/B2B Sales) |
| ZATCA/e-invoicing coupling and Saudi statutory tax-treatment detail beyond the Release 1 VAT baseline | Out of Scope | MESP-49 Saudi Country Pack BRD; External Validation Required |
| Physical migration/ETL implementation, scripts, tooling | Out of Scope | Business migration *requirements* are in §38; technical migration implementation is not |
| Wafra-specific product, price, tax, or party behavior | Out of Scope | Wafra is validation-only (PRD BR-003) |
| Retail POS consumer/product/price behavior | Out of Scope | Retail POS is excluded from Release 1 |
| Invented MESP-48 volumes or MESP-50 retention/purge/residency values | Deferred Gate | MESP-48/MESP-50 |

**Business migration requirements vs. technical migration implementation:** this BRD's §38 defines what must be true of a Master Data migration from a business standpoint (ownership, mapping, duplicate handling, reconciliation, sign-off). It defines no ETL script, staging table, or import tool. That remains MESP-40's technical delivery.

## 6. Source Traceability

| Source | Relevant authority used in this BRD | Classification |
|---|---|---|
| Jira MESP-31 | Required scope: Products, Categories, UOM, Suppliers, Business Customers, Price Lists, Taxes, Payment Terms, Currencies, Exchange Rates; deactivate-not-delete instruction. Parent Epic `MESP-6 — EPIC 06 - Master Data and Product Catalog`. Owner authorizations recorded in comments `10615` and `10616` (§41). | Confirmed |
| Jira MESP-31 Source Baseline (corrected) | MESP-31's Jira Source Baseline now reads: primary anchor **PLT-003**; supporting anchors **PLT-002, SAL-001, PROC-002, PROC-008, FIN-001, FIN-003, FIN-007, FIN-010, KSA-002, BR-013, ADM-003**, plus the applicable PRD RULE set for master-data integrity. This is the authoritative baseline this BRD traces to. `PLT-011`–`PLT-014` and `BR-004` are Platform Administration anchors and are **no longer** listed as MESP-31's baseline — see §42. | Confirmed |
| Owner instruction, 8 August 2026 | BRD-entry authorization and future implementation authorization (see §41); explicit ten-domain scope mandate for MESP-31. | Confirmed — Founder-approved Release 1 requirement |
| PRD v1.2 (`docs/MESP_PRD_v1.2.docx`) | **PLT-003** ("Master data. Authorized users can create, review, activate, deactivate, import, export, and search shared business master data with validation and duplicate detection.") is the primary Platform-foundation anchor. **PLT-002** (organization hierarchy, functional currencies). **SAL-001** (customer identity, addresses, tax attributes, contacts, payment terms, price list, credit limit, status). **PROC-002** (supplier quotations: price, currency, tax, delivery terms). **PROC-008** (suppliers are external parties, no platform accounts). **FIN-001** (chart of accounts, currency behavior). **FIN-003** (journal currencies/exchange rates). **FIN-007** (tax calculated from effective-dated rules). **FIN-010** (document/functional/reporting currency, exchange-rate source, rounding differences). **KSA-002** (Saudi VAT seeded at 15% but configurable, never hard-coded). **BR-013** (import opening and master data with preview, validation, duplicate control, rollback, reconciliation). **ADM-003** (import controls: templates, validation previews, row-level errors, duplicate rules). The PRD's own bounded-context table also assigns "Catalog" (Product, category, unit, price list, tax classification) and "Parties" (Supplier, customer, contacts, addresses, terms) as distinct contexts. | Confirmed |
| **Anchor correction, now reflected in Jira** | The task brief that originally requested this BRD named **PLT-011 through PLT-014 and BR-004** as the PRD traceability anchors. Direct extraction of `docs/MESP_PRD_v1.2.docx` text shows these four PLT anchors are Platform Administration requirements (tenant provisioning, subscriptions/entitlements, tenant branding/structure, no-tenant-specific-code) already owned by the approved MESP-27 BRD (`docs/11_SaaS_Platform_Administration_BRD.md` lines 63–66), and BR-004 is "Manage plans, subscriptions, modules, entitlements, quotas, and tenant lifecycle" — also Platform Administration, not master data. **MESP-31's Jira Source Baseline has since been corrected** to the anchors listed above (principally PLT-003), so the repository and Jira now agree. See §42 Source Conflicts and Corrections. | Confirmed correction |
| `docs/00_ERP_Business_Glossary.md` | Controlled definitions for Product, Item, SKU, Barcode, Category, Unit of Measure, Base Unit, Purchase/Sales Unit, Supplier, Supplier Contact, Business Customer, Customer Contact, Payment Terms, Credit Limit, Price List, Tax Category, Base/Transaction/Reporting Currency, Exchange Rate family, Audit Event, Retail POS. | Confirmed |
| `docs/90_MVP_Founder_Decision_Pack.md` | The pack's §4 legend is explicit: unless a row is marked **APPROVED**, its content is a *recommended default* that "must decide during its owning domain BRD" — it is not an approved requirement. Only MESP-52 and MESP-56 carry an APPROVED marking. Accordingly: **MESP-41** (batch/lot/serial/expiry scope — configurable per Product or Category, disabled by default, enforced end-to-end when enabled; jointly owned by MESP-31/MESP-33) is an **unapproved recommended default** — it is MESP-31's own owning-BRD decision and is raised as **MD-OD-010**. **MESP-54** (manual, effective-dated exchange rates maintained and approved by Finance; preserve the applied rate; automated feeds deferred) is an **unapproved recommended default owned by Finance/MESP-34** and is not approved by this BRD. **MESP-51** (migrate master data plus reconciled opening balances, owned by MESP-40) is likewise an unapproved recommended default. The domain sequence placing MESP-31 fifth, immediately after MESP-30 and before MESP-32, is settled delivery sequencing. | Confirmed as to what the pack states; the MESP-41/MESP-51/MESP-54 defaults themselves are **Deferred Gate / Recommended Default — not yet approved** |
| `docs/13_Multi_Tenancy_BRD.md` | Tenant ownership, private-by-default data, cross-Tenant denial, audit-evidence boundary — all apply unchanged to every master record this BRD defines. | Confirmed |
| `docs/14_Organization_and_Company_Structure_BRD.md` | Approved hierarchy (Platform → Tenant → Company/Legal Entity → Branch → Warehouse); scope never inherits upward; the Organization BRD is silent on which of these levels owns Product/Price List/Tax scope — see MD-OD-001. | Confirmed boundary |
| `docs/12_Identity_and_Access_BRD.md` | Plain-language Permission-category convention (no dotted technical naming); Separation-of-Duties pattern distinguishing SoD from approval workflow. | Confirmed |
| `docs/11_SaaS_Platform_Administration_BRD.md` | Platform-owned vs Tenant-owned record boundary; audit-evidence pattern. | Confirmed boundary |
| `docs/Decisions.md` | ADR-011 (runtime localization/Arabic search/RTL/bilingual documents required before module implementation, explicitly naming MESP-31); ADR-003 (shared-database Tenant isolation); ADR-014/ADR-015 (retention and Saudi e-invoicing gates, not owned here). | Confirmed |
| `docs/94_Product_Delivery_Master_Plan.md` | Sequential BRD delivery discipline; MESP-31 status must remain In Progress until Hossam's review, not silently Done. | Confirmed |

## 7. Actors and Responsibilities

| Actor | Responsibility | Constraint | Classification |
|---|---|---|---|
| Hossam / Product Owner and founder approver | Approves this BRD, resolves the `MD-OD-*` register, and controls delivery sequencing including the implementation gate. | Approval does not itself authorize implementation. | Confirmed |
| Tenant Administrator | Confirms initial master-data setup (Currencies, base Tax configuration, initial Categories) within the Tenant's approved scope. | Cannot cross Tenant boundaries. | Confirmed |
| Master Data Maintainer | Creates, edits, activates, and deactivates Products, Categories, Units of Measure, Suppliers, Business Customers, Price Lists, Taxes, Payment Terms, Currencies, and Exchange Rates within an authorized scope. | Requires the applicable capability (§26); cannot bypass duplicate checks or effective-dating rules. | Confirmed |
| Approver (sensitive master data) | Approves a master-data change **where an approved business policy requires a separate approver**. Which specific changes carry that requirement is not settled by any approved source and is Open Decision MD-OD-005; candidate changes include tax-rate changes, published customer-facing Price List changes, and manually entered exchange rates. | Cannot approve their own change wherever a separate approver is required (§28). | Confirmed as a role; the catalogue of changes it applies to is Open Decision (MD-OD-005) |
| Procurement / Inventory / B2B Sales / Finance consumer | Reads Master Data facts to perform a downstream business function; never redefines them locally. | Cannot silently fork a Product, Supplier, Tax, or Currency definition inside its own domain. | Confirmed |
| Migration / Onboarding owner | Owns source mapping, duplicate review, reconciliation, and sign-off for a Master Data import. | Ambiguous mappings remain quarantined until accountable approval. | Confirmed |
| Security / Privacy / Audit reviewer | Reviews master-data change evidence and denial events. | Does not gain ordinary maintenance access merely by being a reviewer. | Confirmed |

## 8. Controlled Terminology

This BRD reuses every existing glossary definition unchanged (see §6). It does not redefine Product, Item, SKU, Barcode, Category, Unit of Measure, Base Unit, Supplier, Business Customer, Payment Terms, Price List, or the Currency/Exchange Rate family. Whether Product and Item are the same business concept for Release 1, with no separate variant layer, is not treated as settled terminology here — the glossary's Item, SKU, and Barcode entries remain "Draft for BRD Validation" and that question is Open Decision **MD-OD-011** (§40). The table below is limited to terms this BRD relies on that are either new, cross-cutting, or need an explicit business-meaning statement not already fully settled by the glossary.

| Term | Business meaning used by MESP-31 | Classification |
|---|---|---|
| Master record | Any Product, Category, Unit of Measure, Supplier, Business Customer, Price List, Tax, Payment Term, Currency, or Exchange Rate record defined once and reused across transactions. | Confirmed |
| Business Party | The umbrella business concept covering Supplier and Business Customer: an external counterparty recorded as master data inside a Tenant, never a system User. Supplier and Business Customer remain **distinct business roles with distinct approved glossary meanings and lifecycles**; "Business Party" only names what they share (external identity, contact/address structure, duplicate-detection treatment). The approved glossary already states that the same legal company may legitimately exist as both a Supplier record and a Business Customer record, so the term introduces **no unified party record** and **no rule that an identity match across the two roles blocks the second role** — see MD-BR-045. **New glossary entry proposed** (see §42). | Confirmed |
| Currency (generic) | A recognized unit of monetary value maintained as Tenant master data, distinct from the Base/Transaction/Reporting *usage roles* the glossary already defines for a given document or ledger. **New glossary entry proposed** (see §42). | Confirmed |
| Tax (generic) | A configured, effective-dated rate or rule applied to a transaction, distinct from **Tax Category**, which the glossary already defines as the Saudi-Country-Pack-owned classification that determines *which* tax treatment applies. **New glossary entry proposed** (see §42). | Confirmed |
| Effective date | The date from which a rate, price, or configuration value is the one applied to a new transaction; it never changes what an already-posted transaction recorded. | Confirmed |
| Active / Inactive | The two ordinary lifecycle states of a master record. Active records may be selected for a new transaction; Inactive records may not, but remain visible for historical reference. Neither state exists as a standalone glossary entry today — this BRD is the first to define it generically (see §42). | Confirmed |
| Deactivation | The governed act of moving a master record from Active to Inactive. It never deletes the record or any transaction that already referenced it. | Confirmed — Founder-approved Release 1 requirement |
| Tenant ownership (master data) | Every master record this BRD defines belongs to exactly one Tenant, per `docs/13_Multi_Tenancy_BRD.md` MT-BR-002/MT-BR-003. | Confirmed |

## 9. Ownership Boundaries

**Classification: Confirmed.** This is the reconciliation the Executive Summary promised. The approved glossary currently assigns "Owning module" for several of MESP-31's ten mandatory domains to a *later* domain BRD. This BRD does not overrule that ownership; it adds a second, narrower layer beneath it.

| Domain | MESP-31 owns | Downstream owner (per glossary) owns |
|---|---|---|
| Product / Item / SKU / Barcode | Full business identity, classification, tax linkage, and lifecycle — glossary already names MESP-31 as the confirming BRD. | N/A — Master Data and Catalog is already the glossary-assigned owning module. |
| Product Category | Full identity, hierarchy depth (open, §12), and lifecycle. | N/A — same as above. |
| Unit of Measure (general) | Definition, alternate units, conversion identity, and lifecycle. | **Inventory (MESP-33)** owns Base Unit immutability once stock transactions exist and stock-valuation mechanics. |
| Supplier | Business identity, duplicate control, contact structure, and lifecycle as an external party. | **Procurement (MESP-32)** owns supplier-quotation comparison, confirmation workflow, and Purchase Order/AP process detail. |
| Business Customer | Business identity, duplicate control, contact structure, and lifecycle as a B2B counterparty. | **B2B Sales (MESP-35)** owns quotation/sales-order/credit-check process detail; **Finance (MESP-46)** owns Credit Limit mechanics. |
| Price List | Reusable container identity, currency, effective dating, and lifecycle. | **B2B Sales (MESP-35)** owns pricing precedence in a live Sales Order, discount authority, and approval workflow detail. |
| Tax | Reusable rate/rule identity, effective dating, and lifecycle. | **Saudi Country Pack (MESP-49)** owns statutory treatment, e-invoicing coupling, and ZATCA compliance detail. |
| Payment Term | Reusable term identity and lifecycle; assignment to Supplier/Customer. | **Finance (MESP-34)** owns due-date calculation mechanics, AP/AR aging, and collections. |
| Currency | Reusable currency identity and lifecycle. | **Finance (MESP-34)** owns base/functional-currency assignment mechanics, rounding, and GL posting behavior. |
| Exchange Rate | Reusable, effective-dated rate identity and lifecycle. | **Finance (MESP-34)** owns rate application at posting time, rounding-difference posting, and reconciliation. |

**Classification: Confirmed.** Where this table and the glossary's "Owning module" field appear to disagree, this table controls only for the master-record layer described in this BRD; the glossary's named module remains authoritative for the transactional behavior it already owns. §42 records the glossary corrections this reconciliation implies.

## 10. Master Record Lifecycle

**Classification: Confirmed — Founder-approved Release 1 requirement.** Every one of the ten domains in this BRD shares one lifecycle vocabulary. A domain-specific state is not invented unless a genuine business need is identified (none was, for Release 1).

| Status | Business meaning | Entry / exit | Classification |
|---|---|---|---|
| Active | The record may be selected for a new transaction, price, or assignment. | Recommended entry: successful validated creation becomes Active directly, with no Draft step — pending MD-OD-008. | Confirmed as a state; its entry point is Open Decision (MD-OD-008) |
| Inactive (Deactivated) | The record may not be selected for a new transaction, but remains visible for historical reference and reporting. | An authorized actor deactivates it; a record already referenced by a draft or posted transaction may still be deactivated — deactivation blocks *new* use, it does not touch existing references. | Confirmed — Founder-approved Release 1 requirement |
| Reactivated | An Inactive record is returned to Active. | An authorized actor reactivates it; for effective-dated domains (Tax, Exchange Rate, Price List) reactivation never rewrites the effective-dated history already recorded — see MD-BR-004. | Confirmed |

**Classification: Open Decision (MD-OD-008).** Whether any master-data domain requires a Draft-before-Active workflow for Release 1 is Hossam's decision and is **not** confirmed by this BRD. The recommended option is: **no Draft state for Release 1 — successful validated creation becomes Active** (the two-state Active/Inactive lifecycle in the table above). The Active/Inactive table and the reference-preservation matrix below are written on that recommendation; if Hossam decides a Draft state is required for one or more domains, §10, §23.1, and the creation-related acceptance scenarios must be revised before the implementation baseline is finalized.

**Reference-preservation matrix**, per task requirement, for what happens when a master record is:

| Situation | Required business outcome | Classification |
|---|---|---|
| Unused (never referenced) | May be edited freely or deleted outright, since no transaction depends on it. | Confirmed |
| Referenced by a draft transaction | May be deactivated; the draft keeps showing the record's current values until the draft is submitted, at which point normal validation re-checks whether the now-Inactive record may still be used to complete the document (Deferred Gate: exact re-validation policy is owned by the consuming domain's BRD, e.g. MESP-32/MESP-35). | Deferred Gate |
| Referenced by a posted transaction | Deactivation is permitted and does not alter the posted transaction's recorded values in any way; deletion is never permitted. | Confirmed — Founder-approved Release 1 requirement |
| Deactivated | Cannot be selected for a new transaction; remains searchable/reportable; existing references are unaffected. | Confirmed |
| Reactivated | Becomes selectable again for new transactions; does not retroactively change anything recorded while it was Inactive. | Confirmed |

## 11. Product Requirements

**Classification: Confirmed**, except the Product/Item identity model. A Product is Tenant-owned master data with a unique business code, a bilingual (Arabic/English) name where the Tenant's Users require it, an assigned Category, an assigned Base Unit of Measure, a tax classification, and a lifecycle status. Whether Release 1 treats Product and Item as one business concept, with no separate variant layer, is **Open Decision MD-OD-011** (§40); this BRD records the recommendation but does not confirm that identity model and specifies no variant behavior pending Hossam's decision (see MD-BR-015).

| Field | Requirement class | Notes |
|---|---|---|
| Product code | Required, unique within scope | Duplicate detection is mandatory before create/import; exact coding-rule format is MD-OD-003. |
| Arabic name | Conditionally required | Required wherever the Tenant serves Arabic-speaking Users; see §35. |
| English name | Required | |
| Description | Optional | |
| Category | Required, references an active Category | See §12. |
| Base Unit of Measure | Required, references an active Unit of Measure | Immutable once stock transactions exist (Inventory-owned, MD-BR-019). |
| Tax classification | Required | Configuration-led, never hard-coded (§17). |
| Sellable flag | Required | Business meaning only — whether the Product may appear on a Sales quotation/order; does not define Sales workflow. |
| Purchasable flag | Required | Business meaning only — whether the Product may appear on a Purchase Order; does not define Procurement workflow. |
| Inventory-relevant flag | Required | Whether the Product is stock-tracked; detailed costing/valuation is Inventory-owned (MESP-33). |
| Status | Required | Active / Inactive per §10. |
| Batch/lot/serial/expiry tracking | **Open Decision (MD-OD-010)** — not yet a requirement | The `docs/90_MVP_Founder_Decision_Pack.md` MESP-41 entry recommends "configurable per Product or Category; disabled by default; enforce end-to-end when enabled", jointly owned by MESP-31 (identity/configuration) and MESP-33 (enforcement). That is a **Recommended Founder Decision Pack default — pending Hossam approval**, not a confirmed requirement, so this BRD neither adopts it nor specifies any batch/lot/serial/expiry behavior. Whether the field exists at all on a Product depends on MD-OD-010. |

**Business validation:** duplicate code/name detection before create or import; a Product cannot be deleted once referenced by any Purchase Order, Sales Order, Price List line, or stock ledger entry (only deactivation is permitted); deactivating a Product never deactivates or alters historical documents that already referenced it.

## 12. Product Category Requirements

**Classification: Confirmed.** A Category is Tenant-owned master data used to classify Products for grouping, reporting, default rules, and analysis (glossary Category). Hierarchy depth is genuinely undecided (MD-OD-002): the glossary explicitly defers it to this BRD and no other approved source settles it.

| Field | Requirement class |
|---|---|
| Category code/name (Arabic/English) | Required, unique within scope |
| Parent Category | Conditionally required — only if hierarchy is approved (MD-OD-002) |
| Status | Required — Active/Inactive |

Deactivating a Category blocks new Product assignment to it but does not deactivate Products already assigned. A Category referenced by any active Product cannot be deleted, only deactivated.

## 13. Unit of Measure Requirements

**Classification: Confirmed.** A Unit of Measure is Tenant-owned master data identifying the quantity unit a Product is counted, bought, sold, stored, or valued in (glossary Unit of Measure). Every Product has exactly one Base Unit; Base Unit immutability once stock transactions exist is Inventory-owned (MESP-33), not redefined here. Alternate units require a positive, non-zero conversion factor to the Base Unit; a unit without a defined conversion cannot be used for stock movement (glossary).

| Field | Requirement class |
|---|---|
| UOM code/name (Arabic/English) | Required, unique within scope |
| Base Unit indicator | Required (exactly one Base Unit per Product) |
| Conversion factor to Base Unit | Required for an alternate unit; must be positive and non-zero |
| Status | Required — Active/Inactive |

Precision/rounding-algorithm detail for a conversion is not decided here (MD-OD-006); it is not silently assumed. A Unit of Measure referenced by an active conversion or an active Product cannot be deactivated without an explicit impact review.

## 14. Supplier Requirements

**Classification: Confirmed.** A Supplier is an external business party from whom a Company procures goods or services, recorded as Tenant master data (glossary Supplier). **A Supplier is never a system User; it never authenticates, never signs in, and this BRD models no Supplier access or credential of any kind.**

| Field | Requirement class |
|---|---|
| Legal/trading name (Arabic/English) | Required |
| Supplier code | Required, unique within scope |
| Tax/VAT registration number | Conditionally required — mandatory where Saudi statutory requirements apply; exact statutory field list beyond VAT registration is MD-OD-007 |
| Contact information | Optional |
| Default Payment Term | Optional, references an active Payment Term |
| Default Currency | Optional, references an active Currency |
| Status | Required — Active/Inactive |

Duplicate detection uses legal/trading name and tax registration number where available, and it runs **within the Supplier role** — its purpose is to prevent a second Supplier record for the same party. A match against an existing *Business Customer* is not a duplicate: the approved glossary confirms the same legal company may legitimately be both, so such a match is surfaced for review and optional linkage only and never blocks Supplier creation (MD-BR-045). Deactivating a Supplier blocks new Purchase Order creation but preserves every historical Purchase Order, receipt, and invoice that already referenced it. Purchasing/AP dependency detail is Deferred to MESP-32/MESP-34.

## 15. Business Customer Requirements

**Classification: Confirmed.** A Business Customer is a B2B counterparty to whom a Company sells goods or services (glossary Business Customer). Release 1 remains B2B-only: **an anonymous retail consumer is never modeled as a Business Customer**; Retail POS is excluded entirely.

| Field | Requirement class |
|---|---|
| Legal/trading name (Arabic/English) | Required |
| Customer code | Required, unique within scope |
| Tax/VAT registration number | Conditionally required — same Saudi statutory dependency as Supplier (MD-OD-007) |
| Contact information | Optional |
| Default Payment Term | Optional, references an active Payment Term |
| Default Price List | Optional, references an active Price List |
| Default Currency | Optional, references an active Currency |
| Credit Limit reference | Out of Scope — Finance/MESP-46 owns the value and mechanics; this BRD records only that a Business Customer *may carry* such a reference |
| Status | Required — Active/Inactive |

Duplicate detection uses legal/trading name and tax registration number, and it runs **within the Business Customer role** — its purpose is to prevent a second Business Customer record for the same party. A match against an existing *Supplier* is not a duplicate and never blocks Business Customer creation; it is surfaced for review and optional linkage only (MD-BR-045). Deactivating a Business Customer blocks new Sales Order creation but preserves every historical document. Sales/AR dependency detail is Deferred to MESP-35/MESP-34.

## 16. Price List Requirements

**Classification: Confirmed.** A Price List is a reusable, named set of Product prices, valid for a defined context and scoped to exactly one Currency (glossary Price List). It is not a discount-approval engine, not a Credit Limit, and not a cost record.

| Field | Requirement class |
|---|---|
| Price List name | Required, unique within scope |
| Currency | Required, references an active Currency |
| Effective start/end date | Required |
| Customer/segment assignment | Optional, where supported |
| Product price lines | Required — at least one Product/price pair |
| Status | Required — Active/Inactive |

**Classification: Open Decision (MD-OD-004).** Precedence when more than one active Price List could apply to the same Product/Customer/Currency combination is not decided by any approved source; this BRD does not invent a precedence formula. What *is* confirmed: two active Price List entries for the same Product, Customer/segment, and Currency must not have overlapping effective-date ranges without an explicit, approved precedence rule (MD-BR-030). A Price List referenced by a draft or posted Sales document is never deleted, only deactivated, so historical pricing evidence survives. Detailed precedence, discount authority, and Sales Order behavior are Deferred to MESP-35.

## 17. Tax Requirements

**Classification: Confirmed.** Tax rates are effective-dated configuration and are never hard-coded into transaction logic — directly confirmed by PRD FIN-007 ("Transactions calculate configured sales and purchase taxes from effective-dated rules") and KSA-002 ("the Saudi country pack seeds the current standard VAT rate of 15% where applicable, but tax rates, exemptions, zero-rating, effective dates, and evidence are configurable and never hard-coded into transaction logic").

| Field | Requirement class |
|---|---|
| Tax name/code | Required, unique within scope |
| Rate | Required |
| Effective start/end date | Required |
| Product/Customer applicability | Optional, where supported |
| Zero-rated / exempt / out-of-scope treatment | Conditionally required — Saudi VAT baseline requires it; detailed statutory treatment is Deferred to MESP-49 |
| Status | Required — Active/Inactive |

**Classification: External Validation Required.** Beyond the KSA-002 15% VAT baseline, statutory Saudi tax treatment, exemption rules, and ZATCA e-invoicing coupling require confirmation from Saudi legal/tax authority and are owned by MESP-49; this BRD invents none of it. A posted transaction retains its applied tax rate and effective date permanently, even if the Tax master record later changes (MD-BR-033); historical reproducibility is Confirmed — Founder-approved Release 1 requirement.

## 18. Payment Terms Requirements

**Classification: Confirmed.** A Payment Term is reusable configuration determining when an invoice becomes due, expressed as a defined interval or schedule from an agreed base date (glossary Payment Terms). It is not a Credit Limit and not a payment method.

| Field | Requirement class |
|---|---|
| Payment Term name/code | Required, unique within scope |
| Due-date structure (days/schedule) | Required — exact structure options are Deferred to MESP-34 |
| Status | Required — Active/Inactive |

A Payment Term is assignable to a Supplier or a Business Customer as a default. Deactivating a Payment Term preserves its exact meaning on every document already posted using it. AP/AR mechanics, collections, and dunning workflow are Deferred to MESP-34.

## 19. Currency Requirements

**Classification: Confirmed.** Release 1 is multi-currency; SAR is the Saudi default, never a hard-coded ceiling (PRD FIN-010; glossary Base/Transaction/Reporting Currency).

| Field | Requirement class |
|---|---|
| Currency code (ISO where applicable) | Required, unique within scope |
| Currency name (Arabic/English) | Required |
| Status | Required — Active/Inactive |

A Currency referenced by any transaction, Price List, or party default cannot be deleted, only deactivated. Base/functional-currency assignment at Company level, rounding, and GL posting mechanics are Deferred to MESP-34; the Organization BRD (PLT-002) already confirms a Tenant may configure one or more functional currencies across its Companies, which this BRD does not reinterpret.

## 20. Exchange Rate Requirements

**Classification: Confirmed.** An Exchange Rate converts an amount from a source currency to a target currency for an effective date (glossary Exchange Rate). It is not a price and not a silently-changeable constant.

| Field | Requirement class |
|---|---|
| Source currency | Required, references an active Currency |
| Target currency | Required, references an active Currency, must differ from source |
| Rate | Required, must be positive |
| Effective date | Required |
| Source/provenance | Conditionally required — how a rate is sourced and approved is not settled here (see below) |
| Status | Required — Active/Inactive |

**Classification: Deferred Gate / Recommended Default — not yet approved.** The current Founder Decision Pack recommendation is manual, effective-dated rates maintained and approved by Finance, preserving the applied rate on documents, with automated feeds deferred. **Final business authorization for MESP-54 remains owned by MESP-34 / MESP-54 and has not been given by Hossam**; this BRD records the recommendation and does not approve it, adopt it as a Release 1 requirement, or specify any rate-sourcing or Finance-approval mechanism built on it. The rules below are separately confirmed by the PRD and do not depend on MESP-54: a posted transaction retains the exact exchange rate it applied, permanently — no retroactive recalculation (MD-BR-039). A missing rate for a required currency pair/effective date blocks posting; it is never silently defaulted (MD-BR-040). Duplicate or overlapping rate entries for the same currency pair and effective date are rejected (MD-BR-041).

## 21. Data Requirements Summary

**Classification: Confirmed.** Every field table in §§11–20 already classifies each field as required, optional, or conditionally required, and states its uniqueness, effective-dating, and lifecycle expectations. No SQL column, data type, or physical schema is specified anywhere in this BRD, consistent with §5 Out of Scope. The cross-cutting expectations that apply to every field across all ten domains are:

| Cross-cutting data expectation | Applies to | Classification |
|---|---|---|
| Tenant-owned and Tenant-isolated (ownership/security boundary) | Every field of every domain in §§11–20 | Confirmed — Founder-approved Release 1 requirement |
| Business scope inside the owning Tenant (Company / Legal Entity / Branch availability) | Every domain in §§11–20 | Open Decision (MD-OD-001) — undecided; not assumed Tenant-wide |
| Business-uniqueness enforced at create/import | Product code, Category code, UOM code, Supplier code/tax registration, Customer code/tax registration, Price List name, Tax code, Payment Term code, Currency code, Exchange Rate (currency pair + effective date) | Confirmed |
| Effective-dated | Price List, Tax, Exchange Rate | Confirmed |
| Auditable | Every create, edit, activate, deactivate, and rate/price change across all ten domains | Confirmed |
| Bilingual (Arabic/English) where business-facing | Product, Category, UOM, Supplier, Business Customer, Currency names | Confirmed |

## 22. Business Rules Register

The following register is the MESP-31 business-rule baseline. It contains no API, schema, query, or storage prescription.

| ID | Domain | Rule | Classification |
|---|---|---|---|
| MD-BR-001 | Cross-cutting | Every master record this BRD defines is Tenant-owned and belongs to exactly one Tenant; it cannot be read or modified across a Tenant boundary. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-002 | Cross-cutting | A master record referenced by a posted transaction is deactivated, never deleted. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-003 | Cross-cutting | Deactivation never alters the recorded values of a document that already referenced the record. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-004 | Cross-cutting | Reactivation of an effective-dated record (Tax, Exchange Rate, Price List) never rewrites already-recorded effective-dated history. | Confirmed |
| MD-BR-005 | Cross-cutting | Duplicate business identity (code, name, tax registration number, currency-pair+effective-date) is checked before create or import. | Confirmed |
| MD-BR-006 | Cross-cutting | Every create, edit, activate, deactivate, and rate/price change on a master record is audit-evidenced with actor, Tenant, before/after value, and timestamp. | Confirmed |
| MD-BR-007 | Cross-cutting | An effective-dated change (Tax, Price List, Exchange Rate) publishes a future-effective value without disturbing already-posted history. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-008 | Cross-cutting | No Wafra-specific master-data schema, rule, permission, workflow, report, status, price rule, or tax behavior exists; every rule is configuration-led and reusable. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-009 | Cross-cutting | Business-facing master-data names (Product, Category, UOM, Supplier, Business Customer, Currency) support Arabic and English where the Tenant requires bilingual usability. | Confirmed |
| MD-BR-010 | Product | A Product has a unique business code within its owning scope. | Confirmed |
| MD-BR-011 | Product | A Product references exactly one active Category and exactly one active Base Unit of Measure. | Confirmed |
| MD-BR-012 | Product | A Product cannot be deleted once referenced by a Purchase Order, Sales Order, Price List line, or stock ledger entry; only deactivation is permitted. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-013 | Product | Deactivating a Product does not deactivate or alter historical documents that already referenced it. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-014 | Product | A Product's tax classification determines its default tax treatment and is configuration, not hard-coded. | Confirmed |
| MD-BR-015 | Product | **Recommended, pending Hossam's decision (MD-OD-011):** Release 1 treats Product and Item as one business concept, with no separate variant/product-family layer; SKU and Barcode identify the Product/Item per the separately approved coding rules (MD-OD-003). Not adopted as a rule until MD-OD-011 is resolved. | Open Decision (MD-OD-011) |
| MD-BR-016 | Category | A Category code/name is unique within scope. | Confirmed |
| MD-BR-017 | Category | Deactivating a Category blocks new Product assignment but does not deactivate Products already assigned. | Confirmed |
| MD-BR-018 | Category | Category hierarchy depth is not assumed; see MD-OD-002. | Open Decision |
| MD-BR-019 | Unit of Measure | Every Product has exactly one Base Unit, assigned at creation; Base Unit immutability once stock transactions exist is Inventory-owned (MESP-33). | Confirmed boundary |
| MD-BR-020 | Unit of Measure | An alternate-unit conversion factor to the Base Unit must be positive and non-zero. | Confirmed |
| MD-BR-021 | Unit of Measure | A Unit of Measure referenced by an active conversion or an active Product cannot be deactivated without an explicit impact review. | Confirmed |
| MD-BR-022 | Supplier | A Supplier is an external business party; it never becomes or requires a system User account. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-023 | Supplier | Supplier duplicate detection uses legal/trading name and tax registration number where available. | Confirmed |
| MD-BR-024 | Supplier | Deactivating a Supplier blocks new Purchase Order creation but preserves historical Purchase Orders and invoices referencing it. | Confirmed |
| MD-BR-025 | Business Customer | A Business Customer is a B2B counterparty; Release 1 never models an anonymous retail consumer as a Business Customer. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-026 | Business Customer | Business Customer duplicate detection uses legal/trading name and tax registration number. | Confirmed |
| MD-BR-027 | Business Customer | Deactivating a Business Customer blocks new Sales Order creation but preserves historical documents referencing it. | Confirmed |
| MD-BR-028 | Business Customer | Credit Limit value and enforcement mechanics are owned by Finance/MESP-46; a Business Customer record may carry a reference only. | Out of Scope (boundary) |
| MD-BR-029 | Price List | A Price List is scoped to exactly one Currency. | Confirmed |
| MD-BR-030 | Price List | Two active Price List entries for the same Product, Customer/segment, and Currency must not have overlapping effective-date ranges without an approved precedence rule. | Confirmed |
| MD-BR-031 | Price List | A Price List referenced by a draft or posted Sales document is never deleted; deactivation preserves historical pricing evidence. | Confirmed |
| MD-BR-032 | Tax | A Tax rate is effective-dated configuration; it is never hard-coded into transaction logic. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-033 | Tax | A posted transaction retains its applied tax rate and effective date even if the Tax master record later changes. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-034 | Tax | Tax deactivation never deletes historical tax calculations. | Confirmed |
| MD-BR-035 | Payment Term | A Payment Term is reusable configuration assignable to a Supplier or a Business Customer. | Confirmed |
| MD-BR-036 | Payment Term | Deactivating a Payment Term preserves its exact meaning on already-posted documents. | Confirmed |
| MD-BR-037 | Currency | Currency support is not limited to SAR; Release 1 provides genuine multi-currency capability. | Confirmed |
| MD-BR-038 | Currency | A Currency referenced by any transaction, Price List, or party default cannot be deleted, only deactivated. | Confirmed |
| MD-BR-039 | Exchange Rate | Exchange rates are effective-dated; a posted transaction retains its applied rate permanently with no retroactive recalculation. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-040 | Exchange Rate | A missing exchange rate for a required currency pair/effective date blocks posting; it is never silently defaulted. | Confirmed — Founder-approved Release 1 requirement |
| MD-BR-041 | Exchange Rate | Duplicate or overlapping exchange-rate entries for the same currency pair and effective date are rejected. | Confirmed |
| MD-BR-042 | Exchange Rate | The Founder Decision Pack **recommends** manually maintained, effective-dated rates approved by Finance, with automated provider feeds deferred. This is MESP-54, an unapproved recommended default owned by Finance/MESP-34; it is recorded here, not adopted, and no rate-sourcing or approval mechanism is specified by this BRD. | Deferred Gate / Recommended Default — not yet approved |
| MD-BR-043 | Migration | Master-data import/migration follows preview, validation, duplicate control, rollback, and reconciliation. | Confirmed |
| MD-BR-044 | Permissions | Only a Permission-holding, scope-authorized actor may create, edit, activate, or deactivate a master record; the Permission is Tenant-scoped and least-privilege. | Confirmed |
| MD-BR-045 | Business Party | Duplicate detection prevents a second record **within the same party role** (Supplier-to-Supplier, Business Customer-to-Business Customer). A matching identity across the two roles is **not** a duplicate: the same legal company may legitimately be both a Supplier and a Business Customer, so a cross-role match is surfaced for review and optional linkage and must never automatically reject the second role. Supplier and Business Customer remain distinct business roles with distinct records unless a later approved party-unification decision changes that; no unified Party record is defined here. | Confirmed (approved glossary — Supplier entry) |
| MD-BR-046 | Cross-cutting | Where an approved business policy requires a master-data change to be separately approved, the requester may not self-approve and publication is blocked until the required approval exists. Which changes carry that requirement is Open Decision MD-OD-005; this rule states the control, not its catalogue. | Confirmed (generic control); catalogue is Open Decision (MD-OD-005) |

## 23. Main Business Processes

Each process is business-level. No screen, API, schema, or implementation behavior is prescribed.

### 23.1 Create a master record (any of the ten domains)

- **Trigger:** An authorized Master Data Maintainer submits a new record.
- **Preconditions:** Required fields per §§11–20 are present; the actor holds the applicable capability (§26).
- **Main flow:** Validate required fields; run duplicate detection (MD-BR-005); record the create event (MD-BR-006); set status Active.
- **Exception:** A duplicate match holds the record for reviewed resolution rather than silently creating a second identity.
- **Outcome:** One reviewable Active master record with full audit evidence.

### 23.2 Deactivate a master record

- **Trigger:** An authorized actor deactivates a record no longer fit for new use.
- **Preconditions:** The actor holds the applicable capability.
- **Main flow:** Record the deactivation event and reason where required (MD-BR-006); set status Inactive; block the record from new selection; leave every existing reference unaffected (MD-BR-002/003).
- **Exception:** None — deactivation is always permitted regardless of existing references, since it never deletes anything.
- **Outcome:** An Inactive record, unusable for new transactions, with unchanged historical meaning.

### 23.3 Reactivate a master record

- **Trigger:** An authorized actor reactivates an Inactive record.
- **Preconditions:** The actor holds the applicable capability; for effective-dated domains, reactivation is checked against MD-BR-004.
- **Main flow:** Record the reactivation event; set status Active; the record becomes selectable for new transactions again.
- **Exception:** Reactivation is denied if it would rewrite already-recorded effective-dated history.
- **Outcome:** An Active record with preserved historical integrity.

### 23.4 Change an effective-dated value (Tax rate, Price, Exchange Rate)

- **Trigger:** An authorized actor (or Approver, where §27 requires one) publishes a new effective-dated value.
- **Preconditions:** No overlapping effective-dated entry exists for the same scope (MD-BR-030/041); approval is obtained where required.
- **Main flow:** Validate the new value and effective date; record the change event with before/after values; the new value applies only to transactions dated on or after its effective date.
- **Exception:** An overlap or a missing required approval blocks publication.
- **Outcome:** A new effective-dated value with no impact on already-posted transactions.

### 23.5 Import/migrate master data

- **Trigger:** An authorized Migration owner runs a bounded import.
- **Preconditions:** Source mapping, duplicate rules, and rollback strategy are defined (§38).
- **Main flow:** Preview, validate, flag duplicates and rejected rows, quarantine ambiguous mappings, obtain accountable sign-off, commit.
- **Exception:** An ambiguous mapping remains quarantined until its owner resolves it.
- **Outcome:** A reconciled, auditable batch of master records with a documented sign-off.

## 24. Alternative and Exception Paths

| Situation | Required business handling | Classification |
|---|---|---|
| Duplicate code/name/tax-registration detected at create or import | Hold for reviewed resolution; do not silently create a second identity. | Confirmed |
| Deactivation requested for a record referenced by a posted transaction | Permit deactivation; leave the posted transaction's recorded values untouched. | Confirmed — Founder-approved Release 1 requirement |
| Deletion requested for a referenced record | Deny; only deactivation is available once any reference exists. | Confirmed — Founder-approved Release 1 requirement |
| New effective-dated value overlaps an existing one for the same scope | Reject the new entry until the overlap is resolved. | Confirmed |
| Required exchange rate is missing for a transaction's currency pair/date | Block posting; do not silently substitute a different rate or date. | Confirmed — Founder-approved Release 1 requirement |
| Reactivation would rewrite already-recorded effective-dated history | Deny reactivation; require a new effective-dated entry instead. | Confirmed |
| Ambiguous migration mapping | Quarantine until an accountable owner approves the reconciled outcome. | Confirmed |
| Cross-Tenant duplicate-check or search attempt | Deny; never expose another Tenant's master data. | Confirmed — Founder-approved Release 1 requirement |
| Self-approval attempted on a change that an approved business policy requires a separate approver for | Deny; block publication until a distinct approver approves it (MD-BR-046, §27, §28). | Confirmed |
| Cross-role identity match between a Supplier and a Business Customer (same legal company) | Surface for review and optional linkage; never auto-reject the second role (MD-BR-045). | Confirmed |

## 25. Validation Rules

| ID | Validation condition | Required outcome | Classification |
|---|---|---|---|
| MD-VR-001 | Duplicate code/name/tax-registration at create or import | Hold for reviewed resolution | Confirmed |
| MD-VR-002 | Deletion attempted on a referenced record | Deny; deactivation only | Confirmed — Founder-approved Release 1 requirement |
| MD-VR-003 | Product without an active Category or active Base Unit | Reject creation | Confirmed |
| MD-VR-004 | Alternate UOM conversion factor is zero or negative | Reject | Confirmed |
| MD-VR-005 | Price List entry currency differs from the Price List's own currency | Reject | Confirmed |
| MD-VR-006 | Overlapping effective-dated entries for the same scope (Price, Tax, Exchange Rate) | Reject the new entry | Confirmed |
| MD-VR-007 | Exchange Rate source and target currency are identical | Reject | Confirmed |
| MD-VR-008 | Exchange Rate value is zero or negative | Reject | Confirmed |
| MD-VR-009 | Required exchange rate missing at transaction posting time | Block posting; no silent default | Confirmed — Founder-approved Release 1 requirement |
| MD-VR-010 | Cross-Tenant reference in any master-data field (Category, UOM, Currency, Payment Term, Price List) | Reject | Confirmed — Founder-approved Release 1 requirement |
| MD-VR-011 | A master-data change that an approved business policy requires to be separately approved lacks that approval, or the requester attempts to self-approve it | Block publication until a distinct authorized approver approves it (MD-BR-046). Which changes carry the requirement is Open Decision MD-OD-005 | Confirmed as a generic control; its catalogue is Open Decision (MD-OD-005) |
| MD-VR-012 | Reactivation would alter already-recorded effective-dated history | Deny | Confirmed |
| MD-VR-013 | Migration mapping is ambiguous or incomplete | Quarantine until accountable approval | Confirmed |
| MD-VR-014 | Duplicate detection matches an existing record **in the other party role** (Supplier vs Business Customer) for the same legal company | Surface for review and optional linkage; do not reject the creation (MD-BR-045) | Confirmed |

## 26. Permissions

**Classification: Confirmed.** Following the plain-language Permission-category convention already established by `docs/12_Identity_and_Access_BRD.md` §14.3 (no dotted technical syntax), the following business capabilities apply per master-data domain, Tenant-scoped and role-configurable:

| Capability | Applies to | Classification |
|---|---|---|
| View | All ten domains | Confirmed |
| Create | All ten domains | Confirmed |
| Edit | All ten domains | Confirmed |
| Activate | All ten domains | Confirmed |
| Deactivate | All ten domains | Confirmed |
| Approve (sensitive change) | Any domain whose changes an approved business policy requires a separate approver for; the exact catalogue is Open Decision MD-OD-005 | Confirmed as a capability; the changes it gates are Open Decision (MD-OD-005) |
| Maintain Price Lists | Price List | Confirmed |
| Maintain Taxes | Tax | Confirmed |
| Maintain Exchange Rates | Exchange Rate | Confirmed |
| View audit history | All ten domains | Confirmed |
| Import/migrate | All ten domains | Confirmed |

No Wafra-specific role is created; every capability is reusable across Tenants (MD-BR-008).

## 27. Approval Controls

**Classification: Confirmed (control only).** The generic control this BRD confirms is MD-BR-046:

> **Where an approved business policy requires separate approval, the requester may not self-approve and publication is blocked until the required approval exists.**

**No approved source in this repository establishes *which* master-data changes require a separate approver.** The PRD, the approved glossary, the approved adjacent BRDs, and the Founder Decision Pack all leave that catalogue unset. This BRD therefore does not decide it: blanket approval is not invented for every field change, and neither is a specific separate-approver rule for any individual domain. The full catalogue is Open Decision **MD-OD-005**.

| Change | Approval expectation | Classification |
|---|---|---|
| Tax-rate creation or change | **Candidate** for a separate-approver requirement; not established by any approved source. Decide in MD-OD-005. | Open Decision (MD-OD-005) |
| Published customer-facing commercial Price List change | **Candidate** for a separate-approver requirement; not established by any approved source. Decide in MD-OD-005. | Open Decision (MD-OD-005) |
| Manually entered Exchange Rate | The Founder Decision Pack **recommends** Finance approval (MESP-54, MD-BR-042). That recommendation is unapproved and owned by MESP-34; it is not a Release 1 requirement established here. | Deferred Gate / Recommended Default — not yet approved |
| Other sensitive master-data changes (Product Base Unit, Supplier/Business Customer bank or tax-registration detail, Payment Term, Currency activation) | Not established by any approved source. Decide in MD-OD-005. | Open Decision (MD-OD-005) |
| High-impact reference change (e.g., changing a Product's Base Unit before any stock transaction exists) | Requires review; exact policy owned jointly with MESP-33. | Deferred Gate |
| Routine identity/contact-detail edit (a Supplier's contact phone number, a Product's description) | Routine identity/contact-detail changes are recommended not to require separate approval; final policy is part of MD-OD-005. | Open Decision (MD-OD-005) |

Wherever an approved policy *does* require approval, MD-BR-046, MD-VR-011, and §28's no-self-approval boundary apply in full and without exception. Until MD-OD-005 is resolved, this BRD states that control and leaves its scope to Hossam rather than inventing one.

## 28. Separation of Duties

**Classification: Confirmed.** Following the SoD-versus-approval distinction already established in `docs/12_Identity_and_Access_BRD.md` §15.1:

| SoD concern | Required separation | Classification |
|---|---|---|
| Requester vs. approver, on any change requiring approval | Wherever an approved business policy requires a separate approver, the requester may not be that approver (MD-BR-046). This generic no-self-approval boundary is approved and applies unconditionally; which changes it attaches to is MD-OD-005. | Confirmed |
| Tax-rate maintainer vs. transaction poster | The person who changes a Tax rate is not required to also be the poster of transactions using it. Whether a tax-rate change requires a separate approver at all is MD-OD-005; if it does, self-approval is prohibited by the row above. | Confirmed boundary; approval requirement is Open Decision (MD-OD-005) |
| Price maintainer vs. sales approver | Whether publishing a customer-facing Price List entry requires a separate approver is MD-OD-005; if it does, the publisher may not be that sole approver. | Confirmed boundary; approval requirement is Open Decision (MD-OD-005) |
| Supplier maintainer vs. payment execution | Supplier master-data maintenance is separable by Permission from AP payment execution (Finance-owned, MESP-34); this BRD records the separation, not the payment mechanics. | Confirmed boundary |
| Customer maintainer vs. credit authorization | Business Customer master-data maintenance is separable by Permission from credit-limit authorization (Finance-owned, MESP-46). | Confirmed boundary |

This BRD does not over-design a role model; it records the required business separation and leaves technical enforcement to the later Lean Implementation Specification.

## 29. Tenant Isolation

**Classification: Confirmed — Founder-approved Release 1 requirement.** For every one of the ten domains in this BRD: ownership is explicit and belongs to exactly one Tenant; one Tenant cannot read or modify another Tenant's master data; duplicate checks, search, export, and import never leak another Tenant's values; background activity (import jobs, scheduled duplicate scans) remains Tenant-bound; audit evidence retains Tenant context. A Platform Administrator role does not, by itself, grant access to a Tenant's master data — the same MT-BR-009 boundary from `docs/13_Multi_Tenancy_BRD.md` applies unchanged here. Nothing in this BRD invents an exception.

## 30. Company / Legal Entity / Branch Scope

**Classification: Open Decision (MD-OD-001).** This section turns on a distinction the rest of this BRD keeps strictly separate, and which must not be collapsed:

| Question | Status |
|---|---|
| **1. Tenant security and data ownership** — which Tenant owns a master record, and who may ever read or modify it | **Confirmed and mandatory.** Every master record belongs to exactly one Tenant and is never readable or modifiable across a Tenant boundary (§29, MD-BR-001, MD-VR-010). This is settled by `docs/13_Multi_Tenancy_BRD.md` and is not reopened by MD-OD-001. |
| **2. Business availability and scope inside the owning Tenant** — whether a record is usable by every Company/Legal Entity in that Tenant, or is scoped to one Company or Branch | **Open Decision (MD-OD-001).** Unresolved for every domain in this BRD. |

**"Tenant-owned" does not mean "Tenant-wide usable by every Company."** A record can be owned by one Tenant and still be restricted, by approved business configuration, to a subset of that Tenant's Companies, Legal Entities, or Branches. Nothing in this BRD asserts otherwise, and no domain section above states a Company-level answer — each says "within its owning scope" precisely because scope is undecided.

The approved Organization BRD (`docs/14_Organization_and_Company_Structure_BRD.md`) confirms the hierarchy Platform → Tenant → Company/Legal Entity → Branch → Warehouse and confirms scope never inherits upward, but it is explicitly silent on which of these levels business-scopes Product, Price List, Tax, Payment Term, Currency, or Exchange Rate master data — the research for this BRD confirmed no existing approved BRD assigns it.

The recommended option, pending Hossam's decision, is that Product, Category, Unit of Measure, Supplier, Business Customer, Tax, Payment Term, and Currency are available Tenant-wide across the Tenant's Companies (the glossary already frames them as "defined once and reused across all transactions"), while Price List and Exchange Rate *may* need a Company-level restriction where a Tenant's Companies operate in different functional currencies (PLT-002 confirms a Tenant may configure more than one functional currency). **This is a recommendation only and is relied on nowhere else in this BRD.**

**No cross-Tenant shared business data is introduced by this BRD, under any scope option.** If a stable platform or country reference catalogue is ever discussed (for example ISO currency codes or a seeded Saudi VAT starting value), that is a *reference catalogue* a Tenant may draw on — the Tenant's own Currency, Tax, and other master records created from it remain Tenant-owned business configuration under §29. The approved Tenant model is not changed, narrowed, or reinterpreted here.

## 31. Downstream Domain Dependencies

### 31.1 Inventory impact (MESP-33)

**Classification: Confirmed boundary.** Inventory reads Product, Category (for default rules), and Unit of Measure (Base Unit, conversions) to move stock. This BRD defines their identity and active-state validity only; it does not design a stock ledger, costing method, or batch/lot/serial enforcement mechanism.

### 31.2 Purchasing / AP impact (MESP-32, MESP-34)

**Classification: Confirmed boundary.** Procurement reads Supplier, Product, Unit of Measure, Currency, Tax, and Payment Term to raise a Purchase Order; Finance reads the same facts to post an AP invoice. This BRD does not design a Purchase Order, a supplier-confirmation workflow, or an AP posting rule.

### 31.3 B2B Sales / AR impact (MESP-35, MESP-34)

**Classification: Confirmed boundary.** B2B Sales reads Business Customer, Product, Price List, Unit of Measure, Currency, Tax, and Payment Term to quote and invoice; Finance reads the same facts to post an AR invoice. This BRD does not design a Sales Order, an invoice, a receipt, or an AR posting rule.

### 31.4 Accounting impact (MESP-34)

**Classification: Confirmed.** Finance depends on this BRD for stable historical references: a posted journal entry must remain reproducible using the exact Currency, Exchange Rate, Tax rate, and Payment Term it applied, even after any of those master records later changes (MD-BR-003/033/036/039). This BRD defines no journal-entry structure or posting rule.

## 32. Multi-Currency Impact

**Classification: Confirmed.** Every downstream domain that carries a monetary amount depends on this BRD's Currency and Exchange Rate sections. Transaction currency, base/functional currency, and reporting currency are distinct usage roles already defined by the glossary and unaltered here; Price List currency must match the Currency it references (MD-VR-005); Supplier/Business Customer default Currency is optional master data, not a mandate to transact in that currency only. SAR is never hard-coded as the only supported currency (MD-BR-037).

## 33. Saudi Localization Impact

| Item | Classification |
|---|---|
| VAT baseline seeded at 15%, configurable, never hard-coded (KSA-002) | Confirmed |
| VAT/CR statutory registration-number requirement on Supplier/Business Customer | Confirmed (business fact — required where Saudi statutory rules apply) |
| Exact statutory field list beyond VAT registration (e.g., Commercial Registration number format) | Open Decision (MD-OD-007) |
| ZATCA e-invoicing coupling, tax-invoice/simplified-invoice/credit-note/debit-note generation detail | Deferred Gate — MESP-49 |
| Legal completeness of Saudi tax exemption/zero-rating rules beyond the Release 1 VAT baseline | External Validation Required |
| Future country-pack extensibility (a second country's tax/currency/party rules) | Confirmed — this BRD's configuration-led design does not block it |

## 34. Bilingual and RTL Requirements

**Classification: Confirmed.** Every business-facing master-data name (Product, Category, Unit of Measure, Supplier, Business Customer, Currency) supports Arabic and English capture, consistent with ADR-011's requirement that runtime localization, Arabic search, RTL, and bilingual document generation be resolved before module implementation and its explicit naming of MESP-31. Arabic-name mandatory-vs-optional distinction, where not already settled (e.g., internal-only codes), remains as stated per field in §§11–20; this BRD does not specify Angular layout, text direction rendering, or search-indexing mechanics — those are Lean Implementation Specification concerns.

## 35. Reports and KPIs

| Report | Business justification | Classification |
|---|---|---|
| Active/Inactive master-data listing, per domain | Operational maintenance visibility. | Confirmed |
| Incomplete master-data records (missing required field) | Data-quality control before use in a transaction. | Confirmed |
| Duplicate-candidate report | Supports MD-BR-005 duplicate control. | Confirmed |
| Upcoming effective-dated changes (Tax, Price List, Exchange Rate) | Lets Finance/Sales/Tax owners review before a change takes effect. | Confirmed |
| Exchange-rate history | Supports MD-BR-039 historical reproducibility review. | Confirmed |
| Master-data audit-change report | Supports §36 Audit Evidence. | Confirmed |

No dashboard implementation or visualization design is specified.

## 36. Audit Evidence

**Classification: Confirmed.** For every sensitive master-data change — create, edit, activate, deactivate, and any effective-dated rate/price change — the following must be reconstructable: Tenant, actor, action, affected master record, previous business value where required, new business value, effective date where applicable, timestamp, approver identity where §27 required one, and reason where required. No physical audit table or storage mechanism is specified; this is the same evidence pattern already approved in `docs/13_Multi_Tenancy_BRD.md` §20 and the PRD's PLT-008.

## 37. Integration Requirements

| Boundary | Classification |
|---|---|
| Future Product/Supplier/Customer import from an external catalog or accounting system | Deferred — future integration |
| Tax-reference feed from a government/ZATCA source | Deferred — MESP-49 |
| Exchange-rate provider feed | Deferred — MESP-34, superseding the manual MESP-54 default only when separately approved |
| Accounting-system dependency for Currency/Payment Term/Tax at posting time | Release 1 required — internal, MESP-34 |

No vendor is selected; no API contract is specified.

## 38. Migration Requirements

**Classification: Confirmed**, except the MESP-51 scope decision itself. The Founder Decision Pack **recommends** (MESP-51, unapproved, owned by MESP-40) that master data migrates together with reconciled opening balances; this BRD records that recommendation without approving it. What *is* Confirmed below are the business migration expectations traceable to PRD BR-013 and ADM-003. Distinct from technical ETL implementation (§5):

| Requirement | Classification |
|---|---|
| Named source ownership per domain | Confirmed |
| Field-level mapping from source to this BRD's data requirements (§§11–20) | Confirmed |
| Duplicate detection consistent with MD-BR-005 | Confirmed |
| Arabic/English data normalization | Confirmed |
| Currency, Tax, Price List, and UOM-conversion migration validated before opening-balance load (PRD §19.1 ordering: configuration and master data before open documents and opening balances) | Confirmed |
| Preview and validation reports before commit; immutable batch identifier; row-level result | Confirmed (BR-013, ADM-003) |
| Rejected-row handling and quarantine for ambiguous mappings | Confirmed |
| Reconciliation and accountable sign-off before go-live | Confirmed |
| Audit evidence of the migration batch itself | Confirmed |

No migration script, staging schema, or tool is specified.

## 39. Business Acceptance Scenarios

These are business acceptance scenarios, not automated tests.

**Product**

- MD-AC-001 — Given an authorized Master Data Maintainer, when they create a Product with a unique code, an active Category, and an active Base Unit, then the Product is created Active with full audit evidence.
- MD-AC-002 — Given a Product code that already exists in the Tenant, when a User attempts to create a duplicate, then the system holds it for reviewed resolution instead of creating a second identity.
- MD-AC-003 — Given a Product already referenced by a posted Sales Order, when an authorized actor deactivates it, then the Product becomes unselectable for new transactions and the posted Sales Order is unchanged.
- MD-AC-004 — Given a Product referenced by a posted transaction, when any User attempts to delete it, then the deletion is denied and only deactivation is offered.

**Product Category**

- MD-AC-005 — Given an active Category with Products assigned, when an authorized actor deactivates the Category, then new Product assignment is blocked but existing assignments are unaffected.

**Unit of Measure**

- MD-AC-006 — Given a Product with an assigned Base Unit and no stock transactions yet, when an authorized actor changes the Base Unit, then the change is permitted subject to §27 review.
- MD-AC-007 — Given an alternate Unit of Measure conversion factor of zero, when a User attempts to save it, then the system rejects the value.

**Supplier**

- MD-AC-008 — Given a new Supplier record, when it is created, then no system User account, login, or credential is created for it.
- MD-AC-009 — Given an existing **Supplier** with the same tax registration number, when a User attempts to create a new Supplier with that number, then the system holds it for reviewed duplicate resolution. (For a match against a *Business Customer* instead, see MD-AC-035.)
- MD-AC-010 — Given a Supplier referenced by historical Purchase Orders, when an authorized actor deactivates it, then new Purchase Order creation against it is blocked and historical Purchase Orders remain unchanged.

**Business Customer**

- MD-AC-011 — Given a request to record a retail walk-in consumer, when a User attempts to create it as a Business Customer, then the system rejects it as outside Release 1's B2B-only scope.
- MD-AC-012 — Given a Business Customer referenced by historical Sales Orders, when an authorized actor deactivates it, then new Sales Order creation against it is blocked and historical Sales Orders remain unchanged.

**Price List**

- MD-AC-013 — Given an active Price List in SAR, when a User attempts to add a price line in USD, then the system rejects the mismatch.
- MD-AC-014 — Given two Price List entries for the same Product, Customer segment, and Currency with overlapping effective dates, when a User attempts to publish the second, then the system rejects it pending an approved precedence rule.
- MD-AC-015 — Given a Price List referenced by a posted Sales invoice, when an authorized actor deactivates it, then the posted invoice's recorded price is unchanged.

**Tax**

- MD-AC-016 — Given a Tenant with the Saudi VAT baseline seeded at 15%, when an authorized actor publishes a new effective-dated tax rule after satisfying any approval policy applicable under MD-OD-005, then it applies only to transactions dated on or after its effective date.
- MD-AC-017 — Given a posted invoice that applied a 15% VAT rate, when the Tax master record is later changed to a different rate, then the posted invoice continues to show 15% unchanged.
- MD-AC-018 — Given an approved business policy that requires a Tax-rate change to be separately approved (MD-OD-005), and a change requested by a Maintainer who is also the sole Approver, when they attempt to self-approve, then the approval is denied and publication stays blocked until a distinct approver approves it. Where no approved policy requires separate approval for that change, this scenario does not apply.

**Payment Terms**

- MD-AC-019 — Given a Payment Term assigned to a Supplier, when an authorized actor deactivates the term, then documents already posted using it keep their original due-date meaning.

**Currency**

- MD-AC-020 — Given a Tenant operating only in SAR today, when an authorized actor adds a second active Currency, then the Tenant can immediately reference it on new Price Lists, Suppliers, and Business Customers.
- MD-AC-021 — Given a Currency referenced by an active Price List, when a User attempts to delete it, then the deletion is denied and only deactivation is offered.

**Exchange Rate**

- MD-AC-022 — Given no exchange rate exists for USD-to-SAR on a transaction's date, when a User attempts to post a USD transaction, then posting is blocked rather than defaulting to an assumed rate.
- MD-AC-023 — Given a posted transaction that applied an exchange rate of 3.75, when a new effective-dated rate of 3.80 is later published, then the posted transaction continues to reflect 3.75 unchanged.
- MD-AC-024 — Given an approved business policy that requires a manually entered exchange rate to be separately approved, when the entering User attempts to also approve it, then self-approval is denied. Whether exchange rates require Finance approval at all is the unapproved MESP-54 recommendation owned by MESP-34 (§20, MD-BR-042); this scenario tests the generic no-self-approval control (MD-BR-046), not an adopted MESP-54 requirement.
- MD-AC-025 — Given a duplicate exchange-rate entry for the same currency pair and effective date, when a User attempts to save it, then the system rejects it.

**Lifecycle / cross-cutting**

- MD-AC-026 — Given any of the ten master-data domains, when an authorized actor deactivates a record with zero existing references, then the record becomes **Inactive and unselectable for new use**, remains visible for historical reference and reporting, and has no other side effects.
- MD-AC-027 — Given a deactivated Tax rate that would need reactivation to alter already-recorded effective-dated history, when an actor attempts reactivation, then the system denies it and requires a new effective-dated entry instead.

**Tenant isolation**

- MD-AC-028 — Given two Tenants each with a Product coded "SKU-100", when a User in Tenant A searches for duplicates, then Tenant B's identical code is never shown or matched against.
- MD-AC-029 — Given a Platform Administrator with no Tenant-business-data grant, when they attempt to view a Tenant's Supplier list, then access is denied by default.

**Bilingual / multi-currency / audit**

- MD-AC-030 — Given a Product created with only an English name where Arabic is required for the Tenant, when the User attempts to save it, then the system requires the Arabic name before completion.
- MD-AC-031 — Given a Business Customer with a default Currency of EUR trading with a Tenant whose base currency is SAR, when a Sales invoice is issued in EUR, then both the EUR transaction amount and its SAR-converted equivalent are retained.
- MD-AC-032 — Given any create/deactivate/rate-change event across the ten domains, when an auditor requests reconstruction, then actor, Tenant, before/after value, and timestamp are all retrievable.

**Migration**

- MD-AC-033 — Given a migration batch with an ambiguous Supplier-to-tax-registration mapping, when the batch runs, then the ambiguous rows are quarantined rather than committed.
- MD-AC-034 — Given a completed master-data migration dry run, when the accountable owner reviews the reconciliation report, then sign-off is required before the batch is committed to production data.

**Business Party cross-role identity**

- MD-AC-035 — Given an existing **Business Customer** whose tax registration number matches, when a User creates a **Supplier** record for that same legal company, then the creation succeeds; the cross-role match is surfaced for review and optional linkage and is never treated as a blocking duplicate (MD-BR-045, MD-VR-014). The reverse case — creating a Business Customer for an existing Supplier's legal company — behaves identically.

## 40. Open Decision Register

**What is and is not in this register.** It contains only genuine **business** decisions that Hossam owns for MESP-31 — eleven of them, MD-OD-001 through MD-OD-011. It deliberately excludes three other categories, which are recorded in their own sections rather than inflating this register:

- **Decisions owned by a later domain BRD** (MESP-32/33/34/35/49 and MESP-46) are marked *Deferred Gate* where they arise — for example exchange-rate sourcing and Finance approval (MESP-54, owned by MESP-34, §20/MD-BR-042), Credit Limit mechanics (MESP-46, §15), due-date calculation (MESP-34, §18), and pricing precedence inside a live Sales Order (MESP-35, §16). MESP-54 in particular is **not** listed here as an Owner decision for MESP-31 and is not approved by this BRD.
- **Decisions requiring external Saudi legal/tax validation** are marked *External Validation Required* (§33), not carried here as if Hossam could resolve them alone. MD-OD-007 appears here only for the part that is genuinely his — which statutory fields Release 1 collects — with external validation noted.
- **Implementation and architecture decisions** (schema, API, UI, indexing, rounding algorithms as code, search mechanics) are Out of Scope per §5 and belong to the later Lean Implementation Specification, not to a BRD.

None of these decisions is closed by this document. Hossam resolves each one.

| ID | Question | Why it matters | Impacted domains | Recommended option | Owner | Blocking? | Target decision point |
|---|---|---|---|---|---|---|---|
| MD-OD-001 | Inside the owning Tenant, at which organizational level is each master-data domain **available for business use** — Tenant-wide across all Companies, or scoped to a Company/Legal Entity (or Branch)? *This decides business availability only; the Tenant ownership and isolation boundary is already Confirmed and is not in question (§29, §30).* | Determines whether a Tenant with multiple Companies shares one Product/Price List/Tax catalogue or maintains separate ones per Company. | All ten domains | Tenant-wide availability for Product, Category, UOM, Supplier, Business Customer, Tax, Payment Term and Currency; consider a Company-level restriction for Price List and Exchange Rate where functional currencies differ. No cross-Tenant sharing under any option. | Hossam | Yes — blocks Lean Implementation Specification data-scope decisions | Before MESP-31 approval or immediately after, before any Lean Implementation Specification work |
| MD-OD-002 | Is Product Category a flat list or a multi-level hierarchy? | Determines Category data structure and reporting rollups. | Product Category | Not recommended without further evidence; defer to Hossam. | Hossam | No — Release 1 can launch with a flat structure and add hierarchy later without data loss | Before MESP-32/33/35 Lean Implementation Specifications that rely on Category defaults |
| MD-OD-003 | What is the Product SKU/Barcode coding-rule structure — auto-generated, manual, or hybrid; what format? | Affects duplicate detection and migration mapping. | Product | Not recommended without further evidence; defer to Hossam. | Hossam | No | Before MESP-31 implementation phase begins |
| MD-OD-004 | What precedence rule resolves two applicable Price Lists for the same Product/Customer/Currency? | Directly affects which price a Sales Order uses. | Price List, B2B Sales (MESP-35) | Not recommended without further evidence; defer to Hossam and MESP-35. | Hossam / MESP-35 owner | Yes — blocks MESP-35 pricing logic | Before MESP-35 BRD drafting |
| MD-OD-005 | **Which master-data changes require a separate Approver distinct from the requester?** No approved source establishes any specific separate-approver rule, so this covers the whole catalogue and nothing in it is pre-decided: (a) **Tax** creation and rate/effective-date changes; (b) **commercial Price List** changes, in particular publishing a customer-facing price; (c) **other sensitive master-data changes** — Product Base Unit or tax classification, Supplier/Business Customer tax-registration or bank detail, Payment Term terms, Currency activation, and Exchange Rate entry (noting that the exchange-rate recommendation is the unapproved MESP-54 default owned by MESP-34, MD-BR-042). | Determines the full approval-control catalogue. The generic control (MD-BR-046: no self-approval, publication blocked until the required approval exists) is already Confirmed and applies to whatever this decision selects — only the catalogue is open. | All ten domains | Not recommended without further evidence; defer to Hossam. This BRD deliberately confirms no specific approval rule. | Hossam | Yes — §27, §28, MD-VR-011 and the Lean Implementation Specification's authorization design cannot be finalized without it | Before MESP-31 approval, and before the Lean Implementation Specification's authorization design |
| MD-OD-006 | What rounding/precision rule applies to a Unit of Measure conversion? | Affects quantity accuracy across Purchasing, Inventory, and Sales. | Unit of Measure, Inventory (MESP-33) | Not recommended without further evidence; defer to Hossam and MESP-33. | Hossam / MESP-33 owner | No | Before MESP-33 BRD drafting |
| MD-OD-007 | Beyond VAT registration number, which Saudi statutory fields (e.g., Commercial Registration number) are mandatory on Supplier/Business Customer for Release 1? | Affects required-field validation and migration mapping. | Supplier, Business Customer | Not recommended without further evidence; External Validation Required from Saudi legal/tax authority. | Hossam, with external validator | No — this decision does not block MESP-31 BRD approval or the bounded Master Data implementation baseline. Production launch remains gated by MESP-49 and qualified Saudi legal/tax validation of the required statutory fields and tax treatment. | Before MESP-49 Saudi Country Pack BRD drafting |
| MD-OD-008 | Does any master-data domain require a Draft-before-Active state for Release 1? | Determines whether §10's two-state lifecycle is complete, and whether the creation process (§23.1) has a review step before a record becomes usable. | All ten domains | **No Draft state for Release 1; successful validated creation becomes Active.** This is a recommendation, not a confirmed requirement — §10 is written on it and must be revised if Hossam decides otherwise. | Hossam | No | At BRD approval |
| MD-OD-009 | Can a deactivated effective-dated record (Tax, Exchange Rate, Price List) ever be reactivated, or must a new effective-dated entry always be created instead? | Affects the exact reactivation guard in MD-BR-004/MD-VR-012. | Tax, Exchange Rate, Price List | Recommend: reactivation is allowed only when it introduces no new effective-dated value into already-posted history; otherwise require a new entry. | Hossam | No | Before the Lean Implementation Specification's lifecycle design |
| MD-OD-010 | Should Release 1 adopt the MESP-41 default that batch/lot/serial/expiry tracking is configurable per Product or Category, disabled by default, and enforced end-to-end only when enabled? | MESP-41 is an **unapproved recommended default** in `docs/90_MVP_Founder_Decision_Pack.md`, jointly owned by MESP-31 and MESP-33 and explicitly requiring Hossam's approval during its owning domain BRD(s). It determines whether Product identity/configuration carries a tracking setting at all, and therefore what Inventory must enforce. This BRD invents no batch/lot/serial/expiry behavior pending the decision. | Product, Product Category; jointly with **MESP-33 Inventory** | Use the existing Founder Decision Pack default (configurable per Product or Category; disabled by default; enforce end-to-end when enabled). | Hossam | **Yes** — must be resolved before the Master Data implementation baseline is finalized, because it affects Product identity/configuration and Inventory integration | Before the Master Data implementation baseline is finalized; jointly with MESP-33 Inventory BRD drafting |
| MD-OD-011 | Should Release 1 treat Product and Item as one business concept with no separate product-variant layer? | The approved glossary marks Item, SKU, and Barcode "Draft for BRD Validation" and explicitly defers Product-versus-variant modelling to this BRD. This decision determines the Product identity model every downstream domain (Procurement, Inventory, B2B Sales, Finance) reads as a fact; changing it after implementation begins would be a structural rework, not a configuration change. | Product; every downstream consumer of Product identity | **Yes.** For Release 1, Product and Item are one concept. Do not introduce a separate variant/product-family model. SKU and Barcode identify the Product/Item according to the separately approved coding rules (MD-OD-003). | Hossam | **Yes** — must be decided before Master Data implementation because it determines the Product identity model | Before the Master Data implementation baseline is finalized |

## 41. Owner Authorizations and Approval Recorded in This BRD

**Classification: Confirmed — Founder-approved Release 1 requirement.** Two separate Owner decisions are recorded as of 8 August 2026, **both recorded in live Jira on MESP-31**:

1. **BRD-entry authorization — Jira comment `10615`.** Hossam approved beginning MESP-31 — Master Data and Product Catalog BRD drafting, including the explicit ten-domain scope mandate (Products, Categories, Units of Measure, Suppliers, Business Customers, Price Lists, Taxes, Payment Terms, Currencies, Exchange Rates) that this BRD covers. This satisfies the distinct-authorization precedent for MESP-31 BRD entry (the MESP-29 precedent recorded in `.ai/CURRENT_STATE.md`'s "MESP-31 BRD entry eligibility" section, requiring an explicit owner authorization statement beyond Foundation completion alone).
2. **Future implementation authorization — Jira comment `10616`.** Hossam has also explicitly pre-authorized the later Master Data implementation phase. That authorization is recorded, but **it remains conditional and is not yet executable**: implementation cannot start until (a) this BRD is completed, reviewed, and explicitly approved by Hossam as a business baseline, and (b) a dedicated implementation Jira item is identified and activated, separate from MESP-31 and separate from any other implementation item.

3. **Approved Business Baseline — Jira comment `10649`.** On 8 August 2026, Hossam approved MESP-31 BRD v0.3 as the Release 1 Master Data and Product Catalog business baseline at the exact reviewed content head `1e2d055354f0ddde833190948d09fa426707484c`. This approval covers the document as reviewed; it does **not** silently answer MD-OD-001 through MD-OD-011. The Open Decision Register remains preserved, and any decision marked blocking remains an implementation-slice gate. Future implementation is authorized only subject to the normal Definition of Ready and the separately active implementation-readiness item.

**No implementation was performed in the drafting or correction of this BRD.** No Master Data source code, EF Core entity, migration, SQL table, repository, application service, API endpoint, controller, DTO, Angular screen, or database was created. The `MESP` local SQL Server database was not created.

## 42. Source Conflicts and Corrections

| Conflict | Resolution | Classification |
|---|---|---|
| The task brief that requested this BRD cited PRD anchors PLT-011 through PLT-014 and BR-004 as MESP-31's traceability. Direct extraction of `docs/MESP_PRD_v1.2.docx` shows these are Platform Administration anchors (tenant provisioning, subscriptions/entitlements, tenant branding, no-tenant-specific-code) already owned by the approved MESP-27 BRD. | **Resolved.** MESP-31's Jira Source Baseline has been corrected to primary anchor PLT-003 with supporting anchors PLT-002, SAL-001, PROC-002, PROC-008, FIN-001, FIN-003, FIN-007, FIN-010, KSA-002, BR-013, ADM-003 and the applicable PRD RULE set for master-data integrity. PLT-011–PLT-014 and BR-004 are no longer listed as MESP-31's baseline. Jira and this BRD now agree. | Confirmed — closed |
| `docs/90_MVP_Founder_Decision_Pack.md` records MESP-41, MESP-51 and MESP-54 as recommended defaults. v0.1 of this BRD presented the MESP-41 and MESP-54 defaults as though they were already confirmed Release 1 requirements. | **Corrected in v0.2.** The pack's §4 legend marks only MESP-52 and MESP-56 as APPROVED; every other row is an unapproved default that "must decide during its owning domain BRD". MESP-41 is MESP-31's own owning-BRD decision and is now Open Decision **MD-OD-010**; MESP-54 is now classified *Deferred Gate / Recommended Default — not yet approved*, owned by Finance/MESP-34 (§20, MD-BR-042, §27); MESP-51 is described as the pack's recommendation in §38 rather than an approved requirement. No batch/lot/serial/expiry or exchange-rate-sourcing behavior is specified anywhere in this BRD. | Confirmed correction |
| v0.1 of this BRD declared in §27 that Tax-rate changes and published customer-facing Price List changes each require one separate Approver, classified Confirmed. No approved source — PRD, glossary, approved adjacent BRD, or Founder Decision Pack — establishes either rule. | **Corrected in v0.2.** Both are withdrawn from Confirmed status and folded into Open Decision **MD-OD-005**, which now explicitly covers Tax changes, commercial Price List changes, and other sensitive master-data changes. What remains Confirmed is only the generic control, MD-BR-046: where an approved business policy requires separate approval, the requester may not self-approve and publication is blocked until the required approval exists. The approved SoD/no-self-approval boundary from `docs/12_Identity_and_Access_BRD.md` is preserved in full (§28). | Confirmed correction |
| v0.1 stated in §10 as Confirmed that no master-data domain requires Draft-before-Active for Release 1, while MD-OD-008 simultaneously listed the same question as an Open Decision. | **Corrected in v0.2.** Treated consistently as an Open Decision. The recommended option — no Draft state for Release 1; successful validated creation becomes Active — is retained as a recommendation, and §10 states plainly that it must be revised if Hossam decides otherwise. | Confirmed correction |
| The glossary assigns "Owning module" for Supplier, Business Customer, Price List, Tax Category, Payment Terms, and the Currency/Exchange Rate family to Procurement, B2B Sales, Finance, or the Saudi Country Pack — not to "Master Data and Catalog" — yet MESP-31's confirmed scope (§4, per Owner instruction) requires this BRD to define all of them. | §9 Ownership Boundaries resolves this as a two-layer split: MESP-31 owns the master-record identity/lifecycle layer; the named glossary module remains authoritative for transactional behavior. No glossary "Owning module" field is silently overwritten by this BRD. | Confirmed resolution |
| The glossary has no standalone "Business Party," "Currency," or "Tax" entry, and no generic "Active/Inactive" entry, even though this BRD needs all four as controlled cross-cutting terms. | New glossary entries are proposed for these four terms (see §8); each is additive and does not change any existing entry's approved definition, owning module, or approval status. | Confirmed — glossary addition recommended |
| The proposed "Business Party" term could be read as implying that a matching tax registration number across the Supplier and Business Customer roles blocks the second role — which would contradict the approved glossary's Supplier entry, stating that the same legal company may legitimately exist as both records. | **Corrected in v0.2.** Duplicate detection is scoped **within a party role**; a cross-role identity match is surfaced for review and optional linkage and never auto-rejects the second role (MD-BR-045, MD-VR-014, MD-AC-035, §14, §15). The glossary's Business Party entry is corrected to say the same. Supplier and Business Customer remain distinct business roles with distinct records; **no unified Party record or party-unification design is introduced**, and any such change would require a separate approved decision. | Confirmed correction |
| The glossary's Item, SKU, Barcode, and Category entries are marked "Draft for BRD Validation," explicitly deferring confirmation to this BRD (MESP-31). | This BRD does not confirm the Product/Item identity model, SKU coding rules, Barcode identity, or Category hierarchy depth; each is raised as an Open Decision rather than guessed — Product/Item modelling is **MD-OD-011**, SKU/Barcode coding is **MD-OD-003**, and Category hierarchy depth is **MD-OD-002**. Their glossary "Approval status" should move to "Approved Product Baseline" only after Hossam resolves each Open Decision. | Open Decision — deferred pending Hossam |
| v0.2 of this BRD classified MD-BR-015 (Release 1 treats Product and Item as one concept, no separate variant layer) as Confirmed, even though the approved glossary marks Item, SKU, and Barcode "Draft for BRD Validation" and explicitly defers Product-versus-variant modelling to this BRD. | **Corrected in v0.3 (M31-R10).** MD-BR-015 is withdrawn from Confirmed status and raised as new Open Decision **MD-OD-011**, with the same one-concept, no-variant-layer position carried forward only as the recommended option pending Hossam's approval. §11, §8, §42, and §43 are updated to match; no variant/product-family behavior is invented. | Confirmed correction |
| v0.2 of this BRD classified a routine identity/contact-detail edit as "No approval required — Confirmed" (§27) and had MD-AC-016 assume an "authorized Approver" publishes a Tax exemption rule, even though §27 itself states no approved source establishes any separate-approval catalogue (that catalogue is Open Decision MD-OD-005). | **Corrected in v0.3 (M31-R11).** The §27 routine-edit row is restated as a recommendation whose final policy is part of MD-OD-005, and reclassified Open Decision (MD-OD-005). MD-AC-016 is reworded to an authorized actor publishing a new effective-dated tax rule "after satisfying any approval policy applicable under MD-OD-005," removing the residual assumption that a dedicated Approver role or a specific approval requirement already exists. | Confirmed correction |
| v0.2 of this BRD listed MD-OD-007 (Saudi statutory fields beyond VAT registration) as non-blocking with the rationale "can launch with VAT registration only and add fields later" — a production-compliance claim this BRD's business-analysis scope cannot make ahead of external Saudi legal/tax validation. | **Corrected in v0.3 (M31-R12).** The blocking rationale now distinguishes BRD approval and the bounded Master Data implementation baseline (not blocked by MD-OD-007) from production launch, which remains gated by MESP-49 and qualified Saudi legal/tax validation of the required statutory fields and tax treatment. The **External Validation Required** classification is preserved; no claim is made that VAT registration alone is legally sufficient for launch. | Confirmed correction |
| The PR #28 branch delta included `.vscode/settings.json` (added by unrelated commit `c5506e1`, a local Bitbucket-integration editor setting), which has no business-requirements content and does not belong in a documentation-only BRD Pull Request. | **Corrected in v0.3 (M31-R13).** `.vscode/settings.json` is removed from the PR #28 branch delta; the correction commit deletes the file so the cumulative diff against `origin/main` no longer contains it. The setting was not altered globally — only its presence in this PR is corrected. | Confirmed correction |

## 43. Coverage Checklist

| Domain | Coverage | Classification |
|---|---|---|
| Products | Complete (Product/Item identity model is Open Decision MD-OD-011; batch/lot/serial/expiry scope is Open Decision MD-OD-010; SKU/Barcode coding is MD-OD-003) | Confirmed |
| Product Categories | Complete (hierarchy depth Open Decision) | Confirmed |
| Units of Measure | Complete (rounding precision Open Decision) | Confirmed |
| Suppliers | Complete | Confirmed |
| Business Customers | Complete (Credit Limit explicitly Out of Scope, owned by MESP-46) | Confirmed |
| Price Lists | Complete (precedence rule Open Decision) | Confirmed |
| Taxes | Complete (Saudi statutory detail beyond VAT baseline is External Validation Required / Deferred to MESP-49) | Confirmed |
| Payment Terms | Complete (due-date structure detail Deferred to MESP-34) | Confirmed |
| Currencies | Complete | Confirmed |
| Exchange Rates | Complete (rate sourcing, Finance approval and automated feeds are the unapproved MESP-54 default, Deferred to MESP-34) | Confirmed |
| Multi-Tenant isolation | Verified consistent with `docs/13_Multi_Tenancy_BRD.md` throughout | Confirmed |
| No Wafra hard-coding | Verified — MD-BR-008 and no domain section names Wafra | Confirmed |
| Suppliers are external parties, never Users | Verified — MD-BR-022, MD-AC-008 | Confirmed |
| B2B Business Customer boundary | Verified — MD-BR-025, MD-AC-011 | Confirmed |
| Deactivate-not-delete rule | Verified across all ten domains — MD-BR-002/003/012/013/024/027/031/034/038 | Confirmed |
| Effective-dated tax rule | Verified — MD-BR-032/033 | Confirmed |
| Multi-currency preserved | Verified — MD-BR-037, §32 | Confirmed |
| Saudi localization boundaries | Verified — §33 | Confirmed |
| Bilingual/RTL requirements | Verified — §34, ADR-011 | Confirmed |
| No unapproved Founder Decision Pack default presented as approved | Verified — MESP-41 is MD-OD-010; MESP-54 is Deferred Gate / Recommended Default (§20, MD-BR-042, §27); MESP-51 is described as a recommendation (§38). Only MESP-52 and MESP-56 are APPROVED in the pack, and neither is claimed here. | Confirmed |
| No unsupported approval rule presented as Confirmed | Verified — §27 confirms only the generic MD-BR-046 control; Tax, Price List and other sensitive-change approval requirements are Open Decision MD-OD-005 | Confirmed |
| Tenant isolation kept separate from Company/Legal Entity business scope | Verified — §2, §21, §29 (isolation Confirmed and mandatory) vs §30, MD-OD-001 (business scope undecided); no cross-Tenant shared business data introduced | Confirmed |
| Supplier and Business Customer remain distinct roles | Verified — MD-BR-045, MD-VR-014, MD-AC-035; no unified Party record introduced | Confirmed |
| No contradictory Confirmed + Open Decision classification | Verified — MD-OD-008 (Draft-before-Active) and MD-OD-011 (Product/Item modelling, MD-BR-015) are each treated consistently as an Open Decision throughout, not simultaneously Confirmed | Confirmed |

**Register totals (v0.3):** 46 business rules (MD-BR-001–046); 35 acceptance scenarios (MD-AC-001–035); 14 validation rules (MD-VR-001–014); **11 Open Decisions (MD-OD-001–011)**; 1 Deferred-Gate-heavy domain (Tax/Saudi statutory detail, External Validation Required); 0 rules or scenarios silently invented without a cited source.

## 44. Review and Approval Status

**Classification: Confirmed.** This document is an **Approved Business Baseline**. Hossam approved MESP-31 BRD v0.3 on 8 August 2026, with Jira approval evidence in comment `10649`, at the exact reviewed content head `1e2d055354f0ddde833190948d09fa426707484c`. The approval preserves the Open Decision Register MD-OD-001 through MD-OD-011; it does not silently answer any of those decisions. Decisions marked blocking remain implementation-slice gates. Approval of the BRD does not itself move MESP-31 to Done or start source implementation; future implementation remains subject to the normal Definition of Ready and a separately active implementation-readiness item.

**Historical pre-merge Jira position (superseded).** The preceding status
narrative records the position before PR #28 and PR #29 were merged. The
current live position is MESP-31 **Done**, MESP-95 **Done** with closure
evidence `10654`, and MESP-96 **In Progress** for contract-only/non-persistent
M95-SL-01. PR #29's approved head is
`c465d660e49a254f2fffbb95e0d07c5fcf17a193` and its actual merge commit is
`93f4e83992ef46f498cfbfacbb513cfc3d8dda7d`. MD-OD-001 through MD-OD-011
remain unresolved, and no Master Data persistence exists.

**Review history.** v0.1 was published as PR #28 at head `6d0aa80eef0a2860c85a141dd6f13ee38bf5760d` and received a business-requirements review verdict of *CHANGES REQUIRED BEFORE OWNER APPROVAL / MERGE*. v0.2 is the corrected draft: MESP-41 reclassified as an unapproved recommended default and raised as MD-OD-010; MESP-54 reclassified as a Deferred Gate / Recommended Default owned by MESP-34; unsupported Tax and Price List separate-approver claims withdrawn into MD-OD-005 behind the generic MD-BR-046 control; MD-OD-008 restored to a genuine Open Decision; MD-AC-026's lifecycle wording corrected; Business Party cross-role duplicate semantics clarified (MD-BR-045); and the Tenant isolation boundary separated from the undecided Company/Legal Entity business scope. v0.2 was reviewed at head `865701128c86d358f6aa919162c91d91ae025f21` and received a further verdict of *CHANGES REQUIRED — FINAL SMALL CORRECTION ROUND* (M31-R10 through M31-R13). v0.3 is this corrected draft: MD-BR-015's Product/Item modelling withdrawn from Confirmed status and raised as new Open Decision MD-OD-011 (M31-R10); §27's routine-edit row and MD-AC-016 reworded to remove a residual approval assumption not established by any approved source, both made explicitly MD-OD-005-dependent (M31-R11); MD-OD-007's blocking rationale corrected to distinguish BRD/implementation-baseline non-blocking from a still-gated production launch, preserving External Validation Required (M31-R12); and the unrelated `.vscode/settings.json` change removed from the PR #28 branch delta (M31-R13). The Open Decision register now holds eleven decisions (MD-OD-001–011). The review chronology is preserved; the approval overlay below supersedes the former pending-approval status.

**Approval overlay — 8 August 2026.** Hossam approved v0.3 as the Release 1 **Approved Business Baseline** in Jira comment `10649` at reviewed content head `1e2d055354f0ddde833190948d09fa426707484c`. The approval leaves MD-OD-001 through MD-OD-011 open and governed; it answers none of them. Blocking decisions remain gates for the affected implementation slices. PR #28 is approved for merge but remains unmerged until the approval-state reconciliation is pushed and reverified. No Master Data source implementation has started.

## Post-approval Category/UOM Owner-decision overlay — 9 August 2026

This overlay records a later, scope-limited Owner decision package for the
dedicated M95-SL-02 Category and Unit of Measure slice. It is a reconciliation
after approval of the v0.3 business baseline; it does not rewrite the
historical requirements or silently close the complete Open Decision Register.
The activation and decision evidence is Jira MESP-100 comment `10662`, and the
implementation item is MESP-99. The five dispositions below apply only to
Category and UOM. The same MD-OD identifier remains governed and unresolved
for every other Master Data domain unless its own Owner decision is recorded.

### MD-OD-001 — Category/UOM business availability

For Release 1, Category and UOM are Tenant-wide inside the owning Tenant. They
are defined once for that Tenant and are reusable by all Companies and Branches
inside it. There is no cross-Tenant sharing, and no client-selected Tenant or
scope hint can replace or broaden trusted server-derived authority. This is a
Category/UOM business-availability bound for MESP-99, not a global Tenant-wide
fallback for Product, Supplier, Customer, Price List, Tax, Currency, Exchange
Rate, or later slices.

### MD-OD-005 — Category/UOM approval catalogue

Routine Category/UOM Create, Edit, Activate, Deactivate, and Reactivate
operations do not require a separate approver in Release 1. A valid permission,
server-derived Tenant authority, correct resource/scope authorization, and
audit evidence remain mandatory. The generic approval, no-self-approval, and
fail-closed framework remains available for a future approved policy. This
disposition does not establish approval behavior for another Master Data
domain.

### MD-OD-008 — Category/UOM lifecycle

Category and UOM use no Draft lifecycle in Release 1. A valid authorized record
is created Active and may later be Deactivated or Reactivated according to the
approved permission, Tenant/scope, and audit controls. This is a Category/UOM
bound and does not resolve lifecycle behavior for another domain.

### MD-OD-002 — Category hierarchy

Category supports an optional parent and a maximum depth of three levels. A
parent must belong to the same Tenant and cycles are forbidden. The maximum-
depth policy must remain configuration-led/evolvable so changing the policy
does not require a schema redesign. This hierarchy bound applies only to the
Category implementation in MESP-99.

### MD-OD-006 — UOM precision and rounding

For Category/UOM Release 1 behavior, quantity values support six decimal
places, conversion factors support eight decimal places, and conversion
factors must be positive and non-zero. Calculated quantities round to six
decimal places using `MidpointRounding.AwayFromZero`. User-entered values that
exceed supported precision are rejected rather than silently rounded. This is
not a precision or rounding decision for any later domain.

The original MD-OD-001 through MD-OD-011 register remains preserved for
traceability. The MESP-99 task must carry these five Category/UOM bounds into
its implementation and must leave Product/Item, SKU/Barcode, tracking,
Supplier, Business Customer, Price List, Tax, Currency, Exchange Rate, and
production-gate decisions outside this scope.

## M95-SL-03 Product-only Owner-decision overlay — 9 August 2026

This overlay records Hossam's later Product-only decision package for the
M95-SL-03 Product identity readiness gate. It is evidence for the affected
Product slice and does not rewrite the approved v0.3 historical requirements or
globally close the Open Decision Register. The dedicated readiness item is
MESP-101. Owner evidence is Jira comment `10671`.

### MD-OD-011 — Product versus Item

For Release 1, Product and Item are one business concept. No separate
Product/Item model, variant layer, or product-family layer is introduced. No
future-looking variant architecture is added without a later explicit
requirement. SKU and Barcode identify the Product under the separately
approved coding rules.

### MD-OD-003 — SKU and Barcode

Product uses a hybrid SKU model. Every Product has a Tenant-unique SKU; manual
or imported values are allowed, and Tenant-configured server-side generation
may be supported. SKU does not require embedded business semantics and no
Wafra-specific coding rule is introduced. Barcode is an optional alternate
Product identifier: a Product may have zero or multiple barcodes, and barcode
values are unique inside the owning Tenant. Core SaaS does not require EAN,
GS1, or another specific barcode format; validation is configuration- or
integration-led.

### MD-OD-010 — Product tracking configuration

Tracking is configurable and disabled by default. Category may provide a
default and Product may explicitly override that default. Product Identity
owns only the Product-side configuration contract. Inventory owns stock
structures, transactions, operational enforcement, and batch/lot/serial/expiry
traceability. This overlay does not implement or decide Inventory tracking
behavior.

### MD-OD-001 — Product business availability

Product master data is Tenant-wide inside the owning Tenant and reusable by
all Companies and Branches in that Tenant. No cross-Tenant sharing exists.
Client-supplied Tenant or scope hints never replace trusted server-derived
Tenant authority. Warehouse/location stock availability is a later Inventory
concern and does not change Product master-data ownership.

### MD-OD-005 — Product approval policy

Routine Product Create, Edit, Activate, Deactivate, and Reactivate do not
require a separate approver in Release 1. Permission, exact server-derived
Tenant/scope authorization, optimistic concurrency, audit evidence, and
fail-closed authorization remain mandatory. The generic approval architecture
remains available for a future configured policy. This Product disposition
does not resolve approval catalogue behavior for other Master Data domains.

### MD-OD-008 — Product lifecycle

Product has no Draft state. A valid authorized Product is created Active and
supports Deactivate and Reactivate. Deactivation prevents new business use
where applicable while historical references remain valid and auditable.
Reactivation remains subject to permission, Tenant authorization, concurrency,
and applicable integrity rules.

These six dispositions are Product-only. They do not resolve later Price List,
Tax, Supplier, Business Customer, Currency, Exchange Rate, Inventory,
Procurement, Sales, Finance, legal/privacy, Saudi, MESP-48, MESP-49, or MESP-50
decisions. The Product implementation remains separately gated by MESP-101 and
must not begin in this documentation session.

## M95-SL-04 Supplier readiness overlay - 9 August 2026

This delivery overlay records the bounded Supplier readiness analysis and
Owner disposition for MESP-103. It does not amend the approved BRD v0.3,
resolve the global MD-OD-001/005/008 register, or make a Saudi/legal decision.
The disposition applies only to Supplier and is recorded in Jira comment
`10681`, with MESP-103 closure evidence in `10682`.

Supplier remains an external Business Party role: it is not a User, Tenant,
membership, credential holder, login identity, or consumer session. The future
bounded slice may cover Tenant ownership, approved business scope, localized
identity/reference fields, same-role duplicate evidence, cross-role
Supplier/Business Customer review or optional linkage, contacts, Active/Inactive
lifecycle, authorization, audit, concurrency, historical preservation, and
import traceability. It must not create a unified Party record or implement
Procurement transactions, Tax, Finance, payment/bank behavior, or downstream
workflow.

The bounded Supplier disposition is:

| Decision | Supplier-only Owner disposition |
|---|---|
| MD-OD-001 | **Approved for Supplier only:** Tenant-wide inside the owning Tenant, reusable by its Companies/Branches, with no cross-Tenant sharing. Client Company, Branch, Tenant, or scope values cannot override trusted server-derived authorization. |
| MD-OD-005 | **Approved for Supplier only:** no separate approver for routine Supplier identity/contact/reference and lifecycle maintenance; permission, exact server-derived Tenant/resource authorization, optimistic concurrency, audit, and fail-closed controls remain mandatory. Saudi statutory and future payment/banking/settlement changes stay outside this base disposition and their owning controls. |
| MD-OD-008 | **Approved for Supplier only:** no Draft; valid authorized creation is Active, with guarded Deactivate/Reactivate, prevention of new applicable use when inactive, and preserved historical references/audit history. |

MD-OD-007 remains an external Saudi statutory-validation gate. The approved
conditional VAT/registration baseline is retained, but fields beyond VAT are
not guessed or declared legally complete. MESP-49 remains open. The detailed
readiness contract, alternatives, acceptance traceability, exclusions,
non-blocking Product hardening follow-up, and exact revalidation handoff are in
[`19_Supplier_M95_SL_04_Readiness.md`](19_Supplier_M95_SL_04_Readiness.md).
