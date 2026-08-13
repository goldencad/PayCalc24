# PayCalc24 — Data Dictionary Summary
Core entities:
- PayrollSubject
- OrganizationUnit / Position / JobGrade
- PayrollAssignment
- PayrollFundType / PayrollFundDefinition
- PayComponent
- CompensationScheme / CompensationSchemeComponent
- AttendanceType / AttendancePolicy / AttendanceImportBatch / AttendanceRecord
- KPI / KPIAssignment / KPIResult
- PayrollInputDefinition / PayrollInputValue
- FormulaDefinition / FormulaVersion / RuleSet / ParameterSet / LookupTable
- ReportDefinition
- PayrollPeriod / PayrollCalculation / PayrollCalculationLine
- PayrollComponentResult / PayrollFundResult
- SimulationScenario / SimulationResult
- IntegrationProfile / StatutoryCalculationResult / AuditEvent

All company-scoped entities carry `CompanyId`. Money and percentages use decimal. Effective dating and versioning are mandatory for payroll-affecting definitions.
