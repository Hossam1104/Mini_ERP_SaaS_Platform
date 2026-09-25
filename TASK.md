# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **CONSUMED** (executed under the owner's 2026-09-25 authorization).

Routing: **Luna 6, effort max.** Open a **new session**.
- The owner decided this effort on 2026-09-25. MODEL_ROUTING §1 allows max after the xhigh attempt on
  this critical task failed.
- The work is product concurrency and data-integrity fixes on the money and migration path. It is not
  a contained, already-diagnosed Sonnet fix: MESP-162 and MESP-163 still need diagnosis.

```markdown
# MESP-161 (#279), MESP-162 (#280), MESP-163 (#282) — Slice 11 product Bugs, plus R07/R04/R05 oracle corrections
Model: Luna 6 — Effort: max — Fresh session

## 1. Role and authority
- You are the **executor**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you, §1
  especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work items. All are Bugs under MESP-15 (#104), capability MESP-141 (#229):
  - product Bugs: MESP-161 (#279), MESP-162 (#280), MESP-163 (#282);
  - oracle corrections: MESP-157 (#273) R07 and MESP-156 (#272) R04/R05.
- This task **changes product code**, limited to the Migration service and persistence files in §5.6.
- Branch: `fix/mesp-156-slice11-test-oracles` (Draft PR #281), fast-forwarded to this prompt (§3).

## 2. Read order
1. `AGENTS.md`, then this prompt.
2. `RESULT.md`: the top entry (the Opus re-review, which holds the per-row verdict and the MESP-162
   hypothesis), then the Luna handoff below it (the gate history and failure traces).
3. The Bug bodies, read-only: `gh issue view 279 280 282`.
4. Code, symbol by symbol. **Serena:** call `initial_instructions` once, then use
   `get_symbols_overview` and `find_symbol`. Don't read whole files.
   - `backend/src/MiniErp.App/Modules/Migration/MigrationReconciliationService.cs`:
     - `ReconcileCoreAsync` (~57–122): replay branch :70–80, post-save audit/evidence :96–102,
       lifecycle transition :104–114;
     - `ApproveCoreAsync` (~140–195) and `CreateReadinessCoreAsync` (~201–267). The readiness path
       :238–249 already re-reads the run after a failed transition; that is the pattern to reuse;
     - `StableId` (~713) and the detail construction sites (~383, ~466, ~497, ~540);
     - `WithRunGateAsync` (~685).
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationReconciliationPersistence.cs`:
     `SaveAsync` (:14–86), `SaveApprovalAsync`, `SaveReadinessAsync`, `LockRunAsync` (:294–297).
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationPersistence.cs`:
     - `StartAttemptAsync` (:208–~330), `ReadAttemptsAsync` (:949),
       `ResolveConcurrentAttemptAsync`, `ResolveLineageDenialAsync`;
     - the existing locked-transaction pattern at :532–539.
     - `MigrationPersistence` is one `partial` class across both files, so `LockRunAsync` is callable
       from `StartAttemptAsync`.
   - `MigrationDbContext.cs` detail mapping (:471–490) and the Slice 11 migration's detail indexes
     (`Migrations/Mesp141/20260924135807_Mesp141Slice11Reconciliation.cs`, ~155–215 and ~300–320).
     Read only; you never change them.
   - Tests:
     - `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs`:
       R04 (~115–135), R05 (~140–165), R07 (~190–257), R12 (~340–415), R20 (~618–644), helper
       `ReadHistoricalFinanceMappingAsync` (~787);
     - `SqlServerSafetyTests.cs:3467`, `MESP141_sql_server_concurrent_attempt_start_on_one_run_yields_one_attempt_and_replays`;
     - `ModuleBoundaryTests.cs:340–360`, the raw-SQL call-site counts. Read only.
   - Fixture `ArSqlFixture` (`MigrationArOpeningSqlServerRemediationTests.cs:911`):
     `ReadEconomicCountsAsync` (:1509), which returns `Effects` (execution effects).
- **Context7:** only for an EF Core, SqlClient or xUnit API you are unsure of (for example
  `SqlException.Number`, or transaction behaviour).
- **Ponytail:** full. It never trims assertions, fail-closed checks, audit evidence, gate output or
  the RESULT.md entry.
- Use `git grep` or the Grep tool, never recursive `grep -r`. If a plugin is missing, say so in one
  line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status -sb`: the tree is clean and you are on `docs/mesp-150-slice11-rereview`.
