# Release 1 MESP-116 Approved Decision and Dependency Map

## Current checkpoint overlay - 30 August 2026

This overlay does not change the approved plan baseline. MESP-132 through
MESP-137 are complete at their accepted bounded scopes. MESP-137 is the latest
merged capability through PR #84 from accepted feature head
`9406e8c6408251323b96d4a0c25082142546b9ef` at merge commit
`6b3aeb63da15253dee5466f7be001773b80c28ad`. PR #85 carries the post-closure
documentation reconciliation to `main` at
`4d6e33189a3835d5d8d2a58736055a837a3f5bc9`. MESP-144 reconciliation reached
Sol content acceptance at comment `12293` on reviewed head
`ffe5a8975611dcc85c3a7c40dce0b3737b123aeb`. HOLD 5 merge-safety authority is
comment `12296`; at that executor handoff, Jira and PR lifecycle had not yet
been finalized, with Jira and GitHub authoritative for their respective final
states.

Fast-track completion is `21/26 = 80.8%`; this is capability completion, not
production readiness. Overall production readiness remains approximately `47%`
and Procurement/P2P `41%`. MESP-138 through MESP-142 remain To Do/inactive.
MESP-48 and MESP-50 remain open production gates. MESP-144 is recorded as the
repository-health checkpoint under the Sol content acceptance and HOLD 5
evidence above; its final Jira and PR lifecycle are governed by Jira and
GitHub respectively.

<!-- MESP-132-EXECUTION-START -->
## Execution overlay â€” 24 August 2026

This overlay does not change the approved plan baseline.

Inventory fast-track execution currently stands at:

- MESP-128 â€” Done.
- MESP-129 â€” Done.
- MESP-130 â€” Done.
- MESP-131 â€” Done; PR #75 merged to `main` at
  `a8664d6a0d006e463a1a03fadd76c28475475f58`.
- MESP-132 â€” merged through PR #76 under Epic MESP-10; Jira remains In Progress
  pending Sol closure and Finance Epic reconciliation.

This block is historical evidence from the 24 August 2026 MESP-132 handoff.
<!-- MESP-132-EXECUTION-END -->

**Date:** 12 August 2026 (historical approval baseline)
**Status:** Approved MESP-116 governance handoff; current acceptance overlay is above
**Owner evidence:** MESP-116 Jira comment `10957`
**Product Decision evidence:** MESP-22 Jira comment `10958` (PD-025-PD-046)
**Living register:** MESP-23 Jira comment `10976`
**Current acceptance handoff:** MESP-137 accepted feature head `9406e8c6408251323b96d4a0c25082142546b9ef`; PR #84 squash SHA `6b3aeb63da15253dee5466f7be001773b80c28ad`; post-closure main reconciliation is `4d6e33189a3835d5d8d2a58736055a837a3f5bc9`; MESP-144 HOLD 3 is recorded in Sol comment `12288`
**Repository evidence:** PR #59 reviewed at `8b3f7b61c0128f97aa6a775dec23e623c1fde70e`, merged at `b58bcaaeb4103c8fbdfb6a1c933c5239e228c5bd`

## 1. Authority and boundary

Hossam approved A1-A16 and B1-B6 at the exact bounded positions in the
Consolidated Owner Decision Pack. Class B is the Release 1 product and
implementation contract, not production approval: Finance, Inventory,
Reporting, Migration, Security/Audit, SQL/provider, and other named specialist
validation remains mandatory before production acceptance, irreversible
accounting/valuation/posting, destructive migration, cutover, rollback
commitment, or production distribution.

C1-C9 remain open production/external/legal/provider/infrastructure gates.
MESP-39 remains future-release and unactivated. MESP-40 remains a required
Release 1 migration/onboarding task but is unactivated. MESP-133 through
MESP-137 are complete at their accepted scopes; MESP-138 through MESP-142
remain To Do/not activated. MESP-144 is recorded as the health checkpoint;
its content acceptance and HOLD 5 merge-safety evidence are recorded above.

## 2. Cross-cutting gates used by every capability

