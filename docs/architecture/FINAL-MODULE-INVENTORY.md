# Final Module Inventory

| Module | Purpose / public boundary | Immutable or versioned data | Extension point |
|---|---|---|---|
| Organization | Subjects, dependents, organization and assignment contracts | Effective-dated assignments | Employee provider/mapping |
| Compensation | Generic components and schemes | Published scheme/component versions | New configured component |
| Payroll Inputs | Typed append-only ledger | Entries and correction lineage | Input source |
| Formula Repository | Formula, parameter, lookup and rule lifecycle | Published versions/checksums | Repository implementation |
| Formula Engine | I/O-free decimal AST evaluation | Explain trace and engine version | Registered calculation function |
| Payroll Period/Snapshot | Lifecycle and pinned execution basis | Frozen revisions and hashes | Snapshot source |
| Calculation | Deterministic snapshot orchestration | Runs, subject and component results | Calculation contract adapter |
| Fund | Generic coverage/allocation | Fund/member allocation results | Allocation policy configuration |
| Attendance | Import, validate and derive inputs | Batches, facts and provenance | Source mapping/policy |
| Performance | KPI, weights and gates | Assignments/results/evaluations | KPI/gate configuration |
| Review | Validation, explain, variance and funding projections | Read-only provenance projections | Review source |
| Scenario | Replay/back-test/what-if orchestration | Isolated scenario snapshots/results | Override resolver |
| Approval | Submit/review/approve/lock and N+1 adjustment | Cases, events and fingerprints | Actor/authorization boundary |
| Integration/Statutory | Provider-neutral statutory, settlement and accounting | Provider results/delivery receipts | Jurisdiction/provider adapter |
| Reporting | Pinned report source and renderer boundary | Report provenance/hash | Renderer/report definition |
| Desktop | Avalonia shell, operational projections, localization/theme/icons | Selected exact revision only | Application-service adapter |
| Application API / Access Channel | Versioned HTTP and in-process access to the same application operations; canonical request context and capabilities | Correlation/idempotency and audit provenance | Authenticated client/channel adapter |
| Licensing / Entitlement | Shared fail-closed guard over the existing online entitlement source | No new payroll persistence | Online entitlement adapter |

Dependencies point toward public Contracts/Domain. Desktop, providers and persistence are outer adapters; Reporting has no Avalonia dependency.
The external Agent Gateway is a client of Application API, not a PayCalc24 module or payroll engine.

## Desktop operational workspaces

The Actipro Ribbon/Backstage shell hosts 17 presentation-only workspaces: Dashboard, Subjects, Inputs,
Attendance, KPI, Prepare, Calculate, Funds, Validate, Explain, Variance, Scenario, Approval, Settlement,
Accounting and Reports. `OperationalWorkspaceViewModel` owns selection, visible-row filtering, busy/error/empty
presentation and command capability consumption. `MainWindow.axaml` owns the theme-aware workspace templates.
Immutable calculation, review, approval, statutory, accounting and reporting projections remain authoritative;
Desktop formats and navigates them but does not derive business outcomes.
