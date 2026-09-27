# MiniERP Local Development & Integrated Runtime Guide

Use the repository launcher for the normal Development flow. It selects a
local API port, generates an ignored Angular proxy for that exact URL, seeds
the Development account, waits for both applications, and prints the final
URLs. It does not stop unrelated services.

## Recommended one-command startup

From the repository root:

```powershell
dotnet build .\backend\MiniErp.sln --configuration Release
.\scripts\Start-MiniErpDevelopment.ps1 -Restart
```

The launcher starts the built Release executable so the process it records is
the real MiniERP API listener. Rebuild after source changes before restarting.

By default the launcher prompts for `MESP_DEV_ADMIN_PASSWORD` without
displaying or persisting it. The Development login remains
`admin@minierp.local`; the password is exactly the value supplied to
`MESP_DEV_ADMIN_PASSWORD` for the currently running backend process.

For a persistent local QA loop, explicitly enable the Development-only,
loopback-only auth shortcut once in the user environment. It authenticates
only the server-configured Development actor and never accepts a browser
supplied identity, password, Tenant, role, or permission:

```powershell
[Environment]::SetEnvironmentVariable('MESP_DEV_AUTH_BYPASS', 'true', 'User')
[Environment]::SetEnvironmentVariable('MESP_DEV_TENANT_DISPLAY_NAME', 'Wafra', 'User')
```

With that setting, the normal launcher does not prompt for a password and the
Angular guard establishes the session through the server-side Development
shortcut. Disable it when normal credential testing or any non-local
environment is required:

```powershell
[Environment]::SetEnvironmentVariable('MESP_DEV_AUTH_BYPASS', $null, 'User')
```

The committed/default setting remains disabled. The API also fails closed
outside the exact `Development` environment and for non-loopback callers.

The generic default API target is `http://localhost:5000`. If that port is
already occupied by another local service, the launcher leaves that process
alone and selects an available MiniERP Development port beginning at `5300`.
An explicit override is supported:

```powershell
.\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart
```

The `5300` example is a local override for a machine where `5000` is already
used; it is not a product or production port requirement. The current-machine
target is therefore normally:

- Backend: `http://localhost:5300`
- Frontend: `http://localhost:4300`

MESP-143 entry routing is host-aware in Development. Use the same Angular
port with loopback hostnames to exercise the three boundaries:

- `http://localhost:4300` — common MESP entry host;
- `http://tenant.localhost:4300` — generic configured Tenant host;
- `http://admin.localhost:4300` — separate platform-administration host.

The launcher adds the generic Development Tenant binding only when explicit
Tenant host bindings are absent. The generated proxy preserves the browser
`Host` header (`changeOrigin: false`), so the API—not Angular—resolves the
entry mode. Production hostnames, DNS, and TLS are infrastructure concerns and
are not configured by this launcher.

The launcher writes `.runtime\proxy.conf.json` (ignored by Git) with the
selected backend URL and starts Angular with that generated file. The tracked
`frontend\proxy.conf.json` remains the generic `http://localhost:5000`
fallback. Angular must be restarted after changing the proxy target; rerun the
launcher or restart Angular with the newly generated proxy.

## Configuration precedence

The selected backend target is resolved in this order:

1. `-ApiPort` or `-ApiUrl` on the launcher command;
2. `MESP_DEV_API_URL`;
3. `MESP_DEV_API_PORT`;
4. the generic default `http://localhost:5000`, with safe automatic fallback
   when that port is occupied.

For example, this uses a caller-provided target without editing tracked files:

```powershell
$env:MESP_DEV_API_URL = 'http://localhost:5300'
.\scripts\Start-MiniErpDevelopment.ps1 -FrontendPort 4300 -Restart
Remove-Item Env:MESP_DEV_API_URL
```

The generated proxy target and the backend `--urls` value are always the same.
Do not place `http://localhost:<port>` in Angular services or components.

The first interactive Price List create/append/resolve flow on a fresh
Development database may require legitimate reference records for Currency,
Product, Unit of Measure, and (when customer applicability is selected) a
Business Customer. The runtime does not seed fake business records; create or
load approved local reference data through the bounded Master Data/Business
Parties flows before testing those operations.

## Local SQL Server Development database

When `MESP_SQLSERVER_CONNECTION_STRING` is nonblank, normal exact-
`Development` startup uses SQL Server as the authoritative local provider. The
configured connection is read from the environment only; it is never printed,
committed, or written into generated runtime files. The expected local target
for this repository is server `.` and database `MESP`. If the variable is
absent, the existing module-owned SQLite fallback remains available for local
development and tests.

Development startup applies the formal EF migrations sequentially for the
six persistence contexts. Production startup does not auto-migrate:

