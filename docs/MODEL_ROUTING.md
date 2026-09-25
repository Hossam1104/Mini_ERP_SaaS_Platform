# Model Routing and Operating Model

This file is the single authority for AI roles, effort levels, prompt release, the handoff files and the
operating loop. [`AGENTS.md`](../AGENTS.md) points here and holds the executor rules.

The owner installed this model on 2026-09-25 (decisions Q1–Q10 and Q-A–Q-L, recorded in
[`DECISIONS.md`](DECISIONS.md)). It replaces the old governance, in which Sol was acceptance
authority and Terra was an executor. That governance is archived in
[`history/`](history/) (`AI_EXECUTION_POLICY_to_2026-09-25.md`, `AGENTS_to_2026-09-25.md`,
`handover-2026-09-24/`). Nothing in the archive is authority.

## 1. Roles and efforts

| Model | Role | Default effort |
|---|---|---|
| **Claude Opus 5.5** | **Planner / Architect / Acceptance Authority.** Covers planning, architecture, backlog, routing, quota, reviewing every result, acceptance, and the release go/no-go. Not a normal code executor. May edit governance and planning docs and the tracker backlog. | as needed |
| **Luna 6** | **Default executor and heavy scripting.** Covers implementation, refactors, live runs, Git/tracker hygiene, and doc updates. | **xhigh** for implementation, scripting, and live runs. **high** only for docs and bookkeeping. **max** only after an xhigh attempt failed on a critical task. |
| **Sol 6** | **Independent reviewer at critical points only** (§2). Advisory. Has no planning or acceptance authority. | **high**; **xhigh** when release-critical |
| **Claude Sonnet 5** | **Bug fixer** for contained, **already diagnosed** code or automation defects. That means one root cause, a few files, and a failing check to turn green. | **medium**; **high** for core lifecycle, money, or data-oracle fixes |

- The effort scale is low < medium < high < xhigh < max.
- Luna 6 is bug-prone even at xhigh. Never route it at medium for implementation.
- Models not listed here are not routed any work. GPT-5.6 Terra is retired (Q1).

## 2. Sol 6 critical points (the only times Sol is used)

1. Merging a stabilization or feature line into `main`.
2. The first implementation of new money, security, Tenant-isolation, or data-integrity logic.
3. Changes to core lifecycle, identity, authentication, or release-gate logic.
4. The release go/no-go before the owner ships to a client.

## 3. Routing and quota

- Route work to Luna 6 unless a rule says otherwise. Opus and Sol stay out of execution.
- Use Sonnet 5 only for a classified defect with an identified, bounded root cause. If the diagnosis
  is unclear, Luna 6 (xhigh) diagnoses first.
- Product defects become tracker **Bugs**. They are never "fixed" in test code.
- Batch related work into one prompt when its files and gates overlap, because each fresh session
  pays the read cost again. Where it is safe, validate several independent offline changes in one
  run.
- **Local gates and hosted CI are different evidence.** CI is kept (Q3), but hosted Backend CI
  excludes the LocalDB safety subset. Any change to SQL, money, stock, or persistence needs the local
  disposable-LocalDB gate as well.

## 4. Luna 6 guardrails (every Luna prompt carries all of them)

1. One concern per prompt. An offline refactor and a live run are separate prompts.
2. A file allowlist. Any change outside it is a deviation to revert or explain.
3. Explicit stop conditions instead of room to improvise.
4. Mandatory gates with the real output pasted and the wall time. A claimed pass without output
   counts as not run.
5. A self-review of the staged diff before each commit.
6. Opus reviews every Luna result before the next prompt is released.

## 5. Quota-saving plugins (every session, planner and executors)

| Plugin | Use it for | Instead of |
|---|---|---|
| Serena (MCP) | Symbol-level navigation and edits: `get_symbols_overview`, `find_symbol`, `find_referencing_symbols`, `replace_symbol_body`, `insert_after_symbol`. Call `initial_instructions` once first. | Reading whole files, or grepping for callers by hand |
| Ponytail (hook/skill, **full**) | Minimal-diff discipline: reuse, no speculative abstractions, the shortest working change, short prose | Over-built code and long prose |
| Context7 (MCP) | Current docs for any library, framework, or CLI you touch (`resolve-library-id` → `query-docs`) | Web search or guessing the API |

