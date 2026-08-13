using System.Globalization;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.PayrollCalculation.Model;
using PayCalc24.PayrollCalculation.Services;

namespace PayCalc24.ApplicationTests;

public sealed class Task10PayrollCalculationTests
{
    [Fact] public async Task FrozenSnapshotCalculatesDependenciesAndTransitionsPeriod()
    {
        var f=new Fixture();var (period,snapshot)=await f.Freeze();
        var run=await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.Production,"run-1",snapshot.SnapshotHash));
        Assert.Equal(PayrollCalculationRunStatus.SUCCEEDED,run.Status);
        Assert.Equal(PayrollPeriodStatus.CALCULATED,f.Periods.GetById(f.Company,period.Id).LifecycleStatus);
        var results=f.Calculation.ListComponentResults(f.Company,run.Id);
        Assert.Equal(new[]{"B","C","A"},results.Select(x=>x.ComponentCode));
        var byCode=results.ToDictionary(x=>x.ComponentCode);Assert.Equal(100m,byCode["A"].ResultValue!.DecimalValue);Assert.Equal(200m,byCode["B"].ResultValue!.DecimalValue);Assert.Equal(250m,byCode["C"].ResultValue!.DecimalValue);
        Assert.NotNull(byCode["B"].ExplainTraceJson);Assert.Equal(Fixture.FormulaBVersion,byCode["B"].FormulaVersionId);
        Assert.Contains(f.Resolver.EntryId,byCode["A"].InputLedgerEntryIds);Assert.False(string.IsNullOrWhiteSpace(run.ResultHash));
    }

    [Fact] public async Task CalculationUsesFrozenInputAndPinnedFormulaAfterLiveDrift()
    {
        var f=new Fixture();var (_,snapshot)=await f.Freeze();f.Resolver.InputValue=999m;f.Resolver.FormulaBExpression="A_RESULT * 99";
        var run=await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.Replay,"replay"));
        var byCode=f.Calculation.ListComponentResults(f.Company,run.Id).ToDictionary(x=>x.ComponentCode);Assert.Equal(100m,byCode["A"].ResultValue!.DecimalValue);Assert.Equal(200m,byCode["B"].ResultValue!.DecimalValue);Assert.Equal(250m,byCode["C"].ResultValue!.DecimalValue);
        Assert.Equal(PayrollCalculationRunStatus.SUCCEEDED,run.Status);
    }

    [Fact] public async Task SameIdempotencyRequestReturnsRunAndConflictFails()
    {
        var f=new Fixture();var (_,snapshot)=await f.Freeze();var first=await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.Replay,"same"));
        var duplicate=await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.Replay,"same"));Assert.Equal(first.Id,duplicate.Id);
        var ex=await Assert.ThrowsAsync<PayrollCalculationException>(async()=>await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.WhatIf,"same")));
        Assert.Equal("PAYROLL_CALCULATION.IDEMPOTENCY_CONFLICT",ex.Diagnostic.Code);
    }

    [Fact] public async Task RuntimeDependencyCycleFailsClosedAndDoesNotCalculatePeriod()
    {
        var f=new Fixture{Cycle=true};var (period,snapshot)=await f.Freeze();var run=await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.Production,"cycle"));
        Assert.Equal(PayrollCalculationRunStatus.FAILED,run.Status);Assert.Equal("PAYROLL_CALCULATION.COMPONENT_DEPENDENCY_CYCLE",run.FailureDiagnosticCode);
        Assert.Equal(PayrollPeriodStatus.FROZEN,f.Periods.GetById(f.Company,period.Id).LifecycleStatus);
    }

    [Fact] public async Task ResultHashIsCultureIndependent()
    {
        var f=new Fixture();var (_,snapshot)=await f.Freeze();var old=CultureInfo.CurrentCulture;try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("vi-VN");var a=await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.Replay,"vi"));
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");var b=await f.Calculation.StartAsync(new(f.Company,snapshot.Id,PayrollExecutionMode.Replay,"fr"));
            Assert.Equal(a.ResultHash,b.ResultHash);
        }finally{CultureInfo.CurrentCulture=old;}
    }

    private sealed class Fixture
    {
        public static readonly FormulaVersionId FormulaBVersion=FormulaVersionId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        public CompanyId Company{get;}=CompanyId.From(Guid.NewGuid());public Resolver Resolver{get;}=new();public PayrollPeriodService Periods{get;}public PayrollCalculationService Calculation{get;}public bool Cycle{set=>Resolver.Cycle=value;}
        public Fixture(){var context=new Context(Company);var user=new User();var correlation=new Correlation();Periods=new(context,user,correlation,new Audit(),TimeProvider.System,Resolver);Calculation=new(context,user,correlation,TimeProvider.System,Periods,Periods);}
        public async Task<(PayrollPeriodDto,PayrollCalculationSnapshotDto)> Freeze(){var period=await Periods.CreateAsync(new(Company,"2026-08",null,new(2026,8,1),new(2026,8,31),new(2026,8,31)));period=await Periods.PrepareAsync(Company,period.Id,period.Revision);var snapshot=await Periods.FreezeAsync(Company,period.Id,period.Revision,"freeze");return(period,snapshot);}
    }
    private sealed class Resolver:IPayrollSnapshotResolver
    {
        private static readonly PayComponentId A=PayComponentId.From(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));private static readonly PayComponentId B=PayComponentId.From(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));private static readonly PayComponentId C=PayComponentId.From(Guid.Parse("cccccccc-0000-0000-0000-000000000003"));
        private static readonly FormulaDefinitionId FormulaB=FormulaDefinitionId.From(Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111"));private static readonly FormulaDefinitionId FormulaC=FormulaDefinitionId.From(Guid.Parse("cccccccc-1111-1111-1111-111111111111"));
        public decimal InputValue{get;set;}=100m;public string FormulaBExpression{get;set;}="A_RESULT * 2";public bool Cycle{get;set;}public PayrollInputLedgerEntryId EntryId{get;}=PayrollInputLedgerEntryId.From(Guid.NewGuid());
        public PayrollSnapshotCandidate Resolve(CompanyId companyId,PayrollPeriodId payrollPeriodId,DateOnly businessDate)
        {
            var subjectId=PayrollSubjectId.From(Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));var scheme=CompensationSchemeId.From(Guid.Parse("dddddddd-0000-0000-0000-000000000001"));
            var subject=new SnapshotSubjectFact(subjectId,"E001",PayrollAssignmentId.From(Guid.NewGuid()),OrganizationUnitId.From(Guid.NewGuid()),null,null,scheme,new(2026,1,1),null,0,[]);
            var input=new SnapshotResolvedInput(subjectId,PayrollInputDefinitionId.From(Guid.NewGuid()),1,"A_INPUT",PayrollInputDataType.DECIMAL,PayrollInputUnitType.AMOUNT,PayrollInputAggregationType.LATEST,PayrollInputValue.Decimal(InputValue),[EntryId]);
            var components=new[]{new SnapshotPayComponentVersion(A,1,30,CalculationMethod.INPUT,null,"A",true,"A_INPUT",null,Cycle?[B]:[]),new SnapshotPayComponentVersion(B,1,10,CalculationMethod.FORMULA,FormulaB,"B",true,null,null,[A]),new SnapshotPayComponentVersion(C,1,20,CalculationMethod.FORMULA,FormulaC,"C",true,null,null,[B])};
            var formulas=new[]{new SnapshotFormulaVersion(FormulaB,Fixture.FormulaBVersion,1,new string('b',64),FormulaBExpression),new SnapshotFormulaVersion(FormulaC,FormulaVersionId.From(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),1,new string('c',64),"B_RESULT + 50")};
            return new(companyId,new([subject],[input]),new([new(scheme,1,components)],formulas,[],[],[]),[]);
        }
    }
    private sealed record Context(CompanyId CompanyId):ICompanyContext;
    private sealed class User:ICurrentUser{public UserId UserId{get;}=UserId.From(Guid.NewGuid());public bool HasPermission(string permissionCode)=>true;}
    private sealed class Correlation:ICorrelationContext{public string CorrelationId=>"task-10";public string? IdempotencyKey=>null;}
    private sealed class Audit:IAuditWriter{public ValueTask WriteAsync(AuditEntry entry,CancellationToken cancellationToken=default)=>ValueTask.CompletedTask;}
}
