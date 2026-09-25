# Identity and Access Management Business Requirements Document

## 1. Document Control

| Field | Value |
|---|---|
| Document title | Identity and Access Management Business Requirements Document |
| Version | v0.3 - Approved Release 1 Baseline |
| Status | Approved Release 1 Baseline |
| Jira | MESP-28 - Produce Identity and Access BRD |
| Parent Epic | MESP-3 - EPIC 03 - Identity and Access Management |
| Accountable owner | Hossam |
| Prepared by | Luna Max, Senior Business Analyst and Product Requirements Lead |
| Date | 3 August 2026 |
| Approval status | Approved by Hossam on 2 August 2026; founder decision record added 3 August 2026 |
| Source baseline | PRD v1.2 Final Approved Baseline; canonical repository file is `docs/MESP_PRD_v1.2.docx` (formerly `MiniERPSaaSPlatform_PRD_v1.2.docx`; contents unchanged) |
| Mandatory vocabulary | docs/00_ERP_Business_Glossary.md |
| Structural reference | docs/11_SaaS_Platform_Administration_BRD.md |
| Architecture reference | docs/01_Technology_Architecture_Baseline.md (constraint reference only) |
| Classification summary | 46 explicit IAM business rules: 46 Confirmed, 0 Proposed; 34 founder-approved decision points recorded across 23 decision records; 4 resolved/provenance source records; 40 business acceptance scenarios |
| Change history | v0.1 Draft for Founder Review was approved and fast-tracked by Hossam on 2 August 2026. v0.2 records the approved Release 1 baseline. v0.3 records the founder-approved global User versus Tenant Membership lifecycle authority decision without changing prior IAM values. |

This document is the approved business-requirements baseline for Release 1. It authorizes downstream requirements/design preparation only; it does not itself authorize implementation code, a Sprint, or implementation Jira work.

### Requirement classification legend

- **Confirmed** - explicitly supported by an approved PRD, glossary, approved decision, Jira requirement, approved MESP-27 boundary, or the founder decisions recorded in section 27. Confirmed rules use **shall**.
- **Founder-approved** - a decision explicitly authorized for the Release 1 baseline and recorded with its implementation gate in section 27.
- **Deferred gate** - a topic intentionally retained for MESP-50, a later BRD, qualified validation, or a later legal/critical-security decision. It is not an unresolved MESP-28 requirement.
- **Out of Scope** - explicitly excluded from MESP-28 or owned by another BRD. It is not a hidden requirement.

The counts above count the stable IAM-BR business-rule register, the 23 founder-approved IAM-OD records, the four IAM-SC source records, and the IAM-AC register. Process, validation, report, transition, and coverage rows are independently classified in their own tables.

## 2. Executive Summary

Identity and Access exists to ensure that a named User can sign in and act only within the Tenant memberships, Roles, Permissions, and Company / Legal Entity, Branch, and Warehouse scopes that the business has granted. It protects the ERP from accidental disclosure, unauthorized transactions, uncontrolled privilege, conflicting duties, and unaccountable changes.

The business boundary is:

> Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse

The Tenant is the subscription and data-isolation boundary. A Company / Legal Entity is a legal and accounting boundary inside a Tenant. A Branch is an operating location, and a Warehouse is a stock-holding location belonging to a Branch. Identity and Access does not redefine these organizational concepts or absorb the detailed Multi-Tenancy or Organization BRDs.

The BRD separates a User identity from a Person or Employee business reference, and separates a Permission from a Tenant-wide Entitlement. Suppliers and Supplier Contacts are external business parties and are not system Users in Release 1. Wafra is Tenant #1 for validation evidence only; no Wafra-specific rule is introduced.

This document remains business-focused. It defines actors, processes, rules, states, evidence, reports, dependencies, migration expectations, and business acceptance scenarios. Approved Release 1 business values, including the approved session values, are defined in this BRD; technical session mechanisms, token/cookie configuration, and framework implementation remain downstream design concerns. Authentication products, protocols, credential algorithms, interface contracts, storage structures, screens, and automated test implementation remain downstream concerns.

## 3. Business Purpose and Objectives

### 3.1 Purpose

Identity and Access provides a controlled account and authorization lifecycle for a multi-tenant B2B ERP. It creates an accountable link between a named User and every material action while preserving the separation between Platform operations, Tenant administration, organizational scope, commercial Entitlement, and security Permission.

### 3.2 Objectives

| Objective | Business outcome | Classification | Source |
|---|---|---|---|
| Controlled access | Only an eligible, active User with an applicable Tenant Membership, Permission, scope, document state, and context may perform an action. | Confirmed | PRD principle least privilege; BR-010 |
| Tenant isolation | A User cannot view or affect another Tenant through a changed context, identifier, support request, report, export, or background action. | Confirmed | PLT-001; BR-010; M27-RULE-001/002 |
| Least privilege | Users receive only the Roles, Permissions, and scopes necessary for their accountable work. | Confirmed | PRD principle; ADM-001; M27-REQ-045 |
| Accountability | Material access, authentication, assignment, approval, denial, suspension, recovery, and revocation events identify the actor, time, Tenant, scope, action, outcome, and safe evidence. | Confirmed | PLT-008; BR-011 |
| Separation of duties | A User is prevented from performing conflicting steps or approving a prohibited self-request where the applicable policy says so. | Confirmed | PRD table 7; D-006; MESP-55 |
| User lifecycle control | Invitations, activation, membership, Role assignment, suspension, deactivation, access revocation, and offboarding are explicit and reviewable. | Confirmed | Jira MESP-28; glossary Tenant Membership |
| Privileged-access governance | High-risk and support access is identifiable, purpose-bound, scoped, time-bound where approved, revocable, and fully evidenced. | Confirmed | ADM-001; M27-REQ-045 through M27-REQ-049 |
| Auditability | Tenant users cannot edit or delete access evidence, and material events can be retrieved for review. | Confirmed | PLT-008; BR-011; glossary Audit Event |
| Business continuity | Suspension, recovery, reactivation, and session termination protect access controls while preserving enough evidence to resume work deliberately. | Confirmed | Founder decisions 8, 13, 27; M27-REQ-054 through M27-REQ-058 |

## 4. Scope

### 4.1 In scope

1. Authentication business expectations and authentication outcomes.
2. User Account lifecycle, invitation, activation, suspension, deactivation, recovery, and closure questions.
3. Tenant Membership creation, acceptance, modification, suspension, revocation, and offboarding.
4. Predefined and custom Role assignment within an authorized scope.
5. Permission meaning and Permission categories needed by the ERP.
6. Access Scope at Tenant, Company / Legal Entity, Branch, and Warehouse levels.
7. Combined evaluation of membership, Role, Permission, Entitlement, scope, document state, and context.
8. Separation of Duties and prohibition of self-approval where the governing policy applies.
9. Session business expectations, including invalidation after critical access changes.
10. Privileged access and support access where it intersects with Identity and Access.
11. Audit evidence for material access and security events.
12. Identity and access reporting and KPIs.
13. Business dependencies with Tenant lifecycle, Organization, Audit, Files and exports, Notifications, Reporting, and Migration.
14. Migration of existing Users and access assignments, including reconciliation and exception ownership.
15. Business-level Given/When/Then acceptance scenarios.

### 4.2 Boundary statement

Identity and Access owns the User, Tenant Membership, Role, Permission, and access-governance meaning. MESP-29 owns detailed Multi-Tenancy lifecycle and isolation behavior. MESP-30 owns detailed Company / Legal Entity, Branch, and Warehouse organization behavior. Identity and Access consumes their approved business identities and relationships without redefining them.

## 5. Out of Scope

The following are explicitly out of scope for this BRD:

- Technical design, component design, and deployment design.
- API design, interface payloads, endpoint naming, or protocol selection.
- Database design, persistence structures, schemas, keys, migrations, or query design.
- Screen, page, control, navigation, visual, or UI component specifications.
- Source code, framework configuration, credential algorithms, token formats, cookie settings, or identity-provider product selection.
- Automated test implementation, test scripts, performance scripts, or test-case documents. Section 26 contains business acceptance scenarios only.
- Detailed MESP-29 Multi-Tenancy requirements beyond identity boundaries and denial rules referenced here.
- Detailed MESP-30 Organization requirements beyond the approved hierarchy and scope references.
- Procurement, Inventory, Finance, B2B Sales, or Retail POS transactions.
- Production billing, per-Tenant Entitlement overrides, or commercial Plan behavior owned by MESP-27.
- Supplier or Supplier Contact system accounts. Suppliers remain external business parties.
- Wafra-specific roles, workflows, reports, volume limits, or core behavior.
- Identity and Access implementation Stories, Enablers, Tasks, Bugs, or a Sprint.
- Implementation code, a Sprint, implementation Stories, or implementation refinement. The approved baseline feeds the lean downstream specification gate.

## 6. Source Traceability

| Source | Source requirement or decision | BRD section(s) | Classification | Notes / unresolved gap |
|---|---|---|---|---|
| Jira MESP-28 | Required business purpose, actors, triggers, workflows, exceptions, rules, states, data, validation, permissions, approvals, SoD, impacts, reports, audit, integration, migration, GWT scenarios, decisions, and owner approval | 2-30 | Confirmed | This approved v0.3 Release 1 baseline remains separately gated from implementation. |
| Jira MESP-28 | Authentication, tenant membership, Users, Roles, Permissions, Access Scopes, SoD, session handling, privileged access | 4, 7-16, 19, 22 | Confirmed | Release 1 values are approved in section 27; MESP-50 and later legal/critical-security gates remain protected. |
| PRD PLT-003 | Authorized Users can create, review, activate, deactivate, import, export, and search shared master data with validation and duplicate detection | 14, 17, 18, 21, 24 | Confirmed | Applied to access administration; domain master-data behavior remains outside this BRD. |
| PRD PLT-004 | Each business document has a unique human-readable number and immutable internal identity | 9, 23 | Confirmed | Identity and Access controls access to documents; numbering is owned by Organization. |
| PRD PLT-005 | Approval requirements can vary by document type, amount threshold, Branch, and User Role; policy versions remain linked to historical decisions | 15, 16, 19, 20, 22 | Confirmed | Release 1 uses Tenant Administrator administration for ordinary assignments and one separate named approver for privileged assignments; later policy may add dual approval. |
| PRD BR-010 | Enforce Tenant, Company, Branch, Warehouse, module, Role, document-state, and contextual access | 2, 3, 9, 13, 14, 18, 22, 26 | Confirmed | Release 1 scope inheritance is downward only; combined grants never inherit upward and explicit deny rules are excluded. |
| PRD BR-011 | Complete audit evidence for material business, configuration, access, posting, reversal, and support actions | 3, 20-23, 26 | Confirmed | Evidence is required; no automated purge is implemented and retention/privacy/legal-hold/purge remain MESP-50 production gates. |
| PRD section 8 | Authorization combines membership, Role Permissions, business scope, document state, and context; server-side enforcement is mandatory; support modern password, session, MFA capability, expiry, and security events | 3, 9, 13-16, 19, 22 | Confirmed | Password authentication, MFA capability, mandatory MFA populations, fresh authentication for high-risk operations, and business session values are approved here; technical enforcement remains out of scope. |
| PRD ADM-001 | Tenant administrators assign predefined or custom Roles within authorized scope; high-risk Permissions are identifiable and auditable | 7, 14-16, 22 | Confirmed | Tenant Administrators may compose custom Roles only from Platform-approved Permissions; only Platform governance defines Permission types. |
| PRD table 7 | Baseline roles and separation concerns for Tenant Administrator, Requester, Buyer, Warehouse Operator, Sales User, Accountant, Finance Approver, and Auditor | 7, 14, 15 | Confirmed | Final role catalogue remains an approval output. |
| PRD table 17 | Privileged access and audit activity are reportable; authorized SaaS/Admin reports reconcile to platform events and support records | 21, 22 | Confirmed | Report definitions are business-level only. |
| PRD table 18 | Roles/access matrix and privileged-access model are required BRD outputs | 7, 14-16, 21, 27, 29 | Confirmed | Approval roles include business owners and security. |
| PRD D-005/D-006/D-007/D-009 | Arabic/English and RTL; versioned approval policy; hierarchy; B2B-only Release 1 | 2, 14-16, 25 | Confirmed | No Retail POS behavior is added. |
| PRD KSA-005/KSA-006/KSA-007 | Bilingual terminology/RTL, privacy baseline, residency/transfer/support-access decisions | 20, 23, 25, 27 | Confirmed / Deferred gate | MESP-50 remains the production gate for privacy, residency, retention, legal hold, and purge; no legal conclusion is invented. |
| docs/Decisions.md ADR-003 | Shared-database tenant isolation is an approved architecture baseline; detailed controls before tenant-scoped persistence | 9, 23, 28 | Confirmed boundary | No database design is introduced here. |
| docs/Decisions.md ADR-004 | Identity cookie, antiforgery, session, and MFA policy required before authentication/privileged-session implementation | 9, 19, 27, 30 | Confirmed dependency | Exact policy is a later architecture/production gate. |
| docs/Decisions.md ADR-005 | Policy and resource authorization baseline; detailed permissions before affected implementation | 9, 14-16, 27 | Confirmed dependency | This BRD owns business meaning, not technical enforcement. |
| docs/Decisions.md ADR-011/014 | Localization/RTL and residency, retention, legal hold, export, and purge remain controlled gates | 20, 23, 25, 27 | Confirmed dependency | No retention duration or legal conclusion is invented. |
| docs/Decisions.md MESP-55 | One named approver, controlled administrator reassignment, no self-approval; defer parallel approval and automatic escalation | 15, 16, 27 | Confirmed | Applied as the Release 1 direction; dual approval remains excluded unless a later legal or critical-security decision requires it. |
| docs/Decisions.md MESP-56 | Multiple legal entities in a Tenant without consolidation or intercompany automation | 2, 9, 14, 25 | Confirmed boundary | Organization details belong to MESP-30. |
| docs/Decisions.md MESP-50 | Residency, retention, legal hold, support access, export, and purge require production validation | 20, 22-25, 27 | Deferred gate | Release 1 implements no automated audit-evidence purge; MESP-50 remains the production gate. |
| Glossary User, Person/Employee | User is a login identity; Employee/Person is a business reference; access is granted to Users | 7-9, 13, 17 | Confirmed | No HR module is implied. |
| Glossary Tenant Membership, Role, Permission, Access Scope | Explicit membership; Role is a reusable Permission bundle; Permission is atomic; scope is a data boundary | 8, 14, 18, 19 | Confirmed | Founder approved downward-only inheritance, combined grants, no upward inheritance, and no explicit deny rules for Release 1. |
| Glossary Separation of Duties, Approver | SoD prevents conflicting steps; approver is a User with defined authority | 15, 16, 27 | Confirmed | Founder approved no self-approval and separability of buying, receiving, and payment-release responsibilities; detailed implementation remains gated. |
| Glossary Audit Event, Supplier | Audit evidence is immutable to Tenant Users; Suppliers are not Users | 5, 7, 20, 22 | Confirmed | Supports explicit scope protection. |
| MESP-27 approved BRD | Platform/Tenant administrator boundary; named, case/time/scope support access; session invalidation; suspension/reactivation; offboarding; no support export authority; Wafra neutrality | 7, 9-13, 15, 19-24, 28 | Confirmed | MESP-27 detailed platform lifecycle remains authoritative for its own domain. |
| MESP-27 M27-REQ-045-049 | Named, one-Tenant, case/purpose/time-bound support; no shared superuser; expiry/revocation; emergency access separately governed | 7, 11, 12, 15, 19, 22, 27 | Confirmed | Release 1 requires Tenant approval and an eight-hour maximum; break-glass access is excluded. |
| MESP-27 M27-RULE-001/006/012/013/017/018 | Server-established Tenant context; Entitlement/Permission distinction; consistent suspension; reactivation reevaluation; named least-privilege support; no hidden support superuser | 9, 13-16, 19, 22, 26 | Confirmed | Identity-specific interpretation is captured by IAM-BR-005 onward. |
| MESP-27 M27-AC-026-028 | Authorized support, automatic expiry, cross-Tenant support denial | 11, 12, 22, 26 | Confirmed | Used as business acceptance evidence. |
| MESP-27 M27-OQ-005 | When Tenant authorization is mandatory for normal/emergency support | 27, 28, 30 | Confirmed / Deferred gate | Tenant approval is mandatory for Release 1 support access; emergency/break-glass access is excluded and any later change requires a separate decision. |

