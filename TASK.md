# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7.

## Next executor prompt

Status: **NONE.** No prompt has been written. Opus 5.5 writes one only when the owner sends `p`.

## Next-task summaries (Planner, 2026-09-25)

### 1. MESP-150 (#265): acceptance review of MESP-141 Slice 11

| | |
|---|---|
| Model / effort | Claude Opus 5.5, fresh session. It is an acceptance review, which is Opus's role. |
| Work items | MESP-150 (#265), capability MESP-141 (#229), parent MESP-15 (#104) |
| Why | Slice 11 (migration reconciliation, approval, Ready-for-Handover) entered `main` via PR #262 (`ac0309a`) without an acceptance review (D-15). Owner decision Q-B makes this review the first task after the cleanup. |
| Scope | 1. Review PR #262's diff against Slice 11 acceptance matrix R01–R20 and BRD 40 (`docs/requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md`). <br>2. Verify: fail-closed without an approval policy; material or unexplained mismatches block; no balancing Journals or stock adjustments; `Outcome Unknown` blocks; partial completion is visible; stale evidence invalidates approvals; Ready-for-Handover never activates a Tenant; Tenant A cannot read, reconcile or approve Tenant B. <br>3. Confirm the D-18 cancellation fix (`3cacf79`). |
| Out of scope | Deciding M40-DEC-001..006. Activating later MESP-141 slices. MESP-142. Any code change: defects are filed as Bugs and routed separately. |
| Acceptance | An `ACCEPTED` or `REJECTED` entry in `RESULT.md`, with evidence for each matrix row. ROADMAP and the tracker are updated. |

### 2. Independent critical review of the cleanup on `main`

| | |
|---|---|
| Model / effort | **Sol 6 / high**, fresh session. This is Sol critical point 1: governance and architecture on `main`. |
| Work items | MESP-149 (#264), epic MESP-145 (#263) |
| Range | `pre-cleanup-20260925..main`. The tag is on `5ae718a`. |
| Scope | Read-only review of the following. <br>1. The D-18 behavior change. <br>2. The R2–R5 ratchets and their allowlists. <br>3. The doc moves: nothing lost, and no dangling references outside `docs/history/` and `docs/audit/`. <br>4. The five core docs against the code (the code is the fact). <br>5. `AGENTS.md` executor rules against the archived policy: no authority was widened. <br>6. Tracker reconciliation against `docs/audit/tracker-reconciliation.md`. |
| Out of scope | Mutations of any kind. Sol reports findings; Opus 5.5 decides. |
| Acceptance | A `RESULT.md` entry with findings ranked by severity, each with file:line evidence. Opus reviews it next. |
