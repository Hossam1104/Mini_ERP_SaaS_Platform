# ROADMAP.md

## 1. Authority and Current Snapshot

[CONFIRMED] GitHub repository `Hossam1104/Mini_ERP_SaaS_Platform` and GitHub Issues/Project are the current SCM/tracker authority.

[CONFIRMED] Jira is historical migration provenance only and is not to be mutated for current project execution.

[UNVERIFIED] No Azure DevOps work-item IDs are established in the current Mini ERP authoritative tracker evidence reviewed for this handover; known work identifiers are `MESP-*` and GitHub Issue/PR numbers.

[CONFIRMED] Last verified merged `main` before the active Slice 11 implementation is `13ede0af8234c3dc1d58272532f27258f1c87ed6`.

[CONFIRMED] Accepted fast-track capability completion remains `24/26 = 92.3%`.

[CONFIRMED] The separate conservative readiness indicators remain approximately `47%` overall Production readiness and `41%` Procurement/P2P readiness.

[CONFIRMED] Those readiness percentages are not the same metric as feature/capability completion.

---

# 2. Completed Phases / Capabilities

[CONFIRMED] Foundation architecture and safety work established the Modular Monolith, Tenant isolation, Identity/session/authorization seams, REST/OpenAPI contract, audit/observability evidence, durable-work/outbox boundaries, private-file boundary, and disposable SQL safety harness.

[CONFIRMED] Core Master Data capabilities were implemented through Category/UOM, Product identity/catalog, Currency/Payment Term, Tax, Exchange Rate, and Price List slices.

[CONFIRMED] Business Parties Supplier and B2B Customer capabilities were implemented.

[CONFIRMED] Procurement capabilities progressed through Purchase Request, Supplier Quotation/comparison/source decision, Purchase Order/Supplier Confirmation, Goods Receipt/Purchase Invoice handoff, three-way matching, and Supplier Return.

[CONFIRMED] Inventory capabilities progressed through stock ledger, receipt/return/transfer flows, stock control, moving-weighted-average valuation, and Finance valuation handoff.

[CONFIRMED] Finance foundation and later Finance capabilities were implemented through GL/COA/periods/posting rules, AP/AR/Cash settlement, multi-currency/FX, close/corrections/reconciliation/core-report behavior, and related evidence.

[CONFIRMED] B2B Sales capabilities were implemented through quotation/order/credit-control scope and later reservation/fulfillment/Delivery/invoice-eligibility and Customer Return slices.

[CONFIRMED] Reporting capability exists in the repository and has accepted implementation history before the current Migration program.

[CONFIRMED] ADR-019 Tenant-host resolution, Overview-first entry, operational Company/Branch context, configuration-led branding, and SAR presentation was implemented and merged.

---

# 3. MESP-141 — Release 1 Migration / Repeatable Tenant Onboarding

[CONFIRMED] GitHub Issue #229 is `[MESP-141] Implement Release 1 migration and repeatable Tenant onboarding`.

[CONFIRMED] Issue #229 remains Open / Active.

[CONFIRMED] MESP-141 Slices 1 through 10 are accepted, merged, and lifecycle-closed.

[CONFIRMED] Slice 10 — Multi-Currency Opening & FX Evidence — merged through PR #260.

[CONFIRMED] Slice 10 accepted feature head was `8b46a819806bd51c8ac0333f40cb7c6320d2e99e`.

[CONFIRMED] Slice 10 final merged-main commit was `54d7bc0456ecb7c3732365acbdf14b451c33dc97`.

[CONFIRMED] Documentation/state reconciliation PR #261 merged at `13ede0af8234c3dc1d58272532f27258f1c87ed6`.

[CONFIRMED] Slice 10 is `ACCEPTED / MERGED / FINAL-MAIN VERIFIED / LIFECYCLE CLOSED`.

---

# 4. Current Phase

[CONFIRMED] Current phase is `MESP-141 Slice 11 — Migration Reconciliation, Approval and Ready-for-Handover`.

[CONFIRMED] Slice 11 was activated on Issue #229.

[CONFIRMED] Current Slice 11 branch is `feat/mesp-141-reconciliation-handover-evidence`.

[CONFIRMED] Slice 11 base is `13ede0af8234c3dc1d58272532f27258f1c87ed6`.

[CONFIRMED] The first Slice 11 executor stopped before committing implementation and removed its prototype.

[CONFIRMED] No Slice 11 Draft PR existed at the last verified handover point.

[CONFIRMED] Sol subsequently determined the two reported blockers did not block bounded implementation.