## 7. Business Actors and Responsibilities

The actors below are supported by the PRD, glossary, MESP-27, or the MESP-28 Jira Epic. A Company, Branch, or Warehouse-scoped User is an access-scope assignment, not automatically a separate Role or actor.

| Actor | Business responsibility | Permitted scope | Prohibited or constrained actions | Approval responsibility | Audit responsibility | Classification |
|---|---|---|---|---|---|---|
| Hossam / Product Owner and Business Sponsor | Approves the BRD, product decisions, exceptions that require founder authority, and sequencing. | Platform-level governance. | Founder approval is recorded; implementation remains separately gated by the Product Delivery Master Plan. | Approval evidence for this document and its resolved decisions. | Ensures approval evidence is recorded. | Confirmed |
| Platform Administrator | Coordinates platform-level Tenant and lifecycle administration and hands authorized administration to the Tenant. | Platform metadata and explicitly authorized platform operations; global User lifecycle only when the specific global User lifecycle Permission is present; no Tenant business data by default. | Cannot grant self-approval, bypass Entitlements, create tenant-specific behavior, suspend a global User without the specific governance Permission, or access Tenant business data without approved support. | Platform lifecycle or support approval where policy assigns it. | Accountable for platform administration and global-lifecycle evidence. | Confirmed |
| Platform Security Administrator / Platform Administrator with global User lifecycle Permission | Controls the global User identity lifecycle when separately assigned the Platform governance Permission. | Platform/global User identity and all affected User sessions, with no Tenant business-data authority. | May not use the global lifecycle Permission to inspect or mutate Tenant business data, and may not omit active authentication, MFA, fresh authentication, reason, concurrency, idempotency, or immutable audit evidence. | May suspend, reactivate, or offboard a global User only under the approved governance control. | Records the global action, reason, affected sessions, actor, time, outcome, and evidence. | Founder-approved |
| Platform Operations Owner | Owns operational recovery, notifications, jobs, access evidence, and readiness. | Approved platform operations. | Cannot infer wider Tenant or action scope than the initiating record. | Operational exception or recovery approval where assigned. | Ensures failures and retries remain visible. | Confirmed |
| Security / Privacy Owner | Reviews privileged access, support boundaries, privacy, retention, export, and production controls. | Security and privacy evidence, not ordinary Tenant business operation. | Must not be the sole requester and approver of an irreversible action where dual control is required. | Privileged/support/security review where policy assigns it. | Reviews security evidence and exceptions. | Confirmed |
| Tenant Administrator | Manages the Tenant's Users, memberships, Roles, Permissions, organization scope assignments, and tenant-level configuration within governed options. | One authorized Tenant and its Company / Legal Entity, Branch, and Warehouse hierarchy. | May suspend or revoke a Tenant Membership, revoke that Tenant's Role Assignments or AccessScopeGrants, invalidate sessions operating in that Tenant, and block selection of that Tenant; cannot suspend, reactivate, or offboard the global User identity, and cannot affect another Tenant. | Assigns or approves Tenant access where the policy assigns that responsibility. | Reviews Tenant access, membership containment, session invalidation, and support authorization evidence. | Founder-approved |
| Tenant business User | Performs an approved business function such as requesting, buying, warehouse operation, sales, accounting, finance approval, or audit. | Membership and assigned Role/Permission/Access Scope. | Cannot exceed scope, approve prohibited self-actions, or access another Tenant. | May approve only where an explicit Role/Permission and policy allow it. | Every material action is attributable to the named User. | Confirmed |
| Auditor / read-only User | Reviews authorized reports, configuration, access, evidence, and audit history. | Assigned read-only scope. | No transactional or access mutation. | No approval unless a separate approved Role grants it. | Records review outcome where applicable. | Confirmed |
| Authorized Support User | Investigates a named support case under approved Tenant, purpose, scope, and time. | One Tenant and the exact approved support scope. | No shared credential, hidden superuser, unrestricted impersonation, standing access, export authority, or global User suspension/reactivation/offboarding authority from SupportGrant alone. | Support access and any separate export authorization as required. | All authentication, records/actions accessed, changes, downloads, expiry, revocation, and closure are evidenced. | Confirmed |
| Named Privileged-Access Approver | Performs the business approval role for high-risk or privileged access when assigned. | The decision scope in the approval request. | Cannot self-approve a prohibited request. The detailed Separation of Duties catalogue is deferred to MESP-38 and must not weaken the approved Release 1 controls. | Approves or rejects the named request. | Approval reason, actor, decision, and time are retained. | Confirmed |
| Background Operator | Executes an already authorized business operation or recovery action. | The Tenant and scope recorded by the initiating business action. | Cannot expand Tenant or action scope. | No independent privilege beyond the approved work. | Records outcome and failure/retry evidence. | Confirmed |

No separate Platform Customer, Company Administrator, Branch Administrator, or Warehouse Administrator Role is approved by this BRD. Such Roles may be proposed only through the governed Role catalogue and open decisions, not assumed as new actors.

## 8. Business Terminology

The following definitions use the global glossary. Founder approval in this BRD establishes the Release 1 business meaning; the global glossary may be synchronized later through normal change control without changing this baseline.

| Term | Business meaning used here | Boundary / status |
|---|---|---|
| User Account | The account state and authentication identity through which a User may sign in using required password authentication and applicable MFA. | User is an authenticated identity; Release 1 states are defined in section 10. |
| User | An authenticated identity that can act inside one or more Tenants according to granted Roles and Permissions. | Not an Employee, Supplier, or Supplier Contact. Confirmed glossary term. |
| Person / Employee | A business person reference used for attribution such as requester, buyer, salesperson, or approver. | Not a login and not an access grant. Detailed Employee behavior belongs to MESP-30. |
| Tenant Membership | The explicit link granting a User access to one Tenant with applicable Roles and Access Scope. | Revocable without deleting the User identity. Confirmed glossary term. |
| Role | A named, reusable bundle of Permissions assigned to Users to express a job function. | Not a job title or approval authority by itself. Confirmed glossary term. |
| Permission | An atomic User-level right to perform an action on an object type. | Not a Tenant Entitlement. Confirmed glossary term. |
| Access Scope | The data boundary within which the User's Permissions apply. | Release 1 grants inherit downward Tenant -> Company / Legal Entity -> Branch -> Warehouse, never upward; explicit deny rules are excluded. |
| Company Scope | Access bounded to a Company / Legal Entity inside a Tenant. | Company owns legal/accounting meaning; organization rules are MESP-30. |
| Branch Scope | Access bounded to a Branch inside a Company. | A Branch is not a Warehouse. |
| Warehouse Scope | Access bounded to a Warehouse inside a Branch. | Warehouse is the lowest approved hierarchy level for stock location. |
| Privileged Access | High-risk access that can affect security, access administration, support, configuration, or material evidence. | Requires one separate named approver and fresh authentication for high-risk operations. |
| Support Access | Explicit, named, case-bound, least-privilege, Tenant-bound, Tenant-approved access by an authorized support User. | Exact scope is required; maximum Release 1 duration is eight hours; extensions require fresh approval; support never grants export authority. |
| Session | A period in which an authenticated User may continue an authorized interaction. | Release 1 maximum is eight hours with thirty-minute inactivity timeout; concurrent sessions are permitted. |
| Account Suspension | A temporary restriction preventing some or all access while the identity or membership is retained. | Five failed attempts cause a fifteen-minute temporary lockout; suspension and other critical changes revoke affected sessions. |
| Account Deactivation | A deliberate state in which the User Account or membership no longer permits ordinary operation. | Reactivation requires fresh review and does not automatically restore previous Roles, scopes, or privileged access. |
| Access Revocation | Removal or disabling of a Role, Permission, scope, membership, support authorization, or session. | Must be evidenced and take effect according to the approved business state. |
| Separation of Duties | A control preventing one User from performing two conflicting steps of the same business transaction. | No self-approval; buying, receiving, and payment-release responsibilities remain separable. Detailed legal/critical-security additions remain gated. |
| Least Privilege | The principle that access is limited to the minimum authorized Tenant, organizational scope, function, and time needed for the work. | Confirmed product principle. |
| Entitlement | A Tenant-wide commercial right derived from Plan and Subscription. | Not a User Permission and not a per-Tenant override. |

### Glossary follow-up

The global glossary is not changed in this task. Its Access Scope and Separation of Duties entries may be synchronized to this approved Release 1 meaning through a later controlled documentation update.

## 9. Assumptions, Dependencies, and Boundaries

| Boundary or dependency | MESP-28 treatment | Classification / owner |
|---|---|---|
| SaaS Platform Administration (MESP-27) | Supplies Platform/Tenant administration boundaries, Plan/Subscription/Entitlement distinction, support controls, suspension, reactivation, offboarding, and evidence expectations. | Confirmed dependency; MESP-27 |
| Multi-Tenancy (MESP-29) | Owns Tenant isolation, tenant lifecycle, and detailed tenant context. MESP-28 requires membership and denial but does not define the full tenancy model. | Confirmed boundary; MESP-29 |
| Organization (MESP-30) | Owns Company / Legal Entity, Branch, Warehouse identity and relationships. MESP-28 consumes valid scope references. | Confirmed boundary; MESP-30 |
| Security and Audit (MESP-38) | Owns detailed security evidence, SoD matrix, retention, and data-governance controls. MESP-28 provides access events and business rules. | Confirmed dependency; MESP-38 |
| Files and exports | Access to files/exports must be authorized in the same Tenant and scope. Support access never supplies export authority alone. | Confirmed boundary; MESP-27/MESP-39 |
| Notifications | Invitations, assignments, approvals, failures, suspension, recovery, and material exceptions require visible business evidence; the delivery channel remains an implementation/detail gate. | Confirmed need / downstream specification |
| Background processes | Background work cannot expand Tenant or scope and must respect suspension/revocation decisions. | Confirmed dependency; MESP-27 |
| Reporting | Reports and KPIs expose only authorized Users and identify freshness/data-as-of information when asynchronous preparation is involved. | Confirmed dependency; MESP-36 |
| Migration | Existing identities and assignments require source ownership, mapping, reconciliation, and exception approval. | Confirmed dependency; MESP-40 |
| Architecture baseline | Approved technology direction is a feasibility constraint only: Modular Monolith, ASP.NET Core Identity direction, secure first-party session direction, and policy authorization. | Confirmed reference; ADR-004/005 |
| Wafra | Wafra supplies validation evidence only. The BRD remains generic for future Tenants. | Confirmed boundary; MESP-24/M27-RULE-003 |
| B2B Release 1 | Identity supports B2B ERP roles and controls. Retail POS actors and workflows remain excluded. | Confirmed boundary; PRD D-009 |

## 10. Identity and Access Business Lifecycle

The lifecycle below is the approved Release 1 business state model. It records business outcomes only; technical authentication, session, and persistence design remain downstream work.

### 10.1 User Account lifecycle

| Working stage | Business meaning | Entry / exit expectation | Classification |
|---|---|---|---|
| Invitation issued | An authorized Tenant Administrator has invited a proposed User to a Tenant. | The normalized email is checked globally; invitation evidence is recorded. | Confirmed |
| Pending activation | The invited identity has not completed the approved activation path. | Invitation is valid for seven days, may be withdrawn or reissued, and cannot be transferred. | Confirmed |
| Active | The User may authenticate with password authentication and applicable MFA, then act only where membership, Permission, scope, Entitlement, document state, and context allow. | Account or authority changes can invalidate sessions. | Confirmed |
| Locked | Authentication is temporarily prevented after five failed attempts. | The temporary lockout lasts fifteen minutes; the event and outcome are evidenced. | Confirmed |
| Suspended | Access is restricted for a recorded security, administrative, or other approved reason. | Affected sessions are revoked; restoration requires the approved review path. | Confirmed |
| Deactivated | Ordinary operation is no longer permitted for the account or membership. | Identity and evidence are retained; reactivation requires fresh review and does not restore prior Roles, scopes, or privileged access automatically. | Confirmed |

### 10.2 Tenant Membership lifecycle

1. A User may hold multiple explicit memberships in different Tenants after eligibility and authorization checks.
2. Each membership carries the Roles and Access Scope that apply only within that Tenant.
3. Tenant context switching is permitted only between the User's explicit memberships and never expands authority.
4. Membership changes are effective only through an authorized business action and are evidenced.
5. Suspension, revocation, or offboarding removes the membership's ability to authorize Tenant operations without deleting the global User identity.

