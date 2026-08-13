# PayCalc24 — Modular Architecture Specification

## 1. Architectural style
PayCalc24 starts as a **modular monolith**. The deployment can remain simple while code boundaries are designed as if modules may later be replaced, extracted or independently upgraded.

The goal is not microservices. The goal is **low coupling, explicit contracts and replaceable modules**.

## 2. Proposed solution structure
```text
PayCalc24/
├─ src/
│  ├─ PayCalc24.Domain/
│  ├─ PayCalc24.Application/
│  ├─ PayCalc24.Contracts/
│  ├─ Modules/
│  │  ├─ PayCalc24.Organization/
│  │  ├─ PayCalc24.Compensation/
│  │  ├─ PayCalc24.Attendance/
│  │  ├─ PayCalc24.Performance/
│  │  ├─ PayCalc24.PayrollFunds/
│  │  ├─ PayCalc24.FormulaEngine/
│  │  ├─ PayCalc24.PayrollCalculation/
│  │  ├─ PayCalc24.Simulation/
│  │  ├─ PayCalc24.Reporting/
│  │  └─ PayCalc24.Integration/
│  ├─ PayCalc24.Infrastructure.MariaDb/
│  ├─ PayCalc24.Api/
│  └─ PayCalc24.Client.Avalonia/
├─ tests/
└─ docs/
```

Physical project count may be consolidated if build overhead becomes excessive, but logical boundaries and dependency rules must remain.

## 3. Dependency direction
```text
Avalonia Client
      ↓
API / Application Contracts
      ↓
Application / Module Contracts
      ↓
Domain + Calculation Engines
      ↑
Infrastructure adapters implement ports
```

Forbidden dependencies:
- Domain → MariaDB/EF Core.
- FormulaEngine → MariaDB/HTTP/Avalonia.
- ViewModel → DbContext.
- Compensation → Attendance persistence tables.
- PayrollCalculation → iBHXH/TaxOnline/Odoo implementation types.

## 4. Payroll Input Ledger as integration seam
Attendance and Performance do not inject their internal models into payroll calculation.

```text
Attendance Raw Data
   ↓
Attendance Policy Engine
   ↓
Derived PayrollInputValues
                                                   → Payroll Input Ledger → Calculation Engine
                         /
KPI Results
   ↓
Performance/Gate Engine
   ↓
Derived PayrollInputValues
```

This allows a future Attendance module or KPI module to be replaced without rewriting Compensation.

## 5. Replaceability rule
Prefer:
```text
Contract → Implementation → Registry/DI
```
over:
```text
Module A edits Module B internals
```

Examples:
- Add ERP integration → implement `IAccountingProvider`.
- Add Formula function → implement `ICalculationFunction`.
- Replace icon pack → update Media SVG pack/manifest.
- Add report renderer → implement report output contract.

## 6. Module contracts
Each module exposes only:
- Commands/queries or application services.
- DTO/contracts.
- Domain events where needed.
- Extension interfaces intended for implementation.

Internal entities, EF mappings and repositories are not cross-module APIs.

## 7. Suggested extension contracts
```text
ICalculationFunction
IEmployeeProvider
IAttendanceProvider
IInsuranceProvider
ITaxProvider
IAccountingProvider
IReportRenderer
IIconProvider
IThemeProvider
```

Exact signatures are implementation work; the architectural role is fixed.

## 8. Upgrade strategy
A module upgrade should normally require:
1. New implementation/version.
2. Contract compatibility check.
3. Unit/contract tests.
4. Migration only for that module's persistence when possible.
5. Registration/configuration switch.
6. No unrelated feature edits.

## 9. Architecture tests
Automated architecture tests must assert at minimum:
- FormulaEngine does not reference Infrastructure/UI.
- Client does not reference Infrastructure.MariaDb.
- Provider adapters do not leak into Core contracts.
- Feature modules do not reference another module's EF entities.
- Domain does not reference ASP.NET/Avalonia/EF Core.

## 10. ADR requirement
Any exception to these boundaries requires an ADR explaining:
- why the contract is insufficient;
- alternatives considered;
- migration/rollback impact;
- whether the exception creates permanent coupling.
