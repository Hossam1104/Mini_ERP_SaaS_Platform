# Mini ERP SaaS Platform - Jira Simplification Update

> **Historical governance artifact — superseded as an active execution prompt.**
> This document records an earlier Jira-only operating plan and is retained for
> traceability. It is not the current session instruction and must not be
> executed from this B2 handoff. The current model policy is GPT-5.6 Sol for
> planning/live verification/Jira, GPT-5.6 Luna Max for implementation, and
> Claude Opus 5 for defined later independent review.

## Purpose and execution condition

This is a copy-ready execution plan for Claude Sonnet. It simplifies the existing MESP BRD Foundation for solo-founder operation without creating new issues or implementation work.

**Do not execute any Jira write until Hossam has completed the approval block in `docs/90_MVP_Founder_Decision_Pack.md`.** After approval, apply the operations below in order. Preserve existing assignee, component, priority, version, issue type, parent, and description unless an operation explicitly changes them.

Jira site: `https://hossamsqa.atlassian.net`

Project: `MESP - Mini ERP SaaS Platform`

Accountable owner and assignee: Hossam

## Execution order

1. Confirm the Founder Decision Pack approval block says `Approved to start detailed BRD: Yes` and `Approved first Task: MESP-27`.
2. Post the specified comments and apply labels to MESP-17 through MESP-25.
3. Transition eligible MESP-17 through MESP-25 items to Done exactly as stated; keep MESP-23 In Progress.
4. Post the MESP-26 approval comment, update labels, and transition MESP-26 to Done.
5. Transition only MESP-27 to In Progress and post its start comment.
6. Verify MESP-28 through MESP-40 remain To Do.

## MESP-17 through MESP-25

### MESP-17 - Define BRD Governance and Approval Process

- **Action:** Simplify for solo-founder operation.
- **Owner:** Hossam.
- **Status:** In Progress until the comment below is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `solo-founder-governance`.
- **Labels to remove:** `status-in-review`, `status-blocked` if present.
- **May move to Done:** Yes, after Hossam's Founder Decision Pack approval is confirmed.
- **Must Hossam read the full issue:** No; the Founder Decision Pack and this comment are sufficient.
- **Exact comment to post:**

```markdown
## SOLO-FOUNDER GOVERNANCE APPROVAL

Hossam approves the BRD governance framework with this operating correction for the MVP stage.

Interim project accountability is held by Hossam as Product Owner, Business Sponsor, Business Analysis Lead, Architecture Owner, QA Lead, and Implementation Lead. Separate people, deputies, workshop quorum, and standing committees are not prerequisites to beginning a BRD.

This consolidation does not weaken product controls. Only Hossam may record founder approval. Posted records remain immutable and are corrected by controlled reversal or superseding records. System requirements for separation of duties, prevention of self-approval where approved, payment and posting controls, privileged access, audit evidence, and change history remain in force.

External specialist validation is required before production approval for Finance and accounting, Saudi VAT, ZATCA e-invoicing, PDPL, privacy, data residency, data retention, cybersecurity, and penetration testing.

This Task's governance structure is approved for solo-founder BRD operation. No implementation work is authorized by this approval.
```

### MESP-18 - Create ERP Business Glossary

- **Action:** Keep; accept as the controlled working glossary.
- **Owner:** Hossam.
- **Status:** In Progress until the comment below is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `working-glossary`.
- **Labels to remove:** `status-in-review`, `status-blocked` if present.
- **May move to Done:** Yes. The creation Task may close while individual Draft and Requires Business Decision terms remain open in their owning BRDs.
- **Must Hossam read the full issue:** No; use the glossary as a reference and review disputed terms when due.
- **Exact comment to post:**

```markdown
## WORKING GLOSSARY APPROVAL

Hossam approves `docs/00_ERP_Business_Glossary.md` as the mandatory controlled vocabulary for detailed BRD authoring.

This approval accepts the glossary structure, approved-baseline terms, Retail POS exclusion, and terminology controls. It does not silently approve terms marked Draft for BRD Validation or Requires Business Decision. Those terms remain visibly open and must be resolved in their owning BRD or approved decision.

The glossary creation foundation is complete and this Task may move to Done. The glossary remains a living controlled document during BRD authoring.
```

