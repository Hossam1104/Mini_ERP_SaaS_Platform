# Release 1 Saudi Localization and Core ERP Business Requirements Document

> **Version:** v0.1 - Approved bounded product-only baseline
> **Jira:** MESP-37 - Produce Saudi Localization and Compliance BRD
> **Parent:** MESP-12 - EPIC 12 - Saudi Localization and Compliance
> **BRD sequence:** Position 12 of 15; the next BRD after Reporting and Analytics
> **Date:** 11 August 2026
> **Scope:** Saudi-localized Core ERP Release 1; reusable multi-Tenant B2B ERP; Wafra validation-only
> **Status:** Approved product-only business baseline; documentation-only; no implementation authorization
> **Entry evidence:** MESP-112 Done / PD-023; MESP-111 Done with “READY FOR MESP-37 DRAFT ONLY - EXTERNAL VALIDATION OUTSTANDING”; MESP-37 activated in Jira comment 10854
> **Approval evidence:** Jira comment 10855 (validation), Jira comment 10857 (Owner approval), MESP-22 traceability comment 10856, and MESP-23 handoff comment 10858; focused PR #55 reviewed at final content head `ff8eb5901d68a2cc366ed61722c08a7be53f50a1`
> **Closure evidence:** Jira comment 10859; PR #55 merged to `main` at `7d03fa5b19226b8c6368012ec90c8a09eefd4aaf`
> **Canonical scope overlay:** `docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md`

## 1. Document control and reading rules

This document is the bounded Release 1 business-requirements baseline for
localization and configurable Saudi-oriented country-pack behavior. It
defines the business outcome, ownership boundaries, configuration facts,
failure/fallback expectations, and acceptance scenarios for Arabic, English,
RTL, bilingual generic ERP presentation, locale/timezone/currency display,
Tenant-safe country-pack configuration, and the cross-module consequences of
those requirements.

The product position used by this document is **Saudi-localized Core ERP
Release 1** or **Saudi localization baseline**. It is a product-scope and
business-requirements description. It is not a legal, tax, privacy, statutory,
certification, production-readiness, or taxpayer-applicability conclusion.

This is a business document. It does not define database tables, entities,
migrations, API contracts, controllers, screens, framework behavior, provider
selection, deployment topology, production configuration, infrastructure,
credentials, external integration implementation, or automated tests. It
authorizes none of those activities. A later implementation-readiness item
must preserve the evidence and must not turn a conditional branch,
recommendation, or open decision into an implicit default.

The approved PRD, the current scope rebaseline, the regulatory-readiness
artifact, the glossary, the architecture/foundation baselines, and the
approved Procurement, Inventory, Finance, B2B Sales, and Reporting BRDs were
read before authoring this document. Their domain ownership boundaries remain
authoritative. This BRD carries localization consequences at those boundaries;
it does not take ownership of a source transaction or answer a source-domain
policy decision.

### 1.1 Classification legend

| Classification | Meaning in this BRD |
| --- | --- |
| **Confirmed baseline** | Directly supported by the approved PRD, approved glossary, approved scope overlay, approved domain BRD, ADR boundary, or named approved Jira decision. |
| **BRD requirement** | Business behavior required by this bounded baseline after Owner approval, subject to named gates and dependencies. |
| **Open decision / gate** | Not approved. The affected branch remains visible and cannot be implemented as an implicit default. |
| **Conditional branch** | A coherent business path whose detailed policy or implementation depends on a named owner, ADR, domain baseline, or gate. |
| **Recommendation only** | A proposal retained for later decision. It is not a requirement, acceptance criterion, or implementation instruction. |
| **External validation** | Qualified Saudi tax, legal, privacy, security, banking, or other specialist validation required for a future affected release or production claim. |
| **Out of scope** | Excluded from this BRD or deferred from Release 1 by the approved product-scope overlay. |

The Founder Decision Pack is not an approval catalogue. Its defaults are not
requirements unless a named approval record says otherwise. MESP-23 remains the
living register for unresolved questions. MESP-22 is the append-only Product
Decision Register; this BRD does not create a new product decision. PD-023 and
the MESP-112 scope overlay are the current product-scope authority for the
Saudi/localization boundary.

### 1.2 Entry and dependency position

The entry position was freshly verified before this session and recorded in
Jira. MESP-37 was then moved from To Do to In Progress as the single active
documentation item. The table is a status record, not an approval of an open
row.

| Item | Verified position at MESP-37 activation | Consequence for this BRD |
| --- | --- | --- |
| MESP-112 | Done; Owner-approved Release 1 scope rebaseline; PD-023 recorded | The current product boundary is the localization/core ERP slice below. |
| MESP-111 | Done; readiness artifact is complete with draft-only verdict and external validation outstanding | The source/evidence pack is available; no deferred statutory or legal position is imported into this BRD. |
| MESP-37 | In Progress for this bounded session | This BRD is the only active work item in this session. |
| MESP-23 | In Progress; living Open Questions Register | No open row is closed, answered, or silently superseded here. |
| MESP-49 | Done only for the Release 1 deferred-disposition scope | No Saudi statutory, e-invoicing, or tax answer is implied. |
| MESP-50 | To Do and open | Retention, privacy, legal hold, purge, residency, backup, and restoration remain production-governance gates. |
| MESP-48 | To Do and open | No supported-volume, freshness, capacity, or recovery number is invented. |
| MESP-53 | To Do and open | Reporting catalogue, ownership, and distribution decisions remain open. |
| MESP-54 | To Do and open | Currency implementation, exchange-rate sourcing, approval, and rounding remain outside this BRD. |
| MESP-110 | To Do and open | Finance year-end, Payment Term, due-date, aging, and posting-dimension policy remain outside this BRD. |
| ADR-011 | Required dependency; no full ADR is approved in the repository | Runtime localization, Arabic search/collation/tokenization, RTL details, and bilingual-document implementation decisions remain gated. |

## 2. Executive summary

Release 1 localization gives an authorized B2B ERP user a consistent Arabic or
English experience, including RTL page structure where Arabic is active,
bilingual generic business documents/reports/exports where the owning domain
supports them, and locale-aware presentation of dates, times, numbers, and
currency values. The same source fact, Tenant scope, authorization, status,
amount, quantity, identifier, and audit meaning must remain unchanged when its
presentation language or direction changes.

The Saudi-oriented country-pack baseline provides configurable defaults rather
than customer-specific code: Arabic and English are supported; RTL is
supported; SAR is the default currency configuration for a Saudi-oriented
profile; Asia/Riyadh is a configurable Saudi-oriented timezone default; and a
Tenant can activate an allowed country-pack configuration within its own
authority boundary. Multi-currency architecture is retained, but exchange-rate
source/update/approval, Reporting Currency, conversion, and rounding policy
remain governed by MESP-54 and are not decided here.

The baseline is intentionally reusable. Wafra may validate generic behavior,
but no Wafra-specific label, workflow, rule, default, fork, or hard-coded
customer behavior is created. Retail POS remains excluded. Future statutory
tax/e-invoicing, ZATCA/FATOORA, government, banking, legal, privacy-regulatory,
certification, and production-provider work remains separately gated future
scope.

## 3. Purpose and desired outcomes

The localization baseline must provide these business outcomes:

- Arabic and English language selection for authorized users without changing
  the underlying business fact or Tenant scope;
- readable RTL layout for Arabic pages, forms, tables, generic documents, and
  reports, with mixed Arabic/English content remaining legible;
