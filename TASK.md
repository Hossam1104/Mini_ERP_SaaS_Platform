# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **CONSUMED** (written by the Planner on 2026-09-25 after the owner sent `p`; executed by Claude Opus 5.5 on 2026-09-25; verdict REJECTED; see `RESULT.md`).

Routing: **Claude Opus 5.5, effort as needed.** Open a **new session**. This is an acceptance review,
which is Opus's role (`MODEL_ROUTING.md` §1). Luna 6 and Sonnet 5 hold no acceptance authority.

```markdown
# MESP-150 (#265) — Acceptance review of MESP-141 Slice 11 (reconciliation, approval, Ready-for-Handover)
Model: Claude Opus 5.5 — Effort: as needed — Fresh session

## 1. Role and authority
- You are the acceptance authority for MESP-141 (#229) Slice 11 only. The verdict is `ACCEPTED` or
  `REJECTED`. There is no conditional acceptance.
- `AGENTS.md` binds you, §1 especially. Authorization is positive. Every action this prompt does not
  list is forbidden.
- **No product code, test code, migration or script changes.** A defect you find becomes a tracker
  Bug (§9). You never fix it here, and you never fix it in test code.
- Work items: MESP-150 (#265), capability MESP-141 (#229), parent epic MESP-15 (#104).
- Branch: `docs/mesp-150-slice11-acceptance`.

## 2. Read order
1. `AGENTS.md` (loaded by `CLAUDE.md`), then this prompt, then the newest `RESULT.md` entry.
2. Everything under the "R01-R20 executable provider map" heading in the PR #262 body. That table is
   the Slice 11 matrix. Read it with `gh pr view 262 --json body --jq .body`. Also read the Slice 11
   activation, correction and continuation comments on #229, IDs `5811630326`, `5812297427` and
   `5812483949` (`gh api repos/Hossam1104/Mini_ERP_SaaS_Platform/issues/comments/<id> --jq .body`).
   If the repo slug differs, take it from `gh repo view`.
3. `docs/requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md`. Read only §7.1, §13, §14
   (14.1–14.3), §15, §17, §18, WF-05, WF-06 and §23.
4. `docs/ARCHITECTURE.md` §2–§3 and §7 (layers, module boundaries, enforcement).
   `docs/audit/architecture-enforcement.md` (the R4 note at line 87). `docs/audit/drift-report.md`
   D-15 and D-18. `docs/DECISIONS.md` Q-B, Q-L and the 2026-09-25 rows.
5. The PR #262 diff: `git diff ac0309a^1 ac0309a -- backend/`. The merge brought 33 files. Ignore
   `.ai/` and `docs/staticts.md`, which are archived.
- **Serena:** call `initial_instructions` once. Then use `get_symbols_overview` on
  `MigrationReconciliationService.cs`, `MigrationReconciliationContracts.cs`,
  `MigrationReconciliationPersistence.cs` and `MigrationReconciliationSqlServerSafetyTests.cs`.
  Use `find_symbol` and `find_referencing_symbols` on `IMigrationReconciliationApprovalPolicy` and
  `UnconfiguredMigrationReconciliationApprovalPolicy`. Read test bodies at symbol level, not whole
  files.
- **Context7:** not needed. No library API is in question.
- **Ponytail:** full. It never trims the evidence table or the RESULT.md entry.
- If a plugin is missing, say so in one line and fall back to targeted line-range reads. Use
  `git grep` or the Grep tool, never recursive `grep -r`.

## 3. Starting-state check (record the output in RESULT.md)
- `git status -sb`: the tree is clean and you are on `main`.
- HEAD descends from `f833927` (the PR #271 merge), and `git diff --name-only f833927 HEAD` lists only
  `RUN.md` and `TASK.md`. Local `main` is 2 commits ahead of `origin/main`: the owner's `3819ef1`
  (`RUN.md`) and the Planner's `TASK.md` commit. **Keep both.** Never drop, rebase or amend them.
- `git diff --stat a657e48 f833927` is empty. That proves the cleanup's gate evidence (1551/1551)
  applies to this tree.
- `ac0309a` is an ancestor of HEAD (`git merge-base --is-ancestor ac0309a HEAD`), and so is `3cacf79`
  (D-18).
- #265 is OPEN. #229 is OPEN, with Project Status In Progress and Capability State Active.
- Any mismatch: stop (§10).

## 4. Rules the review must hold Slice 11 to
- Release 1 is B2B ERP. There is no Wafra-specific behavior and no ZATCA claim.
- Finance owns accounting (FIN-OD-01). Migration never creates balancing Journals, repair Journals or
  stock adjustments.
- Tenant isolation: Tenant A can never read, reconcile, approve or create readiness evidence for
  Tenant B.
- Fail closed: without a configured approval policy, approval and readiness are refused and no
  decision records are written. M40-DEC-006 (production quorum) stays OPEN. The review decides
  nothing under M40-DEC-001..006.
- Ready-for-Handover is a business snapshot. It never activates a Tenant, never calls M27, and never
  claims MESP-48/MESP-50 or production readiness.
- The layers are Api → Infrastructure → App → Contracts. Modules call each other only through public
  contracts. Allowlists only shrink.
- The REST/API Definition of Done in `AGENTS.md` §2 applies to every Slice 11 REST operation.
- Migrations are additive and module-owned.

## 5. Scope and file allowlist
**Scope:**
1. Verify every row of the acceptance matrix (§7) against the **exact** test assertions and the
   production code path. Name the test and cite the `file:line` of the asserting lines.
   - An oracle covered only indirectly, by an adjacent test, a schema, or a comment, is **NOT MET**
     (`MODEL_ROUTING.md` §8 checklist).
   - Confirm that the provider tests actually execute under the backend gate. They must not be
     silently skipped or early-returned when the safety connection is present. Check the fixture's
     `SqlServerSafetyFixture` behavior.
2. Verify the architecture rows A1–A8.
3. Run the gates (§8) once, at the end, on the final tree.
4. Write the verdict and update the status files (§11).

**Files you may change:** `RESULT.md`, `docs/ROADMAP.md`, `TASK.md`. No other file. Any other change
is a deviation that you revert before you commit.

## 6. Out of scope
- Any code, test, migration, script, CI, frontend or `frontend/assets` change.
- Deciding or narrowing M40-DEC-001..006.
- Activating later MESP-141 slices. Any MESP-142 (#230) change.
- Changing the Status or Capability State of #229 or #104. Closing any epic.
- Sol 6's cleanup review, the second summary below. Do not start it.
- Jira, in any form.

## 7. Acceptance matrix (one evidence row each in RESULT.md: test, file:line, verdict MET / NOT MET)
**R01–R20:** use the PR #262 table. The tests are in
`backend/tests/MiniErp.ArchitectureTests/MigrationReconciliationSqlServerSafetyTests.cs`. For each row,
check that the asserted result in the table is what the test actually asserts. Rows that need
particular care:
- **R02, R04, R05, R06 and R10:** the test asserts that the Journal count is unchanged. A message
  alone is not enough.
- **R03:** the test asserts that the stock movement count is unchanged.
- **R12:** the evidence fingerprint changes, the prior approval becomes stale, and no snapshot is
  persisted.
- **R13:** partial completion is visible in the read model, not only in the logs.
- **R14:** `Outcome Unknown` blocks handover.
- **R15:** all four actions are refused for a foreign Tenant (read, reconcile, approve, readiness),
  and the refusal does not leak whether the resource exists.
- **R18:** the Tenant lifecycle state is read back unchanged.
- **R20:** the actions really run concurrently, and each converges to exactly one record.

**M40-DEC-006 case:** in
`M40_dec_006_unconfigured_policy_fails_closed_without_selecting_production_quorum`, check that
`UnconfiguredMigrationReconciliationApprovalPolicy` is the **default DI registration**. Also check
that no production code path selects a synthetic or test policy.

**Architecture rows:**
- **A1, REST DoD.** Every Slice 11 route in `MigrationEndpoints.cs` is in
  `FoundationRestContracts.cs`, with its route, permission, scope, antiforgery, audit and
  unsafe-effect metadata. It is in the generated OpenAPI document with a stable `operationId`,
  a summary and explicit responses. `RestFoundationTests` covers it.
- **A2, module boundaries.** Slice 11 touched Finance files
  (`FinanceSettlementApplicationContracts.cs` and `FinanceSettlementMigrationGlPersistence.cs`).
  Migration reads Finance evidence only through a public contract. There is no cross-module
  DbContext or table access, and Finance still owns the evidence.
- **A3, R4 raw SQL.** Slice 11 widened the raw-SQL sites from 1 to 4
  (`architecture-enforcement.md:87`). Each of the three new row locks is Tenant-filtered, lock-only
  and necessary, and the R4 ratchet in `ModuleBoundaryTests` pins the exact sites. The widening is
  **reported** to the owner either way. Treat it as a finding. It is not automatically a rejection.
- **A4, migration.** `20260924135807_Mesp141Slice11Reconciliation` is additive: it has no drops, no
  destructive alters and no data rewrites, and the `migration` schema owns it.
- **A5, concurrency and idempotency.** Optimistic concurrency and durable idempotency keys are on
  the new aggregates, and a replay does not create duplicates.
- **A6, D-18.** In `3cacf79`, the AP/AR/cash-bank reconciliation reads propagate
  `OperationCanceledException`, and other faults still yield `partial`. The regression test asserts
  both. `MigrationGlOpeningExecutionCoordinator.cs` is intentionally unchanged (`DECISIONS.md`).
- **A7, no activation.** No Slice 11 code path writes Tenant lifecycle state or calls M27.
- **A8, hosted CI.** Record the check results for PR #262's head and for `ac0309a` on `main`
  (`gh pr checks 262`; `gh run list --commit <sha>`). These are evidence only. Hosted Backend CI
  excludes LocalDB, so it never replaces §8.

**Verdict rule.** `ACCEPTED` only if every R row, the M40-DEC-006 case and A1, A2, A4, A5, A6 and A7
are all MET, and the gates pass. Otherwise the verdict is `REJECTED`, with a Bug for each defect.
Neither A3 nor A8 decides the verdict alone. Both are reported with a recommendation.

## 8. Gates (paste the real output tail and the wall time; run sequentially, never concurrently)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false`. The expected result is 0 warnings / 0 errors
   and **1551/1551**, with 0 skipped. Record the disposable DB name and the "MESP data is intact"
   line. This gate covers the 21 Slice 11 provider cases and the architecture tests that read the
   docs you edit.
