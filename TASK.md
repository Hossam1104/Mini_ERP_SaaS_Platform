# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **OPEN**

Routing: **Claude Sonnet 5, effort high.** Open a **new session**.
- It is a contained, already-diagnosed product regression: one condition in one method, plus one
  regression test (MODEL_ROUTING §1 and §3).
- Effort is high, not medium, because it touches the migration reconciliation lifecycle on the money
  path.

```markdown
# MESP-164 (#283) — Stale reconcile replay after Ready-for-Handover returns Unknown
Model: Claude Sonnet 5 — Effort: high — Fresh session

## 1. Role and authority
- You are the **bug fixer**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you, §1
  especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work item: MESP-164 (#283), a Bug under MESP-15 (#104), capability MESP-141 (#229). It is a
  regression introduced by the MESP-162 (#280) fix, commit `4a9b67c`.
- Branch: `fix/mesp-156-slice11-test-oracles` (Draft PR #281), fast-forwarded to this prompt (§3).

## 2. Read order
1. `AGENTS.md`, then this prompt.
2. `RESULT.md`: the top entry only (the Opus review that found this defect).
3. `gh issue view 283`, read-only.
4. Code. **Serena:** call `initial_instructions` once, then `find_symbol`; don't read whole files.
   - `backend/src/MiniErp.App/Modules/Migration/MigrationReconciliationService.cs`,
     `ReconcileCoreAsync` (~57–130): the stale-version replay branch (:71–80), the common
     audit/evidence path (:98–105), and the clean-record lifecycle check (:107–124). The defect is the
     `else if` at :122–123.
   - The run lifecycle map in `MigrationApplicationContracts.cs` (~250–280): `Reconciled →
     ReadyForHandover → Closed`.
   - Tests: `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs`,
     R17 (:562) and R18 (:585) for the reconcile → approve → readiness setup, and the `ReconcileAsync`
     helper (:747). The helper reads the *current* run version, so your stale replay must build its
     `MigrationReconcileRequest` directly with the run version captured before the first reconcile.
- **Context7:** not expected. **Ponytail:** full; it never trims assertions, audit, gate output or the
  RESULT.md entry. If a plugin is missing, say so in one line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status -sb`: the tree is clean and you are on `docs/mesp-150-slice11-final-review`.
- HEAD descends from `496ca47`, and `git diff --name-only 496ca47 HEAD` lists only `AGENTS.md`,
  `RESULT.md`, `TASK.md`, `docs/DECISIONS.md`, `docs/MODEL_ROUTING.md` and `docs/ROADMAP.md`.
- `git rev-parse origin/fix/mesp-156-slice11-test-oracles` = `496ca47…`.
- `gh pr view 281 --json isDraft,state`: Draft, OPEN. `gh issue view 283 --json state`: OPEN.
- Then `git switch fix/mesp-156-slice11-test-oracles` and
  `git merge --ff-only docs/mesp-150-slice11-final-review`. Fast-forward only; anything else is a stop.

## 4. Rules to preserve
- Never weaken, skip or delete an existing assertion or test. R12, R17, R18, R20 and A5 stay as
  written and green.
- The fresh-save path (`saved.Outcome` Success) keeps its exact current behaviour. Only a
  **Replayed** result may accept a run at or after `Reconciled`.
- Audit and evidence stay fail closed: `AppendAuditAsync` and `SetEvidenceStateAsync` still run on the
  replay path before the result is returned. `Unknown` stays for genuinely unknown outcomes.
- A changed fingerprint under the same key still returns `migration_reconciliation_idempotency_conflict`;
  a different key on a stale version still returns `migration_run_version_conflict` (A5).
- No migration, schema, EF model, API, contract or cross-module change. No new `ExecuteSql*` site.
  No retries, delays or sleeps.

## 5. Scope and file allowlist
1. **Test first.** Add one LocalDB test to `MigrationReconciliationSqlServerSafetyTests`, next to R18,
   named `Sql_server_s11_mesp164_stale_reconcile_replay_after_ready_for_handover_is_replayed`:
   - reuse the R17/R18 setup (fresh fixture, `ExecuteMixedAsync`, test approval policy, reviewer);
   - capture the run version, reconcile with key K using that version, approve, create readiness;
     assert each step succeeded and the run is `ReadyForHandover`;
   - send `ReconcileAsync` again directly with the **captured** version and key K;
   - assert: `Kind == Replayed`; the returned record `Id` equals the first record's `Id`; from a fresh
     `MigrationDbContext`, exactly 1 reconciliation for the run; the run is still `ReadyForHandover`.
   - Run the suite once **before** the fix and record the new test's red result. It must fail with
     `migration_reconciliation_lifecycle_unknown`. Any other failure code, or a pass, means the
     diagnosis is wrong: stop (§8).
2. **Fix** in `ReconcileCoreAsync`: when `saved.Outcome == MigrationPersistenceOutcome.Replayed`,
   accept a run whose status is `Reconciled`, `ReadyForHandover` or `Closed` at the check at :122.
   Keep every other branch unchanged. The smallest diff wins.
3. File allowlist:
   - `backend/src/MiniErp.App/Modules/Migration/MigrationReconciliationService.cs`;
   - `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs`;
   - `RESULT.md` (one new top entry) and `TASK.md` (Status → CONSUMED).
   - No other file.

## 6. Out of scope
- Any other Slice 11 behaviour, the readiness or approval paths, the Sol follow-ups, governance docs,
  ROADMAP, `ModuleBoundaryTests`.
