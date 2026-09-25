# MESP-132 Finance Foundation Architecture

> **Historical capability record - 30 August 2026.** This document preserves
> the MESP-132 implementation boundary and handoff evidence. Its embedded
> status snapshot is frozen at that capability's review point; live project
> state is maintained in `TASK.md`, `.ai/CURRENT_STATE.md`, and
> `docs/staticts.md`.

## Status and boundary

**Current acceptance state:** MESP-132 is merged through PR #76 under Epic
MESP-10 at squash SHA `ccc52a892c8258778f57c55c12fa0032bd3e276b`, from
accepted feature head `c0e04553db3c7b04fa7f7870b60fc439ec8a40b7`. The retained
implementation branch is `feat/MESP-132-finance-foundation`; its source/test
implementation commit is `dcae7e231bd264580c33e60c35f5cc8436c4f050`, based on
`fcec241dfedb529fef89d4336adf1e571917c52a`. MESP-132 remains Jira In Progress
pending Sol closure and MESP-10 reconciliation. Accepted fast-track completion
is `16/26 = 61.5%`; production-readiness remains approximately `47%` overall /
`41%` Procurement/P2P.

MESP-132 adds the bounded Release 1 Finance / General Ledger foundation on
the exact baseline `fcec241dfedb529fef89d4336adf1e571917c52a`. It is Company
owned inside a Tenant and is deliberately configuration-led. It does not
make a Tenant-wide accounting book, seed customer accounts, or add
customer-specific Wafra behavior.

This document describes the merged implementation and retained feature branch.
It is an implementation architecture record, not a replacement for the
approved Finance BRD or the Owner decision register.

## Company accounting boundary

The Finance company provider exposes only active, server-configured Companies
authorized in the resolved Tenant context. Each Company has its own:

- Chart of Accounts;
- functional currency;
- Fiscal Calendar, Fiscal Years, and Fiscal Periods;
- Cost Centers and posting rules;
- journals and immutable posted GL facts.

The implementation never infers a universal Tenant currency. Development
configuration may use SAR as an explicit Company fixture, but SAR is not a
hard-coded Tenant rule. The client does not supply Tenant authority and no
raw Tenant identifier is accepted by Finance selectors.

## Chart of Accounts

Accounts are normalized and unique within `TenantId + CompanyId + Code`.
They retain English and optional Arabic names, parent identity, account type,
posting eligibility, currency behavior, effective dates, lifecycle, and an
optimistic-concurrency version. The bounded account-type catalogue is the
standard five-way Finance classification used by this slice: Asset,
Liability, Equity, Revenue, and Expense. It is a contract enum for the
foundation, not seeded customer policy.

Parent accounts must belong to the same Company. Creation rejects a missing
or self-parent, and maintenance rejects ancestry cycles. Posting and grouping
accounts are distinguished by `IsPostingAccount`. There is no destructive
account delete path: inactive or non-postable accounts remain readable for
historical evidence and are rejected at posting time.

Posted journal lines snapshot account code and name. Later account edits or
lifecycle changes therefore cannot make a historical GL line uninterpretable.

## Fiscal Calendar, Fiscal Year, and Period

Finance owns one active configured Fiscal Calendar per Company. A Calendar
stores the Company functional currency and can represent a non-Gregorian or
non-January configuration through explicit dates; the implementation does not
auto-generate a calendar year or twelve periods.

Fiscal Years have explicit boundaries and belong to one Calendar. Fiscal
Periods belong to one Fiscal Year, must remain inside its boundaries, have
positive deterministic sequence/code values, and cannot overlap. Posting-date
resolution is server-side and must find exactly one applicable period; no
period or an ambiguous period fails closed.

The period state model is `Draft -> Open -> SoftClosed -> Closed`, with
controlled state changes requiring the current version. Open permits normal
posting. SoftClosed is not treated as Open and has no implicit exception
path. Closed blocks ordinary posting. Transitions involving SoftClosed or
Closed require an explicit reason and produce immutable Finance audit
evidence with actor, correlation, idempotency, and timestamp context.

The implementation does not fabricate year-end accounting. Retained-earnings
postings, automatic P&L close, carry-forward journals, equity mappings, and
new-year opening journals remain a future policy seam and are not generated
by Finance migration or period controls.

## Posting dimensions and Cost Center

Cost Center is the approved posting dimension for this bounded foundation.
The repository had Cost Center terminology and BRD references but no existing
Master Data Cost Center persistence to reuse, so MESP-132 owns the narrow
`finance.CostCenters` structure for this slice rather than duplicating an
existing data-bearing Master Data entity. It is Company-applicable, Tenant
owned, lifecycle controlled, effective-dated, and server-authorized.

