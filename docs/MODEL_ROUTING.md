# Model Routing and Operating Model

AGENTS.md is canonical for role assignments, default efforts, and acceptance authority. This file retains detailed routing, prompt release, handoffs, and operating procedures.

The owner installed this model on 2026-09-25 (decisions Q1–Q10 and Q-A–Q-L, recorded in
[`DECISIONS.md`](DECISIONS.md)). It replaces the old governance, in which Sol was acceptance
authority and Terra was an executor. That governance is archived in
[`history/`](history/) (`AI_EXECUTION_POLICY_to_2026-09-25.md`, `AGENTS_to_2026-09-25.md`,
`handover-2026-09-24/`). Nothing in the archive is authority.

## 1. Roles and efforts

Role assignments, authority, and default efforts are normative in [AGENTS.md §7](../AGENTS.md#7-roles-and-loop).

## 2. Independent Review cadence and critical points

Accepted-cycle cadence and counter rules are normative in [AGENTS.md §7](../AGENTS.md#7-roles-and-loop).
Planner records the durable counter in ORCHESTRATION_STATE.yaml.

Independent Review is also triggered regardless of the periodic counter at
these MESP critical points:

1. review before merging a stabilization or major feature line to `main`
   when existing project authority requires independent review;
2. first implementation of new money, security, Tenant-isolation, or
   data-integrity logic;
3. material changes to core lifecycle, identity, authentication, authorization,
   or release-gate logic;
4. release go/no-go before owner delivery;
5. any additional owner-requested independent review.

A critical-point review does not grant merge, Ready, release, tracker, or
capability authority.

Sol reports findings.
Planner reconciles and accepts/rejects.

## 3. Routing and quota

- Role routing and Hard Bug criteria are normative in [AGENTS.md §7](../AGENTS.md#7-roles-and-loop).
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
7. **Runtime restart at the end of every Luna 6 and Sonnet 5 prompt (Q-N)**, whatever its status (DONE or STOPPED), after
   the final commit:
   - build with `dotnet build .\backend\MiniErp.sln --configuration Release` (skip it if the
     final gate already built Release on the final tree);
   - run `.\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart`
     from Windows PowerShell, with the owner's existing environment. Never print, set or write the
     connection string or any secret;
   - record the API and frontend URLs it prints, or its failure, in the RESULT.md entry. A failed
     restart is classified and reported; it is never fixed by editing the launcher or `.env*`.
   - **Every Luna prompt (Q-S, Q-T).** Executors run in the main checkout (Q-T), so the executor
     restarts after its last code commit, DONE or STOPPED. The launcher does not build: stop only the
     `MiniErp.Api` listener on port 5300, build Release, then run the launcher with `-Restart`. If a
     child ever runs in a worktree, the launcher's ownership guard blocks the restart there. The child
     records "restart: Planner (worktree)", and the Planner restarts from the main checkout.
   - The restart is the only runtime action this rule authorizes. It is not a gate and not a live,
     destructive or financial operation.

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
  - security, authorization, Tenant-isolation, and accounting rules;
  - data-loss safeguards;
  - accessibility.

  Ponytail is never authority.
- A harness without a plugin records that in its RESULT.md entry and falls back to targeted
  line-range reads.
- The standing tooling rule is `DETECT → VALIDATE → REUSE`. Before adding a framework, abstraction,
  or policy utility, find the existing pattern that already owns the concern. Examples: approval/SoD,
  idempotency, Tenant authorization, concurrency, audit, money, and persistence. Reuse it.

## 6. Autonomous task release

There is no routine owner `p` gate.

Planner owns `TASK.md`.

After accepting or reconciling the previous result, Planner:

1. determines the next task that is positively authorized by current project
   authority;
2. writes exactly one self-contained `Status: OPEN` executable contract into
   `TASK.md`;
3. creates/selects the appropriate Paseo execution workspace;
4. launches the configured child profile;
5. waits for completion;
6. reviews the result;
7. continues automatically when the next action is already authorized.

When Executor operates in a Paseo Git worktree, Planner must pass the complete
authoritative TASK.md contract in the child-agent instruction because an
uncommitted Planner-side TASK.md may not exist in the worktree.

Autonomy does not create authority.

The absence of a prohibition is not permission.

Planner must stop for owner/current-task authority when required for:

- Ready transition;
- merge;
- issue close/reopen;
- tracker Status or Capability State mutation;
- capability activation;
- production, destructive, financial or external action;
- unresolved business decision;
- material architecture or scope decision;
- credentials or infrastructure;
- any explicit STOP in current authority.

Routine technical implementation and correction do not require owner relay.

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

## 8. Autonomous Paseo operating loop

1. Planner reconciles:
   - live Git state;
   - GitHub tracker state;
   - AGENTS.md;
   - TASK.md;
   - newest RESULT.md entries;
   - ORCHESTRATION_STATE.yaml;
   - docs/ROADMAP.md;
   - applicable requirements, ADRs and architecture authority.

2. Live Git and the tracker outrank Markdown for mutable facts.

3. Planner determines the next positively authorized task.

4. Planner writes exactly one executable TASK.md contract.

5. Planner launches the configured `Executor` profile unless routing evidence
   requires another protected profile.

6. Executor performs the task, required gates, RESULT.md hand-back,
   TASK.md consumption, and only the Git/tracker delivery explicitly authorized.

7. Planner reviews:
   - TASK.md;
   - completion report;
   - RESULT.md;
   - git diff;
   - changed files;
   - required local gates;
   - applicable hosted CI evidence;
   - business/architecture acceptance criteria;
   - GitHub issue/PR state.

8. Planner returns:
   - ACCEPT;
   - REJECT;
   - BLOCKED.

9. Straightforward technical rejection:
   → focused Executor correction.

10. Genuine difficult technical defect:
    → Hard Bug / Sonnet 5 High.

11. ACCEPT:
    - update permitted roadmap/tracker state;
    - increment accepted Luna-cycle counter;
    - evaluate periodic and critical-point Independent Review triggers;
    - determine the next already-authorized task;
    - continue automatically.

12. When Independent Review is due:
    - launch `Independent Review`;
    - reconcile findings;
    - route corrections;
    - reset the periodic counter after review closure.

13. An explicit STOP remains a hard boundary.
    Do not automatically proceed past:
    - Draft/Open/Unmerged;
    - do-not-mark-Ready;
    - do-not-merge;
    - await-acceptance;
    - no-further-mutation;
    - or equivalent task authority.

14. Never infer Ready, merge, issue closure, capability activation or release
    authority merely from passing tests, CI, reviews, or acceptance.

## 9. Executor session lifecycle

One Luna Executor session normally owns one complete `TASK.md` contract.

A new TASK.md contract normally starts a fresh Executor session.

Do not replace Executor merely because Codex automatically compacted its
context.

Continue the same Luna session while:

- the objective is unchanged;
- execution remains coherent;
- completed work is not being repeated;
- repository/worktree state remains clear;
- and useful progress continues.

Start a replacement Luna session for the SAME task only when:

- repeated compaction causes material loss of state;
- Luna rereads or repeats completed work;
- the session becomes materially confused or contradictory;
- context/session exhaustion cannot recover through normal compaction;
- or a clean phase boundary allows safe bounded continuation.

Before replacement:

1. preserve the existing worktree and Git state;
2. write a bounded checkpoint of completed work, remaining work, validations
   and failures;
3. retain the same TASK.md authority;
4. launch the new Executor against the same worktree when safe;
5. instruct it to perform only remaining scope.

Planner does not micromanage normal automatic compaction.

## 10. Writing prompts (they run cold)

**Every prompt contains:**
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

**Delivery.** The Planner selects the appropriate configured Paseo profile and
effort at the orchestration layer.

The executable `TASK.md` contract remains model-neutral: it does not name or
instruct the child about its model or effort.

Planner launches the selected child agent through Paseo and passes the complete
authoritative TASK.md contract to it.

The owner does not manually relay routine executor prompts between Planner and
Executor.

**Skeleton:**

```markdown
# <MESP-n (#issue)> — <title>
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

## 11. Working decisions

- **Code is the fact.** If two governing docs conflict, don't pick one. Record the conflict and ask
  the owner.
- **Commit references.** Commits, PRs, docs, and RESULT.md reference the tracker item as
  **`MESP-<n> (#<issue>)`** (Q6).
- **Branches.** Use bounded branches, `feat|fix|docs|chore/mesp-<n>-<slug>`. Push or open a PR only
  when the task calls for it.
- **Merges.** Partial work is never merged to `main` for tidiness. A merge follows Opus acceptance,
  and Opus may perform it itself (Q-O). The periodic Sol 6 review (§2) covers merged work afterwards. Ruleset `22905800` requires a PR
  plus the checks `Repository Validation`, `Backend`, and `Frontend`. Never bypass it.
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

## 12. Tracker conventions (GitHub Issues + Project #1)

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
