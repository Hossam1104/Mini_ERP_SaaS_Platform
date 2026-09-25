# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **CONSUMED** (written by the Planner, Claude Opus 5.5, on 2026-09-25 after the owner sent `p`).

Routing: **Luna 6, effort xhigh.** Open a **new session**.
- These are new test oracles on money, stock, concurrency and "no Tenant activation" paths. That is
  not a contained, already-diagnosed Sonnet fix.

```markdown
# MESP-156..160 (#272–#276) — Slice 11 test-oracle evidence Bugs
Model: Luna 6 — Effort: xhigh — Fresh session

## 1. Role and authority
- You are the **executor**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you, §1
  especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work items: MESP-156 (#272), MESP-157 (#273), MESP-158 (#274), MESP-159 (#275) and MESP-160 (#276).
  All are Bugs under MESP-15 (#104), capability MESP-141 (#229). They came out of the MESP-150 (#265)
  acceptance review of Slice 11 (PR #262, merge `ac0309a`).
- **This task is test-only.** Every one of these Bugs is an acceptance-evidence defect: the product
  code reads correctly, but the claimed oracle is not asserted. You add or strengthen assertions. You
  never change product code.
- Branch: `fix/mesp-156-slice11-test-oracles`.

## 2. Read order
1. `AGENTS.md`, then this prompt.
2. The two newest `RESULT.md` entries (Opus review of the Sol cleanup; Sol cleanup review) only for the
   Git state. Then the **MESP-150 (#265) Slice 11 acceptance entry**: its acceptance matrix rows R04,
   R05, R07, R12, R18, R20 and A5 are the specification of this task.
3. The five Bug bodies: `gh issue view 272 273 274 275 276` (read-only). **§5 of this prompt overrides
   MESP-159 (#275)'s "Expected" section**; see §5.4.
4. BRD 40 only where the Bugs cite it: `docs/requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md`
   §7.1, §14.1, §14.3 and §23.1.
5. Code, symbol by symbol:
   - **Serena:** call `initial_instructions` once. Use `get_symbols_overview` on
     `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs` (638
     lines; don't read it whole). Use `find_symbol` for the R04, R05, R07, R12, R18, R19 and R20
     tests, the `Service(...)` helper (~612–624) and `ReconciliationCountAsync`. R19 (~470–504) is the
     pattern for exact mapping equality.
   - The fixture `MigrationEconomicOpeningRemediationSqlServerSafetyTests.ArSqlFixture`, in the file
     `MigrationArOpeningSqlServerRemediationTests.cs` (~911; the file and class names differ). Find it
     with `find_symbol`, and read only the members you use.
   - Service: `MigrationReconciliationService` (`ReconcileAsync`, `ApproveAsync`,
     `CreateReadinessAsync`, `ReadAsync`, `ApprovalsSatisfied`, the `workflowGates` field). The
     rejection codes are at `:73`, `:153`, `:161`, `:208`, `:222` and `:229`.
   - Persistence: `MigrationReconciliationPersistence.cs` (locks at `:294-302`, replay at `:71-81`)
     and `MigrationDbContext` (`EconomicRepresentations`, `ReconciliationApprovals`,
     `HandoverReadiness`).
- **Context7:** only if you need an EF Core or xUnit API you are unsure of.
- **Ponytail:** full. It never trims assertions, gate output or the RESULT.md entry.
- Use `git grep` or the Grep tool, never recursive `grep -r`. If a plugin is missing, say so in one
  line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status -sb`: the tree is clean and you are on `docs/mesp-149-sol-cleanup-review`. This branch
  carries the unpushed Planner commits; local `main` does not have them.
- HEAD descends from `0bff4dd` (the Opus verdict), and `git diff --name-only 0bff4dd HEAD` lists only
  `TASK.md`.
- `git merge-base --is-ancestor ac0309a HEAD` succeeds (Slice 11 is present).
- `gh issue view 272 273 274 275 276 --json number,state`: all OPEN.
- Any mismatch: stop (§10).

