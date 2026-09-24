# Architecture Enforcement — PROPOSED (ADOPTED 2026-09-25)

> **ADOPTED 2026-09-25 (Q-E).** Ratchets R2–R5 were installed in
> `backend/tests/MiniErp.ArchitectureTests/ModuleBoundaryTests.cs` by commit `0d5fa4d`. The backend
> gate passed at 1551/1551. The three two-way module pairs are frozen. The live rules are summarized
> in [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) §7. MESP-154 (#269) tracks reducing the exceptions.
> The status note below is the original proposal text.


> **Status: PROPOSED. Nothing is installed.** No test, package, or CI change has been made.
> Adoption needs an owner decision (see drift-report Q5). After that, a bounded executor task will add the rules to `backend/tests/MiniErp.ArchitectureTests`. No new tool or package is needed, because the existing xUnit plus source-scan pattern in `ModuleBoundaryTests.cs` already covers every rule below.

## Actual layering, as measured 2026-09-24 @ `d94cc3c`

```
MiniErp.Api ──► MiniErp.Infrastructure ──► MiniErp.App ──► MiniErp.Contracts
     └──────────────────────────────────────►┘ (host composition)
Infrastructure/Persistence/Modules/<M>/   one internal DbContext per module (7) + TenantPersistenceDbContext
App/Modules/<M>/                          12 module folders; cross-module calls via C# namespace imports
```

## Rules

| ID | Rule | Today | Status |
|---|---|---|---|
| R1 | The project graph follows ADR-002: Contracts has no references, App→Contracts, Infrastructure→App+Contracts, Api→all three, no cycles. App and Api have no EF Core. | Holds | **Already enforced.** `ModuleBoundaryTests.cs` (`Project_reference_direction_matches_adr_002`, `Known_project_dependency_graph_has_no_cycle`, `Application_and_api_do_not_reference_entity_framework_core`, and others). No change. |
| R2 | An App module may import another App module's namespace (`using MiniErp.App.Modules.<X>`) **only** if that edge is in the frozen allowlist below. The allowlist may only **shrink**: the test fails on any new edge and also fails if a listed edge has disappeared but was not removed from the list. | 25 edges, 3 of them bidirectional | **New (ratchet).** |
| R3 | A module's `Infrastructure/Persistence/Modules/<M>/` code must not reference another module's DbContext or persistence namespace. The only exceptions are the composition files `Persistence/DevelopmentSqlServerDatabaseMigrator.cs` and `Persistence/SqlServerDesignTimeDbContextFactories.cs`. | 0 violations | **New (zero-tolerance).** This locks in ADR-006 ("direct cross-module DbContext access is prohibited"), which no test currently guards. |
| R4 | Unscoped EF calls (`IgnoreQueryFilters`, `FromSql*`, `ExecuteSql*`) must be on a named allowlist of **4** call sites that may only shrink. In addition, every allowlisted `ExecuteSql*` string must contain `[TenantId] = {` and `WITH (UPDLOCK`. | 4 sites, all compliant | **Partly enforced.** The count is already frozen at `ModuleBoundaryTests.cs:353-358`. The **Tenant predicate / lock-only content check is new**. |
| R5 | Every class decorated `[Collection(SqlServerSafetyCollection.Name)]` must have a name ending in `SqlServerSafetyTests`, because hosted CI excludes these classes by name (`.github/workflows/ci.yml:86`). | 13/13 comply | **New (zero-tolerance).** It stops a LocalDB test from running, and failing, on hosted Windows CI. |
| R6 | `AGENTS.md` must stay at the repository root. | Holds | **Already enforced implicitly.** `MigrationFoundationTests.cs:1083` finds the repo root by `AGENTS.md`. Documented here so nobody moves it. |

### R2 frozen allowlist (named exceptions, shrink-only)

These are importer → imported edges, with the number of files carrying each import.

| # | Edge | Files | Note |
|---|---|---|---|
| E01 | BusinessParties → MasterData | 6 | **bidirectional with E09** |
| E02 | Finance → Identity | 1 | |
| E03 | Finance → Inventory | 1 | |
| E04 | Finance → Sales | 1 | **bidirectional with E22** (`FinanceCustomerReturnApplicationContracts.cs:6`) |
| E05 | Inventory → MasterData | 2 | |
| E06 | Inventory → Procurement | 5 | |
| E07 | Inventory → Sales | 3 | **bidirectional with E23** |
| E08 | MasterData → Audit | 1 | |
| E09 | MasterData → BusinessParties | 3 | **bidirectional with E01** |
| E10 | Migration → Audit | 3 | |
| E11 | Migration → Finance | 4 | Execution coordinators, all by design |
| E12 | Migration → Inventory | 1 | |
| E13 | Notifications → Audit | 1 | |
| E14 | Procurement → BusinessParties | 2 | |
| E15 | Procurement → MasterData | 4 | |
| E16 | Reporting → Audit | 1 | |
| E17 | Reporting → Finance | 1 | |
| E18 | Reporting → Inventory | 1 | |
| E19 | Reporting → Procurement | 1 | |
| E20 | Reporting → Sales | 1 | |
| E21 | Sales → BusinessParties | 1 | |
| E22 | Sales → Finance | 2 | **bidirectional with E04** |
| E23 | Sales → Inventory | 1 | **bidirectional with E07** |
| E24 | Sales → MasterData | 1 | |
| E25 | Sales → Procurement | 2 | |

The ratchet counts **edges**, not files. An executor must re-measure these edges when installing the rule and must stop if its count differs from this table. The three bidirectional pairs are the removal candidates, but removal is the owner's call (Q5) and is **not** proposed as work now.

## Sketch of the new rules (not installed)

R2, R3 and R5 reuse the existing source-scan helper style in `ModuleBoundaryTests.cs`:

```csharp
// R2 — shrink-only App module edge ratchet
[Fact]
public void App_module_imports_match_frozen_allowlist()
{
    var actual = ScanAppModuleImports();            // set of "From->To" from `using MiniErp.App.Modules.X;`
    Assert.Empty(actual.Except(AllowedModuleEdges)); // no new edge
    Assert.Empty(AllowedModuleEdges.Except(actual)); // removed edge must be deleted from the list (ratchet down)
}
```

R4 content check: for each allowlisted `ExecuteSqlInterpolatedAsync` call, the interpolated string must contain both `[TenantId] = {` and `WITH (UPDLOCK`.

## Exception policy

- An exception is named (E-id / site), comes with a reason, and **may only shrink**.
- Adding an exception needs an explicit owner-approved task that cites this file. An executor must never widen a list to make the build green. Slice 11 did exactly that with R4 (1→4 sites), and it goes on PR #262's review list.
