# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7 and §10.

## Next executor prompt

Status: **RELEASED** (2026-09-27). The Planner launches the child through Paseo inside its own session and
the main checkout (Q-T), and passes this contract verbatim (`MODEL_ROUTING.md` §6, §10).

```markdown
# MESP-170 (#293) — MESP-141 Slice 13: source contract, lineage and validation coverage

## 1. Role and authority
- You are the **executor**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you, §1, §2 and
  §5 especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work item: MESP-170 (#293), a Task under MESP-15 (#104), for capability MESP-141 (#229). Source: the
  MESP-168 (#289) audit, `docs/audit/mesp-141-m40-traceability.md`, gap groups 1 and 2.
- You run **in the main checkout** (Q-T), not in a worktree. The Planner does not edit tracked files while
  you run.

## 2. Read order
1. `AGENTS.md`, then this prompt, then `gh issue view 293` (read-only).
2. `docs/audit/mesp-141-m40-traceability.md`: the rows for M40-REQ-006, -013, -015, -016, -018, -019,
   -024, -026 and M40-AC-006, -007, -008, -009, -010, -013, plus gap groups 1–2.
3. `docs/requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md`: those rows in §8 and §27, the §9
   domain/reference list, the rules they cite in §29, and §26 (M40-DEC-003).
4. Code. **Serena** first (`initial_instructions` once, then symbol tools). Main areas:
   - `backend/src/MiniErp.App/Modules/Migration/`: `MigrationIntakeApplication`,
     `MigrationValidationApplication`, `MigrationValidationContracts`, `MigrationExecutionService`;
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/`;
   - the public contracts of Master Data, Finance and Inventory that validation already consumes;
   - the `Migration*Tests.cs` files and the Migration SQL safety tests (including the MESP-169
     `MigrationRunSafetySqlServerTests.cs` snapshot helpers, reuse them).
- **Context7:** for EF Core or ASP.NET APIs you are unsure of. **Ponytail:** full, within `AGENTS.md`
  §1.6. If a plugin is missing, say so in one line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status` clean, on `main`, HEAD descends from `8060bab` (the PR #297 merge).
- `gh issue view 293 --json state`: OPEN.
- Anything else is a stop (§10).

## 4. Rules to preserve
- Tenant isolation, fail-closed scope checks, idempotency, the Outcome Unknown hard stop, and "Finance
  owns accounting". Modules call each other only through public contracts; no cross-module DbContext.
- Never weaken, skip or delete an existing assertion. Ratcheted allowlists only shrink.
- **Never invent a business rule.** M40-DEC-003 is open: **do not choose a customer template or file
  format**, and do not build an upload transport. "Versioned source contract" means the internal canonical
  package contract that already exists, made explicit and complete. Where the BRD leaves a behavior
  undecided, fail closed and record it.
- Migrations are additive and module-owned. Every EF context reports no pending model changes.
- Any new public REST operation meets the `AGENTS.md` §2 REST/API Definition of Done. Prefer none.

## 5. Scope
1. **Source contract and lineage (gap group 1: M40-REQ-013, -015, -016; M40-AC-006, -007, -009).**
   - Each domain batch records source owner, target owner, source set, extraction time, scope, status and
     cleansing note (REQ-013). Record owners as supplied data; do not infer an owner rule.
   - The canonical package contract is versioned per domain and states its fields, meanings, references
     and a compatibility check: an unknown or incompatible version blocks the batch before any import
     (REQ-015, AC-006, AC-007).
   - Stable source IDs are required. Per-domain duplicate business keys are counted and rejected or
     quarantined with the existing quarantine metadata, and source rows cannot supply target authority
     (IDs, posting status, approvals) (REQ-016, AC-009). Test every supported domain, not one.
2. **Validation coverage (gap group 2: M40-REQ-006, -018, -019, -024, -026; M40-AC-008, -010, -013).**
   - Every stage/attempt exposes owner, status, outcome, failure and next action (REQ-006).
   - Configuration and reference data validate before dependent openings or documents, across the §9
     configuration domains that Release 1 already implements: tax, rate, payment terms, price list,
     currency, UOM (REQ-018, -024). For a §9 domain with no owner implementation yet, record it as
     Depends-on; do not build the owner module.
   - Direct tests for: missing mandatory fields (row rejection vs batch block, AC-008); missing or inactive
     references naming the dependency, one per implemented reference type (AC-010); invalid UOM or
     conversion blocking inventory rows with no guessed conversion (AC-013); dates, amounts, scope and shape
     (REQ-026).
   - Supported master-data domains (REQ-019): prove the ones that exist. List the missing ones
     (e.g. Company/Branch/Warehouse, locations) as Depends-on with the owner item, not built here.
3. Update `docs/audit/mesp-141-m40-traceability.md` for the rows you change: set their Status, add
   file:line and test evidence, and update the counts. A row may end Met, Partial (with the exact
   remaining gap) or Depends-on (with the owner).

## 6. Out of scope
- Choosing a template/file format or an upload channel (M40-DEC-003). Building missing owner modules.
- Gap groups 3–6 (done in MESP-169, or MESP-171/172). UI. Production correction (M40-DEC-005).
- Any tracker write except the one evidence comment in §9. Jira in any form.

## 7. Acceptance matrix (Opus checks each)
| # | Criterion |
|---|---|
| A1 | Batch lineage carries all seven REQ-013 fields; a SQL test asserts them. |
| A2 | Per-domain versioned contract with a compatibility check; an unknown/incompatible version blocks before import; tests. No template format chosen. |
| A3 | Duplicate business keys per supported domain are counted and rejected/quarantined; source cannot supply target authority; tests per domain. |
| A4 | Direct tests for AC-008, AC-010 (each implemented reference type), AC-013 and REQ-026 checks, with exact outcomes. |
| A5 | REQ-006 stage/attempt fields exposed and tested. |
| A6 | Audit rows and counts updated with evidence; each row not Met states its exact gap or owner. No other row changes. |
| A7 | All gates green with 0 skipped; the backend count is 1563 plus the added tests, stated exactly. |

## 8. Gates (paste the output tail and the wall time)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false`: 0 warnings / 0 errors, the pass count, 0
   skipped, the disposable-database line and "MESP data is intact". If the Release build is locked by
   the running dev API (MSB3027/MSB3026), stop only the `MiniErp.Api` process on port 5300, record its PID,
   and let the §9 restart replace it.
