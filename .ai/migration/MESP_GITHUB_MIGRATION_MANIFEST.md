# MESP GitHub Migration Manifest

## 0. Manifest Identity

- Project: Mini ERP SaaS Platform
- Project key: MESP
- Source work-management system: Jira
- Target work-management system: GitHub Issues + GitHub Project
- Canonical repository: `Hossam1104/Mini_ERP_SaaS_Platform`
- Canonical branch for governance state: `main`
- Intended manifest path: `.ai/migration/MESP_GITHUB_MIGRATION_MANIFEST.md`
- Migration mode: Exact-state reconciliation and authority cutover
- Product implementation during migration: FORBIDDEN
- Jira mutation before validated cutover: FORBIDDEN
- MESP-141 activation during migration: FORBIDDEN
- MESP-142 activation during migration: FORBIDDEN

---

## 1. Migration Objective

Migrate the MESP work-management authority from Jira to GitHub without changing product behavior, implementation scope, roadmap activation state, production-gate state, or release governance.

The migration must preserve:

- Jira issue identity and traceability.
- Epic / parent relationships where representable.
- Issue type classification.
- Current issue status.
- Priority.
- Labels.
- Release / capability classification.
- Historical delivery references.
- Relevant comments and attachments where available and appropriate.
- Known dependency and gate semantics.
- Current implementation activation state.
- Current Release 1 progress truth.
- Current repository governance truth.

GitHub becomes the active work-management authority only after validation succeeds.

Jira remains the active authority until cutover is explicitly declared complete.

---

## 2. Verified Starting State

### 2.1 GitHub

Repository:

`Hossam1104/Mini_ERP_SaaS_Platform`

Verified `main` baseline before migration:

`b22c39a4755492bcda729e99032f973a05032551`

Commit message:

`chore: reconcile MESP-140 final governance state`

Known parent:

`0316a2623dfc1b9d8df6952c535757c03dd740b0`

Open pull requests at the verified baseline:

`0`

Canonical GitHub Issues discovered at the verified baseline:

`0`

Therefore the target Issue workspace starts without pre-existing canonical MESP Issue mappings.

---

## 3. Verified Jira Inventory Baseline

Jira project:

`MESP`

Verified live issue range:

`MESP-1` through `MESP-144`

Verified total issue count:

`144`

The migration must reconcile the full live Jira project inventory before cutover.

No issue may be silently omitted.

No issue may be duplicated in GitHub.

No GitHub Issue may represent more than one Jira issue unless the manifest explicitly records an approved deduplication decision.

---

## 4. Current Release 1 Capability State

The following capability state is authoritative at migration start:

- `MESP-138` — Done
- `MESP-139` — Done
- `MESP-140` — Done
- `MESP-141` — To Do / NOT ACTIVATED
- `MESP-142` — To Do / NOT ACTIVATED

Current implementation state:

`ACTIVE IMPLEMENTATION CAPABILITY = NONE`

Current next-capability state:

`NEXT CAPABILITY = NOT ACTIVATED`

Migration work must not activate `MESP-141` or `MESP-142`.

---

## 5. Current Release 1 Progress Truth

Accepted fast-track capabilities:

`24 / 26`

Completion ratio:

`92.3%`

This value is a capability-completion measure only.

It must not be represented as:

- production readiness,
- launch readiness,
- deployment readiness,
- UAT readiness,
- migration readiness,
- tenant onboarding readiness.

---

## 6. Production and Governance Gates

The following known gates remain open and must remain open unless independently resolved by authoritative product/governance work:

- `MESP-48` — Confirm reference tenant volume assumptions — To Do
- `MESP-50` — Confirm tenant data residency and retention policy — To Do

Relevant migration prerequisite state:

- `MESP-40` — Produce Data Migration and Tenant Onboarding BRD — To Do
- `MESP-51` — Confirm migration sources and opening-balance requirements — Done

The tracker migration itself must not close, bypass, reinterpret, or fabricate resolution of any production or governance gate.

---

## 7. Jira to GitHub Issue Identity Rule