| Context | Schema / owner | History table |
|---|---|---|
| `TenantPersistenceDbContext` | `tenancy` / Tenancy (`TenantOwnedRecords`) | `dbo.__EFMigrationsHistory_Tenancy` |
| `MasterDataDbContext` | `masterdata` / Master Data | `dbo.__EFMigrationsHistory_MasterData` |
| `BusinessPartiesDbContext` | `businessparties` / Business Parties | `dbo.__EFMigrationsHistory_BusinessParties` |
| `ProcurementDbContext` | `procurement` / Procurement and Phase-C quotation data | `dbo.__EFMigrationsHistory_Procurement` |
| `InventoryDbContext` | `inventory` / Inventory physical stock and ledger data | `dbo.__EFMigrationsHistory_Inventory` |
| `FinanceDbContext` | `finance` / Finance foundation and GL facts | `dbo.__EFMigrationsHistory_Finance` |

`tenancy.TenantOwnedRecords` is created and upgraded only by the Tenancy
context. The later module alignment migrations are intentionally no-op
database migrations whose snapshots record the shared runtime model without
competing for that physical table.

After a Release build, migration inspection or application uses the
Infrastructure project as both EF project and startup project, for example:

```powershell
dotnet ef migrations list --project .\backend\src\MiniErp.Infrastructure --startup-project .\backend\src\MiniErp.Infrastructure --configuration Release --no-build --context TenantPersistenceDbContext
dotnet ef database update --project .\backend\src\MiniErp.Infrastructure --startup-project .\backend\src\MiniErp.Infrastructure --configuration Release --no-build --context TenantPersistenceDbContext
```

The bounded local SQLite-to-SQL Server cutover utility is inventory-first and
refuses an apply/verify target other than the exact `MESP` database. With the
same environment variable, run it from the repository root:

```powershell
dotnet run --project .\backend\tools\MiniErp.DevelopmentDataCutover\MiniErp.DevelopmentDataCutover.csproj --configuration Release -- --inventory
dotnet run --project .\backend\tools\MiniErp.DevelopmentDataCutover\MiniErp.DevelopmentDataCutover.csproj --configuration Release -- --apply
dotnet run --project .\backend\tools\MiniErp.DevelopmentDataCutover\MiniErp.DevelopmentDataCutover.csproj --configuration Release -- --verify
```

The utility preserves source SQLite files, verifies source hashes and IDs,
checks Tenant/foreign-key lineage, and writes recoverable backups below
`%LOCALAPPDATA%\MiniErp\Development\backups\sqlserver-cutover-*`. This is a
local development data cutover, not a production migration, deployment,
backup/restore, HA/DR, residency, retention, or release-readiness approval.

The canonical disposable provider gate remains:

```powershell
.\scripts\validate-foundation.ps1
```

It creates and cleans its own `MiniErpFoundation_*` LocalDB database and is
independent of the owner-managed local `MESP` database.

For backend-only regression (including the SQL Server safety harness) without
the full Foundation suite, use the dedicated safe runner:

```powershell
.\scripts\Test-MiniErpBackend.ps1
```

## Finance runtime routes

Finance is served at `/app/finance`, with lazy routes `/app/finance/ap`,
`/app/finance/ar`, `/app/finance/settlements` and `/app/finance/tax-fx`.

## Purchase Order runtime

The current Procurement runtime includes the bounded Purchase Order and manual
Supplier Confirmation journey at these relative routes:

- `/app/procurement/purchase-orders` - Tenant/context-scoped list and status filter;
- `/app/procurement/purchase-orders/new` - eligible approved source-decision selection;
- `/app/procurement/purchase-orders/:id` - source lineage, approval, issue,
  confirmation, supplier-change, history, and audit detail;
- `/app/procurement/purchase-orders/:id/edit` - controlled Draft editing.

The server revalidates the approved Purchase Request, submitted Supplier
Quotation, current Source Decision, supplier, currency, organization scope, and
selected lines. It stores source/commercial snapshots and uses the existing
approval, antiforgery, ETag, idempotency, audit, and Tenant-ownership seams.
Supplier responses are manually recorded evidence only. Issue, confirmation,
partial remainder, rejection, and supplier-change reapproval do not create
Goods Receipt, stock, invoice, AP, payment, accounting, or three-way-match
effects.

When `MESP_SQLSERVER_CONNECTION_STRING` is configured, the formal Procurement
migrations (through `20260818103736_PurchaseOrderCommercialIntegrityAndDurableReplay`)
are applied by the Development migration sequence. Production startup still does not
auto-migrate, and disposable SQL safety evidence must use
`scripts/Test-MiniErpBackend.ps1`.