[CONFIRMED] Cross-ledger reconciliation may reuse persisted exact Finance mapping/economic evidence already present in `MigrationEconomicRepresentationRecord`, GL reconciliation records and historical posting-rule/journal evidence.

[CONFIRMED] M40-DEC-006 remains unresolved for Production approval quorum/SoD exceptions, but the BRD explicitly classifies it as a B decision that does not block a bounded decision-neutral implementation slice.

[CONFIRMED] Slice 11 is therefore to use a versioned configuration-led approval-policy seam that fails closed when no effective policy is configured and may use explicit test policies for acceptance evidence without deciding the Production quorum.

---

# 5. Active Slice 11 Required Work

[CONFIRMED] Implement durable Migration reconciliation aggregation.

[CONFIRMED] Reconcile submitted-scope row outcomes: accepted, rejected, duplicate, skipped, quarantined and unresolved categories as applicable.

[CONFIRMED] Reconcile GL debit/credit/net/trial-balance evidence.

[CONFIRMED] Reconcile Inventory quantity/value evidence while leaving valuation authority in Inventory.

[CONFIRMED] Reconcile AR subsidiary opening evidence to the corresponding GL control representation using persisted exact historical Finance evidence.

[CONFIRMED] Reconcile AP subsidiary opening evidence to the corresponding GL control representation using persisted exact historical Finance evidence.

[CONFIRMED] Reconcile Cash/Bank opening evidence to corresponding GL control evidence using persisted exact historical Finance evidence.

[CONFIRMED] Preserve one economic opening effect when subsidiary detail and full Trial Balance/control views coexist.

[CONFIRMED] Material/unexplained mismatch is a blocking reconciliation failure; reconciliation must not create balancing Journals or stock adjustments.

[CONFIRMED] Implement durable/version-bound approval evidence and SoD behavior through a configuration-led policy seam.

[CONFIRMED] No configured approval policy must fail closed and block Ready-for-Handover.

[CONFIRMED] Acceptance tests may configure a test policy to prove independent reviewer, required approvals, SoD, version binding, missing-approval blocking, concurrency, and successful Ready-for-Handover.

[CONFIRMED] Implement stale reconciliation/approval invalidation when underlying authoritative economic evidence changes.

[CONFIRMED] `Outcome Unknown` must block Ready-for-Handover.

[CONFIRMED] Partial domain completion must remain visible and block false whole-run Ready-for-Handover.

[CONFIRMED] Ready-for-Handover must not activate the Tenant; M27 remains separate.

[CONFIRMED] Tenant A must not be able to read/reconcile/approve Tenant B evidence.

[CONFIRMED] Slice 11 acceptance matrix is R01–R20 as recorded in the active execution contract.

---

# 6. Remaining Work After Slice 11

[CONFIRMED] MESP-141 must still be evaluated after Slice 11 to determine whether any implementation slice remains mandatory before MESP-141 can close.

[CONFIRMED] M40 onboarding exit requires completed reconciliation, accepted exceptions/documented decisions, named approvals, and a readiness snapshot.

[CONFIRMED] Handover creates eligibility evidence only; actual Tenant Active transition remains M27-owned.

[CONFIRMED] Production migration/cutover is separately governed and is not authorized by completion of generic MESP-141 implementation.

[CONFIRMED] M40 open decisions remain explicit; unresolved Production decisions are not silently defaulted.

---

# 7. Agreed Sequence to the UI Rework Baseline

[CONFIRMED] The owner-approved sequence is:

`Finish MESP-141`
→ `Prove one Golden Release-1 end-to-end business cycle`
→ `Create a stable functional/API baseline`
→ `Total UI/UX modernization`
→ `MESP-142 stabilization / regression / UAT / release hardening`

[CONFIRMED] The total UI redesign is intentionally after a stable functional baseline and before final MESP-142 stabilization/UAT.

[CONFIRMED] The UI redesign must not start by hard-coding Wafra-specific behavior.

[CONFIRMED] Business/API behavior is to be treated as the baseline to preserve during the later UI redesign except where a genuine defect or separately authorized requirement changes it.

---

# 8. Golden Release-1 Business Cycle

[CONFIRMED] The intended Golden Cycle boundary discussed and accepted for the stable baseline is:

`Tenant / Company / Branch setup`
→ `required Master Data`
→ `Supplier`
→ `Purchase`
→ `Goods Receipt`
→ `Inventory`
→ `Customer / B2B Sale`
→ `Receivable`
→ `Payment / Cash`
→ `Payable / Settlement`
→ `Accounting / GL`
→ `reconciliation / reporting evidence`

