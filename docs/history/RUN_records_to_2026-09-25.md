# RUN.md dated runtime records (archived 2026-09-25)

Moved verbatim from root `RUN.md` during the 2026-09-25 cleanup. Historical evidence only; not current authority.

## MESP-144 reconciliation record - 30 August 2026 (HOLD 5 merge-safety)

MESP-144 reconciliation reached Sol content acceptance at comment `12293` on
reviewed head `ffe5a8975611dcc85c3a7c40dce0b3737b123aeb`. HOLD 5 merge-safety
authority is comment `12296` and exists only to make repository state
merge-safe. At the HOLD 5 executor handoff, Jira and PR lifecycle had not yet
been finalized; Jira and GitHub remain authoritative for their respective
final states. The pre-reconciliation main baseline was
`4d6e33189a3835d5d8d2a58736055a837a3f5bc9`; the reconciled branch is
`chore/project-health-reconciliation-cleanup`.

MESP-137 is Done/accepted/merged. No implementation capability is active;
MESP-138 and MESP-139 remain To Do/inactive; fast-track remains `21/26 = 80.8%`;
production readiness remains approximately `47%` overall / `41%`
Procurement/P2P; and MESP-48/MESP-50 remain open. This checkpoint changes
documentation/state only; no product code, tests, migrations, assets, Jira,
or later capability changed.

### Historical MESP-135 Finance capability snapshot

MESP-134 is Done and squash-merged to `main` at
`1e49814172843c2ec2279b8dcc5fc0a41e5da372` through PR #78 (closure `12122`).
MESP-135 is the only active Finance implementation capability under MESP-10,
In Progress/activated by `12123`, with Finance reconciliation `12124`. The
implementation branch is `feat/MESP-135-finance-close-reports`; feature SHA
`6dca68888c4300dff2575d99b3edf919e965d783` is ready in one Draft/Open/Unmerged
PR for Sol review. The bounded workspace implements Finance close/year-end,
corrections, reconciliation, core reports, and authorized export while
preserving Tenant/Company server authority and the MESP-132/133/134 accounting
evidence model. New lazy routes are `/app/finance/close` and
`/app/finance/reports`.

No Jira writes, Claude Opus review, merge, Ready transition, next-capability
activation, external provider/production credential setup, generic Reporting,
scheduled distribution, consolidation, statutory filing, or Wafra-specific
Finance behavior is in scope. Fast-track remains `18/26 = 69.2%` and production
readiness remains approximately `47%` overall / `41%` Procurement/P2P.

Final validation is Release 0/0; focused MESP-135 persistence 3/3;
REST/OpenAPI/host 55/55; SQL safety 77/77; full backend 1,062/1,062 with 0
failures and 0 skips; Angular 283/283; focused Chromium 5/5; full Chromium
47/47; EF model-change detection clean; and both npm audits at 0
vulnerabilities. The initial Angular bundle is 496.45 kB, with Finance/GL
34.52 kB, close 16.28 kB, reports 16.59 kB, and settlement 56.04 kB lazy
chunks.

### Historical MESP-134 runtime snapshot

MESP-133 is accepted and merged. MESP-134 (Tax, FX, Reporting Currency, and
Revaluation) is the only active implementation capability and is held in Draft
PR #78 for Sol review; HOLD 2 authority is Sol `12080` with Finance
reconciliation `12081` (HOLD 1 `12044` remains historical evidence). It adds
lazy Finance routes under `/app/finance`.
MESP-135 is inactive. Follow the normal restart,
HTTP probe, and protected-asset rules below; no external provider or production
credential is part of this local workflow.

The implemented Tax/FX workspace is `/app/finance/tax-fx`; it is bilingual
EN/AR with RTL support and consumes server-authoritative Tax, Currency,
Exchange Rate, Company, Posting Rule, and evidence contracts. HOLD 1 adds
immutable journal monetary evidence, source snapshots, posting-rule lineage,
supplier-declared-tax fail-closed validation, and visible realized/unrealized/
reporting reconciliation feeds. HOLD 2 corrects one-sided allocation monetary
evidence, uses a real SQL revaluation-versus-allocation race, adds direct
production-persistence regressions, and maps current Finance errors in EN/AR.
The bounded implementation was validated with Release 0/0, backend 1052/1052,
SQL safety 70/70, REST/OpenAPI/host 55/55, Angular 283/283, focused/full
Chromium 10/10 and 42/42, clean EF model-change detection, and both npm audits
at 0 vulnerabilities. The owner runtime validated here is backend `5300` and
frontend `4300`.

## MESP-133 Finance settlement runtime

The current Finance runtime is available at `/app/finance` plus the lazy AP,
AR, and settlement routes `/app/finance/ap`, `/app/finance/ar`, and
`/app/finance/settlements`. The HOLD 4 verification keeps Company/Tenant scope,
server-owned source/direction/mapping authority, truthful approval and
reconciliation states, manual-only settlement methods, and EN/AR RTL
presentation. Runtime evidence is recorded in `docs/staticts.md`; the current
owner-inspection processes are backend PID `32024` and frontend PID `1164`.
PR #77 remains Open/Draft/Unmerged for Sol review. This is not a
production-readiness claim.
