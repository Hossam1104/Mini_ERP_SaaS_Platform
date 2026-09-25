# MESP-134 — Tax Accounting, Multi-Currency, FX, Reporting Currency, and Revaluation

## Bounded capability

MESP-134 adds the next Finance capability on top of the merged MESP-132 GL
foundation and MESP-133 AP/AR settlement spine. It is Company-scoped inside
the already-authorized Tenant and does not create a parallel Tenant or
workspace authority.

The implementation owns durable monetary-policy, tax-accounting-effect, and
revaluation-batch evidence. It consumes the existing authorities for Company
functional currency, MESP-119 tax identities/rate versions, MESP-120 exact
exchange-rate versions, Finance posting rules, fiscal periods, journal facts,
AP/AR open items, settlements, and allocation lineage.

## Monetary policy and evidence

Each Company may have non-overlapping effective-dated policy versions. The
Company remains the authority for functional currency. Reporting currency is
optional and is resolved through an active Master Data currency reference.
Rounding scale and mode are persisted with the policy version and are applied
only at the Finance presentation/evidence boundary.

Every non-functional-currency calculation requires one exact active MESP-120
source/target pair and one effective version on the business date. The
evidence contains the pair, version identity, effective date, rate, scale,
provenance, source notes, and a stable reference value. Triangulation and
silent latest-rate selection are not permitted. Functional-currency
transactions carry no fabricated FX evidence.

Reporting values are derived from functional values through the configured
functional-to-reporting pair. A missing or ambiguous reporting rate blocks
the operation and is represented as an explicit evidence status; it does not
change the persisted transaction or functional amount.

## Tax accounting

Tax preview and posting call the existing `MasterDataTaxService` with the
server-owned open-item date, AP/AR direction, tax identity, taxable base,
transaction currency, and source lineage. The returned tax code, exact rate
version, effective date, rate percentage, and amount are persisted with the
Finance effect.

Posting creates a balanced Finance-owned reclassification journal using the
actual recognition journal account as the source-side control and the
effective `finance-tax.v1` input/output rule for the tax side. It validates
the source account and period before posting, records the rule version and
journal lineage, and supports an exact journal reversal with a required
reason. Supplier-declared tax and future statutory return/submission behavior
remain upstream or separately gated contracts; MESP-134 does not invent a
supplier declaration or ZATCA/FATOORA submission.

## Realized FX on allocation

MESP-133 allocation posting now records historical functional carrying value,
settlement functional value, realized difference, direction, exact
`finance-fx.v1` realized rule, and the realized-FX journal. The allocation
journal uses the actual historical AP/AR control account and the actual linked
cash/bank account from the posted settlement journal. It adds the configured
gain/loss account only when the rounded functional difference is non-zero.

The four sign cases are derived from the open-item kind and the difference
between settlement and historical functional value:

| Source | Settlement above historical | Settlement below historical |
|---|---|---|
| Payable | loss | gain |
| Receivable | gain | loss |

Functional-currency allocations have zero realized FX and no FX rule
evidence. Allocation reversal reverses the complete original journal,
including any gain/loss line, rather than recomputing with a current rule.

## Unrealized revaluation

The revaluation workflow is an explicit `Draft → Calculated → Posted →
Reversed` batch. Calculation snapshots recognized, still-outstanding AP/AR
items and posted, unallocated payment/receipt documents as of the requested
date. It derives historical functional carrying value from the original
source evidence and revalues the outstanding transaction amount with one exact
MESP-120 pair on the as-of date.

Posting requires `finance-fx.v1/unrealized`, an open fiscal period, active
posting accounts, and the original source journal lineage. Payable and
receivable signs determine gain/loss direction; every posted line retains the
rate and source evidence snapshot. A new revaluation for the same source is
blocked while an earlier posted batch is active. The earlier batch must be
reversed exactly before another active revaluation can be posted.

## API, security, and UX

The REST catalogue exposes Company-scoped policy, tax preview/post/reverse,
tax reconciliation, revaluation list/detail/create/calculate/post/reverse,
and realized/unrealized FX reconciliation operations. Unsafe mutations require
the existing Tenant context, Finance permission, antiforgery validation,
idempotency key, and audit evidence. Reversal and revaluation actions use
`If-Match` version concurrency. Cross-Company resources resolve as denied or
not found and never become a browser-selected Tenant/workspace filter.

The lazy Angular Finance workspace at `/app/finance/tax-fx` provides EN/AR and
RTL-safe policy, tax preview/posting, and revaluation controls. It displays
server-returned rate-version and journal/evidence state, exposes loading,
empty, unavailable, and blocked-by-evidence states, and keeps the existing
Finance/GL and settlement workspaces intact.

