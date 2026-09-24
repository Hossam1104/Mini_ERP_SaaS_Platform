# M95-SL-03 Product Identity Readiness and Decision Gate

> **MESP-108 checkpoint clarification - 10 August 2026.** Product creation
> enforces active same-Tenant Category/Base UOM references, but Category/UOM
> creation is not currently exposed by mapped API routes. Product tests do not
> therefore establish a complete public API workflow from Category/UOM creation
> through Product creation. SQL Server collation/Arabic linguistic parity also
> remains unproved; the 21 SQL safety cases are Foundation-only. O5-001,
> O5-002, and O5-003 are recorded without changing Product
> behavior in `docs/98_Independent_Opus_5_Checkpoint_Reconciliation.md`.

## Document control

| Field | Value |
|---|---|
| Slice | M95-SL-03 — Product identity |
| Jira readiness item | MESP-101 — Prepare M95-SL-03 Product identity readiness and decision gate |
| Jira implementation item | MESP-102 — Implement M95-SL-03 Product Identity; Done with PR #37 and Jira closure evidence `10677` |
| Parent Epic | MESP-6 — EPIC 06 - Master Data and Product Catalog |
| Status | Approved readiness baseline plus MESP-102 implementation complete; PR #37 merged to `main` at `202d59068caac5d1fac402794627e41d7f452456`; MESP-102 Done with Jira closure evidence `10677` |
| Readiness PR | #36 — merged cleanly from `09d2e09f6a382187e8cdba32cd594f2b9ad15ab7` |
| Owner | Hossam / Product Owner |
| Scope | Product identity only; readiness baseline and bounded MESP-102 implementation evidence |
| Product implementation | Implemented separately under MESP-102; the MESP-101 readiness session itself remained documentation-only |
| Release boundary | Release 1 B2B ERP; no Retail POS and no Wafra-specific core behavior |

This note records the approved Product-only decision bounds and the exact
technical boundary used by the separately activated MESP-102 implementation
session. The MESP-101 readiness session itself did not create a Product entity,
table, migration, repository, service, endpoint, API implementation, or
business behavior; MESP-102 supplied the bounded source implementation under
those bounds. The remaining decision register and later slices remain gated.

## Authority and reviewed sources

The following sources were reread for this gate. The approved BRD remains the
business baseline; this note adds a slice-bounded execution overlay and does
not rewrite approved historical requirements.

| Source | Use in this gate |
|---|---|
| `docs/16_Master_Data_and_Product_Catalog_BRD.md` v0.3 | Product requirements, business rules, validation rules, acceptance scenarios, ownership and downstream boundaries; approved by Hossam in MESP-31 comment `10649` and closed with comment `10650`. |
| `docs/17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md` | Sequential slice definition, contract/persistence boundaries, localization timing, downstream snapshots, ADR gates, and the M95-SL-03 traceability row. |
| `docs/ADR-002_Backend_Project_Structure_and_Module_Enforcement.md` | Actual four-project dependency direction, API composition root, Infrastructure ownership, and module-persistence enforcement. |
| `docs/Decisions.md` and `docs/01_Technology_Architecture_Baseline.md` | ADR-005 authorization baseline and ADR-011 localization/search/RTL/bilingual-document timing. These are index/baseline records; no separate ADR-005 or ADR-011 file exists in the repository. |
| `docs/ADR-006_Module_Schemas_EF_Core_Migrations_Transactions.md` | Shared SQL Server shape, module-owned EF models/schemas/migrations, Tenant ownership, transaction and cross-module persistence constraints, and production gates. |
| `docs/15_Foundation_Release_1_Lean_Implementation_Specification.md` | Server-derived Tenant context, fail-closed authorization, audit identity, concurrency, module contracts, and API/application boundaries. |
| `docs/00_ERP_Business_Glossary.md` §4 | Product, Item, SKU, Barcode, Category, Unit of Measure, and Base Unit terminology and ownership. |
| `docs/94_Product_Delivery_Master_Plan.md` | Delivery sequence, active-item discipline, MESP-99 completion, M95-SL-03 handoff, and production-gate preservation. |
| Jira MESP-99 comments `10664`–`10667`, `10668`–`10670` | Category/UOM SL-02 activation, validation, closure, PR #34/#35 correction, and duplicate-artifact reconciliation. |
| Jira MESP-101 comment `10671` | M95-SL-03 activation and Hossam's six Product-only decision bounds. |
| Jira MESP-102 comments `10675`–`10677` | Product implementation activation, validation/merge, and closure evidence. |