- HEAD descends from `4ba9a9b`, and `git diff --name-only 4ba9a9b HEAD` lists only `TASK.md`.
- `git rev-parse origin/fix/mesp-156-slice11-test-oracles` = `d410e8d…`, and that commit is an
  ancestor of HEAD.
- `gh pr view 281 --json isDraft,state`: Draft, OPEN.
- `gh issue view 272 273 274 275 276 279 280 282 --json number,state`: all OPEN.
- Then run `git switch fix/mesp-156-slice11-test-oracles` and
  `git merge --ff-only docs/mesp-150-slice11-rereview`. Fast-forward only; any other result is a stop.
- Any mismatch: stop (§10).

## 4. Rules to preserve
- **Never weaken, skip or delete an existing assertion or test** (AGENTS §5).
  - The R12 and R20 tests stay exactly as written, and must turn green because the product is fixed.
  - If a test asserts behaviour this fix must change, stop (§10).
- **No migration, no schema, index or primary-key change, and no EF model change.** If a fix needs
  one, stop.
- **Raw SQL (R4):** add no new `ExecuteSql*` call site.
  - The counts in `ModuleBoundaryTests.cs:358–359` must stay 1 and 2.
  - Reuse `LockRunAsync`; never edit `ModuleBoundaryTests` or the allowlist.
- **Audit and evidence fail closed.** Every Success or Replayed result must still have its audit
  appended and the run evidence confirmed, exactly as the current Success/Replayed paths do. Never
  return success while evidence is unconfirmed, and never skip `AppendAuditAsync`.
  - An `Unknown` outcome stays only for genuinely unknown persistence or audit failures, never for
    "a concurrent winner is still in flight".
- **Idempotency contract (R20/A5):** identical concurrent calls (same key, same request, same
  fingerprint) return Success or Replayed for **one** persisted record.
  - A stale version still gets the exact A5 codes (`migration_run_version_conflict`,
    `migration_reconciliation_version_conflict`).
  - A changed request under the same key still gets its idempotency conflict.
- **No retry loops with delays, and no fixed sleeps,** in product or test code.
  - Convergence comes from DB locks, transactions, unique indexes and re-reading committed state.
  - Catching a specific deadlock (`SqlException` 1205) and resolving from a fresh context is allowed
    only as the existing "resolve concurrent" pattern, not as a timed retry.
- **Tenant isolation:** every read goes through a Tenant-scoped context. No `IgnoreQueryFilters`.
- **FIN-OD-01:** Migration never creates Finance journals itself. Tests never create journals to pass.
- **Module boundaries:** no new cross-module dependency or interface.
- The in-process `workflowGates` stays. Correctness must not depend on it.

## 5. Scope and file allowlist

### 5.1 MESP-161 (#279): re-reconciliation detail-ID collision
- Make every reconciliation detail `Id` unique per reconciliation. For example, derive it from the
  reconciliation's `Id` plus domain and scope, at every `StableId` call site. Keep it deterministic,
  so that building the same record twice gives the same ids.
- `ScopeKey` and all other detail fields stay unchanged. Existing persisted rows are not rewritten.
- First, `git grep` every reader of detail ids (service, persistence, contracts, API, tests). Confirm
  that nothing relies on an id being equal across reconciliation versions. If something does, stop.
- Regression: the R12 test (unchanged) must pass. Add one LocalDB test to the reconciliation test
  class. It reconciles the same run twice with a changed fingerprint (reuse R12's policy-change
  technique on a fresh fixture) and asserts all of these from a fresh `MigrationDbContext`:
  - 2 reconciliations;
  - detail row count = details(v1) + details(v2);
  - the v1 details are unchanged (same ids and values as read before the second reconcile);
  - no detail id is shared between v1 and v2.