Classification: the explicit, revocable, multi-Tenant membership link and isolated context switching are **Confirmed**.

### 10.3 Role Assignment lifecycle

| Stage | Business expectation | Classification |
|---|---|---|
| Administered | Ordinary Role, Permission, and scope assignments are administered by an authorized Tenant Administrator. | Confirmed |
| Reviewed | Eligibility, effective scope, combined grants, and SoD controls are checked. | Confirmed |
| Approved | A separate named approver authorizes a privileged assignment; ordinary assignments do not require a separate approver unless a later policy says otherwise. | Confirmed |
| Active | The Role/Permission assignment authorizes actions only within its approved downward scope. | Confirmed |
| Modified | A change creates new effective evidence without erasing historical decisions; removing a parent scope removes inherited authority and invalidates affected sessions. | Confirmed |
| Revoked | The assignment no longer authorizes actions and affected sessions are revoked. | Confirmed |
| Reviewed quarterly/event-driven | An auditable report supports quarterly and event-driven manual access review. | Confirmed |

### 10.4 Privileged Access lifecycle

1. A privileged request identifies the named User, Tenant, exact scope, business purpose, risk, requested actions, and effective interval.
2. A separate named approver reviews the request and any SoD conflict; self-approval is prohibited.
3. Privileged Users and privileged operations require MFA and fresh authentication for each high-risk operation.
4. Approved access is limited to the approved purpose, Tenant, downward scope, and interval; support access has an eight-hour maximum.
5. Support extensions require fresh approval. Support authorization never grants export authority.
6. Activity, denied actions, changes, expiry/revocation, and closure are evidenced.
7. Break-glass access is excluded from Release 1. Dual approval is excluded unless a later legal or critical-security decision requires it.

Classification: the privileged-access lifecycle is **Confirmed** for Release 1.

### 10.5 Session lifecycle

| Business stage | Expectation | Classification |
|---|---|---|
| Created | Password authentication and required MFA capability establish a session for the authenticated User and selected authorized Tenant context. | Confirmed |
| Continued | An ordinary session has an eight-hour maximum and a thirty-minute inactivity timeout. Concurrent sessions are permitted. | Confirmed |
| Context changed | Changing Tenant context requires an explicit membership and cannot expand access. | Confirmed |
| Logged out | The User can end the current session through the approved logout behavior. | Confirmed |
| Forced termination | Password reset, suspension, offboarding, and critical authority changes revoke affected sessions. | Confirmed |
| Expired / revoked | The session no longer authorizes actions after the approved maximum, inactivity, or revocation condition. | Confirmed |
| Privileged operation | MFA and fresh authentication are required for privileged Users and high-risk operations. | Confirmed |

## 11. Main Business Processes

Each narrative is intentionally business-level. Inputs, actors, decisions, outputs, and evidence are described without prescribing screens, interfaces, persistence, or implementation.

### IAM-PR-001 - Invite a new Tenant User

- **Classification:** Confirmed process under the approved seven-day invitation policy.
- **Trigger:** An authorized Tenant Administrator or other approved inviter identifies a business need for access.
- **Preconditions:** Target Tenant is eligible for access administration; inviter has the required Permission; target identity and requested scope are supplied.
- **Main flow:** Validate the globally unique normalized email and eligibility conditions; select the requested Tenant Membership, Role, and Access Scope; record the invitation; notify the intended User through the approved channel; retain the invitation evidence.
- **Alternative / exception:** Duplicate or ineligible identity is rejected or held for reviewed resolution; an invitation expires after seven days, may be withdrawn or reissued, cannot be transferred, and cannot silently grant a wider scope.
- **Output and evidence:** Pending invitation, requested assignment, inviter, reason, time, Tenant, scope, notification outcome, and audit event.

### IAM-PR-002 - Activate a User Account

- **Classification:** Confirmed process under the approved password, MFA, and invitation policy.
- **Trigger:** The intended User accepts a valid invitation or an authorized administrator activates an eligible identity.
- **Preconditions:** Invitation or activation authority is valid; target Tenant and requested assignment remain active.
- **Main flow:** Confirm the globally unique normalized email; complete password authentication setup and required MFA enrollment/capability; activate the User Account, Tenant Membership, and approved assignments; record evidence.
- **Alternative / exception:** Expired, duplicate, withdrawn, transferred, or invalid invitation is rejected; an account may not become operational when its Tenant is suspended or its scope is inactive.
- **Output and evidence:** Activation outcome, account/membership state, assigned Roles/scopes, actor, time, and audit event.

### IAM-PR-003 - Authenticate an Active User

- **Classification:** Confirmed process; password authentication is required and MFA is mandatory for Platform Administrators, Support Users, Tenant Administrators, and privileged operations.
- **Trigger:** A User attempts to start an authorized session.
- **Preconditions:** Account and membership are eligible; Tenant is not in a state that prohibits the requested access; required authentication policy is satisfied.
- **Main flow:** Evaluate password authentication and applicable MFA; establish the User identity and eligible Tenant context; evaluate access before any protected action; require fresh authentication for high-risk operations; record the outcome.
- **Alternative / exception:** Suspended, deactivated, unverified, or unauthorized identities are denied without exposing another Tenant's data.
- **Output and evidence:** Success or denial outcome, User identity, Tenant context, time, reason category, and audit/security evidence.

### IAM-PR-004 - Handle Failed Authentication

- **Classification:** Confirmed process; five failed attempts cause a fifteen-minute temporary lockout.
- **Trigger:** Authentication evidence is missing, invalid, expired, or otherwise rejected.
- **Preconditions:** An authentication attempt can be associated with an identity or safely recorded as an unknown attempt.
- **Main flow:** Deny the attempt; provide a safe business outcome; record the failed outcome and risk signal; apply the approved protective response when its policy condition is met.
- **Alternative / exception:** On the fifth failed attempt the User is temporarily locked for fifteen minutes; the User, Tenant, and security outcome are evidenced without exposing another Tenant.
- **Output and evidence:** Denial, risk/lockout state if applicable, notification/escalation outcome, and audit/security evidence.

### IAM-PR-005 - Recover Account Access

- **Classification:** Confirmed process using verified-email self-service.
- **Trigger:** An eligible User reports loss of access and requests recovery through the verified email channel.
- **Preconditions:** The normalized email identifies the User; the recovery claimant completes the approved email verification; Tenant and membership state are known.
- **Main flow:** Verify the claimant through the verified email self-service path; allow the User to establish a new credential without exposing the prior credential; revoke affected sessions; require applicable MFA again; record the decision and notify responsible actors.
- **Alternative / exception:** Administrators cannot choose, view, or directly set User passwords. A deactivated, suspended-for-security, ambiguous, or compromised identity is not restored automatically; fresh review and evidence are required.
- **Output and evidence:** Recovery request, verification outcome, decision, affected memberships/sessions, actor, reason, and audit evidence.

### IAM-PR-006 - Assign Tenant Membership

- **Classification:** Confirmed process; multiple explicit Tenant Memberships are permitted.
- **Trigger:** A User requires access to an additional or initial Tenant.
- **Preconditions:** Target Tenant exists and is eligible; assigning actor is authorized; User, Role, scope, and business purpose are valid.
- **Main flow:** Confirm that the target Tenant and organizational scopes are related; assign the explicit membership; assign Roles/scopes through the authorized Tenant Administrator path; notify the User and responsible administrator; record evidence.
- **Alternative / exception:** Cross-Tenant assignment, inactive Tenant/scope, duplicate normalized email, or conflict is rejected; each membership remains isolated and context switching cannot expand authority.
- **Output and evidence:** Membership decision, assignments, actor, effective condition, notification, and audit event.

### IAM-PR-007 - Assign or Change a Role

- **Classification:** Confirmed process; ordinary assignments are administered by an authorized Tenant Administrator and privileged assignments require one separate named approver.
- **Trigger:** A Role is requested, changed, or revoked for a User.
- **Preconditions:** User has a valid membership; Role is available for the target scope; requested Permissions do not violate known controls.
- **Main flow:** Review requested business function and downward scope; check high-risk and SoD controls; obtain the required named approval for privileged assignments; activate or change the Role; preserve previous assignment evidence.
- **Alternative / exception:** Unavailable Role, conflicting duties, insufficient authority, self-approval, or a Permission not approved by Platform governance is rejected. Self-service access requests are excluded from Release 1.
- **Output and evidence:** Role assignment outcome, effective scope, decision, approver where required, and audit evidence.

### IAM-PR-008 - Assign Company, Branch, or Warehouse Scope

- **Classification:** Confirmed process under downward-only inheritance.
- **Trigger:** A User's operating responsibility changes.
- **Preconditions:** Company, Branch, or Warehouse belongs to the target Tenant and is active for assignment.
- **Main flow:** Validate the hierarchy; grant the minimum required scope; allow combined grants at the same or lower levels; never inherit upward; check conflicts and existing sessions; record the changed scope and effective outcome.
- **Alternative / exception:** Inactive or unrelated scope is rejected; explicit deny rules are not used in Release 1; removing a parent scope removes inherited authority and invalidates affected sessions.
- **Output and evidence:** Scope assignment/revocation, before/after scope summary, actor, reason, and audit event.

### IAM-PR-009 - Request and Approve Privileged Access

- **Classification:** Confirmed process; MFA, fresh authentication, one separate named approver, eight-hour support maximum, and no Release 1 break-glass access apply.
- **Trigger:** A User needs high-risk access beyond ordinary Role assignment.
- **Preconditions:** Business purpose, requested Tenant/scope/actions, risk, and named requester are supplied.
- **Main flow:** Review the business need, Platform-approved Permission, downward scope, SoD conflicts, and separate approver eligibility; require MFA and fresh authentication for the high-risk operation; approve or reject; activate only the approved scope; monitor and close the request.
- **Alternative / exception:** Self-approval, conflicting duty, missing purpose, unsupported Tenant, unapproved Permission, or expired request is rejected. Break-glass access is excluded and dual approval is not required unless a later legal or critical-security decision changes the policy.
- **Output and evidence:** Request, justification, approval/rejection, activation/revocation/expiry, activity summary, and audit events.

### IAM-PR-010 - Review Existing Access

- **Classification:** Confirmed process using an auditable report and manual review.
- **Trigger:** A quarterly review cycle or material access, responsibility, or security event occurs.
- **Preconditions:** Current memberships, Roles, Permissions, scopes, privileged assignments, and open exceptions are available to the reviewer.
- **Main flow:** Produce the auditable access report; compare access to current responsibility; retain, modify, suspend, or revoke assignments; resolve exceptions; record the manual review outcome.
- **Alternative / exception:** Missing owner, ambiguous mapping, or conflict remains open with an accountable owner and due decision.
- **Output and evidence:** Review result, changed assignments, exceptions, reviewer, time, and audit evidence.

### IAM-PR-011 - Suspend a User

- **Classification:** Confirmed process; suspension and the approved temporary lockout revoke affected sessions.
- **Trigger:** Security, administrative, Tenant, or other authorized reason requires access restriction.
- **Preconditions:** Authorized actor, reason, affected Tenant/scope, effective time, and restoration condition are identified.
- **Main flow:** Record the suspension; invalidate affected sessions where required; deny prohibited interactive and non-interactive access; notify responsible actors; retain evidence.
- **Alternative / exception:** A suspension may permit a policy-approved read-only mode, but this is never assumed.
- **Output and evidence:** Suspension state, reason, actor, scope, effective time, access mode, session/job outcome, notice, and audit event.

The command is split by ownership. `SuspendUser`, `ReactivateUser`, and
`OffboardUser` are global User lifecycle actions and require the Platform
Security Administrator, or a Platform Administrator with the specific global
User lifecycle Permission, an active authenticated session, MFA, operation-bound
fresh authentication, a reason, immutable audit evidence, optimistic
concurrency, and idempotency. A Tenant Administrator must not use these commands
to change the global identity. A Tenant Administrator instead uses
`SuspendMembership` or `RevokeMembership` for one Tenant and may revoke that
Tenant's Role Assignments and AccessScopeGrants and invalidate sessions operating
in that Tenant.

### IAM-PR-012 - Revoke Membership or Access

- **Classification:** Confirmed process with immediate authority removal for the affected assignment and sessions.
- **Trigger:** A User no longer needs a Role, Permission, scope, membership, or support authorization.
- **Preconditions:** Authorized revoker and affected assignment are identified.
- **Main flow:** Remove or disable the assignment; invalidate affected sessions; prevent future actions; preserve historical evidence and the global User identity where applicable.
- **Alternative / exception:** A request that would remove the last required administrator is held for approved replacement or controlled reassignment.
- **Output and evidence:** Revocation outcome, affected sessions, reason, actor, and immutable audit event.

Membership containment is strictly Tenant-scoped. It blocks selection and
operation in the affected Tenant but does not change the global User, another
Tenant Membership, Role, scope, or session operating only in another Tenant.

### IAM-PR-013 - Offboard a User

- **Classification:** Confirmed process; reactivation requires fresh review and never restores prior authority automatically.
- **Trigger:** Employment, contract, Tenant membership, or business responsibility ends.
- **Preconditions:** Tenant and identity ownership are confirmed; open privileged/support access and sessions are identified.
- **Main flow:** Revoke memberships, Roles, Permissions, scopes, support authorizations, and active sessions; preserve required evidence; reconcile dependent assignments; notify responsible owners.
- **Alternative / exception:** Ambiguous ownership or migration conflict is held for business-owner approval rather than silently deleting access; reactivation does not restore previous Roles, scopes, or privileged access automatically.
- **Output and evidence:** Offboarding decision, revoked assignments, session outcome, outstanding dependencies, and audit evidence.

### IAM-PR-014 - Terminate Active Sessions after a Critical Access Change

- **Classification:** Confirmed process for password reset, suspension, offboarding, and critical authority changes.
- **Trigger:** Account, membership, Role, Permission, scope, Tenant, or security state changes in a way that could invalidate existing authority.
- **Preconditions:** Affected User, Tenant, scope, change reason, and effective time are known.
- **Main flow:** Identify affected sessions; revoke them when password reset, suspension, offboarding, or a critical authority change becomes effective; require a fresh authorization outcome before further work; record evidence.
- **Alternative / exception:** Unaffected sessions remain governed by their own valid context; no session may retain a revoked authority.
- **Output and evidence:** Session termination outcome, reason, affected scope, actor, and audit event.

### IAM-PR-015 - Support Access involving a Tenant

