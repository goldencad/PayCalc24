# ADR 0005: Typed append-only payroll input ledger

- Status: Accepted
- Date: 2026-08-13

## Context

Payroll calculation must consume inputs from manual entry, attendance, performance, imports and future providers without depending on any source implementation. Values must remain auditable and interpretable after a catalog definition changes.

## Decision

`PayrollInputDefinition` is a company-scoped, effective-dated catalog. An accepted `PayrollInputLedgerEntry` snapshots definition identity, revision, code, data type, unit and aggregation metadata.

The ledger stores one of five typed columns: `DECIMAL(28,8)`, `BIGINT`, `BOOLEAN`, `DATE`, or bounded text. A database check constraint requires exactly the column matching `DataType`. Floating-point storage is forbidden.

Accepted entries have no update or delete API. A correction appends a new entry whose `SupersedesEntryId` points to the active entry being corrected. Company, subject, period and definition scope are inherited from that target; correction branches are rejected. History returns every entry, while the effective view excludes superseded entries.

Idempotency keys are unique per company. An identical retry returns the original entry; reuse with a different canonical payload is rejected.

For multiple active observations, `NONE` reports ambiguity. `SUM`, `AVERAGE`, `MIN`, and `MAX` use decimal arithmetic; `COUNT` counts active observations; `LATEST` orders by observation time, recorded time and UUID. No aggregation is payroll formula execution.

## Consequences

The future Formula Engine must consume this canonical effective input view. It must not query Attendance, Performance, Odoo, or another source system directly. Historical entries remain deterministic at the cost of snapshot column duplication. Database triggers are deferred; domain immutability, application contracts, foreign keys, typed-value checks and architecture tests provide Task 06 protection.
