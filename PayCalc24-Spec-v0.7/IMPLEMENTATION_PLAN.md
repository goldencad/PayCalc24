# PayCalc24 — Implementation Plan v0.7

This is the authoritative 20-task roadmap after the v0.7 architecture re-baseline.

## Task 01 — Solution Foundation, Modular Boundaries & Architecture Tests — CLOSED
## Task 02 — Company Context, User Preferences, Localization/Theme, Correlation & Audit — CLOSED
## Task 03 — Effective Dating, Versioning & Publication Framework — CLOSED
## Task 04 — Payroll Subject, Dependents, Organization & Assignment — CLOSED
## Task 05 — Generic Catalogs, Pay Components & Compensation Schemes — CLOSED
## Task 06 — Payroll Input Catalog & Immutable Input Ledger — CLOSED
## Task 07 — Dynamic Formula Repository Schema & Lifecycle — CLOSED
## Task 08 — Safe Formula DSL/AST, Execution Context, Function Registry & Explain Engine — CLOSED
## Task 09 — Payroll Period State Machine & Immutable Snapshot Resolution — CLOSED
## Task 10 — Payroll Calculation Orchestration — CLOSED

## Task 11 — Generic Payroll Funds / Allocation / Coverage Engine — IN PROGRESS
**Depends on:** 05,06,08,09,10

Implement generic company-scoped Payroll Fund catalog/versioning; FIXED/INPUT/FORMULA sources; generic Fund scope; requirement/demand; Coverage; PROPORTIONAL/PRIORITY/WEIGHTED allocation; deterministic rounding/remainder; reserve/deficit boundaries; immutable fund/member allocation results; result hash/trace/provenance; same engine for Production/Replay/Back-test/What-if; minimal Task 09 snapshot policy-pinning extension if required.

**Acceptance:** P3 is configuration only; Company A/B generic Fund proof; no live/current/latest resolution; Task 10 immutable component results are not mutated; Replay/Back-test proof; CI green.

## Task 12 — Attendance Import & Attendance Policy Module
**Depends on:** 04,06,07,08,09

Implement import mapping/validation/preview/commit, immutable import batches, attendance records/policy evaluation and derived canonical Payroll Inputs.

**Acceptance:** blocking import errors; source lineage; derived workdays/leave/OT/attendance-score/gates written to PayrollInputLedger; no direct compensation mutation; replayable.

## Task 13 — KPI / Performance / Gate Module
**Depends on:** 04,06,07,08,09

Implement KPI catalog/assignment/result, weighted achievement, configurable Gate framework and derived canonical Payroll Inputs.

**Acceptance:** no fixed three-tier assumption; Overall → Gate → Final configurable; outputs written to PayrollInputLedger; explain/provenance/tests.

## Task 14 — TS24 Reference Policy Pack / P3 Tests
**Depends on:** 10,11,12,13

Encode TS24 compensation policy entirely as configuration, parameters, formulas, lookup/rules and tests.

**Acceptance:** TS24 P1/P2/P3, Floor/Target/Maximum, Gate, Eligible/Paid and Coverage boundary cases pass; TS24 values never become Core constants.

## Task 15 — Validation, Explain Payroll, Variance & Funding Review
**Depends on:** 10–14

Aggregate validation, employee Explain Payroll, period variance, Fund/coverage review and actionable diagnostics.

**Acceptance:** blocking/warning/info; trace connects facts, formulas, intermediate values, component results, Fund allocations and versions; funding shortage explicit.

## Task 16 — Scenario / Replay / Back-test / What-if Orchestration
**Depends on:** 10,11,12,13,15

Persist isolated ScenarioSnapshots and orchestrate Replay/Back-test/What-if using the same production Formula/Calculation/Fund engines.

**Acceptance:** no production mutation; exact historical Replay; historical facts + alternative policies; scenario comparison for payroll/fund/coverage/cost metrics.

## Task 17 — Review, Approval, Lock & Adjustment/Recalculation
**Depends on:** 09,10,15

Implement submit/review/approve/reject/lock plus authorized adjustment/recalculation producing Revision/Version N+1.

**Acceptance:** reason/audit required; old snapshots/results immutable; direct mutation after lock blocked.

## Task 18 — Canonical Integrations, Statutory Results, Net Pay & Accounting
**Depends on:** 10,17

Implement provider interfaces/adapters for Employee/Attendance/Insurance/PIT/Accounting and compose statutory results into Net Pay/Employer Cost/accounting/export boundaries.

**Acceptance:** provider implementations do not leak into Core; missing statutory/provider result is not zero; idempotent integration; Odoo/ezBooks/generic contracts supported.

## Task 19 — Reporting + Avalonia UI Foundation + SVG Media System
**Depends on:** 01,05,08,15

Implement ReportDefinition/Snapshot backend plus Avalonia shell, shared components, localization, System/Light/Dark theme, Actipro Ribbon/Backstage and SVG IconKey/provider/pack loader.

**Acceptance:** no feature XAML hard-coded SVG paths/colors/business strings; Media pack swappable; dynamic grids; Windows/macOS/Linux UI smoke tests.

## Task 20 — Avalonia + Actipro Operational UI / End-to-End MVP
**Depends on:** 04–19 as relevant

Implement Dashboard, Payroll Workspace, Subjects, Schemes, Inputs, Attendance, KPI, Validate, Calculate, Explain, Funds, Simulation, Approval/Lock, Integrations, Reports and Audit.

**Acceptance:** HR completes end-to-end payroll workflow without scripts/DB edits; dynamic metadata drives Pay Components/KPIs/Funds; no payroll business rules in ViewModels.

---

## Codex execution pattern

For every task:
1. read repository `AGENTS.md`;
2. read current Specification Pack and relevant ADRs;
3. verify required baseline and clean worktree;
4. implement the smallest dependency-complete slice;
5. preserve versioning/reproducibility boundaries;
6. add application/domain/architecture tests;
7. run build/tests;
8. push and verify GitHub CI;
9. do not close until latest CI is green;
10. report commit SHA, tests, migrations, diagnostics, ADRs and explicit non-goals.

Do not broaden scope into later tasks.
