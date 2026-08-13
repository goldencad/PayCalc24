using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;

namespace PayCalc24.Contracts.PayrollReview;

public enum ReviewStatus { Clean, HasWarnings, HasErrors }
public enum VarianceType { UNCHANGED, INCREASE, DECREASE, NEW, REMOVED, UNAVAILABLE }
public enum VarianceDriver { InputChanged, FormulaVersionChanged, ParameterVersionChanged, ComponentVersionChanged, SchemeChanged, AssignmentChanged, PerformanceChanged, AttendanceChanged, FundPolicyChanged, FundCoverageChanged }
public enum FundReviewIndicator { FullyFunded, PartiallyFunded, NoDemand, Deficit }

public sealed record ReviewResultContext(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, PayrollCalculationRunId? CalculationRunId = null);

/// <summary>A source diagnostic plus its source-owned blocking semantic. Canonical arguments are never localized.</summary>
public sealed record ReviewSourceDiagnostic(string SourceModule, Diagnostic Diagnostic, bool IsBlocking = false,
    PayrollSubjectId? PayrollSubjectId = null, Guid? ComponentId = null, FundAllocationResultId? FundResultId = null,
    string? BusinessReference = null, string? ExplainReference = null);

public sealed record ValidationFinding(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId? SnapshotId, PayrollCalculationRunId? CalculationRunId,
    PayrollSubjectId? PayrollSubjectId, Guid? ComponentId, FundAllocationResultId? FundResultId,
    string SourceModule, DiagnosticSeverity Severity, string DiagnosticCode,
    IReadOnlyDictionary<string, object?> CanonicalArguments, bool IsBlocking,
    string? BusinessReference, string? ExplainReference);

public sealed record PayrollValidationSummary(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, PayrollCalculationRunId? CalculationRunId,
    int ErrorCount, int WarningCount, int InfoCount, bool Blocking, bool ReadyForReview,
    ReviewStatus Status, IReadOnlyList<ValidationFinding> Findings);

public sealed record ExplainNode(string NodeType, string? Code = null, PayrollInputValue? Value = null,
    string? DataType = null, string? VersionId = null, string? Hash = null,
    string? DiagnosticCode = null, IReadOnlyDictionary<string, object?>? CanonicalArguments = null,
    IReadOnlyList<ExplainNode>? Children = null);

public sealed record AttendanceInputProvenance(Guid ImportBatchId, string AttendanceSource,
    Guid AttendancePolicyVersionId, string DerivedOutputCode, IReadOnlyList<Guid> SourceFactIds,
    IReadOnlyList<Guid> CorrectionLineage);

public sealed record PerformanceInputProvenance(Guid PerformanceResultId, Guid PerformancePolicyVersionId,
    decimal OverallAchievement, decimal FinalAchievement, IReadOnlyList<Guid> SourceKpiResultIds,
    IReadOnlyList<Guid> KpiDefinitionVersionIds, IReadOnlyList<Guid> AssignmentIds,
    IReadOnlyList<Guid> AppliedGateIds, IReadOnlyList<FormulaVersionId> FormulaVersionIds,
    IReadOnlyList<ParameterSetVersionId> ParameterVersionIds, IReadOnlyList<LookupTableVersionId> LookupVersionIds,
    IReadOnlyList<RuleSetVersionId> RuleVersionIds, string ExplainHash);

public sealed record InputProvenanceExplanation(string InputCode, PayrollInputValue ResolvedValue,
    PayrollInputUnitType Unit, PayrollInputAggregationType Aggregation,
    IReadOnlyList<PayrollInputLedgerEntryId> ContributingLedgerEntryIds, string? SourceSystem,
    string? SourceReference, IReadOnlyList<PayrollInputLedgerEntryId> SupersessionLineage,
    AttendanceInputProvenance? Attendance, PerformanceInputProvenance? Performance);

public sealed record ComponentExplanation(PayComponentCalculationResultDto Result,
    IReadOnlyList<InputProvenanceExplanation> Inputs, IReadOnlyList<Guid> DependencyComponentIds,
    ExplainNode ExplainTree);

public sealed record FundExplanation(FundAllocationResultDto Result, string FundCode, int FundVersion,
    ExplainNode ExplainTree);

public sealed record PayrollExplainResult(PayrollSubjectId PayrollSubjectId, string EmployeeCode,
    PayrollPeriodId PayrollPeriodId, PayrollCalculationSnapshotId SnapshotId,
    PayrollCalculationRunId CalculationRunId, SnapshotSubjectFact HistoricalFacts,
    int? CompensationSchemeVersion, IReadOnlyList<ComponentExplanation> Components,
    IReadOnlyList<FundExplanation> Funds, IReadOnlyList<ValidationFinding> Diagnostics,
    string SnapshotHash, string? CalculationResultHash, string EngineVersion,
    PayrollExecutionMode ExecutionMode, string CorrelationId, ExplainNode ExplainTree);

