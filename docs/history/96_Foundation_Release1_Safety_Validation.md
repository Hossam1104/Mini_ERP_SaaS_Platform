# Foundation Release 1 Safety Validation

| Field | Value |
|---|---|
| Status | Foundation checkpoint; not a production-readiness approval |
| Jira | MESP-64 — Local and critical-flow test harness |
| Catalogue | Foundation Release 1 Lean Implementation Specification v0.4, section 48 |
| Environment | SQL Server 2025-compatible LocalDB instance MSSQLLocalDB; disposable MiniErpFoundation_* database per run; Windows integrated authentication |
| Evidence date | 4 August 2026 |
| Production/shared data | Not accessed |

## Outcome

The deterministic command `scripts/validate-foundation.ps1` started the installed
LocalDB instance, created one disposable database, ran the targeted SQL Server
suite and the full backend regression, and removed the database in fixture
cleanup. The targeted suite passed **11/11** with zero skips. The complete
backend suite passed **296/296**, with zero failures and zero skips. Release
build passed with zero warnings and zero errors.

The Foundation checkpoint has **53 executable foundation assertions PASS**,
**21 assertions NOT APPLICABLE** because their approved Tenant lifecycle,
export, numbering or later-domain workflow is not implemented in this bounded
checkpoint, and **1 production gate DEFERRED**. No assertion failed. Not
Applicable is a scope boundary, not evidence that the later workflow is safe;
the owning implementation must add executable evidence before authorization.

## MESP-91 correction overlay — merged and Done

MESP-91 Correction Package 1 was approved by focused ChatGPT security review
(APPROVED TO MERGE; 0 Critical, 0 High, 0 Medium blockers) and merged to
`main` through PR #20 at commit `f2cde57400fed470ab048776e05b56f353b36890`,
now the current merged-main foundation baseline. It adds the missing
durable-work authority boundary on top of the prior MESP-64 checkpoint:
Identity now exposes a narrow organization ownership
resolver that converts an untrusted `TenantWorkScopeRequest` into a
resolver-issued, authorization-context-bound `TenantWorkScope`. The resolver
validates the exact Tenant -> Company -> Branch -> Warehouse ownership chain
and downward authorized-scope containment; missing ownership and foreign or
sibling targets fail closed.

The worker and outbox dispatch paths now call a narrow live authority
revalidator immediately before a handler or protected outbox effect. It
rechecks the initiating Tenant and authorization path, current User/session,
ordinary Membership or SupportGrant/SupportCase, exact Permission and current
scope/ownership. A failed check produces a terminal `AuthorizationDenied`
dead-letter and safe evidence; no handler or outbox effect is reached. The
correction evidence is in `DurableWorkAuthorityRevalidationTests` and
`DurableWorkTests`. Ordinary revalidation requires the canonical explicit
selected scope and never falls back to a Tenant-wide membership grant; a
SupportGrant revalidation uses the current case-bound stored grant scope rather
than a context marker. Verified authority binds the exact work, Tenant,
operation, correlation, organization boundary, execution context, path,
Membership/SupportGrant, actor and session. The SQL probes remain persistence,
lease, transaction and idempotency evidence only; they are not
worker-authorization evidence.

The current correction validation passes the focused durable-work suite 102/102,
the complete backend suite 360/360, the targeted SQL Server suite 11/11, the
Angular suite 27/27 and four Playwright journeys. The Release build has zero
warnings and zero errors, and the production dependency audit reports zero
vulnerabilities; the `npm ci` development install reports three moderate
development-only advisories outside that production audit.

MESP-48 supported-volume/capacity and MESP-50 retention, privacy, legal-hold,
purge, residency, backup and restoration gates remain unchanged and are not
authorized by this correction.

## SQL Server evidence

- The Tenant query filter was evaluated repeatedly and concurrently for Tenant B after Tenant A model initialization; Tenant A data was not visible.
- Stored-owner verification denied forged Tenant A modification and deletion of a Tenant B record; the stored Tenant B row remained unchanged.
- The SQL Server unique index permits the same BusinessKey in different Tenants and rejects a duplicate within one Tenant without exposing provider detail.
- The `Version` property is a SQL Server `timestamp`/`rowversion`; stale update and stale delete both return the safe persistence conflict.
- The required unique `(TenantId, BusinessKey)` index was inspected from SQL Server catalog metadata.
- The disposable database's collation is queried live (`SqlServerSafetyTests.Sql_server_collation_is_recorded_and_unicode_identifier_round_trips`, `SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128));`), not assumed; the value observed on this validation run is recorded in the "Complete Foundation validation evidence" section below. An Arabic Unicode `BusinessKey` round-trips through storage and a plain equality read of the same session.
  **Proven:** the LocalDB database is created and queried under this recorded collation, and an Arabic-script value can be stored and read back unchanged. **Not proven by this evidence (MESP-94 M-3 correction):** Arabic linguistic sort order, `LIKE`/full-text search semantics, accent- or diacritic-aware matching, or any Saudi-production database collation decision — no test exercises `ORDER BY`, `LIKE`, or a collation-aware comparison against Arabic data, so no claim of Arabic search or sort correctness is made by this row.
- Test-only SQL probes proved transaction rollback removes work and outbox rows together, `(TenantId, EventId)` idempotency is Tenant-scoped, and a lease claim has one optimistic owner.
- The harness's unsafe-configuration rejection is proven against the real validator, not restated as a self-check: `SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString` (the exact method the fixture calls at startup) is unit-tested directly with a null/empty connection string, a non-LocalDB `Server`, an `InitialCatalog` outside the disposable prefix, the bare prefix with no suffix, and an `InitialCatalog` with characters outside the allowed pattern — each is proven to throw `InvalidOperationException` — alongside a positive control proving a safe configuration passes (MESP-94 M-13 correction; the prior test only compared the harness's own already-accepted connection string against two constants and would still pass if the real validator were deleted).

## Safety assertion matrix

The rows below reproduce the exact approved 75-assertion catalogue. A PASS row
names executable evidence already present in the repository. A NOT APPLICABLE
row identifies an approved scope boundary and owning implementation gate. The
DEFERRED row is reserved for the MESP-48/MESP-50 production decisions.