ADR-005 is the approved policy/resource authorization baseline. It does not
permit a caller to select an arbitrary capability. The existing immutable
server-owned `MasterDataOperationCatalog` is the reusable operation-to-
capability binding. ADR-011 remains mandatory before Product localized search,
localized forms, RTL-specific UI behavior, or bilingual document generation;
this readiness note does not invent Arabic collation, normalization, search,
or document-rendering semantics.

## Owner-approved Product-only decision overlay

The six dispositions below are approved for Product identity only. They are
not global closures of MD-OD-001, MD-OD-003, MD-OD-005, MD-OD-008, MD-OD-010,
or MD-OD-011. A later domain must record its own scope-bounded disposition and
must not inherit these Product rules by default.

| Decision | Product-only approved bound |
|---|---|
| **MD-OD-011 — Product vs Item** | Release 1 treats Product and Item as one business concept. There is no separate Product/Item model, variant layer, or product-family layer. No future-looking variant architecture is introduced. SKU and Barcode identify the Product under the separately approved coding rules. |
| **MD-OD-003 — SKU and Barcode** | Product uses a hybrid SKU model. Every Product has a Tenant-unique SKU. Manual/imported SKU is allowed, and Tenant-configured server-side generation may be supported. SKU does not require embedded business semantics and has no Wafra-specific rule. Barcode is an optional alternate identifier: a Product may have zero or multiple barcodes, and barcode values are unique inside the owning Tenant. Core SaaS does not require EAN/GS1 or another specific format; format validation is configuration/integration-led. |
| **MD-OD-010 — Product tracking configuration** | Tracking is configurable and disabled by default. Category may provide a default, and Product may explicitly override that default. Product Identity owns only the Product-side configuration contract. Inventory owns stock structures, transactions, operational enforcement, and batch/lot/serial/expiry traceability. No Inventory tracking behavior is part of Product readiness or the next Product identity implementation unless a later task explicitly activates it. |
| **MD-OD-001 — Product business availability** | Product master data is Tenant-wide within the owning Tenant and reusable by all Companies/Branches inside that Tenant. There is no cross-Tenant sharing. Client-supplied Tenant or scope hints never replace trusted server-derived Tenant authority. Warehouse/location stock availability is a later Inventory concern and does not change Product master-data ownership. |
| **MD-OD-005 — Product approval policy** | Routine Product Create, Edit, Activate, Deactivate, and Reactivate do not require a separate approver in Release 1. Permission, exact server-derived Tenant/scope authorization, optimistic concurrency, audit evidence, and fail-closed authorization remain mandatory. The generic approval architecture remains available for a future configured policy. It is not a substitute for Product data-integrity restrictions. |
| **MD-OD-008 — Product lifecycle** | Product has no Draft state. A valid authorized Product is created Active. Product supports Deactivate and Reactivate. Deactivation prevents new business use where applicable; historical references remain valid and auditable. Reactivation remains subject to permission, Tenant authorization, concurrency, and applicable integrity rules. |

The BRD's remaining decisions are preserved. In particular, this overlay does
not resolve Price List precedence (MD-OD-004), UOM conversion policy for later
domains (MD-OD-006 outside its Category/UOM disposition), effective-dated
reactivation (MD-OD-009), Saudi statutory fields (MD-OD-007), Tax or exchange-
rate ownership, Inventory operational tracking, or any MESP-48/MESP-49/MESP-50
production gate.

## Product identity model

### Business identity

The next implementation session will represent one stable `ProductId` for one
Release-1 Product/Item concept. It will not create a separate Item record or a
variant/product-family abstraction. The Product is a reusable master fact, not
stock, a warehouse balance, a transaction line, or a batch/lot/serial record.

The Product identity contract is expected to carry, subject to the focused
implementation diff and the existing BRD requirements:

- stable Product identifier;
- one Tenant-unique SKU;
- zero or more Tenant-unique barcodes;
- English name and Arabic name when the Tenant's approved bilingual policy
  requires it;
- optional description;
- one active Category reference;
- one active Base UOM reference;
- Product-side tracking configuration and explicit override state;
- Product metadata flags required by the approved Product baseline, without
  implementing Procurement, Sales, or Inventory workflows;
- Active/Inactive lifecycle state;
- optimistic concurrency/version evidence.

