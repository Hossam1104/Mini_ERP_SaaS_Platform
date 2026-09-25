# Cleanup Plan — Phase 2 (PROPOSED, awaiting owner answers)

- **Date:** 2026-09-25.
- **Planner:** Claude Opus 5.5.
- **Baseline:** cleanup baseline `ac0309a`, plus local audit commits (see `branch-consolidation.md`).
- **Inputs:** `drift-report.md` (D-01 to D-23 and the §5 decisions), `tracker-reconciliation.md`, and `architecture-enforcement.md`.

Nothing in this file has been executed. §g lists the questions that gate Phase 2.

**Guiding choices:**
- Code moves: **none**. The 4-project, 12-module layout is ADR-002-enforced and sound, so relocating code would be churn with no benefit.
- Code fixes: only the diagnosed ones (§c).
- The bulk of Phase 2 is docs and governance consolidation.

---

## a. Documentation inventory and action per file

Target layout (from the cleanup contract):

```
/README.md  /AGENTS.md  /TASK.md  /RESULT.md  /RUN.md   (+ CLAUDE.md reduced to the single line "@AGENTS.md")
/docs/PROJECT.md  ARCHITECTURE.md  ROADMAP.md  DECISIONS.md  MODEL_ROUTING.md
/docs/audit/   /docs/history/   /docs/assets/
```

**Action vocabulary:**
- **MERGE→X:** the current content goes into core doc X.
- **HISTORY:** the file moves verbatim, with `git mv`, under `docs/history/`.
- **MERGE+HISTORY:** the current part goes into X, and the full original moves to history.
- **REF:** see Q-G (the BRD placement question).
- **DELETE:** the content is fully covered elsewhere or is a stub. Verbatim history is always kept in git.

### Root and `.ai/`

| File | Lines | Action |
|---|---|---|
| `README.md` | 356 | **KEEP, trimmed.** Keep: product overview, stack, quick start, and quality checks. Status/capability-matrix text that duplicates ROADMAP → ROADMAP. |
| `AGENTS.md` | 776 | **KEEP, rewritten.** It must stay at the root (`MigrationFoundationTests.cs:1083`). It carries: the operating model (roles, `p` gate, loop, prompt rules), the executor policy (merged from `.ai/AI_EXECUTION_POLICY.md`), the Ponytail rule, the permanent architecture rules (ADR-019), the REST/API DoD, and the asset protection rule. The 14 historical overlays → `docs/history/AGENTS_overlays_to_2026-09-24.md`. |
| `CLAUDE.md` | 505 | **Reduce to the single line `@AGENTS.md`.** The overlays → `docs/history/CLAUDE_overlays_to_2026-09-24.md`. |
| `TASK.md` | 3,369 | **HISTORY** → `docs/history/TASK_LOG_to_2026-09-25.md` (verbatim). The new `TASK.md` follows the contract template (Q4). |
| `RESULT.md` | — | **CREATE** (newest first). Its first entry is this cleanup. |
| `Run.md` | 389 | **RENAME → `RUN.md`.** The dated "MESP-144 reconciliation record" block (L3–78) → history. The runbook content stays. |
| `.ai/AI_EXECUTION_POLICY.md` | 130 | **MERGE→AGENTS.md** (§ Executor authorization), with Sol→Opus 5.5 acceptance (Q2). Then delete the file. |
| `.ai/AI_TOOLING_SETUP.md` | 53 | **MERGE→AGENTS.md** (Ponytail section; most of it is already duplicated there). Then delete the file. |
| `.ai/CURRENT_STATE.md` | 4,681 | **MERGE+HISTORY.** The current authority block → `docs/ROADMAP.md` § Current state, rewritten for the post-cleanup truth (D-01, D-15). The full file → `docs/history/CURRENT_STATE_log_to_2026-09-25.md`. |
| `.ai/migration/MESP_GITHUB_MIGRATION_MANIFEST.md` | 1,359 | **HISTORY** → `docs/history/`. It is Jira→GitHub migration provenance. |

### `docs/`