## SQL Server connection variable separation

Two environment variables serve distinct, non-interchangeable roles:

| Variable | Role | Target |
|---|---|---|
| `MESP_SQLSERVER_CONNECTION_STRING` | Persistent MiniERP application runtime | SQL Server `.` / database `MESP` |
| `MESP_SQLSERVER_SAFETY_CONNECTION_STRING` | Disposable SQL safety-test harness | `(localdb)\MSSQLLocalDB` / `MiniErpFoundation_*` |

**Do not conflate these variables.** The destructive SQL safety harness creates
and drops its own database. It must never target the persistent `MESP`
development database. The safety harness rejects any connection that does not
point at `(localdb)\MSSQLLocalDB` with a `MiniErpFoundation_[A-Za-z0-9_]+`
database name.

`scripts/Test-MiniErpBackend.ps1` and `scripts/validate-foundation.ps1`
construct the disposable connection in process memory, assign it only to
`MESP_SQLSERVER_SAFETY_CONNECTION_STRING`, and restore/clear it in a guaranteed
`finally` block. Neither script modifies `MESP_SQLSERVER_CONNECTION_STRING`.

## Manual two-process fallback

If the applications need to be started separately, generate the proxy first
and use the same target for both processes:

```powershell
.\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -ValidateOnly
```

Backend terminal:

```powershell
cd '.\backend'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Scalar__Enabled = 'true'
$env:MESP_DEV_BOOTSTRAP_ENABLED = 'true'
$env:MESP_DEV_AUTH_BYPASS = 'false'
$env:MESP_DEV_ADMIN_LOGIN = 'admin@minierp.local'
$env:MESP_DEV_ADMIN_PASSWORD = '<YOUR-LOCAL-PASSWORD>'
dotnet run --project '.\src\MiniErp.Api\MiniErp.Api.csproj' --configuration Release --no-build --urls 'http://localhost:5300'
```

Angular terminal:

```powershell
cd '.\frontend'
npm start -- --port 4300 --proxy-config '..\.runtime\proxy.conf.json'
```

The placeholder above is not a password. Set the exact password you will type
in the browser as `MESP_DEV_ADMIN_PASSWORD` before starting the backend. Never
commit or put that value in `RUN.md`, a tracked configuration file, or the tracker.

## Development authentication and stale cookies

The browser flow uses relative requests such as
`/api/v1/auth/sign-in`; Angular reaches the selected backend through the local
proxy. Use the frontend origin for the authoritative check:

When the explicit bypass is enabled, the first request is
`POST http://localhost:4300/api/v1/auth/development-bypass`; otherwise use
the normal credential request:

1. `POST http://localhost:4300/api/v1/auth/sign-in` (or the Development
   bypass above);
2. `GET http://localhost:4300/api/v1/auth/session`;
3. `GET http://localhost:4300/api/v1/auth/entry` for the server-resolved entry
   mode, Tenant identity, authorized choices, branding, SAR presentation, and
   operational-context state;
4. `GET http://localhost:4300/api/v1/auth/antiforgery` when a write is needed;
5. `POST http://localhost:4300/api/v1/auth/operational-context-switch` only
   when the Overview header presents multiple authorized Company/Branch
   contexts. The legacy `/auth/contexts` and `/auth/context-switch` routes
   remain compatibility surfaces for the bounded foundation journey.
Development HTTP intentionally uses `MiniErp.Auth` and
`MiniErp.AntiForgery`. If a prior local MiniERP run left incompatible
localhost state, remove only those MiniERP cookies (and any older
`__Host-MiniErp.Auth` or `__Host-MiniErp.AntiForgery` cookies) for the local
MiniERP origin, then sign in again. Do not clear unrelated localhost cookies
or RMS application state.

## Application and API endpoints

With the current-machine local override, use:

- **Frontend application**: `http://localhost:4300`
- **Backend API base**: `http://localhost:5300`
- **Health check**: `http://localhost:5300/health`
- **MiniERP module identity**: `http://localhost:5300/api/v1/module-registration`
- **OpenAPI v1 spec**: `http://localhost:5300/openapi/v1.json`
- **Scalar interactive API documentation**: `http://localhost:5300/scalar`

Replace `5300` with the selected target printed by the launcher. Production
deployment does not depend on this local Development proxy or port selection.

## Focused runtime configuration check

The no-process self-test verifies that the tracked generic target remains
`5000` and that a custom `MESP_DEV_API_URL` produces a generated proxy with
the same target:

```powershell
.\scripts\Test-MiniErpDevelopmentRuntime.ps1
```

<!--
** FOR HOSSAM ONLY **

Execute the prompt in TASK.md.
Review the latest RESULT.md entry.
-->
