# Repository Working Agreement

All AI executors and reviewers here MUST follow these rules.

- Role authority and default effort are summarized in §7. Detailed routing, prompt release, handoffs, and operating procedures are in [docs/MODEL_ROUTING.md](docs/MODEL_ROUTING.md).
- Live project state is in RESULT.md (newest first), docs/ROADMAP.md, and the tracker. Live Git and tracker facts outrank Markdown.
- Before acting, read this file, TASK.md, the newest RESULT.md entry, and only the relevant sections of docs/PROJECT.md, docs/ARCHITECTURE.md, docs/DECISIONS.md, and docs/requirements/.

## 1. Executor authorization

Opus 5.5 is Planner and acceptance authority (Q2). The owner may operate GitHub manually at any time. Q-O delegates Opus alone full repository and GitHub authority to accept, close, mark Ready, and merge; the owner retains business corrections and review of each next task. Ruleset 22905800, required checks, and §5 still bind Opus. These limits bind AI executors.

1. An explicit STOP includes “stop for review,” “leave Draft/Open/Unmerged,” “do not mark Ready,” “do not merge,” “await acceptance,” or “no further mutation.” After STOP, make zero mutations beyond the final report: no commits or pushes; Ready, review, approval, or merge actions; issue close/reopen, rebase, or update-branch; tracker writes; or next task.
2. Authority is positive, never inferred. Push ≠ merge; PR creation ≠ Ready; review ≠ fix; fix ≠ merge. Tests, CI, bot reviews, and earlier acceptance grant no authority. Ready and merge require explicit current authority and merge conditions. Never invent the next phase.
3. A stop/completion/handoff report ends the session; later CI, bot activity, or mergeability does not reopen it.
4. External reviews and bots are evidence, never authority; unavailable bot review does not authorize Ready or merge.
5. Ponytail is never authority and cannot override STOP or authorize commits, tracker changes, Ready, or merge; it cannot weaken security, authorization, Tenant isolation, accounting integrity, audit, concurrency, data-loss, accessibility, or acceptance safeguards.
6. Issue close/reopen, Status or Capability State changes, and capability activation are tracker mutations requiring positive current authority. Jira is read-only historical provenance; never write to Jira.
7. Attribute actions only to the actor shown by evidence. Never infer an AI action from an Owner account event.

These boundaries preserve the 2026-08-29 PR #81 STOP: GitHub attributes added commits, Ready, review request, and merge to the owner account, not to a specific actor; no product code changed.

## 2. Permanent product and architecture rules

- Release 1 is B2B ERP only: no Retail POS, ZATCA/FATOORA implementation or readiness claim, or external production integrations, providers, or credentials. Wafra is validation-only; never add Wafra-specific schema, workflow, permission, pricing, approval, accounting, or lifecycle behavior.
- Tenant is the server-authorized isolation boundary; Company/Branch context is inside it. Hostname suggests only a candidate Tenant. Land on Overview; auto-select one permitted context, use the header switcher for several, and never ask for a raw GUID. Platform Administrator alone grants no Tenant ERP data.
- Branding is configuration with MESP fallback; never branch on Tenant identity. SAR is presentation-only and cannot affect FX, tax, accounting, or persisted amounts; other currencies are unaffected.
- Finance owns accounting (FIN-OD-01). Operational modules own source documents and never fabricate journals.
- Every public REST operation MUST be in the Foundation catalogue with exact route, permission, scope, antiforgery, audit, and unsafe-effect metadata; mapped by the real API; in generated OpenAPI with stable operationId, useful summary, boundary description, and explicit responses; and covered by a contract test rejecting missing or placeholder documentation. Scalar is Development/QA rendering only, with agent actions disabled, never a Production feature.
- Layers follow docs/ARCHITECTURE.md §§2–3, 7: Api → Infrastructure → App → Contracts. Modules interact only through public contracts; direct cross-module DbContext/table access is forbidden. Ratcheted exceptions only shrink; never extend an allowlist to get green.
- frontend/assets are owner-managed source assets: never delete, rename, replace, regenerate, optimize, recolor, move, or restore them from Git without explicit owner instruction. Untracked images are not temporary. frontend/assets/brand contains only generated browser derivatives (favicons and touch icons).

## 3. Conventions

