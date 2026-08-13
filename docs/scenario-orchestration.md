# Scenario orchestration

Task 16 adds an isolated Scenario module:

```text
Frozen Historical Facts + pinned Policy A -> REPLAY -> existing Calculation/Fund engines
Frozen Historical Facts + pinned Policy B -> BACK_TEST -> the same engines
Facts + explicit input/policy overrides -> WHAT_IF -> the same engines
```

`ScenarioDefinition` is editable draft metadata. Finalization creates immutable `ScenarioSnapshot` revision 1; any later override change creates revision 2. A snapshot contains the baseline snapshot identity/hash, business date, frozen historical facts, complete policy configuration, explicit ordered overrides, engine compatibility metadata, and deterministic hash.

Execution is non-authoritative and idempotent. It does not change the Payroll Input Ledger, frozen production snapshot, authoritative calculation/fund results, or payroll period lifecycle. Comparison delegates to Payroll Review, so production-versus-scenario and scenario-versus-scenario use the same variance and funding projections.
