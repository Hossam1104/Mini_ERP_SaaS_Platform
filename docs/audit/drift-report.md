# Drift Report — Sol handover vs. code

- **Date:** 2026-09-24
- **Auditor:** Claude Opus 5.5, as incoming Planner / Architect / Acceptance Authority. Phase 1 was **read-only**: no code or governance file was changed.
- **Branch / HEAD audited:** `feat/mesp-141-reconciliation-handover-evidence` @ `d94cc3c4939e4b10b272d3819ca2c7d29bb0ce78`. `origin/main` = `13ede0af8234c3dc1d58272532f27258f1c87ed6`.
- **Handover audited:** `docs/handover/{ARCHITECTURE,DECISIONS,ROADMAP,HANDOVER_NOTES}.md`. All four are present and **untracked**.
- **Tooling:** Serena MCP **failed to connect** this session, so I used targeted grep and line-range reads instead. Context7 was available but not needed. Ponytail was active.

## Severity scale

| Severity | Meaning |
|---|---|
| **Critical** | A live defect in Tenant isolation, authentication/authorization, accounting/stock integrity, or data safety, **or** a gate that is red. Stop the line. |
| **High** | A governing document tells an executor something false about the current state or authority, so a fresh session would plausibly act wrongly. Or an owner-level conflict that blocks the operating model. |
| **Medium** | A rule that code weakened or never enforced, or a structural governance defect that erodes the process but does not mislead on its own. |
| **Low** | A cosmetic or wording drift, a known accepted debt, or unenforced convention that currently holds. |

**Nothing is Critical.** Every gate I ran is green (§1). Tenant scoping holds at every new raw-SQL site. No module Infrastructure touches another module's DbContext.

## 1. Gates I ran myself

| Gate | Command | Result | Wall time |
|---|---|---|---|
| Backend Release build | `scripts/Test-MiniErpBackend.ps1 -NoBuild:$false` (restore + build step) | `Build succeeded. 0 Warning(s) 0 Error(s)` | 1 m 36 s |
| Backend full suite, including disposable LocalDB SQL safety | same script (test step), target `MiniErpFoundation_20260924214305_1a79aed2` | `Failed: 0, Passed: 1546, Skipped: 0, Total: 1546` | 4 m 35 s (script total 6 m 23 s) |
| Angular unit tests | `npm test -- --watch=false --no-progress` | `Test Files 45 passed (45)`, `Tests 316 passed (316)` | 73 s |
| Angular production build | `npm run build` | success, with a **budget warning**: `Initial total 514.26 kB`, over the 500 kB budget by 14.27 kB | 31 s |
| Whitespace | `git diff --check 13ede0a HEAD` | clean | <1 s |
| npm audit (CI threshold is high) | `npm audit --audit-level=high` / `--omit=dev` | 7 moderate in total, 4 moderate in production, 0 high/critical, so it passes | ~5 s |
| Hosted CI (read, not run) | `gh pr view 262` | Repository Validation / Backend / Frontend = SUCCESS at `d94cc3c` | — |

**Not run by me:** Playwright Chromium, which is hosted, uses mocks, and is green on #262. I also did not run the EF pending-model check on the 8 contexts, so that claim is not re-verified. The numbers above match `.ai/CURRENT_STATE.md:30` exactly.

Linting and type-checking: the backend has no separate linter. `TreatWarningsAsErrors` + `EnableNETAnalyzers` + `AnalysisLevel=latest` (`backend/Directory.Build.props`) act as the lint gate. The frontend has no lint or type-check script (`frontend/package.json`). `ng build` does the type-checking. Prettier is installed but no gate calls it.

## 2. Drift table

