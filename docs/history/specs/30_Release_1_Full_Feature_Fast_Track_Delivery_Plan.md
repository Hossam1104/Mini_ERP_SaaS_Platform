# Release 1 Full-Feature Fast-Track Delivery Plan

## Current execution reconciliation - 30 August 2026

MESP-137 is Done, independently accepted, and squash-merged through PR #84
from accepted feature head `9406e8c6408251323b96d4a0c25082142546b9ef` at
`6b3aeb63da15253dee5466f7be001773b80c28ad`. PR #85 carries the post-closure
documentation reconciliation to `main` at
`4d6e33189a3835d5d8d2a58736055a837a3f5bc9`. MESP-144 reconciliation reached
Sol content acceptance at comment `12293` on reviewed head
`ffe5a8975611dcc85c3a7c40dce0b3737b123aeb`. HOLD 5 merge-safety authority is
comment `12296`; at that executor handoff, Jira and PR lifecycle had not yet
been finalized, with Jira and GitHub authoritative for their respective final
states.

No feature implementation capability is active. MESP-138 through MESP-142
remain To Do/inactive; MESP-9 remains In Progress; and MESP-48/MESP-50 remain
open production gates. Accepted fast-track completion is `21/26 = 80.8%`;
production readiness remains approximately `47%` overall / `41%`
Procurement/P2P. The older execution overlays below are historical records.

## Historical execution overlay - 25 August 2026

HOLD 4 adds no production code. Its implementation/test commit is
`7cf177e8eaf694824a91b8b5b0cf3642d0f049f7`, from exact starting SHA
`30ea4a04e5fb120a292083edc03073e37b278b11`. Final evidence is full backend
`1014/1014`, focused Finance `16/16`, SQL safety `61/61`, REST/OpenAPI/host
`54/54`, Angular `274/274` including focused settlement workspace `15/15`,
focused/full Chromium `6/6` and `38/38`, Release
`0 warnings/0 errors`, and 0-vulnerability audits. Sol HOLD 2 `11926`, Finance
Epic reconciliation `11927`, HOLD 3 `11963` / `11964`, HOLD 4 `11967` /
`11968`, and manual-AR supplemental finding `11928` remain the authority; PR
#77 stays Open/Draft/Unmerged and no Jira write or merge was performed. The
real Procurement source-provider cases and historical recognition rule A→B
regression are now present and passed; the existing allocation-vs-settlement-
reversal race remains retained.

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

Fast-track completion after the verified MESP-132 merge is `16/26 = 61.5%`; this is capability completion, not production readiness. Overall production-readiness remains approximately `47%` and Procurement/P2P `41%`.
<!-- MESP-132-EXECUTION-END -->

**Status:** Current planning baseline; A1-A16 and B1-B6 approved in MESP-116, C1-C9 remain open gates
**Date:** 12 August 2026
**Milestone:** **31 August 2026 — Release 1 Integrated Preview**
**Governance task:** MESP-115 (Done), MESP-116 (Done at bounded governance scope)
**MESP-144 reconciliation record:** The repository health, architecture,
documentation, and backlog reconciliation reached Sol content acceptance at
comment `12293`; HOLD 5 merge-safety cleanup is recorded under comment
`12296`. Final PR/Jira lifecycle is checked live; no Ready transition, merge,
Jira transition, or next capability starts automatically.

## 1. Purpose and authority

This is the canonical delivery plan for the Owner-directed Release 1
full-feature fast-track rebaseline. It converts the fast-track brief into a
single sequential execution order, a capability backlog, an honest preview
definition, and a bounded governance model.

MESP-116 has now approved A1-A16 and B1-B6 only at the exact bounded positions
in the Consolidated Owner Decision Pack. The append-only decisions are PD-025
through PD-046 in MESP-22 comment `10958`, based on Owner evidence in MESP-116
comment `10957`. Class B is a Release 1 product/implementation contract, not
production approval: Finance, Inventory, Reporting, Migration, Security/Audit,
SQL/provider, and named specialist validation remains mandatory before
production or irreversible accounting/cutover decisions. C1-C9 remain open
production/external/legal/provider/infrastructure gates.