- consistent bilingual labels, validation messages, status names, generic
  documents, reports, and exports where the owning core ERP domain exposes the
  artifact;
- configurable Tenant-safe locale, language, direction, timezone, and
  presentation defaults with a Saudi-oriented default profile;
- display of dates, times, numbers, and SAR values according to the effective
  configuration while preserving canonical source meaning;
- a reusable country-pack configuration seam that can serve more than one
  Tenant without copying customer-specific core logic;
- deterministic fallback and stable errors when a translation, locale, or
  country-pack value is missing, unsupported, expired, or not authorized;
- server-derived Tenant, Company, Branch, and user authority for every
  configuration and rendered result;
- immutable configuration and audit evidence for material localization
  changes and their effective periods; and
- a clear future-extension boundary so a later approved compliance or
  integration release can be evaluated without treating this baseline as a
  statutory or certified product.

## 4. Scope and boundaries

### 4.1 In scope for this BRD

- Arabic language support;
- English language support;
- RTL page structure, forms, tables, document presentation, and visual
  usability for Arabic and mixed-direction content;
- bilingual navigation, labels, validation messages, generic documents,
  reports, and exports where the artifact is part of the core ERP domain;
- configurable language, direction, locale, date, number, and timezone
  behavior;
- a Saudi-oriented locale profile with SAR/default currency configuration and
  Asia/Riyadh as a configurable timezone default;
- reusable country-pack configuration and version/effective-date behavior;
- Tenant-safe configuration and server-derived authority;
- localization of cross-module labels, statuses, validation, generic
  document/report presentation, and export metadata across Platform,
  Procurement, Inventory, Finance, B2B Sales, Reporting, Audit, and generic
  ERP documents;
- date, time, number, currency presentation, filtering, sorting, search, and
  mixed Arabic/English content as acceptance concerns, subject to ADR-011 and
  owning-domain decisions;
- fallback, error, unsupported-locale, and unavailable-translation behavior;
- audit/configuration evidence and business-level visual acceptance; and
- future extension boundaries for separately approved external compliance or
  integration work.

### 4.2 Explicitly out of scope or deferred

The following items are not specified, implemented, selected, or certified by
this BRD:

- ZATCA or FATOORA integration, clearance, reporting, onboarding, sandbox,
  credentials, signing keys, submission, XML, QR, security, certification, or
  taxpayer activation;
- statutory VAT automation, a Saudi tax engine, statutory tax rates, returns,
  statutory invoice content, or taxpayer-specific applicability;
- statutory e-invoicing or regulator submission;
- taxpayer phase, wave, obligation, or effective-date logic;
- government, regulator, banking, commercial, or other production external
  integrations;
- legal-compliance automation or a legal conclusion;
- PDPL-specific rights/legal-basis workflow, DPO workflow, controller
  registration, transfer-impact assessment, SCC/BCR, regulator submission, or
  privacy/legal certification;
- Saudi tax, statutory, legal, privacy, or compliance certification;
- provider, hosting, primary-data location, backup, disaster recovery,
  support geography, retention, deletion, purge, subprocessors, or production
  infrastructure decisions;
- Currency implementation, Reporting Currency, exchange-rate source/update/
  approval, conversion, or rounding policy;
- Finance, Integration, Tax, Privacy, Infrastructure, Production, Master Data,
  or other later task execution;
- Product/Item, SKU/Barcode, Category/UOM, or any other Master Data source
  implementation;
- Retail POS, consumer checkout, cashier, restaurant, cash-drawer, or shift
  behavior; and
- Wafra-specific core behavior, customer forks, or hard-coded customer rules.

An excluded item may be named as a future extension boundary only. Exclusion
does not infer legal compliance, non-compliance, taxpayer applicability, or a
production hosting position.

### 4.3 Scope interpretation rules

1. **Presentation does not change source truth.** A language, direction, locale,
   or formatting choice changes presentation only. It cannot change a posted
   amount, quantity, date fact, status, identifier, tax source fact, stock
   fact, authorization, or audit history.
2. **Configuration is not customer code.** A Tenant preference may choose an
   allowed locale or presentation profile. It cannot add a customer-specific
   workflow, bypass domain ownership, or change a product decision.
3. **Generic is not statutory.** A generic invoice-like, receipt-like, report,
   or export template may be bilingual when owned by a core ERP domain. It
   must not be described as a statutory invoice, certified tax output, or
   regulator submission.
4. **Conditional behavior stays conditional.** Arabic search/collation and
   runtime localization implementation remain dependent on ADR-011. Finance,
   Reporting, privacy, retention, and volume behavior remains dependent on its
   named owner or gate.
5. **Tenant boundaries are mandatory.** A user-selected language or browser
   setting cannot change the server-derived Tenant, Company, Branch, Warehouse,
   permission, or support scope.

## 5. Source baseline and traceability

### 5.1 Authority order

When a wording conflict occurs, use this order:

1. the current Owner-approved Release 1 scope overlay and Product Decision
   Register entry PD-023;
2. the approved PRD v1.2 and its historical traceability anchors;
3. the approved glossary, architecture/foundation baselines, and applicable
   ADR boundaries;
4. the approved domain BRDs and their named ownership boundaries;
5. the MESP-23 living register and the owning open-decision Jira item; and
6. a recommendation or example, which never overrides an approved baseline.

The historical PRD is not rewritten by this document. Where the PRD contains
broader statutory or compliance language, the current MESP-112/PD-023 product
scope overlay is the controlling Release 1 boundary for this BRD.

### 5.2 Primary anchors

| Anchor | Requirement or boundary carried into this BRD |
| --- | --- |
| PRD D-002 / KSA launch baseline | Saudi Arabia is the initial launch market; this BRD describes a Saudi-oriented product configuration, not a legal conclusion. |
| PRD D-006 / KSA-005 | Arabic and English are supported; RTL is a Release 1 localization requirement. |
| PRD D-007 / KSA-001 | SAR is the default base-currency configuration for Saudi-oriented tenants while multi-currency architecture remains retained. |
| PRD D-010 / PLT-011 to PLT-014 | Country-pack configuration is reusable and must not become Tenant-specific core code. |
| PRD PLT-001 / PLT-002 | Tenant hierarchy and server-derived organizational scope remain mandatory. |
| PRD PLT-008 / BR-010 | Material configuration and access effects require immutable audit evidence. |
| PRD PLT-009 / RPT-002 | Search, filtering, sorting, and generic exports remain authorized and bounded; implementation detail is deferred. |
| PRD BR-002 / KSA-001 to KSA-008 | Saudi/localization traceability is preserved, with statutory and production gates narrowed or deferred by PD-023. |
| MESP-112 / PD-023 | Release 1 is Saudi-localized Core ERP: Arabic/English/RTL, generic bilingual presentation, configurable Saudi defaults, reusable country-pack, and no statutory/external/production compliance functionality. |
| MESP-111 / KSA-001 to KSA-008, OWN-01 to OWN-05 | Official-source and question-pack evidence remains historical readiness evidence; external validation is still required for future deferred areas. |
| ADR-011 dependency | Runtime localization, Arabic search/collation/tokenization, RTL implementation details, and bilingual document implementation require a separate approved decision/ADR. |

### 5.3 Supporting baselines

