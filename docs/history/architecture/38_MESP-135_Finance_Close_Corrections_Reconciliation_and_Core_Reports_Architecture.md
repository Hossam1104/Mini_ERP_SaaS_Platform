# MESP-135 Finance Close, Corrections, Reconciliation and Core Reports

> **Historical capability record - 30 August 2026.** This document preserves
> the MESP-135 implementation and review evidence. Its embedded branch and
> status snapshot is frozen at that capability's review point; live project
> state is maintained in `TASK.md`, `.ai/CURRENT_STATE.md`, and
> `docs/staticts.md`.

## Status and bounded intent

MESP-135 is implemented on `feat/MESP-135-finance-close-reports` from the
reconciled Finance merge-base `841a777af1622cb4de9c3708cd4a2b389b7ef9e9`.
The current `origin/main` is intentionally one PPT-only commit ahead at
`0d1485d4a2197f23250b1d5acc1a00ddf26dc4c9`; that presentation remains
main-only and is not part of this feature branch. This document records the
implementation boundary for independent Sol review; it does not mark the
capability Done, authorize merge, or activate MESP-139.

### HOLD 6 effective revaluation evidence - 27 August 2026

HOLD 6 keeps the MESP-135 boundary unchanged and corrects the interpretation
of persisted period-end revaluation lines. Close readiness now evaluates the
authoritative MESP-134 `ReconcileUnrealizedFxAsync` result at the exact period
end and builds effective candidates only from uniquely identified,
`Reconciled` rows. A `Reversed` row is retained as valid historical evidence
but is inactive for current coverage; a replacement must be the sole active
candidate for that source. A valid reversed line may remain when the current
authoritative source has zero effect, but an unexpected active line for a
zero-effect source is extra evidence and blocks.

The reconciliation identity is bound to Tenant-filtered Company, source type
and ID, batch, line, original journal, and exact reversal lineage. Reconciled
evidence cannot carry reversal lineage; reversed evidence must carry the exact
persisted reversal journal. Missing, broken, duplicate, stale, extra,
unresolved, cross-Company, and cross-Tenant evidence fails closed. The
deterministic Close readiness fingerprint includes the authoritative scope,
effective active candidates, and unresolved evidence while excluding valid
reversed historical rows from current coverage. This preserves the MESP-134
historical/as-of rule: a later reversal cannot rewrite an earlier period-end
snapshot, and no second revaluation engine, endpoint, entity, migration, or
public operation is introduced.

The implementation/test commit is
`69b20b3c0dbba2a7f3b6c5ade2a19f63ad7fb9bb`. HOLD 6 regression coverage uses
real Finance persistence and genuine journal reversal operations for
replacement-only active evidence, zero-effect reversed evidence, broken
reversal fail-closed behavior, duplicate active candidates, extra active
zero-effect evidence, Tenant/Company isolation, and later-reversal fingerprint
stability.

### HOLD 3 corrective semantics - 27 August 2026

The HOLD 3 remediation preserves the existing Finance boundary and adds no
schema or public-operation change. The close/reopen, year-end-post, and
correction/close SQL safety checks now exercise opposing production
operations through independent contexts and disposable LocalDB databases:
`ReopenPeriodAsync` versus `PostJournalAsync`, `PostYearEndAsync` versus
`PostJournalAsync`, and `CorrectJournalAsync` versus `ClosePeriodAsync`.
Their assertions cover the final persisted period/year state, close history,
close snapshot/fingerprint, year-end closing journal, and correction lineage.

Historical settlement and revaluation exposure is determined from durable
`PostedJournalId`/`PostingDate` and `ReversalJournalId`/reversal `PostingDate`
evidence, together with accounting-date allocations. A settlement row's
current status is not treated as historical truth. MESP-134 Tax, realized-FX,
and unrealized-FX reconciliation similarly requires effective journal dates,
valid reversal lineage, and exact inverse monetary evidence before reporting
`Reversed`; missing or invalid reversal evidence remains pending/blocked.
Production AP/AR reconciliation is covered through the actual
`FinanceSettlementPersistence.GetReconciliationAsync(context, companyId,
asOfDate)` implementation, including control-account journal chronology and
allocation-reversal history.

The capability extends the existing Finance authorities with Company-scoped
period close and reopen/reclose, year-end retained-earnings processing, exact
posted-journal corrections, Finance reconciliation, Trial Balance, General
Ledger, AP/AR aging, account-classified P&L and Balance Sheet, and deterministic
CSV export. It does not add a parallel fiscal calendar, generic Reporting,
scheduling, consolidation, statutory filing, external providers, production
infrastructure, or Wafra-specific accounting behavior.

## Authority and isolation

The server resolves Tenant context before Finance data is read. Every MESP-135
command and query validates the authorized Company against the existing
`IFinanceCompanyProvider`, active lifecycle, functional-currency consistency,
and any selected Company/Branch scope. No request body can select a Tenant,
override actor/session/correlation identity, or change posting authority.

