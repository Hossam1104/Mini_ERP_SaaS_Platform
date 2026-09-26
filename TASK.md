# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **OPEN**

Routing: **Claude Sonnet 5, effort high.** Open a **new session**.
- It is a contained, already-diagnosed product defect: one catch in one persistence method, turned
  green by an existing test (MODEL_ROUTING §1 and §3).
- Effort is high, not medium, because it touches migration approval evidence on the money path.

```markdown
# MESP-165 (#284) — Concurrent identical approvals return Unknown when evidence confirmation races
Model: Claude Sonnet 5 — Effort: high — Fresh session

## 1. Role and authority
- You are the **bug fixer**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you, §1
  especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work item: MESP-165 (#284), a Bug under MESP-15 (#104), capability MESP-141 (#229). It is the last
  open blocker of Slice 11 acceptance under MESP-150 (#265).
- Branch: `fix/mesp-156-slice11-test-oracles` (Draft PR #281).

## 2. Read order
1. `AGENTS.md`, then this prompt.
2. `RESULT.md`: the top entry only (the Opus review that found this defect).
3. `gh issue view 284`, read-only. It holds the full diagnosis.
4. Code. **Serena:** call `initial_instructions` once, then `find_symbol`; don't read whole files.
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationReconciliationPersistence.cs`,
     `ConfirmApprovalEvidenceAsync` (~228–248): an unlocked read-modify-write on a rowversioned row;
     the bare `catch (DbUpdateException)` turns a lost concurrent update into `UnknownOutcome`.
   - `backend/src/MiniErp.App/Modules/Migration/MigrationReconciliationService.cs`, `ApproveCoreAsync`
     (~150–200): the caller; `:193–194` return `migration_approval_evidence_unavailable`. Read only.
   - Test: `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs`,
     R20 `Sql_server_s11_r20_concurrent_repeated_actions_are_idempotent` (~686). Read only.