| File(s) | Action |
|---|---|
| `docs/handover/ARCHITECTURE.md` | **MERGE→`docs/ARCHITECTURE.md`**, as the backbone, with the D-01/D-09/D-10 corrections. Then HISTORY → `docs/history/handover-2026-09-24/`. |
| `docs/handover/ROADMAP.md` | **MERGE→`docs/ROADMAP.md`**, as the backbone, updated for Slice 11 being merged. Then HISTORY. |
| `docs/handover/DECISIONS.md` | **MERGE→`docs/DECISIONS.md`**. Then HISTORY. |
| `docs/handover/HANDOVER_NOTES.md` | Split. Conventions / tests / git → `AGENTS.md`. Role and prompt governance → `MODEL_ROUTING.md` (the prompt cap is dropped, per Q7). Then HISTORY. |
| `docs/README.md` (index) | **DELETE.** The document map moves into `docs/PROJECT.md`. |
| `docs/01_Business_Vision.md` and `02`–`10` (9 stubs, 5–7 lines each) | **MERGE→PROJECT.md** (the one line each has), then **DELETE** (D-21). |
| `docs/00_ERP_Business_Glossary.md` (2,767) | **REF** (Q-G). PROJECT.md links it. |
| `docs/01_Technology_Architecture_Baseline.md` (1,017) | **MERGE+HISTORY.** The still-valid ADR-010..015 open-decision rows → DECISIONS.md. The rest is superseded by the 4-project topology. |
| `docs/ADR-002/004/006/007/008/009/018/019` (8 files, ~1,290 lines) | **MERGE→DECISIONS.md** as full verbatim sections (the ADR text is live authority), plus a decision log table. The originals → HISTORY. |
| `docs/Decisions.md` (438; the ADR index plus 9 stale "Current" overlays) | The index → DECISIONS.md. The overlays → HISTORY. |
| `docs/34`–`38` capability architecture records (~1,250) | **MERGE+HISTORY.** A one-paragraph summary per capability → ARCHITECTURE.md § Module notes. The full records → `docs/history/architecture/`. |
| `docs/CI_CD_Governance.md` (141) | **MERGE→ARCHITECTURE.md** § CI/CD (keep CI, no CD; per Q3). Then HISTORY. |
| `docs/11`–`14`, `16`, `21`–`25`, `28`, `29`, `40` BRDs (approved baselines, ~16k lines) | **REF** (Q-G). These are live requirements, not history. |
| `docs/15`, `17` lean implementation specs; `18`, `19`, `20` readiness; `26` regulatory readiness; `MESP-143_…Plan.md` | **HISTORY** (the implementation of those capabilities is merged and accepted). |
| `docs/27`, `30`, `31`, `32`, `33`, `94` (Release 1 plan and decision docs) | **MERGE+HISTORY.** The approved decisions (PD-024..046, the tax/VAT scope, the dependency map) → DECISIONS.md summary rows. The live plan → ROADMAP.md. The originals → history. |
| `docs/90`–`100` review/checkpoint records | **HISTORY** (the index already calls them historical). |
| `docs/96_Foundation_Release1_Safety_Validation.md` | **HISTORY**, and in the **same commit** update `SafetyCatalogueValidationTests.cs:76` to `docs/history/96_…md` (D-19). The table stays verbatim, and the test must stay green. |
| `docs/staticts.md` (3,593) | Q-G(ii). Recommended: fold the current snapshot into ROADMAP § Progress, move the file to HISTORY, and retire the AGENTS "Project Statistics Tracker" rule (RESULT.md plus ROADMAP replace it). |
| `docs/audit/*` | **KEEP.** `architecture-enforcement.md` and `executor-rules-proposal.md` are marked *adopted* or *superseded* once Phase 2 applies them. |
| `backend/README.md` (368), `frontend/README.md` (231) | **KEEP, trimmed** to component setup and commands. The dated MESP overlays (backend L1–247, frontend L1–156) → history. The contract's root rule does not cover component READMEs (Q-G(iii)). |

### Non-Markdown documents

| File | Action |
|---|---|
| `docs/MESP_PRD_v1.2.docx` | **MOVE → `docs/assets/`.** It is the canonical approved PRD; update the path references in the core docs. |
| `docs/Mini_ERP_SaaS_Platform_Project_Presentation.pptx` | **MOVE → `docs/assets/`** (Q-H). |
| `wireframes/*.dc.html` (7) | **MOVE → `docs/assets/wireframes/`** (Q-H). The dead reference to `05_Layout_Alternatives.md` inside one file stays as is, because the file is an owner design artifact. |