Every Jira issue must retain its original Jira key as immutable migration identity.

GitHub Issue title format:

`[MESP-N] <original Jira summary>`

Example:

`[MESP-140] Close cross-cutting Security, Audit, Files, Notifications, Localization, and Support gaps`

Every migrated GitHub Issue body must contain a migration metadata section including at minimum:

- Original Jira key
- Original Jira issue type
- Original Jira status
- Original Jira priority
- Original Jira parent key, if any
- Original Jira labels
- Original Jira URL
- Migration classification
- Migration timestamp
- Migration source authority
- Historical delivery references where known
- Any migration caveats

The Jira key must never be renumbered or replaced by the GitHub Issue number.

The GitHub Issue number becomes an implementation-platform identifier only.

---

## 8. Status Mapping

Use the following default status mapping unless live target configuration requires a semantically equivalent field value.

| Jira Status | GitHub Issue State | GitHub Project Status |
|---|---|---|
| To Do | Open | Backlog / To Do |
| In Progress | Open | In Progress |
| Done | Closed | Done |

Rules:

1. Done Jira issues must be created or reconciled as historical closed GitHub Issues.
2. To Do Jira issues must remain open.
3. In Progress Jira issues must remain open and visibly classified as In Progress.
4. Migration must not convert status merely to simplify GitHub setup.
5. The GitHub Project field is the workflow/status authority after cutover.
6. GitHub Issue open/closed state is a coarse lifecycle representation and does not replace the Project workflow field.

---

## 9. Issue Type Mapping

Recommended normalized GitHub type labels:

- `type:epic`
- `type:story`
- `type:task`
- `type:bug`
- `type:subtask`
- `type:governance`
- `type:decision`
- `type:gate`

Original Jira issue type must also be preserved verbatim in the Issue body.

Do not infer a different issue type solely from the title.

---

## 10. Priority Mapping

Recommended normalized priority labels:

- `priority:highest`
- `priority:high`
- `priority:medium`
- `priority:low`
- `priority:lowest`

Original Jira priority must remain visible in the Issue body even when a normalized GitHub label is applied.

---

## 11. Parent / Epic Mapping

Parent relationships must be preserved using all available target mechanisms:

1. GitHub sub-issue / parent capability when available.
2. GitHub Project relationship fields when available.
3. Explicit Issue-body metadata when native relationship support is unavailable.

Minimum required fallback:

`Parent Jira Key: MESP-N`

and, once known:

`Parent GitHub Issue: #NNN`

No parent relationship may be silently dropped.

---

## 12. Dependency Mapping

Known dependencies must be preserved.

When GitHub native dependency features are unavailable, use explicit body sections:

### Depends On

- `[MESP-N] ...`

### Blocks

- `[MESP-N] ...`

Migration must preserve dependency direction.

Do not infer dependency solely from issue numbering.

---

## 13. Labels

Preserve existing Jira labels where they are valid GitHub label names.

Additionally, use normalized migration/governance labels as needed:

- `migrated-from-jira`
- `release-1`
- `historical`
- `active`
- `not-activated`
- `production-gate`
- `governance`
- `implementation-capability`
- `decision-gate`
- `blocked`
- `type:*`
- `priority:*`

Do not use `active` on `MESP-141` or `MESP-142` at migration time.

---

## 14. Done / Historical Work

Jira Done issues are not to be discarded.

They must be migrated because they provide:

- project history,
- decision history,
- implementation traceability,
- PR/commit traceability,
- governance evidence,
- auditability,
- context for future maintainers and AI agents.

Done issues should normally be:

- represented as GitHub Issues,
- marked Done in the Project,
- closed in GitHub,
- labeled `historical` and `migrated-from-jira` where useful.

Historical closure must not imply that the migration itself re-accepted or re-tested the product capability.

---

## 15. Delivery References

Where Jira or repository evidence identifies delivery artifacts, preserve them in GitHub.

Known examples at migration baseline include:

