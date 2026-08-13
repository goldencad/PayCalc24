# ADR 0009: Deterministic payroll calculation orchestration

## Status

Accepted for Task 10.

## Decision

Every calculation run belongs to one immutable Task 09 snapshot revision. Production accepts only a `FROZEN` period and its authoritative frozen snapshot; replay uses the same pinned historical facts and policy, while back-test and what-if may explicitly substitute an immutable policy package. No calculation path resolves current configuration, current assignments, live ledger entries, providers, or wall-clock business dates.

The orchestrator traverses the snapshot-pinned compensation scheme generically. Component dependencies determine a stable topological execution order, with sequence and component identity breaking ties; sequence remains the presentation order. Missing dependencies and runtime cycles fail closed. Calculated values enter Task 08's execution context under the canonical `<COMPONENT_CODE>_RESULT` reference. `INPUT`, `FORMULA`, and parameter-backed `FIXED` are supported. Other methods return an explicit unsupported diagnostic until the shared Task 08 evaluator supports them.

Run, subject, and component results are immutable records. Component provenance includes the snapshot, subject, scheme/component version, formula identity/checksum, ledger entries, parameter/lookup/rule versions, execution mode, correlation, structured explain trace, and EngineVersion. SHA-256 hashes use invariant typed values and canonical identity ordering and exclude timestamps, actors, culture, and localized text.

Production stages all results in run scope. Any blocking component or subject failure marks the run failed and leaves the period frozen. Only after every staged subject succeeds does the transaction transition the period from `FROZEN` to `CALCULATED` and publish the immutable result set. A database implementation performs these final operations in one transaction. Company/snapshot/idempotency uniqueness and one authoritative successful production run per snapshot prevent conflicting retries and concurrent finalization.

## Consequences

The boundary is `Task 09 frozen snapshot → Task 08 FormulaExecutionContext → Task 10 immutable results`. Production, replay, back-test, and what-if share one evaluator and orchestration pipeline. Task 10 defines component results only; it does not define gross, net, tax, insurance, attendance, KPI, funds, payments, posting, payslips, or scenario persistence.