- Check availability at session start. If a plugin is missing or failing, say so in one line and
  continue with targeted reads. Install nothing.
- Every prompt names the relevant tools in its read order.
- **Ponytail never trims what governance requires:**
  - the RESULT.md entry;
  - pasted gate output and evidence;
  - input validation at trust boundaries;
  - fail-closed checks;
  - security, Tenant-isolation, and accounting rules.

  Ponytail is never authority.
- A harness without a plugin records that in its RESULT.md entry and falls back to targeted
  line-range reads.
- The standing tooling rule is `DETECT → VALIDATE → REUSE`. Before adding a framework, abstraction,
  or policy utility, find the existing pattern that already owns the concern. Examples: approval/SoD,
  idempotency, Tenant authorization, concurrency, audit, money, and persistence. Reuse it.

## 6. Prompt release

- After Opus reviews a result, it writes the next full executor or reviewer prompt into `TASK.md`
  (Status `OPEN`) **in the same session** and commits it. No `p` or other trigger message is needed.
  The owner removed the `p` gate on 2026-09-25 (Q-M).
- The owner reviews the prompt in `TASK.md`:
  - if the owner has comments, they tell Opus, and Opus revises the prompt and commits the revision
    before anything runs;
  - otherwise the owner hands it to the named executor directly.
- `TASK.md` holds one prompt at a time. Opus still writes the next-task summaries for the tasks
  after it.
- There is **no cap** on prompts per Opus conversation (Q7). The old "Prompt N/10" counter is retired.

## 7. Handoff files

**`TASK.md`**
- It holds only `## Next executor prompt` (Status: `OPEN` | `CONSUMED`) and the Planner's next-task
  summary.
- It never holds results.

**`RESULT.md`**
- It is the shared results log, newest entry first.
- Every model adds exactly one entry per session:

```markdown
## <YYYY-MM-DD> — <step / title> — <model> / <effort> — <work item(s)>
- Status: DONE | STOPPED | FAILED | ACCEPTED | REJECTED
- Branch / starting SHA / ending SHA:
- What changed: (files, commits)
- Gates: (real command output: counts, pass/fail, wall time)
- Evidence: (run IDs, report paths; no secrets)
- Deviations from the prompt:
- Failures and classification:
- Status files updated:
- Exact next action:
```

- Archive long old logs **verbatim** to `docs/history/RESULT_LOG_to_<date>.md`. Never rewrite them.

**Where live state lives.** Live project state is:
- `RESULT.md`, newest first;
- [`ROADMAP.md`](ROADMAP.md);
- the tracker.

Live Git and the tracker outrank every Markdown file for mutable facts such as branch, PR, and issue
state.

## 8. Operating loop

1. The owner opens a fresh executor session with the named model and effort and says: *"Execute the
   prompt in TASK.md."*
2. The executor does the work, adds its RESULT.md entry, marks the prompt `CONSUMED`, and commits.
3. The owner opens a fresh Opus session and says: *"Review the latest RESULT.md entry."* Opus then:
   - adds an `ACCEPTED` or `REJECTED` entry with reasons;
   - updates ROADMAP and the tracker;
   - writes the full next prompt into TASK.md and commits it (§6).
4. The owner reviews the prompt. If they have comments, Opus revises it. Then back to step 1.

**Review checklist for Opus.** These failure patterns have recurred with executors:
- A PASS reported while the exact requested oracle was only covered indirectly. Check the exact
  assertions and the public read path, not adjacent tests or the mere presence of a schema.
- An executor stopping too early on an open Production decision when the BRD explicitly allows a
  bounded, decision-neutral slice.
- An executor missing persisted owner evidence and asking for a new cross-module interface. Inspect the
  historical evidence first.
- Hosted CI accepted as provider evidence (CI excludes LocalDB), or expensive full suites re-run where
  reused evidence is enough. State which one applies.
