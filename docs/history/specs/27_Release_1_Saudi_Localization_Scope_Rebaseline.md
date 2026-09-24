# Release 1 Saudi Localization Scope Rebaseline

| Field | Value |
|---|---|
| Document type | Product Decision / Release 1 scope overlay |
| Status | Owner-approved Release 1 product-scope baseline; documentation and governance only |
| Approval date | 11 August 2026 |
| Accountable Owner | Hossam |
| Parent Jira task | MESP-112 - Rebaseline Release 1 Saudi localization and compliance scope |
| Product Decision | PD-023, appended to the immutable MESP-22 Product Decision Register |
| Historical baseline | Approved PRD v1.2 and the approved domain BRDs remain preserved historical evidence |

## 1. Purpose and authority

This artifact records the Owner-approved rebaseline for the Saudi scope of
Release 1. It is the canonical repository overlay for the current product
position. It narrows and classifies Release 1 capability; it does not rewrite
the approved PRD, silently delete prior requirements, or decide what any law,
regulation, taxpayer, company, or operating business requires.

This is a documentation, Jira, Product Decision, and governance artifact. It
does not authorize application source implementation, production enablement,
external validation to be skipped, or a later task to start automatically.

## 2. Approval date and historical-baseline rule

The Product Owner approved this Release 1 scope decision on 11 August 2026.
The chain of authority is:

1. the approved PRD v1.2, including its original Saudi launch wording,
   BR-002, KSA-001 through KSA-008, and historical integration candidates;
2. the approved Procurement, Inventory, Finance, B2B Sales, and Reporting
   BRDs, which preserve domain ownership and open gates;
3. the Owner-approved Release 1 decision recorded in PD-023 and this overlay;
4. the current Release 1 disposition and future re-entry gates below.

The original approved requirements remain available for historical audit. The
overlay states whether each affected item is retained, narrowed, deferred,
superseded for Release 1, or reserved for a future release. It does not make
the old baseline appear to have contained the current narrower wording.

## 3. Owner-approved Release 1 decision

Release 1 is a **Saudi-localized Core ERP Release 1** / **Saudi localization
baseline** for a reusable multi-Tenant B2B ERP product.

Release 1 contains:

- no production external integrations;
- no Saudi tax or statutory-compliance functionality;
- no ZATCA or FATOORA implementation, certification, onboarding, clearance,
  reporting, submission, credentials, signing keys, taxpayer-wave logic, or
  statutory XML/QR/security behavior; and
- no dedicated legal or regulatory-compliance automation, including dedicated
  PDPL/DPO/controller-registration/TIA/SCC/BCR/certification workflows.

The excluded capabilities may be introduced only through a future separately
approved Saudi Compliance / Integration release with current evidence,
qualified review, design, testing, and production gates. This decision is a
product-scope decision only. It is not a legal conclusion, a certification
claim, or a conclusion about taxpayer applicability.

## 4. Release 1 in-scope capability matrix

| Capability | Release 1 disposition | Boundary and ownership |
|---|---|---|
| B2B ERP backbone | Retained | Purchasing, inventory, B2B sales, receivables, payables, cash, core accounting, reporting, and approved platform foundations remain within their governed domain scopes. |
| Arabic and English | Retained and mandatory | Both languages are product capabilities; exact acceptance evidence is a later localization BRD and implementation/test concern. |
| RTL | Retained and mandatory | RTL page structure, forms, tables, generic documents, and visual usability are future acceptance concerns; no search/collation rule is invented here. |
| Bilingual navigation/forms/reports | Retained where core ERP scope includes them | Labels, messages, reports, exports, and generic document presentation must preserve business meaning and source amounts. |
| SAR and Saudi locale defaults | Retained | SAR and Saudi-oriented locale defaults are configurable country-pack/default behavior where configured; they are not a universal rule for every Tenant or Company. |
| Timezone and locale | Retained as configuration | Asia/Riyadh is a Saudi-oriented default where configured. Exact timezone, date, number, and currency behavior belongs to the localization BRD and later acceptance. |
| Multi-currency | Retained architecturally and as governed Finance scope | Currency and exchange-rate policy remain subject to MESP-54 and later Finance decisions; this overlay does not implement or resolve exchange-rate sourcing. |
| Generic commercial documents | Retained where already in Sales/Finance scope | Generic B2B invoices, credit documents, debit documents, and accounting records may exist as normal ERP documents. They are not ZATCA/FATOORA statutory-certified documents. |
| Tenant isolation and authority | Retained | Server-derived Tenant ownership, authorization, permissions, separation of duties, auditability, and fail-closed boundaries remain mandatory. |
| Operational logging and audit history | Retained | Generic operational and immutable financial/audit evidence remains required; dedicated regulatory certification evidence is deferred. |
| Reusable country-pack architecture | Retained | Configuration and extension points remain reusable across Tenants without Wafra-specific forks or hard-coded customer behavior. |
| Integration extension points | Retained architecturally | The product may remain integration-capable, but Release 1 does not deliver or operate production external integrations. |