The plan continues to record the explicit directions already given in the
Owner brief through PD-024:

- Release 1 remains a reusable, full-feature B2B ERP. The August milestone is
  an integrated preview of the real codebase, not an MVP, throwaway demo,
  Wafra fork, or scope cut.
- The essential business cycles remain required even when unfinished at the
  preview. Pending work is still Release 1 work and is not silently moved
  out of scope.
- Delivery is sequential: one person, one active capability, one focused
  branch/PR at a time. Luna is the primary executor; ChatGPT/Sol plan and
  review; Opus is reserved for the named checkpoints and genuine critical
  risks.
- External production integrations remain outside Release 1. MESP-39 remains
  To Do, unactivated, and future-release work.
- Internal, reusable, configuration-led Tax/VAT capability is restored to
  Release 1. This is not a statutory, ZATCA/FATOORA, certification,
  submission, clearance, or legal-compliance claim.

PD-023 and PD-024 remain immutable. The detailed Tax/VAT boundary is in
`docs/32_Release_1_Tax_VAT_Scope_Clarification.md`; the approved decision
records and row-level clarifications are in
`docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`; and the final
cross-module dependency map is
`docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`.

## 2. Current verified baseline

| Item | Current position |
|---|---|
| MESP-132-MESP-137 | Completed bounded capabilities at their accepted merged scopes. |
| MESP-137 | Done; PR #84 accepted feature head `9406e8c6408251323b96d4a0c25082142546b9ef` merged to `main` at `6b3aeb63da15253dee5466f7be001773b80c28ad`. |
| MESP-138-MESP-142 | Capability backlog remains To Do / not activated. |
| MESP-144 | Sol content acceptance recorded at comment `12293`; HOLD 5 merge-safety authority is comment `12296`; final Jira/PR lifecycle is authoritative in the live systems. |
| Current acceptance handoff | PR #85 documentation reconciliation is merged to `main` at `4d6e33189a3835d5d8d2a58736055a837a3f5bc9`; at the HOLD 5 executor handoff, PR #82 was verified Draft/Open/Unmerged. GitHub remains authoritative for its final lifecycle. |
| Production capability | No percentage increase is justified by this governance/documentation work. |

## 2a. Historical verified baseline retained

| Item | Historical position |
|---|---|
| MESP-38 | Done at its approved bounded Security, Audit, and Data Governance BRD scope. |
| MESP-39 | To Do, not activated, not executed; future-release Integrations and External Services BRD. |
| MESP-40 | To Do, not activated; full Release 1 migration and Tenant Onboarding requirement, scheduled in Wave H. |
| MESP-23 | In Progress as the living Open Questions Register; reconciled by Jira comment `10976`; C gates remain open. |
| MESP-115 | Done at the bounded documentation/Jira/governance rebaseline. |
| MESP-116 | Done at the bounded approval/unblock reconciliation; Jira comment `10957`, closure transition, final dependency map, PR #59 reviewed at `8b3f7b61c0128f97aa6a775dec23e623c1fde70e`, merge `b58bcaaeb4103c8fbdfb6a1c933c5239e228c5bd`, and post-merge synchronization `66183c1` are recorded. |
| MESP-117–MESP-131 | Completed bounded capabilities at their accepted merged scopes. |
| MESP-132–MESP-137 | Completed bounded capabilities at their accepted merged scopes. |
| MESP-137 | Done; PR #84 accepted feature head `9406e8c6408251323b96d4a0c25082142546b9ef` merged to `main` at `6b3aeb63da15253dee5466f7be001773b80c28ad`. |
| MESP-134–MESP-142 | Capability backlog remains To Do / not activated. |
| Product implementation | Existing bounded Category, UOM, Product, Supplier, and Customer slices remain evidence; the planned capability completion and shared Angular experience are not claimed complete. |
| Current acceptance handoff | MESP-132 accepted feature head `c0e04553db3c7b04fa7f7870b60fc439ec8a40b7`; PR #76 squash SHA `ccc52a892c8258778f57c55c12fa0032bd3e276b`; Sol Jira closure and MESP-10 reconciliation are the next governance actions. |
| Production capability | No percentage increase is justified by this governance/Jira work. |

