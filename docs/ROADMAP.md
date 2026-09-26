# Roadmap

The Release 1 plan: what is done, in progress, next, blocked and deferred. Each line links to its
tracker item in [Project #1](https://github.com/users/Hossam1104/projects/1). References use
`MESP-<n> (#<issue>)`. **Live tracker and Git state outrank this file.** Anyone who changes an item's
state also updates the line here.

_Last reconciled: 2026-09-26 (Opus review of the MESP-165 fix, MESP-150 (#265))._

**Executor prompts since the last Sol review: 4** (window 10–15, `MODEL_ROUTING.md` §2, Q-N). The
last Sol review was the MESP-149 cleanup review (PR #278, closed unmerged as superseded).

## Headline metrics (two different things, never conflate them)

| Metric | Value | Meaning |
|---|---|---|
| Capability completion | **24/26 = 92.3%** | Accepted capabilities MESP-117..MESP-140 of the 26 planned (MESP-117..MESP-142). It is not a readiness measure. |
| Production readiness | **~47% overall, ~41% Procurement/P2P** | A separate, conservative estimate. It is gated by MESP-48 and MESP-50. |

Readiness here does not mean production, deployment, launch, UAT or compliance readiness.

## Agreed sequence

The owner approved this order:

```
Finish MESP-141 → Golden Release-1 cycle → stable functional/API baseline → total UI/UX modernization → MESP-142 stabilization/UAT
```

## In progress

| Item | State | Notes |
|---|---|---|
| MESP-141 (#229): Release 1 migration and repeatable Tenant onboarding | Active | Slices 1–11 are merged. Slices 1–10 are accepted. **Slice 11** (reconciliation, approval, Ready-for-Handover; PR #262, `ac0309a`) is still **not accepted** (MESP-150 (#265), reviewed 2026-09-26). Draft PR #281 now fixes MESP-161 (#279), MESP-162 (#280) and MESP-163 (#282) and corrects the R04/R05/R07 oracles; all are met. The MESP-164 (#283) regression fix is met (`06e6e92`). The MESP-165 (#284) approval-evidence race fix is met (`d90c9e5`). One question remains: MESP-166 (#285). `MESP141_sql_server_execution_claim_is_acquired_before_owner_preflight` failed once in a full gate on the attempt-start path that MESP-163 rewrote. Its message was lost, and it passed 25/25 in Opus's repro. Later slices are not activated. |
| MESP-149 (#264): full project cleanup, refactor, tracker reconciliation and operating model | In progress | The cleanup is on `main`. The Sol 6 review (PR #278, closed as superseded) is done and the Opus verdict is **ACCEPTED**: the cleanup stands, with follow-ups SOL-CL-01/04/05/06/07 (see Next). Opus closes #264 once follow-up 2 is accepted (Q-O). |
| MESP-23 (#112): Open Questions Register | Living | The register of open business questions. It stays open. |

## Next (in order)

| # | Item | Model / effort (planned) | Notes |
|---|---|---|---|
| 1 | MESP-166 (#285) claim-race diagnosis on Draft PR #281, then an Opus review; if nothing blocks, Slice 11 is accepted under MESP-150 (#265) | Luna 6 / xhigh | Diagnosis only. Capture and classify the red verbatim over a repro budget (isolated, class-level, full gate), or record that the budget stayed green. The prompt is in `TASK.md`. |
| 2 | Cleanup follow-ups from the Sol review: governance text (SOL-CL-05, SOL-CL-07) and the BRD byte restore (SOL-CL-06) | Opus 5.5 (governance docs), then the backend suite | Restore "completed" in the immutable-report rule, and authorization, data-loss safeguards and accessibility in the Ponytail guard. Restore tag blob `2a5febc` for `requirements/16_…BRD.md`. |
| 3 | Test hardening from the Sol review: the R4 SQL shape check (SOL-CL-01) and the AP/cash-bank D-18 tests (SOL-CL-04) | Luna 6 / xhigh | Test-only. It does not change the R4 allowlist; the four-site baseline is ratified (Q-P). |
| 4 | Decide whether MESP-141 needs further slices before it closes | Opus 5.5 | M40 exit criteria: completed reconciliation, accepted exceptions, named approvals, a readiness snapshot. |
| 5 | MESP-151 (#266): prove one Golden Release-1 end-to-end business cycle | Luna 6 / xhigh | Setup → Master Data → Supplier → Purchase → Goods Receipt → Inventory → Customer/B2B Sale → Receivable → Payment/Cash → Payable/Settlement → GL → reconciliation/reporting. |
| 6 | MESP-152 (#267): stable functional/API baseline | Luna 6 / xhigh | The UI work must preserve this baseline. |
| 7 | MESP-153 (#268): total UI/UX modernization | Luna 6 / xhigh | No hard-coded Wafra behavior. Branding is configuration (ADR-019). |
| 8 | MESP-142 (#230): Release 1 stabilization, regression, performance, UAT and release candidate | — | **Not Activated.** It needs positive activation authority. |

## Backlog (Release 1, not yet sequenced)

| Item | Notes |
|---|---|
| MESP-65..MESP-85 (#154–#174): Platform Administration Wave 1 stories under MESP-2 (#91) | Labelled `release-1` (Q-F keeps the labels). Where they fall in the sequence is still undecided. |
| MESP-146 (#238): run the LocalDB-dependent backend tests in hosted CI | CI enabler under MESP-145 (#263). |
| MESP-147 (#239): harden CI determinism, portability and build-graph coverage | CI enabler. |
| MESP-148 (#240): reproducible release build provenance and artifacts | CI enabler. |
| MESP-154 (#269): reduce the ratcheted architecture exceptions and size watch items | Covers D-07, D-08, D-09 and D-23. See `ARCHITECTURE.md` § Enforcement. |
| MESP-155 (#270): frontend initial-bundle budget overrun and moderate npm advisories | D-13. The bundle is 514.26 kB against a 500 kB budget. npm audit reports 4/7 moderate, 0 high. |

## Blocked / production gates

| Item | Gate |
|---|---|
| MESP-48 (#137): reference Tenant volume assumptions | Production volume, capacity and performance. |
| MESP-50 (#139): Tenant data residency and retention policy | Production security, retention, residency, backup and purge. |
| M40-DEC-001..006 (BRD 40) | Open migration decisions. The Production approval quorum and SoD exceptions (M40-DEC-006) are unresolved. Code fails closed without a configured policy. |
| Production decisions (ADR-010..016) | Telemetry exporter, hosting/RPO/RTO, secrets and keys, residency/retention/purge, Saudi e-invoicing, RLS adoption or deferral. |
| Deployment / CD | Not implemented. It is deferred until the app is published to a server (Q3). |
| Production migration and cutover | Not authorized by MESP-141 development completion. |

## Deferred (not Release 1)

| Item | Notes |
|---|---|
| MESP-39 (#128): Integrations and External Services BRD | Future release. It authorizes no external integrations, providers or credentials. |
| MESP-14 (#103): Integrations epic | Parent of MESP-39. |
| Retail POS, Wafra-specific behavior, statutory ZATCA/FATOORA | Permanently out of scope for Release 1 (see `PROJECT.md` §3). |

## Epics closed after the cleanup (owner decision Q-K)

Q-K: only the owner closes epics. GitHub shows these ten closed on 2026-09-25 between 00:12:38Z and
00:13:22Z. The evidence does not show who closed them (AGENTS.md §1.8).

- MESP-3 (#92) Identity and Access
- MESP-4 (#93) Multi-Tenancy
- MESP-5 (#94) Organization
- MESP-6 (#95) Master Data
- MESP-7 (#96) Procurement
- MESP-9 (#98) Sales
- MESP-10 (#99) Finance
- MESP-11 (#100) Reporting
- MESP-12 (#101) Saudi Localization
- MESP-13 (#102) Security and Audit

MESP-8 (#97) Inventory was closed on 2026-09-09.

Epics that stay open because they have open children:
- MESP-1 (#90): gates MESP-48 and MESP-50, MESP-142, MESP-23 and MESP-151..153.
- MESP-2 (#91): MESP-65..85.
- MESP-14 (#103).
- MESP-15 (#104): now In Progress/Active with MESP-141 and MESP-150.
- MESP-145 (#263).

## Done

| Area | Items |
|---|---|
| Product governance and BRDs | MESP-16..MESP-38 (#105–#127) and MESP-40 (#129). Approved BRDs are in `requirements/`. |
| Owner decisions | MESP-41..47, 49, 51..56 (#130–#136, #138, #140–#145) and MESP-110 (#198), 113 (#201), 116 (#204). |
| Foundation | MESP-57..64 (#146–#153): modular monolith, SQL/Tenant guard, auth seam, REST/OpenAPI, durable work, audit, Angular shell, test harness. MESP-86..94 (#175–#183): spec and hardening. |
| Master Data | MESP-95..107 (#184–#195, #233) and MESP-117..122 (#205–#210): UX, Currency/Payment Terms, Tax/VAT, Exchange Rate, Price List, import. |
| Procurement / P2P | MESP-123..127 (#211–#215): PR/quotation/approval, PO/Supplier Confirmation, Goods Receipt/Purchase Invoice, three-way matching, Supplier Return. |
| Inventory | MESP-128..131 (#216–#219): ledger/reservation, receipts/transfers/returns, adjustments/counts/issue, moving weighted average valuation. |
| Finance | MESP-132..135 (#220–#223): GL foundation, AP/AR/Cash/settlement, tax/FX/revaluation, close/corrections/reports. |
| Sales / O2C | MESP-136..138 (#224–#226): quotation/order/credit, fulfillment/Delivery/invoice, returns/credit notes/receipts. |
| Reporting and cross-cutting | MESP-139 (#227) reporting catalogue. MESP-140 (#228) security/audit/files/notifications/localization/support. |
| Entry model | MESP-143 (#231): Tenant-aware entry routing and Overview-first context (ADR-019). |
| Governance | MESP-144 (#232) repository health checkpoint. GitHub Actions CI (#235). |

## Known non-blocking findings

- **ADR-019 follow-ups:**
  - cross-host session continuity before a Production multi-host cutover;
  - the duplicate active membership invariant;
  - one OpenAPI summary-quality item;
  - canonical-host ambiguity.
- **Historical MESP-124 P3 observations.** Their current closure state has not been re-verified:
  - implicit approval-stage semantics when no one is eligible;
  - supplier-change predicate asymmetry;
  - generic HTTP error classification;
  - frontend idempotency-key scope;
  - privacy of retained audit snapshots;
  - neutralizing the Development auth bypass in tests;
  - source-decision consumption after a cancelled PO;
  - lockfile maintenance.
- **Hosted Backend CI excludes the LocalDB safety subset.** MESP-146 (#238) tracks this.
