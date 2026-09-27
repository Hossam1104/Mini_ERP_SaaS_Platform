# Roadmap

The Release 1 plan: what is done, in progress, next, blocked and deferred. Each line links to its
tracker item in [Project #1](https://github.com/users/Hossam1104/projects/1). References use
`MESP-<n> (#<issue>)`. **Live tracker and Git state outrank this file.** Anyone who changes an item's
state also updates the line here.

_Last reconciled: 2026-09-27 (MESP-168 (#289) accepted; MESP-141 Slices 12–15 created as MESP-169..172 (#292–#295); Q-T)._

**Accepted Executor cycles since the last Sol review: 3** (counter in `ORCHESTRATION_STATE.yaml`,
`MODEL_ROUTING.md` §2; the owner reset it to 0 on 2026-09-27). The last Sol review was the MESP-149
cleanup review (PR #278, closed unmerged as superseded).

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
| MESP-141 (#229): Release 1 migration and repeatable Tenant onboarding | Active | Slices 1–11 are merged and **accepted**. Slice 11 was accepted under MESP-150 (#265) on 2026-09-27 after PR #281 merged; that PR fixed MESP-161..165 (#279, #280, #282–#284) and corrected the MESP-156..160 (#272–#276) oracles. MESP-166 (#285) stays open as a **watch item**: one full-gate red on the attempt-start path, whose output was lost, never reproduced (Opus 25/25; Luna 30 isolated, 10 class and 1 full-gate runs, all green). If it recurs, capture it and classify it under #285. The MESP-168 (#289) audit (`docs/audit/mesp-141-m40-traceability.md`) found 80 rows: Met 30, Partial 32, Not met 1, Deferred-by-authority 5, Depends-on 12. So MESP-141 stays open until Slices 12–15 close the Partial and Not met rows. |
| MESP-23 (#112): Open Questions Register | Living | The register of open business questions. It stays open. |

## Next (in order)

Backend path, run serially. The UI lane is parallel but waits for the owner's reference example (Q-R).

| # | Item | Model / effort (planned) | Notes |
|---|---|---|---|
| 1 | MESP-169 (#292): MESP-141 Slice 12, covering the preview/dry-run no-effect contract, pre-commit cancellation and corrected-row retry | Executor | Audit gap groups 3–4. The prompt is in `TASK.md`. |
| 1b | MESP-170 (#293): Slice 13, covering the source contract, lineage and validation coverage | Executor | Gap groups 1–2, within M40-DEC-003. |
| 1c | MESP-171 (#294): Slice 14, covering the authority matrix, readiness review and report completeness | Executor | Gap group 5, within M40-DEC-006. Then Opus decides MESP-141 closure; the Depends-on rows (M27, Wave 1, MESP-28/30/38) are outside it. |
| 2 | MESP-151 (#266): prove one Golden Release-1 end-to-end business cycle | Executor | Setup → Master Data → Supplier → Purchase → Goods Receipt → Inventory → Customer/B2B Sale → Receivable → Payment/Cash → Payable/Settlement → GL → reconciliation/reporting. |
| 3 | Platform Administration Wave 1: MESP-65..85 (#154–#174) under MESP-2 (#91) | Executor | **In the QA build (Q-Q).** 21 stories; Opus batches them into prompts. |
| 4 | MESP-152 (#267): stable functional/API baseline | Executor | Covers Wave 1. The UI feature screens must preserve it. |
| 5 | MESP-142 (#230): Release 1 stabilization, regression, performance, UAT and release candidate for QA | — | **Not Activated.** It needs positive activation authority. |
| UI | MESP-153 (#268): total UI/UX modernization, plus MESP-172 (#295) bilingual Migration review (Slice 15) | Executor | **Waits for the owner's reference UI (Q-R).** The design system and shell can run beside items 2–4. Feature screens follow item 4. No Wafra-specific behavior (ADR-019). |

**Estimate to a QA-ready build (2026-09-27, Planner):** about 10–14 weeks, so mid-December 2026 to
early January 2027. It assumes the UI reference arrives within about 2 weeks. The biggest risks are
Wave 1 (4–6 weeks) and the golden cycle (1–2 weeks, likely to surface cross-module defects).

## Backlog (Release 1, not yet sequenced)

| Item | Notes |
|---|---|
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
| Migration audit | MESP-168 (#289): BRD 40 traceability audit, PR #291 at `d2de6e8`. |
| Cleanup and operating model | MESP-149 (#264): cleanup on `main` (PR #271); Sol review accepted; follow-ups SOL-CL-05/06/07 applied 2026-09-27, SOL-CL-01/04 done in MESP-167 (#286), PR #288 at `1d2b75a`. MESP-150 (#265): Slice 11 accepted, PR #281 merged at `6d14af7`. |

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