## 5. Release 1 excluded and deferred capability matrix

| Capability | Release 1 disposition | Future boundary |
|---|---|---|
| ZATCA/FATOORA external integration | Deferred / out of scope | Future separately approved Saudi Compliance / Integration release. |
| ZATCA clearance, reporting, onboarding, sandbox, credentials, signing keys, certification | Deferred / out of scope | Requires current official evidence, security design, qualified review, and explicit production enablement. |
| Taxpayer activation, Phase 2, wave, date, or obligation logic | Deferred / out of scope | Future per Company/Legal Entity explicit configuration and taxpayer-specific evidence; never country/name/revenue/Wafra/date inferred. |
| Electronic-invoice submission, statutory XML, QR, and statutory security | Deferred / out of scope | Future integration and statutory design only. |
| Automated statutory VAT treatment, Saudi tax engine, tax-return or report submission | Deferred / out of scope | Future tax/compliance release; generic configurable ERP tax structures are not removed solely by this overlay. |
| External government, regulator, and external tax integrations | Deferred / out of scope | Future separately governed integration scope. |
| Dedicated legal-compliance automation | Deferred / out of scope | Future release after qualified legal/business review. |
| PDPL-specific rights/legal-basis/regulator workflows, DPO, controller registration, TIA, SCC/BCR, and certification | Deferred / out of scope | Future legal/privacy product decision and implementation gate. |
| Saudi-specific commercial hosting or residency commitment | Not approved | Future tenant, platform, contractual, infrastructure, and legal decisions under MESP-50. |
| Production provider, region, backup, DR, retention, purge, and support policy | Open production/platform governance | MESP-50 remains open; no provider, region, or Saudi-only hosting assumption is selected here. |
| Wafra-specific behavior | Prohibited | Wafra remains validation-only; no customer fork or hard-coded customer rule is allowed. |

## 6. KSA-001 through KSA-008 disposition

| Requirement | Original baseline meaning | Release 1 disposition | Trace and boundary |
|---|---|---|---|
| KSA-001 | SAR, Saudi locale defaults, and multi-currency architecture | **Retain for R1** | SAR/default locale and configurable country-pack behavior remain; MESP-54 remains open for exchange-rate source and update policy. |
| KSA-002 | Saudi VAT/statutory tax behavior | **Defer statutory behavior beyond R1** | Do not implement a Saudi statutory VAT engine. Generic configurable tax structures, if part of reusable ERP design, are not removed; no Saudi statutory completeness claim is made. |
| KSA-003 | Saudi statutory invoice requirements | **Defer ZATCA-specific implementation beyond R1** | Generic commercial invoices remain allowed within Sales/Finance scope; they are not certified statutory documents. |
| KSA-004 | FATOORA readiness/integration | **Defer beyond R1** | Preserve only future architectural extensibility; no adapter, credentials, sandbox, submission, or live integration is delivered. |
| KSA-005 | Arabic, English, and RTL | **Retain as mandatory R1 capability** | OWN-05 and the future MESP-37 acceptance catalogue govern navigation, forms, tables, labels, messages, documents, reports, exports, mixed content, and usability. |
| KSA-006 | PDPL/privacy product compliance features | **Defer dedicated regulatory automation beyond R1** | Preserve generic security, authorization, audit, minimization-oriented design, Tenant isolation, and safe SaaS principles; MESP-50 remains open for production governance. |
| KSA-007 | Hosting, residency, and cross-border production decisions | **Keep open through MESP-50** | No Saudi-only hosting assumption, provider selection, or region commitment is made. |
| KSA-008 | Saudi tax/e-invoice statutory evidence and history | **Defer ZATCA-specific evidence implementation beyond R1** | Preserve generic immutable financial and audit history; do not create statutory evidence, certification, or compliance reporting. |

