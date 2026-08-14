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
- New API operation: expose an existing application command/query through the canonical dispatcher and action catalog.
- New AI capability: assign normal permissions/configuration and reuse the existing API operation; never add AI-specific business code.
- New AI virtual employee/role: use normal User/Role/Permission administration and an independent account.
- New license rule: change the entitlement adapter/shared access guard, not controllers or ViewModels.

## Changing an operational workspace

- Workspace layout, table columns, detail panels or empty/loading/error presentation: change
  `src/PayCalc24.Client.Avalonia/MainWindow.axaml`.
- Workspace selection, filtering, localized captions, command enablement projection or demo read models: change
  `src/PayCalc24.Client.Avalonia/Features/Payroll/OperationalWorkspaceViewModel.cs`.
- Ribbon/Backstage labels, language/theme commands or shell wiring: change
  `src/PayCalc24.Client.Avalonia/Features/Shell/ShellViewModel.cs` and `MainWindow.axaml`.
- KPI presentation consumes the structured Performance/Application boundary; it must not add a separate Human
  or AI business path.
- Approval presentation consumes the Task 17 approval lifecycle/capability projection; it must not reproduce
  submit/review/approve/reject/lock rules.
- A new dynamic payroll component normally requires no Desktop code. Add its definition and behavior below
  Desktop; the Calculate/Explain templates render component projection rows dynamically.

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
- Effective access is permission intersected with product entitlement and owning workflow state; channel metadata grants no rights.
