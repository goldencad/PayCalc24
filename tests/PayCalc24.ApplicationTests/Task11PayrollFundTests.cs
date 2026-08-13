using System.Globalization;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.FormulaEngine.Execution;
using PayCalc24.PayrollFunds.Services;

namespace PayCalc24.ApplicationTests;

public sealed class Task11PayrollFundTests
{
    private static readonly decimal[] RoundedThirds=[33.34m,33.33m,33.33m];
    private static readonly string[] CanonicalReferences=["A","B","C"];
    private static readonly decimal[] ProportionalValues=[16m,24m,40m];
    [Fact] public async Task DefinitionIsCompanyScopedVersionedAndPublishedImmutable()
    {
        var company=CompanyId.From(Guid.NewGuid());var service=new PayrollFundDefinitionService(new Context(company),new User(),TimeProvider.System);
        var draft=await service.CreateDraftAsync(new(company,"it_variable_pool","Variable",null,PayrollFundType.VARIABLE,new(FundScopeType.COMPANY),new(FundSourceType.FIXED,80m),new(FundAllocationMethod.PROPORTIONAL),new(2026,1,1)));
        var published=await service.PublishAsync(company,draft.Id);Assert.Equal("IT_VARIABLE_POOL",published.Code);Assert.Equal(published.Id,service.ResolveEffective(company,"it_variable_pool",new(2026,6,1)).Id);
        var ex=await Assert.ThrowsAsync<PayrollFundException>(async()=>await service.UpdateDraftAsync(new(company,draft.Id,"Changed",null,PayrollFundType.VARIABLE,draft.Scope,draft.Source,draft.Policy,draft.EffectiveFrom)));
        Assert.Equal("PAYROLL_FUND.PUBLISHED_IMMUTABLE",ex.Diagnostic.Code);var revision=await service.CreateRevisionAsync(company,draft.Id);Assert.Equal(2,revision.Revision);Assert.Equal(published.Id,service.ResolveEffective(company,published.Code,new(2026,6,1)).Id);
    }

    [Fact] public async Task ProportionalCoverageAndRemainderAreDeterministic()
    {
        var f=new Fixture(100m,2);var requirements=new[]{f.Requirement("C",100m),f.Requirement("A",100m),f.Requirement("B",100m)};
        var result=await f.Service.CalculateAsync(f.Command("round",requirements));Assert.Equal(100m,result.FundedAmount);Assert.Equal(200m,result.UnfundedAmount);Assert.Equal(1m/3m,result.RawCoverageRatio);
        Assert.Equal(RoundedThirds,result.Members.Select(x=>x.AllocatedAmount));Assert.Equal(CanonicalReferences,result.Members.Select(x=>x.RequirementReference));
    }

    [Fact] public async Task CoverageBelowAndAboveOneNeverOverfunds()
    {
        var low=new Fixture(80m);var demand=new[]{low.Requirement("A",20m),low.Requirement("B",30m),low.Requirement("C",50m)};var a=await low.Service.CalculateAsync(low.Command("low",demand));
        Assert.Equal(.8m,a.RawCoverageRatio);Assert.Equal(ProportionalValues,a.Members.Select(x=>x.AllocatedAmount));
        var high=new Fixture(120m);var highDemand=new[]{high.Requirement("A",20m),high.Requirement("B",30m),high.Requirement("C",50m)};var b=await high.Service.CalculateAsync(high.Command("high",highDemand));Assert.Equal(1.2m,b.RawCoverageRatio);Assert.Equal(1m,b.EffectiveFundingRatio);Assert.Equal(100m,b.FundedAmount);Assert.Equal(20m,b.ReserveAmount);
    }

