# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **OPEN**

Routing: **Luna 6, effort xhigh.** Open a **new session**.
- This is diagnosis, not a fix. The red is unreproduced and its message was lost, so the root cause
  is unclear. MODEL_ROUTING §3: Luna 6 (xhigh) diagnoses first; Sonnet 5 fixes only a bounded,
  identified root cause.
- The session makes no product or test change. Its output is evidence: the captured failure, or a
  recorded run budget that stayed green.

```markdown
# MESP-166 (#285) — Diagnose the intermittent MESP141 execution claim-race red on the Slice 11 path
Model: Luna 6 — Effort: xhigh — Fresh session

## 1. Role and authority
- You are the **executor, diagnosis only**. Opus 5.5 accepts or rejects your result. `AGENTS.md`
  binds you, §1 especially. Authorization is positive: every action this prompt does not list is
  forbidden.
- Work item: MESP-166 (#285), a Bug under MESP-15 (#104), capability MESP-141 (#229). It is the last
  open question before Slice 11 acceptance under MESP-150 (#265).
- Branch: `fix/mesp-156-slice11-test-oracles` (Draft PR #281).

## 2. Read order
1. `AGENTS.md`, then this prompt.
2. `RESULT.md`: the top entry only (the Opus review that filed this bug).
3. `gh issue view 285`, read-only. It lists the test's assertions and the suspected failure modes.
4. Code. **Serena:** call `initial_instructions` once, then `find_symbol`; don't read whole files.
   All read only.
   - Test: `backend/tests/MiniErp.ArchitectureTests/SqlServerSafetyTests.cs`,
     `MESP141_sql_server_execution_claim_is_acquired_before_owner_preflight` (~3520), and its double
     `SqlClaimOwnerGateway` (~4087).
   - `backend/src/MiniErp.App/Modules/Migration/MigrationExecutionService.cs`, `ExecuteCoreAsync`
     (~171–261): every early return a loser can take.
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationPersistence.cs`, the
     attempt-start path (~214–335), including `ResolveConcurrentAttemptAsync` (~879) and the
     SqlException 1205 catch. `MigrationAttempt.StartNext` in `MigrationApplicationContracts.cs`
     (~730).