- **Classification:** Confirmed process requiring Tenant approval, a named case, named User, exact scope, and a maximum duration of eight hours.
- **Trigger:** A valid support case needs investigation or controlled assistance.
- **Preconditions:** Named support User, named case, business purpose, Tenant approval, exact scope, start/end condition, and notification requirements are recorded.
- **Main flow:** Authorize the exact support boundary; allow only approved actions; record all access and changes; expire or revoke access; review and close the case.
- **Alternative / exception:** A different Tenant, purpose, scope, export, or extension requires fresh approval; support authorization alone never grants export authority, and the eight-hour maximum cannot be extended without that fresh approval.
- **Output and evidence:** Case, authorization, access period, activity, downloads, revocation/expiry, outcome, and audit evidence.

### IAM-PR-016 - Handle an Attempted Cross-Tenant Access Action

- **Classification:** Confirmed process.
- **Trigger:** A User or support identity attempts to use a Tenant, record, report, file, export, or context outside the authorized membership.
- **Preconditions:** The attempted target and current authorized context can be evaluated safely.
- **Main flow:** Deny the action without disclosing the other Tenant; preserve the current valid context; record a security/audit event; escalate when risk policy requires.
- **Alternative / exception:** Approved platform-level metadata access remains limited to the closed Platform-owned boundary; it does not grant Tenant business-data access.
- **Output and evidence:** Denial, safe reason category, attempted scope, actor, time, and security evidence.

## 12. Alternative Paths and Exception Scenarios

| Scenario | Expected business handling | Classification / owner |
|---|---|---|
| Duplicate invitation | Do not create a second authoritative membership or invitation; show a reviewable duplicate outcome. | Confirmed; PLT-003/M27-REQ-025 |
| Expired invitation | Reject activation after seven days and require a new authorized invitation; withdrawal and reissue are allowed, transfer is not. | Confirmed; founder decision 9 |
| User already belongs to another Tenant | Retain separate membership boundaries and permit a second explicit membership after eligibility checks; do not infer access. | Confirmed; founder decision 1 |
| User belongs to multiple Tenants | Permit explicit multiple memberships; each context remains isolated and context switching cannot expand authority. | Confirmed; founder decision 1 |
| Role no longer available | Do not activate a stale assignment; route to an authorized Tenant Administrator or named privileged approver as applicable. | Confirmed; founder decisions 16/19 |
| Scope removed while User is active | Remove inherited authority from the affected parent and invalidate affected sessions immediately. | Confirmed; founder decisions 30-33 |
| Approver unavailable | Use controlled administrator reassignment where authorized; no automatic escalation or dual approval is assumed in Release 1. | Confirmed; founder decisions 20-21 |
| User attempts self-approval | Reject the request/action and retain the reason and evidence. | Confirmed; founder decision 22 |
| Conflicting duties | Hold or reject the assignment/action until the approved SoD control resolves it; buying, receiving, and payment-release remain separable. | Confirmed; founder decision 23 |
| Suspended User attempts access | Deny prohibited access, revoke affected sessions, and record the attempt; no read-only bypass is implied. | Confirmed; founder decision 13 |
| Deactivated User attempts recovery | Do not restore automatically; require fresh review and do not restore prior Roles, scopes, or privileged access automatically. | Confirmed; founder decision 27 |
| Tenant is suspended | Apply Tenant lifecycle restrictions consistently to Users, sessions, jobs, exports, and integrations. | Confirmed boundary; MESP-27 |
| Company, Branch, or Warehouse is inactive | Do not grant new access to the inactive scope; re-evaluate existing assignments. | Confirmed business validation; BR-010 |
| User changes Tenant context | Require a valid membership for the target Tenant; never use the client-selected context to expand authority. | Confirmed; RULE-001 |
| Support User requests Tenant access | Require case, purpose, exact Tenant/scope, authorization, and expiry; no standing access. | Confirmed; M27-REQ-045 |
| Account is suspected to be compromised | Apply suspension/recovery controls, terminate affected sessions, require verified-email recovery or fresh review as applicable, and preserve evidence. | Confirmed; founder decisions 8/13/27 |
| Active session remains after revocation | Treat the session as invalid and record a control exception for investigation; no revoked authority is retained. | Confirmed; M27-REQ-057 |
| Migration creates ambiguous User or Role mapping | Quarantine the mapping, assign an owner, reconcile, and obtain business approval before activation. | Confirmed; PRD migration baseline |

## 13. Business Rules

The following register contains 40 explicit IAM business rules. All 40 are Confirmed for the approved Release 1 baseline. Deferred production/legal gates are stated explicitly and do not make the business rules Proposed.

| ID | Rule statement | Classification | Source | Business rationale | Related actors / process | Dependency |
|---|---|---|---|---|---|---|
| IAM-BR-001 | A User shall be a named login identity with a normalized email as the globally unique Release 1 login identifier, distinct from a Person/Employee business reference and from a Supplier or Supplier Contact. | Confirmed | Glossary User, Employee, Supplier; founder decision 2 | Prevents duplicate identities and access being inferred from a business party or employee record. | All Users; PR-001/002 | MESP-29/MESP-40 |
| IAM-BR-002 | A User shall be permitted multiple explicit Tenant Memberships, each carrying only the Roles and Access Scope applicable in that Tenant. | Confirmed | Glossary Tenant Membership; founder decision 1 | Makes multi-tenant access visible, isolated, and revocable. | Tenant Administrator; PR-006 | MESP-29 |
| IAM-BR-003 | A User shall not perform Tenant operations without an active, authorized membership for that Tenant; multiple memberships shall not permit context expansion. | Confirmed | Glossary; BR-010; founder decision 1 | Prevents anonymous, implicit, or cross-Tenant access. | All Users; PR-003/016 | MESP-29 |
| IAM-BR-004 | The hierarchy shall remain Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse. | Confirmed | PRD PLT-002; MESP-56 | Preserves legal, operational, and stock scope meaning. | Tenant/organization administrators; PR-008 | MESP-30 |
| IAM-BR-005 | Tenant context shall be established from trusted authenticated business context; a supplied Tenant identifier shall not expand authority. | Confirmed | PRD RULE-001; M27-RULE-001 | Prevents cross-Tenant disclosure through context substitution. | All Users/support; PR-003/016 | MESP-29/ADR-003 |
| IAM-BR-006 | Access shall be denied by default when membership, Role, Permission, scope, Entitlement, document state, or contextual authorization is missing; explicit deny rules are not a Release 1 feature. | Confirmed | BR-010; PRD section 8; founder decision 32 | Makes every required control affirmative without introducing an explicit-deny model. | All Users; PR-003/008/016 | MESP-29/MESP-38 |
| IAM-BR-007 | Least privilege shall limit a User to the minimum authorized Tenant, organization scope, function, and time required for the work; grants may combine but never inherit upward. | Confirmed | PRD principle; M27-REQ-045; founder decisions 30-31 | Reduces exposure and misuse. | All administrators/support; all PRs | MESP-30/security validation |
| IAM-BR-008 | A Role shall be a named reusable bundle of Permissions and shall not be treated as a job title or automatic approval authority. | Confirmed | Glossary Role | Separates job function from atomic authority. | Tenant Administrator; PR-007 | Role catalogue |
| IAM-BR-009 | A Permission shall represent an atomic right to perform an action on an object type and shall remain distinct from a Role and Entitlement. | Confirmed | Glossary Permission; M27-RULE-006 | Supports precise authorization and audit. | Administrators; PR-007/009 | MESP-38 |
| IAM-BR-010 | Access Scope shall bound where a Permission applies and shall be enforced rather than treated as a report filter; grants inherit downward Tenant -> Company -> Branch -> Warehouse and never upward. | Confirmed | Glossary Access Scope; BR-010; founder decisions 30-31 | Prevents a valid function from becoming unrestricted or upward-expanded data access. | Scoped Users; PR-008 | MESP-30 |
| IAM-BR-011 | An action shall require both applicable Tenant Entitlement and User Permission, plus valid lifecycle, scope, document-state, and context controls. | Confirmed | Glossary Entitlement; M27-RULE-006/007 | Prevents commercial and security controls from being confused. | Platform/Tenant administrators; PR-003/016 | MESP-27 |
| IAM-BR-012 | Platform Administrator and Tenant Administrator responsibilities shall remain distinct; one shall not automatically grant the other. | Confirmed | M27-REQ-008; glossary | Prevents platform control from becoming Tenant-data access. | Platform/Tenant administrators; PR-006/015 | MESP-27 |
| IAM-BR-013 | Tenant administration shall be limited to the authorized Tenant and governed organizational hierarchy. | Confirmed | Glossary Tenant Administrator; BR-010 | Preserves customer ownership and isolation. | Tenant Administrator; PR-006/008 | MESP-29/30 |
| IAM-BR-014 | High-risk Permissions shall be identifiable, their assignment and use shall be auditable, and high-risk operations shall require fresh authentication. | Confirmed | ADM-001; PRD table 17; founder decision 6 | Enables privileged-access review and limits credential-reuse risk. | Security owner/approver; PR-009/010 | ADR-004 |
| IAM-BR-015 | A User shall not approve a self-request or self-action; no self-approval is permitted in Release 1. | Confirmed | PRD table 7; MESP-55; founder decision 22 | Reduces fraud and control failure. | Approvers; PR-007/009 | MESP-38 |
| IAM-BR-016 | Buying, receiving, and payment-release responsibilities shall be separable by Permission and policy; a conflicting combination shall be blocked or governed by an approved exception. | Confirmed | PRD table 7; glossary SoD | Protects the purchase-to-pay control chain without defining Procurement behavior. | Buyer/Warehouse/Accountant/Approver; PR-007/009 | MESP-38 |
| IAM-BR-017 | Support Access shall use a named personal identity, Tenant approval, one Tenant, one named case, one purpose, exact scope, least privilege, and a maximum eight-hour interval. | Confirmed | M27-REQ-045; M27-RULE-017; founder decision 24 | Prevents standing or ambiguous support access. | Support/Tenant administrator; PR-015 | MESP-50 |
| IAM-BR-018 | Shared support credentials, hidden superusers, unrestricted impersonation, and unaudited support access shall not be permitted. | Confirmed | M27-REQ-046; M27-RULE-018 | Preserves accountability and Tenant isolation. | Support/security; PR-015/016 | MESP-38 |
| IAM-BR-019 | Support Access shall expire or be revocable and shall require fresh Tenant approval for an extension, another Tenant, another purpose, or another scope. | Confirmed | M27-REQ-047; founder decisions 24-25 | Limits privileged exposure. | Support approver; PR-015 | MESP-50 |
| IAM-BR-020 | Support authorization alone shall not grant Tenant export authority; export requires separate Permission, authorization, and explicit Tenant approval for the named artifact or scope. | Confirmed | M27-REQ-095; M27-RULE-021 | Prevents support from becoming an unbounded data-export channel. | Support/export approver; PR-015 | MESP-27/MESP-39 |
| IAM-BR-021 | A suspended User or suspended Tenant shall be denied prohibited interactive and non-interactive actions, including new sessions and affected background work; affected sessions shall be revoked. | Confirmed | M27-REQ-055; M27-RULE-012; founder decision 13 | Prevents suspension bypass through another execution path. | Platform/Tenant administrators; PR-011/014 | MESP-27 |
| IAM-BR-022 | A deactivated membership shall not authorize Tenant operations, while the global User identity and required historical evidence remain distinct. | Confirmed | Glossary Tenant Membership; M27 offboarding | Supports revocation without destroying accountability. | Tenant Administrator; PR-012/013 | IAM-OD-019 |
| IAM-BR-023 | A password reset, suspension, offboarding, or critical account, membership, Role, Permission, scope, Tenant, or security change shall revoke affected active sessions before the revoked authority is used again. | Confirmed | M27-REQ-057; PRD session security; founder decision 13 | Closes the gap between access change and existing sessions. | Administrators/security; PR-014 | ADR-004 |
| IAM-BR-024 | Offboarding shall revoke relevant memberships, Roles, Permissions, scopes, support access, and sessions while retaining required evidence; prior authority is not restored automatically. | Confirmed | Jira MESP-28; M27 offboarding; founder decision 27 | Provides a complete business exit control. | Tenant Administrator/security; PR-013 | MESP-40/MESP-50 |
| IAM-BR-025 | Password authentication shall be required; MFA capability shall be available and shall be mandatory for Platform Administrators, Support Users, Tenant Administrators, and privileged operations. Authentication outcomes shall be attributable to a User or safely recorded as an unknown attempt. | Confirmed | PRD section 8; BR-011; founder decisions 3-5 | Supports investigation, accountability, and risk-based access control. | All Users/security; PR-003/004/009 | ADR-004 |
| IAM-BR-026 | Material invitation, activation, authentication, recovery, membership, Role, Permission, scope, privileged, support, suspension, revocation, session, and migration events shall produce business evidence; no automated audit-evidence purge is implemented in Release 1. | Confirmed | PLT-008; BR-011; founder decision 28 | Enables control review and dispute resolution without inventing retention behavior. | All administrators/auditors; PR-001-016 | MESP-50 production gate |
| IAM-BR-027 | Tenant Users shall not edit or delete Identity and Access audit evidence. | Confirmed | PLT-008; glossary Audit Event | Protects historical trust. | Tenant Users/auditors; PR-010/015 | MESP-38 |
| IAM-BR-028 | A Company, Branch, or Warehouse that is inactive or unrelated to the target Tenant shall not receive a new access assignment. | Confirmed | BR-010; approved hierarchy | Prevents invalid organizational scope. | Tenant Administrator; PR-008 | MESP-30 |
| IAM-BR-029 | Suppliers and Supplier Contacts shall remain external business parties and shall not receive Release 1 system-user access. | Confirmed | PRD D-008; glossary Supplier | Preserves the approved manual supplier-response model. | Procurement users; PR-001/006 | Procurement BRD |
| IAM-BR-030 | Identity and Access behavior shall remain generic for Wafra and future Tenants and shall support B2B ERP only; Retail POS actors and workflows are excluded. | Confirmed | PRD D-009; MESP-24; M27-RULE-003/030 | Prevents customer-specific and out-of-release scope. | All owners; all PRs | Product change control |
| IAM-BR-031 | A User invitation shall use the globally unique normalized email, expire after seven days, be withdrawable or reissuable, and never be transferable. | Confirmed | M27-REQ-029; founder decision 9 | Reduces accidental activation and stale invitations. | Tenant Administrator; PR-001/002 | MESP-29 |
| IAM-BR-032 | Ordinary access assignments shall be administered by an authorized Tenant Administrator; privileged assignments shall require one separate named approver. | Confirmed | PRD PLT-005; MESP-55; founder decisions 19-20 | Creates accountable assignment control without a self-service request feature. | Tenant Administrator/approver; PR-006/007/009 | MESP-38 |
| IAM-BR-033 | Access reviews shall occur quarterly and after material role, responsibility, or security events, using an auditable report and manual review in Release 1. | Confirmed | PRD table 17; founder decision 15 | Reduces stale access. | Tenant/security reviewers; PR-010 | MESP-38 |
| IAM-BR-034 | A User may hold multiple Roles and combined grants in one Tenant only when the resulting Permissions and SoD controls remain valid; grants never inherit upward. | Confirmed | Glossary Role; BR-010; founder decisions 1, 23, 31 | Supports real job combinations while preserving control. | Tenant Administrator; PR-007 | MESP-30/MESP-38 |
| IAM-BR-035 | Tenant Administrators may create custom Roles only from Platform-approved Permissions; only Platform governance may define Permission types. | Confirmed | ADM-001; Glossary Permission; founder decisions 16-17 | Balances repeatable job functions with controlled atomic rights. | Tenant Administrator/Platform governance; PR-007 | MESP-38 |
| IAM-BR-036 | Release 1 shall not provide self-service access requests; ordinary assignments use the authorized Tenant Administrator path. | Confirmed | Jira required outputs; founder decisions 18-19 | Keeps the first release lean and accountable. | Tenant Administrator; PR-007 | Lean implementation specification |
| IAM-BR-037 | Privileged Users and privileged operations shall require MFA, a recorded business justification, one separate named approver, and fresh authentication before use. | Confirmed | M27-REQ-048/049; ADR-004; founder decisions 5-6, 20 | Makes high-risk access deliberate and attributable. | Privileged requester/approver; PR-009 | ADR-004/MESP-38 |
| IAM-BR-038 | Release 1 shall exclude emergency or break-glass access; dual approval is excluded unless a later legal or critical-security decision changes that policy. | Confirmed | M27-REQ-049; M27-OQ-005; founder decisions 14, 21 | Avoids an unbounded bypass while preserving a controlled future gate. | Security/Platform/Tenant approvers; PR-009/015 | Later legal/critical-security decision |
| IAM-BR-039 | Account suspension and deactivation shall use distinguishable business reasons and restoration paths; reactivation requires fresh review and does not automatically restore prior authority. | Confirmed | M27-REQ-054; glossary lifecycle; founder decision 27 | Enables safer recovery and reporting. | Tenant/security administrators; PR-011/013 | MESP-38/MESP-40 |
| IAM-BR-040 | Historical access evidence shall preserve the decision, effective context, actor, and reason needed to reconstruct past authority; no automated purge is implemented in Release 1. | Confirmed | BR-011; MESP-50; founder decision 28 | Supports audit without inventing retention or legal rules. | Auditors/security; PR-010/013 | MESP-50 production gate |
| IAM-BR-041 | Only a Platform Security Administrator or a Platform Administrator with the specific global User lifecycle Permission may suspend, reactivate, or offboard a global User; the action requires active authentication, MFA, operation-bound fresh authentication, reason, immutable audit, optimistic concurrency, and idempotency. | Confirmed / Founder-approved | Founder decision 3 August 2026 | Prevents Tenant administration or support from changing a global identity. | Platform governance actor; PR-011/013/014 | MESP-38 / downstream security design |
| IAM-BR-042 | A Tenant Administrator may suspend or revoke a User's Membership in its own Tenant and revoke that Tenant's Role Assignments and AccessScopeGrants, invalidate sessions operating in that Tenant, and block Tenant selection; it may not suspend, reactivate, or offboard the global User. | Confirmed / Founder-approved | Founder decision 3 August 2026 | Provides bounded Tenant containment without global impact. | Tenant Administrator; PR-012/014 | MESP-29 / MESP-38 |
| IAM-BR-043 | Tenant-scoped containment shall not affect another Tenant's Membership, Roles, AccessScopeGrants, or sessions operating only in another Tenant. | Confirmed / Founder-approved | Founder decision 3 August 2026 | Preserves independent multi-Tenant operation. | Tenant Administrator; PR-012/014/016 | MESP-29 |
| IAM-BR-044 | Global User suspension shall revoke all affected User sessions and deny the global identity across every Tenant until reactivation; it shall not be represented as a Membership-only change. | Confirmed / Founder-approved | Founder decision 3 August 2026 | Closes stale-session and mixed-state gaps. | Platform governance actor; PR-011/014 | ADR-004 / MESP-38 |
| IAM-BR-045 | Global User reactivation shall not automatically restore any Tenant Membership, Role Assignment, AccessScopeGrant, support grant, or privilege; each affected Tenant must be re-evaluated and explicitly restored. | Confirmed / Founder-approved | Founder decision 3 August 2026 | Prevents privilege resurrection after global containment. | Platform governance and Tenant administrators; PR-011/012/013 | MESP-29 / MESP-38 |
| IAM-BR-046 | A SupportGrant and Support User identity alone shall not authorize global User suspension, reactivation, or offboarding; a separate Platform governance Permission is required. | Confirmed / Founder-approved | Founder decision 3 August 2026 | Keeps support exceptional and purpose-bound. | Support and Platform governance actors; PR-015 | MESP-27 / MESP-38 |