| Gate | Required boundary | Evidence before capability completion |
|---|---|---|
| G-SEC | Tenant is derived server-side; client input cannot broaden Tenant/Company/Branch/Warehouse authority; permissions, SoD, concurrency, denial, and support-access rules are enforced. | Focused authorization/Tenant-isolation tests and failure evidence. |
| G-AUD | Material business effects, approvals, corrections/reversals, imports, exports, and privileged actions have immutable, path-aware audit evidence. | Affected audit tests and traceable business scenarios. |
| G-LOC | EN/AR labels, Arabic/English content, RTL/LTR layout, localized numbers/dates/currency, accessibility, loading/empty/error/denied/unknown states are included where relevant. | Focused Angular tests/build and bilingual/RTL journey evidence; ADR-011 remains authoritative. |
| G-DATA | Module-owned source of truth, database integrity, idempotency/concurrency, accounting/stock invariants, and safe correction/reversal are explicit. | Affected backend/module/database tests; SQL/provider evidence is reported honestly. |
| G-PROD | MESP-48, MESP-50, SQL/provider, credentials, infrastructure, deployment, privacy/legal, external/statutory, and volume gates are not guessed. | Production/irreversible claims wait for the named gate and specialist evidence. |

## 3. Capability dependency map

The map is the implementation ordering and Definition-of-Ready handoff. The
existing capability descriptions in the full-feature plan and Jira remain the
detailed scope source; this map records the cross-module prerequisites and
acceptance boundary needed to activate one capability at a time.

