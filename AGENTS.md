# Repository Working Agreement

## Executor Authorization Policy

`.ai/AI_EXECUTION_POLICY.md` is authoritative for AI executor authorization
boundaries: explicit STOP semantics, positive (never inferred) authorization,
Ready-for-review and merge requiring current-task authority, post-report
immutability, external/bot reviews as evidence only, Ponytail never being
authority, and the Owner-action-vs-executor-action evidence standard. Every
AI executor (Claude Code, OpenAI Codex, or any other agent) must read and
follow it before Ready, merge, Jira, or any other mutation beyond what the
current bounded task explicitly authorizes.

## Project Statistics Tracker

`docs/staticts.md` is the tracked living source of truth for production
progress. At the end of every bounded session that materially changes project
progress, implementation state, Jira counts, phase completion, blockers,
velocity, or forecast:

1. Read the current tracked `docs/staticts.md`.
2. Update it directly in the repository using conservative, validated
   production capability rather than ticket count.
3. Update `Last Updated` and `Progress History` when materially applicable.
4. Commit and push the tracker update with the session.
5. Do not generate or attach a separate copy for Hossam/ChatGPT.
6. Completion reports state only changed headline percentages and confirm
   that the repository tracker was updated; Hossam/ChatGPT inspects the
   tracked GitHub version directly when needed.

## Owner-Managed Asset Protection

- Files manually supplied under `frontend/assets` are product source assets.
- Agents must never delete, rename, replace, regenerate, optimize, recolor, move, or restore them from Git without explicit Owner instruction.
- Untracked image files in `frontend/assets` must not be assumed temporary.
- Before asset cleanup, agents must distinguish Owner source assets from generated derivatives.
- Full logos/icons use `frontend/assets` as source of truth.
- `frontend/assets/brand` is reserved only for necessary generated browser derivatives (e.g., favicons, touch icons).

## Current execution overlay - 4 September 2026 (MESP-138 active under Sol HOLD 3)

> **Single source of current state.** `.ai/CURRENT_STATE.md` carries the
> **CURRENT AUTHORITY** block and is the one place to read live project
> position. This overlay is a short pointer, deliberately kept small.
> Every `## Historical execution overlay ...` heading below is preserved,
> dated evidence — it is **not** current authority, even where its own text
> says "current". Live Jira and live GitHub outrank this file for mutable
> facts.

* Canonical identity is **`Mini_ERP_SaaS_Platform`**. Authoritative SCM is GitHub `Hossam1104/Mini_ERP_SaaS_Platform` (repository ID `1318676370`); authoritative tracker is Jira project `Mini_ERP_SaaS_Platform`, key `MESP`. Azure DevOps is neither.
* Accepted `main` is `644e7b364006a3a62dc8e9756b9a9a64afbd33e1`.
* **MESP-138 is the single active implementation capability**, In Progress under Epic MESP-9, on `feat/MESP-138-customer-return-credit-receipts` at head `8480deef9b0fa1225dfc83c0eb59bc768b1a2960`, published as **Draft PR #86 (Open/Draft/Unmerged)**.
* MESP-138 is under **GPT-5.6 Sol HOLD 3** (MESP-138 comment `12349`) covering blockers **HOLD-138-J** through **HOLD-138-N**. It is **not accepted**; it must not be marked Ready, merged, or counted as completed capability.
* **MESP-139 remains To Do / `not-activated`** and must not be started until Sol explicitly activates it. MESP-144 is Done and was governance/documentation only.
* Accepted fast-track capability completion is **21/26 = 80.8%**, excluding MESP-138. Production readiness remains separately approximately **47% overall** and **41% Procurement/P2P**. **MESP-48** and **MESP-50** remain open production gates.
* There is **no GitHub Actions CI** in this repository; report CI as `NONE / NOT CLAIMED`. Local test runs are not CI.
* `frontend/assets` remains Owner-managed and untouched. Ponytail FULL remains the default executor mode when installed and available, and is never authority.

## AI model governance and routing baseline

This is the Owner's active model routing baseline for `Mini_ERP_SaaS_Platform`.
`.ai/AI_EXECUTION_POLICY.md` remains authoritative for *authorization* —
STOP boundaries, Ready, merge, and mutation limits — and this section does not
widen any executor's authority. Historical records of earlier model usage are
preserved as written and are not rewritten because routing changed later.

| Priority | Model | Role | Typical use |
| --- | --- | --- | --- |
| 1 | **GPT-5.6 Sol** | Planner / Architect / **Acceptance authority** | Requirements, planning, architecture, diagnosis, acceptance, model routing, reconciliation, final go/no-go |
| 2 | **GPT-5.6 Luna xHigh** | Default substantial implementation executor | Large features, cross-cutting work, backend/frontend, multi-file implementation, substantial corrections |
| 3 | **Claude Sonnet 5** | Bounded implementation / fixes / tests | One bug, one service, one endpoint, bounded SQL, unit/integration tests, test automation, focused corrections |
| 4 | **GPT-5.6 Terra HIGH** | Precision finalizer / recovery / surgical executor | Nearly finished work, high-risk final correction, repository/process recovery, difficult bounded finalization |
| 5 | **Claude Opus 5** | Rare independent critical reviewer / exceptional checkpoint executor | Deep independent review, architecture checkpoint, high-risk validation, release-risk checkpoint |
| Aux | **Gemini** | Low-risk bounded/mechanical work | Repetitive documentation, mappings, formatting, low-risk mechanical operations |

Opus is **not** the default project executor. Its use is reserved and
exceptional; an Opus checkpoint does not make Opus the standing executor for
subsequent implementation work.

## Historical execution overlay - 30 August 2026 (MESP-137 HOLD-2 remediation complete — awaiting independent Sol acceptance; superseded by closure above)

* HOLD-2 was complete on `feat/MESP-137-reservation-fulfillment-invoice`, continuing Draft PR #84 from exact HOLD-1 head `4ecfb64ffd5391a934af80fe11003b9031881ae3` and exact `origin/main` baseline `cb58d69fb0a589d363aed2d55b605e39f79d03bc`. HOLD-2 authority is MESP-137 comment `12275` with MESP-9 reconciliation `12277`; pushed head is `d1914884bead08fac7a5b4eddc6f573297ef5550`.
* HOLD-137-E made serializable Sales invoice preparation authoritative for current delivery allocation, prior evidence, quantity, net/tax/gross residuals, tax identity, Payment Term, and source snapshot; Finance posts from persisted Sales evidence. HOLD-137-F persists real downstream fingerprints, known Inventory Movement IDs and Finance effect IDs, explicit committed/not-acknowledged/reconciliation-required semantics, durable retry identity, and fail-closed mismatch checks.
* Validation was Release `0 warnings / 0 errors`; full disposable-LocalDB backend `1,138/1,138`; SQL safety `80/80`; focused Inventory ledger/stock-control/valuation `34/34`, `12/12`, `44/44`; focused Sales `38/38`; Finance MESP-135 `31/31`; REST `36/36`; catalogue `4/4`; host security `19/19`; identity/authorization `89/89`; Angular `305/305`; focused/full Chromium `2/2`, `49/49`; both npm audits `0 vulnerabilities`; five-project NuGet scan clear; all seven EF contexts have no pending model changes; and `git diff --check` clean. Initial bundle was `512.18 kB` (`12.18 kB` over budget), Sales lazy chunk `88.24 kB`; warning retained.
* The isolated runtime was API `http://localhost:5310` PID `34956`, Angular `http://localhost:4300` PID `42444`, data `.runtime/hold2-runtime-20260830`. This snapshot preceded final Sol acceptance and merge; superseded by the closure overlay above.