**Link integrity:** the only code, test or script readers of doc paths are `SafetyCatalogueValidationTests.cs:76` (docs/96) and `MigrationFoundationTests.cs:1083` (AGENTS.md). CI reads no docs. Relative links *inside* history files are left verbatim. Core docs link only to paths that exist; this is verified by a grep in slice S5's gate.

## b. Dot-files, dot-folders and `.txt` files

| Path | Tracked? | Verdict | Reason |
|---|---|---|---|
| `.github/workflows/ci.yml` | yes | **KEEP** | Active CI (Q3). |
| `.gitignore` | yes | **KEEP, extend** | Add `/.artifacts/` and `/backend/.s10-validation/`. |
| `.serena/project.yml`, `.serena/.gitignore` | yes | **KEEP** | Healthy tool configuration. |
| `frontend/.editorconfig`, `.gitignore`, `.prettierrc`, `.vscode/*` (3) | yes | **KEEP** | Standard Angular CLI and editor config. |
| `.ai/` (4 files) | yes | **DELETE after MERGE/HISTORY** (§a) | Content relocated. Nothing reads `.ai/` programmatically. |
| `.claude/settings.local.json` | no (global ignore) | **KEEP** | Machine-local tool config. |
| `.angular/cache/` | no (ignored) | **KEEP** | Build cache that regenerates. Leaving it is harmless. |
| `.vs/` | no (ignored) | **KEEP** | IDE-local. |
| `.artifacts/mesp131-p1-test/` | no | **DELETE** | Stale MESP-131 build output (bin/obj only). |
| `backend/.s10-validation/` | no | **DELETE** | Recursively self-nested .NET output. Paths exceed MAX_PATH, and it slows every `git status --ignored`. Removal uses a long-path-safe `Remove-Item -LiteralPath \\?\…` on that one folder only. It is **not** `git clean`. |
| `scripts/Update-MESP131-Docs.ps1` | no (`.git/info/exclude:7`) | **DELETE** plus remove the exclude line | Q9. |
| `.runtime/` (50 entries: launcher/backend/frontend logs, 6 local runtime DB folders, `stale-md-scan*.txt`) | no (ignored) | **UNCERTAIN → default KEEP** | Local runtime data from past acceptance runs, possibly owner-inspected. Harmless (ignored). See Q-I. |
| `.env*` | none exist | — | Nothing to list. |
| Tracked `.txt` | none | — | — |
| Stashes `@{0}`–`@{5}` | n/a | **UNCERTAIN → untouched** | `@{2}` contains a local dev password literal: **secret, never committed**. See Q-J. |

## c. Code issues and planned fixes

| ID | Issue | Planned fix | Characterization / test first |
|---|---|---|---|
| D-18 | 4× `catch { }` in the Migration reconciliation reads swallow faults and cancellation (`MigrationApOpeningExecutionCoordinator.cs:246`, `MigrationArOpeningExecutionCoordinator.cs:247`, `MigrationCashBankOpeningExecutionCoordinator.cs:157`, `MigrationGlOpeningExecutionCoordinator.cs:120`) | Change to `catch (Exception ex) when (ex is not OperationCanceledException)` with a logged warning (event ID + effect ID, no payload). A fault still yields `partial`, so the fail-closed behavior is unchanged. | 1 test per coordinator family (or 1 shared test if the coordinators share a seam): a Finance reader that throws `InvalidOperationException` → row `partial` (pins current behavior); one that throws `OperationCanceledException` → propagates (new behavior). The tests run in the non-SQL suite. |
| D-07 / D-08 / D-11 | Unenforced raw-SQL content, module-edge and suffix conventions | Install R2, R3, R4-content and R5 as tests only, **if Q-E = yes**. | The rules are themselves tests. First re-measure the 25 edges and 4 sites, and stop if they differ. |
| D-22 | Generated clutter | Delete it and extend `.gitignore` (§b). | `git status` clean; backend build green. |
| D-09 | Large classes (Identity 2,489 lines, Program.cs 1,361, and others) | **No split** in this cleanup. Add them to the ARCHITECTURE watch list. | — |
| D-23 | 227 bare fail-closed `catch` blocks | **No change.** Document the pattern. | — |
| D-13 | Bundle 514 kB > 500 kB budget | **No change** (accepted debt). | — |

