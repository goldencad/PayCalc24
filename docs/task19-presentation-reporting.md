# Task 19 presentation and reporting foundation

The desktop shell provides deterministic navigation for Dashboard, Payroll, Configuration, Scenarios and Reports. `CompanyPresentationContext` clears payroll selections on company change. `PayrollRevisionContext` carries Company, Period, Business Date, Snapshot, exact Revision, Calculation Run and Approval State; refresh code must reuse that identity rather than resolve latest.

Localization uses resource keys with `en-US` and `vi-VN`. Missing keys return the key plus a diagnostic instead of throwing. Appearance supports System, Light and Dark and XAML consumes centralized semantic resources. `IconKey` and `IIconProvider` keep feature ViewModels independent from media paths; SVG assets are replaceable and use `currentColor`.

Reporting consumes `PayrollReportSource` projections, never Attendance/Performance persistence or current policy. A request pins the immutable historical identity. The built-in portable-text renderer is deterministic and culture-aware and proves Payroll Summary, Payroll Subject Detail/Explain and Payroll Settlement Summary. Presentation selects the export destination after receiving bytes. PDF/DevExpress preview can be added through `IPayrollReportRenderer` without changing contracts.

Actipro packages are not present in the repository, so the shell uses the documented standard-Avalonia fallback. A licensed Ribbon/Backstage adapter belongs only in the Desktop project.