## Historical execution overlay - 29 August 2026 (MESP-137 initial implementation; superseded by HOLD-1 remediation)

* MESP-132, MESP-133, MESP-134, MESP-135, and MESP-136 are Done and merged. MESP-136 final Sol acceptance is recorded as MESP-136 comment `12252`; closure evidence is MESP-136 comment `12255` and MESP-9 reconciliation `12256`. PR #80 is closed/merged with squash commit `992195f7e61cf03b94675a498377a6d8bf679ebf` at 2026-08-28T23:37:13Z. Accepted feature head was `507bd1b11b933fd81d734e5cd12cad4c858dffb4`.
* MESP-137 is the single active implementation capability under Epic MESP-9, activated by MESP-137 comment `12265` and MESP-9 reconciliation `12266`, on `feat/MESP-137-reservation-fulfillment-invoice` from exact `origin/main` baseline `cb58d69fb0a589d363aed2d55b605e39f79d03bc`.
* Implementation commit is `86e9c5708daf33caa751e799ebbb8b02bfd717ed`; Draft PR #84 is Open/Draft/Unmerged.
* Fast-track capability completion = **20/26 = 76.9%** after verified merge and Jira closure. Production readiness remains separate and unchanged at approximately **47% overall** and **41% Procurement/P2P**. MESP-48 and MESP-50 remain open production gates.
* MESP-9 remains In Progress pending Sol next-capability activation and Epic reconciliation.
* MESP-137 is bounded to Sales-linked Inventory reservations, partial allocation/fulfillment, atomic Sales delivery stock posting and reservation consumption, Finance-owned invoice eligibility and AR/invoice handoff seams, durable evidence, REST/OpenAPI metadata, and bilingual RTL Angular fulfillment UI. Returns, credit notes, receipts, refunds, revenue recognition, external integrations, ZATCA/FATOORA, Wafra-specific behavior, MESP-138, and MESP-139 are excluded. `frontend/assets` remains untouched and the presentation remains main-only.
* One Draft PR is authorized and must remain Open/Draft/Unmerged. No Jira writes, Ready transition, merge, rebase, force-push, or review request is permitted; stop after push and PR verification for independent GPT-5.6 Sol review.
* Ponytail FULL is the default executor mode when installed and available.

## Historical execution overlay - 29 August 2026 (MESP-136 HOLD 3 active; stop for third Sol acceptance)

* MESP-132, MESP-133, MESP-134, and MESP-135 are Done and merged. MESP-136 remains the single active implementation capability under Epic MESP-9 - B2B Sales and Order-to-Cash, In Progress/activated under Sol evidence MESP-136 comment `12234` and MESP-9 reconciliation `12235`.
* HOLD 3 authority is MESP-136 comment `12248` with MESP-9 reconciliation `12249`. The bounded remediation is on `feat/MESP-136-b2b-quote-order-credit` from exact Sol-reviewed starting head `29bd9bc1b65ef3b451e282091e25aa041796d9d3` and exact main baseline `8bab7e36ca7fe4da0d8e62e7d7a4b9d7bcd59f6c`; the final documentation/tracker handoff SHA is recorded by `git rev-parse HEAD` after push.
* HOLD 3 corrects mixed-currency credit integrity: `FinanceCustomerExposureRecord.CurrencyCode` is the common evaluation currency; same-currency orders use raw totals without fabricated FX; foreign orders use immutable validated Sales transaction-to-functional evidence with repository 8-decimal ToEven rounding; limits resolve in evaluation currency; durable credit/history evidence retains both currency meanings and conversion facts; and missing/mismatched/invalid truth fails closed as `Unknown`. Override reuse is currency-aware and HOLD 1/HOLD 2 invalidation remains intact.
* The minimal additive schema change is Sales-only: migration `20260828224412_MESP136SalesHold3CurrencyIntegrity` adds evaluation/transaction/converted/FX/revision credit evidence and makes the legacy credit currency nullable for safe migration. No Finance schema, external provider, or later MESP capability changed.
* Validation: Release `0 warnings / 0 errors`; focused Sales `26/26`; full disposable-LocalDB backend `1,124/1,124` with 0 failures and 0 skips; SQL safety `80/80`; REST/OpenAPI/host/security `55/55`; generated OpenAPI `410` unique operationIds; catalog `411` public / `2` internal; Angular `305/305` across 43 spec files; focused Sales Chromium `2/2`; full Chromium `49/49`; EF pending-model check clean; both npm audits `0 vulnerabilities`; NuGet vulnerable-package scan clear; and final `git diff --check` clean. Production initial bundle is `510.08 kB`, exceeding the 500 kB budget by `10.08 kB`; Sales lazy chunk is `72.72 kB`. The warning is retained and the budget was not increased.
* The repository-owned runtime is left running through the canonical launcher with loopback Development auth bypass: backend PID `7608` on port `5300`, frontend PID `10148` on port `4300`. It uses isolated `.runtime/hold3-runtime-20260829` because the prior local SQLite schema was stale; the persistent MESP store was not touched. Authenticated backend and frontend health/OpenAPI/Finance/Sales/order-edit probes returned HTTP 200; `LEFT RUNNING = YES`.
* Fast-track capability completion remains `19/26 = 73.1%` until third Sol acceptance, verified merge, and Jira closure. Production readiness remains separate and unchanged at approximately `47%` overall and `41%` Procurement/P2P. MESP-48 and MESP-50 remain open production gates.
* MESP-137, MESP-138, and MESP-139 remain To Do/inactive. No Wafra-specific behavior was added; the presentation remains main-only and `frontend/assets` remains untouched. Draft PR #80 is Open/Draft/Unmerged. No Jira writes, Opus review, Ready transition, merge, rebase, or force-push is permitted; stop for third independent GPT-5.6 Sol acceptance.

## Historical execution overlay - 28 August 2026 (MESP-136 HOLD 2 active; stop for third Sol acceptance)

