# PayCalc24 Specification Pack v0.7

**Type:** Architecture / roadmap re-baseline  
**Roadmap:** 20 product-level tasks  
**Status when authored:** Tasks 01–11 closed; Task 12 not started.

v0.7 supersedes v0.6 as the authoritative specification for remaining task numbering and architecture boundaries.

## Main changes

- Corrected Task 11–13 order:
  - Task 11 = Generic Payroll Funds / Allocation / Coverage
  - Task 12 = Attendance
  - Task 13 = KPI / Performance / Gate
- Kept the MVP at 20 tasks.
- Added a single end-to-end architecture map.
- Formalized the immutable chain:
  `PayrollInputLedger → Frozen Snapshot → Calculation Run → Fund Allocation Result`.
- Reaffirmed `HistoricalFacts / PolicyConfiguration` separation.
- Reaffirmed same-engine semantics for Production / Replay / Back-test / What-if.
- Defined Attendance/KPI as producers of canonical Payroll Inputs.
- Kept TS24 P3 policy in Task 14 reference configuration/tests.

## Authoritative documents

- `IMPLEMENTATION_PLAN.md`
- `AGENTS.md`
- `docs/14-payroll-subject-organization-model.md`
- `docs/15-scenario-backtest-reproducibility.md`
- `docs/16-architecture-rebaseline-v0.7.md`
- `ROADMAP_STATUS-v0.7.md`

## Technology baseline

```text
.NET 9
MariaDB
Avalonia
Actipro
Ribbon + Backstage
Localization-first
System / Light / Dark
SVG IconKey / Media Pack
Modular Monolith
Dynamic Formula Repository
Safe Formula Engine
Immutable Payroll Input Ledger
Reproducible Snapshot-based Calculation
```

Task 11 is closed. Task 12 is the next implementation task under the v0.7 roadmap.