| # | Required assertion | Evidence | Status | Deferred owner or gate |
|---:|---|---|---|---|
| 1 | Cross-Tenant read is denied | `TenantPersistenceTests`, `SqlServerSafetyTests`, `RestFoundationTests`, `DurableWorkTests` | PASS | Foundation evidence in this checkpoint |
| 2 | Cross-Tenant write is denied | `TenantPersistenceTests`, `SqlServerSafetyTests`, `RestFoundationTests`, `DurableWorkTests` | PASS | Foundation evidence in this checkpoint |
| 3 | Cross-Tenant search/report/export/file access is denied | `TenantPersistenceTests`, `SqlServerSafetyTests`, `RestFoundationTests`, `DurableWorkTests` | PASS | Foundation evidence in this checkpoint |
| 4 | Client Tenant ID cannot expand authority | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 5 | Valid Tenant A working state is not visible in Tenant B | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 6 | Valid Tenant A working state is not automatically deleted by a context switch | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 7 | Returning to Tenant A re-evaluates the applicable path: Membership/Role/Permission/scope/session/lifecycle for ordinary access, or the complete SupportGrant path for exceptional support | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 8 | Separate concurrent Tenant contexts, caches and workspaces remain isolated | `HostSecurityTests`, `RestFoundationTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 9 | Revoked Membership is denied | `IdentityAuthorizationTests` session and membership revocation tests | PASS | Foundation evidence in this checkpoint |
| 10 | Role/scope revocation invalidates affected sessions | `IdentityAuthorizationTests` session and membership revocation tests | PASS | Foundation evidence in this checkpoint |
| 11 | Suspended Tenant denies ordinary interactive work | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 12 | Suspended Tenant denies ordinary asynchronous business work | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 13 | Draft, Provisioning and Configuration Required guards reject premature ordinary work | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 14 | Ready for Activation requires all approved activation prerequisites | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 15 | Activation is allowed only from the approved Ready for Activation state | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 16 | Grace Period applies its approved guard and does not invent a duration | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 17 | Reactivation re-evaluates all affected access and does not auto-restore it | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 18 | Export Requested is scoped, authorized and evidenced | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 19 | Termination Pending blocks ordinary work until approved guards pass | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 20 | Terminated revokes ordinary access while preserving evidence | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 21 | Retained state remains subject to MESP-50 and no purge executes | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 22 | Parent Tenant/unit suspension blocks descendants | No Tenant lifecycle workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 23 | Offboarded User is denied | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 24 | User suspension revokes affected sessions | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 25 | User reactivation does not automatically restore prior privileges | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 26 | Expired support grant is denied | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 27 | Support grant cannot reach another Tenant | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 28 | Support grant alone cannot export | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 29 | Privileged operation requires MFA and operation-bound fresh authentication | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 30 | Five-attempt lockout lasts 15 minutes | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 31 | Ordinary session absolute expiry cannot exceed 8 hours | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 32 | Cookie renewal cannot extend the original absolute maximum | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 33 | Inactivity at 30 minutes expires the session | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 34 | Every protected request validates server-side UserSession revocation | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 35 | Password reset invalidates affected sessions | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 36 | Missing or invalid antiforgery is denied | `IdentityAuthorizationTests`, `HostSecurityTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 37 | Inactive or closed units reject new work | No later ERP unit/historical-reference workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 38 | Authorized historical reference remains readable | No later ERP unit/historical-reference workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 39 | Used parent ownership cannot be rewritten | No later ERP unit/historical-reference workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle implementation gate; add executable evidence before that domain is authorized |
| 40 | Same-Tenant composite relationships enforce Branch -> Company, Warehouse -> Branch and Department -> Company | `TenantPersistenceTests.Same_tenant_relationship_is_accepted`/`Cross_tenant_relationship_is_rejected` and `SqlServerSafetyTests.Same_tenant_composite_relationship_is_allowed_on_sql_server`/`Same_tenant_relationship_is_allowed_and_cross_tenant_relationship_is_denied` cover all three named kinds (`CompanyBranch`, `BranchWarehouse`, `CompanyDepartment`) in both the accepted same-Tenant and denied cross-Tenant direction, in-memory and on SQL Server (closes MESP-94 H-3; the prior evidence exercised only one representative kind for acceptance) | PASS | Foundation evidence in this checkpoint; Branch/Warehouse/Department are the generic `TenantRelationshipKind` guard applied to the shared `TenantOwnedRecord` test fixture, not yet real Organization-domain entities — a later Organization/Company-Structure implementation must add its own equivalent relationship evidence for its actual entities |
| 41 | Tenant Membership, Role Assignment and Support Case/Grant cannot reference another Tenant | `TenantPersistenceTests`, `IdentityAuthorizationTests` | PASS | Foundation evidence in this checkpoint |
| 42 | Missing TenantId is rejected by the write pipeline | `TenantPersistenceTests`, `SqlServerSafetyTests` | PASS | Foundation evidence in this checkpoint |
| 43 | Mismatched TenantId versus trusted context is rejected | `TenantPersistenceTests`, `SqlServerSafetyTests` | PASS | Foundation evidence in this checkpoint |
| 44 | Restricted IgnoreQueryFilters/raw SQL/bulk/maintenance paths are unavailable to ordinary Tenant calls | `ModuleBoundaryTests`, `TenantPersistenceTests` | PASS | Foundation evidence in this checkpoint |
| 45 | Background/worker/outbox work revalidates exact initiating Tenant ownership, verified organization ownership and authorized scope, plus live User/session/Membership/SupportGrant/SupportCase/Permission lifecycle | `DurableWorkAuthorityRevalidationTests` (inactive user, suspended Membership, revoked/expired session, missing exact Permission, reduced current scope, expired/revoked SupportGrant, inactive SupportCase, foreign/sibling organization ownership) and `DurableWorkTests` | PASS | Foundation evidence in this checkpoint only, corrected by MESP-94 H-2: `DurableWorkLocalRuntime`, `InMemoryDurableWorkStore`, `DurableWorkDispatcher` and `TenantDurableWorkWorker` are **not referenced by `MiniErp.Api`** — this is an unwired local/in-memory Foundation seam proven by architecture and unit tests, not a production-composed capability reachable from the running host. SQL probes remain persistence/lease/transaction/idempotency evidence only |
| 46 | Denied cross-Tenant audit/telemetry records do not leak target data | `AuditObservabilityTests`, `DurableWorkTests`, `RestFoundationTests` | PASS | Foundation evidence in this checkpoint |
| 47 | Tenant-aware alternate/unique keys and architecture dependency direction remain valid | `ModuleBoundaryTests`, `SqlServerSafetyTests` schema/index evidence | PASS | Foundation evidence in this checkpoint |
| 48 | MESP-48/MESP-50 gates cannot be bypassed and no production purge is authorized | Scope gate recorded; no production threshold or purge test is authorized | DEFERRED | MESP-48 performance/volume and MESP-50 retention, residency, privacy, legal-hold, purge and provider gates |
| 49 | A Platform administrator using PlatformGovernanceContext without an applicable Tenant path is denied Tenant business data; Platform governance remains limited to purpose-bound control-plane records | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 50 | An ordinary Tenant operation requires an active Membership, applicable Role and Permission, explicit Access Scope, current session, and eligible lifecycle | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 51 | Support access requires a named active Support User/session, MFA and operation-bound fresh authentication, active case, Tenant-approved grant, exact Tenant/purpose/scope, applicable Permission, and no more than eight hours | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 52 | The SupportGrant path never creates or uses ordinary Membership or RoleAssignment, standing access, export authority, or ordinary Tenant business operations | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 53 | Identity owns Roles, RolePermissions, RoleAssignments, and AccessScopeGrants; Platform Governance owns the Permission catalogue/policy and system-Role approval/seed meaning | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 54 | A custom Role has a non-null Tenant owner, cannot be assigned to or have ownership changed to another Tenant, and a system Role remains Platform-owned and cannot itself grant Tenant business access | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 55 | A privileged RoleAssignment decision uses a separate named approver, rejects self-approval, and preserves approval/rejection evidence | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 56 | A RoleAssignment without a valid active explicit AccessScopeGrant grants no Tenant authority | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 57 | Scope grants flow only downward, never to parents or siblings, and multiple valid grants combine only within the same Tenant | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 58 | Revoking a RoleAssignment revokes all child grants and blocks further authority; scope changes are single-effect, concurrent-safe, and audited | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 59 | Tenant/User/Membership reactivation requires current revalidation and never automatically restores prior privileges or work | `IdentityAuthorizationTests`, `HostSecurityTests`, `AuditObservabilityTests` | PASS | Foundation evidence in this checkpoint |
| 60 | `Export Requested` stores an explicit operational access decision; `RecordTenantExportDisposition` accepts only ReturnToPriorEligibleState, Suspend, or BeginTermination and never implicitly grants export or executes purge | No Tenant export disposition workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate; MESP-50 for purge concepts |
| 61 | MESP-30 numbering is Company/Legal Entity plus Document Type; optional Branch subdivision requires later approved owning-domain/Saudi justification, Warehouse is excluded absent approval, and no automatic reset is assumed | MESP-30 numbering behavior is an approved later-domain implementation responsibility | NOT APPLICABLE | MESP-30 owning-domain implementation and later approved numbering validation |
| 62 | Numbers are never reused after cancellation, rejection, voiding, or reset; permitted gaps remain attributable and auditable | MESP-30 numbering behavior is an approved later-domain implementation responsibility | NOT APPLICABLE | MESP-30 owning-domain implementation and later approved numbering validation |
| 63 | Every command/query maps to exactly one public or internal operation and every operation maps back to exactly one command/query; no catalogue row is orphaned | `RestFoundationTests` operation/security-profile catalogue checks | PASS | Foundation evidence in this checkpoint |
| 64 | Every API operation has one named homogeneous security profile with explicit owner, actor, path, authentication, MFA/fresh-auth, context, Permission, scope, lifecycle, concurrency, idempotency, audit, safe errors, and response | `RestFoundationTests` operation/security-profile catalogue checks | PASS | Foundation evidence in this checkpoint |
| 65 | Role, assignment, grant, support-case, and support-grant TenantIds are integrity-consistent; cross-Tenant mutation or lookup is denied | `IdentityAuthorizationTests`, `TenantPersistenceTests`, `SqlServerSafetyTests` | PASS | Foundation evidence in this checkpoint |
| 66 | Support-grant approval/rejection evidence is distinct from revocation evidence, and rejection does not masquerade as revocation of active access | Revocation evidence is real and already proven (`IdentityAuthorizationTests.RevokedSupportGrantDeniesImmediately`/`RepeatedSupportGrantRevocationRemainsDenied`, `DurableWorkAuthorityRevalidationTests.Revoked_support_grant_prevents_dispatch`, `HostSecurityTests`, `PrivateFileAndNotificationSecurityTests.Revoked_support_grant_caller_is_denied`); only `AddSupportGrant` (approval-time creation) and `RevokeSupportGrant` exist in the codebase — there is no distinct SupportGrant **rejection** concept at all, so this row's comparative claim (rejection evidence distinct from, and not masquerading as, revocation) has nothing to evaluate against (corrected by MESP-94 M-10; the prior text implied no related evidence existed) | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate — a SupportGrant rejection concept and its distinct evidence remain later implementation scope; the existing revocation evidence above is unaffected by this gate; MESP-50 for purge concepts |
| 67 | Each lifecycle command has an approved predecessor/guard or an explicit rejection outcome; no transition creates a dead-end, and deferred `Purge Approved`/`Purged` concepts remain non-executable in Release 1 | Support rejection evidence and Tenant lifecycle state machine remain later implementation scope | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate; MESP-50 for purge concepts |
| 68 | Wafra-specific behavior remains validation-only and Retail POS remains outside the foundation specification and its test scope | `docs/15_Foundation_Release_1_Lean_Implementation_Specification.md`, ADR-018 scope control | PASS | Foundation evidence in this checkpoint |
| 69 | A Tenant Administrator can suspend a Membership in Tenant A while an independently authorized User remains eligible for Tenant B | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 70 | A Tenant Administrator cannot execute global `SuspendUser`, `ReactivateUser`, or `OffboardUser` | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 71 | A Platform Security Administrator or Platform Administrator with the specific global User lifecycle Permission can execute a global lifecycle action only with active authentication, MFA, fresh authentication, reason, immutable audit, concurrency and idempotency controls | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 72 | Global User suspension revokes all affected sessions across the User's Tenant contexts | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 73 | Global User reactivation never automatically restores Membership, Role, scope, SupportGrant, support authority or prior privilege | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 74 | A SupportGrant alone cannot execute global User lifecycle operations and cannot use a Tenant path to reach global User state | `IdentityAuthorizationTests`, `HostSecurityTests` | PASS | Foundation evidence in this checkpoint |
| 75 | Export disposition rejection or incompleteness leaves the Tenant in `Export Requested` with safe evidence and no implicit transition | No Tenant export disposition workflow is implemented in this foundation checkpoint | NOT APPLICABLE | Approved Tenant lifecycle/export implementation gate; MESP-50 for purge concepts |

