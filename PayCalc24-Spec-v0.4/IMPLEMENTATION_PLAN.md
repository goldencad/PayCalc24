# PayCalc24 — Implementation Plan v0.4

This plan is ordered by dependency for Codex. Each task should normally be a focused PR/worktree. Read `AGENTS.md` and relevant docs first. Do not start dependent work until prerequisite contracts are stable.

## Task 01 — Solution Foundation, Modular Boundaries & Architecture Tests
**Depends on:** none  
Create .NET 9 solution with Domain/Application/Contracts, module boundaries, Infrastructure.MariaDb, Api, Client.Avalonia and tests. Add ADR folder, CI build/test and architecture tests.

**Acceptance:** solution builds; architecture tests prevent Domain/FormulaEngine/Client dependency violations; modular-monolith structure follows `09-modular-architecture.md`.

## Task 02 — Company Context, User Preferences, Localization/Theme Context, Correlation & Audit Foundation
**Depends on:** 01  
Implement TS24 Core abstractions, company isolation, current user, correlation/idempotency and audit writer contracts. Add user presentation preferences for PreferredCulture and ThemeMode (`SYSTEM/LIGHT/DARK`), plus localization resource/diagnostic contracts. Do not implement later payroll feature screens yet.

**Acceptance:** cross-company tests fail safely; no duplicate Company/User master; later commands can emit audit/correlation.

## Task 03 — Effective Dating, Versioning & Publication Framework
**Depends on:** 01,02  
Implement common effective-date/version/status/lifecycle primitives and immutable published-version behavior.

**Acceptance:** version resolution by period/date; used/published historical versions cannot be silently mutated.

## Task 04 — Organization, Payroll Subject & Assignment
**Depends on:** 02,03  
Implement PayrollSubject, OrganizationUnit, Position, JobGrade and PayrollAssignment with MariaDB mappings/API.

**Acceptance:** Company+EmployeeCode unique; close/create history; assignment overlap validation; tests.

## Task 05 — Generic Catalogs, Pay Components & Compensation Schemes
**Depends on:** 03,04  
Implement dynamic catalog foundation, PayComponent, CompensationScheme and SchemeComponent.

**Acceptance:** arbitrary components/schemes work without P1/P2/P3 enums/columns; effective versioning works.

## Task 06 — Payroll Input Catalog & Immutable Input Ledger
**Depends on:** 03,04  
Implement typed PayrollInputDefinition/Value, scopes, source/version lineage and audited manual overrides.

**Acceptance:** derived/manual/provider inputs coexist without erasing lineage; period/scope querying and tests.

## Task 07 — Dynamic Formula Repository Schema & Lifecycle
**Depends on:** 03,06  
Implement FormulaDefinition/Version, ParameterSet/Value, LookupTable/Row, FormulaDependency, FormulaTestCase and RuleSet lifecycle in MariaDB.

**Acceptance:** Draft→Validated→Tested→Approved→Published→Retired state rules; published used versions immutable; dependency records queryable.

## Task 08 — Safe Formula DSL/AST, Function Registry & Explain Engine
**Depends on:** 07  
Implement parser/AST validator, decimal evaluator/compiler/cache and registry-based `ICalculationFunction`. Initial functions: arithmetic, boolean/IF, MIN/MAX, ROUND/FLOOR/CEILING, LOOKUP, THRESHOLD/TIER, PRORATE, INTERPOLATE, WEIGHTED_SUM, ALLOCATE.

**Acceptance:** no arbitrary code/SQL; cycle detection; scope/input validation; deterministic tests; stored test cases run; result returns Value+ExplainTrace+Diagnostics+Version.

## Task 09 — Payroll Period State Machine & Snapshot Resolution
**Depends on:** 03,06,07  
Implement PayrollPeriod lifecycle, policy/input/formula/parameter/lookup snapshot resolution and lock semantics.

**Acceptance:** valid transitions only; snapshot versions reproducible; locked period immutable.

## Task 10 — Core Compensation Calculation Engine
**Depends on:** 05,06,08,09  
Implement CalculationContext, ordered dynamic component execution, Calculation/Line/ComponentResult and Gross classification.

**Acceptance:** same snapshot yields same result; arbitrary components calculate without schema changes; engine has no DB/network dependency.

## Task 11 — Attendance Import & Attendance Policy Module
**Depends on:** 04,06,08,09  
Implement import mapping/validate/preview/commit, AttendanceRecord and dynamic RuleSet/Formula evaluation into PayrollInputLedger.

**Acceptance:** immutable import batches; blocking errors; derived workdays/OT/score/gates are inputs, not direct compensation coupling.