## 4. Rules to preserve
- **Never weaken, skip or delete an existing assertion or test** (AGENTS §5). You only add to them.
- **An oracle counts only if its exact assertion is present.** A neighbouring test, `Assert.NotNull`,
  a schema, a hard-coded constant or a reading of the production code does not count. That is the
  standard MESP-150 applied, and it is the standard Opus will apply to your result.
- **Read persisted state where the oracle is about persistence.** Use a fresh `MigrationDbContext` or
  `FinanceDbContext` built from the fixture's options (the pattern of `ReconciliationCountAsync` and
  `MutateFinanceJournalAsync`), not the object the service returned.
- **Tenant isolation:** every direct DB read in a test goes through a Tenant-scoped context for the
  fixture's Tenant. No `IgnoreQueryFilters`, no raw SQL against module tables in test code, with one
  exception: the `sys.*` catalogue query of §5.4.
- **No fixed sleeps.** Concurrency comes from `Task.WhenAll`, never from delays. No hard-coded
  environment data or secrets.
- **FIN-OD-01:** tests never create Journals to make an oracle pass. Only the existing fixture paths
  create owner effects.
- **R5:** new tests stay inside `MigrationReconciliationSqlServerSafetyTests`, so hosted CI keeps
  excluding them and the LocalDB gate runs them.

## 5. Scope and file allowlist

### 5.1 MESP-156 (#272): R04 and R05 exact mapping
Replace nothing. Add equality assertions after the existing `Assert.NotNull` lines (RT:126–128 and
RT:150–152). The detail's `ControlAccountId`, `PostingRuleId` and `PostingRuleVersionNumber` must each
**equal** the value recorded when the effect was executed. Read that value from the persisted
`MigrationEconomicRepresentation` for the effect, or from the Finance source effect / Journal. Use
whichever one R19 already treats as the historical record, and say which one in RESULT.md.

### 5.2 MESP-157 (#273): R07 subsidiary-to-GL reconciliation
Keep the existing count assertions. Then call `ReconcileAsync` on the same mixed run and assert all of
these:
- the result is Reconciled;
- each subsidiary domain row (AR, AP, cash-bank, inventory) is not blocking and has a **zero**
  variance against its GL control line;
- the GL control representation lines are present;
- the persisted `EconomicRepresentations` count for the run is **exactly one per owner effect**. Take
  the expected number from the owner-effect counts the test already asserts, not a new literal.

### 5.3 MESP-158 (#274): R12 fingerprint and approval staleness
Keep the existing `migration_reconciliation_stale` and 0-snapshot assertions. Add:
- **Changed fingerprint, proven directly:** `ReadAsync` reports `IsCurrent == false` for the saved
  reconciliation. Alternatively, the recomputed evidence fingerprint differs from the saved one.
  Either way, assert it through a public read path or persisted state. This separates the "changed
  fingerprint" case from the "null capture" case, which `:220-222` reports with the same code.
- **Prior approval not counted:** reconcile again to get the current reconciliation, then assert that
  readiness for it is rejected with `migration_approval_required`: the old approval does not carry
  over. Also assert the persisted approval still references the old reconciliation id and
  fingerprint.

If no public read path or persisted state can tell the two cases apart without a product change,
stop (§10).

### 5.4 MESP-159 (#275): R18 no Tenant activation (Planner-corrected oracle)
The Bug text expects a Tenant lifecycle read-back "from the owning Foundation/M27 persistence". **No
such store exists in the code** (`Modules/Platform` has only `Internal/` and a registration). Opus
recorded that as a Planner defect. Implement this oracle instead, keeping the existing assertions:
1. **Persisted snapshot:** after `CreateReadinessAsync`, read the `HandoverReadiness` row back from a
   fresh `MigrationDbContext`. Assert that `TenantActivationPerformed`, `ProductionReady`,
   `Mesp48Complete` and `Mesp50Complete` are all `false` (use the exact column names).
