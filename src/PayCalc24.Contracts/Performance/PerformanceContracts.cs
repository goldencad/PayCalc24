using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.Temporal;

namespace PayCalc24.Contracts.Performance;

#pragma warning disable CA1707,CA1720

public readonly record struct KpiDefinitionId(Guid Value){public static KpiDefinitionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}
public readonly record struct KpiDefinitionVersionId(Guid Value){public static KpiDefinitionVersionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}
public readonly record struct KpiAssignmentId(Guid Value){public static KpiAssignmentId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}
public readonly record struct KpiResultId(Guid Value){public static KpiResultId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}
public readonly record struct PerformancePolicyDefinitionId(Guid Value){public static PerformancePolicyDefinitionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}
public readonly record struct PerformancePolicyVersionId(Guid Value){public static PerformancePolicyVersionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}
public readonly record struct PerformanceGateId(Guid Value){public static PerformanceGateId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}
public readonly record struct PerformanceEvaluationResultId(Guid Value){public static PerformanceEvaluationResultId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value);}

public enum KpiDataType{DECIMAL,INTEGER,BOOLEAN}
public enum KpiDirection{HIGHER_IS_BETTER,LOWER_IS_BETTER,TARGET_IS_BEST,BOOLEAN_PASS_FAIL,OTHER}
public enum KpiLifecycleStatus{DRAFT,PUBLISHED,RETIRED}
public enum KpiScopeType{INDIVIDUAL,ORGANIZATION,POSITION,JOB_GRADE,OTHER}
public enum KpiResultSourceType{MANUAL,IMPORT,API,SYSTEM,EXTERNAL,OTHER}
public enum PerformanceGateEffectType{PASS,FAIL,CAP,MULTIPLY,OVERRIDE,SET_VALUE,OTHER}
public enum PerformanceExecutionMode{PRODUCTION,REPLAY,BACK_TEST,WHAT_IF}
public enum WeightNormalization{NONE,NORMALIZE_TO_ONE,REQUIRE_SUM_ONE}

public sealed record PerformancePeriod(string Code,DateOnly DateFrom,DateOnly DateTo){public bool IsValid=>!string.IsNullOrWhiteSpace(Code)&&DateFrom<=DateTo;}
public sealed record KpiDefinitionContent(string Code,string Name,string? Description,KpiDataType DataType,string UnitType,KpiDirection Direction,decimal? MinValue=null,decimal? TargetValue=null,decimal? MaxValue=null);
public sealed record KpiDefinitionVersionDto(KpiDefinitionVersionId Id,KpiDefinitionId DefinitionId,CompanyId CompanyId,int Revision,EffectivePeriod EffectivePeriod,KpiLifecycleStatus LifecycleStatus,KpiDefinitionContent Content);
public sealed record KpiScope(KpiScopeType Type,PayrollSubjectId? PayrollSubjectId=null,OrganizationUnitId? OrganizationUnitId=null,PositionId? PositionId=null,JobGradeId? JobGradeId=null,string? OtherScopeId=null);
public sealed record KpiAssignmentDto(KpiAssignmentId Id,CompanyId CompanyId,KpiDefinitionVersionId KpiDefinitionVersionId,KpiScope Scope,decimal Weight,EffectivePeriod EffectivePeriod,bool Active,FormulaVersionId? ScoringFormulaVersionId=null,ParameterSetVersionId? ParameterSetVersionId=null,LookupTableVersionId? LookupTableVersionId=null,RuleSetVersionId? RuleSetVersionId=null);
public sealed record KpiMeasuredValue(KpiDataType DataType,decimal? DecimalValue=null,long? IntegerValue=null,bool? BooleanValue=null){public static KpiMeasuredValue Decimal(decimal value)=>new(KpiDataType.DECIMAL,DecimalValue:value);public static KpiMeasuredValue Integer(long value)=>new(KpiDataType.INTEGER,IntegerValue:value);public static KpiMeasuredValue Boolean(bool value)=>new(KpiDataType.BOOLEAN,BooleanValue:value);public decimal AsDecimal()=>DataType switch{KpiDataType.DECIMAL=>DecimalValue!.Value,KpiDataType.INTEGER=>IntegerValue!.Value,KpiDataType.BOOLEAN=>BooleanValue==true?1m:0m,_=>throw new InvalidOperationException()};}
public sealed record KpiResultDto(KpiResultId Id,CompanyId CompanyId,KpiScope Scope,KpiDefinitionVersionId KpiDefinitionVersionId,PerformancePeriod Period,KpiMeasuredValue MeasuredValue,KpiResultSourceType SourceType,string? SourceSystem,string? SourceReference,DateTimeOffset RecordedAt,UserId? RecordedBy,string CorrelationId,string? IdempotencyKey,KpiResultId? SupersedesResultId,string Fingerprint);
public sealed record DerivedPayrollInputMapping(string InputCode,PayrollInputDataType DataType,PayrollInputUnitType UnitType,string ResultField="FINAL_ACHIEVEMENT");
public sealed record PerformanceGateDto(PerformanceGateId Id,CompanyId CompanyId,string Code,int Priority,FormulaVersionId? ConditionFormulaVersionId,RuleSetVersionId? ConditionRuleSetVersionId,PerformanceGateEffectType EffectType,decimal? EffectValue,FormulaVersionId? ResultFormulaVersionId,bool StopOnMatch,bool Enabled=true);
public sealed record PerformancePolicyVersionDto(PerformancePolicyVersionId Id,PerformancePolicyDefinitionId DefinitionId,CompanyId CompanyId,string Code,int Revision,EffectivePeriod EffectivePeriod,KpiLifecycleStatus LifecycleStatus,WeightNormalization WeightNormalization,FormulaVersionId? OverallFormulaVersionId,IReadOnlyList<PerformanceGateDto> Gates,IReadOnlyList<DerivedPayrollInputMapping> Outputs);
public sealed record PerformanceInput(string Code,PayrollInputValue Value,PayrollInputLedgerEntryId LedgerEntryId);
public sealed record PerformanceEvaluationRequest(CompanyId CompanyId,KpiScope Scope,PerformancePeriod Period,DateOnly EvaluationBusinessDate,PerformancePolicyVersionId PolicyVersionId,IReadOnlyList<KpiAssignmentId> AssignmentIds,IReadOnlyList<KpiResultId> ResultIds,IReadOnlyList<PerformanceInput> PayrollInputs,PerformanceExecutionMode ExecutionMode,string CorrelationId,string EngineVersion,PayrollPeriodId? PayrollPeriodId=null,IReadOnlyDictionary<string,PayrollInputLedgerEntryId>? SupersededDerivedInputs=null);
public sealed record PerformanceKpiDetail(KpiAssignmentId AssignmentId,KpiDefinitionVersionId DefinitionVersionId,KpiResultId ResultId,decimal MeasuredValue,decimal Achievement,decimal Weight,decimal WeightedAchievement);
public sealed record PerformanceGateTrace(PerformanceGateId GateId,string Code,int Priority,bool Matched,PerformanceGateEffectType EffectType,decimal ValueBefore,decimal ValueAfter);
public sealed record PerformanceExplainNode(string NodeType,string? ReferenceCode,decimal? Value,IReadOnlyList<PerformanceExplainNode> Children);
public sealed record PerformanceEvaluationResultDto(PerformanceEvaluationResultId Id,CompanyId CompanyId,KpiScope Scope,PerformancePeriod Period,PerformancePolicyVersionId PolicyVersionId,decimal OverallAchievement,decimal FinalAchievement,IReadOnlyList<PerformanceKpiDetail> KpiDetails,IReadOnlyList<PerformanceGateTrace> GateTrace,IReadOnlyList<FormulaVersionId> FormulaVersionIds,IReadOnlyList<ParameterSetVersionId> ParameterSetVersionIds,IReadOnlyList<LookupTableVersionId> LookupTableVersionIds,IReadOnlyList<RuleSetVersionId> RuleSetVersionIds,IReadOnlyList<PayrollInputLedgerEntryId> SourcePayrollInputLedgerEntryIds,PerformanceExplainNode ExplainTrace,string ResultHash,string EngineVersion,PerformanceExecutionMode ExecutionMode,string CorrelationId,IReadOnlyList<PayrollInputLedgerEntryDto> DerivedInputs);

