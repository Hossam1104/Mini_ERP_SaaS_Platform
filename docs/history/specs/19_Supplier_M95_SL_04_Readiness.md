# M95-SL-04 Supplier Readiness and Decision Gate

> **MESP-108 checkpoint clarification - 10 August 2026.** Existing Supplier
> localized storage and SQLite/unit evidence do not prove SQL Server
> collation/unique-index parity or Arabic linguistic search behavior. ADR-011
> remains required. The separately gated 21 SQL safety cases are
> Foundation-only and do not validate `BusinessPartiesDbContext`. See
> `docs/98_Independent_Opus_5_Checkpoint_Reconciliation.md`; no Supplier source
> or test behavior changes here.

**Date:** 9 August 2026
**Jira:** MESP-103 (Done)
**Scope:** Supplier readiness and decision gate only
**Status:** Supplier-only readiness complete and closed; the separate MESP-104
implementation is complete at its bounded source scope

**Jira evidence:** activation comment `10679`; readiness analysis and decision
bundle comment `10680`; Owner disposition comment `10681`; closure comment
`10682`; next-item handoff comment `10683`; implementation item `MESP-104` is
now Done through PR #39, with implementation validation and closure evidence in
Jira comments `10685`/`10686`/`10687`.

## Post-readiness implementation overlay - 10 August 2026

MESP-103 remains the authoritative Supplier readiness and decision-gate record;
this document does not expand or retroactively change that readiness scope.
MESP-104 separately implemented the approved Supplier-only boundary through
PR #39, merged to `main` at `721adeb27c366d2b8aedde66d006ac6a49956f99` from
head `9bf9afcd8a9ea427ed32b63ad9b655081e9592d3`. The implementation provides
module-owned Tenant-filtered persistence/API, server-derived authorization,
exact same-role duplicate controls, non-blocking cross-role match evidence,
Active/Inactive lifecycle, optimistic concurrency, and append-before-effect
audit. Release build 0/0, Supplier tests 7/7, and non-SQL tests 609/609 passed.
The SQL Server safety gate remains unavailable without
`MESP_SQLSERVER_CONNECTION_STRING`; no migration, provider, or production
readiness claim is made. Business Customer is not implemented, and Supplier
Owner decisions are not inherited by that later slice.

## 1. Readiness verdict

M95-SL-04 is complete and closed as a Supplier-only readiness and decision
gate. The Owner disposition recorded in Jira comment `10681` resolves
MD-OD-001, MD-OD-005, and MD-OD-008 for this bounded Supplier slice only:
Tenant-wide business availability inside the owning Tenant, no separate
approver for routine Supplier maintenance, and Active-on-authorized-create with
guarded Deactivate/Reactivate and preserved history. Permission, server-derived
Tenant authorization, optimistic concurrency, audit, and fail-closed controls
remain mandatory. Jira closure evidence is comment `10682`.

MESP-103 was activated as the single Supplier readiness item under MESP-6 in
Jira comment `10679`, after MESP-102 was verified Done and Product PR #37 was
verified merged. This document completes the independent analysis and presents
one consolidated decision bundle. It does not create Supplier source behavior,
persistence, API behavior, UI, or a migration.

The completed readiness boundary is a real business-scope disposition, not a
technical implementation claim:

- Supplier is an external Business Party role, never a User, Tenant member,
  credential holder, login identity, or consumer session.
- Supplier ownership, identity, duplicate handling, lifecycle, authorization,
  audit, concurrency, historical preservation, import, and downstream seams
  are sufficiently specified for a later bounded implementation once the
  affected decision register entries are disposed.
- The business-availability, approval-catalogue, and Draft/Active choices are
  resolved for Supplier only by comment `10681`; the global decision register
  remains preserved and these choices must not be copied into Business Customer,
  Procurement, Finance, Tax, payment/banking, or another downstream domain.
- MD-OD-007 remains a Saudi statutory/external-validation gate. No legal field
  set beyond the approved conditional VAT/registration baseline is invented.

