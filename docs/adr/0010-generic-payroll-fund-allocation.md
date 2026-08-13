# ADR 0010: Generic payroll fund allocation

## Status

Accepted for Task 11.

## Decision

Payroll funding is a company-scoped, effective-dated configuration rather than a business-specific component. A stable definition code owns immutable published revisions. A revision declares a generic type, typed scope reference, deterministic source and allocation policy. Initial executable sources are `FIXED`, frozen snapshot `INPUT`, and pinned `FORMULA`; external/current provider access is forbidden. Initial allocation methods are proportional, weighted and priority. Other technical enum values define a future-safe contract but fail with a stable unsupported diagnostic.

Task 09 policy configuration is compatibly extended with fund-version snapshots. Production and replay calculate only a pinned fund revision and its frozen inputs/formula identity. Back-test and what-if use the same engine with an explicit alternative pinned policy, and may supply an explicit scenario amount. No calculation path resolves latest/current configuration or wall-clock business dates.

A requirement is an immutable calculation input with a canonical reference, company and optional subject/component references, requested and eligible amounts, explicit priority/weight/floor/target/cap metadata, eligibility and typed provenance. Target and cap constrain eligible demand. Floor is retained as policy metadata for future `CAP_FLOOR`; the currently supported strategies do not invent hidden floor precedence when the available fund cannot meet all floors.

Eligible demand is the sum of constrained eligible requirements. When demand is positive, raw coverage is `available / demand`; when demand is zero it is explicitly `1`. Effective funding ratio is `min(1, raw coverage)`, so coverage above 100% is informational and never overpays demand. Funded amount cannot exceed either demand or available fund. Unfunded amount is `max(0, demand - funded)` and reserve is `max(0, available - funded)`. Task 11 records reserve/deficit only and performs no carry-forward accounting.

Proportional and weighted strategies calculate decimal shares at high precision, round at the configured scale with the configured midpoint rule, then reconcile deterministically in priority and ordinal requirement-reference order by one scale unit. Priority funds that same order sequentially. Reconciliation never exceeds a member's eligible amount or the fund budget. Database/insertion order and randomness are not inputs.

Allocation and member results are immutable. They retain snapshot/run/period/fund identities, execution mode, source and requirement provenance, structured language-neutral JSON trace, semantic fund engine version `1.0.0`, and culture-independent SHA-256 hashes. Hashes include ordered calculation inputs and outputs but exclude timestamps, actors, localized labels and display culture. A company/snapshot/idempotency key makes exact retry stable; conflicting reuse fails. One authoritative production result per snapshot and fund revision prevents competing finalization. Persistence implementations commit header and all member details atomically.

Task 10 component results may be mapped into requirements through their immutable result IDs and provenance. The fund result adds `FundedAmount`; it never updates a Task 10 component value and does not define gross, net, tax, insurance or statutory payable amounts.

## Consequences

Codes such as `IT_P3_POOL`, `SALES_BONUS_POOL`, or `PROJECT_INCENTIVE_POOL` are configuration processed by identical code. Core contains no P3 branch or P3-specific class. Parent identity is modeled and cycles are rejected, while recursive fund transfer and future-period carry-forward remain deferred.
