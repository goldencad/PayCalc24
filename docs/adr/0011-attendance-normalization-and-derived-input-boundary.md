# ADR 0011: Attendance normalization and derived payroll input boundary

## Status

Accepted for Task 12.

## Decision

Attendance is an upstream, company-scoped facts module. Source adapters map arbitrary layouts into canonical, explicitly-unitized attendance facts. An import batch pins its source, published mapping, policy version, source fingerprint, actor and correlation. Committed batches and facts are immutable; correction is a new batch.

Policies use half-open effective periods and immutable published revisions. Evaluation always receives an explicit policy version. Production resolution is date-based; replay pins the historical version, while back-test may pin an alternative version. Formula/parameter/lookup/rule identities are retained by policy rules and provenance; Attendance does not introduce another expression language.

Only commit may publish payroll-affecting output, and it does so through `IPayrollInputLedgerService`. Corrections call ledger supersession rather than updating an entry. Preview, replay and back-test never write production entries. Existing frozen snapshots and calculation/fund results are never queried or mutated by Attendance.

Business dates are explicit `DateOnly` values parsed invariantly. A source owns an IANA/system time-zone identity for future timestamp adapters; no server-local time or wall-clock business date participates in normalization or evaluation. Canonical fingerprints and result hashes are SHA-256 over explicitly ordered invariant values.

## Consequences

Vendor column names and company-specific output codes remain configuration. Historical facts plus a pinned policy and unchanged evaluator semantics reproduce the same result. Later connectors may feed the same boundary without adding vendor branches to Core.
