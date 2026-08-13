using PayCalc24.Contracts.Compensation;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollInput;

namespace PayCalc24.Contracts.PayrollCalculation;

public readonly record struct PayrollCalculationSnapshotId(Guid Value)
{
    public static PayrollCalculationSnapshotId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}
public readonly record struct PayrollCalculationRunId(Guid Value)
{
    public static PayrollCalculationRunId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}
public readonly record struct PayrollSubjectCalculationResultId(Guid Value)
{
    public static PayrollSubjectCalculationResultId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}
public readonly record struct PayComponentCalculationResultId(Guid Value)
{
    public static PayComponentCalculationResultId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}

public enum PayrollPeriodStatus { DRAFT, PREPARED, FROZEN, CALCULATED, CLOSED, REOPENED }
public enum PayrollExecutionMode { Production, Replay, BackTest, WhatIf }
public enum PayrollCalculationRunStatus { PENDING, RUNNING, SUCCEEDED, FAILED }
public enum PayrollCalculationResultStatus { SUCCEEDED, FAILED }

/// <summary>PeriodStart and PeriodEnd are inclusive payroll coverage dates. BusinessDate resolves half-open configuration intervals.</summary>
public sealed record PayrollPeriodDto(
    PayrollPeriodId Id, CompanyId CompanyId, string Code, string? Name,
    DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly BusinessDate, DateOnly? PaymentDate,
    PayrollPeriodStatus LifecycleStatus, long Revision, DateTimeOffset CreatedAt, UserId CreatedBy,
    DateTimeOffset? UpdatedAt, UserId? UpdatedBy, DateTimeOffset? PreparedAt, UserId? PreparedBy,
    DateTimeOffset? FrozenAt, UserId? FrozenBy, DateTimeOffset? CalculatedAt, UserId? CalculatedBy,
    DateTimeOffset? ClosedAt, UserId? ClosedBy, DateTimeOffset? ReopenedAt, UserId? ReopenedBy);

public sealed record CreatePayrollPeriod(CompanyId CompanyId, string Code, string? Name,
    DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly BusinessDate, DateOnly? PaymentDate = null);
public sealed record UpdatePayrollPeriodDraft(CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    long ExpectedRevision, string? Name, DateOnly PeriodStart, DateOnly PeriodEnd,
    DateOnly BusinessDate, DateOnly? PaymentDate = null);
public sealed record PayrollPeriodSearch(PayrollPeriodStatus? Status = null, DateOnly? From = null, DateOnly? To = null);

public sealed record PayrollPeriodLifecycleEventDto(Guid Id, CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollPeriodStatus? FromStatus, PayrollPeriodStatus ToStatus, long PeriodRevision,
    int? SnapshotRevision, string? Reason, DateTimeOffset OccurredAt, UserId Actor, string CorrelationId);

public sealed record PreparationDiagnostic(string Code, DiagnosticSeverity Severity,
    PayrollSubjectId? PayrollSubjectId = null, string? ReferenceCode = null,
    IReadOnlyDictionary<string, object?>? Arguments = null);

public sealed record SnapshotSubjectFact(
    PayrollSubjectId PayrollSubjectId, string EmployeeCode, PayrollAssignmentId PayrollAssignmentId,
    OrganizationUnitId OrganizationUnitId, PositionId? PositionId, JobGradeId? JobGradeId,
    CompensationSchemeId? CompensationSchemeId, DateOnly AssignmentEffectiveFrom,
    DateOnly? AssignmentEffectiveTo, int EligibleDependentCount,
    IReadOnlyList<EmployeeDependentId> EligibleDependentIds);

public sealed record SnapshotResolvedInput(
    PayrollSubjectId PayrollSubjectId, PayrollInputDefinitionId PayrollInputDefinitionId,
    int DefinitionRevision, string Code, PayrollInputDataType DataType, PayrollInputUnitType Unit,
    PayrollInputAggregationType Aggregation, PayrollInputValue ResolvedValue,
    IReadOnlyList<PayrollInputLedgerEntryId> ContributingLedgerEntryIds);

public sealed record SnapshotCompensationVersion(CompensationSchemeId CompensationSchemeId,
    int SchemeVersion, IReadOnlyList<SnapshotPayComponentVersion> Components);
public sealed record SnapshotPayComponentVersion(PayComponentId PayComponentId, int Version, int Sequence,
    CalculationMethod CalculationMethod, FormulaDefinitionId? FormulaDefinitionId,
    string? ComponentCode = null, bool Required = true, string? SourceReference = null,
    string? ExpectedDataType = null, IReadOnlyList<PayComponentId>? DependsOn = null);
public sealed record SnapshotFormulaVersion(FormulaDefinitionId FormulaDefinitionId, FormulaVersionId FormulaVersionId,
    int Revision, string Checksum, string? Expression = null);
public sealed record SnapshotParameterVersion(ParameterSetVersionId ParameterSetVersionId, string Code, int Revision,
    IReadOnlyList<ParameterValueDto> Values);
