# MESP-133 - AP / AR / Cash / Payment / Receipt / Settlement Architecture

## Bounded capability

MESP-133 adds the reusable Finance settlement spine for Accounts Payable,
Accounts Receivable, internal payment methods, cash and bank accounts,
payments, receipts, allocations, reversals, aging, exposure, and bounded
reconciliation. It is implemented as a Company-scoped Finance capability
inside the already authorized Tenant boundary. It does not create a separate
Tenant, workspace, or operational authorization hierarchy.

The capability started from the exact merged-main baseline
`9ace42c7a830b5ef155a26b18d4a888676b8c188` and is delivered on
`feat/MESP-133-ap-ar-cash-settlement` as one focused Draft PR. It was accepted
and squash-merged into `main` at
`3c616dd85b9cebb53990934321f1ae7d0d5410c9` from accepted feature head
`6908c003a32be8a3a31782d855b8358f2a9505f5`.

## Scope and non-scope

The implementation covers:

- AP open-item recognition from the existing MESP-126 Finance-ready supplier
  invoice handoff, with source evidence and supplier/company ownership;
- manual AR invoice/open-item creation as a bounded receivable source, without
  implementing the Sales lifecycle;
- Company-scoped internal payment methods and cash/bank accounts with
  lifecycle, direction, and uniqueness controls;
- payment and receipt lifecycle through submit, approve, reject, post, and
  explicit reversal;
- partial and multiple allocations, on-account/unapplied balances, allocation
  reversal, aging, customer exposure, and bounded reconciliation;
- source-to-subledger-to-GL lineage using the MESP-132 journal and Posting
  Rule contracts; and
- Angular EN/AR/RTL lazy workspaces for AP, AR, and settlements.

The following remain explicitly outside this capability: MESP-134 FX and
exchange-rate setup, tax/VAT/ZATCA/FATOORA, the Sales lifecycle, external bank
feeds or payment gateways, supplier/customer portals, statements, fixed
assets, payroll, treasury, generic Reporting, production provider setup,
migration/cutover, and Wafra-specific core behavior.

## Authorization and ownership

Every Finance settlement read and mutation resolves Tenant and Company scope
server-side from the authenticated context. Hostnames and client-supplied
Company identifiers are routing or request inputs only; they are not
authority. Resource routes re-resolve the Company and reject cross-Tenant or
cross-Company access. Mutations require the existing Finance permissions,
anti-forgery protection, idempotency where applicable, and optimistic
concurrency through `If-Match` on mutable lifecycle/configuration resources.

Payment methods, cash accounts, open items, settlement documents, and
allocations are module-owned Finance persistence. Tenant ownership verification
and global query filters are registered for all five new Finance entity types.

## AP and AR source contracts

AP recognition consumes the existing Procurement-to-Finance supplier invoice
handoff. Only the MESP-126 Finance-ready, exact/within-tolerance/resolved
source state is eligible. Held, unresolved, non-comparable, rejected, or
pending source evidence is not recognized as an AP open item. The adapter
preserves source document and match evidence rather than fabricating an
invoice.

Payment terms and the due-date basis are snapshots on the recognized open
item. The adapter resolves the exact upstream Purchase Order payment-term
identity, code, and version through trusted Procurement persistence seams,
verifies the historical active term version, and derives the due date from the
invoice document date and that version. No Net-30 default is invented.
Missing, untrusted, cancelled, or unsupported term evidence fails closed with
`payment_term_not_configured`.

AR manual creation is deliberately distinct from Sales. The request records
server-owned source identity and the same explicit term/due-date snapshot
rules. A missing term fails closed; no Sales invoice or customer credit
workflow is implied by this slice.

## Settlement model

Payment methods and cash/bank accounts are internal configurable resources;
non-manual/provider-style methods fail closed. Their direction, active
lifecycle, and Company ownership are validated before use. Account selection
and posting are resolved through versioned Finance Posting Rules, and the
cash/bank side must equal the selected account's Company-owned
`LinkedAccountId`; the API never accepts a browser-selected GL account as
authority. MESP-132's `IFinanceSourceApprovalPolicy` supplies Required,
NotRequired, and NotConfigured settlement approval behavior, including
server-side SoD/self-approval enforcement.

Cash movement posts first as an unapplied/on-account settlement document.
Allocations are a separate, auditable subledger action that reduces the
remaining open-item balance. Multiple open items and partial amounts are
supported, with exact decimal validation and no over-allocation. Posted
documents and allocations are immutable. Corrections use explicit reversal
documents and reversed allocations; an allocation cannot be silently edited
after posting.

Realized FX is bounded and fails closed with
`fx_settlement_not_configured` when a non-functional settlement currency would
require MESP-134 configuration. No exchange rate, conversion, tax, or stored
amount is invented by MESP-133.

