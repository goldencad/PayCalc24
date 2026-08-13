using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollInput;

namespace PayCalc24.Contracts.PayrollFunds;

public readonly record struct PayrollFundDefinitionId(Guid Value) { public static PayrollFundDefinitionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }
public readonly record struct PayrollFundVersionId(Guid Value) { public static PayrollFundVersionId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }
public readonly record struct FundAllocationResultId(Guid Value) { public static FundAllocationResultId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }
public readonly record struct FundMemberAllocationResultId(Guid Value) { public static FundMemberAllocationResultId From(Guid value)=>value==Guid.Empty?throw new ArgumentException("Identifier cannot be empty.",nameof(value)):new(value); }

public enum PayrollFundType { GENERAL, FIXED, VARIABLE, BONUS, COMMISSION, RESERVE, PROJECT, OTHER }
public enum FundScopeType { COMPANY, ORGANIZATION, TEAM, PROJECT, EmployeeGroup, OTHER }
public enum FundSourceType { FIXED, INPUT, FORMULA, PercentOfBase, EXTERNAL, OTHER }
public enum FundAllocationMethod { PROPORTIONAL, PRIORITY, SEQUENCE, EQUAL, WEIGHTED, CapFloor, CustomFormula, OTHER }
public enum FundLifecycleStatus { DRAFT, PUBLISHED, RETIRED }
public enum FundEligibilityStatus { ELIGIBLE, INELIGIBLE }

public sealed record FundScopeReference(FundScopeType Type,string? EntityType=null,Guid? EntityId=null);
public sealed record FundSourceConfiguration(FundSourceType Type,decimal? FixedAmount=null,string? InputCode=null,
    FormulaDefinitionId? FormulaDefinitionId=null,FormulaVersionId? FormulaVersionId=null,string? FormulaChecksum=null);
public sealed record FundAllocationPolicy(FundAllocationMethod Method,int DecimalScale=8,
    MidpointRounding Rounding=MidpointRounding.AwayFromZero,bool AllowDeficit=false,bool AllowReserve=true,
    decimal? MinCoverage=null,decimal? MaxCoverage=null);

public sealed record PayrollFundVersionDto(PayrollFundVersionId Id,PayrollFundDefinitionId DefinitionId,CompanyId CompanyId,
    string Code,string Name,string? Description,PayrollFundType FundType,FundScopeReference Scope,
    FundSourceConfiguration Source,FundAllocationPolicy Policy,string? CurrencyCode,PayrollFundDefinitionId? ParentFundDefinitionId,
    DateOnly EffectiveFrom,DateOnly? EffectiveTo,FundLifecycleStatus LifecycleStatus,int Revision,
    DateTimeOffset CreatedAt,UserId CreatedBy,DateTimeOffset? PublishedAt,UserId? PublishedBy);
public sealed record CreatePayrollFundDraft(CompanyId CompanyId,string Code,string Name,string? Description,
    PayrollFundType FundType,FundScopeReference Scope,FundSourceConfiguration Source,FundAllocationPolicy Policy,
    DateOnly EffectiveFrom,DateOnly? EffectiveTo=null,string? CurrencyCode=null,PayrollFundDefinitionId? ParentFundDefinitionId=null);
public sealed record UpdatePayrollFundDraft(CompanyId CompanyId,PayrollFundVersionId VersionId,string Name,string? Description,
    PayrollFundType FundType,FundScopeReference Scope,FundSourceConfiguration Source,FundAllocationPolicy Policy,
    DateOnly EffectiveFrom,DateOnly? EffectiveTo=null,string? CurrencyCode=null,PayrollFundDefinitionId? ParentFundDefinitionId=null);

public interface IPayrollFundDefinitionService
{
    ValueTask<PayrollFundVersionDto> CreateDraftAsync(CreatePayrollFundDraft c,CancellationToken token=default);
    ValueTask<PayrollFundVersionDto> UpdateDraftAsync(UpdatePayrollFundDraft c,CancellationToken token=default);
    ValueTask<PayrollFundVersionDto> CreateRevisionAsync(CompanyId companyId,PayrollFundVersionId basedOn,CancellationToken token=default);
    ValueTask<PayrollFundVersionDto> PublishAsync(CompanyId companyId,PayrollFundVersionId id,CancellationToken token=default);
    ValueTask<PayrollFundVersionDto> RetireAsync(CompanyId companyId,PayrollFundVersionId id,DateOnly effectiveTo,CancellationToken token=default);
    PayrollFundVersionDto GetVersion(CompanyId c,PayrollFundVersionId id);
    PayrollFundVersionDto ResolveEffective(CompanyId c,string code,DateOnly businessDate);
    IReadOnlyList<PayrollFundVersionDto> Search(CompanyId c,string? code=null,FundLifecycleStatus? status=null);
}