This plan deliberately does not rewrite approved historical BRDs. Current
scope corrections are carried by this overlay, PD-024, and the Tax/VAT
clarification so that historical evidence remains auditable.

## 3. Release 1 scope is full-feature, not a reduced preview scope

Every row below is required Release 1 capability. “Preview priority” describes
what should be integrated early when safe; it does not remove the lower rows
from the Release 1 commitment.

| Area | Full Release 1 capability boundary |
|---|---|
| Platform and SaaS | Reusable multi-Tenant lifecycle and isolation; Tenant-owned Company/Legal Entity, Branch, Warehouse, subscription and entitlement controls; users, membership, roles, permissions, support access, audit, configuration, bilingual behavior, private files, authorized exports, and notifications. |
| Master Data | Category, UOM, Product, Supplier, Business Customer, Currency, Payment Terms, internal Tax/VAT, Exchange Rates, Price Lists, controlled import, audit/report integration, downstream references, deterministic Tenant-safe identity and lifecycle behavior. |
| Procurement | Purchase Request, quotation, approvals, Purchase Order, Supplier Confirmation including partial/change flows, Goods Receipt, Purchase Invoice handoff, three-way match, tolerances, exceptions, Supplier Return, reversal/correction, attachments, audit/reporting, multi-currency, internal tax, authorization and SoD. |
| Inventory | Opening Balance, Goods Receipt, Warehouse Transfer, In Transit, Adjustment, Count snapshots/cutoff/recount, Supplier and Customer Return, Stock Issue, reservation and availability, ledger/balances, valuation, Moving Weighted Average, tracking, negative-stock policy, correction, audit, reconciliation, reporting, permissions and SoD. |
| Sales | Quote, Sales Order, approval, pricing, credit control, reservation, partial fulfillment, Delivery, Invoice, Receipt and allocation, Customer Return, Credit Note, cancel/reversal, attachments, audit/reporting, multi-currency, internal tax, Payment Terms, authorization and SoD. |
| Finance | COA, journals, balanced posting, source-to-GL, AP, AR, supplier payments, customer receipts, allocation and settlement, cash/bank, posting and accounting periods, reopen/reclose, fiscal/year-end, retained earnings, reversals, Cost Center and posting dimensions, due dates and aging, internal tax, transaction currency, Reporting Currency, FX/revaluation, rounding, reconciliation, trial balance, subledgers, audit and SoD. |
| Reporting | Finance, AR, AP, trial balance, stock, valuation, purchasing, sales, reconciliation, audit, security and operational catalogue; filters, bilingual views, export, lineage, freshness, authorization, and scheduled distribution only where the final approved Release 1 decision allows it. |
| Localization | Arabic/English, RTL/LTR, bilingual documents/reports/exports, localized numbers/dates/currency, and generic Saudi-oriented configuration through a reusable country-pack model. This is not statutory compliance. |
| Security and governance | MESP-38 approved Security, Audit, and Data Governance requirements consumed by implementation; Tenant isolation, server-derived authorization, audit evidence, support controls, private-file boundaries, and production gates remain mandatory. |
| Migration and onboarding | MESP-40 full Release 1 configuration, master data, opening stock/valuation, GL/AP/AR, cash/bank, validation, quarantine, dry-run, reconciliation, cutover, rollback, and repeatable Tenant onboarding. |