| Document | Consequence |
| --- | --- |
| `docs/00_ERP_Business_Glossary.md` | Tenant, Company, Branch, Warehouse, Country Pack, permission, server authority, immutable record, audit event, B2B, and banned Retail POS vocabulary are mandatory. |
| `docs/01_Technology_Architecture_Baseline.md` | Arabic/English, RTL logical layout, locale-aware display, UTC storage, Saudi defaults, Tenant isolation, and four-project topology constrain future work. |
| `docs/15_Foundation_Release_1_Lean_Implementation_Specification.md` | Foundation supports Arabic/English/RTL and configurable date/time/number/currency presentation; it does not authorize source implementation or localized search before ADR-011. |
| `docs/21_Procurement_and_Purchase_to_Pay_BRD.md` | Procurement owns the commercial P2P chain; generic bilingual documents remain subject to Procurement ownership and Finance/Inventory handoffs. |
| `docs/22_Inventory_and_Warehouse_Management_BRD.md` | Inventory owns physical stock facts and Warehouse context; localization cannot change quantity, UOM, movement, valuation, or tracking meaning. |
| `docs/23_Finance_and_Accounting_BRD.md` | Finance owns accounting, tax, currency, rate, and posting semantics; this BRD changes display requirements only and does not resolve MESP-54 or MESP-110. |
| `docs/24_Sales_and_Order_to_Cash_BRD.md` | B2B Sales owns commercial documents; localization cannot add Retail POS or change the Sales/Inventory/Finance boundary. |
| `docs/25_Reporting_and_Analytics_BRD.md` | Reporting is read-only and owns report presentation/lineage; MESP-53 remains open for the final catalogue and distribution policy. |
| ADR-002, ADR-004, ADR-006, ADR-009, ADR-018 | Future implementation must preserve four-project ownership, secure/server-derived context, shared database module ownership, private artifacts, Tenant isolation, and non-production provider gates. |

### 5.4 Trace convention

Requirement identifiers use the `KSA-L10` through `KSA-X` families below. A
requirement is a business requirement only when this bounded baseline is
approved at its exact scope. `PD-023`, `MESP-112`, `MESP-111`, `MESP-23`, and
the named gates remain traceable evidence; they are not silently rewritten by
the identifier scheme.

## 6. Actors, responsibilities, and authority

### 6.1 Actors

| Actor | Responsibility in this BRD | Authority boundary |
| --- | --- | --- |
| Product Owner | Approves the bounded product requirement and any product decision that changes the baseline. | Cannot substitute a tenant-specific rule for a reusable product requirement or approve a statutory/legal conclusion without the required specialist evidence. |
| Platform/Country-Pack owner | Maintains the reusable language, locale, direction, presentation-profile, and country-pack vocabulary and evidence. | Cannot own Procurement, Inventory, Finance, Sales, Reporting, tax, privacy, or production-provider policy. |
| Tenant administrator | Chooses an allowed Tenant configuration and effective presentation defaults within the Tenant's authority. | Cannot view or alter another Tenant's configuration, source facts, audit history, or country-pack definition. |
| Company/Branch administrator | Chooses an allowed subordinate presentation preference where the owning domain permits it. | Cannot widen Tenant scope, change source semantics, or bypass Company/Branch authorization. |
| Authorized ERP user | Selects Arabic or English and consumes authorized generic documents, reports, exports, and validation messages. | A language choice affects presentation only; it is not an authorization or data-scope control. |
| Domain owner | Owns the source meaning, terminology, status, and generic artifact for its domain. | Cannot delegate source ownership to the localization layer. |
| Audit/Platform control owner | Owns configuration-change evidence, access evidence, and immutable audit semantics. | Cannot delete or rewrite an audit event to correct a translation or configuration mistake. |
| Qualified future specialist | Validates a future tax, legal, privacy, banking, or compliance extension where required. | No specialist validation is implied by this product-only BRD. |
| Wafra | Provides validation examples for generic B2B ERP behavior. | Wafra is not a product owner for core rules and cannot create a Wafra-specific branch. |

### 6.2 Authority invariants

- The server derives Tenant, Company, Branch, Warehouse, membership,
  permission, support grant, and effective configuration scope. A browser
  language header, user-entered Tenant identifier, or client-side direction
  flag is never an authority source.
- A user can choose a presentation language only within the language choices
  allowed by the server-side configuration and authorization context.
- A configuration change requires the relevant permission, an effective date
  or immediate-change semantics owned by the configuration policy, and an
  audit record. This BRD does not define a separate approver catalogue.
- A translated label, user-entered Arabic/English name, or mixed-direction
  value is data/presentation content, not permission or workflow authority.
- Support access, if later approved by platform governance, remains time-bound,
  least-privilege, server-issued, and audited under the foundation/ADR
  boundaries.

## 7. Core localization vocabulary

| Term | Required meaning |
| --- | --- |
| **Language** | The selected presentation language, initially Arabic or English. It does not change source data scope. |
| **Direction** | The visual writing direction used by a page, form, table, document, or report. Arabic presentation uses RTL where applicable; English uses LTR. |
| **Locale** | A configurable presentation profile covering language, direction, date/time, number, and related formatting facts. It is not a legal or tax classification. |
| **Saudi-oriented default** | A configurable default profile using SAR and Asia/Riyadh where the owning Tenant/Company chooses the Saudi-oriented country-pack baseline. It is not universal and does not decide exchange rates or statutory treatment. |
| **Country Pack** | A reusable, versioned configuration boundary for country-oriented language, locale, presentation, document, and future-gated rules. In this BRD, only the localization/configuration slice is active. |
| **Bilingual artifact** | A generic core ERP document, report, validation message, or export that presents Arabic and English labels/content according to the owning domain's approved mode. It is not a statutory or certified output. |
| **Source fact** | The domain-owned amount, quantity, date, status, identifier, text, or event meaning that localization must not mutate. |
| **Fallback** | The defined presentation result when a requested language, translation, locale, or pack value is missing or unavailable. Fallback must be visible and must not silently change source meaning. |
| **Mixed-direction content** | Arabic and English text, numerals, punctuation, identifiers, or codes appearing together. It must remain readable and preserve identifiers. |
| **Tenant-safe** | Configuration and rendered results are scoped to the server-derived owning Tenant and cannot leak across Tenant boundaries. |

The terms statutory invoice, certified tax output, FATOORA, regulator
submission, taxpayer phase, and PDPL compliance workflow are future-boundary
terms only in this document. They are not names for any Release 1 artifact.

## 8. Localization business requirements

### 8.1 Language and terminology requirements

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-L10 | Core ERP navigation, labels, field captions, statuses, validation messages, empty states, and generic document/report labels must support Arabic and English presentation. | BRD requirement; Platform/Country-Pack owner with each domain owner |
| KSA-L11 | A user may choose Arabic or English only within the server-authorized context. The choice changes presentation, not data scope, source truth, permissions, or workflow state. | BRD requirement; Platform/Identity |
| KSA-L12 | Product terminology must use the mandatory ERP glossary and a controlled translation/terminology ownership path. A Tenant preference may not rename a core concept into a customer-specific product rule. | BRD requirement; Product Owner and Country-Pack owner |
| KSA-L13 | English must remain an explicit supported language and a safe baseline for any artifact whose Arabic translation is missing or not approved. | BRD requirement; Platform/Country-Pack owner |
| KSA-L14 | Arabic and English user-entered business names, addresses, references, and notes must be preserved as entered within the owning domain's data rules. Localization must not transliterate, overwrite, or discard the original value as a display convenience. | BRD requirement; owning domain |
| KSA-L15 | Translation status must be distinguishable from source-data status. A missing translation is a presentation/configuration exception, not a reason to change, reject, post, or delete the source record. | BRD requirement; Platform/Country-Pack owner |

