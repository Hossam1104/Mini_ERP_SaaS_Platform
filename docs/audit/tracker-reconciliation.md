# Tracker Reconciliation — APPLIED 2026-09-25 (see §5)

> **Applied 2026-09-25** under owner decisions Q-F and Q-K (MESP-149 (#264)). §1–§4 below are the
> original proposal, kept unchanged. §5 is the log of what was applied.

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

## 5. Applied log (2026-09-25)

| Item | Done / pending | What happened |
|---|---|---|
| T-09 | **Done** | Created epic MESP-145 (#263), "EPIC 16 - Architecture & Project Cleanup" (In Progress / Active), and its child MESP-149 (#264), the cleanup execution (Governance, In Progress / Active). |
| T-08 | **Done** | Retitled #238–#240 to MESP-146/147/148, set `Jira Key`, set Parent `[MESP-145] #263`, and added a comment on each. They stay Todo / Backlog because CI is kept (Q3). |
| T-10 | **Done** | Created four items, all Todo / Backlog: MESP-150 (#265) Slice 11 acceptance review, parent MESP-15 (#104); MESP-151 (#266) Golden cycle; MESP-152 (#267) functional/API baseline; MESP-153 (#268) UI/UX modernization. The last three have parent MESP-1 (#90). |
| (debt) | **Done** | Created MESP-154 (#269), architecture exceptions and size watch, and MESP-155 (#270), bundle and npm advisories. Both are Todo, parent MESP-145 (#263). |
| T-04 | **Done** | Set #104 MESP-15 to In Progress, Capability State Active, Classification Active, and added a comment. |
| T-11 | **Done** | Commented on #229 MESP-141 recording the Step Zero merge of Slice 11 (#262 at `ac0309a`) and the pending acceptance MESP-150 (#265). No status change. |
| T-01 / T-02 | **Done: comment only (Q-K)** | Commented on #92, #93, #94, #95, #96, #98, #99, #100, #101 and #102 that all children are Done (verified) and that closure is pending owner review. **None were closed or moved.** They are listed in `ROADMAP.md`. |
| T-06 | **Done: no change (Q-F)** | #154–#174 keep their `release-1` labels. They are listed in the ROADMAP backlog. |
| T-03 / T-05 / T-07 | No action | Consistent. |
| Link comments | **Done** | Commented on #263 and #90 to link ARCHITECTURE and ROADMAP. |
| Epic closure | **Pending: owner** | The owner closes the Q-K epics after review. |

**Project counts** (`gh project item-list 1 --owner Hossam1104 --limit 300`):

| | Items | Done | In Progress | Todo |
|---|---|---|---|---|
| Before (2026-09-25, pre-apply) | 148 | 104 | 11 | 33 |
| After (2026-09-25) | 156 | 104 | 14 | 38 |

The delta is +8 items: MESP-145 and MESP-149..155.
- In Progress: +3 (#263, #264, #104).
- Todo: +6 new, −1 (#104 moved to In Progress).
- Nothing was deleted or closed. Jira was not written.