### MESP-19 - Create BRD Traceability Matrix

- **Action:** Keep with the existing v0.2 correction.
- **Owner:** Hossam.
- **Status:** In Progress until the comment below is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `progressive-traceability`.
- **Labels to remove:** `status-in-review`, `status-blocked` if present.
- **May move to Done:** Yes. The foundation model closes; requirement rows continue to be populated by the owning BRDs.
- **Must Hossam read the full issue:** No; the v0.2 routing correction and this comment are sufficient.
- **Exact comment to post:**

```markdown
## TRACEABILITY FOUNDATION APPROVAL

Hossam approves the BRD traceability structure and baseline inventory, subject to the existing v0.2 correction: MESP-34 is Finance and Accounting and MESP-35 is B2B Sales and Order-to-Cash.

Detailed traceability rows are populated progressively as MESP-27 through MESP-40 are written. Zero detailed BRD coverage at this stage is expected and is not a blocker to starting MESP-27. Every later requirement must still trace to its PRD parent, business rules, decisions, acceptance evidence, and Retail POS/Wafra exclusion controls.

This foundation Task may move to Done. No implementation Story is authorized or created.
```

### MESP-20 - Identify Business and Domain Owners

- **Action:** Simplify; replace the all-TBD operating approach.
- **Owner:** Hossam.
- **Status:** In Progress until the comment below is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `solo-founder-governance`, `external-validation-required`.
- **Labels to remove:** `status-blocked`, `status-in-review` if present.
- **May move to Done:** Yes. Do not require invented deputies or external names before the relevant validation is due.
- **Must Hossam read the full issue:** No; this ownership comment is authoritative for the MVP BRD stage.
- **Exact comment to post:**

```markdown
## INTERIM SOLO-FOUNDER OWNERSHIP

The following interim accountable roles are confirmed:

- Product Owner: Hossam
- Business Sponsor: Hossam
- Business Analysis Lead: Hossam
- Architecture Owner: Hossam
- QA Lead: Hossam
- Implementation Lead: Hossam

Hossam is also the interim accountable owner for coordinating each domain BRD until a specialist or domain owner is explicitly appointed. The absence of separate people or deputies does not block BRD authoring.

The following areas require external specialist validation before production approval; Hossam coordinates but does not substitute for the specialist conclusion:

- Finance and accounting
- Saudi VAT
- ZATCA e-invoicing
- PDPL and privacy
- Data residency and data retention
- Cybersecurity
- Penetration testing

System separation-of-duties requirements remain mandatory even while one founder holds several project roles. Approval, posting, payment, reversal, and privileged-access controls must not be weakened.

This ownership foundation is approved for the MVP BRD stage and the Task may move to Done.
```

### MESP-21 - Create BRD Workshop Plan

- **Action:** Simplify for solo-founder operation.
- **Owner:** Hossam.
- **Status:** In Progress until the comment below is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `solo-founder-governance`, `workshops-on-demand`.
- **Labels to remove:** `status-blocked`, `status-in-review`.
- **May move to Done:** Yes.
- **Must Hossam read the full issue:** No; consult only the relevant domain session when its BRD begins.
- **Exact comment to post:**

```markdown
## SOLO-FOUNDER WORKSHOP PLAN APPROVAL

Hossam approves the workshop plan as a reusable discovery checklist, not as a mandatory pre-scheduled 18-day program.

For the MVP BRD stage, Hossam may run focused working sessions inside the active owning BRD. Each session must still record inputs, evidence, decisions, open questions, Wafra observations as validation only, acceptance scenarios, and founder approval. External specialists join only when their conclusion is due; their production validation is not an entry condition for unrelated BRDs.

Run one BRD Task at a time in the approved sequence. Start only MESP-27 after MESP-26 approval. Do not start MESP-28 through MESP-40 yet.

This planning foundation is approved and the Task may move to Done.
```

### MESP-22 - Create Product Decision Register