## 14. Roles, Permissions, and Access Scopes

### 14.1 Business model

1. A **Role** expresses a job function through a reusable bundle of **Permissions**.
2. A **Permission** is an atomic right to perform an action on an object type, such as read, create, modify, submit, approve, post, reverse, cancel, export, or administer where the owning domain defines that action.
3. An **Access Scope** determines where the Permission applies. Scope is enforced data authority, not a filter or convenience label.
4. A **Tenant Entitlement** determines whether a capability is commercially available to the Tenant. It never grants an individual User Permission.
5. An action is authorized only when the relevant Entitlement, User Permission, membership, scope, lifecycle state, document state, and contextual controls all allow it.
6. High-risk Permissions are identifiable and auditable. Approval authority is not implied by a Role name; it is a Permission and policy outcome.

### 14.2 Scope levels

| Scope level | Business meaning | Example access question | Classification |
|---|---|---|---|
| Platform | Platform-owned metadata and explicitly authorized platform operation. | May this Platform Administrator operate the Tenant catalogue? | Confirmed boundary |
| Tenant | All or selected business areas within one Tenant, subject to lower scopes and Permissions; grants inherit to lower levels only. | May this Tenant Administrator manage membership in Tenant A and its approved lower scopes? | Confirmed; founder decisions 30-31 |
| Company / Legal Entity | One legal/accounting entity within a Tenant; a Tenant grant may inherit downward to its Companies. | May this accountant act for Company A and its lower scopes but not Company B? | Confirmed hierarchy; MESP-30 |
| Branch | One operational location within a Company; a Company grant may inherit downward to its Branches. | May this User operate documents for Branch Riyadh and its Warehouses? | Confirmed hierarchy; MESP-30 |
| Warehouse | One stock-holding location within a Branch; a Branch grant may inherit downward to its Warehouses. | May this Warehouse Operator receive at Warehouse R1? | Confirmed hierarchy; MESP-30 |

### 14.3 Permission categories

The following categories are a business vocabulary, not a final permission catalogue:

- Read and search authorized records.
- Create or draft a record.
- Modify an unposted or otherwise editable record.
- Submit a record for review.
- Approve or reject within an approved policy.
- Post, reverse, cancel, or otherwise apply a material business action where the owning domain allows it.
- Export or download an authorized result or artifact.
- Administer membership, Role, Permission, or scope.
- Request, approve, use, or close privileged/support access.

### 14.4 Approved assignment behavior

- A User may hold multiple explicit Tenant Memberships and multiple Roles; combined grants are evaluated together with SoD controls.
- Tenant Administrators administer ordinary assignments. Tenant Administrators may compose custom Roles only from Platform-approved Permissions.
- Only Platform governance may define Permission types. Release 1 does not provide self-service access requests.
- Grants inherit downward Tenant -> Company / Legal Entity -> Branch -> Warehouse, never upward. Explicit deny rules are excluded from Release 1.
- Removing a parent scope removes inherited authority and invalidates affected sessions.
- Conflicting Role combinations are held or rejected under the approved SoD controls; no self-approval is permitted.

## 15. Separation of Duties

### 15.1 Purpose

SoD prevents one User from performing two conflicting steps of the same business transaction. It is distinct from approval workflow: approval identifies who may authorize; SoD identifies who must not authorize because of a conflicting action or responsibility.

### 15.2 Confirmed controls

- Self-approval is prohibited where the governing policy applies.
- Buying, receiving, and payment-release responsibilities are separable by Permission and policy.
- High-risk access and Role combinations are identifiable for review.
- An SoD decision is linked to the User, Tenant, scope, request/action, policy version, decision, reason, and evidence.
- A conflicting assignment cannot be silently treated as safe merely because the User has both Roles.

### 15.3 Release 1 control boundary

The Release 1 baseline preserves the founder-approved control intent: no self-approval; buying, receiving, and payment-release responsibilities remain separable; conflicting combinations are held or rejected under the applicable SoD control; and dual approval is excluded unless a later legal or critical-security decision changes that policy. MESP-38 and qualified control owners may refine the detailed conflict catalogue without weakening these boundaries.

### 15.4 SoD review responsibilities

| Responsibility | Primary actor | Classification |
|---|---|---|
| Identify high-risk Roles/Permissions | Security/Platform owner with affected business owner | Confirmed |
| Review a requested conflict | Named approver and security/control owner | Confirmed control |
| Reject prohibited self-approval | The authorization decision, with evidence | Confirmed |
| Approve a documented exception | Founder-authorized exception owner after the SoD decision; dual approval is not required in Release 1 | Confirmed boundary; later legal/critical-security gate |
| Review active conflicts | Access reviewer during quarterly and event-driven manual reviews | Confirmed; founder decision 15 |
| Preserve conflict and exception evidence | Security and Audit owner | Confirmed; MESP-38/MESP-50 |

## 16. Approval Controls

Approval is a business authorization, not a technical operation. The following matrix records the approved Release 1 controls.

| Action or decision | Approval expectation | Classification | Source / open decision |
|---|---|---|---|
| Assign ordinary Tenant membership | Is administered by an authorized Tenant Administrator under the approved access policy. | Confirmed | Founder decision 19 |
| Assign a high-risk Role or Permission | Requires MFA, fresh authentication for the high-risk operation, and one separate named approver. | Confirmed control | Founder decisions 5-6, 20 |
| Assign Platform-level Role | Requires platform authority and the applicable privileged control. | Confirmed | M27 actor boundary; founder decisions 5, 20 |
| Assign Tenant Administrator | Requires an authorized Tenant Administrator assignment decision and evidence; the assignee is subject to mandatory MFA. | Confirmed | Glossary; founder decisions 5, 19 |
| Expand Company, Branch, or Warehouse scope | Requires hierarchy validation; grants inherit downward only and removal of a parent removes inherited authority and affected sessions. | Confirmed | BR-010; founder decisions 30-33 |
| Privileged access request | Requires business purpose, one separate named approver, MFA, fresh authentication, approved scope, and evidence. | Confirmed | M27-REQ-045/048; founder decisions 5-6, 20 |
| Support access | Requires Tenant approval, named case, named User, exact scope, and a maximum eight-hour duration. | Confirmed | M27-REQ-045; founder decisions 24-25 |
| Tenant export requested by support identity | Requires separate export Permission, separate authorization, and explicit Tenant authorization for the named artifact. | Confirmed | M27-REQ-095 |
| SoD exception | Requires a separately governed exception decision and compensating evidence; no self-approval and no Release 1 dual-approval requirement. | Confirmed | Founder decisions 21-23; MESP-38 |
| Reactivation after security suspension | Requires fresh review and does not automatically restore previous Roles, scopes, or privileged access. | Confirmed control | M27-REQ-054-058; founder decision 27 |
| Emergency access | Excluded from Release 1. | Confirmed exclusion | Founder decision 14 |

## 17. Data Requirements

These are business information requirements. They are not a logical or physical data model.

| Business information | Required meaning | Classification | Owner / dependency |
|---|---|---|---|
| User identity | The identity used for password authentication and accountability, distinct from Person/Employee; normalized email is globally unique in Release 1. | Confirmed | Identity and Access; founder decision 2 |
| Account status | Whether the User can enter Invitation Issued, Pending Activation, Active, Locked, Suspended, or Deactivated. | Confirmed | Identity and Access; founder decisions 9-13 |
| Tenant Membership | Each explicit Tenant link, current status, effective context, Roles, and downward Access Scope; multiple memberships are permitted. | Confirmed | Identity and Access / MESP-29; founder decisions 1, 30-31 |
| Role assignment | Named Role, User, Tenant, scope, effective decision, approver, reason, and history. | Confirmed | Identity and Access |
| Permission assignment | Atomic action authority, source Role or governed assignment, downward scope, and history; Permission types are Platform-governed. | Confirmed | Identity and Access; founder decisions 16-17, 30-31 |
| Access Scope | Company / Legal Entity, Branch, and Warehouse boundary and relationship to Tenant; grants inherit downward only. | Confirmed hierarchy | MESP-30; founder decisions 30-33 |
| Privileged request | Requester, purpose, Tenant, scope, actions, risk, approver, decision, activation, expiry/revocation, and closure. | Confirmed | Security/Audit; MESP-38 |
| Support authorization | Case, named support User, Tenant, scope, purpose, start/end, approvals, notification, activity, and closure. | Confirmed | MESP-27/MESP-38 |
| Authentication outcome | Success/failure category, User or unknown attempt, Tenant context when known, time, and response. | Confirmed | Security/Audit |
| Recovery request | Verified-email claimant, verification outcome, decision, affected access/sessions, actor, reason, and evidence; administrators cannot set or view passwords. | Confirmed | Founder decision 8 |
| Suspension/deactivation reason | Reason category, scope, authority, effective time, access mode, restoration condition, session revocation, and fresh-review outcome. | Confirmed control | MESP-27; founder decisions 13, 27 |
| Session evidence | Creation, context, logout/termination, reason, maximum/inactivity outcome, and affected authorization. | Confirmed | ADR-004; founder decisions 11-13 |
| Access-review outcome | Quarterly/event-driven reviewer, report population, retain/change/revoke decision, exceptions, owner, and date. | Confirmed | Founder decision 15 |
| Migration source reference | Source identity/assignment, mapping decision, rejected/ambiguous status, approver, and reconciliation result. | Confirmed | MESP-40 |
| Historical evidence | Enough context to reconstruct prior access decisions; no automated purge is implemented and MESP-50 controls retention/privacy/legal hold/purge. | Confirmed / Deferred gate | Founder decision 28; MESP-50 |