### 8.2 RTL and mixed-direction requirements

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-R20 | Arabic presentation must use an RTL page structure for navigation, forms, tables, generic documents, and reports where direction is meaningful. | BRD requirement; Platform and owning domain |
| KSA-R21 | Direction-sensitive controls must remain understandable in both RTL and LTR; labels, field/value association, table reading order, actions, and validation placement must not rely on color or position alone. | BRD requirement; Platform and UX ownership |
| KSA-R22 | Mixed Arabic/English text, Latin identifiers, SKU/barcode values, dates, decimal values, punctuation, and codes must remain readable and unambiguous when displayed in RTL context. | BRD requirement; Platform/Country-Pack and owning domain |
| KSA-R23 | Stable identifiers and codes must retain their canonical character order and must not be reversed, localized into a different identifier, or made inaccessible by visual direction. | BRD requirement; owning domain |
| KSA-R24 | A direction switch must not change the selected record, filter scope, status, quantity, amount, or audit meaning. | BRD requirement; Platform and owning domain |

### 8.3 Generic bilingual documents, reports, and exports

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-D30 | A generic core ERP document, report, or export that supports localization must declare an allowed presentation mode: Arabic, English, or bilingual. The owning domain controls the artifact's business meaning. | BRD requirement; owning domain |
| KSA-D31 | Bilingual generic artifacts must present labels, captions, statuses, and explanatory text in both supported languages when approved translation content exists. Missing content follows KSA-F70 and is visible as a fallback/translation state. | BRD requirement; owning domain/Country-Pack |
| KSA-D32 | Generic artifacts must retain source identifiers, source references, data-as-of or effective-date facts, amounts, quantities, statuses, and authorized organizational scope independent of language or direction. | BRD requirement; owning domain and Reporting |
| KSA-D33 | A localized generic artifact must not be named or described as a statutory invoice, tax certificate, regulator filing, ZATCA/FATOORA output, or certified compliance evidence. | Confirmed boundary; Product Owner |
| KSA-D34 | Exports must preserve the selected authorized scope and must expose the presentation locale/profile used without exposing another Tenant's data. Format/provider/retention decisions remain outside this BRD. | BRD requirement; owning domain, Reporting, Platform |
| KSA-D35 | Reporting remains read-only and owns report presentation, lineage, data-as-of, and freshness semantics. Localization cannot create a report catalogue, KPI formula, scheduled distribution, or reconciliation owner. | Confirmed boundary plus BRD requirement; Reporting/MESP-53 |

### 8.4 Locale, date, time, number, and currency presentation

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-N40 | Locale configuration must be explicit, versioned, scoped, and effective-dated at the allowed Tenant/Company boundary. A user preference cannot widen the configuration scope. | BRD requirement; Platform/Country-Pack |
| KSA-N41 | Date and time presentation must use the effective locale/timezone configuration; UTC storage and server-derived event meaning remain the architectural baseline. | Confirmed baseline plus BRD requirement; Platform/owning domain |
| KSA-N42 | Asia/Riyadh may be the configurable Saudi-oriented timezone default. It is a default profile, not a permanent or universal Tenant rule. | Confirmed baseline; Platform/Country-Pack |
| KSA-N43 | Date, number, decimal separator, grouping, and negative-value presentation must be understandable for the selected locale and must not change the underlying value or posting date. | BRD requirement; Platform/owning domain |
| KSA-N44 | SAR may be the configurable default currency for a Saudi-oriented country-pack profile. The default is not universal, is not a tax decision, and does not answer multi-currency or exchange-rate policy. | Confirmed baseline plus BRD requirement; Finance/Country-Pack |
| KSA-N45 | Currency symbols, codes, precision, and displayed totals must be attributable to the source currency and effective Finance policy. Localization cannot invent conversion, rate, rounding, realized/unrealized, or Reporting Currency behavior. | BRD requirement; Finance/MESP-54 |
| KSA-N46 | A locale or country-pack change must not rewrite historical source documents, posted accounting, stock facts, audit events, or the original applied configuration/effective-date evidence. | BRD requirement; owning domain/Audit |

### 8.5 Country-pack configuration and reuse

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-C50 | A Saudi-oriented country pack must be reusable by more than one Tenant and must carry an explicit version, owner, status, effective period, and change history at the business level. | BRD requirement; Platform/Country-Pack |
| KSA-C51 | Tenant activation of a country-pack profile must be explicit and authorized. A Tenant can choose an allowed profile but cannot edit the product definition or affect another Tenant. | BRD requirement; Platform/Tenant Administration |
| KSA-C52 | Country-pack configuration may hold language, direction, locale, timezone, SAR/default presentation, approved generic terminology, and future extension references. It must not silently contain statutory tax rules, regulator submission behavior, legal conclusions, or production-provider settings in this slice. | BRD requirement; Product Owner/Country-Pack |
| KSA-C53 | Country-pack updates must distinguish a new effective configuration from historical configuration. Existing records must remain reproducible with their source and configuration evidence. | BRD requirement; Country-Pack/owning domain/Audit |
| KSA-C54 | A country pack must have a safe fallback or be rejected before activation if required localization content is missing or invalid. Partial activation cannot silently present a different business meaning. | BRD requirement; Country-Pack/Platform |
| KSA-C55 | A Wafra observation can validate a generic reusable requirement only. It cannot create a Wafra-only country-pack branch, default, label, workflow, or customer fork. | Confirmed baseline; Product Owner |

### 8.6 Search, sort, filter, fallback, and error requirements

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-F60 | Display, filtering, sorting, and search must retain the server-authorized Tenant/Company/Branch/Warehouse scope regardless of language or locale. | BRD requirement; Platform/owning domain |
| KSA-F61 | Localized display of dates, numbers, currency, and text must not cause a hidden change to the underlying comparison value, source identifier, status, or effective date. | BRD requirement; Platform/owning domain |
| KSA-F62 | Arabic normalization, tokenization, collation, transliteration, locale-specific case behavior, and cross-language search semantics remain an open ADR-011 dependency. This BRD does not select an algorithm or implementation rule. | Open decision / gate; ADR-011 |
| KSA-F63 | Where localized search is not yet approved or supported, the user receives a stable, understandable result or explicit unsupported-behavior message; the system must not pretend that an incomplete search is exhaustive. | BRD requirement; Platform/owning domain |
| KSA-F64 | Missing translation, unsupported locale, invalid country-pack version, or unavailable formatting profile must produce a deterministic fallback/error state and must preserve the source record and scope. | BRD requirement; Platform/Country-Pack |
| KSA-F65 | A fallback must identify enough context for an authorized user to understand that presentation content is incomplete; it must not be represented as a statutory or certified result. | BRD requirement; Platform/owning domain |

### 8.7 Audit, configuration evidence, and authority

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-A70 | Material country-pack, locale, language-default, timezone, currency-presentation, translation, or activation changes must produce immutable audit/configuration evidence. | BRD requirement; Audit/Platform |
| KSA-A71 | Configuration evidence must identify the server-derived owning Tenant and allowed subordinate scope, actor or support grant, permission basis, prior/current version or value, effective period, result, and correlation/reference facts appropriate to the domain. | BRD requirement; Audit/Platform |
| KSA-A72 | A client-supplied language, locale, country, Tenant, Company, or direction value cannot override server-derived authority or expose another Tenant's configuration, document, report, export, or audit record. | Confirmed baseline plus BRD requirement; Platform/Identity |
| KSA-A73 | Audit history is immutable and is corrected by a new event or superseding configuration record, never by editing or deleting the original event. | Confirmed baseline; Audit/Platform |
| KSA-A74 | Failed or rejected localization/configuration actions must leave evidence of the attempted outcome where the audit policy requires it, without storing unnecessary sensitive content. MESP-50 remains the authority for retention/privacy/legal-hold/purge policy. | Conditional BRD requirement; Audit/MESP-50 |