* MESP-132, MESP-133, MESP-134, and MESP-135 are Done and merged. MESP-135's accepted feature head is `dbc239d6bd1ef948bb8505d4360208f4a3470dda`, with PR #79 closed at squash commit `8238ce562ee165def8ecdbfa07b285aeb3f1a2ef`; its final accounting acceptance is not reopened.
* MESP-136 is the single active implementation capability under Epic MESP-9 - B2B Sales and Order-to-Cash. Both are In Progress/activated under Sol evidence MESP-136 comment `12234` and MESP-9 reconciliation `12235`.
* HOLD 2 authority is MESP-136 comment `12244` with MESP-9 reconciliation `12245`. The bounded remediation is on `feat/MESP-136-b2b-quote-order-credit` from exact main baseline `8bab7e36ca7fe4da0d8e62e7d7a4b9d7bcd59f6c`; reviewed starting head was `36d1c04cb4537142bdb29d369d40e35ac3b76618`; source/test/UI commit is `397ef6d`; the final documentation/tracker handoff SHA is recorded by `git rev-parse HEAD` after push.
* HOLD 2A makes Draft/ReturnedForChange to PendingApproval resolve the current effective policy at submission, persist that exact snapshot, and use it consistently for pending stage/count/eligibility/SoD/cancellation and policy identity while retaining live, time-bounded delegation validation. HOLD 2B adds integrated SalesService credit coverage through `TransitionOrderAsync` and `OverrideCreditAsync` with a controllable Finance-owned exposure fixture, durable credit/history/audit assertions, and eligible/warning/limit/hold/unavailable/override/invalidation cases. The small Sales UI cleanup removes the duplicate Audit button and restores the Audit tab click handler.
* Validation: Release `0 warnings / 0 errors`; focused Sales `21/21`; full disposable-LocalDB backend `1,119/1,119` with 0 failures and 0 skips; SQL safety `80/80`; REST/OpenAPI/host/security `55/55`; generated OpenAPI `410` operationIds; catalog `411` public / `2` internal; Angular `305/305` across 43 spec files; focused Sales Chromium `2/2`; full Chromium `49/49`; EF pending-model check clean; both npm audits `0 vulnerabilities`; NuGet vulnerable-package scan clear; and final `git diff --check` clean. Production initial bundle is `510.08 kB`, exceeding the 500 kB budget by `10.08 kB`; Sales lazy chunk is `72.72 kB`. The warning is retained and the budget was not increased.
* The repository-owned runtime is left running through the canonical launcher with loopback Development auth bypass: backend PID `44876` on port `5300`, frontend PID `44908` on port `4300`. Required health, OpenAPI, frontend, Finance smoke, Sales landing/quotations/orders, and order-edit probes returned HTTP 200; `LEFT RUNNING = YES`.
* Fast-track capability completion remains `19/26 = 73.1%` until third Sol acceptance, verified merge, and Jira closure. Production readiness remains separate and unchanged at approximately `47%` overall and `41%` Procurement/P2P. MESP-48 and MESP-50 remain open production gates.
* MESP-137, MESP-138, and MESP-139 remain To Do/inactive. No Wafra-specific behavior was added; the presentation remains main-only and `frontend/assets` remains untouched. Draft PR #80 is Open/Draft/Unmerged. No Jira writes, Opus review, Ready transition, merge, rebase, or force-push is permitted; stop for third independent GPT-5.6 Sol acceptance.

## Historical execution overlay - 28 August 2026 (MESP-136 HOLD 1)

* MESP-132, MESP-133, MESP-134, and MESP-135 are Done and merged. MESP-135's accepted feature head is `dbc239d6bd1ef948bb8505d4360208f4a3470dda`, with PR #79 closed at squash commit `8238ce562ee165def8ecdbfa07b285aeb3f1a2ef`; its final accounting acceptance is not reopened.
* MESP-136 is the single active implementation capability under Epic MESP-9 — B2B Sales and Order-to-Cash. Both are In Progress/activated under Sol evidence MESP-136 comment `12234` and MESP-9 reconciliation `12235`.
* MESP-136 is implemented on `feat/MESP-136-b2b-quote-order-credit` from exact main baseline `8bab7e36ca7fe4da0d8e62e7d7a4b9d7bcd59f6c` (`docs: close MESP-135 and reconcile project state`). Sol HOLD 1 authority is MESP-136 comment `12239` with MESP-9 reconciliation `12240`; the bounded remediation source/test commit is `a140237d564ccd90f409404bb861b3b7c96380c2`; the final documentation/tracker handoff SHA is recorded by `git rev-parse HEAD` after push.
* HOLD 1 closes destination Company/Branch relocation by making quotation and order organizational scope immutable, adds durable configuration-led multi-stage approval state with delegation/SoD and revision binding, wires runtime Sales policy providers to `MESP_SALES_POLICIES`, and adds controlled Sales Order edit/revision and ReturnedForChange resubmission. The additive migration changes only Sales approval/revision/history evidence; no later Sales capability or fulfillment/accounting flow was added.
* The bounded capability covers reusable Tenant/Company/Branch-scoped B2B quotations and Sales Orders, server-authoritative Price List resolution, immutable commercial/tax/FX evidence, approval/delegation/SoD seams, Finance-exposed credit outcomes and controlled override, durable idempotency, revisions/history/audit, optimistic concurrency, and bilingual RTL Angular workspace surfaces.
* MESP-137, MESP-138, and MESP-139 remain To Do/inactive. No later Sales capability may be activated or implemented by this session. MESP-48 and MESP-50 remain open production gates.
* Fast-track capability completion remains `19/26 = 73.1%` until Sol acceptance, verified merge, and Jira closure. Production readiness remains separate and unchanged at approximately `47%` overall and `41%` Procurement/P2P.
* No Wafra-specific workflow, schema, API, permission, pricing, credit, approval, accounting, or lifecycle behavior was added. The project presentation remains main-only and `frontend/assets` remains untouched.
* Draft PR #80 is Open/Draft/Unmerged for MESP-136. Sol acceptance is required before any Ready transition or merge. No Jira writes, Opus review, merge, rebase, or force-push is permitted in this handoff.

## Ponytail Executor Tooling

When Ponytail is installed and available in the executor environment, use **Ponytail FULL** as the default mode for normal MESP implementation work.

**FULL semantics**: FULL must never weaken validation, Tenant isolation, authorization, financial/accounting integrity, data-loss safeguards, audit/evidence, concurrency, accessibility, or acceptance gates. It is a productivity and organization tool only.

Different Ponytail modes may only be selected when explicitly justified by Sol/Owner for the bounded task at hand.

Ponytail's installation, plugin cache, marketplace state, and hook trust are machine-local under the executor's Windows user profile. They must not be committed into the repository.

Absence or unavailability of Ponytail must not block otherwise valid work. Report unavailability honestly and continue under the repository's normal rules unless the task specifically requires it.

**Ponytail is never authority.** It cannot broaden an executor's
authorization. It cannot override an explicit STOP, authorize another
commit, authorize a Ready transition, authorize a merge, authorize a Jira
mutation, or weaken security, Tenant isolation, financial/accounting
integrity, audit, concurrency, or acceptance gates. See
`.ai/AI_EXECUTION_POLICY.md`.

## Permanent Architecture Rules (ADR-019 / Tenant & Workspace Isolation)

