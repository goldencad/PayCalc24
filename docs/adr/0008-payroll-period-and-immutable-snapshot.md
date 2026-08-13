# ADR 0008: Payroll period lifecycle and immutable calculation snapshot

## Status

Accepted for Task 09.

## Decision

`PayrollPeriod` is company-scoped and follows `DRAFT → PREPARED → FROZEN → CALCULATED → CLOSED`. Preparation may be reset to draft before freeze. A closed period may be reopened only with a reason and then follows `REOPENED → PREPARED → FROZEN → CALCULATED → CLOSED`.

`PeriodStart` and `PeriodEnd` are inclusive payroll coverage dates. `BusinessDate` resolves configuration and assignments, whose effective intervals remain half-open `[EffectiveFrom, EffectiveTo)` under ADR-0002.

The pipeline is resolve, validate, snapshot, freeze, then calculate. Freeze is atomic, optimistic-concurrency-protected and idempotent. It creates a new immutable snapshot revision containing relational subject/assignment facts, eligible-dependent count and identifiers, resolved typed inputs with contributing ledger entry identifiers, and exact compensation/formula/parameter/lookup/rule versions. Sensitive dependent and national-identifier fields are not copied.

Snapshot content is separated into `HistoricalFacts` and `PolicyConfiguration`. Future replay reuses both pinned halves; back-test/what-if may retain historical facts while explicitly substituting policy. Task 09 does not persist scenarios or execute calculations.

SHA-256 population, input, configuration and combined hashes use invariant scalar formatting and stable ordering. Audit timestamps and actors are excluded. Reopening never edits historical snapshots; the next freeze creates revision N+1.

The snapshot-to-Task-08 mapper builds `FormulaExecutionContext` exclusively from pinned snapshot content and performs no repository lookup.

## Consequences

Historical payroll inputs and policy selections remain reproducible after legitimate ledger, assignment or configuration changes. Snapshot children remain relational and queryable. Failed persistence transactions must not expose partial authoritative snapshots.