## Traceability and limitations

The evidence chain is: approved PRD/BRDs → Foundation Release 1 Lean
Implementation Specification v0.4 → ADR-006/007/008/009/018 → MESP-61
contracts and MESP-64 harness → xUnit/Playwright/backend evidence → this
report. The SQL harness validates provider behavior on this developer machine;
it does not prove production sizing, throughput, HA, backup/restore, network
identity, residency, retention, legal hold, purge, or deployment equivalence.

MESP-48 remains the performance/volume/capacity gate. MESP-50 remains the
retention, privacy, residency, legal-hold, purge, backup/restoration and
provider-decision gate. No production migration, physical purge, retention
execution, database-per-Tenant design, later ERP domain, Retail POS or
Wafra-specific core behavior was added.

## Reproduction

From the repository root on the supported developer machine:

```powershell
.\scripts\validate-foundation.ps1
```

This is the single canonical Foundation validation command (MESP-94 M-14):
backend restore, backend Release build, the full backend regression (which
includes the SQL Server LocalDB probes and the safety-catalogue structural
validator, since both are part of the `MiniErp.ArchitectureTests` project run
by the solution-wide `dotnet test`), Angular unit tests, the Angular
production build, the Playwright Foundation journeys, `npm audit --omit=dev
--audit-level=high`, `git diff --check` against the working tree, and a
second `git diff --check origin/main...HEAD` against the live branch delta
from current `origin/main` (not a hard-coded SHA, so it actually covers
whatever the branch changed, not just uncommitted edits). Every step fails
the whole command closed on a non-zero exit code — none are swallowed.
`SqlLocalDB.exe` and `sqlcmd.exe` are discovered dynamically but boundedly
(PATH first, then a probe of only the known `<version>\Tools\Binn` and
`Client SDK\ODBC\<version>\Tools\Binn` layouts under Program Files — never a
full recursive scan of the much larger SQL Server database-engine tree); no
specific SQL Server release is hard-coded (MESP-94 M-15). The automatic
LocalDB instance is scoped by Windows user, not by logon session, so a
session-scoped `Local\` mutex would not coordinate two same-user processes
running in different sessions; the lock is instead a Global-namespace named
mutex whose name is suffixed with the current Windows user's SID and whose
access control list grants rights only to that SID (`FoundationValidationLock.ps1`).
This coordinates every validation process for the same Windows user across
sessions, without serializing unrelated Windows users and without letting a
different Windows user open or signal the lock at all (MESP-94 F1); it does
not prove that no other process on the machine can interfere with LocalDB by
any other means. The lock is held from before stale-database cleanup through
final cleanup, the orphan-database proof and environment-variable
restoration, so two validation runs for the same Windows user can never
remove each other's active disposable database; the command fails clearly if
another run already holds the lock rather than silently racing it. If the
prior owner terminated unexpectedly while holding the lock (crash, forced
kill, forced session end), `Wait-FoundationValidationLock` recovers ownership
from the resulting `AbandonedMutexException`, logs the interrupted-owner
condition, and continues into the same stale-database discovery/cleanup flow
below rather than treating it as an ordinary competing active run (MESP-94
F2). The command removes any
stale `MiniErpFoundation_*` database from a prior interrupted run before
creating its own disposable database, and proves zero `MiniErpFoundation_*`
databases remain on the LocalDB instance in a `finally` block that always
runs, even when an earlier step fails (MESP-94 M-6); it only ever targets a
database matching that exact disposable naming convention.
`MESP_SQLSERVER_CONNECTION_STRING` restoration runs in its own nested
`finally` so it is guaranteed even if the best-effort database removal or
the final orphan-database proof itself throws (MESP-94 R3). The command
fails closed when the validation lock cannot be acquired, LocalDB or
`sqlcmd` cannot be discovered, the connection is unset or unsafe, any step
fails, or the final orphan-database proof finds a remaining database.
Docker/Testcontainers CI execution remains deferred by ADR-018.

## MESP-91 focused correction validation overlay — merged and Done

The MESP-91 correction was validated on approved head
`92bd9fd38912a062cc3723f46867258d54ca8127` (source/test commit
`4ed4b0588b613d492ce6c446ae963001b28f0eca`) from the correction branch,
against merged-main baseline `4eb1ef3ab094242cbb26ec9ab79b4037512e0d2d`. PR #20
was approved by focused ChatGPT security review and merged to `main` by normal
merge commit at `f2cde57400fed470ab048776e05b56f353b36890`; this overlay is now
the current merged-main capability and does not change the maturity or
production-provider claims above. The same validation totals below were
re-verified against merged `main` after the merge.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work/authority regression | 102 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 360 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test -- --watch=false --no-progress` |
| Angular production build | Passed | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Structural issuer and mandatory-evidence architecture checks | Passed in focused suite | `DurableWorkTests` syntax-tree allow-list and descriptor policy tests |
| Safety catalogue | 53 PASS, 21 NOT APPLICABLE, 1 DEFERRED, 0 failed | Existing 75-assertion catalogue; no scope claim changed |
| Diff check | Passed | `git diff --check` |

The LocalDB validation used the approved `MSSQLLocalDB` instance and Windows
integrated authentication only. The actual LocalDB/model collation observed
for the disposable run was `SQL_Latin1_General_CP1_CI_AS`; no case-sensitivity
policy was inferred from that value. The test database name used the
`MiniErpFoundation_` prefix and teardown was verified with a server query:
zero `MiniErpFoundation_*` databases remained. No production or shared
database was accessed.

The `npm ci` development install reported three moderate development-tree
advisories and four blocked optional install scripts; these do not affect the
production audit result and were not force-fixed as part of this focused
correction. No migration, provider selection, production worker, notification
provider, object-storage provider or purge behavior was introduced.

## MESP-92 single-effect/immutable-payload validation overlay — In Progress

