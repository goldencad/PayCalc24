using System.Globalization;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.PayrollApproval;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollReview;
using PayCalc24.PayrollApproval.Services;

namespace PayCalc24.ApplicationTests;

public sealed class Task17PayrollApprovalTests
{
    [Theory]
    [InlineData("vi-VN")][InlineData("en-US")][InlineData("fr-FR")]
    public void HappyPathPinsArtifactsAndAppendsOneEventPerTransition(string culture)
    {
        using var _=new CultureScope(culture); var f=new Fixture(); var c=f.Create();
        c=f.Service.Submit(new(c.Id,c.Revision,"submit")); c=f.Service.StartReview(new(c.Id,c.Revision,"review"));
        c=f.Service.Approve(new(c.Id,c.Revision,"approve","checked")); c=f.Service.Lock(new(c.Id,c.Revision,"lock"));
        Assert.Equal(PayrollApprovalStatus.Locked,c.Status); Assert.Equal(f.Artifacts.SnapshotHash,c.SnapshotHash);
        Assert.Equal(4,f.Service.GetHistory(c.Id).Count); Assert.Equal(1,f.Close.Count);
        Assert.Equal(c,f.Service.Lock(new(c.Id,c.Revision,"lock")));
    }

    [Fact] public void BlockingValidationPreventsSubmit()
    { var f=new Fixture(blocking:true);var c=f.Create();var e=Assert.Throws<PayrollApprovalException>(()=>f.Service.Submit(new(c.Id,c.Revision,"s")));Assert.Equal("PAYROLL_APPROVAL.SUBMIT_BLOCKED",e.Diagnostic.Code);Assert.Empty(f.Service.GetHistory(c.Id)); }

    [Fact] public void RejectionRequiresReasonAndCannotLock()
    { var f=new Fixture();var c=f.Create();c=f.Service.Submit(new(c.Id,c.Revision,"s"));c=f.Service.StartReview(new(c.Id,c.Revision,"r"));Assert.Throws<PayrollApprovalException>(()=>f.Service.Reject(new(c.Id,c.Revision,"bad")));c=f.Service.Reject(new(c.Id,c.Revision,"reject","Attendance correction required"));Assert.Equal(PayrollApprovalStatus.Rejected,c.Status);Assert.Throws<PayrollApprovalException>(()=>f.Service.Lock(new(c.Id,c.Revision,"l"))); }

    [Fact] public void ConcurrentDecisionUsesRevisionAndStaleCaseCannotApprove()
    { var f=new Fixture();var c=f.Create();c=f.Service.Submit(new(c.Id,c.Revision,"s"));c=f.Service.StartReview(new(c.Id,c.Revision,"r"));var revision=c.Revision;c=f.Service.Approve(new(c.Id,revision,"a"));var e=Assert.Throws<PayrollApprovalException>(()=>f.Service.Reject(new(c.Id,revision,"b","no")));Assert.Equal("PAYROLL_APPROVAL.CONCURRENCY_CONFLICT",e.Diagnostic.Code);var stale=new Fixture(latest:false);var x=stale.Create();x=stale.Service.Submit(new(x.Id,x.Revision,"s"));x=stale.Service.StartReview(new(x.Id,x.Revision,"r"));Assert.Throws<PayrollApprovalException>(()=>stale.Service.Approve(new(x.Id,x.Revision,"a"))); }

    [Fact] public void AdjustmentRequiresReasonAuthorizationAndStrictlyNewRevision()
    { var f=new Fixture();var c=f.Create();c=f.Service.Submit(new(c.Id,c.Revision,"s"));c=f.Service.StartReview(new(c.Id,c.Revision,"r"));c=f.Service.Approve(new(c.Id,c.Revision,"a"));c=f.Service.Lock(new(c.Id,c.Revision,"l"));Assert.Throws<PayrollApprovalException>(()=>f.Service.RequestAdjustment(new(c.Id,PayrollAdjustmentType.InputCorrection," ","q")));var q=f.Service.RequestAdjustment(new(c.Id,PayrollAdjustmentType.InputCorrection,"Late attendance correction","q"));Assert.Throws<PayrollApprovalException>(()=>f.Service.StartNewRevision(new(q.Id,q.Revision,"n")));q=f.Service.AuthorizeAdjustment(q.Id,q.Revision,"auth");q=f.Service.StartNewRevision(new(q.Id,q.Revision,"new"));Assert.Equal(PayrollAdjustmentStatus.RevisionStarted,q.Status);Assert.Equal(2,q.NewSnapshotRevision);Assert.Equal(PayrollApprovalStatus.Locked,f.Service.GetCase(c.Id).Status); }

    [Fact] public void ScenarioCannotCreateProductionApprovalCase()
    { var f=new Fixture(mode:PayrollExecutionMode.WhatIf);var e=Assert.Throws<PayrollApprovalException>(()=>f.Create());Assert.Equal("PAYROLL_APPROVAL.NON_PRODUCTION_RESULT",e.Diagnostic.Code); }

