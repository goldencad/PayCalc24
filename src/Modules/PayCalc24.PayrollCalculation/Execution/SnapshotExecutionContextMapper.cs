using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.FormulaEngine.Execution;
using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.PayrollCalculation.Execution;

/// <summary>Task 10 boundary: maps only pinned snapshot content and performs no repository resolution or calculation.</summary>
public static class SnapshotExecutionContextMapper
{
    public static FormulaExecutionContext Map(PayrollCalculationSnapshotDto snapshot,
        PayrollSubjectId subjectId, FormulaDefinitionId formulaDefinitionId, string correlationId,
        PayrollExecutionMode mode = PayrollExecutionMode.Production,
        IReadOnlyDictionary<string, FormulaValue>? calculatedValues = null,
        SnapshotPolicyConfiguration? policyOverride = null)
    {
        var policy=policyOverride??snapshot.PolicyConfiguration;
        var formula=policy.FormulaVersions.Single(x=>x.FormulaDefinitionId==formulaDefinitionId);
        var inputs=snapshot.HistoricalFacts.Inputs.Where(x=>x.PayrollSubjectId==subjectId).OrderBy(x=>x.Code,StringComparer.Ordinal).ToArray();
        var values=inputs.ToDictionary(x=>x.Code,x=>Map(x.ResolvedValue),StringComparer.OrdinalIgnoreCase);
        if(calculatedValues is not null)foreach(var item in calculatedValues)values[item.Key]=item.Value;
        var parameters=policy.ParameterVersions.SelectMany(x=>x.Values).OrderBy(x=>x.Code,StringComparer.Ordinal).ToDictionary(x=>x.Code,x=>Map(x.Value),StringComparer.OrdinalIgnoreCase);
        var provenance=inputs.ToDictionary(x=>x.Code,x=>(IReadOnlyList<Contracts.PayrollInput.PayrollInputLedgerEntryId>)x.ContributingLedgerEntryIds.ToArray(),StringComparer.OrdinalIgnoreCase);
        return new(snapshot.CompanyId,snapshot.BusinessDate,Map(mode),values,parameters,correlationId,
            formula.FormulaDefinitionId,formula.FormulaVersionId,formula.Checksum,snapshot.PayrollPeriodId,subjectId,null,
            policy.ParameterVersions.Select(x=>x.ParameterSetVersionId).OrderBy(x=>x.Value).ToArray(),
            policy.LookupVersions.Select(x=>x.LookupTableVersionId).OrderBy(x=>x.Value).ToArray(),
            policy.RuleSetVersions.Select(x=>x.RuleSetVersionId).OrderBy(x=>x.Value).ToArray(),provenance);
    }
    private static FormulaExecutionMode Map(PayrollExecutionMode mode)=>mode switch{PayrollExecutionMode.Production=>FormulaExecutionMode.Production,PayrollExecutionMode.Replay=>FormulaExecutionMode.Replay,PayrollExecutionMode.BackTest=>FormulaExecutionMode.BackTest,PayrollExecutionMode.WhatIf=>FormulaExecutionMode.WhatIf,_=>throw new ArgumentOutOfRangeException(nameof(mode))};
    private static FormulaValue Map(Contracts.PayrollInput.PayrollInputValue value)=>value.DataType switch
    {
        Contracts.PayrollInput.PayrollInputDataType.DECIMAL=>FormulaValue.Decimal(value.DecimalValue!.Value),
        Contracts.PayrollInput.PayrollInputDataType.INTEGER=>FormulaValue.Integer(value.IntegerValue!.Value),
        Contracts.PayrollInput.PayrollInputDataType.BOOLEAN=>FormulaValue.Boolean(value.BooleanValue!.Value),
        Contracts.PayrollInput.PayrollInputDataType.DATE=>FormulaValue.Date(value.DateValue!.Value),
        Contracts.PayrollInput.PayrollInputDataType.TEXT=>FormulaValue.Text(value.TextValue!),
        _=>throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static FormulaValue Map(FormulaTypedValue value)=>value.DataType switch
    {
        FormulaDataType.DECIMAL=>FormulaValue.Decimal(value.DecimalValue!.Value),FormulaDataType.INTEGER=>FormulaValue.Integer(value.IntegerValue!.Value),
        FormulaDataType.BOOLEAN=>FormulaValue.Boolean(value.BooleanValue!.Value),FormulaDataType.DATE=>FormulaValue.Date(value.DateValue!.Value),
        FormulaDataType.TEXT=>FormulaValue.Text(value.TextValue!),_=>throw new ArgumentOutOfRangeException(nameof(value))
    };
}
