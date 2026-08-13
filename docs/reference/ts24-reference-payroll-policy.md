# TS24 reference payroll policy

This pack is executable reference/test configuration for Task 14. It is not production seed data and does not state a legal or contractual TS24 policy. The example values prove that the generic PayCalc24 architecture can express a realistic policy without business-code branches.

## Configuration map

The reference compensation scheme contains three ordinary pay components:

| Component | Generic calculation method | Configured source |
|---|---|---|
| `P1` | `INPUT` | `P1_AMOUNT` |
| `P2` | `INPUT` | attendance-derived `ATTENDANCE_SCORE` |
| `P3` | `FORMULA` | pinned safe formula consuming `FINAL_ACHIEVEMENT`, `P3_ELIGIBILITY`, and configured curve points |

`P3_FLOOR`, `P3_TARGET`, and `P3_MAXIMUM` are subject payroll inputs. Thresholds are values in the pinned `P3_CURVE` parameter-set revision. The v1 example uses 0.70/1.00/1.20. Its formula uses nested lazy `IF` and generic `INTERPOLATE`; gate failure returns zero, values between points interpolate, and values above the maximum threshold cap at the configured maximum.

Attendance publishes a canonical input through the Payroll Input Ledger. Performance consumes canonical facts and publishes `FINAL_ACHIEVEMENT` through that same boundary. Calculation only sees frozen snapshot inputs and never reads Attendance or KPI persistence.

The `IT_P3_POOL` example is a generic organization-scoped fund using `PROPORTIONAL` allocation. Calculated P3 demand remains an immutable component result; the funded amount is a separate immutable fund member allocation.

## Reproducibility proofs

The application tests freeze historical facts and policy v1 before calculation. Later live attendance/performance corrections do not alter replay. Back-test and what-if explicitly substitute policy v2 (0.60/0.95/1.30) while keeping historical facts unchanged. Selected scenarios run under `vi-VN`, `en-US`, and `fr-FR` with identical values and hashes.

A second company uses `BASE`, `ATT_ALLOWANCE`, `PERFORMANCE_BONUS`, `PRESENCE_POINTS`, `MERIT_INDEX`, `BONUS_ALLOWED`, thresholds 0.60/0.90/1.30, and `COMPANY_B_BONUS_POOL`. It executes through the same snapshot, formula, calculation, and fund services.

All identifiers, amounts, KPI/attendance codes, thresholds, and organization names in this document are deterministic test data only.