### 8.8 Cross-module localization contract

| ID | Requirement | Classification / owner |
| --- | --- | --- |
| KSA-X80 | Every affected domain must identify which labels, statuses, source fields, generic documents, reports, exports, and validation messages it owns and which localization service/configuration it consumes. | BRD requirement; each domain owner |
| KSA-X81 | A localized handoff must preserve source-document identity, Tenant/Company/Branch/Warehouse scope, amount/quantity/currency facts, lifecycle status, and audit lineage across module boundaries. | BRD requirement; source owners/Reporting |
| KSA-X82 | Localization may not change Procurement, Inventory, Finance, Sales, Reporting, or Platform ownership, nor may it create a second source of truth for a domain term or status. | Confirmed baseline plus BRD requirement; Product Owner/domain owners |
| KSA-X83 | Generic tax/accounting facts already owned by Finance may be displayed in Arabic/English and locale-aware formats. This BRD does not select Saudi statutory treatment, rate, return, invoice, or compliance behavior. | Confirmed boundary; Finance/Product Owner |
| KSA-X84 | A later implementation handoff must map each requirement to the owning module and named open dependency before creating source work. This BRD creates no source task. | BRD requirement; Product Owner/delivery governance |

## 9. Cross-module ownership and dependency matrix

| Area | Localization consequence in this BRD | Source/business owner | Preserved dependency or boundary |
| --- | --- | --- | --- |
| Platform and Identity | Language/locale selection, direction, Tenant/Company/Branch scope, permissions, and server-derived context. | Platform Administration and Identity/Access | ADR-004; Tenant context cannot come from the client. |
| Organization | Allowed configuration scope and Company/Branch presentation preference where approved. | Organization/Company Structure | Tenant → Company/Legal Entity → Branch → Warehouse hierarchy remains mandatory. |
| Saudi Country Pack | Reusable versioned language/locale/timezone/SAR presentation profile and future-extension references. | Country-Pack/Product governance | No Wafra branch; no statutory/legal/provider behavior. |
| Master Data | Consumes approved localized labels or descriptions where the owning Master Data baseline permits them. | Master Data | No Product/Item/SKU/Barcode/Category/UOM implementation or identity decision here. |
| Procurement | Arabic/English/RTL presentation of owned P2P labels, statuses, generic purchasing documents, validation, and exports. | Procurement | Supplier remains an external business party; Finance/Inventory ownership remains unchanged. |
| Inventory | Arabic/English/RTL presentation of warehouse, stock, movement, UOM, and generic inventory artifacts. | Inventory | Localization cannot change quantity, valuation, tracking, stock status, or ledger meaning. |
| Finance | Locale-aware presentation of amounts, currencies, dates, generic tax/accounting labels, and generic Finance documents. | Finance | MESP-54, MESP-110, MESP-49, and external tax validation remain open/deferred as named. |
| B2B Sales | Arabic/English/RTL presentation of B2B quotations, orders, fulfillment handoffs, generic invoices/receipts, and reports. | B2B Sales | Retail POS and cashier behavior remain excluded. |
| Reporting | Localized read-only labels, filters, result presentation, exports, lineage, data-as-of, and freshness. | Reporting | MESP-53 controls final catalogue, KPI definitions, owners, scheduling, and distribution. |
| Audit and configuration | Immutable evidence for material localization/configuration effects. | Audit/Platform | MESP-50 controls retention, privacy, legal hold, purge, residency, backup, and restoration. |
| Files and future integrations | Generic artifact boundary and future extension seam only. | Platform/Integration later | No external provider, government, bank, FATOORA, credential, XML, QR, or submission behavior. |
| Wafra validation | Generic examples can expose missing reusable requirements. | Product Owner | No Wafra-specific core logic, fork, workflow, or default. |

## 10. Configuration and business process requirements

### 10.1 Configuration lifecycle

The business lifecycle for a locale or country-pack configuration is:

1. **Proposed:** an authorized owner identifies a reusable configuration or
   translation change and its scope/effective period.
2. **Validated:** required language content, terminology, direction, locale,
   fallback, and affected-domain evidence is checked. A future statutory or
   legal claim cannot be validated by this lifecycle.
3. **Activated:** the authorized Tenant/Company scope selects the approved
   version. Activation is rejected if required content or dependency evidence
   is missing.
4. **Effective:** authorized users receive the configuration in the relevant
   scope. Source facts, status, and audit meaning remain unchanged.
5. **Superseded:** a later approved version becomes effective. Earlier
   configuration and rendered-history evidence remains reproducible.
6. **Retired or unavailable:** the configuration cannot be selected for new
   use. Existing source evidence remains intact and a defined fallback/error
   path applies.

This is a business lifecycle, not an implementation state machine. The
platform, Country-Pack owner, and MESP-50 governance must define the exact
retention and purge treatment in later work.

### 10.2 User presentation flow

For an authorized request to view or produce a generic ERP artifact:

1. the server resolves the user's Tenant and organizational authority;
2. the allowed language, locale, direction, country-pack version, and
   effective configuration are resolved;
3. the owning domain supplies source facts, labels, statuses, and artifact
   semantics;
4. the presentation applies Arabic/English/RTL/locale rules without mutating
   source truth;
5. missing or unsupported content follows the defined fallback/error path;
6. the result exposes the authorized scope and any required locale/fallback
   evidence; and
7. material configuration or access effects are auditable under the owning
   policy.

No client-side language or locale input can skip step 1, alter step 3, or
  override step 7.

### 10.3 Error and fallback process

The user-facing business outcomes are:

- **Supported:** requested language/locale/country-pack is available and the
  artifact is rendered in the authorized mode.
- **Fallback:** the requested presentation content is missing but an approved
  fallback preserves the artifact and makes the incomplete translation
  visible.
- **Unsupported:** a requested behavior, such as an unapproved localized
  search semantic, is not available; the response is explicit and does not
  claim completeness.
- **Rejected:** the actor lacks authority, the profile is outside scope, or
  activation evidence is invalid; no cross-Tenant or unauthorized result is
  returned.
- **Unknown:** the result of a dependent operation cannot be established; the
  system must not report successful activation or silently use a different
  business meaning.

The exact error codes, UI controls, retry policy, and implementation contract
are later work. They must preserve the business distinctions above.

## 11. Data and configuration requirements

This section describes business facts, not a schema. A later implementation
must choose structures consistent with ADR-002/006 and the owning module
boundaries.

