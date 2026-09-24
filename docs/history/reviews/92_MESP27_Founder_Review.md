# MESP-27 Founder Review — Corrected SaaS Platform Administration BRD v0.10

## 1. Independent audit correction confirmation

All 16 Claude Opus Independent Approval Audit corrections have been applied to `docs/11_SaaS_Platform_Administration_BRD.md`. The corrected BRD is version **v0.10** with status **Ready for Founder Approval after Independent Audit Corrections**.

The corrections restore the PRD NFR targets, complete traceability, tighten provisioning/configuration/module/support/export/purge controls, add the non-production Restricted Validation Plan, close undefined Entitlement-override behavior, and update the controlled glossary and Founder Decision Pack. No application code, implementation Story, MESP-28 work, or Retail POS scope was created.

## 2. Approved MESP-52 decision

Hossam has approved:

- One production Release 1 Plan containing all approved B2B ERP modules.
- Simple configurable limits.
- Manual Plan assignment by the Platform Administrator.
- Effective-dated and audited Plan, Subscription, and Entitlement changes.
- No metered billing, automated subscription invoice, automated pricing engine, overage billing, payment, or accounting transaction.
- One non-production Restricted Validation Plan solely for Entitlement-denial evidence; it cannot be sold or assigned in production.

## 3. Approved Trial exclusion

Trial Tenants and a Trial lifecycle state are excluded from Release 1. The Restricted Validation Plan is a non-production control and is not a Trial offering.

## 4. Approved prohibition of Entitlement override

Per-Tenant Entitlement override is prohibited. Entitlements change only through a versioned Plan change or effective-dated Subscription change. A security or operational-safety restriction may temporarily block access but cannot grant a capability absent from the effective Plan and Subscription.

## 5. Approved Plan metadata

Each Plan records its service/support tier, non-calculating price metadata, and effective dates. These attributes generate no charge, payment, subscription invoice, or accounting transaction.

## 6. Approved purge-certificate wording

The purge certificate states:

- Certified purge scope.
- Systems and data included and excluded.
- Residual backups or retained copies.
- Legal-hold or retention restrictions.
- Whether restoration remains possible outside the certified purge scope.

It must not claim that restoration is universally impossible unless all residual copies are demonstrably removed. Purge execution also requires the MESP-50-controlled cooling-off interval and final notice.

## 7. Approved multiple-legal-entity decision

A Tenant may contain multiple legal entities. Each legal entity owns its legal and accounting boundary. Release 1 excludes financial consolidation, intercompany automation, elimination entries, transfer pricing, and consolidated statements. MESP-30 retains the detailed operating rules.

## 8. Remaining MESP-48 evidence

Wafra must provide current, expected 12-month, and credible peak values with sources for users, legal entities, branches, warehouses, products, suppliers, Business Customers, documents by type, lines per document, monthly/peak transactions, attachments/storage, imports, exports, reports, jobs, integrations/API use, concurrent sessions, and seasonal peaks.

The approved PRD targets remain: 99.9% monthly availability; RPO no more than 15 minutes; RTO no more than 4 hours; common-read p95 no more than 2 seconds; and common-command p95 no more than 3 seconds. MESP-48 supplies the reference load used to validate them; it does not weaken them.

## 9. Remaining MESP-50 production decisions

Before production, qualified owners must approve hosting/data region, permitted cross-border support access, subprocessor consent/restrictions, retention periods, legal-hold authority, backup/residual-copy treatment, purge scope, purge cooling-off duration, final-notice method, certificate retention, and whether/when restoration is possible outside the certified purge scope.

## 10. Risks

- Missing MESP-48 evidence could produce arbitrary limits or unvalidated capacity.
- Standing or export-capable support access could create a cross-Tenant/privacy incident; separate Tenant-approved export authority is mandatory.
- Export waiver plus artifact expiry could leave no accepted recoverable Tenant copy before purge.
- Residual backups could make an absolute “restoration impossible” certificate untrue.
- Single-founder role concentration requires immutable evidence, dual control for purge, and external specialist validation before production.
- Trial or Retail POS scope could leak through packaging unless the explicit exclusions remain enforced.

## 11. Founder approval block

I confirm that the corrected MESP-27 BRD v0.10 applies all 16 Independent Approval Audit corrections.

I confirm the approved MESP-52 decision, Trial exclusion, Entitlement-override prohibition, Plan metadata, purge-certificate wording, and multiple-legal-entity decision exactly as summarized above.

I accept that MESP-48 evidence and MESP-50 production decisions remain open at their stated gates.

| Field | Founder response |
|---|---|
| Approve corrected MESP-27 BRD v0.10 | Yes |
| Confirm approved MESP-52 decision | Yes |
| Confirm Trial Tenant exclusion | Yes |
| Confirm Entitlement-override prohibition | Yes |
| Confirm Plan metadata decision | Yes |
| Confirm purge-certificate wording | Yes |
| Confirm multiple-legal-entity decision | Yes |
| Accept remaining MESP-48/MESP-50 gates | Yes |
| Approved by | Hossam |
| Date | 1 August 2026 |
| Requested changes | None |

MESP-27 Founder Review is **signed** by Hossam as of 1 August 2026. MESP-27 moves to Done and MESP-28 begins only after the Jira update in Appendix A of `docs/11_SaaS_Platform_Administration_BRD.md` is completed. Do not create implementation Stories.