2. `git diff --check`: clean.
- **Reused, not re-run:** the frontend gates, npm audit and the EF pending-model check. The
  `a657e48` evidence in `RESULT.md` applies, because no code changed. State this in the entry.
- If pwsh is missing, invoke the script from Windows PowerShell.

## 9. Git, PR and tracker delivery (positive authority, exactly this)
**Git and PR.** You may do only these:
- create the branch `docs/mesp-150-slice11-acceptance` from local `main` HEAD;
- make **one** commit, `docs(migration): MESP-150 (#265) Slice 11 acceptance verdict`, holding only
  the allowlisted files;
- push the branch;
- open **one Draft PR** to `main`. Its body gives the verdict, the matrix summary and the gates, and
  notes that it also carries the owner's `3819ef1` and the Planner's `TASK.md` commit.

**Git and PR: not authorized.** Ready, reviewer requests, approve, merge, update-branch, rebase,
force-push, or any push to `main`.

**Tracker, if ACCEPTED:**
- comment on #265 with the verdict and a link to the PR;
- set #265's Project Status to Done and close it as completed;
- comment on #229 that Slice 11 is accepted. #229 stays open and its Status and Capability State do
  not change.

**Tracker, if REJECTED:**
- create one Bug issue per defect, titled `[MESP-<next free key>] …` (the next free key is the
  highest in use + 1, verified live);