### 1. Tenant != Workspace
- **Tenant**: The primary SaaS security and data-isolation boundary. Resolved and authorized server-side before Tenant business data is accessible. A Tenant is NEVER an ordinary ERP workspace or user-selectable filter on every login. Normal single-Tenant users must never enumerate or be aware of unrelated Tenants.
- **Operational Workspace / Context**: Exists inside an already authorized Tenant and aligns with approved Company/Branch organization scope rather than a parallel authorization hierarchy. Single permitted context auto-selects; multiple permitted contexts use a header/application-context switcher. Never require raw GUID entry.
- **Platform Tenant Workspace**: A control-plane concept used by MESP Platform Administration (MESP-67), distinct from an ERP operational working context. Platform Administrator role alone grants zero authority over Tenant ERP business data.

### 2. Entry Flow and Host Resolution
- **Tenant-Specific Host** (e.g. `wafra.mesp.com`):
  `Host → Candidate Tenant → Authentication → Exact-Tenant Membership Authorization → Server-Owned Tenant Context → Tenant Overview → Optional Context Selection`.
  Hostname provides candidate routing information only, NOT authorization.
- **Common Host** (e.g. `mesp.com`):
  Routing entry: single authorized Tenant auto-redirects; multiple authorized Tenants presents a chooser limited strictly to active memberships; zero memberships renders a safe no-access state.
- **Platform Administration Host** (e.g. `admin.mesp.com`):
  Separate control plane for platform administration; access to Tenant ERP business data requires an explicit, audited exact-Tenant support grant or membership.
- *Note*: Hostnames are architectural configuration targets; DNS/TLS automation is separate infrastructure scope.

### 3. Workspace UX
- **Overview First**: Tenant Overview is the initial authenticated business landing surface upon entering a Tenant.
- **Context Handling**: One permitted operational context is automatically selected; multiple contexts use a header switcher.
- **Navigation**: Mandatory "Workspaces/Tenant Selection" as a required pre-ERP gateway and `/app/workspaces` as a mandatory entry gate are superseded by ADR-019. "Switch workspace" is removed from primary ordinary-user navigation.

### 4. Tenant Branding Configuration
- **Wafra Logo Asset**: An Owner-managed product source asset under `frontend/assets` mapped via generic Tenant branding configuration data, NEVER hardcoded branch logic (`if tenant == Wafra`).
- **Fallback**: Missing or unconfigured Tenant branding cleanly falls back to MESP platform branding.
- **Invariants**: Tenant branding never alters authorization, workflows, navigation permissions, Tenant scope, tax rules, numbering, or accounting behavior. On Wafra ERP surfaces, Wafra branding may be primary; on common/admin surfaces, MESP branding remains primary.

### 5. Saudi Riyal (SAR) Presentation
- **Saudi Riyal Asset**: A Saudi country-pack / SAR presentation asset under `frontend/assets`, NOT Wafra branding and NOT global currency formatting.
- **Presentational Only**: Presentation-layer only; causes zero FX conversion, zero tax effect, zero accounting effect, and zero persisted amount change.
- **Fallback & Non-SAR**: Safe text fallback (e.g. `SAR`) is preserved for semantic clarity in multi-currency comparison, audit, and exports. Non-SAR currencies remain completely unaffected. Governed by MESP-12 / MESP-37.

## Historical execution overlay - 25 August 2026 (MESP-133 Sol HOLD 3 micro-remediation)

PR #77 is Open/Draft/Unmerged on `feat/MESP-133-ap-ar-cash-settlement` from
HOLD 3 starting head `452441084a44d1a8a0a1d8db3a0d679aac5ff550`, based on main
`9ace42c7a830b5ef155a26b18d4a888676b8c188`. MESP-132 is Done/merged/closed;
MESP-133 remains In Progress/activated under MESP-10 while Sol HOLD `11892` /
HOLD 2 `11926` / HOLD 3 `11963`, MESP-10 progress comment `11893` /
reconciliation `11927` / HOLD 3 `11964`, and manual-AR supplemental finding
`11928` remain the acceptance authority. No Jira
writes, merge, Ready transition, MESP-134/MESP-135 activation, or Opus review
was performed.

The focused source/test remediation commit is
`b9eba368922899165324086aa59298d054fec25d`; the final implementation commit
is `536cd40984d58c3f61ae814ac4efb0d48c6aa8d8`; the final documentation/tracker
handoff SHA is recorded by `git rev-parse HEAD` after push.

The remediation resolves AP trusted payment-term/version and due-date
derivation, validates authoritative Supplier Tenant/lifecycle/Company/source
identity and fails closed without CreatedAt date fallback, reuses
`IFinanceSourceApprovalPolicy`, enforces manual-only
methods, binds cash/bank settlement posting to the selected linked GL account,
reconciles active subledger balances to actual journal lines, applies
accounting-date as-of allocation/receipt semantics, and enforces AP/AR and
Payment/Receipt route integrity plus rejected-to-Draft correction. Manual AR
and settlements resolve config-led MESP-120 Exchange Rate references by exact
document date, persist authorized rate/version evidence, and omit FX evidence
for functional-currency transactions.

Final validation is Release `0 warnings/0 errors`; REST/OpenAPI/host `54/54`;
full backend `1009/1009` with 0 failures/0 skips; SQL safety `61/61` against
disposable LocalDB; Angular `274/274` across 38 specs (focused settlement
workspace `15/15`); focused Finance Chromium `6/6`; full Chromium `38/38`;
initial bundle `496.44 kB`; Finance/GL
lazy `34.31 kB`; settlement lazy `56.04 kB`; and both npm audits
`0 vulnerabilities`. Runtime backend PID `34964` and frontend PID `34380` are
left running with required
HTTP 200 probes. `frontend/assets` is untouched. Accepted fast-track remains
`16/26 = 61.5%`; production readiness remains approximately `47%` overall and
`41%` Procurement/P2P. MESP-134 is planned but not activated.

## Historical execution overlay - 24 August 2026 (MESP-132 final Sol acceptance remediation)

PR #76 is squash-merged into `main` at
`ccc52a892c8258778f57c55c12fa0032bd3e276b`, from accepted feature head
`c0e04553db3c7b04fa7f7870b60fc439ec8a40b7` on retained branch
`feat/MESP-132-finance-foundation`. The implementation began at
`2f523582fbd3394b1eb11580eff490ba83aa9afb` and its source/test implementation
commit is `dcae7e2`. MESP-132 remains **In Progress / activated** in Jira while
Sol owns closure and MESP-10 Finance Epic reconciliation. MESP-131 and its
parent Inventory Epic MESP-8 are Done (closure comments `11842` and `11843`).

The public Manual Journal request no longer accepts source-owned identity,
evidence, or PostingRule fields; the server forces `manual-journal.v1` /
`manual`, null source evidence and rule, ManualTransactionCurrency authority,
and Required approval. Manual edits preserve that identity, while trusted
Inventory handoff processing retains its source lineage. Finance now has five
provider-realistic SQL Server LocalDB races covering period close/post, account
restriction/post, same-Journal posting, same-source Inventory handoff, and
first-company JournalSequence creation; nested SQL contention is classified as
a safe Finance conflict.

