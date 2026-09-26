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