public interface IPerformanceExpressionEvaluator
{
 decimal Score(KpiDefinitionVersionDto definition,KpiAssignmentDto assignment,KpiResultDto result,IReadOnlyDictionary<string,decimal> canonicalInputs);
 decimal Aggregate(PerformancePolicyVersionDto policy,IReadOnlyList<PerformanceKpiDetail> details,IReadOnlyDictionary<string,decimal> canonicalInputs);
 bool GateMatches(PerformanceGateDto gate,IReadOnlyDictionary<string,decimal> canonicalInputs);
 decimal GateResult(PerformanceGateDto gate,decimal current,IReadOnlyDictionary<string,decimal> canonicalInputs);
}
public interface IPerformanceScopeResolver{CompanyId? FindCompany(KpiScope scope);}
public interface IPerformanceService
{
 ValueTask<KpiDefinitionVersionDto> CreateKpiDraftAsync(CompanyId companyId,string code,string name,KpiDataType dataType,string unitType,KpiDirection direction,EffectivePeriod period,KpiDefinitionId? definitionId=null,decimal? minValue=null,decimal? targetValue=null,decimal? maxValue=null,CancellationToken cancellationToken=default);
 ValueTask<KpiDefinitionVersionDto> PublishKpiAsync(CompanyId companyId,KpiDefinitionVersionId id,CancellationToken cancellationToken=default);
 KpiDefinitionVersionDto ResolveKpi(CompanyId companyId,string code,DateOnly businessDate);
 ValueTask<KpiAssignmentDto> AssignAsync(CompanyId companyId,KpiDefinitionVersionId kpiVersionId,KpiScope scope,decimal weight,EffectivePeriod period,FormulaVersionId? scoringFormulaVersionId=null,CancellationToken cancellationToken=default);
 ValueTask<KpiResultDto> SubmitResultAsync(CompanyId companyId,KpiScope scope,KpiDefinitionVersionId kpiVersionId,PerformancePeriod period,KpiMeasuredValue value,KpiResultSourceType sourceType,string? sourceSystem,string? sourceReference,string? idempotencyKey,KpiResultId? supersedesResultId=null,CancellationToken cancellationToken=default);
 ValueTask<PerformancePolicyVersionDto> CreatePolicyDraftAsync(CompanyId companyId,string code,EffectivePeriod period,WeightNormalization normalization,IReadOnlyList<PerformanceGateDto> gates,IReadOnlyList<DerivedPayrollInputMapping> outputs,PerformancePolicyDefinitionId? definitionId=null,FormulaVersionId? overallFormulaVersionId=null,CancellationToken cancellationToken=default);
 ValueTask<PerformancePolicyVersionDto> PublishPolicyAsync(CompanyId companyId,PerformancePolicyVersionId id,CancellationToken cancellationToken=default);
 ValueTask<PerformanceEvaluationResultDto> EvaluateAsync(PerformanceEvaluationRequest request,CancellationToken cancellationToken=default);
}