Tax classification remains a preserved BRD boundary, not a new Tax model or
Tax behavior in this slice. The Product implementation must not invent Tax
master data, tax treatment, or Finance approval semantics. If a later approved
stable Tax reference is needed by a Product consumer, that contract must be
explicitly supplied by the owning Tax/Finance work; it is not silently added
by M95-SL-03.

### Ownership and business scope

Every Product belongs to exactly one Tenant. Product catalogue reuse is
Tenant-wide: Companies and Branches in the owning Tenant can reference the
same Product master fact. This is business availability inside the Tenant,
not permission bypass. A request still requires an authenticated server-
derived Tenant context, the applicable Product capability, and exact resource
authorization.

The Product implementation must not accept a client TenantId, CompanyId,
BranchId, or scope object as authority. Optional client hints may be used only
as validated hints and may never broaden, replace, or select a different
Tenant. No Product record, duplicate check, list, search, import, audit query,
or error response may reveal another Tenant's Product or identifier.

Warehouse/location stock availability is not a Product scope. Inventory owns
that later concern. Product's own scope policy represents the approved
Tenant-wide Product catalogue rule and must be independently named, versioned,
tested, and owned by Product; the Category/UOM policy is not a general
fallback.

## SKU and Barcode boundary

### SKU

- Every Product has exactly one non-empty SKU.
- The uniqueness key is `(TenantId, SKU comparison key)`; two Tenants may use
  the same visible SKU, while two Products in one Tenant may not.
- A manually entered or imported SKU and a server-generated SKU are equivalent
  for uniqueness, authorization, audit, and downstream identity.
- A Tenant may configure server-side generation, but generation is optional
  and must not be hard-coded to Wafra or to a business-meaningful prefix.
- The core Product contract must not require a semantic SKU format. Length,
  control-character, and empty-value validation remains a normal safe input
  boundary; country/integration-specific format checks belong to configuration
  or an approved integration.
- A generated value must use the same Tenant uniqueness invariant as a manual
  value and must fail safely on collision rather than silently selecting a
  different Product identity.

The Product implementation must make its canonical comparison key explicit
and test it at the application/persistence boundary. It must not rely on an
unreviewed database collation or use a Wafra-derived normalization shortcut.
The key's technical normalization is not a new business decision and must be
kept consistent across create, edit, import, generation, lookup, and indexes.

### Barcode

- Barcode is optional; a Product may have no barcode or multiple barcodes.
- Each barcode value is unique inside the owning Tenant, independent of which
  Product owns it.
- A barcode cannot be attached to two Products in one Tenant. Repeating the
  same value on one Product is also rejected as a duplicate child identity.
- Barcode values are alternate identifiers and do not replace the required
  SKU.
- Core SaaS does not require EAN, GS1, checksum, length, or country-specific
  format semantics. A future configuration/integration may validate such a
  format at its owning boundary.
- Barcode add/remove or replacement is a Product identity mutation and must
  use the same Product Tenant authorization, concurrency, duplicate, audit,
  and historical-reference controls.

## Lifecycle and integrity

The Product lifecycle is `Active` and `Inactive` only:

1. An authorized valid create produces an `Active` Product. There is no Draft
   state and no hidden review state.
2. Edit is permission- and concurrency-controlled. It cannot change Product
   identity in a way that creates a duplicate SKU or barcode.
3. Deactivate is allowed for an authorized Product and records a reason and
   audit evidence. The Product becomes unavailable for new business use where
   the downstream domain checks Product availability.
4. Reactivate is the lifecycle counterpart of Activate and uses the existing
   server-owned operation-to-capability mapping. It is not an approval step;
   it remains subject to permission, Tenant authorization, concurrency, and
   integrity checks.
5. Product deletion is not a Product operation. Once a Product is referenced
   by a business document or downstream record, its historical identity and
   references remain valid; deactivation is the lifecycle control.

The Product service must not decide how Procurement, Sales, Inventory, or
Finance use an inactive record. Those owners enforce their own new-use guards
and preserve their authoritative snapshots. Product returns stable identity,
current lifecycle, and audit facts through an authorized contract.

## Category and Base UOM relationship

Product creation requires one active Category and one active Base UOM. The
referenced records must belong to the same Tenant as the Product and must be
eligible for new Product assignment at the time of the operation. A foreign-
Tenant, missing, or inactive Category/UOM reference fails closed and must not
become a partial Product.

