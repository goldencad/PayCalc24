# ADR-0002: Effective dating and publication semantics

- Status: Accepted
- Date: 2026-08-13

## Context

Payroll policy definitions need deterministic historical resolution, immutable published versions, company isolation, and reusable behavior without introducing future business-module tables in Task 03.

## Decision

Business dates use .NET `DateOnly` and half-open intervals `[EffectiveFrom, EffectiveTo)`. The start is inclusive; the optional end is exclusive, so adjacent versions can meet on one date without overlap. A null end is open-ended. A bounded interval must have `EffectiveFrom < EffectiveTo`.

A stable `DefinitionId` owns versions with distinct `DefinitionVersionId` and positive version numbers. Lifecycle is `Draft -> Published -> Superseded`. Drafts are editable. Published and superseded content is immutable and retained. Changes to published content require a new draft/version. The explicit, audited supersede operation may shorten a published period by setting its exclusive end date; it cannot extend the period. This permits an open-ended current version to hand off cleanly to its successor without ordinary row editing.

Resolution considers published and superseded history. Exactly one version must contain the requested business date. Zero matches and multiple matches produce different stable diagnostics; no tie-breaker is applied. Publication rejects overlap with either published or superseded history.

The common model is persistence-agnostic. Task 03 adds no database table or migration. Later modules own their tables and mappings while exposing application contracts rather than EF entities. Company scope is checked through the Task 02 guard, and lifecycle operations write through `IAuditWriter`.

## Consequences

- `EffectiveTo` is not an effective day.
- Historical versions remain resolvable after supersession.
- Correcting an already-published period requires an explicit future correction workflow rather than silent mutation.
- Persistence and optimistic-concurrency tokens can be added by each adopting module without changing temporal semantics.
