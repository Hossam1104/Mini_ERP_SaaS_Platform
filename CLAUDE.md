@AGENTS.md

## Executor authorization policy

`.ai/AI_EXECUTION_POLICY.md` is authoritative for STOP/Ready/merge/post-report
authorization boundaries. Read it before any Ready transition, merge, Jira
write, or mutation not explicitly authorized by the current bounded task.

## Reading order (read these, in this order, before acting)

1. `AGENTS.md` — durable repository working agreement, the single active AI
   model routing baseline, and the short current-overlay pointer.
2. `.ai/AI_EXECUTION_POLICY.md` — authoritative executor authorization:
   STOP semantics, positive authorization, Ready/merge authority, post-report
   immutability, external reviews as evidence only, Ponytail never authority.
3. `.ai/CURRENT_STATE.md` — the **CURRENT AUTHORITY** block at the top of that
   file is the single source of live project position (branch, head, active
   capability, open PR, HOLD state, gates, completion).
4. `TASK.md` — the exact bounded session currently handed off.
5. The owning BRD/spec, affected ADRs, and only materially dependent decision
   rows. Do not reread every BRD/PRD routinely.

Live Jira and live GitHub outrank every Markdown file for mutable facts.

## Current position (pointer only — see `.ai/CURRENT_STATE.md`)

* Canonical identity is **`Mini_ERP_SaaS_Platform`**; Jira key remains `MESP`.
* **MESP-138 (customer returns, credit notes, receipts) is the single active
  implementation capability**, In Progress under Epic MESP-9, published as
  **Draft PR #86 (Open/Draft/Unmerged)** and held by **GPT-5.6 Sol HOLD 3**
  (blockers HOLD-138-J through HOLD-138-N). It is **not accepted**.
* **MESP-139 is To Do / not activated.** Do not start it. Do not mark MESP-138
  Done. Do not transition, merge, or request review without positive
  current-task authority.
* Accepted fast-track completion is **21/26 = 80.8%**, excluding MESP-138.
  Production readiness is separately ~**47%** overall / ~**41%** Procurement/P2P.
  **MESP-48** and **MESP-50** remain open production gates.
* There is **no CI** in this repository. Report CI as `NONE / NOT CLAIMED`;
  local runs are not CI.

## Permanent boundaries

* Release 1 is **B2B ERP only**. Retail POS is excluded. Wafra is a validation
  tenant only — no Wafra-specific schema, workflow, permission, pricing,
  approval, accounting, or lifecycle behavior.
* No statutory/ZATCA/FATOORA implementation or readiness claim; no external
  production integrations, providers, or credentials.
* One active capability, one executor, one focused branch/PR, exact one-session
  `TASK.md` handoffs. Never start the next task automatically.
* Stop for a real Tenant-isolation, authentication/authorization,
  accounting/data-integrity, destructive-migration/data-loss,
  unresolved-business-decision, legal/external-validation,
  credential/production-infrastructure, or material scope/architecture blocker.

Every `## Historical execution overlay ...` and `## Historical approval
overlay ...` heading below is preserved, dated evidence. It is **not** current
authority even where its own text says "current".
## Owner-Managed Asset Protection

- Files manually supplied under `frontend/assets` are product source assets.
- Agents must never delete, rename, replace, regenerate, optimize, recolor, move, or restore them from Git without explicit Owner instruction.
- Untracked image files in `frontend/assets` must not be assumed temporary.
- Before asset cleanup, agents must distinguish Owner source assets from generated derivatives.
- Full logos/icons use `frontend/assets` as source of truth.
- `frontend/assets/brand` is reserved only for necessary generated browser derivatives (e.g., favicons, touch icons).


# HISTORICAL RECORD (preserved; not current authority)

## Historical execution overlay - 30 August 2026 (MESP-137 HOLD-2 remediation complete — awaiting independent Sol acceptance; superseded by closure above)

