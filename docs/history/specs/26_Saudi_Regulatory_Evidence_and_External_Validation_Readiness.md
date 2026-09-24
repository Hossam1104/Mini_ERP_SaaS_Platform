# Saudi Regulatory Evidence and External-Validation Readiness

**Scope:** MESP-37 prerequisite only
**Evidence cut-off and retrieval date:** 11 August 2026
**Owning Jira task:** MESP-111 — Prepare Saudi regulatory evidence and external-validation readiness
**Related Jira items:** MESP-37, MESP-49, MESP-50, MESP-23, MESP-53, MESP-54, MESP-110

## 1. Verdict

### READY FOR MESP-37 DRAFT ONLY — EXTERNAL VALIDATION OUTSTANDING

The official-source register and traceability pack are ready to support a
bounded MESP-37 draft. The pack is not evidence that the product is legally,
tax-wise, privacy-wise, or operationally compliant. No qualified Saudi tax or
compliance adviser validation, qualified Saudi privacy or legal adviser
validation, Finance Controller decision, or Product Owner decision set was
provided or recorded for this task.

Accordingly:

- MESP-37 remains **To Do** and is not activated by this artifact.
- MESP-49 remains **To Do**. Saudi e-invoicing and tax conclusions are not
  closed by inference from official web pages.
- MESP-50 remains **To Do**. Residency, cross-border transfer, retention,
  destruction, legal hold, backup, support, subprocessor, and privacy-governance
  decisions are not closed by inference from official web pages.
- MESP-23 remains **In Progress**; unresolved questions must remain visible.
- No Product, Tax, e-invoicing, PDPL, storage, credential, integration, or
  production source behavior is implemented by this task.

This verdict means the repository can carry a dated, source-grounded draft
input and an advisor question pack. It does not authorize a Saudi BRD approval
session, a production claim, a taxpayer activation, or a FATOORA integration.

## 2. Source register

All sources below were rechecked against the current official page or official
document location on 11 August 2026. A page's current update date is recorded
where the page exposed one. Where the official knowledge-center page did not
expose a publication/version date in the retrieved material, that absence is
recorded instead of being guessed. The PRD Appendix B baseline was accessed on
31 July 2026; this register is the dated revalidation and does not silently
rewrite the approved PRD.