2. **No writes outside the `migration` schema:** read the row counts of every user table grouped by
   schema (`sys.tables` / `sys.schemas` / `sys.partitions`, `index_id IN (0,1)`) immediately before and
   after `CreateReadinessAsync`, on the fixture's disposable database. Assert that every schema other
   than `migration` is unchanged. If the fixture uses more than one database, do this for each.
   Record in RESULT.md that row counts do not catch in-place updates.
3. **No lifecycle dependency:** by reflection, assert that no constructor parameter of
   `MigrationReconciliationService` has a type from a `MiniErp.App.Modules.Platform` or
   `MiniErp.App.Modules.Identity` namespace.

Record in RESULT.md that the direct M27 lifecycle read-back is deferred until an M27 lifecycle store
exists.

### 5.5 MESP-160 (#276): R20 real concurrency and A5 version conflicts
- **R20:** issue each `Task.WhenAll` action (reconcile, approve, readiness) through **separate
  `MigrationReconciliationService` instances**, one `Service(fixture, policy)` call per concurrent
  task, so that the per-instance `workflowGates` semaphore cannot serialize them. Keep the
  convergence assertions: exactly 1 reconciliation, 1 approval and 1 readiness row, and a single id
  per action.
  - First verify that the shared `fixture.Migration` persistence opens a DbContext per operation and
    is safe to share. If it holds one shared DbContext, give each task its own persistence instance
    built from the fixture's options. If that needs a change outside §5.6, stop (§10).
  - Every concurrent result must be a success or a replay of the same record. Assert that; an
    unexpected rejection code fails the test.
- **A5, new test(s):** for each of reconcile (`migration_run_version_conflict`), approve and
  readiness (`migration_reconciliation_version_conflict`), send a **stale** `ExpectedVersion` /
  version. Assert the exact rejection code and that nothing was persisted: the reconciliation,
  approval and readiness counts are unchanged.

### 5.6 File allowlist
- `backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs`: the
  changes above.
- `backend/tests/MiniErp.ArchitectureTests/MigrationArOpeningSqlServerRemediationTests.cs`: **only**
  additive, read-only accessors on `ArSqlFixture` if §5 truly needs one. Never change existing
  members.
- `RESULT.md` (one new top entry) and `TASK.md` (Status → CONSUMED).
- No other file.

## 6. Out of scope
- Any product code, migration, script, CI or governance doc, including the `ponytail:` gate-eviction
  note (`MigrationReconciliationService.cs:28`).