The implementation reuses the MESP-132/133/134 authorities:

- `FinanceFiscalCalendarEntity`, `FinanceFiscalYearEntity`, and
  `FinanceFiscalPeriodEntity` remain the fiscal source of truth.
- Existing journal status, period assignment, posting rules, source lineage,
  reversal links, Cost Center dimensions, and immutable monetary evidence remain
  the accounting source of truth.
- MESP-133 settlement/open-item reconciliation and MESP-134 tax, FX,
  revaluation, and Reporting Currency reconciliation are consumed through
  application interfaces rather than duplicated.

## Durable model and migration

Migration `20260826133441_MESP135FinanceCloseReports` is additive. It adds five
Tenant-owned Finance tables:

| Table | Purpose and invariant |
| --- | --- |
| `finance.PeriodCloseEvidence` | Immutable readiness checks, period version, evaluation time, and deterministic snapshot fingerprint. A Tenant/Period/fingerprint unique index prevents duplicate evidence. |
| `finance.PeriodCloseRuns` | Each close or reclose decision, readiness status, checks, reason, actor/session/correlation, and reopen state. Tenant/Period/Sequence is unique. |
| `finance.PeriodHistory` | Append-only Open/Closed/Reopened/Reclosed transitions with the prior and next state and originating close run. |
| `finance.YearEndRuns` | Calculated/Posted/Reversed Company/Fiscal-Year run, source fingerprint, retained-earnings and posting-rule evidence, journal links, actor/reason, and timestamps. An active source fingerprint is unique. |
| `finance.YearEndLines` | Durable affected P&L balances and copied account classification/name evidence for a year-end snapshot. Tenant/Run/Account is unique. |

All new entities inherit the existing Tenant-owned base, row-version
concurrency, tenant filters, and ownership-verifier registration. Existing
accepted migrations are not edited.

## Period lifecycle and close readiness

Close evaluates the current fiscal period under a serializable transaction and
persists the exact checks used by the successful close. The stable check codes
include `gl_balanced`, `journal_period_assignment`, `posting_lineage`,
`prior_periods_closed`, `period_state`, `subledger_reconciliation`, tax/FX/
Reporting Currency evidence checks, and `revaluation_policy`.

`Ready`, `Warning`, and `Blocked` are distinct. Missing legacy reporting
evidence can remain a warning; unresolved mapping, missing rate, ambiguous
evidence, amount mismatch, invalid period assignment, or required unposted
revaluation blocks close. A later readiness evaluation creates new evidence and
never rewrites the original snapshot.

`revaluation_policy` is decided by historical effectiveness at the requested
as-of date, never by a revaluation batch's current lifecycle status. It reuses
the MESP-134 reconciliation authority evaluated at the same period end date, so
the gate is satisfied only when a period-end revaluation line reports
reconciled evidence there: the original revaluation journal is effective with a
posting date on or before the period end, its monetary evidence is valid, and
it is not reversed on or before the period end. A reversal recorded after the
period end therefore cannot rewrite an already-evaluated historical close,
while a reversal effective on or before the period end, or missing or broken
reversal lineage, keeps the gate blocked. Because the close reconciliation and
the policy gate read one reconciliation result at one as-of date, they cannot
report contradictory unrealized-FX conclusions.

Close requires an explicit reason, exact expected period version, idempotency
key, mandatory antiforgery, protected audit, and Tenant/Company authorization.
Reopen requires the same controls, changes only the current lifecycle, marks
the prior run Reopened, and appends history. A later close creates a new run and
sequence, leaving the original close and reopen evidence intact. Reopen is
rejected while a posted year-end run would make the fiscal year inconsistent;
the year-end reversal path must be used first.

The existing Finance posting fence remains authoritative. Ordinary journal
posting and all source-owned Finance posting paths continue to validate period
state; a Closed period cannot be bypassed through a hidden request flag.
Corrections use the same fence and require a controlled reopen when their
accounting date is in a Closed period.

## Year-end close

Year-end calculation requires every period in the Fiscal Year to be Closed,
the exact year-end date, a configured enabled `finance-year-end.v1` / `close`
Posting Rule, and an active posting Equity account selected by that rule. The
account code and retained-earnings destination are never hard-coded.

The calculation reads actual posted/reversed GL facts and existing
`FinanceAccountType` classification. Revenue and Expense balances are copied
to immutable YearEndLines and balanced against the configured Equity account.
The snapshot fingerprint includes the year and rule versions, target account,
and affected balances. Posting rechecks the source fingerprint, validates the
configuration again, creates one balanced year-end journal in the final period,
retains source/rule/account evidence, and closes the year. Reversal creates an
exact linked journal reversal, reopens the year, and retains both original and
reversal lineage.

Calculation, posting, and reversal are idempotent and serializable. A changed
source or posting configuration fails closed with a stable result such as
`year_end_source_changed` or `year_end_configuration_changed`.

