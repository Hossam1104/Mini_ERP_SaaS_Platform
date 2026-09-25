# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **OPEN** (written by the Planner, Claude Opus 5.5, on 2026-09-25 after the owner sent `p`).

Routing: **Sol 6, effort high.** Open a **new session**.
- This is Sol critical point 1 (`MODEL_ROUTING.md` §2): governance and architecture changes merged into `main`.
- Sol is an independent, advisory reviewer. Opus 5.5 decides on every finding.

```markdown
# MESP-149 (#264) — Independent critical review of the cleanup merged to main (`pre-cleanup-20260925..f833927`)
Model: Sol 6 — Effort: high — Fresh session

## 1. Role and authority
- You are the **independent reviewer** of the MESP-149 (#264) cleanup (epic MESP-145 (#263)). You
  are advisory. You report findings; Opus 5.5 decides what happens to each one.
- `AGENTS.md` binds you, §1 especially. Authorization is positive. Every action this prompt does not
  list is forbidden.
- **The review is read-only.** Change no product code, test, migration, script, CI or governance doc,
  even to fix a finding you are sure of.
- Branch: `docs/mesp-149-sol-cleanup-review`.

## 2. Read order
1. `AGENTS.md`, then this prompt, then the two newest `RESULT.md` entries:
   - the MESP-149 cleanup entry (gates and slice table);
   - the MESP-150 (#265) Slice 11 verdict. Read it for context only; Slice 11 is out of your scope.
2. `docs/audit/cleanup-plan.md`, `docs/audit/drift-report.md` (§1 baseline, the D-rows, §5 owner
   decisions), `docs/audit/architecture-enforcement.md`, `docs/audit/tracker-reconciliation.md`,
   `docs/audit/executor-rules-proposal.md`.
3. `docs/DECISIONS.md` §1–§2 (Q1–Q10, Q-A–Q-L, the 2026-09-25 rows).
4. The range itself, one commit at a time:
   - List it with `git log --oneline pre-cleanup-20260925..f833927`.
   - The tag is on `5ae718a`. There are 7 commits plus the merge `f833927`, touching 113 files.
   - Use `git show --stat <sha>` first, then targeted `git show <sha> -- <path>`.
- **Serena:** call `initial_instructions` once. Use `find_symbol` on `ModuleBoundaryTests` (the
  ratchet members after line 518) and on the three coordinators touched by `3cacf79`. Don't read
  whole files.
- **Context7:** not needed.
- **Ponytail:** full. It never trims the findings table or the RESULT.md entry.
- Use `git grep` or the Grep tool, never recursive `grep -r`. If a plugin is missing, say so in one
  line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status -sb`: the tree is clean and you are on `main`.
- HEAD descends from `0e8ec29` (the MESP-150 verdict), and `git diff --name-only 0e8ec29 HEAD` lists
  only `TASK.md`.
- `origin/main` is either `f833927` or a later owner merge of PR #277. Both are fine. Record which.
- `git rev-parse pre-cleanup-20260925^{commit}` starts with `5ae718a`.
  `git merge-base --is-ancestor f833927 HEAD` succeeds.
- Any mismatch: stop (§10).

## 4. Rules to hold the cleanup to
- **No authority was widened.** The Phase 2 operating model (Q1–Q10, Q-A–Q-L) must not grant an AI
  executor any right that the archived policy withheld. This covers:
  - STOP boundaries;
  - Ready and merge;
  - tracker lifecycle writes;
  - Ponytail;
  - bot reviews.

  The one exception is a change an owner decision explicitly records.
- **Allowlists only shrink.** A ratchet that pins a baseline must pin exactly today's sites. A ratchet
  must not bless a site that was added without owner approval.
- **Code is the fact.** A core doc that states something the code does not do is a finding.
- **The archives are verbatim.** Every moved file must be byte-identical to its tag version.
  `frontend/assets` is owner-managed.
- **Live Git and the tracker outrank Markdown.**

## 5. Scope (read-only) and file allowlist
Review these six areas and record every finding:

1. **D-18, `3cacf79`.** AP, AR and cash-bank reconciliation reads now propagate
   `OperationCanceledException`. Other faults still yield `partial`.
   - Is the change correct and complete?
   - Is the regression test adequate? It covers AR only.
   - Was the "structured log" from the drift-report plan dropped? If so, is that a gap, given that
     Q-L approved propagation only?
   - Is the decision to leave `MigrationGlOpeningExecutionCoordinator` unchanged sound (the
     `DECISIONS.md` 2026-09-25 row)?
2. **The R2–R5 ratchets, `0d5fa4d`** (`ModuleBoundaryTests`, and `ARCHITECTURE.md` near line 131).
   - Does each assert exact equality with its baseline, so that removing a site forces the list to
     shrink and adding a site fails?
   - Do the R2 edge list (25 edges) and the R4 site list match the code exactly?
   - R4 pins 4 unscoped sites, 3 of which Slice 11 added without owner approval
     (`architecture-enforcement.md:87`). Assess whether freezing them as the baseline launders that
     widening. Recommend how to record it; Opus and the owner decide.
3. **The doc moves, `0928f93`.**
   - Each file in `docs/history/`, `docs/requirements/` and `docs/assets/` must be byte-identical to
     its tag path. Compare with `git diff pre-cleanup-20260925 f833927 -M --stat` and blob hashes.
   - No live file may reference a removed or moved path, except inside `docs/history/` and
     `docs/audit/`.
   - The D-19 test path update in `SafetyCatalogueValidationTests` must be correct.
4. **The five core docs, `2635d55`, checked against the code:** `docs/PROJECT.md`,
   `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/DECISIONS.md` and `docs/MODEL_ROUTING.md`.
   Spot-check at least these claims:
   - the layer and module rules;
   - the `AGENTS.md` §4 gate baselines;
   - the three CI job names in `.github/workflows/ci.yml`;
   - that the eight ADRs are embedded verbatim, compared with the tag copies.
5. **`AGENTS.md` and `MODEL_ROUTING.md` against the archived rules:**
   `docs/history/AGENTS_to_2026-09-25.md`, `AI_EXECUTION_POLICY_to_2026-09-25.md` and
   `CLAUDE_to_2026-09-25.md`.
   - List every rule that was dropped, weakened or widened.
   - For each one, say whether an owner decision covers it.