- **Context7:** optional, for the EF Core `DbUpdateConcurrencyException` behaviour only.
  **Ponytail:** full; it never trims assertions, audit, gate output or the RESULT.md entry. If a plugin
  is missing, say so in one line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status -sb`: the tree is clean, you are on `fix/mesp-156-slice11-test-oracles`, and it is level
  with `origin/fix/mesp-156-slice11-test-oracles`.
- HEAD is the Opus review commit whose subject starts `docs(review): MESP-150 (#265) Opus review of
  MESP-164`, and it descends from `b8b4858`.
- `gh pr view 281 --json isDraft,state`: Draft, OPEN. `gh issue view 284 --json state`: OPEN.
- Anything else is a stop (§8). Do not commit someone else's uncommitted work.

## 4. Rules to preserve
- Never weaken, skip or delete an existing assertion or test. R12, R17, R18, R20, A5 and the MESP-164
  test stay as written and green.
- `Unknown` stays for genuinely unknown outcomes. After a failed save, return `Replayed` **only** when
  a fresh read of committed state proves that this approval's evidence is confirmed. Otherwise keep
  the current `UnknownOutcome` / `migration_approval_persistence_unknown`.
- The fresh-confirm path (`Success`) and the already-confirmed path (`Replay`) keep their exact current
  behaviour. `ApproveCoreAsync` still runs audit, confirmation and `SetEvidenceStateAsync` in the
  current order.
- No migration, schema, EF model, API, contract or cross-module change. No new `ExecuteSql*` site, and
  no new lock. No retries, delays or sleeps. Don't make the service gate cross-process.

## 5. Scope and file allowlist
1. **Red first.** On the current tree, run R20 on its own against a disposable LocalDB, three times,
   from Windows PowerShell:
   ```powershell
   $db = "MiniErpFoundation_{0}_{1}" -f (Get-Date -Format 'yyyyMMddHHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0,8))
   $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=$db;Integrated Security=True;TrustServerCertificate=True;"
   Remove-Item Env:MESP_DEV_AUTH_BYPASS -ErrorAction SilentlyContinue
   dotnet test .\backend\tests\MiniErp.ArchitectureTests --configuration Release --no-restore --filter "FullyQualifiedName~r20_concurrent"
   Remove-Item Env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING
   ```
   Build Release once first if it is stale. Never print the connection string. Each run must fail on
   the **approvals** assertion with `UnknownOutcome` / `migration_approval_evidence_unavailable`. A
   pass or any other failure means the diagnosis is wrong: stop (§8).
2. **Fix** `ConfirmApprovalEvidenceAsync`: before the existing bare catch, catch
   `DbUpdateConcurrencyException`. Re-read the approval by ID in a **fresh** context. If it exists
   and `EvidenceConfirmed` is true, return `Replay(ToRecord(it))`. Otherwise return the same
   `UnknownOutcome` as today. The smallest diff wins.
3. **Green.** Run the same isolated R20 command three more times: 3/3 must pass. If R20 now fails at
   a later step (readiness) or with a different code, stop (§8). That is a new diagnosis; don't widen
   the fix.
4. File allowlist:
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationReconciliationPersistence.cs`;
   - `RESULT.md` (one new top entry) and `TASK.md` (Status → CONSUMED).
   - No other file. No new test: R20 run on its own is the check.

## 6. Out of scope
- `MigrationReconciliationService.cs`, the reconcile and readiness paths, `SaveApprovalAsync`, the Sol
  follow-ups, governance docs, ROADMAP, `ModuleBoundaryTests`.
- Closing or reopening any issue, or any Status or Capability State change. Jira in any form.

## 7. Gates (paste the output tail and the wall time)
1. Isolated R20: 3 red before the fix and 3 green after (§5.1, §5.3), with each run's result line.
2. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` from Windows PowerShell, on the final tree:
   0 warnings, 0 errors, **1555 passed**, 0 skipped, with the disposable-database line and "MESP data
   is intact". One final run is enough.
3. `git diff --check`: clean.
4. `git diff --name-only <review commit> HEAD`: only §5.4 files.
- Reused, not re-run: EF pending-model check (no model change), frontend, npm audit, Playwright.

## 8. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match.
- R20 run on its own does not fail as §5.1 describes, or it is not 3/3 green after the fix.
- The fix needs a file outside §5.4, or would change the fresh-confirm or already-confirmed path.
- Any other test fails, in the gate **or in any isolated run**. Keep it as written, record the red
  output, classify it, and stop. "It passes in the full suite" is not a classification.

## 9. Git / PR delivery (positive authority, exactly this)
- One commit: `fix(migration): MESP-165 (#284) …`, then one `docs(migration): MESP-165 (#284) …`
  commit for RESULT.md and TASK.md. Self-review the staged diff before each commit.
- Normal fast-forward push of `fix/mesp-156-slice11-test-oracles`, which updates Draft PR #281. Add a
  MESP-165 row and the gate result to PR #281's body. It stays a Draft.
- One evidence comment on #284 with the commit, `file:line`, the isolated R20 results and the gate
  result.
- **NOT authorized:** Ready, reviewers, approval, merge, update-branch, rebase, force-push, push to
  `main`, closing or reopening any issue, any other tracker write.
- After the push, the PR body edit and the comment, restart the local runtime (`MODEL_ROUTING.md`
  §4.7): `.\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart` (the final
  gate already built Release). Never print a connection string or secret. Record the URLs in
  RESULT.md. It is not a gate; a failure is recorded and classified. Then **STOP.** No further mutation.

## 10. Hand-back
- One `RESULT.md` entry at the top, per `MODEL_ROUTING.md` §7, Status `DONE` or `STOPPED`, with:
  the starting-state output; the isolated red/green R20 evidence; the fix at `file:line`; the gate
  output and wall time; the runtime restart result and URLs; deviations and classified failures.
- **Exact next action:** "Opus 5.5 re-reviews Slice 11 under MESP-150 (#265)."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the next-task summaries untouched.
```

## Next-task summaries (Planner, 2026-09-26)

Executor prompts since the last Sol review: 3 before this one (Sol window 10–15, MODEL_ROUTING §2).

### 1. MESP-165 (#284) approval-evidence race

The prompt is above (Sonnet 5 / high). After it, Opus re-reviews Slice 11 under MESP-150 (#265). If
everything is met, Slice 11 is accepted and #272, #273, #279, #280, #282, #283 and #284 can be closed
by that review. Slice 11 then joins the next periodic Sol review; there is no separate Sol step.

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