Final validation is Finance `12/12`; REST/OpenAPI and host-security `53/53`;
prior Inventory regression `89/89`; SQL Server safety `46/46`; full backend
`982/982` with 0 failures and 0 skips; Release build 0 warnings/0 errors;
Angular `259/259` across 37 spec files; initial bundle `496.34 kB`; Finance
lazy chunk `36.45 kB`; focused/full Chromium `2/2` and `34/34`; both npm audits
report 0 vulnerabilities. Final merged-main runtime is backend
`http://localhost:5300` PID `21112` and frontend `http://localhost:4300` PID
`39640`, with health, root, main.js, and `/app/finance` HTTP 200 probes.
`frontend/assets` and the existing Finance migrations are untouched.

Accepted fast-track capability completion is **16/26 = 61.5%** after the
verified merge. Overall production-ready
completion remains approximately **47%** and Procurement/P2P approximately
**41%**. Production/provider, MESP-48/MESP-50, backup/restore, capacity,
legal, specialist, migration/cutover, external/statutory, and Wafra-specific
core gates remain open or deferred. No next Finance capability starts
automatically, no Jira writes were performed, and no Opus prompt is added by
this handoff.

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
Production/provider, MESP-48/MESP-50, backup/restore, capacity, legal,
specialist, and cutover gates remain open. Protected source assets under
`frontend/assets` remain untouched. Jira writes are prohibited in this
documentation-only reconciliation session; GPT-5.6 Sol owns Jira management.

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

MESP-143 was implemented on branch `feat/MESP-143-tenant-aware-entry` from
the synchronized `main` baseline. The bounded capability adds configuration-led
Tenant host resolution, trusted-proxy handling, exact server-side membership
authority, common/platform entry boundaries, Overview-first Angular routing,
post-Overview Company/Branch context selection, generic Tenant branding, and
presentation-only SAR semantics. It does not add Tenant persistence, DNS/TLS
automation, external providers, Jira writes, or downstream Procurement work.

The current branch is being validated for one Draft PR against `main`; it must
remain unmerged and must not be force-pushed. Owner-managed assets under
`frontend/assets` remain untouched. The next exact session after this bounded
implementation is independent targeted Opus review of MESP-143 host/Tenant
isolation, platform boundary, context concurrency, branding/SAR fallback, and
MESP-123 regression evidence.

## Historical execution overlay - 17 August 2026 (MESP-123 Opus findings corrected; pre-Opus governance reconciliation; forward ADR-019/MESP-143 alignment)

MESP-123 is in progress on branch `feat/MESP-123-purchase-request-approval`,
continuing Draft PR #66 against `main`. Bounded corrective implementation
successfully resolved the merge-blocking findings from the independent Claude
Opus 5 review at commit `50d0c56cdae30f4490e45f8ce66727191b4cd68f`:
- **F-1 (Currency Rendering Resilience)**: `formatMoney` handles valid non-ISO
  MESP currency codes (`S2K`, `CUSTOM`) safely via localized decimal fallback;
- **F-2 (Source Decision Concurrency Token)**: `SupplierQuotationService`
  passes caller `expectedVersion` directly into `SupplierSourceDecisionCommand`;
  Angular `SupplierQuotationWorkspaceComponent.recordDecision()` provides
  `currentDecision()?.version ?? request.version` to enforce optimistic
  concurrency on first decisions and reselections;
- **F-5 & F-6 (Documentation & Bundle Reconciliation)**: Exact test counts
  (754 backend, 202 Angular unit, 8 Playwright E2E) and bundle sizes (478.57 kB
  initial, 91.94 kB lazy quotation chunk) reconciled; non-blocking P3
  observations (F-3, F-4) recorded.

This session performs repository governance reconciliation to inherit the
Owner-approved ADR-019 (`docs/ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md`)
and MESP-143 execution plan (`docs/MESP-143_Tenant_Aware_Entry_Execution_Plan.md`)
into persistent executor context. Zero product code changes are made.

**Current Delivery Rules:**
1. **MESP-143 is Planned / To Do**: ADR-019 is accepted forward architecture;
   do NOT implement MESP-143 (no host middleware, routing, chooser, branding
   service, or database schema changes) in this session.
2. **Next Exact Gate**: Claude Opus 5 targeted read-only re-verification of
   F-1, F-2, and F-5 per `TASK.md`.
3. **Downstream Sequence**: After targeted Opus review, GPT-5.6 Sol and Owner
   decide on PR #66 merge and MESP-123 closure. MESP-143 will then be activated
   as the prerequisite architecture/UX foundation before broad additional
   Tenant-facing UI work.
4. **Draft PR #66**: Remains OPEN, DRAFT, and UNMERGED.
5. **No Purchase Order**: Do not start Purchase Order, Goods Receipt, invoice,
   AP/accounting, payment, stock, or external integration work.
6. **Owner-Managed Assets**: Source assets under `frontend/assets` remain
   protected and untouched.

## Historical execution overlay - 16 August 2026 (MESP-123 B2 post-Phase-C foundation; superseded)

MESP-123 B2 is the bounded implementation session on
branch `feat/MESP-123-purchase-request-approval`, continuing Draft PR #66.
Phase A Purchase Request backend/API, Phase B/B1 Purchase Request Angular
journey, and Phase C Supplier Quotation/comparison/source-decision backend/API
remain present. B2 added the shared workspace shell, server-configured human
Tenant naming, legacy Wafra-inspired visual foundation, representative Purchase
Request list/detail adoption, and secure local Development authentication
shortcut.

Spec Kit initialization was an audit-only artifact on `chore/adopt-spec-kit`
preserved in local stash `spec-kit init generated adoption review`.
The canonical authenticated workspace route at B2 delivery was `/app/workspaces`;
legacy `/tenant/select` redirected into it. The normal shell sidebar exposed
Overview, Workspaces/Tenant Selection, Master Data, Price Lists, Master Data
Import, and Purchase Requests. Tenant labels came from server/configuration
(`Wafra` as a local generic fixture value). `MESP_DEV_AUTH_BYPASS=true` is
explicit, disabled by default, exact-Development, loopback-only, server-actor
based, and fails closed outside Development. B2 was followed by the functional
Supplier Quotation / Comparison Angular UI.

## Historical execution overlay - 12 August 2026 (MESP-116 approved decision reconciliation; superseded)

The Owner has rebaselined Release 1 as a **full-feature reusable B2B ERP**
with a 31 August 2026 **Release 1 Integrated Preview** milestone. The
milestone is an integrated preview of the real codebase, not an MVP,
throwaway/demo UI, Wafra fork, or scope reduction. Unfinished functionality
remains required Release 1 work after the preview.