Only Cost Center is introduced here. Department, Project, Employee,
Salesperson, Profit Center, Region, and other dimensions are not silently
invented. Posting rules can require Cost Center. A missing, foreign, inactive,
or out-of-effective-date dimension fails closed. Posted lines snapshot the
Cost Center identity and code for later inquiry and reconciliation.

## Journal lifecycle and invariants

Finance journals are Company-owned and retain journal sequence/number, dates,
functional and transaction currency facts, exact FX evidence where needed,
source contract/event/evidence, posting-rule identity/version, description,
actor and correlation evidence, status, and concurrency version. Lines retain
account snapshots, debit/credit, functional debit/credit, transaction amount
and currency, Cost Center, and description.

The bounded lifecycle is:

`Draft -> Submitted -> Approved -> Posted`

with `Rejected` and `Cancelled` available before posting. Posting is always a
separate Finance action; an Approved journal is not yet a GL effect. Posted
journals and lines have no edit or delete path.

Every line has exactly one economic side: positive debit with zero credit, or
positive credit with zero debit. A journal needs at least two lines, rejects
negative/both-zero/both-positive line values, and cannot be auto-balanced with
a suspense or plug line. Before posting, Finance revalidates Company scope,
period, account lifecycle/effective dates/posting eligibility, dimensions,
mapping, and exact functional-currency debit/credit equality.

Manual journal lines expose debit/credit sides only; the server derives a
positive transaction amount and reversal swaps sides without negating that
amount. Source-generated handoff lines may carry a null transaction amount
when no authoritative source-document amount exists; Inventory functional
`BaseAmount`/`SignedBaseAmount` is not converted a second time.

Reversal creates a new Posted journal with equal-and-opposite lines, links it
to the exact original, preserves account/dimension/currency/rate evidence,
requires a reason and eligible posting period, and marks the original
`Reversed`. The original remains immutable and a second reversal is rejected.

## Posting rules and source lineage

Posting Rules are Company-owned, explicit, version-numbered, effective-dated,
and lifecycle controlled. A rule maps a source contract/event to one debit
account, one credit account, and the required Cost Center behavior. Creating a
new rule for the same source classification increments the version and
rejects overlapping enabled effective ranges. Lifecycle changes use
If-Match, idempotency, audit, and safe conflict handling.

Source-generated posting requires exactly one enabled, effective rule. No
rule returns `pending_mapping`; more than one applicable rule returns
`ambiguous_mapping`; neither path chooses first/latest/lowest-ID policy.
The selected rule and version are snapshotted on the Posted journal.

The rule key includes both source type and movement direction. Inbound and
outbound classifications are centralized and cannot silently share a mapping;
missing mapping is `PendingMapping`, while ambiguous mapping is `Blocked`.
The handoff company is resolved from the exact server-owned Inventory handoff
before authorization or processing. Source-generated journals distinguish
functional-source authority from manual transaction-currency authority, carry
positive transaction amounts only when authoritative source evidence exists,
and never perform a second FX conversion from Inventory `BaseAmount`.

Source financial effects are protected by a Tenant + Company + source
contract + source evidence ID/version uniqueness constraint. A retry of the
same actor/key/fingerprint replays its durable result; a different payload
with the same key fails with an idempotency conflict.

## Manual Journal source authority and SQL Server concurrency evidence

The public manual Journal write contract is intentionally narrower than the
trusted source-generated path. It does not accept source contract, source
event, source evidence ID/version, Posting Rule identity, or amount-authority
fields from the browser. The server always persists the manual identity
`manual-journal.v1` / `manual`, null source evidence and rule identity, manual
amount authority, and `Required` approval. Manual edit preserves the existing
source identity and amount authority, so a client cannot convert a trusted
source-generated Journal into a manual or otherwise impersonate source-owned
evidence. The Inventory handoff path remains the sole trusted route for
`inventory-valuation-finance.v1` lineage and retains its source evidence,
directional mapping, and source-owned functional amount authority.

The SQL Server provider-realistic safety suite proves the exact contention
boundaries required for this foundation: period close versus posting,
account restriction versus posting, two concurrent posts of the same
approved Journal, two concurrent processes for the same Inventory handoff
with different idempotency keys, and first-company JournalSequence
allocation. Expected SQL Server deadlock/duplicate/concurrency outcomes are
classified as public safe conflicts, and final durable state is asserted for
each race. The suite passes `46/46` against disposable LocalDB with no new
migration or model drift.

## Multi-currency

Books balance in the Company functional currency. Transaction currency is
preserved when supplied, but the browser never authors the functional amount
or FX authority. Functional-currency journals require explicit deterministic
rate-one evidence. Foreign-currency journals require the exact active
MESP-120 Exchange Rate and Version, direct source/target currencies, version
number, rate, effective-date applicability, and provenance-backed Master Data
record. Latest-rate fallback, inversion, future-rate use, external feeds, and
silent defaults are rejected.