## Task 12 — KPI / Performance / Gate Module
**Depends on:** 04,06,08,09  
Implement KPI catalog/assignment/result, configurable weighted achievement and Gate framework.

**Acceptance:** no fixed three-tier assumption; Overall→Gate→Final order; outputs written as derived Payroll Inputs; explain/tests.

## Task 13 — Payroll Fund & Allocation Module
**Depends on:** 04,06,08,09  
Implement FundType/Definition/Result, hierarchy, allocation, floor/cap/carry-forward and Coverage.

**Acceptance:** TS24 department/P3 pools configurable without code; zero denominator handled; results versioned by CalculationId.

## Task 14 — TS24 Reference Policy Pack / P3 Tests
**Depends on:** 10,12,13  
Encode TS24 Floor/Target/Maximum, Gate, Eligible/Paid and Coverage behavior as seed configuration, parameters, formulas, lookup/rules and tests.

**Acceptance:** reference boundary cases pass; TS24 values remain editable data, never Core constants.

## Task 15 — Validation, Explain Payroll, Variance & Funding Review
**Depends on:** 10–14  
Build actionable validation aggregation, employee Explain API/model, period variance and Funding Review workflow.

**Acceptance:** blocking/warning severity; trace includes inputs/formulas/intermediate values/versions; funding shortage is explicit review state.

## Task 16 — Simulation / Back-test Engine
**Depends on:** 10,12,13,15  
Reuse production engines on cloned snapshots with parameter/formula overrides; persist scenarios separately.

**Acceptance:** no production mutation; base/scenario comparisons for Gross, variable pay, pools, coverage, reserve/cost metrics.

## Task 17 — Review, Approval, Lock & Adjustment/Recalculation
**Depends on:** 09,15  
Implement submit/review/approve/reject/lock and authorized adjustment generating Calculation Version N+1.

**Acceptance:** reason/audit required; old versions immutable/queryable; direct mutation blocked after lock.

## Task 18 — Canonical Integrations, Statutory Results, Net Pay & Accounting
**Depends on:** 10,17  
Implement provider interfaces/adapters boundary for Employee, Attendance, Insurance, PIT, Accounting; combine statutory results into configurable Net/Employer Cost.

**Acceptance:** provider implementations do not leak into Core; missing statutory data is not zero; idempotent push/pull; Odoo/ezBooks/generic export contracts supported.

## Task 19 — Reporting + Avalonia UI Foundation + SVG Media System
**Depends on:** 01,05,08,15  
Implement ReportDefinition/Snapshot backend plus Avalonia feature shell, shared `Pc*` components, theme tokens, `IconKey`, `IIconProvider`, IconRegistry/pack loader and SVG manifest validation.

**Acceptance:** no feature XAML hard-codes SVG paths; swapping Media pack changes icons without feature code edits; missing icon fallback works; dynamic grid component support; Windows/macOS/Linux UI smoke tests.

## Task 20 — Avalonia + Actipro Operational UI / End-to-End MVP
**Depends on:** 04–19 as relevant  
Implement feature-oriented screens: Dashboard, Payroll Period Workspace, Subjects/Assignments, Components/Schemes, Attendance, Inputs, Performance, Validate, Calculate Result, Explain, Funds/Funding Review, Simulation, Approval/Lock, Integration, Reports and Audit. Use Actipro where it materially improves desktop UX.

**Acceptance:** HR completes Create Period → Subjects → Attendance → Inputs/KPI → Validate → Calculate → Explain → Funding Review → Approve → Lock → Statutory/Net when available → Report/Export without scripts/DB edits. Dynamic Pay Components/KPIs/Funds render from metadata. No payroll business rules in ViewModels.

---

## Codex execution pattern
For every task:
1. Read `AGENTS.md`.
2. Read only relevant specification documents.
3. State assumptions before coding when a contract is ambiguous.
4. Implement the smallest dependency-complete slice.
5. Add/maintain automated tests.
6. Run build/tests/architecture checks.
7. Summarize schema/API/contract changes and ADRs.
8. Do not broaden scope.

Parallelize only when dependencies permit. Formula Repository/Engine and Avalonia shell scaffolding may proceed in parallel once their contracts are stable, but feature UI must not invent business rules.


## v0.4 UI platform constraint
Before feature UI is built, Codex must treat the following as architecture, not cosmetic backlog:
- localization-first resources (`vi-VN`, `en-US`);
- Actipro Ribbon + Backstage shell;
- System/Light/Dark theme infrastructure;
- semantic SVG IconKey integration;
- localized report/UI labels;
- no hard-coded human-facing UI strings/colors/icon paths.