`MESP-137 HOLD-2 remediation complete — awaiting independent Sol acceptance`.
The authorized branch continued Draft PR #84 from exact HOLD-1 head
`4ecfb64ffd5391a934af80fe11003b9031881ae3` and exact `origin/main` baseline
`cb58d69fb0a589d363aed2d55b605e39f79d03bc`; pushed head is
`d1914884bead08fac7a5b4eddc6f573297ef5550`. HOLD-2 authority is MESP-137
comment `12275` with MESP-9 reconciliation `12277`.

HOLD-137-E made serializable Sales persistence authoritative for delivery
allocation, prior invoice evidence, quantity, exact net/tax/gross residuals,
tax identity, Payment Term, and source snapshot, and Finance posts only from
the persisted Sales invoice request. HOLD-137-F preserves real downstream
fingerprints, known Inventory Movement IDs and Finance effect IDs, explicit
commit/acknowledgement/reconciliation states, durable retry identity, and
fail-closed mismatch protection.

Validation at that time was 0 warnings/0 errors, full backend 1,138/1,138,
Angular 305/305, focused/full Chromium 2/2 and 49/49, and the retained
initial bundle was 512.18 kB with Sales lazy chunk 88.24 kB. The isolated
runtime was API `http://localhost:5310` PID `34956`, Angular
`http://localhost:4300` PID `42444`, data `.runtime/hold2-runtime-20260830`.
This snapshot preceded final Sol acceptance and merge; superseded by the
closure overlay above.

## Historical execution overlay - 29 August 2026 (MESP-137 initial implementation; superseded by HOLD-1 remediation)

MESP-132, MESP-133, MESP-134, MESP-135, and MESP-136 are Done and merged.
MESP-136 merged at `992195f7e61cf03b94675a498377a6d8bf679ebf` (2026-08-28T23:37:13Z).
Final Sol acceptance: MESP-136 comment `12252`. Closure: MESP-136 comment `12255`, MESP-9 reconciliation `12256`.

