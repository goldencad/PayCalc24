# PayCalc24 — Localization & Culture Architecture

## Objective
PayCalc24 is localization-first. Initial UI locales are `vi-VN` and `en-US`; additional locales must be addable without payroll business-code changes.

## Canonical vs localized
Never translate canonical identifiers such as `EmployeeCode`, `InputCode`, `FormulaCode`, `PayComponentCode`, API fields or database keys.

Localize labels, captions, tooltips, help, validation messages, Ribbon captions, status display names and report headings.

## Resource model
Use stable keys such as:
`Common.Save`, `Ribbon.Payroll`, `PayrollPeriod.Calculate`,
`Attendance.Validation.EmployeeNotFound`.

Feature XAML/ViewModels must not hard-code Vietnamese/English UI strings.

## Runtime preferences
User preference:
- PreferredCulture
- fallback culture
- current UI culture

Language changes must not alter Company payroll data, calculation snapshots or policy versions.

## Culture-aware formatting
Internal values remain typed/canonical:
- money/percentages: decimal
- dates/times: typed values
- Company time zone/currency from configuration

Culture is applied only for UI/report formatting.

## Diagnostics
Backend/application emits stable diagnostic codes + arguments. Client localizes messages. Missing resource keys must not crash the app.

## Reports
Report fields remain canonical; captions can be localized. Locked report snapshots store output culture and report/calculation versions.

## Acceptance
- vi-VN and en-US resources work.
- Adding a third locale is resource/configuration work, not payroll rule changes.
- Culture changes never change calculation results.