- **Action:** Keep with a small correction; name Hossam and add the approved technology decision.
- **Owner:** Hossam.
- **Status:** In Progress until the immutable decision is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `architecture-approved`.
- **Labels to remove:** `status-in-review`, `status-blocked` if present.
- **May move to Done:** Yes after the decision entry is posted and linked to the architecture baseline. Future decisions remain tracked by MESP-41 through MESP-56 and the living register.
- **Must Hossam read the full issue:** No; review the immutable technology entry below.
- **Preflight:** Confirm `PD-019` is unused. If it is already used, allocate the next unused immutable `PD-NNN` and substitute that identifier without editing an earlier entry.
- **Exact comment to post:**

```markdown
## PD-019 - RELEASE 1 TECHNOLOGY ARCHITECTURE

**Owner:** Hossam

**Status:** Approved

**Approval date:** 1 August 2026

**Source:** `docs/01_Technology_Architecture_Baseline.md`

**Decision:** Release 1 uses Angular 22, TypeScript, ASP.NET Core Web API on .NET 10 LTS, Entity Framework Core 10, Microsoft SQL Server 2025, a Modular Monolith, REST and OpenAPI, ASP.NET Core Identity, secure HTTP-only cookies, policy and resource authorization, one shared SQL Server database with module-owned schemas and layered tenant isolation, private object storage, SQL-backed jobs, a transactional outbox/inbox, Docker Compose, OpenTelemetry-compatible observability, xUnit, and Playwright TypeScript.

**Explicitly rejected for Release 1:**

- Microservices
- Kubernetes
- Event sourcing
- Message brokers
- Multiple databases per tenant
- Browser-stored authentication tokens for the first-party Angular application
- Retail POS
- Unnecessary distributed infrastructure

**Rationale:** This stack satisfies the approved product baseline while remaining practical for one developer. It preserves explicit module ownership, tenant isolation, auditability, and later extraction seams without introducing premature distributed-system operations.

**Affected modules:** All Release 1 modules.

**Traceability:** PRD D-001, PLT-001, PLT-008, PLT-010, PLT-014, BR-001, BR-010, BR-011, BR-014, BR-016, RULE-001, RULE-002, RULE-014, RULE-016, RULE-018; MESP-19; `docs/01_Technology_Architecture_Baseline.md`.

This is an immutable Product Decision Register entry. Any change requires a new superseding PD-NNN record. It creates no implementation Story and does not authorize application coding.
```

### MESP-23 - Create Open Questions Register

- **Action:** Keep open during BRD and classify all decisions by timing.
- **Owner:** Hossam.
- **Status:** Keep In Progress.
- **Labels to add:** `living-register`, `decision-timing-classified`.
- **Labels to remove:** `status-in-review`, `status-blocked` if present.
- **May move to Done:** No. Close only after every question is approved, superseded, or explicitly deferred with evidence.
- **Must Hossam read the full issue:** No; review each question only when its timing category becomes due.
- **Exact comment to post:**