    [Fact] public async Task ZeroDemandAndFrozenInputHaveExplicitStableSemantics()
    {
        var f=new Fixture(80m,8,FundSourceType.INPUT);var result=await f.Service.CalculateAsync(f.Command("zero",[]));Assert.Equal(1m,result.RawCoverageRatio);Assert.Equal(0m,result.FundedAmount);Assert.Equal(80m,result.ReserveAmount);Assert.Contains(f.EntryId.Value.ToString(),result.SourceProvenanceJson,StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task SameEngineSupportsReplayAndBackTestWithoutProductionMutation()
    {
        var f=new Fixture(80m);var requirements=new[]{f.Requirement("A",20m),f.Requirement("B",30m),f.Requirement("C",50m)};
        var production=await f.Service.CalculateAsync(f.Command("production",requirements,PayrollExecutionMode.Production));
        var replay=await f.Service.CalculateAsync(f.Command("replay",requirements,PayrollExecutionMode.Replay));Assert.Equal(production.ResultHash,replay.ResultHash);
        var alternative=await f.Service.CalculateAsync(f.Command("alternative",requirements,PayrollExecutionMode.BackTest,100m,"11111111-1111-1111-1111-111111111111"));Assert.Equal(100m,alternative.FundedAmount);Assert.Equal(80m,production.FundedAmount);Assert.Equal(PayrollExecutionMode.BackTest,alternative.ExecutionMode);
    }

    [Fact] public async Task HashIsCultureIndependentAndIdempotencyConflictsFail()
    {
        var f=new Fixture(80m);var request=new[]{f.Requirement("A",100m)};var old=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("vi-VN");var first=await f.Service.CalculateAsync(f.Command("same",request,PayrollExecutionMode.Replay));var exact=await f.Service.CalculateAsync(f.Command("same",request,PayrollExecutionMode.Replay));Assert.Equal(first.Id,exact.Id);var ex=await Assert.ThrowsAsync<PayrollFundException>(async()=>await f.Service.CalculateAsync(f.Command("same",[f.Requirement("A",99m)],PayrollExecutionMode.Replay)));Assert.Equal("PAYROLL_FUND_CALCULATION.IDEMPOTENCY_CONFLICT",ex.Diagnostic.Code);}finally{CultureInfo.CurrentCulture=old;}
    }

    private sealed class Fixture
    {
        public CompanyId Company{get;}=CompanyId.From(Guid.NewGuid());public PayrollFundVersionId FundVersion{get;}=PayrollFundVersionId.From(Guid.NewGuid());public PayrollFundDefinitionId FundDefinition{get;}=PayrollFundDefinitionId.From(Guid.NewGuid());public PayrollInputLedgerEntryId EntryId{get;}=PayrollInputLedgerEntryId.From(Guid.NewGuid());public PayrollFundCalculationService Service{get;} private readonly Query query;
        public Fixture(decimal available,int scale=8,FundSourceType sourceType=FundSourceType.FIXED){var policy=new FundAllocationPolicy(FundAllocationMethod.PROPORTIONAL,scale);var source=sourceType==FundSourceType.INPUT?new FundSourceConfiguration(sourceType,InputCode:"FUND_INPUT"):new FundSourceConfiguration(sourceType,available);var fund=new SnapshotPayrollFundVersion(FundVersion,FundDefinition,"GENERIC_POOL",1,PayrollFundType.VARIABLE,new(FundScopeType.COMPANY),source,policy);query=new Query(Company,fund,sourceType==FundSourceType.INPUT?available:0m,EntryId);Service=new(new Context(Company),new Correlation(),TimeProvider.System,query,new SafeFormulaEngine());}
        public FundRequirement Requirement(string reference,decimal amount)=>new(reference,Company,amount);
        public CalculatePayrollFund Command(string key,IReadOnlyList<FundRequirement> requirements,PayrollExecutionMode mode=PayrollExecutionMode.Replay,decimal? explicitAmount=null,string? scenario=null)=>new(Company,query.Snapshot.Id,FundVersion,mode,key,requirements,null,scenario,explicitAmount);
    }
    private sealed class Query:IPayrollSnapshotQueryService
    {
        public PayrollCalculationSnapshotDto Snapshot{get;} public Query(CompanyId company,SnapshotPayrollFundVersion fund,decimal input,PayrollInputLedgerEntryId entry){var period=PayrollPeriodId.From(Guid.NewGuid());var historical=new SnapshotHistoricalFacts([],input==0?[]:[new(default,PayrollInputDefinitionId.From(Guid.NewGuid()),1,"FUND_INPUT",PayrollInputDataType.DECIMAL,PayrollInputUnitType.AMOUNT,PayrollInputAggregationType.SUM,PayrollInputValue.Decimal(input),[entry])]);Snapshot=new(PayrollCalculationSnapshotId.From(Guid.NewGuid()),company,period,1,PayrollExecutionMode.Production,new(2026,6,30),DateTimeOffset.UnixEpoch,UserId.From(Guid.NewGuid()),DateTimeOffset.UnixEpoch,UserId.From(Guid.NewGuid()),"p","i","c",new string('a',64),historical,new([],[],[],[],[],[fund]));}
        public PayrollCalculationSnapshotDto GetSnapshotById(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>Snapshot.CompanyId==companyId&&Snapshot.Id==snapshotId?Snapshot:throw new InvalidOperationException();
        public PayrollCalculationSnapshotDto GetAuthoritative(CompanyId companyId,PayrollPeriodId periodId)=>Snapshot;public PayrollCalculationSnapshotDto GetByRevision(CompanyId companyId,PayrollPeriodId periodId,int revision)=>Snapshot;public IReadOnlyList<PayrollCalculationSnapshotDto> ListRevisions(CompanyId companyId,PayrollPeriodId periodId)=>[Snapshot];
        public IReadOnlyList<SnapshotSubjectFact> GetSubjects(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>Snapshot.HistoricalFacts.Subjects;public IReadOnlyList<SnapshotResolvedInput> GetSubjectInputs(CompanyId companyId,PayrollCalculationSnapshotId snapshotId,PayrollSubjectId subjectId)=>[];public IReadOnlyList<SnapshotCompensationVersion> GetCompensationVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>[];public IReadOnlyList<SnapshotFormulaVersion> GetFormulaVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>[];public IReadOnlyList<SnapshotParameterVersion> GetParameterVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>[];public IReadOnlyList<SnapshotLookupVersion> GetLookupVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>[];public IReadOnlyList<SnapshotRuleSetVersion> GetRuleSetVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>[];public IReadOnlyList<SnapshotPayrollFundVersion> GetFundVersions(CompanyId companyId,PayrollCalculationSnapshotId snapshotId)=>Snapshot.PolicyConfiguration.FundVersions??[];
    }
    private sealed record Context(CompanyId CompanyId):ICompanyContext;private sealed class User:ICurrentUser{public UserId UserId{get;}=UserId.From(Guid.NewGuid());public bool HasPermission(string permissionCode)=>true;}private sealed class Correlation:ICorrelationContext{public string CorrelationId=>"task-11";public string? IdempotencyKey=>null;}
}