At the MESP-103 readiness boundary, no Supplier source behavior, persistence,
API, UI, migration, or downstream business behavior was added. MESP-104 was the
separately prepared next Jira implementation item and is now complete at its
bounded source scope; this historical readiness statement does not claim that
the implementation was part of MESP-103.

## 2. Baseline and authority

The analysis re-read the approved BRD v0.3, the Lean Implementation
Specification, the ERP glossary, the Foundation Release 1 specification, the
Technology Architecture Baseline, ADR-002, ADR-005, ADR-006, ADR-011's indexed
timing, the Product Delivery Master Plan, the statistics tracker, and the
current-state handoff.

The authoritative architectural constraints are:

- Release 1 is B2B ERP only. Retail POS and Wafra-specific core behavior are
  excluded.
- The backend remains the four-project topology enforced by ADR-002:
  `MiniErp.Api`, `MiniErp.App`, `MiniErp.Contracts`, and
  `MiniErp.Infrastructure`.
- ADR-006 governs shared SQL Server, module-owned EF contexts/schemas/migrations,
  stored Tenant ownership, transactions, and provider/production gates.
- ADR-005 governs server-derived authorization inputs. A client Tenant or scope
  hint cannot broaden authority.
- ADR-011 remains required before localized search, forms, RTL behavior, or
  bilingual business-document generation is implemented.
- MESP-48, MESP-49, and MESP-50 remain open production gates.

## 3. Consolidated Owner decision bundle

The following three decisions are the single Supplier-specific Owner bundle.
They are recommendations for efficient disposition, not decisions claimed as
approved by this session. One Owner response may accept, amend, or reject each
row in the same Jira evidence comment.

| Decision | Strongest bounded recommendation | Alternatives and implementation impact | Current status |
|---|---|---|---|
| **MD-OD-001 - Supplier business availability** | Make Supplier master data Tenant-wide inside its owning Tenant and reusable by that Tenant's Companies and Branches. Keep no cross-Tenant sharing. Use trusted server-derived Tenant authority and a future explicit Supplier scope policy. | Company/Legal Entity scope would require a narrower scope anchor, scope-aware uniqueness, downward-access tests, and downstream Procurement reference rules. Branch scope would add still more scope transitions and historical-reference complexity. A generic absent-scope fallback is not acceptable. | **Owner-approved for the bounded Supplier slice in Jira comment `10681`.** Client-supplied Company, Branch, Tenant, or scope values cannot override server authority. |
| **MD-OD-005 - Supplier approval catalogue** | Do not require a separate approver for routine Supplier identity, localized-name, contact, code, ordinary reference, Deactivate, and Reactivate operations. Keep permission, exact server-derived Tenant/resource authorization, audit, optimistic concurrency, and fail-closed behavior mandatory. Keep statutory/tax-sensitive or future payment/bank-detail changes out of this base slice; the owning domain must apply its own approved controls. | Approving every Supplier write adds approval state, separation-of-duties, pending/rejection transitions, and publication semantics to ordinary master maintenance. Approving only sensitive fields requires a field-level policy/effective-value model and clear Procurement/Finance ownership. | **Owner-approved for the bounded Supplier slice in Jira comment `10681`.** Saudi statutory and future payment/banking/settlement changes are outside this base disposition. |
| **MD-OD-008 - Supplier lifecycle** | Do not introduce Draft for the bounded Supplier master record. A valid authorized create becomes Active; Deactivate changes it to Inactive; Reactivate is separately authorized and guarded. Inactive Suppliers cannot be selected for new Procurement use, while historical references remain readable and auditable. | Draft-before-Active requires draft storage, visibility rules, validation timing, publication/approval semantics, duplicate behavior during draft, and migration handling. A no-Draft rule keeps lifecycle small and preserves the existing Active/Inactive contract. | **Owner-approved for the bounded Supplier slice in Jira comment `10681`.** Historical references and audit history remain preserved. |

**MD-OD-007 is intentionally outside this Owner bundle.** The approved baseline
supports conditional Saudi tax/registration requirements where applicable, but
the exact statutory fields beyond VAT (for example, any commercial-registration
or other jurisdiction-specific field) require qualified external validation.
MESP-49 remains the owning production/external-validation gate. The Supplier
implementation must keep the statutory contract extensible and must not claim
legal completeness.

