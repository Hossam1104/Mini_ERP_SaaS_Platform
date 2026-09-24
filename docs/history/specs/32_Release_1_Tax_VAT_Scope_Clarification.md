# Release 1 Tax/VAT Scope Clarification

**Status:** Current Release 1 scope addendum; implementation **Not Started**
**Date:** 12 August 2026
**Product Decision:** PD-024 (explicit Owner fast-track direction)
**Related Jira:** MESP-22, MESP-23, MESP-49, MESP-54, MESP-110, MESP-116, MESP-119, MESP-134
**Related plan:** `docs/30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md`

## 1. Decision and boundary

Internal, reusable, configuration-led Tax/VAT capability is a **Release 1
requirement**. The capability is needed by Master Data, Procurement, Sales,
Finance, returns, credit notes, reporting, migration, and accounting
reconciliation. It is not a separate statutory-compliance product and it does
not authorize external integration.

This clarification is the current scope addendum for the Release 1 fast-track
plan. It does not rewrite or delete approved historical BRDs, PD-023, or the
existing MESP-49 external statutory/e-invoice disposition. PD-023 remains
immutable. MESP-49 remains Done for the existing Release 1 external boundary;
PD-024 separately restores the internal engine.

The restored scope is **not** any of the following:

- ZATCA/FATOORA connectivity, submission, clearance, signing, certification,
  government reporting, or e-invoicing integration;
- a taxpayer-applicability or statutory legal-compliance determination;
- an external tax authority validation or production certification;
- a payment gateway, bank feed, external FX provider, external SSO, webhook,
  provider, credential, or production-infrastructure feature; or
- Saudi/Wafra-specific hard-coded behavior.

## 2. Required internal capability

The Release 1 internal contract must support the following business concepts,
subject to explicit detailed decisions recorded before implementation:

1. **Tax identities and categories:** Tenant-safe tax identity, category,
   code, label, direction (purchase/sales), and applicability configuration.
2. **Versioned effective rates:** One unambiguous effective version for a
   scope/date, with effective-from/effective-to, historical preservation,
   permission, and audit. A later rate must not rewrite a posted document.
3. **Purchase/sales applicability:** Explicit applicability and exemption/
   non-taxable treatment where the internal product contract requires it;
   no legal conclusion is inferred from the label.
4. **Taxable base:** A deterministic, auditable base derived from the
   approved document lines and allowed charges/discounts. The base and
   rounding evidence remain visible.
5. **Calculation:** A reusable calculation contract used consistently by
   Procurement, Sales, Finance, returns, and Credit Notes. Inclusive versus
   exclusive presentation is an explicit Owner/Finance decision and must not
   be invented as a hidden default.
6. **Applied-rate evidence:** Each transaction preserves the applied tax
   identity, category/code, rate version, base, amount, currency, rounding,
   and calculation inputs sufficient to reproduce the business result.
7. **Accounting posting:** Finance owns the account mapping and posting of
   internal tax amounts, with balanced source-to-GL evidence, reversal and
   correction behavior, period controls, and reconciliation.
8. **Returns and credits:** Supplier Returns, Customer Returns, Purchase
   Invoice corrections, Sales Credit Notes, cancellations, and reversals
   carry the appropriate linked tax consequence without rewriting history.
9. **Reporting support:** Finance, tax-support, sales, procurement, audit,
   reconciliation, and transaction reports can expose configured tax
   identity, base, rate, amount, currency, and applied evidence under
   authorized filters and bilingual presentation.
10. **Migration:** MESP-40 can validate and quarantine tax identities,
    categories, codes, effective versions, opening balances, and historical
    references without destructive overwrite.

## 3. Tenant, country-pack, and security requirements

Tax configuration is owned inside the Tenant and may be scoped to the approved
Company/Legal Entity, Branch, product, supplier/customer, transaction type,
or other explicit business boundary. There is no cross-Tenant sharing.

The capability must be country-pack compatible and generic. A Saudi-oriented
default may be configured through the approved localization boundary, but the
engine must not hard-code Saudi, Wafra, a legal rate, or a statutory filing
rule. The server derives Tenant and authorization context. Tax mutation,
override, posting, correction, and reporting actions require permission,
SoD where applicable, and business audit.

## 4. Ownership and implementation boundary

