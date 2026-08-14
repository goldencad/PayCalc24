# Task 20B — Desktop Operational UX

Maintenance baseline: `6446492e08149564b8bd88655a96e2939e70ab50`

Task 20B is ported directly onto the Task 20 maintenance shell. The elevated `RibbonHost`, real Ribbon navigation, and Backstage composition from that maintenance commit are preserved; the structured operational workspaces are layered beneath that shell. Actipro Pro resources are loaded through `ModernTheme Includes="Pro"`, ensuring popup/menu theme tokens are generated before Ribbon menus open.

The default Avalonia window now uses the official Actipro Avalonia Pro 25.2.0 Ribbon and a usable Backstage. The primary left navigation has been removed. Ribbon commands select all operational workspaces, while Backstage exposes runtime language (en-US/vi-VN), runtime theme (System/Light/Dark), application information, and clean exit.

Demo projections are rendered through explicit row templates for subjects, payroll inputs, attendance, KPI, dynamic components, funds, validation findings, statutory values, variance, approval, accounting, and pinned reports. No record relies on default `ToString()` presentation. Missing statutory amounts remain nullable and visibly `UNAVAILABLE`.

The client remains presentation-only, references only public Contracts, and contains no database access, payroll engine, fixed P1/P2/P3 presentation model, or database migration. The lightweight semantic SVG catalog remains replaceable and falls back to `Missing` for unknown keys.

## Verification

- Release client build: 0 warnings, 0 errors.
- Application tests: 188 passed.
- Architecture tests: 32 passed.
- Total: 220 passed.
- macOS ARM64 self-contained publish: PASS.
- Windows x64 self-contained publish: PASS.
- Windows native smoke: NOT EXECUTED (no Windows runtime).
- Real macOS ARM64 GUI smoke on commit `bcb2470180509757f6d823720bccf4dce6ba571a`: PASS. The maintained Actipro Ribbon and Backstage render; the former chevron crash is fixed; structured workspaces remain reachable across all seven tabs; en-US/vi-VN and System/Light/Dark switch visibly at runtime; clean Exit works.

No production MariaDB, login, licensing adapter, signing, or notarization work was performed.