| ID | Area | Plan says (doc + section) | Code does (file:line) | Severity | Recommendation |
|---|---|---|---|---|---|
| D-01 | Current state | Slice 11 "activated, but … no Slice 11 implementation was committed" and "No Slice 11 Draft PR existed" — `docs/handover/ARCHITECTURE.md:207`, `ROADMAP.md:73-75`, `HANDOVER_NOTES.md:270` | Commit `6029b9e` adds `MigrationReconciliationService` (`backend/src/MiniErp.App/Modules/Migration/MigrationReconciliationService.cs:16`), migration `20260924135807_Mesp141Slice11Reconciliation`, 21 provider tests. Draft PR **#262** is OPEN/DRAFT with 3/3 green checks at `d94cc3c`. `.ai/CURRENT_STATE.md:13-23` agrees with the code. | High | **Amend plan.** The handover is a pre-implementation snapshot. The real current step is *acceptance review of PR #262*. |
| D-02 | Governance pointer | "The active capability is MESP-141 Slice 1 only … Draft PR #242" — `CLAUDE.md:36-37`; `AGENTS.md:40,55` | Slices 1–10 are merged. Slice 11 is in Draft PR #262 (`.ai/CURRENT_STATE.md:13`). | High | **Amend plan.** These are the first two files every fresh session reads, and they name the wrong capability and PR. Replace the text with a pointer only. |
| D-03 | Roles / routing | `AGENTS.md:72-76`: Sol GPT-5.6 is planner and acceptance authority, Luna GPT-5.6 xHigh, Terra **active** at priority 4, Opus 5 a rare reviewer. `HANDOVER_NOTES.md:169-183`: GPT-6 Luna, Terra **retired**, Opus 5.5 a protected review lane, Sol-in-Codex emergency-only. The incoming operating model: **Opus 5.5 is planner and acceptance**, Luna 6 is default, Sol 6 reviews at critical points only. `.ai/AI_EXECUTION_POLICY.md:11,26-45` names Sol as the acceptance authority throughout. | n/a (governance) | High | **Needs owner decision** (Q1, Q2). These three governing sources conflict, and I have not picked one. |
| D-04 | CI/CD | The incoming model says: "On Mini_ERP_SaaS_Platform the owner chose *no CI/CD at all*". | `.github/workflows/ci.yml` runs 3 jobs on PRs and pushes to `main`. Ruleset `22905800 main-ci-quality-gates` (enforcement **active**) requires `Repository Validation`, `Backend`, `Frontend`, strict, plus no force-push or deletion. The handover (`ARCHITECTURE.md:386-442`) matches the code: CI yes, CD no. | High | **Needs owner decision** (Q3). Until then the reversible default is to keep the existing CI. |
| D-05 | State file structure | `.ai/CURRENT_STATE.md:3-8`: "The single block … titled CURRENT AUTHORITY is the only current-state authority" | Four `## CURRENT AUTHORITY` headings exist, at lines 13, 136, 177 and 234 (15–16 Sep). | Medium | **Amend plan.** Rename the older three to `HISTORICAL RECORD`. This is a text-only edit. |
| D-06 | Handoff files | The incoming model says `TASK.md` holds only the current prompt, `RESULT.md` holds the results log, and `.ai/MODEL_ROUTING.md` is the routing authority. | `TASK.md` is a 3,369-line lifecycle log with 137 `##` sections, newest first. `RESULT.md` and `.ai/MODEL_ROUTING.md` do not exist. | Medium | **Needs owner decision** (Q4). Archive the log verbatim to `docs/history/TASK_LOG_to_2026-09-24.md` and restart it. |
| D-07 | Raw-SQL boundary | "`IgnoreQueryFilters`, unbounded raw SQL … restricted to explicit privileged boundaries" — `docs/handover/ARCHITECTURE.md:249` | Slice 11 widened the unscoped-EF allowlist **from 1 to 4 call sites** (`backend/tests/MiniErp.ArchitectureTests/ModuleBoundaryTests.cs:346-355`). The new sites are `MigrationPersistence.cs:538`, `MigrationReconciliationPersistence.cs:296` and `:301`, all under `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/`. All three are parameterized `SELECT … WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {…}` row locks. They are Tenant-predicated and read nothing back, but they are **not** privileged boundaries, and no ADR records this new category. | Medium | **Amend plan** to record a "Tenant-predicated lock-only raw SQL" category, and **enforce** the Tenant predicate (see `architecture-enforcement.md` R4). Treat it as an explicit review item on PR #262, not as a code defect. |
| D-08 | Module coupling | Modules consume other modules "through explicit contracts" — `docs/handover/ARCHITECTURE.md:219`. The graph is "acyclic" (`:87`, project level only). | App modules import each other's namespaces along 25 edges, **with 3 bidirectional pairs**: Finance↔Sales (`MiniErp.App/Modules/Finance/FinanceCustomerReturnApplicationContracts.cs:6` / `Sales/SalesApplicationContracts.cs:10`), Inventory↔Sales (`Inventory/InventoryService.cs:6` / `Sales/SalesApplicationContracts.cs:11`), MasterData↔BusinessParties (`MasterData/MasterDataPriceListService.cs:4` / `BusinessParties/CustomerService.cs:4`). No test guards module-level direction. | Medium | **Fix code (tests only)**: add a ratchet (R2) and freeze today's edges. Do not refactor now. |
| D-09 | Complexity watch | `MigrationExecutionService` is "an architecture complexity watch item; new Migration concerns should not automatically be placed into that service" — `HANDOVER_NOTES.md:252` | That rule was followed, because Slice 11 went into a new collaborator. But `MigrationReconciliationService.cs` is 828 lines, next to `MigrationExecutionService.cs` at 819 lines (`:18`). | Low | **Amend plan.** Add it to the watch list. Line count alone is not a reason to refactor (`HANDOVER_NOTES.md:254`). |
| D-10 | InternalsVisibleTo | "leaving `InternalsVisibleTo` only for `MiniErp.ArchitectureTests`" — `docs/handover/DECISIONS.md:37` | That holds for App (`MiniErp.App/Properties/AssemblyInfo.cs:3`), but `MiniErp.Contracts/Properties/AssemblyInfo.cs:3` also grants IVT to `MiniErp.App`. | Low | **Amend plan** wording: the statement concerns App→Api only. |
| D-11 | Hosted CI exclusion | Hosted Backend "excludes the SQL Server LocalDB safety subset" — `ARCHITECTURE.md:392` | The exclusion is a **name-suffix filter** `FullyQualifiedName!~SqlServerSafetyTests` (`.github/workflows/ci.yml:86`). Files named `*IntegrationTests.cs` depend on class names that still end in `…SqlServerSafetyTests` (e.g. `MigrationInventoryOpeningSqlServerIntegrationTests.cs:33`). All 13 collection members conform today, but nothing enforces the convention. | Low | **Fix code (tests only)**: add rule R5. |
| D-12 | Obsolete script | Jira is read-only provenance — `HANDOVER_NOTES.md:17` | `scripts/Update-MESP131-Docs.ps1:284-286` upserts Jira-era MESP-131 blocks into `.ai/CURRENT_STATE.md`, `TASK.md` and `docs/staticts.md`. Running it now would write stale state. | Low | **Needs owner decision** (Q9): retire it. |
| D-13 | Frontend budget / audit | Known 514.26 kB warning. Audit shows 4 moderate production and 7 moderate overall — `ROADMAP.md:208-210` | Reproduced exactly (§1). | Low | No change. This is accepted, known debt. |
| D-14 | Tracker syntax | The incoming model says commits reference "Azure Repos `#<ID>`" | There is no Azure DevOps here: no `azure-pipelines.yml`. The tracker is GitHub Issues with the `MESP-###` key (`AGENTS.md:46-47`). | Low | **Amend** the incoming model for this repo: use `MESP-### (#<issue>)`. This is Q6. |

