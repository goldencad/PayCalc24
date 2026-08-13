# PayCalc24 — Avalonia Screen Wireframes

These are structural wireframes for Codex/Dev. They are not pixel-perfect visual designs.

## 1. Main shell
```text
┌──────────────────────────────────────────────────────────────────────┐
│ PayCalc24   Company [▼]                         Notifications  User  │
├──────────────┬───────────────────────────────────────────────────────┤
│ Dashboard    │ Page title / breadcrumb                              │
│ Payroll      │                                                       │
│ Funds        │ Main workspace                                       │
│ Simulation   │                                                       │
│ Reports      │                                                       │
│ Setup        │                                                       │
│ Integration  │                                                       │
│ Audit        │                                                       │
└──────────────┴───────────────────────────────────────────────────────┘
```

## 2. Payroll Period Workspace
```text
Payroll / 08-2027                                      Status: CALCULATED
[Setup]—[Subjects]—[Attendance]—[Inputs]—[Performance]—[Validate]—[Calculate]—[Review]—[Approve]—[Lock]—[Export]
┌──────────────────────────────────────────────────────────────────────┐
│ Gross Payroll │ Variable Pay │ P3 Pool │ Coverage │ Errors/Warnings │
├──────────────────────────────────────────────────────────────────────┤
│ Current-step content / actionable validation grid                   │
└──────────────────────────────────────────────────────────────────────┘
                                               [Validate] [Calculate]
```

## 3. Payroll Result / Explain
```text
┌──────────────────────────── Result Grid ─────────────────────────────┐
│ Employee │ dynamic components... │ Gross │ Insurance │ PIT │ Net    │
└──────────────────────────────────────────────────────────────────────┘
Double-click/Explain →
┌──────── Summary ────────┬──────── Calculation Tree ─────────────────┐
│ Employee/Period         │ P3 Eligible                               │
│ Gross / Net             │  └─ INTERPOLATE                           │
│ Achievement/Gate        │     ├─ Final Achievement                  │
│ Eligible/Paid           │     ├─ Floor/Target/Maximum               │
│ Versions/Sources        │     └─ Result                             │
└─────────────────────────┴────────────────────────────────────────────┘
```

## 4. Attendance Import
```text
[Upload] → [Select Sheet] → [Map Columns] → [Validate] → [Preview] → [Commit]
┌──────── Source Columns ────────┬──── PayCalc24 Fields ──────────────┐
│ MaNV                           │ EmployeeCode                       │
│ NgayCong                       │ ActualWorkingDays                  │
│ KP                             │ UnauthorizedLeaveDays              │
└────────────────────────────────┴─────────────────────────────────────┘
Validation: 97 valid / 2 warnings / 1 blocking error
```

## 5. Formula Designer
Prefer an Actipro-enabled docking/split workspace where suitable.
```text
┌── Node Toolbox ──┬──────── Formula Canvas/Tree ────────┬ Properties ┐
│ INPUT            │ IF                                  │ selected   │
│ CONST            │ ├─ condition...                     │ node       │
│ ADD/SUBTRACT     │ └─ INTERPOLATE...                   │ settings   │
│ IF/MIN/MAX       │                                     │            │
│ INTERPOLATE      │                                     │            │
├──────────────────┴─────────────────────────────────────┴────────────┤
│ Test Inputs                         │ Explain / Test Result           │
└─────────────────────────────────────┴────────────────────────────────┘
[Validate] [Test Formula] [Preview] [Publish Version]
```

## 6. Fund Designer
```text
Fund hierarchy/tree                 Properties / formula
Gross Payroll                      Source: ...
 └─ Allocable Pool                 Formula: ...
     └─ IT Center                  Floor/Cap: ...
         └─ Dev 3 P3 Pool          Carry-forward: ...
[Test with sample amount] → downstream values/coverage preview
```

## 7. Simulation
```text
Base Period [08-2027]   Scenario [Dev P3 +10%]
┌ Parameter Overrides ────────────────────────────────────────────────┐
│ Revenue -10% | P3 Target +10% | Allocation 35→36 | Headcount +2   │
├─────────────────────────────────────────────────────────────────────┤
│ Metric            Base          Scenario         Change             │
│ Gross Payroll     ...           ...              ...                │
│ P3 Coverage       ...           ...              ...                │
│ Reserve           ...           ...              ...                │
└─────────────────────────────────────────────────────────────────────┘
```

## 8. Approval
Approver view is intentionally compact: Gross, Net, Employer Cost, change vs previous period, Payroll/Revenue, P3 Pool/Paid/Coverage, Reserve usage, exceptions and manual overrides. Drill-down is available; formula setup is not shown by default.

## 9. Dynamic rendering requirements
- Result grids derive Pay Component columns from definitions/results.
- Performance UI derives KPI tiers/groups/weights from configuration.
- Attendance fields/rules may vary by company.
- Provider sections render based on configured integrations/entitlements.
- No screen assumes TS24's P1/P2/P3 or 70/100/120 values.


## 10. Icon and shared-component rule
All toolbar/navigation/status icons shown in these wireframes are semantic placeholders. Implementation must use `IconKey` through the shared icon provider/component layer. Do not encode concrete SVG paths in these screens. Media can replace the SVG pack without altering feature layouts or ViewModels.