| Fact | Required business meaning | Scope/history rule |
| --- | --- | --- |
| Language profile | Arabic or English presentation and whether bilingual output is allowed for the artifact | Must be allowed by the server-derived scope and remain version/effective-date traceable where material. |
| Direction profile | RTL/LTR behavior for the selected presentation | Direction cannot reverse identifiers or change source semantics. |
| Locale profile | Date, time, number, decimal, grouping, and related presentation facts | Explicit, validated, effective-dated, and scoped; not a legal/tax classification. |
| Timezone default | Asia/Riyadh may be the Saudi-oriented default | Configurable; event/source meaning and UTC storage baseline remain unchanged. |
| Currency presentation | SAR may be the Saudi-oriented default; source currency remains attributable | No rate, conversion, Reporting Currency, or rounding policy is selected. |
| Country-pack identity/version | Reusable profile selected by an authorized Tenant/Company scope | No cross-Tenant mutation; old versions remain traceable for historical evidence. |
| Translation/terminology state | Approved, missing, fallback, superseded, or rejected content state | Must not be confused with document or source-record lifecycle. |
| Effective period | When the profile is applicable | Historical artifacts preserve the source/configuration facts needed for reproduction. |
| Owner and approval evidence | Product/domain/configuration authority for the profile or translation | Approval is not an external legal or statutory certification. |
| Fallback policy | Result when content/profile is absent or invalid | Must be deterministic, visible, and scope-safe. |

The exact placement of configuration at Platform, Tenant, Company, or a
subordinate scope remains an implementation/design decision constrained by
server authority and the approved organization hierarchy. This BRD requires
the business outcome and isolation; it does not prescribe a table, entity, or
API.

## 12. Document, report, and export requirements

### 12.1 Generic document requirements

Each owning domain must identify the generic artifacts it supports and record:

- allowed language modes: Arabic, English, bilingual, or a documented
  unsupported state;
- labels, captions, statuses, source references, amounts, quantities, dates,
  and identifiers that must remain semantically stable;
- whether user-entered names/addresses are shown in the original value,
  translated label, or both;
- fallback behavior for missing translation or unsupported direction;
- the authorized Tenant/Company/Branch scope; and
- the owning domain's audit, correction, and history semantics.

No generic artifact may be presented as a statutory invoice, certified tax
output, regulator filing, or external-compliance evidence by virtue of being
Arabic, bilingual, or Saudi-oriented.

### 12.2 Reporting requirements

Reporting may localize titles, labels, filters, column headings, statuses,
dates, numbers, and currency presentation where its baseline permits it. It
must also preserve source lineage, data-as-of, freshness, result status, and
authorized scope. Reporting must not use this BRD to decide:

- the final report catalogue or KPI formulas;
- named report/reconciliation owners;
- scheduled distribution, recipients, or delivery channels;
- Reporting Currency or exchange-rate policy; or
- statutory, tax, or regulator reporting.

Those items remain with MESP-53, MESP-54, Finance, and the applicable future
external validation.

### 12.3 Export requirements

An authorized export of a generic core ERP artifact must retain:

- the server-authorized Tenant and organizational scope;
- source identifiers and references in stable order;
- source amounts, quantities, currency facts, dates, statuses, and data-as-of
  values;
- the presentation language/locale/direction profile used, where the owning
  export contract requires it; and
- fallback or incomplete-translation indication where applicable.

Export file type, delivery provider, schedule, retention, residency, and
backup are outside this BRD and remain subject to MESP-48/MESP-50 and later
implementation decisions.

## 13. Validation, permissions, and separation of duties

### 13.1 Validation rules

- A selected language must be one of the supported/allowed values.
- A locale/timezone/country-pack profile must be valid, versioned, and
  effective for the server-derived scope.
- A Saudi-oriented SAR/Asia/Riyadh default must be treated as a configurable
  profile, not a hard-coded or universal product fact.
- A bilingual artifact must have an owning domain and declared fallback path.
- A translated term must not replace a source identifier, amount, quantity,
  date, status, or user-entered value.
- A requested localized search behavior must remain unavailable or conditional
  until ADR-011 and the owning domain approve its semantics.
- A configuration request outside Tenant/Company/Branch authority must be
  denied without revealing another Tenant's configuration or source data.
- A country-pack with missing mandatory localization content must not become
  silently active.
- Any generic artifact that contains statutory/compliance wording must be
  rejected from this scope or routed to a separately approved future boundary.

### 13.2 Permissions and approval controls

At business level, separate these actions:

- maintain reusable Country-Pack definitions and approved translations;
- activate a Country-Pack/profile for an owning Tenant/Company scope;
- change a Tenant/Company presentation preference;
- view localized generic documents/reports/exports;
- view configuration and audit evidence; and
- approve a product decision or future external-compliance extension.

The detailed permission catalogue and delegated authority remain with Platform
Administration and the relevant later governance item. A tenant administrator
must not approve a product-wide country-pack change by changing a preference,
and a locale selection must not grant a permission.

### 13.3 Separation of duties

- The owner of a reusable product/Country-Pack definition is not silently
  treated as the approver of a statutory, tax, legal, or privacy conclusion.
- The person who changes a Tenant configuration may be recorded separately
  from the Product Owner who approves a product baseline.
- The localization layer cannot approve, post, reverse, tax, receive, issue,
  deliver, invoice, pay, collect, or reconcile a source-domain transaction.
- Audit evidence is independently immutable and cannot be edited by the actor
  who made the configuration change.

## 14. Audit and evidence requirements

For each material localization/configuration event, the evidence should make
it possible for an authorized reviewer to determine:

- what profile, translation, locale, direction, timezone, or presentation
  default was proposed, activated, superseded, rejected, or unavailable;
- who or what server-authorized actor initiated the action;
- the owning Tenant and allowed subordinate scope;
- the permission or support-grant basis;
- the previous and new version/value or an appropriate non-sensitive summary;
- the effective date/period and outcome;
- the affected generic artifact/domain, if any; and
- the link to a later correction/superseding record when applicable.

Audit events and source documents are immutable. Retention, privacy, legal
hold, purge, residency, backup, restoration, support geography, and private
artifact policy remain open under MESP-50 and ADR-009. This BRD must not
promise a duration, region, provider, or purge behavior.

## 15. Given / When / Then acceptance scenarios

These are business acceptance scenarios. They do not prescribe a test
framework, endpoint, screen hierarchy, database structure, or production
environment. Scenarios marked conditional cannot become implementation scope
until their named dependency is approved.

### 15.1 Language, direction, and mixed content

**KSA-GWT-001 - English presentation**

**Given** an authorized user selects English in an allowed scope
**When** the user opens an in-scope generic ERP page or artifact
**Then** supported labels, statuses, validation messages, dates, numbers, and
direction are presented in the approved English mode without changing source
meaning, scope, or authority.

**KSA-GWT-002 - Arabic presentation**

**Given** an authorized user selects Arabic in an allowed scope
**When** the user opens the same generic ERP page or artifact
**Then** approved labels and messages are presented in Arabic, the direction is
RTL where meaningful, and the selected records and source facts are unchanged.

**KSA-GWT-003 - RTL form usability**

**Given** an Arabic user views an in-scope form containing labels, values,
validation, actions, and required fields
**When** the form is presented in RTL
**Then** field/value association, required/error meaning, reading order, and
action meaning remain understandable without relying on color or position
alone.

**KSA-GWT-004 - RTL table usability**

**Given** a localized table includes identifiers, Arabic text, English text,
numbers, dates, and amounts
**When** it is viewed in RTL
**Then** rows, columns, identifiers, numeric values, and headings remain
readable and the underlying sort/filter scope is unchanged.

**KSA-GWT-005 - Mixed Arabic and English content**

**Given** a source record contains Arabic and English names, a Latin code, and
numeric values
**When** it is presented in either direction
**Then** the original values and canonical code order remain visible and
unambiguous; localization does not transliterate or reverse the source value.

**KSA-GWT-006 - Language switch**

