# AGENTS.md — PayCalc24 v0.3

## Mission
Implement PayCalc24 according to `/docs` and `IMPLEMENTATION_PLAN.md`. Prefer correctness, traceability and generic payroll primitives over TS24-specific shortcuts.

## Non-negotiable architecture rules
1. Target .NET 9. Primary desktop UI is Avalonia on .NET 9; use Actipro Avalonia controls where they materially improve UX/productivity.
1a. Primary relational database is MariaDB through the approved EF Core provider. MariaDB-specific persistence logic stays in Infrastructure.
2. Reuse/integrate TS24 Company/User/Permission core through abstractions; do not duplicate identity/company management.
3. Never hard-code TS24 payroll policies. `P1`, `P2`, `P3`, department names, allocation percentages, 70/100/120 thresholds and similar values are configuration/reference data.
4. Dynamic Pay Components must not become columns or enums in the transaction schema.
5. Use `decimal` for money, percentages and payroll calculations; never `float`/`double`.
6. All policy-affecting master data supports effective dating and versioning.
7. Locked payroll periods are immutable. Corrections create adjustments/recalculation versions.
8. Calculation Engine must be deterministic and testable without database/network access. Same input snapshot + policy/formula/parameter versions => same result.
9. Formula Engine uses a safe DSL/AST. Never execute arbitrary C#, SQL, JavaScript, shell or user-provided code.
10. Integrations use canonical provider interfaces. Core must not reference Odoo/iBHXH/TaxOnline/ezBooks implementation types.
11. Every material calculation must be explainable: inputs, formula/rule, intermediate values, result and versions.
12. Every implemented Business Rule must have automated tests.
13. Do not physically delete data already referenced by a calculation/audit record.
14. Company isolation is mandatory on every query/command. Never trust a client-supplied CompanyId without validated Company Context.
15. JSON is appropriate for Formula AST, Explain payloads and flexible definitions; core transactional relationships remain relational.
16. API writes that can be retried (imports, calculate, provider pushes) must support idempotency/correlation.
17. UI must be configuration-driven too: do not assume P1/P2/P3, three KPI tiers, 22 workdays, or specific statutory providers.
18. Do not silently substitute missing statutory data with zero. Use explicit unavailable/pending/error status.
19. Preserve historical versions. Do not update a version already used by a locked/approved calculation.
20. Architecture changes require a short ADR or explicit note in the PR/task output.
21. UI must support Windows, macOS ARM64/x64 and Linux.
22. UI/ViewModels contain no payroll business rules and never access MariaDB directly; use application/API contracts.
23. Use dynamic columns/templates for Pay Components and KPI models; never hard-code P1/P2/P3 UI columns.
24. Use relational schema for payroll transactions. JSON is limited to genuinely dynamic structures (Formula AST, Explain, report definitions, parameter overrides, provider configuration).
25. Use MariaDB-appropriate indexes for high-volume tables, normally beginning with CompanyId where company-scoped.
26. UUID storage strategy must be chosen once by ADR (CHAR(36) for simplicity or BINARY(16) for compactness) and used consistently.
27. MariaDB JSON/provider limitations must not leak into Domain or CalculationEngine.


## Modular architecture rules
28. Build PayCalc24 as a modular monolith. Modules expose explicit Contracts; do not reference another module's persistence entities, DbContext mappings or internal services.
29. Prefer replacement through interfaces/registries over modification of unrelated modules. A new provider/function/renderer should normally be added by implementing a contract and registering it.
30. `FormulaEngine` is an independently testable library. It must not depend on Avalonia, MariaDB, HTTP, TS24 Core, or provider implementations.
31. Attendance and Performance engines emit normalized/derived Payroll Inputs. Core compensation calculation consumes the Payroll Input Ledger rather than reaching back into Attendance/KPI storage.
32. Formula functions implement an extension contract (for example `ICalculationFunction`) and are discovered through an explicit registry. Do not implement a giant switch that requires editing the engine for every new function.
33. Integration providers implement canonical interfaces such as Employee, Attendance, Insurance, Tax and Accounting providers. Provider-specific models stay in provider adapters.
34. Module-to-module workflows go through application contracts/events/DTOs, not direct table access.

## Dynamic Formula Repository rules
35. MariaDB is the repository for formula metadata, source text, canonical AST JSON, parameters, lookup tables, dependencies, versions, lifecycle and test cases. MariaDB does not execute arbitrary payroll formulas.
36. Formula execution occurs in .NET using a safe decimal-only evaluator/compiler. Never generate dynamic SQL/stored procedures from customer formula text.
37. Formula lifecycle is `DRAFT -> VALIDATED -> TESTED -> APPROVED -> PUBLISHED -> RETIRED`. Published versions used by payroll are immutable.
38. Formula dependencies must be explicit and validated as a directed acyclic graph. Reject circular dependencies before publish.
39. Separate built-in Function Catalog from company Formula Catalog. Core functions are safe primitives; customer formulas compose those primitives.
40. Parameters and lookup tables are versioned data. Do not embed company thresholds/rates in formula source when they belong in parameter/lookup sets.
41. Formula execution must return `Value + ExplainTrace + Warnings/Errors + FormulaVersion`. Compiled delegates/caches are runtime artifacts only and are never persisted as executable binaries.
42. Formula scope/allowed inputs must be enforced (`COMPANY`, `ORGANIZATION`, `EMPLOYEE`, `FUND`, `COMPONENT` or future declared scopes).
43. Advanced formula text may use a friendly expression syntax, but it must parse to the same safe AST. Do not host Python or arbitrary scripting runtimes.

## Avalonia / Actipro / SVG rules
44. Organize the Avalonia client by feature (`Features/Payroll`, `Features/Attendance`, etc.), not as one global Views/ViewModels bucket.
45. Shared visual primitives live in `PayCalc24.UI.Components` (or equivalent) and wrap standard Avalonia/Actipro controls where reuse materially reduces duplication.
46. Feature XAML must not hard-code SVG file paths. It requests semantic `IconKey` values resolved by `IIconProvider`/`IconRegistry`.
47. TS24 Media owns SVG artwork. SVG icon packs must be replaceable/versionable without changing feature ViewModels or payroll business code.
48. Keep icon identity semantic (`NAV_PAYROLL`, `ACTION_CALCULATE`, `STATUS_WARNING`) rather than filename-oriented.
49. Centralize theme tokens (typography, spacing, radius, surfaces, status colors). Do not scatter brand values across feature XAML.
50. Prefer themeable/monochrome SVGs for icons that must adapt to light/dark/foreground states; preserve multicolor artwork only where required.
51. Missing icon assets must fail gracefully to a documented fallback/placeholder and emit a diagnosable warning; they must not crash payroll workflows.
52. Media asset replacement must be testable independently of business calculations.

## Required verification before completing a task
- Build succeeds.
- Unit tests pass.
- Relevant architecture/integration tests pass.
- New public API has validation and error contract.
- New business rules have test cases including boundary/error cases.
- No TS24-specific policy value was introduced into Core unless explicitly identified as seed/reference configuration.

## Coding workflow
Read the relevant spec first. Implement the smallest dependency-complete slice. Avoid speculative features outside the task. When the spec is ambiguous, document the assumption instead of inventing hidden behavior.
