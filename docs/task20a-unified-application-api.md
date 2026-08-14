# Task 20A — Unified Application API and Entitlement Guard

## Boundary

PayCalc24 has one canonical application boundary for human and machine clients. The
Avalonia desktop may call it in-process; `/api/v1` is the HTTP adapter used by the
external Agent Gateway and future clients. `IUnifiedApplicationService` invokes the
same `IApplicationOperationDispatcher` regardless of `PrincipalType` or channel.
PayCalc24 contains no Agent Gateway, natural-language parser or AI-specific payroll
handler.

## Authentication and request context

Authentication remains owned by the existing TS24 account/login system. The API
maps its authenticated claims to `ApplicationRequestContext`: company, actor user,
principal type, channel, correlation ID and optional idempotency, agent code and
requested-by metadata. AI virtual employees have independent normal user accounts.
Their permissions are ordinary role/permission claims. Channel and principal type
are audit metadata and never grant authority. Passwords are not API or business
contract fields.

The host intentionally ships with fail-closed composition adapters. Deployment must
replace them with the existing authentication, subscription, workflow and module
adapters; it must never replace them with an allow-all fallback.

## Effective access and capabilities

`ApplicationAccessGuard` calculates effective access from authenticated/enabled
account, normal permission, product/feature entitlement and the owning module's
workflow result. Protected mutations fail closed for expired, blocked or unavailable
entitlement. `ICapabilityService` calls this exact guard, so discovery and execution
return the same diagnostic category. Sensitive and irreversible actions expose
confirmation metadata, while confirmation conversation remains a client/Gateway job.

## API v1

Routes are rooted at `/api/v1`. They cover payroll input, attendance, KPI batch
validate/commit, prepare/freeze/calculate, scenarios, approval/reject/lock/adjustment,
report generation and accounting generation/publication. Metadata is available at
`/api/v1/metadata/actions`; allowed actions are at `/api/v1/capabilities`.
Queries for periods, subjects, validation, explain, fund, scenario, approval,
settlement and report results use the read actions published in the action catalog
and the same application dispatcher.

`X-Correlation-Id` and `Idempotency-Key` are propagated into canonical context.
Responses contain stable diagnostics and correlation identity; internal exceptions
are not returned.

## Structured KPI batch

The Gateway resolves natural language outside PayCalc24, then submits a bounded
structured batch containing period, KPI definition and rows keyed by
`PayrollSubjectId` or company-scoped `EmployeeCode`. It first calls
`PERFORMANCE.KPI.VALIDATE_BATCH`, reviews per-item diagnostics, then explicitly calls
`PERFORMANCE.KPI.COMMIT_BATCH` with an idempotency key. The adapter delegates mapping,
assignment, type/range and duplicate checks to Organization and Task 13 Performance
contracts. Display names are never authoritative and ambiguous subjects are rejected.

## Licensing and offline policy

`IProductEntitlementService` is an adapter over the existing online subscription
source; there is no second license database or migration. Active and existing grace
states can execute according to policy. Expired/blocked deny protected mutations.
Unavailable entitlement fails closed. Login, license/renewal and any policy-approved
read-only functions remain outside protected mutation gating. No new offline licensing
scheme is introduced.