MESP-115 is **Done** at its bounded governance/rebaseline scope. MESP-116 is
**Done** at its bounded Owner decision and implementation-unblock
reconciliation scope. Owner approval evidence is MESP-116 comment `10957`;
PD-025 through PD-046 are appended to the immutable Product Decision Register
in MESP-22 comment `10958`; and the final dependency map is
`docs/33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md`.
Focused PR #59 was reviewed at
`8b3f7b61c0128f97aa6a775dec23e623c1fde70e` and merged to `main` at
`b58bcaaeb4103c8fbdfb6a1c933c5239e228c5bd`; post-merge state/tracker
synchronization is commit `66183c1`; `main`/`origin/main` are synchronized.
A1-A16 and B1-B6 are approved only at their exact bounded positions. Class B
is the Release 1 product/implementation contract, but Finance, Inventory,
Reporting, Migration, and other named specialist validation remains mandatory
before production or irreversible accounting, destructive migration, or
cutover decisions. C1-C9 remain open production/external/legal gates and are
not approved or closed.

MESP-38 remains **Done** at its approved bounded Security, Audit, and Data
Governance BRD scope. MESP-39 remains **To Do, unactivated, and not executed**
as future-release Integrations and External Services BRD; no production
external integration, provider, credential, webhook, payment gateway, bank
feed, automated FX, external SSO, government submission, or external
infrastructure is authorized. MESP-40 remains **To Do/unactivated but required
for Release 1**, scheduled in the migration wave. MESP-23 remains **In
Progress** as the living Open Questions Register; reconciliation evidence is
comment `10976`. MESP-117 through MESP-142 remain To Do/not activated under
existing Epics. MESP-117 is the approved first capability handoff, with Jira
evidence `10977`, and must be executed only in a fresh later session.

Delivery is strictly sequential: one person, one active implementation
capability, one focused branch/PR. Luna executes; ChatGPT/Sol plan and review;
Opus is reserved for checkpoint A after a coherent Procurement+Inventory
spine, checkpoint B after a coherent Finance+Sales spine, and checkpoint C
before serious RC/production review, or earlier only for a genuine critical
security/Tenant, accounting, stock, destructive-migration, or major
cross-module risk. Angular/frontend is included in each capability unless a
real prerequisite is explicitly recorded. Routine readiness tickets are not
created merely for ceremony.

Every fresh chat executes exactly one root `TASK.md` session and stops. The
current exact next session is **MESP-117 - Complete Master Data shared Angular
UX for the existing Category/UOM/Product/Supplier/Customer slices**. It must
use the approved MESP-116 contract and docs/33 handoff, and must not execute
MESP-39, activate MESP-40, or widen scope. MESP-116 itself added no source,
tests, persistence, schema, migrations, APIs, UI, providers, credentials,
infrastructure, or production configuration.

Use the efficient reading hierarchy: `AGENTS.md`, `CLAUDE.md`,
`.ai/CURRENT_STATE.md`, `TASK.md`, the owning BRD/spec, affected ADRs/
contracts/entities, current status/diff, and only materially dependent
decision rows. Do not reread every BRD/PRD routinely. Preserve the complete
Release 1 scope in the fast-track plan, the MESP-38 gates, MESP-48/MESP-50,
SQL/provider/infrastructure/production gates, no Retail POS, and no
Wafra-specific core behavior.

This MESP-116 session was documentation/Jira/governance only. Do not claim a
production-capability percentage increase from planning, approval, or Jira
activity. At the end of this bounded session read and update
`docs/staticts.md`, update current state/forecast/Jira counts conservatively,
validate the allowlisted diff, commit and push the tracker with the focused
PR, merge only when reviewed and clean, synchronize `main`/`origin/main`, and
stop after the complete MESP-117 handoff.

## Historical execution overlay - 12 August 2026 (MESP-115 full-feature fast-track rebaseline; superseded by MESP-116)

The Owner has rebaselined Release 1 as a **full-feature reusable B2B ERP**
with a 31 August 2026 **Release 1 Integrated Preview** milestone. The
milestone is an integrated preview of the real codebase, not an MVP,
throwaway/demo UI, Wafra fork, or scope reduction. Unfinished functionality
remains required Release 1 work after the preview.

MESP-115 is **Done** at the single bounded governance/rebaseline scope for
this session. Focused PR #58 was reviewed at
`0681c0182b0b6894f5f2b83db1728253ac54e279` and merged to `main` at
`a5ee9426d252901e74888bdc3ca94970c969aa20`. Its canonical artifacts are:

- `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`;
- `docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`; and
- `docs/32_Release_1_Tax_VAT_Scope_Clarification.md`.

PD-024 is appended to the immutable Product Decision Register for explicit
Owner directions only: full Release 1/preview intent, essential cycles,
sequential one-person delivery, Luna as primary executor, reserved Opus
checkpoints A/B/C, external integrations excluded from Release 1, and
internal reusable configuration-led Tax/VAT restored without statutory,
ZATCA/FATOORA, legal, certification, submission, signing, clearance, or
external-provider scope. The recommendations in the Consolidated Owner
Decision Pack are **NOT APPROVED UNTIL OWNER SIGNS** and are not silently
implemented by this session.

MESP-38 remains **Done** at its approved bounded Security, Audit, and Data
Governance BRD scope. MESP-39 remains **To Do, unactivated, and not executed**
as a future-release Integrations and External Services BRD; no production
external integration, provider, credential, webhook, payment gateway, bank
feed, automated FX, external SSO, government submission, or external
infrastructure is authorized. MESP-40 remains **To Do/unactivated but required
for Release 1**, scheduled in the migration wave. MESP-23 remains **In
Progress** as the living Open Questions Register. MESP-117 through MESP-142
are capability backlog tasks under existing Epics; all remain To Do and not
activated.

Delivery is strictly sequential: one person, one active implementation
capability, one focused branch/PR. Luna executes; ChatGPT/Sol plan and review;
Opus is reserved for checkpoint A after a coherent Procurement+Inventory
spine, checkpoint B after a coherent Finance+Sales spine, and checkpoint C
before serious RC/production review, or earlier only for a genuine critical
security/Tenant, accounting, stock, destructive-migration, or major
cross-module risk. Angular/frontend is included in each capability unless a
real prerequisite is explicitly recorded. Routine readiness tickets are not
created merely for ceremony.

Every fresh chat executes exactly one root `TASK.md` session and stops. The
current exact next session is **MESP-116 — Release 1 Consolidated Owner
Decision Approval and Implementation-Unblock Reconciliation**. It must read
the Owner Decision Pack, obtain explicit Hossam approval for applicable rows,
apply only approved decisions, reconcile MESP-23 and existing Jira owners,
publish the final dependency map, and hand off the first capability. It must
not execute MESP-39, activate MESP-40, or add source, tests, persistence,
schema, migrations, APIs, UI, providers, credentials, infrastructure, or
production configuration.

