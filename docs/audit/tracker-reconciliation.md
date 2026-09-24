# Tracker Reconciliation — PROPOSED (not applied)

- **Date:** 2026-09-25.
- **Planner:** Claude Opus 5.5.
- **Source:** live `gh project item-list 1 --owner Hossam1104`, read-only, re-verified 2026-09-25.
- **Authority:** GitHub Issues plus Project #1 are the tracker. Jira is read-only provenance and is **not touched**.
- **Rules for applying this:**
  - Every action below is a GitHub Issue or Project lifecycle mutation. Under `.ai/AI_EXECUTION_POLICY.md` §7a, each one needs **positive owner authority** (Q-F / Q-K).
  - Nothing is hard-deleted.
  - Jira is never written.

## 1. Snapshot

- **148** project items: **104** Done, **33** Todo, **11** In Progress.
- Every closed issue shows `Done`, and every `Done` item is closed, so there is no state/status mismatch.
- The 11 In Progress items are 9 epics, **#229 MESP-141** (the active capability) and **#112 MESP-23** (the living Open Questions Register).

## 2. Findings

| ID | Item(s) | Now | Problem | Proposed action |
|---|---|---|---|---|
| T-01 | #92 MESP-3, #93 MESP-4, #94 MESP-5, #98 MESP-9, #99 MESP-10, #100 MESP-11, #102 MESP-13 | In Progress / Active | Every child is Done. The Epic status is stale. | Q-K: set to **Done**, close, and put a comment linking the last child. *Or* keep them open, with a comment naming the Release 1 follow-on work that keeps each one open. |
| T-02 | #95 MESP-6 (20 children), #96 MESP-7 (6), #101 MESP-12 (3) | Todo / Backlog | Every child is Done, yet the Epic says it hasn't started. | Same as T-01. |
| T-03 | #97 MESP-8 | Done / Historical | Consistent. | None. |
| T-04 | #104 MESP-15 | Todo / Backlog | Its child #229 MESP-141 is **In Progress / Active**, and 1 child is Done. | Set to **In Progress / Active**. |
| T-05 | #90 MESP-1 | In Progress | 31 Done. Open: #137 MESP-48 (gate), #139 MESP-50 (gate), #230 MESP-142 (Not Activated), #112 MESP-23 (In Progress). | Consistent. None. |
| T-06 | #91 MESP-2 | In Progress | 10 Done. 21 Todo: #154–#174 (MESP-65..85, Platform Admin Wave 1 stories), most labelled `release-1`. | **Owner decision:** are these in Release 1 scope? If yes, add them to ROADMAP. If no, relabel them `deferred`. No closure either way. |
| T-07 | #103 MESP-14 | Todo | 1 Todo child: #128 MESP-39 (future-release integrations BRD). | Consistent. None. |
| T-08 | #238, #239, #240 (CI enablers, created 2026-09-11) | Todo / Backlog | No `[MESP-n]` title, no `Jira Key`, no Parent. | Allocate MESP-146/147/148 (the highest key in use is MESP-144, verified 2026-09-25), retitle to `[MESP-n] …`, and set Parent to the new cleanup/engineering epic (T-09). CI is kept (Q3), so these stay open. |
| T-09 | — | missing | No epic covers the architecture/project cleanup. | Create an **Epic `[MESP-145] Architecture & Project Cleanup`** (MESP-144 is taken). Child: the cleanup work item, closed at acceptance. |
| T-10 | — | missing | The agreed roadmap (from handover ROADMAP) has no tracker items after MESP-141. | Create Todo items in this order: (1) **MESP-141 Slice 11 acceptance review** (D-15); (2) **Golden Release-1 end-to-end cycle**; (3) **Stable functional/API baseline**; (4) **Total UI/UX modernization**. MESP-142 stays as is (#230, Not Activated). Parent each one to the right existing epic, or to the T-09 epic. |
| T-11 | #229 MESP-141 | In Progress / Active | Slice 11 is merged (#262 at `ac0309a`) without an acceptance review. Later slices are not activated. | No status change. Add a comment recording the Step Zero merge and the pending acceptance (D-15). |

## 3. Conventions (to record in `AGENTS.md` / `docs/MODEL_ROUTING.md`)

- **Issue title:** `[MESP-<n>] <imperative summary>`. The Project `Jira Key` field = `MESP-<n>`, even for items born on GitHub. This keeps one key space.
- **Parent / Epic field:** `[MESP-<epic>] #<issue>`. Every non-epic item has a parent.
- **References in commits, PRs, docs and RESULT.md:** `MESP-<n> (#<issue>)` (Q6).
- **Status set:** Todo, In Progress, Done. Closed ⇔ Done. An Epic moves to Done only by an explicit owner or acceptance decision; it is never inferred from its children.
- **Capability State:** Backlog, Active, Historical, and Not Activated (MESP-142). Only positive authority activates a capability.
- **No hard deletes.** Close as *not planned* with a reason comment instead.

## 4. Apply order (once authorized)

T-09 → T-08 → T-10 → T-04 → T-11 comment → T-01/T-02 (per Q-K) → T-06 (per owner scope answer). Afterwards, re-query the project and record the before and after counts in `RESULT.md`.