## 4. Supplier role and ownership boundary

Supplier is an external business party from whom a Company may procure. It is
master data inside a Tenant; it is not a Tenant, User, Employee, membership,
login, credential, authentication identity, or anonymous consumer.

The future design should use the Business Parties seam for common counterparty
identity, localized names, contacts, duplicate evidence, and role markers. It
must not create a new unified `Party` record or silently merge Supplier and
Business Customer. A record may match a Business Customer for review or an
optional explicit linkage, but cross-role similarity is not a duplicate and
must not reject either role. Procurement owns later purchasing/profile and PO
behavior; Finance owns later AP/payment behavior. This readiness slice owns
neither transaction.

## 5. Bounded future Supplier contract

The following is the implementation handoff boundary after the decision bundle
is recorded. It is not source implementation in this session.

### Identity and references

- Stable Supplier identifier, owning Tenant, approved business scope, lifecycle
  state, optimistic-concurrency version, and audit history are required.
- Legal and trading names support the BRD's Arabic/English localization intent.
  Arabic normalization, collation, tokenization, and localized search remain
  subject to ADR-011 and are not invented here.
- Supplier code is unique within the approved Tenant/role/scope boundary.
- Tax/VAT registration data is retained only to the approved statutory boundary;
  any additional Saudi field remains extensible and externally validated.
- Optional default Payment Term and Currency references must be active,
  same-Tenant, and validated by their owning domain when that future reference
  contract is implemented. This slice does not implement those domains.
- Reference snapshots are required at downstream transaction boundaries so
  later name/code changes do not rewrite historical Procurement or Finance
  evidence.

### Duplicates and cross-role matches

- Same-role duplicate detection is evaluated inside the approved Tenant and
  business scope using normalized legal/trading identity and applicable
  registration values. A high-confidence duplicate must hold or reject the
  operation according to the future validated policy and leave review evidence.
- A Supplier/Business Customer cross-role match is surfaced for review and may
  support an explicit optional linkage. It is not a same-role duplicate, must
  not block the operation solely because of the other role, and does not create
  a unified Party identity.
- Client-supplied IDs, Tenant IDs, or scope hints cannot broaden the lookup or
  duplicate boundary.

### Contacts and lifecycle

- Supplier contacts are optional named external contacts for communication.
  Recording a contact never grants access and never creates a User or Employee.
- The base lifecycle is Active/Inactive, subject to MD-OD-008 disposition.
  Deactivation blocks new selection by later Procurement operations but keeps
  historical references, audit, and reporting available.
- A referenced Supplier is not hard-deleted. Any future purge or legal-hold
  behavior remains under MESP-50 and applicable privacy/legal validation.
- Reactivation is an explicit authorized operation with current scope,
  duplicate/reference/integrity checks and optimistic concurrency.

### Authorization, audit, and concurrency

Every future command must require an active authenticated User/session, one
server-derived Tenant context, the exact Supplier permission, the approved
resource/business-scope decision, lifecycle and reference validation, the
Owner-approved approval result where applicable, idempotency handling, and
optimistic concurrency. No Supplier endpoint may accept external-party
credentials or a Supplier login path.

Append-before-effect audit evidence must contain the Tenant, actor, action,
target record, scope, result, before/after values where applicable, reason,
correlation/evidence identity, timestamp, and approver identity only when an
approved policy requires one. Audit failure must prevent the business effect.
Provider or policy dependency failure must not be presented as a successful
operation or silently converted into an ordinary business denial.

## 6. Persistence, API, import, and integration readiness

No persistence or API was created by M95-SL-04. The Supplier-specific decision
gates are now complete, while the remaining Definition-of-Ready and production
gates still apply. The future implementation must use
module-owned persistence in `MiniErp.Infrastructure`, a Tenant-owned Supplier
aggregate, explicit Tenant ownership verification, a policy-neutral but
non-optional business-scope envelope, concurrency tokens, same-role uniqueness
constraints, and transactionally appended audit evidence. It must not add a
cross-module table or an unscoped query, and it must not execute a migration or
claim SQL/provider readiness without the configured gate.

