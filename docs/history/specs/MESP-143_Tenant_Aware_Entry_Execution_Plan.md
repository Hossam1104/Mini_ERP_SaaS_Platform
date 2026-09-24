# MESP-143 — Tenant-Aware Entry and Operational Workspace Execution Plan

**Status:** Implemented and merged to `main` at commit `866cb75bb7d0d97c929216b1a449f458a2614097` (PR #67); independent Claude Opus 5 review APPROVE FOR MERGE
**Primary Jira:** MESP-143  
**Parent:** MESP-4 Multi-Tenancy and Tenant Lifecycle  
**Prerequisite:** MESP-123 completion was inherited from the synchronized `main` baseline. MESP-143 review and merge are complete.

## Goal

Replace the foundation-centric Tenant/workspace chooser with a production-oriented multi-tenant SaaS entry flow:

- normal single-Tenant users never see unrelated Tenants;
- Tenant context is resolved server-side from a verified host binding and authenticated membership;
- Overview loads before any optional operational-context switching;
- one operational context is automatic; multiple contexts use a header switcher;
- Platform Administration is a separate control plane;
- Wafra logo is Tenant branding configuration;
- Saudi Riyal symbol is Saudi/SAR country-pack presentation.

## Merged implementation closure — 17 August 2026

The repository implementation is **merged to `main` at commit `866cb75bb7d0d97c929216b1a449f458a2614097`**
(PR #67) following independent Claude Opus 5 approval (`APPROVE FOR MERGE`; 0 P0, 0 P1, 0 P2, 4 non-blocking P3 observations):

- `TenantHostRegistry` owns normalized, collision-safe configuration bindings
  and common/platform host sets. `TenantEntryAuthority` combines a host
  candidate with the existing server-side identity/membership authority;
  hostname, forwarded headers, route parameters, and client Tenant headers do
  not grant access.
- `auth.entry.read`, `auth.operational-contexts.read`, and
  `auth.operational-context-switch` are catalogued Foundation contracts. The
  common host returns only authorized ordinary memberships, Tenant hosts
  return only the exact authorized Tenant, and platform-admin entry exposes no
  Tenant ERP choices.
- Angular consumes the runtime entry contract, redirects single/common-host
  selection to the configured canonical host, renders Tenant Overview first,
  retains `/app/workspaces` as compatibility/management, and exposes only a
  multi-context Company/Branch switcher in the header.
- Branding and country/currency metadata are configuration-led with MESP/SAR
  semantic fallbacks. The currency presentation service is display-only and
  does not alter monetary values, tax, FX, accounting, or persistence.
- The Development launcher supports common `localhost`, Tenant
  `tenant.localhost`, and platform `admin.localhost` host testing while
  preserving the browser Host through the local proxy. Owner-managed source
  assets under `frontend/assets` were not changed.

Validation evidence: Release build 0/0; backend suite 770/770 (22/22 SQL safety
genuinely executed on disposable LocalDB); Angular 204/204; production bundle
490.85 kB initial / 91.94 kB lazy quotation chunk; Playwright 8/8; npm audit 0
vulnerabilities.

DNS/TLS provisioning, Tenant persistence/schema/migrations, full Platform
Administration, external integrations, cross-host SSO architecture, and
downstream P2P/Finance/Inventory effects remain planned future work before
production cutover. A Terra HIGH specialist security audit is recommended
prior to production host/TLS/proxy cutover.

## Phase A — Contract and terminology correction

1. Inventory current Tenant, context, organization-scope, routing, session, auth, and shell contracts.
2. Explicitly distinguish:
   - Tenant;
   - Platform `Tenant Workspace` (MESP-67);
   - ERP operational workspace/context;
   - Company;
   - Branch.
3. Prefer existing Company/Branch organization scope as the Release-1 operational context. Do not invent a parallel authorization model without an approved need.
4. Define canonical host-binding contract and fallback/unknown-host behavior.
5. Update API/OpenAPI terminology before Angular depends on it.

## Phase B — Server-side Tenant host resolution

Implement configuration-led host bindings (working name `TenantHostBinding`).

Required flow:

```text
Host
→ normalized trusted host
→ binding lookup
→ candidate Tenant
→ authenticated actor
→ exact-Tenant membership/access check
→ server TenantContext
→ normal authorization
```

Required defenses:

- unknown host fail-safe;
- duplicate/collision prevention;
- host/forwarded-host spoofing protection;
- no client Tenant override;
- no data returned before authorized TenantContext;
- custom-domain-ready design.

## Phase C — Common and platform entry surfaces

### Common `mesp.com`

- one authorized Tenant → auto-redirect;
- multiple authorized Tenants → chooser limited to those memberships;
- no authorized Tenant → safe empty/onboarding/access state;
- no global Tenant catalogue for ordinary users.

### Platform `admin.mesp.com`

- Platform Overview;
- Tenant Catalogue;
- administrative Tenant Workspace from MESP-67;
- no Procurement/Inventory/Finance data through platform role alone;
- explicit audited membership/support-grant required for Tenant ERP entry.

## Phase D — Tenant ERP navigation

On a Tenant host:

1. authenticate;
2. verify Tenant membership;
3. load Overview;
4. resolve operational context.

If one permitted Company/Branch context exists:
- auto-select.

If more than one exists:
- show header/application-context switcher.

Remove mandatory ordinary-user `Switch workspace` sidebar navigation.

Retain `/app/workspaces` only if it provides useful management/discovery, not as an entry gate.

Correct copy such as:
- `Organization: Wafra`
- `Workspace / Company / Branch: Riyadh Branch`

Never:
- `Current workspace: Wafra` when Wafra is the Tenant.

## Phase E — Tenant branding

Inventory the exact owner-added Wafra logo filename(s) from `frontend/assets`.

Implement a generic Tenant branding profile/lookup.

Recommended display:

- Tenant ERP shell/login at Wafra host: Wafra logo/name primary where configured.
- MESP brand may remain a secondary/powered-by identity.
- Common/platform-admin surfaces: MESP brand primary.
- missing/unapproved Tenant brand: MESP fallback.

No `if tenant == Wafra` logic.

Owner source asset files remain unchanged.

## Phase F — Saudi Riyal presentation

Inventory exact Riyal symbol asset filename(s).

Implement via Saudi country-pack/SAR presentation configuration.

Rules:

- symbol is presentational only;
- preserve `SAR` as semantic/accessibility/text fallback;
- multi-currency comparison/audit/export remains unambiguous;
- no FX, tax, accounting, or stored-amount effects;
- non-SAR currencies unaffected;
- EN/AR, RTL/LTR, documents/print tested.

Coordinate with MESP-12/MESP-37 rather than hard-coding the asset into generic money formatting.

## Phase G — Overview redesign

The Tenant Overview becomes the business starting page.

Only surface capabilities already implemented.

Candidate blocks as modules become available:

- My approvals;
- pending Purchase Requests;
- Supplier Quotations requiring attention/source decision;
- recent activity;
- alerts/exceptions;
- later PO/receivables/payables/stock metrics only after those capabilities exist.

Remove foundation/testing language from primary business UX.

## Required testing

Backend:
- host normalization/binding;
- unknown host;
- single/multi membership;
- cross-Tenant denial;
- platform-role isolation;
- support-grant path;
- host/proxy spoofing;
- organization-scope preservation.

Angular:
- single-Tenant no chooser;
- multi-Tenant bounded chooser only on common entry;
- Overview-first behavior;
- one workspace auto-select;
- multiple workspace header switcher;
- no raw GUID UX;
- EN/AR/RTL;
- Tenant branding/fallback;
- Saudi Riyal symbol/fallback;
- accessibility/responsive.

E2E:
- `wafra.mesp.com`-equivalent local host journey;
- user not in Wafra denied;
- common host single membership auto-route;
- common host multi-membership bounded choice;
- platform admin cannot see Tenant ERP data by platform role alone;
- no cross-Tenant cached/session leakage.

## Jira alignment

Already updated:
- MESP-143 — new implementation Story.
- MESP-4 — Tenant-entry architecture clarification.
- MESP-67 — platform Tenant Workspace terminology clarification.
- MESP-77 — Wafra branding as Tenant configuration.
- MESP-12 — Riyal symbol as Saudi/SAR country-pack presentation.
- MESP-2 — separate Platform Administration control plane.
- MESP-123 — explicit note not to widen the current final corrective session.

## Sequence

```text
MESP-123 F-1/F-2/F-5 correction
→ targeted Opus re-verification
→ Owner/Sol merge + MESP-123 closure decision
→ activate MESP-143
→ tenant-aware entry / operational workspace architecture
→ return to next procurement capability
```

## Progress rule

Do not increase product progress percentages merely for accepting this plan or adding assets. Progress changes only after verified implementation.