- add each Bug to Project #1 with `Jira Key` set to `MESP-<n>`, `Parent / Epic` set to
  `[MESP-15] #104`, Status Todo, and a `type:bug` label if one exists;
- comment on #265 and #229 with the verdict and the Bug links;
- leave #265 **open**. Change no Status or Capability State.

**Tracker, either verdict.** No other tracker write. Jira is read-only.

## 10. Stop conditions (stop, write a `STOPPED` entry, do not improvise)
- The starting state does not match §3.
- A gate fails for any reason other than a proven transient environment fault. You may re-run once,
  sequentially. Classify every failure (`MODEL_ROUTING.md` §10).
- A finding needs an unresolved business decision (M40-DEC-*) or touches credentials, production
  infrastructure, or a destructive migration. Record it in MESP-23 (#112) as an open question; do
  not decide it.
- Evidence shows a Tenant-isolation or accounting-integrity breach. Stop with `REJECTED`, file the
  Bug, and do not attempt a fix.
- After the Draft PR and the tracker writes: **STOP.** Make no further mutation. A later CI result or
  bot comment does not reopen the session.

## 11. Hand-back
- Add one `RESULT.md` entry at the top, using the §7 template in `MODEL_ROUTING.md`. It contains:
  - the Status (`ACCEPTED` or `REJECTED`);
  - the starting-state output;
  - the full matrix table: the R01–R20 rows, the M40-DEC-006 case and A1–A8, each with test,
    `file:line` and verdict;
  - the gates with output and wall time;
  - the reused evidence, named;
  - the tracker writes, with URLs;
  - any deviations, and any failures with their classification.
- In **Exact next action**:
  - give a one-line recommendation on whether Slice 11 needs a Sol 6 review under critical point 2
    (new data-integrity logic merged without one). This is a recommendation only;
  - say that the next task is the Sol 6 cleanup review.
- Update `docs/ROADMAP.md`:
  - the MESP-141 row: Slice 11 accepted, or rejected with the Bug keys;
  - the queue: remove MESP-150 or mark it done, and add any Bugs.
- Update `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the Sol summary untouched.
- Commit and deliver per §9. Then stop.

**Opus will check:**
- every matrix row cites exact asserting lines;
- no row was passed on indirect evidence;
- the gate output is pasted;
- the diff touches only the three allowlisted files;
- the tracker writes match §9 exactly;
- nothing is mutated after the stop.
```

## Next-task summaries (Planner, 2026-09-25)

### 1. MESP-150 (#265): acceptance review of MESP-141 Slice 11

The prompt is above.

### 2. Independent critical review of the cleanup on `main`

| | |
|---|---|
| Model / effort | **Sol 6 / high**, fresh session. This is Sol critical point 1: governance and architecture on `main`. |
| Work items | MESP-149 (#264), epic MESP-145 (#263) |
| Range | `pre-cleanup-20260925..main`. The tag is on `5ae718a`. |
| Scope | Read-only review of the following. <br>1. The D-18 behavior change. <br>2. The R2–R5 ratchets and their allowlists. <br>3. The doc moves: nothing lost, and no dangling references outside `docs/history/` and `docs/audit/`. <br>4. The five core docs against the code (the code is the fact). <br>5. `AGENTS.md` executor rules against the archived policy: no authority was widened. <br>6. Tracker reconciliation against `docs/audit/tracker-reconciliation.md`. |
| Out of scope | Mutations of any kind. Sol reports findings; Opus 5.5 decides. |
| Acceptance | A `RESULT.md` entry with findings ranked by severity, each with file:line evidence. Opus reviews it next. |
