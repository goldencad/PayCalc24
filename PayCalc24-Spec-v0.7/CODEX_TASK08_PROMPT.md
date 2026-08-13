# CODEX TASK 08 PROMPT — PayCalc24 Specification Pack v0.6

Repository: `https://github.com/goldencad/PayCalc24.git`  
Branch: `main`  
Required baseline: `cb2b1cdc750b35c4e1a6d3aae1b43aa4b8effb8c`

Task 07 is CLOSED. Do not reimplement Task 01–07. Do not implement Task 09+.

## Pre-flight

Before coding:
1. Fetch/checkout `origin/main`.
2. Confirm HEAD is exactly or contains baseline `cb2b1cdc750b35c4e1a6d3aae1b43aa4b8effb8c`.
3. Confirm clean working tree.
4. Read `AGENTS.md`, `README.md`, `IMPLEMENTATION_PLAN.md`.
5. Read `docs/15-scenario-backtest-reproducibility.md`.
6. Inspect Task 06 typed Payroll Input contracts and Task 07 Formula Repository + ADR-0006.
7. Preserve company isolation, temporal/versioning, decimal, diagnostics and architecture boundaries.

## Objective

Implement the first safe Formula Engine:

```text
ExpressionText
  -> Tokenizer
  -> Parser
  -> Canonical AST
  -> Static validation/type checking
  -> Safe evaluator
  -> EvaluationResult + structured ExplainTrace + Provenance
```

The engine executes configuration data only. It never executes arbitrary source code.

## Hard security boundary

Absolutely prohibited:
- C#/Roslyn scripting or dynamic compilation;
- Python/JavaScript runtimes;
- `eval`;
- arbitrary reflection/method invocation;
- dynamic SQL generated from formula text;
- filesystem/environment/process access;
- network/HTTP access;
- MariaDB/EF queries from evaluator;
- direct Attendance/Performance/Odoo/iBHXH/TaxOnline access.

Only explicitly registered safe built-in functions may execute.

## FormulaEngine module boundary

`PayCalc24.FormulaEngine` must remain independently testable and must not depend on Avalonia, MariaDB Infrastructure, EF persistence entities, providers or HTTP.

FormulaRepository owns persistence.
FormulaEngine owns language semantics.

## Canonical AST

Implement explicit nodes at minimum:
- LiteralNode
- ReferenceNode
- UnaryExpressionNode
- BinaryExpressionNode
- FunctionCallNode

ConditionalNode may be explicit or represented by `IF` consistently.

AST must serialize deterministically for future Task 07 `ExpressionAstJson` persistence.

Do not expose arbitrary .NET expression trees as user-executable language.

## Literals and types

Support:
- DECIMAL
- INTEGER
- BOOLEAN
- DATE
- TEXT
- NULL only if required by clean type semantics

Numeric parsing is culture-invariant and uses `decimal`, never `double`.

## Operators

Arithmetic:
`+ - * /`

Comparison:
`= != > >= < <=`

Boolean:
`AND OR NOT`

Parentheses and deterministic precedence.

Do not add speculative language features unless required.

## Type system

Define deterministic compatibility and promotion rules.

Examples:
- DECIMAL + DECIMAL -> DECIMAL
- INTEGER + INTEGER -> INTEGER or documented canonical numeric result
- DECIMAL + INTEGER -> DECIMAL
- BOOLEAN AND BOOLEAN -> BOOLEAN
- comparisons -> BOOLEAN

Avoid implicit TEXT-to-number coercion.
Invalid combinations return stable diagnostics.

## Safe Function Catalog

Implement registry-based built-ins. Initial catalog:
- IF
- MIN
- MAX
- ABS
- ROUND
- COALESCE
- INTERPOLATE if supported cleanly

Unknown functions fail closed.

Functions declare name, argument/type contract, return semantics and evaluator.

No giant business-name switch and no P1/P2/P3 knowledge.

## INTERPOLATE

If implemented:

`INTERPOLATE(X, X1, Y1, X2, Y2)`

Use decimal arithmetic. Explicitly define/diagnose `X1 == X2`.

## LOOKUP boundary

If LOOKUP runtime is included, it receives an already resolved immutable lookup snapshot in ExecutionContext. Evaluator must not query repositories/databases.

If current Task 07 semantics are insufficient for safe runtime LOOKUP, define the controlled resolver/snapshot contract and defer full runtime behavior rather than coupling persistence into evaluator.

## RuleSet boundary

If RuleSet runtime is included, use the same parser/evaluator for condition/result expressions and immutable rule snapshots. Do not create a second expression engine.