2. `dotnet ef migrations has-pending-model-changes` for every context you touch: none pending.
3. `git diff --check` is clean.
4. The frontend gates are not run unless a frontend file changes; none should.

## 9. Git / PR delivery (positive authority, exactly this)
- Branch `feat/mesp-170-migration-source-contract` from `origin/main`. Self-review the staged diff, then
  make conventional commits scoped `migration`, referencing `MESP-170 (#293)`.
- Push normally and open a **Draft** PR to `main` titled
  `feat(migration): MESP-170 (#293) MESP-141 Slice 13 source contract and validation`. Its body carries the
  A1–A7 evidence.
- One evidence comment on #293.
- **After your last commit that changes code or tests**, restart the runtime from this checkout
  (`MODEL_ROUTING.md` §4.7, Q-S):
  ```powershell
  # stop only the MiniErp.Api listener on 5300 if it is running, then:
  dotnet build .\backend\MiniErp.sln --configuration Release --no-restore
  $env:MESP_DEV_AUTH_BYPASS = [Environment]::GetEnvironmentVariable('MESP_DEV_AUTH_BYPASS','User')
  .\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart -StartupTimeoutSeconds 180
  ```
  Never print a connection string or secret. Record the result and URLs in RESULT.md. Only the
  RESULT.md/TASK.md hand-back commit may follow.
- Finally `git switch main` so the checkout is left on `main`, clean.
- **NOT authorized:**
  - Ready, reviewers, approval or merge;
  - rebase, force-push or any push to `main`;
  - closing or reopening an issue, Status or Capability State changes, new issues.
- Then **STOP.**

## 10. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match.
- A required behavior needs a business rule the BRD does not give (a template format, an owner rule, a
  duplicate-key definition for a domain). Record the exact question and the options, then stop that item
  only. Finish and deliver the rest.
- The change would weaken Tenant isolation, idempotency or the Outcome Unknown stop, or would need an
  allowlist to grow.
- Any unrelated red in the gate: capture the full failure (console + TRX), classify it and stop.

## 11. Hand-back
- One `RESULT.md` entry at the top per `MODEL_ROUTING.md` §7, Status `DONE` or `STOPPED`. It holds:
  - the starting state;
  - the A1–A7 evidence with file:line;
  - the updated audit counts;
  - the gate output and wall time;
  - the restart result and URLs;
  - the PR URL;
  - deviations.
- **Exact next action:** "Opus 5.5 reviews MESP-170 (#293)."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the summaries untouched.
```

## Next-task summaries (Planner, 2026-09-27)

Accepted Executor cycles since the last Sol review: 4 (`ORCHESTRATION_STATE.yaml`; MODEL_ROUTING §2).

1. **MESP-170 (#293)**: the prompt above.
2. **MESP-171 (#294)**: Slice 14, covering the authority matrix, readiness review and reporting.
   MESP-141 closes only after Slices 12–15. Slice 15 (MESP-172, UI lane) blocks closure.
3. **MESP-151 (#266)**: the golden cycle. After it, Wave 1 (Q-Q).
4. **UI lane (Q-R)**: the owner supplied the reference UI on 2026-09-27; the Planner is preparing design
   mockups for owner approval before MESP-153 and MESP-172 start.

### Owner actions pending
- Approve the MESP-153 (#268) design mockups (Q-R).