## 3. `[UNVERIFIED]` handover items

| Handover location | Claim | Verdict | Evidence |
|---|---|---|---|
| `DECISIONS.md:13` | ADR-003 has no dedicated ADR file listing all alternatives | **Confirms** | `docs/` holds ADR files only for 002, 004, 006–009, 018 and 019. |
| `DECISIONS.md:15` | ADR-005 has no dedicated full ADR | **Confirms** | Same listing. There is no ADR-005 file. |
| `DECISIONS.md:20` | ADR-010 rejected exporter products are not recorded | **Confirms** | `docs/01_Technology_Architecture_Baseline.md:929` lists ADR-010 as an open decision with an owner and no product. `:713` is provider-neutral only. |
| `DECISIONS.md:21` | Some localized UI is implemented, but that does not close ADR-011 | **Confirms** | `frontend/src/app/core/i18n/language.service.ts` exists (language/RTL service with spec). There is no ADR-011 file. |
| `DECISIONS.md:22-25` | Alternatives for ADR-012 to ADR-015 are not selected | **Cannot resolve** beyond consistency | No ADR-012..015 files exist, so nothing contradicts the claim. Absence of a file does not prove no discussion happened. |
| `ARCHITECTURE.md:398` | No persistent QA environment | **Confirms (repo-scoped)** | `ci.yml` has no deploy job. Only local launchers exist (`scripts/Start-MiniErpDevelopment.ps1`). I cannot prove there is none outside the repo. |
| `ROADMAP.md:9` | No Azure DevOps work-item IDs | **Confirms** | There is no `azure-pipelines.yml`. The labels are all GitHub/MESP (`gh label list`). |
| `ROADMAP.md:218` | No additional P0/P1 product defect | **Confirms** | 44 open issues, none labelled `bug`/`type:bug`. The only `priority:highest` is #112 (MESP-23, the governance register). All gates are green (§1). |