Category/UOM SL-02 is already a data-bearing, module-owned foundation. Product
may consume its stable reference contract and ownership checks through an
approved Master Data application/persistence port. It must not create a second
Category/UOM model, bypass the existing Tenant ownership verifier, or assume
that `CategoryUomScopePolicy` authorizes Product records.

The Base UOM relationship is a Product reference, not Inventory stock
behavior. Inventory owns the rule that a Base UOM cannot change after stock
transactions exist. Product implementation must use an explicit downstream
policy/contract for that guard and fail closed when a required integrity
decision is unavailable; it must not query or mutate Inventory tables directly.

## Tracking configuration boundary

Product carries only the Product-side tracking configuration:

- tracking is disabled by default when no Category default or Product override
  enables it;
- Category may provide a default;
- Product may explicitly override the Category default;
- an explicit Product override is distinguishable from an absent override;
- no Product table or contract creates batches, lots, serial numbers, expiry
  stock, stock balances, movements, valuation, receiving, issuing, or
  traceability records;
- Inventory alone decides and enforces operational tracking at the relevant
  stock transaction boundary after its own approved scope is ready.

The Product task must not silently choose the operational meaning of batch,
lot, serial, or expiry, and must not make an enabled flag a substitute for an
Inventory transaction policy. A Product read contract may expose the
configuration to an authorized Inventory consumer; it must not expose an
Inventory command path.

## Localization requirements

Product business-facing names use the existing localized-value contract:

- English name is required by the Product BRD.
- Arabic name is required where the Tenant's approved bilingual usability
  policy requires it; the Product service must not impose Arabic on a Tenant
  without that requirement.
- Values are Unicode-safe, preserve the submitted business meaning, and are
  carried with explicit locale/value semantics rather than hidden UI-only
  strings.
- SKU, barcode, stable identifiers, and technical error codes are not
  translated.
- Tenant isolation applies to localized values and all duplicate/search
  operations.
- ADR-011 remains a pre-code dependency for Arabic normalization, collation,
  tokenization, diacritic behavior, localized search ranking, RTL layout, and
  bilingual business-document generation. M95-SL-03 does not select those
  semantics and does not implement Angular forms or document rendering.

The next implementation session may preserve localized Product values in
contracts and persistence only within the approved boundary. It must not claim
ADR-011 completion or implement a search/indexing strategy without that ADR.

## Authorization and capability implications

Product authorization follows the approved Foundation and ADR-005 pattern:

1. The server builds the request context and derives the authoritative Tenant
   and authorization path.
2. The Product operation is passed to the immutable server-owned
   `MasterDataOperationCatalog`; the caller cannot supply an unrelated
   capability.
3. A Product-owned resource policy checks that the resource is Product and
   that the Product resource policy is configured. Unknown operation,
   resource, policy, or scope combinations fail closed.
4. A Product-owned scope policy evaluates the approved Tenant-wide Product
   business scope. It must not reuse or broaden `CategoryUomScopePolicy`.
5. The Product approval policy returns no separate approval for routine
   Create/Edit/Activate/Deactivate/Reactivate. It must delegate any future
   configured sensitive policy to the generic approval architecture rather
   than treating no approval as no permission.
6. Every protected query and mutation repeats the server-side checks. UI
   visibility, request payload fields, or a client-supplied scope hint is
   never a security boundary.

The Product implementation may reuse the shared operation/capability catalog
and generic audit contracts. It must have Product-owned policy identifiers,
policy versions, resource checks, test fixtures, and registration evidence.
It must not use Category/UOM policy classes as a shortcut merely because both
slices are Tenant-wide.

The capability profile for the next implementation session is:

| Product operation | Server capability/condition |
|---|---|
| View/list/get | `View` plus Product resource and Tenant policy |
| Create | `Create` plus Product resource and Tenant policy |
| Edit, including barcode/tracking metadata changes | `Edit` plus Product resource, Tenant policy, and concurrency |
| Activate/Reactivate | `Activate` plus lifecycle/integrity checks |
| Deactivate | `Deactivate` plus reason and lifecycle/integrity checks |
| View audit history | `ViewAuditHistory` plus Product resource and Tenant policy |
| Import/manual batch | `ImportMigrate` only if a later approved Product import contract is included; duplicate/quarantine/sign-off rules remain mandatory |
| Separate approval | Not applicable to the six routine Product lifecycle operations in this slice; future configured policies fail closed until explicitly registered |

## Audit and optimistic concurrency

Product mutations must append auditable evidence before the business effect is
committed, within the owning transaction boundary. Evidence must preserve:

- immutable evidence identity and stable Product record identity;
- owning Tenant and Product business identity/SKU;
- actor identity and authenticated session identity;
- the exact server-derived authorization path and Product business scope;
- operation/action, resource kind, and policy outcome;
- before and after summaries, including barcode/tracking changes where
  applicable;
- approver identity when a future configured policy makes one applicable;
- correlation/operation identity;
- timestamp;
- reason and the safe result/decision code.

Routine Product operations have no separate approver, so their evidence must
record the approval disposition as not applicable rather than fabricating an
approver. If a future policy requires approval, missing approval, self-
approval, or ambiguous policy configuration fails closed. Audit persistence or
evidence construction failure must not produce an apparent successful Product
mutation.

Every mutable Product aggregate and barcode mutation uses an optimistic
concurrency token/version. A stale edit, lifecycle request, or barcode/tracking
change returns a safe conflict, writes no partial state, and does not append a
false successful-effect audit event. The current Product version is returned
through the authorized contract; clients cannot choose or replace the Tenant
owner.

## Duplicate detection

Duplicate control is an identity invariant, not a UI suggestion:

| Candidate | Product rule | Required outcome |
|---|---|---|
| SKU within one Tenant | One comparison key may belong to one Product only | Reject/hold the create, edit, import, or generated collision; never create a second Product identity |
| Same SKU in different Tenants | Tenant isolation makes the keys distinct | Permit independently; never show Tenant B while checking Tenant A |
| Barcode within one Tenant | One comparison key may belong to one Product only | Reject/hold the duplicate, including repeated child value on one Product |
| Multiple distinct barcodes on one Product | Allowed by MD-OD-003 | Persist as separate alternate identifiers in the Product-owned boundary and audit each change |
| Display-name match | BRD duplicate review applies; no global name-identity rule is invented here | Surface a deterministic diagnostic/review result without blocking a valid unique SKU unless a later approved rule says otherwise |
| Manual/imported versus generated SKU | Same Tenant uniqueness invariant | Use one atomic duplicate check and collision-safe generation; no source-specific exception |

Duplicate detection is always Tenant-bound, authorization-protected, and
non-leaking. Import and future generation are not allowed to bypass the
Product unique-key invariant.

## Persistence ownership and future schema expectations

The approved production topology is four projects:

| Project | Product implication |
|---|---|
| `MiniErp.Contracts` | Product public request/response/reference contracts only; no EF Core, SQL provider, repository, or Infrastructure dependency. |
| `MiniErp.App` | Product application behavior, policy orchestration, validation, and contracts; EF-free and Infrastructure-free. |
| `MiniErp.Infrastructure` | Product-owned EF model, mappings, repository/port implementation, schema/migration, Tenant filters/ownership verification, concurrency, and audit transaction adapter inside the shared Infrastructure project. |
| `MiniErp.Api` | Composition root and REST transport; registers the Product module through approved seams; does not own Product persistence or business rules. |

Product persistence remains module-owned inside Infrastructure under the
Master Data boundary defined by ADR-002/ADR-006. It must not create a fifth
project, a microservice, a separate database, a direct cross-module DbContext,
or a Product shortcut around the Category/UOM ownership verifier.

The future migration/schema review must cover, at minimum, without being
created in this readiness session:

- a Tenant-owned Product row with stable identifier, SKU comparison key,
  localized names, Category reference, Base UOM reference, Product tracking
  configuration, lifecycle, and concurrency/version columns;
- a Product-owned barcode child row with Product identity, owning Tenant,
  barcode comparison key, and audit/concurrency treatment appropriate to the
  chosen mutation boundary;
- Tenant-owned uniqueness for Product SKU and barcode values, including
  indexes/constraints that cannot be bypassed by ordinary application paths;
- same-Tenant relationship enforcement for Product→Category, Product→Base
  UOM, and Product→Barcode;
- active-reference and historical-reference safeguards without physical
  Product deletion;
- module-owned audit evidence and append-before-effect transaction behavior;
- query/filter and stored-owner verification consistent with ADR-006;
- a provider-reviewed migration plan and rollback/backup evidence before any
  production migration is considered.

The schema design must not add variant/product-family tables, batch/lot/serial/
expiry tables, stock balances, movement ledgers, Procurement/Sales/Finance
tables, or a Tax master as part of Product identity. Exact SQL types, collation,
provider behavior, migration naming, and production deployment remain future
implementation/review work under ADR-006 and the open production gates.

