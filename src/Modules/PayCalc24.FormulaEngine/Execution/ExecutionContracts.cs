using PayCalc24.Contracts.Diagnostics;
using PayCalc24.Contracts.FormulaRepository;
using PayCalc24.Contracts.Identity;
using PayCalc24.Contracts.Organization;
using PayCalc24.Contracts.PayrollInput;
using PayCalc24.FormulaEngine.Ast;
using PayCalc24.FormulaEngine.Model;

namespace PayCalc24.FormulaEngine.Execution;

public enum FormulaExecutionMode { Production, Replay, BackTest, WhatIf }

public sealed record FormulaProvenance(
    FormulaDefinitionId? FormulaDefinitionId, FormulaVersionId? FormulaVersionId, string? FormulaChecksum,
    IReadOnlyList<ParameterSetVersionId> ParameterSetVersionIds,
    IReadOnlyList<LookupTableVersionId> LookupTableVersionIds,
    IReadOnlyList<RuleSetVersionId> RuleSetVersionIds,
    IReadOnlyList<PayrollInputLedgerEntryId> ReferencedInputEntryIds,
    FormulaExecutionMode ExecutionMode, Guid? ScenarioId, string CorrelationId, string EngineVersion);

public sealed record FormulaExecutionContext(
    CompanyId CompanyId, DateOnly BusinessDate, FormulaExecutionMode ExecutionMode,
    IReadOnlyDictionary<string, FormulaValue> Values, IReadOnlyDictionary<string, FormulaValue>? Parameters,
    string CorrelationId, FormulaDefinitionId? FormulaDefinitionId = null, FormulaVersionId? FormulaVersionId = null,
    string? FormulaChecksum = null, PayrollPeriodId? PayrollPeriodId = null, PayrollSubjectId? PayrollSubjectId = null,
    Guid? ScenarioId = null, IReadOnlyList<ParameterSetVersionId>? ParameterSetVersionIds = null,
    IReadOnlyList<LookupTableVersionId>? LookupTableVersionIds = null, IReadOnlyList<RuleSetVersionId>? RuleSetVersionIds = null,
    IReadOnlyDictionary<string, IReadOnlyList<PayrollInputLedgerEntryId>>? InputEntryIds = null)
{
    internal bool TryResolve(string code, out FormulaValue value) => Values.TryGetValue(code,out value) || (Parameters?.TryGetValue(code,out value) ?? false);
}

public sealed record ExecutionTraceNode(string NodeType, string? Operator = null, string? FunctionName = null,
    string? ReferenceCode = null, string? ResolvedValue = null, string? ResultValue = null,
    FormulaValueType? DataType = null, IReadOnlyList<ExecutionTraceNode>? Children = null, string? DiagnosticCode = null);

public sealed record FormulaEvaluationResult(bool Success, FormulaValue? Value, FormulaValueType? DataType,
    Diagnostic? Diagnostic, ExecutionTraceNode? Trace, FormulaProvenance Provenance);

public sealed record FormulaValidationResult(bool Success, AstNode? Ast, string? CanonicalAstJson,
    FormulaValueType? DataType, IReadOnlyList<Diagnostic> Diagnostics);
