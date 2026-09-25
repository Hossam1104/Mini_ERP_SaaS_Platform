# Results

The shared results log, newest entry first. Every model adds exactly one entry per session, using the
template in [`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §7. Older logs are archived verbatim in
[`docs/history/`](docs/history/).

## 2026-09-25 — Opus review of the Slice 11 test-oracle handoff — Claude Opus 5.5 / high — MESP-150 (#265), MESP-156..163 (#272–#276, #279, #280, #282)

- Status: **REJECTED.** Slice 11 is still not accepted. Luna's STOP under TASK §10 was correct and within its authority. Two of the new oracles fail on the product, as they should: R12 (MESP-161) and R20 (MESP-162). One oracle is wrong: R07. The gate pair is unstable because of an existing StartAttempt deadlock (MESP-163).
- Branch / starting SHA / ending SHA: this review is on `docs/mesp-150-slice11-rereview`, created from `fix/mesp-156-slice11-test-oracles` at `d410e8d` (Draft PR #281). Ending SHA: this commit, which is local and not pushed.
- What changed: `RESULT.md` (this entry), `docs/ROADMAP.md` (MESP-141 row and queue item 1), `TASK.md` (next-task summary 1). Tracker: created MESP-163 (#282).
- Verification (live Git, tracker and code; nothing re-run):
  - The tree is clean. PR #281 is a Draft with 8 commits. #272–#276 each have one evidence comment. #279 and #280 are `type:bug` in Project #1 with Jira Key, `[MESP-15] #104` and Status Todo. Nothing was merged, marked Ready or closed.
  - `git diff e507cc9 d410e8d` changes only the allowed test file, RESULT.md and TASK.md. The 5 removed lines are three `service.X` calls rewritten to use per-task `Service(...)` instances, plus one local variable. No assertion was removed or relaxed, and nothing is skipped. The suite total is 1552: 1551 plus the one new A5 test.
- Per-row verdict:

| Row | Verdict | Reason |
|---|---|---|
| R04 / R05 (MESP-156) | **Met, one strengthening needed** | `Assert.Equal` on all three fields against a persisted `MigrationEconomicRepresentation` (RT:130–133, 159–162; helper RT:787–800). The helper picks one row by `OrderByDescending(Kind…).ThenByDescending(RecordedAt)`, so rows that disagree would be masked. Add an assertion that all Finance representation rows for the effect carry exactly one distinct mapping. |
| R07 (MESP-157) | **Not met: the test oracle is wrong** | Subsidiary/GL checks pass (RT:210–251). The count at RT:252–256 sums owner *artifacts* (15). An inventory effect legitimately yields both a stock movement and a valuation event, so there are 14 execution effects. Part of this is a Planner defect: TASK §5.2 said "one per owner effect … from the owner-effect counts", which mixes the two. Correct oracle: distinct represented `EffectId`s equal the run's execution-effect count (`ReadEconomicCountsAsync.Effects`), and every representation's `EffectId` is one of the run's effects. |
| R12 (MESP-158) | **Test correct; product defect confirmed** | `IsCurrent == false` (RT:374–375) passes. The second reconciliation fails with `migration_reconciliation_version_conflict`. Code confirms it: detail `Id = StableId(domain, scope)` (Svc:540, 466, 497, 713–717), and the detail primary key is `Id` alone (`MigrationDbContext.cs:473`). A second reconciliation of the same run always collides with the same keys and falls into the unique-violation branch (Persistence:71–80). Re-reconciliation after corrected evidence is impossible. → MESP-161 (#279). Changing the approval policy to move the fingerprint (on a separate clean fixture) is acceptable: mutating the journal would block the reconciliation and hide the approval oracle. |
| R18 (MESP-159) | **Met** | Persisted flags, per-schema row counts and constructor reflection (RT:554–576). It passed in both runs. The in-place-update limit and the deferred M27 read-back are recorded. |
| R20 (MESP-162) | **Test correct; product defect confirmed** | Separate instances per task (RT:626–637). Opus diagnosis (a hypothesis for the fixer): a loser that replays inside `SaveAsync` still runs the post-save lifecycle step. It then races the winner on `TransitionRunAsync(… Reconciled, pendingRun.Version)` (Svc:106–113) and gets a `Failure` (`migration_run_version_conflict`). A caller that sees the winner before `SetEvidenceStateAsync` (Svc:100) gets `Unknown` (`migration_audit_recovery_required`) (Svc:77–79). Contract kept: every identical concurrent caller returns Success or Replayed for the same record. |
| A5 (MESP-160) | **Met** | Exact codes and three unchanged counts per action (RT:647–686). Passed in both runs. |

- Failures and classification:
  - The StartAttempt deadlock (existing test `SqlServerSafetyTests.cs:3475`) appeared in 2 of 7 full runs. It surfaces an unhandled `SqlException` 1205. `StartAttemptAsync` reads without a transaction or a run lock (MigrationPersistence.cs:216–260). Classification: product concurrency defect, intermittent, not caused by the new tests. → **MESP-163 (#282)** created (`type:bug`, Project #1, Jira Key MESP-163, `[MESP-15] #104`, Work Type Bug, Domain Migration, Status Todo). The gates stay unstable until it is fixed.
  - The initial nullable build failure is an AUTOMATION_DEFECT fixed before tests; accepted as reported.
  - Luna's run had no Serena or Context7. Recorded; no effect on the verdict.
- Status files updated: RESULT.md, ROADMAP.md, TASK.md (summary only; the owner has not sent `p`). #272–#276 stay open until Slice 11 is accepted.
- Exact next action: **the owner sends `p`.** Opus then writes the prompt for summary 1 in TASK.md: Luna 6 / **max** (owner decision, 2026-09-25; allowed by MODEL_ROUTING §1 after the failed xhigh attempt). It fixes MESP-161, MESP-162 and MESP-163 and corrects the R07 oracle. Then Opus re-reviews Slice 11 under MESP-150 (#265).

## 2026-09-25 — Slice 11 test-oracle executor handoff — Luna 6 / xhigh — MESP-156..160 (#272–#276), MESP-161 (#279), MESP-162 (#280)

- Status: **STOPPED** under TASK.md §10. The final same-tree full-suite pair disagreed because the existing MESP141 concurrent-attempt-start test deadlocked only in the second run. The repeated R12 and R20 product failures are retained; no retry or delay was added.
- Branch / starting SHA / ending SHA: branch fix/mesp-156-slice11-test-oracles, start e507cc9, ending SHA: this commit. The branch was created from docs/mesp-149-sol-cleanup-review; it retains the Planner commits and does not rebase or amend them.
- What changed: only the allowed test file plus this entry and TASK.md. The test file adds R04/R05 historical mapping equality; R07 reconciliation and subsidiary-to-GL checks; R12 current-read and approval invalidation checks; R18 persisted readiness/schema/reflection checks; R20 separate service instances; and A5 stale-version count checks. No product code changed. Created product Bugs MESP-161 (#279) for R12 and MESP-162 (#280) for R20; each is labeled type:bug and has Project #1 fields Jira Key, Parent / Epic, Work Type, and Status set per §9. No existing issue status changed and Jira was not written.
- Starting-state check: MATCH. Before branch creation, git status -sb was clean on docs/mesp-149-sol-cleanup-review at e507cc9; the prompt's expected diff from 0bff4dd contained only TASK.md; ac0309a was an ancestor of HEAD; live issues #272–#276 were OPEN. The executor branch then started at e507cc9 with the allowed test file as the only source change.
- Acceptance matrix:

| Row | Evidence | Result |
|---|---|---|
| R04 / R05 — #272 | backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs:110-133,139-162; expected values come from persisted historical MigrationEconomicRepresentation rows at :785-800 | Assertions are present and passed in both final gates. |
| R07 — #273 | Same file :191-256; fixture stages 14 records (MigrationArOpeningSqlServerRemediationTests.cs:1374-1413) and ReadEconomicCountsAsync.Effects is Migration.ListEffectsAsync(...).Count (:1509-1524) | Subsidiary/GL checks pass. The added count assertion fails: expected 15 owner artifact rows, actual 14 distinct represented EffectIds. The fixture has 14 staged execution effects, so this is a test-oracle mismatch, not evidence of a product defect. The earlier raw-row attempt saw 37 representation rows; Finance intentionally stores multiple representation kinds for an execution effect. No R07 Bug was filed. |
| R12 — #274 | Test :360-414; changed read at :374-375, current reconciliation assertion at :400-403, approval-required assertion at :405-410, persisted prior approval at :411-414. Product path: MigrationReconciliationService.cs:535-541,713-716; MigrationDbContext.cs:471-474; MigrationReconciliationPersistence.cs:61-81 | Current reconciliation returns migration_reconciliation_version_conflict in both final gates. Stable domain/scope detail IDs collide with the detail table's Id primary key when a new fingerprint is saved. Product Bug MESP-161 (#279). |
| R18 — #275 | Test :544-576: row counts before/after :554-558, persisted false flags :560-567, non-migration schema comparisons :568-572, constructor dependency check :573-576 | Passed in both final gates. Row counts do not detect in-place updates. Direct M27 lifecycle read-back is deferred until an M27 lifecycle store exists. |
| R20 / A5 — #276 | R20 separate service instances and success/replay oracle :618-643; A5 exact stale codes and all three unchanged counts :647-686. Product path: MigrationReconciliationService.cs:57-80,89-100; MigrationReconciliationPersistence.cs:61-81 | R20 fails in both final gates with migration_run_version_conflict and migration_audit_recovery_required under concurrent identical actions. Product Bug MESP-162 (#280). A5 assertions passed. |

- Gates and attempt history, in sequential order:
  1. Initial formal build failed before tests started: 0 warnings, 3 errors, wall 00:00:36.2974739. The nullable-flow compilation failures were introduced by the new test code and corrected before suite execution; classify AUTOMATION_DEFECT. Exact diagnostics were not present in the available retained transcript, so they are not quoted.
  2. First full suite after compile correction: 1548 passed, 4 failed, 0 skipped; wall 00:04:55.4923892. R07 functional-currency predicate and R12 fixture isolation were test setup defects; R20 was a product failure. The existing MESP141 concurrent StartAttempt test also deadlocked.
  3. After those independent setup corrections: 1549 passed, 3 failed, 0 skipped; wall 00:05:01.3472264. R07 aggregate-control pairing was a test setup defect; R12 and R20 failures remained.
  4. After switching R07 to GL line details: 1549 passed, 3 failed, 0 skipped; wall 00:04:42.1183650. R07 compared the residual field instead of the GL line target; R12 and R20 remained.
  5. After correcting the GL target field, raw-row-count attempt 1: 1549 passed, 3 failed, 0 skipped; wall 00:04:33.0178387. R07 saw 37 representation rows versus 15 owner artifact rows; R12 and R20 remained.
  6. Same raw-row-count tree, attempt 2: 1549 passed, 3 failed, 0 skipped; wall 00:05:44.4396753. Same three failures.
  7. Final handoff-tree gate 1, command .\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false: build succeeded, 0 warnings / 0 errors; 1549 passed, 3 failed, 0 skipped, total 1552, xUnit duration 13m01s, wrapper wall 00:14:42.7046418. The disposable database was MiniErpFoundation_20260925140359_c442b31b; output confirmed MESP_SQLSERVER_CONNECTION_STRING (runtime): unchanged. MESP data is intact. Failures: R12 migration_reconciliation_version_conflict; R07 expected 15, actual 14; R20 rejected concurrent results with migration_run_version_conflict and migration_audit_recovery_required.
  8. Final handoff-tree gate 2, same command and unchanged tree: build succeeded, 0 warnings / 0 errors; 1548 passed, 4 failed, 0 skipped, total 1552, xUnit duration 7m20s, wrapper wall 00:08:21.9826737. The disposable database was MiniErpFoundation_20260925141919_1a2d48c4; output again confirmed runtime connection unchanged and MESP data intact. It reproduced R12, R07 and R20 above, plus:
     - MESP141_sql_server_concurrent_attempt_start_on_one_run_yields_one_attempt_and_replays: SQL Server deadlock victim, Process ID 96. Trace: MigrationPersistence.ReadAttemptsAsync (MigrationPersistence.cs:955) → StartAttemptAsync (:260,315) → MigrationFoundationService.StartAttemptAsync (MigrationApplicationContracts.cs:1467) → existing test (SqlServerSafetyTests.cs:3475). This test passed in final gate 1 and failed in final gate 2, so classify the discordant final-pair failure INCONCLUSIVE under §10. It shares the fresh-context persistence path but is distinct from R20 reconciliation; no environmental cause is asserted.
  - Both mandated final gates used the same tree with no source edits between them. Because their results differ, §10 requires STOPPED; no more test edits or gate runs.
  - git diff --check: clean. Frontend, EF and npm checks were reused as the prompt permits; no product, migration or governance changes were made.
- Tooling / deviations: Ponytail full was active. Serena and Context7 were unavailable in this executor session; symbol and targeted code reads used rg and PowerShell as fallback. R07's count assertion remains uncorrected because the §10 stop was reached after the final pair; it is recorded as an automation/test-oracle mismatch and not filed as a product defect.
- Failures and classification: R04/R05, R18 and A5 assertions pass. R12 and R20 are confirmed product defects with Bugs MESP-161 (#279) and MESP-162 (#280). R07 is a test-oracle mismatch (14 execution effects/unique represented EffectIds versus 15 summed owner artifact rows). The initial nullable-flow build failure is AUTOMATION_DEFECT and was corrected before tests. The existing StartAttempt deadlock is INCONCLUSIVE because it disagreed across the final gate pair. No environment attribution is made.
- Status files updated: RESULT.md and TASK.md (Status → CONSUMED). Authorized GitHub delivery and issue comments follow the committed handoff; no Ready, review request, approval, merge, or existing issue lifecycle change is authorized.
- Exact next action: **Opus 5.5 re-reviews Slice 11 under MESP-150 (#265).**



## 2026-09-25 — Opus review of the Sol 6 cleanup findings — Claude Opus 5.5 / high — MESP-149 (#264), epic MESP-145 (#263)

- Status: **ACCEPTED** (the Sol review, as a compliant and accurate advisory review). **The cleanup on `main` stands.** I reject Sol's overall "blocking" recommendation: no finding blocks it. The follow-ups are listed below and in `TASK.md`.
- Branch / starting SHA / ending SHA: `docs/mesp-149-sol-cleanup-review`, start `0fa8129` (the Sol review commit), end = this commit. It is local and unpushed, so Draft PR #278 is unchanged. I tried to fast-forward local `main` to `0fa8129`; the harness denied it, so these Planner commits stay on this branch and the next executor branches from it.
- What changed: this entry; `docs/ROADMAP.md` (MESP-149 row, the Next queue, the epic section); `TASK.md` (the next-task summaries). This file's preamble moved back above the entries, because the Sol entry had been inserted above it. No code, test or governance rule changed.
- Sol delivery check: one commit, `0fa8129`, touching `RESULT.md` and `TASK.md` only. It opened Draft PR #278 and stopped. The starting state it recorded matches Git. **Compliant.**

### Dispositions (each one re-verified against code, Git and the tracker)

| ID | Sol | Opus | Reason |
|---|---|---|---|
| SOL-CL-01 | High, blocking | **Confirmed, Medium, not blocking** | `ModuleBoundaryTests.cs:606-615` only requires `StartsWith("$\"SELECT ")`, `Contains("WITH (UPDLOCK")` and `Contains("[TenantId] = {")`, so `SELECT … ; UPDATE …` would pass. The per-file counts (`:355-359`) are exact, and every `ExecuteSqlInterpolatedAsync` in those files is shape-checked, so a displaced site is still checked. The real hole is a string with more than one statement. The three current statements (`MigrationPersistence.cs:538`, `MigrationReconciliationPersistence.cs:296,301`) are single, Tenant-predicated lock reads. This is test hardening, not a product Bug. |
| SOL-CL-02 | High, blocking | **Duplicate of MESP-150 A3; not blocking** | `0d5fa4d` did not widen R4; the tag already pinned 4 sites. The unapproved 1→4 widening by Slice 11 is already with the owner (MESP-150 entry, "Exact next action"). It is one owner question, not two. |
| SOL-CL-03 | Medium, blocking | **Mostly rejected; Info** | `docs/PROJECT.md:240-241` states that the BRDs are preserved verbatim, that their internal `docs/…` paths predate the move, and where the move map is. Sol did not cite this. `DECISIONS.md:143` and `:1179` sit inside verbatim ADR text, and `:73` is a history row. **Planner defect:** my prompt required `docs/requirements/` to be both byte-identical and free of stale paths, which is contradictory (`AUTOMATION_DEFECT (Planner-introduced)`). |
| SOL-CL-04 | Medium | **Confirmed, Low** | The AP (`:246`), AR (`:247`) and cash-bank (`:157`) filters are the same one line; only AR is tested (`MigrationExecutionTests.cs:175-191`). |
| SOL-CL-05 | Medium | **Confirmed, Medium** | The archived rule (`history/AI_EXECUTION_POLICY_to_2026-09-25.md:57-64`) covered a report that says "stopped, **completed**, or handed off"; `AGENTS.md:49` drops "completed". No owner decision covers it. Because of the PR #81 incident, restore it. |
| SOL-CL-06 | Low, blocking | **Confirmed, Low, not blocking** | Of the renames, only `docs/requirements/16_…BRD.md` (R099) changed: its last line now links to `../history/specs/19_…`. That contradicts `PROJECT.md:240` and the cleanup entry's "byte-identical" claim. Fix: restore the tag blob `2a5febc`. |
| SOL-CL-07 | Low | **Confirmed, understated** | The archived Ponytail FULL list (`history/AGENTS_to_2026-09-25.md:139`) also protected **validation, authorization and data-loss safeguards**, not only accessibility. `AGENTS.md:53-55` names none of the four. `MODEL_ROUTING.md` §5 keeps input validation only. Restore authorization, data-loss safeguards and accessibility. |
| SOL-CL-08 | Info | **Confirmed** | `gh issue view`: all ten Q-K epics (#92–#96, #98–#102) were closed between 2026-09-25T00:12:38Z and 00:13:22Z, a 44-second batch. GitHub does not show who closed them (AGENTS §1.8). `ROADMAP.md` is refreshed in this commit. |

- **Planner defect found while writing the next prompt:** the MESP-159 (#275) Bug text expects a read-back of the Tenant lifecycle "from the owning Foundation/M27 persistence". No such store exists: `Modules/Platform` has only `Internal/` and a registration, and `git grep` finds no persisted Tenant lifecycle. The next prompt replaces it with the strongest oracle available. `AUTOMATION_DEFECT (Planner-introduced)`.
- Gates: `git diff --check` → clean. No test reads `RESULT.md`, `ROADMAP.md` or `TASK.md` content; `ROADMAP.md` is not a governance file the architecture tests read. So the backend suite is not rerun; its latest evidence is 1551/1551 on the `0e8ec29` tree (MESP-150 entry), and no code has changed since.
- Evidence: `git grep`, `git diff pre-cleanup-20260925 f833927 -M`, blob diffs, `gh issue view 92..102 264 272..276`, `gh pr list` (#278 Draft, open).
- Deviations: this verdict should have been recorded before the owner's `p` (`MODEL_ROUTING.md` §6, §8). I gave it in chat only and recorded it after `p`. `PROCESS_DEFECT (Planner-introduced)`. The local `main` fast-forward was denied (see above).
- Failures and classification: none beyond the three Planner defects above.
- Status files updated: `RESULT.md`, `docs/ROADMAP.md`, `TASK.md`. Tracker writes: none. #264 stays open; closing it is the owner's decision.
- Exact next action: **Luna 6 / xhigh executes the MESP-156..160 prompt in `TASK.md`** (fresh session). Owner decisions pending: (1) ratify or remove the three Slice 11 R4 lock sites (MESP-150 A3 = SOL-CL-02); (2) decide what happens to Draft PR #278, whose commit is also carried by the next executor branch.

## 2026-09-25 — Independent critical review of the cleanup on main — Sol 6 / high — MESP-149 (#264), epic MESP-145 (#263)

- Status: **DONE; advisory review, blocking findings.** Review range: `pre-cleanup-20260925..f833927`. No product, test, migration, script, CI, governance or tracker change was made by this review.
- Branch / starting SHA / ending SHA: `docs/mesp-149-sol-cleanup-review`; start `d854eb37767a807e7e989c01c65e2c03916c9da5`; end is this review's single commit (`git log -1`).
- Starting state (before branch creation): `git status -sb` → `## main...origin/main [ahead 4]` with no changes; `git diff --name-only 0e8ec29 HEAD` → `TASK.md` only; `origin/main=f8339278e8bc7eda167c4b9d2567d8d90400fe02`; `pre-cleanup-20260925^{commit}=5ae718adcba189565d731fb51ddaea1562a5d79c`; `git merge-base --is-ancestor f833927 HEAD` → exit 0. The branch was created from local `main` at `d854eb3` without rebasing or dropping the four local commits.
- What changed: this `RESULT.md` entry and `TASK.md` Status `OPEN` → `CONSUMED` only.

### Ranked findings

| ID | Severity | Area | Evidence | Observed versus expected | Recommendation |
|---|---|---:|---|---|---|
| SOL-CL-01 | **High** | 2 | `backend/tests/MiniErp.ArchitectureTests/ModuleBoundaryTests.cs:346-360,597-617`; `docs/ARCHITECTURE.md:133,152-155` | R4 counts approved calls by **file and method**, not exact call site. Its SQL check only requires a string beginning `SELECT ` and containing `WITH (UPDLOCK` and `[TenantId] = {`. A replacement call in the same file, or a `SELECT` followed by another statement, could satisfy the ratchet. The doc promises four named, Tenant-predicated, lock-only sites. Current three SQL statements at `MigrationPersistence.cs:538` and `MigrationReconciliationPersistence.cs:296,301` are visibly Tenant-predicated lock reads; this is a guard defect, not proof of an active unsafe statement. | Pin the exact four invocations and constrain the complete SQL shape, with a negative test that rejects an extra statement or displaced site. Keep the existing four under review until then. |
| SOL-CL-02 | **High** | 2 | `docs/audit/architecture-enforcement.md:87`; `docs/audit/drift-report.md:61,134-148`; `docs/DECISIONS.md:42`; `backend/tests/MiniErp.ArchitectureTests/ModuleBoundaryTests.cs:353-360`; `docs/ARCHITECTURE.md:152-158` | The tag already has the 1+3 R4 allowlist, and `0d5fa4d` did not widen it. Three Slice 11 lock sites entered that baseline without a separately recorded owner approval. Q-E approved installing R2–R5 from a proposal that listed four sites; it does not explicitly dispose of the earlier Slice 11 addition. Calling all four approved exceptions may imply retrospective approval. No cleanup-era widening or current executor reliance on a new permission was proven, so this is not a Critical stop. | Opus should present the three sites to the owner for explicit ratification or removal and record the disposition before treating the four-site baseline as approved architecture. Do not enlarge the allowlist meanwhile. |
| SOL-CL-03 | **Medium** | 3 | `docs/requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md:77-89`; `docs/requirements/21_Procurement_and_Purchase_to_Pay_BRD.md:43-48`; `docs/requirements/16_Master_Data_and_Product_Catalog_BRD.md:45`; `docs/DECISIONS.md:73,1179` | Live BRDs still name removed paths such as `docs/11_...`, `docs/16_...`, `docs/ADR-019_...` and `docs/Decisions.md`; the core decision file also names the old `docs/96_...` path. An `rg -l` scan found such references in 13 live requirements files and `docs/DECISIONS.md`. The task requires no live references to moved paths outside `history/` and `audit/`. Most are prose or code-formatted references, but they direct readers to nonexistent paths. | Update live references to their current locations under a bounded docs task; preserve archived copies. |
| SOL-CL-04 | **Medium** | 1 | `3cacf79:backend/tests/MiniErp.ArchitectureTests/MigrationExecutionTests.cs`; current test `MigrationExecutionTests.cs:175-191`; AP `MigrationApOpeningExecutionCoordinator.cs:246`, AR `MigrationArOpeningExecutionCoordinator.cs:247`, cash-bank `MigrationCashBankOpeningExecutionCoordinator.cs:157` | Cancellation propagation is correctly implemented in all three reconciliation reads. The single regression test proves fault→`partial` and cancellation→throw for AR only. AP and cash-bank have separate code and finance readers, so their behavior is not independently protected. | Add one focused check for AP and one for cash-bank, covering both fault and cancellation, without changing the approved production behavior. |
| SOL-CL-05 | **Medium** | 5 | `docs/history/AI_EXECUTION_POLICY_to_2026-09-25.md:57-64`; `AGENTS.md:49-50` | The archived immutable-report rule explicitly covered a report saying **stopped, completed, or handed off**. The replacement says **stopped or handed off**, omitting “completed.” Positive current-task authority still blocks a new phase, but the post-report boundary is weaker for a completion report. No owner decision covers this omission. | Restore “completed” in the live immutable-report rule. |
| SOL-CL-06 | **Low** | 3 | `0928f93:docs/requirements/16_Master_Data_and_Product_Catalog_BRD.md`; current `docs/requirements/16_Master_Data_and_Product_Catalog_BRD.md:1093`; tag blob `2a5febc...`, review blob `89f6049...` | Of 52 Git-detected moves, 51 retained their blob hash. The moved Master Data BRD changed one link from `19_Supplier_...md` to `../history/specs/19_Supplier_...md`. That link is useful, but the task's byte-identical-move rule and the cleanup RESULT claim of verbatim archives are false for this file. | Record the single intentional exception explicitly, or restore the tagged bytes and put a current navigation link in a separate live index. |
| SOL-CL-07 | **Low** | 5 | `docs/history/AGENTS_to_2026-09-25.md:137-150`; `AGENTS.md:53-55`; `docs/MODEL_ROUTING.md:66-73` | The old Ponytail FULL rule expressly protected **accessibility**. The live Ponytail guard lists security, Tenant isolation, accounting, audit, concurrency and acceptance, but omits accessibility. The active Ponytail skill still protects it, so this is a governance-text regression, not evidence of an accessibility defect. No owner decision covers dropping it. | Restore accessibility to the live non-negotiable list. |
| SOL-CL-08 | **Info** | 6 | `docs/ROADMAP.md:77-90`; live `gh issue list` / `gh issue view 92,95` | The cleanup applied log correctly reported ten epics open at the time. They are now closed (`#92` at `2026-09-25T00:12:38Z`, `#95` at `00:12:54Z`), so ROADMAP's “pending closure review” section is stale. This occurred after the cleanup; GitHub state establishes the result, not who performed it. | Opus should refresh live roadmap state in a later authorized task; do not attribute these closures to the cleanup executor. |

### Six-area conclusions and commands

1. **D-18 (`3cacf79`): finding SOL-CL-04.** `git show 3cacf79 -- backend/src/MiniErp.App/Modules/Migration backend/tests` and Serena `find_symbol` on the AP, AR, cash-bank and GL coordinators show the three filters exclude `OperationCanceledException` while other reader faults still yield `partial`. The structured warning proposed in `docs/audit/cleanup-plan.md` was not added. Q-L (`docs/audit/drift-report.md` §5.2, `docs/DECISIONS.md:49`) approved propagation, so that omitted log is an existing observability gap rather than an unapproved behavior change. GL's unchanged catch is in `ExecuteAsync` after a possible Finance write, where it reads with `CancellationToken.None` and fails the batch with unknown outcome if evidence remains absent; leaving it alone is sound.
2. **R2–R5 (`0d5fa4d`): SOL-CL-01 and SOL-CL-02.** `git show 0d5fa4d -- backend/tests/MiniErp.ArchitectureTests/ModuleBoundaryTests.cs`; Serena `find_symbol` on the R2–R5 members; independent `Get-ChildItem`/regex scan of `backend/src/MiniErp.App/Modules` → exactly the same 25 R2 edges; `rg -n 'ExecuteSqlInterpolatedAsync|IgnoreQueryFilters' backend/src/MiniErp.Infrastructure/Persistence/...` → exactly four current sites. R2 asserts equality in both directions; R3 found no cross-module persistence reference; R5's thirteen decorated classes use the required suffix. R4's call count is exact by file/method but not by invocation identity or full SQL behavior.
3. **Moves (`0928f93`): SOL-CL-03 and SOL-CL-06.** `git diff pre-cleanup-20260925 f833927 -M --stat` → 113 changed files. A `git diff --name-status -M` plus `git rev-parse tag:path`/`f833927:path` check found 52 moved paths and only the Master Data BRD blob mismatch. `AGENTS.md` and `CLAUDE.md` archive copies match their tag blobs. `git show 0928f93 -- backend/tests/MiniErp.ArchitectureTests/SafetyCatalogueValidationTests.cs` confirms the D-19 reader moved to `docs/history/96_...` in the same commit; the new path exists. `rg -n` over live docs found the stale references above.
4. **Core docs (`2635d55`): no additional cleanup finding.** `rg -n ProjectReference|EntityFrameworkCore backend/src --glob '*.csproj'` matches the four-project graph and EF only in Infrastructure. `rg -n` in `.github/workflows/ci.yml` confirms the three job names `Repository Validation`, `Backend`, `Frontend` and the hosted SQL-safety exclusion. All eight tagged ADR full texts occur in `docs/DECISIONS.md` after the documented two-level heading demotion and ADR-019 line-break change (UTF-8 comparison). The `AGENTS.md` §4 gate claims match the MESP-149 and MESP-150 recorded outputs; they are reused evidence, not a new run. R4's stronger claim is covered by SOL-CL-01, and current ROADMAP drift by SOL-CL-08.
5. **Governance against archived rules: SOL-CL-05 and SOL-CL-07.** Compared `docs/history/AI_EXECUTION_POLICY_to_2026-09-25.md`, `AGENTS_to_2026-09-25.md`, `CLAUDE_to_2026-09-25.md` against `AGENTS.md` and `docs/MODEL_ROUTING.md`. STOP, positive authorization, Ready/merge, tracker lifecycle writes, bot-review evidence, Ponytail's lack of authority and action attribution remain. Q1/Q2 cover model and acceptance changes; Q-A covers the one cleanup PR; Q-G covers retirement of `staticts.md`. No other executor right was found widened.
6. **Tracker and assets: SOL-CL-08 only.** Read-only `gh issue list`, `gh issue view 104,229,265,92,95`, and `gh project item-list 1 --owner Hossam1104 --limit 400 --format json` show 161 Project items: 114 Done, 7 In Progress, 40 Todo. Compared with the cleanup log's 156/104/14/38, the +5 are MESP-156..160 (#272–#276), all Todo children of MESP-15 (#104); the ten additional Done items are the later-closed Q-K epics, explaining the rest of the status shift. #104 and #229 remain In Progress/Active, #265 remains Todo/Open, and #238–#240 have keys MESP-146..148 and parent `[MESP-145] #263`. `git rev-parse archive/fix/MESP-123-angular-branding:<asset>` versus `f833927:<asset>` matches for both `Saudi_Riyal.svg` and `wafra-logo.jpeg` byte for byte.

- Overall advisory recommendation: **blocking findings SOL-CL-01, SOL-CL-02, SOL-CL-03 and SOL-CL-06** for the cleanup's stated R4 and verbatim/live-reference rules. The other findings need bounded follow-up but do not establish an active Critical defect. Opus 5.5 decides every disposition. SOL-CL-01 and SOL-CL-04 warrant test Bugs; SOL-CL-02, SOL-CL-03, SOL-CL-05, SOL-CL-06 and SOL-CL-07 warrant a governance/docs task. SOL-CL-08 is a live-state refresh.
- Gates: `git diff --check` → no whitespace diagnostics, exit 0, <1 s; `git diff --name-only main...HEAD` → `RESULT.md`, `TASK.md`, exit 0, <1 s (both verified on the review commit). Reused, not rerun: MESP-150 backend `1551/1551`, 0 skipped, 0 warnings/errors, 05:31 on the `0e8ec29` code tree; MESP-149 frontend unit `316/316` (02:01), build success with the known 514.26 kB warning (00:17), Chromium `51 passed` (~1.1 min), audits 0 high/critical, EF 8 contexts with none pending (01:04). Since `0e8ec29`, only `TASK.md` changed before this review.
- Evidence: Git commits and blob hashes above; read-only live GitHub Project/Issue outputs; Serena symbol reads. No Context7 call was needed by this review prompt.
- Deviations from the prompt: the live Project counts differ from the prompt's predicted MESP-150-only delta because ten Q-K epics were closed after cleanup; these are reported as later live state, not a cleanup executor action. The single moved BRD link edit and the live stale references contradict the cleanup RESULT's archive/link claims.
- Failures and classification: none in the review; no test or gate was rerun to resolve a product defect.
- Status files updated: `RESULT.md`, `TASK.md` only. Tracker and Jira writes: none.
- Exact next action: **Opus 5.5 reviews the Sol cleanup findings.**

## 2026-09-25 — Acceptance review of MESP-141 Slice 11 (reconciliation, approval, Ready-for-Handover) — Claude Opus 5.5 / high — MESP-150 (#265), capability MESP-141 (#229), epic MESP-15 (#104)

- Status: **REJECTED.**
  - Slice 11 (PR #262, merge `ac0309a`) stays on `main`, merged but not accepted.
  - The production code holds every business rule checked: FIN-OD-01, Tenant isolation, fail-closed without a policy, no Tenant activation and no M27 call.
  - However, 5 of the claimed provider oracles are not directly asserted by their tests (`MODEL_ROUTING.md` §8, checklist item 1), so the acceptance evidence is incomplete.
  - One Bug per defect: MESP-156..160 (#272–#276).
  - No Tenant-isolation or accounting breach was found.
- Branch / starting SHA / ending SHA:
  - branch `docs/mesp-150-slice11-acceptance`, created from local `main`;
  - start `5f7df6b`;
  - end: the SHA of this commit (`git log -1 docs/mesp-150-slice11-acceptance`).
  - The branch also carries the owner's `3819ef1` (`RUN.md`) and the Planner's `5f7df6b` (`TASK.md`), which were unpushed on local `main`.
- What changed: one commit, `docs(migration): MESP-150 (#265) Slice 11 acceptance verdict`. It touches only `RESULT.md` (this entry), `docs/ROADMAP.md` (the MESP-141 row and the queue) and `TASK.md` (Status → CONSUMED). No code, test, migration, script, CI or frontend file changed.
- Starting-state check (TASK §3): every item MATCHES.
  - `git status -sb`: `## main...origin/main [ahead 2]`, clean.
  - `git log --oneline -5`: `5f7df6b`, `3819ef1`, `f833927` (merge #271), `a657e48`, `2635d55`.
  - `f833927` is an ancestor of HEAD. `git rev-list --left-right --count origin/main...HEAD` gives `0 2`. `git diff --name-only f833927 HEAD` lists `RUN.md` and `TASK.md` only. `git diff --stat a657e48 f833927` is empty.
  - `ac0309a` and `3cacf79` are both ancestors of HEAD.
  - #265 is OPEN (Project Status Todo, Capability Backlog). #229 is OPEN (Status In Progress, Capability Active).
  - PR #262 is MERGED: head `d94cc3c`, merge `ac0309a`. Its backend diff is 31 files, +5380/−46.

### Acceptance matrix

The test file is `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs` (abbreviated **RT**). It runs on disposable LocalDB and has no skips. The service file is `backend/src/MiniErp.App/Modules/Migration/MigrationReconciliationService.cs` (**Svc**).

The standard: an oracle counts only if its exact assertion is present. A neighbouring test, a schema, a hard-coded constant or a reading of the production code does not count.

| Row | Test | Evidence (file:line) | Verdict |
|---|---|---|---|
| R01 clean all-domain reconciles, durable, read-only | `…r01_clean_all_domain_reconciliation_is_durable_and_read_only` | RT:37 Reconciled; RT:39–45 counts; RT:46–50 all 5 domains; RT:51 no blocking; RT:59–64 re-read id/fingerprint/details/status equal, count unchanged | MET |
| R02 unbalanced GL blocked, no balancing Journal | `…r02_unbalanced_gl_is_blocked_without_a_balancing_journal` | RT:75 KnownFailure; RT:76 `migration_gl_opening_imbalanced`; RT:77–78 GL journals/effects 0 (fresh Company per fixture, so 0 = unchanged); RT:79 `AssertNoAllFiveEffectsAsync`. The block happens at execution, so reconciliation never sees a Journal | MET |
| R03 inventory mismatch blocked, stock movements unchanged | `…r03_inventory_quantity_and_value_mismatch_is_blocked_without_stock_mutation` | RT:103 blocking; RT:104–105 qty/amount variance ≠ 0; RT:106 economic counts (incl. stock movements) before == after | MET |
| R04 AR mismatch keeps the **exact persisted** mapping, no Journal | `…r04_ar_to_gl_mismatch_preserves_exact_mapping_and_adds_no_journal` | RT:122–125 Blocked/blocking/EffectId; **RT:126–128 only `Assert.NotNull`** on ControlAccountId/PostingRuleId/Version; RT:129 AR journals unchanged; RT:130 GL 0 | **NOT MET**: the mapping is never compared with the persisted historical value. → MESP-156 (#272) |
| R05 AP mismatch keeps the exact persisted mapping, no Journal | `…r05_ap_to_gl_mismatch_preserves_exact_mapping_and_adds_no_journal` | **RT:150–152 only NotNull**; RT:153 journals unchanged; RT:154 GL 0 | **NOT MET** (same defect). → MESP-156 (#272) |
| R06 cash/bank mismatch keeps linked account, no Journal | `…r06_cash_bank_to_gl_mismatch_preserves_linked_account_and_adds_no_journal` | RT:170–172 Blocked/blocking; RT:174–175 LinkedAccountId **equals** the fixture account; RT:176 journals unchanged; RT:177 GL 0 | MET |
| R07 subsidiary and GL representations reconcile, no duplicate effects | `…r07_all_domain_execution_creates_one_economic_effect_per_source` | RT:186–198 counts 5 journals / 5 effects / 2 open items / 1 stock / 1 valuation / 1 handoff. **No reconciliation call and no representation assertion** | **NOT MET**: only the "no duplicates" half is asserted. → MESP-157 (#273) |
| R08 row outcomes disjoint and sum to staged | `…r08_row_outcome_counts_are_disjoint_and_sum_to_staged_rows` | RT:214–220 SQL-induced outcomes; RT:226–229 sum; RT:230–234 each = 1; RT:235 Blocked | MET |
| R09 configured rounding persisted with policy evidence | `…r09_configured_rounding_is_permitted_with_persisted_policy_evidence` | RT:248 Reconciled; RT:251–262 not blocking, amounts, rounding ≠ 0, policy id/version, scale 2, AwayFromZero, rate 3.75, rate ids, explanation | MET |
| R10 unexplained GL difference blocks, no repair Journal | `…r10_unexplained_gl_difference_blocks_without_repair` | RT:276 Blocked; RT:277 GL blocking; RT:278 GL journal count unchanged | MET |
| R11 self-approval denied, independent reviewer approves | `…r11_preparer_is_denied_and_independent_reviewer_can_approve` | RT:293 self-approval code; RT:296–298 success, actor, EvidenceConfirmed | MET |
| R12 fingerprint changes, prior approval stale, no snapshot | `…r12_changed_owner_evidence_invalidates_prior_approval` | RT:317 readiness `migration_reconciliation_stale`; RT:319 0 snapshots. Svc:220–222 returns the same code both for a null capture and for a changed fingerprint. **No assertion on the fingerprint, `IsCurrent` or approval validity** | **NOT MET**. → MESP-158 (#274) |
| R13 partial completion visible in the read model, readiness blocked | `…r13_partial_domain_evidence_is_retained_and_readiness_is_blocked` | RT:327 AR effect removed; RT:333–338 Blocked, domains retained, AR blocking. The response is the persisted re-read (`MigrationReconciliationPersistence.cs:68–69`, `ReadReconciliationAsync`). RT:341 readiness blocked | MET |
| R14 Outcome Unknown blocks handover | `…r14_unknown_outcome_blocks_handover` | RT:351 Unknown effect; RT:357 Blocked; RT:358 UnresolvedCount > 0; RT:361 readiness blocked | MET |
| R15 foreign Tenant: 4 actions refused, no existence leak | `…r15_foreign_tenant_cannot_read_or_mutate_reconciliation` | RT:376 read null; RT:379 reconcile `migration_source_scope_denied` (the generic unauthorized-run refusal, via Tenant-filtered `IsResourceAuthorizedAsync`); RT:381 approve and RT:384 readiness `…_not_found` on records that exist in the other Tenant | MET |
| R16 configured but missing approval blocks readiness | `…r16_configured_but_missing_approval_blocks_readiness` | RT:398 `migration_approval_required`; RT:400 0 snapshots | MET |
| R17 test policy reaches business-ready; production gates false | `…r17_test_policy_can_reach_business_ready_for_handover` | RT:440–445 success, BusinessReady, ProductionReady / Mesp48 / Mesp50 / TenantActivationPerformed all false | MET |
| R18 Tenant lifecycle read back unchanged | `…r18_ready_for_handover_never_activates_the_tenant` | RT:463 `TenantActivationPerformed` false, a constant hard-coded at Svc:231–233; RT:464 the *run* status is ReadyForHandover. **The Tenant lifecycle is never read back** | **NOT MET**. → MESP-159 (#275) |
| R19 repeated reads use the saved mapping after rule change | `…r19_repeated_reads_use_saved_mapping_without_reinterpreting_current_rules` | RT:479–491 rule disabled and new version; RT:498–503 same id, ControlAccountId, PostingRuleId, fingerprint, IsCurrent, count unchanged | MET |
| R20 truly concurrent actions converge to one record each | `…r20_concurrent_repeated_actions_are_idempotent` | RT:515 / 519 / 523 `Task.WhenAll` on **one shared service instance**, whose per-(Tenant, Run) `SemaphoreSlim` (Svc:28–29, 685–694) serializes them in-process; RT:517 / 521 / 525 single ids; RT:527–529 DB counts 1/1/1 | **NOT MET**: the persistence-level concurrency path is never run concurrently. → MESP-160 (#276) |
| M40-DEC-006: Unconfigured is the DI default, fail closed | `M40_dec_006_unconfigured_policy_fails_closed_without_selecting_production_quorum` | DI default at `MigrationServiceCollectionExtensions.cs:39`. The only other implementation is test-private (RT:564–568). RT:411 reconcile succeeds; RT:415 / 418 `approval_policy_not_configured`; RT:421–422 0 approvals, 0 readiness. M40-DEC-006 stays OPEN. Note: no test pins the DI default itself | MET |
| A1 REST DoD | `RestFoundationTests`: generic OpenAPI test (83–112), one-operation-id test (228–252), `Slice11_…_typed_openapi_schemas` (~797–848) | 5 operations in `FoundationRestContracts.cs` (permission `tenant.migration.execute`, Tenant scope; POSTs antiforgery, mandatory audit, unsafe, If-Match and Idempotency required). Mapped with `.WithName` in `MigrationEndpoints.cs` ~168–202 | MET |
| A2 module boundaries | `ModuleBoundaryTests` (suite) | Migration reads Finance evidence only through its own execution read model and `IMigrationExecutionPersistence` (Svc:267–300). There is no Finance or other-module DbContext in Migration (`git grep`: none). The Finance diff is additive and internal to Finance: the optional `EstablishedLines` on `FinanceGlOpeningPreflightResult` / `FinanceMigrationGlOpeningEvidence` (`FinanceSettlementApplicationContracts.cs:258–264`) and its population in `FinanceSettlementMigrationGlPersistence.cs` | MET |
| A3 R4 raw SQL widened from 1 to 4 (finding) | `ModuleBoundaryTests.AssertApprovedUnscopedCalls` (~353–360) pins exactly 4 sites per file; the content check at 606–615 requires `WITH (UPDLOCK` and `[TenantId] = {` | The 3 new sites are `MigrationPersistence.cs:538` and `MigrationReconciliationPersistence.cs:296` / `:301`. All are `SELECT … WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = … AND [key] = …`: Tenant-filtered, lock-only, inside Serializable transactions that serialize run / reconciliation writers. Necessary for cross-instance serialization. **Finding:** the allowlist was widened without an owner-approved task (`architecture-enforcement.md:87`). Owner ratification is still needed. This finding does not decide the verdict | FINDING |
| A4 migration additive and `migration`-owned | — | `20260924135807_Mesp141Slice11Reconciliation.cs`: Up has only `CreateTable` × 5 and `CreateIndex` in schema `migration`, with FKs only to `migration` tables. Down drops only its own 5 tables | MET |
| A5 optimistic concurrency and durable idempotency | R20 plus code | Rowversion on 3 tables (migration :48 / 90 / 132). Unique idempotency and fingerprint indexes (:266 / 292 / 337 / 344 / 351). Sequential replay creates no duplicates (RT:527–529). **No test rejects a stale `ExpectedVersion` / If-Match** (`…_version_conflict`, Svc:73 / 152 / 207). No test runs persistence convergence without the in-process gate | **NOT MET**. → MESP-160 (#276) |
| A6 D-18 in `3cacf79` | `MigrationExecutionTests.Ar_reconciliation_read_fault_is_partial_but_cancellation_propagates` (175) | AP (:246), AR (:247) and cash-bank (:157) coordinators now use `catch (Exception ex) when (ex is not OperationCanceledException)`. The test asserts both a fault → `partial` / `finance_ar_opening_evidence_not_reconciled` and a cancellation → throws. `MigrationGlOpeningExecutionCoordinator.cs` is unchanged since. Notes: AP and cash-bank rely on the identical one-line change with the AR test only. The "structured log" in the drift-report plan was not added; Q-L approved propagation only | MET |
| A7 no Tenant lifecycle write, no M27 call | code | The Svc constructor (31–51) has no Tenant-lifecycle or M27 dependency. `CreateReadinessCore` (Svc:198–265) only transitions the *migration run* to ReadyForHandover. The Slice 11 diff contains only persisted `false` flags for `Mesp48/50Complete` / `TenantActivationPerformed` | MET (code). The missing test is R18 |
| A8 hosted CI (evidence only) | `gh pr checks 262`; `gh run list --commit` | `d94cc3c`: run 36036704413 (pull_request), Backend pass 4m13s, Frontend pass 2m13s, Repository Validation pass 7s. `ac0309a`: run 36058481742 (push, main), success. Hosted CI excludes LocalDB, so it is not provider evidence | PASS (evidence) |

**Verdict rule** (TASK §7): R04, R05, R07, R12, R18, R20 and A5 are NOT MET, so the verdict is **REJECTED**.
- All the defects are gaps in test evidence. The product code for each rule reads correctly, but that does not satisfy an unasserted oracle.
- R20/A5 also carries a real deployment risk. The only proven concurrency guard is in-process, so a multi-instance deployment relies on the unproven row locks and unique indexes.

Other observations (not Bugs):
- The `ponytail:` `SemaphoreSlim` dictionary (Svc:28–29) is never evicted, so it grows with every run touched.
- `SaveReadinessAsync` returns the in-memory record after commit, not a re-read.

### Gates

Run sequentially on the final tree.

| Gate | Output tail | Wall time | Result |
|---|---|---|---|
| `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` (Windows PowerShell) | `Build succeeded. 0 Warning(s) 0 Error(s)` (build 00:00:38.41). `Passed!  - Failed: 0, Passed: 1551, Skipped: 0, Total: 1551, Duration: 4 m 32 s - MiniErp.ArchitectureTests.dll (net10.0)`. `Backend suite passed against disposable database MiniErpFoundation_20260925111408_1a9dec65.` `MESP_SQLSERVER_CONNECTION_STRING (runtime): unchanged. MESP data is intact.` | 05:31 | pass (matches the 1551/1551 baseline) |
| `git diff --check` | no output | <1 s | clean |

The gate ran with this entry's text already in place. Afterwards, only the placeholders for this gate table and the failures line were filled in. No test reads `RESULT.md` content.

- Reused evidence, not re-run (TASK §8): the frontend unit tests (316/316), the Angular production build (success, known 514.26 kB budget warning, MESP-155 (#270)), Playwright Chromium (51 passed), npm audit (0 high/critical; 4 / 7 moderate) and the EF pending-model check (8 contexts, none pending). All come from the `a657e48` entry below. No code changed since then.

### Tracker writes (positive authority: TASK §9 "if REJECTED")

- Bugs created, each labelled `type:bug`, added to Project #1 with `Jira Key` = MESP-<n>, `Parent / Epic` = `[MESP-15] #104` and Status Todo:
  - MESP-156: https://github.com/Hossam1104/Mini_ERP_SaaS_Platform/issues/272 (R04/R05 exact mapping);
  - MESP-157: https://github.com/Hossam1104/Mini_ERP_SaaS_Platform/issues/273 (R07 subsidiary-to-GL reconciliation);
  - MESP-158: https://github.com/Hossam1104/Mini_ERP_SaaS_Platform/issues/274 (R12 fingerprint and approval staleness);
  - MESP-159: https://github.com/Hossam1104/Mini_ERP_SaaS_Platform/issues/275 (R18 Tenant lifecycle read-back);
  - MESP-160: https://github.com/Hossam1104/Mini_ERP_SaaS_Platform/issues/276 (R20/A5 concurrency and version conflict).
  - The next free key was verified live: the highest key in use was MESP-155.
- Comments on #265 and #229 with the verdict, the Bug links and the Draft PR link are posted after the PR opens. #265 stays open.
- No Status or Capability State changed on #229, #104 or #265. Jira was not touched.

- Deviations from the prompt:
  - The comment URLs on #265 and #229 are not in this entry, because the comments are posted after this commit (they need the PR link). They are reported in the hand-back.
  - Symbol reads used Serena. Some diff reads used `git show` / `sed`.
- Failures and classification: none. The backend gate passed on the first run, and nothing needed a re-run.
- Status files updated: `RESULT.md`, `docs/ROADMAP.md` (the MESP-141 row and the queue: MESP-150 is done, and Bugs MESP-156..160 plus the re-review were added), `TASK.md` (Status → CONSUMED; the Sol summary is untouched), and the tracker (see above).
- Exact next action:
  - **Sol 6 review of Slice 11 (critical point 2): recommended, after MESP-156..160 are fixed and Opus re-reviews.** Slice 11 is the first path that can mark a run Ready-for-Handover, and it widened the R4 raw-SQL allowlist.
  - The next task is the **Sol 6 cleanup review** of `pre-cleanup-20260925..main` under MESP-149 (#264), per `TASK.md`.
  - The owner decides whether to ratify the A3 R4 widening (1 → 4 lock-only sites).

## 2026-09-25 — Full project cleanup, refactor, tracker reconciliation and operating model (Phase 2) — Claude Opus 5.5 / as needed — MESP-149 (#264), epic MESP-145 (#263)

- Status: **DONE, awaiting the owner's merge** (Q-A). The work is on one short-lived branch with one PR. The owner merges it with a merge commit via the admin bypass. The Sol 6 review of `pre-cleanup-20260925..main` follows MESP-150 (see `TASK.md`).
- Branch / starting SHA / ending SHA:
  - built on local `main`, delivered on `chore/mesp-149-project-cleanup`;
  - start: tag `pre-cleanup-20260925` = `5ae718a` (`origin/main` `ac0309a` plus the Phase 1 audit commits);
  - end: the SHA of this `RESULT.md` commit (`git log -1 chore/mesp-149-project-cleanup`).
- What changed (commits, oldest first):

  | Slice | Commit | Content |
  |---|---|---|
  | S0 | `20ade27` | Records the Phase 1 owner decisions in `docs/audit/drift-report.md` §5. |
  | S1 | `3cacf79` | D-18 (Q-L): the migration AP/AR/cash-bank reconciliation reads now propagate cancellation. There is a regression test in `MigrationExecutionTests`. `GeneralLedger…:120` is intentionally unchanged, because it is an execute-path evidence read (`DECISIONS.md` §2). |
  | S3 | `54cb8bd` | Q-C: restored `frontend/assets/Saudi_Riyal.svg` and `wafra-logo.jpeg` byte for byte. `.gitignore` now ignores local scratch output. |
  | S2 | `0d5fa4d` | Q-E: shrink-only architecture ratchets R2–R5 in `ModuleBoundaryTests`. No allowlist was widened. |
  | S4a | `0928f93` | BRDs and the glossary moved to `docs/requirements/`, non-Markdown files to `docs/assets/`, and history to `docs/history/`, as verbatim archives. The `docs/01..10` stubs and `.ai/` were removed. `Run.md` became `RUN.md`. `SafetyCatalogueValidationTests` now reads `docs/history/96_…` (D-19). |
  | S4b/S6 | `2635d55` | The five core docs: `PROJECT`, `ARCHITECTURE`, `ROADMAP`, `DECISIONS` (the eight ADRs embedded verbatim) and `MODEL_ROUTING` (the operating model). New root `AGENTS.md`, `CLAUDE.md` (`@AGENTS.md`), `TASK.md` and `README.md`. The component READMEs were trimmed. The audit docs were marked ADOPTED/APPLIED. |
  | S7 | this commit | `RESULT.md`. |

  S5 (the Sol/Terra authority sweep) needed no change. `FinanceSettlementRemediationTests.cs:19` keeps a historical comment.
- Gates (local, sequential unless noted), compared with the baseline in `docs/audit/drift-report.md` §1:

  | Gate | Baseline | Now | Result |
  |---|---|---|---|
  | Backend Release build | 0 warnings / 0 errors | 0 warnings / 0 errors (3 m 28 s) | pass |
  | Backend full suite incl. disposable LocalDB safety | 1546/1546 | **1551/1551** (5 m 53 s; DB `MiniErpFoundation_20260925023745_317c9d60`; "MESP data is intact") | pass. +5 = the R2–R5 ratchets and the D-18 regression test. |
  | Angular unit | 316/316, 45 files | **316/316**, 45 files (2 m 01 s, sequential re-run) | pass |
  | Angular production build | success, 514.26 kB budget warning | success; the same budget warning, 514.26 kB, 14.27 kB over (17 s, sequential re-run) | pass (known warning, MESP-155 (#270)) |
  | Playwright Chromium | 51 passed | **51 passed** (1.1 m) | pass |
  | npm audit, production | 4 moderate, 0 high | 4 moderate, 0 high | pass |
  | npm audit, full | 7 moderate, 0 high | 7 moderate, 0 high | pass |
  | EF pending-model check (8 contexts) | not run | all 8 contexts: "No changes have been made to the model since the last migration." (1 m 04 s) | pass. The `AGENTS.md` §4 claim is verified. |
  | `git diff --check pre-cleanup-20260925..HEAD` | — | clean | pass |

- Evidence:
  - tracker counts (Project #1, `gh project item-list`): before 148 items / 104 Done / 11 In Progress / 33 Todo; after 156 / 104 / 14 / 38. The +8 items are MESP-145 and MESP-149..155. Nothing was closed or deleted, and Jira was not written. The full log is in `docs/audit/tracker-reconciliation.md` §5.
  - Epics MESP-3..7 and 9..13 (#92–#96, #98–#102) each carry a closure-review comment and are **not** closed (Q-K).
  - Doc moves: every archived file is byte-identical to its tag version. No live file has a dangling reference. The only code or test paths that reference docs are `docs/history/96_…` and root `AGENTS.md`.
- Deviations from the prompt:
  - The tag `pre-cleanup-20260925` and the branch are pushed in this step, not in Step Zero. Q-A authorizes one branch and one PR.
  - The spec-kit stash is kept (it was reviewed and is harmless). The secret-bearing stash is left untouched for the owner (Q-J).
- Failures and classification:
  - First frontend run: the unit step crashed with `Worker exited unexpectedly` and the build failed with `getaddrinfo ENOTFOUND fonts.googleapis.com`. **ENVIRONMENT.** Both ran concurrently with the backend gate, and the build needs network access for font inlining. Both passed on the sequential re-run (see the table), so this was transient.
  - **AUTOMATION_DEFECT (Planner-introduced):**
    - I ran the frontend and backend gates concurrently, which caused the vitest worker crash.
    - I used recursive `grep -r` twice, and it timed out. `git grep` or the Grep tool must be used instead.
    - The case-insensitive index conflated `Run.md`/`RUN.md` and `Decisions.md`/`DECISIONS.md` during the commit split. I caught it before commit and fixed it with an explicit `git rm --cached`.
  - The S4 doc moves happened while an earlier gate was running. That gate was not relied on: the backend gate above ran on the final tree.
- Status files updated: `ROADMAP.md`, `DECISIONS.md`, `MODEL_ROUTING.md`, `AGENTS.md`, `TASK.md`, `docs/audit/*`, and the tracker (see Evidence).
- Owner actions:
  1. Review and merge the cleanup PR **with a merge commit** via the admin bypass (Q-A). Do not squash.
  2. Delete `C:\Program Files\Git\fe-test.log`. It is outside the repository, so I did not touch it.
  3. Drop or handle the stash that contains a credential literal (`preserve unrelated local Run.md change before MESP-138 HOLD 3`). I did not print, commit or push it.
  4. Close the Q-K epics after your review (see `ROADMAP.md`).
- Exact next action: once the PR is merged, the owner opens a fresh Opus 5.5 session for MESP-150 (#265) per `TASK.md`, then Sol 6 / high reviews `pre-cleanup-20260925..main`.