| Source ID | Authority and official source | Publication/version date or current page date | Applicability to this task | Classification | Binding or guidance status | Affected approved IDs |
|---|---|---|---|---|---|---|
| SRC-ZATCA-01 | ZATCA, **E-Invoicing Laws and Regulations** — [official page](https://zatca.gov.sa/en/E-Invoicing/Introduction/LawsAndRegulations/Pages/default.aspx) | Regulations published 4 Dec 2020; page current update 21 Jun 2026 | Legal and program baseline for Saudi e-invoicing | 1 — Official regulatory fact | Binding regulatory baseline and official publication record | KSA-003, KSA-004, KSA-008, BR-002 |
| SRC-ZATCA-02 | ZATCA, **E-Invoicing Implementation Rules and Specifications** — [official page](https://zatca.gov.sa/en/RulesRegulations/Taxes/Pages/ConReqTech.aspx) | Controls, requirements, technical and procedural rules published 19 May 2023 | Current controls/specification family to recheck before design freeze | 1 — Official regulatory fact | Binding/technical requirements as applicable; interpretation still requires qualified review | KSA-003, KSA-004, KSA-008 |
| SRC-ZATCA-03 | ZATCA, **E-Invoice Specifications** — [official page](https://www.zatca.gov.sa/en/E-Invoicing/SystemsDevelopers/Pages/E-Invoice-specifications.aspx) | Data Dictionary and XML Implementation Standard dated 19 May 2023; page current update 12 Jan 2026 | Names, definitions, attributes, XML syntax and business-content references | 1 — Official regulatory fact | Normative technical reference; not a product implementation decision | KSA-003, KSA-004, KSA-008 |
| SRC-ZATCA-04 | ZATCA, **Electronic Invoice XML Implementation Standard v1.2** — [official PDF](https://www.zatca.gov.sa/ar/E-Invoicing/SystemsDevelopers/Documents/20230519_ZATCA_Electronic_Invoice_XML_Implementation_Standard_%20vF.pdf) | Version 1.2, 19 May 2023 | Invoice/credit/debit document categories, structured content and validation references | 1 — Official regulatory fact | Official technical standard; current applicability must be revalidated | KSA-003, KSA-004, KSA-008 |
| SRC-ZATCA-05 | ZATCA, **Electronic Invoice Data Dictionary** — [official specification page](https://www.zatca.gov.sa/en/E-Invoicing/SystemsDevelopers/Pages/E-Invoice-specifications.aspx) and [official XLSX](https://www.zatca.gov.sa/ar/E-Invoicing/SystemsDevelopers/Documents/20230519_EInvoice_Data_Dictionary%20vF.xlsx) | 19 May 2023; page current update 12 Jan 2026 | Field names, definitions and attributes to be mapped only after advisor/scope decisions | 1 — Official regulatory fact | Official technical reference | KSA-003, KSA-004, KSA-008 |
| SRC-ZATCA-06 | ZATCA, **Security Features Implementation Standards v1.2** — [official page](https://zatca.gov.sa/en/e-invoicing/systemsdevelopers/pages/security-requirements.aspx) and [official PDF](https://zatca.gov.sa/ar/E-Invoicing/SystemsDevelopers/Documents/20230519_ZATCA_Electronic_Invoice_Security_Features_Implementation_Standards_vF.pdf) | Version 1.2, 19 May 2023; page current update 31 Jul 2025 | Cryptographic, QR, authentication, key and hashing requirements to be validated before any implementation | 1 — Official regulatory fact | Official technical/security standard | KSA-003, KSA-004, KSA-008 |
| SRC-ZATCA-07 | ZATCA, **FATOORA / E-Invoicing overview** — [official page](https://zatca.gov.sa/en/E-Invoicing/Pages/default.aspx) | Page current update 10 Aug 2026; Phase 1 and Phase 2 program dates shown | Official program boundary and links to current technical material | 1 — Official regulatory fact | Official program information; not a taxpayer-specific determination | KSA-003, KSA-004, BR-002 |
| SRC-ZATCA-08 | ZATCA, **Roll-out phases** — [official page](https://zatca.gov.sa/en/E-Invoicing/Introduction/Pages/Roll-out-phases.aspx?lang=en) | Page current update 1 Aug 2025; Phase 1 enforced 4 Dec 2021; Phase 2 starts in waves from 1 Jan 2023 | Phase 1/Phase 2 distinction and taxpayer-wave notification evidence | 1 — Official regulatory fact | Official program and rollout information | KSA-004 |
| SRC-ZATCA-09 | ZATCA, **E-Invoicing Detailed Guideline** — [official PDF](https://www.zatca.gov.sa/en/E-Invoicing/Introduction/Guidelines/Documents/E-Invoicing_Detailed__Guideline.pdf) | Publication/version date not stated in the retrieved landing metadata; official document retrieved 11 Aug 2026 | Official explanatory guidance on document flow, clearance/reporting, Arabic human-readable forms, correction and operational handling | 1 — Official regulatory fact | Guidance; must not be elevated to an unqualified legal conclusion | KSA-003, KSA-004, KSA-008 |
| SRC-ZATCA-10 | ZATCA, **Wave 25 e-invoicing notice** — [official notice](https://zatca.gov.sa/en/MediaCenter/News/Pages/Wave25-E-invoicing.aspx) | 24 Jul 2026; integration no later than 1 Feb 2027 for the named wave | Current example of official taxpayer-wave evidence, not a Wafra or product rule | 1 — Official regulatory fact | Official notice for the named taxpayers | KSA-004 |
| SRC-ZATCA-11 | ZATCA, **VAT Law and regulations landing page** — [official page](https://zatca.gov.sa/en/RulesRegulations/Taxes/Pages/VATLaw.aspx) | Current official page retrieved 11 Aug 2026; consolidated publication date is not treated as a product decision here | VAT law source to be interpreted by a qualified Saudi tax adviser | 1 — Official regulatory fact | Binding legal source; no applicability conclusion made here | KSA-002, KSA-003, BR-002 |
| SRC-ZATCA-12 | ZATCA, **VAT rate announcement** — [official notice](https://zatca.gov.sa/en/MediaCenter/News/Pages/News-342.aspx) | Published 30 Jun 2020; 15% rate effective 1 Jul 2020; page current update 18 Jun 2026 | Dated evidence for the current standard-rate statement only | 1 — Official regulatory fact | Official public evidence; not a universal tenant tax conclusion | KSA-002 |
| SRC-SDAIA-01 | SDAIA/NDMO, **PDPL and regulations knowledge center** — [official knowledge center](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/) | Current official knowledge center retrieved 11 Aug 2026 | Current location for the PDPL, Implementing Regulation, transfer regulation and official guidance | 1 — Official regulatory fact | Official source index | KSA-006, KSA-007, KSA-008 |
| SRC-SDAIA-02 | SDAIA/NDMO, **Personal Data Protection Law detail** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/PDPL) | Law issued by Royal Decree M/19 dated 9/2/1443 AH; amendment M/148 dated 5/9/1444 AH; current detail retrieved 11 Aug 2026 | Scope, security, breach, rights and impact-assessment legal baseline | 1 — Official regulatory fact | Binding law | KSA-006, KSA-007, KSA-008 |
| SRC-SDAIA-03 | SDAIA/NDMO, **Implementing Regulation of the PDPL** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/PDPL2) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | Notice, rights, minimization, breach, DPIA, DPO, records and processor-contract provisions | 1 — Official regulatory fact | Binding implementing regulation | KSA-006, KSA-007, KSA-008 |
| SRC-SDAIA-04 | SDAIA/NDMO, **Regulation on Personal Data Transfer Outside the Kingdom** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/RegulationonPersonalDataTransferOutsidetheKingdom) | Version 2.0, August 2024 | Transfer conditions, adequacy, safeguards, SCC/BCR and onward-transfer boundary | 1 — Official regulatory fact | Binding regulation | KSA-006, KSA-007, KSA-008 |
| SRC-SDAIA-05 | SDAIA/NDMO, **Risk Assessment Guideline for Transferring Personal Data Outside the Kingdom** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/RiskAssessmentGuideline%20orTransferringPersonalData) | Version/date February 2025 | Processing maps, geography, storage/retention, remote access and disclosure questions for a transfer risk assessment | 1 — Official regulatory fact | Non-binding official guidance | KSA-006, KSA-007, KSA-008 |
| SRC-SDAIA-06 | SDAIA/NDMO, **Standard Contractual Clauses** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/StandardContractualClauses) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | Potential safeguard template and processor/subprocessor/retention/breach questions | 1 — Official regulatory fact | Official safeguard material; legal applicability requires qualified review | KSA-006, KSA-007, KSA-008 |
| SRC-SDAIA-07 | SDAIA/NDMO, **Guidelines for Binding Common Rules** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/GuidelinesforBindingCommonRules) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | Potential group-transfer safeguard only where the facts and adviser confirm it applies | 1 — Official regulatory fact | Official guidance/rules material; not an assumption that BCRs are required or sufficient | KSA-006, KSA-007 |
| SRC-SDAIA-08 | SDAIA/NDMO, **Privacy Policy Guideline** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/ElaborationandDevelopingPrivacyPolicyGuideline) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | Purpose, legal basis, disclosure, geography, retention/destruction, rights and contact topics | 1 — Official regulatory fact | Non-binding official guidance | KSA-006, KSA-007 |
| SRC-SDAIA-09 | SDAIA/NDMO, **Minimum Personal Data Determination Guideline** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/MinimumPersonalDataDeterminationGuideline) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | Necessity and direct-relevance test for collection and processing | 1 — Official regulatory fact | Non-binding official guidance | KSA-006 |
| SRC-SDAIA-10 | SDAIA/NDMO, **Personal Data Destruction, Anonymization and Pseudonymisation Guideline** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/PersonalDataDestruction) | Version 2024, August | Permanent destruction, irreversible anonymization and the continuing personal-data status of pseudonymized data | 1 — Official regulatory fact | Non-binding official guidance that explains law/regulation obligations | KSA-006, KSA-007, KSA-008 |
| SRC-SDAIA-11 | SDAIA/NDMO, **Personal Data Processing Activities Records Guideline** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/PersonalDataProcessingActivities) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | Record content, transfer/recipient fields and record preservation topics | 1 — Official regulatory fact | Non-binding official guidance | KSA-006, KSA-007 |
| SRC-SDAIA-12 | SDAIA/NDMO, **Personal data breach notification service** — [official service](https://dgp.sdaia.gov.sa/wps/portal/pdp/services/personaldatabreachnotification) | Current service retrieved 11 Aug 2026 | Operational route and 72-hour maximum statement where a breach may be harmful | 1 — Official regulatory fact | Official service/instruction; actual incident response requires qualified review | KSA-006, KSA-008 |
| SRC-SDAIA-13 | SDAIA/NDMO, **Appointing a Personal Data Protection Officer** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/AppointingPersonalDataProtectionOfficer) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | DPO trigger categories and duties | 1 — Official regulatory fact | Official rules/guidance; applicability to a particular organization requires review | KSA-006 |
| SRC-SDAIA-14 | SDAIA/NDMO, **Rules Governing the National Register of Controllers Within the Kingdom** — [official detail page](https://dgp.sdaia.gov.sa/wps/portal/pdp/knowledgecenter/details/Rulesandguidelinesp2) | Version/publication date not stated in the retrieved detail metadata; current official detail retrieved 11 Aug 2026 | Controller-registration trigger categories | 1 — Official regulatory fact | Official rules; applicability requires qualified privacy/legal review | KSA-006, KSA-007 |

### Appendix B discrepancy record

The approved PRD Appendix B is still the approved baseline and lists ZATCA
and SDAIA landing pages accessed on 31 July 2026. The current official pages
now expose later page updates and current material, including the ZATCA
overview update of 10 August 2026, technical-page update of 12 January 2026,
security-page update of 31 July 2025, the current Wave 25 notice of 24 July
2026, and the SDAIA/NDMO knowledge-center materials above. This artifact
records the discrepancy and retrieval dates; it does not rewrite or
retroactively alter the approved PRD.

## 3. Approved product traceability: KSA-001–KSA-008 and BR-002

The following are approved reusable product requirements from
docs/MESP_PRD_v1.2.docx, not claims that the law applies identically to every
tenant or that the product is certified. Each row has one classification.

| ID | Approved requirement and evidence boundary | Classification (exactly one) | Current evidence/status | Gate or follow-up |
|---|---|---|---|---|
| KSA-001 | Saudi tenants default to SAR and Asia/Riyadh, with effective-dated multi-currency behavior. | 2 — Approved reusable product requirement | PRD KSA-001; Finance and glossary baselines. | Keep as configurable country-pack/product behavior; MESP-54 remains open for exchange-rate sourcing/update policy. |
| KSA-002 | Saudi country pack seeds the current standard VAT rate where applicable; rates, categories, exemptions, zero-rating and effective dates remain configurable and historical documents reproduce the applied rule. | 2 — Approved reusable product requirement | PRD KSA-002; official 15% notice is evidence only, not a universal applicability rule. | Qualified tax adviser must validate the BRD-safe tax statements and category treatment; no hard-coded statutory scope. |
| KSA-003 | Tax invoices, simplified invoices, credit/debit notes can generate/store required structured data, identifiers, security controls and human-readable Arabic/English forms. | 2 — Approved reusable product requirement | PRD KSA-003; current ZATCA standards and guidance are registered in SRC-ZATCA-02 through SRC-ZATCA-09. | MESP-49 must decide the Release 1 document scope and validate current requirements before implementation. |
| KSA-004 | FATOORA Phase 2 onboarding, clearance/reporting, retry/rejection/status/credential/evidence flows sit behind a versioned Saudi adapter enabled by taxpayer obligation; sandbox/certification and runbook evidence precede enablement. | 2 — Approved reusable product requirement | PRD KSA-004; rollout/wave evidence is current but taxpayer-specific. | MESP-49 and qualified adviser evidence are required; no adapter, credentials or wave logic is implemented here. |
| KSA-005 | The application and reports support Arabic/English and RTL-safe navigation, forms, tables, numbers, dates, search, export and templates. | 2 — Approved reusable product requirement | PRD KSA-005; ADR-011 remains applicable. | Separate generic product localization from statutory invoice-language conclusions; later bilingual and RTL visual evidence is required. |
| KSA-006 | Privacy features cover inventory, purpose limitation, minimization, retention/destruction, subject requests, incidents, controlled disclosure and transfer governance aligned to Saudi PDPL. | 2 — Approved reusable product requirement | PRD KSA-006; current PDPL/NDMO source register is above. | MESP-50 remains open pending qualified privacy/legal advice, owner decisions and data-flow evidence. |
| KSA-007 | Hosting, residency, cross-border processing, backups, subprocessors and support require explicit contractual and architecture decisions with a production data-flow record. | 2 — Approved reusable product requirement | PRD KSA-007; ADR-009 and ADR-014 preserve the open production gates. | No blanket Kingdom-hosting rule is assumed; MESP-50 remains open. |
| KSA-008 | Every posted tax/e-invoice preserves rule version, exchange rate, totals, source document, payload/hash where applicable, response, status and correction chain for reconstruction. | 2 — Approved reusable product requirement | PRD KSA-008; Finance auditability baseline and current ZATCA source family. | Exact retention, payload and archive obligations need qualified tax/privacy/legal validation; no persistence change is made. |
| BR-002 | Release 1 launches a Saudi-ready B2B ERP product with SAR, Arabic/English, RTL, VAT and e-invoicing capability, evidenced by country-pack and bilingual scenarios. | 2 — Approved reusable product requirement | PRD BR-002 and approved BRDs; not a statutory certification statement. | Scenario, owner and external-validation evidence must be attached before any approval or production claim. |

## 4. ZATCA / FATOORA evidence matrix

This matrix separates official program facts from taxpayer applicability,
business decisions, adviser conclusions, implementation design, and later
certification/production evidence. It deliberately does not turn a current
web page into an unconditional product rule.

| Evidence ID | Candidate | Classification (exactly one) | Evidence and boundary | Status |
|---|---|---|---|---|
| ZATCA-001 | Phase 1 is the generation phase; the official rollout page describes generation/storage expectations and the 4 Dec 2021 enforcement date for the named taxpayer population. | 1 — Official regulatory fact | SRC-ZATCA-07 and SRC-ZATCA-08. | Recorded; no tenant applicability inferred. |
| ZATCA-002 | Phase 2 integration is introduced in taxpayer waves from 1 Jan 2023, with ZATCA notification at least six months before the applicable date. | 1 — Official regulatory fact | SRC-ZATCA-08. | Recorded; each taxpayer's notice remains required evidence. |
| ZATCA-003 | Wave 25 notice names taxpayers with VAT-subject revenue above the notice threshold during the stated years and gives an integration date no later than 1 Feb 2027. | 1 — Official regulatory fact | SRC-ZATCA-10, dated 24 Jul 2026. | Current example only; not a Wafra or Release 1 rule. |
| ZATCA-004 | Whether a particular tenant, legal entity or taxpayer is in Phase 1/Phase 2, a wave, a threshold population, or an obligation category. | 3 — Tenant/taxpayer-specific applicability question | Requires the taxpayer's legal identity, VAT status, official ZATCA notification and adviser confirmation. | Unresolved; never hard-code Wafra, revenue, wave, notification date or obligation. |
| ZATCA-005 | The current XML standard names Tax Invoice, Simplified Tax Invoice and associated Credit/Debit Note forms as document categories in its structured invoice model. | 1 — Official regulatory fact | SRC-ZATCA-03 and SRC-ZATCA-04. | Recorded; exact Release 1 scope still needs adviser/owner decision. |
| ZATCA-006 | Which invoice and note types are required for the Release 1 B2B scenario, including whether simplified invoices are in or out of the initial scope. | 5 — Qualified Saudi tax/compliance-advisor validation required | Must be answered against the current taxpayer/customer and transaction model, not inferred from the product menu. | Missing; a required MESP-49 input. |
| ZATCA-007 | The current official guidance distinguishes clearance treatment for Tax Invoices and associated notes from reporting treatment for Simplified Tax Invoices and associated notes. | 1 — Official regulatory fact | SRC-ZATCA-09; the detailed guideline is guidance and must be rechecked against current specifications. | Recorded as source evidence, not as a complete legal interpretation. |
| ZATCA-008 | The exact clearance/reporting applicability, submission timing, exception handling and correction treatment for the Release 1 taxpayer and document set. | 5 — Qualified Saudi tax/compliance-advisor validation required | Requires written adviser answers tied to current official specs and the target taxpayer facts. | Missing; MESP-49 remains open. |
| ZATCA-009 | Current official technical materials provide the field, XML, validation, QR and security reference set. | 1 — Official regulatory fact | SRC-ZATCA-03 through SRC-ZATCA-06. | Recorded; no field map or code is authorized by this artifact. |
| ZATCA-010 | The final field/identifier/Arabic/timestamp/QR/XML/security mapping and any allowed omission or conditionality for Release 1. | 7 — Implementation/design detail—not decided here | Must follow the adviser-approved scope and current versioned specifications. | Unresolved design; do not invent fields, formats, identifiers or security claims. |
| ZATCA-011 | The official detailed guidance describes Arabic human-readable invoice presentation and other operational behaviors such as correction through credit/reissue and handling solution errors. | 1 — Official regulatory fact | SRC-ZATCA-09; guidance status is explicit. | Recorded; current implementation meaning requires MESP-49 review. |
| ZATCA-012 | The product's exact rejection, delay, unavailable, duplicate, correction, retry, escalation, customer-notification and reconciliation behavior. | 7 — Implementation/design detail—not decided here | Requires a validated tax/compliance outcome first, then a bounded design and runbook. | Not designed or implemented. |
| ZATCA-013 | The legal/tax retention and archive period, record form, access/reproduction requirements and interaction with credit/debit correction history. | 5 — Qualified Saudi tax/compliance-advisor validation required | Must be reconciled with privacy/legal retention and MESP-50; no period is guessed here. | Missing; MESP-49 and MESP-50 remain open. |
| ZATCA-014 | Sandbox/certification, credentials, signing keys, integration evidence, taxpayer enablement and production operational-runbook evidence. | 8 — Production/certification evidence required later | ADR-013, ADR-015, KSA-004 and PRD gates require evidence before live enablement. | Explicitly later; no credentials, adapter or production configuration is created. |
| ZATCA-015 | Rechecking official sources immediately before design freeze, certification and each production launch. | 8 — Production/certification evidence required later | PRD Appendix B requires recheck at those gates. | Recorded as a release-control obligation. |

## 5. SDAIA / PDPL evidence matrix

The PDPL material creates a governance and risk-assessment boundary; it does
not decide the deployment architecture or assign all responsibilities to the
software product. The distinction between a legal fact and an applicability
or design question is intentional.

| Evidence ID | Candidate | Classification (exactly one) | Evidence and boundary | Status |
|---|---|---|---|---|
| PDPL-001 | PDPL scope covers processing in the Kingdom and relevant processing of data connected with individuals in the Kingdom; storage is processing, and pseudonymized data remains personal data. | 1 — Official regulatory fact | SRC-SDAIA-02 and the official knowledge-center material. | Recorded; processing inventory still required. |
| PDPL-002 | Controller and processor roles, responsibilities and processor-contract obligations depend on the actual processing arrangement. | 1 — Official regulatory fact | SRC-SDAIA-02 and SRC-SDAIA-03. | Legal baseline recorded; role allocation for MESP is unresolved. |
| PDPL-003 | The MESP Tenant, Company, Branch, Platform Operator, hosting provider, support provider, observability provider, integration provider and subprocessor role allocation. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires an actual data-flow and contractual map, not an assumed SaaS label. | Missing; MESP-50 remains open. |
| PDPL-004 | Notice, purpose, legal basis, disclosure and contact topics are required governance subjects. | 1 — Official regulatory fact | SRC-SDAIA-03 and SRC-SDAIA-08. | Recorded; product notice wording and lawful-basis mapping are not approved. |
| PDPL-005 | The lawful bases, privacy notices, consent/withdrawal treatment and disclosure wording for each Release 1 personal-data processing purpose. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires processing inventory, customer/controller facts and qualified legal review. | Missing; no privacy policy is authored by this task. |
| PDPL-006 | Data-subject rights and response handling are part of the official PDPL/Implementing Regulation baseline; the Implementing Regulation includes a response period and extension conditions. | 1 — Official regulatory fact | SRC-SDAIA-02 and SRC-SDAIA-03. | Recorded; exact service workflow is not implemented. |
| PDPL-007 | The product's identity verification, search, export, correction, objection, restriction, deletion and response workflow for subject requests. | 7 — Implementation/design detail—not decided here | Requires adviser-approved role, basis, exception and evidence rules. | Not designed or implemented. |
| PDPL-008 | The Implementing Regulation states breach-notification timing obligations, including notification within 72 hours where the breach may cause serious harm and notice to the data subject without undue delay where applicable. | 1 — Official regulatory fact | SRC-SDAIA-03 and SRC-SDAIA-12. | Recorded; this is not a complete incident legal assessment. |
| PDPL-009 | Whether a particular incident is reportable, the competent recipient, data-subject notice, cross-border coordination and the MESP escalation path. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires facts, severity assessment, controller role and current official process. | Missing; no breach claim or workflow is made. |
| PDPL-010 | Impact-assessment obligations and triggers exist in the law/regulation. | 1 — Official regulatory fact | SRC-SDAIA-02 and SRC-SDAIA-03. | Recorded; no MESP DPIA conclusion is implied. |
| PDPL-011 | Whether the intended Release 1 processing requires a DPIA, and what its scope, owner, mitigations and approval record must be. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires actual processing, scale, monitoring, sensitive-data and transfer facts. | Missing; MESP-50 remains open. |
| PDPL-012 | DPO appointment trigger categories and DPO duties are described in current official material. | 1 — Official regulatory fact | SRC-SDAIA-13. | Recorded; organizational applicability is unresolved. |
| PDPL-013 | Whether the product operator, a Tenant, a customer legal entity or another controller must appoint a DPO, and what contact/registration evidence is needed. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires entity roles, processing scale, systematic monitoring and sensitive-data facts. | Missing. |
| PDPL-014 | The official controller-register material describes registration trigger categories, including main processing activity, sensitive-data processing and processing beyond personal/family use. | 1 — Official regulatory fact | SRC-SDAIA-14. | Recorded; no registration status is asserted. |
| PDPL-015 | Whether any MESP operating entity or Tenant falls within a controller-registration trigger and which registration evidence is required. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires entity and processing facts. | Missing. |
| PDPL-016 | Minimization and collection of only necessary/directly relevant data are official governance requirements/guidance themes. | 1 — Official regulatory fact | SRC-SDAIA-03 and SRC-SDAIA-09. | Recorded; no field-level data inventory exists in this task. |
| PDPL-017 | The minimum personal-data inventory for Release 1, including accounting, customer, supplier, employee, support, audit, attachment and integration fields. | 7 — Implementation/design detail—not decided here | Must follow a qualified privacy review and approved BRD scope. | Not defined or implemented. |
| PDPL-018 | Destruction must be permanent/irrevocable where applicable; anonymization is irreversible; pseudonymization does not remove personal-data status. | 1 — Official regulatory fact | SRC-SDAIA-10. | Recorded; legal exceptions and technical implementation are open. |
| PDPL-019 | Retention schedules, legal holds, deletion exceptions, backup destruction, anonymization and evidence of destruction for each data class. | 6 — Qualified Saudi privacy/legal-advisor validation required | Must reconcile law, contract, accounting/tax records and MESP-50. | Missing; no one-size-fits-all retention policy is assumed. |
| PDPL-020 | Transfers outside the Kingdom have an official regulation and may involve adequacy, safeguards, SCC/BCR, risk assessment and onward-transfer conditions. | 1 — Official regulatory fact | SRC-SDAIA-04 through SRC-SDAIA-07. | Recorded; actual transfer path and safeguard are unresolved. |
| PDPL-021 | The actual hosting, backup, remote support, observability, subprocessor and integration data flows, countries, access paths, transfer mechanism, TIA and contract controls. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires architecture/vendor facts and qualified advice; no blanket KSA-hosting rule is inferred. | Missing; MESP-50 remains open. |
| PDPL-022 | Whether a particular data category is sensitive and whether Release 1 processes it in a way that changes legal, DPO, DPIA, notice or transfer obligations. | 6 — Qualified Saudi privacy/legal-advisor validation required | Requires the actual data inventory and use cases. | Missing. |
| PDPL-023 | A product or commercial choice to offer KSA-only hosting, regional hosting, customer-selected hosting, or cross-border support. | 4 — Business-owner decision | It may be constrained by law/advice but is not silently selected by this evidence pack. | Unresolved owner decision; MESP-50. |

## 6. FATOORA boundary for the future BRD

The current official material establishes a Phase 1/Phase 2 program boundary
and current taxpayer-wave evidence. It does not establish that Wafra, every
Saudi Tenant, or every Release 1 customer has the same phase, wave, revenue
threshold, notification date or obligation.

The future MESP-37 draft may therefore state only that:

1. Phase 1 and Phase 2 must be modelled as separate concepts.
2. Phase 2 applicability is taxpayer-specific and must be driven by current
   official evidence, including the taxpayer's ZATCA notification where
   applicable. A country pack may support the process, but it must not activate
   an obligation solely from country, revenue, Wafra identity or a hard-coded
   date.
3. The current Wave 25 notice is an example of dated official evidence, not a
   universal rollout rule.
4. Tax Invoice, Simplified Tax Invoice, Credit Note and Debit Note treatment
   must be traced to the current ZATCA standard and adviser-confirmed Release
   1 scope. The product must not invent document timing, fields, QR rules, XML
   structure, security controls, credentials, archive periods or failure
   obligations.
5. The product may later design a versioned adapter boundary, but this task
   creates no adapter, credential, signing key, sandbox registration, XML
   serializer, QR generator, submission endpoint, retry worker or production
   configuration. ADR-015 and MESP-49 remain the gate.
6. Official sources must be rechecked at design freeze, certification and each
   production launch. A dated research pack is not certification evidence.

## 7. VAT evidence and product boundary

ZATCA's dated public notice records a 15% standard VAT rate effective 1 July
2020. That is evidence of the published standard-rate statement; it is not a
conclusion that every Tenant, transaction, supply, exemption, zero-rating,
out-of-scope case or future change has that treatment.

The safe reusable product boundary is the approved KSA-002 requirement:

- seed a country-pack value from dated, revalidated evidence;
- use effective-dated, configurable tax rules and categories rather than a
  source-code constant;
- preserve the applied tax rule/version, rate, evidence reference and totals in
  posted history so an old document can be reconstructed;
- do not decide zero-rated, exempt, out-of-scope, special, transitional or
  future treatment in this task; and
- keep exchange-rate sourcing, update cadence and approval with MESP-54 rather
  than folding it into this regulatory pack.

MESP-49 needs a qualified Saudi tax/compliance answer to the statements that
may safely appear in the BRD. MESP-50 must separately validate privacy and
retention consequences for tax records. No tax configuration, tax engine,
posting rule or historical-data behavior is changed here.

## 8. Privacy, residency and operational data flows

The current PDPL/NDMO evidence supports a processing inventory and a
role-and-flow analysis. It does not create a blanket requirement that all
Saudi customer data, backups, support access, observability, integrations or
subprocessors must be hosted in the Kingdom, nor does it establish that a
cross-border transfer is always prohibited or always permitted.

Before MESP-50 can close, the evidence set must include at least:

- data categories and processing purposes for Tenant, Company, Branch,
  customer, supplier, employee, audit, attachment, tax/e-invoice, support and
  telemetry data;
- controller, processor, joint-controller and subprocessor roles, with the
  actual contract chain;
- collection notices, purposes, legal bases, rights, minimization and any
  sensitive-data determination;
- retention, destruction, anonymization/pseudonymization, accounting/tax
  record exceptions and legal-hold treatment;
- breach detection, controller notification, authority notification and data
  subject communication responsibilities;
- DPO and controller-register applicability;
- hosting, backup, disaster recovery, remote support, observability,
  integrations and onward-transfer geography;
- adequacy/safeguard analysis, TIA where applicable, SCC/BCR choice and
  contract controls; and
- an owner-approved residency/commercial policy that is not presented as a
  legal conclusion.

No provider, region, backup topology, retention duration, purge behavior,
legal hold, support model, transfer mechanism or privacy policy is selected or
implemented by this task. ADR-009, ADR-014, MESP-48 and MESP-50 remain
authoritative gates.

## 9. Arabic, English and RTL boundary

KSA-005 is an approved reusable product requirement for Arabic/English and
RTL-safe application and reporting behavior. ADR-011 remains required; a few
localized labels do not prove Arabic comparison, uniqueness, linguistic
search/sort, fallback, RTL forms, bilingual documents or visual parity.

The current ZATCA detailed guidance also provides official evidence about
Arabic human-readable invoice presentation. That evidence must remain separate
from the product choice of language fallback, translation ownership, field
storage, search behavior, report layout and general RTL implementation. The
future BRD must distinguish:

- generic product Arabic/English and RTL capability (approved product scope);
- statutory invoice-language/format conclusions (current ZATCA evidence plus
  qualified tax/compliance validation); and
- bilingual/RTL signoff and visual regression evidence (later implementation
  and release evidence).

No translation catalog, RTL UI, invoice template or rendering behavior is
changed here.

## 10. MESP-49 gap — Saudi e-invoicing and tax launch scope

**Live status:** To Do.
**Conclusion:** Open and blocking for a fully approved MESP-37 BRD.

The official sources are now dated and traceable, but the evidence needed to
close MESP-49 is absent. At minimum, closure requires:

1. A written answer from a qualified Saudi tax/compliance adviser identifying
   the adviser's identity, qualification, scope, date and sources.
2. An answer for the Release 1 B2B document set, including Tax Invoice,
   Simplified Tax Invoice, Credit Note and Debit Note applicability.
3. Phase 1/Phase 2 and clearance/reporting conclusions tied to actual taxpayer
   facts and official notification evidence, without using Wafra as a legal
   proxy.
4. Current field, identifier, Arabic, timestamp, XML, QR, security, correction,
   rejection, unavailable, duplicate, retention and archive answers.
5. Finance Controller acceptance of the tax/e-invoicing scope and Product Owner
   acceptance of the reusable product boundary.
6. A trace update in MESP-23 that preserves any unresolved question instead of
   marking it resolved by inference.

Until those items exist, MESP-49 remains To Do and KSA-003/KSA-004/KSA-008 are
approved reusable requirements only.

## 11. MESP-50 gap — tenant data residency and retention policy

**Live status:** To Do.
**Conclusion:** Open and blocking for a fully approved MESP-37 BRD.

The official PDPL/NDMO materials support the questions and governance
controls, but there is no qualified privacy/legal adviser evidence or approved
architecture/owner decision for the actual MESP data flows. At minimum,
closure requires:

1. A written qualified Saudi privacy/legal adviser answer with identity,
   qualification, scope, date and sources.
2. A processing inventory and flow diagram covering primary data, backups,
   disaster recovery, support access, observability, integrations and
   subprocessors.
3. Controller/processor role allocation, notice/legal-basis analysis, rights,
   minimization, sensitive-data, breach, DPO and controller-registration
   conclusions.
4. Retention, destruction, anonymization/pseudonymization, legal-hold and
   accounting/tax-record exception decisions.
5. Transfer geography, adequacy/safeguard/TIA/SCC/BCR analysis and contract
   controls where applicable.
6. A Product Owner/Platform Owner decision on commercial residency and support
   posture, clearly labelled as a business decision rather than legal advice.

Until those items exist, MESP-50 remains To Do. ADR-009, ADR-014 and the
MESP-48 production gate are not weakened by this readiness artifact.

## 12. Qualified-advisor and owner question pack

The questions below are ready to send to the appropriate qualified advisers
and owners. An answer must be dated, identify the relevant entity/scenario,
cite the current official sources, state assumptions, and distinguish a legal
conclusion from a recommendation. An unanswered question remains open.

### 12.1 Saudi tax and e-invoicing adviser questions

| Question ID | Question to answer | Classification (exactly one) | Required evidence |
|---|---|---|---|
| TAX-01 | For Release 1 B2B Saudi transactions, which Tax Invoice, Simplified Tax Invoice, Credit Note and Debit Note types are legally required or relevant? | 5 — Qualified Saudi tax/compliance-advisor validation required | Signed/dated adviser memo tied to the target taxpayer and transaction scenarios. |
| TAX-02 | Which Phase 1/Phase 2 obligation applies to each target taxpayer/legal entity, and what official notice or other evidence proves the applicable wave/date? | 5 — Qualified Saudi tax/compliance-advisor validation required | Adviser answer plus taxpayer VAT identity and current ZATCA notification evidence. |
| TAX-03 | For each in-scope document, is the required ZATCA interaction clearance, reporting, generation/storage only, or another current treatment? | 5 — Qualified Saudi tax/compliance-advisor validation required | Source-cited decision table and assumptions. |
| TAX-04 | What current identifiers, invoice fields, Arabic human-readable content, timestamps, XML version, QR content, cryptographic/security controls and correction links are required for the selected scope? | 5 — Qualified Saudi tax/compliance-advisor validation required | Adviser validation against the current Data Dictionary, XML and Security Standards. |
| TAX-05 | What is the compliant behavior for rejection, delay, ZATCA/service unavailability, duplicate submission, correction, cancellation, credit/debit note and reconciliation? | 5 — Qualified Saudi tax/compliance-advisor validation required | Written tax/compliance answer; implementation design follows later. |
| TAX-06 | What retention, archive, reproduction, access and correction-chain requirements apply to the selected invoice and tax-record set? | 5 — Qualified Saudi tax/compliance-advisor validation required | Dated answer that identifies overlap with privacy/legal retention. |
| TAX-07 | Which VAT statements may safely appear in the BRD, and which standard, zero-rated, exempt, out-of-scope, special, transitional or future treatments must remain configurable or unresolved? | 5 — Qualified Saudi tax/compliance-advisor validation required | Signed scope memo; no source-code constant is authorized. |
| TAX-08 | Which values belong in a reusable Saudi country pack and which must be taxpayer/legal-entity configuration driven? | 5 — Qualified Saudi tax/compliance-advisor validation required | Adviser decision table with examples and non-applicability rules. |
| TAX-09 | What onboarding, sandbox, certification, credential, signing-key, operational-runbook and evidence obligations must precede enablement? | 5 — Qualified Saudi tax/compliance-advisor validation required | Adviser answer plus later certification owner/evidence list. |
| TAX-10 | What exact wording may be used for “Saudi-ready,” “e-invoicing capability,” “compliant,” “certified” and “production enabled”? | 5 — Qualified Saudi tax/compliance-advisor validation required | Approved wording memo; no certification claim without formal evidence. |

### 12.2 Saudi privacy and legal adviser questions

| Question ID | Question to answer | Classification (exactly one) | Required evidence |
|---|---|---|---|
| PRIV-01 | For each Release 1 flow, who is controller, processor, joint controller or subprocessor, including SaaS operator, Tenant, customer legal entity, support and observability providers? | 6 — Qualified Saudi privacy/legal-advisor validation required | Dated role map and contract assumptions. |
| PRIV-02 | What legal basis, notice, purpose, disclosure and consent/withdrawal treatment applies to each processing purpose? | 6 — Qualified Saudi privacy/legal-advisor validation required | Processing-purpose/legal-basis matrix and adviser memo. |
| PRIV-03 | Which data-subject rights apply, what identity verification and exceptions are allowed, and who must respond? | 6 — Qualified Saudi privacy/legal-advisor validation required | Rights procedure decision and role owner. |
| PRIV-04 | What breach thresholds, authority notices, data-subject notices, evidence preservation and response times apply to each controller/processor role? | 6 — Qualified Saudi privacy/legal-advisor validation required | Incident legal decision and current official process references. |
| PRIV-05 | Is a DPO required for each relevant entity, and what appointment, contact and duty evidence is needed? | 6 — Qualified Saudi privacy/legal-advisor validation required | Entity-specific DPO applicability memo. |
| PRIV-06 | Is controller registration required for each relevant entity, and what registration/renewal evidence is needed? | 6 — Qualified Saudi privacy/legal-advisor validation required | Entity-specific registration decision. |
| PRIV-07 | What hosting, backup, disaster-recovery, remote-support, observability, integration and subprocessor flows are transfers outside the Kingdom? | 6 — Qualified Saudi privacy/legal-advisor validation required | Data-flow map with countries, access paths and roles. |
| PRIV-08 | For every applicable transfer, what adequacy, safeguard, SCC, BCR, TIA, onward-transfer and contract controls are required? | 6 — Qualified Saudi privacy/legal-advisor validation required | Transfer decision record and TIA/SCC/BCR evidence where applicable. |
| PRIV-09 | Which data categories are sensitive, and do they change notice, DPIA, DPO, registration, retention or transfer obligations? | 6 — Qualified Saudi privacy/legal-advisor validation required | Data classification and impact conclusion. |
| PRIV-10 | What retention, destruction, anonymization/pseudonymization, backup purge and legal-hold rules apply to personal, audit, attachment, tax/e-invoice and support data? | 6 — Qualified Saudi privacy/legal-advisor validation required | Data-class retention/destruction schedule with legal and contractual exceptions. |
| PRIV-11 | Is Kingdom-only hosting legally required, commercially selected, customer-selectable, or unnecessary for each data flow? | 6 — Qualified Saudi privacy/legal-advisor validation required | Legal answer separated from Product/Platform owner decision. |
| PRIV-12 | What privacy-policy content, controller contacts, records of processing, DPIA and audit evidence must be maintained for Release 1? | 6 — Qualified Saudi privacy/legal-advisor validation required | Adviser checklist with accountable owner and review cadence. |

### 12.3 Product and business-owner questions

| Question ID | Question to decide | Classification (exactly one) | Required evidence |
|---|---|---|---|
| OWN-01 | Which Saudi e-invoice document types are in Release 1, subject to the qualified tax answer? | 4 — Business-owner decision | Product Owner and Finance Controller decision referencing TAX-01. |
| OWN-02 | Is taxpayer-specific activation evidence required before a Saudi Company can enable e-invoicing, and who owns the gate? | 4 — Business-owner decision | Product/Finance ownership record referencing TAX-02 and TAX-09. |
| OWN-03 | What customer-facing language is approved for capability, readiness, certification and production enablement? | 4 — Business-owner decision | Owner-approved wording record after adviser review. |
| OWN-04 | What commercial hosting/residency/support options are offered, after the privacy/legal constraints are known? | 4 — Business-owner decision | Product/Platform decision separated from legal advice. |
| OWN-05 | Which bilingual and RTL scenarios must pass product signoff before Saudi launch? | 4 — Business-owner decision | Scenario list, owner and later visual/regression evidence. |

## 13. Exact evidence required before MESP-37 may be activated

MESP-37 may move from To Do to an approved BRD session only when all of the
following are present and current:

1. This artifact is merged and MESP-111 is closed with the exact verdict and
   remaining gaps recorded.
2. The official source register has been rechecked for the date of the MESP-37
   session, with superseded or changed sources recorded rather than silently
   copied into the PRD.
3. TAX-01 through TAX-10 have written answers from a qualified Saudi
   tax/compliance adviser, including identity, qualification, scope, date,
   assumptions and source citations.
4. PRIV-01 through PRIV-12 have written answers from a qualified Saudi
   privacy/legal adviser, including identity, qualification, scope, date,
   assumptions and source citations.
5. Finance Controller, Product Owner and relevant Platform/Privacy owners have
   recorded decisions for the questions assigned to them; an Owner approval
   does not substitute for missing qualified-adviser validation.
6. MESP-49 has explicit closure evidence for the e-invoicing/tax gate, and
   MESP-50 has explicit closure evidence for the privacy/residency/retention
   gate. If either remains open, the MESP-37 BRD must state the bounded draft
   status rather than imply approval.
7. MESP-23 has been updated with the answer, source, owner, date and remaining
   disposition for each related question; unresolved items remain open.
8. Tenant/taxpayer applicability evidence exists for any Phase 2/wave claim;
   no Wafra-specific legal or product implementation shortcut is used.
9. Any later implementation/certification claims have their own evidence plan
   under MESP-48/MESP-49/MESP-50 and ADR-013/ADR-015, with no credentials or
   production configuration in the BRD-only change.

**Current result:** items 1 and the source-register portion of item 2 are
available after this task. Items 3–9 are not yet complete. The next bounded
session is therefore qualified external-validation and owner-decision
handoff; it is not MESP-37 activation.

## 14. Unresolved decisions and preserved gates

| Item | Current state | What this artifact does not decide |
|---|---|---|
| MESP-37 | To Do | Does not activate, approve or draft the Saudi Localization and Compliance BRD. |
| MESP-49 | To Do | Does not close Saudi e-invoice document scope, tax conclusions, clearance/reporting, field/security/correction/retention or certification evidence. |
| MESP-50 | To Do | Does not close data roles, privacy basis, DPO/registration, residency, transfers, retention, purge, legal hold, backup, support or subprocessor policy. |
| MESP-23 | In Progress | Does not mark open questions answered by inference. |
| MESP-53 | To Do | Does not decide report catalogue or reconciliation ownership. |
| MESP-54 | To Do | Does not decide exchange-rate source, cadence or approval. |
| MESP-110 / FIN-OD-09 | To Do | Does not decide Finance year-end, payment-term or posting-dimension policy. |
| MESP-48 | Open production gate | Does not establish supported volume, recovery, RPO/RTO or production infrastructure evidence. |
| ADR-011 | Applicable | Does not establish Arabic comparison/search/index/fallback/RTL parity or visual signoff. |
| ADR-013 | Applicable production gate | Does not select secret/key management or create credentials/signing keys. |
| ADR-014 | Applicable production gate | Does not select retention, legal hold, residency, export or purge behavior. |
| ADR-015 | Applicable future implementation gate | Does not implement a Saudi e-invoicing adapter or assert ZATCA certification. |

## 15. AI-assisted interpretation disclaimer

This is an AI-assisted research, evidence and traceability artifact. It is not
legal advice, Saudi tax advice, privacy advice, an official interpretation of
ZATCA or SDAIA/NDMO material, a certification, a FATOORA approval, a privacy
impact assessment, production-readiness evidence, or MESP-37 approval. Official
pages, regulations, standards and guidance can change. A qualified Saudi tax/
compliance adviser, a qualified Saudi privacy/legal adviser, the Finance
Controller, Product Owner and relevant platform/privacy owners must validate
the facts and decisions for the actual legal entities, tenants, processing
flows and release scope before any BRD approval, implementation, certification
or production enablement.
