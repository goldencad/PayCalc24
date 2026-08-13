# PayCalc24 — ERD Summary
High-level relationship:

```text
PayrollSubject
 └─ PayrollAssignment
     ├─ OrganizationUnit
     ├─ Position
     ├─ JobGrade
     └─ CompensationScheme
         └─ CompensationSchemeComponent
             └─ PayComponent

PayrollPeriod
 ├─ AttendanceImportBatch -> AttendanceRecord
 ├─ KPIResult
 ├─ PayrollInputValue
 └─ PayrollCalculation
     ├─ PayrollCalculationLine
     │   ├─ PayrollComponentResult
     │   └─ StatutoryCalculationResult
     └─ PayrollFundResult
```

Formula/Rule/Parameter/Lookup definitions are versioned separately and referenced by calculation snapshots.
