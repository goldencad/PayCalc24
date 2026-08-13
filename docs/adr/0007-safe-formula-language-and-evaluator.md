# ADR 0007: Safe formula language and deterministic evaluator

## Status

Accepted for Task 08.

## Decision

FormulaEngine parses culture-neutral expression data into explicit literal, reference, unary, binary and function-call nodes. Grammar precedence is `OR`, `AND`, comparisons, additive, multiplicative, unary and primary. Literals are invariant integer/decimal, `TRUE`, `FALSE`, `NULL`, quoted text, and dates through `DATE("yyyy-MM-dd")`. Supported operators are `+ - * / = != > >= < <= AND OR NOT`.

The value system is DECIMAL, INTEGER, BOOLEAN, DATE, TEXT and internal NULL. Numeric mixing promotes to decimal; division always returns decimal. Text is never implicitly converted. Decimal overflow, division by zero, type errors, unknown symbols/functions and malformed syntax return stable diagnostics. `ROUND(value[, digits])` uses `MidpointRounding.AwayFromZero`. `INTERPOLATE(x,x1,y1,x2,y2)` is decimal-only and rejects `x1 == x2`.

The explicit function registry initially contains `IF`, `MIN`, `MAX`, `ABS`, `ROUND`, `COALESCE`, `DATE` and `INTERPOLATE`; `IF` is lazy. Unknown functions fail closed. LOOKUP is deferred until its immutable snapshot semantics need runtime integration; the evaluator has no repository boundary and performs no I/O.

Canonical AST JSON writes fixed property order, invariant scalar values and no timestamps or runtime metadata. Validation returns diagnostics, inferred type, AST and JSON without mutating Formula Repository lifecycle.

Evaluation receives an immutable context containing company, business date, mode, resolved values/parameters, correlation and optional payroll subject/period/scenario plus pinned formula, parameter, lookup and rule version identities. It never selects current versions or reads current time. PRODUCTION, REPLAY, BACK_TEST and WHAT_IF are provenance values processed by the identical parser, AST and evaluator.

Results contain typed value, one structured language-neutral trace, diagnostic and provenance. Trace values are canonical calculation scalars only. Engine semantic version is the constant `1.0.0`.

Default limits are 4,096 expression characters, 512 AST nodes, nesting 32, 32 arguments per function and evaluation depth 64. No arbitrary compilation, scripting, reflection invocation, process, filesystem, environment, network, HTTP, SQL or database access is present.

FormulaTestRunner maps Task 07 JSON inputs into an isolated context and compares expected typed values using explicit decimal tolerance or expected diagnostic code. It never uses production data.

## Consequences

FormulaRepository remains the persistence owner and requires no migration. Later orchestration may map repository DTOs and immutable lookup snapshots into this engine, but may not add I/O or alternate mode-specific evaluators inside FormulaEngine.
