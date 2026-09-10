# CI/CD Governance

This document records the repository's current validation boundary and the
future deployment progression. It is infrastructure governance only; a green
CI run is not a production-readiness, deployment, UAT, or compliance claim.

## Current state

The first repository-owned workflow is `/.github/workflows/ci.yml`.
Until a hosted run succeeds, the governance state is:

`CI = IMPLEMENTATION IN PROGRESS / NOT YET VERIFIED`

The bounded GitHub-native technical enabler is [Issue #235](https://github.com/Hossam1104/Mini_ERP_SaaS_Platform/issues/235).
The CI branch is `ci/github-actions-foundation`, based on the live
`origin/main` recorded when the worktree was created.

No branch protection or required checks are enabled by this work. After a
successful PR run and independent acceptance, a later governance action may
designate the stable checks below as required. A successful PR run may update
the state to `CI = GitHub Actions — PR VALIDATION VERIFIED`; `ACTIVE / VERIFIED`
requires successful validation after the workflow is merged to `main`.

## Workflow contract

Triggers are:

- pull requests targeting `main`;
- pushes to `main`;
- manual `workflow_dispatch` runs.

The workflow grants only `contents: read`. It uses concurrency cancellation
for superseded pull-request runs and does not cancel unrelated branch runs.
It requires no repository or environment secrets and has no production
credentials or deployment steps.

Stable future required-check names are:

- `Repository Validation` — tracked-file whitespace check using `git diff --check`;
- `Backend` — restore, Release build, and hosted-CI-compatible backend tests;
- `Frontend` — deterministic install, Angular unit tests, production build,
  Chromium Playwright tests, and both existing npm audits.

## Discovered toolchains and local equivalents

### Backend

- SDK: .NET `10.0.400`, pinned by `backend/global.json` with `net10.0` projects.
- Solution: `backend/MiniErp.sln`.
- Restore: `dotnet restore backend/MiniErp.sln`.
- Release build: `dotnet build backend/MiniErp.sln --configuration Release --no-restore`.
- Hosted-compatible tests: `dotnet test backend/MiniErp.sln --configuration Release --no-restore --no-build --filter "FullyQualifiedName!~SqlServerSafetyTests"`.
- Local complete safety run: `scripts/Test-MiniErpBackend.ps1 -NoBuild` after a
  Release build. This provisions a disposable `MiniErpFoundation_*` SQL Server
  LocalDB database through `MESP_SQLSERVER_SAFETY_CONNECTION_STRING` only.

The solution has one xUnit test project, `MiniErp.ArchitectureTests`, containing
backend unit, architecture, SQLite integration, REST/host-security, and
SQL Server safety coverage. The hosted job runs on `windows-latest` because
the existing architecture test contract compares Windows-style project paths.
It includes the deterministic non-LocalDB set. The SQL Server LocalDB safety
subset is intentionally `NOT YET CI-ENABLED`: hosted CI does not yet provision
the repository's disposable Windows LocalDB contract, and this task does not
introduce a database service, container, shared database, or production
connection.

### Frontend

- Framework: Angular `22.1.x`.
- Package manager: npm `12.0.1`, declared by `frontend/package.json`; the
  workflow installs that exact npm version after setting up Node.js.
- Node.js: no repository pin or `engines` contract exists; CI uses the
  locally verified Node.js `24.18.0` baseline explicitly until the repository
  adopts a formal Node version file/engines policy.
- Deterministic install: `npm ci --no-audit --fund=false` from `frontend/`.
- Unit tests: `npm test -- --watch=false --no-progress`.
- Production build: `npm run build`.
- E2E: `npm run test:e2e -- --project=chromium`, with the locked Playwright
  package installing Chromium through `npx playwright install --with-deps chromium`.
- Lint: no authoritative npm lint script exists.
- Separate type-check: no authoritative npm type-check script exists; Angular's
  production build performs the configured compilation checks.

The Playwright suite uses mocked browser/API fixtures and does not connect to a
backend, tenant database, production service, or external provider.

## Caching and artifacts

NuGet packages are cached using keys derived from the pinned SDK, central
package versions, and project files. npm's package cache is managed by
`setup-node` from `frontend/package-lock.json`. Build outputs are not cached.

Backend TRX test results and frontend Playwright test-results/reports are
uploaded for seven days when present, including on failed runs. No secrets,
configuration files, credentials, or production deployment packages are
uploaded.

## CI/CD progression

1. **CI Foundation** — this workflow validates repository changes.
2. **Required PR Quality Gates** — after hosted CI is accepted and stable,
   designate the stable checks as required for pull requests.
3. **Reproducible Release Build** — produce immutable, versioned artifacts
   under a separately authorized release workflow.
4. **Non-Production CD** — automate Development/Test deployment only after
   authoritative environment configuration, credentials, health checks, and
   rollback details exist.
5. **Staging Promotion** — add environment-specific configuration, migration
   validation/order, health verification, audit evidence, and recovery proof.
6. **Protected Production CD** — require a protected GitHub Environment,
   least-privilege credentials, explicit approval, immutable release identity,
   migration strategy, health verification, rollback/recovery, and audit
   evidence. Ordinary merges must not deploy automatically to Production.

Future database deployment must detect and validate migrations, establish
deployment order and backward-compatibility expectations, require backup or
recovery evidence where applicable, define failure handling and rollback or
forward-fix, and name the approval owner. This CI foundation does not execute
migrations against any database.

Container publishing, registries, hosting technology, application versioning,
OIDC credentials, and environment-specific deployment details remain deferred
until the accepted architecture and environment contracts authorize them.

## Known limitations

The current frontend lockfile produces moderate npm audit advisories in the
Angular, Vitest, and Hono dependency graph. The existing gate is the
repository-compatible `--audit-level=high` threshold, so those moderate
findings remain visible in CI logs but do not make this first infrastructure PR
change product dependencies. A future dependency-maintenance task should
review and remediate them; this CI task does not claim zero vulnerabilities.

The hosted backend job intentionally excludes the Windows SQL Server LocalDB
safety subset and the Windows validation-lock harness. A future CI-enablement
task must provide an ephemeral, non-production SQL Server strategy and prove
safe cleanup before adding those checks to required hosted CI.