MESP-92 re-ran the same disposable-LocalDB Foundation validation from branch
`fix/MESP-92-single-effect-immutable-payloads`, based on merged-main baseline
`32a91f27bc162685fc0db0f38b031d02ffbc99d2`. It introduces no new SQL Server
schema, index, transaction or lease probe; the existing 75-assertion safety
catalogue and its 53 PASS / 21 NOT APPLICABLE / 1 DEFERRED disposition are
unchanged. The correction's own evidence is new focused DurableWork unit
coverage for payload immutability, effect single-execution semantics and
genuine concurrency, validated alongside the unchanged SQL probes:

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + new) | 136 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 394 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test -- --watch=false` |
| Angular production build | Passed | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npx playwright test` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Architecture enforcement (effect-guard bypass check) | Passed | `Handler_invocation_is_reachable_only_through_the_effect_executor` |
| Safety catalogue | 53 PASS, 21 NOT APPLICABLE, 1 DEFERRED, 0 failed | Existing 75-assertion catalogue; no scope claim changed |
| Diff check | Passed | `git diff --check` |

No `MiniErpFoundation_*` database remained after teardown. MESP-92 is **not**
marked Done by this validation overlay; the PR is opened non-draft and held
unmerged pending focused ChatGPT security review.

## MESP-92 focused ChatGPT re-review corrections — In Progress

A focused ChatGPT security review of PR #22 raised four further findings —
H92-01 (effect-purpose/EventId keying), H92-02 (explicit protected-effect
outcome contract), M92-01 (uncertain reconciliation state) and M92-02
(payload mutation-hook removal) — corrected on the same branch. This overlay
is backend-only: no Angular, Playwright or `npm` evidence changed, so the
frontend rows above remain the prior overlay's evidence and were not re-run
for this correction. Updated backend evidence:

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + all corrections) | 159 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 417 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Architecture enforcement (effect-guard bypass + no bare generic retry) | Passed | `Handler_invocation_is_reachable_only_through_the_effect_executor`, `Protected_effect_boundary_cannot_return_a_bare_generic_retry` |
| Diff check | Passed | `git diff --check` |

No `MiniErpFoundation_*` database remained after teardown. No PRD file
appears in the branch diff and the unrelated `local-prd-rename-before-MESP-92`
stash remains untouched. MESP-92 is **not** marked Done by this overlay; PR
#22 remains open, non-draft and held unmerged pending a focused ChatGPT
re-review.

## MESP-92 second focused ChatGPT re-review corrections — In Progress

A second focused ChatGPT security review of PR #22 raised five further
findings — H92-03 (structurally enforced single effect ledger via
`DurableWorkLocalRuntime`), H92-04 (exact-scope reconciliation-read
authorization via `VerifiedDurableWorkReconciliationAuthorization`), M92-03
(exact uncertain-effect identity via `DurableWorkEffectKey`/`TenantWorkScope`),
M92-04 (custom codec exception normalization) and L92-01 (crash terminology
correction) — corrected on the same branch. This round re-ran the complete
backend and frontend validation pipeline:

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + all corrections) | 199 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 457 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test -- --watch=false` |
| Angular production build | Passed | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Architecture enforcement (single-ledger composition allow-list) | Passed | `Shipping_construction_of_the_ledger_types_occurs_only_inside_the_approved_composition` |
| Diff check | Passed | `git diff --check` |

New focused evidence lives in `DurableWorkLocalRuntimeCompositionTests`
(H92-03: structural construction restriction, one-shared-executor and
one-ledger-per-composition-call proof) and
`DurableWorkReconciliationAuthorizationTests` (H92-04 exact-scope
authorization matrix across Tenant/Company/Branch/Warehouse siblings, missing
permission, expired session, suspended Membership, expired/revoked
SupportGrant and missing selected scope; M92-03 exact identity, safe-reason
preservation and immutability). M92-04 evidence extends
`DurableWorkPayloadAndEffectTests`. No new SQL Server schema, index,
transaction or lease probe was introduced; the existing 75-assertion safety
catalogue disposition is unchanged.

No `MiniErpFoundation_*` database remained after teardown. At the time of this
overlay no PRD file appeared in the branch diff and the unrelated
`local-prd-rename-before-MESP-92` stash was untouched; both statements were
superseded on 6 August 2026 — see the Opus 5 overlay below. MESP-92 is **not**
marked Done by this overlay; PR #22 remains open, non-draft and held unmerged
pending a further focused ChatGPT re-review.

## Opus 5 project-wide validation rerun — 6 August 2026

The Opus 5 project-wide checkpoint re-ran the complete validation once against
branch head `271e9dfedce8e0ea44ef9f8d3ab6e6b61d984ac4`. No source code, test
code or test data was changed to obtain these results, and no hosted CI exists
— every figure below is local.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Backend Release build | 0 warnings, 0 errors | `dotnet build .\backend\MiniErp.sln --configuration Release --no-restore` |
| Complete backend suite | 457 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database `MiniErpFoundation_20260806235236_7add0e49` |
| Disposable database cleanup | No `MiniErp%` database remained | `SELECT name FROM sys.databases WHERE name LIKE 'MiniErp%'` returned no rows |
| Angular suite | 27 passed, 0 failed, 0 skipped, across 5 test files | `npm test -- --watch=false` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Diff check | Passed, no whitespace errors | `git diff --check` |
| Hosted CI | Not available | No hosted workflow is configured in this repository |

This rerun covers the **complete frontend regression** (unit tests, production
build, Playwright journeys and production dependency audit), closing the
earlier gap where the frontend regression had not been rerun after the second
MESP-92 correction.

**PRD status correction.** The approved PRD is now tracked at
`docs/MESP_PRD_v1.2.docx`. The move from `MiniERPSaaSPlatform_PRD_v1.2.docx`
was committed by the repository owner as `271e9dfedce8e0ea44ef9f8d3ab6e6b61d984ac4`
on this branch, so a PRD path change **does** now appear in the branch diff —
recorded by Git as a pure `R100` rename. The binary is unmodified: the original
`docs/MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx`, the
root-level `MiniERPSaaSPlatform_PRD_v1.2.docx` on `origin/main`, and the current
`docs/MESP_PRD_v1.2.docx` all resolve to the identical Git blob
`1f9163b9412cb343a19a98312eb642ad26c1efaa`. The local
`local-prd-rename-before-MESP-92` stash still exists and was not applied,
dropped or otherwise altered.

**Safety-catalogue disposition.** Unchanged. No new SQL Server schema, index,
rowversion, collation, Tenant-filter, stored-owner, relationship, transaction,
idempotency or lease probe was introduced by this review, and the exact
75-assertion catalogue is untouched. The SQL Server evidence remains a
disposable-LocalDB probe and is not a production provider selection.

## O92-01/O92-02 focused correction — 7 August 2026

A bounded correction closed the two non-blocking Low findings the Opus 5
project-wide checkpoint recorded above at head `271e9df`: O92-01 (the effect
guard discarded its uncertain-effect safe reason) and O92-02 (the
`NextAttemptAt` fallback that contradicted the documented occurrence-time
invariant). Both are closed at head `9dc6cb82860b10215d05364f2f6e25f69df3b986`
on the same branch. No SQL Server schema, index, rowversion, collation,
Tenant-filter, stored-owner, relationship, transaction, idempotency or lease
probe changed; the existing 75-assertion safety catalogue is untouched.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + all corrections) | 216 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 474 passed, 0 failed, 0 skipped | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test -- --watch=false` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Diff check | Passed, no whitespace errors | `git diff --check` |
| Hosted CI | Not available | No hosted workflow is configured in this repository |

No `MiniErpFoundation_*` database remained in `MSSQLLocalDB` after teardown.
New focused evidence for O92-01 lives in `DurableWorkPayloadAndEffectTests`
(guard-level reason preservation, duplicate/different-reason overwrite
rejection, handler/outbox and cross-EventId reason isolation, unsafe/unbounded
reason rejection); O92-02 evidence in the same file covers exact handler and
outbox `OutcomeUnknownAt` transition timestamps, the `NextAttemptAt`
non-substitution proof, and reflection-based corrupted-record fail-closed
tests (no production member was made public solely for testing). MESP-92 is
**not** marked Done by this correction; PR #22 remains open, non-draft and
held unmerged pending a focused ChatGPT security re-review at this head.

## H92-05/M92-05 focused correction — 7 August 2026

A further focused ChatGPT security re-review of PR #22 at head
`9dc6cb82860b10215d05364f2f6e25f69df3b986` raised H92-05 (High —
`DurableWorkLocalRuntime` publicly exposed the mutable effect guard) and
M92-05 (Medium — `GetOutcomeUnknownReason` was reachable from a raw effect
key, bypassing the H92-04 authorized reconciliation port). Both are closed at
head `576996f94ae9ddc251767445a7ebddd60c492c45` on the same branch. No SQL
Server schema, index, rowversion, collation, Tenant-filter, stored-owner,
relationship, transaction, idempotency or lease probe changed; the existing
75-assertion safety catalogue is untouched.