## Exact corrections

Posted journals remain immutable. `finance.correction.create` requires the
original journal, Company, posting date, reason, expected version, idempotency,
and protected audit controls. It creates a `finance-correction.v1` posted
reversal with the exact inverse Debit/Credit and FunctionalDebit/
FunctionalCredit values, copies historical transaction and Reporting Currency
evidence through the existing evidence factory, preserves Cost Center and
posting-rule/source lineage, links both journals, and marks the original
Reversed. It never resolves a current exchange rate or current Tax master.

Concurrent correction attempts serialize on the original journal and produce a
single accepted reversal lineage. A correction in a Closed period fails with a
closed-period result until the period is explicitly reopened.

## Reconciliation and report semantics

The close reconciliation view consumes existing Finance settlement, AP/AR,
Tax, realized FX, unrealized revaluation, and Reporting Currency reconciliation
feeds and returns `Reconciled`, `Pending`, `Blocked`, `Mismatch`, or
`LegacyWithoutEvidence`. It includes close history and year-end runs and does
not fabricate unavailable upstream source data.

Trial Balance and General Ledger read posted/reversed immutable journal facts,
with deterministic posting-date/journal-sequence/line ordering. Trial Balance
uses historical facts before the requested period for opening balances and the
requested period/as-of date for current movement; totals are functional
currency authoritative. Optional Reporting Currency values are returned only
when durable MESP-134 evidence is mathematically valid; legacy missing evidence
remains visible.

AP and AR aging read recognized Finance open items and allocation facts with an
explicit accounting `AsOfDate`, preserving transaction and functional amounts,
party/source identity, due date, overdue days, and the bounded buckets Current,
1-30, 31-60, 61-90, and 90+.

P&L uses Revenue/Expense account classification. Balance Sheet uses
Asset/Liability/Equity classification. Neither report maps by account-code
prefix, English name, Wafra convention, or current master-data reinterpretation.

Export runs the same authorized server query and emits stable UTF-8 CSV with a
report-specific header. It exposes no hidden fields and has no scheduling,
email, PDF, Excel, or external distribution behavior.

## REST, permissions, and audit

The public operation catalogue and OpenAPI metadata contain the 22 MESP-135
operations:

- close readiness, close runs/history, close, and reopen;
- year-end list, calculate, post, and reverse;
- journal correction and close reconciliation;
- Trial Balance/query and export, General Ledger/query and export;
- AP aging/query and export, AR aging/query and export; and
- P&L and Balance Sheet.

Read operations use `tenant.finance.close.view` or
`tenant.finance.report.view`. Close/year-end/correction mutations use the
explicit `tenant.finance.close.manage`, `tenant.finance.close.post`, or
`tenant.finance.correction.create` permissions. Report exports use
`tenant.finance.report.export`. Mutations require antiforgery, mandatory audit,
and idempotency; state-changing actions additionally require `If-Match`.
The existing Finance authorization service remains the only permission and
Company-scope mechanism.

Successful and denied high-risk operations retain the existing Finance audit
shape: operation/resource, actor, session, reason, correlation, idempotency,
result, and timestamp. Secrets are not logged.

## Frontend boundary

The Angular Finance routes are lazy:

- `/app/finance/close` provides Company/year/period selection, readiness checks,
  close/reopen/reclose controls, history, reconciliation, and year-end
  calculate/post/reverse actions.
- `/app/finance/reports` provides Trial Balance, General Ledger, AP Aging, AR
  Aging, P&L, Balance Sheet, reconciliation, filters, and report-specific CSV
  links.

Both workspaces use server results for state, preserve loading/empty/error and
authorization-safe behavior, map successful state changes through the existing
antiforgery/idempotency/If-Match client, and support EN/AR direction switching
with RTL-safe layout. No new branding or protected source asset change was
needed; `frontend/assets` remained untouched.

## Concurrency and verification

The direct MESP-135 persistence tests cover public operation contracts, durable
close evidence/history/reopen, exact correction inversion/linkage, deterministic
Trial Balance and GL totals, and the following provider-realistic SQL Server
races using independent contexts and disposable `MiniErpFoundation_*` databases:

1. `Close01_Concurrent_period_close_has_one_committed_winner`
2. `Close02_Concurrent_reopen_has_one_committed_winner`
3. `Close03_Concurrent_close_and_post_reject_closed_period_journal`
4. `Year01_Concurrent_year_end_calculation_has_one_durable_snapshot`
5. `Year02_Concurrent_year_end_post_has_one_committed_journal`
6. `Corr01_Concurrent_correction_has_one_committed_reversal`
7. `Corr02_correction_and_reversal_are_exact_and_linked`

The final bounded validation evidence is recorded in the current tracker and
Sol handoff. Acceptance, Jira closure, merge, production-provider readiness,
capacity, backup/restore, legal/statutory validation, migration/cutover, and
MESP-139 remain outside this implementation handoff.