Use the efficient reading hierarchy: `AGENTS.md`, `CLAUDE.md`,
`.ai/CURRENT_STATE.md`, `TASK.md`, the owning BRD/spec, affected ADRs/
contracts/entities, current status/diff, and only materially dependent
decision rows. Do not reread every BRD/PRD routinely. Preserve the complete
Release 1 scope in the fast-track plan, the MESP-38 gates, MESP-48/MESP-50,
SQL/provider/infrastructure/production gates, no Retail POS, and no
Wafra-specific core behavior.

This rebaseline is documentation/Jira/governance only. Do not claim a
production-capability percentage increase from planning or Jira creation.
At the end of the bounded session read and update `docs/staticts.md`, update
current state/forecast/Jira counts conservatively, validate the allowlisted
diff, commit and push the tracker with the focused PR, merge only when
reviewed and clean, synchronize `main`/`origin/main`, and stop for review.

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
supported-volume, production-governance, Reporting, Currency, Finance, and
Inventory decision boundaries.

MESP-38 was documentation-only. It added no source, tests, persistence,
schema, migrations, APIs, UI, providers, credentials, infrastructure,
production configuration, or production capability. Release 1 remains a
Saudi-localized Core ERP B2B baseline; Retail POS, Wafra-specific core
behavior, statutory/tax/ZATCA/FATOORA implementation, external production
integrations, and privacy/legal certification remain excluded or separately
gated. PRD visual rendering was attempted during the MESP-38 session but was
unavailable because pdf2image and LibreOffice/soffice are not installed; no
visual claim is made.

The root TASK.md contains the exact next MESP-39 session prompt, and
.ai/CURRENT_STATE.md contains the current verified detailed state. No next
task starts automatically.

Hossam's standing Owner approval continues to cover normal bounded BRD,
specification, readiness, merge, closure, and next-session activation work
within the approved project scope and architecture. Stop only for a real
security/Tenant-isolation, accounting/data-integrity, destructive
migration/data-loss, unresolved-business-decision, legal/external-validation,
credential/production-infrastructure, or material scope/architecture blocker.
Each fresh chat executes exactly one root TASK.md session, updates genuinely
affected state and Jira, and stops for review; independent Opus review remains
due after every five completed sessions or earlier at a critical checkpoint.

## Historical execution overlay - 12 August 2026 (Pre-MESP-38 reconciliation handoff)

The verified live sequence is MESP-27 through MESP-37 **Done** at their
approved bounded BRD scopes. MESP-23 remains **In Progress** as the living
Open Questions Register. MESP-38 - Security, Audit, and Data Governance BRD is
the single next BRD task; it is **To Do**, has not been activated, and must not
be executed by this reconciliation. MESP-48, MESP-50, MESP-53, MESP-54, and
MESP-110 remain open and unapproved at their existing supported-volume,
production-governance, Reporting, Currency, and Finance boundaries.

The root `TASK.md` contains the exact next MESP-38 session prompt, and
`.ai/CURRENT_STATE.md` contains the current verified detailed state. MESP-38
is documentation-only: it may define bounded business requirements and
acceptance scenarios, but it must not add source, tests, persistence, APIs,
UI, providers, credentials, infrastructure, production configuration, or
production capability. No next task starts automatically.

Release 1 remains a Saudi-localized Core ERP B2B baseline. Retail POS and
Wafra-specific core behavior remain excluded; statutory/tax/ZATCA/FATOORA
implementation, external production integrations, and privacy/legal
certification remain outside the approved scope.

Hossam's standing Owner approval continues to cover normal bounded BRD,
specification, readiness, merge, closure, and next-session activation work
within the approved project scope and architecture. Stop only for a real
security/Tenant-isolation, accounting/data-integrity, destructive
migration/data-loss, unresolved-business-decision, legal/external-validation,
credential/production-infrastructure, or material scope/architecture blocker.
Each fresh chat executes exactly one root `TASK.md` session, updates genuinely
affected state and Jira, and stops for review; independent Opus review remains
due after every five completed sessions or earlier at a critical
architecture/security, accounting, migration/data-model, or major cross-module
checkpoint.

## Historical execution overlay - 9 August 2026 (MESP-101 readiness complete; preserved)

MESP-100 is Done with closure evidence in Jira comment `10663`. MESP-99 /
M95-SL-02 Category and UOM is Done through focused PR #33, correction PR #34,
and final audit-semantics correction PR #35; the final correction merge is
`3e51f98f8c80b9989632499632605894c18570cf`. MESP-101 was the bounded
readiness item for M95-SL-03 Product identity and is now **Done** after
readiness PR #36 merged to `main` at
`c7392a55e0b60fd83e48447e3f9218f82cfaccea`; Jira closure evidence is comment
`10672`, with activation/owner evidence in comment `10671`.

The approved Product-only bounds are MD-OD-001 (Product master data is
Tenant-wide inside its owning Tenant, reusable by that Tenant's
Companies/Branches, with no cross-Tenant sharing), MD-OD-003 (hybrid
Tenant-unique SKU and barcode coding: manual/import/generate SKU values and
zero-or-more Tenant-unique barcodes, without inventing EAN/GS1 rules),
MD-OD-005 (routine lifecycle actions need permission, exact server-derived
authority, and audit but no separate approver), MD-OD-008 (no Draft; authorized
creation is Active with Deactivate/Reactivate), MD-OD-010 (Product stores
tracking configuration only; Inventory owns operational batch/lot/serial/expiry
behavior), and MD-OD-011 (Product and Item are one Release-1 master-data
identity; no separate variant/Item entity or variant behavior in this slice).
These bounds apply only to M95-SL-03 Product identity and do not resolve the
remaining decision register.

The root `TASK.md` is handed off to the exact next Product identity
implementation session. The completed readiness session added no Product/Item/SKU/Barcode
source behavior, entities, tables, migrations, endpoints, or UI. The actual
backend topology is four projects: `MiniErp.Api`, `MiniErp.App`,
`MiniErp.Contracts`, and `MiniErp.Infrastructure`. ADR-002 is the detailed
project/module enforcement record and ADR-006 remains authoritative for shared
SQL Server, Tenant ownership, module-owned contexts/schemas/migrations, and
provider/production gates. MESP-48, MESP-49, and MESP-50 remain open.

## Historical execution overlay - 8 August 2026 (preserved)

MESP-31 is **Done** as the approved BRD baseline. PR #29 is **merged** at
actual merge commit `93f4e83992ef46f498cfbfacbb513cfc3d8dda7d`; its approved
final head is `c465d660e49a254f2fffbb95e0d07c5fcf17a193`. MESP-95 is **Done**
with Jira closure evidence in comment `10654`. ChatGPT passed the final review,
and M95-R01, M95-R02, and M95-R03 are closed. MESP-96 is the single next
executable implementation item and is **In Progress** in Jira for M95-SL-01
only. This overlay supersedes older live-state claims below.

M95-SL-01 is contract-only and non-persistent. It may not create Master Data
EF entities/tables, migrations, or an `MESP` database/access solely for this
slice, and it may not decide Product/Item identity, SKU/Barcode, tracking,
business availability, approval catalogue, or Draft/Active behavior. Retail
POS and Wafra-specific core behavior remain excluded; M95-SL-02 and later
slices remain out of scope. No Master Data persistence exists yet.