## API boundary for the future implementation session

The future Product API is a contract-first Master Data resource boundary. Its
concrete route names may follow the existing host conventions, but the
operation boundary is fixed:

| API intent | Required behavior |
|---|---|
| List/get Product | Tenant-scoped authorized read; inactive records may be visible for history/audit but are not silently offered as new-use choices. |
| Create Product | Validates active same-Tenant Category/Base UOM, SKU uniqueness, barcode uniqueness, localized-name requirements, tracking default/override, and creates Active with audit. |
| Edit Product | Uses Product resource policy, duplicate checks, downstream integrity guard where required, and optimistic concurrency. |
| Activate/Reactivate Product | Uses the server operation mapping and `Activate` capability; no separate approval in the approved routine policy; preserves audit and conflict behavior. |
| Deactivate Product | Uses the `Deactivate` capability, records reason/audit, and does not rewrite historical consumer references. |
| Manage Product barcodes/tracking metadata | Remains part of the Product-owned mutation boundary and uses the same Tenant, duplicate, concurrency, and audit controls. |
| View Product audit history | Requires `ViewAuditHistory`, Product resource policy, and Tenant authorization; response is safe and Tenant-scoped. |

Requests must not carry authoritative Tenant or business-scope values. A
server-derived correlation/session context is used for authorization and
audit. Responses use stable identifiers, version/concurrency tokens, safe
Problem Details/error codes, and no cross-Tenant existence leakage. No API
route for Inventory stock, tracking transactions, batch/lot/serial/expiry,
Procurement, Sales, Finance, Tax, or future Product variants is part of this
task.

## Downstream integration boundaries

| Consumer/owner | Product identity contract | Explicit boundary |
|---|---|---|
| Procurement | Reads stable Product identity, SKU, active state, Category/UOM facts, and authorized reference data. | Procurement owns requisitions, quotations, purchase orders, supplier behavior, and its own new-use guard; Product does not implement them. |
| Inventory | Reads stable Product identity, Base UOM, and Product tracking configuration after its own gate. | Inventory owns stock balances, movements, receiving/issuing, valuation, Base UOM immutability after stock, operational tracking, and traceability. |
| B2B Sales | Reads Product identity and active-use status for B2B documents. | Sales owns quotation/order/reservation/delivery/invoice behavior, price selection, credit control, and transaction snapshots. Retail POS remains excluded. |
| Price List | May reference Product by stable identity. | Price List scope, effective dates, precedence, prices, and approval are not resolved here. |
| Tax/Finance | May later reference a stable Product tax classification contract. | No Tax master, tax treatment, Finance posting, exchange rate, approval, or accounting behavior is implemented or decided here. |
| Audit/reporting | Reads authorized Product history and immutable evidence. | Reports are Tenant-scoped and read-only; retention, legal hold, purge, residency, and production observability remain MESP-48/MESP-50 gates. |

Downstream authoritative documents must snapshot the Product facts needed to
interpret history at their own business-authoritative point. Product
deactivation never rewrites those snapshots.

## Explicit exclusions

M95-SL-03 readiness and the next Product implementation session exclude:

- Product persistence or Product source behavior in this readiness session;
- Product/Item variants, product families, bundles, kits, or variant
  architecture;
- Inventory stock, batch/lot/serial/expiry records, traceability, valuation,
  transactions, or physical availability;
- Procurement, Sales, Finance, Tax, Supplier, Business Customer, Price List,
  Payment Term, Currency, or Exchange Rate business behavior;
- approval catalogue decisions for later Master Data domains;
- Retail POS and Wafra-specific core behavior;
- Arabic collation/search ranking, RTL UI implementation, bilingual document
  generation, or a claimed completion of ADR-011;
- production credentials, provider provisioning, production database access,
  destructive migration, purge, retention, residency, legal-hold, backup, or
  restoration decisions.

The BRD's requirements for later domains remain preserved even when the
Product implementation does not own them.

## Technical readiness review

The existing Category/UOM foundation is reusable for Product only under these
guardrails:

