# Task

This file holds one executor prompt at a time, plus the Planner's next-task summary. It never holds
results; those go in [`RESULT.md`](RESULT.md). The rules are in
[`docs/MODEL_ROUTING.md`](docs/MODEL_ROUTING.md) §6–§7 and §10.

## Next executor prompt

Status: **OPEN** (first release, 2026-09-27). The Planner launches the child through Paseo inside its own
session and the main checkout (Q-T), and passes this contract verbatim (`MODEL_ROUTING.md` §6, §10).

```markdown
# MESP-169 (#292) — MESP-141 Slice 12: preview no-effect, pre-commit cancellation, corrected retry

## 1. Role and authority
- You are the **executor**. Opus 5.5 accepts or rejects your result. `AGENTS.md` binds you, §1, §2 and
  §5 especially. Authorization is positive: every action this prompt does not list is forbidden.
- Work item: MESP-169 (#292), a Task under MESP-15 (#104), for capability MESP-141 (#229). Source: the
  accepted MESP-168 (#289) audit, `docs/audit/mesp-141-m40-traceability.md`, gap groups 3 and 4.
- You run **in the main checkout** (Q-T), not in a worktree. The Planner does not edit files while you
  run.

## 2. Read order
1. `AGENTS.md`, then this prompt, then `gh issue view 292` (read-only).
2. `docs/audit/mesp-141-m40-traceability.md`: the rows for M40-REQ-027, -031, -032, -040 and M40-AC-014,
   -015, -016, -021, -033.
3. `docs/requirements/40_Data_Migration_and_Tenant_Onboarding_BRD.md`: those rows in §8 and §27, the
   rules they cite in §29 (M40-RULE-013, -015, -016, -017, -018, -028; WF-02..WF-06), §23 and §26
   (M40-DEC-005).
4. Code. **Serena** first (`initial_instructions` once, then symbol tools); targeted line reads if C#
   symbol lookup is namespace-only. Main areas:
   - `backend/src/MiniErp.App/Modules/Migration/`: `MigrationValidationApplication`,
     `MigrationValidationPolicy`, `MigrationExecutionService`, `MigrationApplicationContracts`
     (run lifecycle);
   - `backend/src/MiniErp.Infrastructure/Persistence/Modules/Migration/`;
   - `backend/src/MiniErp.Api/MigrationEndpoints.cs` and the Foundation operation catalogue;
   - the `Migration*Tests.cs` files and the Migration parts of `SqlServerSafetyTests`.
- **Context7:** for EF Core or ASP.NET APIs you are unsure of. **Ponytail:** full, within `AGENTS.md`
  §1.6. If a plugin is missing, say so in one line and continue.

## 3. Starting-state check (record the output in RESULT.md)
- `git status` clean, on `main`, HEAD descends from `d2de6e8` (the PR #291 merge).
- `gh issue view 292 --json state`: OPEN.
- Anything else is a stop (§10).

## 4. Rules to preserve
- Tenant isolation, fail-closed scope checks, idempotency, the Outcome Unknown hard stop, and "Finance
  owns accounting" (no journal is fabricated). Modules call each other only through public contracts.
- Never weaken, skip or delete an existing assertion. Ratcheted allowlists only shrink.
- **Never invent a business rule.** Production correction and recovery stay open under M40-DEC-005:
  do not build or imply them. Where the BRD leaves a behavior undecided, fail closed and record it.
- Migrations are additive and module-owned. Every EF context reports no pending model changes.
- Every new public REST operation meets the `AGENTS.md` §2 REST/API Definition of Done: catalogue
  metadata, real mapping, OpenAPI with a stable `operationId`, and a contract test.

## 5. Scope
1. **The four non-authoritative outcomes (M40-REQ-027; M40-AC-014, -015, -016).**
   - Validation-only, preview, dry-run and reconciliation-preview each return a distinguishable,
     labelled outcome, and none is an authoritative import. Reuse the existing machinery. Preview may
     be computed from the dry-run plan, and reconciliation-preview from the reconciliation computation
     over dry-run evidence, with no approval, readiness or run-state effect. The label and result
     must still be distinct. If mapping any of the four needs a business choice the BRD does not make,
     stop that item (§10).
   - Preview exposes expected additions, duplicate outcomes, dependencies, control totals and
     exceptions (AC-015).
   - SQL-backed tests prove each of the four leaves every authoritative business store unchanged:
     Master Data, Finance journals and subledgers, Inventory ledger and operational documents. **Row
     counts are not enough.** Before and after each operation, capture a stable-key/value snapshot
     of the owner rows (ordered keys plus the business columns and row version, or a hash of them)
     through the owners' test seams. Assert the snapshots are equal, which also catches an update or
     a delete followed by an insert.
2. **Pre-commit cancellation (M40-AC-033; M40-REQ-040 cancellation part).**
   - A public cancel operation for a run with no committed owner effect. It records a required reason
     and moves the run to `Cancelled`. It is idempotent, audited and Tenant/scope-checked.
   - It is rejected once any owner effect is committed or the outcome is unknown.
   - **Permission:** reuse an existing Migration permission whose holder the BRD treats as the run's
     authorized owner. If none fits without a new authorization rule, stop (§10) and list the options.
3. **Quarantine and corrected retry (M40-REQ-031, -032; M40-AC-021).**
   - Quarantined rows carry a row outcome, an error class, an actionable message, the source ID and the
     correction owner.
   - A linked retry processes only corrected eligible rows. Accepted source IDs stay protected (no
     duplicate effect), and the original rejection history stays visible.
   - Reuse the existing intake/attempt lineage. If the BRD does not define who the correction owner is,
     record the field as supplied data, not an inferred rule.
4. Update `docs/audit/mesp-141-m40-traceability.md` for the rows you change: set their Status, add
   file:line and test evidence, and update the counts.

## 6. Out of scope
- Production correction and recovery (M40-DEC-005), compensation and reset flows.
- Gap groups 1, 2, 5 and 6 (MESP-170..172). UI.
- Any tracker write except the one evidence comment in §9. Jira in any form.

## 7. Acceptance matrix (Opus checks each)
| # | Criterion |
|---|---|
| A1 | All four outcomes (validation-only, preview, dry-run, reconciliation-preview) have distinct labelled results. For each, SQL tests prove through stable-key/value snapshots, not only counts, that no authoritative business store changed. |
| A2 | Cancel: pre-commit only, reason retained, idempotent, audited, Tenant/scope fail-closed; rejected after a commit or Outcome Unknown. Tests cover each branch. |
| A3 | Cancel meets the REST/API DoD: catalogue, mapping, OpenAPI `operationId`, contract test. |
| A4 | Corrected retry processes only corrected eligible rows, creates no duplicate effect for accepted IDs, and keeps rejection history. There are SQL tests. |
| A5 | Quarantine rows carry all five M40-REQ-031 fields; a test asserts them. |
| A6 | The audit file rows and counts are updated with evidence. No other row changes. |
| A7 | All gates are green with 0 skipped; the backend count is 1557 plus the added tests, stated exactly. |

## 8. Gates (paste the output tail and the wall time)
1. `.\scripts\Test-MiniErpBackend.ps1 -NoBuild:$false`: 0 warnings / 0 errors, the pass count, 0
   skipped, the disposable-database line and "MESP data is intact". If the Release build is locked
   by the running dev API (MSB3027/MSB3026), stop only the `MiniErp.Api` process on port 5300, record its
   PID, and let the §9 restart replace it.
2. `dotnet ef migrations has-pending-model-changes` for every context you touch: none pending.
3. `git diff --check` is clean.
4. The frontend gates are not run unless a frontend file changes; none should.

## 9. Git / PR delivery (positive authority, exactly this)
- Branch `feat/mesp-169-migration-run-safety` from `origin/main`. Self-review the staged diff, then make
  conventional commits scoped `migration`, referencing `MESP-169 (#292)`.
- Push normally and open a **Draft** PR to `main` titled
  `feat(migration): MESP-169 (#292) MESP-141 Slice 12 run safety`. Its body carries the A1–A7 evidence.
- One evidence comment on #292.
- **After your last commit that changes code or tests**, restart the runtime from this checkout
  (`MODEL_ROUTING.md` §4.7, Q-S):
  ```powershell
  # stop only the MiniErp.Api listener on 5300 if it is running, then:
  dotnet build .\backend\MiniErp.sln --configuration Release --no-restore
  $env:MESP_DEV_AUTH_BYPASS = [Environment]::GetEnvironmentVariable('MESP_DEV_AUTH_BYPASS','User')
  .\scripts\Start-MiniErpDevelopment.ps1 -ApiPort 5300 -FrontendPort 4300 -Restart -StartupTimeoutSeconds 180
  ```
  Never print a connection string or secret. Record the result and URLs in RESULT.md. Only the
  RESULT.md/TASK.md hand-back commit may follow.
- Finally `git switch main` so the checkout is left on `main`, clean.
- **NOT authorized:**
  - Ready, reviewers, approval or merge;
  - rebase, force-push or any push to `main`;
  - closing or reopening an issue, Status or Capability State changes, new issues.
- Then **STOP.**

## 10. Stop conditions (write a `STOPPED` entry; do not improvise)
- The starting state does not match.
- A required behavior needs a business rule the BRD does not give: a cancel permission, the
  correction-owner semantics, or the mapping of one of the four outcomes. Record the exact question and
  the options, then stop that item only. Finish and deliver the rest.
- The change would weaken Tenant isolation, idempotency or the Outcome Unknown stop, or would need an
  allowlist to grow.
- Any unrelated red in the gate: capture the full failure (console + TRX), classify it and stop.

## 11. Hand-back
- One `RESULT.md` entry at the top per `MODEL_ROUTING.md` §7, Status `DONE` or `STOPPED`. It holds:
  - the starting state;
  - the A1–A7 evidence with file:line;
  - the updated audit counts;
  - the gate output and wall time;
  - the restart result and URLs;
  - the PR URL;
  - deviations.
- **Exact next action:** "Opus 5.5 reviews MESP-169 (#292)."
- `TASK.md`: set this prompt's Status to **CONSUMED**. Leave the summaries untouched.
```

## Next-task summaries (Planner, 2026-09-27)

Accepted Executor cycles since the last Sol review: 3 (`ORCHESTRATION_STATE.yaml`; MODEL_ROUTING §2).

1. **MESP-169 (#292)**: the prompt above.
2. **MESP-170 (#293)**: Slice 13, covering the source contract, lineage and validation coverage.
3. **MESP-171 (#294)**: Slice 14, covering the authority matrix, readiness review and reporting.
   MESP-141 closes only after Slices 12–15. Slice 15 (MESP-172, UI lane) blocks closure.
4. **MESP-151 (#266)**: the golden cycle. After it, Wave 1 (Q-Q).
5. **UI lane (Q-R)**: MESP-153 and MESP-172 wait for the owner's reference UI example.

### Owner actions pending
- Provide the reference UI example for MESP-153 (#268) (Q-R).
