# Mini ERP Tenant-Aware Shell

This Angular 22 application is the first-party Release 1 B2B ERP shell. It
contains the responsive application shell, EN/AR localization and RTL/LTR
direction switching, sign-in/session bootstrap, server-resolved Tenant entry,
Overview-first routing, post-Overview Company/Branch context switching,
configuration-led branding/currency presentation, antiforgery bootstrap, and
accessible safe states.

The Procurement workspaces include:
- **Purchase Requests & Approvals**: request creation, submission, multi-stage approval, and cancellation.
- **Supplier Quotations & Source Decisions**: quotation capture, side-by-side comparison, and single-source decision.
- **Purchase Orders & Confirmations**: server-provided source selection, list/create/edit/detail, approval/issue actions, manual full/partial/rejected/no-response confirmation, supplier-change reapproval, history, and audit views.
- **Goods Receipts**: receipt creation from Confirmed POs, authorized warehouse selection, physical partition (`Received = Accepted + Rejected`), descriptive damage overlay (`Damaged <= Received`), commercial remainder tracking, receipt cancellation, and history/audit tabs.
- **Purchase Invoice Handoffs**: handoff creation from accepted Goods Receipt lines, pro-rata tax allocation preview, un-invoiced remainder tracking, supplier invoice reference & date capture, handoff cancellation, and history/audit tabs.
- **Supplier Returns**: accepted Goods Receipt source selection, remaining return quantity, correction/reversal lineage, private evidence references, Inventory handoff evidence, Finance correction/credit references, operational reporting, and history/audit views.
- **Three-way Matching**: independent supplier invoice evidence, PO/accepted-receipt/invoice lineage, exact-safe and configured tolerance outcomes, variance evidence, controlled exception resolution, and bilingual history/audit views.

Cross-currency matching exposes only a server-owned Exchange Rate identity in
the request model. The server derives the effective version exclusively from
immutable supplier-invoice-date evidence. Rate, scale, version, pair, effective
window, effective date, and provenance are returned as the immutable
MESP-120-backed evaluation snapshot; the browser cannot author FX facts. The
workspace shows a human-readable selector only for active, pair-compatible
identities with a valid invoice-date version; it shows no raw GUID or editable
rate/scale/version/effective-date controls. Same-currency matching continues
to evaluate without an FX reference.

Matching keeps the existing exact server-derived Company/Branch scope. It does
not invent Company-to-Branch policy inheritance or allow a browser-selected
scope to widen the evidence boundary.

The browser never stores an authentication token or establishes Tenant authority.

## Local development

From this directory:

```powershell
npm install
npm start
```

The official repository launcher runs the shell at `http://localhost:4300/`
and calls the API through its generated same-origin proxy. In Development,
`localhost` is the common entry host, `tenant.localhost` is the generic Tenant
host, and `admin.localhost` is the platform-admin boundary. The proxy
preserves the browser Host so the API remains the entry authority; Angular
does not parse or authorize subdomains.

## Validation

```powershell
npm test -- --watch=false --no-progress
npm run build
npm run test:e2e -- --project=chromium
npm audit --omit=dev
npm audit
```

Current gate baselines are recorded in `RESULT.md` and `AGENTS.md` § Gates.

The Playwright checks are automated API-fixture/browser evidence, not a manual interactive production sign-off.

Terminal Cancelled/Rejected Purchase Order detail communicates the bounded
recovery rule in English and Arabic: the source decision is consumed and
continuation requires a new sourcing decision. Controlled same-Purchase-Order
reopening remains a future explicit capability/decision and is not implied by
the UI.

The Purchase Order workspace's `formatMoney` reuses the MESP-123 Supplier
Quotation non-ISO-currency safe-fallback pattern: standard ISO currency codes
render with `Intl.NumberFormat`'s currency style, while a valid MESP-configured
non-ISO code (e.g. a custom currency) falls back to a localized decimal render
suffixed with the raw currency code instead of throwing.

The Playwright smoke journey mocks the approved entry, session, and business
responses so it can verify Overview-first navigation, bounded compatibility
context management, RTL direction, and server-confirmed operations without
inventing business data.

## Structure

- `src/app/core` — API client, secure-cookie request policy, session and
  server-resolved entry/context state, safe errors, currency presentation, and
  language direction.
- `src/app/features` — sign-in, authenticated shell, Tenant Overview,
  compatibility context management, and implemented capability workspaces.
- `src/app/shared` — reusable status, Tenant, and operational-context controls.
- `e2e` — focused Playwright TypeScript smoke coverage.

Retail POS, Wafra-specific core behavior, production deployment and external/statutory
country-pack behavior are out of scope (see `docs/PROJECT.md`).
