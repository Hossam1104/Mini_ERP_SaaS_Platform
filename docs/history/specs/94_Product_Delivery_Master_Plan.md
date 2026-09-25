# Mini ERP SaaS Platform — Product Delivery Master Plan

> **Historical fast-track overlay — 24 August 2026 (MESP-132
> acceptance-stage evidence).** Release 1 remains the full-feature reusable B2B ERP. The
> milestone is **31 August 2026 — Release 1 Integrated Preview**, a running
> preview of the real codebase, not an MVP, throwaway/demo UI, Wafra fork, or
> scope cut. Unfinished capability remains required after the preview.
>
> Canonical plan: `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`.
> Canonical Owner Decision Pack: `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`.
> Tax/VAT clarification: `docs/32_Release_1_Tax_VAT_Scope_Clarification.md`.
> Approved decision/dependency map: `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`.
> MESP-143 is merged (PR #67). MESP-124 (Purchase Orders and Supplier Confirmation)
> is complete, reviewed by Claude Opus 5 (APPROVE FOR MERGE), and squash-merged to `main`
> at commit `c742d9c897edb715c7e3c25df7e9ca2c4f30d1e6` (PR #68).
>
> No feature implementation is active. MESP-136 and MESP-137 are
> accepted/merged/closed and MESP-144 is the current In Progress
> repository-health checkpoint. The
> dedicated branch is `chore/project-health-reconciliation-cleanup`, starting
> from `c8c9084d2cf72550e7a51e4ab9475ef54d14e864`. Fast-track progress is
> 21/26 = 80.8%; production readiness remains approximately 47% overall and
> 41% Procurement/P2P. MESP-138 through MESP-142 remain To Do/inactive.
>
> **Authoritative current project-state overlay - 30 August 2026.** MESP-137
> is Done, independently accepted, and squash-merged through PR #84 from
> accepted feature head `9406e8c6408251323b96d4a0c25082142546b9ef` at
> `6b3aeb63da15253dee5466f7be001773b80c28ad`. PR #85 carries the post-closure
> documentation reconciliation to `main` at
> `4d6e33189a3835d5d8d2a58736055a837a3f5bc9`.
>
> MESP-144 reconciliation reached Sol content acceptance at comment `12293` on
> reviewed head `ffe5a8975611dcc85c3a7c40dce0b3737b123aeb`. HOLD 5
> merge-safety authority is comment `12296`. At the HOLD 5 executor handoff,
> Jira and PR lifecycle had not yet been finalized; Jira and GitHub remain
> authoritative for their respective final states. No feature
> implementation capability is active; MESP-138 through MESP-142 remain
> To Do/inactive, and MESP-48/MESP-50 remain open production gates. Accepted
> fast-track completion is 21/26 (80.8%); production readiness remains
> approximately 47% overall and 41% Procurement/P2P. The older overlays below
> are historical planning records and are not current-state authority.

> **Historical pre-fast-track overlay — MESP-38 completion.** The prior
> MESP-38/MESP-39 overlay immediately below remains preserved evidence only;
> the current fast-track overlay above is authoritative.

> **Historical current MESP-38 BRD completion overlay - 12 August 2026.**
> MESP-38 - Produce Security, Audit, and Data Governance BRD is **Done** at
> its approved bounded documentation-only scope. The canonical artifact is
> docs/29_Security_Audit_and_Data_Governance_BRD.md. Focused PR #57 merged to
> main at 67b7fb79475fb194489bc03ed153c999d20a6eaf from reviewed head
> 42f2a1cb7b15580a6a92c4603253b6ea5104c203. Jira evidence is activation
> 10934, validation 10935, Owner approval 10936, MESP-23 handoff 10937,
> final audit 10938, and closure 10939.
>
> MESP-27 through MESP-38 are Done at their approved bounded BRD scopes.
> MESP-23 remains In Progress. MESP-39 - Integrations and External Services
> BRD - is the next separate To Do task and is not activated automatically.
> MESP-40 and later remain unactivated. MESP-48, MESP-50, MESP-53, MESP-54,
> MESP-110, and MESP-113 remain open/unapproved at their existing gates. No
> source or production capability was added. PRD visual rendering was
> attempted but unavailable because pdf2image and LibreOffice/soffice are not
> installed; no visual claim is made.
>
> The root TASK.md now contains the exact MESP-39 documentation-only session
> prompt. No next task starts automatically.

> **Authoritative current MESP-37 BRD completion overlay - 11 August 2026.**
> MESP-37 - Produce Saudi Localization and Compliance BRD is **Done** at the
> bounded product-only documentation scope. The canonical artifact is
> `docs/28_Release_1_Saudi_Localization_BRD.md`, v0.1 Approved bounded
> product-only baseline. It defines Arabic/English, RTL/LTR, bilingual generic
> ERP artifacts, configurable Saudi-oriented locale/timezone/SAR presentation
> defaults, reusable Tenant-safe country-pack configuration, cross-module
> ownership, fallback/error behavior, audit/configuration evidence, and
> business acceptance scenarios. No source or production behavior was added.
> Focused PR #55 merged to `main` at
> `7d03fa5b19226b8c6368012ec90c8a09eefd4aaf` from reviewed head
> `ff8eb5901d68a2cc366ed61722c08a7be53f50a1`. Jira evidence is activation
> 10854, validation 10855, MESP-22 traceability 10856, Owner approval 10857,
> MESP-23 handoff 10858, and closure 10859.
>
> The approval is limited to localization/core ERP behavior. Statutory
> tax/e-invoicing, ZATCA/FATOORA, legal/privacy-regulatory automation,
> certification, external production integrations, provider/residency/
> retention/backup/DR, Currency/MESP-54, Reporting/MESP-53,
> Finance/MESP-110, supported-volume/MESP-48, MESP-50 governance, ADR-011,
> Retail POS, and Wafra-specific behavior remain open, deferred, or out of
> scope as named. MESP-23 remains In Progress; MESP-49 remains Done only for
> the Release 1 deferred disposition. The next exact separately authorized
> session is MESP-38 Security, Audit, and Data Governance BRD only, To Do and
> not activated automatically.

> **Authoritative current Release 1 Saudi scope overlay - 11 August 2026
> (MESP-112 complete).**
> MESP-112 - Rebaseline Release 1 Saudi localization and compliance scope is
> complete at its bounded documentation/Jira/Product Decision/governance scope
> under MESP-12. The canonical overlay is
> docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md; PD-023 was
> appended to the immutable MESP-22 Product Decision Register with Jira
> evidence 10849, and activation evidence is 10848.
>
> The approved Release 1 position is **Saudi-localized Core ERP Release 1**
> / **Saudi localization baseline** for reusable B2B ERP. Arabic, English,
> RTL, bilingual core-ERP presentation/document/report boundaries,
> SAR/default Saudi locale configuration, reusable country-pack architecture,
> Tenant isolation, authorization, audit, and generic ERP capability remain
> in scope. Production external integrations, Saudi statutory/tax-compliance
> functionality, ZATCA/FATOORA implementation or certification, and dedicated
> legal/regulatory/privacy-compliance automation are deferred/out of scope for
> R1. This is a product-scope decision, not a legal or taxpayer-applicability
> conclusion.
>
> MESP-112 is **Done** through focused PR #54, reviewed at
> 65dd650776b2c3abb06c36987b68152deb776958 and merged at
> 6e501d1f2a018c36b76339388ce7b7f09ed9c937; closure evidence is 10850.
> MESP-49 is **Done for R1 scope only** with explicit deferred/out-of-scope
> evidence in comment 10843. MESP-50 remains **To Do/open** for deferred
> legal/privacy product features and minimum production/platform governance
> (comment 10844). MESP-37 remains **To Do and not activated**, with its future
> localization-only BRD boundary in comment 10845. MESP-23 remains
> **In Progress** with reconciliation evidence 10846; MESP-111 remains Done
> with historical evidence preserved and addendum 10847. MESP-48, MESP-53,
> MESP-54, and MESP-110 remain open. No source or production capability was
> added and all production percentages remain unchanged.
>
> The prior MESP-111 external-validation overlay and all older completion
> overlays below remain historical evidence. Focused PR #54 was reviewed at
> 65dd650776b2c3abb06c36987b68152deb776958 and merged at
> 6e501d1f2a018c36b76339388ce7b7f09ed9c937; final tracker/state
> synchronization is complete on main.
> Root TASK.md now hands off the
> exact next session MESP-37 - Release 1 Saudi Localization BRD only; it must
> not start automatically.

> **Authoritative current Saudi regulatory-readiness overlay — 11 August 2026 (MESP-111 complete).**
> MESP-111 — Prepare Saudi regulatory evidence and external-validation
> readiness is **Done** at its bounded documentation/research/governance scope
> under parent MESP-12. The canonical artifact is
> docs/26_Saudi_Regulatory_Evidence_and_External_Validation_Readiness.md.
> Focused PR #53 merged cleanly to main at
> 1bcf1aa75292b927bc165a2a4fb1a8ca737763cf from reviewed branch head
> 51aee480319412ca43a7d97d1af295e1aab775d8. Jira activation evidence is
> comment 10809 and closure evidence is comment 10810.
>
> Its verdict is **READY FOR MESP-37 DRAFT ONLY — EXTERNAL VALIDATION
> OUTSTANDING**. The artifact records current official ZATCA and SDAIA/NDMO
> sources, retrieval dates, KSA-001–KSA-008 / BR-002 traceability, evidence
> classifications, FATOORA/VAT and PDPL/privacy/residency boundaries, and the
> MESP-49/MESP-50 gaps plus qualified-adviser/owner question packs.
>
> MESP-37 remains **To Do** and is not activated. MESP-49 and MESP-50 remain
> open; MESP-23 remains In Progress; MESP-53, MESP-54 and MESP-110 remain
> preserved. No source, database, migration, API, UI, infrastructure,
> credential, FATOORA implementation, production configuration, legal/tax
> advice, certification or production-readiness claim was added. Production
> capability percentages remain unchanged. The next exact session is
> qualified Saudi external-validation and owner-decision handoff only, not
> MESP-37 activation. Older completion overlays below are preserved as
> history.


> **Historical MESP-36 Reporting BRD completion overlay - 11 August 2026.**
> MESP-36 is **Done** as the documentation/governance-only Release 1 B2B
> Reporting and Analytics business baseline at
> `docs/25_Reporting_and_Analytics_BRD.md`, v0.1 Approved Business Baseline.
> Focused PR #52 merged cleanly to `main` at
> `cd3ad20876a0569245ccc6e1ff677315dfcc1a2a` from reviewed head
> `7022b24dc1c9ba6d02f9b77e0038b3e9b6211eeb`. Jira activation, validation,
> Owner approval, final audit, MESP-23 handoff, and closure evidence are
> comments `10769`, `10770`, `10771`, `10772`/`10773`, `10774`, and `10775`.
>
> MESP-53 remains **To Do and unapproved** and is the critical open Reporting
> dependency for the final report catalogue, KPI/figure definitions, named
> business and reconciliation ownership, and scheduled/distribution policy.
> MESP-54 remains **To Do and unapproved** for currency and exchange-rate
> policy. FIN-OD-09 / MESP-110 remains **To Do and unapproved** for
> fiscal-year/year-end, Payment Term, aging, and Finance posting-dimension
> policy. MESP-23 remains In Progress and Currency remains unexecuted. No
> source, transaction, stock, subledger, GL, or reporting mutation behavior is
> added.
>
> The next separately authorized BRD is MESP-37 Saudi Localization and
> Compliance. It remains **To Do** and is not activated automatically. No
> implementation item is active; Release 1 remains B2B ERP only. Older
> completion overlays below are preserved as history.

> **Historical MESP-35 Sales BRD completion overlay - 11 August 2026.**
> MESP-35 is **Done** as the documentation/governance-only Release 1 B2B
> Sales and Order-to-Cash baseline at
> docs/24_Sales_and_Order_to_Cash_BRD.md. Focused PR #51 merged cleanly to
> main at 1daffde06106ab2f1b93ae1773ccd317ddc52089 from reviewed head
> e5daa1048e9c54f34a23f613929a8832c6d8f8c5. Jira evidence is comments
> 10762, 10763, 10764, 10765, 10766, and 10767.
>
> MESP-109 remains **Done** with verdict PASS WITH NON-BLOCKING FINDINGS.
> FIN-OD-09 / MESP-110 remains **To Do and unapproved** for Finance
> year-end, Payment Term, and posting-dimension policy. MESP-54 remains
> separate and open; Currency, MESP-36, MESP-37, and implementation work
> remain unstarted. No source or production behavior is added.
>
> MESP-23 remains In Progress with 14 open Jira-decomposed decision rows
> plus the exact approved MESP-52 / PD-020 and MESP-56 / PD-021 closures.
> The next separately authorized BRD is MESP-36 Reporting and Analytics; it
> remains To Do and is not activated by this session. Older MESP-109,
> MESP-34, and other execution overlays below are preserved as history where
> they appear.

> **Historical MESP-34 completion overlay - 10 August 2026.**
> MESP-34 is **Done** as the approved, documentation-only Release 1 B2B
> Finance and Accounting BRD. The canonical artifact is
> docs/23_Finance_and_Accounting_BRD.md, v0.1 Approved Business Baseline.
> Focused PR #47 merged cleanly to main at
> a6f1960e9ae748c9809b6addbfd7e8d7ea510a1b from final branch head
> 72aa210d462f783671f1b3b33fcdea4955567b9c; Jira activation, validation,
> Owner approval, final-validation, and MESP-23 handoff evidence are comments
> 10746, 10747, 10748, 10749, and 10750; final MESP-34 closure evidence is
> Jira comment 10751.
>
> The BRD covers GL, journals, AP, AR, cash/bank, tax, periods,
> reconciliation, multi-currency, source-to-GL lineage, immutable posted
> history, reversal/correction, permissions/SoD, failure/unknown outcomes,
> reporting, migration, Saudi/localization, and production gates. It adds no
> application source, API, database/schema, migration, UI, provider, or
> production behavior. FIN-OD-01 through FIN-OD-08 remain recommendations
> only. MESP-41 through MESP-55 remain open except the exact approved
> MESP-52 / PD-020 and MESP-56 / PD-021 scopes. MESP-48, MESP-49, and MESP-50
> remain open gates.
>
> MESP-23 remains the bounded In Progress governance register. MESP-35 B2B
> Sales and Order-to-Cash is the next separately authorized To Do BRD and is
> not activated by this session. The root TASK.md contains only the exact
> MESP-35 handoff. Do not execute it automatically.

> **Historical MESP-32 completion overlay - 10 August 2026.**
> MESP-32 is **Done** as the approved, documentation-only Release 1 B2B
> Procurement and Purchase-to-Pay BRD. The canonical artifact is
> `docs/21_Procurement_and_Purchase_to_Pay_BRD.md`, v0.1 Approved Business
> Baseline. Focused PR #45 merged cleanly to `main` at
> `6dec81f3520decdf7d50ef40a44186988ba516d5` from reviewed head
> `9df9ac3df3383d6c7cdecc80a2889dc61997deaf`; Jira activation, validation,
> Owner approval, and closure evidence are comments `10736`, `10738`,
> `10739`, and `10740`. The MESP-23 living-register handoff is comment
> `10737`.
>
> The BRD is business-requirements and governance content only. It adds no
> application source, API, database/schema, migration, UI, provider, or
> production behavior. It preserves MESP-41 through MESP-55 as open except
> the separately approved MESP-52 / PD-020 and MESP-56 / PD-021. Suppliers
> remain external business parties; Retail POS and Wafra-specific core
> behavior remain excluded. MESP-48, MESP-49, and MESP-50 remain open gates.
> MESP-33 Inventory and Warehouse Management is the next separately
> authorized **To Do** BRD and is not activated by this session. The root
> `TASK.md` contains only the exact MESP-33 handoff.

> **Historical Independent Opus 5 reconciliation overlay - 10 August
> 2026.** MESP-108 is Done through PR #44, merged at
> `1f2db0a0b5ca0f39be8db06cc4c442c67b70e786`, and records **PASS - SAFE TO
> PROCEED TO NEXT DOMAIN** against
> reviewed `main` baseline `4c25330055b7c5b64a2f351b22d143b91a2646be`, with
> 0 Critical, 0 High, 3 Medium, and 4 Low findings. The detailed acceptance is
> `docs/98_Independent_Opus_5_Checkpoint_Reconciliation.md`. Current validation
> is 670 passing non-SQL tests plus a separately gated 21-case Foundation-only
> SQL harness (691 total); that SQL harness does not validate Master Data or
> Business Parties contexts. ADR-011/localized linguistic behavior and the
> open provider/production gates remain unresolved. MESP-25/MESP-26 are Done,
> MESP-32 remains To Do, and `TASK.md` hands off only the Procurement BRD to a
> fresh session. No application source or test behavior changed.

> **Historical current execution overlay — 9 August 2026.** MESP-99 /
> M95-SL-02 Category and UOM is Done through PR #33, correction PR #34, and
> final audit-semantics correction PR #35. MESP-101 is **Done** for the
> bounded M95-SL-03 Product identity readiness gate after PR #36 merged at
> `c7392a55e0b60fd83e48447e3f9218f82cfaccea`; Jira closure evidence is comment
> `10672`. MESP-102 delivered the bounded Product identity implementation
> through PR #37, merged to `main` at
> `202d59068caac5d1fac402794627e41d7f452456`; Jira activation, validation,
> and closure evidence are comments `10675`, `10676`, and `10677`.
> Hossam's Product-only bounds for MD-OD-001, MD-OD-003, MD-OD-005,
> MD-OD-008, MD-OD-010, and MD-OD-011 remain the only implemented Product
> decision scope. Release build and non-SQL validation are green; 21 SQL Server
> safety tests remain gated by the missing `MESP_SQLSERVER_CONNECTION_STRING`.
> No migration was executed, no production readiness claim is made, and no
> later Master Data or downstream decision is silently resolved. MESP-48,
> MESP-49, and MESP-50 remain open.

> **Authoritative current Supplier implementation overlay - 10 August 2026.**
> MESP-104 / M95-SL-04 Supplier implementation is **Done** through PR #39.
> The implementation head `9bf9afcd8a9ea427ed32b63ad9b655081e9592d3` merged
> to `main` at `721adeb27c366d2b8aedde66d006ac6a49956f99`; Jira activation,
> validation, and closure evidence are comments `10685`, `10686`, and `10687`.
> The bounded source slice provides module-owned Business Parties Supplier
> persistence/API, server-derived Tenant authorization, identity/reference and
> contact controls, lifecycle/concurrency, and append-before-effect audit.
> Release build 0/0, Supplier-focused tests 7/7, and non-SQL tests 609/609
> passed. The 21 SQL Server safety tests remain gated by the missing
> `MESP_SQLSERVER_CONNECTION_STRING`; no migration, provider, or production
> readiness claim is made. MESP-105 is now Done for the M95-SL-05 Business
> Customer readiness and decision gate with Owner disposition evidence `10691`;
> MESP-107 is Done through PR #41 at
> `fb632982d06fd4f6bf965fb15dff7701a0bddcec`, with activation/validation/
> closure evidence `10692`/`10726`/`10727`. MESP-48, MESP-49, and MESP-50
> remain open.

> **Authoritative current Business Customer implementation overlay - 10 August 2026.**
> MESP-105 is **Done** for the dedicated M95-SL-05 Business Customer
> readiness and decision-gate item under MESP-6; the Customer-only Owner
> disposition is Jira comment `10691`. MESP-107 is the separate
> implementation item with activation evidence `10692`; its bounded source
> implementation is complete on branch `agent/mesp-107-business-customer` at
> commit `8d8d8fddfa79a8e08f2566fcdd2499dfd594277d`; [PR #41](https://github.com/Hossam1104/mini-erp-saas-platform/pull/41) merged to `main` at
> `fb632982d06fd4f6bf965fb15dff7701a0bddcec`.
> The bounded analysis is recorded in
> `docs/20_Business_Customer_M95_SL_05_Readiness.md`. The Customer is an
> external B2B role with no login, credential, membership, consumer identity,
> or unified Party. The implementation provides Tenant-wide identity inside
> the owning Tenant with no cross-Tenant sharing, server-derived
> Tenant/resource authority, no separate approver for routine maintenance,
> no Draft, Active-on-authorized-create, guarded Deactivate/Reactivate,
> same-role integrity, contacts, concurrency, audit, contracts/routes, and
> module-owned persistence. No statutory registration, downstream commercial
> behavior, migration, provider, or production-readiness claim is made.
> MESP-106 is now Done through PR #42; MD-OD-007 and MESP-48/MESP-49/MESP-50
> remain open gates.

> **Authoritative current MESP-106 closure overlay - 10 August 2026.**
> MESP-106 is **Done** through [PR #42](https://github.com/Hossam1104/mini-erp-saas-platform/pull/42),
> merged to `main` at `0f712edcf58119057d614000721fe41227383bc1` from reviewed
> head `678a5598877f55f1b32b012de692ebdf28408acd`. The bounded correction
> hardens Product/Supplier authorization outage versus genuine-denial
> classification, Supplier duplicate validation/conflict classification, and
> failure audit-evidence preservation. Focused tests are 82/82, the full
> non-SQL suite is 670/670, and the Release build is 0/0. Customer behavior is
> unchanged; no new fields, tables, migrations, routes, UI, provider,
> production, downstream, or cross-Tenant scope behavior was added. MESP-23 is
> the existing In Progress governance/open-questions register handoff; no new
> Master Data implementation is activated. MESP-48/MESP-49/MESP-50 remain open.
> Jira activation, validation, and closure evidence are comments
> `10728`/`10729`/`10730`; final tracked handoff metadata is synchronized at
> `09d4471ffc2df1a54adf7fe74f74929b90f3ecb8`.

> **Historical Supplier-readiness overlay - 9 August 2026.** MESP-103 is
> **Done** as the bounded M95-SL-04 Supplier readiness and decision-gate item
> under MESP-6. Jira activation/analysis evidence is in comments `10679` and
> `10680`; the Supplier-only Owner disposition is comment `10681`; closure is
> comment `10682`; the non-activated MESP-104 handoff is comment `10683`.
> MD-OD-001, MD-OD-005, and MD-OD-008 are resolved only for
> Supplier: Tenant-wide availability inside the owning Tenant with trusted
> server-derived authorization, no separate approver for routine maintenance
> with mandatory permission/Tenant authorization/concurrency/audit/fail-closed
> controls, and no Draft with Active-on-authorized-create plus guarded
> Deactivate/Reactivate and preserved history. Supplier remains an external
> Business Party role with no login, credential, membership, or consumer
> session. MD-OD-007 remains an external Saudi statutory-validation/production
> gate under MESP-49. No Supplier source implementation, persistence, API, UI,
> migration, or Procurement transaction behavior has started. MESP-104 is the
> next separately prepared To Do implementation item. The detailed handoff is
> `docs/19_Supplier_M95_SL_04_Readiness.md`; MESP-48, MESP-49, and MESP-50
> remain open.

> **Historical MESP-99 completion overlay - 9 August 2026 (PR #33 and post-merge correction PR #34 merged).**
> MESP-99 / M95-SL-02 Category and UOM is implementation-complete on branch
> `agent/mesp-99-category-uom` at its approved bounded scope: Tenant-wide
> Category/UOM policy, server-derived authorization, lifecycle/concurrency,
> hierarchy, precision/rounding, module-owned persistence, isolation, and
> persistent audit fidelity. Release build, focused tests, and the non-SQL
> suite are green; the 21-test SQL Server safety gate remains an existing
> credential/provider gate because `MESP_SQLSERVER_CONNECTION_STRING` is not
> configured. No migration, production database, Product/Item behavior, or
> later domain slice was introduced. Focused PR #33 merged to `main` at
> `8364a67bce4d7d782115b7347e4e6607f02f9be4` from implementation commits
> `430996c`, `0cf6906`, and `964766b`. The verified post-merge correction commit
> is `e527f8a0cc32a72cef554e2bd93ab6322e9b1064`; focused PR #34 merged to
> `main` at `35417d35c076d1318474a7e4b31144cc9d94279b`. Jira validation evidence
> is comment `10665`, final closure evidence is comment `10666`, and correction
> evidence is comment `10667`; MESP-99 is Done. Stale MESP-97/MESP-98
> administrative duplicates are terminally reconciled with comments
> `10669`/`10668`. MESP-48, MESP-49, and MESP-50 remain open.

> MESP-101 completed the M95-SL-03 Product identity readiness gate through PR
> #36. MESP-102 then completed the bounded Product implementation through PR
> #37. MESP-103 has now closed the M95-SL-04 Supplier readiness gate after the
> Supplier-only Owner disposition. The next exact session is MESP-104 Supplier
> master-data implementation only, and must not start automatically.

> **Historical current overlay at MESP-99 activation - MESP-100 closed; MESP-99 active, 9 August 2026.** MESP-100 is Done with Jira closure evidence `10663`; PR #32 merged at `511f6be9f005e54930f993aead9758d7a66b75a8`. MESP-99 was In Progress with activation evidence `10664` and was the single active implementation item. The five Category/UOM-only bounds are MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006. No Category/UOM persistence or MESP-99 behavior was implemented by MESP-100; the root TASK.md then contained the exact MESP-99 session.

> **Historical MESP-100 readiness overlay — 9 August 2026.** MESP-100 was the
> single active bounded readiness-correction item between completed MESP-96 /
> M95-SL-01 and MESP-99 / M95-SL-02. MESP-99 remains To Do until this session
> is fully validated, merged, and activated. The actual production topology is
> four projects (`MiniErp.Api`, `MiniErp.App`, `MiniErp.Contracts`, and the
> existing `MiniErp.Infrastructure`); ADR-002 now defines the direct
> Api-to-Infrastructure composition route and the Infrastructure-to-App-to-
> Contracts provider direction. The five Category/UOM-only Owner bounds are
> MD-OD-001, MD-OD-005, MD-OD-008, MD-OD-002, and MD-OD-006. No Category/UOM
> persistence or M95-SL-02 business implementation is part of MESP-100.

> **Historical execution overlay - 8 August 2026.** MESP-31, MESP-95, and MESP-96 are **Done** at their bounded scope. MESP-96/M95-SL-01 is contract-only/non-persistent and PR #30 merged at actual commit `87f150d95f583168a86aa56200916343c6404f7f`; Jira completion evidence is comment `10655`. M95-SL-02 Category and UOM was then the next exact session and was not started. MD-OD-001 through MD-OD-011 remained unresolved, MESP-48/MESP-49/MESP-50 remained open gates, and no Master Data persistence existed.
>
> Hossam standing Owner approval covers normal BRD/specification/readiness, merge/closure, and next-session activation within approved scope and architecture. A fresh Codex/Luna chat executes exactly one root `TASK.md` session, validates, updates affected state/Jira, commits/pushes, merges only when clean, then stops for ChatGPT review. Stop only for a real security, Tenant-isolation, accounting/data-integrity, destructive migration/data-loss, legal/external-validation, credential/infrastructure, unresolved-decision, or material scope/architecture blocker. Independent Opus review is due after every five sessions or earlier at a critical checkpoint.

| Field | Value |
|---|---|
| Document | Product Delivery Master Plan |
| Status | Active living plan |
| Owner | Hossam |
| Repository | `Hossam1104/mini-erp-saas-platform` |
| Suggested repository path | `docs/94_Product_Delivery_Master_Plan.md` |
| Last updated | 30 August 2026 |
| Product boundary | Release 1 B2B ERP only |
| Approved PRD | `docs/MESP_PRD_v1.2.docx` (PRD v1.2 Final Approved Baseline, approved 31 July 2026; formerly `MiniERPSaaSPlatform_PRD_v1.2.docx` at the repository root and, before that, `docs/MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx`) |
| Current activity | `MESP-144 repository-health reconciliation reached Sol content acceptance at comment 12293; HOLD 5 merge-safety authority is comment 12296; MESP-137 is accepted/merged/closed; MESP-23 and named production/legal/external gates remain preserved.` |
| Current implementation item | `No feature implementation is active. MESP-138 through MESP-142 remain To Do/inactive. The dedicated health branch is chore/project-health-reconciliation-cleanup.` |
| Merged-main baseline | `The pre-reconciliation main baseline integrated into the MESP-144 branch was 4d6e33189a3835d5d8d2a58736055a837a3f5bc9; MESP-137 accepted feature head 9406e8c6408251323b96d4a0c25082142546b9ef and PR #84 squash SHA 6b3aeb63da15253dee5466f7be001773b80c28ad are recorded.` |
| Current Sprint | `No active Sprint — MESP-63 was delivered outside a Sprint` |
| Open Pull Request | `At the HOLD 5 executor handoff, PR #82 was verified Open/Draft/Unmerged; GitHub is authoritative for its final lifecycle. No merge or Ready transition is authorized by this checkpoint.` |
| Current review checkpoint | `MESP-144 content acceptance is recorded at comment 12293; HOLD 5 merge-safety cleanup is recorded at comment 12296; final PR/Jira lifecycle is checked live.` |

---

## 1. Purpose

This document is the master execution plan for taking the Mini ERP SaaS Platform from approved product requirements through business analysis, domain design, functional specification, technical design, Jira delivery, implementation, testing, and release.

It is a living progress tracker. Completed work is marked as **Done**, the current active item is marked as **In Progress**, and remaining work stays unchecked until it is completed and verified.

---

## 2. Status legend

- [x] **Done** — completed, reviewed, and accepted.
- [ ] **Not Started** — planned but not started.
- [ ] **In Progress** — add `🔄` beside the item while actively executing it.
- [ ] **Blocked** — add `⛔` and the blocking dependency.
- [ ] **Deferred** — add `⏸` and the owning future decision or phase.

---

## 3. Current position

### Product-wide position

The latest bounded BRD position is MESP-31 **Done**, MESP-32 **Done**,
MESP-33 **Done**, MESP-34 **Done**, MESP-35 **Done**, MESP-36 **Done**,
MESP-37 **Done**, and MESP-38 **Done** as approved documentation-only
business baselines. MESP-39 Integrations and External Services is the next
separately authorized BRD and remains To Do; it is not activated by MESP-38
completion.

The project is currently in **Phase 2 — Business Requirements Documents**, because the full set of business-domain BRDs has not yet been completed.

### Foundation backend slice

The SaaS Platform Administration domain and the approved Foundation backend slice
have progressed further than the rest of the product. MESP-57, MESP-58,
MESP-87, MESP-59, MESP-88, MESP-60, MESP-62 and MESP-89 are complete and merged.
MESP-63, MESP-90, MESP-61 and MESP-64 are complete. Product-wide Phase 2
remains in progress because core ERP BRDs are not complete, and Foundation
backend work is not complete ERP backend implementation.

PR #18 ([link](https://github.com/Hossam1104/mini-erp-saas-platform/pull/18))
merged MESP-64 to `main` at
`2002d1c25d39022b227e89b3d70f41a53de0408c`, which remains the historical
Foundation application baseline. MESP-91 Correction Package 1
([PR #20](https://github.com/Hossam1104/mini-erp-saas-platform/pull/20)) was
approved by focused ChatGPT security review and merged to `main` at
`f2cde57400fed470ab048776e05b56f353b36890`; its documentation reconciliation
([PR #21](https://github.com/Hossam1104/mini-erp-saas-platform/pull/21)) merged
at `32a91f27bc162685fc0db0f38b031d02ffbc99d2`, which is the **current
merged-main baseline**.

MESP-92 Correction Package 2 is **Done**. Its work was published as
[PR #22](https://github.com/Hossam1104/mini-erp-saas-platform/pull/22), which
received a focused ChatGPT security review verdict of APPROVED FOR MERGE at
reviewed head `3ec6b45` and was merged to `main` at
`322341e70e56270797d5770b4b90342c20b7833e` on 7 August 2026. MESP-93
(Correction Package 3 — private-file/notification hardening) is **Done**
through PR #24 merged at `005c796629341ab9becfbc6d1abe2ae34b6a7332`, and
MESP-94 safety-catalogue/validation-evidence correction is **Done** through
PR #26 merged at `06d837c958c1cb7977dc121e3aa4e7278944fd`. No Sprint is
active and no parallel implementation item exists. MESP-109 Finance
reconciliation is **Done**; MESP-23 remains the active governance register and
MESP-39 is the next separate To Do BRD after the completed MESP-38 Security,
Audit, and Data Governance baseline.

| Area | Current status |
|---|---|
| Product PRD | Done |
| Product architecture baseline | Done |
| MESP-27 Platform Administration BRD | Done |
| MESP-27 implementation backlog | Done |
| MESP-27 Jira synchronization | Done |
| MESP-57 Sprint activation | Done |
| MESP-57 development branch | Done |
| MESP-57 implementation | Done — implemented, reviewed, merged through PR #1, validated on `main`, and closed in Jira |
| Sprint 1 | Done — `S1-Solution Foundation` completed |
| MESP-58 trusted TenantContext and persistence isolation | Done — PR #6 merged; corrective hardening included in final main |
| MESP-87 Tenant persistence guardrail hardening | Done — merged with MESP-58 correction sequence |
| MESP-59 authentication and authorization seam | Done — PR #8 merged and corrected through MESP-88 |
| MESP-88 MESP-59 security correction | Done — PR #9 merged; 161 tests passed |
| MESP-60 REST/OpenAPI foundation | Done — PR #10 merged to `main` |
| MESP-62 immutable audit and OpenTelemetry evidence | Done — merged with the Foundation Backend Review Checkpoint package |
| MESP-89 Foundation host authentication, antiforgery and evidence integration | Done — PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8`; focused ChatGPT review approved; merged-main validation passed with 247 tests |
| MESP-63 Angular Foundation shell | Done — PR #14 merged at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`; Wave 1 shell, session/context integration, bilingual RTL and safe states validated |
| MESP-90 MESP-63 false-logout security correction | Done — PR #16 merged at `469ab863a5fc20f02d3ba674a97dceb969bbec75`; preserves authenticated state until server-confirmed revocation |
| MESP-61 background processing foundation | Done — PR #17 merged at `7db49a88e11232f055c2016b8bb033a61de629ec`; typed Tenant-bound durable work/outbox/inbox, bounded worker, notification contracts and private-file adapter |
| MESP-64 provider/schema/index validation | Done — PR #18 merged at `2002d1c25d39022b227e89b3d70f41a53de0408c`; ADR-018, disposable LocalDB SQL Server harness, exact 75-assertion evidence and merged-main validation complete; no production provider or migration |
| MESP-91 verified work scope and worker authority correction | Done — Correction Package 1; PR #20 merged at `f2cde57400fed470ab048776e05b56f353b36890` after focused ChatGPT approval; Identity-owned organization resolver, authorization-context-bound scopes, live worker/outbox revalidation and safe authority dead-letter are merged to `main` |
| MESP-92 single-effect durable work and immutable typed payloads | **Done** — Correction Package 2; PR #22 merged to `main` at `322341e70e56270797d5770b4b90342c20b7833e` after focused ChatGPT approval |
| MESP-93 private-file/notification hardening | **Done** — PR #24 merged at `005c796629341ab9becfbc6d1abe2ae34b6a7332` |
| MESP-94 safety-catalogue and validation-evidence correction | **Done** — PR #26 merged at `06d837c958c1cb7977dc121e3aaea4e7278944fd` |
| Final Foundation correction checkpoint | **Done** — closure recorded; MESP-48/MESP-50 remain intentional production gates |
| MESP-3 Identity and Access Epic | In Progress |
| MESP-28 Identity and Access BRD | Done — v0.3 Approved Release 1 Baseline (founder change-control approval 3 August 2026) |
| MESP-4 Multi-Tenancy and Tenant Lifecycle Epic | In Progress |
| MESP-29 Multi-Tenancy BRD | Done — v0.2 Approved Release 1 Baseline (2 August 2026) |
| MESP-5 Organization and Company Structure Epic | In Progress |
| MESP-30 Organization BRD | Done — v0.2 Approved Release 1 Baseline (2 August 2026); outside all Sprints |
| Remaining product BRDs | Not Started / To Do |

### Current Jira and Git state

- [x] `MESP-27` — SaaS Platform Administration BRD is **Done**.
- [x] `MESP-2` — Platform Administration Epic remains **In Progress**.
- [x] `MESP-57` — Modular Monolith solution and module seam is **Done**.
- [x] `MESP-3` — Identity and Access Epic remains **In Progress** for the sequential foundation/domain documentation stream.
- [x] `MESP-28` — Identity and Access BRD approved as the v0.2 Release 1 baseline and moved to **Done**.
- [x] `MESP-4` — Multi-Tenancy and Tenant Lifecycle Epic moved to **In Progress**; the Epic continues beyond this BRD.
- [x] `MESP-29` — Multi-Tenancy BRD approved as the v0.2 Release 1 baseline and moved to **Done**; it remains outside all Sprints.
- [x] `MESP-5` — Organization and Company Structure Epic moved to **In Progress**; the Epic continues beyond this BRD.
- [x] `MESP-30` — Organization BRD approved as the v0.2 Release 1 baseline, moved to **Done**, and kept outside all Sprints; approval commit was prepared on `docs/mesp-30-organization-brd` and is verified in the documentation merge to `main`.
- [x] `MESP-86` — Foundation Release 1 Lean Implementation Specification v0.4 approved and moved to **Done** after review/merge evidence; it remains outside implementation Sprints.
- [x] `MESP-1` — Product Governance and BRD Management Epic moved to **In Progress** for the controlled specification activity.
- [x] `MESP-23` — Open Questions Register remains **In Progress** as a living governance register; its current working deliverable is maintained in Jira comments, not as a second BRD/LIS delivery artifact.
- [x] Sprint `S1-Solution Foundation` was created, started, and completed.
- [x] Sprint contained only `MESP-57`.
- [x] `MESP-58`, `MESP-87`, `MESP-59` and `MESP-88` are **Done** with merged implementation/security evidence.
- [x] `MESP-60` is **Done**; PR #10 is merged and merged-main validation passed.
- [x] `MESP-62` is **Done**; the immutable audit/observability seam and checkpoint package are merged.
- [x] `MESP-89` is **Done**; PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` after focused ChatGPT approval; merged-main validation passed with 247 tests.
- [x] `MESP-63` moved to **In Progress**, implemented and merged through PR #14; it is now **Done** and the sequence advanced to MESP-61, then MESP-64.
- [x] `MESP-90` false-logout correction is **Done**; approved PR #16 merged to `main` at `469ab863a5fc20f02d3ba674a97dceb969bbec75`; MESP-63 remains **Done**.
- [x] `MESP-61` is **Done**; PR #17 merged to `main` at `7db49a88e11232f055c2016b8bb033a61de629ec` and merged-main validation passed.
- [x] `MESP-64` is **Done**; PR #18 merged to `main` at `2002d1c25d39022b227e89b3d70f41a53de0408c`, merged-main validation passed, and its branch was deleted.
- [x] `MESP-91` is **Done**; PR #20 merged at `f2cde57400fed470ab048776e05b56f353b36890` after focused ChatGPT security review approval; branch `fix/MESP-91-verified-work-scope-authority` deleted after merge.
- [x] `MESP-31`, `MESP-32`, `MESP-33`, `MESP-34`, `MESP-35`, `MESP-36`, `MESP-37`, and `MESP-38` are **Done** as approved documentation-only BRD baselines; `MESP-39` and `MESP-40` remain **To Do** and no implementation slice is active.
- [x] `MESP-92` is **Done** (7 August 2026). PR #22 merged to `main` at `322341e70e56270797d5770b4b90342c20b7833e` after focused ChatGPT security review approval.
- [x] `MESP-93` is **Done** through PR #24; `MESP-94` is **Done** through PR #26. No implementation item is active; MESP-109 Finance reconciliation is **Done**, MESP-23 remains the governance register, and MESP-39 is the next separate To Do BRD after the completed MESP-38 Security, Audit, and Data Governance baseline.
- [x] No Sprint is active; MESP-89 and MESP-63 were delivered outside a Sprint.
- [x] MESP-63, MESP-90, MESP-61, MESP-64, MESP-91, MESP-92, MESP-93 and MESP-94 completed sequentially; no implementation item is active and no parallel implementation is authorized.
- [x] MESP-86 v0.4 Approved Release 1 Baseline is merged to `main`; implementation refinement is controlled and is not production readiness.
- [x] Product-wide Phase 2 remains **In Progress** because core ERP BRDs remain incomplete; Foundation backend work does not imply complete ERP backend implementation.
- [x] Branch `feat/mesp-57-modular-monolith-seam` was created from `main` and pushed.
- [x] Implement `MESP-57 / TE-01`.
- [x] Run the Release build.
- [x] Run targeted architecture validation.
- [x] Validate API startup and required endpoints.
- [x] Review the MESP-57 implementation.
- [x] Create and review Pull Request #1.
- [x] Merge Pull Request #1 into `main`.
- [x] Run post-merge validation on `main`.
- [x] Move MESP-57 to **Done**.
- [x] Complete `S1-Solution Foundation`.
- [x] Synchronize local `main` with `origin/main`.

---

# 4. Delivery lifecycle and fast-track documentation policy

The product will follow this controlled lifecycle. The former separate DDD, FRS, Data Design, and TDS documents are not mandatory standing deliverables.

1. Product Requirements Document
2. Business Requirements and BPMN
3. One Lean Implementation Specification per approved foundation/domain slice
4. Implementation-ready Jira Backlog
5. Implementation and Automated Testing
6. Integration, UAT, Release, and Operations

A domain must not move into implementation until its BRD/BPMN and applicable Lean Implementation Specification are approved, except for explicitly approved architecture-foundation Enablers such as MESP-57.

## Fast-track documentation policy

- An approved BRD is the business baseline for its domain; do not keep it in a draft state after founder approval.
- For each approved foundation/domain slice, create one Lean Implementation Specification that carries the required domain model/invariants, functional journeys, logical data decisions, authorization/API behavior, targeted tests, and acceptance traceability in one controlled document.
- Do not create separate mandatory DDD, FRS, Data Design, or TDS documents for the same slice. Those concerns are content sections of the Lean Implementation Specification.
- Security, tenant-isolation, MESP-48 supported-volume, and MESP-50 retention/privacy/legal-hold/purge gates remain mandatory and are not bypassed by the fast-track policy.
- Keep documentation activities sequential: one current BRD or Lean Implementation Specification activity at a time; do not start implementation or a later BRD before the current evidence is committed and reviewed.
- A living governance register such as `MESP-23` is maintenance work, not a second active BRD/LIS delivery artifact. It is exempt from the one-active-delivery-artifact rule while no separate drafting task is actively being executed.
- For the founder-authorized fast-track implementation batch, Luna performs bounded implementation, self-review and validation; eligible Pull Requests may merge automatically after all safety gates pass.
- ChatGPT reviews each execution report using the actual Jira, GitHub, diff, build and test evidence. Independent Opus review is reserved for major checkpoints rather than every Jira item. MESP-89 was an explicit security exception: focused ChatGPT review was required before merge, approved the corrected PR, and no additional full Opus review is required for this completed item.

---

# Phase 1 — Product Requirements Document

## Objective

Freeze the product vision, business scope, target market, product boundaries, architecture direction, major non-functional expectations, and Release 1 decisions.

## Required outputs

- Product vision and goals.
- Target market and primary users.
- Release 1 scope.
- Explicit exclusions.
- Product modules and capability map.
- Approved organizational hierarchy.
- SaaS and multi-tenancy principles.
- Localization and country-launch direction.
- High-level architecture baseline.
- Major product decisions and open decision register.
- Approval record.

## Progress

- [x] Product vision defined.
- [x] Release 1 B2B ERP scope defined.
- [x] Retail POS explicitly excluded.
- [x] Target market and Saudi launch direction defined.
- [x] Platform hierarchy approved:
  - Platform
  - Tenant
  - Company / Legal Entity
  - Branch
  - Warehouse
- [x] Wafra defined as Tenant #1 and validation customer only.
- [x] Tenant-specific core code prohibited.
- [x] Product architecture direction approved.
- [x] PRD v1.2 approved and frozen as the current product baseline.
- [x] Initial product decisions recorded.
- [ ] Close remaining production-gate decisions when their owning phases require them:
  - `MESP-48` — reference volumes and supported-volume evidence.
  - `MESP-50` — hosting, residency, retention, legal hold, backup, purge, and restoration policy.

## Exit criteria

- [x] Approved PRD baseline exists.
- [x] Scope and exclusions are unambiguous.
- [x] Product hierarchy and architecture direction are approved.
- [x] Remaining open decisions have explicit owners and gates.

**Phase 1 status: DONE**

---

# Phase 2 — Business Requirements Documents and BPMN

## Objective

Define the business meaning of each domain before technical implementation. Each BRD must capture workflows, actors, rules, exceptions, states, data requirements, reports, approvals, audit evidence, and business acceptance scenarios. A BRD is approved once its founder/business-owner decision record is complete; it is not held open for a second documentation package.

## Required outputs per domain

- Business purpose.
- Actors and responsibilities.
- Preconditions and triggers.
- Main workflow.
- Alternative workflows.
- Exception scenarios.
- Business rules.
- Document lifecycle.
- State transitions.
- Validation rules.
- Approval controls.
- Separation of duties.
- Permissions.
- Data requirements.
- Reports and KPIs.
- Audit evidence.
- Integration requirements.
- Migration requirements.
- Accounting impact.
- Inventory impact.
- Multi-currency impact.
- Saudi localization impact.
- Given/When/Then business acceptance scenarios.
- BPMN process diagrams where the workflow needs them.
- Founder-approved decision record and explicit deferred gates.
- Founder/business-owner approval.

## Domain sequence and progress

### Foundation and governance

- [x] Product foundation and governance preparation.
- [x] Jira simplification and delivery-governance setup.
- [x] `MESP-27` — SaaS Platform Administration BRD.
- [x] MESP-27 founder approval.
- [x] MESP-27 Wave 1 wireframes and Layout B baseline.
- [ ] BPMN diagrams for all MESP-27 workflows.
- [x] `MESP-28` — Identity and Access BRD — **Done: v0.3 Approved Release 1 Baseline** (founder change-control approval 3 August 2026).
- [x] `MESP-29` — Multi-Tenancy BRD — **Done: v0.2 Approved Release 1 Baseline** (2 August 2026).
- [x] `MESP-30` — Organization BRD — **Done: v0.2 Approved Release 1 Baseline** (2 August 2026).

### Core ERP domains

- [x] Master Data and Catalog BRD — MESP-31 Done; v0.3 at `docs/16_Master_Data_and_Product_Catalog_BRD.md` is an Owner Approved Business Baseline. MD-OD-001 through MD-OD-011 remain open and governed.
- [ ] Business Parties BRD.
- [x] Procurement BRD — MESP-32 Done; v0.1 Approved Business Baseline at `docs/21_Procurement_and_Purchase_to_Pay_BRD.md`; PR #45 merged to `main` at `6dec81f3520decdf7d50ef40a44186988ba516d5`; MESP-41 through MESP-55 remain open except approved MESP-52/MESP-56.
- [x] Inventory BRD — MESP-33 Done; v0.1 Approved Business Baseline at docs/22_Inventory_and_Warehouse_Management_BRD.md; PR #46 merged to main at cd6f57de329b7d193c5d75e2e4268ae87c8aac67; MESP-41 through MESP-55 remain open except approved MESP-52/MESP-56.
- [x] B2B Sales BRD - MESP-35 Done; v0.1 Approved Business Baseline at docs/24_Sales_and_Order_to_Cash_BRD.md; PR #51 merged to main at 1daffde06106ab2f1b93ae1773ccd317ddc52089; FIN-OD-09 / MESP-110 and MESP-54 remain open; no source implementation.
- [x] Finance BRD — MESP-34 historically Done; v0.1 Approved Business Baseline at `docs/23_Finance_and_Accounting_BRD.md`; the bounded independent-review correction is MESP-109 with FIN-OD-09 / MESP-110 still open; no source implementation.
- [x] Reporting and Analytics BRD - MESP-36 Done; v0.1 Approved Business Baseline at `docs/25_Reporting_and_Analytics_BRD.md`; PR #52 merged to `main` at `cd3ad20876a0569245ccc6e1ff677315dfcc1a2a` from reviewed head `7022b24dc1c9ba6d02f9b77e0038b3e9b6211eeb`; MESP-53 remains critical and open; no source implementation.
- [x] Saudi Localization and Core ERP BRD - MESP-37 Done; v0.1 Approved bounded product-only baseline at `docs/28_Release_1_Saudi_Localization_BRD.md`; PR #55 merged to `main` at `7d03fa5b19226b8c6368012ec90c8a09eefd4aaf`; no source implementation and no statutory/compliance claim.
- [x] Security, Audit, and Data Governance BRD - MESP-38 Done; approved bounded documentation-only baseline at docs/29_Security_Audit_and_Data_Governance_BRD.md; PR #57 merged at 67b7fb79475fb194489bc03ed153c999d20a6eaf; no source implementation.
- [ ] Integrations and External Services BRD - MESP-39 remains To Do and is not activated.
- [ ] Files and Integrations BRD.
- [ ] Migration and cutover requirements.

## MESP-27 completed work

- [x] Business scope defined.
- [x] Actors and responsibilities defined.
- [x] Plan and Subscription model approved.
- [x] Entitlement override prohibition approved.
- [x] Tenant lifecycle defined.
- [x] Support-access controls defined.
- [x] Export and offboarding controls defined.
- [x] Purge review and certificate requirements defined.
- [x] Multiple Legal Entities per Tenant approved.
- [x] No consolidation or intercompany automation in Release 1.
- [x] MESP-48 and MESP-50 production gates preserved.
- [x] MESP-27 marked Done in Jira.

## Phase 2 exit criteria per domain

- [ ] Applicable BRD approved with a recorded founder/business-owner decision record.
- [ ] BPMN diagrams approved where they are required to remove material workflow ambiguity.
- [ ] All critical business rules are explicit.
- [ ] State transitions are unambiguous.
- [ ] Exceptions and negative paths are covered.
- [ ] Permissions and separation-of-duties rules are approved.
- [x] Deferred decisions have owners and explicit implementation or production gates.
- [ ] No technical behavior is invented to fill a business gap.

MESP-28, MESP-29, MESP-30, MESP-31, MESP-32, MESP-33, MESP-34, MESP-35,
MESP-36, MESP-37, and MESP-38 are approved business baselines at their bounded
documentation scopes. MESP-39 and MESP-40 remain To Do and are not implied
to be approved by any prior approval; each requires its own controlled BRD
decision.
MESP-41 through MESP-55 remain open except the exact approved MESP-52/MESP-56
scopes.

**Phase 2 status: IN PROGRESS — the approved BRD sequence is complete through
MESP-38 Security, Audit, and Data Governance; MESP-39 Integrations and
External Services is the next separate To Do BRD. MESP-28 through MESP-38 are not
implementation authorization, and no later BRD is implicitly approved by this
status.**

---

# Phase 3 — Lean Implementation Specification: domain and behavior

## Objective

Capture the domain-model and behavior sections of one Lean Implementation Specification for an approved foundation/domain slice. This is not a separate DDD deliverable.

## Required outputs per domain

- Bounded Context.
- Context map and relationships.
- Ubiquitous Language updates.
- Aggregate Roots.
- Aggregates.
- Entities.
- Value Objects.
- Domain Services.
- Domain Events.
- Commands.
- Business invariants.
- Ownership boundaries.
- Cross-context contracts.
- Consistency boundaries.
- Transaction boundaries.
- Domain error taxonomy.
- Deferred design decisions and explicit production gates.

## Planned execution

For each approved BRD, create or update one Lean Implementation Specification:

1. Extract nouns, roles, documents, states, and business events.
2. Confirm the owning Bounded Context.
3. Define Aggregate Roots and consistency boundaries.
4. Define entities and immutable Value Objects.
5. Define domain invariants.
6. Define domain events for completed business facts.
7. Define allowed dependencies on other contexts.
8. Validate the model against every BRD workflow and exception.
9. Update the Business Glossary.
10. Obtain approval for the complete Lean Implementation Specification before implementation refinement.

## Progress

- [x] High-level module boundaries defined in the architecture baseline.
- [x] Platform Administration identified as a distinct module boundary.
- [ ] Platform Administration Lean Implementation Specification.
- [ ] Identity and Access Lean Implementation Specification.
- [ ] Multi-Tenancy Lean Implementation Specification.
- [ ] Organization Lean Implementation Specification.
- [ ] Remaining domain Lean Implementation Specifications.

## Exit criteria per domain

- [ ] Every business concept has one owning context.
- [ ] Aggregate boundaries are explicit.
- [ ] Business invariants are enforceable.
- [ ] Cross-context dependencies are approved.
- [ ] No shared business-model dumping ground exists.
- [ ] The model covers all BRD workflows and exceptions.

**Phase 3 status: IN PROGRESS — MESP-86 v0.4 is the approved combined lean domain/behavior baseline for MESP-28/29/30; implementation refinement is controlled and begins only with MESP-58.**

---

# Phase 4 — Lean Implementation Specification: user journeys and acceptance

## Objective

Capture user journeys, screens, actions, inputs, outputs, states, validations, permissions, errors, reports, localization, and functional acceptance criteria as sections of the same Lean Implementation Specification. No standalone FRS document is required.

## Required outputs per domain

- Feature catalogue.
- Actor-to-feature matrix.
- User journeys.
- Navigation flows.
- Screen and page inventory.
- Fields and controls.
- Input validation.
- Default values.
- Read-only and editable states.
- Empty, loading, error, restricted, and no-result states.
- Search, filter, sort, and pagination behavior.
- Notifications and confirmations.
- Reports and exports.
- Localization and RTL behavior.
- Accessibility expectations.
- Functional acceptance criteria.
- Traceability to BRD requirements and BPMN steps.

## Progress

- [x] MESP-27 Wave 1 low-fidelity wireframes exist.
- [x] Layout B selected as the Tenant Workspace baseline.
- [x] MESP-27 implementation Stories contain user-facing outcomes and acceptance criteria.
- [ ] MESP-27 user-journey and acceptance sections in its Lean Implementation Specification.
- [ ] Identity and Access user-journey and acceptance sections.
- [ ] Multi-Tenancy user-journey and acceptance sections.
- [ ] Organization user-journey and acceptance sections.
- [ ] Remaining domain user-journey and acceptance sections.

## Exit criteria per domain

- [ ] Every BRD capability maps to one or more functions.
- [ ] User-visible behavior is unambiguous.
- [ ] Validation and state behavior are defined.
- [ ] Error and restricted states are covered.
- [ ] Localization and accessibility expectations are included.
- [ ] Functional acceptance criteria are testable.

**Phase 4 status: IN PROGRESS — MESP-86 v0.4 contains the approved combined functional journeys, route/state inventory and acceptance coverage for MESP-28/29/30; no standalone FRS documents are planned.**

---

# Phase 5 — Lean Implementation Specification: logical data and integrity

## Objective

Capture the logical data and integrity decisions required to support approved domain behavior, tenant isolation, business integrity, performance, auditability, retention, and reporting in the same Lean Implementation Specification. Physical implementation detail is added only when the slice is ready; no standalone Data Design document is required.

## Required outputs per domain

### Logical design

- Logical ERD.
- Business entities.
- Relationships and cardinality.
- Ownership boundaries.
- Tenant and organizational scope.
- Lifecycle and state history.
- Audit and evidence requirements.
- Retention classification.

### Physical design

- Module-owned database schema.
- Tables.
- Columns and data types.
- Primary keys.
- Foreign keys.
- Unique constraints.
- Check constraints.
- Tenant keys.
- Organizational keys.
- Concurrency fields.
- Audit columns.
- Index strategy.
- Partitioning decision where justified.
- Temporal/history strategy where justified.
- Migration strategy.
- Seed/reference-data strategy.
- Backup and purge impact.
- Reporting/read-model requirements.

## Approved baseline constraints

- [x] Shared SQL Server database approved for Release 1.
- [x] Module-owned schemas approved.
- [x] Database-per-Tenant excluded.
- [x] Direct cross-module table mutation prohibited.
- [x] Tenant isolation required across persistence, jobs, files, exports, and audit.
- [ ] Detailed tenant-isolation data model.
- [ ] Logical ERDs.
- [ ] Physical ERDs.
- [ ] Domain indexing strategy.
- [ ] Domain migration plans.

## Exit criteria per domain

- [ ] Logical ERD approved.
- [ ] Physical ERD approved.
- [ ] Tenant and organizational scoping is explicit.
- [ ] Constraints enforce critical invariants.
- [ ] Indexes support expected queries and supported volumes.
- [ ] Audit, retention, and purge effects are covered.
- [ ] No table ownership conflict exists between modules.

**Phase 5 status: IN PROGRESS — MESP-86 v0.4 contains the approved logical Foundation data model, ERD and tenant-aware integrity design; the implemented persistence, immutable audit seam and MESP-64 SQL Server provider/schema/index/collation/rowversion validation remain bounded foundation seams. Physical migrations remain excluded, and detailed physical ERDs are required before implementing each future ERP domain. MESP-50 remains a production gate.**

---

# Phase 6 — Lean Implementation Specification: implementation readiness

## Objective

Capture the implementation-readiness decisions for the approved slice across solution boundaries, APIs, database, authorization, integrations, background processing, observability, security, and deployment in the same Lean Implementation Specification. No standalone TDS document is required.

## Required outputs per domain

- Component architecture.
- Module boundaries.
- Application-service design.
- Domain-service design.
- Public contracts.
- API contracts.
- OpenAPI definitions.
- Request and response models.
- Error contracts.
- Authentication requirements.
- Authorization policies.
- Resource and scope checks.
- Transaction boundaries.
- Idempotency approach.
- Concurrency behavior.
- Database mapping.
- Outbox and background-job design.
- Integration contracts.
- File and object-storage behavior.
- Logging.
- Metrics.
- Tracing.
- Health checks.
- Rate limiting.
- Threat model.
- Security controls.
- Configuration.
- Deployment requirements.
- Technical test strategy.
- Failure and recovery behavior.
- Traceability to the approved BRD/BPMN and the relevant Lean Implementation Specification sections.

## Progress

### Product-level technical baseline

- [x] Modular Monolith approved.
- [x] Angular 22 approved.
- [x] TypeScript approved.
- [x] ASP.NET Core Web API on .NET 10 LTS approved.
- [x] EF Core 10 approved.
- [x] SQL Server 2025 approved.
- [x] REST and OpenAPI approved.
- [x] ASP.NET Core Identity direction approved.
- [x] Secure HTTP-only cookie direction approved.
- [x] Policy/resource authorization direction approved.
- [x] Docker Compose local-dependency direction approved.
- [x] OpenTelemetry direction approved.
- [x] xUnit and Playwright TypeScript direction approved.
- [x] Transactional outbox/inbox direction approved.
- [ ] Identity and Access Lean Implementation Specification implementation-readiness sections.
- [ ] Multi-Tenancy Lean Implementation Specification implementation-readiness sections.
- [ ] Organization Lean Implementation Specification implementation-readiness sections.
- [ ] Detailed domain API and database sections inside each Lean Implementation Specification.
- [ ] Detailed production deployment design.

### Current MESP-57 technical foundation

- [x] MESP-57 implementation scope approved.
- [x] MESP-57 Demonstration Outcome defined.
- [x] MESP-57 Definition of Done defined.
- [x] MESP-57 branch created.
- [x] MESP-57 Sprint activated.
- [x] Implement the four-project Modular Monolith seam.
- [x] Add targeted architecture validation.
- [x] Build and demonstrate the API host/module-registration seam.
- [x] Review the MESP-57 implementation.
- [x] Merge MESP-57 through Pull Request #1.
- [x] Validate the merged result on `main`.
- [x] Move MESP-57 to **Done**.
- [x] Complete Sprint 1.

## Exit criteria per domain

- [ ] Detailed design covers all approved functions.
- [ ] API, data, authorization, and transaction behavior are explicit.
- [ ] Security and tenant-isolation controls are reviewable.
- [ ] Failure and recovery paths are defined.
- [ ] Observability and operational requirements are included.
- [ ] Technical risks and deferred decisions are recorded.
- [ ] No implementation depends on an unresolved critical decision.

**Phase 6 status: IN PROGRESS — MESP-86 v0.4 contains the approved authorization, API, persistence, security, observability and slicing design. MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62, MESP-89, MESP-63, MESP-90, MESP-61 and MESP-64 are implemented as bounded Foundation seams; MESP-89 merged the catalog-backed exact operation authorization, mandatory protected-write evidence, composite idempotency replay and separate eligibility/selection versions. Production provider validation remains separately gated.**

---

# Phase 7 — Implementation-Ready Jira Backlog

## Objective

Convert approved specifications into a controlled, traceable, sequenced backlog that can be implemented without inventing business or technical behavior.

## Required Jira structure

- Epics.
- Features where the Jira configuration supports them.
- Technical Enablers.
- User Stories.
- Tasks only when independently deliverable.
- Bugs.
- Releases and Fix Versions.
- Acceptance criteria.
- Dependencies.
- Estimates.
- Design references.
- Traceability.
- Definition of Ready.
- Definition of Done.
- Demonstration outcome.
- Sprint proposal.

## MESP-27 backlog progress

- [x] Existing Epic `MESP-2` reused.
- [x] Duplicate implementation Epic avoided.
- [x] 8 Technical Enablers created:
  - `MESP-57` through `MESP-64`.
- [x] 21 User Stories created:
  - `MESP-65` through `MESP-85`.
- [x] All issues mapped to MESP-2.
- [x] Component assigned.
- [x] Release assigned.
- [x] Sequence labels added.
- [x] Dependency pairs created.
- [x] Dependency graph confirmed acyclic.
- [x] Jira descriptions repaired for encoding issues.
- [x] MESP-57 refined for implementation.
- [x] MESP-58 through MESP-64 descriptions refined against MESP-86 v0.4; the founder-authorized fast-track sequence is MESP-60 followed by MESP-62, with the later items gated by the checkpoint.
- [x] Sprint `S1-Solution Foundation` created with MESP-57 only.
- [ ] Refine each future Enabler or Story immediately before implementation.
- [ ] Populate native estimation fields when Jira configuration supports them.
- [ ] Create Bugs only from verified defects.
- [ ] Keep future items out of active Sprints until Definition of Ready is met.

## Definition of Ready

A future Story or Enabler may enter a Sprint only when:

- [ ] Applicable BRD is approved.
- [ ] BPMN workflow is approved where required.
- [ ] Lean Implementation Specification for the slice is approved, including domain ownership, journeys, data decisions, authorization, and technical readiness.
- [ ] Dependencies are resolved or explicitly sequenced.
- [ ] Acceptance criteria are testable.
- [ ] Security impact is reviewed.
- [ ] Tenant-isolation impact is reviewed.
- [ ] MESP-48 or MESP-50 gates are resolved where applicable.
- [ ] Traceability is complete.
- [ ] No unresolved critical product decision remains.

## Definition of Done for Jira preparation

- [ ] Correct parent Epic.
- [ ] Correct issue type.
- [ ] Clear single outcome.
- [ ] Scope and exclusions recorded.
- [ ] Acceptance criteria complete.
- [ ] Dependencies correct.
- [ ] Estimate recorded.
- [ ] Demonstration outcome recorded.
- [ ] Technical and design references recorded.
- [ ] No duplicate issue exists.
- [ ] No parallel-work recommendation exists.

**Phase 7 status: DONE FOR MESP-27 WAVE 1; PENDING FOR OTHER DOMAINS**

---

# Phase 8 — Implementation and Automated Testing

## Objective

Implement approved backlog items sequentially, validate them with focused automated tests, review the code, merge safely, and preserve traceability from requirements to release evidence.

## Standard execution flow per item

1. Confirm the item is implementation-ready.
2. Add only the approved item to the active Sprint.
3. Move the item to In Progress.
4. Create one feature branch.
5. Read only the required approved sources.
6. Implement the smallest complete scope.
7. Add targeted automated tests.
8. Run targeted validation.
9. Review the complete task-related diff.
10. Commit with the Jira key.
11. Push the branch.
    12. Review the complete task-related diff, run `git diff --check`, and correct every finding.
    13. Add implementation evidence to Jira.
    14. Create a non-draft Pull Request.
    15. Automatically merge only after base/head, build, tests, security, scope and repository gates pass; use a normal merge commit. MESP-89 was the completed security exception and required focused ChatGPT approval before its merge.
    16. Run post-merge validation on synchronized `main`.
    17. Demonstrate the outcome and update the delivery documentation.
    18. Move the Jira item to Done.
    19. Delete the completed local and remote branch.
    20. Select the next explicitly authorized item only.

## Current implementation sequence

### Sprint 1 — Solution Foundation

- [x] Sprint created.
- [x] Sprint started.
- [x] MESP-57 added as the only Sprint item.
- [x] MESP-2 moved to In Progress.
- [x] MESP-57 moved to In Progress.
- [x] Development branch created and published.
- [x] Implement MESP-57.
- [x] Run the MESP-57 Release build and architecture validation.
- [x] Review the MESP-57 code and evidence.
- [x] Create MESP-57 Pull Request #1.
- [x] Complete final review of Pull Request #1.
- [x] Merge Pull Request #1.
- [x] Validate `main` after merge.
- [x] Move MESP-57 to Done.
- [x] Complete Sprint 1.

### Founder-authorized fast-track Foundation batch (no Sprint)

- [x] No Sprint is required or active for this controlled batch.
- [x] `MESP-57`, `MESP-58`, `MESP-87`, `MESP-59` and `MESP-88` are complete and merged.
- [x] `MESP-60` is Done; PR #10 merged at `2569acbe6dc26223108f7ad539ca7db2bcdf5f93` and merged-main validation passed.
- [x] `MESP-62` is Done; immutable audit/observability evidence and the checkpoint package are complete and merged.
- [x] `MESP-89` is Done; PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` after focused ChatGPT approval and 247-test merged-main validation.
- [x] `MESP-63` moved to In Progress, completed its bounded Angular Wave 1 implementation, and merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`.
- [x] `MESP-61` is **Done**; PR #17 merged to `main` at
  `7db49a88e11232f055c2016b8bb033a61de629ec` and merged-main validation passed.
- [x] `MESP-64` is **Done**; PR #18 merged to `main` at
  `2002d1c25d39022b227e89b3d70f41a53de0408c` and merged-main validation passed.

### Current sequence after the approved Foundation specification

The product-wide Phase 2 BRD stream remains in progress because core ERP BRDs
remain incomplete. The Foundation slice has completed the approved MESP-63
frontend baseline, MESP-61 durable-work foundation and the MESP-91 Correction
Package 1 authority hardening and the MESP-92 Correction Package 2 payload/
effect hardening; that does not mean complete ERP backend implementation has
started. MESP-91 and MESP-92 are Done and merged (PR #22 merged to `main` at
`322341e70e56270797d5770b4b90342c20b7833e`). MESP-93 is **In Progress** and is
the only active implementation item; no Sprint is active. Do not start
MESP-31, MESP-94 or any downstream ERP transaction work before MESP-93 closes
and its entry conditions are reconfirmed.

- [x] Approve `MESP-28` Identity and Access BRD v0.3 change-control baseline on `docs/foundation-release1-lean-spec`.
- [x] Begin and complete `MESP-29` Multi-Tenancy BRD as the single requirements activity; v0.2 is approved and Done.
- [x] Merge the approved MESP-29 documentation to `main`; no implementation item or Sprint was created.
- [x] Begin and complete `MESP-30` Organization BRD; approve v0.2 Release 1 Baseline on `docs/mesp-30-organization-brd`.
- [x] Resolve and record `ORG-OD-001` through `ORG-OD-007`; move `MESP-30` to Done after approval evidence.
- [x] Produce and approve v0.4 of the combined Foundation Release 1 Lean Implementation Specification under MESP-86; merge the approved documentation to `main`.
- [x] Refine MESP-58 through MESP-64 against v0.4 and complete the Definition of Ready checks for the Foundation sequence.
- [x] Complete MESP-58, MESP-87, MESP-59 and MESP-88 with merged validation evidence.
- [x] Complete MESP-60 REST/OpenAPI and safe operation contracts.
- [x] Complete MESP-62 immutable audit and OpenTelemetry evidence.
- [x] Complete `MESP-89` host authentication, antiforgery, catalog-backed exact permissions, trusted context, mandatory protected-write evidence, composite idempotency replay and separate context eligibility/selection versions; PR #12 merged after focused ChatGPT approval.
- [x] Start `MESP-63` Angular Foundation shell sequentially after the MESP-89 Jira closure and reconciliation evidence.

### MESP-63 completed delivery evidence

- [x] Jira MESP-63 moved to **In Progress**, completed, and has its implementation evidence recorded; MESP-61 and MESP-64 are **Done**.
- [x] Branch `feature/mesp-63-angular-wave-1-shell-rtl` was created from synchronized `main`; implementation commits `798d15d1aa1e53781df3a2683305e95ac3143890` and `46bf2d30f91ef00e9e450b59b8de0b3a2d34dbab` were merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`.
- [x] Angular 22/TypeScript standalone workspace and modular `core`, `features`, and `shared` structure created.
- [x] EN/AR translations, runtime direction switching, responsive shell, navigation, header, context rail and accessible focus baseline implemented.
- [x] Session bootstrap, secure-cookie `withCredentials`, in-memory antiforgery bootstrap, safe expiry handling and server-confirmed context switching implemented against the merged `/api/v1/auth/*` contracts.
- [x] Loading, empty, restricted, denied, expired-session and safe-error presentation states included; no token or Tenant authority is stored in browser storage.
- [x] Focused Angular tests pass (8/8); mocked Playwright TypeScript Wave 1 smoke journey passes (1/1); production/shared provider and database work is excluded.
- [x] Review the complete diff, publish the non-draft Pull Request #14, merge after all gates passed, validate merged `main`, record Jira closure evidence and delete the completed branch.

### MESP-90 — MESP-63 false-logout security correction (completed)

- [x] Jira correction task `MESP-90` is **Done**, relates to MESP-63 and unblocked MESP-61; MESP-63 remains **Done**.
- [x] Branch `fix/mesp-63-signout-fail-closed` was created from the verified `main` baseline `7efb9e76e3bd12d8c97a48cb882efd238ea93373`.
- [x] AuthService now distinguishes confirmed sign-out, server-confirmed already-invalid sessions and unconfirmed outcomes; it does not clear local state or navigate to login after antiforgery, audit, server, malformed-response or network failure.
- [x] Cached antiforgery material is cleared after a 403, no sign-out POST is sent without a non-empty in-memory token, concurrent sign-outs coalesce, and stale responses cannot overwrite newer authentication state.
- [x] The shell keeps the selected context visible, exposes an accessible EN/AR retry message and disables the action only while the request is active; no token or cookie authority is stored in browser storage.
- [x] Correction validation currently passes 27 Angular unit/component tests and 4 Playwright journeys; backend source and contract remain unchanged and the 247-test/0-warning/0-error backend baseline remains required.
- [x] Publish non-draft correction PR #16 ([link](https://github.com/Hossam1104/mini-erp-saas-platform/pull/16)); focused ChatGPT review approved the exact head and the PR merged by normal merge at `469ab863a5fc20f02d3ba674a97dceb969bbec75`.
- [x] Validate merged `main`, post Jira closure evidence, move `MESP-90` to Done and delete the completed implementation branch; MESP-61 was started only after this closure.

### MESP-61 — Durable work, notification, and private-file adapters (completed)

- [x] Confirm MESP-90 is Done on merged `main` at
  `469ab863a5fc20f02d3ba674a97dceb969bbec75`; keep MESP-63 Done and advance
  to MESP-64 only after MESP-61 closure.
- [x] Move MESP-61 to **In Progress** only after the MESP-90 closure comment;
  no Sprint is active.
- [x] Review ADR timing and author ADR-006, ADR-007, ADR-008 and ADR-009.
- [x] Add typed Tenant-bound durable-work identity, scope, initiator,
  lifecycle, lease, retry, dead-letter and optimistic-concurrency contracts.
- [x] Add the bounded local relational outbox/inbox adapter, typed dispatcher
  and one-item worker seam; no global Tenant business query path exists.
- [x] Add provider-neutral notification intent/delivery contracts with a
  deterministic local adapter that stores no contact data.
- [x] Add private-file metadata/access contracts and a deterministic local
  adapter with exact Tenant ownership and checksum/concurrency checks; expiry
  remains metadata only and no physical purge exists.
- [x] Add focused tests for Tenant isolation, single-effect dispatch, retry,
  lease ownership, safe audit, notifications and private files; merged-main
  validation remains a closure gate.
- [x] Complete the full diff/self-review, publish and merge PR #17 after all
  required tests and security gates passed, validate merged `main`, post Jira
  closure evidence, move MESP-61 to Done and delete the branch.

MESP-48 supported-volume/performance evidence and MESP-50 retention, privacy,
legal-hold, purge, residency, backup and restoration decisions remain gates;
MESP-61 does not select production providers or execute purge.

### MESP-64 — Foundation safety harness and SQL Server validation (completed)

- [x] Confirm MESP-61 is Done on merged `main` at
  `7db49a88e11232f055c2016b8bb033a61de629ec`; no Sprint is active.
- [x] Author ADR-018 before environment setup. The current machine uses the
  installed SQL Server LocalDB `MSSQLLocalDB`, a unique disposable
  `MiniErpFoundation_*` database, Windows integrated authentication and fixture
  cleanup. Docker/Testcontainers CI compatibility remains deferred.
- [x] Add provider-specific SQL Server tests for the Tenant filter, stored-owner
  update/delete guard, Tenant-aware unique index, rowversion concurrency,
  schema/index metadata, collation/Arabic Unicode, transaction atomicity,
  Tenant-scoped idempotency and single-owner lease claims.
- [x] Add the exact 75-assertion report at
  `docs/96_Foundation_Release1_Safety_Validation.md`; record 53 PASS, 21 NOT
  APPLICABLE scope-boundary rows and one MESP-48/MESP-50 DEFERRED production gate.
- [x] Run the complete diff/self-review, publish and merge PR #18 after
  targeted SQL, backend/frontend regression, security and scope gates passed;
  validate merged `main`, post Jira evidence, move MESP-64 to Done and delete
  the branch.

MESP-48 and MESP-50 remain production gates; MESP-64 did not select a
production provider, create a migration, execute purge or authorize later ERP
work.

### MESP-91 — Verified work scope and worker authority correction (Done, Package 1)

- [x] Confirm MESP-91 is the sole active implementation item; move it to
  **In Progress** and keep MESP-31, MESP-92, MESP-93 and MESP-94 **To Do**.
- [x] Create `fix/MESP-91-verified-work-scope-authority` from the verified
  baseline `4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`; keep no Sprint active.
- [x] Add the narrow Identity-owned organization resolver for an untrusted
  scope request, exact Tenant -> Company -> Branch -> Warehouse ownership,
  downward containment and authorization-context-bound verified scopes.
- [x] Add live worker and outbox authority revalidation immediately before
  handler/effect dispatch, covering current User/session, authorization path,
  Membership or SupportGrant/SupportCase, exact Permission, scope and
  ownership.
- [x] Terminally dead-letter authority failures with safe
  `AuthorizationDenied` evidence and never call the handler or protected
  outbox effect after a failed check.
- [x] Close H91-03: require canonical explicit ordinary `Kind:GUID` scope,
  reject missing/malformed/marker/broader/sibling scope, and use the current
  case-bound stored SupportGrant scope as the only SupportGrant authority.
- [x] Close H91-04: use one exact binding for WorkItemId, Tenant, operation,
  correlation, Company/Branch/Warehouse boundary, execution TenantContext,
  path, Membership/SupportGrant, actor and session; defensively recheck it
  before handler/outbox execution.
- [x] Add the Identity-only structural issuer allow-list and make mandatory
  security evidence an operation-descriptor requirement enforced at creation,
  handler registration, dispatch and live revalidation.
- [x] Add focused organization-boundary, lifecycle, permission, support-path,
  dead-letter and no-effect regression tests; reconcile the safety catalogue
  and ADR/checkpoint/current-state documentation without changing MESP-48 or
  MESP-50 gates.
- [x] Complete full validation and review the complete task-related diff:
  focused durable-work 102/102, backend 360/360 including SQL 11/11, Angular
  27/27, Playwright 4/4, Release build 0 warnings/0 errors and production
  audit 0 vulnerabilities.
- [x] Commit and push the correction branch and update the existing non-draft
  PR #20 for focused ChatGPT security review.
- [x] Focused ChatGPT security review returned APPROVED TO MERGE (0 Critical,
  0 High, 0 Medium blockers); verify the approved head, rerun final
  validation, and merge PR #20 by normal merge commit at
  `f2cde57400fed470ab048776e05b56f353b36890`.
- [x] Validate merged `main`, reconcile documentation to the merged state,
  post Jira closure evidence, move MESP-91 to Done and delete the completed
  branch. Do not start MESP-31, MESP-92, MESP-93, MESP-94 or another
  implementation item in this context.

### Foundation Completion Opus 5 checkpoint (documentation-only)

- [x] Confirm MESP-90, MESP-61 and MESP-64 are Done on merged `main` and no
  implementation item or Sprint is active.
- [x] Create `docs/97_Foundation_Completion_Review_Checkpoint.md` with the
  complete Foundation sequence, traceability, capabilities, maturity
  boundaries, remaining gates and Opus 5 questions.
- [ ] Submit the documentation-only checkpoint for Opus 5 review; do not start
  MESP-31, Master Data/Catalog work, MESP-48/MESP-50 implementation or any
  other Jira item before review disposition.

## Testing strategy

- xUnit for:
  - Domain rules.
  - Application behavior.
  - Persistence.
  - Authorization.
  - Concurrency.
  - Architecture boundaries.
  - Integration behavior.

- Playwright TypeScript for:
  - Critical browser journeys.
  - API journeys.
  - Cookie and antiforgery behavior.
  - RTL behavior.
  - Tenant-isolation failures.
  - End-to-end business flows.

- Additional validation where needed:
  - SQL integrity checks.
  - Contract validation.
  - Security checks.
  - Performance checks against MESP-48 evidence.
  - Recovery and retry behavior.
  - Deployment smoke tests.

## Exit criteria per implementation item

- [ ] Approved scope implemented.
- [ ] No later-phase scope introduced.
- [ ] Targeted automated tests pass.
- [ ] Build passes.
- [ ] Security and tenant-isolation checks pass where applicable.
- [ ] No known P0 or P1 defect remains.
- [ ] Code review completed.
- [ ] Pull Request merged.
- [ ] Jira evidence added.
- [ ] Demonstration completed.
- [ ] Documentation updated only where necessary.

**Phase 8 status: MESP-57, MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62,
MESP-89, MESP-63, MESP-90, MESP-61, MESP-64 and MESP-91 are Done on the
merged-main Foundation baseline `32a91f27bc162685fc0db0f38b031d02ffbc99d2`.
MESP-91 Correction Package 1 merged through PR #20 at
`f2cde57400fed470ab048776e05b56f353b36890` after focused ChatGPT security
review approval. MESP-92 is In Progress on PR #22, which is open, non-draft and
unmerged at head `e991641`; no Sprint is
active; MESP-93, MESP-94 and MESP-31 remain To Do; and the final Foundation
correction checkpoint has not started.**

---

# Phase 9 — Integration, UAT, Release, and Operations

## Objective

Validate the complete product across modules, prepare production operations, complete business acceptance, release safely, and monitor the platform after deployment.

## Required outputs

- Integrated test environment.
- Integration test plan.
- Cross-module process validation.
- Data migration rehearsal.
- Security assessment.
- Performance and supported-volume validation.
- Backup and restoration validation.
- Monitoring and alerting.
- Operational runbooks.
- Deployment checklist.
- Rollback plan.
- UAT plan.
- UAT evidence.
- Release notes.
- Training material.
- Support handover.
- Production readiness review.
- Go-live approval.
- Post-release monitoring.
- Incident and defect process.
- Release retrospective.

## Progress

- [ ] Integration environment.
- [ ] Full cross-domain integration validation.
- [ ] MESP-48 supported-volume validation.
- [ ] MESP-50 production-policy decisions.
- [ ] Security testing.
- [ ] Performance testing.
- [ ] Migration rehearsal.
- [ ] Backup and restore test.
- [ ] UAT.
- [ ] Production readiness review.
- [ ] Release 1 deployment.
- [ ] Post-release monitoring.

## Exit criteria

- [ ] UAT approved.
- [ ] Critical defects resolved.
- [ ] Security risks accepted or resolved.
- [ ] Performance targets validated.
- [ ] Operational ownership established.
- [ ] Backup and restoration proven.
- [ ] Deployment and rollback tested.
- [ ] Production gates approved.
- [ ] Founder release approval recorded.

**Phase 9 status: NOT STARTED**

---

# 5. Domain delivery order

The expected execution order is:

1. Platform Administration foundation.
2. Identity and Access.
3. Multi-Tenancy.
4. Organization.
5. Master Data and Catalog.
6. Business Parties.
7. Procurement.
8. Inventory.
9. B2B Sales.
10. Finance.
11. Reporting and Analytics.
12. Saudi Country Pack.
13. Files and Integrations.
14. Migration, UAT, and Release.

This order may be adjusted only when approved dependencies require a different sequence.

---

# 6. Requirements traceability

Every implemented capability must preserve this chain:

```text
PRD requirement
→ BRD requirement and business rule
→ BPMN activity
→ Lean Implementation Specification (domain model, journey, data, API, security, and test sections)
→ Jira Enabler, Story, or Task
→ Automated and manual test evidence
→ Commit and Pull Request
→ Release evidence
```

## Traceability rules

- [ ] No Jira implementation item without an upstream approved source.
- [ ] No approved requirement omitted from downstream artifacts.
- [ ] No API or table created without an owning domain.
- [ ] No code behavior invented to resolve an unapproved business decision.
- [ ] Every acceptance criterion maps to validation evidence.
- [ ] Every release item maps to a Jira key and Pull Request.
- [ ] Every production gate maps to an approval or evidence record.

---

# 7. Cross-cutting controls

These controls must be reviewed in every applicable phase and domain.

## Multi-tenancy

- Tenant isolation.
- Organizational scope.
- Cross-Tenant denial.
- Background jobs.
- Files and exports.
- Audit evidence.
- Database constraints.
- Support access.

## Security

- Authentication.
- Authorization.
- Least privilege.
- Separation of duties.
- Session handling.
- Privileged access.
- Export authorization.
- Auditability.
- Secrets management.
- Threat modeling.

## Data integrity

- Transaction boundaries.
- Idempotency.
- Concurrency.
- Referential integrity.
- Decimal and currency precision.
- State transition integrity.
- Immutable evidence.
- Migration safety.

## Localization

- English and Arabic.
- RTL behavior.
- LTR handling for identifiers.
- Saudi defaults.
- Country-pack extensibility.
- Accessible status presentation.

## Operations

- Logging.
- Metrics.
- Tracing.
- Correlation identifiers.
- Health checks.
- Retry behavior.
- Background-job visibility.
- Backup and restoration.
- Purge and legal hold.
- Incident evidence.

---

# 8. Model usage policy

## Luna Max — default model

Use Luna Max for:

- BRDs.
- BPMN preparation.
- Lean Implementation Specification drafting.
- Jira backlog work.
- Jira synchronization.
- Routine implementation.
- Documentation updates.
- Normal self-review.

## Codex / project direction

Use Codex for:

- Directing the next step.
- Reviewing Luna deliverables.
- Checking Jira and GitHub state.
- Detecting scope drift.
- Issuing bounded execution prompts.
- Verifying implementation reports.
- Deciding whether a critical specialist review is required.

## SOL High — critical use only

Use SOL High only for:

- Tenant-isolation architecture.
- Authentication and authorization security.
- Accounting and posting integrity.
- Irreversible physical database design.
- Data-loss, purge, retention, and restoration decisions.
- Severe contradictions between approved sources.
- High-impact production architecture changes.

## Sonnet

Sonnet is not part of the normal workflow. Use it only when explicitly approved and quota is available for a narrowly scoped independent review.

---

# 9. Immediate action plan

## Current action

- [x] Approve `MESP-28 — Identity and Access BRD` v0.3 as the Release 1 baseline change-control update.
- [x] Approve `MESP-29 — Multi-Tenancy BRD` v0.2 as the Release 1 baseline, including the four Tenant-isolation clarifications; move MESP-29 to Done.
- [x] Merge the two approved MESP-29 documentation files to `main`; keep MESP-4 In Progress.
- [x] Complete `MESP-30 — Organization BRD` as the sequential requirements activity; v0.2 is Approved Release 1 Baseline and MESP-30 is Done outside all Sprints.
- [x] Approve and merge the v0.4 combined Foundation Release 1 Lean Implementation Specification; no application implementation started in this correction cycle.
- [x] Refine MESP-58 through MESP-64 against the approved Foundation sequence; no Sprint is required for the founder-authorized fast-track batch.
- [x] Reconcile MESP-59 and close its completed implementation/security correction sequence before starting the next implementation item.
- [x] Complete MESP-60 REST/OpenAPI foundation implementation and validation; PR #10 merged at `2569acbe6dc26223108f7ad539ca7db2bcdf5f93`.
- [x] Complete MESP-62 immutable audit and OpenTelemetry evidence; its checkpoint package is included in the merged delivery.
- [x] Complete MESP-89 host authentication, antiforgery, trusted context and evidence integration; PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` and MESP-89 is Done after focused ChatGPT approval.
- [x] Close the MESP-89 Jira/reconciliation record, complete the authorized MESP-63 Angular Foundation shell, and keep MESP-61 and MESP-64 outside parallel execution until the MESP-90 sequential gate was met.
- [x] Implement, merge and close MESP-63 through PR #14 on `feature/mesp-63-angular-wave-1-shell-rtl`; no Sprint is active.
- [x] Merge and close MESP-90 through PR #16 at `469ab863a5fc20f02d3ba674a97dceb969bbec75` after focused ChatGPT approval.
- [x] Implement and close MESP-61 on `feature/mesp-61-durable-work-private-files`; PR #17 merged at `7db49a88e11232f055c2016b8bb033a61de629ec` and the ADR index/Foundation evidence were updated for the bounded scope.
- [x] Implement and close MESP-64 on `feature/mesp-64-foundation-safety-harness`; PR #18 merged at `2002d1c25d39022b227e89b3d70f41a53de0408c`, ADR-018 and the exact 75-assertion safety evidence were updated without production/provider or later ERP scope.
- [x] Prepare the documentation-only Foundation Completion Opus 5 checkpoint at `docs/97_Foundation_Completion_Review_Checkpoint.md`; stop before MESP-31 or any later implementation.

### MESP-91 current correction activity (Done)

- [x] Move Jira MESP-91 to **In Progress** after confirming no other
  implementation item or Sprint is active.
- [x] Implement the verified organization-scope resolver, context binding,
  authorized-scope containment and current worker/outbox authority
  revalidation in Correction Package 1.
- [x] Add focused regression tests and reconcile `docs/96`, `docs/97`,
  `docs/ADR-008_SQL_Background_Workers_and_Ownership.md` and `.ai/CURRENT_STATE.md`.
- [x] Complete final validation: focused durable-work 102/102, backend
  360/360 including SQL 11/11, Angular 27/27, Playwright 4/4, Release build 0
  warnings/0 errors and production audit 0 vulnerabilities.
- [x] Commit/push, publish the non-draft PR #20 for focused ChatGPT security
  review.
- [x] Focused ChatGPT security review returned APPROVED TO MERGE; merge PR #20
  by normal merge commit at `f2cde57400fed470ab048776e05b56f353b36890`,
  validate merged `main`, reconcile documentation, post Jira closure evidence,
  move MESP-91 to Done and delete the branch.

## Completed MESP-57 outputs

- [x] `backend/MiniErp.sln`.
- [x] `backend/Directory.Build.props`.
- [x] `backend/Directory.Packages.props`.
- [x] `backend/src/MiniErp.Api`.
- [x] `backend/src/MiniErp.App`.
- [x] `backend/src/MiniErp.Contracts`.
- [x] `backend/tests/MiniErp.ArchitectureTests`.
- [x] Platform Administration module registration seam.
- [x] Public contracts separated from internal module code.
- [x] Minimal API startup evidence.
- [x] Targeted architecture validation.
- [x] Local build instructions.
- [x] Successful restore and Release build.
- [x] Six architecture tests passed.
- [x] `/health` validated successfully.
- [x] `/api/v1/module-registration` validated successfully.
- [x] Implementation commit `de6578f` created and pushed.
- [x] Pull Request #1 created, reviewed, and merged.
- [x] Merge commit `47be691cfbe4946139dcd55e55f5cbb1b86e257d` validated on `main`.
- [x] Jira evidence added.
- [x] MESP-57 moved to Done.
- [x] Sprint 1 completed.

## Closure confirmation

- [x] The full MESP-57 diff was reviewed.
- [x] The project dependency graph was verified.
- [x] No future-Enabler scope was implemented.
- [x] Build and architecture-validation evidence was verified.
- [x] No secret was identified in the reviewed change; historical generated IDE files under `.vs` remain tracked in the repository. Their cleanup is a separate bounded repository-hygiene task and is not performed here.
- [x] No MESP-28 behavior was implemented.
- [x] No persistence, authentication, authorization, tenant-isolation, downstream ERP transaction, or Retail POS scope was introduced.
- [x] `main` is synchronized with `origin/main`.

## Next requirements gate

- [x] Review the approved scope and source documents for `MESP-28`.
- [x] Produce and approve the Identity and Access BRD v0.2 without starting implementation.
- [x] Resolve the 22 historical IAM-OD records plus IAM-OD-023 (23 total) and four source-conflict records in the approved baseline.
- [x] Begin, approve, and close the single MESP-29 Multi-Tenancy BRD activity; v0.2 is merged to `main`.
- [x] Record the four Tenant-isolation clarifications and preserve MESP-48/MESP-50 Deferred Gates.
- [x] Keep future implementation items outside the active sequence in To Do; MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62, MESP-89, MESP-90, MESP-61, MESP-64 and MESP-91 are complete; no implementation item is currently active.
- [x] Keep `MESP-31` through `MESP-40` in To Do while the approved foundation requirements remain the current delivery boundary.
- [x] Keep `MESP-30` outside all Sprints and restrict it to business requirements only.
- [x] Complete founder approval of the MESP-30 baseline and resolve `ORG-OD-001` through `ORG-OD-007`.
- [x] Start, approve, and merge `MESP-86` v0.4 combined Foundation Release 1 Lean Implementation Specification on `docs/foundation-release1-lean-spec`; keep it outside all implementation Sprints.
- [x] Refine MESP-58 through MESP-64 and complete the MESP-58 Definition of Ready review.
- [x] No Sprint is required for the founder-authorized MESP-60/MESP-62 fast-track batch.
- [x] Start and complete MESP-62 only after MESP-60 was merged, validated and moved to Done; no Sprint was created.
- [x] Complete focused ChatGPT review of the MESP-89 PR, merge PR #12, validate merged `main`, and record the MESP-89 Done state; MESP-63 and MESP-90 subsequently completed sequentially.
- [x] Complete focused ChatGPT review of MESP-90 PR #16, merge the exact approved head, validate merged `main`, post Jira closure evidence and move MESP-90 to Done.
- [x] Complete the MESP-61 durable-work/private-file implementation and merged-main validation before starting MESP-64.
- [x] Complete the MESP-64 foundation safety harness PR, merged-main validation and Jira closure before creating the documentation-only Foundation Completion Opus 5 checkpoint.
- [ ] Obtain Opus 5 disposition of the complete Foundation checkpoint before starting Master Data and Catalog or any other core ERP BRD/implementation item.

---

# 10. Progress log

Use this section to record major milestones.

| Date | Item | Status | Evidence / Notes |
|---|---|---|---|
| 31 July 2026 | PRD v1.2 approved | Done | Final approved product baseline |
| 1 August 2026 | Architecture baseline approved | Done | Modular Monolith, Angular 22, .NET 10, EF Core 10, SQL Server 2025 |
| 1 August 2026 | MESP-27 BRD approved | Done | Platform Administration BRD v0.10 |
| 1 August 2026 | MESP-27 Jira backlog synchronized | Done | MESP-57–64 and MESP-65–85 under MESP-2 |
| 1 August 2026 | Sprint 1 activated | Done | `S1-Solution Foundation`, MESP-57 only |
| 1 August 2026 | Development branch published | Done | `feat/mesp-57-modular-monolith-seam` |
| 1 August 2026 | MESP-57 implementation completed | Done | Commit `de6578f` created and pushed; Release build and 6 architecture tests passed |
| 1 August 2026 | Product Delivery Master Plan added | Done | Added in commit `d86e00f` as an approved project-governance addition |
| 1 August 2026 | Delivery plan aligned with review state | Done | Commit `c547f3c` |
| 1 August 2026 | Pull Request #1 merged | Done | Merge commit `47be691cfbe4946139dcd55e55f5cbb1b86e257d` |
| 2 August 2026 | Post-merge validation on `main` | Done | Restore and Release build passed; 6 architecture tests passed; required endpoints validated |
| 2 August 2026 | MESP-57 closed | Done | Jira status moved to Done |
| 2 August 2026 | Sprint 1 completed | Done | `S1-Solution Foundation` closed with MESP-57 completed |
| 2 August 2026 | MESP-28 BRD approved | Done | `docs/12_Identity_and_Access_BRD.md` v0.2 Approved Release 1 Baseline; original baseline approval, no implementation Sprint or Jira implementation work started |
| 2 August 2026 | MESP-29 BRD approved | Done | `docs/13_Multi_Tenancy_BRD.md` v0.2 Approved Release 1 Baseline; four Tenant-isolation clarifications incorporated; MESP-4 remains In Progress; no Sprint or implementation item started; approval commit merged to `main` |
| 2 August 2026 | MESP-30 BRD approved | Done | `docs/14_Organization_and_Company_Structure_BRD.md` v0.2 Approved Release 1 Baseline; ORG-OD-001 through ORG-OD-007 resolved; approval merged to `main` at `a1e5eb439bf6723efb5f0638cfc518ad044fce86`; no implementation started |
| 2 August 2026 | MESP-86 foundation specification started | In Progress | Governance Task under MESP-1; design/documentation only; no Sprint or implementation item |
| 3 August 2026 | MESP-28 IAM change-control and MESP-86 approval | Done | IAM BRD v0.3 records the founder global User/Membership decision; MESP-86 v0.4 approved and merged; no application code or implementation started |
| 3 August 2026 | MESP-58 trusted TenantContext and persistence isolation | Done | PR #6 merged; security correction and merged-main validation completed |
| 3 August 2026 | MESP-87 Tenant persistence guardrail hardening | Done | Completed in the MESP-58 correction sequence; cross-Tenant Modified/Deleted protections validated |
| 3 August 2026 | MESP-59 authentication and authorization seam | Done | PR #8 merged; status reconciled after MESP-88/PR #9 evidence; Jira reconciliation comment 10274 |
| 3 August 2026 | MESP-88 MESP-59 security correction | Done | PR #9 merged at `723dc8e28b0a927750230b51b9d05e26d039038c`; 161 tests passed |
| 3 August 2026 | MESP-60 REST/OpenAPI foundation | Done | PR #10 merged at `2569acbe6dc26223108f7ad539ca7db2bcdf5f93`; versioned contracts, trusted context, safe errors, correlation, idempotency, concurrency and antiforgery seam validated with 188 tests |
| 3 August 2026 | MESP-62 immutable audit and OpenTelemetry evidence | Done | Immutable path-aware evidence, append-before-effect coordinator, safe telemetry hooks and focused tests merged; checkpoint package included in the PR |
| 4 August 2026 | Foundation Backend Review Checkpoint reconciled | Done | `docs/95_Foundation_Backend_Review_Checkpoint.md`; PR #12 merged and MESP-63 authorized next after focused ChatGPT approval |
| 4 August 2026 | MESP-89 foundation host security integration | Done | PR #12 merged at `a1c5627b40e11b14a50736663c6da56cf11c9ef8` after focused ChatGPT approval; ADR-004 reconciled for catalog-backed exact permissions, mandatory protected-write evidence, composite idempotency replay and separate eligibility/selection versions; merged-main validation passed with 247 tests and a 0-warning/0-error Release build; MESP-63 is authorized next |
| 4 August 2026 | MESP-63 Angular Wave 1 shell | Done | Commits `798d15d1aa1e53781df3a2683305e95ac3143890` and `46bf2d30f91ef00e9e450b59b8de0b3a2d34dbab` merged through PR #14 at `ad9e6a7c40d229b564a7232ca62b3d70ec1fdc15`; Angular 22 shell, EN/AR RTL foundation, server session/antiforgery/context integration, safe states and accessibility baseline implemented; focused Angular tests 8/8, mocked Playwright smoke 1/1, backend regression 247/0/0 and Release build 0/0 passed; no Sprint active |
| 4 August 2026 | MESP-90 false-logout correction | Done | PR #16 merged by normal merge at `469ab863a5fc20f02d3ba674a97dceb969bbec75`; approved head preserved; 27 Angular tests, 4 Playwright journeys, backend 247-test regression, Release build and production dependency audit passed; MESP-63 remains Done and MESP-61 started only after closure |
| 4 August 2026 | MESP-61 durable work foundation | Done | PR #17 merged at `7db49a88e11232f055c2016b8bb033a61de629ec`; typed Tenant-bound work/outbox/inbox/worker, notification and private-file contracts; backend 285/0/0, Angular 27/0/0, Playwright 4/0/0 and production audit passed |
| 4 August 2026 | MESP-64 foundation safety harness | Done | PR #18 merged at `2002d1c25d39022b227e89b3d70f41a53de0408c`; ADR-018, disposable LocalDB SQL Server probes and exact 75-assertion report; targeted SQL 11/11, backend 296/0/0, Angular 27, Playwright 4 and production audit 0 vulnerabilities passed |
| 4 August 2026 | Foundation Completion Opus 5 checkpoint | Ready for review | Documentation-only `docs/97_Foundation_Completion_Review_Checkpoint.md`; no implementation item or Sprint active; MESP-48 and MESP-50 remain production gates |
| 6 August 2026 | MESP-91 Correction Package 1 merged | Done | Focused ChatGPT security review APPROVED TO MERGE (0 Critical/0 High/0 Medium blockers); PR #20 merged by normal merge commit at `f2cde57400fed470ab048776e05b56f353b36890`; merged-main validation passed 102/102 focused durable-work, 360/360 backend including 11/11 SQL Server LocalDB, 27/27 Angular, 4/4 Playwright, Release build 0 warnings/0 errors, production audit 0 vulnerabilities; implementation branch deleted; MESP-92 is the next eligible correction |

---

# 11. Change-control rules

- Update this file after each approved milestone.
- Do not mark an item Done based only on a model report.
- Mark Done only after checking the actual Jira state, repository change, generated artifact, or approval evidence.
- Do not change completed product decisions without an explicit decision record.
- Do not silently reorder domain dependencies.
- Do not create implementation scope from unresolved BRD, BPMN, or Lean Implementation Specification gaps.
- Keep one active implementation item unless an explicit founder decision authorizes otherwise.
- Preserve Release 1 B2B ERP scope.
- Keep Retail POS excluded.
- Keep Wafra as Tenant #1 and a validation customer, not a source of Tenant-specific core code.

---

# 12. Overall progress summary

| Phase | Status |
|---|---|
| Phase 1 — PRD | **Done** |
| Phase 2 — BRDs and BPMN | **In Progress** |
| Phase 3 — Lean Implementation Specification: domain and behavior | **In Progress — MESP-86 v0.4 approved combined lean domain/behavior baseline; implementation refinement is limited to the approved Foundation sequence** |
| Phase 4 — Lean Implementation Specification: user journeys | **In Progress — MESP-86 v0.4 approved journeys, states and acceptance baseline** |
| Phase 5 — Lean Implementation Specification: logical data | **In Progress — MESP-86 v0.4 approved logical model, ERD and integrity baseline; physical design remains gated** |
| Phase 6 — Lean Implementation Specification: implementation readiness | **In Progress — MESP-86 v0.4 approved; MESP-57 through MESP-64 completed; the Foundation Completion checkpoint is ready for Opus 5 review** |
| Phase 7 — Jira Backlog | **Done for MESP-27 Wave 1** |
| Phase 8 — Implementation and Automated Testing | **MESP-57, MESP-58, MESP-87, MESP-59, MESP-88, MESP-60, MESP-62, MESP-63, MESP-89, MESP-90, MESP-61, MESP-64, MESP-91, MESP-92, MESP-93 and MESP-94 Done; no implementation item or Sprint active** |
| Phase 9 — Integration, UAT, Release, Operations | **Not Started** |

---

## Current next action

> MESP-92 (`Guarantee single-effect durable work execution and immutable
> typed payloads`) is **In Progress** on branch
> `fix/MESP-92-single-effect-immutable-payloads` at head
> `e991641`. PR #22 received a first focused
> ChatGPT security review (H92-01, H92-02, M92-01, M92-02) and a second
> focused ChatGPT security review (H92-03, H92-04, M92-03, M92-04, L92-01);
> both rounds of corrections are recorded in the overlays below. The Opus 5
> project-wide checkpoint of 6 August 2026 independently verified all ten
> findings as closed at head `271e9df` and recorded two new **non-blocking
> Low** findings (O92-01, O92-02), closed by a bounded correction at head
> `9dc6cb8` — see the O92-01/O92-02 overlay below. A further focused ChatGPT
> re-review at that head raised H92-05 (High) and M92-05 (Medium); both were
> closed by a bounded correction at head `576996f` — see the H92-05/M92-05
> overlay below. A follow-up shipping-boundary correction then found that
> closure incomplete (`MiniErp.App` still granted `InternalsVisibleTo("MiniErp.Api")`)
> and raised/closed H92-06 (High) and M92-07 (Medium), plus the unrelated
> L92-02 (Low) Angular scope cleanup, at the current head — see the
> H92-06/M92-07/L92-02 overlay below; **no known MESP-92 code finding remains
> open**. PR #22
> remains open, non-draft and held unmerged pending a further focused
> ChatGPT re-review; it is **not** approved. MESP-93, MESP-94 and MESP-31
> remain To Do. Master Data and Catalog, Retail POS and future ERP
> transaction work remain out of scope. `MESP-48` and `MESP-50` remain
> explicit production gates. No Sprint is active, and MESP-93, MESP-94 and
> MESP-31 must not start before MESP-92 closes.

## MESP-92 In Progress — single-effect durable work and immutable payloads

MESP-92 corrects four MESP-91-review findings in the merged durable-work
seam: H-5 (mutable stored payload references), H-6 (duplicate protected
effect after a caught post-boundary interruption or uncertain completion),
M-2 (sequential tests presented as concurrency evidence) and L-1 (misleading
Relational store naming). Branch `fix/MESP-92-single-effect-immutable-payloads`
is based on merged-main baseline `32a91f27bc162685fc0db0f38b031d02ffbc99d2`.
A subsequent focused ChatGPT security review of PR #22 raised four further
findings — H92-01, H92-02, M92-01 and M92-02 — corrected below.

An explicitly registered `IDurableWorkPayloadRegistry`/`IDurableWorkPayloadCodec<TPayload>`
pair converts every submitted payload immediately into an immutable,
checksummed `DurableWorkPayloadEnvelope`. `DurableWorkItem` never retains the
caller's original payload reference; every external byte access and every
handler decode returns an independent defensive copy. Unknown payload types,
handler/payload mismatches, checksum tampering and oversized or malformed
payloads fail closed before a handler executes, and payload bytes never
appear in audit or evidence. M92-02 removes the production
`TamperForValidation()` fault-injection hook; checksum-corruption tests use
bounded reflection in the test project instead, and a custom codec's
encode/decode exception is always wrapped in the safe
`DurableWorkPayloadException` with no original message, CLR type name or
payload-controlled data exposed.

A server-owned `DurableWorkEffectKey` guards one protected effect.
H92-01 namespaces that key with an explicit `DurableWorkEffectPurpose`
(`Handler` or `Outbox`) and, for an outbox effect, the immutable `EventId`,
so a handler effect and an outbox effect for the identical
Tenant/WorkItemId/OperationId can never suppress each other even when both
are guarded by the same shared `IDurableWorkEffectExecutor`. H92-03 replaces
the removed `DurableWorkEffectComposition.CreateSharedExecutor()` (which
produced a new, independent ledger on every call) with
`DurableWorkLocalRuntime.Create(operationCatalogue, payloadRegistry)`, the one
approved composition entry point; it is the only place shipping code may
construct `InMemoryDurableWorkEffectGuard`, `DurableWorkEffectExecutor`,
`InMemoryDurableWorkStore` or `DurableWorkDispatcher` (all four constructors
are `internal`), and it supplies the identical executor instance to the store
and the dispatcher it returns. A syntax-tree architecture test scans all of
`src/MiniErp.App` and fails if any of those four types is constructed
anywhere outside `DurableWorkLocalRuntime.cs`. Reservation of that
key remains the single non-reversible boundary: every registered handler
invocation and every outbox effect is routed exclusively through
`ExecuteHandlerEffectAsync`, which a normal handler cannot bypass
(architecture-enforced). H92-02 replaces the generic `DurableWorkHandlerResult`
returned from inside that boundary with an explicit
`DurableWorkProtectedEffectResult` outcome — `Applied`, `NotAppliedRetryable`,
`OutcomeUnknown` or `TerminalNotApplied` — so a bare generic retry can no
longer release a reservation after an effect may already have run; only an
explicit `NotAppliedRetryable` outcome releases it. An interruption
discovered before the reservation boundary permits bounded retry; a caught
exception or cancellation observed inside the running process after that
boundary yields `OutcomeUnknown` and is never automatically retried.
Completed effects replay their exact recorded safe result on duplicate
dispatch. Outbox delivery now reports explicit `Delivered` (Applied),
`RetryScheduled` (NotAppliedRetryable), `DeadLettered` (TerminalNotApplied or
an exhausted retry budget) or `OutcomeUnknown` outcomes.

M92-01 makes `DurableWorkLifecycle.OutcomeUnknown` a dedicated, Tenant-scoped
reconciliation state for both handler work items and outbox messages: normal
polling never selects it, the generic outbox redelivery/replay hook refuses
to restart it, and audit records the safe `work.outcome-unknown`/
`outbox.outcome-unknown` events with no payload or provider exception text.
`IDurableWorkStore.ReadUncertainEffectsAsync` is a read-only reconciliation
port; H92-04 replaces its raw `TenantContext` parameter with a server-issued
`VerifiedDurableWorkReconciliationAuthorization`. `IdentityAuthorizationService`
(as the new `IDurableWorkReconciliationAuthorizer`) live-revalidates actor,
session, Membership-or-SupportGrant validity and a dedicated catalogue-backed
`work.reconciliation.read` permission, reusing the identical
organization-scope ownership/containment logic as MESP-91 dispatch
revalidation so a missing or malformed selected scope fails closed;
`TenantWorkScope.ContainsDescendant` then filters returned records to the
authorized Tenant/Company/Branch/Warehouse boundary and its verified
descendants only. A sibling organization and another Tenant are never
visible, and `PlatformGovernanceContext` has no path into this authorizer. No
production reconciliation UI or provider decision is implemented.

M92-03 gives every returned record an exact, safe identity: it now carries
the exact `DurableWorkEffectKey` (so `OperationId` is always present and
`EventId` is present only for an Outbox-purpose record) plus the exact
verified `TenantWorkScope`, the actual `OutcomeUnknownAt` transition time and
a preserved safe reason. `TenantOutboxMessage` gained explicit
`OutcomeUnknownAt`/`SafeFailureReason` fields, removing the prior reuse of
`NextAttemptAt` as the occurrence time and the hard-coded outbox reason.

M92-04 normalizes every exception a registered payload codec raises --
including one raised as `DurableWorkPayloadException` itself -- to one of
`DurableWorkPayloadRegistry`'s own fixed, safe messages, never attaching the
original exception as `InnerException`; `DurableWorkPayloadException`'s
constructor is now `internal` so only the envelope/registry seam can raise a
trusted one. `OperationCanceledException` still propagates unwrapped, and
checksum-mismatch/oversized-payload rejections keep their own fixed messages.

L92-01 corrects the `OutcomeUnknown`/`IDurableWorkEffectExecutor` documentation:
a caught post-boundary exception, a caught cancellation, provider-reported
uncertainty or a completion-recording failure observed by the running process
-- never an actual process crash, which instead loses this in-memory ledger
entirely and is never represented as a recorded outcome. Production durable
crash recovery for this local Foundation seam remains explicitly deferred.

Genuine concurrency evidence uses `Barrier`-synchronized concurrent Tasks to
prove: one lease winner under active and expired-lease contention; one
effect winner under concurrent reservation; stale-completion rejection after
lease reclaim; and one effect from concurrent duplicate submissions.

`IRelationalDurableWorkStore`/`InMemoryRelationalDurableWorkStore` are
renamed to `IDurableWorkStore`/`InMemoryDurableWorkStore`; the type no longer
implies relational, SQL-backed, process-crash-durable, production-ready or
distributed exactly-once behavior. This adapter preserves only a caught
post-boundary interruption as `OutcomeUnknown`; an actual process crash loses
its in-memory guard and lifecycle state entirely and is not represented as
`OutcomeUnknown` or any other recorded outcome. Production durable crash
recovery remains deferred to a future SQL/durable provider.

Validation on this branch after the second focused-review correction: Release
build **0 warnings/0 errors**; focused DurableWork suite **199/199** passed;
full backend regression **457/457** passed including **11/11** SQL Server
LocalDB probes with no `MiniErpFoundation_*` database remaining after
teardown; Angular unit tests **27/27** passed; Angular production build
succeeded; Playwright **4/4** passed; `npm audit --omit=dev
--audit-level=high` reported **0** vulnerabilities.

MESP-92 is **not** marked Done by this update. PR #22 is opened non-draft and
held unmerged for a further focused ChatGPT re-review of the structurally
enforced single effect ledger, the scope-authorized reconciliation read port,
the exact uncertain-effect identity, the custom codec exception
normalization and the corrected crash terminology. No broker, production SQL
work store, production worker deployment, migration, Master Data
implementation, MESP-48 or MESP-50 work was introduced.

## MESP-92 O92-01/O92-02 focused correction — In Progress

The Opus 5 project-wide checkpoint of 6 August 2026 (recorded below)
independently verified the ten prior findings closed and additionally
recorded two new, non-blocking **Low** findings against the same head
`271e9df`. This overlay closes both on the same branch, at head
`9dc6cb82860b10215d05364f2f6e25f69df3b986`.

O92-01 closes the guard-level evidence gap: `InMemoryDurableWorkEffectGuard.RecordOutcomeUnknown`
used to accept a `safeReason` and discard it, so the effect ledger itself could
not explain why one of its own records became uncertain independently of the
separately stored work-item/outbox reason. The guard now persists the
sanitized reason on its own `EffectRecord` and exposes it read-only through a
new `IDurableWorkEffectGuard.GetOutcomeUnknownReason(key)` query. The existing
Reserved-only write guard on `RecordOutcomeUnknown` already makes the
Reserved -> OutcomeUnknown transition one-way, so a duplicate call or a
different reason for an already-recorded key fails with the same
`InvalidOperationException` it always has -- the original reason can never be
replaced. An empty, whitespace-only, control-character-containing or
over-128-character reason fails closed with `ArgumentException`, mirroring
`DurableWorkHandlerResult`'s existing safe-reason sanitization convention. No
public mutation surface was added.

O92-02 closes the code/comment contradiction: `InMemoryDurableWorkStore.ReadUncertainEffectsAsync`
used to fall back to `message.NextAttemptAt` when `OutcomeUnknownAt` was null,
even though the adjacent comment stated `NextAttemptAt` is never reused as an
uncertain effect's occurrence time. `DurableWorkItem` now carries its own
`OutcomeUnknownAt` (set only on the `OutcomeUnknown` transition inside
`Complete()`, mirroring the field `TenantOutboxMessage` already had), and the
read port's `RequireOutcomeUnknownAt` helper fails closed with a generic
`InvalidOperationException` -- carrying no work item id, tenant id or internal
type name -- instead of substituting `NextAttemptAt`, creation time, update
time, lease time or current time.

New regression coverage in `DurableWorkPayloadAndEffectTests` proves: the
guard preserves the exact approved reason and it survives a round trip through
the executor; a duplicate or different-reason `RecordOutcomeUnknown` call
cannot replace the original; the reason is isolated per exact effect key,
including across handler/outbox purposes and across different outbox
`EventId` values on the same work item; unsafe or unbounded reasons fail
closed; the preserved reason never contains a raw exception message; handler
and outbox uncertain-effect records report their exact transition timestamp;
an outbox record's timestamp is proven distinct from its original
`NextAttemptAt`; and a synthetic corrupted record (built through bounded
reflection over the private store fields, matching the existing M92-02
`CorruptStoredBytesForTest` pattern -- no production member was made public
solely for testing) fails closed on read.

Validation at this head: Release build **0 warnings/0 errors**; focused
DurableWork suite **216/216** passed; full backend regression **474/474**
passed via `validate-foundation.ps1`, including **11/11** SQL Server LocalDB
probes with no `MiniErpFoundation_*` database remaining after teardown;
Angular unit tests **27/27** passed; Angular production build succeeded;
Playwright **4/4** passed; `npm audit --omit=dev --audit-level=high` reported
**0** vulnerabilities; `git diff --check` reported no whitespace errors.

MESP-92 is **not** marked Done by this overlay. PR #22 remains open,
non-draft and held unmerged pending a focused ChatGPT security re-review at
this head -- **no known MESP-92 code finding remains open**. No broker,
production SQL work store, production worker deployment, migration, Master
Data implementation, MESP-48 or MESP-50 work was introduced, and the
`local-prd-rename-before-MESP-92` stash was preserved untouched.

## MESP-92 H92-05/M92-05 focused correction — In Progress

A further focused ChatGPT security re-review of PR #22 at head
`9dc6cb82860b10215d05364f2f6e25f69df3b986` raised H92-05 (High) and M92-05
(Medium). This overlay closes both on the same branch, at head
`576996f94ae9ddc251767445a7ebddd60c492c45`.

H92-05 closed a public mutable-ledger exposure: `DurableWorkLocalRuntime`
publicly exposed `EffectGuard` (`IDurableWorkEffectGuard`) and `EffectExecutor`
(`IDurableWorkEffectExecutor`), so a shipping caller holding the runtime could
call `TryReserve`, `Release`, `RecordCompleted` or `RecordOutcomeUnknown`
directly -- for example reserving an effect through the dispatcher, then
releasing that same reservation through the runtime's public guard while the
protected effect was still executing, letting a second dispatch reserve and
execute it again. `DurableWorkLocalRuntime`'s public surface is now limited to
`Store` and `Dispatcher`; `EffectGuard` and `EffectExecutor` are internal
properties, and `IDurableWorkEffectGuard`, `InMemoryDurableWorkEffectGuard`,
`IDurableWorkEffectExecutor`, `DurableWorkEffectExecutor` and their
state/reservation/execution-result types are internal to `MiniErp.App`. `Store`
and `Dispatcher` continue to share the identical internal guard and executor
instance, unchanged from H92-03.

M92-05 closed the raw-key reason-read bypass: `IDurableWorkEffectGuard.GetOutcomeUnknownReason(key)`
(added by O92-01) required only a `DurableWorkEffectKey`, with no live User,
session, Membership/SupportGrant, `work.reconciliation.read` permission or
exact Tenant/Company/Branch/Warehouse scope check -- bypassing the H92-04
authorized reconciliation port entirely. Because `IDurableWorkEffectGuard` is
now internal, `GetOutcomeUnknownReason` is not reachable from any public type;
it remains solely an internal/test-only seam that still preserves the O92-01
safe reason on the guard's own `EffectRecord`. The only publicly reachable
uncertain-effect evidence path remains
`IDurableWorkStore.ReadUncertainEffectsAsync(VerifiedDurableWorkReconciliationAuthorization)`,
unchanged from H92-04.

`DurableWorkEffectKey`, `DurableWorkEffectPurpose`, `DurableWorkProtectedEffectResult`
and `DurableWorkProtectedEffectOutcome` remain public: the first two are
required by the public `DurableWorkUncertainEffectRecord` reconciliation
evidence contract, and the latter two are the outcome contract a handler
author implementing `IDurableWorkHandler<TPayload>` must return.

New structural/architecture coverage in `DurableWorkEffectLedgerSurfaceTests`
(14 tests) proves: no public `EffectGuard`/`EffectExecutor` property or type
exists anywhere in the `MiniErp.App` assembly; no public method accepts only a
raw `DurableWorkEffectKey` and returns reason or state evidence;
`ReadUncertainEffectsAsync` still has exactly one overload requiring
`VerifiedDurableWorkReconciliationAuthorization`; `Store` and `Dispatcher`
still share one internal guard and executor; and an executable
attack-regression test blocks a handler mid-effect, proves no publicly
reachable member can obtain the guard to release the in-flight reservation,
then completes the handler and issues a duplicate dispatch to confirm the
protected effect executed exactly once. All previously added O92-01/O92-02
tests continue to pass unmodified.

Validation at this head: Release build **0 warnings/0 errors**; focused
DurableWork/composition suite **230/230** passed (up from 216); full backend
regression **488/488** passed via `validate-foundation.ps1`, including
**11/11** SQL Server LocalDB probes with no `MiniErpFoundation_*` database
remaining after teardown; Angular unit tests **27/27** passed; Angular
production build succeeded (351.02 kB initial / 87.80 kB transferred);
Playwright **4/4** passed; `npm audit --omit=dev --audit-level=high` reported
**0** vulnerabilities.

MESP-92 is **not** marked Done by this overlay. PR #22 remains open,
non-draft and held unmerged pending a further focused ChatGPT security
re-review at this head. No broker, production SQL work store, production
worker deployment, migration, Master Data implementation, MESP-48 or MESP-50
work was introduced, and the `local-prd-rename-before-MESP-92` stash was
preserved untouched.

## MESP-92 H92-06/M92-07/L92-02 focused correction — In Progress

A follow-up shipping-boundary correction on PR #22, starting from head
`dd3b4f58e9dcf290d42536dbd4a1196232f378e7`, found that the H92-05/M92-05
`internal` closure above was itself incomplete: `MiniErp.App` still declared
`[assembly: InternalsVisibleTo("MiniErp.Api")]`. A friend assembly sees
another assembly's `internal` members exactly as if they were public, so that
one attribute let the shipping `MiniErp.Api` host reach `EffectGuard`,
`EffectExecutor`, construct `InMemoryDurableWorkEffectGuard`/
`DurableWorkEffectExecutor` directly, and call `TryReserve`, `Release`,
`RecordCompleted`, `RecordOutcomeUnknown` and `GetOutcomeUnknownReason` on the
raw key -- the entire mutable ledger and the M92-05 raw-key reason bypass were
still reachable from the compiled shipping host. **Any earlier statement in
this document that `internal` visibility alone closed that surface while
`MiniErp.App` granted `InternalsVisibleTo("MiniErp.Api")` is corrected here.**
This overlay closes both findings, plus the unrelated L92-02 Angular scope
cleanup, at head `e991641` (`e99164134054ae9f5c48ad53399e1fbb9898225b`).

H92-06 is closed: `backend/src/MiniErp.App/Properties/AssemblyInfo.cs` grants
`InternalsVisibleTo` only to `MiniErp.ArchitectureTests`; the grant to
`MiniErp.Api` is removed. Rebuilding the full solution with only that change
surfaced exactly one `MiniErp.Api` compile break, unrelated to durable work:
`Program.cs`'s sign-in endpoint read the internal
`FoundationHostSignInResult.Principal` to call `HttpContext.SignInAsync`. That
property is now public -- a narrow, intentional application seam carrying
only the `ClaimsPrincipal` this module already issues through
`FoundationIdentityClaims`, never a raw credential -- instead of restoring
friend access. No mutable ledger type, guard or executor was made public or
given friend access back.

M92-07 closes as a direct consequence of the same change: `GetOutcomeUnknownReason`
is declared only on the already-internal `IDurableWorkEffectGuard`, so
removing `MiniErp.Api`'s friend grant removes its only path to that raw-key
evidence too. The sole production uncertain-effect evidence path remains
`IDurableWorkStore.ReadUncertainEffectsAsync(VerifiedDurableWorkReconciliationAuthorization)`,
unchanged.

L92-02 is closed: `frontend/angular.json` is restored to the exact
`origin/main` analytics state (no `analytics` key), removing the unrelated
identifier commit `9e0999e` had added. This is scope cleanup, not an MESP-92
security or feature change.

New structural coverage in `FriendAssemblyPolicyTests.cs` (5 tests) proves,
by reflection and full Roslyn compilation rather than source-scanning
convention: `MiniErp.App`'s `InternalsVisibleTo` allow-list is exactly
`["MiniErp.ArchitectureTests"]`; and source compiled under the assembly name
`MiniErp.Api` fails to compile (`CS0122`) against
`InMemoryDurableWorkEffectGuard`, `DurableWorkEffectExecutor` and their
construct/reserve/release/record/read-reason members, while identical source
compiled under `MiniErp.ArchitectureTests` still succeeds. These tests were
verified to fail against the prior `InternalsVisibleTo("MiniErp.Api")` state
before being verified to pass against this correction, confirming they are a
genuine regression proof for H92-06/M92-07. All previously added H92-05/M92-05
and O92-01/O92-02 tests continue to pass unmodified.

Validation at this head: Release build **0 warnings/0 errors**; focused
DurableWork/ledger/composition/reconciliation suite **238/238** passed (up
from 230); full backend regression **493/493** passed via
`validate-foundation.ps1`, including **11/11** SQL Server LocalDB probes with
no `MiniErpFoundation_*` database remaining after teardown; Angular unit tests
**27/27** passed; Angular production build succeeded (351.02 kB initial /
87.80 kB transferred, unchanged); Playwright **4/4** passed; `npm audit
--omit=dev --audit-level=high` reported **0** vulnerabilities.

MESP-92 is **not** marked Done by this overlay. PR #22 remains open,
non-draft and held unmerged pending a further focused ChatGPT security
re-review at this head. No broker, production SQL work store, production
worker deployment, migration, Master Data implementation, MESP-48, MESP-50,
MESP-93, MESP-94 or MESP-31 work was introduced, and the
`local-prd-rename-before-MESP-92` stash was preserved untouched.

## MESP-91 Correction Package 1 — merged and Done

The MESP-91 correction overlay was implemented on
`fix/MESP-91-verified-work-scope-authority`, based on merged-main baseline
`4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`. Source and regression corrections
are recorded through commit `92bd9fd38912a062cc3723f46867258d54ca8127`
(approved PR #20 head).

The durable-work contract now selects an operation from an authoritative
descriptor catalogue. The descriptor owns the exact permission code, allowed
authorization paths and scope policy; the same immutable descriptor is used by
submission identity, stored initiator facts, handler registration, live
Identity revalidation and worker/verified outbox dispatch. Unknown or
mismatched operation descriptors fail closed. Approved revalidation returns a
server-issued exact-scope execution authorization; the worker and outbox effect
receive that verified context rather than the broad caller context.

True authority denials remain terminal `AuthorizationDenied` outcomes. Provider
exceptions/timeouts are `ProviderUnavailable` bounded retries, and cancellation
is a distinct recoverable `Cancelled` outcome. No handler or protected outbox
effect runs after a failed authority check.

Validation evidence for this correction is **102/102** focused durable-work
tests, **360/360** complete backend tests, **11/11** SQL Server LocalDB
probes, **27/27** Angular tests, **4/4** Playwright journeys, Release build 0
warnings/0 errors, and production dependency audit 0 vulnerabilities — verified
both pre-merge on the approved head and again against merged `main`. The
disposable LocalDB/model collation observed was `SQL_Latin1_General_CP1_CI_AS`;
no disposable Foundation database remained after cleanup.

Approved head `92bd9fd38912a062cc3723f46867258d54ca8127` received a focused
ChatGPT security review disposition of **APPROVED TO MERGE** (0 Critical, 0
High, 0 Medium blockers), closing findings H-1, H-2, H-4, H91-01, H91-02,
H91-03, H91-04, M91-01 and M91-02. PR #20 was merged by normal merge commit at
`f2cde57400fed470ab048776e05b56f353b36890`. MESP-91 is **Done**; MESP-92 is
now **In Progress** on the open, unmerged PR #22; MESP-93, MESP-94 and MESP-31
remain **To Do**. No Sprint, Master Data implementation, production provider,
migration, Retail POS, Wafra-core, MESP-48 or MESP-50 work was started.

---

## Opus 5 project-wide checkpoint — 6 August 2026 (documentation-only)

This checkpoint reviewed the live Jira project, Git history, all Pull Requests,
merged source, the open PR #22 head, automated test evidence, every tracked
Markdown file and the approved PRD. It changed no source, test, Jira or
Pull Request state, and it did not merge PR #22.

### Verified project position

| Item | Verified state (6 August 2026) |
|---|---|
| Product-wide phase | Phase 2 — BRDs **In Progress**; Phase 1 PRD **Done** |
| Approved BRDs | MESP-27, MESP-28, MESP-29, MESP-30 **Done** |
| Next business-domain BRD | MESP-31 Master Data and Product Catalog — **To Do**, not started |
| Master Data implementation | **Not started**; blocked pending its BRD and implementation gates |
| Merged-main baseline | `32a91f27bc162685fc0db0f38b031d02ffbc99d2` |
| Active branch / head | `fix/MESP-92-single-effect-immutable-payloads` / `271e9dfedce8e0ea44ef9f8d3ab6e6b61d984ac4` |
| MESP-91 | **Done** |
| MESP-92 | **In Progress** — only active implementation item |
| MESP-93 / MESP-94 | **To Do** |
| MESP-48 / MESP-50 | **To Do** — open production gates, preserved |
| PR #20, PR #21 | Merged |
| PR #22 | **Open, non-draft, unmerged** |
| Sprint | None active |
| Final Foundation correction checkpoint | **Not started** |
| Hosted CI | **None configured** — all validation below is local only |

### Verified branch composition

Every commit on `fix/MESP-92-single-effect-immutable-payloads` above the
merged-main baseline belongs to MESP-92, except the last, which is the
user-authorized PRD move:

| Commit | Purpose |
|---|---|
| `e03ef44` | MESP-92 — persist immutable typed payload envelopes |
| `1f06985` | MESP-92 — prevent duplicate effects after uncertain completion |
| `c95062e` | MESP-92 — concurrency race tests |
| `75d32f3` | MESP-92 — clarify local durable-work maturity |
| `4c933f0` | MESP-92 — namespace effect identities; explicit outcomes |
| `83f0412` | MESP-92 — remove production corruption seam; sanitize codec exceptions |
| `6ef02e4` | MESP-92 — first-round review test corrections |
| `2db44f3` | MESP-92 — first-round review documentation |
| `abeebc1` | MESP-92 — H92-03 one local effect ledger |
| `fadeddc` | MESP-92 — H92-04/M92-03 scoped reconciliation and exact effect identity |
| `62e335d` | MESP-92 — M92-04 normalize all codec failures |
| `87b61da` | MESP-92 — attack composition and reconciliation boundary tests |
| `e2fd63b` | MESP-92 — L92-01 crash-maturity documentation |
| `271e9df` | **Authorized PRD move only** — `MiniERPSaaSPlatform_PRD_v1.2.docx` → `docs/MESP_PRD_v1.2.docx`, detected by Git as `R100`, blob unchanged |

### Local validation rerun at head `271e9df`

| Check | Result |
|---|---|
| Backend Release build | Succeeded — **0 warnings, 0 errors** |
| Backend regression (`validate-foundation.ps1`) | **457/457** passed, 0 failed, 0 skipped |
| SQL Server LocalDB probes | **11/11** passed, 0 failed, 0 skipped |
| Disposable database cleanup | No `MiniErp%` database remained in `MSSQLLocalDB` |
| Angular unit tests | **27/27** passed across 5 files |
| Angular production build | Succeeded — 351.02 kB initial, 87.80 kB transferred |
| Playwright end-to-end | **4/4** passed |
| `npm audit --omit=dev --audit-level=high` | **0** vulnerabilities |
| Hosted CI | Not available — no hosted workflow exists |

The complete frontend regression (unit, production build, Playwright and
production dependency audit) was rerun at this head, closing the earlier gap
where it had not been rerun after the second MESP-92 correction.

### Code verdict

**CHANGES REQUIRED BEFORE MERGE** — 0 Critical, 0 High, 0 Medium, 2 Low. The
two Low findings (O92-01, O92-02) are non-blocking evidence/documentation
issues; the merge hold on PR #22 is the standing MESP-92 process gate awaiting
focused ChatGPT re-review, not an outstanding security defect.

- **O92-01 (Low)** —
  `InMemoryDurableWorkEffectGuard.RecordOutcomeUnknown(key, safeReason)`
  accepts a safe reason and discards it; the guard retains no reason for an
  uncertain effect. Reconciliation evidence therefore depends entirely on the
  separately stored work-item/outbox reason. Not exploitable; an evidence
  completeness weakness only.
- **O92-02 (Low)** —
  `InMemoryDurableWorkStore.ReadUncertainEffectsAsync` still falls back to
  `message.NextAttemptAt` when `OutcomeUnknownAt` is null, while the adjacent
  comment states `NextAttemptAt` is never reused as the occurrence time. The
  fallback is currently unreachable because `OutcomeUnknownAt` is always set on
  the transition, so this is a code/comment contradiction rather than a
  behavioral defect.

### Verified maturity boundaries

- `DurableWorkLocalRuntime`, `InMemoryDurableWorkStore`,
  `DurableWorkDispatcher` and `TenantDurableWorkWorker` are **not referenced by
  `MiniErp.Api`**. The durable-work seam is a contract and local adapter with
  test coverage; it is **not composed into the running host** and is not a
  production capability.
- The single-ledger guarantee is enforced by internal constructors plus a
  syntax-tree architecture test that scans the whole `backend/src` tree.
  `MiniErp.App` grants `InternalsVisibleTo("MiniErp.Api")`, so the compiler
  alone does not stop the future host composition root from constructing an
  independent ledger — the architecture test is what closes that path, and it
  matches only direct `new` expressions.
- The in-memory effect ledger is not process-crash durable. Production durable
  crash recovery, distributed exactly-once delivery, a production SQL work
  provider, a broker, production object storage and a production notification
  provider all remain deferred.

### Preserved gates and exclusions

MESP-48 supported-volume/performance and MESP-50 retention, privacy,
legal-hold, purge, residency, backup and restoration remain open production
gates. No production deployment, production SQL work provider, broker,
production object storage, production notification provider, migration, Master
Data implementation, Retail POS behavior or Wafra-specific core behavior
exists. Wafra remains a validation Tenant only.

### Next authorized sequence

1. Correct the bounded MESP-92 findings (O92-01, O92-02 are optional and
   non-blocking).
2. Obtain the focused ChatGPT security review of PR #22.
3. Merge PR #22 only after that review authorizes it.
4. Reconcile documentation to the merged state.
5. Close MESP-92.
6. Start MESP-93.
7. Complete MESP-94.
8. Complete the final Foundation correction checkpoint.
9. Begin the MESP-31 BRD only after its entry conditions are reconfirmed.
10. Keep Master Data implementation blocked until its BRD and implementation
    gates are approved.

### MESP-92 closure overlay — supersedes the sequence above (7 August 2026)

Steps 1–5 of the "Next authorized sequence" above are now complete: the
bounded O92-01/O92-02 correction, then H92-05/M92-05 and H92-06/M92-07/L92-02,
closed every MESP-92 finding; a focused ChatGPT security review of PR #22 at
final reviewed head `3ec6b45` returned verdict APPROVED FOR MERGE; PR #22 was
merged to `main` at `322341e70e56270797d5770b4b90342c20b7833e`; documentation
was reconciled; and MESP-92 was moved to **Done** in Jira. Step 6, starting
MESP-93, is now underway. All narrative and status text above this overlay
that describes MESP-92 as "In Progress" or PR #22 as "open, non-draft and
unmerged" is the preserved historical record of the correction sequence, not
the current state. `.ai/CURRENT_STATE.md` is the canonical live-state
document for MESP-93's exact branch, head and Pull Request.

### MESP-93 implementation overlay (7 August 2026)

MESP-93 (Correction Package 3 — private-file access outcomes, filename
validation and notification bounds) is implemented on branch
`fix/MESP-93-private-files-notifications`, based on `main` at `322341e`.
Seven findings are closed: M-1 (foreign vs missing file existence oracle),
M-4 (expired/invalid object overwrite), M-5 (unsafe Unicode filename
controls), M-7 (unbounded notification retry), M-8 (unverified notification
recipient), M-9 (untested returned-content immutability) and L-4 (dead
`AnonymousDenied` enum member). 45 new focused tests were added. Full
validation: Release build 0 warnings/0 errors; full backend regression
**538/538** passed including **11/11** SQL Server LocalDB probes; Angular
**27/27**, Playwright **4/4**, `npm audit` **0** vulnerabilities. MESP-93 is
**not** marked Done; its Pull Request is held open, non-draft and unmerged
pending a focused ChatGPT security review, the same standing merge-hold
gate MESP-92 carried. MESP-94 and MESP-31 remain To Do; no production object
storage, notification provider or physical purge was introduced. See
`.ai/CURRENT_STATE.md` for the exact PR number and head once opened.

### MESP-93 focused re-review overlay (7 August 2026, historical)

A focused ChatGPT/Copilot re-review of [PR #24](https://github.com/Hossam1104/mini-erp-saas-platform/pull/24)
at pushed head `759eb04` returned CHANGES REQUIRED BEFORE MERGE, raising
H93-01 (High — wrong-Tenant notification delivery could mutate/dead-letter
the owner Tenant's intent, partly outside the adapter's synchronization
boundary), H93-02 (High — the recipient authorizer validated the recipient
but never live-revalidated the caller's own Membership/SupportGrant
authority), M93-01 (Medium — `INotificationRecipientAuthorizer` implemented
but not registered in DI), M93-02 (Medium — a disposed or already
checksum-failed object was misleadingly surfaced as `Expired`) and L93-01
(Low — avoidable filename over-rejection plus incomplete exact-policy
tests). All five are closed at head `1820416`: see the closure entry in
`.ai/CURRENT_STATE.md` for the exact corrections and validation totals
(28 new tests, 566/566 backend, 11/11 SQL LocalDB, 27/27 Angular, 4/4
Playwright, 0 audit vulnerabilities). At the time this overlay was written,
MESP-93 remained not Done and PR #24 was held open, non-draft and unmerged
pending the next focused ChatGPT security re-review. **Superseded — see the
closure overlay below.**

### MESP-93 closure overlay — supersedes the sequence above (7 August 2026)

The further focused ChatGPT security re-review requested above completed
with verdict **APPROVED FOR MERGE** at reviewed head
`83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`. PR #24 was merged to `main` at
`005c796629341ab9becfbc6d1abe2ae34b6a7332`. Post-merge validation on `main`
(rerun, not copied from pre-merge) passed: Release build 0 warnings/0 errors;
full backend regression **566/566** passed, 0 failed, 0 skipped, including
**11/11** SQL Server LocalDB probes with no `MiniErpFoundation_*` database
remaining after teardown; Angular unit tests **27/27** passed; Angular
production build succeeded (351.02 kB / 87.80 kB, unchanged); Playwright
**4/4** passed; `npm audit --omit=dev --audit-level=high` reported **0**
vulnerabilities; `git diff --check` clean. All seven original findings (M-1,
M-4, M-5, M-7, M-8, M-9, L-4) and all five focused re-review findings
(H93-01, H93-02, M93-01, M93-02, L93-01) are closed. MESP-93 was moved to
**Done** in Jira.

PR #23 (a separate docs-only MESP-92 reconciliation branch, opened before
PR #24 merged) was investigated rather than mechanically conflict-resolved:
every one of its 11 changed files was found identical to, or a strict subset
of, the equivalent content PR #24 had already carried onto `main`. It was
closed as superseded without merging, preserving its history.

MESP-94 (Correct Foundation safety-catalogue classifications and
validation-evidence accuracy) was the next eligible Foundation correction; it
uses normal bounded review, not the MESP-92/MESP-93 manual security merge
hold. MESP-31 remains To Do. All narrative and status text above this
overlay that describes MESP-93 as "In Progress" or its Pull Request as "open,
non-draft and unmerged" is the preserved historical record of the correction
sequence, not the current state. `.ai/CURRENT_STATE.md` is the canonical
live-state document.

**Superseded (7 August 2026):** PR #25 (docs-only MESP-93 post-merge
reconciliation) merged to `main` at
`9f333c9734c767673e43a30d6b57c05793e1fb69`. MESP-94 was then started: Jira
transitioned To Do -> In Progress, and branch
`fix/MESP-94-foundation-validation-evidence` was created from that head. See
`.ai/CURRENT_STATE.md` and `docs/96_Foundation_Release1_Safety_Validation.md`
for MESP-94's exact scope, findings and evidence.

### MESP-94 closure and Foundation completion checkpoint — supersedes the sequence above (8 August 2026)

Steps 6–8 of the "Next authorized sequence" recorded earlier in this document
are now complete. MESP-94's R1–R7 and F1–F2 focused review rounds both
closed; a ChatGPT final merge review of PR #26 at approved exact head
`2c7ed3dec4662672bb78967ceb70db7ed73eb7d4` returned verdict **APPROVED FOR
MERGE** (0 Critical, 0 High, 0 Medium blockers); PR #26 merged to `main` at
actual merge commit `06d837c958c1cb7977dc121e3aaea4e7278944fd`; post-merge
focused verification passed (`SafetyCatalogueValidationTests` +
`SqlServerSafetyTests` 25/25, lock harness 5/5, both `git diff --check`
scopes clean, 0 orphan databases); and MESP-94 was moved to **Done** in
Jira. All narrative and status text above this overlay that describes
MESP-94 as "In Progress" or its Pull Request as "open" is the preserved
historical record of the correction sequence, not the current state.

A bounded Foundation completion checkpoint followed (not a redesign):
MESP-92, MESP-93 and MESP-94 are confirmed Done; MESP-48 and MESP-50 remain
intentionally open production gates and are not treated as blockers to
MESP-31 BRD entry; no remaining Foundation correction ticket blocks BRD
entry. See `docs/97_Foundation_Completion_Review_Checkpoint.md` for the
checkpoint record.

**Step 9 (begin the MESP-31 BRD only after its entry conditions are
reconfirmed) is evaluated, not executed.** Foundation completion is a
necessary but not sufficient condition: the only recorded precedent for
"reconfirming" a BRD's entry conditions in this repository is MESP-29's,
where a distinct, explicit founder/owner authorization statement — not
Foundation completion alone — permitted the Task to move to In Progress
(`docs/13_Multi_Tenancy_BRD.md` SC-001). No equivalent authorization is
recorded for MESP-31, and Jira MESP-31 itself still carries the standing
instruction not to move to In Progress until its BRD entry criteria are
approved. This step-9 evaluation and its "NOT YET ELIGIBLE" conclusion are
the preserved historical record as of the Foundation completion checkpoint;
**superseded 8 August 2026:** Hossam recorded the required distinct
BRD-entry owner authorization, explicitly scoping MESP-31 to Products,
Product Categories, Units of Measure, Suppliers, Business Customers, Price
Lists, Taxes, Payment Terms, Currencies and Exchange Rates, and separately
pre-authorized the later Master Data implementation phase (not yet
executable). MESP-31 moved to **In Progress** on branch
`docs/MESP-31-master-data-product-catalog-brd`; a v0.1 draft BRD was produced
at `docs/16_Master_Data_and_Product_Catalog_BRD.md` and is pending Hossam's
business-owner review — it is not Approved. Step 10 (Master Data
implementation blocked until its BRD and a dedicated implementation Jira
item are approved) continues to apply unchanged. `.ai/CURRENT_STATE.md`
is the canonical live-state document for the exact current position.

### MESP-31 BRD review round — supersedes every status statement above (8 August 2026)

This overlay is the current position. Every narrative and status statement
earlier in this document — including any that describes MESP-93 or MESP-94 as
"In Progress", MESP-31 as "To Do", or an earlier Pull Request as "open" — is
the preserved historical record of the Foundation correction sequence, not the
current state.

**Foundation:** MESP-92, MESP-93 and MESP-94 are all **Done**. The final
merges were PR #26 to `main` at `06d837c958c1cb7977dc121e3aaea4e7278944fd`
(approved head `2c7ed3dec4662672bb78967ceb70db7ed73eb7d4`, ChatGPT final merge
review verdict APPROVED FOR MERGE) and PR #27 to `main` at
`c86ecb851e88205f1d3907f5a5c36cfb59ce8b54`, which is the **current
merged-main baseline**. No Foundation correction ticket remains open. MESP-48
and MESP-50 remain intentionally open **production** gates and are not BRD
entry blockers.

**MESP-31:** **In Progress**, under Parent Epic
`MESP-6 — EPIC 06 - Master Data and Product Catalog`. Both Owner
authorizations are recorded in live Jira — comment `10615` (BRD entry) and
comment `10616` (future Master Data implementation, conditional on this BRD's
approval and activation of a dedicated, separate implementation Jira item).
MESP-31's Jira Source Baseline is corrected to primary anchor **PLT-003** with
supporting anchors PLT-002, SAL-001, PROC-002, PROC-008, FIN-001, FIN-003,
FIN-007, FIN-010, KSA-002, BR-013, ADM-003 and the applicable PRD RULE set for
master-data integrity; PLT-011–PLT-014 and BR-004 are Platform Administration
anchors and are no longer MESP-31's baseline.

**PR #28** ([link](https://github.com/Hossam1104/mini-erp-saas-platform/pull/28))
is **open and unmerged** on branch `docs/MESP-31-master-data-product-catalog-brd`,
based on `main` at `c86ecb851e88205f1d3907f5a5c36cfb59ce8b54`. Its v0.1 draft
at head `6d0aa80eef0a2860c85a141dd6f13ee38bf5760d` received a
business-requirements review verdict of **CHANGES REQUIRED BEFORE OWNER
APPROVAL / MERGE** and has been corrected to v0.2: MESP-41 reclassified as an
unapproved recommended default and raised as Open Decision MD-OD-010; MESP-54
reclassified as a Deferred Gate / Recommended Default owned by MESP-34;
unsupported Tax and Price List separate-approver claims withdrawn into
MD-OD-005 behind a generic no-self-approval control; the Draft-before-Active
question restored to a genuine Open Decision; a lifecycle wording error
corrected; Business Party cross-role duplicate semantics clarified; and the
Tenant isolation boundary separated from the still-undecided Company/Legal
Entity business scope. The Open Decision register now holds ten decisions.

**The BRD remains Draft and is not Approved.** MESP-31 stays In Progress; it
is not moved to Done by this correction round. **No Master Data implementation
is active, authorized to start, or permitted to start automatically** — the
recorded implementation authorization becomes executable only after Hossam
approves this BRD as a business baseline and a dedicated implementation Jira
item, separate from MESP-31, is identified and activated. No Sprint is active
and no parallel implementation item exists. `.ai/CURRENT_STATE.md` remains the
canonical live-state document.

### MESP-31 BRD second correction round — supersedes the above (8 August 2026)

The v0.2 draft was reviewed at head `865701128c86d358f6aa919162c91d91ae025f21`
and received a further verdict of **CHANGES REQUIRED — FINAL SMALL
CORRECTION ROUND**: M31-R10 (MD-BR-015's Product/Item modelling needed an
Owner decision rather than Confirmed status), M31-R11 (§27's routine-edit row
and MD-AC-016 carried residual approval assumptions not established by any
approved source), M31-R12 (MD-OD-007's blocking rationale made a
production-compliance claim ahead of external Saudi validation) and M31-R13
(the PR delta carried an unrelated `.vscode/settings.json` change). A second
bounded, documentation-only correction round closed all four and produced
**v0.3**: MD-BR-015 withdrawn from Confirmed status and raised as new Open
Decision MD-OD-011; the §27 routine-edit row and MD-AC-016 reworded to remove
the residual approval assumption, both now explicitly MD-OD-005-dependent;
MD-OD-007's rationale corrected to distinguish BRD/implementation-baseline
non-blocking from a still-gated production launch, preserving External
Validation Required; and `.vscode/settings.json` removed from the PR #28
branch delta. The Open Decision register now holds **eleven** decisions
(MD-OD-001 through MD-OD-011). **The BRD remains Draft and is not Approved;**
MESP-31 stays In Progress; PR #28 remains open and unmerged; no Master Data
implementation is active or permitted to start automatically.
`.ai/CURRENT_STATE.md` remains the canonical live-state document and carries
the exact new reviewed head.

### MESP-31 Owner approval overlay — supersedes the above (8 August 2026)

The review chronology above is preserved. Hossam approved MESP-31 BRD v0.3 as
the Release 1 **Approved Business Baseline** in Jira comment `10649` at
reviewed content head `1e2d055354f0ddde833190948d09fa426707484c`. The approval
does not silently answer MD-OD-001 through MD-OD-011; the register remains
open, and decisions marked blocking remain implementation-slice gates. PR #28
is approved for merge but remains open and unmerged until the approval-state
reconciliation is pushed and reverified. MESP-31 remains In Progress until
the actual merge and Jira closure. MESP-95 exists as To Do and is the single
next authorized implementation-readiness item only after those gates. No
Master Data source implementation has started. MESP-48, MESP-49 and MESP-50
and all qualified external-production gates remain open.

### MESP-31 closure and MESP-95 implementation-readiness overlay — supersedes the above (8 August 2026)

The historical MESP-31 approval and review chronology above is preserved.
The current verified state is that MESP-31 is **Done**. PR #28 merged with
final PR head `8396197b54189cb550f07bd4bb6779fd38ac30cb` at actual merge
commit `1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b`; the approved reviewed BRD
head `1e2d055354f0ddde833190948d09fa426707484c` is an ancestor of `main`.
Final Jira closure evidence is MESP-31 comment `10650`. The BRD remains the
v0.3 Approved Business Baseline and MD-OD-001 through MD-OD-011 remain open;
approval silently resolves none of them.

MESP-95 already existed as the dedicated next Task and was transitioned from
To Do to **In Progress** only after the Stage-A exit gate. Its branch is
`docs/MESP-95-master-data-lean-implementation-spec` and its draft document is
`docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`.
The documentation-only readiness review is published as open, non-draft PR #29
from initial draft head `dc550e1171e8f9d20cd7fdf5509dfffb7537b3bd`; it is not a
source implementation PR and must not be merged automatically.
The document defines implementation-readiness boundaries and a proposed
sequential backlog only. It creates no source implementation, migration,
database, credential, Jira child slice, Sprint, Retail POS behavior, or
Wafra-specific behavior. MESP-23 remains the separate In Progress governance
task for the Open Questions Register; no implementation/readiness item other
than MESP-95 is active. MESP-48, MESP-49, and MESP-50 remain open gates.

### Current MESP-95 closure and MESP-96 activation overlay - 8 August 2026

The historical progress and MESP-95 review overlays above are preserved. The
current verified state is that MESP-31 is **Done**, PR #29 is merged normally
at `93f4e83992ef46f498cfbfacbb513cfc3d8dda7d` from approved head
`c465d660e49a254f2fffbb95e0d07c5fcf17a193`, and MESP-95 is **Done** with Jira
closure evidence `10654`. MESP-96 is **In Progress** as the single next
executable implementation item for M95-SL-01.

M95-SL-01 is contract-only and non-persistent. It creates no Master Data
entities/tables, migration, or `MESP` database/access solely for the slice and
does not decide Product/Item, SKU/Barcode, tracking, business availability,
approval catalogue, or Draft/Active behavior. It adds no Wafra-specific or
Retail POS behavior and does not start M95-SL-02. MD-OD-001 through MD-OD-011
remain unresolved; MESP-48, MESP-49, and MESP-50 remain open gates; no Master
Data persistence exists.

### MESP-96/M95-SL-01 completion overlay - 8 August 2026

The MESP-96 source slice is complete at its approved contract-only boundary.
It adds the Master Data/Catalog and Business Parties composition seams,
server-derived Tenant context consumption, policy-neutral `BusinessScope` and
scope-policy hooks, capability/resource/approval authorization hooks,
tenant-aware stable reference contracts, and audit/evidence integration. The
implementation creates no Master Data entity, table, migration, database
access, endpoint, Product/Item identity, SKU/Barcode rule, tracking behavior,
availability rule, approval catalogue, or Draft/Active lifecycle behavior.

Release build and the focused `MasterDataBoundaryTests` plus
`ModuleBoundaryTests` passed with 32/32 tests and zero warnings/errors. PR #30
merged at `87f150d95f583168a86aa56200916343c6404f7f`; Jira completion evidence
is comment `10655`, and the exact branch/PR/merge/Jira evidence is recorded in
`.ai/CURRENT_STATE.md`. M95-SL-02 Category and UOM remains the next exact
session and is gated by the affected Owner decisions and ADR timing; it was
not started automatically.

### MESP-96 post-merge correction overlay - 8 August 2026

The historical MESP-96 completion overlay above is preserved. A bounded
post-merge correction repaired the optional target-hint semantics in
`MasterDataTenantContextResolver.Resolve`: no selection, an empty selection,
and a same-Tenant tenant-only hint preserve the trusted server-derived
Tenant/scope authority; an exact trusted scope remains compatible; foreign
Tenant and sibling/foreign scope remain denied. Client input does not create
broader or replacement authority.

The correction was committed as `85d3c48f20a97f8057e5960c305a3bcc0cb8d672` on
`fix/mesp-96-optional-scope-hint`, published as PR #31, and merged to `main` at
`4eeefe0d1a9af209cc3e31608812ec35ef283fd9`. Merged-main validation passed with
0 Release build warnings/errors and 34/34 focused boundary tests. The
correction added no persistence, migration, database, endpoint, Product/Item,
SKU/Barcode, tracking, availability, approval-catalogue, lifecycle, Retail
POS, or Wafra-specific behavior. MESP-96 remains **Done**; Jira correction
evidence is comment `10657`; the exact final synchronized handoff main is
recorded in `.ai/CURRENT_STATE.md`. The original PR #30 review thread was
replied to and resolved. M95-SL-02 Category and UOM is the next exact session
and was not started; MD-OD-001 through MD-OD-011 and MESP-48/MESP-49/MESP-50
remain open.
