# ADR-0003: MariaDB UUID storage

- Status: Accepted
- Date: 2026-08-13

## Context

Task 04 introduces the first persisted company-scoped aggregates. A single UUID representation is required before creating their schema.

## Decision

Persist UUID identifiers as lowercase canonical `CHAR(36)` values. Domain and contract boundaries continue to use typed `Guid`-backed identifiers. Every company-scoped index begins with `CompanyId` where practical.

## Consequences

The representation is simple to inspect and interoperable with provider data, at the cost of more storage than `BINARY(16)`. Infrastructure owns all conversions; the choice does not leak into domain contracts.