## Explicit boundaries

This bounded capability does not add external bank feeds, payment gateways,
automated provider FX, statutory VAT/ZATCA/FATOORA returns or submissions,
financial statements, generic Reporting, Sales lifecycle, payroll, fixed
assets, treasury, production DNS/TLS, migration/cutover, or Wafra-specific
core behavior. Production/provider, capacity, backup/restore, legal,
specialist, and external/statutory gates remain open.

## HOLD 1 and HOLD 2 corrective hardening - 26 August 2026

The bounded HOLD 1 remediation closes the evidence and provider-realistic
concurrency gaps identified for Sol review without widening MESP-134. Finance
now persists immutable monetary evidence for generic journals, allocations,
settlement journals, tax effects, revaluation journals, and exact reversals.
The evidence retains transaction/functional/reporting currency amounts,
unrounded source values, rounding policy, exact MESP-120 rate identity/version,
and source lineage. Revaluation lines also retain the immutable source snapshot,
snapshot fingerprint, and the posting-rule identity/version used at posting.

Tax posting remains server-authoritative. Supplier-declared tax is accepted only
when the trusted source provides one unambiguous tax identity, invoice date,
currency, taxable base, rate, and amount matching the server calculation;
missing, ambiguous, or mismatched declarations fail closed. Preview and Post
share the same effective-date tax and monetary evidence path.

The REST surface now exposes realized-FX, unrealized-FX, and Reporting Currency
reconciliation feeds. These feeds compare persisted journal/effect evidence and
actual journal lines; they never recompute historical values from a current rate.
The Angular Tax/FX workspace renders all three feeds with loading, empty,
blocked, evidence, and EN/AR RTL states while preserving the disabled exact
revaluation scope `AP_AR_AND_UNALLOCATED_SETTLEMENTS`.

Provider-realistic SQL Server LocalDB races cover allocation capacity and
allocation/reversal serialization, duplicate allocation reversal, concurrent
tax post and reversal lineage, same-batch and same-source revaluation posting,
the real source-allocation race during revaluation post, and reversal versus
later revaluation. HOLD 2 also asserts allocation monetary evidence from one
balanced side rather than both absolute line sides, and directly exercises
Procurement Tax, historical FX identity, realized gain/loss, exact reversal,
and revaluation persistence behavior. The completed validation is Release 0
warnings/0 errors; focused MESP-134 persistence 24/24; backend 1052/1052 with
0 failures and 0 skips; SQL safety 70/70; REST/OpenAPI/host 55/55; Angular
283/283 across 39 spec files with focused Tax/FX 9/9; focused Finance Chromium
10/10; full Chromium 42/42; EF model-change detection clean; initial bundle
496.44 kB; Finance/GL lazy 34.52 kB; Tax/FX lazy 40.38 kB; settlement lazy
56.04 kB; and both npm audits 0 vulnerabilities.

This HOLD 2 remediation is commit
`550c9a7ccf1a7d5d3115efc495a289d80a63bb4c` from HOLD 1 head
`4ee5b39e47f514178ffb40a5add5facce4c32b28` and remains a Draft PR #78 handoff
for GPT-5.6 Sol acceptance.
No Jira write, Ready transition, merge, Opus review, MESP-135 activation,
external provider, statutory submission, production cutover, or
Wafra-specific core behavior is part of this bounded session.

## HOLD 6 shared historical/effective reconciliation clarification - 27 August 2026

The downstream MESP-135 Close readiness gate consumes this module's
`ReconcileUnrealizedFxAsync` result at the requested accounting as-of date; it
does not infer active evidence from the existence of a persisted
`FinanceRevaluationLineEntity`. MESP-134 remains the authority for effective
original/reversal journal chronology, exact reversal lineage, monetary inverse
evidence, and Tenant/Company/source identity.

At a period end, `Reconciled` is an active candidate, `Reversed` is valid
historical evidence but not active coverage, and missing/broken/ambiguous
evidence remains unresolved. This lets a correctly reversed line coexist with
one replacement active line, permits valid reversed historical evidence for a
current zero-effect source, and rejects extra active zero-effect evidence.
MESP-135 applies the resulting one-active-candidate-per-non-zero-source rule
and includes effective candidates plus unresolved evidence in its deterministic
readiness fingerprint. No MESP-134 endpoint, entity, migration, or FX
calculation semantics are changed by HOLD 6.