Hossam has standing Owner approval for normal BRD, specification, readiness,
merge, closure, and next-session activation while work remains inside the
approved project scope and architecture. Do not stop for ceremonial approval.
Stop only for a real security/Tenant-isolation, accounting/data-integrity,
destructive migration/data-loss, unresolved-business-decision,
legal/external-validation, credential/production-infrastructure, or material
scope/architecture blocker.

Each fresh Codex/Luna chat executes exactly one root `TASK.md` session. At the
end of every session, validate, review the complete task diff, update `TASK.md`
with the next exact session, update `.ai/CURRENT_STATE.md`, update every
genuinely affected Markdown state/plan file, update Jira, commit/push, merge
only when clean and unblocked, then STOP and return the completion report to
Hossam for ChatGPT review. Never execute the next `TASK.md` automatically in
the same chat. Run an independent Opus project review after every five
completed sessions, or earlier at a critical architecture/security,
accounting, migration/data-model, or major cross-module checkpoint.

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

- Read the active Jira item and the relevant approved PRD, BRD, ADRs, glossary,
  foundation specification, and Product Delivery Master Plan before changing
  scope.
- Release 1 is B2B ERP only. Retail POS and Wafra-specific core behavior are
  prohibited; Wafra is validation-only.
- Keep one implementation item active at a time. Do not automatically start the
  next Jira issue or create parallel work.
- Review the complete task-related diff and run targeted tests before commit or
  merge. Do not change source code for documentation/Jira-only work.
- Preserve MESP-48 supported-volume and MESP-50 retention, privacy, legal-hold,
  purge, residency, backup and restoration gates.
- Stop and escalate on Tenant leakage, authentication/authorization weakness,
  data loss or purge, accounting-integrity risk, or a legal/privacy decision
  that cannot be safely deferred.
- MESP-94 is Done: PR #26 merged to `main` at the actual merge commit
  `06d837c958c1cb7977dc121e3aaea4e7278944fd` (approved final head
  `2c7ed3dec4662672bb78967ceb70db7ed73eb7d4`) after a ChatGPT final merge
  review verdict of APPROVED FOR MERGE; it used normal bounded review, not
  the MESP-92/MESP-93 manual security merge hold. MESP-93 is Done: PR #24
  merged to `main` at `005c796629341ab9becfbc6d1abe2ae34b6a7332` (reviewed
  head `83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`) after focused ChatGPT
  security re-review approval. PR #25 (docs) merged to `main` at
  `9f333c9734c767673e43a30d6b57c05793e1fb69`. MESP-92 carried the same
  manual-hold exception earlier in the sequence; PR #22 merged to `main` at
  `322341e70e56270797d5770b4b90342c20b7833e` after focused ChatGPT approval,
  and MESP-92 is Done, as are MESP-89, MESP-63, MESP-61 and MESP-64. The
  Foundation completion checkpoint following MESP-94 confirms MESP-92/93/94
  Done and MESP-48/MESP-50 as intentional open production gates, not BRD
  entry blockers. PR #27 merged that closure to `main` at
  `c86ecb851e88205f1d3907f5a5c36cfb59ce8b54`, the current merged-main
  baseline.
- MESP-31 is **In Progress** under Parent Epic `MESP-6 — EPIC 06 - Master
  Data and Product Catalog`, on branch
  `docs/MESP-31-master-data-product-catalog-brd`. Both Owner authorizations
  are recorded in live Jira: comment `10615` (BRD entry) and comment `10616`
  (future Master Data implementation, conditional). MESP-31's Jira Source
  Baseline is primary anchor PLT-003 with supporting anchors PLT-002,
  SAL-001, PROC-002, PROC-008, FIN-001, FIN-003, FIN-007, FIN-010, KSA-002,
  BR-013, ADM-003 and the applicable PRD RULE set for master-data integrity;
  PLT-011 through PLT-014 and BR-004 are Platform Administration anchors and
  are not MESP-31's baseline.
- `docs/16_Master_Data_and_Product_Catalog_BRD.md` covers Products, Product
  Categories, Units of Measure, Suppliers, Business Customers, Price Lists,
  Taxes, Payment Terms, Currencies and Exchange Rates, and is published on
  open **PR #28**. Its v0.1 head `6d0aa80` drew a business-requirements
  verdict of CHANGES REQUIRED BEFORE OWNER APPROVAL / MERGE; a bounded
  correction round produced v0.2 with ten Open Decisions (MD-OD-001–010),
  reviewed at head `8657011`, which drew a further verdict of CHANGES
  REQUIRED — FINAL SMALL CORRECTION ROUND: M31-R10 (Product/Item modelling
  needed an Owner decision rather than Confirmed status), M31-R11 (residual
  approval assumptions), M31-R12 (Saudi launch language) and M31-R13 (an
  unrelated `.vscode/settings.json` change in the PR delta). A second bounded
  correction round closed all four and produced v0.3, whose Open Decision
  register now holds eleven decisions (MD-OD-001–011, adding Product/Item
  modelling as MD-OD-011).
  The BRD is **v0.3 Approved Business Baseline**, approved by Hossam in Jira
  comment `10649` at reviewed content head
  `1e2d055354f0ddde833190948d09fa426707484c`. The Open Decision Register
  MD-OD-001 through MD-OD-011 remains preserved; approval does not silently
  resolve any of those decisions. Two Founder Decision Pack defaults remain
  explicitly unapproved and must not be treated as requirements: MESP-41
  (batch/lot/serial/expiry, now MD-OD-010) and MESP-54 (exchange-rate sourcing
  and Finance approval, owned by MESP-34). Hossam also authorized the later
  Master Data implementation phase, subject to the normal Definition of Ready
  and the dedicated MESP-95 readiness item. No Master Data implementation has
  begun and none may start automatically — see `.ai/CURRENT_STATE.md` for the
  exact live position.
- The canonical approved PRD is `docs/MESP_PRD_v1.2.docx`. Older references to
  `MiniERPSaaSPlatform_PRD_v1.2.docx` or
  `MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx` name the same
  unchanged file.
- `.ai/CURRENT_STATE.md` is the entry point for the verified current branch,
  head, active item, open Pull Request and open findings.

## Permanent REST/API Definition of Done

Every public REST operation must be complete as one connected contract. Before
the owning capability can close, the operation must be present in the
Foundation operation catalogue with its exact route, permission, scope,
antiforgery, audit, and unsafe-effect metadata; be mapped by the real API;
appear in the generated OpenAPI document with a stable `operationId`, useful
summary and boundary description, and explicit response outcomes; and be
covered by an architecture/contract test that rejects missing or placeholder
documentation. Scalar is only the developer-facing rendering of that
generated document in Development/QA, with agent actions disabled; it is not a
second handwritten contract and must not be exposed as a production feature.
