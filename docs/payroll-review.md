# Payroll Review

Task 15 adds four read-only application projections over explicitly selected immutable payroll results.

- Validation Summary aggregates source diagnostics without translating or renaming their codes and arguments. A finding blocks only when its source or review mapping explicitly marks it blocking.
- Explain Payroll links frozen historical facts to stored input provenance, component versions/results/traces, Attendance and Performance lineage, and Fund allocations. It does not rerun an engine or resolve current policy.
- Variance compares two explicit result contexts at subject, component, and funded-amount levels. Amounts use decimal arithmetic; a zero baseline has no percentage delta. Drivers are emitted only from differing provenance identities.
- Funding Review exposes available fund, eligible demand, funded/unfunded amounts, reserve, coverage, allocation method, members, hashes, and a generic review indicator from immutable Fund results.

`IPayrollReviewSource` supplies one batched `PayrollReviewDataset`. This keeps module ownership intact, supports query optimization without obvious N+1 access, and permits later production-versus-scenario comparisons without adding Task 16 persistence.

The module has no database migration, mutation command, localized explanation text, approval workflow, statutory calculation, or UI.