MESP-137 is the single active implementation capability under Epic MESP-9,
activated by MESP-137 comment \`12265\` and MESP-9 reconciliation \`12266\`.
The bounded implementation is on \`feat/MESP-137-reservation-fulfillment-invoice\`
from exact \`origin/main\` baseline
\`cb58d69fb0a589d363aed2d55b605e39f79d03bc\`. It covers Sales-linked
Warehouse-scoped reservation, partial allocation/backorder, atomic physical
Delivery handoff, and Finance-owned invoice eligibility/AR handoff.
Implementation commit is `86e9c5708daf33caa751e799ebbb8b02bfd717ed`; Draft PR #84 is Open/Draft/Unmerged.
MESP-138/139 remain To Do/inactive. No Jira writes, Ready transition, merge,
rebase, force-push, or review request is authorized; stop for independent
GPT-5.6 Sol review with the PR Open/Draft/Unmerged.
Fast-track = 20/26 = 76.9%. Production readiness remains ~47% overall / ~41% Procurement/P2P.
Ponytail FULL is the default executor mode when available.

## Historical execution overlay - 25 August 2026 (MESP-133 Sol HOLD 3 micro-remediation)

PR #77 is Open/Draft/Unmerged on `feat/MESP-133-ap-ar-cash-settlement` from
HOLD 3 starting head `452441084a44d1a8a0a1d8db3a0d679aac5ff550`, based on main
`9ace42c7a830b5ef155a26b18d4a888676b8c188`. MESP-132 is Done/merged/closed;
MESP-133 remains In Progress/activated under MESP-10 while Sol HOLD `11892` /
HOLD 2 `11926` / HOLD 3 `11963`, MESP-10 progress comment `11893` /
reconciliation `11927` / HOLD 3 `11964`, and manual-AR supplemental finding
`11928` remain authoritative. No Jira writes,
merge, Ready transition, next-capability activation, or Opus review occurred.

The focused source/test remediation commit is
`b9eba368922899165324086aa59298d054fec25d`; the final implementation commit
is `536cd40984d58c3f61ae814ac4efb0d48c6aa8d8`; the documentation/tracker
handoff SHA is recorded after push.

The remediation closes trusted AP payment-term/version and due-date handling,
reuses `IFinanceSourceApprovalPolicy`, enforces manual-only settlement
methods, binds cash/bank posting to linked GL mappings, implements actual
subledger/GL reconciliation and accounting-date as-of semantics, and enforces
AP/AR and Payment/Receipt route integrity with rejected-to-Draft correction.
Final validation is REST/OpenAPI/host `54/54`, full backend `1009/1009`, SQL
safety `61/61`, Angular `274/274` (focused settlement workspace `15/15`),
focused/full Chromium `6/6` and `38/38`,
Release `0/0`, initial `496.44 kB`, Finance/GL lazy `34.31 kB`, settlement
lazy `56.04 kB`, both audits clean; runtime PIDs `34964`/`34380` remain
running.
Fast-track remains `16/26 = 61.5%`; production readiness remains `~47%`
overall / `~41%` Procurement/P2P; MESP-134 is not activated.

## Historical execution overlay - 24 August 2026 (MESP-132 final Sol acceptance remediation)

The current merged capability is **MESP-132 Finance / General Ledger
foundation** under Epic MESP-10. PR #76 is squash-merged into `main` at
`ccc52a892c8258778f57c55c12fa0032bd3e276b` from accepted feature head
`c0e04553db3c7b04fa7f7870b60fc439ec8a40b7`; the feature branch is retained.
The bounded remediation started at `2f523582fbd3394b1eb11580eff490ba83aa9afb`
and its source/test implementation commit is `dcae7e2`. MESP-132 remains In
Progress / activated in Jira pending Sol closure and MESP-10 reconciliation,
while MESP-131 and MESP-8 are Done.

The public Manual Journal contract is now server-owned manual identity only;
manual edits cannot convert lineage or amount authority, and trusted Inventory
handoffs retain source evidence. Five provider-realistic SQL Server LocalDB
concurrency cases cover period close/post, account restriction/post, the same
Journal, the same Inventory source handoff, and first-company JournalSequence;
nested SQL contention maps to safe Finance conflict behavior.

Final validation is Finance `12/12`, REST/OpenAPI and host security `53/53`,
prior Inventory `89/89`, SQL safety `46/46`, full backend `982/982` with 0
failures and 0 skips, Release 0/0, Angular `259/259` across 37 spec files,
initial bundle `496.34 kB`, Finance lazy chunk `36.45 kB`, Chromium `2/2`
focused and `34/34` full, and both npm audits clean. Final merged-main runtime
is backend `http://localhost:5300` PID `21112` and frontend `http://localhost:4300` PID
`39640`, with the recorded HTTP 200 probes. Accepted fast-track completion
is now `16/26 = 61.5%`; production readiness remains approximately `47%`
overall and `41%` Procurement/P2P. Do not start the next capability
automatically.

## Historical execution overlay - 19 August 2026 (MESP-125 activated; FIN-OD-01 reconciled; MESP-124 merged)

MESP-124 is **complete, independently reviewed by Claude Opus 5 (APPROVE FOR
MERGE), and squash-merged to `main`** at commit
`c742d9c897edb715c7e3c25df7e9ca2c4f30d1e6` (merge timestamp 2026-08-18T21:37:47Z;
reviewed feature head `0eca12dbecffe7e8abeff6914566fa4de329d2c7`; PR #68
merged).