6. **The tracker reconciliation** against `docs/audit/tracker-reconciliation.md` §5.
   - Use read-only `gh issue list`, `gh issue view` and `gh project item-list 1 --owner Hossam1104`.
   - Live counts differ by the MESP-150 writes (Bugs #272–#276, MESP-156..160). Account for them;
     don't flag them.
   - Also check the asset restore `54cb8bd`: `frontend/assets/Saudi_Riyal.svg` and `wafra-logo.jpeg`
     must match their last owner version byte for byte.

**Files you may change:** `RESULT.md` (one new top entry) and `TASK.md` (Status → CONSUMED). No other
file.

## 6. Out of scope
- Fixing anything, in any file.
- Any tracker write: no issue, comment, label, field or state change. Jira in any form.
- The Slice 11 code (PR #262, `ac0309a`) and the MESP-150 verdict. Both predate or follow the range.
  Slice 11's own Sol review is a separate, later task.
- Commits after `f833927` (`3819ef1`, `5f7df6b`, `0e8ec29` and the Planner's `TASK.md` commit). The
  only exception: note it if they contradict a cleanup doc.
- Deciding any owner question, or deciding M40-DEC-*. Record a genuine new business question as a
  finding for Opus; do not write to MESP-23 (#112).

## 7. Acceptance (what Opus will check)
- Every finding has these fields:
  - an ID `SOL-CL-nn`;
  - a severity: **Critical** (authority widened, Tenant isolation, accounting integrity, data loss),
    **High**, **Medium**, **Low** or **Info**;
  - the area (1–6);
  - `file:line` or `sha:path` evidence;
  - the observed versus expected behavior;
  - a recommendation.
- Rank the findings most severe first.
- Every one of the six areas has a conclusion, even if that conclusion is "no finding", together with
  the commands that support it.
- Report an overall advisory recommendation: **no blocking findings**, or **blocking findings**
  (name them).
- Record facts only from live Git and the tracker. Never trust Markdown for them.

## 8. Gates (paste the output and the wall time)
1. `git diff --check`: it must be clean.
2. `git diff --name-only main...HEAD`: it lists only `RESULT.md` and `TASK.md`.
- Reused evidence, not re-run:
  - the backend suite, 1551/1551, from the MESP-150 entry. That run was on the `0e8ec29` tree, and
    no code has changed since.
  - the frontend, npm audit and EF checks from the MESP-149 entry.
- You may run `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` read-only if a finding needs it.
  Never run gates concurrently.

## 9. Git / PR delivery (positive authority, exactly this)
- Create `docs/mesp-149-sol-cleanup-review` from local `main` HEAD. Keep the unpushed local commits;
  never drop, rebase or amend them.
- Make ONE commit, `docs(review): MESP-149 (#264) Sol 6 cleanup review findings`, containing only
  `RESULT.md` and `TASK.md`.
- Push the branch. Open ONE **Draft** PR to `main`. Its body gives the overall recommendation, a
  findings summary by severity, and the gates.
- **NOT authorized:** Ready, reviewers, approval, merge, update-branch, rebase, force-push, push to
  `main`, and any tracker write.

## 10. Stop conditions (stop, write a `STOPPED` entry, do not improvise)
- The starting state does not match.
- You would need to change a file outside the allowlist.
- You find a **Critical** issue: an active Tenant-isolation, accounting or data-loss defect, or an
  authority widening that executors are relying on now. Record it, deliver per §9, and stop. Do not
  go on to the remaining areas.
- A command would mutate the tracker or a remote other than your branch push.
- After the Draft PR: STOP. No further mutation.

## 11. Hand-back
- Add one `RESULT.md` entry at the top, using the `MODEL_ROUTING.md` §7 template, with Status
  `DONE` or `STOPPED`. It contains:
  - the starting-state output;
  - the per-area conclusions with their commands;
  - the ranked findings table;
  - the overall recommendation;
  - the gates;
  - any deviations.
- **Exact next action:** "Opus 5.5 reviews the Sol cleanup findings." Add a one-line recommendation
  on which findings need a Bug or a docs task.
- In `TASK.md`, set this prompt's Status to **CONSUMED**. Leave the next-task summaries untouched.
- Commit and deliver per §9. Then stop.
```

## Next-task summaries (Planner, 2026-09-25)

### 1. MESP-150 (#265): acceptance review of MESP-141 Slice 11: done, REJECTED

Recorded in `RESULT.md`, Draft PR #277. Bugs MESP-156..160 (#272–#276). #265 stays open until the
Bugs are fixed and Slice 11 is re-reviewed.

### 2. Independent critical review of the cleanup on `main`

The prompt is above.

### 3. Slice 11 evidence Bugs MESP-156..160 (#272–#276)

| | |
|---|---|
| Model / effort | **Luna 6 / xhigh**, fresh session. These are new test oracles on money, stock, concurrency and Tenant-lifecycle paths, so this is not a Sonnet contained fix. |
| Work items | MESP-156..160 (#272–#276), under MESP-15 (#104); capability MESP-141 (#229) |
| Scope | Test-only. Strengthen `MigrationReconciliationSqlServerSafetyTests` to cover these oracles: <br>- R04/R05: exact equality with the persisted mapping. <br>- R07: reconcile the run and assert that each subsidiary matches GL with no double count. <br>- R12: `IsCurrent == false`, a changed fingerprint, and the prior approval no longer counted. <br>- R18: read back the Tenant lifecycle before and after readiness. <br>- R20: separate service instances. <br>- A5: version conflicts rejected and nothing persisted. <br>If a strengthened test **fails**, that is a product defect: stop and file a Bug. Never adjust the product in the same prompt. |
| Out of scope | Product code, migrations, allowlists, and deciding M40-DEC-*. |
| Acceptance | Every new assertion is exact. The full backend suite passes on disposable LocalDB with 0 skipped. Then Opus re-reviews Slice 11 under MESP-150 (#265). |