## 4. `[CONFIRMED]` claims I spot-checked, all of which hold

- Four-project graph and direction: the `ProjectReference` entries in `backend/src/*/*.csproj`. It is enforced by `ModuleBoundaryTests.cs:24-85,465`.
- App and Api have no EF Core, enforced at `ModuleBoundaryTests.cs:40`. EF packages are referenced only by Infrastructure, tests and the cutover tool.
- .NET SDK `10.0.400` (`backend/global.json`); `net10.0`, `LangVersion 14.0`, nullable, TWAE (`backend/Directory.Build.props`); EF `10.0.10`, OpenApi `10.0.10`, Microsoft.OpenApi `2.7.5`, Scalar `2.16.16`, xUnit `2.9.2`, Test SDK `17.12.0` (`backend/Directory.Packages.props:8-21`).
- 8 contexts: 7 module DbContexts plus `TenantPersistenceDbContext` (`backend/src/MiniErp.Infrastructure/Persistence/TenantPersistenceDbContext.cs:6`). The `Persistence/Migrations/Mesp141` folder exists.
- Module Infrastructure never references another module's DbContext or persistence namespace. I measured **0** such references. The only cross-context files are the composition-level `Persistence/DevelopmentSqlServerDatabaseMigrator.cs` and `Persistence/SqlServerDesignTimeDbContextFactories.cs`.
- Cookie `__Host-MiniErp.Auth` (`backend/src/MiniErp.App/Modules/Identity/FirstPartyCookieConfiguration.cs:24`). Routes live under `/api/v1` (`backend/src/MiniErp.Api/CategoryUomEndpoints.cs:21`).
- Slice 11 approval fails closed. The default registration is `UnconfiguredMigrationReconciliationApprovalPolicy`, which returns null (`MigrationServiceCollectionExtensions.cs:39`, `MigrationReconciliationContracts.cs:82-86`). Both approve and handover reject with `approval_policy_not_configured` (`MigrationReconciliationService.cs:164,224`). SoD is at `:169`. OutcomeUnknown and incomplete runs block (`:347-350`).
- CI shape (`ci.yml`): Ubuntu/Windows/Ubuntu, .NET 10.0.400, Node 24.18.0, npm 12.0.1, the LocalDB exclusion, audits at `high`, and no CD.
- Issue #229 is OPEN with the `active` label. Issue #230 is OPEN with `not-activated`. There are 6 stashes. `frontend/assets` is untouched: the working tree is clean apart from the untracked `docs/handover/`.

## 4a. Planner-introduced mistake during this audit

`AUTOMATION_DEFECT (Planner-introduced)`: one background frontend command used an undefined `$TMPDIR_X` variable. Its log landed at `C:\Program Files\Git\fe-test.log` (806 bytes, an aborted `npm test` output). I stopped the command and reran it with the scratchpad path. The built-in safety check blocked me from deleting the stray file, so the **owner needs to delete it**. It is outside the repo, and no repository file was affected. This entry moves to `RESULT.md` once that file exists.

## 4b. Open questions for the owner (I have not guessed any answers)

