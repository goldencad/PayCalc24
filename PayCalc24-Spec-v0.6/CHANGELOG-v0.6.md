# v0.6 Change Summary

Date: 2026-08-13

Changes from v0.5:
- Added `docs/15-scenario-backtest-reproducibility.md`.
- Added architectural ExecutionModes: PRODUCTION, REPLAY, BACK_TEST, WHAT_IF.
- Required explicit immutable ExecutionContext and pinned configuration versions.
- Prohibited Formula/Calculation engine access to latest/current configuration or hidden clock state.
- Added EngineVersion, structured ExplainTrace and provenance requirements.
- Clarified FormulaTestCase vs business Back-test boundary.
- Updated Task 08 to build replay/back-test-compatible safe Formula Engine.
- Updated Task 16 to orchestrate/persist scenarios while reusing the production engine.
- Added `CODEX_TASK08_PROMPT.md`.