[CONFIRMED] Golden E2E had not started at the last verified MESP-141 handover.

---

# 9. MESP-142

[CONFIRMED] GitHub Issue #230 is `[MESP-142] Release 1 end-to-end stabilization, regression, performance, UAT, and release-candidate readiness`.

[CONFIRMED] Issue #230 remains Open / `not-activated`.

[CONFIRMED] MESP-142 must not be started until explicitly activated.

[CONFIRMED] The agreed project order places total UI/UX modernization before MESP-142 final stabilization/UAT.

---

# 10. Production / External Gates

[CONFIRMED] MESP-48 remains an open Production volume/capacity/performance governance gate.

[CONFIRMED] MESP-50 remains an open Production security/data-governance/retention/residency/backup/purge gate.

[CONFIRMED] Production deployment/CD is not implemented.

[CONFIRMED] Production migration/cutover is not authorized by current Migration development completion.

[CONFIRMED] Production hosting topology, RPO/RTO, secret/key management, retention/residency/legal hold/purge, RLS adoption/deferral and Saudi external e-invoicing remain separately governed decisions where applicable.

---

# 11. Known Defects / Non-Blocking Findings

[CONFIRMED] Current frontend production build has an existing initial-bundle warning around `514.26 kB`, approximately `14.27 kB` above the configured `500 kB` budget.

[CONFIRMED] Current npm audit baseline reported moderate findings only at the last verified Slice 10 integration evidence: four moderate production advisories and seven moderate overall, with zero high/critical.

[CONFIRMED] Hosted Backend CI intentionally excludes the SQL Server LocalDB safety subset.

[CONFIRMED] ADR-019 carries four non-blocking follow-ups: cross-host session continuity before Production multi-host cutover; duplicate active membership invariant; one OpenAPI summary-quality item; canonical-host ambiguity/single-canonical-host constraint.

[CONFIRMED] Historical MESP-124 P3 observations include implicit approval-stage empty-eligible semantics, minor supplier-change predicate asymmetry, some generic HTTP error classification, per-user-action frontend idempotency-key behavior, retained audit snapshot privacy governance, Development auth-bypass neutralization in tests, source-decision consumption after cancelled/rejected PO, and historical lockfile security maintenance; their present closure state is not reverified here.

[UNVERIFIED] No additional current P0/P1 product defect is established from the repository evidence reviewed for this handover.

---

# 12. Known Deviations From Earlier Plan

[CONFIRMED] The original three-project backend wording was replaced by the four-project topology including `MiniErp.Infrastructure`.

[CONFIRMED] Docker Compose/Testcontainers was part of the early architecture direction but the current developer validation strategy uses SQL Server LocalDB because Docker was unavailable on the developer machine; Docker/Testcontainers CI remains separately deferred.

[CONFIRMED] Hosted CI does not run the full LocalDB safety suite even though the local acceptance strategy does.

[CONFIRMED] The project tracker moved from Jira to GitHub Issues/Project; Jira is now historical provenance only.

[CONFIRMED] The initially temporary Tenant/workspace chooser flow was superseded by ADR-019's Tenant-host/Overview-first operational-context model.

[CONFIRMED] The total UI/UX modernization phase was explicitly inserted between the stable functional/Golden-Cycle baseline and MESP-142 final stabilization/UAT.

---

# 13. Open Questions / Explicitly Unresolved Decisions

[CONFIRMED] M40-DEC-001 through M40-DEC-006 remain open unless separately resolved after the last verified state.

[CONFIRMED] M40-DEC-006: exact Production approval quorum and SoD exceptions for migration/reconciliation/handover remain unresolved.

[CONFIRMED] Production Tenant-specific source systems/extract owners/opening date remain per-onboarding decisions under M40.

[CONFIRMED] Production migration transport/volume envelope remains gated by Product/Architecture/Operations and MESP-48 evidence.

[CONFIRMED] Production sensitive-field classification/export/support policy remains governed by Security/Privacy/Tenant decisions and MESP-50-related controls.

[CONFIRMED] Production recovery/correction authority after cutover effects remains separately governed.

[CONFIRMED] Production host/TLS/cross-host SSO/custom-domain operations remain future Production concerns under ADR-019.

[CONFIRMED] SQL Server RLS adoption or formal deferral remains unresolved before Production security approval.

[CONFIRMED] Production object-storage provider/region/scanning/retention/purge is unresolved.

[CONFIRMED] Production worker hosting/provider/retention topology is unresolved.

[CONFIRMED] Production deployment/CD topology is unresolved.