```markdown
## OPEN-DECISION TIMING CLASSIFICATION - APPROVED FOR BRD PLANNING

The register structure is accepted. Open questions are not all prerequisites to beginning the detailed BRD. The classifications below control when each answer becomes mandatory. Recommended defaults are working positions only and are not approved answers.

| Jira | Timing category | Owning domain / BRD | Recommended MVP default | Hossam approval | Wafra validation | External specialist validation | Risk if delayed past timing |
|---|---|---|---|---|---|---|---|
| MESP-41 | Must decide during owning domain BRD | Master Data/Inventory - MESP-31/MESP-33 | Configurable per product/category; disabled by default; enforce when enabled | Yes | Yes | If regulated goods enter scope | Tracking model remains ambiguous |
| MESP-42 | Must decide during owning domain BRD | Procurement - MESP-32 | PR required; quotation comparison optional; one configurable threshold; no self-approval | Yes | Yes | Finance/control before production | Approval states and permissions remain open |
| MESP-43 | Must decide during owning domain BRD | Procurement - MESP-32 | Manual informational confirmation; partials allowed; reviewed material changes | Yes | Yes | No | PO state and receipt readiness remain open |
| MESP-44 | Must decide during owning domain BRD | Procurement/Finance - MESP-32/MESP-34 | Three-way match, zero tolerance, manual exception | Yes | Yes | Finance/accounting | Liability and exception control remain open |
| MESP-45 | Must decide during owning domain BRD | Inventory - MESP-33 | Block negative stock | Yes | Yes | Finance/accounting | Valuation and close remain unsafe |
| MESP-46 | Must decide during owning domain BRD | B2B Sales - MESP-35 | Hard check at order confirmation with audited Finance override | Yes | Yes | Finance/accounting | Credit and AR controls remain incomplete |
| MESP-47 | Must decide during owning domain BRD | Finance - MESP-34 | Manual bank transfer and cash; partial/on-account allocation; no gateway/feed | Yes | Yes | Finance/accounting | Settlement and reconciliation remain incomplete |
| MESP-48 | Must decide before implementing affected module | Platform/NFR - gather in MESP-27 | Wafra measurements plus a conservative SME profile | Yes | Yes | Performance validation before production | No measurable performance or limit target |
| MESP-49 | Must decide before production | Saudi Compliance - MESP-37 | No production compliance claim until qualified ZATCA advice | Yes | Evidence only | Saudi VAT/ZATCA required | Live invoicing may be non-compliant |
| MESP-50 | Must decide before production | Privacy/Data Governance - MESP-38 | No contractual promise until qualified privacy/legal advice | Yes | Evidence only | PDPL/privacy/legal required | Hosting and tenant commitments may be invalid |
| MESP-51 | Must decide during owning domain BRD | Migration - MESP-40 | Masters plus reconciled stock/GL/AP/AR openings; no full history | Yes | Yes | Finance/accounting | Cutover and reconciliation remain undefined |
| MESP-52 | Must decide during owning domain BRD | Platform Administration - MESP-27 | One R1 plan, all approved modules, simple limits, no metered billing | Yes | No requirement authority | No | MESP-27 cannot finish entitlement rules |
| MESP-53 | Must decide during owning domain BRD | Reporting - MESP-36 | Minimum statutory/core operational set with named reconciliation owners | Yes | Yes | Finance/tax as applicable | Reporting and close evidence can sprawl |
| MESP-54 | Must decide during owning domain BRD | Finance - MESP-34 | Manual effective-dated approved rates; no automated feed | Yes | Yes | Finance/accounting | Multi-currency posting remains ambiguous |
| MESP-55 | Must decide during owning domain BRD | Identity and Access - MESP-28 | One approver, controlled manual reassignment, no self-approval; defer parallel/escalation automation | Yes | Yes | Security/control | Approval behavior may be inconsistent |
| MESP-56 | Must decide during owning domain BRD | Organization - MESP-30 with MESP-34 validation | One legal entity per tenant; multiple branches/warehouses; no intercompany automation or consolidation | Yes | Yes | Finance/accounting | Numbering, calendars, posting, and reporting scope remain open |

Category totals:

- Must approve before the first detailed BRD: 0
- Must decide during its owning domain BRD: 13
- Must decide before implementing the affected module: 1
- Must decide before production: 2
- Defer to post-MVP: 0 decisions; advanced options inside the decisions may be explicitly excluded from MVP

No recommended default is an approved answer. Close each Jira decision only after Hossam records the answer and all required validation evidence.
```

### MESP-24 - Define Wafra Discovery and Validation Approach

- **Action:** Keep as-is.
- **Owner:** Hossam.
- **Status:** In Progress until the comment below is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `wafra-validation-only`.
- **Labels to remove:** `status-in-review`, `status-blocked` if present.
- **May move to Done:** Yes.
- **Must Hossam read the full issue:** No; apply the classification control during each BRD.
- **Exact comment to post:**

```markdown
## WAFRA VALIDATION CONTROL APPROVAL

Hossam approves the Wafra discovery and validation control.

Wafra remains Tenant #1 and a source of evidence only. Wafra must not create tenant-specific core code, schemas, permissions, reports, workflows, or product rules. Observations must be classified as generic product need, tenant configuration, future scope, rejected preference, or open product decision before they affect a BRD.

This foundation Task may move to Done. No Wafra requirement has been invented or approved by this comment.
```

### MESP-25 - Confirm BRD Module Sequence

- **Action:** Keep with the authoritative v0.2 key correction.
- **Owner:** Hossam.
- **Status:** In Progress until the comment below is posted; then move to Done.
- **Labels to add:** `foundation-approved`, `sequence-approved`.
- **Labels to remove:** `status-in-review`, `status-blocked` if present.
- **May move to Done:** Yes.
- **Must Hossam read the full issue:** No; approve the sequence below.
- **Exact comment to post:**