- Static state text trusted over live Git and tracker state.
- Generated directories deleted without first proving what is at the exact path.

## 9. Writing prompts (they run cold)

**Every prompt contains:**
- the model and effort;
- the work item(s) as `MESP-<n> (#<issue>)`, and the branch;
- a minimal read order with Serena and Context7 hints;
- a starting-state check;
- the exact scope, with a file allowlist and an out-of-scope list;
- the gates;
- the stop conditions;
- the hand-back: a RESULT.md entry plus status updates;
- the acceptance criteria Opus will check.

**Starting state.** Never pin it to a SHA that the prompt's own commit will move. Use: "HEAD descends
from `<sha>`, and `git diff --name-only <sha> HEAD` lists only `<planner files>`".

**Delivery.** State the model, effort, and routing reason, and say "open a new session". Then give
exactly one fenced Markdown prompt, and nothing after the closing fence.

**Skeleton:**

```markdown
# <MESP-n (#issue)> — <title>
Model: <model> — Effort: <effort> — Fresh session
## 1. Role and authority        (executor; Opus 5.5 accepts; AGENTS.md rules apply)
## 2. Read order                (AGENTS.md → this prompt → owning BRD/ADR sections; Serena/Context7 hints)
## 3. Starting-state check      (HEAD descends from <sha>; tree clean; tracker state)
## 4. Business / architecture rules to preserve
## 5. Scope + file allowlist
## 6. Out of scope
## 7. Acceptance matrix
## 8. Gates                     (exact commands; paste output + wall time)
## 9. Git / PR delivery         (branch, Draft PR, what is NOT authorized)
## 10. Stop conditions
## 11. Hand-back                (RESULT.md entry, TASK.md → CONSUMED, tracker evidence)
```

## 10. Working decisions

- **Code is the fact.** If two governing docs conflict, don't pick one. Record the conflict and ask
  the owner.
- **Commit references.** Commits, PRs, docs, and RESULT.md reference the tracker item as
  **`MESP-<n> (#<issue>)`** (Q6).
- **Branches.** Use bounded branches, `feat|fix|docs|chore/mesp-<n>-<slug>`. Push or open a PR only
  when the task calls for it.
- **Merges.** Partial work is never merged to `main` for tidiness. A merge follows Opus acceptance
  **and** a Sol 6 review (§2). Ruleset `22905800` requires a PR plus the checks `Repository Validation`,
  `Backend`, and `Frontend`. Never bypass it.
- **Classify every failure** as one of: product defect, automation defect, environment, test data,
  database connectivity, configuration, or inconclusive. Only product defects become tracker Bugs.
- **Test hygiene.**
  - No fixed sleeps; use bounded condition waits.
  - No hard-coded environment data or secrets.
  - DB validation is read-only unless the task authorizes otherwise.
  - No live, destructive, or financial actions without explicit task authorization.
- **CI/CD.** CI is kept and CD is deferred until the app is published to a server (Q3).
- **Chat replies** are a short summary plus questions or the next step. Details go in files.
- **Record your own mistakes** in RESULT.md with a classification, e.g. `AUTOMATION_DEFECT
  (Planner-introduced)`.

## 11. Tracker conventions (GitHub Issues + Project #1)

- **Tracker.** The tracker is GitHub Issues plus Project
  [`MESP — Mini ERP SaaS Platform`](https://github.com/users/Hossam1104/projects/1). **Jira is
  read-only historical provenance.** Never write to it.
- **Issue titles** are `[MESP-<n>] <summary>`. The Project `Jira Key` field is `MESP-<n>`, including
  for items born on GitHub, so there is one key space. The next free key is the highest in use + 1.
- **`Parent / Epic`** is `[MESP-<epic>] #<issue>`. Every non-epic item has a parent.
- **Status** is Todo, In Progress, or Done. Closed ⇔ Done.
- **Epics** move to Done only by an explicit owner or acceptance decision. It is never inferred from
  their children.
- **Capability State** is Backlog, Active, Accepted, Historical, Gate, or Not Activated. Only positive
  authority activates a capability.
- **No hard deletes.** Close an item as *not planned*, with a reason comment.
