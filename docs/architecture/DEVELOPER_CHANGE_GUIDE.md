# Developer Change Guide

- New pay component or scheme: Compensation configuration.
- New formula/function: Formula Repository or Formula Engine registry, respectively.
- New attendance rule/source: Attendance policy or source adapter.
- New KPI/gate: Performance configuration.
- New fund/allocation choice: Fund configuration/extension boundary.
- New jurisdiction/statutory provider: Integration/Statutory adapter.
- New ERP: Integration adapter.
- New report/renderer: Reporting definition/renderer.
- New screen: Desktop feature using application contracts.

## Non-negotiables

- No P1/P2/P3 or company-specific branches in generic engines or UI.
- No fixed KPI count or fixed statutory item fields.
- Use decimal, never float/double, for payroll semantics.
- Historical replay uses explicit pinned identity, never current/latest lookup.
- Immutable corrections create N+1; old results are not edited.
- UI does not calculate payroll or bind persistence entities.
- Formula Engine is I/O-free and statutory law stays behind provider boundaries.
- Missing statutory result is not zero.
- Scenario execution never mutates Production.
