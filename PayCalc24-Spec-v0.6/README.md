# PayCalc24 Specification Pack v0.6

**Architecture baseline:** Modular Monolith / Dynamic Formula Repository / Reproducible Calculation / Localization-first / Avalonia + Actipro  
**Task 08 source baseline:** `main@cb2b1cdc750b35c4e1a6d3aae1b43aa4b8effb8c`

v0.6 preserves all v0.5 decisions and adds the **Scenario / Replay / Back-test / Reproducibility Architecture** before the Formula Engine is implemented.

## v0.6 core rule

Production, Replay, Back-test and What-if must use the **same Formula/Calculation Engine**.

Engines receive explicit immutable ExecutionContext with pinned versions and must not query "latest/current" configuration during evaluation.

## New v0.6 document

- `docs/15-scenario-backtest-reproducibility.md`

## Task 08

Use:

- `AGENTS.md`
- `IMPLEMENTATION_PLAN.md`
- `docs/15-scenario-backtest-reproducibility.md`
- `CODEX_TASK08_PROMPT.md`

Task 08 implements the safe Formula Engine foundation but does **not** implement Task 09+ payroll orchestration or Task 16 scenario persistence/batch back-test orchestration.

## Existing v0.5 domain decision

PayCalc24 owns PayrollSubject/PayrollAssignment rather than duplicating a full HRM Employee Master. Legacy iBHXH employee/department/position schemas remain source-system boundaries.
