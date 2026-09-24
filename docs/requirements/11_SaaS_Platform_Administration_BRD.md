# Mini ERP SaaS Platform — SaaS Platform Administration BRD

## 1. Document control

| Field | Value |
|---|---|
| Document | SaaS Platform Administration Business Requirements Document |
| Jira | MESP-27 — Produce SaaS Platform Administration BRD |
| Version | v0.10 |
| Status | Approved |
| Approved by / date | Hossam / 1 August 2026 |
| Prepared | 1 August 2026 |
| Accountable owner | Hossam, interim Product Owner and Platform Operations owner |
| Canonical PRD | `docs/MESP_PRD_v1.2.docx` (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Mandatory vocabulary | `docs/00_ERP_Business_Glossary.md` |
| Architecture constraint | `docs/01_Technology_Architecture_Baseline.md`; Jira PD-019 |
| Approval authority | Hossam; external privacy, legal, Saudi compliance, security, and finance validation where stated |

Change control: this document may clarify approved product requirements but may not change PRD scope. Approved requirements and decisions are superseded through a dated revision and decision record, never silently edited. This BRD creates no implementation Story and authorizes no code.

## 2. Purpose

Define the business operating model by which the Platform Owner provisions, configures, governs, supports, suspends, reactivates, exports, retains, and ultimately purges a Tenant. The document makes platform operations testable while keeping commercial packaging simple enough for one developer and the first production cohort.

- **M27-REQ-001 (Must):** Platform administration must provide one controlled, auditable operating record for every Tenant from initial request through purge evidence.
- **M27-REQ-002 (Must):** Platform administration must remain reusable for Wafra and future tenants without tenant-specific code, workflow, schema, report, or permission branches.
- **M27-REQ-003 (Must):** No Tenant may enter Active until identity, organization, country pack, plan, entitlements, initial administrator, and required evidence pass the activation gate.

## 3. Scope

Release 1 scope includes:

- Platform Administrator roles, accountability, configuration ownership, and separation of duties.
- Tenant catalogue, provisioning, lifecycle, activation, grace, suspension, reactivation, export, termination, retention, legal hold, purge approval, and purge evidence.
- Plan, Subscription, Entitlement, module activation, limits, usage visibility, branding, country pack selection, notifications, audit, feature rollout, support access, reports, and exception recovery.
- Platform-level coordination with Identity and Access, Multi-Tenancy, Organization, Files, Reporting, Security and Audit, Integrations, and the Saudi Country Pack.

- **M27-REQ-004 (Must):** All in-scope actions must preserve the hierarchy Platform → Tenant → Company / Legal Entity → Branch → Warehouse.
- **M27-REQ-005 (Must):** Release 1 must support B2B ERP modules only: Procurement, Inventory, B2B Sales, Finance, Reporting, SaaS Administration, Identity, Organization, Audit, Files, and Saudi localization.

## 4. Out of scope

- Retail POS, cashier shifts, cash drawers, peripherals, retail checkout, loyalty, promotions, and offline store operation.
- Trial Tenants and a Trial lifecycle state. Release 1 permits no commercial or production Trial Tenant. The non-production Restricted Validation Plan defined in section 11 is test evidence only and is not a Trial offering.
- Automated subscription invoicing, payment collection, pricing calculation, tax on the Platform Owner's subscription sale, metered billing, overage billing, automated renewals, or tier automation.
- Microservices, Kubernetes, event sourcing, database-per-tenant, per-tenant schemas, or infrastructure design.
- Screen design, API design, database design, source code, implementation tasks, and detailed test scripts.
- Final legal conclusions for retention, residency, legal hold, privacy, or irreversible purge.
- Detailed Identity, Multi-Tenancy, Finance, Saudi Country Pack, Reporting, Migration, or domain-module BRDs.

- **M27-REQ-006 (Must):** Any POS request must be rejected as unavailable in Release 1 and routed to formal product change control.

## 5. Source requirements and traceability

| Source | BRD interpretation |
|---|---|
| PLT-001; RULE-001; RULE-016; BR-010 | Tenant scope is server-established, deny-by-default, and applies to data, files, jobs, reports, exports, search, and audit. |
| PLT-002 | The governed hierarchy is Platform → Tenant → Company / Legal Entity → Branch → Warehouse; multiple legal entities are supported without consolidation. |
| PLT-007 | Assignments, approvals, failures, material exceptions, and lifecycle actions produce governed notifications with visible delivery state. |
| PLT-008; BR-011 | Material platform, privileged, configuration, entitlement, lifecycle, export, and purge actions require immutable audit evidence. |
| PLT-009 | Authorized platform registers are searchable and bounded exports are asynchronous, stateful, expiring, and audited. |
| PLT-010 | Provisioning, export, notification, job, and integration commands use a stable request identity and safe retry without duplicate authoritative effects. |
| PLT-011; BR-001 | A Platform Administrator can provision and operate tenants from approved configuration without code change. |
| PLT-012; BR-004 | Plans, Subscriptions, Entitlements, limits, service tier, and effective dates remain distinct and auditable. |
| PLT-013 | Tenant identity, branding, structure, templates, and numbering are governed configuration. |
| PLT-014; BR-003; RULE-002 | Wafra is Tenant #1 and never a product fork. |
| RPT-003 | Reports and usage views expose their data-as-of time, freshness, and whether data is current or asynchronously prepared. |
| ADM-001 | Platform and Tenant role administration is scope-bound, high-risk rights are identifiable, and material assignments are audited. |
| ADM-002 | Plan, Subscription, Entitlement, limit, lifecycle, branding, template, numbering, and other material configuration history is versioned or effective-dated. |
| ADM-003 | Administrative imports require templates, validation preview, row-level errors, duplicate controls, resumable behavior where needed, and an audit summary. |
| BR-015 | Country and industry expansion uses reusable configuration and controlled extensions. |
| BR-016 | Availability, recovery, localization, accessibility, scale, and observability must be measurable after MESP-48 evidence. |
| PRD §17 | Plan catalogue, lifecycle, entitlements, modules, quotas, branding, support access, feature rollout, and offboarding are the SaaS operations baseline. |
| MESP-27 | This BRD's approved business scope and required outputs. |
| MESP-48 | Reference volumes remain open; Wafra evidence is gathered now and thresholds approved before affected implementation. |
| MESP-52 | Plan, module, limit, Trial-exclusion, and Entitlement-governance decision approved by Hossam and incorporated in this BRD. |
| PD-019 | Approved technology architecture constrains feasibility but does not define new business scope. |

Traceability rule: every requirement, business rule, decision, open question, risk, and acceptance scenario in this document carries a stable identifier or explicit source link.

## 6. Definitions

| Term | Controlled business meaning |
|---|---|
| Platform | The single multi-tenant Mini ERP SaaS service operated by the Platform Owner. |
| Tenant | The isolated customer subscription boundary owning its users, configuration, data, files, audit, and reports. |
| Plan | A named, reusable commercial package defining available modules, features, limits, service/support tier, non-calculating price metadata, and effective dates. |
| Subscription | The effective-dated commercial agreement linking one Tenant to one Plan for a period. It is not tenant ERP sales data. |
| Entitlement | The Tenant-wide right to use a module, feature, or capacity, derived from the Subscription and Plan. It is not a user Permission. |
| Permission | A User-level security right. A permitted user is still denied when the Tenant lacks the relevant Entitlement. |
| Feature flag | A temporary operational rollout control for a capability already allowed by product scope and Entitlement. It is not a Plan or Permission. |
| Usage measurement | A recorded quantity used for visibility, warning, capacity control, or later commercial analysis. It is not a charge. |
| Commercial limit | A Plan-derived allowed capacity. Release 1 values are configurable and not billable overage measures. |
| Operational safety limit | A protective ceiling applied to preserve service, security, or data integrity; it may be stricter temporarily and is not a commercial term. |
| Country Pack | Reusable country-specific configuration, rules, documents, terminology, and compliance controls; never tenant customization. |
| Support access | Explicit, time-bound, least-privilege access by an authorized support person, with approval and full audit. |
| Legal hold | A documented prohibition on deletion or purge due to legal, contractual, investigation, or dispute need. |

The glossary remains authoritative. Any terminology conflict must be resolved there before this BRD is approved.

The **closed list of Platform-owned metadata** is: Plan catalogue and versions; non-production validation-plan designation; Tenant catalogue and lifecycle summary; Subscription and Entitlement records; country-pack catalogue/version references; provisioning and activation records; Platform limit definitions and cross-Tenant usage summaries; feature-rollout records; support-authorization metadata; Platform notification-delivery metadata; and audit, export, retention, legal-hold, purge, and operational-job metadata. Tenant business records, master data, transactions, files, document content, Role/Permission assignments, branding content, and organization configuration are not Platform-owned merely because the Platform hosts them. Cross-Tenant Platform registers are available only to specifically authorized Platform actors and are denied to every Tenant actor; a Tenant actor may receive only an authorized Tenant-scoped view or report.

## 7. Actors and responsibilities

| Actor | Responsibilities | Prohibited or constrained actions |
|---|---|---|
| Product Owner (Hossam) | Approves product decisions, MESP-52, scope changes, and founder review. | Cannot approve irreversible production purge alone. |
| Platform Administrator | Maintains tenant catalogue, starts provisioning, assigns approved Plan, controls lifecycle, coordinates support and export. | Cannot see Tenant business data by default; cannot grant self-approval; cannot bypass Entitlements or audit. |
| Platform Operations Owner | Owns provisioning recovery, service limits, notifications, jobs, evidence, and operational readiness. | Temporary operational-safety restrictions are block-only and require reason, expiry, and review; they cannot grant an Entitlement. |
| Commercial Owner | Owns Subscription agreement, effective dates, and approved commercial limits. | Cannot grant user Permissions or access tenant data. |
| Security/Privacy Owner | Reviews privileged access, legal hold, export, retention, and purge. | Must not be the sole requester and approver of purge. |
| Tenant Administrator | Confirms tenant identity, creates organization structure/users, assigns Roles, and approves support access where policy requires. | Cannot alter Plan, commercial limits, platform audit, or platform lifecycle. |
| Authorized Support User | Investigates a named case within approved scope and time. | No shared account, hidden superuser, unrestricted impersonation, or access after expiry. |
| Auditor | Reviews configuration, lifecycle, access, export, and purge evidence. | Read-only; no operational mutation. |
| Background Operator | Executes approved asynchronous platform work and records outcome. | May not infer a wider Tenant or action scope than the initiating record. |

- **M27-REQ-007 (Must):** Every lifecycle, Entitlement, support, export, and purge decision must identify the accountable actor, approver where required, reason, effective time, and evidence.
- **M27-REQ-008 (Must):** Platform and Tenant Administrator responsibilities must remain distinct; neither role automatically grants the other.

## 8. Business assumptions

1. Hossam temporarily holds multiple founder roles; system controls and audit still enforce separation where business risk requires it.
2. Wafra supplies evidence and validates workflows but does not own product requirements.
3. Saudi Arabia is the first country pack; SAR, Asia/Riyadh, Arabic, and English are initial defaults, subject to company-level rules in later BRDs.
4. Release 1 has one approved production Plan and no billing automation. Trial Tenants are excluded.
5. Reference volumes and production thresholds are unknown until MESP-48 evidence and performance validation.
6. Retention and purge periods remain unresolved under MESP-50 and external specialist review.
7. Email delivery may be unavailable; in-app or operational evidence must still exist.
8. A Tenant may contain multiple legal entities. Each owns its legal and accounting boundary; financial consolidation, intercompany automation, elimination entries, transfer pricing, and consolidated statements are excluded from Release 1. MESP-30 owns detailed operating rules.

- **M27-REQ-009 (Must):** Unknown volume, retention, or legal values must be represented as governed open configuration or decision, never invented as a production default.

## 9. Business processes

| Process | Trigger | Main outcome | Owner | Evidence |
|---|---|---|---|---|
| Tenant onboarding | Approved customer onboarding request | Tenant reaches Ready for Activation | Platform Administrator | Request, validation, provisioning run, handover checklist |
| Activation | Tenant setup and readiness confirmed | Tenant becomes Active | Platform Administrator with Tenant Administrator acknowledgement | Activation decision and readiness snapshot |
| Subscription change | Approved commercial change | Future or current Entitlements recalculated | Commercial Owner | Effective-dated change record |
| Limit management | Warning, approved Plan change, or safety event | Limit is maintained without hidden billing | Platform Operations | Usage snapshot, decision, notice |
| Support session | Valid support case and authorization | Time-bound access granted then closed | Security/Support owner | Case, scope, consent, access events, outcome |
| Suspension/reactivation | Commercial, security, contractual, or administrative decision | Access restricted or safely restored | Authorized Platform Administrator | Reason, approvals, notice, state transition |
| Feature rollout | Approved product capability ready for controlled release | Cohort-enabled or rolled back | Product/Operations owner | Flag owner, cohort, start, expiry, outcome |
| Offboarding | Tenant requests exit or contract terminates | Export, closure, retention, and later purge | Platform Administrator plus Security/Privacy | Export manifest, termination and purge evidence |

- **M27-REQ-010 (Must):** Every process must expose its current state, next permitted actions, blockers, responsible owner, and last material evidence.
- **M27-REQ-011 (Must):** Failed platform processes must support safe retry from recorded state without creating duplicate Tenants, Entitlements, invitations, exports, or purge actions.

## 10. Tenant lifecycle

Canonical sequence:

`Draft → Provisioning → Configuration Required → Ready for Activation → Active → Grace Period → Suspended → Reactivated → Export Requested → Termination Pending → Terminated → Retained → Purge Approved → Purged`

Trial is intentionally absent because Hossam has approved Trial Tenants as out of scope for Release 1. Reactivated is an explicit evidence state and must return to Active after restoration checks; it is not a permanent operating state. Some controlled transitions may skip Grace Period for security or legal suspension, but no state may skip required evidence.

| State | Meaning and entry | Allowed / prohibited actions | Exit, actor, and evidence | Data access, Entitlement, audit, and notice effects |
|---|---|---|---|---|
| Draft | Onboarding request exists; no operational Tenant. Entered by Platform Administrator after commercial authority. | Edit request; validate identity; cancel. No sign-in, data import, module use, or invitations. | Provisioning after completeness/duplicate checks; or cancelled request retained. Evidence: approved request. | No Tenant data access or effective Entitlements. Creation/change/cancellation audited; internal notice only. |
| Provisioning | Controlled creation is running. | Observe, stop safely, retry failed steps. No tenant use or manual bypass. | Configuration Required on successful foundation; Draft/failed recovery on cleanup. Evidence: unique run and step results. | Platform operators only; Entitlements recorded but inactive. Run, retry, cleanup, and failures audited; operator notified. |
| Configuration Required | Foundation exists; mandatory business configuration is incomplete. | Tenant Administrator invitation/setup, country pack, company, roles, modules, numbering, branding. No production transactions. | Ready for Activation after checklist passes. Evidence: completed configuration snapshot. | Limited setup access only; enabled modules may be configured but not operated. Changes and invitations audited; owner receives checklist notices. |
| Ready for Activation | All mandatory setup and validation passed. | Review, correct via controlled return, activate. No production posting. | Active by authorized Platform Administrator after Tenant Administrator acknowledgement. Evidence: readiness and acceptance. | Read/setup access; Entitlements staged. Activation decision and acknowledgment audited; both parties notified. |
| Active | Tenant may operate entitled modules subject to Permissions and limits. | Normal B2B ERP operation, configuration, exports, support request, plan change. | Grace Period, Suspended, or Export Requested according to authorized trigger. | Normal authorized access. Entitlements effective. Material actions audited; operational notices apply. |
| Grace Period | Time-bounded commercial/administrative remedy window. | Existing authorized users may continue approved functions; warnings shown; new high-cost activation may be restricted by policy. No silent data restriction. | Active if resolved; Suspended at documented expiry. Evidence: trigger, deadline, notices. | Entitlements remain effective unless an explicit risk control says otherwise. State and notices audited; Tenant Administrator warned at entry and milestones. |
| Suspended | Access is restricted for a recorded commercial, security, legal, or administrative reason. | Platform operations, approved export/support, evidence review, and remediation. Tenant mutation, posting, jobs, integrations, and new sessions prohibited unless suspension policy explicitly allows read-only access. | Reactivated after reason cleared and restoration checklist passes; or Export Requested. | Data retained and isolated. Entitlements remain recorded but unusable. All enforcement, attempts, exceptions, and notices audited. |
| Reactivated | Restoration has been authorized and is being verified. | Restore sessions/jobs/integrations in controlled order; validate entitlement and access. No assumption that interrupted work completed. | Active after checks; Suspended if checks fail. Evidence: clearance and restoration results. | Access restored only after validation; Entitlements reevaluated at effective time. Transition and outcomes audited; admins notified. |
| Export Requested | Authorized offboarding or portability request accepted. | Generate bounded authorized export, verify manifest, retry safely. No purge. Normal operation follows prior state until explicitly changed. | Termination Pending after accepted export or documented waiver; may return to prior state if request cancelled before termination. | Access determined by prior/explicit state; export permission independent. Export scope, generator, downloads, expiry, and notices audited. |
| Termination Pending | Contractual closure approved; final controls underway. | Final export, access schedule, integration closure, legal-hold check, termination approval. No new module activation or commercial expansion. | Terminated at effective date; may return to Active only through approved reversal before termination. | Access progressively restricted by schedule; Entitlements end at effective date. All decisions and notices audited. |
| Terminated | Tenant operation has ended. | Platform-only retention, approved evidence retrieval, support, and export if contract/policy permits. No tenant business mutation or sign-in. | Retained after termination completion is recorded. Evidence: closure checklist. | Entitlements expired; data remains isolated and immutable except governed retention administration. Tenant and owners notified; transition audited. |
| Retained | Data is held under approved retention or legal hold. | Authorized retention review, hold management, evidence retrieval. No operational reuse, sign-in, module use, or new exports unless policy expressly permits. | Purge Approved only after all holds expire and dual approval exists. | No Entitlements or tenant access. Every read, hold, and decision audited; review notices sent to owners. |
| Purge Approved | Purge has passed legal, contractual, retention, backup, evidence, and dual-control checks for a certified scope. | Observe the mandatory cooling-off interval; issue final notice; recheck legal hold and every precondition; revoke approval before execution if needed. Execution before the interval and final notice is prohibited. | Purge execution begins only after the MESP-50-controlled interval expires and final notice is evidenced; return to Retained if approval is revoked before execution. | No tenant access or Entitlements. Approval, certified scope, exclusions, residual copies, notices, interval, start, errors, and completion are audited. |
| Purged | Data and files inside the certified purge scope are verified removed according to policy; residual backups or retained copies outside that scope are disclosed and governed. | View the purge certificate and permitted minimal evidence. No operational reuse, sign-in, or export from the certified purge scope. | Terminal for the certified purge scope. | No Tenant access or Entitlements. The certificate states whether restoration remains possible from residual copies; it must not claim universal impossibility without evidence that all copies were removed. |

- **M27-REQ-012 (Must):** Only defined transitions may occur; each transition must validate actor, reason, prerequisites, effective time, evidence, notification, and resulting access.
- **M27-REQ-013 (Must):** A security or legal suspension may move Active directly to Suspended, but must record why Grace Period was bypassed.
- **M27-REQ-014 (Must):** Data must never be deleted merely because access, Subscription, Entitlement, or a module is disabled.
- **M27-REQ-015 (Must):** Purged is terminal for the certified purge scope. The Platform must not promise restoration from that scope, and must not claim restoration is universally impossible unless all residual backups and retained copies are demonstrably removed.

## 11. Plan and subscription model

### Approved MESP-52 Release 1 decision

1. One named production Release 1 Plan contains all approved B2B ERP modules.
2. Retail POS is absent and cannot be assigned.
3. Trial Tenants are excluded.
4. The Plan carries simple configurable limits, a service/support tier, non-calculating price metadata, and effective dates, but no automated pricing engine, metered billing, overage charge, automatic subscription invoice, payment, or accounting transaction.
5. An authorized Platform Administrator manually assigns the Plan to a Tenant.
6. Per-Tenant Entitlement override is prohibited. Entitlement changes use a versioned Plan change or an effective-dated Subscription change. A security/safety restriction may temporarily block access but may never grant an unapproved Entitlement.
7. One **Restricted Validation Plan** exists only in non-production, deliberately omits at least one module/capacity, cannot be sold, cannot be assigned to a production Tenant, and cannot create a Trial Tenant. It exists solely to validate Entitlement-denial and transition evidence.
8. Plan versions and Subscription changes are effective-dated, reasoned, and audited; historical Entitlements remain reproducible.

| Concept | Release 1 treatment |
|---|---|
| Plan | Reusable versioned package; one production R1 Plan plus one non-production Restricted Validation Plan. Records service/support tier, price metadata, and effective dates. |
| Subscription | One effective-dated Tenant-to-Plan agreement at a time; future changes may be scheduled. |
| Entitlement | Evaluated right created from the effective Subscription and Plan. |
| Permission | Separately granted user security right; never generated by Plan assignment. |
| Feature flag | Temporary rollout switch inside approved scope and Entitlement. |
| Usage measurement | Informational/control count; not a billable event. |
| Commercial limit | Configured Plan capacity; a Tenant-specific change requires a versioned Plan or effective-dated Subscription change, not an Entitlement override. |
| Operational safety limit | Protective operational ceiling with owner, reason, review, and expiry. |

- **M27-REQ-016 (Must):** A Plan version must have a unique identity, name, status, effective interval, included modules/features, limit definitions, service/support tier, non-calculating price metadata, environment eligibility, owner, and approval record. Price metadata creates no charge, payment, subscription invoice, or accounting transaction.
- **M27-REQ-017 (Must):** A Subscription must record Tenant, Plan version, status, start/end/effective times, assignment reason, assigner, and change history.
- **M27-REQ-018 (Must):** A scheduled Plan change must not alter current Entitlements before its effective time and must not rewrite historical access evidence.
- **M27-REQ-019 (Must):** Release 1 must not calculate subscription charges or infer payment status from Tenant ERP finance data.

## 12. Entitlement model

Access to a capability is allowed only when product scope, Tenant lifecycle, effective Subscription, Entitlement, user Permission, organizational scope, and business-state controls all allow it.

- **M27-REQ-020 (Must):** Entitlements must be evaluated for module use, feature use, capacity creation, background work, integration, export, and privileged support actions where applicable.
- **M27-REQ-021 (Must):** Entitlement denial must not be overridden by a user Permission, Role, hidden interface action, import, background job, or integration.
- **M27-REQ-022 (Must):** Entitlement alone must never grant any user access; Tenant Membership, Permission, scope, and context remain mandatory.
- **M27-REQ-023 (Must):** Every Entitlement must expose its source Plan version, Subscription, effective interval, status, any temporary security/safety restriction, and audit history. Per-Tenant Entitlement override is prohibited.
- **M27-REQ-024 (Must):** Expired or revoked Entitlement must block new actions while preserving lawful read/export access according to lifecycle and offboarding policy; it must never delete data.
- **M27-REQ-092 (Must):** An Entitlement may change only through a versioned Plan change or an effective-dated Subscription change. A security/safety restriction may block an otherwise valid Entitlement for a controlled interval but cannot grant a capability absent from the effective Plan and Subscription.

## 13. Tenant provisioning

Required inputs:

- Tenant code, legal/customer identity, Arabic and English display names, business contacts, country, and contractual reference.
- Contracted hosting region, permitted cross-border support-access terms, and subprocessor consent or restrictions. Final allowed values and production enforcement remain open under MESP-50.
- Country Pack, default language, supported languages, time zone, and initial currency context.
- Initial Company / Legal Entity identity and regulatory fields required by the selected Country Pack.
- Initial Tenant Administrator identity and verified invitation channel.
- Plan version, Subscription dates, modules, Entitlements, commercial limits, operational safety profile, and branding.
- Required document-template profile, numbering profile, their governed versions/scopes, and onboarding owner.

Provisioning stages: request → duplicate/completeness validation → foundation creation → country/Plan application → initial company/admin → module preparation → validation → handover. Each stage records Pending, Running, Succeeded, Failed, Skipped with reason, or Compensated.

- **M27-REQ-025 (Must):** Tenant code and legal/customer identity must be unique under approved duplicate rules; duplicate detection must occur before authoritative creation.
- **M27-REQ-026 (Must):** Provisioning must use one idempotent request identity so a retry returns or continues the original outcome instead of creating a second Tenant.
- **M27-REQ-027 (Must):** A failed run must identify the failed stage, completed stages, safe retry point, cleanup/compensation status, owner, and user-safe message.
- **M27-REQ-028 (Must):** Partial provisioning must not produce an Active Tenant, usable invitation, or operational module.
- **M27-REQ-029 (Must):** Activation evidence must include input snapshot, validation results, provisioning results, country/Plan versions, module readiness, initial administrator acceptance, and approving actor.
- **M27-REQ-030 (Must):** Country Pack application must be versioned and reusable; Wafra-specific values remain Tenant configuration, not Country Pack behavior.
- **M27-REQ-093 (Must):** Provisioning must capture and validate contracted hosting region, cross-border support-access permission, and subprocessor consent/restrictions. A production Tenant cannot be activated against an unresolved or contradictory MESP-50 control.

## 14. Module activation

Module states are Not Entitled, Entitled / Not Configured, Configuration Required, Ready, Active, Read-Only, Deactivation Pending, and Inactive.

- **M27-REQ-031 (Must):** A module may become Active only when the Tenant is Active, the Entitlement is effective, dependencies are Active/Ready, required configuration and opening evidence are complete, a documented rollback plan is approved, and an authorized actor approves activation.
- **M27-REQ-032 (Must):** Dependency failure must identify the blocking module/configuration and prevent partial business use.
- **M27-REQ-033 (Must):** Deactivation with existing data must preserve records, reports, audit, and required read/export access; destructive deletion is prohibited.
- **M27-REQ-034 (Must):** A module with open processes, pending jobs, legal/reporting obligations, or downstream dependencies must enter Deactivation Pending or Read-Only until a reviewed closure plan is complete.
- **M27-REQ-035 (Must):** Re-enablement must reevaluate current Entitlement, dependencies, configuration versions, data consistency, permissions, jobs, and missed notifications before new transactions are allowed.

## 15. Limits and usage

Configurable measurement categories for MESP-48 evidence and later approved profiles:

| Category | Candidate measures | Release 1 behavior before approval |
|---|---|---|
| People/organization | Active users, companies, branches, warehouses | Measure and report; no invented production threshold. |
| Master data | Products, suppliers, Business Customers | Measure growth; configurable warning/hard control only after approval. |
| Transactions | Documents, document lines, monthly transactions | Measure by type and period; preserve incomplete/failed behavior. |
| Files | File count, individual size, total storage | Safety controls may protect service; no billing. |
| Work | Imports, exports, reports, background jobs | Queue/concurrency/bounded-work controls with visible state. |
| Integrations | API operations and integrations | Measure only for security/operations; no public commercial promise until approved. |
| Concurrency | Active sessions and concurrent work | Operational safety control, validated under MESP-48. |

- **M27-REQ-036 (Must):** Every limit definition must state measure, scope, period, warning point, hard behavior, owner, source (commercial or safety), effective time, and review date.
- **M27-REQ-037 (Must):** A warning threshold must notify authorized administrators without blocking the triggering action unless the hard limit is also reached.
- **M27-REQ-038 (Must):** A hard limit must deny only the capacity-increasing action, preserve existing data and lawful read access, explain the limit, and provide an escalation path.
- **M27-REQ-039 (Must):** Safety limits may temporarily restrict work to protect service, but require reason, accountable owner, tenant impact, notice, review, and expiry or renewal.
- **M27-REQ-040 (Must):** Usage values must show measurement time, scope, unit, freshness, exclusions, and reconciliation status; they must not be presented as invoiceable amounts.
- **M27-REQ-041 (Must):** No production threshold, capacity promise, or supported-volume claim is approved until MESP-48 records Wafra evidence, a conservative SME profile, and validation results.

## 16. Branding

Allowed branding/configuration: Arabic/English display names, approved logo, contact details, document identity, governed document-template profiles, governed numbering profiles, and colors within accessible governed choices. Branding cannot alter behavior, authorization, tax meaning, legal evidence, audit, error severity, or Platform ownership disclosures.

- **M27-REQ-042 (Must):** Branding changes require authorized Tenant administration, file/type/size validation, accessible contrast, preview, effective time, and audit.
- **M27-REQ-043 (Must):** Authentication and security-critical experiences must retain trustworthy Platform identity and may not be disguised as another service.
- **M27-REQ-044 (Must):** Missing or rejected Tenant branding must fall back safely to approved Platform defaults in Arabic and English.
- **M27-REQ-094 (Must):** A document-template or numbering profile must identify owner, Tenant/Company/document scope, Country Pack compatibility, version, validation/preview evidence, effective interval, and change history. Historical documents retain the template/number interpretation used; issued numbers are never silently changed or reused.

## 17. Support access

Support access lifecycle: Requested → Authorized → Scheduled/Active → Expired/Revoked → Reviewed/Closed.

Minimum request: support case, Tenant, named user, business purpose, requested scope, data sensitivity, start/end, Tenant authorization where required, Platform approval, and recording/notification requirements.

- **M27-REQ-045 (Must):** Support access must use a named personal identity, least privilege, one Tenant, one case, explicit scope, and a maximum approved interval.
- **M27-REQ-046 (Must):** There must be no hidden superuser, shared support credential, unaudited impersonation, or default cross-tenant access.
- **M27-REQ-047 (Must):** Access must expire automatically, be revocable immediately, and require fresh authorization for extension or a different Tenant/purpose.
- **M27-REQ-048 (Must):** Support evidence must record approvers, access grants, authentication, records/actions accessed, changes attempted/completed, exports/downloads, revocation, and case outcome without logging secrets.
- **M27-REQ-049 (Must):** Emergency access, if later approved, must be separately governed, time-bounded, reviewed after use, and must not weaken Tenant isolation.
- **M27-REQ-095 (Must):** A support identity is prohibited from requesting, generating, or downloading a Tenant export under support authorization alone. Each export requires separate export Permission, separate export authorization, and explicit Tenant authorization for that named export scope and artifact.

## 18. Feature rollout

- **M27-REQ-050 (Must):** A feature flag must identify the approved capability, owner, eligible Entitlement, environment, Tenant/cohort, start, success criteria, rollback trigger, expiry, and removal decision.
- **M27-REQ-051 (Must):** A flag cannot introduce unapproved product scope, grant Permission, bypass Entitlement, alter posted history, or enable POS.
- **M27-REQ-052 (Must):** Rollout and rollback must preserve data interpretation and must record which Tenants and transactions experienced each flag state.
- **M27-REQ-053 (Must):** Expired flags must be reviewed and removed or deliberately renewed; permanent commercial variation belongs in the Plan/Entitlement model.

## 19. Suspension and reactivation

| Suspension type | Trigger authority | Default Tenant effect | Restoration evidence |
|---|---|---|---|
| Commercial | Approved contract/non-payment decision | Grace Period where contract allows, then block new sessions and mutation; retain data. | Commercial clearance and effective Subscription. |
| Security | Confirmed or credible risk | Immediate session revocation; stop risky jobs/integrations; read access only if Security approves. | Risk contained, credentials/sessions addressed, Security approval. |
| Legal/regulatory | Authorized legal/compliance instruction | Restrict access/actions exactly as instructed; preserve hold/evidence. | Written clearance from the governing owner. |
| Administrative | Tenant request or onboarding/control failure | Scope-specific restriction with clear notice and remedy. | Request reversal or corrected control evidence. |
| Operational safety | Service-integrity risk | Restrict the affected workload, not unrelated business access where avoidable. | Capacity/recovery validation and Operations approval. |

- **M27-REQ-054 (Must):** Suspension must record type, scope, reason, authority, effective time, grace decision, access mode, job/integration behavior, notification, review date, and reactivation criteria.
- **M27-REQ-055 (Must):** Suspension must stop prohibited interactive and non-interactive actions consistently, including background jobs, imports, integrations, exports, and existing sessions.
- **M27-REQ-056 (Must):** Read-only access during suspension is an explicit policy result, never an assumption, and remains subject to Permission and Tenant isolation.
- **M27-REQ-057 (Must):** Reactivation must not replay, duplicate, or silently discard work interrupted during suspension; pending work requires review and deliberate restart.
- **M27-REQ-058 (Must):** Reactivation must close or supersede the suspension reason and notify Tenant and Platform owners of restored and still-restricted capabilities.

## 20. Offboarding

Offboarding sequence: authorized request → scope and identity validation → export → acceptance/waiver → termination schedule → access/integration closure → retention/legal hold → purge approval → purge execution → certificate.

- **M27-REQ-059 (Must):** Export must identify scope, formats, identifiers/relationships preserved, exclusions, data-as-of time, requester, approver, generated artifacts, checksums or equivalent integrity evidence, expiry, and downloads.
- **M27-REQ-060 (Must):** Termination must not proceed without export disposition, open support/security matters, legal-hold check, Subscription end, access closure plan, and responsible approver.
- **M27-REQ-061 (Must):** Retained data must be inaccessible for ordinary Tenant operation and reviewed at approved intervals, with every exceptional read audited.
- **M27-REQ-062 (Must):** Purge requires dual control, verified retention expiry, no legal hold, exact Tenant/certified-scope confirmation, backup treatment, failure recovery plan, a MESP-50-controlled cooling-off interval, final notice, and an irreversible-action warning. Execution before the interval and notice are complete is prohibited.
- **M27-REQ-063 (Must):** A purge failure must stop safely, preserve evidence of completed and incomplete scope, notify owners, and require reviewed recovery; it must never be reported complete prematurely.
- **M27-REQ-064 (Must):** Production retention periods, legal-hold authority, purge scope, backup deletion, and residual evidence are unresolved under MESP-50 and require qualified external validation before production.
- **M27-REQ-096 (Must):** The purge certificate must state the certified purge scope; systems/data included and excluded; residual backups or retained copies; legal-hold or retention restrictions; and whether restoration remains possible outside the certified scope.

## 21. Business rules

| Rule | Binding statement |
|---|---|
| M27-RULE-001 | Tenant context comes from authenticated, trusted Platform context; a supplied Tenant identifier can never expand access. |
| M27-RULE-002 | Every Tenant-owned record, file, job, report, export, notification, audit event, and support action belongs to exactly one Tenant unless it falls within the closed Platform-owned metadata list in section 6. Cross-Tenant Platform registers are denied to Tenant actors. |
| M27-RULE-003 | Wafra is configured as Tenant #1; no Wafra name, volume, workflow, role, report, limit, or exception becomes reusable product behavior without an approved product decision. |
| M27-RULE-004 | A Plan version is immutable after use; a change creates a new effective-dated version. |
| M27-RULE-005 | Plan and Subscription changes apply only at their approved effective time and never rewrite historical Entitlements or audit. |
| M27-RULE-006 | Entitlement is Tenant-wide commercial availability; Permission is User-level authorization. Both, plus lifecycle and scope controls, must allow an action. |
| M27-RULE-007 | A Permission cannot override missing Entitlement; an Entitlement cannot grant a User Permission; and per-Tenant Entitlement override is prohibited. Entitlement changes require a versioned Plan or effective-dated Subscription change. |
| M27-RULE-008 | A module cannot activate until its Entitlement, dependencies, configuration, opening evidence, rollback plan, and activation approval are valid. |
| M27-RULE-009 | Module deactivation never deletes business data and cannot bypass open-process, reporting, retention, or dependency obligations. |
| M27-RULE-010 | Tenant activation requires a completed readiness checklist and named Platform and Tenant acknowledgements. |
| M27-RULE-011 | Grace Period has a recorded start, deadline, reason, permitted access, notices, and terminal action. |
| M27-RULE-012 | Suspension restricts interactive and non-interactive work consistently and preserves Tenant data isolation and retention. |
| M27-RULE-013 | Reactivation reevaluates current Subscription, Entitlements, Permissions, dependencies, sessions, jobs, integrations, and interrupted work. |
| M27-RULE-014 | A limit increase or exception requires an authorized owner, reason, effective interval, and audit; no exception is permanent by omission. |
| M27-RULE-015 | Commercial limits and operational safety limits are named and reported separately; neither creates a charge in Release 1. |
| M27-RULE-016 | Platform-owned configuration is changed only by Platform authority; Tenant-owned configuration is changed only by an authorized Tenant actor within governed options. |
| M27-RULE-017 | Support access is named, case-bound, Tenant-bound, purpose-bound, least-privilege, time-bound, revocable, and fully audited. |
| M27-RULE-018 | There is no shared, hidden, standing, or unaudited support superuser. |
| M27-RULE-019 | Feature flags control rollout only; they cannot redefine Plan, Subscription, Entitlement, Permission, product scope, or posted history. |
| M27-RULE-020 | Every feature flag has an owner, eligible cohort, success/rollback criteria, expiry, and review outcome. |
| M27-RULE-021 | Tenant export requires separate authorization at request, generation, and download; an expired artifact cannot be downloaded. Support authorization never supplies export authority, and a support identity also requires explicit Tenant authorization for the named export. |
| M27-RULE-022 | Termination ends operational access and Entitlements but does not itself delete retained data. |
| M27-RULE-023 | Legal hold blocks purge regardless of Subscription, termination date, retention schedule, or commercial request. |
| M27-RULE-024 | Purge is dual-controlled, certified-scope, evidence-producing, and permitted only after MESP-50 controls, cooling-off interval, and final notice are satisfied. The certificate distinguishes removed scope from residual copies and does not make unsupported restoration claims. |
| M27-RULE-025 | A failed provisioning request is retried using the same request identity; the same authoritative request cannot create two Tenants. |
| M27-RULE-026 | Partial provisioning is isolated from operational use and is either safely completed or compensated with evidence. |
| M27-RULE-027 | Country Pack behavior is reusable for all eligible Tenants and versioned; tenant preference cannot modify the pack's controlled rules. |
| M27-RULE-028 | Irreversible or high-risk actions require specific confirmation of Tenant, consequence, approver, and evidence; broad bulk approval is prohibited. |
| M27-RULE-029 | No production limit or supported-volume claim is approved without MESP-48 evidence and validation against an approved reference profile. |
| M27-RULE-030 | Retail POS is unavailable in Release 1 under every Plan, Entitlement, flag, support action, import, and integration path. |
| M27-RULE-031 | Trial Tenants and a Trial lifecycle state are unavailable in Release 1. The non-production Restricted Validation Plan is not a Trial offering and cannot be assigned in production. |

## 22. State machines

| Object | States and controlled transitions | Guards and terminal meaning |
|---|---|---|
| Tenant | Draft → Provisioning → Configuration Required → Ready for Activation → Active → Grace Period → Suspended → Reactivated → Active; Active/Suspended → Export Requested → Termination Pending → Terminated → Retained → Purge Approved → Purged | Trial is excluded. Direct Active → Suspended is permitted for security/legal cause with bypass reason. Purged is terminal for the certified scope. |
| Subscription | Proposed → Scheduled → Effective → Expiring → Expired; Effective → Suspended → Effective; Proposed/Scheduled → Cancelled | One effective Subscription per Tenant; historical record immutable. |
| Entitlement | Staged → Effective → Expired/Revoked; Effective → Temporarily Restricted → Effective | Source Subscription and Plan remain traceable; no deletion of tenant data. |
| Module | Not Entitled → Entitled/Not Configured → Configuration Required → Ready → Active → Read-Only/Deactivation Pending → Inactive; Inactive → Configuration Required/Ready | Dependencies, open data/processes, and approval determine allowed transitions. |
| Support access | Requested → Authorized → Active → Expired/Revoked → Reviewed/Closed | Named case, scope, Tenant, approval, and time. Closed is read-only evidence. |
| Export | Requested → Validating → Generating → Ready → Downloaded/Accepted → Expired; any nonterminal state → Failed/Cancelled | Download rechecks authorization; retry is idempotent; artifact expiry is enforced. |
| Purge | Candidate → On Hold/Eligible → Approved → Cooling-Off / Final Notice → Executing → Completed or Failed | Legal hold blocks approval/execution. The MESP-50 interval and final notice block early execution. Completed is terminal for the certified scope; Failed requires reviewed recovery. |

- **M27-REQ-065 (Must):** State transitions must be atomic from the business user's perspective: either the transition and its required evidence succeed, or the prior authoritative state remains with a visible failure.
- **M27-REQ-066 (Must):** Every state object must show entered time, entered by/source, reason, current owner, next review or expiry where relevant, and permitted transitions.

## 23. Data requirements

| Business record | Minimum information | Owner / retention note |
|---|---|---|
| Tenant catalogue | Stable ID/code, legal/customer identity, bilingual display identity, contacts, country, lifecycle, onboarding owner | Platform-owned; retained through governed offboarding evidence. |
| Plan version | Identity/name/version, production or restricted-validation eligibility, status, modules/features, limit definitions, service/support tier, non-calculating price metadata, effective interval, approval | Product/Commercial; historical versions immutable; metadata creates no financial transaction. |
| Subscription | Tenant, Plan version, status, dates, reason, commercial reference, assigner/approver | Commercial; not tenant ERP finance data. |
| Entitlement | Tenant, capability/capacity, source Plan/Subscription, status, effective interval, temporary security/safety restriction | Platform; historical evaluation reproducible; no override field or authority. |
| Provisioning request/run | Inputs, contracted hosting region, cross-border support terms, subprocessor restrictions, duplicate key, stages, attempts, results, errors, compensations, correlation, evidence | Operations; safe retry and audit required; final residency rules under MESP-50. |
| Module activation | Module, dependency/config checklist, data/open-process assessment, rollback plan, status, approver, effective time | Platform/Tenant shared evidence by responsibility. |
| Usage/limit | Measure, scope, unit, period, value, freshness, warning/hard points, source, exceptions | Operations/Commercial; never invoice amount in R1. |
| Branding/configuration | Bilingual identity, logo metadata, colors, contacts, document-template and numbering profiles, scope, version, preview/validation, effective time | Tenant-owned within Platform controls. |
| Support authorization | Case, Tenant, named user, purpose, scope, sensitivity, approvals, start/end, activity, closure | Security/Support; privileged record. |
| Feature rollout | Capability, cohort, owner, dates, Entitlement requirement, criteria, events, expiry/outcome | Product/Operations. |
| Lifecycle decision | From/to state, trigger, reason, actor, approver, effective time, checklist, notices | Platform; immutable transition evidence. |
| Export/retention/purge | Scope, manifest, integrity, holds, approvals, cooling-off/final notice, artifacts, expiry/downloads, and certificate fields covering included/excluded systems/data, residual copies, restrictions, and restoration possibility | Privacy/Security; final retention and interval set by MESP-50. |

- **M27-REQ-067 (Must):** Tenant identity, Subscription, Entitlement, lifecycle, support, export, and purge records must use stable identifiers that are never reassigned.
- **M27-REQ-068 (Must):** Sensitive values, secrets, credentials, raw authentication data, and unnecessary personal data must be excluded from general audit, notifications, exports, and reports.
- **M27-REQ-097 (Must):** Only the Platform-owned metadata categories closed in section 6 may appear in cross-Tenant Platform registers. Such registers require specific Platform authorization and are denied to Tenant actors; Tenant actors may access only separately authorized Tenant-scoped views.

## 24. Validation rules

1. Mandatory inputs are present, normalized, and within an approved vocabulary.
2. Tenant code, legal/customer identity, contact, and initial administrator pass duplicate and format checks.
3. Country Pack, Plan version, environment eligibility, Subscription interval, module dependencies, rollback plan, and limit definitions exist and are eligible at the effective time.
4. Dates form valid non-overlapping effective intervals; end is after start.
5. Arabic and English names accept Unicode and preserve script; required bilingual fields follow Country Pack policy.
6. Branding files and colors meet security, content, size, and accessibility policies.
7. State transition source, target, actor, authority, reason, prerequisites, and evidence are valid.
8. Support access cannot exceed approved Tenant, purpose, scope, or time; support authorization alone never validates export authority.
9. Export scope is authorized and bounded; a support identity also has separate export Permission, separate export authorization, explicit Tenant authorization, and an unexpired artifact.
10. Purge Tenant/certified scope is independently confirmed, has no active hold, and cannot execute until the cooling-off interval and final notice are complete.
11. Contracted hosting region, cross-border support terms, and subprocessor restrictions are present and consistent with the current MESP-50-controlled production policy.

- **M27-REQ-069 (Must):** Validation failure must identify the affected field/control, preserve safe user input, perform no partial authoritative transition, and provide a correction path.
- **M27-REQ-070 (Must):** Duplicate detection must distinguish confirmed duplicate, possible match requiring review, and unique request; uncertain matches cannot create a second Tenant without recorded resolution.

## 25. Permissions and authorization requirements

| Action | Minimum authority | Additional control |
|---|---|---|
| Create onboarding draft | Platform onboarding permission | Commercial authority reference |
| Start/retry provisioning | Platform operations permission | Same request identity; failure context |
| Assign/change Plan | Platform Administrator permission | Effective date, reason, approval; no Entitlement override |
| Activate Tenant/module | Platform activation permission | Readiness/activation checklist, approved rollback plan, and Tenant acknowledgment |
| Change commercial limit | Commercial limit permission | Approved source and effective interval |
| Apply safety limit | Operations safety permission | Reason, expiry, impact review |
| Suspend/reactivate | Lifecycle permission appropriate to cause | Reason-owner approval; security/legal authority when applicable |
| Authorize support | Tenant and/or Platform authority per policy | Named case, scope, duration; no self-approval |
| Request/generate/download export | Separate export rights for each stage | Recheck scope at every stage; support identity also needs explicit Tenant authorization for the named export |
| Terminate | Contract/lifecycle authority | Export and legal-hold checks |
| Approve/execute purge | Two distinct authorized actors | MESP-50 policy, certified-scope confirmation, cooling-off interval, and final notice |
| View cross-Tenant Platform register | Specifically authorized Platform actor | Denied to every Tenant actor; tenant-scoped view is a separate authorization |

- **M27-REQ-071 (Must):** Authorization must be enforced for each action and protected view, including background and integration paths; interface visibility is not a security boundary.
- **M27-REQ-072 (Must):** Denied actions must reveal no other Tenant's existence or data and must create proportionate security/audit evidence.
- **M27-REQ-073 (Must):** High-risk actions must prevent the same actor from being both requester and sole approver where dual control is required.

## 26. Audit requirements

Every material event records: event identity, event time and trusted time zone, actor/service identity, Tenant and applicable organization scope, action, object and state, source/correlation, reason, safe before/after values, approval, outcome, failure classification, and related case/request. Audit events are immutable and searchable only by authorized scope.

Mandatory events include onboarding, provisioning attempts/cleanup, activation, lifecycle transition, Plan/Subscription/Entitlement/limit changes, module changes, branding, feature flags, privileged access, denied cross-tenant attempts, export generation/download, legal hold, termination, purge approval/execution/failure, and configuration ownership changes.

- **M27-REQ-074 (Must):** A sampled Tenant history must reconstruct who authorized and performed every material platform action and what access/Entitlement resulted.
- **M27-REQ-075 (Must):** Audit retrieval and export must itself be authorized, bounded, and audited.

## 27. Notifications

| Event | Recipient | Minimum content |
|---|---|---|
| Provisioning/activation | Platform owner; Tenant Administrator when safe | Tenant, state, required action, owner, time, no secret |
| Limit warning/hard denial | Tenant Administrator; Operations | Measure, current value/freshness, threshold type, effect, escalation |
| Grace/suspension/reactivation | Tenant Administrator, Platform/Commercial/Security owners as applicable | Reason category, effect, effective time, remedy/review, contact |
| Support access | Tenant Administrator and security owner | Named support user, case, scope, start/end, revoke route |
| Feature rollout material effect | Product/Operations and affected tenant administrator where needed | Capability, window, expected effect, support route |
| Export | Requester and authorized admin | Status, scope, expiry, secure access route; never embed data |
| Termination/retention/purge | Tenant and Platform owners per stage | Consequence, effective time, holds, remaining action, irreversible warning |

- **M27-REQ-076 (Must):** Notification failure must not reverse a valid business state transition, but must be visible, retryable, and escalated according to severity.
- **M27-REQ-077 (Must):** Notifications must support Arabic and English templates and must not disclose sensitive Tenant data in subjects, unsecured channels, or unrelated recipients.

## 28. Reports and KPIs

| Report / KPI | Purpose | Required reconciliation |
|---|---|---|
| Tenant lifecycle register | Current state, age, next action, blocked reason | Lifecycle events and decisions |
| Provisioning quality | Success/failure/retry duration and stage | Provisioning runs and final Tenant count |
| Plan/Entitlement register | Effective Plan, modules, limits, exceptions | Subscription versions and Entitlement history |
| Usage and limits | Current usage, warnings, hard denials, freshness | Source measurement and approved definitions |
| Module activation readiness | Active/inactive/read-only modules and blockers | Entitlements, dependencies, configuration evidence |
| Privileged support report | Open/expired sessions, scope, actions, overdue review | Authorization and security audit |
| Feature rollout register | Cohorts, exposure, outcome, expired flags | Flag decisions and activity |
| Suspension/offboarding | Cause, duration, notice, exports, holds, retention/purge state | Lifecycle, export, hold, and purge evidence |
| Isolation/security exceptions | Denied cross-tenant and privileged attempts | Security and audit events |

- **M27-REQ-078 (Must):** Every report must show data-as-of time, scope, filters, freshness, source, and authorized drill-down to evidence.
- **M27-REQ-079 (Must):** KPIs must not imply committed service capacity before MESP-48 approval and must reproduce the effective approved MESP-52 Plan/Subscription/Entitlement state.

## 29. Exceptions and recovery scenarios

| Exception | Required recovery behavior |
|---|---|
| Duplicate onboarding request | Stop creation, show confirmed/possible match to authorized reviewer, link resolution, audit. |
| Provisioning partial failure | Keep non-operational, show completed/failed stages, retry idempotently or compensate, preserve evidence. |
| Initial administrator invitation failure | Keep Configuration Required; allow corrected reissue without duplicate membership. |
| Country Pack or Plan version unavailable | Block readiness; choose approved current version or escalate. |
| Dependency becomes invalid after module readiness | Revert to Configuration Required/Read-Only as risk requires; notify owner. |
| Usage measurement stale/unavailable | Mark unknown; do not claim a hard commercial breach; safety action requires separate evidence. |
| Notification delivery failure | Retain business outcome, queue retry, show failure, provide alternate contact procedure. |
| Support access misuse or scope breach | Revoke immediately, preserve evidence, notify Security, start incident process. |
| Feature rollout harms cohort | Trigger rollback, preserve exposure history, block further rollout, review affected transactions. |
| Suspension interrupts work | Record interrupted jobs/actions; do not infer success; review and restart deliberately after reactivation. |
| Export fails or expires | Preserve request/manifest state; safe retry creates a new controlled artifact; old artifact remains inaccessible. Before an export waiver or purge proceeds, confirm whether any accepted/recoverable Tenant copy exists and warn if none will remain. |
| Legal hold arrives during purge preparation | Revoke eligibility/approval and return to Retained/On Hold before execution. |
| Purge partially fails | Stop, isolate, record exact completion, escalate; never certify completion until verified. |

- **M27-REQ-080 (Must):** Recovery actions must retain the original request/correlation and never obscure the first failure or duplicate authoritative effects.

## 30. Localization requirements

- Platform administration must operate in Arabic and English with complete RTL/LTR behavior, locale-aware dates/numbers, Unicode-safe validation/search, and bilingual templates where required.
- Saudi Tenant defaults are SAR, Asia/Riyadh, Arabic and English, but each value remains explicit governed configuration.
- Tenant, Company/Legal Entity, branding, notification, export metadata, reason codes, and validation messages must support the approved bilingual terminology.
- System identifiers, audit identities, and state codes remain language-neutral while display text is localized.

- **M27-REQ-081 (Must):** Switching language or direction must not change authority, scope, state, values, effective times, or evidence.
- **M27-REQ-082 (Must):** Missing translation must use a safe approved fallback and be visible for correction; it must never hide a critical warning or irreversible consequence.

## 31. Security and privacy business requirements

- Deny-by-default Tenant isolation across users, jobs, files, reports, exports, notifications, audit, support, and future integrations.
- Least privilege, personal identities, controlled privileged reauthentication, session revocation, and MFA capability according to the later approved Identity/Security BRDs.
- Data minimization in onboarding, support, notifications, audit, usage, and purge certificates.
- Purpose limitation for support and export; no reuse of retained or terminated Tenant data.
- Explicit legal-hold, retention, residency, cross-border support, subprocessor, and purge decisions before production.
- Security incident authority may suspend without Grace Period and must preserve evidence.

- **M27-REQ-083 (Must):** Production use requires approved privacy, retention, residency, privileged-access, incident, and purge controls; technology selection alone is not compliance evidence.
- **M27-REQ-084 (Must):** Cross-tenant access attempts must fail without confirming the target Tenant and must be investigated according to severity.

## 32. Integration requirements

Release 1 business dependencies may include email delivery, private object storage, authentication/session services, observability, and approved Saudi adapters. Platform administration must treat an integration as a governed dependency with owner, purpose, Tenant scope, availability state, failure/retry behavior, reconciliation, credentials responsibility, and evidence.

- **M27-REQ-085 (Must):** An integration may not activate for a Tenant unless product scope, Entitlement where applicable, configuration, authorization, and readiness controls pass.
- **M27-REQ-086 (Must):** Integration failure must not silently change Tenant lifecycle, Entitlement, export, notification, or purge outcome; failures remain visible and reconcilable.
- **M27-REQ-087 (Should):** External machine access is deferred until an approved integration need and separate authentication decision; first-party user sessions must not be reused as partner credentials.

## 33. Migration and opening requirements

This BRD does not define business-data migration. For a new Tenant it requires controlled opening configuration: Tenant identity, Plan/Subscription, country and language defaults, initial Company/Legal Entity, initial administrator, modules, limits, branding, and numbering profile. Where an existing customer's platform administration data is migrated, it must have source, owner, mapping, duplicate rules, preview, rejected-row handling, batch identity, rollback strategy, reconciliation, and sign-off.

- **M27-REQ-088 (Must):** Migrated Tenant and Subscription records must not bypass lifecycle, uniqueness, Entitlement, support, or activation controls.
- **M27-REQ-089 (Must):** Opening configuration must be reconciled to the approved onboarding request before Active state.

## 34. Non-functional business expectations

| Expectation | Business requirement at BRD stage |
|---|---|
| Availability | Production service target is at least 99.9% monthly, excluding communicated planned maintenance, measured by the approved service-boundary method. |
| Recovery | RPO is no more than 15 minutes and RTO is no more than 4 hours. Production topology and restore exercises must validate both. |
| Performance | Under the MESP-48-approved reference load, common reads have p95 no more than 2 seconds and common commands have p95 no more than 3 seconds. |
| Scale/noisy tenant | Limits, queues, and concurrency must prevent one Tenant from materially degrading others; test profile remains open. |
| Accessibility | Core platform administration targets WCAG 2.2 AA, including RTL and critical warnings. |
| Auditability | 100% of material lifecycle, entitlement, privileged, export, and purge actions require evidence. |
| Data portability | Authorized exports preserve stable identifiers and material relationships in documented formats. |
| Operability | Failures, retries, queues, stale measures, notifications, and owner actions are visible and reconcilable. |
| Maintainability | Configuration and reusable product rules take precedence over tenant-specific behavior. |

- **M27-REQ-090 (Must):** Before affected implementation, MESP-48 must approve reference measures for tenants, users, products, warehouses, documents/lines, monthly transactions, files/storage, imports, exports, reports, jobs, integrations/API, and concurrency.
- **M27-REQ-091 (Must):** Before production, the approved reference profile must be validated through performance, recovery, isolation, long-running work, and noisy-Tenant evidence.

## 35. Given/When/Then acceptance scenarios

1. **M27-AC-001 — Provisioning success:** Given a complete approved onboarding request with unique identity, eligible Plan/Country Pack, and initial administrator, when provisioning and configuration validation succeed, then one Tenant reaches Ready for Activation with complete evidence and no operational transactions.
2. **M27-AC-002 — Provisioning failure:** Given a provisioning stage fails, when the run stops, then the Tenant is not Active, completed/failed stages and safe recovery are visible, and an operator is notified.
3. **M27-AC-003 — Provisioning retry:** Given a failed run and the same request identity, when an authorized operator retries, then the original run continues or safely compensates and no second Tenant is created.
4. **M27-AC-004 — Duplicate request:** Given a matching Tenant code or legal/customer identity, when creation is requested, then authoritative creation is blocked pending reviewed duplicate resolution.
5. **M27-AC-005 — Plan assignment:** Given an eligible Draft/Configuration Required Tenant and approved production R1 Plan version, when a Platform Administrator assigns it with effective dates, then the Subscription and staged Entitlements are recorded without user Permissions, charges, invoices, payments, or accounting entries.
6. **M27-AC-006 — Future Plan change:** Given an Active Subscription and a future-dated change, when it is approved, then current Entitlements remain unchanged until the effective time and history remains reproducible.
7. **M27-AC-007 — Entitlement granted:** Given an effective Subscription includes Inventory and the Tenant/module is ready, when activation is approved, then entitled and permitted users may use Inventory within scope.
8. **M27-AC-008 — Entitlement expired:** Given a module Entitlement expires, when a new module action is attempted, then it is denied, existing data is preserved, and denial/source/expiry are audited.
9. **M27-AC-009 — Permission without Entitlement:** Given a non-production Tenant uses the Restricted Validation Plan, a User has a Permission for an intentionally omitted module, and no Entitlement exists, when the User acts, then access is denied without enabling the module; the Restricted Validation Plan cannot be assigned in production or treated as a Trial.
10. **M27-AC-010 — Entitlement without Permission:** Given the Tenant has Entitlement but the User lacks Permission, when the User acts, then access is denied without revealing unauthorized data.
11. **M27-AC-011 — Module activation:** Given Entitlement, dependencies, configuration, opening evidence, an approved rollback plan, and activation approval are complete, when activation occurs, then the module becomes Active and the readiness/rollback evidence snapshot is retained.
12. **M27-AC-012 — Missing dependency:** Given Finance depends on incomplete organization configuration, when activation is requested, then activation is blocked and the named dependency/action owner is shown.
13. **M27-AC-013 — Deactivation with data:** Given an Active module contains historical or open data, when deactivation is requested, then data is preserved and the module enters Read-Only/Deactivation Pending until obligations are resolved.
14. **M27-AC-014 — Re-enable module:** Given an Inactive module with preserved data is re-entitled, when dependencies and consistency checks pass, then it can be reactivated without duplicating opening data or jobs.
15. **M27-AC-015 — Usage warning:** Given a fresh usage measure crosses its warning point, when usage is recorded, then authorized administrators are notified and the action remains allowed below the hard limit.
16. **M27-AC-016 — Hard limit:** Given a configured hard capacity is reached, when a capacity-increasing action occurs, then only that action is denied, existing data/read access remain, and escalation is explained/audited.
17. **M27-AC-017 — No Wafra volume evidence:** Given MESP-48 has no approved evidence, when a production threshold or capacity promise is requested, then it remains unapproved and no invented value is published.
18. **M27-AC-018 — Grace Period:** Given an approved commercial remedy window, when the Tenant enters Grace Period, then deadline, access behavior, notices, and automatic/manual next decision are recorded.
19. **M27-AC-019 — Suspension login:** Given a Suspended Tenant where sign-in is prohibited, when an existing or new session accesses the Platform, then it is denied/revoked without data loss and the attempt is audited.
20. **M27-AC-020 — Suspension job:** Given a job belongs to a Suspended Tenant, when execution is due, then prohibited work does not run, its state/reason remain visible, and no other Tenant is affected.
21. **M27-AC-021 — Reactivation:** Given the suspension reason is cleared and restoration checks pass, when reactivation is approved, then Entitlements, sessions, jobs, integrations, and interrupted work are reevaluated before Active operation resumes.
22. **M27-AC-022 — Branding on authentication:** Given valid Tenant branding, when a user reaches authentication, then approved Tenant identity may appear while trustworthy Platform/security identity and accessibility remain intact.
23. **M27-AC-023 — Invalid branding:** Given a malicious, unsupported, or inaccessible branding asset, when submitted, then it is rejected, safe defaults remain, and the attempt/result are audited.
24. **M27-AC-024 — Feature cohort rollout:** Given an approved feature, eligible Entitlement, and controlled cohort, when the flag starts, then only eligible Tenants see behavior and exposure is auditable.
25. **M27-AC-025 — Feature rollback:** Given rollback criteria are met, when the owner disables the flag, then new exposure stops, existing data remains interpretable, and affected Tenants/transactions are identifiable.
26. **M27-AC-026 — Support authorized:** Given a valid case, named support user, Tenant authorization, approved scope, and end time, when access begins, then only approved Tenant/data/actions are available and fully audited.
27. **M27-AC-027 — Support expired:** Given the support interval ends, when further access is attempted, then it is denied automatically and review/closure evidence remains.
28. **M27-AC-028 — Cross-Tenant support attempt:** Given support is authorized for Tenant A, when the same identity targets Tenant B, then access is denied without revealing Tenant B and a security event is raised.
29. **M27-AC-029 — Export:** Given an authorized export request, when generation succeeds, then a bounded manifest/artifact with integrity, scope, expiry, and audit is available only to an authorized downloader. If that downloader is a support identity, separate export Permission, separate export authorization, and explicit Tenant authorization for the named artifact are all present.
30. **M27-AC-030 — Export expiry:** Given an export artifact has expired, when download is attempted, then it is denied and a new authorized request is required.
31. **M27-AC-031 — Termination:** Given export disposition, Subscription end, access closure, and legal-hold review are complete, when termination takes effect, then operational access/Entitlements end and data moves to governed retention.
32. **M27-AC-032 — Legal hold:** Given a Tenant is Retained and an active legal hold exists, when purge approval is requested, then approval/execution are blocked and the hold owner/review remain visible.
33. **M27-AC-033 — Purge approval:** Given MESP-50 controls are approved, retention expired, no hold exists, and two authorized actors confirm the certified scope, when purge is approved, then the cooling-off interval begins and final notice is issued; execution remains blocked until both complete and all conditions are rechecked.
34. **M27-AC-034 — Purge failure:** Given purge execution partially fails, when the failure is detected, then completion is not certified, exact completed/incomplete scope is preserved, and reviewed recovery is required.
35. **M27-AC-035 — Purge evidence:** Given purge completes and verification passes, when the Tenant becomes Purged, then the certificate states the certified scope, included and excluded systems/data, residual backups or retained copies, legal-hold/retention restrictions, and whether restoration remains possible outside the certified scope. It claims restoration is universally impossible only if all residual copies are demonstrably removed.
36. **M27-AC-036 — Arabic configuration:** Given Arabic is selected, when the administrator provisions, reviews, receives warnings, and exports metadata, then RTL, Arabic text, dates/numbers, authority, and values remain correct.
37. **M27-AC-037 — Notification failure:** Given a valid suspension transition and email delivery failure, when notification retries fail, then suspension remains valid, failure/escalation are visible, and alternate contact can be recorded.
38. **M27-AC-038 — Unauthorized lifecycle change:** Given a User lacks suspension authority, when suspension is attempted through any channel, then state is unchanged, access is denied, and an audit/security event is recorded.
39. **M27-AC-039 — Retail POS unavailable:** Given any Plan, Entitlement, feature flag, import, support action, or integration request, when Retail POS is requested, then it remains unavailable and is routed to product change control.
40. **M27-AC-040 — Country Pack reuse:** Given Wafra and another Saudi Tenant use the same Country Pack version, when each applies its own branding and identity, then controlled country behavior is identical while Tenant preferences remain isolated.
41. **M27-AC-041 — Trial exclusion:** Given any production onboarding, Plan, lifecycle, support, import, or integration path, when Trial Tenant or Trial state is requested, then it is rejected as outside Release 1; the non-production Restricted Validation Plan cannot create an exception.
42. **M27-AC-042 — Entitlement override prohibited:** Given an actor requests a direct per-Tenant Entitlement grant, when authorization is evaluated, then the request is denied and the actor is directed to a versioned Plan or effective-dated Subscription change; a security restriction can only block access.
43. **M27-AC-043 — Multiple legal entities:** Given one Tenant has two legal entities, when each is configured, then each retains its own legal/accounting boundary and no consolidated statements, intercompany automation, elimination entries, or transfer-pricing behavior is created.
44. **M27-AC-044 — NFR baseline:** Given the MESP-48-approved reference load and production topology, when service and recovery validation runs, then monthly availability is at least 99.9%, RPO is no more than 15 minutes, RTO is no more than 4 hours, common-read p95 is no more than 2 seconds, and common-command p95 is no more than 3 seconds.

## 36. Open questions

| ID | Question | Owner / timing | Blocking effect |
|---|---|---|---|
| M27-OQ-002 | What measured Wafra and conservative SME reference volumes apply to every category in §15/§34? | Hossam + Wafra evidence; approve before affected implementation | No thresholds/claims until answered. |
| M27-OQ-003 | What Grace Period duration and eligible trigger categories apply? | Hossam/Commercial before lifecycle implementation | Rule structure approved; numeric duration open. |
| M27-OQ-004 | Which suspension types permit read-only access and which require immediate total lockout? | Security/Legal/Commercial before implementation | Default is explicit-case decision. |
| M27-OQ-005 | When is Tenant authorization mandatory for normal and emergency support access? | Hossam + Security/Privacy before privileged access implementation | No emergency access assumption. |
| M27-OQ-006 | What retention periods, hosting/residency rules, cross-border support conditions, subprocessor rules, legal-hold authority, backup treatment, purge cooling-off duration, residual-copy treatment, and certificate retention apply? | MESP-50 + qualified advisors before production | Blocks production offboarding/purge. |
| M27-OQ-007 | Which Platform notifications must also use email at launch, and who owns delivery escalation? | Hossam/Operations before notification implementation | In-app/operational evidence remains required. |
| M27-OQ-008 | Which production provider/region and object-storage/observability choices satisfy approved obligations? | Architecture ADR gates before production | Does not change BRD behavior. |

## 37. Decisions

| Decision | Status | Owner | Effect |
|---|---|---|---|
| PD-019 — Release 1 Technology Architecture | Approved 1 Aug 2026 | Hossam | Feasibility constraint; no new business scope. |
| M27-DEC-001 / MESP-52 — One production R1 Plan with all approved B2B ERP modules, simple configurable limits, service/support tier, non-calculating price metadata, manual Platform Administrator assignment, effective-dated audit, no Trial, no Entitlement override, and no metered/automated billing or pricing engine | **Approved by Hossam** | Hossam | Baselines Plan, Subscription, Entitlement, module, limit, and lifecycle requirements. A Restricted Validation Plan is non-production evidence only. |
| M27-DEC-002 — MESP-48 evidence is gathered without invented thresholds | BRD baseline recommendation | Hossam + Wafra evidence | Detailed profile is approved before affected implementation. |
| M27-DEC-003 — Irreversible production purge remains blocked by MESP-50 and external specialist review | Required production gate | Hossam + Privacy/Legal/Security | Prevents unsafe retention/purge assumptions. |
| M27-DEC-004 — Purge certificates distinguish certified purge scope from residual copies and state whether restoration remains possible outside scope | **Approved by Hossam** | Hossam | Makes purge evidence truthful without pre-empting MESP-50 rules. |
| M27-DEC-005 / MESP-56 — A Tenant may contain multiple legal entities; each owns its legal/accounting boundary; consolidation, intercompany automation, elimination entries, transfer pricing, and consolidated statements are excluded | **Approved by Hossam** | Hossam | Baselines hierarchy scope; MESP-30 defines detailed operations. |

Prepare immutable Product Decision Register entries PD-020 for MESP-52, PD-021 for MESP-56, and PD-022 for purge-certificate truthfulness. Jira recording remains a separate controlled action and must not overwrite prior records.

## 38. Dependencies

| Dependency | Why needed | Timing |
|---|---|---|
| MESP-18 / glossary | Mandatory business terminology | Current and ongoing |
| MESP-19 traceability | Links BRD to PRD/decisions/evidence | Before BRD closure |
| MESP-28 Identity and Access | Detailed User, Role, Permission, session, MFA behavior | Before affected implementation; do not start now |
| MESP-29 Multi-Tenancy | Detailed isolation and organization lifecycle | Before affected implementation; do not start now |
| MESP-37 Saudi Country Pack | Detailed Saudi configuration/compliance | Before production |
| MESP-38 Security/Audit/Data Governance | Privilege, retention evidence, controls | Before affected implementation/production |
| MESP-39 Integrations | External dependency contracts | When approved scope exists |
| MESP-40 Migration | Tenant business-data onboarding/cutover | Before Wafra cutover |
| MESP-48 | Reference volume decision | Before affected implementation |
| MESP-50 | Residency, retention, legal hold, purge | Before production and irreversible purge |
| MESP-52 | Approved Plan/Subscription/Entitlement decision | Glossary is updated; record PD-020, close the Jira open question, and update traceability after founder approval workflow |
| MESP-56 | Approved multiple-legal-entity and consolidation exclusion decision | Record PD-021 and retain MESP-30 detailed operating rules |
| ADR-003/004/005/008/009/010/012/014/016 | Detailed architecture controls | At timing defined in `docs/Decisions.md` |

## 39. Risks

| Risk | Consequence | Mitigation / owner |
|---|---|---|
| Entitlement and Permission are confused | Commercial or security bypass | Mandatory distinction, combined denial scenarios, Hossam/Security |
| Wafra preference becomes product logic | Upgrade/support burden | RULE-003, configuration inventory, Product review |
| Unknown volumes become arbitrary limits | Poor UX, false promises, capacity failure | MESP-48 evidence and no-threshold rule |
| Suspension loses or duplicates work | Business/data integrity incident | Job/session/integration controls and reviewed reactivation |
| Standing support privilege | Cross-Tenant/privacy incident | Named, case/time/scope access; no superuser |
| Flag becomes permanent product variation | Uncontrolled configuration debt | Owner/expiry/review; move commercial variation to Plan |
| Offboarding deletes too early or retains too long | Legal, privacy, contractual harm | MESP-50, legal hold, dual purge control, external review |
| Export is waived and the export artifact expires before purge | Tenant may have no accepted recoverable copy when the certified scope is purged | Before waiver/purge, confirm accepted copy, artifact status, residual-copy position, final notice, and explicit Tenant acknowledgment |
| Single-founder role concentration | Weak independent approval | System dual control, immutable evidence, external specialists before production |
| Email/provider outage hides decisions | Missed action or dispute | In-app/operational evidence, retries, escalation |
| POS scope re-enters through packaging | R1 scope expansion | Explicit denial across every control path |

## 40. Approval criteria

MESP-27 may be approved only when:

1. Hossam accepts or requests changes to this BRD and founder review in writing.
2. Hossam confirms the already approved M27-DEC-001 / MESP-52 and M27-DEC-005 / MESP-56 decisions in the corrected Founder Review.
3. All Must requirements are traced to PRD/Jira sources, business rules, actors, states, data, permissions, audit, notifications, reports, and acceptance evidence.
4. Tenant lifecycle, Plan/Subscription/Entitlement distinctions, limits, module controls, support access, feature rollout, suspension/reactivation, and offboarding are accepted.
5. MESP-48 evidence categories and owners are accepted without approving invented thresholds.
6. MESP-50 and external production validation gates remain explicit.
7. No Retail POS, Wafra-specific behavior, code, API design, database design, implementation Story, or unapproved commercial billing behavior has been introduced.
8. MESP-27 remains In Progress with `status-in-review` until Hossam approves; only then may it move to Done.
9. MESP-28 is not started until MESP-27 approval is recorded.
10. The controlled glossary and Founder Decision Pack reflect the approved MESP-52 and MESP-56 decisions without changing the established BRD sequence.

### Approval block

| Decision | Founder response |
|---|---|
| Approve MESP-27 BRD | Approve |
| Confirm approved M27-DEC-001 / MESP-52 correction | Confirm |
| Confirm approved M27-DEC-005 / MESP-56 correction | Confirm |
| Accept MESP-48 evidence plan and no invented thresholds | Approve |
| Confirm MESP-50 remains a production gate | Approve |
| Approved by / date | Hossam / 1 August 2026 |
| Requested changes | None |

---

### Appendix A — Copy-ready Jira update for MESP-27

**MESP-27 correction summary:** Reissued `docs/11_SaaS_Platform_Administration_BRD.md` as v0.10, status **Ready for Founder Approval after Independent Audit Corrections**. All 16 independent-audit corrections are applied: Trial exclusion; PRD NFR targets restored; Plan service/support tier and non-calculating price metadata; complete PLT/RPT/ADM traceability; contracted hosting/cross-border/subprocessor provisioning attributes; template/numbering governance; module rollback plan; separate support-export authority; Entitlement override prohibition; non-production Restricted Validation Plan; closed Platform-owned metadata list; truthful certified-scope purge certificate; purge cooling-off/final notice; export-waiver/expiry risk; post-decision glossary update; and canonical glossary PRD filename.

**MESP-52 approved decision:** One production Release 1 Plan contains all approved B2B ERP modules. Trial Tenants and Retail POS are excluded. The Plan records simple configurable limits, service/support tier, non-calculating price metadata, and effective dates. The Platform Administrator assigns it manually. Plan and Entitlement changes are effective-dated and audited. There is no metered billing, automated subscription invoicing, automated pricing engine, or per-Tenant Entitlement override. Entitlements change only through a versioned Plan or effective-dated Subscription change. A Restricted Validation Plan exists only in non-production and cannot create a Trial Tenant.

**MESP-56 approved decision:** A Tenant may contain multiple legal entities. Each legal entity owns its legal and accounting boundary. Release 1 excludes financial consolidation, intercompany automation, elimination entries, transfer pricing, and consolidated statements. Detailed operating rules remain in MESP-30.

**MESP-23 Open Questions Register:** Mark MESP-52 and MESP-56 answered by Hossam. Keep MESP-48 open for measured Wafra plus conservative SME reference volumes. Keep MESP-50 open for hosting/residency, cross-border support, subprocessors, retention, legal hold, backup treatment, purge cooling-off duration, residual copies, and certificate retention. Retain the remaining MESP-27 open questions for Grace Period duration, suspension/read-only behavior, emergency support, and notification channels.

**MESP-22 Product Decision Register — immutable entries:**

- **PD-020 — Release 1 Plan, Trial exclusion, Plan metadata, and Entitlement governance:** record the complete MESP-52 decision above; owner/approver Hossam; date 1 August 2026; affected modules Platform Administration, Multi-Tenancy, Identity and Access, Reporting, Security and Audit; trace PLT-011/012/013, BR-001/004/010/011/015/016, PRD §17, MESP-27/52.
- **PD-021 — Multiple legal entities without consolidation:** record the complete MESP-56 decision above; owner/approver Hossam; date 1 August 2026; affected modules Organization, Finance, Inventory, Procurement, B2B Sales, Reporting; trace PLT-002, BR-010/015, approved hierarchy, MESP-30/56.
- **PD-022 — Purge certificate truthfulness:** certified purge scope and residual-copy scope must be distinguished, including restoration possibility outside the certified scope. This does not authorize purge or close MESP-50; owner/approver Hossam; date 1 August 2026; affected modules Platform Administration, Security and Audit, Files, Operations; trace PRD §5.3/§17, PLT-008/011, BR-011, MESP-27/50.

**MESP-19 Traceability Matrix:** Add MESP-27 v0.10 links for PLT-001/002/007/008/009/010/011/012/013/014; RPT-003; ADM-001/002/003; BR-001/003/004/010/011/015/016; RULE-001/002/016; PRD §17; PD-019/020/021/022; MESP-27/48/50/52/56. Record 99.9% availability, RPO ≤15 minutes, RTO ≤4 hours, common-read p95 ≤2 seconds, and common-command p95 ≤3 seconds, with reference-load validation under MESP-48.

**MESP-27 final approval workflow:** Keep **In Progress** with `status-in-review`. Attach/link the v0.10 BRD and `docs/92_MESP27_Founder_Review.md`. Do not move to Done until Hossam signs the Founder Review approval block. After signature, post the approval evidence, record PD-020/021/022, update MESP-23 and MESP-19, update/close MESP-52 and MESP-56 according to the Jira workflow, then move MESP-27 to Done. Do not create implementation Stories and do not start MESP-28.

### Appendix B — Independent audit correction verification

| # | Correction | Applied evidence |
|---|---|---|
| 1 | Trial exclusion | Sections 4, 10, 11; RULE-031; AC-041 |
| 2 | PRD NFR targets | Section 34; AC-044 |
| 3 | Plan tier/price metadata | Sections 6, 11, 23; REQ-016 |
| 4 | Traceability completion | Section 5 and MESP-19 Jira preparation |
| 5 | Residency/hosting provisioning attributes | Sections 13, 23, 24; REQ-093 |
| 6 | Template/numbering governance | Sections 13, 16, 23; REQ-094 |
| 7 | Activation rollback plan | Section 14; RULE-008; AC-011 |
| 8 | Support export separation | Sections 17, 24, 25; RULE-021; AC-029 |
| 9 | No Entitlement override | Sections 11–12; RULE-007; AC-042 |
| 10 | Restricted Validation Plan | Section 11; AC-009/041 |
| 11 | Closed Platform metadata list | Sections 6, 23, 25; REQ-097 |
| 12 | Truthful purge certificate | Sections 10, 20, 23; AC-035 |
| 13 | Purge cooling-off/final notice | Sections 10, 20, 22, 25; AC-033 |
| 14 | Export waiver/expiry risk | Sections 29 and 39 |
| 15 | Glossary after MESP-52 | Section 37, Approval criteria, and controlled glossary update |
| 16 | Canonical glossary PRD filename | Controlled glossary document-control update |
