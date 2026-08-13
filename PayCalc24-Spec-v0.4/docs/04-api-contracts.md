# PayCalc24 — API Contracts Summary
Primary resource groups:
`/payroll-subjects`, `/organization-units`, `/positions`, `/job-grades`,
`/payroll-assignments`, `/payroll-funds`, `/pay-components`,
`/compensation-schemes`, `/attendance`, `/kpis`, `/payroll-inputs`,
`/formulas`, `/payroll-periods`, `/payroll-calculations`,
`/simulations`, `/reports`, `/integrations`.

Canonical provider contracts:
`IEmployeeProvider`, `IAttendanceProvider`, `IInsuranceProvider`,
`ITaxProvider`, `IAccountingProvider`.

The Avalonia client uses Application/API contracts only; it does not access MariaDB directly.