## 7. BR-002 disposition

The original BR-002 Saudi-ready wording is preserved as approved historical
evidence. Its Release 1 disposition is narrowed to:

> Release 1 provides a Saudi-localized B2B ERP baseline with Arabic/English,
> RTL, SAR/default Saudi configuration, and reusable country-pack capability.
> Saudi statutory tax, ZATCA/FATOORA integration, certification, and dedicated
> legal/privacy compliance automation are deferred to later separately
> governed releases.

BR-002 is therefore **narrowed for Release 1**, not deleted. The original
PRD wording about broader VAT/e-invoicing readiness remains traceable to this
Owner-approved overlay and the future re-entry gates.

## 8. MESP-49 disposition

MESP-49, **Confirm Saudi e-invoicing launch scope**, is **Done for the Release
1 scope decision only**. Its original question and options remain historical
Jira evidence. The current disposition is:

> Saudi VAT/statutory e-invoicing, ZATCA/FATOORA onboarding,
> clearance/reporting, external integration, credentials, certification,
> taxpayer activation, and statutory-compliance implementation are excluded
> from Release 1 and deferred to a future separately approved Saudi Compliance
> / Integration release.

The closure is not a legal conclusion, taxpayer-applicability conclusion, or
certification claim. No implementation exists. Generic B2B commercial
documents remain possible within their Sales/Finance scopes but must not be
represented as ZATCA-certified statutory documents. Future activation requires
a new or reopened bounded task, current official ZATCA evidence, and
qualified review at that time.

Jira closure/disposition evidence is comment 10843. MESP-49 was transitioned
to Done only after the explicit Release 1 disposition was recorded.

## 9. MESP-50 disposition

MESP-50, **Confirm tenant data residency and retention policy**, remains
**To Do / open**. Excluding dedicated Saudi legal/privacy product features
does not remove minimum production SaaS governance.

### Deferred product compliance features

- PDPL-specific rights and legal-basis workflow automation;
- regulator submission or regulatory-rights workflow automation presented as
  PDPL compliance;
- DPO or controller-registration workflow;
- SCC/BCR workflow;
- TIA automation; and
- legal/privacy certification.

### Remaining production/platform governance

MESP-50 still owns or gates evidence and decisions for hosting/provider and
primary-data location, backup location and lifecycle, disaster-recovery
location, support and administrator access, retention, deletion, Tenant
export, audit history, subprocessors, observability, operational access,
security, and Tenant isolation.

This overlay selects no provider or region, requires no Saudi-only hosting
model, and makes no Saudi legal conclusion. The operational policy remains
open until sufficient Product/Platform evidence exists.

Jira rebaseline evidence is comment 10844.

## 10. MESP-37 revised BRD boundary

MESP-37, **Produce Saudi Localization and Compliance BRD**, remains **To Do**.
It was not activated or executed by this session.

### Future MESP-37 Release 1 BRD in scope

- Arabic and English;
- RTL;
- Saudi locale behavior and configurable defaults;
- SAR/default configuration;
- timezone and locale configuration;
- reusable Saudi country-pack configuration;
- bilingual generic business documents;
- Saudi localization impacts across Procurement, Inventory, Finance, Sales,
  Reporting, and Platform;
- audit and configuration evidence;
- multi-Tenant configuration;
- localization acceptance scenarios; and
- clear future-extension boundaries.

### Explicitly out of scope or deferred for MESP-37

- ZATCA/FATOORA integration, clearance, reporting, onboarding, sandbox,
  credentials, signing keys, submission, XML/QR/security, or certification;
- statutory VAT or Saudi tax-engine implementation;
- statutory e-invoicing and regulator submission;
- taxpayer activation, phase, wave, or date logic;
- government integrations;
- legal-compliance automation;
- PDPL regulatory workflow automation;
- Saudi legal or tax certification;
- production infrastructure, provider, hosting, residency, retention, backup,
  DR, and recovery decisions; and
- Currency implementation and later tasks.

These deferred areas may be named only as future external-compliance or
integration boundaries. MESP-37's future BRD must not silently restore them as
Release 1 requirements.

The Jira boundary reconciliation is comment 10845. The exact next-session
prompt is in the root TASK.md.

## 11. OWN-01 through OWN-05 decisions