| Review item | Readiness result |
|---|---|
| Server-owned operation→capability mapping | **Pass.** `MasterDataOperationCatalog` is immutable and maps defined operations to capabilities; unknown/unmapped values fail closed. Product uses the mapping and does not accept a caller-supplied capability pair. |
| Product scope/resource policy | **Required and bounded.** Product must introduce its own production-owned scope, resource, and approval policy identifiers/versioning. `CategoryUomScopePolicy` and `CategoryUomResourcePolicy` are not Product policy. |
| Module-owned persistence | **Pass as architecture.** Product EF model, repository, mapping, schema, migration, Tenant ownership, and audit adapter belong to the Master Data module inside Infrastructure under ADR-002/ADR-006. |
| App EF boundary | **Pass.** `MiniErp.App` remains EF-free and Infrastructure-free; Product application behavior consumes ports/contracts. |
| Contracts boundary | **Pass.** `MiniErp.Contracts` remains Infrastructure/provider-free and owns only stable public Product/reference contracts. |
| API composition root | **Pass.** `MiniErp.Api` remains the host/composition root and registers Infrastructure/module composition; it does not own Product rules or tables. |
| Tenant isolation | **Pass as foundation, Product-specific tests required.** Tenant is server-derived and fail-closed; Product list, duplicate, reference, audit, and mutation paths must add positive/negative Tenant tests. |
| Cross-module persistence | **Pass as guardrail.** Product must not reach Inventory/Procurement/Sales/Finance tables or another module's DbContext/repository; use approved contracts/ports and stable snapshots. |
| Audit fidelity | **Required.** Product evidence must preserve stable record identity, Tenant, actor, session, scope, action, before/after, policy outcome, approver when applicable, correlation, timestamp, and reason. |
| Localization | **Bounded.** Use localized value contracts and BRD requiredness; ADR-011 gates search/forms/RTL/documents. No collation or normalization decision is invented here. |
| Inventory tracking | **Explicitly excluded.** Product carries configuration only; Inventory owns operational enforcement and traceability. |

No material security, Tenant-isolation, accounting, data-integrity, or
architecture conflict was found in the foundation review. The bounded
Product-specific policy, persistence, and downstream-integrity work was then
implemented under MESP-102; the readiness session itself remained
documentation-only.

## Acceptance and validation traceability

The following checks were the minimum acceptance catalogue for the separately
activated MESP-102 Product implementation. The readiness session itself did not
run or implement them; their results are recorded below and in Jira comments
`10676` and `10677`.

| ID | Acceptance/validation evidence | Trace |
|---|---|---|
| PROD-VAL-001 | Authorized valid Product with active same-Tenant Category and Base UOM is created Active; no Draft path exists; create audit contains full identity and policy evidence. | MD-OD-001/005/008; MD-BR-001/006/010/011; MD-VR-003; MD-AC-001 |
| PROD-VAL-002 | SKU is required and unique within a Tenant; manual, imported, and configured-generated values share one collision-safe invariant. | MD-OD-003; MD-BR-005/010; MD-AC-002/028 |
| PROD-VAL-003 | Same visible SKU in two different Tenants is independent and never leaked during duplicate checks or search. | MD-OD-001/003; MD-BR-001/005; MD-AC-028 |
| PROD-VAL-004 | Product accepts zero, one, or multiple distinct barcodes; a duplicate barcode in one Tenant is rejected, while the same value in another Tenant is independent. | MD-OD-003; MD-BR-005; glossary Barcode |
| PROD-VAL-005 | Core Product accepts no required EAN/GS1 format and does not contain a Wafra-specific SKU/barcode rule; integration/configuration validation remains an explicit seam. | MD-OD-003/008; MD-BR-008 |
| PROD-VAL-006 | Product references only active same-Tenant Category and Base UOM; missing, inactive, foreign-Tenant, or invalid references fail closed without a partial Product. | MD-OD-001; MD-BR-011/017/019; MD-VR-003/010; MD-AC-005/006 |
| PROD-VAL-007 | Product tracking is disabled by default, accepts a Category default, and allows an explicit Product override; no stock, batch, lot, serial, expiry, or traceability behavior is executed. | MD-OD-010; MESP-33 boundary; hard exclusion |
| PROD-VAL-008 | Create, edit, activate, deactivate, and reactivate use Product-owned resource/scope/approval policies and the server operation catalog; caller-supplied mismatched capabilities, unknown operations, and cross-Tenant resources fail closed. | MD-OD-001/005/008; ADR-005; Foundation authorization contracts |
| PROD-VAL-009 | Deactivation and reactivation use Active/Inactive only, preserve historical references, record reason/audit, and never expose a delete path for a referenced Product. | MD-OD-005/008; MD-BR-002/003/012/013; MD-AC-003/004/026 |
| PROD-VAL-010 | Stale Product, barcode, lifecycle, or tracking writes return a safe concurrency conflict, do not partially commit, and do not append a false success event. | MD-OD-005/008; MD-AC-032; ADR-006 |
| PROD-VAL-011 | Product audit evidence includes record identity, Tenant, actor, session, scope, action, before/after, policy outcome, approver when applicable, correlation, timestamp, and reason; audit failure prevents the effect. | MD-OD-005; MD-BR-006/046; MD-AC-032; Foundation audit contract |
| PROD-VAL-012 | English/Arabic Product names follow the BRD/Tenant requiredness boundary and remain Tenant-isolated; no ADR-011 search/collation/RTL/document behavior is claimed. | MD-BR-009; MD-AC-030; ADR-011 |
| PROD-VAL-013 | App contains no EF/provider dependency, Contracts contains no Infrastructure dependency, API remains composition root, and Product persistence stays module-owned in Infrastructure. | ADR-002/006; architecture tests |
| PROD-VAL-014 | Product endpoints expose stable identifiers/version/error contracts, derive Tenant authority on the server, and do not leak cross-Tenant existence through list, get, duplicate, or audit responses. | MD-BR-001/044; ADR-005; Foundation REST/Tenant rules |
| PROD-VAL-015 | Source and diff scans prove no Product implementation was added by the readiness session; no variant, Inventory, downstream, production-provider, or migration behavior is introduced outside the next task. | MESP-101 hard exclusions; session boundary |

