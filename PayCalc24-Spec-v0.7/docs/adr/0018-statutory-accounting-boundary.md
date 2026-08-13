# ADR 0018 — Versioned statutory and accounting boundary

## Decision

Payroll policy, statutory policy and external-system mapping are separate boundaries. Frozen snapshot facts and immutable calculation/fund results form a canonical `StatutoryCalculationRequest`. A company-scoped provider registry resolves only the explicitly pinned jurisdiction, provider, implementation and policy versions. Providers return dynamic contribution, deduction and optional tax-band items; generic contracts contain no country-specific columns or legal rates.

Requests and results carry deterministic SHA-256 fingerprints. Exact retries reuse an immutable result; a changed payload under the same identity is rejected. Missing, unavailable and failed results are explicit states and block Net Pay finalization rather than becoming zero. Replay requires the historical provider/policy identity; Back-test and What-if can pin alternatives but remain non-authoritative.

Net Pay uses components classified for settlement and preserves calculated versus funded provenance. Employer Cost combines configured cost items with dynamic employer contributions. Both are immutable results tied to snapshot revision and run.

Accounting is a balanced canonical document of posting keys, not GL account numbers. Target adapters (including future Odoo or ezBooks adapters) own mappings and transport. Authoritative publish requires the exact locked production approval and is idempotent per company, target and key. Scenario results cannot be posted.

## Consequences

Historical results remain reproducible while compatible provider versions exist; an unavailable historical version produces a stable diagnostic and is never upgraded silently. Reference/test providers demonstrate the contract only and are not declarations of current legal rules. Corrections create revision N+1 and new statutory, settlement and accounting records.