| Capability / owning Epic | Prerequisite decision, BRD, ADR | Source-of-truth module | Backend / API / DB / UI surfaces | Auth, audit, localization, and validation gates | Preview / full Release 1 acceptance |
|---|---|---|---|---|---|
| **MESP-117** / MESP-6 Master Data | Approved Product/Supplier/Customer bounded slices; docs/16, docs/17, docs/18-20; PD-033/035/036 where consumed; ADR-002, ADR-005, ADR-011. | Existing Category, UOM, Product, Supplier, and Business Parties contracts; server-derived Master Data authority. | Reuse/close only missing list/search/filter/pagination/detail/form/workflow contracts; Angular shared headers, grids, forms, validation, status/actions; no new domain expansion. | G-SEC/G-AUD/G-LOC/G-DATA; focused backend boundary tests, Angular tests/build, authorization/denial/concurrency/audit checks. | **Preview:** usable real UX for existing slices. **Full R1:** complete reusable bilingual Master Data UX with historical references and downstream contract evidence. **Not activated in MESP-116.** |
| MESP-118 / MESP-6 | docs/16 and approved Currency/Payment Terms contract; PD-031/043/044; Finance/Reporting input; ADR-002/005/011. | Master Data currency and terms configuration; Finance consumes effective references. | Master/API/DB/UI for currency and terms, effective dates, due-date behavior, permissions, audit. | G-SEC/G-AUD/G-LOC/G-DATA; Finance/Reporting validation; no external rate/provider work. | **Preview:** local bounded configuration journey if ready. **Full R1:** deterministic terms/currency references consumed by AP/AR and reports. |
| MESP-119 / MESP-6 | docs/32; PD-024 and PD-037/040/043/046 as applicable; Master Data/Finance BRDs; ADR-002/005/011. | Tenant-owned Tax/VAT identity, category, code, effective version, and applicability configuration. | Tax master, calculation contract, applied-rate evidence, API/DB/UI, audit; no statutory adapter. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance posting, Tax/VAT, Reporting, and specialist validation. | **Preview:** internal configuration-led tax journey with reproducible evidence. **Full R1:** reusable tax calculation/posting/reporting/return contract without ZATCA/FATOORA or legal claim. |
| MESP-120 / MESP-6 | PD-043 / MESP-54; Finance and Reporting BRDs; ADR-002/005/011. | Master Data configured rates and currency identities; Finance owns applied accounting result. | Manual/effective-dated rate master, transaction/Reporting Currency references, API/DB/UI, source notes and audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance/Reporting specialist validation; no automated external FX. | **Preview:** manual rate configuration and evidence. **Full R1:** historical applied-rate, realized/unrealized FX, revaluation, and reconciliation contract. |
| MESP-121 / MESP-6 | PD-034 / MD-OD-004/SAL-OD-01; Master Data and Sales BRDs; ADR-002/005/011. | Tenant-owned Price List and deterministic precedence service. | Effective-dated price list API/DB/UI, currency/UOM/tax interaction, source snapshot, audit. | G-SEC/G-AUD/G-LOC/G-DATA; no promotion engine; deterministic precedence tests. | **Preview:** visible price source and bounded B2B pricing. **Full R1:** repeatable pricing through Sales, returns, credits, and reports. |
| MESP-122 / MESP-6 | PD-041/042; MESP-40 contract but not activation; MESP-53; docs/16-20; ADR-002/005/011. | Versioned import batches, validation/quarantine, Master Data records, downstream references. | Import API/DB, row provenance/error status, repeatability, audit/report references, bilingual Angular errors. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; migration/reporting validation; no destructive overwrite. | **Preview:** safe local import/quarantine slice. **Full R1:** repeatable authorized Master Data import and references consumed by migration. |
| MESP-123 / MESP-7 | PD-026/032; Procurement BRD; Finance/Security/Audit; ADR-002/005/007/011. | Procurement document and reusable approval policy. | Request/quotation API/DB/UI, stages, SoD, delegation, attachments, audit, rejection/expiry. | G-SEC/G-AUD/G-LOC/G-DATA; approval/SoD/idempotency tests. | **Preview:** real request-to-approval journey. **Full R1:** controlled reusable approval transitions with evidence. |
| MESP-124 / MESP-7 | PD-027/032/043/044; Procurement BRD; ADR-002/005/006/011. | Purchase Order and manually recorded Supplier Confirmation. | PO/confirmation API/DB/UI, partial/change/reapproval lineage, tax/currency/terms references, audit. | G-SEC/G-AUD/G-LOC/G-DATA; Procurement/Finance/Inventory validation. | **Preview:** PO and confirmation/partial journey. **Full R1:** complete auditable supplier response and downstream receiving handoff. |
| MESP-125 / MESP-7 | PD-028/040/046; Procurement, Inventory, Finance BRDs; ADR-002/005/006/011. | Procurement documents plus Inventory receipt evidence and Finance source-to-GL contract. | Goods Receipt/Purchase Invoice API/DB/UI, partials, holds, tax references, source lineage, audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; accounting/stock specialist validation before posting. | **Preview:** safe receiving-to-invoice contract slice. **Full R1:** no PO/receipt/invoice ambiguity, balanced handoff, and correction evidence. |
| MESP-126 / MESP-7 | PD-028/030/046; Procurement/Finance/Inventory BRDs; ADR-002/005/006/011. | PO, receipt, invoice source documents; Finance owns posting eligibility. | Matching API/DB/UI, tolerance states, hold/exception resolution, SoD, audit/report evidence. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance tolerance and posting validation; no inferred tolerance values. | **Preview:** deterministic match/hold journey. **Full R1:** approved tolerance and exception evidence with no silent auto-posting. |
| MESP-127 / MESP-7 | PD-039/046; Procurement, Inventory, Finance, Tax/VAT BRDs; ADR-002/005/006/011. | Supplier Return lineage, Inventory movement, Finance reversal, internal Tax/VAT evidence. | Return/correction/attachment API/DB/UI, reversal lineage, audit/reporting. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; stock/accounting/Tax validation. | **Preview:** linked return/correction journey. **Full R1:** auditable stock and liability consequence without external refund/submission. |
| MESP-128 / MESP-8 | PD-025/029/038/041/045/046; Inventory BRD, MESP-113, Finance valuation; ADR-002/005/006/011. | Inventory ledger, balance/availability/reservation, Product tracking configuration. | Ledger/balance/reservation/tracking API/DB/UI, opening evidence, audit/reporting. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Inventory/Finance validation; negative stock blocked by default. | **Preview:** real ledger/availability slice. **Full R1:** explainable tracking, reservation, opening, valuation, and correction behavior. |
| MESP-129 / MESP-8 | PD-025/045; MESP-113; Procurement/Sales handoffs; ADR-002/005/006/011. | Inventory movement ledger and warehouse transfer ownership. | Receipt/transfer/In Transit/return API/DB/UI, partial/shortage/cancellation, audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; stock and valuation validation; no silent balance changes. | **Preview:** controlled receipt/transfer path. **Full R1:** complete movement/return lineage with reconciliation. |
| MESP-130 / MESP-8 | PD-029/045; MESP-113; Inventory BRD; ADR-002/005/006/011. | Inventory ledger and count/issue evidence. | Adjustment/count/Stock Issue API/DB/UI, cutoff/recount/variance/reason/authority, audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Inventory/Finance/Audit validation before irreversible correction. | **Preview:** count/issue controlled slice. **Full R1:** no overwrite or free-form issue; all variance and correction evidence reconciles. |
| MESP-131 / MESP-8 | PD-025/029/043/045/046; Finance valuation contract; ADR-002/005/006/011. | Inventory movement ledger and Finance-owned valuation handoff. | MWA cost history/reconciliation/report API/DB/UI, correction/reversal evidence. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance/Inventory/Reporting specialist validation. | **Preview:** deterministic local valuation evidence. **Full R1:** repeatable MWA, currency, returns, corrections, and reconciliation. |
| MESP-132 / MESP-10 | PD-026/032/044/046; Finance BRD, MESP-110; ADR-002/005/006/011. | Finance chart/period/journal source of truth. | COA/fiscal/period/journal/dimension API/DB/UI, balanced posting, source-to-GL, audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance validation mandatory before irreversible posting/close. | **Preview:** balanced journal foundation. **Full R1:** controlled periods, dimensions, source lineage, corrections, and reconciliation. |
| MESP-133 / MESP-10 | PD-031/044/046; Finance BRD; Procurement/Sales; ADR-002/005/006/011. | AP/AR subledgers, manual payment/receipt catalogue, cash/bank records. | Payment/receipt/allocation/settlement API/DB/UI, due dates, reconciliation, audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance/SoD validation; no gateway/bank feed. | **Preview:** internal manual settlement journey. **Full R1:** AP/AR/cash/bank allocation and balanced reconciliation. |
| MESP-134 / MESP-10 | PD-037/039/040/043/046; docs/32; Finance/Tax/Reporting BRDs; ADR-002/005/006/011. | Finance tax/FX/posting records with applied source evidence. | Tax posting, transaction/Reporting Currency, FX/revaluation API/DB/UI, audit/reporting. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance/Reporting/Tax specialist validation. | **Preview:** internal tax/FX evidence. **Full R1:** balanced tax/FX/revaluation and historical reproducibility without external/statutory behavior. |
| MESP-135 / MESP-10 | PD-044/046; Finance source-of-truth and Reporting catalogue; ADR-002/005/006/011. | Finance periods, journals, subledgers and reconciliation controls. | Year-end/reversal/correction/report API/DB/UI, trial balance, retained-earnings behavior as validated. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance/Reporting/Migration validation before irreversible close. | **Preview:** controlled period/correction evidence. **Full R1:** repeatable close, reversals, reconciliation, and core reports. |
| MESP-136 / MESP-9 | PD-026/032/034/038/030; Sales/Finance/Master Data BRDs; ADR-002/005/011. | B2B Sales documents, Price List, Finance AR/credit source. | Quotation/Order/pricing/approval/credit API/DB/UI, holds, audit, bilingual UX. | G-SEC/G-AUD/G-LOC/G-DATA; Finance credit and Sales validation; no Retail POS. | **Preview:** B2B quote/order journey. **Full R1:** deterministic price, approval, credit, and AR-source evidence. |
| MESP-137 / MESP-9 | PD-038/040; Inventory availability/reservation; Finance invoice eligibility; ADR-002/005/006/011. | Sales Order/Delivery and Inventory reservation/ledger. | Reservation/partial fulfillment/Delivery/Invoice API/DB/UI, tax/currency/terms snapshot, audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Inventory/Finance validation; server-side invoice eligibility. | **Preview:** controlled partial delivery/invoice path. **Full R1:** no unauthorized delivery/invoice and traceable remainder. |
| MESP-138 / MESP-9 | PD-039/040/046; Sales/Inventory/Finance/Tax BRDs; ADR-002/005/006/011. | Delivery/Invoice lineage, Inventory return, Finance Credit Note/reversal. | Return/Credit Note/receipt allocation/correction API/DB/UI, attachments, audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; stock/accounting/Tax validation; no external refund. | **Preview:** linked internal return/credit journey. **Full R1:** complete B2B correction/refund-status/reconciliation evidence. |
| MESP-139 / MESP-11 | PD-042/043/044/046; Reporting BRD, Finance/Inventory source contracts; ADR-005/010/011. | Owning source modules; Reporting owns catalogue, lineage, formulas, and reconciliation view. | Report/API/DB/query/export/UI, filters, EN/AR, authorized export, conditional scheduling. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Reporting/Finance/Inventory validation; MESP-48/50 before production distribution. | **Preview:** authorized core reports with honest freshness. **Full R1:** full approved catalogue, lineage, bilingual output, reconciliation, and governed distribution. |
| MESP-140 / MESP-13 | MESP-38; ADR-004/005/009/010/011/018; C gates. | Domain business evidence remains with owning modules; cross-cutting controls own shared seams. | Files/export/notifications/support/localization/observability API/DB/UI only at approved local boundaries. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; privacy/retention/provider/infrastructure gates remain explicit. | **Preview:** visible safe shared states. **Full R1:** cross-cutting controls integrated with every affected capability; no provider claim without gate. |
| MESP-141 / MESP-15 | PD-041; MESP-40 BRD must be activated/ready; all source BRDs and MESP-48/50/SQL gates. | Versioned import batches, quarantine/reconciliation evidence, owning opening ledgers. | Onboarding/migration API/DB/UI, dry run, resumability/idempotency, cutover/rollback/audit. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; Finance/Inventory/Migration/SQL/backup/restore validation before destructive action. | **Preview:** after safe contracts exist, no production cutover claim. **Full R1:** repeatable Tenant onboarding with reconciled rollback-safe opening data. |
| MESP-142 / MESP-1 | All completed capability contracts, C1-C9, Opus checkpoints A/B/C, UAT/release gates. | Executable real codebase and verified cross-module evidence. | End-to-end API/DB/UI, regression/performance/UAT/release checklist; no fake demo branch. | G-SEC/G-AUD/G-LOC/G-DATA/G-PROD; full affected suites, SQL/provider, volume, privacy/legal, deployment and Opus checkpoint evidence. | **Preview:** truthful running integrated preview with completed/pending/blocked/gated map. **Full R1:** complete Definition of Done and production/RC evidence; unfinished work is never reclassified as complete. |

