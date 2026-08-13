using System.Globalization;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.Temporal;
using PayCalc24.Organization.Model;
using PayCalc24.Organization.Services;

#pragma warning disable CA1861

namespace PayCalc24.ApplicationTests;

public sealed class Task04OrganizationTests
{
    private static readonly DateOnly Jan1=new(2027,1,1), Jul1=new(2027,7,1);
    [Fact] public void EmployeeCodeIsUniqueWithinCompanyCaseInsensitively(){var f=new Fixture();f.Registry.AddSubject(f.Subject("EMP001"));var ex=Assert.Throws<OrganizationValidationException>(()=>f.Registry.AddSubject(f.Subject("emp001")));Assert.Equal(DiagnosticCodes.DuplicateEmployeeCode,ex.Diagnostic.Code);}
    [Fact] public void SameEmployeeCodeCanExistInAnotherCompanyButCurrentScopeRejectsIt(){var f=new Fixture();var other=CompanyId.From(Guid.NewGuid());var ex=Assert.Throws<OrganizationValidationException>(()=>f.Registry.AddSubject(f.Subject("EMP001",other)));Assert.Equal(DiagnosticCodes.CompanyScopeMismatch,ex.Diagnostic.Code);}
    [Fact] public void OrdinarySubjectDtoMasksNationalId(){var subject=new Fixture().Subject("EMP001",nationalId:"012345678901");var dto=subject.ToDto();Assert.Equal("********8901",dto.MaskedNationalId);Assert.DoesNotContain("012345678901",dto.ToString());}
    [Fact] public void DerivedDependentCountUsesEffectiveEligibilityAndDeductionPeriods(){var f=new Fixture();var s=f.Subject("EMP001");f.Registry.AddSubject(s);f.Registry.AddDependent(s,f.Dependent(s,new(Jan1,null),new(Jan1,Jul1),new(Jan1,null)));f.Registry.AddDependent(s,f.Dependent(s,new(Jan1,null),new(Jul1,null),new(Jul1,null)));Assert.Equal(1,f.Registry.GetEligibleDependentCount(f.CompanyId,s.Id,Jan1));Assert.Equal(1,f.Registry.GetEligibleDependentCount(f.CompanyId,s.Id,Jul1));}
    [Fact] public void DependentCountDoesNotHardCodeAgeOrRelationshipRules(){var f=new Fixture();var s=f.Subject("EMP001");f.Registry.AddSubject(s);var d=new EmployeeDependent(EmployeeDependentId.From(Guid.NewGuid()),f.CompanyId,s.Id,null,"Any","CONFIGURED_RELATIONSHIP",new DateOnly(1900,1,1),null,new(Jan1,null),new(Jan1,null),new(Jan1,null),EligibilityStatus.ELIGIBLE);f.Registry.AddDependent(s,d);Assert.Equal(1,f.Registry.GetEligibleDependentCount(f.CompanyId,s.Id,Jan1));}
    [Fact] public void OrganizationRejectsCrossCompanyParent(){var f=new Fixture();var foreign=new OrganizationUnit(OrganizationUnitId.From(Guid.NewGuid()),CompanyId.From(Guid.NewGuid()),"X","X",null,OrganizationUnitType.TEAM,new(Jan1,null));var child=f.Unit("C",foreign.Id);var ex=Assert.Throws<OrganizationValidationException>(()=>f.Registry.AddUnit(child));Assert.Equal(DiagnosticCodes.InvalidOrganizationReference,ex.Diagnostic.Code);}
    [Fact] public void OrganizationRejectsCycles(){var f=new Fixture();var root=f.Unit("ROOT");var child=f.Unit("CHILD",root.Id);f.Registry.AddUnit(root);f.Registry.AddUnit(child);var ex=Assert.Throws<OrganizationValidationException>(()=>f.Registry.ReparentUnit(f.CompanyId,root.Id,child.Id));Assert.Equal(DiagnosticCodes.OrganizationCycle,ex.Diagnostic.Code);}
    [Fact] public void PrimaryAssignmentOverlapIsRejected(){var f=new Fixture();var c=f.WithCatalog();f.Registry.AddAssignment(f.Assignment(c,new(Jan1,null)));var ex=Assert.Throws<OrganizationValidationException>(()=>f.Registry.AddAssignment(f.Assignment(c,new(Jul1,null))));Assert.Equal(DiagnosticCodes.PrimaryAssignmentOverlap,ex.Diagnostic.Code);}
    [Fact] public void AdjacentAssignmentsResolveAndPreserveTransferHistory(){var f=new Fixture();var c=f.WithCatalog();var first=f.Assignment(c,new(Jan1,Jul1));var second=f.Assignment(c,new(Jul1,null));f.Registry.AddAssignment(first);f.Registry.AddAssignment(second);Assert.Same(first,f.Registry.ResolvePrimaryAssignment(f.CompanyId,c.Subject.Id,Jan1));Assert.Same(second,f.Registry.ResolvePrimaryAssignment(f.CompanyId,c.Subject.Id,Jul1));Assert.Equal([first,second],f.Registry.GetAssignmentHistory(f.CompanyId,c.Subject.Id));}
    [Theory][InlineData("en-US")][InlineData("vi-VN")] public void CultureDoesNotChangeIdentityOrTemporalResolution(string culture){var original=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new(culture);var f=new Fixture();var c=f.WithCatalog();var assignment=f.Assignment(c,new(Jan1,null));f.Registry.AddAssignment(assignment);Assert.Equal("EMP001",c.Subject.EmployeeCode);Assert.Same(assignment,f.Registry.ResolvePrimaryAssignment(f.CompanyId,c.Subject.Id,Jan1));}finally{CultureInfo.CurrentCulture=original;}}
    [Fact] public void PositionContainsRoleIdentityOnly(){var names=typeof(Position).GetProperties().Select(x=>x.Name).ToArray();Assert.DoesNotContain(names,n=>new[]{"Salary","Allowance","Overtime","Shift","InsuranceSalary","CompensationSchemeId"}.Any(x=>n.Contains(x,StringComparison.OrdinalIgnoreCase)));}
    [Fact] public void CanonicalContractsDoNotDependOnLegacyPersistenceTypes(){var assembly=typeof(IEmployeeProvider).Assembly;Assert.DoesNotContain(assembly.GetTypes(),t=>t.FullName?.Contains("nhanvien",StringComparison.OrdinalIgnoreCase)==true||t.FullName?.Contains("phongban",StringComparison.OrdinalIgnoreCase)==true||t.FullName?.Contains("vitricongviec",StringComparison.OrdinalIgnoreCase)==true);}

    private sealed class Fixture
    { public CompanyId CompanyId{get;}=CompanyId.From(Guid.NewGuid()); public OrganizationRegistry Registry{get;} public Fixture(){Registry=new(new Context(CompanyId));}
      public PayrollSubject Subject(string code,CompanyId? company=null,string? nationalId=null)=>new(PayrollSubjectId.From(Guid.NewGuid()),company??CompanyId,code,"Nguyen Van A",NationalIdType.CCCD,nationalId,"CC1",EmploymentStatus.ACTIVE,"MANUAL",null,new(Jan1,null));
      public EmployeeDependent Dependent(PayrollSubject s,EffectivePeriod effective,EffectivePeriod eligible,EffectivePeriod deduction)=>new(EmployeeDependentId.From(Guid.NewGuid()),CompanyId,s.Id,null,"Dependent","OTHER",null,null,effective,eligible,deduction,EligibilityStatus.ELIGIBLE);
      public OrganizationUnit Unit(string code,OrganizationUnitId? parent=null)=>new(OrganizationUnitId.From(Guid.NewGuid()),CompanyId,code,code,parent,OrganizationUnitType.DEPARTMENT,new(Jan1,null));
      public Catalog WithCatalog(){var subject=Subject("EMP001");var unit=Unit("DEV");var position=new Position(PositionId.From(Guid.NewGuid()),CompanyId,"DEV","Developer",new(Jan1,null));var grade=new JobGrade(JobGradeId.From(Guid.NewGuid()),CompanyId,"L2","Level 2",new(Jan1,null));Registry.AddSubject(subject);Registry.AddUnit(unit);Registry.AddPosition(position);Registry.AddJobGrade(grade);return new(subject,unit,position,grade);}
      public PayrollAssignment Assignment(Catalog c,EffectivePeriod period)=>new(PayrollAssignmentId.From(Guid.NewGuid()),CompanyId,c.Subject.Id,c.Unit.Id,c.Position.Id,c.Grade.Id,period);
    }
    private sealed record Catalog(PayrollSubject Subject,OrganizationUnit Unit,Position Position,JobGrade Grade); private sealed record Context(CompanyId CompanyId):ICompanyContext;
}