Its Tenant- and Company/Branch-scoped Purchase Order source selection, immutable
PR/quotation/source-decision and commercial snapshots, reusable approval/SoD/
delegation seams, issue/commit evidence, manual Supplier Confirmation (full,
partial, rejected, no-response), supplier-proposed changes with controlled
reapproval, exact confirmation remainder, lifetime Tenant-scoped uniqueness
consumption, exact durable idempotent replay, immutable history/audit, REST/
OpenAPI metadata, formal Procurement EF Core migrations, and bilingual EN/AR RTL
Angular workspace are merged to `main`.

No Goods Receipt, stock, warehouse movement, invoice, AP/accounting, payment,
three-way matching, supplier portal, external integration, ZATCA/FATOORA,
production DNS/TLS, or customer-specific (Wafra) core behavior was added.
Protected source assets under `frontend/assets` remain untouched. Zero Jira
operations were performed; GPT-5.6 Sol owns Jira management.

The active implementation capability is **MESP-125 (Goods Receipt and Purchase
Invoice handoff)** under Epic MESP-7. MESP-125 is **IN PROGRESS / ACTIVATED**
(Jira activation comment `11503`). FIN-OD-01 is **APPROVED CONTRACT-BOUND** under
MESP-116 (comment `10957`) and PD-046 (MESP-22 comment `10958`): Finance owns
balanced journals, source-to-GL mapping, account and period validation, subledger
reconciliation, Inventory valuation handoff, controlled corrections and
reversals, and auditable posting evidence; operational modules own source
documents and do not fabricate accounting entries outside the approved Finance
contract. Prerequisite gates MESP-41, MESP-43, MESP-44, MESP-45, MESP-113, and
MESP-116 are Done. Immediate implementation executor is Claude Sonnet 5
(Reasoning: HIGH) on branch `feat/MESP-125-goods-receipt-purchase-invoice-handoff`
per root `TASK.md`.

## Historical execution overlay - 17 August 2026 (MESP-124 implementation; superseded by merge)

The bounded MESP-143 implementation was active on branch
`feat/MESP-143-tenant-aware-entry` from synchronized `main`. It establishes
configuration-led host candidate routing with exact server-side Tenant
membership authority, common/platform entry boundaries, Overview-first shell
routing, post-Overview Company/Branch operational context, generic branding,
and presentation-only SAR semantics. No Tenant schema, DNS/TLS automation,
external provider, Jira write, or downstream Procurement capability is in scope.

The branch is validated for one Draft PR against `main`, remains unmerged, and
must not be force-pushed. Owner assets under `frontend/assets` are unchanged.
The next exact session is independent targeted Opus review of MESP-143 security,
UX, fallback, and regression evidence.

## Historical execution overlay - 17 August 2026 (MESP-123 Opus findings corrected; pre-Opus governance reconciliation; forward ADR-019/MESP-143 alignment)

The current bounded state is MESP-123 on branch
`feat/MESP-123-purchase-request-approval`, continuing Draft PR #66 against `main`.
Merge-blocking findings from the independent Opus review are resolved:
- F-1 non-ISO currency fallback in quotation workspace;
- F-2 source-decision concurrency passthrough (`If-Match` on first decision & reselection);
- F-5/F-6 documentation, test suite, and bundle reconciliation.

Permanent architecture rules from `@AGENTS.md` and
`docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md` govern:
- **Tenant != Workspace**: Tenant is server-authorized isolation boundary; operational context is inside Tenant and aligns with Company/Branch;
- **Entry Flow**: Candidate host resolution (`wafra.mesp.com` → candidate Tenant → auth & membership check → Tenant Overview);
- **Workspace UX**: Overview loads first; single context auto-selects; multiple contexts use header switcher;
- **Tenant Branding**: Wafra logo is configuration data under `frontend/assets` with MESP fallback; never hardcoded logic;
- **Saudi Riyal**: SAR symbol is a country-pack presentation asset under `frontend/assets`; presentation only with zero FX/tax/accounting effect.