The following remain outside Release 1 production: ZATCA/FATOORA connectivity or
submission, government submission, payment gateways, bank feeds, automated
external FX, external SSO, partner webhooks, external providers, credentials,
production infrastructure, and any statutory, taxpayer-applicability,
certification, signing, clearance, or legal/privacy certification claim.

Retail POS and Wafra-specific core behavior remain excluded. Wafra is a
validation reference only and must never become a hard-coded product branch.

## 4. Integrated Preview definition — 31 August 2026

The milestone is **Release 1 Integrated Preview**, meaning a running build of
the real repository codebase with the maximum safely integrated functionality
that the sequential executor can complete and validate by that date. It is
not a promise that every full-feature capability is production-ready by the
milestone, and it is not permission to represent a stub, fake UI, or isolated
demo as complete ERP behavior.

The preview must show, to the extent actually implemented and validated:

1. the real Tenant-safe application topology and reusable Angular experience;
2. visible, bilingual-ready master-data behavior connected to real contracts;
3. as much of the Procurement-to-Inventory spine as is safely integrated;
4. the Finance and Sales dependencies that are genuinely wired, with
   incomplete areas labelled honestly;
5. audit, authorization, localization, error, and reconciliation behavior
   where the affected capability requires them; and
6. a truthful list of pending, blocked, gated, and after-preview capability.

No preview acceptance may waive accounting balance, stock integrity,
Tenant-isolation, authorization, audit, destructive-migration, or external
production gates.

## 5. Sequential operating model

### 5.1 One active capability

Only one implementation capability may be active at a time. A capability task
includes its Definition of Ready, implementation, backend/API/DB and Angular
surfaces, authorization/audit/localization, affected tests, validation, PR,
merge, Jira closure, and the next exact handoff. Routine readiness tickets are
not created merely to make the board look active.

The next capability starts only after the previous one has a clean review,
validated merge, and updated state. A fresh chat executes exactly the root
`TASK.md` session and stops at its boundary.

### 5.2 Roles and review quota

- **Luna:** primary sequential executor.
- **ChatGPT/Sol:** planning, repository/Jira reconciliation, code/document
  review, and final bounded-session review.
- **Opus checkpoint A:** after Procurement and Inventory have a coherent
  integrated spine.
- **Opus checkpoint B:** after Finance and Sales have a coherent integrated
  spine.
- **Opus checkpoint C:** before serious RC/production review.
- **Early Opus use:** only for a genuine Tenant/security, accounting, stock,
  destructive migration, or major cross-module risk.

The Opus quota is not spent on routine capability work. A checkpoint may hold
the sequence for a real critical finding; a ceremonial review does not create
an implementation dependency.

### 5.3 Reading and context discipline

Read in this order for each capability: `AGENTS.md`, `CLAUDE.md`,
`.ai/CURRENT_STATE.md`, root `TASK.md`, the owning BRD/specification, affected
ADRs/contracts/entities, current `git status`/diff, and only the decision rows
that materially affect the capability. Do not reread every BRD or the entire
PRD when the owning contract and state already answer the question. Reopen
the PRD and broad architecture only at a real cross-module or release gate.

## 6. Capability waves and Jira backlog

The backlog uses existing Epics and does not duplicate the existing
foundation/platform work. MESP-57–MESP-64 and the existing MESP-65–MESP-85
platform stories remain their original work. MESP-117–MESP-142 are the
full-feature capability tasks below.

*Historical wording (12 August 2026, preserved for traceability): "MESP-117–
MESP-142 are the new full-feature capability tasks; all remain To Do/not
activated. MESP-117 is the prepared first handoff after the completed
MESP-116 reconciliation and starts only in a fresh session after its
Definition of Ready is verified." This described the state at BRD-entry time
and is superseded by current status below.*

Current status (30 August 2026): MESP-117 through MESP-137 have completed at
their accepted bounded scopes and are Done/merged. MESP-138 through MESP-142
remain To Do/inactive. MESP-144 is recorded as the repository-health
reconciliation checkpoint; it is documentation/metadata-only and adds no
production capability. No feature implementation capability is currently
active, and no later capability starts automatically — each is activated only
by explicit Sol/Owner authorization in a fresh session after its Definition of
Ready is verified.