## MESP-102 implementation evidence

MESP-102 implemented the bounded Product identity slice through PR #37, merged
to `main` at `202d59068caac5d1fac402794627e41d7f452456` from implementation head
`f984835b28fe6d29156246b45917b12f1933b75b`. The implementation preserves the
approved Product-only bounds: Product and Item are one identity; scope is
Tenant-wide and server-derived; SKU and zero-or-more barcodes are Tenant-unique
without EAN/GS1 semantics; routine lifecycle operations require permission,
exact server-derived authority, and audit without a separate approver; create
is Active with Deactivate/Reactivate; Product stores tracking configuration
only; and Product-owned policy, audit, concurrency, and reference integrity are
bounded to this slice.

| Evidence | Result |
|---|---|
| PROD-VAL-001/002/003/004/006/007/008/009/010/011/013/014 | **Passed** through the focused Product suite (8/8), non-SQL suite (602/602), and source/boundary scans. |
| PROD-VAL-005 | **Passed by source boundary:** no EAN/GS1 or Wafra-specific Product coding semantics were introduced. |
| PROD-VAL-012 | **Passed:** English name is required, Arabic name is optional, and no ADR-011 search/collation/RTL/document behavior was introduced. |
| PROD-VAL-015 | **Passed for the readiness boundary:** MESP-101 itself remained documentation-only; MESP-102 introduced only the approved Product slice. |
| SQL/provider/migration gate | **Not claimed:** release build passed with 0 warnings/0 errors, but the 21-test SQL Server safety gate remains blocked by missing `MESP_SQLSERVER_CONNECTION_STRING`; no migration was executed and no production readiness claim is made. |
| Audit failure safety | **Passed:** missing audit persistence returns `audit_unavailable` and leaves no Product effect. |

## Historical next exact implementation boundary (fulfilled by MESP-102)

When MESP-101 was closed and Hossam/ChatGPT reviewed the merged readiness
evidence, the fresh MESP-102 session executed only:

- Product public contracts;
- Product application behavior and Product-owned authorization/scope/resource/
  approval policies;
- Product entity/model and module-owned Infrastructure persistence;
- Product schema/migration and repository within ADR-006/provider gates;
- Product endpoints and safe API contracts;
- SKU/barcode rules and collision-safe optional generation boundary;
- Product→Category and Product→Base UOM references;
- Product tracking configuration metadata only;
- Active/Inactive lifecycle, Tenant authorization, audit, concurrency, and
  focused Product tests listed above.

That session did not start a different Master Data slice or broaden the
Product boundary. No Product source behavior was started by the MESP-101
readiness session.

## Next session boundary

The next fresh root `TASK.md` session is **M95-SL-04 Supplier readiness and
decision gate only**. It must create or revalidate its own dedicated Jira item
and may produce only Supplier readiness/specification/decision-gate evidence.
It must not implement Supplier source behavior, entities, tables, migrations,
API, UI, or business workflow in this Product session.
