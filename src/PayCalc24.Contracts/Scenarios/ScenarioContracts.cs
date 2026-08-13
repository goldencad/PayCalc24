using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.PayrollReview;

namespace PayCalc24.Contracts.Scenarios;

public readonly record struct ScenarioDefinitionId(Guid Value)
{
    public static ScenarioDefinitionId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}

public readonly record struct ScenarioSnapshotId(Guid Value)
{
    public static ScenarioSnapshotId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}

public readonly record struct ScenarioExecutionId(Guid Value)
{
    public static ScenarioExecutionId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Identifier cannot be empty.", nameof(value)) : new(value);
}

public enum ScenarioType { Replay, BackTest, WhatIf }
public enum ScenarioStatus { DRAFT, READY, RUNNING, SUCCEEDED, FAILED, ARCHIVED }
public enum ScenarioExecutionStatus { RUNNING, SUCCEEDED, FAILED }
public enum ScenarioPolicyKind { CompensationSchemeVersion, PayComponentVersion, FormulaVersion, ParameterSetVersion, LookupTableVersion, RuleSetVersion, FundVersion }

public sealed record ScenarioDefinitionDto(ScenarioDefinitionId Id, CompanyId CompanyId, string Code,
    string Name, string? Description, ScenarioType ScenarioType, PayrollPeriodId? BaselinePayrollPeriodId,
    PayrollCalculationSnapshotId? BaselineSnapshotId, int? BaselineSnapshotRevision, ScenarioStatus Status,
    UserId CreatedBy, DateTimeOffset CreatedAt, string CorrelationId);

public sealed record CreateScenarioDefinition(CompanyId CompanyId, string Code, string Name, string? Description,
    ScenarioType ScenarioType, PayrollPeriodId? BaselinePayrollPeriodId = null,
    PayrollCalculationSnapshotId? BaselineSnapshotId = null, int? BaselineSnapshotRevision = null);

public sealed record UpdateScenarioDraft(CompanyId CompanyId, ScenarioDefinitionId ScenarioDefinitionId,
    string Name, string? Description);

public sealed record ScenarioPolicyOverride(ScenarioPolicyKind PolicyKind, Guid? BaselineVersionId,
    Guid OverrideVersionId, string? Reason = null, int Order = 0);

public sealed record ScenarioInputOverride(PayrollSubjectId PayrollSubjectId,
    PayrollInputDefinitionId PayrollInputDefinitionId, string InputCode, PayrollInputValue? OriginalValue,
    PayrollInputValue OverrideValue, PayrollInputDataType DataType, PayrollInputUnitType Unit,
    string? Reason = null, int Sequence = 0);

public sealed record FinalizeScenarioSnapshot(CompanyId CompanyId, ScenarioDefinitionId ScenarioDefinitionId,
    PayrollCalculationSnapshotId BaselineSnapshotId, string ExpectedBaselineSnapshotHash, DateOnly BusinessDate,
    SnapshotPolicyConfiguration PolicyConfiguration, IReadOnlyList<ScenarioPolicyOverride>? PolicyOverrides = null,
    IReadOnlyList<ScenarioInputOverride>? InputOverrides = null,
    IReadOnlyDictionary<string, string>? EngineVersions = null);

public sealed record ScenarioSnapshotDto(ScenarioSnapshotId Id, ScenarioDefinitionId ScenarioDefinitionId,
    CompanyId CompanyId, int Revision, ScenarioType ScenarioType, PayrollExecutionMode ExecutionMode,
    PayrollPeriodId BaselinePayrollPeriodId, PayrollCalculationSnapshotId BaselineSnapshotId,
    int BaselineSnapshotRevision, string BaselineSnapshotHash, DateOnly BusinessDate,
    SnapshotHistoricalFacts HistoricalFacts, SnapshotPolicyConfiguration PolicyConfiguration,
    IReadOnlyList<ScenarioPolicyOverride> PolicyOverrides, IReadOnlyList<ScenarioInputOverride> InputOverrides,
    IReadOnlyDictionary<string, string> EngineVersions, UserId CreatedBy, DateTimeOffset CreatedAt,
    string ScenarioHash);

public sealed record ExecuteScenario(CompanyId CompanyId, ScenarioSnapshotId ScenarioSnapshotId,
    string IdempotencyKey, string? ExpectedScenarioHash = null,
    IReadOnlyList<FundRequirement>? FundRequirements = null);

public sealed record ScenarioExecutionResultDto(ScenarioExecutionId Id, CompanyId CompanyId,
    ScenarioSnapshotId ScenarioSnapshotId, int ScenarioRevision, PayrollExecutionMode ExecutionMode,
    ScenarioExecutionStatus Status, PayrollCalculationRunId? CalculationRunId,
    IReadOnlyList<FundAllocationResultId> FundResultIds, string ScenarioHash, string? ResultHash,
    IReadOnlyDictionary<string, string> EngineVersions, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt,
    string CorrelationId, string IdempotencyKey, string? DiagnosticCode);

public sealed record ScenarioProvenance(ScenarioDefinitionId ScenarioDefinitionId,
    ScenarioSnapshotId ScenarioSnapshotId, int Revision, string ScenarioHash,
    PayrollCalculationSnapshotId BaselineSnapshotId, string BaselineSnapshotHash,
    IReadOnlyList<ScenarioPolicyOverride> PolicyOverrides, IReadOnlyList<ScenarioInputOverride> InputOverrides,
    PayrollCalculationRunId? CalculationRunId, IReadOnlyList<FundAllocationResultId> FundResultIds,
    IReadOnlyDictionary<string, string> EngineVersions, PayrollExecutionMode ExecutionMode, string CorrelationId);

public sealed record ScenarioValidationResult(bool IsValid, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record ScenarioComparison(ReviewResultContext Baseline, ReviewResultContext Scenario,
    PayrollVarianceResult PayrollVariance, FundingReview BaselineFunding, FundingReview ScenarioFunding);

public interface IScenarioService
{
    ValueTask<ScenarioDefinitionDto> CreateDraftAsync(CreateScenarioDefinition command, CancellationToken token = default);
    ValueTask<ScenarioDefinitionDto> UpdateDraftAsync(UpdateScenarioDraft command, CancellationToken token = default);
    ScenarioDefinitionDto GetScenario(CompanyId companyId, ScenarioDefinitionId id);
    ValueTask<ScenarioSnapshotDto> FinalizeSnapshotAsync(FinalizeScenarioSnapshot command, CancellationToken token = default);
    ScenarioSnapshotDto GetSnapshot(CompanyId companyId, ScenarioSnapshotId id);
    IReadOnlyList<ScenarioSnapshotDto> ListRevisions(CompanyId companyId, ScenarioDefinitionId id);
    ScenarioValidationResult Validate(CompanyId companyId, ScenarioSnapshotId id);
    ValueTask<ScenarioExecutionResultDto> ExecuteAsync(ExecuteScenario command, CancellationToken token = default);
    ScenarioExecutionResultDto GetExecution(CompanyId companyId, ScenarioExecutionId id);
    ScenarioExecutionResultDto? ResolveByIdempotencyKey(CompanyId companyId, ScenarioSnapshotId snapshotId, string key);
    ScenarioProvenance GetProvenance(CompanyId companyId, ScenarioExecutionId id);
    ScenarioComparison Compare(CompanyId companyId, ReviewResultContext baseline, ReviewResultContext scenario);
}

public sealed class ScenarioException(Diagnostic diagnostic) : Exception(diagnostic.Code)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}