Every task description contains the common Definition of Done: Tenant-safe
server-derived context; permission and SoD checks; business audit; private
file/export controls where applicable; Arabic/English and RTL/LTR-ready
Angular patterns; backend/API/DB ownership; affected tests; validation;
reconciliation and error behavior; preview/full Release 1 acceptance; and
honest production gates. No task description authorizes external providers or
credentials.

| Wave | Jira capability | Existing Epic | Scope, source truth, dependencies, and delivery surfaces |
|---|---|---|---|
| A — Master Data | MESP-117 | MESP-6 | Complete shared Angular UX for existing Category, UOM, Product, Supplier, and Customer slices; consume their bounded contracts and current Product/Supplier/Customer decisions; include reusable list/detail/forms, bilingual labels, permissions, audit and downstream references across API, persistence, and UI. |
| A — Master Data | MESP-118 | MESP-6 | Currency and Payment Terms capability; depend on Finance/Reporting decisions where unresolved; own master-data/API/DB/UI contract, deterministic effective behavior, bilingual UX, audit and authorization. |
| A — Master Data | MESP-119 | MESP-6 | Internal configuration-led Tax/VAT master and engine contract; consume docs/32 and PD-024; support tax identities/categories/codes, effective versions, applicability, taxable base, applied-rate evidence, accounting/reporting handoff, returns/credits, and UI/API/DB/audit without statutory integration. |
| A — Master Data | MESP-120 | MESP-6 | Exchange Rate and multi-currency master capability; consume MESP-54 approved contract; support manual/configured rates, transaction and Reporting Currency, effective history, FX evidence, permissions, audit, and UI/API/DB boundaries without an external provider. |
| A — Master Data | MESP-121 | MESP-6 | Price List and deterministic B2B pricing; consume MD-OD-004/SAL-OD-01 approval; implement precedence, effective dates, currency/tax interaction, authorization, audit, and reusable Angular/API/DB behavior. |
| A — Master Data | MESP-122 | MESP-6 | Controlled import, audit/report integration, and downstream references for Master Data; depend on the approved import/migration and reporting contracts; include validation/quarantine-safe behavior, permissions, audit, bilingual error UX, and repeatability. |
| B — Procurement | MESP-123 | MESP-7 | Purchase Request, quotation, and reusable approval; consume MESP-42/MESP-55; implement document lifecycle, approval policy, SoD, attachments, audit, bilingual Angular, API/DB, and rejection/expiry behavior. |
| B — Procurement | MESP-124 | MESP-7 | Purchase Order and Supplier Confirmation including partial changes; consume MESP-43; implement confirmation/rejection/change/reapproval, authorization, audit, tax/currency/terms references, Angular and API/DB. |
| B — Procurement | MESP-125 | MESP-7 | Goods Receipt and Purchase Invoice handoff; consume Inventory receiving and Finance source-to-GL contracts; implement quantities, partial receipt, exception/error behavior, tax/currency references, audit, permissions, and UI/API/DB. |
| B — Procurement | MESP-126 | MESP-7 | Three-way matching, tolerances, and authorized exception resolution; consume MESP-44 and Finance posting decisions; implement deterministic match evidence, configurable approved tolerances, hold/override authority, SoD, audit, reporting, Angular/API/DB. |
| B — Procurement | MESP-127 | MESP-7 | Supplier Return, correction, attachments, audit, and procurement reporting; consume Inventory return and Finance reversal contracts; implement full correction/reversal lineage, permissions, bilingual UI, API/DB, and reporting references. |
| C — Inventory | MESP-128 | MESP-8 | Inventory ledger, opening balance, availability, reservation, and tracking; consume MESP-41, MESP-45, MESP-113, MESP-51 and Finance valuation; implement ledger/balances, reservation authority, tracking, negative-stock boundary, audit, reports, API/DB and Angular. |
| C — Inventory | MESP-129 | MESP-8 | Goods Receipt, Warehouse Transfer, In Transit, and returns; consume INV-OD-004/MESP-113 and Procurement/Sales handoffs; implement movement integrity, partials, returns, audit, authorization, bilingual UI, API/DB. |
| C — Inventory | MESP-130 | MESP-8 | Stock Adjustment, Inventory Count, Stock Issue, and corrections; consume MESP-113/MESP-45; implement snapshot/cutoff/recount/variance, reason and authority, audit, SoD, reconciliation, API/DB/UI. |
| C — Inventory | MESP-131 | MESP-8 | Moving Weighted Average valuation, reconciliation, and inventory reporting; consume Finance valuation and MESP-54 currency contracts; implement deterministic cost history, correction, reconciliation, reports, audit, API/DB and Angular. |
| D — Finance | MESP-132 | MESP-10 | COA, fiscal calendar, periods, journals, posting rules, and dimensions; consume MESP-110/FIN-OD decisions; implement balanced journals, source-to-GL, period controls, Cost Center/posting dimensions, authorization, audit, UI/API/DB. |
| D — Finance | MESP-133 | MESP-10 | AP, AR, supplier payments, customer receipts, cash, bank, allocation, and settlement; consume MESP-47 and payment/receipt contract; implement internal manual methods, due dates/terms, settlement/allocation, reconciliation, SoD, audit, API/DB/UI. |
| D — Finance | MESP-134 | MESP-10 | Tax accounting, transaction currency, FX, Reporting Currency, and revaluation; consume docs/32, MESP-54, and approved Finance decisions; implement internal tax postings, historical rates, realized/unrealized FX, revaluation, rounding, audit, reporting, API/DB/UI. |
| D — Finance | MESP-135 | MESP-10 | Year-end close, reversals, corrections, reconciliation, and core Finance reports; consume MESP-110, Finance source-of-truth rules, and Reporting catalogue; implement fiscal/year-end, retained earnings, reversals, trial balance/subledgers, permissions, audit, UI/API/DB. |
| E — Sales | MESP-136 | MESP-9 | B2B quotations, Sales Orders, pricing, approvals, and credit control; consume MD-OD-004, MESP-42/MESP-46/MESP-55 and Finance AR; implement price precedence, credit exposure/holds, approvals, SoD, audit, bilingual Angular, API/DB. |
| E — Sales | MESP-137 | MESP-9 | Reservation, partial fulfillment, Delivery, and Sales Invoice; consume Inventory reservation/availability and Finance invoice eligibility; implement partials/backorder behavior, tax/currency/terms references, authorization, audit, API/DB/UI. |
| E — Sales | MESP-138 | MESP-9 | Customer Return, Credit Note, receipts/allocation, and Sales correction; consume MESP-46 and Finance reversal/tax contracts; implement return/credit lineage, allocation, reversal, permissions, audit, bilingual UI, API/DB. |
| F — Reporting | MESP-139 | MESP-11 | Approved Release 1 reporting catalogue, lineage, export, and distribution; consume MESP-53/RPT-OD rows; implement source ownership, formulas/freshness, filters, bilingual views, authorized export, scheduling only if approved, audit, API/DB/UI. |
| G — Cross-cutting | MESP-140 | MESP-13 | Close Security, Audit, Files, Notifications, Localization, and Support gaps; consume MESP-38 and approved ADRs; implement only bounded local capability, with Tenant isolation, authorization, private files, audit, bilingual/RTL, and production-gate evidence. |
| H — Migration | MESP-141 | MESP-15 | Release 1 migration and repeatable Tenant onboarding; execute only after MESP-40 is activated and ready; cover configuration, master/openings, stock/valuation, GL/AP/AR, cash/bank, dry-run, quarantine, reconciliation, cutover, rollback, and audit. |
| I — E2E/RC | MESP-142 | MESP-1 | Integrated stabilization, regression, performance, UAT, and RC readiness; follows coherent module waves and Opus checkpoints; no production claim until SQL/provider/infrastructure/privacy/legal/volume gates are evidenced. |