**Execution Boundary:**
- Zero product code changes in this governance reconciliation session.
- MESP-143 is planned/unimplemented and must not be started in this session.
- The next exact session is **Claude Opus 5 targeted read-only re-verification of F-1, F-2, and F-5** per `TASK.md`.
- PR #66 remains OPEN, DRAFT, and UNMERGED. No Jira operations (GPT-5.6 Sol owns Jira).

## Historical execution overlay - 16 August 2026 (MESP-123 B2 post-Phase-C foundation; superseded)

The historical bounded session was MESP-123 B2 on branch
`feat/MESP-123-purchase-request-approval`, continuing Draft PR #66.
B2 delivered the canonical `/app/workspaces` shell route,
compatibility `/tenant/select`, server/configured human Tenant labels,
the explicit exact-Development loopback-only `MESP_DEV_AUTH_BYPASS=true`
server-actor shortcut, a bounded sidebar, and shared ERP UI primitives adopted
by Workspace/Tenant Selection and representative Purchase Request list/detail
screens.

## Historical execution overlay - 12 August 2026 (MESP-116 approved decision reconciliation; superseded)

At the time of this historical entry, the authoritative overlay was the
MESP-116 approved decision reconciliation in `AGENTS.md`. Release 1 remains the
full-feature reusable B2B ERP
and 31 August 2026 is the **Release 1 Integrated Preview** of the real
codebase, not an MVP, throwaway/demo UI, Wafra fork, or scope cut. Unfinished
capability remains Release 1 work after the preview.

Canonical artifacts are `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`,
`docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`, and
`docs/32_Release_1_Tax_VAT_Scope_Clarification.md`, and the approved
dependency map `docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`.
PD-024 records only the
explicit Owner directions for full scope/preview, essential cycles, sequential
one-person delivery, Luna execution, Opus checkpoints A/B/C, external
integration deferral, and restoration of internal reusable configuration-led
Tax/VAT without statutory/ZATCA/FATOORA/legal/external-provider scope.
MESP-116 approved A1-A16 and B1-B6 at their exact bounded positions and
appended PD-025 through PD-046. Class B is the Release 1 product/implementation
contract, subject to mandatory specialist validation before production or
irreversible accounting, migration, and cutover decisions. C1-C9 remain open
gates. Focused PR #59 was reviewed at
8b3f7b61c0128f97aa6a775dec23e623c1fde70e and merged to main at
b58bcaaeb4103c8fbdfb6a1c933c5239e228c5bd; post-merge state/tracker
synchronization is 66183c1; main and origin/main are synchronized.

MESP-38 is Done. MESP-115 is Done through focused PR #58 (reviewed head
0681c0182b0b6894f5f2b83db1728253ac54e279; merge a5ee9426d252901e74888bdc3ca94970c969aa20).
MESP-39 is To Do, unactivated, and not executed as future
release work. MESP-40 is To Do/unactivated but required for Release 1 in Wave H.
MESP-23 is In Progress; reconciliation evidence is Jira comment 10976.
MESP-117-MESP-142 are To Do/not activated capability tasks under existing
Epics. MESP-117 is the approved first capability handoff, with detailed
handoff evidence in Jira comment 10977 and docs/33. One active capability, one
executor, one focused branch/PR, Angular included, and exact one-session TASK
handoffs remain mandatory. The next session is MESP-117; it must not activate
MESP-39 or MESP-40 or expand the approved boundaries.

## Historical execution overlay - 12 August 2026 (MESP-38 BRD complete; superseded by MESP-115 rebaseline)

MESP-38 - Security, Audit, and Data Governance BRD is **Done** at its
approved bounded documentation-only scope. The canonical artifact is
docs/29_Security_Audit_and_Data_Governance_BRD.md. Focused PR #57 merged to
main at 67b7fb79475fb194489bc03ed153c999d20a6eaf from reviewed head
42f2a1cb7b15580a6a92c4603253b6ea5104c203. Jira evidence is activation 10934,
validation 10935, Owner approval 10936, MESP-23 handoff 10937, final audit
10938, closure 10939, and the Done transition metadata.

