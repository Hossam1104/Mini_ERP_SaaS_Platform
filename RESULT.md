# Results

The shared results log, newest entry first. Every model adds exactly one entry per session, using the
template in [`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §7. Older logs are archived verbatim in
[`docs/history/`](docs/history/).

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