- Closing or reopening any issue, or any Status or Capability State change. Jira in any form.

## 7. Gates (paste the output tail and the wall time)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` from Windows PowerShell, on the final tree:
   0 warnings, 0 errors, **1555 passed** (1554 + your one test), 0 skipped, with the
   disposable-database line and "MESP data is intact". One final run is enough; this is not a
   concurrency fix.
2. `git diff --check`: clean.
3. `git diff --name-only 496ca47 HEAD`: only §5.3 files plus the planner files listed in §3.
- Reused, not re-run: EF pending-model check (no model change), frontend, npm audit, Playwright.

## 8. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match, or the fast-forward is not possible.
- The new test does not fail before the fix with `migration_reconciliation_lifecycle_unknown`.
- The fix needs a file outside §5.3, or would change the fresh-save path or skip audit/evidence.
- Any other test fails. Keep it as written, record the red output, classify it, and stop.

## 9. Git / PR delivery (positive authority, exactly this)
- Commits: `test(migration): MESP-164 (#283) …` (red test) and `fix(migration): MESP-164 (#283) …`,
  or one `fix` commit containing both. Self-review the staged diff before each commit.
- Normal fast-forward push of `fix/mesp-156-slice11-test-oracles`, which updates Draft PR #281. Add a
  MESP-164 row and the gate result to PR #281's body. It stays a Draft.
- One evidence comment on #283 with the commit, `file:line` and the gate result.
- **NOT authorized:** Ready, reviewers, approval, merge, update-branch, rebase, force-push, push to
  `main`, closing or reopening any issue, any other tracker write.
- After the push, the PR body edit and the comment, restart the local runtime (`MODEL_ROUTING.md`
  §4.7): Release build (skip it if the final gate built Release on the final tree), then
  `.\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart`. Never print a
  connection string or secret. Record the URLs in RESULT.md. It is not a gate; a failure is recorded
  and classified. Then **STOP.** No further mutation.

## 10. Hand-back
- One `RESULT.md` entry at the top, per `MODEL_ROUTING.md` §7, Status `DONE` or `STOPPED`, with:
  the starting-state output; the red-before / green-after evidence; the fix at `file:line`; the gate
  output and wall time; the runtime restart result and URLs; deviations and classified failures.
- **Exact next action:** "Opus 5.5 re-reviews Slice 11 under MESP-150 (#265)."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the next-task summaries untouched.
```

## Next-task summaries (Planner, 2026-09-26)

Executor prompts since the last Sol review: 2 before this one (Sol window 10–15, MODEL_ROUTING §2).

### 1. MESP-164 (#283) regression fix

The prompt is above (Sonnet 5 / high). After it, Opus re-reviews Slice 11 under MESP-150 (#265). If
everything is met, Slice 11 is accepted and #272, #273, #279, #280, #282 and #283 can be closed by
that review. Slice 11 then joins the next periodic Sol review; there is no separate Sol step.

### 2. Cleanup follow-ups: governance text and BRD byte restore (SOL-CL-05, -06, -07)

| | |
|---|---|
| Model / effort | **Opus 5.5** (governance docs are Planner-owned), then the backend suite, because the architecture tests read `AGENTS.md`. |
| Work item | MESP-149 (#264) |
| Scope | `AGENTS.md` §1.4: restore "stopped, **completed** or handed off". `AGENTS.md` §1.6 and `MODEL_ROUTING.md` §5: Ponytail never weakens **authorization, data-loss safeguards or accessibility**, in addition to the current list. `docs/requirements/16_Master_Data_and_Product_Catalog_BRD.md`: restore the tag blob `2a5febc` byte for byte. |
| Out of scope | Any other rule change. No rule widens. |
| Acceptance | `git diff` shows only these lines. `git rev-parse HEAD:docs/requirements/16_Master_Data_and_Product_Catalog_BRD.md` = `2a5febc…`. Backend suite green with 0 skipped. |

### 3. Test hardening: R4 SQL shape and the D-18 AP/cash-bank tests (SOL-CL-01, SOL-CL-04)

| | |
|---|---|
| Model / effort | **Luna 6 / xhigh**, ending with the runtime restart (MODEL_ROUTING §4.7) |
| Work item | A new Task under MESP-145 (#263), created when the prompt is written |
| Scope | `ModuleBoundaryTests.Allow_listed_raw_sql_is_tenant_scoped_and_lock_only`: pin the complete statement shape (one `SELECT`, the `WITH (UPDLOCK, HOLDLOCK)` hint, a `WHERE [TenantId] = {…} AND [<key>] = {…}` predicate, no `;` and no second statement). Add a negative self-check proving that a multi-statement string is rejected. `MigrationExecutionTests`: add AP and cash-bank fault → `partial` and cancellation → throw tests, mirroring the AR test. |
| Out of scope | The R4 allowlist itself: the four-site baseline is ratified (Q-P). Product code. |
| Acceptance | Exact assertions. The negative check fails the old shape check and passes the new one. Backend suite green with 0 skipped. |

### Owner actions pending
- None of the former owner decisions remain. The R4 sites are ratified (Q-P). MESP-149 (#264) stays
  open for follow-up 2; Opus closes it on acceptance (Q-O).
- Close Draft PRs #277 and #278 as superseded by the #281 lineage, keeping their branches. Opus
  decided this under Q-O, but the session's tool permissions refused the close.