```markdown
## BRD SEQUENCE APPROVAL

Hossam approves this detailed BRD sequence:

1. MESP-27 - SaaS Platform Administration
2. MESP-28 - Identity and Access
3. MESP-29 - Multi-Tenancy and Tenant Lifecycle
4. MESP-30 - Organization and Company Structure
5. MESP-31 - Master Data and Product Catalog
6. MESP-32 - Procurement and Purchase-to-Pay
7. MESP-33 - Inventory and Warehouse Management
8. MESP-34 - Finance and Accounting
9. MESP-35 - B2B Sales and Order-to-Cash
10. MESP-36 - Reporting and Analytics
11. MESP-37 - Saudi Localization and Compliance
12. MESP-38 - Security, Audit, and Data Governance
13. MESP-39 - Integrations and External Services
14. MESP-40 - Data Migration and Tenant Onboarding

MESP-34 is Finance and precedes MESP-35 B2B Sales. The retracted v0.1 reversed key mapping is not authoritative.

This sequence is a BRD order, not a development plan. Start only MESP-27 after MESP-26 approval. No implementation or Retail POS work is authorized.
```

## MESP-26 - Approve BRD Entry Criteria

- **Action:** Replace the former all-owners/all-decisions-first gate with the approved solo-founder gate.
- **Owner:** Hossam as accountable approver.
- **Status before approval:** To Do.
- **Status after approval:** Done.
- **Labels to add:** `entry-gate-approved`, `solo-founder-governance`.
- **Labels to remove:** `status-blocked`, `status-in-review` if present.
- **May move to Done:** Only after the Founder Decision Pack approval block is completed.
- **Exact approval comment to post:**

```markdown
# BRD ENTRY GATE - APPROVED

**Approved by:** Hossam

**Approval date:** 1 August 2026

**Operating model:** Solo-founder governance

Hossam approves the BRD Foundation and authorizes the first detailed BRD under the controls in `docs/90_MVP_Founder_Decision_Pack.md`.

Entry conditions met:

1. Hossam is the interim Product Owner, Business Sponsor, Business Analysis Lead, Architecture Owner, QA Lead, and Implementation Lead.
2. The PRD, working glossary, traceability model, decision register, open-question register, Wafra control, BRD sequence, and Approved Architecture Baseline are available.
3. MESP-41 through MESP-56 are classified by timing. None must be answered before MESP-27 begins.
4. External Finance, Saudi VAT, ZATCA, PDPL/privacy, residency/retention, cybersecurity, and penetration-test validation remains mandatory before production approval where applicable.
5. System separation-of-duties, approval, posting, payment, reversal, tenant-isolation, audit, and privileged-access controls remain mandatory.
6. Release 1 remains B2B ERP only. Retail POS remains excluded.

**Authorized first Task:** MESP-27 - Produce SaaS Platform Administration BRD.

**Not authorized:** MESP-28 through MESP-40, application code, implementation Stories, Bugs, Subtasks, API Tasks, database Tasks, UI Tasks, test-case Tasks, deployment Tasks, or Retail POS work.

This issue may now move to Done. Move only MESP-27 to In Progress.
```

## Domain BRDs

Do not start MESP-28 through MESP-40.

After MESP-26 is Done, apply only these operations to MESP-27:

- Keep owner/assignee Hossam.
- Keep existing labels `brd-deliverable`, `brd-seq-02`, and `phase-brd`.
- Add label `active-brd`.
- Transition MESP-27 from To Do to In Progress.
- Post the exact comment below.

```markdown
## DETAILED BRD STARTED

MESP-27 is the only active domain BRD. The BRD entry gate was approved in MESP-26 by Hossam.

Scope is SaaS Platform Administration business requirements only. Resolve MESP-52 during this BRD using the approved decision process. Gather MESP-48 volume evidence, but finalize the reference profile before affected implementation rather than blocking BRD authoring.

Do not introduce application design, API definitions, database design, UI specifications, source code, development Stories, or Retail POS scope. Do not start MESP-28 through MESP-40.
```

## Forbidden Jira actions

Do not create:

