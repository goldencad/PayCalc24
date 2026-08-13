# PayCalc24 — Scenario, Back-test & Reproducibility Architecture

**Specification Pack:** v0.6  
**Status:** Architecture baseline before Task 08  
**Source baseline:** `main@cb2b1cdc750b35c4e1a6d3aae1b43aa4b8effb8c`

## 1. Objective

PayCalc24 must support Production, Replay, Back-test and What-if through the **same deterministic calculation pipeline**.

There must not be separate business-rule engines for production and simulation.

```text
Historical/Current Input Snapshot
+ Payroll Subject / Assignment
+ Compensation Configuration
+ Formula Versions
+ Parameter Versions
+ Lookup Versions
+ RuleSet Versions
        ↓
Explicit Version Resolution
        ↓
Immutable ExecutionContext / Scenario Snapshot
        ↓
Same Formula / Calculation Engine
        ↓
Result + Explain Trace + Provenance
```

## 2. Execution modes

Technical execution modes:

```text
PRODUCTION
REPLAY
BACK_TEST
WHAT_IF
```

### PRODUCTION
Use the policy/configuration versions resolved for the target payroll period/business date. The resolved versions are pinned before calculation begins.

### REPLAY
Use the exact historical input entries and exact configuration-version identities originally used. The objective is deterministic reproduction.

### BACK_TEST
Use historical input data with explicitly selected alternative policy/configuration versions.

### WHAT_IF
Use historical or synthetic inputs plus explicit scenario overrides/configuration. This must not mutate production data.

Execution mode is context/provenance. It is not a separate evaluator implementation.

## 3. CalculationExecutionContext

Formula/calculation engines must receive an explicit immutable execution context.

Conceptual contract:

```text
CalculationExecutionContext
---------------------------
ExecutionId
CompanyId
PayrollPeriodId?
PayrollSubjectId?
BusinessDate
ExecutionMode

InputSet

FormulaVersionIds
ParameterSetVersionIds
LookupTableVersionIds
RuleSetVersionIds

CompensationSchemeVersionId?
PayComponentVersionIds?

CorrelationId
ScenarioId?
EngineVersion
```

Not every field must be implemented in Task 08, but the contract must remain future-safe.

## 4. Resolve first, execute second

Required pattern:

```text
Business Date / Explicit Scenario Version Selection
                  ↓
            Version Resolver
                  ↓
          Immutable Version Set
                  ↓
        CalculationExecutionContext
                  ↓
             Formula Engine
```

Forbidden pattern:

```text
Formula Engine
  ↓
GetLatestFormula()
GetCurrentParameter()
DateTime.Today
Query current assignment
  ↓
Evaluate
```

Engine evaluation must never select "latest/current" policy implicitly.

## 5. Version pinning

Production normally uses effective-date resolution.

Replay/Back-test/What-if may explicitly pin a version whose effective range does not match the scenario BusinessDate.

The evaluator must accept the pinned version and must not reject it merely because another version would be "current."

Repositories must support both:

```text
ResolveEffective(businessDate)
GetByVersionId(versionId)
```

Do not design APIs that expose only `GetCurrent()` or `GetLatest()`.

## 6. ScenarioSnapshot boundary

Future Task 16 will persist immutable scenario definitions.

Conceptual model:

```text
ScenarioSnapshot
----------------
ScenarioId
CompanyId
Name
Description?
ScenarioType

BasePayrollPeriodId
BusinessDate

InputSnapshotReference
PayrollSubjectSnapshotReference?
AssignmentSnapshotReference?

CompensationSchemeVersionId?
PayComponentVersionIds
FormulaVersionIds
ParameterSetVersionIds
LookupTableVersionIds
RuleSetVersionIds

CreatedBy
CreatedAt
```

Task 08 must not persist ScenarioSnapshot. It must only make the engine compatible with it.

## 7. Historical vs alternative policy

Back-test must support controlled mixing:

```text
June 2026 Payroll Inputs
June 2026 Organization/Assignment
2027 Proposed Formula Version
2027 Proposed Parameter Version
2027 Proposed Lookup Version
```

This is valid in BACK_TEST/WHAT_IF.

Do not require every pinned configuration version to be effective on BusinessDate outside PRODUCTION resolution rules.

## 8. Reproducibility invariant

Every payroll-affecting input/configuration must be either:

1. immutable/append-only historical data; or
2. immutable published versioned configuration.

Expected invariant:

```text
Same Input Snapshot
+ Same Pinned Configuration Versions
+ Same Engine Semantics
= Same Deterministic Result
```

Task 06 already establishes append-only payroll inputs.
Task 07 establishes immutable/versioned Formula/Parameter/Lookup/Rule repositories.