**Public-surface evidence.** `DurableWorkLocalRuntime`'s public instance
members are now exactly `Store` and `Dispatcher` (verified by reflection in
`DurableWorkEffectLedgerSurfaceTests.Runtime_public_instance_surface_is_limited_to_Store_and_Dispatcher`).
`IDurableWorkEffectGuard`, `InMemoryDurableWorkEffectGuard`,
`IDurableWorkEffectExecutor` and `DurableWorkEffectExecutor` are confirmed
non-public types by reflection (`Type.IsPublic` false for each). A
whole-assembly reflection scan
(`No_public_type_in_the_App_assembly_exposes_an_internal_ledger_type`) proves
no public property or method anywhere in `MiniErp.App` returns, accepts or is
typed as one of these four types. A second whole-assembly scan
(`No_public_method_in_the_App_assembly_resolves_uncertain_effect_evidence_from_a_raw_key_alone`)
proves no public method accepts only a `DurableWorkEffectKey` and returns a
`string` or enum (reason/state evidence); a third
(`GetOutcomeUnknownReason_is_not_declared_by_any_public_type`) proves no
public type declares that method at all.

**Architecture-test evidence.** `DurableWorkEffectLedgerSurfaceTests.cs` adds
14 tests: the public-surface checks above; `Store_and_dispatcher_still_share_the_identical_internal_guard`
proving both still route through the same internal guard/executor instance
(unchanged from H92-03); `ReadUncertainEffectsAsync_still_requires_verified_reconciliation_authorization_and_has_one_overload`
proving the H92-04 port was not weakened or overloaded; `Internal_reason_is_preserved_and_inspectable_only_through_the_internal_test_only_seam`
proving O92-01's preserved reason is still readable through
`InternalsVisibleTo`; and an executable attack-regression test
(`Shipping_caller_cannot_reach_the_guard_to_release_an_in_flight_reservation_and_effect_still_runs_once`)
that blocks a handler mid-effect, proves no publicly reachable member yields
the guard during that window, then completes the handler and issues a
duplicate dispatch to confirm the protected effect executed exactly once.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work regression (baseline + all corrections) | 230 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~DurableWork` |
| Complete backend suite | 488 passed, 0 failed, 0 skipped | `powershell -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npx ng test --watch=false` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred | `npx ng build --configuration production` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npx playwright test` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Diff check | Passed, no whitespace errors | `git diff --check` |
| Hosted CI | Not available | No hosted workflow is configured in this repository |

No `MiniErpFoundation_*` database remained in `MSSQLLocalDB` after teardown.
O92-01 and O92-02 remain closed; all previously added O92-01/O92-02 tests
continue to pass unmodified. MESP-92 is **not** marked Done by this
correction; PR #22 remains open, non-draft and held unmerged pending a
further focused ChatGPT security re-review at this head.

## H92-06/M92-07/L92-02 focused correction — 7 August 2026

A follow-up shipping-boundary correction found that the H92-05/M92-05
`internal` closure above, while accurate about source-level visibility, did
not close the compiled shipping boundary: `MiniErp.App` still declared
`[assembly: InternalsVisibleTo("MiniErp.Api")]`. A friend assembly sees
another assembly's `internal` members exactly as if they were public, so
`MiniErp.Api` could still reach `EffectGuard`/`EffectExecutor`, construct
`InMemoryDurableWorkEffectGuard`/`DurableWorkEffectExecutor` directly, and
call `TryReserve`/`Release`/`RecordCompleted`/`RecordOutcomeUnknown`/
`GetOutcomeUnknownReason`. This is H92-06 (High); M92-07 (Medium) is the same
root cause applied to the M92-05 raw-key reason bypass specifically. Both are
closed at head `e991641` on the same branch. No SQL Server schema, index,
rowversion, collation, Tenant-filter, stored-owner, relationship, transaction,
idempotency or lease probe changed; the existing 75-assertion safety
catalogue is untouched.

**Friend-assembly evidence.** `backend/src/MiniErp.App/Properties/AssemblyInfo.cs`
now declares `InternalsVisibleTo` only for `MiniErp.ArchitectureTests`; the
grant to `MiniErp.Api` is removed. The one resulting `MiniErp.Api` compile
break was `FoundationHostSignInResult.Principal` (needed by the sign-in
endpoint to call `HttpContext.SignInAsync`), unrelated to durable work; it is
now a narrow public property rather than a restored friend grant. No mutable
ledger type is public.

**Architecture-test evidence.** `FriendAssemblyPolicyTests.cs` adds 5 tests:
reflection over `InternalsVisibleToAttribute` proves `MiniErp.App`'s
friend-assembly allow-list is exactly `["MiniErp.ArchitectureTests"]` and
contains no non-test assembly; a full Roslyn in-memory compilation
(`Source_compiled_as_the_shipping_Api_assembly_cannot_access_the_internal_effect_guard_or_executor_types`)
proves source compiled under the assembly name `MiniErp.Api` fails with
`CS0122` when constructing the guard/executor or calling their
reserve/release/record/read-reason members; a matching positive control
proves the identical source compiled under `MiniErp.ArchitectureTests` still
succeeds. Both the reflection and compilation tests were run against the
prior (vulnerable) `InternalsVisibleTo("MiniErp.Api")` state and confirmed to
fail there before being confirmed to pass against this correction -- genuine
regression proof, not a restatement of the fix.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused durable-work/ledger/composition/reconciliation regression | 238 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter "FullyQualifiedName~DurableWork\|FullyQualifiedName~FriendAssemblyPolicy\|FullyQualifiedName~Reconciliation\|FullyQualifiedName~LedgerSurface\|FullyQualifiedName~Composition\|FullyQualifiedName~Payload"` |
| Complete backend suite | 493 passed, 0 failed, 0 skipped | `powershell -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npx ng test --watch=false` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred (unchanged) | `npx ng build --configuration production` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npx playwright test` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Diff check | Passed, no whitespace errors | `git diff --check` |
| Hosted CI | Not available | No hosted workflow is configured in this repository |

No `MiniErpFoundation_*` database remained in `MSSQLLocalDB` after teardown.
`frontend/angular.json` is byte-for-byte identical to `origin/main` (L92-02
scope cleanup, not a security finding). O92-01, O92-02, H92-05 and M92-05
remain closed; all previously added tests for those findings continue to pass
unmodified. MESP-92 is **not** marked Done by this correction; PR #22 remains
open, non-draft and held unmerged pending a further focused ChatGPT security
re-review at this head.

## MESP-92 closure overlay — Done (7 August 2026, current, not a rewrite of the overlays above)

Every overlay above this line is the preserved historical validation record of
the MESP-92 correction sequence and is not rewritten. The further focused
ChatGPT security re-review requested by the last overlay completed with
verdict APPROVED FOR MERGE at reviewed head `3ec6b45bc108d1388035caa8c331866a2c72d043`.
PR #22 merged to `main` at `322341e70e56270797d5770b4b90342c20b7833e`.
Post-merge validation on `main` reproduced the identical totals recorded in
the last overlay: Release build 0 warnings/0 errors; full backend regression
**493/493** passed (0 failed, 0 skipped) including **11/11** SQL Server
LocalDB probes with no `MiniErpFoundation_*` database remaining after
teardown; Angular unit tests **27/27** passed; Angular production build
succeeded (351.02 kB initial / 87.80 kB transferred, unchanged); Playwright
**4/4** passed; `npm audit --omit=dev --audit-level=high` reported **0**
vulnerabilities; `git diff --check` clean. MESP-92 is now marked **Done** in
Jira. MESP-93 (private-file access and notification hardening) is the next
active Foundation correction — see `.ai/CURRENT_STATE.md` for its exact
branch, head and Pull Request.

## MESP-93 implementation validation (7 August 2026, historical)

