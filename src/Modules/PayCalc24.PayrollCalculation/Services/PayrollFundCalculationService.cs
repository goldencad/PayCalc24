using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.FormulaEngine.Execution;
using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.PayrollFunds.Services;

public sealed class PayrollFundCalculationService(ICompanyContext companyContext,ICorrelationContext correlationContext,
    TimeProvider timeProvider,IPayrollSnapshotQueryService snapshots,SafeFormulaEngine formulaEngine):IPayrollFundCalculationService
{
    public const string EngineVersion="1.0.0";
    private readonly List<FundAllocationResultDto> results=[]; private readonly Dictionary<(CompanyId,PayrollCalculationSnapshotId,string),string> fingerprints=[]; private readonly Lock gate=new();
    public ValueTask<FundAllocationResultDto> CalculateAsync(CalculatePayrollFund c,CancellationToken token=default)
    {
        Scope(c.CompanyId);if(string.IsNullOrWhiteSpace(c.IdempotencyKey))throw new ArgumentException("Idempotency key is required.",nameof(c));
        var snapshot=snapshots.GetSnapshotById(c.CompanyId,c.SnapshotId);var pinned=(snapshot.PolicyConfiguration.FundVersions??[]).SingleOrDefault(x=>x.FundVersionId==c.FundVersionId)??throw Error(DiagnosticCodes.PayrollFundVersionNotFound);
        Validate(c,pinned,snapshot);
        var fingerprint=Fingerprint(c,snapshot.SnapshotHash);lock(gate)
        {
            var prior=results.SingleOrDefault(x=>x.CompanyId==c.CompanyId&&x.SnapshotId==c.SnapshotId&&x.IdempotencyKey==c.IdempotencyKey);
            if(prior is not null){if(fingerprints[(c.CompanyId,c.SnapshotId,c.IdempotencyKey)]!=fingerprint)Throw(DiagnosticCodes.PayrollFundCalculationIdempotencyConflict);return ValueTask.FromResult(prior);}
            if(c.ExecutionMode==PayrollExecutionMode.Production&&results.Any(x=>x.CompanyId==c.CompanyId&&x.SnapshotId==c.SnapshotId&&x.FundVersionId==c.FundVersionId&&x.ExecutionMode==PayrollExecutionMode.Production))Throw(DiagnosticCodes.PayrollFundCalculationConcurrentAllocation);
            var source=ResolveSource(c,snapshot,pinned);var built=Allocate(c,snapshot,pinned,source);results.Add(built);fingerprints[(c.CompanyId,c.SnapshotId,c.IdempotencyKey)]=fingerprint;return ValueTask.FromResult(built);
        }
    }
    public FundAllocationResultDto GetResult(CompanyId c,FundAllocationResultId id){Scope(c);var any=results.SingleOrDefault(x=>x.Id==id)??throw Error(DiagnosticCodes.PayrollFundVersionNotFound);if(any.CompanyId!=c)Throw(DiagnosticCodes.PayrollFundCrossCompanyReference);return any;}
    public FundAllocationResultDto? ResolveByIdempotencyKey(CompanyId c,PayrollCalculationSnapshotId s,string key){Scope(c);return results.SingleOrDefault(x=>x.CompanyId==c&&x.SnapshotId==s&&x.IdempotencyKey==key);}
    public IReadOnlyList<FundAllocationResultDto> ListResults(CompanyId c,PayrollCalculationSnapshotId s){Scope(c);return results.Where(x=>x.CompanyId==c&&x.SnapshotId==s).OrderBy(x=>x.FundVersionId.Value).ThenBy(x=>x.CreatedAt).ToArray();}
    public IReadOnlyList<FundMemberAllocationResultDto> ListMemberAllocations(CompanyId c,FundAllocationResultId id)=>GetResult(c,id).Members;

    private (decimal Amount,string Provenance) ResolveSource(CalculatePayrollFund c,PayrollCalculationSnapshotDto snapshot,SnapshotPayrollFundVersion fund)
    {
        if(c.ExplicitAvailableFund is not null)
        {if(c.ExecutionMode is PayrollExecutionMode.Production or PayrollExecutionMode.Replay||c.ExplicitAvailableFund<0)Throw(DiagnosticCodes.PayrollFundCalculationAvailableFundMissing);return(c.ExplicitAvailableFund.Value,JsonSerializer.Serialize(new{type="explicit_scenario",c.ScenarioId}));}
        switch(fund.Source.Type)
        {
            case FundSourceType.FIXED:return(fund.Source.FixedAmount!.Value,JsonSerializer.Serialize(new{type="fund_version",fundVersionId=fund.FundVersionId.Value}));
            case FundSourceType.INPUT:
                var inputs=snapshot.HistoricalFacts.Inputs.Where(x=>StringComparer.OrdinalIgnoreCase.Equals(x.Code,fund.Source.InputCode)).OrderBy(x=>x.PayrollSubjectId.Value).ToArray();
                if(inputs.Length==0||inputs.Any(x=>x.ResolvedValue.DataType is not(PayrollInputDataType.DECIMAL or PayrollInputDataType.INTEGER)))Throw(DiagnosticCodes.PayrollFundCalculationAvailableFundMissing);
                var amount=inputs.Sum(x=>x.ResolvedValue.DataType==PayrollInputDataType.DECIMAL?x.ResolvedValue.DecimalValue!.Value:x.ResolvedValue.IntegerValue!.Value);
                return(amount,JsonSerializer.Serialize(new{type="snapshot_input",code=fund.Source.InputCode,entryIds=inputs.SelectMany(x=>x.ContributingLedgerEntryIds).Select(x=>x.Value).Order().ToArray()}));
            case FundSourceType.FORMULA:
                var formula=snapshot.PolicyConfiguration.FormulaVersions.SingleOrDefault(x=>x.FormulaVersionId==fund.Source.FormulaVersionId&&x.FormulaDefinitionId==fund.Source.FormulaDefinitionId&&x.Checksum==fund.Source.FormulaChecksum);
                if(formula?.Expression is null)throw Error(DiagnosticCodes.PayrollFundCalculationAvailableFundMissing);
                var values=snapshot.HistoricalFacts.Inputs.GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>FormulaValue.Decimal(x.Sum(y=>Numeric(y.ResolvedValue))),StringComparer.OrdinalIgnoreCase);
                var parameters=snapshot.PolicyConfiguration.ParameterVersions.SelectMany(x=>x.Values).Where(x=>x.Value.DataType is Contracts.FormulaRepository.FormulaDataType.DECIMAL or Contracts.FormulaRepository.FormulaDataType.INTEGER).GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>FormulaValue.Decimal(x.Sum(y=>y.Value.DecimalValue??y.Value.IntegerValue??0)),StringComparer.OrdinalIgnoreCase);
                var context=new FormulaExecutionContext(c.CompanyId,snapshot.BusinessDate,Mode(c.ExecutionMode),values,parameters,correlationContext.CorrelationId,formula.FormulaDefinitionId,formula.FormulaVersionId,formula.Checksum,snapshot.PayrollPeriodId,null,ParseScenario(c.ScenarioId),snapshot.PolicyConfiguration.ParameterVersions.Select(x=>x.ParameterSetVersionId).ToArray(),snapshot.PolicyConfiguration.LookupVersions.Select(x=>x.LookupTableVersionId).ToArray(),snapshot.PolicyConfiguration.RuleSetVersions.Select(x=>x.RuleSetVersionId).ToArray());
                var evaluated=formulaEngine.Evaluate(formula.Expression,context);if(!evaluated.Success||evaluated.Value is not { } formulaValue||!formulaValue.IsNumeric)throw Error(evaluated.Diagnostic?.Code??DiagnosticCodes.PayrollFundCalculationAvailableFundMissing);
                return(formulaValue.AsDecimal(),JsonSerializer.Serialize(new{type="formula",formulaVersionId=formula.FormulaVersionId.Value,formula.Checksum,trace=evaluated.Trace}));
            default:Throw(DiagnosticCodes.PayrollFundCalculationAvailableFundMissing);return default;
        }
    }
    private FundAllocationResultDto Allocate(CalculatePayrollFund c,PayrollCalculationSnapshotDto snapshot,SnapshotPayrollFundVersion fund,(decimal Amount,string Provenance) source)
    {
        if(source.Amount<0)Throw(DiagnosticCodes.PayrollFundCalculationAvailableFundMissing);var eligible=c.Requirements.Where(x=>x.EligibilityStatus==FundEligibilityStatus.ELIGIBLE).Select(x=>new Work(x,Eligible(x))).OrderBy(x=>x.Requirement.Priority).ThenBy(x=>x.Requirement.RequirementReference,StringComparer.Ordinal).ToArray();
        var demand=eligible.Sum(x=>x.Eligible);var raw=demand==0m?1m:source.Amount/demand;var effective=demand==0m?1m:decimal.Min(1m,raw);var budget=decimal.Min(source.Amount,demand);var allocations=fund.Policy.Method switch{FundAllocationMethod.PROPORTIONAL=>Proportional(eligible,budget,fund.Policy),FundAllocationMethod.WEIGHTED=>Weighted(eligible,budget,fund.Policy),FundAllocationMethod.PRIORITY=>Priority(eligible,budget,fund.Policy),_=>throw Error(DiagnosticCodes.PayrollFundCalculationUnsupportedMethod)};
        var funded=allocations.Sum();if(funded>source.Amount||funded>demand)Throw(DiagnosticCodes.PayrollFundCalculationAllocationExceedsFund);var resultId=FundAllocationResultId.From(Guid.NewGuid());var members=new List<FundMemberAllocationResultDto>();
        for(var i=0;i<eligible.Length;i++){var w=eligible[i];var hash=Hash($"{snapshot.SnapshotHash}|{fund.FundVersionId.Value:D}|{w.Requirement.RequirementReference}|{Canonical(w.Eligible)}|{Canonical(allocations[i])}|{i+1}");members.Add(new(FundMemberAllocationResultId.From(Guid.NewGuid()),resultId,w.Requirement.RequirementReference,w.Requirement.PayrollSubjectId,w.Requirement.PayComponentId,w.Requirement.RequiredAmount,w.Eligible,allocations[i],w.Requirement.Weight,w.Requirement.Priority,w.Requirement.FloorAmount,w.Requirement.TargetAmount,w.Requirement.CapAmount,i+1,w.Requirement.ProvenanceType,w.Requirement.ProvenanceIds??[],hash));}
        var trace=JsonSerializer.Serialize(new{nodeType="fund_allocation",fundCode=fund.Code,availableFund=Canonical(source.Amount),eligibleDemand=Canonical(demand),rawCoverageRatio=Canonical(raw),effectiveFundingRatio=Canonical(effective),allocationMethod=fund.Policy.Method.ToString(),members=members.Select(x=>new{x.RequirementReference,eligibleAmount=Canonical(x.EligibleAmount),allocatedAmount=Canonical(x.AllocatedAmount)})});
        var resultHash=Hash($"{snapshot.SnapshotHash}|{fund.FundVersionId.Value:D}|{fund.Revision}|{fund.Policy.Method}|{fund.Policy.DecimalScale}|{Canonical(source.Amount)}|{Canonical(demand)}|{Canonical(funded)}|{string.Join(';',members.Select(x=>x.ResultHash))}|{EngineVersion}");
        return new(resultId,c.CompanyId,snapshot.PayrollPeriodId,snapshot.Id,snapshot.SnapshotRevision,c.CalculationRunId,fund.FundDefinitionId,fund.FundVersionId,fund.Scope,source.Amount,demand,funded,decimal.Max(0m,demand-funded),decimal.Max(0m,source.Amount-funded),raw,effective,fund.Policy.Method,c.ExecutionMode,EngineVersion,correlationContext.CorrelationId,c.ScenarioId,c.IdempotencyKey,snapshot.SnapshotHash,source.Provenance,trace,resultHash,timeProvider.GetUtcNow(),members);
    }
    private static decimal[] Proportional(Work[] items,decimal budget,FundAllocationPolicy policy)=>Shares(items,budget,policy,x=>x.Eligible);
    private static decimal[] Weighted(Work[] items,decimal budget,FundAllocationPolicy policy)=>Shares(items,budget,policy,x=>x.Eligible*x.Requirement.Weight);
    private static decimal[] Shares(Work[] items,decimal budget,FundAllocationPolicy policy,Func<Work,decimal> score)
    {var result=new decimal[items.Length];if(budget==0||items.Length==0)return result;var active=Enumerable.Range(0,items.Length).ToList();var remaining=budget;while(active.Count>0&&remaining>0){var total=active.Sum(i=>score(items[i]));if(total<=0)break;var capped=new List<int>();foreach(var i in active){var raw=remaining*score(items[i])/total;if(raw>=items[i].Eligible-result[i]){remaining-=items[i].Eligible-result[i];result[i]=items[i].Eligible;capped.Add(i);}}if(capped.Count==0){foreach(var i in active)result[i]+=decimal.Round(remaining*score(items[i])/total,policy.DecimalScale,policy.Rounding);break;}active.RemoveAll(capped.Contains);}return Remainder(result,items,budget,policy);}
    private static decimal[] Priority(Work[] items,decimal budget,FundAllocationPolicy policy){var result=new decimal[items.Length];var remaining=budget;for(var i=0;i<items.Length&&remaining>0;i++){result[i]=decimal.Round(decimal.Min(items[i].Eligible,remaining),policy.DecimalScale,policy.Rounding);remaining-=result[i];}return Remainder(result,items,budget,policy);}
    private static decimal[] Remainder(decimal[] result,Work[] items,decimal budget,FundAllocationPolicy policy){var unit=Unit(policy.DecimalScale);var delta=budget-result.Sum();for(var i=0;delta>=unit&&i<result.Length;i=(i+1)%result.Length){if(result[i]+unit<=items[i].Eligible){result[i]+=unit;delta-=unit;}else if(result.All((x,index)=>x+unit>items[index].Eligible))break;}while(delta<=-unit){for(var i=result.Length-1;i>=0&&delta<=-unit;i--)if(result[i]>=unit){result[i]-=unit;delta+=unit;}}return result;}
    private static decimal Eligible(FundRequirement r){if(string.IsNullOrWhiteSpace(r.RequirementReference)||r.RequiredAmount<0||r.EligibleAmount<0||r.Weight<=0||r.FloorAmount<0||r.CapAmount<0||r.FloorAmount is not null&&r.CapAmount is not null&&r.FloorAmount>r.CapAmount)Throw(r.RequiredAmount<0?DiagnosticCodes.PayrollFundCalculationNegativeRequirement:DiagnosticCodes.PayrollFundCalculationInvalidRequirement);var value=r.EligibleAmount??r.RequiredAmount;if(r.TargetAmount is not null)value=decimal.Min(value,r.TargetAmount.Value);if(r.CapAmount is not null)value=decimal.Min(value,r.CapAmount.Value);return value;}
    private static void Validate(CalculatePayrollFund c,SnapshotPayrollFundVersion f,PayrollCalculationSnapshotDto s){if(c.Requirements.GroupBy(x=>x.RequirementReference,StringComparer.Ordinal).Any(x=>x.Count()>1))Throw(DiagnosticCodes.PayrollFundCalculationDuplicateRequirement);if(c.Requirements.Any(x=>x.CompanyId!=c.CompanyId))Throw(DiagnosticCodes.PayrollFundCrossCompanyReference);if(c.Requirements.Any(x=>x.PayrollSubjectId is not null&&!s.HistoricalFacts.Subjects.Any(y=>y.PayrollSubjectId==x.PayrollSubjectId)))Throw(DiagnosticCodes.PayrollFundCrossCompanyReference);if(f.Source.Type is FundSourceType.EXTERNAL or FundSourceType.PercentOfBase or FundSourceType.OTHER)Throw(DiagnosticCodes.PayrollFundCalculationAvailableFundMissing);}
    private static decimal Numeric(PayrollInputValue v)=>v.DataType switch{PayrollInputDataType.DECIMAL=>v.DecimalValue!.Value,PayrollInputDataType.INTEGER=>v.IntegerValue!.Value,_=>0m};
    private static FormulaExecutionMode Mode(PayrollExecutionMode x)=>x switch{PayrollExecutionMode.Production=>FormulaExecutionMode.Production,PayrollExecutionMode.Replay=>FormulaExecutionMode.Replay,PayrollExecutionMode.BackTest=>FormulaExecutionMode.BackTest,PayrollExecutionMode.WhatIf=>FormulaExecutionMode.WhatIf,_=>throw new ArgumentOutOfRangeException(nameof(x))};
    private static Guid? ParseScenario(string? value)=>Guid.TryParse(value,out var id)?id:null;
    private static decimal Unit(int scale){var value=1m;for(var i=0;i<scale;i++)value/=10m;return value;}
    private static string Fingerprint(CalculatePayrollFund c,string hash)=>Hash($"{hash}|{c.FundVersionId.Value:D}|{c.ExecutionMode}|{c.CalculationRunId?.Value:D}|{c.ScenarioId}|{c.ExplicitAvailableFund?.ToString(CultureInfo.InvariantCulture)}|{string.Join(';',c.Requirements.OrderBy(x=>x.RequirementReference,StringComparer.Ordinal).Select(x=>$"{x.RequirementReference}:{Canonical(x.RequiredAmount)}:{Canonical(x.EligibleAmount)}:{x.Priority}:{Canonical(x.Weight)}"))}");
    private static string Canonical(decimal? value)=>value?.ToString(CultureInfo.InvariantCulture)??""; private static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private void Scope(CompanyId c){if(c!=companyContext.CompanyId)Throw(DiagnosticCodes.CompanyScopeMismatch);} private static PayrollFundException Error(string code)=>new(new(code,DiagnosticSeverity.Error,new Dictionary<string,object?>())); private static void Throw(string code)=>throw Error(code);
    private sealed record Work(FundRequirement Requirement,decimal Eligible);
}

internal static class EnumerableFundExtensions
{
    internal static bool All<T>(this IReadOnlyList<T> values,Func<T,int,bool> predicate){for(var i=0;i<values.Count;i++)if(!predicate(values[i],i))return false;return true;}
}