MESP-27 through MESP-38 are Done at their approved bounded BRD scopes.
MESP-23 remains In Progress as the living Open Questions Register. MESP-39 -
Integrations and External Services BRD - is the single next BRD task; it is
To Do, has not been activated, and must not be executed by this handoff.
MESP-40 and later tasks remain unactivated. MESP-48, MESP-50, MESP-53,
MESP-54, MESP-110, and MESP-113 remain open and unapproved at their existing
boundaries.

MESP-38 was documentation-only and adds no production capability. Release 1
remains Saudi-localized Core ERP B2B only. Retail POS, Wafra-specific core
behavior, statutory/tax/ZATCA/FATOORA implementation, external production
integrations, and privacy/legal certification remain excluded or separately
gated. PRD visual rendering was attempted but unavailable because pdf2image
and LibreOffice/soffice are not installed; no visual claim is made.

The root TASK.md contains the exact next MESP-39 session prompt, and
.ai/CURRENT_STATE.md contains the current verified detailed state. No next
task starts automatically.

## Historical execution overlay - 12 August 2026 (Pre-MESP-38 reconciliation handoff)

The verified live sequence is MESP-27 through MESP-37 **Done** at their
approved bounded BRD scopes. MESP-23 remains **In Progress** as the living
Open Questions Register. MESP-38 - Security, Audit, and Data Governance BRD is
the single next BRD task; it is **To Do**, has not been activated, and must not
be executed by this reconciliation. MESP-48, MESP-50, MESP-53, MESP-54, and
MESP-110 remain open and unapproved at their existing boundaries.

The root `TASK.md` contains the exact next MESP-38 session prompt, and
`.ai/CURRENT_STATE.md` contains the current verified detailed state. MESP-38
is documentation-only and adds no production capability. No next task starts
automatically.

Release 1 remains Saudi-localized Core ERP B2B only. Retail POS,
Wafra-specific core behavior, statutory/tax/ZATCA/FATOORA implementation,
external production integrations, and privacy/legal certification remain
excluded or separately gated.

Hossam's standing Owner approval continues for normal bounded BRD,
specification, readiness, merge, closure, and next-session activation within
approved scope and architecture. Stop only for a real security/Tenant-
isolation, accounting/data-integrity, destructive migration/data-loss,
unresolved-business-decision, legal/external-validation,
credential/production-infrastructure, or material scope/architecture blocker.
One fresh chat executes exactly one root `TASK.md` session, updates genuinely
affected state and Jira, and stops for review; never start the next task
automatically.

## Historical execution overlay - 9 August 2026 (MESP-101 readiness complete; preserved)

MESP-100 is Done with closure evidence `10663`. MESP-99 / M95-SL-02 is Done
through PR #33, correction PR #34, and final audit-semantics correction PR #35,
with final correction merge `3e51f98f8c80b9989632499632605894c18570cf`.
MESP-101 is **Done** for the bounded M95-SL-03 Product identity readiness
gate after PR #36 merged to `main` at
`c7392a55e0b60fd83e48447e3f9218f82cfaccea`; closure evidence is Jira comment
`10672` and activation/owner evidence is comment `10671`.
The Product-only bounds are MD-OD-001, MD-OD-003, MD-OD-005, MD-OD-008,
MD-OD-010, and MD-OD-011 as recorded in `docs/18_Product_Identity_M95_SL_03_Readiness.md`.
The completed readiness session adds no Product source behavior, persistence, migration,
endpoint, or UI. The root `TASK.md` is being handed off to the exact next
Product identity implementation session.
The actual backend consists of `MiniErp.Api`, `MiniErp.App`,
`MiniErp.Contracts`, and `MiniErp.Infrastructure`; detailed project/module
enforcement is in ADR-002 and shared SQL Server/Tenant/module persistence
controls remain governed by ADR-006. MESP-48, MESP-49, and MESP-50 remain open.