## 18. Validation Rules

| ID | Business validation | Classification | Source / dependency |
|---|---|---|---|
| IAM-VR-001 | A User must have a globally unique normalized email and be eligible for the target Tenant before membership is created. | Confirmed | Glossary Tenant Membership; founder decision 2 |
| IAM-VR-002 | A Tenant Membership must reference the same Tenant as its Roles and scopes. | Confirmed | BR-010; hierarchy |
| IAM-VR-003 | Company, Branch, and Warehouse scope must belong to the target Tenant and follow the approved hierarchy. | Confirmed | PLT-002; BR-010 |
| IAM-VR-004 | An inactive or unrelated organization scope cannot receive a new assignment. | Confirmed | BR-010 |
| IAM-VR-005 | A Role must be available and appropriate for the requested downward scope before activation; custom Roles may use only Platform-approved Permissions. | Confirmed | ADM-001; founder decision 16 |
| IAM-VR-006 | A Permission must be evaluated together with Entitlement, membership, scope, state, and context. | Confirmed | M27-RULE-006/007 |
| IAM-VR-007 | A suspended User cannot authenticate or perform a prohibited action. | Confirmed control | M27-REQ-055 |
| IAM-VR-008 | A deactivated membership cannot authorize Tenant operations. | Confirmed | Glossary Tenant Membership |
| IAM-VR-009 | A User cannot approve a self-request or self-action. | Confirmed | MESP-55; founder decision 22 |
| IAM-VR-010 | A cross-Tenant membership, assignment, or action must be rejected without exposing the other Tenant. | Confirmed | BR-010; M27-AC-028 |
| IAM-VR-011 | A privileged request must include business justification, MFA/fresh-authentication outcome, one separate named approver, exact scope, and approval evidence. | Confirmed control | M27-REQ-048; founder decisions 5-6, 20 |
| IAM-VR-012 | Support access must identify Tenant approval, a named case, named User, Tenant, purpose, exact scope, and a maximum eight-hour interval. | Confirmed | M27-REQ-045; founder decision 24 |
| IAM-VR-013 | Support authorization cannot satisfy export authorization by itself. | Confirmed | M27-REQ-095 |
| IAM-VR-014 | A critical access change must cause affected sessions to lose the changed authority. | Confirmed | M27-REQ-057 |
| IAM-VR-015 | A migration mapping with ambiguous identity, Role, or scope must be held from activation until owner approval. | Confirmed | PRD migration baseline |
| IAM-VR-016 | A general invitation must be rejected after seven days; withdrawal and reissue are allowed and transfer is prohibited. | Confirmed | Founder decision 9 |
| IAM-VR-017 | A recovery request must use verified-email self-service; an unverified claimant is rejected or escalated, and administrators cannot set or view passwords. | Confirmed | Founder decision 8 |
| IAM-VR-018 | A Role combination must be rejected or routed to SoD review when the approved control identifies a conflict; buying, receiving, and payment-release remain separable. | Confirmed | Founder decision 23; MESP-38 |

## 19. Status Transitions

The tables below define the approved Release 1 business state vocabulary and transition guards. Detailed implementation state names may be refined in the downstream Lean Implementation Specification, but the controls below are fixed for Release 1.

### 19.1 User Account

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| No account | Valid invitation | Authorized Tenant Administrator | Invitation issued | Identity, Tenant, inviter, reason, time | Confirmed |
| Invitation issued | Invitation accepted and activation started | Intended User and authorized policy | Pending activation | Invitation reference, acceptance, time | Confirmed |
| Pending activation | Valid password and MFA activation outcome | Intended User and authorized policy | Active | Activation evidence, normalized email, membership context | Confirmed |
| Active | Approved security/administrative reason | Authorized administrator/security owner | Suspended | Reason, scope, time, restoration condition, session outcome | Confirmed |
| Active | Offboarding or membership end | Authorized Tenant/security owner | Deactivated | Revocation, session outcome, reason | Confirmed |
| Suspended | Fresh review and approved restoration | Authorized owner | Active | Clearance, fresh review, new access decision, session outcome | Confirmed; prior Roles/scopes are not restored automatically |
| Deactivated | Fresh review and approved reactivation | Authorized owner | Active | Reverification, fresh Role/scope decision, approval evidence | Confirmed; no automatic restoration |

### 19.2 Tenant Membership

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| None | Eligible membership decision | Tenant Administrator | Invited or Pending | User, Tenant, Role/scope request | Confirmed |
| Pending | Accepted and activated | Authorized policy | Active | Acceptance and activation evidence | Confirmed |
| Active | Role/scope change | Authorized administrator/approver | Active with revised authority | Before/after assignment, decision, reason | Confirmed |
| Active | Suspension | Authorized administrator/security | Suspended | Reason, scope, access mode, time | Confirmed |
| Active | Revocation/offboarding | Authorized administrator/security | Revoked or Deactivated | Reason, session outcome, evidence | Confirmed |
| Suspended | Fresh review and restoration | Authorized owner | Active or Revoked | Restoration decision and new assignment evidence | Confirmed |

### 19.3 Role Assignment

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| Not assigned | Direct ordinary administration or approved privileged request | Tenant Administrator; one separate named approver for privileged access | Active | Requested Role, scope, reason, and applicable approval | Confirmed |
| Active | Change request | Authorized administrator/approver | Active revised | Before/after and effective decision | Confirmed |
| Active | Conflict or risk | Security/authorized administrator | Suspended or Revoked | Conflict/reason and decision | Confirmed control |
| Active | Quarterly or event-driven access review | Named reviewer and accountable owner | Retained, Revised, or Revoked | Review outcome, exceptions, owner, and date | Confirmed |

### 19.4 Privileged Access Request

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| Not requested | Business need | Named requester | Requested | Purpose, Tenant, scope, actions, risk | Confirmed |
| Requested | Review | One separate named approver/security owner | Approved or Rejected | Decision, reason, SoD result, MFA/fresh-authentication evidence | Confirmed control |
| Approved | Start condition | Authorized support/privileged User | Active | Activation, effective context, MFA/fresh authentication | Confirmed |
| Active | End, eight-hour support limit, revocation, or case closure | System policy/authorized owner | Expired or Revoked | Activity, outcome, revocation reason | Confirmed |
| Expired/Revoked | Review | Security/auditor | Closed | Closure evidence and exceptions | Confirmed |

Break-glass access and Release 1 dual approval are excluded. A support-access extension requires a fresh Tenant approval and does not grant export authority.

### 19.5 Access Review

| From | Trigger | Authorized actor | To | Required evidence | Classification |
|---|---|---|---|---|---|
| Not due | Quarterly cadence or risk event | Access owner | Due | Review population and trigger | Confirmed |
| Due | Review begins | Named reviewer | In review | Reviewer, date, population | Confirmed |
| In review | Retain/change/revoke decisions | Reviewer/approver | Completed or Exception | Per-assignment outcome and owner | Confirmed |
| Exception | Missing owner/conflict/ambiguous mapping | Accountable owner | Resolved or Escalated | Resolution and decision | Confirmed control |

### 19.6 Session business state

| From | Trigger | Authorized actor/policy | To | Required evidence | Classification |
|---|---|---|---|---|---|
| None | Successful authentication after password/MFA policy | Approved authentication policy | Active | User, Tenant context, time, outcome | Confirmed |
| Active | Logout | User or authorized policy | Ended | End outcome and time | Confirmed |
| Active | Critical access change, password reset, suspension, or offboarding | Membership/Role/security authority | Invalidated | Change reason, affected context | Confirmed |
| Active | Parent-scope removal or Tenant restriction | Authorized lifecycle policy | Invalidated or restricted | State, scope, policy result | Confirmed |
| Active | Eight-hour maximum, 30-minute inactivity, or other approved session condition | Approved policy | Expired | Expiry reason and time | Confirmed |

Concurrent sessions are permitted. Affected sessions lose authority immediately when a parent scope is removed or a critical authority change occurs.

## 20. Document and Evidence Lifecycle

Identity and Access does not own a commercial ERP document. It owns business evidence associated with identity and access decisions.

| Evidence type | Create | Review/use | Close/supersede | Retention position | Classification |
|---|---|---|---|---|---|
| Invitation evidence | When invitation is issued, withdrawn, accepted, or rejected | Tenant Administrator and auditor review | Superseded by activation, expiry, withdrawal, or rejection | Retention/privacy/legal-hold/purge treatment is an MESP-50 gate | Confirmed evidence / Deferred gate |
| Membership evidence | On create, change, suspend, revoke, or offboarding | Access review and support investigation | Superseded by a later decision; historical link retained | MESP-50 and privacy policy | Confirmed |
| Role/Permission evidence | On request, approval, activation, modification, suspension, or revocation | Access and SoD review | Superseded without erasing history | MESP-50 | Confirmed |
| Scope evidence | On Company/Branch/Warehouse assignment or removal | Organization and access review | Superseded by revised scope | MESP-50 | Confirmed |
| Privileged-access evidence | On request, approval, activation, activity, expiry/revocation, and closure | Security/auditor review | Closed with outcome and exceptions | MESP-50 | Confirmed |
| Support evidence | On case, authorization, access, actions, downloads, expiry/revocation, and closure | Tenant/security/auditor review | Closed after case outcome | MESP-50 | Confirmed |
| Suspension/deactivation evidence | At state change and restoration decision | Security/Tenant review | Superseded by restoration or final offboarding | MESP-50 | Confirmed |
| Access-review evidence | At review start, each outcome, and closure | Control owner/auditor review | Closed or escalated | MESP-50 | Confirmed |
| Migration evidence | At mapping, validation, exception, reconciliation, and sign-off | Business owner and migration review | Closed at accepted cutover or retained exception | MESP-50/MESP-40 | Confirmed |

No retention period, purge duration, legal-hold rule, residency, or backup treatment is set here. MESP-50 remains the production gate, and Release 1 performs no automated audit-evidence purge.

## 21. Reports and KPIs

| Report / KPI | Business definition | Classification | Owner / evidence |
|---|---|---|---|
| Active Users by Tenant and scope | Count/list of active Users by Tenant, Company, Branch, and Warehouse scope visible to an authorized reviewer. | Confirmed need | Tenant Administrator; PRD table 17 |
| Suspended and deactivated Users | Users and memberships restricted or ended, with reason category and effective time. | Confirmed need | Tenant/Security owner |
| Privileged Users and assignments | Current high-risk Roles, Permissions, privileged requests, and support authorizations. | Confirmed need | Security/Audit; PRD table 17 |
| Access assignment administration queue | Ordinary assignments and privileged approvals awaiting authorized administration, with status and accountable actor. Self-service access requests are out of Release 1 scope. | Confirmed | Tenant/Security owner; founder decisions 18-20 |
| Access-review completion | Population due, completed, overdue, exception, retained, changed, and revoked for quarterly/event-driven manual review. | Confirmed | Security/Tenant owner; founder decision 15 |
| Authentication outcomes | Success, failure, lockout/suspension outcomes, and trend by authorized Tenant scope. | Confirmed need | Security/Audit; BR-011 |
| Multiple-Tenant memberships | Users with more than one explicit membership, with each Tenant boundary visible only to authorized reviewers. | Confirmed | Identity and Access; founder decision 1 |
| SoD conflicts and exceptions | Active conflict, exception, owner, compensating decision, and review status; buying, receiving, and payment-release remain separable. | Confirmed need / detailed catalogue MESP-38 | Security/Audit; founder decisions 21-23 |
| Support-access activity | Case, Tenant, User, scope, duration, actions, downloads, expiry/revocation, and closure. | Confirmed need | MESP-27/MESP-38 |
| Orphaned access assignments | Roles, Permissions, or scopes without a valid User, membership, or organizational owner. | Confirmed need | Migration/access review; MESP-40 and access review |
| Migration exceptions | Ambiguous, rejected, unresolved, or manually approved identity/Role/scope mappings. | Confirmed need | MESP-40 |
| Access denial trend | Authorized analysis of denial categories such as cross-Tenant, inactive scope, suspended User, lockout, and missing Permission. | Confirmed need | Security/Audit |

Reports must be Tenant- and scope-authorized, identify data-as-of/freshness when asynchronous preparation is involved, and never expose another Tenant. No dashboard or screen layout is defined.

## 22. Audit Evidence

Material business events requiring evidence include:

| Event | Minimum business evidence | Classification |
|---|---|---|
| Invitation issued, withdrawn, accepted, rejected, or expired | User/identity reference, Tenant, scope, inviter, reason, time, outcome, notification outcome; general validity is seven days and transfer is prohibited | Confirmed |
| Activation | User, Tenant Membership, assignments, actor, time, activation outcome | Confirmed |
| Authentication success | User, Tenant context, time, outcome category, source context | Confirmed |
| Authentication failure | User or unknown attempt, time, outcome category, protective response | Confirmed |
| Recovery request and decision | Verified-email claimant, verification outcome, decision, actor, affected access/session, reason; administrators cannot set or view passwords | Confirmed |
| Membership create/change/suspend/revoke | User, Tenant, before/after assignment, actor, reason, effective time | Confirmed |
| Role or Permission assignment/change | User, Role/Permission, scope, approver, policy version, reason, effective time | Confirmed |
| Scope assignment/removal | Tenant, Company/Branch/Warehouse scope, before/after, actor, reason | Confirmed |
| Privileged-access request/approval/use/closure | Case/purpose, requester, approver, scope, actions, activation, end, activity, outcome | Confirmed |
| Support access | Case, named support User, Tenant, scope, purpose, authorization, actions, downloads, expiry/revocation, closure | Confirmed |
| Suspension/reactivation/deactivation | Reason, authority, state/effective time, session/job outcome, restoration criteria, notice | Confirmed |
| Session invalidation | Affected User/Tenant/scope, critical change, time, outcome | Confirmed |
| Cross-Tenant denial | Actor, attempted Tenant/context, safe denial category, time, escalation outcome | Confirmed |
| Access review | Reviewer, population, decisions, exceptions, owner, completion time for quarterly/event-driven manual review | Confirmed |
| Migration mapping | Source reference, mapping, validation, exception, reconciliation, approver | Confirmed |