| ID | Owner-approved decision | Release 1 status |
|---|---|---|
| OWN-01 | No Saudi statutory/ZATCA e-invoice document implementation. Generic B2B invoices, credit documents, and normal accounting documents may remain in Sales/Finance scope but are not statutory-certified documents. | **Approved - statutory implementation deferred.** |
| OWN-02 | No R1 taxpayer activation because no ZATCA/FATOORA integration is enabled. Future activation is per Company/Legal Entity using explicit configuration and taxpayer-specific evidence; never inferred solely from Saudi country, Company name, Wafra identity, revenue, phase, wave, or date. | **Approved - future integration requirement preserved.** |
| OWN-03 | Use Saudi-localized, Arabic/English capable, RTL capable, SAR/default-locale configurable, or Saudi-oriented core ERP. Do not use ZATCA/FATOORA/Saudi statutory/tax/legal/certified claims. | **Approved.** |
| OWN-04 | No Saudi-specific commercial hosting or residency commitment. Architecture stays deployable according to future tenant, infrastructure, contractual, and regulatory decisions; hosting, backup, DR, support, retention, deletion, and subprocessors remain MESP-50/platform decisions. | **Partially approved boundary; operational policy open.** |
| OWN-05 | Arabic/English and RTL are required R1 capabilities. Later acceptance covers English/Arabic navigation, RTL pages/forms/tables, bilingual labels/messages, dates, numbers, currency, search, sort, filtering, generic documents, reports, export, fallback, mixed content, and visual usability. | **Approved R1 product requirement.** |

The Owner decisions are recorded in MESP-23 comment 10846 and the Product
Decision Register entry PD-023.

## 12. External-integration policy

Release 1 contains **no production external integrations**, governmental or
commercial, unless a separately reviewed and explicitly approved Release 1
requirement is demonstrated to contradict this Owner decision. No such
contradiction was identified for the Saudi scope in the approved PRD and
current decision register.

Existing PRD integration candidates are historical/planned architecture
evidence, not silently deleted requirements. They are classified as future
or deferred where they require an external provider or production exchange.
The core architecture remains integration-capable through bounded extension
points, but no connector, adapter, credential, webhook, broker, government
exchange, tax exchange, or commercial exchange is delivered or operated in
this Release 1 scope.

No customer-specific integration fork is permitted.

## 13. Compliance-claim wording policy

Approved Release 1 descriptions include:

- Saudi-localized Core ERP Release 1;
- Saudi localization baseline;
- Arabic/English capable;
- RTL capable;
- SAR/default-locale configurable;
- Saudi-oriented core ERP; and
- reusable Saudi country-pack configuration.

Release 1 must not be described as:

- ZATCA compliant, certified, or integrated;
- FATOORA compliant, certified, or integrated;
- Saudi tax compliant;
- Saudi statutory compliant;
- legally compliant or legally certified;
- PDPL certified;
- certified for Saudi production;
- fully Saudi compliant; or
- government-integrated.

This wording policy describes product scope. It does not assert that a law or
regulatory obligation does not apply to an organization using the product.

## 14. Future-release re-entry gates

Future Saudi tax, statutory, legal, privacy, or integration work requires all
applicable gates below:

1. a new or reopened bounded scope and Owner approval;
2. current official-source revalidation at the time of the future work;
3. qualified tax, legal, privacy, security, or other specialist review where
   appropriate;
4. explicit Company/Legal Entity and taxpayer configuration policy;
5. integration, credential, signing, threat, Tenant-isolation, and data-flow
   design;
6. sandbox, conformance, certification, and negative-path evidence where
   applicable;
7. implementation, test, audit, operational-support, and rollback evidence;
8. production provider, hosting, backup, DR, retention, observability, and
   access approval through the relevant platform gates; and
9. an explicit production enablement decision for the actual deployment
   context.

No future gate is satisfied by this artifact or by a generic Owner scope
decision alone.

## 15. Product Decision Register references

The Product Decision Register is the append-only Jira register MESP-22. The
next unused identifier after PD-022 is **PD-023**.

PD-023 records:

- the Saudi-localized Core ERP Release 1 decision;
- rationale focused on dependable B2B ERP and reusable multi-Tenant
  architecture;
- positive consequences of reduced scope and lower external dependency;
- trade-offs, including no statutory-compliance claim or replacement of
  required future ZATCA/FATOORA capability;
- future re-entry gates; and
- traceability to PRD BR-002, KSA-001 through KSA-008, MESP-23, MESP-37,
  MESP-49, MESP-50, MESP-111, and OWN-01 through OWN-05.

