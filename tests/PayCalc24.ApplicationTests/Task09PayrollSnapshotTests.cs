using System.Globalization;
using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.PayrollCalculation.Execution;
using PayCalc24.PayrollCalculation.Model;
using PayCalc24.PayrollCalculation.Services;

namespace PayCalc24.ApplicationTests;

public sealed class Task09PayrollSnapshotTests
{
    [Fact] public async Task FullLifecycleAndReopenCreateImmutableRevisions()
    {
        var f=new Fixture();var period=await f.Create();period=await f.Service.PrepareAsync(f.Company,period.Id,period.Revision);
        var first=await f.Service.FreezeAsync(f.Company,period.Id,period.Revision,"freeze-1");period=f.Service.GetById(f.Company,period.Id);
        period=await f.Service.MarkCalculatedAsync(f.Company,period.Id,period.Revision);period=await f.Service.CloseAsync(f.Company,period.Id,period.Revision);
        period=await f.Service.ReopenAsync(f.Company,period.Id,period.Revision,"Late approved attendance correction");f.Resolver.InputValue=10m;
        period=await f.Service.PrepareAsync(f.Company,period.Id,period.Revision);var second=await f.Service.FreezeAsync(f.Company,period.Id,period.Revision,"freeze-2");
        Assert.Equal(1,first.SnapshotRevision);Assert.Equal(2,second.SnapshotRevision);Assert.Equal(9m,first.HistoricalFacts.Inputs.Single().ResolvedValue.DecimalValue);Assert.Equal(10m,second.HistoricalFacts.Inputs.Single().ResolvedValue.DecimalValue);Assert.Equal(first,f.Service.GetByRevision(f.Company,period.Id,1));
    }
    [Fact] public async Task InvalidTransitionsAndEmptyReopenReasonAreRejected()
    {
        var f=new Fixture();var p=await f.Create();var jump=await Assert.ThrowsAsync<PayrollCalculationException>(async()=>await f.Service.MarkCalculatedAsync(f.Company,p.Id,p.Revision));Assert.Equal(DiagnosticCodes.InvalidPayrollPeriodTransition,jump.Diagnostic.Code);
        p=await f.Service.PrepareAsync(f.Company,p.Id,p.Revision);p=(await f.Service.FreezeAsync(f.Company,p.Id,p.Revision,"a") is not null)?f.Service.GetById(f.Company,p.Id):p;p=await f.Service.MarkCalculatedAsync(f.Company,p.Id,p.Revision);p=await f.Service.CloseAsync(f.Company,p.Id,p.Revision);
        var reason=await Assert.ThrowsAsync<PayrollCalculationException>(async()=>await f.Service.ReopenAsync(f.Company,p.Id,p.Revision," "));Assert.Equal(DiagnosticCodes.PayrollPeriodReopenReasonRequired,reason.Diagnostic.Code);
    }
    [Fact] public async Task PeriodIdentityIsCultureIndependentAndDuplicateScopeIsRejected()
    {
        var old=CultureInfo.CurrentCulture;try{foreach(var name in new[]{"en-US","vi-VN","fr-FR"}){CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);Assert.Equal("2026-08",PayrollPeriod.NormalizeCode(" 2026-08 "));}var f=new Fixture();await f.Create();var ex=await Assert.ThrowsAsync<PayrollCalculationException>(async()=>await f.Service.CreateAsync(new(f.Company,"another",null,new(2026,8,1),new(2026,8,31),new(2026,8,31))));Assert.Equal(DiagnosticCodes.DuplicatePayrollPeriodCode,ex.Diagnostic.Code);}finally{CultureInfo.CurrentCulture=old;}
    }
    [Fact] public async Task FreezeIsIdempotentAndOptimisticConcurrencyIsStable()
    {
        var f=new Fixture();var p=await f.Create();p=await f.Service.PrepareAsync(f.Company,p.Id,p.Revision);var first=await f.Service.FreezeAsync(f.Company,p.Id,p.Revision,"same");var retry=await f.Service.FreezeAsync(f.Company,p.Id,p.Revision,"same");Assert.Equal(first.Id,retry.Id);Assert.Single(f.Service.ListRevisions(f.Company,p.Id));
        var ex=await Assert.ThrowsAsync<PayrollCalculationException>(async()=>await f.Service.MarkCalculatedAsync(f.Company,p.Id,p.Revision));Assert.Equal(DiagnosticCodes.PayrollPeriodConcurrencyConflict,ex.Diagnostic.Code);
    }
    [Fact] public async Task BlockingPreparationDiagnosticsPreventFreeze()
    {
        var f=new Fixture();f.Resolver.Diagnostics=[new(DiagnosticCodes.PayrollPreparationRequiredInputMissing,DiagnosticSeverity.Error)];var p=await f.Create();p=await f.Service.PrepareAsync(f.Company,p.Id,p.Revision);var ex=await Assert.ThrowsAsync<PayrollCalculationException>(async()=>await f.Service.FreezeAsync(f.Company,p.Id,p.Revision,"blocked"));Assert.Equal(DiagnosticCodes.PayrollPreparationBlockingErrors,ex.Diagnostic.Code);
    }
    [Fact] public async Task CrossCompanyResolvedPackageIsRejected()
    {
        var f=new Fixture();f.Resolver.OverrideCompany=CompanyId.From(Guid.NewGuid());var p=await f.Create();var ex=await Assert.ThrowsAsync<PayrollCalculationException>(async()=>await f.Service.PrepareAsync(f.Company,p.Id,p.Revision));Assert.Equal(DiagnosticCodes.PayrollPreparationCrossCompanyReference,ex.Diagnostic.Code);
    }
    [Fact] public async Task SnapshotPinsProvenanceAndMapsOnlyPinnedExecutionContext()
    {
        var f=new Fixture();var p=await f.Create();p=await f.Service.PrepareAsync(f.Company,p.Id,p.Revision);var snapshot=await f.Service.FreezeAsync(f.Company,p.Id,p.Revision,"mapped");
        f.Resolver.InputValue=999m;var context=SnapshotExecutionContextMapper.Map(snapshot,f.Resolver.SubjectId,f.Resolver.FormulaDefinitionId,"replay");
        Assert.Equal(9m,context.Values["OVERTIME_HOURS"].AsDecimal());Assert.Equal(f.Resolver.EntryId,context.InputEntryIds!["OVERTIME_HOURS"].Single());Assert.Equal(f.Resolver.FormulaVersionId,context.FormulaVersionId);Assert.Null(typeof(SnapshotSubjectFact).GetProperty("NationalId"));
    }
    [Fact] public void CanonicalHashChangesForInputProvenanceAndPolicyButNotCulture()
    {
        var f=new Fixture();var a=f.Resolver.Resolve(f.Company,PayrollPeriodId.From(Guid.NewGuid()),new(2026,8,31));var period=PayrollPeriodId.From(Guid.NewGuid());var old=CultureInfo.CurrentCulture;try{var hashes=new List<string>();foreach(var culture in new[]{"en-US","vi-VN","fr-FR"}){CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);hashes.Add(SnapshotHasher.Hash(f.Company,period,1,new(2026,8,31),a).Snapshot);}Assert.Single(hashes.Distinct());f.Resolver.EntryId=PayrollInputLedgerEntryId.From(Guid.NewGuid());var b=f.Resolver.Resolve(f.Company,period,new(2026,8,31));Assert.NotEqual(hashes[0],SnapshotHasher.Hash(f.Company,period,1,new(2026,8,31),b).Snapshot);}finally{CultureInfo.CurrentCulture=old;}
    }

    private sealed class Fixture
    {
        public CompanyId Company{get;}=CompanyId.From(Guid.NewGuid());public CandidateResolver Resolver{get;}=new();public PayrollPeriodService Service{get;}
        public Fixture(){Service=new(new Context(Company),new User(),new Correlation(),new Audit(),TimeProvider.System,Resolver);}
        public ValueTask<PayrollPeriodDto> Create()=>Service.CreateAsync(new(Company,"2026-08","August",new(2026,8,1),new(2026,8,31),new(2026,8,31),new(2026,9,5)));
    }
    private sealed class CandidateResolver:IPayrollSnapshotResolver
    {
        public PayrollSubjectId SubjectId{get;}=PayrollSubjectId.From(Guid.NewGuid());public FormulaDefinitionId FormulaDefinitionId{get;}=FormulaDefinitionId.From(Guid.NewGuid());public FormulaVersionId FormulaVersionId{get;}=FormulaVersionId.From(Guid.NewGuid());public PayrollInputLedgerEntryId EntryId{get;set;}=PayrollInputLedgerEntryId.From(Guid.NewGuid());public decimal InputValue{get;set;}=9m;public IReadOnlyList<PreparationDiagnostic> Diagnostics{get;set;}=[];public CompanyId? OverrideCompany{get;set;}
        public PayrollSnapshotCandidate Resolve(CompanyId companyId,PayrollPeriodId payrollPeriodId,DateOnly businessDate)
        {
            var scheme=CompensationSchemeId.From(Guid.NewGuid());var subject=new SnapshotSubjectFact(SubjectId,"E001",PayrollAssignmentId.From(Guid.NewGuid()),OrganizationUnitId.From(Guid.NewGuid()),PositionId.From(Guid.NewGuid()),null,scheme,new(2026,1,1),null,2,[EmployeeDependentId.From(Guid.NewGuid()),EmployeeDependentId.From(Guid.NewGuid())]);
            var input=new SnapshotResolvedInput(SubjectId,PayrollInputDefinitionId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),1,"OVERTIME_HOURS",PayrollInputDataType.DECIMAL,PayrollInputUnitType.HOURS,PayrollInputAggregationType.SUM,PayrollInputValue.Decimal(InputValue),[EntryId]);
            var policy=new SnapshotPolicyConfiguration([new(scheme,1,[])],[new(FormulaDefinitionId,FormulaVersionId,1,new string('a',64))],[],[],[]);return new(OverrideCompany??companyId,new([subject],[input]),policy,Diagnostics);
        }
    }
    private sealed record Context(CompanyId CompanyId):ICompanyContext;
    private sealed class User:ICurrentUser{public UserId UserId{get;}=UserId.From(Guid.NewGuid());public bool HasPermission(string permissionCode)=>true;}
    private sealed class Correlation:ICorrelationContext{public string CorrelationId=>"task-09";public string? IdempotencyKey=>null;}
    private sealed class Audit:IAuditWriter{public ValueTask WriteAsync(AuditEntry entry,CancellationToken cancellationToken=default)=>ValueTask.CompletedTask;}
}