Audit evidence shall be immutable to Tenant Users, correlated to the business action where applicable, and safe: secrets and unnecessarily sensitive personal data are not recorded. Retention, legal hold, residency, and purge remain MESP-50 decisions; no automated purge is permitted in Release 1.

## 23. Integration Requirements

These are business dependencies and ownership expectations, not interface specifications.

| Dependency | Business interaction | Required outcome | Classification |
|---|---|---|---|
| Tenant lifecycle | Identity access follows Tenant provisioning, activation, suspension, reactivation, termination, and retention states. | No unauthorized User/session operation in an ineligible Tenant. | Confirmed; MESP-27/MESP-29 |
| Organization lifecycle | Membership Roles/scopes reference valid Company, Branch, and Warehouse identities. | Scope cannot cross Tenant or hierarchy boundaries. | Confirmed; MESP-30 |
| Security and Audit | Identity events produce immutable, retrievable evidence. | Access decisions and denials can be reconstructed. | Confirmed; MESP-38 |
| Notifications | Invitations, approvals, failures, suspension, recovery, and material exceptions have visible outcome evidence. | A delivery failure does not silently change the authorization decision. | Confirmed need / delivery channel deferred to downstream specification |
| Files and exports | Access to attachments and exported artifacts is evaluated under User/Tenant/scope authority. | Support access cannot substitute for export authorization. | Confirmed; MESP-27/MESP-39 |
| Reporting | Authorized access reports expose current state and freshness. | Reports do not bypass authorization. | Confirmed; MESP-36 |
| Background processes | Jobs act only for the recorded Tenant and approved scope and stop when access/lifecycle policy prohibits them. | No asynchronous access bypass. | Confirmed; MESP-27 |
| Migration and onboarding | Existing identities and assignments are mapped, reconciled, and approved before activation. | Ambiguous mappings remain blocked and visible. | Confirmed; MESP-40 |

## 24. Migration Requirements

Migration is a business onboarding and reconciliation concern. This BRD does not define migration scripts or storage structures.

| Migration area | Business requirement | Classification | Owner / gate |
|---|---|---|---|
| Existing Users | Identify source owner, identity purpose, active/inactive state, Tenant relationship, and accountable business owner; normalize the email used for the globally unique Release 1 login identifier. | Confirmed | MESP-40; founder decision 2 |
| Duplicate identities | Detect likely duplicates before activation and assign a reviewed merge/retain/reject decision; ambiguous duplicate mappings remain quarantined until owner approval. | Confirmed | MESP-40; founder decision 29 |
| Existing Roles | Map source Roles to approved Release 1 Roles and record unmapped/retired assignments. | Confirmed | MESP-40; IAM-OD-013 |
| Permission assignments | Reconcile atomic rights to approved Permissions without silently granting broader authority. | Confirmed | MESP-40; IAM-OD-014 |
| Organizational access | Map Company, Branch, and Warehouse scope within the approved Tenant hierarchy. | Confirmed | MESP-30/MESP-40 |
| Disabled/inactive Users | Keep disabled source identities from becoming active without a fresh review and approved reactivation decision; prior Roles/scopes are not restored automatically. | Confirmed | Founder decision 27 |
| Missing ownership | Hold a User or assignment without a clear business owner for founder/business-owner decision. | Confirmed | MESP-40 |
| Ambiguous scope | Quarantine an assignment that could map to more than one Tenant or organizational scope until accountable owner approval. | Confirmed | Founder decision 29 |
| Privileged Users | Identify high-risk and support assignments separately and obtain approval before activation. | Confirmed | MESP-38 |
| Historical evidence | Record what source evidence exists and what cannot be migrated; do not claim unavailable history, and retain evidence subject to the MESP-50 production gate. | Confirmed / Deferred gate | Founder decision 28; MESP-50 |
| Reconciliation | Reconcile source counts, active assignments, Roles, scopes, exceptions, and owner decisions before cutover. | Confirmed | MESP-40 |
| Founder/business-owner sign-off | Hossam and named business owners approve unresolved mappings before access is enabled; ambiguous mappings remain quarantined until this approval. | Confirmed | Founder decision 29; MESP-40 |

## 25. Business Impact Assessment

### 25.1 Inventory impact

Identity and Access does not directly create, increase, decrease, value, or own Inventory quantities. It indirectly controls which authorized Users may create, review, approve, post, reverse, count, transfer, or adjust inventory records within a valid Company, Branch, or Warehouse scope. Inventory movement meaning remains owned by MESP-33 and the approved glossary.

Classification: **Confirmed boundary**. No Inventory transaction requirement is introduced.

### 25.2 Accounting impact

Identity and Access does not directly post journals, payables, receivables, cash, tax, or general-ledger entries. It indirectly controls which authorized Users may prepare, approve, post, reverse, reconcile, or release accounting actions and supports SoD evidence around buying, receiving, and payment responsibilities. Finance meaning remains owned by MESP-34.

Classification: **Confirmed boundary**. No Finance transaction requirement is introduced.

### 25.3 Multi-currency impact

Identity and Access does not calculate currencies, select exchange rates, or alter transaction/base/reporting currency facts. It only controls access to Users and Roles that may perform approved currency-related business actions in the owning Finance context.

Classification: **Confirmed boundary**. Exchange-rate behavior remains MESP-54/MESP-34.

### 25.4 Saudi localization impact

- English and Arabic terminology and RTL are foundational Release 1 product capabilities and must cover Identity and Access business messages and evidence labels.
- Saudi launch context is generic for all eligible Saudi Tenants; Wafra does not create a special identity rule.
- Residency, cross-border support, privacy, retention, and support-access obligations require qualified review under KSA-006/KSA-007 and MESP-50.
- No Saudi legal, tax, or regulatory conclusion is invented by this BRD.

Classification: **Confirmed product direction / Deferred production gate**. KSA-006/KSA-007 and MESP-50 remain qualified production validation gates.

## 26. Given/When/Then Acceptance Scenarios

These are business acceptance scenarios, not automated test instructions or a separate test-case document.

1. **IAM-AC-001 - Valid invitation:** Classification: Confirmed. Given an eligible User, Tenant, authorized inviter, and valid requested scope, when the invitation is issued, then one reviewable invitation and membership request are recorded without granting unrelated Tenant access.
2. **IAM-AC-002 - Duplicate invitation:** Classification: Confirmed. Given a matching pending or active membership, when another invitation is requested, then duplicate creation is prevented and the reviewed duplicate outcome is evidenced.
3. **IAM-AC-003 - Invitation expiry:** Classification: Confirmed. Given an invitation is more than seven days old, when activation is attempted, then activation is rejected; an authorized actor may withdraw or reissue an invitation, and transfer is prohibited.
4. **IAM-AC-004 - Activation:** Classification: Confirmed. Given a valid invitation and eligible Tenant, when the User completes the approved activation path, then the account and membership become eligible only for the recorded Roles and scopes.
5. **IAM-AC-005 - Suspended Tenant activation denied:** Classification: Confirmed. Given a suspended Tenant, when a new User attempts activation or sign-in, then prohibited access is denied and the outcome is evidenced.
6. **IAM-AC-006 - Authentication success:** Classification: Confirmed. Given an active User with a valid membership and required authentication evidence, when authentication succeeds, then the User can act only within the authorized Tenant context.
7. **IAM-AC-007 - Authentication failure:** Classification: Confirmed. Given invalid authentication evidence, when the User attempts sign-in, then access is denied, a safe outcome is provided, and the failed event is recorded.
8. **IAM-AC-008 - Lockout policy:** Classification: Confirmed. Given five failed authentication attempts occur, when the next attempt is made, then the identity is temporarily locked for 15 minutes, the event is evidenced, and no other Tenant is exposed.
9. **IAM-AC-009 - Recovery verification:** Classification: Confirmed. Given an eligible recovery request, when the claimant completes verified-email self-service, then the approved recovery outcome is recorded, affected sessions are revoked, and administrators cannot set or view the password.
10. **IAM-AC-010 - Recovery of deactivated identity:** Classification: Confirmed. Given a deactivated User or membership, when reactivation is requested, then a fresh review and new access decision are required and prior Roles, scopes, and privileged access are not restored automatically.
11. **IAM-AC-011 - Membership scope:** Classification: Confirmed. Given a User has membership in Tenant A but not Tenant B, when the User targets Tenant B, then access is denied without revealing Tenant B data.
12. **IAM-AC-012 - Multiple Tenant membership isolation:** Classification: Confirmed. Given a User has approved memberships in two Tenants, when the User changes context, then each context exposes only its own authorized Roles, scopes, and data.
13. **IAM-AC-013 - Role assignment:** Classification: Confirmed. Given a valid membership and an available Role, when a Tenant Administrator performs an ordinary assignment or one separate named approver approves a privileged assignment, then the User gains only the Role's Platform-approved Permissions and downward scope.
14. **IAM-AC-014 - Role unavailable:** Classification: Confirmed. Given a requested Role has been retired or is unavailable, when assignment is attempted, then the User does not receive stale authority and the exception is visible.
15. **IAM-AC-015 - Permission without Entitlement:** Classification: Confirmed. Given a User has a Permission but the Tenant lacks the applicable Entitlement, when the action is attempted, then access is denied and no commercial capability is granted.
16. **IAM-AC-016 - Entitlement without Permission:** Classification: Confirmed. Given the Tenant has an Entitlement but the User lacks the Permission, when the action is attempted, then access is denied.
17. **IAM-AC-017 - Company scope:** Classification: Confirmed. Given a User is scoped to Company A, when the User acts on Company B, then the action is denied and the valid Company A authority is unchanged; grants never inherit upward.
18. **IAM-AC-018 - Branch/Warehouse scope:** Classification: Confirmed. Given a User is scoped to one Branch or Warehouse, when the User targets an unrelated or inactive scope, then the action is denied and the attempt is evidenced; valid parent grants inherit downward only.
19. **IAM-AC-019 - Self-approval:** Classification: Confirmed. Given a policy prohibits self-approval, when a User attempts to approve their own prohibited request, then the approval is rejected and the reason is recorded.
20. **IAM-AC-020 - SoD conflict:** Classification: Confirmed. Given the approved SoD matrix identifies a conflict, when a User requests or performs the conflicting assignment/action, then it is blocked or routed to the approved exception path.
21. **IAM-AC-021 - Privileged request:** Classification: Confirmed. Given a named requester, business purpose, Tenant, exact scope, requested actions, MFA, fresh authentication, and one separate named approver, when privileged access is approved, then only the requested boundary becomes active and the approval evidence is retained.
22. **IAM-AC-022 - Privileged request rejection:** Classification: Confirmed. Given missing purpose, MFA/fresh authentication, ineligible or same-user approver, conflict, unsupported scope, or excluded break-glass path, when privileged access is requested, then it is rejected or held and no privilege is activated.
23. **IAM-AC-023 - Support access:** Classification: Confirmed. Given a valid case, named support User, Tenant approval, exact scope, purpose, and interval of no more than eight hours, when support access begins, then only that boundary is available and activity is evidenced.
24. **IAM-AC-024 - Support expiry:** Classification: Confirmed. Given the eight-hour support interval has ended, when further support access is attempted, then it is denied; any extension requires fresh Tenant approval and closure evidence remains.
25. **IAM-AC-025 - Support cross-Tenant attempt:** Classification: Confirmed. Given support is authorized for Tenant A, when the same identity targets Tenant B, then access is denied without revealing Tenant B and a security event is recorded.
26. **IAM-AC-026 - Support export separation:** Classification: Confirmed. Given a support User requests an export, when support authorization alone is evaluated, then export is denied until separate Permission, export authorization, and explicit Tenant authorization are present.
27. **IAM-AC-027 - Suspension:** Classification: Confirmed. Given an authorized suspension with reason and scope, when the suspension takes effect, then prohibited sessions and actions are denied, affected sessions are revoked, and the suspension evidence is visible.
28. **IAM-AC-028 - Session invalidation:** Classification: Confirmed. Given a critical membership, Role, Permission, scope, password, suspension, offboarding, or security change, when the change becomes effective, then affected sessions cannot use the revoked authority; ordinary sessions remain limited to eight hours and 30 minutes of inactivity.
29. **IAM-AC-029 - Revocation:** Classification: Confirmed. Given a Role, Permission, scope, or membership is revoked, when the User attempts the formerly authorized action, then it is denied, inherited authority is removed, affected sessions are invalidated, and the original decision remains auditable.
30. **IAM-AC-030 - Offboarding:** Classification: Confirmed. Given a User is offboarded, when the process completes, then applicable memberships, Roles, scopes, support access, and sessions no longer authorize operations while required evidence remains.
31. **IAM-AC-031 - Migration exception:** Classification: Confirmed. Given an ambiguous identity, Role, Permission, or scope mapping, when migration validation runs, then activation is quarantined, an accountable owner is assigned, and the decision is reconciled before access is enabled.
32. **IAM-AC-032 - Audit retrieval:** Classification: Confirmed. Given an auditor or authorized reviewer requests material access evidence, when the evidence is retrieved, then actor, Tenant, scope, action, time, outcome, and safe decision context are available without allowing Tenant-user editing or automated purge.
33. **IAM-AC-033 - Tenant membership containment:** Classification: Founder-approved. Given a User has active Memberships in Tenant A and Tenant B, when a Tenant A Administrator suspends the User's Membership in Tenant A, then Tenant A selection and operations are denied, Tenant A sessions are invalidated, and the global User and Tenant B remain unchanged.
34. **IAM-AC-034 - Independent Tenant access:** Classification: Founder-approved. Given Tenant A Membership is suspended while Tenant B Membership remains active, when the User authenticates and selects Tenant B, then the User may access only the currently valid Tenant B scope and no Tenant A data is exposed.
35. **IAM-AC-035 - Tenant Administrator boundary:** Classification: Founder-approved. Given a Tenant Administrator attempts to suspend, reactivate, or offboard a global User, when the request is evaluated, then it is denied and no global identity state changes.
36. **IAM-AC-036 - Platform global lifecycle authority:** Classification: Founder-approved. Given a Platform Security Administrator or Platform Administrator has the specific global User lifecycle Permission and completes MFA and operation-bound fresh authentication, when a reasoned global suspension is submitted, then it is accepted only once with optimistic concurrency, idempotency, and immutable evidence.
37. **IAM-AC-037 - Global suspension session revocation:** Classification: Founder-approved. Given a global User is suspended, when any affected session attempts further work in any Tenant, then the session is denied and the global suspension and session-revocation evidence is retained.
38. **IAM-AC-038 - Global reactivation does not restore:** Classification: Founder-approved. Given a globally suspended User is reactivated, when the User attempts to select a former Tenant, then no Membership, Role, scope, support grant, or privilege is restored without a separate current Tenant decision.
39. **IAM-AC-039 - SupportGrant boundary:** Classification: Founder-approved. Given a Support User has an active Tenant-approved SupportGrant but no Platform governance Permission, when the User attempts a global User lifecycle action, then it is denied and the SupportGrant remains limited to its approved Tenant, purpose, scope, and expiry.
40. **IAM-AC-040 - Cross-Tenant non-impact evidence:** Classification: Founder-approved. Given Tenant A containment occurs, when audit evidence is reviewed, then the action identifies Tenant A and affected sessions without exposing or mutating Tenant B state or session evidence.