PD-023 is immutable. It does not edit earlier Product Decisions, resolve
unrelated MESP-23 rows, or authorize implementation. The Jira evidence is
comment 10849.

## 16. Jira evidence

| Issue | Verified Release 1 position | Evidence |
|---|---|---|
| MESP-112 | Done; PR #54 reviewed at 65dd650776b2c3abb06c36987b68152deb776958 and merged at 6e501d1f2a018c36b76339388ce7b7f09ed9c937 | Activation/closure comments 10848/10850; parent MESP-12 |
| MESP-49 | Done for R1 scope only; statutory/ZATCA/FATOORA scope deferred/out of scope | Disposition comment 10843 |
| MESP-50 | To Do / open; dedicated legal/privacy features deferred, production/platform governance remains | Rebaseline comment 10844 |
| MESP-37 | To Do; not activated; future BRD narrowed to localization/core ERP | Boundary comment 10845 |
| MESP-23 | In Progress; exact scope overlay reconciled without closing unrelated open decisions | Reconciliation comment 10846 |
| MESP-111 | Done; history preserved; R1 product-scope gate superseded by Owner deferral | Addendum comment 10847; historical activation/closure comments 10809/10810 |
| MESP-22 | Done; PD-023 appended to immutable Product Decision Register | Register comment 10849 |

MESP-48, MESP-53, MESP-54, and MESP-110 remain open and are not resolved by
this scope overlay.

## 17. MESP-23 traceability

The canonical trace is:

**Approved PRD BR-002 and KSA-001 through KSA-008 -> Owner-approved R1
scope decision -> PD-023 -> current R1 disposition in this artifact and
Jira comments 10843-10849.**

MESP-23 remains the living Open Questions Register and remains **In Progress**.
The overlay gives an exact Release 1 disposition for the Saudi scope and
MESP-49, but it does not close MESP-50, MESP-54, MESP-48, MESP-53, MESP-110,
or any unrelated question by inference. The original PRD's broader Saudi
requirements remain auditable historical inputs.

The affected approved domain BRDs preserve their own ownership boundaries:
Procurement, Inventory, Finance, B2B Sales, and Reporting continue to own
their generic ERP outcomes, while statutory, privacy, provider, and
production claims remain gated by the current decision register and platform
ADRs.

## 18. No-Wafra-hardcoding statement

Wafra is validation-only. No Release 1 Saudi behavior may be selected because
the Tenant, Company, customer name, revenue assumption, country field, phase,
wave, or date resembles Wafra. The reusable multi-Tenant product must not
contain Wafra-specific SKU, invoice, taxpayer, hosting, tax, integration,
legal, privacy, or workflow rules.

Future taxpayer-specific integration, if ever approved, must be explicit per
Company/Legal Entity and evidence-backed; it must never be enabled solely
from country, identity, name, revenue, phase, wave, or date.

## 19. Implementation exclusions and validation

This session added no application source, tests, entities, tables, EF models,
migrations, endpoints, API contracts, UI, provider configuration, production
configuration, credentials, external integration, ZATCA/FATOORA behavior,
tax implementation, privacy/legal workflow, or Wafra-specific core behavior.

The bounded validation is documentation/governance validation:

- required governance, PRD, BRD, ADR, Jira, and Git baseline evidence was
  read or structurally inspected;
- the complete task-related diff must remain Markdown/TASK/Jira/state only;
- git diff --check is required;
- changed files and the full base-to-final diff must be reviewed;
- Jira statuses and evidence must be rechecked live; and
- no full application build/test suite is claimed or required for this
  documentation-only task.

The PRD remains historical; no approved PRD text was silently rewritten.

## 20. Exact next-task readiness verdict

**READY FOR MESP-37 DRAFT ONLY - LOCALIZATION/CORE ERP BOUNDARY CONFIRMED;
NO STATUTORY OR EXTERNAL-COMPLIANCE IMPLEMENTATION.**

MESP-37 may be activated only by a fresh session after its current Jira entry,
this artifact, MESP-23, the approved PRD/BRDs/ADRs, and the synchronized
repository state are reverified. The next session may draft only the
localization/core ERP BRD boundary in section 10. It must not implement
software or start Currency, tax, integration, privacy, production,
infrastructure, or any later task.

The current session ends after the focused documentation PR is reviewed and
merged only if clean and unblocked. It does not execute MESP-37.
