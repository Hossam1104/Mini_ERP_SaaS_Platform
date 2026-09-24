# Drift Report — plan vs. code

- **Date:** 2026-09-25 (supersedes the 2026-09-24 handover audit; its findings are carried forward below with an updated status).
- **Auditor:** Claude Opus 5.5, Planner / Architect / Acceptance Authority (owner decision Q2, §5). Phase 1 is **audit only**. No code or governance file changed. Writes went only to `docs/audit/`.
- **Audited:** local `main` @ `90f9c26`. This is the cleanup baseline `ac0309a4b6b363b906f49af6c1b6b6ce71288ce7` plus two local, unpushed commits: the `.serena` default-key expansion and `docs/handover` + `docs/audit`. Step Zero is recorded in `branch-consolidation.md`.
- **Tooling (Phase 0):**
  - **Serena:** works, with the C# language server active.
  - **Context7:** available, not needed.
  - **Ponytail FULL:** active.
  - **`gh`:** authenticated as `Hossam1104`.
  - **Tracker:** GitHub Issues + Project #1, reachable.
  - **Atlassian MCP:** reachable. Jira is read-only provenance and was not touched.

## Severity scale

| Severity | Meaning |
|---|---|
| **Critical** | A live defect in Tenant isolation, authentication/authorization, accounting/stock integrity, or data safety, **or** a red gate. Stop the line. |
| **High** | A governing document or process fact would plausibly make a fresh session act wrongly, or an owner-level conflict blocks the operating model. |
| **Medium** | A rule that code weakened or never enforced, or a structural defect that erodes the process but does not mislead on its own. |
| **Low** | A cosmetic or wording drift, known accepted debt, or an unenforced convention that currently holds. |

**Nothing is Critical.** Every gate is green (§1). Tenant scoping holds at every allowlisted raw-SQL site. No module's Infrastructure touches another module's DbContext.

## 1. Gates run by me on `main` @ `90f9c26` (2026-09-25)

| Gate | Command (repo root) | Result | Wall time |
|---|---|---|---|
| Backend Release build | `pwsh scripts/Test-MiniErpBackend.ps1 -NoBuild:$false` (build step) | `Build succeeded. 0 Warning(s) 0 Error(s)` | 2 m 34 s |
| Backend full suite, including disposable LocalDB SQL safety | same script (test step), target `MiniErpFoundation_20260925000520_c903b621` | `Failed: 0, Passed: 1546, Skipped: 0, Total: 1546` | 9 m 37 s (script total **12 m 34 s**) |
| Angular unit | `cd frontend; npm test -- --watch=false --no-progress` | 45 files, **316/316** | 165 s |
| Angular production build (also the type-check) | `npm run build` | success; known **budget warning** 514.26 kB, 14.27 kB over 500 kB | 78 s |
| Playwright Chromium (mocked fixtures) | `npm run test:e2e -- --project=chromium` | **51 passed** | 156 s |
| npm audit, production | `npm audit --omit=dev --audit-level=high` | 4 moderate, 0 high/critical → pass | 35 s |
| npm audit, full | `npm audit --audit-level=high` | 7 moderate, 0 high/critical → pass | 12 s |

The runner's post-run check reported *"MESP_SQLSERVER_CONNECTION_STRING (runtime): unchanged. MESP data is intact."*

Wall times are higher than on 2026-09-24 (6 m 23 s / 73 s) because the backend and frontend jobs ran **concurrently** on one machine.

**Not run:** the EF `has-pending-model-changes` check on the 8 contexts. The last recorded evidence is `.ai/CURRENT_STATE.md:30`. It runs in Phase 2 after the refactor slices.

**Lint and type-check:**
- **Backend.** There is no separate linter. `TreatWarningsAsErrors` + `EnableNETAnalyzers` + `AnalysisLevel=latest` in `backend/Directory.Build.props` act as the lint gate.
- **Frontend.** There is no lint or type-check script (`frontend/package.json`). `ng build` does the type-checking. Prettier is installed, but no gate runs it.