## Persistence and transactions

The additive Finance migration is
`20260824220208_MESP133ApArCashSettlement`. It adds the five module-owned
tables for payment methods, cash accounts, open items, settlement documents,
and allocations. Existing MESP-132 Finance migrations remain unchanged.

Lifecycle transitions, posting, reversal, allocation, and configuration
mutations use the Finance persistence boundary with serializable transaction
semantics where competing identity, balance, period, or allocation decisions
could race. Durable idempotency keys, version checks, source-to-GL uniqueness,
audit records, and explicit conflict classification protect retry and
concurrent-request behavior. SQL Server LocalDB tests retain the five earlier
MESP-133 configuration races and add ten financial races, including the
provider-realistic allocation-vs-settlement-reversal race, for AP recognition,
open-item/cash-document over-allocation, settlement post/lifecycle ordering,
submit-version ordering, same-payment post, same-receipt post,
posted-settlement reversal, allocation reversal, and allocation-vs-settlement
reversal serialization.

## API and UI surface

The REST/OpenAPI surface is grouped under Finance and includes AP source-ready
and recognition routes, AR open-item routes, payment-method and cash-account
configuration routes, payment/receipt lifecycle routes, allocation and
allocation-reversal routes, and reconciliation/aging/exposure routes. Source
identity, settlement direction, posting authority, and evidence fields are
server-owned.

Angular adds lazy Finance routes:

- `/app/finance/ap`
- `/app/finance/ar`
- `/app/finance/settlements`

The shared standalone workspace exposes Company context, AP/AR open items,
settlement documents, aging/exposure summaries, and bilingual EN/AR labels
with RTL presentation. It is an operational Finance surface, not a new
Tenant/workspace chooser.

## Verification evidence

HOLD 4 is verification-only from the exact HOLD 4 starting SHA
`30ea4a04e5fb120a292083edc03073e37b278b11`; the implementation/test commit is
`7cf177e8eaf694824a91b8b5b0cf3642d0f049f7`, and no production code changed.
The real `ProcurementFinanceSupplierInvoiceSourceProvider` is directly
instantiated with bounded authoritative dependency fakes. Its focused tests
prove active source readiness, missing/inactive/cross-Tenant Supplier
exclusion, no `CreatedAt` commercial-date fabrication, and unsupported
ReceiptDate/DeliveryDate fail-closed behavior. The additional Finance
persistence regression recognizes February under rule A and May under rule B,
inspects the actual AP Control A/B journal lines, and proves historical
reconciliation remains truthful:

- Release backend build: 0 warnings, 0 errors;
- focused Finance remediation: 16/16 passed;
- disposable SQL Server LocalDB backend suite: 1014/1014 passed, 0 failed,
  0 skipped;
- SQL Server safety coverage: 61/61 passed, with the five retained MESP-133
  configuration races and ten financial races, including
  `MESP133_sql_server_allocation_vs_settlement_reversal_race_has_one_valid_serialization`;
- Angular unit tests: 274/274 passed across 38 spec files, including 15/15
  focused Finance settlement-workspace tests;
- focused Finance Playwright: 6/6; full Playwright Chromium: 38/38;
- production build: 0 warnings/errors, 496.44 kB initial bundle, 34.31 kB
  Finance/GL lazy chunk, and 56.04 kB settlement lazy chunk;
- both npm audits: 0 vulnerabilities;
- runtime health, frontend root, `main.js`, `/app/finance`,
  `/app/finance/ap`, `/app/finance/ar`, and `/app/finance/settlements` probes
  returned HTTP 200; a Development-authenticated request to
  `/api/v1/finance/companies` returned HTTP 200;
  and
- `frontend/assets` was inspected and remains untouched.

The existing historical allocation/reversal regression remains retained and
passing. The overall production-ready headline remains approximately 47% and
Procurement/P2P remains approximately 41%. A Draft PR is not merged
capability credit, so the accepted fast-track headline remains 16/26 = 61.5%
until the bounded review and merge decision is complete.

## Deferred handoff

GPT-5.6 Sol must independently review the complete remediation diff, rerun or
verify the validation evidence, inspect the AP term/source boundary, approval
policy reuse, Posting Rule/GL lineage, reconciliation/as-of semantics,
route/document integrity, reversal invariants, and additive migration safety.
MESP-133 remains In Progress / activated in Jira; HOLD comments `11892`,
`11926`, `11963`, and `11967`, MESP-10 progress comments `11893`, `11927`,
`11964`, and `11968`, and manual-AR supplemental finding `11928` remain
authority. No Jira writes, merge, Ready transition, MESP-134/MESP-135
activation, or Opus review/prompt were performed.