Future `/api/v1` business routes and contracts are to be designed only after
the Owner bundle. Commands must derive authorization from server context and
must validate scope, lifecycle, duplicate/effective references, approval,
idempotency, concurrency, and audit before effect. Queries must preserve
Tenant isolation and historical visibility rules.

Import/migration is a later bounded activity: identify source ownership, map
Arabic/English fields, normalize values, detect same-role duplicates, preview
and quarantine invalid rows, obtain sign-off, commit idempotently, reconcile,
and audit. No ETL, import endpoint, or migration is implemented here.

Procurement may later read an Active Supplier for new purchasing use and retain
historical Supplier snapshots. Procurement workflow, PO confirmation, receipt,
invoice, payment, Tax, Finance, and Business Customer behavior remain outside
this slice.

## 7. Traceability and acceptance handoff

The later Supplier implementation must trace at least the following approved
BRD/LIS material without expanding scope:

| Evidence area | Required proof after implementation |
|---|---|
| External-party boundary | Supplier has no login, credential, authentication identity, membership, or consumer session (`MD-AC-008`). |
| Tenant and scope | Same-Tenant ownership is server-derived; cross-Tenant reads/writes and client-authority substitution are denied. |
| Same-role duplicates | Applicable tax/registration and normalized identity duplicates are held/rejected with audit evidence (`MD-AC-009`). |
| Cross-role match | Supplier/Business Customer similarity is surfaced for review or optional linkage and does not falsely reject (`MD-AC-035`). |
| Lifecycle/history | Deactivation prevents new use while preserving historical references and audit (`MD-AC-010`). No referenced hard delete. |
| Authorization | Permission, resource scope, lifecycle, approval result, concurrency, idempotency, and audit are server-enforced and fail closed. |
| Localization/statutory | Arabic/English contract stays within ADR-011 timing; Saudi fields stay within MD-OD-007 external validation and MESP-49. |
| Architecture | Four-project dependency direction, module-owned persistence, shared-SQL/Tenant controls, and no cross-module table access remain intact. |

## 8. Explicit exclusions

This readiness package does not implement or decide Product/Item/SKU/Barcode,
Category/UOM, Business Customer, Procurement transactions, purchasing profile,
POs, receipts, invoices, payments, Tax, Finance, Inventory, Price Lists,
Currencies, Exchange Rates, approval catalogue behavior outside the bundle,
Saudi legal compliance, external integrations, migrations, production
credentials, production database provisioning, Retail POS, or Wafra-specific
core behavior.

## 9. Non-blocking Product hardening follow-up

No Product source is changed by this session. The independent review recorded a
bounded future follow-up in the live state/backlog because no existing Jira
item was found for the exact issue.

`ProductIdentityPolicies.cs` and the shared Master Data authorization path fail
closed for `permission_unavailable`, `scope_policy_unavailable`,
`approval_policy_unavailable`, `resource_policy_unavailable`, and
`authorization_operation_unmapped`. That fail-closed security posture is
correct. The remaining hardening issue is failure classification: Product's
service-level audit-reason mapping currently falls through to generic
authorization denial for several of these codes, while the Product endpoint
explicitly maps only `permission_unavailable` among them and leaves other
unavailable/unmapped codes to the default client-error path.

The future bounded direction is to classify infrastructure/policy unavailability
as `FoundationAuditReason.InternalFailure`, return service-unavailable where
the dependency failure is infrastructural, preserve genuine permission denial
as authorization denial, and keep configured policy rejection fail-closed. This
is non-blocking for Supplier readiness and must be handled in a future Product
hardening item rather than by changing the completed Product slice here.

## 10. Next exact session

The next fresh root `TASK.md` session is **MESP-104 / M95-SL-04 Supplier
master-data implementation only**. It must re-read the Owner disposition and
closure evidence (`10681`/`10682`), verify that MESP-104 is the intended
separate To Do item, and implement only the approved Supplier boundary. It must
preserve MD-OD-007 as an external-validation/production gate and must not
generalize the Supplier disposition to another domain. No Supplier source work
starts automatically in this session.