MESP-93 closes M-1, M-4, M-5, M-7, M-8, M-9 and L-4 on branch
`fix/MESP-93-private-files-notifications`, based on `main` at `322341e`. 45
new focused tests were added in `PrivateFileAndNotificationSecurityTests.cs`.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused MESP-93 suite | 45 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~PrivateFileAndNotificationSecurityTests` |
| Complete backend suite | 538 passed, 0 failed, 0 skipped | `powershell -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped (unchanged; no frontend files touched) | `npx ng test` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred (unchanged) | `npx ng build --configuration production` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npx playwright test` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Diff check | Passed, no whitespace errors | `git diff --check` |

No `MiniErpFoundation_*` database remained after teardown. No production
object storage, public URL, signed download, malware scanner, production
notification provider or physical purge was introduced. MESP-93 is **not**
marked Done by this validation; its Pull Request is held open, non-draft and
unmerged pending a focused ChatGPT security review, the same standing gate
MESP-92 carried.

## MESP-93 focused re-review correction validation (7 August 2026, historical)

A focused re-review of PR #24 at head `759eb04` raised H93-01, H93-02,
M93-01, M93-02 and L93-01; all five are closed at head `1820416`. 28 new
focused tests were added (73 total).

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Focused MESP-93 suite | 73 passed, 0 failed, 0 skipped | `dotnet test backend/tests/MiniErp.ArchitectureTests/MiniErp.ArchitectureTests.csproj --filter FullyQualifiedName~PrivateFileAndNotificationSecurityTests` |
| Complete backend suite | 566 passed, 0 failed, 0 skipped | `powershell -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped (unchanged) | `npx ng test` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred (unchanged) | `npx ng build --configuration production` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npx playwright test` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Diff check | Passed, no whitespace errors | `git diff --check` |

No `MiniErpFoundation_*` database remained after teardown. At the time this
validation was recorded, MESP-93 remained not marked Done and PR #24 was
held open, non-draft and unmerged pending a further focused ChatGPT security
re-review at head `1820416`. **Superseded — see the closure validation
below.**

## MESP-93 closure validation (7 August 2026, current)

The further focused ChatGPT security re-review requested above completed
with verdict **APPROVED FOR MERGE** at reviewed head
`83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`. PR #24 merged to `main` at
`005c796629341ab9becfbc6d1abe2ae34b6a7332`. Post-merge validation was rerun
on `main` (not copied from the pre-merge totals above):

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Complete backend suite | 566 passed, 0 failed, 0 skipped | `powershell -File .\scripts\validate-foundation.ps1` |
| SQL Server LocalDB suite | 11 passed, 0 failed, 0 skipped | Same validation command; disposable `MSSQLLocalDB` database |
| Backend Release build | 0 warnings, 0 errors | Same validation command |
| Angular suite | 27 passed, 0 failed, 0 skipped | `npm test` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred (unchanged) | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npx playwright test` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Diff check | Passed, no whitespace errors | `git diff --check` |

No `MiniErpFoundation_*` database remained after teardown (verified directly
via `sys.databases` on the `MSSQLLocalDB` instance, in addition to the
script's own teardown). MESP-93 is marked **Done** in Jira. MESP-94 is the
next eligible Foundation correction, not yet started. **Superseded — see the
MESP-94 correction overlay below for the current safety-catalogue and
validation-evidence state.**

## MESP-94 correction — safety-catalogue and validation-evidence accuracy (In Progress)

MESP-94 makes this checkpoint's validation tooling, SQL evidence, safety-row
classifications and provenance say exactly what the repository proves, on
branch `fix/MESP-94-foundation-validation-evidence` based on `main` at
`9f333c9734c767673e43a30d6b57c05793e1fb69` (PR #25 merge — the MESP-93
post-merge Markdown reconciliation).

**SHA evidence model (M-12, L-5).** From this correction forward, evidence
explicitly distinguishes two fields instead of one ambiguous SHA:

- **Implementation SHA** — the commit containing the source/test/tooling
  change being evaluated.
- **Validated repository SHA** — the exact commit whose working tree the
  complete Foundation validation command was actually run against.

These may be equal; when documentation is committed after the validated
source commit, the two are recorded separately rather than collapsed.

**Findings closed:**

- **H-2 (row 45 overstated) — closed.** Row 45's evidence/limitation column
  now states explicitly that `DurableWorkLocalRuntime`,
  `InMemoryDurableWorkStore`, `DurableWorkDispatcher` and
  `TenantDurableWorkWorker` are not referenced by `MiniErp.Api` — an unwired
  local Foundation seam, not a production-composed capability. The underlying
  test evidence itself (`DurableWorkAuthorityRevalidationTests`,
  `DurableWorkTests`) was found to be genuinely broad across the claimed
  User/session/Membership/SupportGrant/SupportCase/Permission lifecycle list;
  status remains PASS with the corrected, honest limitation stated.
- **H-3 (row 40 incorrectly Passed) — closed.** The prior evidence proved
  cross-Tenant *rejection* for all relationship kinds but only one
  representative *acceptance* case. New focused tests
  (`TenantPersistenceTests.Same_tenant_relationship_is_accepted` and
  `SqlServerSafetyTests.Same_tenant_composite_relationship_is_allowed_on_sql_server`,
  both `[Theory]`-driven) now prove acceptance for all three named composite
  kinds (`CompanyBranch`, `BranchWarehouse`, `CompanyDepartment`) in-memory and
  on SQL Server. Row 40 remains PASS with genuinely complete evidence instead
  of a single representative case, and the evidence text now discloses that
  Branch/Warehouse/Department are the generic relationship-kind test fixture,
  not yet real Organization-domain entities.
- **M-3 (collation/SQL probe evidence overstated) — closed.** The collation
  assertion already queried the database live; the safety-catalogue text now
  explicitly states what is and is not proven (see the "SQL Server evidence"
  section above) — no claim of Arabic linguistic sort/search correctness is
  made from a storage round-trip alone.
- **M-6 (incomplete orphan DB cleanup) — closed.** `validate-foundation.ps1`
  now removes stale `MiniErpFoundation_*` databases before creating its own,
  and proves zero remain in a `finally` block that always runs (even after a
  failed step), in addition to the existing xUnit fixture teardown. Only
  databases matching the exact disposable naming convention are ever dropped.
- **M-10 (row 66 partially misclassified) — closed.** Row 66 now states that
  SupportGrant revocation evidence is real and already proven elsewhere,
  while the row's specific comparative claim (rejection evidence distinct
  from revocation) has no implemented "rejection" concept to evaluate.
  Status remains NOT APPLICABLE with an honest reason instead of implying no
  related evidence exists at all.
- **M-12 (repository SHA not distinguished) — closed.** See the SHA evidence
  model above.
- **M-13 (vacuous unsafe SQL configuration test) — closed.** The connection-
  string safety check is extracted into
  `SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString` and
  exercised directly by 6 negative cases + 1 positive control; the fixture
  itself now calls this same method, so the test would fail if the real
  check were weakened or removed.
- **M-14 (validation script does not run the complete Foundation) — closed.**
  `validate-foundation.ps1` is now the single canonical command; see
  "Reproduction" above for its exact scope, including the branch-delta
  `git diff --check origin/main...HEAD`, the validation lock (see the F1/F2
  correction below for its current exact scope) and the guaranteed
  environment-variable restoration added in this PR's review-correction
  round.
- **M-15 (hard-coded LocalDB version path) — closed.** `SqlLocalDB.exe` and
  `sqlcmd.exe` are resolved dynamically but boundedly (PATH first, then a
  probe of only the known `<version>\Tools\Binn` and
  `Client SDK\ODBC\<version>\Tools\Binn` layouts under Program Files — never
  a full recursive scan of the SQL Server tree); no `150`/`160`/`170` version
  segment is hard-coded anywhere in the tracked repository.
- **L-2 (MESP-90 regression missing) — closed.** A new focused test,
  `auth.service.spec.ts`'s `'MESP-90 regression guard: does not desynchronize
  local session state ahead of the server sign-out response'`, extends the
  existing MESP-90 coverage by asserting the session and route remain
  untouched between calling `signOut()` and the server's response settling,
  then confirms the normal 204 outcome still clears state and navigates.
- **L-3 (documentation PR provenance incomplete) — closed.** PR #25 (docs)
  merged to `main` at `9f333c9734c767673e43a30d6b57c05793e1fb69`; PR #23 was
  closed as superseded, never merged; PR #22 (MESP-92) merged at
  `322341e70e56270797d5770b4b90342c20b7833e`; PR #24 (MESP-93) merged at
  `005c796629341ab9becfbc6d1abe2ae34b6a7332` (reviewed head
  `83b0c0ed547dcc1b41c873ed087ab4e62d49c50e`, verdict APPROVED FOR MERGE).
- **L-5 (safety evidence lacks validated commit SHA) — closed.** See the SHA
  evidence model above and the validation table below.

### Focused review corrections (R1–R7) — PR #26

A focused ChatGPT review of PR #26 at reviewed head
`88146a733a65bd6070ae80a3c1b6d17c4a456efa` returned CHANGES REQUIRED BEFORE
MERGE, raising R1–R7. All seven are closed on the same branch:

