# Roadmap

The Release 1 plan: what is done, in progress, next, blocked and deferred. Each line links to its
tracker item in [Project #1](https://github.com/users/Hossam1104/projects/1). References use
`MESP-<n> (#<issue>)`. **Live tracker and Git state outrank this file.** Anyone who changes an item's
state also updates the line here.

_Last reconciled: 2026-09-25 (cleanup MESP-149 (#264))._

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
| MESP-141 (#229): Release 1 migration and repeatable Tenant onboarding | Active | Slices 1–11 are merged. Slices 1–10 are accepted. **Slice 11** (reconciliation, approval, Ready-for-Handover; PR #262, `ac0309a`) was **REJECTED** by MESP-150 (#265) on 2026-09-25. Five provider oracles are not directly asserted: Bugs MESP-156..160 (#272–#276). Slice 11 stays merged and not accepted. Later slices are not activated. |
| MESP-149 (#264): full project cleanup, refactor, tracker reconciliation and operating model | In progress | This cleanup. The Sol 6 review of `pre-cleanup-20260925..main` comes after MESP-150. |
| MESP-23 (#112): Open Questions Register | Living | The register of open business questions. It stays open. |

## Next (in order)

| # | Item | Model / effort (planned) | Notes |
|---|---|---|---|
| 1 | Sol 6 independent review of the cleanup (`pre-cleanup-20260925..main`) under MESP-149 (#264) | Sol 6 / high | This is a critical point: it is a governance and architecture change on `main`. MESP-150 (#265) is done: Slice 11 was REJECTED, and #265 stays open until the Bugs are fixed and the slice is re-reviewed. |
| 2 | Slice 11 evidence Bugs MESP-156..160 (#272–#276), then an Opus re-review of Slice 11 under MESP-150 (#265) | Planner to route | Test-only fixes: R04/R05 exact mapping, R07 subsidiary-to-GL reconciliation, R12 fingerprint and approval staleness, R18 Tenant lifecycle read-back, R20/A5 real concurrency and version conflict. |
| 3 | Decide whether MESP-141 needs further slices before it closes | Opus 5.5 | M40 exit criteria: completed reconciliation, accepted exceptions, named approvals, a readiness snapshot. |
| 4 | MESP-151 (#266): prove one Golden Release-1 end-to-end business cycle | Luna 6 / xhigh | Setup → Master Data → Supplier → Purchase → Goods Receipt → Inventory → Customer/B2B Sale → Receivable → Payment/Cash → Payable/Settlement → GL → reconciliation/reporting. |
| 5 | MESP-152 (#267): stable functional/API baseline | Luna 6 / xhigh | The UI work must preserve this baseline. |
| 6 | MESP-153 (#268): total UI/UX modernization | Luna 6 / xhigh | No hard-coded Wafra behavior. Branding is configuration (ADR-019). |
| 7 | MESP-142 (#230): Release 1 stabilization, regression, performance, UAT and release candidate | — | **Not Activated.** It needs positive activation authority. |

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

## Epics pending closure review (owner decision Q-K)

Every child of these epics is Done. Each one carries a review comment. **Only the owner closes them.**

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

MESP-8 (#97) Inventory is already Done.

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