**Given** an authorized user is viewing a selected record in English
**When** the user changes presentation language to Arabic
**Then** the same record, scope, status, amount, quantity, and audit meaning are
shown in the Arabic presentation mode without a new authorization decision.

**KSA-GWT-007 - Stable identifiers**

**Given** a document or report contains a source identifier, reference, SKU,
barcode, or code
**When** its language or direction changes
**Then** the identifier remains canonical and selectable/readable in the same
character order.

### 15.2 Generic documents, reports, and exports

**KSA-GWT-008 - Bilingual generic document**

**Given** an owning domain has approved a generic document for bilingual mode
**When** an authorized user produces it
**Then** approved Arabic and English labels, statuses, source references,
amounts, quantities, and dates are presented without describing the artifact as
statutory, certified, or regulator-submitted.

**KSA-GWT-009 - Bilingual report**

**Given** Reporting permits bilingual presentation for an in-scope report
**When** the report is generated for an authorized scope
**Then** labels and headings adapt while source lineage, data-as-of, freshness,
result status, amounts, and authorized scope remain intact.

**KSA-GWT-010 - Authorized generic export**

**Given** an authorized user exports a generic core ERP artifact
**When** the export is produced in Arabic, English, or bilingual mode
**Then** the export preserves source identifiers, scope, values, statuses, and
the applicable presentation/fallback evidence without exposing another Tenant.

**KSA-GWT-011 - No source mutation by localization**

**Given** a source record has a stored amount, quantity, date, status, or
user-entered name
**When** it is displayed in another language or locale
**Then** the source record and its history remain unchanged.

**KSA-GWT-012 - Generic output non-claim**

**Given** a user requests a Saudi-oriented generic invoice-like or report-like
artifact
**When** the artifact is assessed against this BRD
**Then** it is described only as a generic ERP artifact and is not labeled as a
statutory invoice, tax certificate, ZATCA/FATOORA output, or compliance proof.

### 15.3 Locale, timezone, number, and currency presentation

**KSA-GWT-013 - Saudi-oriented default profile**

**Given** a Tenant activates an allowed Saudi-oriented presentation profile
**When** the profile becomes effective
**Then** its configured default may be Arabic/RTL, SAR, and Asia/Riyadh, while
the profile remains configurable, scoped, versioned, and non-statutory.

**KSA-GWT-014 - Non-universal default**

**Given** two Tenants have different authorized locale profiles
**When** each views the same class of generic artifact in its own scope
**Then** each receives its own allowed presentation configuration and neither
Tenant's default changes the other Tenant's source facts or configuration.

**KSA-GWT-015 - Timezone presentation**

**Given** an event has a server-recorded time and an effective timezone profile
**When** an authorized user views the event
**Then** the displayed time follows the profile while the event identity,
ordering, source meaning, and audit history remain unchanged.

**KSA-GWT-016 - Date and number presentation**

**Given** a source amount, quantity, decimal, date, or negative value is valid
in the owning domain
**When** it is displayed in an allowed locale
**Then** grouping, separator, sign, and date presentation are understandable and
the canonical value is not changed or rounded by presentation alone.

**KSA-GWT-017 - SAR attribution**

**Given** a Saudi-oriented profile uses SAR as its configured default
**When** a generic artifact displays a monetary value
**Then** the displayed code/symbol is attributable to the source currency and
does not imply an exchange-rate, tax, or statutory decision.

**KSA-GWT-018 - Multi-currency boundary**

**Given** a source transaction is in a non-SAR currency
**When** it is presented under a Saudi-oriented locale profile
**Then** the source currency remains visible and no conversion, Reporting
Currency, rate, realized/unrealized treatment, or rounding policy is invented.

**KSA-GWT-019 - Historical configuration**

**Given** a generic artifact was created or posted under an earlier effective
configuration
**When** a later locale or country-pack version becomes active
**Then** the earlier source/configuration facts remain reproducible and are not
rewritten by the later presentation default.

### 15.4 Tenant authority, fallback, and audit

**KSA-GWT-020 - Server-derived Tenant scope**

**Given** a user supplies a browser language, locale, or direction preference
and belongs to one Tenant
**When** the user requests a localized artifact
**Then** the server derives the Tenant and organizational scope and the
preference cannot expose or select another Tenant.

**KSA-GWT-021 - Unauthorized configuration**

**Given** a user lacks permission to activate or change a country-pack/profile
**When** the user attempts that action
**Then** the action is denied, the other scope's configuration is not revealed,
and the outcome follows the applicable audit policy.

**KSA-GWT-022 - Configuration audit**

**Given** an authorized actor activates or supersedes a material locale,
translation, timezone, currency-presentation, or country-pack configuration
**When** the action completes
**Then** immutable evidence records the server-derived scope, actor/authority,
version/value summary, effective period, and outcome.

**KSA-GWT-023 - Missing translation fallback**

**Given** a requested Arabic or English translation is missing
**When** an authorized user opens the generic artifact
**Then** the approved fallback is displayed, the incomplete translation state
is understandable, and the source record/status/value is preserved.

**KSA-GWT-024 - Unsupported localized search**

**Given** the requested Arabic search/collation behavior has not been approved
under ADR-011
**When** a user invokes that behavior
**Then** the system returns an explicit supported/unsupported result and does
not claim exhaustive localized matching or silently widen scope.

**KSA-GWT-025 - Invalid country-pack**

**Given** a country-pack version lacks required content or is invalid for the
requested scope
**When** activation or rendering is attempted
**Then** activation is rejected or a defined fallback/error state is returned;
no different business meaning is silently substituted.

### 15.5 Cross-module preservation

**KSA-GWT-026 - Procurement handoff**

**Given** a Procurement source document is handed to Inventory or Finance
**When** its labels or generic presentation use Arabic/English/RTL
**Then** source-document identity, commercial status, quantities, amounts,
currency, and ownership remain unchanged.

**KSA-GWT-027 - Inventory facts**

**Given** an Inventory artifact is displayed in Arabic or English
**When** the locale changes
**Then** Warehouse, item/product identity, UOM, quantity, movement, valuation,
tracking, and stock status retain their source meaning.

**KSA-GWT-028 - Finance facts**

**Given** a Finance artifact contains accounting, tax, currency, or posting
facts
**When** the artifact is localized
**Then** Finance remains the source owner and localization does not select
statutory treatment, rate, return, exchange-rate policy, posting rule, or
period behavior.

**KSA-GWT-029 - B2B Sales boundary**

**Given** a B2B quotation, order, delivery handoff, generic invoice, receipt,
return, or report is localized
**When** it is presented in Arabic/English/RTL
**Then** the B2B commercial chain remains intact and no Retail POS/cashier
behavior appears.

**KSA-GWT-030 - Reporting boundary**

**Given** Reporting renders a localized result
**When** labels, filters, headings, or formats change
**Then** Reporting remains read-only, preserves source lineage/data-as-of/
freshness, and does not close MESP-53 or change a source fact.

**KSA-GWT-031 - Wafra validation-only**

**Given** Wafra supplies an Arabic/English or RTL example
**When** the example is reviewed against this BRD
**Then** it may validate a reusable generic requirement but cannot create a
Wafra-specific rule, default, workflow, label, or fork.

### 15.6 Future-boundary and visual acceptance

**KSA-GWT-032 - Deferred statutory request**

**Given** a request asks for ZATCA/FATOORA, statutory VAT/e-invoicing, regulator
submission, certification, or taxpayer phase behavior
**When** it is assessed against this BRD
**Then** it is routed to a separately approved future external-compliance or
integration boundary and is not treated as Release 1 localization scope.

