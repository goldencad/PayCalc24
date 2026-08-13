using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Organization.Model;

public sealed class OrganizationValidationException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

public sealed class PayrollSubject
{
    private readonly List<EmployeeDependent> _dependents = [];
    public PayrollSubject(PayrollSubjectId id, CompanyId companyId, string employeeCode, string fullName,
        NationalIdType? nationalIdType, string? nationalId, string? attendanceCode, EmploymentStatus employmentStatus,
        string? sourceSystem, string? externalEmployeeId, EffectivePeriod effectivePeriod, RecordStatus status = RecordStatus.ACTIVE)
    {
        Id = id; CompanyId = companyId; EmployeeCode = Required(employeeCode, nameof(employeeCode)); FullName = Required(fullName, nameof(fullName));
        NationalIdType = nationalIdType; NationalId = Optional(nationalId); AttendanceCode = Optional(attendanceCode);
        EmploymentStatus = employmentStatus; SourceSystem = Optional(sourceSystem); ExternalEmployeeId = Optional(externalEmployeeId);
        EffectivePeriod = Valid(effectivePeriod); Status = status;
    }
    private PayrollSubject() { }
    public PayrollSubjectId Id { get; private set; }
    public CompanyId CompanyId { get; private set; }
    public string EmployeeCode { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public NationalIdType? NationalIdType { get; private set; }
    public string? NationalId { get; private set; }
    public string? AttendanceCode { get; private set; }
    public EmploymentStatus EmploymentStatus { get; private set; }
    public string? SourceSystem { get; private set; }
    public string? ExternalEmployeeId { get; private set; }
    public EffectivePeriod EffectivePeriod { get; private set; }
    public RecordStatus Status { get; private set; }
    public IReadOnlyCollection<EmployeeDependent> Dependents => _dependents;
    internal void AddDependent(EmployeeDependent dependent) => _dependents.Add(dependent);
    public PayrollSubjectDto ToDto() => new(Id, CompanyId, EmployeeCode, FullName, NationalIdType,
        SensitiveIdentifier.Mask(NationalId), AttendanceCode, EmploymentStatus, SourceSystem, ExternalEmployeeId, EffectivePeriod, Status);
    internal static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
    internal static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    internal static EffectivePeriod Valid(EffectivePeriod value) => value.EffectiveTo is not null && value.EffectiveFrom >= value.EffectiveTo.Value ? throw InvalidRange(value) : value;
    private static OrganizationValidationException InvalidRange(EffectivePeriod p) => new(new(DiagnosticCodes.InvalidEffectiveRange, DiagnosticSeverity.Error, new Dictionary<string, object?> { ["effectiveFrom"] = p.EffectiveFrom, ["effectiveTo"] = p.EffectiveTo }));
}

public sealed class EmployeeDependent
{
    public EmployeeDependent(EmployeeDependentId id, CompanyId companyId, PayrollSubjectId payrollSubjectId,
        string? dependentCode, string fullName, string relationship, DateOnly? dateOfBirth, string? nationalId,
        EffectivePeriod effectivePeriod, EffectivePeriod? eligibilityPeriod, EffectivePeriod? deductionPeriod,
        EligibilityStatus eligibilityStatus, string? eligibilityReason = null, string? sourceSystem = null, string? externalId = null,
        RecordStatus status = RecordStatus.ACTIVE)
    {
        Id=id; CompanyId=companyId; PayrollSubjectId=payrollSubjectId; DependentCode=PayrollSubject.Optional(dependentCode);
        FullName=PayrollSubject.Required(fullName,nameof(fullName)); Relationship=PayrollSubject.Required(relationship,nameof(relationship));
        DateOfBirth=dateOfBirth; NationalId=PayrollSubject.Optional(nationalId); EffectivePeriod=PayrollSubject.Valid(effectivePeriod);
        EffectivePeriod? eligible = eligibilityPeriod is null ? null : PayrollSubject.Valid(eligibilityPeriod.Value);
        EffectivePeriod? deduction = deductionPeriod is null ? null : PayrollSubject.Valid(deductionPeriod.Value);
        EligibilityFrom=eligible?.EffectiveFrom; EligibilityTo=eligible?.EffectiveTo;
        DeductionFrom=deduction?.EffectiveFrom; DeductionTo=deduction?.EffectiveTo; EligibilityStatus=eligibilityStatus;
        EligibilityReason=PayrollSubject.Optional(eligibilityReason); SourceSystem=PayrollSubject.Optional(sourceSystem); ExternalId=PayrollSubject.Optional(externalId); Status=status;
    }
    private EmployeeDependent() { }
    public EmployeeDependentId Id { get; private set; } public CompanyId CompanyId { get; private set; }
    public PayrollSubjectId PayrollSubjectId { get; private set; } public string? DependentCode { get; private set; }
    public string FullName { get; private set; } = ""; public string Relationship { get; private set; } = "";
    public DateOnly? DateOfBirth { get; private set; } public string? NationalId { get; private set; }
    public EffectivePeriod EffectivePeriod { get; private set; } public DateOnly? EligibilityFrom { get; private set; } public DateOnly? EligibilityTo { get; private set; }
    public DateOnly? DeductionFrom { get; private set; } public DateOnly? DeductionTo { get; private set; }
    public EffectivePeriod? EligibilityPeriod => EligibilityFrom is null ? null : new(EligibilityFrom.Value, EligibilityTo);
    public EffectivePeriod? DeductionPeriod => DeductionFrom is null ? null : new(DeductionFrom.Value, DeductionTo);
    public EligibilityStatus EligibilityStatus { get; private set; }
    public string? EligibilityReason { get; private set; } public string? SourceSystem { get; private set; } public string? ExternalId { get; private set; }
    public RecordStatus Status { get; private set; }
    public bool IsEligible(DateOnly businessDate) => Status == RecordStatus.ACTIVE && EligibilityStatus == EligibilityStatus.ELIGIBLE && EffectivePeriod.Contains(businessDate) && (EligibilityPeriod?.Contains(businessDate) ?? true) && (DeductionPeriod?.Contains(businessDate) ?? true);
}

public sealed class OrganizationUnit
{
    public OrganizationUnit(OrganizationUnitId id, CompanyId companyId, string code, string name, OrganizationUnitId? parentId,
        OrganizationUnitType unitType, EffectivePeriod effectivePeriod, RecordStatus status = RecordStatus.ACTIVE, int? sortOrder = null)
    { Id=id; CompanyId=companyId; Code=PayrollSubject.Required(code,nameof(code)); Name=PayrollSubject.Required(name,nameof(name)); ParentId=parentId; UnitType=unitType; EffectivePeriod=PayrollSubject.Valid(effectivePeriod); Status=status; SortOrder=sortOrder; }
    private OrganizationUnit() { }
    public OrganizationUnitId Id { get; private set; } public CompanyId CompanyId { get; private set; } public string Code { get; private set; }=""; public string Name { get; private set; }="";
    public OrganizationUnitId? ParentId { get; private set; } public OrganizationUnitType UnitType { get; private set; }
    public EffectivePeriod EffectivePeriod { get; private set; } public RecordStatus Status { get; private set; } public int? SortOrder { get; private set; }
    internal void Reparent(OrganizationUnitId? parentId) => ParentId=parentId;
}

public sealed class Position
{
    public Position(PositionId id, CompanyId companyId, string code, string name, EffectivePeriod effectivePeriod, string? description=null, string? managementLevel=null, RecordStatus status=RecordStatus.ACTIVE)
    { Id=id;CompanyId=companyId;Code=PayrollSubject.Required(code,nameof(code));Name=PayrollSubject.Required(name,nameof(name));EffectivePeriod=PayrollSubject.Valid(effectivePeriod);Description=PayrollSubject.Optional(description);ManagementLevel=PayrollSubject.Optional(managementLevel);Status=status; }
    private Position() { } public PositionId Id{get;private set;} public CompanyId CompanyId{get;private set;} public string Code{get;private set;}=""; public string Name{get;private set;}=""; public string? Description{get;private set;} public string? ManagementLevel{get;private set;} public EffectivePeriod EffectivePeriod{get;private set;} public RecordStatus Status{get;private set;}
}

public sealed class JobGrade
{
    public JobGrade(JobGradeId id, CompanyId companyId, string code, string name, EffectivePeriod effectivePeriod, string? level=null, string? description=null, RecordStatus status=RecordStatus.ACTIVE)
    { Id=id;CompanyId=companyId;Code=PayrollSubject.Required(code,nameof(code));Name=PayrollSubject.Required(name,nameof(name));EffectivePeriod=PayrollSubject.Valid(effectivePeriod);Level=PayrollSubject.Optional(level);Description=PayrollSubject.Optional(description);Status=status; }
    private JobGrade() { } public JobGradeId Id{get;private set;} public CompanyId CompanyId{get;private set;} public string Code{get;private set;}=""; public string Name{get;private set;}=""; public string? Level{get;private set;} public string? Description{get;private set;} public EffectivePeriod EffectivePeriod{get;private set;} public RecordStatus Status{get;private set;}
}

public sealed class PayrollAssignment
{
    public PayrollAssignment(PayrollAssignmentId id, CompanyId companyId, PayrollSubjectId payrollSubjectId, OrganizationUnitId organizationUnitId,
        PositionId positionId, JobGradeId? jobGradeId, EffectivePeriod effectivePeriod, bool isPrimary=true, RecordStatus status=RecordStatus.ACTIVE,
        string? sourceSystem=null, string? externalAssignmentId=null)
    { Id=id;CompanyId=companyId;PayrollSubjectId=payrollSubjectId;OrganizationUnitId=organizationUnitId;PositionId=positionId;JobGradeId=jobGradeId;EffectivePeriod=PayrollSubject.Valid(effectivePeriod);IsPrimary=isPrimary;Status=status;SourceSystem=PayrollSubject.Optional(sourceSystem);ExternalAssignmentId=PayrollSubject.Optional(externalAssignmentId); }
    private PayrollAssignment() { } public PayrollAssignmentId Id{get;private set;} public CompanyId CompanyId{get;private set;} public PayrollSubjectId PayrollSubjectId{get;private set;} public OrganizationUnitId OrganizationUnitId{get;private set;} public PositionId PositionId{get;private set;} public JobGradeId? JobGradeId{get;private set;} public EffectivePeriod EffectivePeriod{get;private set;} public bool IsPrimary{get;private set;} public RecordStatus Status{get;private set;} public string? SourceSystem{get;private set;} public string? ExternalAssignmentId{get;private set;}
}