## 9. EngineVersion

Formula/calculation result provenance must include an explicit semantic EngineVersion.

Reason: a FormulaVersion can remain unchanged while a future built-in function implementation changes.

Example:

```text
FormulaVersionId = ...
EngineVersion = 1.0.0
```

Do not persist executable binaries for reproducibility.

## 10. Calculation provenance

Results must be future-safe to identify the exact execution basis:

```text
FormulaVersionId
FormulaChecksum
ParameterSetVersionIds
LookupTableVersionIds
RuleSetVersionIds
InputLedgerEntryIds
PayrollSubjectId
PayrollAssignment identity/version where applicable
CompensationSchemeVersionId
PayComponentVersionIds
ExecutionMode
ScenarioId?
CorrelationId
EngineVersion
```

Do not store only the final number without provenance.

## 11. Explain Trace

Formula execution must return structured Explain Trace.

Conceptual:

```text
ExecutionTraceNode
------------------
NodeType
Expression?
FunctionName?
ReferenceCode?
ResolvedValue?
ResultValue?
DataType?
Children[]
DiagnosticCode?
```

Trace is language-neutral structured data, not a pre-localized sentence.

Example:

```text
INTERPOLATE
 ├─ FINAL_ACHIEVEMENT -> 0.92
 ├─ ACHIEVEMENT_FLOOR -> 0.70
 ├─ P3_FLOOR -> 13000000
 ├─ ACHIEVEMENT_TARGET -> 1.00
 └─ P3_TARGET -> 17000000
 Result -> ...
```

UI/report layers localize the trace later.

## 12. Trace privacy

Explain Trace contains only calculation-relevant canonical references/values.

Do not dump:
- full Employee/PayrollSubject objects;
- NationalId/CCCD;
- source documents;
- arbitrary external payloads;
- database entities;
- secrets/tokens.

## 13. FormulaTestCase vs Back-test

`FormulaTestCase` verifies one formula in an isolated deterministic context.

```text
input values -> expected value / expected diagnostic
```

Back-test evaluates business history at larger scope, potentially many employees and periods.

Task 08 implements FormulaTestCase execution.
Task 16 implements Scenario/Back-test orchestration.

Do not merge these concepts.

## 14. Scenario isolation

BACK_TEST/WHAT_IF results must not write into production Payroll Input Ledger or production Payroll Result records.

Conceptually:

```text
Production Result Store != Scenario Result Store
```

Scenario may read historical production snapshots, but its output remains isolated.

## 15. Scenario comparison boundary

Task 16 must be able to compare baseline vs scenario metrics such as:

```text
Total Payroll
Variable Pay / P3 Eligible
P3 Paid
Fund Coverage
Reserve
Employer Cost
```

Task 08 does not implement these metrics, but execution results/provenance must support later aggregation.

## 16. No hidden current-state dependency

From Task 08 forward, payroll calculation engines must not depend directly on:

```text
DateTime.Now
DateTime.Today
latest/current Formula
latest/current Parameter
latest/current Lookup
current Assignment without explicit resolution
database row order
```

Clock/date information must come from an explicit context.

## 17. Deterministic numeric semantics

Use `decimal` for payroll numeric evaluation.

Do not use `float` or `double`.

Rounding must be explicit and documented.

No NaN/Infinity semantics.

## 18. Deterministic ordering

Order-sensitive calculation inputs must have explicit stable ordering:
- Rule priority;
- Formula dependency ordering where required;
- Lookup row sequence;
- Compensation component sequence.

Do not rely on dictionary/database incidental iteration order.

## 19. Formula Engine purity target

Formula evaluation should be close to a pure function:

```text
Evaluate(AST, ExecutionContext)
    -> EvaluationResult
```

The evaluator must not perform I/O.

Database/API/provider access occurs before the engine boundary.

## 20. Task boundaries after v0.6

```text
Task 07 — DONE
Formula Repository + lifecycle + versions

Task 08
Tokenizer / Parser
Canonical AST
Type System
Safe Function Catalog
Safe Evaluator
Formula Test Runner
Execution Context
Structured Explain Trace
Provenance / EngineVersion

Task 09+
Payroll period / snapshot orchestration

Task 16
ScenarioSnapshot persistence
Replay/Back-test/What-if orchestration
Scenario comparison
```

## 21. Architecture acceptance rule

From Task 08 forward, every payroll-affecting feature must be able to answer:

> Can the same logic run against an explicitly pinned historical input/configuration snapshot without reading current state?

If no, the design does not satisfy PayCalc24 v0.6 reproducibility requirements.