No business rule changes. No test is weakened or deleted.

## d. Target architecture, folder tree, move map

**Code:** unchanged. See `docs/handover/ARCHITECTURE.md` §3 and §8, which become `docs/ARCHITECTURE.md`:

```
backend/src/{MiniErp.Api, MiniErp.Infrastructure, MiniErp.App, MiniErp.Contracts}   (ADR-002; R1 enforced)
backend/tests/MiniErp.ArchitectureTests   backend/tools/MiniErp.DevelopmentDataCutover
frontend/src/app/{core, features, shared}   frontend/e2e   frontend/assets (Owner-managed, untouched)
scripts/  (6 tracked PowerShell runners)
```

**Docs move map:** see §a. In summary:

```
docs/
  PROJECT.md        ← vision/scope/boundaries + stub content + document map (+ BRD/glossary index)
  ARCHITECTURE.md   ← handover ARCHITECTURE + CI_CD_Governance + 34–38 summaries + watch list + D-10 fix
  ROADMAP.md        ← handover ROADMAP + CURRENT_STATE authority (rewritten) + staticts current snapshot + 30/33/94 live plan
  DECISIONS.md      ← ADR-002..019 full text + Decisions.md index + handover DECISIONS + PD/approved-decision rows + open decisions
  MODEL_ROUTING.md  ← operating model roles, routing, p gate, prompt-writing rules (Q1, Q2, Q6, Q7)
  audit/            ← this audit (kept)
  history/          ← verbatim originals (TASK log, CURRENT_STATE log, overlays, handover, 15–20, 26, 27, 30–38, 90–100, ADR originals, manifest, staticts)
  assets/           ← PRD .docx, presentation .pptx, wireframes/
  <REF location>    ← BRDs 11–14,16,21–25,28,29,40 + glossary 00 (per Q-G)
```

All moves use `git mv`, so file history is preserved.

## e. Enforcement rules, in plain terms (for Q5 / Q-E)

The rules are **automatic tests** added to the existing architecture test project. They run in every local gate and in hosted CI. No new tool or package is needed.

- **R2, "module edges only shrink":** today 25 places exist where one business module reaches into another module's code. The test freezes that list. A new one makes the build fail. If someone removes one, the test tells them to strike it from the list, so the number can only go down. The 3 two-way pairs (Finance↔Sales, Inventory↔Sales, MasterData↔BusinessParties) are **frozen, not refactored**, unless you schedule their removal.
- **R3, "no module touches another module's database code":** it holds today with zero exceptions. The test keeps it that way.
- **R4, "raw SQL must be Tenant-scoped":** there are 4 raw SQL spots (one existing, plus three Slice 11 row locks). The test demands that each keeps its `TenantId` filter and stays a lock-only statement. It prevents a future raw query from leaking across Tenants.
- **R5, "LocalDB tests must be named so CI can skip them":** CI skips the LocalDB-only tests by name suffix. The test makes sure a new one cannot silently run, and fail, on hosted CI.

**Exception policy:** exceptions are named, each has a reason, and they may only shrink. Adding one needs an owner-approved task. An executor never widens a list to go green.

## f. Refactor slices and their gates

Each slice is one local commit on `main` (`MESP-<n> (#<issue>)` refs where one applies; cleanup-epic ref per Q-F).

**Full gate** means both of the following:
- **Backend:** `pwsh scripts/Test-MiniErpBackend.ps1 -NoBuild:$false`, expected 0 warnings, 1546+ tests passed, 0 failed.
- **Frontend:** `npm test` 316/316, `npm run build` (budget warning only), Playwright chromium 51/51, and both audits at 0 high/critical.

