# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7 and §10.

## Next executor prompt

Status: **OPEN** (first release, 2026-09-27). The Planner launches the child through Paseo and passes
this contract verbatim (`MODEL_ROUTING.md` §6, §10).

```markdown
# MESP-167 (#286) — Harden the R4 raw-SQL shape check and add the AP/cash-bank D-18 execution tests

## 1. Role and authority
- You are the **executor, test-only**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you,
  §1 and §5 especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work item: MESP-167 (#286), a Technical Enabler under MESP-145 (#263). Its source is the Sol review of
  MESP-149 (#264): findings SOL-CL-01 and SOL-CL-04.

## 2. Read order
1. `AGENTS.md`, then this prompt.
2. `gh issue view 286`, read-only.
3. Code. **Serena:** call `initial_instructions` once, then `find_symbol` / `get_symbols_overview`;
   don't read whole files.
   - `backend/tests/MiniErp.ArchitectureTests/ModuleBoundaryTests.cs`:
     `Allow_listed_raw_sql_is_tenant_scoped_and_lock_only` (~598) and its path constants.
   - The three `ExecuteSqlInterpolatedAsync` sites it parses, in
     `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/MigrationPersistence.cs` and
     `MigrationReconciliationPersistence.cs`.
   - `backend/tests/MiniErp.ArchitectureTests/MigrationExecutionTests.cs`:
     `Ar_reconciliation_read_fault_is_partial_but_cancellation_propagates` (~175) and the test
     doubles it uses.
   - The product code that reads AP and cash-bank reconciliation evidence during execution (find it
     from the AR path). Read only.
- **Context7:** only if you touch a Roslyn API you are unsure of. **Ponytail:** full; it never trims
  assertions, gate output or the RESULT.md entry. If a plugin is missing, say so in one line and
  continue.

## 3. Starting-state check (record the output in RESULT.md)
- You are in a fresh worktree created from `origin/main`. HEAD descends from `6d14af7` (the PR #281
  merge), and the tree is clean.
- `gh issue view 286 --json state`: OPEN.
- Anything else is a stop (§10).

## 4. Rules to preserve
- **No product code change.** If an AP or cash-bank fault/cancellation path does not behave like AR,
  that is a product finding: record it and stop (§10). Never shape a test around a defect.
- Never weaken, skip or delete an existing assertion. The R4 allowlist stays exactly three raw-SQL
  sites (the four-site baseline is ratified as Q-P). The ratchet only shrinks.
- No fixed sleeps. No hard-coded environment data or secrets.

## 5. Scope and file allowlist
1. **SOL-CL-01: R4 shape.** In `Allow_listed_raw_sql_is_tenant_scoped_and_lock_only`, replace the
   prefix/substring checks with a complete-shape check. Every statement must be:
   - exactly one `SELECT … FROM [schema].[table] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {…} AND
     [<key>] = {…}`;
   - with no `;`, no second statement, and no other clause or keyword after the predicate.

   Use one anchored regex or an equivalent single predicate. Keep `Assert.Equal(3, statements.Length)`.
   Add a **negative self-check**, in the same test or in one new `[Fact]`: the new shape predicate
   rejects at least these strings:
   - a multi-statement string (`…; DELETE …`);
   - a string without `HOLDLOCK`;
   - a string without the `[TenantId]` predicate.

   It must also accept each real statement. Record in RESULT.md one string that the **old** checks
   would have accepted and the new one rejects.
2. **SOL-CL-04: D-18.** In `MigrationExecutionTests`, add AP and cash-bank equivalents of
   `Ar_reconciliation_read_fault_is_partial_but_cancellation_propagates`. They cover the same two
   facts:
   - a read fault gives `partial`;
   - cancellation propagates as a throw.

   Reuse the existing doubles and helpers. A `[Theory]` over the three domains is acceptable if it
   keeps every AR assertion.
3. File allowlist: the two test files above, `RESULT.md` (one new top entry) and `TASK.md` (Status →
   CONSUMED). No other file.

## 6. Out of scope
- Product code, the allowlist contents, other tests and governance docs.
- The MESP-166 (#285) watch item, the golden cycle, UI.
- Any tracker write except the one evidence comment in §9. Jira in any form.

## 7. Acceptance matrix (Opus checks each)
| # | Criterion |
|---|---|
| A1 | The shape check is anchored and complete: one statement, hint, both predicates, nothing after. |
| A2 | The negative self-check rejects the three bad strings and accepts all three real statements. |
| A3 | A string the old check accepted is shown to be rejected by the new one. |
| A4 | The AP and cash-bank tests assert `partial` on fault and a throw on cancellation, with exact values. |
| A5 | The diff touches only the §5.3 files; no assertion is weakened. |
| A6 | Backend gate green with 0 skipped; the count equals 1555 plus the tests you added, stated exactly. |

## 8. Gates (paste the output tail and the wall time)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` from Windows PowerShell: 0 warnings / 0
   errors, pass count, 0 skipped, the disposable-database line and "MESP data is intact". If the Release
   build is locked by a running dev API (MSB3026), stop only the `MiniErp.Api` process on port 5300 and
   the frontend on port 4300, record their PIDs, and let the §9 restart replace them.
