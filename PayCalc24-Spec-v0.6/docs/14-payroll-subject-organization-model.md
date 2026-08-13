# PayCalc24 — Payroll Subject & Organization Model

**Specification Pack:** v0.5  
**Scope:** Task 04 foundation  
**Status:** Draft for implementation

## 1. Objective

PayCalc24 must not reuse the legacy iBHXH employee schema as its internal payroll domain model.

The existing iBHXH tables remain valid legacy/source-system data. PayCalc24 introduces a clean canonical payroll-facing model and accesses legacy/external employee data through mapping/provider contracts.

```text
External Employee Master
(iBHXH / Odoo / HRM / Excel / API)
          ↓
Employee Provider / Mapping
          ↓
Canonical PayrollSubject
          ↓
PayrollAssignment
          ↓
Organization / Position / JobGrade / Compensation
```

PayCalc24 owns **PayrollSubject and PayrollAssignment**. It does not need to own a full HRM employee profile.

## 2. Legacy iBHXH source boundary

Current iBHXH source tables:

```text
nhanvien
phongban
vitricongviec
```

The legacy employee table mixes personal identity, tax, insurance, employment contract, banking, payroll/salary fields, allowances and synchronization references. The legacy position table also mixes position identity with salary, insurance salary, allowances, shifts, overtime and product/revenue compensation values.

Therefore:

1. Do not map the legacy tables 1:1 into PayCalc24 schema.
2. Do not rename/migrate legacy iBHXH columns as part of Task 04.
3. Do not make PayCalc24 Domain depend on legacy table names.
4. Treat iBHXH as one Employee Provider / legacy adapter.
5. Preserve iBHXH production compatibility.

## 3. PayrollSubject

### Purpose

`PayrollSubject` represents a person who participates in payroll for a Company. It is intentionally smaller than a full Employee Master.

### Proposed fields

```text
PayrollSubject
--------------
Id                    UUID            technical primary key
CompanyId             CompanyId       company scope
EmployeeCode          string          company employee code
FullName              string          display/reference name
NationalIdType        enum/string     CCCD / PASSPORT / OTHER
NationalId            string?         national identifier
AttendanceCode        string?         time-attendance code
EmploymentStatus      enum/string
SourceSystem          string?         IBHXH / ODOO / HRM / EXCEL / API / MANUAL
ExternalEmployeeId    string?         source-system reference
EffectiveFrom         DateOnly
EffectiveTo           DateOnly?
Status                lifecycle/status
```

### Identity rules

Technical identity:

```text
PayrollSubject.Id
```

Company business identity:

```text
UNIQUE (CompanyId, EmployeeCode)
```

`NationalId` is a strong matching identifier but is not the database primary key. Do not use `FullName` as an identity key.

### National ID rules

Initial types:

```text
CCCD
PASSPORT
OTHER
```

Requirements:
- use as strong matching/synchronization data;
- do not place full NationalId in ordinary logs, correlation IDs or URLs;
- UI may mask NationalId according to permission/use case;
- do not assume global uniqueness across all Companies as a database PK.

## 4. EmployeeDependent

### Purpose

Do not store only a static `DependentCount` on PayrollSubject. Dependents are separate records because eligibility changes over time and each dependent can have different periods.

### Proposed fields

```text
EmployeeDependent
-----------------
Id                    UUID
CompanyId
PayrollSubjectId
DependentCode         string?
FullName              string
Relationship          string/code
DateOfBirth           DateOnly?
NationalId            string?
EligibilityFrom       DateOnly?
EligibilityTo         DateOnly?
DeductionFrom         DateOnly?
DeductionTo           DateOnly?
EligibilityStatus     string/enum
EligibilityReason     string?
SourceSystem          string?
ExternalId            string?
EffectiveFrom         DateOnly
EffectiveTo           DateOnly?
Status
```

### Derived dependent count

For a Payroll Period, eligible count is derived:

```text
PayrollPeriod
+ EmployeeDependent
+ Effective/Eligibility Rules
→ DEPENDENT_COUNT
```

Do not carry forward the previous month's integer as source-of-truth.

### Policy boundary

Age/relationship eligibility must not be hard-coded in PayrollSubject. Legal/tax rules are policy/provider logic. The data model stores identity, relationship and effective/eligibility periods.

## 5. OrganizationUnit

Use a hierarchical organization model rather than a flat Department-only table.

```text
Company
 └─ Division / Center
     └─ Department
         └─ Team
             └─ Sub-team
```

Proposed fields:

```text
OrganizationUnit
----------------
Id
CompanyId
Code
Name
ParentId?
UnitType
ManagerPayrollSubjectId?
EffectiveFrom
EffectiveTo?
Status
SortOrder?
```

Suggested initial `UnitType` values:

```text
DIVISION
CENTER
DEPARTMENT
TEAM
PROJECT
OTHER
```

Rules:
- parent and child must belong to the same Company;
- prevent circular parent relationships;
- closing a unit must not destroy historical assignments;
- historical unit identity must remain resolvable for locked payroll periods.

## 6. Position

`Position` represents job/role identity only.

```text
Position
--------
Id
CompanyId
Code
Name
Description?
ManagementLevel?
EffectiveFrom
EffectiveTo?
Status
```

Do **not** store salary, insurance salary, allowance, overtime rules or working-hour policies in Position. Those belong to Pay Components, Compensation Schemes, Attendance Policies, Payroll Inputs or Formula/Rule catalogs.

## 7. JobGrade

```text
JobGrade
--------
Id
CompanyId
Code
Name
Level?
Description?
EffectiveFrom
EffectiveTo?
Status
```

A Position may be used with multiple Job Grades.