- MESP-139 delivery lineage includes PR `#88`
- MESP-140 delivery lineage includes PR `#89`
- MESP-140 lifecycle reconciliation includes commit `0316a2623dfc1b9d8df6952c535757c03dd740b0`

These are historical references, not new migration actions.

Do not fabricate PR or commit mappings when evidence is unavailable.

---

## 16. Comment Migration Rules

Migrate comments when they materially contribute to:

- decisions,
- acceptance evidence,
- implementation evidence,
- blockers,
- test findings,
- approvals,
- dependencies,
- production gates,
- governance state.

Avoid importing low-value noise when it does not affect project history or decision traceability.

When comment authorship cannot be represented natively, prefix the migrated comment with:

- Original Jira author
- Original Jira timestamp
- Original Jira issue key

Do not rewrite historical meaning.

---

## 17. Attachment Rules

Attachments must be handled conservatively.

For each Jira attachment:

1. Preserve it directly in GitHub when technically supported and appropriate.
2. Otherwise retain the Jira attachment reference / filename in the GitHub Issue.
3. Record any attachment that could not be migrated.
4. Never delete the Jira source attachment as part of this migration.
5. Never claim successful attachment migration without evidence.

Attachment gaps are allowed to produce a PARTIAL result, but they must be documented.

---

## 18. Duplicate Resolution Rules

At migration start, no canonical GitHub MESP Issues were discovered.

Therefore:

- Jira is the initial identity source for all 144 work items.
- Do not create duplicates if issues appear during execution or due to retries.
- Before creating each GitHub Issue, search for the Jira key.
- Reuse an existing Issue if exactly one canonical Issue already carries that Jira key.
- If multiple GitHub Issues carry the same Jira key, stop that item and classify it as a duplicate conflict.
- Never merge two distinct Jira keys merely because their summaries are similar.

Deduplication key:

`Original Jira Key`

---

## 19. Idempotency Rule

Phase 2 must be safe to retry.

Before each create operation:

1. Search GitHub Issues for the exact Jira key.
2. If zero canonical matches exist, create the Issue.
3. If one canonical match exists, reconcile/update it.
4. If more than one canonical match exists, do not create another; record a conflict.

This rule applies to all `MESP-1` through `MESP-144`.

---

## 20. GitHub Project Design

Target canonical Project name:

`MESP — Mini ERP SaaS Platform`

Recommended Project fields:

### Status

- Backlog
- To Do
- In Progress
- Blocked
- Done

### Jira Key

Text field preserving `MESP-N`.

### Work Type

- Epic
- Story
- Task
- Bug
- Governance
- Decision
- Gate

### Priority

- Highest
- High
- Medium
- Low
- Lowest

### Release

At minimum:

- Release 1
- Post Release 1
- Governance / Cross-cutting

### Capability State

- Historical
- Accepted
- Active
- Not Activated
- Gate
- Backlog

### Parent / Epic

Use native relationship capability when available; otherwise preserve relationship in Issue metadata.

---

## 21. Recommended Project Views

Create or reproduce equivalent views where tooling permits.

### Current Execution

Filter:

- Status != Done
- Capability State in Active, Not Activated, Gate

### Release 1

Filter:

- Release = Release 1

### Remaining Release 1

Focus on:

- MESP-141
- MESP-142
- unresolved Release 1 gates and prerequisites

### Production Gates

Focus on governance / production gate items such as:

- MESP-48
- MESP-50

### Historical Delivered

Filter:

- Status = Done
- historical / delivered work

### Epics

Group by parent Epic or Work Type = Epic.

---

## 22. Migration Classification

Each Jira item must be classified into one of the following target migration categories:

- `HISTORICAL_DONE`
- `OPEN_BACKLOG`
- `IN_PROGRESS`
- `RELEASE_1_REMAINING`
- `PRODUCTION_GATE`
- `GOVERNANCE`
- `DECISION`
- `PREREQUISITE`
- `POST_RELEASE_1`
- `DUPLICATE_CONFLICT`
- `MIGRATION_EXCEPTION`

Classification must derive from live Jira state and project governance, not issue number alone.