MESP-116 selected MESP-117 as the first implementation handoff after approved
decisions and published the final dependency map in
`docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`. The
backlog order is the recommended sequence, not an automatic activation
command. MESP-117 has since completed at its accepted bounded scope (see
Section 2); MESP-138 through MESP-142 remain To Do/not activated, each
requiring explicit Sol/Owner activation in a fresh session.

## 7. Definition of Ready for each capability

The capability may become the single active task only when:

- the applicable BRD, implementation specification, ADR, and approved
  decision rows are identified;
- Tenant ownership, server-derived context, authorization, SoD, audit, and
  module ownership are explicit;
- persistence and API contracts do not cross a module ownership boundary
  without an approved contract;
- required upstream contracts and test data are available or safely stubbed
  only at a documented contract boundary;
- unresolved decisions are either approved, explicitly deferred without
  blocking the bounded contract, or escalated as a real blocker;
- the Angular surface, bilingual labels, RTL/LTR behavior, accessibility,
  error states, and authorized export/file behavior are included where
  relevant; and
- the acceptance scenarios can be validated without external credentials,
  providers, statutory submission, or production infrastructure.

## 8. Definition of Done and validation

Each capability is complete only when its implementation and evidence meet all
applicable items below:

1. backend, contracts, persistence, API, and Angular behavior are integrated
   in the real repository;