### 5.2 MESP-162 (#280): concurrent reconcile/approve/readiness convergence
- **Diagnose first.** Record the exact code path and result for each observed rejection in RESULT.md.
  Opus's hypothesis to confirm or refute:
  - (a) a caller that replays inside `SaveAsync` still runs the lifecycle transition at Svc:104–114
    and loses the race on `TransitionRunAsync(… Reconciled, pendingRun.Version)`, getting a
    `Failure`;
  - (b) a caller that takes the replay branch at Svc:70–80 while the winner has not yet confirmed
    evidence returns `Unknown("migration_audit_recovery_required")`.
- Then check the approve and readiness paths for the same patterns, including
  `CreateReadinessCoreAsync`'s `!run.EvidenceConfirmed && !replayingReadyResult` rejection.
- **Fix** with the smallest change that satisfies §4. Directions (choose by evidence, not all of
  them):
  - after a failed transition, re-read the run and accept the target state the way readiness
    :238–249 already does;
  - route a same-key, same-fingerprint replay through the same audit-and-confirm path that a
    `SaveAsync` replay already uses, instead of returning `Unknown`;
  - serialize the critical section on the existing run lock inside persistence.
- Regression: the R20 test (unchanged) passes in both gate runs. If the diagnosis finds a path R20
  does not exercise, add one LocalDB test for it in the same class.
- **R20 is timing-dependent.** It passed once, unfixed, in Opus's governance gate run on 2026-09-25.
  A green R20 is therefore not proof on its own: RESULT.md must show, with `file:line`, that each
  diagnosed race path is closed in the code.

### 5.3 MESP-163 (#282): concurrent attempt-start deadlock
- **Diagnose first.** Record the deadlocking statements and lock order from the code, and from SQL
  Server error text if the gate reproduces the deadlock.
- **Fix:** make concurrent `StartAttemptAsync` calls on one run serialize or converge without a
  deadlock. Preferred: a transaction that takes the existing `LockRunAsync` run lock before the
  idempotency and attempts reads (the pattern at MigrationPersistence.cs:532–539). Keep every
  existing outcome code and the `ResolveConcurrentAttemptAsync`/`ResolveLineageDenialAsync` paths.
- Check that no caller of `StartAttemptAsync` already holds an ambient transaction that a new one
  would conflict with.
- Regression:
  - the existing test at `SqlServerSafetyTests.cs:3467` passes in both gate runs;
  - add one LocalDB test next to it that runs the same 8-way concurrent start on 4 separate runs at
    once (32 tasks through one `Task.WhenAll`). For each run, assert 1 attempt, 1 Succeeded and 7
    Replayed, as the existing test does.

### 5.4 MESP-157 (#273): R07 oracle correction
- Replace only the wrong count assertion added in `d410e8d` (`ownerEffectCount`, RT:~252–256). That
  assertion is your own unaccepted test code, not an accepted oracle.
- New oracle, read from a fresh `MigrationDbContext`:
  - the distinct represented `EffectId`s for the run equal `counts.Effects`;
  - every represented `EffectId` is one of the run's execution effects (`fixture.Migration.ListEffectsAsync`).
- Keep every other R07 assertion.

### 5.5 MESP-156 (#272): R04/R05 strengthening
- In `ReadHistoricalFinanceMappingAsync`, or next to its callers, assert that all Finance
  `EconomicRepresentations` rows for the effect that carry a mapping have **exactly one** distinct
  `(ControlAccountId, PostingRuleId, PostingRuleVersionNumber)`.
- Keep the existing equality assertions.

### 5.6 File allowlist
- Product:
  - `backend/src/MiniErp.App/Modules/Migration/MigrationReconciliationService.cs`;
  - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationReconciliationPersistence.cs`;
  - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationPersistence.cs`
    (attempt start and its private helpers only).
- Tests:
  - `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs`;
  - `backend/tests/MiniErp.ArchitectureTests/SqlServerSafetyTests.cs` (the one new MESP-163 test
    only);
  - `backend/tests/MiniErp.ArchitectureTests/MigrationArOpeningSqlServerRemediationTests.cs`
    (additive, read-only `ArSqlFixture` accessors only, if needed).
- Handoff: `RESULT.md` (one new top entry) and `TASK.md` (Status → CONSUMED).
- No other file.