public sealed record ValueProvenance(string ResultHash, string? FormulaVersionId,
    IReadOnlyList<string> InputEntryIds, IReadOnlyList<string> ParameterVersionIds,
    string? ComponentVersionId, string? SchemeVersionId, string? AssignmentId,
    string? AttendanceProvenanceId, string? PerformanceProvenanceId, string? FundPolicyVersionId,
    decimal? FundCoverage);

public sealed record ComparableValue(string Code, decimal Value, ValueProvenance Provenance);
public sealed record SubjectComparison(PayrollSubjectId PayrollSubjectId, string EmployeeCode,
    IReadOnlyList<ComparableValue> Components, IReadOnlyList<ComparableValue> FundedAmounts);

public sealed record VarianceItem(PayrollSubjectId PayrollSubjectId, string EmployeeCode, string Code,
    decimal? PriorValue, decimal? CurrentValue, decimal AbsoluteDelta, decimal? PercentageDelta,
    VarianceType VarianceType, IReadOnlyList<VarianceDriver> Drivers,
    ValueProvenance? PriorProvenance, ValueProvenance? CurrentProvenance);

public sealed record PayrollVarianceResult(ReviewResultContext Current, ReviewResultContext Comparison,
    IReadOnlyList<VarianceItem> Components, IReadOnlyList<VarianceItem> FundedAmounts,
    decimal PriorTotal, decimal CurrentTotal, decimal AbsoluteDelta, decimal? PercentageDelta);

public sealed record FundReview(FundAllocationResultId FundResultId, string FundCode, int FundVersion,
    decimal Available, decimal Demand, decimal Funded, decimal Unfunded, decimal Reserve,
    decimal RawCoverage, decimal EffectiveCoverage, FundAllocationMethod AllocationMethod,
    int MemberCount, FundReviewIndicator Indicator, string ResultHash);

public sealed record FundingReview(ReviewResultContext Context, IReadOnlyList<FundReview> Funds,
    decimal TotalAvailable, decimal TotalEligibleDemand, decimal TotalFunded,
    decimal TotalUnfunded, decimal TotalReserve, int DeficitCount, int WarningCount);

public sealed record PayrollPeriodReview(ReviewResultContext Context, int PopulationCount,
    int CalculatedSubjectCount, int FailedSubjectCount, int ComponentResultCount,
    PayrollValidationSummary Validation, FundingReview Funding);

/// <summary>Immutable, batched read model supplied by owning modules or optimized infrastructure.</summary>
public sealed record PayrollReviewDataset(ReviewResultContext Context, PayrollCalculationSnapshotDto Snapshot,
    PayrollCalculationRunDto? Run, IReadOnlyList<PayrollSubjectCalculationResultDto> SubjectResults,
    IReadOnlyList<PayComponentCalculationResultDto> ComponentResults,
    IReadOnlyList<FundAllocationResultDto> FundResults, IReadOnlyList<ReviewSourceDiagnostic> Diagnostics,
    IReadOnlyDictionary<PayrollInputLedgerEntryId, InputProvenanceExplanation> InputProvenance,
    IReadOnlyList<SubjectComparison> ComparableSubjects);

public interface IPayrollReviewSource { PayrollReviewDataset GetDataset(ReviewResultContext context); }

public interface IPayrollReviewService
{
    PayrollValidationSummary GetPayrollValidationSummary(ReviewResultContext context);
    IReadOnlyList<ValidationFinding> ListValidationFindings(ReviewResultContext context);
    IReadOnlyList<ValidationFinding> GetSubjectValidation(ReviewResultContext context, PayrollSubjectId subjectId);
    PayrollExplainResult ExplainPayrollSubject(ReviewResultContext context, PayrollSubjectId subjectId);
    ComponentExplanation ExplainComponent(ReviewResultContext context, PayComponentCalculationResultId componentResultId);
    InputProvenanceExplanation GetInputProvenance(ReviewResultContext context, PayrollInputLedgerEntryId entryId);
    FundExplanation ExplainFundAllocation(ReviewResultContext context, FundAllocationResultId fundResultId);
    PayrollVarianceResult ComparePayrollPeriods(ReviewResultContext current, ReviewResultContext comparison);
    IReadOnlyList<VarianceItem> GetSubjectVariance(ReviewResultContext current, ReviewResultContext comparison, PayrollSubjectId subjectId);
    IReadOnlyList<VarianceItem> GetComponentVariance(ReviewResultContext current, ReviewResultContext comparison, string componentCode);
    FundingReview GetFundingReview(ReviewResultContext context);
    FundReview GetFundReview(ReviewResultContext context, FundAllocationResultId fundResultId);
    PayrollPeriodReview GetPeriodReview(ReviewResultContext context);
}

public sealed class PayrollReviewException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}