- Development Stories
- Bugs
- Subtasks
- Backend Tasks
- Frontend Tasks
- API endpoint Tasks
- Database Tasks
- UI Tasks
- Test-case Tasks
- Deployment Tasks
- Retail POS Tasks
- Additional governance Epics or Tasks

Do not edit or delete historical Product Decision Register entries. Do not mark a recommended default as approved without Hossam's explicit decision. Do not change application code.

## Verification after Jira execution

Confirm all of the following:

- MESP-17, MESP-18, MESP-19, MESP-20, MESP-21, MESP-22, MESP-24, MESP-25, and MESP-26 are Done with the specified approval comments.
- MESP-23 remains In Progress with all 16 timing classifications.
- MESP-27 is In Progress and is the only active domain BRD.
- MESP-28 through MESP-40 remain To Do.
- MESP-41 through MESP-56 remain open unless Hossam separately approved a specific answer with evidence.
- No new Jira issue was created.
- No Retail POS work was created.

## Current MESP-23 reconciliation - 10 August 2026

The Jira issue is the authoritative living register; this note is a traceability
pointer, not a second register. The current reconciliation is recorded in Jira
comment `10731`. The historical v0.1/v0.2 register comments remain preserved,
while comment `10055` remains the timing baseline and comment `10067` records
the later MESP-52/MESP-56 closure evidence.

- MESP-23 remains **In Progress** with the 16 Jira-decomposed entries
  OQ-001--OQ-016 for MESP-41--MESP-56. The canonical PRD v1.2 section 13.2
  contains 12 broader clarification prompts; the 16-entry count is the Jira
  decision-register decomposition, not a claim that the PRD has 16 separate
  paragraphs.
- MESP-41--MESP-51, MESP-53, MESP-54, and MESP-55 remain Open / To Do (14
  entries). MESP-52 is Done with PD-020 and MESP-56 is Done with PD-021.
- MESP-52 closure is evidenced by Jira comments `10065` and `10062`; MESP-56
  closure is evidenced by comments `10066` and `10063`. The glossary's
  Subscription/Plan/Entitlement and Company/Legal Entity baselines reflect
  those approvals; remaining decision-dependent terms remain open.
- MESP-48, MESP-49, and MESP-50 remain open external/performance/production
  gates. No recommended default, Wafra observation, assistant analysis, or
  source behavior is an approved answer. MESP-20 comment `10052` supplies the
  interim Hossam accountability baseline, but binding domain and qualified
  external validation remains required where stated.

## Copy-ready Claude Sonnet execution prompt

```text
Act as the Jira execution operator for the Mini ERP SaaS Platform.

Project: MESP at https://hossamsqa.atlassian.net
Repository: D:\AI Tools\Hossam\mini-erp-saas-platform

Before any Jira write:

1. Read docs/90_MVP_Founder_Decision_Pack.md completely.
2. Confirm its Hossam approval block is completed, says Approved to start detailed BRD: Yes, and approves MESP-27 as the first Task.
3. If the approval block is incomplete or says No, stop and report that Jira was not changed.
4. Read docs/91_Jira_Simplification_Update.md completely.

If approval is valid, apply docs/91_Jira_Simplification_Update.md exactly:

1. Update MESP-17 through MESP-26 with the specified comments, labels, owners, statuses, and transition rules.
2. Use Hossam as the interim solo-founder owner for the approved project roles.
3. Record the approved Release 1 technology decision in MESP-22 as immutable PD-019, or the next unused PD-NNN if PD-019 is already used.
4. Classify MESP-41 through MESP-56 by timing in MESP-23. Do not turn recommended defaults into approved decisions.
5. Approve and move MESP-26 to Done only because the Founder Decision Pack contains Hossam's completed approval.
6. Move only MESP-27 to In Progress and post the specified start comment.
7. Keep MESP-28 through MESP-40 in To Do.
8. Create no new Jira issues.

Forbidden: development Stories, Bugs, Subtasks, backend/frontend/API/database/UI/test/deployment Tasks, new governance issues, application code changes, Retail POS work, or historical decision edits.

After execution, report every changed issue, old and new status, labels added/removed, comment posted, the immutable PD identifier used, and verification that only MESP-27 was started.
```
