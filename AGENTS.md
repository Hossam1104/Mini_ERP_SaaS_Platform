# Repository Working Agreement

These rules bind every AI executor and reviewer working in this repository, including Claude Code,
Codex and any other agent.

- **Roles, efforts, prompt release, handoff files and the operating loop** are defined only in
  [`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md).
- **Live project state** is in:
  - [`RESULT.md`](RESULT.md), newest entry first;
  - [`docs/ROADMAP.md`](docs/ROADMAP.md);
  - the tracker.
- **Live Git and the tracker outrank every Markdown file** for mutable facts.

Before you act, read:
1. this file;
2. `TASK.md`;
3. the latest `RESULT.md` entry;
4. only the sections of [`docs/PROJECT.md`](docs/PROJECT.md), [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md),
   [`docs/DECISIONS.md`](docs/DECISIONS.md) and `docs/requirements/` that your task touches.

## 1. Executor authorization

Opus 5.5 is Planner and acceptance authority (Q2). Luna 6 executes, Sonnet 5 fixes diagnosed bugs, and
Sol 6 reviews independently once every 10–15 executor prompts (Q-N) and holds no acceptance authority. The owner may operate GitHub manually at any time. These rules limit
**AI executors**.

The owner has given Opus 5.5 a standing delegation (Q-O): full authority on GitHub and the repository
to accept, close, mark Ready and merge as it sees fit. That delegation is Opus's alone. The owner keeps
business corrections and the review of each next task. Ruleset `22905800`, the required checks and §5
still bind Opus.

1. **An explicit STOP is a hard boundary.** Treat any of these as a hard stop:
   - "stop for review";
   - "leave Draft/Open/Unmerged";
   - "do not mark Ready";
   - "do not merge";
   - "await acceptance";
   - "no further mutation".

   Once you reach it, make **zero further mutations** beyond what your final report needs. That means
   no:
   - commit, push or force-push;
   - Ready transition, reviewer request, approval or merge;
   - close, reopen, rebase or update-branch;
   - tracker write;
   - next task.
2. **Authorization is positive, never inferred.** The absence of a prohibition is not permission.
   - Push ≠ merge. Create PR ≠ Ready. Review ≠ fix. Fix ≠ merge.
   - Passing tests, CI, a clean review or a bot review authorize nothing.
   - Acceptance of an earlier task authorizes nothing later.
   - Never manufacture your own next phase or next capability.
3. **Ready and merge need explicit authority in the current task.** Merge authority must also state
   its conditions. Opus 5.5 holds that authority standing, under Q-O.
4. **Your report is immutable.** Once you report that you stopped or handed off, the session is over.
   A later bot comment, finished CI run or mergeable PR does not reopen it.
5. **External and bot reviews are evidence, not authority.** This covers Copilot, other bots, CI and
   scanners. If a bot review is unavailable, that never licenses your own Ready or merge decision.
6. **Ponytail is never authority.** It cannot override a STOP. It cannot authorize a commit, Ready,
   merge or tracker change. It cannot weaken security, Tenant isolation, accounting integrity, audit,
   concurrency or acceptance gates.
7. **Tracker lifecycle writes are mutations.** These need positive authority in the current task:
   - closing or reopening an issue;
   - changing `Status` or `Capability State`;
   - activating a capability.

   **Jira is read-only historical provenance. Never write to it.**
8. **Attribute actions only by evidence.** Record an Owner action by the evidence actually available.
   Never claim that an AI executor did something the evidence does not show.

**Why these rules exist.** On 2026-08-29, PR #81 moved past its authorized Draft/Open/Unmerged STOP:
- it received more commits;
- it was marked Ready;
- a Copilot review was requested;
- it was merged at `c8c9084`.

The GitHub evidence attributes these actions to the owner account. It does not show who or what
performed them. No product code was affected.

## 2. Permanent product and architecture rules

- **Release 1 is B2B ERP only.**
  - No Retail POS.
  - Wafra is a validation tenant only. There is no Wafra-specific schema, workflow, permission,
    pricing, approval, accounting or lifecycle behavior.
  - No ZATCA/FATOORA implementation or readiness claim.
  - No external production integrations, providers or credentials.
- **Tenant ≠ Workspace (ADR-019).**
  - Tenant is the server-authorized isolation boundary.
  - The operational context (Company/Branch) sits inside an authorized Tenant.
  - The hostname only suggests a *candidate* Tenant; it never grants authorization.
  - Users land on Overview first. A single context is auto-selected; several contexts use the header
    switcher. Never ask for a raw GUID.
  - The Platform Administrator role alone grants no Tenant ERP data.
- **Branding and SAR.**
  - Tenant branding is configuration data with an MESP fallback. Never write `if tenant == Wafra`.
  - The SAR symbol is presentation only: no FX, tax, accounting or persisted-amount effect.
  - Non-SAR currencies are unaffected.
- **Finance owns accounting (FIN-OD-01).** Operational modules own their source documents and never
  fabricate journals.
- **REST/API Definition of Done.** Every public REST operation must be all of these:
  - in the Foundation operation catalogue, with its exact route, permission, scope, antiforgery,
    audit and unsafe-effect metadata;
  - mapped by the real API;
  - in the generated OpenAPI document, with a stable `operationId`, a useful summary and boundary
    description, and explicit responses;
  - covered by a contract test that rejects missing or placeholder documentation.

  Scalar is only the Development/QA rendering of that document, with agent actions disabled. It is
  never a Production feature.
- **Layers and modules** follow [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) §2–§3 and §7:
  - Api → Infrastructure → App → Contracts.
  - Modules call each other only through public contracts.
  - Direct cross-module DbContext or table access is forbidden.
  - The ratcheted exceptions may only shrink. Never extend an allowlist to get green.
- **Owner-managed assets.** Files under `frontend/assets` are product source assets. Never delete,
  rename, replace, regenerate, optimize, recolor, move or restore them from Git without explicit owner
  instruction. Untracked images there are not temporary. `frontend/assets/brand` holds only generated
  browser derivatives (favicons and touch icons).

## 3. Conventions

- **References.** Use `MESP-<n> (#<issue>)` in commits, PRs, docs and RESULT.md.
- **Tracker.** Follow [`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §11:
  - titles are `[MESP-<n>] …`;
  - one key space;
  - every non-epic item has a `Parent / Epic`;
  - no hard deletes.
- **Branches.** Use `feat|fix|docs|chore/mesp-<n>-<slug>`: one bounded concern per branch and per PR.
- **Commits.** Use conventional prefixes (`feat`, `fix`, `docs`, `test`, `chore`, `refactor`). Their
  scope is the module or area.
- **Migrations.** They are additive and module-owned. Every EF context must report no pending model
  changes.
- **Live state never goes in static docs.** Record it in RESULT.md, ROADMAP.md or the tracker. The old
  date-stamped "current overlay" sections are retired.

## 4. Gates

Run these from the repository root. If `pwsh` is not installed, invoke the script from Windows
PowerShell.

| Gate | Command | Current baseline |
|---|---|---|
| Backend build + full suite (incl. disposable LocalDB SQL safety) | `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false` | 0 warnings / 0 errors; **1554/1554** passed |
| EF pending-model check | `dotnet ef migrations has-pending-model-changes` per context | none pending |
| Angular unit | `cd frontend; npm test -- --watch=false --no-progress` | 316/316 |
| Angular production build (also the type check) | `cd frontend; npm run build` | success. Known budget warning: 514.26 kB against 500 kB (MESP-155 (#270)) |
| Playwright Chromium | `cd frontend; npm run test:e2e -- --project=chromium` | 51 passed |
| npm audit | `npm audit --omit=dev --audit-level=high`; `npm audit --audit-level=high` | 0 high/critical (4 / 7 moderate) |
| Whitespace | `git diff --check` | clean |

**Hosted CI** (`.github/workflows/ci.yml`) has three required checks: `Repository Validation`,
`Backend` and `Frontend`. Hosted Backend CI **excludes** `SqlServerSafetyTests`. Any change that
touches SQL, money, stock or persistence also needs the local LocalDB gate. **After any governance-doc
change, run the backend suite**, because architecture tests read repository files.

## 5. Test hygiene and forbidden actions

- **Test hygiene**
  - Never delete, skip or weaken a valid test to get green.
  - Product defects become tracker Bugs; never "fix" them in test code.
  - No fixed sleeps. No hard-coded environment data or secrets.
  - DB validation is read-only unless the task authorizes more.
- **Forbidden without explicit owner instruction:**
  - `git reset --hard`, `git clean -fd`, force push, history rewrite;
  - bypassing branch policies or ruleset `22905800`;
  - deleting unmerged, unarchived branches;
  - pushing to archive remotes;
  - discarding owner work;
  - printing, committing or moving secrets, or touching `.env*`;
  - live, production, destructive or financial operations;
  - hard-deleting tracker items;
  - touching anything outside the repository root.
- **Stop and escalate** on a real blocker in any of these areas:
  - Tenant isolation;
  - authentication or authorization;
  - accounting or data integrity;
  - destructive migration or data loss;
  - an unresolved business decision;
  - legal or external validation;
  - credentials or production infrastructure;
  - material scope or architecture.

  Never invent business rules. Record open questions in MESP-23 (#112).

## 6. Tooling

- Use the plugins **Serena**, **Ponytail (full)** and **Context7** as
  [`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §5 describes.
- Ponytail's install, cache and hook state are machine-local. Never commit them.
- If a tool is missing, report it in one line and continue under these rules.


# AI ORCHESTRATION GOVERNANCE

This section governs multi-agent execution through Paseo.

All existing project-specific requirements, architecture rules, business rules,
acceptance rules, source-control rules, stop conditions, and owner decisions
elsewhere in this repository remain authoritative.

This section does NOT supersede project-specific authority.

If this orchestration governance conflicts with a project-specific rule,
the project-specific rule wins.

---

## 1. MODEL ROLES

### Planner — Claude Opus 5.5 / Medium

The Planner is the primary:

- planner;
- architect;
- requirements interpreter;
- project-state authority;
- task decomposer;
- model router;
- normal acceptance reviewer;
- orchestration controller.

The Planner normally does NOT implement product code.

The Planner may modify governance/state documents such as:

- TASK.md;
- RESULT.md when synchronizing accepted execution evidence;
- ORCHESTRATION_STATE.md;
- roadmap/decision/state documents when required by existing project rules.

The Planner must conserve Claude quota.

It must avoid broad repository exploration when the Executor can perform that
work more economically.

---

### Executor — GPT-6 Luna / Max

The Executor is the DEFAULT implementation agent.

Use Executor for:

- feature implementation;
- refactoring;
- test implementation;
- automation;
- repository-heavy discovery;
- build and CI work;
- straightforward recovery;
- easy bugs;
- normal bugs;
- medium-complexity bugs;
- localized regressions;
- known-root-cause defects;
- ordinary implementation corrections.

Executor should perform the majority of technical work.

Business severity alone does NOT justify escalation away from Executor.

---

### Hard Bug — Claude Sonnet 5 / High

Hard Bug is a protected specialist.

Use it only when concrete technical evidence establishes genuine difficulty,
including examples such as:

- difficult concurrency or race conditions;
- nondeterministic failures;
- deep lifecycle or state corruption;
- difficult cross-layer root causes;
- architecture-sensitive defects;
- severe data-integrity-sensitive recovery;
- technically complex production failures;
- competent Executor investigation has failed and another normal attempt is
  unlikely to be economical.

Do not use Hard Bug merely because a ticket is High or Critical severity.

If Executor made an ordinary implementation mistake, return a focused
correction to Executor instead.

---

### Independent Review — GPT-6 Sol / High

Independent Review is a protected project-wide reviewer.

It is reviewer-only by default.

Use it:

- normally after 12 accepted Executor cycles;
- earlier after approximately 8–10 cycles when material architectural risk,
  repeated failures, major scope changes, data-integrity work, or accumulated
  uncertainty justify earlier review;
- never allow more than 15 accepted Executor cycles without an independent
  review.

Independent Review must independently assess:

- current project plan;
- architecture and accumulated design decisions;
- implementation drift;
- test quality and coverage;
- repeated workaround patterns;
- hidden technical debt;
- unresolved assumptions;
- consistency between requirements, implementation and project authority;
- whether Planner decisions remain sound;
- whether model routing remains appropriate.

Independent Review does not automatically override Planner authority.

Its findings are returned to Planner for reconciliation.

---

# 2. NORMAL AUTONOMOUS LOOP

Unless an existing project-specific rule requires otherwise, use this loop:

Planner
→ TASK.md
→ Executor
→ RESULT.md + implementation evidence
→ Planner acceptance review
→ next TASK.md
→ Executor
→ repeat

The owner should not be required to manually transfer prompts or results
between Planner and Executor.

Planner should use Paseo orchestration to start the required child agents.

---

# 3. TASK.md AUTHORITY

TASK.md contains exactly ONE current executable contract.

Planner owns TASK.md.

Before dispatching Executor, Planner must ensure TASK.md contains:

- objective;
- verified current state relevant to the task;
- scope;
- constraints;
- execution requirements;
- validation requirements;
- stop conditions;
- expected result contract.

TASK.md must be sufficiently self-contained for Executor to act without routine
clarification from Planner.

Do not place unrelated future tasks into the active execution contract.

---

# 4. WORKTREE TASK TRANSFER

Executor should normally operate in an isolated Paseo Git worktree when
compatible with project-specific source-control rules.

Because an uncommitted TASK.md in the Planner workspace may not automatically
exist in a newly created worktree, Planner MUST pass the complete authoritative
TASK.md content to the Executor when creating the child agent.

Executor must treat that transferred TASK.md content as the authoritative task
for the cycle.

Do not require an unnecessary governance-only commit merely to transfer TASK.md.

---

# 5. EXECUTOR RESULT CONTRACT

Executor must complete implementation and validation before returning.

Executor must create or update RESULT.md in its execution workspace containing:

## RESULT
PASS / PARTIAL / BLOCKED

## TASK
Concise statement of the executed task.

## ROOT CAUSE
Required for bug/recovery work when applicable.

## CHANGES
Changed files and concise purpose.

## VALIDATION
Tests, builds, checks, or runtime validation performed and their results.

## DEFECTS OR GAPS
Only unresolved material gaps.

## REPOSITORY STATE
Branch/worktree state and material uncommitted changes.

## NEXT ACTION
Only when further action is genuinely required.

Executor must also return a concise completion notification to Planner.

Do not produce long implementation narratives.

---

# 6. PLANNER ACCEPTANCE REVIEW

Planner must not rediscover the entire project after every Executor cycle.

Normal review begins from:

1. TASK.md;
2. Executor completion report;
3. RESULT.md;
4. git diff;
5. changed files;
6. affected tests/build results;
7. project-specific acceptance rules.

Inspect additional repository content only when a concrete review concern
requires it.

Planner returns one result:

- ACCEPT;
- REJECT;
- BLOCKED.

---

# 7. REJECT / CORRECTION ROUTING

When Planner rejects Executor work, classify the reason.

If the gap is straightforward, such as:

- missed requirement;
- localized logic mistake;
- missing edge case;
- incorrect selector;
- incomplete test;
- compilation/build issue;
- configuration mistake;
- narrow regression;

route a focused correction back to Executor.

Normally allow one focused Executor correction when the problem remains
technically straightforward.

Do not escalate automatically after one imperfect implementation.

If evidence establishes a genuinely difficult technical problem, route the
bounded problem to Hard Bug.

---

# 8. HARD BUG RETURN PATH

Hard Bug must receive:

- the precise unresolved defect;
- relevant Executor findings;
- reproduction evidence;
- failed approaches when useful;
- applicable project constraints.

Hard Bug should not rediscover irrelevant project history.

After Hard Bug completes:

Hard Bug
→ Planner review
→ Executor for ordinary follow-up if necessary
→ next normal cycle

Planner remains the acceptance authority.

---

# 9. INDEPENDENT REVIEW COUNTER

A cycle counts only when an Executor implementation cycle is ACCEPTED by
Planner.

Rejected attempts and correction attempts do not independently increment the
accepted-cycle counter.

After every accepted Executor cycle:

increment:

accepted_luna_cycles_since_sol_review

Normal Independent Review trigger:

12

Early trigger:

8–10 when material project risk warrants it.

Hard maximum:

15

At 15, no further normal implementation cycle may start until Independent
Review has completed and Planner has reconciled its findings.

---

# 10. INDEPENDENT REVIEW OUTPUT

Independent Review must return:

## RESULT
PASS / CONCERNS / RECONCILIATION_REQUIRED

## REVIEW WINDOW
Accepted Executor cycles reviewed.

## PROJECT PLAN
Independent assessment.

## ARCHITECTURE
Independent findings.

## IMPLEMENTATION DRIFT
Independent findings.

## TEST / QUALITY
Independent findings.

## ROUTING / PROCESS
Independent findings.

## FINDINGS
For each material finding:

- severity: CRITICAL / MATERIAL / MINOR;
- evidence;
- affected area;
- impact;
- required action.

## CONTINUE
YES / NO

## NEXT REVIEW
Recommended accepted-cycle threshold.

Independent Review does NOT modify production code unless explicitly authorized
by the owner for a separate task.

---

# 11. SOL FINDING RECONCILIATION

Planner must evaluate Independent Review findings against current project
authority.

Do not blindly accept or dismiss them.

If no material correction is required:

- record the review;
- reset the independent-review counter;
- continue normal execution.

If correction is required:

- update project state/plan when authorized;
- create the required TASK.md;
- route normal corrections to Executor;
- route only genuinely difficult technical defects to Hard Bug.

---

# 12. OWNER INTERRUPTION POLICY

The autonomous loop should continue without owner intervention for normal
technical decisions.

Do NOT ask the owner questions that can be resolved through:

- repository inspection;
- existing project authority;
- architecture;
- tests;
- logs;
- Executor investigation;
- Planner reasoning;
- Hard Bug investigation.

Interrupt the owner only when a genuine owner/business decision is required,
including:

- materially ambiguous business behavior;
- two or more valid business outcomes with no existing authority selecting one;
- conflicting authoritative requirements;
- missing business acceptance criteria that cannot be inferred safely;
- destructive or irreversible operation requiring explicit authorization;
- production/external action requiring explicit authorization;
- unavailable credential/access requiring owner involvement;
- project-specific stop condition explicitly requiring owner approval.

When interruption is required, Planner must:

1. stop affected execution;
2. preserve current repository state;
3. state the exact unresolved decision;
4. provide the smallest necessary factual context;
5. ask one precise owner question.

Do not ask broad or exploratory questions.

---

# 13. AUTONOMY RULE

After the owner authorizes continuation, Planner should autonomously:

PLAN
→ DELEGATE
→ WAIT FOR COMPLETION
→ REVIEW
→ CORRECT OR ACCEPT
→ UPDATE PROJECT STATE
→ SELECT NEXT TASK
→ REPEAT

Do not require the owner to send routine continuation commands between cycles.

---

# 14. QUOTA GOVERNANCE

Claude quota is protected.

Planner must use Opus for judgment rather than mechanical repository work.

Use Executor for repository-heavy investigation whenever practical.

Hard Bug is protected and should remain exceptional.

Independent Review consumes Codex capacity and should be broad enough to add
independent value rather than duplicate Planner's narrow per-task review.

Fast/priority inference modes should remain disabled unless explicitly
authorized.

---

# 15. SESSION GOVERNANCE

Do not keep a Planner conversation indefinitely merely for convenience.

Durable project state belongs in repository authority files.

Planner should periodically begin a fresh session when context has grown
materially, after first updating:

- TASK.md;
- RESULT.md where applicable;
- ORCHESTRATION_STATE.md;
- existing project state/roadmap/decision files required by project-specific
  governance.

A fresh Planner session must reconstruct current authority from repository
state rather than relying on unavailable conversational history.

---

# 16. PRECEDENCE

Authority order:

1. explicit current owner instruction;
2. project-specific business and acceptance authority;
3. existing project architecture/decision/governance documents;
4. this orchestration governance;
5. model inference.

Never override higher authority using lower authority.