    private sealed class Fixture
    {
        public readonly ApprovalArtifactContext Artifacts; public readonly FakeClose Close=new(); public readonly IPayrollApprovalService Service;
        public Fixture(bool blocking=false,bool latest=true,PayrollExecutionMode mode=PayrollExecutionMode.Production)
        {
            var company=CompanyId.From(Guid.NewGuid());var period=PayrollPeriodId.From(Guid.NewGuid());var snapshot=PayrollCalculationSnapshotId.From(Guid.NewGuid());var run=PayrollCalculationRunId.From(Guid.NewGuid());
            Artifacts=new(company,period,snapshot,1,"snapshot-hash",run,"result-hash",mode,["fund-b","fund-a"],"review-hash",true,true,latest);
            var source=new FakeArtifacts(Artifacts,latest);var review=new FakeReview(Artifacts,blocking);var context=new Context(company);var user=new Current(UserId.From(Guid.NewGuid()));
            Service=new PayrollApprovalService(context,user,new Correlation(),new Allow(),new InMemoryPayrollApprovalRepository(),source,review,new Revisioner(Artifacts),Close,()=>DateTimeOffset.UnixEpoch);
        }
        public PayrollApprovalCaseDto Create()=>Service.Create(new(Artifacts,null,"create"));
    }
    private sealed record Context(CompanyId CompanyId):ICompanyContext;
    private sealed record Current(UserId UserId):ICurrentUser{public bool HasPermission(string code)=>true;}
    private sealed class Correlation:ICorrelationContext{public string CorrelationId=>"corr";public string? IdempotencyKey=>null;}
    private sealed class Allow:IPayrollApprovalAuthorization{public void Demand(CompanyId c,UserId u,PayrollApprovalAction a){}public void ValidateDecisionActors(PayrollApprovalCaseDto c,UserId u){} }
    private sealed class FakeArtifacts(ApprovalArtifactContext value,bool latest):IPayrollApprovalArtifactSource{public ApprovalArtifactContext GetAuthoritativeArtifacts(ReviewResultContext c)=>value;public bool IsLatestProductionRevision(ApprovalArtifactContext a)=>latest;}
    private sealed class Revisioner(ApprovalArtifactContext a):IPayrollRevisionOrchestrator{public RevisionStartResult StartNewRevision(PayrollAdjustmentRequestDto q){var x=a with{SnapshotId=PayrollCalculationSnapshotId.From(Guid.NewGuid()),SnapshotRevision=2,CalculationRunId=PayrollCalculationRunId.From(Guid.NewGuid()),SnapshotHash="snapshot-2",CalculationResultHash="result-2"};return new(x.SnapshotId,2,x.CalculationRunId,x);}}
    private sealed class FakeClose:IPayrollPeriodCloseBoundary{public List<PayrollPeriodId> Count{get;}=[];public void CloseCalculatedPeriod(CompanyId c,PayrollPeriodId p)=>Count.Add(p);}
    private sealed class FakeReview(ApprovalArtifactContext a,bool blocking):IPayrollReviewService
    {
        private PayrollValidationSummary Summary()=>new(a.CompanyId,a.PayrollPeriodId,a.SnapshotId,a.CalculationRunId,blocking?1:0,blocking?0:1,0,blocking,!blocking,blocking?ReviewStatus.HasErrors:ReviewStatus.HasWarnings,[]);
        public PayrollValidationSummary GetPayrollValidationSummary(ReviewResultContext c)=>Summary();public IReadOnlyList<ValidationFinding> ListValidationFindings(ReviewResultContext c)=>[];public IReadOnlyList<ValidationFinding> GetSubjectValidation(ReviewResultContext c,PayCalc24.Contracts.Organization.PayrollSubjectId s)=>[];
        public PayrollExplainResult ExplainPayrollSubject(ReviewResultContext c,PayCalc24.Contracts.Organization.PayrollSubjectId s)=>throw new NotSupportedException();public ComponentExplanation ExplainComponent(ReviewResultContext c,PayComponentCalculationResultId id)=>throw new NotSupportedException();public InputProvenanceExplanation GetInputProvenance(ReviewResultContext c,PayCalc24.Contracts.PayrollInput.PayrollInputLedgerEntryId id)=>throw new NotSupportedException();public FundExplanation ExplainFundAllocation(ReviewResultContext c,PayCalc24.Contracts.PayrollFunds.FundAllocationResultId id)=>throw new NotSupportedException();public PayrollVarianceResult ComparePayrollPeriods(ReviewResultContext a,ReviewResultContext b)=>throw new NotSupportedException();public IReadOnlyList<VarianceItem> GetSubjectVariance(ReviewResultContext a,ReviewResultContext b,PayCalc24.Contracts.Organization.PayrollSubjectId s)=>[];public IReadOnlyList<VarianceItem> GetComponentVariance(ReviewResultContext a,ReviewResultContext b,string c)=>[];public FundingReview GetFundingReview(ReviewResultContext c)=>throw new NotSupportedException();public FundReview GetFundReview(ReviewResultContext c,PayCalc24.Contracts.PayrollFunds.FundAllocationResultId id)=>throw new NotSupportedException();public PayrollPeriodReview GetPeriodReview(ReviewResultContext c)=>throw new NotSupportedException();
    }
    private sealed class CultureScope:IDisposable{private readonly CultureInfo old=CultureInfo.CurrentCulture;public CultureScope(string c)=>CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(c);public void Dispose()=>CultureInfo.CurrentCulture=old;}
}
