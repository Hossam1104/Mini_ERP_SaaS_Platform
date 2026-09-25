# Results

The shared results log, newest entry first. Every model adds exactly one entry per session, using the
template in [`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §7. Older logs are archived verbatim in
[`docs/history/`](docs/history/).

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
