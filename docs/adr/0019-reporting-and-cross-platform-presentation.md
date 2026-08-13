# ADR 0019 — Reporting and cross-platform presentation foundation

- Status: Accepted
- Date: 2026-08-13

## Decision

The Avalonia desktop client is a presentation-side composition root. Feature ViewModels consume public application contracts and immutable read projections; they never evaluate formulas, calculate payroll/Funds/statutory values, or bind persistence entities. Navigation, company context, pinned payroll revision state, culture, appearance, diagnostics and semantic icons are small independently testable presentation services. Company changes discard company-scoped selections. Historical workspace state always carries snapshot revision and calculation-run identity.

Actipro is isolated to a future Desktop adapter and is not referenced until licensed packages are available. The current standard-Avalonia shell is the compiling fallback. Avalonia, platform APIs and future Actipro types remain outside Domain, Application, engines and Reporting.

Reporting is independent of Avalonia. `PayrollReportRequest` pins Company, Period, Snapshot, Revision and Calculation Run. Reporting accepts only explicit immutable projections, returns content bytes rather than writing files, and records source/renderer/culture provenance. A deterministic portable-text renderer proves summary, subject Explain/settlement and settlement-summary output; a future DevExpress or PDF implementation replaces only `IPayrollReportRenderer`.

English and Vietnamese strings resolve by resource key. Canonical codes and values are never translated or parsed from display text. Appearance uses `SYSTEM`, `LIGHT` and `DARK` with semantic Avalonia resources. Feature state uses semantic `IconKey`; a centralized SVG provider maps keys to replaceable media, with a safe missing icon. No payroll schema migration is introduced.

## Dependency direction

```text
Desktop -> application/public contracts
Reporting -> immutable application/result projections
Infrastructure -> persistence and adapters

View code -/-> Formula, Calculation, Fund or Statutory engines
```

## Consequences

Task 20 can assemble operational screens without moving business behavior into UI. Commercial Ribbon/reporting integrations and OS-specific file pickers remain replaceable presentation adapters. Windows, macOS and Linux share the same ViewModels and reporting core.