---

## 23. Critical Item Classification

The following classification is fixed at migration start:

### MESP-138

- Jira status: Done
- Classification: `HISTORICAL_DONE`
- Release: Release 1

### MESP-139

- Jira status: Done
- Classification: `HISTORICAL_DONE`
- Release: Release 1
- Known delivery reference: PR #88

### MESP-140

- Jira status: Done
- Classification: `HISTORICAL_DONE`
- Release: Release 1
- Known delivery reference: PR #89
- Known lifecycle reconciliation commit: `0316a2623dfc1b9d8df6952c535757c03dd740b0`

### MESP-141

- Jira status: To Do
- Classification: `RELEASE_1_REMAINING`
- Activation state: `NOT ACTIVATED`
- Must remain open
- Must not become Active during migration

### MESP-142

- Jira status: To Do
- Classification: `RELEASE_1_REMAINING`
- Activation state: `NOT ACTIVATED`
- Must remain open
- Must not become Active during migration

### MESP-48

- Classification: `PRODUCTION_GATE`
- Current state: Open / To Do

### MESP-50

- Classification: `PRODUCTION_GATE`
- Current state: Open / To Do

### MESP-40

- Classification: `PREREQUISITE`
- Current state: To Do

### MESP-51

- Classification: `HISTORICAL_DONE`
- Current state: Done
- Relevance: migration-source and opening-balance requirements

---

## 24. GitHub Issue Body Template

Use this minimum template for migrated work items.

```markdown
# <Original Jira Summary>

## Migration Metadata

- Jira Key: `MESP-N`
- Jira Issue Type: `<type>`
- Jira Status: `<status>`
- Jira Priority: `<priority>`
- Jira Parent: `<parent key or NONE>`
- Jira URL: `<original Jira URL>`
- Migration Classification: `<classification>`
- Source Authority: `Jira`
- Migration Label: `migrated-from-jira`

## Original Description

<Preserve Jira description faithfully.>

## Relationships

### Parent

<Parent Jira key and GitHub Issue reference when available.>

### Depends On

<Dependencies when known.>

### Blocks

<Blocked items when known.>

## Historical Delivery Evidence

<PRs, commits, acceptance references, test evidence, or NONE when no evidence is available.>

## Migration Notes

<Any comments, attachment exceptions, representational limitations, or reconciliation notes.>
```

---

## 25. Cutover Acceptance Criteria

GitHub must not become work-management authority until all required checks below pass.

### Inventory

- All 144 Jira items accounted for.
- No silent omissions.
- No unexplained duplicate GitHub Issues.

### Identity

- Every GitHub Issue preserves its Jira key.
- Every migrated Issue can be traced back to Jira.

### Status

- Jira status is preserved semantically.
- Done items are represented as Done / closed.
- Open items remain open.
- In Progress items remain visible as In Progress.

### Relationships

- Epic / parent links are preserved or explicitly represented.
- Known dependencies are preserved or explicitly represented.

### Critical State

- MESP-138 remains Done.
- MESP-139 remains Done.
- MESP-140 remains Done.
- MESP-141 remains To Do / NOT ACTIVATED.
- MESP-142 remains To Do / NOT ACTIVATED.
- MESP-48 remains open unless independently resolved.
- MESP-50 remains open unless independently resolved.

### Product Boundary

- No product implementation occurs.
- No capability is activated.
- No runtime/schema behavior is changed merely for tracker migration.

### Governance

- GitHub Project exists or an explicit tooling limitation is documented.
- GitHub Issues provide complete canonical traceability.
- Repository governance documents are changed only after migration validation.

---

## 26. Cutover Procedure

Only after acceptance criteria pass:

1. Declare GitHub Issues + GitHub Project the active work-management authority.
2. Add an explicit Jira cutover note stating that Jira is now historical migration provenance.
3. Do not delete Jira.
4. Do not rewrite Jira history.
5. Update repository governance references from Jira authority to GitHub authority.
6. Preserve Jira URLs in GitHub Issues for provenance.
7. Record migration completion counts and exceptions.
8. Record the exact GitHub baseline SHA after governance updates.
9. Keep `MESP-141` and `MESP-142` NOT ACTIVATED unless a separate Sol governance decision activates one.