## Historical execution overlay - 8 August 2026 (preserved)

MESP-31 is **Done**. PR #29 merged at actual commit
`93f4e83992ef46f498cfbfacbb513cfc3d8dda7d` from approved head
`c465d660e49a254f2fffbb95e0d07c5fcf17a193`. MESP-95 is **Done** with Jira
closure evidence `10654`; ChatGPT passed the final review and M95-R01,
M95-R02, and M95-R03 are closed. MESP-96 is the single next executable item,
**In Progress** in Jira, and is limited to M95-SL-01. This overlay supersedes
older live-state claims below.

M95-SL-01 is contract-only and non-persistent. Do not create Master Data EF
entities/tables, migrations, or an `MESP` database/access solely for this
slice. Do not decide Product/Item, SKU/Barcode, tracking, business
availability, approval catalogue, or Draft/Active behavior. Retail POS,
Wafra-specific core behavior, and M95-SL-02 remain out of scope. No Master
Data persistence exists yet.

Hossam has standing Owner approval for normal BRD/specification/readiness,
merge/closure, and next-session activation within approved scope and
architecture; stop only for a real security, Tenant-isolation,
accounting/data-integrity, destructive migration/data-loss,
legal/external-validation, credential/infrastructure, unresolved-business-
decision, or material scope/architecture blocker. One fresh Codex/Luna chat
executes exactly one root `TASK.md` session, validates, updates all genuinely
affected state, updates Jira, commits/pushes, merges only when clean, then
STOPs for ChatGPT review. Never automatically execute the next task. Run an
independent Opus review after every five sessions or earlier at a critical
architecture/security/financial/data-model checkpoint.
# Mini ERP Delivery Rules

## Historical approval overlay — 8 August 2026 (preserved)

MESP-31 BRD v0.3 is an **Approved Business Baseline** and is now **Done** in
live Jira. Hossam's approval is recorded in comment `10649`; final closure
evidence is in comment `10650`. PR #28 is merged at actual merge commit
`1dc4d2092d6e9a5bf8f6cfc3347e552a5ddbad1b`, with final PR head
`8396197b54189cb550f07bd4bb6779fd38ac30cb` confirmed as an ancestor of
`main`. The Open Decision Register MD-OD-001 through MD-OD-011 remains
preserved; approval resolves none of those decisions, and blocking decisions
remain implementation-slice gates. MESP-95 is the single active Jira item,
In Progress, on branch `docs/MESP-95-master-data-lean-implementation-spec`.
Its Draft implementation-readiness specification is documentation-only; no
Master Data source implementation has started, no Jira child slice is active,
and no next coding item may start automatically. The review is published as
PR #29 (Open, non-draft) from initial draft head
`dc550e1171e8f9d20cd7fdf5509dfffb7537b3bd`; it must not be merged or followed
by source implementation automatically.

Authoritative sources are the approved PRD, owning BRDs, `docs/Decisions.md`,
approved ADRs, the active Lean Implementation Specification, Jira, and
`docs/94_Product_Delivery_Master_Plan.md` in that order. Read the active Jira
item and relevant approved documents before acting.

Keep Release 1 B2B-only; exclude Retail POS and Wafra-specific core behavior.
Use one implementation item at a time, review the full diff, run focused tests,
and never auto-start the next Jira item. MESP-48 and MESP-50 remain explicit
gates. Stop for Tenant leakage, auth weakness, data loss/purge, accounting
integrity, or an unresolved legal/privacy decision.

