# Codex Prompt — PayCalc24 Task 04 (Specification Pack v0.5)

## Baseline
Repository: `https://github.com/goldencad/PayCalc24.git`  
Branch: `main`  
Required baseline commit: `16b52b6d14aea463acbf23b8992fa686ecca382b`

Task 01–03 plus CI maintenance are complete. GitHub CI is green with 30/30 tests passing at the baseline.

Before coding:
1. checkout/fetch `main`;
2. confirm HEAD includes the required baseline;
3. confirm clean working tree;
4. read `AGENTS.md`;
5. read `README.md`;
6. read Task 04 in `IMPLEMENTATION_PLAN.md`;
7. read `docs/14-payroll-subject-organization-model.md` plus the relevant architecture/data/business-rule docs;
8. inspect and reuse Task 03 temporal/version primitives rather than inventing a parallel framework.

Implement Task 04 only.

## Required implementation

### PayrollSubject
Implement a canonical payroll subject with technical UUID Id, CompanyId, EmployeeCode, FullName, NationalIdType/NationalId, AttendanceCode, EmploymentStatus and source/external reference fields consistent with current architecture.

Rules:
- unique `CompanyId + EmployeeCode`;
- NationalId is strong matching data, not primary key;
- do not expose full NationalId in ordinary diagnostics/logs/URLs;
- do not create a full HRM employee profile.

### EmployeeDependent
Implement 1:N dependents with FullName, Relationship, DateOfBirth, optional NationalId, eligibility/deduction periods and effective dating.

Do not persist only `DependentCount` as source-of-truth. Provide a query/domain operation to derive eligible dependent count for a business date/Payroll Period context. Do not hard-code age/legal tax rules.

### OrganizationUnit
Implement hierarchical units with Code/Name/ParentId/UnitType/status/effective dating. Prevent cycles and cross-company parent-child relationships.

### Position
Implement role identity only. Do not add salary, insurance salary, allowance, overtime, shift or compensation calculation fields.

### JobGrade
Implement separate JobGrade.

### PayrollAssignment
Implement effective-dated assignment linking PayrollSubject to OrganizationUnit, Position and optional JobGrade. Preserve transfer/history. Reject ambiguous overlapping primary assignments by default.

### Canonical Employee Provider / Legacy Mapping Contract
Define the contracts/DTO boundary needed to later source employees from iBHXH, Odoo, Excel and generic API. Do not implement full integrations yet and do not leak legacy iBHXH table/column names into Domain contracts.

Legacy mapping intent for future adapter only:
- MANV → EmployeeCode
- HoTen → FullName
- CMND → NationalId
- MaChamCong → AttendanceCode
- TinhTrangCongTac → EmploymentStatus
- MaPhongBan/PhongBanCTac → Organization mapping
- MaViTri → Position mapping

Do not migrate/rename legacy production tables and do not copy legacy salary/allowance/shift/overtime fields into Position.

## Tests
Cover at minimum:
- company isolation;
- EmployeeCode uniqueness;
- sensitive NationalId behavior appropriate to structural validation;
- dependent effective periods and derived count;
- organization hierarchy/cycle rejection;
- cross-company parent rejection;
- assignment temporal resolution and overlap rejection;
- transfer history;
- no dependency on legacy iBHXH persistence types;
- localization/culture changes do not alter identity/effective-date results.

## Verification
Run:
`dotnet restore`
`dotnet build -c Release`
`dotnet test -c Release`

Local NuGet may still have the known `CookieContainer/GetDomainName` issue. Do not change dependencies to work around it. Push Task 04 and use GitHub CI as authoritative full verification.

Task 04 is not closed until commit is pushed and GitHub CI is green.

Do not implement Task 05.

Suggested commit message:
`feat: add payroll subject and organization foundation`

Final report must include baseline SHA, Task 04 SHA, push/working-tree status, GitHub CI URL/status, build warnings/errors, tests, schema/migrations, contracts/entities, diagnostics, ADRs and explicit confirmation that Task 05 was not implemented.
