# PayCalc24 Specification Pack

**Version:** 0.3 Draft  
**Target:** .NET 9 / ASP.NET Core 9 / Avalonia / Actipro / MariaDB  
**Architecture:** Modular Monolith / Contract-first / Configuration-driven  
**Purpose:** Payroll, Performance & Compensation Platform

PayCalc24 is a company-scoped, configuration-driven payroll calculation platform. It is not intended to replace a full HRM. It calculates compensation, attendance-derived inputs, KPI/performance, payroll funds, variable pay/P3, Gross Pay, and integrates with statutory/accounting providers for insurance, PIT, Net Pay and downstream accounting.

## Technical baseline
- Runtime/backend: .NET 9 and ASP.NET Core 9.
- Primary desktop client: Avalonia UI on .NET 9.
- UI component suite: Actipro Avalonia where appropriate.
- Primary relational database: MariaDB through the approved EF Core provider.
- Supported desktop targets: Windows, macOS ARM64/x64 and Linux.
- Client communicates through PayCalc24 application/API contracts; UI never accesses MariaDB directly.
- Architecture is a modular monolith: modules have explicit contracts and do not reach into each other's persistence models.
- Dynamic formulas/rules are stored and versioned in MariaDB but executed by a safe .NET Formula Engine.
- Media owns replaceable SVG icon packs; feature views reference semantic IconKeys, never hard-coded asset paths.

## Core principles
- Reuse TS24 Platform Core for User / Company / Permission / Company Context.
- Employee identity for payroll is `CompanyId + EmployeeCode`; detailed employee master may come from iBHXH or another provider.
- Company policies are configuration, not source-code logic. P1/P2/P3 are dynamic Pay Components.
- Effective dating, versioning, audit, explainability and immutable locked periods are mandatory.
- Calculation is deterministic and uses decimal arithmetic.
- Integrations use canonical contracts; PayCalc24 Core must not depend directly on Odoo, iBHXH, TaxOnline or ezBooks.
- Prefer replacement through contracts/registries over cross-module modification.
- UI visual assets and styling are replaceable without changing payroll business code.

## Documents
1. [Product Brief](docs/00-product-brief.md)
2. [Master Catalogs](docs/01-master-catalogs.md)
3. [Data Dictionary](docs/02-data-dictionary.md)
4. [Business Rules](docs/03-business-rules.md)
5. [API Contracts](docs/04-api-contracts.md)
6. [ERD](docs/05-erd.md)
7. [Use Cases & Screen Flow](docs/06-use-cases-screen-flow.md)
8. [UI Design System — Avalonia + Actipro](docs/07-ui-design-system.md)
9. [Avalonia Screen Wireframes](docs/08-avalonia-screen-wireframes.md)
10. [Modular Architecture](docs/09-modular-architecture.md)
11. [Dynamic Formula Repository & Function Extension](docs/10-dynamic-formula-repository.md)
12. [SVG Icon System & Media Asset Contract](docs/11-svg-icon-system.md)
13. [Implementation Plan](IMPLEMENTATION_PLAN.md)
14. [Codex/Agent Rules](AGENTS.md)

## Reference implementation
The TS24 payroll/P3 policies are the first reference policy pack. They must be encoded through catalogs, parameters, formulas, lookup tables and rules rather than hard-coded into the product core.

## Codex starting rule
Codex must read `AGENTS.md`, then the relevant `/docs` files, then execute tasks in `IMPLEMENTATION_PLAN.md` dependency order. Architecture boundaries, Formula Repository rules and IconKey rules are non-negotiable unless an ADR explicitly approves a change.