**Hosted CI:** 3 required checks, read from GitHub rather than run here. They were green on `ac0309a`'s PR head (#262).

## 2. Drift table

The **Status** column shows where each 2026-09-24 finding stands now.

| ID | Area | Plan says | Code / fact (file:line) | Severity | Recommendation | Status |
|---|---|---|---|---|---|---|
| D-01 | Current state | `.ai/CURRENT_STATE.md:13-21` (CURRENT AUTHORITY): Slice 11 "**PR #262 OPEN / DRAFT / UNMERGED / NOT READY**", awaiting Sol acceptance | PR #262 was **merged** during Step Zero at `ac0309a` (2026-09-24T20:58:57Z), as the consolidation contract directed. `docs/handover/*` still says "no Slice 11 implementation committed". | High | Rewrite the authority block for the post-cleanup state (Phase 2 docs slice). Slice 11 is **merged but not accepted**; see D-15. | open |
| D-02 | Governance pointer | `CLAUDE.md:36-37`, `AGENTS.md:40,55`: "active capability is MESP-141 Slice 1 only … Draft PR #242" | Slices 1–11 are merged. | High | Replace with pointer-only text. Decided in Q8. | decided |
| D-03 | Roles / routing | `AGENTS.md:72-77` (Sol planner/acceptance, Terra active), `.ai/AI_EXECUTION_POLICY.md:11,38,59,69,73` (Sol acceptance), `HANDOVER_NOTES.md:169-183` | The owner adopted the new model (Q1, Q2). | High | Replace with `docs/MODEL_ROUTING.md` and amend the policy's Sol references to Opus 5.5. | decided |
| D-04 | CI/CD | The cleanup contract's project profile says "CI/CD: already decided NO" | `.github/workflows/ci.yml` runs 3 required checks. Ruleset `22905800` is active. | High → resolved | **Owner decided Q3: keep CI.** The contract profile line is superseded for this repo. | decided |
| D-05 | State file structure | `.ai/CURRENT_STATE.md:3-8`: a single CURRENT AUTHORITY block | 4 `CURRENT AUTHORITY` headings, at L13, L136, L177 and L234 | Medium | Rename 3 to HISTORICAL RECORD (Q8). | decided |
| D-06 | Handoff files | The operating model: `TASK.md` holds the single current prompt, plus `RESULT.md` and a routing file | `TASK.md` is a 3,369-line log. `RESULT.md` does not exist. | Medium | Archive verbatim and restart it (Q4). The routing file goes to `docs/MODEL_ROUTING.md` per the cleanup contract; see Q-D. | decided |
| D-07 | Raw-SQL boundary | `docs/handover/ARCHITECTURE.md:249`: raw SQL only at privileged boundaries | Allowlist grew 1→4 (`backend/tests/MiniErp.ArchitectureTests/ModuleBoundaryTests.cs:346-355`). The 3 new sites are Tenant-predicated `UPDLOCK` row locks in `Migration/MigrationPersistence.cs:538` and `MigrationReconciliationPersistence.cs:296,301`. | Medium | Record a "Tenant-predicated lock-only raw SQL" category, then enforce R4 (§e of the cleanup plan). | open (Q-E) |
| D-08 | Module coupling | `ARCHITECTURE.md:219`: cross-module use goes through explicit contracts | 25 App cross-module import edges, 3 of them bidirectional: Finance↔Sales, Inventory↔Sales, MasterData↔BusinessParties (see the 2026-09-24 audit for file:line) | Medium | Add ratchet R2 and freeze. Do not refactor the pairs now. | open (Q-E) |
| D-09 | Complexity watch | `HANDOVER_NOTES.md:252`: watch `MigrationExecutionService` | `MigrationReconciliationService.cs` 828 lines. Also `Identity/IdentityAuthorizationService.cs` **2,489** lines, `MasterData/MasterDataImportProcessors.cs` 1,786, `MiniErp.Api/Program.cs` 1,361, `frontend/src/app/core/i18n/language.service.ts` 1,924 | Low | Add to the watch list in `ARCHITECTURE.md`. Size alone does not justify a split without tests pinning behavior. | open |
| D-10 | InternalsVisibleTo | `docs/handover/DECISIONS.md:37`: "IVT only for ArchitectureTests" | `MiniErp.Contracts/Properties/AssemblyInfo.cs:3` also grants IVT to `MiniErp.App` | Low | Amend the wording when merging it into `docs/DECISIONS.md`. | open |
| D-11 | Hosted CI exclusion | `ARCHITECTURE.md:392`: the hosted run excludes the LocalDB subset | The exclusion is a name-suffix filter `FullyQualifiedName!~SqlServerSafetyTests` (`.github/workflows/ci.yml:86`). Nothing enforces the suffix. | Low | Add rule R5. | open (Q-E) |
| D-12 | Obsolete script | Jira is read-only | `scripts/Update-MESP131-Docs.ps1` writes stale Jira-era state. It is **untracked**, hidden by `.git/info/exclude:7`. | Low | Delete the local file and the exclude line (Q9). No commit is involved. | decided |
| D-13 | Frontend budget / audit | Known 514.26 kB and 4/7 moderate advisories (`ROADMAP.md:208-210`) | Reproduced exactly (§1) | Low | Accepted debt. No change. | accepted |
| D-14 | Tracker syntax | The cleanup contract: Jira keys / "Azure `#<ID>`" | The tracker is GitHub Issues with `MESP-<n>` keys | Low | Use `MESP-<n> (#<issue>)` (Q6). | decided |
| **D-15** | Acceptance | Q2: "Opus 5.5 does the acceptance review of PR #262" | #262 (Slice 11: reconciliation, approval, Ready-for-Handover; 21 provider tests; migration `20260924135807_Mesp141Slice11Reconciliation`) entered `main` through Step Zero consolidation, **without an acceptance review**. The gates are green, and Slice 11 fails closed without a policy (`MigrationReconciliationService.cs:164,224`). | High | Treat Slice 11 as *merged, pending acceptance*. The acceptance review runs **after the cleanup** as a normal task (recommended), not inside it. See Q-B. | new |
| **D-16** | Push path | The cleanup contract: all work goes direct on `main`, pushed **once**, with no PRs after Step Zero | Ruleset `22905800` (active on `main`) requires a pull request and strict required checks. It blocks non-fast-forward and deletion. The bypass is admin role 5 in **`pull_request` mode only** (`current_user_can_bypass: pull_requests_only`). A direct `git push origin main` **will be rejected**. | High | Owner choice, and the executor will not bypass the ruleset. **Option 1 (recommended):** at the end of Phase 2, push the local `main` commits to one short-lived PR branch, then merge it as a **merge commit**; the admin bypass covers the review requirement and CI runs. **Option 2:** the owner temporarily edits the ruleset to allow a direct push. See Q-A. | new |
| **D-17** | Tracker state | `.ai/AI_EXECUTION_POLICY.md:85-98`: GitHub Project `Status` / `Capability State` record lifecycle | **Epics are stale.** 9 epics (MESP-3, -4, -5, -6, -7, -9, -10, -11, -12, -13; issues #92–#102) have **all children Done** but are still In Progress or Todo, with no closure recorded. MESP-15 (#104) is **Todo/Backlog** while its child MESP-141 (#229) is Active. #238–#240 have **no `[MESP-n]` key, Jira Key, or parent** (they violate `AGENTS.md:46`). The roadmap phases (Golden E2E, functional baseline, UI modernization) and this cleanup **have no tracker items**. | Medium | See `tracker-reconciliation.md`. Applying it needs positive authority (Q-F). | new |
| **D-18** | Error handling | `ARCHITECTURE.md:105`: material mismatch must be a visible failure, and `Outcome Unknown` must block | The reconciliation read paths swallow every exception with `catch { }`: `Migration/MigrationApOpeningExecutionCoordinator.cs:246`, `MigrationArOpeningExecutionCoordinator.cs:247`, `MigrationCashBankOpeningExecutionCoordinator.cs:157`, `MigrationGlOpeningExecutionCoordinator.cs:120`. A DB fault or a **cancellation** (`OperationCanceledException`) becomes a normal `"partial"` / `*_not_reconciled` row, with no log entry. The result still fails closed (it never reports `reconciled`), so this is not an integrity defect. It does hide infrastructure failures and ignores cancellation. | Medium | Planned fix: `catch (Exception ex) when (ex is not OperationCanceledException)` plus a structured log, and a characterization test that a cancellation propagates. Behavior for real errors is unchanged: still `partial`. | new |
| **D-19** | Doc-path coupling | The target docs layout moves or merges docs | `backend/tests/MiniErp.ArchitectureTests/SafetyCatalogueValidationTests.cs:76` reads `docs/96_Foundation_Release1_Safety_Validation.md` and checks its 5-column numbered table. `MigrationFoundationTests.cs:1083` finds the repo root through `AGENTS.md`. | Medium | Keep the catalogue content verbatim at a new path, `docs/history/96_Foundation_Release1_Safety_Validation.md`, and update the one path in the test in the same commit. The test must stay green. Keep `AGENTS.md` at the root. | new |
| **D-20** | Governance duplication | `CLAUDE.md:1` begins `@AGENTS.md` | `CLAUDE.md` is 505 lines. It re-states the AGENTS content plus 14 historical overlays. `docs/Decisions.md` has 9 headings titled `Current …`, all historical (e.g. L12, L74, L95). `docs/staticts.md` has 12 `Current …` snapshot headings (e.g. L5, L122, L150). | Medium | Reduce `CLAUDE.md` to `@AGENTS.md` and move the history to `docs/history/`. Take the ADR index into `docs/DECISIONS.md`. See Q-G for `staticts.md`. | new |
| **D-21** | Dead or stub docs | `docs/README.md:82-89` lists them as domain references | `docs/01_Business_Vision.md` and `02`–`10` are **5–7 line stubs** (a title and one line). `wireframes/Wireframes - Layout Alternatives.dc.html` references `05_Layout_Alternatives.md`, which does not exist. | Low | Fold the one-line content into `PROJECT.md` and delete the stubs. Move the wireframes to `docs/assets/wireframes/` (Q-H). | new |
| **D-22** | Generated clutter | `.gitignore` covers build output | `backend/.s10-validation/` is an **untracked, recursively nested .NET output tree** (paths exceed `MAX_PATH`; `git status --ignored` warns *Filename too long*). `.artifacts/mesp131-p1-test/` is stale build output. Neither is tracked; `**/bin/` and `**/obj/` hide both from status. | Low | Delete both (generated, verified). Add `/.artifacts/` and `/backend/.s10-validation/` to `.gitignore`. | new |
| **D-23** | Exception pattern | — | 227 bare `catch` blocks in `backend/src`. Nearly all are the fail-closed `catch { return await FailedAsync(…) }` audit pattern (e.g. `BusinessParties/SupplierService.cs:58`). These also convert a cancellation into an audited failure. | Low | Leave as is: behavior is consistent and fail-closed. Note it in `ARCHITECTURE.md` as a known pattern. Do not mass-refactor during cleanup. | new |

## 3. `[UNVERIFIED]` handover items

The verdicts from 2026-09-24 stand.

| Location | Claim | Verdict |
|---|---|---|
| `DECISIONS.md:13,15` | No ADR-003/005 files | Confirms. ADR files exist only for 002, 004, 006–009, 018 and 019. |
| `DECISIONS.md:20` | ADR-010 exporter not selected | Confirms (`docs/01_Technology_Architecture_Baseline.md:929`). |
| `DECISIONS.md:21` | Localized UI exists; ADR-011 is not closed | Confirms (`frontend/src/app/core/i18n/language.service.ts`). |
| `DECISIONS.md:22-25` | ADR-012..015 alternatives not selected | Cannot resolve. No files contradict the claim. |
| `ARCHITECTURE.md:398` | No persistent QA environment | Confirms, repo-scoped: no deploy job, only local launchers. |
| `ROADMAP.md:9` | No Azure DevOps IDs | Confirms. |
| `ROADMAP.md:218` | No additional P0/P1 defect | Confirms. 44 open issues, none labelled bug. All gates green (§1). D-18 is Medium, not P0/P1. |

## 4. `[CONFIRMED]` claims spot-checked

All of these still hold:
- the 4-project graph (`ModuleBoundaryTests.cs:24-85`);
- no EF Core in App or Api;
- the SDK and package versions (`backend/global.json`, `Directory.Packages.props`);
- 8 contexts;
- zero cross-module DbContext references in module Infrastructure;
- the `__Host-MiniErp.Auth` cookie;
- `/api/v1`;
- Slice 11 fails closed without a policy;
- the CI shape;
- Issue #229 active, #230 not-activated;
- `frontend/assets` untouched. `git status` is clean. The two Owner assets on `archive/fix/MESP-123-angular-branding` were deliberately **not** restored (Q-C).

## 4a. Planner-introduced defects

- `AUTOMATION_DEFECT (Planner-introduced, 2026-09-24)`: a stray `C:\Program Files\Git\fe-test.log` (806 bytes). It is **still present** (verified 2026-09-25). It is outside the repo root, so the contract forbids me from touching it. **The owner needs to delete it.**
- `AUTOMATION_DEFECT (Planner-introduced, 2026-09-25)`: during Phase 1, one `git status --ignored` + `du` command walked the recursive `backend/.s10-validation` tree and timed out. I stopped it. Nothing changed.

## 5. Owner decisions

### 5.1 Answered 2026-09-24 (Hossam)

| # | Question | Decision | Consequence |
|---|---|---|---|
| 1 | Routing authority | **Adopt the new model:** Opus 5.5 Planner, Luna 6 xhigh default executor, Sol 6 critical-point reviewer, Sonnet 5 bug fixer. **Terra retired.** | Replace the routing table at `AGENTS.md:72-77` with a pointer to the routing file. `HANDOVER_NOTES.md` routing becomes history. |
| 2 | Acceptance authority | **Opus 5.5 replaces Sol.** | Change every Sol-as-acceptance reference in `.ai/AI_EXECUTION_POLICY.md`. |
| 3 | CI | **Keep the CI pipeline and ruleset** until the app is published to a server. CD waits. | No change to `ci.yml` or ruleset `22905800`. |
| 4 | Handoff files | **Yes.** | Archive `TASK.md` verbatim. Single-prompt `TASK.md`. Add `RESULT.md` and a routing file. |
| 5 | Enforcement | **Open.** Clarification requested. | Re-asked plainly as Q-E. |
| 6 | Reference syntax | **`MESP-<n> (#<issue>)`.** | Record in `AGENTS.md` and the routing file. |
| 7 | Prompt cap | **None.** | Do not carry it into governance. |
| 8 | Stale pointers | **Fix.** | `AGENTS.md`, `CLAUDE.md`, and the `CURRENT_STATE.md` headings. |
| 9 | Obsolete script | **Retire.** | Delete the local untracked file and the exclude line. |
| 10 | Where to commit | ~~PR to `main`~~ → superseded by the 2026-09-24 cleanup contract (direct on `main`, one push). That push is blocked by the ruleset; see D-16 / Q-A. | — |

### 5.2 Pending (Phase 1 questions, 2026-09-25)

Q-A to Q-L are in the chat reply and `cleanup-plan.md` §g. Answers are recorded here before Phase 2 starts.