1. **Routing authority.** Which one is authoritative: `AGENTS.md:72-76`, `HANDOVER_NOTES.md:169-183`, or the incoming model (Opus 5.5 Planner, Luna 6 xhigh default, Sol 6 critical reviewer, Sonnet 5 bug fixer)? Is Terra retired?
2. **Acceptance authority.** `.ai/AI_EXECUTION_POLICY.md` names Sol as acceptance authority. Does Opus 5.5 replace Sol in that policy? Who performs the acceptance review of Draft PR #262 (Slice 11)?
3. **CI.** The incoming model says "no CI/CD". This repo has active GitHub Actions CI plus required-check ruleset `22905800`. Keep CI, with no CD, as it is today?
4. **Handoff files.** Archive the 3,369-line `TASK.md` verbatim to `docs/history/`, make `TASK.md` a single-prompt file, and create `RESULT.md` and `.ai/MODEL_ROUTING.md`?
5. **Enforcement.** Adopt `architecture-enforcement.md`? That means freezing the 25 App module edges, including 3 bidirectional pairs, as shrink-only exceptions; accepting Slice 11's 1→4 raw-SQL lock sites as named exceptions; and adding R3 and R5. Should the bidirectional pairs be scheduled for removal, or only frozen?
6. **Reference syntax.** Use `MESP-<n> (#<issue>)` in commits and PRs instead of the Azure `#<ID>` form?
7. **Prompt cap.** Keep the 10-prompt-per-planner-conversation cap from `HANDOVER_NOTES.md:197`?
8. **Stale pointers.** May I correct the stale overlays in `AGENTS.md:40,55`, `CLAUDE.md:36-37`, and the three older `CURRENT AUTHORITY` headings in `.ai/CURRENT_STATE.md`? These are text-only edits.
9. **Obsolete script.** Retire `scripts/Update-MESP131-Docs.ps1`?
10. **Where to commit.** `docs/handover/` and `docs/audit/` are untracked on Slice 11's branch. Should they go on a separate `docs/` governance branch and PR, off `main`, so PR #262 stays focused?

## 5. Owner decisions

Answered by Hossam on 2026-09-24. These are the inputs for the Phase 2 cleanup session.

| # | Question | Decision | Consequence |
|---|---|---|---|
| 1 | Routing authority | **Adopt the new operating model:** Opus 5.5 is Planner, Luna 6 at xhigh is the default executor, Sol 6 reviews at critical points, Sonnet 5 fixes bugs. **Terra is retired.** | Replace the routing table at `AGENTS.md:72-76` with a pointer to a new `.ai/MODEL_ROUTING.md`. `HANDOVER_NOTES.md` routing becomes history. |
| 2 | Acceptance authority | **Opus 5.5 replaces Sol** as acceptance authority and accepts reviews. | Change every Sol-as-acceptance reference in `.ai/AI_EXECUTION_POLICY.md`. Opus 5.5 does the acceptance review of PR #262. |
| 3 | CI | **Keep the CI pipeline and ruleset** until the app is published to a specific server. CD waits for that target. | No change to `ci.yml` or ruleset `22905800`. Drop the "no CI/CD" line from the operating model for this repo. |
| 4 | Handoff files | **Yes.** | Archive `TASK.md` verbatim to `docs/history/TASK_LOG_to_2026-09-24.md`. `TASK.md` becomes a single-prompt file. Create `RESULT.md` (newest first) and `.ai/MODEL_ROUTING.md`. |
| 5 | Enforcement proposal | **Open: the owner asked for clarification.** | Re-ask in plain terms. See the chat reply of 2026-09-24. |
| 6 | Reference syntax | **`MESP-<n> (#<issue>)`.** No Azure DevOps. The repo is on GitHub. | Record this in the executor rules and routing file. |
| 7 | 10-prompt cap | **No cap.** | Drop the cap from `HANDOVER_NOTES.md:197`. Do not carry it into governance. |
| 8 | Stale pointers | **Yes, fix them.** | Correct `AGENTS.md:40,55` and `CLAUDE.md:36-37`. Rename `.ai/CURRENT_STATE.md` headings at L136, L177, L234 to `HISTORICAL RECORD`. |
| 9 | Obsolete script | **Yes, retire it.** | Delete `scripts/Update-MESP131-Docs.ps1`. |
| 10 | Where to commit | **PR to `main`, and merge if accepted.** | Use a separate `docs/` branch from `main` that carries `docs/handover/` and `docs/audit/` plus the governance cleanup. Keep PR #262 focused. |