MESP-94 is Done: PR #26 merged to `main` at the actual merge commit
`06d837c958c1cb7977dc121e3aaea4e7278944fd` (approved final head
`2c7ed3dec4662672bb78967ceb70db7ed73eb7d4`, correcting Foundation
safety-catalogue classifications and validation-evidence accuracy) after a
ChatGPT final merge review verdict of APPROVED FOR MERGE; it used normal
bounded review, not the MESP-92/MESP-93 manual security merge hold. MESP-93
is Done: PR #24 merged to `main` at
`005c796629341ab9becfbc6d1abe2ae34b6a7332` (reviewed head
`83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`) after focused ChatGPT security
re-review approval. PR #25 (docs) merged to `main` at
`9f333c9734c767673e43a30d6b57c05793e1fb69`. MESP-92 carried the manual-hold
exception earlier in the sequence; PR #22 merged at
`322341e70e56270797d5770b4b90342c20b7833e` after focused ChatGPT approval and
MESP-92 is Done, as are MESP-89, MESP-63, MESP-61 and MESP-64.

A Foundation completion checkpoint following MESP-94 confirmed MESP-92,
MESP-93 and MESP-94 Done, and MESP-48/MESP-50 as intentional open production
gates that do not block MESP-31 BRD entry. PR #27 merged that closure to
`main` at `c86ecb851e88205f1d3907f5a5c36cfb59ce8b54`, the current merged-main
baseline.

MESP-31 is **In Progress** under Parent Epic
`MESP-6 — EPIC 06 - Master Data and Product Catalog`, on branch
`docs/MESP-31-master-data-product-catalog-brd`. Both Owner authorizations are
recorded in live Jira: comment `10615` (BRD entry) and comment `10616`
(future Master Data implementation, conditional). MESP-31's Jira Source
Baseline is primary anchor PLT-003 with supporting anchors PLT-002, SAL-001,
PROC-002, PROC-008, FIN-001, FIN-003, FIN-007, FIN-010, KSA-002, BR-013,
ADM-003 and the applicable PRD RULE set for master-data integrity; PLT-011
through PLT-014 and BR-004 are Platform Administration anchors and are not
MESP-31's baseline.

`docs/16_Master_Data_and_Product_Catalog_BRD.md` (Products, Product
Categories, Units of Measure, Suppliers, Business Customers, Price Lists,
Taxes, Payment Terms, Currencies, Exchange Rates) is published on open
**PR #28**. Its v0.1 head `6d0aa80` drew a business-requirements verdict of
CHANGES REQUIRED BEFORE OWNER APPROVAL / MERGE; a bounded correction round
produced v0.2 (ten Open Decisions, MD-OD-001–010), reviewed at head
`8657011`, which drew a further verdict of CHANGES REQUIRED — FINAL SMALL
CORRECTION ROUND: M31-R10 (Product/Item modelling needed an Owner decision
rather than Confirmed status), M31-R11 (residual approval assumptions),
M31-R12 (Saudi launch language) and M31-R13 (an unrelated
`.vscode/settings.json` change in the PR delta). A second bounded correction
round closed all four and produced v0.3, whose Open Decision register now
holds eleven decisions (MD-OD-001–011, adding Product/Item modelling as
MD-OD-011). The BRD is **v0.3 Approved Business Baseline**, approved by
Hossam in Jira comment `10649` at reviewed content head
`1e2d055354f0ddde833190948d09fa426707484c`. The Open Decision Register
remains preserved; approval silently resolves none of MD-OD-001 through
MD-OD-011. Two Founder Decision Pack defaults remain explicitly **not**
approved and must not be treated as requirements: MESP-41
(batch/lot/serial/expiry — now MD-OD-010) and MESP-54 (exchange-rate sourcing
and Finance approval — owned by MESP-34). Hossam also authorized the later
Master Data implementation phase, subject to the normal Definition of Ready
and the dedicated MESP-95 readiness item. No Master Data implementation has
begun, and none may start automatically — see `.ai/CURRENT_STATE.md` for the
exact live position.

The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`; older filenames name
the same unchanged file. Start from `.ai/CURRENT_STATE.md` for the verified
branch, head, active item, open Pull Request and open findings.
