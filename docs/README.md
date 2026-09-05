# Mini_ERP_SaaS_Platform — documentation index

Start here. This index classifies every tracked document so current state can be
recovered without reading historical overlays end to end.

## 1. Read these first (live authority)

| Document | Role |
|---|---|
| [`../.ai/CURRENT_STATE.md`](../.ai/CURRENT_STATE.md) | **CURRENT AUTHORITY** block at the top is the single source of live project position |
| [`../.ai/AI_EXECUTION_POLICY.md`](../.ai/AI_EXECUTION_POLICY.md) | Authoritative executor authorization: STOP, Ready, merge, Jira, post-report immutability |
| [`../AGENTS.md`](../AGENTS.md) | Durable working agreement, AI model routing baseline, permanent architecture rules |
| [`../CLAUDE.md`](../CLAUDE.md) | Reading order and bounded execution rules for AI executors |
| [`../TASK.md`](../TASK.md) | The exact bounded session currently handed off |
| [`staticts.md`](staticts.md) | Project statistics and production-readiness tracker (filename spelling is canonical) |
| [`../README.md`](../README.md) | Product overview, stack, capability matrix, quick start |

Live Jira and live GitHub outrank every Markdown file for mutable facts. In each
of the files above, sections under a `HISTORICAL RECORD` divider are preserved
evidence, not current authority — even where their own text says "current".

## 2. Architecture decision records

| ADR | Subject |
|---|---|
| [ADR-002](ADR-002_Backend_Project_Structure_and_Module_Enforcement.md) | Backend project structure and module enforcement |
| [ADR-004](ADR-004_Identity_Cookie_Server_Session_Antiforgery_Context_Resolution.md) | Identity, cookie/server session, antiforgery, context resolution |
| [ADR-006](ADR-006_Module_Schemas_EF_Core_Migrations_Transactions.md) | Module schemas, EF Core migrations, transactions |
| [ADR-007](ADR-007_Internal_Events_Transactional_Outbox_Inbox.md) | Internal events, transactional outbox/inbox |
| [ADR-008](ADR-008_SQL_Background_Workers_and_Ownership.md) | SQL background workers and ownership |
| [ADR-009](ADR-009_Private_Object_Storage_Boundary.md) | Private object storage boundary |
| [ADR-018](ADR-018_Testing_Environments_SQL_Server_Containers_and_Gates.md) | Testing environments, SQL Server containers, gates |
| [ADR-019](ADR-019_Tenant_Host_Resolution_Workspace_Context_and_Branding.md) | Tenant host resolution, workspace context, branding |

ADR numbering is intentionally sparse; absent numbers were never issued.

## 3. Capability architecture records

Written per merged capability, newest last.

- [34 — MESP-131 moving-weighted-average valuation](34_MESP-131_MWA_Valuation_Architecture.md)
- [35 — MESP-132 Finance foundation](35_MESP-132_Finance_Foundation_Architecture.md)
- [36 — MESP-133 AP / AR / cash settlement](36_MESP-133_AP_AR_Cash_Settlement_Architecture.md)
- [37 — MESP-134 tax, FX, reporting currency, revaluation](37_MESP-134_Tax_FX_Reporting_Currency_Revaluation_Architecture.md)
- [38 — MESP-135 Finance close, corrections, reconciliation, core reports](38_MESP-135_Finance_Close_Corrections_Reconciliation_and_Core_Reports_Architecture.md)

No architecture record exists yet for MESP-136, MESP-137 or MESP-138.

## 4. Release 1 scope, plan and approved decisions

- [30 — Release 1 full-feature fast-track delivery plan](30_Release_1_Full_Feature_Fast_Track_Delivery_Plan.md)
- [31 — Release 1 consolidated Owner decision pack](31_Release_1_Consolidated_Owner_Decision_Pack.md)
- [32 — Release 1 tax / VAT scope clarification](32_Release_1_Tax_VAT_Scope_Clarification.md)
- [33 — MESP-116 approved decision and dependency map](33_Release_1_MESP_116_Approved_Decision_and_Dependency_Map.md)
- [27 — Saudi localization scope rebaseline](27_Release_1_Saudi_Localization_Scope_Rebaseline.md)
- [Decisions register](Decisions.md)
- [94 — Product delivery master plan](94_Product_Delivery_Master_Plan.md)

