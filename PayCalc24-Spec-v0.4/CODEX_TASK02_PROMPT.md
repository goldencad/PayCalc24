# Codex Prompt — PayCalc24 Task 02 on Specification Pack v0.4

Task 01 is complete.

Before coding:
1. Read `AGENTS.md`.
2. Read `README.md`.
3. Read `docs/09-modular-architecture.md`.
4. Read `docs/11-svg-icon-system.md`.
5. Read `docs/12-localization-culture.md`.
6. Read `docs/13-ribbon-theme-architecture.md`.
7. Read Task 02 in `IMPLEMENTATION_PLAN.md`.
8. Inspect Task 01 and preserve its architecture unless v0.4 requires a correction; document material corrections with an ADR.

Implement Task 02 only.

Required outcomes:
- Company Context and Current User abstractions compatible with the TS24 Core boundary.
- Correlation/idempotency and audit writer contracts required by Task 02.
- User presentation preference contracts/model for `PreferredCulture` and `ThemeMode`.
- Theme modes: `SYSTEM`, `LIGHT`, `DARK`.
- Localization abstraction with deterministic fallback for `vi-VN` and `en-US`.
- Stable diagnostic codes + arguments suitable for localized client messages.
- Tests for company isolation, localization fallback, canonical-code invariance and no payroll-state side effects from culture/theme preference changes.

Architectural constraints:
- Do not hard-code UI strings.
- Do not translate canonical identifiers, API fields, formula/input/component codes.
- Do not implement payroll business rules.
- Do not build the complete Ribbon UI yet; only establish contracts/preferences needed by future Avalonia/Actipro shell work.
- Do not add direct MariaDB access to the Avalonia UI.
- Keep semantic SVG IconKey/IIconProvider architecture.
- Preserve modular-monolith boundaries and deterministic calculation constraints.
- Do not broaden into later implementation-plan tasks.

At completion:
- run build/tests/architecture tests;
- summarize changed contracts/schema;
- list ADRs if any;
- identify any Task 01 code adjusted for v0.4.