---

## 27. Rollback Rule

Before cutover, rollback is simple:

- Jira remains authority.
- GitHub migration artifacts may be corrected or deleted if necessary.
- Product repository implementation remains untouched.

After cutover, do not silently revert authority.

Any rollback after cutover requires explicit governance reconciliation and must document:

- reason,
- affected issues,
- target authority,
- rollback timestamp,
- residual GitHub/Jira state.

---

## 28. Migration Execution Result Categories

Phase 2 final result must be exactly one of:

### SUCCESS

Use only when:

- all 144 Jira items are accounted for,
- GitHub Issues are validated,
- Project/workflow authority is established,
- critical state is preserved,
- cutover is completed,
- no material unresolved migration gaps remain.

### PARTIAL

Use when:

- core Issue migration is valid,
- but non-authoritative gaps remain, such as:
  - attachment limitations,
  - GitHub Project automation limitations,
  - representational limitations,
- and those gaps are documented without corrupting authority.

### BLOCKED

Use when:

- migration cannot proceed safely,
- permissions prevent canonical migration,
- duplicate identity conflicts prevent safe reconciliation,
- critical state cannot be verified,
- required target workspace cannot be established safely.

---

## 29. Phase 2 Execution Guardrails

During Phase 2:

- Do not restart product implementation.
- Do not activate MESP-141.
- Do not activate MESP-142.
- Do not merge product PRs.
- Do not change product source code.
- Do not change EF migrations or schema.
- Do not change runtime behavior.
- Do not change production-gate truth.
- Do not mark Jira historical until GitHub migration validates.
- Do not use conversation memory as a substitute for live Jira/GitHub freshness checks.
- Do not fabricate attachments, comments, PR mappings, dependencies, or issue relationships.
- Do not silently skip inaccessible data.
- Do not create duplicate Issues on retries.
- Prefer exact Jira-key identity over summary matching.

---

## 30. Required Phase 2 Final Report

The Phase 2 executor must report:

1. Final result: SUCCESS / PARTIAL / BLOCKED.
2. GitHub repository and final migration baseline.
3. GitHub Project name and URL if created.
4. Total Jira items discovered.
5. Total GitHub Issues created.
6. Total GitHub Issues reused.
7. Total GitHub Issues closed as historical Done.
8. Total GitHub Issues left open.
9. Duplicate/conflict count.
10. Comment migration count.
11. Attachment migration count.
12. Attachment exception count.
13. Parent/relationship exception count.
14. Status mapping exception count.
15. Jira items mutated.
16. Repository files changed.
17. Governance commit / PR if any.
18. MESP-138 status.
19. MESP-139 status.
20. MESP-140 status.
21. MESP-141 status and activation state.
22. MESP-142 status and activation state.
23. MESP-48 gate state.
24. MESP-50 gate state.
25. Active implementation capability.
26. Next capability activation state.
27. Product implementation performed: YES / NO.
28. Final tracker authority.
29. Remaining migration risks.
30. Any manual follow-up required.

---

## 31. Phase 1 Reconciliation Conclusion

The migration baseline has been reconciled sufficiently to authorize Phase 2 under the guardrails in this manifest.

Authoritative starting truths:

- Jira project contains 144 live MESP items.
- GitHub repository is canonical for source control.
- No canonical GitHub MESP Issues existed at the verified migration baseline.
- MESP-138 is Done.
- MESP-139 is Done.
- MESP-140 is Done.
- MESP-141 is To Do and NOT ACTIVATED.
- MESP-142 is To Do and NOT ACTIVATED.
- MESP-48 remains open.
- MESP-50 remains open.
- Active implementation capability is NONE.
- Tracker migration is governance work only and is not product implementation.
- Jira remains authority until Phase 2 validation and explicit cutover.

---

# MIGRATION PHASE 2 READY