The accepted FX direction is transaction/source currency to Company functional
currency (for example USD → SAR at 3.75); the inverse pair is rejected. At
posting time Finance re-reads each account's current CurrencyBehavior, so a
manual foreign-currency journal is blocked if any participating account is
now `FunctionalOnly`.

Reporting Currency is not a second ledger. MESP-132 creates no parallel
reporting book, consolidation, revaluation, or generic financial-statement
projection.

## MESP-131 Inventory valuation handoff

Finance consumes the accepted `inventory-valuation-finance.v1` read contract.
Inventory remains the owner of physical movement and valuation state. A
ReadyForFinance handoff contains source movement/evidence lineage, ledger
sequence, direction, quantity, base amount, signed effect, rounding evidence,
functional/transaction currency, exact FX references where applicable,
valuation policy/version, correction lineage, contract version, and
correlation.

Finance resolves one applicable Company posting rule, creates the Finance
journal, and posts it atomically through Finance controls. Missing mapping is
truthfully exposed as Pending Mapping; duplicate source evidence is replayed
or rejected by durable uniqueness. Finance never mutates Inventory movement,
valuation, or correction records and does not automatically backfill or post
historical handoffs during migration.

## Authorization, SoD, concurrency, idempotency, and audit

Every Finance request starts from the trusted Foundation Tenant context and
uses an exact Finance operation descriptor and permission. Company scope is
checked against server-owned Company configuration and compatible Branch
scope. The implementation does not create a second approval engine; journal
status transitions and the reusable authorization/SoD seams leave approval
and posting authority separate where policy requires it. Approval uses the
exact `finance.journal.approve` operation and rejects approval by either the
creator or submitter. Source handoff approval is policy-driven: `Required`
creates and submits a journal with `pending_approval`, `NotRequired` posts
directly, and an unconfigured policy fails closed as
`approval_policy_not_configured`; no browser or handoff actor is fabricated
as `ApprovedBy`.

Unsafe REST mutations require antiforgery and `Idempotency-Key`. Durable
Finance idempotency records preserve actor, operation, key, fingerprint,
resource, and result snapshot. EF concurrency versions plus Serializable
transactions protect account/rule/period/journal changes; `If-Match` is
required for lifecycle and journal state mutations. Audit records preserve
Tenant, actor, session, operation, resource, result, reason, correlation,
idempotency, and timestamp evidence.

## REST and OpenAPI

Finance routes are registered through the existing Foundation operation
catalogue and carry operation metadata/tags. The bounded surface includes
Company and account selectors, account create/edit/lifecycle, calendars,
years and periods, Cost Centers, posting-rule create/list/lifecycle,
journal create/list and lifecycle/post/reverse actions, GL inquiry, and
Inventory handoff list/process. Safe Problem Details codes are returned for
scope, mapping, period, dimension, balance, FX, idempotency, and concurrency
failures. Finance API code does not reference EF directly.

## Angular workspace

`/app/finance` is lazy-loaded, keeping Finance out of the eager application
bundle. The workspace provides server-populated Company context and bounded
tabs for Overview, Chart of Accounts, Fiscal Periods, Journals, Posting
Rules, Inventory Handoff, and GL inquiry. Manual journal entry shows debit,
credit, and difference as UX feedback; the backend remains authoritative.

The workspace uses existing EN/AR localization and RTL direction services,
accessible labels, responsive tables/forms, safe error presentation, and no
raw GUID entry. It does not compute posting authorization, period
eligibility, balance authority, FX authority, or source uniqueness.

## Persistence and migration ownership

Finance owns the `finance` schema and its DbContext, entities, query filters,
Tenant ownership registration, SQL Server migration history, design-time
factory, and SQLite Development composition. The migration creates only
Finance-owned tables: Accounts, FiscalCalendars, FiscalYears, FiscalPeriods,
CostCenters, PostingRules, Journals, JournalLines, AuditEvents,
IdempotencyEntries, and SourceEffects. Shared `tenancy.TenantOwnedRecords`
remains physically owned by the Tenancy module.

MESP-132 is not production migration, opening-balance migration, cutover, or
provider approval. Migration creates Finance structures only; it does not
seed accounts or post existing Inventory valuation evidence.

## Explicitly deferred

AP/AR lifecycle, supplier/customer invoices, payments, receipts, cash/bank,
settlement, tax/VAT/ZATCA/FATOORA, financial statements, trial-balance and
generic Reporting platforms, P&L/Balance Sheet/Cash Flow, retained-earnings
year-end mechanics, consolidation/intercompany, fixed assets, payroll,
treasury, budgeting, automated FX and revaluation, Sales, external providers,
production migration/cutover, statutory certification, and Wafra-specific
Finance behavior remain outside MESP-132.
