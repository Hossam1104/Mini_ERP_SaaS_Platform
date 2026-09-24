# Executor Rules — PROPOSAL (ADOPTED 2026-09-25)

> **ADOPTED 2026-09-25 (Phase 2, MESP-149 (#264)).** The rules below are now in the root
> [`AGENTS.md`](../../AGENTS.md) and [`docs/MODEL_ROUTING.md`](../MODEL_ROUTING.md), with Opus 5.5 as
> the acceptance authority (Q2). This file is kept as the audit record. Where it differs from those
> files, they win. The status note below is the original proposal text.


> **Status: PROPOSAL.** `AGENTS.md`, `CLAUDE.md` and `.ai/AI_EXECUTION_POLICY.md` already exist and **remain authoritative**. Nothing here overwrites them.
>
> Layout constraint I checked: `AGENTS.md` **must stay at the root** because `MigrationFoundationTests.cs:1083` uses it to find the repo root. No test forbids or requires other root-level `.md` files. `CLAUDE.md` already exists and `@`-imports `AGENTS.md`.
>
> If the owner accepts, the text below is merged as a short section into the existing files. No new root file is needed.

## 1. Architecture in brief

- A .NET 10 modular monolith with four projects: Api → Infrastructure → App → Contracts. One shared SQL Server database, with one EF context and schema per module.
- Tenant isolation: server-owned Tenant context, global query filters, and raw SQL only at allowlisted sites (see `architecture-enforcement.md`).
- Frontend: Angular 22 (`frontend/src/app/{core,features,shared}`), Vitest unit tests, Playwright Chromium, bilingual EN/AR RTL.

## 2. Layer rules

1. Keep EF Core and provider code in `MiniErp.Infrastructure` only.
2. A module never touches another module's DbContext or tables. Cross-module work is coordinated in App through the owning module's service or contract.
3. Add no new App cross-module import edge unless an owner-approved task allows it (ratchet R2).
4. Add no new `IgnoreQueryFilters`, `FromSql*` or `ExecuteSql*` site. If one is unavoidable, stop and ask. Never widen the allowlist to get a green build.
5. Every public REST operation must satisfy the REST/API Definition of Done in `AGENTS.md`: catalogue, mapping, OpenAPI `operationId`, and a contract test.

## 3. Conventions

- Branch: `feat|fix|docs/mesp-<n>-<slug>`. Open one focused **Draft** PR per bounded task.
- Commit and PR references: `MESP-<n>` plus the GitHub issue `#<n>`. Azure DevOps is not used here.
- Any class in the LocalDB safety collection must have a name ending in `SqlServerSafetyTests`.
- `frontend/assets` is owner-managed and must never be touched.
- Put no secrets or connection strings in code or docs. Tests use only the disposable `MiniErpFoundation_*` target.

## 4. The real gates (run them; do not paraphrase them)

| Gate | Command (from repo root) | Expected on the 2026-09-24 baseline |
|---|---|---|
| Backend build and full tests, including LocalDB safety | `pwsh scripts/Test-MiniErpBackend.ps1 -NoBuild:$false` | 0 warnings / 0 errors; 1546/1546; about 6.5 min |
| Frontend unit tests | `cd frontend; npm test -- --watch=false --no-progress` | 316/316; about 75 s |
| Frontend build (acts as the type-check) | `cd frontend; npm run build` | success; known budget warning of 514.26 kB |
| Frontend audit | `cd frontend; npm audit --audit-level=high` | 0 high/critical |
| EF model drift (when a model changes) | `dotnet ef migrations has-pending-model-changes` for each touched context | none pending |
| Whitespace | `git diff --check origin/main...HEAD` | clean |
| Hosted CI | the checks on the PR: `Repository Validation`, `Backend`, `Frontend` | all SUCCESS |

A local run is not hosted CI. Report the two separately. Report any gate you did not run as **not run**. Never report it as passed.

## 5. Executor rules

1. Read `.ai/AI_EXECUTION_POLICY.md`, then the **CURRENT AUTHORITY** block of `.ai/CURRENT_STATE.md`, then `TASK.md`. Execute exactly that one task.
2. STOP means stop. Ready, merge, and GitHub Issue or Project lifecycle changes need positive authority in the current task.
3. Before creating any root-level file, check whether a test or governance rule constrains the doc layout.
4. Existing governance stays authoritative until the owner replaces it. Propose changes under `docs/audit/`; never overwrite governance files.
5. Tests contain no hard-coded environment data or secrets. Database validation is read-only unless the task says otherwise. No live, destructive, or financial actions without explicit authorisation.
6. Record your own mistakes in the results log, classified (for example `AUTOMATION_DEFECT (executor-introduced)`).
7. Before editing a shared function, grep all of its callers. Fix the root cause once, where every caller passes through.
