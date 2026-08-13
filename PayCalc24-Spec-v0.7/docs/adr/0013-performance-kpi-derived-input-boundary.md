# ADR 0013 — Performance/KPI evaluation and derived Payroll Input boundary

## Decision

Performance is a company-scoped module owning versioned KPI definitions, effective-dated assignments, append-only measured facts, versioned policies and gates, and immutable evaluation results. Assignments are a collection and therefore impose no fixed KPI count.

Evaluation receives explicit fact, assignment, policy, formula/rule and Payroll Input identities. It scores KPI facts, aggregates overall achievement, evaluates gates in ascending explicit priority (ending after a matched `StopOnMatch` gate), then creates final achievement. Gates never change facts.

Scoring, non-default aggregation and configured conditions/results cross `IPerformanceExpressionEvaluator`, an adapter to the Task 07/08 Formula/Rule facilities. Performance contains no second expression parser or evaluator. Attendance values cross only the immutable Payroll Input contract; Performance does not reference Attendance storage or its module.

Only Production may publish derived outputs, through `IPayrollInputLedgerService`. Corrections create new KPI facts, evaluation results and ledger corrections that supersede prior entries. Replay, Back-test and What-if use the same pipeline and cannot publish production inputs.

The result hash uses canonical ordinal ordering and invariant decimal formatting. It includes facts, definitions, assignments/weights, policy/gates, pinned Formula/Parameter/Lookup/Rule identities, result values and engine version; it excludes timestamps, display names and localized labels.

## Consequences

Policies can model different companies, scopes, KPI counts and output codes without code branches. Historical facts plus pinned policy plus engine semantics reproduce the same result. Task 14 can add TS24 policy purely as configuration.
