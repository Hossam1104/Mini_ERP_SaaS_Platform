# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7 and §10.

## Next executor prompt

Status: **OPEN** (first release, 2026-09-27). The Planner launches the child through Paseo and passes
this contract verbatim (`MODEL_ROUTING.md` §6, §10).

```markdown
# MESP-168 (#289) — MESP-141 closure audit: trace BRD 40 to implementation and tests

## 1. Role and authority
- You are the **executor, read-only audit**. Opus 5.5 accepts or rejects your result and alone decides
  whether MESP-141 (#229) closes. `AGENTS.md` binds you, §1 and §5 especially. Authorization is
  positive: every action this prompt does not list is forbidden.
- Work item: MESP-168 (#289), a Task under MESP-15 (#104), for capability MESP-141 (#229). Slices 1–11
  are merged and accepted on `main`.

## 2. Read order
1. `AGENTS.md`, then this prompt, then `gh issue view 289` (read-only).
2. `docs/requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md`:
   - §8 (`M40-REQ-*`, about 86 rows);
   - §23 (the MESP-141 implementation contract);
   - §26 (open decisions);
   - §27 (acceptance criteria);
   - §29 (traceability).
3. `docs/ROADMAP.md` MESP-141 row; `docs/DECISIONS.md` for anything that defers or narrows M40 scope.
4. Code, read-only. **Serena:** call `initial_instructions` once, then `find_symbol` /
   `get_symbols_overview` / `find_referencing_symbols`; never read whole files. Main areas:
   - `backend/src/MiniErp.App/Modules/Migration/`;
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/`;
   - the Migration API mappings;
   - `backend/tests/MiniErp.ArchitectureTests/Migration*Tests.cs`;
   - the Migration parts of `SqlServerSafetyTests.cs`;
   - any Migration UI under `frontend/src/app/features/`.
- **Context7:** not needed. **Ponytail:** full; it never trims evidence. If a plugin is missing, say so
  in one line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- A fresh worktree from `origin/main`. HEAD descends from `1d2b75a` (the PR #288 merge), and the tree is
  clean.
- `gh issue view 289 --json state` and `gh issue view 229 --json state`: both OPEN.
- Anything else is a stop (§10).

## 4. Rules to preserve
- **Read-only.** No code, test, BRD, governance or tracker change beyond §9.
- **Evidence, not inference.** "Met" needs a production file:line **and** a test that asserts it.
  Code without a test is "Partial"; a test without matching code is not evidence.
- Never invent a business rule or decide an open question. Open M40 decisions (e.g. M40-DEC-001) are
  recorded as "Deferred-by-authority" with their source.

## 5. Scope and file allowlist
1. Create `docs/audit/mesp-141-m40-traceability.md` with:
   - **One table row per requirement:** every §8 `M40-REQ-*` and every §27 acceptance criterion.
     Columns: `ID | Requirement (≤ 15 words) | Status | Implementation file:line | Test(s) | Notes`.
     Status is one of `Met`, `Partial`, `Not met`, `Deferred-by-authority`, or
     `Depends-on:<capability>`. Examples of the last: M27 Tenant lifecycle; Platform Administration
     Wave 1, MESP-2 (#91) / MESP-65..85; MESP-48; MESP-50.
   - **Counts** per status.
   - **Gap groups:** the `Partial` and `Not met` rows grouped into candidate slices. Give each a
     one-line scope and name the owning module. Keep `Depends-on` rows in their own group.
   - **§29 check:** every ID in the BRD §29 matrix appears in your table. List any the BRD references
     but never defines.
2. `RESULT.md` (one new top entry) and `TASK.md` (Status → CONSUMED). No other file.

## 6. Out of scope
- Fixing anything, creating Bugs or Tasks, and changing Capability State or Status.
- Proposing business rules.
- Running live migrations or any database write.

## 7. Acceptance matrix (Opus checks each)
| # | Criterion |
|---|---|
| A1 | Every §8 `M40-REQ-*` and every §27 criterion has exactly one row. The counts reconcile to the BRD's own row counts (state them). |
| A2 | Every `Met` row cites a production file:line and a named test. Opus spot-checks at least 10. |
| A3 | Every `Deferred-by-authority` row cites its authority (BRD section, decision ID or issue). |
| A4 | Gap groups are coherent candidate slices with owning modules; `Depends-on` rows are separated. |
| A5 | The diff is exactly the audit file, `RESULT.md` and `TASK.md`. |

## 8. Gates
1. `git diff --check` is clean. `git diff --name-only origin/main HEAD` lists only §5 files.
2. Backend suite: `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false`, because a `docs/` file is added
   and the architecture tests read repository files. Report 0 warnings / 0 errors, the pass count
   (baseline **1557**), 0 skipped, the disposable-database line and "MESP data is intact", with wall
   time. If the Release build is locked by the running dev API (MSB3026), run it with
   `-Configuration Debug` instead and say so. **Do not stop or kill the dev runtime.**

## 9. Git / PR delivery (positive authority, exactly this)
- Branch `docs/mesp-168-m40-traceability` from `origin/main`. Self-review the staged diff.
- Commit `docs(migration): MESP-168 (#289) BRD 40 traceability audit`; push normally.
- Open a **Draft** PR to `main` titled `docs(migration): MESP-168 (#289) MESP-141 closure audit`. Its
  body carries the status counts and gap groups.
- One evidence comment on #289 with the counts.
- **Runtime restart:** you are in a Paseo worktree, so record "restart: Planner (worktree)"
  (`MODEL_ROUTING.md` §4.7, Q-S). Do not run the launcher.
- **NOT authorized:**
  - Ready, reviewers, approval or merge;
  - rebase, force-push or any push to `main`;
  - closing or reopening an issue, Status or Capability State changes, new issues.
- Then **STOP.**

## 10. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match.
- The change would need a file outside §5.
- The BRD is internally inconsistent in a way that blocks classification. Record the conflicting
  lines and stop the affected rows only; classify the rest.
- Any red in the gate: capture the full failure, classify it and stop.

## 11. Hand-back
- One `RESULT.md` entry at the top per `MODEL_ROUTING.md` §7, Status `DONE` or `STOPPED`. It holds:
  - the starting state;
  - the status counts;
  - the gap-group summary;
  - the gate output and wall time;
  - the PR URL;
  - deviations.
- **Exact next action:** "Opus 5.5 reviews MESP-168 (#289) and decides MESP-141 closure or remaining
  slices."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the summaries untouched.
```

## Next-task summaries (Planner, 2026-09-27)

Accepted Executor cycles since the last Sol review: 2 (`ORCHESTRATION_STATE.yaml`; MODEL_ROUTING §2).

1. **MESP-168 (#289)**: the prompt above. Opus then decides whether MESP-141 closes or which slices
   remain.
2. **MESP-151 (#266): Golden Release-1 end-to-end cycle** (Executor).
3. **Platform Administration Wave 1 (Q-Q)**: MESP-65..85, batched by Opus.
4. **UI lane (Q-R)**: waits for the owner's reference UI example.

### Owner actions pending
- Provide the reference UI example for MESP-153 (#268) (Q-R).
