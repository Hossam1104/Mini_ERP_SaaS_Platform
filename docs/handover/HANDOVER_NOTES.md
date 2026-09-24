# HANDOVER_NOTES.md

## 1. Purpose

[CONFIRMED] These notes preserve project execution conventions, coding constraints, executor prompt structure, governance roles, and recurring failure patterns established during the Mini ERP project.

[CONFIRMED] No credentials, connection-string values, passwords, tokens, API keys, or secrets are included.

---

# 2. Repository and Authority Rules

[CONFIRMED] Canonical repository is `Hossam1104/Mini_ERP_SaaS_Platform`.

[CONFIRMED] GitHub repository plus GitHub Issues/Project are authoritative for current SCM/tracker state.

[CONFIRMED] Jira is historical migration provenance only and must not be mutated for current project execution.

[CONFIRMED] Current-state truth is reconciled against live Git/repository/tracker evidence before issuing substantial new work.

[CONFIRMED] Historical overlays in architecture/decision/statistics documents may be preserved as provenance and are not automatically current authority.

[CONFIRMED] Current-state documents used repeatedly are `.ai/CURRENT_STATE.md`, `TASK.md`, and `docs/staticts.md`, with live GitHub state taking precedence for mutable branch/PR/issue facts.

---

# 3. Product Constraints Everyone Is Expected to Preserve

[CONFIRMED] Wafra is the first production/validation Tenant, not a product fork.

[CONFIRMED] Do not hard-code Wafra workflows, schemas, accounting, permissions, reports, migration logic, or reusable UX.

[CONFIRMED] Hierarchy is `Platform → Tenant → Company / Legal Entity → Branch → Warehouse`.

[CONFIRMED] Tenant is not Company.

[CONFIRMED] Release 1 is B2B ERP; Retail POS/cashier workflows are outside this product.

[CONFIRMED] Arabic and English are first-class; RTL/LTR must represent identical business behavior.

[CONFIRMED] Multi-Tenant isolation is fail-closed and server-authoritative.

[CONFIRMED] Finance owns accounting, monetary evidence, FX, rounding, posting rules, journals, AP/AR/Cash/Bank accounting, settlement and revaluation.

[CONFIRMED] Inventory owns stock movement and valuation.

[CONFIRMED] Master Data owns reference/master truth.

[CONFIRMED] Migration orchestrates and records migration evidence; it must not duplicate Finance/Inventory/Master Data algorithms.

[CONFIRMED] M27 owns actual Tenant activation.

---

# 4. Coding Conventions

[CONFIRMED] Backend uses the four-project topology: Contracts, App, Infrastructure, Api.

[CONFIRMED] Allowed direction is Api → Infrastructure → App → Contracts, with Api also referencing App/Contracts for host composition.

[CONFIRMED] App must not reference Infrastructure or EF Core.

[CONFIRMED] Infrastructure must not reference Api.

[CONFIRMED] Direct cross-module DbContext/table access is prohibited.

[CONFIRMED] Public transport/application boundary contracts are typed; anonymous public API responses are rejected for stable business operations.

[CONFIRMED] Endpoint handlers stay thin; business invariants belong in application/domain behavior.

[CONFIRMED] EF/provider code belongs in Infrastructure.

[CONFIRMED] Module-owned persistence stays module-owned even though Infrastructure is one shared project.

[CONFIRMED] EF migrations are additive; accepted historical migrations are not edited to implement later behavior.

[CONFIRMED] Money, quantities, rates and tax/accounting values use exact decimal semantics.

[CONFIRMED] High-risk writes use optimistic concurrency/idempotency/Serializable transaction patterns where the owning capability already established them.

[CONFIRMED] Historical evidence is preserved rather than silently re-resolved from current configuration where replay/reconciliation depends on original meaning.

[CONFIRMED] No balancing Journal, synthetic stock adjustment or silent tolerance may conceal an accounting/stock mapping defect.

[CONFIRMED] Comments/code should remain human-readable; the standing architecture recovery rule is to preserve behavior while improving readability/SOLID/OOP when authorized, not to perform unrelated cleanup during feature work.

[CONFIRMED] No debug/temp/dead code should be left in accepted implementation.

---

# 5. Naming / Structural Rules

[CONFIRMED] Backend namespaces follow `MiniErp.<Project>.Modules.<Domain>` or the existing project-specific equivalent.

[CONFIRMED] Infrastructure module persistence lives under `MiniErp.Infrastructure/Persistence/Modules/<Domain>`.

[CONFIRMED] Public module contracts live under `MiniErp.Contracts/Modules/<Domain>` where applicable.

[CONFIRMED] API base path is `/api/v1`.