## 27. Founder-Approved Release 1 Decisions

The 22 former open-decision records below are retained for traceability, and
IAM-OD-023 records the new founder-approved lifecycle authority decision. Hossam
approved the historical decisions on 2 August 2026 and IAM-OD-023 on 3 August
2026. The decision IDs are traceability references, not outstanding requirements.

| ID | Founder-approved Release 1 decision | Business impact / downstream gate | Status |
|---|---|---|---|
| IAM-OD-001 | A User may hold multiple explicit Tenant Memberships; each membership remains isolated and context switching never crosses Tenant boundaries. | Membership and session context; MESP-29/MESP-30 detail | Approved — Hossam — 2 August 2026 |
| IAM-OD-002 | The normalized email is globally unique and is the Release 1 login identifier. | Identity migration and duplicate handling; MESP-40 | Approved — Hossam — 2 August 2026 |
| IAM-OD-003 | Password authentication is required and MFA capability is required for Release 1. | Authentication baseline; downstream security specification | Approved — Hossam — 2 August 2026 |
| IAM-OD-004 | MFA is mandatory for Platform Administrators, Support Users, Tenant Administrators, and privileged operations; high-risk operations require fresh authentication. | Privileged workflow and audit evidence | Approved — Hossam — 2 August 2026 |
| IAM-OD-005 | Credential policy is Platform-controlled; Tenants cannot override it. | Central governance and security validation | Approved — Hossam — 2 August 2026 |
| IAM-OD-006 | Recovery is verified-email self-service; administrators cannot choose, view, or set User passwords. | Recovery and session revocation behavior | Approved — Hossam — 2 August 2026 |
| IAM-OD-007 | Invitations remain valid for seven days, may be withdrawn and reissued, and are not transferable. | Invitation lifecycle and audit evidence | Approved — Hossam — 2 August 2026 |
| IAM-OD-008 | Five failed authentication attempts cause a 15-minute temporary lockout. | Protective state and notification evidence | Approved — Hossam — 2 August 2026 |
| IAM-OD-009 | Ordinary sessions have an eight-hour maximum and 30-minute inactivity timeout. | Session lifecycle and authorization invalidation | Approved — Hossam — 2 August 2026 |
| IAM-OD-010 | Concurrent sessions are permitted; password reset, suspension, offboarding, parent-scope removal, and critical authority changes revoke affected sessions. | Session lifecycle and immediate authority removal | Approved — Hossam — 2 August 2026 |
| IAM-OD-011 | Break-glass access is excluded from Release 1. | No emergency bypass in R1 | Approved — Hossam — 2 August 2026 |
| IAM-OD-012 | Access reviews occur quarterly and on relevant events; the report is auditable and review is manual. | Review report and MESP-38 detail | Approved — Hossam — 2 August 2026 |
| IAM-OD-013 | Tenant Administrators may create custom Roles only from Platform-approved Permissions. | Role administration guardrail | Approved — Hossam — 2 August 2026 |
| IAM-OD-014 | Only Platform governance defines Permission types. | Permission catalogue governance | Approved — Hossam — 2 August 2026 |
| IAM-OD-015 | Self-service access requests are excluded from Release 1. | Administrator-led access assignment only | Approved — Hossam — 2 August 2026 |
| IAM-OD-016 | Ordinary assignments are authorized by a Tenant Administrator; privileged assignments require one separate named approver; self-approval is prohibited. | Approval control and authority separation | Approved — Hossam — 2 August 2026 |
| IAM-OD-017 | Dual approval is excluded unless a later legal or critical-security decision requires it; buying, receiving, and payment-release responsibilities remain separable. | MESP-38 SoD catalogue may refine details without changing the R1 boundary | Approved — Hossam — 2 August 2026 |
| IAM-OD-018 | Support access requires Tenant approval, a named case and User, exact scope, and a maximum eight hours; extensions require fresh approval and support never grants export authority. | MESP-27/MESP-38 support controls | Approved — Hossam — 2 August 2026 |
| IAM-OD-019 | Reactivation requires a fresh review and does not automatically restore prior Roles, scopes, or privileged access. | Lifecycle/recovery detail | Approved — Hossam — 2 August 2026 |
| IAM-OD-020 | There is no automated audit-evidence purge in Release 1; MESP-50 is the production gate for retention, privacy, residency, legal hold, and purge. | Historical evidence and production/legal validation gate | Approved — Hossam — 2 August 2026 |
| IAM-OD-021 | Ambiguous migration mappings are quarantined until accountable owner approval. | MESP-40 migration reconciliation | Approved — Hossam — 2 August 2026 |
| IAM-OD-022 | Access Scope grants inherit downward from Tenant to Company, Branch, and Warehouse, combine without upward inheritance, and exclude explicit deny rules; parent removal revokes inherited authority and invalidates affected sessions. | MESP-30 scope model and session invalidation | Approved — Hossam — 2 August 2026 |
| IAM-OD-023 | A global User may belong to many Tenants; only a Platform Security Administrator or Platform Administrator with the specific global User lifecycle Permission may suspend, reactivate, or offboard the global User under active authentication, MFA, operation-bound fresh authentication, reason, immutable audit, optimistic concurrency, and idempotency. A Tenant Administrator may suspend or revoke only the Membership and Tenant grants in its own Tenant and invalidate sessions operating there. Tenant containment must not affect another Tenant; global reactivation never auto-restores Membership, Roles, scopes, support, or privileges; SupportGrant alone has no global lifecycle authority. | Global identity and Tenant Membership ownership, containment, session revocation, and explicit reactivation decisions; implementation remains downstream gated by the Product Delivery Master Plan, MESP-29, MESP-38, and the approved foundation specification. | Approved — Hossam — 3 August 2026 |

## 28. Source Conflict Register

| ID | Conflict or ambiguity | Affected sections | Resolution status | Resolution / evidence |
|---|---|---|---|---|
| IAM-SC-001 | The Jira prompt and MESP-28 description name MiniERPSaaSPlatform_PRD_v1.2_Final_Approved_Baseline.docx, and older documents name MiniERPSaaSPlatform_PRD_v1.2.docx, while the canonical repository path is now `docs/MESP_PRD_v1.2.docx`. | 1, 6, 30 | Nonblocking source-provenance note | All three names resolve to the identical unchanged PRD v1.2 file; only the repository path moved. The older filenames are retained only for traceability, and the Jira references remain stale. |
| IAM-SC-002 | An earlier MESP-28 Jira comment required entry-criteria approval before starting, while the later explicit founder authorization fast-tracks approval now. | 1, 6, 30 | Resolved by later founder authorization | Historical Jira evidence is preserved; this v0.2 baseline supersedes the earlier timing instruction. |
| IAM-SC-003 | The approved MESP-27 review package previously required MESP-27 sequencing before MESP-28, while MESP-27, MESP-57, and Sprint 1 are now complete. | 6, 9, 30 | Resolved by delivery state and founder authorization | The current master plan records MESP-28 Done and MESP-29 as the next single activity. |
| IAM-SC-004 | The glossary marked Access Scope and Separation of Duties as Draft for BRD Validation, while the PRD/Jira task required them in MESP-28. | 8, 14, 15, 27 | Resolved by founder authorization | Release 1 meaning and boundaries are approved here; later glossary synchronization and detailed SoD catalogue remain downstream work. |

No unresolved source conflict remains in this baseline. The four records are retained for provenance and traceability.

## 29. BRD Coverage Checklist

| Jira MESP-28 required output | Covered section(s) | Coverage status | Deferred owner / decision |
|---|---|---|---|
| Business purpose | 2-3 | Covered | None |
| Actors and responsibilities | 7 | Covered | Detailed Role catalogue may be refined in MESP-38 |
| Trigger and preconditions | 11 | Covered | Approved values are recorded in the founder decision register |
| Main process | 11 | Covered | BPMN, if required later, is separate work |
| Alternative paths | 12 | Covered | Approved behavior is classified; downstream detail remains gated |
| Exception scenarios | 12 | Covered | Approved behavior is classified; downstream detail remains gated |
| Business rules | 13 | Covered | 46 stable IAM-BR rules |
| Document lifecycle | 20 | Covered | Identity/access evidence lifecycle; no commercial document |
| Status transitions | 19 | Covered | Approved Release 1 states; downstream naming may refine implementation detail |
| Data requirements | 17 | Covered | Business information only; no structures |
| Validation rules | 18 | Covered | 18 stable business validations |
| Permissions | 14 | Covered | Atomic Permission and Role/scope distinction |
| Approval controls | 16 | Covered | Tenant Administrator ordinary assignment; one separate named approver for privileged assignment; no self-approval |
| Separation of duties | 15 | Covered | Buying/receiving/payment-release separability approved; detailed catalogue remains MESP-38 |
| Inventory impact | 25.1 | Covered | Indirect authorization only; MESP-33 owns transactions |
| Accounting impact | 25.2 | Covered | Indirect authorization only; MESP-34 owns transactions |
| Multi-currency impact | 25.3 | Covered | No currency calculation; MESP-34/MESP-54 own it |
| Saudi localization impact | 25.4 | Covered | Bilingual/RTL and Saudi production gates |
| Reports and KPIs | 21 | Covered | Report catalogue remains business-level |
| Audit evidence | 22 | Covered | Material event catalogue |
| Integration requirements | 23 | Covered | Business dependencies, not interface design |
| Migration requirements | 24 | Covered | Mapping, reconciliation, exceptions, approval |
| Given/When/Then scenarios | 26 | Covered | 40 business acceptance scenarios |
| Founder-approved decisions | 27 | Covered | 22 historical IAM-OD records plus IAM-OD-023 resolved on 3 August 2026 (23 total) |
| Business-owner approval | 30 | Approved | Hossam approval recorded for v0.3 on 3 August 2026; implementation remains separately gated |

### Coverage result

All Jira MESP-28 required outputs have a dedicated section and an owner or explicit downstream gate. No coverage gap remains. All Release 1 business decisions are approved; only explicitly named downstream or production gates remain.

## 30. Founder Approval and Release 1 Baseline

### 30.1 Approved boundaries

The following boundaries are approved for Release 1:

- Identity and Access is a business requirements baseline, separate from implementation design.
- The hierarchy remains Platform -> Tenant -> Company / Legal Entity -> Branch -> Warehouse.
- User, Tenant Membership, Role, Permission, Access Scope, Entitlement, and Audit Event remain distinct concepts.
- Tenant isolation, least privilege, deny-by-default, support boundaries, session invalidation after critical changes, auditability, and offboarding evidence are required controls.
- Suppliers are external business parties and are not system Users.
- Wafra is validation-only and creates no reusable Tenant-specific behavior.
- Release 1 is B2B ERP only; Retail POS is excluded.
- The 22 historical IAM-OD records, new IAM-OD-023 decision (23 total), and four source-conflict records are retained as resolved traceability evidence.
- Global User lifecycle authority and Tenant Membership containment are separate controls. A Tenant Administrator cannot suspend, reactivate, or offboard the global User.

### 30.2 Founder approval record

| Approval field | Record |
|---|---|
| Approver | Hossam |
| Approval date | 3 August 2026 for v0.3 change-control; original baseline approval was 2 August 2026 |
| Baseline | Identity and Access BRD v0.3 — Approved Release 1 Baseline |
| Requirement result | 46 explicit IAM business rules: 46 Confirmed, 0 Proposed |
| Decision result | 22 historical IAM-OD records plus IAM-OD-023 resolved (23 total); 34 founder-approved decision points applied across the baseline |
| Source result | IAM-SC-001 retained as a nonblocking provenance note; IAM-SC-002 through IAM-SC-004 resolved |
| Production boundary | MESP-50 remains the gate for retention, privacy, residency, legal hold, and purge; no automated audit-evidence purge in Release 1 |
| Delivery boundary | This approval authorizes downstream requirements/design work only; it does not authorize implementation, Sprint creation, or code |

### 30.3 Deferred gates

The following are explicit downstream or production gates, not unresolved MESP-28 requirements:

- MESP-50 qualified validation of retention, privacy, residency, legal hold, and purge.
- MESP-29 and MESP-30 detailed Tenant and organization behavior.
- MESP-38 detailed Separation of Duties catalogue and exception evidence.
- MESP-40 migration mapping and reconciliation detail.
- Downstream ADRs and one Lean Implementation Specification per approved foundation/domain slice.

No separate DDD, FRS, Data Design, or TDS document is required as a standing gate. No implementation backlog, Sprint, or code is created by this BRD approval.

### 30.4 Approval checklist

| Approval item | Final status |
|---|---|
| Identity and Access business scope and boundaries | Approved |
| 46-rule classification register (46 Confirmed, 0 Proposed) | Approved |
| IAM-OD-001 through IAM-OD-023 | Approved and resolved |
| IAM-SC-001 through IAM-SC-004 | Resolved / nonblocking provenance retained |
| Suppliers external and not system Users | Approved |
| Wafra validation-only treatment | Approved |
| Release 1 B2B-only and Retail POS exclusion | Approved |
| No implementation backlog, Sprint, or code starts from this document | Approved |
| Approver / date | Hossam / 3 August 2026 (v0.3 decision record; original baseline approval 2 August 2026) |
| Requested changes | None recorded |

**This document is the Approved Release 1 Baseline. MESP-28 remains Done; v0.3 records the founder-approved global User versus Tenant Membership lifecycle decision. Implementation remains separately gated by the Product Delivery Master Plan and downstream requirements.**