| # | Slice | Gate |
|---|---|---|
| S0 | Record the owner answers in drift-report §5.2. Tag `pre-cleanup-20260925` on the Phase 1 audit commit. | `git tag -n` |
| S1 | D-18 characterization and cancellation tests, plus the `catch` fix | Backend full |
| S2 | Enforcement tests R2/R3/R4/R5 (only if Q-E = yes) | Backend full; re-measured counts match |
| S3 | File cleanup §b: generated folders, the obsolete script, `.gitignore` | `git status` clean; backend build |
| S4 | Docs layout §a: `git mv` to history/assets/REF; create the 5 core docs; `CLAUDE.md` → `@AGENTS.md`; `Run.md` → `RUN.md`; archive `TASK.md`; create `RESULT.md`; update `SafetyCatalogueValidationTests.cs:76` | Backend full (catalogue and root-finder tests); grep for dangling core-doc links = 0; the root holds only the allowed files |
| S5 | Operating model in `AGENTS.md` and `MODEL_ROUTING.md`: roles (Opus 5.5 Planner and acceptance, Luna 6 xhigh default executor, Sol 6 critical review, Sonnet 5 bug fixes); the `p` gate; the TASK/RESULT templates; the loop; the prompt rules; the executor policy with Sol→Opus | Grep: no live "Sol acceptance" / "Terra" references outside history |
| S6 | Tracker reconciliation (only as authorized by Q-F) | `gh` re-query matches `tracker-reconciliation.md` |
| S7 | Final gates versus the §1 baseline; review the full diff `pre-cleanup-20260925..main`; `RESULT.md` entry; `TASK.md` next task: "Sol 6 / high — independent critical review of the cleanup on `main` (range `pre-cleanup-20260925..main`)"; push per Q-A | Full gate; the diff review is clean |

If context runs short: commit the current slice, write a STOPPED entry in `RESULT.md`, and set `TASK.md` to "Continue the cleanup from slice S<n>".

## g. Owner questions (gate Phase 2)

These are the same list as the chat reply.

- **Q-A.** The final push is blocked by ruleset `22905800` (D-16). Options: (1) push to one branch and merge a PR with a merge commit under your admin PR-bypass, **recommended**; (2) you temporarily edit the ruleset to allow a direct push.
- **Q-B.** Slice 11 (#262) is merged but not accepted (D-15). Should the Opus 5.5 acceptance review run as the first task **after** the cleanup (recommended), or inside it?
- **Q-C.** Two Owner assets exist only on the archive tag `archive/fix/MESP-123-angular-branding`: `frontend/assets/Saudi_Riyal.svg` and `wafra-logo.jpeg`. Restore them to `frontend/assets/`, or leave them archived?
- **Q-D.** Put the routing file at `docs/MODEL_ROUTING.md` (the contract) rather than `.ai/MODEL_ROUTING.md` (Q4 wording)? Recommended: `docs/`.
- **Q-E.** Install the enforcement tests R2–R5 (§e)? Should the 3 two-way pairs only be frozen, or also scheduled for removal as roadmap items?
- **Q-F.** Tracker: may I apply `tracker-reconciliation.md` (epic status fixes, the #238–#240 keys and parents, the cleanup epic, roadmap items)? These are GitHub Project/Issue lifecycle mutations and need your positive authority.
- **Q-G.** Docs that don't fit 5 files:
  - (i) The BRDs and glossary (~19k lines, live requirements): keep them verbatim under an extra `docs/requirements/` folder (recommended), put them under `docs/history/`, or merge them into PROJECT.md?
  - (ii) Retire `docs/staticts.md` and its AGENTS tracker rule?
  - (iii) Keep `backend/README.md` and `frontend/README.md`, trimmed?
- **Q-H.** Move the PRD `.docx`, the presentation `.pptx` and `wireframes/` to `docs/assets/`? Or delete the pptx or the wireframes?
- **Q-I.** `.runtime/`: keep it (ignored local runtime data), or may I delete its old logs and runtime DB folders?
- **Q-J.** Stashes: may I drop `@{4}` (superseded by #66), `@{5}` (already in `main`) and `@{0}`/`@{1}` (Serena config, superseded by `b3cd923`)? Keep `@{3}` (spec-kit)? `@{2}` holds a password literal. I recommend **you** drop it yourself, and I will not touch it.
- **Q-K.** Epics whose children are all Done (MESP-3/4/5/6/7/9/10/11/12/13): close them, or keep them open for Release 1 follow-on work?
- **Q-L.** The stray `C:\Program Files\Git\fe-test.log` is outside the repo, so I cannot remove it. Please delete it.
