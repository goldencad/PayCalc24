using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Integration;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollApproval;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Integration.Services;

namespace PayCalc24.ApplicationTests;

public sealed class Task18StatutoryIntegrationTests
{
    [Fact]
    public void NetPayAndEmployerCostUseDynamicItemsAndFundedSettlementAmount()
    {
        var f=new Fixture();var statutory=f.Result(8m,12m,5m);
        var result=new PayrollSettlementComposer().Compose(f.Request(
            [new("BASE",100m,PayrollAmountClassification.NetSettlementEarning,"calculation"),
             new("BASE_COST",100m,PayrollAmountClassification.PayrollCost,"calculation"),
             new("BONUS",15m,PayrollAmountClassification.NetSettlementEarning,"fund",20m,15m),
             new("OTHER",2m,PayrollAmountClassification.OtherDeduction,"input")],[statutory]));
        Assert.Equal(100m,result.NetPay.NetPay);
        Assert.Equal(112m,result.EmployerCost.TotalEmployerCost);
        Assert.Equal(15m,result.NetPay.EarningItems.Single(x=>x.Code=="BONUS").FundedAmount);
        Assert.Equal("EMPLOYER_A",Assert.Single(result.EmployerCost.EmployerContributionItems).Code);
    }

    [Fact]
    public void MissingOrUnavailableStatutoryResultNeverBecomesZero()
    {
        var f=new Fixture();
        var empty=Assert.Throws<StatutoryIntegrationException>(()=>new PayrollSettlementComposer().Compose(f.Request([],[])));
        Assert.Equal(DiagnosticCodes.NetPayRequiredStatutoryResultMissing,empty.Diagnostic.Code);
        var unavailable=f.Result(0m,0m,0m) with{Status=StatutoryResultStatus.Unavailable};
        Assert.Throws<StatutoryIntegrationException>(()=>new PayrollSettlementComposer().Compose(f.Request([], [unavailable])));
    }

    [Fact]
    public void AccountingPublishIsBalancedLockedProductionAndIdempotent()
    {
        var f=new Fixture();var adapter=new FakeAdapter();var locked=f.Approval(PayrollApprovalStatus.Locked);
        var publisher=new AccountingPublisher([adapter],new InMemoryAccountingDeliveryRepository(),_=>locked);
        var document=f.Document(PayrollExecutionMode.Production);
        var first=publisher.Publish(new(document,"FAKE","same","corr"));
        var second=publisher.Publish(new(document,"FAKE","same","corr"));
        Assert.Equal(first,second);Assert.Equal(1,adapter.Count);
        var conflict=Assert.Throws<StatutoryIntegrationException>(()=>publisher.Publish(new(document with{ResultHash="changed"},"FAKE","same","corr")));
        Assert.Equal(DiagnosticCodes.AccountingIdempotencyConflict,conflict.Diagnostic.Code);
    }

    [Fact]
    public void ScenarioAndUnbalancedAccountingCannotPublish()
    {
        var f=new Fixture();var publisher=new AccountingPublisher([new FakeAdapter()],new InMemoryAccountingDeliveryRepository(),_=>f.Approval(PayrollApprovalStatus.Locked));
        Assert.Throws<StatutoryIntegrationException>(()=>publisher.Publish(new(f.Document(PayrollExecutionMode.WhatIf),"FAKE","x","corr")));
        var unbalanced=f.Document(PayrollExecutionMode.Production) with{TotalCredit=99m};
        var error=Assert.Throws<StatutoryIntegrationException>(()=>publisher.Publish(new(unbalanced,"FAKE","y","corr")));
        Assert.Equal(DiagnosticCodes.AccountingDocumentUnbalanced,error.Diagnostic.Code);
    }

    private sealed class Fixture
    {
        public CompanyId Company{get;}=CompanyId.From(Guid.NewGuid());public PayrollPeriodId Period{get;}=PayrollPeriodId.From(Guid.NewGuid());
        public PayrollCalculationSnapshotId Snapshot{get;}=PayrollCalculationSnapshotId.From(Guid.NewGuid());public PayrollCalculationRunId Run{get;}=PayrollCalculationRunId.From(Guid.NewGuid());
        public PayrollSubjectId Subject{get;}=PayrollSubjectId.From(Guid.NewGuid());public PayrollApprovalCaseId ApprovalId{get;}=PayrollApprovalCaseId.From(Guid.NewGuid());
        public StatutoryCalculationResult Result(decimal employee,decimal employer,decimal tax)=>new(new(Guid.NewGuid()),Company,Period,Snapshot,1,Run,Subject,
            StatutoryResultKind.Insurance,StatutoryResultStatus.Calculated,new("AA","TEST","1","policy-1"),new(2026,1,1),
            new([new("EMPLOYEE_A","SOCIAL",ContributionParty.Employee,100m,null,employee),new("EMPLOYER_A","SOCIAL",ContributionParty.Employer,100m,null,employer)],employee,employer),
            new(100m,null,[],[],tax),[],[],"request","result","corr");
        public ComposeSettlementRequest Request(IReadOnlyList<SettlementAmountItem> items,IReadOnlyList<StatutoryCalculationResult> statutory)=>new(Company,Period,Snapshot,1,Run,ApprovalId,Subject,PayrollExecutionMode.Production,items,statutory);
        public PayrollAccountingDocument Document(PayrollExecutionMode mode)=>new(new(Guid.NewGuid()),Company,Period,Snapshot,1,Run,ApprovalId,"VND",new(2026,1,1),"PAY-1",1,
            [new("EXPENSE",AccountingSide.Debit,100m,"net"),new("PAYABLE",AccountingSide.Credit,100m,"net")],100m,100m,"hash",mode);
        public PayrollApprovalCaseDto Approval(PayrollApprovalStatus status)=>new(ApprovalId,Company,Period,Snapshot,1,"snapshot",Run,"calculation",[],"review","approval",status,1,null,DateTimeOffset.UnixEpoch,UserId.From(Guid.NewGuid()),null,null,null,null,null,null,null,null,status==PayrollApprovalStatus.Locked?DateTimeOffset.UnixEpoch:null,null,null,"corr");
    }
    private sealed class FakeAdapter:IExternalAccountingAdapter
    { public string TargetSystem=>"FAKE";public int Count{get;private set;}public AccountingPublishResult Publish(PayrollAccountingDocument document,string correlationId){Count++;return new("delivery",IntegrationDeliveryStatus.Succeeded,"external",document.ResultHash);} }
}