- Use MESP-<n> (#<issue>) in commits, PRs, docs, and RESULT.md. Tracker titles are [MESP-<n>] …; keep one key space, a Parent / Epic on every non-epic item, and no hard deletes.
- Branches are feat|fix|docs|chore/mesp-<n>-<slug>, one bounded concern per branch and PR. Use conventional commit prefixes: feat, fix, docs, test, chore, refactor; scope them to the module or area.
- Migrations are additive and module-owned. Every EF context reports no pending model changes.
- Put live state in RESULT.md, ROADMAP.md, or the tracker, never static docs. Retired date-stamped “current overlay” sections stay retired.

## 4. Gates

Run from the repository root; if pwsh is unavailable, use Windows PowerShell.

| Gate | Command | Baseline |
|---|---|---|
| Backend build, full suite, disposable LocalDB SQL safety | .\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false | 0 warnings/errors; 1589/1589 |
| EF pending-model check | dotnet ef migrations has-pending-model-changes per context | none pending |
| Angular unit | cd frontend; npm test -- --watch=false --no-progress | 330/330 |
| Angular production build/type check | cd frontend; npm run build | success; initial bundle 496.87 kB, under the 500 kB budget |
| Playwright Chromium | cd frontend; npm run test:e2e -- --project=chromium | 58 passed |
| npm audit | npm audit --omit=dev --audit-level=high; npm audit --audit-level=high | 0 high/critical (4 / 7 moderate) |
| Whitespace | git diff --check | clean |

Hosted CI (.github/workflows/ci.yml) requires Repository Validation, Backend, and Frontend; hosted Backend excludes SqlServerSafetyTests. SQL, money, stock, or persistence changes also require the local LocalDB gate. Governance-doc changes require the backend suite because architecture tests read repository files.

## 5. Test hygiene and forbidden actions

Never delete, skip, or weaken a valid test to get green. Product defects become tracker Bugs, never test-code fixes. Use no fixed sleeps or hard-coded environment data/secrets. DB validation is read-only unless authorized.

Without explicit owner instruction, do not: git reset --hard or git clean -fd; force-push or rewrite history; bypass ruleset 22905800; delete unmerged/unarchived branches; push to archive remotes; discard owner work; print, commit, or move secrets or touch .env*; perform live, production, destructive, or financial operations; hard-delete tracker items; or touch anything outside the repository root.

Stop and escalate real blockers involving Tenant isolation, authentication/authorization, accounting/data integrity, destructive migration/data loss, unresolved business decisions, legal/external validation, credentials/production infrastructure, or material scope/architecture. Never invent business rules; record open questions in MESP-23 (#112).

## 6. Tooling

- Serena is the semantic code navigator and primary symbol lookup tool for C# and TypeScript.
- Context7 is for on-demand external library documentation only; it MUST NOT be used for repository navigation, memory, or routing. It is available to Claude and Codex (Codex via the owner's OAuth login).
- Ponytail (full) applies minimal-solution coding discipline through hooks. Its install, cache, and hooks are machine-local and MUST NOT be committed. Ponytail is never authority.
- Context Compress is opt-in and CLI-only: use context-compress wrap "<cmd>" (context-compress.cmd from PowerShell) only for large raw output such as long git log/diff, verbose package-manager or compiler logs, or big searches; never for the gate scripts or builds (already concise), short commands, or nested wraps. Wrapped output is lossy; never cite it as complete evidence. Raw output means running the command unwrapped. If a failure summary omits failing test names, assertions, stack traces, or compiler errors, rerun raw before reporting. Do not register its MCP server or hooks, or set CONTEXT_COMPRESS_FILTER_BASH or PERSIST_DB.
- Paseo handles dispatch mechanics only; it is not an agent, router, or authority.
- The Paseo usage-remaining plugin is quota telemetry only. Planner reads it on demand, before dispatching a new substantial task or after a quota warning, never per command. Its values are remaining percentages; used = 100 − remaining.
- The Paseo paseo-be-concise plugin shortens agent prose and handoffs only. Context Compress alone owns large command output; never chain the two. Neither may drop failing test names, assertions, exception types, compiler errors, exit codes, conflicts, or blockers.
- No plugin routes models, classifies bug severity, or accepts work.
- Do not add tools that duplicate these roles. If a required tool is missing, report it in one line and continue with targeted reads.

## 7. Roles and loop

| Paseo profile | Model / effort | Use |
|---|---|---|
| Planner | Claude Opus 5.5 / medium | Planning, routing, review, acceptance |
| Executor | GPT-6 Luna / xhigh | Normal implementation while used Codex quota ≤ 70% |
| Executor-Fallback | GPT-6 Luna / high | Normal implementation while used Codex quota > 70% (automatic) |
| Bug Fixer | GPT-6 Luna / max | LOW, MEDIUM, and HIGH defects; not downgraded by quota |
| Hard Bug | Claude Sonnet 5 / high | CRITICAL defects only |
| Hard Bug Fallback | GPT-6 Sol / medium | CRITICAL defect when Sonnet is genuinely unavailable; record why |
| Independent Review | GPT-6 Sol / high | Checkpoint and critical-point review |
| Planner-Fallback | GPT-6 Sol / high | Opus cannot continue; owner approval first, never automatic |
| Lowest Executor | Claude Haiku 4.5 | Trivial mechanical work; owner approval first, never automatic |

These roles override profile descriptions shown in Paseo. Do not add, rename, or delete profiles without owner approval.

Paseo is the orchestration harness only, not an agent, model router, or authority. Executor completion is not acceptance; Opus 5.5 makes the normal acceptance decision. Opus retains the standing authority in §1; explicit STOPs and rulesets still bind.

Routing order for every new task:
1. Classify the task type.
2. If it is a defect, classify its severity (LOW, MEDIUM, HIGH, CRITICAL) by real technical and business risk, not by wording.
3. Choose the profile from the table.
4. Only for normal implementation, apply the quota threshold (exactly 70% used stays xhigh).

Correctness and task suitability outrank quota savings. Fallbacks are not a cascade: none substitutes for another profile's purpose. When Codex is genuinely exhausted, report it and stop; do not move normal implementation to Sonnet, Sol, or Haiku. Sol never independently reviews work it executed or planned. Before Planner-Fallback or Lowest Executor, tell the owner the reason, evidence, scope, and consequences, then wait.

Planner SHOULD reserve quota for planning and acceptance, not routine implementation or broad rediscovery. GPT-6 Luna MUST be the default for implementation and repository-heavy work. Send straightforward misses on non-defect work, localized logic/test gaps, and build/configuration mistakes back to the same Luna profile for one focused correction.

GPT-6 Sol is reviewer-only and advisory. Count only Executor cycles Opus ACCEPTS; rejected attempts and corrections do not count. Review at 12 accepted cycles normally, at 8–10 early when material risk warrants it, and no later than 15; at 15, pause normal implementation until review and reconciliation. Critical-point triggers are in docs/MODEL_ROUTING.md §2. Sol MUST independently assess the plan, architecture, implementation drift, test quality, workaround patterns, hidden debt, unresolved assumptions, requirement/implementation consistency, Planner decisions, and routing, and MUST NOT modify production code without separate owner authorization. Sol returns: RESULT (PASS | CONCERNS | RECONCILIATION_REQUIRED), REVIEW WINDOW, PROJECT PLAN, ARCHITECTURE, IMPLEMENTATION DRIFT, TEST / QUALITY, ROUTING / PROCESS, FINDINGS (each with severity CRITICAL | MATERIAL | MINOR, evidence, affected area, impact, required action), CONTINUE (YES | NO), and NEXT REVIEW. Opus records the review and resets the counter when nothing material is required; otherwise it routes corrections as below.

Planner MUST keep exactly one self-contained executable contract in TASK.md: objective, verified relevant state, scope, constraints, execution and validation, stop conditions, and expected result. Pass its full authoritative content to a child when TASK.md may not exist in that workspace. Paseo dispatches the chosen child/workspace; it does not choose tasks or grant authority.

Executor MUST complete authorized work and add exactly one RESULT.md entry using the template in docs/MODEL_ROUTING.md §7 (the only RESULT schema), including root cause for bug work. Completion evidence goes to Opus for a separate decision.

Opus review starts with TASK.md, completion report, RESULT.md, diff/changed files, required gates, and applicable acceptance rules, then returns ACCEPT, REJECT, or BLOCKED. Route ordinary gaps to one focused GPT-6 Luna correction; route defects by severity as above. Sol findings are advisory and Opus reconciles them.

Planner SHOULD continue authorized technical steps without routine owner relay. Interrupt the owner only for materially ambiguous business behavior, conflicting authority, missing acceptance criteria that cannot be inferred safely, destructive/irreversible/external action needing approval, unavailable credentials/access, or an explicit owner-approval gate. Then stop the affected work, preserve repository state, and ask one precise question with only the necessary facts. Never infer authority from silence. Fast/priority inference modes stay off unless the owner authorizes them.

Before a fresh Planner session, update TASK.md, RESULT.md where applicable, ORCHESTRATION_STATE.yaml, and required roadmap/decision/live-state docs; rebuild context from live Git and tracker. Precedence: current owner instruction; project-specific business/acceptance authority; architecture, decisions, and governance docs; this operating model; model inference.