**KSA-GWT-033 - Deferred legal/privacy/production request**

**Given** a request asks for PDPL regulatory workflow, legal conclusion,
residency/provider selection, retention/purge promise, backup/DR, or production
infrastructure
**When** it is assessed against this BRD
**Then** it remains outside this baseline and is not inferred from the
Saudi-oriented locale or country-pack configuration.

**KSA-GWT-034 - Visual acceptance**

**Given** the same representative generic ERP flow is reviewed in Arabic/RTL,
English/LTR, and mixed Arabic/English content
**When** an authorized product/domain reviewer assesses the result
**Then** text remains readable, direction is coherent, identifiers and numbers
remain unambiguous, validation is understandable, and no visual defect changes
the business meaning or authorized scope.

**KSA-GWT-035 - No Retail POS**

**Given** a proposed localization example includes cashier, consumer checkout,
cash drawer, restaurant, or retail shift behavior
**When** it is reviewed against Release 1
**Then** it is rejected as out of scope unless a later approved scope change
authorizes it.

**KSA-GWT-036 - Unknown dependent result**

**Given** a required translation/profile/configuration dependency has an
unknown result
**When** the user requests the affected artifact
**Then** the result is shown as unknown/pending or follows an approved fallback;
the system does not report a successful activation or silently use a different
business rule.

## 16. Open decisions, gates, and non-resolution record

This section is a traceability map, not a new Product Decision Register. No
row is closed by this BRD. An open decision can change only through the
owning Jira/Product Decision Register evidence.

| ID / gate | Current position | Consequence for this BRD |
| --- | --- | --- |
| ADR-011 | Required but not approved as a full runtime decision | Arabic search/collation/tokenization, runtime localization details, RTL implementation details, and bilingual-document implementation remain conditional. |
| MESP-23 | In Progress living register | The BRD links to open questions but does not answer or close them. |
| MESP-49 | Done only for Release 1 deferred disposition | No statutory VAT/e-invoicing/ZATCA/FATOORA behavior or conclusion is added. |
| MESP-50 | To Do/open | No retention, privacy, legal hold, purge, residency, backup, restoration, provider, or production promise is made. |
| MESP-48 | To Do/open | No numeric volume, localization catalog size, freshness, async threshold, or recovery target is selected. |
| MESP-53 | To Do/open | No final report catalogue, KPI formula, named owner, schedule, or distribution policy is selected. |
| MESP-54 | To Do/open | No exchange-rate source/update/approval, Reporting Currency, conversion, realized/unrealized, or rounding policy is selected. |
| MESP-110 / FIN-OD-09 | To Do/open | No Finance year-end, Payment Term, due-date, aging, settlement, or posting-dimension policy is selected. |
| External validation | Outstanding for future statutory, tax, legal, privacy, banking, and production claims | Future validation remains required; it is not replaced by a product-only localization approval. |

The following are explicitly **not** new requirements from this table: a Saudi
statutory rate, invoice schema, e-invoice/QR/XML behavior, taxpayer phase,
residency promise, privacy role, provider, exchange-rate source, report
catalogue, or Finance policy.

## 17. Future implementation and test handoff

This section identifies evidence a later separately authorized implementation
item would need. It creates no source task and is not a Definition of Ready for
coding by itself.

Before implementation work can be activated, the delivery owner must have:

- an approved version of this product-only BRD and a complete trace from each
  implementation story to a requirement and owning domain;
- the approved ADR-011/runtime localization decision covering translation
  ownership, Arabic search/collation/tokenization, RTL behavior, bilingual
  artifact semantics, fallback, and supported locale behavior;
- an approved configuration ownership model at Platform/Tenant/Company scope
  that preserves server-derived authority and no cross-Tenant sharing;
- domain-by-domain artifact inventories for Procurement, Inventory, Finance,
  B2B Sales, Reporting, Platform, Audit, and generic documents/exports;
- a translation/terminology governance path, missing-content policy, and
  version/effective-date evidence model;
- a locale/timezone/number/currency presentation matrix that does not choose
  MESP-54 or Finance statutory policy;
- Arabic/English, RTL/LTR, mixed-direction, fallback/error, Tenant-isolation,
  effective-date, audit, and visual acceptance coverage derived from the GWT
  catalogue;
- confirmed MESP-48 supported-volume/recovery/freshness evidence where
  applicable;
- confirmed MESP-50 privacy, retention, legal-hold, purge, residency, backup,
  and restoration governance where artifacts or exports are persisted; and
- explicit future-gate treatment for any statutory, tax, external integration,
  legal, privacy-regulatory, or production request.

The future implementation must follow the four-project topology and module
ownership in ADR-002, server-derived authority in ADR-004, shared database and
Tenant boundaries in ADR-006, private artifacts in ADR-009, and non-production
testing/provider gates in ADR-018. This BRD does not select an API, database,
entity, migration, UI technology, provider, deployment, or test framework.

## 18. Review notes and source-conflict handling

- The approved PRD remains unchanged. Historical PRD references to broader
  Saudi statutory/compliance concerns are retained as traceability evidence,
  while PD-023 and `docs/27_Release_1_Saudi_Localization_Scope_Rebaseline.md`
  control the current product slice.
- The MESP-111 readiness verdict remains true for deferred external-validation
  areas: the official evidence pack does not substitute for qualified Saudi
  tax, legal, privacy, banking, or compliance validation.
- “Saudi-oriented default” means configurable SAR/Asia/Riyadh presentation
  defaults for an allowed profile. It never means universal Saudi tax/legal
  treatment or a production residency claim.
- No recommendation from the Founder Decision Pack has been promoted to a
  requirement. No MESP-23 row has been answered or closed.
- No source behavior, persistence, API, UI, migration, provider, credential,
  integration, tax, legal/privacy workflow, production configuration, Retail
  POS behavior, or Wafra-specific core behavior is authorized by this BRD.

## 19. Definition of Done for this BRD session

This bounded session is complete when:

- `docs/28_Release_1_Saudi_Localization_BRD.md` is reviewed as the canonical
  product-only MESP-37 artifact;
- the document contains the stated scope matrices, source traceability,
  ownership/dependency table, configuration/audit requirements, GWT catalogue,
  non-claims, open-gate map, and future handoff;
- the Product Decision Register remains append-only and no new decision is
  invented or silently closed;
- MESP-23 and all named open gates remain live and preserved;
- repository/state/statistics/plan handoffs identify the next exact session;
- Jira records activation, validation, Owner approval at the exact product-only
  scope, and closure evidence; and
- the focused documentation change is reviewed, merged if clean and
  unblocked, and the final main worktree is synchronized and clean.

## 20. Approval and handoff record

This document is an **Approved bounded product-only baseline**. Hossam's Owner
approval is recorded in Jira comment 10857 after validation in comment 10855.
The approval is limited to the requirements in sections 3 through 17 at the
exact localization/core ERP scope. MESP-22 comment 10856 records that no new
Product Decision was created, and MESP-23 handoff comment 10858 preserves the
open register and named gates.

The approval does not approve or close the open decisions or the future
statutory, tax, legal, privacy, external-integration, or production boundaries
listed above. MESP-37 closure is recorded in Jira comment 10859 after focused
PR #55 merged to `main` at
`7d03fa5b19226b8c6368012ec90c8a09eefd4aaf`. A later change to this baseline
must use a new reviewed version or superseding decision; it must not silently
edit the approved scope.
