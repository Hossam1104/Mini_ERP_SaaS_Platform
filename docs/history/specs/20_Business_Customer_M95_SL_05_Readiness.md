# Business Customer - M95-SL-05 Readiness and Decision Gate

> **MESP-108 checkpoint clarification - 10 August 2026.** Independent Opus 5
> accepted Customer failure-classification coverage as a non-blocking Low
> finding (O5-007). A future bounded hardening item should prove observable
> HTTP-level parity with Product/Supplier for denial versus authorization
> outage, duplicate/conflict, concurrency, not-found, validation, and internal
> failure responses. This readiness record and Customer behavior are unchanged.
> The 21 SQL safety cases referenced historically are Foundation-only and do
> not validate `BusinessPartiesDbContext`; current backend arithmetic is 670
> non-SQL + 21 separately gated Foundation SQL = 691.

**Date:** 10 August 2026
**Jira:** MESP-105 - Prepare M95-SL-05 Business Customer readiness and decision gate
**Parent:** MESP-6 - EPIC 06 - Master Data and Product Catalog
**Status:** Done; Owner-disposed readiness gate
**Owner disposition evidence:** Jira comment `10691`
**Closure evidence:** Jira comment `10693`
**Implementation activation:** MESP-107; Jira comment `10692`
**Implementation evidence:** [PR #41](https://github.com/Hossam1104/mini-erp-saas-platform/pull/41) merged to `main` at `fb632982d06fd4f6bf965fb15dff7701a0bddcec`
**Review PR:** [#40](https://github.com/Hossam1104/mini-erp-saas-platform/pull/40) (documentation-only readiness/state handoff; merged to `main` at `aa778038a509ad24ffabcd5d0fbb1824002451df`)
**Shared follow-up:** MESP-106 - Master Data authorization and duplicate-audit classification hardening (Done through PR #42; closure evidence `10730`; non-blocking)

## 1. Readiness verdict

M95-SL-05 is **ready for the separate Business Customer implementation item**.
Hossam's Customer-only Owner disposition is recorded in Jira comment `10691`.
MESP-105 is **Done**, and MESP-107 is the separately created and activated
implementation item under MESP-6, with activation evidence in comment `10692`.
This readiness/state handoff adds no Customer source behavior, persistence,
migration, API, UI, or downstream transaction behavior. MD-OD-007 remains an
external Saudi statutory, legal/tax, and production gate under MESP-49; it is
not silently resolved here.

## 2. Authority and scope

This record is subordinate to the approved product and architecture baselines.
The applicable authority order is: explicit Owner decisions, approved PRD,
owning BRD, approved architecture/ADRs, Jira evidence, then glossary and plan
material. The primary sources reviewed were:

- `docs/16_Master_Data_and_Product_Catalog_BRD.md`, v0.3 Approved Business
  Baseline, approved by Hossam in Jira comment `10649` at content head
  `1e2d055354f0ddde833190948d09fa426707484c`.
- `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md`.
- `docs/00_ERP_Business_Glossary.md`.
- `docs/15_Foundation_Release_1_Lean_Implementation_Specification.md`.
- `docs/01_Technology_Architecture_Baseline.md`, ADR-002 and ADR-006, and the
  indexed ADR-005/ADR-011 baselines.
- `docs/94_Product_Delivery_Master_Plan.md` and `docs/Decisions.md`.
- `docs/19_Supplier_M95_SL_04_Readiness.md`, used for comparison only; its
  Owner dispositions are explicitly Supplier-only.

MESP-105 was created because the live Jira search found no dedicated
Business Customer readiness issue after completed MESP-104. It was the single
active readiness item under MESP-6. After the Owner disposition, MESP-105 was
closed and MESP-107 was created and activated as the single implementation
item. The Jira activation records that MESP-35, MESP-46, and the remaining
Master Data decisions are separate ownership boundaries.

This session is bounded to readiness, decision analysis, traceability, and
handoff. It adds no Business Customer source behavior, persistence, migration,
API, UI, or downstream transaction behavior.

## 3. Business Customer identity boundary

The Business Customer is an external B2B counterparty to whom a Company sells.
It is a business role and master-data identity, not a User, login identity,
Tenant membership, credential holder, authentication subject, consumer session,
or external-party portal account. Release 1 is B2B ERP only. An anonymous
walk-in or retail consumer is rejected as a Business Customer and is not
represented through a hidden consumer record.

Supplier and Business Customer remain distinct role records. A legal entity may
be both roles, and a cross-role match may be surfaced for review or optional
linkage, but the match must never reject the Customer or create a unified Party
record by implication. The common Business Parties seam may hold shared
counterparty identity/contact concepts later, subject to its owning design; it
does not authorize a new unified Party aggregate in this slice.

The Customer base identity is separate from the commercial Customer profile and
transaction behavior owned by Sales. The base slice may expose a stable B2B
reference contract for later Sales consumption. It does not implement orders,
quotes, credit enforcement, AR, invoices, payment collection, tax posting,
price precedence, or currency conversion.

## 4. Candidate identity and reference contract

The approved BRD supports the following candidate Customer identity data. The
list is a readiness boundary, not a physical schema or an implementation
commitment.

| Candidate concept | Readiness boundary |
|---|---|
| Legal name | Arabic and English localized legal names are required according to the approved bilingual rule; exact normalization/search behavior waits for ADR-011 implementation timing and must not be invented here. |
| Trading name | Arabic and English localized trading names are supported where the BRD requires them; requiredness and validation remain the approved BRD rule, not a new legal-field decision. |
| Customer code | Required and unique within the approved Tenant-owned Customer identity scope; downstream Company/business-context references must not redefine Customer ownership. |
| VAT/tax registration | Conditional reference only. Fields beyond the approved VAT/registration baseline remain subject to MD-OD-007, external validation, and MESP-49. |
| Contacts | Optional Customer contacts are external-party contacts, not Users and not portal identities. |
| Default Payment Term | Optional active reference; Payment Terms remain Finance-owned and are not implemented here. |
| Default Price List | Optional active reference; Sales owns price-list precedence and behavior under MESP-35. |
| Default Currency | Optional active reference; Currency and exchange-rate ownership remains MESP-34/MESP-54. |
| Credit Limit | Reference/placeholder only. Finance and MESP-46 own value, enforcement, warning, blocking, override, and approval mechanics. |
| Status | No Draft state; authorized creation is Active, with guarded Deactivate/Reactivate under the approved Customer-only MD-OD-008 disposition. |

No bank account, payment method, settlement, tax certificate catalogue,
invoice profile, credit engine, or customer login field is added by this
readiness document.

## 5. Tenant ownership and business scope

Tenant is the server-derived ownership and isolation boundary. A Customer must
not be readable, writable, searchable, or inferable across Tenants. A Platform
Administrator alone does not obtain Tenant business-data access; access must
come through the Foundation-approved ordinary Tenant membership or support
grant context, entitlement, permission, resource policy, and applicable
Company/Branch business scope.

The approved Customer-only scope is **Tenant-wide inside its owning Tenant,
reusable by that Tenant's Companies/Branches, with no cross-Tenant sharing**.
It is recorded in Jira comment `10691` and is not inherited from the
Supplier/Product precedents or generalized to another domain. Customer
ownership remains Tenant-wide even when downstream Sales or Finance profiles
later carry Company/business-context configuration.

Company/Branch selection may affect downstream Sales use and access scope. It
must not be treated as a client-supplied ownership override. An unset or
missing business scope must not be interpreted as Tenant-wide by accident; the
server must apply the approved resource policy and fail closed when policy is
unavailable or ambiguous.

## 6. Duplicate and cross-role matching

Within one Tenant and the same Customer role, the BRD requires duplicate
handling using legal/trading name and applicable tax-registration evidence.
The final normalized comparison, collation, locale and conflict response must
follow the approved localization/ADR-011 path. A same-role duplicate must
produce a deterministic validation/conflict result and must not be bypassed by
changing the caller's Company, Branch, or client-provided Tenant value.

A Customer match against a Supplier is a **cross-role review or optional-link
signal**, not a same-role duplicate and not a rejection. The same legal entity
may validly be both Supplier and Customer. The result must be auditable and
safe against cross-Tenant existence leakage. The slice must not introduce a
unified Party entity or silently merge either role.

## 7. Lifecycle and historical preservation

The BRD requires deactivation to block a Customer from new Sales Order use
while preserving historical references and reporting. Existing historical
records are not rewritten or deleted merely because the Customer becomes
Inactive. Deletion is not a substitute for deactivation where posted or
historical references exist.

The approved Customer-only lifecycle is **no Draft; authorized creation is
Active, with guarded Deactivate/Reactivate and preserved history**. It is
recorded in Jira comment `10691`. The implementation must enforce
operation-level permission, exact server-derived Tenant/resource
authorization, optimistic concurrency, duplicate/integrity checks, and audit;
no unapproved effective-date or statutory rule may be invented.

Inactive Customer records may remain visible to authorized history/reporting
queries but must be rejected as new selectable Customer references where the
owning downstream operation requires an active record. Sales owns the exact
Sales Order enforcement contract; this slice does not implement it.

## 8. Authorization, resource policy, and approval

Any future Customer command or query must evaluate server-derived inputs:

- active User/session and exactly one approved Tenant context;
- ordinary Tenant membership or authorized support grant, never a generic
  platform-admin bypass;
- active entitlement and plain-language permission for the operation;
- Customer resource identity, Tenant owner, approved BusinessScope and
  Company/Branch access scope;
- lifecycle state, concurrency token and idempotency key where applicable; and
- an approval result only if an approved policy requires a separate approver.

The API must fail closed when Tenant context, scope policy, permission policy,
resource policy, or required approval is unavailable. Safe error responses must
not disclose whether a record exists in another Tenant. Authorization is a
server-side application/resource policy, not a UI visibility rule.

The approved Customer-only MD-OD-005 policy requires no separate approver for
routine Customer creation, identity/name, code/reference, contacts,
activation, deactivation, or reactivation. Permission, Tenant/resource
authorization, audit, concurrency, fail-closed dependency handling, and
integrity remain mandatory. Credit, banking/payment, statutory/tax-sensitive,
Finance/AR, Sales override, and settlement operations remain with their owning
domains; the generic approval architecture remains available where a separate
policy is later approved.

## 9. Concurrency, idempotency, audit, and import evidence

Future writes need optimistic concurrency with a stale-write rejection that
does not silently overwrite a newer Customer state. Create/import/retry
operations need a bounded idempotency key scoped to the Tenant and operation;
asynchronous work must revalidate Tenant ownership before effect. No physical
token, index, or persistence implementation is introduced here.

Audit evidence must be sufficient to reconstruct actor, authorization path,
Tenant and business scope, operation, target, result, before/after values,
reason, correlation/evidence identity, approval where applicable, and time.
Audit records must not contain credentials or secrets, and first-persistent
audit fidelity must be preserved. MESP-50 retention, privacy, legal-hold,
purge, residency, backup, and restoration gates remain open.

Later import work must be Tenant-bound and use preview, validation,
quarantine, sign-off, commit, row-level errors, and reconciliation. MESP-40
owns the import/migration implementation. This readiness session does not
create an import endpoint, migration, seed data, or production database.

## 10. Future code and persistence boundary

MESP-107 is the separate activated implementation item. Its work must preserve
the ADR-002 topology:

`MiniErp.Api -> MiniErp.Infrastructure -> MiniErp.App -> MiniErp.Contracts`

Contracts remain provider-free, App remains free of EF/Infrastructure/Api,
Infrastructure owns EF/provider/module persistence, and the API composes the
host. ADR-006 requires module-owned EF models, mappings, repositories, schema
and migrations, immutable Tenant ownership, stored ownership checks, and
explicit cross-module contracts/transactions. The future Customer design must
not reach into Supplier/Product/Inventory/Sales/Finance tables directly or
invent a shared persistence owner.

This session creates no entity, table, schema, EF mapping, repository, service,
endpoint, generated client, UI, migration, database, provider configuration,
or production-access claim.

## 11. Downstream ownership and decision dependencies

| Dependency | Owner/boundary for this gate |
|---|---|
| B2B Customer use in quotes and Sales Orders | Sales/MESP-35 owns the commercial Customer profile, selection rules, inactive-use enforcement, price precedence, discount, quote, order, and AR handoff. |
| Credit limit and credit control | Finance/MESP-46 owns mechanics, warning/block/override and approval; Customer only carries an allowed reference or optional value boundary. |
| Payment Terms | Finance/MESP-34 owns term structure, due-date calculation, posting and reconciliation. |
| Price List | Sales/MESP-35 owns precedence, effective dates, prices, discounts and downstream selection. |
| Default Currency and Exchange Rates | Finance/MESP-34 owns monetary behavior; MESP-54 remains an unapproved exchange-rate sourcing/approval decision. |
| Tax and Saudi statutory fields | Tax/Finance plus MESP-49 owns statutory validation and production readiness; no legal conclusion is made here. |
| Payments, banking and settlement | MESP-47 and owning Finance boundaries; not Customer master identity behavior. |
| Import/migration/opening data | MESP-40/MESP-51 owning boundaries; no physical migration here. |
| Supported volume, retention, privacy, legal hold and production operations | MESP-48/MESP-50 and the production gates remain open. |

The optional default references must not become hidden implementations of any
downstream domain. A later Customer implementation can expose stable,
Tenant-safe references only after the owning contracts and decision gates are
ready.

## 12. Acceptance and validation traceability

The following are readiness acceptance targets, not claims that production
behavior exists:

| Trace | Customer-specific readiness interpretation |
|---|---|
| MD-BR-025 / MD-AC-011 | Business Customer is B2B only; anonymous retail/walk-in consumer input is rejected and is not modeled as a Customer. |
| MD-BR-026 / MD-AC-028 | Same-role duplicate detection is Tenant-isolated and does not leak cross-Tenant existence. |
| MD-BR-027 / MD-AC-012 / MD-AC-026 | Inactive Customer cannot be used for new Sales Order selection while historical references remain visible and unchanged to authorized readers. |
| MD-BR-044 / MD-AC-029 | Permission-holding, scope-authorized actor is required; platform administrator alone cannot grant Tenant access. |
| MD-AC-030 | Required Arabic/English localized identity fields follow the approved localization path; no invented collation/normalization rule. |
| MD-AC-031 | A later downstream contract must preserve Customer default EUR and Tenant/base SAR conversion semantics; this session does not implement money behavior. |
| MD-AC-032 | Audit must capture actor, Tenant, operation, before/after, result, time and correlation/evidence context. |
| MD-BR-045 / MD-AC-035 | Cross-role Supplier/Customer match is review/optional link evidence, succeeds without rejection, and is not a unified Party. |
| MD-VR-001 / MD-VR-002 / MD-VR-010 / MD-VR-014 | Tenant ownership, server authority, duplicate/lifecycle integrity, and auditable conflict outcomes must be tested in the later implementation. |
| MD-OD-001 / MD-OD-005 / MD-OD-008 | Customer-specific Owner disposition is recorded in Jira comment `10691`; it applies only to the bounded Customer identity slice and is a prerequisite now satisfied for MESP-107. |

The future focused suite must include positive and negative Tenant isolation,
safe cross-Tenant not-found/denial behavior, B2B-only rejection, same-role
duplicate conflict, cross-role non-blocking match, inactive history/new-use
behavior, bilingual requiredness, audit fidelity, stale-write rejection, and
no-login/no-credential proof. The suite must not be presented as run in this
readiness session.

## 13. Owner disposition - Business Customer only

The following disposition is recorded in Jira comment `10691`. It is scoped
only to Business Customer and must not be treated as a global resolution of the
preserved decision register or as approval for downstream commercial policy.

| Decision | Approved Customer-only disposition |
|---|---|
| **BC-OD-001 / MD-OD-001 - Customer availability** | Customer master identity is Tenant-wide inside its owning Tenant and may be reused by authorized Companies and Branches in that Tenant. Cross-Tenant sharing is prohibited. Tenant ownership and resource authorization are derived from trusted server-side context; client-supplied Tenant, Company, Branch, or scope values cannot expand authority. Company/business-context Sales and Finance configuration remains separately owned and must reference this Tenant-owned identity without duplicating ownership. |
| **BC-OD-005 / MD-OD-005 - Customer approval policy** | No separate Release-1 approver is required for routine Customer creation, identity/name maintenance, Customer code/reference maintenance, contact maintenance, activation, deactivation, or reactivation. Permission, trusted Tenant/resource authorization, optimistic concurrency, audit, fail-closed dependency handling, and applicable integrity checks remain mandatory. Credit-control, banking/payment, statutory/tax-sensitive, Finance/AR, Sales override, and payment/settlement operations remain governed by their owning domains. |
| **BC-OD-008 / MD-OD-008 - Customer lifecycle** | Customer has no Draft state in Release 1. A valid authorized Customer is created Active. Deactivate and Reactivate are explicit guarded operations. Deactivation prevents selection for new applicable downstream transactions where an Active Customer is required while preserving historical references, posted-document references, reporting visibility, and audit history. Reactivation requires current authorization, Tenant/resource validation, optimistic concurrency, duplicate/integrity validation, and audit. |

MD-OD-007 is intentionally not part of this Owner bundle. Saudi statutory,
legal/tax fields beyond the approved conditional baseline require external
validation and remain a MESP-49 production gate. MESP-46, MESP-47, MESP-34,
MESP-35, MESP-40, MESP-48 and MESP-50 likewise remain in their owning scope.

No decision is needed to establish that a Customer is B2B-only, external to
the User/membership model, role-local rather than a unified Party, or allowed
to cross-match a Supplier for review without rejection; those boundaries are
already supported by the approved BRD. They must still be preserved in the
implementation DoR.

This disposition does not resolve or implicitly approve MESP-46 credit-control
policy, MESP-47 payment/receipt methods, B2B Sales behavior, AR/Finance,
Price List precedence, Payment Term behavior, Currency or Exchange Rate
behavior, Tax behavior, Saudi statutory requirements, customer portal/login,
unified Party architecture, or Retail POS behavior.

## 14. Definition of Ready and implementation activation

The readiness gate is satisfied for the bounded Customer slice because:

- the Owner recorded one explicit Customer-only disposition for MD-OD-001,
  MD-OD-005, and MD-OD-008 in Jira comment `10691`;
- the repository and Jira record the exact Customer outcome without rewriting
  the global decision register or Supplier history;
- Business Parties/common identity and Sales Customer reference ownership are
  preserved as implementation boundaries rather than silently expanded;
- ADR-011 timing remains required before localized search/forms/RTL/bilingual
  behavior, and no unapproved collation or legal rule is invented;
- authorization, audit, lifecycle, concurrency, integrity, idempotency,
  import, and failure contracts remain required and Tenant-safe; and
- MESP-107 was created under MESP-6 and explicitly activated with Jira comment
  `10692` as the single next implementation item.

MESP-105 is Done. The next implementation session is **MESP-107 Business
Customer master-data implementation only**. It may add only the bounded
Customer source behavior and required tests/documentation; it must not begin
downstream commercial or statutory behavior in the same or a later automatic
handoff.

## 15. Hard exclusions and preserved gates

This record does not authorize Product/Item/SKU/Barcode/Category/UOM/Supplier
source changes, a unified Party or variant entity, customer login/credentials/
membership, an external portal, Retail POS or Wafra core behavior, Sales/AR/AP/
Finance/Tax/credit/payment/banking/settlement/Inventory behavior, Price List,
Payment Terms, Currency or Exchange Rate behavior, migration, SQL/provider
configuration, database provisioning, legal certification, Saudi validation,
or production readiness.

MESP-48 supported volume and operational gates, MESP-49 Saudi/statutory and
production gates, and MESP-50 retention/privacy/legal-hold/purge/residency/
backup/restoration gates remain open. No credential or production connection
string was required, created, or claimed.

## 16. Non-blocking shared hardening follow-up

MESP-106 is the single shared follow-up for two observations carried forward
without changing this gate:

- classify unavailable/unmapped authorization dependencies such as
  `permission_unavailable`, `scope_policy_unavailable`,
  `approval_policy_unavailable`, `resource_policy_unavailable`, and
  `authorization_operation_unmapped` as service/configuration failures where
  appropriate, while keeping genuine permission/resource denial as denial and
  preserving fail-closed behavior; and
- review deterministic Supplier duplicate outcomes such as
  `supplier_duplicate`, `supplier_code_duplicate`, and
  `supplier_registration_duplicate` so they use the approved validation/
  conflict audit classification rather than a generic internal failure.

MESP-106 is **Done** and non-blocking through PR #42, merged to `main` at
`0f712edcf58119057d614000721fe41227383bc1`. Its Product/Supplier-only
correction preserved genuine denials, fail-closed outage behavior, deterministic
Supplier duplicate conflict classification, and failure audit evidence. It did
not change Customer source behavior, broaden Supplier/Product scope, or expand
MESP-107 beyond the approved Customer slice. Focused classification tests are
82/82, the full non-SQL suite is 670/670, and the Release build is 0/0; the 21
SQL safety tests remain gated by the missing connection string.
Jira activation, validation, and closure evidence are comments
`10728`/`10729`/`10730`.

## 17. Handoff

MESP-105 is Done with the Customer-only Owner disposition in Jira comment
`10691` and closure evidence in comment `10693`; MESP-107 is now Done with
activation evidence in comment `10692`. The bounded MESP-107 source
implementation is complete through PR #41, merged to `main` at
`fb632982d06fd4f6bf965fb15dff7701a0bddcec`. It provides the external B2B Customer identity, Tenant-safe
authorization, same-role integrity, Active/Inactive lifecycle, concurrency,
audit, contacts, contracts/routes, and module-owned persistence boundary. The
focused Customer tests are 14/14 and the non-SQL architecture suite is
623/623; the 21 SQL safety tests remain gated by the missing
`MESP_SQLSERVER_CONNECTION_STRING`. Review PR #40 carries the
documentation/state handoff and merged to `main` at
`aa778038a509ad24ffabcd5d0fbb1824002451df`. No statutory registration,
downstream commercial behavior, migration, provider, or production-readiness
claim is made. MESP-106 hardening is complete. The next exact root `TASK.md`
session is the existing MESP-23 governance/open-questions register only; it
must not execute another Jira item automatically.