[CONFIRMED] Public REST operations have stable operation IDs/catalogue/OpenAPI metadata under the existing Foundation contract.

[CONFIRMED] Source contracts use explicit versioned names where historical cross-module evidence matters, for example `inventory-valuation-finance.v1`, `migration-ar-opening.v1`, `migration-ap-opening.v1`, and `migration-cash-bank-opening.v1`.

[CONFIRMED] Migration economic execution fingerprint remains `migration-economic-execution-v2`.

[CONFIRMED] Error codes are stable machine-readable codes; safe messages must not leak foreign Tenant/resource existence or provider internals.

[CONFIRMED] Tenant-specific branding/host behavior is configuration-led, not named-customer branching.

---

# 6. Test Conventions

[CONFIRMED] Backend Release build treats warnings as errors.

[CONFIRMED] The consolidated backend test project is `MiniErp.ArchitectureTests`.

[CONFIRMED] Provider-specific SQL Server economic/race/persistence tests use the disposable LocalDB safety harness.

[CONFIRMED] The destructive SQL test target must be a `MiniErpFoundation_*` database on `MSSQLLocalDB`.

[CONFIRMED] `MESP_SQLSERVER_CONNECTION_STRING` is the persistent Development runtime configuration and must never be used by the destructive safety harness.

[CONFIRMED] `MESP_SQLSERVER_SAFETY_CONNECTION_STRING` is the transient disposable safety configuration.

[CONFIRMED] The official local full backend runner is `scripts/Test-MiniErpBackend.ps1`; `scripts/validate-foundation.ps1` is also an established full validation entry point.

[CONFIRMED] A missing/unsafe LocalDB configuration is a failed validation, not a silently skipped green result.

[CONFIRMED] Hosted GitHub Backend CI excludes the SQL Server LocalDB safety subset, so high-risk persistence changes normally require local disposable-LocalDB evidence in addition to hosted CI.

[CONFIRMED] Frontend CI runs Angular tests, production build, Chromium Playwright and npm audits.

[CONFIRMED] Hosted Playwright is mocked browser/API testing; it is not evidence of a real backend/DB journey.

[CONFIRMED] Real provider/browser journeys have been run locally for selected acceptance slices and are reported separately from hosted CI.

[CONFIRMED] `git diff --check` is a standard final gate.

[CONFIRMED] EF pending-model checks are required when persistence changes; the recent governed baseline checks eight contexts.

---

# 7. Git / PR / Lifecycle Conventions

[CONFIRMED] Feature work is performed on a bounded feature branch from a verified exact `main`.

[CONFIRMED] Substantial implementation is normally published as a Draft PR first.

[CONFIRMED] Draft PR remains unmerged until independent Sol acceptance authorizes the Ready/merge lifecycle step.

[CONFIRMED] Force-push/history rewrite is not part of normal project execution.

[CONFIRMED] Branch protection/checks must not be bypassed merely to complete a prompt.

[CONFIRMED] Exact candidate SHA is recorded and hosted CI must be tied to that exact SHA for acceptance.

[CONFIRMED] Merge/lifecycle closure is treated as a separate governed state transition from implementation candidate completion.

[CONFIRMED] After merge, local `main` and `origin/main` are reconciled and exact-main validation/CI is checked before declaring lifecycle closed.

[CONFIRMED] Documentation/state reconciliation may be required after a feature lifecycle closes.

---

# 8. Architect / Executor Role Model

[CONFIRMED] Persistent GPT-5.6 High in Chat (“Sol”) is the Planner, Architect, Project-State Authority, Requirements/Business-Knowledge Authority, Acceptance Authority, Governance Authority, Model Router and Continuation Authority.

[CONFIRMED] Sol performs 0% normal implementation.

[CONFIRMED] GPT-6 Luna Reasoning xHigh in Codex is the default substantial executor.

[CONFIRMED] Claude Sonnet 5 HIGH is reserved for difficult recovery/surgical finalization and bounded bug-fix work.

[CONFIRMED] Claude Opus 5.5 HIGH is the protected independent critical-review lane and may be used earlier for material risk.

[CONFIRMED] GPT-6 Sol in Codex is emergency-only direct implementation.

[CONFIRMED] Gemini 3.8 Flash HIGH is auxiliary low-risk/mechanical.

[CONFIRMED] GPT-5.6 Terra is retired and must not be routed future work.

---

# 9. Executor Prompt Governance

[CONFIRMED] A standalone lowercase `p` from the owner is required before every executor/reviewer contract.

[CONFIRMED] A premature `p` sent before the prior executor result is reviewed is not banked for the next prompt.