## 5. Business requirement documents (approved baselines)

Foundation and platform: [11 Platform Administration](11_SaaS_Platform_Administration_BRD.md) ·
[12 Identity and Access](12_Identity_and_Access_BRD.md) ·
[13 Multi-Tenancy](13_Multi_Tenancy_BRD.md) ·
[14 Organization and Company Structure](14_Organization_and_Company_Structure_BRD.md) ·
[29 Security, Audit and Data Governance](29_Security_Audit_and_Data_Governance_BRD.md)

Business domains: [16 Master Data and Product Catalog](16_Master_Data_and_Product_Catalog_BRD.md) ·
[21 Procurement and Purchase-to-Pay](21_Procurement_and_Purchase_to_Pay_BRD.md) ·
[22 Inventory and Warehouse Management](22_Inventory_and_Warehouse_Management_BRD.md) ·
[23 Finance and Accounting](23_Finance_and_Accounting_BRD.md) ·
[24 Sales and Order-to-Cash](24_Sales_and_Order_to_Cash_BRD.md) ·
[25 Reporting and Analytics](25_Reporting_and_Analytics_BRD.md) ·
[28 Saudi Localization](28_Release_1_Saudi_Localization_BRD.md)

## 6. Domain and design references

[00 Business glossary](00_ERP_Business_Glossary.md) ·
[01 Business vision](01_Business_Vision.md) ·
[01 Technology architecture baseline](01_Technology_Architecture_Baseline.md) ·
[02 Domain model](02_ERP_Domain_Model.md) · [03 ERD](03_ERD.md) ·
[04 Procurement](04_Procurement.md) · [05 Inventory](05_Inventory.md) ·
[06 Sales](06_Sales.md) · [07 Finance](07_Finance.md) ·
[08 State machine](08_State_Machine.md) · [09 Module boundaries](09_Module_Boundaries.md) ·
[10 Non-functional requirements](10_Non_Functional_Requirements.md)

## 7. Implementation specifications and readiness records

- [15 — Foundation Release 1 lean implementation specification](15_Foundation_Release_1_Lean_Implementation_Specification.md)
- [17 — Master Data lean implementation specification](17_Master_Data_and_Product_Catalog_Lean_Implementation_Specification.md)
- [18 — Product identity readiness](18_Product_Identity_M95_SL_03_Readiness.md)
- [19 — Supplier readiness](19_Supplier_M95_SL_04_Readiness.md)
- [20 — Business Customer readiness](20_Business_Customer_M95_SL_05_Readiness.md)
- [26 — Saudi regulatory evidence and external validation readiness](26_Saudi_Regulatory_Evidence_and_External_Validation_Readiness.md)
- [MESP-143 — Tenant-aware entry execution plan](MESP-143_Tenant_Aware_Entry_Execution_Plan.md)

## 8. Historical review and checkpoint records

Dated evidence from completed reviews. Not current authority.

[90 Founder decision pack](90_MVP_Founder_Decision_Pack.md) ·
[91 Jira simplification](91_Jira_Simplification_Update.md) ·
[92 MESP-27 founder review](92_MESP27_Founder_Review.md) ·
[93 Wave 1 backlog](93_MESP27_Wave1_Implementation_Backlog.md) ·
[95 Foundation backend review](95_Foundation_Backend_Review_Checkpoint.md) ·
[96 Foundation safety validation](96_Foundation_Release1_Safety_Validation.md) ·
[97 Foundation completion review](97_Foundation_Completion_Review_Checkpoint.md) ·
[98 Independent Opus 5 checkpoint reconciliation](98_Independent_Opus_5_Checkpoint_Reconciliation.md) ·
[99 Independent Opus 5 Finance BRD reconciliation](99_Independent_Opus_5_Finance_BRD_Reconciliation.md) ·
[100 Pre-MESP-38 independent review reconciliation](100_Pre_MESP_38_Independent_Review_Reconciliation.md)

## 9. Note on the approved PRD

The canonical approved PRD is the binary `MESP_PRD_v1.2.docx`. Older filenames
referring to it name the same unchanged file.
