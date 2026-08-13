# ADR-0001: Modular monolith and dependency direction

- Status: Accepted
- Date: 2026-08-13

## Context

PayCalc24 needs a simple deployment model while keeping payroll features, calculation engines, persistence, providers, and desktop UI independently replaceable and testable.

## Decision

Use a modular monolith with separate .NET projects for the shared Domain, Contracts, Application layer, feature modules, FormulaEngine, MariaDB infrastructure, API composition root, and Avalonia client.

Dependencies point inward. Domain has no external dependencies. FormulaEngine may depend only on Domain. Feature modules communicate through public contracts and do not access another module's persistence. Infrastructure implements application/domain ports. API composes the server. Avalonia references public contracts and never persistence.

The MariaDB UUID storage representation is intentionally deferred until the first persisted company-scoped model is introduced; it must be selected once in a dedicated ADR before any migration is created.

Architecture tests enforce these rules from project references and forbidden source dependencies.

## Consequences

- Module contracts must be explicit before cross-module workflows are implemented.
- Some project count and build overhead are accepted in exchange for enforceable boundaries.
- Exceptions require a superseding ADR and corresponding architecture-test update.

## v0.4 clarification

Specification v0.4 makes localization, theme selection, Actipro Ribbon/Backstage, and semantic IconKey resolution architectural concerns. The Task 01 Avalonia shell therefore contains no hard-coded user-facing text, colors, or SVG paths. Task 02 establishes culture/theme/localization contracts; the complete Ribbon and icon-pack shell remains in the later UI-foundation task as explicitly scoped by the implementation plan.