2. Tenant isolation and server-derived authorization are tested, with no
   client-supplied Tenant/Company/Branch/Warehouse authority;
3. permission, SoD, audit, correction/reversal, idempotency, and error paths
   are covered where the business capability requires them;
4. accounting and stock invariants are validated before the next dependent
   capability starts;
5. Arabic/English, RTL/LTR, localized numbers/dates/currency, and bilingual
   documents/reports/exports are covered where applicable;
6. affected backend tests, frontend tests/build, and repository validation
   pass; full non-SQL/module validation is run at cross-module boundaries;
7. SQL/database validation is reported honestly, including any unavailable
   local SQL tooling; no green claim is manufactured;
8. the full diff is reviewed for source-scope allowlist, secrets, provider,
   credentials, infrastructure, and accidental Wafra/POS behavior;
9. the focused PR is reviewed, merged only when clean, and the Jira issue,
   `docs/staticts.md`, `.ai/CURRENT_STATE.md`, and root `TASK.md` are updated;
10. the next task is written exactly and the session stops.

## 9. Gates and dependency policy

The following are preserved as real gates, not hidden inside a percentage:

- Tenant isolation, authentication/authorization, support access, business
  audit, private files, and MESP-38 Security/Audit/Governance requirements;
- accounting balance, source-to-GL, AP/AR, period, tax-posting, FX, and
  reconciliation integrity;
- stock ledger, availability, reservation, tracking, valuation, count,
  negative-stock, correction, and return integrity;
- destructive migration/data loss, repeatability, quarantine, rollback, and
  cutover safety;
- MESP-48 supported-volume and production governance;
- MESP-50 retention, privacy, legal hold, purge, residency,
  backup/restoration, and production governance;
- SQL Server, provider, credentials, infrastructure, deployment, external
  service, and legal/external validation boundaries; and
- any unresolved business decision that changes ownership or accounting/data
  integrity.

Safe local capability work is not blocked merely because a later production
gate is open. A capability must stop when it would have to invent an external
contract, legal answer, production credential, data-loss behavior, or
cross-module accounting/stock rule.

## 10. Forecast through 31 August 2026

