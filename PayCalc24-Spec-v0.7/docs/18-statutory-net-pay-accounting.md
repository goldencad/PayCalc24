# Statutory, settlement and accounting composition

```text
Frozen Calculation → Funded Results → Versioned Statutory Providers
                                      ↓
                         Net Pay + Employer Cost
                                      ↓
                     Balanced Accounting Document
                                      ↓
                        External Target Adapter
```

Only canonical frozen facts cross the provider boundary. Provider and policy versions, jurisdiction, business date, request hash and result hash are pinned. Dynamic item codes represent contributions and deductions; generic Core has no jurisdiction-specific law or fixed statutory columns.

Missing/failed statutory output blocks settlement. Production accounting publishing requires the exact locked approval. Replay uses historical versions; Back-test and What-if reuse composition but cannot publish. Accounting adapters map canonical posting keys to the target chart of accounts and enforce idempotency.

Any reference or test provider uses explicit fixture parameters solely to prove architecture. It is not a statement of current legal rules.