[CONFIRMED] One contract equals one fresh executor/reviewer session.

[CONFIRMED] Executor/reviewer results return to persistent Sol for independent review before another contract is released.

[CONFIRMED] Each Sol conversation has a hard maximum of 10 executor/reviewer prompts; after Prompt 10 returns, project state is reconciled and transferred to a new Sol conversation rather than issuing Prompt 11.

[CONFIRMED] The standard executor prompt structure is:

```text
ROLE
→ TASK
→ CONTEXT
→ EXECUTION LOGIC
→ STOP CONDITIONS
→ OUTPUT CONTRACT
```

[CONFIRMED] Current prompts usually also include explicit project identity, exact branch/base/head, business authority, acceptance matrix, validation gates, Git/PR authority, metric rules, and final recommendation text.

[CONFIRMED] Executor prompts are self-contained so a fresh session does not depend on chat history.

[CONFIRMED] Prompt delivery convention is: state Model + Reasoning/Effort + routing reason + “open a new session”, then provide exactly one fenced Markdown prompt and nothing after the closing fence.

---

# 10. Standard Executor Authority Boundaries

[CONFIRMED] Executors may inspect the repository, run tests, implement bounded authorized scope, create branches/commits, push normally, create Draft PRs, and post tracker evidence when the contract explicitly authorizes those actions.

[CONFIRMED] Executors do not infer authority to merge, mark Ready, close parent issues, activate later work, change Production, change metrics, mutate Jira, force-push, delete unknown owner files, or clean unrelated repository state.

[CONFIRMED] Production actions, destructive recovery, branch-protection bypass, unknown file ownership and secrets remain explicit stop boundaries.

[CONFIRMED] `frontend/assets` contains owner-managed assets and must not be deleted/renamed/replaced/regenerated/optimized/recolored/moved/restored without explicit authorization.

[CONFIRMED] Existing unrelated stashes are left untouched unless ownership and relevance are deterministically established.

---

# 11. Tooling Convention

[CONFIRMED] Standing tooling rule is `DETECT → VALIDATE → REUSE`.

[CONFIRMED] Before introducing a new framework/abstraction/policy utility, executors search for existing repository patterns that already own the concern.

[CONFIRMED] Existing approval/SoD, idempotency, Tenant authorization, concurrency, audit, monetary, and persistence patterns are reused when structurally applicable.

[CONFIRMED] Tooling convenience is never authority; repository/business contracts remain authoritative.

---

# 12. Architecture Quality Guard

[CONFIRMED] Preserve dependency direction and module ownership during every slice.

[CONFIRMED] Avoid duplicated business authority.

[CONFIRMED] Avoid broad unrelated refactoring during bounded feature implementation.

[CONFIRMED] `MigrationExecutionService` is an architecture complexity watch item; new Migration concerns should not automatically be placed into that service if a cohesive collaborator is required.

[CONFIRMED] File/class line count alone is not authority to refactor.

[CONFIRMED] Existing architecture/static/build/test gates are part of the acceptance evidence.

[CONFIRMED] Substantial executor reports include an architecture/code-quality delta.

---

# 13. Current Project Execution State to Remember

[CONFIRMED] MESP-141 Slices 1–10 are closed.

[CONFIRMED] Slice 11 is activated on `feat/mesp-141-reconciliation-handover-evidence`.

[CONFIRMED] Slice 11 currently targets Migration Reconciliation, Approval and Ready-for-Handover.

[CONFIRMED] The initial Slice 11 prototype was removed and no implementation was committed at the last verified stop.

[CONFIRMED] The first reported blocker — missing AR/AP/Cash/Inventory-to-GL control mapping authority — was rejected by Sol because accepted Slice 9/10 already persists exact historical Finance mapping/economic evidence including `ControlAccountId`, PostingRule ID/version, functional amount, source contract/event and owner-source evidence.

[CONFIRMED] Slice 11 reconciliation must reuse that persisted historical owner evidence and must not infer today's current Finance rule.

[CONFIRMED] The second reported blocker — unresolved M40-DEC-006 approval quorum — was rejected as an implementation blocker because M40 explicitly says the decision blocks unbounded/Production-affecting scope but not a bounded decision-neutral implementation slice.

[CONFIRMED] Slice 11 must therefore fail closed when no policy is configured and may use an explicit configured test policy to prove generic approval/SoD/readiness behavior without selecting the Production quorum.

---

# 14. Recurring Executor Failure Patterns Observed

[CONFIRMED] Executors sometimes stop too conservatively when an unresolved Production decision is present even though the governing BRD explicitly permits a bounded decision-neutral implementation.