- The R4 allowlist and `ModuleBoundaryTests` (SOL-CL-01 and the owner's R4 ratification are separate).
- The D-18 AP/cash-bank tests (SOL-CL-04, separate task).
- Deciding M40-DEC-*, or configuring a Production approval policy. The tests keep their private
  `TestApprovalPolicy`.
- Closing, reopening or changing the Status of any issue. Jira in any form.

## 7. Acceptance matrix (what Opus will check)

| Bug | Oracle | Evidence Opus expects |
|---|---|---|
| MESP-156 | R04 and R05 mapping equals the persisted historical mapping | `Assert.Equal` on all three fields in both tests, against a value read from persistence |
| MESP-157 | R07 subsidiaries reconcile to GL, one representation per effect | `ReconcileAsync` call; per-domain non-blocking and zero variance; GL lines present; exact representation count |
| MESP-158 | R12 changed fingerprint; prior approval not counted | `IsCurrent == false` or fingerprint inequality; `migration_approval_required` on the current reconciliation; persisted approval references the old one |
| MESP-159 | R18 no activation, no side effect outside `migration` | persisted snapshot flags false; per-schema row counts unchanged; constructor-dependency check |
| MESP-160 | R20 converges across instances; A5 stale versions rejected | separate instances per task; 1/1/1 rows; the exact conflict codes; unchanged counts |
| All | No weakening | `git diff` shows no removed or relaxed assertion and no `Skip` |

Every row needs `file:line` evidence in RESULT.md.

## 8. Gates (paste the output tail and the wall time)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` from Windows PowerShell, on the final tree:
   0 warnings, 0 errors, **all passed, 0 skipped**. The total must be 1551 plus exactly the tests you
   added. It must also print the disposable-database and "MESP data is intact" lines.
2. **Run gate 1 a second time**, sequentially, on the same tree: concurrency tests must be stable. A
   failure on either run counts (§10).
3. `git diff --check`: clean.
4. `git diff --name-only 0bff4dd HEAD`: only the §5.6 files.
- The script has no test filter, so every run is the full suite. Never run gates concurrently. Never
  point a test at a non-disposable database.
- Reused evidence, not re-run: frontend, npm audit and EF checks (no production or model change).

## 9. Git / PR delivery (positive authority, exactly this)
- Create `fix/mesp-156-slice11-test-oracles` from the current HEAD. Keep the unpushed Planner and Sol
  commits; never drop, rebase or amend them.
- Commits: `test(migration): MESP-156..160 (#272–#276) <what>`. One per Bug is fine. Self-review the
  staged diff before each commit.
- Push the branch. Open ONE **Draft** PR to `main`. Its body gives the per-Bug evidence table, the
  gates and the §3 Git note (the branch also carries Planner/Sol commits already in Draft PRs #277 and
  #278).
- **Tracker writes, only these:** one comment on each of #272–#276 with the PR link and that Bug's
  evidence (`file:line`, gate result). If §10 product-defect applies: create one Bug per defect,
  following `MODEL_ROUTING.md` §11. That means label `type:bug`, Project #1, `Jira Key` = the next
  free MESP-n (verify live), `Parent / Epic` = `[MESP-15] #104`, Status Todo.
- **NOT authorized:** Ready, reviewers, approval, merge, update-branch, rebase, force-push, push to
  `main`, closing or reopening any issue, any Status or Capability State change, and any other
  tracker write.

## 10. Stop conditions (stop, write a `STOPPED` entry, do not improvise)
- The starting state does not match.
- You would need to change a file outside §5.6, or change product code.
- **A strengthened or new test fails on the product** (not on your test code). That is a product
  defect.
  - Keep the failing test as written. Never weaken or skip it.
  - Classify the failure, file the Bug (§9), commit, push and open the Draft PR with the red gate
    output.
  - Finish any remaining independent Bugs only if the failure cannot affect them; otherwise stop at
    once.
- The two gate-1 runs disagree (a flaky test). Record both outputs and stop. Do not add retries or
  delays.
- An oracle cannot be observed through public reads or persisted state without a product change
  (§5.3, §5.5).
- After the Draft PR and the issue comments: STOP. No further mutation.

## 11. Hand-back
- Add one `RESULT.md` entry at the top, directly under the file preamble, using the
  `MODEL_ROUTING.md` §7 template, with Status `DONE` or `STOPPED`. It contains:
  - the starting-state output;
  - the §7 matrix with `file:line` evidence;
  - both gate-1 outputs with wall times;
  - the §5.4 notes (in-place updates, deferred M27 read-back);
  - any deviations, and every failure with its classification.
- **Exact next action:** "Opus 5.5 re-reviews Slice 11 under MESP-150 (#265)."
- In `TASK.md`, set this prompt's Status to **CONSUMED**. Leave the next-task summaries untouched.
- Commit and deliver per §9. Then stop.
```

## Next-task summaries (Planner, 2026-09-25)

### 1. Slice 11 evidence Bugs MESP-156..160 (#272–#276)

The prompt is above. After it: Opus re-reviews Slice 11 under MESP-150 (#265). If Slice 11 is
accepted, the Sol 6 review of Slice 11 follows (critical point 2).

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