## 6. Out of scope
- Migrations, the EF model, schema and indexes.
- `ModuleBoundaryTests` and the R4 allowlist (the owner's decision, SOL-CL-02).
- The `ponytail:` gate-eviction note (Svc:28).
- The Sol cleanup follow-ups (SOL-CL-01/04/05/06/07), governance docs and ROADMAP.
- Deciding M40-DEC-*, and Production approval policy configuration.
- Closing, reopening or changing the Status or Capability State of any issue. Jira in any form.

## 7. Acceptance matrix (what Opus will check)

| Item | Oracle | Evidence Opus expects |
|---|---|---|
| MESP-161 | Re-reconciliation persists; v1 history intact | R12 green; new test: 2 reconciliations, summed details, v1 unchanged, disjoint ids; detail-id reader audit in RESULT.md |
| MESP-162 | Identical concurrent callers converge | Diagnosis with `file:line`; R20 green in both runs; §4 audit/evidence rules intact (diff shows no skipped audit or confirm) |
| MESP-163 | No deadlock; one attempt plus replays | Diagnosis; existing test and new 4×8 test green in both runs; no new `ExecuteSql*` site |
| MESP-157 | R07 distinct effects = execution effects | Corrected assertion `file:line`; all other R07 assertions kept |
| MESP-156 | One distinct historical mapping per effect | Assertion `file:line` |
| All | No weakening; bounded change | `git diff d410e8d HEAD` shows no removed or relaxed accepted assertion, no `Skip`, only §5.6 files; ModuleBoundary counts unchanged |

Every row needs `file:line` evidence in RESULT.md.

## 8. Gates (paste the output tail and the wall time)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` from Windows PowerShell, on the final tree.
   - It must show 0 warnings, 0 errors, **all passed, 0 skipped**.
   - The total must be 1552 plus exactly the tests you added.
   - It must print the disposable-database line and the "MESP data is intact" line.
2. **Run gate 1 a second time**, sequentially, on the same unchanged tree. Both runs must be fully
   green.
3. EF: for each Infrastructure context, run
   `dotnet ef migrations has-pending-model-changes --project backend/src/MiniErp.Infrastructure --startup-project backend/src/MiniErp.Api --context <Context>`.
   Every context must report no pending changes. At minimum run `MigrationDbContext`; list the
   contexts you ran.
4. `git diff --check`: clean.
5. `git diff --name-only d410e8d HEAD`: only §5.6 files plus `RESULT.md`/`TASK.md`/`docs/ROADMAP.md`.
   `docs/ROADMAP.md` comes from the Planner commit `4ba9a9b`.
- During development you may run the suite as often as you need; the script has no filter. Never run
  two suites at once. Never point a test at a non-disposable database.
- Reused evidence, not re-run: the frontend, npm audit and Playwright gates (no frontend change).

## 9. Git / PR delivery (positive authority, exactly this)
- Work on `fix/mesp-156-slice11-test-oracles` after the §3 fast-forward. Never rebase, amend or drop
  commits.
- Commits:
  - `fix(migration): MESP-161 (#279) …`, `fix(migration): MESP-162 (#280) …`,
    `fix(migration): MESP-163 (#282) …`, `test(migration): MESP-156/157 (#272/#273) …`;
  - one per item is fine;
  - self-review the staged diff before each commit.
- Push the branch with a normal fast-forward push, which updates Draft PR #281. Edit PR #281's body to
  add the per-item evidence table and both gate results. It stays a Draft.
- **Tracker writes, only these:**
  - one comment each on #279, #280, #282, #273 and #272 with the commit, `file:line` evidence and the
    gate result;
  - if §10 product-defect applies to a new defect, create one Bug per defect per `MODEL_ROUTING.md`
    §11: label `type:bug`, Project #1, `Jira Key` = the next free MESP-n (verify live; the highest is
    currently MESP-163), `Parent / Epic` = `[MESP-15] #104`, Work Type Bug, Status Todo.
- **NOT authorized:**
  - Ready, reviewers, approval, merge, update-branch, rebase, force-push, push to `main`;
  - closing or reopening any issue, any Status or Capability State change, any other tracker write.

