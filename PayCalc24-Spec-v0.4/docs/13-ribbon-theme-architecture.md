# PayCalc24 — Actipro Ribbon & Theme Architecture

## Application shell
Primary desktop shell uses Actipro Avalonia Ribbon/Bars with Backstage.

Suggested tabs:
- Payroll
- Attendance
- Performance
- Funds
- Simulation
- Reports
- Integration
- Setup

Backstage hosts global/non-daily actions:
Company/App Preferences, Language, Theme, Integration Profiles, Import/Export Templates, Help/About and Diagnostics.

## Command rule
Ribbon/context-menu/toolbar surfaces reuse the same Application Commands. No business logic exists in Ribbon handlers.

## Themes
Supported modes:
`SYSTEM`, `LIGHT`, `DARK`.

Use Avalonia/Actipro theme infrastructure and centralized semantic resources. Changing theme must not affect calculation state or unsaved payroll business data.

## Localization
Ribbon tab/group/button captions use localization resource keys. IconKeys remain language-neutral.

## SVG
Ribbon and shared controls resolve icons through `IconKey`/`IIconProvider`. Media can replace SVG packs without changing feature code.

## Cross-platform
Smoke test Ribbon, SVG, high-DPI/Retina, localization expansion and Light/Dark/System on supported Windows/macOS/Linux targets.
