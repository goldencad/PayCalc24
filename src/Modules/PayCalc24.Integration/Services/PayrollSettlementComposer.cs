using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Integration;

namespace PayCalc24.Integration.Services;

public sealed class PayrollSettlementComposer : IPayrollSettlementComposer
{
    public PayrollSettlementResult Compose(ComposeSettlementRequest request)
    {
        var required=request.RequiredStatutoryResults;
        if(required.Count==0||required.Any(x=>x.Status is not (StatutoryResultStatus.Calculated or StatutoryResultStatus.NotApplicable)))
            throw Error(DiagnosticCodes.NetPayRequiredStatutoryResultMissing);
        if(required.Any(x=>x.CompanyId!=request.CompanyId||x.PayrollSubjectId!=request.PayrollSubjectId||x.CalculationRunId!=request.CalculationRunId))
            throw Error(DiagnosticCodes.StatutoryCrossCompanyReference);
        var earnings=request.PayrollItems.Where(x=>x.Classification==PayrollAmountClassification.NetSettlementEarning).ToArray();
        var otherDeductions=request.PayrollItems.Where(x=>x.Classification==PayrollAmountClassification.OtherDeduction).ToArray();
        var statutoryEmployee=required.Sum(x=>(x.Insurance?.TotalEmployeeContribution??0m)+(x.IncomeTax?.TaxAmount??0m)+x.OtherDeductions.Sum(y=>y.Amount));
        var totalEarnings=earnings.Sum(x=>x.Amount);var totalOther=otherDeductions.Sum(x=>x.Amount);
        var net=totalEarnings-statutoryEmployee-totalOther;
        var statutoryIds=required.Select(x=>x.Id).ToArray();
        var netHash=CanonicalHash.Create(request.CompanyId.Value,request.SnapshotId.Value,request.SnapshotRevision,
            request.CalculationRunId.Value,request.PayrollSubjectId.Value,totalEarnings,statutoryEmployee,totalOther,net,
            string.Join(";",required.OrderBy(x=>x.ResultHash,StringComparer.Ordinal).Select(x=>x.ResultHash)));
        var netResult=new NetPayResult(new(Guid.NewGuid()),request.CompanyId,request.PayrollPeriodId,request.SnapshotId,
            request.SnapshotRevision,request.CalculationRunId,request.ApprovalCaseId,request.PayrollSubjectId,
            earnings,otherDeductions,statutoryIds,totalEarnings,statutoryEmployee,totalOther,net,netHash,request.ExecutionMode);
        var costs=request.PayrollItems.Where(x=>x.Classification is PayrollAmountClassification.PayrollCost or PayrollAmountClassification.EmployerPaidCost).ToArray();
        var employerItems=required.SelectMany(x=>x.Insurance?.ContributionItems??[]).Where(x=>x.Party==ContributionParty.Employer).ToArray();
        var payrollCost=costs.Sum(x=>x.Amount);var statutoryCost=employerItems.Sum(x=>x.Amount);var totalCost=payrollCost+statutoryCost;
        var costHash=CanonicalHash.Create(request.CompanyId.Value,request.SnapshotId.Value,request.SnapshotRevision,
            request.CalculationRunId.Value,request.PayrollSubjectId.Value,payrollCost,statutoryCost,totalCost,
            string.Join(";",employerItems.OrderBy(x=>x.Code,StringComparer.Ordinal).Select(x=>$"{x.Code}:{x.Amount:0.00000000}")));
        var employer=new EmployerCostResult(new(Guid.NewGuid()),request.CompanyId,request.PayrollPeriodId,
            request.SnapshotId,request.SnapshotRevision,request.CalculationRunId,request.PayrollSubjectId,costs,
            employerItems,payrollCost,statutoryCost,totalCost,costHash);
        return new(netResult,employer);
    }
    private static StatutoryIntegrationException Error(string code)=>new(new(code,DiagnosticSeverity.Error,new Dictionary<string,object?>()));
}