public sealed record SnapshotLookupVersion(LookupTableVersionId LookupTableVersionId, string Code, int Revision,
    IReadOnlyList<LookupRowDto> Rows);
public sealed record SnapshotRuleSetVersion(RuleSetVersionId RuleSetVersionId, string Code, int Revision,
    bool StopOnMatch, IReadOnlyList<RuleDto> Rules);

public sealed record SnapshotHistoricalFacts(IReadOnlyList<SnapshotSubjectFact> Subjects,
    IReadOnlyList<SnapshotResolvedInput> Inputs);
public sealed record SnapshotPolicyConfiguration(
    IReadOnlyList<SnapshotCompensationVersion> CompensationVersions,
    IReadOnlyList<SnapshotFormulaVersion> FormulaVersions,
    IReadOnlyList<SnapshotParameterVersion> ParameterVersions,
    IReadOnlyList<SnapshotLookupVersion> LookupVersions,
    IReadOnlyList<SnapshotRuleSetVersion> RuleSetVersions);

public sealed record PayrollCalculationSnapshotDto(
    PayrollCalculationSnapshotId Id, CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    int SnapshotRevision, PayrollExecutionMode ExecutionMode, DateOnly BusinessDate,
    DateTimeOffset CreatedAt, UserId CreatedBy, DateTimeOffset FrozenAt, UserId FrozenBy,
    string PopulationHash, string InputHash, string ConfigurationHash, string SnapshotHash,
    SnapshotHistoricalFacts HistoricalFacts, SnapshotPolicyConfiguration PolicyConfiguration);

public sealed record StartPayrollCalculation(CompanyId CompanyId, PayrollCalculationSnapshotId SnapshotId,
    PayrollExecutionMode ExecutionMode, string IdempotencyKey, string? ExpectedSnapshotHash = null,
    SnapshotPolicyConfiguration? AlternativePolicy = null);

public sealed record PayrollCalculationRunDto(PayrollCalculationRunId Id, CompanyId CompanyId,
    PayrollPeriodId PayrollPeriodId, PayrollCalculationSnapshotId SnapshotId, int SnapshotRevision,
    PayrollExecutionMode ExecutionMode, string EngineVersion, PayrollCalculationRunStatus Status,
    DateTimeOffset StartedAt, UserId StartedBy, DateTimeOffset? CompletedAt, UserId? CompletedBy,
    string CorrelationId, string IdempotencyKey, string SnapshotHash, string? ResultHash,
    string? FailureDiagnosticCode, bool IsAuthoritative);

public sealed record PayComponentCalculationResultDto(PayComponentCalculationResultId Id,
    PayrollCalculationRunId CalculationRunId, CompanyId CompanyId, PayrollPeriodId PayrollPeriodId,
    PayrollCalculationSnapshotId SnapshotId, PayrollSubjectId PayrollSubjectId,
    CompensationSchemeId CompensationSchemeVersionId, PayComponentId PayComponentId, int PayComponentVersion,
    string ComponentCode, int Sequence, CalculationMethod CalculationMethod,
    PayrollCalculationResultStatus Status, PayrollInputValue? ResultValue, string? ResultDataType,
    FormulaDefinitionId? FormulaDefinitionId, FormulaVersionId? FormulaVersionId, string? FormulaChecksum,
    string? ExplainTraceJson, string? DiagnosticCode,
    IReadOnlyList<PayrollInputLedgerEntryId> InputLedgerEntryIds,
    IReadOnlyList<ParameterSetVersionId> ParameterSetVersionIds,
    IReadOnlyList<LookupTableVersionId> LookupTableVersionIds,
    IReadOnlyList<RuleSetVersionId> RuleSetVersionIds,
    string EngineVersion, PayrollExecutionMode ExecutionMode, string CorrelationId,
    string ResultHash, DateTimeOffset CreatedAt);

public sealed record PayrollSubjectCalculationResultDto(PayrollSubjectCalculationResultId Id,
    PayrollCalculationRunId CalculationRunId, CompanyId CompanyId, PayrollSubjectId PayrollSubjectId,
    string EmployeeCode, int ComponentResultCount, PayrollCalculationResultStatus CalculationStatus,
    string ResultHash, string? DiagnosticCode, DateTimeOffset CreatedAt);