## 4. Historical MESP-117 fresh-session handoff

The following handoff is preserved as approved historical evidence from the
MESP-116 governance session. The current executable handoff is the MESP-132
acceptance overlay at the top of this document.

MESP-117 is the first capability handoff, not an activation performed by
MESP-116. The next fresh session must:

1. verify the Jira issue remains To Do/not activated and no other capability is
   active;
2. read docs/16, docs/17, docs/18, docs/19, docs/20, ADR-002, ADR-005,
   ADR-011, the existing Master Data/Business Parties contracts, and the
   current source/diff;
3. preserve the bounded Category/UOM/Product/Supplier/Customer decisions and
   consume only server-derived Tenant authority;
4. implement the complete shared Angular UX plus only the missing API,
   contract, persistence, or integration seams necessary for those existing
   slices; include EN/AR, RTL/LTR, accessibility, audit, concurrency,
   authorization, denial, and unknown/error states;
5. validate focused backend/frontend behavior, authorization/Tenant isolation,
   audit/concurrency, localization/accessibility, and the applicable release
   build before opening a focused PR; and
6. stop after the MESP-117 review/closure handoff and write the next exact task.

MESP-117 must not start Currency, Tax/VAT, Pricing, Procurement, Inventory,
Finance, Sales, Reporting, Migration, MESP-39, providers, credentials,
infrastructure, Retail POS, or Wafra-specific core behavior. One person, one
active capability, one focused branch/PR remains mandatory.

## 5. Governance completion and unchanged boundaries

MESP-116 is Done at its bounded governance/reconciliation scope. No source,
tests, persistence, schema, migrations, APIs, UI, providers, credentials,
infrastructure, production configuration, or production capability changed.
The production-capability percentages therefore remain unchanged. MESP-23
remains In Progress; MESP-39 is future-release and unexecuted; MESP-40 is
required but unactivated; internal Tax/VAT is Release 1 required/Not Started
without statutory scope; Retail POS and Wafra-specific core behavior remain
excluded.