## CalculationExecutionContext — mandatory v0.6

Implement explicit immutable context with at least:

```text
CompanyId
BusinessDate
ExecutionMode
Resolved reference/input values
Parameter values
Lookup snapshots where supported
CorrelationId

FormulaVersionId
ParameterSetVersionIds
LookupTableVersionIds
RuleSetVersionIds

PayrollPeriodId? / PayrollSubjectId? / ScenarioId? where future-safe
```

ExecutionMode:
- PRODUCTION
- REPLAY
- BACK_TEST
- WHAT_IF

The evaluator must never internally select current/latest configuration.

## Version pinning

Evaluator receives explicit FormulaVersion content/identity.

PRODUCTION may later resolve effective versions outside the engine.
REPLAY/BACK_TEST/WHAT_IF can pin explicit versions even if not effective on BusinessDate.

Do not reject pinned scenario versions based on effective dates inside evaluator.

## No hidden current state

FormulaEngine must not use:
- `DateTime.Now`
- `DateTime.Today`
- repository `GetLatest`/`GetCurrent`
- current assignment/provider queries

BusinessDate and all resolved values come from ExecutionContext.

## EvaluationResult

Return a structured result with:
- Success/status
- typed Value
- DataType
- diagnostics
- structured Trace
- provenance

Normal formula failures should return stable diagnostics, not generic exceptions.

## Provenance

Future-safe result provenance includes as available:
- FormulaDefinitionId
- FormulaVersionId
- FormulaChecksum
- ParameterSetVersionIds
- LookupTableVersionIds
- RuleSetVersionIds
- referenced PayrollInputLedgerEntryIds
- ExecutionMode
- ScenarioId?
- CorrelationId
- EngineVersion

Do not fabricate IDs unavailable to Task 08.

## EngineVersion

Expose a deterministic semantic Formula Engine version (for example `1.0.0`) in result provenance.

Do not use assembly timestamp as the sole semantic version.

## Structured Explain Trace

Implement language-neutral `ExecutionTraceNode`-style structure with:
- NodeType
- Expression/function/reference metadata
- resolved value/result value
- data type
- child nodes
- diagnostic code if relevant

Do not generate localized prose inside FormulaEngine.
Do not expose NationalId, employee objects, arbitrary source payloads, database entities or secrets.

## FormulaTestCase runner

Execute Task 07 stored FormulaTestCases in isolated contexts:
- expected value
- expected data type
- decimal tolerance where configured
- expected diagnostic code

Return structured test-run results.
Formula tests must not touch production data.

## Validation

Expose parser/static validation capable of returning canonical AST + syntax/type/reference diagnostics where sufficient context/schema is supplied.

Do not silently mutate Task 07 lifecycle states from core FormulaEngine.

## Resource limits

Define and test bounded limits for at least:
- expression length
- AST node count
- nesting/evaluation depth
- function argument count

Pathological input fails with stable diagnostic.

## Numeric safety

Explicitly handle:
- division by zero
- decimal overflow
- invalid interpolation
- rounding

No NaN/Infinity.

`ROUND` semantics must be explicit/documented.

## Culture independence

Canonical formula language is invariant.

`1.25 + 2.50` must behave the same under `vi-VN`, `en-US`, `fr-FR`.

Do not parse locale decimal `1,25` as canonical numeric syntax.

## Mandatory v0.6 Replay / Back-test proof

Use the SAME parser/AST/evaluator for all modes.

Create one historical input/context.

Historical policy example:
- `P3_FLOOR = 13000000`
- `P3_TARGET = 17000000`

Alternative policy:
- `P3_FLOOR = 14000000`
- `P3_TARGET = 19000000`

Execute:
- REPLAY with historical pinned versions
- BACK_TEST with alternative pinned versions
- optional WHAT_IF synthetic override

Assert:
- same evaluator and AST semantics;
- only explicit context/version selection differs;
- deterministic independent results;
- no production mutation;
- no `if (mode == BACK_TEST) use BackTestEngine` pattern.

ExecutionMode is provenance/context, not an alternate runtime.

## Generic P3 proof

Evaluate a generic expression equivalent to:

```text
INTERPOLATE(
  FINAL_ACHIEVEMENT,
  ACHIEVEMENT_FLOOR,
  P3_FLOOR,
  ACHIEVEMENT_TARGET,
  P3_TARGET
)
```

The engine must not know what P3 means and must not branch on formula code.

## Company A / Company B proof

Company A:
`P3_ELIGIBLE`

Company B:
`SALES_INCENTIVE`