public interface IPayrollCalculationService
{
    ValueTask<PayrollCalculationRunDto> StartAsync(StartPayrollCalculation command, CancellationToken token = default);
    PayrollCalculationRunDto GetRun(CompanyId companyId, PayrollCalculationRunId runId);
    PayrollCalculationRunDto? ResolveByIdempotencyKey(CompanyId companyId, PayrollCalculationSnapshotId snapshotId, string idempotencyKey);
    PayrollCalculationRunDto? GetAuthoritativeResult(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
    IReadOnlyList<PayrollSubjectCalculationResultDto> ListSubjectResults(CompanyId companyId, PayrollCalculationRunId runId);
    PayrollSubjectCalculationResultDto GetSubjectResult(CompanyId companyId, PayrollCalculationRunId runId, PayrollSubjectId subjectId);
    IReadOnlyList<PayComponentCalculationResultDto> ListComponentResults(CompanyId companyId, PayrollCalculationRunId runId, PayrollSubjectId? subjectId = null);
    PayComponentCalculationResultDto GetComponentResult(CompanyId companyId, PayComponentCalculationResultId resultId);
}

/// <summary>Resolved candidate package. Implementations read canonical Task 04-07 contracts; the state machine validates and freezes it.</summary>
public sealed record PayrollSnapshotCandidate(CompanyId CompanyId, SnapshotHistoricalFacts HistoricalFacts,
    SnapshotPolicyConfiguration PolicyConfiguration, IReadOnlyList<PreparationDiagnostic> Diagnostics);

public interface IPayrollSnapshotResolver
{
    PayrollSnapshotCandidate Resolve(CompanyId companyId, PayrollPeriodId payrollPeriodId, DateOnly businessDate);
}

public interface IPayrollPeriodService
{
    ValueTask<PayrollPeriodDto> CreateAsync(CreatePayrollPeriod command, CancellationToken token = default);
    ValueTask<PayrollPeriodDto> UpdateDraftAsync(UpdatePayrollPeriodDraft command, CancellationToken token = default);
    PayrollPeriodDto GetById(CompanyId companyId, PayrollPeriodId periodId);
    IReadOnlyList<PayrollPeriodDto> Search(CompanyId companyId, PayrollPeriodSearch search);
    ValueTask<PayrollPeriodDto> PrepareAsync(CompanyId companyId, PayrollPeriodId periodId, long expectedRevision, CancellationToken token = default);
    IReadOnlyList<PreparationDiagnostic> GetPreparationDiagnostics(CompanyId companyId, PayrollPeriodId periodId);
    ValueTask<PayrollPeriodDto> ResetPreparationAsync(CompanyId companyId, PayrollPeriodId periodId, long expectedRevision, CancellationToken token = default);
    ValueTask<PayrollCalculationSnapshotDto> FreezeAsync(CompanyId companyId, PayrollPeriodId periodId, long expectedRevision, string idempotencyKey, CancellationToken token = default);
    ValueTask<PayrollPeriodDto> MarkCalculatedAsync(CompanyId companyId, PayrollPeriodId periodId, long expectedRevision, CancellationToken token = default);
    ValueTask<PayrollPeriodDto> CloseAsync(CompanyId companyId, PayrollPeriodId periodId, long expectedRevision, CancellationToken token = default);
    ValueTask<PayrollPeriodDto> ReopenAsync(CompanyId companyId, PayrollPeriodId periodId, long expectedRevision, string reason, CancellationToken token = default);
    IReadOnlyList<PayrollPeriodLifecycleEventDto> GetLifecycleHistory(CompanyId companyId, PayrollPeriodId periodId);
}

public interface IPayrollSnapshotQueryService
{
    PayrollCalculationSnapshotDto GetAuthoritative(CompanyId companyId, PayrollPeriodId periodId);
    PayrollCalculationSnapshotDto GetSnapshotById(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
    PayrollCalculationSnapshotDto GetByRevision(CompanyId companyId, PayrollPeriodId periodId, int revision);
    IReadOnlyList<PayrollCalculationSnapshotDto> ListRevisions(CompanyId companyId, PayrollPeriodId periodId);
    IReadOnlyList<SnapshotSubjectFact> GetSubjects(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
    IReadOnlyList<SnapshotResolvedInput> GetSubjectInputs(CompanyId companyId, PayrollCalculationSnapshotId snapshotId, PayrollSubjectId subjectId);
    IReadOnlyList<SnapshotCompensationVersion> GetCompensationVersions(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
    IReadOnlyList<SnapshotFormulaVersion> GetFormulaVersions(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
    IReadOnlyList<SnapshotParameterVersion> GetParameterVersions(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
    IReadOnlyList<SnapshotLookupVersion> GetLookupVersions(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
    IReadOnlyList<SnapshotRuleSetVersion> GetRuleSetVersions(CompanyId companyId, PayrollCalculationSnapshotId snapshotId);
}

public static class PayrollAuditActions
{
    public const string Created="PAYROLL_PERIOD.CREATED"; public const string DraftUpdated="PAYROLL_PERIOD.DRAFT_UPDATED";
    public const string Prepared="PAYROLL_PERIOD.PREPARED"; public const string PreparationReset="PAYROLL_PERIOD.PREPARATION_RESET";
    public const string Frozen="PAYROLL_PERIOD.FROZEN"; public const string SnapshotCreated="PAYROLL_SNAPSHOT.CREATED";
    public const string Calculated="PAYROLL_PERIOD.CALCULATED"; public const string Closed="PAYROLL_PERIOD.CLOSED";
    public const string Reopened="PAYROLL_PERIOD.REOPENED";
}