| Concern | Release 1 owner/boundary |
|---|---|
| Tax identity, category, code, rate version, applicability configuration | Master Data, subject to approved contract and audit. |
| Tax calculation on documents | Shared domain contract consumed by Procurement and Sales; no client-side authority. |
| Applied tax evidence and document snapshot | Owning operational document plus Finance-relevant immutable evidence. |
| Tax account mapping and posting | Finance; balanced source-to-GL and reconciliation. |
| Return/Credit Note consequence | Owning Sales/Procurement document flow with Finance posting/reversal. |
| Tax reporting support | Reporting consumes owned, authorized source evidence; no unaudited duplicate formula. |
| Migration and onboarding | MESP-40/MESP-141 after its own readiness and production gates. |
| Statutory/external submission or certification | Outside Release 1; future-release and external/legal gate. |

## 5. Business acceptance boundary

At implementation readiness, the capability must be able to demonstrate, in
the real codebase and without external credentials:

- a Tenant-authorized user configures a tax identity and effective version;
- a purchase and a sales document select the correct internal tax contract by
  configured applicability and preserve the applied rate evidence;
- a correction or later rate does not rewrite a posted historical document;
- a Supplier Return and Customer Return/Credit Note carry an auditable,
  linked tax consequence;
- Finance receives balanced, authorized tax postings and can reconcile them;
- tax data is visible in authorized bilingual reports/exports with the
  applied identity, base, rate, amount, currency, and source lineage; and
- invalid, overlapping, unauthorized, expired, or missing configuration is
  rejected or held with a useful bilingual error and audit event.

These are business boundaries, not permission to skip implementation tests,
SQL validation, Tenant isolation, accounting reconciliation, or production
gates.

## 6. Open decisions retained

PD-024 restores the capability category only. The following remain explicit
decision items in the consolidated pack and must not be inferred:

- inclusive versus exclusive presentation and calculation mode;
- exact taxable-base treatment for discounts, charges, freight, and rounding;
- exemption/non-taxable internal categories and their accounting behavior;
- account mapping defaults, period behavior, and correction/reversal policy;
- report catalogue, scheduling, export, freshness, and retention boundary;
- migration source formats, validation/quarantine, and historical evidence;
- any statutory field needed only for a later external/legal integration.

The detailed positions are in
`docs/31_Release_1_Consolidated_Owner_Decision_Pack.md`. The applicable
internal Release 1 contract positions were approved by Hossam in MESP-116
comment `10957` and recorded as PD-025 through PD-046 in MESP-22 comment
`10958`. Approval is bounded by the exact row text and by the specialist
validation and production gates recorded there; it does not approve the
remaining Tax/VAT implementation details below, statutory behavior, or an
external integration.

## 7. Current classification and traceability

- `M95-SL-08 Tax` is reclassified from deferred/out-of-Release-1 to
  **Release 1 required — Not Started**.
- MESP-119 owns the Master Data Tax/VAT master and engine contract.
- MESP-134 owns Finance tax accounting, transaction currency, FX, Reporting
  Currency, and revaluation integration.
- MESP-125/126/127, MESP-136/137/138, and MESP-139 consume the contract for
  procurement, matching, returns, sales, credits, and reporting.
- MESP-141 consumes the contract for migration/onboarding validation.
- MESP-49 remains the external statutory/e-invoice boundary; MESP-39 remains
  the future external integration BRD and is not executed.
- MESP-23 remains the living register; this clarification itself does not
  close a row. MESP-116 reconciled the applicable approved contract rows and
  preserved the remaining Tax/VAT detail decisions as implementation and
  specialist-validation work.

No Tax/VAT source, test, entity, table, migration, API, UI, provider,
credential, infrastructure, or production configuration was added by this
scope clarification.

## 8. MESP-116 approval overlay

MESP-116 approved the following bounded Release 1 contract positions relevant
to this clarification:

- **B3 / PD-043 — Currency and FX:** internal manual/configured currency and
  FX policy remains contract-bound to Finance/Reporting ownership. No
  automated FX source, bank feed, external provider, or production decision is
  implied.
- **B6 / PD-046 — Finance posting and valuation:** internal tax amounts and
  related posting/valuation evidence remain Finance-owned, balanced,
  traceable, reversible, and subject to Finance and named specialist
  validation. No irreversible accounting or cutover decision is approved by
  the governance session.
- **PD-024 / internal Tax/VAT:** reusable configuration-led Tax/VAT remains a
  Release 1 requirement and is still **Not Started**. It is not statutory
  compliance, ZATCA/FATOORA behavior, legal advice, certification, filing,
  clearance, signing, submission, or external-provider integration.

Before implementation or any production/irreversible accounting decision,
Finance, Inventory/operational owners where affected, Reporting, and
Migration specialists must validate the detailed contract, including
inclusive/exclusive treatment, taxable base, exemption behavior, account
mapping, period/correction policy, report evidence, migration quarantine, and
historical traceability. MESP-40 remains To Do and unactivated; this approval
does not authorize migration execution. C2/C3 and all other C1-C9 gates remain
open.
