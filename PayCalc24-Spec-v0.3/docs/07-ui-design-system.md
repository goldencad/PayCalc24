# PayCalc24 UI Design System — Avalonia + Actipro

## 1. Technical UI baseline
- .NET 9, Avalonia UI, MVVM.
- Actipro Avalonia controls are preferred for advanced desktop UX where licensed/available and materially useful.
- Targets: Windows, macOS ARM64/x64, Linux.
- Client talks to PayCalc24 API/application contracts only; no direct MariaDB access.
- UI contains presentation/state logic only; payroll formulas/rules remain in backend/domain engines.

## 2. Application shell
Persistent shell: top bar with product/company/user context; left navigation; content workspace; global notification/error area. Company selection comes from TS24 Core. Main navigation: Dashboard; Payroll; Funds; Simulation; Reports; Setup; Integration; Audit.

## 3. Standard screen patterns
- List/Detail: searchable grid left/top with detail editor.
- Workspace/Stepper: Payroll Period uses Setup → Subjects → Attendance → Inputs → Performance → Validate → Calculate → Review → Approve → Lock → Export.
- Designer: Formula, Fund and Report designers use split/docking layouts with toolbox/tree, canvas/editor, properties and test/preview output.
- Explain: summary + expandable calculation tree + version/source trace.
- Review: totals/cards + exception grid + drill-down.

## 4. Component guidance
Use Actipro controls where appropriate for docking/tool windows, tree/navigation, advanced editors, charts and other productivity-oriented desktop interactions. Standard Avalonia controls are acceptable where simpler. Do not couple domain contracts to a specific UI control.

Grids must support: virtualization, filtering, sorting, grouping where relevant, dynamic Pay Component/KPI columns, copy/export affordances, keyboard navigation, validation markers and drill-down.

## 5. Payroll-specific UX rules
1. Payroll Period is the primary operational workspace.
2. Errors are actionable and navigate to the record requiring correction.
3. Important calculated numbers expose Explain.
4. Locked history is visibly read-only; corrections use Adjustment/Recalculation.
5. Advanced setup is separated from monthly payroll operations.
6. Missing Insurance/PIT is `Unavailable/Pending/Error`, never visually represented as zero.
7. Dynamic configuration must not make routine monthly payroll unnecessarily complex; templates/defaults should reduce steps.
8. All destructive/version-changing actions show consequence and require appropriate reason/permission.

## 6. Responsive/platform scope
Desktop-first. Full payroll authoring/designers target normal desktop widths. Dashboard/review/approval should tolerate narrower windows. Platform-specific differences must be isolated behind UI/platform services; business behavior is identical across Windows/macOS/Linux.

## 7. Visual design ownership
This document defines functional design-system constraints, not final brand styling. Typography, spacing, icons, accent colors and final visual polish may be refined by TS24 Media/CEO review without changing domain/workflow contracts.


## 8. Shared component layer
Feature screens must consume reusable PayCalc24 UI primitives instead of styling raw controls independently:
```text
PcButton
PcIconButton
PcTextBox
PcComboBox
PcDataGrid
PcCard
PcMetricCard
PcStatusBadge
PcValidationPanel
PcWizard
PcDialog
PcToolbar
```
These may wrap Avalonia or Actipro controls. A visual redesign should normally be implemented in shared components/theme resources, not repeated across feature screens.

## 9. Feature-oriented client structure
Organize the Avalonia client by business feature:
```text
Features/
├─ Dashboard/
├─ PayrollPeriod/
├─ Attendance/
├─ Performance/
├─ Funds/
├─ Calculation/
├─ Explain/
├─ Simulation/
├─ Reports/
└─ Setup/
```
Avoid one large global Views/ViewModels folder.

## 10. SVG and theme ownership
All icons are resolved through semantic `IconKey` and `IIconProvider`. TS24 Media supplies versioned SVG icon packs. Feature XAML must not reference SVG file paths directly. Theme tokens and shared controls are centralized so Media/CEO visual changes do not require payroll business-code changes. See `11-svg-icon-system.md`.