- **R1 (final catalogue content needs its own validation)** — closed by this
  correction round itself: the complete canonical validation is rerun once
  at the exact commit containing every R2–R7 source/tooling/catalogue
  change, and the source-implementation SHA / validated repository SHA /
  final PR head are recorded separately below rather than reusing the prior
  round's SHA against changed Markdown.
- **R2 (`git diff --check` must cover the branch delta)** — closed.
  `validate-foundation.ps1` now runs both `git diff --check` against the
  working tree and `git diff --check origin/main...HEAD` against the live
  branch delta from current `origin/main`, not a hard-coded SHA. Either
  failure fails the command closed.
- **R3 (guarantee `MESP_SQLSERVER_CONNECTION_STRING` restoration)** —
  closed. Restoration now runs in its own nested `finally`, so it executes
  regardless of a backend failure, a frontend failure, a database-removal
  failure, or a final orphan-database assertion failure.
- **R4 (protect concurrent validation runs)** — closed at this round with a
  named, session-scoped mutex (`Local\MiniErpFoundationValidation`) acquired
  before stale-database cleanup and held through database creation, complete
  validation, final cleanup, the orphan-database proof and environment
  restoration; a run that cannot acquire the lock fails clearly instead of
  racing another run's active database. Manually verified at the time: a
  second acquisition attempt while a first holder is active correctly
  returns not-acquired, and correctly succeeds once the first holder
  releases. **Historical note, corrected by the F1/F2 correction below:** a
  session-scoped `Local\` mutex does not coordinate two processes for the
  same Windows user running in different logon sessions, even though the
  automatic LocalDB instance they share is scoped by Windows user rather
  than by session; that gap, plus recovery from an abandoned lock, is closed
  by the MESP-94 F1/F2 correction below.
- **R5 (SQL configuration evidence counts)** — closed. Every occurrence of
  ambiguous wording is corrected to the exact, unambiguous
  `6 negative cases + 1 positive control`.
- **R6 (safety-catalogue parser column counting)** — closed.
  `SafetyCatalogueValidationTests.ReadCatalogueRows()` now trims the row's
  outer `|` delimiters before splitting, asserts the real 5-column count,
  and indexes `cells[0]`–`cells[4]` directly instead of splitting into 7
  tokens (2 of them always-empty artifacts of the leading/trailing pipe)
  and asserting a confusingly-worded 7-count.
- **R7 (bound SQL tool discovery)** — closed. `Resolve-SqlToolExecutable`
  no longer performs a full recursive `Get-ChildItem -Recurse` scan of the
  entire `Microsoft SQL Server` Program Files tree (which can contain a
  large database-engine installation and be slow). It now probes only the
  two known tool layouts —
  `Microsoft SQL Server\<version>\Tools\Binn\<exe>` and
  `Microsoft SQL Server\Client SDK\ODBC\<version>\Tools\Binn\<exe>` — with a
  single wildcarded version-directory segment per layout; PATH remains the
  first lookup, no SQL Server release is hard-coded, and SQL validation
  still never silently skips (a clear error is thrown if neither tool is
  found).

**Safety-catalogue disposition after this correction:** unchanged in
aggregate — 53 PASS, 21 NOT APPLICABLE, 1 DEFERRED — because rows 40 and 45
remain PASS and row 66 remains NOT APPLICABLE; only their evidence and
limitation text changed. A new structural validator,
`SafetyCatalogueValidationTests`, now proves the catalogue's 75 rows are
uniquely and sequentially numbered, every status is within the allowed
`PASS`/`NOT APPLICABLE`/`DEFERRED` vocabulary, every PASS row declares
non-empty evidence, and every non-PASS row names a deferred owner or scope
boundary; it runs as part of the same `MiniErp.ArchitectureTests` project the
canonical validation command already executes.

### Focused review corrections (F1–F2) — PR #26

A further focused ChatGPT review of PR #26 at reviewed head
`809a4da0e6e3804a6461e55ce34fdfaec0df690e` returned CHANGES REQUIRED BEFORE
MERGE, raising F1 and F2 against the R4 lock closed above. Both are closed on
the same branch:

- **F1 (align validation lock scope with LocalDB scope)** — closed. The
  automatic `MSSQLLocalDB` instance is owned/scoped by Windows user, not by
  logon session, so two processes for the same Windows user running from
  different sessions can reach the identical LocalDB instance; the R4
  session-scoped `Local\` mutex did not coordinate across those sessions.
  The lock now lives in `scripts/FoundationValidationLock.ps1`, shared by
  `validate-foundation.ps1` and its own focused verification harness: a
  Global-namespace named mutex whose name is suffixed with the current
  Windows user's SID (`Global\MiniErpFoundationValidation_<SID>`) and whose
  access control list grants `MutexRights.FullControl` only to that exact
  SID. This coordinates every validation process for the same Windows user
  across sessions, does not serialize unrelated Windows users against each
  other (each user's lock has a distinct name), and cannot be opened or
  signaled by a different Windows user at all (the ACL grants that user no
  rights on it). It does not claim that no other process on the machine can
  ever interfere with LocalDB by some unrelated means -- only that this
  specific lock is user-scoped and cross-session, which is exactly and only
  what a `Global\`-namespace, SID-named, SID-ACL'd mutex proves.
- **F2 (recover safely from an abandoned validation lock)** — closed.
  `Wait-FoundationValidationLock` wraps the wait call and catches
  `System.Threading.AbandonedMutexException`: when the prior owner for this
  Windows user terminated unexpectedly (crash, forced kill, forced session
  end) while still holding the lock, ownership transfers to the current
  process, a warning records the interrupted-prior-owner condition, and the
  run continues into the identical stale-database discovery/cleanup flow
  used for an uncontested acquisition -- it is never treated as an ordinary
  competing active validation run. The lock remains held through the same
  points as before (database creation, complete validation, final cleanup,
  the orphan-database proof and environment restoration) and is
  released/disposed in the same guaranteed outer `finally`.

**Focused lock verification (`scripts/verify-foundation-validation-lock.ps1`,
new).** Dot-sources the identical `FoundationValidationLock.ps1` functions
`validate-foundation.ps1` uses, so the check exercises the real production
lock, not a re-implementation. It spawns genuine separate `powershell.exe`
child processes (not just background threads/jobs, since a Win32 named
mutex is owned per-thread and a second wait from the same thread would
trivially re-enter) and proves, each against a real process boundary:

1. **Active owner prevents another run from entering cleanup** — while a
   holder child process actively owns the lock, a second wait attempt from
   the orchestrator times out (`Acquired = $false`); a real
   `validate-foundation.ps1` run would throw at that point and never reach
   stale-database cleanup, since it never acquired the lock.
2. **A second same-user process/session cannot bypass the lock** — the same
   timed-out wait proves the lock cannot be silently bypassed; the second
   process either genuinely blocks/fails or genuinely acquires, never both.
3. **An abandoned/interrupted owner is recovered safely** — a second child
   process is deliberately terminated (`[Environment]::Exit`) while still
   holding the lock, without calling `ReleaseMutex`, producing a genuine
   OS-abandoned mutex; the orchestrator's own handle is opened before that
   child exits (a handle must already be open when the sole owner
   terminates, or Windows destroys the named object outright instead of
   marking it abandoned), and `Wait-FoundationValidationLock` observes the
   real `AbandonedMutexException` and recovers ownership.
4. **Lock is released after normal completion** — acquire, release,
   dispose, then immediately re-acquire from a fresh handle; the
   re-acquisition succeeds, proving the lock was actually freed.
5. **Lock is released after validation failure** — acquire, throw inside a
   `try`, release inside the matching `finally` (mirroring
   `validate-foundation.ps1`'s own structure), then immediately re-acquire
   from a fresh handle; the re-acquisition succeeds.

All five checks passed, re-run twice for stability. This supersedes the R4
"manually verified" narrative above with an automated, repeatable harness
against genuine multi-process contention and genuine OS-level abandonment.

MESP-94 does not implement production SQL hosting, a production migration,
performance/retention/purge behavior, or any Master Data domain work.
MESP-48 and MESP-50 remain unchanged, explicit production gates.

**Complete Foundation validation evidence (re-run after the R1-R7 focused
review corrections).** Source-implementation SHA:
`ac65e204ca4f134d4c3ae98e7871b936fe01c613`
(`fix(validation): apply PR #26 focused review corrections R2-R7`, branch
`fix/MESP-94-foundation-validation-evidence`, based on starting `main`
`9f333c9734c767673e43a30d6b57c05793e1fb69`). Validated repository SHA:
identical — the run below was executed directly against this commit's
working tree, not a stale prior commit whose Markdown had since changed
(closing R1). Final PR head: see `.ai/CURRENT_STATE.md` and PR #26 for the
exact pushed head once this documentation is committed.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Backend Release build | 0 warnings, 0 errors | `.\scripts\validate-foundation.ps1` |
| Complete backend suite (includes SQL Server LocalDB probes and the safety-catalogue validator) | 582 passed, 0 failed, 0 skipped | Same validation command |
| SQL Server LocalDB suite | 21 passed, 0 failed, 0 skipped (up from 11: -1 removed vacuous test, +6 negative cases + 1 positive control for the real configuration validator, +1 fixture-consistency check, +3 same-Tenant composite acceptance cases) | Same validation command; disposable `MiniErpFoundation_*` database |
| SQL Server collation observed | `SQL_Latin1_General_CP1_CI_AS` (queried live via `DATABASEPROPERTYEX`; no linguistic sort/search claim made — see "SQL Server evidence" above) | Same validation command |
| LocalDB/sqlcmd discovery | Dynamic but bounded: PATH first, then a probe of only the known `<version>\Tools\Binn` and `Client SDK\ODBC\<version>\Tools\Binn` layouts under Program Files — never a full recursive scan of the SQL Server tree (MESP-94 R7); no hard-coded version | `Resolve-SqlLocalDbExecutable`/`Resolve-SqlCmdExecutable` in `scripts/validate-foundation.ps1` |
| Disposable database cleanup | 0 `MiniErpFoundation_*` databases remained (pre-run stale-database sweep and post-run proof both ran, serialized by the validation lock) | Same validation command |
| Validation concurrency lock | **Superseded by the F1/F2 correction below** — at this round it was still the R4 session-scoped `Local\MiniErpFoundationValidation` mutex | `scripts/validate-foundation.ps1`; manually verified two concurrent acquisition attempts block/release correctly |
| Repository-integrity check | `git diff --check` against the working tree passed, and separately `git diff --check origin/main...HEAD` against the live branch delta from current `origin/main` passed (MESP-94 R2) | Same validation command |
| Environment restoration | `MESP_SQLSERVER_CONNECTION_STRING` restoration ran in its nested `finally`, guaranteed regardless of backend/frontend/database-removal/orphan-proof failure (MESP-94 R3) | Same validation command |
| Angular suite | 28 passed, 0 failed, 0 skipped, across 5 test files (up from 27: +1 MESP-90 regression guard) | `npm test -- --watch=false --no-progress` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred (unchanged) | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Hosted CI | Not available | No hosted workflow is configured in this repository |

