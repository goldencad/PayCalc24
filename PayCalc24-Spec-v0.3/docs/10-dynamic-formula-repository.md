# PayCalc24 — Dynamic Formula Repository & Function Extension Specification

## 1. Objective
PayCalc24 must provide Odoo-like flexibility for payroll formulas without executing arbitrary Python/C#/SQL. Company formulas are data stored/versioned in MariaDB and executed by a safe .NET Formula Engine.

## 2. Runtime architecture
```text
MariaDB Formula Repository
 ├─ FormulaDefinition
 ├─ FormulaVersion
 ├─ FormulaDependency
 ├─ ParameterSet / ParameterValue
 ├─ LookupTable / LookupRow
 ├─ RuleSet / Rule
 └─ FormulaTestCase
          ↓
Formula Loader / Validator
          ↓
AST Compiler / Runtime Cache
          ↓
Safe .NET Decimal Evaluator
          ↓
Value + ExplainTrace + Diagnostics
```

MariaDB stores definitions. **MariaDB does not execute customer payroll formulas.**

## 3. Core entities

### FormulaDefinition
Stable identity:
- Id
- CompanyId nullable for system formulas
- Code
- Name
- ReturnType
- ScopeType
- Status

### FormulaVersion
Immutable after publish/use:
- Id
- FormulaDefinitionId
- VersionNo
- ExpressionText
- ExpressionAstJson
- EffectiveFrom
- EffectiveTo
- LifecycleStatus
- Checksum
- CreatedBy/CreatedAt
- PublishedBy/PublishedAt

### FormulaDependency
- FormulaVersionId
- DependsOnFormulaDefinitionId
- DependencyType (`FORMULA`, `INPUT`, `PARAMETER`, `LOOKUP`)
- ReferenceCode

### ParameterSet / ParameterValue
Versioned company policy values such as:
- ACHIEVEMENT_FLOOR
- ACHIEVEMENT_TARGET
- ACHIEVEMENT_MAXIMUM
- department allocation rates
- attendance thresholds

### LookupTable / LookupRow
Versioned decision/lookup data for:
- achievement multipliers;
- commission tiers;
- attendance grades;
- allowance bands;
- job-grade values.

### FormulaTestCase
- FormulaVersionId
- Name
- InputJson
- ExpectedValue
- Tolerance/rounding policy
- ExpectedDiagnosticCode nullable

Published formulas should not be approved without required tests passing.

## 4. Function Catalog vs Formula Catalog

### Function Catalog — built into the product
Safe primitives implemented in .NET:
```text
CONST, INPUT
ADD, SUBTRACT, MULTIPLY, DIVIDE
IF, AND, OR, NOT
MIN, MAX
ROUND, FLOOR, CEILING
LOOKUP, THRESHOLD, TIER
PRORATE, INTERPOLATE
WEIGHTED_SUM, ALLOCATE
```

Functions implement an extension contract such as:
```text
ICalculationFunction
- FunctionName
- Validate(arguments, context)
- Evaluate(arguments, context)
- Explain(...)
```

The engine resolves functions through a registry/DI. Avoid a monolithic switch.

### Formula Catalog — dynamic company formulas
Examples:
```text
DEPARTMENT_P3_POOL
ATTENDANCE_SCORE
P3_ELIGIBLE
SALES_COMMISSION
MONTHLY_GROSS
```

Company formulas compose Function Catalog primitives and other allowed formulas.

## 5. Friendly expression syntax
Advanced users may edit a friendly expression:
```text
MAX(0, DepartmentPayrollPool - DepartmentFixedPayroll)
```

The parser converts it to canonical AST:
```json
{
  "op": "MAX",
  "args": [
    { "const": 0 },
    {
      "op": "SUBTRACT",
      "args": [
        { "input": "DepartmentPayrollPool" },
        { "input": "DepartmentFixedPayroll" }
      ]
    }
  ]
}
```

The AST is the execution contract. Expression text is authoring/display data.

## 6. Formula calling formula
Formula dependencies are allowed when declared and scope-safe.

Example:
```text
GrossPay()
 ├─ FixedPay()
 ├─ AllowancePay()
 └─ P3Paid()
      ├─ P3Eligible()
      └─ FundingRule()
```

Before publish, build a dependency graph and reject cycles:
```text
A → B → C → A  => FORMULA_CYCLE
```

## 7. Formula scope
Each formula declares scope:
- COMPANY
- ORGANIZATION
- EMPLOYEE
- FUND
- COMPONENT

The evaluator exposes only inputs allowed for that scope. A formula must not query arbitrary company data or the database.

## 8. Lifecycle
```text
DRAFT
  ↓ Validate syntax/types/inputs/dependencies
VALIDATED
  ↓ Run test cases
TESTED
  ↓ Authorized approval
APPROVED
  ↓ Publish immutable version
PUBLISHED
  ↓ superseded/end-dated
RETIRED
```

A published version already referenced by a payroll calculation is immutable. Changes create a new version.

## 9. Execution and cache
At calculation start:
1. Resolve effective Formula/Parameter/Lookup versions.
2. Validate snapshot.
3. Load canonical ASTs.
4. Compile to internal evaluator/delegate representation where useful.
5. Cache by formula checksum/version.
6. Evaluate many employees in memory.
7. Persist results and explain trace references.

Compiled executable artifacts are runtime cache only; never persist arbitrary binaries as policy definitions.

## 10. Determinism and numeric rules
- Decimal only for payroll numeric calculation.
- Explicit rounding policy.
- Divide-by-zero returns a controlled diagnostic, never undefined behavior.
- Same input snapshot + same formula/parameter/lookup versions = same result.
- No current-time/random/network/database side effects inside formula evaluation.

## 11. Explain trace
Each evaluation can emit:
```text
Formula: DEPARTMENT_P3_POOL v2
MAX
 ├─ 0
 └─ SUBTRACT
     ├─ DepartmentPayrollPool = 120,000,000
     └─ DepartmentFixedPayroll = 72,000,000
Result = 48,000,000
```

Trace records input/source/version references sufficient for payroll Explain and audit.

## 12. RuleSet / Decision Table
Use RuleSet when business users need ordered IF/THEN rules rather than mathematical expressions.

Example:
| Priority | Condition | Result |
|---:|---|---|
| 1 | UnauthorizedLeaveDays >= 2 | Score = 0 |
| 2 | UnauthorizedLeaveDays = 1 | Score = 0.70 |
| 3 | LateCount > 3 | Score = 0.90 |
| 4 | Otherwise | Score = 1.00 |

RuleSet supports sequence, stop-on-match and versioning. It uses the same safe condition primitives and explain framework.

## 13. Database responsibilities
MariaDB may perform normal retrieval/filtering/aggregation for preparing inputs, such as Headcount or sum of fixed payroll. Company-authored formula text must not be translated into dynamic SQL for execution.

Boundary:
```text
MariaDB: persistence, retrieval, approved aggregation
.NET Engine: payroll formulas, rules, simulation, explain
```

## 14. Security
Formula environment forbids:
- filesystem;
- network;
- process/thread control;
- reflection;
- arbitrary SQL;
- arbitrary C#/Python/JavaScript;
- direct DbContext access;
- secrets/environment access.

## 15. Definition of Done
The Formula Repository is ready when an authorized user can:
1. create a Draft formula;
2. author with builder or expression text;
3. validate syntax/types/dependencies;
4. run stored test cases;
5. approve/publish a version;
6. resolve the effective version for a payroll period;
7. calculate from an immutable input snapshot;
8. receive result + explain trace;
9. create v2 without changing historical v1 results.
