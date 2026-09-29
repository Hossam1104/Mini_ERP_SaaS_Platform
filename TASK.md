# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7 and §10.

## Next executor prompt

Status: **CONSUMED** (released 2026-09-28). The Planner launches the child through Paseo inside its own session and
the main checkout (Q-T), and passes this contract verbatim (`MODEL_ROUTING.md` §6, §10).

```markdown
# MESP-173 (#299) — MESP-169 correction from the Sol critical-point review

## 1. Role and authority
- You are the **executor**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you, §1, §2 and
  §5 especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work item: MESP-173 (#299), a Task under MESP-15 (#104), for capability MESP-141 (#229). Source: the Sol
  critical-point review of MESP-169 (#292) (PR #297, merged at `8060bab`), reconciled by Opus on 2026-09-27.
  All four findings are accepted. After you finish, Sol re-reviews this correction.
- You run **in the main checkout** (Q-T), not in a worktree. The Planner does not edit tracked files while
  you run. A separate UI lane may be running in `.worktrees/`; do not touch it or port 4310.

## 2. Read order
1. `AGENTS.md`, then this prompt, then `gh issue view 299` (read-only).
2. The MESP-169 and MESP-170 entries in `RESULT.md`, and the `docs/audit/mesp-141-m40-traceability.md` rows
   that MESP-169 set.
3. Code. **Serena** first. Main areas:
   - `MigrationReconciliationService.cs`, `MigrationValidationApplication.cs`, `MigrationExecutionService.cs`,
     and the Migration persistence under `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/`;
   - `MigrationRunSafetySqlServerTests.cs` and the other Migration SQL safety tests.
- **Context7:** for EF Core or SQL Server APIs you are unsure of. **Ponytail:** full, within `AGENTS.md` §1.

## 3. Starting-state check (record the output in RESULT.md)
- `git status` clean, on `main`, and HEAD descends from both `b52364a` (the PR #302 merge) and `3472671` (the
  PR #304 merge).
- `gh issue view 299 --json state`: OPEN.
- Anything else is a stop (§10).

## 4. Rules to preserve
- Tenant isolation, fail-closed scope checks, idempotency, the Outcome Unknown hard stop, and "Finance owns
  accounting". Modules call each other only through public contracts; no cross-module DbContext.
- Never weaken, skip or delete an existing assertion. Ratcheted allowlists only shrink.
- Never invent a business rule; where the BRD is silent, fail closed and record the gap.
- Migrations are additive and module-owned. Every EF context reports no pending model changes.
- Prefer no new public REST operation. Any new one meets the `AGENTS.md` §2 REST Definition of Done.

## 5. Scope (the four MATERIAL findings; line references are at `8060bab` and may have moved)
1. **Reconciliation preview.** It only relabels the ordinary preview (`MigrationReconciliationService.cs:146`).
   Compute the reconciliation controls from the dry-run evidence, with no approval, readiness or run
   effect. Test that the result differs from the ordinary preview and that no state changes.
2. **Legacy null `CanonicalPayload`.** Legacy validation rows with a null payload can strand a corrected
   retry (`MigrationValidationApplication.cs:247`). Use the immutable staged payload for unchanged legacy
   rows, and preflight so that no pending attempt is ever stranded. Add an upgrade-path SQL test that seeds
   legacy rows.
3. **No-effect snapshot tests.** They seed no owner rows and exclude owner audit and outbox
   (`MigrationRunSafetySqlServerTests.cs:37,364`). Seed representative owner rows (Master Data, Finance,
   Inventory), compare keys, values and row versions, and assert owner audit and outbox are unchanged.
   Strengthen only; never loosen an existing assertion.
4. **Concurrent cancel vs execute.** Add a SQL test proving exactly one path wins and no owner effect
   coexists with `Cancelled`. Use real concurrency (barriers or tasks) and no fixed sleeps. If the test
   exposes a product defect, fix the product code.
5. Update the audit rows these touch (evidence file:line and tests). Change no other row.
6. **File allowlist.** You may modify only:
   - `backend/src/MiniErp.App/Modules/Migration/**`;
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/**`;
   - `backend/src/MiniErp.Infrastructure/Persistence/Adapters/Migration*.cs`;
   - `backend/src/MiniErp.Infrastructure/Persistence/Migrations/Mesp141/**`, for an additive migration only;
   - `backend/src/MiniErp.Api/MigrationEndpoints.cs`, only if a response shape must change;
   - `backend/tests/MiniErp.ArchitectureTests/Migration*.cs` and `backend/tests/MiniErp.ArchitectureTests/SqlServerSafetyTests.cs`;
   - `docs/audit/mesp-141-m40-traceability.md`, `RESULT.md` and `TASK.md` (Status line only).

   A needed file outside this list is a stop (§10). Record it with the reason.

## 6. Out of scope
MESP-171/172 scope, UI, templates or file formats (M40-DEC-003), production correction (M40-DEC-005), and
any tracker write except the one evidence comment in §9. No Jira.

## 7. Acceptance matrix (Opus checks each)
| # | Criterion |
|---|---|
| A1 | The reconciliation preview is computed from dry-run evidence, tested to differ from the preview and to have no effect. |
| A2 | Legacy null-payload rows retry correctly; an upgrade-path SQL test; no stranded pending attempt. |
| A3 | Snapshot tests seed owner rows and cover keys, values, versions, owner audit and outbox. |
| A4 | A concurrent cancel-vs-execute SQL test: exactly one winner, and no owner effect alongside `Cancelled`. |
| A5 | Audit rows updated with evidence; no other row changes. |
| A6 | All gates green with 0 skipped; the backend count is 1589 plus the added tests, stated exactly. |

## 8. Gates (paste the output tail and the wall time)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false`: 0 warnings / 0 errors, the pass count, 0 skipped,
   the disposable-database line and "MESP data is intact". If the build is locked by the dev API
   (MSB3027/MSB3026), stop only the `MiniErp.Api` process on port 5300 and record its PID.
2. `dotnet ef migrations has-pending-model-changes` for every context you touch: none pending.
3. `git diff --check` is clean. Run the frontend gates only if a frontend file changes; none should.
4. An intermittent red on the execution-claim path is MESP-166 (#285): rerun that single test 5 times,
   record the message and stack in RESULT.md and add them to #285, then rerun the **full** wrapper. You
   deliver only on a green full gate; a second red is a stop (§10).

## 9. Git / PR delivery (positive authority, exactly this)
- Branch `fix/mesp-173-mesp-169-correction` from `origin/main`. Self-review the staged diff, then make
  conventional commits scoped `migration`, referencing `MESP-173 (#299)`.
- Push normally and open a **Draft** PR to `main` titled
  `fix(migration): MESP-173 (#299) MESP-169 correction from Sol review`, with the A1–A6 evidence.
- One evidence comment on #299.
- After your last code or test commit, restart the runtime from this checkout (Q-S):
  ```powershell
  # stop only the MiniErp.Api listener on 5300 if it is running, then:
  dotnet build .\backend\MiniErp.sln --configuration Release --no-restore
  $env:MESP_DEV_AUTH_BYPASS = [Environment]::GetEnvironmentVariable('MESP_DEV_AUTH_BYPASS','User')
  .\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart -StartupTimeoutSeconds 240
  ```
  Never print a connection string or secret. Only the RESULT.md/TASK.md hand-back commit may follow.
- Finally, `git switch main` so the checkout is left on `main`, clean.
- **NOT authorized:** Ready, reviewers, approval or merge; rebase, force-push or any push to `main`;
  closing or reopening an issue, Status or Capability State changes, new issues.
- Then **STOP.**

## 10. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match.
- A required behavior needs a business rule the BRD does not give. Record the question and the options,
  stop that item only, and deliver the rest.
- The change would weaken Tenant isolation, idempotency or the Outcome Unknown stop, or would need an
  allowlist to grow.
- A reproducible unrelated red in the gate: capture the console and TRX, classify it and stop.

## 11. Hand-back
- One `RESULT.md` entry at the top per `MODEL_ROUTING.md` §7, Status `DONE` or `STOPPED`, holding the
  starting state, the A1–A6 evidence with file:line, the gate output and wall time, the restart result, the
  PR URL and any deviations.
- **Exact next action:** "Opus 5.5 reviews MESP-173 (#299); Sol re-reviews."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the summaries untouched.
```

## Next-task summaries (Planner, 2026-09-28)

Accepted Executor cycles since the last Sol review: 6 (`ORCHESTRATION_STATE.yaml`; MODEL_ROUTING §2).

1. **MESP-173 (#299)**: the prompt above. Sol re-reviews it afterwards.
2. **MESP-171 (#294)**: Slice 14, covering the authority matrix, readiness review and reporting.
   MESP-141 closes only after Slices 12–15. Slice 15 (MESP-172, UI lane) blocks closure.
3. **MESP-151 (#266)**: the golden cycle. After it, Wave 1 (Q-Q).
4. **UI lane (Q-U)**, in a worktree, Luna → Opus review → owner approval: MESP-175 (#305) Tenant-branding
   default theme and SAR symbol; then MESP-176 (#306) Login page; MESP-177 (#307) notification bell;
   MESP-172 (#295) Migration review.
