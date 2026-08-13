# ADR 0014: Payroll review projections over immutable provenance

## Status

Accepted for Task 15.

## Decision

Payroll Review is a read-only module. It consumes an explicitly selected, company-scoped `PayrollReviewDataset` containing a frozen snapshot, immutable calculation and fund results, source diagnostics, and stored input provenance. The source contract is batched so infrastructure can avoid per-employee queries without exposing persistence entities or allowing cross-module joins in Domain.

Validation preserves each source module's diagnostic code, severity, canonical arguments, references, and explicit blocking semantic. Review adds diagnostic codes only for failures at its own query/projection boundary.

Explain Payroll projects stored component traces, version identities, hashes, input ledger lineage, Attendance/Performance provenance, and fund allocation traces into a language-neutral tree. It never invokes Formula, Attendance, Performance, Calculation, or Fund engines and never resolves current policy.

Variance compares two explicit immutable result contexts. Decimal deltas are deterministic, a zero prior value yields no percentage, and structural drivers are reported only when stored provenance identities differ. The same contracts can compare production, replay, back-test, or what-if result sets supplied by Task 16 later.

Funding Review classifies the already-recorded Fund result as fully funded, partially funded, no demand, or deficit. These are projection indicators and do not change Fund engine semantics.

No review tables or migration are introduced because every projection can be reproduced from immutable source results.

## Consequences

Historical explanations remain stable after policy or ledger corrections. Review cannot mutate payroll facts or results. A future optimized data source may batch reads while remaining behind `IPayrollReviewSource` and enforcing company isolation.