2. `git diff --check` is clean. `git diff --name-only origin/main HEAD` lists only §5.3 files.
- Not re-run, because nothing they cover changes: EF pending-model, frontend, Playwright, npm audit.

## 9. Git / PR delivery (positive authority, exactly this)
- Branch `chore/mesp-167-r4-shape-d18-tests` from `origin/main`.
- Self-review the staged diff, then make commits with the prefix `test(architecture)` or
  `test(migration)`. Reference `MESP-167 (#286)`.
- Push the branch normally and open a **Draft** PR to `main` titled
  `test: MESP-167 (#286) R4 shape pin and D-18 AP/cash-bank tests`. Its body has the A1–A6 evidence.
- One evidence comment on #286.
- **Before the final commit**, restart the local runtime (`MODEL_ROUTING.md` §4.7):
  ```powershell
  $env:MESP_DEV_AUTH_BYPASS = [Environment]::GetEnvironmentVariable('MESP_DEV_AUTH_BYPASS','User')
  .\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart -StartupTimeoutSeconds 180
  ```
  Never print a connection string or secret. Record the result and URLs in RESULT.md.
- **NOT authorized:**
  - Ready, reviewers, approval or merge;
  - rebase, force-push or any push to `main`;
  - closing or reopening an issue, or a Status or Capability State change.
- Then **STOP.**

## 10. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match, or the build is not clean.
- The change would need a file outside §5.3, or a product change.
- The AP or cash-bank path does not behave like AR (not `partial` on fault, or cancellation swallowed).
  Record the file:line evidence as a product finding.
- A real statement fails the new shape check. Record it; do not loosen the pattern to fit.
- Any unrelated red in the gate: capture the full failure (console + TRX), classify it and stop.

## 11. Hand-back
- One `RESULT.md` entry at the top per `MODEL_ROUTING.md` §7, Status `DONE` or `STOPPED`. It holds:
  - the starting-state output;
  - the A1–A6 evidence with file:line;
  - the old-vs-new rejection example;
  - the gate output and wall time;
  - the runtime restart result and URLs;
  - the PR URL;
  - deviations.
- **Exact next action:** "Opus 5.5 reviews MESP-167 (#286)."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the summaries untouched.
```

## Next-task summaries (Planner, 2026-09-27)

Accepted Executor cycles since the last Sol review: 1 (`ORCHESTRATION_STATE.yaml`; MODEL_ROUTING §2).

1. **MESP-167 (#286)**: the prompt above. Opus reviews it and, on acceptance, merges it under Q-O.
2. **Decide whether MESP-141 (#229) needs further slices before it closes** (Opus). The M40 exit
   criteria are completed reconciliation, accepted exceptions, named approvals and a readiness
   snapshot. This is a Planner judgment against BRD 40.
3. **MESP-151 (#266): Golden Release-1 end-to-end cycle** (Executor), after item 2.

### Owner actions pending
- None.
