# Payroll approval and correction workflow

```text
Calculate → Validate → Submit → Review → Approve → Lock
```

The case contains identities and deterministic hashes, never copied payroll amounts. Submit and approve read Task 15 for that exact immutable context; blocking findings stop the transition, while warnings do not. Lock calls the PayrollPeriod close boundary only after approval.

```text
Locked/Approved/Rejected Revision N
  → Adjustment Request (reason required)
  → Authorization
  → owning source/policy correction
  → Task 09 new frozen snapshot N+1
  → Task 10 calculation → Task 11 funding → Task 15 review
  → new Approval Case (supersedes Revision N)
```

Attendance and KPI corrections publish canonical derived inputs via the immutable Payroll Input Ledger. A manual adjustment uses the same versioned ledger mechanism. Scenario runs cannot enter this production workflow.