```text
SOFTWARE_ENGINEER
  ├─ L1
  ├─ L2
  ├─ L3
  └─ EXPERT
```

## 8. PayrollAssignment

### Purpose

`PayrollAssignment` answers: during this business period, where does this PayrollSubject belong and what payroll/compensation context applies?

### Proposed fields

```text
PayrollAssignment
-----------------
Id
CompanyId
PayrollSubjectId
OrganizationUnitId
PositionId
JobGradeId?
CompensationSchemeId?     future reference; Task 05 owns scheme behavior
FixedCompensationFlag?
EffectiveFrom
EffectiveTo?
Status
SourceSystem?
ExternalAssignmentId?
```

### History rule

Do not overwrite historical department/position/grade when a person transfers.

```text
EMP001
01/01/2027 → 30/06/2027   DEV01 / SOFTWARE_ENGINEER / L2
01/07/2027 → ...          DEV03 / TECH_LEAD / L3
```

Reuse the Task 03 effective-dating framework.

### Overlap rule

By default a PayrollSubject has one primary active PayrollAssignment at a business date. Multi-assignment, if introduced later, must be explicit policy and must not create ambiguous primary payroll assignment.

## 9. Employee Provider / Canonical Mapping Contract

PayCalc24 must support employee sources without changing PayrollSubject.

Suggested boundary:

```text
IEmployeeProvider
-----------------
GetSubjects(...)
GetSubject(...)
SyncSubjects(...)
```

Canonical transfer record:

```text
EmployeeProviderRecord
----------------------
SourceSystem
ExternalEmployeeId
EmployeeCode
FullName
NationalIdType
NationalId
AttendanceCode
EmploymentStatus
```

Future providers may include:

```text
IBHXHEmployeeProvider
OdooEmployeeProvider
ExcelEmployeeProvider
GenericApiEmployeeProvider
```

Task 04 defines the canonical/provider contract but does not implement every integration.

## 10. Initial iBHXH legacy mapping

Suggested mapping from legacy `nhanvien`:

```text
MANV                → EmployeeCode
HoTen               → FullName
CMND                → NationalId
MaChamCong          → AttendanceCode
TinhTrangCongTac    → EmploymentStatus
ID / GUID           → ExternalEmployeeId candidates
GUID_ODOO           → external integration mapping/reference
```

Organization mapping:

```text
MaPhongBan / PhongBanCTac
        ↓
OrganizationUnit.Code / source mapping
```

Position mapping:

```text
MaViTri
        ↓
Position.Code / source mapping
```

Do not copy all legacy `nhanvien` columns into PayrollSubject.

## 11. Legacy `phongban` mapping

Legacy `phongban` is a source catalog only.

```text
MaPhongBan   → OrganizationUnit.Code
TenPhongBan  → OrganizationUnit.Name
ID/GUID      → source/external reference
```

PayCalc24 organization hierarchy may be enriched independently from the legacy flat structure.

## 12. Legacy `vitricongviec` mapping

Only identity/reference fields should seed Position:

```text
MaViTri      → Position.Code
TenViTri     → Position.Name
ID/GUID      → source/external reference
```

Do not map these directly into Position:

```text
MucLuongCB
MucLuongBHXH
allowances
shift times
overtime values
daily/hourly salary values
product/revenue compensation fields
```

Those belong to later compensation/attendance/formula modules.

## 13. Search & HR usability

Payroll Subject search should support at least:

```text
EmployeeCode
FullName
NationalId
AttendanceCode
```

UI should display at minimum:

```text
EmployeeCode
FullName
Organization
Position
JobGrade
EmploymentStatus
```

NationalId may be masked in list views.

## 14. Dependent-entry UX

Use an editable grid or fast-entry form, not a long dependent profile.

Minimum input:

```text
FullName
Relationship
DateOfBirth
DeductionFrom
```

System may propose `DeductionTo` from a configured policy when available. HR override must be permissioned/audited. Import Center should later support Excel/API batch import.

## 15. Privacy and audit

Sensitive identifiers include NationalId and dependent NationalId.

Requirements:
- never emit full sensitive identifiers in ordinary logs;
- access/display follows permissions;
- import/mapping changes are auditable;
- historical payroll references remain stable even if display data changes later.

## 16. Task 04 implementation boundary

Task 04 should implement:
- PayrollSubject;
- EmployeeDependent;
- OrganizationUnit hierarchy;
- Position;
- JobGrade;
- PayrollAssignment;
- effective dating/company isolation;
- validation;
- employee-provider/canonical mapping contracts;
- persistence/API/application/architecture tests needed by this foundation.

Task 04 should **not** implement:
- Pay Components;
- compensation calculations;
- dependent tax deduction amounts;
- tax law engine;
- full iBHXH/Odoo integration;
- Formula Engine;
- Attendance calculation;
- KPI calculation;
- later UI feature implementation.

## 17. Acceptance criteria

Task 04 is complete when:
1. PayrollSubject is company-scoped and uniquely identified by `CompanyId + EmployeeCode`.
2. FullName and NationalId are available without turning PayCalc24 into a full HRM.
3. EmployeeDependent supports effective/eligibility periods.
4. Dependent count can be derived by business date/Payroll Period.
5. OrganizationUnit supports hierarchy without cycles.
6. Position contains role identity only, not payroll-policy fields.
7. JobGrade is separate from Position.
8. PayrollAssignment preserves transfer/history using Task 03 temporal semantics.
9. Ambiguous overlapping primary assignments are rejected.
10. Legacy iBHXH table names/types do not leak into Domain contracts.
11. Canonical Employee Provider contracts allow iBHXH/Odoo/Excel/API sources later.
12. Company isolation and architecture tests pass.
13. Commit is pushed and GitHub CI is green before Task 04 is closed.
