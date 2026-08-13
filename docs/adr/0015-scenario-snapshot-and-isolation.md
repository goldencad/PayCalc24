# ADR 0015: Scenario snapshot and production isolation

## Status

Accepted for Task 16.

## Decision

A mutable, company-scoped `ScenarioDefinition` owns descriptive metadata and a small lifecycle. Each executable configuration is a new immutable `ScenarioSnapshot` revision. It references a frozen Task 09 snapshot for historical facts and baseline identity, pins a complete policy configuration, and stores generic ordered policy/input overrides. Input overrides replace values only in the scenario execution context and never write the Payroll Input Ledger.

Replay, back-test, and what-if are execution provenance. The Scenario module calls the existing Task 10 calculation, Task 11 fund, and Task 15 review contracts. It contains no formula, calculation, allocation, or variance engine. Non-production calls pass explicit historical facts and policy; no current/latest resolution occurs.

Scenario and result hashes are SHA-256 over invariant, deterministically ordered facts, version identities, overrides, engine versions, and result hashes. Timestamps, actors, localized names, and database row order are excluded. Successful results and finalized snapshots are append-only. Retry identity is company, scenario snapshot, and idempotency key; conflicting fingerprints fail, and concurrent execution of one snapshot is rejected.

Scenario outputs remain non-authoritative: production period state, calculation authority, fund results, ledger entries, and frozen snapshots are not mutated. Task 15 compares explicit production/scenario or scenario/scenario review contexts.

## Consequences

Historical facts can be combined with a deliberately different policy without a mode-specific engine. Revision N remains queryable after revision N+1. Persistence keeps core relationships relational while permitting immutable structured explain/comparison payloads where justified.