Use different reference/parameter names but exactly the same tokenizer/parser/AST/type/function/evaluator pipeline.

No business-name branching.

## Diagnostics

Add stable codes aligned with existing conventions, at minimum:
- FORMULA_ENGINE.SYNTAX_ERROR
- FORMULA_ENGINE.UNKNOWN_REFERENCE
- FORMULA_ENGINE.UNKNOWN_FUNCTION
- FORMULA_ENGINE.INVALID_ARGUMENT_COUNT
- FORMULA_ENGINE.TYPE_MISMATCH
- FORMULA_ENGINE.DIVISION_BY_ZERO
- FORMULA_ENGINE.DECIMAL_OVERFLOW
- FORMULA_ENGINE.INVALID_INTERPOLATION
- FORMULA_ENGINE.RESOURCE_LIMIT_EXCEEDED
- FORMULA_ENGINE.INVALID_AST
- FORMULA_ENGINE.TEST_EXPECTATION_FAILED

## Architecture tests

Assert FormulaEngine has no references/dependencies on:
- Avalonia
- MariaDB Infrastructure / EF persistence
- Attendance / Performance modules
- Odoo/iBHXH/TaxOnline implementations
- HTTP clients
- scripting runtimes
- Roslyn scripting
- Python/JavaScript runtime
- dynamic SQL from formula source

Assert evaluator has no hidden current-state resolution.

## Persistence

Task 08 should require no MariaDB migration by default.
Task 07 owns Formula Repository persistence.

Do not create new DB tables unless a concrete v0.6 engine requirement absolutely requires it and document why.

## Do not implement Task 09+

Do not implement:
- Payroll Period state machine/snapshot orchestration
- full PayComponent calculation pipeline
- Gross/Net payroll
- Funds
- Attendance/KPI engines
- ScenarioSnapshot persistence
- batch Back-test orchestration/comparison
- Simulation UI
- Formula Designer UI
- Ribbon feature screens

## ADR / documentation

Create a material ADR documenting:
- formula grammar;
- AST;
- type system;
- built-in Function Catalog;
- security boundary;
- decimal/rounding semantics;
- resource limits;
- CalculationExecutionContext;
- EngineVersion;
- ExplainTrace;
- reproducibility / Replay / Back-test rule.

## Verification

Attempt locally:

```bash
dotnet restore
dotnet build PayCalc24.sln --configuration Release
dotnet test PayCalc24.sln --configuration Release
git diff --check
```

Known local NuGet `CookieContainer/GetDomainName / NU1301` issue must not cause package downgrade, dependency workaround, analyzer suppression or weaker tests.

GitHub Actions is authoritative if local restore remains blocked.

## Commit / close

Suggested feature commit:

`feat: add safe formula engine`

Push `main`, inspect GitHub Actions with `gh`, and fix Task 08-only compiler/analyzer/test issues with minimal maintenance commits until latest CI is GREEN.

Do not begin Task 09.

Task 08 is CLOSED only when:
- baseline confirmed;
- tokenizer/parser/AST/type system/function registry/evaluator implemented;
- FormulaTestCase runner works;
- CalculationExecutionContext and four execution modes exist;
- same evaluator passes PRODUCTION/REPLAY/BACK_TEST/WHAT_IF architecture proof;
- structured ExplainTrace/provenance/EngineVersion exist;
- no current/latest DB resolution or I/O exists in evaluator;
- no arbitrary scripting exists;
- Release build 0 warnings / 0 errors;
- all application + architecture tests pass;
- latest GitHub Actions GREEN;
- working tree clean;
- `main == origin/main`;
- Task 09 not implemented.

## Final handoff report

Report:
1. Baseline SHA
2. Feature commit SHA
3. Maintenance commit SHA(s)
4. Resulting HEAD SHA
5. GitHub Actions URL/status
6. Release warning/error count
7. Application tests
8. Architecture tests
9. Total tests
10. Formula grammar
11. AST node types
12. AST serialization
13. Type system
14. Function Catalog
15. Decimal/rounding semantics
16. ExecutionContext
17. ExecutionMode behavior
18. Provenance
19. ExplainTrace
20. EngineVersion
21. FormulaTestCase runner
22. Resource limits
23. Security restrictions
24. Generic P3 proof
25. Company A/B proof
26. REPLAY proof
27. BACK_TEST proof
28. Diagnostics
29. ADR
30. Confirmation no DB/current-state access in evaluator
31. Confirmation no arbitrary scripting
32. Confirmation no Task 09
33. Working tree clean
34. `main == origin/main`
