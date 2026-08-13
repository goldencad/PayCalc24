# ADR 0006: Formula repository relational and execution boundary

## Status

Accepted for Task 07.

## Decision

Dynamic company formula configuration is owned by a separate `FormulaRepository` module. Stable identities, versions, dependencies, parameters, lookup rows, rules, and test cases remain relational and company-scoped. UUIDs use the existing `CHAR(36)` strategy and numeric typed values use `DECIMAL(28,8)`.

`ExpressionText` is authoring data. Nullable `ExpressionAstJson` reserves the persistence boundary for Task 08 without defining an AST format. Test inputs and future canonical AST are the only inherently dynamic JSON fields. No executable delegate, script, generated SQL, or runtime function catalog is persisted.

Published content uses the common half-open `[EffectiveFrom, EffectiveTo)` convention and is immutable. Formula lifecycle adds explicit validation, test, and approval evidence states around the existing publication semantics. Declared formula-to-formula edges are checked as a directed acyclic graph without inspecting expression source.

## Consequences

The existing `FormulaEngine` project remains a pure, empty execution boundary until Task 08. Repository consumers can reproduce historical policy and audit lifecycle decisions, while parsing, AST validation, test execution, and formula evaluation remain out of scope for Task 07.
