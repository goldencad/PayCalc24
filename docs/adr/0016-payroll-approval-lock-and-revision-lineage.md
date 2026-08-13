# ADR 0016: Payroll approval, lock and revision lineage

## Status

Accepted for Task 17.

## Decision

`PayrollApprovalCase` pins an exact production snapshot revision, successful calculation run, snapshot/result hashes, ordered Fund result hashes and Task 15 review-context fingerprint. Its lifecycle is `DRAFT → SUBMITTED → IN_REVIEW → APPROVED → LOCKED`; review may instead end at `REJECTED`. Reject and adjustment require a reason. Lifecycle events are append-only, commands are idempotent, and transitions use optimistic revision checks.

Approval records the human decision over exact immutable content. Lock finalizes that decision and may close an existing `CALCULATED` PayrollPeriod through its owning boundary; `ApprovalCase.LOCKED` and `PayrollPeriod.CLOSED` remain distinct semantics. Authorization and optional separation-of-duties are policies outside the aggregate.

Approval is latest-production-only at decision and lock time. A stale case is never redirected to a newer snapshot, and Scenario results are rejected. Corrections create an authorized `PayrollAdjustmentRequest`, then call Task 09/10/11/15 boundaries to reopen where necessary, freeze snapshot revision N+1, calculate/fund/review, and create a new case that supersedes the prior case. Historical cases, snapshots, component results and fund results have no mutation API.

## Consequences

An approver and auditor can identify precisely what was decided. Concurrent approve/reject races have one winner. Attendance, KPI and manual-input corrections enter their owning append-only ledger boundaries and therefore prove N+1 without Task 17 duplicating calculation logic.