This is a planning forecast, not an impossible promise. “Optimistic” means
unusually smooth sequential execution with decisions available; “realistic”
assumes ordinary corrections and review; “minimum credible” is the smallest
truthful outcome that still shows a real integrated preview. All tiers keep
unfinished full-feature work in Release 1.

| Date/window | Optimistic | Realistic | Minimum credible |
|---|---|---|---|
| 12 Aug | Finish MESP-115 and MESP-116 governance with focused evidence. | MESP-115/MESP-116 documentation/Jira synchronization and review complete. | Canonical scope/decision/Tax artifacts, PD-025-PD-046, and exact MESP-117 handoff. |
| 13 Aug | MESP-117 Definition of Ready and capability activation. | MESP-117 starts only after contract/source/validation review. | MESP-117 remains unactivated until a fresh session verifies readiness. |
| 14 Aug | MESP-117 shared Angular/master-data work underway with affected tests. | MESP-117 starts after Definition of Ready and focused branch/PR setup. | No unsafe activation; approved decisions and scope remain synchronized. |
| 15–16 Aug | MESP-117 shared Angular/master-data work underway with affected tests. | MESP-117 starts after Definition of Ready and contract review. | No unsafe activation; approved decisions and scope remain synchronized. |
| 17–18 Aug | MESP-117 completed and MESP-118/119 begins in sequence. | Shared Master Data surface and one bounded capability integrated. | A real codebase preview path is prepared; incomplete work is labelled. |
| 19–20 Aug | MESP-118/119 plus initial Procurement contract work integrated. | Master Data foundation and first Procurement path visibly running. | One validated capability with no fake downstream behavior. |
| 21–22 Aug | Procurement and Inventory spine begins with real ledger/receiving contracts. | Procurement-to-Inventory handoff and validation are the main focus. | Preview remains honest about unimplemented Finance/Sales behavior. |
| 23–24 Aug | Inventory movement/reservation/valuation slice and Finance posting foundation. | One coherent cross-module spine, with remaining decisions/gates listed. | Running real application with the strongest safe completed slice. |
| 25–26 Aug | Finance/Sales contract work and bilingual/reporting surfaces broaden. | Finance or Sales foundation follows dependency order; no parallel implementation. | Current completed capabilities remain demonstrable without scope claims. |
| 27–28 Aug | Integrated preview hardening, cross-module tests, and early Opus checkpoint if the named spine is coherent. | Integration, correction, frontend build, and audit/authorization verification. | Reproducible build and truthful pending/blocked/gated register. |
| 29 Aug | Preview candidate with maximum safely completed real Release 1 code. | Preview candidate after affected tests and review; unfinished waves remain scheduled. | No release-candidate claim; only verified preview evidence. |
| 30 Aug | Preview rehearsal, defect correction, and review. | Rehearsal and evidence review with production gates still visible. | Stable statement of what is and is not implemented. |
| 31 Aug | **Integrated Preview:** running real codebase showing the broadest safely integrated capability achieved. | **Integrated Preview:** running real codebase with coherent completed slices and a precise remaining-work map. | **Integrated Preview:** a truthful, buildable real codebase and documented full-feature continuation plan; no fake UI, no MVP reclassification, and no external integration claim. |

After 31 August, Waves A–I continue sequentially until the full Release 1
Definition of Done is met. The preview date does not become a deadline for
unsafe shortcuts.

## 11. Progress and reporting rules

Planning, Jira creation, Product Decisions, and documentation do not increase
production-capability percentages. `docs/staticts.md` must update Jira counts,
milestone, forecast, blockers, Tax/VAT classification, and the progress-history
row while preserving the current conservative production percentages unless
validated code capability changed.

The completion report for this bounded session must state: MESP-38 Done,
MESP-39 not executed and future-release, MESP-40 still a Release 1
requirement, MESP-23 still In Progress, no recommendation silently approved,
PD-024 appended only for explicit directions, internal Tax/VAT restored without
statutory scope, all capability tasks To Do/not activated, no source or
production capability changed, and the exact PR/review/merge evidence.