- **Context7:** not needed. **Ponytail:** full; it never trims failure output, gate output or the
  RESULT.md entry. If a plugin is missing, say so in one line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status -sb`: the tree is clean, you are on `fix/mesp-156-slice11-test-oracles`, and it is level
  with `origin/fix/mesp-156-slice11-test-oracles`.
- HEAD is the Opus review commit whose subject starts `docs(review): MESP-150 (#265) Opus review of
  MESP-165`, and it descends from `6de2e84`.
- `gh pr view 281 --json isDraft,state`: Draft, OPEN. `gh issue view 285 --json state`: OPEN.
- Anything else is a stop (§8). Do not commit someone else's uncommitted work.

## 4. Rules to preserve
- **No product or test change of any kind**, not even a temporary one left in the tree. Do not
  weaken, skip, retry or re-order any assertion. Stress comes from repetition and load, never from
  editing code.
- Every run's **complete console output** goes to a log under `$env:TEMP\mesp166\`, outside the
  repository, via `*>&1 | Tee-Object`. A red without its full failure message counts as not captured.
- Never print, write to a file or commit a connection string or secret. Each run uses a fresh
  disposable LocalDB and removes `MESP_DEV_AUTH_BYPASS` for the process.

## 5. Scope and file allowlist
1. **Build once:** `dotnet build .\backend\MiniErp.sln --configuration Release --no-restore`, with 0
   warnings and 0 errors.
2. **Repro budget.** Run the stages in order. **At the first red, stop running and go to §5.3.**
   For stages A and B, use this block from Windows PowerShell, changing only `$filter` and `$tag`
   (`$tag` is unique per run):
   ```powershell
   New-Item -ItemType Directory -Force "$env:TEMP\mesp166" | Out-Null
   $db = "MiniErpFoundation_{0}_{1}" -f (Get-Date -Format 'yyyyMMddHHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0,8))
   $env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=$db;Integrated Security=True;TrustServerCertificate=True;"
   Remove-Item Env:MESP_DEV_AUTH_BYPASS -ErrorAction SilentlyContinue
   dotnet test .\backend\tests\MiniErp.ArchitectureTests --configuration Release --no-restore --no-build --filter $filter *>&1 | Tee-Object -FilePath "$env:TEMP\mesp166\$tag.log"
   Remove-Item Env:MESP_SQLSERVER_SAFETY_CONNECTION_STRING
   ```
   - **Stage A:** 30 isolated runs with
     `$filter = "FullyQualifiedName~MESP141_sql_server_execution_claim_is_acquired_before_owner_preflight"`.
   - **Stage B:** 10 class-level runs with
     `$filter = "FullyQualifiedName~MiniErp.ArchitectureTests.SqlServerSafetyTests"`.
   - **Stage C:** up to 4 runs of the full gate,
     `.\scripts\Test-MiniErpBackend.ps1 *>&1 | Tee-Object -FilePath "$env:TEMP\mesp166\gate-<n>.log"`
     (Release is already built). The original red happened here.
   - Record a table in RESULT.md with the stage, run number, pass or fail, and duration.
3. **If a red is captured**, record these verbatim in RESULT.md:
   - the failing test name;
   - the failing assertion line;
   - the full assertion message, including both `Kind:Code:attempt=` values;
   - the stack frame inside the test.

   Then classify the red with a `file:line` reading of the code:
   - **(a) Wrong loser code.** The loser returned `Unknown`, although committed state proves a
     conflict. This is a product defect in the MESP-165 class. Name the return site.
   - **(b) Correct but unlisted loser code.** The loser returned a safe, deterministic rejection that
     the test does not accept: `migration_run_version_conflict`, `migration_run_terminal`, or a
     lineage denial. This is an oracle question for Opus. **Do not edit the test.**
   - **(c) Zero winners, or two owner executions.** A claim-safety defect. Name the interleaving.
   - **(d) Other**, such as infrastructure, a deadlock victim surfaced as a failure, or LocalDB. Say
     what it is, with the evidence.

   A red in a different test is also a finding. Record it the same way and stop.
4. **If the whole budget stays green**, record the table and say so plainly. Do not re-classify the
   original red; that decision is Opus's.
5. File allowlist: `RESULT.md` (one new top entry) and `TASK.md` (Status → CONSUMED). No other file.
   Logs stay in `$env:TEMP\mesp166\`; paste only the relevant excerpts.

## 6. Out of scope
- Any fix, even an obvious one.
- Any change to the test, the owner gateway double, product code, governance docs, ROADMAP or the
  Sol follow-ups.
- Closing or reopening any issue, or any Status or Capability State change. Jira in any form.

## 7. Gates (paste the output tail and the wall time)
1. The §5.1 build result and the §5.2 run table, with each run's result line.
2. At least one Stage C full-gate run on the final tree, whatever its result. Report 0 warnings,
   0 errors, the pass count, the disposable-database line and "MESP data is intact".
   - If Stage C ran, its runs count.
   - If a red stopped the budget earlier, run the gate once more and report the result as it is.
3. `git diff --check` is clean, and `git diff --name-only <review commit> HEAD` lists only §5.5 files.
- Reused, not re-run: the EF pending-model check (no model change), frontend, npm audit, Playwright.

## 8. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match, or the build is not clean.
- Continuing would need a change to a file outside §5.5.
- A red appears whose full output you did not capture. Record that fact. Do not re-run beyond the
  remaining budget to "get a better one".
- **After a red, commit only RESULT.md and TASK.md.** Never push anything else. Never call a red
  "timing-dependent" or "pre-existing" without the §5.3 classification.

## 9. Git / PR delivery (positive authority, exactly this)
- One commit, `docs(migration): MESP-166 (#285) record claim-race diagnosis`, containing RESULT.md and
  TASK.md. Self-review the staged diff first.
- A normal fast-forward push of `fix/mesp-156-slice11-test-oracles`, which updates Draft PR #281. Add
  a MESP-166 row with the outcome to PR #281's body. The PR stays a Draft.
- One evidence comment on #285, with the run table, the captured failure (or "budget green") and the
  classification.
- **NOT authorized:**
  - Ready, reviewers, approval or merge;
  - update-branch, rebase or force-push;
  - any push to `main`;
  - closing or reopening any issue, or any other tracker write.
- After the push, the PR body edit and the comment, restart the local runtime (`MODEL_ROUTING.md`
  §4.7) with `.\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart`.
  Never print a connection string or secret. Record the URLs in RESULT.md. Then **STOP.** No further
  mutation.

## 10. Hand-back
- One `RESULT.md` entry at the top, per `MODEL_ROUTING.md` §7, with Status `DONE` or `STOPPED`. It
  contains:
  - the starting-state output and the build result;
  - the run table;
  - the captured failure, verbatim, with its §5.3 class, or "budget green";
  - the gate output and wall time;
  - the runtime restart result and URLs;
  - deviations.
- **Exact next action:** "Opus 5.5 reviews the MESP-166 (#285) diagnosis and decides Slice 11 under
  MESP-150 (#265)."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the next-task summaries untouched.
```

## Next-task summaries (Planner, 2026-09-26)

Executor prompts since the last Sol review: 4 before this one (Sol window 10–15, MODEL_ROUTING §2).

### 1. MESP-166 (#285) claim-race diagnosis

The prompt is above (Luna 6 / xhigh). Opus then reviews the result:
- class (a) or (c): a Sonnet 5 fix prompt, with the captured red as its check;
- class (b): Opus rules on the oracle;
- budget green: Opus decides whether the single lost red still blocks Slice 11.

If nothing blocks, Slice 11 is accepted under MESP-150 (#265). That review closes #272, #273, #279,
#280, #282, #283, #284 and #285. Slice 11 then joins the next periodic Sol review.

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
- None. The R4 sites are ratified (Q-P). MESP-149 (#264) stays open for follow-up 2; Opus closes it on
  acceptance (Q-O).
- Opus closed Draft PRs #277 and #278 on 2026-09-26 as superseded by the #281 lineage. Their branches
  are kept.