[CONFIRMED] Executors sometimes overlook already-persisted owner evidence and conclude that a new cross-module owner interface is required; direct inspection of historical representation/posting evidence must precede that conclusion.

[CONFIRMED] A prior Slice 10 acceptance report claimed complete P12/P23 proof while direct tests omitted Finance OpenItems from the before/after count tuple and did not assert Reporting Currency evidence/status through the actual public read path.

[CONFIRMED] The correction pattern for that failure was to require direct executable evidence, not rely on adjacent tests or schema presence.

[CONFIRMED] Executor outputs may report PASS while a precise requested oracle is only indirectly covered; Sol independently checks exact test assertions and public paths.

[CONFIRMED] Documentation/current-state files can become stale immediately after a merge or tracker transition; live Git/PR/Issue state must be checked before trusting static state text.

[CONFIRMED] A malformed local test invocation once created an untracked generated directory whose name contained CLI filter/logger text; cleanup was only allowed after exact-path inspection proved it was generated output and contained no owner source/config/evidence.

[CONFIRMED] Executors may over-run expensive full suites even when an exact accepted candidate and unchanged base make targeted integration validation sufficient; later contracts explicitly distinguish reused evidence from newly required final-main evidence.

[CONFIRMED] Executors may under-run provider evidence by accepting hosted CI even though hosted Backend excludes LocalDB; SQL/economic changes require the local disposable provider gate where specified.

[CONFIRMED] Executor reports sometimes leave state-document reconciliation in a separate Draft PR after the primary feature lifecycle; this is treated as governance handoff, not product failure, but it must eventually land so static current-state files do not drift.

---

# 15. “Everyone Knows” Context That Must Be Explicit for a New Architect

[CONFIRMED] The headline `24/26 = 92.3%` is fast-track capability completion, not Production readiness.

[CONFIRMED] Approximately `47%` overall and `41%` Procurement/P2P are separate conservative Production-readiness/domain-readiness indicators and were intentionally not increased for every technical slice.

[CONFIRMED] The owner intends to start product/manual testing before 100% Production readiness; 100% is a release-readiness target, not a QA-entry prerequisite.

[CONFIRMED] The owner also intends to totally rework the UI, but only after a stable functional point and one Golden Release-1 business cycle.

[CONFIRMED] Agreed sequence is MESP-141 complete → Golden Cycle → stable functional/API baseline → total UI/UX modernization → MESP-142 stabilization/UAT.

[CONFIRMED] Do not invest heavily in pixel-perfect/current-UI visual automation immediately before the planned total UI rework.

[CONFIRMED] The eventual UI modernization is expected to preserve accepted business behavior rather than redefine accounting/workflow semantics implicitly.

[CONFIRMED] Production/CD/cutover/security-volume gates remain distinct from development acceptance and must not be presented as completed merely because feature suites are green.

[CONFIRMED] MESP-142 is still Open / Not Activated.

[CONFIRMED] No next implementation capability starts automatically after a merge; it requires Sol review and owner `p` authorization under the current governance model.

---

# 16. Prompt Template Skeleton

[CONFIRMED] The reusable structure used for substantial implementation contracts is:

```markdown
# <PROJECT / CAPABILITY TITLE>

## SESSION MODE
FRESH EXECUTION SESSION

## ASSIGNED MODEL
<model>

## COMPUTE SETTING
<reasoning>

## SOL PROMPT COUNTER
Prompt N / 10

# 1. ROLE
<executor role and Sol authority>

# 2. PROJECT / REPOSITORY
<repo, root, tracker authority>

# 3. CURRENT VERIFIED STATE
<exact branch/base/head/PR/issues/CI>

# 4. BUSINESS / ARCHITECTURE AUTHORITY
<rules that must be preserved>

# 5. TASK / IMPLEMENTATION SCOPE
<bounded requested capability>

# 6. EXCLUSIONS
<what must not be implemented>

# 7. ACCEPTANCE MATRIX
<explicit numbered executable acceptance cases>

# 8. ARCHITECTURE / QUALITY GUARD
<ownership, dependency and complexity rules>

# 9. VALIDATION
<focused, provider, regression, build, EF, frontend, security/static, exact-head CI>

# 10. GIT / PR DELIVERY
<branch/commit/push/Draft PR; no merge unless explicitly authorized>

# 11. STOP CONDITIONS
<genuine authority/security/architecture blockers>

# 12. PASS CONDITIONS
<complete definition of pass>

# 13. OUTPUT CONTRACT
<exact report sections and evidence>
```

[CONFIRMED] The exact prompt contents vary by slice; the structure above records the recurring governance pattern rather than granting implementation authority by itself.
