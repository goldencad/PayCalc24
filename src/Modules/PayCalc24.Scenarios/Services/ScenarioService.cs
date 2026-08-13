using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Operations;
using PayCalc24.Contracts.PayrollCalculation;
using PayCalc24.Contracts.PayrollFunds;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.Contracts.PayrollReview;
using PayCalc24.Contracts.Scenarios;

namespace PayCalc24.Scenarios.Services;

/// <summary>Isolated orchestration over pinned facts/policy; calculation, fund, and variance semantics remain owned by Tasks 10, 11, and 15.</summary>
public sealed class ScenarioService(ICompanyContext companyContext, ICurrentUser currentUser,
    ICorrelationContext correlationContext, TimeProvider timeProvider, IPayrollSnapshotQueryService snapshots,
    IPayrollCalculationService calculations, IPayrollFundCalculationService funds,
    IPayrollReviewService reviews) : IScenarioService
{
    private readonly List<ScenarioDefinitionDto> definitions = [];
    private readonly List<ScenarioSnapshotDto> scenarioSnapshots = [];
    private readonly List<ScenarioExecutionResultDto> executions = [];
    private readonly Dictionary<(CompanyId, ScenarioSnapshotId, string), (string Fingerprint, ScenarioExecutionId Id)> keys = [];
    private readonly Lock gate = new();

    public ValueTask<ScenarioDefinitionDto> CreateDraftAsync(CreateScenarioDefinition command, CancellationToken token = default)
    {
        Scope(command.CompanyId);
        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name)) throw Error(DiagnosticCodes.ScenarioBlockingValidationFailed);
        lock (gate)
        {
            if (definitions.Any(x => x.CompanyId == command.CompanyId && StringComparer.OrdinalIgnoreCase.Equals(x.Code, command.Code)))
                throw Error(DiagnosticCodes.ScenarioBlockingValidationFailed, ("code", command.Code));
            var item = new ScenarioDefinitionDto(ScenarioDefinitionId.From(Guid.NewGuid()), command.CompanyId,
                command.Code.Trim(), command.Name.Trim(), command.Description, command.ScenarioType,
                command.BaselinePayrollPeriodId, command.BaselineSnapshotId, command.BaselineSnapshotRevision,
                ScenarioStatus.DRAFT, currentUser.UserId, timeProvider.GetUtcNow(), correlationContext.CorrelationId);
            definitions.Add(item);
            return ValueTask.FromResult(item);
        }
    }

    public ValueTask<ScenarioDefinitionDto> UpdateDraftAsync(UpdateScenarioDraft command, CancellationToken token = default)
    {
        Scope(command.CompanyId);
        lock (gate)
        {
            var current = FindDefinition(command.CompanyId, command.ScenarioDefinitionId);
            if (current.Status != ScenarioStatus.DRAFT || string.IsNullOrWhiteSpace(command.Name)) throw Error(DiagnosticCodes.ScenarioBlockingValidationFailed);
            var updated = current with { Name = command.Name.Trim(), Description = command.Description };
            definitions[definitions.IndexOf(current)] = updated;
            return ValueTask.FromResult(updated);
        }
    }

    public ScenarioDefinitionDto GetScenario(CompanyId companyId, ScenarioDefinitionId id) { Scope(companyId); return FindDefinition(companyId, id); }

    public ValueTask<ScenarioSnapshotDto> FinalizeSnapshotAsync(FinalizeScenarioSnapshot command, CancellationToken token = default)
    {
        Scope(command.CompanyId);
        var definition = FindDefinition(command.CompanyId, command.ScenarioDefinitionId);
        if (definition.Status is ScenarioStatus.ARCHIVED or ScenarioStatus.RUNNING) throw Error(DiagnosticCodes.ScenarioBlockingValidationFailed);
        PayrollCalculationSnapshotDto baseline;
        try { baseline = snapshots.GetSnapshotById(command.CompanyId, command.BaselineSnapshotId); }
        catch { throw Error(DiagnosticCodes.ScenarioBaselineNotFound); }
        if (baseline.CompanyId != command.CompanyId) throw Error(DiagnosticCodes.ScenarioCrossCompanyReference);
        if (!StringComparer.Ordinal.Equals(baseline.SnapshotHash, command.ExpectedBaselineSnapshotHash)) throw Error(DiagnosticCodes.ScenarioBaselineHashInvalid);
        var policyOverrides = (command.PolicyOverrides ?? []).OrderBy(x => x.Order).ThenBy(x => x.PolicyKind).ThenBy(x => x.OverrideVersionId).ToArray();
        var inputOverrides = (command.InputOverrides ?? []).OrderBy(x => x.Sequence).ThenBy(x => x.PayrollSubjectId.Value).ThenBy(x => x.InputCode, StringComparer.Ordinal).ToArray();
        ValidateOverrides(baseline, command.PolicyConfiguration, policyOverrides, inputOverrides, definition.ScenarioType);
        var facts = ApplyInputOverrides(baseline.HistoricalFacts, inputOverrides);
        var engines = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var engine in command.EngineVersions ?? new Dictionary<string, string>()) engines.Add(engine.Key, engine.Value);
        lock (gate)
        {
            var revision = scenarioSnapshots.Where(x => x.ScenarioDefinitionId == definition.Id).Select(x => x.Revision).DefaultIfEmpty().Max() + 1;
            var hash = ScenarioHash(command.CompanyId, definition.ScenarioType, baseline, command.BusinessDate, facts,
                command.PolicyConfiguration, policyOverrides, inputOverrides, engines);
            var item = new ScenarioSnapshotDto(ScenarioSnapshotId.From(Guid.NewGuid()), definition.Id, command.CompanyId,
                revision, definition.ScenarioType, Mode(definition.ScenarioType), baseline.PayrollPeriodId, baseline.Id,
                baseline.SnapshotRevision, baseline.SnapshotHash, command.BusinessDate, facts, command.PolicyConfiguration,
                policyOverrides, inputOverrides, engines, currentUser.UserId, timeProvider.GetUtcNow(), hash);
            scenarioSnapshots.Add(item);
            definitions[definitions.IndexOf(definition)] = definition with { Status = ScenarioStatus.READY };
            return ValueTask.FromResult(item);
        }
    }

    public ScenarioSnapshotDto GetSnapshot(CompanyId companyId, ScenarioSnapshotId id) { Scope(companyId); return FindSnapshot(companyId, id); }
    public IReadOnlyList<ScenarioSnapshotDto> ListRevisions(CompanyId companyId, ScenarioDefinitionId id)
    { Scope(companyId); _ = FindDefinition(companyId, id); return scenarioSnapshots.Where(x => x.CompanyId == companyId && x.ScenarioDefinitionId == id).OrderBy(x => x.Revision).ToArray(); }

    public ScenarioValidationResult Validate(CompanyId companyId, ScenarioSnapshotId id)
    {
        Scope(companyId); var item = FindSnapshot(companyId, id); var diagnostics = new List<Diagnostic>();
        PayrollCalculationSnapshotDto baseline;
        try { baseline = snapshots.GetSnapshotById(companyId, item.BaselineSnapshotId); }
        catch { diagnostics.Add(Diagnostic(DiagnosticCodes.ScenarioBaselineNotFound)); return new(false, diagnostics); }
        if (!StringComparer.Ordinal.Equals(baseline.SnapshotHash, item.BaselineSnapshotHash)) diagnostics.Add(Diagnostic(DiagnosticCodes.ScenarioBaselineHashInvalid));
        if (item.EngineVersions.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value))) diagnostics.Add(Diagnostic(DiagnosticCodes.ScenarioEngineIncompatible));
        return new(diagnostics.Count == 0, diagnostics);
    }

    public async ValueTask<ScenarioExecutionResultDto> ExecuteAsync(ExecuteScenario command, CancellationToken token = default)
    {
        Scope(command.CompanyId);
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) throw new ArgumentException("Idempotency key is required.", nameof(command));
        var snapshot = FindSnapshot(command.CompanyId, command.ScenarioSnapshotId);
        if (command.ExpectedScenarioHash is not null && !StringComparer.Ordinal.Equals(command.ExpectedScenarioHash, snapshot.ScenarioHash)) throw Error(DiagnosticCodes.ScenarioBlockingValidationFailed);
        var validation = Validate(command.CompanyId, command.ScenarioSnapshotId);
        if (!validation.IsValid) throw Error(DiagnosticCodes.ScenarioBlockingValidationFailed);
        var fingerprint = Hash($"{snapshot.ScenarioHash}|{string.Join(';', (command.FundRequirements ?? []).OrderBy(x => x.RequirementReference, StringComparer.Ordinal).Select(x => $"{x.RequirementReference}:{Canonical(x.RequiredAmount)}"))}");
        ScenarioExecutionResultDto result;
        lock (gate)
        {
            if (keys.TryGetValue((command.CompanyId, snapshot.Id, command.IdempotencyKey), out var prior))
            {
                if (!StringComparer.Ordinal.Equals(prior.Fingerprint, fingerprint)) throw Error(DiagnosticCodes.ScenarioIdempotencyConflict);
                return FindExecution(command.CompanyId, prior.Id);
            }
            if (executions.Any(x => x.CompanyId == command.CompanyId && x.ScenarioSnapshotId == snapshot.Id && x.Status == ScenarioExecutionStatus.RUNNING)) throw Error(DiagnosticCodes.ScenarioConcurrentExecution);
            result = new(ScenarioExecutionId.From(Guid.NewGuid()), command.CompanyId, snapshot.Id, snapshot.Revision,
                snapshot.ExecutionMode, ScenarioExecutionStatus.RUNNING, null, [], snapshot.ScenarioHash, null,
                snapshot.EngineVersions, timeProvider.GetUtcNow(), null, correlationContext.CorrelationId,
                command.IdempotencyKey, null);
            executions.Add(result); keys[(command.CompanyId, snapshot.Id, command.IdempotencyKey)] = (fingerprint, result.Id);
        }
        try
        {
            var run = await calculations.StartAsync(new(command.CompanyId, snapshot.BaselineSnapshotId,
                snapshot.ExecutionMode, $"scenario:{snapshot.Id.Value:D}:{command.IdempotencyKey}:calculation",
                snapshot.BaselineSnapshotHash, snapshot.PolicyConfiguration, snapshot.HistoricalFacts, snapshot.Id.Value.ToString("D")), token);
            if (run.Status != PayrollCalculationRunStatus.SUCCEEDED) throw Error(run.FailureDiagnosticCode ?? DiagnosticCodes.ScenarioExecutionFailed);
            var fundIds = new List<FundAllocationResultId>();
            foreach (var fund in (snapshot.PolicyConfiguration.FundVersions ?? []).OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.FundVersionId.Value))
            {
                var allocated = await funds.CalculateAsync(new(command.CompanyId, snapshot.BaselineSnapshotId,
                    fund.FundVersionId, snapshot.ExecutionMode, $"scenario:{snapshot.Id.Value:D}:{command.IdempotencyKey}:fund:{fund.FundVersionId.Value:D}",
                    command.FundRequirements ?? [], run.Id, snapshot.Id.Value.ToString("D"), null, fund, snapshot.HistoricalFacts), token);
                fundIds.Add(allocated.Id);
            }
            var hashes = new List<string> { snapshot.ScenarioHash, run.ResultHash ?? string.Empty };
            hashes.AddRange(fundIds.Select(x => funds.GetResult(command.CompanyId, x).ResultHash));
            hashes.AddRange(snapshot.EngineVersions.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"));
            result = result with { Status = ScenarioExecutionStatus.SUCCEEDED, CalculationRunId = run.Id,
                FundResultIds = fundIds, ResultHash = Hash(string.Join("\n", hashes)), CompletedAt = timeProvider.GetUtcNow() };
        }
        catch (Exception exception)
        {
            result = result with { Status = ScenarioExecutionStatus.FAILED, CompletedAt = timeProvider.GetUtcNow(),
                DiagnosticCode = exception is ScenarioException scenario ? scenario.Diagnostic.Code : DiagnosticCodes.ScenarioExecutionFailed };
        }
        lock (gate) { executions[executions.FindIndex(x => x.Id == result.Id)] = result; }
        return result;
    }

    public ScenarioExecutionResultDto GetExecution(CompanyId companyId, ScenarioExecutionId id) { Scope(companyId); return FindExecution(companyId, id); }
    public ScenarioExecutionResultDto? ResolveByIdempotencyKey(CompanyId companyId, ScenarioSnapshotId snapshotId, string key)
    { Scope(companyId); return executions.SingleOrDefault(x => x.CompanyId == companyId && x.ScenarioSnapshotId == snapshotId && x.IdempotencyKey == key); }

    public ScenarioProvenance GetProvenance(CompanyId companyId, ScenarioExecutionId id)
    {
        var execution = GetExecution(companyId, id); var snapshot = GetSnapshot(companyId, execution.ScenarioSnapshotId);
        return new(snapshot.ScenarioDefinitionId, snapshot.Id, snapshot.Revision, snapshot.ScenarioHash,
            snapshot.BaselineSnapshotId, snapshot.BaselineSnapshotHash, snapshot.PolicyOverrides, snapshot.InputOverrides,
            execution.CalculationRunId, execution.FundResultIds, execution.EngineVersions, execution.ExecutionMode, execution.CorrelationId);
    }

    public ScenarioComparison Compare(CompanyId companyId, ReviewResultContext baseline, ReviewResultContext scenario)
    {
        Scope(companyId);
        if (baseline.CompanyId != companyId || scenario.CompanyId != companyId) throw Error(DiagnosticCodes.ScenarioCrossCompanyReference);
        return new(baseline, scenario, reviews.ComparePayrollPeriods(scenario, baseline),
            reviews.GetFundingReview(baseline), reviews.GetFundingReview(scenario));
    }

    private static SnapshotHistoricalFacts ApplyInputOverrides(SnapshotHistoricalFacts facts, IReadOnlyList<ScenarioInputOverride> overrides)
    {
        var inputs = facts.Inputs.ToList();
        foreach (var item in overrides)
        {
            var index = inputs.FindIndex(x => x.PayrollSubjectId == item.PayrollSubjectId && x.PayrollInputDefinitionId == item.PayrollInputDefinitionId);
            var source = inputs[index];
            inputs[index] = source with { ResolvedValue = item.OverrideValue };
        }
        return new(facts.Subjects.ToArray(), inputs.OrderBy(x => x.PayrollSubjectId.Value).ThenBy(x => x.Code, StringComparer.Ordinal).ToArray());
    }

    private static void ValidateOverrides(PayrollCalculationSnapshotDto baseline, SnapshotPolicyConfiguration policy,
        ScenarioPolicyOverride[] policyOverrides, ScenarioInputOverride[] inputOverrides, ScenarioType type)
    {
        if (type == ScenarioType.Replay && (policyOverrides.Length != 0 || inputOverrides.Length != 0)) throw Error(DiagnosticCodes.ScenarioBlockingValidationFailed);
        if (inputOverrides.Length != 0 && type != ScenarioType.WhatIf) throw Error(DiagnosticCodes.ScenarioInvalidInputOverride);
        if (policyOverrides.Any(x => x.OverrideVersionId == Guid.Empty) || policyOverrides.GroupBy(x => (x.PolicyKind, x.Order)).Any(x => x.Count() > 1)) throw Error(DiagnosticCodes.ScenarioInvalidPolicyOverride);
        foreach (var item in inputOverrides)
        {
            if (!baseline.HistoricalFacts.Subjects.Any(x => x.PayrollSubjectId == item.PayrollSubjectId)) throw Error(DiagnosticCodes.ScenarioCrossCompanyReference);
            var source = baseline.HistoricalFacts.Inputs.SingleOrDefault(x => x.PayrollSubjectId == item.PayrollSubjectId && x.PayrollInputDefinitionId == item.PayrollInputDefinitionId);
            if (source is null || source.DataType != item.DataType || source.Unit != item.Unit || item.OverrideValue.DataType != item.DataType || !StringComparer.Ordinal.Equals(source.Code, item.InputCode)) throw Error(DiagnosticCodes.ScenarioInvalidInputOverride);
            if (item.OriginalValue is not null && item.OriginalValue != source.ResolvedValue) throw Error(DiagnosticCodes.ScenarioInvalidInputOverride);
        }
        if (policy.CompensationVersions.Count == 0) throw Error(DiagnosticCodes.ScenarioInvalidPolicyOverride);
    }

    private static string ScenarioHash(CompanyId companyId, ScenarioType type, PayrollCalculationSnapshotDto baseline,
        DateOnly businessDate, SnapshotHistoricalFacts facts, SnapshotPolicyConfiguration policy,
        IReadOnlyList<ScenarioPolicyOverride> policyOverrides, IReadOnlyList<ScenarioInputOverride> inputOverrides,
        IReadOnlyDictionary<string, string> engines)
    {
        var parts = new List<string> { companyId.Value.ToString("D"), type.ToString(), baseline.Id.Value.ToString("D"),
            baseline.SnapshotRevision.ToString(CultureInfo.InvariantCulture), baseline.SnapshotHash,
            businessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        parts.AddRange(facts.Subjects.OrderBy(x => x.PayrollSubjectId.Value).Select(x => $"S|{x.PayrollSubjectId.Value:D}|{x.PayrollAssignmentId.Value:D}"));
        parts.AddRange(facts.Inputs.OrderBy(x => x.PayrollSubjectId.Value).ThenBy(x => x.Code, StringComparer.Ordinal).Select(x => $"I|{x.PayrollSubjectId.Value:D}|{x.PayrollInputDefinitionId.Value:D}|{x.DataType}|{x.Unit}|{Value(x.ResolvedValue)}"));
        parts.AddRange(policy.CompensationVersions.OrderBy(x => x.CompensationSchemeId.Value).Select(x => $"C|{x.CompensationSchemeId.Value:D}|{x.SchemeVersion}"));
        parts.AddRange(policy.FormulaVersions.OrderBy(x => x.FormulaVersionId.Value).Select(x => $"F|{x.FormulaVersionId.Value:D}|{x.Checksum}"));
        parts.AddRange(policy.ParameterVersions.OrderBy(x => x.ParameterSetVersionId.Value).Select(x => $"P|{x.ParameterSetVersionId.Value:D}|{x.Revision}"));
        parts.AddRange(policy.LookupVersions.OrderBy(x => x.LookupTableVersionId.Value).Select(x => $"L|{x.LookupTableVersionId.Value:D}|{x.Revision}"));
        parts.AddRange(policy.RuleSetVersions.OrderBy(x => x.RuleSetVersionId.Value).Select(x => $"R|{x.RuleSetVersionId.Value:D}|{x.Revision}"));
        parts.AddRange((policy.FundVersions ?? []).OrderBy(x => x.FundVersionId.Value).Select(x => $"U|{x.FundVersionId.Value:D}|{x.Revision}"));
        parts.AddRange(policyOverrides.Select(x => $"PO|{x.PolicyKind}|{x.BaselineVersionId:D}|{x.OverrideVersionId:D}|{x.Order}"));
        parts.AddRange(inputOverrides.Select(x => $"IO|{x.PayrollSubjectId.Value:D}|{x.PayrollInputDefinitionId.Value:D}|{x.DataType}|{x.Unit}|{Value(x.OverrideValue)}|{x.Sequence}"));
        parts.AddRange(engines.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"E|{x.Key}|{x.Value}"));
        return Hash(string.Join("\n", parts));
    }

    private ScenarioDefinitionDto FindDefinition(CompanyId companyId, ScenarioDefinitionId id)
    { var item = definitions.SingleOrDefault(x => x.Id == id) ?? throw Error(DiagnosticCodes.ScenarioNotFound); if (item.CompanyId != companyId) throw Error(DiagnosticCodes.ScenarioCrossCompanyReference); return item; }
    private ScenarioSnapshotDto FindSnapshot(CompanyId companyId, ScenarioSnapshotId id)
    { var item = scenarioSnapshots.SingleOrDefault(x => x.Id == id) ?? throw Error(DiagnosticCodes.ScenarioSnapshotNotFound); if (item.CompanyId != companyId) throw Error(DiagnosticCodes.ScenarioCrossCompanyReference); return item; }
    private ScenarioExecutionResultDto FindExecution(CompanyId companyId, ScenarioExecutionId id)
    { var item = executions.SingleOrDefault(x => x.Id == id) ?? throw Error(DiagnosticCodes.ScenarioResultNotFound); if (item.CompanyId != companyId) throw Error(DiagnosticCodes.ScenarioCrossCompanyReference); return item; }
    private void Scope(CompanyId companyId) { if (companyContext.CompanyId != companyId) throw Error(DiagnosticCodes.ScenarioCrossCompanyReference); }
    private static PayrollExecutionMode Mode(ScenarioType type) => type switch { ScenarioType.Replay => PayrollExecutionMode.Replay, ScenarioType.BackTest => PayrollExecutionMode.BackTest, ScenarioType.WhatIf => PayrollExecutionMode.WhatIf, _ => throw new ArgumentOutOfRangeException(nameof(type)) };
    private static string Value(PayrollInputValue value) => value.DataType switch { PayrollInputDataType.DECIMAL => Canonical(value.DecimalValue!.Value), PayrollInputDataType.INTEGER => value.IntegerValue!.Value.ToString(CultureInfo.InvariantCulture), PayrollInputDataType.BOOLEAN => value.BooleanValue!.Value ? "true" : "false", PayrollInputDataType.DATE => value.DateValue!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), PayrollInputDataType.TEXT => value.TextValue ?? string.Empty, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static string Canonical(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static Diagnostic Diagnostic(string code, params (string Key, object? Value)[] values) => new(code, DiagnosticSeverity.Error, values.ToDictionary(x => x.Key, x => x.Value));
    private static ScenarioException Error(string code, params (string Key, object? Value)[] values) => new(Diagnostic(code, values));
}
