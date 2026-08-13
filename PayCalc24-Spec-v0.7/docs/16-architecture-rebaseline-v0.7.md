# PayCalc24 — Architecture Re-baseline v0.7

**Specification Pack:** v0.7  
**Status:** Authoritative architecture baseline for Tasks 12–20  
**Roadmap:** 20 product-level tasks

## Purpose

v0.7 aligns the specification with the implementation actually delivered and the scope already locked for Task 11. It does not redesign Tasks 01–10.

Authoritative remaining order:

```text
Task 11 — Generic Payroll Funds / Allocation / Coverage
Task 12 — Attendance Import & Attendance Policy
Task 13 — KPI / Performance / Gate
Task 14 — TS24 Reference Policy Pack / P3 Tests
Task 15 — Validation / Explain / Variance / Funding Review
Task 16 — Scenario / Replay / Back-test / What-if
Task 17 — Review / Approval / Lock / Adjustment/Recalculation
Task 18 — Canonical Integrations / Statutory / Net Pay / Accounting
Task 19 — Reporting + Avalonia UI Foundation + SVG Media System
Task 20 — Avalonia + Actipro Operational UI / End-to-End MVP
```

No Task 21 is introduced.

## Architecture map

```text
Company / User Context
        ↓
PayrollSubject / Dependents / Organization / Assignment
        ↓
CompensationScheme / PayComponent
        ↓
PayrollInputDefinition
        ↓
Immutable PayrollInputLedger
        ↓
FormulaRepository
        ↓
Safe FormulaEngine
        ↓
PayrollPeriod
        ↓
Frozen PayrollCalculationSnapshot
        ↓
Payroll Calculation Orchestration
        ↓
Generic Payroll Fund Engine
        ↓
Attendance / KPI Derived Inputs
        ↓
Validation / Explain / Variance / Funding Review
        ↓
Scenario / Replay / Back-test / What-if
        ↓
Approval / Lock / Recalculation
        ↓
Statutory / Net Pay / Accounting / Integrations
        ↓
Reporting
        ↓
Avalonia + Actipro UI
```

## Dependency direction

Business engines consume immutable contracts/snapshots. They must not query mutable provider state during calculation.

Forbidden:
- FormulaEngine → MariaDB / HTTP / Odoo;
- Calculation Engine → current/latest Formula, Parameter, Input or Assignment;
- Fund Engine → live Attendance/Odoo/current Fund policy;
- UI ViewModel → payroll business-rule implementation.

## Reproducibility invariant

```text
HistoricalFacts
+
Pinned PolicyConfiguration
+
Engine Semantics
=
Deterministic Result
```

HistoricalFacts and PolicyConfiguration remain separable.

HistoricalFacts include subject/assignment context, resolved Payroll Inputs and provenance.

PolicyConfiguration includes Compensation Scheme, Pay Components, Formula/Parameter/Lookup/Rule versions and Payroll Fund policies.

## Execution modes

`PRODUCTION`, `REPLAY`, `BACK_TEST`, `WHAT_IF` reuse the same Formula, Calculation and Fund engine semantics. ExecutionMode is context/provenance, not a separate business engine.

## Immutable production chain

```text
PayrollInputLedger
    append-only
        ↓
PayrollCalculationSnapshot
    frozen / revisioned
        ↓
PayrollCalculationRun
    immutable finalized results
        ↓
FundAllocationResult
    immutable finalized funding results
```

Corrections create new entries/revisions/runs/scenarios. Historical authoritative data is not overwritten.

## Dynamic policy principle

P1/P2/P3, Attendance rules, KPI/Gate rules, Fund codes, Floor/Target/Maximum, Coverage thresholds, reserve thresholds, allowance/bonus/commission names are configuration/data, not Core business-name branches.

## Task 11 boundary

Task 11 is formally Generic Payroll Funds / Allocation / Coverage. Its already-issued prompt remains authoritative while the task is in progress.

Allowed scope includes versioned Fund catalog, FIXED/INPUT/FORMULA sources, generic scope, requirement/demand, coverage, proportional/priority/weighted allocation, deterministic rounding/remainder, reserve/deficit boundaries, immutable results/hash/trace/provenance and minimal snapshot pinning extension if required.

Not allowed in Task 11: Attendance, KPI, TS24-specific P3 branches, Gross/Net, PIT/BHXH, UI.

## Task 12 boundary — Attendance

Attendance source → import/mapping/validation → Attendance policy/rules → derived canonical Payroll Inputs → immutable PayrollInputLedger.

Attendance does not mutate compensation results directly.

## Task 13 boundary — KPI / Performance / Gate

KPI/performance facts → weighted achievement/gates → derived canonical Payroll Inputs → calculation pipeline.

No fixed three-tier assumption.

## Task 14 boundary — TS24 Reference Policy Pack

TS24-specific P1/P2/P3, Floor/Target/Maximum, Gate, Eligible/Paid and Coverage behavior are encoded as editable configuration, formulas, parameters, lookups/rules and tests, never Core constants.

## Task 15 boundary

Aggregate validation, employee Explain Payroll, period variance and Funding Review using structured diagnostics/provenance from Input, Formula, Calculation, Fund and Snapshot layers.

## Task 16 boundary

Persist isolated ScenarioSnapshots and orchestrate Replay/Back-test/What-if with the same production engines. No production mutation.

## Task 17 boundary

Submit/review/approve/reject/lock and authorized adjustment/recalculation create new revisions/versions. Historical results remain immutable.

## Task 18 boundary

Provider interfaces/adapters for Insurance, PIT, Accounting and related integrations. Missing provider/statutory results must not silently become zero. Provider implementations do not leak into Core engines.

## Tasks 19–20 UI boundary

Target:
```text
.NET 9
Avalonia
Actipro
Ribbon + Backstage
System / Light / Dark
Localization-first
SVG IconKey / Media Pack
Windows / macOS / Linux
```

No feature XAML/ViewModel should hard-code payroll business rules, SVG paths, colors or human-facing non-resource strings.

## Database standards

```text
MariaDB
UUID → CHAR(36)
Payroll decimals → DECIMAL(28,8)
No FLOAT/DOUBLE for payroll semantics
Effective dating → [EffectiveFrom, EffectiveTo)
```

Core identity/provenance stays relational. Hierarchical immutable AST/ExplainTrace may use JSON where appropriate.

## Audit and privacy

Audit retains actor, action, lifecycle transition, business identity, version/revision, correlation/idempotency and required reason.

Do not emit full CCCD/NationalId, secrets or source-document payloads in normal logs/diagnostics/traces.

## Current status when authored

```text
Tasks 01–10 — CLOSED
Task 11 — IN PROGRESS; scope locked as Generic Payroll Funds
Tasks 12–20 — NOT STARTED
```

## Acceptance rule for Tasks 12–20

Every remaining task preserves:
1. company isolation;
2. immutable historical provenance;
3. deterministic decimal semantics;
4. explicit business-date/version resolution;
5. no hidden current/latest policy lookup inside engines;
6. no business-name hard-coding where configuration is appropriate;
7. replay/back-test compatibility for calculation-affecting behavior;
8. architecture tests;
9. GitHub CI green before closure;
10. clean `main == origin/main` handoff.

## Roadmap stability

The MVP remains a 20-task roadmap. A large future task may be internally split into implementation slices such as `18A/18B` without changing the product-level numbering unless a later architecture review explicitly re-baselines it.