**Final-head targeted verification (R1 step 5).** After the evidence-only
documentation commit that recorded this table, `SafetyCatalogueValidationTests`
(4/4 passed, confirming the R6 parser fix against the just-committed
Markdown), the MESP-94 focused SQL/configuration tests (`SqlServerSafetyTests`,
21/21 passed), and `git diff --check` / `git diff --check origin/main...HEAD`
against the final committed state (both passed) were re-run at that exact
head. This round is superseded by the F1/F2 correction below.

**Complete Foundation validation evidence (re-run after the F1-F2 focused
review corrections).** Source-implementation SHA:
`037491cee8650bfd38c4fad4d58e3baa86a3e2a4`
(`fix(validation): scope Foundation validation lock per-user across sessions
and recover from abandonment`, branch `fix/MESP-94-foundation-validation-evidence`,
based on the R1-R7 correction at `ac65e204ca4f134d4c3ae98e7871b936fe01c613`).
Validated repository SHA: identical — the run below was executed directly
against this commit's working tree. Final PR head: see
`.ai/CURRENT_STATE.md` and PR #26 for the exact pushed head once this
documentation is committed.

| Validation | Exact result | Command/evidence |
|---|---:|---|
| Backend Release build | 0 warnings, 0 errors | `.\scripts\validate-foundation.ps1` |
| Complete backend suite (includes SQL Server LocalDB probes and the safety-catalogue validator) | 582 passed, 0 failed, 0 skipped (unchanged — F1/F2 add no new `dotnet test` cases; their own verification is the separate PowerShell harness below) | Same validation command |
| SQL Server LocalDB suite | 21 passed, 0 failed, 0 skipped (unchanged) | Same validation command; disposable database `MiniErpFoundation_20260807235850_aaac3c7e` |
| Disposable database cleanup | 0 `MiniErpFoundation_*` databases remained after teardown | Same validation command |
| Validation concurrency lock scope | A Global-namespace named mutex (`Global\MiniErpFoundationValidation_<current-user-SID>`), ACL-restricted to that exact SID, coordinates every validation run for the same Windows user across logon sessions; it does not serialize unrelated Windows users and cannot be opened or signaled by a different Windows user (MESP-94 F1) | `scripts/FoundationValidationLock.ps1`; `scripts/verify-foundation-validation-lock.ps1` (5/5 checks passed, re-run twice) |
| Abandoned-lock recovery | `Wait-FoundationValidationLock` recovers ownership from a genuine `AbandonedMutexException` (prior owner terminated unexpectedly) and continues into the normal stale-database cleanup flow instead of treating it as an ordinary competing run (MESP-94 F2) | Same harness, check 3 |
| Repository-integrity check | `git diff --check` against the working tree passed, and separately `git diff --check origin/main...HEAD` against the live branch delta from current `origin/main` passed | Same validation command |
| Environment restoration | `MESP_SQLSERVER_CONNECTION_STRING` restoration ran in its nested `finally`, guaranteed regardless of backend/frontend/database-removal/orphan-proof failure (unchanged from R3) | Same validation command |
| Angular suite | 28 passed, 0 failed, 0 skipped, across 5 test files (unchanged) | `npm test -- --watch=false --no-progress` |
| Angular production build | Passed — 351.02 kB initial, 87.80 kB transferred (unchanged) | `npm run build` |
| Playwright | 4 passed, 0 failed, 0 skipped | `npm run test:e2e` |
| Production dependency audit | 0 vulnerabilities | `npm audit --omit=dev --audit-level=high` |
| Hosted CI | Not available | No hosted workflow is configured in this repository |

**Final-head targeted verification (F1/F2).** After the evidence-only
documentation commit that records this table, the following are re-run at
that exact final head, without re-running the full suite a second time:
`SafetyCatalogueValidationTests`, `SqlServerSafetyTests`, the focused lock
verification (`scripts/verify-foundation-validation-lock.ps1`), and
`git diff --check` / `git diff --check origin/main...HEAD` against the final
committed state. See `.ai/CURRENT_STATE.md` for the exact final PR head these
were run against and the exact results.

MESP-94 is **not** marked Done by this validation; its Pull Request is pending
review, merge and post-merge closure. No production SQL provider, migration,
retention/purge, performance claim or Master Data implementation was
introduced.

## PR #26 merge and MESP-94 closure (8 August 2026)

The section above is preserved unchanged as the pre-merge evidence record. A
ChatGPT final merge review of PR #26 at approved exact head
`2c7ed3dec4662672bb78967ceb70db7ed73eb7d4` returned verdict **APPROVED FOR
MERGE** (0 Critical, 0 High, 0 Medium blockers). The PR description was
updated to make the F1/F2 final lock design and validation totals
unmistakable, then PR #26 was merged to `main` at actual GitHub merge commit
`06d837c958c1cb7977dc121e3aaea4e7278944fd` (a fast-forward-equivalent normal
merge with `2c7ed3d` as an ancestor, no divergence, no semantic merge edits).

**Post-merge focused verification**, run directly on merged `main` rather
than repeating the full expensive suite (already complete pre-merge at
`037491cee8650bfd38c4fad4d58e3baa86a3e2a4` and targeted at final head
`2c7ed3d`): `SafetyCatalogueValidationTests` + `SqlServerSafetyTests`
**25/25** passed, `scripts/verify-foundation-validation-lock.ps1` **5/5**
passed, `git diff --check` (working tree) and `git diff --check
origin/main...HEAD` both passed, and 0 `MiniErpFoundation_*` databases
remained after the run.

MESP-94 is marked **Done**. No production SQL provider, migration,
retention/purge, performance claim or Master Data implementation was
introduced. See `.ai/CURRENT_STATE.md` for the current canonical state and
the Foundation completion checkpoint / MESP-31 BRD entry eligibility
conclusion in `docs/97_Foundation_Completion_Review_Checkpoint.md`.