## 10. Stop conditions (stop, write a `STOPPED` entry, do not improvise)
- The starting state does not match, or the fast-forward is not possible.
- A fix needs a file outside §5.6, a migration, a schema/EF model change, or a new `ExecuteSql*` site.
- A fix would need to weaken audit or evidence confirmation, or an existing test asserts behaviour the
  fix must change.
- The diagnosis shows the expected contract itself is wrong or undecided (a business decision). Record
  it for MESP-23 (#112) and stop.
- A test fails on the product for a defect **outside** MESP-161/162/163:
  - keep the test as written; never weaken or skip it;
  - file the Bug (§9), commit, push, and record the red output;
  - finish the other items only if they are independent of it.
- The two gate-1 runs disagree. Record both outputs and stop; add no retries or delays.
- After the push, the PR body edit and the issue comments: STOP. No further mutation.

## 11. Hand-back
- Add one `RESULT.md` entry at the top, directly under the file preamble, using the
  `MODEL_ROUTING.md` §7 template, with Status `DONE` or `STOPPED`. It contains:
  - the starting-state output and the fast-forward result;
  - the diagnosis for MESP-162 and MESP-163, with `file:line`, and whether Opus's hypothesis held;
  - the MESP-161 detail-id reader audit;
  - the §7 matrix with `file:line` evidence;
  - both gate-1 outputs with wall times, the EF results and `git diff --check`;
  - any deviations, and every failure with its classification.
- **Exact next action:** "Opus 5.5 re-reviews Slice 11 under MESP-150 (#265)."
- In `TASK.md`, set this prompt's Status to **CONSUMED**. Leave the next-task summaries untouched.
- Commit and deliver per §9. Then stop.
```

## Next-task summaries (Planner, 2026-09-25)

### 1. Slice 11 product Bugs MESP-161/162/163 and the R07 oracle correction

The prompt is above (Luna 6 / max). After it: Opus re-reviews Slice 11 under MESP-150 (#265). If
Slice 11 is accepted, the Sol 6 review of Slice 11 follows (critical point 2).

### 2. Cleanup follow-ups: governance text and BRD byte restore (SOL-CL-05, -06, -07)

| | |
|---|---|
| Model / effort | **Opus 5.5** (governance docs are Planner-owned), then the backend suite, because the architecture tests read `AGENTS.md`. |
| Work item | MESP-149 (#264) |
| Scope | `AGENTS.md` §1.4: restore "stopped, **completed** or handed off". `AGENTS.md` §1.6 and `MODEL_ROUTING.md` §5: Ponytail never weakens **authorization, data-loss safeguards or accessibility**, in addition to the current list. `docs/requirements/16_Master_Data_and_Product_Catalog_BRD.md`: restore the tag blob `2a5febc` byte for byte. |
| Out of scope | Any other rule change. No rule widens. |
| Acceptance | `git diff` shows only these lines. `git rev-parse HEAD:docs/requirements/16_Master_Data_and_Product_Catalog_BRD.md` = `2a5febc…`. Backend suite 1551/1551 plus any tests added by then. |

### 3. Test hardening: R4 SQL shape and the D-18 AP/cash-bank tests (SOL-CL-01, SOL-CL-04)

| | |
|---|---|
| Model / effort | **Luna 6 / xhigh** |
| Work item | A new Task under MESP-145 (#263), created when the prompt is written |
| Scope | `ModuleBoundaryTests.Allow_listed_raw_sql_is_tenant_scoped_and_lock_only`: pin the complete statement shape (one `SELECT`, the `WITH (UPDLOCK, HOLDLOCK)` hint, a `WHERE [TenantId] = {…} AND [<key>] = {…}` predicate, no `;` and no second statement). Add a negative self-check proving that a multi-statement string is rejected. `MigrationExecutionTests`: add AP and cash-bank fault → `partial` and cancellation → throw tests, mirroring the AR test. |
| Out of scope | The R4 allowlist itself; it waits for the owner's decision on the three Slice 11 sites. Product code. |
| Acceptance | Exact assertions. The negative check fails the old shape check and passes the new one. Backend suite green with 0 skipped. |

### Owner decisions pending
- Ratify or remove the three Slice 11 R4 lock sites (MESP-150 A3 = SOL-CL-02).
- Draft PRs #277 and #278, and closing MESP-149 (#264).