/// <summary>Immutable fund configuration pinned by Task 09 before any calculation begins.</summary>
public sealed record SnapshotPayrollFundVersion(PayrollFundVersionId FundVersionId,PayrollFundDefinitionId FundDefinitionId,
    string Code,int Revision,PayrollFundType FundType,FundScopeReference Scope,FundSourceConfiguration Source,FundAllocationPolicy Policy,
    string? CurrencyCode=null,PayrollFundDefinitionId? ParentFundDefinitionId=null);

public sealed record FundRequirement(string RequirementReference,CompanyId CompanyId,decimal RequiredAmount,
    PayrollSubjectId? PayrollSubjectId=null,Guid? PayComponentId=null,decimal? EligibleAmount=null,int Priority=0,
    decimal Weight=1m,decimal? FloorAmount=null,decimal? TargetAmount=null,decimal? CapAmount=null,
    FundEligibilityStatus EligibilityStatus=FundEligibilityStatus.ELIGIBLE,string ProvenanceType="CONFIGURATION",
    IReadOnlyList<Guid>? ProvenanceIds=null);
public sealed record CalculatePayrollFund(CompanyId CompanyId,PayrollCalculationSnapshotId SnapshotId,
    PayrollFundVersionId FundVersionId,PayrollExecutionMode ExecutionMode,string IdempotencyKey,
    IReadOnlyList<FundRequirement> Requirements,PayrollCalculationRunId? CalculationRunId=null,string? ScenarioId=null,
    decimal? ExplicitAvailableFund=null);

public sealed record FundMemberAllocationResultDto(FundMemberAllocationResultId Id,FundAllocationResultId FundAllocationResultId,
    string RequirementReference,PayrollSubjectId? PayrollSubjectId,Guid? PayComponentId,decimal RequestedAmount,
    decimal EligibleAmount,decimal AllocatedAmount,decimal Weight,int Priority,decimal? FloorAmount,decimal? TargetAmount,
    decimal? CapAmount,int AllocationSequence,string ProvenanceType,IReadOnlyList<Guid> ProvenanceIds,string ResultHash);
public sealed record FundAllocationResultDto(FundAllocationResultId Id,CompanyId CompanyId,PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId,int SnapshotRevision,PayrollCalculationRunId? CalculationRunId,
    PayrollFundDefinitionId FundDefinitionId,PayrollFundVersionId FundVersionId,FundScopeReference Scope,
    decimal AvailableFund,decimal EligibleDemand,decimal FundedAmount,decimal UnfundedAmount,decimal ReserveAmount,
    decimal RawCoverageRatio,decimal EffectiveFundingRatio,FundAllocationMethod AllocationMethod,
    PayrollExecutionMode ExecutionMode,string EngineVersion,string CorrelationId,string? ScenarioId,string IdempotencyKey,
    string SnapshotHash,string SourceProvenanceJson,string ExplainTraceJson,string ResultHash,DateTimeOffset CreatedAt,
    IReadOnlyList<FundMemberAllocationResultDto> Members);

public interface IPayrollFundCalculationService
{
    ValueTask<FundAllocationResultDto> CalculateAsync(CalculatePayrollFund c,CancellationToken token=default);
    FundAllocationResultDto GetResult(CompanyId c,FundAllocationResultId id);
    FundAllocationResultDto? ResolveByIdempotencyKey(CompanyId c,PayrollCalculationSnapshotId s,string key);
    IReadOnlyList<FundAllocationResultDto> ListResults(CompanyId c,PayrollCalculationSnapshotId s);
    IReadOnlyList<FundMemberAllocationResultDto> ListMemberAllocations(CompanyId c,FundAllocationResultId id);
}

public sealed class PayrollFundException(Diagnostic diagnostic):Exception(diagnostic.Code) { public Diagnostic Diagnostic { get; }=diagnostic; }
